//GamePi's Potato tier (#801): the 3D drawn below the display's resolution, scaled up onto the back buffer in one pass,
//before the menus and the HUD are drawn over it at the display's own size. The source is the 8-bit scene target, already
//through PotatoOutput.fxh's curve, so nothing here touches exposure or colour.
//
//Three ways up. Bilinear is one tap; Soft is the cubic B-spline in four (#804, at SoftPS). Fxaa is FXAA 3.11's console variant (Timothy Lottes, NVIDIA; public domain) run in the
//SOURCE's texel grid at each output pixel, so the up-scale and the anti-aliasing are one pass and one read of the target:
//a cross of four corner taps decides whether the pixel is on an edge at all - the rest leave on the centre tap, five in
//all - and an edge is blended along its own direction from four more. Its luma is the display-encoded colour's, which is
//what FXAA is tuned on.

#include "PotatoProfile.fxh"

float2 SourceTexel;

texture Source;
sampler2D SourceSampler = sampler_state
{
    Texture = <Source>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexData
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

//The quad comes in already in clip space, two triangles over the whole target
VertexData UpscaleVS(VertexData input)
{
    return input;
}

float4 BilinearPS(VertexData input) : COLOR0
{
    return float4(tex2D(SourceSampler, input.TexCoord).rgb, 1);
}

//Soft (#804): the cubic B-spline over the source's four-by-four texels, as four bilinear taps (Sigg and Hadwiger, GPU
//Gems 2: two neighbouring texels weighted w and v are one bilinear tap between them, v / (w + v) of the way, times w + v).
//Bilinear magnification has a kink at every texel centre, so a thin bright line - a ball's limb is exactly that - comes
//out as a string of beads on the source's grid: softened, and still plainly made of pixels. The B-spline has no kink
//(its second derivative is continuous) and no overshoot (its weights are all positive), so the line comes out even and
//the source's grid cannot be seen in it. The price is sharpness, which the owner gave up for this ("soft is fine,
//blocky is not"), and three more taps.
float4 SoftPS(VertexData input) : COLOR0
{
    float2 texel = input.TexCoord / SourceTexel - 0.5;
    float2 whole = floor(texel);
    float2 f = texel - whole;

    //The four weights along each axis, times six
    float2 w0 = f * (f * (3.0 - f) - 3.0) + 1.0;
    float2 w1 = f * f * (3.0 * f - 6.0) + 4.0;
    float2 w2 = f * (f * (3.0 - 3.0 * f) + 3.0) + 1.0;
    float2 w3 = f * f * f;

    //Two pairs an axis: each pair's weight, and where between its two texels one bilinear tap reads both
    float2 near = w0 + w1;
    float2 far = w2 + w3;
    float2 nearAt = (whole - 0.5 + w1 / near) * SourceTexel;
    float2 farAt = (whole + 1.5 + w3 / far) * SourceTexel;

    float3 colour
        = (tex2D(SourceSampler, float2(nearAt.x, nearAt.y)).rgb * near.x + tex2D(SourceSampler, float2(farAt.x, nearAt.y)).rgb * far.x) * near.y
        + (tex2D(SourceSampler, float2(nearAt.x, farAt.y)).rgb * near.x + tex2D(SourceSampler, float2(farAt.x, farAt.y)).rgb * far.x) * far.y;

    return float4(colour / 36.0, 1);
}

float Luma(float3 rgb)
{
    return dot(rgb, float3(0.299, 0.587, 0.114));
}

//FXAA 3.11's console defaults: how sharp an edge is kept, and the contrast below which a pixel is left alone - relative
//to the local maximum, and absolute for the dark end
static const float EDGE_SHARPNESS = 8.0;
static const float EDGE_THRESHOLD = 0.125;
static const float EDGE_THRESHOLD_MIN = 0.05;

float4 FxaaPS(VertexData input) : COLOR0
{
    float2 pos = input.TexCoord;
    float2 half_ = SourceTexel * 0.5;

    float lumaNw = Luma(tex2D(SourceSampler, pos + float2(-half_.x, -half_.y)).rgb);
    float lumaSw = Luma(tex2D(SourceSampler, pos + float2(-half_.x, half_.y)).rgb);
    float lumaNe = Luma(tex2D(SourceSampler, pos + float2(half_.x, -half_.y)).rgb) + 1.0 / 384.0;
    float lumaSe = Luma(tex2D(SourceSampler, pos + float2(half_.x, half_.y)).rgb);
    float3 rgbM = tex2D(SourceSampler, pos).rgb;
    float lumaM = Luma(rgbM);

    float lumaMax = max(max(lumaNw, lumaSw), max(lumaNe, lumaSe));
    float lumaMin = min(min(lumaNw, lumaSw), min(lumaNe, lumaSe));
    float range = max(lumaMax, lumaM) - min(lumaMin, lumaM);

    [branch]
    if (range < max(EDGE_THRESHOLD_MIN, lumaMax * EDGE_THRESHOLD))
        return float4(rgbM, 1);

    float dirSwMinusNe = lumaSw - lumaNe;
    float dirSeMinusNw = lumaSe - lumaNw;
    float2 dir1 = normalize(float2(dirSwMinusNe + dirSeMinusNw, dirSwMinusNe - dirSeMinusNw) + 1e-6);

    float3 rgbN1 = tex2D(SourceSampler, pos - dir1 * half_).rgb;
    float3 rgbP1 = tex2D(SourceSampler, pos + dir1 * half_).rgb;

    float dirAbsMinTimesC = min(abs(dir1.x), abs(dir1.y)) * EDGE_SHARPNESS;
    float2 dir2 = clamp(dir1 / max(dirAbsMinTimesC, 1e-4), -2.0, 2.0);

    float3 rgbN2 = tex2D(SourceSampler, pos - dir2 * SourceTexel * 2.0).rgb;
    float3 rgbP2 = tex2D(SourceSampler, pos + dir2 * SourceTexel * 2.0).rgb;

    float3 rgbA = rgbN1 + rgbP1;
    float3 rgbB = (rgbN2 + rgbP2) * 0.25 + rgbA * 0.25;

    //The wide blend overshoots on a thin feature: keep the narrow one when it left the local range
    float lumaB = Luma(rgbB);
    return float4(lumaB < lumaMin || lumaB > lumaMax ? rgbA * 0.5 : rgbB, 1);
}

technique Bilinear
{
    pass P0
    {
        VertexShader = compile POTATO_VS UpscaleVS();
        PixelShader = compile POTATO_PS BilinearPS();
    }
};

technique Soft
{
    pass P0
    {
        VertexShader = compile POTATO_VS UpscaleVS();
        PixelShader = compile POTATO_PS SoftPS();
    }
};

technique Fxaa
{
    pass P0
    {
        VertexShader = compile POTATO_VS UpscaleVS();
        PixelShader = compile POTATO_PS FxaaPS();
    }
};
