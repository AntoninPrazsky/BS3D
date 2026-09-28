//Everything planted on the savanna, as REAL 3D geometry (#202, #451). Each plant is an instanced procedural
//mesh - a bark trunk under wide flat tiers of foliage (AcaciaMesh), a low rounded clump for a bush, a spiked
//clump for a bunch of grass, a fluted spire for a termite mound, a boulder, a fallen log (SavannaScatter) -
//shaded from the scene's own sun and dome exactly as the terrain is (Savanna.fx), so a tree sits in the
//savanna's light rather than being pasted over it. It replaces the flat billboard that read as a paper
//cutout: a surface of revolution has volume from every angle, where a camera-facing quad only ever shows one
//silhouette. One instanced draw per mesh variant per material - DiffuseColor/DiffuseDry, DappleStrength and
//BarkStrength are the per-draw material: dappled green foliage for a canopy, fissured brown for a trunk,
//plain for a stone. The per-instance world matrix rides in a second vertex stream (TEXCOORD1-4), like
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
//both of its sides are lit, and the sun comes through it. See LeafMask and AcaciaMesh's LeafSprays.
float LeafStrength;

//The distance the plant melts into the skyline over - Savanna.fx's own HorizonHazeDistance, handed over so a
//far tree fades the way the ground it stands on does. Without it the treeline stood in full colour on a
//hazed hillside, which is most of what made the far scatter read as pasted on (#451). It is stretched by
//PLANT_HAZE_REACH below: at the ground's own distance the treeline at 300-400 out came back as pale lumps on
//a pale hill, and the references keep a far wooded edge darker than the field under it - a dark object in
//aerial perspective stays darker than a bright one beside it.
float HorizonHazeDistance;
static const float PLANT_HAZE_REACH = 1.5;

//Light this draw carries that the scene's own sun and dome do not account for, added flat after them. It is
//zero for every plant and non-zero for exactly one thing: the campfires' hearth stones (#282), which stand
//in a ring at one distance from one fire, so what a point light would work out per pixel is a constant per
//draw here - and it flickers, because SceneRenderer hands over the fire's own colour at this frame. It buys
//the fire pit its firelight for one add and no loop; what it does not do is favour the side of a stone that
//faces the flame.
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

    float rachis = (MIDRIB_HALF - across) * 8.0 * step(0.0, uv.x - 0.02);
    float fine = max(leaflet, rachis);

    //The spray as one outline: the same envelope, filled
    float coarse = envelope * 0.9 - across;

    float blur = fwidth(cell);
    return lerp(fine, coarse, smoothstep(0.35, 0.8, blur));
}

struct AcaciaVertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 World1 : TEXCOORD1;   //per-instance world matrix, row-major like InstancedModel.fx (no transpose)
    float4 World2 : TEXCOORD2;
    float4 World3 : TEXCOORD3;
    float4 World4 : TEXCOORD4;
    float4 Custom : TEXCOORD5;   //x: dryness 0..1 (towards DiffuseDry), y: brightness offset (-1..1 about 0)
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
    float RootY : TEXCOORD5;     //the instance's own foot, for the bark's darker base (#610)
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
    output.RootY = input.World4.y;

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
        clip(LeafMask(input.UV, input.Seed));
        N = front ? N : -N;
        layerShade = 1.0 - LAYER_AMBIENT_FALL * saturate(floor(input.UV.y * 0.5) * 0.5);
        leaf = 1.0;
    }

    //This instance's own shade of the draw's material: its dryness leans the colour towards the drier one,
    //its brightness offset lifts or lowers it, so a grove of one variant is not one green stamped out.
    float3 albedo = lerp(DiffuseColor, DiffuseDry, saturate(input.Tint.x)) * (1.0 + input.Tint.y);

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

        //The foot darker, fading out over FOOT_HEIGHT above the plant's root
        float foot = 1.0 - saturate((input.WorldPosition.y - input.RootY) / FOOT_HEIGHT);
        color *= 1.0 - FOOT_DARKEN * foot * foot * saturate(BarkStrength * 1.7);
    }

    //The per-draw light that is not the sky's - a hearth stone's own fire, and nothing else today.
    color += AddedLight;

    //Horizon haze, the ground's own: a far plant softens into the skyline at the rate the field under it does.
    float dist = distance(CameraPosition, input.WorldPosition);
    float haze = saturate(dist / (HorizonHazeDistance * PLANT_HAZE_REACH));
    color = lerp(color, HorizonColor, haze * haze);

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
        clip(LeafMask(input.UV, input.Seed));

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
