//Everything planted on the savanna, as REAL 3D geometry (#202, #451). Each plant is an instanced procedural
//mesh - a bark trunk under wide flat tiers of foliage (AcaciaMesh), a low rounded clump for a bush, a spiked
//clump for a bunch of grass, a fluted spire for a termite mound, a boulder, a fallen log (SavannaScatter) -
//shaded from the scene's own sun and dome exactly as the terrain is (Savanna.fx), so a tree sits in the
//savanna's light rather than being pasted over it. It replaces the flat billboard that read as a paper
//cutout: a surface of revolution has volume from every angle, where a camera-facing quad only ever shows one
//silhouette. One instanced draw per mesh variant per material - DiffuseColor/DiffuseDry, DappleStrength and
//BarkStrength are the per-draw material: dappled green foliage for a canopy, fissured brown for a trunk,
//plain for a stone (and, on High and Ultra's rocks, a stone surface of its own: StoneStrength below, #777). The
//per-instance world matrix rides in a second vertex stream (TEXCOORD1-4), like
//InstancedModel.fx, and the instance's Custom vector (TEXCOORD5) carries its own dryness and brightness,
//so one mesh variant reads as several plants by colour as well as by size (#451).
//Testbed-shared (the map editor and the game build it too), Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//For the canopy's leaf mottle and the bark's fissures - 3D fields of world position, so they have no seam
//and do not swim as the plant turns with its instance, the same reasoning the crown fields carry.
#include "Noise.fxh"
//The sun's cast shadows (#469): every plant receives them, and the ShadowCaster technique below is how
//every plant is drawn INTO the map.
#include "Shadows.fxh"
//The cloud deck's shadow on the sun (#748), for the one draw that asks for it. PlantPass zeroes the deck's gain before
//every draw (the savanna's own draw does too), so every planted scene's plants stand in full sun as they always have and
//CloudSunlight answers a flat 1 without evaluating anything; the volcano's land hands it the frame's clouds, because under
//its storm deck the ground gets a sixth of the sun, and a wood in full sun on that ground stood out as pale cones.
#include "Clouds.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;     //for the horizon haze, the same distance the ground under the plant melts over

//Towards the sun, dotted with the surface normal exactly as Savanna.fx does it, and the dome's ambient split
//zenith-to-horizon by the normal's height - so the acacia takes the same light the ground under it does.
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//The per-draw material: the diffuse colour, the drier shade an instance's dryness leans it towards, how
//strongly the leaf mottle breaks it up (0 on a trunk) and how strongly the bark's fissures do (0 on foliage).
float3 DiffuseColor;
float3 DiffuseDry;
float DappleStrength;
float BarkStrength;

//1 for the acacias' leaf-spray cards (#610), 0 for every other mesh: the leaflets are cut out of each card here,
//both of its sides are lit, and the sun comes through it. See LeafMask and AcaciaMesh's LeafSprays. 2 for the
//meadow's old trees (#609's third round), whose cards are twigs of broad leaves instead - see BroadLeafMask. 3 for the
//tropical beach's sea grapes (#753): the same twigs, the leaves round. 4 for the savanna's acacias and thorn bushes
//(#964): twiglets of twice-pinnate leaves - see TuftMask.
float LeafStrength;

//The distance the plant melts into the skyline over - Savanna.fx's own HorizonHazeDistance, handed over so a
//far tree fades the way the ground it stands on does. Without it the treeline stood in full colour on a
//hazed hillside, which is most of what made the far scatter read as pasted on (#451). It is stretched by
//PLANT_HAZE_REACH below: at the ground's own distance the treeline at 300-400 out came back as pale lumps on
//a pale hill, and the references keep a far wooded edge darker than the field under it - a dark object in
//aerial perspective stays darker than a bright one beside it.
float HorizonHazeDistance;
static const float PLANT_HAZE_REACH = 1.5;

//What that haze is made of: the dome's horizon for every scene but one (PlantPass states it with the horizon, the savanna's
//own draw states it too), and the volcano's ash pall for the land past its lava field (#748) - Volcano.fx hazes its ground
//to the horizon tinted DOWN, and a wood hazed to the bright horizon over that ground stood on it as pale cones.
float3 HazeColor;

//Light this draw carries that the scene's own sun and dome do not account for, added flat after them. It is
//zero for every plant and non-zero for two things. The campfires' hearth stones (#282), which stand in a ring
//at one distance from one fire, so what a point light would work out per pixel is a constant per draw here -
//and it flickers, because SceneRenderer hands over the fire's own colour at this frame. It buys the fire pit
//its firelight for one add and no loop; what it does not do is favour the side of a stone that faces the
//flame. And the volcano's village windows (#748, ScatterBucket.Emissive): lamps behind glass, the light of
//their own.
float3 AddedLight;

//The leaf mottle's world-space frequency and its mean (a canopy is dappled foliage, not a flat green mass).
static const float DAPPLE_FREQUENCY = 0.55;
static const float DAPPLE_MEAN = 0.86;

//The bark's fissures: a world-space field stretched along Y so its features run up the trunk (and up a
//termite mound's flutes), and the gain that turns a field of about +-0.25 into ridges and grooves.
static const float BARK_FREQUENCY = 1.6;
static const float BARK_STRETCH = 0.22;
static const float BARK_GAIN = 2.6;

//The bark's weathering (#610, the references' trunks): pale grey-green lichen in patches, and the wood darker
//near the ground, dusted and wetted where it meets the soil. Both scale with BarkStrength, so a termite
//mound's flutes (0.45) take less of them than a trunk's (0.6) and stone and foliage none.
static const float LICHEN_FREQUENCY = 0.9;
static const float LICHEN_COVER = 0.18;          //how far above its mean the field must rise to carry lichen
static const float3 LICHEN_COLOR = float3(0.19, 0.20, 0.145);  //a little paler and greyer than the bark, not white: at 0.36 it read as snow
static const float LICHEN_AMOUNT = 0.6;
static const float FOOT_HEIGHT = 1.6;            //over the plant's root, in world units
static const float FOOT_DARKEN = 0.35;

//The ground's own bare earth carried up the foot (#670): the savanna hands over its GrassColorBare, linear, and
//black is none — the plant pass other scenes draw through shares this effect and sets it black. Over about
//EARTH_HEIGHT above the root, its line broken by one noise octave, at most EARTH_AMOUNT of the way to the earth.
float3 FootEarth;
static const float EARTH_HEIGHT = 0.9;
static const float EARTH_AMOUNT = 0.8;

//--- STONE (#777). 0 on every plant; on a rock that has a real surface (the fine blocks and boulders High and Ultra plant
//on the Moon and the volcano, FracturedRockMesh) how deep that surface goes. The owner, of the Moon's rocks: "triangular
//rocks... rocks on the Moon do not really look like that", and the klein references say what is missing: no face of a rock
//is one flat tone. So the normal is tilted by a relief (dents across the rock at its own size, a grain and pits at the
//world's), the colour is mottled in patches, darkened in the relief's hollows, flecked and crossed by a few hairline
//cracks, and the foot and the top are dusted with the ground's own colour (FootDust, through FootEarth: black is none).
//Every octave is faded out as its period nears two pixels (StoneOctave), so a far rock goes to its mean, not to a crawl.
//Uniform branch: one side for every pixel of a draw, so the derivatives inside are taken by every pixel of a quad alike.
float StoneStrength;

//--- THE BARK'S RELIEF (#980). 0 on everything but the meadow's oaks at High and Ultra (their fine wood's bucket carries
//it at 1, and that bucket is drawn only there). The owner: "tree bark on High/Ultra ... now it looks like a blurred texture",
//and BARK above is an albedo multiplication only - no normal, nothing in shade. So the bark gets a surface: thick plates
//parted by fissures that run up the trunk, from a noise's zero set (a winding network of lines: wrong for a lawn, #944,
//right for bark); the normal tilted by it and the fissures in their own
//shade, the plates' faces a little paler with weathering. Every octave fades as its period nears a few pixels, so a far
//trunk keeps the plain bark. Stretched up world Y as BARK is (AcaciaMesh: the bark stays the world's), and harder: the
//references (980-klein, 980-zimage) draw an oak's furrows long, dense and nearly parallel. Uniform branch.
//
//THE SECOND CUT (the owner's verdict on the first: "it needs a larger detail of the bark, at least on high/ultra"). One
//octave of broad plates read as wavy wood grain beside the references, which draw MANY narrow, deep furrows, the ridges
//between them broken across into plates by cracks, the plates' faces rough, and pale lichen sitting on them. So: two
//octaves of furrows (the second finer, inside the plates), the ridges broken into blocks of different heights (the steps
//between them are the cracks: ⚠ drawn as a noise's zero set, the cracks were thin wiggly loops, doodles on the bark), a
//grain on the faces and lichen, every one faded as its period nears a few pixels, the furrows far darker than the first
//cut's.
float BarkRelief;
static const float BARK_RELIEF_FREQUENCY = 9.0;  //furrows across the trunk per world unit; up it BARK_RELIEF_STRETCH times that
static const float BARK_RELIEF_STRETCH = 0.09;   //the references' furrows run about eleven times as long as they are apart
static const float BARK_RELIEF_DEPTH = 0.06;     //the plates' height over the furrows, world units
static const float BARK_FINE_FREQUENCY = 23.0;   //the finer furrows inside the plates
static const float BARK_BLOCK_ALONG = 2.2;       //the blocks a ridge is broken into, per world unit up the trunk (a plate several
static const float BARK_BLOCK_ACROSS = 4.0;      //furrows long) and across it (two or three ridges: squashed further across, the
                                                 //steps lined up round the trunk as fine horizontal stripes)
static const float BARK_GRAIN_FREQUENCY = 70.0;  //the roughness of a plate's face
static const float BARK_LICHEN_FREQUENCY = 1.2;  //the lichen's patches
static const float3 BARK_LICHEN = float3(0.36, 0.42, 0.33);   //pale grey-green, linear

//--- STRAW (#971). 0 on everything but the meadow's round hay bales, which carry it at 1. The owner: "on the bottom and top
//side of the cylinder there is not that typical tangle/swirl - we need to think about how a cylindrical bale of straw is
//formed". A round baler rolls the windrow up from a loose core outward and ties the roll round, so an END shows the layers
//as a spiral round a darker, looser core, the straws laid along the turns and loose ends at the rim; the SIDE shows straws
//running round the bale in clumps (the twine that ties a real one read as stripes, and the owner ruled them out). Painted
//on BaleGeometry's own UV: the side's u 0..1 round the bale
//and v 0..1 along it, an end's u 2..3 round it (CAP_U) and v 1 at the rim to 0 at its centre. Every term fades to its mean
//as its period nears two pixels. Uniform branch, as STONE's, so the derivatives inside are taken by every pixel alike.
float StrawStrength;
static const float STRAW_LAYERS = 9.0;           //turns of the spiral from the core to the rim
static const float STRAW_SIDES = 32.0;           //BaleGeometry.SIDES: the end is a 32-gon, its v linear across each sector
static const float STRAW_CORE = 0.2;             //the loose core's radius, as a share of the end's
static const float STONE_DENTS = 2.3;            //the dents across a rock, per its own size: a fracture face is never flat
static const float STONE_DENT_DEPTH = 0.06;      //as a share of the rock's size
static const float STONE_GRAIN = 3.2;            //the rough grain, per world unit
static const float STONE_GRAIN_DEPTH = 0.04;     //world units, on a rock a unit or more across
static const float STONE_PITS = 14.0;            //the pits (vesicles), per world unit: the references' face is pocked with them
static const float STONE_PIT_DEPTH = 0.015;
static const float STONE_PATCHES = 1.4;          //the mottle, per the rock's size
static const float STONE_MOTTLE = 0.6;           //how far the patches lift and lower the colour
static const float STONE_CAVITY = 0.8;           //how much darker a hollow of the relief is than its rim
static const float STONE_FLECKS = 40.0;          //bright mineral flecks, per world unit, and how bright
static const float STONE_FLECK_GAIN = 0.3;
static const float STONE_CRACKS = 1.6;           //hairline fractures, per the rock's size, and their half-width in the field's units
static const float STONE_CRACK_HALF = 0.025;
static const float STONE_DUST_LINE = 0.16;       //how high the foot's dust climbs, as a share of the rock's size
static const float STONE_TOP_DUST = 0.4;         //how much of the dust lies on the faces that look up
static const float STONE_MEAN = 0.88;            //the stone's mean shade, so the mottle lifts as much as it lowers

//How much of an octave of `frequency` (per world unit) a pixel `footprint` world units wide keeps: all of it while a period
//spans four pixels, none once it spans two (its Nyquist limit)
float StoneOctave(float footprint, float frequency) { return saturate(2.0 - 4.0 * footprint * frequency); }

//--- THE LEAF SPRAYS (#610). A card's texture coordinate runs X from the spray's stem to its tip and Y across it,
//plus twice its layer (0 on top of the crown, 1, 2 underneath). The references: a crown of thin flat tiers of
//tiny compound leaves, the sky showing between them, back-lit yellow-green towards the sun, dark underneath.
static const float PI = 3.14159265;
static const float LEAFLET_PAIRS = 11.0;      //leaflets along each side of a spray
static const float LEAF_STALK = 0.12;         //the bare stalk at the stem end, as a share of the length
static const float MIDRIB_HALF = 0.07;        //the rachis, as a share of the half-width
//How much darker the ambient is on the layers under the top one: the inside of an umbrella is its shade
static const float LAYER_AMBIENT_FALL = 0.45;
//The sun through a leaf when the lens looks towards the sun: its colour against the albedo, and how tightly
//it gathers round the sun's own direction
static const float3 TRANSMISSION_TINT = float3(1.35, 1.55, 0.45);
static const float TRANSMISSION_POWER = 3.0;
static const float TRANSMISSION_GAIN = 0.9;
//Leaves wrap the sun round their edge a little, as a thin sheet does
static const float LEAF_WRAP = 0.4;

float LeafHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

//Signed coverage of one spray card at uv: above zero where a leaflet (or the rachis) is. Past the resolution of
//its leaflets - a pixel wider than half of one - it turns into the spray's own solid outline, which is what a
//spray is from far away, rather than aliasing into sparkle.
float LeafMask(float2 uv, float2 seed)
{
    float layer = floor(uv.y * 0.5);
    float v = uv.y - 2.0 * layer;
    float across = abs(v - 0.5) * 2.0;                     //0 on the rachis, 1 at the card's edge

    float along = saturate((uv.x - LEAF_STALK) / (1.0 - LEAF_STALK));
    //The spray's envelope: widest past the middle, closing to the tip and to the stalk
    float envelope = sin(PI * saturate(along * 0.92 + 0.08)) * step(LEAF_STALK, uv.x);

    float cell = along * LEAFLET_PAIRS;
    float f = frac(cell) - 0.5;
    float id = floor(cell);

    //One leaflet each side of the rachis in every cell: an ellipse from the rachis out to the envelope
    float halfLength = max(envelope * 0.5, 1e-3);
    float lx = f / 0.42;
    float ly = (across - halfLength) / halfLength;
    float leaflet = 1.0 - (lx * lx + ly * ly);

    //A few missing, as a real spray has them
    float side = step(0.5, v);
    leaflet -= 2.0 * step(0.88, LeafHash(seed + float2(id, side)));

    //Nothing at all before the stalk's own start: a term multiplied to ZERO there is not a cut, since clip() keeps a 0 -
    //it kept a strip a fiftieth of the card long across its whole width at every spray's base (#697)
    float rachis = uv.x >= 0.02 ? (MIDRIB_HALF - across) * 8.0 : -1.0;
    float fine = max(leaflet, rachis);

    //The spray as one outline: the same envelope, filled
    float coarse = envelope * 0.9 - across;

    float blur = fwidth(cell);
    return lerp(fine, coarse, smoothstep(0.35, 0.8, blur));
}

//--- BROAD LEAVES (#609's third round). The meadow's old trees wear the same cards round their clumps (MeadowTreeMesh),
//LeafStrength 2, and a card is a twig of broad leaves rather than a spray of leaflets: eight leaves up a stem, turned
//out from it on alternate sides, and one at its tip, each an ellipse. The card is square, so its texture space is
//the world's and a leaf is the shape it is drawn. Past the leaves' resolution it turns into the twig's filled round,
//as a spray turns into its outline.
static const float BROAD_LEAF_HALF_LENGTH = 0.13;  //as a share of the card
static const float BROAD_LEAF_HALF_WIDTH = 0.065;
static const float BROAD_STEM_HALF = 0.015;
static const int BROAD_LEAVES = 9;

//ROUND LEAVES (#753): LeafStrength 3, the tropical beach's sea grape. The same twig, its leaves as wide as they are long:
//the round, leathery leaves the references (753-klein) draw on a sea grape's red stems
static const float ROUND_LEAF_HALF = 0.11;

float BroadLeafMask(float2 uv, float round)
{
    float halfLength = lerp(BROAD_LEAF_HALF_LENGTH, ROUND_LEAF_HALF, round);
    float halfWidth = lerp(BROAD_LEAF_HALF_WIDTH, ROUND_LEAF_HALF, round);
    float layer = floor(uv.y * 0.5);
    float2 p = float2(uv.x, uv.y - 2.0 * layer - 0.5);     //x along the stem 0..1, y across it -0.5..0.5
    //The stem, and nothing past its end. It was this times step(p.x, 0.74), which is ZERO beyond the stem, not negative,
    //and clip() keeps a 0: the whole tip quarter of every card drew solid across its full width, the "rectangles" of
    //#697 - a twig with a flat strip stuck to its end, and on its own, seen edge-on, a paper scrap
    float fine = p.x <= 0.74 ? (BROAD_STEM_HALF - abs(p.y)) * 20.0 : -1.0;

    [unroll]
    for (int i = 0; i < BROAD_LEAVES; i++)
    {
        //Eight on alternate sides up the stem, leaning out towards the tip and shorter towards it; the last straight on
        bool tip = i == BROAD_LEAVES - 1;
        float side = tip ? 0.0 : ((i & 1) ? -1.0 : 1.0);
        float2 dir = tip ? float2(1.0, 0.0) : float2(0.55, 0.835 * side);
        float2 centre = float2(tip ? 0.74 : 0.08 + 0.08 * i, 0.0) + dir * halfLength;
        float2 d = p - centre;
        float2 q = float2(dot(d, dir), dot(d, float2(-dir.y, dir.x)));
        fine = max(fine, 1.0 - (q.x * q.x) / (halfLength * halfLength) - (q.y * q.y) / (halfWidth * halfWidth));
    }

    float coarse = (0.4 - length(p - float2(0.5, 0.0))) * 2.5;
    float blur = fwidth(p.x) * 9.0;
    return lerp(fine, coarse, smoothstep(0.35, 0.8, blur));
}

//--- THE ACACIA'S TUFTS (#964): LeafStrength 4, the savanna's acacias and thorn bushes. Every reference crown is a flat mat
//of fine, spiky foliage, because an acacia's leaf is twice pinnate: a stalk carrying pairs of thin pinnae, each fringed
//with leaflets a few millimetres long. LeafMask's frond, eleven broad leaflets a side on a card a metre long, read as a
//fern. A card here is a twiglet: its stalk, TUFT_PAIRS pairs of thin pinnae leaning forwards off it, ragged in length,
//fringed with leaflets while a pixel can hold them, a plain strip once it cannot, and the spray's outline once the pinnae
//themselves are finer than a pixel. The stalk runs on past the last pair as a spike.
static const float TUFT_ASPECT = 0.45;            //AcaciaMesh's SPRAY_ASPECT: the card's width against its length
static const float TUFT_PAIRS = 10.0;             //pairs of pinnae up the stalk
static const float TUFT_FIRST = 0.1;              //where the first pair leaves the stalk, as a share of the card's length
static const float TUFT_STEP = 0.08;              //and the next ones, so the last leaves at 0.82
static const float TUFT_SIN = 0.766;              //the pinnae lean 50 degrees off the stalk: its sine and cotangent
static const float TUFT_COT = 0.839;
static const float TUFT_PINNA_HALF = 0.027;       //a pinna's half-width with its leaflets, in the card's lengths
static const float TUFT_LEAFLET = 0.014;          //a leaflet's spacing along its pinna
static const float TUFT_RIB_HALF = 0.004;         //a pinna's own rib
static const float TUFT_STALK_HALF = 0.011;       //the stalk at the stem, tapering to the spike's point

float TuftMask(float2 uv, float2 seed)
{
    float layer = floor(uv.y * 0.5);
    float v = uv.y - 2.0 * layer;
    //The card in its own lengths: x up the stalk, s off it to either side
    float x = uv.x;
    float y = (v - 0.5) * TUFT_ASPECT;
    float s = abs(y);

    //The pair whose pinna passes nearest: every pinna of a side is parallel, so the point on the stalk a pinna through
    //this pixel would leave from says which one it is
    float from = x - s * TUFT_COT;
    float slot = (from - TUFT_FIRST) / TUFT_STEP;
    float id = clamp(floor(slot + 0.5), 0.0, TUFT_PAIRS - 1.0);
    float off = (slot - id) * TUFT_STEP * TUFT_SIN;        //square off that pinna's line
    float t = s / TUFT_SIN;                                 //along it, from the stalk

    //Its length: the most the card's half-width allows, shorter near the stem and towards the tip, ragged, and a few missing
    float at = TUFT_FIRST + id * TUFT_STEP;
    float envelope = sin(PI * saturate(at * 0.95 + 0.05));
    float side = step(0.5, v);
    float roll = LeafHash(seed + float2(id, side) * 1.7);
    float reach = (0.5 * TUFT_ASPECT / TUFT_SIN) * envelope * (0.6 + 0.4 * roll) * step(roll, 0.93);
    float taper = 1.0 - 0.55 * saturate(t / max(reach, 1e-3));

    //The leaflets: an ellipse across the rib at every step along it, so a pinna is a string of them
    float lf = frac(t / TUFT_LEAFLET) - 0.5;
    float half_ = TUFT_PINNA_HALF * taper;
    float leaflets = 1.0 - (lf * lf) / (0.48 * 0.48) - (off * off) / (half_ * half_);
    leaflets = max(leaflets, (TUFT_RIB_HALF - abs(off)) / TUFT_RIB_HALF);
    //Past the leaflets' resolution the pinna is a strip of their mean width
    float strip = (0.8 * half_ - abs(off)) / half_;
    float pinna = lerp(leaflets, strip, smoothstep(0.3, 0.6, fwidth(t / TUFT_LEAFLET)));
    pinna = min(pinna, (reach - t) / TUFT_PINNA_HALF);

    //The stalk, from just past the stem's own start to the spike's point (nothing before it: clip() keeps a 0, #697)
    float stalkHalf = TUFT_STALK_HALF * (1.0 - 0.85 * x);
    float stalk = x >= 0.02 && x <= 0.98 ? (stalkHalf - s) / stalkHalf : -1.0;
    float fine = max(pinna, stalk);

    //The spray as one outline, as LeafMask's, once the pinnae are finer than a pixel
    float across = s / (0.5 * TUFT_ASPECT);
    float coarse = sin(PI * saturate(x * 0.92 + 0.08)) * step(0.05, x) * 0.85 - across;
    return lerp(fine, coarse, smoothstep(0.35, 0.8, fwidth(from / TUFT_STEP)));
}

//Which cut a card takes: the draw says, one side for every pixel of it, so the derivatives inside either are
//taken by every pixel of a quad alike
float CardMask(float2 uv, float2 seed)
{
    //One return: an inlined early return inside a branch is the X4000 pattern Clouds.fxh records
    float mask;
    [branch]
    if (LeafStrength > 3.5)
        mask = TuftMask(uv, seed);
    else if (LeafStrength > 1.5)
        mask = BroadLeafMask(uv, step(2.5, LeafStrength));
    else
        mask = LeafMask(uv, seed);
    return mask;
}

struct AcaciaVertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 World1 : TEXCOORD1;   //per-instance world matrix, row-major like InstancedModel.fx (no transpose)
    float4 World2 : TEXCOORD2;
    float4 World3 : TEXCOORD3;
    float4 World4 : TEXCOORD4;
    float4 Custom : TEXCOORD5;   //x: dryness 0..1 (towards DiffuseDry), y: brightness offset (-1..1 about 0),
                                 //z: the ground at the trunk's centre above the pivot (#670; 0 where nobody says)
    float2 UV : TEXCOORD0;       //read by the leaf sprays alone (#610); every savanna mesh carries one
};

struct AcaciaVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float2 Tint : TEXCOORD2;
    float2 UV : TEXCOORD3;
    float2 Seed : TEXCOORD4;     //the instance's own place, so two trees do not lose the same leaflets
    float RootY : TEXCOORD5;     //the ground at the instance's foot, for the bark's earth and darker base (#610, #670)
    float Scale : TEXCOORD6;     //the instance's uniform scale: a rock's size, which its stone is laid out in (#777)
};

AcaciaVertexOutput AcaciaVS(AcaciaVertexInput input)
{
    AcaciaVertexOutput output;

    float4x4 world = float4x4(input.World1, input.World2, input.World3, input.World4);
    float4 worldPosition = mul(input.Position, world);

    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    //The instance transform is rotation + uniform scale + translation, so the plain matrix rotates the normal
    //(a uniform scale leaves it only needing a re-normalize).
    output.WorldNormal = normalize(mul(input.Normal, (float3x3)world));
    output.Tint = input.Custom.xy;
    output.UV = input.UV;
    output.Seed = input.World4.xz;
    //The ground the eye sees at the trunk, not the pivot: a tree planted at the lowest ground under its ring (#658)
    //stands with its pivot under the plain, and its foot measured from there went under the ground with it (#670)
    output.RootY = input.World4.y + input.Custom.z;
    output.Scale = length(input.World1.xyz);

    return output;
}

float4 AcaciaPS(AcaciaVertexOutput input, bool front : SV_IsFrontFace) : COLOR
{
    float3 N = normalize(input.WorldNormal);

    //The leaf sprays (#610): cut out, turned to the side seen, and the deeper in the crown the darker the
    //shade. Uniform branch: one side for every pixel of a draw, so the derivative inside LeafMask is taken
    //by every pixel of a quad alike.
    float layerShade = 1.0;
    float leaf = 0.0;
    [branch]
    if (LeafStrength > 0.0)
    {
        clip(CardMask(input.UV, input.Seed));
        N = front ? N : -N;
        layerShade = 1.0 - LAYER_AMBIENT_FALL * saturate(floor(input.UV.y * 0.5) * 0.5);
        leaf = 1.0;
    }

    //This instance's own shade of the draw's material: its dryness leans the colour towards the drier one,
    //its brightness offset lifts or lowers it, so a grove of one variant is not one green stamped out.
    float3 albedo = lerp(DiffuseColor, DiffuseDry, saturate(input.Tint.x)) * (1.0 + input.Tint.y);

    //A rock's own surface (#777): the relief tilts N before anything is lit, the mottle and the dust go into the albedo
    [branch]
    if (StoneStrength > 0.0)
    {
        float3 p = input.WorldPosition;
        float size = max(input.Scale, 1e-3);
        float3 q = p / size;
        float footprint = max(length(ddx(p)), length(ddy(p)));

        //The relief, in world units: dents laid out in the rock's own size, so a landmark's faces undulate as a pebble's do,
        //then the grain and the pits at the world's scale, shallower on a pebble than the pebble is
        float small = saturate(size * 1.5);
        float dentW = StoneOctave(footprint, STONE_DENTS / size);
        float grainW = StoneOctave(footprint, STONE_GRAIN) * small;
        //A pit's rim is several times sharper than its spacing: its tilt fades as an octave three times finer would, its shade
        //as one half again finer
        float pitW = StoneOctave(footprint, STONE_PITS * 3.0) * small;
        float pitShadeW = StoneOctave(footprint, STONE_PITS * 1.5);
        float dent = GradientNoise3(q * STONE_DENTS + 7.3);
        float grain = GradientNoise3(p * STONE_GRAIN + 1.9);
        float pit = smoothstep(0.26, 0.42, GradientNoise3(p * STONE_PITS + 4.7));   //round, where the field peaks
        float relief = dent * dentW * STONE_DENT_DEPTH * size + grain * grainW * STONE_GRAIN_DEPTH - pit * pitW * STONE_PIT_DEPTH;
        float3 geometric = N;
        N = PerturbNormalFromHeight(N, p, relief * StoneStrength);

        //The colour: patches across the rock, darker in the hollows, a few bright flecks, and hairline cracks where a second
        //field crosses zero (only where the patches allow, so a rock has a few, not a net)
        float patch = GradientNoise3(q * STONE_PATCHES + 11.7)
            + 0.5 * GradientNoise3(q * STONE_PATCHES * 3.1 + 5.3) * StoneOctave(footprint, STONE_PATCHES * 3.1 / size);
        float hollow = dent * dentW * 0.5 + grain * grainW * 0.35 - pit * pitShadeW * 0.6;
        float fleck = smoothstep(0.34, 0.44, GradientNoise3(p * STONE_FLECKS + 2.3)) * StoneOctave(footprint, STONE_FLECKS);
        float field = GradientNoise3(q * STONE_CRACKS + 3.1);
        float width = max(fwidth(field), 1e-5);
        float crack = saturate((STONE_CRACK_HALF - abs(field)) / width + 0.5) * saturate(2.0 * STONE_CRACK_HALF / width)
            * smoothstep(-0.1, 0.1, patch);
        float shade = STONE_MEAN * (1.0 + STONE_MOTTLE * patch) * (1.0 + STONE_CAVITY * hollow)
            * (1.0 + STONE_FLECK_GAIN * fleck) * (1.0 - 0.7 * crack);
        albedo *= lerp(1.0, shade, saturate(StoneStrength));

        //The ground's dust: up the foot to a line the dents wander, and on what faces the sky
        float above = (p.y - input.RootY) / size;
        float dustLine = STONE_DUST_LINE * (0.6 + 0.8 * saturate(0.5 + dent));
        float foot = 1.0 - saturate(above / dustLine);
        float top = saturate(geometric.y);
        top *= top;
        float dust = max(foot * foot * 0.85, top * top * STONE_TOP_DUST * saturate(0.6 + patch));
        albedo = lerp(albedo, FootEarth * (1.0 + input.Tint.y), dust * (any(FootEarth) ? 1.0 : 0.0));
    }

    //The bark's relief (#980): see THE BARK'S RELIEF above
    [branch]
    if (BarkRelief > 0.0)
    {
        float3 p = input.WorldPosition;
        float footprint = max(length(fwidth(p)), 1e-4);
        float3 stretch = float3(1.0, BARK_RELIEF_STRETCH, 1.0);

        //The furrows: bands about a noise's zero set, deep and narrow between flat-topped ridges, and the finer furrows
        //inside the plates, each faded out as its period nears a few pixels
        float plateSeen = saturate(1.6 - footprint * BARK_RELIEF_FREQUENCY * 3.0);
        float fineSeen = saturate(1.6 - footprint * BARK_FINE_FREQUENCY * 3.0);
        float ridge = smoothstep(0.0, 0.22, abs(GradientNoise3(p * stretch * BARK_RELIEF_FREQUENCY + 3.7)));
        float fine = smoothstep(0.0, 0.3, abs(GradientNoise3(p * stretch * BARK_FINE_FREQUENCY + 8.1)));

        //The ridges broken into blocks of their own heights
        float block = smoothstep(-0.12, 0.12, GradientNoise3(p * float3(BARK_BLOCK_ACROSS, BARK_BLOCK_ALONG, BARK_BLOCK_ACROSS) + 1.3));

        //A plate's face is rough; the grain only near the lens
        float grainSeen = saturate(1.6 - footprint * BARK_GRAIN_FREQUENCY * 3.0);
        float grain = grainSeen > 0.0 ? GradientNoise3(p * BARK_GRAIN_FREQUENCY + 5.9) * grainSeen : 0.0;

        float plate = lerp(1.0, ridge, plateSeen) * lerp(1.0, 0.55 + 0.45 * fine, fineSeen) * lerp(1.0, 0.6 + 0.4 * block, plateSeen);
        float height = (plate + 0.12 * grain) * BARK_RELIEF_DEPTH;
        N = PerturbNormalFromHeight(N, p, height * BarkRelief);

        //The furrows in deep shade, the plates' faces weathered paler, and lichen on them
        albedo *= lerp(1.0, lerp(0.32, 1.3, plate) * (1.0 + 0.12 * grain), BarkRelief);
        float lichen = smoothstep(0.15, 0.45, GradientNoise3(p * BARK_LICHEN_FREQUENCY + 2.2) + 0.25 * fine) * plate;
        albedo = lerp(albedo, BARK_LICHEN * (0.8 + 0.3 * grain), 0.75 * lichen * BarkRelief);
    }

    //A round bale's straw (#971): see STRAW above
    [branch]
    if (StrawStrength > 0.0)
    {
        float2 uv = input.UV;
        float3 p = input.WorldPosition;
        //The footprints before the branch an end and the side take, which a quad on the rim splits
        float2 uvFootprint = max(fwidth(uv), 1e-4);
        float relief, shade;
        [branch]
        if (uv.x > 1.5)
        {
            //An end. Its v is the share of the radius at the 18-gon's corners and linear across each sector, so along a
            //sector's chord it overstates the true radius by 1 / cos of the angle off the sector's middle: taken out, the
            //spiral's turns come out round instead of 18-sided.
            float turn = frac(uv.x);
            float offset = (frac(turn * STRAW_SIDES) - 0.5) * (6.2831853 / STRAW_SIDES);
            float r = uv.y * cos(3.1415927 / STRAW_SIDES) / cos(offset);
            float footprint = uvFootprint.y;

            //The spiral: one layer more for every turn round, so the phase is radius in layers less the turn. Each layer
            //bulges between two seams, the seams wander and come and go where the straw was fed in unevenly, and every
            //layer is its own swath of the windrow, a shade lighter or darker. Cut as clean as its first version, the
            //spiral read as a tree's rings.
            float2 polar = float2(cos(turn * 6.2831853), sin(turn * 6.2831853)) * r;
            float wobble = 0.3 * GradientNoise2(polar * 3.1 + 4.1) + 0.12 * GradientNoise2(polar * 9.3 + 1.7);
            float phase = r * STRAW_LAYERS - turn + wobble;
            float layer = frac(phase);
            float seen = saturate(1.0 - 2.0 * footprint * STRAW_LAYERS);
            float bulge = 4.0 * layer * (1.0 - layer);
            float seam = (1.0 - smoothstep(0.0, 0.16, layer) * smoothstep(1.0, 0.84, layer))
                * saturate(0.55 + 0.9 * GradientNoise2(polar * 6.7 + 9.2));
            //The swath's tone runs along the strip, which is ONE strip wound round: its place along it is the layer plus the
            //turn, continuous across u's seam where both jump by one (a tone per layer index stepped there in a radial line)
            float swath = 0.6 * GradientNoise2(float2((floor(phase) + turn) * 0.9, 3.7));

            //The straws, along the turns: fine across the radius, long round it, in two sizes; and the core loose, its
            //straws every way
            float arc = turn * 6.2831853 * max(r, 0.05);
            float straws = GradientNoise2(float2(r * 140.0, arc * 9.0)) * saturate(1.0 - footprint * 140.0)
                + 0.8 * GradientNoise2(float2(r * 55.0, arc * 3.5) + 2.2) * saturate(1.0 - footprint * 55.0);
            float loose = GradientNoise2(polar * 26.0 + 7.7) * saturate(1.0 - footprint * 30.0);
            float core = 1.0 - smoothstep(STRAW_CORE * 0.6, STRAW_CORE, r);

            relief = lerp(0.6 * bulge * seen + 0.25 * straws, 0.5 * loose, core);
            shade = lerp((1.0 - 0.32 * seam * seen + 0.2 * straws) * (1.0 + 0.24 * swath * seen), 0.62 + 0.25 * loose, core);
            //Loose ends at the rim catch the light
            shade += 0.12 * smoothstep(0.9, 1.0, r) * saturate(0.5 + straws);
        }
        else
        {
            //The side: straws running round it in clumps, and NO twine - the owner on the first cut's seven bands: "the ends
            //look good, but the cylinder's side does not - there should be no stripes there"
            float footprint = uvFootprint.y;
            float straws = GradientNoise2(float2(uv.x * 14.0, uv.y * 160.0)) * saturate(1.0 - footprint * 160.0);
            float clumps = GradientNoise2(float2(uv.x * 5.0, uv.y * 18.0) + 3.3);
            relief = 0.35 * straws + 0.3 * clumps;
            shade = 1.0 + 0.18 * straws + 0.1 * clumps;
        }

        N = PerturbNormalFromHeight(N, p, relief * 0.012 * StrawStrength);
        albedo *= lerp(1.0, saturate(shade), StrawStrength);

        //Weathered where the rain sits, on top; darker and damp at the foot, where it lies on the grass
        float top = saturate(N.y);
        albedo *= 1.0 - 0.18 * top * top * saturate(0.5 + GradientNoise2(p.xz * 0.9 + 2.7));
        float foot = 1.0 - saturate((p.y - input.RootY) / FOOT_HEIGHT);
        albedo *= 1.0 - FOOT_DARKEN * foot * foot;
    }

    //The scene's own light, matched to the terrain: a hemisphere ambient tinted zenith-to-horizon by the
    //normal's height, plus the sun's own diffuse.
    float3 ambient = lerp(HorizonColor, ZenithColor, saturate(N.y * 0.5 + 0.5)) * layerShade;
    float ndotl = lerp(saturate(dot(N, SunDirection)), saturate((dot(N, SunDirection) + LEAF_WRAP) / (1.0 + LEAF_WRAP)), leaf);

    //The sun's cast shadow (#469): a trunk under its own crown, a boulder behind another, a tuft under a
    //tree - the sun term alone, the dome's ambient stays. Off the map (ShadowStrength 0) the branch is skipped.
    float shadow = 1.0;
    [branch]
    if (ShadowStrength > 0.0)
        shadow = SunShadow(input.WorldPosition, N, SunDirection);
    shadow *= CloudSunlight(input.WorldPosition, SunDirection);

    float3 color = albedo * (ambient + SunColor * (ndotl * shadow));

    //The sun THROUGH the leaves (#610): strongest looking straight towards the sun, yellow-green, and only where
    //the sun reaches the leaf - the shadow map is the upper layers shading the lower ones
    [branch]
    if (LeafStrength > 0.0)
    {
        float towards = saturate(dot(normalize(input.WorldPosition - CameraPosition), SunDirection));
        color += albedo * TRANSMISSION_TINT * SunColor * (TRANSMISSION_GAIN * pow(towards, TRANSMISSION_POWER) * shadow);
    }

    //The canopy's leaf mottle: a 3D field of WORLD position, so a big canopy gets bigger clumps in the same
    //place every frame and neighbouring trees do not share a pattern. Zero on a trunk (DappleStrength 0).
    //Uniform branches, so a wavefront takes one side and nothing inside takes a derivative.
    [branch]
    if (DappleStrength > 0.0)
        color *= DAPPLE_MEAN + DappleStrength * Fbm3(input.WorldPosition * DAPPLE_FREQUENCY, 3);

    //The bark's fissures (#451): the same kind of field, stretched up the trunk, ridging and grooving the
    //wood's shade. Zero on foliage and stone. Two octaves - a trunk is a few pixels wide from anywhere the
    //camera stands, and the first octave is the one that reads.
    [branch]
    if (BarkStrength > 0.0)
    {
        float3 p = input.WorldPosition * float3(BARK_FREQUENCY, BARK_FREQUENCY * BARK_STRETCH, BARK_FREQUENCY);
        color *= saturate(1.0 + BarkStrength * BARK_GAIN * Fbm3(p, 2));

        //Lichen in patches, the lit colour of its own grey-green rather than the bark's
        float lichen = smoothstep(LICHEN_COVER, LICHEN_COVER + 0.08, Fbm3(input.WorldPosition * LICHEN_FREQUENCY + 17.3, 2));
        float light = dot(color, float3(0.2126, 0.7152, 0.0722)) / max(dot(albedo, float3(0.2126, 0.7152, 0.0722)), 1e-3);
        color = lerp(color, LICHEN_COLOR * light, lichen * LICHEN_AMOUNT * BarkStrength);

        //The foot stained with the ground it stands in (#670): the references' lower bark is the soil's colour,
        //dusted and splashed, and a root diving into the plain is the plain's colour where it goes in. Lit as
        //the bark is, so the stain takes the light the trunk takes.
        float earthLine = EARTH_HEIGHT * (0.65 + 0.7 * saturate(0.5 + Fbm3(input.WorldPosition * 2.1 + 5.0, 1)));
        float earth = 1.0 - saturate((input.WorldPosition.y - input.RootY) / earthLine);
        color = lerp(color, FootEarth * light, earth * earth * EARTH_AMOUNT * (any(FootEarth) ? 1.0 : 0.0));

        //The foot darker, fading out over FOOT_HEIGHT above the plant's root
        float foot = 1.0 - saturate((input.WorldPosition.y - input.RootY) / FOOT_HEIGHT);
        color *= 1.0 - FOOT_DARKEN * foot * foot * saturate(BarkStrength * 1.7);
    }

    //The per-draw light that is not the sky's - a hearth stone's own fire, a lit window's lamp.
    color += AddedLight;

    //Horizon haze, the ground's own: a far plant softens into the skyline at the rate the field under it does.
    float dist = distance(CameraPosition, input.WorldPosition);
    float haze = saturate(dist / (HorizonHazeDistance * PLANT_HAZE_REACH));
    color = lerp(color, HazeColor, haze * haze);

    return float4(color, 1.0);
}

technique Acacia
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL AcaciaVS();
        PixelShader = compile PS_SHADERMODEL AcaciaPS();
    }
};

//--- The shadow caster (#469): the same instanced geometry drawn from the sun into SunShadowMap's target,
//writing the map's own clip depth (orthographic, so linear) into a 32-bit channel. No material, no light -
//the instance stream is read for the world matrix and nothing else.

struct ShadowVertexOutput
{
    float4 Position : SV_POSITION;
    float Depth : TEXCOORD0;
    float2 UV : TEXCOORD1;
    float2 Seed : TEXCOORD2;
};

ShadowVertexOutput ShadowVS(AcaciaVertexInput input)
{
    ShadowVertexOutput output;
    float4x4 world = float4x4(input.World1, input.World2, input.World3, input.World4);
    float4 worldPosition = mul(input.Position, world);
    output.Position = mul(worldPosition, ShadowViewProjection);
    output.Depth = output.Position.z;
    output.UV = input.UV;
    output.Seed = input.World4.xz;
    return output;
}

float4 ShadowPS(ShadowVertexOutput input) : COLOR
{
    //The leaf sprays cast their leaflets, not their cards (#610): the dapple under the umbrella
    [branch]
    if (LeafStrength > 0.0)
        clip(CardMask(input.UV, input.Seed));

    return float4(input.Depth, 0.0, 0.0, 1.0);
}

technique ShadowCaster
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ShadowVS();
        PixelShader = compile PS_SHADERMODEL ShadowPS();
    }
};
