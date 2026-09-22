//The flank of an erupting volcano (#223): black basalt and heaped scoria raked by gullies, rivers of
//red-orange lava running down them and out past the arena, thin incandescent rivulets threading down the
//cone from its crater, and a lava lake in the crater itself. The cone stands off to one side with its
//crater against the sky; the lava fountains and the smoke plume over it are LavaFountain.fx and the drifting
//ash is Ash.fx, both drawn after this.
//
//The machinery is the desert's and the mountain's: a camera-centred grid (360 a side, 32-bit indices - see
//CreateGridMesh) snapped to its cell so it does not swim, displaced in the vertex shader, its base normal by
//finite differences per vertex and a fine relief perturbing it per pixel. What is new here is that the GROUND
//IS THE LIGHT. Every other terrain shader in this project takes its whole radiance from the sun and the dome;
//this one adds an emissive band of its own - narrow, and that is deliberate. Red and orange balls hang over
//this scene, so the rivers are kept thin and hot rather than wide and warm, and what the flows throw back on
//the cluster is a capped point light (SceneLights, VolcanoLightStrength), not a tint over the frame.
//
//DRAWN FROM REFERENCES (#509), and every one of them said the same three things the first build had the other
//way round. (1) A lava flow is mostly DARK: a skin of chilled crust carried on the melt, the incandescence
//showing as a core down the middle of the channel and as thin cracks and streamlines - and the lines run
//ALONG the flow, because the melt shears downstream. The first build lit a honeycomb of cells over the whole
//width, which read as a giraffe's hide laid on the cone. (2) The flank of an erupting cone is black rock with
//a few thin glowing threads running down it from the crater, not a glowing web over the whole summit. (3)
//Cooled lava is GLASSY: black pahoehoe reflects the sky as a slate sheen, which is the only thing that tells
//a lava field from a heap of soot at night. The drawings are local renders (the design-references skill,
//C:\Users\panrd\AI\sd\out\509) and nothing of them is in this repository; docs/scenes.md "The volcano" has
//what each one settled.
//
//Shader Model 5.0, drawn in all three executables out of the one Testbed content directory.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
//The sun's cast shadows (#469, in this scene since #471): SceneRenderer renders the map before the scene
//pass and this reads it. See Shadows.fxh for what casts and what a receiver owes.
#include "Shadows.fxh"
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

float2 OriginXZ;
float VolcanoTime;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground. The Testbed sets this to the
//island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

//--- The massing -------------------------------------------------------------------------------------------

float VolcanoLevelY;
float ClearingRadius;
float ClearingTransition;

float2 ConeCenterXZ;
float ConeRadius;
float ConeHeight;
float ConeProfile;
float CraterRadius;
float CraterDepth;

//GullyCount arrives already ROUNDED TO AN INTEGER from the host, and it has to be: every bearing term below
//is a multiple of it, and only an integer multiple of atan2's angle closes seamlessly across the ±π seam. A
//fractional count would leave a straight scar running from the crater to the horizon along -X.
float GullyDepth;
float GullyCount;
float ScoriaRelief;

//--- The rivers --------------------------------------------------------------------------------------------

//Matched by MAX_RIVERS in SceneRenderer. Rivers are radial from the cone's axis: a bearing each, a reach
//each (how far down the flank the flow gets before its front stops), computed once by the host so the CPU
//side can put a moving point light on the same river the shader draws.
#define MAX_RIVERS 6
float RiverBearing[MAX_RIVERS];
float RiverReach[MAX_RIVERS];
int RiverCount;

float RiverWidth;
float RiverWander;
float RiverSpeed;
float HaloWidth;

float3 RockColor;
float3 RockColorLight;
float3 ScoriaColor;
float3 LavaHot;
float3 LavaCool;
float3 CrustColor;
float CrustGlow;
float CrackGlow;
float PlateSize;
float SheenStrength;
float RivuletStrength;
float FieldCrackStrength;

float AmbientStrength;
float HorizonHazeDistance;
float3 HazeTint;
float HazeStrength;
float2 WindDirection;

static const float TWO_PI = 6.2831853;

//--- The form's own figures ---------------------------------------------------------------------------------
//Constants and not config dials, the meadow's rosette rule: they say what SHAPE a thing is, and a designer
//turning one of them is redesigning the feature rather than tuning its look.

//A flow's open core, as a fraction of its half-width: nearly the whole channel just below the vent, pinched
//to a thread by the time the front is reached - the melt crusts over from the banks inwards as it runs.
static const float CORE_AT_VENT = 0.85;
static const float CORE_AT_FRONT = 0.22;

//The streamlines: stripes lying along the flow, STREAM_FREQUENCY radians of phase per world unit across it,
//their phase bent by a noise STREAM_STRETCH times longer along the flow than across it.
static const float STREAM_FREQUENCY = 2.2;
static const float STREAM_STRETCH = 12.0;

//The rivulets on the cone. Sampled on a circle of this radius in noise space (seam-free round the axis - see
//Rivulets), so it is also roughly how many lines run down the whole cone divided by five; RIVULET_RADIAL is
//how fast a line wanders as it descends, per world unit.
static const float RIVULET_ANGULAR = 5.0;
static const float RIVULET_RADIAL = 0.011;
static const float RIVULET_WIDTH = 0.03;

//The glowing cracks in the lava field: world frequency of the line field, and a crack's half-width in world
//units - under a pixel from the play camera, which ContourLine turns into a dimmer line rather than a gap.
static const float FIELD_CRACK_SCALE = 0.045;
static const float FIELD_CRACK_HALF_WIDTH = 0.22;

//--- Height field ------------------------------------------------------------------------------------------

//The cone's analytic massing at a world XZ, relative to VolcanoLevelY and BEFORE the clearing ramp: the
//flank, the crater bitten out of its summit and the gullies raked down it. Mirrored term for term by
//SceneRenderer.VolcanoGroundY, which places the vents and the rivers' lights on this surface - the one
//term that mirror leaves out is the scoria fBm below, whose few units are invisible under a lamp.
float VolcanoMassing(float2 p)
{
    float2 d = p - ConeCenterXZ;
    float r = length(d);
    float bearing = atan2(d.y, d.x);

    //Shallow at the foot, steepest across the middle of the flank, rounding off at the summit - the profile
    //of a young stratovolcano rather than the straight-sided cone pow(t, 1) would give.
    //
    //THE RADIUS FED IN HERE IS CLAMPED TO CraterRadius, not r itself - #223's own shipped bug, found by
    //looking at the summit rather than by the maths. Evaluated at r the cone's flank is maximal exactly at
    //r = 0 for any ConeProfile > 0 (its slope there is -ConeProfile / ConeRadius, never zero), so
    //subtracting a crater term that is ALSO maximal at r = 0 only steepens the final approach to a point -
    //it cannot put the true summit anywhere but dead centre, whatever CraterDepth is. The clamp is what
    //moves the peak: the flank now plateaus at its own r = CraterRadius height for every r inside it, so
    //that plateau - not a receding point - is the surface the crater below is cut into.
    float flankRadius = max(r, CraterRadius);
    float t = saturate(1.0 - flankRadius / ConeRadius);
    float flank = ConeHeight * pow(t, ConeProfile);

    //The bowl cut into that plateau: zero at the rim (r = CraterRadius, where it must join the ordinary
    //flank with no step - and does, since the clamp above holds the plateau at exactly the unclamped
    //flank's own value there) and CraterDepth deep at the vent. The rim itself is not drawn taller than the
    //plateau it sits on; it reads as a rim only because the bowl falls away on one side of it and the
    //ordinary flank falls away on the other - a real crater's whole shape, not an added lip.
    float crater = CraterDepth * smoothstep(CraterRadius, 0.0, r);

    //Radial gullies, absent at the crater rim and at the foot, deepest across the flank the lava runs down.
    //The inner sine bends them so they are not a clean starburst.
    //
    //SceneRenderer.SnapToGully solves this same term for its floors and lays the rivers in them, so the
    //flows run where the ground drains. Change the expression and change that one with it.
    float rake = 0.5 - 0.5 * cos(bearing * GullyCount + 2.0 * sin(bearing * 3.0));
    float gullyBand = smoothstep(CraterRadius * 1.3, ConeRadius * 0.30, r)
        * smoothstep(ConeRadius * 1.15, ConeRadius * 0.85, r);

    return flank - crater - GullyDepth * rake * gullyBand;
}

//The terrain displacement at a world XZ: flat at the island's foot, rising into the flank with distance from
//the ARENA (not from the cone), so the play surface sits in a clearing exactly as it does in every other
//terrain scene. Evaluated three times per vertex for the finite-difference normal.
float TerrainHeight(float2 p)
{
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, length(p));

    //Broken scoria over the whole field. Mean zero, so it does not lift the clearing as the ramp opens - the
    //failure Desert.fx's trailing constant exists to prevent.
    float scoria = ScoriaRelief * Fbm2(p * 0.038, 4);

    return VolcanoLevelY + ramp * (VolcanoMassing(p) + scoria);
}

//--- Rivers ------------------------------------------------------------------------------------------------

//Signed angular difference wrapped into (-π, π], so a river whose bearing sits near the seam is still one
//river and not two half ones.
float AngleDelta(float a, float b)
{
    float d = a - b;
    return d - TWO_PI * floor((d + 3.14159265) / TWO_PI);
}

struct RiverSample
{
    float Mask;     //1 on the flow, feathered to 0 at its edge
    float Halo;     //a wider, softer field: the band of ground the flow is heating
    float Along;    //distance down the flank, already scrolled - the flow's own coordinate
    float Across;   //SIGNED distance across the flow from its centre line, so a pattern drawn in it is not
                    //mirrored about that line
    float HalfWidth;//the nearest flow's own half-width here, which Across is read against
    float Reach;    //how far from the cone's axis the nearest flow's front stops
    float ConeR;    //distance from the cone's axis, which the summit's own heat is a function of
};

//The nearest river at a world point. A loop over a UNIFORM count (uniform flow control, no divergence) with
//no gradient operation inside it, which is what lets the whole thing run unbranched: everything the pixel
//needs from the rivers comes out of this one call, on the flow and off it alike.
RiverSample SampleRivers(float2 p, float footprint)
{
    float2 d = p - ConeCenterXZ;
    float r = length(d);
    float bearing = atan2(d.y, d.x);

    RiverSample best;
    best.Mask = 0.0;
    best.Halo = 0.0;
    best.Along = 0.0;
    best.Across = 1e6;
    best.HalfWidth = RiverWidth;
    best.Reach = ConeRadius;
    best.ConeR = r;

    for (int i = 0; i < RiverCount; i++)
    {
        //A flow wanders on the way down rather than running true - the wander is a function of the RADIUS,
        //so it bends the river's course and does not merely wobble its edges.
        float wander = RiverWander * sin(r * 0.017 + i * 2.13) * saturate(r / ConeRadius);
        float across = AngleDelta(bearing, RiverBearing[i] + wander) * r;

        //The front: the flow thins and stops somewhere down the flank, each river at its own reach, with a
        //noisy edge so it ends in a lobed front rather than on a circle.
        float front = 1.0 - smoothstep(RiverReach[i] * 0.82, RiverReach[i] + 24.0 * GradientNoise2(p * 0.01 + i), r);

        //Feathered against the pixel footprint as well as the width, or a river narrower than a pixel out
        //near the horizon turns into a crawling dashed line. And NARROW UNDER THE SUMMIT (#509): a flow
        //leaves its vent as a channel and spreads as the slope eases, and at full width from the rim the five
        //flows between them paved the whole upper cone - which no reference drew.
        float halfWidth = RiverWidth * (0.75 + 0.45 * sin(r * 0.026 + i * 1.7))
            * lerp(0.4, 1.0, smoothstep(CraterRadius, ConeRadius * 0.55, r));
        float edge = footprint * 0.5;
        float mask = (1.0 - smoothstep(halfWidth * 0.55 - edge, halfWidth + edge, abs(across))) * front;

        //The heated BAND either side of the flow. HaloWidth is a multiple of the river's own half-width, and
        //it is a dial because the first pass had it at six and the arena stands on the river that passes it:
        //the halo swallowed the whole foreground and the entire plain glowed orange.
        float halo = (1.0 - smoothstep(halfWidth, halfWidth * HaloWidth, abs(across))) * front;

        //The nearest flow wins the coordinates the crust is drawn in; the masks take the strongest, so two
        //rivers that meet high on the flank merge instead of one cancelling the other.
        bool nearer = abs(across) < abs(best.Across);
        best.Across = nearer ? across : best.Across;
        best.Along = nearer ? r - VolcanoTime * RiverSpeed : best.Along;
        best.HalfWidth = nearer ? halfWidth : best.HalfWidth;
        best.Reach = nearer ? RiverReach[i] : best.Reach;

        best.Mask = max(best.Mask, mask);
        best.Halo = max(best.Halo, halo);
    }

    return best;
}

//A thin bright line on the zero contour of a noise value, band-limited against how many noise units one
//pixel spans. Near, a hairline of full strength with a crisp edge; far, the line WIDENS to the pixel and
//DIMS in proportion, so its average brightness over an area stays what it was near instead of either
//crawling or vanishing. The flat core is what makes it a line: a profile falling off from its centre all
//the way out reads as a soft glowing worm at every distance, which is what the first cut drew.
float GlowLine(float n, float width, float unitsPerPixel)
{
    float spread = width + unitsPerPixel;
    return (1.0 - smoothstep(width * 0.5, spread, abs(n))) * (width / spread);
}

//The cracks in the lava field: a hairline on the zero contour of a two-octave noise, a fixed WORLD width wide
//and antialiased off the noise's own SLOPE - |n| / (|grad n| * footprint) is the distance to the contour in
//pixels, to first order, however steep or shallow the field happens to be there. The first cut thresholded |n|
//against one assumed steepness, as the rivulets do, and near a saddle of the noise - where the field lingers
//close to zero across a wide area - that painted fat orange blobs. The second took the slope as fwidth(n),
//which cannot sit in a branch, so every pixel of the field paid for a crack only a few patches have. The
//slope comes analytically out of Clouds.fxh's CloudNoiseD instead, so this runs inside one. Two octaves, so
//the line is jagged the way a crack in rock is; one octave's contour is a smooth curve and reads as a glowing
//wire laid on the ground. Narrower than a pixel, the line dims in proportion rather than thinning away.
float FieldCracks(float2 xz, float footprint)
{
    float2 q = xz * FIELD_CRACK_SCALE;
    float3 coarse = CloudNoiseD(q);
    float3 fine = CloudNoiseD(q * 2.7 + 3.1);

    float n = coarse.x + 0.35 * fine.x;
    float2 slope = (coarse.yz + 0.35 * 2.7 * fine.yz) * FIELD_CRACK_SCALE;

    float pixels = abs(n) / max(length(slope) * footprint, 1e-6);
    float halfPixels = FIELD_CRACK_HALF_WIDTH / max(footprint, 1e-4);
    return (1.0 - smoothstep(halfPixels, halfPixels + 1.0, pixels)) * saturate(2.0 * halfPixels)
        * saturate(1.0 - footprint * FIELD_CRACK_SCALE * 4.0);
}

//--- Vertex ------------------------------------------------------------------------------------------------

struct VolcanoVertexInput
{
    float4 Position : POSITION0;
};

struct VolcanoVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
};

VolcanoVertexOutput VolcanoVS(VolcanoVertexInput input)
{
    VolcanoVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float height = TerrainHeight(xz);

    //Base normal per vertex, as on the mountain and for the same reason: a per-pixel finite-difference
    //normal on this much distant, steep ground aliases into shimmer, and the per-pixel relief below carries
    //the near detail anyway.
    float e = 2.0;
    float hx = TerrainHeight(xz + float2(e, 0.0));
    float hz = TerrainHeight(xz + float2(0.0, e));
    output.WorldNormal = normalize(float3(-(hx - height) / e, 1.0, -(hz - height) / e));

    float3 worldPosition = float3(xz.x, height, xz.y);
    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//--- Pixel -------------------------------------------------------------------------------------------------

//The flow's own surface at one point, as radiance: the chilled crust it carries, the open core down its
//middle, the streamlines in that core and the cracks in the crust. `detail` is the caller's crackle fade -
//the lines and cracks are hairlines, and past the distance they resolve they are replaced by their own mean.
float3 FlowRadiance(RiverSample river, float footprint, float detail)
{
    //0 on the centre line, 1 at the bank; 0 just below the vent, 1 at the front
    float acrossN = saturate(abs(river.Across) / max(river.HalfWidth, 1e-3));
    float run = saturate((river.ConeR - CraterRadius) / max(river.Reach - CraterRadius, 1.0));

    //The open core: wide under the vent, a thread by the front, and torn wider in slow patches along the
    //way - where the crust has broken up and the melt is showing through.
    float tear = GradientNoise2(float2(river.Along * 0.03, 3.7));
    float coreWidth = lerp(CORE_AT_VENT, CORE_AT_FRONT, run) * (0.75 + 0.5 * tear);
    float core = 1.0 - smoothstep(coreWidth * 0.5, coreWidth, acrossN);

    //The streamlines in the core: bright threads and duller ones lying downstream, side by side. STRIPES
    //whose phase a stretched noise bends, not the zero contour of that noise: a contour closes on itself
    //round every saddle, and the first cut drew the core full of long glowing eyes where the references draw
    //parallel striations. SIGNED across, or the pattern is mirrored about the centre line. Sharpened by the
    //cube, so the bright threads are narrow and the dull ones wide - and faded to the cube's own mean,
    //5/16, as the stripes approach the pixel.
    float bend = GradientNoise2(float2(river.Across * 0.2, river.Along / (STREAM_STRETCH * 1.5)));
    float stripe = 0.5 + 0.5 * cos(river.Across * STREAM_FREQUENCY + 2.5 * bend);
    float stream = lerp(0.3125, stripe * stripe * stripe, saturate(1.5 - footprint * STREAM_FREQUENCY * 0.5));

    //The crust: rafts of chilled skin, stretched downstream because the flow stretches them, with hairline
    //cracks between them. The cells are LONG along the flow on purpose - an isotropic Voronoi here is the
    //honeycomb the first build drew. Warped for the reason every Voronoi in this file is: a jittered
    //lattice still has a lattice in it.
    float2 raftUV = float2(river.Across / (PlateSize * 0.9), river.Along / (PlateSize * 3.2));
    float raftEdge = VoronoiEdge2(raftUV + float2(GradientNoise2(raftUV * 0.35),
                                                  GradientNoise2(raftUV * 0.35 + 11.3)) * 0.6);
    float crack = 1.0 - saturate(raftEdge * 11.0);

    //Past the distance the cracks resolve, their own mean: about a tenth of the crust.
    crack = lerp(0.1, crack, detail);

    //The melt pulses slowly as the supply behind it surges, and cools on its way down.
    float surge = 0.85 + 0.15 * sin(VolcanoTime * 0.7 + river.Along * 0.05);
    float heat = saturate((0.55 + 0.45 * stream) * (1.0 - 0.35 * run) * surge);
    float3 melt = lerp(LavaCool, LavaHot, heat * heat) * (0.55 + 0.45 * heat);

    //What the crust itself radiates: a dull red where it is young and thin, near the vent; its cracks glow
    //through it at a cooler colour than the core does.
    float3 crust = LavaCool * CrustGlow * (1.0 - 0.7 * run)
        + lerp(LavaCool, LavaHot, 0.3) * crack * CrackGlow * (1.0 - 0.5 * run);

    //And the bank: where the crust tears from the levee the melt shows as a thin bright line along the
    //flow's edge, which is what outlines every lobe in the references.
    float bank = smoothstep(0.62, 0.84, acrossN) * (1.0 - smoothstep(0.84, 1.0, acrossN));
    crust += LavaCool * bank * 0.6 * lerp(1.0, 0.5, detail);

    return lerp(crust, melt, core);
}

//The rivulets: thin incandescent threads running down the cone from its crater, a few of them long and most
//short, each wandering as it goes. Every eruption the references drew had them - a cone streaked with fine
//glowing lines, not banded by five wide ones.
//
//Sampled on a CIRCLE in noise space, (cos b, sin b) scaled, and not on the bearing itself: atan2's bearing
//jumps by 2π across -X, and a noise read off it tears along that line from the crater to the foot (the
//cavern's mineral veins shipped with exactly that scar once). The circle is a closed curve in the noise's
//domain, so it comes back round to itself with no seam; the outback's gullies are drawn the same way.
float Rivulets(float2 around, float coneR, float footprint)
{
    float n = GradientNoise3(float3(around * RIVULET_ANGULAR, coneR * RIVULET_RADIAL));
    float thread = GlowLine(n, RIVULET_WIDTH, footprint * RIVULET_ANGULAR / max(coneR, 1.0));

    //Which threads run, and how far each gets: a coarse field round the cone that does not change down it,
    //so a thread is on or off for its whole length and stops at its own reach.
    float gate = GradientNoise3(float3(around * RIVULET_ANGULAR * 0.35, 5.3));
    float active = smoothstep(-0.05, 0.25, gate);
    float reach = ConeRadius * (0.32 + 0.4 * saturate(gate * 1.2 + 0.5));

    float start = smoothstep(CraterRadius * 0.9, CraterRadius * 1.25, coneR);
    float stop = 1.0 - smoothstep(reach * 0.6, reach, coneR);

    return thread * active * start * stop;
}

float4 VolcanoSurface(VolcanoVertexOutput input, uniform bool fullDetail)
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float3 toEye = CameraPosition - worldPosition;
    float dist = length(toEye);
    float3 view = toEye / max(dist, 1e-4);
    float footprint = length(fwidth(worldPosition.xz));

    //How much per-pixel detail this pixel can still resolve. Everything cellular fades out against it well
    //before its cells reach pixel size - a Voronoi web left running to the horizon is a field of crawling
    //sparks, and this one is EMISSIVE, so it would spark straight through the glare pass.
    float detail = saturate(1.0 - footprint * 0.09);
    float crackleFade = saturate(1.0 - footprint * 0.55);

    float3 baseNormal = normalize(input.WorldNormal);

    RiverSample river = SampleRivers(worldPosition.xz, footprint);

    float2 fromCone = worldPosition.xz - ConeCenterXZ;
    float coneR = river.ConeR;
    float2 around = fromCone / max(coneR, 1e-3);

    //--- The rock ----------------------------------------------------------------------------------------
    //Black basalt with patches of weathered grey scoria, and the ground round the summit OXIDISED: every
    //flank the references drew is dark grey below and a dark rust-red towards the crater, where the hot
    //gases have been at it. Keyed to the same broad field so it comes in patches and not as a ring.
    float rockPatch = saturate(Fbm2BandLimited(worldPosition.xz * 0.03, 3, footprint * 0.03) * 1.6 + 0.5);
    float3 albedo = lerp(RockColor, RockColorLight, rockPatch * detail);

    float oxide = (1.0 - smoothstep(CraterRadius * 1.1, ConeRadius * 0.6, coneR)) * saturate(rockPatch * 1.5 + 0.2);
    albedo = lerp(albedo, ScoriaColor, oxide);

    //A fine relief so the near scoria reads as broken clinker rather than as a smooth floor. Combed, because
    //isotropic noise has no grain and reads as gravel (the savanna's #117 lesson) - and the grain runs ACROSS
    //the way the flow that laid it was moving, which is how pahoehoe folds into ropes. Until #509 it ran
    //downhill, which the sheen then drew as a brushed-metal field of streaks converging on the cone.
    float relief = Fbm2Combed(worldPosition.xz * 0.55, float2(-around.y, around.x), 1.8, 4, footprint * 0.55) * 0.5;
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    //--- Lighting ----------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows (#471), into the same sunlight factor the clouds dim, so everything read off it
    //is shadowed at once. What casts here: the island and the gun on the flank.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));
    float3 light = skyAmbient * AmbientStrength + SunColor * ndotl * sunlight;

    float3 color = albedo * light;

    //Heat off the flows: the band beside a flow glows with what the flow is throwing on it. An ADDED
    //radiance rather than a tint on the albedo - lit black rock is still black rock, and at 2 % albedo a
    //multiplied light would not show at all.
    //Kept faint: at 0.3 the halos of the five flows met under the summit and washed the whole upper cone
    //orange, where every reference keeps the rock between the flows black.
    color += LavaCool * river.Halo * river.Halo * 0.07;

    //--- The rivulets and the cracks in the field --------------------------------------------------------
    //Both are hairlines of light on the dark ground, and both are in the full program only: they are the
    //reduced program's pair (see the techniques). Both sit behind DATA branches with no gradient operation
    //inside - the footprint was taken above.
    if (fullDetail)
    {
        [branch]
        if (coneR < ConeRadius * 0.75)
        {
            float rivulet = Rivulets(around, coneR, footprint) * (1.0 - river.Halo);
            float hotter = 1.0 - saturate(coneR / (ConeRadius * 0.75));
            color += lerp(LavaCool, LavaHot, 0.25 + 0.45 * hotter) * rivulet * RivuletStrength;
        }

        //Sinuous cracks in the crust of the field, a few patches of them and more beside the flows, where the
        //ground is still hot underneath: the zero contour of a noise, which draws a WANDERING line where a
        //Voronoi web would draw a net. Which patches crack is decided first and cheaply, and the line itself
        //only where one does - see FieldCracks for why that needs a noise that knows its own slope.
        float cracked = max(smoothstep(0.25, 0.5, GradientNoise2(worldPosition.xz * 0.006 + 17.3)), river.Halo * 0.6)
            * (1.0 - river.Mask);

        [branch]
        if (cracked > 0.0)
            color += lerp(LavaCool, LavaHot, 0.2) * FieldCracks(worldPosition.xz, footprint) * cracked * FieldCrackStrength;
    }

    //--- The flow ----------------------------------------------------------------------------------------
    //Behind a data branch: a flow is a narrow band of any frame, and its crust costs a Voronoi and three
    //noise taps that every pixel beside it paid until #509.
    float gloss = (1.0 - rockPatch) * (1.0 - oxide);

    [branch]
    if (river.Mask > 0.0)
    {
        float3 flow = CrustColor * light + FlowRadiance(river, footprint, crackleFade);

        //The flow is opaque and replaces what is under it rather than adding to it - lava is rock.
        color = lerp(color, flow, river.Mask);
        gloss = lerp(gloss, 1.0, river.Mask);
    }

    //--- The crater's lake -------------------------------------------------------------------------------
    //Every crater the references drew at night holds molten lava: plates of dark crust drifting outward over
    //it from where it wells up, their seams incandescent. The one place in the scene a Voronoi web is the
    //right picture - a lake's crust is plates, where a flow's is streaks. Its walls are lit by it.
    [branch]
    if (coneR < CraterRadius * 1.25)
    {
        float walls = 1.0 - smoothstep(CraterRadius * 0.6, CraterRadius * 1.25, coneR);
        color += LavaCool * walls * 0.12;

        float lake = 1.0 - smoothstep(CraterRadius * 0.4, CraterRadius * 0.62, coneR);
        float2 lakeUV = (fromCone - around * VolcanoTime * 0.6) / (PlateSize * 2.5);
        float lakeEdge = VoronoiEdge2(lakeUV + float2(GradientNoise2(lakeUV * 0.4),
                                                      GradientNoise2(lakeUV * 0.4 + 9.1)) * 0.6);
        float seam = lerp(0.15, 1.0 - saturate(lakeEdge * 5.0), crackleFade);
        float upwell = 1.0 - smoothstep(0.0, CraterRadius * 0.22, coneR);
        float heat = saturate(max(seam * CrackGlow * 3.0, upwell));
        float3 lava = CrustColor * light + LavaCool * CrustGlow + lerp(LavaCool, LavaHot, heat * heat) * heat;

        color = lerp(color, lava, lake);
        gloss *= 1.0 - lake;
    }

    //--- The sheen ---------------------------------------------------------------------------------------
    //Cooled lava is glass. Black pahoehoe and a flow's chilled skin reflect the sky as a slate sheen, which is
    //the only thing that separates a lava field from a heap of soot at night - every ground-level reference
    //draws it. Fresnel-weighted, so it is the grazing ground the play camera looks across that takes it, and
    //the scoria and the oxidised summit do not (they are rough).
    float3 reflected = reflect(-view, normal);
    float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(normal, view)), 5.0);
    //Mostly the ZENITH, even at grazing: this dome's horizon uniform is a warm lit band far brighter than
    //the storm deck the scene actually stands under, and reflected in full it turned the whole field into
    //brown mud. A little of it is kept, so the sheen still follows the dome.
    float3 skyReflection = lerp(ZenithColor, HorizonColor, 0.08 * (1.0 - saturate(reflected.y)));

    //On the CRESTS of the ropy relief and not as a film: the references' pahoehoe catches the sky along each
    //fold and is black in between, and an even sheen reads as a wet floor.
    float crests = saturate(0.35 + relief * 2.5);
    color += skyReflection * fresnel * gloss * crests * SheenStrength;

    //Haze, and it is the dome's horizon TINTED DOWN rather than the horizon itself. Every other terrain
    //scene here stands under a sky whose horizon is roughly its own ground's tone, so lerping to it is
    //aerial perspective; black basalt under a cream horizon at the desert's haze distance came out as a
    //SAND DUNE, cone and all. An ash pall is what is actually in this air.
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, HorizonColor * HazeTint, haze * haze * HazeStrength);

    return float4(color, 1.0);
}

//Two programs from one body, the idiom Forest.fx established (#298). "Volcano" is the authored flank;
//"VolcanoReduced" is the same flank without its two kinds of hairline off the flows - the rivulets down the
//cone and the cracks in the field - which arrived together in #509 and are given up together, because a lone
//reduction buys nothing on a pass that is occupancy-bound (SceneRenderer.SceneDetail). What the scene is made
//of stays on every tier: the massing, the flows with their crust and streamlines, the crater's lake, the
//sheen and the haze.
float4 VolcanoPS(VolcanoVertexOutput input) : COLOR { return VolcanoSurface(input, true); }
float4 VolcanoReducedPS(VolcanoVertexOutput input) : COLOR { return VolcanoSurface(input, false); }

technique Volcano
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL VolcanoVS();
        PixelShader = compile PS_SHADERMODEL VolcanoPS();
    }
};

technique VolcanoReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL VolcanoVS();
        PixelShader = compile PS_SHADERMODEL VolcanoReducedPS();
    }
};
