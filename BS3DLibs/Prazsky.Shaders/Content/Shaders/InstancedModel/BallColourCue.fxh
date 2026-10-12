//===================================================================================================
//THE COLOUR CUE PROTOTYPES (#737), THE FIGURES. A player could not tell two wool balls apart by their colour
//alone, and the owner wants every colour to carry a second channel of its own. Whether that channel is PAINTED
//on the round ball or CARVED into its shape is not decided yet, so both readings are built here on four colours
//and are put in front of him side by side; only then are all thirteen built, in one of them.
//
//This file is the figures and nothing else: functions and static constants, NO UNIFORMS, so including it
//moves nothing in the constant buffer (the rule in InstancedModel.fx). The one uniform that says which figure a
//draw carries, and the programs that draw them, are BallColourCueTechniques.fxh's, last in the effect. The three
//ball bodies that take a cue (PatternShade, WoolShade, MarbleShade) call these with their switches passed as
//literals, so the shipped techniques compile exactly as they did before the prototypes existed.
//
//`cue` is the per-draw slot, 1..4 (Prazsky.BS3D.ColourCues says which colour carries which):
//
//    slot   painted (motif)            carved (shape)
//     1     three tilted stripes       six ridges (a melon's lobes)
//     2     twelve dots                twelve dimples
//     3     checks                     twenty facets
//     4     six five-pointed stars     four twisted grooves
//
//EVERYTHING IS IN OBJECT SPACE, so a figure turns with its ball (contract point 6 comes free) and every ball of
//one colour carries the same one: the figure names the colour, not the ball.
//
//WHAT NEITHER MAY LOOK LIKE. A special kind - the bomb's slanted glowing seams and studs, the stone's broken
//outline and cracks, the frozen ball's six flat faces, the heavy casting's patches, the gravity well's spiral,
//the zap's bolts, the clear glass's cast seam - or the vinyl's gores, which are the rotation cue. So a painted
//figure is a paler or darker shade of the ball's own colour and never glows, the stripes are tilted off every
//axis the gores and the polar discs use, the facets are twenty small ones and not six big ones, and every carve
//is INWARD only: a drawn ball that left its cell would overlap a neighbour the simulation says it is not
//touching (the stone's and the frozen ball's rule). The physics, the lattice and the match rule never see any
//of it - they are about the type, and the type is still a sphere.
//===================================================================================================

//THE PAINTED FIGURES

//The stripes run round this axis: tilted off the object Y the gores converge on, so three pale bands cannot be
//read as the beach ball's own pattern, and off the horizontal so they are not the bomb's latitude bands either.
static const float3 ColourCueStripeAxis = float3(0.449439, 0.799002, -0.399501);

//Three bands (at the axis's equator and at +-2/3 along it) and the share of each period the pale band takes.
//Three, not more: at play distance a ball is 14-20 display pixels in radius and a band has to stay several
//pixels wide or it averages into a flat wash (#311's lesson about the wool's strands).
static const float ColourCueStripeCount = 3.0;
static const float ColourCueStripeShare = 0.42;

//The twelve vertices of an icosahedron as six axes, both ends of each: the most even way to put a dozen points
//on a ball, so from every side about the same number of dots (or dimples) faces the camera.
static const float3 ColourCueIcosahedron[6] =
{
    float3(0.000000, 0.525731, 0.850651),
    float3(0.000000, 0.525731, -0.850651),
    float3(0.525731, 0.850651, 0.000000),
    float3(0.525731, -0.850651, 0.000000),
    float3(0.850651, 0.000000, 0.525731),
    float3(-0.850651, 0.000000, 0.525731)
};

//A dot's angular radius as a cosine: 0.34 rad, 19.5 degrees, about a third of the gap to the next vertex, so the
//dots cover a third of the ball and read as dots rather than as a net.
static const float ColourCueDotCos = 0.942755;

//The checks are the sign of sin(k x) sin(k y) sin(k z) in a turned frame: k = 1.5 pi cuts each axis at 0 and
//+-2/3, about three cells across the ball, square at the middle of each face. The frame is turned so the cuts
//do not line up with the gores or the poles.
static const float3x3 ColourCueCheckFrame = float3x3(
    0.837812, -0.427769, 0.339242,
    0.477673, 0.875240, -0.076051,
    -0.264386, 0.225763, 0.937620);
static const float ColourCueCheckFrequency = 4.712389;

//Six stars, one round each axis of a turned frame (the six faces of a cube, read through a gnomonic projection),
//their points reaching ColourCueStarOuter and their notches ColourCueStarInner in that projection's units
//(0.62 is 32 degrees off the face's centre, short of the 45 where the next face starts, so no star is ever cut).
static const float3x3 ColourCueStarFrame = float3x3(
    0.959194, -0.236530, 0.154919,
    0.154919, 0.897985, 0.411852,
    -0.236530, -0.371046, 0.897985);
static const float ColourCueStarOuter = 0.62;
static const float ColourCueStarInner = 0.27;

//The painted figure's ink: a darker shade of the colour on a light colour, and on a dark one a paler shade (the
//wool, the marble) or the gores' white (the vinyl) - the same hue either way, so the ball still says its colour
//first and the figure second. The split is a linear luminance between the silver's 0.24 and the beige's 0.57:
//white on the beige or the yellow is the contrast the first captures could not see.
static const float ColourCueInkSplit = 0.4;
static const float ColourCueInkDark = 0.28;
static const float3 ColourCueInkPale = float3(0.85, 0.85, 0.85);
static const float ColourCueInkPaleShare = 0.6;

//Below about seven target pixels of radius a figure cannot be drawn, only aliased, so it is eased out on the
//pixel footprint (the vinyl's yardstick, surface per pixel over the radius): whole up to 0.2, gone by 0.4. A
//ball in play is 0.07-0.15 at the desktop's and the laptop's resolutions, so this is the overview's far balls
//and nothing a player aims at.
static const float ColourCueFadeStart = 2.0;
static const float ColourCueFadeRate = 5.0;

//The painted figure's signed field, positive inside it. No derivative inside the branch - the branch is on a
//per-draw uniform, but the antialiasing that needs the field's screen gradient is taken outside it.
float ColourCueMotifField(float3 direction, float cue)
{
    float field;

    [branch]
    if (cue < 1.5)
    {
        float along = dot(direction, ColourCueStripeAxis);
        field = cos(3.14159265 * ColourCueStripeCount * along) - cos(3.14159265 * ColourCueStripeShare);
    }
    else if (cue < 2.5)
    {
        float nearest = 0;

        [unroll]
        for (int i = 0; i < 6; i++) nearest = max(nearest, abs(dot(direction, ColourCueIcosahedron[i])));

        field = nearest - ColourCueDotCos;
    }
    else if (cue < 3.5)
    {
        float3 cell = mul(ColourCueCheckFrame, direction) * ColourCueCheckFrequency;
        field = sin(cell.x) * sin(cell.y) * sin(cell.z);
    }
    else
    {
        //The face of the cube this direction is on, and where on it: a select, not a branch, so the quad stays together
        float3 q = mul(ColourCueStarFrame, direction);
        float3 a = abs(q);
        float2 uv = a.x > max(a.y, a.z) ? q.yz / a.x : (a.y > a.z ? q.zx / a.y : q.xy / a.z);

        //1 along a point, 0 in the notch between two. Five points, so the period is 2 pi / 5, which also makes the
        //value continuous across atan2's branch cut. Squared, so the arms taper like a star's and do not bulge.
        float spoke = abs(frac(atan2(uv.y, uv.x) * (5.0 / 6.2831853)) * 2 - 1);
        field = lerp(ColourCueStarInner, ColourCueStarOuter, spoke * spoke) - length(uv);
    }

    return field;
}

//How much of this pixel the painted figure covers, 0..1: a one-pixel soft edge, then the whole figure eased out
//on balls too small to carry it.
float ColourCueMotif(float3 direction, float footprint, float cue)
{
    float field = ColourCueMotifField(direction, cue);

    return AntialiasedStep(0, field) * saturate(ColourCueFadeStart - footprint * ColourCueFadeRate);
}

//What the figure is painted in: the darker shade on a light colour, the caller's pale ink on a dark one.
float3 ColourCueInk(float3 primary, float3 pale)
{
    float luminance = dot(primary, float3(0.2126, 0.7152, 0.0722));

    return luminance > ColourCueInkSplit ? primary * ColourCueInkDark : pale;
}

//THE CARVED FIGURES. Each is a share of the radius taken away along a direction, never negative, so the carved
//ball stays inside its sphere. The vertex shader moves the mesh by it (the silhouette and the depth the
//neighbours meet), and the pixel shader shades it as a height field on the sphere's own normal through
//PerturbNormalFromHeight - the only way a pixel here can light a shape, since nothing at that stage can turn an
//object-space normal into the world (the gem's lesson). The deepest carve is 0.16 of the radius, 0.08 world units
//on a ball of radius 0.5: inside the sun shadow's 0.12 bias, so a carved ball does not shadow itself on its own
//sphere's map (the caster still draws spheres).

//The grooves between the ridges and the twisted ones: how wide a groove is (in |sin| of its angle round the ball)
//and how deep, and where towards the poles they fade (the lobes converge there and a groove would pinch to a point).
static const float ColourCueGrooveWidth = 0.45;
static const float ColourCueRidgeDepth = 0.10;
static const float ColourCueTwistDepth = 0.10;
static const float ColourCuePoleFade = 0.55;

//How far the four twisted grooves turn from one pole to the other: 3.2 radians of phase over the ball's height,
//so each groove winds through about 90 degrees of the ball - a twist, not a spiral.
static const float ColourCueTwist = 3.2;

//The dimples: a paraboloid bowl at each of the icosahedron's twelve vertices, 23 degrees in angular radius (a
//sharp rim, so it reads as a pressed cup and not as a soft dent) and 0.16 of the radius deep at its centre - the
//deepest carve here, because a bowl on a dark matte colour (the brown wool) shows by its outline and its shadow
//and has little highlight to show by.
static const float ColourCueDimpleCos = 0.921061;
static const float ColourCueDimpleDepth = 0.16;

//The facets: the twenty faces of an icosahedron, as the planes at this distance from the centre along the ten
//axes through opposite faces (a dodecahedron's vertices). 0.90 of the radius lets neighbouring planes meet in a
//real edge (the edge's midpoint is 20.9 degrees off a face's centre, the flat reaches 25.8) and leaves the sphere
//standing at the twelve corners, so it is a rounded die and not a cut gem - and twenty small faces, never the
//frozen ball's six big ones.
static const float3 ColourCueDodecahedron[10] =
{
    float3(0.577350, 0.577350, 0.577350),
    float3(0.577350, 0.577350, -0.577350),
    float3(0.577350, -0.577350, 0.577350),
    float3(-0.577350, 0.577350, 0.577350),
    float3(0.000000, 0.356822, 0.934172),
    float3(0.000000, 0.356822, -0.934172),
    float3(0.356822, 0.934172, 0.000000),
    float3(0.356822, -0.934172, 0.000000),
    float3(0.934172, 0.000000, 0.356822),
    float3(-0.934172, 0.000000, 0.356822)
};
static const float ColourCueFacetPlane = 0.90;

//How much of the ambient the bottom of the deepest groove or dimple still sees: a hollow is what makes a carve
//read as a carve once the ball is small, after the highlight across it has gone to a pixel.
static const float ColourCueHollowShade = 0.55;

//The share of the radius carved away along this direction (0 = the sphere). On a per-draw uniform, so the branch
//cannot diverge; no derivative inside it.
float ColourCueCarve(float3 direction, float cue)
{
    float carve;

    [branch]
    if (cue < 1.5)
    {
        float groove = saturate(1 - abs(sin(3 * atan2(direction.z, direction.x))) / ColourCueGrooveWidth);
        carve = ColourCueRidgeDepth * groove * groove * smoothstep(0, ColourCuePoleFade, length(direction.xz));
    }
    else if (cue < 2.5)
    {
        float nearest = 0;

        [unroll]
        for (int i = 0; i < 6; i++) nearest = max(nearest, abs(dot(direction, ColourCueIcosahedron[i])));

        carve = ColourCueDimpleDepth * saturate((nearest - ColourCueDimpleCos) / (1 - ColourCueDimpleCos));
    }
    else if (cue < 3.5)
    {
        float nearest = 0;

        [unroll]
        for (int i = 0; i < 10; i++) nearest = max(nearest, abs(dot(direction, ColourCueDodecahedron[i])));

        //On the plane where it is nearer than the sphere: the point at this direction lies ON the plane
        carve = max(0, 1 - ColourCueFacetPlane / max(nearest, 1e-3));
    }
    else
    {
        float phase = 2 * atan2(direction.z, direction.x) + ColourCueTwist * direction.y;
        float groove = saturate(1 - abs(sin(phase)) / ColourCueGrooveWidth);
        carve = ColourCueTwistDepth * groove * groove * smoothstep(0, ColourCuePoleFade, length(direction.xz));
    }

    return carve;
}

//The carve as the pixel shader shades it: a height in world units on the sphere (the ball's object radius is its
//world radius, as PatternReliefStrength already assumes), eased out with the painted figures' fade, and the
//ambient the hollows keep. The facets are flat, not hollow, and keep all of it.
float ColourCueShape(float3 direction, float footprint, float radius, float cue, out float cavity)
{
    float carve = ColourCueCarve(direction, cue);
    float fade = saturate(ColourCueFadeStart - footprint * ColourCueFadeRate);

    cavity = cue > 2.5 && cue < 3.5 ? 1 : 1 - ColourCueHollowShade * saturate(carve / ColourCueDimpleDepth) * fade;

    return -carve * radius * fade;
}
