//The aurora (#205, rebuilt in #963): a moonlit snowfield in a boreal clearing under a great folded aurora. Real
//ground under a replaced sky, so the scene is in both families (SceneRenderer.IsSolidTerrainScene and ReplacesSky),
//drawn by AuroraBackdrop in this order:
//
//  AuroraSnowFine / AuroraSnowCoarse - the snowfield, depth-writing, opaque: the fine grid round the camera first, then
//      the coarse one out to the hills, lowered wherever the fine one draws. Neither program calls clip(): the island's
//      hole is cut in the vertex shader, so the depth test runs before the pixel shader (MountainFine's lesson, #781).
//  AuroraStand - the far stand: a few thousand cheap spruces on the hills past the planted wood, instanced.
//  AuroraSky - the night sky, depth-READ at the far plane behind all of it.
//
//The wood standing in the snow is the hosts' ForestScatterRenderer, lit through the shared instanced effect, which
//takes the same mist (SceneFog.fxh) and the same moon shadow (Shadows.fxh) as the snow under it.
//
//THE GROUND is the forest's clearing-and-hills field (ForestGround.fxh, the one copy) plus three things of the snow's
//own: wind drifts (SnowDrift), the bank piled against the island's drum (SnowBank), and the drifts round every trunk,
//log, snag and boulder (the mound map, baked once by AuroraBackdrop from the planting). TerrainMirror.AuroraBase
//mirrors the first three on the CPU term for term; the mound map is the same array on both sides. Keep them in one
//change - mirrorcheck catches a drift.
//
//THE SKY is a gradient from a deep blue zenith to a paler horizon with a glow along it, the stars (Stars.fxh), the
//Moon, and the aurora: a few ARCS, each a vertical sheet hanging along a folded line across the sky. A view ray is
//intersected with each sheet analytically (two refinements of the fold), so every curtain has a sharp, bright lower
//edge at a real altitude, rays that run straight up it, a red top, and folds that brighten where the sheet turns
//edge-on to the eye. AuroraBackdrop knows where the arcs hang (their places and brightness are uniforms it writes),
//which is how the light it throws on the snow and the wood moves with the curtains.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
#include "Noise.fxh"
#include "Stars.fxh"
#include "Shadows.fxh"
#include "SceneFog.fxh"

float4x4 View;
float4x4 Projection;
float4x4 ViewRayBasis;  //the lens's axes over the projection's slopes - see SkyRay.Basis
float3 CameraPosition;
float AuroraTime;

//--- The ground's field --------------------------------------------------------------------------------------------

float2 OriginXZ;          //where this grid is centred this frame
float GridCell;           //its vertex spacing: the base normal's finite difference
float IslandHoleRadius;   //the platform's footprint; 0 in the map editor, which draws no island
float2 FineOriginXZ;      //the fine grid's centre, which the coarse grid sinks under
float FineHalfExtent;
float2 CoarseOriginXZ;    //the coarse grid's centre: the ground ends CoarseReach from it, and fades into the haze before
float CoarseReach;

//ForestGround.fxh's uniforms (the forest's clearing and hills, AuroraSceneConfig.Terrain)
float ForestLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;
float FloorLumpStrength;
float FloorLumpFrequency;
float NeedleReliefFrequency;
float NeedleReliefStrength;

#include "ForestGround.fxh"

//The wind's drifts (AuroraSnowfieldConfig): long low dunes, their crests across the wind
float2 DriftWind;     //unit: the way the wind blows
float DriftHeight;
float DriftAlong;     //the dunes' spacing along the wind
float DriftAcross;    //their length across it

//The bank piled against the island's drum, deeper on the lee side
float BankRadius;
float BankHeight;
float BankReach;
float BankLee;

//The mound map (AuroraBackdrop.BakeMounds): x the snow drifted round the planting, yz that height's slope (exact,
//so the normals are smooth across texels), w how much open sky the snow sees past the crowns over it.
Texture2D MoundMap;
sampler MoundPoint = sampler_state
{
    Texture = <MoundMap>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};
sampler MoundLinear = sampler_state
{
    Texture = <MoundMap>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};
float2 MoundOrigin;   //world XZ of the map's corner
float MoundTexel;     //world units a texel
float MoundSize;      //texels a side

//Long low dunes, their crests across the wind: gradient noise stretched along it, warped so no crest runs straight,
//in patches. ⚠ Mirrored line for line by TerrainMirror.SnowDrift (CloudNoise is ShaderMath.Noise).
float SnowDrift(float2 p)
{
    float2 across = float2(-DriftWind.y, DriftWind.x);
    float2 q = float2(dot(p, DriftWind) / DriftAlong, dot(p, across) / DriftAcross);
    q.x += 1.4 * CloudNoise(p * 0.009 + float2(3.7, 8.1));

    float dune = CloudNoise(q) + 0.45 * CloudNoise(q * float2(2.1, 1.7) + float2(5.2, 1.3));
    float patch = saturate(0.6 + 0.8 * CloudNoise(p * 0.0065 + float2(9.1, 2.4)));

    return DriftHeight * dune * patch;
}

//The snow piled against the island's drum: flat under the platform, falling away over BankReach outside it, deeper on
//the side the wind blows towards. ⚠ Mirrored by TerrainMirror.SnowBank.
float SnowBank(float2 p)
{
    float r = length(p);
    float2 outward = p / max(r, 1e-3);
    float lee = 1.0 + BankLee * dot(outward, DriftWind);
    float rise = exp(-max(r - BankRadius, 0.0) / BankReach);

    return BankHeight * rise * lee;
}

//The ground before the mounds: what the planting stands on (TerrainMirror.AuroraBase)
float SnowBaseHeight(float2 p)
{
    return TerrainHeight(p) + SnowDrift(p) + SnowBank(p);
}

//The mound map's height at p, bilinear by hand over four point taps: the arithmetic TerrainMirror.Aurora repeats on
//the same array, so the CPU and the GPU agree to the rounding (a hardware filter weights in 8-bit steps). Zero off
//the map.
float MoundHeight(float2 p)
{
    float2 t = (p - MoundOrigin) / MoundTexel - 0.5;
    float2 cell = floor(t);
    float2 f = t - cell;
    float onMap = step(0.0, min(cell.x, cell.y)) * step(max(cell.x, cell.y), MoundSize - 2.0);

    float texel = 1.0 / MoundSize;
    float2 uv = (cell + 0.5) * texel;
    float a = tex2Dlod(MoundPoint, float4(uv, 0.0, 0.0)).x;
    float b = tex2Dlod(MoundPoint, float4(uv + float2(texel, 0.0), 0.0, 0.0)).x;
    float c = tex2Dlod(MoundPoint, float4(uv + float2(0.0, texel), 0.0, 0.0)).x;
    float d = tex2Dlod(MoundPoint, float4(uv + float2(texel, texel), 0.0, 0.0)).x;

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y) * onMap;
}

//The ground as drawn, and as the planting's mirror reads it with the mounds (SceneRenderer.AuroraGroundHeight)
float SnowHeight(float2 p)
{
    return SnowBaseHeight(p) + MoundHeight(p);
}

//--- The snow -------------------------------------------------------------------------------------------------------

float3 MoonDirection;          //towards the Moon: the key light, and the sun of the shadow map
float3 MoonLight;              //on a face square to it (linear)
float3 AuroraLightDirection;   //towards the curtains' light this frame
float3 AuroraLight;            //what they throw on a face square to them this frame, the pulse included
float3 SkyLightZenith;         //the night sky's own light from overhead (linear)
float3 SkyLightHorizon;        //and from the horizon, its glow and the snow's own bounce
float3 SnowAlbedo;
float3 SnowDeepTint;           //light that went into the pack and came out of the shade: the blue of shadowed snow
float SastrugiStrength;
float SastrugiScale;
float SparkleDensity;
float SparkleStrength;

struct SnowVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 Normal : TEXCOORD1;
};

//One vertex program for both grids. The base field's normal is taken here over one cell (its finest features are tens
//of units), the mounds' slope is the map's own in the pixel shader, and the relief finer than a cell is per pixel.
SnowVertexOutput SnowVS(float4 position : POSITION0, uniform bool coarse)
{
    SnowVertexOutput output;

    float2 xz = position.xz + OriginXZ;
    float base = SnowBaseHeight(xz);
    float h = base + MoundHeight(xz);

    float e = GridCell;
    float hx = SnowBaseHeight(xz + float2(e, 0.0));
    float hz = SnowBaseHeight(xz + float2(0.0, e));
    output.Normal = normalize(float3(-(hx - base) / e, 1.0, -(hz - base) / e));

    //The coarse grid sinks wherever the fine one draws, all the way in from two of its own cells inside the fine
    //grid's edge: the fine surface covers it there, and a pixel the depth test rejects costs nothing. Both programs
    //shade a point alike, so where the two overlap at the seam it does not matter which wins.
    if (coarse)
    {
        float2 offset = abs(xz - FineOriginXZ);
        float inside = 1.0 - smoothstep(FineHalfExtent - 2.0 * GridCell, FineHalfExtent, max(offset.x, offset.y));
        h -= 4.0 * inside;
    }

    float3 world = float3(xz.x, h, xz.y);

    //The island's hole, in the vertex shader (MountainFine's InHole): every vertex inside it goes to one point deep
    //under the clearing, so the triangles across the edge fall away as a cone the pit shaft hides and the drum
    //overhangs. Never onto the circle: three corners on it would be a triangle across the hole.
    if (length(xz) < IslandHoleRadius) world = float3(0.0, ForestLevelY - 400.0, 0.0);

    output.WorldPosition = world;
    output.Position = mul(mul(float4(world, 1.0), View), Projection);

    return output;
}

SnowVertexOutput SnowFineVS(float4 position : POSITION0) { return SnowVS(position, false); }
SnowVertexOutput SnowCoarseVS(float4 position : POSITION0) { return SnowVS(position, true); }

//Wind-cut relief finer than a cell: sharp-crested ridges across the wind (sastrugi), a second set half their size, and
//the grain of the crust. Each octave fades out as the pixel stops resolving it.
float Sastrugi(float2 p, float footprint, uniform bool detail)
{
    float2 across = float2(-DriftWind.y, DriftWind.x);
    float2 q = float2(dot(p, DriftWind) * 0.9, dot(p, across) * 0.22) * SastrugiScale;

    float r1 = 1.0 - abs(CloudNoise(q + float2(2.3, 7.7)));
    float ridge = r1 * r1 * saturate(1.0 - footprint * SastrugiScale * 1.2);

    if (detail)
    {
        float r2 = 1.0 - abs(CloudNoise(q * 2.7 + float2(11.1, 4.2)));
        ridge += 0.45 * r2 * r2 * saturate(1.0 - footprint * SastrugiScale * 3.0);
        ridge += 0.12 * CloudNoise(p * 7.3) * saturate(1.0 - footprint * 6.0);
    }

    return SastrugiStrength * ridge;
}

//Glitter: one grain in so many of a lattice of them catches the Moon in its own facet. Each grain's facet is the
//surface's normal tilted its own way, so a glint lights only from where the eye is - it twinkles as the camera
//moves, which is what tells snow from white paint. A glint is a dot about a pixel and a half across at any distance
//and fades where a cell gets smaller than the pixel.
float SnowSparkle(float3 worldPosition, float3 normal, float3 toEye, float footprint)
{
    const float SCALE = 5.0;

    float2 p = worldPosition.xz * SCALE;
    float2 cell = floor(p);
    float2 h = NoiseHash22(cell);
    float lit = step(1.0 - SparkleDensity * 2.0, h.x);

    float2 tilt = frac(h * float2(173.3, 291.7)) * 2.0 - 1.0;
    float3 facet = normalize(normal + float3(tilt.x, 0.0, tilt.y) * 0.3);
    float glint = pow(saturate(dot(facet, normalize(MoonDirection + toEye))), 400.0);

    float2 centre = cell + 0.5 + (frac(h * float2(431.7, 197.3)) - 0.5) * 0.5;
    float radius = clamp(footprint * SCALE * 1.5, 0.12, 0.5);
    float grain = 1.0 - smoothstep(radius * 0.3, radius, distance(p, centre));

    return lit * glint * grain * saturate(1.0 - footprint * SCALE * 0.5);
}

float4 SnowShade(SnowVertexOutput input, uniform bool detail)
{
    float3 worldPosition = input.WorldPosition;
    float footprint = length(fwidth(worldPosition.xz));

    //The mounds' slope, added to the base field's in slope space, and how much sky the crowns leave the snow
    float4 mound = tex2Dlod(MoundLinear, float4((worldPosition.xz - MoundOrigin) / (MoundTexel * MoundSize), 0.0, 0.0));
    float3 baseNormal = normalize(input.Normal);
    float baseY = max(baseNormal.y, 0.2);
    float3 normal = normalize(float3(baseNormal.x / baseY - mound.y, 1.0, baseNormal.z / baseY - mound.z));
    normal = PerturbNormalFromHeight(normal, worldPosition, Sastrugi(worldPosition.xz, footprint, detail));

    float3 toEye = normalize(CameraPosition - worldPosition);
    float openSky = mound.w;

    float moonShadow = 1.0;
    [branch]
    if (ShadowStrength > 0.0)
        moonShadow = SunShadow(worldPosition, normal, MoonDirection);

    //Snow's terminator is soft (light scatters through the top of the pack), and the curtains are a wide light
    float moonFacing = saturate((dot(normal, MoonDirection) + 0.15) / 1.15);
    float auroraFacing = saturate((dot(normal, AuroraLightDirection) + 0.5) / 1.5);
    float3 sky = lerp(SkyLightHorizon, SkyLightZenith, saturate(normal.y * 0.5 + 0.5));

    float moonlit = moonFacing * moonShadow;
    float3 light = MoonLight * moonlit + (AuroraLight * auroraFacing + sky) * openSky;

    //Wind crust and powder: the crust a shade greyer, in patches the size of the drifts
    float crust = saturate(0.5 + 0.9 * CloudNoise(worldPosition.xz * 0.045 + float2(17.0, 3.0)));
    float3 color = SnowAlbedo * lerp(1.0, 0.9, crust) * light;

    //The blue of shaded snow: light that entered the lit pack nearby and came out in the shade
    color += SnowDeepTint * (1.0 - moonlit) * openSky;

    if (detail)
        color += MoonLight * (SparkleStrength * moonShadow) * SnowSparkle(worldPosition, normal, toEye, footprint);

    //Down the drain's throat the hole's cone is black, as the pit shaft round it is
    color *= smoothstep(ForestLevelY - 10.0, ForestLevelY - 6.0, worldPosition.y);

    color = ApplySceneFog(color, CameraPosition, worldPosition);

    //Where the grid ends the ground has gone into the haze the horizon is drawn in, so its edge is never a line
    float2 fromCentre = abs(worldPosition.xz - CoarseOriginXZ);
    color = lerp(color, SceneFogColor, smoothstep(CoarseReach * 0.72, CoarseReach, max(fromCentre.x, fromCentre.y)));

    return float4(color, 1.0);
}

float4 SnowPS(SnowVertexOutput input) : COLOR { return SnowShade(input, true); }
float4 SnowReducedPS(SnowVertexOutput input) : COLOR { return SnowShade(input, false); }

technique AuroraSnowFine
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SnowFineVS();
        PixelShader = compile PS_SHADERMODEL SnowPS();
    }
};

technique AuroraSnowCoarse
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SnowCoarseVS();
        PixelShader = compile PS_SHADERMODEL SnowPS();
    }
};

technique AuroraSnowFineReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SnowFineVS();
        PixelShader = compile PS_SHADERMODEL SnowReducedPS();
    }
};

technique AuroraSnowCoarseReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SnowCoarseVS();
        PixelShader = compile PS_SHADERMODEL SnowReducedPS();
    }
};

//--- The far stand --------------------------------------------------------------------------------------------------

float3 StandNeedles;    //the needles' colour (linear)
float3 StandSnow;       //the snow on the tiers' tops

struct StandVertexInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float Snow : TEXCOORD0;          //how much of a tier's top the snow lies on, per vertex
};

struct StandInstanceInput
{
    float4 Place : TEXCOORD1;        //foot in world, and the scale
    float4 Look : TEXCOORD2;         //yaw (sin, cos), the height's stretch, the needles' tint
};

struct StandVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 Material : TEXCOORD2;     //snow, tint
};

StandVertexOutput StandVS(StandVertexInput input, StandInstanceInput instance)
{
    StandVertexOutput output;

    float s = instance.Look.x, c = instance.Look.y;

    //A spruce past where the ground is drawn would stand on nothing: it goes to a point (no triangle to draw)
    float2 offGround = abs(instance.Place.xz - CoarseOriginXZ);
    float onGround = step(max(offGround.x, offGround.y), CoarseReach - 4.0);
    float3 local = input.Position * float3(1.0, instance.Look.z, 1.0) * (instance.Place.w * onGround);
    float3 world = float3(local.x * c - local.z * s, local.y, local.x * s + local.z * c) + instance.Place.xyz;
    float3 normal = float3(input.Normal.x * c - input.Normal.z * s, input.Normal.y / instance.Look.z, input.Normal.x * s + input.Normal.z * c);

    output.WorldPosition = world;
    output.Normal = normal;
    output.Material = float2(input.Snow, instance.Look.w);
    output.Position = mul(mul(float4(world, 1.0), View), Projection);

    return output;
}

float4 StandPS(StandVertexOutput input) : COLOR
{
    float3 normal = normalize(input.Normal);
    float3 albedo = lerp(StandNeedles * input.Material.y, StandSnow, 0.55 * input.Material.x * saturate(normal.y * 1.6));

    float moonFacing = saturate((dot(normal, MoonDirection) + 0.3) / 1.3);
    float auroraFacing = saturate((dot(normal, AuroraLightDirection) + 0.5) / 1.5);
    float3 sky = lerp(SkyLightHorizon, SkyLightZenith, saturate(normal.y * 0.5 + 0.5));
    float3 color = albedo * (MoonLight * moonFacing + AuroraLight * auroraFacing + sky);

    color = ApplySceneFog(color, CameraPosition, input.WorldPosition);

    float2 fromCentre = abs(input.WorldPosition.xz - CoarseOriginXZ);
    color = lerp(color, SceneFogColor, smoothstep(CoarseReach * 0.72, CoarseReach, max(fromCentre.x, fromCentre.y)));

    return float4(color, 1.0);
}

technique AuroraStand
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL StandVS();
        PixelShader = compile PS_SHADERMODEL StandPS();
    }
};

//--- The sky --------------------------------------------------------------------------------------------------------

float3 SkyZenith;          //the night sky overhead (linear)
float3 SkyHorizon;         //and at the horizon
float SkyGradientPower;    //how fast the one turns into the other going up
float3 HorizonGlow;        //the glow along the horizon: airglow, and the curtains' light scattered low
float HorizonGlowWidth;    //its height, as a share of the hemisphere
float2 AuroraBearing;      //unit XZ: the side of the sky the curtains hang over, where the glow is strongest

float3 MoonDisc;           //the disc's radiance
float MoonRadius;          //its angular radius, radians
float3 MoonHalo;           //the glow round it, through the thin air
float MoonHaloWidth;

#define AURORA_ARCS 3
float4 ArcPlace[AURORA_ARCS];   //x the line's offset across its axis (altitudes, + ahead of AuroraBearing), y its axis's angle, z the folds' depth, w its brightness this frame
float4 ArcShape[AURORA_ARCS];   //x the lower edge's altitude, y the curtain's height, z the folds' frequency, w its own seed
float4 ArcBend[AURORA_ARCS];    //x the long bend's depth, y its frequency, z how often the curtain is lit along its length
float3 AuroraGreen;        //the curtains' body, oxygen at the lower altitudes (linear)
float3 AuroraRed;          //their tops
float3 AuroraFringe;       //the thin magenta of the lowest edge
float AuroraRayScale;      //rays a unit of the arc's length
float AuroraRayStrength;
float AuroraRaySpeed;      //how fast the rays run along the curtain
float AuroraFoldSpeed;     //how fast the folds change shape
float AuroraEdge;          //the lower edge's softness, in the curtain's own height
float AuroraLength;        //how far along its axis an arc reaches before it fades

//A cheap 1D gradient noise for the curtains: no CPU mirror reads it, so it needs nothing but to be smooth
float AuroraHash(float n)
{
    n = frac(n * 0.1031);
    n *= n + 33.33;
    n *= n + n;
    return frac(n) * 2.0 - 1.0;
}

float AuroraNoise(float x)
{
    float i = floor(x);
    float f = x - i;
    float u = f * f * (3.0 - 2.0 * f);
    return lerp(AuroraHash(i) * f, AuroraHash(i + 1.0) * (f - 1.0), u) * 2.0;
}

//An arc's fold at u along its axis: two octaves that re-form in place over time
float AuroraFold(float u, float seed, float time)
{
    return AuroraNoise(u + seed + time * AuroraFoldSpeed)
        + 0.45 * AuroraNoise(u * 2.3 + seed * 1.7 - time * AuroraFoldSpeed * 1.3);
}

//One arc seen along dir. The ray's line across the sky crosses the arc's mean line once; there the curtain hangs off
//that line by its long bend and its folds, so the ray meets the sheet one step further on, and the altitude it meets it
//at says where in the curtain's height the eye looks. ONE step and no iteration: a fixed point iterated through a deep
//fold jumps between its roots from pixel to pixel and drew the lower edge as a saw (the first cut of #963). One step is
//a smooth function of the direction, so the edge is a smooth curve however deep the fold.
float3 AuroraArc(float3 dir, float4 place, float4 shape, float4 bend, float time)
{
    float s, c;
    sincos(place.y, s, c);
    float along = dir.x * c + dir.z * s;
    float across = dir.z * c - dir.x * s;

    //Only a ray heading the arc's way meets it, and one running nearly along it meets it at the horizon
    float heads = step(0.0, across * place.x) * smoothstep(0.02, 0.06, abs(across));
    float safeAcross = (across < 0.0 ? -1.0 : 1.0) * max(abs(across), 0.02);

    float x0 = place.x / safeAcross * along;
    float u = x0 * shape.z;
    float hang = bend.x * AuroraNoise(x0 * bend.y + shape.w * 3.7 + time * AuroraFoldSpeed * 0.3)
        + place.z * AuroraFold(u, shape.w, time);
    float t = max((place.x + hang) / safeAcross, 0.0);

    float x = t * along;
    float y = t * dir.y;

    //Where in the curtain the eye looks: 0 at the lower edge (a gentle ripple along it), 1 at the top
    float edge = shape.x + 0.03 * shape.y * AuroraNoise(x0 * 0.9 + shape.w * 5.0 + time * 0.03);
    float v = (y - edge) / shape.y;
    float bottom = smoothstep(-AuroraEdge, 0.0, v);
    float body = exp(-2.6 * max(v, 0.0)) * (1.0 - smoothstep(0.7, 1.0, v));

    //And under the edge a faint glow, the curtain's light scattered in the air below it: without it the dark sky under
    //a bright edge reads as a dark range of mountains cut out against the curtain
    body += 0.35 * exp(min(v, 0.0) / 0.5) * (1.0 - bottom);

    //The slant: the bend and the folds turn the sheet, and a sheet seen edge-on is looked through for longer
    const float DELTA = 0.02;
    float hangAhead = bend.x * AuroraNoise((x0 + DELTA) * bend.y + shape.w * 3.7 + time * AuroraFoldSpeed * 0.3)
        + place.z * AuroraFold((x0 + DELTA) * shape.z, shape.w, time);
    float slope = (hangAhead - hang) / DELTA;
    float2 sheet = normalize(float2(-slope, 1.0));
    float2 heading = normalize(float2(along, across));
    float slant = min(1.0 / max(abs(dot(heading, sheet)), 0.2), 3.5);

    //The rays: brightness running straight up the sheet, varying only along it at three widths, shimmering along it;
    //faded where they would be finer than the pixel
    float rayFade = saturate(1.5 - fwidth(x) * AuroraRayScale * 2.5);
    float rays = saturate(0.4 + 0.55 * AuroraNoise(x * AuroraRayScale * 0.45 + shape.w * 1.9 + time * AuroraRaySpeed * 0.6)
        + 0.45 * AuroraNoise(x * AuroraRayScale + shape.w * 7.1 + time * AuroraRaySpeed)
        + 0.3 * AuroraNoise(x * AuroraRayScale * 2.7 + shape.w * 3.3 - time * AuroraRaySpeed * 1.7));
    rays = lerp(1.0, rays * rays * 2.2, AuroraRayStrength * rayFade);

    //The curtain is lit in stretches, not along its whole length: a bright fold here, a dim one there, gaps between -
    //what makes a display of distinct curtains and not one even fence across the sky
    float stretch = smoothstep(-0.25, 0.6, AuroraNoise(x0 * bend.z + shape.w * 2.3 + time * 0.012)
        + 0.5 * AuroraNoise(x0 * bend.z * 2.6 + shape.w * 9.7 - time * 0.02));
    rays *= 0.04 + 0.96 * stretch;

    //Out to its ends along the axis, and dimmer far away and low, where it is seen through more air
    float reach = 1.0 - smoothstep(AuroraLength * 0.55, AuroraLength, abs(x));
    float air = exp(-t * 0.05) * saturate(dir.y * 9.0);

    float3 colour = lerp(AuroraGreen, AuroraRed, smoothstep(0.3, 0.9, v))
        + AuroraFringe * exp(-max(v, 0.0) * 25.0);

    return colour * (place.w * heads * bottom * body * slant * rays * reach * air);
}

struct AuroraSkyVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
};

AuroraSkyVertexOutput AuroraSkyVS(float3 position : POSITION0)
{
    AuroraSkyVertexOutput output;

    //On the far plane, depth-READ after the ground: LessEqual passes against the untouched clear only
    output.Position = float4(position.xy, 1.0, 1.0);
    output.Ray = mul(float4(position.xy, 1.0, 0.0), ViewRayBasis).xyz;

    return output;
}

float4 AuroraSkyPS(AuroraSkyVertexOutput input) : COLOR
{
    float3 dir = normalize(input.Ray);
    float pixelAngle = max(sqrt(dot(ddx(dir), ddx(dir)) + dot(ddy(dir), ddy(dir))), 1e-6);
    float elevation = dir.y;
    float up = saturate(elevation);

    float3 sky = lerp(SkyHorizon, SkyZenith, pow(up, SkyGradientPower));

    //The glow along the horizon, strongest under the curtains
    float2 flat = normalize(dir.xz + float2(1e-5, 0.0));
    float towards = saturate(dot(flat, AuroraBearing) * 0.5 + 0.5);
    sky += HorizonGlow * exp(-max(elevation, 0.0) / HorizonGlowWidth) * (0.45 + 0.9 * towards * towards);

    //The Moon and the air lit round it
    float toMoon = dot(dir, MoonDirection);
    float angle = acos(clamp(toMoon, -1.0, 1.0));
    sky += MoonHalo * exp(-angle / MoonHaloWidth);
    float disc = 1.0 - smoothstep(MoonRadius - pixelAngle, MoonRadius + pixelAngle, angle);
    float limb = sqrt(saturate(1.0 - angle * angle / (MoonRadius * MoonRadius)));
    float3 moonRight = normalize(cross(float3(0.0, 1.0, 0.0), MoonDirection));
    float2 face = float2(dot(dir, moonRight), dir.y - MoonDirection.y) / MoonRadius;
    float maria = 0.82 + 0.18 * CloudNoise(face * 2.2 + 3.0);
    float3 moon = MoonDisc * disc * (0.75 + 0.25 * limb) * maria;

    //The stars, thinned near the horizon and in the Moon's glare, and hidden behind its disc
    float starsSeen = saturate(elevation * 10.0) * (1.0 - disc) * saturate(angle / (MoonRadius * 12.0));
    float3 stars = StarLayer(dir, pixelAngle, StarCellScale[0], StarChance[0], StarPeak[0], true)
        + StarLayer(dir, pixelAngle, StarCellScale[1], StarChance[1], StarPeak[1], false)
        + StarLayer(dir, pixelAngle, StarCellScale[2], StarChance[2], StarPeak[2], false);
    sky += stars * starsSeen;

    //The curtains, added in front of the stars (a glow hides nothing behind it), behind the Moon
    float3 aurora = 0.0;
    [unroll]
    for (int k = 0; k < AURORA_ARCS; k++)
        aurora += AuroraArc(dir, ArcPlace[k], ArcShape[k], ArcBend[k], AuroraTime);
    sky += aurora * (1.0 - disc);
    sky = max(sky, moon);

    //The mist at the horizon, so the hills fade into the sky and not into a line under it
    [branch]
    if (SceneFogDensity > 0.0)
        sky = lerp(sky, SceneFogColor, saturate(1.0 - max(elevation, 0.0) / 0.035) * 0.85);

    return float4(sky, 1.0);
}

technique AuroraSky
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL AuroraSkyVS();
        PixelShader = compile PS_SHADERMODEL AuroraSkyPS();
    }
};

//--- The height probe (#590) ----------------------------------------------------------------------------------------

//TerrainMirror.Aurora's field with the mound map, for the Testbed's mirrorcheck (see HeightProbe.fxh)
#define HEIGHT_PROBE_MIRRORED(p) SnowHeight(p)
#include "HeightProbe.fxh"
