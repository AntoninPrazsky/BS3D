//Resolves the HDR scene target onto the back buffer. Three jobs in one full-screen pass:
//box-filters the supersampled image, maps the open-ended linear radiance the scene shader now writes
//down into the 0-1 the display can show, and encodes the result to sRGB.
//
//This pass is the only place the renderer leaves linear light. Everything drawn before it works in
//linear radiance, where adding two lights or averaging two samples means what it says; everything drawn
//after it (the text overlay, the aimer) is authored in display space and goes straight to the back
//buffer. All three executables build this file out of this directory and resolve their HDR targets
//through it (the editor leaves UnderwaterAmount at zero).

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

texture SceneTexture;
sampler2D SceneSampler = sampler_state
{
	Texture = <SceneTexture>;
	//Point sampling on purpose: the box filter below reads exact texel centers, so letting the hardware
	//interpolate would only blur the samples into each other before they are averaged.
	MinFilter = Point;
	MagFilter = Point;
	MipFilter = None;
	AddressU = Clamp;
	AddressV = Clamp;
};

//One texel of the HDR source, and how many source texels make up one output pixel along each axis
float2 SourceTexelSize;
int SupersampleFactor;

//Linear scale applied before the tonemap curve - the renderer's "shutter speed"
float Exposure;

//Quarter-resolution glare, added back before the tonemap curve so it blows the highlights out through
//the curve the way a real over-bright source does, rather than being pasted on top of a finished image
texture GlareTexture;
sampler2D GlareSampler = sampler_state
{
	Texture = <GlareTexture>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = None;
	AddressU = Clamp;
	AddressV = Clamp;
};

float GlareIntensity;

//Underwater tint. When the camera dips below the sea surface the whole frame is pulled into a blue-green
//murk so it reads as being submerged rather than showing the world unchanged with a water plane cutting
//through it. Applied in LINEAR light before the curve, as a cheap water column: the scene is absorbed
//towards a tint (red goes first, so it blues) and the water's own in-scattered glow is added, both ramped
//by UnderwaterAmount = how far under the surface the camera is (0 above water, 1 a few units down). There
//is no per-pixel depth here, so it is uniform over the frame - good enough, and it fades in smoothly as you
//dive. Zero in every scene but the sea, where the camera cannot get under the water.
float UnderwaterAmount;
float3 UnderwaterAbsorb;      //linear multiplier < 1: dims and shifts the scene blue-green
float3 UnderwaterInscatter;   //linear add: the ambient water glow, so the murk is never pure black

//Peak radius (in texcoord units, i.e. fraction of the frame) of the underwater peripheral blur, reached in
//the corners. Resolution-independent, so the blur looks the same at 720p and 4K.
static const float UNDERWATER_BLUR_RADIUS = 0.014;

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
};

//The quad arrives already in clip space, so there is nothing to transform
VertexShaderOutput MainVS(float3 position : POSITION0, float2 texCoord : TEXCOORD0)
{
	VertexShaderOutput output;

	output.Position = float4(position, 1);
	output.TexCoord = texCoord;

	return output;
}

//Krzysztof Narkowicz's fit of the ACES filmic curve. A tonemap curve is what lets the scene hold
//detail in a bright sky and a dark corner at the same time: it compresses the highlights gently
//instead of letting everything above 1 clip flat to white, which is what a linear renderer without
//one does to every lit surface.
float3 ACESFilmic(float3 x)
{
	const float a = 2.51;
	const float b = 0.03;
	const float c = 2.43;
	const float d = 0.59;
	const float e = 0.14;

	return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

//Linear radiance to sRGB. The exact piecewise curve rather than a 1/2.2 power: the toe near black is
//where the difference shows, and a night scene lives there.
float3 LinearToSrgb(float3 c)
{
	c = max(c, 0);

	return lerp(c * 12.92, 1.055 * pow(c, 1.0 / 2.4) - 0.055, step(0.0031308, c));
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	//Box filter over the block of source texels this output pixel covers. Offsets run from the block's
	//first texel center to its last: for a factor of two that is the pixel center plus and minus half a
	//texel, for a factor of one it collapses to a single tap at the center.
	float3 color = 0;

	for (int y = 0; y < SupersampleFactor; y++)
	{
		for (int x = 0; x < SupersampleFactor; x++)
		{
			float2 offset = (float2(x, y) + 0.5 - SupersampleFactor * 0.5) * SourceTexelSize;
			color += tex2D(SceneSampler, input.TexCoord + offset).rgb;
		}
	}

	color /= SupersampleFactor * SupersampleFactor;

	//Underwater peripheral blur: a diver's vision goes soft towards the edges. A cheap 8-tap spiral disc blur
	//whose radius and blend both grow from 0 at the frame centre out to the corners (edge*edge, so the centre
	//stays crisp), gated by UnderwaterAmount. The branch is on a uniform, so the whole frame takes the same
	//path (no divergence) and it costs nothing above water; tex2Dlod reads level 0 (there are no mips here).
	if (UnderwaterAmount > 0.0)
	{
		float edge = saturate(length(input.TexCoord - 0.5) * 1.5);
		float blend = UnderwaterAmount * edge * edge;
		float radius = blend * UNDERWATER_BLUR_RADIUS;

		float3 disc = 0.0;
		[unroll]
		for (int t = 0; t < 8; t++)
		{
			float f = (t + 0.5) / 8.0;
			float ang = 2.3999632 * t;                    //golden angle -> an even spiral, no hexagonal ghosting
			float2 off = float2(cos(ang), sin(ang)) * (sqrt(f) * radius);
			disc += tex2Dlod(SceneSampler, float4(input.TexCoord + off, 0, 0)).rgb;
		}

		color = lerp(color, disc * 0.125, blend);
	}

	//Glare goes in here, in linear light and before the curve. Added after the curve it would look like
	//a decal; added here it pushes the pixels it lands on up the highlight roll-off, so a glaring ball
	//bleaches towards white through the same response as everything else.
	color += tex2D(GlareSampler, input.TexCoord).rgb * GlareIntensity;

	//Underwater murk (see the uniforms): absorb the scene towards the water tint and add its in-scattered
	//glow, ramped by how deep the camera is under the surface. In linear light, before the curve, so the
	//submerged scene rolls through the same highlight response as everything else. A no-op above water.
	color = lerp(color, color * UnderwaterAbsorb + UnderwaterInscatter, UnderwaterAmount);

	//Averaging happens in linear light, before the curve: averaging tonemapped samples would average
	//display values, which is the same mistake as compositing in gamma space.
	return float4(LinearToSrgb(ACESFilmic(color * Exposure)), 1);
}

technique Tonemap
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
