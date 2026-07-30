//Draws a forest clearing: a mossy needle-strewn floor whose hills dress in a dark conifer canopy, the
//round stone island standing in the middle of the open ground. The eighth SceneKind, and past the end of
//the NumPad2/V cycle (which still runs % 7, over the seven scenes a map is authored against), so
//"scene=forest" on the command line is how it is reached. The look is a temperate woodland glade - cool
//low green undergrowth, darker and more shadowed than the meadow, combed by the same wind and shaded by
//the same drifting clouds. No wildflowers: a forest floor is leaf litter and moss, not a lawn of blooms,
//so the colour work is in the patchy undergrowth, the treeline and the fine needle relief rather than in
//scattered rosettes.
//
//Real geometry like the meadow - a camera-centred grid (shared CreateGridMesh on the C# side) displaced
//by a smooth rolling field, low around the arena and rising into tree-covered hills with distance, its
//normal taken by finite differences. The scattered trees, rocks and stumps that stand ON this floor are
//the Game's own instanced draws (ForestScatter over SceneRenderer.ForestTerrainHeight, a CPU mirror of
//TerrainHeight below - keep the two in one change), so the other two executables draw the bare clearing.
//Drawn in all three executables, Shader Model 5.0, no OPENGL branch.

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

//The wooded hills: past the clearing the ground dresses in a dark conifer-canopy colour (linear), mottled
//at grove and crown scale so it reads as treetops rather than dark paint. The scattered tree meshes stop
//well before the horizon; this is what carries the forest to the skyline. Strength 0 leaves bare hills.
float3 TreelineColor;
float TreelineStrength;

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

//Rolling hills behind the trees, low around the arena centre (world origin) and rising into wooded hills
//with distance, so the clearing is flat where the arena stands and rolls up towards the treeline. Kept in
//ONE change with SceneRenderer.ForestTerrainHeight, its CPU mirror - the scatter plants trees on this.
float TerrainHeight(float2 p)
{
	float dist = length(p);
	float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

	//A domain warp bends the octaves' straight wavefronts before they are summed. Without it the hills
	//read as a regular swell - the "row of smooth mounds" the first build had - because summed plane
	//waves keep their planes however many there are; the warp is what breaks the planes themselves. It
	//is two long sines, so the CPU mirror stays exact.
	float2 q = p + 26.0 * float2(sin(p.y * 0.011 + 2.0), sin(p.x * 0.013 + 5.0));

	//Five octaves rather than three, amplitudes summing to 1 so the ramp's height stays the authored
	//HillHeight: the two added octaves fill the gap between hill and lump scales, which is exactly the
	//band a "smooth blob with bumps on it" is missing.
	float rolling = 0.40 * sin(dot(q, float2(0.020, 0.015)))
		+ 0.26 * sin(dot(q, float2(-0.013, 0.024)) + 1.5)
		+ 0.17 * sin(dot(q, float2(0.031, 0.026)) + 3.0)
		+ 0.10 * sin(dot(q, float2(0.056, -0.041)) + 0.7)
		+ 0.07 * sin(dot(q, float2(-0.083, 0.062)) + 2.4);

	float basin = ClearingRelief * sin(dot(p, float2(0.05, 0.035)));

	//Floor lumps are present even inside the clearing - the uneven ground the trees stand on - and fade
	//out with the rolling hills so the distant hills stay smooth (their detail is the trees, not the
	//floor). Three waves under a broad mask: unmasked, two sines interfere into an even weave across the
	//whole floor, which reads as a pattern rather than as ground; masked, the roughness comes in patches
	//the way roots and hollows do.
	float mask = 0.55 + 0.45 * sin(dot(p, float2(0.021, -0.017)) + 4.0);
	float lumps = sin(dot(p, float2(FloorLumpFrequency, FloorLumpFrequency * 0.7)))
		+ 0.5 * sin(dot(p, float2(-FloorLumpFrequency * 0.8, FloorLumpFrequency * 1.1)) + 2.0)
		+ 0.35 * sin(dot(p, float2(FloorLumpFrequency * 1.9, FloorLumpFrequency * 1.4)) + 5.1);
	float lumpHeight = FloorLumpStrength * lumps * mask * (1.0 - ramp * 0.5);

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
};

ForestVertexOutput ForestVS(ForestVertexInput input)
{
	ForestVertexOutput output;

	float2 xz = input.Position.xz + OriginXZ;
	float3 worldPosition = float3(xz.x, TerrainHeight(xz), xz.y);

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

//A fine needle/moss texture that drifts on the wind, band-limited PER OCTAVE against the footprint (the
//ball relief's rule: one global fade has to be tuned for the finest wave and flattens the lot at arm's
//length, while per-octave fades let each drop out exactly where the pixels stop resolving it). Four
//octaves rather than two: two sines interfere into a soft weave, which is most of what read as "blurred"
//on the near floor - a needle floor is fine, sharp and directionless.
float NeedleRelief(float2 xz, float footprint)
{
	float2 p = xz + WindDirection * ForestTime * 0.7;
	float f = NeedleReliefFrequency;

	float h = 0.45 * sin(dot(p, normalize(float2(0.9, 0.3))) * f) * saturate(1.0 - footprint * f / 3.14159265)
		+ 0.28 * sin(dot(p, normalize(float2(-0.4, 1.0))) * f * 1.83) * saturate(1.0 - footprint * f * 1.83 / 3.14159265)
		+ 0.17 * sin(dot(p, normalize(float2(0.2, -1.0))) * f * 3.1) * saturate(1.0 - footprint * f * 3.1 / 3.14159265)
		+ 0.10 * sin(dot(p, normalize(float2(-1.0, -0.35))) * f * 5.7) * saturate(1.0 - footprint * f * 5.7 / 3.14159265);

	return h * NeedleReliefStrength;
}

//The terrain's base normal, per pixel from the height field's own gradient - the savanna's fix, and the
//reason it exists here too: interpolating a coarse displaced grid's per-vertex normal leaves a Mach band
//at every cell edge, and on this scene it read as the whole floor being slightly out of focus. Three taps
//per pixel; the grid keeps the silhouette, the gradient does the shading.
float3 TerrainNormal(float2 p)
{
	float e = 1.2;
	float h = TerrainHeight(p);
	float hx = TerrainHeight(p + float2(e, 0.0));
	float hz = TerrainHeight(p + float2(0.0, e));

	return normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));
}

float4 ForestPS(ForestVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;

	//Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
	clip(length(worldPosition.xz) - IslandHoleRadius);

	float3 baseNormal = TerrainNormal(worldPosition.xz);
	float footprint = length(fwidth(worldPosition.xz));

	//Fine needle/moss texture tilts the normal, so the floor catches the light unevenly and the wind reads on it
	float relief = NeedleRelief(worldPosition.xz, footprint);
	float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

	//Undergrowth colour: mossy green in broad patches, varying towards the dark needle litter and shadow, so
	//the floor is not one flat green but mottled the way a real clearing is. CloudNoise is gradient noise
	//that clusters hard around zero - one sigma 0.18, five to ninety-five per cent inside +/-0.3, and only
	//its extremes near +/-0.8 - so the gain is well over 1 (and the result saturated): at half gain the
	//patches would move the colour by a fifth and the ACES curve flattens that into one tone.
	float patch = saturate(CloudNoise(worldPosition.xz * 0.15) * 1.4 + 0.5);
	float3 floor = lerp(ForestColorDark, ForestColor, patch);

	//Wind combing the undergrowth: bright and dark bands travelling downwind, the clearing's own motion
	float wind = sin(dot(worldPosition.xz, WindDirection) * WindRippleFrequency + ForestTime * WindRippleSpeed);
	floor *= 1.0 + wind * WindRippleStrength;

	//Scattered darker litter patches - the fallen needles and leaf decay that sit between the moss, finer than
	//the broad colour patches and darker still, so the floor reads as layered ground cover rather than one tone
	//(spread like the patches above, and for the same reason)
	float litter = saturate(CloudNoise(worldPosition.xz * 0.6 + 47.0) * 1.6 + 0.5);
	floor = lerp(floor, ForestColorDark * 0.7, litter * litter * 0.5);

	//Needle-scale colour grain, the finest layer: twigs, cones and litter flecks at arm's length, gone by
	//the middle distance (band-limited to nothing before it can shimmer). This is the layer whose absence
	//read as "out of focus" up close - the relief tilts the light, but a floor with no fine ALBEDO change
	//still looks airbrushed however it is lit. Spread hard, the CloudNoise rule above.
	float grainFade = saturate(1.0 - footprint * 2.4);
	float grain = CloudNoise(worldPosition.xz * 2.6) * 1.8;
	floor *= 1.0 + 0.22 * grain * grainFade;

	//The wooded hills: away from the clearing the undergrowth gives way to the dark canopy of the trees
	//covering them. The treeline has its own ramp, tighter and NEARER than the hills': the slopes that
	//fill the frame are the hills' transition band itself, and a canopy keyed to that full ramp only
	//arrives where the haze already owns the colour - so this one starts INSIDE the clearing, three
	//quarters of the way out (under the first scattered trees, which stand in front of it) and is
	//complete a little past the clearing's edge. Its edge rides the grove noise, so the woods begin on a
	//ragged line rather than a drawn circle. Two mottle scales: broad grove-sized patches (lit stands
	//against shadowed ones) and a finer crown-sized grain. Multiplied rather than blended so the canopy
	//keeps its darks.
	float grove = saturate(CloudNoise(worldPosition.xz * 0.035 + 11.0) * 1.7 + 0.5);
	float crowns = saturate(CloudNoise(worldPosition.xz * 0.17 + 73.0) * 1.7 + 0.5);
	float treeDist = length(worldPosition.xz) + (grove - 0.5) * 40.0;
	float canopyRamp = smoothstep(ClearingRadius * 0.75, ClearingRadius + ClearingTransition * 0.45, treeDist);
	//Wide multiplicative swings: after the ACES curve a timid mottle flattens into one tone, and it is
	//the swing between sunlit stands and shadowed ones that says "treetops" at this distance. A third,
	//crown-top grain under a band limit gives the near slopes individual treetops without turning the far
	//ridge into shimmer - the two broad scales alone read soft-focus exactly where the hills fill the frame.
	float crownTops = saturate(CloudNoise(worldPosition.xz * 0.45 + 31.0) * 1.7 + 0.5);
	float crownFade = saturate(1.0 - footprint * 0.45);
	float3 canopy = TreelineColor * (0.4 + 1.2 * grove) * (0.55 + 0.9 * crowns)
		* (1.0 - (0.25 - 0.5 * crownTops) * crownFade);
	floor = lerp(floor, canopy, canopyRamp * TreelineStrength);

	//Matte forest floor: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds
	//that drift across the sky sweep their shadows over the clearing. Lower ambient than the meadow: a clearing
	//is shaded by the trees around it, not open sky.
	float sunlight = CloudSunlight(worldPosition, SunDirection);
	float ndotl = saturate(dot(normal, SunDirection));
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = floor * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

	//Horizon haze in two stages. Straight to the horizon colour - the one stage every other terrain
	//uses - the dark wooded hills bleach cream long before the skyline and read as bare slopes; what
	//distant forested ridges actually do is turn BLUE, because the air between scatters skylight. So
	//the hills first recede into a murk built mostly from the zenith (tinted green so the canopy keeps
	//reading through it; a fraction of the dome's own colours, so a dusk's murk is dark), and only the
	//last stretch melts into the horizon itself - reaching it exactly at the haze distance, which is
	//what keeps the terrain grid's edge invisible against the dome behind it.
	float dist = distance(CameraPosition, worldPosition);
	float haze = saturate(dist / HorizonHazeDistance);
	float3 murk = (ZenithColor * 0.7 + HorizonColor * 0.3) * float3(0.5, 0.65, 0.58);
	color = lerp(color, murk, saturate(haze * haze * 1.2) * 0.6);
	float skyward = haze * haze;
	skyward *= skyward;
	color = lerp(color, HorizonColor, skyward);

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
