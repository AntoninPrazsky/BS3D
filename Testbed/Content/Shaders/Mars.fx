//Draws Mars (#277): rust-red cratered ground under a dusty, horizon-bright dome - the sixteenth scene, and
//deliberately NOT a red-palette copy of the Moon (#125). The real Moon has no atmosphere at all, which is
//the only reason that scene replaces the sky and closes its horizon with curvature; Mars keeps a (thin)
//atmosphere, so it is an ordinary IsSolidTerrainScene backdrop - real ground under an ordinary dome, one
//more of the nineteen SkyDome palettes rather than a domeless void.
//
//Two techniques over two draws of one frame:
//  - MarsTerrain: the crater field lifted VERBATIM from Moon.fx (CraterLayer/TurnCrater/CraterField/
//    MareBase are generic height-field math with nothing Moon-specific in them - only the constants that
//    tune it and the colour it is coloured by are this scene's own), retextured rust/ochre instead of
//    grey, on the outback's plumbing instead of the Moon's: an ordinary sun-and-dome light rig, the shared
//    cloud shadow (Clouds.fxh), and the outback's two-stage haze fade to the dome's own horizon colour -
//    NOT the Moon's curvature-and-highland-belt, which exists purely because the Moon has no air to fade
//    into. There is no highland belt here; the ordinary haze closes the horizon the way the desert's and
//    the outback's does. Standing on it: two lattices of boulders and pebbles, the outback's RockLayer
//    lifted VERBATIM too and retuned from a skyline of monoliths to the litter of stones a rover
//    photograph shows, dark volcanic basalt rather than more of the ground's own rust.
//  - MarsMoons: Phobos and Deimos, two small analytic discs composited over the dome and the terrain -
//    space's full-screen-quad machinery (the shared corner quad, InverseViewProjection), depth-READ
//    against the depth MarsTerrain just wrote (Moon.fx's own measured reason: depth-read after the ground,
//    never before it) so a moon low enough to sit behind a crater rim is occluded by it, and ALPHA-BLENDED
//    (unlike the Moon's opaque sky pass) so the dome and the terrain show through everywhere neither disc
//    covers. No continents, no clouds, no atmosphere rim - that is the Moon's Earth, and neither of these
//    moons has an atmosphere of its own to put one on.
//
//DELIBERATELY NOT IN THIS PASS (see issue #277's own open questions, and MarsSceneConfig's doc): a
//foreground dust-haze/dust-devil overlay (Spray.fx's or the volcano's ash's machinery). A real gap, left
//for later - it does not block a shippable scene the way a bare, stoneless plain would have.
//
//Everything is written in LINEAR RADIANCE into the HDR target. Built by all three executables out of this
//directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, and the sun's own radiance (the lit-cloud colour the weather uses, tinted by the dome) -
//shared between the MarsTerrain and MarsMoons techniques, so the moons are lit by the same sun the ground is.
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline. Mars's own dome
//(the nineteenth SkyDome palette) gets BRIGHTER near the horizon than at the zenith - the opposite of
//Earth's blue-zenith Rayleigh falloff - but nothing here assumes which end is which; it just reads whatever
//the current dome states.
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), so the terrain sits still in the
//world while the mesh slides under it
float2 OriginXZ;

//Radius of the island's footprint cut out of the terrain around the world origin. The map editor draws no
//island and leaves it 0.
float IslandHoleRadius;

//The plain: its mean level (the island's foot), and the clearing it stands in - gates the crater field only,
//never a plain's-worth of undulation, the same shape every solid-terrain sibling uses around the round island.
float MarsLevelY;
float ClearingRadius;
float ClearingTransition;

//Peak height of the crater field out in the far field (world units over the whole three-octave sum).
float CraterAmplitude;

//The stone field: two lattices of boulders and pebbles standing on the plain (RockLayer below, ported
//verbatim from Outback.fx). Spacing/seed pairs are unrelated so the two grids share no lines.
float RockSpacing;
float RockChance;
float RockHeight;
float PebbleSpacing;
float PebbleChance;
float PebbleHeight;

//Rust reflectance (linear): the deep oxidised iron of the plain and the paler, dustier rust of fresh crater
//rims and settled patches.
float3 RustColor;
float3 RustColorPale;

//How strongly a crater's raised rim brightens towards RustColorPale (freshly excavated material).
float EjectaBrightness;

//The stones' own reflectance (linear): dark volcanic basalt in shadow, its sunlit facets dusted with the
//same rust the ground wears - the one thing on the plain that is not red in the shade.
float3 BoulderColorDeep;
float3 BoulderColorBright;

//How rough a stone's own face is (world units of relief) - the scale below its silhouette.
float RockRelief;

//Fine surface: peak height of the pixel-scale relief (world units) and the near-camera grain strength.
float MicroReliefStrength;
float GrainStrength;

//How much of the sky's hemisphere light fills the plain.
float AmbientStrength;

//Airborne rust dust: how far the haze is carried from the dome's own horizon colour towards HazeTint - the
//outback's own two-stage fade (see MarsTerrainPS), so Mars's distance stays rust-coloured under any dome
//rather than borrowing whatever hue that dome happens to be.
float3 HazeTint;
float DustStrength;
float HorizonHazeDistance;

//--- The moons -------------------------------------------------------------------------------------------

//Recovers the view ray per pixel for the full-screen MarsMoons pass - space's own trick (the far plane is a
//plane in world space and screen-to-far-plane is affine, so interpolating it across the quad is exact).
float4x4 InverseViewProjection;

float3 PhobosDirection;      //normalized
float PhobosAngularRadius;   //radians
float3 PhobosColor;          //linear

float3 DeimosDirection;      //normalized
float DeimosAngularRadius;   //radians
float3 DeimosColor;          //linear

//=====================================================================================================
//The terrain
//=====================================================================================================

//One octave of craters: at most one per cell of a jittered lattice, and only the pixel's OWN cell is ever
//read - the single-cell trick Moon.fx's craters, the space starfield and the meadow's wildflowers all use.
//Ported verbatim from Moon.fx (#125): this field is generic height-field math with nothing Moon-specific in
//it, so retexturing it rust rather than grey needed no change here at all. See Moon.fx for the derivation
//of every constant below - CRATER_MIN_RADIUS/CRATER_MAX_RADIUS, the per-octave amplitudes and periods in
//CraterField, and the margin arithmetic that keeps a crater inside its own cell (#240 there).
static const float CRATER_MIN_RADIUS = 0.12;
static const float CRATER_MAX_RADIUS = 0.21;

float CraterLayer(float2 p, float seedOffset, float chance, out float ejecta)
{
    float2 cellId = floor(p);
    float2 f = p - cellId;

    ejecta = 0.0;

    float2 rollA = NoiseHash22(cellId + seedOffset) * 0.5 + 0.5;

    if (rollA.x > chance) return 0.0;

    float2 rollB = NoiseHash22(cellId + seedOffset + 47.9) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seedOffset + 91.7) * 0.5 + 0.5;

    float radius = lerp(CRATER_MIN_RADIUS, CRATER_MAX_RADIUS, rollB.x * rollB.x);
    float margin = radius * 1.6;
    float2 centre = margin + rollC * (1.0 - 2.0 * margin);

    float d = length(f - centre) / radius;

    if (d >= 1.6) return 0.0;

    float depth = lerp(0.55, 1.0, rollB.y);

    float cup = saturate(1.0 - d * d);
    float bowl = -cup * cup * depth;

    float rimWidth = lerp(0.18, 0.42, rollA.y);
    float rimT = (d - 1.0) / rimWidth;

    float rim = exp(-rimT * rimT) * smoothstep(1.6, 1.1, d);

    ejecta = rim * rollB.y;

    return bowl + rim * depth * 0.62;
}

static const float2 CRATER_TURN_0 = float2(0.97437, 0.22495);   //13 degrees
static const float2 CRATER_TURN_1 = float2(0.75471, 0.65606);   //41 degrees
static const float2 CRATER_TURN_2 = float2(0.27564, 0.96126);   //74 degrees
static const float2 CRATER_TURN_3 = float2(0.55919, 0.82903);   //56 degrees

float2 TurnCrater(float2 p, float2 turn)
{
    return float2(p.x * turn.x - p.y * turn.y, p.x * turn.y + p.y * turn.x);
}

float CraterField(float2 p, out float ejecta)
{
    float e0, e1, e2;

    float height = CraterLayer(TurnCrater(p, CRATER_TURN_0) * (1.0 / 129.0), 11.3, 0.86, e0) * 0.58
        + CraterLayer(TurnCrater(p, CRATER_TURN_1) * (1.0 / 49.0), 37.7, 0.82, e1) * 0.29
        + CraterLayer(TurnCrater(p, CRATER_TURN_2) * (1.0 / 18.6), 71.1, 0.70, e2) * 0.13;

    ejecta = max(e0 * 0.9, max(e1, e2 * 0.8));

    return height;
}

//Gentle undulation under the craters, so the plain is not a snooker table between them - genuinely two
//octaves of gradient noise, not a sine pair (Noise.fxh's opening, learned by three scenes the hard way).
float MareBase(float2 p)
{
    return GradientNoise2(p * 0.011) * 0.65 + GradientNoise2(p * 0.031 + 7.3) * 0.35;
}

//The stone field: two lattices of boulders and pebbles, ported VERBATIM from Outback.fx's RockLayer - a
//single-cell jittered lattice (the craters' own trick, above) shaped into a whaleback rock with a talus
//apron. Generic height-field math with nothing outback-specific in it: no gullies and no elongation to
//speak of here (ribDepth 0, a low maxElongation) because Mars rock is wind-worn, not water-cut, so the
//shape reads as a rounded stone rather than a ribbed bornhardt.
#define TALUS_REACH 1.3

float2 RotateInto(float2 p, float2 axis)
{
    return float2(dot(p, axis), dot(p, float2(-axis.y, axis.x)));
}

float2 RollDirection(float2 roll)
{
    float2 v = roll * 2.0 - 1.0;

    return v * rsqrt(max(dot(v, v), 1e-4));
}

float RockLayer(float2 p, float cellSize, float seed, float chance, float height,
    float minRadius, float maxRadius, float maxElongation, float ribDepth, out float shape, out float rib)
{
    shape = 0.0;
    rib = 0.0;

    float2 q = p / cellSize;
    float2 cellId = floor(q);
    float2 f = q - cellId;

    float2 rollA = NoiseHash22(cellId + seed) * 0.5 + 0.5;

    if (rollA.x > chance) return 0.0;

    float2 rollB = NoiseHash22(cellId + seed + 23.7) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seed + 57.1) * 0.5 + 0.5;
    float2 rollD = NoiseHash22(cellId + seed + 91.3) * 0.5 + 0.5;

    float radius = lerp(minRadius, maxRadius, rollB.x);
    float elongation = lerp(1.0, maxElongation, rollD.y);

    float margin = min(radius * TALUS_REACH * (1.0 + ribDepth) * elongation, 0.45);
    float2 centre = margin + rollC * (1.0 - 2.0 * margin);

    float2 centreWorld = (cellId + centre) * cellSize;
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, length(centreWorld));

    if (ramp <= 0.0) return 0.0;

    float2 local = RotateInto(f - centre, RollDirection(rollD));
    local.x /= elongation;

    float reach = length(local);
    float d1 = reach / radius;

    float2 radial = local * rsqrt(max(dot(local, local), 1e-6));
    rib = GradientNoise2(radial * 3.2 + cellId * 13.1 + seed);

    float ribbed = d1 * (1.0 + rib * ribDepth * smoothstep(0.20, 0.62, d1));

    float2 rollE = NoiseHash22(cellId + seed + 131.9) * 0.5 + 0.5;
    float lobeScale = lerp(0.40, 0.60, rollE.y);
    float2 lobeCentre = RollDirection(rollE) * radius * 0.5;

    float d2 = length(local - lobeCentre) / (radius * lobeScale);

    float crest = lerp(0.30, 0.55, rollA.y);
    float body = max(smoothstep(1.0, crest, ribbed), smoothstep(1.0, crest, d2) * lerp(0.55, 0.90, rollE.x));

    float apron = smoothstep(TALUS_REACH, 1.0, min(d1, d2));

    shape = saturate(body + apron * 0.45);

    return (body * 0.9 + apron * 0.1) * height * lerp(0.62, 1.0, rollB.y) * ramp;
}

//The full displaced height at a world point: flat at MarsLevelY inside the clearing around the island,
//rising into cratered ground with distance, with boulders and pebbles standing on it. UNLIKE THE MOON
//there is no highland belt and no planetary curvature - Mars keeps its air, so MarsTerrainPS's haze fade
//closes the horizon the ordinary way, not geometry. Tapped to displace the vertex (VS) and, thrice, for
//the per-pixel normal (PS).
float MarsHeight(float2 p, out float ejecta, out float rockShape)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float mare = MareBase(p);
    float field = CraterField(p, ejecta) + mare * 0.18;

    //Two lattices, ROTATED against each other (the outback's own reason) so the boulders and the pebbles
    //between them never come out ranked along one grid's rows. Neither carries a rib (ribDepth 0) - Mars
    //rock is wind-worn, not water-cut.
    float rockShapeA, pebbleShape, ribUnusedA, ribUnusedB;
    float rocks = RockLayer(p, RockSpacing, 311.7, RockChance, RockHeight,
        0.12, 0.22, 1.35, 0.0, rockShapeA, ribUnusedA);
    float pebbles = RockLayer(RotateInto(p, float2(0.8253, 0.5647)), PebbleSpacing, 733.1, PebbleChance, PebbleHeight,
        0.15, 0.28, 1.30, 0.0, pebbleShape, ribUnusedB);

    rockShape = saturate(rockShapeA + pebbleShape);

    return MarsLevelY + CraterAmplitude * ramp * field + rocks + pebbles;
}

struct MarsTerrainVertexInput
{
    float4 Position : POSITION0;
};

struct MarsTerrainVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

MarsTerrainVertexOutput MarsTerrainVS(MarsTerrainVertexInput input)
{
    MarsTerrainVertexOutput output;

    float2 worldXZ = input.Position.xz + OriginXZ;

    float ejectaUnused, rockShapeUnused;
    float3 worldPosition = float3(worldXZ.x, MarsHeight(worldXZ, ejectaUnused, rockShapeUnused), worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

float4 MarsTerrainPS(MarsTerrainVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    clip(length(worldPosition.xz) - IslandHoleRadius);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The base normal, taken PER PIXEL from the height field's own gradient (three taps) rather than
    //interpolated from a per-vertex normal - every terrain scene's rule.
    float e = 1.5;
    float ejecta, ejectaX, ejectaZ, rockShape, rockShapeX, rockShapeZ;
    float h = MarsHeight(worldPosition.xz, ejecta, rockShape);
    float hx = MarsHeight(worldPosition.xz + float2(e, 0.0), ejectaX, rockShapeX);
    float hz = MarsHeight(worldPosition.xz + float2(0.0, e), ejectaZ, rockShapeZ);

    float2 slope = float2(hx - h, hz - h) / e;
    float3 baseNormal = normalize(float3(-slope.x, 1.0, -slope.y));

    //Fine surface: the Moon's fourth, small-crater detail octave (normal-only - at this scale a crater is
    //shading, not silhouette) plus an isotropic fine relief, both band-limited against the footprint.
    float smallEjecta;
    float smallCraters = CraterLayer(TurnCrater(worldPosition.xz, CRATER_TURN_3) * (1.0 / 7.2), 133.7, 0.64, smallEjecta)
        * saturate(1.0 - footprint * (2.0 / 5.0));

    float relief = Fbm2BandLimited(worldPosition.xz * 1.7, 3, footprint * 1.7);

    //The rock's own face, height-folded (Outback's rule: a field of XZ alone is constant down a vertical
    //flank, and a boulder is mostly flank). Only blended in where the stone field actually stands.
    float rockMask = smoothstep(0.03, 0.30, rockShape);
    float rockSurface = Fbm2BandLimited(worldPosition.xz * 0.42 + worldPosition.y * 0.24, 3, footprint * 0.42);

    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition,
        lerp(smallCraters * (MicroReliefStrength * 3.0) + relief * MicroReliefStrength,
            rockSurface * RockRelief, rockMask));

    ejecta = max(ejecta, smallEjecta * 0.5);

    //--- The rust colour -----------------------------------------------------------------------------
    //Rust on rust, but never one rust: broad albedo patches, pale fresh ejecta on crater rims, and a
    //per-pixel grain close up - the Moon's cascaded three-octave field, carried over unchanged (only the
    //colour it modulates is Mars's own).
    float broad = GradientNoise2(worldPosition.xz * 0.021);

    float3 rust = lerp(RustColor, RustColorPale, saturate(broad * 1.5 + 0.35));

    rust = lerp(rust, RustColorPale * 1.18, saturate(ejecta * EjectaBrightness));

    float grainFine = saturate(1.0 - footprint * 96.0);
    float grainCoarse = saturate(1.0 - footprint * 1.5);
    float grainPebbles = saturate(1.0 - footprint * 0.36);
    rust *= 1.0 + GrainStrength * (
        NoiseHash22(floor(worldPosition.xz * 48.0)).x * grainFine
        + NoiseHash22(floor(worldPosition.xz * 0.75) + 17.0).x * 0.7 * grainCoarse
        + NoiseHash22(floor(worldPosition.xz * 0.18) + 41.0).x * 0.5 * grainPebbles);

    //--- The stones ----------------------------------------------------------------------------------
    //Dark volcanic basalt, not more of the ground's own rust - the one thing on the plain that is not
    //red, dusted where the sun catches it. A real rover photograph's whole reason a stone field reads as
    //ROCK rather than as lumps of the dust it stands in.
    float3 boulder = lerp(BoulderColorDeep, BoulderColorBright, saturate(broad * 0.6 + rockSurface * 0.9 + 0.45));

    float3 albedo = lerp(rust, boulder, rockMask);

    //--- Lighting ------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);
    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light: up-facing ground takes the zenith, faces turned to the skyline take the horizon
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //--- The air -------------------------------------------------------------------------------------
    //Aerial perspective through rust dust, the outback's own two-stage fade: the mid-distance keeps Mars's
    //own colour (a dome's own horizon colour alone would paint the far plain whatever hue that dome
    //happens to be), and the last stretch arrives at the dome's exact HorizonColor so the mesh's edge
    //never shows as a seam against a sky it does not match. No heat shimmer: Mars' thin, cold CO2
    //atmosphere does not refract light the way the Sahara's hot ground does.
    float3 skyLight = HorizonColor + SunColor * 0.35;
    float skyLuminance = dot(skyLight, float3(0.2126, 0.7152, 0.0722));

    float3 dustLit = HazeTint * lerp(skyLuminance.xxx, skyLight, 0.45);

    float haze = saturate(dist / HorizonHazeDistance);

    color = lerp(color, dustLit, DustStrength * haze * haze);
    color = lerp(color, HorizonColor, haze * haze * haze * haze);

    return float4(color, 1.0);
}

//=====================================================================================================
//The moons: Phobos and Deimos, drawn over the dome (see the header for why this is a separate full-screen
//pass rather than part of the terrain shader above - they stand against the SKY, which MarsTerrainPS never
//touches).
//=====================================================================================================

//One moon: a plain diffuse-lit rock, ray-sphere tested the way Moon.fx's Earth is - a unit sphere at the
//distance that gives the configured angular radius - but with none of the Earth's continents, weather or
//atmosphere rim, because neither Phobos nor Deimos has an atmosphere of its own to put one on. Returns the
//lit colour; `coverage` comes back as the antialiased 0..1 the caller composites by.
float3 MoonDisc(float3 dir, float3 moonDirection, float angularRadius, float3 albedo, float pixelAngle, out float coverage)
{
    coverage = 0.0;

    float cosine = dot(dir, moonDirection);
    float cosLimb = cos(angularRadius);

    [branch]
    if (cosine <= cosLimb || angularRadius <= 0.0) return 0.0;

    //The limb is where cos(angle) crosses cosLimb, antialiased over one pixel's worth of angle
    float edge = max(pixelAngle * sin(angularRadius) * 0.8, 1e-6);
    coverage = smoothstep(cosLimb - edge, cosLimb + edge, cosine);

    float distance = 1.0 / max(sin(angularRadius), 1e-4);
    float discriminant = max(distance * distance * (cosine * cosine - 1.0) + 1.0, 0.0);
    float t = distance * cosine - sqrt(discriminant);
    float3 normal = normalize(t * dir - distance * moonDirection);

    float ndotl = saturate(dot(normal, SunDirection));

    //A small ambient floor, or the dark limb reads as a hole cut in the dome rather than the shadowed side
    //of a small lit rock (the space scene's lesson about the island, restated at a moon's scale).
    return albedo * (ndotl * 1.35 + 0.05);
}

struct MarsMoonsVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
};

MarsMoonsVertexOutput MarsMoonsVS(float3 position : POSITION0)
{
    MarsMoonsVertexOutput output;

    //The quad arrives already in normalized device coordinates; z = w puts it on the far plane, so
    //DepthStencilState.DepthRead passes it wherever the terrain has not already written something nearer
    //(space's trick, and Moon.fx's own reason for drawing its sky after its terrain, not before).
    output.Position = float4(position.xy, 1.0, 1.0);

    float4 far = mul(float4(position.xy, 1.0, 1.0), InverseViewProjection);
    output.Ray = far.xyz / far.w - CameraPosition;

    return output;
}

float4 MarsMoonsPS(MarsMoonsVertexOutput input) : COLOR
{
    float3 dir = normalize(input.Ray);

    //This pixel's angular footprint (Space.fx's Frobenius-norm trick, isotropic in the camera's bearing).
    float pixelAngle = max(sqrt(dot(ddx(dir), ddx(dir)) + dot(ddy(dir), ddy(dir))), 1e-6);

    float deimosCoverage;
    float3 deimos = MoonDisc(dir, DeimosDirection, DeimosAngularRadius, DeimosColor, pixelAngle, deimosCoverage);

    float phobosCoverage;
    float3 phobos = MoonDisc(dir, PhobosDirection, PhobosAngularRadius, PhobosColor, pixelAngle, phobosCoverage);

    //Phobos composited over Deimos - the two never actually overlap at the shipped directions, but the
    //nearer moon should win if a level's own config ever moves them together.
    float3 color = lerp(deimos, phobos, phobosCoverage);
    float coverage = max(deimosCoverage, phobosCoverage);

    //Alpha IS the coverage: unlike Moon.fx's opaque sky pass (which repaints every pixel of a domeless
    //void) this composites over an already-drawn dome and terrain, so everywhere neither disc covers must
    //stay fully transparent.
    return float4(color, coverage);
}

technique MarsTerrain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MarsTerrainVS();
        PixelShader = compile PS_SHADERMODEL MarsTerrainPS();
    }
};

technique MarsMoons
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MarsMoonsVS();
        PixelShader = compile PS_SHADERMODEL MarsMoonsPS();
    }
};
