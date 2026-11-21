//The desert's creatures (#966): beetles and scorpions that come up out of the sand, walk a little way and dig back in
//(DesertCritters). Two techniques: the bodies, instanced, their legs swung in the vertex shader on each creature's gait
//(CritterMesh's texture coordinate carries a leg's phase and how far along it a vertex is), lit by the scene's sun and dome
//and hazed with the dunes; and the soft discs on the sand under and round them - a shadow, the burrow's hole, the puffs of
//sand they throw - each a quad whose texture coordinate runs -1 to 1 across it.

#include "ShaderTarget.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;
float HazeDistance;
float Time;

//How high a foot lifts on its way forwards, in body lengths. How far it reaches is the instance's (Custom.w, CritterGait.Stride)
static const float LIFT = 0.05;
static const float PI = 3.14159265;
static const float TWO_PI = 6.28318531;

//A foot's gait at a leg's phase (#1217), CritterGait's lines, which DesertCritters lays a beetle's prints by: c the share of
//its cycle since it lifted off; the first half it swings forwards through the air (easing in and out, at rest as it leaves
//the sand and as it lands), the second it stands on the sand and sweeps straight back as far as the body walks, so it stays
//where it touched down. x: how far forwards of its place at rest, in strides; y: how high, in LIFTs
float2 Foot(float phase)
{
    float c = frac(phase / TWO_PI + 0.25);
    float u = saturate(c * 2.0);
    float v = saturate(c * 2.0 - 1.0);
    float forward = c < 0.5 ? 4.0 * u * u * (3.0 - 2.0 * u) - 2.0 * u - 1.0 : 1.0 - 2.0 * v;
    float up = c < 0.5 ? sin(PI * u) : 0.0;
    return float2(forward, up);
}

//The two shells: a darkling beetle's glossy black; a scorpion's dark reddish amber, its legs, claws and tail an orange amber,
//as the references draw them. ⚠ A sand-like amber (0.33, 0.19, 0.07) lit by the desert's sun was the sand's own colour
static const float3 BEETLE = float3(0.018, 0.017, 0.02);
static const float3 SCORPION = float3(0.15, 0.055, 0.018);
static const float3 SCORPION_LIMB = float3(0.36, 0.14, 0.035);

struct CritterInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 Part : TEXCOORD0;      //x: a leg's gait phase, -1 the body, -2 the tail; y: how far along the leg or the tail
    float4 World1 : TEXCOORD1;    //per instance: the world matrix, row-major
    float4 World2 : TEXCOORD2;
    float4 World3 : TEXCOORD3;
    float4 World4 : TEXCOORD4;
    float4 Custom : TEXCOORD5;    //per instance: x the gait's phase, y the creature's seed, z the kind (0 beetle, 1 scorpion), w its stride in body lengths
};

struct CritterOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 Part : TEXCOORD2;
    float Kind : TEXCOORD3;
};

CritterOutput CritterVS(CritterInput input)
{
    CritterOutput output;
    float3 p = input.Position.xyz;
    float gait = input.Custom.x;

    //A leg: its foot swings forwards through the air and stands on the sand on the gait, more the further down the leg
    [flatten]
    if (input.Part.x >= 0.0)
    {
        float2 foot = Foot(gait + input.Part.x);
        p.z += foot.x * input.Custom.w * input.Part.y;
        p.y += foot.y * LIFT * input.Part.y;
    }
    //The tail sways and the sting twitches
    else if (input.Part.x < -1.5)
    {
        p.x += sin(Time * 1.7 + input.Custom.y * 13.0) * 0.04 * input.Part.y;
        p.y += sin(Time * 2.9 + input.Custom.y * 7.0) * 0.015 * input.Part.y;
    }

    float4x4 world = float4x4(input.World1, input.World2, input.World3, input.World4);
    float4 worldPosition = mul(float4(p, 1.0), world);
    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.Normal = normalize(mul(input.Normal, (float3x3)world));
    output.Part = input.Part;
    output.Kind = input.Custom.z;
    return output;
}

float4 CritterPS(CritterOutput input) : COLOR
{
    float3 n = normalize(input.Normal);
    float3 toEye = normalize(CameraPosition - input.WorldPosition);
    float scorpion = step(0.5, input.Kind);
    //The scorpion darkens down its tail and its legs, as the references' do
    float limb = input.Part.x >= 0.0 || input.Part.x < -1.5 ? saturate(0.4 + input.Part.y) : 0.0;
    float3 albedo = lerp(BEETLE, lerp(SCORPION, SCORPION_LIMB, limb), scorpion);

    float3 ambient = lerp(HorizonColor * 0.6, ZenithColor, saturate(n.y * 0.5 + 0.5)) * 0.8;
    float diffuse = saturate(dot(n, SunDirection));
    //Chitin is glossy: a tight highlight, the beetle's tighter and brighter
    float3 h = normalize(SunDirection + toEye);
    float specular = pow(saturate(dot(n, h)), lerp(60.0, 30.0, scorpion)) * lerp(0.9, 0.35, scorpion);
    float3 color = albedo * (ambient + SunColor * diffuse) + SunColor * specular * diffuse;

    float haze = saturate(distance(CameraPosition, input.WorldPosition) / HazeDistance);
    color = lerp(color, HorizonColor, haze * haze);
    return float4(color, 1.0);
}

//--- The discs on the sand: a quad, its texture coordinate -1 to 1, its colour premultiplied by its strength in the vertex
struct DiscInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
};

struct DiscOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
};

DiscOutput DiscVS(DiscInput input)
{
    DiscOutput output;
    output.Position = mul(mul(input.Position, View), Projection);
    output.Color = input.Color;
    output.UV = input.UV;
    return output;
}

float4 DiscPS(DiscOutput input) : COLOR
{
    //A soft disc: full at the middle, gone at the rim
    float r = length(input.UV);
    clip(1.0 - r);
    float a = 1.0 - smoothstep(0.2, 1.0, r);
    return input.Color * a;
}

technique Critter
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CritterVS();
        PixelShader = compile PS_SHADERMODEL CritterPS();
    }
};

technique Disc
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL DiscVS();
        PixelShader = compile PS_SHADERMODEL DiscPS();
    }
};
