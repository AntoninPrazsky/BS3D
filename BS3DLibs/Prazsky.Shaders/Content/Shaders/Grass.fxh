//The grass's fine relief, which the meadow and the savanna share (#581): the two were line-for-line copies -
//the meadow began as one of the savanna - and both faults below were found in one and then again in the
//other, which is the case for one copy. What stays per scene is the TUNING: each declares its own
//GRASS_COMB_STRETCH and GRASS_FBM_GAIN (the same figures today, a lush lawn and a dry veld meant to be free
//to differ; the savanna's copy says what each answers), and its GrassReliefStrength/Frequency and
//WindDirection uniforms, before including this. Needs Noise.fxh (Fbm2Combed, Fbm2BandLimited, NoiseHash22). The
//blades seen from the side (GrassStrokes, below) are here too, with their own scale passed in: the meadow's. The
//savanna still combs its own along the bearing to the lens (Savanna.fx), the construction GrassStrokes warns
//against.
//
//A fine grass texture, band-limited against the footprint so it fades to smooth grass towards the horizon
//instead of aliasing.
//
//THREE OCTAVES OF GRADIENT NOISE, not the two crossed plane-wave sines this used to be. Two plane waves
//crossing ARE a lattice - that is what their interference is - and these crossed at 93.4 degrees, so the
//lattice was very nearly square and read in perspective as a field of diamonds across the middle distance
//(#117, filed against the savanna; the meadow's copy was found while fixing it). At GrassReliefFrequency 2
//the two periods were 3.14 and 1.75 world units, which is the scale the diamonds appeared at. It showed as
//strongly as it did because the field feeds PerturbNormalFromHeight, so it tilts the NORMAL and lands in the
//shading rather than merely in the colour. This is the failure Noise.fxh's own opening documents - "a sum of
//plane-wave sines keeps its planes however many terms it has" - and the one #86 removed from Mountain.fx's
//peaks. Octaves of gradient noise on a rotated domain have no planes to keep.
//
//⚠ GRASS SWAYS, IT DOES NOT TRAVEL (#276). This used to sample at `(xz + WindDirection * Time * 0.7)` — a
//flat 0.7 world units a second, for ever. At GrassReliefFrequency 2 a grass feature is half a unit, so the
//blades' own texture SLID ACROSS THE GROUND IT IS ROOTED IN at 1.4 features a second: measured on two meadow
//frames 0.6 s apart, essentially every pixel of the near field had changed. That is the crawl the owner
//reported, and no amount of retuning the speed fixes it, because a texture that translates is wallpaper
//however slowly it goes. (It also drifted UPWIND — adding to the sample position moves the pattern the other
//way — which nothing said and nothing could see, a sliding texture having no direction the eye can name.)
//
//What the wind does to grass is BEND it: the blades lean where a gust is passing and spring back behind it.
//So the lean is the gust field's own value, applied in the NOISE domain so it is a fixed fraction of a grass
//feature whatever GrassReliefFrequency is set to, and it is bounded by construction — the gust is clamped to
//[-1, 1], so the texture rocks about an eighth of a feature either side of where it is rooted and stays there.
static const float GRASS_SWAY_REACH = 0.16;

float GrassRelief(float2 xz, float footprint, float gust)
{
    float f = GrassReliefFrequency;
    float2 p = xz * f + WindDirection * (gust * GRASS_SWAY_REACH);

    //Combed along the wind, and the footprint scaled by the same factor the domain is — Fbm2BandLimited's
    //stated contract, which Fbm2Combed passes straight through.
    return Fbm2Combed(p, WindDirection, GRASS_COMB_STRETCH, 3, footprint * f) * GRASS_FBM_GAIN * GrassReliefStrength;
}

//BLADES SEEN FROM THE SIDE (#281, #1190). A standing blade is an upright stroke to a lens looking across the field. On
//the ground plane that is a fine noise stretched along the bearing TOWARDS THE LENS: the strokes run along the rays from
//the point under the lens, and a ray on the ground projects to a line through the nadir's vanishing point, upright on
//the screen. Its root stays where it grows, so the noise's phase must be fixed to the ground.
//
//⚠ NEVER TURN THE DOMAIN ABOUT A POINT FAR FROM THE PIXEL. Fbm2Combed(xz * f, lens.xz - xz) turned it about the WORLD
//ORIGIN by the pixel's bearing: its across coordinate f * xz . across is f * lens.xz . across, one value along each ray
//from the point under the lens. So every stroke was a whole ray from that point, fixed to the screen while the ground
//slid under it (the owner's "one pattern that does not move at all", #1190, plainest looking down), re-rolled f times a
//unit of the lens's travel across it (a new field every frame of a flight: the glitter), and packed f * |lens.xz| to
//the radian round that point, so how fine it was depended on how far the lens stood from the arena: in the intro, a
//hundred units out, hairlines far past the band limit. Fixed bearings blended instead were fixed to the ground but
//crossed at their step's angle, which a grazing view widens on the screen into a weave.
//
//So the domain is turned by the pixel's own bearing about ANCHORS, the corners of a ground grid a stroke's length apart
//(GRASS_STROKE_STRETCH over the frequency), each with its own offset into the noise, and the four round the pixel are
//blended (smooth bilinear weights, the spread kept: over the weights' own length). Each anchor's across coordinate is
//the ray's distance from that anchor, one value along the ray, so every stroke still runs exactly along the bearing;
//and it is measured from a point of the ground, so it is fixed to it. When the lens moves a stroke only turns with its
//bearing, about the point of it nearest its anchor, so the field drifts over the ground by about the spacing over the
//distance to the lens: the parallax of a blade whose stroke on the ground is a stroke long (a blade of height H seen
//from a height h lies H/h of the distance along the ground). A closer grid would drift less and chop the strokes
//shorter than they are.
static const float GRASS_STROKE_STRETCH = 24.0;    //a stroke is this many times longer towards the lens than across
static const float GRASS_STROKE_SCATTER = 61.0;    //how far into the noise each anchor's offset reaches, in noise cells

//And from overhead a blade shows its tip, not its flank, and the strokes' rays would all meet in the middle of the
//screen: they fade as the view of the ground turns steep, by the cosine between the ground's normal and the view (the
//sine of the view's height over flat ground). Whole up to GRASS_STROKE_FLANK_FULL (about 37 degrees over the ground,
//so every view across the field keeps them), gone at GRASS_STROKE_FLANK_GONE (about 55 degrees): a view straight down
//shows the relief alone, but for faint strokes leaning outward at the sides of a wide frame, where real blades do too.
static const float GRASS_STROKE_FLANK_FULL = 0.60;
static const float GRASS_STROKE_FLANK_GONE = 0.82;

//A pixel's size in the strokes' domain, per unit of frequency: the longer of the two screen steps (ddx and ddy of the
//ground position, taken by the caller outside any branch), each measured across the grain and along it over the
//stretch. Anisotropic, because the strokes run along the very direction a grazing view foreshortens: the field's own
//isotropic footprint is that long side, and faded the strokes by it where they still had pixels across them.
float GrassStrokeFootprint(float2 axis, float2 dpdx, float2 dpdy)
{
    float2 across = float2(-axis.y, axis.x);
    float2 stepX = float2(dot(dpdx, axis) / GRASS_STROKE_STRETCH, dot(dpdx, across));
    float2 stepY = float2(dot(dpdy, axis) / GRASS_STROKE_STRETCH, dot(dpdy, across));
    return max(length(stepX), length(stepY));
}

//One anchor's field: `cell` the anchor's grid cell, `scaled` the pixel's offset from the anchor in strokes (times the
//frequency), `axis` and `across` the pixel's bearing to the lens and its normal.
float GrassStrokeAnchor(float2 cell, float2 scaled, float2 axis, float2 across, float footprint)
{
    float2 p = float2(dot(scaled, axis) / GRASS_STROKE_STRETCH, dot(scaled, across)) + NoiseHash22(cell) * GRASS_STROKE_SCATTER;
    return Fbm2BandLimited(p, 2, footprint);
}

//The strokes, about -1..1 at the most: `frequency` strokes a world unit across (the coarser of two octaves), `toLens` the
//ground's direction to the lens (need not be normalised), `normal` and `toCamera` the ground's normal and the unit view
//for the flank, `dpdx` and `dpdy` the screen steps of xz. Behind a branch on whether any of it can show (the flank, and
//the coarser octave's band limit): nothing inside takes a derivative, and a frame looking down, or far, skips the four
//anchors' eight noises.
float GrassStrokes(float2 xz, float2 toLens, float3 normal, float3 toCamera, float frequency, float2 dpdx, float2 dpdy)
{
    float flank = 1.0 - smoothstep(GRASS_STROKE_FLANK_FULL, GRASS_STROKE_FLANK_GONE, dot(normal, toCamera));

    //Under the lens the bearing is undefined; the flank has faded the strokes out long before
    float distance = length(toLens);
    float2 axis = distance > 1e-4 ? toLens / distance : float2(1.0, 0.0);
    float2 across = float2(-axis.y, axis.x);
    //The domain is turned about an anchor up to a spacing and a half away, so a pixel's step also turns it by that
    //much over the distance to the lens: the bearing's own spin, which the screen steps alone do not see
    float spacing = GRASS_STROKE_STRETCH / frequency;
    float footprint = GrassStrokeFootprint(axis, dpdx, dpdy) * frequency * (1.0 + 1.5 * spacing / max(distance, 1e-4));

    float strokes = 0.0;
    [branch]
    if (flank * saturate(1.0 - 2.0 * footprint) > 0.0)
    {
        float2 grid = xz / spacing - 0.5;
        float2 cell = floor(grid);
        float2 t = grid - cell;
        //The pixel's offset from the cell's first anchor, in strokes (a spacing is GRASS_STROKE_STRETCH of them); the
        //other three anchors are a spacing over
        float2 scaled = t * GRASS_STROKE_STRETCH;
        float n00 = GrassStrokeAnchor(cell, scaled, axis, across, footprint);
        float n10 = GrassStrokeAnchor(cell + float2(1.0, 0.0), scaled - float2(GRASS_STROKE_STRETCH, 0.0), axis, across, footprint);
        float n01 = GrassStrokeAnchor(cell + float2(0.0, 1.0), scaled - float2(0.0, GRASS_STROKE_STRETCH), axis, across, footprint);
        float n11 = GrassStrokeAnchor(cell + float2(1.0, 1.0), scaled - GRASS_STROKE_STRETCH, axis, across, footprint);
        t = t * t * (3.0 - 2.0 * t);
        float2 w = float2(1.0 - t.x, t.x);
        float2 v = float2(1.0 - t.y, t.y);
        float sum = (n00 * w.x + n10 * w.y) * v.x + (n01 * w.x + n11 * w.y) * v.y;
        float spread = dot(w, w) * dot(v, v);
        strokes = flank * sum * rsqrt(spread);
    }
    return strokes;
}
