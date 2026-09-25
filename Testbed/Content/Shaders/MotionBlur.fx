//THE MOTION BLUR (#402): a per-pixel velocity buffer and a reconstruction filter over the HDR scene, after
//McGuire, Hennessy, Bukowski and Osman, "A Reconstruction Filter for Plausible Motion Blur" (I3D 2012).
//Only the Game builds it; Prazsky.Core's MotionBlur class drives it and PostProcessPipeline reads its result
//in place of the scene target. See "Motion blur" in docs/rendering.md.
//
//Five techniques, in the order a frame runs them:
//  Velocity     - the moving objects (the gun, the balls) drawn into the velocity target: per pixel, HALF the
//                 screen distance the surface covered over the shutter, in back-buffer pixels, plus its view
//                 depth. Alpha 1 marks a pixel as covered; everything else is "background".
//  TileMaxX/Y   - the largest velocity in each TileSize x TileSize tile, in two separable passes.
//  NeighborMax  - the largest over each tile's 3x3 neighbourhood: the widest smear that can reach into it.
//  Reconstruct  - the scene gathered along that velocity, each sample weighed by whether it is in front and
//                 whether its own smear (or this pixel's) reaches across the distance between them.
//
//⚠ THE VELOCITY IS HALF THE SHUTTER'S MOTION throughout, which is the paper's convention: a pixel is smeared
//from -v to +v around where it is, so |v| is the smear's reach either side and the cone and cylinder weights
//below read it directly.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//---------------------------------------------------------------------------------------------------------
//Velocity pass
//---------------------------------------------------------------------------------------------------------

float4x4 ViewProjection;
float4x4 ShutterViewProjection;
float4x4 Bone;

//NDC delta -> half-velocity in back-buffer pixels: (width / 4, -height / 4). A quarter because NDC spans two
//units across the target and the velocity is half the motion; negative Y because NDC's Y points up.
float2 VelocityScale;

//The longest half-velocity any pixel may carry, in pixels. It is also the tile size (see MotionBlur.cs): the
//neighbour-max only ever looks one tile out, so a smear longer than a tile would be cut off at the tile edge.
float MaxHalfVelocity;

struct VelocityVSInput
{
    float4 Position : POSITION0;
    float4 WorldRow1 : TEXCOORD1;
    float4 WorldRow2 : TEXCOORD2;
    float4 WorldRow3 : TEXCOORD3;
    float4 WorldRow4 : TEXCOORD4;
    float4 ShutterRow1 : TEXCOORD5;
    float4 ShutterRow2 : TEXCOORD6;
    float4 ShutterRow3 : TEXCOORD7;
    float4 ShutterRow4 : TEXCOORD8;
};

struct VelocityVSOutput
{
    float4 Position : SV_POSITION;
    float4 Now : TEXCOORD0;
    float4 Then : TEXCOORD1;
};

VelocityVSOutput VelocityVS(VelocityVSInput input)
{
    VelocityVSOutput output;

    float4 local = mul(input.Position, Bone);
    float4x4 world = float4x4(input.WorldRow1, input.WorldRow2, input.WorldRow3, input.WorldRow4);
    float4x4 shutter = float4x4(input.ShutterRow1, input.ShutterRow2, input.ShutterRow3, input.ShutterRow4);

    output.Now = mul(mul(local, world), ViewProjection);
    output.Then = mul(mul(local, shutter), ShutterViewProjection);
    output.Position = output.Now;

    return output;
}

float4 VelocityPS(VelocityVSOutput input) : COLOR
{
    //Divided per pixel, not per vertex: the two clip positions interpolate linearly in clip space, and the
    //perspective divide of the interpolated values is the exact screen position of THIS pixel's surface point.
    float2 now = input.Now.xy / input.Now.w;

    //A point that was behind the lens when the shutter opened has no screen position to have come from; it
    //carries no motion rather than a huge one.
    float2 velocity = 0;
    if (input.Then.w > 1e-3)
    {
        float2 then = input.Then.xy / input.Then.w;
        velocity = (now - then) * VelocityScale;

        float speed = length(velocity);
        if (speed > MaxHalfVelocity) velocity *= MaxHalfVelocity / speed;
    }

    //Clip w is the view depth for a perspective projection: what the reconstruction orders surfaces by
    return float4(velocity, input.Now.w, 1);
}

technique Velocity
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL VelocityVS();
        PixelShader = compile PS_SHADERMODEL VelocityPS();
    }
};

//---------------------------------------------------------------------------------------------------------
//Full-screen passes
//---------------------------------------------------------------------------------------------------------

texture VelocityTexture;
sampler2D VelocitySampler = sampler_state
{
    Texture = <VelocityTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//The tile passes' source (the velocity target, or the previous tile pass's output)
texture TileSourceTexture;
sampler2D TileSourceSampler = sampler_state
{
    Texture = <TileSourceTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

texture NeighborTexture;
sampler2D NeighborSampler = sampler_state
{
    Texture = <NeighborTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//The HDR scene, read BILINEARLY at back-buffer pixel centres: at supersample factor 2 such a tap lands on the
//corner four scene texels share and is their exact box filter, which is the resolve's own reading; at factor 1
//it lands on a texel centre and is that texel. So every tap below is one fetch whatever the factor.
texture SceneTexture;
sampler2D SceneSampler = sampler_state
{
    Texture = <SceneTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

float2 TileSourceTexelSize;
int TileSize;

//The back buffer's size in pixels, and one pixel of it in UV
float2 OutputSize;
float2 OutputTexelSize;

//THE BACKGROUND: every pixel no moving object covers. It has no depth to reproject by (MonoGame cannot sample
//the scene's depth buffer), so it is reprojected as if it all stood at ONE depth — the one the caller chose,
//the cluster's — through this matrix, which takes a point of this frame's NDC at that depth to the shutter's
//clip space. That is exact for a camera turning in place (precise aim, the recoil's shake), and for the
//orbiting overview it holds the cluster's own depth still while the far scenery turns a little too slowly —
//which is the direction to be wrong in.
float4x4 BackgroundReproject;
float BackgroundDepthNdc;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput FullScreenVS(float3 position : POSITION0, float2 texCoord : TEXCOORD0)
{
    VertexShaderOutput output;
    output.Position = float4(position, 1);
    output.TexCoord = texCoord;
    return output;
}

//The longer of two velocities
float2 VMax(float2 a, float2 b)
{
    return dot(a, a) >= dot(b, b) ? a : b;
}

//TILE MAX, ACROSS: one texel per tile column, the longest velocity along TileSize source texels of its row.
float4 TileMaxXPS(VertexShaderOutput input) : COLOR
{
    float x0 = floor(input.Position.x) * TileSize;
    float v = input.TexCoord.y;

    float2 best = 0;

    [loop]
    for (int i = 0; i < TileSize; i++)
    {
        float2 uv = float2((x0 + i + 0.5) * TileSourceTexelSize.x, v);
        best = VMax(best, tex2Dlod(TileSourceSampler, float4(uv, 0, 0)).xy);
    }

    return float4(best, 0, 0);
}

//TILE MAX, DOWN: the same over TileSize rows of the across pass's output.
float4 TileMaxYPS(VertexShaderOutput input) : COLOR
{
    float y0 = floor(input.Position.y) * TileSize;
    float u = input.TexCoord.x;

    float2 best = 0;

    [loop]
    for (int i = 0; i < TileSize; i++)
    {
        float2 uv = float2(u, (y0 + i + 0.5) * TileSourceTexelSize.y);
        best = VMax(best, tex2Dlod(TileSourceSampler, float4(uv, 0, 0)).xy);
    }

    return float4(best, 0, 0);
}

//NEIGHBOUR MAX: the longest of the 3x3 tiles round this one — every smear that can reach into this tile, since
//none is longer than a tile.
float4 NeighborMaxPS(VertexShaderOutput input) : COLOR
{
    float2 best = 0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 uv = input.TexCoord + float2(x, y) * TileSourceTexelSize;
            best = VMax(best, tex2Dlod(TileSourceSampler, float4(uv, 0, 0)).xy);
        }
    }

    return float4(best, 0, 0);
}

//How deep a surface may be behind another and still count as level with it, in world units. Half a ball: two
//neighbouring balls of the cluster blur into each other, a ball and the gun a dozen units behind it do not.
static const float SOFT_Z_EXTENT = 0.5;

//The depth the background is given, for the ordering: behind everything.
static const float BACKGROUND_DEPTH = 60000.0;

//Below this reach in pixels a smear is not drawn at all: the pixel is copied through.
static const float MIN_HALF_VELOCITY = 0.5;

//Samples per pixel of reach, and the most any pixel takes. One per two pixels of the full length: at one per three
//the jittered taps of a long smear read as a speckle over the streak in a still frame.
static const float SAMPLES_PER_PIXEL = 0.5;
static const int MAX_SAMPLES = 32;

//1 where the surface at depth za is in front of (or level with) the one at zb, fading to 0 SOFT_Z_EXTENT behind
float SoftDepthCompare(float za, float zb)
{
    return saturate(1 - (za - zb) / SOFT_Z_EXTENT);
}

float Cone(float distance, float reach)
{
    return saturate(1 - distance / reach);
}

float Cylinder(float distance, float reach)
{
    return 1 - smoothstep(0.95 * reach, 1.05 * reach, distance);
}

//Jimenez's interleaved gradient noise: a per-pixel offset for the sample positions, so the discrete taps of a
//long smear dissolve into fine grain instead of stacking into ghost copies of the object
float InterleavedGradientNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

float2 BackgroundVelocity(float2 uv)
{
    float2 ndc = float2(uv.x * 2 - 1, 1 - uv.y * 2);
    float4 then = mul(float4(ndc, BackgroundDepthNdc, 1), BackgroundReproject);

    if (then.w <= 1e-3) return 0;

    float2 velocity = (ndc - then.xy / then.w) * float2(OutputSize.x * 0.25, -OutputSize.y * 0.25);

    float speed = length(velocity);
    return speed > MaxHalfVelocity ? velocity * (MaxHalfVelocity / speed) : velocity;
}

//This pixel's velocity and depth: the object's where one is drawn, the background's everywhere else
void Surface(float2 uv, out float2 velocity, out float depth)
{
    float4 sampled = tex2Dlod(VelocitySampler, float4(uv, 0, 0));

    if (sampled.a > 0.5)
    {
        velocity = sampled.xy;
        depth = sampled.z;
    }
    else
    {
        velocity = BackgroundVelocity(uv);
        depth = BACKGROUND_DEPTH;
    }
}

float4 ReconstructPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TexCoord;
    float3 centre = tex2Dlod(SceneSampler, float4(uv, 0, 0)).rgb;

    float2 velocityX;
    float depthX;
    Surface(uv, velocityX, depthX);

    //The widest smear that can reach this pixel: the tiles' objects, or the background's own here
    float2 widest = VMax(tex2Dlod(NeighborSampler, float4(uv, 0, 0)).xy, velocityX);
    float reachN = length(widest);

    //Nothing moves near here - the whole frame, most of the time. Tile-coherent, so the branch costs nothing.
    [branch]
    if (reachN < MIN_HALF_VELOCITY) return float4(centre, 1);

    float reachX = max(length(velocityX), MIN_HALF_VELOCITY);

    int samples = clamp((int)ceil(2 * reachN * SAMPLES_PER_PIXEL), 4, MAX_SAMPLES);
    float jitter = InterleavedGradientNoise(input.Position.xy) - 0.5;

    //The pixel itself, weighed down by its own speed: a fast surface is spread over many pixels, so it owns
    //little of any one of them
    float weight = 1 / reachX;
    float3 sum = centre * weight;

    [loop]
    for (int i = 0; i < samples; i++)
    {
        //Evenly across -1..1 of the reach, never on the centre itself
        float t = lerp(-1.0, 1.0, (i + 0.5 + jitter) / samples);
        float2 offset = widest * t;
        float distance = length(offset);

        //Snapped to a pixel centre, where the scene read is an exact box filter
        float2 pixelY = floor(input.Position.xy + offset) + 0.5;
        float2 uvY = pixelY * OutputTexelSize;

        float2 velocityY;
        float depthY;
        Surface(uvY, velocityY, depthY);
        float reachY = max(length(velocityY), MIN_HALF_VELOCITY);

        //In front of X / behind X
        float front = SoftDepthCompare(depthY, depthX);
        float back = SoftDepthCompare(depthX, depthY);

        float alpha = front * Cone(distance, reachY)
            + back * Cone(distance, reachX)
            + Cylinder(distance, reachY) * Cylinder(distance, reachX) * 2;

        weight += alpha;
        sum += tex2Dlod(SceneSampler, float4(uvY, 0, 0)).rgb * alpha;
    }

    return float4(sum / weight, 1);
}

technique TileMaxX
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FullScreenVS();
        PixelShader = compile PS_SHADERMODEL TileMaxXPS();
    }
};

technique TileMaxY
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FullScreenVS();
        PixelShader = compile PS_SHADERMODEL TileMaxYPS();
    }
};

technique NeighborMax
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FullScreenVS();
        PixelShader = compile PS_SHADERMODEL NeighborMaxPS();
    }
};

technique Reconstruct
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FullScreenVS();
        PixelShader = compile PS_SHADERMODEL ReconstructPS();
    }
};
