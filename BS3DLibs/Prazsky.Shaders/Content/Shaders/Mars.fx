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
//    cloud shadow (Clouds.fxh), and a two-stage haze fade to the dome's own horizon colour (the outback's until #967) -
//    NOT the Moon's curvature-and-highland-belt, which exists purely because the Moon has no air to fade
//    into. There is no highland belt here; the ordinary haze closes the horizon the way the desert's and
//    the outback's does. Standing on it: two lattices of boulders and pebbles, the outback's RockLayer
//    lifted VERBATIM too and retuned from a skyline of monoliths to the litter of stones a rover
//    photograph shows, dark volcanic basalt rather than more of the ground's own rust.
//  - MarsMoons: Phobos and Deimos, two small analytic potatoes (ray-ellipsoid bodies with craters, #744)
//    composited over the dome and the terrain - space's full-screen-quad machinery (the shared corner quad,
//    ViewRayBasis), depth-READ against the depth MarsTerrain just wrote (Moon.fx's own measured reason:
//    depth-read after the ground, never before it) so a moon low enough to sit behind a crater rim is occluded
//    by it, and ALPHA-BLENDED (unlike the Moon's opaque sky pass) so the dome and the terrain show through
//    everywhere neither moon covers, and part of the dome in front of the moons as well: the airlight. No
//    continents, no clouds, no atmosphere rim - that is the Moon's Earth, and neither of these moons has an
//    atmosphere of its own to put one on.
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

float MoonTransmission;      //the share of a moon's own light the dust lets through
float MoonAirlight;          //the share of the dome behind a moon that still shows in front of it
float3 MarsshineColor;       //linear: the planet's own sunlit ground lighting the moons' Mars-facing side

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
#include "BankCover.fxh"

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

//The rust at a point of the ground: the plain's own (MarsTerrain) or the bank against the island's drum (MarsBankPS, #983),
//whose surface is its mesh's. Both literals at each entry point, so each is a program of its own; the bank's keeps the
//plain's gradient, which it blends to at its edge.
float4 MarsGroundAt(float3 worldPosition, float3 meshNormal, bool onBank, bool detail)
{
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

    //On the bank: the plain's normal where it lies thin and the bank's where it is deep, so its edge, where it goes
    //under the plain, draws no line in the light; and the small craters and the fresh rims under it buried with it
    //(the Moon's drift, #983). No paler pigment: on Mars a paler bank read as a tan ring round the island (#646)
    float buried = 0.0;
    if (onBank)
    {
        buried = saturate((worldPosition.y - (h + big.Height)) * 1.6);
        baseNormal = normalize(lerp(baseNormal, meshNormal, buried));
    }

    //Fine surface: the Moon's fourth, small-crater detail octave (normal-only - at this scale a crater is
    //shading, not silhouette) plus an isotropic fine relief, both band-limited against the footprint.
    float smallEjecta;
    float smallCraters = CraterLayer(TurnCrater(worldPosition.xz, CRATER_TURN_3) * (1.0 / 7.2), 133.7, 0.64, smallEjecta)
        * saturate(1.0 - footprint * (2.0 / 5.0)) * (1.0 - buried);

    float relief = Fbm2BandLimited(worldPosition.xz * 1.7, 3, footprint * 1.7);

    //The rock's own face, height-folded (Outback's rule: a field of XZ alone is constant down a vertical
    //flank, and a boulder is mostly flank). Only blended in where the stone field actually stands.
    float rockMask = smoothstep(0.03, 0.30, rockShape);
    float rockSurface = Fbm2BandLimited(worldPosition.xz * 0.42 + worldPosition.y * 0.24, 3, footprint * 0.42);

    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition,
        lerp(smallCraters * (MicroReliefStrength * 3.0) + relief * MicroReliefStrength,
            rockSurface * RockRelief, rockMask));

    ejecta = max(max(ejecta, smallEjecta * 0.5), big.Ejecta) * (1.0 - buried);

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

//The two bodies' shapes (#744): semi-axes across the sky (the configured angular radius, so always 1), up the
//sky and along the line of sight, and the outline's tilt in the sky plane. Both moons are tidally locked with
//their long axis on Mars, so from the ground that axis points at the camera and the outline is the two shorter
//ones: Phobos's 27 x 22 x 18 km shows as a 1 : 0.8 potato end-on, Deimos's 15 x 12 x 10 km as 1 : 0.83. A sphere
//was what made Phobos a disc punched out of the dome.
static const float3 PHOBOS_AXES = float3(1.0, 0.8, 1.2);
static const float PHOBOS_TILT = 0.45;   //radians
static const float3 DEIMOS_AXES = float3(1.0, 0.83, 1.15);
static const float DEIMOS_TILT = -0.3;

//Stickney, Phobos's one great crater, a third of the body across: in body space (x across the sky, y up it, z
//away from the camera), on the Mars-facing hemisphere towards the upper limb, where the high sun rakes it.
static const float3 STICKNEY_CENTRE = float3(0.35, 0.5, -0.79);
static const float STICKNEY_RADIUS = 0.42;
//The grooves' common axis: Phobos's parallel striations, chains of pits running round the body.
static const float3 GROOVE_AXIS = float3(0.8, -0.35, 0.5);

//The gain the old disc lit its sunward side by, kept: MarsMoonsConfig's reflectances are tuned against it.
static const float MOON_SUN_GAIN = 1.35;

//One crater's slope at `s`, a point of the body's unit sphere: a parabolic bowl inside `radius` with a raised rim
//just outside it, as the tangent vector the surface normal tilts away by. Analytic, so the [branch] it runs under
//holds no gradient op. `depth` is the bowl's depth over its radius, so a crater's slopes do not depend on its
//size. `centre` need not lie on the sphere: a ball cut by the sphere still rims a round crater.
float3 CraterTilt(float3 s, float3 centre, float radius, float depth)
{
    float3 away = s - centre;
    float x = length(away) / radius;

    //d/dx of depth * (min(x, 1)^2 - 1) + 0.35 * depth * exp(-((x - 1) / 0.25)^2), the bowl and its rim
    float rimX = (x - 1.0) / 0.25;
    float slope = depth * ((x < 1.0 ? 2.0 * x : 0.0) - 2.8 * rimX * exp(-rimX * rimX));

    float3 tangent = away - s * dot(away, s);
    return tangent * (slope / max(length(tangent), 1e-5));
}

//A field of small craters, one per cell of a 3D lattice laid over the unit sphere: the single-cell trick the
//terrain's craters use, with every crater's rim (it has died away by 1.6 radii) kept inside its own cell so no
//cell boundary ever cuts one.
float3 CraterCells(float3 s, float frequency, float depth, float salt)
{
    float3 cell = floor(s * frequency);
    float3 h = NoiseHash33(cell + salt);    //-1..1

    float3 centre = (cell + 0.5 + h * 0.12) / frequency;
    float radius = (0.17 + 0.05 * h.x) / frequency;     //0.12..0.22 of a cell, 0.12 + 0.22 * 1.6 < 0.5
    float present = step(-0.4, h.z);                     //seven cells in ten carry one

    return CraterTilt(s, centre, radius, depth) * present;
}

//One moon's body: the ray is intersected with the ellipsoid by scaling both into the space where the ellipsoid
//is the unit sphere (Moon.fx's Earth's ray-sphere test, on a scaled ray), and the craters, the grooves and a
//low-frequency lumpiness then tilt the normal on that sphere. Returns the radiance the moon itself sends towards
//the camera, before the air; `coverage` is the antialiased 0..1 the caller composites by. `detail` scales the
//small craters, the lumps and the mottling (Deimos is smoother: its regolith has filled them); `stickney` is 1
//for the body that carries Stickney and its grooves.
float3 MoonBody(float3 dir, float3 moonDirection, float angularRadius, float3 albedo, float3 axes, float tilt,
                float detail, float stickney, float pixelAngle, out float coverage)
{
    //The body's frame: across the sky and up it, tilted in the sky plane, and along the line of sight
    float3 across = normalize(cross(float3(0.0, 1.0, 0.0), moonDirection));
    float3 up = cross(moonDirection, across);
    float sine, cosine;
    sincos(tilt, sine, cosine);
    float3 axisA = across * cosine + up * sine;
    float3 axisB = up * cosine - across * sine;

    //The camera at the origin and the centre `distance` radii out, in the space where the body is the unit sphere
    float distance = 1.0 / max(sin(angularRadius), 1e-4);
    float3 origin = float3(0.0, 0.0, -distance / axes.z);
    float3 ray = float3(dot(dir, axisA), dot(dir, axisB), dot(dir, moonDirection)) / axes;

    //The ray's closest approach to the centre: under 1, it hits. A slow wobble of the outline makes the
    //silhouette a lump rather than an ellipse.
    float rayLength2 = dot(ray, ray);
    float3 closest = origin - ray * (dot(origin, ray) / rayLength2);
    float miss = length(closest);
    float outline = 1.0 + 0.05 * GradientNoise3(closest / max(miss, 1e-4) * 2.3 + 5.1);

    //One pixel's angle, carried into the scaled space (an angle moves the point `distance` times as far, then
    //over the outline's semi-axes)
    float edge = max(pixelAngle * distance * 1.6 / (axes.x + axes.y), 1e-5);
    coverage = 1.0 - smoothstep(outline - edge, outline + edge, miss);

    //The near surface; where the wobble reaches past the ellipsoid, the limb's own closest point
    float3 s = normalize(closest - ray * sqrt(max(1.0 - miss * miss, 0.0) / rayLength2));

    //Craters and lumps, as one tilt of the unit sphere's normal
    float3 tilted = CraterTilt(s, normalize(STICKNEY_CENTRE), STICKNEY_RADIUS, 0.22) * stickney;
    tilted += (CraterCells(s, 2.6, 0.3, 1.7) + CraterCells(s, 5.5, 0.26, 7.3) + CraterCells(s, 11.0, 0.14, 4.9)) * detail;
    float3 lump = float3(GradientNoise3(s * 1.6 + 3.1), GradientNoise3(s * 1.6 + 11.7), GradientNoise3(s * 1.6 + 23.3));
    tilted += (lump - s * dot(lump, s)) * 0.7 * detail;

    //Back to world space through the ellipsoid's scale: the gradient of x^2/a^2 + y^2/b^2 + z^2/c^2
    float3 scaled = (s - tilted) / axes;
    float3 normal = normalize(scaled.x * axisA + scaled.y * axisB + scaled.z * moonDirection);

    //The surface: mottled, and on Phobos crossed by the faint dark lines of its grooves
    float3 surface = albedo * (1.0 + 0.9 * detail * Fbm3(s * 3.5 + 2.0, 3));
    float groove = sin(dot(s, normalize(GROOVE_AXIS)) * 34.0 + 2.5 * GradientNoise3(s * 2.7));
    surface *= 1.0 - 0.12 * stickney * smoothstep(0.75, 1.0, groove);

    //Regolith scatters by Lommel-Seeliger rather than Lambert: the lit side stays bright out to the limb, which
    //is what reads as a moon; mixed with Lambert, the terminator stays soft.
    float sunCos = saturate(dot(normal, SunDirection));
    float viewCos = saturate(-dot(normal, dir));
    float lit = lerp(sunCos, 2.0 * sunCos / max(sunCos + viewCos, 1e-3), 0.6);

    //Marsshine: from either moon Mars fills a great part of the sky, lit by the same sun, and it stands below the
    //moon and behind the camera, about 1.8 planet radii from the moon for every one the camera stands above
    //the planet's centre - so the side the camera sees is never black.
    float3 towardMars = normalize(-moonDirection * 1.8 - float3(0.0, 1.0, 0.0));
    float shine = saturate(dot(normal, towardMars));

    return surface * (SunColor * (lit * MOON_SUN_GAIN) + MarsshineColor * shine);
}

//The cheap test every pixel of the full-screen pass runs: only those inside the body's bounding cone (the outline
//at most 5 % past its largest semi-axis, axes.x = 1, and a pixel's antialiasing) run MoonBody. The body is
//assigned in the branch rather than returned from it, so nothing is left uninitialised on the way out (#713).
float3 MoonDisc(float3 dir, float3 moonDirection, float angularRadius, float3 albedo, float3 axes, float tilt,
                float detail, float stickney, float pixelAngle, out float coverage)
{
    float3 color = 0.0;
    coverage = 0.0;

    [branch]
    if (angularRadius > 0.0 && dot(dir, moonDirection) > cos(angularRadius * 1.08 + pixelAngle))
        color = MoonBody(dir, moonDirection, angularRadius, albedo, axes, tilt, detail, stickney, pixelAngle, coverage);

    return color;
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
    float3 deimos = MoonDisc(dir, DeimosDirection, DeimosAngularRadius, DeimosColor, DEIMOS_AXES, DEIMOS_TILT,
                             0.45, 0.0, pixelAngle, deimosCoverage);

    float phobosCoverage;
    float3 phobos = MoonDisc(dir, PhobosDirection, PhobosAngularRadius, PhobosColor, PHOBOS_AXES, PHOBOS_TILT,
                             1.0, 1.0, pixelAngle, phobosCoverage);

    //Phobos composited over Deimos, premultiplied - the two never actually overlap at the shipped directions,
    //but the nearer moon should win if a level's own config ever moves them together.
    float3 color = phobos * phobosCoverage + deimos * (deimosCoverage * (1.0 - phobosCoverage));
    float coverage = phobosCoverage + deimosCoverage * (1.0 - phobosCoverage);

    //Through the air: the moon's own light arrives dimmed by MoonTransmission, and the daylight the dust
    //scatters between the camera and the moon is still in front of it, MoonAirlight of the dome behind. That is
    //how a daytime moon looks: its shadowed side sinks most of the way into the sky instead of reading as a hole
    //cut in it (#744). BlendState.AlphaBlend is premultiplied, out = rgb + dome * (1 - a), so everywhere neither
    //moon covers stays the dome and terrain already drawn - unlike Moon.fx's opaque sky pass.
    return float4(color * MoonTransmission, coverage * (1.0 - MoonAirlight));
}

//Two programs from one body, the forest's and the meadow's pattern. "MarsTerrain" is the authored ground;
//"MarsTerrainReduced" drops the sand drifts with their ripples, the bedrock slabs with their cracks and the
//strata's wobble - the ground's added noise - and keeps the mesas, which are geometry and must be lit the same
//on every tier. SceneRenderer.SceneDetail picks; the Game's Low tier takes the reduced one.
float4 MarsTerrain(MarsTerrainVertexOutput input, bool detail)
{
    float3 worldPosition = input.WorldPosition;

    clip(length(worldPosition.xz) - IslandHoleRadius);
    FarRingClip(worldPosition.xz);

    //Under the bank, which is the plain too (#983, BankCover.fxh): shaded there, not here
    clip(UnderBank(worldPosition.xz) ? -1.0 : 1.0);

    return MarsGroundAt(worldPosition, float3(0.0, 1.0, 0.0), false, detail);
}

float4 MarsTerrainPS(MarsTerrainVertexOutput input) : COLOR { return MarsTerrain(input, true); }
float4 MarsTerrainReducedPS(MarsTerrainVertexOutput input) : COLOR { return MarsTerrain(input, false); }

//THE BANK AGAINST THE DRUM (#983): IslandBerm's mesh worn in the rust itself, as the forest's floor and the Moon's dust
//are, so the drum rises out of the plain's own ground and not out of a ring of flat colour laid round it
struct MarsBankVertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TextureCoordinate : TEXCOORD0;
};

struct MarsBankVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 Normal : TEXCOORD1;
};

MarsBankVertexOutput MarsBankVS(MarsBankVertexInput input)
{
    MarsBankVertexOutput output;
    output.WorldPosition = input.Position.xyz;
    output.Normal = input.Normal;
    output.Position = mul(mul(float4(input.Position.xyz, 1.0), View), Projection);
    return output;
}

float4 MarsBankPS(MarsBankVertexOutput input) : COLOR { return MarsGroundAt(input.WorldPosition, normalize(input.Normal), true, true); }
float4 MarsBankReducedPS(MarsBankVertexOutput input) : COLOR { return MarsGroundAt(input.WorldPosition, normalize(input.Normal), true, false); }

technique MarsBank
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MarsBankVS();
        PixelShader = compile PS_SHADERMODEL MarsBankPS();
    }
};

technique MarsBankReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MarsBankVS();
        PixelShader = compile PS_SHADERMODEL MarsBankReducedPS();
    }
};

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
