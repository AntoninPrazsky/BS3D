//Draws the Moon: a cratered grey regolith plain under a black, starlit sky with the Earth hanging in it at
//its real angular size - the Apollo-photo look. Twelfth scene variant (#125), and the FIRST that belongs to
//BOTH scene families at once: it is a solid-terrain scene (the island stands on real displaced ground, the
//terrain cuts the island's footprint out, the dark pit shaft backs the drain) AND a sky-replacing one (no
//dome, no clouds, no atmosphere - a full-screen pass paints the stars and the Earth where a dome would be).
//Every prior scene was exactly one of the two, and SceneRenderer's classification docs say why the Moon is
//deliberately in both.
//
//Two techniques over two passes of one frame:
//  - MoonSky: Space.fx's full-screen machinery (an NDC quad, the view ray through InverseViewProjection,
//    depth state off) painting a near-black void, the shared three-layer starfield (Stars.fxh - one copy
//    with Space.fx, same lattice, same glare discipline) and an analytically ray-traced Earth.
//  - MoonTerrain: the desert's machinery (a camera-centred displaced grid, snapped to its cell on the CPU,
//    the base normal taken PER PIXEL from the height field's own gradient) with a CRATER field instead of
//    dunes, and none of the atmosphere: no horizon haze, no dust, no cloud shadow - there is no air. The
//    horizon is closed by CURVATURE instead: the height field falls with the square of the distance, the
//    way a small world's surface does, so nearer ground occludes the grid's far edge and the skyline is a
//    hard sunlit line against black - the Moon's horizon is only a couple of kilometres out for exactly
//    this reason.
//
//Nothing here takes a time uniform, deliberately: no wind, no weather, no waves, not a single moving thing.
//The Moon is the stillest scene in the game, and the stillness is part of the mood. (In vacuum the stars do
//not twinkle either - Stars.fxh already guarantees that.)
//
//Everything is written in LINEAR RADIANCE into the HDR target. The Earth is small (about 1.9 degrees across,
//its true size from the Moon - a deliberate expectation-setting decision, see MoonEarthConfig), so unlike
//Space.fx's planet its lit body is NOT allowed over GLARE_THRESHOLD: a ~30-pixel disc is no "long coherent
//arc", and a small bright thing the glare's sparse grid samples stochastically flickers. Only its thin
//atmosphere rim is allowed a touch over, carried by saturated blue whose LUMINANCE stays modest.
//
//Built by all three executables out of this directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//The shared starfield lattice and its uniforms (StarCellScale/Chance/Peak, StarSpread, StarFalloff, the
//spike pair, SupersampleFactor), one copy with Space.fx.
#include "Stars.fxh"

//The shared noise library, for the crater jitter, the regolith's broad albedo patches and the Earth's
//continents and clouds. Quintic-fade gradient noise: the terrain field feeds a normal, where the cubic
//fade's discontinuous second derivative would show as lattice creases.
#include "Noise.fxh"

//--- Shared uniforms -----------------------------------------------------------------------------------

float4x4 View;
float4x4 Projection;
float4x4 InverseViewProjection;

float3 CameraPosition;

//Towards the sun - the same direction the island, the gun and the balls take, which is what ties the
//terrain's raking light and the Earth's phase to the light everything else in the frame is lit by.
float3 SunDirection;

//The sun's own radiance for the terrain (linear, over 1 - it is a sun). The config's value, NOT the frame's
//dome-derived SunColor: this scene draws no dome, and a sky-replacing scene that shaded its ground with a
//dome-derived sun would be lying about a light that is not there (the same argument TryGetLightRig's doc
//makes for the ambient).
float3 SunColor;

//The sky fill on the ground (linear, tiny). A black starlit sky is not quite zero - starlight plus the
//scene's own earthshine - and at exactly zero the shadowed side of every crater reads as a hole cut in the
//ground (the space scene's lesson about the island, at terrain scale).
float3 AmbientColor;

//Earthshine: a directional fill from the Earth's own direction, cool and blue. What makes a shadowed slope
//facing the Earth read a shade less black than one facing away - the classic Apollo fill light.
float3 EarthshineColor;

//--- Terrain uniforms ------------------------------------------------------------------------------------

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), and the island's footprint cut out
//of the terrain around the world origin (the Testbed and the Game set it; the map editor draws no island, so
//it leaves 0 and nothing is cut).
float2 OriginXZ;
float IslandHoleRadius;

//The mean regolith level in the clearing (the island's foot), and the flat clearing that rises into cratered
//ground over its transition band - the same shape every solid-terrain sibling uses around the round island.
float MoonLevelY;
float ClearingRadius;
float ClearingTransition;

//Peak height of the crater field out in the far field (world units over the whole three-octave sum).
float CraterAmplitude;

//The planet's curvature: the height field drops Curvature * distance^2, which is what closes the horizon.
//1/(2R) of the small world being stood on - at the shipped 8e-5 the "moon" has a radius of 6.25 km and the
//horizon stands 360-450 units out from the play and menu cameras. That places it INSIDE the Game camera's
//500-unit far plane, which is the constraint the value is sized against: halve it and the far plane cuts
//the terrain before the curvature can occlude it, putting a dead-level, camera-locked clip line where the
//horizon should be (MoonSceneConfig.Curvature carries the same warning).
float Curvature;

//Regolith reflectance (linear): the dark grey of the plains and the paler grey of fresh ejecta. The real
//thing is astonishingly dark (albedo ~0.12) but sits under a sun with no air in the way; these are authored
//a touch over the physical value so the scene keys to the game's exposure.
float3 RegolithColor;
float3 RegolithColorPale;

//How strongly a crater's raised rim brightens towards RegolithColorPale (fresh excavated material - what
//makes young craters read pale against the plains, and the cheapest "ejecta" there is).
float EjectaBrightness;

//Fine surface: peak height of the pixel-scale relief (world units) and the near-camera grain strength.
float MicroReliefStrength;
float GrainStrength;

//--- Sky uniforms ------------------------------------------------------------------------------------------

//The void between the stars (linear). Even blacker than space's: the lunar sky has no airglow at all. Not
//exactly zero - a frame that goes to zero reads as a hole rather than as darkness.
float3 VoidColor;

//--- The Earth ---------------------------------------------------------------------------------------------

float3 EarthDirection;      //where it hangs, normalized
float EarthAngularRadius;   //radians. ~0.0166 (0.95 deg) is the true value - see MoonEarthConfig
float3 EarthAxis;           //its pole; the continents and clouds are laid out in this frame

float3 OceanColor;          //the deep marble blue (linear)
float3 LandColor;           //vegetated land (linear)
float3 LandColorArid;       //desert land (linear)
float3 CloudColor;          //the white swirls (linear)
float CloudAmount;          //0..1, how much of the disc the weather covers
float3 RimColor;            //the thin atmosphere seen edge-on (linear)
float RimStrength;
float NightAmbient;         //what the unlit side keeps, so it stays a sphere and not a hole

//=====================================================================================================
//The terrain
//=====================================================================================================

//One octave of craters: at most one per cell of a jittered lattice, and only the pixel's OWN cell is ever
//read - the star lattice's single-cell trick (Stars.fxh), which works because the margin below holds the
//whole crater, skirt included, inside its cell, so no neighbour's crater can reach this point. This field
//is evaluated four times per terrain pixel (three normal taps and the small-crater detail octave); a 3x3
//neighbourhood walk here would cost nine times the hashes for craters that can never contribute, and the
//first build did exactly that - it ran at 2 FPS on the reference APU where the finished scene runs ~50,
//all of it in hashes answering "no".
//
//Each crater rolls its own existence, position, radius, depth and rim sharpness off the cell hash - the
//variation is load-bearing: a sum of IDENTICAL bowls reads as a regular texture, which is the
//plane-wave-sine failure in bowl form (the issue that asked for this field says so in as many words). The
//confinement does mean two craters of ONE layer never overlap; the three layers run on unrelated lattices,
//so craters-inside-craters still happen freely across scales.
//
//The profile is a bowl with a raised rim and nothing past the rim's skirt:
//    d < 1        : the bowl, depth * (a smooth parabola-ish cup)
//    d ~ 1        : the rim lip, a smooth bump of about a third of the depth
//    d > ~1.6     : exactly zero, so a crater ends and the plain between craters is genuinely flat
//
//The bowl and the rim are balanced so one crater's MEAN over its cell is near zero - a real crater's rim
//and apron are the excavated bowl put back on the surface, and the practical reason is the desert's: the
//whole field is multiplied by the clearing ramp, and a field with a mean would make the ramp a visible
//bowl or pedestal around the island.
//
//Returns the height in units of `depth`, and a 0..1 "rim freshness" the caller colours ejecta with.
//Cost: three 2D hashes and a little arithmetic.
float CraterLayer(float2 p, float seedOffset, out float ejecta)
{
	float2 cellId = floor(p);
	float2 f = p - cellId;

	ejecta = 0.0;

	//Three independent rolls per candidate crater: whether/what shape, size/depth, and where. Separate
	//hashes rather than one reused - a position correlated with a rim width is a correlation nobody would
	//name but a texture the eye would still catch.
	float2 rollA = NoiseHash22(cellId + seedOffset) * 0.5 + 0.5;

	//Not every cell carries a crater - a plain with a crater in every cell of a lattice IS the lattice,
	//however hard the jitter works. The empty cells are what break the grid.
	if (rollA.x > 0.62) return 0.0;

	float2 rollB = NoiseHash22(cellId + seedOffset + 47.9) * 0.5 + 0.5;
	float2 rollC = NoiseHash22(cellId + seedOffset + 91.7) * 0.5 + 0.5;

	//The centre is jittered inside the middle of the cell, held clear of the edge by the crater's own
	//reach (radius * 1.6 skirt) - which is exactly what makes the single-cell read above sound, the way
	//the star lattice and the meadow's flowers hold their margins.
	float radius = lerp(0.16, 0.30, rollB.x);
	float margin = radius * 1.6;
	float2 centre = margin + rollC * (1.0 - 2.0 * margin);

	float d = length(f - centre) / radius;

	//Past the rim skirt this crater contributes nothing
	if (d >= 1.6) return 0.0;

	float depth = lerp(0.55, 1.0, rollB.y);

	//The cup: flat-bottomed rather than a cone (real simple craters have a bowl floor), zero at the rim
	//line d = 1
	float cup = saturate(1.0 - d * d);
	float bowl = -cup * cup * depth;

	//The rim: a smooth lip centred just past d = 1, its width (sharpness) the crater's own roll - an
	//eroded old crater has a soft wide lip, a young one a tight sharp one. VARYING this is what keeps a
	//field of bowls from reading as a texture.
	float rimWidth = lerp(0.18, 0.42, rollA.y);
	float rimT = (d - 1.0) / rimWidth;

	//The gaussian's tail is taken to EXACTLY zero across the outer half of the skirt, because the early
	//return above is a hard cut and at the widest rims the bare gaussian still holds ~0.13 at d = 1.6 -
	//which drew every soft-rimmed crater with a circular cliff at its skirt, a shading ring where the
	//finite-difference normal straddled the step, and its ejecta cut dead on the same circle. The fade
	//starts past the lip's crest, so the lip itself keeps its full profile.
	float rim = exp(-rimT * rimT) * smoothstep(1.6, 1.1, d);

	//Fresh material where the rim stands, fading with the rim itself
	ejecta = rim * rollB.y;

	//0.62: the weight at which the lip plus its apron hands back roughly the volume the cup took out, so
	//the layer stays near mean-zero (measured over the profile, not eyeballed: the cup's area integral is
	//~0.53 of the disc and the gaussian annulus at this width returns it).
	return bowl + rim * depth * 0.62;
}

//The crater field: three octaves, so small craters sit inside and on top of larger ones the way the real
//surface is layered. Amplitudes fall roughly with the radius (a crater's depth scales with its size), and
//each octave has its own seed so the three lattices share nothing.
float CraterField(float2 p, out float ejecta)
{
	float e0, e1, e2;

	float height = CraterLayer(p * (1.0 / 90.0), 11.3, e0) * 0.58
		+ CraterLayer(p * (1.0 / 34.0), 37.7, e1) * 0.29
		+ CraterLayer(p * (1.0 / 13.0), 71.1, e2) * 0.13;

	//The freshest rim wins: ejecta is a colour cue, not a height, so the octaves MAX rather than sum -
	//summed, three faint aprons stack into a pale wash that reads as dirt rather than as rays.
	ejecta = max(e0 * 0.9, max(e1, e2 * 0.8));

	return height;
}

//Gentle mare undulation under the craters, so the plain is not a snooker table between them. Two octaves
//of gradient noise - genuinely two, not a sine pair; a sum of plane waves keeps its planes (Noise.fxh's
//opening, learned by three scenes the hard way).
float MareBase(float2 p)
{
	return GradientNoise2(p * 0.011) * 0.65 + GradientNoise2(p * 0.031 + 7.3) * 0.35;
}

//The full displaced height at a world point: flat at MoonLevelY inside the clearing around the island,
//rising into cratered ground with distance, the whole surface falling away with the square of the distance
//so the horizon closes. Tapped to displace the vertex (VS) and, thrice, for the per-pixel normal (PS) -
//the one field, so the silhouette and the shading can never drift apart.
//
//The ejecta output is only read in the pixel shader's own tap; the vertex shader ignores it (the compiler
//strips the dead half there).
float MoonHeight(float2 p, out float ejecta)
{
	float dist = length(p);
	float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

	float field = CraterField(p, ejecta) + MareBase(p) * 0.18;

	//The curvature is OUTSIDE the ramp: the clearing must stay flat where the island's physics floor is,
	//but the fall of the horizon is the planet's, not the field's, and ramping it would put a crease at
	//the clearing's edge. Inside ClearingRadius the drop is under a hundredth of a unit - nothing.
	return MoonLevelY + CraterAmplitude * ramp * field - Curvature * dist * dist;
}

struct MoonTerrainVertexInput
{
	float4 Position : POSITION0;
};

struct MoonTerrainVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
};

MoonTerrainVertexOutput MoonTerrainVS(MoonTerrainVertexInput input)
{
	MoonTerrainVertexOutput output;

	//Local grid position + the snapped origin gives the world XZ; the craters are sampled there, so they
	//sit still in the world while the grid slides under them
	float2 worldXZ = input.Position.xz + OriginXZ;

	float ejectaUnused;
	float3 worldPosition = float3(worldXZ.x, MoonHeight(worldXZ, ejectaUnused), worldXZ.y);

	output.WorldPosition = worldPosition;
	output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

	return output;
}

//Tangent-free normal tilt from a height field (Christian Schueler), the same one the desert, the sea chop
//and the balls use - the grid carries no tangents and the micro-relief never reaches it anyway.
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

float4 MoonTerrainPS(MoonTerrainVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;

	//Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
	clip(length(worldPosition.xz) - IslandHoleRadius);

	float footprint = length(fwidth(worldPosition.xz));

	//The base normal, taken PER PIXEL from the height field's gradient (three taps) rather than
	//interpolated from a per-vertex normal - the savanna's fix, the desert's practice: on a coarse
	//displaced mesh a per-vertex normal leaves a Mach-band grid, and per-pixel evaluation makes the
	//shading smooth regardless of tessellation.
	float e = 1.5;
	float ejecta, ejectaX, ejectaZ;
	float h = MoonHeight(worldPosition.xz, ejecta);
	float hx = MoonHeight(worldPosition.xz + float2(e, 0.0), ejectaX);
	float hz = MoonHeight(worldPosition.xz + float2(0.0, e), ejectaZ);

	float2 slope = float2(hx - h, hz - h) / e;
	float3 baseNormal = normalize(float3(-slope.x, 1.0, -slope.y));

	//Fine surface on top of the crater normal: a fourth, small crater octave the mesh could never hold
	//(normal-only - at this scale a crater is shading, not silhouette) plus an isotropic regolith relief.
	//Both band-limited against the footprint; the perturbation is derivative-driven and would checkerboard
	//the moment a wave nears pixel size.
	float smallEjecta;
	float smallCraters = CraterLayer(worldPosition.xz * (1.0 / 5.0), 133.7, smallEjecta)
		* saturate(1.0 - footprint * (2.0 / 5.0));

	float relief = Fbm2BandLimited(worldPosition.xz * 1.7, 3, footprint * 1.7);

	float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition,
		smallCraters * (MicroReliefStrength * 3.0) + relief * MicroReliefStrength);

	ejecta = max(ejecta, smallEjecta * 0.5);

	//--- The regolith's colour -----------------------------------------------------------------------
	//Grey on grey, but never ONE grey: broad albedo patches (old flows and ray systems are visibly
	//lighter and darker from every Apollo window), pale fresh ejecta on the crater rims, and a
	//per-pixel grain close up - regolith at arm's length is glass beads and crushed rock, and the grain
	//is the only cue at that distance that says so.
	float broad = GradientNoise2(worldPosition.xz * 0.021);

	float3 regolith = lerp(RegolithColor, RegolithColorPale, saturate(broad * 1.5 + 0.35));

	//Fresh rims brighten towards the pale grey - the cheapest ejecta there is, and what makes the young
	//craters read young
	regolith = lerp(regolith, RegolithColorPale * 1.18, saturate(ejecta * EjectaBrightness));

	//The grain, faded out BEFORE its cells reach pixel size (the desert's rule - faded at the cell size
	//the last metres are a crawling speckle)
	float grainFade = saturate(1.0 - footprint * 96.0);
	regolith *= 1.0 + NoiseHash22(floor(worldPosition.xz * 48.0)).x * GrainStrength * grainFade;

	//--- Lighting -------------------------------------------------------------------------------------
	//A sun and almost nothing else, which is the whole look: no air means no sky fill and no aerial
	//perspective, so the shadowed side of a crater goes very nearly black and the horizon stays razor
	//sharp at any distance. AmbientColor is the tiny floor that keeps "very nearly" from being a hole,
	//and the earthshine is a real directional fill - the two together are still far under any dome's
	//hemisphere. There is no haze term in this shader at all, deliberately.
	float ndotl = saturate(dot(normal, SunDirection));

	float3 fill = AmbientColor + EarthshineColor * saturate(dot(normal, EarthDirection));

	float3 color = regolith * (SunColor * ndotl + fill);

	return float4(color, 1.0);
}

//=====================================================================================================
//The sky
//=====================================================================================================

//The Earth, solved analytically the way Space.fx solves its gas giant: a unit sphere at the distance that
//gives the configured angular radius, the ray test a quadratic, the surface normal falling out of it. What
//is different is the surface - continents under swirled weather instead of banded clouds - and the size:
//this disc is ~30 pixels at the window the game usually runs, not half the frame, so its lit body stays
//UNDER the glare threshold (see the header).
float3 Earth(float3 dir, float pixelAngle, out float coverage)
{
	coverage = 0.0;

	float cosine = dot(dir, EarthDirection);
	float cosLimb = cos(EarthAngularRadius);

	//A little slack past the limb so the atmosphere's halo is reached as well
	float halo = cos(EarthAngularRadius * 1.35);

	[branch]
	if (cosine <= halo || EarthAngularRadius <= 0.0) return 0.0;

	//The limb is where cos(angle) crosses cosLimb, antialiased over one pixel's worth of angle
	float edge = max(pixelAngle * sin(EarthAngularRadius) * 0.8, 1e-6);
	coverage = smoothstep(cosLimb - edge, cosLimb + edge, cosine);

	//The rim of atmosphere standing off the disc: a thin blue arc, lit only where the sun is. On a disc
	//this small it is most of what says "atmosphere", so it is allowed a touch of glare - but through a
	//saturated blue, whose luminance stays modest however bright the blue channel runs.
	//
	//Lit PER POINT of the ring, not by one global phase factor: for a ring direction dir near the disc,
	//dot(dir, sun) exceeds the disc centre's own dot exactly on the sunward side, so the difference -
	//normalized by the disc's angular radius - is which side of the planet this piece of atmosphere is on.
	//One factor for the whole ring (the first build) drew a complete blue circle around a gibbous Earth,
	//night limb included, which reads as a ring around the planet rather than as its air.
	float3 outside = 0.0;
	if (coverage < 0.999)
	{
		float ring = saturate((cosine - halo) / max(cosLimb - halo, 1e-5));
		float sunward = (dot(dir, SunDirection) - dot(EarthDirection, SunDirection))
			/ max(sin(EarthAngularRadius) * 1.5, 1e-5);
		float lit = saturate(sunward + 0.35);
		outside = RimColor * (RimStrength * 0.5 * ring * ring * lit * (1.0 - coverage));
	}

	if (coverage <= 0.0009) return outside;

	//Ray-sphere: a unit sphere centred at distance 1/sin(R) along the Earth's direction
	float distance = 1.0 / max(sin(EarthAngularRadius), 1e-4);
	float discriminant = max(distance * distance * (cosine * cosine - 1.0) + 1.0, 0.0);
	float t = distance * cosine - sqrt(discriminant);
	float3 normal = normalize(t * dir - distance * EarthDirection);

	//The Earth's own frame, so the continents stay put on it as the camera moves
	float3 right, forward;
	BuildFrame(EarthAxis, right, forward);
	float latitude = dot(normal, EarthAxis);
	float3 local = float3(dot(normal, right), latitude, dot(normal, forward));

	//Continents: a low-frequency fractal thresholded into land and ocean. The threshold puts about a
	//third of the sphere under land, which is the real ratio; the coastline detail rides the fractal's
	//own octaves. THREE octaves, not four, and the frequencies are chosen against the disc's size on
	//screen rather than for close-up richness: this sphere is ~30 pixels across, so an octave whose
	//features fall under a couple of pixels contributes speckle, not coastline - the first build ran a
	//fourth octave and the marble came out peppered.
	float continents = Fbm3(local * 2.2 + 19.0, 3);
	float land = smoothstep(-0.02, 0.06, continents);

	//Vegetated against arid by a second, coarser field offset from the first, so the deserts sit in
	//belts of their own rather than tracking the coastlines
	float arid = saturate(Fbm3(local * 1.8 + 53.0, 3) * 1.8 + 0.5);
	float3 landColor = lerp(LandColor, LandColorArid, arid);

	float3 surface = lerp(OceanColor, landColor, land);

	//Polar ice, its edge broken by the continents' own fractal so the caps are not compass circles
	float ice = smoothstep(0.78, 0.88, abs(latitude) + continents * 0.07);
	surface = lerp(surface, float3(0.85, 0.88, 0.92), ice);

	//The weather: a swirled cloud field over everything. Swirl by the cheap warp trick - the field is
	//sampled through an offset driven by another octave - because straight fbm clouds read as static
	//mottle, and Earth's clouds are storms drawn out into hooks and fronts. The mask's band is deliberately
	//high and narrow: the marble must stay BLUE with white swirls on it, and the first build's wide band
	//painted most of the disc into partial cloud and handed back a white planet with blue flecks.
	float3 swirl = float3(
		Fbm3(local * 2.7 + 91.0, 3),
		Fbm3(local * 2.5 + 137.0, 3),
		Fbm3(local * 2.9 + 173.0, 3));

	float clouds = Fbm3(local * 2.8 + swirl * 1.2 + 7.0, 3);
	float cloudMask = smoothstep(0.68 - CloudAmount * 0.25, 0.86 - CloudAmount * 0.25, clouds + 0.5);

	surface = lerp(surface, CloudColor, cloudMask);

	//A soft terminator - never a hard N.L cut (the planet's rule) - but NARROWER than the gas giant's:
	//Earth's atmosphere is thin, and a wide twilight band on a 30-pixel disc smears the phase away.
	float ndotl = dot(normal, SunDirection);
	float daylight = smoothstep(-0.08, 0.22, ndotl);

	float3 lit = surface * (daylight + NightAmbient);

	//The atmosphere ON the disc: a blue grazing-angle veil, strongest at the limb - what makes the marble
	//read as wrapped in air rather than painted
	float grazing = 1.0 - saturate(dot(normal, -dir));
	lit += RimColor * (RimStrength * pow(grazing, 3.0) * saturate(daylight + 0.1));

	return lit * coverage + outside;
}

struct MoonSkyVertexOutput
{
	float4 Position : SV_POSITION;
	float3 Ray : TEXCOORD0;
};

MoonSkyVertexOutput MoonSkyVS(float3 position : POSITION0)
{
	MoonSkyVertexOutput output;

	//The quad arrives already in normalized device coordinates; z = w puts it on the far plane — and
	//unlike Space.fx's sibling this pass runs under DepthStencilState.DepthRead, AFTER the terrain: at
	//exactly the far plane LessEqual passes against untouched depth (cleared to 1.0) and fails against
	//anything the terrain wrote, which is the whole terrain-occludes-sky optimization. (The ray maths is
	//Space.fx's, verbatim: the far plane is a plane in world space and the map from screen to it is
	//affine, so interpolating the ray across the quad is exact.)
	output.Position = float4(position.xy, 1.0, 1.0);

	float4 far = mul(float4(position.xy, 1.0, 1.0), InverseViewProjection);
	output.Ray = far.xyz / far.w - CameraPosition;

	return output;
}

float4 MoonSkyPS(MoonSkyVertexOutput input) : COLOR
{
	float3 dir = normalize(input.Ray);

	//This pixel's angular footprint, measured on the DIRECTION rather than on any chart of it (continuous
	//everywhere, so the cube lattice's twelve seams never show - Space.fx's rule).
	float pixelAngle = max(length(fwidth(dir)), 1e-6);

	float3 sky = VoidColor;

	//The stars: the shared three-layer lattice, nothing dimming them - no Milky Way band here, no dust,
	//no nebulae. A lunar sky is stars on black and the Earth, and the sparseness IS the look.
	sky += StarLayer(dir, pixelAngle, StarCellScale[0], StarChance[0], StarPeak[0], true);
	sky += StarLayer(dir, pixelAngle, StarCellScale[1], StarChance[1], StarPeak[1], false);
	sky += StarLayer(dir, pixelAngle, StarCellScale[2], StarChance[2], StarPeak[2], false);

	//A fine dither against banding, the space scene's own (the void is an enormous area crossed by
	//almost no gradient, which is where an 8-bit back buffer draws contour rings). Hashed on all three
	//components of the quantised direction: any 2D pick goes flat along one screen axis wherever that
	//axis maps onto the dropped component, and a 1D dither is stripes. Applied to the sky alone; the
	//Earth is composited after and a lit disc has gradient of its own.
	sky *= 1.0 + NoiseHash33(floor(dir / pixelAngle)).x * 0.015;

	//The Earth stands in front of the stars
	float coverage;
	float3 earth = Earth(dir, pixelAngle, coverage);
	sky = sky * (1.0 - coverage) + earth;

	return float4(sky, 1.0);
}

technique MoonSky
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MoonSkyVS();
		PixelShader = compile PS_SHADERMODEL MoonSkyPS();
	}
};

technique MoonTerrain
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MoonTerrainVS();
		PixelShader = compile PS_SHADERMODEL MoonTerrainPS();
	}
};
