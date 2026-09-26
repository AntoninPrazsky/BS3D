//The sun's cast shadows, one copy (#469): sampling of the orthographic shadow map SceneRenderer renders from
//the sun's direction before the scene pass (SunShadowMap). A receiver includes this, declares nothing else,
//and multiplies its SUN term by SunShadow() - the dome's ambient is untouched, which is what a shadow is: the
//sky still lights it, the sun does not.
//
//Nothing in the project cast a shadow until #469: a tree was lit by dot(N, Sun) and the grass under it by the
//same rule with nothing in between, so a plain full of solids floated on an evenly lit floor. The map is a
//single 32-bit depth target fitted round the camera (a fixed extent, the window snapped to its own texels so
//the shadows do not swim as the camera moves), so shadows exist within ShadowExtent of the lens and fade out
//at the map's edge rather than ending in a line.
//
//Every caller gates on ShadowStrength with a [branch]: 0 means no map is bound this frame (the scene has
//none, the tier dropped it, or the sun is below the horizon), and the samples must not run - they would
//read an unbound texture, and on a scene without shadows they would cost nine taps for nothing.

texture ShadowMap;
sampler ShadowSampler = sampler_state
{
    Texture = <ShadowMap>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//World -> the map's clip space (view * orthographic projection), the size of one texel in the map's UV, how
//dark a full shadow is (1 = the sun term gone entirely; a little under it keeps a shadow from reading as a
//hole), and the depth bias in the map's own depth units, already scaled to the map's depth range.
float4x4 ShadowViewProjection;
float ShadowTexel;
float ShadowStrength;
float ShadowBias;

//THE CEILING'S GLASS CASTS TOO (#553) - and not through the map. A depth map can only say lit or unlit, and the
//glass drawn into it would cast exactly as solid stone (the rule the drain's funnel and the gun's window already
//follow, see "Sun shadows" in docs/rendering.md). But the plate is one axis-aligned slab hung over the island, so
//whether a point's ray to the sun passes through it is a few lines of arithmetic, and that is what is done here: the
//ray is carried up to the slab's underside and to its top face, each landing is measured against the outline
//CutSlabMesh cut (a rectangle with its corners cut round), and a ray that lands inside either face's outline has
//crossed glass. A second light-space target the plate would write its transmittance into was the alternative; it
//would cost a target, a caster pass and more taps a pixel for the shadow of one box the receiver can intersect
//itself - and it could not hold the cut below, which is a property of where the light went rather than of the glass.
//
//What the glass does to the light it passes, in three zones in from the outline, as CeilingPlate cut them:
//  - through the ground top edge (the crown's steep facets, CeilingShadowCut.z) most of the light is thrown out
//    sideways or reflected, so the shadow's rim is its darkest line (GLASS_SHADOW_RIM);
//  - through the border of flutes it is scattered finely enough that it lands as an even mean;
//  - through the diamond field every facet is a thin prism that turns its light by (n - 1) x its tilt towards its
//    pyramid's axis, so a facet's patch of light lands shifted by that angle times how far it travels - where two
//    patches overlap the light doubles, where they part there is a gap. Which patches reach a point is answered
//    exactly for flat facets: each facet's light arrives shifted by one known vector, so a point is lit by facet k
//    if the point that shift away belongs to facet k. Four lookups, summed: 1 on average (the light is conserved),
//    0 in a gap, 2 on a caustic line, softened by the sun's own width over the distance travelled.
//
//CeilingShadowCentre: the slab's centre in world space, and in w how much of the sun its uncut glass takes away (0 = no
//plate this frame, and the whole of this is skipped). The scene's ShadowStrength is applied by SunShadow over the
//glass and the map together. CeilingShadowSize: its
//half extents, and in w the radius the corners are cut round to. CeilingShadowCut: the diamond cut's period and
//facet slope, the crown's width, and the inset where the diamond field begins. SceneRenderer.CastCeilingShadow's
//figures, read off the plate's own renderer so the shadow and the drawn glass cannot disagree.
float4 CeilingShadowCentre;
float4 CeilingShadowSize;
float4 CeilingShadowCut;

//Crown glass's index less one: a thin prism turns a ray by this times its angle.
static const float GLASS_SHADOW_BEND = 0.5;

//The sun's angular diameter in radians: how fast the edge of the shadow and its caustic lines go soft with distance.
static const float GLASS_SHADOW_SUN_WIDTH = 0.0093;

//The least softness the edge and the lines take, in world units, so a caustic seen right under the pane still
//resolves into a line rather than a stair of texels.
static const float GLASS_SHADOW_MIN_SOFT = 0.04;

//How much of the light the crown's facets take out of the shadow's rim, on top of what uncut glass takes.
static const float GLASS_SHADOW_RIM = 0.5;

//How far the diamond field's focusing swings the light about its mean: 1 would be the full 0-to-2 of the facets'
//patches, which from a pane this faint read as a grid painted on the stone. Glass, not a lens.
static const float GLASS_SHADOW_CAUSTIC = 0.45;

//How much of a diamond facet's light a point of the cut (in cell coordinates) takes, for the facet whose tilt runs
//along `axis` (a signed cell axis): 1 inside that facet, 0 outside, soft over `soft` cells at its edges. The four
//facets of a pyramid share the cell between them exactly, so the four sum to 1 at every point.
float GlassFacetShare(float2 cell, float2 axis, float soft)
{
    float2 c = frac(cell) - 0.5;
    float2 a = abs(c);
    float2 across = abs(axis);
    float dominant = dot(a, across) - dot(a, 1.0 - across);
    return saturate(dominant / soft + 0.5) * saturate(dot(c, axis) / soft + 0.5);
}

//The sunlight factor the ceiling's glass leaves at a point: 1 where the ray to the sun misses the slab, down to what
//the glass passes (and lower on its rim, higher on a caustic) where it crosses it.
float CeilingGlassShadow(float3 worldPosition, float3 sunDirection)
{
    [branch]
    if (CeilingShadowCentre.w <= 0.0) return 1.0;

    float3 centre = CeilingShadowCentre.xyz;
    float3 extent = CeilingShadowSize.xyz;
    float underside = centre.y - extent.y;

    //Only what is under the pane: its own faces, and anything level with it or above, have nothing of it between
    //them and the sun. A per-pixel branch with nothing inside that takes a derivative.
    if (worldPosition.y > underside - 0.01) return 1.0;

    float invY = 1.0 / max(sunDirection.y, 0.05);
    float toUnder = (underside - worldPosition.y) * invY;
    float toTop = toUnder + 2.0 * extent.y * invY;
    float2 under = worldPosition.xz + sunDirection.xz * toUnder - centre.xz;
    float2 top = worldPosition.xz + sunDirection.xz * toTop - centre.xz;

    //How far in from the outline each landing is, negative outside: the rounded rectangle's own distance
    float radius = CeilingShadowSize.w;
    float2 dUnder = abs(under) - (extent.xz - radius), dTop = abs(top) - (extent.xz - radius);
    float insetUnder = radius - length(max(dUnder, 0.0)) - min(max(dUnder.x, dUnder.y), 0.0);
    float insetTop = radius - length(max(dTop, 0.0)) - min(max(dTop.x, dTop.y), 0.0);

    //A ray that lands inside either outline has crossed glass (a low sun sends it in through the side), so the
    //shadow is the union of the two faces' projections
    float soft = max(toUnder * GLASS_SHADOW_SUN_WIDTH, GLASS_SHADOW_MIN_SOFT);
    float cover = saturate(max(insetUnder, insetTop) / soft + 0.5);
    if (cover <= 0.0) return 1.0;

    //The diamond field's caustics: each facet's light arrives shifted towards its pyramid's axis by its bend times
    //the way it travels from the top face (cell units along the turned grid, the cut's own GlassCutCell)
    float period = CeilingShadowCut.x;
    float2 cell = float2(top.x + top.y, top.x - top.y) * (0.70710678 / period);
    float shift = GLASS_SHADOW_BEND * CeilingShadowCut.y * toTop / period;
    float cellSoft = max(toTop * GLASS_SHADOW_SUN_WIDTH, GLASS_SHADOW_MIN_SOFT) / period;
    float gathered = GlassFacetShare(cell + float2(shift, 0.0), float2(1.0, 0.0), cellSoft)
        + GlassFacetShare(cell - float2(shift, 0.0), float2(-1.0, 0.0), cellSoft)
        + GlassFacetShare(cell + float2(0.0, shift), float2(0.0, 1.0), cellSoft)
        + GlassFacetShare(cell - float2(0.0, shift), float2(0.0, -1.0), cellSoft);

    float field = saturate((insetTop - CeilingShadowCut.w) / soft + 0.5);
    float rim = saturate((CeilingShadowCut.z - insetTop) / soft + 0.5) * saturate(insetTop / soft + 0.5);

    //Four lookups can all land on their facets where the shift is several cells long, and four times the sun on
    //one point is a lens, not a pane: held to the doubling two overlapping facets give
    float light = 1.0 + (min(gathered, 2.0) - 1.0) * GLASS_SHADOW_CAUSTIC * field;
    light *= 1.0 - GLASS_SHADOW_RIM * rim;

    return lerp(1.0, (1.0 - CeilingShadowCentre.w) * light, cover);
}

//The map is sampled by hand with a nine-tap box (PCF) rather than a comparison sampler. (This said MonoGame's
//effect path gives no comparison state; 3.8.5 has SamplerState.ComparisonFunction and TextureFilterMode.Comparison,
//so hardware PCF is possible and untried - #591.) Nine point taps on a 2048 map are cheap against a full-screen terrain
//shader's other work. tex2Dlod rather than tex2D, because this runs under a [branch] and a gradient
//instruction inside divergent flow is what the compiler refuses.
//
//Returns the sunlight factor: 1 lit, down to (1 - ShadowStrength) in full shadow. Outside the map, or past
//its far plane, everything is lit but for the ceiling's glass (which the map does not hold), and the last few
//per cent of the map's width fade the map's shadow out.
float SunShadow(float3 worldPosition, float3 normal, float3 sunDirection)
{
    //The ceiling's glass first (#553): it is not in the map, so it answers whether the map covers this point or not
    float glass = CeilingGlassShadow(worldPosition, sunDirection);

    float4 lp = mul(float4(worldPosition, 1.0), ShadowViewProjection);
    float2 uv = float2(lp.x * 0.5 + 0.5, 0.5 - lp.y * 0.5);
    float depth = lp.z;

    if (any(uv < 0.0) || any(uv > 1.0) || depth > 1.0) return 1.0 - (1.0 - glass) * ShadowStrength;

    //Slope-scaled: a surface turned away from the sun needs more bias than one facing it, or its own map
    //texels, quantised across it, put it in and out of its own shadow in stripes (acne).
    float ndotl = saturate(dot(normal, sunDirection));
    float bias = ShadowBias * (1.0 + 2.5 * (1.0 - ndotl));
    float reference = depth - bias;

    float lit = 0.0;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float stored = tex2Dlod(ShadowSampler, float4(uv + float2(x, y) * ShadowTexel, 0.0, 0.0)).r;
            lit += reference <= stored ? 1.0 : 0.0;
        }
    }
    lit /= 9.0;

    float2 edge = min(uv, 1.0 - uv);
    float fade = saturate(min(edge.x, edge.y) / 0.06);
    //The glass acts on the light that reaches the point past everything the map holds, so it vanishes in an umbra
    //rather than drawing its caustics into the cluster's shadow; the whole is scaled by the scene's strength as before
    lit = lerp(1.0, lit, fade);
    return 1.0 - (1.0 - lit * glass) * ShadowStrength;
}
