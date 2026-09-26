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
//The sun's cast shadows (#469): the acacias, the rocks and the mounds on the grass, off SceneRenderer's map.
#include "Shadows.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

#include "FarField.fxh"
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

//Game trails (#451): bare earth worn through the grass along the zero contours of one low-frequency noise,
//which wander and fork the way trodden paths do and carry no plane wave. How bare (0 skips the term), how
//wide the zero band is, and how closely the paths wander.
float TrailStrength;
float TrailWidth;
float TrailFrequency;

//Where a path has to step aside, and which way (#476). A small CPU-built RG field over the plain: the
//trail's noise is sampled at the warped position, so a contour that would have crossed a tree is pushed out
//and arrives as a BEND. It is the answer to "nobody wears a track through a trunk", and the reach is wide
//on purpose - a person crossing open ground sees the tree coming and is already going round it.
//
//Zero TrailWarpAmount leaves the sampling exactly where it was, which is what every scene but the savanna
//gets and what this one had before #476.
texture TrailWarpTexture;
sampler2D TrailWarpSampler = sampler_state
{
    Texture = <TrailWarpTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

float TrailWarpExtent;
float TrailWarpAmount;

//THE GRASS AS A MATERIAL (#281), read off references rendered for it (a savanna field, the same field towards a low
//sun, bunch grass from above): savanna grass grows in separate tufts with the red earth showing between them, its
//dry tips are pale straw, and the field has a sheen seen edge-on and glows gold looking into the sun. The meadow
//got the same treatment first; the dials mean the same things here.
float3 GrassTipColor;
float GrassTipStrength;
float TuftSize;
float TuftStrength;
float GrassSheenStrength;
float GrassTranslucency;

//Scene point lights (the savanna's campfire) that light the grass under every dome, same as InstancedModel.fx.
//Colours are linear radiance.
#define MAX_SCENE_LIGHTS 8
float3 SceneLightPosition[MAX_SCENE_LIGHTS];
float3 SceneLightColor[MAX_SCENE_LIGHTS];
float SceneLightRange[MAX_SCENE_LIGHTS];
int SceneLightCount;

//The fires' hearths (#282): the ground each campfire has burnt into the grass. The positions are the fires'
//own - SceneRenderer pushes SavannaCampfirePosition for each - and they are passed SEPARATELY from the scene
//lights above rather than read off them, even though on this scene the two arrays hold the same points
//today. A scene light is not necessarily a fire, and a light added here for any other reason must not burn a
//hole in the grass under it.
#define MAX_HEARTHS 8
float3 HearthPosition[MAX_HEARTHS];
int HearthCount;
float HearthRadius;      //char at the fire's foot out to grass again at this distance
float HearthNear, HearthFar;   //how far the nearest and furthest fire stand from the world origin: the early-out below
float3 HearthAsh;        //the burnt ring's cold ash (linear)
float3 HearthChar;       //and the near-black char at the fire's own foot

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

//How burnt this spot is: 1 in the char at a fire's foot, 0 where the grass has it back at HearthRadius.
//
//The NEAREST fire wins rather than the sum. Two hearths whose rings overlap would otherwise burn each other
//blacker than either fire can, and the ring the config lays out is even enough that a Count high enough to
//touch would char the whole circle black.
//
//The early-out is what keeps this off the rest of the field: the fires all stand on one ring around the
//origin, so a pixel whose own radius is outside that ring by more than a hearth cannot be in any of them.
//HearthNear/HearthFar are measured in C# FROM THE FIRES' OWN POSITIONS rather than derived from the config's
//ring rule a second time here - the placement lives in one place (SceneRenderer.SavannaCampfirePosition) and
//this reads the answer, not the rule. The branch is divergent, but pixels are coherent enough that a
//wavefront agrees, and nothing inside it takes a derivative.
float HearthBurn(float2 xz)
{
    float r = length(xz);
    if (r < HearthNear - HearthRadius || r > HearthFar + HearthRadius) return 0.0;

    float burn = 0.0;
    [loop]
    for (int i = 0; i < HearthCount; i++)
    {
        float d = distance(xz, HearthPosition[i].xz);
        burn = max(burn, 1.0 - smoothstep(HearthRadius * 0.22, HearthRadius, d));
    }

    //A fire does not burn a circle. One noise tap breaks the edge up, taken once for the pixel rather than
    //per fire - two hearths never overlap far enough for the difference to show, and it keeps the tap out of
    //the loop. Below the early-out, so it costs nothing on the field at large.
    return saturate(burn * (0.82 + 0.36 * (CloudNoise(xz * 0.45) * 0.5 + 0.5)));
}

float4 SavannaField(SavannaVertexOutput input, bool detail)
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);
    FarRingClip(worldPosition.xz);

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

    //How burnt this spot is (#282): the campfires ring the island and each has burnt the grass under it.
    float burn = HearthBurn(worldPosition.xz);

    //How trodden it is (#451): a game trail is grass worn away to the earth, so it takes the hearth's own
    //route through the material below - the relief, the tips, the strokes and the sheen all read off
    //(1 - worn), and a trail is a milder wear than a fire. One noise tap, band-limited by hand: the trail's
    //own width in world units is TrailWidth / TrailFrequency, and past the footprint where it is under a
    //pixel wide it fades out rather than aliasing into a shimmer of dots along the horizon.
    float trail = 0.0;
    [branch]
    if (TrailStrength > 0.0)
    {
        //The step aside, read off the field the planting built (#476). tex2Dlod rather than tex2D: this is
        //inside a [branch] and a gradient instruction in divergent flow is what the compiler refuses. Clamped
        //addressing means the field simply runs out past the plain's edge, where there is nothing to go round.
        float2 warp = float2(0.0, 0.0);
        if (TrailWarpAmount > 0.0)
        {
            float2 warpUv = worldPosition.xz / TrailWarpExtent + 0.5;
            warp = (tex2Dlod(TrailWarpSampler, float4(warpUv, 0.0, 0.0)).rg * 2.0 - 1.0) * TrailWarpAmount;
        }

        float contour = abs(CloudNoise((worldPosition.xz + warp) * TrailFrequency + 33.0));
        float trailFade = saturate(1.0 - footprint * TrailFrequency / max(TrailWidth, 1e-4) * 0.5);
        trail = (1.0 - smoothstep(TrailWidth * 0.5, TrailWidth, contour)) * trailFade * TrailStrength;
    }
    //Where the blades are gone, by either route: the hearth's char or the trail's wear. The COLOUR of the two
    //stays separate below - a hearth is ash and char, a trail is the red earth the grass grew out of.
    float worn = max(burn, trail);

    //CLUMPS (#281): savanna grass grows in bunches, and a bunch is its own shade of dry gold or green - a noise one
    //bunch across, band-limited as a whole, and one of the reduced program's two cuts. No more than that, and the
    //layouts that tried for more were each rejected on sight: jittered discs with earth round each read as polka
    //dots, the same discs raised into domes in the relief read as a field of pebbles (green stone, the very report
    //this answers), and the meadow's clump seams - dark lines along a noise's zero-crossings - drew a web of worm-like
    //cracks under a high sun, which reads as dried mud. What makes grass read as grass at a field's distance is
    //fibre, not form: the tips, the strokes, the sheen and the glow below.
    float clumpShade = 0.0;
    if (detail)
    {
        float clumpFade = saturate(1.0 - 2.5 * footprint / max(TuftSize, 1e-3)) * (1.0 - worn);
        clumpShade = GradientNoise2(worldPosition.xz / max(TuftSize, 1e-3) + 5.1) * clumpFade;
    }

    //Fine grass texture tilts it, so the grass catches the light unevenly and the wind reads on it - and it
    //fades out with the char, because what that relief is a texture OF is blades, and a hearth has none.
    float relief = GrassRelief(worldPosition.xz, footprint, gust) * (1.0 - worn * 0.85);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    //Three-tone grass: dry gold as the base, green flushes where it is lusher, and patches of bare reddish
    //earth. Sampled at several noise scales so the field reads varied and alive, not one flat tone.
    float patchLarge = CloudNoise(worldPosition.xz * 0.012) * 0.5 + 0.5;   //broad green vs gold zones
    float patchMed = CloudNoise(worldPosition.xz * 0.05 + 17.0) * 0.5 + 0.5;
    float bare = CloudNoise(worldPosition.xz * 0.09 + 60.0) * 0.5 + 0.5;   //scattered bare earth

    //Green over most of the field, drying to gold in patches - a lusher savanna than the all-gold first pass.
    float3 grass = lerp(GrassColorDry, GrassColor, saturate((patchLarge - 0.12) * 1.9) * (0.7 + 0.3 * patchMed));
    grass = lerp(grass, GrassColorBare, smoothstep(0.72, 0.85, bare) * 0.55);

    grass *= 1.0 + TuftStrength * 0.45 * clumpShade;
    grass = lerp(grass, grass * float3(1.12, 1.0, 0.78), TuftStrength * saturate(clumpShade * 1.5));

    //Wind combing the grass: the gust computed above, over the blades it lays down. Same dial and the same
    //range it always had; what it is applied to is a travelling patch rather than an infinite plane wave
    //42 world units across (#276 — see WindGust in Noise.fxh).
    grass *= 1.0 + gust * WindRippleStrength * (1.0 - worn);

    //TIPS AND HOLLOWS (#281), off the very relief that tilts the normal: where the combed field stands high the dry,
    //pale tips are showing, where it dips the eye is looking down between the blades. Normalised to the relief's own
    //amplitude, and it needs no band limit of its own - the relief's octaves already fade with the footprint.
    float blade = relief / max(GrassReliefStrength * GRASS_FBM_GAIN, 1e-4);
    float tipCover = 1.0 - worn;
    grass *= 1.0 + 0.32 * GrassTipStrength * clamp(blade, -1.0, 1.0) * tipCover;
    grass = lerp(grass, GrassTipColor, 0.45 * GrassTipStrength * saturate(blade) * tipCover);

    //BLADES SEEN FROM THE SIDE (#281), the meadow's strokes: a fine noise stretched along the ground towards the
    //camera projects to upright strokes in front of the lens - longer here, the savanna's grass being tall. The
    //reduced program's other cut.
    if (detail)
    {
        float strokeFrequency = GrassReliefFrequency * 3.0;
        float strokes = Fbm2Combed(worldPosition.xz * strokeFrequency, CameraPosition.xz - worldPosition.xz,
            6.0, 2, footprint * strokeFrequency);
        grass *= 1.0 + 0.85 * GrassTipStrength * strokes * tipCover;
        grass = lerp(grass, GrassTipColor, 0.5 * GrassTipStrength * saturate(strokes * 2.0) * tipCover);
    }

    //The trail's earth (#451): the ground's own bare-patch colour, redder and lighter for being trodden
    //dust rather than shaded soil under grass, worn through where the path runs.
    grass = lerp(grass, GrassColorBare * float3(1.5, 1.15, 0.95), trail);

    //And the hearth over the top of all three tones: ash across the burnt ring, char at the fire's own foot.
    //Two steps rather than one lerp so the patch has an edge INSIDE it - a fire pit is a dark eye in a pale
    //ring, and a single fade from black out to grass is a smudge.
    grass = lerp(grass, HearthAsh, saturate((burn - 0.12) * 1.6));
    grass = lerp(grass, HearthChar, smoothstep(0.78, 1.0, burn));

    //Matte grass: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds that
    //drift across the sky sweep their shadows over the field
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows (#469), into the same sunlight factor the clouds dim: the sun term, the sheen and
    //the backlit glow all read off it, so shadowed grass neither glints nor glows. The base normal rather than
    //the combed one, so the blades' relief does not put the field in and out of its own bias.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

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

    //THE SHEEN AND THE GLOW (#281), the meadow's two: a field seen edge-on is a sea of tips catching the light, and a
    //blade is thin, so with the sun behind it light comes through - gold here, which is the savanna at the hour its
    //default dome is set to. Off the base normal, since both are about how the FIELD is seen; kept off the char.
    float3 toCamera = normalize(CameraPosition - worldPosition);
    float grazing = 1.0 - saturate(dot(baseNormal, toCamera));
    grazing *= grazing * grazing;
    float bladeCover = 1.0 - worn;

    float3 sheenColor = lerp(grass, GrassTipColor, 0.5) + 0.1;
    color += bladeCover * GrassSheenStrength * grazing * sheenColor
        * (skyAmbient * AmbientStrength + SunColor * (0.35 * sunlight));

    float towardSun = saturate(dot(-toCamera, SunDirection));
    float backlit = towardSun * towardSun;
    backlit *= backlit * backlit;
    color += bladeCover * GrassTranslucency * backlit * sunlight * (0.35 + 0.65 * pow(grazing, 0.33))
        * SunColor * lerp(grass, GrassTipColor, 0.7);

    //Horizon haze: the distant field softens into the skyline
    float dist = distance(CameraPosition, worldPosition);
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, HorizonColor, haze * haze);

    return float4(FarFadeToSky(color, worldPosition), 1.0);
}

float4 SavannaPS(SavannaVertexOutput input) : COLOR { return SavannaField(input, true); }
float4 SavannaReducedPS(SavannaVertexOutput input) : COLOR { return SavannaField(input, false); }

technique Savanna
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SavannaVS();
        PixelShader = compile PS_SHADERMODEL SavannaPS();
    }
};

//Two programs from one body (#281), the meadow's and the forest's pattern. "Savanna" is the authored field;
//"SavannaReduced" gives up the tuft gaps and the blade strokes and keeps everything that is arithmetic on values
//already computed. SceneRenderer.SceneDetail picks; the Game's Low tier takes the reduced one.
technique SavannaReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SavannaVS();
        PixelShader = compile PS_SHADERMODEL SavannaReducedPS();
    }
};

//--- The height probe (#590) ----------------------------------------------------------------------------

//TerrainMirror.Savanna's field, for the Testbed's mirrorcheck (see HeightProbe.fxh).
#define HEIGHT_PROBE_MIRRORED(p) TerrainHeight(p)
#include "HeightProbe.fxh"
