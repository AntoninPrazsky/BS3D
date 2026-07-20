//Resolves the HDR scene target onto the back buffer. Three jobs in one full-screen pass:
//box-filters the supersampled image, maps the open-ended linear radiance the scene shader now writes
//down into the 0-1 the display can show, and encodes the result to sRGB.
//
//This pass is the only place the renderer leaves linear light. Everything drawn before it works in
//linear radiance, where adding two lights or averaging two samples means what it says; everything drawn
//after it (the text overlay, the aimer) is authored in display space and goes straight to the back
//buffer. Only the Testbed builds this file - the map editor has no HDR target to resolve.

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
