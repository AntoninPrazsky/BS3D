//Turns the brightest parts of the HDR scene into visible glare: a bright pass that keeps only what
//exceeds a threshold, then a MULTI-SCALE BLOOM PYRAMID - the bright image downsampled level by level to
//a thirty-second of the frame and accumulated back up, so a light source wears a halo that is tight and
//hot at its core and melts away over half the screen at its widest. This replaced a six-armed streak
//star (#69): a star filter reads as a lens-flare pack from the nineties, where the wide soft pyramid is
//how every modern renderer spends its glow - and the pyramid's dense sampling also fixed the star's one
//real fault, small bright points flickering in and out of a sparse quarter-resolution grid.
//
//This is still deliberately not a physical camera model. The point is to make it unmistakable that the
//balls, the neon, the crystals and the orbs are light sources rather than lit objects; the pyramid just
//says it in this decade's accent.
//
//The down/up kernels are the "dual filter" pair (Bjorge, SIGGRAPH 2015): a 5-tap downsample and a 9-tap
//tent upsample, each pass at half the previous resolution, additively blended on the way back up. Cheap
//enough to be invisible in the frame budget and free of the boxy artifacts a plain bilinear chain shows.

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

//One texel of the SOURCE being read (each pass reads the level it consumes, so this changes per pass)
float2 SourceTexelSize;

//Radiance above which a pixel starts to glare, and how sharply it ramps in past that
float GlareThreshold;

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

//The downsample: four corner taps half a source texel out, around a centre tap weighted as four. The
//half-texel offsets put every tap on a bilinear seam, so each is already an average of four texels - the
//13 effective texels per output pixel are what stops a bright dot strobing as it crosses the coarser
//grid, which is exactly the artifact the old quarter-resolution star suffered.
float4 BloomDownPS(VertexShaderOutput input) : COLOR
{
	float2 h = SourceTexelSize;

	float3 sum = tex2D(SourceSampler, input.TexCoord).rgb * 4.0;
	sum += tex2D(SourceSampler, input.TexCoord + float2(-h.x, -h.y)).rgb;
	sum += tex2D(SourceSampler, input.TexCoord + float2(h.x, -h.y)).rgb;
	sum += tex2D(SourceSampler, input.TexCoord + float2(-h.x, h.y)).rgb;
	sum += tex2D(SourceSampler, input.TexCoord + float2(h.x, h.y)).rgb;

	return float4(sum / 8.0, 1);
}

technique BloomDown
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL BloomDownPS();
	}
};

//The upsample: a 9-tap tent - four side taps a full texel out, four diagonal taps half a texel out at
//double weight - drawn ADDITIVELY into the next-larger level, so each level keeps its own detail and
//gains the wider halo of everything below it. The tent is what keeps the accumulated halo round; a plain
//bilinear upsample stacks its box footprints into visible squares around every hot point.
float4 BloomUpPS(VertexShaderOutput input) : COLOR
{
	float2 h = SourceTexelSize;

	float3 sum = 0;
	sum += tex2D(SourceSampler, input.TexCoord + float2(-h.x * 2.0, 0.0)).rgb;
	sum += tex2D(SourceSampler, input.TexCoord + float2(h.x * 2.0, 0.0)).rgb;
	sum += tex2D(SourceSampler, input.TexCoord + float2(0.0, -h.y * 2.0)).rgb;
	sum += tex2D(SourceSampler, input.TexCoord + float2(0.0, h.y * 2.0)).rgb;
	sum += (tex2D(SourceSampler, input.TexCoord + float2(-h.x, -h.y)).rgb
		+ tex2D(SourceSampler, input.TexCoord + float2(h.x, -h.y)).rgb
		+ tex2D(SourceSampler, input.TexCoord + float2(-h.x, h.y)).rgb
		+ tex2D(SourceSampler, input.TexCoord + float2(h.x, h.y)).rgb) * 2.0;

	return float4(sum / 12.0, 1);
}

technique BloomUp
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL BloomUpPS();
	}
};
