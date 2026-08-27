//Draws the tropical island beach (#244): a ring of white-gold sand the arena and its palms stand on,
//moss-capped rocks strung along the waterline, and a calm turquoise lagoon beyond it that the island's
//own green shore ridge closes all round — the arena stands in the middle of a tropical island's lagoon
//rather than on an open sea. The lagoon's water itself is Sea.fx unchanged, drawn over this terrain by
//DrawTropicalWater; this shader is the land alone.
//
//The land is ONE RADIAL PROFILE keyed on the distance to the waterline rather than the distance to the
//island, and that is the whole of its shape: flat dry sand at the island's foot slopes down through the
//waterline (where the profile crosses the water level BY CONSTRUCTION — two hermite ramps meeting at
//the coast radius, so the waterline and the coast are the same line and cannot drift), carries on to
//the lagoon bed hidden under opaque water, and rises past its own wiggling radius into the far shore
//ridge. The waterline is a mean radius wobbled by sine octaves of BEARING — integer multipliers, so
//the wobble is continuous round the full circle and the coast breaks into bays and headlands instead
//of reading as the circle it is.
//
//TWO THINGS THE HEIGHT FIELD DELIBERATELY IS NOT:
//
//  1. It is built from SINES AND HERMITE RAMPS ONLY, no gradient noise, because the C#
//     TropicalTerrainHeight mirrors it exactly to plant the palms and the rocks on the ground this
//     shader draws — the same contract SavannaTerrainHeight holds. Everything noisy (sand patches,
//     grain, canopy mottle) lives in the pixel shader, where no plant ever asks for it.
//  2. The far ridge is RINGED, not a horizon of hills — and it carries ONE CHANNEL through it, carved
//     by a cosine bump of the bearing, where the open sea reaches the horizon. A closed ring reads as
//     a crater lake; the channel is what says the lagoon belongs to an island in an ocean.
//
//Shader Model 5.0, built out of this one directory by all three executables. It borrows the scene
//toolkit: the sky is the current dome's two-colour gradient in linear radiance, every procedural
//feature band-limits against the pixel footprint, and the cloud shadow is the one shared field in
//Clouds.fxh (the map editor never sets the cloud uniforms, so there CloudSunlight is a flat 1.0).

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

//The shared noise library: gradient noise for the sand's patches and the canopy's mottle, combed fBm
//for the sand's wind ripple. Its hashes are the sine-free ones and its gradient noise fades
//quintically, which matters here for the same reason it matters in the desert — these fields end up
//driving a normal, and the cubic fade's discontinuous second derivative shows as faint lattice
//creases the moment it does.
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, and the sun's own radiance (tinted by the dome)
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance — zenith overhead, horizon at the skyline
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), so the terrain sits still in
//the world while the mesh slides under it
float2 OriginXZ;

//Radius of the island's footprint cut out of the terrain around the world origin. The map editor
//draws no island and leaves it 0.
float IslandHoleRadius;

//The land's profile (see the header): the dry sand's level and undulation, the waterline's mean radius
//and wiggle, the beach's rise and run either side of it, the lagoon bed, and the far shore ridge's
//radius, wiggle, width, height, channel and hills.
float TropicalLevelY;
float ClearingRelief;
float ShoreRadius;
float CoastNoise;
float BeachRise;
float BeachRun;
float SeabedY;
float RingRadius;
float RingNoise;
float RingWidth;
float HillHeight;
float ChannelBearing;
float ChannelSharpness;

//The lagoon's mean level. The terrain shader only reads it to know where the waterline is (the wet
//sand band, and the sand fringe on the far shore) — the water itself is Sea.fx.
float WaterLevelY;

//Reflectances, all LINEAR radiance. The sand runs between warm white-gold and the paler shell debris
//the surf bands; the ridge's jungle between a deep green and a drier, sun-bleached one.
float3 SandColor;
float3 SandColorPale;
float3 VegetationColor;
float3 VegetationDry;

//How strongly the wind combs the far canopy in travelling bands, and the two fine reliefs (sand
//ripple, canopy) that tilt the normal.
float CanopyWindStrength;
float CanopyRelief;
float SandRelief;

//How much of the sky's hemisphere light fills the flats
float AmbientStrength;

//The wind, a direction in the XZ plane: it combs the canopy and lays the sand's ripple downwind.
float2 WindDirection;

//Airborne marine haze: how far the distance fade is carried from the dome's own horizon colour
//towards HazeTint. Weaker than the outback's red dust — tropical air is clear, and the scene keeps
//its own colours; the two-stage arrangement itself is the outback's, for the outback's reason.
float3 HazeTint;
float HazeStrength;
float HorizonHazeDistance;

//The wall clock, for the wind bands travelling through the far canopy.
float TropicalTime;

//--- Form constants ------------------------------------------------------------------------------------

//What a shore IS, rather than what this shore is set to — the meadow's rosette rule, so these live here
//and not on the config. The bare band a coast keeps at its waterline, in world units of height above the
//water, and the rise over which the jungle closes above that band.
static const float SHORE_SAND_FRINGE = 1.1;
static const float JUNGLE_RISE = 4.0;

//How far the sand's own ripple darkens its troughs. The cheapest ambient occlusion there is, and the one
//thing that makes a ripple read on ground the sun is not raking — Desert.fx's lesson, which this scene
//was missing entirely (#268): the ripple existed only as a tilt of the normal, and on flat sand under a
//high sun a tilt draws nothing whatever its amplitude. Measured before the fix, the beach's sand had a
//luminance sd of 4.4 against the desert's 6.0 from the same vantage under the same dome — near enough the
//same AMPLITUDE, and the difference was that the desert's is organised and shaded and this was neither.
static const float RIPPLE_SHADE = 0.24;

//--- The land ------------------------------------------------------------------------------------------

//The waterline's radius at a bearing: the mean wobbled by three sine octaves (integer multipliers of
//the bearing, so the wobble comes back round the circle continuous). The weights sum to 1, so
//CoastNoise is the full peak wiggle in world units.
float CoastRadius(float b)
{
    return ShoreRadius + CoastNoise * (0.45 * sin(2.0 * b + 0.7)
        + 0.35 * sin(3.0 * b + 1.3)
        + 0.20 * sin(5.0 * b + 4.1));
}

//The far shore's own coastline, on its own octaves and phases so the two coasts share no lines.
float ShoreRingRadius(float b)
{
    return RingRadius + RingNoise * (0.40 * sin(2.0 * b + 2.9)
        + 0.34 * sin(3.0 * b + 0.6)
        + 0.26 * sin(7.0 * b + 3.4));
}

//The one channel through the far ridge: a cosine bump of the bearing, sharp enough to read as a pass
//rather than a dip, that takes the ridge's rise back out of the ground inside it. pow(max(0, cos))
//is continuous and zero almost everywhere — no pole, no seam, and the open sea shows through to the
//horizon in the gap.
float ChannelMask(float b)
{
    return pow(max(0.0, cos(b - ChannelBearing)), ChannelSharpness);
}

//The land's height at a world point. Sines and hermite ramps only — TropicalTerrainHeight on the CPU
//mirrors this term for term to plant the scatter, and a gradient noise in here would plant palms in
//the air the day the two drifted. Keep the two in one change.
float TropicalHeight(float2 p)
{
    float r = length(p);
    float b = atan2(p.y, p.x);

    //The dry sand's gentle undulation — a beach is not a snooker table. Amplitudes are kept well under
    //the sand's height above water, so no dip ever puddles below a waterline it is not at.
    float gentle = ClearingRelief * 0.5 * (sin(p.x * 0.043 + p.y * 0.031)
        + 0.6 * sin(-p.x * 0.052 + p.y * 0.046 + 2.1));

    //The beach, as two hermite ramps meeting at the coast: the first takes the flat sand down THROUGH
    //the water level exactly at the coast radius (the waterline is where the coast is, by construction
    //rather than by tuning), the second carries the slope on to the bed. Past the second ramp the
    //ground is under opaque water and nothing about it is ever seen.
    float d = r - CoastRadius(b);

    float toWaterline = smoothstep(-BeachRise, 0.0, d);
    float toBed = smoothstep(0.0, BeachRun, d);

    float h = lerp(TropicalLevelY + gentle, WaterLevelY, toWaterline);
    h = lerp(h, SeabedY, toBed);

    //The far shore ridge, rising out of the bed past its own wiggling coastline, scaled by rolling
    //hills and pierced by the one channel. The hills are DOMAIN-WARPED sines — summed plane waves keep
    //their planes however many terms they have, and the warp is what bends their wavefronts into
    //ridges (the forest's own trick, and exact to mirror for the same reason everything else here is:
    //a warp by a sine is still a sine).
    float ring = smoothstep(0.0, RingWidth, r - ShoreRingRadius(b)) * (1.0 - ChannelMask(b));

    float qx = p.x + 26.0 * sin(p.y * 0.011 + 2.0);
    float qz = p.y + 26.0 * sin(p.x * 0.013 + 5.0);

    float rolling = 0.40 * sin(qx * 0.020 + qz * 0.015)
        + 0.27 * sin(-qx * 0.013 + qz * 0.024 + 1.5)
        + 0.19 * sin(qx * 0.031 + qz * 0.026 + 3.0)
        + 0.14 * sin(-qx * 0.056 + qz * 0.041 + 0.7);

    h += ring * HillHeight * (0.55 + 0.45 * (0.5 + 0.5 * rolling));

    return h;
}

struct TropicalVertexInput
{
    float4 Position : POSITION0;
};

struct TropicalVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

TropicalVertexOutput TropicalVS(TropicalVertexInput input)
{
    TropicalVertexOutput output;

    //Local grid position + the snapped origin gives the world XZ; the land is sampled there, so it sits
    //still in the world while the grid slides under it
    float2 worldXZ = input.Position.xz + OriginXZ;
    float3 worldPosition = float3(worldXZ.x, TropicalHeight(worldXZ), worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//--- The surface ---------------------------------------------------------------------------------------

//Tangent-free normal tilt from a height field (Christian Schueler), the same one the balls, the ground
//relief and every terrain scene here use — the grid carries no tangents and the fine relief never
//reaches it anyway.
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

float4 TropicalPS(TropicalVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The base normal, taken PER PIXEL from the height field's own gradient (three taps) rather than
    //interpolated from a per-vertex normal. Interpolating a coarse displaced grid's per-vertex normal
    //is what left a Mach-band grid across the old dunes; evaluated per pixel the shading is smooth
    //whatever the tessellation.
    float e = 1.5;
    float here = TropicalHeight(worldPosition.xz);
    float hx = TropicalHeight(worldPosition.xz + float2(e, 0.0));
    float hz = TropicalHeight(worldPosition.xz + float2(0.0, e));

    float2 slope = float2(hx - here, hz - here) / e;
    float3 baseNormal = normalize(float3(-slope.x, 1.0, -slope.y));

    //--- Where the ground is vegetated -----------------------------------------------------------------
    //The far shore ridge is the jungle; the near beach is sand, all the way to its own waterline. Two
    //separate questions, and #268 was them being answered by one term: WHICH landmass this is (a radial
    //gate, so the near beach can never turn green under a dome that wants it to), and HOW VEGETATED it
    //is (its own height above the water — a coast is bare sand at the waterline and jungle above it).
    //
    //It used to be `ring * fringe`, where `ring` is the ridge's own RISE — the same 95-unit ramp
    //(RingWidth) that lifts the ridge in the height field. Greenness therefore grew OUTWARD: zero at
    //the ridge's inner edge and full only most of a hundred units further out, i.e. over the crest and
    //beyond. The near flank is the whole of what the lagoon and the play camera look at, and it was
    //sand by construction — measured across the ridge band at (204, 199, 168), red over green, with no
    //green in it anywhere. The jungle was on the far side.
    float b = atan2(worldPosition.z, worldPosition.x);

    //The gate is now SHORT — a couple of units of feather either side of the far coastline. It only
    //separates the two landmasses; it no longer decides how green anything is.
    float onRidge = smoothstep(-2.0, 8.0, length(worldPosition.xz) - ShoreRingRadius(b))
        * (1.0 - ChannelMask(b));

    //The bare band every shore keeps at its waterline, and the rise over which the jungle closes above
    //it. Form constants rather than config dials (the meadow's rosette rule): they say what a shore IS,
    //and the ridge stands 15–28 units out of the water, so all but its own beach comes up green.
    float fringe = smoothstep(SHORE_SAND_FRINGE, SHORE_SAND_FRINGE + JUNGLE_RISE, here - WaterLevelY);
    float green = onRidge * fringe;

    //A broad field doing two jobs: the sand's patch tone and the canopy's dry patches. One octave —
    //everything above this scale is carried by the canopy mottle and the grain, and nothing below the
    //pixel frequency at any distance this is drawn from.
    float broad = GradientNoise2(worldPosition.xz * 0.017);

    //The sand's ripple, combed downwind — what the wind lays on a beach. Computed HERE rather than down
    //in the normal block, because it now does two jobs: it tilts the normal (as it always did) and it
    //shades its own troughs (as it never did, which is #268's second finding). Band-limited against the
    //footprint, so both jobs fade together into smooth distant sand rather than into a crawl.
    //The domain scale is the SECOND half of #268's sand finding, and the measurable one. At 0.85 a ripple
    //was about 1.2 world units across — three times finer than the desert's (whose crest lines run about
    //3.9 apart) — so `Fbm2BandLimited`'s own contract band-limited it away at a third of the distance and
    //the beach was smooth everywhere the eye actually looks. Shading the troughs of a field nothing can
    //resolve buys nothing: measured, the shading alone moved the sand's luminance sd 4.42 → 4.70 against
    //the desert's 5.98, and coarsening it is what closes the rest.
    float rippleDomain = 0.28;
    //Four octaves rather than three, since the coarsest is now three times coarser: the family still reaches
    //down to the half-unit detail the near sand had, and the band-limit drops each one as it becomes
    //unresolvable rather than all of them at once.
    float ripple = Fbm2Combed(worldPosition.xz * rippleDomain, WindDirection, 2.2, 4, footprint * rippleDomain);

    //--- The sand --------------------------------------------------------------------------------------
    float3 sand = lerp(SandColor, SandColorPale, saturate(0.5 + broad * 1.6));

    //The ripple's own shading. This is the whole of the fix: a normal tilt only draws where the light
    //rakes across it, and a beach is flat ground under a sun that mostly does not — so the troughs are
    //darkened directly. Rides the same band-limited field the tilt does, so it cannot alias.
    sand *= 1.0 - RIPPLE_SHADE * saturate(0.5 - ripple * 1.6);

    //WET SAND just above the waterline — darker and a little heavier the nearer the water reaches,
    //which is how a beach says where its surf dies. Keyed on HEIGHT above the water, not on distance
    //to the coast, so it follows the wiggling waterline of either coast exactly.
    float wet = 1.0 - smoothstep(0.12, 1.05, here - WaterLevelY);
    sand *= lerp(1.0, 0.60, wet);

    //The shell-and-coral grain, one hash per pixel over a fine lattice, gone within a few units — the
    //fade finishes while a cell is still two pixels wide (the desert's grain rule).
    float grainFade = saturate(1.0 - footprint * 90.0);
    sand *= 1.0 + NoiseHash22(floor(worldPosition.xz * 45.0)).x * 0.14 * grainFade;

    //--- The canopy ------------------------------------------------------------------------------------
    //Two scales of broken foliage — the forest floor's lesson that a two-scale mottle is what reads
    //as vegetation and a one-scale patch reads as camouflage — both band-limited against the footprint,
    //which at the distances the ridge is seen from is most of the point.
    float canopyDomain = 0.045;
    float canopy = Fbm2BandLimited(worldPosition.xz * canopyDomain, 3, footprint * canopyDomain);

    float3 veg = lerp(VegetationColor, VegetationDry, saturate(0.5 - broad * 1.4 + canopy * 1.1));

    //Travelling wind bands through the canopy, the savanna's grass trick at a distance: brightness
    //riding a plane wave laid along the wind. Mild — at this range it is a shimmer in the green, not
    //a pattern.
    veg *= 1.0 + CanopyWindStrength * sin(dot(worldPosition.xz, WindDirection) * 0.14 - TropicalTime * 1.2);

    //Shading between the canopy's masses: the cheap ambient occlusion every scene here leans on, so a
    //shadowed flank of the ridge is not a flat patch of colour. Rides the mottle, not the mask.
    veg *= 0.80 + 0.30 * saturate(0.5 + canopy * 1.6);

    float3 albedo = lerp(sand, veg, green);

    //--- Normal ----------------------------------------------------------------------------------------
    //ONE height field for the fine relief and ONE perturbation off it, the two materials' reliefs
    //lerped by the same mask their colours are (the outback's rule: two PerturbNormalFromHeight calls
    //would be two more pairs of screen derivatives for a result that is a lerp of the inputs anyway).
    //The sand's ripple (computed up in the sand block, since it shades as well as tilts) and the canopy's,
    //which runs at its own two scales with no grain — a crown of leaves from far away has none of.
    float relief = lerp(ripple * SandRelief, canopy * CanopyRelief, green);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    //--- Lighting --------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);
    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light: up-facing ground takes the zenith, faces turned to the skyline take the horizon
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //--- The air ---------------------------------------------------------------------------------------
    //The outback's two-stage fade, for the outback's reason: the mid-distance keeps the scene's own
    //colour under any dome (aerial perspective tinting the far shore with a teal horizon is correct
    //and still the wrong picture at strength), and only the last stretch arrives at the dome's exact
    //HorizonColor, so the terrain's edge has no seam against the sky it does not match. The tint is
    //weak and near-neutral here — clear marine air, not dust.
    float3 skyLight = HorizonColor + SunColor * 0.35;
    float skyLuminance = dot(skyLight, float3(0.2126, 0.7152, 0.0722));

    float3 hazeLit = HazeTint * lerp(skyLuminance.xxx, skyLight, 0.45);

    float haze = saturate(dist / HorizonHazeDistance);
    float haze2 = haze * haze;
    float haze4 = haze2 * haze2;

    color = lerp(color, hazeLit, HazeStrength * haze2);

    //The eighth power, where the outback (whose arrangement this is) uses the fourth — and the difference
    //is the two scenes' compositions, not taste. The outback's far field is empty plain and arriving at
    //the dome's exact horizon colour early costs it nothing; THIS scene's horizon IS a feature, the green
    //ridge that closes the lagoon, and it stands at 300–420 units against a haze distance pinned at 480
    //by the grid's own half-extent (see HorizonHazeDistance). At the fourth power the ridge's crest was
    //already 48 % of the way to the sky, which is what turned the jungle beige and left #268 measuring
    //(204, 199, 168) across it — red over green, with no green in it anywhere. The eighth still reaches 1
    //at the grid's edge, so the seam it exists to hide is still hidden; it just stops eating the ridge.
    color = lerp(color, HorizonColor, haze4 * haze4);

    return float4(color, 1.0);
}

technique Tropical
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL TropicalVS();
        PixelShader = compile PS_SHADERMODEL TropicalPS();
    }
};
