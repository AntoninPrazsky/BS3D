//GamePi's SM 3.0 port of Prazsky.Shaders' LaserGrid.fx (#789): the floor alarm's grid of red beams, shown while the
//descending ceiling is close to pushing the cluster past the death line (LaserGrid.cs owns the beams), compiled for
//OpenGL through vs_3_0/ps_3_0 and MojoShader. Each beam is billboarded about its own axis exactly as ShotTrail.fx
//billboards its streak, and the port makes the same three changes for the same reasons:
//  - POSITION0 on the vertex output, because SM 3.0 has no SV_POSITION, and a pixel input without it, because a
//    ps_3_0 shader may not read POSITION.
//  - The billboard's two normalizing selects divide by a clamped length. vs_3_0 has no select instruction, so a
//    ternary can come out as a blend of both sides, and the unused side's 0/0 would put a NaN into the position.
//  - The pixel shader ends on the display curve (PotatoOutput.fxh), because Potato draws straight into the 8-bit back
//    buffer.
//The wave, the profile, the white filament and the uniforms LaserGrid.cs fetches are the desktop's, plus
//PotatoOutput's Exposure.
//
//Drawn with BlendState.Additive, depth-read and writing no depth. The colour is the ceiling alarm's red in linear
//radiance (CeilingDescent.CEILING_FLASH_COLOR). It was set that far over 1 so the desktop's glare would bloom it red;
//Potato has no glare, so here it only saturates the curve's red core.

#include "PotatoOutput.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 LaserColor;      //linear radiance, far over 1 (the ceiling alarm's red)
float LaserHalfWidth;   //half-thickness of a beam in world units; never changes, set once
float LaserIntensity;   //the whole grid's envelope x pulse, 0..1, computed on the CPU per frame
float Time;             //wall-clock seconds, for the wave below

//The wave: a shallow brightness ripple travelling along every beam, so the grid reads as energized rather than as
//painted lines. Shallow on purpose: the alarm is the PULSE, which arrives whole in LaserIntensity, and this only keeps
//the beams visibly alive between its peaks.
static const float WAVE_FREQUENCY = 1.7;    //radians per world unit along the beam
static const float WAVE_SPEED = 6.0;        //radians per second, so a crest runs a few units a second
static const float WAVE_DEPTH = 0.15;

//The white filament inside the red: lifting the channels the colour does NOT have is what "hot" looks like. Kept well
//under the red, because the play camera sees the grid near edge-on, the beams stack additively across the frame, and
//too much white bleached the whole net at every pulse peak. That value was judged on the desktop, where the beams are
//summed before the curve. Potato curves each beam and then sums them, so where they stack they whiten sooner here;
//not yet seen on the Pi.
static const float CORE_WHITE = 0.9;

struct LaserVertexInput
{
    float3 Start : POSITION0;   //one end of the beam's axis
    float3 End : TEXCOORD0;     //the other
    float2 Data : TEXCOORD1;    //(side in {-1,1}, along in {0,1})
};

struct LaserVertexOutput
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;          //(side, along)
    float AlongWorld : TEXCOORD1;   //distance along the beam in world units, the wave's coordinate
};

//What the pixel shader reads: the vertex output without its position
struct LaserPixelInput
{
    float2 UV : TEXCOORD0;
    float AlongWorld : TEXCOORD1;
};

LaserVertexOutput LaserVS(LaserVertexInput input)
{
    LaserVertexOutput output;

    float along = input.Data.y;
    float3 pos = lerp(input.Start, input.End, along);

    //The divisors are clamped even though each select already guards them (see the header)
    float3 axis = input.End - input.Start;
    float axisLen = length(axis);
    float3 dir = axisLen > 1e-4 ? axis / max(axisLen, 1e-4) : float3(1.0, 0.0, 0.0);

    //Billboard about the beam's axis. The play camera stands barely below the grid's plane, so a flat quad lying in
    //it would be seen edge-on and vanish; this way the beam keeps its thickness wherever the lens stands.
    float3 toCam = CameraPosition - pos;
    float3 side = cross(dir, toCam);
    float sideLen = length(side);
    side = sideLen > 1e-4 ? side / max(sideLen, 1e-4) : float3(0.0, 1.0, 0.0);

    pos += side * (input.Data.x * LaserHalfWidth);

    output.Position = mul(mul(float4(pos, 1.0), View), Projection);
    output.UV = float2(input.Data.x, along);
    output.AlongWorld = along * axisLen;

    return output;
}

float4 LaserPS(LaserPixelInput input) : COLOR
{
    float across = 1.0 - abs(input.UV.x);   //1 at the beam's core, 0 at its edges
    float profile = across * across;        //soft falloff, ShotTrail's own cross-section

    //Soft at both ends, so a beam dissolves at the grid's edge instead of stopping on a hard rectangle. Short
    //fades, so the beams still read as lasers and the grid's edge still comes out straight.
    float endFade = smoothstep(0.0, 0.06, input.UV.y) * smoothstep(1.0, 0.94, input.UV.y);

    float wave = 1.0 - WAVE_DEPTH + WAVE_DEPTH * sin(input.AlongWorld * WAVE_FREQUENCY - Time * WAVE_SPEED);

    //No clip. A clip on a value scaled by the pulsing intensity would sweep a hard edge in and out across every
    //beam with each pulse, the same trap ShotTrail.fx's streak avoids.
    float a = profile * endFade * LaserIntensity * wave;

    //The filament is the profile cubed, narrowed twice more, so the white stays a thread inside a red glow
    float core = profile * profile * profile;

    float3 color = LaserColor * a + CORE_WHITE * (core * endFade * LaserIntensity * wave);

    //THE OUTPUT. The desktop's Additive state (SourceAlpha/One) adds color * a, so that is the light that goes through
    //the curve, and alpha 1 makes the same state add it unchanged. ShotTrail.fx's port explains why the curve takes
    //the light this pixel adds and not the colour alone.
    return float4(ToDisplay(color * a), 1.0);
}

technique LaserGrid
{
    pass P0
    {
        VertexShader = compile vs_3_0 LaserVS();
        PixelShader = compile ps_3_0 LaserPS();
    }
};
