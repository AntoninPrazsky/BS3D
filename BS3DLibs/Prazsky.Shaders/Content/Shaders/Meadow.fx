//Draws a flowering meadow: lush green rolling hills scattered with wildflowers, a clearing the arena sits
//in with the hills rolling up in the distance. Sixth scene variant (NumPad2 cycles ... -> meadow). The
//look is the Windows XP "Bliss" hill - smooth vivid green under a blue sky - and the motion is the wind:
//bands of it comb through the grass, while the shared cloud shadows drift over the whole field. The
//round stone island stays as the platform standing in the meadow.
//
//Real geometry like the desert and the mountains - a camera-centred grid (shared CreateGridMesh on the C#
//side) displaced by a smooth rolling field, low around the arena and rising into hills with distance, its
//normal taken by finite differences. Drawn in both executables, Shader Model 5.0, no OPENGL branch.

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
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground (the flat clearing otherwise slices
//across the funnel just below its rim, hiding its depth and swallowing the balls falling through). The Testbed
//sets this to the island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

float MeadowLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

float MeadowTime;
float2 WindDirection;

//Grass (linear) and the darker green it varies towards in patches, how much sky fills the flats, and the
//distance over which the field melts into the skyline
float3 GrassColor;
float3 GrassColorDark;
float AmbientStrength;
float HorizonHazeDistance;

//Wind combing the grass: how fast the bright/dark bands travel, how far apart they are, how deep they cut
float WindRippleSpeed;
float WindRippleFrequency;
float WindRippleStrength;

//Fine grass texture (a normal-tilting height field), its amplitude and blades-per-world-unit
float GrassReliefStrength;
float GrassReliefFrequency;

//THE GRASS AS A MATERIAL (#281). The relief above is the same bump-and-matte recipe the mountain's rock and
//the island's stone use, which is why the field read as green stone: nothing in it said "blades". What does,
//read off real meadows (references rendered for #281): clumps of slightly different greens with darker seams
//between them, light dry tips over dark hollows, broad drier patches, a velvety sheen where the field is seen
//edge-on, and blades that glow when the sun is behind them.
float3 GrassTipColor;        //the light, drier colour a blade's tip takes, linear
float GrassTipStrength;      //how far tips lighten and hollows darken, 0..1
float GrassClumpSize;        //world units across one clump
float GrassClumpStrength;    //how much clumps differ and how dark the seams between them are, 0..1
float GrassDryPatchStrength; //how far the broad dry patches pull towards yellow-green, 0..1
float GrassSheenStrength;    //the velvet: light the field gives back where it is seen edge-on
float GrassTranslucency;     //the glow of blades with the sun behind them

//Wildflowers: how many of the grid cells carry one, how far apart the cells are, and the flower size
float FlowerDensity;
float FlowerSpacing;
float FlowerSize;

//The footpath (#609): its bearing out of the clearing, its trodden width and its wander. MeadowPath.cs is the CPU
//copy of PathLateral - change the one and the other has to follow.
float PathBearing;
float PathWidth;
float PathMeander;
static const float PATH_START = 0.72;                    //MeadowPath.START
static const float3 PATH_DIRT = float3(0.20, 0.155, 0.10);
static const float3 PATH_VERGE = float3(0.16, 0.20, 0.07);
static const float PATH_RAGGED = 0.35;                    //how far the noise moves the path's edges, world units

//The brook (#609): MeadowPath.BrookLateral is its CPU copy
float BrookBearing;
float BrookWidth;
float BrookMeander;
static const float BROOK_START = 0.82;                    //MeadowPath.BROOK_START
static const float3 BROOK_BANK = float3(0.07, 0.11, 0.035);
static const float3 BROOK_BED = float3(0.035, 0.05, 0.045);
static const float BROOK_RAGGED = 0.3;                    //and the brook's

//The pond the brook runs into and the knoll the footpath climbs to its old tree (#609's third round). The owner: the
//brook "ends abruptly in the middle of the meadow ... it should flow into something, a pond for example", and "a path
//leads somewhere, to a tree or a small hill". Both stand where the two lines used to stop short in the grass
//(MeadowPath.PondCentre and PathEnd). PondLevel is the field's own height at the pond's centre, worked out on the CPU
//(TerrainMirror.MeadowNatural), and the ground round the pond is eased to it so the water lies level.
float2 PondCentre;
float PondRadius;
float PondLevel;
float2 KnollCentre;
float KnollRadius;
float KnollHeight;
static const float POND_LEVEL_FROM = 1.3;                 //TerrainMirror.MEADOW_POND_LEVEL_FROM
static const float POND_LEVEL_FADE = 14.0;                //TerrainMirror.MEADOW_POND_LEVEL_FADE
static const float POND_OUTLINE_MAX = 1.3;                //PondOutline's bound: 1 + 0.16 + 0.09 + 0.05
static const float POND_BANK = 2.6;                       //the wet bank's width outside the shore, world units
static const float POND_RAGGED = 0.35;                    //how far the noise moves the shore
static const float POND_SHELF = 3.0;                      //how far in from the shore the bed shows through the shallows
static const float3 POND_SHALLOW_BED = float3(0.075, 0.07, 0.035);
static const float LILY_SPACING = 1.5;                    //one pad a cell, world units
static const float3 LILY_PAD = float3(0.03, 0.085, 0.018);
static const float3 LILY_FLOWER = float3(0.92, 0.9, 0.86);
//The ground under the old tree at the path's end: trodden bare round its foot, worn round that (world units)
static const float TREE_BARE = 4.8;
static const float TREE_WORN = 9.5;

float BrookLateral(float2 p)
{
    float d = length(p);
    float angle = atan2(p.y, p.x) - BrookBearing;
    angle -= 6.2831853 * round(angle / 6.2831853);
    float wander = BrookMeander * (0.6 * sin(d * 0.017 + 2.4) + 0.4 * sin(d * 0.049 + 0.3));
    float off = step(d, ClearingRadius * BROOK_START) + step(1.5707963, abs(angle));
    return d * angle - wander + off * 1e4;
}

float PathLateral(float2 p)
{
    float d = length(p);
    float angle = atan2(p.y, p.x) - PathBearing;
    angle -= 6.2831853 * round(angle / 6.2831853);
    float wander = PathMeander * (0.7 * sin(d * 0.021 + 0.6) + 0.3 * sin(d * 0.057 + 2.1));
    //Off its length the answer is far enough away to be no path at all
    float off = step(d, ClearingRadius * PATH_START) + step(1.5707963, abs(angle));
    return d * angle - wander + off * 1e4;
}

//Gentle rolling hills: smooth sines (not the mountains' ridges), low around the arena centre (world
//origin) and rising into hills with distance, so the meadow is flat where the arena stands and rolls up
//towards the horizon. Sampled three times per vertex for the finite-difference normal.
float TerrainHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float rolling = 0.5 * sin(dot(p, float2(0.020, 0.015)))
        + 0.3 * sin(dot(p, float2(-0.013, 0.024)) + 1.5)
        + 0.2 * sin(dot(p, float2(0.031, 0.026)) + 3.0);

    float basin = ClearingRelief * sin(dot(p, float2(0.05, 0.035)));

    float natural = MeadowLevelY + basin + HillHeight * ramp * (rolling * 0.5 + 0.5);

    //The pond lies level and the knoll rises under the old tree (#609's third round) - TerrainMirror.Meadow, term for term
    float pondLevel = 1.0 - smoothstep(PondRadius * POND_LEVEL_FROM, PondRadius * POND_LEVEL_FROM + POND_LEVEL_FADE,
        distance(p, PondCentre));
    float2 k = p - KnollCentre;
    float knoll = saturate(1.0 - dot(k, k) / (KnollRadius * KnollRadius));
    return natural + (PondLevel - natural) * pondLevel + KnollHeight * knoll * knoll;
}

struct MeadowVertexInput
{
    float4 Position : POSITION0;
};

struct MeadowVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
};

MeadowVertexOutput MeadowVS(MeadowVertexInput input)
{
    MeadowVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float h = TerrainHeight(xz);

    float e = 2.0;
    float hx = TerrainHeight(xz + float2(e, 0.0));
    float hz = TerrainHeight(xz + float2(0.0, e));
    output.WorldNormal = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));

    float3 worldPosition = float3(xz.x, h, xz.y);
    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//How far the grass is stretched ALONG the wind, and the gain that carries fBm to the amplitude the two
//crossed sines here used to have. Its own values rather than the savanna's, though both start at the same
//figures: this is a lush lawn where that is dry veld, and the two scenes are meant to differ. See the
//savanna's copy for what each number is answering.
static const float GRASS_COMB_STRETCH = 2.6;
static const float GRASS_FBM_GAIN = 5.0;

//The rosettes' form (#127), as fractions of a flower's own world size: how high a petal domes, how much
//the golden eye rises over the petals, and how hard the grass darkens in the contact ring just outside
//the rim. The flowers' fabric rather than a mood — the mood dials (density, spacing, size) stay on the
//config — and the heights are fractions so a big rosette is proportionally domed rather than uniformly.
static const float FLOWER_PETAL_RELIEF = 0.20;
static const float FLOWER_EYE_RELIEF = 0.16;
static const float FLOWER_CONTACT_SHADOW = 0.22;
//The flowers' drifts (#609): a patch of this many world units is mostly one of FLOWER_SPECIES kinds
static const float FLOWER_PATCH = 16.0;
static const float FLOWER_SPECIES = 5.0;
//Their sizes (#609's third round), against the species' own: the smallest and the largest a flower rolls, the roll
//cubed so most are small and a few big - see where it is rolled
static const float FLOWER_SMALLEST = 0.3;
static const float FLOWER_LARGEST = 1.3;
//The small flowers under them (#609's third round): one to a cell this many world units across, this big (a radius,
//world units), and in a drift this much denser than the rosettes are
static const float SMALL_FLOWER_SPACING = 0.8;
static const float SMALL_FLOWER_RADIUS = 0.075;
static const float SMALL_FLOWER_DENSITY = 2.0;

#include "Grass.fxh"

float4 MeadowField(MeadowVertexOutput input, bool detail)
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);
    FarRingClip(worldPosition.xz);

    float3 baseNormal = normalize(input.WorldNormal);
    float footprint = length(fwidth(worldPosition.xz));

    //Wildflowers: little rosettes rather than dots — a ring of petals around a bright eye, each with its
    //own petal count, size, rotation and colour. One per grid cell that draws one, faded against the
    //footprint so the distant meadow stays clean green instead of a shimmer. Evaluated BEFORE the normal
    //since #127: a flower is no longer only an albedo swap — it has a height of its own, and that height
    //goes through the very perturbation the grass relief rides.
    float2 cell = floor(worldPosition.xz / FlowerSpacing);
    float2 within = frac(worldPosition.xz / FlowerSpacing);

    //IN DRIFTS AND BY SPECIES (#609). The owner: the flowers are boring and they repeat - one rosette formula,
    //evenly sown. A meadow's flowers come in patches, and a patch is mostly one kind. So the density swells and
    //thins over tens of metres (a drift field), and every FLOWER_PATCH-wide patch has its own species, which
    //two flowers in five ignore for a neighbour's - the edges of patches mix, as a real meadow's do.
    float driftField = saturate(CloudNoise(worldPosition.xz * 0.045 + 3.7) * 0.5 + 0.5);
    float density = FlowerDensity * (0.35 + 2.4 * driftField * driftField);
    float present = step(1.0 - density, Hash21(cell));

    float patchSpecies = floor(Hash21(floor(worldPosition.xz / FLOWER_PATCH) + 41.3) * FLOWER_SPECIES);
    float ownSpecies = floor(Hash21(cell + 23.9) * FLOWER_SPECIES);
    float species = Hash21(cell + 61.1) < 0.4 ? ownSpecies : patchSpecies;

    //The footpath (#609): trodden dirt down the middle, a verge of worn short grass either side, both with a
    //ragged edge. No flower grows on it.
    //
    //ONLY NEAR THE LINE, and exactly so. The edge is ragged by PATH_RAGGED of a noise that never leaves ±1 (the
    //bound of GradientNoise2 over every gradient its hash can deal, reached at a cell's centre), so a pixel further
    //from the line than the widest term's edge plus that amplitude comes out trodden 0 and verge 0 whatever the
    //noise says - and its noise is not evaluated. The path and the brook cross a sliver of the field and their
    //three noises ran on every pixel of it: measured on the APU (Toadstool, retired in #649; 1600x900) the path cost 0.61 ms at Low
    //and the brook 0.38, most of the meadow's 12 % rise with #609, and this branch and the brook's took 0.50 of it
    //back (1.02 at High; "What #609 costs on the APU" in docs/scenes.md). The dirt's grain rides in the same
    //branch, since it is only ever mixed in by trodden.
    float lateral = abs(PathLateral(worldPosition.xz));
    float treeDistance = distance(worldPosition.xz, KnollCentre);
    float trodden = 0.0, verge = 0.0, dirt = 1.0;

    [branch]
    if (lateral < max(PathWidth * 1.9, PathWidth * 0.5 + 0.15) + PATH_RAGGED || treeDistance < TREE_WORN + 2.0 * PATH_RAGGED)
    {
        float ragged = PATH_RAGGED * GradientNoise2(worldPosition.xz * 0.9);
        trodden = 1.0 - smoothstep(PathWidth * 0.5 - 0.15, PathWidth * 0.5 + 0.15, lateral + ragged);
        verge = 1.0 - smoothstep(PathWidth, PathWidth * 1.9, lateral + ragged);

        //Where it arrives (#609's third round): the ground under the old tree on the knoll, trodden bare round the
        //foot where walkers stop and sit, worn round that - more ragged than the path, as a patch is
        trodden = max(trodden, 1.0 - smoothstep(TREE_BARE - 0.4, TREE_BARE + 0.4, treeDistance + 2.0 * ragged));
        verge = max(verge, 1.0 - smoothstep(TREE_BARE, TREE_WORN, treeDistance + 2.0 * ragged));
        dirt = 0.85 + 0.3 * GradientNoise2(worldPosition.xz * 2.3);
    }

    present *= 1.0 - verge;

    //The brook (#609): water down the middle, a wet dark bank either side, no flower near it. Only near its line,
    //for the path's reason.
    float brookLateral = abs(BrookLateral(worldPosition.xz));
    float water = 0.0, bank = 0.0;

    [branch]
    if (brookLateral < max(BrookWidth * 1.4, BrookWidth * 0.5 + 0.2) + BROOK_RAGGED)
    {
        float brookRagged = BROOK_RAGGED * GradientNoise2(worldPosition.xz * 0.7 + 9.1);
        water = 1.0 - smoothstep(BrookWidth * 0.5 - 0.2, BrookWidth * 0.5 + 0.2, brookLateral + brookRagged);
        bank = 1.0 - smoothstep(BrookWidth * 0.5, BrookWidth * 1.4, brookLateral + brookRagged);
    }

    //The pond it runs into (#609's third round): the same water and bank round a soft irregular shore. Only near it,
    //for the path's reason - the shore never reaches past POND_OUTLINE_MAX of the mean radius, and its ragged edge
    //past POND_RAGGED. pondDepth is how far in from the shore, as a share of the shallow shelf.
    float2 pondOffset = worldPosition.xz - PondCentre;
    float pondReach = PondRadius * POND_OUTLINE_MAX + POND_BANK + POND_RAGGED;
    float pondWater = 0.0, pondDepth = 1.0;

    [branch]
    if (dot(pondOffset, pondOffset) < pondReach * pondReach)
    {
        float a = atan2(pondOffset.y, pondOffset.x);
        //MeadowPath.PondShore
        float shore = length(pondOffset) - PondRadius * (1.0 + 0.16 * sin(2.0 * a + 0.7) + 0.09 * sin(3.0 * a + 2.3)
            + 0.05 * sin(5.0 * a + 4.1));
        shore += POND_RAGGED * GradientNoise2(worldPosition.xz * 0.6 + 3.3);
        pondWater = 1.0 - smoothstep(-0.25, 0.25, shore);
        pondDepth = saturate(-shore / POND_SHELF);
        water = max(water, pondWater);
        bank = max(bank, 1.0 - smoothstep(0.0, POND_BANK, shore));
    }

    present *= 1.0 - bank;

    //Per-flower character: the species' petal count, petal shape and size, then the cell hash's own variation.
    //0 oxeye daisy: many thin white rays round a big yellow eye. 1 buttercup: five round glossy yellow petals.
    //2 poppy: four broad red petals round a black eye, the biggest. 3 cornflower: ragged blue rays. 4 clover: a
    //round pink-purple head of florets and no petals at all.
    float petalCount = species < 0.5 ? 13.0 : (species < 1.5 ? 5.0 : (species < 2.5 ? 4.0 : (species < 3.5 ? 9.0 : 1.0)));
    float petalShape = species < 0.5 ? 2.2 : (species < 1.5 ? 0.6 : (species < 2.5 ? 0.35 : (species < 3.5 ? 3.0 : 0.0)));
    float speciesSize = species < 0.5 ? 1.0 : (species < 1.5 ? 0.75 : (species < 2.5 ? 1.35 : (species < 3.5 ? 0.85 : 0.6)));
    float eyeShare = species < 0.5 ? 0.38 : (species < 1.5 ? 0.22 : (species < 2.5 ? 0.26 : (species < 3.5 ? 0.24 : 0.0)));
    float rotation = Hash21(cell + 9.9) * 6.2831853;

    //MOSTLY SMALL, A FEW BIG (#609's third round). The owner: the flowers are too big for what stands round them;
    //the big ones can stay, but most should be smaller, or small. The roll is cubed, so half the flowers are under a
    //quarter of the old middle size's worth above FLOWER_SMALLEST and one in ten reaches past three quarters of the
    //range; the largest are the largest they ever were (0.7 to 1.3 of the species' size, evenly, until then).
    float sizeRoll = Hash21(cell + 2.2);
    float size = min(FlowerSize * speciesSize
        * (FLOWER_SMALLEST + (FLOWER_LARGEST - FLOWER_SMALLEST) * sizeRoll * sizeRoll * sizeRoll), 0.45);

    //The centre may only wander in [size, 1-size], so the whole flower stays inside its cell and no petal
    //is cut off by the cell edge (the flower is only evaluated within its own cell's fraction).
    float2 flowerCentre = size + float2(Hash21(cell + 3.1), Hash21(cell + 7.7)) * (1.0 - 2.0 * size);
    float2 delta = within - flowerCentre;
    float radius = length(delta);
    float angle = atan2(delta.y, delta.x);

    //The scalloped outer edge: petalCount rounded lobes around the centre. |cos(N*angle/2)| makes N lobes
    //and, being even in the angle, stays continuous across the atan2 seam; the power rounds the petals out.
    //A clover head has no petals: petalShape 0 makes the lobes a flat 1, a round head, and its floret texture
    //is the cell hash below
    float lobes = petalShape > 0.0 ? pow(abs(cos(petalCount * (angle + rotation) * 0.5)), petalShape) : 1.0;
    float petalEdge = size * (petalShape > 0.0 ? 0.34 + 0.66 * lobes : 0.8);
    float centreEdge = size * eyeShare;

    //A small flower is lost to the pixel sooner than a big one, and fades sooner: the old size's flowers (a
    //quarter of the cell and up) keep the old fade, a cell wide in the footprint
    float resolvable = saturate(1.0 - footprint / (FlowerSpacing * saturate(0.35 + 2.5 * size)));
    float aa = fwidth(radius) * 1.5 + 1e-4;

    float flowerMask = present * (1.0 - smoothstep(petalEdge - aa, petalEdge + aa, radius)) * resolvable;
    float centreMask = 1.0 - smoothstep(centreEdge - aa, centreEdge + aa, radius);

    //The rosette's own height (#127): each petal a dome — full mid-petal, falling to nothing at the
    //scalloped rim, dipping in the gaps between petals (the lobes term) — and the eye a raised boss over
    //them. In WORLD units like the grass relief it is summed with, so the perturbation's derivatives read
    //both as one surface; scaled by the flower's own world size, so a big flower is proportionally domed.
    //Zero by construction at the cell border (the profile dies at the rim, and the rim never reaches the
    //border), so the per-cell hashes cannot tear the height field where cells meet.
    float petalProfile = saturate(1.0 - radius / max(petalEdge, 1e-4));
    float eyeDome = 1.0 - smoothstep(0.0, centreEdge * 1.4, radius);
    float flowerRelief = present * resolvable * size * FlowerSpacing
        * (FLOWER_PETAL_RELIEF * petalProfile * (0.35 + 0.65 * lobes) + FLOWER_EYE_RELIEF * eyeDome);

    //How much of this pixel the rosette OWNS, for the grass texture below — 1 at the flower's centre,
    //falling to 0 at the scalloped rim (#283).
    //
    //⚠ THE SMOOTH PROFILE AND NOT flowerMask, AND THAT IS THE WHOLE CARE IN THIS FIX. The mask is
    //antialiased over `aa`, i.e. a pixel and a half — and this weight goes into a height field that
    //PerturbNormalFromHeight differentiates, so a mask-shaped falloff would put a pixel-wide step in the
    //DERIVATIVE and light a hard ring right around every rosette. That is the seam the fix has to avoid,
    //and it would have looked like a new bug rather than a botched fix for the old one. petalProfile is
    //linear in radius, so its derivative is bounded by 1/petalEdge and the crossover is invisible. It is
    //also the same profile the flower's own dome is built on, which is what makes the two complementary:
    //where the flower stands tallest the grass texture is gone entirely, and they trade off at the rim.
    //Zero at the cell border by the same construction that keeps flowerRelief zero there.
    float flowerCover = present * resolvable * petalProfile;

    //Fine grass texture tilts the normal — and the rosette tilts it with it, one height field through one
    //perturbation. The grass's share is FADED OUT under the rosette (#283): summed in flat, it put the
    //ground's own bumps and whatever the gust was doing at that point onto the petals, so a flower wore the
    //grass's shading with its dome added on top rather than catching the sun on its own shape. The comment
    //here claimed the latter and the code did the former — true only against the pre-#127 state, where a
    //flower had no height at all and was a pure albedo swap. The colour had always isolated itself
    //correctly (`lerp(grass, flowerColor, flowerMask)` replaces outright); this is the normal being given
    //the same treatment.
    //ONE gust field, and everything the wind does reads off it (#276) — the grass leans by it below and the
    //shading darkens by it further down, so the bend and the band cannot disagree about where the wind is.
    //The speed handed over is the old plane wave's own PHASE speed, WindRippleSpeed / WindRippleFrequency,
    //so the gusts cross the field at exactly the rate the authored dials always meant; what changes is that
    //they are gusts instead of one straight edge 42 units wide.
    float gust = WindGust(worldPosition.xz, WindDirection, MeadowTime, WindRippleFrequency,
        WindRippleSpeed / max(WindRippleFrequency, 1e-4), footprint);

    float relief = GrassRelief(worldPosition.xz, footprint, gust) * (1.0 - flowerCover) * (1.0 - 0.8 * verge);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief + flowerRelief);

    //Grass color, varied in broad patches so the field is not one flat green
    float patch = CloudNoise(worldPosition.xz * 0.15) * 0.5 + 0.5;
    float3 grass = lerp(GrassColorDark, GrassColor, patch);

    //Broader still, the DRY patches (#281): stretches of the field a few tens of metres across where the grass
    //has gone yellow-green. Squared, so most of the meadow stays lush and the dry ground comes in islands.
    float dry = saturate(CloudNoise(worldPosition.xz * 0.043 + 17.3) * 0.5 + 0.5);
    grass = lerp(grass, grass * float3(1.20, 1.03, 0.62), GrassDryPatchStrength * dry * dry);

    //CLUMPS (#281): grass grows in tufts, and a tuft is its own shade of green with a darker seam of shadow
    //between it and the next. The zero-crossings of a gradient noise one clump across give the seams — a
    //connected web of thin lines, which is what a cellular field would give, at one noise instead of Voronoi2's
    //nine hashes (measured: 0.43 → 0.36 ms added at the near camera) — and a noise at the same scale gives each
    //clump its own brightness.
    //Band-limited as a whole: once a clump is a couple of pixels the seams would shimmer, so the lot fades to
    //its mean and the far field is simply green.
    //One of the reduced program's two cuts (see MeadowReducedPS).
    if (detail)
    {
        float clumpFade = saturate(1.0 - 2.5 * footprint / max(GrassClumpSize, 1e-3));
        float2 clumpDomain = worldPosition.xz / max(GrassClumpSize, 1e-3);

        //⚠ THE SEAM IS THE ZERO SET OF TWO NOISES, NOT ONE (#674). A gradient noise is exactly zero at every integer
        //lattice point (every dot product in GradientNoise2 is against f, and f is 0 there), so |noise| is small in a
        //disc round each of them and the web of its zero crossings ties a knot at every lattice point - a lattice of dark
        //spots one clump apart. That is arithmetic. Tools/LatticeProbe measured the meadow from above at 83 % of its
        //scored tiles over the noise null (50 of 60; one wave, period 15 px, on the axis) and 2 % after; a continuous
        //narrow-band texture is not flagged by it (its ring case: one tile in ten), so what it saw was a lattice. The second noise is turned and at another scale, so the two zero sets share no
        //lattice point; 0.7 each keeps the sum's spread about what one noise's was, so the threshold below means
        //what it did.
        float2 seamDomain = float2(clumpDomain.x * 0.8 - clumpDomain.y * 0.6, clumpDomain.x * 0.6 + clumpDomain.y * 0.8) * 1.37 + 7.3;
        float seam = 1.0 - smoothstep(0.0, 0.22, abs(0.7 * (GradientNoise2(clumpDomain) + GradientNoise2(seamDomain))));
        float clumpShade = GradientNoise2(clumpDomain * 0.7 + 5.1);
        grass *= 1.0 + GrassClumpStrength * clumpFade * (0.32 * clumpShade - 0.55 * seam + 0.2);
        grass = lerp(grass, grass * float3(1.10, 1.0, 0.82), GrassClumpStrength * clumpFade * saturate(clumpShade));
    }

    //TIPS AND HOLLOWS (#281), off the very relief that tilts the normal: where the combed field stands high
    //the blades' light, drier tips are showing, where it dips the eye is looking down into the shade between
    //them. Normalised to the relief's own amplitude, so it means the same at any GrassReliefStrength; and it
    //needs no band limit of its own — the relief's octaves already fade with the footprint, so this fades
    //with them and the far field keeps its mean.
    float blade = relief / max(GrassReliefStrength * GRASS_FBM_GAIN, 1e-4);
    grass *= 1.0 + 0.32 * GrassTipStrength * clamp(blade, -1.0, 1.0);
    grass = lerp(grass, GrassTipColor, 0.45 * GrassTipStrength * saturate(blade));

    //BLADES SEEN FROM THE SIDE (#281). A standing blade is a vertical stroke to a camera looking across the
    //field, and nothing drawn on the ground plane is — every other term here is isotropic or combed along the
    //wind. A fine noise stretched along the ground direction TOWARDS THE CAMERA projects to exactly those
    //strokes: lines radiating from the vanishing point, upright in front of the lens. Light where a tip catches
    //the sky, dark in the gaps. Two octaves, band-limited like everything at this scale, so it is the near
    //field's and is gone well before it could shimmer.
    //The reduced program's other cut.
    if (detail)
    {
        float strokeFrequency = GrassReliefFrequency * 4.0;
        float strokes = Fbm2Combed(worldPosition.xz * strokeFrequency, CameraPosition.xz - worldPosition.xz,
            6.0, 2, footprint * strokeFrequency);
        grass *= 1.0 + 0.85 * GrassTipStrength * strokes;
        grass = lerp(grass, GrassTipColor, 0.5 * GrassTipStrength * saturate(strokes * 2.0));
    }

    //Wind combing the grass: the gust computed above, darkening the blades it lays over and letting the ones
    //behind it stand back up. Same dial and the same ±12 % it always had; what it is applied to is a patch
    //travelling downwind rather than an infinite plane wave (#276 — see WindGust in Noise.fxh).
    grass *= 1.0 + gust * WindRippleStrength;

    //The species' colours, a little varied flower to flower
    float shade = 0.9 + 0.2 * Hash21(cell + 13.7);
    float3 petalColor = species < 0.5 ? float3(0.96, 0.96, 0.92)
        : (species < 1.5 ? float3(0.98, 0.82, 0.10)
        : (species < 2.5 ? float3(0.86, 0.09, 0.05)
        : (species < 3.5 ? float3(0.22, 0.34, 0.92)
        : float3(0.78, 0.36, 0.62) * (0.8 + 0.4 * Hash21(floor(within * 40.0) + cell)))));
    petalColor *= shade;
    float3 eyeColor = species < 0.5 ? float3(0.98, 0.74, 0.12)
        : (species < 1.5 ? float3(0.80, 0.78, 0.20)
        : (species < 2.5 ? float3(0.05, 0.04, 0.05)
        : float3(0.14, 0.12, 0.45)));

    //The face of a petal against its rim (#127): a cheap occlusion gradient — the face keeps its colour,
    //the scalloped rim falls into shade, the eye brightens at its very centre — so a rosette reads as a
    //form even where the light is flat. The normal above carries the real shape; this carries the ambient
    //half the hemisphere term is too broad to give.
    float3 flowerColor = petalColor * (0.80 + 0.28 * petalProfile);
    flowerColor = lerp(flowerColor, eyeColor * (0.9 + 0.35 * eyeDome), centreMask * step(0.001, eyeShare));

    //And a hint of the flower standing OVER the grass: a narrow contact shadow just outside the petals.
    //Inside the rosette it darkens grass the petals then replace, which leaves exactly the AA fringe of
    //the rim reading as the petal's own cast edge.
    float contact = present * resolvable * (1.0 - smoothstep(petalEdge, petalEdge * 1.45, radius));
    grass *= 1.0 - FLOWER_CONTACT_SHADOW * contact;

    //THE SMALL FLOWERS (#609's third round). Every reference meadow is thick with tiny ones - daisies and buttercups in
    //their hundreds, a few clover heads - under a scatter of big ones, and a grid of rosettes 2.2 units apart cannot be
    //that. So a finer grid of plain discs under them: at the size they are drawn a ring of petals is a pixel or two,
    //and a white disc with a yellow eye is what reads as a daisy. They follow the same drifts, mostly the patch's own
    //kind where it is a daisy or a buttercup patch, and they keep off the path and the banks as the rosettes do. Past
    //their resolution each drift keeps its colour as a tint instead of its flowers, which is what a flowering meadow
    //looks like from a hill - so where the discs fade out the field does not go plain green.
    //Only where they can be drawn, behind a branch on the footprint - a distance, so whole stretches of the frame
    //take one side - and past that only the tint below, which needs no cell of its own: measured at 0.125 ms on the
    //desktop's High with every pixel of the field walking the cell, and the far field is most of the pixels.
    float smallDensity = saturate(FlowerDensity * SMALL_FLOWER_DENSITY * (0.2 + 2.6 * driftField * driftField))
        * (1.0 - verge) * (1.0 - bank);
    float smallResolvable = saturate(1.5 - footprint / (SMALL_FLOWER_RADIUS * 2.5));

    [branch]
    if (smallResolvable > 0.0)
    {
        float2 smallCell = floor(worldPosition.xz / SMALL_FLOWER_SPACING);
        float2 smallWithin = frac(worldPosition.xz / SMALL_FLOWER_SPACING);
        //Their kind: the patch's where it is daisies or buttercups, three in four; otherwise daisy, buttercup or clover
        float smallKind = Hash21(smallCell + 13.1) < 0.75 && patchSpecies < 1.5 ? patchSpecies : floor(Hash21(smallCell + 29.3) * 2.99);
        float3 smallColor = smallKind < 0.5 ? float3(0.95, 0.95, 0.9) : (smallKind < 1.5 ? float3(0.98, 0.8, 0.1) : float3(0.78, 0.36, 0.62));
        float smallRadius = SMALL_FLOWER_RADIUS * (0.7 + 0.6 * Hash21(smallCell + 5.1)) / SMALL_FLOWER_SPACING;
        float2 smallCentre = smallRadius + float2(Hash21(smallCell + 1.3), Hash21(smallCell + 2.7)) * (1.0 - 2.0 * smallRadius);
        float smallDistance = length(smallWithin - smallCentre);
        //No derivative inside the branch: the footprint is the field's own, taken before any of them
        float smallAa = footprint / SMALL_FLOWER_SPACING + 1e-4;
        float smallMask = step(1.0 - smallDensity, Hash21(smallCell + 71.3)) * smallResolvable
            * (1.0 - smoothstep(smallRadius - smallAa, smallRadius + smallAa, smallDistance));
        //A daisy's yellow eye
        smallColor = lerp(smallColor, float3(0.98, 0.74, 0.12), (smallKind < 0.5 ? 1.0 : 0.0)
            * (1.0 - smoothstep(smallRadius * 0.35 - smallAa, smallRadius * 0.35 + smallAa, smallDistance)));
        grass = lerp(grass, smallColor, smallMask);
    }

    //And the drift's colour where they are too small to draw: their mean cover (a disc of the mean radius in its cell,
    //times how many cells carry one, a little over), in the patch's own colour rather than a cell's - a cell is under
    //a pixel by then, and its colour would be a speckle. A daisy drift pales the field, a buttercup one yellows it.
    float3 driftTint = patchSpecies < 0.5 ? float3(0.95, 0.95, 0.9) : (patchSpecies < 1.5 ? float3(0.98, 0.8, 0.1) : float3(0.9, 0.75, 0.8));
    float meanRadius = SMALL_FLOWER_RADIUS / SMALL_FLOWER_SPACING;
    grass = lerp(grass, driftTint, smallDensity * 3.14159 * meanRadius * meanRadius * (1.0 - smallResolvable) * 2.5);

    grass = lerp(grass, flowerColor, flowerMask);

    //The brook's bank: the grass darkens and goes muddy towards the water
    grass = lerp(grass, BROOK_BANK, bank * (1.0 - water) * 0.75);

    //The path over everything the grass is: the verge pales and shortens, the middle is bare earth
    grass = lerp(grass, lerp(grass, PATH_VERGE, 0.55), verge * (1.0 - trodden));
    grass = lerp(grass, PATH_DIRT * dirt, trodden);

    //Matte grass: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds that
    //drift across the sky sweep their shadows over the field
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows (#471), into the same sunlight factor the clouds dim, so everything read off it
    //is shadowed at once. What casts here: the island and the gun, and since #609 everything MeadowScatter plants -
    //the old trees, the hedges, the bales, the fences - and this is the scene the first chapter plays in, so it is
    //the shadow a new player sees first.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = grass * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //THE VELVET AND THE GLOW (#281), the two things no matte surface does and grass always does. Both off the
    //terrain's base normal rather than the perturbed one — they are about how the FIELD is seen, and the blade
    //relief is far too fine to decide it — and both kept off the flowers, which are petals and not blades.
    //
    // * The sheen: a meadow seen edge-on is a sea of blade tips catching the light, so it brightens and pales
    //   towards grazing — which is what makes a far slope read as a soft carpet and not a painted hill.
    // * The translucency: a blade is thin, and with the sun behind it light comes THROUGH, yellow-green. Looking
    //   towards the sun the field glows, strongest where it is seen edge-on and gone in the cloud shadows.
    float3 toCamera = normalize(CameraPosition - worldPosition);
    float grazing = 1.0 - saturate(dot(baseNormal, toCamera));
    grazing *= grazing * grazing;
    float bladeCover = (1.0 - flowerMask) * (1.0 - trodden) * (1.0 - bank);

    float3 sheenColor = lerp(grass, GrassTipColor, 0.5) + 0.12;
    color += bladeCover * GrassSheenStrength * grazing * sheenColor
        * (skyAmbient * AmbientStrength + SunColor * (0.35 * sunlight));

    float towardSun = saturate(dot(-toCamera, SunDirection));
    float backlit = towardSun * towardSun;
    backlit *= backlit * backlit;
    color += bladeCover * GrassTranslucency * backlit * sunlight * (0.35 + 0.65 * pow(grazing, 0.33))
        * SunColor * lerp(grass, GrassTipColor, 0.6);

    //THE WATER (#609): a thin sheet over a dark bed, mostly the sky it mirrors - more of it towards grazing,
    //the Fresnel every still water has - broken by ripples running downstream and glinting where they face the sun.
    //DOWNSTREAM IS TOWARDS THE ARENA since the pond (#609's third round): the brook comes down off the hills into the
    //pond in the clearing, where until then its ripples ran away from the arena - uphill. The pond itself lies still
    //but for the wind's cat's-paws, its shallows showing the bed, and on one side it carries lily pads.
    [branch]
    if (water > 0.0)
    {
        float2 slope = 0.0;
        [branch]
        if (pondWater < 1.0)
        {
            float2 flowDir = normalize(worldPosition.xz + 1e-3);
            float2 rippleDomain = worldPosition.xz * 1.6 + flowDir * MeadowTime * 1.2;
            slope = float2(GradientNoise2(rippleDomain), GradientNoise2(rippleDomain * 1.7 + 4.2)) * (0.18 * (1.0 - pondWater));
        }
        [branch]
        if (pondWater > 0.0)
        {
            //Fine and faint: at 0.05 of slope a unit across, the first pond read as a choppy grey sheet from the bank
            float2 stillDomain = worldPosition.xz * 2.4 - WindDirection * MeadowTime * 0.5;
            slope += float2(GradientNoise2(stillDomain), GradientNoise2(stillDomain * 1.9 + 7.1)) * (0.018 * pondWater);
        }
        float3 waterNormal = normalize(float3(slope.x, 1.0, slope.y));
        float3 reflected = reflect(-toCamera, waterNormal);
        float3 sky = lerp(HorizonColor, ZenithColor, saturate(reflected.y * 2.0 + 0.45));

        //AND THE CLOUDS IN IT (#609's third round): every reference pond mirrors the white cumulus over it, and the
        //sky here is one cloud plane the sky shader draws and the ground's shadows come from (Clouds.fxh) - so the
        //reflected ray crosses that same plane, and the cloud in the water is the cloud overhead. The weather layer
        //alone, two octaves; its lit colour is the sun's over the horizon's, without the sky shader's own shading.
        //⚠ Off the FLAT reflection, not the rippled one: the plane is ~200 up, so a lens near the ground crosses it
        //thousands of units out, and the ripple's few hundredths of slope moved that point by as much again from one
        //ripple cell to the next - a coherent cloud came out as per-pixel speckle. The ripples still move the sky
        //gradient and the glint; the cloud is the still water's. Faded in over the grazing angles rather than cut.
        float flatUp = toCamera.y;
        [branch]
        if (flatUp > 0.005)
        {
            float2 cloudAt = worldPosition.xz - toCamera.xz * ((CloudPlaneY - worldPosition.y) / flatUp);
            sky = lerp(sky, SunColor * 0.5 + HorizonColor * 0.55, CloudCover(cloudAt) * smoothstep(0.005, 0.04, flatUp));
        }
        sky *= float3(0.62, 0.72, 0.82);
        float fresnel = 0.08 + 0.62 * pow(1.0 - saturate(dot(waterNormal, toCamera)), 4.0);
        float glint = pow(saturate(dot(reflected, SunDirection)), 180.0) * sunlight;
        float3 bed = lerp(POND_SHALLOW_BED, BROOK_BED, pondWater > 0.0 ? pondDepth : 1.0);
        float3 waterColor = lerp(bed * (skyAmbient * AmbientStrength + SunColor * 0.4 * sunlight), sky, fresnel)
            + SunColor * glint * 2.0;

        //The lily pads: a round leaf with its notch, one a cell where the pads grow, a few in flower
        [branch]
        if (pondWater > 0.0)
        {
            float lilies = smoothstep(0.05, 0.35, GradientNoise2(worldPosition.xz * 0.09 + 11.0)) * saturate(pondDepth * 2.0);
            float2 padCell = floor(worldPosition.xz / LILY_SPACING);
            float padRadius = 0.28 + 0.14 * Hash21(padCell + 8.1);
            float2 padCentre = (float2(Hash21(padCell + 1.7), Hash21(padCell + 2.9)) - 0.5) * (1.0 - 2.0 * padRadius);
            float2 dp = frac(worldPosition.xz / LILY_SPACING) - 0.5 - padCentre;
            float padDistance = length(dp);
            //No derivative inside the branch: the footprint is the field's own, taken before any of them
            float padAa = footprint / LILY_SPACING + 1e-4;
            float notchBearing = Hash21(padCell + 4.4) * 6.2831853;
            float2 notchDir = float2(cos(notchBearing), sin(notchBearing));
            float notch = step(0.0, dot(dp, notchDir))
                * (1.0 - smoothstep(padDistance * 0.2 - padAa, padDistance * 0.2 + padAa, abs(dp.x * notchDir.y - dp.y * notchDir.x)));
            float pad = (1.0 - smoothstep(padRadius - padAa, padRadius + padAa, padDistance)) * (1.0 - notch)
                * step(1.0 - 0.7 * lilies, Hash21(padCell + 5.3)) * pondWater;
            float bloom = (1.0 - smoothstep(padRadius * 0.35 - padAa, padRadius * 0.35 + padAa, padDistance))
                * step(0.85, Hash21(padCell + 6.6));
            float3 padColor = lerp(LILY_PAD * (0.8 + 0.4 * Hash21(padCell + 3.7)), LILY_FLOWER, bloom);
            waterColor = lerp(waterColor, padColor * (skyAmbient * AmbientStrength + SunColor * saturate(SunDirection.y) * sunlight), pad);
        }
        color = lerp(color, waterColor, water);
    }

    //Horizon haze: the distant hills soften into the skyline
    float dist = distance(CameraPosition, worldPosition);
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, HorizonColor, haze * haze);

    return float4(FarFadeToSky(color, worldPosition), 1.0);
}

//Two programs from one body (#281), the forest's pattern. "Meadow" is the authored field; "MeadowReduced" is the
//same field without the two near-field terms that cost the most — the clump seams and the blade strokes —
//and keeps everything that is arithmetic on values already computed (the tips, the dry patches, the velvet and
//the glow). SceneRenderer.SceneDetail picks; the Game's Low tier takes the reduced one.
float4 MeadowPS(MeadowVertexOutput input) : COLOR { return MeadowField(input, true); }
float4 MeadowReducedPS(MeadowVertexOutput input) : COLOR { return MeadowField(input, false); }

technique Meadow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MeadowVS();
        PixelShader = compile PS_SHADERMODEL MeadowPS();
    }
};

technique MeadowReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MeadowVS();
        PixelShader = compile PS_SHADERMODEL MeadowReducedPS();
    }
};

//--- The height probe (#590) ----------------------------------------------------------------------------

//TerrainMirror.Meadow's field, for the Testbed's mirrorcheck (see HeightProbe.fxh).
#define HEIGHT_PROBE_MIRRORED(p) TerrainHeight(p)
#include "HeightProbe.fxh"
