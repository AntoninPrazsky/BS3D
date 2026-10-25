//The visible fire of the savanna's campfires (#282, #468, #481): SUBFLAME_COUNT upright billboards per fire
//- not one - the pixel shader a procedural FIRE on each: several tongues that split and rejoin over a
//turbulent body, a broad yellow-white base over the hearth narrowing into orange licks with red tips and
//detached red shreds - and, in a second technique over a small shared buffer of billboards, the sparks
//rising out of it. Both drawn additively and bright (radiance well over 1), so they glow and bloom through
//the glare pass the way a real fire throws light. The illumination a fire casts on the ground, the balls
//and the island is a separate scene point light (see AddSceneLights); this is only the source you see.
//Depth-read (the terrain or platform in front hides it) but writes no depth. Testbed-shared, SM 5.0.
//
//Until #468 this was ONE tongue: a centre line wobbled by two sines, a width of (1 - v), amber core to
//orange edge, no red anywhere and no structure inside - exactly a candle or a lighter, which is what the
//owner saw. The references rendered for it (468-campfire-photo, 468-campfire-concept) are the brief: a wide
//bright base, tongues that break apart higher up, red only at the outer tips and in what detaches, sparks.
//
//#468 fixed the SHAPE and left the SILHOUETTE: every fire was still exactly one camera-facing quad, so
//however good the plasma inside it, the fire could never be seen from the side - a billboard always turns
//to face the lens, so orbiting it (the chapter intro and the front end's menu both orbit round fire 0) never
//shows a different face, which is the one thing a real fire always does. #481 is this: SUBFLAME_COUNT
//separate billboards per fire, each its own small camera-facing quad at its own offset from the hearth
//centre and its own seed (so no two run the same plasma), rather than one quad scaled up. Every one still
//turns to face the camera on its own - the depth comes from the OFFSETS parallaxing against each other and
//against the ground as the view moves, exactly the trick a tree's billboard cross or a grass clump's few
//crossed quads already use, and the pattern LavaFountain.fx already uses for the volcano's jets (one static
//buffer of camera-facing quads, drawn per source). Sub-flame 0 sits exactly where the old single quad did,
//at its old scale and seed, so the view the fire was tuned against (front-on, from the play camera) is
//pixel-for-pixel what it always was; only the two smaller sub-flames beside it are new.
//
//A billboard is a rectangle, and a fire painted on one shows the rectangle the moment any of the fire reaches
//its border: the three tongues sit anywhere in U = -1..1 and wander on their own sines, so in some phases a
//near-opaque tongue ended on the quad's side as a straight vertical cut (#886). The quad is therefore WIDER
//than the fire (FLAME_QUAD_WIDEN) and the heat is faded to nothing before it reaches the new border, so no
//flame pixel can sit on an edge in any phase; the tongues themselves are unchanged in the world, and the
//fire is pixel-for-pixel what it was inside the old rectangle.

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

//The fire's sub-flames (#481): a fixed arrangement, not a per-fire random one - it is the fire's own shape,
//the way three tongues rather than two is. Index 0 is the old single quad exactly (zero offset, full scale,
//no seed shift); 1 and 2 are smaller licks tucked either side of it, close enough that head-on they read as
//part of the same fire rather than three campfires, far enough apart that orbiting the hearth visibly
//parallaxes them against each other and the ground. World-space XZ, a fraction of FlameSize.
#define SUBFLAME_COUNT 3

//How much wider the quad is than the fire's own U range of -1..1 (#886). The fire is drawn in the pixel
//shader's U, which runs -FLAME_QUAD_WIDEN..FLAME_QUAD_WIDEN across the quad, while one U unit stays the same
//distance in the world as it always was: the vertex shader scales the corner's own -1..1 by it in both the
//position and the UV, so the tongues do not grow. The heat is faded from |U| = 1 (the old border, so the
//fire inside it is unchanged) down to 0 at |U| = FLAME_QUAD_WIDEN. The broad base of the outer tongues is
//what reaches furthest: a port of this pixel shader run over 400 phases of all the fires' and sub-flames'
//seeds found no pixel above the clip level beyond |U| = 1.31 and 95 % of the phases under 1.29, so 1.3 is
//where the fire has nothing left to show; a larger value only buys pixel-shader area that draws nothing,
//the fBm running before the clip.
#define FLAME_QUAD_WIDEN 1.3

static const float2 SUBFLAME_OFFSET[SUBFLAME_COUNT] = { float2(0.0, 0.0), float2(0.34, 0.20), float2(-0.28, -0.24) };
static const float SUBFLAME_SCALE[SUBFLAME_COUNT] = { 1.0, 0.62, 0.55 };
//Added straight onto FlameSeed before it drives the plasma (FlamePS's own r), so no two sub-flames of the
//same fire share a turbulence any more than two different fires do.
static const float SUBFLAME_SEED_OFFSET[SUBFLAME_COUNT] = { 0.0, 1.7, 3.1 };

struct FlameVertexInput
{
    float4 Position : POSITION0; //the fire quad: X is which sub-flame (#481), 0..SUBFLAME_COUNT-1; a spark: its three randoms
    float3 Data : TEXCOORD0;     //the fire quad: (corner u in {-1,1}, corner v in {0,1}, unused); a spark: (corner x, corner y in {-1,1}, a fourth random)
};

struct FlameVertexOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
    float Fade : TEXCOORD1;
    float SeedOffset : TEXCOORD2; //which sub-flame this quad is (#481), added onto FlameSeed in the pixel shader
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

    //Sub-flame 0 is FlamePosition itself, at full scale - the old single quad, unmoved (#481). The other
    //two sit a fraction of FlameSize away in world XZ and stand smaller, so head-on the fire still reads as
    //the one shape it was tuned as, with two lesser licks either side of it.
    int sub = (int)input.Position.x;
    float3 subPosition = FlamePosition + float3(SUBFLAME_OFFSET[sub].x, 0.0, SUBFLAME_OFFSET[sub].y) * FlameSize;
    float scale = SUBFLAME_SCALE[sub];

    float w = FlameSize * scale;
    float h = FlameSize * FlameHeightScale * scale;
    //The corner's U (-1 or 1) scaled by FLAME_QUAD_WIDEN in the position AND the UV, so the pixel shader's U
    //keeps its world length and only its range grows (#886)
    float u = input.Data.x * FLAME_QUAD_WIDEN;
    float3 world = subPosition + FacingRight(subPosition) * (u * w) + float3(0.0, 1.0, 0.0) * (input.Data.y * h);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.UV = float2(u, input.Data.y);
    output.Fade = 1.0;
    output.SeedOffset = SUBFLAME_SEED_OFFSET[sub];

    return output;
}

float4 FlamePS(FlameVertexOutput input) : COLOR
{
    float u = input.UV.x; //[-FLAME_QUAD_WIDEN,FLAME_QUAD_WIDEN]; the fire itself lives in [-1,1] and the margin is its taper (#886)
    float v = input.UV.y; //[0,1], 0 at the base
    //The seed offset (#481) is what keeps a fire's own three sub-flames from running the identical plasma a
    //fraction apart - the same idea FlameSeed already is between different fires, one level down.
    float r = FlameSeed + input.SeedOffset;
    float t = FlameTime;

    //The body's turbulence: two fBm fields scrolling UP at different rates, offset per fire (and per
    //sub-flame) so no two share a pattern. This is the "plasma" - the structure inside the fire, the ragged
    //edges, and the cut that breaks the tongues apart towards their tips.
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
        //X3571 left on purpose (#713): v is the quad's own V, 0 at the base to 1, interpolated between values in that range, so it is
        //never negative - a fact about the vertex data, not about this file, so it stays as it was rather than gaining an unshown saturate
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

    //The side taper (#886): full strength out to the old border at |u| = 1, then down to nothing at the
    //quad's own border, so no flame pixel can be drawn on an edge however the tongues have wandered. Exactly
    //1 inside |u| = 1, so the fire there is what it was.
    heat *= smoothstep(FLAME_QUAD_WIDEN, 1.0, abs(u));

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

//--- THE FIRE AS A VOLUME (#886), High and Ultra. The owner: "Fire is plasma in space." However many billboards
//stand in a fire, each is a picture turned to the lens, and a 2D field painted on it has no inside: walking round a
//fire shows the same flat plasma. Here the fire is a field in the WORLD - the same tongues, turbulence and colour
//ramp as FlamePS, but each tongue a column round its own wandering axis and the turbulence a 3D noise rising
//through it - and every pixel of the fire's bounding box marches its view ray through that field. So the fire has
//depth: a tongue in front glows over one behind it, a view from the side sees different tongues, and the plasma moves
//through the fire rather than across a card. Low and Medium keep the billboards (the Flame technique above); a lens
//inside the box draws them too, the box having no face before it.
//
//The box stands FLAME_VOLUME_RADIUS fire sizes round the hearth's axis, the billboard's height tall. The march runs
//only where the ray is inside the cone the tongues can reach - FLAME_REACH_BASE fire sizes round the axis at the
//hearth, FLAME_REACH_FALL less by the top (their spread at the base, 0.45, plus their width, 0.9, and the lean) -
//worked out per pixel, so the box's empty corners cost a quadratic and no sample. ⚠ The first cut marched the whole
//cylinder in twenty steps with a third noise for the sheets: +2.0 ms at the #468 camera and +1.4 at the play camera.
static const float FLAME_VOLUME_RADIUS = 1.45;
static const float FLAME_REACH_BASE = 1.4;
static const float FLAME_REACH_FALL = 0.8;
static const int FLAME_VOLUME_STEPS = 12;
//The fire GLOWS through itself: it emits more than it hides. Per fire size of depth at full heat, how much light it
//gives (as a share of its ramp's colour) and how much of what is behind it it takes away. Composited as a surface
//(each sample covering what is behind it by its own opacity) the cool red skin on the near side hid the hot core, and
//the fire read as a column of orange gel; with the absorption under the emission the core shines through the skin.
//The absorption is what keeps the fire red over the daylit sky (#468's reason for blending it at all) and its body
//solid.
static const float FLAME_VOLUME_EMISSION = 2.4;
static const float FLAME_VOLUME_ABSORPTION = 3.0;
//The plasma's sheets: where the turbulence crosses zero it draws a surface through the fire, and the flame is brighter
//along it - thin bright sheets streaming up with the plasma, for no noise of their own
static const float FLAME_SHEET_SHARPNESS = 5.0;
static const float FLAME_SHEET_GAIN = 1.4;
//The tongues: one up the middle and three round it, spread at the base and leaning in as they rise
static const int FLAME_TONGUES = 4;

//The plasma's noise, baked (ProxyVolumes.GradientNoiseTexels): gradient noise repeating every FLAME_NOISE_PERIOD units,
//four texels a unit, read with trilinear filtering. Two fetches a sample where Fbm3's two octaves were two gradient
//noises of eight hashed corners each: the noise was most of the march's cost.
static const float FLAME_NOISE_PERIOD = 8.0;
Texture3D FlameNoiseVolume;
sampler3D FlameNoiseSampler = sampler_state
{
    Texture = <FlameNoiseVolume>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = NONE;
    AddressU = WRAP;
    AddressV = WRAP;
    AddressW = WRAP;
};

//Fbm3(p, 2) off the baked noise: the same two octaves, the second rotated and at twice the frequency
float FlameFbm(float3 p)
{
    float first = tex3Dlod(FlameNoiseSampler, float4(p / FLAME_NOISE_PERIOD, 0.0)).r;
    float second = tex3Dlod(FlameNoiseSampler, float4(mul(NOISE_ROTATE3, p) * (2.02 / FLAME_NOISE_PERIOD), 0.0)).r;
    return 0.5 * first + 0.25 * second;
}

struct FlameVolumeVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

FlameVolumeVertexOutput FlameVolumeVS(FlameVertexInput input)
{
    FlameVolumeVertexOutput output;
    //A corner of the unit box (X and Z -1 or 1, Y 0 or 1) round the hearth's axis
    float3 extent = float3(FLAME_VOLUME_RADIUS * FlameSize, FlameSize * FlameHeightScale, FLAME_VOLUME_RADIUS * FlameSize);
    float3 world = FlamePosition + input.Position.xyz * extent;
    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.WorldPosition = world;
    return output;
}

//The fire's heat at q (fire sizes from the hearth's centre), v its share of the fire's height: FlamePS's body, in 3D.
//`bearing` is each tongue's direction off the axis this frame, the same for every sample of a pixel. `turb` is handed
//back for the sheets.
float FlameVolumeHeat(float3 q, float v, float r, float t, float2 bearing[FLAME_TONGUES], out float turb)
{
    //The plasma: fBm in the world scrolling up through the fire, offset per fire so no two share it
    //Finer than FlamePS's (1.8 across, 3.2 up): a billboard's plasma is one sheet, and in a volume that coarse it merged
    //the tongues into one smooth plume
    float3 p = float3(q.x * 2.5, v * 4.2 - t * 1.4 * r, q.z * 2.5) + float3(r * 11.0, 0.0, r * 7.0);
    turb = FlameFbm(p);

    //The whole fire leans with height on the turbulence, the lean's bearing turning slowly up it and in time
    float leanAt = v * 2.3 + t * 0.35 * r;
    float2 lean = float2(cos(leanAt), sin(leanAt)) * (turb * 0.7 * v);

    float body = 0.0;
    [unroll]
    for (int i = 0; i < FLAME_TONGUES; i++)
    {
        float fi = (float)i;
        //Tongue 0 up the middle; 1 to 3 at a third of a turn apart, each wobbling square to its own bearing
        float spread = (i == 0 ? 0.0 : 0.45) * (1.0 - 0.6 * v);
        float wobble = 0.22 * sin(v * (5.0 + fi * 1.7) * r - t * (7.0 + 2.0 * fi) * r + fi * 2.1);
        float2 c = bearing[i] * spread + float2(-bearing[i].y, bearing[i].x) * wobble + lean;
        float w = lerp(i == 0 ? 0.9 : 0.75, 0.1, pow(saturate(v), 0.6)) * (0.85 + 0.15 * sin(t * (6.0 + fi) * r + fi * 1.3));
        body = max(body, 1.0 - length(q.xz - c) / w);
    }

    //The turbulence cuts harder with height than FlamePS's (0.3 + 1.5 v), so the tongues part into licks as they climb
    return saturate(body * 1.3 - (turb * 0.5 + 0.5) * (0.3 + 1.9 * v)) * smoothstep(-0.02, 0.10, v) * smoothstep(1.0, 0.55, v);
}

float4 FlameVolumePS(FlameVolumeVertexOutput input) : COLOR
{
    //In fire sizes from the hearth's centre from here on
    float height = FlameHeightScale;
    float3 o = (CameraPosition - FlamePosition) / FlameSize;
    float3 rd = normalize(input.WorldPosition - CameraPosition);

    //Where the ray is inside the cone the tongues reach: radius k + m t along it, squared against the distance off the
    //axis. A ray nearly as steep as the cone's side (a lens straight over the fire) takes the cylinder at the base's reach
    float fall = FLAME_REACH_FALL / height;
    float k = FLAME_REACH_BASE - fall * o.y;
    float m = -fall * rd.y;
    float a = dot(rd.xz, rd.xz) - m * m;
    float b = dot(o.xz, rd.xz) - k * m;
    float c = dot(o.xz, o.xz) - k * k;
    [flatten]
    if (a < 1e-4)
    {
        a = max(dot(rd.xz, rd.xz), 1e-6);
        b = dot(o.xz, rd.xz);
        c = dot(o.xz, o.xz) - FLAME_REACH_BASE * FLAME_REACH_BASE;
    }
    float disc = b * b - a * c;
    clip(disc);
    float root = sqrt(max(disc, 0.0));
    float t0 = (-b - root) / a;
    float t1 = (-b + root) / a;
    //And between the hearth and the top
    float ry = abs(rd.y) > 1e-5 ? rd.y : 1e-5;
    float ya = -o.y / ry, yb = (height - o.y) / ry;
    t0 = max(max(t0, min(ya, yb)), 0.0);
    t1 = min(t1, max(ya, yb));
    clip(t1 - t0);

    float r = FlameSeed;
    float t = FlameTime;
    //Each tongue's bearing this frame: a third of a turn apart, swaying a little
    float2 bearing[FLAME_TONGUES];
    [unroll]
    for (int i = 0; i < FLAME_TONGUES; i++)
    {
        float angle = i * 2.0944 + 0.4 * sin(t * 0.7 * r + i);
        bearing[i] = float2(cos(angle), sin(angle));
    }

    float dt = (t1 - t0) / FLAME_VOLUME_STEPS;
    //Each pixel starts its samples at its own fraction of a step (interleaved gradient noise), so the steps do not
    //draw contour lines across the fire
    float jitter = frac(52.9829189 * frac(dot(input.Position.xy, float2(0.06711056, 0.00583715))));

    float transmit = 1.0;
    float3 color = 0.0;
    [loop]
    for (int j = 0; j < FLAME_VOLUME_STEPS; j++)
    {
        float3 q = o + rd * (t0 + (j + jitter) * dt);
        float v = q.y / height;
        float turb;
        float heat = FlameVolumeHeat(q, v, r, t, bearing, turb);
        [branch]
        if (heat > 0.02)
        {
            //FlamePS's ramp: red where the fire is thin, orange through the body, the yellow-white core only low down
            float h = saturate(heat * (0.55 + 0.75 * (1.0 - v)));
            float3 ramp = lerp(FIRE_RED, FIRE_ORANGE, smoothstep(0.10, 0.65, h));
            ramp = lerp(ramp, FIRE_CORE, smoothstep(0.85, 1.0, h) * smoothstep(0.34, 0.06, v));
            float sheet = saturate(1.0 - abs(turb) * FLAME_SHEET_SHARPNESS);
            sheet *= sheet;
            float body = smoothstep(0.02, 0.10, heat);
            //A tongue thins as it climbs, and the light it gives over a shorter path with it: the tips glow more per depth
            color += transmit * ramp * (body * FLAME_VOLUME_EMISSION * (1.0 + 1.2 * v) * dt * (0.7 + FLAME_SHEET_GAIN * sheet));
            transmit *= exp(-FLAME_VOLUME_ABSORPTION * body * dt);
        }
        if (transmit < 0.02)
            break;
    }

    //Premultiplied, as FlamePS's: the blend is AlphaBlend
    clip(0.995 - transmit);
    return float4(color, 1.0 - transmit);
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
    output.SeedOffset = 0.0; //unread by SparkPS; only here because the struct is shared with FlameVS

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

technique FlameVolume
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FlameVolumeVS();
        PixelShader = compile PS_SHADERMODEL FlameVolumePS();
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
