//The flying saucer in space (#955): the owner wanted something to make the scene special, "a flying saucer with aliens".
//One rigid craft (SaucerFlight) on a slow loop far out round the station: a polished hull lathed round its axis, a glass
//dome with the crew in it, a ring of small coloured lights round the rim and a soft glow under the hull.
//
//THE HULL. Chrome, as every reference (955-zimage, 955-klein) draws it: little colour of its own and a mirror of a studio's
//light - a bright band round the horizon over a dark top and a dimmer floor, the key light's glint - not a cube map; the
//saucer is fifty pixels across in play, and what reads at that size is bright streaks where the hull turns. ⚠ Mirroring only
//the scene's own dark rig, it read as matte grey plastic. The rim's lights are round warm bulbs painted on its band and on a
//ring round the top (whole numbers round, so the pattern meets itself), steady with a slow wave of brightness running
//round: ⚠ small lights stay UNDER the glare threshold (#401: a speck over it is sampled by the glare's sparse grid and
//strobes).
//
//THE DOME. Opaque, shaded as glass over a warm-lit cabin, with the crew painted on it - three grey figures standing round
//the middle looking out, a big round head with two black eyes on a narrow body each - so from any side one or two are
//seen through the glass. At the craft's size a painted crew reads as well as a modelled one, and needs no blending or
//sorting. ⚠ A dark crew on a dark cabin did not read at all. Over the crown a short antenna with a red tip.
//
//Drawn in the Game and the Testbed through the shared SceneRenderer (SpaceBackdrop), after the sky's quad and writing depth.
//Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 World;
float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float Time;

float3 SunDirection;     //towards the sun
float3 SunColor;
float3 SkyAmbient;       //the scene's rig, linear
float3 GroundAmbient;

float RimRadius;         //the rim's lights: their radius in the craft's own frame and the band's height range
float2 RimBand;
float UnderY;            //below this the belly's glow
float2 TopRing;          //the top's ring of bulbs: its radius and height in the craft's frame
float2 DomeSpan;         //the dome's base and height in the craft's frame

static const float PI = 3.14159265;
static const float TWO_PI = 6.28318531;

static const float3 HULL_COLOR = float3(0.16, 0.17, 0.19);
static const float HULL_SHININESS = 60.0;
static const float3 STUDIO_BAND = float3(0.62, 0.66, 0.74);   //the band of light round a chrome hull's horizon
static const float RIM_LIGHTS = 24.0;            //round the rim: a whole number
static const float TOP_LIGHTS = 16.0;            //round the top's ring
static const float RIM_LIGHT_SIZE = 0.22;        //a light's radius, the craft's units
static const float RIM_LIGHT_RADIANCE = 0.48;    //under the glare threshold (0.55 of luminance)
static const float3 BULB_COLOR = float3(1.00, 0.90, 0.68);
static const float3 BELLY_GLOW = float3(0.25, 0.85, 0.75);
static const float BELLY_RADIANCE = 0.35;

static const float3 CABIN_COLOR = float3(0.30, 0.16, 0.10);   //the cabin's warm light through the glass
static const float CREW = 3.0;                   //standing round the middle: a whole number
static const float3 CREW_COLOR = float3(0.30, 0.36, 0.33);    //grey-green, lit by the cabin
static const float3 ANTENNA_TIP = float3(1.00, 0.20, 0.15);

struct SaucerVertexInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 UV : TEXCOORD0;
};

struct SaucerVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float3 World : TEXCOORD1;
    float3 Local : TEXCOORD2;
};

SaucerVertexOutput SaucerVS(SaucerVertexInput input)
{
    SaucerVertexOutput output;
    float4 world = mul(float4(input.Position, 1.0), World);
    output.World = world.xyz;
    output.Position = mul(mul(world, View), Projection);
    output.Normal = mul(input.Normal, (float3x3)World);
    output.Local = input.Position;
    return output;
}

//What chrome mirrors: the scene's rig (its sky ambient above, its ground's below), a studio's bright band round the
//horizon, and the sun
float3 Mirror(float3 r)
{
    float3 env = lerp(GroundAmbient, SkyAmbient, saturate(r.y * 0.5 + 0.5)) * 3.0;
    //The band broken into softboxes round the horizon, so a turning hull runs with bright and dark streaks
    float boxes = 0.45 + 0.55 * saturate(sin(atan2(r.z, r.x) * 3.0) * 1.5 + 0.5);
    env += STUDIO_BAND * (exp(-r.y * r.y * 30.0) * boxes * 1.3 + 0.3 * exp(-(r.y + 0.35) * (r.y + 0.35) * 40.0)
        + 0.25 * exp(-(r.y - 0.55) * (r.y - 0.55) * 60.0));
    return env + SunColor * pow(saturate(dot(r, SunDirection)), HULL_SHININESS) * 2.0;
}

//A ring of round bulbs at `radius` and `y` in the craft's frame, `count` round it: how much of one is under the point, and
//its index
float Bulbs(float3 local, float radius, float y, float count, out float index)
{
    float angle = atan2(local.z, local.x);
    float slot = angle / TWO_PI * count;
    index = floor(slot);
    float across = (frac(slot) - 0.5) * TWO_PI * radius / count;
    float radial = length(local.xz) - radius;
    return 1.0 - smoothstep(RIM_LIGHT_SIZE * 0.6, RIM_LIGHT_SIZE, length(float3(across, local.y - y, radial)));
}

//The warm light a ring of bulbs throws on the hull round each of them
float BulbHalo(float3 local, float radius, float y, float count)
{
    float slot = atan2(local.z, local.x) / TWO_PI * count;
    float across = (frac(slot) - 0.5) * TWO_PI * radius / count;
    float3 off = float3(across, local.y - y, length(local.xz) - radius);
    return exp(-dot(off, off) / 0.35);
}

float4 HullPS(SaucerVertexOutput input) : COLOR
{
    float3 n = normalize(input.Normal);
    float3 eye = normalize(CameraPosition - input.World);
    float3 r = reflect(-eye, n);

    float diffuse = saturate(dot(n, SunDirection));
    float3 ambient = lerp(GroundAmbient, SkyAmbient, saturate(n.y * 0.5 + 0.5));
    float fresnel = 0.55 + 0.45 * pow(1.0 - saturate(dot(n, eye)), 4.0);
    float3 color = HULL_COLOR * (SunColor * diffuse * 0.45 + ambient) + Mirror(r) * fresnel;

    //The bulbs round the rim and round the top, warm, a slow wave of brightness running round each ring
    float rimIndex, topIndex;
    float rim = Bulbs(input.Local, RimRadius, 0.5 * (RimBand.x + RimBand.y), RIM_LIGHTS, rimIndex);
    float top = Bulbs(input.Local, TopRing.x, TopRing.y, TOP_LIGHTS, topIndex);
    float rimWave = 0.65 + 0.35 * sin(rimIndex * (TWO_PI / RIM_LIGHTS) * 2.0 - Time * 1.6);
    float topWave = 0.65 + 0.35 * sin(topIndex * (TWO_PI / TOP_LIGHTS) * 2.0 + Time * 1.3);
    color = lerp(color, BULB_COLOR * RIM_LIGHT_RADIANCE * rimWave, rim);
    color = lerp(color, BULB_COLOR * RIM_LIGHT_RADIANCE * topWave, top);

    color += BULB_COLOR * 0.12 * (BulbHalo(input.Local, RimRadius, 0.5 * (RimBand.x + RimBand.y), RIM_LIGHTS)
        + BulbHalo(input.Local, TopRing.x, TopRing.y, TOP_LIGHTS));

    //The belly's glow, brightest at its middle
    float belly = saturate((UnderY - input.Local.y) / 0.4) * (1.0 - smoothstep(0.0, RimRadius * 0.55, length(input.Local.xz)));
    color += BELLY_GLOW * BELLY_RADIANCE * belly;

    return float4(color, 1.0);
}

float4 DomePS(SaucerVertexOutput input) : COLOR
{
    float3 n = normalize(input.Normal);
    float3 eye = normalize(CameraPosition - input.World);
    float facing = saturate(dot(n, eye));

    //The antenna over the crown: brushed metal, its tip a small red light
    float overCrown = input.Local.y - (DomeSpan.x + DomeSpan.y) - 0.02;
    [branch]
    if (overCrown > 0.0)
    {
        float3 metal = HULL_COLOR * (SunColor * saturate(dot(n, SunDirection)) * 0.45) + Mirror(reflect(-eye, n)) * 0.6;
        return float4(lerp(metal, ANTENNA_TIP * 0.45, smoothstep(0.6, 0.7, overCrown)), 1.0);
    }

    //The cabin: warm light through the glass, darker at the dome's foot
    float h = saturate((input.Local.y - DomeSpan.x) / DomeSpan.y);
    float3 cabin = CABIN_COLOR * (0.5 + 0.7 * h);

    //The crew, standing round the middle facing out: a big round head over a narrow body, in the dome's own frame (round
    //it, up it), each at a whole share of the turn so they meet themselves where the angle wraps
    float angle = atan2(input.Local.z, input.Local.x);
    float slot = angle / TWO_PI * CREW + 0.25;
    float across = (frac(slot) - 0.5) * 2.4;
    float head = (length(float2(across / 0.26, (h - 0.58) / 0.2)) - 1.0) * 0.25;
    float body = max(abs(across) - (0.10 + 0.05 * saturate(0.4 - h)), h - 0.5);
    float crew = 1.0 - smoothstep(-0.03, 0.03, min(head, body));
    float eyes = 1.0 - smoothstep(0.8, 1.0, length(float2((abs(across) - 0.1) / 0.07, (h - 0.6 + 0.4 * (abs(across) - 0.1)) / 0.05)));
    cabin = lerp(cabin, lerp(CREW_COLOR * (0.7 + 0.5 * h), float3(0.0, 0.0, 0.0), eyes * step(head, 0.0)), crew);

    //The glass over it: the void and the sun mirrored, strongest at a grazing angle
    float fresnel = 0.08 + 0.92 * pow(1.0 - facing, 5.0);
    float3 color = lerp(cabin, Mirror(reflect(-eye, n)), fresnel);

    return float4(color, 1.0);
}

technique SaucerHull
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SaucerVS();
        PixelShader = compile PS_SHADERMODEL HullPS();
    }
};

technique SaucerDome
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SaucerVS();
        PixelShader = compile PS_SHADERMODEL DomePS();
    }
};
