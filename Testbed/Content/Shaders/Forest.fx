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

//The floor's two expensive extras - the triplanar normal variation and the procedural tree shadows - are
//switched by TECHNIQUE rather than by a uniform, and they go together. Both facts are measured rather than
//tidy. Front end, 1600x900, desktop GPU, forest under dome 13, nocap: all on 2.69 ms, both gone 2.09.
//Cutting either one ALONE saves NOTHING - 2.71 without the normal variation, 2.72 without the shadows - and
//dropping the floor's FBM from four octaves to two saves nothing either (2.71). The pass is occupancy-bound
//rather than work-bound, which is what makes a per-feature dial useless here, and what makes a runtime
//branch useless too: see ForestPS's `detail` parameter for the 2.72-against-2.09 that settled it.

//Procedural tree-shadow tuning (ForestShadow). The cell is the spacing of the virtual trees the hash grid
//plants; it sits inside the scattered wood's own spacing so the two read as the same forest rather than two
//different ones laid over each other. Reach is how far down-sun a crown's shadow is searched - past it the
//shadow has thinned to nothing and the march stops.
static const float FOREST_SHADOW_CELL = 9.0;
static const float FOREST_SHADOW_REACH = 22.0;
static const float FOREST_SHADOW_MIN_H = 5.0;
static const float FOREST_SHADOW_MAX_H = 11.0;

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

	return normalize(float3(-(hx - h) / e,1.0, -(hz - h) / e));
}

//Fractional Brownian motion on the shared gradient noise (CloudNoise, Clouds.fxh) - four octaves, each band-
//limited against the pixel's footprint the way every other procedural feature in this project is (NeedleRelief,
//CloudDetailD): one global fade would have to be tuned for the finest octave and would flatten the coarse ones
//at arm's length, while per-octave fades let each drop out exactly where the pixels stop resolving it. Amplitudes
//sum to 1 so the result sits in roughly the same range as a single CloudNoise tap (after its own spread/saturate).
//Replaces the hand-spread single-frequency noise taps the floor colour used to read off: a single octave reads as
//smooth patches, four sum to the broken, organic mottle a real forest floor has.
float ForestFbm(float2 p, float footprint)
{
	float sum = 0.0;
	float amplitude = 0.5;
	float frequency = 1.0;

	[unroll]
	for (int octave = 0; octave < 4; octave++)
	{
		float resolvable = saturate(1.0 - footprint * frequency * 0.9);
		sum += amplitude * resolvable * CloudNoise(p * frequency + octave * 29.7);
		amplitude *= 0.5;
		frequency *= 2.1;
	}

	return sum;
}

//Perturbs a surface normal with a triplanar FBM field, after dr2's VaryNf: three taps of the field on the
//planes perpendicular to the normal (yz, zx, xy), differenced against the central tap, folded back into the
//normal the way a tangent-space bump would be. This is a SECOND, finer layer over PerturbNormalFromHeight: the
//relief tilts the normal along the needle/moss waves, this one adds the broken micro-relief that a single
//frequency cannot. TerrainNormal itself is untouched - it must stay the gradient of TerrainHeight, which the
//scatter plants trees on.
float3 VaryNormal(float3 p, float3 n, float frequency, float strength, float footprint)
{
	float2 e = float2(0.1, 0.0);

	float c = ForestFbm(p.yz * frequency, footprint);
	float gx = ForestFbm((p + e.xyy).yz * frequency, footprint) - c;
	float gy = ForestFbm((p + e.yxy).zx * frequency, footprint) - c;
	float gz = ForestFbm((p + e.yyx).xy * frequency, footprint) - c;
	float3 g = float3(gx, gy, gz);

	return normalize(n + strength * (g - n * dot(n, g)));
}

//Procedural tree shadows on the forest floor, after dr2's ObjSShadow but without a distance field: the trees
//are VIRTUAL, placed by a hash grid (CloudHash22) the way dr2's SetTrParms places one per hex cell, rather than
//the instanced meshes' real positions - the terrain shader does not know where those stand, and passing ~240 of
//them in is a limit and a cost this does not need. What the floor gets is shadow that READS as woods: dappled
//where the canopy is open, denser under a stand, swept down-sun the way a real shadow is. The scatter's real
//trees stand closer in (inside ClearingRadius) where density is 0, so the two never argue about who shadows whom.
//
//A cell holds one tree: a hash picks its offset within the cell, its height and its crown radius. The shadow is
//the closest approach of the sun ray to that tree's trunk axis, tested against the crown radius at the height the
//ray passes it - a cylinder+sphere stand-in for the crown, cheap and analytic. A short march across the grid
//cells the sun ray walks through catches the trees it could actually pass behind.
float ForestShadow(float3 worldPosition, float3 sunDir, float density)
{
	//A flat clearing (density 0) is in full sun: the wood's trees stand outside it, and the procedural wood
	//begins where density rises. This one is NOT the uniform branch CLAUDE.md's convention describes - density
	//is canopyRamp, which varies per pixel - so it does diverge, along the one ring of pixels at the clearing's
	//edge where neighbours disagree. It is still the right shape: the march below has no gradient ops in it (no
	//sampling, only arithmetic and CloudHash22), so nothing here needs neighbouring lanes to have taken it, and
	//the whole clearing interior - most of the floor the player ever sees up close - skips the march outright.
	[branch]
	if (density <= 0.001) return 1.0;

	float grid = FOREST_SHADOW_CELL;          //world units between virtual trees
	float3 ro = worldPosition;

	//Walk the sun ray across the grid in small steps, accumulating the deepest shadow any tree casts. The step
	//is a fraction of the cell so a tree between two samples is not skipped; the march is short because a crown's
	//shadow reaches only so far down-sun.
	float shadow = 1.0;
	float maxStep = FOREST_SHADOW_REACH;
	float step = grid * 0.35;
	float t = step;

	[loop]
	for (int i = 0; i < 10; i++)
	{
		if (t > maxStep) break;

		float3 p = ro + sunDir * t;

		//The cell the ray has walked into, and that cell's single tree.
		float2 cell = floor(p.xz / grid);
		float2 h = CloudHash22(cell * 7.0 + 13.0);
		float2 treeXZ = (cell + 0.5 + 0.36 * h) * grid;
		float treeHeight = FOREST_SHADOW_MIN_H + h.x * (FOREST_SHADOW_MAX_H - FOREST_SHADOW_MIN_H);

		//Closest approach of the sun ray (from the shaded point) to the tree's trunk axis (the line straight up
		//through treeXZ), in the horizontal plane only - the shadow a vertical trunk throws is what this measures.
		float2 toTree = treeXZ - ro.xz;
		float along = dot(toTree, sunDir.xz);
		float2 perp = toTree - along * sunDir.xz;
		float closestDist = length(perp);

		//The crown is a disc at treeHeight; the ray passes that height at rayHeight. Shadow if the ray is under
		//the crown at the horizontal point it crosses it - softened across the crown's radius for a penumbra.
		float rayHeight = ro.y + along / max(sunDir.xz.x * sunDir.xz.x + sunDir.xz.y * sunDir.xz.y, 0.0001) * sunDir.y;
		float crownRadius = lerp(treeHeight * 0.35, treeHeight * 0.55, h.y);

		float under = saturate((crownRadius - closestDist) / crownRadius);
		float atHeight = smoothstep(treeHeight * 0.3, treeHeight, rayHeight);
		shadow = min(shadow, 1.0 - under * atHeight * 0.75);

		t += step;
	}

	//Density shapes how deep the shadow lands: a clearing (0) is untouched, full wood (1) gets the lot.
	return lerp(1.0, shadow, density);
}

//`detail` is an ordinary function argument passed a LITERAL by each of the two entry points below, so it is
//constant-folded at compile time and each of them comes out a SEPARATE PROGRAM - the reduced one with the
//expensive halves gone and, crucially, with its own register allocation.
//
//It began as a `float FloorDetail` uniform with two [branch]es on it, which measured EXACTLY NOTHING: 2.72 ms
//against the full look's 2.70, where deleting the same two pieces of code measures 2.09. That is what an
//occupancy-bound pass does - the register footprint is decided for the whole shader when it is compiled, so
//a runtime branch skips the WORK and keeps the REGISTERS, and the occupancy that was the real limit never
//rises. A cost of that shape can only be bought back by compiling a different program.
//
//And it is two entry points rather than the tidier `compile PS_SHADERMODEL ForestPS(true)`: MGFX cannot
//parse a uniform argument in a compile statement ("Unexpected token 't' found. Expected CloseParenthesis").
float4 ForestFloor(ForestVertexOutput input, bool detail)
{
	float3 worldPosition = input.WorldPosition;

	//Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
	clip(length(worldPosition.xz) - IslandHoleRadius);

	float3 baseNormal = TerrainNormal(worldPosition.xz);
	float footprint = length(fwidth(worldPosition.xz));

	//Fine needle/moss texture tilts the normal, so the floor catches the light unevenly and the wind reads on it
	float relief = NeedleRelief(worldPosition.xz, footprint);
	float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

	//A second, finer relief layer over the needle waves: triplanar FBM perturbs the normal with the broken
	//micro-relief a single frequency cannot (VaryNormal, after dr2's VaryNf). Where the needle relief is the
	//comb the wind reads on, this is the rough moss-and-root grain that stops the floor shading flat. Lighter
	//than the needle relief itself, and band-limited through ForestFbm so the near floor gets the detail and the
	//far ridge does not shimmer.
	//Behind FloorDetail: one of the two extras the reduced tier gives up. What is lost is the near floor's
	//micro-grain, which the band limit has already faded to nothing a short way out - so the reduced floor is
	//the same picture beyond arm's length and a slightly smoother one underfoot.
	if (detail) normal = VaryNormal(worldPosition, normal, 1.8, 0.35, footprint);

	//Undergrowth colour: mossy green in broad patches, varying towards the dark needle litter and shadow, so
	//the floor is not one flat green but mottled the way a real clearing is. ForestFbm (four octaves of the shared
	//gradient noise) replaces the single CloudNoise tap a single octave reads as: smooth patches, where four sum
	//to the broken organic mottle a real forest floor has. Spread and saturated the same way as before - CloudNoise
	//clusters hard around zero (one sigma 0.18, 5-95% inside +/-0.3), so the gain is well over 1.
	float patch = saturate(ForestFbm(worldPosition.xz * 0.15, footprint) * 1.4 + 0.5);

	//Slope drives the colour the way it does on a real bank: flats keep the moss (cool green, ForestColor),
	//slopes wash to bare earth and litter (warm-dark, ForestColorDark dimmed). dr2 mixes by vn.y the same way;
	//without it the floor reads as one material however it is lit, because the colour never answers the form.
	float slope = smoothstep(0.55, 0.85, baseNormal.y);
	float3 floor = lerp(ForestColorDark * 0.8, ForestColor, patch * slope);

	//Wind combing the undergrowth: bright and dark bands travelling downwind, the clearing's own motion
	float wind = sin(dot(worldPosition.xz, WindDirection) * WindRippleFrequency + ForestTime * WindRippleSpeed);
	floor *= 1.0 + wind * WindRippleStrength;

	//Scattered darker litter patches - the fallen needles and leaf decay that sit between the moss, finer than
	//the broad colour patches and darker still, so the floor reads as layered ground cover rather than one tone.
	//ForestFbm at a finer scale, the same spread-and-saturate.
	float litter = saturate(ForestFbm(worldPosition.xz * 0.6 + 47.0, footprint) * 1.6 + 0.5);
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
	//that drift across the sky sweep their shadows over the clearing, AND by the procedural tree shadow so the
	//woods cast their own dapple. The tree shadow's density rides the canopy ramp - 0 in the open clearing (the
	//scatter's real trees stand there, and theirs is the only shadow that should show), rising to full under the
	//procedural wood beyond it. Lower ambient than the meadow: a clearing is shaded by the trees around it.
	float sunlight = CloudSunlight(worldPosition, SunDirection);

	//The other extra behind FloorDetail. The cloud shadow above is NOT given up with it: that one is shared
	//with every other scene and costs a fraction of this, and a clearing with no drifting shade at all reads
	//as a different weather rather than as a cheaper frame.
	float treeShadow = 1.0;

	if (detail) treeShadow = ForestShadow(worldPosition, SunDirection, canopyRamp);
	float ndotl = saturate(dot(normal, SunDirection));
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = floor * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight * treeShadow);

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

//Two programs from one body. "Forest" is the authored floor; "ForestReduced" is the same floor without the
//triplanar normal variation and without the procedural tree shadows - the pair that has to go together,
//since removing either alone saves nothing. The caller picks by tier; SceneRenderer.TerrainDetail decides.
float4 ForestPS(ForestVertexOutput input) : COLOR { return ForestFloor(input, true); }
float4 ForestReducedPS(ForestVertexOutput input) : COLOR { return ForestFloor(input, false); }

technique Forest
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL ForestVS();
		PixelShader = compile PS_SHADERMODEL ForestPS();
	}
};

technique ForestReduced
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL ForestVS();
		PixelShader = compile PS_SHADERMODEL ForestReducedPS();
	}
};
