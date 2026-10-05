//GamePi's Potato effects (#789): the end of the frame, done by each effect itself.
//
//The desktop renders linear radiance into a half-float target and turns it into a picture ONCE, in Tonemap.fx's
//resolve: exposure, Narkowicz's ACES fit, the sRGB curve. Potato has no such target - an RGBA16F target and its
//resolve cost the Pi as much again as the whole scene (9.0 against 4.5 ms at 1080p, #785) - so every Potato effect
//draws straight into the 8-bit back buffer and ends with the same three steps on its own colour. Colours stay
//linear radiance up to here, exactly as on the desktop, so a value tuned there means the same thing here.
//
//What this gives up, knowingly: the curve is applied to each surface before blending rather than to the blended
//sum, so a translucent layer over another tonemaps a little differently, and an additive pile of sparks saturates
//per spark rather than as a whole. The glare, the grain and the defocus of the resolve are not here at all.

//Which compiler this is being read by, and so what a technique calls its shaders' profiles (#808)
#include "PotatoProfile.fxh"

//The player's brightness (#711). PostProcessPipeline.DEFAULT_EXPOSURE until the host pushes the setting; it is a
//uniform of every Potato effect, so the host sets it on each.
float Exposure = 1.1;

//Jim Hejl's cubic fit of the sRGB curve, as InstancedModel.fx has it: the host hands colours over as authored, sRGB
float3 SrgbToLinear(float3 color)
{
    return color * (color * (color * 0.305306011 + 0.682171111) + 0.012522878);
}

//Tonemap.fx's ACESFilmic, verbatim
float3 ACESFilmic(float3 x)
{
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;

    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

//Tonemap.fx's LinearToSrgb, verbatim: the exact piecewise curve, not a 1/2.2 power
float3 LinearToSrgb(float3 c)
{
    c = max(c, 0);

    return lerp(c * 12.92, 1.055 * pow(c, 1.0 / 2.4) - 0.055, step(0.0031308, c));
}

//Linear radiance to what the back buffer holds
float3 ToDisplay(float3 radiance)
{
    return LinearToSrgb(ACESFilmic(radiance * Exposure));
}

//No premultiplied variant (curving rgb / alpha and multiplying the alpha back in): it is right for a surface's own
//colour and wrong for light added over the background, which it caps at the surface's alpha - the trails, the
//wormhole, the confetti and PotatoModel's glass each say how they end instead (#789's review).
