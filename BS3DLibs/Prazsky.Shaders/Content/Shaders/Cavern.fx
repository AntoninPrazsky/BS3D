//Draws the cavern (#958): an irregular limestone hall over a still black lake. Every surface is real geometry built
//once on the CPU (CavernChamber, CavernGeometry): the shell of the vault and walls and the lake's bed in one mesh, the
//stalactites, stalagmites and columns in another, the crystals, the glowworms and the light shafts. This file shades
//them; nothing here decides where anything stands.
//
//The lake is not a mesh. Every surface under the waterline shades itself as SEEN THROUGH the water: the view ray
//crosses the surface on its way down, so the pixel is the surface's reflection (the hall drawn mirrored into a target
//of its own, sampled at the pixel's own screen position, bent by the ripple) over what the water lets through of the
//bed. No water plane, so nothing to fight the bed for depth at a shoreline, and the shore is continuous by construction:
//at the waterline the bed's own colour and its reflection meet.
//
//Linear radiance in, one tonemap out (docs/rendering.md): the palette and the lights are the grade. The crystals and
//the glowworms are allowed over the glare threshold deliberately; the rock stays far under it.
//
//Built by all three executables out of this directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

float4x4 ViewProjection;
float3 CameraPosition;
float CavernTime;
float2 ViewportSize;        //the bound target's size in pixels: the screen position the reflection is read at, the worms' size

//--- The rock -------------------------------------------------------------------------------------------------------
float3 RockColor;
float3 CalciteColor;
float3 FogColor;
float FogDensity;
float3 AmbientTop;          //the dim cool fill from the vault (linear)

//--- The lake -------------------------------------------------------------------------------------------------------
float WaterLevelY;
float3 WaterDeepColor;
float WaterAbsorption;
float3 WaterGlowColor;
float RippleStrength;
float RippleScale;
float RippleSpeed;
float3 MistColor;
float MistDensity;
float MistHeight;
float ReflectionAvailable;  //1 when the mirrored hall was drawn this frame; 0 reads a dark ramp instead

Texture2D Reflection;
sampler ReflectionSampler = sampler_state
{
    Texture = <Reflection>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//--- The lights: every crystal cluster is a point light, its colour carrying this frame's breath -----------------------
#define MAX_LIGHTS 12
float4 LightPosition[MAX_LIGHTS];   //xyz, w = 1 / range^2
float4 LightColor[MAX_LIGHTS];      //rgb, linear radiance at the light's own falloff scale
int LightCount;
static const float LIGHT_NEAR = 15.0;   //world units: the distance at which a light's falloff halves

//--- The shafts --------------------------------------------------------------------------------------------------------
#define SHAFT_COUNT 2
float4 ShaftSource[SHAFT_COUNT];        //xyz the crack, w the beam's radius there
float4 ShaftDirection[SHAFT_COUNT];     //xyz the way it falls, w its length to the lake
float3 ShaftColor;
float ShaftStrength;

//--- The crystals and the worms -----------------------------------------------------------------------------------------
float3 CrystalColorA;
float3 CrystalColorB;
float CrystalEmission;
float CrystalPulseSpeed;
float3 GlowwormColor;
float GlowwormPixels;       //a worm's radius on screen, in pixels: the same at every distance (#507's rule)

static const float PI = 3.14159265;
static const float VAULT_GLOW = 0.024;  //the vault's share of the glowworms' light, over their colour

//===================================================================================================================
//THE AIR: the hall's fog and the lake's steam
//===================================================================================================================

//Optical thickness of the steam along the first t units of a ray: the classic exponential height fog, closed form. Height
//fog rather than a slab, because a slab has a top and its top is a hard line of its own.
float MistAmount(float3 origin, float3 direction, float t)
{
    float h0 = max(origin.y - WaterLevelY, 0.0) / MistHeight;
    float dh = direction.y * t / MistHeight;
    float column = t * exp(-h0) * (abs(dh) > 1e-4 ? (1.0 - exp(-dh)) / dh : 1.0);
    return 1.0 - exp(-column * MistDensity);
}

//What the air does to a surface's colour on its way to the lens: the hall's distance fog, then the steam
float3 Atmosphere(float3 colour, float3 worldPosition, float3 eye)
{
    float3 toPoint = worldPosition - eye;
    float distanceTravelled = length(toPoint);
    float3 direction = toPoint / max(distanceTravelled, 1e-3);

    float fog = 1.0 - exp(-distanceTravelled * FogDensity);
    colour = lerp(colour, FogColor, fog);
    return lerp(colour, MistColor, MistAmount(eye, direction, distanceTravelled));
}

//===================================================================================================================
//THE LIGHT ON STONE
//===================================================================================================================

//Where a shaft's light lands on a surface: a soft disc round the beam's axis, brightest where the surface faces the light
float ShaftPool(float3 p, float3 normal)
{
    float pool = 0.0;

    [unroll]
    for (int s = 0; s < SHAFT_COUNT; s++)
    {
        float3 source = ShaftSource[s].xyz;
        float3 direction = ShaftDirection[s].xyz;
        float along = dot(p - source, direction);
        float t = saturate(along / ShaftDirection[s].w);
        float radius = ShaftSource[s].w * (1.0 + 0.5 * t);
        float3 offset = p - (source + direction * along);
        float disc = exp(-dot(offset, offset) / (radius * radius));
        pool += disc * saturate(dot(normal, -direction)) * step(0.0, along);
    }

    return pool;
}

//Lights a surface: the vault's dim fill from above, the lake's teal from below (fading with height over it), every crystal
//as a point light (a little wrap, so a lit flank does not end in a hard terminator), a wet highlight, and the shafts'
//pools. `occlusion` darkens what the fills and the lights reach alike; `gloss` is how wet the stone is.
float3 LightStone(float3 albedo, float3 p, float3 normal, float3 view, float occlusion, float gloss)
{
    float lowness = exp(-max(p.y - WaterLevelY, 0.0) / 45.0);
    float3 colour = albedo * occlusion * (AmbientTop * (0.30 + 0.70 * saturate(normal.y * 0.5 + 0.5))
        + WaterGlowColor * (saturate(0.5 - 0.5 * normal.y) * 0.8 + 0.2) * lowness * 4.0);

    [loop]
    for (int k = 0; k < LightCount; k++)
    {
        float3 toLight = LightPosition[k].xyz - p;
        float d2 = dot(toLight, toLight);
        float3 l = toLight * rsqrt(max(d2, 1e-4));
        float range = saturate(1.0 - d2 * LightPosition[k].w);
        float falloff = range * range / (1.0 + d2 / (LIGHT_NEAR * LIGHT_NEAR));
        float wrap = saturate(dot(normal, l) * 0.8 + 0.2);
        float3 halfway = normalize(l + view);
        float specular = pow(saturate(dot(normal, halfway)), 48.0) * gloss;
        colour += LightColor[k].rgb * falloff * (albedo * wrap * occlusion + specular * 0.35);
    }

    colour += albedo * ShaftColor * (ShaftPool(p, normal) * ShaftStrength * 3.0);
    return colour;
}

//The vault's own faint light: the glowworms hang in their thousands on its downward faces, and the rock between them takes
//their blue-green - what lets the vault be seen as rock round the points rather than as black sky behind them
float3 VaultGlow(float3 p, float3 baseNormal, float occlusion)
{
    float facing = saturate(-baseNormal.y * 1.4 - 0.25);
    float patches = 0.55 + 0.45 * GradientNoise3(p * 0.012 + 5.0);
    return GlowwormColor * (VAULT_GLOW * facing * patches * occlusion);
}

//Where a shaft comes in: the crack in the vault, a ragged opening blazing with the daylight beyond
float3 ShaftOpening(float3 p)
{
    float3 light = 0.0;

    [unroll]
    for (int s = 0; s < SHAFT_COUNT; s++)
    {
        float3 offset = p - ShaftSource[s].xyz;
        float r = ShaftSource[s].w * 0.9;
        float ragged = 1.0 + 0.45 * GradientNoise3(p * 0.35 + s * 9.0);
        float opening = saturate(1.0 - dot(offset, offset) / (r * r * ragged * ragged));
        light += ShaftColor * (ShaftStrength * 12.0 * opening * opening * opening);
    }

    return light;
}

//===================================================================================================================
//THE LAKE, seen from above its surface
//===================================================================================================================

//The surface's slope at p (xz): two octaves of drifting gradient noise, still enough to mirror, never glass
float2 RippleSlope(float2 p)
{
    float2 q = p / RippleScale;
    float t = CavernTime * RippleSpeed;
    const float e = 0.35;
    float a0 = GradientNoise2(q + float2(t, t * 0.6));
    float ax = GradientNoise2(q + float2(e + t, t * 0.6));
    float az = GradientNoise2(q + float2(t, e + t * 0.6));
    float2 q2 = q * 2.7 + 13.0;
    float b0 = GradientNoise2(q2 - float2(t * 1.3, t * 0.4));
    float bx = GradientNoise2(q2 + float2(e - t * 1.3, -t * 0.4));
    float bz = GradientNoise2(q2 + float2(-t * 1.3, e - t * 0.4));
    return (float2(ax - a0, az - a0) + 0.45 * float2(bx - b0, bz - b0)) / e;
}

//What the eye sees where its ray meets the lake on the way down to `bed` (a point under the waterline, shaded as
//`bedColour`, or black where the water hides it anyway): the hall's reflection bent by the ripple, over the bed dimmed by
//the water it is seen through, the crystals glinting on the ripple, all of it through the air down to the surface
float3 SeenThroughWater(float3 bed, float3 bedColour, float2 pixel)
{
    float3 toBed = bed - CameraPosition;
    float travel = saturate((WaterLevelY - CameraPosition.y) / min(toBed.y, -1e-4));
    float3 surface = CameraPosition + toBed * travel;
    float3 view = normalize(toBed);
    float inWater = length(toBed) * (1.0 - travel);
    float transmittance = exp(-inWater * WaterAbsorption);

    //The ripple calms with distance: a far ripple is under a pixel and would only shimmer
    float distanceToSurface = length(surface - CameraPosition);
    float calm = saturate(1.0 - distanceToSurface / 260.0);
    float2 slope = RippleSlope(surface.xz) * RippleStrength * calm;
    float3 normal = normalize(float3(-slope.x, 1.0, -slope.y));

    //Fresnel, water's, over a floor: a cave lake is a black mirror, because nothing comes up out of its depths
    float facing = saturate(dot(normal, -view));
    float fresnel = 0.03 + 0.97 * pow(1.0 - facing, 5.0);
    fresnel = lerp(0.20, 1.0, fresnel);

    float stir = saturate(dot(slope, slope) / (RippleStrength * RippleStrength + 1e-6));
    float2 uv = pixel / ViewportSize + slope * float2(0.15, -0.15);
    float3 mirrored = lerp(FogColor, RockColor * 0.4, saturate(-view.y));
    [branch]
    if (ReflectionAvailable > 0.5)
    {
        //Drawn out into vertical streaks, as a lake's ripples draw every light over it: the picture smeared down the screen,
        //more where the water stirs and the nearer the look comes to grazing
        float streak = (0.003 + 0.016 * stir) * (0.35 + 0.65 * (1.0 - facing)) * calm;
        float3 sum = 0.0;
        float weights = 0.0;
        [unroll]
        for (int k = -3; k <= 3; k++)
        {
            float w = exp(-k * k / 5.0);
            sum += w * Reflection.SampleLevel(ReflectionSampler, uv + float2(0.0, k * streak / 3.0), 0).rgb;
            weights += w;
        }
        mirrored = sum / weights;
    }

    float3 depths = bedColour * transmittance + WaterDeepColor * (1.0 - transmittance);
    float3 colour = lerp(depths, mirrored, fresnel);

    //The crystals glint on the ripple: the lights' own highlights, which the mirrored picture holds only as broad glows
    float3 reflected = reflect(view, normal);
    [loop]
    for (int k = 0; k < LightCount; k++)
    {
        float3 toLight = LightPosition[k].xyz - surface;
        float d2 = dot(toLight, toLight);
        float glint = pow(saturate(dot(reflected, toLight * rsqrt(max(d2, 1e-4)))), 300.0);
        colour += LightColor[k].rgb * glint * (1.2 / (1.0 + d2 / 3600.0)) * (0.3 + 2.0 * stir);
    }

    return Atmosphere(colour, surface, CameraPosition);
}

//===================================================================================================================
//THE ROCK: the shell and the lake's bed
//===================================================================================================================

struct RockInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 Data : TEXCOORD0;    //x the vertex's occlusion, y 0 on the shell and 1 on the bed (a formation's: along, roll)
};

struct RockOutput
{
    float4 Position : SV_POSITION;
    float3 World : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 Data : TEXCOORD2;
};

RockOutput RockVS(RockInput input)
{
    RockOutput output;
    output.World = input.Position.xyz;
    output.Position = mul(float4(input.Position.xyz, 1.0), ViewProjection);
    output.Normal = input.Normal;
    output.Data = input.Data;
    return output;
}

//The stone's relief as a height: five octaves of 3D noise from fifteen units down to a hand's width, each faded as its
//period nears the pixel (a world-fixed octave under a pixel is shimmer), and where `full`, creased joints between blocks.
//The reduced program (Low, and the mirror) keeps the three broad octaves (#1029): the shell is nearly every pixel of the
//cavern's frame, and on the reference APU the shell was 10.7 ms of a 17.3 ms Low frame
float ReliefHeight(float3 p, float footprint, uniform bool full)
{
    float h = 0.0;
    float amplitude = 2.6;
    float frequency = 0.07;

    [unroll]
    for (int i = 0; i < (full ? 5 : 3); i++)
    {
        float fade = 1.0 - smoothstep(0.25, 0.6, footprint * frequency);
        h += amplitude * fade * GradientNoise3(p * frequency + i * 7.3);
        amplitude *= 0.45;
        frequency *= 2.25;
    }

    if (full)
    {
        float fade = 1.0 - smoothstep(0.15, 0.5, footprint * 0.11);
        h -= 0.9 * fade * pow(1.0 - abs(GradientNoise3(p * 0.11 + 4.1)), 6.0);
    }

    return h;
}

//The reduced program takes the sheets' and the mottle's fields at one octave (#1029), the finer of their two being the
//part of them a Low frame resolves least
float3 RockAlbedo(float3 p, float3 baseNormal, float floorWeight, float height, uniform bool full)
{
    //Flowstone: sheets of paler calcite running down the steep walls in curtains, streaked along their fall, in patches
    float steep = 1.0 - abs(baseNormal.y);
    float sheet = smoothstep(0.05, 0.35, Fbm3(p * float3(0.022, 0.006, 0.022) + 3.0, full ? 2 : 1)) * smoothstep(0.35, 0.8, steep);
    float streak = 0.5 + 0.5 * GradientNoise3(p * float3(0.55, 0.035, 0.55) + 9.0);
    float3 albedo = lerp(RockColor, CalciteColor * (0.30 + 0.30 * streak), sheet * (1.0 - floorWeight));

    //The rock's own mottle, and the relief's hollows a little darker
    float mottle = Fbm3(p * 0.035 + 17.0, full ? 2 : 1);
    albedo *= 0.75 + 0.5 * saturate(mottle + 0.5);
    albedo *= 0.8 + 0.2 * saturate(height * 0.4 + 0.5);

    //The bed: gravel and silt, a little warmer and paler than the walls
    return lerp(albedo, RockColor * float3(1.25, 1.15, 1.0), floorWeight * 0.5);
}

float4 RockScene(RockOutput input, uniform bool full, uniform bool mirrored)
{
    float3 p = input.World;
    if (mirrored) clip(p.y - WaterLevelY + 0.2);

    float3 baseNormal = normalize(input.Normal);
    float footprint = length(fwidth(p));
    float height = ReliefHeight(p, footprint, full);
    float3 normal = PerturbNormalFromHeight(baseNormal, p, height);

    float3 eye = CameraPosition;
    float3 view = normalize(eye - p);
    float occlusion = input.Data.x * (0.65 + 0.35 * saturate(height * 0.35 + 0.6));

    //Wet near the water, and darker: wet stone is
    float wet = 1.0 - smoothstep(0.0, 7.0, p.y - WaterLevelY);
    float3 albedo = RockAlbedo(p, baseNormal, input.Data.y, height, full) * lerp(1.0, 0.55, wet);

    float3 colour;
    if (!mirrored && p.y < WaterLevelY)
    {
        //The bed is shaded only where the water lets it through; the deep lake is the reflection alone
        float3 toBed = p - eye;
        float travel = saturate((WaterLevelY - eye.y) / min(toBed.y, -1e-4));
        float inWater = length(toBed) * (1.0 - travel);
        float3 bedColour = 0.0;
        [branch]
        if (inWater * WaterAbsorption < 5.0) bedColour = LightStone(albedo, p, normal, view, occlusion, 0.0);
        colour = SeenThroughWater(p, bedColour, input.Position.xy);
    }
    else
    {
        colour = LightStone(albedo, p, normal, view, occlusion, 0.15 + 0.85 * wet);
        colour += VaultGlow(p, baseNormal, occlusion) + ShaftOpening(p);
        colour = Atmosphere(colour, p, eye);
    }

    return float4(colour, 1.0);
}

float4 RockPS(RockOutput input) : COLOR0 { return RockScene(input, true, false); }
float4 RockReducedPS(RockOutput input) : COLOR0 { return RockScene(input, false, false); }
float4 RockMirroredPS(RockOutput input) : COLOR0 { return RockScene(input, false, true); }

//===================================================================================================================
//THE FORMATIONS: stalactites, stalagmites, columns
//===================================================================================================================

float4 FormationScene(RockOutput input, uniform bool full, uniform bool mirrored)
{
    float3 p = input.World;
    if (mirrored) clip(p.y - WaterLevelY + 0.2);

    float3 baseNormal = normalize(input.Normal);
    float along = input.Data.x;
    bool column = input.Data.y >= 2.0;
    float footprint = length(fwidth(p));

    //Calcite in growth bands down the formation, the drip line's own record: faint, and of uneven spacing (a band of
    //one period read as a candy stripe); and ribs of flowstone running down it, a column's carried by its mesh
    float bandCoord = p.y * (column ? 0.35 : 0.7) + GradientNoise3(p * float3(0.15, 0.05, 0.15)) * 3.0;
    float bands = 0.5 + 0.5 * sin(bandCoord * 2.1 + 1.7 * sin(bandCoord * 0.53));
    bands *= 1.0 - smoothstep(0.3, 1.0, footprint * 0.7);
    //A stalactite's fine vertical ribs; a column's would be bark, so it takes broad lumps of wet flowstone instead
    float3 ribScale = column ? float3(0.35, 0.18, 0.35) : float3(1.6, 0.12, 1.6);
    float height = (column ? 0.9 : 0.34) * GradientNoise3(p * ribScale) * (1.0 - smoothstep(0.3, 0.9, footprint * ribScale.x));
    height += 0.10 * bands;
    if (full) height += 0.08 * GradientNoise3(p * float3(4.0, 0.6, 4.0)) * (1.0 - smoothstep(0.2, 0.7, footprint * 4.0)) * (column ? 0.4 : 1.0);
    float3 normal = PerturbNormalFromHeight(baseNormal, p, height);

    float3 view = normalize(CameraPosition - p);
    float tip = column ? 0.0 : along;
    float stain = 0.5 + 0.5 * GradientNoise3(p * 0.09 + 31.0);
    float3 albedo = lerp(RockColor * 1.8, CalciteColor, 0.45 + 0.4 * tip);
    albedo *= 0.85 + 0.12 * bands - 0.15 * stain * (1.0 - tip);
    if (column) albedo = lerp(RockColor * 1.6, CalciteColor * 0.9, 0.55 + 0.15 * bands - 0.2 * stain);
    //Where a column meets the vault it is the vault's rock: the head darkens into the ceiling it grew out of
    if (column) albedo = lerp(albedo, RockColor, smoothstep(0.86, 0.99, along));

    float wet = 1.0 - smoothstep(0.0, 6.0, p.y - WaterLevelY);
    albedo *= lerp(1.0, 0.6, wet);
    float occlusion = column ? 0.85 : lerp(0.65, 1.0, along);
    float gloss = 0.35 + 0.65 * tip;

    float3 colour;
    if (!mirrored && p.y < WaterLevelY)
    {
        float3 bedColour = LightStone(albedo, p, normal, view, occlusion, 0.0);
        colour = SeenThroughWater(p, bedColour, input.Position.xy);
    }
    else
    {
        colour = LightStone(albedo, p, normal, view, occlusion, gloss);
        //A thin formation glows at its rim against the dark: calcite is translucent where it is thin
        float rim = pow(1.0 - saturate(dot(normal, view)), 3.0) * tip;
        colour += albedo * MistColor * rim * 6.0;
        colour = Atmosphere(colour, p, CameraPosition);
    }

    return float4(colour, 1.0);
}

float4 FormationPS(RockOutput input) : COLOR0 { return FormationScene(input, true, false); }
float4 FormationReducedPS(RockOutput input) : COLOR0 { return FormationScene(input, false, false); }
float4 FormationMirroredPS(RockOutput input) : COLOR0 { return FormationScene(input, false, true); }

//===================================================================================================================
//THE CRYSTALS: translucent six-sided prisms glowing from within (#756)
//===================================================================================================================

struct CrystalInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 Frame0 : TEXCOORD0;      //base.xyz, radius to a corner
    float4 Frame1 : TEXCOORD1;      //axis.xyz, length to the shoulder
    float4 Frame2 : TEXCOORD2;      //tip length, turn, hue, phase
};

struct CrystalOutput
{
    float4 Position : SV_POSITION;
    float3 World : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float4 Frame0 : TEXCOORD2;
    float4 Frame1 : TEXCOORD3;
    float4 Frame2 : TEXCOORD4;
};

CrystalOutput CrystalVS(CrystalInput input)
{
    CrystalOutput output;
    output.World = input.Position.xyz;
    output.Position = mul(float4(input.Position.xyz, 1.0), ViewProjection);
    output.Normal = input.Normal;
    output.Frame0 = input.Frame0;
    output.Frame1 = input.Frame1;
    output.Frame2 = input.Frame2;
    return output;
}

//The prism's frame, rebuilt from its axis and turn exactly as CavernGeometry.PrismFrame builds it
void PrismFrame(float3 axis, float turn, out float3 side, out float3 other)
{
    float3 helper = abs(axis.y) < 0.9 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 s0 = normalize(cross(axis, helper));
    float3 o0 = cross(axis, s0);
    side = s0 * cos(turn) + o0 * sin(turn);
    other = cross(axis, side);
}

//How far a ray starting ON the prism's surface at local q, heading d, runs inside it: the nearest of the thirteen planes
//it leaves through (six sides, six pyramid faces, the foot). Each plane is n.x <= c in the prism's frame (x and z across,
//y along the axis); the face normals stand between the corners, at 30, 90 and 150 degrees and their opposites.
float PrismThickness(float3 q, float3 d, float apothem, float len, float tip)
{
    float exitT = 1e5;
    float pc = apothem * (1.0 + len / tip);

    [unroll]
    for (int k = 0; k < 6; k++)
    {
        float angle = (k * 60.0 + 30.0) * PI / 180.0;
        float2 n = float2(cos(angle), sin(angle));

        //The side face
        float nd = dot(n, d.xz);
        exitT = min(exitT, nd > 1e-5 ? (apothem - dot(n, q.xz)) / nd : 1e5);

        //The pyramid's face over it: n.xz + (apothem / tip) y <= apothem (1 + len / tip)
        float3 pn = float3(n.x, apothem / tip, n.y);
        float pd = dot(pn, d);
        exitT = min(exitT, pd > 1e-5 ? (pc - dot(pn, q)) / pd : 1e5);
    }

    float tFoot = d.y < -1e-5 ? -q.y / d.y : 1e5;
    return max(min(exitT, tFoot), 0.0);
}

float CrystalPulse(float phase)
{
    float beat = 0.5 + 0.5 * sin(CavernTime * CrystalPulseSpeed + phase);
    return 0.65 + 0.35 * beat * beat;
}

float4 CrystalScene(CrystalOutput input, uniform bool mirrored)
{
    float3 p = input.World;
    if (mirrored) clip(p.y - WaterLevelY + 0.2);

    float3 normal = normalize(input.Normal);
    float3 view = normalize(p - CameraPosition);

    float3 axis = normalize(input.Frame1.xyz);
    float3 side, other;
    PrismFrame(axis, input.Frame2.y, side, other);
    float radius = input.Frame0.w;
    float apothem = radius * 0.8660254;
    float len = input.Frame1.w;
    float tip = input.Frame2.x;

    float3 rel = p - input.Frame0.xyz;
    float3 q = float3(dot(rel, side), dot(rel, axis), dot(rel, other));
    float3 d = float3(dot(view, side), dot(view, axis), dot(view, other));

    //The light crosses this much crystal: thick through the core, a sliver at the edges, where the cave shows through
    float thickness = mirrored ? radius : PrismThickness(q, d, apothem, len, tip);

    //The milky core: how near the ray passes the axis, at the middle of its path through the prism
    float3 middle = q + d * (thickness * 0.5);
    float core = exp(-dot(middle.xz, middle.xz) / (radius * radius) * 2.5);

    //Veils and fractures inside: a field sampled along the path, so they float in the body and turn with the view
    float veil = 0.0;
    if (!mirrored)
    {
        float3 w1 = p + view * (thickness * 0.3);
        float3 w2 = p + view * (thickness * 0.65);
        veil = smoothstep(0.25, 0.55, abs(Fbm3(w1 * 0.9 + input.Frame2.w, 2)) * 2.0)
            + smoothstep(0.3, 0.6, abs(GradientNoise3(w2 * 1.7 + 3.0)) * 2.0);
    }

    //Striations along the faces, and the arrises bright: the edge is where a crystal reads as cut
    float striation = 0.5 + 0.5 * sin(q.y * 7.0 + GradientNoise3(p * 2.0) * 3.0);
    float best = 0.0, second = 0.0;
    [unroll]
    for (int k = 0; k < 6; k++)
    {
        float angle = (k * 60.0 + 30.0) * PI / 180.0;
        float s = dot(float2(cos(angle), sin(angle)), q.xz) / apothem;
        second = max(second, min(best, s));
        best = max(best, s);
    }
    float edge = smoothstep(0.80, 0.98, second) + smoothstep(0.92, 1.0, q.y / max(len, 1e-3)) * step(q.y, len + 0.01) * 0.6;

    float3 own = lerp(CrystalColorA, CrystalColorB, input.Frame2.z);
    float pulse = CrystalPulse(input.Frame2.w);
    float transmittance = exp(-thickness * 0.16);

    float3 glow = own * CrystalEmission * pulse
        * ((0.20 + 0.80 * (1.0 - transmittance)) * (0.55 + 0.9 * core) + 0.35 * veil);
    glow += own * CrystalEmission * pulse * edge * 0.6;
    glow *= 0.85 + 0.15 * striation;

    //What the faces reflect: a dark cave, and the clusters' highlights
    float facing = saturate(dot(normal, -view));
    float fresnel = 0.04 + 0.96 * pow(1.0 - facing, 5.0);
    float3 reflected = reflect(view, normal);
    float3 specular = 0.0;
    [loop]
    for (int l = 0; l < LightCount; l++)
    {
        float3 toLight = LightPosition[l].xyz - p;
        float d2 = dot(toLight, toLight);
        specular += LightColor[l].rgb * pow(saturate(dot(reflected, toLight * rsqrt(max(d2, 1e-4)))), 60.0) / (1.0 + d2 / 400.0);
    }
    float3 colour = glow + specular * 0.8 + fresnel * FogColor * 4.0;

    //The air: the crystal's own light fogs like the rock, and what it lets through is the background, already fogged
    float3 toP = p - CameraPosition;
    float distanceTravelled = length(toP);
    float fog = 1.0 - exp(-distanceTravelled * FogDensity);
    float mist = MistAmount(CameraPosition, toP / max(distanceTravelled, 1e-3), distanceTravelled);
    colour = colour * (1.0 - fog) * (1.0 - mist) + (FogColor * fog + MistColor * mist) * (1.0 - transmittance);

    //Blended One, SourceAlpha: the frame behind is kept by the transmittance and the crystal's light added over it
    return float4(colour, transmittance);
}

float4 CrystalPS(CrystalOutput input) : COLOR0 { return CrystalScene(input, false); }
float4 CrystalMirroredPS(CrystalOutput input) : COLOR0 { return CrystalScene(input, true); }

//===================================================================================================================
//THE GLOWWORMS: points on the vault, a pixel or two wide at any distance
//===================================================================================================================

struct WormInput
{
    float4 Position : POSITION0;
    float4 Corner : TEXCOORD0;      //corner xy, phase, size
};

struct WormOutput
{
    float4 Position : SV_POSITION;
    float2 Corner : TEXCOORD0;
    float Light : TEXCOORD1;
};

WormOutput WormVS(WormInput input)
{
    WormOutput output;
    //A hand's width towards the lens, so the rock the worm hangs on cannot cover it - in world units, not as a bias in
    //depth: the depth buffer's resolution falls with the square of the distance, and a bias that held at the near vault
    //put the far worms in front of the columns and stalactites between them and the lens
    float3 p = input.Position.xyz;
    p += normalize(CameraPosition - p) * 0.6;
    float4 clipPosition = mul(float4(p, 1.0), ViewProjection);

    //Spread the corners by a size in PIXELS: a world-sized dot at the far end of the hall is a fraction of a pixel and a
    //field of those crawls, and a world-sized dot on rock seen edge-on was the stretched "firefly" (#958)
    clipPosition.xy += input.Corner.xy * (GlowwormPixels * input.Corner.w * 2.0 / ViewportSize) * clipPosition.w;
    output.Position = clipPosition;
    output.Corner = input.Corner.xy;

    float distanceTravelled = length(p - CameraPosition);
    float fog = exp(-distanceTravelled * FogDensity * 0.8);
    float breathe = 0.55 + 0.45 * sin(CavernTime * 0.7 + input.Corner.z);
    output.Light = breathe * fog;
    return output;
}

float4 WormPS(WormOutput input) : COLOR0
{
    float r2 = dot(input.Corner, input.Corner);
    float spot = exp(-r2 * 3.2) - 0.04;
    clip(spot);
    return float4(GlowwormColor * (spot * input.Light), 0.0);
}

//===================================================================================================================
//THE SHAFTS: light falling through cracks in the vault, made visible by the dust in it
//===================================================================================================================

RockOutput ShaftVS(RockInput input)
{
    return RockVS(input);
}

float4 ShaftPS(RockOutput input) : COLOR0
{
    //Only the side towards the lens, the beam gathered along the whole ray from there. The cone reaches back into the rock
    //over the crack, so a ray looking up into the opening still crosses a face under the vault: kept on its far side, whose
    //top lies in the rock, the opening was ringed by a dark ellipse with the haze only round it
    float3 toP = input.World - CameraPosition;
    clip(-dot(input.Normal, toP));

    int s = (int)(input.Data.x + 0.5);
    float3 source = ShaftSource[s].xyz;
    float3 axis = ShaftDirection[s].xyz;
    float len = ShaftDirection[s].w;

    float distanceTravelled = length(toP);
    float3 ray = toP / distanceTravelled;

    //The ray's closest approach to the beam's axis
    float3 w = CameraPosition - source;
    float b = dot(ray, axis);
    float denom = max(1.0 - b * b, 1e-4);
    float tRay = max((b * dot(w, axis) - dot(w, ray)) / denom, 0.0);
    float3 nearest = CameraPosition + ray * tRay;
    float along = clamp(dot(nearest - source, axis), 0.0, len);
    float radius = ShaftSource[s].w * (1.0 + 0.5 * along / len);
    float3 offset = nearest - (source + axis * along);

    //Through a gaussian beam the light gathered grows as the ray runs along it
    float path = min(radius * 1.77 / sqrt(denom), len * 0.5);
    float density = exp(-dot(offset, offset) / (radius * radius)) * path / radius;

    float fall = along / len;
    float envelope = smoothstep(0.0, 0.06, fall) * (1.0 - 0.6 * fall);
    float dust = 0.6 + 0.6 * Fbm3(nearest * 0.18 + float3(0.0, CavernTime * 0.25, 0.0), 3);

    float fog = exp(-tRay * FogDensity);
    return float4(ShaftColor * (ShaftStrength * 0.22 * density * envelope * dust * fog), 0.0);
}

//===================================================================================================================
//THE FORMATIONS' DEPTH: written into the scene's own target when the hall was shaded into a smaller one (#755)
//===================================================================================================================

float4 DepthVS(float4 position : POSITION0) : SV_POSITION
{
    return mul(float4(position.xyz, 1.0), ViewProjection);
}

float4 DepthPS() : COLOR0 { return 0.0; }

technique Rock
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL RockVS();
        PixelShader = compile PS_SHADERMODEL RockPS();
    }
};

technique RockReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL RockVS();
        PixelShader = compile PS_SHADERMODEL RockReducedPS();
    }
};

technique RockMirrored
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL RockVS();
        PixelShader = compile PS_SHADERMODEL RockMirroredPS();
    }
};

technique Formation
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL RockVS();
        PixelShader = compile PS_SHADERMODEL FormationPS();
    }
};

technique FormationReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL RockVS();
        PixelShader = compile PS_SHADERMODEL FormationReducedPS();
    }
};

technique FormationMirrored
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL RockVS();
        PixelShader = compile PS_SHADERMODEL FormationMirroredPS();
    }
};

technique Crystal
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CrystalVS();
        PixelShader = compile PS_SHADERMODEL CrystalPS();
    }
};

technique CrystalMirrored
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CrystalVS();
        PixelShader = compile PS_SHADERMODEL CrystalMirroredPS();
    }
};

technique Glowworm
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL WormVS();
        PixelShader = compile PS_SHADERMODEL WormPS();
    }
};

technique Shaft
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ShaftVS();
        PixelShader = compile PS_SHADERMODEL ShaftPS();
    }
};

technique Depth
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL DepthVS();
        PixelShader = compile PS_SHADERMODEL DepthPS();
    }
};
