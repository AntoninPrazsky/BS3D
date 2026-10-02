//Draws the open sea as a rough, storm-driven ocean: a camera-centred grid displaced by a sum of Gerstner
//(trochoidal) waves - real geometry with sharp crests and rounded troughs that break the horizon and
//occlude each other, not the flat mirror the first version was. On top of the wave geometry the pixel
//shader adds fine wind chop, a Fresnel sky reflection, a sun glint, subsurface scattering that lights the
//crests from behind, and whitecap foam where the waves fold. It is the second scene variant (NumPad2
//cycles the seven scenes); the round stone island stays as the platform floating on it, and the drain
//funnel bored through it holds a standing pool of the same water — dead calm, meeting the glass in a
//capillary rim — where the cone crosses the mean level (#132; see FunnelPoolRadius and the calm ramp).
//
//It shares the whole scene toolkit with Desert.fx/Mountain.fx/Meadow.fx: the grid is recentred on the
//camera each frame and snapped to a cell on the CPU (OriginXZ) so the surface never swims; the dome is a
//two-color vertical gradient sampled in closed form (the reflection and the ambient); features band-limit
//against the pixel footprint the way the ground relief does; and the cloud shadow comes from the one
//shared field in Clouds.fxh, so the water darkens under the very cloud the sky shows overhead. Built for
//Shader Model 5.0 and drawn through SceneRenderer in both executables (the map editor draws it too now).

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

#include "FarField.fxh"

//Towards the sun, normalized (the same direction the scene is shadowed and the clouds are lit along), and
//the sun's own radiance (the lit-cloud color the weather uses, tinted by the dome) for the glint and SSS.
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline. The water
//mirrors this, so it takes on the mood of whichever of the eighteen skies is up, exactly as the city does.
float3 ZenithColor;
float3 HorizonColor;

//Wall clock driving the waves, in seconds (shared with the balls' pulse and the clouds, so the water keeps
//moving while the simulation is paused).
float SeaTime;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell) and the mean water level
float2 OriginXZ;
float SeaLevelY;

//The grid's vertex spacing in world units (TerrainPass sets it): what decides which waves the vertices can carry
float GridCell;

//Radius of the island's footprint cut out of the surface around the world origin, so the sea does not run
//through the drain funnel's open throat. 0 keeps it all (the map editor draws no island). See IslandHoleRadius
//in the terrain shaders, which cut the same footprint for the same reason.
float IslandHoleRadius;

//Radius of the pool standing INSIDE the drain (#132): where the funnel's glass cone crosses the sea's mean
//level, buried a hair into the glass (see DrawSea, which derives it from ArenaIsland's own figures). The cut
//above is an annulus now, not a disc: water inside this radius is kept — calmed to a standstill by the calm
//ramp below — so the drain holds standing water instead of going bone dry, and only the ring hidden inside
//the island's stone is clipped. Ignored wherever IslandHoleRadius is 0 (the map editor), where nothing is cut.
float FunnelPoolRadius;

//Deep body color of the water and the paler shade its up-facing faces take, both linear and both treated
//as reflectances - they are multiplied by the sky's own light below, so night water goes dark.
float3 WaterColorDeep;
float3 WaterColorShallow;

//How much of the body colour is WaterColorShallow regardless of which way the surface faces — 0 for water
//with nothing under it (the open sea, and the sea's default), up towards 1 for a lagoon, whose bed is a few
//units down across the whole basin. See the body-colour block below for what it replaces and why.
float ShallowBias;

//Overall wave height (world units, the dominant swell's amplitude), how sharp the crests pinch (0..1, the
//Gerstner steepness) and a multiplier on the dispersion-derived wave speed.
float WaveAmplitude;
float WaveSteepness;
float WaveSpeed;

//The waves are faded to flat between these camera distances, so the far sea settles into a clean hazed
//horizon line rather than a jagged fringe of crests against the sky.
float WaveFadeStart;
float WaveFadeEnd;

//Fine wind chop layered on top of the Gerstner geometry in the pixel shader: peak height, ripples per
//world unit and scroll speed, plus the wind it crawls along (a unit direction in the XZ plane).
float ChopAmplitude;
float ChopFrequency;
float ChopSpeed;
float2 WindDirection;

//How sharp and how bright the sun's reflection sparkles off the crests
float SunGlintStrength;
float SunGlintPower;

//Whitecap foam: how far the Gerstner Jacobian must fold before foam appears (nearer 1 = more foam), how
//strong that fold foam is, where on a crest the height-driven foam starts (0..1) and how strong it is, and
//the foam's own near-white color.
float FoamJacobianThreshold;
float FoamStrength;
float FoamCrestStart;
float FoamCrestStrength;
float3 FoamColor;

//Subsurface scattering: how strongly a crest glows when the sun is behind it and the eye looks into it,
//and the warm green-blue that light takes coming through the water.
float SssStrength;
float3 SssColor;

//World distance over which the sea melts into the horizon haze (the sky's own skyline color), so the
//finite grid has no visible edge and no hard seam against the dome.
float HorizonHazeDistance;

static const float TWO_PI = 6.28318530718;
static const float GRAVITY = 9.81;

//The swell: a SPECTRUM of twenty waves (#674), a dominant long swell and nineteen shorter ones fanned about the wind.
//Directions need not be unit here - they are normalized at use. The amplitudes are weights on WaveAmplitude, and
//WAVE_STEEP_WEIGHT rides on WaveSteepness. Wavelengths are kept short relative to the scene (a ball is ~1 unit) so
//several crests fall inside the visible water and it reads as waves, not one flat tilt.
//
//It was six waves, picked by hand, and from above the sea read as a lattice twice over. The six summed into an
//interference pattern that came back almost exactly: the slope field correlated 0.971 with itself 68 units away, a
//figure any six plane waves land near (random sets: 0.89-0.99 inside 120 units), because six lines in a spectrum ARE a
//quasi-crystal. And the two shortest (8.5 and 5.5 units) were evaluated only at the vertices, 4.2 units apart, under
//the grid's Nyquist limit of two cells: sampled that coarsely they folded into a honeycomb on the grid's own pitch,
//which a capture with the chop turned off showed plainly.
//
//How these twenty were drawn (numpy's default_rng(1), once - the numbers are the result, not dials): the dominant swell
//is the old one exactly, 52 units along (1, 0.35) at amplitude 1, so the foam streaks keep their fabric; the other
//nineteen carry the old five's energy (their squared amplitudes sum to the same 0.952), one per step of a geometric
//ladder from 46 down to 9.6 units with the length jittered inside its step, amplitude in proportion to length^0.8 as
//the old set had it, at random phases, travelling within 75 degrees of the wind with a spread that widens from 22
//degrees for the long waves to 55 for the short. The slope field's strongest self-likeness inside 250 units falls to
//0.614, and it lies 242 units away. WAVE_STEEP_WEIGHT holds the summed pinch (q k a over the waves) at the old set's,
//so the crests sharpen as far as they did. None is shorter than 9.6 units, a little over two cells, and a short one's
//displacement is faded where the grid cannot carry it (GridCarries), and the normals of all but the longest are the
//pixel's own (SwellNormal).
static const int WAVE_COUNT = 20;
static const float2 WAVE_DIR[WAVE_COUNT] = {
    float2(1.0000, 0.3500), float2(0.9727, 0.2320), float2(0.9427, 0.3335), float2(0.9787, 0.2055), float2(0.5790, 0.8153),
    float2(0.6477, 0.7619), float2(0.5634, -0.8262), float2(0.7371, -0.6758), float2(0.9738, 0.2273), float2(0.9978, 0.0664),
    float2(0.8855, 0.4647), float2(0.8834, 0.4685), float2(-0.0748, 0.9972), float2(0.8691, -0.4946), float2(0.9992, 0.0404),
    float2(-0.0748, 0.9972), float2(0.6296, 0.7769), float2(0.5976, 0.8018), float2(0.9909, -0.1345), float2(0.5634, -0.8262) };
static const float WAVE_LEN[WAVE_COUNT] = {
    52.00, 44.18, 42.19, 36.35, 35.77, 31.25, 29.04, 27.65, 24.60, 22.91,
    20.21, 19.76, 17.88, 16.18, 15.47, 13.69, 12.76, 11.45, 10.78, 9.76 };
static const float WAVE_AMP[WAVE_COUNT] = {
    1.0000, 0.3573, 0.3443, 0.3056, 0.3017, 0.2708, 0.2554, 0.2456, 0.2236, 0.2113,
    0.1911, 0.1877, 0.1732, 0.1600, 0.1543, 0.1399, 0.1323, 0.1213, 0.1156, 0.1068 };
static const float WAVE_PHASE[WAVE_COUNT] = {
    0.000, 0.392, 4.030, 5.357, 3.726, 1.634, 5.277, 3.201, 3.210, 4.731,
    0.929, 5.150, 4.293, 4.945, 1.204, 5.041, 1.202, 0.512, 5.374, 5.412 };
static const float WAVE_STEEP_WEIGHT = 0.70;

//The first VERTEX_NORMAL_WAVES (52 down to 27.65 units, six and a half cells and more) are slow enough across the grid
//for their normal to be interpolated from the vertices; the shorter rest are the pixel's own (SwellNormal)
static const int VERTEX_NORMAL_WAVES = 8;

//The old six's sum of amplitudes over the root of their summed squares (2.92 / 1.397): see the crest in OceanSurface
static const float CREST_NORM = 2.09;

//How much of a wave the vertex grid can carry: none below two cells a wavelength (the Nyquist limit - sampled coarser
//it folds into a pattern on the grid's own pitch, the honeycomb above), all of it from four cells up. A GridCell of 0
//(nothing set it) carries everything.
float GridCarries(float wavelength)
{
    return GridCell > 0.0 ? saturate((wavelength / GridCell - 2.0) * 0.5) : 1.0;
}

//The foam streaks' fabric (#128): lanes per world unit, how many times longer a lane runs along a crest
//than across it, and the crest line itself - crests run PERPENDICULAR to their wave's travel, so this is
//the normalized perpendicular of WAVE_DIR[0], the dominant swell every other wave is fanned around.
//Static rather than config dials: they are what foam IS here, not a mood - the mood (how much, where it
//may start) stays on the config as FoamStrength/FoamCrestStart/FoamCrestStrength, exactly as before.
static const float FOAM_STREAK_FREQUENCY = 0.55;
static const float FOAM_STREAK_STRETCH = 5.0;
static const float2 FOAM_STREAK_ALONG = float2(-0.3303, 0.9438);

//The island's shelter (#132). The swell dies over CALM_BAND world units approaching IslandHoleRadius from
//the open sea, so the pool in the drain is dead flat geometry and the last visible water at the island's
//foot laps rather than breaks. The band is about one grid cell (SEA_EXTENT / SEA_GRID_N ≈ 4.2), deliberately:
//every vertex a pool-edge triangle can touch is then fully calm, so the pool's clipped rim cannot breathe
//with the swell. Inside the pool a POOL_CHOP fraction of the wind chop survives as a fine capillary ripple,
//and over the last MENISCUS_BAND before the glass the normal is tilted up toward the wall — the capillary
//climb that reads as water meeting glass instead of a razor-cut disc.
//How far the distance haze DESATURATES its target (#503). Water at the horizon is a mirror at grazing
//incidence, so what it shows is the whole sky standing over it - the cloud deck included - and not the
//clear-sky horizon band that HorizonColor is. Hazing straight to that band made the far sea GREENER and
//more saturated than the near water, which is backwards: measured under dome 13 from a play-height lens,
//the sky just over the horizon came out (167, 176, 198) and the water just under it (60, 137, 160), a
//107-level drop in red across one row, with the green channel RISING with distance. Every reference
//photograph's far water is less saturated than its near water, never more. Pulling the target towards its
//own luminance is the cheap stand-in for the sky the pass cannot sample; it keeps the target's brightness,
//which is the half that was right. Static, not a dial: it is what water at a grazing angle IS, like the
//foam streak fabric above, and both water scenes want it.
static const float HAZE_DESATURATION = 0.55;
static const float3 LUMA = float3(0.2126, 0.7152, 0.0722);

//What a white cap gathers, against the (Zenith + Horizon)/2 the body is lit by (#503). Foam is a matte
//white surface collecting light from the WHOLE hemisphere, where that average is one direction's worth, so
//foam shaded by it alone came out a dim blue-grey smudge barely a third brighter than the water it rides -
//and the references' foam is the brightest thing in every frame by a wide margin, bright white against
//dark water. The gain stays under the glare threshold (0.55 on luminance) at the sea's ambient: the foam is
//meant to read by CONTRAST against dark water, not by blooming.
static const float FOAM_AMBIENT_GAIN = 1.9;

static const float CALM_BAND = 4.0;
static const float POOL_CHOP = 0.18;
static const float MENISCUS_BAND = 0.9;
static const float MENISCUS_TILT = 0.35;

struct SeaVertexInput
{
    float4 Position : POSITION0;
};

struct SeaVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    //xy = the rest position the swell is evaluated at, z = the swell's fade there (the horizon and the island's calm)
    float3 Rest : TEXCOORD1;
    //The long waves' normal accumulators (SwellNormal finishes them with the short ones)
    float3 Slope : TEXCOORD3;
    //x = whitecap fold factor (0..1), y = crest height normalized (0..1)
    float2 Foam : TEXCOORD2;
};

//Sum of Gerstner waves at the rest position p0, as far as the vertex grid can carry each (GridCarries). Returns the
//world-space displacement (horizontal AND vertical - the horizontal pinch is what sharpens the crests over a plain
//height field), the horizontal Jacobian fold (drops below 1 as crests pinch, negative where the surface overhangs -
//the whitecap generator) and the crest height normalized to 0..1. ampScale fades the whole thing to flat towards the
//horizon. Of the normal only the long waves' part is here (`slope`, see VERTEX_NORMAL_WAVES); the short ones are the
//pixel's (SwellNormal), since interpolated between vertices 4.2 units apart they printed the grid into the shading.
void OceanSurface(float2 p0, float ampScale, out float3 disp, out float3 slope, out float fold, out float crest)
{
    disp = float3(0.0, 0.0, 0.0);

    //The long waves' normal accumulators (x, z, y-pinch), for SwellNormal to finish: see VERTEX_NORMAL_WAVES
    slope = float3(0.0, 0.0, 0.0);

    //Jacobian accumulators for the horizontal displacement map (x,z) -> (x+dx, z+dz)
    float jxx = 0.0, jzz = 0.0, jxz = 0.0;

    float sumSquares = 0.0;

    [unroll]
    for (int i = 0; i < WAVE_COUNT; i++)
    {
        float2 d = normalize(WAVE_DIR[i]);
        float k = TWO_PI / WAVE_LEN[i];
        float a = WaveAmplitude * WAVE_AMP[i] * ampScale * GridCarries(WAVE_LEN[i]);
        float w = sqrt(GRAVITY * k) * WaveSpeed;   //deep-water dispersion: long swells roll slower than chop
        float q = WaveSteepness * WAVE_STEEP_WEIGHT;

        float phase = k * dot(d, p0) + w * SeaTime + WAVE_PHASE[i];
        float c = cos(phase);
        float s = sin(phase);

        float qa = q * a;
        disp.x += qa * d.x * c;
        disp.z += qa * d.y * c;
        disp.y += a * s;

        float wa = k * a;

        if (i < VERTEX_NORMAL_WAVES)
        {
            float full = WaveAmplitude * WAVE_AMP[i] * ampScale * k;
            slope += float3(d.x * full * c, d.y * full * c, q * full * s);
        }

        //d(disp.x)/dx0 = -q*a*k*d.x*d.x*sin(phase); the map derivative subtracts that from the identity
        jxx += q * wa * d.x * d.x * s;
        jzz += q * wa * d.y * d.y * s;
        jxz += q * wa * d.x * d.y * s;

        sumSquares += a * a;
    }

    float jacobian = (1.0 - jxx) * (1.0 - jzz) - jxz * jxz;
    fold = saturate((FoamJacobianThreshold - jacobian) / max(FoamJacobianThreshold, 1e-3));

    //Normalised by the spread of the height and not by the sum of the amplitudes (#674): the sum grows with the number of
    //waves while the height's spread does not, so with twenty the crest parked near 0.5 and the whitecaps all but went.
    //CREST_NORM makes the six-wave set's sum of amplitudes exactly, so the crest is distributed as it was (emulated:
    //13.1 % above 0.7 before and after).
    crest = saturate(disp.y / max(CREST_NORM * sqrt(sumSquares), 1e-3) * 0.5 + 0.5);
}

//The swell's analytic normal at the rest position p0 (GPU Gems 1 form, WA = k*A): the long waves' accumulators as the
//vertices interpolated them (vertexSlope), and the short waves summed here at their full amplitude, each faded against
//the pixel's footprint the way the chop is - a wave under two pixels a wavelength fades to flat rather than shimmer.
//The rest position reaches the pixel interpolated across its triangle, which is exact, since the grid's rest
//positions are affine in it. All twenty here looked the same and cost 0.23 ms more on the sea at 3840 x 1600, ssaa 2.
float3 SwellNormal(float2 p0, float ampScale, float footprint, float3 vertexSlope)
{
    float nx = vertexSlope.x, nz = vertexSlope.y, nySub = vertexSlope.z;

    [unroll]
    for (int i = VERTEX_NORMAL_WAVES; i < WAVE_COUNT; i++)
    {
        float2 d = normalize(WAVE_DIR[i]);
        float k = TWO_PI / WAVE_LEN[i];
        float a = WaveAmplitude * WAVE_AMP[i] * ampScale * saturate(1.0 - footprint * k / 3.14159265);
        float w = sqrt(GRAVITY * k) * WaveSpeed;
        float q = WaveSteepness * WAVE_STEEP_WEIGHT;

        float s, c;
        sincos(k * dot(d, p0) + w * SeaTime + WAVE_PHASE[i], s, c);

        float wa = k * a;
        nx += d.x * wa * c;
        nz += d.y * wa * c;
        nySub += q * wa * s;
    }

    return normalize(float3(-nx, 1.0 - nySub, -nz));
}

SeaVertexOutput SeaVS(SeaVertexInput input)
{
    SeaVertexOutput output;

    //Local grid position + the snapped origin gives the rest world XZ; the waves are sampled there, so they
    //sit still in the world while the grid slides under them
    float2 restXZ = input.Position.xz + OriginXZ;

    //Fade the waves to flat between WaveFadeStart and WaveFadeEnd so the horizon reads as one hazed line
    float restDist = distance(CameraPosition.xz, restXZ);
    float ampScale = saturate(1.0 - (restDist - WaveFadeStart) / max(WaveFadeEnd - WaveFadeStart, 1.0));

    //The island's shelter (#132): the swell dies completely under the stone, so the pool standing in the
    //drain is exactly the flat rest grid at SeaLevelY. Keyed on the REST position — displacement is zero
    //wherever it matters, so the pixel shader sees the same radius. IslandHoleRadius 0 (the map editor)
    //makes this saturate to 0 and the open sea is untouched.
    float calm = saturate((IslandHoleRadius - length(restXZ)) / CALM_BAND);

    float3 disp, slope;
    float fold, crest;
    OceanSurface(restXZ, ampScale * (1.0 - calm), disp, slope, fold, crest);

    float3 worldPosition = float3(restXZ.x + disp.x, SeaLevelY + disp.y, restXZ.y + disp.z);

    output.WorldPosition = worldPosition;
    output.Rest = float3(restXZ, ampScale * (1.0 - calm));
    output.Slope = slope;
    output.Foam = float2(fold, crest);
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//One fine chop octave: gradient noise stretched CHOP_STRETCH times along its crests (#674), band-limited against the
//pixel footprint like the ground relief, so the chop fades into smooth water towards the horizon rather than aliasing
//into a shimmer. Accumulated as a height for PerturbNormalFromHeight to tilt the swell's normal by.
//
//It was a sine, four of them crossing at wide angles, and two crossing sines are a lattice of diamonds by definition;
//four of them came back exactly every 50.6 units (the field correlated 1.000 with its own copy there, any four sines
//land within 0.006 of that inside 60 units). Noise has no line spectrum to repeat. A gradient-noise cell holds about
//one swing, so the domain is the sine's frequency over pi, and CHOP_NOISE_GAIN gives it the sine's slope across the
//crests (measured over 400 000 samples of this noise: 0.501 of slope a unit, so the gain is pi / sqrt(2) / 0.501).
static const float CHOP_STRETCH = 3.0;
static const float CHOP_NOISE_GAIN = 4.43;

float ChopRipple(float2 xz, float2 dir, float frequency, float footprint, float2 seed)
{
    float resolvable = saturate(1.0 - footprint * frequency / 3.14159265);
    float2 domain = float2(dot(xz, dir), dot(xz, float2(-dir.y, dir.x)) / CHOP_STRETCH) * (frequency / 3.14159265);
    return GradientNoise2(domain + seed) * CHOP_NOISE_GAIN * resolvable;
}

//The fine wind-chop height field: a few octaves crossing the wind, scrolling downwind so the surface crawls
float ChopHeight(float2 xz, float footprint)
{
    float2 p = xz + WindDirection * SeaTime * ChopSpeed;
    float f = ChopFrequency;

    float h = 0.5 * ChopRipple(p, normalize(float2(0.9, 0.4)), f, footprint, float2(0.0, 0.0))
        + 0.28 * ChopRipple(p, normalize(float2(0.6, -0.8)), f * 1.9, footprint, float2(37.1, 11.9))
        + 0.15 * ChopRipple(p, normalize(float2(-0.5, 0.85)), f * 3.4, footprint, float2(74.2, 23.8))
        + 0.09 * ChopRipple(p, normalize(float2(0.2, -0.98)), f * 5.7, footprint, float2(111.3, 35.7));

    return h * ChopAmplitude;
}

float4 SeaPS(SeaVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the ring of the island's footprint out of the surface, keeping BOTH sides of it: the open sea past
    //IslandHoleRadius and the pool standing inside the drain (#132) — max() keeps a pixel that survives on
    //either count. Only the annulus between them, hidden inside the island's stone, is discarded. 0 in the
    //map editor keeps it all: r - 0 is never negative, whatever the pool radius says.
    float r = length(worldPosition.xz);
    clip(max(r - IslandHoleRadius, FunnelPoolRadius - r));
    FarRingClip(worldPosition.xz);

    //How deep into the island's shelter this pixel is: 1 across the whole pool, 0 on the open sea and in the
    //map editor. The vertex shader keyed the same ramp on the rest position; the two agree wherever it
    //matters because the calm water is exactly where the displacement is zero.
    float calm = saturate((IslandHoleRadius - r) / CALM_BAND);

    float3 toEye = CameraPosition - worldPosition;
    float dist = length(toEye);
    float3 viewDir = toEye / dist;

    float footprint = length(fwidth(worldPosition.xz));

    //Fine chop tilts the Gerstner normal; it carries the close-up sparkle and breaks the big waves into a
    //surface. Faded with the same distance ramp as the geometry so the far water is smooth. In the pool it
    //is damped to a POOL_CHOP fraction — the sheltered water keeps a fine capillary ripple, nothing more.
    float chopFade = saturate(1.0 - (dist - WaveFadeStart) / max(WaveFadeEnd - WaveFadeStart, 1.0));
    float chop = ChopHeight(worldPosition.xz, footprint) * chopFade * lerp(1.0, POOL_CHOP, calm);
    float3 normal = PerturbNormalFromHeight(SwellNormal(input.Rest.xy, input.Rest.z, footprint, input.Slope), worldPosition, chop);

    //Capillary climb at the glass (#132): over the last MENISCUS_BAND before the pool's edge the surface
    //reads as curling up the wall — the normal tilts away from the outward radial, so the rim catches the
    //sky at a different angle than the flat pool and the water meets the glass in a soft bright ring rather
    //than a razor-cut circle. Scaled by calm, so the open sea (and the map editor) never sees it.
    float meniscus = saturate(1.0 - (FunnelPoolRadius - r) / MENISCUS_BAND) * calm;
    float2 outward = worldPosition.xz / max(r, 1e-3);
    normal = normalize(normal - float3(outward.x, 0.0, outward.y) * (meniscus * meniscus * MENISCUS_TILT));

    //How much sun reaches this patch through the clouds - the very field the whole scene is shadowed by
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //Sky reflection. The dome is a vertical gradient, so the reflected ray's height picks between horizon
    //and zenith in closed form - the same trick InstancedModel.fx's SkyRadiance uses. A grazing view mirrors
    //the low sky near the horizon; a steep look down shows more zenith. A cloud overhead greys it a little.
    float3 reflected = reflect(-viewDir, normal);
    float3 sky = lerp(HorizonColor, ZenithColor, saturate(reflected.y * 0.5 + 0.5));
    float3 reflection = sky * lerp(0.65, 1.0, sunlight);

    //Fresnel: at a grazing angle the water is a mirror, straight down it shows mostly its own body. A small
    //floor above water's ~2% head-on reflectance keeps a little sky in the surface even looking straight down.
    float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(normal, viewDir)), 5.0);

    //Body color: deep water lit by the sky above it (so a night sea goes dark), the up-facing faces a touch
    //paler. Water has almost no light of its own; what you see into it is skylight scattered back out. The
    //pool is biased towards the deep colour: its normal points straight up, which would pick the palest mix
    //of all, and a still column of water standing in a drain reads dark, not pale.
    float3 ambient = (ZenithColor + HorizonColor) * 0.5;

    //How much of the body is the SHALLOW colour. The open sea's rule is the first term: the up-facing faces
    //of the swell show a paler column and everything else shows the deep, and a calm patch (the drain's
    //standing pool) is biased darker still. That rule is written for water with nothing under it — and it
    //is what left the tropical LAGOON reading grey (#268, measured at (140, 146, 133), blue below red,
    //against a WaterColorShallow that is honestly turquoise). A lagoon is shallow EVERYWHERE: its bed is a
    //few units under the surface across the whole basin, so its colour is the shallow one wherever you look
    //at it, not only on the faces that happen to tilt up. ShallowBias lifts the floor of the mix.
    //
    //It is 0 for the open sea and lerp(x, 1, 0) is bit-exactly x, so that scene's water is UNCHANGED — which
    //is what makes it safe to put this in the shader both water scenes draw through.
    //The open sea's own term is modulated by the CREST since #503. Every reference photograph of open water
    //has dark troughs and paler flanks and crests - a crest is a thin column of water with light coming
    //through it, a trough looks down into the deep - where this shader keyed the mix on `normal.y` alone.
    //That is near 1 over almost the whole surface (only the steep faces tilt), so the mix barely varied and
    //the water came out one mid value from trough to crest. The crest height is already interpolated per
    //pixel for the foam gate, so this costs a lerp.
    //
    //⚠ The mean of the new factor is deliberately the old constant: lerp(0.12, 0.85, 0.5) = 0.485 against
    //0.5, so the LAGOON - which draws through this shader with ShallowBias 0.78 and therefore keeps 22 % of
    //this term - moves by under a hundredth of its mix from THIS line. The range is what changed, not the
    //level. (The lagoon does move, by about 13 levels on its distant water band, and it is the desaturated
    //haze above that moves it - measured, not assumed: (161, 167, 162) before against (148, 158, 152) after,
    //over 1800 samples of the water either side of the palms. That one is deliberate and shared: water at a
    //grazing angle mirrors the whole sky in a lagoon exactly as it does at sea.)
    float openMix = saturate(normal.y) * lerp(0.12, 0.85, input.Foam.y) * (1.0 - 0.8 * calm);
    float shallowMix = lerp(openMix, 1.0, ShallowBias);

    float3 body = lerp(WaterColorDeep, WaterColorShallow, shallowMix) * ambient + ZenithColor * 0.05;

    float3 color = lerp(body, reflection, fresnel);

    //Subsurface scattering: a crest glows when the sun is behind it and the eye looks into the water. The
    //classic cheap term - looking towards the sun, strongest on the raised faces of the waves, snuffed by cloud.
    //Widened and re-aimed at the CREST since #503: the translucent green edge is the signature of a wave in
    //every backlit reference, and the fourth power put it only in the few degrees either side of looking
    //straight into the sun - where the glint path owns the frame anyway, so it was paying for a term almost
    //nothing ever saw. The third power spreads it across the sunward quarter of the view, and the tighter
    //crest gate spends that back: it now needs a real crest (the top fifth of the swell) rather than
    //anything above the mean, so what it lights is the thin water at the top of a wave, which is the only
    //water light gets through.
    float backlight = pow(saturate(dot(viewDir, -SunDirection)), 3.0);
    float sss = backlight * saturate(input.Foam.y * 2.5 - 1.0) * SssStrength * sunlight * (1.0 - calm);
    color += SssColor * SunColor * sss;

    //Sun glint: a sharp spark where the reflected ray points at the sun, sparkling across the chop facets,
    //snuffed out under a cloud shadow
    float glint = pow(saturate(dot(reflected, SunDirection)), SunGlintPower) * SunGlintStrength * sunlight;
    color += glint * SunColor;

    //Whitecap foam. Two per-vertex signals say where foam may LIVE - the Jacobian fold (the wave genuinely
    //breaking) and the crest gate (high on the combined swell) - and neither may draw its own silhouette:
    //both are smooth interpolated scalars, and thresholding the crest height painted the round white blobs
    //#128 was opened for. Where the six fanned waves constructively interfere, their sum is a localized
    //round bump, and a height threshold on a round bump is a disc - a white ball drifting with the phase
    //speed, which is exactly how it was reported. So the gates set only the foam DENSITY, and the visible
    //shape comes from a streak field: band-limited fbm combed along the dominant swell's crest line
    //(Fbm2Combed, the grass relief's idiom), advected downwind, so foam reads as wind-torn lanes riding
    //the crests. The density slides the streak threshold - more energy widens the lanes toward a connected
    //cap rather than brightening a disc - and the field fades against the pixel footprint, so the far sea
    //loses the pattern smoothly instead of shimmering (the fade costs variance, which the horizon haze
    //covers anyway). Foam stays a near-white matte cap lit by sun and sky, composited over the water.
    //(1 - calm) kills the foam in the pool outright: the damped Gerstner sum already gives it no fold and no
    //crest, but the crest signal parks at 0.5 when the amplitudes are zero, and a config with FoamCrestStart
    //under that would lay foam lanes across dead-still water.
    float crestGate = saturate((input.Foam.y - FoamCrestStart) / max(1.0 - FoamCrestStart, 1e-3));
    float density = saturate(max(input.Foam.x * FoamStrength, crestGate * FoamCrestStrength)) * (1.0 - calm);

    float2 foamDomain = (worldPosition.xz + WindDirection * SeaTime * ChopSpeed * 0.5) * FOAM_STREAK_FREQUENCY;
    float streaks = Fbm2Combed(foamDomain, FOAM_STREAK_ALONG, FOAM_STREAK_STRETCH, 4,
        footprint * FOAM_STREAK_FREQUENCY) + 0.5;

    //⚠ The threshold window has to stay ABOVE the streak field's mean at every density, or the lanes stop
    //being lanes (#503). `streaks` is an fbm plus 0.5, so it sits around 0.5; the old window slid to
    //(0.285, 0.49) at full density, which is entirely below that mean and painted about half the area at
    //one value - a connected sheet with a soft outline, which is the #128 blob again at a larger size. The
    //window now slides from the top eighth of the field to its top quarter, so more energy still widens a
    //lane but can never flood the surface.
    //And where a lane DOES fire it goes to full white rather than to a fraction of it (#503). Scaling the
    //alpha linearly with the density spread a little foam over a lot of water, which reads as a sheen or an
    //oil slick - the two things the water is least like. Foam in a photograph is either there, brilliant
    //white, or not there at all; the streak window above is what decides WHERE, and this decides that where
    //it is, it is opaque. Rarer and stronger beats wider and dimmer, which is the same lesson the glowworms
    //and the mineral veins each cost a session to learn.
    float foam = saturate(density * 2.2)
        * smoothstep(0.74 - density * 0.30, 0.88 - density * 0.26, streaks) * chopFade;
    float3 foamCol = FoamColor * (ambient * FOAM_AMBIENT_GAIN
        + SunColor * sunlight * saturate(dot(normal, SunDirection)) * 0.7);
    color = lerp(color, foamCol, foam);

    //Horizon haze: melt the sea into the skyline color over distance, so the plane has no visible edge -
    //towards a DESATURATED horizon since #503, for the reason HAZE_DESATURATION carries. The brightness of
    //the target is untouched; only its saturation comes down.
    float3 hazeTarget = lerp(HorizonColor, dot(HorizonColor, LUMA), HAZE_DESATURATION);
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, hazeTarget, haze * haze);

    return float4(FarFadeToSky(color, worldPosition), 1.0);
}

technique Sea
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SeaVS();
        PixelShader = compile PS_SHADERMODEL SeaPS();
    }
};
