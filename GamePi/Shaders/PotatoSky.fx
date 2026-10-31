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
//Model 5.0. Here the smooth part of each - the Milky Way's band and the nebula, the curtains, the marble and the orbs -
//is computed PER VERTEX on a finer sphere of the host's (PotatoSkyFigure, 48 rings by 96 segments) and interpolated,
//exactly as the gradient above is: a field of sines over a few degrees per vertex is the same picture to the eye,
//and the pixel costs one move. The first cut did it per pixel and cost 12 ms at 1080p. The stars, which are points,
//are a batch of small screen-aligned quads the host builds once (PotatoSkyStars), added over the sphere.

float SkyTime;
float3 SkyVoid;         //the colour behind everything, linear (the scene's void or its deep)
float3 SkyColorA;       //space: the Milky Way's colour; the aurora: its low colour; the dream: palette A
float3 SkyColorB;       //space: the nebula's colour; the aurora: its high colour; the dream: palette B
float3 SkyColorC;       //the dream: palette C
float3 SkyColorD;       //space: the Milky Way's core; the dream: palette D
float3 SkyAxis;         //space: the band's normal
float3 SkyAxis2;        //space: where the nebula stands
float4 SkyParams;       //the aurora: intensity, band height, (unused), pulse depth; the dream: swirl scale, warp, slow speed, fast speed
float4 SkyParams2;      //the aurora: drift speed, morph speed, curtain scale, (unused); the dream: ribbon sharpness, brightness, orb brightness, orb width
float2 StarSize;        //half the width and height of a full-brightness star, in clip units

struct FigureOutput
{
    float4 Position : POSITION0;
    float3 Color : TEXCOORD0;
};

float3 SpaceSky(float3 dir)
{
    //The Milky Way: a band across the sky about its own axis, brighter and warmer at its core, with the star clouds
    //as a slow mottle of sines along it; the nebula a soft patch where SkyAxis2 points
    float across = dot(dir, SkyAxis);
    float band = exp(-across * across * 38.0);
    float clouds = 0.55 + 0.45 * sin(dir.x * 7.3 + dir.y * 5.1) * sin(dir.z * 6.7 - dir.x * 3.9);
    float3 way = lerp(SkyColorA, SkyColorD, band * band) * (band * clouds);
    float towards = saturate(dot(dir, SkyAxis2));
    float nebula = pow(towards, 60.0) * (0.7 + 0.3 * sin(dir.y * 11.0 + dir.z * 9.0));
    return SkyVoid + way + nebula * SkyColorB;
}

float3 AuroraSky(float3 dir)
{
    float intensity = SkyParams.x, bandHeight = SkyParams.y, pulseDepth = SkyParams.w;
    float driftSpeed = SkyParams2.x, morphSpeed = SkyParams2.y, curtainScale = SkyParams2.z;

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

    //Two clocks summed, the desktop's envelope
    float pulse = 1.0 - pulseDepth * (0.5 + 0.5 * sin(SkyTime * 0.4)) * (0.7 + 0.3 * sin(SkyTime * 0.124 + 1.7));

    float within = saturate((elevation - 0.1) / max(bandHeight, 0.01));
    float3 glow = lerp(SkyColorA, SkyColorB, within) * (intensity * band * curtain * pulse);
    return SkyVoid + glow;
}

float3 DreamPalette(float u)
{
    //Inigo Quilez's cosine palette, the desktop's own
    return SkyColorA + SkyColorB * cos(6.28318 * (SkyColorC * u + SkyColorD));
}

float3 DreamSky(float3 dir)
{
    float swirl = SkyParams.x, warp = SkyParams.y, slow = SkyParams.z, fast = SkyParams.w;
    float sharpness = SkyParams2.x, brightness = SkyParams2.y, orbBrightness = SkyParams2.z, orbWidth = SkyParams2.w;

    //The marbling: fields of sines on the direction, the direction itself bent by a slower field first, which is
    //what turns bands into marble; slow enough to read over minutes, and a fraction of the palette's cycle across
    //the field (the whole cycle was a rainbow)
    float t = SkyTime * slow;
    float3 bent = dir + warp * 0.3 * float3(sin(dir.z * 3.1 + t * 1.3), sin(dir.x * 2.7 - t), sin(dir.y * 3.7 + t * 0.7));
    float field = sin(bent.x * swirl * 3.1 + t * 2.0) + sin(bent.y * swirl * 4.3 - t * 1.4) + sin(bent.z * swirl * 2.6 + t * 1.1)
        + 0.5 * sin((bent.x + bent.z) * swirl * 7.1 - t * 2.3);
    //The orange, red, magenta and violet of the desktop's marble: the palette's cycle round one, a fraction either way;
    //round a half it was teal and blue
    float3 palette = DreamPalette(1.0 + 0.09 * field);

    //The ribbons: a sharper fine layer travelling fast across the slow marble
    float ribbon = 0.5 + 0.5 * sin(dot(bent, float3(9.1, 7.3, 5.7)) * swirl + SkyTime * fast);
    ribbon = pow(ribbon, sharpness);

    float3 colour = SkyVoid + palette * (brightness * (0.6 + 0.35 * ribbon));

    //Three soft orbs breathing in the murk, each in a colour of the palette, wheeling slowly round the vertical
    float sinWheel, cosWheel;
    sincos(SkyTime * 0.03, sinWheel, cosWheel);
    float3 o0 = normalize(float3(0.6 * cosWheel - 0.3 * sinWheel, 0.45, 0.6 * sinWheel + 0.3 * cosWheel));
    float3 o1 = normalize(float3(-0.7 * cosWheel + 0.2 * sinWheel, 0.25, -0.7 * sinWheel - 0.2 * cosWheel));
    float3 o2 = normalize(float3(0.2 * cosWheel + 0.8 * sinWheel, 0.6, 0.2 * sinWheel - 0.8 * cosWheel));
    float breathe0 = 0.75 + 0.25 * sin(SkyTime * 0.37), breathe1 = 0.75 + 0.25 * sin(SkyTime * 0.29 + 2.0), breathe2 = 0.75 + 0.25 * sin(SkyTime * 0.23 + 4.0);
    float3 orbs = DreamPalette(0.05) * (exp((dot(dir, o0) - 1.0) * orbWidth) * breathe0)
        + DreamPalette(0.22) * (exp((dot(dir, o1) - 1.0) * orbWidth) * breathe1)
        + DreamPalette(0.85) * (exp((dot(dir, o2) - 1.0) * orbWidth) * breathe2);
    return colour + orbs * orbBrightness;
}

FigureOutput SpaceVS(VertexInput input)
{
    FigureOutput output;
    output.Position = mul(mul(mul(input.Position, World), View), Projection);
    output.Color = ToDisplay(SpaceSky(normalize(input.Position.xyz)));
    return output;
}

FigureOutput AuroraVS(VertexInput input)
{
    FigureOutput output;
    output.Position = mul(mul(mul(input.Position, World), View), Projection);
    output.Color = ToDisplay(AuroraSky(normalize(input.Position.xyz)));
    return output;
}

FigureOutput DreamVS(VertexInput input)
{
    FigureOutput output;
    output.Position = mul(mul(mul(input.Position, World), View), Projection);
    output.Color = ToDisplay(DreamSky(normalize(input.Position.xyz)));
    return output;
}

float4 FigurePS(FigureOutput input) : COLOR0
{
    return float4(input.Color, 1);
}

//The stars: four corners a star, the direction in the position, the tint and the brightness in the colour, the
//corner in the texture coordinate. Each is laid out on the screen as a square of StarSize round its direction,
//grown a little with its brightness, and shaded as a soft dot; added over the sphere, so the light is display-encoded
//already and the brightness is tuned to that.
struct StarInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 Corner : TEXCOORD0;
};

struct StarOutput
{
    float4 Position : POSITION0;
    float4 Color : TEXCOORD0;
    float2 Corner : TEXCOORD1;
};

StarOutput StarVS(StarInput input)
{
    StarOutput output;
    float4 clip = mul(mul(mul(float4(input.Position.xyz, 1), World), View), Projection);
    float size = 0.55 + 0.9 * input.Color.a;
    clip.xy += input.Corner * StarSize * (size * clip.w);
    output.Position = clip;
    output.Color = input.Color;
    output.Corner = input.Corner;
    return output;
}

float4 StarPS(StarOutput input) : COLOR0
{
    float r2 = dot(input.Corner, input.Corner);
    float a = saturate(1.0 - r2);
    a *= a;
    return float4(input.Color.rgb * (input.Color.a * a), 1);
}

technique PotatoSkySpace
{
    pass P0
    {
        VertexShader = compile POTATO_VS SpaceVS();
        PixelShader = compile POTATO_PS FigurePS();
    }
};

technique PotatoSkyAurora
{
    pass P0
    {
        VertexShader = compile POTATO_VS AuroraVS();
        PixelShader = compile POTATO_PS FigurePS();
    }
};

technique PotatoSkyDream
{
    pass P0
    {
        VertexShader = compile POTATO_VS DreamVS();
        PixelShader = compile POTATO_PS FigurePS();
    }
};

technique PotatoSkyStars
{
    pass P0
    {
        VertexShader = compile POTATO_VS StarVS();
        PixelShader = compile POTATO_PS StarPS();
    }
};

//--- THE CLOUD DECK (#1044) ------------------------------------------------------------------------------------------
//The desktop's weather over the seven scenes under a dome, which had their dome's gradient alone here. The same
//CloudField drives it, and its uniforms are the desktop sky's names, so CloudField.ApplyTo, ApplyDome and
//ApplyStaticParameters hand them over as they do to Sky.fx. Sky.fx computes gradient noise per pixel, octave by octave,
//and per-pixel noise cost the Pi 12 ms at 1080p (#829's skies). Here the field is baked into a tiling texture once
//(PotatoCloudDeck.BakeField: the weather layer in red, finer octaves in green), and the pixel reads it twice.
//
//The mesh is a disc on the plane one unit over the eye, drawn round the camera as the dome is. A vertex's position is
//the direction to its point on the deck, so the deck's world position is the camera's plus that direction scaled to
//the deck's height. The triangles are flat, so the texture's coordinates are interpolated exactly. The fade towards the
//horizon, the sun's silver and the two colours change slowly across the sky, so they are the vertex's.

float3 CameraPosition;
float3 SunDirection;
float CloudPlaneY;
float CloudScale;
float CloudTime;
float2 CloudWind;
float CloudCoverageBias;
float CloudCoverageGain;
float CloudDetailStrength;
float CloudOpacity;
float CloudHorizonFade;
float CloudSelfAbsorption;
float CloudSilverStrength;
float CloudSilverPower;
float3 CloudSunColor;
float3 CloudShadowColor;

texture CloudTexture;
sampler2D CloudSampler = sampler_state
{
    Texture = <CloudTexture>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
};

//PotatoCloudDeck.TILE_FEATURES, the weather layer's features across one tile; and how much finer the second read is,
//not a whole number, so its tile never lines up with the first
#define CLOUD_TILE_FEATURES 4.0
#define CLOUD_DETAIL_SCALE 2.37
//How far the finer octaves carry an edge in or out, in the weather layer's own units, at a detail strength of one. The
//desktop erodes with four more octaves than this; its strengths (1.4 to 3.2) scale this the same way
#define CLOUD_DETAIL_REACH 0.06

struct DeckOutput
{
    float4 Position : POSITION0;
    float4 Uv : TEXCOORD0;      //the weather's read, then the finer one
    float3 Sun : TEXCOORD1;     //the lit side, display-encoded, the silver in it
    float3 Shade : TEXCOORD2;   //the underside, display-encoded
    float Fade : TEXCOORD3;     //none at the horizon
};

DeckOutput DeckVS(VertexInput input)
{
    DeckOutput output;
    output.Position = mul(mul(mul(input.Position, World), View), Projection);

    //The point of the deck this vertex stands for: the camera's place plus the direction scaled to the deck's height.
    //Under a deck the camera has risen through (a tour's high pass), the height stays a little over it
    float3 toward = input.Position.xyz;
    float height = max(CloudPlaneY - CameraPosition.y, 10.0);
    float2 hit = CameraPosition.xz + toward.xz * height;

    //The weather drifts with the wind, the finer octaves a quarter faster (Clouds.fxh's second octave), so the edges
    //keep changing as the forms cross the sky
    float2 drift = CloudWind * CloudTime;
    output.Uv.xy = (hit + drift) * (CloudScale / CLOUD_TILE_FEATURES);
    output.Uv.zw = (hit + drift * 1.25) * (CloudScale * CLOUD_DETAIL_SCALE / CLOUD_TILE_FEATURES) + 0.37;

    float3 direction = normalize(toward);
    output.Fade = smoothstep(0.0, CloudHorizonFade, direction.y);

    //Towards the sun a thin edge is bright: Sky.fx's forward scattering, weighted below by the light that got through
    float silver = pow(saturate(dot(direction, SunDirection)), CloudSilverPower) * CloudSilverStrength;
    output.Sun = ToDisplay(CloudSunColor * (1.0 + silver));
    output.Shade = ToDisplay(CloudShadowColor);

    return output;
}

float4 DeckPS(DeckOutput input) : COLOR0
{
    //Stored as half the value plus a half
    float weather = tex2D(CloudSampler, input.Uv.xy).r * 2.0 - 1.0;
    float detail = tex2D(CloudSampler, input.Uv.zw).g * 2.0 - 1.0;

    //Sky.fx's thickness (unclamped, for the shading) and coverage (clamped, for the compositing): the finer octaves
    //move the threshold where the weather layer drew it, which is what chews the edges
    float thickness = (weather + CloudCoverageBias + detail * CLOUD_DETAIL_REACH * CloudDetailStrength) * CloudCoverageGain;
    float density = saturate(thickness);

    //A flat field seen from under it: the thick parts are the undersides with the light used up, the thin parts let it
    //through - dark cores and bright edges, Sky.fx's Beer-Lambert on the body alone
    float sunlight = exp(-max(thickness, 0.0) * CloudSelfAbsorption);
    float3 colour = lerp(input.Shade, input.Sun, sunlight);

    return float4(colour, saturate(density * CloudOpacity) * input.Fade);
}

technique PotatoCloudDeck
{
    pass P0
    {
        VertexShader = compile POTATO_VS DeckVS();
        PixelShader = compile POTATO_PS DeckPS();
    }
};
