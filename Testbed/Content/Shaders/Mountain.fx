//Draws a snowy mountain range: a basin the arena sits in, ringed by craggy snow-capped peaks rising with
//distance into an alpine haze. Fifth scene variant (NumPad2); the island stands in the basin and snow
//falls over it (Snow.fx).
//
//A camera-centred grid (360 a side, drawn through a 32-bit index buffer - see CreateGridMesh; a 16-bit one
//wrapped at 65k vertices and stretched the far peaks into dark bands across the sky) carries the massing,
//its per-vertex finite-difference normal interpolated as the smooth base. On top of it a fine ROCK RELIEF
//perturbs the normal per pixel (band-limited against the footprint) so the rock faces read as rough stone,
//not flat shading. And the snow is no longer a clean slope line: it lies by slope AND altitude, with an
//irregular, noisy snowline and patchy rock beneath, so the range reads as rock with snow on it rather than
//one white blanket. Camera-centred grid snapped to its cell so it does not swim. Shader Model 5.0.
//
//What decides how the peaks read is the FIELD, not the grid - see MountainField, and #86, which was filed
//against the tessellation and turned out to be about the field the grid was oversampling ninefold.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

//The shared noise library, for the ridged fractal field the massing is built from. Its opening states this
//scene's own defect: a sum of plane-wave sines keeps its planes however many terms it has, and that is what
//the range was made of until #86.
#include "Noise.fxh"

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

//How far apart the first octave's ridges run, in world units; each octave after it halves that. Five puts
//the finest at about 8 units, which the vertex grid's 3.34-unit cell (MOUNTAIN_EXTENT / (MOUNTAIN_GRID_N - 1))
//still carries - a crest the grid cannot hold does not come out finer, it comes out jagged.
static const float MOUNTAIN_RIDGE_SPACING = 130.0;
static const int MOUNTAIN_FIELD_OCTAVES = 5;

//RidgedFbm2 sits high and narrow: measured over the annulus the peaks occupy, mean 0.70 with a standard
//deviation of 0.13, against the plane-wave field's 0.36 and 0.17. Used raw it therefore lifts the whole
//basin and leaves shallow dips in it - a white blanket, not a range. Dropping the floor out and cubing what
//is left puts the terrain back on the height distribution the rest of the scene is tuned against (median
//0.30 and p95 0.67, against the old field's 0.35 and 0.66, so MountainHeight and the snowline both stand),
//and the curve is the ridged look itself: valleys flatten, crests keep their height. Measured slope bears
//that out - the median falls from 0.77 to 0.67 while the p99 rises from 2.48 to 3.72.
static const float MOUNTAIN_FIELD_FLOOR = 0.24;
static const float MOUNTAIN_FIELD_SPAN = 0.72;

//Ridged mountain field, roughly [0,1]: a network of crests with the detail gathered onto them, big enough in
//its first octave to mass the range and fine enough in its last to break the skyline.
//
//This was four octaves of 1-|sin(dot(p, direction))| until #86, and both of that construction's failures
//were on the screen at once. Plane waves keep their planes: every face carried the same long straight
//corrugations, which read as sand ripples rather than rock. And the finest octave's ridges ran about thirty
//units apart - so an eighty-two-unit peak had three features from base to summit and its outline against the
//sky was a smooth simple cone. Nothing about that was the tessellation's fault: the vertex grid samples every
//3.34 units and was oversampling the field ninefold. Noise.fxh's header calls this exact construction out and
//exists to replace it; the mountain was the scene that never got converted.
float MountainField(float2 p)
{
	float ridged = RidgedFbm2(p / MOUNTAIN_RIDGE_SPACING, MOUNTAIN_FIELD_OCTAVES);
	float shaped = saturate((ridged - MOUNTAIN_FIELD_FLOOR) / MOUNTAIN_FIELD_SPAN);

	return shaped * shaped * shaped;
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

//Rough rock texture, and it is FRACTAL NOISE and not crossed sines - which it was until #170, the third and
//last instance of that defect this project has found. #86 took MountainField's own massing off the same
//construction and #117 did the same for the savanna's GrassRelief; this was the one left, and #140 even added
//a fourth sine ONTO it rather than replacing it.
//
//A family of plane waves keeps its planes however many terms it has (Noise.fxh's own header says it), so the
//four octaves - periods 2*pi/f, i.e. about 10.5, 5.5, 2.9 and 1.5 world units at RockReliefFrequency's 0.6 -
//interfered into a legible PLAID over the snow. Captured before it was touched: a cross-hatch in the interior
//of the snowfield, which is also what separates it from snowNoise below (that one would wobble the snow's
//EDGE, not lay a lattice over its middle). It reached the snow because the slope term that scales this floors
//at 0.4 rather than at zero on up-facing ground - see the call site - so flat snow took 40 % of it, and a
//relief feeds the NORMAL, which is the strongest way to make a pattern visible.
//
//Four octaves of ROTATED gradient noise instead: no shared axis, so there is no lattice to read. COMBED along
//one axis (Fbm2Combed), because isotropic noise has no grain and a relief without one reads as gravel rather
//than as rock lying in beds - the direction the dominant sine used to supply for free, kept as ROCK_GRAIN.
//Four and not the two or three the report suggested: the finest sine was added to reach crag scale, and it is
//the fourth octave that still reaches it (coarsest ~10.5 world units down to ~1.3, the old spectrum's span).
static const float2 ROCK_GRAIN = float2(0.9, 0.4);
static const float ROCK_STRETCH = 2.4;
static const float TWO_PI = 6.2831853;

//The amplitude the swap had to buy back, and it is a GAIN here rather than a new RockReliefStrength - #117's
//own choice in the savanna, and for a reason this scene has too: levels pin that figure in their scene config
//(two of the shipped ones do, and a hand-built level nobody has seen may), so moving it would leave them all
//holding a number that now means a fifth of the relief.
//
//About three, and both halves of it are real. The four sines' weights (0.46/0.28/0.16/0.10) put their RMS near
//0.41 where four octaves of Fbm2BandLimited come to about 0.20 - gradient noise spends its time well inside
//its extremes where a sine sits near them - and for equal amplitude and period a sine is half again as steep,
//which matters because it is the field's SLOPE and not its height that tilts the normal. Confirmed by eye
//against the old field from the same camera: the relief reads as strongly as it did, and irregularly.
static const float ROCK_FBM_GAIN = 3.0;

//The authored frequency is CONVERTED rather than reinterpreted, and that is the one thing here that could
//quietly change the look: the config's figure is a sine's angular frequency (period 2*pi/f) where a noise
//field wants a domain scale (a feature is about 1/scale across), so it goes in divided by 2*pi and the
//coarsest feature stays exactly where it was authored. Passing f straight in would make every feature 6.3x
//finer and take the broad undulation out of the terrain.
float RockRelief(float2 xz, float footprint)
{
	float scale = RockReliefFrequency / TWO_PI;

	//The footprint goes in scaled by the same figure, which is Fbm2BandLimited's contract: it wants the
	//pixel's size in the domain's own units, and that is what fades each octave out as its period approaches
	//the pixel - the job RockOctave's own `resolvable` guard used to do for the sines.
	return Fbm2Combed(xz * scale, ROCK_GRAIN, ROCK_STRETCH, 4, footprint * scale)
		* (RockReliefStrength * ROCK_FBM_GAIN);
}

//THE SNOW'S OWN SURFACE, which #208 found missing: the snowfields were flat SnowColor under a 40 % share of
//the ROCK relief, and rock beside them carried grain, patches and per-pixel hash while the snow carried
//nothing - the range read as "beautiful peaks with airbrushed white on them". Two things, both snow-only and
//both band-limited against the footprint like everything else in this shader, because a relief that reaches
//pixel size checkerboards (#170's whole lesson) and a glint that does is a crawling speckle:
//
//WIND-STREAKED DRIFT, the relief: snow does not lie flat, it lies in SASTRUGI - long, shallow ridges the
//wind draws out of it. Combed fbm like the rock's, but on its OWN grain (crossed to it: weather in these
//mountains is one system, and the rock lies in beds while the snow on top lies where the last wind put it)
//and stretched harder - a streak reads as a streak past about three-to-one. Softer than rock by half: a
//drift is a thing you could push a boot through, and a relief strong enough for crag would read as crag.
static const float2 SNOW_GRAIN = float2(-0.55, 0.85);
static const float SNOW_STRETCH = 3.2;
static const float SNOW_FBM_GAIN = 0.5;

float SnowRelief(float2 xz, float footprint)
{
	float scale = RockReliefFrequency / TWO_PI;

	return Fbm2Combed(xz * scale, SNOW_GRAIN, SNOW_STRETCH, 3, footprint * scale)
		* (RockReliefStrength * ROCK_FBM_GAIN * SNOW_FBM_GAIN);
}

//AND SPARKLE, the albedo's half: snow is ice crystals, and what the eye forgives a photograph of snow for
//not resolving is the GLINT - a sparse dust of points bright enough to be specular, laid on a lattice of
//crystal-sized cells. The cells here are ~0.3 world units, NOT crystal-sized, on purpose: at the footprint
//the mid slopes actually draw (~0.08 world/pixel) crystal-scale cells are sub-pixel and a hash over them is
//noise, while a third-of-a-metre cell lands 3-4 pixels wide - the size a glint reads at. Thresholded hard
//(the top ~1.5 % of cells), so it is a dusting and not a wash; sun-facing only, because a glint is a
//reflection; and faded by the footprint the same way as everything else, so the far ranges stay the clean
//hazy shapes the aerial perspective wants.
static const float SNOW_SPARKLE_DENSITY = 0.985;

float SnowSparkle(float2 xz, float3 normal, float footprint)
{
	float fade = saturate(1.0 - footprint * 3.0);
	if (fade <= 0.0) return 0.0;

	float cell = NoiseHash22(floor(xz * 3.33)).x;

	return step(SNOW_SPARKLE_DENSITY, cell) * fade * saturate(normal.y);
}

float4 MountainPS(MountainVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;

	//Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
	clip(length(worldPosition.xz) - IslandHoleRadius);

	float dist = distance(CameraPosition, worldPosition);
	float footprint = length(fwidth(worldPosition.xz));

	//Smooth per-vertex base normal (see the vertex shader for why not per pixel)
	float3 baseNormal = normalize(input.WorldNormal);

	//Fine rock relief roughens the faces, band-limited against the footprint so it fades to smooth towards the
	//horizon - which also keeps it off the distant faces, leaving the clean per-vertex normal to do the work there
	float relief = RockRelief(worldPosition.xz, footprint) * (1.0 - 0.6 * saturate(baseNormal.y));
	float3 rockNormal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

	//Snow lies where the surface is BOTH up-facing (flats/shoulders keep it, cliffs shed it) AND high enough
	//(above an irregular, noisy snowline); rock shows on the steep faces and below the line. Noise breaks the
	//snowline so it is drifts and patches, not a clean contour. Read off the ROCK-perturbed normal: the
	//terrain's own slope decides what holds snow, and the snow's own relief is about to be added on top of
	//that decision rather than feeding back into it.
	float slopeSnow = smoothstep(RockSlope, SnowSlope, rockNormal.y);
	float snowNoise = CloudNoise(worldPosition.xz * 0.03) * 9.0;
	float altSnow = smoothstep(SnowlineLow + snowNoise, SnowlineHigh + snowNoise, worldPosition.y);
	float snowDetailed = slopeSnow * saturate(altSnow + 0.15); //a little snow even on lower shoulders

	//Fade the snow/rock DETAIL to a smooth snowy value with distance (footprint): the sharp rock/snow split
	//aliases into a crawl on the far, stacked ranges, so smoothing it there leaves clean snowy peaks while the
	//near and mid slopes keep the rock/snow detail.
	float detailFade = saturate(1.0 - footprint * 0.05);
	float snow = lerp(0.72, snowDetailed, detailFade);

	//And the snow's OWN two surfaces, over the mask that just came back (#208): the drift relief tilts the
	//normal only where snow lies, weighted by how much of it there is, and the sparkle dusts the albedo
	//after lighting as an additive glint. Both die with detailFade like everything else the near slopes
	//carry, so the far ranges stay clean shapes in haze.
	float snowRelief = SnowRelief(worldPosition.xz, footprint) * snow * detailFade;
	float3 normal = PerturbNormalFromHeight(rockNormal, worldPosition, relief + snowRelief);

	//Rock varies between a dark and a lighter grey-brown in patches, so the faces are not one flat colour
	float rockPatch = saturate(CloudNoise(worldPosition.xz * 0.08 + 21.0) * 0.5 + 0.5);
	float3 rock = lerp(RockColor, RockColorLight, rockPatch * detailFade);

	//Fine per-pixel rock grain, the cue Desert.fx's sand added that "a floor with no fine albedo change still
	//looks airbrushed however it is lit" (docs/scenes.md). One hash per pixel over a fine world lattice,
	//band-limited against the footprint the same way as the relief so it fades to smooth before its cells reach
	//pixel size (a hard-edged per-cell value is its own aliasing source) and stays off the distant ranges with
	//the rest of the detail. Only on the rock, not the snow it sits beside.
	float rockGrainFade = saturate(1.0 - footprint * 120.0) * detailFade;
	rock *= 1.0 + NoiseHash22(floor(worldPosition.xz * 60.0)).x * 0.14 * rockGrainFade;

	float3 albedo = lerp(rock, SnowColor, snow);

	float sunlight = CloudSunlight(worldPosition, SunDirection);
	float ndotl = saturate(dot(normal, SunDirection));

	//Hemisphere sky light: up-facing takes the zenith, slopes take the horizon. The blue zenith filling the
	//shadows gives the snow its cold cast where the sun misses it.
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

	//The glint, ON TOP of the lit snow: specular in character, additive in code - a glint is bright enough
	//that tonemapping it down a little is the look, not a loss. Scaled by the direct sun only (ndotl and the
	//cloud shadow with it), because a glint IS reflected sun.
	color += SunColor * SnowSparkle(worldPosition.xz, normal, footprint)
		* ndotl * sunlight * detailFade * 3.0;

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
