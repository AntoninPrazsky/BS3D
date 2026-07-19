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

float3 DirLight0Direction;
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
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
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

	return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);
	float3 eyeVector = normalize(EyePosition - input.WorldPosition);

	//Same math as ComputeLights in BasicEffect.fx with all three lights enabled
	float3x3 lightDirections = float3x3(DirLight0Direction, DirLight1Direction, DirLight2Direction);
	float3x3 lightDiffuse = float3x3(DirLight0DiffuseColor, DirLight1DiffuseColor, DirLight2DiffuseColor);
	float3x3 lightSpecular = float3x3(DirLight0SpecularColor, DirLight1SpecularColor, DirLight2SpecularColor);

	float3x3 halfVectors;
	halfVectors[0] = normalize(eyeVector - DirLight0Direction);
	halfVectors[1] = normalize(eyeVector - DirLight1Direction);
	halfVectors[2] = normalize(eyeVector - DirLight2Direction);

	float3 dotL = mul(-lightDirections, worldNormal);
	float3 dotH = mul(halfVectors, worldNormal);

	float3 zeroL = step(0, dotL);

	float3 diffuse = zeroL * dotL;
	float3 specular = pow(max(dotH, 0) * zeroL, SpecularPower);

	float3 hemisphere = lerp(GroundColor, SkyColor, worldNormal.y * 0.5 + 0.5);

	float4 color = float4(mul(diffuse, lightDiffuse) * DiffuseColor.rgb + hemisphere * AmbientColor + EmissiveColor, DiffuseColor.a);
	color.rgb += mul(specular, lightSpecular) * SpecularColor * color.a;

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
