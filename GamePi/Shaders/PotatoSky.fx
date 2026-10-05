//GamePi's Potato tier (#789): the sky dome, as nothing but its own gradient. SkyDome hands any effect it is given the
//dome's World/View/Projection and its vertices (VertexPositionColor), and the desktop gives it Sky.fx, whose cloud deck
//and sun disc are Shader Model 5.0 work Potato does without. This draws the stored palette alone - linear radiance, the
//dome built with linearVertexColors: true as on the desktop - and ends in PotatoOutput.fxh's exposure and curve like
//every other Potato surface, so the player's brightness moves the sky with the rest of the frame (#789's review: drawn
//through SkyDome's own BasicEffect it took neither, and stood still while everything under it darkened or brightened).

#include "PotatoOutput.fxh"

float4x4 World;
float4x4 View;
float4x4 Projection;

struct VertexInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
};

struct VertexOutput
{
    float4 Position : POSITION0;
    float3 Color : TEXCOORD0;
};

VertexOutput SkyVS(VertexInput input)
{
    VertexOutput output;

    output.Position = mul(mul(mul(input.Position, World), View), Projection);
    output.Color = input.Color.rgb;

    return output;
}

float4 SkyPS(VertexOutput input) : COLOR0
{
    return float4(ToDisplay(input.Color), 1);
}

technique PotatoSky
{
    pass P0
    {
        VertexShader = compile vs_3_0 SkyVS();
        PixelShader = compile ps_3_0 SkyPS();
    }
};
