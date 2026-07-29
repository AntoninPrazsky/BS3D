//Draws deep space: a dense starfield, the Milky Way with its dust lanes, coloured emission nebulae,
//scattered distant galaxies and one large planet - the long-exposure astrophoto of the night sky, which is
//frankly not what an eye in orbit would see (it would see mostly black) but is what "space" looks like to
//everyone who has ever seen a picture of it. Ninth scene variant; the island floats in it.
//
//This one is not like the other backdrops, and the difference decides everything below. Every other scene
//replaces the CITY - the ground around the island - and leaves the sky dome standing over it. This one
//replaces the SKY. So it is not a displaced grid pinned to the camera but ONE FULL-SCREEN PASS: a quad
//already in normalized device coordinates, the view ray recovered per pixel through InverseViewProjection,
//drawn with the depth state off so it writes no depth and the island, the cluster and the gun then draw
//over it normally. It covers the whole frame rather than a hemisphere, because in space there is no
//horizon and the stars go on below you; the caller therefore draws no dome and no cloud deck in this scene,
//and suppresses the cloud shadow on the instanced effect so nothing is shaded by weather that is not there.
//
//Everything is written in LINEAR RADIANCE into the HDR target, and everything SMALL is deliberately kept
//under GLARE_THRESHOLD (0.55 on luminance). That is not timidity: the glare's bright pass samples the
//supersampled scene target with a bilinear tap into a quarter-BACK-BUFFER target, i.e. roughly one tap per
//8x8 source texels, so a one-pixel star is sampled only when it happens to fall under a tap - and a star
//that glares on some frames and not others reads as a fault, not as a star. The brightest stars therefore
//carry their diffraction spikes in the shader, where they are stable, and their peak is capped at the
//layer's own `peak` (see StarLayer). Every star, nebula and Milky Way value here sits under the threshold.
//
//The ONE deliberate exception is the planet's lit limb, which is allowed over it. The difference is size and
//coherence, not brightness: the limb is a smooth arc hundreds of pixels long, so the glare's sparse grid
//lands on it many times over and it blooms steadily, which is exactly what a planet's atmosphere should do.
//An isolated point has one chance of being sampled and therefore flickers. The balls still glare too.
//
//Built by all three executables out of this directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//How many nebulae the scene carries. Matched by SpaceSceneConfig's array length on the C# side.
#define NEBULA_COUNT 3

float4x4 InverseViewProjection;
float3 CameraPosition;

//Towards the sun - the same one the island, the gun and the balls take, which is what ties the planet's
//terminator to the light everything in front of it is lit by
float3 SunDirection;

//How many scene-target texels make one output pixel. The scene is rendered supersampled and box-filtered on
//resolve, so a star drawn one TEXEL across would come out four times dimmer at 2x than at 1x - the same star
//on two quality settings. Sized in OUTPUT pixels instead, it reads the same on every setting, and since the
//factor is never below 1 it is also never below the texel size that would alias.
float SupersampleFactor;

//The empty sky between everything else (linear). Not pure black: a real deep-sky frame has an airglow/
//zodiacal floor, and a frame that goes to zero looks like a hole rather than like distance.
float3 VoidColor;

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

//--- The Milky Way -----------------------------------------------------------------------------------
//The galactic plane is defined by its pole; the core direction sets where the bright bulge sits and which
//way the band thins out. Both normalized on the C# side.
float3 GalacticPole;
float3 GalacticCore;

float MilkyWayWidth;        //angular half-width of the band (as sin of galactic latitude)
float MilkyWayBrightness;   //peak linear radiance at the core
float3 MilkyWayColor;       //the cooler outer arms (linear)
float3 MilkyWayCoreColor;   //the warmer, denser bulge (linear)
float MilkyWayDust;         //how hard the dust lanes cut, 0..1
float MilkyWayStarBoost;    //extra star density inside the band

//--- Nebulae -----------------------------------------------------------------------------------------
//Each is a direction on the sky plus a shape: x = angular radius (radians), y = strength, z = detail scale,
//w = domain-warp amount. The warp is what turns a soft blob into filaments and cavities and is the single
//biggest quality lever in here.
float3 NebulaDirection[NEBULA_COUNT];
float3 NebulaColor[NEBULA_COUNT];
float4 NebulaShape[NEBULA_COUNT];

//--- Distant galaxies --------------------------------------------------------------------------------
float GalaxyCellScale;
float GalaxyChance;
float GalaxySize;         //base angular radius (radians)
float GalaxyBrightness;
float3 GalaxyColor;

//--- The planet --------------------------------------------------------------------------------------
float3 PlanetDirection;      //where it hangs, normalized
float PlanetAngularRadius;   //radians; how much of the sky it takes
float3 PlanetAxis;           //its pole, which is what the cloud bands run around
float3 PlanetColorLight;     //the pale bands (linear)
float3 PlanetColorDark;      //the dark belts (linear)
float3 PlanetStormColor;     //the one big storm oval (linear)
float3 PlanetRimColor;       //the limb's atmosphere (linear)
float PlanetBandScale;       //bands per pole-to-pole sweep
float PlanetRimStrength;
float PlanetNightAmbient;    //what the unlit side keeps, so it reads as a sphere and not as a hole

//=====================================================================================================
//Hashes and noise. No sin anywhere: a sine-based hash is where two implementations of the same field part
//company, and this project keeps that rule even where only one implementation exists.
//=====================================================================================================

float Hash13(float3 p)
{
	p = frac(p * 0.1031);
	p += dot(p, p.zyx + 31.32);

	return frac((p.x + p.y) * p.z);
}

float3 Hash33(float3 p)
{
	p = frac(p * float3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);

	return frac((p.xxy + p.yxx) * p.zyx);
}

//Value noise on the direction sphere. 3D rather than a 2D chart on purpose: any 2D parametrization of a
//sphere has a seam or a pole, and a seam across the Milky Way is the one artifact nobody would forgive.
float ValueNoise3(float3 p)
{
	float3 i = floor(p);
	float3 f = p - i;
	f = f * f * (3.0 - 2.0 * f);

	float n000 = Hash13(i);
	float n100 = Hash13(i + float3(1, 0, 0));
	float n010 = Hash13(i + float3(0, 1, 0));
	float n110 = Hash13(i + float3(1, 1, 0));
	float n001 = Hash13(i + float3(0, 0, 1));
	float n101 = Hash13(i + float3(1, 0, 1));
	float n011 = Hash13(i + float3(0, 1, 1));
	float n111 = Hash13(i + float3(1, 1, 1));

	float x00 = lerp(n000, n100, f.x);
	float x10 = lerp(n010, n110, f.x);
	float x01 = lerp(n001, n101, f.x);
	float x11 = lerp(n011, n111, f.x);

	return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
}

//Four octaves, normalized to 0..1. Everything that has to READ as structure on screen goes through this;
//two-octave work is spelled out at the call site where it is cheap enough to matter.
float Fbm3(float3 p)
{
	float sum = ValueNoise3(p);
	sum += 0.5 * ValueNoise3(p * 2.03 + 17.1);
	sum += 0.25 * ValueNoise3(p * 4.07 + 39.7);
	sum += 0.125 * ValueNoise3(p * 8.11 + 71.3);

	return sum * (1.0 / 1.875);
}

float Fbm3Cheap(float3 p)
{
	return (ValueNoise3(p) + 0.5 * ValueNoise3(p * 2.11 + 23.4)) * (1.0 / 1.5);
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

//=====================================================================================================
//The Milky Way
//=====================================================================================================

//The band's shape before any structure: a gaussian across the galactic plane, thinning away from the core.
//Returned on its own because the star field reads it too - a real sky has far more stars in the band.
float MilkyWayBand(float3 dir)
{
	float latitude = dot(dir, GalacticPole);
	float band = exp(-(latitude * latitude) / (MilkyWayWidth * MilkyWayWidth));

	//Towards the bulge the band is brighter and wider; away from it, it thins to a faint arm. The saturate is
	//not decoration: both vectors are unit, so within about a milliradian of the ANTI-core direction the
	//float32 dot rounds to nextafter(-1), this lands at exactly -5.96e-8, and pow() of a negative base to a
	//fractional power is NaN. MilkyWayGlow's `band < 0.004` guard does not catch it either, since every
	//comparison against a NaN is false. The damage is one black output pixel and no more - Glare.fx combines
	//its streak arms with max(), which in D3D11 returns the non-NaN operand, so the NaN cannot spread through
	//the blur, and the tonemap's closing saturate() takes it to 0. One pixel in a near-black sky is invisible,
	//which is exactly why this would have gone unnoticed rather than why it is acceptable.
	float towardsCore = saturate(dot(dir, GalacticCore) * 0.5 + 0.5);

	return band * lerp(0.20, 1.0, pow(towardsCore, 2.2));
}

//The band with its star clouds and dust lanes. dustExtinction comes back so the star field can be thinned
//behind the lanes: the dark rifts of a real Milky Way are dark because there is opaque dust in front, and
//dust that dims the glow but leaves the stars shining through it reads as a painted stripe.
float3 MilkyWayGlow(float3 dir, float band, out float dustExtinction)
{
	dustExtinction = 1.0;
	if (band < 0.004) return 0.0;

	//Galactic coordinates, mildly squashed in latitude: sampled through that squash the noise comes out in
	//features somewhat longer along the plane than across it, which is what star clouds and dust lanes both
	//are. MILDLY is the operative word - the first build squashed by five and sampled at a third of these
	//frequencies, and the band came out as long smeared streaks that read as motion blur across the sky
	//rather than as a galaxy. Elongation says "in a plane"; too much of it says "something is wrong".
	float3 right, forward;
	BuildFrame(GalacticPole, right, forward);
	float3 galactic = float3(dot(dir, right), dot(dir, GalacticPole) * 1.6, dot(dir, forward));

	//Two scales of star cloud: patches, and a finer grain inside them
	float clouds = Fbm3(galactic * 13.0);
	float grain = Fbm3Cheap(galactic * 38.0 + 5.7);
	float structure = saturate((clouds - 0.44) * 2.8 + 0.5) * (0.55 + 0.72 * grain);

	//The dust. Its own squash again, and much finer than the clouds it cuts across - a lane has to be
	//narrow against the glow to read as something standing in front of it. Smoothstepped so it is lanes and
	//not a haze, and never taken all the way to zero or the band would be cut into detached islands.
	float dust = Fbm3(galactic * float3(1.0, 1.5, 1.0) * 16.0 + 61.0);
	float lanes = smoothstep(0.44, 0.70, dust);

	//Faded out with the band, which the glow is but the extinction was not. The dust noise is full strength
	//everywhere, and the star field reads the extinction whatever the glow is doing - so at the early-out
	//contour above, the stars stepped by up to two thirds of their brightness across a line 16 degrees off
	//the plane where there is no visible band at all. It is also the physically right shape: the dust lives
	//in the galactic plane, so it has to run out where the plane does.
	dustExtinction = 1.0 - lanes * MilkyWayDust * saturate(band * 3.0);

	float towardsCore = saturate(dot(dir, GalacticCore) * 0.5 + 0.5);
	float3 tint = lerp(MilkyWayColor, MilkyWayCoreColor, pow(towardsCore, 1.8));

	return tint * (MilkyWayBrightness * band * structure * dustExtinction);
}

//=====================================================================================================
//Stars
//=====================================================================================================

//One layer. A single cell lookup: the star is jittered inside its own cell but held its own radius clear
//of the edges, so it can never straddle a boundary and the eight neighbours never have to be sampled.
//
//The core is sized in OUTPUT pixels rather than in texels or in radians, which is what keeps it identical
//on every supersampling setting and always at least one texel across - a star drawn smaller than a texel
//crawls and scintillates as the camera turns, and in vacuum a star is the one thing that must NOT twinkle.
float3 StarLayer(float3 dir, float pixelAngle, float scale, float chance, float peak, bool spikes)
{
	float3 chart = CubeChart(dir);
	float2 p = chart.xy * scale;

	float pixelCells = pixelAngle * CubeJacobian(chart.xy) * scale;

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
	float magnitude = pow(rollB.x, StarFalloff);
	float3 tint = StarTint(saturate(rollB.y * (1.1 - 0.75 * magnitude)));

	//A brighter star is drawn a little wider as well as a little brighter. Physically a star is a point
	//whatever its magnitude, but no optics resolve it as one - a bright source spreads further in an eye,
	//a lens and a sensor alike, and a field where every star is the same width reads as a texture of dots
	//with some of them turned up. The floor is what keeps the smallest of them from crawling.
	float core = max(StarSpread * SupersampleFactor, 0.62) * pixelCells * (1.0 + magnitude * 0.9);

	//Held clear of the cell edge by however far this star actually reaches, so nothing is ever clipped by the
	//boundary of the one cell being sampled. Three core radii is enough for the gaussian, but a SPIKED star
	//throws arms StarSpikeLength core radii out and needs far more room - at three radii its arms were cut
	//dead straight where they crossed into the next cell, at about two thirds of their brightness.
	float margin = min(spikes ? core * StarSpikeLength * 2.5 : core * 3.0, 0.34);

	//REMAPPED into the safe box, not clamped into it. A clamp piles every roll outside the box onto the two
	//margin lines themselves - at a typical margin that is a third of all stars sitting on four lines per
	//cell, which is a lattice. A remap keeps the distribution uniform inside the box.
	float2 centre = cell + margin + rollA.yz * (1.0 - 2.0 * margin);

	float2 offset = p - centre;
	float distance2 = dot(offset, offset);

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
	if (spikes && magnitude > StarSpikeThreshold)
	{
		float reach = core * StarSpikeLength;
		float2 along = abs(offset);

		float horizontal = exp(-along.x / reach) * exp(-(along.y * along.y) / (core * core));
		float vertical = exp(-along.y / reach) * exp(-(along.x * along.x) / (core * core));

		float strength = (magnitude - StarSpikeThreshold) / max(1.0 - StarSpikeThreshold, 1e-3);

		//Halved so the two arms crossing at the centre come to 1 and not 2 - the same ceiling the core has
		profile = max(profile, strength * 0.5 * (horizontal + vertical));
	}

	return tint * (peak * magnitude * profile);
}

//=====================================================================================================
//Nebulae
//=====================================================================================================

//Emission nebulae, worked in each one's own tangent plane: they are small on the sky, so a flat chart about
//their own direction has no seam to worry about and the noise stays 3D anyway (it is sampled on the view
//direction, so the structure is pinned to the sky and does not swim as the camera turns).
float3 Nebulae(float3 dir, out float transmittance)
{
	float3 total = 0.0;

	//What of the sky behind gets through. Without it a nebula is a glow decal with the whole starfield
	//shining out of its densest knots, and no amount of filament detail rescues that - a molecular cloud is
	//opaque, and the stars it hides are half of what tells the eye it is a thing in front of something else.
	transmittance = 1.0;

	[unroll]
	for (int i = 0; i < NEBULA_COUNT; i++)
	{
		float4 shape = NebulaShape[i];

		//Angular falloff first, and the body behind a branch: a nebula covers a small part of the sky, so
		//most of the frame must not pay for its noise. Coherent in screen space, so the branch is nearly
		//free; and there are no derivatives inside it, so it is safe to take.
		float cosine = dot(dir, NebulaDirection[i]);
		float falloff = saturate((cosine - cos(shape.x)) / max(1.0 - cos(shape.x), 1e-4));

		[branch]
		if (falloff > 0.002 && shape.y > 0.0)
		{
			float3 sample = dir * shape.z;

			//Domain warp. This is what makes it filaments and cavities instead of a soft blob, and it is
			//worth its two extra noise taps more than any other line in this function.
			float3 warp = float3(
				Fbm3Cheap(sample + 4.3),
				Fbm3Cheap(sample * 1.13 + 27.9),
				Fbm3Cheap(sample * 0.91 + 53.1)) - 0.5;

			float body = Fbm3(sample * 1.6 + warp * shape.w);

			//Spread hard about its own mean before it is used. A timid mottle is exactly what the ACES
			//curve flattens into one tone, and a nebula that is one tone is a smudge.
			body = saturate((body - 0.44) * 2.9);

			//Bright core, thin skirts: the falloff is taken to a power so the edges fade out rather than
			//ending on a circle, and squared into the body so the middle is where the colour is.
			float shell = falloff * falloff * (0.35 + 0.65 * falloff);
			float density = shell * body * body;

			total += NebulaColor[i] * (shape.y * density);
			transmittance *= 1.0 - saturate(density * 1.35);
		}
	}

	return total;
}

//=====================================================================================================
//Distant galaxies
//=====================================================================================================

//Scattered elliptical smudges: a bright core in a faint halo, each at its own size, elongation and angle.
//One coarse cube-lattice layer, the same single-cell trick the stars use.
float3 Galaxies(float3 dir, float pixelAngle)
{
	float3 chart = CubeChart(dir);
	float jacobian = CubeJacobian(chart.xy);
	float2 p = chart.xy * GalaxyCellScale;

	float2 cell = floor(p);
	float3 seed = float3(cell, chart.z + 100.0);

	float3 rollA = Hash33(seed);
	if (rollA.x > GalaxyChance) return 0.0;

	float3 rollB = Hash33(seed + 71.13);

	//Size in cell units, never under the pixel footprint - a galaxy a texel across would shimmer exactly
	//as an unlimited star would
	float pixelCells = pixelAngle * jacobian * GalaxyCellScale;
	float size = max(GalaxySize * (0.55 + 1.5 * rollB.x) * jacobian * GalaxyCellScale, pixelCells * 1.6);

	float margin = min(size * 2.2, 0.4);
	float2 centre = cell + clamp(rollA.yz, margin, 1.0 - margin);

	//Rotate into the galaxy's own axes and squash one of them: an ellipse on the sky is a disc seen at an
	//angle, and edge-on ones are what make a field of them read as galaxies rather than as fuzzy stars.
	float angle = rollB.y * 6.2831853;
	float2 axis = float2(cos(angle), sin(angle));
	float2 offset = p - centre;
	float2 local = float2(dot(offset, axis), dot(offset, float2(-axis.y, axis.x)) / (0.28 + 0.72 * rollB.z));

	float radius = length(local) / size;

	float core = exp(-radius * radius * 5.0);
	float halo = exp(-radius * 2.1) * 0.34;

	//The larger ones lean warm-white (an elliptical), the smaller ones a little blue (a disc still forming
	//stars) - a field all one colour reads as a texture rather than as a sky full of separate objects
	float3 tint = lerp(GalaxyColor, float3(0.78, 0.86, 1.0), saturate(1.2 - rollB.x * 1.6));

	return tint * (GalaxyBrightness * (core + halo));
}

//=====================================================================================================
//The planet
//=====================================================================================================

//Solved analytically rather than drawn: the planet is a unit sphere at the distance that gives it the
//configured angular radius, so the ray test is a quadratic and the surface normal falls out of it. Coverage
//comes back separately so the caller can composite it over the sky it hides.
float3 Planet(float3 dir, float pixelAngle, out float coverage)
{
	coverage = 0.0;

	float cosine = dot(dir, PlanetDirection);
	float cosLimb = cos(PlanetAngularRadius);

	//An extra half-degree of slack so the atmospheric halo outside the disc is reached as well
	float halo = cos(PlanetAngularRadius * 1.09);

	[branch]
	if (cosine <= halo || PlanetAngularRadius <= 0.0) return 0.0;

	//The limb is where cos(angle) crosses cosLimb, and d(cos)/d(angle) is sin there - so the edge is
	//antialiased over exactly one pixel's worth of it
	float edge = max(pixelAngle * sin(PlanetAngularRadius) * 0.8, 1e-6);
	coverage = smoothstep(cosLimb - edge, cosLimb + edge, cosine);

	//The rim of atmosphere that stands off the disc: a thin arc outside the limb, lit only where the sun is
	float3 outside = 0.0;
	if (coverage < 0.999)
	{
		float ring = saturate((cosine - halo) / max(cosLimb - halo, 1e-5));
		float lit = saturate(dot(PlanetDirection, SunDirection) * 0.5 + 0.62);
		outside = PlanetRimColor * (PlanetRimStrength * 0.55 * ring * ring * lit * (1.0 - coverage));
	}

	if (coverage <= 0.0009) return outside;

	//Ray-sphere: a unit sphere centred at distance 1/sin(R) along the planet direction
	float distance = 1.0 / max(sin(PlanetAngularRadius), 1e-4);
	float discriminant = max(distance * distance * (cosine * cosine - 1.0) + 1.0, 0.0);
	float t = distance * cosine - sqrt(discriminant);
	float3 normal = normalize(t * dir - distance * PlanetDirection);

	//The planet's own frame, so the bands stay put on it as the camera moves
	float3 right, forward;
	BuildFrame(PlanetAxis, right, forward);
	float latitude = dot(normal, PlanetAxis);
	float3 local = float3(dot(normal, right), latitude, dot(normal, forward));

	//Bands: noise squashed hard in latitude comes out as belts and zones running the whole way round, and a
	//shear along longitude gives them the drawn-out swirl a gas giant has at every band boundary
	float shear = Fbm3Cheap(local * 2.2 + 9.4) - 0.5;
	float3 banded = float3(local.x, latitude * PlanetBandScale + shear * 1.7, local.z);
	float bands = Fbm3(banded * float3(0.9, 1.0, 0.9) + 3.0);
	bands = saturate((bands - 0.45) * 2.3 + 0.5);

	float3 albedo = lerp(PlanetColorDark, PlanetColorLight, bands);

	//One big long-lived storm oval, wider than it is tall as a real one is. Its centre is a DIRECTION in the
	//planet's own frame, not a pair of projections: the pair (dot(n, forward), latitude) is satisfied by two
	//points, one either side of the forward-pole plane, so the first version stamped the storm twice,
	//mirrored - and half of all placements put the ghost on the very face being drawn. Measured as a
	//tangential offset from a centre direction, with the antipode rejected, there is exactly one of it.
	float3 stormCentre = normalize(forward * 0.82 + right * 0.38 - PlanetAxis * 0.30);
	float3 stormEast = normalize(cross(PlanetAxis, stormCentre));
	float3 stormNorth = cross(stormCentre, stormEast);

	float2 stormLocal = float2(dot(normal, stormEast), dot(normal, stormNorth) * 2.4);
	float storm = exp(-dot(stormLocal, stormLocal) * 26.0) * step(0.0, dot(normal, stormCentre));

	albedo = lerp(albedo, PlanetStormColor, storm * 0.85);

	//A soft terminator: a hard N.L cut on a gas giant reads as a cardboard cut-out, and a real one has an
	//atmosphere carrying the light some way round. The night side keeps a little so it stays a sphere.
	float ndotl = dot(normal, SunDirection);
	float daylight = smoothstep(-0.22, 0.42, ndotl);

	float3 surface = albedo * (daylight + PlanetNightAmbient);

	//The limb brightens: at a grazing angle the line of sight runs a long way through the atmosphere. This is
	//the one thing in the sky allowed over GLARE_THRESHOLD, and only in the last few pixels of the disc - a
	//long coherent arc blooms steadily where an isolated point would flicker (see the header). The body of
	//the disc stays under it, which is what keeps the glare to the rim rather than the whole planet.
	float grazing = 1.0 - saturate(dot(normal, -dir));
	surface += PlanetRimColor * (PlanetRimStrength * pow(grazing, 3.5) * saturate(daylight + 0.15));

	return surface * coverage + outside;
}

//=====================================================================================================

struct SpaceVertexOutput
{
	float4 Position : SV_POSITION;
	float3 Ray : TEXCOORD0;
};

SpaceVertexOutput SpaceVS(float3 position : POSITION0)
{
	SpaceVertexOutput output;

	//The quad arrives already in normalized device coordinates. z = w puts it on the far plane, which is
	//where a sky belongs; the depth state is off for this pass, so it is only clipping that has to be
	//survived.
	output.Position = float4(position.xy, 1.0, 1.0);

	//Back through the projection and the view to the world point this corner's ray reaches on the far
	//plane. The far plane is a plane in world space and the map from screen to it is affine, so
	//interpolating this across the quad is exact rather than approximate.
	float4 far = mul(float4(position.xy, 1.0, 1.0), InverseViewProjection);
	output.Ray = far.xyz / far.w - CameraPosition;

	return output;
}

float4 SpacePS(SpaceVertexOutput input) : COLOR
{
	float3 dir = normalize(input.Ray);

	//This pixel's angular footprint, measured on the DIRECTION rather than on any chart of it. The
	//direction is continuous everywhere, so this number is too - which is what lets stars and galaxies
	//sit on a cube lattice without its twelve seams showing as rings of wrongly sized dots.
	float pixelAngle = max(length(fwidth(dir)), 1e-6);

	//A very slow, very faint large-scale mottle on the void: the zodiacal light and the airglow. It does
	//almost nothing except stop the empty sky reading as a flat fill.
	float3 sky = VoidColor * (0.7 + 0.9 * Fbm3Cheap(dir * 1.7));

	float band = MilkyWayBand(dir);

	float dustExtinction;
	sky += MilkyWayGlow(dir, band, dustExtinction);

	//The nebulae both add their own light and swallow what is behind them
	float nebulaTransmittance;
	sky += Nebulae(dir, nebulaTransmittance);

	sky += Galaxies(dir, pixelAngle) * nebulaTransmittance;

	//The stars last, and dimmed behind whatever dust stands in front of them - the Milky Way's lanes and the
	//nebulae's own bodies. A lane that dims the glow but leaves the stars shining through reads as a stripe
	//painted over the sky rather than as something standing in front of it.
	float density = 1.0 + band * MilkyWayStarBoost;

	float3 stars = StarLayer(dir, pixelAngle, StarCellScale[0], StarChance[0] * density, StarPeak[0], true);
	stars += StarLayer(dir, pixelAngle, StarCellScale[1], StarChance[1] * density, StarPeak[1], false);
	stars += StarLayer(dir, pixelAngle, StarCellScale[2], StarChance[2] * density, StarPeak[2], false);

	sky += stars * lerp(1.0, dustExtinction, 0.75) * lerp(1.0, nebulaTransmittance, 0.85);

	//A fine dither. The void and the band's outskirts are enormous areas crossed by a very shallow
	//gradient - a few dozen display codes over a whole screen - which is exactly where an 8-bit back
	//buffer draws contour rings. A couple of percent of noise at about one cell per pixel breaks them up,
	//and the supersample box filter halves whatever is left of it. Applied to the sky alone: the planet is
	//composited after, and a lit disc has plenty of gradient of its own.
	sky *= 1.0 + (Hash13(floor(dir / pixelAngle)) - 0.5) * 0.03;

	//The planet stands in front of all of it
	float coverage;
	float3 planet = Planet(dir, pixelAngle, coverage);
	sky = sky * (1.0 - coverage) + planet;

	return float4(sky, 1.0);
}

technique Space
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SpaceVS();
		PixelShader = compile PS_SHADERMODEL SpacePS();
	}
};
