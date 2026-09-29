//The shared starfield: the cube-face star lattice, its helpers and the uniforms that drive it — one copy
//for the two skies with no atmosphere in them, Space.fx and Moon.fx. It moved here verbatim when the Moon
//arrived (#125): the two scenes want an identical starfield (same lattice, same glare-threshold discipline,
//same footprint-on-the-direction sizing), and a second copy would have been the duplicated-classification
//mistake #75 spent a whole issue ending, in shader form.
//
//Like Clouds.fxh and Noise.fxh it is a header, not an .mgcb entry; editing it rebuilds every .fx that
//includes it. The uniforms declared here (StarCellScale/Chance/Peak, StarSpread, StarFalloff, the spike
//pair, SupersampleFactor) become each including effect's own parameters under the same names, so one C#
//push routine serves both effects.
//
//Everything here is written in LINEAR RADIANCE and everything SMALL is deliberately kept under
//GLARE_THRESHOLD (0.55 on luminance). That is not timidity: the glare's bright pass samples the
//supersampled scene target far too sparsely to catch a one-pixel star reliably, and a star that glares on
//some frames and not others reads as a fault, not as a star. The brightest stars therefore carry their
//diffraction spikes in the shader, where they are stable. See Space.fx's header for the discipline in full.

//How many scene-target texels make one output pixel. The scene is rendered supersampled and box-filtered on
//resolve, so a star drawn one TEXEL across would come out four times dimmer at 2x than at 1x - the same star
//on two quality settings. Sized in OUTPUT pixels instead, it reads the same on every setting, and since the
//factor is never below 1 it is also never below the texel size that would alias.
float SupersampleFactor;

//--- Stars -------------------------------------------------------------------------------------------
//Three layers, coarse to fine. At most one star per cell of a cube-face lattice, jittered over the WHOLE of its cell
//(since #674: the box that used to hold it clear of the cell wall was itself a lattice, measured at 8.83 px on the
//axes) and found by looking up the (at most four) cells its light can come from, so a star may stand on a cell wall and its light
//crosses it. Cell scale is cells per unit of cube-face uv.
float StarCellScale[3];
//Fraction of cells carrying a star, as a DENSITY ON THE SKY rather than per cell: each cell's roll is
//weighted by the solid angle it actually covers (STAR_DENSITY_GAIN in StarLayer), because a chart cell at a
//cube corner covers a fifth of the sky one at a face centre does. Capped at SpaceStarsConfig.MaxChance.
float StarChance[3];
float StarPeak[3];        //peak linear radiance of the brightest star of the layer
float StarSpread;         //core radius in OUTPUT pixels; under ~0.4 the field starts to crawl

//Steepness of the brightness law: brightness = pow(hash, StarFalloff). Real star counts climb steeply
//towards the faint end, and a field of evenly bright dots reads as noise rather than as a sky.
float StarFalloff;

//Fraction of the layer's peak past which a star also gets drawn diffraction spikes, and how far they reach
//(in units of the star's own core radius). Only the coarse layer draws them - its cells are wide enough that an arm,
//held to 0.34 of a cell (armMargin below), stays inside the cells StarLayer looks up.
float StarSpikeThreshold;
float StarSpikeLength;

//No sin anywhere: a sine-based hash is where two implementations of the same field part company, and this
//project keeps that rule even where only one implementation exists.
float3 Hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);

    return frac((p.xxy + p.yxx) * p.zyx);
}

//A stable orthonormal frame about an axis. The reference vector is swapped near the pole so the cross
//product never degenerates - the axes are config values and nothing stops one being straight up.
void BuildFrame(float3 axis, out float3 right, out float3 forward)
{
    float3 reference = abs(axis.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0);
    right = normalize(cross(reference, axis));
    forward = cross(axis, right);
}

//Direction to a cube-face chart: xy in [-1,1] on the face, z a face id so two faces never draw the same
//stars. The chart is uv = tan(angle) about the face centre, which is what CubeJacobian below undoes.
float3 CubeChart(float3 dir)
{
    float3 a = abs(dir);

    if (a.x >= a.y && a.x >= a.z) return float3(dir.zy / a.x, dir.x > 0.0 ? 0.5 : 1.5);
    if (a.y >= a.z) return float3(dir.xz / a.y, dir.y > 0.0 ? 2.5 : 3.5);

    return float3(dir.xy / a.z, dir.z > 0.0 ? 4.5 : 5.5);
}

//How many chart units one radian covers here. uv = tan(theta), so duv/dtheta = sec^2 = 1 + uv^2 - and
//because it depends only on |uv|, which is 1 at every face edge, it is continuous ACROSS the seams. That
//continuity is the whole reason the pixel footprint is measured on the direction and converted here
//instead of being taken as fwidth() of the chart coordinate, which jumps at every one of the twelve edges
//and would ring them with wrongly sized stars.
float CubeJacobian(float2 chartUv)
{
    return 1.0 + dot(chartUv, chartUv);
}

//A cheap blackbody-ish ramp, normalized so every class carries about the same luminance and the hash
//decides hue rather than brightness. 0 = the blue-white of an O/B star, 1 = the orange-red of an M dwarf.
float3 StarTint(float temperature)
{
    float3 hot = float3(0.62, 0.75, 1.00);
    float3 white = float3(1.00, 0.97, 0.94);
    float3 cool = float3(1.00, 0.62, 0.36);

    return temperature < 0.5
        ? lerp(hot, white, temperature * 2.0)
        : lerp(white, cool, (temperature - 0.5) * 2.0);
}

//Radiance under which a cut in a star's profile cannot be seen, and so the distance the margin below has
//to hold clear. One code of an 8-bit back buffer is about 3e-4 of linear radiance at the bottom of the sRGB
//curve, so this is a third of a code - under the dither the sky is already broken up with.
static const float STAR_CUT = 1e-4;

//The level a spike arm has fallen to by the time it reaches the end of its reach - its cell wall until #674, and since
//then the distance armMargin puts it at, 0.34 of a cell from the star: both branches of `margin` below land that end at
//exactly 2.5 e-folding lengths (uncapped by construction, capped because reach = margin / 2.5), so every arm used to
//terminate at exp(-2.5) = 8.21 % of its amplitude, in a straight cut.
//The taper below subtracts this floor off and renormalises, so the arm reaches exactly zero AT the wall
//instead of being cut dead at 8.21 % there - a straight cut is a square step the eye reads as the lattice
//drawn out, where a taper to zero is not (#148).
static const float STAR_SPIKE_FLOOR = exp(-2.5);

//A cube face is 4 square chart units and covers 4*pi/6 steradians, so weighting the existence roll by the
//solid angle a cell covers (below) and nothing else would leave the sky with 52.4 % of the stars it had.
//This puts the count back: 4 / (4*pi/6). The field keeps its density, it just stops piling up at the
//corners. Face centres therefore roll against 1.91 * chance, which is why the chances are capped at
//SpaceStarsConfig.MaxChance - above that the correction would clip exactly where it has most to give.
static const float STAR_DENSITY_GAIN = 1.90986;

//The most core radii any star of any layer can reach before it is under STAR_CUT: sqrt(log(peak / STAR_CUT)) with the
//brightest peak a layer may have, which the glare discipline holds under GLARE_THRESHOLD (0.55): sqrt(log(0.6 / 1e-4))
//is 2.95, so 3 is a bound - for a peak up to 0.81 (sqrt(log(0.81 / 1e-4)) = 3.0); SpaceStarsConfig does not clamp a
//layer's Peak, and above that the far tail is cut a little short of where it falls under STAR_CUT. A constant rather than the figure worked out per pixel from each layer's own peak - a log and
//a sqrt for every pixel of three layers cost about 2 ms a frame on the laptop at 1600 x 900 and 2x supersampling,
//to save reaching a little less than the bound where the layer is dimmer.
static const float STAR_MAX_REACH_RADII = 3.0;

//One CELL of one layer (#674 split it out of StarLayer, which now judges the four nearest and takes the max): the
//star is jittered over the whole cell, held its own radius clear only of a cube face's own edge, and drawn where the
//pixel is within its reach - which is at most half a cell, the promise the four-cell lookup is built on.
//
//The core is sized in OUTPUT pixels rather than in texels or in radians, which is what keeps it identical
//on every supersampling setting and always at least one texel across - a star drawn smaller than a texel
//crawls and scintillates as the camera turns, and in vacuum a star is the one thing that must NOT twinkle.
//
//⚠ THE NEXT PARAGRAPH IS HISTORY SINCE #674, kept because it says why the lattice showed: the box is gone (see `margin`
//and `lower`/`upper` below), the margin now holds a star clear of the CUBE FACE's edge only, and the four-cell lookup
//in StarLayer is what lets a star stand on a cell wall.
//
//That margin was the whole of #88 ("the stars read as arranged in a grid"). It was subtracted from BOTH ends
//of the cell, so a star may only land in the middle `1 - 2 * margin` of it, and under about half a cell of
//that box the spacing between neighbours stops looking random and the lattice shows through. The margin is
//in cell units while the star is sized in pixels, so the box closes as the cells get smaller on screen -
//which is why this was reported from a laptop and is invisible at 4K, and why both figures below are
//derived from what a star actually reaches rather than assumed.
float3 StarInCell(float2 cell, float2 p, float3 chart, float pixelAngle, float scale, float chance, float peak, bool spikes,
    float quickReach)
{
    //sec^2(theta) off the face centre, and it is wanted three times: once to size the star in chart units,
    //once to undo the chart's own anisotropy where the profile is measured (#87), and once in `axis` below
    //to measure that anisotropy one chart axis at a time for the margin and the spike arms (#148)
    float jacobian = CubeJacobian(chart.xy);

    //Per-axis angular scaling (#148): a chart step along X turns the view sqrt(1+chart.y^2) further per unit
    //than the same step at a face centre does - and symmetrically for Y - so drawing in raw chart axes
    //stretches everything tangentially by up to sec(theta), 1.41:1 at the middle of a face edge and 1.73:1
    //at a corner. Componentwise this is (jacobian - chart.xy^2) = (1+cy^2, 1+cx^2): the jacobian that
    //`core` and the spike reach already carry cancels against the jacobian in the chart-step-to-angle
    //conversion, leaving exactly this factor. Measured on the same pixel's chart as #87's closed form, and
    //reduces to (1,1) - bit for bit no change - at a face centre. Two sqrts, no basis, no normalize: the
    //full corrected VECTOR is only needed for arms that are not axis-aligned, and these are.
    float2 axis = sqrt(jacobian - chart.xy * chart.xy);

    float pixelCells = pixelAngle * jacobian * scale;

    //The layer goes into the seed as well as the cell and the face. The three layers run at different scales,
    //so cell (5,7) of the coarse layer and cell (5,7) of the fine one are unrelated patches of sky - but
    //without this they share their existence roll, their jitter, their magnitude and their colour, which is a
    //correlation nobody would ever see and every reason to not have.
    float3 seed = float3(cell, chart.z + scale);

    //THE CELLS ARE UNIFORM IN THE CHART, AND THE CHART IS NOT UNIFORM ON THE SKY. uv = tan(theta), so one
    //square chart unit covers jacobian^-1.5 steradians - and left alone that is 5.196x as many stars per
    //steradian at a cube CORNER as at a face centre, 2.83x along the middle of an edge. Measured before the
    //fix by pointing the lens straight down (1,1,1): 4.05x the lit pixels of a patch 35 degrees off it.
    //
    //The eye does not read that as anisotropy. It reads it as eight knots of stars with bands drawn between
    //them, which is to say AS THE CUBE - and this is why #87, #88 and #148 could all correct the star's
    //SHAPE and leave the report standing. Density is smooth and continuous across every seam, so a search
    //for a discontinuity cannot find it; the artifact is a gradient whose ridges merely happen to run along
    //the twelve edges. The star COUNT needed the correction, not the star.
    //
    //Taken at the CELL CENTRE and not at the pixel, so the roll is one answer for the whole cell - graded
    //per pixel it would cut stars in half down an invisible contour. rsqrt(j^3) rather than pow(j, -1.5):
    //same value, one rsqrt instead of a log and an exp.
    float2 cellCentre = (cell + 0.5) / scale;
    float cellJacobian = CubeJacobian(cellCentre);
    float density = STAR_DENSITY_GAIN * rsqrt(cellJacobian * cellJacobian * cellJacobian);

    float3 rollA = Hash33(seed);
    if (rollA.x > chance * density) return 0.0;

    //THE CHEAP REJECT (#674). Four cells are judged for every pixel of every layer now, where one was, and most of them
    //hold a star that is nowhere near this pixel: a star that exists is far from a given pixel, in a cell that
    //holds it, about nine times in ten. The first cut ran the whole of the star (a second hash, a log, an exp, a
    //sqrt, the profile) for each of them and cost 7.3 ms a frame on the laptop, +33 % on the space scene. Everything
    //that decides WHERE the star stands is the first roll and the cell - the margin against the cube face's edge
    //is the one exception, so a cell on a face edge skips this - and how FAR it can reach is bounded by the
    //brightest a star can be: the profile is exp(-d^2 / core^2) with d^2 at least the chart distance squared
    //(the jacobian term only adds to it), so a pixel further than core * reachRadii of a full-magnitude star is under
    //STAR_CUT of every star of the layer. The spikes' arms run along the axes to at most the 0.34 of a cell their
    //reach is held to, so that layer tests the arms' box instead of a circle.
    bool onFaceEdge = cell.x <= -scale || cell.y <= -scale || cell.x >= scale - 1.0 || cell.y >= scale - 1.0;
    float2 standing = cell + rollA.yz;

    if (!onFaceEdge)
    {
        float2 toStar = p - standing;

        if (spikes ? max(abs(toStar.x), abs(toStar.y)) > quickReach : dot(toStar, toStar) > quickReach * quickReach)
            return 0.0;
    }

    float3 rollB = Hash33(seed + 19.73);

    //Brightness climbs steeply towards the faint end, so the layer is thousands of faint stars with a
    //handful of obvious ones rather than a wall of identical dots. Hotter stars are the brighter ones,
    //which is both true and what makes the bright few read blue-white against a warmer field.
    //
    //Spelled out as exp(log()) rather than as pow(), which is what pow compiles to anyway, because the
    //margin below wants log(magnitude) as well and this way it shares the one logarithm.
    float logRoll = log(max(rollB.x, 1e-6));
    float magnitude = exp(StarFalloff * logRoll);

    float3 tint = StarTint(saturate(rollB.y * (1.1 - 0.75 * magnitude)));

    //A brighter star is drawn a little wider as well as a little brighter. Physically a star is a point
    //whatever its magnitude, but no optics resolve it as one - a bright source spreads further in an eye,
    //a lens and a sensor alike, and a field where every star is the same width reads as a texture of dots
    //with some of them turned up. The floor is what keeps the smallest of them from crawling.
    float core = max(StarSpread * SupersampleFactor, 0.62) * pixelCells * (1.0 + magnitude * 0.9);

    //Whether THIS star draws spikes, decided here rather than at the spike block below, because the margin
    //turns on it: a spiked star needs 5.8x the room a plain one does, and only 4.2% of the coarse layer is
    //over the threshold. Charging the layer's flag to all of it - which is what this did - capped 96.8% of
    //the coarse layer's stars at 1366x768 and 46.9% at 1920x1080 on the menu's 60-degree camera, i.e. held
    //almost every bright star in the middle third of its cell. Per star, that is 1.4% and 1.6%.
    bool drawSpikes = spikes && magnitude > StarSpikeThreshold;

    //How far the gaussian reaches before it is under STAR_CUT: exp(-r^2) * peak * magnitude = STAR_CUT.
    //This was a flat three radii, the same distance for a 0.50-peak star at full magnitude and for a faint
    //one at a hundredth of it - and on the fine layer, whose cells are only about ten pixels across at
    //1920x1080, three radii of a star that peaks at 0.16 is a third of the cell. Solving it per star costs
    //one sqrt over the logarithm the magnitude already took, and takes the typical case to about 2.2 radii.
    float reachRadii = sqrt(max(log(peak / STAR_CUT) + StarFalloff * logRoll, 1.0));

    //Held clear of the cube FACE's edge (and of half a cell, the lookup's promise) by however far this star actually
    //reaches - until #674 it was held clear of every cell wall, so nothing was ever clipped by the boundary of the one
    //cell being sampled; the rest of this paragraph is written for that. A SPIKED star throws arms StarSpikeLength core radii out and
    //needs far more room - at three radii its arms were cut dead straight where they crossed into the next
    //cell, at about two thirds of their brightness. PER AXIS since #148: the reach is an angular distance
    //and the cell wall is a chart one, so each axis divides by its own `axis` factor - the same conversion
    //the profile measures - which both keeps every profile short of the wall and makes this smaller
    //tangentially than the isotropic figure it replaced, buying back jitter room where the cap binds.
    //
    //⚠ SINCE #674 THIS MARGIN NO LONGER KEEPS A STAR OFF ITS CELL'S EDGE, ONLY OFF THE CUBE FACE'S: the star is
    //jittered over the WHOLE cell and StarLayer looks the four nearest cells up, so a star may stand on a cell
    //boundary and its light crosses into the next cell's pixels, where the neighbour lookup finds it. What is
    //left of the margin is (a) the face edge, where there is no neighbouring cell on this chart and the star's
    //light would be cut at the seam, and (b) the cap of half a cell, which is what four cells can promise: a
    //pixel is served by the cells whose centres are within half a cell of it, so a star reaching further than
    //that could be missed by the pixel it should light.
    float2 margin = min((drawSpikes ? core * StarSpikeLength * 2.5 : core * reachRadii) / axis, 0.5);

    //Jittered over the whole cell - anywhere in it, uniformly - except against a cube face's own edge, where the
    //margin holds the star clear of the seam. The cell wall used to be held clear by a REMAP into a smaller box
    //(a clamp would have piled a third of the stars onto four lines per cell), and that box is the lattice
    //(#88, #148, #370, #674): one star per cell, none of them within a margin of the wall, is a periodic
    //structure whatever the jitter inside it. The probe (Tools/LatticeProbe) measured it at a period of 8.83 px on
    //the axes, in three vantages, on 36-40 tiles of a 66-tile frame; a jitter of a whole cell has no spectral peak.
    float2 lower = cell + float2(cell.x <= -scale ? margin.x : 0.0, cell.y <= -scale ? margin.y : 0.0);
    float2 upper = cell + 1.0 - float2(cell.x >= scale - 1.0 ? margin.x : 0.0, cell.y >= scale - 1.0 ? margin.y : 0.0);
    float2 centre = onFaceEdge ? lower + rollA.yz * (upper - lower) : standing;

    float2 offset = p - centre;

    //ROUND IN THE SKY, not round in the chart (#87). The chart is uv = tan(angle) about the face centre, and
    //it does not stretch the same way in every direction: per chart unit the view direction turns by
    //cos^2(theta) radially but only cos(theta) tangentially. `core` is sized in pixelCells, which carries the
    //jacobian sec^2(theta) — so it gets the RADIAL angular size exactly right (sec^2 * cos^2 = 1, which is the
    //whole point of measuring the footprint on the direction) and thereby leaves the TANGENTIAL one a factor
    //sec(theta) too large. A circle drawn here is an ellipse on the sky: 1.41:1 at the middle of a face edge
    //and 1.73:1 at a face corner, growing smoothly between. That is what the report of "a seam near a cube-face
    //corner" is made of — three faces meet there, so a whole neighbourhood of the sky is at the worst of it at
    //once, and the stars stop being points and become dashes all leaning the same way.
    //
    //Measuring the distance with the tangential component scaled by sec(theta) makes the profile round in ANGLE
    //instead. Written as the closed form rather than by building a radial basis, which is what makes it free:
    //substituting the radial/tangential split into r^2 + t^2 * sec^2 collapses to this, with no normalize, no
    //divide and no branch — and at a face centre chart.xy is zero and the jacobian is one, so it reduces to
    //exactly the dot(offset, offset) it replaces, bit for bit.
    //NOT named `along`: the spike block below already has a float2 of that name, and a float here would be
    //shadowed by it inside the branch rather than clash — which compiles, and leaves the next reader of the
    //arms unsure which `along` they are looking at.
    float radialDot = dot(offset, chart.xy);
    float distance2 = dot(offset, offset) * jacobian - radialDot * radialDot;

    //The margin above is PER AXIS since #148: it divides the star's angular reach by each axis's own factor,
    //which is the same conversion this quadratic form applies, so no profile can reach the limit the margin sets and the
    //tangential over-estimate the isotropic margin used to carry is gone - jitter room bought back exactly
    //where the cap binds.
    float profile = exp(-distance2 / (core * core));

    //Diffraction spikes, on the brightest few of the coarse layer only. They are drawn here rather than left
    //to the glare post-pass because the glare samples this target far too sparsely to catch a one-pixel star
    //reliably, and a star that spikes on some frames and not others reads as a bug.
    //
    //Taken as a MAX with the core rather than added to it, which is what keeps the brightest star's peak at
    //exactly `peak` instead of twice it. Added, a full-magnitude spiked star reached about 0.97 luminance -
    //nearly double GLARE_THRESHOLD - and so became the one thing in this sky the glare samples stochastically
    //and pops on and off, which is precisely the artifact the spikes are drawn here to avoid. A max is also
    //the physically sensible reading: a diffraction spike is the star's own light spread out, not extra light.
    if (drawSpikes)
    {
        //Shortened when the margin above hit its cap, rather than left to run past the cell and be cut there:
        //a clipped arm ends in a straight line on the cell boundary, which is the lattice drawn out in full,
        //and a shortened one is only shorter. Below the cap this is exactly core * StarSpikeLength. The cap
        //test runs in ANGULAR units - margin * axis undoes the division the per-axis margin applied - and
        //takes the tighter axis, so both arms fit the wall whichever one that is.
        //⚠ The cap is the OLD margin cap, 0.34 of a cell, and not the 0.5 the margin itself is held to since #674: the
        //cap used to be the size of the box a spiked star was allowed to stand in, and what it bought was the length
        //of a bright star's cross. With the box gone the arms could run to 0.5 - and did, in the first cut: the
        //same sky with visibly more and longer crosses, a change of LOOK that no one asked for. Kept, so a bright
        //star's arms are the length they always were.
        float2 armMargin = min(margin, 0.34);
        float reach = min(core * StarSpikeLength, min(armMargin.x * axis.x, armMargin.y * axis.y) * (1.0 / 2.5));

        //The arms measured in ANGLE, like the core above them (#148): `along` is the raw chart offset scaled
        //by each axis's own factor, so a horizontal arm keeps one constant angular length wherever its cell
        //sits on the face, instead of stretching tangentially by up to sec(theta) - 1.41:1 at the middle of
        //a face edge, 1.73:1 at a corner - around a core that was already round. The transverse gaussian
        //below is corrected by the same factor, which is exactly what #87's closed form gives for an offset
        //along a single axis, so arm and core agree at the crossing. Only stars over StarSpikeThreshold
        //draw arms (~1.3 % of coarse cells); a diffraction cross is an artifact of the optics rather than
        //a shape on the sky.
        float2 along = abs(offset) * axis;

        //Tapered to zero at exactly 2.5 e-folding lengths (STAR_SPIKE_FLOOR above), which is the end of the reach
        //(the cell wall until #674, armMargin from the star since) both branches of `margin` land on - so the arm ends in a smooth taper instead of the straight cut a
        //raw exp(-along/reach) leaves at 8.21 % there. Renormalised by 1/(1-floor) so the peak at along = 0
        //stays 1 and the MAX-with-core ceiling below is unchanged. (#148)
        float horizontal = max(exp(-along.x / reach) - STAR_SPIKE_FLOOR, 0.0)
            * (1.0 / (1.0 - STAR_SPIKE_FLOOR)) * exp(-(along.y * along.y) / (core * core));
        float vertical = max(exp(-along.y / reach) - STAR_SPIKE_FLOOR, 0.0)
            * (1.0 / (1.0 - STAR_SPIKE_FLOOR)) * exp(-(along.x * along.x) / (core * core));

        float strength = (magnitude - StarSpikeThreshold) / max(1.0 - StarSpikeThreshold, 1e-3);

        //Halved so the two arms crossing at the centre come to 1 and not 2 - the same ceiling the core has
        profile = max(profile, strength * 0.5 * (horizontal + vertical));
    }

    return tint * (peak * magnitude * profile);
}

//One layer: the cells a star's light can come from - at most 2 x 2, usually one or two - each judged by StarInCell and
//combined by MAX (#674). A star reaches at most half a cell (its margin is capped there), so the cells that can hold
//one that lights THIS pixel are the ones overlapping half a cell either side of it: a 2 x 2 block at worst, and,
//and where the real bound is well under half a cell, a single cell (the fine layers at 900p are not: their reach is a good part of a cell, so they usually look up two or four). Cells off the
//cube face are skipped: the chart goes on past the face, but what stands there is the next face's sky, drawn by
//that face's own lookup, and drawing it here too would double the stars in a band along every seam.
//
//MAX and not a sum, for the reason the spikes are a max with the core: two bright stars that happen to land
//within a pixel or two of each other must not add up past GLARE_THRESHOLD, where the glare samples the target too
//sparsely to catch them steadily and they would flicker in and out. Overlapping stars now DO occur - stars near a
//cell wall are no longer pushed apart - and that is the point: a real sky has clumps, and one star to a cell
//with none allowed to touch is the lattice.
float3 StarLayer(float3 dir, float pixelAngle, float scale, float chance, float peak, bool spikes)
{
    float3 chart = CubeChart(dir);
    float2 p = chart.xy * scale;

    //How far, in cells, ANY star of this layer can reach this pixel: the brightest a star can be, so a bound and
    //not a per-star figure (StarInCell's own reach is per star). The jacobian is the one StarInCell takes
    //(chart units to pixels). Held to half a cell, which is what up to four cells can promise - and, for an interior star, a hard circular clip at that distance (a star bright enough to reach further is cut there, a step of a few codes at the outer edge of a large jacobian; the old cut sat at 0.34, so this is no worse); the spikes' arms, on the
    //coarse layer, reach 0.34 of a cell along the axes.
    float pixelCells = pixelAngle * CubeJacobian(chart.xy) * scale;
    float plainReach = max(StarSpread * SupersampleFactor, 0.62) * pixelCells * (1.0 + 0.9) * STAR_MAX_REACH_RADII;
    float quickReach = min(spikes ? max(plainReach, 0.34) : plainReach, 0.5);

    //The cells a star reaching this far can stand in: those overlapping [p - reach, p + reach]. Never more than two
    //a side, and mostly one - a pixel in the middle of its own cell is served by that cell alone - so what the fine
    //layers cost is about a cell and a half of hashing, not the four the block could hold.
    float2 first = floor(p - quickReach);
    float2 last = floor(p + quickReach);

    float3 stars = 0.0;

    [unroll]
    for (int y = 0; y < 2; y++)
    {
        [unroll]
        for (int x = 0; x < 2; x++)
        {
            float2 cell = first + float2(x, y);
            if (cell.x > last.x || cell.y > last.y) continue;
            if (cell.x < -scale || cell.y < -scale || cell.x > scale - 1.0 || cell.y > scale - 1.0) continue;

            stars = max(stars, StarInCell(cell, p, chart, pixelAngle, scale, chance, peak, spikes, quickReach));
        }
    }

    return stars;
}
