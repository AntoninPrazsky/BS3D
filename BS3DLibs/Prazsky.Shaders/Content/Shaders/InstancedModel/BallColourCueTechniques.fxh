//===================================================================================================
//THE COLOUR CUE (#737), THE PROGRAMS. Three techniques - the vinyl, the wool and the marble with a colour's figure
//PAINTED on - over the bodies those three styles already have (PatternShade, WoolShade, MarbleShade) and the
//figures in BallColourCue.fxh. Switched on only by a caller that asks (the Testbed's and the Game's colourcue=
//argument) and never by anything a player can reach: the owner picks one of the figure sets first.
//
//LAST IN THE EFFECT, and that is the point of a separate file: its uniforms land after everything else in the
//constant buffer, so no existing program's layout moves, and the shipped techniques - their switch passed as a
//literal false - compile to the code they had before the cue existed.
//===================================================================================================

//This draw's figure and its two inks, per draw like PatternPrimaryColor, because a bucket is one colour.
//InstancedModelRenderer sets them only when it selects one of the programs below; what each component means is
//ColourCuePaint's comment in BallColourCue.fxh. The frame's upper three rows are the figure's axes in object space.
float4 ColourCueA;
float4 ColourCueB;
float4 ColourCueC;
float4x4 ColourCueFrame;
float3 ColourCueFigureInk;
float3 ColourCueGroundInk;

ColourCuePaint ColourCueOfThisDraw()
{
    ColourCuePaint cue;
    cue.a = ColourCueA;
    cue.b = ColourCueB;
    cue.c = ColourCueC;
    cue.frame = (float3x3)ColourCueFrame;
    cue.figureInk = ColourCueFigureInk;
    cue.groundInk = ColourCueGroundInk;

    return cue;
}

float4 PatternMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return PatternShade(input, true, ColourCueOfThisDraw());
}

float4 WoolMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return WoolShade(input, true, ColourCueOfThisDraw());
}

float4 MarbleMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return MarbleShade(input, true, ColourCueOfThisDraw());
}

technique InstancedModelPatternMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PatternMotifPS();
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

technique InstancedModelMarbleMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL MarbleMotifPS();
    }
};
