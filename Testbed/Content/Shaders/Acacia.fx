//Scattered acacia trees and low bushes over the savanna - the flat-topped umbrella acacia is what says
//"savanna" at a glance. One upright (cylindrical) billboard per plant, from a static buffer the C# side
//positions on the ground; the pixel shader IS the plant - a thin trunk under a wide flat-topped dappled
//crown for a tree, a low rounded clump for a bush. Alpha-tested (clip), so it writes depth and occludes the
//terrain and other plants correctly (the hard cutout edge is softened by the scene's supersampling). Lit by
//the dome so it takes the scene's mood. One buffer carries both forms: the packed random is in [0,1) for a
//tree and [1,2) for a bush. Testbed-shared, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//For the crown's leaf mottle. It used to be a hash of floor(UV * 11) - one value held flat across each cell
//of an 11x11 grid, with a hard step at every cell edge, which is a checkerboard drawn on every tree (#97).
#include "Noise.fxh"

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
	float2 Dapple : TEXCOORD2;  //the leaf mottle's domain: plant-local WORLD units, pre-scaled and offset
};

//The crown's leaf mottle. Coarsest octave in cycles per WORLD unit, so a leaf clump is a fixed size in the
//scene: a crown is 9 to 15.6 units across and 2.3 to 3.8 thick, which at 1.2 puts roughly eleven to nineteen
//clumps across it and three to five through its thickness. MEAN and STRENGTH are the mottle's centre and its
//spread about it - the mean is the old dapple's exactly (0.78 + 0.22 * uniform), so the savanna's trees keep
//the brightness they were tuned to and only their texture changes.
static const float DAPPLE_FREQUENCY = 1.2;
static const float DAPPLE_MEAN = 0.89;
static const float DAPPLE_STRENGTH = 1.0;

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

	//The mottle is laid out in the plant's OWN world-unit extent, not in UV: in UV a big tree would get the
	//same number of bigger clumps as a small one (so every plant reads the same size), and the billboard is
	//not square, so a round clump would come out stretched. Offset per plant through a hash rather than off
	//`rand` directly - the plants' randoms are only ~1/Count apart, which off a straight multiple would land
	//neighbouring trees inside one noise cell wearing the same pattern.
	float2 plantOffset = NoiseHash22(float2(rand, rand * 3.7 + 1.0)) * 100.0;
	output.Dapple = float2(input.Data.x * w, input.Data.y * h) * DAPPLE_FREQUENCY + plantOffset;

	return output;
}

float4 AcaciaPS(AcaciaVertexOutput input) : COLOR
{
	float u = input.UV.x; //[-1,1]
	float v = input.UV.y; //[0,1], 0 at the foot, 1 at the top
	float rand = input.Kind.x;
	float isBush = input.Kind.y;

	//The mottle's pixel footprint, in the mottle's own units, taken HERE - before the clip and before the
	//crown/trunk branch below. Both of those diverge inside a quad (the trunk runs up the middle of the crown,
	//and the cutout edge crosses it everywhere), and the derivatives of a quad whose lanes took different
	//paths are undefined; off a linear interpolant taken up front they are exact.
	float dappleFootprint = max(length(ddx(input.Dapple)), length(ddy(input.Dapple)));

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
		//Three octaves: past the third the features are under a fifth of a world unit and the band limit has
		//them out before they can be seen. The mottle needs more amplitude than the old cell hash carried, not
		//less - what made a 6% cell hash visible at all was its hard EDGES, and a smooth field of that spread
		//reads as no mottle whatsoever, which is a flat green blob for a crown.
		float3 leaf = lerp(CanopyColor, CanopyColorDry, rand * (isBush > 0.5 ? 1.0 : 0.7) + isBush * 0.2);
		float dapple = DAPPLE_MEAN + DAPPLE_STRENGTH * Fbm2BandLimited(input.Dapple, 3, dappleFootprint);
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
