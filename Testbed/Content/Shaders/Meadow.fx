//Draws a flowering meadow: lush green rolling hills scattered with wildflowers, a clearing the arena sits
//in with the hills rolling up in the distance. Sixth scene variant (NumPad2 cycles ... -> meadow). The
//look is the Windows XP "Bliss" hill - smooth vivid green under a blue sky - and the motion is the wind:
//bands of it comb through the grass, while the shared cloud shadows drift over the whole field. The
//round stone island stays as the platform standing in the meadow.
//
//Real geometry like the desert and the mountains - a camera-centred grid (shared CreateGridMesh on the C#
//side) displaced by a smooth rolling field, low around the arena and rising into hills with distance, its
//normal taken by finite differences. Drawn in both executables, Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground (the flat clearing otherwise slices
//across the funnel just below its rim, hiding its depth and swallowing the balls falling through). The Testbed
//sets this to the island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

float MeadowLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

float MeadowTime;
float2 WindDirection;

//Grass (linear) and the darker green it varies towards in patches, how much sky fills the flats, and the
//distance over which the field melts into the skyline
float3 GrassColor;
float3 GrassColorDark;
float AmbientStrength;
float HorizonHazeDistance;

//Wind combing the grass: how fast the bright/dark bands travel, how far apart they are, how deep they cut
float WindRippleSpeed;
float WindRippleFrequency;
float WindRippleStrength;

//Fine grass texture (a normal-tilting height field), its amplitude and blades-per-world-unit
float GrassReliefStrength;
float GrassReliefFrequency;

//Wildflowers: how many of the grid cells carry one, how far apart the cells are, and the flower size
float FlowerDensity;
float FlowerSpacing;
float FlowerSize;

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);

    return frac(p.x * p.y);
}

//Gentle rolling hills: smooth sines (not the mountains' ridges), low around the arena centre (world
//origin) and rising into hills with distance, so the meadow is flat where the arena stands and rolls up
//towards the horizon. Sampled three times per vertex for the finite-difference normal.
float TerrainHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float rolling = 0.5 * sin(dot(p, float2(0.020, 0.015)))
        + 0.3 * sin(dot(p, float2(-0.013, 0.024)) + 1.5)
        + 0.2 * sin(dot(p, float2(0.031, 0.026)) + 3.0);

    float basin = ClearingRelief * sin(dot(p, float2(0.05, 0.035)));

    return MeadowLevelY + basin + HillHeight * ramp * (rolling * 0.5 + 0.5);
}

struct MeadowVertexInput
{
    float4 Position : POSITION0;
};

struct MeadowVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
};

MeadowVertexOutput MeadowVS(MeadowVertexInput input)
{
    MeadowVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float h = TerrainHeight(xz);

    float e = 2.0;
    float hx = TerrainHeight(xz + float2(e, 0.0));
    float hz = TerrainHeight(xz + float2(0.0, e));
    output.WorldNormal = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));

    float3 worldPosition = float3(xz.x, h, xz.y);
    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//Tangent-free normal tilt from a height field, as everywhere else in this project
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

//How far the grass is stretched ALONG the wind, and the gain that carries fBm to the amplitude the two
//crossed sines here used to have. Its own values rather than the savanna's, though both start at the same
//figures: this is a lush lawn where that is dry veld, and the two scenes are meant to differ. See the
//savanna's copy for what each number is answering.
static const float GRASS_COMB_STRETCH = 2.6;
static const float GRASS_FBM_GAIN = 5.0;

//The rosettes' form (#127), as fractions of a flower's own world size: how high a petal domes, how much
//the golden eye rises over the petals, and how hard the grass darkens in the contact ring just outside
//the rim. The flowers' fabric rather than a mood — the mood dials (density, spacing, size) stay on the
//config — and the heights are fractions so a big rosette is proportionally domed rather than uniformly.
static const float FLOWER_PETAL_RELIEF = 0.20;
static const float FLOWER_EYE_RELIEF = 0.16;
static const float FLOWER_CONTACT_SHADOW = 0.22;

//A fine grass texture that drifts on the wind, band-limited against the footprint so it fades to smooth
//green towards the horizon instead of aliasing.
//
//THREE OCTAVES OF GRADIENT NOISE, not the two crossed plane-wave sines this used to be — the meadow carried
//a line-for-line copy of the savanna's field, so it carried its diamond lattice too (#117 was filed against
//the savanna alone; the copy here was found while fixing it). Two plane waves crossing ARE a lattice, and
//these crossed at 93.4 degrees, so it was very nearly square and read in perspective as a field of diamonds.
//The mechanism is Noise.fxh's Fbm2Combed now, one copy for both scenes; what stays per scene is the tuning.
float GrassRelief(float2 xz, float footprint)
{
    float f = GrassReliefFrequency;
    float2 p = (xz + WindDirection * MeadowTime * 0.7) * f;

    return Fbm2Combed(p, WindDirection, GRASS_COMB_STRETCH, 3, footprint * f) * GRASS_FBM_GAIN * GrassReliefStrength;
}

float4 MeadowPS(MeadowVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float3 baseNormal = normalize(input.WorldNormal);
    float footprint = length(fwidth(worldPosition.xz));

    //Wildflowers: little rosettes rather than dots — a ring of petals around a bright eye, each with its
    //own petal count, size, rotation and colour. One per grid cell that draws one, faded against the
    //footprint so the distant meadow stays clean green instead of a shimmer. Evaluated BEFORE the normal
    //since #127: a flower is no longer only an albedo swap — it has a height of its own, and that height
    //goes through the very perturbation the grass relief rides.
    float2 cell = floor(worldPosition.xz / FlowerSpacing);
    float2 within = frac(worldPosition.xz / FlowerSpacing);
    float present = step(1.0 - FlowerDensity, Hash21(cell));

    //Per-flower character, all off the cell hash
    float petalCount = 5.0 + floor(Hash21(cell + 5.5) * 3.0); //5, 6 or 7 petals
    float rotation = Hash21(cell + 9.9) * 6.2831853;
    float size = FlowerSize * (0.7 + 0.6 * Hash21(cell + 2.2));

    //The centre may only wander in [size, 1-size], so the whole flower stays inside its cell and no petal
    //is cut off by the cell edge (the flower is only evaluated within its own cell's fraction).
    float2 flowerCentre = size + float2(Hash21(cell + 3.1), Hash21(cell + 7.7)) * (1.0 - 2.0 * size);
    float2 delta = within - flowerCentre;
    float radius = length(delta);
    float angle = atan2(delta.y, delta.x);

    //The scalloped outer edge: petalCount rounded lobes around the centre. |cos(N*angle/2)| makes N lobes
    //and, being even in the angle, stays continuous across the atan2 seam; the power rounds the petals out.
    float lobes = pow(abs(cos(petalCount * (angle + rotation) * 0.5)), 0.6);
    float petalEdge = size * (0.34 + 0.66 * lobes);
    float centreEdge = size * 0.3;

    float resolvable = saturate(1.0 - footprint / FlowerSpacing);
    float aa = fwidth(radius) * 1.5 + 1e-4;

    float flowerMask = present * (1.0 - smoothstep(petalEdge - aa, petalEdge + aa, radius)) * resolvable;
    float centreMask = 1.0 - smoothstep(centreEdge - aa, centreEdge + aa, radius);

    //The rosette's own height (#127): each petal a dome — full mid-petal, falling to nothing at the
    //scalloped rim, dipping in the gaps between petals (the lobes term) — and the eye a raised boss over
    //them. In WORLD units like the grass relief it is summed with, so the perturbation's derivatives read
    //both as one surface; scaled by the flower's own world size, so a big flower is proportionally domed.
    //Zero by construction at the cell border (the profile dies at the rim, and the rim never reaches the
    //border), so the per-cell hashes cannot tear the height field where cells meet.
    float petalProfile = saturate(1.0 - radius / max(petalEdge, 1e-4));
    float eyeDome = 1.0 - smoothstep(0.0, centreEdge * 1.4, radius);
    float flowerRelief = present * resolvable * size * FlowerSpacing
        * (FLOWER_PETAL_RELIEF * petalProfile * (0.35 + 0.65 * lobes) + FLOWER_EYE_RELIEF * eyeDome);

    //Fine grass texture tilts the normal — and the rosette tilts it with it, one height field through one
    //perturbation, so a flower catches the sun on its own domes instead of wearing the ground's shading
    float relief = GrassRelief(worldPosition.xz, footprint);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief + flowerRelief);

    //Grass color, varied in broad patches so the field is not one flat green
    float patch = CloudNoise(worldPosition.xz * 0.15) * 0.5 + 0.5;
    float3 grass = lerp(GrassColorDark, GrassColor, patch);

    //Wind combing the grass: bright and dark bands travelling downwind, the meadow's own motion
    float wind = sin(dot(worldPosition.xz, WindDirection) * WindRippleFrequency + MeadowTime * WindRippleSpeed);
    grass *= 1.0 + wind * WindRippleStrength;

    //White daisies, yellow buttercups, the odd pink one - all with a warm golden eye
    float pick = Hash21(cell + 13.7);
    float3 petalColor = pick < 0.5 ? float3(0.96, 0.96, 0.92)
        : (pick < 0.80 ? float3(0.97, 0.88, 0.28) : float3(0.90, 0.45, 0.68));

    //The face of a petal against its rim (#127): a cheap occlusion gradient — the face keeps its colour,
    //the scalloped rim falls into shade, the eye brightens at its very centre — so a rosette reads as a
    //form even where the light is flat. The normal above carries the real shape; this carries the ambient
    //half the hemisphere term is too broad to give.
    float3 flowerColor = petalColor * (0.80 + 0.28 * petalProfile);
    flowerColor = lerp(flowerColor, float3(0.98, 0.74, 0.12) * (0.9 + 0.35 * eyeDome), centreMask);

    //And a hint of the flower standing OVER the grass: a narrow contact shadow just outside the petals.
    //Inside the rosette it darkens grass the petals then replace, which leaves exactly the AA fringe of
    //the rim reading as the petal's own cast edge.
    float contact = present * resolvable * (1.0 - smoothstep(petalEdge, petalEdge * 1.45, radius));
    grass *= 1.0 - FLOWER_CONTACT_SHADOW * contact;

    grass = lerp(grass, flowerColor, flowerMask);

    //Matte grass: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds that
    //drift across the sky sweep their shadows over the field
    float sunlight = CloudSunlight(worldPosition, SunDirection);
    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = grass * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //Horizon haze: the distant hills soften into the skyline
    float dist = distance(CameraPosition, worldPosition);
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, HorizonColor, haze * haze);

    return float4(color, 1.0);
}

technique Meadow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MeadowVS();
        PixelShader = compile PS_SHADERMODEL MeadowPS();
    }
};
