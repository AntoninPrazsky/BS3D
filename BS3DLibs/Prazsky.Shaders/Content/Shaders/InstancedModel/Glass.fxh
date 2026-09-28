//InstancedModel.fx: the two passes that bend what is behind a glass - the crystal cup's refraction target
//(InstancedRefraction, #426) and the ceiling plate's in-scene refraction (InstancedGlass, #541).

//THE CRYSTAL'S REFRACTION (#426). The crystal cup is alpha-blended over the frame behind it, which dims and tints
//what shows through and never bends it, and the owner's report was that it reads as fake because of exactly that.
//This pass draws the cup a second time into its own target (PostProcessPipeline.RefractionTarget), writing depth,
//so only the nearest surface of each pixel survives, and records where that surface sends the eye: the shift in
//screen uv the composite applies to the frame it re-resolves under the cup (Tonemap.fx, ForegroundCompositePS).
//
//The shift is the surface's view-space normal carried RefractionDepth world units in, projected at the surface's
//own depth - so it is a distance through the glass rather than a fraction of the screen, and the bending keeps the
//same look as the cup dollies in and out. Along the silhouette the normal lies in the view plane and the shift is
//largest, which is what a real rim does; across the middle of the bowl it swings from one side to the other, so a
//strong enough depth flips the background inside the bowl, which is what the references rendered for #426 show a
//thick crystal cup do. Blue channel: how edge-on the surface is, for the composite's darkening of the rim.
float RefractionDepth;

float4 RefractionPS(VertexShaderOutput input, bool isFrontFace : SV_IsFrontFace) : COLOR
{
    float3 normal = normalize(input.WorldNormal) * (isFrontFace ? 1.0 : -1.0);
    float3 viewNormal = mul(normal, (float3x3)View);
    float viewDepth = max(-mul(float4(input.WorldPosition, 1.0), View).z, 0.05);

    float2 shift = viewNormal.xy * (RefractionDepth / viewDepth) * float2(Projection._11, Projection._22) * 0.5;

    return float4(shift, saturate(1.0 - abs(viewNormal.z)), 1.0);
}

technique InstancedRefraction
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL RefractionPS();
    }
};

//THE CEILING'S GLASS BENDS WHAT IS BEHIND IT (#541). The plate was alpha-blended over the frame, which dims and tints
//what shows through it and never displaces it - the state the crystal cup was in before #426. The cup's answer does
//not fit here, because the cup is a layer outside the scene and the plate is inside it: the cluster hangs in front of
//it, the gun is seen against it, and its own highlights are in the very frame a re-resolve would bend. So the plate
//bends a COPY of the frame instead, taken just before it is drawn (GlassBehind, PostProcessPipeline.GrabScene), and it
//draws in the scene like everything else - depth-tested, so what stands in front of it hides it as before, with its
//own lit surface laid over the bent copy in the same pixel, so the highlights stay where they are.
//
//The copy is taken at the point that separates what is behind the glass from what is in front of it, which the host
//picks from where the camera is (BS3DGame.GrabCeilingBackground): from under the plate, before the session's own
//objects, so no ball pressed against the underside can be dragged into the pane; from over it, just before the pane,
//when everything in the frame is beneath it. This pass writes its pixel whole (alpha 1), since the copy already holds
//what the blend would have laid it over.
//
//WHERE A PIXEL LOOKS. The eye's ray is traced through the slab in the mesh's own space: in through the face this
//pixel is drawn on (refracted at GLASS_INDEX), across to the first of the slab's six planes it meets, out through
//that face (refracted again), and on for GLASS_BEHIND_DISTANCE; the copy is sampled where that point lands on the
//screen. A flat parallel slab only offsets a ray, so across the flat underside and a flat top the image would barely
//move - which is why the plate is CUT: every edge ground back in a run of facets and every corner cut round in a fan
//of them (CutSlabMesh), which are prisms, and across the top face a border of fine flutes inside the rim, a groove,
//and a diamond field of low pyramids, all evaluated here rather than modelled (GlassTopNormal). The underside stays
//flat because the cluster hangs from it, so from the play camera, which looks up at the plate, the bend is seen where
//the ray LEAVES through the cut top - the second surface, which a single-normal shift cannot see. The exit is traced
//to the box's planes, but where it reaches the top's plane over the ground edge the grind's own facet is taken (a ray
//leaving there really leaves through it), which is what makes the rim read cut from below as well as from above.
texture GlassBehind;
sampler2D GlassBehindSampler = sampler_state
{
    Texture = <GlassBehind>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//The slab's half size about the mesh's origin, and the diamond cut on its top face: the pyramids' spacing in world
//units and the tangent of their facets' tilt. CeilingPlate's figures, stated per draw by the renderer.
float3 GlassHalfExtents;
float GlassCutPeriod;
float GlassCutSlope;

//The outline CutSlabMesh built the slab on, and its ground edge, so a ray can be traced out through them rather than
//through the box (CeilingPlate's figures again). GlassCorner: the radius every corner's cuts are tangent to, and the
//angle between two consecutive cuts. GlassCrownInset/GlassCrownDrop: the top edge's grind in section, four points
//from the side band up to the top face's rim - how far in from the outline, and how far below the top face.
//GlassRim: the border of flutes cut round the top face inside that rim - its width, the flutes' spacing, their
//facets' tilt, and how far the whole border leans down towards the rim (all tangents).
float2 GlassCorner;
float4 GlassCrownInset;
float4 GlassCrownDrop;
float4 GlassRim;

//Crown glass.
static const float GLASS_INDEX = 1.5;

//How far past the slab what shows through it is taken to stand, in world units. From under the plate that is the
//sky and the far backdrop, at hundreds; from over it, the cluster and the island, at a few. At thirty the bend is
//three quarters of what a point at infinity would show from the play camera's ten units off, and a near cluster seen
//from above is bent a little more than it truly would be, which reads as thicker glass rather than as an error.
static const float GLASS_BEHIND_DISTANCE = 30.0;

//How far the three channels part along the bend - the cup's REFRACTION_DISPERSION reasoning (Tonemap.fx): a fringe of
//colour where the bend is strongest is most of what separates cut glass from a lens in the eye's reading of it.
static const float GLASS_DISPERSION = 0.03;

//The longest shift a pixel may take, as a fraction of the frame, approached softly (shift / (1 + |shift| / this)) so
//there is no crease where the bend saturates. A ray grazing a bevel can be sent far across the screen, and past the
//frame's edge the copy has nothing to show but its clamped border.
static const float GLASS_MAX_SHIFT = 0.06;

//The mitre groove that parts the border of flutes from the diamond field: its half width in world units and the
//tangent of its walls. Narrow and steep, because a cut line is read by the hard bright/dark pair its two walls throw.
static const float GLASS_GROOVE_HALF_WIDTH = 0.05;
static const float GLASS_GROOVE_SLOPE = 0.6;

//THE GLASS IS HAND-MADE (#640). A cut of mathematically identical pyramids in a flawless pane read as a render, and
//the owner's rule is that a material should never be let render as perfect: the natural stone with inclusions is the
//one that reads, and sells, as precious. So the pane carries the three flaws the references rendered for #640 show
//every piece of old cut glass carrying, all of them in what shows THROUGH it rather than painted on it:
//
//THE CUTTER'S HAND: no two pyramids alike. Each cell's four facets are ground a little steeper or shallower than the
//pattern's, and turned a little about the vertical, by a hash of the cell - so the bent sky breaks into fragments of
//uneven size instead of a regular mosaic. Both vanish where the facet's tilt does (the valleys), so the cut still
//meets its neighbours without a seam.
static const float GLASS_CUT_UNEVEN = 0.6;   //the slope, per cell, 1 +- this
static const float GLASS_CUT_TWIST = 0.45;    //and the facets turned sideways by up to this share of their tilt
//
//CORD (striae): the glass did not melt evenly, and streaks of a slightly different index run through it where it
//flowed, bending what is seen through them in long faint waves. A noise stretched along the pane's X, added to the
//ray inside the slab.
static const float GLASS_CORD_STRENGTH = 0.07;
static const float3 GLASS_CORD_SCALE = float3(0.18, 1.6, 0.9);
//
//SEEDS: small air bubbles caught in the melt, on two planes inside the slab so they part in depth as the eye moves.
//A bubble is a diverging lens with a dark rim - the rim is where the light inside meets the bubble's wall past the
//critical angle - and a pinpoint glint. Sparse: most cells of the scatter hold none.
static const float GLASS_SEED_CELL = 0.55;       //the scatter's lattice, world units
static const float GLASS_SEED_CHANCE = 0.5;     //the share of cells holding a bubble
static const float2 GLASS_SEED_RADIUS = float2(0.03, 0.11);
static const float2 GLASS_SEED_DEPTHS = float2(-0.12, 0.22);  //the two planes, as a share of the slab's half thickness

struct GlassVSOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
    float3 LocalPosition : TEXCOORD3;
};

GlassVSOutput GlassVS(VertexShaderInput input, InstanceInput instance)
{
    VertexShaderOutput main = MainVS(input, instance);

    GlassVSOutput output;
    output.Position = main.Position;
    output.WorldPosition = main.WorldPosition;
    output.WorldNormal = main.WorldNormal;
    output.OcclusionData = main.OcclusionData;
    output.LocalPosition = mul(input.Position, Bone).xyz;

    return output;
}

//The diamond cut's cell coordinate at a mesh-space xz: the pyramid grid turned 45 degrees, so the cuts run corner to
//corner of the pane the way a cut-glass tray's do. One cell per GlassCutPeriod along each diagonal.
float2 GlassCutCell(float2 xz)
{
    return float2(xz.x + xz.y, xz.x - xz.y) * (0.70710678 / GlassCutPeriod);
}

//The outward normal of the diamond cut's facet at a cell coordinate. `footprint` is how many cells one pixel spans
//(taken by the caller with fwidth, outside any branch): every facet edge is softened over about a pixel, so the bent
//image does not alias along it, and the cut fades flat as the cells shrink towards a few pixels - a distant plate on
//the menu's orbit - where the facets would only shimmer. `strength` scales the facets' slope on top of that: the
//caller fades the cut out where the eye meets the pane almost edge-on (see GlassPS).
float3 GlassCutNormal(float2 cell, float footprint, float strength)
{
    float2 c = frac(cell) - 0.5;
    float2 a = abs(c);
    float soft = max(footprint, 1e-4);

    //Which of a pyramid's four facets: the side of its dominant axis, blended across the diagonals between them
    float onX = saturate((a.x - a.y) / (2.0 * soft) + 0.5);

    //And the tilt falls to nothing at the valley between two pyramids over the same pixel, so the flip from one
    //pyramid's facet to its neighbour's is a narrow flat rather than a hard edge
    float2 valley = saturate((0.5 - a) / soft);
    float2 tilt = float2(sign(c.x) * onX * valley.x, sign(c.y) * (1.0 - onX) * valley.y);

    //The cutter's hand (#640): this cell's own pitch, and its facets turned a little about the vertical
    float2 hand = NoiseHash22(floor(cell) + 17.0);
    tilt = tilt * (1.0 + GLASS_CUT_UNEVEN * hand.x) + float2(-tilt.y, tilt.x) * (GLASS_CUT_TWIST * hand.y);

    float slope = GlassCutSlope * strength * saturate(1.0 - (footprint - 0.1) / 0.2);

    //Back from the turned grid to xz
    float2 t = float2(tilt.x + tilt.y, tilt.x - tilt.y) * 0.70710678;

    return normalize(float3(t.x * slope, 1.0, t.y * slope));
}

//Where a mesh-space xz stands against the slab's outline: how far in from it (Inset), the outward normal of the cut
//of it nearest (Outward) and that cut's own direction along the outline (Along, both xz), and a coordinate along that
//cut counted in flutes (Phase), which is what the border's flutes are cut across.
struct GlassRimFrame
{
    float Inset;
    float2 Outward;
    float2 Along;
    float Phase;
};

//Every cut of CutSlabMesh's outline is tangent to its corner's circle (the axis edges are the cuts at 0 and 90
//degrees), so the nearest cut is the one whose normal lies closest in angle to the point's offset from that circle's
//centre, and the inset is the radius less that offset along it. Worked in the positive quadrant and mirrored out.
GlassRimFrame GlassRimAt(float2 xz)
{
    float2 mirror = xz >= 0 ? 1.0 : -1.0;
    float2 v = abs(xz) - (GlassHalfExtents.xz - GlassCorner.x);

    //Past the corner's fan on either side the nearest cut is an axis edge; deep inside both of them, it is whichever
    //of the two axis edges the point is nearer, which is the larger of v's two (negative) components
    float angle = clamp(atan2(v.y, v.x), 0.0, 1.57079633);
    angle = v.x < 0 && v.y < 0 ? (v.x > v.y ? 0.0 : 1.57079633) : angle;
    float cut = round(angle / GlassCorner.y) * GlassCorner.y;

    float2 n = float2(cos(cut), sin(cut));
    float2 t = float2(-n.y, n.x);

    GlassRimFrame frame;
    frame.Inset = GlassCorner.x - dot(n, v);
    frame.Outward = n * mirror;
    frame.Along = t * mirror;
    frame.Phase = dot(v, t) / GlassRim.y;
    return frame;
}

//The outward normal of the slab's top at a point of the top face's plane, cut or ground. `cellFootprint`,
//`fluteFootprint` and `insetFootprint` are how many diamond cells, flutes and world units one pixel spans there (taken
//by the caller with fwidth, outside any branch), `strength` the caller's edge-on fade; see GlassCutNormal.
//
//Three zones in from the outline. Inside the top edge's grind (a ray the box trace sends out through the top's plane
//there really leaves through one of the grind's facets, CutSlabMesh's crown): that facet, as modelled, with no fade -
//it is geometry, and the mesh draws it at the same angle. Then the border: flutes cut across it, square to the nearest
//cut of the outline, so they fan round every corner and meet each other at a mitre along the corner's cuts, the way a
//cut-glass tray's border does; the whole border leans a little down towards the rim, and a steep V groove parts it
//from the field. Inside the groove, the diamond cut.
float3 GlassTopNormal(GlassRimFrame frame, float2 cell, float cellFootprint, float fluteFootprint, float insetFootprint,
    float strength)
{
    float3 outward = float3(frame.Outward.x, 0, frame.Outward.y);

    if (frame.Inset < GlassCrownInset.w)
    {
        //The crown's facet this inset lies on: from point a in to point b, rising by the drop between them
        bool first = frame.Inset < GlassCrownInset.y, second = frame.Inset < GlassCrownInset.z;
        float2 a = first ? float2(GlassCrownInset.x, GlassCrownDrop.x) : second ? float2(GlassCrownInset.y, GlassCrownDrop.y) : float2(GlassCrownInset.z, GlassCrownDrop.z);
        float2 b = first ? float2(GlassCrownInset.y, GlassCrownDrop.y) : second ? float2(GlassCrownInset.z, GlassCrownDrop.z) : float2(GlassCrownInset.w, GlassCrownDrop.w);

        return normalize(outward * (a.y - b.y) + float3(0, b.x - a.x, 0));
    }

    float intoField = frame.Inset - GlassCrownInset.w - GlassRim.x;

    if (intoField > GLASS_GROOVE_HALF_WIDTH)
        return GlassCutNormal(cell, cellFootprint, strength);

    float3 tilt;

    if (intoField > -GLASS_GROOVE_HALF_WIDTH)
    {
        //The groove: each wall faces across it, softened over a pixel at its floor so it does not alias
        float wall = clamp(intoField / max(insetFootprint, 1e-4), -1.0, 1.0);
        float fade = saturate(1.0 - (insetFootprint / (2.0 * GLASS_GROOVE_HALF_WIDTH) - 0.1) / 0.2);
        tilt = outward * wall * GLASS_GROOVE_SLOPE * fade;
    }
    else
    {
        //A flute: a V across the border, its two facets turned along the outline either way, flat for a pixel at
        //the valley and the ridge so neither aliases, and fading out as the flutes shrink to a few pixels
        float c = frac(frame.Phase) - 0.5;
        float soft = max(fluteFootprint, 1e-4);
        float flank = sign(c) * saturate(abs(c) / soft) * saturate((0.5 - abs(c)) / soft);
        float fade = saturate(1.0 - (fluteFootprint - 0.1) / 0.2);
        float3 along = float3(frame.Along.x, 0, frame.Along.y);
        tilt = along * flank * GlassRim.z * fade + outward * GlassRim.w;
    }

    return normalize(float3(0, 1, 0) + tilt * strength);
}

float4 GlassPS(GlassVSOutput input) : COLOR
{
    float3 p = input.LocalPosition;
    float3 faceNormal = normalize(input.WorldNormal);
    float3 view = normalize(input.WorldPosition - EyePosition);

    //The top face carries the cut; every other face is as modelled. The cap's normal is exactly up, so a primitive is
    //one side of this and nothing below branches on a pixel.
    bool onTop = faceNormal.y > 0.99;

    //The cut fades out where the eye meets the pane nearly edge-on, from 78 degrees off its normal to 87. There the
    //cells are foreshortened into slivers a few pixels tall and many wide, which the footprint fade above cannot see
    //(it takes the larger of the two), and the facets shattered the image into a sawtooth of bands across the frame:
    //the front end's close pass (#261) skims the plate at exactly that angle. A plate seen that way is a mirror of the
    //sky anyway (the Fresnel term below), so what fades is a bend nobody could read.
    float cutStrength = saturate((abs(dot(view, faceNormal)) - 0.05) / 0.15);

    float2 cellHere = GlassCutCell(p.xz);
    float footprintHere = max(fwidth(cellHere.x), fwidth(cellHere.y));
    GlassRimFrame rimHere = GlassRimAt(p.xz);
    float fluteFootprintHere = fwidth(rimHere.Phase), insetFootprintHere = fwidth(rimHere.Inset);
    float3 entryNormal = onTop
        ? GlassTopNormal(rimHere, cellHere, footprintHere, fluteFootprintHere, insetFootprintHere, cutStrength)
        : faceNormal;

    //In through this face, and bent on its way across by the cord in the glass (#640)...
    float3 inside = refract(view, entryNormal, 1.0 / GLASS_INDEX);
    float3 cordAt = p * GLASS_CORD_SCALE;
    inside = normalize(inside + GLASS_CORD_STRENGTH * float3(GradientNoise3(cordAt), 0, GradientNoise3(cordAt + 31.7)));

    //...across the slab to the first of its six planes the ray reaches. The bevels are not traced on the way out: a
    //ray that leaves through one leaves within a bevel's width of the plane it is sent to instead.
    float3 dir = inside + (inside >= 0 ? 1e-5 : -1e-5);
    float3 toPlane = ((dir >= 0 ? GlassHalfExtents : -GlassHalfExtents) - p) / dir;
    float t = min(toPlane.x, min(toPlane.y, toPlane.z));
    float3 exitPoint = p + inside * t;

    float3 planeNormal = t == toPlane.x ? float3(sign(dir.x), 0, 0)
        : t == toPlane.y ? float3(0, sign(dir.y), 0)
        : float3(0, 0, sign(dir.z));

    //...and out through it: the cut's facet if that is the top - or, within the top edge's grind, the grind's facet
    //the ray really leaves through - and the plane otherwise
    float2 cellThere = GlassCutCell(exitPoint.xz);
    float footprintThere = max(fwidth(cellThere.x), fwidth(cellThere.y));
    GlassRimFrame rimThere = GlassRimAt(exitPoint.xz);
    float fluteFootprintThere = fwidth(rimThere.Phase), insetFootprintThere = fwidth(rimThere.Inset);
    float3 exitNormal = planeNormal.y > 0.5
        ? GlassTopNormal(rimThere, cellThere, footprintThere, fluteFootprintThere, insetFootprintThere, cutStrength)
        : planeNormal;

    float3 leaving = refract(inside, -exitNormal, GLASS_INDEX);

    //A ray the exit face turns back (total internal reflection) does leave, after a bounce this trace does not follow,
    //back out on the eye's side of the glass: it is sent off as the eye's ray mirrored in that face, which is where
    //such a facet's image comes from. So a facet the sky cannot pass through shows the scene under the pane instead,
    //as real cut glass does, rather than going dark - dark was tried first and put black bands across a pane seen
    //edge-on, where most rays meet the far face past the critical angle.
    leaving = dot(leaving, leaving) < 1e-6 ? reflect(view, exitNormal) : leaving;

    //Where that ray lands on the screen, against where this pixel is. The mesh is not rotated, so its offsets are the
    //world's.
    float3 seen = input.WorldPosition + (exitPoint - p) + leaving * GLASS_BEHIND_DISTANCE;
    float4 seenClip = mul(mul(float4(seen, 1.0), View), Projection);
    float4 hereClip = mul(mul(float4(input.WorldPosition, 1.0), View), Projection);

    float2 here = hereClip.xy / hereClip.w * float2(0.5, -0.5) + 0.5;
    float2 there = seenClip.xy / max(seenClip.w, 1e-3) * float2(0.5, -0.5) + 0.5;
    float2 shift = seenClip.w > 1e-3 ? there - here : 0;

    shift /= 1.0 + length(shift) / GLASS_MAX_SHIFT;

    //The seeds (#640): where the ray inside crosses either plane of bubbles, whether it passes through one. The
    //nearer one met wins; `seedAt` is where across it (0 at its centre, 1 at its wall), `seedAway` which way from it.
    float seedAt = 2.0;
    float2 seedAway = 0;
    [unroll]
    for (int layer = 0; layer < 2; layer++)
    {
        float planeY = (layer == 0 ? GLASS_SEED_DEPTHS.x : GLASS_SEED_DEPTHS.y) * GlassHalfExtents.y;
        float tPlane = (planeY - p.y) / (abs(inside.y) > 1e-4 ? inside.y : 1e-4);
        float2 q = (p + inside * clamp(tPlane, 0.0, t)).xz / GLASS_SEED_CELL + layer * 7.31;
        float2 id = floor(q);
        float2 roll = NoiseHash22(id + 3.9) * 0.5 + 0.5;
        float2 centre = id + 0.5 + 0.3 * NoiseHash22(id + 11.2);
        float radius = lerp(GLASS_SEED_RADIUS.x, GLASS_SEED_RADIUS.y, roll.y * roll.y) / GLASS_SEED_CELL;
        float2 d = q - centre;
        float across = length(d) / radius;
        bool crossed = tPlane > 0.0 && tPlane < t && roll.x < GLASS_SEED_CHANCE;
        if (crossed && across < seedAt)
        {
            seedAt = across;
            seedAway = d / max(length(d), 1e-4);
        }
    }

    //Softened over a pixel at the wall, and gone where a bubble is smaller than a couple of pixels
    float seedSoft = max(fwidth(seedAt), 1e-3);
    float seed = saturate((1.0 - seedAt) / seedSoft);

    //A diverging lens: what is behind is pushed out from its centre, more towards the wall
    shift += seedAway * seed * seedAt * seedAt * 0.02;

    float3 behind = float3(
        tex2Dlod(GlassBehindSampler, float4(here + shift * (1.0 + GLASS_DISPERSION), 0, 0)).r,
        tex2Dlod(GlassBehindSampler, float4(here + shift, 0, 0)).g,
        tex2Dlod(GlassBehindSampler, float4(here + shift * (1.0 - GLASS_DISPERSION), 0, 0)).b);

    //Its dark wall, a bright ring just inside it where the wall mirrors the sky, and a glint on the side facing up
    float seedWall = seed * smoothstep(0.75, 0.97, seedAt);
    float seedRing = seed * saturate(1.0 - abs(seedAt - 0.68) / 0.12);
    float3 sky = SkyRadiance(float3(0, 1, 0));
    behind *= 1.0 - 0.8 * seedWall;
    behind += (seedRing * 0.55 + seed * saturate(1.0 - length(seedAway * seedAt - float2(0.3, 0.3)) / 0.22)) * sky;

    //What the entry face lets in, and what it turns away: at a grazing angle glass is a mirror far more than a window
    //(Schlick at the dielectric F0). The part turned away is not lost - it is the sky reflected off the face - and
    //ShadePixel below lays alpha's worth of that reflection on already, so the rest of it is laid on here, and a pane
    //seen edge-on becomes a mirror of the sky rather than a dark band. Taking the transmission alone down (the first
    //cut of this) is what darkened every grazing view: the light went nowhere.
    float fresnel = FresnelSchlick(DielectricF0, dot(-view, entryNormal), 1.0).x;
    float3 mirror = SkyRadiance(reflect(view, entryNormal)) * SpecularAmbientStrength;

    //The plate's own surface, lit as it always was - off the cut's facet on the top face, so its highlights sparkle
    //facet by facet from above - over what shows through it, in place of the undisplaced frame the blend would have
    //used. Premultiplied like everything this effect writes, so the pane passes (1 - alpha) of what is behind it.
    float4 shaded = ShadePixel(input.WorldPosition, onTop ? entryNormal : faceNormal, input.OcclusionData, float4(1, 1, 1, 1), 1, 1);

    return float4(shaded.rgb + (behind * (1.0 - fresnel) + mirror * fresnel) * (1.0 - shaded.a), 1.0);
}

technique InstancedGlass
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL GlassVS();
        PixelShader = compile PS_SHADERMODEL GlassPS();
    }
};

//THE CRYSTAL CUP'S INCLUSIONS (#640). The cup was a flawless dielectric, and the owner's rule is that a material
//should never render as perfect: rock crystal with inclusions is the one that reads as natural, and as precious. The
//references rendered for the issue (rock-crystal goblets) carry three kinds, and this lays all three into the plain
//material's own shading, in the mesh's space so they turn with the cup rather than swimming over it:
//
// * a VEIL - a wisp of a healed fracture, milky where it is dense, flashing a thin film's rainbow as the cup turns;
// * RUTILE - a few sets of fine golden threads, straight and parallel, the way the needles grow;
// * and CLOUD - a dusting of pinpoint specks.
//
//All of it sits in the bowl and the stem, where the glass is thick in the references; none on the foot. Additive over
//the premultiplied surface, each raising the alpha by what it scatters, so a veil is milky rather than a hole. The
//figures are in mesh units (the cup is TrophyMesh.HEIGHT = 1 tall).
static const float CRYSTAL_VEIL_SCALE = 7.0;       //the veil's fbm, cycles per unit
static const float CRYSTAL_VEIL_AMOUNT = 0.4;     //how milky it gets at its densest
static const float CRYSTAL_FLASH_AMOUNT = 0.7;    //the thin film's rainbow over it
static const float CRYSTAL_RUTILE_AMOUNT = 0.45;
static const float CRYSTAL_RUTILE_SPACING = 60.0;  //planes per unit across the threads
static const float CRYSTAL_RUTILE_CHANCE = 0.12;   //the share of them holding one
static const float3 CRYSTAL_RUTILE_COLOR = float3(1.0, 0.72, 0.32);
static const float CRYSTAL_SPECK_CELL = 0.035;     //the cloud's lattice, and the share of its cells that hold a speck
static const float CRYSTAL_SPECK_CHANCE = 0.025;

GlassVSOutput CrystalVS(VertexShaderInput input, InstanceInput instance)
{
    return GlassVS(input, instance);
}

float4 CrystalPS(GlassVSOutput input, bool isFrontFace : SV_IsFrontFace) : COLOR
{
    VertexShaderOutput main;
    main.Position = input.Position;
    main.WorldPosition = input.WorldPosition;
    main.WorldNormal = input.WorldNormal;
    main.OcclusionData = input.OcclusionData;
    float4 shaded = MainPS(main, isFrontFace);

    float3 p = input.LocalPosition;
    float3 view = normalize(input.WorldPosition - EyePosition);
    float3 normal = normalize(input.WorldNormal) * (isFrontFace ? 1.0 : -1.0);
    float facing = abs(dot(view, normal));

    //Where the glass is thick enough to hold anything: the bowl and the stem, not the foot
    float held = smoothstep(0.12, 0.3, p.y);

    //The veil: two wisps, where a low-frequency field rises; milky where its fbm is dense
    float patch = smoothstep(0.0, 0.4, GradientNoise3(p * 2.2 + 5.3));
    float wisp = Fbm3(p * CRYSTAL_VEIL_SCALE + 1.7, 4);
    float veil = held * patch * saturate((wisp + 0.05) * 2.5);

    //Its thin film: a rainbow whose hue walks with the wisp and with the angle the eye meets the surface at, so it
    //flashes and moves as the cup turns rather than being painted on
    float hue = frac(wisp * 3.0 + facing * 2.0);
    float3 rainbow = saturate(abs(frac(hue + float3(0.0, 0.333, 0.667)) * 6.0 - 3.0) - 1.0);
    float flash = veil * smoothstep(0.35, 0.85, facing) * saturate(wisp * 4.0);

    //Rutile: straight parallel threads - a family of planes across one direction, a hash choosing which of them hold
    //a thread and where along it the thread starts and stops. The surface slices each plane in a straight line.
    float3 along = normalize(float3(0.35, 1.0, 0.2));
    float3 across = normalize(cross(along, float3(1, 0, 0.3)));
    float threadAt = dot(p, across) * CRYSTAL_RUTILE_SPACING;
    float2 threadRoll = NoiseHash22(float2(floor(threadAt), 3.1));
    float threadMid = 0.55 + 0.25 * threadRoll.y;
    float threadSpan = abs(dot(p, along) - threadMid);
    float threadWidth = max(fwidth(threadAt), 1e-3);
    float rutile = held * step(1.0 - 2.0 * CRYSTAL_RUTILE_CHANCE, threadRoll.x)
        * saturate(1.0 - abs(frac(threadAt) - 0.5) / (1.2 * threadWidth)) * saturate(0.25 / threadWidth)
        * saturate((0.18 - threadSpan) / 0.04);

    //Cloud: pinpoint specks where the surface passes through a speck of the lattice
    float3 cell = floor(p / CRYSTAL_SPECK_CELL);
    float3 roll = NoiseHash33(cell) * 0.5 + 0.5;
    float3 toSpeck = frac(p / CRYSTAL_SPECK_CELL) - (0.3 + 0.4 * roll);
    float speck = held * step(roll.x, CRYSTAL_SPECK_CHANCE) * saturate(1.0 - length(toSpeck) / 0.16);

    //Lit by how bright the sky over the cup is - its brightness only, so a gold thread stays gold and a veil white
    //under a coloured sky
    float3 light = dot(SkyRadiance(float3(0, 1, 0)), float3(0.2126, 0.7152, 0.0722));
    float3 added = light * (veil * CRYSTAL_VEIL_AMOUNT + speck * 1.2 + flash * CRYSTAL_FLASH_AMOUNT * rainbow
        + rutile * CRYSTAL_RUTILE_AMOUNT * CRYSTAL_RUTILE_COLOR);
    float scattered = saturate(veil * CRYSTAL_VEIL_AMOUNT + speck * 0.4 + rutile * 0.3);

    return float4(shaded.rgb + added, saturate(shaded.a + scattered));
}

technique InstancedCrystal
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CrystalVS();
        PixelShader = compile PS_SHADERMODEL CrystalPS();
    }
};

//THE DRAIN'S GLASS IS USED (#640, the rule the ceiling and the cup took, carried on to the next flawless surface in
//every frame of every level). Every shot that misses rolls down this cone, so what marks it is its traffic: fine
//scuffs running DOWNHILL where balls have rolled, a frosted band just inside the mouth where they land, and - the
//glass itself being made as the plate's was - a few seeds and a faint cord waving its reflection. In the mesh's
//space, which is the world's offset (the funnel does not move), in world units: the mouth is 14 across the radius.
static const float DRAIN_SCUFF_LINES = 420.0;      //angular positions round the cone a scuff can take
static const float DRAIN_SCUFF_CHANCE = 0.07;      //the share of them that carry one
static const float DRAIN_SCUFF_AMOUNT = 0.14;
static const float DRAIN_SCUFF_SPIRAL = 0.05;     //radians of turn per unit down the slope: a rolled ball curves
static const float DRAIN_WEAR_AMOUNT = 0.06;       //the landing band's frost at its densest
static const float2 DRAIN_WEAR_BAND = float2(11.0, 13.6);  //radii the landing band spans
static const float DRAIN_CORD_STRENGTH = 0.06;
static const float DRAIN_SEED_CELL = 0.7;
static const float DRAIN_SEED_CHANCE = 0.05;

float4 DrainGlassPS(GlassVSOutput input, bool isFrontFace : SV_IsFrontFace) : COLOR
{
    float3 p = input.LocalPosition;

    //Cord: the reflection waves a little where the glass flowed
    float3 cordAt = p * float3(0.25, 0.9, 0.25);
    float3 cord = float3(GradientNoise3(cordAt), 0, GradientNoise3(cordAt + 13.1)) * DRAIN_CORD_STRENGTH;

    VertexShaderOutput main;
    main.Position = input.Position;
    main.WorldPosition = input.WorldPosition;
    main.WorldNormal = normalize(input.WorldNormal + cord);
    main.OcclusionData = input.OcclusionData;
    float4 shaded = MainPS(main, isFrontFace);

    float radius = length(p.xz);
    float angle = (atan2(p.z, p.x) + radius * DRAIN_SCUFF_SPIRAL) * (0.15915494 * DRAIN_SCUFF_LINES);

    //Scuffs: a run of radial lines, each present or not and spanning its own stretch of the slope by a hash of its
    //place round the cone. An integer count of places keeps the pattern continuous across atan2's seam.
    float2 scuffRoll = NoiseHash22(float2(floor(angle), 7.7)) * 0.5 + 0.5;
    float scuffTop = lerp(8.0, 13.8, scuffRoll.y);
    float scuffLength = lerp(2.0, 7.0, frac(scuffRoll.y * 7.13));
    float scuffWidth = max(fwidth(angle), 1e-3);
    float scuff = step(scuffRoll.x, DRAIN_SCUFF_CHANCE) * (0.35 + 0.65 * frac(scuffRoll.x * 91.7))
        * saturate(1.0 - abs(frac(angle) - 0.5) / (1.2 * scuffWidth)) * saturate(0.3 / scuffWidth)
        * saturate((scuffTop - radius) / 0.6) * saturate((radius - (scuffTop - scuffLength)) / 1.5);

    //The landing band: a frost that thickens and thins round the ring
    float wear = smoothstep(DRAIN_WEAR_BAND.x, DRAIN_WEAR_BAND.x + 1.5, radius) * smoothstep(DRAIN_WEAR_BAND.y, DRAIN_WEAR_BAND.y - 0.8, radius)
        * saturate(Fbm3(p * 0.6, 3) * 1.6 + 0.4);

    //Seeds: pinpoint rings where the surface passes through one
    float3 cell = floor(p / DRAIN_SEED_CELL);
    float3 roll = NoiseHash33(cell + 5.0) * 0.5 + 0.5;
    float seedAt = length(frac(p / DRAIN_SEED_CELL) - (0.3 + 0.4 * roll)) / 0.11;
    float seed = step(roll.x, DRAIN_SEED_CHANCE) * saturate(1.0 - abs(seedAt - 0.75) / 0.25);

    //Milky, lit by how bright the sky is - see CrystalPS
    float3 light = dot(SkyRadiance(float3(0, 1, 0)), float3(0.2126, 0.7152, 0.0722));
    float milk = scuff * DRAIN_SCUFF_AMOUNT + wear * DRAIN_WEAR_AMOUNT + seed * 0.35;

    return float4(shaded.rgb + light * milk, saturate(shaded.a + milk * 0.6));
}

technique InstancedDrainGlass
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CrystalVS();
        PixelShader = compile PS_SHADERMODEL DrainGlassPS();
    }
};
