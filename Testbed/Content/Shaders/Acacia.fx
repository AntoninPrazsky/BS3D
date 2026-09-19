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

struct AcaciaVertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 World1 : TEXCOORD1;   //per-instance world matrix, row-major like InstancedModel.fx (no transpose)
    float4 World2 : TEXCOORD2;
    float4 World3 : TEXCOORD3;
    float4 World4 : TEXCOORD4;
    float4 Custom : TEXCOORD5;   //x: dryness 0..1 (towards DiffuseDry), y: brightness offset (-1..1 about 0)
};

struct AcaciaVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float2 Tint : TEXCOORD2;
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

    return output;
}

float4 AcaciaPS(AcaciaVertexOutput input) : COLOR
{
    float3 N = normalize(input.WorldNormal);

    //This instance's own shade of the draw's material: its dryness leans the colour towards the drier one,
    //its brightness offset lifts or lowers it, so a grove of one variant is not one green stamped out.
    float3 albedo = lerp(DiffuseColor, DiffuseDry, saturate(input.Tint.x)) * (1.0 + input.Tint.y);

    //The scene's own light, matched to the terrain: a hemisphere ambient tinted zenith-to-horizon by the
    //normal's height, plus the sun's own diffuse.
    float3 ambient = lerp(HorizonColor, ZenithColor, saturate(N.y * 0.5 + 0.5));
    float ndotl = saturate(dot(N, SunDirection));
    float3 color = albedo * (ambient + SunColor * ndotl);

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
