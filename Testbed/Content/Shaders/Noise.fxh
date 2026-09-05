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
//Included by the scene shaders and - since #337, for the ice ball's fracture - by InstancedModel.fx;
//.fxh edits trigger a rebuild of every .fx that includes them (the Clouds.fxh precedent, verified there).

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

//A GUST FIELD: irregular patches of wind, drawn out along it, running downwind (#276). One copy for the four
//ground scenes that need one, for the reason Fbm2Combed above is one copy — the meadow, the savanna and the
//forest each carried the same wind line, character for character, and it was wrong in all three.
//
//WHAT IT REPLACES, because the shape of the mistake is the argument for this function. The wind used to be
//`sin(dot(xz, WindDirection) * frequency + time * speed)`: an infinite plane wave, dead straight, perfectly
//regular, marching at a constant speed for ever. At the authored frequency of 0.15 its wavelength is
//2π/0.15 ≈ **42 world units — wider than the visible field**, so what the player saw was not bands combing
//the grass but the whole ground brightening and darkening under one straight edge sweeping across it. The
//owner reported it as "a strange creeping pattern with no obvious cause", which is exactly what a plane wave
//is: nothing outdoors is that regular, so the eye reads it as an artefact rather than as weather.
//
//A gust is the shape wind actually has over a field — a patch, irregular, longer along the wind than across
//it, travelling downwind and fading. ONE octave of combed noise gives that for about the price of the sine
//it replaces, and being a FIELD rather than a phase it can drive more than brightness: the callers lean
//their relief by the same value they shade with, so the grass bends where the gust is and springs back
//behind it, and the two motions cannot disagree because they are one field.
//
//`frequency` is the reciprocal of a gust's size (0.15 puts one at ~7 world units, not 42), `speed` is how
//fast it travels in world units a second, and the result is roughly [-1, 1] with the typical excursion well
//inside it. The domain is sampled UPWIND of the caller so the pattern travels ALONG `wind`.
static const float WIND_GUST_STRETCH = 4.0;   //a gust is four times longer along the wind than across it
static const float WIND_GUST_GAIN = 3.9;      //one octave rarely leaves ±0.25 on its own; this fills the range

//⚠ ONE OCTAVE, AND THAT IS A MEASURED CEILING RATHER THAN A TASTE (#276). At TWO this fell off an occupancy
//cliff in the two scenes that were already the dearest, and not by a little: on the reference desktop
//(6900XT, Testbed, fixed camera, 1600×900 at ssaa 4, fpscap 400, median of 17 readings) the savanna went
//8.496 → 27.933 ms and the forest 12.788 → 28.011, while the meadow — the same addition, in a cheaper
//shader — went 7.746 → 8.097 and merely started dipping to 28 now and then. Dropped back to one octave, all
//three come home: meadow 7.949, savanna 8.547, forest 12.953, i.e. 0.05–0.20 ms for the whole effect.
//
//It is the same wall `Forest.fx` documents for its own two extras ("cutting either one ALONE saves NOTHING")
//read from the other side: these passes are occupancy-bound, so cost does not scale with work — it steps
//when the shader crosses a register threshold, and a 3.3× frame is what one extra noise evaluation bought.
//**Anything added here has to be re-measured on the savanna and the forest, not on the meadow**, and a
//second octave is not available at any price a scene shader can pay.

float WindGust(float2 xz, float2 wind, float time, float frequency, float speed, float footprint)
{
    float2 p = (xz - wind * (time * speed)) * frequency;

    return clamp(Fbm2Combed(p, wind, WIND_GUST_STRETCH, 1, footprint * frequency) * WIND_GUST_GAIN, -1.0, 1.0);
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

//--- Cellular in three dimensions ------------------------------------------------------------------------
//The same borders-between-cells field on a 3D lattice, and the reason it exists rather than a 2D lookup is
//that a SPHERE has no seamless 2D parameterisation: an azimuth/elevation pair pinches at both poles and
//tears along the atan2 branch cut, and on a ball those three defects sit exactly where the silhouette is.
//Evaluated on an object-space direction this is continuous everywhere on the sphere with nothing to hide.
//
//Returns BOTH halves, because a caller that wants a fracture wants both and the second is nearly free:
//  x = F2 - F1, zero exactly on a border, so 1 - smoothstep(0, w, x) is the web BETWEEN cells;
//  y = the nearest site's own random value in 0..1, which NAMES the cell - what lets each plate of a
//      fracture be shaded differently, a figure the size of a plate rather than of a hairline.
//The jitter stays at 0.4 for the 2D field's reason: a site cannot then leave the 3x3x3 the loop reads.
//Cost: 27 hashes, three times the 2D field's - which is what the third axis costs, and is why a caller
//budgets this as its whole pattern rather than as one term of it.
float2 VoronoiEdgeCell3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float best = 8.0;
    float second = 8.0;
    float bestCell = 0.0;

    [unroll]
    for (int z = -1; z <= 1; z++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int x = -1; x <= 1; x++)
            {
                float3 cell = float3(x, y, z);
                float3 hash = NoiseHash33(i + cell);
                float3 toSite = f - (cell + 0.5 + 0.4 * hash);
                float d = dot(toSite, toSite);

                //All three tests read the OLD best, so they are written before it is updated - the site's
                //identity travels with the winning distance rather than being looked up again afterwards.
                bestCell = d < best ? frac(hash.x * 7.13 + hash.y * 3.71 + hash.z * 11.9) : bestCell;
                second = d < best ? best : min(second, d);
                best = min(best, d);
            }
        }
    }

    return float2(sqrt(second) - sqrt(best), bestCell);
}

//===================================================================================================
//THE HEIGHT FIELD'S NORMAL, IN ONE COPY (#297)
//
//Tilts a normal by a height field using only screen-space derivatives, for the same reason
//InstancedModel.fx CotangentFrame exists: the instance streams carry no tangents and the object-to-world
//rotation never reaches a pixel shader, so the frame is rebuilt from the screen-space derivatives of the
//position and of the height. Christian Schueler, "Bump Mapping Unparametrized Surfaces on the GPU".
//
//IT LIVES HERE BECAUSE IT WAS COPIED TWELVE TIMES. The audit #297 asked for found this function
//character-for-character in eleven scene shaders and in InstancedModel.fx - and, more to the point, found
//that the eleven were NOT all the same function: the mountains and the volcano carried a guard the other
//nine and the instanced shader did not, added where somebody noticed the fault and nowhere else. That is
//the exact shape #297 exists to attack, so the guarded version is now the only version. Every file that
//had a copy already includes this header, so nothing gained an include line for it.
//
//THE GUARD is the determinant floor. At a grazing angle ddx/ddy of the world position go near-degenerate,
//the determinant collapses to zero, and normalize(0) is NaN - which reaches the frame as a black or
//white speck on a far surface. It was found on the mountains' far peaks and it was never theirs alone:
//the dunes, the sea, the moon's regolith and the island's own stone are all height fields seen at
//grazing angles.
float3 PerturbNormalFromHeight(float3 normal, float3 worldPosition, float height)
{
    float3 dpdx = ddx(worldPosition);
    float3 dpdy = ddy(worldPosition);

    float3 r1 = cross(dpdy, normal);
    float3 r2 = cross(normal, dpdx);

    float determinant = dot(dpdx, r1);
    float3 surfaceGradient = sign(determinant) * (ddx(height) * r1 + ddy(height) * r2);

    return normalize(max(abs(determinant), 1e-4) * normal - surfaceGradient);
}
