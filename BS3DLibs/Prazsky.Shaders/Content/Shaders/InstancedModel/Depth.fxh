//InstancedModel.fx: the shadow-map caster (InstancedDepth).

//THE CASTER (#470). Depth-only pass for shadow mapping: renders the instances from the light's point of
//view, writing the map's own depth into the red channel of its Single-format render target.
//
//It was written long before anything called it and sat unused until #470 - the island, the gun and the
//city are what it was waiting for. The matrix is Shadows.fxh's own ShadowViewProjection rather than a
//LightViewProjection of its own: a caster and a receiver that disagreed about where the light stands would
//shadow the scene from two places, and one uniform cannot.
//
//THE DISSOLVE REACHES THE MAP (#920). A ball that is being dithered away - a released ball that has come to
//rest (dead weight, #620) - is drawn on screen as a thinning dither (BallCommon.fxh's contract, point 1), and
//until #920 this pass ignored the channel and cast a WHOLE, solid ball into the map, so a half-gone ball threw a
//full shadow. DepthVS now carries the instance's Dissolve and DepthPS cuts the same two-sided clip, so the shadow
//thins with the ball. Four things hold it together:
//
//  - THE CELL IS ONE TEXEL OF THE MAP, and there is no uniform for it. SV_POSITION in the pixel stage is the
//    bound target's own pixel, which here IS the map's texel, so floor(Position.xy) is the cell. The ball pass's
//    DissolvePixelSize cannot be used and is not read: it is a measure in DISPLAY pixels that BallRenderSet.Draw
//    sets for the scene target AFTER this pass has run, so in here the uniform holds the previous frame's value.
//    A texel is also the right size: Shadows.fxh's receiver takes a 3 x 3 box over the map, which averages a
//    texel-scale dither into a proportionally lighter shadow instead of a stipple. And no new uniform means the
//    effect's constant buffer is laid out as it was (the rule in InstancedModel.fx).
//  - THE TWO SIGNS ARE EXACT COMPLEMENTS here as on screen, so a colour crossing keeps its WHOLE shadow. A crossing
//    (the magazine's re-colour, a thaw, a lock-in, an infection tick) draws one ball twice at one place, at +d and
//    at -d, and the two dithers partition its pixels. They hash the same texel with the same DissolveNoise, so in
//    the map the two instances together cover every texel once: the ball changes colour, it does not thin, and a
//    full shadow is right. Only a ball with ONE side of the pair - dead weight, which is a lone +d - thins.
//  - ZERO IS NEVER CLIPPED, which is what leaves everything else as it was. DissolveNoise is a frac(), so in
//    [0, 1) and `noise - 0` is never negative: the island, the gun, the city, the scatter and every other
//    ModelInstance that does not dissolve (all of them send 0) write the depth they always wrote.
//  - A GHOST (a value below -1, #794) keeps every pixel (`-d - noise` is positive there), as on screen. Its SIZE is
//    a vertex-side scale in PatternVS that this vertex shader does not apply, so a ghost cast into the map would
//    cast a whole-size ball; the aim ghost is collected after this pass and casts nothing (#894), which is why that
//    is a note and not a term. The discard writes nothing and the caster blend is a Min (SceneRenderer.ShadowCasterBlend),
//    so a clipped texel leaves whatever another caster put there.

struct DepthVertexShaderOutput
{
    float4 Position : SV_POSITION;
    float Depth : TEXCOORD0;
    //Flat across the instance, like PatternVS's: interpolating a constant is free and a zero stays exactly zero
    float Dissolve : TEXCOORD1;
};

DepthVertexShaderOutput DepthVS(VertexShaderInput input, InstanceInput instance)
{
    DepthVertexShaderOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
    float4 worldPosition = mul(mul(input.Position, Bone), world);

    output.Position = mul(worldPosition, ShadowViewProjection);
    output.Depth = output.Position.z / output.Position.w;
    output.Dissolve = instance.Dissolve;

    return output;
}

float4 DepthPS(DepthVertexShaderOutput input) : COLOR
{
    //The ball techniques' clip, character for character (BallCommon.fxh), over cells of one map texel. Branchless for
    //their reason: Dissolve varies per instance, so a branch on it would diverge inside one draw call.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

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
