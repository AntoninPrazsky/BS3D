//Draws many instances of a rigid model in a single draw call per mesh part.
//The per-instance world matrix is supplied through a second vertex stream (TEXCOORD1-TEXCOORD4 hold its rows).
//Lighting replicates BasicEffect with EnableDefaultLighting and per-pixel (Blinn-Phong) shading,
//so instanced models look the same as those rendered through ModelRenderer.

//Both the Testbed and the map editor build this for DirectX now and get Shader Model 5.0. The editor
//used to build it for DesktopGL, where MojoShader capped shaders at 3.0 and everything 5.0-only had to
//sit behind #if OPENGL; it has since moved onto WindowsDX so it can render the balls exactly as the game
//does, tonemapping and all. There is no OPENGL build of this file any more, so nothing needs a fallback.
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//Every color that enters the lighting math - material colors, light colors, sky palette, texture
//samples - is authored or stored in sRGB, which is a display encoding, not a quantity of light.
//Adding, multiplying and averaging those numbers is only meaningful once they are back in linear
//radiance; Tonemap.fx encodes the result for the display again at the very end of the frame. Both the
//game and the map editor render into a linear HDR target and tonemap now, so this always decodes.
float3 SrgbToLinear(float3 color)
{
	//Jim Hejl's cubic fit of the sRGB curve - accurate to well under a display bit and no pow()
	return color * (color * (color * 0.305306011 + 0.682171111) + 0.012522878);
}

//Cloud shadows. The map editor has no weather and never sets the cloud uniforms, so there CloudCoverageGain
//stays zero and CloudSunlight falls straight through to a flat 1.0 - full sun, no shadow - on its own.
#include "Clouds.fxh"

//Towards the sun. The key light is positional and sits only forty units off, so its direction swings
//right across the scene - useless for a shadow that has to fall in parallel bands over a whole city.
float3 SunDirection;

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
	//How much of this instance has been dithered away: 0 draws it whole, positive eats it away, negative
	//fills it in. Read by the ball pattern technique alone; every other technique ignores it, and an
	//unconsumed element in the vertex layout costs nothing.
	float Dissolve : TEXCOORD6;
	//How brightly this instance is flaring as the ripple passes through it, 0 = not at all. Read by the
	//ball pattern technique alone, like Dissolve above.
	float Ripple : TEXCOORD7;
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

//Additional scene-specific point lights (a campfire on the savanna, the city's neon, ...) that exist under
//EVERY sky dome, added on top of the sun and the dome ambient rather than derived from either. Colours are
//linear radiance; the count is how many of the fixed slots are live this frame. They light everything that
//comes through ShadePixel - the balls, the island, the cannon, the city facades - so a fire warms the balls
//near it and the neon actually colours the towers.
#define MAX_SCENE_LIGHTS 8
float3 SceneLightPosition[MAX_SCENE_LIGHTS];
float3 SceneLightColor[MAX_SCENE_LIGHTS];
float SceneLightRange[MAX_SCENE_LIGHTS];
int SceneLightCount;

//Submerge fade for anything sinking below the sea (only pushed on the sea scene; SeaFadeDepth <= 0 disables
//it everywhere else). A ball that misses falls past the platform into the water, and the opaque sea surface
//hides it the instant it crosses - this fades it instead, blending its lit colour toward the deep-water tint
//and its alpha down over a shallow band below SeaLevelY so it reads as dimming into dark water rather than
//vanishing in one frame (#131). The sea itself stops writing depth (DrawSea) so the ball reaches this path at
//all; without that it would be depth-killed under the surface plane before the pixel shader ran. Declared
//with the shared uniforms because two paths read it: PatternPS for the balls, and MainPS for the drain's
//glass and gold below the pool standing in the drain (#132).
float SeaLevelY;
float SeaFadeDepth;
float3 SeaSubmergeTint;

//...and the fade is RELEASED as the LENS goes under with it (#159), by exactly the amount the tonemap's own
//underwater murk arrives with. What the effect is for is a ball seen from ABOVE, through the surface: the water
//column between the eye and the ball is what dims it. Once the camera is down there too the murk owns the
//frame, and dissolving individual objects inside it as well is one water effect too many.
//
//It was not academic. Sea is one of the scenes the drop cinematic can film from BELOW (SceneRenderer's
//OpenBelow), and that shot deliberately swings the eye well under the surface to follow the released cluster
//down the drain - so the one thing the cinematic exists to show faded to nothing exactly while it was being
//shown, on a level that ships.
//
//THE HANDOVER IS THE WHOLE OF THE FIX'S CORRECTNESS, and the first attempt got it wrong in a way worth keeping
//here. It released the fade over a 1.5-unit band measured from EyePosition, which put the release ABOVE the
//surface: at a tenth of a unit over the water the fade was already 93 % gone, and since DrawSea gives up its
//depth write, the whole submerged drain shaft then drew straight THROUGH the standing pool - the exact
//composite MainPS's copy of this block was added to prevent (#132). The band also claimed the murk was behind
//it and the murk is 7 % in at that height (its own ramp is 7 units, not 1.5), and it read nothing at all in the
//map editor, which pins the murk to zero.
//
//So the release is not a band of its own: it is 1 - SeaLensSubmerged, where that uniform is the SAME number the
//caller hands PostProcessPipeline.Resolve for the murk (SceneRenderer.LensSubmergedAmount). Above the water it
//is 0 and the fade is untouched, so #131's sinking ball and #132's pool are bit-for-bit what they were; under
//it the two effects hand over at one rate by construction rather than one leaning on the other being there;
//and the map editor passes 0, which is correct there precisely because it has no murk to hand over to.
float SeaLensSubmerged;

//Fade band above the kill plane (#192): the host deletes a fallen ball the instant its body crosses this
//height, with nothing to soften it - in the six OpenBelow scenes the drop cinematic can put the lens below
//the island, so that cull happens in shot, at full brightness, one frame from solid to gone. Pushed
//unconditionally, once a frame, by whichever host owns the physics kill plane (SceneRenderer.ApplyKillPlaneFade);
//the map editor never calls it, so KillPlaneFadeDepth stays at its compiled default of 0, which the gate below
//reads as off. Read by the two BALL techniques alone - this cull only ever touches a ball, never the island or
//the drain glass.
float KillPlaneY;
float KillPlaneFadeDepth;

//The sea's submerge fade, applied to an already-shaded premultiplied colour. A FUNCTION since the bubbles
//joined (#258) and not by preference: it stood written out in MainPS and again in PatternPS with a comment on
//each saying the two copies are identical character for character, and a third hand-kept copy is how that
//stops being true. A no-op off the sea scene, where SeaFadeDepth is pushed <= 0.
float4 ApplySeaSubmerge(float4 shaded, float3 worldPosition)
{
	if (SeaFadeDepth > 0.0)
	{
		//Times what the murk has NOT taken over yet, so the fade releases as the camera goes under (#159) - see
		//SeaLensSubmerged, which is 0 above the water, so everything above it is untouched. Folded into
		//`submerge` rather than applied after, so the colour and the alpha keep taking the SAME figure: this
		//output is premultiplied, and two fades that could disagree is exactly how a vanished surface starts
		//ADDING its colour instead of leaving.
		float submerge = saturate((SeaLevelY - worldPosition.y) / SeaFadeDepth)
			* (1.0 - SeaLensSubmerged);

		shaded.rgb = lerp(shaded.rgb, SeaSubmergeTint, submerge) * (1.0 - submerge);
		shaded.a *= 1.0 - submerge;
	}

	return shaded;
}

//The kill plane's own fade (#192), the same shape as the sea's and applied on top of it - there is no tint to
//sink into here, only nothing, so the colour is scaled straight towards zero WITH the alpha rather than lerped
//towards one. A no-op off every scene the map editor draws, where KillPlaneFadeDepth is left at its compiled
//default of 0.
float4 ApplyKillPlaneFade(float4 shaded, float3 worldPosition)
{
	if (KillPlaneFadeDepth > 0.0)
	{
		float killFade = saturate((worldPosition.y - KillPlaneY) / KillPlaneFadeDepth);

		shaded.rgb *= killFade;
		shaded.a *= killFade;
	}

	return shaded;
}

void AddSceneLights(float3 worldPosition, float3 worldNormal, float3 eyeVector, inout float3 diffuse, inout float3 specular)
{
	[loop]
	for (int i = 0; i < SceneLightCount; i++)
	{
		float3 toLight = SceneLightPosition[i] - worldPosition;
		float dist = length(toLight);
		float3 L = toLight / max(dist, 1e-4);

		//Smooth distance falloff to the light's range (quadratic, so it fades gently and dies at the edge)
		float atten = saturate(1.0 - dist / SceneLightRange[i]);
		atten *= atten;

		diffuse += SceneLightColor[i] * (saturate(dot(worldNormal, L)) * atten);

		float dotH = saturate(dot(worldNormal, normalize(L + eyeVector)));
		specular += SceneLightColor[i] * (pow(dotH, SpecularPower) * atten);
	}
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
//
//Schlick rises to a FULL mirror at grazing incidence, and that is only true of a SMOOTH surface. On a
//rough one the microfacets shadow and mask each other, so the fraction reflected never approaches 1 --
//which is why plaster does not flare white along a wall the way polished stone does. Smoothness caps the
//grazing value (the roughness-aware Schlick from Lagarde's Frostbite notes); at 1 this is Schlick exactly,
//so every polished surface in the scene is unchanged to the bit. It matters most where a whole surface is
//seen edge-on, and almost every facade of a city viewed from inside it is: that flare, over a dark albedo,
//is what made the towers read as glass.
float3 FresnelSchlick(float3 reflectanceAtNormal, float cosTheta, float smoothness)
{
	float3 reflectanceAtGrazing = max(smoothness, reflectanceAtNormal);

	return reflectanceAtNormal + (reflectanceAtGrazing - reflectanceAtNormal) * pow(1 - saturate(cosTheta), 5);
}

//How strongly the surface reflects the sky as an environment (0 = off)
float SpecularAmbientStrength;

//0 = dielectric (the default for everything): the reflection is the ~4% dielectric F0 tinted by the
//specular color, near white and only mirror-like at grazing angles. 1 = metal: the reflectance at normal
//incidence *is* the specular color, so the whole surface reflects the environment in that tint (gold
//reflects gold), which is what a bare-metal trim needs. Left at 0 unless a renderer sets it.
float Metalness;

//How far the two specular terms are attenuated by the material's own ALPHA. 1 - what every renderer sets
//unless it says otherwise, and what every surface in this game did before there was a dial - scales both
//by color.a. 0 leaves them at full strength, which is what a TRANSPARENT surface actually does: alpha is
//how much of what is BEHIND a surface comes through, and a reflection is light coming off the FRONT of it
//- exactly the argument the EmissiveTint line at the end of ShadePixel already makes for a glowing pane.
//
//It exists for the result screen's crystal cup (#228). Written into a premultiplied target, an
//unattenuated reflection composites as light ADDED over the background rather than as a fraction of a
//surface, which is the whole look of cut glass: the sky flares off the bowl and the frame behind it still
//shows through. Attenuated, a 38%-transparent cup keeps 38% of its own sparkle and reads as a coloured
//film rather than as crystal.
float SpecularAlphaWeight;

//Light a surface puts out on its own, in linear radiance, added at the end of ShadePixel so every
//technique that shades through it can use it. Zero everywhere but the glass ceiling as it steps down,
//which is the one surface in this game that has to announce itself.
float3 EmissiveTint;

//How much of the three-light rig (key, fill, back) reaches this surface, 1 for everything but the glass
//ceiling. Per-renderer where the DirLight* colors cannot be: those are one set of values for the whole
//scene, so dimming them for one surface would dim every surface drawn after it. The ceiling glass stands
//against the sky itself and is dimmed to the sky's own brightness through this instead (#156,
//SkyLightRig.ApplyToGlass). Deliberately not applied to the scene point lights: a neon sign or the
//cavern's crystals light a nearby pane regardless of how dark the sky over it is.
float DirLightStrength;

//Normal-incidence reflectance of a dielectric. Stone, marble, glass, vinyl, paint - everything in this
//scene that is not bare metal - reflects roughly this fraction of what hits it head-on.
static const float DielectricF0 = 0.04;

//What a surface does with light where it is not diffuse: how much of the direct highlight it shows, how
//much of the environment it mirrors, and how polished it is.
//
//These three are uniforms for every technique but one, because a mesh part is one material. The city is the
//exception: a facade is plaster AND glass on the same triangle, and a uniform cannot vary per pixel -- so
//CityPS blends one of these per pixel and hands it in. Everything else passes DefaultSurfaceSpecular(),
//which is the uniforms verbatim, so the rest of the scene is untouched by this existing.
struct SurfaceSpecular
{
	//Scales the direct lights' highlight. 1 = the whole of it, as every other surface gets.
	float Highlight;

	//Scales SpecularAmbientStrength, the reflected environment. 1 = the renderer's own dial, unchanged.
	float Environment;

	//1 = polished: a sharp reflection and a full mirror at grazing angles. 0 = rough: the reflection blurred
	//all the way to the sky's average, and no grazing mirror at all. Drives both, because both are the same
	//physical fact about the surface, and driving them apart is how a material stops being one material.
	float Smoothness;
};

SurfaceSpecular DefaultSurfaceSpecular()
{
	SurfaceSpecular surface;

	surface.Highlight = 1;
	surface.Environment = 1;
	surface.Smoothness = 1;

	return surface;
}

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
float4 ShadePixel(float3 worldPosition, float3 rawWorldNormal, float4 occlusionData, float4 texColor, float keyShadow, float cavity, SurfaceSpecular surface)
{
	float3 worldNormal = normalize(rawWorldNormal);
	float3 eyeVector = normalize(EyePosition - worldPosition);

	//The key light is accumulated on its own so the relief's self-shadow can be applied to it without
	//touching the fill and back lights, which stand in for bounced light and are not blocked by a bump
	float3 keyDiffuse = 0;
	float3 keySpecular = 0;

	//The rig arrives linear, decoded once on the CPU along with the tints applied to it
	AddLight(normalize(KeyLightPosition - worldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, worldNormal, eyeVector, keyDiffuse, keySpecular);

	//The cloud shadow rides on the same multiplier the relief's own bumps use, which is why one line here
	//puts weather across the whole scene at once - balls, city, floor and cannon all come through here.
	float sunlight = keyShadow * CloudSunlight(worldPosition, SunDirection);

	float3 diffuse = keyDiffuse * sunlight;
	float3 specular = keySpecular * sunlight;

	AddLight(-DirLight1Direction, DirLight1DiffuseColor, DirLight1SpecularColor, worldNormal, eyeVector, diffuse, specular);
	AddLight(-DirLight2Direction, DirLight2DiffuseColor, DirLight2SpecularColor, worldNormal, eyeVector, diffuse, specular);

	//The whole three-light rig, and only it: the scene lights below stay at full strength (see the
	//declaration - this is the glass ceiling's per-renderer dimmer, and a cave's own glow still reaches it)
	diffuse *= DirLightStrength;
	specular *= DirLightStrength;

	//Scene point lights (fire, neon, ...) on top of the sun and sky - present under every dome
	AddSceneLights(worldPosition, worldNormal, eyeVector, diffuse, specular);

	float3 hemisphere = SkyRadiance(worldNormal);

	float occlusion = SurfaceOcclusion(worldPosition, worldNormal, occlusionData) * cavity;
	float diffuseOcclusion = lerp(0.6, 1.0, occlusion);

	//texColor arrives linear already: every sampling site linearizes at the tap, where the sRGB
	//encoding of the texture is still an established fact rather than an assumption
	float4 color = float4((diffuse * SrgbToLinear(DiffuseColor.rgb) * diffuseOcclusion + hemisphere * SrgbToLinear(AmbientColor) * occlusion + SrgbToLinear(EmissiveColor)) * texColor.rgb, DiffuseColor.a * texColor.a);

	//What the specular terms are scaled by: the surface's own coverage, or a flat 1 for a surface that
	//declares its reflection is not something transparency takes away (see SpecularAlphaWeight). At the
	//weight of 1 every renderer sets, this is color.a and the two lines below are unchanged.
	float specularAlpha = lerp(1.0, color.a, SpecularAlphaWeight);

	float3 linearSpecular = SrgbToLinear(SpecularColor);
	color.rgb += specular * linearSpecular * surface.Highlight * specularAlpha * occlusion;

	//Specular ambient: the sky reflected off the surface, which the renderer simply never had. The
	//direct lights gave every material one highlight from one lamp, and that is a plastic look no
	//matter how the highlight is shaped - real surfaces mostly show their surroundings.
	//
	//Roughness comes from the Blinn-Phong exponent so no material has to be re-authored to get this:
	//sqrt(2 / (n + 2)) is the standard correspondence. It lerps the mirror sample towards the average
	//of the whole sky, which is what blurring a two-color gradient converges to. A surface that declares
	//itself rough is driven the rest of the way to 1 -- fully blurred, i.e. it shows the sky's average and
	//no image of it, which is the whole difference between a plastered wall and a pane of glass.
	float roughness = lerp(1.0, sqrt(2.0 / (SpecularPower + 2.0)), surface.Smoothness);
	float3 reflection = reflect(-eyeVector, worldNormal);
	float3 environment = lerp(SkyRadiance(reflection), (SkyColor + GroundColor) * 0.5, saturate(roughness));

	//F0 is the fraction reflected head-on, and for every non-metal that is about 4%. BasicEffect's
	//SpecularColor is a highlight tint rather than a reflectance - it is near white on most materials -
	//so it modulates that 4% instead of standing in for it. Handing it to Schlick directly makes F come
	//out near 1 at every angle, which mirrors the entire sky off every surface and veils the scene.
	//The Fresnel rise to 1 at grazing angles is then the whole effect, which is as it should be.
	//A dielectric reflects DielectricF0 * tint head-on; a metal reflects its specular color itself (its F0
	//is high and colored). Metalness picks between them, so gold trim mirrors the sky in gold.
	float3 reflectanceAtNormal = lerp(DielectricF0 * linearSpecular, linearSpecular, Metalness);

	color.rgb += environment * FresnelSchlick(reflectanceAtNormal, dot(worldNormal, eyeVector), surface.Smoothness)
		* SpecularAmbientStrength * surface.Environment * specularAlpha * occlusion;

	//Light the surface is putting out itself, on top of everything it reflects. Zero for everything except
	//the glass ceiling as it steps down, which is the one surface in the game that has to announce itself.
	//
	//NOT multiplied by color.a, unlike the specular ambient above: alpha is how much of what is BEHIND the
	//surface comes through, and a pane that is glowing is emitting rather than transmitting. Attenuating it
	//by the glass's own transparency is what would make a warning on a 35 %-opaque plate almost invisible.
	color.rgb += EmissiveTint;

	return color;
}

//One material over the whole surface, described by the uniforms -- every technique but the city's. At
//DefaultSurfaceSpecular() the three terms above are multiplied by 1 and Smoothness 1 makes the roughness
//lerp and FresnelSchlick identities, so this is the shading this function did before it was split.
float4 ShadePixel(float3 worldPosition, float3 rawWorldNormal, float4 occlusionData, float4 texColor, float keyShadow, float cavity)
{
	return ShadePixel(worldPosition, rawWorldNormal, occlusionData, texColor, keyShadow, cavity, DefaultSurfaceSpecular());
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	//Untextured, unrelieved parts: nothing to shadow itself and no pits to darken
	float4 shaded = ShadePixel(input.WorldPosition, input.WorldNormal, input.OcclusionData, float4(1, 1, 1, 1), 1, 1);

	//Submerge fade, the balls' #131 treatment for the plain-material surfaces: in the sea scene the drain's
	//glass cone and its bottom gold band continue below the pool standing in the drain (#132), draw AFTER
	//the water, and the water writes no depth — so without this the submerged glass would composite over
	//the pool's surface from underneath it across the whole disc.
	shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

	return shaded;
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
}

//Marches the height field along the view ray and returns where it actually hits. Tilting the normal
//tells the eye a surface is uneven; moving the shading point tells it the surface has depth, because
//the near walls of a groove start hiding its far walls as the camera moves. That parallax is the cue
//normal mapping cannot fake, and it is what "plastic" means here.
float3 ParallaxSurfacePosition(float3 worldPosition, float3 normal, float3 towardsEye, float3 dpdx, float3 dpdy)
{
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

//How hard a ball flares at the peak of its own ripple, as a multiple of its colour. Zero switches the
//whole term off - which is what the map editor and the testbed leave it at, since neither has a shot
//landing in a cluster to start one.
float RippleStrength;

//How far the flare is carried to white. Enough that it lifts the channels the ball has none of - which is
//what makes it read as lighting up - while keeping enough hue that a red ball's flare is still warm and
//short of the point where every ball in the front goes the same featureless white.
static const float RippleWhiten = 0.5;

//A ripple can carry an alarm instead of the ball's own light, and the SIGN of the per-instance value says
//which: positive is the ordinary landing wave, negative the alarm. One channel, two meanings, exactly as
//Dissolve encodes its two directions - and it means a ball can only be in one wave at a time, which is
//already true of it (the newest wave to reach a ball takes it over).
//
//The flare is a flat colour the ball's own has no say in: the whole point is that every ball in the wave
//says the same thing, and a red flare tinted by a green ball is not red.
//
//A UNIFORM and not a constant, because the wave has two meanings and they must not look alike. A descent
//the ceiling forces on the player is a threat and burns red; a descent the game hands them because they
//just cleared a great deal of a tall column is a REWARD arriving, and a red flash there tells them off for
//playing well. The caller states the colour with the wave. InstancedModelRenderer sets it unconditionally
//and defaults it to the red, so a renderer nobody has told is still saying "alarm" rather than black.
float3 RippleAlarmColor;

//How bright the alarm burns (linear radiance, over GLARE_THRESHOLD so it blooms) and how much of the ball
//it takes at the peak. Short of 1: leaving a trace of the ball's own shading is what keeps the cluster
//looking like balls rather than like flat red discs cut out of the frame.
static const float RippleAlarmBrightness = 1.7;
static const float RippleAlarmCoverage = 0.95;

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
	//Flat across the instance; interpolating a constant is free and saves a nointerpolation qualifier
	float Dissolve : TEXCOORD4;
	float Ripple : TEXCOORD5;
};

//How wide one cell of the dissolve's dither is, in pixels of the CURRENTLY BOUND target. The caller sends
//the display-pixel size it wants multiplied by however much the scene is supersampled, so the cell is a
//block of the finished image whatever the render resolution - see BallRenderSet.Draw, which is the one
//thing that sets it, the dissolve being read by the ball technique alone.
//
//It is a SCREEN-space dither, and the reason is what the effect is for: the old colour has to visibly go
//away in PIXELS rather than fading, so the player sees the game re-colouring a loaded ball instead of a
//colour quietly changing behind their back. Cells in the ball's own object space - which is what this was,
//7 of them along each axis - are cubes in the world: they turn with the ball, they take its perspective,
//and what they read as on screen is a lumpy three-dimensional mottling of the surface rather than
//pixelation of the picture. The measured trap that argued for object space was real but was an argument
//against the WRONG screen-space form: a cell one TARGET pixel across is averaged straight back into a
//smooth cross-fade by the box filter that resolves a supersampled frame. A cell a whole display pixel or
//more across is not, because every target sample inside it takes the same decision - which is exactly what
//scaling this by the supersampling factor buys.
float DissolvePixelSize;

/// A hash with no sin in it, for the same reason the cloud field's has none: sine-based hashes band
/// differently across drivers. Cheap enough to run unconditionally rather than behind a per-instance
/// branch, which would diverge within a draw call. Two-dimensional now that the cell is a block of the
/// screen; the swizzle to three components is the usual way this hash family reaches one output.
float DissolveNoise(float2 cell)
{
	float3 p = frac(cell.xyx * float3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yzx + 33.33);

	return frac((p.x + p.y) * p.z);
}

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
	output.Dissolve = instance.Dissolve;
	output.Ripple = instance.Ripple;

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

	//The dissolve cut, before anything else is worth computing. Branchless — a select rather than an if,
	//because Dissolve varies per instance and a branch on it would diverge inside a single draw call.
	//At the settled value of 0 this reduces to clip(noise), and the hash is never negative, so every ball
	//that is not transmuting keeps all of its pixels and pays a handful of ALU for the privilege.
	//
	//The cell is a block of the SCREEN, so input.Position is read as what SV_POSITION is in a pixel shader:
	//the pixel's centre in target pixels. Snapped to the block grid with floor, so every target sample
	//inside one block hashes the same and the resolve cannot average the dither away (see DissolvePixelSize).
	float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
	clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

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

	//And on top of the resting breath, the ripple: the light that runs out through the cluster from
	//wherever a ball has just landed. WHEN this ball takes its turn was decided on the CPU by walking the
	//balls that touch each other outwards from the impact, so what arrives here is only how brightly it is
	//flaring this frame - the walk is a question about the cluster's connectivity, and a wave evaluated
	//from a world-space distance here would run straight through the holes a played cluster is full of
	//instead of around them.
	//
	//Branched on the UNIFORM, not on the per-instance value: the strength is the same for every instance in
	//a draw call, so the branch cannot diverge, and a renderer that never ripples pays nothing at all.
	[branch]
	if (RippleStrength > 0)
	{
		//The flare is mostly WHITE with the ball's hue in it, and that is not a stylistic preference - it is
		//the only thing that reads. Adding light in the ball's own colour piles it into the one channel that
		//is already near the top of the ACES curve, so a red ball taking a full-strength flare goes from
		//bright red to very slightly brighter red and the wave is invisible; measured, it was there in the
		//instance data at 0.97 and could not be seen on screen at all. Lifting the channels the ball does
		//NOT have is what turns it white-hot, which is what "lighting up" looks like.
		//
		//Normalising the hue to peak 1 first also settles the dark types: primary runs from a full-strength
		//red down to the 8-ball's 0.045 grey, and multiplying that raw would leave the black balls out of
		//the wave entirely. Light passing through a cluster does not care what colour the ball under it is.
		float amount = abs(input.Ripple);
		float peak = max(primary.r, max(primary.g, primary.b));

		float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);

		//The alarm REPLACES the ball's colour rather than adding to it, and that is the whole difference
		//between a warning and a wash. Added, a red flare on a green ball is green plus red, which is yellow;
		//on a red one it is a slightly brighter red, and on black a pale grey - every ball came out a
		//different pastel and none of them said "red". Blended, the cluster momentarily TURNS red, which is
		//a thing the player cannot mistake for the scene doing something of its own.
		float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

		//A select and not an if, for the reason the dissolve's clip is one: the sign varies PER INSTANCE, so
		//a branch on it would diverge inside a single draw call. Both sides are a handful of ops.
		shaded.rgb = input.Ripple < 0 ? alarmed : lit;
	}

	//The hand-rolled vinyl sheen that used to sit here is gone: it was a Fresnel reflection of the sky,
	//which ShadePixel's specular ambient now does for every surface with a real dielectric F0 behind it.
	//Two Fresnel sky terms stacked on one sphere - where a grazing angle covers most of what you can see
	//of it - is what was bleaching the balls out under a bright dome.

	//Submerge fade: a ball below the sea level dims into the deep-water tint and becomes transparent over a
	//shallow band, so it reads as sinking into dark water rather than being cut off by the opaque surface
	//(see SeaLevelY). Disabled (a no-op) off the sea scene, where SeaFadeDepth is pushed <= 0.
	//
	//The colour is scaled towards zero WITH the alpha, not only lerped to the tint: this output rides
	//premultiplied alpha, and a fade that leaves rgb standing turns every faded pixel ADDITIVE. One sinking
	//ball hides it (the residue is the near-black tint, once), but a released cluster piles hundreds of
	//half-sunk balls into the pool standing in the drain (#132), and their residues stack into a pale glowing
	//mush over the dark water. Found the moment the pool gave them something dark to stack against.
	shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

	//And the kill plane's own fade (#192) on top of it, for the ball about to be culled under the island.
	return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelPattern
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL PatternVS();
		PixelShader = compile PS_SHADERMODEL PatternPS();
	}
};

//===================================================================================================
//THE GLASS BUBBLE (#258): the same sphere, the same instances, the same once-per-frame walk and the
//same vertex shader, shaded as a soap film blown round a pocket of air instead of as a moulded vinyl
//skin. A map names which of the two it hangs (Level.Balls), and nothing else about the game changes -
//the lattice, the physics, the match rule and the score are all about a ball's TYPE, which this does
//not touch.
//
//It is a second TECHNIQUE and not a branch inside PatternPS, for the reason measured on this project's
//other big shaders: a runtime branch over a whole alternative shading model costs the union of both
//register allocations in every wavefront, and these passes are occupancy-bound. Two techniques compile
//two programs and each draw picks one.
//
//WHAT MAKES IT READ AS A BUBBLE, in the order the eye picks them up:
//  1. It is TRANSPARENT, which is what the drawing states have to buy: the shell writes no depth on the
//     way through and the two walls are drawn as two passes with opposite cull modes (BallRenderSet.Draw
//     carries that whole argument). Everything below assumes the output is PREMULTIPLIED - rgb is the
//     light the film sends the eye, alpha only how much of the picture behind it that film hides.
//  2. The RIM. A film is crossed nearly edge-on along the silhouette, so it both mirrors most and hides
//     most there. One expression gives both, which is why a bubble is a bright ring around a nearly
//     empty middle rather than a tinted disc.
//  3. The IRIDESCENCE - the soap rainbow. Light reflected off the film's two faces interferes, and which
//     wavelengths survive depends on how far apart those faces are along the line of sight. That is a
//     real thin-film computation here rather than a rainbow texture, so it moves correctly: it stretches
//     towards the rim, drains downward under gravity and marbles with the film's own thickness.
//  4. The ball's COLOUR, which the game cannot do without: thirteen types have to stay apart at a glance
//     across a whole cluster. Physical soap is very nearly colourless, so this is where the model is
//     deliberately left: the film is dyed, and it carries its type's hue in what it transmits, in what it
//     radiates on the heartbeat and in what it flares with when a ripple reaches it.
//===================================================================================================

//Which wall of the shell this draw is putting out: +1 the near one, seen from outside, -1 the far one,
//seen from inside. A per-PASS uniform rather than anything on the instance because it IS a pass - the two
//walls are separate draw calls with opposite cull modes, which is how a hollow shell gets drawn without
//sorting three thousand balls against one another every frame.
float BubbleShell;

//Optical thickness of the film seen face-on, in whole waves of the reference wavelength - the pitch of the
//interference, and so the whole character of the rainbow. Under about 1 the film is in its first order and
//shows broad single-colour washes; over about 4 the fringes crowd together into a fine oily marbling.
float BubbleFilmThickness;

//How much of its type's colour the film carries into what it transmits. The one dial that decides whether
//a cluster of thirteen colours is still readable at a glance, which is why it is a uniform the C# side
//states and defends rather than a constant buried here.
float BubbleTintStrength;

//What the film hides where it is seen face-on, before the rim adds its own - and the one figure that decides
//whether a ball's colour is nameable, because what a film does NOT hide is the backdrop arriving UNTINTED.
//The C# side states and defends it (BallRenderSet.BUBBLE_BODY_OPACITY carries the arithmetic and the history).
float BubbleBodyOpacity;

//Ratios of the reference wavelength to the three the eye samples - red 680 nm over red, green and blue
//(550 and 450 nm). The interference phase scales with 1/wavelength, so these ARE the phase ratios, and
//using them rather than a hand-picked triple is what makes the fringe order come out in spectral sequence
//instead of as three unrelated sinusoids.
static const float3 BubbleWavelengthRatio = float3(1.0, 1.236, 1.511);

//A hanging film drains: gravity pulls the soap down, so the crown thins towards the black spot a real
//bubble shows just before it bursts and the underside carries the heavy fringes. Measured against the
//WORLD normal and not the object one, deliberately, because gravity does not turn with the ball.
static const float BubbleFilmDrain = 0.7;

//...and how far the film's own turbulence marbles that, on the same octave sum the vinyl skin uses for its
//moulding. This one IS in object space, so it turns with the ball - it is what replaces the beach ball's
//gores as the cue that a bubble is rolling.
static const float BubbleFilmVariation = 0.5;

//How hard the shell crowds its light and its coverage into the silhouette. 3 to 4 is a soap film's own
//falloff closely enough; lower reads as thick blown glass, higher as a wire ring with nothing in it.
static const float BubbleRimPower = 3.5;

//The bright edge on top of that: tighter than the coverage rim, so it reads as a RING drawn on the ball
//rather than as the ball being darker in the middle - how strong it is, and how far it is carried to white.
static const float BubbleEdgePower = 6.0;
static const float BubbleEdgeStrength = 1.5;
static const float BubbleEdgeWhiten = 0.12;

//How much of the rainbow survives into the reflection. Short of 1 so a white highlight stays white at its
//core - a fully iridised specular has no neutral in it and stops reading as a light source.
static const float BubbleIridescence = 0.8;

//The film's own highlight: far tighter than the vinyl skin's 40, because a soap film is a mirror and not a
//gloss coat, and correspondingly brighter so the pinpoint survives being that small.
static const float BubbleGloss = 220.0;
static const float BubbleGlossStrength = 1.6;

//How much of the highlight counts as coverage. A specular is light coming OFF the front of the film, so it
//is not something transparency takes away (SpecularAlphaWeight makes exactly this argument for the crystal
//cup) - but a pinpoint the picture behind shows straight through does not read as a highlight at all, so
//the brightest part of it closes the film underneath itself.
static const float BubbleHighlightOpacity = 0.5;

//What the far wall is worth against the near one. Both walls are drawn for every bubble and the far one is
//seen THROUGH the near one, so at parity a bubble would carry two full rims and read as a double-walled
//jar. Half is enough to say the thing is hollow.
static const float BubbleInnerWall = 0.5;

//How far the occlusion vector's reading is stretched before it counts as "screened by the pile". That vector
//is a SUM of up to twelve unit vectors over twelve, and neighbours on opposite sides cancel — a ball with a
//full layer of them towards the camera reads about a quarter, so a quarter has to mean most of the way in.
static const float BubbleScreenReach = 3.0;

//...and how much of a shell that reading is allowed to take away. Short of 1 deliberately: a ball deep in the
//pile should go faint, not vanish, or the cluster reads as a hollow shell of balls around nothing and the
//holes a played field is full of stop being holes.
static const float BubbleScreenFade = 0.8;

//How near the silhouette the interference is still evaluated honestly. The path through a film goes as
//1/cos, which runs away at the rim into fringes finer than a pixel; clamping the cosine caps the pitch
//there instead, and the band-limit below fades what is left.
static const float BubbleGrazingClamp = 0.2;

//THE ONE PLACE THE FILM PARTS COMPANY WITH THE SKIN OVER WHAT THE NEIGHBOURS TAKE, and it is not a
//preference. An opaque ball hides the ones behind it, so occlusion there only has to darken the crevices
//between touching spheres; a film hides nothing, so every ball in the pile reaches the eye and what a
//pixel shows is the SUM over four or five of them. At the skin's falloff a cluster's interior added up to
//a lamp: photographed on the space scene, where the sky contributes nothing and the balls' own light is
//all there is, the middle of a 438-ball cluster came out a flat pastel wash with no ball in it anywhere.
//Squaring the occlusion is the whole correction - a fully surrounded bubble keeps about a fifth of its
//light rather than half - and it is applied to the EMISSION as well, which is the second half of the same
//point: "a light buried in the pile is the one that should still show" is an argument about a ball you can
//only see one of, and it is false of a pile you can see all the way into.
static const float BubbleOcclusionPower = 2.0;

//One lamp's pinpoint on the film. The rig's own AddLight is not it: that is Blinn-Phong over the vinyl's
//SpecularPower with a Lambert diffuse beside it, and a film has neither - it mirrors, and everything else
//it does with light it does by transmitting.
void AddBubbleHighlight(float3 towardsLight, float3 lightSpecular, float3 worldNormal, float3 eyeVector,
	inout float3 hotspot)
{
	float3 halfway = normalize(towardsLight + eyeVector);

	hotspot += lightSpecular * pow(saturate(dot(worldNormal, halfway)), BubbleGloss);
}

float4 BubblePS(PatternVertexShaderOutput input) : COLOR
{
	float radius = max(length(input.ObjectPosition), 1e-5);
	float3 direction = input.ObjectPosition / radius;

	//The dissolve cut, character for character what PatternPS does and for its reasons: a bubble being
	//transmuted in the bore has to go away in blocks of the screen like every other ball. Branchless.
	float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
	clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

	//Turned to face the eye. On the far wall the geometric normal points away from the camera - it is the
	//inside of the shell - and every term below is about the film as the eye meets it, so one multiply by
	//the pass's own sign puts both walls through the same arithmetic.
	float3 normal = normalize(input.WorldNormal) * BubbleShell;
	float3 eyeVector = normalize(EyePosition - input.WorldPosition);
	float facing = saturate(dot(normal, eyeVector));

	float3 tint = SrgbToLinear(PatternPrimaryColor);

	//How much surface one screen pixel covers, over the ball radius - the same yardstick the vinyl skin
	//band-limits its moulding against, and here what keeps the fringes from strobing on a distant ball.
	float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

	//THE FILM. Thin at the crown, heavy underneath, marbled by its own turbulence.
	float drain = lerp(1.0 - BubbleFilmDrain, 1.0 + BubbleFilmDrain, saturate(0.5 - 0.5 * normal.y));
	float thickness = BubbleFilmThickness * drain
		* (1.0 + BubbleFilmVariation * SurfaceRelief(direction, footprint));

	//Interference. The two faces of the film are `thickness` apart along the normal and the eye crosses
	//them at `facing`, so the path between them - and with it the phase - goes as 1/cos: the fringes stretch
	//and multiply towards the rim, which is exactly where a real bubble shows its bands.
	float path = thickness / max(facing, BubbleGrazingClamp);
	float3 interference = 0.5 + 0.5 * cos(6.2831853 * path * BubbleWavelengthRatio);

	//Faded out where one pixel starts to span a whole fringe, or the rainbow turns into coloured noise that
	//crawls as the camera moves. Band-limited against the SCREEN-SPACE derivative of the path — how much of
	//a fringe one pixel actually covers — and not against the pixel footprint times the path, which is what
	//this said first and what is wrong: that measures the pixel's reach across the BALL, and a ball nine
	//tenths of a fringe wide is perfectly resolvable while one pixel of it is not. Measured, that fade was
	//already fully closed at the stand-off a level is played from, so the whole effect existed only in the
	//arithmetic. Nyquist puts the limit at half a fringe per pixel; this holds full colour to a fifth of one.
	float fringe = fwidth(path);
	float bands = saturate((0.5 - fringe) * 3.3);

	//The interference averages 0.5, so doubling it leaves a tint that averages white and the fade lands on
	//exactly that.
	float3 film = lerp(float3(1, 1, 1), interference * 2.0, BubbleIridescence * bands);

	//What the neighbours take, and they take it harder than they take it from a skin — see
	//BubbleOcclusionPower, which is the whole of why.
	float occlusion = pow(SurfaceOcclusion(input.WorldPosition, normal, input.OcclusionData),
		BubbleOcclusionPower);

	//What it mirrors. A soap film is a dielectric like every other surface here, so it reflects the same
	//4 % head-on and rises to a full mirror along the rim - and being smooth, it shows an IMAGE of the sky
	//rather than the sky's average.
	float3 fresnel = FresnelSchlick(DielectricF0.xxx, facing, 1.0);
	float3 reflected = SkyRadiance(reflect(-eyeVector, normal)) * fresnel * film
		* SpecularAmbientStrength * occlusion;

	//The lamps' pinpoints, the key one under the weather like every other surface in the scene.
	float3 hotspot = 0;
	AddBubbleHighlight(normalize(KeyLightPosition - input.WorldPosition),
		DirLight0SpecularColor * CloudSunlight(input.WorldPosition, SunDirection), normal, eyeVector, hotspot);
	AddBubbleHighlight(-DirLight1Direction, DirLight1SpecularColor, normal, eyeVector, hotspot);
	AddBubbleHighlight(-DirLight2Direction, DirLight2SpecularColor, normal, eyeVector, hotspot);
	hotspot *= DirLightStrength * BubbleGlossStrength * occlusion;

	//And the scene's own lamps - the campfire, the neon ring, the cavern's crystals. Their diffuse share is
	//kept: a nearby fire does not just spark off a bubble, it fills it with warm light.
	float3 lampWash = 0;
	AddSceneLights(input.WorldPosition, normal, eyeVector, lampWash, hotspot);

	hotspot *= film;

	//HOW MUCH OF THE PILE STANDS BETWEEN THIS SHELL AND THE EYE, and it is the answer to the one thing that
	//looked wrong about a bubble cluster: several layers of balls all showing through one another with EQUAL
	//clarity, where the eye expects each layer behind the last to be fainter than it. Nothing was fading them,
	//and the reason is in DrawShell's own note — the far walls are drawn with no depth write, so every ball in
	//the pile puts its inner rim into the picture whether or not four other balls stand in front of it, and
	//bucket order decides which of those lands on top.
	//
	//The proper cure is a back-to-front sort, which this renderer structurally cannot do (colour is a per-draw
	//uniform). This is the order-INDEPENDENT stand-in, and it costs one dot product: the occlusion vector
	//already carries the DIRECTION this ball's occupied neighbours lie in, so dotting it against the eye asks
	//exactly "are my neighbours between me and the camera?". Positive means the ball is screened by the pile
	//and is faded; a ball on the cluster's near face has its neighbours BEHIND it, so the dot goes negative and
	//it is left alone. A shot in flight and a loaded round carry a zero vector and are untouched by
	//construction.
	//
	//It is a per-BALL figure, so a shell fades as a whole rather than pixel by pixel, which is right: what is
	//being modelled is the depth of the pile in front of it, not the shape of its own surface.
	float screened = saturate(dot(input.OcclusionData.xyz, eyeVector) * BubbleScreenReach);

	//WHAT THE FILM HIDES. Face-on almost nothing; along the rim, where the eye looks the long way through
	//it, nearly everything. The far wall is worth half, for the reason BubbleInnerWall gives.
	float wall = (BubbleShell > 0 ? 1.0 : BubbleInnerWall) * (1.0 - screened * BubbleScreenFade);
	float rim = pow(1.0 - facing, BubbleRimPower);
	float alpha = saturate(BubbleBodyOpacity + (1.0 - BubbleBodyOpacity) * rim) * wall;

	//WHAT COMES THROUGH IT, in the ball's own colour: the film is dyed, so the light it passes is dyed too.
	//Tied to `alpha` on purpose - what a wall transmits is what it took out of the picture behind it, so a
	//film that hides nothing tints nothing, and the two cannot drift into a coloured haze over open sky.
	//
	//WHAT ARRIVES IS TAKEN AS A BRIGHTNESS AND NOT AS A COLOUR, and that is a deliberate departure from the
	//physics, made for the one constraint this game cannot trade away: thirteen types have to stay apart at a
	//glance under eighteen domes. Multiplied as a colour, a dye can only pass what the backdrop happens to
	//contain - and a RED film over the meadow's BLUE sky passes almost nothing, so the opening block's red
	//pyramid came out pink whatever the dye was set to. The backdrop's hue does not get a veto over the
	//ball's; how much light there is does. Rec. 709 luminance, the same weights the rest of the pipeline uses.
	float skyThrough = dot(SkyRadiance(-eyeVector), float3(0.2126, 0.7152, 0.0722));

	//The scene's own lamps take the same reading, and for the same reason: a campfire seen through a green
	//film is green light, not a warm cast on a green ball.
	float lampThrough = dot(lampWash, float3(0.2126, 0.7152, 0.0722));

	float3 through = (skyThrough * alpha + lampThrough) * tint * BubbleTintStrength * occlusion;

	//AND THE EDGE, which is what actually makes a bubble a bubble rather than a tinted disc. Along the
	//silhouette the eye looks the long way ALONG the film, and the same path length that makes it opaque
	//there is the path everything it carries has been dyed over - so a bubble's rim is both its brightest
	//part and its most saturated one.
	//
	//It is here and not left to the Fresnel mirror above because that mirror shows the SKY, and four of the
	//scenes have next to none: under space, the cavern, a night dome or the pit, the reflection returns
	//nothing at all and every ball came out a flat coloured circle with no edge on it. Photographed on the
	//space scene, which is the case that found it.
	//
	//Carried to white the way the ripple's flare is, and normalised to peak 1 first for the ripple's other
	//reason: the dark types (black, navy, brown) would otherwise have no edge to speak of, and how brightly
	//a film catches light along its rim has nothing to do with what colour it was dyed.
	float tintPeak = max(tint.r, max(tint.g, tint.b));
	float edge = pow(1.0 - facing, BubbleEdgePower);
	float3 rimGlow = lerp(tint / max(tintPeak, 1e-3), float3(1, 1, 1), BubbleEdgeWhiten)
		* (edge * BubbleEdgeStrength * wall * occlusion * film);

	//Emission on the heartbeat, the vinyl skin's own - but OCCLUDED, which its is deliberately not. See
	//BubbleOcclusionPower: that rule is an argument about a ball whose neighbours you cannot see past, and
	//it is exactly false of a pile of films. Times the wall weight, so the two shells together radiate one
	//ball's worth.
	float beat = Heartbeat(PulseTime * PulseSpeed - dot(input.WorldPosition, PulseDirection) / max(PulseWavelength, 1e-4));
	float3 emitted = tint * EmissiveStrength * lerp(1 - PulseDepth, 1, beat) * wall * occlusion;

	float4 shaded = float4(reflected + hotspot + through + emitted + rimGlow, alpha);

	//The pinpoint closes the film under itself (see BubbleHighlightOpacity). After the colour, because it
	//is coverage the highlight ADDS rather than a share of it that the highlight is scaled by.
	shaded.a = saturate(shaded.a + max(hotspot.r, max(hotspot.g, hotspot.b)) * BubbleHighlightOpacity * wall);

	//The landing ripple, the same wave PatternPS carries and switched off by the same uniform. Two
	//differences, both forced by the film being transparent: the flare has to raise the ALPHA as well or a
	//wave running through open sky is invisible, and the alarm - which replaces the ball's colour rather
	//than adding to it - has to close the film almost completely, or a cluster "turning red" would only be
	//tinting the sky behind it.
	[branch]
	if (RippleStrength > 0)
	{
		float amount = abs(input.Ripple);
		float peak = max(tint.r, max(tint.g, tint.b));

		float3 lit = shaded.rgb + lerp(tint / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount * wall);
		float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness * wall, amount * RippleAlarmCoverage);

		//A select and not an if: the sign varies PER INSTANCE, so a branch on it would diverge inside one
		//draw call.
		shaded.rgb = input.Ripple < 0 ? alarmed : lit;
		shaded.a = saturate(shaded.a + amount * wall * (input.Ripple < 0 ? RippleAlarmCoverage : 0.5));
	}

	//The sea's submerge fade and the kill plane's, both exactly as the vinyl skin takes them - a bubble
	//that misses sinks into the same dark water and is culled under the same island.
	shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

	return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelBubble
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL PatternVS();
		PixelShader = compile PS_SHADERMODEL BubblePS();
	}
};

//Detail texturing: a texture that only modulates the existing material colors
//(DetailStrength 0 = untextured look), mapped either through the model's own UVs
//(InstancedModelDetailUV — required for objects that move, or the texture would swim
//across them) or projected along the world axes for models with no UVs at all
//(InstancedModelTriplanar, e.g. the arena's stone island).

//Triplanar: world units per texture tile = 1 / DetailScale. UV mapping: tiles per UV span.
float DetailScale;
//How strongly the detail texture modulates the material color (0 = not at all, 1 = fully)
float DetailStrength;
//Brightness compensation so a mid-gray detail texture does not darken the whole material
float DetailBoost;

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
//Headroom the cavity term carries above the relief's own amplitude, in world units. It was the depth the
//castle's mortar joints were sunk to, and it is kept at that exact figure because the cavity range is the
//one place the construction patterns reached that survives them: every surface in the game has been shaded
//through this number since the joints themselves stopped being drawn, so rounding it away would change the
//look of all of them for nothing.
static const float CavityHeadroom = 0.055;

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

	float3 dpdx = ddx(input.WorldPosition);
	float3 dpdy = ddy(input.WorldPosition);

	float height = SceneSurfaceHeight(input.WorldPosition, dpdx, dpdy);

	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);
	float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

	//Cavity shading needs only the height and applies cleanly, so this path runs it instead of the generic
	//relief marches — which would be reading a different surface than the one drawn here.
	float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
	float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

	return ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity);
}

//Window layout and look. These are uniforms now, set on every city draw from CitySceneConfig (whose
//defaults reproduce the values that used to be hard-coded here). Only the city technique reads them, so
//leaving them zero for the ball and scene draws is harmless.
//Vertical/horizontal window spacing (world units) and how much of each cell is glass rather than wall:
float WindowPitchY;
float WindowPitchX;
float WindowFillY;
float WindowFillX;

//The raised plaster frame moulding around each pane. A pane cut straight into a flat wall reads as a
//hole in it, not as a window; a real window is set into a frame, and the frame is render carried proud
//of the surrounding wall. The moulding lives between the glass (withinCell < WindowFill) and its outer
//edge (WindowFill + WindowFrameWidth), so it is the same material as the wall -- it catches light and
//casts its own shadow -- rather than a second surface pasted on.
float WindowFrameWidth;
//How high the moulding stands proud of the wall, in world units (0 = no frame, just a flat pane).
float WindowFrameHeight;
//How much the moulding's own relief also SHADES -- the lit top of the bead and the shadow it throws on
//the wall just past it (0 = the normal is tilted and nothing else). The same lesson FacadeGrainShading
//already taught: a tilted normal alone is invisible at a tower's distance, so the bead's height also
//darkens and lightens the render's tone, which at this range is the stronger cue.
float WindowFrameShading;

//Wall border kept clear of glass at every building edge (see CityPS): the grid is laid out per building
//with this margin held at every side, so no window is ever jammed into a corner and every tower matches.
float WindowMargin;

//Fraction of the windows that are lit, and the two colours they are lit with.
float WindowLitFraction;
float3 WindowWarm;
float3 WindowCool;

//How long a window holds one state before deciding again, how much that varies from window to window,
//and how much of an interval the switch itself takes.
float WindowHoldSeconds;
float WindowHoldVariation;
float WindowSwitchFade;

//How brightly the lit windows glow, and how dark the facade around them is
float CityWindowBrightness;

//Wall clock driving the windows switching on and off, in seconds
float CityWindowTime;

//0 = ordinary city, 1 = neon night city: near-black facades and vivid saturated signs, one hue per tower
float CityNeon;

//The plaster: what the wall between the windows is made of. Its albedo by day and under neon, how far that
//wanders from tower to tower, and the grain of the render itself.
//
//The albedo has to be a real one, and that is not a detail. NEITHER specular term is multiplied by albedo --
//a highlight does not care how dark the surface under it is -- so with a dark facade almost the whole
//brightness of a tower used to BE its white highlight and its grazing sky reflection. Take the shine off a
//dark wall and what is left is a black slab; the albedo comes up in the same change, and the light on the
//wall is then diffuse light, which is what plaster does with it.
float3 FacadeColor;
float3 FacadeNeonColor;
float FacadeColorVariation;

//Peak height of the render's grain in world units, and its base wave count per unit. What makes a wall read
//as plaster is not its BRDF but that the light plays over it: a flat box face takes the light as a flat box
//face however the reflectance is tuned.
float FacadeGrainStrength;
float FacadeGrainFrequency;

//How much that same grain also shades: the ambient it keeps out of its hollows, and the mottling of the
//render's own tone. Without it the grain is invisible at a tower's distance, where a few degrees of normal
//tilt move a matte surface by a percent.
float FacadeGrainShading;

//How the wall answers light against how the glass does (see SurfaceSpecular). The wall is rough and shows
//almost no highlight; the window keeps -- and is boosted past -- the shine the whole tower used to have,
//because a window IS the glass on a building and should be the only thing on it mirroring the sky.
float FacadeSmoothness;
float FacadeHighlight;
float WindowHighlightBoost;
float WindowReflectionBoost;

//Albedo of the glass itself: what is behind a pane is a dim room, not a rendered wall.
float3 WindowGlassColor;

//Glass is glass: no dial, and the value that makes FresnelSchlick and the roughness lerp identities, so a
//pane behaves exactly as every polished surface elsewhere in the scene does.
static const float WindowSmoothness = 1.0;

float Hash21(float2 p)
{
	p = frac(p * float2(123.34, 456.21));
	p += dot(p, p + 45.32);

	return frac(p.x * p.y);
}

//A fully saturated color from a hue in [0,1] - the neon signs' palette. Pure and bright; the brightness
//that makes them bloom comes from CityWindowBrightness, not from here.
float3 HueToRGB(float h)
{
	float3 k = frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0));
	return saturate(abs(k * 6.0 - 3.0) - 1.0);
}

//The render's grain, and it has to be NOISE rather than a sum of waves. The scene's own SurfaceReliefWorld
//sums seven sines along seven fixed 3D directions, which decorrelate handsomely over a ball or a triplanar
//floor -- but a facade is a FLAT, axis-aligned plane, and on one of those only each direction's projection
//into the plane survives. Several of the seven project alike, the sum interferes with itself, and the wall
//comes out under a regular diagonal weave: woven cloth, not plaster. (It is the trap the cannon barrel
//already showed once, one surface further on, and the reason that function has seven octaves in the first
//place -- on a plane no number of them is enough.) A hashed lattice has no preferred direction to interfere
//along, so it cannot weave however few octaves it is given.
//Returns the value AND both partial derivatives, in the units of p. Analytic rather than taken with
//ddx/ddy, and that buys two things: the tilt below needs no tangent frame reconstructed from screen
//derivatives, and with no gradient op left anywhere in the grain the whole of it can sit behind a branch on
//a uniform -- which is this shader's rule for scene-gated work, and the off switch a low quality preset needs.
float3 FacadeNoise(float2 p)
{
	float2 cell = floor(p);
	float2 f = p - cell;

	//Smoothstep and its own derivative. Not the raw fraction: a linear ramp's derivative jumps at every cell
	//boundary, which would print the noise lattice straight back onto the wall as a grid of creases in the
	//normal -- one regular pattern traded for another.
	float2 u = f * f * (3.0 - 2.0 * f);
	float2 du = 6.0 * f * (1.0 - f);

	float a = Hash21(cell);
	float b = Hash21(cell + float2(1, 0));
	float c = Hash21(cell + float2(0, 1));
	float d = Hash21(cell + float2(1, 1));

	//The bilinear patch written as a + k0.u + k1.v + k2.u.v, which differentiates by inspection
	float k0 = b - a;
	float k1 = c - a;
	float k2 = a - b - c + d;

	//All three scaled alike by the [0,1] -> [-1,1] remap, so the derivatives stay the derivatives OF the value
	return float3(
		(a + k0 * u.x + k1 * u.y + k2 * u.x * u.y) * 2.0 - 1.0,
		(k0 + k2 * u.y) * du.x * 2.0,
		(k1 + k2 * u.x) * du.y * 2.0);
}

//Three octaves of it, value and gradient together. Amplitudes sum to one, so FacadeGrainStrength stays the
//peak height in world units, and each octave fades out on its own once a pixel grows past half its cell --
//the same per-octave band-limiting ReliefOctave does, so the render is fully present on the towers around the
//arena and silently gone on the skyline behind them instead of boiling into a moire.
//
//Three and not more: a fourth would have a cell of some four centimetres of tower, which is inside two units
//of the eye -- closer than the play camera ever gets to a facade -- and it measured at 0.16 ms of the frame.
//Footprint arrives in the same units as the position.
float3 FacadeGrain(float2 position, float footprint)
{
	float3 grain = 0;
	float amplitude = 0.57;
	float frequency = 1.0;

	[unroll]
	for (int i = 0; i < 3; i++)
	{
		float3 octave = FacadeNoise(position * frequency);

		//The gradient is with respect to this octave's own scaled position, so it carries its frequency back
		grain += amplitude * saturate(1 - footprint * frequency * 2.0) * float3(octave.x, octave.yz * frequency);

		amplitude *= 0.5;
		frequency *= 2.17;   //not an exact doubling, which would line successive octaves' lattices up
	}

	return grain;
}

//The profile of one edge of the plaster frame moulding around a pane, as a function of the per-axis cell
//coordinate `withinCell` (0 at the pane centre, 1 at the wall centre between panes). It returns THREE
//things as a float3, each of which the shading below reads separately so the moulding reads as a real
//raised body and not as a painted-on stripe:
//
//   .x = the bead height (0 off the moulding, 1 on its crest). Drives the normal tilt.
//   .y = the bead's analytic slope (signed: +rising up the inner flank toward the glass, -falling down
//        the outer flank toward the wall). Tilts the normal on both flanks so light catches the crest.
//   .z = a sharper "on the crest" mask, near-1 only across the flat top of the bead and 0 on the flanks.
//        This is what gets LIGHTENED -- the crest stands proud, catches the sun, reads as the top face of
//        a real piece of trim, where a height field alone would leave its top the same shade as its sides.
//
//The moulding occupies the ring between the glass edge (withinCell = WindowFill) and its own outer edge
//(WindowFill + WindowFrameWidth). The crest is a real flat top -- a classical profile is a flat fillet, not
//a needle -- occupying the middle third of the ring, with the two flanks rising to and falling from it.
//The flanks are kept SHARP (a fraction of the footprint, not the whole bead width): a moulding that reads
//as standing off the wall has crisp edges, and the first version's mistake was softening the whole bead
//across the footprint, which smeared it into the blurry line you could not read as 3D.
//
//`soft` widens only each flank by the pixel footprint (so a sub-pixel edge still anti-aliases rather than
//shimmering), never the whole bead. At WindowFrameWidth <= 0 this returns 0 (frame off).
float3 WindowFrameProfile(float withinCell, float WindowFill, float WindowFrameWidth, float footprint)
{
	float3 result = 0;

	if (WindowFrameWidth > 0.0)
	{
		float edge0 = WindowFill;
		float edgeOuter = WindowFill + WindowFrameWidth;
		float crest0 = WindowFill + WindowFrameWidth * 0.34;
		float crest1 = WindowFill + WindowFrameWidth * 0.66;

		//The two flanks are each anti-aliased across roughly one pixel -- sharp, but not a shimmering hard
		//step. The flat crest between them is the fillet's own top face.
		float soft = max(footprint * 0.7, WindowFrameWidth * 0.06);
		float inner = smoothstep(edge0 - soft, edge0 + soft, withinCell);
		float outer = 1.0 - smoothstep(edgeOuter - soft, edgeOuter + soft, withinCell);
		float bead = saturate(min(inner, outer));

		//The flat top: 1 between crest0 and crest1, softening off across a pixel at each end of the fillet.
		float topSoft = max(footprint * 0.7, WindowFrameWidth * 0.06);
		float crest = smoothstep(crest0 - topSoft, crest0 + topSoft, withinCell)
			* (1.0 - smoothstep(crest1 - topSoft, crest1 + topSoft, withinCell));

		//The slope is the bead's height derivative: +1 scaled up the inner flank, -1 down the outer, ~0 on
		//the flat crest. Used to tilt the normal; on the crest itself it is ~0, which is correct (a flat
		//top face has the wall's own normal). Analytic so no ddx/ddy is needed in the frame block.
		float innerSlope = 6.0 * inner * (1.0 - inner) / max(2.0 * soft, 1e-5);
		float outerSlope = 6.0 * outer * (1.0 - outer) / max(2.0 * soft, 1e-5);
		float slope = innerSlope - outerSlope;

		result = float3(bead, slope, crest);
	}

	return result;
}

//The city needs each building's own extent, not just world position, so windows can be laid out relative to
//the tower (a consistent edge margin) instead of on a world grid that clips them at the corners. This VS
//hands the pixel shader the offset from the building's centre and the building's world size. The box is the
//1x1x1 unit cube, so a transformed unit direction is the world size along that axis and the transform of the
//object origin is the centre; Bone is applied exactly as for the position. Everything else matches MainVS.
struct CityVSOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
	float3 WorldNormal : TEXCOORD1;
	float4 OcclusionData : TEXCOORD2;
	float3 PosFromCenter : TEXCOORD3;
	float3 BuildingSize : TEXCOORD4;
};

CityVSOutput CityVS(VertexShaderInput input, InstanceInput instance)
{
	CityVSOutput output;

	float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
	float4 worldPosition = mul(mul(input.Position, Bone), world);

	output.WorldPosition = worldPosition.xyz;
	output.Position = mul(mul(worldPosition, View), Projection);
	output.WorldNormal = mul(mul(float4(input.Normal, 0), Bone), world).xyz;
	output.OcclusionData = instance.Custom;

	float3 center = mul(mul(float4(0, 0, 0, 1), Bone), world).xyz;
	output.PosFromCenter = worldPosition.xyz - center;
	output.BuildingSize = float3(
		length(mul(mul(float4(1, 0, 0, 0), Bone), world).xyz),
		length(mul(mul(float4(0, 1, 0, 0), Bone), world).xyz),
		length(mul(mul(float4(0, 0, 1, 0), Bone), world).xyz));

	return output;
}

//Windows laid out relative to the building rather than on a world grid: a fixed wall margin (WindowMargin)
//is held at every edge and the windows are spread evenly across the interior, so none is jammed into a
//corner and every tower carries the same border. The count is solved from the building's world size against
//the target pitch, so a taller tower still gets more floors and a wider one more columns.
float4 CityPS(CityVSOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	//Which pair of world axes runs across this facade. Branchless: a lerp on the face's own normal,
	//which is constant over a flat face, so the derivatives below stay well defined.
	float facingX = step(abs(worldNormal.z), abs(worldNormal.x));
	//Facade coordinates measured from the building centre (horizontal axis picked by the facing), and the
	//building's half-size along the same two axes. Vertical is always world Y.
	float2 posFromCenter = float2(lerp(input.PosFromCenter.x, input.PosFromCenter.z, facingX), input.PosFromCenter.y);
	float2 halfSize = float2(lerp(input.BuildingSize.x, input.BuildingSize.z, facingX), input.BuildingSize.y) * 0.5;

	//Roofs and the ground faces get no windows
	float vertical = 1 - step(0.5, abs(worldNormal.y));

	//Interior left for glass after the wall margin, the whole windows that fit at the target pitch, and the
	//per-building pitch that fills the interior evenly (kept near the target, so it still reads natural).
	float2 interior = halfSize - WindowMargin;
	float2 count = floor(interior * 2.0 / float2(WindowPitchX, WindowPitchY) + 0.5);
	float hasGrid = step(1.0, count.x) * step(1.0, count.y) * vertical;
	float2 cellPitch = (interior * 2.0) / max(count, 1.0);

	float2 grid = (posFromCenter + interior) / cellPitch;
	float2 cell = floor(grid);
	float2 withinCell = abs(frac(grid) - 0.5) * 2;

	//The pixel's extent across the facade, per axis, in cells. Band-limited the way every other feature
	//here is: once a pixel covers more than a window the pattern fades to its own average rather than
	//aliasing into a moire of lit and unlit floors, which is what a city at distance would otherwise do.
	float2 footprint = (abs(ddx(posFromCenter)) + abs(ddy(posFromCenter))) / cellPitch;
	float resolvable = saturate(1 - max(footprint.x, footprint.y));

	float2 shape = smoothstep(float2(WindowFillX, WindowFillY) + footprint, float2(WindowFillX, WindowFillY) - footprint, withinCell);

	//No glass in the wall margin outside the interior (the cut lands in wall, so it needs no smoothing)
	float2 inside = step(abs(posFromCenter), interior);
	float window = shape.x * shape.y * hasGrid * inside.x * inside.y;

	//The building this facade belongs to, taken from the tower's own centre so it is one value across the
	//whole tower (neon hue per tower, and a window pattern that belongs to the building not the world grid)
	float facadeY = input.WorldPosition.y;
	float2 buildingId = floor((input.WorldPosition.xz - input.PosFromCenter.xz) * 0.37);
	float2 windowId = cell + buildingId * 101.0;

	//A window does not decide once and for all. Each keeps its own rhythm — a stretch of its own length,
	//then it decides again — so lamps come on and go out across the skyline at their own pace. A city
	//whose windows never change reads as a texture of a city rather than as one with people in it.
	float rhythm = Hash21(windowId + 3.71);
	float interval = WindowHoldSeconds + rhythm * WindowHoldVariation;
	float slot = CityWindowTime / interval + rhythm * 37.0;
	float slotIndex = floor(slot);

	float wasLit = step(1 - WindowLitFraction, Hash21(windowId + slotIndex * 17.13));
	float willBeLit = step(1 - WindowLitFraction, Hash21(windowId + (slotIndex + 1) * 17.13));

	//The switch is a short fade rather than a cut: at this distance a lamp that vanishes between two
	//frames reads as a rendering glitch, one that dies over a moment reads as somebody leaving
	float lit = lerp(wasLit, willBeLit, smoothstep(1 - WindowSwitchFade, 1, frac(slot)));

	//Ordinary warm/cool lamp for the plain daytime city
	float3 lamp = lerp(WindowWarm, WindowCool, step(0.5, Hash21(cell * 1.7 + 11.3)));

	//Fading to the average keeps a distant tower a dim glowing block instead of a flickering one
	float coverage = lerp(WindowFillX * WindowFillY * WindowLitFraction * hasGrid * inside.x * inside.y, window * lit, resolvable);

	//Where the facade is glass rather than plaster, which is what the material below is blended by. NOT the
	//emission's coverage: that one is multiplied by `lit`, and whether the lamp behind a pane happens to be
	//on says nothing about what the pane is made of -- a dark window is still the one part of the wall that
	//mirrors the sky. Faded to the windows' own area fraction at distance, the same band-limiting the
	//emission gets, so a far tower becomes one averaged material instead of aliasing between two.
	float glass = lerp(WindowFillX * WindowFillY * hasGrid * inside.x * inside.y, window, resolvable);

	//The plaster frame moulding around each pane. Two earlier versions were wrong in instructive ways: the
	//first combined the two axes with max and drew a grid over the facade; the second was a correct ring but
	//a height field at 0.015 world units, softened across the whole footprint, which read as a blurry line
	//rather than as trim standing off the wall. A moulding that reads as 3D needs three things a flat bead
	//lacks: a CRISP edge (so the eye sees a body, not a gradient), a LIT FLAT TOP that stands proud of the
	//wall and catches the sun, and a CAST SHADOW thrown onto the wall in the sun's lee. All three are below.
	//
	//WindowFrameProfile returns (.x height, .y slope, .z crestTop): the height tilts the normal, the crest
	//mask picks out the flat fillet's top to be lightened, and the slope carries the flank shading. The two
	//axes are gated by each other's pane span so the four pieces meet as one ring per window, never a grid.
	float3 frameX = WindowFrameProfile(withinCell.x, WindowFillX, WindowFrameWidth, footprint.x);
	float3 frameY = WindowFrameProfile(withinCell.y, WindowFillY, WindowFrameWidth, footprint.y);

	//The cross-axis span of one window plus its frame: 1 inside, softening to 0 across one pixel at the
	//pane's outer edge, so the bead stops where the wall between windows begins.
	float paneSpanX = 1.0 - smoothstep(WindowFillX + WindowFrameWidth - footprint.x, WindowFillX + WindowFrameWidth + footprint.x, withinCell.x);
	float paneSpanY = 1.0 - smoothstep(WindowFillY + WindowFrameWidth - footprint.y, WindowFillY + WindowFrameWidth + footprint.y, withinCell.y);

	//Side beads (height/slope/crest on X) exist only within the pane's vertical span; head/sill (on Y) only
	//within its horizontal span. The two never run past the pane, so they form a ring, not a grid.
	float frameBead = saturate(max(frameX.x * paneSpanY, frameY.x * paneSpanX)) * (1.0 - glass) * hasGrid * inside.x * inside.y * vertical;
	float frameCrest = saturate(max(frameX.z * paneSpanY, frameY.z * paneSpanX)) * (1.0 - glass) * hasGrid * inside.x * inside.y * vertical;
	frameBead = lerp(0.0, frameBead, resolvable);
	frameCrest = lerp(0.0, frameCrest, resolvable);

	//The cast shadow: the moulding stands proud of the wall, so it occludes the sun for the strip of wall on
	//its lee side. Sample the bead mask a little way DOWN-SUN from this pixel and, if that offset point sits
	//on the frame, this pixel is in the frame's shadow. The offset is the frame's own height projected onto
	//the wall along the sun's direction -- the same geometry a real projection-cast shadow uses, and the one
	//cue that most strongly reads as "this trim is raised", because a flat stripe casts nothing. `cellPitch`
	//turns the world-space offset back into the cell space `withinCell` lives in. Softened by the footprint
	//so the penumbra widens at distance instead of aliasing; zero where the sun is behind the camera (no
	//visible cast shadow then, and the division would blow up).
	float2 sunOnFacade = float2(lerp(SunDirection.x, SunDirection.z, facingX), SunDirection.y);
	float sunLen = length(sunOnFacade);
	float frameShadow = 0.0;
	if (sunLen > 0.05)
	{
		float2 offsetDir = sunOnFacade / sunLen;
		//Project the bead's height along the sun direction: taller trim throws a longer shadow.
		float2 offsetCells = offsetDir * WindowFrameHeight * 1.7 / cellPitch;
		float3 ssX = WindowFrameProfile(withinCell.x - offsetCells.x, WindowFillX, WindowFrameWidth, footprint.x);
		float3 ssY = WindowFrameProfile(withinCell.y - offsetCells.y, WindowFillY, WindowFrameWidth, footprint.y);
		float shadowBead = saturate(max(ssX.x * paneSpanY, ssY.x * paneSpanX)) * (1.0 - glass) * hasGrid * inside.x * inside.y * vertical;
		//Only the wall in the moulding's lee is shadowed, not the moulding itself, and not the glass.
		shadowBead *= (1.0 - frameBead) * (1.0 - glass);
		float penumbra = max(footprint.x, footprint.y);
		frameShadow = lerp(shadowBead, 0.0, saturate(penumbra * 3.0)) * resolvable;
	}

	float3 lampColor = lamp;
	float windowFlicker = 1.0;
	float3 signEmission = float3(0.0, 0.0, 0.0);

	//One tower is not the next. Real renders are mixed and painted and weathered per building, and once the
	//wall is matte its tone is the only variety it has left -- the tonal spread the skyline used to get came
	//from the mirror, and goes out with it. Off the building's own id, so it is one shade per tower.
	float3 facadeColor = FacadeColor * (1.0 + FacadeColorVariation * (Hash21(buildingId + 13.9) - 0.5) * 2.0);

	//Neon night city, gated at runtime by CityNeon; both the Testbed and the map editor drive it (V cycles
	//to the neon scene in the editor too). The skyline runs on magenta and cyan, the pink-and-blue of a neon
	//street, with the odd off-colour tower; about one window in six sparks the opposite hue, big sign bands
	//wrap some towers in the contrast colour, and a fraction of it all buzzes. Brightness sits over the glare
	//threshold, so every lit pane blooms.
	//
	//A uniform branch, deliberately: CityNeon is 0 in the ordinary city — the DEFAULT scene — and this block
	//is ~7 hashes of per-pixel work that the lerps below would throw away entirely at 0. A branch on a
	//uniform is non-divergent (every pixel takes the same path) and there are no gradient ops inside, so it
	//is derivative-safe; the defaults above already hold the plain-city values for the else path.
	[branch]
	if (CityNeon > 0.0)
	{
		float3 neonMagenta = float3(1.0, 0.04, 0.85);
		float3 neonCyan = float3(0.05, 0.85, 1.0);

		float pickBuilding = Hash21(buildingId + 5.0);
		float3 buildingNeon = pickBuilding < 0.45 ? neonMagenta : (pickBuilding < 0.9 ? neonCyan : HueToRGB(Hash21(buildingId + 6.3)));
		float3 contrast = buildingNeon.r > buildingNeon.b ? neonCyan : neonMagenta;
		float3 neonWindow = lerp(buildingNeon, contrast, step(0.83, Hash21(windowId + 4.4)));

		//A bright solid sign band wrapping some towers at a hashed height, in the contrast colour
		float hasSign = step(0.5, Hash21(buildingId + 21.0));
		float signHeight = 5.0 + Hash21(buildingId + 22.0) * 34.0;
		float signBand = hasSign * vertical * (1.0 - smoothstep(1.1, 1.9, abs(facadeY - signHeight))) * resolvable;

		//A fraction of the windows buzz on and off, the way a tired neon tube does
		float flickerId = Hash21(windowId + 8.8);
		float buzz = 0.55 + 0.45 * step(0.45, frac(CityWindowTime * (5.0 + flickerId * 9.0) + flickerId * 13.0));

		//Kept as lerps by CityNeon (not straight assignments), so a fractional CityNeon still blends exactly
		//as it did when this ran unconditionally
		lampColor = lerp(lamp, neonWindow, CityNeon);
		windowFlicker = lerp(1.0, lerp(1.0, buzz, step(0.86, flickerId)), CityNeon);
		signEmission = signBand * contrast * (CityWindowBrightness * 1.6) * CityNeon;
		facadeColor = lerp(facadeColor, FacadeNeonColor, CityNeon);
	}

	//The render's grain, in the facade's own 2D frame. Offset by the building's id so every tower is rendered
	//in its own patch rather than all of them wearing one pattern at the same height, and by the facing so a
	//tower's two axes do not match each other. Anchored to the building, like the window grid and for the same
	//reason; the pattern does not carry around a corner, which a hard 90-degree edge hides.
	//
	//The footprint is the window grid's own, multiplied back out of cells into world units along the facade --
	//that quantity is already abs(ddx) + abs(ddy) of posFromCenter, so reusing it saves a second pair of
	//derivative ops rather than merely tidying.
	float2 facadeFootprint = footprint * cellPitch;

	//Vertical faces only. A roof's posFromCenter is (z, a constant), so the field would degenerate into
	//stripes along one axis there; a flat concrete roof reading flat is the better answer, and it is matte
	//either way, since the material below does not depend on the grain.
	float3 grain = 0;

	//A branch on a uniform, which is what this shader's conventions ask for: FacadeGrainStrength is the one
	//dial that turns the render's grain off, everything below is multiplied away at 0, every pixel takes the
	//same path, and there is not a single gradient op inside -- the noise's gradient being analytic is exactly
	//what makes that last part true.
	[branch]
	if (FacadeGrainStrength > 0.0)
		grain = FacadeGrain((posFromCenter + buildingId * 37.0 + facingX * 19.0) * FacadeGrainFrequency,
			max(facadeFootprint.x, facadeFootprint.y) * FacadeGrainFrequency) * vertical;

	//A facade is an axis-aligned plane, and that is worth exploiting rather than working around: its two
	//tangents ARE world axes, so the height field's analytic gradient becomes a world-space tilt directly --
	//no tangent frame rebuilt from screen derivatives, no ddx of the height, and exact. The same flatness that
	//makes a sum of sines weave here is what makes this cheap.
	float3 tangentAcross = facingX > 0.5 ? float3(0, 0, 1) : float3(1, 0, 0);
	float3 slope = FacadeGrainStrength * FacadeGrainFrequency * (grain.y * tangentAcross + grain.z * float3(0, 1, 0));

	//The frame's own tilt rides the same mechanism: its analytic slope (frameX.y / frameY.y) becomes a
	//world-space lean on the bead's two flanks. The slope is gated by the bead mask, so it is exactly zero
	//over the glass and in the deep wall -- only the bead's two rising/falling edges tilt the normal, which
	//is what makes the moulding read as standing proud of the wall rather than as a painted-on stripe.
	//Scale by the bead height (world units) the way the grain's slope carries FacadeGrainStrength.
	slope += WindowFrameHeight * frameBead * (frameX.y * tangentAcross + frameY.y * float3(0, 1, 0));

	//Tilted first and blended back towards the flat face by the glass mask, never the other way round: a pane
	//is flat, but folding the mask into the height would put a window edge inside the gradient.
	float3 shadingNormal = normalize(worldNormal - slope * (1.0 - glass));
	float grainField = grain.x;

	//A tilted normal alone is not enough, and that is the lesson the ground's coursed slabs already taught:
	//relief by normal has its bumps lit and its hollows just as bright as its peaks, which is most of why it
	//reads as a painted-on texture rather than as shape. At a tower's distance a few degrees of tilt changes
	//a matte surface's N.L by a percent or two and is simply invisible. So the same field also SHADES -- it
	//darkens the ambient a hollow cannot see, and mottles the render's own tone, which at this range is the
	//stronger cue of the two. One field doing both is what a real render does: the hollows hold the shade and
	//the high spots wear lighter. Off over the glass, which is flat and evenly tinted.
	float plaster = 1.0 - glass;
	float cavity = 1.0 - FacadeGrainShading * saturate(-grainField) * plaster;

	facadeColor *= 1.0 + FacadeGrainShading * grainField * plaster;

	//The frame's three shading cues, each strong enough to read at a tower's distance:
	//  - the FLAT TOP lightens, because a fillet standing proud catches the sky and the sun a flat wall does
	//    not. This is the cue that says "the top of a body", and it is why the profile keeps a crest mask
	//    separate from the height: lightening the whole bead would light its shadowed flanks too.
	//  - the CAST SHADOW darkens the wall in the moulding's lee (frameShadow, computed above). This is the
	//    cue that says "a body that occludes the sun", and a flat stripe casts none -- which is exactly why
	//    the height-field-only version read as paint.
	//  - the cavity term still darkens the bead's own hollows for the grain, untouched by the frame.
	facadeColor *= 1.0 + WindowFrameShading * 1.4 * frameCrest;
	facadeColor *= 1.0 - WindowFrameShading * 0.9 * frameShadow;
	cavity = saturate(cavity - WindowFrameShading * 0.5 * frameShadow);

	//And the glass gets its own albedo, which is DARK: what is behind a pane is a dim room, not a rendered
	//wall. That is the other half of why the windows read as glass and the wall does not -- a dark surface
	//under a bright mirror is exactly what glass looks like, and it is the same combination that was wrong
	//on the plaster. It also gives a facade its variation back: a pane is dark where it faces nothing and
	//bright where it catches the sky, which is how a glazed tower reads at all.
	facadeColor = lerp(facadeColor, WindowGlassColor, glass);

	//Two materials on one triangle, blended per pixel: rough plaster, and the glass of the windows in it.
	SurfaceSpecular surface;
	surface.Highlight = lerp(FacadeHighlight, WindowHighlightBoost, glass);
	surface.Environment = lerp(1.0, WindowReflectionBoost, glass);
	surface.Smoothness = lerp(FacadeSmoothness, WindowSmoothness, glass);

	float4 shaded = ShadePixel(input.WorldPosition, shadingNormal, input.OcclusionData, float4(facadeColor, 1), 1, cavity, surface);

	shaded.rgb += coverage * lampColor * CityWindowBrightness * windowFlicker;
	shaded.rgb += signEmission;

	return shaded;
}

technique InstancedCity
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL CityVS();
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

//The same surface with a COARSE height field: three relief octaves instead of seven. What a quality tier
//below High draws on the stone cap, and nothing else - see InstancedModelRenderer.CoarseSurfaceRelief and
//ArenaIsland.SurfaceDetail. It is a second TECHNIQUE and not a branch inside TriplanarPS for the reason
//the bubble's own header states and #155 measured: a runtime branch costs the union of both register
//allocations in every wavefront of an occupancy-bound pass.
//
//WHY THIS ONE AND NOT ONE OF THE OTHER CUTS (#151, measured on the reference desktop, Testbed, meadow,
//dome 13, play camera, windowed 1920x1080 at ssaa 4, four interleaved rounds):
//  - The shipped pass is 10.971 ms a frame and shading the cap as a CONSTANT is 9.481, so the cap's whole
//    pixel shader is 1.490 ms - 13.6 % of the frame, on a surface that is in every scene.
//  - Of that, the height field and the normal it tilts are 0.660 and this cut is 0.336. The three
//    triplanar taps are 0.029 (one tap instead of three) to 0.137 (no taps at all), i.e. nothing: the
//    third suspect this issue has named and measured at zero. The remaining ~0.83 ms is ShadePixel, which
//    every surface in the game is lit through and which this is not the issue to cut.
//  - Dropping the height field ENTIRELY saves twice as much and cannot ship: SlabGroove is part of the
//    same field, so the cap loses its coursed slab joints with it - the only structure the stone has at a
//    scale the eye can see. Three octaves keep the joints and give up the finest grain.
float4 TriplanarCoarsePS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	float3 blend = pow(abs(worldNormal), 4);
	blend /= blend.x + blend.y + blend.z;

	float3 p = input.WorldPosition * DetailScale;

	float3 detail
		= SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
		+ SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
		+ SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

	float3 dpdx = ddx(input.WorldPosition);
	float3 dpdy = ddy(input.WorldPosition);

	float height = SceneSurfaceHeightCoarse(input.WorldPosition, dpdx, dpdy);

	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);
	float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

	float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
	float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

	return ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity);
}

technique InstancedModelTriplanarCoarse
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL TriplanarCoarsePS();
	}
};

//===================================================================================================
//#151 - THE STONE CAP'S MEASUREMENT PROBES.
//
//Five cut-down copies of TriplanarPS, each with one named suspect taken out, chosen per draw by
//InstancedModelRenderer.TriplanarProbe (Testbed: capprobe=N; capprobe=3 is the coarse technique above,
//which is why the numbering skips it). They exist so that "where does the stone cap's per-pixel time
//go" is ONE build measured several ways rather than several builds measured against each other: the
//previous round of this issue needed six interleaved pairs of whole builds before a 0.26 ms effect was
//legible at all, because an hour into hammering the GPU a session drifts further than the effect does.
//One build cannot drift between its own variants.
//
//KEPT rather than deleted with the answer, for the reason ArenaIsland.Members and the Testbed's arena=
//were kept: the ratio #151 was opened on - 27 ms of a 42 ms frame - is the WEAK machine's and has still
//not been re-derived there, and these are what will do it in a few minutes instead of a rebuild. They
//cost a shipped frame nothing: each technique is its own program and nothing selects a probe unless the
//command line asks for it.
//===================================================================================================

//1 - no height field at all: the three taps and ShadePixel on the geometric normal, cavity off.
//Bounds SceneSurfaceHeight and PerturbNormalFromHeight together.
float4 TriplanarProbe1PS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	float3 blend = pow(abs(worldNormal), 4);
	blend /= blend.x + blend.y + blend.z;

	float3 p = input.WorldPosition * DetailScale;

	float3 detail
		= SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
		+ SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
		+ SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

	return ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(texRgb, 1), 1, 1);
}

//2 - the height field, but not the normal it tilts: seven octaves and the joints are still evaluated
//and still shade the cavity, only PerturbNormalFromHeight is gone. Probe 1 against this is the cost
//of the field; this against the shipped technique is the cost of the perturb's ddx/ddy pair.
float4 TriplanarProbe2PS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	float3 blend = pow(abs(worldNormal), 4);
	blend /= blend.x + blend.y + blend.z;

	float3 p = input.WorldPosition * DetailScale;

	float3 detail
		= SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
		+ SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
		+ SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

	float3 dpdx = ddx(input.WorldPosition);
	float3 dpdy = ddy(input.WorldPosition);

	float height = SceneSurfaceHeight(input.WorldPosition, dpdx, dpdy);

	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

	float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
	float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

	return ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity);
}

//4 - one texture tap instead of three, the world-XZ projection alone. Everything else is the shipped
//pass, so the difference is the two samples and the blend that fed them.
float4 TriplanarProbe4PS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	float3 p = input.WorldPosition * DetailScale;

	float3 detail = SrgbToLinear(tex2D(TextureSampler, p.xz).rgb);

	float3 dpdx = ddx(input.WorldPosition);
	float3 dpdy = ddy(input.WorldPosition);

	float height = SceneSurfaceHeight(input.WorldPosition, dpdx, dpdy);

	float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);
	float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

	float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
	float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

	return ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(texRgb, 1), 1, cavity);
}

//5 - no detail texture at all: the full height field, the perturb and ShadePixel on the flat material
//colour. Probe 4 and this one bracket the three taps from both sides.
float4 TriplanarProbe5PS(VertexShaderOutput input) : COLOR
{
	float3 worldNormal = normalize(input.WorldNormal);

	float3 dpdx = ddx(input.WorldPosition);
	float3 dpdy = ddy(input.WorldPosition);

	float height = SceneSurfaceHeight(input.WorldPosition, dpdx, dpdy);

	float3 reliefNormal = PerturbNormalFromHeight(worldNormal, input.WorldPosition, height);

	float cavityRange = max(SurfaceReliefStrength + CavityHeadroom, 1e-6);
	float cavity = lerp(1 - CavityStrength, 1, saturate((height + SurfaceReliefStrength + CavityHeadroom) / (cavityRange + SurfaceReliefStrength)));

	return ShadePixel(input.WorldPosition, reliefNormal, input.OcclusionData, float4(1, 1, 1, 1), 1, cavity);
}

//6 - a constant. The bound on the whole pixel shader, and the only figure here that says how much of
//the cap is shading at all rather than raster, depth and the vertex work behind it.
float4 TriplanarProbe6PS(VertexShaderOutput input) : COLOR
{
	return float4(0.2, 0.2, 0.2, 1);
}

technique InstancedModelTriplanarProbe1
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL TriplanarProbe1PS();
	}
};

technique InstancedModelTriplanarProbe2
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL TriplanarProbe2PS();
	}
};

technique InstancedModelTriplanarProbe4
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL TriplanarProbe4PS();
	}
};

technique InstancedModelTriplanarProbe5
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL TriplanarProbe5PS();
	}
};

technique InstancedModelTriplanarProbe6
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL TriplanarProbe6PS();
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
