//Draws the open sea: a large flat water plane, animated with gentle procedural ripples, reflecting the
//current sky dome and receiving the same cloud shadows as the rest of the scene. It is the second scene
//variant (NumPad2 switches city <-> sea); the marble/glass arena stays as a platform floating on it.
//
//Testbed-only - the map editor draws no scene backdrop, so unlike InstancedModel.fx this file is never
//built for DesktopGL and needs no Shader Model 3.0 branch. It mirrors the idioms of InstancedModel.fx and
//Sky.fx: the dome is a two-color vertical gradient sampled in closed form (SkyRadiance), features
//band-limit against the pixel footprint the way the clouds and the ground relief do, and the cloud shadow
//comes from the one shared field in Clouds.fxh, so the water darkens under the very cloud the sky shows
//overhead rather than under a second field tuned to look similar.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

float4x4 World;
float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, normalized (the same direction the scene is shadowed and the clouds are lit along)
float3 SunDirection;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline. The water
//mirrors this, so it takes on the mood of whichever of the eighteen skies is up, exactly as the city does.
float3 ZenithColor;
float3 HorizonColor;

//Radiance of the sun itself, for the glint: the same lit-cloud color the weather uses, tinted by the dome.
float3 SunColor;

//Wall clock driving the ripples, in seconds (shared with the balls' pulse and the clouds, so the water
//keeps moving while the simulation is paused).
float SeaTime;

//Deep body color of the water and the paler shade the up-facing wave faces take, both linear and both
//treated as reflectances - they are multiplied by the sky's own light below, so night water goes dark.
float3 WaterColorDeep;
float3 WaterColorShallow;

//Peak wave height (world units), base ripples per world unit, and how fast the phase scrolls
float WaveAmplitude;
float WaveFrequency;
float WaveSpeed;

//How sharp and how bright the sun's reflection sparkles off the crests
float SunGlintStrength;
float SunGlintPower;

//World distance over which the sea melts into the horizon haze (the sky's own skyline color), so the
//finite plane has no visible edge and no hard seam against the dome.
float HorizonHazeDistance;

struct SeaVertexInput
{
	float4 Position : POSITION0;
};

struct SeaVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
};

SeaVertexOutput SeaVS(SeaVertexInput input)
{
	SeaVertexOutput output;

	float4 world = mul(input.Position, World);

	output.WorldPosition = world.xyz;
	output.Position = mul(mul(world, View), Projection);

	return output;
}

//One directional ripple, accumulated as the XZ slope of its height rather than the height itself, since
//the flat plane is shaded off its normal and never displaced. Band-limited against the pixel footprint
//the way ReliefOctave is, so the fine ripples retire into a flat mirror towards the horizon instead of
//aliasing into a shimmer. dir is a unit direction in the XZ plane.
void SeaRipple(float2 xz, float2 dir, float frequency, float speed, float amplitude, float footprint, inout float2 slope)
{
	float resolvable = saturate(1.0 - footprint * frequency / 3.14159265);
	float phase = dot(xz, dir) * frequency + SeaTime * speed;

	//d/dxz of [amplitude * sin(phase)] is amplitude * frequency * cos(phase) * dir
	slope += amplitude * resolvable * frequency * cos(phase) * dir;
}

//The XZ slope of the wave field: five ripples along directions and at frequencies sharing no common
//factor, so the sum never settles into a tile. Gentle ripples, not ocean swell.
float2 SeaSurfaceSlope(float2 xz, float footprint)
{
	float2 slope = 0.0;

	float f = WaveFrequency;
	float a = WaveAmplitude;

	SeaRipple(xz, normalize(float2(1.0, 0.3)),   f,        WaveSpeed,        a,        footprint, slope);
	SeaRipple(xz, normalize(float2(-0.4, 1.0)),  f * 1.7,  WaveSpeed * 1.3,  a * 0.65, footprint, slope);
	SeaRipple(xz, normalize(float2(0.7, -0.8)),  f * 2.9,  WaveSpeed * 0.8,  a * 0.42, footprint, slope);
	SeaRipple(xz, normalize(float2(-0.9, -0.3)), f * 4.6,  WaveSpeed * 1.7,  a * 0.28, footprint, slope);
	SeaRipple(xz, normalize(float2(0.2, 0.98)),  f * 7.3,  WaveSpeed * 2.1,  a * 0.18, footprint, slope);

	return slope;
}

float4 SeaPS(SeaVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;

	//View ray and the pixel's footprint on the plane - the same measure the clouds and the ground relief
	//band-limit against. It grows without bound towards the horizon, which is what silently flattens the
	//ripples into a mirror out there.
	float3 toEye = CameraPosition - worldPosition;
	float dist = length(toEye);
	float3 viewDir = toEye / dist;

	float footprint = length(fwidth(worldPosition.xz));

	//Wave normal from the field's slope: a flat plane tilted by the ripples, no vertex moved - the same
	//normal-from-height idiom as the ball skin and the ground relief.
	float2 slope = SeaSurfaceSlope(worldPosition.xz, footprint);
	float3 normal = normalize(float3(-slope.x, 1.0, -slope.y));

	//Sky reflection. The dome is a vertical gradient, so the reflected ray's height picks between horizon
	//and zenith in closed form - the same trick InstancedModel.fx's SkyRadiance uses for its specular
	//ambient. A grazing view mirrors the low sky near the horizon; looking steeply down shows more zenith.
	float3 reflected = reflect(-viewDir, normal);
	float3 sky = lerp(HorizonColor, ZenithColor, saturate(reflected.y * 0.5 + 0.5));

	//Fresnel: at a grazing angle the water is a mirror, straight down it shows mostly its own body. Water
	//reflects about 2% head-on, but a small floor above that keeps a little sky in the surface even looking
	//straight down, which is what stops stylised water from reading as flat black paint.
	float fresnel = 0.06 + 0.94 * pow(1.0 - saturate(dot(normal, viewDir)), 5.0);

	//How much sun reaches this patch through the clouds - the very field the whole scene is shadowed by
	float sunlight = CloudSunlight(worldPosition, SunDirection);

	//Body color: deep water lit by the sky above it (so a night sea goes dark), the up-facing wave faces
	//a touch paler. Water has almost no light of its own; what you see looking into it is skylight, both
	//scattered back out of the body and a dim always-present tint of the sky straight overhead.
	float3 ambient = (ZenithColor + HorizonColor) * 0.5;
	float3 body = lerp(WaterColorDeep, WaterColorShallow, saturate(normal.y) * 0.5) * ambient + ZenithColor * 0.06;

	//A cloud overhead greys the mirrored sky down a little, since there is less lit blue to reflect
	float3 reflection = sky * lerp(0.65, 1.0, sunlight);

	//Sun glint: a sharp spark where the reflected ray points at the sun, snuffed out under a cloud shadow
	float glint = pow(saturate(dot(reflected, SunDirection)), SunGlintPower) * SunGlintStrength * sunlight;

	float3 color = lerp(body, reflection, fresnel) + glint * SunColor;

	//Horizon haze: melt the sea into the skyline color over distance, so the plane has no visible edge
	float haze = saturate(dist / HorizonHazeDistance);
	color = lerp(color, HorizonColor, haze * haze);

	return float4(color, 1.0);
}

technique Sea
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SeaVS();
		PixelShader = compile PS_SHADERMODEL SeaPS();
	}
};
