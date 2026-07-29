//Draws a forest clearing: a mossy needle-strewn floor ringed by dark green hills, the round stone island
//standing in the middle of the open ground. Eighth scene variant (cycles ... -> meadow -> forest). The
//look is a temperate woodland glade - cool low green undergrowth, darker and more shadowed than the
//meadow, combed by the same wind and shaded by the same drifting clouds. No wildflowers: a forest floor
//is leaf litter and moss, not a lawn of blooms, so the colour work is in the patchy undergrowth and the
//fine needle relief rather than in scattered rosettes.
//
//Real geometry like the meadow - a camera-centred grid (shared CreateGridMesh on the C# side) displaced
//by a smooth rolling field, low around the arena and rising into tree-covered hills with distance, its
//normal taken by finite differences. Drawn in both executables, Shader Model 5.0, no OPENGL branch.

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

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground (the flat clearing otherwise slices
//across the funnel just below its rim, hiding its depth and swallowing the balls falling through). The Testbed
//sets this to the island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

float ForestLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

//Low-amplitude lumps across the floor - root bulges and the uneven ground a real clearing has, finer and
//closer than the rolling hills. Sampled three times per vertex for the finite-difference normal.
float FloorLumpStrength;
float FloorLumpFrequency;

float ForestTime;
float2 WindDirection;

//Undergrowth (linear): the cool low green the floor varies towards in patches, and the darker shade of the
//needle litter and shadow, how much sky fills the flats (less than the meadow - a clearing is shaded), and
//the distance over which the field melts into the skyline
float3 ForestColor;
float3 ForestColorDark;
float AmbientStrength;
float HorizonHazeDistance;

//Wind combing the undergrowth: how fast the bright/dark bands travel, how far apart they are, how deep they cut
float WindRippleSpeed;
float WindRippleFrequency;
float WindRippleStrength;

//Fine needle/moss texture (a normal-tilting height field), its amplitude and blades-per-world-unit - stronger
//than the meadow's grass relief, because a needle floor reads coarser than a lawn
float NeedleReliefStrength;
float NeedleReliefFrequency;

float Hash21(float2 p)
{
	p = frac(p * float2(123.34, 456.21));
	p += dot(p, p + 45.32);

	return frac(p.x * p.y);
}

//Gentle rolling hills behind the trees, low around the arena centre (world origin) and rising into wooded
//hills with distance, so the clearing is flat where the arena stands and rolls up towards the treeline.
//Overlaid with low lumps so the floor itself is never glass-smooth - a clearing's ground is uneven.
//Sampled three times per vertex for the finite-difference normal.
float TerrainHeight(float2 p)
{
	float dist = length(p);
	float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

	float rolling = 0.5 * sin(dot(p, float2(0.020, 0.015)))
		+ 0.3 * sin(dot(p, float2(-0.013, 0.024)) + 1.5)
		+ 0.2 * sin(dot(p, float2(0.031, 0.026)) + 3.0);

	float basin = ClearingRelief * sin(dot(p, float2(0.05, 0.035)));

	//Floor lumps are present even inside the clearing - the uneven ground the trees stand on - and fade out
	//with the rolling hills so the distant hills stay smooth (their detail is the trees, not the floor).
	float lumps = sin(dot(p, float2(FloorLumpFrequency, FloorLumpFrequency * 0.7)))
		+ 0.5 * sin(dot(p, float2(-FloorLumpFrequency * 0.8, FloorLumpFrequency * 1.1)) + 2.0);
	float lumpHeight = FloorLumpStrength * lumps * (1.0 - ramp * 0.5);

	return ForestLevelY + basin + lumpHeight + HillHeight * ramp * (rolling * 0.5 + 0.5);
}

struct ForestVertexInput
{
	float4 Position : POSITION0;
};

struct ForestVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
};

ForestVertexOutput ForestVS(ForestVertexInput input)
{
	ForestVertexOutput output;

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

//A fine needle/moss texture that drifts on the wind, band-limited against the footprint so it fades to smooth
//green towards the horizon instead of aliasing. Coarser than the meadow's grass relief: a needle floor reads
//at a lower frequency than a mown lawn.
float NeedleRelief(float2 xz, float footprint)
{
	float2 p = xz + WindDirection * ForestTime * 0.7;
	float f = NeedleReliefFrequency;

	float h = 0.6 * sin(dot(p, normalize(float2(0.9, 0.3))) * f)
		+ 0.4 * sin(dot(p, normalize(float2(-0.4, 1.0))) * f * 1.8);

	return h * saturate(1.0 - footprint * f / 3.14159265) * NeedleReliefStrength;
}

float4 ForestPS(ForestVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;

	//Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
	clip(length(worldPosition.xz) - IslandHoleRadius);

	float3 baseNormal = normalize(input.WorldNormal);
	float footprint = length(fwidth(worldPosition.xz));

	//Fine needle/moss texture tilts the normal, so the floor catches the light unevenly and the wind reads on it
	float relief = NeedleRelief(worldPosition.xz, footprint);
	float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

	//Undergrowth colour: mossy green in broad patches, varying towards the dark needle litter and shadow, so
	//the floor is not one flat green but mottled the way a real clearing is
	float patch = CloudNoise(worldPosition.xz * 0.15) * 0.5 + 0.5;
	float3 floor = lerp(ForestColorDark, ForestColor, patch);

	//Wind combing the undergrowth: bright and dark bands travelling downwind, the clearing's own motion
	float wind = sin(dot(worldPosition.xz, WindDirection) * WindRippleFrequency + ForestTime * WindRippleSpeed);
	floor *= 1.0 + wind * WindRippleStrength;

	//Scattered darker litter patches - the fallen needles and leaf decay that sit between the moss, finer than
	//the broad colour patches and darker still, so the floor reads as layered ground cover rather than one tone
	float litter = CloudNoise(worldPosition.xz * 0.6 + 47.0) * 0.5 + 0.5;
	floor = lerp(floor, ForestColorDark * 0.7, litter * litter * 0.4);

	//Matte forest floor: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds
	//that drift across the sky sweep their shadows over the clearing. Lower ambient than the meadow: a clearing
	//is shaded by the trees around it, not open sky.
	float sunlight = CloudSunlight(worldPosition, SunDirection);
	float ndotl = saturate(dot(normal, SunDirection));
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = floor * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

	//Horizon haze: the distant wooded hills soften into the skyline
	float dist = distance(CameraPosition, worldPosition);
	float haze = saturate(dist / HorizonHazeDistance);
	color = lerp(color, HorizonColor, haze * haze);

	return float4(color, 1.0);
}

technique Forest
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL ForestVS();
		PixelShader = compile PS_SHADERMODEL ForestPS();
	}
};
