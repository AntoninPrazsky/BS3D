//Draws the Sahara: a field of rolling sand dunes stretching to the horizon, alive with wind. It is the
//third scene variant (NumPad2 cycles city -> sea -> desert); the marble/glass arena stays as a platform
//standing in the sand.
//
//Nothing lives in a desert, so what moves is the wind and the sand it carries: fine ripples crawl down
//the dunes and a veil of blown dust drifts across them, both scrolling downwind off the wall clock. The
//dunes themselves are real geometry — a displaced grid, not the sea's flat plane — because a dune with no
//silhouette against the sky is just a lit patch of floor. The grid is recentred on the camera each frame
//and snapped to its own cell on the CPU, so the surface never swims as the camera moves.
//
//Testbed-only (the map editor draws no backdrop), so Shader Model 5.0 with no OPENGL branch. It borrows
//the scene's toolkit: the sky is the dome's two-color gradient sampled in closed form, the fine ripples
//band-limit against the pixel footprint like every other procedural feature here, and the cloud shadow is
//the one shared field in Clouds.fxh, so the sand darkens under the very cloud the sky shows overhead.

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

//Wall clock (seconds) and the wind, a unit direction in the XZ plane
float DesertTime;
float2 WindDirection;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell) and the mean sand level
float2 OriginXZ;
float DesertLevelY;

//Peak dune height in world units above/below the mean
float DuneAmplitude;

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

//Rolling dunes: low-frequency sine octaves along mixed directions. Returns the height and its XZ gradient
//(dh/dx, dh/dz), so the vertex shader can both displace the vertex and build its normal from the one
//evaluation, with no finite differences. Amplitudes sum to about 1, so DuneAmplitude is the peak height.
void DuneField(float2 p, out float height, out float2 grad)
{
	height = 0.0;
	grad = float2(0.0, 0.0);

	float2 k0 = float2(0.090, 0.052);  float a0 = 0.52;
	float2 k1 = float2(-0.041, 0.101); float a1 = 0.29;
	float2 k2 = float2(0.071, -0.083); float a2 = 0.19;
	float2 k3 = float2(0.163, 0.128);  float a3 = 0.10;

	float p0 = dot(p, k0);        height += a0 * sin(p0);       grad += a0 * cos(p0) * k0;
	float p1 = dot(p, k1) + 1.7;  height += a1 * sin(p1);       grad += a1 * cos(p1) * k1;
	float p2 = dot(p, k2) + 3.1;  height += a2 * sin(p2);       grad += a2 * cos(p2) * k2;
	float p3 = dot(p, k3) + 5.2;  height += a3 * sin(p3);       grad += a3 * cos(p3) * k3;
}

struct DesertVertexInput
{
	float4 Position : POSITION0;
};

struct DesertVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
};

DesertVertexOutput DesertVS(DesertVertexInput input)
{
	DesertVertexOutput output;

	//Local grid position + the snapped origin gives the world XZ; the dunes are sampled there, so they sit
	//still in the world while the grid slides under them
	float2 worldXZ = input.Position.xz + OriginXZ;

	float height;
	float2 grad;
	DuneField(worldXZ, height, grad);

	float3 worldPosition = float3(worldXZ.x, DesertLevelY + DuneAmplitude * height, worldXZ.y);

	//Normal straight from the dune slope: dY/dxz = DuneAmplitude * grad
	float2 slope = DuneAmplitude * grad;
	output.WorldNormal = normalize(float3(-slope.x, 1.0, -slope.y));

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
	float3 duneNormal = normalize(input.WorldNormal);

	float dist = distance(CameraPosition, worldPosition);
	float footprint = length(fwidth(worldPosition.xz));

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
