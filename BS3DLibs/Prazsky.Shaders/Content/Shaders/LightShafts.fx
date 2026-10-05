//The sun's light coming down through the sea's surface, seen from under the water (#760): long soft shafts hanging from
//the surface along the refracted sun, the thing every underwater photograph the references drew is lit by
//(C:\Users\panrd\AI\sd\out\760-zimage and 760-klein: rays fanning down round a floating platform's dark underside).
//
//A static buffer of quads, one per shaft, each vertex carrying (which shaft, across -1..1, along 0..1); where a shaft
//stands, how wide and how long it is and how it shimmers are hashed off its index here, and each quad is turned about
//its own axis to face the lens. Additive, depth-read and never depth-write: light adds to what is behind it and hides
//nothing, and the island in front of a shaft occludes it. Drawn only with the lens under the water (SeaLightShafts).
//
//Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float Time;
float3 CameraPosition;

//The surface they hang from, the direction they run (the sun's, bent down by the water) and their figures
float SurfaceY;
float3 ShaftDirection;
float2 ShaftRadii;      //the annulus round the island's axis they stand in: inner, outer
float ShaftLength;
float2 ShaftWidths;     //narrowest, widest
float3 ShaftColor;
float NearFade;         //how close to the lens a shaft fades out, so a shaft through the lens is never a wall

struct ShaftVertexInput
{
    float3 Corner : POSITION0;   //(shaft index, across -1..1, along 0..1)
};

struct ShaftVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Shape : TEXCOORD0;    //(across, along, brightness)
    float3 World : TEXCOORD1;
};

ShaftVertexOutput ShaftVS(ShaftVertexInput input)
{
    ShaftVertexOutput output;

    float shaft = input.Corner.x;
    float across = input.Corner.y;
    float along = input.Corner.z;

    float3 h = NoiseHash33(float3(shaft * 5.17, shaft * 2.31 + 7.7, 3.9)) * 0.5 + 0.5;

    //Where it meets the surface: in the annulus, most of them near its inner edge, where a lens under the island
    //sees them cross the frame rather than stand along the far horizon, drifting slowly with the swell above
    float radius = lerp(ShaftRadii.x, ShaftRadii.y, h.x * h.x);
    float bearing = h.y * 6.2831853 + 0.02 * sin(Time * 0.11 + shaft);
    float3 top = float3(cos(bearing) * radius, SurfaceY, sin(bearing) * radius);

    float3 axisPoint = top + ShaftDirection * (ShaftLength * (0.6 + 0.4 * h.z) * along);

    //Turned about its own axis to face the lens
    float3 toEye = CameraPosition - axisPoint;
    float3 side = normalize(cross(ShaftDirection, toEye) + float3(1e-4, 0.0, 0.0));
    float width = lerp(ShaftWidths.x, ShaftWidths.y, h.z) * (1.0 + 0.6 * along);

    float3 world = axisPoint + side * (across * width);

    //A shimmer of its own: the surface's waves focus and spread the light it carries
    float shimmer = 0.55 + 0.45 * sin(Time * (0.6 + 0.8 * h.x) + shaft * 3.7);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Shape = float3(across, along, shimmer);
    output.World = world;

    return output;
}

float4 ShaftPS(ShaftVertexOutput input) : COLOR
{
    //Soft across, bright where it leaves the surface and gone well before its end
    float across = 1.0 - abs(input.Shape.x);
    across *= across;
    float along = smoothstep(0.0, 0.06, input.Shape.y) * pow(saturate(1.0 - input.Shape.y), 1.6);

    //Gone near the lens, per pixel: a shaft is a quad with corners only at its two ends, 42 to 70 apart, so a fade
    //worked out per vertex never fired with the lens beside a shaft's middle (the review of #760)
    float near = saturate((length(CameraPosition - input.World) - NearFade) / NearFade);

    return float4(ShaftColor * (across * along * input.Shape.z * near), 1.0);
}

technique LightShafts
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ShaftVS();
        PixelShader = compile PS_SHADERMODEL ShaftPS();
    }
}
