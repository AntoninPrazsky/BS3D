//InstancedModel.fx: THE SPACE STATION'S DECK (#954) - the island's material in space, a technique of its own selected by
//InstancedModelRenderer.StationSurface, on the cap and the hull of IslandShape.Station and nowhere else.
//
//The owner: the island "does not feel finished", and he asked for a space base. It was the machined disc with a square
//grid of seams in world X and Z, which is how a floor is tiled and not how anything round is built. Every reference
//(954-zimage, 954-klein) lays the deck in RADIAL WEDGE PLATES round a collar at the drain, staggered ring by ring, the
//plates of slightly different tones with the odd hatch, hazard stripes at the edge, and a bright light strip running round
//the rim in a groove. This draws exactly that, from the polar coordinates round the drain's axis.
//
//A technique of its own for the reason the dream's gives (IslandDream.fxh, #155): a branch in TriplanarPS would cost every
//scene's stone cap the union of both register allocations.

//The ring seams' radii from the drain's axis: the collar's edge, then the two rings between the wedge bands
float3 StationRings;

//How many wedges the two bands are cut into (the outer band, past the last ring, carries the second count, staggered)
float2 StationSpokes;

//The hazard arcs on the deck's edge: (inner radius, outer radius)
float2 StationHazard;

//The light strip in the rim's groove: (the world height of the groove's top, of its bottom, the radius inside which a side
//face is the groove's own face, the strip's radiance)
float4 StationStrip;
float3 StationStripColor;

//The seams: how wide one is cut (world units) and how deep the normal takes it
static const float SEAM_HALF_WIDTH = 0.045;
static const float SEAM_DEPTH = 0.025;

//A hatch on one plate in five: a ring cut this far round the plate's middle
static const float HATCH_RADIUS = 0.42;
static const float HATCH_SHARE = 0.2;

//How the plates' tones vary, and how many of them are a darker panel
static const float PLATE_TONE = 0.09;
static const float DARK_PLATE_SHARE = 0.16;
static const float DARK_PLATE_TONE = 0.72;

//The hazard arcs: how many round the deck, what share of the circle each covers, and the stripes' width along it
static const float HAZARD_ARCS = 6.0;
static const float HAZARD_ARC_SHARE = 0.11;
static const float HAZARD_STRIPE = 0.55;
static const float3 HAZARD_YELLOW = float3(0.80, 0.56, 0.06);
static const float3 HAZARD_DARK = float3(0.05, 0.05, 0.05);

float StationHash(float2 cell)
{
    return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
}

//The distance to the nearest spoke in a band of `count` wedges (offset by `stagger` of a wedge), and the wedge's index
float SpokeDistance(float angle, float radius, float count, float stagger, out float wedge)
{
    float turns = angle * (count / 6.2831853) + stagger;
    wedge = floor(turns);
    float f = turns - wedge;
    return min(f, 1.0 - f) * radius * (6.2831853 / count);
}

float4 TriplanarStationPS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);
    float3 p = input.WorldPosition;

    //The machined metal's grain: the island's detail texture, triplanar, as the stone takes it
    float3 blend = pow(abs(worldNormal), 4);
    blend /= blend.x + blend.y + blend.z;
    float3 t = p * DetailScale;
    float3 detail
        = SrgbToLinear(tex2D(TextureSampler, t.zy).rgb) * blend.x
        + SrgbToLinear(tex2D(TextureSampler, t.xz).rgb) * blend.y
        + SrgbToLinear(tex2D(TextureSampler, t.xy).rgb) * blend.z;
    float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

    //The plate under the pixel: which band (inside the collar's ring, between the rings, past the last) and which wedge
    float radius = length(p.xz);
    float angle = atan2(p.z, p.x) + 3.14159265;
    float band = radius < StationRings.x ? 0.0 : radius < StationRings.y ? 1.0 : radius < StationRings.z ? 2.0 : 3.0;
    float count = band < 1.5 ? StationSpokes.x : StationSpokes.y;
    float wedge;
    float spoke = SpokeDistance(angle, radius, count, band == 2.0 ? 0.5 : 0.0, wedge);
    spoke = band < 0.5 ? 1e3 : spoke;   //the collar is one ring, uncut
    float ring = min(min(abs(radius - StationRings.x), abs(radius - StationRings.y)), abs(radius - StationRings.z));
    float seam = min(spoke, ring);

    //A hatch on some plates of the two middle bands: a round cover cut about the plate's middle, on the deck only
    float plate = StationHash(float2(band, wedge));
    float up = saturate(worldNormal.y * 4.0 - 3.0);
    [branch]
    if (plate < HATCH_SHARE && band > 0.5 && band < 2.5)
    {
        float inner = band < 1.5 ? StationRings.x : StationRings.y;
        float outer = band < 1.5 ? StationRings.y : StationRings.z;
        float middleAngle = (wedge + 0.5 - (band == 2.0 ? 0.5 : 0.0)) * (6.2831853 / count) - 3.14159265;
        float middleRadius = 0.5 * (inner + outer);
        float2 middle = float2(cos(middleAngle), sin(middleAngle)) * middleRadius;
        float hatch = abs(distance(p.xz, middle) - HATCH_RADIUS);
        seam = lerp(seam, min(seam, hatch), up);
    }

    //The seams cut as grooves (the normal leans into them) and shaded down
    float footprint = length(abs(ddx(p)) + abs(ddy(p)));
    float groove = 1.0 - smoothstep(SEAM_HALF_WIDTH, SEAM_HALF_WIDTH + max(0.03, footprint * 2.5), seam);
    float3 reliefNormal = PerturbNormalFromHeight(worldNormal, p, -groove * SEAM_DEPTH);

    //The plates' own tones, a darker panel here and there
    texRgb *= (1.0 - PLATE_TONE + 2.0 * PLATE_TONE * frac(plate * 7.31)) * (plate > 1.0 - DARK_PLATE_SHARE ? DARK_PLATE_TONE : 1.0);
    texRgb *= 1.0 - 0.55 * groove;

    //The hazard arcs at the deck's edge: diagonal yellow and black stripes along a few arcs, on the deck only
    float arc = frac(angle * (HAZARD_ARCS / 6.2831853) + 0.08);
    float onHazard = step(StationHazard.x, radius) * step(radius, StationHazard.y) * step(arc, HAZARD_ARC_SHARE) * up;
    float stripe = step(0.5, frac((angle * radius + radius) / HAZARD_STRIPE));
    texRgb = lerp(texRgb, lerp(HAZARD_DARK, HAZARD_YELLOW, stripe) / max(SrgbToLinear(DiffuseColor.rgb), 1e-3), onHazard * (1.0 - groove));

    SurfaceSpecular surface = DefaultSurfaceSpecular();
    float4 shaded = ShadePixel(p, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, 1, surface);

    //The seams' faint running light (the dressing's JointGlow, #537), as on the machined disc
    shaded.rgb += JointGlow * (groove * groove);

    //The light strip: the groove's face glows, and the groove's floor and ceiling take its spill
    float inGroove = step(StationStrip.y, p.y) * step(p.y, StationStrip.x) * step(radius, StationStrip.z);
    float face = 1.0 - abs(worldNormal.y);
    shaded.rgb += StationStripColor * (StationStrip.w * inGroove * lerp(0.35, 1.0, face * face));

    return shaded;
}

technique InstancedModelTriplanarStation
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarStationPS();
    }
};
