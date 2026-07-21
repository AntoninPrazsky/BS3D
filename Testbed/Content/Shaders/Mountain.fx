//Draws a snowy mountain landscape: a snow basin the arena sits in, ringed by tall snow-capped peaks that
//rise with distance and fade into an alpine haze. Fourth scene variant (NumPad2 cycles city -> sea ->
//desert -> mountain); the marble/glass arena stays as a platform standing in the basin, and snow falls
//over the whole thing (Snow.fx).
//
//Real geometry like the desert - a camera-centred grid displaced by a terrain field, snapped to its cell
//on the CPU so it does not swim - but the field is ridged into peaks rather than rolling dunes, and it is
//low around the arena (world origin) and rises with distance, so the play surface sits in a clearing the
//way it sits in the city. The normal is taken by finite differences (three field taps) rather than
//analytically, because the ridge folds and the distance ramp make a closed-form gradient more trouble than
//it is worth. Snow settles on the flats and gentle slopes, bare rock shows on the steep faces.
//Testbed-only, Shader Model 5.0, no OPENGL branch.

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

//Basin floor level, peak height far out, and the ramp that lifts the peaks out of the basin: flat within
//ClearingRadius of the arena centre, full height ClearingTransition beyond it. ClearingRelief is the
//gentle undulation of the snow in the basin itself.
float MountainLevelY;
float MountainHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

//Snow (linear, near white) and bare rock (linear, dark), and the surface-normal Y between which the
//surface goes from all rock (steep) to all snow (flat).
float3 SnowColor;
float3 RockColor;
float RockSlope;
float SnowSlope;

float AmbientStrength;
float HorizonHazeDistance;

//Ridged mountain field, roughly [0,1]: each octave is 1-|sin|, whose sharp maxima are ridge lines. A big
//smooth octave sets the massing, finer ones cut the ridges into it.
float MountainField(float2 p)
{
	return 0.42 * (1.0 - abs(sin(dot(p, float2(0.031, 0.019)))))
		+ 0.28 * (1.0 - abs(sin(dot(p, float2(-0.017, 0.036)) + 1.1)))
		+ 0.18 * (1.0 - abs(sin(dot(p, float2(0.045, 0.052)) + 2.3)))
		+ 0.12 * (1.0 - abs(sin(dot(p, float2(-0.083, 0.061)) + 3.7)));
}

//The terrain displacement at a world XZ: a gentle basin around the arena centre (world origin) rising
//into peaks with distance. Sampled three times per vertex for the finite-difference normal.
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
	float h = TerrainHeight(xz);

	//Normal by finite differences - robust through the ridge folds and the basin ramp, where an analytic
	//gradient would need the derivative of the |sin| kink and of the smoothstep ramp
	float e = 2.0;
	float hx = TerrainHeight(xz + float2(e, 0.0));
	float hz = TerrainHeight(xz + float2(0.0, e));
	output.WorldNormal = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));

	float3 worldPosition = float3(xz.x, h, xz.y);
	output.WorldPosition = worldPosition;
	output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

	return output;
}

float4 MountainPS(MountainVertexOutput input) : COLOR
{
	float3 normal = normalize(input.WorldNormal);
	float3 worldPosition = input.WorldPosition;
	float dist = distance(CameraPosition, worldPosition);

	//Snow lies on the flats and gentle slopes; the steep faces shed it to bare rock. Decided by how
	//upward the surface faces, which is exactly what keeps snow off a cliff and on a shoulder.
	float snow = smoothstep(RockSlope, SnowSlope, normal.y);
	float3 albedo = lerp(RockColor, SnowColor, snow);

	float sunlight = CloudSunlight(worldPosition, SunDirection);
	float ndotl = saturate(dot(normal, SunDirection));

	//Hemisphere sky light: up-facing surface takes the zenith, slopes towards the skyline take the horizon.
	//The blue zenith filling the shadows is what gives snow its cold cast where the sun does not reach it.
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

	//Alpine haze: the distant range fades into the skyline color, the strong aerial perspective that reads
	//as air over a lot of cold distance
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
