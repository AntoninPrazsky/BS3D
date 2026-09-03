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

//The highland belt: the massifs that ring the mare the island stands on, and the ONLY part of this scene the
//play camera can see. The reason is geometric and worth stating where the figures are: the gameplay lens sits
//at GameCameraFit.LENS_FLOOR_Y = -7.9, which is 0.6 units over the island's own deck plane
//(ArenaIsland.TOP_Y = -8.5), so the deck occludes every ray steeper than ~atan(0.6 / distance-to-the-far-arris)
//- half a degree of depression at a full map's stand-off. A curved plain's skyline sits 2*sqrt(eyeHeight *
//Curvature) BELOW the eye - 2.4 degrees at the shipped figures - which is far under that line, so the first
//build of this scene played as a black sky over bare stone with no ground in it anywhere. Neither obvious dial
//reaches: raising MoonLevelY keeps the skyline below the deck even with the plain flush against it (0.8 deg at
//zero eye height), and slackening Curvature runs into the 500-unit far plane long before the skyline clears
//(see the warning on Curvature). What clears the deck is RELIEF THAT STANDS ABOVE THE LENS - which is exactly
//why the atmospheric siblings' ground is visible at all: the desert's 14-unit dunes crest at y = +0.5, eight
//units over the same lens.
//
//So the mare is a basin with a rim, as most of them are: the ground climbs from HighlandInnerRadius to
//HighlandCrestRadius, and past the crest the curvature takes over again and closes the horizon as before -
//quadratic growth against a saturated rise, so the elevation falls monotonically outward and the far-plane cut
//stays hidden behind the crest, which is the constraint MOON_EXTENT and Curvature were sized against.
float HighlandHeight;        //the belt's full rise over the plain at its crest (world units)
float HighlandInnerRadius;   //where the ground starts to climb - well past the crater plain's clearing ramp
float HighlandCrestRadius;   //where it reaches HighlandHeight; the skyline stands here
float HighlandSaddleFloor;   //0..1, the fraction of the height the LOWEST saddle keeps (see HighlandBelt)

//The planet's curvature: the height field drops Curvature * distance^2, which is what closes the horizon
//PAST the highland belt. 1/(2R) of the small world being stood on - at the shipped 8e-5 the "moon" has a
//radius of 6.25 km. The skyline itself is the belt's crest at HighlandCrestRadius, not the curvature bulge:
//the bulge alone put the horizon 360-450 units out and 2.4 degrees BELOW the play camera's eye, where the
//island's own deck hides it (the HighlandHeight block has the geometry). What the curvature still does is
//everything past the crest - quadratic drop against the belt's saturated rise, so the ground's elevation
//falls monotonically outward from the crest and the far-plane cut stays behind it. That places the cut
//INSIDE the occluded region, which is the constraint the value is sized against: halve it and the far plane
//cuts the terrain before the curvature can occlude it, putting a dead-level, camera-locked clip line through
//the saddles of the belt (MoonSceneConfig.Curvature carries the same warning).
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
//The radius a crater is cut to, as a fraction of its own cell. The pair is set by the JITTER the margin
//below has to leave, not by how big a crater should look on the ground - the octave periods in CraterField
//carry the world size, and they were rescaled with these so no crater changed size when they moved.
//
//⚠ THE MARGIN IS THE CRATER'S OWN RADIUS TIMES ITS SKIRT, SO A CRATER THAT FILLS ITS CELL IS PINNED TO THE
//LATTICE - and that was #240 in one line. At the old 0.30 the box left for the centre was 1 - 2*0.30*1.6 =
//FOUR PER CENT of the cell, so the largest craters of every octave, which are the ones the eye picks out
//first, sat on their cell centres to within a fortieth of a cell. Photographed from above, the plain was a
//stamped carpet of rings in rows and columns. The jitter was never weakest where it mattered least; it was
//weakest exactly where it mattered most, and no amount of hashing could have hidden that.
//
//At 0.21 the worst case leaves 33 % of the cell and the median crater 55 % (the squared roll below).
static const float CRATER_MIN_RADIUS = 0.12;
static const float CRATER_MAX_RADIUS = 0.21;

//What the regolith's interpolated grain octave is multiplied by so it keeps the amplitude the flat hash it
//replaced had (#345). NoiseHash22 fills -1..1 uniformly; GradientNoise2 over the same hash is a
//Perlin-style field whose values crowd towards zero and rarely reach half of that, so swapping one for the
//other at the same coefficient would have quietly halved the mottling as well as smoothing it - two
//changes where only one was asked for. Set so the surface keeps the tonal spread it had, which is what
//makes the fix READ as "the squares are gone" rather than "the ground got flatter".
static const float GRAIN_SMOOTH_GAIN = 2.0;

float CraterLayer(float2 p, float seedOffset, float chance, out float ejecta)
{
    float2 cellId = floor(p);
    float2 f = p - cellId;

    ejecta = 0.0;

    //Three independent rolls per candidate crater: whether/what shape, size/depth, and where. Separate
    //hashes rather than one reused - a position correlated with a rim width is a correlation nobody would
    //name but a texture the eye would still catch.
    float2 rollA = NoiseHash22(cellId + seedOffset) * 0.5 + 0.5;

    //Not every cell carries a crater - a plain with a crater in every cell of a lattice IS the lattice,
    //however hard the jitter works. The empty cells are what break the grid. Per octave since #240: the
    //finest layers were the loudest offenders, because a carpet of small craters puts dozens of cells in
    //one glance and a row of dozens reads as a row where a row of three does not.
    if (rollA.x > chance) return 0.0;

    float2 rollB = NoiseHash22(cellId + seedOffset + 47.9) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seedOffset + 91.7) * 0.5 + 0.5;

    //The centre is jittered inside the middle of the cell, held clear of the edge by the crater's own
    //reach (radius * 1.6 skirt) - which is exactly what makes the single-cell read above sound, the way
    //the star lattice and the meadow's flowers hold their margins. What it also does is trade the jitter
    //away against the radius, so the radius pair is chosen for the jitter (see CRATER_MAX_RADIUS).
    //
    //The roll is SQUARED, which weights the radii towards the small end. That is what a real crater count
    //does - N climbs steeply as the diameter falls, and a plain of one-size bowls is the plane-wave-sine
    //failure in bowl form - and it pays for itself twice over here, because a crater's margin IS its
    //radius: a field of mostly small craters is a field of mostly free centres. Median radius 0.14 of a
    //cell against a uniform law's 0.165, and a median jitter box of 55 % of the cell against 47 %.
    float radius = lerp(CRATER_MIN_RADIUS, CRATER_MAX_RADIUS, rollB.x * rollB.x);
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

//Each octave is TURNED TO ITS OWN BEARING before it is cut into cells, and the fourth (the pixel shader's
//5-unit detail layer) with them. A separate seed makes two lattices share no crater; it does not stop them
//sharing their ROWS, and `floor()` of an unturned domain puts every octave's rows along world X and Z. So
//the four grids lined up and each one drew the others' lattice in again at its own scale - which is why
//#240 reads as a single carpet in the photographs rather than as three faint ones. Free: two multiplies
//and an add per octave, off constants folded at compile time. The angles share no small ratio.
static const float2 CRATER_TURN_0 = float2(0.97437, 0.22495);   //13 degrees
static const float2 CRATER_TURN_1 = float2(0.75471, 0.65606);   //41 degrees
static const float2 CRATER_TURN_2 = float2(0.27564, 0.96126);   //74 degrees
static const float2 CRATER_TURN_3 = float2(0.55919, 0.82903);   //56 degrees

float2 TurnCrater(float2 p, float2 turn)
{
    return float2(p.x * turn.x - p.y * turn.y, p.x * turn.y + p.y * turn.x);
}

//The crater field: three octaves, so small craters sit inside and on top of larger ones the way the real
//surface is layered. Amplitudes fall roughly with the radius (a crater's depth scales with its size), and
//each octave has its own seed so the three lattices share nothing.
//
//The periods were 90 / 34 / 13 and are 1.43x that, which is CRATER_MAX_RADIUS's own move (0.30 -> 0.21)
//turned around: the crater is a smaller fraction of a bigger cell, so it has room to move and comes out
//the SAME SIZE ON THE GROUND - 27.1, 10.3 and 3.9 world units at the top of each octave, against 27.0,
//10.2 and 3.9 before. What the bigger cells do cost is COUNT, at 49 % per unit area, and the chances below
//buy about half of that back deliberately rather than all of it: the plain photographed for #240 is
//over-populated, a wall-to-wall carpet with no bare mare between anything, and the thinning is most of why
//the fix reads as a plain rather than as the same plain jittered.
float CraterField(float2 p, out float ejecta)
{
    float e0, e1, e2;

    float height = CraterLayer(TurnCrater(p, CRATER_TURN_0) * (1.0 / 129.0), 11.3, 0.86, e0) * 0.58
        + CraterLayer(TurnCrater(p, CRATER_TURN_1) * (1.0 / 49.0), 37.7, 0.82, e1) * 0.29
        + CraterLayer(TurnCrater(p, CRATER_TURN_2) * (1.0 / 18.6), 71.1, 0.70, e2) * 0.13;

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

//The highland belt's rise at a world point (see the HighlandHeight block for why the scene has one at all).
//The ring is shaped into peaks and saddles by `mare` - MareBase's own two octaves, which MoonHeight has
//already paid for - rather than by octaves of its own, and that reuse is the whole reason the belt is
//affordable. MoonHeight is evaluated FOUR times a pixel (the vertex tap plus the normal's three), so a
//private two-octave shape here cost eight extra gradient noises per pixel and measured 2.0 ms of an 18.9 ms
//frame at ssaa 2 on the reference APU - 12% of the frame for a silhouette this field can carry for nothing.
//
//What the reuse buys back is a correlation: where the massif stands high, the mare undulation under the
//craters runs high too. It is invisible and can stay that way - MareBase moves the plain by
//CraterAmplitude * 0.18, about two units, against the belt's forty, and the belt only exists 190 units out
//where the plain's own swells are far below the eye.
//
//The shape is mapped into [HighlandSaddleFloor, 1] rather than allowed to reach zero: a saddle that drops to
//the plain is a notch the eye looks straight THROUGH, onto ground the crest was there to occlude, and at a
//shallow enough angle that is the far plane's cut. At the shipped 0.5 the lowest saddle still stands over a
//degree above the lens, and everything past the crest reads under it.
float HighlandBelt(float2 p, float dist, float mare)
{
    //0.75 stretches MareBase's usual +-0.7 swing across the whole 0..1 span, so the belt actually reaches its
    //full height somewhere; the little that clips at either end flattens the highest massifs into plateaus,
    //which is what a highland IS.
    float shape = saturate(mare * 0.75 + 0.5);

    //The rise and the shape MULTIPLY rather than add: where the shape is low the massif both starts later and
    //ends lower, so the belt's inner edge is as ragged as its crest. An additive shape would ring the plain
    //with one circle at one radius - the lathe-turned bowl rim.
    return HighlandHeight
        * smoothstep(HighlandInnerRadius, HighlandCrestRadius, dist)
        * lerp(HighlandSaddleFloor, 1.0, shape);
}

//The full displaced height at a world point: flat at MoonLevelY inside the clearing around the island,
//rising into cratered ground with distance, climbing again into the highland belt that rings the mare, the
//whole surface falling away with the square of the distance so the horizon closes. Tapped to displace the
//vertex (VS) and, thrice, for the per-pixel normal (PS) - the one field, so the silhouette and the shading
//can never drift apart.
//
//The ejecta output is only read in the pixel shader's own tap; the vertex shader ignores it (the compiler
//strips the dead half there).
float MoonHeight(float2 p, out float ejecta)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    //One MareBase evaluation, two consumers: the plain's gentle undulation under the craters, and the shape
    //of the highland belt out past them (see HighlandBelt for why it borrows this field rather than rolling
    //its own).
    float mare = MareBase(p);
    float field = CraterField(p, ejecta) + mare * 0.18;

    //The curvature is OUTSIDE the ramp: the clearing must stay flat where the island's physics floor is,
    //but the fall of the horizon is the planet's, not the field's, and ramping it would put a crease at
    //the clearing's edge. Inside ClearingRadius the drop is under a hundredth of a unit - nothing.
    //
    //The belt is outside it too, and has its own inner radius well past the clearing's, so the flat ground
    //under the island's physics floor stays exactly as flat as it was.
    return MoonLevelY + CraterAmplitude * ramp * field + HighlandBelt(p, dist, mare)
        - Curvature * dist * dist;
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
    //Turned and thinned with the other three (#240). This is the octave the eye is closest to, so its rows
    //are the longest ones in the frame: it sat unturned on world X and Z at 5 units, which put a hundred
    //cells of it across the near ground in a single glance.
    float smallCraters = CraterLayer(TurnCrater(worldPosition.xz, CRATER_TURN_3) * (1.0 / 7.2), 133.7, 0.64, smallEjecta)
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

    //The grain - CASCADED, not one lattice, which is #208's finding: a single arm's-length grain faded out
    //before its cells reached pixel size (the desert's rule, kept per octave below) left everything past a
    //few metres of the camera as one flat grey, craters and all, and the surface read as untextured. Two
    //octaves, each at its own scale and each faded out before ITS features reach pixel size: crystal-and-dust
    //grit at 2 cm, and mottling at ~2.8 m that the first could never bridge. The broad `broad` mottling
    //above starts at tens of metres and is smooth and unlimited, so between them a resolvable octave exists
    //at every footprint the plain is drawn at - grey on grey, but never one grey at any distance. Both
    //scale with GrainStrength, so a config pinning it to zero still silences the lot.
    //
    //⚠ THE LARGE OCTAVE IS INTERPOLATED AND THE SMALL ONE IS NOT, and that asymmetry is #345 - the
    //checkerboard the owner reported on this surface. There were three octaves and all three were a flat
    //per-cell hash, and A FLAT PER-CELL HASH IS A VISIBLE SQUARE whenever its cell covers more than a few
    //pixels. The band limits beside them cannot help: they fade an octave out as its cells get SMALL, which
    //is the aliasing end, and say nothing about the near end - where a 1.3 m cell is a hundred pixels
    //across and a 5.5 m cell is most of the near ground. That is not grain, it is tiling.
    //
    //⚠ AND THE FIRST DIAGNOSIS WAS WRONG, which is why the method is worth stating: the 5.5 m octave looked
    //like the obvious culprit, so it was silenced - and the checkerboard stayed. Silencing the WHOLE term
    //cleared it, and silencing the 1.3 m one alone left the larger squares standing. BOTH large octaves were
    //doing it. Each octave was killed in turn and photographed; nothing here was reasoned about.
    //
    //Smooth is not a compromise, it is what a metre-scale octave is FOR: mottled ground at that size has no
    //hard edges in it. The 2 cm octave keeps its flat hash because at that size the cell is sub-pixel
    //wherever it is drawn at all, so the hard edge never resolves - it is grit, and grit is what a hash is
    //good at.
    //
    //⚠ THE TWO LARGE OCTAVES BECAME ONE, and that is a COST decision taken with numbers rather than a
    //simplification. GradientNoise2 is four hashes against a flat hash's one, so smoothing both of them
    //measured 49.9 -> 46.5/47.4 FPS on this desktop (pinned camera, ssaa 2, 1600x900, nocap, medians of
    //31 readings): 1.1-1.5 ms, about 6 % of the frame. One octave at 2.8 m - between the two it replaces -
    //measured 49.0, i.e. 0.37 ms, and the two crops are hard to tell apart. This shader's own header records
    //a build that ran at 2 FPS on the reference APU for exactly this reason, so 6 % for a difference nobody
    //can see was not a trade worth making. The fine octave's cascade partner is now this one plus `broad`
    //above, which is smooth and unlimited and carries the far field on its own.
    float grainFine = saturate(1.0 - footprint * 96.0);
    float grainCoarse = saturate(1.0 - footprint * 1.5);
    regolith *= 1.0 + GrainStrength * (
        NoiseHash22(floor(worldPosition.xz * 48.0)).x * grainFine
        + GradientNoise2(worldPosition.xz * 0.36 + 17.0) * GRAIN_SMOOTH_GAIN * 1.2 * grainCoarse);

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
    //everywhere, so the cube lattice's twelve seams never show - Space.fx's rule) and as the FROBENIUS norm
    //of the screen-to-direction Jacobian rather than fwidth's L1 sum, which is isotropic in the camera's
    //bearing (Space.fx has the why - #150).
    float pixelAngle = max(sqrt(dot(ddx(dir), ddx(dir)) + dot(ddy(dir), ddy(dir))), 1e-6);

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
