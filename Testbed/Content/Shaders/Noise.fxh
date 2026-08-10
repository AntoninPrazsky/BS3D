//The shared noise library: gradient noise in 2D and 3D, fractal Brownian motion with per-octave rotation,
//ridged fBm, and jittered cellular (Voronoi) distance. This exists because the look of a procedural scene
//is decided by the QUALITY of its fields before anything else: a sum of plane-wave sines - which is what
//the first fantasy scenes were built from - keeps its planes however many terms it has, and the eye reads
//it instantly as the plasma effect of a 1994 demo. Fractal noise with rotated octaves has no planes to
//keep; domain-warping it (feeding one field's output into another's input) is what produces filaments,
//eddies and mixing - the structures real turbulence has and the modern look is made of.
//
//GPU-only, deliberately: unlike Clouds.fxh these fields have NO C# mirror (nothing plants objects on them),
//so the hashes are free to be quality-first. Costs are stated per function because callers budget in noise
//evaluations per pixel; everything here is branchless and derivative-safe (usable where ddx/ddy live).
//
//Included by scene shaders; .fxh edits trigger a rebuild of every .fx that includes them (the Clouds.fxh
//precedent, verified there).

//--- Hashes (Dave Hoskins' "hash without sine") ------------------------------------------------------
//Sine-based hashes degrade as their argument grows and their period shows in large domains; these stay
//uniform over any range a scene reaches.

float2 NoiseHash22(float2 p)
{
	float3 q = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
	q += dot(q, q.yzx + 33.33);
	return frac((q.xx + q.yz) * q.zy) * 2.0 - 1.0;
}

float3 NoiseHash33(float3 p)
{
	p = frac(p * float3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return frac((p.xxy + p.yxx) * p.zyx) * 2.0 - 1.0;
}

//--- Gradient noise -----------------------------------------------------------------------------------
//Perlin-style: a random unit-ish gradient per lattice corner, dotted with the offset and blended by a
//QUINTIC fade (C2-continuous - the cubic's discontinuous second derivative shows as faint lattice creases
//once a field feeds a normal). Returns roughly -1..1.

float GradientNoise2(float2 p)
{
	float2 i = floor(p);
	float2 f = frac(p);
	float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

	return lerp(
		lerp(dot(NoiseHash22(i), f),
		     dot(NoiseHash22(i + float2(1.0, 0.0)), f - float2(1.0, 0.0)), u.x),
		lerp(dot(NoiseHash22(i + float2(0.0, 1.0)), f - float2(0.0, 1.0)),
		     dot(NoiseHash22(i + float2(1.0, 1.0)), f - float2(1.0, 1.0)), u.x), u.y);
}

float GradientNoise3(float3 p)
{
	float3 i = floor(p);
	float3 f = frac(p);
	float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

	return lerp(
		lerp(
			lerp(dot(NoiseHash33(i), f),
			     dot(NoiseHash33(i + float3(1, 0, 0)), f - float3(1, 0, 0)), u.x),
			lerp(dot(NoiseHash33(i + float3(0, 1, 0)), f - float3(0, 1, 0)),
			     dot(NoiseHash33(i + float3(1, 1, 0)), f - float3(1, 1, 0)), u.x), u.y),
		lerp(
			lerp(dot(NoiseHash33(i + float3(0, 0, 1)), f - float3(0, 0, 1)),
			     dot(NoiseHash33(i + float3(1, 0, 1)), f - float3(1, 0, 1)), u.x),
			lerp(dot(NoiseHash33(i + float3(0, 1, 1)), f - float3(0, 1, 1)),
			     dot(NoiseHash33(i + float3(1, 1, 1)), f - float3(1, 1, 1)), u.x), u.y), u.z);
}

//--- Fractal Brownian motion --------------------------------------------------------------------------
//Octaves of gradient noise, each about twice the frequency and half the amplitude of the last, the domain
//ROTATED between octaves - without the rotation the octaves' lattices align and their common axes read as
//a grid through the finished field. The octave count is a literal at every call site, so the compiler
//unrolls the loop after inlining. Cost: one gradient-noise evaluation per octave.

static const float3x3 NOISE_ROTATE3 = float3x3(
	0.00, 0.80, 0.60,
	-0.80, 0.36, -0.48,
	-0.60, -0.48, 0.64);

static const float2x2 NOISE_ROTATE2 = float2x2(0.80, 0.60, -0.60, 0.80);

float Fbm2(float2 p, int octaves)
{
	float value = 0.0;
	float amplitude = 0.5;

	for (int i = 0; i < octaves; i++)
	{
		value += amplitude * GradientNoise2(p);
		p = mul(NOISE_ROTATE2, p) * 2.02;
		amplitude *= 0.5;
	}

	return value;
}

//Fbm2 with each octave faded out as its own period approaches the pixel, so a field drawn on something
//SMALL on screen loses its detail rather than crawling. `footprint` is the pixel's size in the field's own
//units - the domain p is sampled at - so a caller that scales its domain by a frequency scales its
//footprint by the same frequency; take it from ddx/ddy of that domain, outside any divergent branch.
//An octave of period 1/frequency needs a footprint under half of it, and the fade reaches zero exactly
//there. Note this costs the field VARIANCE with distance, which is the point: the mottle flattens out
//instead of turning into a shimmer. Cost: Fbm2 plus two ALU an octave.

float Fbm2BandLimited(float2 p, int octaves, float footprint)
{
	float value = 0.0;
	float amplitude = 0.5;
	float frequency = 1.0;

	for (int i = 0; i < octaves; i++)
	{
		value += amplitude * saturate(1.0 - 2.0 * frequency * footprint) * GradientNoise2(p);
		p = mul(NOISE_ROTATE2, p) * 2.02;
		frequency *= 2.02;
		amplitude *= 0.5;
	}

	return value;
}

//Fbm2BandLimited on a domain STRETCHED along `along`, so the field has a GRAIN. Isotropic noise has none by
//construction, and a surface relief without one reads as gravel rather than as anything lying over - which
//is exactly what is missing when a family of plane-wave sines is replaced by noise, since one of those sines
//always dominated and supplied a direction for free. `stretch` is how many times longer a feature is along
//the axis than across it; 1 is plain Fbm2BandLimited. `along` need not be normalised, and a zero vector is
//legal (it falls back to +X rather than to the NaN normalize() would give, which would take the whole
//surface's shading with it).
//
//The footprint is the caller's as usual, and the stretch only ever makes the domain COARSER along one axis,
//so the caller's unstretched footprint stays an honest bound for both. Cost: Fbm2BandLimited plus a
//normalize and two dots.
float Fbm2Combed(float2 p, float2 along, float stretch, int octaves, float footprint)
{
	float2 axis = dot(along, along) > 1e-6 ? normalize(along) : float2(1.0, 0.0);
	float2 across = float2(-axis.y, axis.x);

	return Fbm2BandLimited(float2(dot(p, axis) / stretch, dot(p, across)), octaves, footprint);
}

float Fbm3(float3 p, int octaves)
{
	float value = 0.0;
	float amplitude = 0.5;

	for (int i = 0; i < octaves; i++)
	{
		value += amplitude * GradientNoise3(p);
		p = mul(NOISE_ROTATE3, p) * 2.02;
		amplitude *= 0.5;
	}

	return value;
}

//--- Ridged fBm ----------------------------------------------------------------------------------------
//1 - |noise| per octave, squared so the ridge lines sharpen, each octave weighted by the last's value so
//detail gathers ON the ridges (Musgrave's trick). This is the field for crests, cracks, filaments and
//strata - anywhere the structure is a NETWORK of lines rather than a mottle. Returns roughly 0..1.

float RidgedFbm2(float2 p, int octaves)
{
	float value = 0.0;
	float amplitude = 0.5;
	float weight = 1.0;

	for (int i = 0; i < octaves; i++)
	{
		float ridge = 1.0 - abs(GradientNoise2(p));
		ridge *= ridge * weight;
		weight = saturate(ridge * 2.0);

		value += ridge * amplitude;
		p = mul(NOISE_ROTATE2, p) * 2.02;
		amplitude *= 0.5;
	}

	return value;
}

float RidgedFbm3(float3 p, int octaves)
{
	float value = 0.0;
	float amplitude = 0.5;
	float weight = 1.0;

	for (int i = 0; i < octaves; i++)
	{
		float ridge = 1.0 - abs(GradientNoise3(p));
		ridge *= ridge * weight;
		weight = saturate(ridge * 2.0);

		value += ridge * amplitude;
		p = mul(NOISE_ROTATE3, p) * 2.02;
		amplitude *= 0.5;
	}

	return value;
}

//--- Cellular (Voronoi) distance ------------------------------------------------------------------------
//Distance to the nearest of a jittered lattice of sites: the field whose LOW values form closed cells and
//whose ridges form the bright web between them - water caustics, cracked earth, cell walls. The jitter
//stays at 0.4 so a site cannot leave the 3x3 neighbourhood the loop reads. Cost: nine hashes.

float Voronoi2(float2 p)
{
	float2 i = floor(p);
	float2 f = frac(p);
	float best = 8.0;

	[unroll]
	for (int y = -1; y <= 1; y++)
	{
		[unroll]
		for (int x = -1; x <= 1; x++)
		{
			float2 cell = float2(x, y);
			float2 site = cell + 0.5 + 0.4 * NoiseHash22(i + cell);
			float2 toSite = f - site;
			best = min(best, dot(toSite, toSite));
		}
	}

	return sqrt(best);
}

//The distance to the second-nearest site LESS the nearest (F2 - F1): zero exactly on the borders between
//cells, so 1 - saturate(k * edge) lights the WEB between cells rather than their centres - which is what
//water caustics are: the bright net where wavelets focus, running around dark cells.
float VoronoiEdge2(float2 p)
{
	float2 i = floor(p);
	float2 f = frac(p);
	float best = 8.0;
	float second = 8.0;

	[unroll]
	for (int y = -1; y <= 1; y++)
	{
		[unroll]
		for (int x = -1; x <= 1; x++)
		{
			float2 cell = float2(x, y);
			float2 site = cell + 0.5 + 0.4 * NoiseHash22(i + cell);
			float2 toSite = f - site;
			float d = dot(toSite, toSite);

			second = d < best ? best : min(second, d);
			best = min(best, d);
		}
	}

	return sqrt(second) - sqrt(best);
}
