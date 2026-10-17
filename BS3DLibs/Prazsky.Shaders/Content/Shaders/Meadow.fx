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
static const float POND_SHELF = 3.0;                      //MeadowCarve.POND_SHELF: how far in from the shore the shallow shelf reaches
static const float3 POND_SHALLOW_BED = float3(0.075, 0.07, 0.035);
//The cut's bare earth, lit and in its darker layers, and how fast the water's body hides the bed: per world unit of the
//light's path through it, which is the depth over the view's steepness (#892)
static const float3 CUT_EARTH = float3(0.12, 0.08, 0.045);
static const float3 CUT_EARTH_DARK = float3(0.045, 0.032, 0.02);
static const float WATER_MURK = 1.8;
static const float LILY_SPACING = 1.5;                    //one pad a cell, world units
static const float3 LILY_PAD = float3(0.03, 0.085, 0.018);
static const float3 LILY_FLOWER = float3(0.92, 0.9, 0.86);
//The ground under the old tree at the path's end: trodden bare round its foot, worn round that (world units)
static const float TREE_BARE = 4.8;
static const float TREE_WORN = 9.5;

float BrookLateral(float2 p)
{
    float d = length(p);
    float wander = BrookMeander * (0.6 * sin(d * 0.017 + 2.4) + 0.4 * sin(d * 0.049 + 0.3));
    //The bearing off the brook's own centreline at this distance, taken in that line's frame rather than as atan2 of p
    //less the bearing and the wander: the same angle, but within a few hundredths of zero wherever the brook is, where the
    //compiler's atan2 agrees with MeadowPath's. Taken the old way it was up to two thousandths of a unit of lateral off,
    //which the groove's walls (#892) steepen into a hundredth of height - mirrorcheck's tolerance - 280 out.
    float centre = BrookBearing + wander / d;
    float2 along = float2(cos(centre), sin(centre));
    float angle = atan2(along.x * p.y - along.y * p.x, dot(along, p));
    float off = step(d, ClearingRadius * BROOK_START) + step(1.5707963, abs(angle));
    return d * angle + off * 1e4;
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
//towards the horizon. Sampled three times per vertex for the finite-difference normal. The FIELD, before the brook and
//the pond are cut into it (#892): what the camera grid displaces by - TerrainMirror.MeadowGround, term for term.
float GroundHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float rolling = 0.5 * sin(dot(p, float2(0.020, 0.015)))
        + 0.3 * sin(dot(p, float2(-0.013, 0.024)) + 1.5)
        + 0.2 * sin(dot(p, float2(0.031, 0.026)) + 3.0);

    float basin = ClearingRelief * sin(dot(p, float2(0.05, 0.035)));

    float natural = MeadowLevelY + basin + HillHeight * ramp * (rolling * 0.5 + 0.5);

    //The pond lies level and the knoll rises under the old tree (#609's third round) - TerrainMirror.MeadowGround, term for term
    float pondLevel = 1.0 - smoothstep(PondRadius * POND_LEVEL_FROM, PondRadius * POND_LEVEL_FROM + POND_LEVEL_FADE,
        distance(p, PondCentre));
    float2 k = p - KnollCentre;
    float knoll = saturate(1.0 - dot(k, k) / (KnollRadius * KnollRadius));
    return natural + (PondLevel - natural) * pondLevel + KnollHeight * knoll * knoll;
}

//The camera grid's normal: forward differences two units across, as its vertex shader takes it
float3 GroundNormal(float2 xz, float h)
{
    float e = 2.0;
    float hx = GroundHeight(xz + float2(e, 0.0));
    float hz = GroundHeight(xz + float2(0.0, e));
    return normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));
}

//--- THE BROOK'S GROOVE AND THE POND'S CRATER (#892) -----------------------------------------------------------------
//The owner: the brook and the pond "have no real geometric depth - the brook should be a groove cut into the ground and
//the pond a kind of irregular crater - only inside them is then the water surface". MeadowCarve.cs is the CPU copy, term
//for term, and its notes say why a cut is the lower of the field and a channel; change the one and the other has to
//follow, then run mirrorcheck on the meadow. The camera grid cannot carry the cut (5.5 units a cell against a wall a
//quarter of a unit wide): it draws GroundHeight with a hole here, and the patch MeadowPatch.cs builds draws the cut.
static const float BROOK_DROP = 1.25;                     //MeadowCarve.BROOK_DROP: the water under the field
static const float BROOK_CARVE_BED = 0.55;                //MeadowCarve.BROOK_BED: the bed under the water
static const float BROOK_TOP = 2.2;                       //MeadowCarve.BROOK_TOP
static const float BROOK_WALL_FROM = -0.25;               //MeadowCarve.BROOK_WALL_FROM, _TO
static const float BROOK_WALL_TO = 0.6;
static const float CARVE_TAIL = 3.0;                      //MeadowCarve.CARVE_TAIL
static const float BROOK_REFERENCE = 2.4;                 //MeadowCarve.BROOK_REFERENCE
static const float BROOK_CARVE_FADE = 400.0;              //MeadowCarve.BROOK_CARVE_FADE, _END
static const float BROOK_CARVE_END = 470.0;
static const float BROOK_HOLE_END = 472.0;                //MeadowCarve.BROOK_HOLE_END
static const float BROOK_CARVE_HALF = 3.2;                //MeadowCarve.BROOK_CARVE_HALF, BROOK_HOLE_HALF
static const float BROOK_HOLE_HALF = 4.6;
static const float BROOK_LIP_OUT = 0.26;                  //where the wall reaches BROOK_DROP over its water, past its edge
static const float POND_DROP = 1.25;                      //MeadowCarve.POND_DROP
static const float POND_SHELF_DEPTH = 0.35;               //MeadowCarve.POND_SHELF_DEPTH (POND_SHELF is the shelf's width)
static const float POND_DEEP = 1.0;                       //MeadowCarve.POND_DEEP, POND_DEEP_OVER
static const float POND_DEEP_OVER = 6.0;
static const float POND_STEP = 0.35;                      //MeadowCarve.POND_STEP, POND_RISE, POND_BANK, POND_LIP
static const float POND_RISE = 0.6;
static const float POND_BANK_WIDTH = 3.0;
static const float POND_LIP = 0.35;
static const float POND_CARVE_RADIUS = 19.0;              //MeadowCarve.POND_CARVE_RADIUS, POND_HOLE_RADIUS
static const float POND_HOLE_RADIUS = 21.5;
static const float CARVE_ROUND = 0.35;                    //MeadowCarve.CARVE_ROUND: the lip's rounding, world units of height
static const float CARVE_NONE = 1e4;

//The camera grid's cell (TerrainPass sets it) and where its vertices fall in the world, in cells: the grid moves by whole
//cells, so its vertices are always on one lattice and the patch can ease onto the very triangles it draws
float GridCell;
float CoarseGridPhase;

//MeadowPath.BrookPoint: the point side units across the brook's line, d from the arena
float2 BrookPointAt(float d, float side)
{
    float wander = BrookMeander * (0.6 * sin(d * 0.017 + 2.4) + 0.4 * sin(d * 0.049 + 0.3));
    float angle = BrookBearing + (wander + side) / d;
    return float2(cos(angle), sin(angle)) * d;
}

float BrookWaterHalf(float d)
{
    return BrookWidth * 0.5 * (1.0 + 0.12 * sin(d * 0.23 + 0.4) + 0.07 * sin(d * 0.61 + 2.0)
        + 0.05 * sin(d * 1.9 + 0.7) + 0.03 * sin(d * 3.7 + 2.9));
}

float BrookPresence(float d)
{
    return 1.0 - smoothstep(BROOK_CARVE_FADE, BROOK_CARVE_END, d);
}

float BrookReference(float d)
{
    float centre = GroundHeight(BrookPointAt(d, 0.0));
    return min(centre, min(GroundHeight(BrookPointAt(d, -BROOK_REFERENCE)), GroundHeight(BrookPointAt(d, BROOK_REFERENCE))));
}

float BrookChannel(float s, float water)
{
    return water - BROOK_CARVE_BED + (BROOK_CARVE_BED + BROOK_TOP) * smoothstep(BROOK_WALL_FROM, BROOK_WALL_TO, s)
        + CARVE_TAIL * max(s - BROOK_WALL_TO, 0.0);
}

//MeadowPath.PondOutline: the shore as a multiple of the mean radius
float PondOutline(float a)
{
    return 1.0 + 0.16 * sin(2.0 * a + 0.7) + 0.09 * sin(3.0 * a + 2.3) + 0.05 * sin(5.0 * a + 4.1);
}

float PondBankWidth(float a)
{
    return POND_BANK_WIDTH * (1.0 + 0.35 * sin(4.0 * a + 0.9) + 0.25 * sin(7.0 * a + 2.6));
}

float PondCrater(float e, float bank)
{
    return -POND_SHELF_DEPTH * (1.0 - smoothstep(-POND_SHELF, 0.0, e))
        - POND_DEEP * (1.0 - smoothstep(-POND_SHELF - POND_DEEP_OVER, -POND_SHELF, e))
        + POND_STEP * smoothstep(-0.1, 0.35, e)
        + (POND_DROP + POND_RISE - POND_STEP) * smoothstep(0.2, bank, e)
        + CARVE_TAIL * max(e - bank, 0.0);
}

float PondLip(float e, float bank, float a)
{
    float height = POND_LIP * (0.6 + 0.4 * sin(3.0 * a + 1.7) + 0.25 * sin(5.0 * a + 0.2));
    float crest = 0.2 + 0.6 * (bank - 0.2) + 0.4;
    return height * (1.0 - smoothstep(0.0, 1.3, abs(e - crest)));
}

//MeadowCarve.CarveMin: the lower of two heights, rounded over CARVE_ROUND (a polynomial smooth minimum), so the cut's lip
//is a curve the grass rolls over and not a crease, which the patch's triangles drew as a zigzag along the wall's top
float CarveMin(float a, float b)
{
    float h = max(CARVE_ROUND - abs(a - b), 0.0) / CARVE_ROUND;
    return min(a, b) - h * h * CARVE_ROUND * 0.25;
}

//MeadowCarve.Height: the field with the cut in it. Beside it, the field the cut is made in (the pond's lip on it), the
//water standing over this point (far below where there is none) and whether the cut here is the brook's.
float CarvedHeight(float2 p, out float field, out float water, out float brook)
{
    float height = GroundHeight(p);
    float crater = CARVE_NONE;
    water = -CARVE_NONE;
    brook = 0.0;

    float2 q = p - PondCentre;
    float r2 = dot(q, q);
    [branch]
    if (r2 < POND_CARVE_RADIUS * POND_CARVE_RADIUS)
    {
        float a = atan2(q.y, q.x);
        float e = sqrt(r2) - PondRadius * PondOutline(a);
        float bank = PondBankWidth(a);
        height += PondLip(e, bank, a);
        crater = PondLevel - POND_DROP + PondCrater(e, bank);
        water = PondLevel - POND_DROP;
    }
    field = height;

    float lateral = abs(BrookLateral(p));
    [branch]
    if (lateral < BROOK_CARVE_HALF)
    {
        float d = length(p);
        float presence = BrookPresence(d);
        float reference = BrookReference(d);
        float cut = presence * (height - CarveMin(height, BrookChannel(lateral - BrookWaterHalf(d), reference - BROOK_DROP)));
        height -= cut;
        if (cut > 0.0 && height < crater)
        {
            water = reference - BROOK_DROP * presence;
            brook = 1.0;
        }
    }

    return CarveMin(height, crater);
}

//The meadow's ground: TerrainMirror.Meadow, term for term, and what mirrorcheck holds it to
float TerrainHeight(float2 p)
{
    float field, water, brook;
    return CarvedHeight(p, field, water, brook);
}

//What the camera grid draws at p: its own triangle's plane through three of its vertices (TerrainMirror.OnGrid's split)
float CoarseGridHeight(float2 p)
{
    float2 g = p / GridCell - CoarseGridPhase;
    float2 cell = floor(g);
    float2 f = g - cell;
    float2 corner = (cell + CoarseGridPhase) * GridCell;
    float hb = GroundHeight(corner + float2(GridCell, 0.0));
    float hc = GroundHeight(corner + float2(0.0, GridCell));
    if (f.x + f.y <= 1.0)
    {
        float ha = GroundHeight(corner);
        return ha + f.x * (hb - ha) + f.y * (hc - ha);
    }
    float hd = GroundHeight(corner + GridCell);
    return hd + (1.0 - f.x) * (hc - hd) + (1.0 - f.y) * (hb - hd);
}

//How much of the patch is the cut and how much the camera grid: 1 wherever the cut can reach, easing to 0 at the edge of
//the grid's hole, past which the patch lies on the grid's own triangles
float PatchWeight(float2 p)
{
    float pond = 1.0 - smoothstep(POND_CARVE_RADIUS, POND_HOLE_RADIUS, distance(p, PondCentre));
    float brook = (1.0 - smoothstep(BROOK_CARVE_HALF, BROOK_HOLE_HALF, abs(BrookLateral(p))))
        * (1.0 - smoothstep(BROOK_CARVE_END, BROOK_HOLE_END, length(p)));
    return max(pond, brook);
}

//How much sun reaches a point down in the brook's groove at height y: a low sun across the groove is stood in the way of
//by the lip on its own side, so the far wall is lit, and the near wall and the water under it are in the lip's shade. The
//cross section only: along a straight groove the sun's run along it changes nothing.
float GrooveSun(float2 p, float y)
{
    float d = length(p);
    float2 across = float2(-p.y, p.x) / d;                //the way BrookLateral grows here
    float sunAcross = dot(SunDirection.xz, across);
    float towards = sunAcross >= 0.0 ? 1.0 : -1.0;
    float run = towards * (BrookWaterHalf(d) + BROOK_LIP_OUT) - BrookLateral(p);
    float lip = GroundHeight(BrookPointAt(d, towards * BROOK_REFERENCE));
    float rise = run * SunDirection.y / max(abs(sunAcross), 1e-3);
    return lerp(1.0, smoothstep(-0.12, 0.12, y + rise - lip), BrookPresence(d) * step(0.0, run));
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

//The patch's (#892), with the cut: x how far under the field, y how far under the water (negative over it), z how much of
//the sun the groove lets in, w 1 where the cut is the brook's. The camera grid hands down nothing cut, and its programs
//take NO_CUT as a literal, so none of the cut's arithmetic or registers is in them: the field's program is a full-screen
//pass bound by how many waves fit, and a few more live values in it cost two tenths of a millisecond at 4K.
struct CarvedVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 Carve : TEXCOORD2;
};

static const float4 NO_CUT = float4(0.0, -1e4, 1.0, 0.0);

//The camera grid: the field, uncut - it has a hole where the patch draws the cut (MeadowField)
MeadowVertexOutput MeadowVS(MeadowVertexInput input)
{
    MeadowVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float h = GroundHeight(xz);
    output.WorldNormal = GroundNormal(xz, h);

    float3 worldPosition = float3(xz.x, h, xz.y);
    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//The patch's vertex (MeadowPatch.cs, GroundVertex): where it stands in the brook's coordinates - distance out along the
//brook in Rows, lateral off its centreline in Sides - at its own spacing (x) and at two, four and eight times it (y, z, w)
struct PatchVertexInput
{
    float4 Rows : POSITION0;
    float4 Sides : TEXCOORD0;
};

//MeadowPatchGeometry.LOD_ROWS_FROM, LOD_COLUMNS_FROM: where the patch's rows, and its columns, start to coarsen
static const float LOD_ROWS_FROM = 14.0;
static const float LOD_COLUMNS_FROM = 22.0;

//How far under the camera grid the patch lies where both are drawn, near and per unit of distance (MeadowCarvedVS)
static const float PATCH_SINK = 0.02;
static const float PATCH_SINK_PER_UNIT = 0.0003;

//A patch vertex's place at continuous level `level`: one level slid into the next, so a vertex reaches its coarser place
//smoothly and lands on another vertex, whose triangles with it close to nothing
float PatchLevel(float4 places, float level)
{
    float at = lerp(places.x, places.y, saturate(level));
    at = lerp(at, places.z, saturate(level - 1.0));
    return lerp(at, places.w, saturate(level - 2.0));
}

//The patch (#892): the brook's groove and the pond's crater, on a mesh fixed to the world, fine enough across the groove to
//hold its walls near the lens and coarser with distance (MeadowPatchGeometry's notes): how far it has coarsened is read off
//the vertex's own finest place, so a vertex shared by two triangles lands in the same place for both and the mesh never opens
CarvedVertexOutput MeadowCarvedVS(PatchVertexInput input)
{
    CarvedVertexOutput output;

    float2 finest = BrookPointAt(input.Rows.x, input.Sides.x);
    float range = distance(CameraPosition, float3(finest.x, GroundHeight(finest), finest.y));
    float2 xz = BrookPointAt(PatchLevel(input.Rows, clamp(log2(range / LOD_ROWS_FROM), 0.0, 3.0)),
        PatchLevel(input.Sides, clamp(log2(range / LOD_COLUMNS_FROM), 0.0, 3.0)));
    float field, water, brook;
    float h = CarvedHeight(xz, field, water, brook);
    float weight = PatchWeight(xz);

    //The cut's own normal, by central differences a tenth of a unit across: the camera grid's two units would round a
    //groove a quarter of a unit wide into a ditch
    float3 normal = float3(0.0, 1.0, 0.0);
    [branch]
    if (weight > 0.0)
    {
        float e = 0.1;
        float hx = TerrainHeight(xz + float2(e, 0.0)) - TerrainHeight(xz - float2(e, 0.0));
        float hz = TerrainHeight(xz + float2(0.0, e)) - TerrainHeight(xz - float2(0.0, e));
        normal = normalize(float3(-hx / (2.0 * e), 1.0, -hz / (2.0 * e)));
    }

    //Towards the grid's hole the patch becomes the grid: its triangles' height and its normal's formula, so where the two
    //overlap they are two copies of one surface and not two surfaces fighting for the depth. And past the hole's edge, where
    //the grid is drawn too, the patch lies a hair under it (PATCH_SINK, more with distance for the depth buffer's coarser
    //steps): the grid is drawn first, so the early depth test throws the patch's pixels away there instead of shading the
    //same field a second time over the band the patch overlaps the grid by - up to a block wide (MeadowPatchGeometry).
    [branch]
    if (weight < 1.0)
    {
        float ground = GroundHeight(xz);
        h = lerp(CoarseGridHeight(xz), h, weight) - (1.0 - weight) * (PATCH_SINK + PATCH_SINK_PER_UNIT * range);
        normal = normalize(lerp(GroundNormal(xz, ground), normal, weight));
    }

    float3 worldPosition = float3(xz.x, h, xz.y);
    output.WorldPosition = worldPosition;
    output.WorldNormal = normal;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);
    output.Carve = float4(max(field - h, 0.0) * weight, water - h, brook > 0.5 ? GrooveSun(xz, h) : 1.0, brook);

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

//THE WATER'S SURFACE (#609): mostly the sky it mirrors - more of it towards grazing, the Fresnel every still water has -
//broken by ripples and glinting where they face the sun. DOWNSTREAM IS TOWARDS THE ARENA since the pond (#609's third
//round): the brook comes down off the hills into the pond in the clearing, where until then its ripples ran away from the
//arena - uphill. The pond (still = 1) lies still but for the wind's cat's-paws. Answers the reflection with its glint,
//and the Fresnel; what shows through the rest is the caller's - the painted brook's dark bed, or the cut's own (#892).
float3 WaterReflection(float3 worldPosition, float3 toCamera, float still, float sunlight, out float fresnel)
{
    float2 slope = 0.0;
    [branch]
    if (still < 1.0)
    {
        float2 flowDir = normalize(worldPosition.xz + 1e-3);
        float2 rippleDomain = worldPosition.xz * 1.6 + flowDir * MeadowTime * 1.2;
        slope = float2(GradientNoise2(rippleDomain), GradientNoise2(rippleDomain * 1.7 + 4.2)) * (0.18 * (1.0 - still));
    }
    [branch]
    if (still > 0.0)
    {
        //Fine and faint: at 0.05 of slope a unit across, the first pond read as a choppy grey sheet from the bank
        float2 stillDomain = worldPosition.xz * 2.4 - WindDirection * MeadowTime * 0.5;
        slope += float2(GradientNoise2(stillDomain), GradientNoise2(stillDomain * 1.9 + 7.1)) * (0.018 * still);
    }
    float3 waterNormal = normalize(float3(slope.x, 1.0, slope.y));
    float3 reflected = reflect(-toCamera, waterNormal);
    float3 sky = lerp(HorizonColor, ZenithColor, saturate(reflected.y * 2.0 + 0.45));

    //AND THE CLOUDS IN IT (#609's third round): every reference pond mirrors the white cumulus over it, and the
    //sky here is one cloud plane the sky shader draws and the ground's shadows come from (Clouds.fxh) - so the
    //reflected ray crosses that same plane, and the cloud in the water is the cloud overhead. The weather layer
    //alone, two octaves; its lit colour is the sun's over the horizon's, without the sky shader's own shading.
    //Off the FLAT reflection, not the rippled one: the plane is ~200 up, so a lens near the ground crosses it
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
    fresnel = 0.08 + 0.62 * pow(1.0 - saturate(dot(waterNormal, toCamera)), 4.0);
    float glint = pow(saturate(dot(reflected, SunDirection)), 180.0) * sunlight;
    return sky * fresnel + SunColor * glint * 2.0;
}

//The lily pads on the pond: a round leaf with its notch, one a cell where the pads grow (only off the shallow shelf, by
//`deep`), a few in flower. Answers the pad's cover and its colour. No derivative: the footprint is the caller's.
float LilyPad(float2 xz, float deep, float footprint, out float3 padColor)
{
    float lilies = smoothstep(0.05, 0.35, GradientNoise2(xz * 0.09 + 11.0)) * saturate(deep * 2.0);
    float2 padCell = floor(xz / LILY_SPACING);
    uint padSeed = HashSalt(HashCell(padCell), 11u);
    uint padSeed2 = HashSalt(padSeed, 1u);
    float padRadius = 0.28 + 0.14 * HashBits(padSeed, 0u, 8u);
    float2 padCentre = (float2(HashBits(padSeed, 8u, 8u), HashBits(padSeed, 16u, 8u)) - 0.5) * (1.0 - 2.0 * padRadius);
    float2 dp = frac(xz / LILY_SPACING) - 0.5 - padCentre;
    float padDistance = length(dp);
    float padAa = footprint / LILY_SPACING + 1e-4;
    float notchBearing = HashBits(padSeed, 24u, 8u) * 6.2831853;
    float2 notchDir = float2(cos(notchBearing), sin(notchBearing));
    float notch = step(0.0, dot(dp, notchDir))
        * (1.0 - smoothstep(padDistance * 0.2 - padAa, padDistance * 0.2 + padAa, abs(dp.x * notchDir.y - dp.y * notchDir.x)));
    float pad = (1.0 - smoothstep(padRadius - padAa, padRadius + padAa, padDistance)) * (1.0 - notch)
        * step(1.0 - 0.7 * lilies, HashBits(padSeed2, 0u, 12u));
    float bloom = (1.0 - smoothstep(padRadius * 0.35 - padAa, padRadius * 0.35 + padAa, padDistance))
        * step(0.85, HashBits(padSeed2, 12u, 8u));
    padColor = lerp(LILY_PAD * (0.8 + 0.4 * HashBits(padSeed2, 20u, 8u)), LILY_FLOWER, bloom);
    return pad;
}

float4 MeadowField(float3 worldPosition, float3 worldNormal, float4 carve, bool detail, bool grid)
{
    //Cut the island's footprint out of the terrain (see IslandHoleRadius; 0 in the map editor keeps it all), and the far
    //ring where the grid covers. And the camera grid and the far ring give way to the patch where the brook and the pond
    //are cut (#892): the patch reaches past this hole on every side, so nothing between them is left open. The patch
    //itself has NO clip: it never reaches the island or past the grid's far edge, and a program that can discard loses
    //the early depth test - every pixel of the patch hidden by a hill or by its own nearer bank was shaded in full.
    float brookLateral = abs(BrookLateral(worldPosition.xz));
    if (grid)
    {
        clip(length(worldPosition.xz) - IslandHoleRadius);
        FarRingClip(worldPosition.xz);
        float2 pondHole = worldPosition.xz - PondCentre;
        clip(dot(pondHole, pondHole) < POND_HOLE_RADIUS * POND_HOLE_RADIUS
            || (brookLateral < BROOK_HOLE_HALF && length(worldPosition.xz) < BROOK_HOLE_END) ? -1.0 : 1.0);
    }

    //What the patch's vertices said about the cut: how far into it this is, how far under the water, and the groove's sun
    float cut = saturate((carve.x - 0.04) / 0.2);
    float under = saturate(carve.y / 0.06);

    float3 baseNormal = normalize(worldNormal);
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
    //Every roll of a flower comes off two words of its cell's integer seed (#674): Hash21, which this rolled on, repeats
    //exactly every 50 x 100 cells on whole-number cells, so the flowers' layout came back in the same arrangement.
    uint flowerSeed = HashCell(cell);
    uint flowerSeed2 = HashSalt(flowerSeed, 1u);
    float present = step(1.0 - density, HashBits(flowerSeed, 0u, 12u));

    float patchSpecies = floor(HashUnit(HashSalt(HashCell(floor(worldPosition.xz / FLOWER_PATCH)), 41u)) * FLOWER_SPECIES);
    float ownSpecies = floor(HashBits(flowerSeed, 12u, 8u) * FLOWER_SPECIES);
    float species = HashBits(flowerSeed, 20u, 6u) < 0.4 ? ownSpecies : patchSpecies;

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
    //for the path's reason. PAINTED only past its groove (#892): to BROOK_CARVE_FADE it is cut into the field and its water
    //is a surface of its own (MeadowWaterPS), and over the groove's last stretch the paint comes back as the cut shallows.
    //The pond is cut along its whole shore and has no paint at all.
    float water = 0.0, bank = 0.0;

    [branch]
    if (brookLateral < max(BrookWidth * 1.4, BrookWidth * 0.5 + 0.2) + BROOK_RAGGED)
    {
        float painted = 1.0 - BrookPresence(length(worldPosition.xz));
        float brookRagged = BROOK_RAGGED * GradientNoise2(worldPosition.xz * 0.7 + 9.1);
        water = (1.0 - smoothstep(BrookWidth * 0.5 - 0.2, BrookWidth * 0.5 + 0.2, brookLateral + brookRagged)) * painted;
        bank = (1.0 - smoothstep(BrookWidth * 0.5, BrookWidth * 1.4, brookLateral + brookRagged)) * painted;
    }

    present *= (1.0 - bank) * (1.0 - cut);

    //Per-flower character: the species' petal count, petal shape and size, then the cell hash's own variation.
    //0 oxeye daisy: many thin white rays round a big yellow eye. 1 buttercup: five round glossy yellow petals.
    //2 poppy: four broad red petals round a black eye, the biggest. 3 cornflower: ragged blue rays. 4 clover: a
    //round pink-purple head of florets and no petals at all.
    float petalCount = species < 0.5 ? 13.0 : (species < 1.5 ? 5.0 : (species < 2.5 ? 4.0 : (species < 3.5 ? 9.0 : 1.0)));
    float petalShape = species < 0.5 ? 2.2 : (species < 1.5 ? 0.6 : (species < 2.5 ? 0.35 : (species < 3.5 ? 3.0 : 0.0)));
    float speciesSize = species < 0.5 ? 1.0 : (species < 1.5 ? 0.75 : (species < 2.5 ? 1.35 : (species < 3.5 ? 0.85 : 0.6)));
    float eyeShare = species < 0.5 ? 0.38 : (species < 1.5 ? 0.22 : (species < 2.5 ? 0.26 : (species < 3.5 ? 0.24 : 0.0)));
    float rotation = HashBits(flowerSeed, 26u, 6u) * 6.2831853;

    //MOSTLY SMALL, A FEW BIG (#609's third round). The owner: the flowers are too big for what stands round them;
    //the big ones can stay, but most should be smaller, or small. The roll is cubed, so half the flowers are under a
    //quarter of the old middle size's worth above FLOWER_SMALLEST and one in ten reaches past three quarters of the
    //range; the largest are the largest they ever were (0.7 to 1.3 of the species' size, evenly, until then).
    float sizeRoll = HashBits(flowerSeed2, 0u, 10u);
    float size = min(FlowerSize * speciesSize
        * (FLOWER_SMALLEST + (FLOWER_LARGEST - FLOWER_SMALLEST) * sizeRoll * sizeRoll * sizeRoll), 0.45);

    //The centre may only wander in [size, 1-size], so the whole flower stays inside its cell and no petal
    //is cut off by the cell edge (the flower is only evaluated within its own cell's fraction).
    float2 flowerCentre = size + float2(HashBits(flowerSeed2, 10u, 8u), HashBits(flowerSeed2, 18u, 8u)) * (1.0 - 2.0 * size);
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

    //The cut's faces (#892): steep enough and the grass gives way to bare earth, and under the water to silt; both keep a
    //little of the relief, which reads as lumps of earth and not as blades
    float earth = cut * (1.0 - smoothstep(0.5, 0.78, baseNormal.y));
    float bare = max(earth, under);

    float relief = GrassRelief(worldPosition.xz, footprint, gust) * (1.0 - flowerCover) * (1.0 - 0.8 * verge) * (1.0 - 0.6 * bare);
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
    float shade = 0.9 + 0.2 * HashBits(flowerSeed2, 26u, 6u);
    float3 petalColor = species < 0.5 ? float3(0.96, 0.96, 0.92)
        : (species < 1.5 ? float3(0.98, 0.82, 0.10)
        : (species < 2.5 ? float3(0.86, 0.09, 0.05)
        : (species < 3.5 ? float3(0.22, 0.34, 0.92)
        : float3(0.78, 0.36, 0.62) * (0.8 + 0.4 * HashUnit(HashSalt(flowerSeed, 2u + (uint)dot(floor(within * 40.0), float2(1.0, 40.0))))))));
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
        * (1.0 - verge) * (1.0 - bank) * (1.0 - cut);
    float smallResolvable = saturate(1.5 - footprint / (SMALL_FLOWER_RADIUS * 2.5));

    [branch]
    if (smallResolvable > 0.0)
    {
        float2 smallCell = floor(worldPosition.xz / SMALL_FLOWER_SPACING);
        float2 smallWithin = frac(worldPosition.xz / SMALL_FLOWER_SPACING);
        //Two words of the cell's integer seed, salted apart from the big flowers' cells it shares coordinates with.
        //These repeated every 40 units on Hash21 (50 cells of 0.8), the plainest case of #674 in the game.
        uint smallSeed = HashSalt(HashCell(smallCell), 7u);
        uint smallSeed2 = HashSalt(smallSeed, 1u);
        //Their kind: the patch's where it is daisies or buttercups, three in four; otherwise daisy, buttercup or clover
        float smallKind = HashBits(smallSeed, 12u, 6u) < 0.75 && patchSpecies < 1.5 ? patchSpecies : floor(HashBits(smallSeed, 18u, 8u) * 2.99);
        float3 smallColor = smallKind < 0.5 ? float3(0.95, 0.95, 0.9) : (smallKind < 1.5 ? float3(0.98, 0.8, 0.1) : float3(0.78, 0.36, 0.62));
        float smallRadius = SMALL_FLOWER_RADIUS * (0.7 + 0.6 * HashBits(smallSeed, 26u, 6u)) / SMALL_FLOWER_SPACING;
        float2 smallCentre = smallRadius + float2(HashBits(smallSeed2, 0u, 8u), HashBits(smallSeed2, 8u, 8u)) * (1.0 - 2.0 * smallRadius);
        float smallDistance = length(smallWithin - smallCentre);
        //No derivative inside the branch: the footprint is the field's own, taken before any of them
        float smallAa = footprint / SMALL_FLOWER_SPACING + 1e-4;
        float smallMask = step(1.0 - smallDensity, HashBits(smallSeed, 0u, 12u)) * smallResolvable
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

    //THE CUT (#892), as the references draw it: the brook's walls and the pond's step at the waterline bare earth in
    //layers, the bed under the water silt, the earth dark and wet just over the waterline. Behind a branch, since the
    //camera grid is never cut; nothing in it takes a derivative.
    [branch]
    if (cut > 0.0)
    {
        float layers = sin(worldPosition.y * 11.0 + 2.0 * GradientNoise2(worldPosition.xz * 0.5)) * 0.5 + 0.5;
        float clods = GradientNoise2(worldPosition.xz * 3.1 + worldPosition.y * 2.3);
        float3 earthColor = lerp(CUT_EARTH_DARK, CUT_EARTH, saturate(0.65 * layers + 0.35 + 0.3 * clods));
        float3 siltColor = POND_SHALLOW_BED * (0.85 + 0.3 * clods);
        grass = lerp(grass, earthColor, earth);
        grass = lerp(grass, siltColor, under);
        grass *= 1.0 - 0.45 * cut * (1.0 - smoothstep(0.0, 0.35, -carve.y));
    }

    //The path over everything the grass is: the verge pales and shortens, the middle is bare earth
    grass = lerp(grass, lerp(grass, PATH_VERGE, 0.55), verge * (1.0 - trodden));
    grass = lerp(grass, PATH_DIRT * dirt, trodden);

    //Matte grass: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds that
    //drift across the sky sweep their shadows over the field
    float sunlight = CloudSunlight(worldPosition, SunDirection) * carve.z;

    //The sun's cast shadows (#471), into the same sunlight factor the clouds dim, so everything read off it
    //is shadowed at once. What casts here: the island and the gun, and since #609 everything MeadowScatter plants -
    //the old trees, the hedges, the bales, the fences - and this is the scene the first chapter plays in, so it is
    //the shadow a new player sees first.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    //Down in the cut less of the sky reaches (#892): the narrow groove more than the open bowl
    float occlusion = 1.0 - saturate(carve.x / 1.6) * lerp(0.25, 0.55, carve.w);
    float3 color = grass * (skyAmbient * AmbientStrength * occlusion + SunColor * ndotl * sunlight);

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
    float bladeCover = (1.0 - flowerMask) * (1.0 - trodden) * (1.0 - bank) * (1.0 - bare);

    float3 sheenColor = lerp(grass, GrassTipColor, 0.5) + 0.12;
    color += bladeCover * GrassSheenStrength * grazing * sheenColor
        * (skyAmbient * AmbientStrength + SunColor * (0.35 * sunlight));

    float towardSun = saturate(dot(-toCamera, SunDirection));
    float backlit = towardSun * towardSun;
    backlit *= backlit * backlit;
    color += bladeCover * GrassTranslucency * backlit * sunlight * (0.35 + 0.65 * pow(grazing, 0.33))
        * SunColor * lerp(grass, GrassTipColor, 0.6);

    //The painted brook past its groove (#609; only there since #892): the water's surface over a dark bed
    [branch]
    if (water > 0.0)
    {
        float fresnel;
        float3 reflection = WaterReflection(worldPosition, toCamera, 0.0, sunlight, fresnel);
        float3 waterColor = BROOK_BED * (skyAmbient * AmbientStrength + SunColor * 0.4 * sunlight) * (1.0 - fresnel) + reflection;
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
//the glow). SceneRenderer.SceneDetail picks; the Game's Low tier takes the reduced one. Each comes twice since #892:
//over the camera grid (and the far ring, which takes "MeadowReduced"), with the hole the cut is drawn in, and over the
//patch that draws the cut.
float4 MeadowPS(MeadowVertexOutput input) : COLOR { return MeadowField(input.WorldPosition, input.WorldNormal, NO_CUT, true, true); }
float4 MeadowReducedPS(MeadowVertexOutput input) : COLOR { return MeadowField(input.WorldPosition, input.WorldNormal, NO_CUT, false, true); }
float4 MeadowCarvedPS(CarvedVertexOutput input) : COLOR { return MeadowField(input.WorldPosition, input.WorldNormal, input.Carve, true, false); }
float4 MeadowCarvedReducedPS(CarvedVertexOutput input) : COLOR { return MeadowField(input.WorldPosition, input.WorldNormal, input.Carve, false, false); }

//The bed under the water (#892): the patch's triangles wholly under it (MeadowPatchGeometry.BED_UNDER), which the field's
//program shades as silt only after working out the flowers, the clumps and the blades and throwing them away - so this is
//that silt and its light alone, the same figures: wet-darkened, occluded by depth, in the groove's sun, hazed and faded.
//The water over it shows how much of it.
float4 MeadowBedPS(CarvedVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;
    float3 normal = normalize(input.WorldNormal);

    float sunlight = CloudSunlight(worldPosition, SunDirection) * input.Carve.z;
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, normal, SunDirection);

    float clods = GradientNoise2(worldPosition.xz * 3.1 + worldPosition.y * 2.3);
    float3 silt = POND_SHALLOW_BED * (0.85 + 0.3 * clods) * 0.55;
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));
    float occlusion = 1.0 - saturate(input.Carve.x / 1.6) * lerp(0.25, 0.55, input.Carve.w);
    float3 color = silt * (skyAmbient * AmbientStrength * occlusion + SunColor * saturate(dot(normal, SunDirection)) * sunlight);

    float haze = saturate(distance(CameraPosition, worldPosition) / HorizonHazeDistance);
    color = lerp(color, HorizonColor, haze * haze);
    return float4(FarFadeToSky(color, worldPosition), 1.0);
}

//--- The water in the cut (#892) ---------------------------------------------------------------------------------
//A surface of its own at the water's level, inside the groove and the crater, drawn after everything opaque that stands
//on the ground (the reeds' feet in it) with the translucent things (Backdrop.DrawGrounded): blended over the cut's bed
//and reading the depth without writing it, so the banks hide its edge and the bed shows through the shallows.

struct WaterVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    //x: 1 the pond, 0 the brook; y: how much of the groove there is (the brook's water fades out with it); z: the
    //groove's sun
    float3 Water : TEXCOORD1;
};

//The water mesh (MeadowPatch.cs) carries world XZ and in Y which water a vertex is: the level is set here, the pond's
//one plane, the brook's falling with its bed
WaterVertexOutput MeadowWaterVS(MeadowVertexInput input)
{
    WaterVertexOutput output;

    float2 xz = input.Position.xz;
    float pond = input.Position.y;
    float level = PondLevel - POND_DROP, presence = 1.0, sun = 1.0;
    [branch]
    if (pond < 0.5)
    {
        float d = length(xz);
        presence = BrookPresence(d);
        level = BrookReference(d) - BROOK_DROP * presence;
        sun = GrooveSun(xz, level);
    }

    float3 worldPosition = float3(xz.x, level, xz.y);
    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);
    output.Water = float3(pond, presence, sun);

    return output;
}

float4 MeadowWaterPS(WaterVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;
    float pond = input.Water.x;
    float3 toCamera = normalize(CameraPosition - worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The shore: the brook's water gives way to the pond's there, so the two never lie one over the other at the mouth
    float2 q = worldPosition.xz - PondCentre;
    float a = atan2(q.y, q.x);
    float e = length(q) - PondRadius * PondOutline(a);
    clip(pond > 0.5 ? -e : e);

    //How deep it is, off the cut's own profile: the bed is drawn under it, and this is how much of the bed shows
    float depth = pond > 0.5 ? -PondCrater(e, PondBankWidth(a))
        : -BrookChannel(abs(BrookLateral(worldPosition.xz)) - BrookWaterHalf(length(worldPosition.xz)), 0.0);
    depth = max(depth, 0.0);

    float sunlight = CloudSunlight(worldPosition, SunDirection) * input.Water.z;
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, float3(0.0, 1.0, 0.0), SunDirection);
    float3 light = ZenithColor * AmbientStrength + SunColor * 0.4 * sunlight;

    float fresnel;
    float3 reflection = WaterReflection(worldPosition, toCamera, pond, sunlight, fresnel);

    //Premultiplied, as BlendState.AlphaBlend takes it: the reflection and the water's own dark body are the colour, and
    //alpha is how much of the bed they cover - the more, the deeper the water and the flatter it is seen
    float through = exp(-depth * WATER_MURK / max(toCamera.y, 0.12));
    float cover = 1.0 - (1.0 - fresnel) * through;
    float3 color = reflection + (1.0 - fresnel) * (1.0 - through) * BROOK_BED * light;

    [branch]
    if (pond > 0.5)
    {
        float3 padColor;
        float pad = LilyPad(worldPosition.xz, saturate(-e / POND_SHELF), footprint, padColor);
        color = lerp(color, padColor * (ZenithColor * AmbientStrength + SunColor * saturate(SunDirection.y) * sunlight), pad);
        cover = lerp(cover, 1.0, pad);
    }

    //The haze and the far fade on the colour as seen, not on its premultiplied share; then the brook's last stretch fades
    //out with its groove, into the brook painted on the field
    float3 seen = color / max(cover, 1e-4);
    float haze = saturate(distance(CameraPosition, worldPosition) / HorizonHazeDistance);
    seen = FarFadeToSky(lerp(seen, HorizonColor, haze * haze), worldPosition);
    float alpha = cover * input.Water.y;
    return float4(seen * alpha, alpha);
}

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

technique MeadowCarved
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MeadowCarvedVS();
        PixelShader = compile PS_SHADERMODEL MeadowCarvedPS();
    }
};

technique MeadowCarvedReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MeadowCarvedVS();
        PixelShader = compile PS_SHADERMODEL MeadowCarvedReducedPS();
    }
};


technique MeadowBed
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MeadowCarvedVS();
        PixelShader = compile PS_SHADERMODEL MeadowBedPS();
    }
};

technique MeadowWater
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MeadowWaterVS();
        PixelShader = compile PS_SHADERMODEL MeadowWaterPS();
    }
};

//--- The height probe (#590) ----------------------------------------------------------------------------

//TerrainMirror.Meadow's field, for the Testbed's mirrorcheck (see HeightProbe.fxh).
#define HEIGHT_PROBE_MIRRORED(p) TerrainHeight(p)
#include "HeightProbe.fxh"
