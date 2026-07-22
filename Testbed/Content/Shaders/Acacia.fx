//Scattered acacia trees over the savanna - the flat-topped umbrella tree that says "savanna" at a glance.
//One upright (cylindrical) billboard per tree, from a static buffer the C# side positions on the ground; the
//pixel shader IS the tree - a thin trunk under a wide flat-topped green crown. Alpha-tested (clip) rather
//than alpha-blended, so it writes depth and occludes the terrain and other trees correctly (the hard cutout
//edge is softened by the scene's supersampling). Lit modestly by the dome so it takes the scene's mood.
//Testbed-shared, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//Base half-width and height of a tree in world units (varied per tree by the packed random)
float TreeWidth;
float TreeHeight;

float3 CanopyColor;
float3 TrunkColor;

struct AcaciaVertexInput
{
	float4 Position : POSITION0; //Tree base position on the ground
	float3 Data : TEXCOORD0;     //(corner u in {-1,1}, corner v in {0,1}, per-tree random)
};

struct AcaciaVertexOutput
{
	float4 Position : SV_POSITION;
	float2 UV : TEXCOORD0; //u across the billboard [-1,1], v up it [0,1]
};

AcaciaVertexOutput AcaciaVS(AcaciaVertexInput input)
{
	AcaciaVertexOutput output;

	float3 basePos = input.Position.xyz;
	float rand = input.Data.z;

	//Upright billboard: up is world up (trees stay vertical), right is horizontal, perpendicular to the view
	float3 toCam = CameraPosition - basePos;
	toCam.y = 0.0;
	float3 right = normalize(cross(float3(0.0, 1.0, 0.0), normalize(toCam)));
	float3 up = float3(0.0, 1.0, 0.0);

	float w = TreeWidth * (0.75 + 0.5 * rand);
	float h = TreeHeight * (0.8 + 0.4 * rand);

	float3 world = basePos + right * (input.Data.x * w) + up * (input.Data.y * h);

	output.Position = mul(mul(float4(world, 1.0), View), Projection);
	output.UV = input.Data.xy;

	return output;
}

float4 AcaciaPS(AcaciaVertexOutput input) : COLOR
{
	float u = input.UV.x; //[-1,1]
	float v = input.UV.y; //[0,1], 0 at the foot, 1 at the top

	//Crown: a wide, vertically-flattened, flat-topped umbrella (acacia). A squashed ellipse whose top is
	//clipped nearly flat.
	float cy = 0.74, cw = 0.94, ch = 0.27;
	float ell = (u / cw) * (u / cw) + ((v - cy) / ch) * ((v - cy) / ch);
	float inCanopy = (ell < 1.0 && v < 0.99) ? 1.0 : 0.0;

	//Trunk: thin, widening a little at the base, up to the crown
	float trunkW = 0.05 * (1.0 + (1.0 - v / cy) * 0.9);
	float inTrunk = (v < cy && abs(u) < trunkW) ? 1.0 : 0.0;

	clip(max(inCanopy, inTrunk) - 0.5);

	float3 ambient = (ZenithColor + HorizonColor) * 0.5;

	float3 color;
	if (inCanopy > 0.5)
	{
		//Matte crown: sky ambient plus a flat sun term, the top a touch lighter than the underside for volume
		float shade = 0.62 + 0.38 * saturate((v - (cy - ch)) / (2.0 * ch));
		color = CanopyColor * (ambient * 0.85 + SunColor * 0.45) * shade;
	}
	else
	{
		color = TrunkColor * (ambient * 0.6 + SunColor * 0.3);
	}

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
