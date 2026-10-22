//GamePi's SM 3.0 port of Prazsky.Shaders' LaserGrid.fx (#789): the floor alarm's grid of red beams, shown while the
//descending ceiling is close to pushing the cluster past the death line, and the four soft cones that project it from
//the emitters' lenses in the funnel (#874; LaserGrid.cs owns the beams and the cones), compiled for
//OpenGL through vs_3_0/ps_3_0 and MojoShader. Each beam is a ribbon along its bowed path (LaserNetShape, #941), every row
//of it billboarded about the path's own direction exactly as ShotTrail.fx billboards its streak, and the port makes the
//same three changes for the same reasons:
//  - POSITION0 on the vertex output, because SM 3.0 has no SV_POSITION, and a pixel input without it, because a
//    ps_3_0 shader may not read POSITION.
//  - The billboard's normalizing select divides by a clamped length. vs_3_0 has no select instruction, so a
//    ternary can come out as a blend of both sides, and the unused side's 0/0 would put a NaN into the position. The
//    pixel's division by the half-width is clamped for the same reason.
//  - The pixel shader ends on the display curve (PotatoOutput.fxh), because Potato draws straight into the 8-bit back
//    buffer.
//The wave, the profile, the white filament, the per-vertex half-width, gain and fades, and the uniforms LaserGrid.cs
//fetches are the desktop's, plus PotatoOutput's Exposure.
//
//Drawn with BlendState.Additive, depth-read and writing no depth. The colour is the ceiling alarm's red in linear
//radiance (CeilingDescent.CEILING_FLASH_COLOR). It was set that far over 1 so the desktop's glare would bloom it red;
//Potato has no glare, so here it only saturates the curve's red core.

#include "PotatoOutput.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 LaserColor;      //linear radiance, far over 1 (the ceiling alarm's red)
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
    float3 Position : POSITION0;    //on the path of the beam or the cone's axis
    float3 Tangent : TEXCOORD0;     //unit, which way the path runs here: the axis the ribbon turns about
    float4 Data : TEXCOORD1;        //(side in {-1,1}, along the whole path in [0,1], half-width here, gain here)
    float3 Fade : TEXCOORD2;        //(fade-in, fade-out) as shares of the path: how soft each end is, and the distance along it in world units
};

struct LaserVertexOutput
{
    float4 Position : POSITION0;
    float4 Beam : TEXCOORD0;        //(side x half-width, along, half-width, gain)
    float3 Extra : TEXCOORD1;       //(distance along the path in world units - the wave's coordinate, fade-in, fade-out)
};

//What the pixel shader reads: the vertex output without its position
struct LaserPixelInput
{
    //Under DirectX the position the vertex output begins with (PotatoProfile.fxh, #808); nothing under OpenGL
    POTATO_PIXEL_HEAD
    float4 Beam : TEXCOORD0;
    float3 Extra : TEXCOORD1;
};

LaserVertexOutput LaserVS(LaserVertexInput input)
{
    LaserVertexOutput output;

    float along = input.Data.y;
    float3 pos = input.Position;

    //Billboard about the beam's path. The play camera stands barely below the grid's plane, so a flat ribbon lying in
    //it would be seen edge-on and vanish; this way the beam keeps its thickness wherever the lens stands, and a cone
    //is the same billboard widening along its axis. The divisor is clamped even though the select already guards it
    //(see the header); a bent beam's tangent is the chord of its two neighbours, so two quads agree on a row's edge.
    float3 toCam = CameraPosition - pos;
    float3 side = cross(input.Tangent, toCam);
    float sideLen = length(side);
    side = sideLen > 1e-4 ? side / max(sideLen, 1e-4) : float3(0.0, 1.0, 0.0);

    float halfWidth = input.Data.z;
    pos += side * (input.Data.x * halfWidth);

    output.Position = mul(mul(float4(pos, 1.0), View), Projection);

    //The across coordinate times the half-width, and the half-width: the pixel divides them, so a cone's trapezoid
    //shows no crease down its diagonal (the desktop's note)
    output.Beam = float4(input.Data.x * halfWidth, along, halfWidth, input.Data.w);
    output.Extra = float3(input.Fade.z, input.Fade.xy);

    return output;
}

float4 LaserPS(LaserPixelInput input) : COLOR
{
    float across = 1.0 - saturate(abs(input.Beam.x / max(input.Beam.z, 1e-4)));   //1 at the core, 0 at the edges
    float profile = across * across;        //soft falloff, ShotTrail's own cross-section

    //Soft at both ends: a beam's fades are short, so the grid's edge still comes out straight; a cone's far end is
    //long, so it dissolves onto the net it lands on.
    float along = input.Beam.y;
    float endFade = smoothstep(0.0, input.Extra.y, along) * smoothstep(1.0, 1.0 - input.Extra.z, along);

    float wave = 1.0 - WAVE_DEPTH + WAVE_DEPTH * sin(input.Extra.x * WAVE_FREQUENCY - Time * WAVE_SPEED);

    //1 along a beam; along a cone, the brighter the narrower (the desktop's note)
    float gain = input.Beam.w;

    //No clip. A clip on a value scaled by the pulsing intensity would sweep a hard edge in and out across every
    //beam with each pulse, the same trap ShotTrail.fx's streak avoids.
    float a = profile * endFade * LaserIntensity * wave * gain;

    //The filament is the profile cubed, narrowed twice more, so the white stays a thread inside a red glow; on a cone
    //it goes with the square of the gain, hot only at the lens
    float core = profile * profile * profile;

    float3 color = LaserColor * a + CORE_WHITE * (core * endFade * LaserIntensity * wave * gain * gain);

    //THE OUTPUT. The desktop's Additive state (SourceAlpha/One) adds color * a, so that is the light that goes through
    //the curve, and alpha 1 makes the same state add it unchanged. ShotTrail.fx's port explains why the curve takes
    //the light this pixel adds and not the colour alone.
    return float4(ToDisplay(color * a), 1.0);
}

technique LaserGrid
{
    pass P0
    {
        VertexShader = compile POTATO_VS LaserVS();
        PixelShader = compile POTATO_PS LaserPS();
    }
};
