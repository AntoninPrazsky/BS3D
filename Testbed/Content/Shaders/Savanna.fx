//Draws a savanna: open golden grassland rolling gently to a wide horizon, a flat clearing the island stands
//in that rises into low rises with distance, dotted with acacia trees (drawn separately, Acacia.fx). Third
//scene variant (NumPad2), replacing the Sahara dunes.
//
//Real geometry like the meadow and the mountains - a camera-centred grid (shared CreateGridMesh on the C#
//side) snapped to a cell so it does not swim. The one thing done differently, and the whole point of the
//rework: the terrain NORMAL is taken PER PIXEL from the height field's own gradient, not interpolated from
//per-vertex normals. A coarse mesh's per-vertex normal creates a faint facet/grid pattern across the
//surface (Mach bands at every cell edge); evaluating the gradient per pixel makes the shading smooth
//regardless of tessellation, so the grid is gone. TerrainHeight is a handful of sines, cheap to tap thrice.
//Grass is dry gold-green, varied in patches and combed by the wind; the field takes the dome's mood and the
//shared cloud shadows drift across it. Shader Model 5.0.

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

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), and the terrain shape dials
float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground (the flat clearing otherwise slices
//across the funnel just below its rim, hiding its depth and swallowing the balls falling through). The Testbed
//sets this to the island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

float SavannaLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

float SavannaTime;
float2 WindDirection;

//Grass (linear): the greener and the drier golden shade it varies between in patches, how much sky fills the
//flats, and the distance the field melts into the skyline over
float3 GrassColor;
float3 GrassColorDry;
float3 GrassColorBare;
float AmbientStrength;
float HorizonHazeDistance;

//Wind combing the grass: band speed, spacing and depth
float WindRippleSpeed;
float WindRippleFrequency;
float WindRippleStrength;

//Fine grass texture (a normal-tilting height field): amplitude and blades-per-world-unit
float GrassReliefStrength;
float GrassReliefFrequency;

//Scene point lights (the savanna's campfire) that light the grass under every dome, same as InstancedModel.fx.
//Colours are linear radiance.
#define MAX_SCENE_LIGHTS 8
float3 SceneLightPosition[MAX_SCENE_LIGHTS];
float3 SceneLightColor[MAX_SCENE_LIGHTS];
float SceneLightRange[MAX_SCENE_LIGHTS];
int SceneLightCount;

//Gentle rolling savanna: smooth low sines, flat within the clearing around the origin (where the island
//stands) and rising into low rises with distance. Kept flatter than the meadow's hills - a savanna is open.
float TerrainHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float rolling = 0.5 * sin(dot(p, float2(0.016, 0.012)))
        + 0.3 * sin(dot(p, float2(-0.011, 0.020)) + 1.5)
        + 0.2 * sin(dot(p, float2(0.026, 0.021)) + 3.0);

    //Gentle undulation even inside the clearing, so the near ground is not a dead-flat plane (two crossing
    //swells rather than one). Kept low enough that the crests clear the island's foot.
    float gentle = ClearingRelief * (sin(dot(p, float2(0.04, 0.03))) + 0.6 * sin(dot(p, float2(-0.055, 0.048)) + 2.1));

    return SavannaLevelY + gentle + HillHeight * ramp * (rolling * 0.5 + 0.5);
}

struct SavannaVertexInput
{
    float4 Position : POSITION0;
};

struct SavannaVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

SavannaVertexOutput SavannaVS(SavannaVertexInput input)
{
    SavannaVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float3 worldPosition = float3(xz.x, TerrainHeight(xz), xz.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//Tangent-free normal tilt from a height field (Christian Schueler), as everywhere else in this project
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

//How far the grass is stretched ALONG the wind. The comb is what the two crossed sines below used to supply
//for free - one of them dominated, so the field had a grain - and isotropic noise has no grain at all: it
//reads as gravel rather than as grass lying over. Stretching the domain along the wind elongates the
//features the same way, which is the look, without a single plane wave in it.
static const float GRASS_COMB_STRETCH = 2.6;

//The gain that carries fBm to the amplitude the sines had. NOT cosmetic, and NOT to be folded into
//GrassReliefStrength, which is the authored dial and means the same thing in both scenes: two crossed sines
//have an RMS near 0.51 (0.6 and 0.4 in quadrature), while three octaves of Fbm2 sum to about 0.10 - gradient
//noise clusters hard around zero, one sigma 0.18, and the octave weights add in quadrature. Carrying the old
//strength over unchanged would have made the relief a fifth of what it was, which on a normal-tilting field
//is the effect gone rather than the effect softened. This is the trap #97 hit on the acacia crowns.
static const float GRASS_FBM_GAIN = 5.0;

//Fine grass texture that drifts on the wind, band-limited against the footprint so it fades to smooth grass
//towards the horizon rather than aliasing.
//
//THREE OCTAVES OF GRADIENT NOISE, not the two crossed plane-wave sines this used to be. Two plane waves
//crossing ARE a lattice - that is what their interference is - and these crossed at 93.4 degrees, so the
//lattice was very nearly square and read in perspective as a field of diamonds across the middle distance
//(#117). At GrassReliefFrequency 2 the two periods were 3.14 and 1.75 world units, which is the scale the
//diamonds appeared at. It showed as strongly as it did because the field feeds PerturbNormalFromHeight, so
//it tilts the NORMAL and lands in the shading rather than merely in the colour.
//
//This is the failure Noise.fxh's own opening documents - "a sum of plane-wave sines keeps its planes however
//many terms it has" - and the one #86 removed from Mountain.fx's peaks. Two terms is the smallest case of
//it, and being only two they never even get the chance to hide each other. Octaves of gradient noise on a
//rotated domain have no planes to keep.
//⚠ GRASS SWAYS, IT DOES NOT TRAVEL (#276). This sampled at `(xz + WindDirection * SavannaTime * 0.7)` — a
//flat 0.7 world units a second, for ever, which at GrassReliefFrequency 2 slides the blades' own texture
//across the ground it is rooted in at 1.4 features a second. #276 was filed against the meadow and the
//desert; this scene carried the identical line and so does the forest, which is the #117/#170 story over
//again — the meadow was a line-for-line copy of THIS file, and copying it copied the fault. The lean is the
//gust field's own value now, bounded to about an eighth of a feature either side of where the grass stands.
static const float GRASS_SWAY_REACH = 0.16;

float GrassRelief(float2 xz, float footprint, float gust)
{
    float f = GrassReliefFrequency;
    float2 p = xz * f + WindDirection * (gust * GRASS_SWAY_REACH);

    //Combed along the wind, and the footprint scaled by the same factor the domain is — Fbm2BandLimited's
    //stated contract, which Fbm2Combed passes straight through.
    return Fbm2Combed(p, WindDirection, GRASS_COMB_STRETCH, 3, footprint * f) * GRASS_FBM_GAIN * GrassReliefStrength;
}

float4 SavannaPS(SavannaVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float footprint = length(fwidth(worldPosition.xz));

    //The base terrain normal, taken PER PIXEL from the height field's gradient (three cheap taps) rather than
    //interpolated from per-vertex normals - this is what removes the coarse mesh's facet/grid pattern.
    float e = 1.5;
    float h = TerrainHeight(worldPosition.xz);
    float hx = TerrainHeight(worldPosition.xz + float2(e, 0.0));
    float hz = TerrainHeight(worldPosition.xz + float2(0.0, e));
    float3 baseNormal = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));

    //ONE gust field, and everything the wind does reads off it (#276): the grass leans by it here and the
    //shading darkens by it below. The speed handed over is the old plane wave's own phase speed,
    //WindRippleSpeed / WindRippleFrequency, so the gusts cross the field at the rate the dials always meant.
    float gust = WindGust(worldPosition.xz, WindDirection, SavannaTime, WindRippleFrequency,
        WindRippleSpeed / max(WindRippleFrequency, 1e-4), footprint);

    //Fine grass texture tilts it, so the grass catches the light unevenly and the wind reads on it
    float relief = GrassRelief(worldPosition.xz, footprint, gust);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    //Three-tone grass: dry gold as the base, green flushes where it is lusher, and patches of bare reddish
    //earth. Sampled at several noise scales so the field reads varied and alive, not one flat tone.
    float patchLarge = CloudNoise(worldPosition.xz * 0.012) * 0.5 + 0.5;   //broad green vs gold zones
    float patchMed = CloudNoise(worldPosition.xz * 0.05 + 17.0) * 0.5 + 0.5;
    float bare = CloudNoise(worldPosition.xz * 0.09 + 60.0) * 0.5 + 0.5;   //scattered bare earth

    //Green over most of the field, drying to gold in patches - a lusher savanna than the all-gold first pass.
    float3 grass = lerp(GrassColorDry, GrassColor, saturate((patchLarge - 0.12) * 1.9) * (0.7 + 0.3 * patchMed));
    grass = lerp(grass, GrassColorBare, smoothstep(0.72, 0.85, bare) * 0.55);

    //Wind combing the grass: the gust computed above, over the blades it lays down. Same dial and the same
    //range it always had; what it is applied to is a travelling patch rather than an infinite plane wave
    //42 world units across (#276 — see WindGust in Noise.fxh).
    grass *= 1.0 + gust * WindRippleStrength;

    //Matte grass: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds that
    //drift across the sky sweep their shadows over the field
    float sunlight = CloudSunlight(worldPosition, SunDirection);
    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    //Scene point lights (the campfire) warming the grass around them, on top of sun and sky
    float3 sceneLight = float3(0.0, 0.0, 0.0);
    [loop]
    for (int i = 0; i < SceneLightCount; i++)
    {
        float3 toL = SceneLightPosition[i] - worldPosition;
        float dist = length(toL);
        float3 L = toL / max(dist, 1e-4);
        float atten = saturate(1.0 - dist / SceneLightRange[i]);
        atten *= atten;
        sceneLight += SceneLightColor[i] * (saturate(dot(normal, L)) * atten);
    }

    float3 color = grass * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight + sceneLight);

    //Horizon haze: the distant field softens into the skyline
    float dist = distance(CameraPosition, worldPosition);
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, HorizonColor, haze * haze);

    return float4(color, 1.0);
}

technique Savanna
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SavannaVS();
        PixelShader = compile PS_SHADERMODEL SavannaPS();
    }
};
