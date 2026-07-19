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

//Y of the ground plane, for the ground-contact part of the ambient occlusion
float GroundHeight;

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

//Material texture of the mesh part (InstancedModelTextured) or the world-space detail
//texture (InstancedModelTriplanar)
texture Texture;
sampler2D TextureSampler = sampler_state
{
	Texture = <Texture>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Linear;
	AddressU = Wrap;
	AddressV = Wrap;
};

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

//Ground-contact occlusion: how strongly the downward-facing side of a ball darkens near the ground,
//and over how many world units above the ground the effect fades out
static const float GroundOcclusionStrength = 0.55;
static const float GroundOcclusionRange = 2.0;

//Shared shading: texColor is the sampled material texture (white for untextured parts).
//Like BasicEffect, the texture modulates the whole non-specular colour (diffuse, ambient and emissive).
float4 ShadePixel(float3 worldPosition, float3 rawWorldNormal, float4 occlusionData, float4 texColor)
{
	float3 worldNormal = normalize(rawWorldNormal);
	float3 eyeVector = normalize(EyePosition - worldPosition);

	float3 diffuse = 0;
	float3 specular = 0;

	AddLight(normalize(KeyLightPosition - worldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, worldNormal, eyeVector, diffuse, specular);
	AddLight(-DirLight1Direction, DirLight1DiffuseColor, DirLight1SpecularColor, worldNormal, eyeVector, diffuse, specular);
	AddLight(-DirLight2Direction, DirLight2DiffuseColor, DirLight2SpecularColor, worldNormal, eyeVector, diffuse, specular);

	float3 hemisphere = lerp(GroundColor, SkyColor, worldNormal.y * 0.5 + 0.5);

	//Neighbour-based ambient occlusion: the base factor darkens the whole ball a little, the directional
	//part darkens the side of the ball facing its occluders, so the crevices between touching balls go dark
	float occlusion = saturate(occlusionData.w - DirectionalOcclusionStrength * max(0, dot(worldNormal, occlusionData.xyz)));

	//The ground is one more occluder: downward-facing surface close to the ground plane darkens
	float groundProximity = saturate(1 - (worldPosition.y - GroundHeight) / GroundOcclusionRange);
	occlusion = saturate(occlusion - GroundOcclusionStrength * groundProximity * saturate(-worldNormal.y));
	float diffuseOcclusion = lerp(0.6, 1.0, occlusion);

	float4 color = float4((diffuse * DiffuseColor.rgb * diffuseOcclusion + hemisphere * AmbientColor * occlusion + EmissiveColor) * texColor.rgb, DiffuseColor.a * texColor.a);
	color.rgb += specular * SpecularColor * color.a * occlusion;

	return color;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	return ShadePixel(input.WorldPosition, input.WorldNormal, input.OcclusionData, float4(1, 1, 1, 1));
}

technique InstancedModel
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};

//Textured variant: the model vertices carry UVs in TEXCOORD0 (the instance stream stays in TEXCOORD1-5)

struct TexturedVertexShaderInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
	float2 TexCoord : TEXCOORD0;
};

struct TexturedVertexShaderOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
	float4 OcclusionData : TEXCOORD2;
	float2 TexCoord : TEXCOORD3;
};

TexturedVertexShaderOutput TexturedVS(TexturedVertexShaderInput input, InstanceInput instance)
{
	TexturedVertexShaderOutput output;

	float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

	float4 worldPosition = mul(mul(input.Position, Bone), world);

	output.WorldPosition = worldPosition.xyz;
	output.Position = mul(mul(worldPosition, View), Projection);
	output.WorldNormal = mul(mul(float4(input.Normal, 0), Bone), world).xyz;
	output.OcclusionData = instance.Custom;
	output.TexCoord = input.TexCoord;

	return output;
}

float4 TexturedPS(TexturedVertexShaderOutput input) : COLOR
{
	return ShadePixel(input.WorldPosition, input.WorldNormal, input.OcclusionData, tex2D(TextureSampler, input.TexCoord));
}

technique InstancedModelTextured
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL TexturedVS();
		PixelShader = compile PS_SHADERMODEL TexturedPS();
	}
};

//Detail texturing: a texture that only modulates the existing material colours
//(DetailStrength 0 = untextured look), mapped either through the model's own UVs
//(InstancedModelDetailUV — required for objects that move, or the texture would swim
//across them) or projected along the world axes for models with no UVs at all
//(InstancedModelTriplanar, e.g. the castle backdrop).

//Triplanar: world units per texture tile = 1 / DetailScale. UV mapping: tiles per UV span.
float DetailScale;
//How strongly the detail texture modulates the material colour (0 = not at all, 1 = fully)
float DetailStrength;
//Brightness compensation so a mid-grey detail texture does not darken the whole material
float DetailBoost;

//How strongly the procedural masonry joints show on vertical triplanar surfaces (0 = plain stone)
float MasonryStrength;

//Tangent-space normal map paired with the detail texture, and how far it tilts the surface normal
texture NormalMapTexture;
sampler2D NormalMapSampler = sampler_state
{
	Texture = <NormalMapTexture>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Linear;
	AddressU = Wrap;
	AddressV = Wrap;
};

float NormalStrength;

//Tangent frame derived from screen-space derivatives instead of vertex tangents: the instance
//vertex streams carry only position, normal and UV (the procedural meshes have nothing else to give),
//and this works for any mesh drawn through the renderer. Based on Christian Schueler's
//"Normal Mapping Without Precomputed Tangents".
float3x3 CotangentFrame(float3 normal, float3 worldPosition, float2 uv)
{
	float3 dp1 = ddx(worldPosition);
	float3 dp2 = ddy(worldPosition);
	float2 duv1 = ddx(uv);
	float2 duv2 = ddy(uv);

	float3 dp2perp = cross(dp2, normal);
	float3 dp1perp = cross(normal, dp1);

	float3 tangent = dp2perp * duv1.x + dp1perp * duv2.x;
	float3 bitangent = dp2perp * duv1.y + dp1perp * duv2.y;

	float invmax = rsqrt(max(dot(tangent, tangent), dot(bitangent, bitangent)));

	return float3x3(tangent * invmax, bitangent * invmax, normal);
}

float4 DetailUVNormalPS(TexturedVertexShaderOutput input) : COLOR
{
	float2 uv = input.TexCoord * DetailScale;

	float3 detail = tex2D(TextureSampler, uv).rgb;
	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

	float3 geometricNormal = normalize(input.WorldNormal);

	float3 tangentNormal = tex2D(NormalMapSampler, uv).xyz * 2 - 1;
	tangentNormal.xy *= NormalStrength;

	float3 worldNormal = normalize(mul(normalize(tangentNormal), CotangentFrame(geometricNormal, input.WorldPosition, uv)));

	return ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(texRgb, 1));
}

technique InstancedModelDetailUVNormal
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL TexturedVS();
		PixelShader = compile PS_SHADERMODEL DetailUVNormalPS();
	}
};

float4 DetailUVPS(TexturedVertexShaderOutput input) : COLOR
{
	float3 detail = tex2D(TextureSampler, input.TexCoord * DetailScale).rgb;
	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

	return ShadePixel(input.WorldPosition, input.WorldNormal, input.OcclusionData, float4(texRgb, 1));
}

technique InstancedModelDetailUV
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL TexturedVS();
		PixelShader = compile PS_SHADERMODEL DetailUVPS();
	}
};

//Stone block coursing drawn on the vertical surfaces (world units)
static const float BrickWidth = 3.0;
static const float BrickHeight = 1.4;
static const float MortarWidth = 0.09;
static const float MortarDarkness = 0.62;

//0 in a mortar joint, 1 inside a block; p is a vertical wall plane in world units.
//Every other course is offset by half a block, like real coursed masonry.
float BrickMask(float2 p)
{
	float row = floor(p.y / BrickHeight);
	float2 cell = float2(frac((p.x + row * BrickWidth * 0.5) / BrickWidth), frac(p.y / BrickHeight));

	//Distance to the nearest cell border, back in world units
	float2 border = min(cell, 1 - cell) * float2(BrickWidth, BrickHeight);
	float distance = min(border.x, border.y);

	//Screen-space derivative keeps the joint edge soft and fades it out at long range instead of shimmering
	float soft = max(fwidth(distance), 0.02);

	return smoothstep(MortarWidth - soft, MortarWidth + soft, distance);
}

float4 TriplanarPS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	//Sharpened normal weights avoid visible cross-fading except near 45-degree edges
	float3 blend = pow(abs(worldNormal), 4);
	blend /= blend.x + blend.y + blend.z;

	float3 p = input.WorldPosition * DetailScale;

	float3 detail
		= tex2D(TextureSampler, p.zy).rgb * blend.x
		+ tex2D(TextureSampler, p.xz).rgb * blend.y
		+ tex2D(TextureSampler, p.xy).rgb * blend.z;

	//Masonry joints on the vertical faces only (roofs and other Y-facing surfaces keep plain stone)
	float verticalWeight = (blend.x + blend.z) * MasonryStrength;
	float brick = (BrickMask(input.WorldPosition.zy) * blend.x + BrickMask(input.WorldPosition.xy) * blend.z) / max(blend.x + blend.z, 0.001);
	float joints = lerp(1, lerp(MortarDarkness, 1, brick), verticalWeight);

	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength) * joints;

	return ShadePixel(input.WorldPosition, input.WorldNormal, input.OcclusionData, float4(texRgb, 1));
}

technique InstancedModelTriplanar
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL TriplanarPS();
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
