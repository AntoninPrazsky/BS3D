//One cloud field, three consumers.
//
//The clouds live on a flat plane at a finite altitude rather than as a texture painted on the sky dome,
//and that is the decision the whole feature rests on. A dome texture sits at infinity and casts no
//shadow anybody can derive from it, so the clouds you look at and the shadows crossing the ground would
//have to be two separate fields - and two fields drift apart, which is precisely what makes this kind of
//effect feel fake. With a plane there is one field and three ray-plane intersections into it:
//
//  - the sky shader crosses it with the view ray, and draws what it finds there,
//  - the scene shader crosses it with the sun ray, and darkens the key light by what it finds there,
//  - the CPU crosses it with the sun ray from the arena, and dims the whole light rig by the same.
//
//So the cloud overhead is the cloud that shadows you, by construction rather than by tuning.
//
//The field is split in two. The *weather* layer is coarse, cheap and evaluated identically on the CPU
//and the GPU; it decides where cloud is at all, and it is the only part the shadow and the light rig
//ever look at. The *detail* layer is finer, lives only in the sky shader, and only erodes the edges the
//weather layer drew. That split is what removes the usual CPU/GPU noise-synchronisation problem: the
//CPU never evaluates anything but two octaves of gradient noise, which is trivially reproducible, and
//the octaves it cannot reproduce are the ones nothing but the visible sky depends on.

//Height of the cloud plane in world Y, and how far one world unit is along the noise
float CloudPlaneY;
float CloudScale;

//Wind, in world units per second, and the clock driving it
float2 CloudWind;
float CloudTime;

//Where the coverage threshold sits and how sharply the field crosses it. Bias is the dial for
//"how cloudy is it": negative clears the sky, positive closes it over.
float CloudCoverageBias;
float CloudCoverageGain;

//How hard the fine octaves chew at the edges the weather layer drew
float CloudDetailStrength;

//Least amount of sun that reaches through the thickest cloud, and how fast the shadow deepens.
//Never zero: cloud scatters far too much light for its shadow to be a hole.
float CloudShadowFloor;
float CloudShadowGain;

//Hash and gradient noise. Built out of frac/dot/multiply only - no sin - because the weather layer has
//to come out bit-for-bit the same in C#, and a sine-based hash is exactly where the two would part ways.
float2 CloudHash22(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, float3(p3.y, p3.z, p3.x) + 33.33);

    return frac((float2(p3.x, p3.x) + float2(p3.y, p3.z)) * float2(p3.z, p3.y)) * 2.0 - 1.0;
}

//Gradient noise rather than value noise: value noise betrays its grid as a faint square weave, and on
//something as large and as slow as a sky there is nothing else for the eye to look at.
float CloudNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = p - cell;

    //Quintic, so the second derivative is continuous too - clouds are shaded off this field's slope
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float a = dot(CloudHash22(cell + float2(0, 0)), f - float2(0, 0));
    float b = dot(CloudHash22(cell + float2(1, 0)), f - float2(1, 0));
    float c = dot(CloudHash22(cell + float2(0, 1)), f - float2(0, 1));
    float d = dot(CloudHash22(cell + float2(1, 1)), f - float2(1, 1));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

//Gradient noise again, carrying its analytic derivative (value in x, d/dworld in yz). The sky shader
//shades the deck off this field's slope, and an analytic derivative is both cheaper and cleaner than
//re-evaluating the field three times: the finite-difference version pays two full extra fbm walks and
//still hands back a derivative one step stale. GPU-only - the CPU mirror never shades, so it never
//needs this, and CloudNoise above stays the one function the two sides keep in step.
float3 CloudNoiseD(float2 p)
{
    float2 cell = floor(p);
    float2 f = p - cell;

    //Quintic and its derivative - the same fade CloudNoise uses, or the two would disagree
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);

    float2 ga = CloudHash22(cell + float2(0, 0));
    float2 gb = CloudHash22(cell + float2(1, 0));
    float2 gc = CloudHash22(cell + float2(0, 1));
    float2 gd = CloudHash22(cell + float2(1, 1));

    float va = dot(ga, f - float2(0, 0));
    float vb = dot(gb, f - float2(1, 0));
    float vc = dot(gc, f - float2(0, 1));
    float vd = dot(gd, f - float2(1, 1));

    float3 result;
    result.x = va + u.x * (vb - va) + u.y * (vc - va) + u.x * u.y * (va - vb - vc + vd);
    result.yz = ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd)
        + du * (float2(u.y, u.x) * (va - vb - vc + vd) + float2(vb, vc) - va);

    return result;
}

//The weather layer: two octaves drifting at different speeds, so the sky evolves instead of sliding past
//as one rigid sheet. Keep this in step with CloudField.Weather on the C# side.
float CloudWeather(float2 world)
{
    float2 drift = CloudWind * CloudTime;

    float w = 0.62 * CloudNoise((world + drift) * CloudScale);
    w += 0.38 * CloudNoise((world + drift * 1.6) * CloudScale * 2.7 + 31.4);

    return w;
}

//The weather layer with its slope - the same two octaves as CloudWeather, value-identical by
//construction (same taps, same weights), plus the derivative the shading wants. Sky-shader only:
//nothing the CPU or the shadow reads ever touches a derivative.
float3 CloudWeatherD(float2 world)
{
    float2 drift = CloudWind * CloudTime;

    float3 n1 = CloudNoiseD((world + drift) * CloudScale);
    float3 n2 = CloudNoiseD((world + drift * 1.6) * CloudScale * 2.7 + 31.4);

    return float3(
        0.62 * n1.x + 0.38 * n2.x,
        0.62 * CloudScale * n1.yz + 0.38 * CloudScale * 2.7 * n2.yz);
}

//0 = clear sky, 1 = solid cloud.
float CloudCover(float2 world)
{
    return saturate((CloudWeather(world) + CloudCoverageBias) * CloudCoverageGain);
}

//How much the character field below swings the detail strength either way. A character of one kind of
//cloud everywhere is precisely what made the deck read as one material: every bank had the same grain,
//so the eye had nothing to tell one cloud from the next by.
float CloudCharacterStrength;

//The fine octaves, with the slope of the first four. Each fades against its own wavelength using the
//pixel's footprint on the cloud plane, the way every other procedural feature in this project does - and
//here the fade earns its keep twice over, because towards the horizon the view ray runs nearly parallel
//to the plane, the footprint explodes, and the detail washes out into haze on its own without a special
//case for it.
//
//The derivative deliberately stops at the fourth octave: a gradient scales with its octave's frequency,
//so summed to the end the finest grain would own the whole slope and the shaded form would fizz - the
//normal wants the shape of the lobes, the value wants the grain on them, and the two want different
//parts of the spectrum. (The same reasoning as iq's fbmd_8, which accumulates half its octaves' values
//and only a quarter of their derivatives.)
float3 CloudDetailD(float2 world, float footprint)
{
    float2 drift = CloudWind * CloudTime;

    float3 sum = float3(0.0, 0.0, 0.0);
    float amplitude = 0.5;

    //Starting only three times over the weather layer rather than six leaves no gap between the two:
    //with the fine octaves an octave too high there was nothing at all between the shape of a bank and
    //the grain on its edge, and a cloud is mostly made of what happens in between.
    float frequency = CloudScale * 3.0;

    //Seven octaves, not five: the fifth bottomed out at a wavelength of ~7 world units, which from the
    //arena subtends tens of pixels - the deck's "low resolution" read was exactly this. Two more take
    //the grain to ~1.5 units, and the footprint fade already owns the question of where they may show.
    [unroll]
    for (int octave = 0; octave < 7; octave++)
    {
        float resolvable = saturate(1.0 - footprint * frequency * 1.2);

        float3 n = CloudNoiseD((world + drift * (1.0 + octave * 0.4)) * frequency + octave * 17.3);

        sum.x += amplitude * resolvable * n.x;
        if (octave < 4) sum.yz += amplitude * resolvable * frequency * n.yz;

        //Falling off slower than the usual half leaves more of the sum in the fine octaves, which is
        //where the lobes along a cloud's edge come from; at a half the first octave drowns the rest and
        //the boundary comes out as a few big soft bulges.
        amplitude *= 0.62;
        frequency *= 2.13;
    }

    return sum;
}

//How deep the cloud is here (x) and which way the field slopes (yz), and the depth deliberately **not**
//clamped: past 1 the difference between solid and very solid is the only thing left to shade a cloud's
//interior by, and clamping it is what turns a bank into a flat grey wash with a nice edge. Opacity
//clamps, shading must not.
//
//The character tap is what makes two banks in one sky different animals: a third, very low octave -
//far below the weather layer, so it selects whole clouds rather than patches of one - swings the detail
//strength about its authored value, and a bank the character smiles on comes out shredded and fibrous
//where its neighbour stays rounded and dense. GPU-only like the detail: nothing the shadow or the light
//rig reads ever sees it.
float3 CloudThicknessD(float2 world, float footprint)
{
    float3 weather = CloudWeatherD(world);
    float3 detail = CloudDetailD(world, footprint);

    float character = CloudNoise((world + CloudWind * CloudTime * 0.8) * CloudScale * 0.37 + 113.7);
    float strength = CloudDetailStrength * (1.0 + CloudCharacterStrength * character);

    float thickness = (weather.x + CloudCoverageBias) * CloudCoverageGain + detail.x * strength;

    return float3(max(thickness, 0.0), weather.yz * CloudCoverageGain + detail.yz * strength);
}

//How much of the sun still reaches a point in the world. Branchless on purpose past the early-out: the
//callers take screen-space derivatives further down, and those want every pixel of a quad to have walked
//one path.
float CloudSunlight(float3 worldPosition, float3 sunDirection)
{
    //No clouds configured (the map editor never sets the uniforms, so the gain sits at 0): the answer is
    //a flat 1 and the noise below would be evaluated only to be multiplied away. A branch on a uniform is
    //non-divergent — every pixel takes the same path — and there are no gradient ops in this function, so
    //the derivative-coherence concern above does not apply to it.
    [branch]
    if (CloudCoverageGain <= 0.0) return 1.0;

    //Guarded rather than branched. A sun on the horizon would send the ray along the plane for an
    //unbounded distance, which is meaningless as a shadow lookup and noisy as a number.
    float climb = max(sunDirection.y, 0.05);
    float distanceToPlane = max((CloudPlaneY - worldPosition.y) / climb, 0.0);

    float2 hit = worldPosition.xz + sunDirection.xz * distanceToPlane;

    //The weather layer alone, deliberately. A cloud shadow thrown from that height has a penumbra
    //hundreds of units wide, so the fine detail would not survive the trip anyway - and leaving it out
    //is also what keeps the shadow in step with the coarse field the CPU dims the light rig by.
    return lerp(1.0, CloudShadowFloor, saturate(CloudCover(hit) * CloudShadowGain));
}
