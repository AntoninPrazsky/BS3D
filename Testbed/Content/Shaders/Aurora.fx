//Draws the aurora scene (#205): a night sky over forested land, a strong, clearly pulsing coloured aurora
//overhead with the stars still showing through and around it. The eighteenth SceneKind, and — like the
//Moon (#125) before it — the second to belong to BOTH scene families at once: real ground
//(SceneRenderer.IsSolidTerrainScene, a forest clearing rising into wooded hills exactly like Forest.fx's)
//under a REPLACED sky (SceneRenderer.ReplacesSky — no dome, the shared star lattice, its own light rig).
//Two techniques over one effect, drawn in that order by DrawAurora: AuroraTerrain first (depth-writing,
//opaque), then AuroraSky depth-READ against it on the shared space quad — the Moon's own measured order
//(the opposite interleave was an 8x frame blow-up there; see DrawMoon's doc). Drawn in all three
//executables, Shader Model 5.0, no OPENGL branch.
//
//AuroraTerrain is Forest.fx's REDUCED floor (ForestFloor's `detail=false` path) ported verbatim rather than
//shared: the two expensive extras that path already gives up (the triplanar normal variation, the
//procedural tree shadows) buy the least where the eye can see the least of them, and a night scene lit
//mostly by its own dim, coloured glow is exactly that case. What is NOT ported is the sun and the cloud
//shadow: this scene draws no dome and no clouds, so SunColor/ZenithColor/HorizonColor are pushed every
//frame from the aurora's OWN pulsing glow (SceneRenderer.AuroraGlowColor) rather than from SceneFrame, on
//the same reasoning TryGetLightRig's doc states for every sky-replacing scene's ground: a dome-derived sun
//painted onto a domeless scene would be a lie. The scattered wood standing on this floor is the Game's own
//instanced draw (a SECOND ForestScatterRenderer planting, independent of the daytime forest's — see
//AuroraSceneConfig's class doc), over TerrainMirror.Forest, the same generic CPU mirror the
//daytime forest already shares with its own planting — keep TerrainHeight below in one change with it.
//
//AuroraSky is Moon.fx's sky pass verbatim (the quad, the ray, the three-layer star lattice, Stars.fxh's one
//C# push routine reused a third time) with the Earth's compositing replaced by the aurora ribbons: soft
//vertical curtains folded out of Fbm2Combed (Noise.fxh) — stretched ALONG the curtain so the field reads as
//streaks rather than blobs, the reason that helper exists — banded low-green to high-violet by elevation,
//pulsing in brightness on two summed clocks so the breathing never quite repeats. Drawn ADDITIVE over the
//stars, never composited over them the way the Moon's opaque Earth disc is: a glow adds light to what is
//behind it and does not hide it, the storm's lightning channel's own reasoning, and the literal answer to
//the issue's "stars partially visible through/alongside the aurora".

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
//For WindGust — the wind still combs the undergrowth here, the same field the meadow, the savanna and the
//daytime forest read it off — and for Fbm2Combed, the aurora curtains' own grain.
#include "Noise.fxh"
#include "Stars.fxh"

float4x4 View;
float4x4 Projection;
float4x4 InverseViewProjection;
float3 CameraPosition;

float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin — see Forest.fx's own
//uniform of the same name for why. The Testbed and the Game set this to the island's radius; the map
//editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

//--- Terrain: Forest.fx's clearing-and-hills shape, ported verbatim (see the header) -------------------

float ForestLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;
float FloorLumpStrength;
float FloorLumpFrequency;

float AuroraTerrainTime;
float2 WindDirection;

float3 ForestColor;
float3 ForestColorDark;
float AmbientStrength;
float HorizonHazeDistance;

float3 TreelineColor;
float TreelineStrength;

float WindRippleSpeed;
float WindRippleFrequency;
float WindRippleStrength;

float NeedleReliefStrength;
float NeedleReliefFrequency;

//Pushed every frame by DrawAurora from SceneRenderer.AuroraGlowColor(wallClock) — the one clock this scene
//shares between the ground, the trees (ForestScatterRenderer.ApplySkyTint) and, independently, the sky's
//own richer in-shader animation. Reused as the terrain's "sun" and "sky hemisphere" the way Forest.fx's
//SunColor/ZenithColor/HorizonColor already are, so ForestFloor's body ports without renaming a single one
//of them — only where their VALUES come from changes. SunDirection stays fixed, straight up: the aurora is
//overhead, not off at a dome's sun angle.
float3 SunColor;
float3 SunDirection;
float3 ZenithColor;
float3 HorizonColor;

#include "ForestGround.fxh"

struct AuroraVertexInput
{
    float4 Position : POSITION0;
};

struct AuroraVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

AuroraVertexOutput AuroraTerrainVS(AuroraVertexInput input)
{
    AuroraVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float3 worldPosition = float3(xz.x, TerrainHeight(xz), xz.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//Forest.fx's ForestFbm as it stood before #281 replaced it there with Fbm2BandLimited/Fbm2Combed: four
//band-limited octaves of the cloud field's gradient noise, amplitudes summing to 1. Kept here because the
//aurora's colour patches and litter were tuned against it and neither reads as the sines' stripes did.
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

//Forest.fx's ForestFloor, its `detail=false` (ForestReducedPS) path inlined rather than parametrised: this
//scene never offers the triplanar normal variation or the procedural tree shadows, so there is only ever
//one program to compile. Everything else — the colour patches, the litter, the grain, the treeline, the
//wind, the two-stage haze — ports untouched; only where SunColor/ZenithColor/HorizonColor and `sunlight`
//come from differs (see the header and the uniform block above).
float4 AuroraFloor(AuroraVertexOutput input)
{
    float3 worldPosition = input.WorldPosition;

    clip(length(worldPosition.xz) - IslandHoleRadius);

    float3 baseNormal = TerrainNormal(worldPosition.xz);
    float footprint = length(fwidth(worldPosition.xz));

    float relief = NeedleRelief(worldPosition.xz, footprint);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    float patch = saturate(ForestFbm(worldPosition.xz * 0.15, footprint) * 1.4 + 0.5);

    float slope = smoothstep(0.55, 0.85, baseNormal.y);
    float3 floorColor = lerp(ForestColorDark * 0.8, ForestColor, patch * slope);

    float wind = WindGust(worldPosition.xz, WindDirection, AuroraTerrainTime, WindRippleFrequency,
        WindRippleSpeed / max(WindRippleFrequency, 1e-4), footprint);
    floorColor *= 1.0 + wind * WindRippleStrength;

    float litter = saturate(ForestFbm(worldPosition.xz * 0.6 + 47.0, footprint) * 1.6 + 0.5);
    floorColor = lerp(floorColor, ForestColorDark * 0.7, litter * litter * 0.5);

    float grainFade = saturate(1.0 - footprint * 2.4);
    float grain = CloudNoise(worldPosition.xz * 2.6) * 1.8;
    floorColor *= 1.0 + 0.22 * grain * grainFade;

    float grove = saturate(CloudNoise(worldPosition.xz * 0.035 + 11.0) * 1.7 + 0.5);
    float crowns = saturate(CloudNoise(worldPosition.xz * 0.17 + 73.0) * 1.7 + 0.5);
    float treeDist = length(worldPosition.xz) + (grove - 0.5) * 40.0;
    float canopyRamp = smoothstep(ClearingRadius * 0.75, ClearingRadius + ClearingTransition * 0.45, treeDist);
    float crownTops = saturate(CloudNoise(worldPosition.xz * 0.45 + 31.0) * 1.7 + 0.5);
    float crownFade = saturate(1.0 - footprint * 0.45);
    float3 canopy = TreelineColor * (0.4 + 1.2 * grove) * (0.55 + 0.9 * crowns)
        * (1.0 - (0.25 - 0.5 * crownTops) * crownFade);
    floorColor = lerp(floorColor, canopy, canopyRamp * TreelineStrength);

    //No dome, no cloud deck: full "sunlight" always, and no tree shadow (the procedural one is the extra
    //this scene never compiles, and the wood's own real trees cast none on the terrain shader either way).
    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = floorColor * (skyAmbient * AmbientStrength + SunColor * ndotl);

    float dist = distance(CameraPosition, worldPosition);
    float haze = saturate(dist / HorizonHazeDistance);
    float3 murk = (ZenithColor * 0.7 + HorizonColor * 0.3) * float3(0.5, 0.65, 0.58);
    color = lerp(color, murk, saturate(haze * haze * 1.2) * 0.6);
    float skyward = haze * haze;
    skyward *= skyward;
    color = lerp(color, HorizonColor, skyward);

    return float4(color, 1.0);
}

float4 AuroraTerrainPS(AuroraVertexOutput input) : COLOR { return AuroraFloor(input); }

//--- Sky: Moon.fx's sky pass verbatim, the Earth replaced by the aurora ribbons (see the header) --------

//SupersampleFactor is declared by Stars.fxh (included above) - one copy shared with Space.fx and Moon.fx.
float AuroraSkyTime;
float3 VoidColor;

float3 AuroraColorLow;
float3 AuroraColorHigh;
float AuroraIntensity;
float AuroraBandHeight;
float AuroraBandSoftness;
float AuroraCurtainScale;
float AuroraCurtainWarp;
float AuroraDriftSpeed;
float AuroraMorphSpeed;
float AuroraPulseSpeed;
float AuroraPulseDepth;

//The slow hue drift (#462), pushed per frame by DrawAurora from SceneRenderer.AuroraHueShift: added to the
//low-to-high colour ramp inside the band, so the whole curtain leans greener or more violet over minutes.
//The same number the ground's light and the island's light rig are coloured from, which is the point of it
//being pushed rather than re-derived here - until #462 only the CPU side had a drift at all, and the ground
//went violet under a sky that never did.
float AuroraHueShift;

//The rays (#462): the fine vertical striation every photograph and every reference rendered for the pass
//shows INSIDE a curtain - the folds alone read as soft smoke. RayScale is their frequency across the sky,
//RayStrength how deep they cut the curtain's brightness. See Aurora().
float AuroraRayScale;
float AuroraRayStrength;

//The aurora ribbons: a band across the upper sky (elevation measured from the horizon, so BandHeight reads
//the way its own doc states it — "how much of the upper sky"), folded into curtains by 3D noise on the
//view DIRECTION itself, stretched vertically so the field reads as long thin streaks rather than blobs —
//sharpened with a power curve so the folds read as ribbons rather than a wash, coloured low-to-high by
//elevation WITHIN the band, and pulsing on two summed clocks so the breathing is a breath and not a
//metronome (the issue's own "strong, clearly pulsing"). Additive: the caller adds this to the starfield
//rather than compositing over it, so the stars always show through at least a little.
//
//⚠ It was Fbm2Combed on an unwrapped AZIMUTH (atan2(dir.x, dir.z)) once, and that is the wrong shape of
//coordinate for anything drawn on a sphere the camera orbits: atan2 has exactly one line where it jumps
//from +pi to -pi, and feeding that jump straight into a noise function's input makes the noise jump too —
//not a moving seam the camera carries with it, but one fixed in the WORLD, at dir.x = 0, dir.z < 0, that
//the camera swept past and photographed (reported after #205 first shipped: "a distinct vertical seam,
//the same kind of problem other scenes solved" — the star lattice's own cube chart is exactly that kind of
//fix, seamless by construction rather than patched after the fact). The angle itself is never computed
//now: DRIFT rotates the sample DIRECTION about the vertical axis instead of adding to an azimuth value (a
//rotated vector has nothing to wrap), and the STREAKS are 3D noise on that rotated direction with its own
//vertical axis compressed by CurtainWarp — Fbm2Combed's stretch-along-an-axis idea, carried into 3D so
//there is no 2D chart anywhere underneath it to seam on.
float3 Aurora(float3 dir, float time, float pixelAngle)
{
    float elevation = saturate(dir.y);
    float bandLow = 1.0 - AuroraBandHeight - AuroraBandSoftness;
    float bandHigh = 1.0 - AuroraBandHeight;

    float band = smoothstep(bandLow, bandHigh, elevation) * (1.0 - smoothstep(0.90, 1.0, elevation));

    [branch]
    if (band <= 0.001) return 0.0;

    float sinDrift, cosDrift;
    sincos(time * AuroraDriftSpeed, sinDrift, cosDrift);
    float3 rotated = float3(
        dir.x * cosDrift - dir.z * sinDrift,
        dir.y,
        dir.x * sinDrift + dir.z * cosDrift);

    //CurtainWarp compresses the vertical axis of the noise domain, so a fold changes little as elevation
    //rises and reads as a long streak rather than a blob — high warp, long ribbons; low warp (floored well
    //short of zero, or the division blows up), closer to a flat mottle.
    //
    //MorphSpeed slides the domain along that same compressed axis (#462): the folds change shape IN PLACE
    //rather than only being carried round the sky by the drift, which is what a real curtain does — it
    //ripples and re-forms, it does not rotate like a carousel. Along the long axis on purpose: a slide
    //there changes a streak slowly, where the same speed across it would sweep folds sideways.
    float3 comb = float3(rotated.x, rotated.y / max(AuroraCurtainWarp, 0.15), rotated.z) * AuroraCurtainScale;
    comb.y += time * AuroraMorphSpeed;
    float streaks = Fbm3(comb, 4);
    float curtain = pow(saturate(streaks * 0.6 + 0.55), 2.4);

    //The rays (#462): one octave of the same 3D noise on the same rotated direction, at a far higher
    //frequency across the sky and stretched a further RAY_STRETCH along its long axis, so it is a comb of
    //thin vertical striations rather than a finer mottle. They cut INTO the curtain's brightness rather than
    //adding to it, so the folds keep their shape and gain structure inside it. Sliding along the long axis a
    //few times faster than the folds re-form, which is the slow shimmer real rays have. Faded out as a ray
    //approaches the pixel's own size - a comb finer than the pixels would alias into crawling moire.
    const float RAY_STRETCH = 7.0;
    float3 rayDomain = float3(rotated.x, rotated.y / (max(AuroraCurtainWarp, 0.15) * RAY_STRETCH), rotated.z)
        * AuroraRayScale;
    rayDomain.y += time * AuroraMorphSpeed * 4.0;
    float rays = saturate(0.5 + 1.4 * GradientNoise3(rayDomain));
    float rayFade = saturate(1.5 - pixelAngle * AuroraRayScale * 3.0);
    curtain *= lerp(1.0, 0.3 + 1.0 * rays, AuroraRayStrength * rayFade);

    //Two clocks summed rather than one, so the envelope never repeats on a plain, watchable period — the
    //slower is 31/100 of the primary rather than a round fraction, for the same reason.
    float pulse = 1.0 - AuroraPulseDepth * (0.5 + 0.5 * sin(time * AuroraPulseSpeed))
                       * (0.7 + 0.3 * sin(time * AuroraPulseSpeed * 0.31 + 1.7));

    float withinBand = saturate((elevation - bandLow) / max(AuroraBandHeight, 1e-4) + AuroraHueShift);
    float3 colour = lerp(AuroraColorLow, AuroraColorHigh, withinBand);

    return colour * (AuroraIntensity * band * curtain * pulse);
}

struct AuroraSkyVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
};

AuroraSkyVertexOutput AuroraSkyVS(float3 position : POSITION0)
{
    AuroraSkyVertexOutput output;

    //Depth-READ after the terrain (see the header) — z = w puts the quad on the far plane, which is what
    //makes that test cheap and correct: LessEqual passes against the untouched far-plane clear and fails
    //against anything the terrain wrote.
    output.Position = float4(position.xy, 1.0, 1.0);

    float4 far = mul(float4(position.xy, 1.0, 1.0), InverseViewProjection);
    output.Ray = far.xyz / far.w - CameraPosition;

    return output;
}

float4 AuroraSkyPS(AuroraSkyVertexOutput input) : COLOR
{
    float3 dir = normalize(input.Ray);
    float pixelAngle = max(sqrt(dot(ddx(dir), ddx(dir)) + dot(ddy(dir), ddy(dir))), 1e-6);

    float3 sky = VoidColor;

    sky += StarLayer(dir, pixelAngle, StarCellScale[0], StarChance[0], StarPeak[0], true);
    sky += StarLayer(dir, pixelAngle, StarCellScale[1], StarChance[1], StarPeak[1], false);
    sky += StarLayer(dir, pixelAngle, StarCellScale[2], StarChance[2], StarPeak[2], false);

    sky *= 1.0 + NoiseHash33(floor(dir / pixelAngle)).x * 0.015;

    //Additive, not composited: the aurora adds light in FRONT of the stars rather than replacing them, so
    //a bright fold never fully erases what is behind it — the issue's own "stars partially visible
    //through/alongside the aurora".
    sky += Aurora(dir, AuroraSkyTime, pixelAngle);

    return float4(sky, 1.0);
}

technique AuroraTerrain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL AuroraTerrainVS();
        PixelShader = compile PS_SHADERMODEL AuroraTerrainPS();
    }
};

technique AuroraSky
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL AuroraSkyVS();
        PixelShader = compile PS_SHADERMODEL AuroraSkyPS();
    }
};

//--- The height probe (#590) ----------------------------------------------------------------------------

//TerrainMirror.Forest's field on the aurora's own terrain, for the Testbed's mirrorcheck (see HeightProbe.fxh).
#define HEIGHT_PROBE_MIRRORED(p) TerrainHeight(p)
#include "HeightProbe.fxh"
