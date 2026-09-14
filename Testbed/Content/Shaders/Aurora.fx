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
//AuroraSceneConfig's class doc), over SceneRenderer.ForestTerrainHeight, the same generic CPU mirror the
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

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);

    return frac(p.x * p.y);
}

//Identical to Forest.fx's TerrainHeight — see there for why each term exists. Kept in ONE change with it
//AND with SceneRenderer.ForestTerrainHeight, which both this shader's ground and the daytime forest's own
//already share as their one CPU mirror.
float TerrainHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float2 q = p + 26.0 * float2(sin(p.y * 0.011 + 2.0), sin(p.x * 0.013 + 5.0));

    float rolling = 0.40 * sin(dot(q, float2(0.020, 0.015)))
        + 0.26 * sin(dot(q, float2(-0.013, 0.024)) + 1.5)
        + 0.17 * sin(dot(q, float2(0.031, 0.026)) + 3.0)
        + 0.10 * sin(dot(q, float2(0.056, -0.041)) + 0.7)
        + 0.07 * sin(dot(q, float2(-0.083, 0.062)) + 2.4);

    float basin = ClearingRelief * sin(dot(p, float2(0.05, 0.035)));

    float mask = 0.55 + 0.45 * sin(dot(p, float2(0.021, -0.017)) + 4.0);
    float lumps = sin(dot(p, float2(FloorLumpFrequency, FloorLumpFrequency * 0.7)))
        + 0.5 * sin(dot(p, float2(-FloorLumpFrequency * 0.8, FloorLumpFrequency * 1.1)) + 2.0)
        + 0.35 * sin(dot(p, float2(FloorLumpFrequency * 1.9, FloorLumpFrequency * 1.4)) + 5.1);
    float lumpHeight = FloorLumpStrength * lumps * mask * (1.0 - ramp * 0.5);

    return ForestLevelY + basin + lumpHeight + HillHeight * ramp * (rolling * 0.5 + 0.5);
}

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

//Identical to Forest.fx's NeedleRelief — see there. Static (no ForestTime drift): #276's own "a forest
//floor does not drift" rule applies just as much at night.
float NeedleRelief(float2 xz, float footprint)
{
    float2 p = xz;
    float f = NeedleReliefFrequency;

    float h = 0.45 * sin(dot(p, normalize(float2(0.9, 0.3))) * f) * saturate(1.0 - footprint * f / 3.14159265)
        + 0.28 * sin(dot(p, normalize(float2(-0.4, 1.0))) * f * 1.83) * saturate(1.0 - footprint * f * 1.83 / 3.14159265)
        + 0.17 * sin(dot(p, normalize(float2(0.2, -1.0))) * f * 3.1) * saturate(1.0 - footprint * f * 3.1 / 3.14159265)
        + 0.10 * sin(dot(p, normalize(float2(-1.0, -0.35))) * f * 5.7) * saturate(1.0 - footprint * f * 5.7 / 3.14159265);

    return h * NeedleReliefStrength;
}

//Identical to Forest.fx's TerrainNormal — per-pixel from the height field's own gradient (the savanna's
//fix): interpolating the coarse grid's per-vertex normal leaves a Mach band at every cell edge.
float3 TerrainNormal(float2 p)
{
    float e = 1.2;
    float h = TerrainHeight(p);
    float hx = TerrainHeight(p + float2(e, 0.0));
    float hz = TerrainHeight(p + float2(0.0, e));

    return normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));
}

//Identical to Forest.fx's ForestFbm — four band-limited octaves of the shared gradient noise, amplitudes
//summing to 1. See there for why four and not one.
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
float AuroraPulseSpeed;
float AuroraPulseDepth;

//The aurora ribbons: a band across the upper sky (elevation measured from the horizon, so BandHeight reads
//the way its own doc states it — "how much of the upper sky"), folded into curtains by Fbm2Combed stretched
//ALONG the curtain — that helper's own reason to exist, so the field reads as long thin streaks rather than
//blobs — sharpened with a power curve so the folds read as ribbons rather than a wash, coloured low-to-high
//by elevation WITHIN the band, and pulsing on two summed clocks so the breathing is a breath and not a
//metronome (the issue's own "strong, clearly pulsing"). Additive: the caller adds this to the starfield
//rather than compositing over it, so the stars always show through at least a little.
float3 Aurora(float3 dir, float time)
{
    float elevation = saturate(dir.y);
    float bandLow = 1.0 - AuroraBandHeight - AuroraBandSoftness;
    float bandHigh = 1.0 - AuroraBandHeight;

    float band = smoothstep(bandLow, bandHigh, elevation) * (1.0 - smoothstep(0.90, 1.0, elevation));

    [branch]
    if (band <= 0.001) return 0.0;

    float azimuth = atan2(dir.x, dir.z) * AuroraCurtainScale + time * AuroraDriftSpeed;
    float streaks = Fbm2Combed(float2(azimuth, elevation * 2.2), float2(0.0, 1.0), 4.0, 4, 0.0);
    float curtain = pow(saturate(streaks * AuroraCurtainWarp * 0.5 + 0.55), 2.4);

    //Two clocks summed rather than one, so the envelope never repeats on a plain, watchable period — the
    //slower is 31/100 of the primary rather than a round fraction, for the same reason.
    float pulse = 1.0 - AuroraPulseDepth * (0.5 + 0.5 * sin(time * AuroraPulseSpeed))
                       * (0.7 + 0.3 * sin(time * AuroraPulseSpeed * 0.31 + 1.7));

    float withinBand = saturate((elevation - bandLow) / max(AuroraBandHeight, 1e-4));
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
    sky += Aurora(dir, AuroraSkyTime);

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
