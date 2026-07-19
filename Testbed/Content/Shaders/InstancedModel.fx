//Draws many instances of a rigid model in a single draw call per mesh part.
//The per-instance world matrix is supplied through a second vertex stream (TEXCOORD1-TEXCOORD4 hold its rows).
//Lighting replicates BasicEffect with EnableDefaultLighting and per-pixel (Blinn-Phong) shading,
//so instanced models look the same as those rendered through ModelRenderer.

#if OPENGL
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 View;
float4x4 Projection;

//Absolute transform of the mesh parent bone, applied before the per-instance world matrix
float4x4 Bone;

float3 EyePosition;

//Material of the mesh part being drawn
float4 DiffuseColor;
float3 EmissiveColor;
//Premultiplied on the CPU: ambient tint * material diffuse. Modulated per pixel by the sky hemisphere below.
float3 AmbientColor;
float3 SpecularColor;
float SpecularPower;

//Hemisphere ambient palette taken from the current sky dome: upward-facing surfaces receive SkyColor,
//downward-facing ones GroundColor. Both default to white, which reproduces a constant ambient term.
float3 SkyColor;
float3 GroundColor;

//The key light is positional (a "sun" placed in the scene): its direction differs per surface point,
//so every ball is lit according to where it sits relative to the light instead of all balls looking identical.
float3 KeyLightPosition;
float3 DirLight0DiffuseColor;
float3 DirLight0SpecularColor;

float3 DirLight1Direction;
float3 DirLight1DiffuseColor;
float3 DirLight1SpecularColor;

float3 DirLight2Direction;
float3 DirLight2DiffuseColor;
float3 DirLight2SpecularColor;

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
};

struct InstanceInput
{
	float4 WorldRow1 : TEXCOORD1;
	float4 WorldRow2 : TEXCOORD2;
	float4 WorldRow3 : TEXCOORD3;
	float4 WorldRow4 : TEXCOORD4;
	//XYZ = world-space direction towards the instance's occluders (zero = none), W = base occlusion factor
	float4 Custom : TEXCOORD5;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
	float4 OcclusionData : TEXCOORD2;
};

VertexShaderOutput MainVS(VertexShaderInput input, InstanceInput instance)
{
	VertexShaderOutput output;

	//Rows are stored in the same layout as an XNA row-major matrix, so no transpose is needed
	float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

	float4 worldPosition = mul(mul(input.Position, Bone), world);

	output.WorldPosition = worldPosition.xyz;
	output.Position = mul(mul(worldPosition, View), Projection);
	//Bone and instance transforms are rotation + translation (+ uniform scale at most), so the adjoint transpose is not needed
	output.WorldNormal = mul(mul(float4(input.Normal, 0), Bone), world).xyz;
	output.OcclusionData = instance.Custom;

	return output;
}

//Same per-light math as ComputeLights in BasicEffect.fx (Blinn-Phong with the dotL > 0 mask)
void AddLight(float3 towardsLight, float3 lightDiffuse, float3 lightSpecular, float3 worldNormal, float3 eyeVector,
	inout float3 diffuse, inout float3 specular)
{
	float dotL = dot(worldNormal, towardsLight);
	float lit = step(0, dotL);

	diffuse += lightDiffuse * (dotL * lit);

	float dotH = max(dot(worldNormal, normalize(towardsLight + eyeVector)), 0);
	specular += lightSpecular * pow(dotH * lit, SpecularPower);
}

//How strongly the directional part of the occlusion darkens the surface facing the occluders
static const float DirectionalOcclusionStrength = 1.1;

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);
	float3 eyeVector = normalize(EyePosition - input.WorldPosition);

	float3 diffuse = 0;
	float3 specular = 0;

	AddLight(normalize(KeyLightPosition - input.WorldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, worldNormal, eyeVector, diffuse, specular);
	AddLight(-DirLight1Direction, DirLight1DiffuseColor, DirLight1SpecularColor, worldNormal, eyeVector, diffuse, specular);
	AddLight(-DirLight2Direction, DirLight2DiffuseColor, DirLight2SpecularColor, worldNormal, eyeVector, diffuse, specular);

	float3 hemisphere = lerp(GroundColor, SkyColor, worldNormal.y * 0.5 + 0.5);

	//Neighbour-based ambient occlusion: the base factor darkens the whole ball a little, the directional
	//part darkens the side of the ball facing its occluders, so the crevices between touching balls go dark
	float occlusion = saturate(input.OcclusionData.w - DirectionalOcclusionStrength * max(0, dot(worldNormal, input.OcclusionData.xyz)));
	float diffuseOcclusion = lerp(0.6, 1.0, occlusion);

	float4 color = float4(diffuse * DiffuseColor.rgb * diffuseOcclusion + hemisphere * AmbientColor * occlusion + EmissiveColor, DiffuseColor.a);
	color.rgb += specular * SpecularColor * color.a * occlusion;

	return color;
}

technique InstancedModel
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};

//Depth-only pass for shadow mapping: renders the instances from the light's point of view,
//writing normalized depth into the red channel of a Single-format render target.

float4x4 LightViewProjection;

struct DepthVertexShaderOutput
{
	float4 Position : SV_POSITION;
	float Depth : TEXCOORD0;
};

DepthVertexShaderOutput DepthVS(VertexShaderInput input, InstanceInput instance)
{
	DepthVertexShaderOutput output;

	float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
	float4 worldPosition = mul(mul(input.Position, Bone), world);

	output.Position = mul(worldPosition, LightViewProjection);
	output.Depth = output.Position.z / output.Position.w;

	return output;
}

float4 DepthPS(DepthVertexShaderOutput input) : COLOR
{
	return float4(input.Depth, 0, 0, 1);
}

technique InstancedDepth
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL DepthVS();
		PixelShader = compile PS_SHADERMODEL DepthPS();
	}
};
