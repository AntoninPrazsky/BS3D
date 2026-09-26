//InstancedModel.fx: the shadow-map caster (InstancedDepth).

//THE CASTER (#470). Depth-only pass for shadow mapping: renders the instances from the light's point of
//view, writing the map's own depth into the red channel of its Single-format render target.
//
//It was written long before anything called it and sat unused until #470 - the island, the gun and the
//city are what it was waiting for. The matrix is Shadows.fxh's own ShadowViewProjection rather than a
//LightViewProjection of its own: a caster and a receiver that disagreed about where the light stands would
//shadow the scene from two places, and one uniform cannot.

struct DepthVertexShaderOutput
{
    float4 Position : SV_POSITION;
    float Depth : TEXCOORD0;
};

DepthVertexShaderOutput DepthVS(VertexShaderInput input, InstanceInput instance)
{
    DepthVertexShaderOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
    float4 worldPosition = mul(mul(input.Position, Bone), world);

    output.Position = mul(worldPosition, ShadowViewProjection);
    output.Depth = output.Position.z / output.Position.w;

    return output;
}

float4 DepthPS(DepthVertexShaderOutput input) : COLOR
{
    return float4(input.Depth, 0, 0, 1);
}

technique InstancedDepth
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL DepthVS();
        PixelShader = compile PS_SHADERMODEL DepthPS();
    }
};
