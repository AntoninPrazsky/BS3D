//===================================================================================================
//THE COLOUR CUE (#737), THE PROGRAMS. One technique per ball style - each style's body (PatternShade, BubbleShade,
//MarbleShade, WoolShade, MetalShade, IceShade, GemShade, PlasmaShade, LavaShade, PorcelainShade) with a colour's
//figure PAINTED on, from the figures in BallColourCue.fxh. Every style has one, and InstancedModelRenderer refuses
//to load an effect that lacks one, because the cue is on by default and a style without its program would draw
//its balls in colour alone without anything failing.
//
//LAST IN THE EFFECT, and that is the point of a separate file: its uniforms land after everything else in the
//constant buffer, so no existing program's layout moves, and the shipped techniques - their switch passed as a
//literal false - compile to the code they had before the cue existed (they still draw where the cue is off:
//colourcue=none, the special kinds, the Potato path's own effects never reach here).
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

float4 BubbleMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return BubbleShade(input, true, ColourCueOfThisDraw());
}

float4 MetalMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return MetalShade(input, true, ColourCueOfThisDraw());
}

float4 IceMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return IceShade(input, true, ColourCueOfThisDraw());
}

float4 GemMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return GemShade(input, true, ColourCueOfThisDraw());
}

float4 PlasmaMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return PlasmaShade(input, true, ColourCueOfThisDraw());
}

float4 LavaMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return LavaShade(input, true, ColourCueOfThisDraw());
}

float4 PorcelainMotifPS(PatternVertexShaderOutput input) : COLOR
{
    return PorcelainShade(input, true, ColourCueOfThisDraw());
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

technique InstancedModelBubbleMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL BubbleMotifPS();
    }
};

technique InstancedModelMetalMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL MetalMotifPS();
    }
};

technique InstancedModelIceMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL IceMotifPS();
    }
};

technique InstancedModelGemMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL GemMotifPS();
    }
};

technique InstancedModelPlasmaMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PlasmaMotifPS();
    }
};

technique InstancedModelLavaMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL LavaMotifPS();
    }
};

technique InstancedModelPorcelainMotif
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PorcelainMotifPS();
    }
};
