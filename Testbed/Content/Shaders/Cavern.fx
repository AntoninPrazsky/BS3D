//Draws the bioluminescent crystal cavern: deep noise-carved rock walls veined with glowing minerals, an
//underground river under them, god rays falling through unseen gaps in the ceiling, sharp crystal
//clusters pulsing cyan and magenta on the walls and casting their light onto the rock around them, and
//spores drifting up through the dark. The eleventh scene, and the third that replaces the SKY (see Space
//and Dream): one full-screen pass over the shared NDC quad, the view ray recovered per pixel through
//InverseViewProjection, drawn with the depth state off so the island, the cluster and the gun draw over it.
//The caller draws no dome and no cloud deck, suppresses the cloud shadow on the instanced effect, and takes
//the scene's own light rig (CavernLightingConfig).
//
//The scene is ANALYTIC except where marching earns its keep: the cave is a noise-shaded cylinder-and-ceiling
//shell (a quadratic and a plane); the river is a flat plane, hit exactly, carrying a three-component RIPPLE
//in its normal alone; and the crystals are the one marched element left, gated per cluster by analytic
//bounding spheres exactly as the dream's solids are. The god rays and the spores are closest-approach glows
//with no geometry at all.
//
//The river used to be a MARCHED height field of seven components whose REFLECTION was the wall shading
//function evaluated a second time along the reflected ray - real waves with real silhouettes over a real
//mirror of the real cave. #250 traded both away: this scene does not need maximum graphics, it needs to run
//cool on every machine, and those two were the dearest things in the pass. What the water reflects now is a
//vertical ramp between the fog and the rock, plus the crystals' pooled light. See CavernScene.
//
//Levels against the glare (GLARE_THRESHOLD 0.55 on luminance): the rock stays far under it - a cave is
//dark, and the dark is what makes the glow read - the crystals and the water's crest glow are allowed over
//deliberately (smooth areas wide enough to bloom steadily, the planet's lit-limb reasoning), and the spores
//sit at the threshold's edge, small and slow, reading through motion rather than bloom.
//
//One deliberate deviation from the usual full-scene grading brief: BS3D renders linear radiance into one
//HDR target and tonemaps ONCE (docs/rendering.md), so there is no per-scene colour grade or contrast pass
//here - the grade lives in the authored palette and the scene's light rig, where it composes with the ACES
//curve instead of fighting it. The one cinematic touch kept in-pass is a subtle vignette on the BACKDROP
//alone, which darkens the cave's corners without touching the island drawn over it.
//
//Built by all three executables out of this directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
#include "Noise.fxh"

//How many of each element the cavern carries. Fixed at compile time so the loops unroll.
#define CRYSTAL_COUNT 8
#define RAY_COUNT 4
//Eight spores where there were twenty-eight: the count the reduced program used to carry is what every
//machine carries now (#250). Twenty-eight unrolled closest-approach gaussians is a lot of live state for a
//mote at the threshold's edge, and it is half of the one PAIR the attribution ever measured a win from.
#define SPORE_COUNT 8

float4x4 InverseViewProjection;
float3 CameraPosition;
float CavernTime;

//--- The cave shell ---------------------------------------------------------------------------------------
float CaveRadius;          //the rock cylinder's radius around the world origin
float CaveCeilingY;        //the ceiling plane the god rays fall from
float3 RockColor;          //desaturated blue-grey (linear), the walls' albedo-times-ambient in one value
float3 VeinColor;          //the glowing mineral veins threading the rock (linear, gently over the rock)
float3 FogColor;           //the abyssal blue-purple the far cave sinks into (linear)
float FogDensity;          //exponential distance-fog density

//--- The river --------------------------------------------------------------------------------------------
float WaterLevelY;         //the river's surface plane, well under the island
float3 WaterDeepColor;     //what the depths transmit (linear, dark)
float3 WaterGlowColor;     //the bioluminescent glow the crests carry (linear, allowed over the threshold)
float WaveScale;           //global frequency multiplier on the wave spectrum (0.16 = the authored look)
float WaveSpeed;           //global speed multiplier on the spectrum's dispersion (0.8 = the authored look)
float WaveAmplitude;       //the dominant swell's height in world units; the spectrum weights hang off it
float CausticStrength;     //the caustic shimmer added where the eye looks into the water
float3 MistColor;          //the steam standing over the river (linear) - the water's glow diffused
float MistDensity;         //optical density of the steam AT the water surface, per world unit
float MistHeight;          //e-folding height over which the steam thins (world units)

//--- The god rays -----------------------------------------------------------------------------------------
float3 GodRayColor;        //cool shaft light (linear)
float GodRayStrength;

//--- The crystals -----------------------------------------------------------------------------------------
float3 CrystalColorA;      //one end of the cluster palette - cyan (linear)
float3 CrystalColorB;      //the other end - magenta (linear)
float CrystalEmission;     //peak emissive level of a pulsing cluster (over the threshold, deliberately)
float CrystalPulseSpeed;
float CrystalWallLight;    //how much of a cluster's light pools on the rock and the water around it

//--- The spores -------------------------------------------------------------------------------------------
float3 SporeColor;         //the drifting motes (linear, at the threshold's edge)
float SporeBrightness;

static const float PI = 3.14159265;

//--- The rock ---------------------------------------------------------------------------------------------
//Where crystal cluster k grows: ON the wall — at nearly the full cave radius, so the growths visibly stand
//on rock rather than hovering in the air (the first build had them at 0.86 R, a thirty-unit float off the
//wall, and they read as balloons) — spaced round the cylinder by the golden angle so no two share a
//bearing, at heights from just over the river to high on the wall. Shared by the cluster SDF, the light it
//casts on the rock, and the water's reflection of both.
float3 CrystalCenter(float k)
{
    float angle = k * 2.39996 + 0.7;
    float radius = CaveRadius * 0.965;
    float y = WaterLevelY + 6.0 + fmod(k * 37.0, 70.0);

    return float3(cos(angle) * radius, y, sin(angle) * radius);
}

//One cluster's distance field, in the cluster's own frame: three interpenetrating octahedra - the sharpest
//cheap SDF there is, and sharp is what a crystal means - at different elongations, offsets and yaws, so a
//cluster reads as a GROWTH of spars rather than one gem.
float ClusterSdf(float3 p, float k)
{
    float3 q1 = p;
    q1.y *= 0.38;
    float d = (abs(q1.x) + abs(q1.y) + abs(q1.z) - 9.5) * 0.577;

    float ca = cos(k * 1.3), sa = sin(k * 1.3);
    float3 q2 = float3(p.x * ca - p.z * sa, p.y - 4.0, p.x * sa + p.z * ca);
    q2.y *= 0.5;
    q2.x *= 0.8;
    d = min(d, (abs(q2.x) + abs(q2.y) + abs(q2.z) - 6.8) * 0.577);

    float3 q3 = float3(p.x + 4.5, (p.y + 6.0) * 0.45, p.z - 3.5);
    d = min(d, (abs(q3.x) + abs(q3.y) + abs(q3.z) - 5.2) * 0.577);

    return d;
}

//The pulse of cluster k: a slow swell, phase-spread so the cavern never beats in unison, and lifted well
//off zero - these are light sources, and a light source that dies reads as broken. Squaring the wave
//sharpens it into quick bright peaks over long dim valleys, a pulse rather than a sine.
float CrystalPulse(float k)
{
    float beat = 0.5 + 0.5 * sin(CavernTime * CrystalPulseSpeed + k * 2.1);
    return 0.55 + 0.45 * beat * beat;
}

//The colour of cluster k: the cyan-magenta axis walked per cluster, not per pixel - each cluster is ONE
//colour, and the contrast between neighbouring clusters is what reads as "cyan and magenta cavern".
float3 CrystalColor(float k)
{
    return lerp(CrystalColorA, CrystalColorB, frac(k * 0.381));
}

//The coloured light the clusters pool onto a point of rock or water. Inverse-square with a floor, summed
//over all clusters - eight multiply-adds, and the single thing that makes the crystals belong to the cave
//instead of being stickers on it: rock that a magenta cluster stands on IS magenta around it.
float3 CrystalLightAt(float3 position)
{
    float3 light = 0.0;

    [unroll]
    for (int k = 0; k < CRYSTAL_COUNT; k++)
    {
        float fk = (float)k;
        float3 toCrystal = CrystalCenter(fk) - position;
        float d2 = dot(toCrystal, toCrystal);

        light += CrystalColor(fk) * (CrystalPulse(fk) / (1.0 + d2 * 0.0016));
    }

    return light * CrystalWallLight;
}

//Shades a point of the cave shell: fractal rock under a perturbed normal, real directional light from the
//crystals, mineral veins, distance fog. Written as a function of the hit point and distance because the
//RIVER used to call it a second time along the reflected ray, its mirror being the real wall rather than an
//approximation of it. That second call is what #250 cut - the dearest single thing in the pass - so this now
//runs at most ONCE per pixel and the signature is all that is left of the arrangement.
float3 ShadeWall(float3 position, float distanceTravelled)
{
    //The shell's own normal: radial on the wall cylinder, folding smoothly into the down-facing ceiling
    //over the top twenty-odd units. The two used to meet in a hard select, and the crease cut a sharp lit
    //line across the veins where wall shading snapped to ceiling shading - rock coves, it does not mitre.
    float3 wallNormal = -normalize(float3(position.x, 0.0, position.z));
    float cove = smoothstep(CaveCeilingY - 22.0, CaveCeilingY - 0.5, position.y);
    float3 baseNormal = normalize(lerp(wallNormal, float3(0.0, -1.0, 0.0), cove));

    //The rock's relief: a 3D fBm sampled for its GRADIENT (three extra taps), which tilts the shell's
    //smooth normal into pitted, folded stone. The first build had only a flat colour mottle, and a wall
    //whose colour varies while its light does not reads as painted plaster - it is the light picking out
    //the bumps that says rock, and everything below (the key, the crystal lights) works against this
    //perturbed normal, which is what makes them all agree about where the surface leans.
    float3 bumpDomain = position * 0.10;
    float bump = Fbm3(bumpDomain, 3);

    const float e = 0.18;
    float3 slope = float3(
        Fbm3(bumpDomain + float3(e, 0.0, 0.0), 3) - bump,
        Fbm3(bumpDomain + float3(0.0, e, 0.0), 3) - bump,
        Fbm3(bumpDomain + float3(0.0, 0.0, e), 3) - bump) / e;

    slope -= baseNormal * dot(slope, baseNormal);
    float3 normal = normalize(baseNormal - slope * 0.6);

    //The rock's body: broad fractal strata for the albedo, and a ridged crack network pressed DOWN into
    //it - crevices are darker because light cannot reach into them, the poor man's occlusion.
    float body = Fbm3(position * 0.045, 4);
    float3 rock = RockColor * (0.55 + 0.45 * saturate(body * 0.9 + 0.5));

    float crack = RidgedFbm3(position * 0.020, 3);
    rock *= 1.0 - 0.4 * saturate(crack - 0.3);

    //The waterline stain: rock within a few units of the river is WET - darker, as wet stone is - which
    //absorbs whatever mismatch survives the water's shore band and the steam, and reads as a cave with a
    //real water level instead of a geometric intersection.
    rock *= lerp(0.45, 1.0, smoothstep(0.0, 3.0, position.y - WaterLevelY));

    //The mineral veins: ridge lines of a 3D field of WORLD POSITION - thin where |noise| is small, raised
    //to a power so only the ridge lines survive. This was the one field in the scene that was 2D-
    //parameterized instead (atan2 of the bearing), and it paid twice: the branch cut stood as a full-
    //height seam of unrelated pattern down the -X wall (mirrored again by the river), and on the ceiling,
    //where y is constant, the mapping degenerated to radial spokes converging on a singularity over the
    //origin. A 3D field has no seam and no pole by construction, and its ceiling slice continues its wall
    //slice across their junction for free - exactly like the bump, body and crack fields above, which
    //were 3D from the start. The patch mask keeps the veins THREADING - ore follows some fractures and
    //leaves others bare - where the unmasked web wallpapered the whole cave with even neon.
    float veinField = Fbm3(position * 0.022 + 7.0, 3);
    float vein = pow(saturate(1.0 - abs(veinField) * 2.4), 6.0);
    vein *= smoothstep(-0.04, 0.36, Fbm3(position * 0.0065 + 13.0, 2));

    //The light: a cool key falling from the ceiling gaps against the perturbed normal, and each crystal
    //as a REAL point light - N dot L towards it over inverse square - so the rock around a magenta
    //cluster is not merely tinted magenta but LIT from the cluster's side, bumps shadowing away from it.
    float keyDiffuse = saturate(dot(normal, normalize(float3(0.2, 1.0, 0.15))));
    float3 shaded = rock * (0.45 + 1.0 * keyDiffuse) + VeinColor * vein;

    [unroll]
    for (int k = 0; k < CRYSTAL_COUNT; k++)
    {
        float fk = (float)k;
        float3 toCrystal = CrystalCenter(fk) - position;
        float d2 = dot(toCrystal, toCrystal);
        float towards = saturate(dot(normal, toCrystal * rsqrt(max(d2, 1e-4))));

        shaded += rock * CrystalColor(fk)
            * (CrystalPulse(fk) * towards / (1.0 + d2 * 0.0016)) * (CrystalWallLight * 16.0);
    }

    //The abyss: exponential distance fog into the deep blue-purple, so the far side of the cavern sinks
    //away instead of presenting a lit wall at every distance.
    float fog = 1.0 - exp(-distanceTravelled * FogDensity);
    return lerp(shaded, FogColor, fog);
}

//Optical thickness of the river's steam along the first t units of a ray: a fog whose density is `density`
//at the water surface and thins exponentially with height over MistHeight, integrated in closed form (the
//classic exponential height fog). Height fog rather than a slab because a slab has a TOP, and its top is a
//new hard line standing in for the old one. The layer earns its keep at the WATERLINE: the analytic wall
//meets the analytic river in a machined edge no real cave has, and a ray grazing the surface toward that
//edge runs its whole length through the densest steam - the seam fades exactly where it was sharpest.
float MistAmount(float3 origin, float3 direction, float t, float density)
{
    float h0 = max(origin.y - WaterLevelY, 0.0) / MistHeight;
    float dh = direction.y * t / MistHeight;
    //The 1e-4 threshold keeps the near-horizontal select's step at ~5e-5 of the column - far below one
    //display code either side of the tonemap - where 1e-3 was already a real (if invisible) hard select.
    float column = t * exp(-h0) * (abs(dh) > 1e-4 ? (1.0 - exp(-dh)) / dh : 1.0);

    return 1.0 - exp(-column * density);
}

//Where the view ray meets the cave shell (the wall cylinder or the ceiling plane), as a distance along the
//ray. The cave is watertight from inside: one of the two always hits.
float CaveShellDistance(float3 origin, float3 direction)
{
    //Ray vs the infinite vertical cylinder |xz| = CaveRadius, from inside: the positive root.
    float2 oxz = origin.xz;
    float2 dxz = direction.xz;
    float a = max(dot(dxz, dxz), 1e-6);
    float b = dot(oxz, dxz);
    float c = dot(oxz, oxz) - CaveRadius * CaveRadius;
    float tWall = (-b + sqrt(max(b * b - a * c, 0.0))) / a;

    //The ceiling plane, when the ray climbs towards it.
    float tCeiling = direction.y > 1e-4 ? (CaveCeilingY - origin.y) / direction.y : 1e9;

    return min(tWall, tCeiling);
}

//--- The river's ripple -------------------------------------------------------------------------------------
//THREE components where there were seven (#250): the long swell, one mid wave and one chop, directions fanned
//so no two are near-parallel, still riding the deep-water dispersion (w = sqrt(g k)) so the swell visibly
//outruns the chop instead of the whole surface sliding as one sheet. A plain sum-of-sines HEIGHT field,
//summed and never multiplied (the ball relief's crosshatch lesson).
//
//Seven components paid for themselves while this field was MARCHED - sixteen evaluations a pixel to find the
//surface, where the spectrum's detail became real silhouette. It is read three times a pixel now, for a
//gradient and nothing else, and at that job the four smallest components were riding along: their 1.15-to-5.2
//wavelengths sit under a display pixel at any distance the river is actually seen from, so what they added to
//the normal was noise the eye reads as shimmer.
#define RIVER_WAVE_COUNT 3
static const float2 RIVER_DIR[RIVER_WAVE_COUNT] = {
    float2(0.94, 0.33), float2(0.82, -0.58), float2(-0.97, -0.24) };
static const float RIVER_LEN[RIVER_WAVE_COUNT] = { 26.0, 8.8, 3.1 };
static const float RIVER_AMP[RIVER_WAVE_COUNT] = { 1.0, 0.38, 0.13 };
static const float RIVER_PHASE[RIVER_WAVE_COUNT] = { 0.0, 3.9, 5.1 };
static const float RIVER_AMP_SUM = 1.51;   //the weights above summed: the ripple's worst-case reach

//How far the ripple's slope drags the caustic lookup, in world units per unit of gradient. The caustic cell
//is 1/0.30 = 3.33 world units across and this field's gradient runs about 0.25 at rest and 0.54 at its
//steepest (the three components' amp x 2pi/wavelength, summed), so 6.0 displaces the web by half a cell
//typically and a whole one on a flank - enough to break the lattice, not enough to swim. See the caustics
//in CavernScene for why the warp exists at all.
#define CAUSTIC_WARP 6.0

//How high the surface stands over the mean plane at p, in world units - a figure only the NORMAL and the
//crest glow read now, the surface itself being the plane. The amplitude still dies between 70 and 170 units
//of horizontal distance from the lens, so the far river is dead flat: at that distance a ripple is under a
//pixel and would alias, and the steam owns the distance anyway. WaveScale and WaveSpeed act as global
//multipliers around their shipped defaults (0.16 and 0.8 map to 1.0), so a config that meant "calmer,
//slower" still means it.
float RiverHeight(float2 p, float t)
{
    float freqScale = WaveScale * 6.25;
    float timeScale = WaveSpeed * 1.25;
    float fade = 1.0 - smoothstep(70.0, 170.0, length(p - CameraPosition.xz));

    float h = 0.0;

    [unroll]
    for (int w = 0; w < RIVER_WAVE_COUNT; w++)
    {
        float k = 6.2832 / RIVER_LEN[w] * freqScale;
        float omega = sqrt(9.81 * k) * timeScale;
        h += RIVER_AMP[w] * sin(dot(p, normalize(RIVER_DIR[w])) * k + t * omega + RIVER_PHASE[w]);
    }

    return h * (WaveAmplitude * fade);
}

struct CavernVertexInput
{
    float4 Position : POSITION0;
};

struct CavernVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
    float2 Ndc : TEXCOORD1;
};

CavernVertexOutput CavernVS(CavernVertexInput input)
{
    CavernVertexOutput output;
    output.Position = float4(input.Position.xy, 0.0, 1.0);
    output.Ndc = input.Position.xy;

    //The corner unprojected to the far plane; the pixel shader normalizes the interpolated ray.
    float4 far = mul(float4(input.Position.xy, 1.0, 1.0), InverseViewProjection);
    output.Ray = far.xyz / far.w - CameraPosition;

    return output;
}

//ONE program now, and a cheaper one. This pass is OCCUPANCY-bound rather than work-bound, and the #102
//attribution says so plainly - front end, 1600x900, desktop GPU, dome 13, nocap, the full scene at 4.98 ms,
//and every single reduction on its own worth NOTHING:
//
//  reflection's wall shading dropped   5.01 ms
//  spores 28 -> 1                      5.02
//  river search 16 -> 8 steps          4.97
//  crystal march 24 -> 14, step 0.75   5.01
//
//while the PAIR of the reflection and the spores measured 3.33, and adding the river and the crystal cuts on
//top of that gave 3.31 - nothing further. That pair was the reduced technique this file used to compile as a
//second program, engaged below High.
//
//#250 lifted the "deliberately expensive" constraint on this scene outright: the owner traded the reflections
//and the waves away for a cave that runs cool on every machine, at every tier. So the pair is cut from the
//scene ITSELF - the water reflects a ramp instead of a second wall shade, and the spore count is the reduced
//one - and the river's sixteen-step march went with them, the one reduction the measurements ranked as
//worthless on its own but which cannot help once the program around it is short enough to matter. There is
//no `detail` argument any more and no second technique: what is left is what every machine draws.
//
//AND THE FOUR ROWS ABOVE ARE A DESKTOP GPU'S ANSWER, WHICH DOES NOT TRANSFER (#250, measured on the APU).
//That matters because those rows are the reason nobody expected this cut to pay. Same instrument, fixed
//camera over the river, arena off, 1600x900: this pass went 23.9 -> 13.3 ms, and the spore cut ALONE - a
//camera turned away so no water is in frame at all - went 20.7 -> 17.9. On the 6900 XT the same spore cut
//measured 5.02 against 4.98, i.e. nothing. So "every single reduction is worth nothing" is true of a wide
//desktop part with occupancy to spare and false of the integrated Radeon the tiers exist for; do not carry
//an attribution across machine classes here, and take the one for the class you are trying to fix.
float4 CavernScene(CavernVertexOutput input)
{
    float3 direction = normalize(input.Ray);
    float t = CavernTime;

    //--- The solid the ray ends on: the cave shell, unless the river is nearer. -------------------------
    float tShell = CaveShellDistance(CameraPosition, direction);
    float tWater = direction.y < -1e-4 ? (WaterLevelY - CameraPosition.y) / direction.y : 1e9;

    float3 color;
    float tSolid;

    [branch]
    if (tWater < tShell)
    {
        //THE RIVER - a flat plane again, and deliberately. #250 traded the marched waves and the real mirror
        //away for a cave that runs cool: what is left is the plane hit, which is exact and needs no search at
        //all, under a RIPPLE that lives in the normal alone. The surface has no silhouette of its own any
        //more - the river sits forty units under the island and is never seen against a skyline, so what
        //that costs is the flank the glints used to catch, and the glints below ride the ripple instead.
        tSolid = tWater;
        float3 hit = CameraPosition + direction * tWater;

        float2 p = hit.xz;
        float ampMax = WaveAmplitude * RIVER_AMP_SUM + 0.05;

        //The ripple's normal: the height field's gradient at FULL strength - three evaluations of three
        //components, where the marched version paid sixteen of seven before it could even find the surface.
        //The full gradient is not an accident: the flat plane this scene shipped with once damped its fake
        //normal to 0.18 and read as a patterned floor, and it was the damping that did that, not the
        //flatness.
        const float eW = 0.3;
        float h = RiverHeight(p, t);
        float2 grad = float2(RiverHeight(p + float2(eW, 0.0), t) - h, RiverHeight(p + float2(0.0, eW), t) - h) / eW;
        float3 normal = normalize(float3(-grad.x, 1.0, -grad.y));

        float3 bounced = reflect(direction, normal);

        //A ripple can fold the reflected ray downward; the mirror looks up. abs() used to fold the IMAGE
        //back on itself along that locus - a doubled strip of mirrored wall sliding with the water - so the
        //fold is a SOFT max against zero: same guarantee, no reversal, no crease.
        bounced.y = 0.5 * (bounced.y + sqrt(bounced.y * bounced.y + 0.02));
        bounced = normalize(bounced);

        float tReflected = CaveShellDistance(hit, bounced);

        //THE MIRROR, WITHOUT THE CAVE IN IT. The reflected ray used to be handed back to ShadeWall - a
        //second full wall shade, every fBm and every crystal light of it, on every water pixel - and that
        //was the dearest single thing in this pass. What stands in for it is the cave's own vertical ramp:
        //a grazing ray runs into the far wall, which the fog owns at that distance; a climbing one ends on
        //ceiling rock, which nothing lights. Both colours are the scene's own.
        float3 mirrored = lerp(FogColor, RockColor, saturate(bounced.y * 2.0));

        //The one thing the mirror cannot lose is the clusters standing over the water - they are the
        //brightest things in it. Their pooled light is read ONCE here and spent twice, in the mirror and in
        //the depths below. The 12 against the wall's own 16 is that wall's N-dot-L term averaged away: this
        //has no surface to lean towards the cluster, so it takes the pooling flat and a little weaker.
        float3 crystalPool = CrystalLightAt(hit);
        mirrored += RockColor * crystalPool * 12.0;

        //The mirror shows the steam too, and this is also what closes most of the gap the wall shade left:
        //the reflected path starts ON the surface, in the layer's densest air, so at any distance the
        //mirrored wall was already most of the way to this colour.
        mirrored = lerp(mirrored, MistColor, MistAmount(hit, bounced, tReflected, MistDensity));

        //Fresnel: grazing looks mirror, steep looks into the water. The floor sits at 0.25 - HIGH for
        //water, deliberately: a cave river is a black mirror before it is a window, and the more the
        //depths (and their caustic web) show through, the more the surface reads as a patterned floor
        //instead of standing water. The mirror, the crest glow and the glints carry the read.
        float fresnel = pow(1.0 - saturate(dot(normal, -direction)), 5.0);
        fresnel = lerp(0.25, 1.0, fresnel);

        //Caustics as a CELLULAR EDGE field: the bright net where wavelets focus runs along the borders
        //between Voronoi cells (VoronoiEdge2 is zero exactly there), which is what real pool caustics
        //look like - a web around dark cells, not dots and not the sine checkerboard the first two
        //attempts produced. Two scales drifting against each other so the net never sits still; finer
        //than the ripple itself, being the focused image of the surface.
        //
        //WARPED BY THE RIPPLE'S OWN GRADIENT, and that is not decoration: while the surface was marched, the
        //web was sampled at the point the ray met a real CREST or TROUGH, so the waves distorted it for free -
        //squeezed on the flanks, stretched over the tops - and that distortion is most of what said "water"
        //rather than "tiling". Read on the flat plane the same field comes out an undistorted world-space
        //lattice, and at a grazing look it reads as a patterned FLOOR, which is the failure this scene has
        //already had once (see CausticStrength). The gradient is already in registers from the normal above,
        //so displacing the lookup by it costs nothing and is the same optical argument the geometry used to
        //make: the focused image moves with the slope of the surface focusing it.
        float2 cp = (p + grad * CAUSTIC_WARP) * 0.30;
        float web1 = pow(saturate(1.0 - VoronoiEdge2(cp + float2(t * 0.20, t * 0.14)) * 2.4), 6.0);
        float web2 = pow(saturate(1.0 - VoronoiEdge2(cp * 1.9 + float2(-t * 0.15, t * 0.11) + 17.0) * 2.4), 6.0);
        float caustic = web1 + 0.55 * web2;
        float3 depths = WaterDeepColor
            + WaterGlowColor * caustic * CausticStrength
            + WaterDeepColor * crystalPool * 6.0;

        //The SHORE BAND: over the last few units before the wall the water shows pure mirror, so the depths
        //and the crest glow are gone before the contact circle rather than stepping across it in a machined
        //edge. That half still works and is why the band stays.
        //
        //ITS CONVERGENCE ARGUMENT DIED WITH THE MIRROR (#250), and the comment used to state it as fact:
        //while the mirror was ShadeWall along the reflected ray, the reflection of the wall right above the
        //contact converged to that wall BY CONSTRUCTION, so the two shading rules met at a shared value and
        //the seam had nothing to show. The mirror is a ramp now, and a ramp cannot converge to anything: at a
        //steep look on a NEAR shoreline the band comes out RockColor*(1 + 12*pool) against a wall that is
        //ShadeWall's own dimmer, wet-darkened value, so what used to be a raw dark line is now a PALE strip -
        //measured, photographed, and left standing on purpose. It needs a camera out at radius ~228 of a 240
        //cave: every camera the Game has sits within tens of units of the origin, where 230 units of fog
        //(exp(-0.0045*230) = 0.36) and the steam own that contact entirely. It is the free camera in the
        //Testbed and the map editor that can reach it. The caustic warp above cannot touch this either way -
        //the caustics live in `depths`, which is exactly what this band excludes.
        float shore = saturate((CaveRadius - length(p)) * 0.11);

        color = lerp(mirrored, depths, (1.0 - fresnel) * shore);

        //The crests carry the bioluminescence itself, riding the ripple's own height.
        float crest = saturate(0.5 + 0.5 * h / max(ampMax, 0.2));
        color += WaterGlowColor * (pow(crest, 5.0) * 0.5 * shore);

        //And the crystals GLINT on the ripple - the cave's stand-in for the sea's sun sparkle: a Blinn lobe
        //on the water normal towards each cluster, inverse-square like every other crystal term, riding
        //the pulse so the sparkles breathe with their sources.
        [unroll]
        for (int g = 0; g < CRYSTAL_COUNT; g++)
        {
            float fg = (float)g;
            float3 toGlint = CrystalCenter(fg) - hit;
            float dg2 = dot(toGlint, toGlint);
            float3 halfway = normalize(toGlint * rsqrt(max(dg2, 1e-4)) - direction);
            float glint = pow(saturate(dot(normal, halfway)), 90.0);

            color += CrystalColor(fg)
                * (glint * CrystalPulse(fg) * (CrystalWallLight * 8.0) / (1.0 + dg2 * 0.0016));
        }
    }
    else
    {
        //THE ROCK.
        tSolid = tShell;
        color = ShadeWall(CameraPosition + direction * tShell, tShell);
    }

    //--- The crystals, gated per cluster (the dream's pattern). A cluster is three interpenetrating
    //octahedra - the sharpest cheap SDF there is, and sharp is what a crystal means - standing on the
    //wall, marched only when the ray crosses its bounding sphere and only up to the solid already found.
    //BEFORE the god rays, deliberately: the crystal branch replaces the pixel wholesale, and when the
    //shafts were accumulated first, every shaft crossing a cluster was punched out along the crystal's
    //silhouette - the beam switched off across the outline as the camera orbited.
    float bestT = tSolid;
    float bestCluster = -1.0;

    [unroll]
    for (int i = 0; i < CRYSTAL_COUNT; i++)
    {
        float fi = (float)i;
        float3 center = CrystalCenter(fi);
        float bound = 16.0;

        float3 oc = CameraPosition - center;
        float b = dot(oc, direction);
        float c = dot(oc, oc) - bound * bound;
        float disc = b * b - c;

        [branch]
        if (disc > 0.0)
        {
            float t0 = max(-b - sqrt(disc), 0.0);
            float t1 = min(-b + sqrt(disc), bestT);

            float rayT = t0;

            //0.55 on the step keeps the march conservative under the octahedra's elongations (a scaled
            //SDF underestimates distance).
            [loop]
            for (int march = 0; march < 24; march++)
            {
                float d = ClusterSdf(CameraPosition + direction * rayT - center, fi);
                if (d < 0.03 || rayT > t1) break;
                rayT += d * 0.55;
            }

            if (rayT <= t1)
            {
                bestT = rayT;
                bestCluster = fi;
            }
        }
    }

    [branch]
    if (bestCluster >= 0.0)
    {
        //A crystal face, shaded off the SDF's own normal - four tetrahedral taps, the dream's trick. The
        //octahedra have FLAT faces, so the reconstructed normal is constant across each facet and jumps at
        //the arris: the facet structure appears in the shading for free, which is exactly what the first
        //build's smooth radial term threw away (it made every cluster a rounded gem).
        float3 hit = CameraPosition + direction * bestT;
        float3 local = hit - CrystalCenter(bestCluster);

        const float e = 0.2;
        float2 tap = float2(1.0, -1.0);
        float3 normal = normalize(
            tap.xyy * ClusterSdf(local + tap.xyy * e, bestCluster) +
            tap.yyx * ClusterSdf(local + tap.yyx * e, bestCluster) +
            tap.yxy * ClusterSdf(local + tap.yxy * e, bestCluster) +
            tap.xxy * ClusterSdf(local + tap.xxy * e, bestCluster));

        //Ambient occlusion off the cluster's own field - four probes up the normal - so the clefts where
        //the three octahedra interpenetrate sit visibly deeper than the open faces. Emission is allowed
        //to dim in them: a crystal glows from inside, but its junctions still swallow light.
        float occlusion = 0.0;

        [unroll]
        for (int a = 1; a <= 4; a++)
        {
            float reach = 0.9 * (float)a;
            occlusion += (reach - ClusterSdf(local + normal * reach, bestCluster)) / reach * pow(0.55, (float)a);
        }

        float ao = saturate(1.0 - 1.2 * occlusion);

        //Lit by its own emission on the pulse, a hard rim at the silhouette, and a facet term keyed to the
        //god rays' downward light so neighbouring faces take visibly different brightness - the facet
        //CONTRAST is what says "crystal", not the outline.
        float3 own = CrystalColor(bestCluster);
        float pulse = CrystalPulse(bestCluster);
        float rim = pow(1.0 - saturate(dot(normal, -direction)), 2.0);
        float facetLight = saturate(dot(normal, normalize(float3(0.25, 0.85, 0.2))));

        color = own * (CrystalEmission * pulse * (0.30 + 0.35 * rim + 0.45 * facetLight) * (0.45 + 0.55 * ao));
    }

    //--- The god rays: vertical shafts from unseen gaps above, as closest-approach glows to fixed vertical
    //lines - no geometry, no march. Each fades with height below the ceiling (light thins as it falls),
    //breathes on its own slow cycle, and carries a dust shimmer down its length. Clamped to bestT, so a
    //crystal truncates the beam exactly where its face stands, and the segment in FRONT of the crystal
    //still glows.
    [unroll]
    for (int r = 0; r < RAY_COUNT; r++)
    {
        float fr = (float)r;
        float angle = fr * 1.62 + 0.4;
        float2 beamXz = float2(cos(angle), sin(angle)) * CaveRadius * (0.30 + 0.14 * frac(fr * 0.53));

        //Closest approach of the view ray to the beam's vertical line, in the XZ plane.
        float2 oxz = CameraPosition.xz - beamXz;
        float2 dxz = direction.xz;
        float along = -dot(oxz, dxz) / max(dot(dxz, dxz), 1e-5);
        along = clamp(along, 0.0, bestT);
        float3 nearest = CameraPosition + direction * along;
        float2 offset = nearest.xz - beamXz;

        float shaft = exp(-dot(offset, offset) / 90.0);

        //Height envelope: strongest just under the ceiling, gone by the water, and C1 the whole way down
        //(the old clamped ramp put a visible Mach shelf at each end of its ramp - the eye picks derivative
        //breaks out of a smooth additive glow); the beam breathes and its dust drifts downward.
        float fall = saturate((CaveCeilingY - nearest.y) / (CaveCeilingY - WaterLevelY));
        float envelope = 1.0 - smoothstep(0.0, 0.857, fall);

        //The dust in the shaft: fractal, and drifting DOWN the beam - a shaft of light is visible only
        //because of what floats through it, and a single smooth noise read as a slow flicker where an
        //fBm reads as motes and wisps sinking.
        float dust = 0.55 + 0.5 * Fbm2(float2(fr * 9.0 + nearest.x * 0.06, nearest.y * 0.07 + t * 0.30), 3);
        float breathe = 0.7 + 0.3 * sin(t * 0.11 + fr * 2.6);

        color += GodRayColor * (shaft * envelope * dust * breathe * GodRayStrength);
    }

    //--- The river's steam, over whatever the ray has gathered so far - the solid it ended on (rock, water
    //or crystal) and the god rays' lower reaches, which rightly sink into it: the exponential height layer
    //integrated along the view ray (see MistAmount). An fBm wisp factor on the OPTICAL DEPTH, not on the
    //blend - so the far shoreline still saturates to a full veil while the mid-water steam visibly drifts -
    //keeps it steam rather than a uniform grade. The wisp is keyed to the RAY DIRECTION, not the hit point:
    //bestT jumps across every crystal silhouette, and a hit-keyed wisp printed a ghostly crystal-shaped
    //cutout into the fog behind each cluster. The spores are added after: a mote climbs OUT of the layer,
    //and stays visible doing it.
    float wisp = 0.8 + 0.2 * Fbm2(direction.xz * 2.6 + float2(t * 0.05, t * 0.035), 2);
    color = lerp(color, MistColor, MistAmount(CameraPosition, direction, bestT, MistDensity * wisp));

    //--- The spores: slow rising motes, swaying as they climb, reborn at the water for ever.
    //Closest-approach gaussians like the dream's sparks, but SLOW - the cave's stillness is the point.
    [unroll]
    for (int s = 0; s < SPORE_COUNT; s++)
    {
        float fs = (float)s;
        float lane = frac(fs * 0.618);
        float riseSpan = CaveCeilingY - WaterLevelY;

        //The climb wraps on a CONTINUOUS envelope: born dark at the water, bright through the middle,
        //dimmed out just under the ceiling. The raw fmod teleported each mote from the ceiling to the
        //water at full brightness - a hard pop every few seconds somewhere in view, in a scene whose
        //whole point is stillness.
        float phase = frac((fs * 11.0 + t * (1.6 + lane * 1.4)) / riseSpan);
        float sporeFade = smoothstep(0.0, 0.06, phase) * (1.0 - smoothstep(0.90, 1.0, phase));

        float3 center;
        center.y = WaterLevelY + phase * riseSpan;
        float swayAngle = fs * 2.4 + t * (0.10 + lane * 0.06);
        float ringRadius = CaveRadius * (0.25 + 0.55 * frac(fs * 0.373));
        center.x = cos(swayAngle) * ringRadius + sin(t * 0.5 + fs * 3.1) * 3.0;
        center.z = sin(swayAngle) * ringRadius + cos(t * 0.43 + fs * 1.7) * 3.0;

        //Closest approach along the ray, clamped to in-front AND to the solid: a mote drifting behind the
        //lens must not glow through it (the dream's sparks learned the clamp), and a mote drifting behind
        //a CRYSTAL must not shine through the crystal's face - this loop runs after the march, so bestT
        //is final and the gaussian's tail fades continuously as the mote recedes behind the spar.
        float3 toSpore = center - CameraPosition;
        float along = clamp(dot(toSpore, direction), 0.0, bestT);
        float3 offset = toSpore - direction * along;

        color += SporeColor * (exp(-dot(offset, offset) / 3.0) * SporeBrightness * sporeFade);
    }

    //--- The vignette, on the backdrop alone: the cave's corners sink, the glowing centre holds the eye.
    //Subtle, and in the PASS rather than in the resolve - the island drawn over this is untouched.
    float edge = dot(input.Ndc, input.Ndc) * 0.5;
    color *= 1.0 - 0.22 * edge * edge;

    return float4(color, 1.0);
}

//ONE program, where there were two. The reduced technique existed to drop the water's second wall shade and
//most of the spores; #250 cut both out of the authored scene itself, so the pair it used to drop is no longer
//in here to drop and a second program would be a copy of this one. The tier does not reach this scene any
//more - SceneRenderer.SceneDetail still picks the forest's and the dream's programs, not the cavern's.
float4 CavernPS(CavernVertexOutput input) : COLOR { return CavernScene(input); }

technique Cavern
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CavernVS();
        PixelShader = compile PS_SHADERMODEL CavernPS();
    }
};
