//Draws a Sahara: rolling sand dunes stretching to a hazed horizon, alive with wind. It is a scene variant
//(NumPad2 cycles city -> sea -> savanna -> desert -> mountain -> ...); the round stone island stays as the
//platform standing in the sand, exactly as it floats on the sea and stands in the grass.
//
//Nothing grows in a Sahara, so what moves is the wind and the sand it carries: fine ripples crawl down the
//dunes and a veil of blown dust drifts across them, both scrolling downwind off the wall clock. The dunes
//themselves are REAL geometry - a camera-centred grid (the shared CreateGridMesh, like the sea, savanna,
//mountains and meadow) displaced in the vertex shader and snapped to its own cell on the CPU each frame, so
//the surface never swims as the camera moves - because a dune with no silhouette against the sky is just a
//lit patch of floor. Flat in a clearing the island stands in (world origin), rising into dunes with distance,
//the same clearing-then-terrain shape its sibling scenes use since the arena became the small round island.
//
//The one thing the terrain does deliberately, and the whole reason this scene was reworked before: the base
//NORMAL is taken PER PIXEL from the height field's own gradient (three cheap DesertHeight taps in the pixel
//shader), NOT interpolated from a per-vertex normal. Interpolating a coarse displaced mesh's per-vertex normal
//is exactly what left a faint facet / Mach-band grid across the old dunes; evaluating the gradient per pixel
//makes the shading smooth regardless of tessellation, so the grid is gone (this is what the savanna's rework
//taught, applied here so the Sahara can come back clean).
//
//THE SAND ITSELF is the part that was rebuilt, and the reason is what the flat version looked like: one
//uniform tan, ripples that read as a regular diagonal crosshatch (which is what a sum of plane-wave sines
//always reads as - see Noise.fxh's opening), and dunes whose tops were the rounded crest of a sine. Real
//wind-rippled sand is none of those. It is braided: sets of wavy lines running across the wind, each set
//wandering, one taking over from another in patches. What is drawn here is that structure, described under
//SandLayer - a technique whose lineage is Shane's "Desert Sand" on Shadertoy, rewritten in this project's
//idiom because the original cannot be used as it stands: its hashes are sine-based (which band differently
//across drivers - Noise.fxh exists to end that) and it has no footprint band-limiting at all, relying on a
//distance fade that would flatten near sand along with far.
//
//Shared between the game and the map editor (both build it for Shader Model 5.0, there being no OPENGL build
//of any shader). It borrows the scene toolkit: the sky is the dome's two-color gradient in linear radiance,
//every procedural feature band-limits against the pixel footprint, and the cloud shadow is the one shared
//field in Clouds.fxh, so the sand darkens under the very cloud the sky shows overhead (the editor never sets
//the cloud uniforms, so there CloudSunlight is a flat 1.0 - full sun, no shadow).

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

//The shared noise library, for the gradient noise the sand pattern and the sand's colour patches are built
//from. Its hashes are the sine-free ones and its gradient noise fades QUINTICALLY, which matters here more
//than anywhere: this field ends up driving a normal, and the cubic fade's discontinuous second derivative
//shows as faint lattice creases the moment it does.
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, and the sun's own radiance (the lit-cloud color the weather uses, tinted by the dome)
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), and the dune-shape dials: the mean
//sand level in the clearing (the island's foot), peak dune height, and the flat clearing that rises into
//dunes over its transition band - the same shape the savanna/mountains/meadow use around the round island.
float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground (the flat clearing otherwise slices
//across the funnel just below its rim, hiding its depth and swallowing the balls falling through). The Testbed
//sets this to the island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

float DesertLevelY;
float DuneAmplitude;
float ClearingRadius;
float ClearingTransition;

//Wall clock (seconds) and the wind, a unit direction in the XZ plane
float DesertTime;
float2 WindDirection;

//Fine wind ripples: peak height and ripple lines per world unit. They do not move - a ripple is carved into
//the dune, and the dial that used to scroll them went with #276; see the ripple domain in DesertPS.
float RippleAmplitude;
float RippleFrequency;

//Blown dust: how strong the veil is, how fast it drifts, and the distance it starts thickening over
float DustStrength;
float DustSpeed;
float DustStart;

//Sand reflectance (linear): the warm ochre of the troughs and the bleached pale of the crests. Sand is never
//one colour, and one colour is exactly what the flat version looked like.
float3 SandColor;
float3 SandColorPale;

//How much of the sky's hemisphere light reaches the flats
float AmbientStrength;

//How hard the sun glints off the sand at a grazing angle. Sand is close to matte but not matte: quartz grains
//are little mirrors, and a dune with the sun low behind it carries a sheen along its crest lines.
float SheenStrength;

//World distance over which the dunes melt into the skyline haze
float HorizonHazeDistance;

//--- The dune field -----------------------------------------------------------------------------------

//Rolling dunes. Two long, low waves along mixed directions give the shape of the field; a THIRD term is
//waveshaped into crest lines, which is the one thing the old smooth four-sine sum could not do. A dune has a
//sharp crest and a long windward face, not the rounded top of a sine: 1 - |x| puts a ridge wherever its wave
//crosses zero, and the smoothstep sharpens that ridge.
//
//How far it may be sharpened is set by the grid, not by taste. The mesh is DESERT_GRID_N over DESERT_EXTENT,
//a cell of ~2.8 world units, and the ridge terms' own wavelengths are ~57 and ~30 units - so the crest's
//effective width stays several cells wide and the geometry can still hold it. Sharpened further the crest
//would fall between vertices, and the per-pixel normal would then shade a ridge the silhouette does not have.
//
//The constant at the end takes the mean back to about zero. It has to: the whole field is multiplied by the
//clearing ramp, so a field with a mean would rise with the ramp and the desert would read as a shallow bowl
//with the island at the bottom of it.
float DuneSum(float2 p)
{
    float base = 0.62 * sin(dot(p, float2(0.090, 0.052)))
        + 0.24 * sin(dot(p, float2(-0.041, 0.101)) + 1.7);

    float ridge = 0.74 * sin(dot(p, float2(0.071, -0.083)) + 3.1)
        + 0.26 * sin(dot(p, float2(0.163, 0.128)) + 5.2);

    float crest = smoothstep(0.08, 1.0, 1.0 - abs(ridge));

    return base + 0.62 * crest - 0.25;
}

//The full displaced sand height at a world point: flat at DesertLevelY inside the clearing around the island,
//rising into dunes with distance. Tapped both to displace the vertex (VS) and, thrice, for the per-pixel
//normal (PS) - the one field, so the two can never drift apart.
float DesertHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    return DesertLevelY + DuneAmplitude * ramp * DuneSum(p);
}

struct DesertVertexInput
{
    float4 Position : POSITION0;
};

struct DesertVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

DesertVertexOutput DesertVS(DesertVertexInput input)
{
    DesertVertexOutput output;

    //Local grid position + the snapped origin gives the world XZ; the dunes are sampled there, so they sit
    //still in the world while the grid slides under them
    float2 worldXZ = input.Position.xz + OriginXZ;
    float3 worldPosition = float3(worldXZ.x, DesertHeight(worldXZ), worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//--- The sand's surface -------------------------------------------------------------------------------

//A 2D rotation. Each set of ripple lines is laid at its own small angle, and it is the SMALL difference
//between the angles that makes the pattern read as sand rather than as corduroy.
float2 Rot2(float2 p, float angle)
{
    float s = sin(angle);
    float c = cos(angle);

    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

//One set of repeat ripple lines, from a phase in cycles. A triangle wave, rounded, with a little of a PEAKY
//variant mixed in so the crests keep an edge instead of reading as a corrugation of pure sines.
//
//`sharpen` fades that peaky part out as the pixels stop resolving it, and it is a separate control from the
//amplitude for a reason worth keeping: the shaping is a NONLINEARITY, so it generates harmonics well above
//the fundamental. Fading the fundamental's amplitude alone would leave those harmonics at full strength to
//alias into a shimmer - the crests would be the last thing to fade and the first thing to crawl.
float RippleLine(float cycles, float sharpen)
{
    //Repeat triangle wave, 0 at the trough and 1 at the crest.
    float t = abs(frac(cycles) - 0.5) * 2.0;

    float rounded = t * t * (3.0 - 2.0 * t);

    //Zero over the whole lower half and rising steeply after it: a crest with a flank rather than a hump.
    float peaky = saturate(t * t * (2.0 * t - 1.0));

    return lerp(rounded, peaky, 0.35 * sharpen);
}

//One layer of the sand pattern, and the whole trick is here.
//
//Two sets of lines are laid at slightly different angles (+10 and -9 degrees - enough that they cross
//visibly, little enough that they still read as one family of ripples running across the wind), each made
//WAVY by perturbing its across-line phase with gradient noise, and then SCREEN-BLENDED with a weight that
//wanders over the ground.
//
//Every one of those three parts is load-bearing. One set alone is corduroy. A straight average of two sets is
//a lattice - the same failure as multiplying sines instead of summing them, which the ball relief records. It
//is blending them with a field that wanders that makes each set own the ground in patches and hand over to
//the other, which is what the braided look of wind-rippled sand actually is. And the SCREEN blend rather than
//a mix, because where the two sets cross their crests should stay bright: averaging darkens exactly the
//intersections the eye reads the weave from.
//
//The perturbing noise runs at roughly the line frequency itself - deliberately fast. A slow perturbation
//bends the whole set like a bent comb; one at the line spacing makes each line wander independently of its
//neighbours, which is what stops the set reading as a printed pattern.
float SandLayer(float2 p, float frequency, float resolvable, float sharpen)
{
    //A third of a period of wander, faded with the resolvable factor along with everything else: at the
    //distance the lines themselves are gone, a wobble on them is pure noise in the normal.
    float wander = 0.32 * resolvable;

    //The wandering field, taken FIRST because it does two jobs: it is the blend weight below, and it also
    //bends the whole family so the ripples are not one straight march everywhere (see SandPattern on why that
    //job is done here rather than by a second layer). Gradient noise where the original used a transcendental
    //pair - one evaluation instead of four, no preferred direction, and the library's quintic fade leaves no
    //lattice crease in a field that is about to drive a normal.
    float w = saturate(GradientNoise2(Rot2(p, 0.785) * frequency * 0.55) * 1.4 + 0.5);

    //A SLOW BEND of the whole family, added to the position and not multiplied into the frequency. That
    //distinction is the trap this cost a rebuild to learn, and it is worth stating plainly: scaling the
    //frequency by a field means the phase carries `coordinate * field`, and the coordinate here is a WORLD
    //position running to +-500. The phase gradient then goes as the coordinate times the field's gradient -
    //hundreds of cycles per unit - while `resolvable` above is still computed from the nominal frequency and
    //has no idea. The near sand came out as a violent rainbow moire across the entire frame. Adding to the
    //position instead is bounded by construction: it shifts the lines where they are without ever changing
    //how fast they repeat, so the band-limit stays true.
    float2 bend = p + w * 0.6;

    float2 a = Rot2(bend, 0.175);
    float line1 = RippleLine(a.y * frequency + GradientNoise2(a * frequency * 1.40) * wander, sharpen);

    float2 b = Rot2(bend, -0.157);
    float line2 = RippleLine(b.y * frequency + GradientNoise2(b * frequency * 0.95) * wander + 0.5, sharpen);

    return 1.0 - (1.0 - line1 * (1.0 - w)) * (1.0 - line2 * w);
}

//The sand pattern. Returns 0..1 and it is a HEIGHT in spirit - the normal comes off it through
//PerturbNormalFromHeight, and the colour darkens in its troughs, which is what makes the ripples read even on
//the faces the sun is not raking.
//
//`footprint` is the screen pixel's size in world units, the same yardstick every other procedural feature in
//this project band-limits against. The resolvable factor reaches zero exactly at Nyquist for the line
//spacing (a period of 1/frequency needs a footprint under 1/(2*frequency)), so the ripples fade into smooth
//sand as the pixels lose them rather than aliasing into a crawl. The sharpening fades QUADRATICALLY faster,
//for the reason RippleLine gives.
//
//ONE layer, and that is a measured decision rather than a simplification. The original stacks a second layer
//turned 15 degrees and a quarter finer, mixed into the first by a further noise field - four more gradient
//noise evaluations per pixel, on a shader that already covers the whole frame. Measured on this scene at
//ssaa 2: the two-layer version cost 47.6 -> 30.5 FPS against the flat sand it replaced, i.e. +56 % frame
//time, which is not a backdrop's share of the frame. What the second layer bought was large-scale VARIETY in
//the ripple field, and most of that can be had for nothing instead: SandLayer already computes a wandering
//field for its blend, so the same field is fed back as a slow BEND of the line family's position. The ripples
//then wander across the dunes in broad sweeps rather than marching straight, and it costs one multiply and
//one add. (It must be a bend of the position and not of the frequency - see SandLayer for what that mistake
//looked like on screen.)
float SandPattern(float2 p, float footprint)
{
    float f = RippleFrequency;

    float resolvable = saturate(1.0 - 2.0 * f * footprint);
    float sharpen = resolvable * resolvable;

    //No early exit once the pattern is unresolvable, tempting as it is. This value feeds ddx/ddy through
    //PerturbNormalFromHeight, and `resolvable` varies per pixel - so a branch on it would diverge inside a
    //quad exactly along the line where the ripples fade out, and the derivatives of a quad whose lanes took
    //different paths are undefined. The lerp below does the same job for a handful of ALU.
    float pattern = SandLayer(p, f, resolvable, sharpen);

    //Towards flat sand (0.5) rather than towards zero, so fading the pattern out does not also darken the
    //distance through the trough shading below.
    return lerp(0.5, pattern, resolvable);
}

float4 DesertPS(DesertVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The base dune normal, taken PER PIXEL from the height field's gradient (three cheap taps) rather than
    //interpolated from a per-vertex normal - this is what removes the coarse mesh's facet/grid pattern.
    float e = 1.5;
    float h = DesertHeight(worldPosition.xz);
    float hx = DesertHeight(worldPosition.xz + float2(e, 0.0));
    float hz = DesertHeight(worldPosition.xz + float2(0.0, e));

    //The dune's own slope, which the three taps above have already paid for
    float2 duneSlope = float2(hx - h, hz - h) / e;
    float3 duneNormal = normalize(float3(-duneSlope.x, 1.0, -duneSlope.y));

    //The ripple field's domain: WARPED BY THE DUNE'S OWN SLOPE so the lines bend as they run over a crest and
    //pool in the hollows. That warp is the difference between ripples that belong to the dune and ripples
    //that are wallpaper laid over it, and it is free here - the slope is the same one the normal above was
    //built from.
    //
    //⚠ AND IT IS STATIC (#276). This used to add `WindDirection * DesertTime * RippleSpeed` at 1.4 world
    //units a second, and that is the fault the owner reported as the sand "just looking wrong": a sand
    //ripple is a ridge CARVED INTO the dune, and scrolling the field slides every ridge across the surface
    //it is cut into - the exact wallpaper the paragraph above sets out to avoid, added two lines under it.
    //Measured on two frames 0.6 s apart, the whole sand surface had changed in essentially every pixel.
    //Nor is it a matter of a slower speed: real ripples migrate centimetres in an hour, so at any speed the
    //eye can see, the motion is wrong. What moves in a desert is the AIR - the dust veil further down, which
    //has its own DustSpeed and keeps it. `RippleSpeed` is gone rather than zeroed, here and in
    //DesertSceneConfig: a dial whose only correct value is zero is a trap for whoever finds it next.
    float2 ripplePos = worldPosition.xz + duneSlope * 1.6;

    float pattern = SandPattern(ripplePos, footprint);

    //Fine wind ripples tilt the dune normal; they carry the whole sense of a surface crawling in the wind
    float3 normal = PerturbNormalFromHeight(duneNormal, worldPosition, pattern * RippleAmplitude);

    //--- The sand's colour ---------------------------------------------------------------------------
    //Sand is not one colour, and one colour is what this read as before: a flat tan the eye takes for a lit
    //floor. A broad field mixes the bleached pale of the crests against the warm ochre of the troughs, and
    //the same field - offset, so the two do not move together - varies the brightness of whatever that gave.
    //Patches with no tone read as stains and tone with no patches reads as vignetting; it takes both.
    //
    //ONE evaluation for both, and one octave rather than an fBm. Three of them measured as a real share of
    //this shader's cost for a difference nobody can point at: the field is only ever asked for a broad
    //mottle, and the ripples and the grain already carry every frequency above it. It is far below the pixel
    //frequency at any distance the sand is drawn from, so it needs no band-limiting either.
    float broad = GradientNoise2(worldPosition.xz * 0.055);

    float3 sand = lerp(SandColor, SandColorPale, saturate(broad * 1.6 + 0.5));

    sand *= lerp(0.80, 1.20, saturate(broad * -2.1 + 0.5));

    //Shading in the ripple troughs, independent of the sun. It is the cheapest kind of ambient occlusion and
    //it is what makes the ripples read on the faces the sun is NOT raking - with the normal tilt alone, a
    //dune's shadowed flank goes back to being a flat patch of colour.
    sand *= 0.58 + 0.52 * pattern;

    //And the grains themselves, close up: one hash per pixel over a fine world lattice, gone within a few
    //units. Sand at arm's length is not smooth, and this is the only cue at that distance that says so.
    //
    //A hard-edged per-cell value is its own aliasing source, so the fade has to be finished BEFORE the cells
    //reach pixel size, not at it: the lattice is 60 cells per unit and this reaches zero at a footprint of
    //1/120, i.e. while a cell is still two pixels across. Faded at the cell size instead, the grain would
    //spend its last few metres as a crawling speckle.
    float grainFade = saturate(1.0 - footprint * 120.0);
    sand *= 1.0 + NoiseHash22(floor(worldPosition.xz * 60.0)).x * 0.16 * grainFade;

    //--- Lighting ------------------------------------------------------------------------------------
    //Sand is a near-matte diffuse surface: the sun rakes the dunes (lit windward faces, shadowed lee ones)
    //and the sky fills the rest. The cloud shadow dims the sun exactly as it does for the whole scene.
    float sunlight = CloudSunlight(worldPosition, SunDirection);
    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light: up-facing sand takes the zenith, slopes towards the skyline take the horizon
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = sand * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //The sheen. Quartz grains are little mirrors, so sand is not quite matte: with the sun low, its crest
    //lines catch a glint. A wide Blinn lobe rather than a tight one - the reflection is off a million grains
    //facing every way, so it is a broad sheen and not a highlight - and gated on ndotl, or the lee faces
    //would glint in light that never reaches them.
    float3 towardsEye = normalize(CameraPosition - worldPosition);
    float3 halfway = normalize(SunDirection + towardsEye);
    float sheen = pow(saturate(dot(normal, halfway)), 12.0);

    color += SunColor * sheen * SheenStrength * ndotl * sunlight;

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
