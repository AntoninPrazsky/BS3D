//Blown sea spray and spindrift over the sea scene: a slab of billboard droplets hugging the water surface,
//whipped downwind by the storm. Like the mountain's snow it is a static vertex buffer (one quad per
//particle, its base a fixed random point in a unit cube) animated entirely in the vertex shader - but the
//slab is thin and anchored to the sea surface (not centred on the camera in Y), and the wind is strong and
//near-horizontal, so the particles read as spray torn off the waves rather than a falling veil. Each quad is
//stretched along the wind's screen projection, so a particle reads as a blown streak and not a disc (#169).
//
//One buffer carries two reads, split per particle: fine bright DROPLETS (the spray itself) and larger,
//much fainter MIST wisps (the spindrift haze that gives the storm its atmosphere). The slab follows the
//camera in XZ and feathers out at its edges and towards its top, so it has no hard boundary. Drawn last in
//the sea scene, alpha-blended and depth-read (the waves and platform occlude it) but writing no depth.
//Testbed-shared (the map editor builds it too, since SceneRenderer loads it), Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;

//The camera, and its right/up in world space for billboarding the particles towards it
float3 CameraPosition;
float3 CameraRight;
float3 CameraUp;

float SprayTime;

//The slab the particles fill: XZ extent (follows the camera) and Y thickness, the world Y it is centred on
//(just above the mean sea level), the strong downwind drift (world units/sec, XZ), a slow vertical churn,
//and the per-particle turbulent sway amplitude.
float3 SprayBoxSize;
float SprayLevelY;
float2 SprayWind;
float SprayRise;
float SprayTurb;

//Base billboard size (world units), the spray color (linear, kept under the glare threshold so a near
//droplet does not bloom into an orb), and the overall opacity.
float DropletSize;
float3 SprayColor;
float SprayOpacity;

struct SprayVertexInput
{
	float4 Position : POSITION0; //Base position of the particle, a fixed random point in the unit cube
	float3 Data : TEXCOORD0;     //(corner x, corner y in {-1,1}, per-particle random)
};

struct SprayVertexOutput
{
	float4 Position : SV_POSITION;
	float2 Corner : TEXCOORD0;
	float Alpha : TEXCOORD1;
};

SprayVertexOutput SprayVS(SprayVertexInput input)
{
	SprayVertexOutput output;

	float3 b = input.Position.xyz;
	float r = input.Data.z;

	//Animate the base point within [0,1): blown fast downwind, with a slow vertical churn, wrapping with frac
	float2 drift = SprayTime * SprayWind / SprayBoxSize.xz;
	float rise = SprayTime * SprayRise / SprayBoxSize.y;

	float3 o;
	o.x = frac(b.x + drift.x);
	o.y = frac(b.y + rise + r);
	o.z = frac(b.z + drift.y);

	//Into a slab whose XZ follows the camera but whose Y is anchored to the sea surface, with a turbulent
	//per-particle sway so the spray churns rather than sliding in lockstep
	float3 boxPosition = (o - 0.5) * SprayBoxSize;
	float ph = r * 40.0;
	boxPosition.x += sin(SprayTime * 1.7 + ph) * SprayTurb;
	boxPosition.y += sin(SprayTime * 2.3 + ph * 1.7) * SprayTurb * 0.6;
	boxPosition.z += cos(SprayTime * 1.9 + ph * 1.3) * SprayTurb;

	float3 center = float3(CameraPosition.x + boxPosition.x, SprayLevelY + boxPosition.y, CameraPosition.z + boxPosition.z);

	//Two classes from the one random: fine droplets (the spray itself) and a few larger, much fainter mist
	//wisps (the spindrift haze). The wisps stay small - large billboards read as bokeh orbs, not mist - and
	//those were the round white "snowballs" of #169, so their size multiplier is cut back here on top of the
	//streak shape below.
	float isMist = step(r, 0.4);
	float size = DropletSize * lerp(0.5 + 1.0 * r, 1.8 + 1.6 * r, isMist);
	float classOpacity = lerp(1.0, 0.14, isMist);

	//Spray is torn off the waves and blown DOWNWIND, so it reads as streaks and flecks, not discs (#169). The
	//billboard is stretched along the wind's direction as that projects onto the screen and thinned across it,
	//with the AREA preserved so the per-particle coverage - and thus the particle stacking the colour is kept
	//under the glare threshold for - does not change. How far it stretches scales with how side-on the wind is
	//to the lens: the wind direction dotted onto the camera's screen axes has length sin(angle-to-view), so a
	//head-on wind foreshortens to a disc on its own (correct - a streak seen end-on IS a dot) while a crosswind
	//draws the longest streak, and the whole thing tracks the orbiting camera for free. A small per-particle
	//turn off the wind, from a hash of r decorrelated from the size class, keeps the streaks out of one comb.
	float3 windDir = normalize(float3(SprayWind.x, 0.0, SprayWind.y));
	float2 windScreen = float2(dot(windDir, CameraRight), dot(windDir, CameraUp));
	float sideOn = length(windScreen);
	float2 axisLong = sideOn > 1e-4 ? windScreen / sideOn : float2(1.0, 0.0);

	float jitterAngle = (frac(r * 17.0 + 0.37) - 0.5) * 0.7;
	float cj = cos(jitterAngle), sj = sin(jitterAngle);
	axisLong = float2(axisLong.x * cj - axisLong.y * sj, axisLong.x * sj + axisLong.y * cj);
	float2 axisShort = float2(-axisLong.y, axisLong.x);

	float stretch = 1.0 + 1.7 * sideOn;
	float2 local = input.Data.xy * float2(stretch, 1.0 / stretch) * size;
	float2 screenOffset = local.x * axisLong + local.y * axisShort;

	float3 world = center + CameraRight * screenOffset.x + CameraUp * screenOffset.y;

	//Feather the slab: fade towards its XZ edges (no hard boundary as the box follows the camera) and towards
	//its top, so the spray is densest right on the water and thins with height
	float edge = saturate((1.0 - 2.0 * max(abs(o.x - 0.5), abs(o.z - 0.5))) * 3.0);
	float heightFade = saturate((1.0 - o.y) * 1.4 + 0.15);

	output.Position = mul(mul(float4(world, 1.0), View), Projection);
	output.Corner = input.Data.xy;
	output.Alpha = edge * heightFade * classOpacity;

	return output;
}

float4 SprayPS(SprayVertexOutput input) : COLOR
{
	//Soft mask, opaque at the centre and feathered to nothing at the rim. It is round in corner space, but the
	//quad it rides was stretched along the wind (see SprayVS), so on screen it is a wind-aligned streak (#169).
	float mask = 1.0 - smoothstep(0.35, 1.0, length(input.Corner));

	float alpha = mask * input.Alpha * SprayOpacity;
	clip(alpha - 0.003);

	return float4(SprayColor, alpha);
}

technique Spray
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SprayVS();
		PixelShader = compile PS_SHADERMODEL SprayPS();
	}
};
