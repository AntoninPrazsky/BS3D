//InstancedModel.fx: the textured scene surfaces - the island's stone and concrete, the forest's bark and rock -
//through the world-axis projection (InstancedModelTriplanar), its coarse copy for the quality tiers below
//High, and #151's capprobe= measurement probes.

//Detail texturing: a texture that only modulates the existing material colors
//(DetailStrength 0 = untextured look), projected along the world axes so it needs no UVs
//(InstancedModelTriplanar, e.g. the arena's stone island). It is the only way a material texture reaches
//this effect: the procedural meshes carry no UVs, and the UV-mapped paths that once sat here
//(InstancedModelTextured, InstancedModelDetailUV/DetailUVNormal and the normal map) were deleted
//in #581 with the loaded-Model constructor that was the only thing able to reach them.

//World units per texture tile = 1 / DetailScale.
float DetailScale;
//How strongly the detail texture modulates the material color (0 = not at all, 1 = fully)
float DetailStrength;
//Brightness compensation so a mid-gray detail texture does not darken the whole material
float DetailBoost;

//Headroom the cavity term carries above the relief's own amplitude, in world units. It was the depth the
//castle's mortar joints were sunk to, and it is kept at that exact figure because the cavity range is the
//one place the construction patterns reached that survives them: every surface in the game has been shaded
//through this number since the joints themselves stopped being drawn, so rounding it away would change the
//look of all of them for nothing.
static const float CavityHeadroom = 0.055;

float4 TriplanarPS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);

    //Sharpened normal weights avoid visible cross-fading except near 45-degree edges
    float3 blend = pow(abs(worldNormal), 4);
    blend /= blend.x + blend.y + blend.z;

    float3 p = input.WorldPosition * DetailScale;

    //Each tap is linearized before the blend: the three projections are averaged, and averaging
    //display-encoded values is exactly the mistake this whole pass exists to stop making
    float3 detail
        = SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
        + SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
        + SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

    float3 dpdx = ddx(input.WorldPosition);
    float3 dpdy = ddy(input.WorldPosition);

    float groove;
    float height = SceneSurfaceHeightGroove(input.WorldPosition, dpdx, dpdy, groove);

    float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

    //The dust (#535): on the top, by the geometric normal, and only there - and blown clear round the axis (#538)
    //where a ring asks for it. And the rime (#534): on the sides.
    float up = saturate(worldNormal.y);
    float clear = smoothstep(TopDustClear.x, TopDustClear.x + max(TopDustClear.y, 1e-3), length(input.WorldPosition.xz));
    texRgb = lerp(texRgb, texRgb * TopDustTint, TopDustStrength * up * up * clear);
    float side = 1 - abs(worldNormal.y);
    texRgb = lerp(texRgb, texRgb * SideDustTint, SideDustStrength * side * side);

    //The tide line and the sand crust, and the bedding planes (#536)
    float wet = ApplyHeightBands(texRgb, input.WorldPosition, side, abs(dpdx.y) + abs(dpdy.y));

    float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

    //Cavity shading needs only the height and applies cleanly, so this path runs it instead of the generic
    //relief marches — which would be reading a different surface than the one drawn here.
    float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
    float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

    //A wet band mirrors more of the sky (#536); everywhere else this is the default surface, to the bit
    SurfaceSpecular surface = DefaultSurfaceSpecular();
    surface.Environment += wet;

    float4 shaded = ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity, surface);

    //The joints' glow (#535), behind a branch on the uniform: the groove came out of the height field above
    //(one evaluation, #534), and nothing inside the branch is a gradient operation.
    [branch]
    if (dot(JointGlow, JointGlow) > 0)
    {
        //IN PATCHES, not along every joint: lit along their whole length the joints photographed as a neon
        //grid laid over the stone (the first cut), where a cooling crack glows where the crust is thinnest
        //and a vein runs in some fractures and not others. A low world-space noise gates the glow, so a
        //stretch of a joint burns, fades and goes dark along the line, and about a third of the grid is lit.
        float patch = smoothstep(0.05, 0.45, GradientNoise3(input.WorldPosition * 0.23 + 3.7));
        shaded.rgb += JointGlow * (groove * groove * lerp(1.0, patch, JointGlowPatchiness));
    }

    return shaded;
}

technique InstancedModelTriplanar
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarPS();
    }
};

//The same surface with a COARSE height field: three relief octaves instead of seven. What a quality tier
//below High draws on the stone cap, and nothing else - see InstancedModelRenderer.CoarseSurfaceRelief and
//ArenaIsland.SurfaceDetail. It is a second TECHNIQUE and not a branch inside TriplanarPS for the reason
//the bubble's own header states and #155 measured: a runtime branch costs the union of both register
//allocations in every wavefront of an occupancy-bound pass.
//
//WHY THIS ONE AND NOT ONE OF THE OTHER CUTS (#151, measured on the reference desktop, Testbed, meadow,
//dome 13, play camera, windowed 1920x1080 at ssaa 4, four interleaved rounds):
//  - The shipped pass is 10.971 ms a frame and shading the cap as a CONSTANT is 9.481, so the cap's whole
//    pixel shader is 1.490 ms - 13.6 % of the frame, on a surface that is in every scene.
//  - Of that, the height field and the normal it tilts are 0.660 and this cut is 0.336. The three
//    triplanar taps are 0.029 (one tap instead of three) to 0.137 (no taps at all), i.e. nothing: the
//    third suspect this issue has named and measured at zero. The remaining ~0.83 ms is ShadePixel, which
//    every surface in the game is lit through and which this is not the issue to cut.
//  - Dropping the height field ENTIRELY saves twice as much and cannot ship: SlabGroove is part of the
//    same field, so the cap loses its coursed slab joints with it - the only structure the stone has at a
//    scale the eye can see. Three octaves keep the joints and give up the finest grain.
float4 TriplanarCoarsePS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);

    float3 blend = pow(abs(worldNormal), 4);
    blend /= blend.x + blend.y + blend.z;

    float3 p = input.WorldPosition * DetailScale;

    float3 detail
        = SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
        + SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
        + SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

    float3 dpdx = ddx(input.WorldPosition);
    float3 dpdy = ddy(input.WorldPosition);

    float groove;
    float height = SceneSurfaceHeightCoarseGroove(input.WorldPosition, dpdx, dpdy, groove);

    float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

    //The dust (#535): on the top, by the geometric normal, and only there - and blown clear round the axis (#538)
    //where a ring asks for it. And the rime (#534): on the sides.
    float up = saturate(worldNormal.y);
    float clear = smoothstep(TopDustClear.x, TopDustClear.x + max(TopDustClear.y, 1e-3), length(input.WorldPosition.xz));
    texRgb = lerp(texRgb, texRgb * TopDustTint, TopDustStrength * up * up * clear);
    float side = 1 - abs(worldNormal.y);
    texRgb = lerp(texRgb, texRgb * SideDustTint, SideDustStrength * side * side);

    //The tide line and the sand crust, and the bedding planes (#536)
    float wet = ApplyHeightBands(texRgb, input.WorldPosition, side, abs(dpdx.y) + abs(dpdy.y));

    float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

    float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
    float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

    //A wet band mirrors more of the sky (#536); everywhere else this is the default surface, to the bit
    SurfaceSpecular surface = DefaultSurfaceSpecular();
    surface.Environment += wet;

    float4 shaded = ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity, surface);

    //The joints' glow (#535), behind a branch on the uniform: the groove came out of the height field above
    //(one evaluation, #534), and nothing inside the branch is a gradient operation.
    [branch]
    if (dot(JointGlow, JointGlow) > 0)
    {
        //IN PATCHES, not along every joint: lit along their whole length the joints photographed as a neon
        //grid laid over the stone (the first cut), where a cooling crack glows where the crust is thinnest
        //and a vein runs in some fractures and not others. A low world-space noise gates the glow, so a
        //stretch of a joint burns, fades and goes dark along the line, and about a third of the grid is lit.
        float patch = smoothstep(0.05, 0.45, GradientNoise3(input.WorldPosition * 0.23 + 3.7));
        shaded.rgb += JointGlow * (groove * groove * lerp(1.0, patch, JointGlowPatchiness));
    }

    return shaded;
}

technique InstancedModelTriplanarCoarse
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarCoarsePS();
    }
};

//===================================================================================================
//#151 - THE STONE CAP'S MEASUREMENT PROBES.
//
//Five cut-down copies of TriplanarPS, each with one named suspect taken out, chosen per draw by
//InstancedModelRenderer.TriplanarProbe (Testbed: capprobe=N; capprobe=3 is the coarse technique above,
//which is why the numbering skips it). They exist so that "where does the stone cap's per-pixel time
//go" is ONE build measured several ways rather than several builds measured against each other: the
//previous round of this issue needed six interleaved pairs of whole builds before a 0.26 ms effect was
//legible at all, because an hour into hammering the GPU a session drifts further than the effect does.
//One build cannot drift between its own variants.
//
//KEPT rather than deleted with the answer, for the reason ArenaIsland.Members and the Testbed's arena=
//were kept: the ratio #151 was opened on - 27 ms of a 42 ms frame - is the WEAK machine's and has still
//not been re-derived there, and these are what will do it in a few minutes instead of a rebuild. They
//cost a shipped frame nothing: each technique is its own program and nothing selects a probe unless the
//command line asks for it.
//===================================================================================================

//1 - no height field at all: the three taps and ShadePixel on the geometric normal, cavity off.
//Bounds SceneSurfaceHeight and PerturbNormalFromHeight together.
float4 TriplanarProbe1PS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);

    float3 blend = pow(abs(worldNormal), 4);
    blend /= blend.x + blend.y + blend.z;

    float3 p = input.WorldPosition * DetailScale;

    float3 detail
        = SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
        + SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
        + SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

    float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

    return ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(texRgb, 1), 1, 1);
}

//2 - the height field, but not the normal it tilts: seven octaves and the joints are still evaluated
//and still shade the cavity, only PerturbNormalFromHeight is gone. Probe 1 against this is the cost
//of the field; this against the shipped technique is the cost of the perturb's ddx/ddy pair.
float4 TriplanarProbe2PS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);

    float3 blend = pow(abs(worldNormal), 4);
    blend /= blend.x + blend.y + blend.z;

    float3 p = input.WorldPosition * DetailScale;

    float3 detail
        = SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
        + SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
        + SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

    float3 dpdx = ddx(input.WorldPosition);
    float3 dpdy = ddy(input.WorldPosition);

    float height = SceneSurfaceHeight(input.WorldPosition, dpdx, dpdy);

    float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

    float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
    float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

    return ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity);
}

//4 - one texture tap instead of three, the world-XZ projection alone. Everything else is the shipped
//pass, so the difference is the two samples and the blend that fed them.
float4 TriplanarProbe4PS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);

    float3 p = input.WorldPosition * DetailScale;

    float3 detail = SrgbToLinear(tex2D(TextureSampler, p.xz).rgb);

    float3 dpdx = ddx(input.WorldPosition);
    float3 dpdy = ddy(input.WorldPosition);

    float height = SceneSurfaceHeight(input.WorldPosition, dpdx, dpdy);

    float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);
    float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

    float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
    float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

    return ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity);
}

//5 - no detail texture at all: the full height field, the perturb and ShadePixel on the flat material
//colour. Probe 4 and this one bracket the three taps from both sides.
float4 TriplanarProbe5PS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);

    float3 dpdx = ddx(input.WorldPosition);
    float3 dpdy = ddy(input.WorldPosition);

    float height = SceneSurfaceHeight(input.WorldPosition, dpdx, dpdy);

    float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

    float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
    float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

    return ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(1, 1, 1, 1), 1, cavity);
}

//6 - a constant. The bound on the whole pixel shader, and the only figure here that says how much of
//the cap is shading at all rather than raster, depth and the vertex work behind it.
float4 TriplanarProbe6PS(VertexShaderOutput input) : COLOR
{
    return float4(0.2, 0.2, 0.2, 1);
}

technique InstancedModelTriplanarProbe1
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarProbe1PS();
    }
};

technique InstancedModelTriplanarProbe2
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarProbe2PS();
    }
};

technique InstancedModelTriplanarProbe4
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarProbe4PS();
    }
};

technique InstancedModelTriplanarProbe5
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarProbe5PS();
    }
};

technique InstancedModelTriplanarProbe6
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarProbe6PS();
    }
};
