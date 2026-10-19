#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//THE GAME'S NAME ON HIGH AND ULTRA (#929): the 3D wordmark's letters, its badge's disc and their rims, shaded to
//match the PNG logo (Images/logo) rather than lit by the scene. The logo was rendered as a studio piece - glossy
//inflated tubes, a key light from the upper left laying a crisp white streak along every stroke, the colour running
//deep and saturated into the shadowed side - and the scene's own rig cannot be that over twenty-one domes (a dusk,
//a night, a black sky). So the light here is fixed in VIEW space, and the colours come out as the picture itself:
//every opaque pass writes a DISPLAY-LINEAR colour with a negative alpha, which the resolve recognises and lays over
//the mapped frame without the curve, the exposure, the grain or the colour fringe (Tonemap.fx, DisplayReferred).
//The additive halo is the one pass that stays light: it writes radiance with an alpha of zero, so the bloom
//still makes a halo of it.
//
//The eye is taken as straight down the view axis rather than per pixel. The block hangs in the frame's corner, and
//with the true eye ray a horizontal stroke on the right of the frame never faces the key light's half vector at all;
//with the axis every stroke carries the same streak wherever the block stands, and the block's own sway still moves
//it, since the normals turn with the block.

float4x4 WorldView;
float4x4 Projection;

float3 Albedo;          //linear display colour (Body, Disc, Flat); linear RADIANCE for Glow
float3 AlbedoB;         //the disc's second colour, at the far end of its gradient
float2 GradientAxis;    //the disc's gradient direction in its own face plane, over the disc's diameter

//The studio: a key light above, left and in front, and a small fill from the lower right whose reflection the logo
//shows as a second, fainter streak on each stroke's underside. View space: +x right, +y up, +z towards the lens.
static const float3 KEY = float3(-0.4208, 0.6211, 0.6612);     //normalize(-0.42, 0.62, 0.66)
static const float3 FILL = float3(0.6211, -0.4208, 0.6612);    //normalize(0.62, -0.42, 0.66)
static const float3 EYE = float3(0.0, 0.0, 1.0);

//Where the key's streak starts on a stroke (a cosine of the angle off the streak's own line round the tube) and how
//strong it lands; the disc's flat face must stay below its own figure at every sway, or the whole face flashes white.
static const float STREAK_AT = 0.984;
static const float DISC_STREAK_AT = 0.992;
static const float FILL_STREAK_AT = 0.995;

struct VertexInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 Axis : TEXCOORD0;        //the stroke's direction in the letter's plane; zero on the disc
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float3 Axis : TEXCOORD1;
    float2 Face : TEXCOORD2;        //the disc's own face-plane position (a lathe about its Y axis)
};

VertexOutput MainVS(VertexInput input)
{
    VertexOutput output;

    float4 view = mul(float4(input.Position, 1.0), WorldView);
    output.Position = mul(view, Projection);

    //The world matrix scales uniformly, so the upper 3x3 carries a direction faithfully up to its length
    output.Normal = mul(input.Normal, (float3x3)WorldView);
    output.Axis = mul(float3(input.Axis, 0.0), (float3x3)WorldView);
    output.Face = input.Position.xz;

    return output;
}

//One crisp-edged band: on from a cosine threshold, its edge ramped over one pixel's change so it is antialiased at any
//size rather than over a fixed fraction of a stroke
float Band(float s, float at)
{
    float w = max(fwidth(s), 1e-3);
    return smoothstep(at - w, at + w, s);
}

//The studio's look on a round surface of one colour. AXIS is the stroke's direction (zero on the disc): the half vector
//with the axis taken out of it is the direction a TUBE's normal has to face to reflect the light, so the streak runs
//the length of every stroke whichever way the stroke runs. With no axis it is an ordinary round highlight.
float3 StudioShade(float3 albedo, float3 normal, float3 axis, float streakAt)
{
    float3 N = normalize(normal);

    //The body: deep and saturated where it turns away from the key (a colour times itself is the same hue, darker
    //and purer), the colour itself where it faces it - the logo's letters are never grey in the shade
    float key = dot(N, KEY);
    float3 deep = albedo * albedo * 0.55;
    float3 colour = lerp(deep, albedo, smoothstep(-0.35, 0.55, key));

    //and lighter again on the side that faces it squarely, the logo's lit upper half of every stroke
    colour = lerp(colour, lerp(albedo, 1.0, 0.3), smoothstep(0.55, 0.95, key) * 0.6);

    //The round side runs deeper still towards the silhouette, and a lighter bounce of the colour lines the lower rim
    float facing = saturate(N.z);
    colour *= lerp(0.55, 1.0, sqrt(facing));
    float rim = pow(1.0 - facing, 3.0) * saturate(0.25 - N.y * 0.75);
    colour += lerp(albedo, 1.0, 0.5) * rim * 0.6;

    float axisLength = dot(axis, axis);
    float hasAxis = step(0.01, axisLength);
    float3 t = axis * rsqrt(max(axisLength, 1e-6)) * hasAxis;

    float3 H = normalize(KEY + EYE);
    float3 Hf = normalize(FILL + EYE);
    float3 h = normalize(H - dot(H, t) * t);
    float3 hf = normalize(Hf - dot(Hf, t) * t);

    float s = dot(N, h);
    float sf = dot(N, hf);

    //A soft sheen round the streak, then the fill's faint second streak, then the key's own, white and crisp
    colour += lerp(albedo, 1.0, 0.6) * pow(saturate(s), 16.0) * 0.30;
    colour = lerp(colour, lerp(albedo, 1.0, 0.75), Band(sf, FILL_STREAK_AT) * 0.5);
    colour = lerp(colour, 1.0, Band(s, streakAt) * 0.92);

    return colour;
}

float4 BodyPS(VertexOutput input) : COLOR
{
    return float4(StudioShade(Albedo, input.Normal, input.Axis, STREAK_AT), -1.0);
}

float4 DiscPS(VertexOutput input) : COLOR
{
    float3 albedo = lerp(Albedo, AlbedoB, saturate(0.5 + dot(input.Face, GradientAxis)));

    return float4(StudioShade(albedo, input.Normal, 0.0, DISC_STREAK_AT), -1.0);
}

float4 FlatPS(VertexOutput input) : COLOR
{
    return float4(Albedo, -1.0);
}

//Additive light, not picture: an alpha of zero leaves the frame's own alpha, and with it the frame's own resolve
float4 GlowPS(VertexOutput input) : COLOR
{
    return float4(Albedo, 0.0);
}

technique Body
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL BodyPS();
    }
};

technique Disc
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL DiscPS();
    }
};

technique Flat
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL FlatPS();
    }
};

technique Glow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL GlowPS();
    }
};
