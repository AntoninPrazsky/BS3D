//The sea's school of fish (#760): every fish of the school in one static buffer, placed, turned and swum here off the
//clock and a hash of its index, so the school is one draw and no CPU work per fish (FishSchool). The mesh is one
//rest-pose fish repeated (FishMesh): +X forward, +Y up, one unit long, each vertex carrying (which fish, fin or body).
//
//THE SWIM. Each fish circles the ring round the funnel's cone at its own radius and height, at the school's speed, so
//the ring turns as one body and the fish keep their places in it the way a school does; its radius and height breathe
//slowly so the ring is never a rigid wheel. The body bends in a travelling wave that grows towards the tail, which is
//most of what makes a moving fish read as swimming rather than sliding.
//
//THE LOOK. A dark blue back over a silver belly (countershading, the concept sheets' and every pelagic fish's), lit from
//above as the water lights anything in it, with a flash where a silver flank turns the light from the surface back at
//the eye; and faded into the water with distance, because the underwater post (Tonemap.fx) has no depth.
//
//Drawn in the Game and the Testbed through the shared SceneRenderer. Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float Time;
float3 CameraPosition;

//The ring the school circles (FishSchool's figures)
float3 SchoolCentre;
float2 SchoolRadii;     //inner, outer
float SchoolDepth;
float SwimSpeed;
float TailHz;
float FishLength;

float3 BackColor;
float3 BellyColor;
float FlashStrength;
float FadeDistance;

//What reaches down into the water from the sun and the dome, and the colour a far fish fades into
float3 Light;
float3 WaterColor;

struct FishVertexInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 Data : TEXCOORD0;   //(fish index, 1 on a fin, 0, 0)
};

struct FishVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float3 World : TEXCOORD1;
    float2 Shade : TEXCOORD2;   //(how far up the fish's own side the facet faces, 1 on a fin)
};

//Four numbers in [0, 1) per fish, fixed for its life
float4 FishHash(float fish)
{
    float3 a = NoiseHash33(float3(fish * 7.31, fish * 1.73 + 3.1, 11.7)) * 0.5 + 0.5;
    float b = NoiseHash22(float2(fish * 3.37 + 5.3, 2.9)).x * 0.5 + 0.5;
    return float4(a, b);
}

FishVertexOutput FishVS(FishVertexInput input)
{
    FishVertexOutput output;

    float fish = input.Data.x;
    float4 h = FishHash(fish);

    //Its place in the ring: a radius and a height of its own, breathing slowly, and an angle round the ring that
    //advances at the school's speed - the same linear speed for every fish, so the inner ones lap the outer
    float radius = lerp(SchoolRadii.x, SchoolRadii.y, h.x) + 0.8 * sin(Time * 0.31 + fish * 1.7);
    float height = SchoolCentre.y + SchoolDepth * (h.y - 0.5) + 0.45 * sin(Time * 0.53 + fish * 2.3);
    float angle = h.z * 6.2831853 + Time * SwimSpeed / max(radius, 1.0) + 0.08 * sin(Time * 0.7 + fish);

    float3 place = SchoolCentre + float3(cos(angle) * radius, 0.0, sin(angle) * radius);
    place.y = height;

    //Heading along the ring, nosing up and down a little with its own bob
    float3 forward = normalize(float3(-sin(angle), 0.12 * cos(Time * 0.53 + fish * 2.3), cos(angle)));
    float3 up = float3(0.0, 1.0, 0.0);
    float3 across = normalize(cross(forward, up));
    up = cross(across, forward);

    //The swimming wave: a travelling bend that grows towards the tail, at the fish's own beat, and travelling FROM
    //THE HEAD TO THE TAIL (phase t + x, so a crest's x falls as time runs), the way a fish pushes the water back.
    //The first cut had t - x, a wave running up the body towards the nose, and the owner saw fish swimming tail
    //first (#760, 2026-10-05) although every one moves nose first along the ring.
    float3 local = input.Position;
    float tail = saturate((0.3 - local.x) / 1.08);
    float beat = TailHz * (0.85 + 0.3 * h.w);
    local.z += 0.09 * tail * tail * sin(Time * beat * 6.2831853 + fish * 2.1 + local.x * 8.0);

    float size = FishLength * (0.75 + 0.5 * h.w);
    float3 world = place + (forward * local.x + up * local.y + across * local.z) * size;

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Normal = forward * input.Normal.x + up * input.Normal.y + across * input.Normal.z;
    output.World = world;
    output.Shade = float2(input.Normal.y, input.Data.y);

    return output;
}

float4 FishPS(FishVertexOutput input) : COLOR
{
    float3 normal = normalize(input.Normal);

    //A fin is one sheet seen from either side
    if (input.Shade.y > 0.5 && dot(normal, CameraPosition - input.World) < 0.0) normal = -normal;

    //Countershading: the back dark, the belly silver, the flanks between; a fin takes the back's colour
    float side = input.Shade.y > 0.5 ? 1.0 : smoothstep(-0.35, 0.55, input.Shade.x);
    float3 albedo = lerp(BellyColor, BackColor, side);

    //Lit from above, the way the water lights everything in it, and never black
    float lit = 0.35 + 0.65 * saturate(normal.y * 0.5 + 0.5);

    //The flash: a silver flank mirrors the bright surface above back at the eye
    float3 toEye = normalize(CameraPosition - input.World);
    float3 mirrored = reflect(-toEye, normal);
    float flash = pow(saturate(mirrored.y), 6.0) * FlashStrength * (1.0 - side);

    float3 color = (albedo * lit + BellyColor * flash) * Light;

    //Into the water with distance
    float fade = exp(-length(CameraPosition - input.World) / max(FadeDistance, 1.0));
    color = lerp(WaterColor, color, fade);

    return float4(color, 1.0);
}

technique Fish
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FishVS();
        PixelShader = compile PS_SHADERMODEL FishPS();
    }
}
