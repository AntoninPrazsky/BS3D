//The launch smear of a freshly shot ball: the ball leaves the cannon very fast (~200 u/s, several ball
//diameters a frame), so a colour streak is drawn to sell it. The C# side anchors it at the muzzle and points
//it along the shot (the ball attaches within ~0.075 s, too brief to trail), passing the two ends as TrailHead
//and TrailTail; one billboard per live smear, turned to face the camera about that axis. The pixel shader is
//the streak itself - a soft comet, brightest and full width at the head, tapering and fading to the tail; the
//caller puts the bright head at the *leading* (far) end out in the open and the faint tail at the muzzle,
//since the muzzle end is hidden behind the barrel. Drawn additively and in linear radiance boosted over 1, so
//it glows and blooms through the glare pass like the emissive balls; depth-read (the cluster/platform/cannon
//in front hide it) but writes no depth. Its overall alpha fades over the shot's first fraction of a second
//(TrailAlpha, set on the CPU), so it is a launch burst that leaves the ball crisp. Built by the Testbed
//and the Game (the editor never shoots). SM 5.0.
//
//The second technique, AimRay, is the aim beam's far end (#1196, AimBeam): the same billboard drawn as a CRISP thin
//line instead of a soft streak - covered whole across it but for a pixel's anti-aliasing at its edges, no fades at its
//ends, its alpha running from RayTailAlpha to RayHeadAlpha along it - for premultiplied alpha blending
//(BlendState.AlphaBlend), so it lays its colour over what is behind it and never adds light to it. A technique of its
//own rather than a switch on ShotTrail, because the effect is shared with LaunchSmears and LineSparks, whose pass is
//this file's first technique and who set none of the ray's parameters.

#include "ShaderTarget.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 TrailHead;       //leading (far) tip of the smear: muzzle + shot direction * trail length (fixed; the ball is long past it)
float3 TrailTail;       //the muzzle end (the smear is anchored there, mostly hidden by the barrel)
float TrailHeadWidth;   //half-width at the head
float TrailTailWidth;   //half-width at the tail
float3 TrailColor;      //linear radiance, already boosted and hue-floored on the CPU
float TrailAlpha;       //overall launch fade, 1 at the shot down to 0

float RayTailAlpha;     //AimRay only: the line's alpha at its tail
float RayHeadAlpha;     //AimRay only: the line's alpha at its head

struct TrailVertexInput
{
    float3 Position : POSITION0; //ignored; the quad is placed from TrailHead/TrailTail
    float2 Data : TEXCOORD0;     //(side in {-1,1}, along in {0 tail, 1 head})
};

struct TrailVertexOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;       //(side, along)
};

TrailVertexOutput TrailVS(TrailVertexInput input)
{
    TrailVertexOutput output;

    float along = input.Data.y;
    float3 pos = lerp(TrailTail, TrailHead, along);

    float3 axis = TrailHead - TrailTail;
    float axisLen = length(axis);
    float3 dir = axisLen > 1e-4 ? axis / axisLen : float3(0.0, 1.0, 0.0);

    //Billboard about the streak axis: the width runs perpendicular to both the axis and the view ray, so the
    //streak keeps its thickness from any angle and collapses edge-on to a line (as a real thin smear would).
    float3 toCam = CameraPosition - pos;
    float3 side = cross(dir, toCam);
    float sideLen = length(side);
    side = sideLen > 1e-4 ? side / sideLen : float3(1.0, 0.0, 0.0);

    float width = lerp(TrailTailWidth, TrailHeadWidth, along);
    pos += side * (input.Data.x * width);

    output.Position = mul(mul(float4(pos, 1.0), View), Projection);
    output.UV = float2(input.Data.x, along);

    return output;
}

float4 TrailPS(TrailVertexOutput input) : COLOR
{
    float across = 1.0 - abs(input.UV.x); //1 at the core, 0 at the edges
    float along = input.UV.y;             //0 at the muzzle end, 1 at the leading tip

    float profile = across * across;      //soft, round-ish falloff across the streak

    //Soft at both ends along its length: fades in from the muzzle and out to the leading tip (holding full
    //through the middle), so the streak dissolves smoothly instead of stopping at a hard rectangular edge.
    float lengthFade = smoothstep(0.0, 0.25, along) * smoothstep(1.0, 0.75, along);

    //No clip. With additive blending a zero-alpha pixel adds nothing, so the streak can fade smoothly to
    //nothing everywhere. A clip on (profile * TrailAlpha) would sweep inward as the smear fades and cut it
    //with a hard moving edge halfway through the fade - which read as the streak being sliced off.
    float a = profile * lengthFade * TrailAlpha;
    return float4(TrailColor * a, a);
}

//The aim beam's crisp line: covered whole across, the edge anti-aliased over the pixel the side coordinate crosses it
//in (fwidth), so a line a few pixels wide reads as drawn geometry rather than a glow, and a sub-pixel one fades by its
//coverage instead of flickering. Premultiplied, for BlendState.AlphaBlend.
float4 RayPS(TrailVertexOutput input) : COLOR
{
    float coverage = saturate((1.0 - abs(input.UV.x)) / max(fwidth(input.UV.x), 1e-4));
    float a = coverage * saturate(lerp(RayTailAlpha, RayHeadAlpha, input.UV.y));
    return float4(TrailColor * a, a);
}

//The first technique, so CurrentTechnique.Passes[0] (what LaunchSmears and LineSparks cache) is still the streak
technique ShotTrail
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL TrailVS();
        PixelShader = compile PS_SHADERMODEL TrailPS();
    }
};

technique AimRay
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL TrailVS();
        PixelShader = compile PS_SHADERMODEL RayPS();
    }
};
