//GamePi's SM 3.0 port of Prazsky.Shaders' ShotTrail.fx (#789): the colour streak of a freshly shot ball, and in the
//Game the aim beam and the line-loss sparks, which borrow this effect (AimBeam, LineSparks), compiled for OpenGL
//through vs_3_0/ps_3_0 and MojoShader. The desktop file's header says what the streak is for; what changed here:
//  - POSITION0 on the vertex output, because SM 3.0 has no SV_POSITION, and a pixel input of its own without it,
//    because a ps_3_0 shader may not read POSITION.
//  - The two "is this vector long enough to normalize" selects divide by a clamped length. vs_3_0 has no select
//    instruction, so fxc may build a ternary as a blend of BOTH sides, and the unused side's 0/0 would then put a NaN
//    into the position. On SM 5.0 a movc never looked at that side.
//  - The pixel shader ends on the display curve (PotatoOutput.fxh), because Potato draws straight into the 8-bit back
//    buffer instead of into a linear target that is tonemapped later. The return says which light it curves.
//The billboard, the profile and the fades are the desktop's, line for line. The uniforms are the ones LaunchSmears,
//AimBeam and LineSparks fetch, plus PotatoOutput's Exposure.
//
//Drawn with BlendState.Additive, depth-read and writing no depth (the owners set that state). The colour is linear
//radiance boosted over 1. On the desktop that radiance was meant to bloom through the glare pass; Potato has no glare,
//so here it only reaches the curve's bright core.

#include "PotatoOutput.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 TrailHead;       //leading (far) tip of the smear: muzzle + shot direction * trail length
float3 TrailTail;       //the muzzle end (the smear is anchored there, mostly hidden by the barrel)
float TrailHeadWidth;   //half-width at the head
float TrailTailWidth;   //half-width at the tail
float3 TrailColor;      //linear radiance, already boosted and hue-floored on the CPU
float TrailAlpha;       //overall launch fade, 1 at the shot down to 0

struct TrailVertexInput
{
    float3 Position : POSITION0; //ignored; the quad is placed from TrailHead/TrailTail
    float2 Data : TEXCOORD0;     //(side in {-1,1}, along in {0 tail, 1 head})
};

struct TrailVertexOutput
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;       //(side, along)
};

//What the pixel shader reads: the vertex output without its position
struct TrailPixelInput
{
    float2 UV : TEXCOORD0;
};

TrailVertexOutput TrailVS(TrailVertexInput input)
{
    TrailVertexOutput output;

    float along = input.Data.y;
    float3 pos = lerp(TrailTail, TrailHead, along);

    //The divisors are clamped even though each select already guards them (see the header)
    float3 axis = TrailHead - TrailTail;
    float axisLen = length(axis);
    float3 dir = axisLen > 1e-4 ? axis / max(axisLen, 1e-4) : float3(0.0, 1.0, 0.0);

    //Billboard about the streak axis: the width runs perpendicular to both the axis and the view ray, so the
    //streak keeps its thickness from any angle and collapses edge-on to a line, as a real thin smear would.
    float3 toCam = CameraPosition - pos;
    float3 side = cross(dir, toCam);
    float sideLen = length(side);
    side = sideLen > 1e-4 ? side / max(sideLen, 1e-4) : float3(1.0, 0.0, 0.0);

    float width = lerp(TrailTailWidth, TrailHeadWidth, along);
    pos += side * (input.Data.x * width);

    output.Position = mul(mul(float4(pos, 1.0), View), Projection);
    output.UV = float2(input.Data.x, along);

    return output;
}

float4 TrailPS(TrailPixelInput input) : COLOR
{
    float across = 1.0 - abs(input.UV.x); //1 at the core, 0 at the edges
    float along = input.UV.y;             //0 at the muzzle end, 1 at the leading tip

    float profile = across * across;      //soft, round-ish falloff across the streak

    //Soft at both ends along its length, so the streak dissolves smoothly instead of stopping at a hard
    //rectangular edge.
    float lengthFade = smoothstep(0.0, 0.25, along) * smoothstep(1.0, 0.75, along);

    //No clip. With additive blending a dark pixel adds nothing, so the streak can fade smoothly to nothing
    //everywhere. A clip on a value scaled by TrailAlpha would sweep inward as the smear fades and cut it with a hard
    //moving edge.
    float a = profile * lengthFade * TrailAlpha;

    //THE OUTPUT. BlendState.Additive is SourceAlpha/One, so the light the desktop adds to its linear target is
    //TrailColor * a * a. The alpha weighs the light twice, and that squared falloff is the streak every tuning was
    //judged by. Potato cannot sum radiance first and curve the sum, so it curves the light this pixel adds (exactly
    //that product) and returns alpha 1, so the same blend state adds it unchanged. Over a dark backdrop that matches
    //the desktop's curve exactly (its glare aside). Over a bright one the 8-bit sum clips sooner than the desktop's
    //curve would bend, which no additive blend in display space can avoid.
    //
    //It does not curve TrailColor alone and weight it by a afterwards. That is ToDisplayPremultiplied's rule, right
    //for a surface's coverage but wrong for a glow: this radiance sits on the curve's flat shoulder, so the soft body
    //would shrink to a thin core. At a = 0.5 and a colour of 4, that rule gives 0.25 of full white where the desktop
    //shows 0.92 (PotatoOutput's curve at its default exposure). Nothing reads the back buffer's alpha, so the 1
    //written there does no harm.
    return float4(ToDisplay(TrailColor * (a * a)), 1.0);
}

technique ShotTrail
{
    pass P0
    {
        VertexShader = compile vs_3_0 TrailVS();
        PixelShader = compile ps_3_0 TrailPS();
    }
};
