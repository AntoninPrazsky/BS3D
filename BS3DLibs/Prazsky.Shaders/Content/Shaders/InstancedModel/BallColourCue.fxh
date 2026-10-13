//===================================================================================================
//THE COLOUR CUE (#737), THE FIGURES. A player could not tell two wool balls apart by their colour alone, so
//every colour carries a figure of its own beside its hue, PAINTED on the round ball. There are four sets of
//figures, each giving all thirteen colours one, and every ball style draws one set (the owner's ruling,
//2026-10-12); which figure draws is a per-draw description, not code (Prazsky.BS3D.ColourCues holds the sets, the
//set each style draws and the inks). Prazsky.BS3D.GameStructure.ColourCueField is this file's field ported line
//for line to C#, for the HUD's discs and the tests: a figure changed here is changed there in the same commit.
//
//This file is the figures and nothing else: functions, one struct and static constants, NO UNIFORMS, so
//including it moves nothing in the constant buffer (the rule in InstancedModel.fx). The uniforms that describe a
//draw's figure, and the programs that paint it, are BallColourCueTechniques.fxh's, last in the effect. Every ball
//style's body (PatternShade, BubbleShade, MarbleShade, WoolShade, MetalShade, IceShade, GemShade, PlasmaShade,
//LavaShade, PorcelainShade) takes its switch as a literal, so the shipped techniques, which colourcue=none draws,
//compile exactly as they did before the cue existed.
//
//A FIGURE IS ONE OF FOUR KINDS, each a signed field on the unit sphere, positive inside the figure, read in a
//frame the draw supplies (so one kind turned differently is another figure):
//
//    1  bands     round the frame's Y: how many, how wide, wavy, hollow (a double line) or dashed
//    2  spots     at the vertices of a regular solid (4, 6, 8, 12 or 20 of them), solid or rings, or two sizes
//    3  checks    the sign of sin sin sin, about three cells across the ball
//    4  emblems   one printed symbol on each face of the frame's cube, like the pips of a card
//
//EVERYTHING IS IN OBJECT SPACE, so a figure turns with its ball (the rolling cue comes free) and every ball of one
//colour carries the same one: the figure names the colour, not the ball.
//
//WHAT NO FIGURE MAY LOOK LIKE. A special kind - the bomb's two slanted glowing seams (a "+" face on), the stone's
//broken outline and cracks, the frozen ball's six flat faces, the heavy casting's soft patches and mould flash, the
//gravity well's spiral and its concentric rings (a bullseye), the zap's forked bolts, the clear glass's cast seam,
//the acid's glowing lower half, the infectious ball's one lumpy crust, the buckshot's clump of loose pellets, the
//wildcard's two colours swirled together, the dead weight's dither (its pixels cut away) - or the vinyl's gores,
//which are the rotation cue. So no figure spirals, zigzags or runs along meridians, no two bands cross round the
//ball (the mine's seams: there is no grid), no figure sits on one side of the ball (the acid's fill, the crust):
//each covers it evenly, round an axis or at a solid's vertices; a figure is the ball's own colour in two shades,
//never a second colour (the wildcard), painted on the round ball, never an outline of its own (the buckshot); none
//glows brighter than its ball as a whole (it glows in its own ink, and Prazsky.BS3D.ColourCues balances the two
//inks so that the ball's average colour stays its plain one), and none is a thin dark crack. The smallest spots
//(the spots set's 9-degree dots) come nearest the dither: two or three pixels on a 23-pixel ball, they still read as
//dots of the ball's darker shade, where a dead ball shows what is behind it.
//===================================================================================================

//One draw's figure, as BallColourCueTechniques.fxh reads it from its uniforms. The kinds' parameters:
//
//    a = (kind, ...)       bands:   (1, count, phase, share of a period inside a band)
//                          spots:   (2, solid, cos of the spot's radius, cos of a ring's inner radius or 2)
//                          checks:  (3, frequency, 0, 0)
//                          emblems: (4, shape, size in the face's gnomonic units, 0)
//    b                     bands:   (wave amplitude, waves round the ball, hollow share or 0, dashes or 0)
//                          spots:   (second solid or 0, cos of its spots' radius, 0, 0)
//    c                     bands:   (share of a dash period drawn, 0, 0, coverage)
//                          others:  (0, 0, 0, coverage)
//
//c.w is the share of the ball the play camera sees the figure cover (Prazsky.BS3D.ColourCues.Figure says how it
//is measured): where a ball is too small to carry its figure, the figure fades to this share of its ink rather
//than to nothing, so a far ball still averages its plain colour.
//
//inkScales is the same two inks as SCALES on the plain colour (figure, ground), for the styles whose colour is a
//factor in something else rather than a body to dye: the metal's reflectance, the plasma's discharge, the lava's
//molten seams. For every colour but black they are the inks' own ratios to the plain colour; black's figure ink is
//a dim grey ADDED, its ratio to a near-zero black about 24, so its scales are a darker ink's balance instead.
struct ColourCuePaint
{
    float4 a;
    float4 b;
    float4 c;
    float3x3 frame;
    float3 figureInk;
    float3 groundInk;
    float2 inkScales;
};

//The regular solids' vertices, as axes (both ends of each are a vertex) except the tetrahedron's, which has no
//opposite vertices: the most even ways to put 4, 8, 12 and 20 points on a ball. Six is the frame's own axes.
static const float3 ColourCueTetrahedron[4] =
{
    float3(0.577350, 0.577350, 0.577350),
    float3(0.577350, -0.577350, -0.577350),
    float3(-0.577350, 0.577350, -0.577350),
    float3(-0.577350, -0.577350, 0.577350)
};

static const float3 ColourCueCube[4] =
{
    float3(0.577350, 0.577350, 0.577350),
    float3(0.577350, 0.577350, -0.577350),
    float3(0.577350, -0.577350, 0.577350),
    float3(-0.577350, 0.577350, 0.577350)
};

static const float3 ColourCueIcosahedron[6] =
{
    float3(0.000000, 0.525731, 0.850651),
    float3(0.000000, 0.525731, -0.850651),
    float3(0.525731, 0.850651, 0.000000),
    float3(0.525731, -0.850651, 0.000000),
    float3(0.850651, 0.000000, 0.525731),
    float3(-0.850651, 0.000000, 0.525731)
};

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

//Below about seven target pixels of radius a figure cannot be drawn, only aliased, so it is eased out on the
//pixel footprint (the vinyl's yardstick, surface per pixel over the radius): whole up to 0.2, gone by 0.4. A
//ball in play is 0.07-0.15 at the desktop's and the laptop's resolutions, so this is the overview's far balls
//and nothing a player aims at.
static const float ColourCueFadeStart = 2.0;
static const float ColourCueFadeRate = 5.0;

//The cosine of the angle to the nearest vertex of a solid, `solid` its vertex count (-4 the tetrahedron turned
//inside out, the other four corners of a cube). On a per-draw uniform, so the branches cannot diverge.
float ColourCueNearest(float3 q, float solid)
{
    float nearest = -1;

    [branch]
    if (solid < -3.5)
    {
        [unroll]
        for (int i = 0; i < 4; i++) nearest = max(nearest, -dot(q, ColourCueTetrahedron[i]));
    }
    else if (solid < 4.5)
    {
        [unroll]
        for (int i = 0; i < 4; i++) nearest = max(nearest, dot(q, ColourCueTetrahedron[i]));
    }
    else if (solid < 6.5)
    {
        float3 m = abs(q);
        nearest = max(m.x, max(m.y, m.z));
    }
    else if (solid < 8.5)
    {
        [unroll]
        for (int i = 0; i < 4; i++) nearest = max(nearest, abs(dot(q, ColourCueCube[i])));
    }
    else if (solid < 12.5)
    {
        [unroll]
        for (int i = 0; i < 6; i++) nearest = max(nearest, abs(dot(q, ColourCueIcosahedron[i])));
    }
    else
    {
        [unroll]
        for (int i = 0; i < 10; i++) nearest = max(nearest, abs(dot(q, ColourCueDodecahedron[i])));
    }

    return nearest;
}

//One family of bands round an axis: `along` the height along it, `around` the angle round it. Every count of
//waves and dashes is a whole number, so sin and cos of it stay continuous across atan2's cut.
float ColourCueBandFamily(float along, float around, float4 a, float4 b, float4 c)
{
    float t = a.y * along + a.z + b.x * sin(b.y * around);
    float wave = cos(3.14159265 * t);
    float field = wave - cos(3.14159265 * a.w);

    //A hollow band is its two edges: a double line
    if (b.z > 0) field = min(field, cos(3.14159265 * b.z) - wave);

    //A dashed one is cut round the ball
    if (b.w > 0) field = min(field, cos(b.w * around) - cos(3.14159265 * c.x));

    return field;
}

//A heart, point down: a square stood on its corner with a disc on each upper side, as a heart is drawn by hand
float ColourCueHeart(float2 p)
{
    p.y -= 0.08;
    float corner = (0.672 - (abs(p.x) + abs(p.y + 0.15))) * 0.707107;
    float lobe = 0.475 - length(float2(abs(p.x), p.y) - float2(0.336, 0.186));

    return max(corner, lobe);
}

//A spade's or a club's stem: a short foot widening downwards
float ColourCueStem(float2 p)
{
    return min(0.06 + 0.25 * (-0.30 - p.y) - abs(p.x), min(p.y + 0.90, -0.20 - p.y));
}

//The emblems' signed fields, in the face's units over the emblem's size, positive inside. Each is the plain
//distance algebra of its shape (discs, half-planes, a polar star), sized to fill about the same share of its
//face, so that an emblem is told by its outline, not by how much of the ball it inks. The numbers are the shapes'.
float ColourCueEmblem(float2 p, float shape)
{
    float field;

    [branch]
    if (shape < 1.5)
    {
        //1 a five-pointed star, a point up. 1 along a point and 0 in the notch between two, squared so the arms
        //taper like a star's and do not bulge; five points, so the period also hides atan2's cut
        float spoke = abs(frac((atan2(p.y, p.x) - 1.5707963) * (5.0 / 6.2831853)) * 2 - 1);
        field = lerp(0.44, 1.08, spoke * spoke) - length(p);
    }
    else if (shape < 2.5)
    {
        //2 a heart
        field = ColourCueHeart(p);
    }
    else if (shape < 3.5)
    {
        //3 a spade: a heart upside down on a stem
        field = max(ColourCueHeart(float2(p.x, 0.15 - p.y) * 1.2), ColourCueStem(p));
    }
    else if (shape < 4.5)
    {
        //4 a club: three discs and a heart between them, on a stem
        float leaves = max(0.34 - length(p - float2(0, 0.40)),
            max(0.34 - length(p - float2(0.40, -0.08)), 0.34 - length(p - float2(-0.40, -0.08))));
        field = max(max(leaves, 0.20 - length(p - float2(0, 0.08))), ColourCueStem(p));
    }
    else if (shape < 5.5)
    {
        //5 a diamond, the card's lozenge
        field = 1 - (abs(p.x) / 0.66 + abs(p.y));
    }
    else if (shape < 6.5)
    {
        //6 a square
        field = 0.62 - max(abs(p.x), abs(p.y));
    }
    else if (shape < 7.5)
    {
        //7 a ring: one, never two in one another (two concentric are the gravity well's bullseye)
        float d = length(p);
        field = min(0.85 - d, d - 0.50);
    }
    else if (shape < 8.5)
    {
        //8 a triangle, point up
        field = min(0.45 - (abs(p.x) * 0.866025 + p.y * 0.5), p.y + 0.45);
    }
    else if (shape < 9.5)
    {
        //9 a crescent moon: a disc with a disc taken out of it
        field = min(0.85 - length(p), length(p - float2(0.42, 0.18)) - 0.68);
    }
    else if (shape < 10.5)
    {
        //10 a drop: a disc drawn up to a point by the two lines from the point that touch it
        field = max(0.55 - length(p - float2(0, -0.25)),
            min(-(abs(p.x) * 0.888 + (p.y - 0.95) * 0.458), p.y + 0.25));
    }
    else if (shape < 11.5)
    {
        //11 a flower: four petals round a heart
        float petals = max(max(0.40 - length(p - float2(0.42, 0)), 0.40 - length(p + float2(0.42, 0))),
            max(0.40 - length(p - float2(0, 0.42)), 0.40 - length(p + float2(0, 0.42))));
        field = max(petals, 0.30 - length(p));
    }
    else if (shape < 12.5)
    {
        //12 a chevron pointing up
        field = min(0.28 - abs(p.y - 0.35 + abs(p.x) * 0.9), 0.85 - abs(p.x));
    }
    else if (shape < 13.5)
    {
        //13 a saltire, an X. Square to the face it would be a "+", the bomb's two seams seen face on
        float2 r = float2(p.x + p.y, p.x - p.y) * 0.707107;
        field = max(min(0.24 - abs(r.x), 0.95 - abs(r.y)), min(0.24 - abs(r.y), 0.95 - abs(r.x)));
    }
    else
    {
        //14 a hexagon
        float2 m = abs(p);
        field = 0.75 - max(m.x * 0.866025 + m.y * 0.5, m.y);
    }

    return field;
}

//The figure's signed field, positive inside it. No derivative inside the branches - they are on a per-draw
//uniform and cannot diverge, but the antialiasing that needs the field's screen gradient is taken outside them.
float ColourCueMotifField(float3 direction, ColourCuePaint cue)
{
    float3 q = mul(cue.frame, direction);
    float field;

    [branch]
    if (cue.a.x < 1.5)
    {
        field = ColourCueBandFamily(q.y, atan2(q.z, q.x), cue.a, cue.b, cue.c);
    }
    else if (cue.a.x < 2.5)
    {
        float nearest = ColourCueNearest(q, cue.a.y);

        //A spot, or a ring where an inner radius is given (its cosine is 2, out of reach, where none is)
        field = min(nearest - cue.a.z, cue.a.w - nearest);

        //A second solid's spots, the other size of a two-size pattern
        [branch]
        if (cue.b.x != 0) field = max(field, ColourCueNearest(q, cue.b.x) - cue.b.y);
    }
    else if (cue.a.x < 3.5)
    {
        float3 cell = q * cue.a.y;
        field = sin(cell.x) * sin(cell.y) * sin(cell.z);
    }
    else
    {
        //The face of the frame's cube this direction is on, and where on it (a gnomonic projection): a select, not
        //a branch, so the quad stays together. On the four side faces the emblem stands up along the frame's Y.
        float3 m = abs(q);
        float2 uv = m.x >= max(m.y, m.z) ? float2(q.z, q.y) / m.x
            : (m.y >= m.z ? float2(q.x, q.z) / m.y : float2(q.x, q.y) / m.z);

        field = ColourCueEmblem(uv / cue.a.z, cue.a.y);
    }

    return field;
}

//How much of this pixel the painted figure covers, 0..1: a one-pixel soft edge, and on a ball too small to carry
//the figure, the figure's own share of the ball (cue.c.w), so that the two inks mix to the ball's plain colour.
//
//An emblem's field JUMPS where two faces of its cube meet, each face having its own projection, and the soft edge's
//width, taken over the pixel quad, would span the jump: a faint line of ink (up to a third of the figure's) along
//parts of the cube's edges, away from any emblem (the chevron, drop, triangle, star, spade, club and diamond leave
//one). No emblem reaches the edge of its face, so a quad that straddles two faces is outside every emblem and draws
//the ground. The face is the field's own select, taken here because a derivative may not sit in its branches.
float ColourCueMotif(float3 direction, float footprint, ColourCuePaint cue)
{
    float field = ColourCueMotifField(direction, cue);
    float fade = saturate(ColourCueFadeStart - footprint * ColourCueFadeRate);
    float figure = AntialiasedStep(0, field);

    float3 m = abs(mul(cue.frame, direction));
    float face = m.x >= max(m.y, m.z) ? 0 : (m.y >= m.z ? 1 : 2);
    figure = cue.a.x > 3.5 && fwidth(face) > 0 ? 0 : figure;

    return lerp(cue.c.w, figure, fade);
}

//The ink at this pixel: the figure's where it covers, the ground's elsewhere
float3 ColourCueDye(float figure, ColourCuePaint cue)
{
    return lerp(cue.groundInk, cue.figureInk, figure);
}

//The same ink as a SCALE on the plain colour (inkScales: the figure's, the ground's), for a style whose colour is
//not a body to be dyed but a factor in something else: the metal's reflectance, the plasma's discharge, the lava's
//molten seams. The scales average to 1 over the ball as the inks average to the plain colour, black's included
//(Prazsky.BS3D.ColourCues.Inks), so the ball as a whole reflects, burns or glows what it did.
float ColourCueInkScale(float figure, ColourCuePaint cue)
{
    return lerp(cue.inkScales.y, cue.inkScales.x, figure);
}
