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

//#298 PROBE. The same scene texture read BILINEARLY, for the case the target is SMALLER than the back
//buffer and the resolve is magnifying rather than averaging. Point sampling is right for the box filter
//below and wrong here — magnified, it is nearest-neighbour, which measures the same and looks like
//nothing anyone would ship. A second sampler rather than a second technique: the pair of techniques would
//have to be duplicated whole for one filter state, and the branch that picks between them is on a uniform.
sampler2D SceneSamplerLinear = sampler_state
{
    Texture = <SceneTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//One texel of the HDR source, and how many source texels make up one output pixel along each axis
float2 SourceTexelSize;
int SupersampleFactor;

//#298 PROBE: non-zero when the scene target is smaller than the back buffer, so the resolve magnifies.
//A uniform, so the branch below is non-divergent, and there is no gradient op inside it.
float MagnifyScene;

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

//Lateral chromatic aberration: the peak displacement of the red/blue channels at the frame CORNERS, in
//texcoord units (resolution-independent, the underwater blur's rule), zero disabling the effect and the
//whole branch with it. It grows with the SQUARE of the distance from the centre, so the middle of the
//frame - where the cluster and the gun live - stays perfectly registered and only the periphery fringes,
//which is what a real lens does and why the effect reads as "shot through glass" rather than "broken".
float ChromaticAberration;

//How many samples the spectrum is walked in below. THREE, which is what the three point samples this
//replaced already cost, so the fix is free: each tap is a full SampleScene, and measured on the sea from a
//fixed camera the resolve runs 1.50 ms at three taps, 1.60 at five and 1.69 at seven. Five and seven are not
//distinguishable from three by eye at the strength this ships at, so the extra 0.10 ms buys nothing.
//Must not go below three: the weights below are triangles centred on 0, 0.5 and 1, so a single tap lands on
//the green centre alone and the red and blue weights come out zero - which divides by zero and renders the
//whole frame green. Measured that way round by accident, which is how it is known.
#define ABERRATION_TAPS 3

//Film grain: a monochrome per-output-pixel modulation re-rolled every frame, applied AFTER the tonemap
//curve and before the sRGB encode - grain lives on the print, not in the light - and weighted by
//4*luma*(1-luma), which peaks in the mid-tones and falls to zero at both ends: blacks stay black (no
//lifted noise floor over the space scene's void) and highlights stay clean, which is where cheap grain
//gives itself away. Monochrome because film grain is silver halide, not three dyes disagreeing. Zero
//disables the effect and its branch.
float GrainStrength;
float GrainSeed;      //advanced per frame off the wall clock, so the grain never freezes
float2 OutputSize;    //back-buffer pixels: one grain per OUTPUT pixel, whatever the supersample factor

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

//Where a lens's periphery starts to go soft: the texcoord-distance-from-centre multiplier whose saturate
//reaches 1 two thirds of the way to a corner. ONE figure on purpose, read by both peripheral falloffs -
//the underwater blur's and precise aim's defocus focus (#214) - because they are the same statement about
//the same lens; retune it and both move together.
static const float PERIPHERY_EDGE = 1.5;

//The defocus: a heavily blurred copy of this very scene, built by the pipeline out of the
//target this pass reads (PostProcessPipeline.DrawDefocus) at a quarter of the back buffer per axis. So it
//is the same light, and it is mixed in HERE - in linear radiance, before the curve - which is the whole
//reason a blurred firework stays a glowing orb: averaging tonemapped pixels would spread a spark's clipped
//white into the dark around it and give a grey smudge. The glare is added AFTER, so bright things go on
//blooming through a frame that has gone soft. Zero disables the effect and its branch.
float DefocusAmount;

//How much the defocus is bent into a LENS instead of a wash: 0 = the whole frame takes DefocusAmount
//alike (the result page and the pause, where nothing in the scene is being read), 1 = precise aim's
//periphery-only falloff (#214) - zero dead centre and growing with the square of the distance (the
//aberration's quadratic growth, saturated at PERIPHERY_EDGE's band like the underwater blur), because
//the centre of the frame is where the aimed cluster and its landing ghost live, and the ghost's
//display-pixel dissolve dither does not survive being averaged.
float DefocusFocus;
texture DefocusTexture;
sampler2D DefocusSampler = sampler_state
{
    Texture = <DefocusTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//The sharp foreground layer (#225): linear radiance in its own target, put there precisely so the
//defocus above cannot reach it — the result page presents the won cup over an arena that is melting
//into bokeh, and the cup is the thing being watched. It is composited AFTER the resolve, over the
//finished frame, by the ForegroundComposite technique at the foot of this file — the whole reason it
//is sampled HERE rather than left to the back buffer's own blend is the coverage: the layer's alpha
//is how much of it covers each pixel, and the box filter below has to average that coverage exactly
//as it averages the scene's colour, or a supersampled cup would grow a hard halo of its own edge.
//Point sampling for the same reason as the scene's own read: the filter walks exact texel centers.
texture ForegroundTexture;
sampler2D ForegroundSampler = sampler_state
{
    Texture = <ForegroundTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

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

//One grain: a sine-free hash of the output pixel's cell (Hoskins), branch-safe - no gradients.
float GrainHash(float2 p)
{
    float3 q = frac(p.xyx * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

//The grain, lifted out of MainPS whole when the foreground composite needed it too: grain lives on the
//PRINT, not in the light, so every pixel that leaves this file through the curve leaves through the
//grain as well - a composited layer with none would read as pasted onto the frame rather than in it.
float3 ApplyGrain(float3 mapped, float2 texCoord)
{
    [branch]
    if (GrainStrength > 0.0)
    {
        float2 cell = floor(texCoord * OutputSize);
        float grain = GrainHash(cell + frac(GrainSeed * float2(0.7013, 0.9127)) * 289.0) - 0.5;
        float luma = dot(mapped, float3(0.2126, 0.7152, 0.0722));

        mapped = saturate(mapped + grain * (GrainStrength * 4.0 * luma * (1.0 - luma)));
    }

    return mapped;
}

//Box filter over the block of source texels one output pixel covers. Offsets run from the block's first
//texel center to its last: for a factor of two that is the pixel center plus and minus half a texel, for
//a factor of one it collapses to a single tap at the center. tex2Dlod rather than tex2D so the aberration
//branch below may call it - there are no mips here, and a gradient instruction inside a branch is not
//allowed even when the branch is on a uniform.
float3 SampleScene(float2 uv)
{
    //#298 PROBE: magnifying, so one bilinear tap and no box filter — there is no block of source texels
    //under an output pixel to average, there is less than one.
    [branch] if (MagnifyScene > 0) return tex2Dlod(SceneSamplerLinear, float4(uv, 0, 0)).rgb;

    float3 color = 0;

    for (int y = 0; y < SupersampleFactor; y++)
    {
        for (int x = 0; x < SupersampleFactor; x++)
        {
            float2 offset = (float2(x, y) + 0.5 - SupersampleFactor * 0.5) * SourceTexelSize;
            color += tex2Dlod(SceneSampler, float4(uv + offset, 0, 0)).rgb;
        }
    }

    return color / (SupersampleFactor * SupersampleFactor);
}

//The same box filter over the sharp foreground layer, returning the coverage in the alpha the scene's
//own read throws away. The layer rides PREMULTIPLIED alpha — an opaque shader write carries the pixel's
//full colour whatever partial coverage antialiasing leaves it, and averaging premultiplied samples is
//the one averaging that composites correctly afterwards — so the composite keeps the rgb as it stands.
float4 SampleForeground(float2 uv)
{
    float4 layer = 0;

    for (int y = 0; y < SupersampleFactor; y++)
    {
        for (int x = 0; x < SupersampleFactor; x++)
        {
            float2 offset = (float2(x, y) + 0.5 - SupersampleFactor * 0.5) * SourceTexelSize;
            layer += tex2Dlod(ForegroundSampler, float4(uv + offset, 0, 0));
        }
    }

    return layer / (SupersampleFactor * SupersampleFactor);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float3 color;

    [branch]
    if (ChromaticAberration > 0.0)
    {
        //The red channel is sampled a touch OUTWARD of the pixel and the blue a touch inward - a lens
        //magnifies long wavelengths slightly more - along the direction from the frame centre, scaled by
        //the squared distance so the shift vanishes quadratically towards the middle and reaches
        //ChromaticAberration exactly at the corners: |fromCentre|*dot there is 1/(2*sqrt(2)), which the
        //2.828 undoes. Green anchors the geometry: the eye reads luminance mostly from green, so the
        //image never appears to move when the effect toggles.
        float2 fromCentre = input.TexCoord - 0.5;
        float2 shift = fromCentre * dot(fromCentre, fromCentre) * (ChromaticAberration * 2.828);

        //SPECTRAL, rather than one point sample per channel. Sampling R, G and B at three fixed offsets does
        //not blur an edge into a fringe - it makes THREE DISPLACED COPIES of it, one per channel, and on a
        //line thinner than a pixel that is what the eye reads: three separate coloured lines. The island
        //cap's slab joints are the most regular field of such lines in this game and they read as a
        //flickering rainbow lattice because of it (#126). Four changes to the joint field itself moved that
        //artifact not at all; forcing this effect to zero removed it completely.
        //
        //A real lens disperses a CONTINUUM, so each channel is an integral across its own band rather than
        //one sample from a point. Walking the shift and weighting each tap into RGB by overlapping triangles
        //is that integral. At three taps it costs exactly what the three point samples cost, and it does two
        //things they did not: the red and blue lobes land at 2/3 of the shift instead of all of it, and
        //GREEN - which carries the luminance edge the eye actually reads - is averaged across the whole
        //span instead of being taken from the centre alone.
        float3 spectral = 0.0;
        float3 weightSum = 0.0;

        [unroll]
        for (int c = 0; c < ABERRATION_TAPS; c++)
        {
            //0 at the outward (red) end of the shift, 1 at the inward (blue) end.
            float f = (c + 0.5) / ABERRATION_TAPS;
            float3 weight = saturate(1.0 - abs(f - float3(0.0, 0.5, 1.0)) * 2.0);

            spectral += SampleScene(input.TexCoord + shift * (1.0 - 2.0 * f)) * weight;
            weightSum += weight;
        }

        color = spectral / weightSum;
    }
    else
    {
        color = SampleScene(input.TexCoord);
    }

    //Underwater peripheral blur: a diver's vision goes soft towards the edges. A cheap 8-tap spiral disc blur
    //whose radius and blend both grow from 0 at the frame centre out to the corners (edge*edge, so the centre
    //stays crisp), gated by UnderwaterAmount. The branch is on a uniform, so the whole frame takes the same
    //path (no divergence) and it costs nothing above water; tex2Dlod reads level 0 (there are no mips here).
    if (UnderwaterAmount > 0.0)
    {
        float edge = saturate(length(input.TexCoord - 0.5) * PERIPHERY_EDGE);
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

    //The defocus (see the uniforms), over the sharp frame. What GROWS with the effect is the blurred copy's
    //own radius; this is only how much of it shows, and the pipeline has it reach 1 early - so what the eye
    //follows from there is one image going soft rather than two images crossfading. Branch on a uniform, so
    //the whole frame takes the same path and an unblurred frame costs nothing; tex2Dlod for the reason above.
    [branch]
    if (DefocusAmount > 0.0)
    {
        //DefocusFocus (see its declaration) holds the centre of the frame in focus for precise aim; at 0
        //the lerp is the identity and the whole frame takes DefocusAmount, exactly as before #214
        float edge = saturate(length(input.TexCoord - 0.5) * PERIPHERY_EDGE);
        float blend = DefocusAmount * lerp(1.0, edge * edge, DefocusFocus);

        color = lerp(color, tex2Dlod(DefocusSampler, float4(input.TexCoord, 0, 0)).rgb, blend);
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
    float3 mapped = ACESFilmic(color * Exposure);

    //The grain, on the tonemapped value (see the uniforms). The seed's fraction shifts the hash lattice
    //to a fresh position every frame, which is what makes grain read as film rather than as a dirty pane.
    mapped = ApplyGrain(mapped, input.TexCoord);

    return float4(LinearToSrgb(mapped), 1);
}

//The sharp foreground layer's own exit from linear light (#225): the same exposure, the same ACES
//curve, the same grain and the same sRGB encode as the frame it lands on, so that all the move out of
//the HDR pass changed about the cup is that the defocus no longer takes it. The output alpha is the
//layer's own box-filtered coverage, and the pipeline draws this through premultiplied blending
//(One / InverseSourceAlpha): tonemapping a premultiplied edge and blending it by its coverage is the
//standard resolve, and it is why the edge stays as soft here as it was inside the HDR pass.
//
//Two things the resolve gives the frame are deliberately NOT here. The glare is not, because the layer
//already fed the bloom pyramid its own bright pass — its halo arrives under and around these pixels
//with the frame's, and adding it again would double it. And the underwater murk is not, because the
//layer exists to hold one presented object out of an effect that belongs to the whole frame; nothing
//the game presents through it ever stands in water.
float4 ForegroundCompositePS(VertexShaderOutput input) : COLOR
{
    float4 layer = SampleForeground(input.TexCoord);

    float3 mapped = ACESFilmic(layer.rgb * Exposure);
    mapped = ApplyGrain(mapped, input.TexCoord);

    return float4(LinearToSrgb(mapped), layer.a);
}

technique Tonemap
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

technique ForegroundComposite
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL ForegroundCompositePS();
    }
};
