//The flank of an erupting volcano (#223): black basalt and heaped scoria raked by gullies, rivers of
//red-orange lava running down them and out past the arena, and crust that cracks glowing in its seams. The
//cone stands off to one side with its crater against the sky; the lava fountains over it are LavaFountain.fx
//and the drifting ash is Ash.fx, both drawn after this.
//
//The machinery is the desert's and the mountain's: a camera-centred grid (360 a side, 32-bit indices - see
//CreateGridMesh) snapped to its cell so it does not swim, displaced in the vertex shader, its base normal by
//finite differences per vertex and a fine relief perturbing it per pixel. What is new here is that the GROUND
//IS THE LIGHT. Every other terrain shader in this project takes its whole radiance from the sun and the dome;
//this one adds an emissive band of its own - narrow, and that is deliberate. Red and orange balls hang over
//this scene, so the rivers are kept thin and hot rather than wide and warm, and what the flows throw back on
//the cluster is a capped point light (SceneLights, VolcanoLightStrength), not a tint over the frame.
//
//Two crackle fields carry the whole "crust over liquid" read, and they are the same field at two
//temperatures: VoronoiEdge2 is zero exactly on the borders between cells, so 1 - saturate(k * edge) lights
//the web BETWEEN plates. On the cold flank that web is a dim seam glow between basalt plates; on a river it
//is the incandescent net between drifting crust rafts, scrolled downhill so the flow visibly moves.
//
//Shader Model 5.0, drawn in all three executables out of the one Testbed content directory.

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
float3 LavaHot;
float3 LavaCool;
float SeamGlow;
float PlateSize;

float AmbientStrength;
float HorizonHazeDistance;
float3 HazeTint;
float HazeStrength;
float2 WindDirection;

static const float TWO_PI = 6.2831853;

//--- Height field ------------------------------------------------------------------------------------------

//The cone's analytic massing at a world XZ, relative to VolcanoLevelY and BEFORE the clearing ramp: the
//flank, the crater bitten out of its summit and the gullies raked down it. Mirrored term for term by
//SceneRenderer.VolcanoConeHeight, which places the vents and the rivers' lights on this surface - the one
//term that mirror leaves out is the scoria fBm below, whose few units are invisible under a lamp.
float VolcanoMassing(float2 p)
{
    float2 d = p - ConeCenterXZ;
    float r = length(d);
    float bearing = atan2(d.y, d.x);

    //Shallow at the foot, steepest across the middle of the flank, rounding off at the summit - the profile
    //of a young stratovolcano rather than the straight-sided cone pow(t, 1) would give.
    float t = saturate(1.0 - r / ConeRadius);
    float flank = ConeHeight * pow(t, ConeProfile);

    //The crater bitten out of the summit. Zero outside its radius, full inside; the rim is what is left
    //standing between the two.
    float crater = CraterDepth * smoothstep(CraterRadius, CraterRadius * 0.45, r);

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
    float Mask;  //1 on the flow, feathered to 0 at its edge
    float Halo;  //a wider, softer field: the band of ground the flow is heating
    float Along; //distance down the flank, already scrolled - the flow's own coordinate
    float Across;//distance across the flow from its centre line
    float ConeR; //distance from the cone's axis, which the summit's own heat is a function of
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
    best.ConeR = r;

    for (int i = 0; i < RiverCount; i++)
    {
        //A flow wanders on the way down rather than running true - the wander is a function of the RADIUS,
        //so it bends the river's course and does not merely wobble its edges.
        float wander = RiverWander * sin(r * 0.017 + i * 2.13) * saturate(r / ConeRadius);
        float across = abs(AngleDelta(bearing, RiverBearing[i] + wander)) * r;

        //The front: the flow thins and stops somewhere down the flank, each river at its own reach, with a
        //noisy edge so it ends in a lobed front rather than on a circle.
        float front = 1.0 - smoothstep(RiverReach[i] * 0.82, RiverReach[i] + 24.0 * GradientNoise2(p * 0.01 + i), r);

        //Feathered against the pixel footprint as well as the width, or a river narrower than a pixel out
        //near the horizon turns into a crawling dashed line.
        float halfWidth = RiverWidth * (0.75 + 0.45 * sin(r * 0.026 + i * 1.7));
        float edge = footprint * 0.5;
        float mask = (1.0 - smoothstep(halfWidth * 0.55 - edge, halfWidth + edge, across)) * front;

        //The heated BAND either side of the flow. HaloWidth is a multiple of the river's own half-width, and
        //it is a dial because the first pass had it at six and the arena stands on the river that passes it:
        //the halo swallowed the whole foreground and the entire plain glowed orange.
        float halo = (1.0 - smoothstep(halfWidth, halfWidth * HaloWidth, across)) * front;

        //The nearest flow wins the two coordinates the crust is drawn in; the masks take the strongest, so
        //two rivers that meet high on the flank merge instead of one cancelling the other.
        bool nearer = across < best.Across;
        best.Across = nearer ? across : best.Across;
        best.Along = nearer ? r - VolcanoTime * RiverSpeed : best.Along;

        best.Mask = max(best.Mask, mask);
        best.Halo = max(best.Halo, halo);
    }

    return best;
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

//Tangent-free normal tilt from a height field (Christian Schueler), as everywhere else in this project
float3 PerturbNormalFromHeight(float3 normal, float3 worldPosition, float height)
{
    float3 dpdx = ddx(worldPosition);
    float3 dpdy = ddy(worldPosition);

    float3 r1 = cross(dpdy, normal);
    float3 r2 = cross(normal, dpdx);

    float determinant = dot(dpdx, r1);
    float3 surfaceGradient = sign(determinant) * (ddx(height) * r1 + ddy(height) * r2);

    return normalize(max(abs(determinant), 1e-4) * normal - surfaceGradient);
}

//--- Pixel -------------------------------------------------------------------------------------------------

float4 VolcanoPS(VolcanoVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //How much per-pixel detail this pixel can still resolve. Everything cellular fades out against it well
    //before its cells reach pixel size - a Voronoi web left running to the horizon is a field of crawling
    //sparks, and this one is EMISSIVE, so it would spark straight through the glare pass.
    float detail = saturate(1.0 - footprint * 0.09);
    float crackleFade = saturate(1.0 - footprint * 0.55);

    float3 baseNormal = normalize(input.WorldNormal);

    RiverSample river = SampleRivers(worldPosition.xz, footprint);

    //--- The crust ---------------------------------------------------------------------------------------
    //The cold flank: dark basalt plates with a dim glowing seam between them wherever the ground is being
    //heated (near a flow, and in the crater). Two facts make this read as crust over liquid rather than as
    //painted rock: the plates are DARK - well under the rivers, not a shade under - and the seams are lit
    //from inside rather than being a darker line, which is the one thing a cracked-mud texture cannot do.
    //The domain is WARPED before the cells are read, and it earns its two noise taps: a Voronoi over a
    //jittered lattice still has a lattice in it, and at this scale the flank came out as a regular honeycomb
    //- which reads as a texture laid over rock rather than as rock that has cracked. Warping the coordinate
    //by a field coarser than the cells bends whole runs of plates without tearing any single one.
    float2 plateWarp = float2(GradientNoise2(worldPosition.xz * 0.09),
                              GradientNoise2(worldPosition.xz * 0.09 + 31.7));
    float plateEdge = VoronoiEdge2(worldPosition.xz / PlateSize + plateWarp * 0.7);
    float seam = (1.0 - saturate(plateEdge * 3.2)) * crackleFade;

    float rockPatch = saturate(Fbm2BandLimited(worldPosition.xz * 0.03, 3, footprint * 0.03) * 1.6 + 0.5);
    float3 albedo = lerp(RockColor, RockColorLight, rockPatch * detail);

    //A fine relief so the near scoria reads as broken clinker rather than as a smooth floor. Combed, because
    //isotropic noise has no grain and reads as gravel (the savanna's #117 lesson) - the grain here runs
    //DOWNHILL, along the way the flow that laid it was moving.
    float2 downhill = normalize(worldPosition.xz - ConeCenterXZ + 1e-4);
    float relief = Fbm2Combed(worldPosition.xz * 0.55, downhill, 2.2, 4, footprint * 0.55) * 0.5;
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    //--- The flow ----------------------------------------------------------------------------------------
    //The river's own crust: rafts of chilled skin drifting on the melt, the incandescent net between them
    //scrolled downhill so the flow visibly MOVES. The along-coordinate is already scrolled by SampleRivers.
    //Anisotropic on purpose: a raft of chilled skin on a moving flow is stretched DOWNSTREAM, so the cells
    //are drawn short across the river and long along it. Isotropic cells read as a cracked-mud texture laid
    //over a river rather than as a crust the river is carrying.
    float2 flowUV = float2(river.Across / (PlateSize * 0.75), river.Along / (PlateSize * 2.0));
    //Warped like the crust's, and for the same reason - rafts on a moving flow are the least regular thing
    //in the scene. The warp is read from the flow's own coordinates, so it travels downhill with them.
    float raftEdge = VoronoiEdge2(flowUV + float2(GradientNoise2(flowUV * 0.35),
                                                  GradientNoise2(flowUV * 0.35 + 11.3)) * 0.6);
    float raftSeam = 1.0 - saturate(raftEdge * 2.6);

    //A flow is hottest along its middle and crusts over towards its banks, and it pulses slowly as the
    //supply behind it surges. Faded to a plain mean with distance, where the rafts are unresolvable.
    float centre = saturate(1.0 - river.Across / max(RiverWidth, 1e-3));
    float surge = 0.85 + 0.15 * sin(VolcanoTime * 0.7 + river.Along * 0.05);
    float rafted = lerp(0.30, 1.0, raftSeam);
    float heat = saturate(lerp(0.62, rafted, crackleFade) * (0.45 + 0.55 * centre) * surge);

    float3 lava = lerp(LavaCool, LavaHot, heat * heat) * river.Mask;

    //--- Lighting ----------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);
    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //Where the ground is hot: in the band beside a flow, and over the summit, which is standing on the
    //chamber that feeds all of them. Both are needed - halo alone leaves a cold crater with rivers pouring
    //out of it, and the summit term alone leaves the flows running over stone that does not know they are
    //there.
    //Patchy, not even: the summit's heat rides the same broad field the rock's own patches do, so the crust
    //over the chamber cracks open in places and holds in others. An even glow over the whole upper cone
    //reads as a shader effect; a patchy one reads as ground.
    float summitHeat = (1.0 - smoothstep(CraterRadius * 0.8, ConeRadius * 0.35, river.ConeR))
        * (0.35 + 0.9 * rockPatch);
    float hot = saturate(max(river.Halo * river.Halo, summitHeat * 0.5));

    //What the heat does to the basalt, and it is an ADDED radiance rather than a tint on the albedo: heated
    //rock emits, it does not become orange rock. The seam glow rides the same field, so the crust cracks
    //open exactly where it is hottest and stays shut where it is cold.
    color += LavaCool * hot * 0.35;
    color += LavaHot * seam * hot * SeamGlow;

    //The flow itself is emissive and replaces what is under it rather than adding to it - lava is opaque.
    color = lerp(color, lava, saturate(river.Mask));

    //Haze, and it is the dome's horizon TINTED DOWN rather than the horizon itself. Every other terrain
    //scene here stands under a sky whose horizon is roughly its own ground's tone, so lerping to it is
    //aerial perspective; black basalt under a cream horizon at the desert's haze distance came out as a
    //SAND DUNE, cone and all. An ash pall is what is actually in this air.
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, HorizonColor * HazeTint, haze * haze * HazeStrength);

    return float4(color, 1.0);
}

technique Volcano
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL VolcanoVS();
        PixelShader = compile PS_SHADERMODEL VolcanoPS();
    }
};
