//Draws a flowering meadow: lush green rolling hills scattered with wildflowers, a clearing the arena sits
//in with the hills rolling up in the distance. Fifth scene variant (NumPad2 cycles ... -> meadow). The
//look is the Windows XP "Bliss" hill - smooth vivid green under a blue sky - and the motion is the wind:
//bands of it comb through the grass, while the shared cloud shadows drift over the whole field. The
//marble/glass arena stays as a platform standing in the meadow.
//
//Real geometry like the desert and the mountains - a camera-centred grid (shared CreateGridMesh on the C#
//side) displaced by a smooth rolling field, low around the arena and rising into hills with distance, its
//normal taken by finite differences. Testbed-only, Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

float2 OriginXZ;
float MeadowLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

float MeadowTime;
float2 WindDirection;

//Grass (linear) and the darker green it varies towards in patches, how much sky fills the flats, and the
//distance over which the field melts into the skyline
float3 GrassColor;
float3 GrassColorDark;
float AmbientStrength;
float HorizonHazeDistance;

//Wind combing the grass: how fast the bright/dark bands travel, how far apart they are, how deep they cut
float WindRippleSpeed;
float WindRippleFrequency;
float WindRippleStrength;

//Fine grass texture (a normal-tilting height field), its amplitude and blades-per-world-unit
float GrassReliefStrength;
float GrassReliefFrequency;

//Wildflowers: how many of the grid cells carry one, how far apart the cells are, and the flower size
float FlowerDensity;
float FlowerSpacing;
float FlowerSize;

float Hash21(float2 p)
{
	p = frac(p * float2(123.34, 456.21));
	p += dot(p, p + 45.32);

	return frac(p.x * p.y);
}

//Gentle rolling hills: smooth sines (not the mountains' ridges), low around the arena centre (world
//origin) and rising into hills with distance, so the meadow is flat where the arena stands and rolls up
//towards the horizon. Sampled three times per vertex for the finite-difference normal.
float TerrainHeight(float2 p)
{
	float dist = length(p);
	float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

	float rolling = 0.5 * sin(dot(p, float2(0.020, 0.015)))
		+ 0.3 * sin(dot(p, float2(-0.013, 0.024)) + 1.5)
		+ 0.2 * sin(dot(p, float2(0.031, 0.026)) + 3.0);

	float basin = ClearingRelief * sin(dot(p, float2(0.05, 0.035)));

	return MeadowLevelY + basin + HillHeight * ramp * (rolling * 0.5 + 0.5);
}

struct MeadowVertexInput
{
	float4 Position : POSITION0;
};

struct MeadowVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
};

MeadowVertexOutput MeadowVS(MeadowVertexInput input)
{
	MeadowVertexOutput output;

	float2 xz = input.Position.xz + OriginXZ;
	float h = TerrainHeight(xz);

	float e = 2.0;
	float hx = TerrainHeight(xz + float2(e, 0.0));
	float hz = TerrainHeight(xz + float2(0.0, e));
	output.WorldNormal = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));

	float3 worldPosition = float3(xz.x, h, xz.y);
	output.WorldPosition = worldPosition;
	output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

	return output;
}

//Tangent-free normal tilt from a height field, as everywhere else in this project
float3 PerturbNormalFromHeight(float3 normal, float3 worldPosition, float height)
{
	float3 dpdx = ddx(worldPosition);
	float3 dpdy = ddy(worldPosition);

	float3 r1 = cross(dpdy, normal);
	float3 r2 = cross(normal, dpdx);

	float determinant = dot(dpdx, r1);
	float3 surfaceGradient = sign(determinant) * (ddx(height) * r1 + ddy(height) * r2);

	return normalize(abs(determinant) * normal - surfaceGradient);
}

//A fine grass texture that drifts on the wind, band-limited against the footprint so it fades to smooth
//green towards the horizon instead of aliasing
float GrassRelief(float2 xz, float footprint)
{
	float2 p = xz + WindDirection * MeadowTime * 0.7;
	float f = GrassReliefFrequency;

	float h = 0.6 * sin(dot(p, normalize(float2(0.9, 0.3))) * f)
		+ 0.4 * sin(dot(p, normalize(float2(-0.4, 1.0))) * f * 1.8);

	return h * saturate(1.0 - footprint * f / 3.14159265) * GrassReliefStrength;
}

float4 MeadowPS(MeadowVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;
	float3 baseNormal = normalize(input.WorldNormal);
	float footprint = length(fwidth(worldPosition.xz));

	//Fine grass texture tilts the normal, so the grass catches the light unevenly and the wind reads on it
	float relief = GrassRelief(worldPosition.xz, footprint);
	float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

	//Grass color, varied in broad patches so the field is not one flat green
	float patch = CloudNoise(worldPosition.xz * 0.15) * 0.5 + 0.5;
	float3 grass = lerp(GrassColorDark, GrassColor, patch);

	//Wind combing the grass: bright and dark bands travelling downwind, the meadow's own motion
	float wind = sin(dot(worldPosition.xz, WindDirection) * WindRippleFrequency + MeadowTime * WindRippleSpeed);
	grass *= 1.0 + wind * WindRippleStrength;

	//Wildflowers: little rosettes rather than dots — a ring of petals around a bright eye, each with its
	//own petal count, size, rotation and colour. One per grid cell that draws one, faded against the
	//footprint so the distant meadow stays clean green instead of a shimmer.
	float2 cell = floor(worldPosition.xz / FlowerSpacing);
	float2 within = frac(worldPosition.xz / FlowerSpacing);
	float present = step(1.0 - FlowerDensity, Hash21(cell));

	//Per-flower character, all off the cell hash
	float petalCount = 5.0 + floor(Hash21(cell + 5.5) * 3.0); //5, 6 or 7 petals
	float rotation = Hash21(cell + 9.9) * 6.2831853;
	float size = FlowerSize * (0.7 + 0.6 * Hash21(cell + 2.2));

	//The centre may only wander in [size, 1-size], so the whole flower stays inside its cell and no petal
	//is cut off by the cell edge (the flower is only evaluated within its own cell's fraction).
	float2 flowerCentre = size + float2(Hash21(cell + 3.1), Hash21(cell + 7.7)) * (1.0 - 2.0 * size);
	float2 delta = within - flowerCentre;
	float radius = length(delta);
	float angle = atan2(delta.y, delta.x);

	//The scalloped outer edge: petalCount rounded lobes around the centre. |cos(N*angle/2)| makes N lobes
	//and, being even in the angle, stays continuous across the atan2 seam; the power rounds the petals out.
	float lobes = pow(abs(cos(petalCount * (angle + rotation) * 0.5)), 0.6);
	float petalEdge = size * (0.34 + 0.66 * lobes);
	float centreEdge = size * 0.3;

	float resolvable = saturate(1.0 - footprint / FlowerSpacing);
	float aa = fwidth(radius) * 1.5 + 1e-4;

	float flowerMask = present * (1.0 - smoothstep(petalEdge - aa, petalEdge + aa, radius)) * resolvable;
	float centreMask = 1.0 - smoothstep(centreEdge - aa, centreEdge + aa, radius);

	//White daisies, yellow buttercups, the odd pink one - all with a warm golden eye
	float pick = Hash21(cell + 13.7);
	float3 petalColor = pick < 0.5 ? float3(0.96, 0.96, 0.92)
		: (pick < 0.80 ? float3(0.97, 0.88, 0.28) : float3(0.90, 0.45, 0.68));
	float3 flowerColor = lerp(petalColor, float3(0.98, 0.74, 0.12), centreMask);

	grass = lerp(grass, flowerColor, flowerMask);

	//Matte grass: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds that
	//drift across the sky sweep their shadows over the field
	float sunlight = CloudSunlight(worldPosition, SunDirection);
	float ndotl = saturate(dot(normal, SunDirection));
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = grass * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

	//Horizon haze: the distant hills soften into the skyline
	float dist = distance(CameraPosition, worldPosition);
	float haze = saturate(dist / HorizonHazeDistance);
	color = lerp(color, HorizonColor, haze * haze);

	return float4(color, 1.0);
}

technique Meadow
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MeadowVS();
		PixelShader = compile PS_SHADERMODEL MeadowPS();
	}
};
