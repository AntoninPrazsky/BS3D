//Falling snow over the mountain scene: a boxful of flakes around the camera, drifting down on the wind.
//The whole flake set lives in a static vertex buffer - one quad per flake, its base position a fixed
//random point in a unit cube - and the vertex shader animates it: the flake falls and drifts, wrapping
//within a box that follows the camera, so the snowfall is endless and always around you. Drawn in the
//mountain and the aurora scenes, alpha-blended into the HDR scene target (before glare and tonemap — which is
//why the flake colour has to mind GLARE_THRESHOLD), depth-read.
//
//The box follows the camera rather than being pinned to the world, which trades a little translational
//parallax for never popping as the camera crosses a box boundary - the right trade for a uniform veil of
//small flakes. Drawn in every executable through the shared SceneRenderer, Shader Model 5.0.
//
//WHAT A FLAKE IS, since #654, and it is what a camera sees rather than what a snowflake is. Every reference of
//real snowfall (#654, both local models) drew the same three things and none of them was a crystal: most flakes
//are tiny sharp dots, the few near the lens are big, flat, faint discs - out of focus - and in between they
//are soft specks of every size, a little drawn out along the way they fall. The six-armed crystal #85 drew
//instead read as a sprinkle of snowflake ICONS. #85's worry stands and is answered differently: a feathered
//round speck reads as a ball when it is OPAQUE and CRISP at a ball's size, and here a flake near enough to be
//that size is defocused into a translucent disc whose alpha falls with the square of its growth - the light a
//flake sends is spread over the disc it is blurred into, never added to.
//
//Two layers from one buffer: the near box, and the same flakes drawn again in a box SnowLayerScale times
//larger (Snowfall.Draw), whose flakes perspective alone makes tiny - the distant veil a heavy snowfall lays
//over the range. And a flake is never drawn with a radius under one pixel: past that it is kept at one and its
//alpha takes the difference, so a far flake is a faint dot rather than one that shimmers in and out as it
//crosses pixel centres. One pixel and not less, because the mask is read at pixel centres only: at 0.6 a flake
//centred on a pixel's corner reached no centre at all, and its coverage swung from nothing to full as it fell.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;

//The camera, and its right/up in world space for billboarding the flakes towards it
float3 CameraPosition;
float3 CameraRight;
float3 CameraUp;

float SnowTime;

//The volume the flakes fill around the camera (times this layer's scale), how fast they fall, the wind that
//drifts them sideways, how far a flake sways as it falls, and the flake size in world units
float3 SnowBoxSize;
float SnowLayerScale;
float SnowFallSpeed;
float2 SnowWind;
float SnowSway;
float FlakeSize;

//The lens: a flake nearer than SnowNearFade fades out (at a quarter of it, it is gone), one nearer than
//SnowFocus is blurred by up to SnowAperture world units, and one moving is drawn out over SnowShutter seconds.
//SnowPixel is one pixel's world size at unit distance (Snowfall.Draw, off the projection and the viewport).
float SnowNearFade;
float SnowFocus;
float SnowAperture;
float SnowShutter;
float SnowPixel;

float3 SnowColor;
float SnowOpacity;

struct SnowVertexInput
{
    float4 Position : POSITION0; //Base position of the flake, a fixed random point in the unit cube
    float3 Data : TEXCOORD0;     //(corner x, corner y in {-1,1}, per-flake random)
};

struct SnowVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Corner : TEXCOORD0; //(along the streak, across it, in units of the flake's drawn radius; stretch)
    float2 Look : TEXCOORD1;   //(alpha, how defocused 0..1)
};

SnowVertexOutput SnowVS(SnowVertexInput input)
{
    SnowVertexOutput output;

    float3 b = input.Position.xyz;
    float rand = input.Data.z;
    float3 box = SnowBoxSize * SnowLayerScale;

    //Animate the base point within [0,1): it falls (y decreases) and drifts on the wind, wrapping with frac
    float fall = SnowTime * SnowFallSpeed / box.y;
    float2 drift = SnowTime * SnowWind / box.xz;

    float3 o;
    o.x = frac(b.x + drift.x);
    o.y = frac(b.y - fall);
    o.z = frac(b.z + drift.y);

    //Into a box centred on the camera, with a gentle per-flake sway
    float swayPhase = SnowTime * 1.3 + rand * 40.0;
    float3 boxPosition = (o - 0.5) * box;
    boxPosition.x += sin(swayPhase) * SnowSway;

    float3 center = CameraPosition + boxPosition;
    float distance = length(boxPosition);

    //Mostly small, a few large: a cube of the random puts five flakes in eight under the mean, which is the
    //spread every reference drew. Decorrelated from the sway's phase.
    float sizeRandom = frac(rand * 7.31);
    float size = FlakeSize * (0.4 + 1.6 * sizeRandom * sizeRandom * sizeRandom);

    //Out of focus nearer than the focus, by up to the aperture at the lens; the disc it is spread over grows
    //in quadrature with the flake, and its alpha falls with the area it is spread over
    float blur = SnowAperture * saturate((SnowFocus - distance) / SnowFocus);
    float radius = sqrt(size * size + blur * blur);
    float alpha = (size * size) / (radius * radius);

    //Never under a pixel's radius: the flake is kept at one and its alpha pays for the difference
    float pixel = distance * SnowPixel;
    float drawn = max(radius, pixel);
    alpha *= (radius * radius) / (drawn * drawn);

    //Drawn out along the way it moves, on screen: the fall, the wind and the sway's own speed over the
    //shutter. The streak's light is spread along it too, so its alpha falls with the length it adds.
    float3 velocity = float3(SnowWind.x + cos(swayPhase) * 1.3 * SnowSway, -SnowFallSpeed, SnowWind.y);
    float2 onScreen = float2(dot(velocity, CameraRight), dot(velocity, CameraUp));
    float speed = length(onScreen);
    float2 along = speed > 1e-4 ? onScreen / speed : float2(0.0, 1.0);
    float streak = speed * SnowShutter;
    float halfLength = drawn + 0.5 * streak;
    alpha /= 1.0 + 0.64 * streak / drawn;

    float3 alongWorld = CameraRight * along.x + CameraUp * along.y;
    float3 acrossWorld = CameraRight * -along.y + CameraUp * along.x;
    float3 world = center + alongWorld * (input.Data.x * halfLength) + acrossWorld * (input.Data.y * drawn);

    //A flake at the lens is gone rather than a blur over half the frame, and one at the box's faces comes and
    //goes by degrees: a wrap is a flake leaving one face and entering the opposite one, which a hard edge
    //shows as a dot popping into being a box's half-width away
    alpha *= smoothstep(SnowNearFade * 0.25, SnowNearFade, distance);
    float3 face = abs(o - 0.5) * 2.0;
    alpha *= 1.0 - smoothstep(0.8, 1.0, max(face.x, max(face.y, face.z)));

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Corner = float3(input.Data.x * halfLength / drawn, input.Data.y, halfLength / drawn);
    output.Look = float2(alpha, 1.0 - size / radius);

    return output;
}

float4 SnowPS(SnowVertexOutput input) : COLOR
{
    //A capsule one drawn radius wide: the round speck, with the streak's straight run in its middle
    float stretch = input.Corner.z;
    float run = max(abs(input.Corner.x) - (stretch - 1.0), 0.0);
    float r = length(float2(run, input.Corner.y));

    //A sharp flake is a small solid speck with a soft rim; a defocused one is a flatter disc with a wider
    //soft rim, which is what a lens makes of a point of light
    float inner = lerp(0.3, 0.7, input.Look.y);
    float mask = 1.0 - smoothstep(inner, 1.0, r);

    float alpha = saturate(mask * input.Look.x * SnowOpacity);
    clip(alpha - 0.002);

    //PREMULTIPLIED, because BlendState.AlphaBlend is (One, InverseSourceAlpha). Until #654 this returned the
    //colour straight, so a flake added its FULL colour wherever it drew and alpha only darkened what lay behind
    //it: every flake was a solid white shape with a hard edge at the clip, whatever its opacity or feather said
    //- the "white coin" #85 blamed on the tonemap crushing the rim, and the crisp crystal icons after it. The
    //sea's spray and the storm's clouds share the fault (#675).
    return float4(SnowColor * alpha, alpha);
}

technique Snow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SnowVS();
        PixelShader = compile PS_SHADERMODEL SnowPS();
    }
};
