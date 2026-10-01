//The impossible shot's wormhole (#230): a small vortex torn open in the air outside the arena, swallowing the shots
//that missed into it, then snapping shut with a pop. One camera-facing quad, everything drawn in its pixel shader
//from four uniforms - there is only ever one hole, and it is up for about two seconds in a blue moon.
//
//Three parts, all in polar coordinates round the quad's centre, measured in the hole's own radii:
//  the SWIRL  - filaments of light winding into the centre, violet at the rim through magenta to cyan at the throat,
//               three sets at different counts and pitches over one another so the pattern never reads as a printed
//               disc (the owner's rule that a perfect surface looks synthetic, #640), sparks drawn down them, and one
//               thin pale ring round the whole - the portal's edge, which the design references all had
//  the THROAT - a black disc ringed by one thin violet line: the only dark thing this file draws, which is why it is
//               premultiplied and not additive - an additive pass cannot put black over the sky
//  the POP    - a white flash with four rays when it shuts, drawn at the quad's own scale so it outlives the swirl
//
//⚠ THE COLOURS ARE KEPT LOW, and the first capture is why: driven at 3-7 the swirl came out a pale pink-white smear
//(the tonemap's shoulder takes the hue off anything that bright) and its glare filled the throat with grey. The arms
//peak at about 2 now and the pale edge at about 1.3 - still over the glare's threshold (a luminance of 0.55, see
//BS3DGame), so they glow a little, but no longer enough to wash the hue out or fog the throat; the sparks and the
//pop are the parts driven far over it, to bloom.
//
//Premultiplied alpha (BlendState.AlphaBlend), depth-read and writing no depth, in linear radiance. SM 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

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

//The arms: how many, and how tightly they wind - radians of turn per e-fold of radius, so the spiral is
//logarithmic and keeps one shape at every size, as a vortex down a drain does. Broad arms, then fine filaments.
static const float ARMS = 3.0;
static const float TWIST = 2.4;
static const float ARMS_B = 7.0;
static const float TWIST_B = 3.1;
static const float ARMS_C = 12.0;
static const float TWIST_C = 3.8;

//The sparks: cells in (log radius, angle along the arms) that drift inwards with the spin, a spark in some of them
static const float SPARK_RINGS = 7.0;     //cells per unit of log radius
static const float SPARK_SPOKES = 18.0;   //cells round the circle - an integer, for the atan2 seam

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

//How much of what is behind the swirl it takes away, so its colours sit on a darkened disc rather than being
//added over a bright sky and washed out by it
static const float SWIRL_DIM = 0.85;

struct WormholeVertexInput
{
    float2 Corner : TEXCOORD0;   //-1..1 across the quad
};

struct WormholeVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Polar : TEXCOORD0;    //the corner in hole radii at full size
};

WormholeVertexOutput WormholeVS(WormholeVertexInput input)
{
    WormholeVertexOutput output;

    float3 world = HoleCentre.xyz + (CameraRight * input.Corner.x + CameraUp * input.Corner.y) * HoleCentre.w;
    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Polar = input.Corner * QUAD_SPAN;
    return output;
}

float4 WormholePS(WormholeVertexOutput input) : COLOR0
{
    float2 p = input.Polar;
    float spin = HoleState.x;
    float size = HoleState.y;
    float flash = HoleState.z;

    float3 light = float3(0.0, 0.0, 0.0);
    float dark = 0.0;

    if (size > 0.001)
    {
        float2 q = p / size;
        float r = length(q);
        float theta = atan2(q.y, q.x);
        float logR = log(max(r, 0.001));

        //Integer arm counts, so the atan2 branch cut at ±pi is seamless. Broad arms, then fine filaments over them
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

        //The cell round the circle is hashed by its place modulo SPARK_SPOKES (#230's review): the integer count keeps
        //frac(cell) continuous across atan2's cut at ±pi, but the id there jumps by exactly SPARK_SPOKES, and hashed as
        //it is, every spark crossing the cut - they all do, drawn round by the spin - vanished or turned into another,
        //a seam from the throat to the rim on the hole's left
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

    return float4(light, dark);
}

technique Wormhole
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL WormholeVS();
        PixelShader = compile PS_SHADERMODEL WormholePS();
    }
};
