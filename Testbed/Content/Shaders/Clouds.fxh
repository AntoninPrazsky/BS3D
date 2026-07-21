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

//The weather layer: two octaves drifting at different speeds, so the sky evolves instead of sliding past
//as one rigid sheet. Keep this in step with CloudField.Weather on the C# side.
float CloudWeather(float2 world)
{
	float2 drift = CloudWind * CloudTime;

	float w = 0.62 * CloudNoise((world + drift) * CloudScale);
	w += 0.38 * CloudNoise((world + drift * 1.6) * CloudScale * 2.7 + 31.4);

	return w;
}

//0 = clear sky, 1 = solid cloud.
float CloudCover(float2 world)
{
	return saturate((CloudWeather(world) + CloudCoverageBias) * CloudCoverageGain);
}

//The fine octaves. Each fades against its own wavelength using the pixel's footprint on the cloud plane,
//the way every other procedural feature in this project does - and here the fade earns its keep twice
//over, because towards the horizon the view ray runs nearly parallel to the plane, the footprint
//explodes, and the detail washes out into haze on its own without a special case for it.
float CloudDetail(float2 world, float footprint)
{
	float2 drift = CloudWind * CloudTime;

	float sum = 0.0;
	float amplitude = 0.5;

	//Starting only three times over the weather layer rather than six leaves no gap between the two:
	//with the fine octaves an octave too high there was nothing at all between the shape of a bank and
	//the grain on its edge, and a cloud is mostly made of what happens in between.
	float frequency = CloudScale * 3.0;

	[unroll]
	for (int octave = 0; octave < 5; octave++)
	{
		float resolvable = saturate(1.0 - footprint * frequency * 1.2);

		sum += amplitude * resolvable * CloudNoise((world + drift * (1.0 + octave * 0.4)) * frequency + octave * 17.3);

		//Falling off slower than the usual half leaves more of the sum in the fine octaves, which is
		//where the lobes along a cloud's edge come from; at a half the first octave drowns the rest and
		//the boundary comes out as a few big soft bulges.
		amplitude *= 0.62;
		frequency *= 2.13;
	}

	return sum;
}

//How deep the cloud is here, and deliberately **not** clamped: past 1 the difference between solid and
//very solid is the only thing left to shade a cloud's interior by, and clamping it is what turns a bank
//into a flat grey wash with a nice edge. Opacity clamps, shading must not.
float CloudThickness(float2 world, float footprint)
{
	float weather = (CloudWeather(world) + CloudCoverageBias) * CloudCoverageGain;

	return max(weather + CloudDetail(world, footprint) * CloudDetailStrength, 0.0);
}

//What covers the sky: the thickness clamped. Detail only moves the edges, since where the weather layer
//is already solid or already clear the clamp swallows it.
float CloudDensity(float2 world, float footprint)
{
	return saturate(CloudThickness(world, footprint));
}

//How much of the sun still reaches a point in the world. Branchless on purpose: the callers take
//screen-space derivatives further down, and those want every pixel of a quad to have walked one path.
float CloudSunlight(float3 worldPosition, float3 sunDirection)
{
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
