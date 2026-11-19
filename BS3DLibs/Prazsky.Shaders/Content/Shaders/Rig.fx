//What stands on the open sea: the oil platforms (#969), and the lighthouse on its rock, the container ship and the yacht
//(#1125). The owner wants things that stand in a real sea, "platforms for extracting oil" first. OilRigs stands the
//platforms far out where the swell has faded flat, SeaTraffic the rest; this shades them all.
//
//THE PARTS. Every part has its own colour and kind: plain painted steel, stone or hull (the bracing is tubes of it), a
//helideck's marked top (#1169: its colour's red and green carry where on the deck the pixel is, and the markings are drawn
//here), a lattice (the coarser derrick: members over darker steel - ⚠ with the sky's colour between them the framework read
//as perforated sheets tinted by the sky), sailcloth (one sheet, lit on the side the lens sees and glowing where the sun is
//behind it), or a light (the flare's flame, the deck's lamps, the lighthouse's lantern, a ship's windows: steady, #401). Lit
//by the scene's sun and its sky.
//
//THE FRAME. Everything is built in its own frame and World stands each where it is: a platform (OilRigLayout, scaled for
//the farther), the lighthouse and the two vessels (SeaTrafficLayout), the vessels moving on the wall clock.
//
//THE WATER. The sea's surface writes no depth (#131: a missed ball must fall through it), so anything under it drawn
//after it shows through the water as through glass - the fault #761 fixed for the fireworks. So everything is cut at the
//waterline here (clip), and a band of foam stands where it meets the water. All of it stands where the swell has faded
//flat (SeaSceneConfig.WaveFadeEnd), so the cut is a straight line the water agrees with.
//
//THE HORIZON. Past SinkFrom from the lens a part sinks, by the square of how far past (SinkScale): the curve of a real
//sea, which takes a ship from its waterline up as it goes away, here so that a vessel at the end of its lane has gone under
//the cut from every lens before it wraps round (SeaTrafficLayout). Nearer than SinkFrom nothing moves. Only the vessels
//are drawn with a SinkFrom a lens can reach: the platforms and the lighthouse stand still and take one no lens stands
//that far from (SeaTrafficLayout.NO_SINK), so they never sink, from the Testbed's or the editor's free lenses either.
//SeaTrafficSourceTests read this drop.
//
//THE AIR. The sea's own horizon colour, lighter than the water's haze, so a rig a kilometre off still stands on the horizon.
//
//Drawn in the Game and the Testbed through the shared SceneRenderer (SeaBackdrop), after the water. Shader Model 5.0 on DirectX and 3.0 under OPENGL (ShaderTarget.fxh).

#include "ShaderTarget.fxh"

float4x4 World;
float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float Time;

float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;
float RigHaze;           //the distance over which the air thickens to the horizon's colour
float WaterY;            //the sea's mean level
float SinkFrom;          //the distance from the lens past which a part sinks under the horizon, set per draw
float SinkScale;         //how slowly: the drop is (distance - SinkFrom)^2 / SinkScale

static const float FOAM_BAND = 1.2;       //the foam round a leg, world units over the water
static const float3 FOAM_COLOR = float3(0.85, 0.88, 0.92);
static const float LATTICE_PERIOD = 4.0;  //a lattice's bays, world units
static const float LIGHT_RADIANCE = 1.4;
static const float CLOTH_THROUGH = 0.6;   //how much of the sun behind a sail comes through it

//A helideck's markings: the deck's green, the touchdown circle's yellow, the H and the perimeter line's white, the rope net
//over the middle (NET_CELLS diamonds across the deck's half width)
static const float3 HELIDECK_GREEN = float3(0.10, 0.15, 0.10);
static const float3 MARKING_WHITE = float3(0.72, 0.72, 0.70);
static const float3 MARKING_YELLOW = float3(0.75, 0.55, 0.06);
static const float3 NET_DARK = float3(0.05, 0.07, 0.05);
static const float NET_CELLS = 9.0;

struct RigVertexInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 Color : COLOR0;      //(rgb, kind: 0 painted, 0.25 a helideck's top, 0.5 lattice, 0.8 cloth, 1 light)
};

struct RigVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float3 World : TEXCOORD1;
    float4 Color : TEXCOORD2;
};

RigVertexOutput RigVS(RigVertexInput input)
{
    RigVertexOutput output;
    float4 world = mul(float4(input.Position, 1.0), World);

    //Under the horizon: nothing nearer than SinkFrom moves at all
    float past = max(length(world.xz - CameraPosition.xz) - SinkFrom, 0.0);
    world.y -= past * past / SinkScale;

    output.Position = mul(mul(world, View), Projection);
    output.Normal = mul(input.Normal, (float3x3)World);
    output.World = world.xyz;
    output.Color = input.Color;
    return output;
}

//How far inside the band from a to b x stands, softened over aa either side of each edge
float Band(float x, float a, float b, float aa)
{
    return smoothstep(a - aa, a + aa, x) * (1.0 - smoothstep(b - aa, b + aa, x));
}

//A helideck's top at p (-1 to 1 across the eight-sided deck, its corners on the unit circle about the middle): the green
//deck, a white line round its edge, the yellow touchdown circle, the white H, and a rope net of diamonds over the middle,
//which fades out where its strands are finer than a pixel rather than shimmer. aa is p's footprint on the pixel and
//netWidth the net's, taken by the caller before its branch
float3 HelipadAlbedo(float2 p, float aa, float netWidth)
{
    float r = length(p);

    //The edge: 1 along the octagon's sides, which face the axes and the diagonals
    float edge = max(max(abs(p.x), abs(p.y)), max(abs(p.x + p.y), abs(p.x - p.y)) * 0.70710678) / 0.92387953;

    float3 albedo = HELIDECK_GREEN;
    albedo = lerp(albedo, MARKING_YELLOW, Band(r, 0.50, 0.60, aa));
    float upright = Band(abs(p.x), 0.12, 0.20, aa) * (1.0 - smoothstep(0.28 - aa, 0.28 + aa, abs(p.y)));
    float bar = (1.0 - smoothstep(0.16 - aa, 0.16 + aa, abs(p.x))) * (1.0 - smoothstep(0.035 - aa, 0.035 + aa, abs(p.y)));
    albedo = lerp(albedo, MARKING_WHITE, max(upright, bar));
    albedo = lerp(albedo, MARKING_WHITE, Band(edge, 0.90, 0.96, aa));

    float2 cells = float2(p.x + p.y, p.x - p.y) * NET_CELLS;
    float2 f = abs(frac(cells) - 0.5);
    float strands = smoothstep(0.40, 0.48, max(f.x, f.y)) * saturate(1.0 - netWidth * 2.5);
    albedo = lerp(albedo, NET_DARK, 0.6 * strands * (1.0 - smoothstep(0.78 - aa, 0.78 + aa, r)));
    return albedo;
}

float4 RigPS(RigVertexOutput input) : COLOR
{
    float3 p = input.World;
    clip(p.y - WaterY);

    float3 n = normalize(input.Normal);
    float kind = input.Color.a;
    float3 albedo = input.Color.rgb;

    //A helideck's top draws its markings where its colour carries where on the deck it is (#1169). Only one slab of a finer
    //rig is a helideck, and every other pixel this effect shades skips them: the kind is one value across a triangle and a
    //quad is one triangle's, so the branch never parts a quad. Its derivatives are taken before it all the same, which
    //Shader Model 3.0 asks (X3528, "can't force branch with gradients on non-inputs")
    float2 deck = input.Color.rg * 2.0 - 1.0;
    float deckFootprint = max(fwidth(deck.x) + fwidth(deck.y), 1e-4);
    float netFootprint = fwidth((deck.x + deck.y) * NET_CELLS);
    [branch] if (kind > 0.15 && kind < 0.35)
        albedo = HelipadAlbedo(deck, deckFootprint, netFootprint);

    //Sailcloth is one sheet: its normal turned to the lens, and the sun behind it partly through it
    float isCloth = step(0.7, kind) * step(kind, 0.9);
    float3 toLens = CameraPosition - p;
    float3 facing = dot(n, toLens) < 0.0 ? -n : n;
    n = isCloth > 0.5 ? facing : n;
    float through = isCloth * CLOTH_THROUGH * saturate(-dot(n, SunDirection));

    //A lattice: diagonal members crossing in bays, the sky between them
    float2 face = abs(n.x) > abs(n.z) ? float2(p.z, p.y) : float2(p.x, p.y);
    float2 bay = face / LATTICE_PERIOD;
    //How far from the nearest member in bays: the two diagonals and the horizontal chords
    float d1 = frac(bay.x + bay.y), d2 = frac(bay.x - bay.y), h = frac(bay.y);
    float nearest = min(min(min(d1, 1.0 - d1), min(d2, 1.0 - d2)), min(h, 1.0 - h));
    float footprint = max(fwidth(bay.y), 1e-4);
    float member = 1.0 - smoothstep(0.06, 0.06 + footprint * 1.5, nearest);
    float isLattice = step(0.4, kind) * step(kind, 0.6);

    float sun = saturate(dot(n, SunDirection)) + through;
    float3 sky = lerp(HorizonColor, ZenithColor, saturate(n.y * 0.5 + 0.5)) * 0.5;
    float3 color = albedo * (SunColor * sun + sky);
    color = lerp(color, color * lerp(0.45, 1.1, member), isLattice);

    //The foam where a leg, a hull or a rock meets the water
    float foam = 1.0 - smoothstep(0.0, FOAM_BAND, p.y - WaterY);
    color = lerp(color, FOAM_COLOR * (SunColor * 0.5 + sky), foam * 0.85);

    //A light: steady
    color = lerp(color, albedo * LIGHT_RADIANCE, step(0.9, kind));

    //The air between
    float haze = 1.0 - exp(-length(p - CameraPosition) / max(RigHaze, 1.0));
    color = lerp(color, HorizonColor, haze * 0.55 * (1.0 - step(0.9, kind) * 0.5));

    return float4(color, 1.0);
}

technique Rig
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL RigVS();
        PixelShader = compile PS_SHADERMODEL RigPS();
    }
};
