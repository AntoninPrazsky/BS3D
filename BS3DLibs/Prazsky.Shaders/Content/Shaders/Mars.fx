//Draws Mars (#277): rust-red cratered ground under a dusty, horizon-bright dome - the sixteenth scene, and
//deliberately NOT a red-palette copy of the Moon (#125). The real Moon has no atmosphere at all, which is
//the only reason that scene replaces the sky and closes its horizon with curvature; Mars keeps a (thin)
//atmosphere, so it is an ordinary IsSolidTerrainScene backdrop - real ground under an ordinary dome, one
//more of the nineteen SkyDome palettes rather than a domeless void.
//
//Two techniques over two draws of one frame:
//  - MarsTerrain: the crater field lifted VERBATIM from Moon.fx (CraterLayer/TurnCrater/CraterField/
//    MareBase - the last two shared with it through Craters.fxh since #581 - are generic height-field math with nothing Moon-specific in them - only the constants that
//    tune it and the colour it is coloured by are this scene's own), retextured rust/ochre instead of
//    grey, on the outback's plumbing instead of the Moon's: an ordinary sun-and-dome light rig, the shared
//    cloud shadow (Clouds.fxh), and the outback's two-stage haze fade to the dome's own horizon colour -
//    NOT the Moon's curvature-and-highland-belt, which exists purely because the Moon has no air to fade
//    into. There is no highland belt here; the ordinary haze closes the horizon the way the desert's and
//    the outback's does. Standing on it: two lattices of boulders and pebbles, the outback's RockLayer
//    lifted VERBATIM too and retuned from a skyline of monoliths to the litter of stones a rover
//    photograph shows, dark volcanic basalt rather than more of the ground's own rust.
//  - MarsMoons: Phobos and Deimos, two small analytic discs composited over the dome and the terrain -
//    space's full-screen-quad machinery (the shared corner quad, ViewRayBasis), depth-READ
//    against the depth MarsTerrain just wrote (Moon.fx's own measured reason: depth-read after the ground,
//    never before it) so a moon low enough to sit behind a crater rim is occluded by it, and ALPHA-BLENDED
//    (unlike the Moon's opaque sky pass) so the dome and the terrain show through everywhere neither disc
//    covers. No continents, no clouds, no atmosphere rim - that is the Moon's Earth, and neither of these
//    moons has an atmosphere of its own to put one on.
//
//DELIBERATELY NOT IN THIS PASS (see issue #277's own open questions, and MarsSceneConfig's doc): a
//foreground dust-haze/dust-devil overlay (Spray.fx's or the volcano's ash's machinery). A real gap, left
//for later - it does not block a shippable scene the way a bare, stoneless plain would have.
//
//Everything is written in LINEAR RADIANCE into the HDR target. Built by all three executables out of this
//directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
//The sun's cast shadows (#469, in this scene since #471): SceneRenderer renders the map before the scene
//pass and this reads it. See Shadows.fxh for what casts and what a receiver owes.
#include "Shadows.fxh"
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

#include "FarField.fxh"

//Towards the sun, and the sun's own radiance (the lit-cloud colour the weather uses, tinted by the dome) -
//shared between the MarsTerrain and MarsMoons techniques, so the moons are lit by the same sun the ground is.
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline. Mars's own dome
//(the nineteenth SkyDome palette) gets BRIGHTER near the horizon than at the zenith - the opposite of
//Earth's blue-zenith Rayleigh falloff - but nothing here assumes which end is which; it just reads whatever
//the current dome states.
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), so the terrain sits still in the
//world while the mesh slides under it
float2 OriginXZ;

//Radius of the island's footprint cut out of the terrain around the world origin. The map editor draws no
//island and leaves it 0.
float IslandHoleRadius;

//The plain: its mean level (the island's foot), and the clearing it stands in - gates the crater field only,
//never a plain's-worth of undulation, the same shape every solid-terrain sibling uses around the round island.
float MarsLevelY;
float ClearingRadius;
float ClearingTransition;

//Peak height of the crater field out in the far field (world units over the whole three-octave sum).
float CraterAmplitude;

//The stone field: two lattices of boulders and pebbles standing on the plain (RockLayer below, ported
//verbatim from Outback.fx). Spacing/seed pairs are unrelated so the two grids share no lines.
float RockSpacing;
float RockChance;
float RockHeight;
float PebbleSpacing;
float PebbleChance;
float PebbleHeight;

//Rust reflectance (linear): the deep oxidised iron of the plain and the paler, dustier rust of fresh crater
//rims and settled patches.
float3 RustColor;
float3 RustColorPale;

//How strongly a crater's raised rim brightens towards RustColorPale (freshly excavated material).
float EjectaBrightness;

//The stones' own reflectance (linear): dark volcanic basalt in shadow, its sunlit facets dusted with the
//same rust the ground wears - the one thing on the plain that is not red in the shade.
float3 BoulderColorDeep;
float3 BoulderColorBright;

//How rough a stone's own face is (world units of relief) - the scale below its silhouette.
float RockRelief;

//Fine surface: peak height of the pixel-scale relief (world units) and the near-camera grain strength.
float MicroReliefStrength;
float GrainStrength;

//How much of the sky's hemisphere light fills the plain.
float AmbientStrength;

//THE SKYLINE: a ring of layered mesas and buttes out in the dust (see MesaField). How tall they stand, the radius
//the ring begins at, and how much of the ring is mesa rather than plain (a threshold on the noise that shapes
//them - higher is fewer, smaller mesas).
float MesaHeight;
float MesaInnerRadius;
float MesaThreshold;

//The sediment a mesa's cliffs are cut through: pale and dark layers, and how many a world unit of height holds.
float3 StrataColorPale;
float3 StrataColorDark;
float StrataFrequency;

//Dark basaltic sand lying in drifts on the flats, and how much of the plain it covers.
float3 SandColor;
float SandCoverage;

//Flat pale slabs of bedrock showing through the dust, cracked, and how much of the near plain they cover.
float3 SlabColor;
float SlabCoverage;

//Airborne rust dust: how far the haze is carried from the dome's own horizon colour towards HazeTint - the
//outback's own two-stage fade (see MarsTerrainPS), so Mars's distance stays rust-coloured under any dome
//rather than borrowing whatever hue that dome happens to be.
float3 HazeTint;
float DustStrength;
float HorizonHazeDistance;

//--- The moons -------------------------------------------------------------------------------------------

//Recovers the view ray per pixel for the full-screen MarsMoons pass - space's own trick (the far plane is a
//plane in world space and screen-to-far-plane is affine, so interpolating it across the quad is exact).
float4x4 ViewRayBasis;  //the lens's axes over the projection's slopes - see SkyRay.Basis

float3 PhobosDirection;      //normalized
float PhobosAngularRadius;   //radians
float3 PhobosColor;          //linear

float3 DeimosDirection;      //normalized
float DeimosAngularRadius;   //radians
float3 DeimosColor;          //linear

//=====================================================================================================
//The terrain
//=====================================================================================================

//One octave of craters: at most one per cell of a jittered lattice, and only the pixel's OWN cell is ever
//read - the single-cell trick Moon.fx's craters, the space starfield and the meadow's wildflowers all use.
//Derived from Moon.fx's (#125): this field is generic height-field math with nothing Moon-specific in it,
//so retexturing it rust rather than grey needed no change at the time. No longer identical - the Moon's later
//shadow terms (#508) never came here (#579), so the two have been separate copies since. See Moon.fx for the derivation
//of every constant below - CRATER_MIN_RADIUS/CRATER_MAX_RADIUS, the per-octave amplitudes and periods in
//CraterField, and the margin arithmetic that keeps a crater inside its own cell (#240 there).
static const float CRATER_MIN_RADIUS = 0.12;
static const float CRATER_MAX_RADIUS = 0.21;

float CraterLayer(float2 p, float seedOffset, float chance, out float ejecta)
{
    float2 cellId = floor(p);
    float2 f = p - cellId;

    ejecta = 0.0;

    float2 rollA = NoiseHash22(cellId + seedOffset) * 0.5 + 0.5;

    if (rollA.x > chance) return 0.0;

    float2 rollB = NoiseHash22(cellId + seedOffset + 47.9) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seedOffset + 91.7) * 0.5 + 0.5;

    float radius = lerp(CRATER_MIN_RADIUS, CRATER_MAX_RADIUS, rollB.x * rollB.x);
    float margin = radius * 1.6;
    float2 centre = margin + rollC * (1.0 - 2.0 * margin);

    float d = length(f - centre) / radius;

    if (d >= 1.6) return 0.0;

    float depth = lerp(0.55, 1.0, rollB.y);

    float cup = saturate(1.0 - d * d);
    float bowl = -cup * cup * depth;

    float rimWidth = lerp(0.18, 0.42, rollA.y);
    float rimT = (d - 1.0) / rimWidth;

    float rim = exp(-rimT * rimT) * smoothstep(1.6, 1.1, d);

    ejecta = rim * rollB.y;

    return bowl + rim * depth * 0.62;
}

#include "Craters.fxh"

float CraterField(float2 p, out float ejecta)
{
    float e0, e1, e2;

    float height = CraterLayer(TurnCrater(p, CRATER_TURN_0) * (1.0 / 129.0), 11.3, 0.86, e0) * 0.58
        + CraterLayer(TurnCrater(p, CRATER_TURN_1) * (1.0 / 49.0), 37.7, 0.82, e1) * 0.29
        + CraterLayer(TurnCrater(p, CRATER_TURN_2) * (1.0 / 18.6), 71.1, 0.70, e2) * 0.13;

    ejecta = max(e0 * 0.9, max(e1, e2 * 0.8));

    return height;
}

//The stone field: two lattices of boulders and pebbles, derived from Outback.fx's RockLayer (which has since
//grown its bornhardt shape and is no longer this copy's twin, #579) - a
//single-cell jittered lattice (the craters' own trick, above) shaped into a whaleback rock with a talus
//apron. Generic height-field math with nothing outback-specific in it: no gullies and no elongation to
//speak of here (ribDepth 0, a low maxElongation) because Mars rock is wind-worn, not water-cut, so the
//shape reads as a rounded stone rather than a ribbed bornhardt.
#define TALUS_REACH 1.3

#include "Rocks.fxh"

float RockLayer(float2 p, float cellSize, float seed, float chance, float height,
    float minRadius, float maxRadius, float maxElongation, float ribDepth, out float shape, out float rib)
{
    shape = 0.0;
    rib = 0.0;

    float2 q = p / cellSize;
    float2 cellId = floor(q);
    float2 f = q - cellId;

    float2 rollA = NoiseHash22(cellId + seed) * 0.5 + 0.5;

    if (rollA.x > chance) return 0.0;

    float2 rollB = NoiseHash22(cellId + seed + 23.7) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seed + 57.1) * 0.5 + 0.5;
    float2 rollD = NoiseHash22(cellId + seed + 91.3) * 0.5 + 0.5;

    float radius = lerp(minRadius, maxRadius, rollB.x);
    float elongation = lerp(1.0, maxElongation, rollD.y);

    float margin = min(radius * TALUS_REACH * (1.0 + ribDepth) * elongation, 0.45);
    float2 centre = margin + rollC * (1.0 - 2.0 * margin);

    float2 centreWorld = (cellId + centre) * cellSize;
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, length(centreWorld));

    if (ramp <= 0.0) return 0.0;

    float2 local = RotateInto(f - centre, RollDirection(rollD));
    local.x /= elongation;

    float reach = length(local);
    float d1 = reach / radius;

    float2 radial = local * rsqrt(max(dot(local, local), 1e-6));
    rib = GradientNoise2(radial * 3.2 + cellId * 13.1 + seed);

    float ribbed = d1 * (1.0 + rib * ribDepth * smoothstep(0.20, 0.62, d1));

    float2 rollE = NoiseHash22(cellId + seed + 131.9) * 0.5 + 0.5;
    float lobeScale = lerp(0.40, 0.60, rollE.y);
    float2 lobeCentre = RollDirection(rollE) * radius * 0.5;

    float d2 = length(local - lobeCentre) / (radius * lobeScale);

    float crest = lerp(0.30, 0.55, rollA.y);
    float body = max(smoothstep(1.0, crest, ribbed), smoothstep(1.0, crest, d2) * lerp(0.55, 0.90, rollE.x));

    float apron = smoothstep(TALUS_REACH, 1.0, min(d1, d2));

    shape = saturate(body + apron * 0.45);

    return (body * 0.9 + apron * 0.1) * height * lerp(0.62, 1.0, rollB.y) * ramp;
}

//THE SKYLINE: layered mesas and buttes on a ring out in the dust. The plain used to run flat to a haze line,
//and a horizon with nothing standing on it is most of what read as bland - every rover photograph and every
//reference rendered for this has a crater wall or a range of flat-topped, sediment-banded hills across the
//distance. The shape is a low-frequency noise THRESHOLDED: a steep smoothstep turns its high ground into flat
//tops with cliffs around them, which is what a mesa is, and pulling a third of the profile towards quarter steps
//gives the flanks the ledges of eroding layers. The ring's own ramp feeds the threshold, so the mesas rise out of
//the plain as it recedes instead of starting at a circle. `mesa` comes back 0..1 - how far up the landform a
//point is - for the strata colouring.
float MesaField(float2 p, out float mesa)
{
    mesa = 0.0;

    float ring = smoothstep(MesaInnerRadius, MesaInnerRadius + 140.0, length(p));

    float shapeNoise = GradientNoise2(p * 0.0042) * 0.7 + GradientNoise2(p * 0.0115 + 3.7) * 0.3;
    float rise = shapeNoise + (ring - 1.0) * 0.5;

    float profile = smoothstep(MesaThreshold - 0.10, MesaThreshold + 0.03, rise);
    float ledges = floor(profile * 4.0 + 0.5) / 4.0;
    mesa = lerp(profile, ledges, 0.3);

    return MesaHeight * mesa;
}

//THE BIG CRATERS (#638, after the owner's verdict of 2026-09-29): "larger geometric craters are missing" - and
//his island ruling, that a crater is a HOLE and not a texture. The field above tops out at a bowl about 2.6 units
//deep under a rim some 20 units out, which from the play camera is a dark ellipse painted on the plain. Every
//reference (C:\Users\panrd\AI\sd\out\638-klein and 638-zimage: a rover's plain, from the air, the island among
//them) cuts deep bowls with raised rims into the ground, several near one another. So two sparse lattices of big
//ones: their periods share no small ratio and each lattice is turned its own way, so their cells never line up
//into rows, and one crater a cell at most - the single-cell trick again, a crater held inside its own cell by its
//reach. A crater's depth and its rim are shares of its OWN radius, as a real simple crater's are (a bowl about a
//fifth as deep as it is wide, rim to floor), so a big one is a big hole rather than a wide dimple. None stands
//whose blanket would reach the island's bank (BIG_CRATER_CLEARING): left out whole, never cut.
//OffworldGround.MarsBigCraters is the CPU copy - change the one and the other has to follow.
static const float BIG_CRATER_DEPTH = 0.3;       //the bowl's floor under the plain, as a share of the radius
static const float BIG_CRATER_RIM = 0.12;        //the rim's crest over it
static const float BIG_CRATER_BLANKET = 0.3;     //the ejecta blanket at the rim's foot, as a share of the rim
static const float BIG_CRATER_REACH = 1.9;       //how far out the blanket reaches, in radii
static const float BIG_CRATER_MIN = 0.07;        //the radius range, as a share of the cell
static const float BIG_CRATER_MAX = 0.13;
static const float2 BIG_CRATER_TURN_0 = float2(0.92050, 0.39073);   //23 degrees
static const float2 BIG_CRATER_TURN_1 = float2(0.39073, 0.92050);   //67 degrees
static const float2 BIG_CRATER_TURN_2 = float2(-0.37461, 0.92718);  //112 degrees
//How near the island a big crater's blanket may come, from the arena's centre: past the island's bank (#646), which
//stands on the plain's level, and well inside the crater field's own clearing - the references stand the island
//among craters, and the orbiting menu camera, the only one that shows Mars, sees little past a hundred units
static const float BIG_CRATER_CLEARING = 50.0;

//Everything one big crater does to a point, in ONE evaluation: its height over (or under) the plain, the height's
//gradient in world XZ - in closed form, the profile being radial, so the pixel's normal needs no taps of its own -
//the pale ejecta on its rim and blanket, and the light inside its bowl. THE LIGHT: from the ten units over the plain
//the menu's orbit and the play camera stand at, a bowl lit as brightly as the plain round it is a faint dent; what
//makes the references' craters read as HOLES from the ground is that they are dark inside - their walls hide part
//of the sky, and on the sun's side the rim hides the sun. The terrain casts nothing into the sun's map, so both are
//worked out here in closed form: the sky a point in a bowl still sees falls with how deep in it the point is, and the
//sun is hidden where the ray towards it, climbing at the sun's own slope, meets the rim's crest below the crest's
//height - one quadratic for the distance to the rim along the sun's bearing, the rim taken as a ring at its crest.
//⚠ Evaluated once a pixel, and not inside MarsHeight's three taps: at four evaluations of three lattices a pixel
//(three taps and the light) the craters cost 0.65 ms at High and 0.38 at Low on the desktop's front end.
struct BigCraterSample
{
    float Height;
    float2 Gradient;
    float Ejecta;
    float Occlusion;     //the share of the sky the bowl's walls hide
    float Sun;           //how much of the sun the rim leaves, 1 outside any bowl
};

void BigCraterLayer(float2 p, float period, float2 turn, float seed, float chance, float2 sunFlat, float sunSlope,
    inout BigCraterSample sample)
{
    float2 q = TurnCrater(p, turn) / period;
    float2 cellId = floor(q);
    float2 f = q - cellId;

    float2 rollA = NoiseHash22(cellId + seed) * 0.5 + 0.5;
    if (rollA.x > chance) return;

    float2 rollB = NoiseHash22(cellId + seed + 47.9) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seed + 91.7) * 0.5 + 0.5;

    float radius = lerp(BIG_CRATER_MIN, BIG_CRATER_MAX, rollB.x);
    float margin = radius * BIG_CRATER_REACH;
    float2 centre = margin + rollC * (1.0 - 2.0 * margin);

    //The turn is a rotation about the origin, so the centre's distance from the arena is the same in either frame
    float radiusWorld = radius * period;
    if (length((cellId + centre) * period) - radiusWorld * BIG_CRATER_REACH < BIG_CRATER_CLEARING) return;

    float2 v = f - centre;
    float d = length(v) / radius;
    if (d >= BIG_CRATER_REACH) return;

    //The bowl: round-floored and steep under the rim. Zero at the rim, so the rim and blanket sit on the plain's level
    float cup = saturate(1.0 - d * d);
    float bowl = -cup * (0.6 + 0.4 * cup);
    float bowlSlope = d < 1.0 ? 2.0 * d * (0.6 + 0.8 * cup) : 0.0;           //d(bowl)/dd

    //The rim's crest, a little sharper outside than in, and the blanket of thrown-out rock falling away from it,
    //both gone by BIG_CRATER_REACH (fall: 1 to 1.3 radii, easing to 0 at the reach)
    float width = d > 1.0 ? 0.28 : 0.2;
    float rimT = (d - 1.0) / width;
    float crest = exp(-rimT * rimT);
    float x = saturate((BIG_CRATER_REACH - d) / (BIG_CRATER_REACH - 1.3));
    float fall = x * x * (3.0 - 2.0 * x);
    float fallSlope = -6.0 * x * (1.0 - x) / (BIG_CRATER_REACH - 1.3);
    float spread = exp(-max(d - 1.0, 0.0) / 0.35);
    float spreadSlope = d > 1.0 ? -spread / 0.35 : 0.0;

    float rim = crest * fall;
    float blanket = BIG_CRATER_BLANKET * spread * fall;
    float rimSlope = -2.0 * rimT / width * crest * fall + crest * fallSlope;
    float blanketSlope = BIG_CRATER_BLANKET * (spreadSlope * fall + spread * fallSlope);

    sample.Height += (bowl * BIG_CRATER_DEPTH + (rim + blanket) * BIG_CRATER_RIM) * radiusWorld;

    //dh/dd, and d along the world: the unit vector from the centre, turned back out of the lattice's frame
    float slope = (bowlSlope * BIG_CRATER_DEPTH + (rimSlope + blanketSlope) * BIG_CRATER_RIM) * radiusWorld;
    float2 outward = TurnCrater(v * rsqrt(max(dot(v, v), 1e-10)), float2(turn.x, -turn.y));
    sample.Gradient += outward * (slope / radiusWorld);

    //Fresh excavated rock on the rim and the blanket: the pale ejecta the rust lightens towards
    sample.Ejecta = max(sample.Ejecta, saturate(rim + blanket * 1.5) * lerp(0.6, 1.0, rollA.y));

    [branch]
    if (d < 1.0)
    {
        //The sky: a point at the floor sees about half of it past the walls, one at the rim nearly all
        sample.Occlusion = max(sample.Occlusion, -0.55 * bowl);

        //The sun: the distance to the rim along the sun's bearing (in the lattice's frame), the ray's height there
        float2 sunTurned = TurnCrater(sunFlat, turn);
        float b = dot(v, sunTurned);
        float t = -b + sqrt(max(b * b - (dot(v, v) - radius * radius), 0.0));
        float pointHeight = (bowl * BIG_CRATER_DEPTH + (crest + BIG_CRATER_BLANKET) * BIG_CRATER_RIM) * radiusWorld;
        float crestHeight = (1.0 + BIG_CRATER_BLANKET) * BIG_CRATER_RIM * radiusWorld;
        float rayHeight = pointHeight + t * period * sunSlope;
        sample.Sun *= saturate((rayHeight - crestHeight) / (0.04 * radiusWorld) + 0.5);
    }
}

//Three lattices: incommensurate periods (their radii 14-26, 21-39 and 30-55 units), turned apart. The smallest is
//the one a low lens sees into: the menu's orbit and the play camera stand some ten units over the plain, and from
//there a crater shows its bowl only within about ten times that - so the near plain needs them densest.
BigCraterSample BigCraters(float2 p, float3 sun)
{
    BigCraterSample sample;
    sample.Height = 0.0;
    sample.Gradient = 0.0;
    sample.Ejecta = 0.0;
    sample.Occlusion = 0.0;
    sample.Sun = 1.0;

    float2 sunFlat = sun.xz * rsqrt(max(dot(sun.xz, sun.xz), 1e-6));
    float sunSlope = sun.y * rsqrt(max(dot(sun.xz, sun.xz), 1e-6));

    BigCraterLayer(p, 300.0, BIG_CRATER_TURN_0, 517.3, 0.62, sunFlat, sunSlope, sample);
    BigCraterLayer(p, 423.0, BIG_CRATER_TURN_1, 881.9, 0.55, sunFlat, sunSlope, sample);
    BigCraterLayer(p, 197.0, BIG_CRATER_TURN_2, 263.1, 0.6, sunFlat, sunSlope, sample);
    return sample;
}

//The full displaced height at a world point: flat at MarsLevelY inside the clearing around the island,
//rising into cratered ground with distance, with boulders and pebbles standing on it. UNLIKE THE MOON
//there is no highland belt and no planetary curvature - Mars keeps its air, so MarsTerrainPS's haze fade
//closes the horizon the ordinary way, not geometry. Tapped to displace the vertex (VS) and, thrice, for
//the per-pixel normal (PS). The mesas are NOT in it: the vertex adds them itself, and the pixel adds them only
//past the ring's inner radius (MarsTerrain), which is the whole saving of keeping them apart - measured, a mesa
//field summed in here and early-outing inside its own function cost the near plain as much as the far one. Nor are
//the big craters (#638): the vertex adds them, and the pixel evaluates them once with their gradient in closed form.
float MarsHeight(float2 p, out float ejecta, out float rockShape)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float mare = MareBase(p);
    float field = CraterField(p, ejecta) + mare * 0.18;

    //Two lattices, ROTATED against each other (the outback's own reason) so the boulders and the pebbles
    //between them never come out ranked along one grid's rows. Neither carries a rib (ribDepth 0) - Mars
    //rock is wind-worn, not water-cut.
    float rockShapeA, pebbleShape, ribUnusedA, ribUnusedB;
    float rocks = RockLayer(p, RockSpacing, 311.7, RockChance, RockHeight,
        0.12, 0.22, 1.35, 0.0, rockShapeA, ribUnusedA);
    float pebbles = RockLayer(RotateInto(p, float2(0.8253, 0.5647)), PebbleSpacing, 733.1, PebbleChance, PebbleHeight,
        0.15, 0.28, 1.30, 0.0, pebbleShape, ribUnusedB);

    rockShape = saturate(rockShapeA + pebbleShape);

    return MarsLevelY + CraterAmplitude * ramp * field + rocks + pebbles;
}

struct MarsTerrainVertexInput
{
    float4 Position : POSITION0;
};

struct MarsTerrainVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

MarsTerrainVertexOutput MarsTerrainVS(MarsTerrainVertexInput input)
{
    MarsTerrainVertexOutput output;

    float2 worldXZ = input.Position.xz + OriginXZ;

    float ejectaUnused, rockShapeUnused, mesaUnused;
    float height = MarsHeight(worldXZ, ejectaUnused, rockShapeUnused) + MesaField(worldXZ, mesaUnused)
        + BigCraters(worldXZ, SunDirection).Height;
    float3 worldPosition = float3(worldXZ.x, height, worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

float4 MarsTerrain(MarsTerrainVertexOutput input, bool detail)
{
    float3 worldPosition = input.WorldPosition;

    clip(length(worldPosition.xz) - IslandHoleRadius);
    FarRingClip(worldPosition.xz);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The base normal, taken PER PIXEL from the height field's own gradient (three taps) rather than
    //interpolated from a per-vertex normal - every terrain scene's rule.
    float e = 1.5;
    float ejecta, ejectaX, ejectaZ, rockShape, rockShapeX, rockShapeZ;
    float h = MarsHeight(worldPosition.xz, ejecta, rockShape);
    float hx = MarsHeight(worldPosition.xz + float2(e, 0.0), ejectaX, rockShapeX);
    float hz = MarsHeight(worldPosition.xz + float2(0.0, e), ejectaZ, rockShapeZ);

    //The mesas' share of the three taps, only where the ring can have any (see MarsHeight)
    float mesa = 0.0;
    [branch]
    if (length(worldPosition.xz) > MesaInnerRadius - 2.0 * e)
    {
        float mesaX, mesaZ;
        h += MesaField(worldPosition.xz, mesa);
        hx += MesaField(worldPosition.xz + float2(e, 0.0), mesaX);
        hz += MesaField(worldPosition.xz + float2(0.0, e), mesaZ);
    }

    //The big craters once, their gradient in closed form (BigCraters): the taps above leave them out
    BigCraterSample big = BigCraters(worldPosition.xz, SunDirection);

    float2 slope = float2(hx - h, hz - h) / e + big.Gradient;
    float3 baseNormal = normalize(float3(-slope.x, 1.0, -slope.y));

    //Fine surface: the Moon's fourth, small-crater detail octave (normal-only - at this scale a crater is
    //shading, not silhouette) plus an isotropic fine relief, both band-limited against the footprint.
    float smallEjecta;
    float smallCraters = CraterLayer(TurnCrater(worldPosition.xz, CRATER_TURN_3) * (1.0 / 7.2), 133.7, 0.64, smallEjecta)
        * saturate(1.0 - footprint * (2.0 / 5.0));

    float relief = Fbm2BandLimited(worldPosition.xz * 1.7, 3, footprint * 1.7);

    //The rock's own face, height-folded (Outback's rule: a field of XZ alone is constant down a vertical
    //flank, and a boulder is mostly flank). Only blended in where the stone field actually stands.
    float rockMask = smoothstep(0.03, 0.30, rockShape);
    float rockSurface = Fbm2BandLimited(worldPosition.xz * 0.42 + worldPosition.y * 0.24, 3, footprint * 0.42);

    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition,
        lerp(smallCraters * (MicroReliefStrength * 3.0) + relief * MicroReliefStrength,
            rockSurface * RockRelief, rockMask));

    ejecta = max(max(ejecta, smallEjecta * 0.5), big.Ejecta);

    //--- The rust colour -----------------------------------------------------------------------------
    //Rust on rust, but never one rust: broad albedo patches, pale fresh ejecta on crater rims, and a
    //per-pixel grain close up - the Moon's cascaded three-octave field, carried over unchanged (only the
    //colour it modulates is Mars's own).
    float broad = GradientNoise2(worldPosition.xz * 0.021);

    float3 rust = lerp(RustColor, RustColorPale, saturate(broad * 1.5 + 0.35));

    rust = lerp(rust, RustColorPale * 1.18, saturate(ejecta * EjectaBrightness));

    //The coarse grain is SMOOTH NOISE, not a hash per cell, since the skyline pass: it was two hashes held
    //constant over a 1.3- and a 5.5-unit square, stepping at their edges, and they drew a visible checkerboard
    //of tiles across the near plain - the "blocky ground" the flat Mars read as close up. The 5.5-unit term is
    //gone outright rather than smoothed, the sand drifts and the slabs now carrying variation at that scale.
    //The fine grain stays a hash, being sub-pixel by the time its cells could show.
    float grainFine = saturate(1.0 - footprint * 96.0);
    float grainCoarse = saturate(1.0 - footprint * 1.5);
    float coarse = detail ? GradientNoise2(worldPosition.xz * 0.75 + 17.0) * 1.2 * grainCoarse : 0.0;
    rust *= 1.0 + GrainStrength * (NoiseHash22(floor(worldPosition.xz * 48.0)).x * grainFine + coarse);

    //--- The ground's own materials ------------------------------------------------------------------
    //Three things a rover photograph shows on a plain besides rust and stones, each read off the references
    //rendered for this pass.
    //
    // * Dark basaltic sand lying in DRIFTS on the flats - long streaks along the prevailing wind, not blobs -
    //   and only where the ground is level: sand does not hold on a crater wall or a boulder.
    float level = smoothstep(0.90, 0.985, baseNormal.y);
    [branch]
    if (detail)
    {
    float2 drift = float2(dot(worldPosition.xz, float2(0.81, 0.59)) * 0.35, dot(worldPosition.xz, float2(-0.59, 0.81)));
    float sandField = GradientNoise2(drift * 0.060);
    float sand = smoothstep(0.36 - SandCoverage, 0.56 - SandCoverage, sandField) * level;

    // * Pale bedrock SLABS where the dust is thin: flat plates the colour of dry clay, broken by dark cracks.
    //   The cracks are a noise's zero-crossings - a connected web of thin lines - faded out before they could
    //   shimmer.
    float slabField = GradientNoise2(worldPosition.xz * 0.055 + 23.0);
    float slab = smoothstep(0.52 - SlabCoverage, 0.60 - SlabCoverage, slabField) * level * (1.0 - sand);
    float crack = 0.0;
    [branch]
    if (slab > 0.001)
        crack = (1.0 - smoothstep(0.0, 0.07, abs(GradientNoise2(worldPosition.xz * 0.85 + 5.0))))
            * saturate(1.0 - footprint * 3.0);

    //Wind ripples across the drift - fine crests square to the wind, which is what tells sand from a shadow
    //lying on the plain. Wobbled by a slower sine across them so they are not ruled lines (a noise did it first,
    //for a cost the eye could not tell apart), band-limited so they are gone long before a crest could shrink
    //to a pixel.
    float rippleFade = saturate(1.0 - footprint * 2.2);
    float ripple = sin(dot(worldPosition.xz, float2(0.81, 0.59)) * 3.1
        + sin(dot(worldPosition.xz, float2(-0.59, 0.81)) * 0.45) * 1.8 + broad * 6.0);
    rust = lerp(rust, SandColor * (1.0 + 0.25 * broad) * (1.0 + 0.22 * ripple * rippleFade), sand);
    rust = lerp(rust, SlabColor * (1.0 - 0.55 * crack), slab);
    }

    // * The MESAS' strata: layers of pale and dark sediment banded by height, the band wobbling a little so it
    //   does not read as ruled lines, blended in with how far up the landform a point stands - the plain at
    //   their feet stays rust, their dusty tops lighten.
    //`mesa` came out of the height field's own centre tap, so the landform is not evaluated a fourth time here.
    [branch]
    if (mesa > 0.001)
    {
        //The wobble's noise is the reduced program's third cut: without it the bands are ruled lines, which at
        //the mesas' distance and haze Low's player will not tell apart.
        float wobble = detail ? GradientNoise2(worldPosition.xz * 0.02) * 2.5 : 0.0;
        float band = sin(worldPosition.y * StrataFrequency + wobble) * 0.5 + 0.5;
        float3 strata = lerp(StrataColorDark, StrataColorPale, band * band);
        float cliff = 1.0 - smoothstep(0.55, 0.95, baseNormal.y);
        rust = lerp(rust, lerp(RustColorPale, strata, cliff), saturate(mesa * 1.6));
    }

    //--- The stones ----------------------------------------------------------------------------------
    //Dark volcanic basalt, not more of the ground's own rust - the one thing on the plain that is not
    //red, dusted where the sun catches it. A real rover photograph's whole reason a stone field reads as
    //ROCK rather than as lumps of the dust it stands in.
    float3 boulder = lerp(BoulderColorDeep, BoulderColorBright, saturate(broad * 0.6 + rockSurface * 0.9 + 0.45));

    float3 albedo = lerp(rust, boulder, rockMask);

    //--- Lighting ------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows (#471), into the same sunlight factor the clouds dim, so everything read off it
    //is shadowed at once. What casts here: the island and the gun on the rust.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

    //And inside the big craters, the rim's shadow and the walls' share of the sky (BigCraterLayer)
    sunlight *= big.Sun;

    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light: up-facing ground takes the zenith, faces turned to the skyline take the horizon
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5)) * (1.0 - big.Occlusion);

    float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //--- The air -------------------------------------------------------------------------------------
    //Aerial perspective through rust dust, the outback's own two-stage fade: the mid-distance keeps Mars's
    //own colour (a dome's own horizon colour alone would paint the far plain whatever hue that dome
    //happens to be), and the last stretch arrives at the dome's exact HorizonColor so the mesh's edge
    //never shows as a seam against a sky it does not match. No heat shimmer: Mars' thin, cold CO2
    //atmosphere does not refract light the way the Sahara's hot ground does.
    float3 skyLight = HorizonColor + SunColor * 0.35;
    float skyLuminance = dot(skyLight, float3(0.2126, 0.7152, 0.0722));

    float3 dustLit = HazeTint * lerp(skyLuminance.xxx, skyLight, 0.45);

    float haze = saturate(dist / HorizonHazeDistance);

    color = lerp(color, dustLit, DustStrength * haze * haze);
    color = lerp(color, HorizonColor, haze * haze * haze * haze);

    return float4(FarFadeToSky(color, worldPosition), 1.0);
}

//=====================================================================================================
//The moons: Phobos and Deimos, drawn over the dome (see the header for why this is a separate full-screen
//pass rather than part of the terrain shader above - they stand against the SKY, which MarsTerrainPS never
//touches).
//=====================================================================================================

//One moon: a plain diffuse-lit rock, ray-sphere tested the way Moon.fx's Earth is - a unit sphere at the
//distance that gives the configured angular radius - but with none of the Earth's continents, weather or
//atmosphere rim, because neither Phobos nor Deimos has an atmosphere of its own to put one on. Returns the
//lit colour; `coverage` comes back as the antialiased 0..1 the caller composites by.
//X4000 LEFT ON PURPOSE (#713): "use of potentially uninitialized variable (MoonDisc)" is the [branch] early return below, which Aurora()
//and Glowworms() were split to get rid of (the body in a MoonDiscLit, a wrapper that chooses and returns once, as #587 did for the
//clouds). Here the split compiles to different code and the picture could not be shown to stay the same:
//the moon is not in the Testbed's default view, so no capture reached the function's body, only its early-out. Left as it
//was until one does.
float3 MoonDisc(float3 dir, float3 moonDirection, float angularRadius, float3 albedo, float pixelAngle, out float coverage)
{
    coverage = 0.0;

    float cosine = dot(dir, moonDirection);
    float cosLimb = cos(angularRadius);

    [branch]
    if (cosine <= cosLimb || angularRadius <= 0.0) return 0.0;

    //The limb is where cos(angle) crosses cosLimb, antialiased over one pixel's worth of angle
    float edge = max(pixelAngle * sin(angularRadius) * 0.8, 1e-6);
    coverage = smoothstep(cosLimb - edge, cosLimb + edge, cosine);

    float distance = 1.0 / max(sin(angularRadius), 1e-4);
    float discriminant = max(distance * distance * (cosine * cosine - 1.0) + 1.0, 0.0);
    float t = distance * cosine - sqrt(discriminant);
    float3 normal = normalize(t * dir - distance * moonDirection);

    float ndotl = saturate(dot(normal, SunDirection));

    //A small ambient floor, or the dark limb reads as a hole cut in the dome rather than the shadowed side
    //of a small lit rock (the space scene's lesson about the island, restated at a moon's scale).
    return albedo * (ndotl * 1.35 + 0.05);
}

struct MarsMoonsVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
};

MarsMoonsVertexOutput MarsMoonsVS(float3 position : POSITION0)
{
    MarsMoonsVertexOutput output;

    //The quad arrives already in normalized device coordinates; z = w puts it on the far plane, so
    //DepthStencilState.DepthRead passes it wherever the terrain has not already written something nearer
    //(space's trick, and Moon.fx's own reason for drawing its sky after its terrain, not before).
    output.Position = float4(position.xy, 1.0, 1.0);

    output.Ray = mul(float4(position.xy, 1.0, 0.0), ViewRayBasis).xyz;

    return output;
}

float4 MarsMoonsPS(MarsMoonsVertexOutput input) : COLOR
{
    float3 dir = normalize(input.Ray);

    //This pixel's angular footprint (Space.fx's Frobenius-norm trick, isotropic in the camera's bearing).
    float pixelAngle = max(sqrt(dot(ddx(dir), ddx(dir)) + dot(ddy(dir), ddy(dir))), 1e-6);

    float deimosCoverage;
    float3 deimos = MoonDisc(dir, DeimosDirection, DeimosAngularRadius, DeimosColor, pixelAngle, deimosCoverage);

    float phobosCoverage;
    float3 phobos = MoonDisc(dir, PhobosDirection, PhobosAngularRadius, PhobosColor, pixelAngle, phobosCoverage);

    //Phobos composited over Deimos - the two never actually overlap at the shipped directions, but the
    //nearer moon should win if a level's own config ever moves them together.
    float3 color = lerp(deimos, phobos, phobosCoverage);
    float coverage = max(deimosCoverage, phobosCoverage);

    //Alpha IS the coverage: unlike Moon.fx's opaque sky pass (which repaints every pixel of a domeless
    //void) this composites over an already-drawn dome and terrain, so everywhere neither disc covers must
    //stay fully transparent.
    return float4(color, coverage);
}

//Two programs from one body, the forest's and the meadow's pattern. "MarsTerrain" is the authored ground;
//"MarsTerrainReduced" drops the sand drifts with their ripples, the bedrock slabs with their cracks and the
//strata's wobble - the ground's added noise - and keeps the mesas, which are geometry and must be lit the same
//on every tier. SceneRenderer.SceneDetail picks; the Game's Low tier takes the reduced one.
float4 MarsTerrainPS(MarsTerrainVertexOutput input) : COLOR { return MarsTerrain(input, true); }
float4 MarsTerrainReducedPS(MarsTerrainVertexOutput input) : COLOR { return MarsTerrain(input, false); }

technique MarsTerrain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MarsTerrainVS();
        PixelShader = compile PS_SHADERMODEL MarsTerrainPS();
    }
};

technique MarsTerrainReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MarsTerrainVS();
        PixelShader = compile PS_SHADERMODEL MarsTerrainReducedPS();
    }
};

technique MarsMoons
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MarsMoonsVS();
        PixelShader = compile PS_SHADERMODEL MarsMoonsPS();
    }
};
