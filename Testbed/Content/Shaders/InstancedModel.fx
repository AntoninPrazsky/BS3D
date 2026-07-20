//Draws many instances of a rigid model in a single draw call per mesh part.
//The per-instance world matrix is supplied through a second vertex stream (TEXCOORD1-TEXCOORD4 hold its rows).
//Lighting replicates BasicEffect with EnableDefaultLighting and per-pixel (Blinn-Phong) shading,
//so instanced models look the same as those rendered through ModelRenderer.

//Testbed builds this for DirectX and gets Shader Model 5.0; the map editor still builds the very same
//file for DesktopGL, where MojoShader caps out at 3.0. Anything written below that 5.0 allows and 3.0
//does not has to be guarded by #if OPENGL, or the map editor stops compiling.
#if OPENGL
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

//Every color that enters the lighting math - material colors, light colors, sky palette, texture
//samples - is authored or stored in sRGB, which is a display encoding, not a quantity of light.
//Adding, multiplying and averaging those numbers is only meaningful once they are back in linear
//radiance; Tonemap.fx encodes the result for the display again at the very end of the frame.
//
//The map editor builds this same file for DesktopGL and has no tonemapping pass to encode back with,
//so there this stays a no-op and the editor keeps working in gamma space exactly as before.
float3 SrgbToLinear(float3 color)
{
#if OPENGL
	return color;
#else
	//Jim Hejl's cubic fit of the sRGB curve - accurate to well under a display bit and no pow()
	return color * (color * (color * 0.305306011 + 0.682171111) + 0.012522878);
#endif
}

float4x4 View;
float4x4 Projection;

//Absolute transform of the mesh parent bone, applied before the per-instance world matrix
float4x4 Bone;

float3 EyePosition;

//Material of the mesh part being drawn
float4 DiffuseColor;
float3 EmissiveColor;
//Premultiplied on the CPU: ambient tint * material diffuse. Modulated per pixel by the sky hemisphere below.
float3 AmbientColor;
float3 SpecularColor;
float SpecularPower;

//Hemisphere ambient palette taken from the current sky dome: upward-facing surfaces receive SkyColor,
//downward-facing ones GroundColor. Both arrive in LINEAR radiance - Prazsky.Core.Tools.ColorSpace
//decodes them on the CPU, because the scales and tints applied to them there are multiplications and
//those mean nothing in a display encoding. Neither is clamped to 1: a bright sky does exceed white.
float3 SkyColor;
float3 GroundColor;

//Y of the ground plane, for the ground-contact part of the ambient occlusion
float GroundHeight;

//The key light is positional (a "sun" placed in the scene): its direction differs per surface point,
//so every ball is lit according to where it sits relative to the light instead of all balls looking identical.
float3 KeyLightPosition;
float3 DirLight0DiffuseColor;
float3 DirLight0SpecularColor;

float3 DirLight1Direction;
float3 DirLight1DiffuseColor;
float3 DirLight1SpecularColor;

float3 DirLight2Direction;
float3 DirLight2DiffuseColor;
float3 DirLight2SpecularColor;

//Material texture of the mesh part (InstancedModelTextured) or the world-space detail
//texture (InstancedModelTriplanar)
texture Texture;
sampler2D TextureSampler = sampler_state
{
	Texture = <Texture>;
	//Anisotropic, not plain trilinear: a pixel on the ground seen at a grazing angle covers a long thin
	//sliver of texture, and isotropic mip selection has to pick the mip matching its *long* axis, so it
	//blurs across the short one too and the floor dissolves into a smear. The ground is the surface this
	//shows on worst, being the one the camera always looks along.
	MinFilter = Anisotropic;
	MagFilter = Linear;
	MipFilter = Linear;
	MaxAnisotropy = 16;
	AddressU = Wrap;
	AddressV = Wrap;
};

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
};

struct InstanceInput
{
	float4 WorldRow1 : TEXCOORD1;
	float4 WorldRow2 : TEXCOORD2;
	float4 WorldRow3 : TEXCOORD3;
	float4 WorldRow4 : TEXCOORD4;
	//XYZ = world-space direction towards the instance's occluders (zero = none), W = base occlusion factor
	float4 Custom : TEXCOORD5;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
	float4 OcclusionData : TEXCOORD2;
};

VertexShaderOutput MainVS(VertexShaderInput input, InstanceInput instance)
{
	VertexShaderOutput output;

	//Rows are stored in the same layout as an XNA row-major matrix, so no transpose is needed
	float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

	float4 worldPosition = mul(mul(input.Position, Bone), world);

	output.WorldPosition = worldPosition.xyz;
	output.Position = mul(mul(worldPosition, View), Projection);
	//Bone and instance transforms are rotation + translation (+ uniform scale at most), so the adjoint transpose is not needed
	output.WorldNormal = mul(mul(float4(input.Normal, 0), Bone), world).xyz;
	output.OcclusionData = instance.Custom;

	return output;
}

//Same per-light math as ComputeLights in BasicEffect.fx (Blinn-Phong with the dotL > 0 mask)
void AddLight(float3 towardsLight, float3 lightDiffuse, float3 lightSpecular, float3 worldNormal, float3 eyeVector,
	inout float3 diffuse, inout float3 specular)
{
	float dotL = dot(worldNormal, towardsLight);
	float lit = step(0, dotL);

	diffuse += lightDiffuse * (dotL * lit);

	float dotH = max(dot(worldNormal, normalize(towardsLight + eyeVector)), 0);
	specular += lightSpecular * pow(dotH * lit, SpecularPower);
}

//Radiance arriving from the sky in a given direction. The domes are vertical gradients between two
//vertex colors and nothing else, so the environment can be evaluated in closed form instead of being
//baked into a cubemap: for a gradient, a prefiltered cubemap would only reproduce this expression at
//lower resolution. It is the same function for the diffuse ambient (sampled along the normal) and for
//the specular ambient (sampled along the reflection).
float3 SkyRadiance(float3 direction)
{
	return lerp(GroundColor, SkyColor, direction.y * 0.5 + 0.5);
}

//How much of the environment a surface mirrors back at this angle. Schlick's approximation: every
//dielectric turns mirror-like at a grazing angle, which is why a stone floor picks up the sky along it
//and why polished marble reads as polished at all. Nothing in the renderer said this before.
float3 FresnelSchlick(float3 reflectanceAtNormal, float cosTheta)
{
	return reflectanceAtNormal + (1 - reflectanceAtNormal) * pow(1 - saturate(cosTheta), 5);
}

//How strongly the surface reflects the sky as an environment (0 = off)
float SpecularAmbientStrength;

//Normal-incidence reflectance of a dielectric. Stone, marble, glass, vinyl, paint - everything in this
//scene that is not bare metal - reflects roughly this fraction of what hits it head-on.
static const float DielectricF0 = 0.04;

//How strongly the directional part of the occlusion darkens the surface facing the occluders
static const float DirectionalOcclusionStrength = 1.1;

//Ground-contact occlusion: how strongly the downward-facing side of a ball darkens near the ground,
//and over how many world units above the ground the effect fades out
static const float GroundOcclusionStrength = 0.55;
static const float GroundOcclusionRange = 2.0;

//How much ambient light reaches a surface point. Shared with the pattern technique's rim sheen,
//which has to stay dark on a ball buried in the pile.
float SurfaceOcclusion(float3 worldPosition, float3 worldNormal, float4 occlusionData)
{
	//Neighbor-based ambient occlusion: the base factor darkens the whole ball a little, the directional
	//part darkens the side of the ball facing its occluders, so the crevices between touching balls go dark
	float occlusion = saturate(occlusionData.w - DirectionalOcclusionStrength * max(0, dot(worldNormal, occlusionData.xyz)));

	//The ground is one more occluder: downward-facing surface close to the ground plane darkens
	float groundProximity = saturate(1 - (worldPosition.y - GroundHeight) / GroundOcclusionRange);

	return saturate(occlusion - GroundOcclusionStrength * groundProximity * saturate(-worldNormal.y));
}

//Shared shading: texColor is the sampled material texture (white for untextured parts).
//Like BasicEffect, the texture modulates the whole non-specular color (diffuse, ambient and emissive).
//keyShadow attenuates the key light alone - it is what the relief's own bumps block - while cavity
//attenuates the ambient, which is the sky a pit cannot see. Surfaces with no relief pass 1 for both.
float4 ShadePixel(float3 worldPosition, float3 rawWorldNormal, float4 occlusionData, float4 texColor, float keyShadow, float cavity)
{
	float3 worldNormal = normalize(rawWorldNormal);
	float3 eyeVector = normalize(EyePosition - worldPosition);

	//The key light is accumulated on its own so the relief's self-shadow can be applied to it without
	//touching the fill and back lights, which stand in for bounced light and are not blocked by a bump
	float3 keyDiffuse = 0;
	float3 keySpecular = 0;

	//The rig arrives linear, decoded once on the CPU along with the tints applied to it
	AddLight(normalize(KeyLightPosition - worldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, worldNormal, eyeVector, keyDiffuse, keySpecular);

	float3 diffuse = keyDiffuse * keyShadow;
	float3 specular = keySpecular * keyShadow;

	AddLight(-DirLight1Direction, DirLight1DiffuseColor, DirLight1SpecularColor, worldNormal, eyeVector, diffuse, specular);
	AddLight(-DirLight2Direction, DirLight2DiffuseColor, DirLight2SpecularColor, worldNormal, eyeVector, diffuse, specular);

	float3 hemisphere = SkyRadiance(worldNormal);

	float occlusion = SurfaceOcclusion(worldPosition, worldNormal, occlusionData) * cavity;
	float diffuseOcclusion = lerp(0.6, 1.0, occlusion);

	//texColor arrives linear already: every sampling site linearizes at the tap, where the sRGB
	//encoding of the texture is still an established fact rather than an assumption
	float4 color = float4((diffuse * SrgbToLinear(DiffuseColor.rgb) * diffuseOcclusion + hemisphere * SrgbToLinear(AmbientColor) * occlusion + SrgbToLinear(EmissiveColor)) * texColor.rgb, DiffuseColor.a * texColor.a);

	float3 linearSpecular = SrgbToLinear(SpecularColor);
	color.rgb += specular * linearSpecular * color.a * occlusion;

	//Specular ambient: the sky reflected off the surface, which the renderer simply never had. The
	//direct lights gave every material one highlight from one lamp, and that is a plastic look no
	//matter how the highlight is shaped - real surfaces mostly show their surroundings.
	//
	//Roughness comes from the Blinn-Phong exponent so no material has to be re-authored to get this:
	//sqrt(2 / (n + 2)) is the standard correspondence. It lerps the mirror sample towards the average
	//of the whole sky, which is what blurring a two-color gradient converges to.
	float roughness = sqrt(2.0 / (SpecularPower + 2.0));
	float3 reflection = reflect(-eyeVector, worldNormal);
	float3 environment = lerp(SkyRadiance(reflection), (SkyColor + GroundColor) * 0.5, saturate(roughness));

	//F0 is the fraction reflected head-on, and for every non-metal that is about 4%. BasicEffect's
	//SpecularColor is a highlight tint rather than a reflectance - it is near white on most materials -
	//so it modulates that 4% instead of standing in for it. Handing it to Schlick directly makes F come
	//out near 1 at every angle, which mirrors the entire sky off every surface and veils the scene.
	//The Fresnel rise to 1 at grazing angles is then the whole effect, which is as it should be.
	float3 reflectanceAtNormal = DielectricF0 * linearSpecular;

	color.rgb += environment * FresnelSchlick(reflectanceAtNormal, dot(worldNormal, eyeVector)) * SpecularAmbientStrength * color.a * occlusion;

	return color;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	//Untextured, unrelieved parts: nothing to shadow itself and no pits to darken
	return ShadePixel(input.WorldPosition, input.WorldNormal, input.OcclusionData, float4(1, 1, 1, 1), 1, 1);
}

technique InstancedModel
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};

//Procedural surface relief, shared by the ball pattern and by the scene objects. Nothing here moves a
//vertex: the height field only tilts the normal, so silhouettes stay exactly as modeled and what
//changes is that a surface catches light unevenly, the way a real material does.

//Peak height of the relief on the scene objects, in world units (0 = flat shading), and the base wave
//count per world unit — larger is finer grained. Four more octaves ride on top at rising frequencies.
float SurfaceReliefStrength;
float SurfaceReliefFrequency;

//Floor slabs: joints cut into the horizontal plane, in world units. SlabSize 0 turns them off.
//These exist so the relief has something at a scale the eye can actually see. Micro-relief alone is
//sub-centimeter, and neither parallax nor self-shadowing has anything to bite on at that size - they
//need real structure, and on a marble floor the structure is the joints between the slabs.
float SlabSize;
float SlabJointWidth;
float SlabJointDepth;

//How dark the pits of the relief go from being shaded by their own walls (0 = off)
float CavityStrength;

//How strongly the relief shadows itself along the key light (0 = off)
float ReliefShadowStrength;

//Depth range the parallax march covers, as a fraction of the relief amplitude (0 = off)
float ParallaxScale;

//How much surface one screen pixel covers, in world units. This is the yardstick every relief feature
//is band-limited against, and it is why supersampling buys real detail: more samples shrink it.
float PixelFootprint(float3 worldPosition)
{
	return length(ddx(worldPosition)) + length(ddy(worldPosition));
}

//One octave, band-limited on the spot: a wave of this frequency spans 2 * pi / f of whatever space it
//is evaluated in, so it is faded out as a pixel grows towards half of that — its Nyquist limit.
//Attenuating each octave against its own wavelength, rather than the whole field against the finest
//one, is what lets fine detail exist at all: it stays fully present while the pixels can still resolve
//it and drops out silently when they cannot, instead of breaking into the hard checkerboard that
//point-sampled high frequencies produce through the derivatives below. Position and footprint only
//have to share units — object-space directions over a ball radius, or plain world space.
float ReliefOctave(float3 position, float3 waveDirection, float frequency, float footprint)
{
	return sin(dot(position, waveDirection) * frequency) * saturate(1 - footprint * frequency / 3.14159265);
}

//The same octave, band-limited against the footprint measured *along the wave's own direction* instead
//of against its overall extent. A pixel only fails to resolve a wave when it is wide across that wave's
//crests; how far it stretches parallel to them costs nothing. One scalar footprint cannot express that,
//and on a surface seen at a grazing angle — where a pixel covers meters along the view but stays
//millimeters across it — it reports the long axis and fades out every octave at once. The floor then
//becomes geometrically perfect exactly where it should look roughest, and takes the light like polished
//glass: the milky smear this replaces. Directionally, the waves running across the view survive.
float ReliefOctaveDirectional(float3 position, float3 waveDirection, float frequency, float3 dpdx, float3 dpdy)
{
	float footprint = abs(dot(dpdx, waveDirection)) + abs(dot(dpdy, waveDirection));

	return sin(dot(position, waveDirection) * frequency) * saturate(1 - footprint * frequency / 3.14159265);
}

//World-space grain for the scene surfaces: stone, marble and cast metal all read as an irregular
//surface rather than a polished one. Amplitudes sum to one, so SurfaceReliefStrength stays the peak
//height in world units, and the frequency ratios are irrational so the sum never settles into a tile.
//Seven octaves rather than a handful on purpose: too few waves spaced too far apart interfere into a
//regular diagonal weave instead of a surface, which is exactly what the cannon barrel showed first.
float SurfaceReliefWorld(float3 worldPosition, float frequency, float3 dpdx, float3 dpdy)
{
	return 0.26 * ReliefOctaveDirectional(worldPosition, float3(0.71, 0.52, -0.47), frequency, dpdx, dpdy)
		+ 0.20 * ReliefOctaveDirectional(worldPosition, float3(-0.36, 0.83, 0.42), frequency * 1.43, dpdx, dpdy)
		+ 0.16 * ReliefOctaveDirectional(worldPosition, float3(0.55, -0.44, 0.71), frequency * 2.11, dpdx, dpdy)
		+ 0.12 * ReliefOctaveDirectional(worldPosition, float3(-0.82, -0.31, 0.48), frequency * 3.07, dpdx, dpdy)
		+ 0.10 * ReliefOctaveDirectional(worldPosition, float3(0.31, 0.62, 0.72), frequency * 4.51, dpdx, dpdy)
		+ 0.09 * ReliefOctaveDirectional(worldPosition, float3(-0.64, 0.27, -0.72), frequency * 6.73, dpdx, dpdy)
		+ 0.07 * ReliefOctaveDirectional(worldPosition, float3(0.18, -0.91, 0.37), frequency * 9.87, dpdx, dpdy);
}

//Tilts a normal by a height field using only screen-space derivatives, for the same reason
//CotangentFrame exists: the instance streams carry no tangents, and the object-to-world rotation
//never reaches the pixel shader. Christian Schueler, "Bump Mapping Unparametrized Surfaces on the GPU".
float3 PerturbNormalFromHeight(float3 normal, float3 worldPosition, float height)
{
	float3 dpdx = ddx(worldPosition);
	float3 dpdy = ddy(worldPosition);

	float3 r1 = cross(dpdy, normal);
	float3 r2 = cross(normal, dpdx);

	float determinant = dot(dpdx, r1);
	float3 surfaceGradient = sign(determinant) * (ddx(height) * r1 + ddy(height) * r2);

	return normalize(abs(determinant) * normal - surfaceGradient);
}

//The world-space relief of a scene object, ready to hand to PerturbNormalFromHeight.
//Takes the world-space screen derivatives rather than a scalar footprint so every octave can be
//band-limited along its own direction (see ReliefOctaveDirectional).
//Width of the run-out from a joint's floor up to the slab face
static const float SlabJointBevel = 0.03;

//One axis of the joint grid: 1 in the floor of a joint, 0 out on the slab face.
//
//Two things here are deliberate, and both are the lesson the ground's grain already taught. The
//footprint is the pixel's extent along this axis alone, because a joint running across the view is
//perfectly resolvable however far the pixel stretches along it. And once the pixel does grow past the
//joint, the profile widens to the pixel rather than fading out: a joint that is thinner than a pixel
//still darkens that pixel, in proportion to how much of it the joint covers. Fading it to nothing —
//which is what measuring it against its own bevel did — deletes the only structure the floor has at a
//scale the eye can see. It only leaves once the pixel can no longer resolve the slab grid itself.
float SlabGrooveAxis(float coordinate, float footprint)
{
	float cell = frac(coordinate / SlabSize);
	float distance = min(cell, 1 - cell) * SlabSize;

	float width = max(SlabJointWidth, footprint * 0.5);
	float bevel = max(SlabJointBevel, footprint * 0.5);

	return (1 - smoothstep(width, width + bevel, distance)) * saturate(1 - footprint / (SlabSize * 0.5));
}

float SlabGroove(float3 worldPosition, float3 dpdx, float3 dpdy)
{
	if (SlabSize <= 0) return 0;

	//Extent of this pixel along X and along Z, measured separately
	float2 footprint = abs(dpdx.xz) + abs(dpdy.xz);

	return max(SlabGrooveAxis(worldPosition.x, footprint.x), SlabGrooveAxis(worldPosition.z, footprint.y));
}

//The height field the whole surface is built from: micro-relief on the slab faces, joints cut below
//them. Everything below - the normal, the cavity shading, the self-shadow march and the parallax
//march - reads this one function, so a feature added here is automatically lit, occluded and
//parallaxed rather than needing to be handled three more times.
float SceneSurfaceHeight(float3 worldPosition, float3 dpdx, float3 dpdy)
{
	float height = SurfaceReliefWorld(worldPosition, SurfaceReliefFrequency, dpdx, dpdy) * SurfaceReliefStrength;

	return height - SlabGroove(worldPosition, dpdx, dpdy) * SlabJointDepth;
}

//The same field with three octaves instead of seven, for the ray marches. They evaluate it dozens of
//times per pixel and only need the shape that casts a shadow or hides something, not the grain: the
//octaves left out are finer than the steps the march takes anyway.
float SceneSurfaceHeightCoarse(float3 worldPosition, float3 dpdx, float3 dpdy)
{
	float frequency = SurfaceReliefFrequency;

	float height = (0.26 * ReliefOctaveDirectional(worldPosition, float3(0.71, 0.52, -0.47), frequency, dpdx, dpdy)
		+ 0.20 * ReliefOctaveDirectional(worldPosition, float3(-0.36, 0.83, 0.42), frequency * 1.43, dpdx, dpdy)
		+ 0.16 * ReliefOctaveDirectional(worldPosition, float3(0.55, -0.44, 0.71), frequency * 2.11, dpdx, dpdy)) * SurfaceReliefStrength;

	return height - SlabGroove(worldPosition, dpdx, dpdy) * SlabJointDepth;
}

//Highest and lowest the field can reach: the micro-relief rides above zero, the joints cut below it
float ReliefCeiling() { return SurfaceReliefStrength; }
float ReliefFloor() { return -(SurfaceReliefStrength + SlabJointDepth); }

//A pit is shaded by its own walls, and this is the cheapest honest way to say so: the deeper a point
//sits in the field, the less of the sky it can see. Without it a normal-mapped surface has its bumps
//lit but its hollows just as bright as its peaks, which is most of why relief-by-normal reads as a
//painted-on texture rather than as shape.
float CavityOcclusion(float height)
{
	float openness = saturate((height - ReliefFloor()) / max(ReliefCeiling() - ReliefFloor(), 1e-6));

	return lerp(1 - CavityStrength, 1, openness);
}

//How many steps each march takes. Both are cheap at a steep angle and expensive at a grazing one,
//which is also exactly where they matter, so the counts follow the angle.
static const int ReliefShadowSteps = 8;
static const int ParallaxMinSteps = 8;
static const int ParallaxMaxSteps = 28;

//Marches the height field towards the light and reports how much of the key light survives. This is
//the other half of what makes relief read as shape: a raking light on a real surface does not just
//shade the far sides of the bumps, it throws the bumps' shadows across the hollows behind them.
float ReliefSelfShadow(float3 worldPosition, float3 normal, float3 towardsLight, float height, float3 dpdx, float3 dpdy)
{
#if OPENGL
	//The map editor compiles this file at Shader Model 3.0, where these marches are not worth
	//attempting, and it has no use for them either
	return 1;
#else
	if (ReliefShadowStrength <= 0) return 1;

	float alongNormal = dot(towardsLight, normal);
	if (alongNormal <= 0.02) return 1; //Light at or below the horizon: the N.L term already has this

	float3 alongSurface = towardsLight - normal * alongNormal;
	float surfaceLength = length(alongSurface);
	if (surfaceLength < 1e-5) return 1; //Light straight overhead: nothing can shadow anything

	alongSurface /= surfaceLength;

	//Height the ray gains per unit travelled across the surface
	float rise = alongNormal / surfaceLength;

	//Travel far enough for the ray to clear the tallest thing the field can put in its way
	float reach = max((ReliefCeiling() - height) / max(rise, 1e-5), 0);
	float amplitude = max(ReliefCeiling() - ReliefFloor(), 1e-6);

	float blocked = 0;

	[unroll]
	for (int i = 1; i <= ReliefShadowSteps; i++)
	{
		float travel = reach * i / ReliefShadowSteps;
		float rayHeight = height + travel * rise;
		float fieldHeight = SceneSurfaceHeightCoarse(worldPosition + alongSurface * travel, dpdx, dpdy);

		//How far the field pokes above the ray, as a fraction of the field's own depth. Taking the
		//largest overlap rather than a hit/miss keeps the shadow's edge soft.
		blocked = max(blocked, saturate((fieldHeight - rayHeight) / amplitude));
	}

	return 1 - blocked * ReliefShadowStrength;
#endif
}

//Marches the height field along the view ray and returns where it actually hits. Tilting the normal
//tells the eye a surface is uneven; moving the shading point tells it the surface has depth, because
//the near walls of a groove start hiding its far walls as the camera moves. That parallax is the cue
//normal mapping cannot fake, and it is what "plastic" means here.
float3 ParallaxSurfacePosition(float3 worldPosition, float3 normal, float3 towardsEye, float3 dpdx, float3 dpdy)
{
#if OPENGL
	return worldPosition;
#else
	if (ParallaxScale <= 0) return worldPosition;

	float alongNormal = dot(towardsEye, normal);
	if (alongNormal <= 0.05) return worldPosition; //Edge-on: the offset would run away to infinity

	//World-space offset that corresponds to descending one unit into the surface
	float3 perDepth = -(towardsEye - normal * alongNormal) / alongNormal;

	float ceiling = ReliefCeiling();
	float range = max(ceiling - ReliefFloor(), 1e-6) * ParallaxScale;

	int steps = (int)lerp(ParallaxMaxSteps, ParallaxMinSteps, alongNormal);
	float stepDepth = range / steps;

	float rayDepth = 0;
	float previousRayDepth = 0;
	float previousSurfaceDepth = 0;

	[loop]
	for (int i = 0; i < steps; i++)
	{
		previousRayDepth = rayDepth;
		rayDepth += stepDepth;

		//Depth of the field below its ceiling at the point the ray has reached
		float surfaceDepth = ceiling - SceneSurfaceHeightCoarse(worldPosition + perDepth * rayDepth, dpdx, dpdy);

		if (surfaceDepth <= rayDepth)
		{
			//Crossed it between the last two samples. One linear solve for where the ray and the
			//surface actually met beats halving the step size again.
			float previousGap = previousSurfaceDepth - previousRayDepth;
			float gap = surfaceDepth - rayDepth;
			float t = saturate(previousGap / max(previousGap - gap, 1e-6));

			return worldPosition + perDepth * lerp(previousRayDepth, rayDepth, t);
		}

		previousSurfaceDepth = surfaceDepth;
	}

	return worldPosition + perDepth * rayDepth;
#endif
}

//Textured variant: the model vertices carry UVs in TEXCOORD0 (the instance stream stays in TEXCOORD1-5)

struct TexturedVertexShaderInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
	float2 TexCoord : TEXCOORD0;
};

struct TexturedVertexShaderOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
	float4 OcclusionData : TEXCOORD2;
	float2 TexCoord : TEXCOORD3;
};

TexturedVertexShaderOutput TexturedVS(TexturedVertexShaderInput input, InstanceInput instance)
{
	TexturedVertexShaderOutput output;

	float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

	float4 worldPosition = mul(mul(input.Position, Bone), world);

	output.WorldPosition = worldPosition.xyz;
	output.Position = mul(mul(worldPosition, View), Projection);
	output.WorldNormal = mul(mul(float4(input.Normal, 0), Bone), world).xyz;
	output.OcclusionData = instance.Custom;
	output.TexCoord = input.TexCoord;

	return output;
}

float4 TexturedPS(TexturedVertexShaderOutput input) : COLOR
{
	//The ground comes through here: marble slabs whose texture draws the veining, with the joints
	//between them cut into the height field so they are real recesses that hide, shadow and shift
	float3 dpdx = ddx(input.WorldPosition);
	float3 dpdy = ddy(input.WorldPosition);

	float3 geometricNormal = normalize(input.WorldNormal);
	float3 towardsEye = normalize(EyePosition - input.WorldPosition);

	//Where the view ray actually meets the relief, rather than where it meets the flat polygon
	float3 reliefPosition = ParallaxSurfacePosition(input.WorldPosition, geometricNormal, towardsEye, dpdx, dpdy);

	float height = SceneSurfaceHeight(reliefPosition, dpdx, dpdy);

	//The tangent frame stays on the real geometry; only the height is read at the parallaxed point, so
	//the derivatives pick up both the field's own slope and the way the offset changes across the screen
	float3 worldNormal = PerturbNormalFromHeight(geometricNormal, input.WorldPosition, height);

	float keyShadow = ReliefSelfShadow(reliefPosition, geometricNormal, normalize(KeyLightPosition - input.WorldPosition), height, dpdx, dpdy);

	//The albedo is mapped through the model's UVs rather than world space, so the parallax offset is not
	//applied to it: the veining stays put while the joints move. At these depths the mismatch is well
	//under a pixel, and the joints are what carry the parallax anyway.
	float4 texColor = tex2D(TextureSampler, input.TexCoord);
	texColor.rgb = SrgbToLinear(texColor.rgb);

	return ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, texColor, keyShadow, CavityOcclusion(height));
}

technique InstancedModelTextured
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL TexturedVS();
		PixelShader = compile PS_SHADERMODEL TexturedPS();
	}
};

//Procedural beach-ball pattern. Evaluated in the model's own object space, so it turns with the
//object instead of sliding over it — which is the whole point: it makes a rolling ball's rotation
//readable. Gores alternate between the two colors, with a disc of the secondary color at each pole.

//The colors the gores alternate between; the material shade multiplies both
float3 PatternPrimaryColor;
float3 PatternSecondaryColor;

//Segments around the object = 2 * PatternGoreCount
float PatternGoreCount;

//Where the boundary between two gores sits in sin(azimuth). Zero splits every pair of segments
//evenly, a positive value widens the primary-colored gore at the expense of the secondary one
//(the renderer derives this from PatternGoreWidth, which says it in plain fractions).
float PatternGoreThreshold;

//Where the polar discs start, as the |Y| of the object-space direction (1 = the pole itself)
float PatternCapExtent;

//Amplitude of the molded micro-relief of the skin, in world units (0 = a perfectly smooth sphere)
float PatternReliefStrength;

//How strongly the skin catches the sky color at grazing angles
float PatternSheenStrength;

//How much of its own color the ball radiates, independent of any light falling on it
float EmissiveStrength;

//How much light is carried through the shell from a source behind it
float TranslucencyStrength;

//Seconds since the level started, beats per second, and how deep the pulse swings (0 = steady glow)
float PulseTime;
float PulseSpeed;
float PulseDepth;

//Direction the beat travels through the cluster, and how many world units one beat spans. Offsetting
//the phase by position is what turns a cluster of balls flashing in lockstep into a wave passing
//through them - the difference between a strobe and something breathing.
float3 PulseDirection;
float PulseWavelength;

//A heart does not beat like a sine. Two pulses per cycle, the second smaller and close behind the
//first, then a long rest: the lub-dub that reads as alive rather than as a fading lamp.
float Heartbeat(float t)
{
	float phase = frac(t);

	//Squared by multiplication, not pow(x, 2): HLSL compiles pow as exp(y * log(x)), and log of a
	//negative is a NaN. Both bases go negative over most of the cycle, which left the whole term NaN
	//and the beat silently stuck at zero.
	float lubOffset = (phase - 0.10) * 13.0;
	float dubOffset = (phase - 0.29) * 15.0;

	float lub = exp(-lubOffset * lubOffset);
	float dub = 0.55 * exp(-dubOffset * dubOffset);

	return saturate(lub + dub);
}

//Width of the ring outlining each disc, so the circle reads whichever gore it lands on
static const float PatternRingWidth = 0.045;

//Depth of the weld between two panels (world units) and how wide the groove is, measured in the
//value of the field whose threshold the seam follows
static const float PatternSeamDepth = 0.010;
static const float PatternSeamGoreWidth = 0.13;
static const float PatternSeamCapWidth = 0.035;

//Wave count of the coarsest relief octave, used to fade the seam grooves, which are about that broad
static const float PatternSeamFrequency = 8.0;

struct PatternVertexShaderOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
	float4 OcclusionData : TEXCOORD2;
	float3 ObjectPosition : TEXCOORD3;
};

PatternVertexShaderOutput PatternVS(VertexShaderInput input, InstanceInput instance)
{
	PatternVertexShaderOutput output;

	float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

	float4 bonePosition = mul(input.Position, Bone);
	float4 worldPosition = mul(bonePosition, world);

	output.ObjectPosition = bonePosition.xyz;
	output.WorldPosition = worldPosition.xyz;
	output.Position = mul(mul(worldPosition, View), Projection);
	output.WorldNormal = mul(mul(float4(input.Normal, 0), Bone), world).xyz;
	output.OcclusionData = instance.Custom;

	return output;
}

//Soft step across a boundary, one screen pixel wide, so the stripes do not crawl on the
//small distant balls (a scene holds thousands of them, most only a few pixels across)
float AntialiasedStep(float edge, float value)
{
	float width = max(fwidth(value), 1e-5);

	return smoothstep(edge - width, edge + width, value);
}

//Molded micro-relief of the skin: the dimples and waviness a real ball is left with when it comes
//out of the mold. Four waves along spread-out directions at frequencies sharing no common factor,
//so the sum never repeats over the ball — multiplying two waves instead would lay down a regular
//crosshatch, the same plaid the seamless cannon metal tile had to avoid. The amplitudes add up to
//one, which leaves PatternReliefStrength as the peak height in world units.
float SurfaceRelief(float3 direction, float footprint)
{
	return 0.36 * ReliefOctave(direction, float3(0.71, 0.52, -0.47), 13.0, footprint)
		+ 0.27 * ReliefOctave(direction, float3(-0.36, 0.83, 0.42), 21.0, footprint)
		+ 0.21 * ReliefOctave(direction, float3(0.55, -0.44, 0.71), 34.0, footprint)
		+ 0.16 * ReliefOctave(direction, float3(-0.82, -0.31, 0.48), 55.0, footprint);
}

float4 PatternPS(PatternVertexShaderOutput input) : COLOR
{
	//The object-space radius is the ball's own radius, which turns the pixel footprint below into the
	//units the relief is written in without the shader having to be told how big a ball is
	float radius = max(length(input.ObjectPosition), 1e-5);
	float3 direction = input.ObjectPosition / radius;

	//sin(N * azimuth) stays continuous across the atan2 branch cut for integer N, so neither the
	//value nor its screen-space derivative jumps there and the seam needs no special handling
	float azimuth = atan2(direction.z, direction.x);
	float gore = sin(PatternGoreCount * azimuth);

	//Linearized before the blends: these crossfades run along the antialiased gore edges, so they are
	//averaging light across a pixel and have to do it in linear
	float3 primary = SrgbToLinear(PatternPrimaryColor);
	float3 secondary = SrgbToLinear(PatternSecondaryColor);

	float3 color = lerp(primary, secondary, AntialiasedStep(PatternGoreThreshold, gore));

	//Discs at the poles, where the gores would otherwise converge into an aliasing mess
	float pole = abs(direction.y);

	color = lerp(color, primary, AntialiasedStep(PatternCapExtent, pole));
	color = lerp(color, secondary, AntialiasedStep(PatternCapExtent + PatternRingWidth, pole));

	//The panels are welded together, not painted on: press a groove in along every gore boundary and
	//around the rim of each polar disc. The gore grooves fade out towards the poles, where the
	//boundaries crowd together and the disc takes over anyway.
	float goreSeam = (1 - smoothstep(0, PatternSeamGoreWidth, abs(gore - PatternGoreThreshold))) * saturate((PatternCapExtent - pole) * 8);
	float capSeam = 1 - smoothstep(0, PatternSeamCapWidth, abs(pole - PatternCapExtent));

	//How much surface one screen pixel covers, over the ball radius — the yardstick every feature is
	//band-limited against. It shrinks when the scene is supersampled, which is exactly why raising the
	//render resolution buys back the fine octaves instead of just making the same mush smoother.
	//Kept branchless: ddx/ddy need every pixel of a quad to have taken the same path.
	float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

	//Relief and welds ride in one height field, so a single perturbation covers both
	float seams = (goreSeam + capSeam) * saturate(1 - footprint * PatternSeamFrequency / 3.14159265);
	float height = SurfaceRelief(direction, footprint) * PatternReliefStrength - seams * PatternSeamDepth;

	float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);

	//The balls carry their own relief; the scene cavity and self-shadow terms are not it
	float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1);

	//Light carried through the shell from behind. A ball lit from the far side glows around its rim
	//instead of going flatly black, which is what tells the eye the thing is a skin around a volume
	//rather than a painted solid — and is half of why it can read as alive.
	float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
	float throughShell = pow(saturate(dot(-worldNormal, towardsKey)), 2);

	shaded.rgb += throughShell * TranslucencyStrength * DirLight0DiffuseColor * color
		* SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

	//Emission: the ball radiates its own color rather than only reflecting what falls on it, and does it
	//on a heartbeat. The phase runs with world position, so the beat travels through the cluster as a
	//wave instead of every ball flashing in lockstep — a pile of them breathing together, not a strobe.
	//Emission is not occluded: a light source buried in the pile is exactly the one that should still
	//show, glowing out through its neighbors.
	float beat = Heartbeat(PulseTime * PulseSpeed - dot(input.WorldPosition, PulseDirection) / max(PulseWavelength, 1e-4));

	//The ball glows with its own color, not with the pattern's: the gores and the polar discs are white,
	//and emitting through them made half of every ball radiate white light, which is both the wrong color
	//and the reason they read as washed out. What is alive here is the ball, not its paint job.
	shaded.rgb += primary * EmissiveStrength * lerp(1 - PulseDepth, 1, beat);

	//The hand-rolled vinyl sheen that used to sit here is gone: it was a Fresnel reflection of the sky,
	//which ShadePixel's specular ambient now does for every surface with a real dielectric F0 behind it.
	//Two Fresnel sky terms stacked on one sphere - where a grazing angle covers most of what you can see
	//of it - is what was bleaching the balls out under a bright dome.
	return shaded;
}

technique InstancedModelPattern
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL PatternVS();
		PixelShader = compile PS_SHADERMODEL PatternPS();
	}
};

//Detail texturing: a texture that only modulates the existing material colors
//(DetailStrength 0 = untextured look), mapped either through the model's own UVs
//(InstancedModelDetailUV — required for objects that move, or the texture would swim
//across them) or projected along the world axes for models with no UVs at all
//(InstancedModelTriplanar, e.g. the castle backdrop).

//Triplanar: world units per texture tile = 1 / DetailScale. UV mapping: tiles per UV span.
float DetailScale;
//How strongly the detail texture modulates the material color (0 = not at all, 1 = fully)
float DetailStrength;
//Brightness compensation so a mid-gray detail texture does not darken the whole material
float DetailBoost;

//How strongly the procedural construction pattern shows on vertical triplanar surfaces (0 = plain)
float MasonryStrength;

//What the mesh being drawn is made of: 0 plain, 1 coursed stone, 2 sawn timber. Matches SurfaceStyle
//on the renderer, which reads it off the model's own mesh names.
float SurfaceStyle;

//Tangent-space normal map paired with the detail texture, and how far it tilts the surface normal
texture NormalMapTexture;
sampler2D NormalMapSampler = sampler_state
{
	Texture = <NormalMapTexture>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Linear;
	AddressU = Wrap;
	AddressV = Wrap;
};

float NormalStrength;

//Tangent frame derived from screen-space derivatives instead of vertex tangents: the instance
//vertex streams carry only position, normal and UV (the procedural meshes have nothing else to give),
//and this works for any mesh drawn through the renderer. Based on Christian Schueler's
//"Normal Mapping Without Precomputed Tangents".
float3x3 CotangentFrame(float3 normal, float3 worldPosition, float2 uv)
{
	float3 dp1 = ddx(worldPosition);
	float3 dp2 = ddy(worldPosition);
	float2 duv1 = ddx(uv);
	float2 duv2 = ddy(uv);

	float3 dp2perp = cross(dp2, normal);
	float3 dp1perp = cross(normal, dp1);

	float3 tangent = dp2perp * duv1.x + dp1perp * duv2.x;
	float3 bitangent = dp2perp * duv1.y + dp1perp * duv2.y;

	float invmax = rsqrt(max(dot(tangent, tangent), dot(bitangent, bitangent)));

	return float3x3(tangent * invmax, bitangent * invmax, normal);
}

float4 DetailUVNormalPS(TexturedVertexShaderOutput input) : COLOR
{
	float2 uv = input.TexCoord * DetailScale;

	float3 detail = SrgbToLinear(tex2D(TextureSampler, uv).rgb);
	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

	float3 geometricNormal = normalize(input.WorldNormal);

	float3 tangentNormal = tex2D(NormalMapSampler, uv).xyz * 2 - 1;
	tangentNormal.xy *= NormalStrength;

	float3 worldNormal = normalize(mul(normalize(tangentNormal), CotangentFrame(geometricNormal, input.WorldPosition, uv)));

	//The normal map carries the cast pattern at texture resolution; the procedural relief goes on top of
	//it, finer than the map can hold and free of its tiling, so the barrel keeps breaking up the
	//highlight right down to where a pixel can no longer tell
	float height = SceneSurfaceHeight(input.WorldPosition, ddx(input.WorldPosition), ddy(input.WorldPosition));
	worldNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

	return ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(texRgb, 1), 1, CavityOcclusion(height));
}

technique InstancedModelDetailUVNormal
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL TexturedVS();
		PixelShader = compile PS_SHADERMODEL DetailUVNormalPS();
	}
};

float4 DetailUVPS(TexturedVertexShaderOutput input) : COLOR
{
	float3 detail = SrgbToLinear(tex2D(TextureSampler, input.TexCoord * DetailScale).rgb);
	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

	float height = SceneSurfaceHeight(input.WorldPosition, ddx(input.WorldPosition), ddy(input.WorldPosition));
	float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);

	return ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(texRgb, 1), 1, CavityOcclusion(height));
}

technique InstancedModelDetailUV
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL TexturedVS();
		PixelShader = compile PS_SHADERMODEL DetailUVPS();
	}
};

//Stone block coursing drawn on the vertical surfaces (world units)
static const float BrickWidth = 3.0;
static const float BrickHeight = 1.4;
static const float MortarWidth = 0.09;
static const float MortarDarkness = 0.62;

//How far the mortar sits behind the block faces and how wide the bevel running down to it is, both in
//world units. Giving the joint a real width rather than letting it collapse into a one-pixel crease is
//what makes it read as a recess instead of a dark line.
static const float MortarDepth = 0.055;
static const float MortarBevel = 0.07;

//Distance to the nearest joint, in world units; p is a vertical wall plane.
//Every other course is offset by half a block, like real coursed masonry.
float BrickJointDistance(float2 p)
{
	float row = floor(p.y / BrickHeight);
	float2 cell = float2(frac((p.x + row * BrickWidth * 0.5) / BrickWidth), frac(p.y / BrickHeight));

	//Distance to the nearest cell border, back in world units
	float2 border = min(cell, 1 - cell) * float2(BrickWidth, BrickHeight);

	return min(border.x, border.y);
}

//0 in a mortar joint, 1 inside a block
float BrickMask(float distanceToJoint)
{
	//Screen-space derivative keeps the joint edge soft and fades it out at long range instead of shimmering
	float soft = max(fwidth(distanceToJoint), 0.02);

	return smoothstep(MortarWidth - soft, MortarWidth + soft, distanceToJoint);
}

//Sawn timber: upright boards with a chamfer between them (world units)
static const float BoardWidth = 0.42;
static const float BoardGrooveWidth = 0.05;
static const float BoardGrooveDepth = 0.05;
static const float BoardDarkness = 0.72;

//Distance to the nearest gap between boards, in world units. p runs across the boards.
float BoardSeamDistance(float p)
{
	return abs(frac(p / BoardWidth) - 0.5) * BoardWidth;
}

//Long grain of the timber: the same octaves as the stone, but squashed hard along the board so the
//waves stretch into fibers running its length instead of reading as isotropic mottling.
float WoodGrain(float3 worldPosition, float3 dpdx, float3 dpdy)
{
	//The derivatives are squashed along with the position, or the band limit would be measured in a
	//different space than the waves it is limiting
	float3 squash = float3(1.0, 0.14, 1.0);

	return SurfaceReliefWorld(worldPosition * squash, SurfaceReliefFrequency * 2.2, dpdx * squash, dpdy * squash);
}

float4 TriplanarPS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	//Sharpened normal weights avoid visible cross-fading except near 45-degree edges
	float3 blend = pow(abs(worldNormal), 4);
	blend /= blend.x + blend.y + blend.z;

	float3 p = input.WorldPosition * DetailScale;

	//Each tap is linearized before the blend: the three projections are averaged, and averaging
	//display-encoded values is exactly the mistake this whole pass exists to stop making
	float3 detail
		= SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
		+ SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
		+ SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

	//Construction patterns land on the vertical faces only (roofs and other Y-facing surfaces stay plain)
	float verticalWeight = (blend.x + blend.z) * MasonryStrength;
	float sideWeight = max(blend.x + blend.z, 0.001);
	float footprint = PixelFootprint(input.WorldPosition);

	//Coursed stone. The joints are cut, not painted: sinking the mortar behind the block faces is what
	//makes every course light and shadow from the side as the sun moves across the castle. Faded on the
	//pixel footprint like the octaves are, against the bevel that sets the groove's own width.
	float jointDistanceZY = BrickJointDistance(input.WorldPosition.zy);
	float jointDistanceXY = BrickJointDistance(input.WorldPosition.xy);

	float brick = (BrickMask(jointDistanceZY) * blend.x + BrickMask(jointDistanceXY) * blend.z) / sideWeight;
	float stoneShade = lerp(MortarDarkness, 1, brick);
	float stoneGroove = (1 - smoothstep(MortarWidth, MortarWidth + MortarBevel, (jointDistanceZY * blend.x + jointDistanceXY * blend.z) / sideWeight))
		* saturate(1 - footprint / MortarBevel);

	//Sawn timber. The boards run up the wall, so their seams are spaced across it — along whichever
	//horizontal axis the face is turned towards.
	float boardSeamDistance = (BoardSeamDistance(input.WorldPosition.z) * blend.x + BoardSeamDistance(input.WorldPosition.x) * blend.z) / sideWeight;
	float boardSoft = max(fwidth(boardSeamDistance), 0.004);
	float woodShade = lerp(BoardDarkness, 1, smoothstep(BoardGrooveWidth - boardSoft, BoardGrooveWidth + boardSoft, boardSeamDistance));
	float woodGroove = (1 - smoothstep(0, BoardGrooveWidth, boardSeamDistance)) * saturate(1 - footprint / BoardGrooveWidth);

	//Pick the style branchlessly: the derivatives below need every pixel of a quad to have walked the
	//same path, and SurfaceStyle is a uniform, so a branch would save nothing anyway.
	float isWood = step(1.5, SurfaceStyle);
	float isPatterned = step(0.5, SurfaceStyle) * verticalWeight;

	float3 dpdx = ddx(input.WorldPosition);
	float3 dpdy = ddy(input.WorldPosition);

	float grain = lerp(SceneSurfaceHeight(input.WorldPosition, dpdx, dpdy), WoodGrain(input.WorldPosition, dpdx, dpdy) * SurfaceReliefStrength, isWood);
	float groove = lerp(stoneGroove * MortarDepth, woodGroove * BoardGrooveDepth, isWood);

	float shade = lerp(1, lerp(stoneShade, woodShade, isWood), isPatterned);
	float height = grain - groove * isPatterned;

	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength) * shade;
	float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

	//This path builds its own height field (masonry courses, board seams) rather than going through
	//SceneSurfaceHeight, so the generic marches would be reading a different surface than the one drawn
	//here. Cavity shading needs only the height and applies cleanly; sinking the mortar joints into
	//shadow is most of what the marches would have bought on a wall anyway.
	float cavityRange = max(SurfaceReliefStrength + MortarDepth, 1e-6);
	float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + MortarDepth) / (cavityRange + SurfaceReliefStrength)));

	return ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity);
}

//Vertical spacing of a window row and horizontal spacing of a column, in world units, and how much of
//each cell is glass rather than the wall around it
static const float WindowPitchY = 2.2;
static const float WindowPitchX = 1.7;
static const float WindowFillY = 0.52;
static const float WindowFillX = 0.46;

//Fraction of the windows that are lit, and the two colors they are lit with
static const float WindowLitFraction = 0.42;
static const float3 WindowWarm = float3(1.0, 0.78, 0.44);
static const float3 WindowCool = float3(0.52, 0.82, 1.0);

//How brightly the lit windows glow, and how dark the facade around them is
float CityWindowBrightness;

float Hash21(float2 p)
{
	p = frac(p * float2(123.34, 456.21));
	p += dot(p, p + 45.32);

	return frac(p.x * p.y);
}

//Windows evaluated from world position rather than from the model's own coordinates: the buildings are
//one box scaled per instance, so an object-space grid would stretch with the building and give a
//hundred-storey tower the same number of floors as a low one.
float4 CityPS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	//Which pair of world axes runs across this facade. Branchless: a lerp on the face's own normal,
	//which is constant over a flat face, so the derivatives below stay well defined.
	float facingX = step(abs(worldNormal.z), abs(worldNormal.x));
	float2 facade = lerp(input.WorldPosition.xy, input.WorldPosition.zy, facingX);

	//Roofs and the ground faces get no windows
	float vertical = 1 - step(0.5, abs(worldNormal.y));

	float2 grid = facade / float2(WindowPitchX, WindowPitchY);
	float2 cell = floor(grid);
	float2 withinCell = abs(frac(grid) - 0.5) * 2;

	//The pixel's extent across the facade, per axis, in cells. Band-limited the way every other feature
	//here is: once a pixel covers more than a window the pattern fades to its own average rather than
	//aliasing into a moire of lit and unlit floors, which is what a city at distance would otherwise do.
	float2 footprint = (abs(ddx(facade)) + abs(ddy(facade))) / float2(WindowPitchX, WindowPitchY);
	float resolvable = saturate(1 - max(footprint.x, footprint.y));

	float2 shape = smoothstep(float2(WindowFillX, WindowFillY) + footprint, float2(WindowFillX, WindowFillY) - footprint, withinCell);
	float window = shape.x * shape.y * vertical;

	//Each window decides once and for all whether it is lit, from its own cell and the building it is on
	float lamp = Hash21(cell + floor(input.WorldPosition.xz * 0.37));
	float lit = step(1 - WindowLitFraction, lamp);

	float3 lampColor = lerp(WindowWarm, WindowCool, step(0.5, Hash21(cell * 1.7 + 11.3)));

	//Fading to the average keeps a distant tower a dim glowing block instead of a flickering one
	float coverage = lerp(WindowFillX * WindowFillY * WindowLitFraction * vertical, window * lit, resolvable);

	//The facade itself is dark: a night city is mostly unlit concrete with light punched through it
	float3 facadeColor = float3(0.06, 0.065, 0.08);

	float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(facadeColor, 1), 1, 1);

	shaded.rgb += coverage * lampColor * CityWindowBrightness;

	return shaded;
}

technique InstancedCity
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL CityPS();
	}
};

technique InstancedModelTriplanar
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL TriplanarPS();
	}
};

//Depth-only pass for shadow mapping: renders the instances from the light's point of view,
//writing normalized depth into the red channel of a Single-format render target.

float4x4 LightViewProjection;

struct DepthVertexShaderOutput
{
	float4 Position : SV_POSITION;
	float Depth : TEXCOORD0;
};

DepthVertexShaderOutput DepthVS(VertexShaderInput input, InstanceInput instance)
{
	DepthVertexShaderOutput output;

	float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
	float4 worldPosition = mul(mul(input.Position, Bone), world);

	output.Position = mul(worldPosition, LightViewProjection);
	output.Depth = output.Position.z / output.Position.w;

	return output;
}

float4 DepthPS(DepthVertexShaderOutput input) : COLOR
{
	return float4(input.Depth, 0, 0, 1);
}

technique InstancedDepth
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL DepthVS();
		PixelShader = compile PS_SHADERMODEL DepthPS();
	}
};
