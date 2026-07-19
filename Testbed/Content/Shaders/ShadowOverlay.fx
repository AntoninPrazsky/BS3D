//Darkens the ground where the shadow map says the key light is blocked.
//Drawn as a single translucent quad lying on the ground plane (premultiplied alpha blending),
//so the ground below keeps its own material and lighting and is only multiplied down —
//no need to replicate its BasicEffect shading in a custom shader.

#if OPENGL
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 ViewProjection;
float4x4 LightViewProjection;

//0 = no darkening, 1 = fully black shadows
float ShadowStrength;
//1 / shadow map resolution, for the PCF taps
float ShadowMapTexelSize;

//Only the balls render into the shadow map (the receiving ground does not), so no self-shadowing
//can occur and the bias only needs to cover numeric noise. Too large a bias eats the part of the
//shadow closest to the ball-ground contact, leaving a crescent-shaped hole behind resting balls.
static const float DepthBias = 0.0005;

//How far apart the PCF taps are, in shadow map texels: larger = softer (and slightly dithered) edges
static const float PcfSpread = 2.5;

texture ShadowMap;
sampler ShadowMapSampler = sampler_state
{
	Texture = <ShadowMap>;
	MinFilter = Point;
	MagFilter = Point;
	MipFilter = None;
	AddressU = Clamp;
	AddressV = Clamp;
};

struct VertexShaderInput
{
	float4 Position : POSITION0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 WorldPosition : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
	VertexShaderOutput output;

	output.WorldPosition = mul(input.Position, World);
	output.Position = mul(output.WorldPosition, ViewProjection);

	return output;
}

float SampleShadow(float2 shadowUV, float depth)
{
	//1 when this ground point is farther from the light than what the shadow map saw (= occluded)
	return step(tex2D(ShadowMapSampler, shadowUV).r + DepthBias, depth);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float4 lightClip = mul(input.WorldPosition, LightViewProjection);

	float2 shadowUV = lightClip.xy / lightClip.w * float2(0.5, -0.5) + 0.5;
	float depth = lightClip.z / lightClip.w;

	if (shadowUV.x < 0 || shadowUV.x > 1 || shadowUV.y < 0 || shadowUV.y > 1) return 0;

	//4x4 PCF with spread-out taps for soft edges
	float shadow = 0;

	[unroll]
	for (int x = 0; x < 4; x++)
	{
		[unroll]
		for (int y = 0; y < 4; y++)
		{
			float2 offset = float2(x - 1.5, y - 1.5) * ShadowMapTexelSize * PcfSpread;
			shadow += SampleShadow(shadowUV + offset, depth);
		}
	}

	shadow /= 16;

	//Premultiplied alpha: zero RGB with this alpha multiplies the destination down
	return float4(0, 0, 0, shadow * ShadowStrength);
}

technique ShadowOverlay
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
