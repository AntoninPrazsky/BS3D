//Draws a snowy mountain range: a basin the arena sits in, ringed by craggy snow-capped peaks rising with
//distance into an alpine haze. Fourth scene variant (NumPad2); the island stands in the basin and snow
//falls over it (Snow.fx).
//
//Reworked for realism (the first version was smooth low-poly white lumps): a finer camera-centred grid
//(360 a side, drawn through a 32-bit index buffer - see CreateGridMesh; a 16-bit one wrapped at 65k
//vertices and stretched the far peaks into dark bands across the sky) carries the massing, its per-vertex
//finite-difference normal interpolated as the smooth base. On top of it a fine ROCK RELIEF perturbs the
//normal per pixel (band-limited against the footprint) so the rock faces read as rough stone, not flat
//shading. And the snow is no longer a clean slope line: it lies by slope AND altitude, with an irregular,
//noisy snowline and patchy rock beneath, so the range reads as rock with snow on it rather than one white
//blanket. Camera-centred grid snapped to its cell so it does not swim. Shader Model 5.0.

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

float MountainLevelY;
float MountainHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

//Snow (near white) and two bare-rock tones (dark and a lighter grey-brown), varied in patches. The
//surface-normal Y band the snow lies over (flat/gentle keeps snow, steep sheds it), and the altitude band
//the snowline sits in (below it is rock, above it snow) - both linear.
float3 SnowColor;
float3 RockColor;
float3 RockColorLight;
float RockSlope;
float SnowSlope;
float SnowlineLow;
float SnowlineHigh;

//Fine rock relief: a normal-tilting height field on the rock faces, its amplitude and features per world unit
float RockReliefStrength;
float RockReliefFrequency;

float AmbientStrength;
float HorizonHazeDistance;

//Ridged mountain field, roughly [0,1]: octaves of 1-|sin| (whose sharp maxima are ridge lines), a big smooth
//one for the massing down to fine ones that cut crags into it.
float MountainField(float2 p)
{
	return 0.42 * (1.0 - abs(sin(dot(p, float2(0.031, 0.019)))))
		+ 0.28 * (1.0 - abs(sin(dot(p, float2(-0.017, 0.036)) + 1.1)))
		+ 0.18 * (1.0 - abs(sin(dot(p, float2(0.045, 0.052)) + 2.3)))
		+ 0.12 * (1.0 - abs(sin(dot(p, float2(-0.083, 0.061)) + 3.7)));
}

//The terrain displacement at a world XZ: a gentle basin around the arena (world origin) rising into peaks
//with distance. Evaluated three times per pixel for the finite-difference normal.
float TerrainHeight(float2 p)
{
	float dist = length(p);
	float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

	float basin = ClearingRelief * (sin(dot(p, float2(0.06, 0.04))) + 0.6 * sin(dot(p, float2(-0.05, 0.08)) + 2.0));

	return MountainLevelY + basin + MountainHeight * ramp * MountainField(p);
}

struct MountainVertexInput
{
	float4 Position : POSITION0;
};

struct MountainVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
};

MountainVertexOutput MountainVS(MountainVertexInput input)
{
	MountainVertexOutput output;

	float2 xz = input.Position.xz + OriginXZ;
	float height = TerrainHeight(xz);

	//Base normal per vertex (finite differences) and interpolated, not per pixel. On this mostly-distant,
	//steep range a per-pixel finite-difference normal aliases into shimmer on the far faces; the smooth
	//per-vertex normal reads cleaner and the finer grid plus the per-pixel rock relief carry the detail.
	float e = 2.0;
	float hx = TerrainHeight(xz + float2(e, 0.0));
	float hz = TerrainHeight(xz + float2(0.0, e));
	output.WorldNormal = normalize(float3(-(hx - height) / e, 1.0, -(hz - height) / e));

	float3 worldPosition = float3(xz.x, height, xz.y);
	output.WorldPosition = worldPosition;
	output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

	return output;
}

//Tangent-free normal tilt from a height field (Christian Schueler), as everywhere else in this project
float3 PerturbNormalFromHeight(float3 normal, float3 worldPosition, float height)
{
	float3 dpdx = ddx(worldPosition);
	float3 dpdy = ddy(worldPosition);

	float3 r1 = cross(dpdy, normal);
	float3 r2 = cross(normal, dpdx);

	float determinant = dot(dpdx, r1);
	float3 surfaceGradient = sign(determinant) * (ddx(height) * r1 + ddy(height) * r2);

	//Guard the determinant off zero: at the far peaks' grazing angles ddx/ddy of the world position go
	//near-degenerate, and abs(determinant) hitting 0 makes normalize(0) return NaN. The floor keeps it alive.
	return normalize(max(abs(determinant), 1e-4) * normal - surfaceGradient);
}

//One rock-relief octave, band-limited against the pixel footprint so the crags fade to smooth towards the
//horizon rather than aliasing
float RockOctave(float2 xz, float2 dir, float frequency, float footprint)
{
	float resolvable = saturate(1.0 - footprint * frequency / 3.14159265);
	return sin(dot(xz, dir) * frequency) * resolvable;
}

//A few crossed octaves of rough rock texture
float RockRelief(float2 xz, float footprint)
{
	float f = RockReliefFrequency;

	float h = 0.5 * RockOctave(xz, normalize(float2(0.9, 0.4)), f, footprint)
		+ 0.3 * RockOctave(xz, normalize(float2(-0.5, 0.85)), f * 1.9, footprint)
		+ 0.2 * RockOctave(xz, normalize(float2(0.3, -0.95)), f * 3.6, footprint);

	return h * RockReliefStrength;
}

float4 MountainPS(MountainVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;
	float dist = distance(CameraPosition, worldPosition);
	float footprint = length(fwidth(worldPosition.xz));

	//Smooth per-vertex base normal (see the vertex shader for why not per pixel)
	float3 baseNormal = normalize(input.WorldNormal);

	//Fine rock relief roughens the faces, band-limited against the footprint so it fades to smooth towards the
	//horizon - which also keeps it off the distant faces, leaving the clean per-vertex normal to do the work there
	float relief = RockRelief(worldPosition.xz, footprint) * (1.0 - 0.6 * saturate(baseNormal.y));
	float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

	//Snow lies where the surface is BOTH up-facing (flats/shoulders keep it, cliffs shed it) AND high enough
	//(above an irregular, noisy snowline); rock shows on the steep faces and below the line. Noise breaks the
	//snowline so it is drifts and patches, not a clean contour.
	float slopeSnow = smoothstep(RockSlope, SnowSlope, normal.y);
	float snowNoise = CloudNoise(worldPosition.xz * 0.03) * 9.0;
	float altSnow = smoothstep(SnowlineLow + snowNoise, SnowlineHigh + snowNoise, worldPosition.y);
	float snowDetailed = slopeSnow * saturate(altSnow + 0.15); //a little snow even on lower shoulders

	//Fade the snow/rock DETAIL to a smooth snowy value with distance (footprint): the sharp rock/snow split
	//aliases into a crawl on the far, stacked ranges, so smoothing it there leaves clean snowy peaks while the
	//near and mid slopes keep the rock/snow detail.
	float detailFade = saturate(1.0 - footprint * 0.05);
	float snow = lerp(0.72, snowDetailed, detailFade);

	//Rock varies between a dark and a lighter grey-brown in patches, so the faces are not one flat colour
	float rockPatch = saturate(CloudNoise(worldPosition.xz * 0.08 + 21.0) * 0.5 + 0.5);
	float3 rock = lerp(RockColor, RockColorLight, rockPatch * detailFade);
	float3 albedo = lerp(rock, SnowColor, snow);

	float sunlight = CloudSunlight(worldPosition, SunDirection);
	float ndotl = saturate(dot(normal, SunDirection));

	//Hemisphere sky light: up-facing takes the zenith, slopes take the horizon. The blue zenith filling the
	//shadows gives the snow its cold cast where the sun misses it.
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

	//Alpine haze: the distant range fades into the skyline, the strong aerial perspective of a lot of cold air
	float haze = saturate(dist / HorizonHazeDistance);
	color = lerp(color, HorizonColor, haze * haze);

	return float4(color, 1.0);
}

technique Mountain
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MountainVS();
		PixelShader = compile PS_SHADERMODEL MountainPS();
	}
};
