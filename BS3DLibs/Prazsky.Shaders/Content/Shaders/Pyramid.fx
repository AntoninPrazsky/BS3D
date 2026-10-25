//The desert's pyramids (#965): the owner, "pyramids, realistically placed and rendered after the real pyramids ... players
//must feel: wow, that is beautiful". Pyramids stands the Giza group far out over the dunes; this shades them.
//
//THE STONE. Limestone in its courses, as Giza stands today: horizontal courses of blocks a little over a metre high, each its
//own tone, a dark line under each where the course above oversails it; weathering in patches; sand drifted up the foot; and
//on Khafre the remains of the smooth pale casing near the top. The courses fade to their average as they near a pixel, so a
//far face does not shimmer.
//
//THE LIGHT. The scene's sun on four flat faces - one lit warm, one in shade, which is most of what makes a pyramid read - and
//the sky's ambient from above. THE HAZE is the desert's own horizon colour but LIGHTER than the dunes' (PyramidHaze against
//DesertSceneConfig.HorizonHazeDistance): at the dunes' rate the group dissolved into the sky.
//
//Drawn in the Game and the Testbed through the shared SceneRenderer (DesertBackdrop), after the dunes. SM 5.0, no OPENGL.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 SunDirection;     //towards the sun
float3 SunColor;
float3 ZenithColor;      //the sky, linear
float3 HorizonColor;
float PyramidHaze;       //the distance over which the air thickens to the horizon's colour

static const float3 STONE = float3(0.40, 0.26, 0.12);
static const float3 CASING = float3(0.58, 0.46, 0.30);
static const float3 SAND = float3(0.50, 0.27, 0.10);   //the dunes' own (DesertSceneConfig.SandColor, a shade paler)
static const float COURSE = 1.5;          //a course's height, world units
static const float SAND_RISE = 0.07;      //how far up a face the sand drifts, as a share of its height

struct PyramidVertexInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 Data : TEXCOORD0;   //(the pyramid's base y, its height, its casing share from the top, its seed)
};

struct PyramidVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float3 World : TEXCOORD1;
    float4 Data : TEXCOORD2;
};

PyramidVertexOutput PyramidVS(PyramidVertexInput input)
{
    PyramidVertexOutput output;
    output.Position = mul(mul(float4(input.Position, 1.0), View), Projection);
    output.Normal = input.Normal;
    output.World = input.Position;
    output.Data = input.Data;
    return output;
}

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float Noise2(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
}

float4 PyramidPS(PyramidVertexOutput input) : COLOR
{
    float3 n = normalize(input.Normal);
    float3 p = input.World;
    float baseY = input.Data.x, height = input.Data.y, casing = input.Data.z, seed = input.Data.w;
    float up = saturate((p.y - baseY) / height);

    //The courses: a tone a course, and a dark line under each, faded as a course nears a pixel
    float course = (p.y - baseY) / COURSE;
    float footprint = max(fwidth(course), 1e-4);
    float courseSeen = saturate(1.0 - footprint * 2.5);
    float tone = Hash(float2(floor(course), seed)) - 0.5;
    float under = 1.0 - smoothstep(0.0, max(0.18, footprint * 1.5), frac(course));
    float3 albedo = STONE * (1.0 + 0.3 * tone * courseSeen) * (1.0 - 0.45 * under * courseSeen);

    //Weathering in patches across the face, and blocks fallen out in places: dark notches a course or two high, a few
    //blocks long, where the face is broken (the photographs' faces are rough, not a smooth slope)
    //Along the face: its own horizontal (a fixed axis such as x + z runs along one face and streaks it)
    float u = dot(p.xz, normalize(float2(-n.z, n.x) + float2(1e-4, 0.0)));
    float2 across = float2(u, p.y) * 0.08 + seed * 7.0;
    float weather = Noise2(across) * 0.5 + Noise2(across * 3.1) * 0.3 + Noise2(across * 9.7) * 0.2;
    //Blocks a course high and a few long, every course staggered against the one under it as masonry is laid
    float row = floor((p.y - baseY) / COURSE);
    float2 blockAt = float2(u / 4.5 + Hash(float2(row, seed + 3.0)), row);
    float broken = step(0.9, Hash(floor(blockAt) + seed)) * saturate(1.0 - footprint * 1.2);
    albedo *= (0.62 + 0.55 * weather) * (1.0 - 0.3 * broken);

    //The casing near the top (Khafre's), and the sand drifted up the foot
    albedo = lerp(albedo, CASING * (0.95 + 0.1 * weather), smoothstep(1.0 - casing, 1.0 - casing + 0.02, up) * step(0.001, casing));
    float rise = SAND_RISE * (0.3 + 1.2 * Noise2(float2(u * 0.05, seed)) * weather);
    float drift = 1.0 - smoothstep(rise, rise + 0.015, up);
    albedo = lerp(albedo, SAND, drift);

    //The sun on the face and the sky over it
    float sun = saturate(dot(n, SunDirection));
    float3 sky = lerp(HorizonColor, ZenithColor, saturate(n.y * 0.5 + 0.5)) * 0.32;
    float3 color = albedo * (SunColor * sun * 1.1 + sky);

    //The air between, lighter than over the dunes
    float distance = length(p - CameraPosition);
    float haze = 1.0 - exp(-distance / max(PyramidHaze, 1.0));
    color = lerp(color, HorizonColor, haze * 0.7);

    return float4(color, 1.0);
}

technique Pyramid
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PyramidVS();
        PixelShader = compile PS_SHADERMODEL PyramidPS();
    }
};
