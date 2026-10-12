//===================================================================================================
//THE COLOUR CUE PROTOTYPES (#737), THE PROGRAMS. Six techniques - the vinyl, the wool and the marble, each with
//its colour cue PAINTED (Motif) or CARVED (Shape) - over the bodies those three styles already have
//(PatternShade, WoolShade, MarbleShade) and the figures in BallColourCue.fxh. They are prototypes, switched on
//only by a caller that asks (the Testbed's and the Game's colourcue= argument) and never by anything a player
//can reach: the owner chooses between the two readings first, and only then are all thirteen colours built.
//
//LAST IN THE EFFECT, and that is the point of a separate file: its one uniform lands after everything else in
//the constant buffer, so no existing program's layout moves, and the shipped techniques - their switches passed
//as literal falses - compile to the code they had before the prototypes existed.
//===================================================================================================

//Which figure this draw's colour carries, 1..4, the slots BallColourCue.fxh tabulates. Per draw, like
//PatternPrimaryColor, because a bucket is one colour. InstancedModelRenderer sets it only when it selects one of
//the programs below.
float ColourCue;

//PatternVS with the carve. The MESH is moved - the silhouette, and the depth a neighbour or the aim beam meets -
//while every interpolant stays the sphere's: the object position the figures and the gores are read at, the world
//position and the normal. So the pixel shader shades the carve as a height field on the sphere (ColourCueShape)
//and every pattern stays where it was on the ball; the radial move maps each mesh point to the sphere point it
//came from, which is all the interpolants need to agree with the raster.
PatternVertexShaderOutput ColourCueShapeVS(VertexShaderInput input, InstanceInput instance)
{
    PatternVertexShaderOutput output = PatternVS(input, instance);

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

    float4 bonePosition = mul(input.Position, Bone);
    float radius = max(length(bonePosition.xyz), 1e-5);
    float3 carved = bonePosition.xyz * (1 - ColourCueCarve(bonePosition.xyz / radius, ColourCue));

    //The ghost's own shrink (#794), the way PatternVS applies it, on the carved point
    float4 worldPosition = mul(float4(carved, 1), world);
    worldPosition.xyz += (worldPosition.xyz - instance.WorldRow4.xyz) * (GhostScale(instance.Dissolve) - 1.0);

    output.Position = mul(mul(worldPosition, View), Projection);

    return output;
}

float4 PatternMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return PatternShade(input, true, false, ColourCue);
}

float4 PatternShapePS(PatternVertexShaderOutput input) : COLOR
{
    return PatternShade(input, false, true, ColourCue);
}

float4 WoolMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return WoolShade(input, true, false, ColourCue);
}

float4 WoolShapePS(PatternVertexShaderOutput input) : COLOR
{
    return WoolShade(input, false, true, ColourCue);
}

float4 MarbleMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return MarbleShade(input, true, false, ColourCue);
}

float4 MarbleShapePS(PatternVertexShaderOutput input) : COLOR
{
    return MarbleShade(input, false, true, ColourCue);
}

technique InstancedModelPatternMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PatternMotifPS();
    }
};

technique InstancedModelPatternShape
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ColourCueShapeVS();
        PixelShader = compile PS_SHADERMODEL PatternShapePS();
    }
};

technique InstancedModelWoolMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL WoolMotifPS();
    }
};

technique InstancedModelWoolShape
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ColourCueShapeVS();
        PixelShader = compile PS_SHADERMODEL WoolShapePS();
    }
};

technique InstancedModelMarbleMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL MarbleMotifPS();
    }
};

technique InstancedModelMarbleShape
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ColourCueShapeVS();
        PixelShader = compile PS_SHADERMODEL MarbleShapePS();
    }
};
