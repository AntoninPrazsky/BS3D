//Scattered coconut palms over the tropical beach, as real 3D geometry (#244): the acacia's arrangement
//(Acacia.fx) with the wind added. Each palm is an instanced procedural mesh - a bowed trunk under a
//crown of radiating leafleted fronds with a skirt of dead ones hanging beneath it (PalmMesh) - shaded
//from the scene's own sun and dome as the terrain is (Tropical.fx), so a palm sits in the beach's light
//rather than being pasted over it. DiffuseColor and DappleStrength are the per-draw material: the live
//green of the leaves' draw, the bark of the solids' draw (see the palm material below).
//
//THE SWAY IS THE ONE THING THIS SHADER DOES THE ACAIA DOES NOT, and it is why it exists as its own
//file rather than sharing Acacia.fx: a palm that stands dead still on a tropical beach reads as a
//plastic one. Each vertex carries a SWAY WEIGHT in its TEXCOORD0.x - zero along the trunk, rising
//along each frond to its tip - so the wind moves the crown and never the trunk (a palm whose whole
//body waves reads as a kelp). The weight is built into PalmMesh, which is why the two are one change.
//The phase comes off the instance's own world position, so no two palms beat in time - the campfire
//ring's reasoning, applied to leaves.
//
//THE PALM'S OWN MATERIAL (#557) is the second thing, behind PalmShading. The owner, on the tall palms of
//#555: "they don't look like real palms - paper or plastic models at best". Under the plain shading below
//(still what the rocks and the beach's dressing get) a frond was a card: one flat green lit from both sides
//alike, never in its own shade, never lit from behind, and the trunk a smooth even brown tube. PalmSurface
//gives each part of a palm what the references (C:\Users\panrd\AI\sd\out\557) show it has:
//  - a leaf is lit from BEHIND as well as in front - the sun coming through a frond is the brightest,
//    yellowest green on a palm - so its far face transmits, strongest looking into the sun;
//  - a crown is dark inside and under itself: occlusion rising along each frond from its root, and the sun
//    itself half-blocked near the crown's heart where the shadow map is too coarse to say so;
//  - the leaves are waxy, with a small sheen on the lit face, and old fronds yellow towards ochre, the tips
//    bleach, the midrib is pale and the dead skirt is dry grey-brown;
//  - the trunk is matte grey-brown bark with a leaf scar every few centimetres (band-limited, so they fade to
//    their mean instead of shimmering once a pixel covers several), fibre cracks along it, a darker fibrous
//    top under the crown, and occlusion at its foot and in the crown's shade;
//  - the boot of old frond bases and the coconuts hang at the crown's heart, dark and shadowed.
//The vertex's TEXCOORD0.y says which part a pixel is (PalmMesh.Part is the same table): the palms' draws
//set PalmShading 1, every other mesh drawn through this effect keeps 0 and the plain path, for exactly the
//reason the sway strength is per draw (below) - their TEXCOORD0 is a real texture coordinate.
//
//Testbed-shared (the map editor and the game build it too), Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//For the fronds' leaf mottle - a 3D field of world position, so it has no seam and does not swim as
//the frond turns with its tree (Acacia.fx's own reasoning).
#include "Noise.fxh"
//The sun's cast shadows (#471): a palm stands in the shadow of the palm beside it, and throws its own
//across the sand - the acacia's arrangement again, ShadowCaster technique at the bottom included.
#include "Shadows.fxh"

float4x4 View;
float4x4 Projection;

//Towards the sun, dotted with the surface normal exactly as Tropical.fx does it, and the dome's
//ambient split zenith-to-horizon by the normal's height - so the palm takes the same light the
//sand under it does.
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//The per-draw material: the diffuse colour and how strongly the leaf mottle breaks it up (0 on wood).
float3 DiffuseColor;
float DappleStrength;

//The palm's own material (#557): 1 on the palms' two draws, 0 on everything else - see the header. Per draw,
//like SwayStrength and for its reason. The three colours are the parts DiffuseColor cannot carry, since one
//draw holds several parts: the frond draw's DiffuseColor is the live green and the wood draw's the bark.
float PalmShading;
float3 AgedFrondColor;     //the ochre an old frond yellows towards, and the bleached tips
float3 DeadFrondColor;     //the hanging skirt's dry grey-brown
float3 CoconutColor;       //a green nut; a per-nut roll ripens it towards brown
float3 CameraPosition;     //for the transmission's look into the sun and the leaves' sheen

//The wind: how far the fronds sway at their tips and how fast, along this direction. Off the wall
//clock, so the palms keep moving while the simulation is paused - like the clouds, the sea and the
//birds, the wind does not wait for the player.
//
//⚠ SwayStrength IS PER DRAW, NOT PER FRAME, and the reason is that the weight it scales is read out of
//TEXCOORD0 - an ordinary TEXTURE COORDINATE (see PalmVertexInput.Sway). That is only a sway weight
//because PalmMesh chooses to bake one there; every other mesh drawn through this effect carries real
//UVs, and a LatheMesh - which is what the tropical scene's waterline boulders and their moss caps both
//are - writes s/segments, i.e. 0 to 1 AROUND THE CIRCUMFERENCE. Set once for the whole frame, the palms'
//strength therefore reached the rocks and swung one side of every ring at full frond-tip weight while
//the other side stood still, so the stones sheared open rather than merely drifting (#268's second
//round, reported as "the rocks move with the wind, which is nonsense"). A caller drawing anything but a
//PalmMesh through this must pass 0.
float2 WindDirection;
float SwayStrength;
float SwaySpeed;
float PalmTime;

//The leaf mottle's world-space frequency and its mean (a crown is dappled foliage, not a flat mass).
static const float DAPPLE_FREQUENCY = 0.55;
static const float DAPPLE_MEAN = 0.86;

struct PalmVertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 Sway : TEXCOORD0;       //x = the sway weight the mesh bakes per vertex; y unused
    float4 World1 : TEXCOORD1;     //per-instance world matrix, row-major like InstancedModel.fx (no transpose)
    float4 World2 : TEXCOORD2;
    float4 World3 : TEXCOORD3;
    float4 World4 : TEXCOORD4;
};

struct PalmVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float2 Surface : TEXCOORD2;    //the mesh's TEXCOORD0 passed on: x along a frond, y the part code (see PalmSurface)
};

//The sway: two incommensurate oscillators (a single sin is a metronome), travelling downwind, phased off
//the instance's own position so a grove never beats in unison. Applied after the world transform in world
//space - the frond strips are built in the mesh's own frame, but the wind does not care which tree it is
//moving. A function rather than two copies because the shadow caster has to bend a frond exactly as the
//drawn one bends, and a shadow that lags its own leaf is the sort of fault nobody can name and everybody
//sees.
float4 Sway(float4 worldPosition, float4 instanceRow, float weight)
{
    float phase = dot(instanceRow.xz, WindDirection) * 0.35 + PalmTime * SwaySpeed;
    worldPosition.xz += WindDirection * SwayStrength * weight
        * (sin(phase) + 0.5 * sin(phase * 2.3 + 1.7));
    return worldPosition;
}

PalmVertexOutput PalmVS(PalmVertexInput input)
{
    PalmVertexOutput output;

    float4x4 world = float4x4(input.World1, input.World2, input.World3, input.World4);
    float4 worldPosition = mul(input.Position, world);

    //The sway, one copy (see Sway below): the shadow caster bends the crown by exactly the same
    //arithmetic, or a frond and the shadow it throws would part company in the wind.
    worldPosition = Sway(worldPosition, input.World4, input.Sway.x);

    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    //The instance transform is rotation + uniform scale + translation, so the plain matrix rotates the
    //normal (a uniform scale leaves it only needing a re-normalize). The sway's small horizontal
    //offset is not un-rotated into the normal - at a fraction of a frond's length the lighting error
    //is beneath notice, and the alternative is re-deriving a normal per vertex for a wind that barely
    //tilts it.
    output.WorldNormal = normalize(mul(input.Normal, (float3x3)world));
    output.Surface = input.Sway;

    return output;
}

//The plain material: everything drawn through this effect that is not a palm (the waterline's rocks and
//their moss, the beach's scrub, tufts and driftwood) - and every palm until #557.
float3 PlainSurface(float3 worldPosition, float3 N)
{
    //The scene's own light, matched to the terrain: a hemisphere ambient tinted zenith-to-horizon by
    //the normal's height, plus the sun's own diffuse.
    float3 ambient = lerp(HorizonColor, ZenithColor, saturate(N.y * 0.5 + 0.5));
    float ndotl = saturate(dot(N, SunDirection));

    //The sun's cast shadows (#471): the sun term alone, the dome's ambient stays - a rock under the crown
    //of a palm is still lit by the sky. Off the map (ShadowStrength 0) the branch is skipped.
    float shadow = 1.0;
    [branch]
    if (ShadowStrength > 0.0)
        shadow = SunShadow(worldPosition, N, SunDirection);

    float3 color = DiffuseColor * (ambient + SunColor * ndotl * shadow);

    //The foliage mottle: a 3D field of WORLD position, so neighbouring plants do not share a pattern.
    //Zero on stone and wood (DappleStrength 0).
    if (DappleStrength > 0.0)
        color *= DAPPLE_MEAN + DappleStrength * Fbm3(worldPosition * DAPPLE_FREQUENCY, 3);

    return color;
}

//--- The palm's own material (#557) -------------------------------------------------------------------------

//How much of the sun a leaf passes to its far face, and the colour that light takes: yellower and more
//saturated than the leaf seen from its lit side, which is what a frond against the sun looks like in every
//back-lit reference. The boost is the extra looking INTO the sun through it (forward scattering).
static const float3 LEAF_TRANSMISSION_TINT = float3(1.15, 1.3, 0.5);
static const float LEAF_TRANSMISSION = 0.28;
static const float LEAF_FORWARD_BOOST = 2.5;
//The waxy sheen of a live coconut leaf's upper face, small: a plastic frond is the thing being removed.
static const float LEAF_SHEEN = 0.10;
static const float LEAF_SHEEN_POWER = 36.0;
//Leaf scars up the trunk: this many to a trunk, a little closer towards the crown as on a real one. At
//22 units and ~0.4 radius that is one every fifteen or so centimetres of a 20 m palm.
static const float TRUNK_SCARS = 120.0;
//The bark's fibre cracks: how many to a world unit round the trunk, and a fifth of that along it.
static const float BARK_FIBRE_FREQUENCY = 4.0;

//The trunk's scar coordinate and the fibre field's footprint. Taken OUTSIDE PalmSurface's branch on the
//part, because they are gradients (fwidth) and the part is per pixel: a gradient under a divergent branch is
//undefined across the quad. Cheap, and only the palms' draws reach them.
float TrunkScarCoordinate(float code)
{
    float up = saturate(-code - 1.0);
    return TRUNK_SCARS * (up + 0.2 * up * up);
}

float3 PalmSurface(float3 worldPosition, float3 N, float2 surface, float scar, float scarFootprint,
    float fibreFootprint)
{
    float code = surface.y;
    float along = surface.x;                 //how far out along a frond, 0 at the crown
    float3 L = SunDirection;
    float3 V = normalize(CameraPosition - worldPosition);
    float3 ambientSky = lerp(HorizonColor, ZenithColor, saturate(N.y * 0.5 + 0.5));

    float ndotl = dot(N, L);
    float shadow = 1.0;
    [branch]
    if (ShadowStrength > 0.0)
        shadow = SunShadow(worldPosition, ndotl >= 0.0 ? N : -N, L);

    float3 color;

    [branch]
    if (code > 0.5 && code < 12.5)
    {
        //--- A leaf: a live frond's leaflet or midrib, or the dead skirt's.
        float part = floor(code);
        float s = code - part;               //0 at the midrib, 1 at the leaflet's tip
        bool dead = part > 10.5;
        float age = dead ? 0.0 : (part - 1.0) * 0.5;   //0 young .. 3 old

        float3 albedo = DiffuseColor;
        //Only the oldest go properly ochre; the rest are the variant's own green with a hint of it.
        float yellowing = age / 3.0;
        albedo = lerp(albedo, AgedFrondColor, yellowing * yellowing * 0.6);
        //Bleached tips: the outer leaflets' ends, strongest at the frond's far end.
        albedo = lerp(albedo, AgedFrondColor * 1.2, 0.4 * smoothstep(0.5, 1.0, s) * smoothstep(0.45, 1.0, along));
        //The pale midrib the leaflets grow from.
        albedo = lerp(lerp(albedo, AgedFrondColor, 0.65) * 1.25, albedo, smoothstep(0.0, 0.2, s));
        if (dead)
            albedo = DeadFrondColor * (1.1 - 0.35 * s);

        if (DappleStrength > 0.0)
            albedo *= DAPPLE_MEAN + DappleStrength * Fbm3(worldPosition * DAPPLE_FREQUENCY, 3);

        //Inside the crown: occlusion rising along each frond from its root, and the sun itself blocked
        //near the heart - a crown is dark under and inside itself however coarse the shadow map is there.
        //And a leaf's UNDERSIDE sees the crown and the sand, not the bright horizon the plain hemisphere
        //would hand it: the back-lit references are dark green under the fronds wherever the sun is not
        //coming through.
        float ao = lerp(0.22, 1.0, smoothstep(0.0, 0.65, along)) * lerp(0.5, 1.0, saturate(N.y * 0.5 + 0.5));
        float sunReach = lerp(0.45, 1.0, smoothstep(0.05, 0.5, along)) * shadow;

        float front = saturate(ndotl);
        float back = saturate(-ndotl);
        float intoSun = pow(saturate(dot(-V, L)), 4.0);
        float3 transmitted = albedo * LEAF_TRANSMISSION_TINT * LEAF_TRANSMISSION * back
            * (1.0 + LEAF_FORWARD_BOOST * intoSun) * (dead ? 0.25 : 1.0);

        float3 H = normalize(L + V);
        float sheen = LEAF_SHEEN * pow(saturate(dot(N, H)), LEAF_SHEEN_POWER) * front * (dead ? 0.2 : 1.0);

        color = albedo * ambientSky * ao + SunColor * sunReach * (albedo * front + transmitted + sheen);
    }
    else if (code < -0.5)
    {
        //--- The trunk: matte grey-brown bark with a leaf scar every few centimetres and fibres along it.
        float up = saturate(-code - 1.0);

        //The scar: a narrow dark groove and a paler lip over it, band-limited - fading to its own mean
        //(within a few per cent of 1) as a pixel starts to span a scar, never aliasing into a moire.
        //The scars are not machined: each wanders a little round the trunk, off a low-frequency field.
        float f = frac(scar + 0.35 * GradientNoise3(worldPosition * 1.7));
        float pattern = f < 0.1 ? 0.72 : (f < 0.26 ? 1.1 : 1.0);
        float scarFade = saturate(1.6 - scarFootprint * 2.5);
        float bark = lerp(1.0, pattern, scarFade);

        //Fibre cracks along the trunk: thin dark lines where a stretched noise crosses zero.
        float fibre = GradientNoise3(worldPosition * float3(BARK_FIBRE_FREQUENCY, BARK_FIBRE_FREQUENCY * 0.04, BARK_FIBRE_FREQUENCY));
        float crackFade = saturate(1.0 - fibreFootprint * 1.5);
        bark *= 1.0 - 0.15 * crackFade * (1.0 - smoothstep(0.0, 0.06, abs(fibre)));
        //Weathering: long pale and dark streaks at a lower frequency, visible from anywhere.
        bark *= 0.86 + 0.28 * Fbm3(worldPosition * float3(1.3, 0.15, 1.3), 2);

        //Grey weathered bark low, browner and darker fibrous wood near the crown.
        float3 albedo = DiffuseColor * bark * lerp(float3(1.0, 1.0, 1.0), float3(0.7, 0.58, 0.45), smoothstep(0.82, 1.0, up));

        //Occlusion at the foot and in the crown's shade (the sun blocked by the crown too, as on a leaf).
        float ao = lerp(0.65, 1.0, smoothstep(0.0, 0.03, up)) * lerp(1.0, 0.45, smoothstep(0.9, 1.0, up));
        float sunReach = lerp(1.0, 0.4, smoothstep(0.92, 1.0, up)) * shadow;

        color = albedo * (ambientSky * ao + SunColor * saturate(ndotl) * sunReach);
    }
    else
    {
        //--- The crown's heart: the boot of old frond bases, and the coconuts under it. Both sit in the
        //crown's shade, so the sun barely reaches them and the sky only in part.
        bool nut = code < 14.5;
        float ripe = code - floor(code);
        float3 albedo = nut
            ? lerp(CoconutColor, CoconutColor * float3(1.6, 1.0, 0.55), ripe)
            : DiffuseColor * float3(0.8, 0.62, 0.45) * (0.8 + 0.4 * Fbm3(worldPosition * float3(4.0, 1.0, 4.0), 2));

        float3 H = normalize(L + V);
        float gloss = nut ? 0.18 * pow(saturate(dot(N, H)), 30.0) : 0.0;
        color = albedo * ambientSky * 0.5 + SunColor * (albedo * saturate(ndotl) + gloss) * 0.4 * shadow;
    }

    return color;
}

float4 PalmPS(PalmVertexOutput input, bool isFront : SV_IsFrontFace) : COLOR
{
    float3 N = normalize(input.WorldNormal);

    //The palms' leaves are drawn unculled and single-sided (#557), so the face the camera sees takes the
    //normal of the side it is: the underside of a frond is the underside. Every solid is culled and only
    //ever shows its front, so this is a no-op for them.
    N = isFront ? N : -N;

    //The gradients the palm material needs, before any branch (see TrunkScarCoordinate).
    float scar = TrunkScarCoordinate(input.Surface.y);
    float scarFootprint = fwidth(scar);
    float fibreFootprint = length(fwidth(input.WorldPosition)) * BARK_FIBRE_FREQUENCY;

    float3 color;
    [branch]
    if (PalmShading > 0.5)
        color = PalmSurface(input.WorldPosition, N, input.Surface, scar, scarFootprint, fibreFootprint);
    else
        color = PlainSurface(input.WorldPosition, N);

    return float4(color, 1.0);
}

technique Palm
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PalmVS();
        PixelShader = compile PS_SHADERMODEL PalmPS();
    }
};

//--- The shadow caster (#471): the same instanced geometry drawn from the sun into SunShadowMap's target,
//writing the map's own clip depth (orthographic, so linear) into a 32-bit channel. No material, no light -
//the instance stream is read for the world matrix, and the sway weight for the wind. Acacia.fx's caster
//with the one addition that shader has no need of.

struct PalmShadowOutput
{
    float4 Position : SV_POSITION;
    float Depth : TEXCOORD0;
};

PalmShadowOutput PalmShadowVS(PalmVertexInput input)
{
    PalmShadowOutput output;
    float4x4 world = float4x4(input.World1, input.World2, input.World3, input.World4);
    float4 worldPosition = Sway(mul(input.Position, world), input.World4, input.Sway.x);
    output.Position = mul(worldPosition, ShadowViewProjection);
    output.Depth = output.Position.z;
    return output;
}

float4 PalmShadowPS(PalmShadowOutput input) : COLOR
{
    return float4(input.Depth, 0.0, 0.0, 1.0);
}

technique ShadowCaster
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PalmShadowVS();
        PixelShader = compile PS_SHADERMODEL PalmShadowPS();
    }
};
