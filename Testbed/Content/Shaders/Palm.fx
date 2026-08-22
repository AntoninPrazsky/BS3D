//Scattered coconut palms over the tropical beach, as real 3D geometry (#244): the acacia's arrangement
//(Acacia.fx) with the wind added. Each palm is an instanced procedural mesh - a bowed ring-scarred
//trunk under a crown of radiating drooping fronds with a skirt of dead ones hanging beneath it
//(PalmMesh) - shaded from the scene's own sun and dome exactly as the terrain is (Tropical.fx), so a
//palm sits in the beach's light rather than being pasted over it. DiffuseColor and DappleStrength are
//the per-draw material: dappled green fronds, plain brown wood.
//
//THE SWAY IS THE ONE THING THIS SHADER DOES THE ACAIA DOES NOT, and it is why it exists as its own
//file rather than sharing Acacia.fx: a palm that stands dead still on a tropical beach reads as a
//plastic one. Each vertex carries a SWAY WEIGHT in its TEXCOORD0.x - zero along the trunk, rising
//along each frond to its tip - so the wind moves the crown and never the trunk (a palm whose whole
//body waves reads as a kelp). The weight is built into PalmMesh, which is why the two are one change.
//The phase comes off the instance's own world position, so no two palms beat in time - the campfire
//ring's reasoning, applied to leaves.
//
//Testbed-shared (the map editor and the game build it too), Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//For the fronds' leaf mottle - a 3D field of world position, so it has no seam and does not swim as
//the frond turns with its tree (Acacia.fx's own reasoning).
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

//Towards the sun, dotted with the surface normal exactly as Tropical.fx does it, and the dome's
//ambient split zenith-to-horizon by the normal's height - so the palm takes the same light the
//sand under it does.
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//The per-draw material: the diffuse colour and how strongly the leaf mottle breaks it up (0 on wood).
float3 DiffuseColor;
float DappleStrength;

//The wind: how far the fronds sway at their tips and how fast, along this direction. Off the wall
//clock, so the palms keep moving while the simulation is paused - like the clouds, the sea and the
//birds, the wind does not wait for the player.
float2 WindDirection;
float SwayStrength;
float SwaySpeed;
float PalmTime;

//The leaf mottle's world-space frequency and its mean (a crown is dappled foliage, not a flat mass).
static const float DAPPLE_FREQUENCY = 0.55;
static const float DAPPLE_MEAN = 0.86;

struct PalmVertexInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
	float2 Sway : TEXCOORD0;       //x = the sway weight the mesh bakes per vertex; y unused
	float4 World1 : TEXCOORD1;     //per-instance world matrix, row-major like InstancedModel.fx (no transpose)
	float4 World2 : TEXCOORD2;
	float4 World3 : TEXCOORD3;
	float4 World4 : TEXCOORD4;
};

struct PalmVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
};

PalmVertexOutput PalmVS(PalmVertexInput input)
{
	PalmVertexOutput output;

	float4x4 world = float4x4(input.World1, input.World2, input.World3, input.World4);
	float4 worldPosition = mul(input.Position, world);

	//The sway: two incommensurate oscillators (a single sin is a metronome), travelling downwind,
	//phased off the instance's own position so a grove never beats in unison. Applied after the world
	//transform in world space - the frond strips are built in the mesh's own frame, but the wind does
	//not care which tree it is moving.
	float3 instancePos = float3(input.World4.x, input.World4.y, input.World4.z);
	float phase = dot(instancePos.xz, WindDirection) * 0.35 + PalmTime * SwaySpeed;

	worldPosition.xz += WindDirection * SwayStrength * input.Sway.x
		* (sin(phase) + 0.5 * sin(phase * 2.3 + 1.7));

	output.WorldPosition = worldPosition.xyz;
	output.Position = mul(mul(worldPosition, View), Projection);
	//The instance transform is rotation + uniform scale + translation, so the plain matrix rotates the
	//normal (a uniform scale leaves it only needing a re-normalize). The sway's small horizontal
	//offset is not un-rotated into the normal - at a fraction of a frond's length the lighting error
	//is beneath notice, and the alternative is re-deriving a normal per vertex for a wind that barely
	//tilts it.
	output.WorldNormal = normalize(mul(input.Normal, (float3x3)world));

	return output;
}

float4 PalmPS(PalmVertexOutput input) : COLOR
{
	float3 N = normalize(input.WorldNormal);

	//The scene's own light, matched to the terrain: a hemisphere ambient tinted zenith-to-horizon by
	//the normal's height, plus the sun's own diffuse. A frond strip is a single sheet whose normal the
	//mesh tilts along its spine, so a drooping frond shades under itself for free.
	float3 ambient = lerp(HorizonColor, ZenithColor, saturate(N.y * 0.5 + 0.5));
	float ndotl = saturate(dot(N, SunDirection));
	float3 color = DiffuseColor * (ambient + SunColor * ndotl);

	//The fronds' leaf mottle: a 3D field of WORLD position, so neighbouring crowns do not share a
	//pattern and the mottle does not slide over the leaves as they sway. Zero on wood (DappleStrength 0).
	if (DappleStrength > 0.0)
		color *= DAPPLE_MEAN + DappleStrength * Fbm3(input.WorldPosition * DAPPLE_FREQUENCY, 3);

	return float4(color, 1.0);
}

technique Palm
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL PalmVS();
		PixelShader = compile PS_SHADERMODEL PalmPS();
	}
};
