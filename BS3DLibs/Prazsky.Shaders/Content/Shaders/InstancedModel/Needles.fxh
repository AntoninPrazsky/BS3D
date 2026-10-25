//InstancedModel.fx: High and Ultra's spruce boughs in needle (#751, InstancedModelTriplanarNeedles). The owner, of both
//woods on High and Ultra: "the conifers have no needles". A bough is a plate over an arm (SpruceCrownGeometry), and the
//references (#751's spruce branch, klein and Z-Image) draw what covers it: branchlets leaving the arm forward on both
//sides, each a tapered bottlebrush with a lighter midrib and needles leaning towards its end, dark gaps between them, and
//lighter new growth at the ends, the plate's outline a comb of branchlet tips and its underside a fringe of them hanging.
//
//DRAWN ON THE PLATE, NOT BESIDE IT. #962's first stage tried cards along the boughs through the plant material and they
//read as dark dots against the bright plates: two lighting models side by side. Here the needles are the plate's own
//pixels, through the very triplanar shading the boughs were lit by (TriplanarPS, which this copies but for the joints'
//glow, which no tree has): the strands' roundness goes into the height the normal and the cavity read, the gaps into the
//albedo, and the comb is cut with clip. So a needle is lit exactly as the bough under it.
//
//Where on the bough a pixel is comes from the mesh's texture coordinate (SpruceCrownGeometry.UNDERSIDE): x along the arm
//in world units, y across the plate (-1 to 1 on the top, the ridge 0; offset by UNDERSIDE underneath, the keel at it;
//PLAIN on the leader). Everything fades with the pixel's footprint on the branchlet bands, so a far crown is the solid
//plate it was and does not shimmer.

//SpruceCrownGeometry.UNDERSIDE, and the leader's mark (its PLAIN is 10): a y past this is the leader
static const float UNDERSIDE_MARK = 4.0;
static const float PLAIN_MARK = 8.0;

//The branchlets: how far apart along the arm (world units), how many spacings one runs forward while it crosses from the
//midrib to the edge, and a strand's half-width at its root and its end, as shares of its band
static const float BRANCHLET_SPACING = 0.16;
static const float BRANCHLET_SWEEP = 1.5;
static const float STRAND_ROOT = 0.44, STRAND_END = 0.32;
//How far a strand's middle strays from its band's, as a share of the band
static const float STRAND_STRAY = 0.22;

//The needles: how many along a strand from its root to its end, how far one leans towards the end as it leaves the midrib,
//and how much shorter than the strand's half-width the shortest is (each its own length, so the strand's sides are the
//ragged row of needle tips the references draw rather than a smooth edge)
static const float NEEDLES_PER_STRAND = 13.0;
static const float NEEDLE_LEAN = 1.6;
static const float NEEDLE_SHORTEST = 0.6;

//The fringe: how deep the comb is cut into the plate, as a share of the way from the midrib on the top, and from the side
//edge underneath, where the branchlets hang down to the keel
static const float FRINGE_DEPTH = 0.4, FRINGE_DEPTH_UNDER = 0.6;

//The shading: how dark a gap between strands is against a strand, how much lighter the midrib and a strand's new growth
//are, and the new growth's yellower green; how high a strand stands over its gap (world units)
static const float GAP_SHADE = 0.58;
static const float MIDRIB_LIFT = 0.12, TIP_LIFT = 0.14;
static const float3 NEW_GROWTH = float3(0.97, 1.08, 0.97);
static const float STRAND_RELIEF = 0.016;

//The footprint (band widths a pixel covers) past which the fine detail fades, and over how much
static const float DETAIL_FADE_START = 0.22, DETAIL_FADE_SPAN = 0.3;

struct NeedleVertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 Bough : TEXCOORD0;
};

struct NeedleVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
    float2 Bough : TEXCOORD3;
};

NeedleVertexOutput NeedleVS(NeedleVertexInput input, InstanceInput instance)
{
    NeedleVertexOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
    float4 worldPosition = mul(mul(input.Position, Bone), world);

    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(input.Normal, world);
    output.OcclusionData = instance.Custom;
    output.Bough = input.Bough;

    return output;
}

float NeedleHash(float n)
{
    return frac(sin(n * 12.9898 + 4.1414) * 43758.5453);
}

//Where a pixel is on its bough's strands, and the comb's cut there: one function for the depth pass that cuts the comb and the
//shading pass that draws on what it left, so the two cannot disagree about a strand
struct NeedleBands
{
    float Under, Outward, F, Rnd, Footprint, Fine, HalfWidth, NeedleRnd, NeedleFade, Strand, Coverage;
};

NeedleBands NeedleBandsAt(float2 bough)
{
    NeedleBands b;

    //Which surface, and how far outward a pixel is on its strands: from the midrib (0) to the plate's edge (1) on the top,
    //from the side edge the fringe hangs from (0) to the keel (1) underneath
    float plain = step(PLAIN_MARK, bough.y);
    b.Under = step(UNDERSIDE_MARK * 0.5, bough.y) * (1 - plain);
    float across = saturate(abs(bough.y - UNDERSIDE_MARK * b.Under));
    b.Outward = lerp(across, 1 - across, b.Under);

    //The bands the strands run in: forward along the arm as they go outward. Each has its own width, reach and shade
    float phase = bough.x / BRANCHLET_SPACING - b.Outward * BRANCHLET_SWEEP + 0.5 * b.Under;
    float band = floor(phase);
    b.F = frac(phase) - 0.5;
    b.Rnd = NeedleHash(band + 31.0 * b.Under);
    b.F -= STRAND_STRAY * (b.Rnd - 0.5);
    b.Footprint = fwidth(phase);
    b.Fine = saturate((DETAIL_FADE_START + DETAIL_FADE_SPAN - b.Footprint) / DETAIL_FADE_SPAN) * (1 - plain);
    b.HalfWidth = lerp(STRAND_ROOT, STRAND_END, b.Outward) * (0.85 + 0.3 * b.Rnd);

    //Along a strand, the needles: each leaves the midrib leaning towards the strand's end, at its own length and shade, so
    //the strand's sides are needle tips. Their own footprint fades them before the strands go
    float needlePhase = b.Outward * NEEDLES_PER_STRAND - abs(b.F) / max(b.HalfWidth, 1e-3) * NEEDLE_LEAN;
    float needleCell = floor(needlePhase);
    b.NeedleRnd = NeedleHash(needleCell * 3.17 + band * 7.71 + 11.0 * sign(b.F));
    b.NeedleFade = saturate((0.6 - fwidth(needlePhase)) / 0.3) * b.Fine;
    float edge = b.HalfWidth * lerp(1, lerp(NEEDLE_SHORTEST, 1.15, b.NeedleRnd), b.NeedleFade);
    b.Strand = 1 - smoothstep(edge - b.Footprint, edge + b.Footprint, abs(b.F));

    //The comb: past the fringe's depth a pixel is plate only inside a strand that reaches it, each ending at its own length;
    //all plate where the bands are too fine to cut
    float depth = lerp(FRINGE_DEPTH, FRINGE_DEPTH_UNDER, b.Under);
    float reach = 1 - depth * (0.2 + 0.8 * NeedleHash(band * 1.7 + 5.0 + 13.0 * b.Under));
    float outFootprint = fwidth(b.Outward) + 1e-4;
    float within = 1 - smoothstep(reach - outFootprint, reach + outFootprint, b.Outward);
    float fringe = smoothstep(1 - depth - 0.05, 1 - depth, b.Outward);
    b.Coverage = lerp(1, lerp(1, b.Strand * within, fringe), b.Fine);
    return b;
}

float4 TriplanarNeedlesPS(NeedleVertexOutput input) : COLOR
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
    float height = SceneSurfaceHeightGroove(input.WorldPosition, worldNormal, dpdx, dpdy, groove);

    float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

    //--- The needles: where the pixel is on its strands (the comb itself was cut by the depth pass, NeedleDepthPS)
    NeedleBands b = NeedleBandsAt(input.Bough);
    float under = b.Under, outward = b.Outward, f = b.F, rnd = b.Rnd, footprint = b.Footprint, fine = b.Fine;
    float halfWidth = b.HalfWidth, needleRnd = b.NeedleRnd, needleFade = b.NeedleFade, strand = b.Strand;

    float midrib = (1 - smoothstep(0.04, 0.04 + 2 * footprint, abs(f))) * fine;
    float growth = smoothstep(0.55, 1.0, outward) * strand * fine * (1 - under);

    float shade = lerp(1, lerp(GAP_SHADE, 1, strand), fine);
    shade *= lerp(1, 0.78 + 0.4 * needleRnd, strand * needleFade);
    shade *= 1 + MIDRIB_LIFT * midrib * strand + TIP_LIFT * growth;
    shade *= lerp(1, 0.88 + 0.24 * rnd, fine);
    texRgb *= shade * lerp(float3(1, 1, 1), NEW_GROWTH, growth);

    //A strand stands round over its gap: into the height the normal and the cavity read
    float roundness = sqrt(saturate(1 - (f / max(halfWidth, 1e-3)) * (f / max(halfWidth, 1e-3))));
    height += STRAND_RELIEF * strand * roundness * fine;

    //--- And the bough's shading as TriplanarPS lights it
    float up = saturate(worldNormal.y);
    float clear = smoothstep(TopDustClear.x, TopDustClear.x + max(TopDustClear.y, 1e-3), length(input.WorldPosition.xz));
    texRgb = lerp(texRgb, texRgb * TopDustTint, TopDustStrength * up * up * clear);
    float side = 1 - abs(worldNormal.y);
    texRgb = lerp(texRgb, texRgb * SideDustTint, SideDustStrength * side * side);

    float wet = ApplyHeightBands(texRgb, input.WorldPosition, side, abs(dpdx.y) + abs(dpdy.y));

    float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

    float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
    float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

    SurfaceSpecular surface = DefaultSurfaceSpecular();
    surface.Environment += wet;

    float4 shaded = ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity, surface);
    shaded.rgb = ApplySceneFog(shaded.rgb, EyePosition, input.WorldPosition);

    return shaded;
}

//THE COMB, CUT IN A DEPTH PASS OF ITS OWN. A clip anywhere in a pixel program keeps the GPU from rejecting a hidden pixel
//before it is shaded, and a crown is plate over plate: measured at the close camera (3840 x 1600, ssaa 2) the comb's clip was
//1.0 of the near crowns' 1.09 ms, the strands without it 0.09. So the near crowns are drawn twice (ForestScatterRenderer):
//this pass writes their depth with the comb cut and no colour, and InstancedModelTriplanarNeedles shades on exactly that depth
//(an EQUAL test, no clip), every visible pixel once. Both run NeedleVS, so the two depths are the same to the bit.
float4 NeedleDepthPS(NeedleVertexOutput input) : COLOR
{
    NeedleBands b = NeedleBandsAt(input.Bough);
    clip(b.Coverage - 0.5);
    return 0;
}

technique InstancedModelNeedleDepth
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL NeedleVS();
        PixelShader = compile PS_SHADERMODEL NeedleDepthPS();
    }
};

technique InstancedModelTriplanarNeedles
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL NeedleVS();
        PixelShader = compile PS_SHADERMODEL TriplanarNeedlesPS();
    }
};
