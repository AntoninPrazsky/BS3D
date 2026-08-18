//Scattered acacia trees and low bushes over the savanna, as REAL 3D geometry (#202). Each plant is an
//instanced procedural mesh - a bark trunk under a wide flat-topped umbrella canopy (AcaciaMesh), a low
//rounded clump for a bush - shaded from the scene's own sun and dome exactly as the terrain is (Savanna.fx),
//so a tree sits in the savanna's light rather than being pasted over it. It replaces the flat billboard that
//read as a paper cutout: a surface of revolution has volume from every angle, where a camera-facing quad
//only ever shows one silhouette. One instanced draw per mesh variant per material - DiffuseColor and
//DappleStrength are the per-draw material: dappled green foliage for a canopy, plain brown for a trunk.
//The per-instance world matrix rides in a second vertex stream (TEXCOORD1-4), like InstancedModel.fx.
//Testbed-shared (the map editor and the game build it too), Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//For the canopy's leaf mottle - a 3D field of world position, so it has no seam and does not swim as the
//canopy turns with its tree, the same reasoning the crown fields carry.
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;     //kept for parity; unused now the plant is real geometry rather than a billboard

//Towards the sun, dotted with the surface normal exactly as Savanna.fx does it, and the dome's ambient split
//zenith-to-horizon by the normal's height - so the acacia takes the same light the ground under it does.
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//The per-draw material: the diffuse colour and how strongly the leaf mottle breaks it up (0 on a trunk).
float3 DiffuseColor;
float DappleStrength;

//The leaf mottle's world-space frequency and its mean (a canopy is dappled foliage, not a flat green mass).
static const float DAPPLE_FREQUENCY = 0.55;
static const float DAPPLE_MEAN = 0.86;

struct AcaciaVertexInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
	float4 World1 : TEXCOORD1;   //per-instance world matrix, row-major like InstancedModel.fx (no transpose)
	float4 World2 : TEXCOORD2;
	float4 World3 : TEXCOORD3;
	float4 World4 : TEXCOORD4;
};

struct AcaciaVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
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

	return output;
}

float4 AcaciaPS(AcaciaVertexOutput input) : COLOR
{
	float3 N = normalize(input.WorldNormal);

	//The scene's own light, matched to the terrain: a hemisphere ambient tinted zenith-to-horizon by the
	//normal's height, plus the sun's own diffuse.
	float3 ambient = lerp(HorizonColor, ZenithColor, saturate(N.y * 0.5 + 0.5));
	float ndotl = saturate(dot(N, SunDirection));
	float3 color = DiffuseColor * (ambient + SunColor * ndotl);

	//The canopy's leaf mottle: a 3D field of WORLD position, so a big canopy gets bigger clumps in the same
	//place every frame and neighbouring trees do not share a pattern. Zero on a trunk (DappleStrength 0).
	if (DappleStrength > 0.0)
		color *= DAPPLE_MEAN + DappleStrength * Fbm3(input.WorldPosition * DAPPLE_FREQUENCY, 3);

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
