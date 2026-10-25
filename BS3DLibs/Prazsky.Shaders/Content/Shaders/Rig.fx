//The sea's oil platforms (#969): the owner wants things that stand in a real sea, "platforms for extracting oil" first.
//OilRigs stands them far out where the swell has faded flat; this shades them.
//
//THE PARTS. Every part is a box with its own colour and kind: plain painted steel (the bracing is tubes of it), a lattice
//(the derrick: members over darker steel - ⚠ with the sky's colour between them the framework read as perforated sheets
//tinted by the sky), or a light (the flare's flame, the deck's lamps: steady, #401). Lit by the scene's sun and its sky.
//
//THE WATER. The sea's surface writes no depth (#131: a missed ball must fall through it), so anything under it drawn
//after it shows through the water as through glass - the fault #761 fixed for the fireworks. So the legs are cut at the
//waterline here (clip), and a band of foam stands where they meet the water. The rigs stand where the swell has faded flat
//(SeaSceneConfig.WaveFadeEnd), so the cut is a straight line the water agrees with.
//
//THE AIR. The sea's own horizon colour, lighter than the water's haze, so a rig a kilometre off still stands on the horizon.
//
//Drawn in the Game and the Testbed through the shared SceneRenderer (SeaBackdrop), after the water. SM 5.0, no OPENGL.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

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

static const float FOAM_BAND = 1.2;       //the foam round a leg, world units over the water
static const float3 FOAM_COLOR = float3(0.85, 0.88, 0.92);
static const float LATTICE_PERIOD = 4.0;  //a lattice's bays, world units
static const float LIGHT_RADIANCE = 1.4;

struct RigVertexInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 Color : COLOR0;      //(rgb, kind: 0 painted, 0.5 lattice, 1 light)
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
    output.Position = mul(mul(float4(input.Position, 1.0), View), Projection);
    output.Normal = input.Normal;
    output.World = input.Position;
    output.Color = input.Color;
    return output;
}

float4 RigPS(RigVertexOutput input) : COLOR
{
    float3 p = input.World;
    clip(p.y - WaterY);

    float3 n = normalize(input.Normal);
    float kind = input.Color.a;
    float3 albedo = input.Color.rgb;

    //A lattice: diagonal members crossing in bays, the sky between them
    float2 face = abs(n.x) > abs(n.z) ? float2(p.z, p.y) : float2(p.x, p.y);
    float2 bay = face / LATTICE_PERIOD;
    //How far from the nearest member in bays: the two diagonals and the horizontal chords
    float d1 = frac(bay.x + bay.y), d2 = frac(bay.x - bay.y), h = frac(bay.y);
    float nearest = min(min(min(d1, 1.0 - d1), min(d2, 1.0 - d2)), min(h, 1.0 - h));
    float footprint = max(fwidth(bay.y), 1e-4);
    float member = 1.0 - smoothstep(0.06, 0.06 + footprint * 1.5, nearest);
    float isLattice = step(0.25, kind) * step(kind, 0.75);

    float sun = saturate(dot(n, SunDirection));
    float3 sky = lerp(HorizonColor, ZenithColor, saturate(n.y * 0.5 + 0.5)) * 0.5;
    float3 color = albedo * (SunColor * sun + sky);
    color = lerp(color, color * lerp(0.45, 1.1, member), isLattice);

    //The foam where a leg meets the water
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
