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

//The curve per VERTEX, not per pixel: the dome is the whole background, every pixel of the frame, and the exposure, the
//ACES fit and the sRGB curve on each of them measured as part of a 1-2.5 ms rise on the Pi at 1080p (#789's review
//fixes against the build before them). Its palette is a smooth vertical gradient over a few dozen vertices, so the
//curve interpolated between them is the same picture to the eye at a cost of nothing.
VertexOutput SkyVS(VertexInput input)
{
    VertexOutput output;

    output.Position = mul(mul(mul(input.Position, World), View), Projection);
    output.Color = ToDisplay(input.Color.rgb);

    return output;
}

float4 SkyPS(VertexOutput input) : COLOR0
{
    return float4(input.Color, 1);
}

technique PotatoSky
{
    pass P0
    {
        VertexShader = compile POTATO_VS SkyVS();
        PixelShader = compile POTATO_PS SkyPS();
    }
};

//--- THE SKIES THAT REPLACE THE SKY (#829): space, the aurora and the dream ------------------------------------------
//On the desktop each is a full-screen pass of its own (Space.fx, Aurora.fx, Dream.fx), marched and noised in Shader
//Model 5.0. Here they are the same dome, drawn with a figure computed per pixel from the view direction alone - the
//dome's own vertex, which SkyDome keeps centred on the camera, so a vertex position IS a direction - over the scene's
//void instead of the stored palette. Everything is sines, hashes and a lattice: no noise octaves, no march, nothing
//that needs a texture. The curve is per pixel here (ToDisplay), the thing the gradient above avoids: a star is a
//point and a curtain a glow, neither of which a vertex can carry. Three scenes of fourteen pay it.

float SkyTime;
float3 SkyVoid;         //the colour behind everything, linear (the scene's void or its deep)
float3 SkyColorA;       //space: the Milky Way's colour; the aurora: its low colour; the dream: palette A
float3 SkyColorB;       //space: the nebula's colour; the aurora: its high colour; the dream: palette B
float3 SkyColorC;       //space: the stars' tint; the dream: palette C
float3 SkyColorD;       //space: the Milky Way's core; the dream: palette D
float3 SkyAxis;         //space: the band's normal
float3 SkyAxis2;        //space: where the nebula stands
float4 SkyParams;       //the aurora: intensity, band height, ray strength, pulse depth; the dream: swirl scale, warp, slow speed, fast speed
float4 SkyParams2;      //the aurora: drift speed, morph speed, curtain scale, ray scale; the dream: ribbon sharpness, brightness, orb brightness, orb width

struct FigureOutput
{
    float4 Position : POSITION0;
    float3 Direction : TEXCOORD0;
};

FigureOutput FigureVS(VertexInput input)
{
    FigureOutput output;
    output.Position = mul(mul(mul(input.Position, World), View), Projection);
    output.Direction = input.Position.xyz;
    return output;
}

//Dave Hoskins's hashes, the ones the desktop's Stars.fxh uses too
float Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float3 Hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

//One layer of stars: a cube lattice over the direction scaled by cells, at most one star in a cell, jittered over
//it, lit by its own roll against chance and as bright as a steep law makes it (most stars faint). Only the pixel's
//own cell is looked up, so a star on a cell wall is clipped - at a few pixels across, nothing the eye finds.
float StarLayer(float3 dir, float cells, float chance, float peak, float core)
{
    float3 p = dir * cells;
    float3 cell = floor(p);
    float3 jitter = Hash33(cell);
    float roll = Hash31(cell + 7.1);
    float lit = step(roll, chance);
    float bright = Hash31(cell + 11.7);
    bright = bright * bright * bright * peak;
    float d = length(p - (cell + jitter)) / (core * cells);
    float glow = saturate(1.0 - d);
    return lit * bright * glow * glow;
}

float4 SpacePS(FigureOutput input) : COLOR0
{
    float3 dir = normalize(input.Direction);

    //The Milky Way: a band across the sky about its own axis, brighter and warmer at its core, with the star clouds
    //as a slow mottle of sines along it
    float across = dot(dir, SkyAxis);
    float band = exp(-across * across * 38.0);
    float clouds = 0.55 + 0.45 * sin(dir.x * 7.3 + dir.y * 5.1) * sin(dir.z * 6.7 - dir.x * 3.9);
    float3 way = lerp(SkyColorA, SkyColorD, band * band) * (band * clouds);

    //The nebula: a soft patch where SkyAxis2 points
    float towards = saturate(dot(dir, SkyAxis2));
    float nebula = pow(towards, 60.0) * (0.7 + 0.3 * sin(dir.y * 11.0 + dir.z * 9.0));

    //Three layers of stars, denser in the band, as the desktop's three are
    float stars = StarLayer(dir, 34.0, 0.06 + 0.10 * band, 1.0, 0.09)
        + StarLayer(dir, 70.0, 0.14 + 0.25 * band, 0.45, 0.075)
        + StarLayer(dir, 130.0, 0.22 + 0.4 * band, 0.18, 0.07);

    float3 colour = SkyVoid + way + nebula * SkyColorB + stars * SkyColorC;
    return float4(ToDisplay(colour), 1);
}

float4 AuroraPS(FigureOutput input) : COLOR0
{
    float3 dir = normalize(input.Direction);
    float intensity = SkyParams.x, bandHeight = SkyParams.y, rayStrength = SkyParams.z, pulseDepth = SkyParams.w;
    float driftSpeed = SkyParams2.x, morphSpeed = SkyParams2.y, curtainScale = SkyParams2.z, rayScale = SkyParams2.w;

    //The band: from a little over the horizon up through bandHeight of the sky, fading before the zenith
    float elevation = dir.y;
    float band = smoothstep(0.06, 0.26, elevation) * (1.0 - smoothstep(0.1 + bandHeight, 0.98, elevation));

    //The drift turns the sample direction about the vertical, as the desktop's does, so there is no azimuth to seam
    float sinDrift, cosDrift;
    sincos(SkyTime * driftSpeed, sinDrift, cosDrift);
    float3 r = float3(dir.x * cosDrift - dir.z * sinDrift, dir.y, dir.x * sinDrift + dir.z * cosDrift);

    //The folds: sines along the band, compressed little along the vertical so a fold is a streak, re-forming in place
    float morph = SkyTime * morphSpeed;
    float folds = sin(r.x * 2.1 * curtainScale + r.z * 1.3 * curtainScale + morph * 1.7)
        + 0.6 * sin(r.z * 4.3 * curtainScale - r.x * 2.7 * curtainScale - morph * 1.1 + r.y * 0.8)
        + 0.35 * sin(r.x * 8.9 * curtainScale + r.z * 6.1 * curtainScale + morph * 2.9);
    float curtain = saturate(0.5 + 0.3 * folds);
    curtain = curtain * curtain * curtain;

    //The rays: a fine comb of thin striations cutting into the curtain, sliding a little faster than the folds re-form
    float rays = 0.5 + 0.5 * sin((r.x * 7.3 + r.z * 11.1) * rayScale * 0.35 - r.y * 1.5 + morph * 4.0);
    curtain *= lerp(1.0, 0.3 + rays, rayStrength);

    //Two clocks summed, the desktop's envelope
    float pulse = 1.0 - pulseDepth * (0.5 + 0.5 * sin(SkyTime * 0.4)) * (0.7 + 0.3 * sin(SkyTime * 0.124 + 1.7));

    float within = saturate((elevation - 0.1) / max(bandHeight, 0.01));
    float3 glow = lerp(SkyColorA, SkyColorB, within) * (intensity * band * curtain * pulse);

    float stars = StarLayer(dir, 44.0, 0.07, 0.7, 0.085) + StarLayer(dir, 90.0, 0.18, 0.3, 0.075);

    float3 colour = SkyVoid + glow + stars * SkyColorC;
    return float4(ToDisplay(colour), 1);
}

float4 DreamPS(FigureOutput input) : COLOR0
{
    float3 dir = normalize(input.Direction);
    float swirl = SkyParams.x, warp = SkyParams.y, slow = SkyParams.z, fast = SkyParams.w;
    float sharpness = SkyParams2.x, brightness = SkyParams2.y, orbBrightness = SkyParams2.z, orbWidth = SkyParams2.w;

    //The marbling: fields of sines on the direction, the direction itself bent by a slower field first, which is
    //what turns bands into marble; slow enough to read over minutes
    float t = SkyTime * slow;
    float3 bent = dir + warp * 0.3 * float3(sin(dir.z * 3.1 + t * 1.3), sin(dir.x * 2.7 - t), sin(dir.y * 3.7 + t * 0.7));
    float field = sin(bent.x * swirl * 3.1 + t * 2.0) + sin(bent.y * swirl * 4.3 - t * 1.4) + sin(bent.z * swirl * 2.6 + t * 1.1)
        + 0.5 * sin((bent.x + bent.z) * swirl * 7.1 - t * 2.3);
    float u = 0.5 + 0.18 * field;

    //Inigo Quilez's cosine palette, the desktop's own
    float3 palette = SkyColorA + SkyColorB * cos(6.28318 * (SkyColorC * u + SkyColorD));

    //The ribbons: a sharp fine layer travelling fast across the slow marble
    float ribbon = 0.5 + 0.5 * sin(dot(bent, float3(9.1, 7.3, 5.7)) * swirl + SkyTime * fast);
    ribbon = pow(ribbon, sharpness);

    float3 colour = SkyVoid + palette * (brightness * (0.6 + 0.5 * ribbon));

    //Three soft orbs breathing in the murk, each in a colour of the palette, wheeling slowly round the vertical
    float sinWheel, cosWheel;
    sincos(SkyTime * 0.03, sinWheel, cosWheel);
    float3 o0 = normalize(float3(0.6 * cosWheel - 0.3 * sinWheel, 0.45, 0.6 * sinWheel + 0.3 * cosWheel));
    float3 o1 = normalize(float3(-0.7 * cosWheel + 0.2 * sinWheel, 0.25, -0.7 * sinWheel - 0.2 * cosWheel));
    float3 o2 = normalize(float3(0.2 * cosWheel + 0.8 * sinWheel, 0.6, 0.2 * sinWheel - 0.8 * cosWheel));
    float breathe0 = 0.75 + 0.25 * sin(SkyTime * 0.37), breathe1 = 0.75 + 0.25 * sin(SkyTime * 0.29 + 2.0), breathe2 = 0.75 + 0.25 * sin(SkyTime * 0.23 + 4.0);
    float3 orbs = (SkyColorA + SkyColorB * cos(6.28318 * (SkyColorC * 0.15 + SkyColorD))) * (exp((dot(dir, o0) - 1.0) * orbWidth) * breathe0)
        + (SkyColorA + SkyColorB * cos(6.28318 * (SkyColorC * 0.55 + SkyColorD))) * (exp((dot(dir, o1) - 1.0) * orbWidth) * breathe1)
        + (SkyColorA + SkyColorB * cos(6.28318 * (SkyColorC * 0.85 + SkyColorD))) * (exp((dot(dir, o2) - 1.0) * orbWidth) * breathe2);
    colour += orbs * orbBrightness;

    return float4(ToDisplay(colour), 1);
}

technique PotatoSkySpace
{
    pass P0
    {
        VertexShader = compile POTATO_VS FigureVS();
        PixelShader = compile POTATO_PS SpacePS();
    }
};

technique PotatoSkyAurora
{
    pass P0
    {
        VertexShader = compile POTATO_VS FigureVS();
        PixelShader = compile POTATO_PS AuroraPS();
    }
};

technique PotatoSkyDream
{
    pass P0
    {
        VertexShader = compile POTATO_VS FigureVS();
        PixelShader = compile POTATO_PS DreamPS();
    }
};
