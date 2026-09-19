//The visible fire of the savanna's campfires (#282, #468): one upright billboard per fire, the pixel shader
//a procedural FIRE - several tongues that split and rejoin over a turbulent body, a broad yellow-white base
//over the hearth narrowing into orange licks with red tips and detached red shreds - and, in a second
//technique over a small shared buffer of billboards, the sparks rising out of it. Both drawn additively and
//bright (radiance well over 1), so they glow and bloom through the glare pass the way a real fire throws
//light. The illumination a fire casts on the ground, the balls and the island is a separate scene point
//light (see AddSceneLights); this is only the source you see. Depth-read (the terrain or platform in front
//hides it) but writes no depth. Testbed-shared, SM 5.0.
//
//Until #468 this was ONE tongue: a centre line wobbled by two sines, a width of (1 - v), amber core to
//orange edge, no red anywhere and no structure inside - exactly a candle or a lighter, which is what the
//owner saw. The references rendered for it (468-campfire-photo, 468-campfire-concept) are the brief: a wide
//bright base, tongues that break apart higher up, red only at the outer tips and in what detaches, sparks.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//The body's turbulence: gradient-noise fBm scrolled upward, the same Noise.fxh field the terrain uses.
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 FlamePosition; //Base of the flame on the ground
float FlameSize;

//The billboard's height, as a multiple of FlameSize. A uniform rather than the 2.4 that used to be written
//here, because height and width have to move independently: the game's camera sits about level with the
//island's stone while the fires stand on grass some five units below it, so a flame has to be tall to be
//seen at all - and a flame made tall by growing FlameSize is a bonfire.
float FlameHeightScale;
float FlameTime;

//Per-fire rate stretch, 1 for the first fire and a few per cent more for each one after it. The caller
//already offsets FlameTime per fire; on its own that would leave every flame licking the IDENTICAL pattern
//a moment apart, which the eye reads as one wave travelling round the island as soon as two are in shot.
//Stretching the rates as well means no two are ever running the same shape, however long they burn. It
//also offsets the noise domain, so two fires never share a turbulence either.
float FlameSeed;

//The fire's three colours, linear radiance over 1 so they bloom: the red of the outer tips and the shreds,
//the orange of the body, the yellow-white of the core and the base. Kept from blowing to pure white (the
//blue stays under the red) so it reads as fire, not as a lamp.
//⚠ Measured on the first cut: (2.6, 2.0, 0.85) for the core with the orange at 2.0 read as a white column
//under a daytime dome - additive over a bright sky plus the tonemap's shoulder takes everything over ~1.5
//to white and the red never showed. These are a stop lower, and the core is confined to the base below.
static const float3 FIRE_RED = float3(0.95, 0.07, 0.01);
static const float3 FIRE_ORANGE = float3(1.45, 0.42, 0.05);
static const float3 FIRE_CORE = float3(1.9, 1.35, 0.45);

//The sparks: born yellow-white in the base, cooling to red as they climb and die.
static const float3 SPARK_HOT = float3(3.0, 2.0, 0.8);
static const float3 SPARK_COOL = float3(1.4, 0.2, 0.03);

struct FlameVertexInput
{
    float4 Position : POSITION0; //the fire quad: ignored (one billboard at FlamePosition); a spark: its three randoms
    float3 Data : TEXCOORD0;     //the fire quad: (corner u in {-1,1}, corner v in {0,1}, unused); a spark: (corner x, corner y in {-1,1}, a fourth random)
};

struct FlameVertexOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
    float Fade : TEXCOORD1;
};

//A camera-facing quad's right vector at a point: horizontal, so the billboard stands upright and turns to
//face the lens about the vertical only - a fire does not tilt over when the camera looks down at it.
float3 FacingRight(float3 at)
{
    float3 toCam = CameraPosition - at;
    toCam.y = 0.0;
    return normalize(cross(float3(0.0, 1.0, 0.0), normalize(toCam)));
}

FlameVertexOutput FlameVS(FlameVertexInput input)
{
    FlameVertexOutput output;

    float w = FlameSize;
    float h = FlameSize * FlameHeightScale;
    float3 world = FlamePosition + FacingRight(FlamePosition) * (input.Data.x * w) + float3(0.0, 1.0, 0.0) * (input.Data.y * h);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.UV = input.Data.xy;
    output.Fade = 1.0;

    return output;
}

float4 FlamePS(FlameVertexOutput input) : COLOR
{
    float u = input.UV.x; //[-1,1]
    float v = input.UV.y; //[0,1], 0 at the base
    float r = FlameSeed;
    float t = FlameTime;

    //The body's turbulence: two fBm fields scrolling UP at different rates, offset per fire so no two fires
    //share a pattern. This is the "plasma" - the structure inside the fire, the ragged edges, and the cut
    //that breaks the tongues apart towards their tips.
    float2 p = float2(u * 1.8, v * 3.2 - t * 1.2 * r) + float2(r * 11.0, 0.0);
    float n1 = Fbm2(p, 3);
    float n2 = Fbm2(p * 2.1 + float2(3.7, -t * 0.6 * r), 2);
    float turb = n1 + 0.5 * n2;

    //Three tongues, each its own centre line and width: spread apart at the base and leaning in higher up,
    //each wobbling on its own rates (stretched by FlameSeed so no two fires beat together), all of them
    //bent by the turbulence more the higher they reach. The fire is the union of them; where two overlap
    //they read as one tongue splitting or two rejoining, which is what a fire does.
    float body = 0.0;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float fi = (float)i;
        float c = (fi - 1.0) * 0.5 * (1.0 - 0.6 * v)
            + 0.22 * sin(v * (5.0 + fi * 1.7) * r - t * (7.0 + 2.0 * fi) * r + fi * 2.1)
            + turb * 0.7 * v;
        float w = lerp(0.8, 0.1, pow(v, 0.6)) * (0.85 + 0.15 * sin(t * (6.0 + fi) * r + fi * 1.3));
        body = max(body, 1.0 - abs(u - c) / w);
    }

    //The turbulence CUTS the body, and cuts it more with height: solid and broad at the base, breaking into
    //separate licking tips and detached shreds above. Then the base rises out of the hearth, and the mass
    //of the fire sits in the lower two thirds of the billboard - the reference fire is a pyramid about twice
    //as tall as its base is wide, and the billboard is six times as tall for the play camera's sake (see
    //CampfireConfig.FlameHeightScale) - with only the tips reaching on up.
    float heat = saturate(body * 1.3 - (turb * 0.5 + 0.5) * (0.3 + 1.5 * v));
    heat *= smoothstep(-0.02, 0.10, v) * smoothstep(1.0, 0.55, v);

    clip(heat - 0.02);

    //The colour ramp, hotter low down: red where the fire is thinnest (the tips and the shreds), orange
    //through the body, yellow-white only in the core and across the base over the embers.
    //
    //⚠ THE CORE IS GATED ON HEIGHT AS WELL AS ON HEAT, and that is the whole of why the red shows at all.
    //The first cut said in its own comment that the core was "confined to the base" and it was not: h alone
    //decided the colour, and h carries a (1 - v) boost, so any heat over about 0.6 reached full core - which
    //is most of the body, all the way up the tongues. Photographed under the savanna's own dome, eight fires
    //read as eight white-yellow candles with a red fringe too thin and too faint to see, which is the report
    //this issue was opened on, one rewrite later. The gate below cuts the core off above the bottom third,
    //so the yellow-white is the fire's SEAT and the tongues leaving it cool through orange into red.
    float h = saturate(heat * (0.55 + 0.75 * (1.0 - v)));

    //Widened with it: the red band used to end at h 0.5 and now runs to 0.65, so the shreds and the thin
    //outer edge of every tongue are red rather than a rim on an orange body.
    float3 color = lerp(FIRE_RED, FIRE_ORANGE, smoothstep(0.10, 0.65, h));
    color = lerp(color, FIRE_CORE, smoothstep(0.85, 1.0, h) * smoothstep(0.34, 0.06, v));

    //Premultiplied by the coverage so additive blending fades it out towards the edges
    float alpha = smoothstep(0.0, 0.35, heat);
    return float4(color * alpha, alpha);
}

//A spark: one small camera-facing quad on a looping life off the wall clock - born in the fire's base,
//climbing on a swirl, drifting, shrinking and cooling, gone by the top of its rise. Everything is a function
//of the clock and the vertex's own randoms, so there is no particle state anywhere (the volcano's rule).
FlameVertexOutput SparkVS(FlameVertexInput input)
{
    FlameVertexOutput output;

    float3 rnd = input.Position.xyz;
    float2 corner = input.Data.xy;
    float rnd2 = input.Data.z;
    float r = FlameSeed;

    float period = 1.1 + 1.6 * rnd.z;
    float phase = frac((FlameTime * r + rnd.x * 37.0) / period);
    float height = FlameSize * FlameHeightScale;
    float rise = phase * height * (0.7 + 0.6 * rnd2);

    //Born inside the base, swirling out as it climbs, with a drift of its own on top.
    float ang = rnd.y * 6.2832 + phase * (2.0 + 3.0 * rnd2);
    float radius = FlameSize * (0.15 + 0.35 * rnd.x) * (0.4 + phase);
    float3 pos = FlamePosition + float3(cos(ang) * radius, rise, sin(ang) * radius)
        + float3(sin(phase * 9.0 + rnd.y * 20.0), 0.0, cos(phase * 7.0 + rnd.x * 15.0)) * (FlameSize * 0.12 * phase);

    float size = FlameSize * (0.06 + 0.05 * rnd2) * (1.0 - 0.4 * phase);
    float3 world = pos + FacingRight(pos) * (corner.x * size) + float3(0.0, 1.0, 0.0) * (corner.y * size);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.UV = corner;
    //Fades slowly at first and fast at the end, so a spark is seen climbing rather than dying at the base.
    output.Fade = 1.0 - phase * phase;

    return output;
}

float4 SparkPS(FlameVertexOutput input) : COLOR
{
    float d = length(input.UV);
    float a = saturate(1.0 - d * d) * input.Fade;
    float3 color = lerp(SPARK_COOL, SPARK_HOT, input.Fade * input.Fade);
    return float4(color * a, a);
}

technique Flame
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FlameVS();
        PixelShader = compile PS_SHADERMODEL FlamePS();
    }
};

technique Sparks
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SparkVS();
        PixelShader = compile PS_SHADERMODEL SparkPS();
    }
};
