//GamePi's SM 3.0 port of Prazsky.Shaders' Wormhole.fx (#789): the impossible shot's wormhole (#230), a small vortex
//torn open in the air outside the arena. It is one camera-facing quad placed from HoleCentre and drawn entirely in its
//pixel shader from HoleState, compiled for OpenGL through vs_3_0/ps_3_0 and MojoShader. What changed, and why:
//  - POSITION0 on the vertex output, because SM 3.0 has no SV_POSITION, and a pixel input without it, because a
//    ps_3_0 shader may not read POSITION.
//  - The swirl's p / size divides by a clamped size. It sits inside the "is the hole open" branch, but fxc may flatten
//    that branch into a blend of both sides, and a shut hole's 0/0 would then turn the pop's pixels NaN.
//  - The output goes through the display curve (PotatoOutput.fxh), because Potato draws straight into the 8-bit back
//    buffer. Only the light is curved: rgb here is light added and alpha is how much of the backdrop is taken away,
//    not a premultiplied colour (see the return).
//Everything drawn is the desktop's: the swirl, the sparks, the throat, the edge and the pop. So are the uniforms
//Wormhole.cs fetches, plus PotatoOutput's Exposure.
//
//Three parts, all in polar coordinates round the quad's centre, measured in the hole's own radii:
//  the SWIRL  - filaments of light winding into the centre, violet at the rim through magenta to cyan at the throat,
//               three sets at different counts and pitches over one another so the pattern never reads as a printed
//               disc (#640), sparks drawn down them, and one thin pale ring round the whole, the portal's edge
//  the THROAT - a black disc ringed by one thin violet line: the only dark thing this file draws, which is why it is
//               premultiplied and not additive (an additive pass cannot put black over the sky)
//  the POP    - a white flash with four rays when it shuts, drawn at the quad's own scale so it outlives the swirl
//
//THE COLOURS ARE KEPT LOW, which matters here as much as on the desktop. Potato ends on the same ACES curve, and that
//curve's shoulder takes the hue off anything driven much over 1, so the swirl would come out a pale pink-white smear.
//The arms peak at about 2 and the pale edge at about 1.3. The sparks and the pop are driven far higher so the desktop's
//glare would bloom them; Potato has no glare, so here they only reach the curve's white core.
//
//Drawn with BlendState.AlphaBlend (One/InverseSourceAlpha), depth-read and writing no depth.

#include "PotatoOutput.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraRight;   //view basis, handed over rather than derived: the hole faces the lens squarely
float3 CameraUp;

//HoleCentre.xyz  where the hole hangs, world space
//HoleCentre.w    the quad's half-size in world units: the hole's full radius times QUAD_SPAN (Wormhole.cs)
//HoleState.x     the swirl's spin, radians - accumulated on the C# side so its rate can change without a jump
//HoleState.y     the hole's size, 0 shut, 1 fully open, a little over 1 on the opening's overshoot
//HoleState.z     the pop's flash, 1 the instant it shuts, falling to 0
float4 HoleCentre;
float4 HoleState;

//The quad's half-size in hole radii. Must match Wormhole.QUAD_SPAN: room for the opening's overshoot and the pop's rays.
static const float QUAD_SPAN = 1.6;

//The throat, in hole radii, and how wide the line round it is; and the portal's own edge, the pale ring
static const float THROAT = 0.3;
static const float RIM_WIDTH = 0.03;
static const float EDGE = 0.96;
static const float EDGE_WIDTH = 0.02;

//The arms: how many, and how tightly they wind - radians of turn per e-fold of radius, so the spiral is logarithmic
//and keeps one shape at every size, as a vortex down a drain does. Broad arms, then fine filaments.
static const float ARMS = 3.0;
static const float TWIST = 2.4;
static const float ARMS_B = 7.0;
static const float TWIST_B = 3.1;
static const float ARMS_C = 12.0;
static const float TWIST_C = 3.8;

//The sparks: cells in (log radius, angle along the arms) that drift inwards with the spin, a spark in some of them
static const float SPARK_RINGS = 7.0;     //cells per unit of log radius
static const float SPARK_SPOKES = 18.0;   //cells round the circle - a whole number, for the atan2 seam

//The palette, rim to throat, normalised to a peak of 1 and driven by the radiances below
static const float3 RIM_HUE = float3(0.36, 0.1, 1.0);
static const float3 MID_HUE = float3(1.0, 0.16, 0.8);
static const float3 HOT_HUE = float3(0.2, 0.85, 1.0);
static const float3 EDGE_HUE = float3(0.9, 0.85, 1.0);

static const float SWIRL_RADIANCE = 1.2;
static const float THROAT_RIM_RADIANCE = 1.6;
static const float EDGE_RADIANCE = 1.5;
static const float SPARK_RADIANCE = 6.0;
static const float POP_RADIANCE = 10.0;

//How much of what is behind the swirl it takes away, so its colours sit on a darkened disc rather than being added
//over a bright sky and washed out by it
static const float SWIRL_DIM = 0.85;

struct WormholeVertexInput
{
    float2 Corner : TEXCOORD0;   //-1..1 across the quad
};

struct WormholeVertexOutput
{
    float4 Position : POSITION0;
    float2 Polar : TEXCOORD0;    //the corner in hole radii at full size
};

//What the pixel shader reads: the vertex output without its position
struct WormholePixelInput
{
    float2 Polar : TEXCOORD0;
};

WormholeVertexOutput WormholeVS(WormholeVertexInput input)
{
    WormholeVertexOutput output;

    float3 world = HoleCentre.xyz + (CameraRight * input.Corner.x + CameraUp * input.Corner.y) * HoleCentre.w;
    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Polar = input.Corner * QUAD_SPAN;
    return output;
}

float4 WormholePS(WormholePixelInput input) : COLOR0
{
    float2 p = input.Polar;
    float spin = HoleState.x;
    float size = HoleState.y;
    float flash = HoleState.z;

    float3 light = float3(0.0, 0.0, 0.0);
    float dark = 0.0;

    if (size > 0.001)
    {
        //The divisor is clamped although the branch guards it (see the header)
        float2 q = p / max(size, 0.001);
        float r = length(q);
        float theta = atan2(q.y, q.x);
        float logR = log(max(r, 0.001));

        //Whole-number arm counts, so the atan2 branch cut at +-pi is seamless. Broad arms, then fine filaments over them
        float armsA = 0.5 + 0.5 * cos(ARMS * (theta + TWIST * logR) - spin);
        float armsB = 0.5 + 0.5 * cos(ARMS_B * (theta + TWIST_B * logR) - spin * 1.37 + 1.3);
        float armsC = 0.5 + 0.5 * cos(ARMS_C * (theta + TWIST_C * logR) - spin * 1.9 + 4.1);
        //saturate()d before the pow: a cos that overshoots -1 by a rounding would hand pow a negative base and a NaN
        float arms = saturate(0.55 * armsA * armsA + 0.5 * pow(saturate(armsB), 6.0) + 0.35 * pow(saturate(armsC), 10.0));

        //Gone at the rim, gone again just outside the throat; hotter inwards
        float envelope = smoothstep(1.0, 0.5, r) * smoothstep(THROAT, THROAT + 0.08, r);
        float heat = saturate((1.0 - r) / (1.0 - THROAT));

        float3 hue = lerp(RIM_HUE, MID_HUE, saturate(heat * 1.8));
        hue = lerp(hue, HOT_HUE, saturate(heat * heat * 1.4 - 0.25));

        light += hue * (SWIRL_RADIANCE * (0.5 + 1.2 * heat) * (0.15 + 0.85 * arms) * envelope);

        //Sparks drawn down the spiral: a lattice of cells in (log radius, angle along the arms) sliding inwards
        float2 cell = float2((logR + spin * 0.12) * SPARK_RINGS, (theta + TWIST * logR) / 6.2831853 * SPARK_SPOKES);
        float2 id = floor(cell);

        //The cell round the circle is hashed by its place modulo SPARK_SPOKES (#230's review). frac(cell) is continuous
        //across atan2's cut at +-pi, but the id jumps there by exactly SPARK_SPOKES, and hashed as it is every spark
        //crossing the cut would vanish or turn into another, a seam from the throat to the rim. The modulo is a float
        //one (a floor), as SM 3.0 has no integer remainder.
        id.y = id.y - SPARK_SPOKES * floor(id.y / SPARK_SPOKES);
        float2 jitter = frac(sin(float2(dot(id, float2(127.1, 311.7)), dot(id, float2(269.5, 183.3)))) * 43758.5453);
        float2 local = frac(cell) - (0.2 + 0.6 * jitter);
        float spark = exp(-dot(local, local) * 160.0) * step(0.55, jitter.x * 0.6 + jitter.y * 0.4);
        light += lerp(EDGE_HUE, HOT_HUE, heat) * (SPARK_RADIANCE * spark * envelope);

        //The throat: black inside, one violet line round it; and the portal's pale edge
        float rim = (r - THROAT) / RIM_WIDTH;
        light += MID_HUE * (THROAT_RIM_RADIANCE * exp(-rim * rim));
        float edge = (r - EDGE) / EDGE_WIDTH;
        light += EDGE_HUE * (EDGE_RADIANCE * exp(-edge * edge));

        float throat = 1.0 - smoothstep(THROAT - 0.03, THROAT + 0.01, r);

        //And the swirl takes away much of what is behind it, so the disc reads as a hole in the sky
        dark = max(throat, SWIRL_DIM * smoothstep(1.02, 0.9, r));
        light *= 1.0 - throat;
    }

    if (flash > 0.0)
    {
        float d = length(p);
        float angle = atan2(p.y, p.x);

        float glow = exp(-d * 4.0);
        float rays = pow(abs(cos(2.0 * angle)), 48.0) * exp(-d * 2.4);
        float fade = smoothstep(QUAD_SPAN, QUAD_SPAN * 0.7, d);

        light += float3(1.0, 0.96, 1.0) * (POP_RADIANCE * flash * flash * (glow + 0.7 * rays) * fade);
    }

    //THE OUTPUT. AlphaBlend keeps (1 - dark) of the backdrop and adds light on top, so light is added radiance and dark
    //is a cover; light is not a colour premultiplied by dark. So the light alone goes through the curve, as an additive
    //effect's added light does (ShotTrail.fx's port explains why), and dark goes out unchanged. Over a dark sky that
    //matches the desktop's curve exactly (its glare aside). Curving light / dark and multiplying dark back in would
    //divide the light by dark, and the pop's rays outside the swirl, which take nothing away, would vanish.
    return float4(ToDisplay(light), dark);
}

technique Wormhole
{
    pass P0
    {
        VertexShader = compile vs_3_0 WormholeVS();
        PixelShader = compile ps_3_0 WormholePS();
    }
};
