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

//The storm spectrum: a dominant long swell down to fine chop, directions fanned around and across the wind
//so the sum never settles into a tile. Directions need not be unit here - they are normalized at use. The
//amplitudes are weights on WaveAmplitude; the steepness weights ride on WaveSteepness. Kept few enough to
//unroll cheaply, since every vertex evaluates the lot.
//Wavelengths are kept short relative to the scene (a ball is ~1 unit) so several crests fall inside the
//visible water and it reads as waves, not one flat tilt - a 100-unit swell only ever shows a corner of one
//wave here. Steepness is high so the crests pinch sharp for a rough sea.
static const int WAVE_COUNT = 6;
static const float2 WAVE_DIR[6]   = { float2(1.0, 0.35), float2(0.7, 0.72), float2(1.0, -0.30), float2(0.35, 1.0), float2(-0.45, 1.0), float2(1.0, 0.85) };
static const float  WAVE_LEN[6]   = { 52.0, 33.0, 21.0, 13.0, 8.5, 5.5 };
static const float  WAVE_AMP[6]   = { 1.0, 0.72, 0.5, 0.34, 0.22, 0.14 };
static const float  WAVE_STEEP[6] = { 0.9, 0.95, 1.0, 1.0, 1.0, 1.0 };
static const float  WAVE_PHASE[6] = { 0.0, 1.7, 3.1, 4.2, 5.5, 0.9 };

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
    float3 WorldNormal : TEXCOORD1;
    //x = whitecap fold factor (0..1), y = crest height normalized (0..1)
    float2 Foam : TEXCOORD2;
};

//Sum of Gerstner waves at the rest position p0. Returns the world-space displacement (horizontal AND
//vertical - the horizontal pinch is what sharpens the crests over a plain height field), the analytic
//surface normal (GPU Gems 1 form, WA = k*A), the horizontal Jacobian fold (drops below 1 as crests pinch,
//negative where the surface overhangs - the whitecap generator) and the crest height normalized to 0..1.
//ampScale fades the whole thing to flat towards the horizon.
void OceanSurface(float2 p0, float ampScale, out float3 disp, out float3 normal, out float fold, out float crest)
{
    disp = float3(0.0, 0.0, 0.0);

    //Normal accumulators: N = normalize(-nx, 1 - nySub, -nz)
    float nx = 0.0, nz = 0.0, nySub = 0.0;

    //Jacobian accumulators for the horizontal displacement map (x,z) -> (x+dx, z+dz)
    float jxx = 0.0, jzz = 0.0, jxz = 0.0;

    float sumAmp = 0.0;

    [unroll]
    for (int i = 0; i < WAVE_COUNT; i++)
    {
        float2 d = normalize(WAVE_DIR[i]);
        float k = TWO_PI / WAVE_LEN[i];
        float a = WaveAmplitude * WAVE_AMP[i] * ampScale;
        float w = sqrt(GRAVITY * k) * WaveSpeed;   //deep-water dispersion: long swells roll slower than chop
        float q = WaveSteepness * WAVE_STEEP[i];

        float phase = k * dot(d, p0) + w * SeaTime + WAVE_PHASE[i];
        float c = cos(phase);
        float s = sin(phase);

        float qa = q * a;
        disp.x += qa * d.x * c;
        disp.z += qa * d.y * c;
        disp.y += a * s;

        float wa = k * a;
        nx += d.x * wa * c;
        nz += d.y * wa * c;
        nySub += q * wa * s;

        //d(disp.x)/dx0 = -q*a*k*d.x*d.x*sin(phase); the map derivative subtracts that from the identity
        jxx += q * wa * d.x * d.x * s;
        jzz += q * wa * d.y * d.y * s;
        jxz += q * wa * d.x * d.y * s;

        sumAmp += a;
    }

    normal = normalize(float3(-nx, 1.0 - nySub, -nz));

    float jacobian = (1.0 - jxx) * (1.0 - jzz) - jxz * jxz;
    fold = saturate((FoamJacobianThreshold - jacobian) / max(FoamJacobianThreshold, 1e-3));

    crest = saturate(disp.y / max(sumAmp, 1e-3) * 0.5 + 0.5);
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

    float3 disp;
    float3 normal;
    float fold, crest;
    OceanSurface(restXZ, ampScale * (1.0 - calm), disp, normal, fold, crest);

    float3 worldPosition = float3(restXZ.x + disp.x, SeaLevelY + disp.y, restXZ.y + disp.z);

    output.WorldPosition = worldPosition;
    output.WorldNormal = normal;
    output.Foam = float2(fold, crest);
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//One fine chop octave, band-limited against the pixel footprint like the ground relief, so the chop fades
//into smooth water towards the horizon rather than aliasing into a shimmer. Accumulated as a height for
//PerturbNormalFromHeight to tilt the Gerstner normal by.
float ChopRipple(float2 xz, float2 dir, float frequency, float footprint)
{
    float resolvable = saturate(1.0 - footprint * frequency / 3.14159265);
    return sin(dot(xz, dir) * frequency) * resolvable;
}

//The fine wind-chop height field: a few octaves crossing the wind, scrolling downwind so the surface crawls
float ChopHeight(float2 xz, float footprint)
{
    float2 p = xz + WindDirection * SeaTime * ChopSpeed;
    float f = ChopFrequency;

    float h = 0.5 * ChopRipple(p, normalize(float2(0.9, 0.4)), f, footprint)
        + 0.28 * ChopRipple(p, normalize(float2(0.6, -0.8)), f * 1.9, footprint)
        + 0.15 * ChopRipple(p, normalize(float2(-0.5, 0.85)), f * 3.4, footprint)
        + 0.09 * ChopRipple(p, normalize(float2(0.2, -0.98)), f * 5.7, footprint);

    return h * ChopAmplitude;
}

//Tangent-free normal tilt from a height field (Christian Schueler), the same one the balls and the ground
//relief use - the grid carries no tangents and the chop never reaches the vertices anyway.
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

float4 SeaPS(SeaVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the ring of the island's footprint out of the surface, keeping BOTH sides of it: the open sea past
    //IslandHoleRadius and the pool standing inside the drain (#132) — max() keeps a pixel that survives on
    //either count. Only the annulus between them, hidden inside the island's stone, is discarded. 0 in the
    //map editor keeps it all: r - 0 is never negative, whatever the pool radius says.
    float r = length(worldPosition.xz);
    clip(max(r - IslandHoleRadius, FunnelPoolRadius - r));

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
    float3 normal = PerturbNormalFromHeight(normalize(input.WorldNormal), worldPosition, chop);

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
    float shallowMix = lerp(saturate(normal.y) * 0.5 * (1.0 - 0.8 * calm), 1.0, ShallowBias);

    float3 body = lerp(WaterColorDeep, WaterColorShallow, shallowMix) * ambient + ZenithColor * 0.05;

    float3 color = lerp(body, reflection, fresnel);

    //Subsurface scattering: a crest glows when the sun is behind it and the eye looks into the water. The
    //classic cheap term - looking towards the sun, strongest on the raised faces of the waves, snuffed by cloud.
    float backlight = pow(saturate(dot(viewDir, -SunDirection)), 4.0);
    float sss = backlight * saturate(input.Foam.y * 2.0 - 0.5) * SssStrength * sunlight * (1.0 - calm);
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

    float foam = min(1.0, density * 1.15)
        * smoothstep(0.60 - density * 0.45, 0.70 - density * 0.30, streaks) * chopFade;
    float3 foamCol = FoamColor * (ambient + SunColor * sunlight * saturate(dot(normal, SunDirection)) * 0.7);
    color = lerp(color, foamCol, foam);

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
