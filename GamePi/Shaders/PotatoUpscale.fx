//GamePi's Potato tier (#801): the 3D drawn below the display's resolution, scaled up onto the back buffer in one pass,
//before the menus and the HUD are drawn over it at the display's own size. The source is the 8-bit scene target, already
//through PotatoOutput.fxh's curve, so nothing here touches exposure or colour.
//
//Two ways up. Bilinear is one tap. Fxaa is FXAA 3.11's console variant (Timothy Lottes, NVIDIA; public domain) run in the
//SOURCE's texel grid at each output pixel, so the up-scale and the anti-aliasing are one pass and one read of the target:
//a cross of four corner taps decides whether the pixel is on an edge at all - the rest leave on the centre tap, five in
//all - and an edge is blended along its own direction from four more. Its luma is the display-encoded colour's, which is
//what FXAA is tuned on.

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
        VertexShader = compile vs_3_0 UpscaleVS();
        PixelShader = compile ps_3_0 BilinearPS();
    }
};

technique Fxaa
{
    pass P0
    {
        VertexShader = compile vs_3_0 UpscaleVS();
        PixelShader = compile ps_3_0 FxaaPS();
    }
};
