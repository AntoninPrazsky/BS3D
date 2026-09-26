//The floor alarm: a grid of red laser beams hovering on the plane a ball's surface would touch at the
//moment the level is lost, shown while the descending ceiling is within two more steps of pushing the
//cluster past the death line (GameplayScreen.UpdateLaserWarning owns the trigger; LaserGrid.cs owns the
//beams). The C# side bakes one static quad per beam - both endpoints ride on every vertex, so nothing is
//rebuilt or re-uploaded per frame - and drives the whole grid with three per-frame uniforms: the camera,
//the pulsing intensity (computed on the CPU) and the wall clock for the shallow wave running along each
//beam. Every beam is billboarded about its own axis exactly as ShotTrail.fx billboards the launch smear,
//and here it is load-bearing rather than nice: the play camera stands barely below the grid's plane, so a
//flat quad lying in it would be seen edge-on and vanish. Drawn additively in linear radiance far over
//GLARE_THRESHOLD - a red needs a channel near 2.6 before its luminance clears 0.55, which is why the
//colour is the ceiling alarm's own (6, 0.15, 0.1) - so the beams bloom red through the glare; depth-read
//but writing no depth, so the island, the gun and any ball in front hide them. Game-only (nothing else
//has a descending ceiling), built by Game/Content/Content.mgcb alone, the Fireworks.fx precedent. SM 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 LaserColor;      //linear radiance, already far over the glare threshold (the ceiling alarm's red)
float LaserHalfWidth;   //half-thickness of a beam in world units; never changes, set once
float LaserIntensity;   //the whole grid's envelope x pulse, 0..1, computed on the CPU per frame
float Time;             //wall-clock seconds, for the wave below

//The wave: a shallow brightness ripple travelling along every beam, so the grid reads as energized
//rather than as painted lines. Shallow on purpose - the alarm is the PULSE, which arrives whole in
//LaserIntensity; this only keeps the beams visibly alive between its peaks.
static const float WAVE_FREQUENCY = 1.7;    //radians per world unit along the beam
static const float WAVE_SPEED = 6.0;        //radians per second, so a crest runs a few units a second
static const float WAVE_DEPTH = 0.15;

//The white filament inside the red: at this radiance the beam's own red channel is long saturated, and
//lifting the channels the colour does NOT have is what "hot" looks like - the cluster ripple's lesson.
//Kept well under the red, though: the play camera sees the grid near edge-on, so the beams stack
//additively across the frame, and at 2.5 the whole net bleached WHITE at every pulse peak - a red
//warning that turns white at exactly its loudest moment stops being a red warning (verified in
//screenshots; at 0.9 the peak blooms red-pink and the trough stays pure laser red).
static const float CORE_WHITE = 0.9;

struct LaserVertexInput
{
    float3 Start : POSITION0;   //one end of the beam's axis
    float3 End : TEXCOORD0;     //the other
    float2 Data : TEXCOORD1;    //(side in {-1,1}, along in {0,1})
};

struct LaserVertexOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;          //(side, along)
    float AlongWorld : TEXCOORD1;   //distance along the beam in world units, the wave's coordinate
};

LaserVertexOutput LaserVS(LaserVertexInput input)
{
    LaserVertexOutput output;

    float along = input.Data.y;
    float3 pos = lerp(input.Start, input.End, along);

    float3 axis = input.End - input.Start;
    float axisLen = length(axis);
    float3 dir = axisLen > 1e-4 ? axis / axisLen : float3(1.0, 0.0, 0.0);

    //Billboard about the beam's axis, ShotTrail's own trick: the width runs perpendicular to both the
    //axis and the view ray, so the beam holds its thickness wherever the lens stands.
    float3 toCam = CameraPosition - pos;
    float3 side = cross(dir, toCam);
    float sideLen = length(side);
    side = sideLen > 1e-4 ? side / sideLen : float3(0.0, 1.0, 0.0);

    pos += side * (input.Data.x * LaserHalfWidth);

    output.Position = mul(mul(float4(pos, 1.0), View), Projection);
    output.UV = float2(input.Data.x, along);
    output.AlongWorld = along * axisLen;

    return output;
}

float4 LaserPS(LaserVertexOutput input) : COLOR
{
    float across = 1.0 - abs(input.UV.x);   //1 at the beam's core, 0 at its edges
    float profile = across * across;        //soft falloff, ShotTrail's own cross-section

    //Soft at both ends, so a beam dissolves at the grid's edge instead of stopping on a hard rectangle.
    //Short fades: these are meant to read as lasers, and the grid's edge should still come out straight.
    float endFade = smoothstep(0.0, 0.06, input.UV.y) * smoothstep(1.0, 0.94, input.UV.y);

    float wave = 1.0 - WAVE_DEPTH + WAVE_DEPTH * sin(input.AlongWorld * WAVE_FREQUENCY - Time * WAVE_SPEED);

    //No clip. Additive blending makes a zero-alpha pixel free, and a clip on a value scaled by the
    //pulsing intensity would sweep a hard edge in and out across every beam twice a second - the trap
    //ShotTrail.fx documents, made periodic.
    float a = profile * endFade * LaserIntensity * wave;

    //The filament is the profile cubed - narrowed twice more, so the white stays a thread inside a red
    //glow instead of bleaching the whole beam.
    float core = profile * profile * profile;

    float3 color = LaserColor * a + CORE_WHITE * (core * endFade * LaserIntensity * wave);

    return float4(color, a);
}

technique LaserGrid
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL LaserVS();
        PixelShader = compile PS_SHADERMODEL LaserPS();
    }
};
