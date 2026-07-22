//Scattered acacia trees and low bushes over the savanna - the flat-topped umbrella acacia is what says
//"savanna" at a glance. One upright (cylindrical) billboard per plant, from a static buffer the C# side
//positions on the ground; the pixel shader IS the plant - a thin trunk under a wide flat-topped dappled
//crown for a tree, a low rounded clump for a bush. Alpha-tested (clip), so it writes depth and occludes the
//terrain and other plants correctly (the hard cutout edge is softened by the scene's supersampling). Lit by
//the dome so it takes the scene's mood. One buffer carries both forms: the packed random is in [0,1) for a
//tree and [1,2) for a bush. Testbed-shared, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//Base half-width and height of a tree in world units (varied per plant by the packed random)
float TreeWidth;
float TreeHeight;

//The green a crown varies towards (the drier, yellower shade) and the trunk brown
float3 CanopyColor;
float3 CanopyColorDry;
float3 TrunkColor;

struct AcaciaVertexInput
{
	float4 Position : POSITION0; //Plant base position on the ground
	float3 Data : TEXCOORD0;     //(corner u in {-1,1}, corner v in {0,1}, packed random: [0,1) tree, [1,2) bush)
};

struct AcaciaVertexOutput
{
	float4 Position : SV_POSITION;
	float2 UV : TEXCOORD0;      //u across the billboard [-1,1], v up it [0,1]
	float2 Kind : TEXCOORD1;    //(per-plant random 0..1, isBush 0/1)
};

float Hash21(float2 p)
{
	p = frac(p * float2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return frac(p.x * p.y);
}

AcaciaVertexOutput AcaciaVS(AcaciaVertexInput input)
{
	AcaciaVertexOutput output;

	float3 basePos = input.Position.xyz;
	float packed = input.Data.z;
	float isBush = step(1.0, packed);
	float rand = frac(packed);

	//Upright billboard: up is world up (plants stay vertical), right is horizontal, perpendicular to the view
	float3 toCam = CameraPosition - basePos;
	toCam.y = 0.0;
	float3 right = normalize(cross(float3(0.0, 1.0, 0.0), normalize(toCam)));
	float3 up = float3(0.0, 1.0, 0.0);

	//Trees are tall and wide; bushes are short and squat. Sizes vary per plant.
	float w = lerp(TreeWidth * (0.75 + 0.55 * rand), TreeWidth * (0.35 + 0.30 * rand), isBush);
	float h = lerp(TreeHeight * (0.75 + 0.5 * rand), TreeHeight * (0.22 + 0.16 * rand), isBush);

	float3 world = basePos + right * (input.Data.x * w) + up * (input.Data.y * h);

	output.Position = mul(mul(float4(world, 1.0), View), Projection);
	output.UV = input.Data.xy;
	output.Kind = float2(rand, isBush);

	return output;
}

float4 AcaciaPS(AcaciaVertexOutput input) : COLOR
{
	float u = input.UV.x; //[-1,1]
	float v = input.UV.y; //[0,1], 0 at the foot, 1 at the top
	float rand = input.Kind.x;
	float isBush = input.Kind.y;

	//Wobble the silhouette a little per plant so no two read identical and the edge is not a clean arc
	float wob = 0.06 * sin(u * 9.0 + rand * 30.0);

	float inCrown, inTrunk, crownShade;

	if (isBush > 0.5)
	{
		//Bush: a low rounded clump, no trunk, a touch wider than tall
		float bx = u / 0.96, by = (v - 0.5) / 0.52;
		inCrown = (bx * bx + by * by < 1.0 + wob) ? 1.0 : 0.0;
		inTrunk = 0.0;
		crownShade = 0.55 + 0.45 * saturate(v / 0.9); //darker at the base of the clump
	}
	else
	{
		//Acacia crown: a WIDE, FLAT-TOPPED umbrella - flat across the top, bulging down in the middle. Built as
		//a flat top edge at topV and a domed lower edge that curves up towards the rim, so it is thick in the
		//middle and thins to the sides.
		float cw = 0.98, topV = 0.9, thickness = 0.34;
		float atSide = 1.0 - saturate(abs(u) / cw);
		float lowerEdge = topV - thickness * sqrt(saturate(atSide)) - thickness * 0.35 * atSide;
		inCrown = (abs(u) < cw + wob && v <= topV + wob && v >= lowerEdge) ? 1.0 : 0.0;

		//Trunk: thin, widening a little at the base, up to the crown's underside
		float trunkTop = 0.6;
		float trunkW = 0.045 * (1.0 + (1.0 - v / trunkTop) * 1.1);
		inTrunk = (v < trunkTop && abs(u) < trunkW) ? 1.0 : 0.0;

		crownShade = 0.55 + 0.45 * saturate((v - lowerEdge) / max(topV - lowerEdge, 0.1)); //lit top, shaded underside
	}

	clip(max(inCrown, inTrunk) - 0.5);

	float3 ambient = (ZenithColor + HorizonColor) * 0.5;

	float3 color;
	if (inCrown > 0.5)
	{
		//Per-plant green, dappled with a leafy texture so the crown is not one flat colour. Bushes drier/olive.
		float3 leaf = lerp(CanopyColor, CanopyColorDry, rand * (isBush > 0.5 ? 1.0 : 0.7) + isBush * 0.2);
		float dapple = 0.78 + 0.22 * Hash21(floor(input.UV * 11.0) + rand * 17.0);
		color = leaf * (ambient * 0.9 + SunColor * 0.5) * crownShade * dapple;
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
