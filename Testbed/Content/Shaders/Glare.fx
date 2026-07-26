//Turns the brightest parts of the HDR scene into visible glare: a bright pass that keeps only what
//exceeds a threshold, then a star of directional streaks smeared out of it.
//
//This is deliberately not a physical camera model. Real glare comes from diffraction in a lens or an
//eye, and reproducing it faithfully would give a faint halo nobody notices. The point here is to make
//it unmistakable that the balls are light sources rather than lit objects, so the streaks are long,
//bright and cheerfully exaggerated. Both passes run at quarter resolution - glare is the one thing in
//the frame that is supposed to be blurry.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

texture SourceTexture;
sampler2D SourceSampler = sampler_state
{
	Texture = <SourceTexture>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = None;
	AddressU = Clamp;
	AddressV = Clamp;
};

//One texel of the source, so the streaks can walk it in pixel steps
float2 SourceTexelSize;

//Radiance above which a pixel starts to glare, and how sharply it ramps in past that
float GlareThreshold;

//Length of a streak arm in source texels (screen space — the quarter-resolution bright pass), and how
//fast it fades along its length
float StreakLength;
float StreakFalloff;

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput MainVS(float3 position : POSITION0, float2 texCoord : TEXCOORD0)
{
	VertexShaderOutput output;

	output.Position = float4(position, 1);
	output.TexCoord = texCoord;

	return output;
}

//Keeps the excess over the threshold rather than the whole pixel, so a surface that merely sits at the
//threshold contributes nothing and only genuinely bright things bloom. Working on the excess also means
//the glare grows smoothly as a ball's pulse rises instead of switching on.
float4 BrightPassPS(VertexShaderOutput input) : COLOR
{
	float3 color = tex2D(SourceSampler, input.TexCoord).rgb;

	//Luminance decides whether it glares; the color it glares with is the pixel's own, which is what
	//keeps a red ball's glare red instead of bleaching everything to white
	float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
	float excess = max(luminance - GlareThreshold, 0);

	return float4(color * (excess / max(luminance, 1e-4)), 1);
}

technique BrightPass
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL BrightPassPS();
	}
};

//Six arms rather than four: an even star reads as a rendering artifact, a six-point one reads as glare.
//Each arm is walked in fixed steps with an exponential falloff, which is what a diffraction streak
//actually looks like - bright at the source and trailing off, not a uniform line.
static const int StreakArms = 6;
static const int StreakSamples = 16;

float4 StreakPS(VertexShaderOutput input) : COLOR
{
	//Each arm is normalized against its own weights and the arms are then combined by taking the
	//brightest, not by averaging them together. Summing every tap and dividing by the total weight of all
	//six arms - the obvious way to write this - divides each ray by six times the samples it actually
	//has, and the star collapses into a soft round blob. Taking the max keeps the arms as arms.
	float3 brightest = 0;

	[unroll]
	for (int arm = 0; arm < StreakArms; arm++)
	{
		//Half a turn spread over the arms; each is then walked both ways, since a streak runs out from
		//its source in both directions
		float angle = 3.14159265 * arm / StreakArms;
		float2 direction = float2(cos(angle), sin(angle));

		float3 total = tex2D(SourceSampler, input.TexCoord).rgb;
		float weightSum = 1;

		[unroll]
		for (int i = 1; i <= StreakSamples; i++)
		{
			float fraction = (float)i / StreakSamples;
			float weight = exp(-StreakFalloff * fraction);

			float2 offset = direction * (StreakLength * fraction) * SourceTexelSize;

			total += (tex2D(SourceSampler, input.TexCoord + offset).rgb
				+ tex2D(SourceSampler, input.TexCoord - offset).rgb) * weight;

			weightSum += 2 * weight;
		}

		brightest = max(brightest, total / weightSum);
	}

	return float4(brightest, 1);
}

technique Streak
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL StreakPS();
	}
};
