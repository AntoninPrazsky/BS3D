//The visible flame of the savanna's campfire: one upright billboard at the fire's position, the pixel
//shader a procedural flickering flame - a tapering tongue, hot yellow-white at the core to orange at the
//edges, wobbling on the wall clock. Drawn additively and bright (radiance well over 1), so it glows and
//blooms through the glare pass the way a real flame throws light. The illumination it casts on the ground,
//the balls and the island is a separate scene point light (see AddSceneLights); this is only the source you
//see. Depth-read (the terrain or platform in front hides it) but writes no depth. Testbed-shared, SM 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 FlamePosition; //Base of the flame on the ground
float FlameSize;

//The billboard's height, as a multiple of FlameSize. A uniform rather than the 2.4 that used to be written
//here, because height and width have to move independently: the game's camera sits about level with the
//island's stone while the fires stand on grass some five units below it, so a flame has to be tall to be
//seen at all - and a flame made tall by growing FlameSize is a bonfire.
float FlameHeightScale;
float FlameTime;

//Per-fire rate stretch, 1 for the first fire and a few per cent more for each one after it. The caller
//already offsets FlameTime per fire; on its own that would leave every flame licking the IDENTICAL pattern
//a moment apart, which the eye reads as one wave travelling round the island as soon as two are in shot.
//Stretching the rates as well means no two are ever running the same shape, however long they burn.
float FlameSeed;

struct FlameVertexInput
{
    float4 Position : POSITION0; //ignored; the flame is one billboard at FlamePosition
    float3 Data : TEXCOORD0;     //(corner u in {-1,1}, corner v in {0,1}, unused)
};

struct FlameVertexOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};

FlameVertexOutput FlameVS(FlameVertexInput input)
{
    FlameVertexOutput output;

    float3 toCam = CameraPosition - FlamePosition;
    toCam.y = 0.0;
    float3 right = normalize(cross(float3(0.0, 1.0, 0.0), normalize(toCam)));
    float3 up = float3(0.0, 1.0, 0.0);

    float w = FlameSize;
    float h = FlameSize * FlameHeightScale;
    float3 world = FlamePosition + right * (input.Data.x * w) + up * (input.Data.y * h);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.UV = input.Data.xy;

    return output;
}

float4 FlamePS(FlameVertexOutput input) : COLOR
{
    float u = input.UV.x; //[-1,1]
    float v = input.UV.y; //[0,1], 0 at the base

    //A tongue that narrows upward, its centre wobbling on the wall clock so it licks and flickers. Every
    //rate is stretched by FlameSeed, so each fire of the ring has its own gait rather than the ring beating
    //together; the vertical frequencies are stretched too, so the tongues differ in SHAPE and not only in
    //timing - two flames the same height with the same number of kinks in them read as copies however far
    //apart their phases are.
    float r = FlameSeed;
    float wob = 0.18 * sin(v * 6.0 * r - FlameTime * 9.0 * r) + 0.10 * sin(v * 11.0 * r + FlameTime * 13.0 * r);
    float width = (1.0 - v) * (0.85 + 0.15 * sin(FlameTime * 7.0 * r));

    float d = abs(u - wob) / max(width, 1e-3);
    float body = saturate(1.0 - d);
    body *= smoothstep(1.0, 0.65, v);   //taper to nothing at the tip
    body *= smoothstep(-0.03, 0.12, v); //and rise from the base

    clip(body - 0.02);

    //Hot amber core to deeper orange edges; radiance over 1 so it blooms through the glare, but kept from
    //blowing straight to white (the blue stays low) so it reads as fire, not a white spark
    float3 core = float3(2.0, 1.15, 0.35);
    float3 edge = float3(1.5, 0.34, 0.04);
    float3 color = lerp(edge, core, body * body);

    //Premultiplied by the body so additive blending fades it out towards the edges
    return float4(color * body, body);
}

technique Flame
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FlameVS();
        PixelShader = compile PS_SHADERMODEL FlamePS();
    }
};
