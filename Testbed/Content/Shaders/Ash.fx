//Drifting ash over the volcano scene (#223): a boxful of specks around the camera, animated entirely in the
//vertex shader, wrapping with frac inside a box that follows the camera so the fall never ends and never
//pops at a boundary. The mountain's snowfall argument (Snow.fx), in grey - and a separate shader rather than
//that one retuned, because ash is not a crystal. It has no six arms to cut into its silhouette and no glint
//as it turns broadside, and a couple of specks in a hundred are still live EMBERS, which is the one thing
//falling snow must never do.
//
//Drawn alpha-blended into the HDR scene target before glare and tonemap - which is why the ash colour has to
//stay well under GLARE_THRESHOLD (ash that blooms is snow) while the ember colour is deliberately over it.
//Depth-read, writing no depth. Shader Model 5.0, drawn in all three executables.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;

float3 CameraPosition;
float3 CameraRight;
float3 CameraUp;

float AshTime;

float3 AshBoxSize;
float AshFallSpeed;
float2 AshWind;
float AshSway;
float SpeckSize;
float AshSpin;
float AshNearFade;

float EmberFraction;
float3 AshColor;
float3 EmberColor;
float AshOpacity;

struct AshVertexInput
{
    float4 Position : POSITION0; //Base position of the speck, a fixed random point in the unit cube
    float3 Data : TEXCOORD0;     //(corner x, corner y in {-1,1}, per-speck random)
};

struct AshVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Corner : TEXCOORD0;
    float3 Color : TEXCOORD1;
    float Alpha : TEXCOORD2;
};

AshVertexOutput AshVS(AshVertexInput input)
{
    AshVertexOutput output;

    float3 b = input.Position.xyz;
    float rand = input.Data.z;

    //Animate the base point within [0,1): it falls and drifts on the wind, wrapping with frac
    float fall = AshTime * AshFallSpeed / AshBoxSize.y;
    float2 drift = AshTime * AshWind / AshBoxSize.xz;

    float3 o;
    o.x = frac(b.x + drift.x);
    o.y = frac(b.y - fall);
    o.z = frac(b.z + drift.y);

    //Into a box centred on the camera. Ash is light enough to be pushed around on the way down, so it sways
    //on two rates rather than one - a single sine reads as every speck riding the same wave.
    float3 boxPosition = (o - 0.5) * AshBoxSize;
    boxPosition.x += sin(AshTime * 1.1 + rand * 40.0) * AshSway;
    boxPosition.z += sin(AshTime * 0.7 + rand * 27.0) * AshSway * 0.6;

    float3 center = CameraPosition + boxPosition;

    float size = SpeckSize * (0.5 + 1.0 * rand);

    //A flake of soot tumbles flat-on to edge-on as it falls, so its silhouette narrows and widens. Squashing
    //the quad on one axis is the whole effect and it costs one sine: ash never reads as round.
    float spin = AshTime * AshSpin * (0.6 + 0.8 * rand) + rand * 40.0;
    float2 turn;
    sincos(spin, turn.x, turn.y);
    float2 corner = float2(input.Data.x * turn.y - input.Data.y * turn.x,
                           input.Data.x * turn.x + input.Data.y * turn.y);
    corner.y *= 0.45 + 0.55 * abs(turn.y);

    float3 world = center + CameraRight * (corner.x * size) + CameraUp * (corner.y * size);

    //The box is centred on the camera, so this IS the distance from the lens; a speck right at it is a blur
    //in any real shot and drawing it crisply is what would put a grey coin over the frame (the snow's #85).
    float fade = smoothstep(AshNearFade * 0.25, AshNearFade, length(boxPosition));

    //The few specks that are still burning. Decorrelated from everything else the random drives, and they
    //keep their brightness all the way down: an ember that faded would just be pale ash.
    float ember = step(frac(rand * 13.37), EmberFraction);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Corner = input.Data.xy;
    output.Color = lerp(AshColor, EmberColor, ember);
    output.Alpha = fade * lerp(1.0, 1.6, ember);

    return output;
}

float4 AshPS(AshVertexOutput input) : COLOR
{
    float r = length(input.Corner);

    //A soft, torn speck rather than a disc: the mask's edge is pulled in and out around the rim so no two
    //specks share an outline, which is what a hundred identical circles would otherwise announce.
    float ragged = 0.82 + 0.18 * sin(input.Corner.x * 9.0 + input.Corner.y * 7.0);
    float mask = 1.0 - smoothstep(ragged * 0.3, ragged, r);

    float alpha = saturate(mask * input.Alpha * AshOpacity);
    clip(alpha - 0.004);

    //PREMULTIPLIED: MonoGame's BlendState.AlphaBlend is (One, InverseSourceAlpha), so returning a straight
    //colour adds it at full strength on every speck rather than covering what is behind. Ash has to be able
    //to DARKEN the sky it falls across - a speck that can only brighten is an ember, and this scene has
    //those on purpose and separately.
    return float4(input.Color * alpha, alpha);
}

technique Ash
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL AshVS();
        PixelShader = compile PS_SHADERMODEL AshPS();
    }
};
