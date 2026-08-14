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
//Three layers, coarse to fine. One star per cell of a cube-face lattice, jittered inside its own cell and
//kept clear of the cell edge by its own radius, so a single cell lookup is enough and no lattice shows -
//the meadow's wildflowers solve their clipping the same way. Cell scale is cells per unit of cube-face uv.
float StarCellScale[3];
float StarChance[3];      //fraction of cells carrying a star
float StarPeak[3];        //peak linear radiance of the brightest star of the layer
float StarSpread;         //core radius in OUTPUT pixels; under ~0.4 the field starts to crawl

//Steepness of the brightness law: brightness = pow(hash, StarFalloff). Real star counts climb steeply
//towards the faint end, and a field of evenly bright dots reads as noise rather than as a sky.
float StarFalloff;

//Fraction of the layer's peak past which a star also gets drawn diffraction spikes, and how far they reach
//(in units of the star's own core radius). Only the coarse layer draws them - its cells are wide enough
//that a spike cannot run out of the one cell being sampled.
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

//The level a spike arm has fallen to by the time it reaches its own cell wall: both branches of `margin`
//below land the wall at exactly 2.5 e-folding lengths (uncapped by construction, capped because reach =
//margin / 2.5), so every arm used to terminate at exp(-2.5) = 8.21 % of its amplitude, in a straight cut.
//The taper below subtracts this floor off and renormalises, so the arm reaches exactly zero AT the wall
//instead of being cut dead at 8.21 % there - a straight cut is a square step the eye reads as the lattice
//drawn out, where a taper to zero is not (#148).
static const float STAR_SPIKE_FLOOR = exp(-2.5);

//One layer. A single cell lookup: the star is jittered inside its own cell but held its own radius clear
//of the edges, so it can never straddle a boundary and the eight neighbours never have to be sampled.
//
//The core is sized in OUTPUT pixels rather than in texels or in radians, which is what keeps it identical
//on every supersampling setting and always at least one texel across - a star drawn smaller than a texel
//crawls and scintillates as the camera turns, and in vacuum a star is the one thing that must NOT twinkle.
//
//That margin is the whole of #88 ("the stars read as arranged in a grid"). It is subtracted from BOTH ends
//of the cell, so a star may only land in the middle `1 - 2 * margin` of it, and under about half a cell of
//that box the spacing between neighbours stops looking random and the lattice shows through. The margin is
//in cell units while the star is sized in pixels, so the box closes as the cells get smaller on screen -
//which is why this was reported from a laptop and is invisible at 4K, and why both figures below are
//derived from what a star actually reaches rather than assumed.
float3 StarLayer(float3 dir, float pixelAngle, float scale, float chance, float peak, bool spikes)
{
	float3 chart = CubeChart(dir);
	float2 p = chart.xy * scale;

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

	float2 cell = floor(p);

	//The layer goes into the seed as well as the cell and the face. The three layers run at different scales,
	//so cell (5,7) of the coarse layer and cell (5,7) of the fine one are unrelated patches of sky - but
	//without this they share their existence roll, their jitter, their magnitude and their colour, which is a
	//correlation nobody would ever see and every reason to not have.
	float3 seed = float3(cell, chart.z + scale);

	float3 rollA = Hash33(seed);
	if (rollA.x > chance) return 0.0;

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

	//Held clear of the cell edge by however far this star actually reaches, so nothing is ever clipped by the
	//boundary of the one cell being sampled. A SPIKED star throws arms StarSpikeLength core radii out and
	//needs far more room - at three radii its arms were cut dead straight where they crossed into the next
	//cell, at about two thirds of their brightness. PER AXIS since #148: the reach is an angular distance
	//and the cell wall is a chart one, so each axis divides by its own `axis` factor - the same conversion
	//the profile measures - which both keeps every profile short of the wall and makes this smaller
	//tangentially than the isotropic figure it replaced, buying back jitter room where the cap binds.
	float2 margin = min((drawSpikes ? core * StarSpikeLength * 2.5 : core * reachRadii) / axis, 0.34);

	//REMAPPED into the safe box, not clamped into it. A clamp piles every roll outside the box onto the two
	//margin lines themselves - at a typical margin that is a third of all stars sitting on four lines per
	//cell, which is a lattice. A remap keeps the distribution uniform inside the box.
	float2 centre = cell + margin + rollA.yz * (1.0 - 2.0 * margin);

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
	//which is the same conversion this quadratic form applies, so no profile can reach the cell wall and the
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
		float reach = min(core * StarSpikeLength, min(margin.x * axis.x, margin.y * axis.y) * (1.0 / 2.5));

		//The arms measured in ANGLE, like the core above them (#148): `along` is the raw chart offset scaled
		//by each axis's own factor, so a horizontal arm keeps one constant angular length wherever its cell
		//sits on the face, instead of stretching tangentially by up to sec(theta) - 1.41:1 at the middle of
		//a face edge, 1.73:1 at a corner - around a core that was already round. The transverse gaussian
		//below is corrected by the same factor, which is exactly what #87's closed form gives for an offset
		//along a single axis, so arm and core agree at the crossing. Only stars over StarSpikeThreshold
		//draw arms (~1.3 % of coarse cells); a diffraction cross is an artifact of the optics rather than
		//a shape on the sky.
		float2 along = abs(offset) * axis;

		//Tapered to zero at exactly 2.5 e-folding lengths (STAR_SPIKE_FLOOR above), which is the cell wall
		//both branches of `margin` land on - so the arm ends in a smooth taper instead of the straight cut a
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
