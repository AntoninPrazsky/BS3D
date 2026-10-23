//Draws the Australian outback (#112, rebuilt for the horizon in #967): weathered red monoliths standing on a wide,
//near-flat spinifex plain, the round stone island in a clearing among them. It is the arid sibling of the Sahara
//next door in the enum, and the two are deliberately nothing alike to look at - the desert is golden dunes with a
//braided ripple surface and no silhouette but its own crests, this is RED ground under ROCK: a skyline of
//Uluru-style bornhardts buttressed by the gullies water has cut down their flanks, Kata Tjuta's clustered domes,
//and the long shadows both throw across the plain.
//
//THE ONE HEIGHT FIELD, DRAWN BY TWO KINDS OF MESH (#967). OutbackHeight is the land everywhere - the plain's swell,
//the monoliths and the boulder outcrops - and TerrainMirror.Outback copies it term for term. What draws it:
//
//  * THE PLAIN: the camera grid and the far ring (technique Outback), which carry the swell alone and give back
//    every pixel inside a formation's FOOTPRINT (an ellipse round it, InsideFootprint). They never shade rock, so
//    the plain - most of any frame - pays for neither rock lattice.
//  * THE ROCK: one polar mesh per formation (technique OutbackRock), instanced, laid over its footprint and
//    displaced by the whole field. It is fine enough for a near-vertical wall at any distance, where the grid's
//    2.5-unit cells and the far ring's 432 cells round the arena could not hold one: past the grid every formation
//    used to come out as a flat-topped trapezoid with 45-degree sides - the "hats" the owner named. Its rim lies on
//    the plain (a footprint holds the formation, apron included), so where it meets the grid both are the same
//    surface; it is drawn a little past the ellipse and the grid stops on it, so there is no crack to see through.
//
//THE FORMATIONS, in the order the eye takes them: their outline (a loaf's buttressed wall, a cluster's domes), the
//light and shade on them (cast shadows across the plain and into their own clefts - OutbackSunBake), then their
//skin (varnish streaks, flaking, the hollows at the foot). The plain's texture is spinifex. The air is an
//extinction by distance towards the colour the sky is drawn at the skyline, so a far formation keeps its own
//colour and stands darker than the sky behind it (see the air, at the end of ShadeOutback).
//
//Shader Model 5.0, built once in Prazsky.Shaders for all three executables. The sky is the current dome's
//gradient in linear radiance, every procedural feature band-limits against the pixel footprint, and the cloud
//shadow is the one shared field in Clouds.fxh (the map editor never sets the cloud uniforms, so there
//CloudSunlight is a flat 1.0).

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
//The sun's cast shadows (#469, in this scene since #471): SceneRenderer renders the map before the scene
//pass and this reads it. See Shadows.fxh for what casts and what a receiver owes.
#include "Shadows.fxh"

//The shared noise library: gradient noise for the plain's swell and the rock's buttresses, Voronoi for the
//spinifex hummocks. Its gradient noise fades QUINTICALLY, which matters because these fields drive a normal.
#include "Noise.fxh"
#include "BankCover.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

#include "FarField.fxh"

//Towards the sun, and the sun's own radiance (the lit-cloud colour the weather uses, tinted by the dome)
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), so the terrain sits still in the
//world while the mesh slides under it
float2 OriginXZ;

//Radius of the island's footprint cut out of the terrain around the world origin. The map editor leaves it 0.
float IslandHoleRadius;

//The plain: its mean level (the island's foot) and the amplitude of the broad swell. Deliberately small - a
//gibber plain IS flat, and the drama here is what stands on it.
float OutbackLevelY;
float PlainRelief;

//The clearing the island stands in. It gates the FORMATIONS (per formation, from its own centre - see RollRock)
//and never the plain's swell, so there is no ramped mean anywhere to read as a bowl.
float ClearingRadius;
float ClearingTransition;

//The monoliths: how far apart their lattice cells sit, how many cells carry one, and how tall the tallest
//loaf stands over the plain. The small outcrops are the same three dials an octave down.
float RockSpacing;
float RockChance;
float RockHeight;
float OutcropSpacing;
float OutcropChance;
float OutcropHeight;

//What share of the formations are a CLUSTER of domes (Kata Tjuta) rather than one loaf (Uluru), and how tall a
//cluster's tallest dome may stand against RockHeight.
float ClusterChance;
float ClusterHeight;

//The gullies: roughly how many run round a loaf (a radius in the noise's domain), how deeply they cut into its
//radius, and how black the water-stained streaks down the walls run.
float RibCount;
float RibDepth;
float VarnishStrength;

//The flaking: how pale the rock is where its oxidised skin has come away, and how much of the lower wall it
//claims. And how dark the hollows at a wall's foot are - 0 leaves the foot lit like the face above it.
float3 FlakeColor;
float FlakeStrength;
float CaveShade;

//How rough the rock's own face is, in world units of relief - the scale below the gullies.
float RockRelief;

//The spinifex: world units between tussocks, how big they grow where they grow (a radius, not an opacity),
//and how high they stand.
float SpinifexSpacing;
float SpinifexCover;
float SpinifexRelief;

//Reflectances, all LINEAR radiance.
float3 RockColorDeep;
float3 RockColorBright;
float3 VarnishColor;
float3 SoilColor;
float3 SoilColorPale;
float3 SpinifexColor;

//How much of the sky's hemisphere light fills the flats
float AmbientStrength;

//Sunlight thrown back off the lit red sand onto the faces turned towards it (the walls, never the plain)
float SoilBounce;

//How hard the desert varnish glints - the one part of this scene that is not matte
float VarnishGloss;

//The air (#967): an extinction by distance. HazeDistance is the distance over which the light from a surface
//falls to 1/e; what replaces it is the colour the sky is drawn at the skyline, leaned towards the dust's own hue
//(HazeTint) by DustStrength. HorizonHazeDistance is where FarField may begin the last fade into the drawn sky.
float3 HazeTint;
float DustStrength;
float HazeDistance;
float HorizonHazeDistance;

//Heat shimmer over the middle distance, and the wall clock and wind that drive it.
float HeatShimmer;
float OutbackTime;
float2 WindDirection;

//The formations' cast shadows (#967), baked once per sun into a square of the world round the arena by the
//OutbackSunBake technique below (OutbackBackdrop.BakeSunShadows). xy: the square's lowest corner in world XZ,
//z: one over its side, w: how dark a full shadow is (0 = none baked: the map editor's first frame, a sun under
//the horizon). Separate from the sun's shadow MAP, which is fitted round the camera and holds what stands on the
//island; this holds the land itself, out to the far ring.
texture TerrainShadowMap;
sampler TerrainShadowSampler = sampler_state
{
    Texture = <TerrainShadowMap>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = NONE;
    AddressU = Clamp;
    AddressV = Clamp;
};

float4 TerrainShadowArea;

//The bake's own inputs: the square (xy its lowest corner, z its side, w one over the target's side in texels)
//and the direction towards the sun. Read by OutbackSunBake only.
float4 BakeArea;
float3 BakeSun;

//--- The land ------------------------------------------------------------------------------------------

//How far past its base line a formation's talus apron reaches, as a multiple of its own radius. It is also the
//figure a FOOTPRINT is made of (see FootprintRadius): a formation's margin inside its lattice cell is its full
//reach, so nothing it draws crosses into a cell that is not read, and the polar mesh that draws it covers that
//reach and no more.
#define TALUS_REACH 1.3

//A loaf's wall, as a share of its radius: the mesh is fine enough for a steep one now (#967; it was 0.30 when the
//camera grid's 2.5-unit cells had to hold it - narrower drew horizontal ripples, one per grid row).
#define LOAF_WALL 0.20

//A cluster's dome: its wall as a share of the dome's radius, and the range of dome radii and their distances from
//the cluster's centre, as shares of the formation's radius. 0.50 + 0.62 x 1.25 stays inside TALUS_REACH, so a
//cluster fits the footprint a loaf of the same radius has.
#define DOME_WALL 0.42
#define DOME_RADIUS_MIN 0.46
#define DOME_RADIUS_MAX 0.62
#define DOME_REACH 1.25
#define MAX_DOMES 5

//The golden angle (2.39996 rad) a cluster's domes are turned round its centre by, as a rotation
#define GOLDEN_COS -0.7373688
#define GOLDEN_SIN 0.6754903

//How sharp a gully's floor is: the gully is where the buttress field crosses zero, and this is how fast it climbs
//out of it onto the buttress's face (see RockLayer).
#define RIB_SHARPNESS 2.4

//The talus apron's share of a formation's height: a skirt, not a brim. At 0.10 a far loaf read as a hat.
#define APRON_SHARE 0.05

//Where the rock's own last fade into the sky begins and ends, from the lens (see the end of ShadeOutback)
#define ROCK_FADE_START 1000.0
#define ROCK_FADE_END 1450.0

//How many streak features run round a formation's unit circle, as a radius in the noise's domain.
#define STREAK_RING 32.0

static const float PI = 3.14159265;

//The outcrops' lattice is turned against the monoliths', so the two grids share no lines.
static const float2 OUTCROP_FRAME = float2(0.8253, 0.5647);

//The profile of a BORNHARDT: a loaf, walls standing near vertical to a rounded crown. The CROWN is a superellipse,
//sqrt(1 - t^p): p = 2 is a round dome, higher p a flatter top breaking more abruptly into the wall. The WALL
//multiplies it down to nothing over the last `wall` of the radius, so the foot meets the plain rather than
//standing on it on a knife edge. The pow is kept off an exact zero, which some compilers turn into a NaN.
float Bornhardt(float t, float squareness, float wall)
{
    float clamped = saturate(t);

    return sqrt(saturate(1.0 - pow(max(clamped, 1e-4), squareness))) * smoothstep(1.0, 1.0 - wall, clamped);
}

#include "Rocks.fxh"

//One lattice cell's formation: whether it has one, and where and how it lies. Its rolls are the ones the
//formation has always been placed by, so #967 moved no formation; RollF is new and decides only its KIND.
struct RockRoll
{
    float Ramp;          //0 = no formation in this cell (an empty cell, or the clearing has it)
    float2 Centre;       //in the cell's own 0..1 frame
    float Radius;        //in cells
    float Elongation;
    float2 Axis;         //the formation's long axis
    float2 RollA;
    float2 RollB;
    float2 RollE;
    float2 RollF;
};

//The rolls of one cell. Five independent hashes rather than one reused - a size correlated with a bearing is a
//texture nobody would name but the eye would still catch. The MARGIN keeps the formation inside its own cell by
//its full reach (radius x apron x gullies x elongation), which is what lets only the pixel's own cell be read.
RockRoll RollRock(float2 cellId, float cellSize, float seed, float chance, float minRadius, float maxRadius,
    float maxElongation, float ribDepth)
{
    RockRoll roll;
    roll.RollA = NoiseHash22(cellId + seed) * 0.5 + 0.5;
    float2 rollB = NoiseHash22(cellId + seed + 23.7) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seed + 57.1) * 0.5 + 0.5;
    float2 rollD = NoiseHash22(cellId + seed + 91.3) * 0.5 + 0.5;

    roll.RollB = rollB;
    roll.Radius = lerp(minRadius, maxRadius, rollB.x);
    roll.Elongation = lerp(1.0, maxElongation, rollD.y);
    roll.Axis = RollDirection(rollD);

    //The clamp only stops a jitter box going NEGATIVE if the ranges are ever widened (the shipped ranges give 0.44)
    float margin = min(roll.Radius * TALUS_REACH * (1.0 + ribDepth) * roll.Elongation, 0.45);
    roll.Centre = margin + rollC * (1.0 - 2.0 * margin);

    //THE CLEARING IS DECIDED PER FORMATION, from its own centre: a formation is whole or not there at all
    float2 centreWorld = (cellId + roll.Centre) * cellSize;
    roll.Ramp = roll.RollA.x > chance ? 0.0
        : smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, length(centreWorld));

    roll.RollE = NoiseHash22(cellId + seed + 131.9) * 0.5 + 0.5;
    roll.RollF = NoiseHash22(cellId + seed + 173.3) * 0.5 + 0.5;

    return roll;
}

//A formation's footprint is an ellipse along its own axis: this is its short semi-axis, in cells. The long one is
//this times the elongation. OutbackBackdrop lays each polar mesh over exactly this ellipse (a hair larger, so the
//grid that stops on it never shows a crack) - one formula, read in both places through TerrainMirror.
float FootprintRadius(RockRoll roll, float ribDepth)
{
    return roll.Radius * TALUS_REACH * (1.0 + ribDepth);
}

//Whether world point p lies inside a formation's footprint on one lattice - the test the plain's meshes stop on.
//Four hashes and no noise: the plain pays this instead of the formations themselves.
bool InsideFootprint(float2 p, float cellSize, float seed, float chance, float minRadius, float maxRadius,
    float maxElongation, float ribDepth)
{
    float2 q = p / cellSize;
    float2 cellId = floor(q);
    RockRoll roll = RollRock(cellId, cellSize, seed, chance, minRadius, maxRadius, maxElongation, ribDepth);

    float2 local = RotateInto(q - cellId - roll.Centre, roll.Axis);
    local.x /= roll.Elongation;

    float reach = FootprintRadius(roll, ribDepth);

    return roll.Ramp > 0.0 && dot(local, local) < reach * reach;
}

//The two lattices' fixed figures, as OutbackHeight passes them (TerrainMirror carries the same).
#define MONOLITH_ARGS RockSpacing, 13.7, RockChance, 0.10, 0.16, 1.8, RibDepth
#define OUTCROP_ARGS OutcropSpacing, 71.3, OutcropChance, 0.045, 0.085, 1.35, 0.0

bool InsideMonolith(float2 p)
{
    return InsideFootprint(p, MONOLITH_ARGS);
}

bool InsideOutcrop(float2 p)
{
    return InsideFootprint(RotateInto(p, OUTCROP_FRAME), OUTCROP_ARGS);
}

//One lattice of rock formations. Returns the rise in world units above the plain; `shape` comes back as the 0..1
//coverage the pixel shader blends rock material in by, `body` as the profile itself (0 at the foot, 1 on the
//crown), `rib` as the gully field (high in a gully, low on a buttress) and `bearing` as the domain the wall's
//streaks run in.
//
//A formation is one of two kinds, rolled per cell:
//  * A LOAF (Uluru): a long rounded block whose wall is a row of BUTTRESSES split by sharp V-gullies from the
//    crown's edge down (the references, #967: every one drew rounded columns of rock in light and shadow between
//    dark clefts), with a second lobe for a shoulder.
//  * A CLUSTER (Kata Tjuta): three to five tall rounded domes leaning on each other, deep clefts between them.
//The outcrops are loaves with no gullies, which is what a boulder is (`clusters` false, ribDepth 0).
float RockLayer(float2 p, float cellSize, float seed, float chance, float minRadius, float maxRadius,
    float maxElongation, float ribDepth, float height, float wall, float minSquareness, float maxSquareness,
    bool clusters, out float shape, out float rib, out float body, out float2 bearing)
{
    shape = 0.0;
    rib = 0.0;
    body = 0.0;
    bearing = 0.0;

    float2 q = p / cellSize;
    float2 cellId = floor(q);
    RockRoll roll = RollRock(cellId, cellSize, seed, chance, minRadius, maxRadius, maxElongation, ribDepth);

    if (roll.Ramp <= 0.0) return 0.0;

    //The formation's own frame: turned to its own bearing and stretched along it
    float2 local = RotateInto(q - cellId - roll.Centre, roll.Axis);
    local.x /= roll.Elongation;

    float radius = roll.Radius;
    float d1 = length(local) / radius;
    float2 radial = local * rsqrt(max(dot(local, local), 1e-6));

    float rise;
    float apron;

    [branch]
    if (clusters && roll.RollF.x < ClusterChance)
    {
        //A CLUSTER. The domes are placed round the centre by the golden angle from a rolled start, the first
        //nearest the middle and the tallest; each is a round Bornhardt with a steep wall. Their union is a max,
        //so where two meet their walls cross part way up and the cleft between them is a V of their own walls.
        //They carry no gullies: a dome's flank is smooth in every reference, and its clefts are the dark lines.
        int count = 3 + (int)(roll.RollF.y * 2.999);
        float best = 0.0;
        float nearest = 1e4;
        float2 bestRadial = radial;
        float bestBody = 0.0;

        //Each dome's two rolls come out of the cell's own by a little arithmetic rather than by a hash of their own:
        //a hash takes the fraction of a product near 20 000, where one rounding of the compiler's folded constants
        //would move a dome and break TerrainMirror's copy. The golden angle turns the first dome's direction round
        //for the next one, as a rotation by literals (no sin or cos approximation on either side).
        float2 direction = RollDirection(roll.RollE);

        [unroll]
        for (int i = 0; i < MAX_DOMES; i++)
        {
            float2 dh = frac(roll.RollE * (3.17 + 2.31 * i) + roll.RollF.yx * (1.93 + 0.77 * i) + float2(0.43, 0.71) * i);
            float spread = i == 0 ? 0.12 * dh.x : lerp(0.32, 0.50, dh.x);
            float2 domeCentre = direction * radius * spread;
            float domeRadius = radius * lerp(DOME_RADIUS_MIN, DOME_RADIUS_MAX, i == 0 ? 0.6 + 0.4 * dh.y : dh.y);

            float2 offset = local - domeCentre;
            float d = length(offset) / domeRadius;
            float present = i < count ? 1.0 : 0.0;
            float tall = (i == 0 ? 1.0 : lerp(0.55, 0.85, dh.y)) * present;
            float profile = Bornhardt(d, lerp(2.0, 2.5, dh.x), DOME_WALL);
            float h = profile * tall;

            if (h > best)
            {
                best = h;
                bestBody = profile;
                bestRadial = offset * rsqrt(max(dot(offset, offset), 1e-6));
            }

            nearest = min(nearest, lerp(1e4, d, present));
            direction = float2(direction.x * GOLDEN_COS - direction.y * GOLDEN_SIN, direction.x * GOLDEN_SIN + direction.y * GOLDEN_COS);
        }

        body = bestBody;
        rib = -0.3;
        bearing = bestRadial * STREAK_RING + cellId * 17.3 + seed;
        apron = smoothstep(DOME_REACH, 1.0, nearest);
        rise = best * ClusterHeight;
    }
    else
    {
        //A LOAF. THE GULLIES are sampled on the UNIT CIRCLE of the formation's own frame, so the noise domain is a
        //closed curve with no seam and no pole (the reason this is a direction and not an atan2 bearing). A gully
        //is where the field crosses zero: 1 - |n| has a cusp there, squared it rounds off onto the buttress face,
        //so each buttress is a rounded column and each gully a sharp dark V. They cut the RADIUS, so they reach the
        //silhouette and notch the crown's edge, and they fade out towards the crest, where the ribs converge.
        float n = GradientNoise2(radial * RibCount + cellId * 13.1 + seed);
        float gully = 1.0 - saturate(abs(n) * RIB_SHARPNESS);
        gully *= gully;
        rib = gully * 2.0 - 1.0;

        float ribbed = d1 * (1.0 + gully * ribDepth * smoothstep(0.20, 0.62, d1));

        //A SECOND LOBE, because a bornhardt comes in shoulders: even Uluru is two humps with a saddle. Its wall is
        //widened by its own scale so it falls over the same world width the main wall does.
        float lobeScale = lerp(0.40, 0.60, roll.RollE.y);
        float2 lobeCentre = RollDirection(roll.RollE) * radius * 0.5;
        float d2 = length(local - lobeCentre) / (radius * lobeScale);

        float squareness = lerp(minSquareness, maxSquareness, roll.RollA.y);
        float mainBody = Bornhardt(ribbed, squareness, wall);
        float lobeBody = Bornhardt(d2, squareness, min(wall / lobeScale, 0.9)) * lerp(0.55, 0.90, roll.RollE.x);

        body = max(mainBody, lobeBody);

        //THE STREAKS RUN ON THE BEARING: water runs straight down a wall, so a streak must hold still as a point
        //moves down it, and the formation's radial direction does not change down a wall at all.
        bearing = radial * STREAK_RING + cellId * 17.3 + seed;

        //The talus apron, taken off the UNRIBBED radius: cut with the gullies it drew a starfish on the sand
        apron = smoothstep(TALUS_REACH, 1.0, min(d1, d2));
        rise = body;
    }

    //The material mask reaches further than the height does, so the scree at the foot is still rock-coloured
    shape = saturate(body + apron * 0.45);

    return (rise * (1.0 - APRON_SHARE) + apron * APRON_SHARE) * height * lerp(0.62, 1.0, roll.RollB.y) * roll.Ramp;
}

//The land at a world point: its height, and what the pixel shader shades the rock by.
struct OutbackLand
{
    //World Y of the ground
    float Height;

    //0..1 rock coverage, apron included
    float Rock;

    //The gully field of the formation the point is on: high in a gully, low on a buttress
    float Rib;

    //How far up its own formation the point stands: 0 at the foot of the wall, 1 on the crown
    float Body;

    //Where on the formation's circuit the point is, in the streaks' noise domain
    float2 Bearing;
};

//The plain's own swell: two octaves of gradient noise, genuinely two and not a sine pair. Not gated by the
//clearing, so its mean is honest (no bowl round the island).
float PlainHeight(float2 p)
{
    float swell = GradientNoise2(p * 0.0062) * 0.70 + GradientNoise2(p * 0.0185 + 5.1) * 0.30;

    return OutbackLevelY + PlainRelief * swell;
}

//The whole land: the plain, the monoliths and the outcrops. TerrainMirror.Outback is this, term for term.
OutbackLand OutbackHeight(float2 p)
{
    float rockShape, outcropShape, rockRib, outcropRib, rockBody, outcropBody;
    float2 rockBearing, outcropBearing;

    //The monoliths: loaves (a wall of LOAF_WALL, crowns from a round dome to a gently flattened block - at
    //squareness 5 a far one was a table) and clusters.
    float rock = RockLayer(p, MONOLITH_ARGS, RockHeight, LOAF_WALL, 2.0, 3.4, true,
        rockShape, rockRib, rockBody, rockBearing);

    //The boulders: radii held near their own HEIGHT (a wide gullied disc drew a starfish), no gullies, rounded
    //(their wall and squareness are the least-squares fit to the whaleback they shipped with).
    float outcrop = RockLayer(RotateInto(p, OUTCROP_FRAME), OUTCROP_ARGS, OutcropHeight, 0.475, 1.5, 5.5, false,
        outcropShape, outcropRib, outcropBody, outcropBearing);

    //A monolith's BODY always wins: a boulder on a monolith's lower wall would otherwise fan its streaks across it
    bool onRock = rockShape >= outcropShape || rockBody > 0.0;

    OutbackLand land;
    land.Height = PlainHeight(p) + rock + outcrop;
    land.Rock = saturate(rockShape + outcropShape);
    land.Rib = onRock ? rockRib : outcropRib;
    land.Body = onRock ? rockBody : outcropBody;
    land.Bearing = onRock ? rockBearing : outcropBearing;

    return land;
}

//The plain alone, in the land's struct: what the grid and the ring shade
OutbackLand PlainLand(float2 p)
{
    OutbackLand land;
    land.Height = PlainHeight(p);
    land.Rock = 0.0;
    land.Rib = 0.0;
    land.Body = 0.0;
    land.Bearing = 0.0;

    return land;
}

//--- The formations' cast shadows ------------------------------------------------------------------------

float TerrainSunShadow(float3 worldPosition)
{
    float shadow = 1.0;

    [branch]
    if (TerrainShadowArea.w > 0.0)
    {
        float2 uv = (worldPosition.xz - TerrainShadowArea.xy) * TerrainShadowArea.z;
        float lit = tex2Dlod(TerrainShadowSampler, float4(uv, 0.0, 0.0)).r;

        //Past the baked square the land is lit, rather than clamped to its edge texel's shadow
        float inside = all(saturate(uv) == uv) ? 1.0 : 0.0;

        shadow = 1.0 - TerrainShadowArea.w * inside * (1.0 - lit);
    }

    return shadow;
}

//--- The surface ---------------------------------------------------------------------------------------

struct OutbackVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

//How the land at a pixel is lit and hazed. `rocky` is false for the plain's meshes, which never hold rock, and the
//compiler drops every rock term from their program. `onBank` is the bank against the island's drum (#983), whose
//surface is its mesh's (`meshNormal`): the plain's ground worn on it, blended to the plain's normal at its edge.
float3 ShadeOutback(float3 worldPosition, bool rocky, float3 meshNormal, bool onBank)
{
    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The base normal, taken PER PIXEL from the height field's own gradient (three taps) rather than interpolated
    //from a per-vertex normal: smooth whatever the tessellation, and the gullies' and buttresses' shading reaches
    //the pixel even where the mesh is coarser than they are.
    float e = 1.5;
    OutbackLand here = PlainLand(worldPosition.xz);
    if (rocky) here = OutbackHeight(worldPosition.xz);
    float hx = rocky ? OutbackHeight(worldPosition.xz + float2(e, 0.0)).Height : PlainHeight(worldPosition.xz + float2(e, 0.0));
    float hz = rocky ? OutbackHeight(worldPosition.xz + float2(0.0, e)).Height : PlainHeight(worldPosition.xz + float2(0.0, e));

    float2 slope = float2(hx - here.Height, hz - here.Height) / e;
    float3 baseNormal = normalize(float3(-slope.x, 1.0, -slope.y));

    //On the bank: the plain's normal where it lies thin, the bank's where it is deep (the Moon's drift, #983)
    if (onBank)
        baseNormal = normalize(lerp(baseNormal, meshNormal, saturate((worldPosition.y - here.Height) * 1.6)));

    float rockMask = rocky ? smoothstep(0.03, 0.30, here.Rock) : 0.0;

    //The pixel's size measured off the VIEW RAY rather than off the surface: fwidth of the world position steps
    //once per facet on a wall, and a band limit keyed to it draws the facets.
    float3 towardsEye = normalize(CameraPosition - worldPosition);
    float rayFootprint = dist * length(fwidth(towardsEye));

    //A broad field, one evaluation doing three jobs: the rock's shade-to-sun colour, the ground's sand-to-dust
    //colour, and where the spinifex grows thick.
    float broad = GradientNoise2(worldPosition.xz * 0.021);

    //--- The rock ------------------------------------------------------------------------------------
    float3 rock = 0.0;
    float rockSurface = 0.0;
    float varnish = 0.0;
    float cave = 0.0;
    float rib = here.Rib;

    if (rocky)
    {
        //How steep it is here: the crown is bald rock, the walls are streaked
        float steep = saturate(1.0 - baseNormal.y * baseNormal.y);
        float wallGate = smoothstep(0.45, 0.85, steep);

        //The rock's own surface, HEIGHT folded into the domain (an XZ field is constant down a vertical face).
        //The height FIELD's height, not the mesh's, which differs per facet and drew chevrons.
        float rockDomain = 0.28;
        rockSurface = Fbm2BandLimited(worldPosition.xz * rockDomain + here.Height * 0.19, 3, footprint * rockDomain);

        //DESERT VARNISH: a threshold of 3D noise on (bearing, height), the gullies lowering the threshold so the
        //streaks crowd where water gathers, band-limited to the field's own average coverage.
        const float streakDown = 0.03;
        const float streakWidth = 1.4;
        float streak = 0.0;

        [branch]
        if (wallGate * rockMask > 0.0)
        {
            float streakField = GradientNoise3(float3(here.Bearing, here.Height * streakDown));
            float streakFootprint = rayFootprint / streakWidth;
            float streakThreshold = 0.08 - rib * 0.30 + broad * 0.12;
            float streakEdge = streakFootprint * 0.35 + 0.03;

            streak = lerp(1.0 - smoothstep(-0.4, 0.4, streakThreshold),
                smoothstep(streakThreshold - streakEdge, streakThreshold + streakEdge, streakField),
                saturate(1.0 - 1.5 * streakFootprint));
        }

        //Only down a wall, and not in its lowest reach, where the rock flakes and hollows
        varnish = streak * wallGate * smoothstep(0.12, 0.45, here.Body) * VarnishStrength;

        //THE TONE MUST NOT SATURATE: the weights land inside 0..1 for all but the extremes. The gullies are the
        //deeper red.
        float tone = saturate(0.5 + broad * 0.45 - rib * 0.20 + rockSurface * 0.9);

        rock = lerp(RockColorDeep, RockColorBright, tone);

        //FLAKING: pale patches where the oxidised skin has come away, on the walls, mostly low, never under a streak
        float flake = smoothstep(0.12, 0.26, rockSurface + broad * 0.2) * wallGate * (1.0 - streak)
            * lerp(0.35, 1.0, smoothstep(0.8, 0.3, here.Body)) * FlakeStrength;

        rock = lerp(rock, FlakeColor, flake);
        rock = lerp(rock, VarnishColor, varnish);

        //THE HOLLOWS at a wall's foot: a band of shade whose top wanders with the broad field
        float caveTop = saturate(broad * 1.4 + 0.1) * 0.32 + rockSurface * 0.12;
        cave = smoothstep(caveTop, caveTop - 0.08, here.Body) * wallGate;
    }

    //--- The ground ----------------------------------------------------------------------------------
    //SPINIFEX: round tussocks spaced the way plants competing for water space themselves - a CELLULAR field,
    //warped at a third of the spacing (a raw Voronoi is a honeycomb) and the same field jittering the radius.
    float2 spinifexP = worldPosition.xz / SpinifexSpacing;
    float wander = GradientNoise2(spinifexP * 0.34 + 17.0);

    spinifexP += float2(wander, wander * 0.62 - 0.31) * 1.15;

    float patch = saturate(GradientNoise2(worldPosition.xz * 0.035 + 31.4) * 2.8 + 0.15);
    float tussockRadius = SpinifexCover * patch * (0.28 + wander * 0.14);
    float present = saturate(tussockRadius * 12.0);

    //The RIM is band-limited, not the tussock, and the field fades to its own AVERAGE cover, not to bare sand
    float aa = min(footprint / SpinifexSpacing, 0.35);
    float detail = saturate(1.0 - footprint * 2.5 / SpinifexSpacing);
    float cellDistance = Voronoi2(spinifexP);

    //RAGGED, not round: a fine noise frays each tussock's outline
    const float frayFrequency = 2.4;
    cellDistance += GradientNoise2(worldPosition.xz * frayFrequency) * 0.16 * saturate(1.0 - footprint * frayFrequency * 2.0);

    float tussock = (1.0 - smoothstep(tussockRadius - 0.14 - aa, tussockRadius + 0.05 + aa * 2.0, cellDistance)) * present;
    float hummock = lerp(saturate(PI * tussockRadius * tussockRadius), tussock, detail);

    //The shade a tussock throws on the sand round its foot - a ring rather than a cast shadow
    float ringRadius = tussockRadius + 0.09;
    float ring = lerp(saturate(PI * ringRadius * ringRadius) * present,
        (1.0 - smoothstep(tussockRadius, tussockRadius + 0.18 + aa * 2.0, cellDistance)) * present, detail);

    float tip = 1.0 - smoothstep(0.0, tussockRadius * 0.9 + aa, cellDistance);
    float3 tussockColor = SpinifexColor * lerp(0.95, 1.15, lerp(0.5, tip, detail));

    //THE TUSSOCK IS A DOME, (1 - x^2)^2: round over the top and meeting the sand with no crease
    float domeReach = saturate(cellDistance / max(tussockRadius + 0.03, 1e-3));
    float dome = saturate(1.0 - domeReach * domeReach);
    dome *= dome * present;

    float3 soil = lerp(SoilColor, SoilColorPale, saturate(broad * -1.7 + 0.5));

    //The gibber: one hash per pixel over a fine lattice, gone within a few units
    float grainFade = saturate(1.0 - footprint * 90.0);
    soil *= 1.0 + NoiseHash22(floor(worldPosition.xz * 45.0)).x * 0.20 * grainFade;
    soil *= 1.0 - 0.3 * saturate(ring - hummock);

    float3 ground = lerp(soil, tussockColor, hummock);

    //--- Normal and colour -----------------------------------------------------------------------------
    //ONE height field for the fine relief and ONE perturbation off it, the two reliefs lerped by the rock mask
    float relief = lerp(dome * detail * SpinifexRelief, rockSurface * RockRelief, rockMask);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    float3 albedo = lerp(ground, rock, rockMask);

    //Shading in the gullies, independent of the sun: the cheapest ambient occlusion there is, deeper where `rib`
    //is high. The hollows at the foot take their shade here too.
    albedo *= lerp(1.0, (0.70 + 0.38 * saturate(0.5 - rib * 0.6 + rockSurface * 1.4)) * (1.0 - cave * CaveShade),
        rockMask);

    //--- Lighting ------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows: the map (the island, the gun and the bank on the spinifex near the lens) and the
    //formations' own, baked over the whole land. Where both hold one shadow they agree, so the darker one wins.
    float landShadow = TerrainSunShadow(worldPosition);

    [branch]
    if (ShadowStrength > 0.0)
        landShadow = min(landShadow, SunShadow(worldPosition, baseNormal, SunDirection));

    sunlight *= landShadow;

    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light: up-facing ground takes the zenith, faces turned to the skyline take the horizon
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    //The bounce off the lit sand onto the faces turned towards the ground (the walls take it, the plain none)
    float3 bounce = SunColor * saturate(SunDirection.y + 0.2) * SoilColorPale * SoilBounce
        * saturate(1.0 - normal.y) * (1.0 - ndotl * sunlight);

    float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight + bounce);

    //The varnish glint, gated on the varnish, the rock mask and the sun's own reach
    float3 halfway = normalize(SunDirection + towardsEye);
    float gloss = pow(saturate(dot(normal, halfway)), 34.0);

    color += SunColor * gloss * VarnishGloss * varnish * rockMask * ndotl * sunlight;

    //--- The air -------------------------------------------------------------------------------------
    //AN EXTINCTION BY DISTANCE, and nothing else (#967). The light from a surface falls off as exp(-d / HazeDistance)
    //and what replaces it is the AIR: the colour the dome is drawn at the skyline (FarSkyAt(0) - what a horizontal
    //ray of endless air looks like, which is exactly what the haze tends to), leaned towards the dust's own hue by
    //DustStrength. What it replaced reached a pale murk by 480 units whatever stood there: every formation past it
    //came out the same light grey-pink, about twice the plain's brightness and with its red gone (measured,
    //(151,137,138) against (128,59,42) under dome 13), and the far ones, carried on by the far fade to the sky drawn
    //BEHIND them, stood up as pale "hats" against it. Now a far formation keeps its own colour and shading to the
    //far ring, darker than the skyline behind it, and only the last stretch dissolves into the sky (FarFadeToSky).
    //
    //The air's colour is the sky's at the horizon, never the HorizonColor uniform: that one is an average over the
    //dome's bottom fifth, the sun's tint, which under some domes is a colour the dome never draws above the skyline.
    //
    //The ROCK's air is the sky drawn BEHIND it rather than the skyline's: the classic aerial-perspective shortcut, and
    //the one that keeps a far formation from standing paler than the sky over the skyline (the skyline's band is the
    //brightest sky there is, and a formation 1 200 units out hazed towards it measured paler than the sky round it).
    //The plain stays on the skyline's, and the two meet where the rock mask fades out over the apron, at the foot,
    //where the two directions are the same within a degree.
    const float3 luminanceWeights = float3(0.2126, 0.7152, 0.0722);
    bool farSky = FarFade.y > FarFade.x;
    float3 skyline = farSky ? FarSkyAt(0.0) : HorizonColor;
    if (rocky && farSky)
        skyline = lerp(skyline, FarSkyAt((worldPosition.y - CameraPosition.y) / max(dist, 1e-3)), rockMask);

    float3 dust = HazeTint * (dot(skyline, luminanceWeights) / max(dot(HazeTint, luminanceWeights), 1e-3));
    float3 air = lerp(skyline, dust, DustStrength);

    float haze = 1.0 - exp(-dist / HazeDistance);

    //HEAT SHIMMER, keyed to the VIEW RAY, never to the surface (on a near-vertical flank `y` is stripes one unit
    //apart): features of a fixed angular size sliding up against the clock. It rides haze * (1 - haze), so the
    //near ground stays steady and the skyline clean.
    float3 ray = -towardsEye;
    float2 shimmerP = float2(dot(ray.xz, WindDirection) * 44.0 + ray.x * 30.0, ray.y * 90.0 - OutbackTime * 0.55);
    float shimmer = GradientNoise2(shimmerP) * HeatShimmer * haze * (1.0 - haze) * 4.0;

    color = lerp(color, air, saturate(haze + shimmer));

    //THE LAST FADE: the plain takes the far field's (FarFadeToSky, from 600), which is what hides its edge; the ROCK
    //takes its own, later one, towards the same sky. A formation standing above the skyline fades into the sky drawn
    //BEHIND it, which over a pale skyline is what made the far ones pale ghosts, so it holds its own colour to
    //ROCK_FADE_START and is gone by ROCK_FADE_END, inside the distance the discs are laid out to
    //(OutbackRockDiscs.SEARCH_RADIUS). Blended by the rock mask, so where a disc's own plain meets the grid's at the
    //footprint's edge (no rock) the two are the same colour.
    float3 plainFaded = FarFadeToSky(color, worldPosition);

    [branch]
    if (rocky && rockMask > 0.0 && FarFade.y > FarFade.x)
    {
        float3 toPixel = worldPosition - CameraPosition;
        float3 rockFaded = lerp(color, FarSkyAt(toPixel.y / max(dist, 1e-3)), smoothstep(ROCK_FADE_START, ROCK_FADE_END, dist));
        plainFaded = lerp(plainFaded, rockFaded, rockMask);
    }

    return plainFaded;
}

//The plain: the camera grid and the far ring
OutbackVertexOutput OutbackVS(float4 position : POSITION0)
{
    OutbackVertexOutput output;

    //Local grid position + the snapped origin gives the world XZ; the plain is sampled there, so it sits still
    //in the world while the grid slides under it
    float2 worldXZ = position.xz + OriginXZ;
    float3 worldPosition = float3(worldXZ.x, PlainHeight(worldXZ), worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

float4 OutbackPS(OutbackVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);
    FarRingClip(worldPosition.xz);

    //Every formation's footprint is its own mesh's: the plain stops on it
    clip(InsideMonolith(worldPosition.xz) || InsideOutcrop(worldPosition.xz) ? -1.0 : 1.0);

    //And so it does under the bank, which is the plain too (#983, BankCover.fxh)
    clip(UnderBank(worldPosition.xz) ? -1.0 : 1.0);

    return float4(ShadeOutback(worldPosition, false, float3(0.0, 1.0, 0.0), false), 1.0);
}

technique Outback
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL OutbackVS();
        PixelShader = compile PS_SHADERMODEL OutbackPS();
    }
};

//Declared after the plain's own technique, which the backdrop leaves current and so must stay the effect's first
//THE BANK AGAINST THE DRUM (#983): IslandBerm's mesh worn in the plain itself - its soil, spinifex and the formations'
//baked shadow - so the drum rises out of the ground and not out of a ring of flat colour laid round it
struct OutbackBankVertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TextureCoordinate : TEXCOORD0;
};

struct OutbackBankVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 Normal : TEXCOORD1;
};

OutbackBankVertexOutput OutbackBankVS(OutbackBankVertexInput input)
{
    OutbackBankVertexOutput output;
    output.WorldPosition = input.Position.xyz;
    output.Normal = input.Normal;
    output.Position = mul(mul(float4(input.Position.xyz, 1.0), View), Projection);
    return output;
}

float4 OutbackBankPS(OutbackBankVertexOutput input) : COLOR
{
    return float4(ShadeOutback(input.WorldPosition, false, normalize(input.Normal), true), 1.0);
}

technique OutbackBank
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL OutbackBankVS();
        PixelShader = compile PS_SHADERMODEL OutbackBankPS();
    }
};

//The formations: one polar mesh, instanced over every footprint (OutbackBackdrop's rock discs). The mesh is a unit
//disc; each instance carries its footprint's centre and long axis (TEXCOORD1) and its two semi-axes, already
//widened a hair past the ellipse, and whether it is a boulder (TEXCOORD2).
struct RockVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float Outcrop : TEXCOORD1;
};

RockVertexOutput OutbackRockVS(float2 disc : POSITION0, float4 placement : TEXCOORD1, float4 size : TEXCOORD2)
{
    RockVertexOutput output;

    float2 along = placement.zw;
    float2 across = float2(-along.y, along.x);
    float2 worldXZ = placement.xy + along * (disc.x * size.x) + across * (disc.y * size.y);
    float3 worldPosition = float3(worldXZ.x, OutbackHeight(worldXZ).Height, worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);
    output.Outcrop = size.z;

    return output;
}

float4 OutbackRockPS(RockVertexOutput input) : COLOR
{
    //A boulder's mesh gives way to a monolith's where the two footprints overlap, so no point is drawn twice
    clip(input.Outcrop > 0.5 && InsideMonolith(input.WorldPosition.xz) ? -1.0 : 1.0);

    return float4(ShadeOutback(input.WorldPosition, true, float3(0.0, 1.0, 0.0), false), 1.0);
}

technique OutbackRock
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL OutbackRockVS();
        PixelShader = compile PS_SHADERMODEL OutbackRockPS();
    }
};

//--- The bake (#967) ------------------------------------------------------------------------------------
//One pass over a square of the world round the arena, once per sun: from the land at each texel a ray is walked
//towards the sun through the very height field the land is drawn by, and what comes back is how much of the sun
//reaches it, soft at the edge (the clearance angle over the distance, the usual soft-shadow march). The marched
//distances crowd near the start, where the land's own relief decides; the walk ends where the ray clears the
//tallest thing the lattices can raise, or BAKE_MAX_REACH.
#define BAKE_STEPS 48
#define BAKE_MAX_REACH 640.0
#define BAKE_LIFT 0.3
#define BAKE_SOFTNESS 22.0

float4 OutbackSunBakeVS(float4 position : POSITION0) : SV_POSITION
{
    return position;
}

float4 OutbackSunBakePS(float4 screen : SV_POSITION) : COLOR
{
    float2 uv = screen.xy * BakeArea.w;
    float2 p = BakeArea.xy + uv * BakeArea.z;
    float start = OutbackHeight(p).Height + BAKE_LIFT;

    float across = max(length(BakeSun.xz), 1e-3);
    float2 toward = BakeSun.xz / across;
    float rise = BakeSun.y / across;

    float top = OutbackLevelY + PlainRelief + RockHeight * max(ClusterHeight, 1.0) + OutcropHeight;
    float reach = clamp((top - start) / max(rise, 0.05), 1.0, BAKE_MAX_REACH);

    float lit = 1.0;

    [loop]
    for (int i = 1; i <= BAKE_STEPS; i++)
    {
        float s = reach * pow(i / (float)BAKE_STEPS, 1.6);
        float clearance = start + rise * s - OutbackHeight(p + toward * s).Height;

        lit = min(lit, saturate(BAKE_SOFTNESS * clearance / s));
    }

    return float4(lit, lit, lit, 1.0);
}

technique OutbackSunBake
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL OutbackSunBakeVS();
        PixelShader = compile PS_SHADERMODEL OutbackSunBakePS();
    }
};

//--- The height probe (#590) ----------------------------------------------------------------------------

//TerrainMirror.Outback's field, for the Testbed's mirrorcheck (see HeightProbe.fxh).
#define HEIGHT_PROBE_MIRRORED(p) OutbackHeight(p).Height
#include "HeightProbe.fxh"
