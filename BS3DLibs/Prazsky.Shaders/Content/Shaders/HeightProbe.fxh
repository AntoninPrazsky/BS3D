//The terrain height probe (#590): one technique, HeightProbe, that a terrain shader gains by defining what its
//height is and including this file LAST - after its own techniques, so Techniques[0] stays the scene's.
//
//It exists for the Testbed's `mirrorcheck` and nothing else. The ground is placed on the CPU by
//TerrainMirror - the trees, the rocks, the lamps and the chapter-intro lenses all stand on a C# copy of the
//field each shader displaces its grid by - and until this file nothing compared the two: "a drift here plants
//trees underground or floating, and there is nothing to catch it but the eye". The probe draws one quad into
//a small float target, each pixel evaluating the shader's own height function at the world XZ the quad
//carries, and hands back that XZ beside the height so the CPU evaluates its mirror at exactly the point the GPU
//did. No frame selects it, so it costs a shipping frame nothing but the few instructions of bytecode.
//
//What the including shader defines, before the include:
//  HEIGHT_PROBE_MIRRORED(p) - the height the CPU mirror claims to copy, at world XZ p. The verdict is on this.
//  HEIGHT_PROBE_DRAWN(p)    - optional: the height the vertex shader actually displaces by, where the mirror
//                             leaves a term out by design (the volcano's scoria). Defaults to the mirrored one.
//
//Out: float4(x, mirrored height, z, drawn height) - so the target is SurfaceFormat.Vector4.

#ifndef HEIGHT_PROBE_DRAWN
#define HEIGHT_PROBE_DRAWN(p) HEIGHT_PROBE_MIRRORED(p)
#endif

struct HeightProbeVertex
{
    float4 Position : SV_POSITION;
    float2 WorldXZ : TEXCOORD0;
};

//The quad comes in already in clip space, carrying its corners' world XZ as the texture coordinate, so the
//rasteriser's interpolation IS the sample grid and there is no camera to get wrong.
HeightProbeVertex HeightProbeVS(float4 position : POSITION0, float2 worldXZ : TEXCOORD0)
{
    HeightProbeVertex output;
    output.Position = position;
    output.WorldXZ = worldXZ;

    return output;
}

float4 HeightProbePS(HeightProbeVertex input) : COLOR
{
    float2 p = input.WorldXZ;

    return float4(p.x, HEIGHT_PROBE_MIRRORED(p), p.y, HEIGHT_PROBE_DRAWN(p));
}

technique HeightProbe
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL HeightProbeVS();
        PixelShader = compile PS_SHADERMODEL HeightProbePS();
    }
}
