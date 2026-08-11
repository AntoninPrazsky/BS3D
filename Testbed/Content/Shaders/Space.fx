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

//The starfield lattice, its helpers (Hash33, BuildFrame, CubeChart/CubeJacobian, StarTint) and the star
//uniforms — one copy shared with Moon.fx, the other sky with no atmosphere in it (#125). It is this file's
//own code moved out verbatim; the history and the discipline stay in the comments there.
#include "Stars.fxh"

//How many nebulae the scene carries. Matched by SpaceSceneConfig's array length on the C# side.
#define NEBULA_COUNT 3

float4x4 InverseViewProjection;
float3 CameraPosition;

//Towards the sun - the same one the island, the gun and the balls take, which is what ties the planet's
//terminator to the light everything in front of it is lit by
float3 SunDirection;

//The empty sky between everything else (linear). Not pure black: a real deep-sky frame has an airglow/
//zodiacal floor, and a frame that goes to zero looks like a hole rather than like distance.
float3 VoidColor;

//Wall clock (seconds), for the volume's slow drift. Nothing else in this scene moves — a long-exposure sky
//has nothing to animate — which is why this arrived with the volume rather than with the scene.
float SpaceTime;

//--- The volume the island is inside -----------------------------------------------------------------
//See StarNestVolume. Strength 0 skips the march outright and restores the scene to its painted-dome self.
float VolumeStrength;
float VolumeScale;        //world units -> field units on the way in; this is the parallax dial
float VolumeDrift;        //field units per second the eye slides through it
float VolumeSaturation;
float VolumeOpacity;      //how hard the web swallows the sky behind it
float3 VolumeTint;

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
//The volume the island is INSIDE
//=====================================================================================================

//Everything above this point is a function of the view DIRECTION and nothing else, and that is exactly what
//made this scene read as a painted dome rather than as a place: the camera's position enters the shader only
//to build the ray. Move the camera and not one pixel changes; turn it and the whole picture slides rigidly,
//near and far together. A starfield is entitled to behave that way - real stars have no parallax worth
//drawing - but it means the scene had no depth of its own to be inside of.
//
//This layer is the one thing here that is marched THROUGH. Steps are taken along the ray from the camera's
//own position, so structure a step away moves across the frame faster than structure ten steps away: real
//parallax, from real depth, and it is the whole reason the layer exists. The drop cinematic diving under the
//island is where it pays most, but even the menu's slow orbit now moves through something.
//
//The field is Pablo Roman Andrioli's "Star Nest" (MIT licensed, hence usable rather than merely admirable),
//and the shape of it is worth understanding before it is retuned. Space is folded into mirrored cells by
//the triangle wave `2T * abs(frac(p / 2T) - 0.5)`, which repeats the volume for free and mirrors at every
//cell wall so no seam shows. It is spelled with frac rather than as the original's `abs(T - mod(p, 2T))`,
//because the original is GLSL and GLSL mod is floor-based where HLSL fmod truncates toward zero: ported
//naively, the fold stopped mirroring below zero and JUMPED by the full 2T period at every p = -2T*k
//(k >= 1). The drift walks p.z down through those planes - the first crossing derived at 483-772 s of
//play at the shipped camera, a circle per march step sweeping the sky - photographed in #147 at a pinned
//661 s, where the circles stack into one hard, frame-crossing step. The fix and the identity between the
//two spellings are worked through there. Inside a cell an iterated map
//`p = abs(p)/dot(p,p) - FORMULA` runs: a sphere inversion (which turns
//the space inside out about the unit sphere, sending near points far and far points near), a fold into the
//positive octant, and a translate. Iterated, that is a kaleidoscopic IFS whose attractor is a filigree of
//sheets and filaments - and what is drawn is not the attractor's position but how far the point MOVED at each
//iteration, summed. Points near the attractor move little and points far from it move a lot, so the sum is
//an inside-out distance field, and the filaments come out as the dark tracery inside a glowing web.
//
//What was changed from the original, and why:
//  - the volume is entered at the CAMERA rather than at a time-driven position, which is the point above;
//  - it is rotated into a basis of its own, so the fold's axes do not line up with the world's and the cell
//    walls do not read as a grid squared up with the island and the gun;
//  - the world is scaled down hard on the way in (VolumeScale). The cells are 0.85 units across in the
//    field's own space while this scene's island is 26 across and its camera stands 33 out, so feeding world
//    coordinates in raw would fly the camera through thousands of cells and the whole thing would boil;
//  - it carries an EXTINCTION, which the original has no need of because it draws nothing behind itself. Here
//    the stars are behind it, and a bright web that lets every star shine through its densest knots reads as
//    a decal over the sky rather than as something the sky is seen through.
//
//Cost is why the step and iteration counts are here as constants rather than dials: they are the shader's
//whole budget, they multiply, and a dial invites setting them somewhere that drops the frame rate off a
//cliff. The original runs 20 steps of 17 iterations - 340 evaluations of the formula for every pixel of the
//frame - which is a Shadertoy's budget and not a backdrop's. See the measured figures in docs/scenes.md.
//Eight steps and ten iterations, and the step count in particular is nearly free to cut: each step's
//contribution is multiplied by VOLUME_DISTANCE_FADING, so the geometric series 0.76^r has already spent 93 %
//of its total by the eighth step and the eleventh adds 4 %. Marching further is paying full price for a
//contribution that is below the dither. Measured from the same vantage at ssaa 2, against the painted sky
//this was added to (60.4 FPS): 11 steps x 12 iterations gives 29.5 FPS, i.e. +105 % frame time, which is a
//backdrop taking the frame over; 8 x 10 gives 36.5, i.e. +65 %, and the difference between the two is not
//visible side by side. Cutting the STEPS further is a bad trade even though it is the cheaper knob - the
//march's depth is what the layer exists for, so eight steps of 0.13 is the depth of volume there is to be
//inside of, and six would be spending the feature to save its own cost.
static const int VOLUME_STEPS = 8;
static const int VOLUME_ITERATIONS = 10;

static const float VOLUME_TILE = 0.85;
static const float VOLUME_FORMULA = 0.53;
static const float VOLUME_STEP_SIZE = 0.13;

//How much of the previous step's light survives one step further out. Under 1, so the far end of the march
//contributes less than the near end - which is the depth cue the whole layer is for, and it doubles as the
//march's own horizon so there is no hard end to it.
static const float VOLUME_DISTANCE_FADING = 0.76;

//The original's "dark matter": where the summed movement is LOW the point is near the attractor, and those
//regions are made to absorb rather than emit. It is what keeps the web from filling in to a fog.
static const float VOLUME_DARK_MATTER = 0.30;

//How much of the flat between-filaments haze is kept. See where it is used for why it is a fraction.
static const float VOLUME_HAZE = 0.22;

//A fixed basis, orthonormal to about three decimals, that shares no axis with the world's. Baked as a
//constant because it never changes and the compiler folds it into the multiplies.
static const float3x3 VOLUME_BASIS = float3x3(
	0.5749, 0.7385, 0.3502,
	-0.7906, 0.4180, 0.4463,
	0.2115, -0.5292, 0.8218);

//The volume, marched from the eye. Returns its emission; `transmittance` is what of the sky behind survives.
float3 StarNestVolume(float3 dir, float3 eye, out float transmittance)
{
	transmittance = 1.0;

	//A branch on a UNIFORM, so it cannot diverge, and there are no derivatives anywhere inside this
	//function - which is what makes it safe to skip the whole march when the layer is switched off. That
	//matters: at strength 0 this is the most expensive thing in the frame doing nothing.
	[branch]
	if (VolumeStrength <= 0.0) return 0.0;

	float3 rayDirection = mul(VOLUME_BASIS, dir);

	//The eye, scaled down into the field's own space, plus the slow drift. The offset is the original's own
	//starting point, kept because the field is not homogeneous - some places in it are far more interesting
	//than others, and this is a place that was chosen by someone who looked.
	float3 rayOrigin = mul(VOLUME_BASIS, eye * VolumeScale)
		+ float3(1.0, 0.5, 0.5)
		+ SpaceTime * VolumeDrift * float3(2.0, 1.0, -1.0);

	float s = 0.1;
	float fade = 1.0;
	float3 accumulated = 0.0;

	for (int r = 0; r < VOLUME_STEPS; r++)
	{
		float3 p = rayOrigin + s * rayDirection * 0.5;

		//The mirrored tiling fold: a triangle wave over [0, TILE], continuous and mirrored for every real
		//input. NOT fmod - that truncates toward zero and breaks the fold below zero (see the header, #147).
		p = VOLUME_TILE * 2.0 * abs(frac(p / (VOLUME_TILE * 2.0)) - 0.5);

		float previousLength = 0.0;
		float activity = 0.0;

		[unroll]
		for (int i = 0; i < VOLUME_ITERATIONS; i++)
		{
			p = abs(p) / dot(p, p) - VOLUME_FORMULA;

			float currentLength = length(p);
			activity += abs(currentLength - previousLength);
			previousLength = currentLength;
		}

		float dark = max(0.0, VOLUME_DARK_MATTER - activity * activity * 0.001);

		//Cubed, which is the original's contrast: the web has to be mostly dark with bright filaments, and a
		//linear sum of movement is mostly middling grey.
		activity *= activity * activity;

		//The absorbing regions are only allowed to act past the first few steps. Applied from the first, the
		//cell the camera is standing in would dim the entire frame every time the fold put a dense patch
		//against the lens.
		fade *= (r > 4) ? (1.0 - dark) : 1.0;

		//A flat term as well as the coloured one: the milky haze between the filaments, which is what makes
		//it a medium rather than a set of curves hanging in a vacuum.
		//
		//Scaled down hard from the original's full `v += fade`, and this is the one number that had to change
		//for the layer to belong to THIS scene rather than replace it. That term is a constant lift over every
		//pixel of the frame - the series sums to about 3.7 - and at full weight it took the void from the 3 or
		//4 display codes this scene is authored around up to a uniform grey, which swallowed the Milky Way's
		//band and both bright nebulae whole. In Star Nest it is free to do that because there is nothing else
		//in the frame. Here the haze has to be the thin stuff BETWEEN the filaments and nothing more.
		accumulated += fade * VOLUME_HAZE;

		//Coloured BY DEPTH along the ray - s, s squared, s to the fourth - so the near web comes out cool and
		//the far one warm. It is a cheap trick standing in for scattering, and it is most of why the field
		//reads as having a front and a back.
		accumulated += float3(s, s * s, s * s * s * s) * activity * 0.0015 * fade;

		fade *= VOLUME_DISTANCE_FADING;
		s += VOLUME_STEP_SIZE;
	}

	//Pull the saturation back towards its own luminance: the depth ramp above is violent, and left raw the
	//field is a rainbow rather than a nebula.
	float3 volume = lerp(length(accumulated), accumulated, VolumeSaturation) * 0.01;

	volume *= VolumeTint * VolumeStrength;

	//And the extinction, off the emission's own luminance rather than a second accumulator: where the web is
	//bright it is also dense, so this is very nearly free and cannot disagree with what is drawn. Reciprocal
	//rather than linear so it can never go negative however hard the strength is turned up.
	transmittance = 1.0 / (1.0 + dot(volume, float3(0.30, 0.59, 0.11)) * VolumeOpacity);

	return volume;
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

	//Remapped into the safe box rather than clamped into it, for the reason StarLayer states above: a clamp
	//piles every roll that falls outside the box onto the two margin lines themselves, and at this layer's
	//margins that is about a fifth of all galaxies sitting on four straight lines per cell.
	float margin = min(size * 2.2, 0.4);
	float2 centre = cell + margin + rollA.yz * (1.0 - 2.0 * margin);

	float2 offset = p - centre;

	//Into a frame where a chart distance is proportional to an ANGULAR one, before the galaxy's own ellipse is
	//built on top (#87). The chart is uv = tan(angle), which turns the view direction by cos^2(theta) per chart
	//unit radially but only cos(theta) tangentially, so a circle measured here is an ellipse on the sky —
	//1.41:1 at the middle of a face edge and 1.73:1 at a face corner, always combed about the face centre.
	//StarLayer gets this for free as a closed form, because it only needs the squared distance; a galaxy is
	//deliberately elliptical at an angle of its own, so it needs the corrected VECTOR and pays a normalize.
	//
	//The squashed axis compounds with it, which is why leaving this out was easy to miss: a galaxy that is
	//already a random ellipse of up to 3.6:1 hides an extra 1.73 well — until a corner, where the whole knot of
	//them combs the same way and the eye reads it as structure in the sky rather than as a field of discs.
	//`size` is left riding the jacobian: |offset| after this correction is the angular distance times J, and
	//size carries J too, so the J cancels out of `radius` and a galaxy keeps its angular size everywhere.
	//
	//At a face centre chart.xy is zero and rootJacobian is one, so the whole term is multiplied by zero and the
	//arbitrary fallback axis cannot show — and it fades in continuously, since (rootJacobian - 1) goes to zero
	//with |chart.xy|.
	float chartRadius2 = dot(chart.xy, chart.xy);
	float2 radialDir = chartRadius2 > 1e-12 ? chart.xy * rsqrt(chartRadius2) : float2(1.0, 0.0);
	float rootJacobian = sqrt(jacobian);

	offset = rootJacobian * offset - (rootJacobian - 1.0) * dot(offset, radialDir) * radialDir;

	//Rotate into the galaxy's own axes and squash one of them: an ellipse on the sky is a disc seen at an
	//angle, and edge-on ones are what make a field of them read as galaxies rather than as fuzzy stars.
	float angle = rollB.y * 6.2831853;
	float2 axis = float2(cos(angle), sin(angle));
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

	//The volume last of the sky, and that ordering is what makes it read as being IN FRONT of all of it. It is
	//the only layer here with a position rather than just a direction, so it is the only one that can be
	//between the eye and the rest — everything above is at infinity by construction. Its extinction is applied
	//to the whole accumulated sky rather than to the stars alone: the Milky Way and the nebulae are behind it
	//too, and a web that dims the stars while the band shines through it undimmed reads as two skies.
	float volumeTransmittance;
	float3 volume = StarNestVolume(dir, CameraPosition, volumeTransmittance);

	sky = sky * volumeTransmittance + volume;

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
