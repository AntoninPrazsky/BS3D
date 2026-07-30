//Draws the bioluminescent crystal cavern: deep noise-carved rock walls veined with glowing minerals, an
//underground river mirroring them, god rays falling through unseen gaps in the ceiling, sharp crystal
//clusters pulsing cyan and magenta on the walls and casting their light onto the rock around them, and
//spores drifting up through the dark. The eleventh scene, and the third that replaces the SKY (see Space
//and Dream): one full-screen pass over the shared NDC quad, the view ray recovered per pixel through
//InverseViewProjection, drawn with the depth state off so the island, the cluster and the gun draw over it.
//The caller draws no dome and no cloud deck, suppresses the cloud shadow on the instanced effect, and takes
//the scene's own light rig (CavernLightingConfig).
//
//The scene is ANALYTIC, never marched except where marching is gated and cheap: the cave is a noise-shaded
//cylinder-and-ceiling shell (a quadratic and a plane), the river is a plane whose REFLECTION is the wall
//shading function evaluated a second time along the reflected ray - a real mirror of the real cave, where a
//screen-space approximation would smear - and the crystals are the one raymarched element, gated per
//cluster by analytic bounding spheres exactly as the dream's solids are. The god rays and the spores are
//closest-approach glows with no geometry at all.
//
//Levels against the glare (GLARE_THRESHOLD 0.55 on luminance): the rock stays far under it - a cave is
//dark, and the dark is what makes the glow read - the crystals and the water's crest glow are allowed over
//deliberately (smooth areas wide enough to bloom steadily, the planet's lit-limb reasoning), and the spores
//sit at the threshold's edge, small and slow, reading through motion rather than bloom.
//
//One deliberate deviation from the usual full-scene grading brief: BS3D renders linear radiance into one
//HDR target and tonemaps ONCE (docs/rendering.md), so there is no per-scene colour grade or contrast pass
//here - the grade lives in the authored palette and the scene's light rig, where it composes with the ACES
//curve instead of fighting it. The one cinematic touch kept in-pass is a subtle vignette on the BACKDROP
//alone, which darkens the cave's corners without touching the island drawn over it.
//
//Built by all three executables out of this directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

//How many of each element the cavern carries. Fixed at compile time so the loops unroll.
#define CRYSTAL_COUNT 8
#define RAY_COUNT 4
#define SPORE_COUNT 28

float4x4 InverseViewProjection;
float3 CameraPosition;
float CavernTime;

//--- The cave shell ---------------------------------------------------------------------------------------
float CaveRadius;          //the rock cylinder's radius around the world origin
float CaveCeilingY;        //the ceiling plane the god rays fall from
float3 RockColor;          //desaturated blue-grey (linear), the walls' albedo-times-ambient in one value
float3 VeinColor;          //the glowing mineral veins threading the rock (linear, gently over the rock)
float3 FogColor;           //the abyssal blue-purple the far cave sinks into (linear)
float FogDensity;          //exponential distance-fog density

//--- The river --------------------------------------------------------------------------------------------
float WaterLevelY;         //the river's surface plane, well under the island
float3 WaterDeepColor;     //what the depths transmit (linear, dark)
float3 WaterGlowColor;     //the bioluminescent glow the crests carry (linear, allowed over the threshold)
float WaveScale;           //interference-wave frequency across the surface
float WaveSpeed;
float CausticStrength;     //the caustic shimmer added where the eye looks into the water

//--- The god rays -----------------------------------------------------------------------------------------
float3 GodRayColor;        //cool shaft light (linear)
float GodRayStrength;

//--- The crystals -----------------------------------------------------------------------------------------
float3 CrystalColorA;      //one end of the cluster palette - cyan (linear)
float3 CrystalColorB;      //the other end - magenta (linear)
float CrystalEmission;     //peak emissive level of a pulsing cluster (over the threshold, deliberately)
float CrystalPulseSpeed;
float CrystalWallLight;    //how much of a cluster's light pools on the rock and the water around it

//--- The spores -------------------------------------------------------------------------------------------
float3 SporeColor;         //the drifting motes (linear, at the threshold's edge)
float SporeBrightness;

static const float PI = 3.14159265;

//--- The rock ---------------------------------------------------------------------------------------------
//Where crystal cluster k grows: ON the wall — at nearly the full cave radius, so the growths visibly stand
//on rock rather than hovering in the air (the first build had them at 0.86 R, a thirty-unit float off the
//wall, and they read as balloons) — spaced round the cylinder by the golden angle so no two share a
//bearing, at heights from just over the river to high on the wall. Shared by the cluster SDF, the light it
//casts on the rock, and the water's reflection of both.
float3 CrystalCenter(float k)
{
	float angle = k * 2.39996 + 0.7;
	float radius = CaveRadius * 0.965;
	float y = WaterLevelY + 6.0 + fmod(k * 37.0, 70.0);

	return float3(cos(angle) * radius, y, sin(angle) * radius);
}

//One cluster's distance field, in the cluster's own frame: three interpenetrating octahedra - the sharpest
//cheap SDF there is, and sharp is what a crystal means - at different elongations, offsets and yaws, so a
//cluster reads as a GROWTH of spars rather than one gem.
float ClusterSdf(float3 p, float k)
{
	float3 q1 = p;
	q1.y *= 0.38;
	float d = (abs(q1.x) + abs(q1.y) + abs(q1.z) - 9.5) * 0.577;

	float ca = cos(k * 1.3), sa = sin(k * 1.3);
	float3 q2 = float3(p.x * ca - p.z * sa, p.y - 4.0, p.x * sa + p.z * ca);
	q2.y *= 0.5;
	q2.x *= 0.8;
	d = min(d, (abs(q2.x) + abs(q2.y) + abs(q2.z) - 6.8) * 0.577);

	float3 q3 = float3(p.x + 4.5, (p.y + 6.0) * 0.45, p.z - 3.5);
	d = min(d, (abs(q3.x) + abs(q3.y) + abs(q3.z) - 5.2) * 0.577);

	return d;
}

//The pulse of cluster k: a slow swell, phase-spread so the cavern never beats in unison, and lifted well
//off zero - these are light sources, and a light source that dies reads as broken. Squaring the wave
//sharpens it into quick bright peaks over long dim valleys, a pulse rather than a sine.
float CrystalPulse(float k)
{
	float beat = 0.5 + 0.5 * sin(CavernTime * CrystalPulseSpeed + k * 2.1);
	return 0.55 + 0.45 * beat * beat;
}

//The colour of cluster k: the cyan-magenta axis walked per cluster, not per pixel - each cluster is ONE
//colour, and the contrast between neighbouring clusters is what reads as "cyan and magenta cavern".
float3 CrystalColor(float k)
{
	return lerp(CrystalColorA, CrystalColorB, frac(k * 0.381));
}

//The coloured light the clusters pool onto a point of rock or water. Inverse-square with a floor, summed
//over all clusters - eight multiply-adds, and the single thing that makes the crystals belong to the cave
//instead of being stickers on it: rock that a magenta cluster stands on IS magenta around it.
float3 CrystalLightAt(float3 position)
{
	float3 light = 0.0;

	[unroll]
	for (int k = 0; k < CRYSTAL_COUNT; k++)
	{
		float fk = (float)k;
		float3 toCrystal = CrystalCenter(fk) - position;
		float d2 = dot(toCrystal, toCrystal);

		light += CrystalColor(fk) * (CrystalPulse(fk) / (1.0 + d2 * 0.0016));
	}

	return light * CrystalWallLight;
}

//Shades a point of the cave shell: layered noise rock, mineral veins, crystal light, distance fog. Written
//as a function of the hit point and distance because the RIVER calls it a second time along the reflected
//ray - the water's mirror is the real wall, not an approximation of it.
float3 ShadeWall(float3 position, float distanceTravelled)
{
	//Two planar projections of the shared 2D gradient noise stand in for a 3D rock field: the walls are
	//near-vertical (the cylinder), so a wall-tangent projection carries most of the detail and a floor
	//projection fills the ceiling. Three octaves, spread hard - the CloudNoise rule: gradient noise
	//clusters around zero and a timid mottle flattens to one tone under the ACES curve.
	float2 wallUv = float2(atan2(position.z, position.x) * CaveRadius * 0.5, position.y);
	float rough = CloudNoise(wallUv * 0.020) * 1.5
		+ 0.5 * CloudNoise(wallUv * 0.055 + 31.0)
		+ 0.25 * CloudNoise(wallUv * 0.15 + 73.0);

	float3 rock = RockColor * (0.55 + 0.45 * saturate(rough * 0.8 + 0.5));

	//The mineral veins: the ridges of a noise field - thin where |noise| is small - raised to a power so
	//only the ridge lines survive, threading the rock the way ore veins follow cracks. They glow gently:
	//bright enough to read as luminous minerals, far under the crystals they feed.
	float veinField = CloudNoise(wallUv * 0.033 + 7.0);
	float vein = pow(saturate(1.0 - abs(veinField) * 2.6), 6.0);
	rock += VeinColor * vein;

	//The clusters' pooled light, then the abyss: exponential distance fog into the deep blue-purple, so
	//the far side of the cavern sinks away instead of presenting a lit wall at every distance.
	rock += RockColor * CrystalLightAt(position) * 6.0;

	float fog = 1.0 - exp(-distanceTravelled * FogDensity);
	return lerp(rock, FogColor, fog);
}

//Where the view ray meets the cave shell (the wall cylinder or the ceiling plane), as a distance along the
//ray. The cave is watertight from inside: one of the two always hits.
float CaveShellDistance(float3 origin, float3 direction)
{
	//Ray vs the infinite vertical cylinder |xz| = CaveRadius, from inside: the positive root.
	float2 oxz = origin.xz;
	float2 dxz = direction.xz;
	float a = max(dot(dxz, dxz), 1e-6);
	float b = dot(oxz, dxz);
	float c = dot(oxz, oxz) - CaveRadius * CaveRadius;
	float tWall = (-b + sqrt(max(b * b - a * c, 0.0))) / a;

	//The ceiling plane, when the ray climbs towards it.
	float tCeiling = direction.y > 1e-4 ? (CaveCeilingY - origin.y) / direction.y : 1e9;

	return min(tWall, tCeiling);
}

//The river's wave field: two interference sine sets scrolled against each other. The DERIVATIVE of this
//(the analytic gradient below) is the water normal - summed sines, never multiplied, the ball relief's
//crosshatch lesson.
float WaveHeight(float2 p, float t)
{
	return sin(dot(p, float2(0.9, 0.35)) * WaveScale + t * WaveSpeed)
		+ 0.6 * sin(dot(p, float2(-0.4, 1.0)) * WaveScale * 1.7 - t * WaveSpeed * 0.8)
		+ 0.35 * sin(dot(p, float2(0.2, -0.95)) * WaveScale * 2.9 + t * WaveSpeed * 1.3);
}

struct CavernVertexInput
{
	float4 Position : POSITION0;
};

struct CavernVertexOutput
{
	float4 Position : SV_POSITION;
	float3 Ray : TEXCOORD0;
	float2 Ndc : TEXCOORD1;
};

CavernVertexOutput CavernVS(CavernVertexInput input)
{
	CavernVertexOutput output;
	output.Position = float4(input.Position.xy, 0.0, 1.0);
	output.Ndc = input.Position.xy;

	//The corner unprojected to the far plane; the pixel shader normalizes the interpolated ray.
	float4 far = mul(float4(input.Position.xy, 1.0, 1.0), InverseViewProjection);
	output.Ray = far.xyz / far.w - CameraPosition;

	return output;
}

float4 CavernPS(CavernVertexOutput input) : COLOR
{
	float3 direction = normalize(input.Ray);
	float t = CavernTime;

	//--- The solid the ray ends on: the cave shell, unless the river is nearer. -------------------------
	float tShell = CaveShellDistance(CameraPosition, direction);
	float tWater = direction.y < -1e-4 ? (WaterLevelY - CameraPosition.y) / direction.y : 1e9;

	float3 color;
	float tSolid;

	[branch]
	if (tWater < tShell)
	{
		//THE RIVER. The wave normal comes from the height field's analytic gradient - the same three sines
		//differentiated - tilted gently so the mirror stays recognisable rather than shattering.
		tSolid = tWater;
		float3 hit = CameraPosition + direction * tWater;

		float2 p = hit.xz;
		float e = 0.6;
		float h = WaveHeight(p, t);
		float2 grad = float2(WaveHeight(p + float2(e, 0.0), t) - h, WaveHeight(p + float2(0.0, e), t) - h) / e;
		float3 normal = normalize(float3(-grad.x * 0.18, 1.0, -grad.y * 0.18));

		//The reflection is the CAVE, evaluated again along the reflected ray - walls, veins, fog, crystal
		//light and all. One extra shell test and wall shade per water pixel, and the river genuinely
		//mirrors the cavern standing over it.
		float3 bounced = reflect(direction, normal);
		bounced.y = abs(bounced.y);   //a wave can fold the ray downward; the mirror looks up
		float tReflected = CaveShellDistance(hit, bounced);
		float3 mirrored = ShadeWall(hit + bounced * tReflected, tWater + tReflected);

		//Fresnel: grazing looks mirror, steep looks into the water - where the depths transmit their dark
		//colour, the caustic shimmer plays (the wave crests focused on the riverbed, cheap: the height
		//field re-read at a second scale, sharpened), and the crystals pool their light. The floor is
		//high for water: a cave river is a black mirror before it is a window, and at 0.06 the reflection
		//never read at all from the play camera's angle.
		float fresnel = pow(1.0 - saturate(dot(normal, -direction)), 5.0);
		fresnel = lerp(0.22, 1.0, fresnel);

		//The wave sum's amplitude is 1.95, so it is NORMALIZED before the shaping pow - unnormalized, the
		//saturate clipped it into broad plateaus and the "caustics" came out as a cyan checkerboard of
		//solid patches instead of a thread network. Finer scale than the waves themselves: caustics are
		//the focused image of the surface, always tighter than the surface is.
		float causticField = WaveHeight(p * 1.4 + 40.0, t * 0.7) / 1.95;
		float caustic = pow(saturate(0.5 + 0.5 * causticField), 10.0);
		float3 depths = WaterDeepColor
			+ WaterGlowColor * caustic * CausticStrength
			+ WaterDeepColor * CrystalLightAt(hit) * 6.0;

		color = lerp(depths, mirrored, fresnel);

		//The crests carry the bioluminescence itself: the higher the wave stands, the more its organisms
		//glow - the river's own light. Normalized like the caustics, and for the same reason.
		float crest = saturate(0.5 + 0.5 * h / 1.95);
		color += WaterGlowColor * pow(crest, 5.0) * 0.4;
	}
	else
	{
		//THE ROCK.
		tSolid = tShell;
		color = ShadeWall(CameraPosition + direction * tShell, tShell);
	}

	//--- The god rays: vertical shafts from unseen gaps above, as closest-approach glows to fixed vertical
	//lines - no geometry, no march. Each fades with height below the ceiling (light thins as it falls),
	//breathes on its own slow cycle, and carries a dust shimmer down its length.
	[unroll]
	for (int r = 0; r < RAY_COUNT; r++)
	{
		float fr = (float)r;
		float angle = fr * 1.62 + 0.4;
		float2 beamXz = float2(cos(angle), sin(angle)) * CaveRadius * (0.30 + 0.14 * frac(fr * 0.53));

		//Closest approach of the view ray to the beam's vertical line, in the XZ plane.
		float2 oxz = CameraPosition.xz - beamXz;
		float2 dxz = direction.xz;
		float along = -dot(oxz, dxz) / max(dot(dxz, dxz), 1e-5);
		along = clamp(along, 0.0, tSolid);
		float3 nearest = CameraPosition + direction * along;
		float2 offset = nearest.xz - beamXz;

		float shaft = exp(-dot(offset, offset) / 90.0);

		//Height envelope: strongest just under the ceiling, gone by the water; the beam breathes and its
		//dust drifts downward on the shared noise.
		float fall = saturate((CaveCeilingY - nearest.y) / (CaveCeilingY - WaterLevelY));
		float envelope = saturate(1.2 - fall * 1.4);
		float dust = 0.75 + 0.35 * CloudNoise(float2(fr * 9.0, nearest.y * 0.05 + t * 0.35));
		float breathe = 0.7 + 0.3 * sin(t * 0.11 + fr * 2.6);

		color += GodRayColor * (shaft * envelope * dust * breathe * GodRayStrength);
	}

	//--- The crystals: the one raymarched element, gated per cluster (the dream's pattern). A cluster is
	//three interpenetrating octahedra - the sharpest cheap SDF there is, and sharp is what a crystal means -
	//standing on the wall, marched only when the ray crosses its bounding sphere and only up to the solid
	//already found.
	float bestT = tSolid;
	float bestCluster = -1.0;

	[unroll]
	for (int i = 0; i < CRYSTAL_COUNT; i++)
	{
		float fi = (float)i;
		float3 center = CrystalCenter(fi);
		float bound = 16.0;

		float3 oc = CameraPosition - center;
		float b = dot(oc, direction);
		float c = dot(oc, oc) - bound * bound;
		float disc = b * b - c;

		[branch]
		if (disc > 0.0)
		{
			float t0 = max(-b - sqrt(disc), 0.0);
			float t1 = min(-b + sqrt(disc), bestT);

			float rayT = t0;

			//0.55 on the step keeps the march conservative under the octahedra's elongations (a scaled
			//SDF underestimates distance).
			[loop]
			for (int march = 0; march < 24; march++)
			{
				float d = ClusterSdf(CameraPosition + direction * rayT - center, fi);
				if (d < 0.03 || rayT > t1) break;
				rayT += d * 0.55;
			}

			if (rayT <= t1)
			{
				bestT = rayT;
				bestCluster = fi;
			}
		}
	}

	[branch]
	if (bestCluster >= 0.0)
	{
		//A crystal face, shaded off the SDF's own normal - four tetrahedral taps, the dream's trick. The
		//octahedra have FLAT faces, so the reconstructed normal is constant across each facet and jumps at
		//the arris: the facet structure appears in the shading for free, which is exactly what the first
		//build's smooth radial term threw away (it made every cluster a rounded gem).
		float3 hit = CameraPosition + direction * bestT;
		float3 local = hit - CrystalCenter(bestCluster);

		const float e = 0.2;
		float2 tap = float2(1.0, -1.0);
		float3 normal = normalize(
			tap.xyy * ClusterSdf(local + tap.xyy * e, bestCluster) +
			tap.yyx * ClusterSdf(local + tap.yyx * e, bestCluster) +
			tap.yxy * ClusterSdf(local + tap.yxy * e, bestCluster) +
			tap.xxy * ClusterSdf(local + tap.xxy * e, bestCluster));

		//Lit by its own emission on the pulse, a hard rim at the silhouette, and a facet term keyed to the
		//god rays' downward light so neighbouring faces take visibly different brightness - the facet
		//CONTRAST is what says "crystal", not the outline.
		float3 own = CrystalColor(bestCluster);
		float pulse = CrystalPulse(bestCluster);
		float rim = pow(1.0 - saturate(dot(normal, -direction)), 2.0);
		float facetLight = saturate(dot(normal, normalize(float3(0.25, 0.85, 0.2))));

		color = own * (CrystalEmission * pulse * (0.30 + 0.35 * rim + 0.45 * facetLight));
	}

	//--- The spores: slow rising motes, swaying as they climb, wrapping over the cave's height for ever.
	//Closest-approach gaussians like the dream's sparks, but SLOW - the cave's stillness is the point.
	[unroll]
	for (int s = 0; s < SPORE_COUNT; s++)
	{
		float fs = (float)s;
		float lane = frac(fs * 0.618);
		float riseSpan = CaveCeilingY - WaterLevelY;

		float3 center;
		center.y = WaterLevelY + fmod(fs * 11.0 + t * (1.6 + lane * 1.4), riseSpan);
		float swayAngle = fs * 2.4 + t * (0.10 + lane * 0.06);
		float ringRadius = CaveRadius * (0.25 + 0.55 * frac(fs * 0.373));
		center.x = cos(swayAngle) * ringRadius + sin(t * 0.5 + fs * 3.1) * 3.0;
		center.z = sin(swayAngle) * ringRadius + cos(t * 0.43 + fs * 1.7) * 3.0;

		//Closest approach along the ray, clamped to in-front: a mote drifting BEHIND the lens must not
		//glow through it (the dream's sparks learned the same clamp).
		float3 toSpore = center - CameraPosition;
		float along = max(dot(toSpore, direction), 0.0);
		float3 offset = toSpore - direction * along;

		color += SporeColor * (exp(-dot(offset, offset) / 3.0) * SporeBrightness);
	}

	//--- The vignette, on the backdrop alone: the cave's corners sink, the glowing centre holds the eye.
	//Subtle, and in the PASS rather than in the resolve - the island drawn over this is untouched.
	float edge = dot(input.Ndc, input.Ndc) * 0.5;
	color *= 1.0 - 0.22 * edge * edge;

	return float4(color, 1.0);
}

technique Cavern
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL CavernVS();
		PixelShader = compile PS_SHADERMODEL CavernPS();
	}
};
