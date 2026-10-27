//The flank of an erupting volcano (#223): black basalt and heaped scoria raked by gullies, rivers of
//red-orange lava running down them and out past the arena, thin incandescent rivulets threading down the
//cone from its crater, and a lava lake in the crater itself. The cone stands off to one side with its
//crater against the sky; the lava fountains and the smoke plume over it are LavaFountain.fx and the drifting
//ash is Ash.fx, both drawn after this. Past the field's raised edge lies a landscape (#748): a scorched band, pasture
//and woods, a river and a village's fields, ranges and a dormant cone - "The country" below, its height and its
//surface, with the snags, woods and houses on it planted by VolcanoCountryPlanting.
//
//The machinery is the desert's and the mountain's: a camera-centred grid (360 a side, 32-bit indices - see
//CreateGridMesh) snapped to its cell so it does not swim, displaced in the vertex shader, its base normal by
//finite differences per vertex and a fine relief perturbing it per pixel. What is new here is that the GROUND
//IS THE LIGHT. Every other terrain shader in this project takes its whole radiance from the sun and the dome;
//this one adds an emissive band of its own - narrow, and that is deliberate. Red and orange balls hang over
//this scene, so the rivers are kept thin and hot rather than wide and warm, and what the flows throw back on
//the cluster is a capped point light (SceneLights, VolcanoLightStrength), not a tint over the frame.
//
//DRAWN FROM REFERENCES (#509), and every one of them said the same three things the first build had the other
//way round. (1) A lava flow is mostly DARK: a skin of chilled crust carried on the melt, the incandescence
//showing as a core down the middle of the channel and as thin cracks and streamlines - and the lines run
//ALONG the flow, because the melt shears downstream. The first build lit a honeycomb of cells over the whole
//width, which read as a giraffe's hide laid on the cone. (2) The flank of an erupting cone is black rock with
//a few thin glowing threads running down it from the crater, not a glowing web over the whole summit. (3)
//Cooled lava is GLASSY: black pahoehoe reflects the sky as a slate sheen, which is the only thing that tells
//a lava field from a heap of soot at night. The drawings are local renders (the design-references skill,
//C:\Users\panrd\AI\sd\out\509) and nothing of them is in this repository; docs/scenes.md "The volcano" has
//what each one settled.
//
//Shader Model 5.0, drawn in all three executables out of the one copy in Prazsky.Shaders.

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
float VolcanoTime;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground. The Testbed sets this to the
//island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

//--- The massing -------------------------------------------------------------------------------------------

float VolcanoLevelY;
float ClearingRadius;
float ClearingTransition;

float2 ConeCenterXZ;
float ConeRadius;
float ConeHeight;
float ConeProfile;
float CraterRadius;
float CraterDepth;

//GullyCount arrives already ROUNDED TO AN INTEGER from the host, and it has to be: every bearing term below
//is a multiple of it, and only an integer multiple of atan2's angle closes seamlessly across the ±π seam. A
//fractional count would leave a straight scar running from the crater to the horizon along -X.
float GullyDepth;
float GullyCount;
float ScoriaRelief;

//--- The rivers --------------------------------------------------------------------------------------------

//Matched by MAX_RIVERS in SceneRenderer. Rivers are radial from the cone's axis: a bearing each, a reach
//each (how far down the flank the flow gets before its front stops), computed once by the host so the CPU
//side can put a moving point light on the same river the shader draws.
#define MAX_RIVERS 6
float RiverBearing[MAX_RIVERS];
float RiverReach[MAX_RIVERS];
int RiverCount;

float RiverWidth;
float RiverWander;
float RiverSpeed;
float HaloWidth;

float3 RockColor;
float3 RockColorLight;
float3 ScoriaColor;
float3 LavaHot;
float3 LavaCool;
float3 CrustColor;
float CrustGlow;
float CrackGlow;
float PlateSize;
float SheenStrength;
float RivuletStrength;
float FieldCrackStrength;

float AmbientStrength;
float HorizonHazeDistance;
float3 HazeTint;
float HazeStrength;
float2 WindDirection;

static const float TWO_PI = 6.2831853;

//--- The form's own figures ---------------------------------------------------------------------------------
//Constants and not config dials, the meadow's rosette rule: they say what SHAPE a thing is, and a designer
//turning one of them is redesigning the feature rather than tuning its look.

//A flow's open core, as a fraction of its half-width: nearly the whole channel just below the vent, pinched
//to a thread by the time the front is reached - the melt crusts over from the banks inwards as it runs.
static const float CORE_AT_VENT = 0.85;
static const float CORE_AT_FRONT = 0.22;

//The streamlines: stripes lying along the flow, STREAM_FREQUENCY radians of phase per world unit across it,
//their phase bent by a noise STREAM_STRETCH times longer along the flow than across it.
static const float STREAM_FREQUENCY = 2.2;
static const float STREAM_STRETCH = 12.0;

//The crust rafts a flow carries over its fixed bed of fissures (#554): the share of the channel that is crust
//(the rest is open leads), how much of a fissure still shows through a raft, and how brightly an open lead
//glows as a fraction of LavaCool.
static const float RAFT_COVER = 0.55;
static const float RAFT_SHOW = 0.2;
static const float LEAD_GLOW = 0.15;

//The rivulets on the cone. Sampled on a circle of this radius in noise space (seam-free round the axis - see
//Rivulets), so it is also roughly how many lines run down the whole cone divided by five; RIVULET_RADIAL is
//how fast a line wanders as it descends, per world unit.
static const float RIVULET_ANGULAR = 5.0;
static const float RIVULET_RADIAL = 0.011;
static const float RIVULET_WIDTH = 0.03;

//The glowing cracks in the lava field: world frequency of the line field, and a crack's half-width in world
//units - under a pixel from the play camera, which ContourLine turns into a dimmer line rather than a gap.
static const float FIELD_CRACK_SCALE = 0.045;
static const float FIELD_CRACK_HALF_WIDTH = 0.22;

//--- The country past the field (#748) ---------------------------------------------------------------------
//The owner, three times: the far side was "just a sort of lunar landscape", "there are no trees, mountains, rivers,
//human dwellings", "it now looks more like some alien planet". The references (C:\Users\panrd\AI\sd\out\748r2-zimage
//and 748r2-klein) all paint one gradient: the black field ends at a raised, ragged flow front, a band of scorched
//ground with dead trunks standing in it, then pasture, dark conifer belts, a river with pale gravel bars mirroring
//the dusk, a small village, and layered ridges with a second, dormant cone on the horizon. VolcanoCountry (C#)
//holds the same figures, solves the river's course and its levels, and TerrainMirror.Volcano mirrors the height
//below term for term; VolcanoCountryPlanting stands the snags, the woods and the village on it.

float FieldReach;
float FieldLobes;
float FrontHeight;
float LandRise;
float HillHeight;
float RidgeHeight;
float4 DormantCone;        //xy its axis, z its foot's radius, w its height
float RiverHalfWidth;
float2 VillageXZ;
float2 VillageRiver;       //the village's place along the river's course, and which bank it stands on (+1 or -1)

//The river's course (VolcanoCountry.RiverPath): xy a point, z the distance along the course to it, w the water's
//level there over VolcanoLevelY. Matched by VolcanoCountry.MAX_RIVER_POINTS.
#define MAX_RIVER_POINTS 28
float4 RiverPath[MAX_RIVER_POINTS];
int RiverPoints;

float3 PastureColor;
float3 PastureDry;
float3 ScorchedColor;
float3 AshColor;
float3 ForestFloorColor;
float3 HeathColor;
float3 ScreeColor;
float3 SoilColor;
float3 StubbleColor;
float3 CropColor;
float3 HedgeColor;
float3 GravelColor;
float3 LaneColor;
float3 WaterColor;
float WaterFire;

//What the air over the land is, away from the cone (VolcanoBackdrop.CountryHaze, stated every frame): the dome as it is
//drawn at the horizon, dimmed. The ash pall the field hazes into is the eruption's and thins away from it over PALL_THINS
//past the field's edge; hazed into the pall all the way out, the green hills went the brown of felt, where every reference
//turns its far ranges blue. The land's plants are hazed into the same colour (PlantPass.DrawInWeather).
float3 LandHazeColor;

//The land's shape, VolcanoCountry's constants to the figure (see there for what each says): the field's edge, the
//front, the country's rise, hills and ridges, the dormant cone, the valley and the meanders, the woods, the fields.
static const float EDGE_F1 = 0.0017, EDGE_O1 = 3.1, EDGE_W1 = 1.4;
static const float EDGE_F2 = 0.0055, EDGE_O2 = 7.7, EDGE_W2 = 0.55;
static const float EDGE_F3 = 0.016, EDGE_O3 = 1.3, EDGE_W3 = 0.3;
static const float FRONT_LIP = 2.0, FRONT_FOOT = 5.0, LEVEE_HEIGHT = 1.1, LEVEE_RUN = 16.0;
static const float LAND_RAMP = 30.0, LAND_RISE_RUN = 600.0;
static const float HILLS_FROM = 40.0, HILLS_TO = 260.0, HILL_F1 = 0.0075, HILL_O1 = 1.3, HILL_F2 = 0.019, HILL_O2 = 5.1;
static const float RIDGES_FROM = 250.0, RIDGES_TO = 650.0, RIDGE_F1 = 0.0028, RIDGE_O1 = 9.4, RIDGE_F2 = 0.0071, RIDGE_O2 = 2.6;
static const float RIDGE_F3 = 0.018, RIDGE_O3 = 5.7, RIDGE_W3 = 0.12;
static const float DORMANT_SUMMIT = 0.1, DORMANT_PROFILE = 1.6, DORMANT_HOLLOW = 0.04;
static const float VALLEY_HALF = 80.0, VALLEY_WIDENING = 140.0, BANK_RUN = 9.0, GRAVEL_RISE = 0.8, FLOOD_SLOPE = 0.1;
static const float MEANDER_A1 = 20.0, MEANDER_K1 = 0.0273, MEANDER_P1 = 0.7;
static const float MEANDER_A2 = 7.0, MEANDER_K2 = 0.0661, MEANDER_P2 = 2.9;
static const float FOREST_F1 = 0.0062, FOREST_O1 = 4.4, FOREST_F2 = 0.019, FOREST_O2 = 1.9;
static const float FOREST_LOW = 0.04, FOREST_HIGH = 0.24, FOREST_FROM = 70.0, FOREST_RUN = 50.0;
static const float FOREST_RIVER_FROM = 16.0, FOREST_RIVER_TO = 44.0;
static const float FIELDS_REACH = 260.0, VILLAGE_CORE = 62.0;

//The pixels' own figures, which nothing on the CPU reads. Where the field's shading gives way to the land's, past the
//edge (the front's lower face is still the field's rubble; the land starts at its foot), and how ragged that line is:
//a noise of RAGGED_SIZE world units, RAGGED units either way, worked out only within RAGGED_BAND of the edge.
static const float LAND_FROM = 2.5, LAND_TO = 7.0;
static const float RAGGED = 4.0, RAGGED_BAND = 24.0;
//The scorched band before the front: char and ash out to between these, its far line broken by noise
static const float SCORCH_FROM = 26.0, SCORCH_TO = 70.0;
//How far past the field's edge the ash pall has thinned into the land's own air
static const float PALL_THINS = 220.0;
//How much of the far fade (FarField.fxh) a pixel may have before it is left to it: past this the ground is the sky behind
//it to within a hundredth, whatever is shaded there, so neither the field nor the land is
static const float SKY_TAKES_OVER = 0.99;
//The village's plots: a long side, a short side, the axis they are laid out along, and the lane along the river
static const float PLOT_LENGTH = 46.0, PLOT_DEPTH = 27.0;
static const float2 PLOT_AXIS = float2(0.94, 0.34);
static const float LANE_OFFSET = 44.0, LANE_HALF = 1.6, LANE_SPAN = 420.0;

//--- Height field ------------------------------------------------------------------------------------------

//The cone's analytic massing at a world XZ, relative to VolcanoLevelY and BEFORE the clearing ramp: the
//flank, the crater bitten out of its summit and the gullies raked down it. Mirrored term for term by
//TerrainMirror.Volcano, which places the vents and the rivers' lights on this surface - the one
//term that mirror leaves out is the scoria fBm below, whose few units are invisible under a lamp.
float VolcanoMassing(float2 p)
{
    float2 d = p - ConeCenterXZ;
    float r = length(d);
    float bearing = atan2(d.y, d.x);

    //Shallow at the foot, steepest across the middle of the flank, rounding off at the summit - the profile
    //of a young stratovolcano rather than the straight-sided cone pow(t, 1) would give.
    //
    //THE RADIUS FED IN HERE IS CLAMPED TO CraterRadius, not r itself - #223's own shipped bug, found by
    //looking at the summit rather than by the maths. Evaluated at r the cone's flank is maximal exactly at
    //r = 0 for any ConeProfile > 0 (its slope there is -ConeProfile / ConeRadius, never zero), so
    //subtracting a crater term that is ALSO maximal at r = 0 only steepens the final approach to a point -
    //it cannot put the true summit anywhere but dead centre, whatever CraterDepth is. The clamp is what
    //moves the peak: the flank now plateaus at its own r = CraterRadius height for every r inside it, so
    //that plateau - not a receding point - is the surface the crater below is cut into.
    float flankRadius = max(r, CraterRadius);
    float t = saturate(1.0 - flankRadius / ConeRadius);
    float flank = ConeHeight * pow(t, ConeProfile);

    //The bowl cut into that plateau: zero at the rim (r = CraterRadius, where it must join the ordinary
    //flank with no step - and does, since the clamp above holds the plateau at exactly the unclamped
    //flank's own value there) and CraterDepth deep at the vent. The rim itself is not drawn taller than the
    //plateau it sits on; it reads as a rim only because the bowl falls away on one side of it and the
    //ordinary flank falls away on the other - a real crater's whole shape, not an added lip.
    float crater = CraterDepth * smoothstep(CraterRadius, 0.0, r);

    //Radial gullies, absent at the crater rim and at the foot, deepest across the flank the lava runs down.
    //The inner sine bends them so they are not a clean starburst.
    //
    //SceneRenderer.SnapToGully solves this same term for its floors and lays the rivers in them, so the
    //flows run where the ground drains. Change the expression and change that one with it.
    float rake = 0.5 - 0.5 * cos(bearing * GullyCount + 2.0 * sin(bearing * 3.0));
    float gullyBand = smoothstep(CraterRadius * 1.3, ConeRadius * 0.30, r)
        * smoothstep(ConeRadius * 1.15, ConeRadius * 0.85, r);

    return flank - crater - GullyDepth * rake * gullyBand;
}

//--- The country's height (#748) --------------------------------------------------------------------------

//The flow's margin over the field's level: a levee standing proud of the field behind it, then the front's face
//falling FrontHeight to the land. A flow ends in a WALL, not a colour line - every reference draws the black lava
//stopping at a raised, ragged edge with the land lower beyond it.
float FrontProfile(float outside)
{
    float levee = LEVEE_HEIGHT * smoothstep(-LEVEE_RUN, -FRONT_LIP, outside);
    float fall = smoothstep(-FRONT_LIP, FRONT_FOOT, outside);
    return levee * (1.0 - fall) - FrontHeight * fall;
}

//The river at p: the signed distance across its meandering centre line, how far along the course the nearest point
//is, and the water's level there over VolcanoLevelY; and the course's direction there and how fast its water falls
//along it, which LandAt carries the three on by. A loop over a uniform count with no gradient operation, run once a
//vertex (the pixels read its answers interpolated) and in the height probe.
float3 RiverCourse(float2 p, out float2 direction, out float levelRate)
{
    float best = 1e12;
    float side = 1.0;
    float along = 0.0;
    float level = 0.0;
    float2 run = float2(1.0, 0.0);
    float fall = 0.0;

    for (int i = 0; i < RiverPoints - 1; i++)
    {
        float4 a = RiverPath[i];
        float4 b = RiverPath[i + 1];
        float2 e = b.xy - a.xy;
        float2 w = p - a.xy;
        float t = saturate(dot(w, e) / dot(e, e));
        float2 q = w - e * t;
        float d2 = dot(q, q);
        if (d2 < best)
        {
            best = d2;
            side = e.x * w.y - e.y * w.x < 0.0 ? -1.0 : 1.0;
            along = lerp(a.z, b.z, t);
            level = lerp(a.w, b.w, t);
            run = e;
            fall = b.w - a.w;
        }
    }

    float runLength = length(run);
    direction = run / runLength;
    levelRate = fall / runLength;

    //The meanders: two sines of the distance along the course, so the river swings across its valley and the
    //valley follows it (a meander is the river's, not the ground's)
    float meander = MEANDER_A1 * sin(along * MEANDER_K1 + MEANDER_P1) + MEANDER_A2 * sin(along * MEANDER_K2 + MEANDER_P2);
    return float3(side * sqrt(best) - meander, along, level);
}

//The ranges from their three noises: rounded crests along the zero contours of a broad one, over a finer one, and knolls
//and saddles along them from a third (a range's skyline is never one smooth curve). Rounded, not the sharp 1 - |n| of a
//ridged noise: the ranges stand 700 units out and more, on the far ring's cells of about a degree, and a sharp crest
//drawn by them was a polyline of straight runs against the sky - the low-poly look, not a mountain.
float Ridge(float3 n)
{
    float crest = saturate(1.0 - 2.6 * n.x * n.x);
    return 0.75 * crest * crest + 0.25 * saturate(1.0 - 2.6 * n.y * n.y) + RIDGE_W3 * n.z;
}

//The dormant cone on the horizon: an old volcano, worn round. Its flank plateaus at DORMANT_SUMMIT of its radius for
//the main cone's reason (VolcanoMassing), with a shallow hollow worn into the plateau where its crater was.
float DormantRise(float2 p)
{
    float r = length(p - DormantCone.xy);
    float t = saturate(1.0 - max(r, DormantCone.z * DORMANT_SUMMIT) / DormantCone.z);
    return DormantCone.w * (pow(t, DORMANT_PROFILE) - DORMANT_HOLLOW * smoothstep(DormantCone.z * DORMANT_SUMMIT, 0.0, r));
}

//The land's figures at a point (#748), what its height is composed from: how far past the field's edge, the river, and
//the noises the hills and the ranges are made of. LandAt works them out with their slopes, and the vertex program
//carries them two units on (Carried) to the neighbours its normal is differenced against instead of working them out
//three times: the river's course is a loop over 27 points and the edge, hills and ranges eight noises, and evaluated at
//all three points they made the vertex program three times its old length. The height at the vertex itself - the one
//the mirror copies - takes them exact.
struct Land
{
    float ConeR;      //distance from the cone's axis (exact at a neighbour too: it is cheap)
    float Outside;    //how far past the field's edge
    float3 River;     //RiverCourse's: across, along, the water's level
    float Hills;      //the hills' two octaves
    float3 Ridge;     //the ranges' three noises
};

struct LandSlopes
{
    float2 Outside;
    float2 Across;
    float2 Along;
    float2 Level;
    float2 Hills;
    float2 Ridge1;
    float2 Ridge2;
    float2 Ridge3;
};

//How far short of the field's edge the land's own figures are worked out: the neighbours stand two units off, and the
//distance past the edge moves under two units for each (its slope is the bearing's plus the edge's, under 1.83)
static const float LAND_FIGURES_FROM = -6.0;

//The field's edge is three octaves of the clouds' noise read on the world (a bearing would tear along atan2's seam):
//tongues where one flow ran further than the next, lobes on them and the margin's ragged run. One octave drew a wavy
//circle, the rim of a plate seen from the cone's far side. Their slopes summed stay under one, so the distance past it
//is a true distance to within that. CloudNoiseD's values are CloudNoise's (the same corners and fade), with the slope.
Land LandAt(float2 p, out LandSlopes slopes)
{
    Land land;
    float2 fromCone = p - ConeCenterXZ;
    land.ConeR = length(fromCone);

    float3 e1 = CloudNoiseD(p * EDGE_F1 + EDGE_O1);
    float3 e2 = CloudNoiseD(p * EDGE_F2 + EDGE_O2);
    float3 e3 = CloudNoiseD(p * EDGE_F3 + EDGE_O3);
    land.Outside = land.ConeR - (FieldReach + FieldLobes * (EDGE_W1 * e1.x + EDGE_W2 * e2.x + EDGE_W3 * e3.x));
    slopes.Outside = fromCone / max(land.ConeR, 1e-3)
        - FieldLobes * (EDGE_W1 * EDGE_F1 * e1.yz + EDGE_W2 * EDGE_F2 * e2.yz + EDGE_W3 * EDGE_F3 * e3.yz);

    land.River = float3(1e4, 0.0, 0.0);
    land.Hills = 0.0;
    land.Ridge = 0.0;
    slopes.Across = 0.0;
    slopes.Along = 0.0;
    slopes.Level = 0.0;
    slopes.Hills = 0.0;
    slopes.Ridge1 = 0.0;
    slopes.Ridge2 = 0.0;
    slopes.Ridge3 = 0.0;

    [branch]
    if (land.Outside > LAND_FIGURES_FROM)
    {
        float2 direction;
        float levelRate;
        land.River = RiverCourse(p, direction, levelRate);
        float meanderRate = MEANDER_A1 * MEANDER_K1 * cos(land.River.y * MEANDER_K1 + MEANDER_P1)
            + MEANDER_A2 * MEANDER_K2 * cos(land.River.y * MEANDER_K2 + MEANDER_P2);
        slopes.Across = float2(-direction.y, direction.x) - meanderRate * direction;
        slopes.Along = direction;
        slopes.Level = levelRate * direction;

        float3 h1 = CloudNoiseD(p * HILL_F1 + HILL_O1);
        float3 h2 = CloudNoiseD(p * HILL_F2 + HILL_O2);
        land.Hills = h1.x + 0.5 * h2.x;
        slopes.Hills = HILL_F1 * h1.yz + 0.5 * HILL_F2 * h2.yz;

        float3 r1 = CloudNoiseD(p * RIDGE_F1 + RIDGE_O1);
        float3 r2 = CloudNoiseD(p * RIDGE_F2 + RIDGE_O2);
        float3 r3 = CloudNoiseD(p * RIDGE_F3 + RIDGE_O3);
        land.Ridge = float3(r1.x, r2.x, r3.x);
        slopes.Ridge1 = RIDGE_F1 * r1.yz;
        slopes.Ridge2 = RIDGE_F2 * r2.yz;
        slopes.Ridge3 = RIDGE_F3 * r3.yz;
    }

    return land;
}

//The land's figures carried `offset` on from where they were worked out, to first order (offset is two units; the
//noises' shortest wavelength is 55 and the river's bends are rounded), at the point p they are carried to
Land Carried(Land land, LandSlopes slopes, float2 offset, float2 p)
{
    Land carried;
    carried.ConeR = length(p - ConeCenterXZ);
    carried.Outside = land.Outside + dot(slopes.Outside, offset);
    carried.River = land.River + float3(dot(slopes.Across, offset), dot(slopes.Along, offset), dot(slopes.Level, offset));
    carried.Hills = land.Hills + dot(slopes.Hills, offset);
    carried.Ridge = land.Ridge + float3(dot(slopes.Ridge1, offset), dot(slopes.Ridge2, offset), dot(slopes.Ridge3, offset));
    return carried;
}

//The land past the front, added to `relative` (the massing and the front over the field's level), and the river's
//valley cut into it. Only past the field's edge (the caller's branch); `land` brings it in over the front's foot.
//
//The land RISES from the front's foot towards the horizon (LandRise), rolls (the hills) and stands up into ranges past
//them, with the dormant cone among them; that rise is also what keeps it in view from a camera at the arena, which
//looks across the field's raised edge. The hills and ridges ease down to nothing in the valley (`shoulder`), so the
//valley floor is the rise alone, and the water lies RIVER_SINK under that (its levels are VolcanoCountry's): the cut
//is a min() against the bank's profile, so it carves where the land stands higher and leaves lower ground alone, and
//the channel itself is laid exactly level across, which is what the water is drawn on.
float CountryHeight(float2 p, Land figures, float relative)
{
    float3 river = figures.River;
    float outside = figures.Outside;
    float d = abs(river.x);
    float land = smoothstep(0.0, LAND_RAMP, outside);

    float rise = LandRise * smoothstep(0.0, LAND_RISE_RUN, figures.ConeR - FieldReach);
    float hills = HillHeight * smoothstep(HILLS_FROM, HILLS_TO, outside) * figures.Hills;
    float ridgeShare = smoothstep(RIDGES_FROM, RIDGES_TO, outside);
    float ridges = RidgeHeight * ridgeShare * Ridge(figures.Ridge);
    float valleyHalf = VALLEY_HALF + VALLEY_WIDENING * ridgeShare;
    float shoulder = smoothstep(RiverHalfWidth + BANK_RUN, valleyHalf, d);
    float ground = rise + (hills + ridges) * shoulder;
    ground = max(ground, rise + DormantRise(p));

    relative += land * ground;

    float bank = river.z + GRAVEL_RISE * smoothstep(RiverHalfWidth, RiverHalfWidth + BANK_RUN, d)
        + FLOOD_SLOPE * max(d - RiverHalfWidth - BANK_RUN, 0.0);
    relative = lerp(relative, min(relative, bank), land * smoothstep(valleyHalf, valleyHalf * 0.5, d));
    relative = lerp(relative, river.z, land * (1.0 - smoothstep(RiverHalfWidth - 0.5, RiverHalfWidth + 0.5, d)));
    return relative;
}

//The terrain displacement at a world XZ from the land's figures there: flat at the island's foot, rising into the flank
//with distance from the ARENA (not from the cone), so the play surface sits in a clearing exactly as it does in every
//other terrain scene.
float HeightOf(float2 p, Land figures, uniform int scoriaOctaves)
{
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, length(p));

    //Broken scoria over the whole field. Mean zero, so it does not lift the clearing as the ramp opens - the
    //failure Desert.fx's trailing constant exists to prevent. Past the front a quarter of it is left: ground is
    //never a table, and what is left is under the land's own grain.
    float scoria = ScoriaRelief * Fbm2(p * 0.038, scoriaOctaves)
        * lerp(0.25, 1.0, 1.0 - smoothstep(-FRONT_LIP, FRONT_FOOT, figures.Outside));

    float relative = VolcanoMassing(p) + scoria + FrontProfile(figures.Outside);

    [branch]
    if (figures.Outside > 0.0)
        relative = CountryHeight(p, figures, relative);

    return VolcanoLevelY + ramp * relative;
}

//The height at p with its land worked out there, exact: what the vertex program displaces each vertex by and what the
//height probe reads. `country` hands back what the pixels need of the land (#748): how far past the field's edge p
//stands, and the river's across and along there.
float TerrainHeightAt(float2 p, uniform int scoriaOctaves, out float3 country)
{
    LandSlopes slopes;
    Land figures = LandAt(p, slopes);
    country = float3(figures.Outside, figures.River.x, figures.River.y);
    return HeightOf(p, figures, scoriaOctaves);
}

float TerrainHeight(float2 p, uniform int scoriaOctaves)
{
    float3 country;
    return TerrainHeightAt(p, scoriaOctaves, country);
}

//--- Rivers ------------------------------------------------------------------------------------------------

//The reduced program's flow front (#540): lobes from two sines of the world position in place of the gradient noise
//the full program bends each front by. That noise sat inside the loop over the rivers, so every pixel of the terrain
//paid for five of them, flow or not - on the APU at Low it was worth about 0.9 ms of the frame on its own - and two
//crossed sines still end a flow in lobes rather than on a circle, which is all the eye reads of a front a hundred
//units off.
float FrontLobes(float2 p, float r, int i)
{
    return 0.6 * sin(dot(p, float2(0.061, 0.047)) + i * 2.3) * sin(dot(p, float2(-0.043, 0.071)) + r * 0.004 + i * 1.1);
}

//Signed angular difference wrapped into (-π, π], so a river whose bearing sits near the seam is still one
//river and not two half ones.
float AngleDelta(float a, float b)
{
    float d = a - b;
    return d - TWO_PI * floor((d + 3.14159265) / TWO_PI);
}

struct RiverSample
{
    float Mask;     //1 on the flow, feathered to 0 at its edge
    float Halo;     //a wider, softer field: the band of ground the flow is heating
    float Along;    //distance down the flank, already scrolled - the flow's own coordinate. What the melt
                    //carries is drawn in it; what is fixed in the channel (its fissures) in ConeR instead
    float Across;   //SIGNED distance across the flow from its centre line, so a pattern drawn in it is not
                    //mirrored about that line
    float HalfWidth;//the nearest flow's own half-width here, which Across is read against
    float Reach;    //how far from the cone's axis the nearest flow's front stops
    float ConeR;    //distance from the cone's axis, which the summit's own heat is a function of
};

//The nearest river at a world point. A loop over a UNIFORM count (uniform flow control, no divergence) with
//no gradient operation inside it, which is what lets the whole thing run unbranched: everything the pixel
//needs from the rivers comes out of this one call, on the flow and off it alike.
RiverSample SampleRivers(float2 p, float footprint, uniform bool fullDetail)
{
    float2 d = p - ConeCenterXZ;
    float r = length(d);
    float bearing = atan2(d.y, d.x);

    RiverSample best;
    best.Mask = 0.0;
    best.Halo = 0.0;
    best.Along = 0.0;
    best.Across = 1e6;
    best.HalfWidth = RiverWidth;
    best.Reach = ConeRadius;
    best.ConeR = r;

    for (int i = 0; i < RiverCount; i++)
    {
        //A flow wanders on the way down rather than running true - the wander is a function of the RADIUS,
        //so it bends the river's course and does not merely wobble its edges.
        float wander = RiverWander * sin(r * 0.017 + i * 2.13) * saturate(r / ConeRadius);
        float across = AngleDelta(bearing, RiverBearing[i] + wander) * r;

        //The front: the flow thins and stops somewhere down the flank, each river at its own reach, with a
        //noisy edge so it ends in a lobed front rather than on a circle.
        float front = 1.0 - smoothstep(RiverReach[i] * 0.82, RiverReach[i] + 24.0 * (fullDetail ? GradientNoise2(p * 0.01 + i) : FrontLobes(p, r, i)), r);

        //Feathered against the pixel footprint as well as the width, or a river narrower than a pixel out
        //near the horizon turns into a crawling dashed line. And NARROW UNDER THE SUMMIT (#509): a flow
        //leaves its vent as a channel and spreads as the slope eases, and at full width from the rim the five
        //flows between them paved the whole upper cone - which no reference drew.
        float halfWidth = RiverWidth * (0.75 + 0.45 * sin(r * 0.026 + i * 1.7))
            * lerp(0.4, 1.0, smoothstep(CraterRadius, ConeRadius * 0.55, r));
        float edge = footprint * 0.5;
        float mask = (1.0 - smoothstep(halfWidth * 0.55 - edge, halfWidth + edge, abs(across))) * front;

        //The heated BAND either side of the flow. HaloWidth is a multiple of the river's own half-width, and
        //it is a dial because the first pass had it at six and the arena stands on the river that passes it:
        //the halo swallowed the whole foreground and the entire plain glowed orange.
        float halo = (1.0 - smoothstep(halfWidth, halfWidth * HaloWidth, abs(across))) * front;

        //The nearest flow wins the coordinates the crust is drawn in; the masks take the strongest, so two
        //rivers that meet high on the flank merge instead of one cancelling the other.
        bool nearer = abs(across) < abs(best.Across);
        best.Across = nearer ? across : best.Across;
        best.Along = nearer ? r - VolcanoTime * RiverSpeed : best.Along;
        best.HalfWidth = nearer ? halfWidth : best.HalfWidth;
        best.Reach = nearer ? RiverReach[i] : best.Reach;

        best.Mask = max(best.Mask, mask);
        best.Halo = max(best.Halo, halo);
    }

    return best;
}

//A thin bright line on the zero contour of a noise value, band-limited against how many noise units one
//pixel spans. Near, a hairline of full strength with a crisp edge; far, the line WIDENS to the pixel and
//DIMS in proportion, so its average brightness over an area stays what it was near instead of either
//crawling or vanishing. The flat core is what makes it a line: a profile falling off from its centre all
//the way out reads as a soft glowing worm at every distance, which is what the first cut drew.
float GlowLine(float n, float width, float unitsPerPixel)
{
    float spread = width + unitsPerPixel;
    return (1.0 - smoothstep(width * 0.5, spread, abs(n))) * (width / spread);
}

//The cracks in the lava field: a hairline on the zero contour of a two-octave noise, a fixed WORLD width wide
//and antialiased off the noise's own SLOPE - |n| / (|grad n| * footprint) is the distance to the contour in
//pixels, to first order, however steep or shallow the field happens to be there. The first cut thresholded |n|
//against one assumed steepness, as the rivulets do, and near a saddle of the noise - where the field lingers
//close to zero across a wide area - that painted fat orange blobs. The second took the slope as fwidth(n),
//which cannot sit in a branch, so every pixel of the field paid for a crack only a few patches have. The
//slope comes analytically out of Clouds.fxh's CloudNoiseD instead, so this runs inside one. Two octaves, so
//the line is jagged the way a crack in rock is; one octave's contour is a smooth curve and reads as a glowing
//wire laid on the ground. Narrower than a pixel, the line dims in proportion rather than thinning away.
float FieldCracks(float2 xz, float footprint)
{
    float2 q = xz * FIELD_CRACK_SCALE;
    float3 coarse = CloudNoiseD(q);
    float3 fine = CloudNoiseD(q * 2.7 + 3.1);

    float n = coarse.x + 0.35 * fine.x;
    float2 slope = (coarse.yz + 0.35 * 2.7 * fine.yz) * FIELD_CRACK_SCALE;

    float pixels = abs(n) / max(length(slope) * footprint, 1e-6);
    float halfPixels = FIELD_CRACK_HALF_WIDTH / max(footprint, 1e-4);
    return (1.0 - smoothstep(halfPixels, halfPixels + 1.0, pixels)) * saturate(2.0 * halfPixels)
        * saturate(1.0 - footprint * FIELD_CRACK_SCALE * 4.0);
}

//--- Vertex ------------------------------------------------------------------------------------------------

struct VolcanoVertexInput
{
    float4 Position : POSITION0;
};

struct VolcanoVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    //The country (#748): how far past the field's edge, and the river's across and along - smooth at the grid's
    //scale, so interpolated they are exact enough, and the pixels pay no loop over the river's course
    float3 Country : TEXCOORD2;
};

VolcanoVertexOutput VolcanoVSImpl(VolcanoVertexInput input, uniform int scoriaOctaves)
{
    VolcanoVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;

    //The land's figures once, with their slopes (#748: see LandAt), and the height at the vertex from them exact
    LandSlopes slopes;
    Land figures = LandAt(xz, slopes);
    output.Country = float3(figures.Outside, figures.River.x, figures.River.y);
    float height = HeightOf(xz, figures, scoriaOctaves);

    //Base normal per vertex, as on the mountain and for the same reason: a per-pixel finite-difference
    //normal on this much distant, steep ground aliases into shimmer, and the per-pixel relief below carries
    //the near detail anyway. The neighbours take the land's figures carried to them.
    float e = 2.0;
    float2 xa = xz + float2(e, 0.0);
    float2 xb = xz + float2(0.0, e);
    float hx = HeightOf(xa, Carried(figures, slopes, float2(e, 0.0), xa), scoriaOctaves);
    float hz = HeightOf(xb, Carried(figures, slopes, float2(0.0, e), xb), scoriaOctaves);
    output.WorldNormal = normalize(float3(-(hx - height) / e, 1.0, -(hz - height) / e));

    float3 worldPosition = float3(xz.x, height, xz.y);
    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//The reduced vertex program (#540) takes the scoria in two octaves rather than four, three times a vertex: the grid is
//129 600 vertices (66 049 at the reduced grid, VOLCANO_GRID_N_REDUCED), and the finest two octaves of a relief whose
//coarsest wavelength is 26 units are finer than the reduced grid's own cell resolves.
VolcanoVertexOutput VolcanoVS(VolcanoVertexInput input) { return VolcanoVSImpl(input, 4); }
VolcanoVertexOutput VolcanoVSReduced(VolcanoVertexInput input) { return VolcanoVSImpl(input, 2); }

//--- Pixel -------------------------------------------------------------------------------------------------

//The flow's own surface at one point, as radiance: the chilled crust rafts it carries, the open core down its
//middle, the streamlines in that core and the fissures in the channel's bed, which stay put while the rafts
//roll over them and glow where a lead between rafts uncovers them (#554). `detail` is the caller's crackle fade -
//the lines and cracks are hairlines, and past the distance they resolve they are replaced by their own mean.
float3 FlowRadiance(RiverSample river, float footprint, float detail)
{
    //0 on the centre line, 1 at the bank; 0 just below the vent, 1 at the front
    float acrossN = saturate(abs(river.Across) / max(river.HalfWidth, 1e-3));
    float run = saturate((river.ConeR - CraterRadius) / max(river.Reach - CraterRadius, 1.0));

    //The open core: wide under the vent, a thread by the front, and torn wider in slow patches along the
    //way - where the crust has broken up and the melt is showing through.
    float tear = GradientNoise2(float2(river.Along * 0.03, 3.7));
    float coreWidth = lerp(CORE_AT_VENT, CORE_AT_FRONT, run) * (0.75 + 0.5 * tear);
    float core = 1.0 - smoothstep(coreWidth * 0.5, coreWidth, acrossN);

    //The streamlines in the core: bright threads and duller ones lying downstream, side by side. STRIPES
    //whose phase a stretched noise bends, not the zero contour of that noise: a contour closes on itself
    //round every saddle, and the first cut drew the core full of long glowing eyes where the references draw
    //parallel striations. SIGNED across, or the pattern is mirrored about the centre line. Sharpened by the
    //cube, so the bright threads are narrow and the dull ones wide - and faded to the cube's own mean,
    //5/16, as the stripes approach the pixel.
    float bend = GradientNoise2(float2(river.Across * 0.2, river.Along / (STREAM_STRETCH * 1.5)));
    float stripe = 0.5 + 0.5 * cos(river.Across * STREAM_FREQUENCY + 2.5 * bend);
    float stream = lerp(0.3125, stripe * stripe * stripe, saturate(1.5 - footprint * STREAM_FREQUENCY * 0.5));

    //TWO LAYERS, AND THEY MOVE DIFFERENTLY (#554). The glowing fissures are the channel's BED: a network of
    //seams in the ground the flow runs in, and they stay where they are. The crust is a skin of rafts CARRIED
    //on the melt, and it rolls downstream over them. Until #554 both were one Voronoi web read in the scrolled
    //coordinate, so the whole cracked pattern slid down the flank with the melt - which the owner read the
    //right way round: the seams are fixed and the lava flows through and over them.
    //
    //The fissures: the old anisotropic web, read in the channel's UNSCROLLED coordinate (the distance from
    //the cone's axis itself, not Along). Still long along the flow - the seams run the way the channel
    //does, and an isotropic Voronoi here is the honeycomb the first build drew. Warped for the reason every
    //Voronoi in this file is: a jittered lattice still has a lattice in it.
    float2 bedUV = float2(river.Across / (PlateSize * 0.9), river.ConeR / (PlateSize * 3.2));
    float bedEdge = VoronoiEdge2(bedUV + float2(GradientNoise2(bedUV * 0.35),
                                                GradientNoise2(bedUV * 0.35 + 11.3)) * 0.6);
    float fissure = 1.0 - saturate(bedEdge * 11.0);

    //The rafts: a noise in the SCROLLED coordinate, stretched along the flow, thresholded into crust that
    //covers the bed and open leads between rafts where it does not. RAFT_COVER of the channel is crust; a
    //fissure under a raft is only a trace (RAFT_SHOW) and one in a lead glows in full, so the seams are
    //uncovered and buried again as the rafts pass - which is the whole reading of lava rolling over them.
    float2 raftUV = float2(river.Across / (PlateSize * 1.4), river.Along / (PlateSize * 5.0));
    float lead = 1.0 - smoothstep(RAFT_COVER - 0.12, RAFT_COVER + 0.12, GradientNoise2(raftUV) * 0.5 + 0.5);
    float crack = fissure * lerp(RAFT_SHOW, 1.0, lead);

    //Past the distance the cracks resolve, their own mean: about a tenth of the crust.
    crack = lerp(0.1, crack, detail);
    lead = lerp(0.3, lead, detail);

    //The melt pulses slowly as the supply behind it surges, and cools on its way down.
    float surge = 0.85 + 0.15 * sin(VolcanoTime * 0.7 + river.Along * 0.05);
    float heat = saturate((0.55 + 0.45 * stream) * (1.0 - 0.35 * run) * surge);
    float3 melt = lerp(LavaCool, LavaHot, heat * heat) * (0.55 + 0.45 * heat);

    //What the crust itself radiates: a dull red where it is young and thin, near the vent; its cracks glow
    //through it at a cooler colour than the core does.
    //An open lead between rafts is the melt itself showing, a moving dull glow the rafts carry down with
    //them - kept far under the core's, or the channel turns back into the lit width #509 took away.
    float3 crust = LavaCool * (CrustGlow + LEAD_GLOW * lead) * (1.0 - 0.7 * run)
        + lerp(LavaCool, LavaHot, 0.3) * crack * CrackGlow * (1.0 - 0.5 * run);

    //And the bank: where the crust tears from the levee the melt shows as a thin bright line along the
    //flow's edge, which is what outlines every lobe in the references.
    float bank = smoothstep(0.62, 0.84, acrossN) * (1.0 - smoothstep(0.84, 1.0, acrossN));
    crust += LavaCool * bank * 0.6 * lerp(1.0, 0.5, detail);

    return lerp(crust, melt, core);
}

//The rivulets: thin incandescent threads running down the cone from its crater, a few of them long and most
//short, each wandering as it goes. Every eruption the references drew had them - a cone streaked with fine
//glowing lines, not banded by five wide ones.
//
//Sampled on a CIRCLE in noise space, (cos b, sin b) scaled, and not on the bearing itself: atan2's bearing
//jumps by 2π across -X, and a noise read off it tears along that line from the crater to the foot (the
//cavern's mineral veins shipped with exactly that scar once). The circle is a closed curve in the noise's
//domain, so it comes back round to itself with no seam; the outback's gullies are drawn the same way.
float Rivulets(float2 around, float coneR, float footprint)
{
    float n = GradientNoise3(float3(around * RIVULET_ANGULAR, coneR * RIVULET_RADIAL));
    float thread = GlowLine(n, RIVULET_WIDTH, footprint * RIVULET_ANGULAR / max(coneR, 1.0));

    //Which threads run, and how far each gets: a coarse field round the cone that does not change down it,
    //so a thread is on or off for its whole length and stops at its own reach.
    float gate = GradientNoise3(float3(around * RIVULET_ANGULAR * 0.35, 5.3));
    float active = smoothstep(-0.05, 0.25, gate);
    float reach = ConeRadius * (0.32 + 0.4 * saturate(gate * 1.2 + 0.5));

    float start = smoothstep(CraterRadius * 0.9, CraterRadius * 1.25, coneR);
    float stop = 1.0 - smoothstep(reach * 0.6, reach, coneR);

    return thread * active * start * stop;
}

//The field's own surface at one pixel (#223, #509, #554), as radiance before the haze: the rock, the heat off the
//flows and the hairlines, the flows themselves, the crater's lake and the sheen. Its own function since #748, so a
//pixel of the land past the field pays for none of it (VolcanoSurface's branches). `normal` and `relief` come in,
//because the derivatives behind them cannot be taken in the branch this runs in; `rubble` is the flow front's face
//(#748), broken clinker in blocks that takes no sheen and glows in a few cracks. Everything else it works out from the
//interpolants itself rather than taking it in, so nothing of the field's is held live across the land's branch.
float3 FieldSurface(VolcanoVertexOutput input, float footprint, float3 normal, float relief, float sunlight, float rubble,
    uniform bool fullDetail)
{
    float3 worldPosition = input.WorldPosition;
    float3 view = normalize(CameraPosition - worldPosition);
    float2 fromCone = worldPosition.xz - ConeCenterXZ;
    float coneR = length(fromCone);
    float2 around = fromCone / max(coneR, 1e-3);

    //How much per-pixel detail this pixel can still resolve. Everything cellular fades out against it well
    //before its cells reach pixel size - a Voronoi web left running to the horizon is a field of crawling
    //sparks, and this one is EMISSIVE, so it would spark straight through the glare pass.
    float detail = saturate(1.0 - footprint * 0.09);
    float crackleFade = saturate(1.0 - footprint * 0.55);

    RiverSample river = SampleRivers(worldPosition.xz, footprint, fullDetail);

    //--- The rock ----------------------------------------------------------------------------------------
    //Black basalt with patches of weathered grey scoria, and the ground round the summit OXIDISED: every
    //flank the references drew is dark grey below and a dark rust-red towards the crater, where the hot
    //gases have been at it. Keyed to the same broad field so it comes in patches and not as a ring.
    float rockPatch = saturate(Fbm2BandLimited(worldPosition.xz * 0.03, fullDetail ? 3 : 2, footprint * 0.03) * 1.6 + 0.5);
    float3 albedo = lerp(RockColor, RockColorLight, rockPatch * detail);

    float oxide = (1.0 - smoothstep(CraterRadius * 1.1, ConeRadius * 0.6, coneR)) * saturate(rockPatch * 1.5 + 0.2);
    albedo = lerp(albedo, ScoriaColor, oxide);


    //--- Lighting ----------------------------------------------------------------------------------------
    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));
    float3 light = skyAmbient * AmbientStrength + SunColor * ndotl * sunlight;

    float3 color = albedo * light;

    //Heat off the flows: the band beside a flow glows with what the flow is throwing on it. An ADDED
    //radiance rather than a tint on the albedo - lit black rock is still black rock, and at 2 % albedo a
    //multiplied light would not show at all.
    //Kept faint: at 0.3 the halos of the five flows met under the summit and washed the whole upper cone
    //orange, where every reference keeps the rock between the flows black.
    color += LavaCool * river.Halo * river.Halo * 0.07;

    //--- The rivulets and the cracks in the field --------------------------------------------------------
    //Both are hairlines of light on the dark ground, and both are in the full program only: they are the
    //reduced program's pair (see the techniques). Both sit behind DATA branches with no gradient operation
    //inside - the footprint was taken above.
    if (fullDetail)
    {
        [branch]
        if (coneR < ConeRadius * 0.75)
        {
            float rivulet = Rivulets(around, coneR, footprint) * (1.0 - river.Halo);
            float hotter = 1.0 - saturate(coneR / (ConeRadius * 0.75));
            color += lerp(LavaCool, LavaHot, 0.25 + 0.45 * hotter) * rivulet * RivuletStrength;
        }

        //Sinuous cracks in the crust of the field, a few patches of them and more beside the flows, where the
        //ground is still hot underneath: the zero contour of a noise, which draws a WANDERING line where a
        //Voronoi web would draw a net. Which patches crack is decided first and cheaply, and the line itself
        //only where one does - see FieldCracks for why that needs a noise that knows its own slope.
        //And along a stretch of the front here and there (#748): the margin is where the crust is thinnest.
        float cracked = max(smoothstep(0.25, 0.5, GradientNoise2(worldPosition.xz * 0.006 + 17.3)), river.Halo * 0.6);
        [branch]
        if (rubble > 0.0)
            cracked = max(cracked, rubble * smoothstep(0.15, 0.4, GradientNoise2(worldPosition.xz * 0.012 + 8.8)));
        cracked *= 1.0 - river.Mask;

        [branch]
        if (cracked > 0.0)
            color += lerp(LavaCool, LavaHot, 0.2) * FieldCracks(worldPosition.xz, footprint) * cracked * FieldCrackStrength;
    }

    //--- The flow ----------------------------------------------------------------------------------------
    //Behind a data branch: a flow is a narrow band of any frame, and its crust costs a Voronoi and three
    //noise taps that every pixel beside it paid until #509.
    float gloss = (1.0 - rockPatch) * (1.0 - oxide) * (1.0 - rubble);

    [branch]
    if (river.Mask > 0.0)
    {
        float3 flow = CrustColor * light + FlowRadiance(river, footprint, crackleFade);

        //The flow is opaque and replaces what is under it rather than adding to it - lava is rock.
        color = lerp(color, flow, river.Mask);
        gloss = lerp(gloss, 1.0, river.Mask);
    }

    //--- The crater's lake -------------------------------------------------------------------------------
    //Every crater the references drew at night holds molten lava: plates of dark crust drifting outward over
    //it from where it wells up, their seams incandescent. The one place in the scene a Voronoi web is the
    //right picture - a lake's crust is plates, where a flow's is streaks. Its walls are lit by it.
    [branch]
    if (coneR < CraterRadius * 1.25)
    {
        float walls = 1.0 - smoothstep(CraterRadius * 0.6, CraterRadius * 1.25, coneR);
        color += LavaCool * walls * 0.12;

        float lake = 1.0 - smoothstep(CraterRadius * 0.4, CraterRadius * 0.62, coneR);
        float2 lakeUV = (fromCone - around * VolcanoTime * 0.6) / (PlateSize * 2.5);
        float lakeEdge = VoronoiEdge2(lakeUV + float2(GradientNoise2(lakeUV * 0.4),
                                                      GradientNoise2(lakeUV * 0.4 + 9.1)) * 0.6);
        float seam = lerp(0.15, 1.0 - saturate(lakeEdge * 5.0), crackleFade);
        float upwell = 1.0 - smoothstep(0.0, CraterRadius * 0.22, coneR);
        float heat = saturate(max(seam * CrackGlow * 3.0, upwell));
        float3 lava = CrustColor * light + LavaCool * CrustGlow + lerp(LavaCool, LavaHot, heat * heat) * heat;

        color = lerp(color, lava, lake);
        gloss *= 1.0 - lake;
    }

    //--- The sheen ---------------------------------------------------------------------------------------
    //Cooled lava is glass. Black pahoehoe and a flow's chilled skin reflect the sky as a slate sheen, which is
    //the only thing that separates a lava field from a heap of soot at night - every ground-level reference
    //draws it. Fresnel-weighted, so it is the grazing ground the play camera looks across that takes it, and
    //the scoria and the oxidised summit do not (they are rough).
    float3 reflected = reflect(-view, normal);
    float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(normal, view)), 5.0);
    //Mostly the ZENITH, even at grazing: this dome's horizon uniform is a warm lit band far brighter than
    //the storm deck the scene actually stands under, and reflected in full it turned the whole field into
    //brown mud. A little of it is kept, so the sheen still follows the dome.
    float3 skyReflection = lerp(ZenithColor, HorizonColor, 0.08 * (1.0 - saturate(reflected.y)));

    //On the CRESTS of the ropy relief and not as a film: the references' pahoehoe catches the sky along each
    //fold and is black in between, and an even sheen reads as a wet floor.
    float crests = saturate(0.35 + relief * 2.5);
    color += skyReflection * fresnel * gloss * crests * SheenStrength;

    //The front's face (#748): aa clinker, blocks of it lighter and darker - a flow's margin is rubble that has tumbled
    //off its top, not the glassy skin of the field behind it. A shade on the finished colour and the last thing done,
    //where little else is live: laid into the albedo at the top, with the rock's patches and the rivers still live, it
    //cost the compiled program four temporaries.
    [branch]
    if (rubble > 0.0)
        color *= lerp(1.0, 0.55 + 1.6 * saturate(0.5 + 1.4 * GradientNoise2(worldPosition.xz * 0.7 + 3.3)), rubble);

    return color;
}

//How wooded the land is at p, 0 to 1 (#748): a two-octave field thresholded into belts, kept off the front, the
//river's banks and the village's fields. VolcanoCountry.ForestCover is the same mask, and the planting stands its
//spruces where it is high; here it darkens the ground under them to a forest floor - and past where anything is
//planted, to the dark a wooded hill is from far away.
float ForestCover(float2 p, float outside, float d, float villageD)
{
    float n = CloudNoise(p * FOREST_F1 + FOREST_O1) + 0.5 * CloudNoise(p * FOREST_F2 + FOREST_O2);
    return smoothstep(FOREST_LOW, FOREST_HIGH, n)
        * smoothstep(FOREST_FROM, FOREST_FROM + FOREST_RUN, outside)
        * smoothstep(RiverHalfWidth + FOREST_RIVER_FROM, RiverHalfWidth + FOREST_RIVER_TO, d)
        * smoothstep(FIELDS_REACH * 0.7, FIELDS_REACH * 1.05, villageD);
}

//The land past the field (#748), as radiance before the haze: the scorched band the flow burned on its way, pasture,
//the woods' floor, the village's patchwork of plots and the lane along the river, heath and scree on the ranges, the
//dormant cone bare above its woods, the river's gravel bars and the water. `country` is the vertex shader's (how far
//past the edge, the river's across and along). Everything here is a field of the world position or a noise that hands
//back its own slope (CloudNoiseD): this runs in a data branch, where no derivative may be taken. It works out its own
//normal and view from the interpolants, as FieldSurface does, for the same reason.
float3 CountrySurface(VolcanoVertexOutput input, float footprint, float sunlight)
{
    float3 worldPosition = input.WorldPosition;
    float2 p = worldPosition.xz;
    float outside = input.Country.x;
    float across = input.Country.y;
    float along = input.Country.z;
    float d = abs(across);
    float rise = worldPosition.y - (VolcanoLevelY - FrontHeight);

    //The ground's own grain: tussocks, ruts and clods tilting the light near, an even field far; and the folds a hillside
    //is made of - spurs and the gullies between them, too fine for the grid that draws the land - which still read across
    //a valley, where the grain is long gone. Both tilt the normal by the noise's own slope. The light is worked out
    //straight away, so the normal and the slopes are done with before the materials are laid (register pressure: see
    //VolcanoSurface): lit as the field is, the dome's hemisphere and the sun through the clouds and the shadow map.
    float3 baseNormal = normalize(input.WorldNormal);
    float steep = 1.0 - smoothstep(0.62, 0.82, baseNormal.y);
    float3 grain = CloudNoiseD(p * 0.23 + 4.7);
    float3 folds = CloudNoiseD(p * 0.03 + 8.3);
    float near = saturate(1.0 - footprint * 0.45);
    float3 normal = normalize(baseNormal - float3(grain.y, 0.0, grain.z) * (0.23 * 0.6 * near)
        - float3(folds.y, 0.0, folds.z) * (0.03 * 7.0 * saturate(1.5 - footprint * 0.04)));
    float3 light = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5)) * AmbientStrength
        + SunColor * (saturate(dot(normal, SunDirection)) * sunlight);
    float tussock = grain.x * near;

    //The pasture: green, and dried to straw in patches
    float patch = CloudNoise(p * 0.017 + 6.1) + 0.5 * CloudNoise(p * 0.061 + 1.7);
    float3 albedo = lerp(PastureColor, PastureDry, saturate(0.5 + 1.2 * patch)) * (1.0 + 0.2 * tussock);

    //The village's fields: a patchwork of plots laid along one axis, the hedges between them. No surveyor drew these:
    //the boundaries wander, every row of plots is shifted along on its own, and no plot grows evenly.
    float villageD = length(p - VillageXZ);
    [branch]
    if (villageD < FIELDS_REACH)
    {
        float2 rel = p - VillageXZ;
        float2 q = float2(dot(rel, PLOT_AXIS), dot(rel, float2(-PLOT_AXIS.y, PLOT_AXIS.x)))
            + float2(CloudNoise(p * 0.011 + 2.2), CloudNoise(p * 0.011 + 9.9)) * 10.0;
        float row = floor(q.y / PLOT_DEPTH);
        float2 cellF = float2(q.x / PLOT_LENGTH + Hash21(float2(row, 3.7)), q.y / PLOT_DEPTH);
        float2 cell = floor(cellF);
        float2 f = frac(cellF);
        float2 edge = min(f, 1.0 - f) * float2(PLOT_LENGTH, PLOT_DEPTH);
        //A hedge is a metre wide: past the distance it resolves it widens to the pixel and dims in proportion
        float hedgeWidth = 0.8 + footprint * 0.5;
        float hedge = (1.0 - smoothstep(hedgeWidth, hedgeWidth + 0.8 + footprint, min(edge.x, edge.y))) * (0.9 / (0.9 + footprint * 0.25));

        float kind = Hash21(cell + 17.0);
        float3 plot = kind < 0.28 ? CropColor : kind < 0.48 ? SoilColor : kind < 0.7 ? StubbleColor : kind < 0.86 ? PastureColor * 1.12 : PastureDry;
        float furrows = (kind < 0.48 ? 0.5 : 0.0) * sin(q.y * 4.2) * saturate(1.0 - footprint * 1.6);
        plot *= (1.0 + 0.3 * furrows) * (0.82 + 0.36 * saturate(0.5 + CloudNoise(p * 0.05 + cell * 3.1)));

        float fields = (1.0 - smoothstep(FIELDS_REACH * 0.55, FIELDS_REACH, villageD + 45.0 * patch))
            * smoothstep(VILLAGE_CORE, VILLAGE_CORE + 25.0, villageD)
            * smoothstep(RiverHalfWidth + 14.0, RiverHalfWidth + 30.0, d)
            * smoothstep(FOREST_FROM, FOREST_FROM + 40.0, outside);
        albedo = lerp(albedo, lerp(plot, HedgeColor, hedge), fields);
    }

    //The woods' floor, and the dark of a wooded hill past the planting
    float forest = ForestCover(p, outside, d, villageD);
    albedo = lerp(albedo, ForestFloorColor * (0.8 + 0.4 * saturate(0.5 + patch)), forest);

    //The high ground: heath on the ranges with the woods thinning into it, scree where they are steep, and the dormant
    //cone bare above its own woods
    float high = smoothstep(55.0, 95.0, rise + 18.0 * patch);
    albedo = lerp(albedo, HeathColor * (1.0 + 0.3 * patch), high * (1.0 - 0.6 * forest));
    albedo = lerp(albedo, ScreeColor, steep * high);
    float bare = 1.0 - smoothstep(DormantCone.z * 0.36, DormantCone.z * 0.6, length(p - DormantCone.xy) + 40.0 * patch);
    albedo = lerp(albedo, ScorchedColor * 1.8, bare);

    //The scorched band before the front: what the flow burned on its way, char with ash lying on it in drifts, its far
    //line broken into the grass rather than ruled round the field
    float scorch = 1.0 - smoothstep(SCORCH_FROM, SCORCH_TO, outside + 26.0 * CloudNoise(p * 0.013 + 3.3) + 9.0 * patch);
    float ash = smoothstep(-0.1, 0.35, CloudNoise(p * 0.045 + 8.2));
    albedo = lerp(albedo, lerp(ScorchedColor, AshColor, 0.6 * ash), scorch);

    //The lane along the river to the village, on its bank, a fixed way off the river's course (not off its meanders:
    //a lane runs straight where the water swings)
    [branch]
    if (abs(along - VillageRiver.x) < LANE_SPAN)
    {
        float course = across + MEANDER_A1 * sin(along * MEANDER_K1 + MEANDER_P1) + MEANDER_A2 * sin(along * MEANDER_K2 + MEANDER_P2);
        float lane = (1.0 - smoothstep(LANE_HALF, LANE_HALF + 0.6 + footprint, abs(course - VillageRiver.y * LANE_OFFSET)))
            * (1.0 - smoothstep(LANE_SPAN * 0.6, LANE_SPAN, abs(along - VillageRiver.x)));
        albedo = lerp(albedo, LaneColor * (0.85 + 0.3 * tussock), lane * (1.0 - steep));
    }

    //The gravel bars beside the water, pale and uneven
    float bar = 1.0 - smoothstep(RiverHalfWidth + 3.0, RiverHalfWidth + 10.0, d + 3.5 * CloudNoise(p * 0.08 + 5.5));
    albedo = lerp(albedo, GravelColor * (0.85 + 0.3 * tussock), bar);

    float3 color = albedo * light;

    //--- The water ---------------------------------------------------------------------------------------
    //Deep and nearly black under its reflection, which is the whole of how a river reads at dusk: a bright line of
    //the sky laid in the dark land. Wind ripples in two drifting octaves break it, flattened to a mirror as they near
    //the pixel; the sky is the dome as it is drawn (FarField.fxh's table) where the scene hands one over; and where the
    //reflection looks at the crater it carries the eruption's red, as every reference looking back over water does.
    [branch]
    if (d < RiverHalfWidth + 1.0)
    {
        float3 view = normalize(CameraPosition - worldPosition);
        float wet = 1.0 - smoothstep(RiverHalfWidth - 0.4 - footprint * 0.5, RiverHalfWidth + footprint * 0.5, d);
        float3 r1 = CloudNoiseD(p * 0.3 + VolcanoTime * float2(0.04, 0.07));
        float3 r2 = CloudNoiseD(p * 1.1 - VolcanoTime * float2(0.09, 0.02));
        float2 slope = (r1.yz * 0.3 + r2.yz * (1.1 * 0.35)) * (0.12 * saturate(1.0 - footprint * 0.6));
        float3 waterNormal = normalize(float3(-slope.x, 1.0, -slope.y));
        float3 reflected = reflect(-view, waterNormal);
        float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(waterNormal, view)), 5.0);

        //The ripples' slopes throw a river's reflection down towards the horizon's glow (a calm river at dusk is a band of
        //the brightest sky there is), so it is read a little lower than the mirror direction
        float lookUp = reflected.y * 0.7;
        float3 sky = lerp(HorizonColor, ZenithColor, saturate(lookUp * 2.5));
        [branch]
        if (FarFade.y > FarFade.x)
            sky = lerp(FarSkyAt(lookUp), ZenithColor, saturate((lookUp - 0.3) * 2.0));

        float3 crater = float3(ConeCenterXZ.x, VolcanoLevelY + ConeHeight - CraterDepth * 0.5, ConeCenterXZ.y);
        float fire = pow(saturate(dot(reflected, normalize(crater - worldPosition))), 90.0);

        float3 water = WaterColor * light + sky * fresnel + lerp(LavaCool, LavaHot, 0.2) * (fire * WaterFire);
        color = lerp(color, water, wet);
    }

    return color;
}

//Whether a pixel is shaded at all: not where the far fade has all but finished (FarField.fxh), where the ground is the
//sky behind it to within a hundredth whatever is shaded there (#748: the land's ranges stand where the dome was). Worked
//out again at each branch rather than held, since anything held is live through the field's branch, the register peak.
bool Shaded(float3 worldPosition)
{
    return FarFade.y <= FarFade.x || smoothstep(FarFade.x, FarFade.y, length(CameraPosition - worldPosition)) < SKY_TAKES_OVER;
}

float4 VolcanoSurface(VolcanoVertexOutput input, uniform bool fullDetail)
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);
    FarRingClip(worldPosition.xz);

    float footprint = length(fwidth(worldPosition.xz));
    float3 baseNormal = normalize(input.WorldNormal);


    //Where the field gives way to the land past it (#748): the distance past the field's edge off the vertex shader,
    //made ragged per pixel near the edge (a flow's margin is broken blocks, not a curve). The front's face is the
    //field's rubble down to about its foot; the land's own ground starts there.
    float outside = input.Country.x;
    [branch]
    if (abs(outside) < RAGGED_BAND)
        outside += RAGGED * (GradientNoise2(worldPosition.xz * 0.21 + 2.7) + 0.5 * GradientNoise2(worldPosition.xz * 0.63 + 6.1));
    float landShare = smoothstep(LAND_FROM, LAND_TO, outside);
    float rubble = smoothstep(-7.0, -2.0, outside) * (1.0 - landShare);

    //A fine relief so the near scoria reads as broken clinker rather than as a smooth floor. Combed, because
    //isotropic noise has no grain and reads as gravel (the savanna's #117 lesson) - and the grain runs ACROSS
    //the way the flow that laid it was moving, which is how pahoehoe folds into ropes. Until #509 it ran
    //downhill, which the sheen then drew as a brushed-metal field of streaks converging on the cone.
    //The reduced program (#540) has none: the combed fBm and the derivative normal behind it were 0.5 ms of the APU's Low
    //frame, and at 1600×900 the ropes it draws are a few pixels wide beyond the island's own edge.
    //The field's alone since #748, faded out across the front so the derivative below meets no step: the land past it
    //tilts its light by a grain of its own (CountrySurface).
    float relief = 0.0;
    if (fullDetail)
    {
        [branch]
        if (landShare < 1.0 && Shaded(worldPosition))
        {
            //Across the bearing from the cone: Fbm2Combed normalises the direction itself
            float2 fromCone = worldPosition.xz - ConeCenterXZ;
            relief = Fbm2Combed(worldPosition.xz * 0.55, float2(-fromCone.y, fromCone.x), 1.8, 4, footprint * 0.55) * 0.5 * (1.0 - landShare);
        }
    }
    float3 normal = fullDetail ? PerturbNormalFromHeight(baseNormal, worldPosition, relief) : baseNormal;

    //--- Lighting ----------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows (#471), into the same sunlight factor the clouds dim, so everything read off it
    //is shadowed at once. What casts here: the island and the gun on the flank.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

    //The field and the land, each behind a data branch on where the pixel stands: a pixel of the field pays nothing
    //for the land past it, and one of the land nothing for the flows, the cracks and the sheen it has none of. ⚠ What
    //either needs it works out itself from the interpolants (FieldSurface, CountrySurface): every value the land's branch
    //takes in is held live through the field's, which is this program's register peak, and the pass is occupancy-bound
    float3 color = 0.0;
    [branch]
    if (landShare < 1.0 && Shaded(worldPosition))
        color = FieldSurface(input, footprint, normal, relief, sunlight, rubble, fullDetail);
    [branch]
    if (landShare > 0.0 && Shaded(worldPosition))
        color = lerp(color, CountrySurface(input, footprint, sunlight), landShare);

    //Haze, and it is the dome's horizon TINTED DOWN rather than the horizon itself. Every other terrain
    //scene here stands under a sky whose horizon is roughly its own ground's tone, so lerping to it is
    //aerial perspective; black basalt under a cream horizon at the desert's haze distance came out as a
    //SAND DUNE, cone and all. An ash pall is what is actually in this air.
    //Past the field's edge the pall thins into the land's own air (#748), over PALL_THINS: see LandHazeColor.
    float3 hazeColor = HorizonColor * HazeTint;
    hazeColor = lerp(hazeColor, LandHazeColor, landShare * smoothstep(0.0, PALL_THINS, input.Country.x));

    float haze = saturate(length(CameraPosition - worldPosition) / HorizonHazeDistance);
    color = lerp(color, hazeColor, haze * haze * HazeStrength);

    return float4(FarFadeToSky(color, worldPosition), 1.0);
}

//Two programs from one body, the idiom Forest.fx established (#298). "Volcano" is the authored flank;
//"VolcanoReduced" is the same flank without its two kinds of hairline off the flows - the rivulets down the
//cone and the cracks in the field - which arrived together in #509 and are given up together, because a lone
//reduction buys nothing on a pass that is occupancy-bound (SceneRenderer.SceneDetail). What the scene is made
//of stays on every tier: the massing, the flows with their crust and streamlines, the crater's lake, the
//sheen and the haze.
//
//#540 took the reduced program further, because the volcano was the one scene still missing Low's budget on the
//APU and the layers #540 separated said it was the flank and nothing else (the plume, the jets, the blaze and
//the ash measured at nothing): the fine relief and its derivative normal, an octave of the rock's
//patches, the noise in each flow's front (FrontLobes) and two octaves of the scoria in the vertex program, over a
//coarser grid (SceneRenderer.VOLCANO_GRID_N_REDUCED). Measured paired in the Testbed at Low's settings, the play
//pose over Caldera: 14.89 -> 11.82 ms, cheaper in every one of 40 cycles.
float4 VolcanoPS(VolcanoVertexOutput input) : COLOR { return VolcanoSurface(input, true); }
float4 VolcanoReducedPS(VolcanoVertexOutput input) : COLOR { return VolcanoSurface(input, false); }

technique Volcano
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL VolcanoVS();
        PixelShader = compile PS_SHADERMODEL VolcanoPS();
    }
};

technique VolcanoReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL VolcanoVSReduced();
        PixelShader = compile PS_SHADERMODEL VolcanoReducedPS();
    }
};

//--- The height probe (#590) ----------------------------------------------------------------------------

//TerrainMirror.Volcano's field for the Testbed's mirrorcheck (see HeightProbe.fxh): the massing without the
//scoria, which that mirror leaves out by design - no octaves is exactly that - and, beside it, the full
//program's four-octave ground the vertex shader actually displaces by, so the check can say how far the
//omission puts the mirror off the drawn surface as well.
#define HEIGHT_PROBE_MIRRORED(p) TerrainHeight(p, 0)
#define HEIGHT_PROBE_DRAWN(p) TerrainHeight(p, 4)
#include "HeightProbe.fxh"
