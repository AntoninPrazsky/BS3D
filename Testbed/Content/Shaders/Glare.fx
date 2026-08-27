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
//
//The file also carries the DEFOCUS blur at the foot, which is not glare at all: it blurs the whole frame
//rather than only the bright parts of it, for the moment a level ends (see PostProcessPipeline.DrawDefocus).
//It lives here because it wants exactly what the pyramid wants - the same full-screen quad, the same linear
//clamped sampler, the same downsample kernel to get to a working resolution - and a second effect file
//would be a third content entry in every executable for one pixel shader.

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

//One axis of a separable Gaussian, drawn twice - across, then down - to take the whole scene out of focus.
//This is the end-of-level effect: the arena melts behind the result screen while the fireworks go on
//flaring in it (see PostProcessPipeline.DrawDefocus, which runs it on a quarter-resolution copy of the
//frame so thirteen taps reach four times as far).
//
//The TAP COUNT IS FIXED and the spacing grows instead, which is what lets the blur widen smoothly out of
//nothing: a kernel that gained taps as it widened would step, and one that only crossfaded a fixed-radius
//blur over the sharp frame would read as a double exposure rather than as a lens going soft.
float2 BlurStep;   //one tap's offset in texcoords - the direction and the spacing in one vector

//A Gaussian of sigma 2.2 taps, truncated at +-6 (about 2.7 sigma) and normalized so the pass conserves the
//image's brightness at every spacing. It runs in linear radiance, where a kernel that does not sum to one
//dims or blows the frame outright rather than merely shifting its tone.
static const float BLUR_WEIGHTS[7] = { 0.18205, 0.16413, 0.12037, 0.07183, 0.03468, 0.01362, 0.00435 };

float4 DefocusBlurPS(VertexShaderOutput input) : COLOR
{
    float3 sum = tex2D(SourceSampler, input.TexCoord).rgb * BLUR_WEIGHTS[0];

    //Symmetric pairs, so seven weights buy a thirteen-tap kernel. The sampler clamps, so the taps that fall
    //off an edge repeat its border texel - the frame's outermost row, blurred with itself.
    [unroll]
    for (int i = 1; i < 7; i++)
    {
        float2 offset = BlurStep * i;

        sum += (tex2D(SourceSampler, input.TexCoord + offset).rgb
            + tex2D(SourceSampler, input.TexCoord - offset).rgb) * BLUR_WEIGHTS[i];
    }

    return float4(sum, 1);
}

technique DefocusBlur
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL DefocusBlurPS();
    }
};
