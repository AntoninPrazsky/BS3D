//Draws a Sahara: rolling sand dunes stretching to a hazed horizon, alive with wind. It is a scene variant
//(NumPad2 cycles city -> sea -> savanna -> desert -> mountain -> ...); the round stone island stays as the
//platform standing in the sand, exactly as it floats on the sea and stands in the grass.
//
//Nothing grows in a Sahara, so what moves is the wind and the sand it carries: fine ripples crawl down the
//dunes and a veil of blown dust drifts across them, both scrolling downwind off the wall clock. The dunes
//themselves are REAL geometry - a camera-centred grid (the shared CreateGridMesh, like the sea, savanna,
//mountains and meadow) displaced in the vertex shader and snapped to its own cell on the CPU each frame, so
//the surface never swims as the camera moves - because a dune with no silhouette against the sky is just a
//lit patch of floor. Flat in a clearing the island stands in (world origin), rising into dunes with distance,
//the same clearing-then-terrain shape its sibling scenes use since the arena became the small round island.
//
//The one thing the terrain does deliberately, and the whole reason this scene was reworked before: the base
//NORMAL is taken PER PIXEL from the height field's own gradient (three cheap DesertHeight taps in the pixel
//shader), NOT interpolated from a per-vertex normal. Interpolating a coarse displaced mesh's per-vertex normal
//is exactly what left a faint facet / Mach-band grid across the old dunes; evaluating the gradient per pixel
//makes the shading smooth regardless of tessellation, so the grid is gone (this is what the savanna's rework
//taught, applied here so the Sahara can come back clean).
//
//Shared between the game and the map editor (both build it for Shader Model 5.0, there being no OPENGL build
//of any shader). It borrows the scene toolkit: the sky is the dome's two-color gradient in linear radiance,
//the ripples band-limit against the pixel footprint like every other procedural feature here, and the cloud
//shadow is the one shared field in Clouds.fxh, so the sand darkens under the very cloud the sky shows overhead
//(the editor never sets the cloud uniforms, so there CloudSunlight is a flat 1.0 - full sun, no shadow).

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, and the sun's own radiance (the lit-cloud color the weather uses, tinted by the dome)
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), and the dune-shape dials: the mean
//sand level in the clearing (the island's foot), peak dune height, and the flat clearing that rises into
//dunes over its transition band - the same shape the savanna/mountains/meadow use around the round island.
float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground (the flat clearing otherwise slices
//across the funnel just below its rim, hiding its depth and swallowing the balls falling through). The Testbed
//sets this to the island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

float DesertLevelY;
float DuneAmplitude;
float ClearingRadius;
float ClearingTransition;

//Wall clock (seconds) and the wind, a unit direction in the XZ plane
float DesertTime;
float2 WindDirection;

//Fine wind ripples: peak height, ripples per world unit, and how fast they crawl downwind
float RippleAmplitude;
float RippleFrequency;
float RippleSpeed;

//Blown dust: how strong the veil is, how fast it drifts, and the distance it starts thickening over
float DustStrength;
float DustSpeed;
float DustStart;

//Sand reflectance (linear) and how much of the sky's hemisphere light reaches the flats
float3 SandColor;
float AmbientStrength;

//World distance over which the dunes melt into the skyline haze
float HorizonHazeDistance;

//Rolling dunes: a low-frequency sine sum along mixed directions, amplitudes summing to ~1.1, so DuneAmplitude
//is roughly the peak dune height. Direction-mixed so it reads as dunes rather than one axis of ridges.
float DuneSum(float2 p)
{
	return 0.52 * sin(dot(p, float2(0.090, 0.052)))
		+ 0.29 * sin(dot(p, float2(-0.041, 0.101)) + 1.7)
		+ 0.19 * sin(dot(p, float2(0.071, -0.083)) + 3.1)
		+ 0.10 * sin(dot(p, float2(0.163, 0.128)) + 5.2);
}

//The full displaced sand height at a world point: flat at DesertLevelY inside the clearing around the island,
//rising into dunes with distance. Tapped both to displace the vertex (VS) and, thrice, for the per-pixel
//normal (PS) - the one field, so the two can never drift apart.
float DesertHeight(float2 p)
{
	float dist = length(p);
	float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

	return DesertLevelY + DuneAmplitude * ramp * DuneSum(p);
}

struct DesertVertexInput
{
	float4 Position : POSITION0;
};

struct DesertVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
};

DesertVertexOutput DesertVS(DesertVertexInput input)
{
	DesertVertexOutput output;

	//Local grid position + the snapped origin gives the world XZ; the dunes are sampled there, so they sit
	//still in the world while the grid slides under them
	float2 worldXZ = input.Position.xz + OriginXZ;
	float3 worldPosition = float3(worldXZ.x, DesertHeight(worldXZ), worldXZ.y);

	output.WorldPosition = worldPosition;
	output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

	return output;
}

//One fine ripple octave, band-limited against the pixel footprint like ReliefOctave, so the ripples fade
//into smooth sand towards the horizon rather than aliasing into a shimmer. Accumulated as a height for
//PerturbNormalFromHeight to tilt the normal by.
float SandRipple(float2 xz, float2 dir, float frequency, float footprint)
{
	float resolvable = saturate(1.0 - footprint * frequency / 3.14159265);

	return sin(dot(xz, dir) * frequency) * resolvable;
}

//The wind-ripple height field: a few octaves crossing the wind, scrolling downwind so the sand crawls.
float RippleHeight(float2 xz, float footprint)
{
	float2 drift = WindDirection * DesertTime * RippleSpeed;
	float2 p = xz + drift;

	float f = RippleFrequency;

	float h = 0.55 * SandRipple(p, normalize(float2(0.95, 0.31)), f, footprint)
		+ 0.30 * SandRipple(p, normalize(float2(0.72, -0.69)), f * 1.9, footprint)
		+ 0.15 * SandRipple(p, normalize(float2(-0.52, 0.85)), f * 3.3, footprint);

	return h * RippleAmplitude;
}

//Tangent-free normal tilt from a height field (Christian Schueler), the same one the balls and the ground
//relief use - the grid carries no tangents and the ripples never reach it anyway.
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

float4 DesertPS(DesertVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;

	//Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
	clip(length(worldPosition.xz) - IslandHoleRadius);

	float dist = distance(CameraPosition, worldPosition);
	float footprint = length(fwidth(worldPosition.xz));

	//The base dune normal, taken PER PIXEL from the height field's gradient (three cheap taps) rather than
	//interpolated from a per-vertex normal - this is what removes the coarse mesh's facet/grid pattern.
	float e = 1.5;
	float h = DesertHeight(worldPosition.xz);
	float hx = DesertHeight(worldPosition.xz + float2(e, 0.0));
	float hz = DesertHeight(worldPosition.xz + float2(0.0, e));
	float3 duneNormal = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));

	//Fine wind ripples tilt the dune normal; they carry the whole sense of a surface crawling in the wind
	float ripple = RippleHeight(worldPosition.xz, footprint);
	float3 normal = PerturbNormalFromHeight(duneNormal, worldPosition, ripple);

	//Sand is a matte diffuse surface: the sun rakes the dunes (lit windward faces, shadowed lee ones) and
	//the sky fills the rest. The cloud shadow dims the sun exactly as it does for the whole scene.
	float sunlight = CloudSunlight(worldPosition, SunDirection);
	float ndotl = saturate(dot(normal, SunDirection));

	//Hemisphere sky light: up-facing sand takes the zenith, slopes towards the skyline take the horizon
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = SandColor * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

	//Blown dust: a veil of sand-colored haze drifting downwind, thickening with distance so the far dunes
	//dissolve into a windblown murk. Its noise comes from the shared cloud field's generator (included
	//above), scrolled along the wind.
	float2 dustP = (worldPosition.xz + WindDirection * DesertTime * DustSpeed) * 0.03;
	float dust = saturate(CloudNoise(dustP) * 0.5 + 0.5);
	dust *= DustStrength * saturate(dist / DustStart);
	float3 dustColor = SandColor * skyAmbient * 2.0 + HorizonColor * 0.4;
	color = lerp(color, dustColor, saturate(dust));

	//Horizon haze: the finite grid melts into the skyline color, so it has no edge and no seam with the dome
	float haze = saturate(dist / HorizonHazeDistance);
	color = lerp(color, HorizonColor, haze * haze);

	return float4(color, 1.0);
}

technique Desert
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL DesertVS();
		PixelShader = compile PS_SHADERMODEL DesertPS();
	}
};
