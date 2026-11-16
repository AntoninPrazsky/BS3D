//The noise hash, drawn once at load so the C# copy can round it the way this GPU does (#1134). Noise.fxh's NoiseHash22 takes
//the fraction of a product near 20 000, where a float carries a fraction to about 1/500, so whether its dot rounds once or
//twice per multiply-add moves it by whole quanta, and that is the driver's choice: the desktop's RDNA2 fuses, the laptop's
//GCN does not. Every terrain a CPU mirror places things on is built on this hash, so HashRoundingProbe draws it here for a
//grid of inputs, reads it back and sets ShaderMath.Hash22 to whichever rounding agrees. Making the hash precise instead
//would have spread back through every noise field's arithmetic (up to 1.2 ms at Low on the reference APU).
//
//One full-screen quad over a HASH_PROBE_SIZE square target: each pixel hashes its own input and hands back the hash and
//the input it used, so the C# side compares on exactly the numbers the GPU hashed.

#include "ShaderTarget.fxh"

#include "Noise.fxh"

//Must match HashRoundingProbe.SIZE
static const float HASH_PROBE_SIZE = 64.0;

struct HashProbeVertex
{
    float4 Position : SV_POSITION;
    float2 Cell : TEXCOORD0;
};

HashProbeVertex HashProbeVS(float4 position : POSITION0)
{
    HashProbeVertex output;
    output.Position = float4(position.xy, 0.0, 1.0);
    output.Cell = float2(position.x * 0.5 + 0.5, 0.5 - position.y * 0.5) * HASH_PROBE_SIZE;
    return output;
}

float4 HashProbePS(HashProbeVertex input) : COLOR
{
    //The pixel's column and row, whole numbers (the interpolated centre is half a unit inside each); then an input of the
    //kind the terrains hash, a whole cell plus a seed, over the range they reach. Every step is exact or one rounding, and
    //the input is handed back anyway.
    float2 cell = floor(input.Cell) - HASH_PROBE_SIZE * 0.5;
    float2 p = cell * float2(19.0, 23.0) + float2(23.7, 57.1);

    return float4(NoiseHash22(p), p);
}

technique HashProbe
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL HashProbeVS();
        PixelShader = compile PS_SHADERMODEL HashProbePS();
    }
}
