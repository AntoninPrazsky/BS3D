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

//The shared noise library, for the one field in here a sum of waves genuinely cannot be: the ice's
//fracture net (#337). Everything else on a ball is built from sines on purpose - they are cheap, they
//band-limit exactly, and a surface wants a surface. A FRACTURE wants irregular closed cells, which is
//what no number of plane waves adds up to, and the library's own header is the argument for why.
#include "Noise.fxh"

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

//1 flips the shading normal on back faces (#291), for a mesh that is one OPEN single-sided wall drawn
//CullNone with its only normal pointing at the concave side - the drain's glass funnel and its dark pit.
//Without it the wall's outside is shaded with the inside's normal, and the grazing-angle sky sheen turned
//the glass cone, seen from below, into an opaque milky sheet. Read by MainPS alone - the one technique
//those two renderers draw through - and 0 for everything else. SV_IsFrontFace is constant across a
//primitive, so a derivative quad never straddles the select and nothing downstream takes gradients of the
//normal on this path.
float TwoSidedNormals;

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

//The burial ramp (#303), mirrored from BallRenderSet's OCCLUSION_STRENGTH / OCCLUSION_DEPTH_STRENGTH:
//the W channel bottomed at 1 - 0.55 = 0.45 while the twelve touching cells were all the grid knew, and
//the air-distance term now takes it a further 0.35 down to 0.10 at three shells under. Anything below
//the first-shell floor is therefore SAYING "shells of balls between this one and open air", and the
//two figures here are how that is read back out. Statics rather than uniforms deliberately: they are
//one contract with the C# constants, and a host that could set half of it could break it.
static const float OcclusionFirstShellFloor = 0.45;
static const float OcclusionDepthStrength = 0.35;

//How much of the key light a fully buried ball keeps. The first-shell floor on the diffuse (the
//lerp to 0.6 below) deliberately spared the key light - a crevice on the SURFACE still catches the
//lamp - but burial is a different fact: a lamp has no line of sight three shells into a pile.
static const float BurialKeyLight = 0.4;

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

    //Burial pulls the key light down where the first-shell floor deliberately did not (#303). Surface
    //pixels - W at or above the first-shell floor, which is every scene object and every ball touching
    //air - take exactly the multiplier they always took, by construction of the ramp.
    float burial = saturate((OcclusionFirstShellFloor - occlusionData.w) / OcclusionDepthStrength);
    float diffuseOcclusion = lerp(0.6, 1.0, occlusion) * lerp(1.0, BurialKeyLight, burial);

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

float4 MainPS(VertexShaderOutput input, bool isFrontFace : SV_IsFrontFace) : COLOR
{
    //An open single-sided wall drawn CullNone shades its outside with the inside's normal unless told
    //otherwise - see TwoSidedNormals. A branchless select: the uniform is 0 on every closed mesh.
    float3 worldNormal = (TwoSidedNormals > 0 && !isFrontFace) ? -input.WorldNormal : input.WorldNormal;

    //Untextured, unrelieved parts: nothing to shadow itself and no pits to darken
    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(1, 1, 1, 1), 1, 1);

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
//
//THE BEVEL IS WIDENED HARDER THAN THE WIDTH, AND THAT IS THE FIX FOR #351: two and a half pixels of
//run-out against the width's half. It is the shoulder, not the floor, that has to survive being sampled -
//and what it has to survive is the NORMAL being a per-quad quantity. PerturbNormalFromHeight builds its
//normal out of ddx/ddy, and a screen derivative is one value per 2x2 quad, so a shoulder that climbs
//inside one or two pixels tilts whole quads at a time. Strung along a joint running away from the eye that
//is a row of 2x2 bright blobs - the beading #126 chased into the chromatic aberration, fixed the fringing
//half of, and closed noting a residual. This is the residual. Stretched over ~5 pixels the same climb is
//spread across several quads and reads as a line again.
//
//Four things were measured on the way and none of them is worth rediscovering:
//  - it IS the direct highlight: forcing SurfaceSpecular.Highlight to 0 on this surface makes the beads
//    vanish outright, so nothing about the groove's darkening is at fault;
//  - Toksvig - the textbook answer, damping the highlight by the screen-space variance of the normal - is
//    useless here, because that variance is identically zero for exactly the reason above. Drawn out as a
//    colour the whole cap came back black, and the damping did nothing at 1.4 or at 50;
//  - the beads are NOT on the pixels the groove's coverage marks: damping the covered pixels to zero left
//    every one of them standing, and at the brightest beads the coverage-based gate measured 0.00;
//  - and they are not a sub-pixel problem either. At the brightest beads the pixel measured SMALLER than
//    the joint - the joint was fully resolved and beading anyway, which is what finally pointed at the
//    quad rather than at the footprint.
float SlabGrooveAxis(float coordinate, float footprint)
{
    float cell = frac(coordinate / SlabSize);
    float distance = min(cell, 1 - cell) * SlabSize;

    float width = max(SlabJointWidth, footprint * 0.5);
    float bevel = max(SlabJointBevel, footprint * 2.5);

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

//===================================================================================================
//THE BALL TECHNIQUES, AND THE CONTRACT ALL OF THEM OWE (#304)
//
//Everything from here to the end of InstancedModelBubble draws A BALL. A map names what its balls are
//made of and that picks one of these programs (Prazsky.BS3D.GameStructure.BallStyle ->
//Prazsky.Core.Render.BallShading -> the technique table in InstancedModelRenderer); nothing about the
//lattice, the physics, the match rule or the score reads it, so a bubble level plays move for move
//like a vinyl one.
//
//Each is a TECHNIQUE and not a branch inside another one, for the reason measured on this project's
//other big shaders: a runtime branch over a whole alternative shading model costs the union of both
//register allocations in every wavefront, and these passes are occupancy-bound.
//
//THE COST OF THAT IS NOT WHAT IT LOOKS LIKE. A frame draws ONE ball technique, because a level names
//one style - so the bubble's measured ~8-10% over vinyl is what a BUBBLE LEVEL pays, not a tax the
//other levels carry, and N styles are not N times anything per frame. What N styles cost is N programs
//to compile here and N looks to keep working across the eighteen domes.
//
//WHAT EVERY ONE OF THEM MUST CARRY. A ball technique is not "PatternPS with different lighting": most
//of what it does has nothing to do with what the ball is MADE of, and a new one that drops a line of
//the following fails silently - it looks right in a screenshot of a still cluster and is wrong in play.
//
//  1. THE DISSOLVE CLIP, on BOTH signs of input.Dissolve, over cells of DissolvePixelSize. It is the
//     magazine re-colouring a loaded ball; the sign says which direction. Screen space, and the cell
//     is a whole DISPLAY pixel or more - an object-space cell is a lumpy 3D mottling and a one-target-
//     pixel cell is averaged straight back into a smooth cross-fade by the supersample resolve.
//  2. THE HEARTBEAT, normally as the whole of BallEmission(primary, worldPosition, occlusion): the
//     position term in the phase is what makes it a wave THROUGH the cluster (without it the cluster
//     strobes in lockstep, a lamp rather than something breathing), and since #303 the RESTING emission
//     inside it follows the occlusion squared while the beat's swing rides through - see the helper's
//     own comment for why a flat resting glow was the thing keeping the pile unreadable. A style whose
//     identity is its glow may occlude linearly instead (the plasma and the lava do, each saying why),
//     but a style that skips the occlusion entirely re-breaks #303 on that style alone.
//  3. THE RIPPLE, IN BOTH OF ITS MEANINGS. RippleStrength gates it, and the SIGN of input.Ripple
//     chooses: positive is the landing wave (the ball's own colour carried RippleWhiten towards white),
//     negative is the ALARM (RippleAlarmColor, a flat colour the ball has no say in, because every ball
//     in that wave has to say the same thing). A technique that implements only the positive branch
//     loses the ceiling's alarm on that style alone and nothing anywhere reports it.
//  4. SurfaceOcclusion FROM input.OcclusionData - what the neighbours take.
//  5. ApplySeaSubmerge AND ApplyKillPlaneFade on the way out, in that order. The kill-plane fade is
//     read by the ball techniques alone and is how a ball below the line stops being drawn.
//  6. A CUE, IN OBJECT SPACE, THAT THE BALL IS ROLLING. The gores exist for this; the bubble replaces
//     them with object-space film marbling (its gravity drainage is deliberately WORLD space, because
//     gravity does not turn with the ball). A material whose whole figure is world- or view-space -
//     a mirror is the obvious trap - draws a spinning ball as a still one.
//
//Points 1-5 are mechanical and every one of them is already written below twice; point 6 is a design
//constraint on the LOOK and is the one that has to be answered before a style is worth building.
//===================================================================================================

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

//What a ball technique adds as its own light (#303 - #40's second half finally landing). The RESTING
//emission follows the occlusion, SQUARED - the bubble's measured correction (BubbleOcclusionPower)
//arriving on the opaque styles: "a light buried in the pile is exactly the one that should still show"
//was an argument about one ball seen alone, and a flat EmissiveStrength added to every ball of a pile
//equally was a floor under the whole cluster that no amount of AO could take it below - the single
//biggest reason burial did not read. What still punches through is the ANNOUNCEMENTS: the heartbeat's
//swing rides unoccluded, so the wave the beat carries through the cluster stays legible in the pile's
//interior, and the ripple and the ceiling's alarm are added after shading and are never dimmed at all.
//(The identity is the old lerp(1 - PulseDepth, 1, beat) split into its resting and swinging halves, so
//a surface ball at occlusion 1 emits exactly what it always did.)
float3 BallEmission(float3 primary, float3 worldPosition, float occlusion)
{
    float beat = Heartbeat(PulseTime * PulseSpeed - dot(worldPosition, PulseDirection) / max(PulseWavelength, 1e-4));

    return primary * EmissiveStrength * ((1 - PulseDepth) * occlusion * occlusion + PulseDepth * beat);
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

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    shaded.rgb += throughShell * TranslucencyStrength * DirLight0DiffuseColor * color * occlusion;

    //Emission: the ball radiates its own color rather than only reflecting what falls on it, and does it
    //on a heartbeat whose phase runs with world position - a wave through the cluster, not a strobe.
    //The resting part follows the occlusion since #303 and the beat's swing does not: this line said
    //"emission is not occluded" for a long time, on an argument BallEmission's own comment now answers.
    //
    //The ball glows with its own color, not with the pattern's: the gores and the polar discs are white,
    //and emitting through them made half of every ball radiate white light, which is both the wrong color
    //and the reason they read as washed out. What is alive here is the ball, not its paint job.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

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

//===================================================================================================
//POLISHED MARBLE (#305): the same sphere, the same instances, the same PatternVS, shaded as a piece of
//cut and polished stone. The heavy one - where the vinyl is an air-filled skin and the bubble is a film
//around nothing, this has MASS, and everything below is in service of that one impression.
//
//WHAT MAKES IT READ AS STONE, in the order the eye picks it up:
//  1. THE POLISH. The loudest cue and the cheapest: the vinyl's broad sheen is cut right back and a far
//     tighter lobe put in its place, over a raised reflection of the sky. Stone that has been polished
//     picks the sky up; the vinyl deliberately does not, and that difference alone separates them at a
//     distance where no vein is resolvable any more.
//  2. THE VEINING. Turbulence-warped bands in OBJECT space - the standard way marble is faked, on the
//     same octave sum the vinyl skin already uses for its moulding, so no new noise machinery. It is
//     also the whole of this style's answer to point 6 of the ball contract above: the veins turn with
//     the ball, and being aperiodic they read as rotation better than the gores do.
//  3. NO RELIEF AND NO SEAMS. Polished marble is smooth. There is no PerturbNormalFromHeight call in
//     here at all, which is why this technique is CHEAPER than the vinyl it stands beside rather than
//     dearer - the moulding, the welds and their self-shadow are the skin's signature, not stone's.
//
//THE VEIN IS THE BALL'S OWN COLOUR BRIGHTENED, NEVER A COLOUR OF ITS OWN, and that is the thirteen-colour
//constraint deciding a design question rather than a preference. A white vein over a magenta ball is a
//white ball with magenta between the veins - exactly the trap the beach ball's white gores set for Type4
//and Type11 (see BallType) - and over Type8, whose tint is a 0.045 grey, it would be the whole ball.
//
//WHAT IT IS BRIGHTENED TOWARDS IS THE PART THAT HAD TO BE MEASURED, and the first two answers were both
//wrong. Carrying the tint a fixed fraction TOWARDS WHITE reads beautifully on the dark types and is
//INVISIBLE on the bright saturated ones: photographed on Kepler, which carries all thirteen, the veins
//were a clear golden filigree on black and brown and could not be seen at all on cyan, red, magenta,
//green, yellow or orange. Lifting by a RATIO instead - the normalisation the ripple uses, and the obvious
//second guess - moved it barely at all, because a saturated tint is already near its own peak and the
//normalisation has nothing left to give. What the vein is competing against on a bright ball is not the
//body colour, it is the SHADING: a lit sphere runs from a blown highlight to a nearly black underside,
//and a figure that changes the colour by less than the light does across the same ball cannot be seen.
//
//So the vein is a SECOND MINERAL and not a brightening at all - a pale grey whose brightness follows the
//stone's own (calcite through a coloured marble, which is what the real thing is), with a FLOOR under it
//so a dark stone still shows its figure. It reads at every tint because it moves the colour along the one
//axis the shading does not: a vein is DESATURATED where the highlight is merely bright, and no amount of
//light on a cyan ball makes a part of it grey. The body keeps most of its hue inside the vein, so this is
//still not a white overlay - a magenta ball's veins are a greyed magenta, never white.
//===================================================================================================

//Wave count of the vein bands over the ball, before the turbulence bends them: the coarse spacing of the
//figure. Under about 3 the ball reads as two-tone rather than veined; far over it the bands crowd into a
//mottle that stops looking like stone and starts looking like noise.
float MarbleVeinFrequency;

//How far the turbulence bends those bands out of their parallel course, in the same units. This is the
//dial that separates MARBLE from a barber's pole: at zero the bands are perfect circles round one axis,
//and it is the warp alone that makes them wander, split and rejoin the way a mineral seam does.
float MarbleVeinWarp;

//How far a vein is carried from the stone's colour to the pale mineral running through it (0 = no figure
//at all, 1 = the mineral alone, with none of the stone's hue left in it). The one figure the C# side
//states and defends, because it is what the thirteen colours are spent on: see the header above.
float MarbleVeinContrast;

//What the mineral is: this multiple of the stone's own luminance, so a vein through a bright stone is
//bright and one through a dark stone is not, exactly as a real seam is lit by the same light as the rock
//around it. Over 1 so it is the LIGHTER of the two, which is what a vein reads as.
static const float MarbleVeinPale = 2.2;

//...but never below this, which is the whole of what makes the figure survive on the dark types. The
//8-ball's tint is a 0.045 grey: at twice its own luminance its veins would be a 0.1 grey on a 0.045 one,
//a difference nothing can see. The floor decouples the vein from the stone exactly where following it
//stops meaning anything.
static const float MarbleVeinFloor = 0.40;

//The axis the unwarped bands run around. Nothing about it is special - the warp is what the eye sees -
//but it is not aligned to an axis of the sphere either, so the bands do not agree with the mesh's own
//poles and give the LOD ladder's coarsest spheres nothing to line up with.
static const float3 MarbleVeinAxis = float3(0.42, 0.78, -0.46);

//How thin a vein is: the exponent on the band profile. Higher is thinner and harder-edged, and the
//profile is taken at the ZEROS of the band wave rather than its crests, so the veins come out as narrow
//lines through broad fields of stone rather than as broad stripes with narrow gaps.
static const float MarbleVeinSharpness = 2.0;

//===================================================================================================
//WHY THE VEINING WAS INVISIBLE, AND WHY THE ANSWER IS NOT MORE CONTRAST (#335). The owner read these
//balls as a flat tint. One warped sine puts ONE line through each of its zero crossings, and at this
//frequency a ball shows two or three of them on the hemisphere facing the camera - which is a stone
//with a couple of hairlines on it, not a figured marble.
//
//The obvious lever is the wrong one, and MARBLE_VEIN_CONTRAST's own note says why: over 0.6 the pale
//types run together, because carrying the vein further towards the mineral LIGHTENS THE WHOLE BALL and
//four of the thirteen are already pale. So the figure has to get louder without the ball getting
//lighter, and there are two ways to do that, both used here.
//
//MORE VEINS, at a second and finer scale on its own axis, which is what a real figured stone has: a few
//bold seams with a finer web between them, not a set of parallel bands.
//
//AND A DARK MARGIN BESIDE EACH PALE CORE. Real veining is not a light line on stone - it is a light
//line with a darker selvage where the mineral has stained the rock either side of it, and that pairing
//is most of what makes a seam read from across a room. It is also exactly what protects the palette:
//the margin takes back the value the core adds, so the ball's MEAN barely moves while the local step
//across a vein roughly doubles. Contrast where the eye reads it, not in the average.
//===================================================================================================

//How much wider the stained margin is than the pale core (as a divisor on the sharpness, so 1 is no
//margin at all), and how far it darkens the stone.
static const float MarbleVeinMargin = 2.4;
static const float MarbleVeinShade = 0.42;

//The finer web: its axis, how much faster it runs than the main seams, how much more it is warped, how
//much thinner its lines are, and how much of the figure it carries. Weaker than the main veins on
//purpose - it is the web BETWEEN them and not a second set of seams competing with the first.
static const float3 MarbleVeinAxisFine = float3(-0.58, 0.51, 0.63);
static const float MarbleFineRatio = 2.7;
static const float MarbleFineWarp = 1.6;
static const float MarbleFineSharpness = 1.7;
static const float MarbleFineWeight = 0.58;

//How much the same turbulence darkens the body between the veins. Small on purpose - it is there so the
//stone is not a flat wash of one colour, and anything more starts competing with the veins themselves.
//Free: it reuses the turbulence the warp already evaluated.
static const float MarbleMottle = 0.38;

//What the broad direct highlight is cut to, and how much more of the sky the surface mirrors than a
//renderer's own dial says. Both halves of "polished": the vinyl's wide soft sheen goes away, and what
//replaces it is a sharper reflection of the environment plus the pinpoint below.
static const float MarbleBroadHighlight = 0.3;
static const float MarbleEnvironment = 1.6;

//The polish itself: one tight lobe off the key light. Far tighter than the ball material's own exponent
//- that is a gloss coat's falloff and this is a stone that has been ground flat - and correspondingly
//brighter, because a pinpoint that small has to be intense to survive being that small.
static const float MarbleGloss = 190.0;
static const float MarbleGlossStrength = 0.9;

//Turbulence: the same four octaves the moulding uses, rectified and summed. abs() is what turns a sum of
//smooth waves into the creased, filament-like field a mineral figure needs - the standard construction,
//and the reason it is worth the four evaluations rather than one sine. Amplitudes sum to one, so the
//warp and the mottle above are both stated in units of the field itself.
float MarbleTurbulence(float3 direction, float footprint)
{
    return 0.50 * abs(ReliefOctave(direction, float3(0.71, 0.52, -0.47), 2.5, footprint))
        + 0.28 * abs(ReliefOctave(direction, float3(-0.36, 0.83, 0.42), 4.5, footprint))
        + 0.14 * abs(ReliefOctave(direction, float3(0.55, -0.44, 0.71), 8.0, footprint))
        + 0.08 * abs(ReliefOctave(direction, float3(-0.82, -0.31, 0.48), 14.0, footprint));
}

float4 MarblePS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1, and first, before anything else is worth computing. Branchless for the reason
    //PatternPS gives: Dissolve varies per instance and a branch on it would diverge within one draw call.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //The figure. The bands run round MarbleVeinAxis and the turbulence displaces the coordinate they are
    //read at, which is what bends them; warping the INPUT rather than adding to the output is what makes a
    //vein wander as one continuous line instead of breaking up into blotches.
    float turbulence = MarbleTurbulence(direction, footprint);
    float band = sin(dot(direction, MarbleVeinAxis) * MarbleVeinFrequency + turbulence * MarbleVeinWarp);

    //Thin lines at the band's zero crossings. Band-limited on the same form every octave in this file
    //uses: once a pixel spans half a wavelength the veins cannot be resolved and are faded out rather
    //than left to crawl, which on a cluster of thousands of small balls is the difference between stone
    //and a boiling speckle.
    float bandLimit = saturate(1 - footprint * MarbleVeinFrequency / 3.14159265);
    float core = pow(saturate(1 - abs(band)), MarbleVeinSharpness) * bandLimit;

    //The stained margin either side of it: the same profile at a lower exponent - so a wider band - with
    //the core taken back out, which leaves the selvage and nothing else. See the header above for why
    //this and not more contrast.
    float margin = saturate(pow(saturate(1 - abs(band)), MarbleVeinSharpness / MarbleVeinMargin) * bandLimit - core);

    //And the finer web, on its own axis and more heavily warped, so it crosses the main seams instead of
    //running with them. Its own frequency in its own band limit, which is the per-octave rule this file
    //keeps everywhere: it drops out first and takes nothing else down with it.
    float fineFrequency = MarbleVeinFrequency * MarbleFineRatio;
    float fineBand = sin(dot(direction, MarbleVeinAxisFine) * fineFrequency + turbulence * MarbleVeinWarp * MarbleFineWarp);

    float fine = pow(saturate(1 - abs(fineBand)), MarbleFineSharpness)
        * saturate(1 - footprint * fineFrequency / 3.14159265);

    float vein = saturate(core + fine * MarbleFineWeight);

    //Linearized before the blends, like the gores': these crossfades average light across a pixel.
    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The mineral in the seam: a pale grey following the stone's own luminance, floored so the dark types
    //keep their figure. Rec. 709 luminance, the same weights the film's transmission is measured with.
    float mineral = max(dot(primary, float3(0.2126, 0.7152, 0.0722)) * MarbleVeinPale, MarbleVeinFloor);

    float3 body = primary * (1 - MarbleMottle * turbulence) * (1 - MarbleVeinShade * margin);
    float3 color = lerp(body, lerp(primary, mineral, MarbleVeinContrast), vein);

    //Polished: the broad lobe cut back, the environment raised, and smoothness left at 1 so Fresnel still
    //rises to a full mirror along the silhouette - which on a sphere is most of what can be seen of it,
    //and is why a cluster of these picks up the dome so strongly.
    SurfaceSpecular surface;
    surface.Highlight = MarbleBroadHighlight;
    surface.Environment = MarbleEnvironment;
    surface.Smoothness = 1;

    //No relief and no cavity: the surface IS the sphere. Both of the shading's relief arguments are 1.
    float3 worldNormal = normalize(input.WorldNormal);
    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //The polish. One lobe, off the key light alone: the fill and back lights are what the material's own
    //broad highlight above still answers, and three pinpoints on one sphere read as three suns rather
    //than as a harder surface.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float3 halfway = normalize(towardsKey + normalize(EyePosition - input.WorldPosition));

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    shaded.rgb += DirLight0SpecularColor * MarbleGlossStrength
        * pow(saturate(dot(worldNormal, halfway)), MarbleGloss) * occlusion;

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through. In the ball's own colour and not the vein's, for the reason PatternPS gives
    //about its gores - what is alive here is the ball, not its figure.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //Contract point 3, in BOTH of its meanings, and the arithmetic is PatternPS's deliberately: the wave
    //has to look the same whatever the cluster is cut from, or a level tells the player something
    //different about a landing depending on what its balls are made of.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);
        float peak = max(primary.r, max(primary.g, primary.b));

        float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelMarble
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL MarblePS();
    }
};

//===================================================================================================
//WOUND WOOL (#311): a ball of yarn - one strand crossing itself over and over, wrapped in bands that
//lie at changing angles, fibrous and soft with no highlight worth the name and a fuzzy halo at the
//silhouette. THE ONLY SOFT MATERIAL among the ball styles, which is the whole of why it exists: the
//vinyl is an air-filled skin, the film a bubble around nothing and the marble a polished stone, and all
//three are HARD. This one changes what a cluster feels like rather than what it is made of.
//
//WHAT MAKES IT READ AS WOOL, in the order the eye picks it up:
//  1. THE WINDING, and specifically that the strand DIRECTION CHANGES ACROSS THE BALL. A single wrap
//     direction is a spool of thread, not a ball of yarn - the thing that says "wound by hand" is that
//     one region runs one way and its neighbour another, with the strands crossing where they meet.
//     Three winding axes, blended by a low-frequency mask that mostly picks one, does exactly that.
//  2. THE SOFTNESS OF THE LIGHT. Fibre scatters, so the terminator is soft and light carries past 90
//     degrees. A wrapped diffuse term over the key light is one expression and is most of the read.
//  3. THE FUZZ at the silhouette - loose fibres lit from behind. This is the Fresnel slot every other
//     style spends on a mirror, spent instead on a halo IN THE BALL'S OWN COLOUR. It is deliberately
//     not a sky reflection: two Fresnel sky terms on one sphere is what bleached the vinyl balls out
//     before ShadePixel took the job over, and a wool ball must not pick the dome up at all.
//  4. NO SPECULAR TO SPEAK OF. Resisting the highlight is most of the work of making this convincing,
//     and it is the one thing that will look wrong first if anyone "improves" it later.
//
//IT IS THE SAFEST STYLE ON THE THIRTEEN COLOURS, and unlike the marble that is not a claim that needed
//measuring - it falls out of the construction. The tint is dyed wool: a full-saturation diffuse body
//with no reflection diluting it, no transmission filtering it, no emission blowing it out and no
//backdrop entering it anywhere. What the map says the ball is, the ball is, under all eighteen domes.
//
//AND ITS FIGURE CANNOT FAIL THE WAY THE MARBLE'S NEARLY DID. The vein was a COLOUR change competing
//against the shading, which is why it went invisible on the bright saturated types and had to be
//rebuilt as a second mineral (see MarblePS's header). The strands are a NORMAL change - they ARE
//shading - so they read at every tint by construction, and on the darkest types too, where a colour
//figure has the least room.
//===================================================================================================

//How many strands are wound across the ball. Read as a wave count over the object-space direction, so
//the diameter shows about a third of this many crossings; under about 12 the strands read as fat tubes
//and over about 40 they cross into a felt that no longer has a strand in it.
float WoolStrandFrequency;

//Peak height of a strand's ridge in world units, exactly as PatternReliefStrength is for the moulding.
//It only tilts the normal, so the silhouette stays the sphere's - a wool ball with a lumpy outline
//would need geometry, and the LOD ladder's coarsest spheres have nothing to spare.
float WoolStrandDepth;

//How brightly the loose fibres at the silhouette catch the light, in the ball's own colour. The one
//figure the C# side states and defends: it is added light over every ball's rim at once, so a cluster
//is where it is judged and never a single ball.
float WoolHalo;

//The three axes the bands are wound around, and the low-frequency directions that choose between them.
//Neither set is aligned to the sphere's own poles or to each other, so the winding never agrees with
//the mesh and the coarsest LODs have nothing to line up with.
static const float3 WoolWindA = float3(0.36, 0.86, -0.36);
static const float3 WoolWindB = float3(-0.79, 0.24, 0.56);
static const float3 WoolWindC = float3(0.48, -0.31, 0.82);
static const float3 WoolPickA = float3(0.83, -0.42, 0.37);
static const float3 WoolPickB = float3(-0.28, 0.71, 0.65);
static const float3 WoolPickC = float3(0.55, 0.63, -0.55);

//How decisively the mask picks ONE winding rather than mixing all three. The whole design is here: at 0
//every region shows all three directions at once, which is a crosshatched net and not a wound ball; far
//too high and the regions get hard edges the yarn has no reason to have. Around 3 leaves broad patches
//running one way with the strands visibly crossing in the seams between them.
static const float WoolWindSelect = 5.0;

//Wave count of that mask. Low, deliberately: it sets how BIG a patch of parallel winding is, and a ball
//of yarn has three or four of them, not thirty.
static const float WoolWindPatchFrequency = 1.1;

//The fibre on top of the strand: two much finer octaves at a fraction of the depth, which is what makes
//the surface read as spun wool rather than as extruded plastic tubing. Their band-limit is ReliefOctave's
//own, so they fade out as the ball shrinks instead of boiling.
static const float WoolFibreFrequency = 47.0;
static const float WoolFibreDepth = 0.22;

//How far light is carried past the terminator (0 = ordinary Lambert). Fibrous materials scatter, so the
//lit side runs round further than a hard surface's and the shadow edge is soft rather than a line. This
//is the single biggest cue after the winding itself.
static const float WoolWrap = 0.35;

//...and how strongly that extra light is added. It is only ever the DIFFERENCE between the wrapped
//response and the Lambert one, so it cannot brighten the surface facing the light - it fills the
//terminator and the far side, which is exactly where a hard ball goes flatly black.
static const float WoolWrapStrength = 0.5;

//How tight the fuzz is at the silhouette. Much broader than a mirror's Fresnel, because loose fibre
//stands out from the surface over a wide band rather than turning at the very edge.
static const float WoolHaloPower = 2.2;

//What a strand's own crevice takes from the ambient. The valleys between wound strands are deep and
//narrow and see very little sky, and without this the ball reads as a smooth sphere with a pattern
//drawn on it however well the normals are tilted.
static const float WoolCrevice = 0.35;

//The specular a fibre surface is allowed: almost none, and rough with it. Wool has no gloss, and every
//attempt to give it "just a little" is what turns it back into plastic.
static const float WoolHighlight = 0.12;
static const float WoolEnvironment = 0.2;
static const float WoolSmoothness = 0.15;

//The wound height field: three wrappings, one of them chosen per region, plus the fibre on top.
//Returns roughly [-1, 1], so the depth uniforms above are peak heights in world units.
float WoolHeight(float3 direction, float footprint)
{
    //Which winding this part of the ball is wrapped in. exp2 of a low-frequency wave is a cheap soft
    //maximum: it is smooth everywhere, never negative, and WoolWindSelect turns the mixing down without
    //ever producing an edge.
    float3 pick = exp2(WoolWindSelect * float3(
        ReliefOctave(direction, WoolPickA, WoolWindPatchFrequency, footprint),
        ReliefOctave(direction, WoolPickB, WoolWindPatchFrequency * 1.31, footprint),
        ReliefOctave(direction, WoolPickC, WoolWindPatchFrequency * 1.73, footprint)));

    pick /= (pick.x + pick.y + pick.z);

    float strands = pick.x * ReliefOctave(direction, WoolWindA, WoolStrandFrequency, footprint)
        + pick.y * ReliefOctave(direction, WoolWindB, WoolStrandFrequency * 1.09, footprint)
        + pick.z * ReliefOctave(direction, WoolWindC, WoolStrandFrequency * 0.94, footprint);

    float fibre = 0.6 * ReliefOctave(direction, WoolPickB, WoolFibreFrequency, footprint)
        + 0.4 * ReliefOctave(direction, WoolPickC, WoolFibreFrequency * 1.63, footprint);

    return strands + fibre * WoolFibreDepth;
}

float4 WoolPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1, first, and branchless for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Contract point 6 lives here and costs nothing extra: the winding is evaluated in OBJECT space, so
    //it turns with the ball, and a ball of yarn rolling is about the most legible rotation there is
    //because the strand direction visibly sweeps across the surface.
    float profile = WoolHeight(direction, footprint);
    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, profile * WoolStrandDepth);

    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //Dyed wool: the tint is the body, undiluted. The only thing that touches it is the crevice between
    //strands, which is a depth and not a colour, so no hue is lost at any tint.
    float crevice = 1 - WoolCrevice * saturate(-profile);
    float3 color = primary * crevice;

    //Matte and rough. The specular ambient in particular is nearly off: a wool ball that picks up the
    //dome is a wool ball made of plastic.
    SurfaceSpecular surface;
    surface.Highlight = WoolHighlight;
    surface.Environment = WoolEnvironment;
    surface.Smoothness = WoolSmoothness;

    //The crevice is handed in as the CAVITY too, which is what it physically is - a valley between two
    //strands sees a slice of sky and not the whole hemisphere.
    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, crevice, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Scattering: light carried past the terminator. Only ever the DIFFERENCE between the wrapped
    //response and the Lambert one ShadePixel already applied, so this cannot brighten the side facing
    //the light - it fills the soft band around the shadow edge and the far side, where a hard ball goes
    //flatly black and a fibrous one does not.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float ndl = dot(worldNormal, towardsKey);
    float scattered = saturate((ndl + WoolWrap) / (1 + WoolWrap)) - saturate(ndl);

    shaded.rgb += scattered * WoolWrapStrength * DirLight0DiffuseColor * color * occlusion;

    //The fuzz: loose fibres standing out from the silhouette, in the BALL'S OWN COLOUR and never the
    //sky's. Occluded, so a ball buried in the pile does not put a halo on its neighbours.
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float fuzz = pow(1 - saturate(dot(worldNormal, eyeVector)), WoolHaloPower);

    shaded.rgb += fuzz * WoolHalo * primary * occlusion;

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately: a landing has to look
    //the same whatever the cluster is made of.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);
        float peak = max(primary.r, max(primary.g, primary.b));

        float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelWool
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL WoolPS();
    }
};

//===================================================================================================
//ANODISED METAL (#306): thirteen alloys, not thirteen mirrors. #272 proposed chrome and named its own
//fatal objection in the same breath - a near-mirror dilutes its own tint by construction - and that
//objection is correct AS STATED. A mirror has no albedo, so thirteen chrome balls are thirteen balls
//the same colour, and the game cannot afford that.
//
//BUT IT IS ONLY TRUE OF A WHITE METAL. A metal's colour lives in what it does to the light it
//REFLECTS: its reflectance at normal incidence IS its colour, which is why gold reflects gold. So the
//tint becomes F0 and there is NO DIFFUSE TERM AT ALL - every photon leaving this surface bounced off
//it. Gold, copper, brass, oxidised titanium, gunmetal, thirteen of them, each mirroring the same dome
//in its own colour.
//
//THE SKY IS THE ALBEDO NOW, WHICH IS THE SAME FAULT THE FILM HAS BY ANOTHER ROAD, and it takes the
//same departure from physics. A red metal under a blue sky is tint x sky = nearly black; #258 found
//exactly this for a red film over the meadow and its answer applies unchanged - take the environment
//as a BRIGHTNESS and not as a colour (Rec. 709 luminance, then multiplied by the alloy). What is NOT
//given up is the grazing rim: Fresnel rises to a full mirror there whatever the metal, so the sky's
//real colour comes back at the silhouette, where it is physically right and where it cannot be
//mistaken for the ball's own hue. The blend between the two IS Schlick's own shape, so nothing is
//bolted on: the body is tinted-by-luminance, the rim is the honest mirror.
//
//THE ROTATION CUE IS THE REAL DESIGN PROBLEM OF THIS STYLE and it is not optional (contract point 6). A
//perfect mirror sphere spinning looks IDENTICAL frame to frame - the reflection is view- and
//world-dependent and nothing on the surface turns. So the metal is BRUSHED: fine parallel ridges in
//OBJECT space, which give the highlight something to travel over as the ball turns and read as turned
//metal rather than as a chrome bead. It is also a figure in the NORMAL rather than in the colour,
//which is the lesson #305 and #311 paid for together - see WoolPS's header.
//===================================================================================================

//Wave count of the brush grain over the ball. High: a brushed finish is many fine parallel lines, and
//the band-limit inside ReliefOctave fades them out honestly as the ball shrinks.
float MetalBrushFrequency;

//Peak height of a brush ridge in world units. Very small - this is a polish direction, not a corrugation,
//and anything deep enough to see as ridges stops being brushed metal and becomes a screw thread.
float MetalBrushDepth;

//How much of the environment the surface mirrors. The one figure the C# side states, because it decides
//how bright a cluster of these is against its backdrop and there is no diffuse term underneath to carry
//the ball if it is set too low.
float MetalReflectance;

//The brush direction. The wave runs along it, so the RIDGES run ACROSS it - fine parallel lines, which is
//what brushing leaves - and every octave of the grain shares it (see MetalPS).
static const float3 MetalBrushA = float3(0.31, 0.88, 0.36);

//And a direction PERPENDICULAR to it, which is a correction and not a tuning (#336). This used to be
//(0.27, 0.91, 0.31) - a few degrees off MetalBrushA - carrying a second octave at 1.73x the frequency.
//TWO NEAR-PARALLEL WAVES OF DIFFERENT FREQUENCY BEAT, and a beat is a row of blobs: that is exactly the
//"small bumps on the ball" the owner reported, and no change of frequency could have fixed it, because
//the fault was the interference and not the scale. Perpendicular, this wave does the one job a second
//direction is good for here - varying how deep the scratches bite ALONG their length.
static const float3 MetalBrushB = float3(0.943, -0.332, 0.0);

//How far the scratch depth swings along a scratch's length, and how quickly. Low frequency on purpose:
//what this expresses is that a brush is not a comb - it bites hard in places and skips in others, so a
//line fades in, runs and dies instead of circling the ball at one depth.
static const float MetalScratchVariation = 0.55;
static const float MetalScratchLengthFrequency = 5.0;

//===================================================================================================
//THE ENVIRONMENT A METAL NEEDS, AND WHY SkyRadiance IS NOT IT (#336). That function is a LINEAR RAMP
//from the ground colour to the sky colour across the whole 180 degrees. That is exactly right for an
//AMBIENT term - it is what a diffuse surface integrates - and it is the reason this style read as a
//flat, softly lit sphere with no gloss in it: a smooth sphere reflecting a smooth gradient has nothing
//anywhere on it that is sharp.
//
//A polished surface does not INTEGRATE its environment, it SHOWS it, and an outdoor scene has exactly
//two hard things in it: the HORIZON, which is an edge and not a ramp, and the SUN, which is a small
//object thousands of times brighter than the sky around it. Neither exists in a gradient, so no amount
//of reflectance was ever going to make one appear - which is why "make it glossier" is answered here
//rather than at MetalReflectance.
//
//It is local to this style DELIBERATELY. SkyRadiance is the ambient every other surface in the game
//integrates, and sharpening it there would draw a horizon line across the ground, the island, the
//cannon and all nine other ball styles.
//===================================================================================================

//How sharp the horizon is in the reflection, in the direction's own y. Not zero: a real horizon at this
//distance is a couple of degrees of haze, and a step function on a curved mirror aliases - and dead
//sharp it stops reading as a reflection and starts reading as a two-tone paint job.
static const float MetalHorizonEdge = 0.075;

//And how far the ground half is taken down. GroundColor is an AMBIENT BOUNCE colour - what the ground
//sends back up into a diffuse integral - which is a good deal brighter than the ground's own face, so
//used raw as a reflection it put the LOWER half of every ball brighter than the sky above it. That is
//upside down: outdoors the sky is the light source and the ground is what it falls on, and a mirror ball
//with a bright bottom and a dark top reads as anything but metal.
static const float MetalGroundDarkening = 0.5;

//The sun in the reflection: a tight core inside a broader lobe, which is what a reflected sun looks like
//on a surface that is polished but not a mirror - and is where most of this style's gloss now comes from.
//The gain is against the key light's own specular colour, so a dusk dome reflects a dusk sun.
static const float MetalSunSharpness = 900.0;
static const float MetalSunHaloSharpness = 45.0;
static const float MetalSunHaloWeight = 0.12;
static const float MetalSunGain = 9.0;

float3 MetalSky(float3 direction)
{
    float3 sky = lerp(GroundColor * MetalGroundDarkening, SkyColor,
        smoothstep(-MetalHorizonEdge, MetalHorizonEdge, direction.y));

    float towards = saturate(dot(direction, SunDirection));
    float disc = pow(towards, MetalSunSharpness) + MetalSunHaloWeight * pow(towards, MetalSunHaloSharpness);

    return sky + DirLight0SpecularColor * disc * MetalSunGain;
}

//How bright the direct lights' highlight is on the metal, tinted by the alloy like everything else it
//reflects. Under 1 because the environment is the main event here and a metal that answers the three-light
//rig as strongly as it answers the sky reads as a plastic ball with a lot of gloss.
static const float MetalHighlight = 0.6;

//How far down the alloy's own hue F0 is allowed to go. WITHOUT THIS THE BLACK BALL IS NOT A BALL: Type8's
//tint is a 0.045 grey, and a mirror that reflects 4.5% of a dim sky is a hole in the picture. The floor is
//taken along the tint's OWN HUE at full brightness, so it lifts the dark types into a gunmetal without
//turning any of the coloured ones grey.
static const float MetalF0Floor = 0.16;

//How hard the reflection is crowded into the silhouette on its way from tinted body to honest mirror.
//Schlick's own exponent - this is the standard curve and not a tuned one.
static const float MetalGrazingPower = 5.0;

float4 MetalPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Contract point 6: the brush, in object space, so it turns with the ball.
    //
    //FOUR OCTAVES ALONG ONE DIRECTION, which is what makes them LINES. Collinear sines sum into a
    //one-dimensional profile, and a one-dimensional profile on a surface is a set of parallel grooves of
    //unequal depth and spacing - a wire brush's own signature. Put the octaves on DIFFERENT directions,
    //as this did, and they interfere into blobs instead (see MetalBrushB). The ratios are irrational-ish
    //so the profile never repeats, and each octave band-limits itself, so the finest simply drop out as
    //the ball recedes and the coarse streak direction is what survives - which was always the intent.
    float f = MetalBrushFrequency;

    float grain = 0.36 * ReliefOctave(direction, MetalBrushA, f, footprint)
        + 0.28 * ReliefOctave(direction, MetalBrushA, f * 1.61, footprint)
        + 0.21 * ReliefOctave(direction, MetalBrushA, f * 2.63, footprint)
        + 0.15 * ReliefOctave(direction, MetalBrushA, f * 4.27, footprint);

    //How hard the brush bit, varying ACROSS the scratches so it varies along their length.
    grain *= lerp(1 - MetalScratchVariation, 1,
        0.5 + 0.5 * ReliefOctave(direction, MetalBrushB, MetalScratchLengthFrequency, footprint));

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, grain * MetalBrushDepth);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The alloy: the tint as reflectance at normal incidence, floored along its own hue so the dark types
    //are gunmetal rather than holes. See MetalF0Floor.
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 f0 = max(primary, primary / max(peak, 1e-3) * MetalF0Floor);

    //The three-light rig and the scene's own point lights, accumulated exactly as ShadePixel does it so a
    //campfire or the city's neon lights a metal ball the way it lights everything else. Only the SPECULAR
    //half is kept: a metal has no diffuse, which is the single biggest cue that it is one.
    float3 diffuse = 0;
    float3 specular = 0;

    AddLight(normalize(KeyLightPosition - input.WorldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, worldNormal, eyeVector, diffuse, specular);

    specular *= CloudSunlight(input.WorldPosition, SunDirection);

    AddLight(-DirLight1Direction, DirLight1DiffuseColor, DirLight1SpecularColor, worldNormal, eyeVector, diffuse, specular);
    AddLight(-DirLight2Direction, DirLight2DiffuseColor, DirLight2SpecularColor, worldNormal, eyeVector, diffuse, specular);

    specular *= DirLightStrength;

    AddSceneLights(input.WorldPosition, worldNormal, eyeVector, diffuse, specular);

    //The environment, along the mirror direction. Tinted by LUMINANCE at the body and left honest at the
    //rim, blended on Schlick's own curve - see the header for why that is not a compromise but the whole
    //design.
    float3 environment = MetalSky(reflect(-eyeVector, worldNormal));
    float grazing = pow(1 - saturate(dot(worldNormal, eyeVector)), MetalGrazingPower);

    float3 reflection = lerp(f0 * dot(environment, float3(0.2126, 0.7152, 0.0722)), environment, grazing);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    float4 shaded = float4((reflection * MetalReflectance + specular * f0 * MetalHighlight) * occlusion, 1);

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //Contract point 3, both meanings, PatternPS's arithmetic.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelMetal
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL MetalPS();
    }
};

//===================================================================================================
//FROSTED ICE (#307): a ball of cloudy frozen water. Light goes IN, scatters a short way and comes back
//out, so the colour arrives from inside the ball rather than off its face; a network of internal cracks
//catches the light in bright threads, and a cool rim sits on the silhouette.
//
//IT IS OPAQUE, AND THAT IS THE ONE DECISION THAT MATTERS HERE. #272 framed this as "a cold cousin of
//the glass bubble", and building it that way would have been the expensive mistake: a second
//transparent style costs the whole two-pass opposite-cull machinery in BallRenderSet.Draw, doubles the
//ball pass, and re-opens every argument BUBBLE_BODY_OPACITY settled about what a film hides. It would
//also stand next to the bubble needing to be told apart from it at a glance, or one of the two is
//redundant.
//
//And it is not even the right physics. FROSTED ice is not CLEAR ice: frosting IS short-range subsurface
//scattering, which is an opaque phenomenon - you cannot see a level through a frosted marble. So the
//body is lit as a diffuse solid with a real translucency term through it, and the result is a genuinely
//different MATERIAL from the film rather than a recolour of it: one is a hollow thing you see the level
//through, the other is a solid you cannot. Two styles, two reads, one ball pass each.
//
//THE FIGURE IS BUILT IN THE NORMAL FIRST AND IN THE COLOUR SECOND, which is #305 and #311's shared
//lesson applied deliberately (see WoolPS's header): a crack cuts a groove that catches the light, and
//only then adds a bright thread along itself. The groove reads at every tint because it is shading; the
//thread is what makes it ICE rather than a scratch.
//
//THE NET IS A FRACTURE AND NOT A LATTICE, and that is #337's whole correction. Until it, the cracks were
//three SeamLine fields crossing: three sets of straight, evenly spaced bands around great circles. Three
//regular line families over a surface is the construction a quilted hide is stitched on, and the owner's
//playtest read the ball as exactly that - padded upholstery, not ice. NO WIDTH AND NO FREQUENCY COULD
//HAVE FIXED IT, because what was wrong was the REGULARITY and not the size of the figure; a finer
//quilting is still quilting. Ice does not crack in a lattice. It breaks into irregular polygonal plates
//that meet three at a point, along edges of unequal length that open, taper and die out - which is a
//Voronoi diagram, so the net is one (VoronoiEdgeCell3, over the object-space direction, seamless on a
//sphere where any azimuth/elevation lookup would pinch at the poles and tear at the branch cut).
//
//AND AT PLAY DISTANCE IT IS THE PLATES THAT CARRY THE READ, NOT THE CRACKS. A hairline net is a
//high-frequency figure and it is the first thing to go sub-pixel - the lesson #326 paid for on the bomb,
//in the same file. So each plate also takes its own value from the cell's id: a figure the size of a
//PLATE, which is still resolvable long after the thread between two of them is not. Where the net has
//faded, the ball is a thing broken into facets of frozen water rather than the smooth pale sphere that
//kept being mistaken for the porcelain (#339).
//
//A per-plate NORMAL TILT was tried first and dropped, and the reason is worth keeping: a piecewise
//constant field has no gradient inside a plate and an infinite one at its border, so pushing it through
//PerturbNormalFromHeight - which differentiates - puts a one-pixel spike along every crack, which is the
//hard-checkerboard trap this file warns about two hundred lines up. The value figure below carries the
//same read with no derivative in it.
//===================================================================================================

//How many fracture cells the ball is broken into - the plate size. Cells sit one unit apart in the
//field's own space and the ball is a unit sphere scaled by this, so the count goes as its AREA: about
//4*pi*f^2 over the whole ball, a dozen or so on the hemisphere facing the camera. Reading that as a great
//circle (2*pi*f) is what put fifty pebbles on a ball in this change's first pass. Far over it the ball is
//shattered gravel, far under it two or three continents. Named "crack frequency" still, because that is
//what the C# side has always called it.
float IceCrackFrequency;

//How wide a crack opens, as a fraction of a cell. This is the MEAN and not the width: every stretch of
//crack multiplies it by its own wander (IceCrackOpenMin..IceCrackOpenMax), because a net drawn at one
//width all over a ball is a drawn net - which is what the seam lines were, and half of why they read as
//stitching rather than as fracture.
float IceCrackWidth;

//How strongly each plate differs from its neighbours: the low-frequency half of the figure, and the half
//that survives to play distance. Stated by the C# side rather than fixed here because it is the same kind
//of decision MARBLE_VEIN_CONTRAST is - how loud the figure is against thirteen tints that must stay apart.
float IcePlateContrast;

//How brightly the silhouette goes cool and pale - the cold, and the one figure the C# side states,
//because at full strength it eats the tint on the rim of every ball at once and a cluster is mostly rims.
float IceRim;

//The bend put through the fracture before the cells are read, and the three wave directions it is built
//from. A Voronoi's borders are dead straight, and a ball tiled in straight-edged polygons reads as a
//geodesic DOME - a made object - rather than as something broken; bowing the domain a little breaks that
//without touching what makes the net irregular, which is the cells themselves and not their edges.
static const float IceFractureBend = 0.09;
static const float IceFractureBendFrequency = 3.4;
static const float3 IceWarpA = float3(0.77, 0.41, -0.49);
static const float3 IceWarpB = float3(-0.33, 0.86, 0.39);
static const float3 IceWarpC = float3(0.52, -0.38, 0.77);

//How far a stretch of crack opens or closes against IceCrackWidth, and how quickly it changes along its
//length. The floor is well under one: a crack that thins to a fifth of the mean is a hairline that
//disappears, which is where the dead ends and the branch stubs come from - a fracture stops, and a
//stitched seam never does.
static const float IceCrackOpenMin = 0.18;
static const float IceCrackOpenMax = 1.55;
static const float IceCrackOpenFrequency = 7.0;
static const float3 IceCrackOpenAxis = float3(0.44, -0.72, 0.53);

//THE PLATE FIGURE IS TWO TERMS BECAUSE ONE OF THEM ALWAYS HAS A BLIND TINT, which is #305 and #311's
//lesson arriving here in its own shape. A plate that is only ever LIGHTENED is invisible on white, where
//the tonemap's shoulder eats the difference; a plate that is only ever DARKENED is invisible on the
//8-ball, whose tint is a 0.045 grey with nothing under it. So a plate does both, from opposite ends:
//IcePlateShade takes value OUT of the body for the plates whose id is low, and IcePlateContrast (which
//the C# side states) adds cool light back for the ones whose id is high. Every tint is on one of those
//two axes and the pale ones are on both.
static const float IcePlateShade = 0.16;

//How much white rides in the cool light a plate adds, so a dark tint gets a plate figure at all - the
//rim below carries the same 0.3-ish term for the same reason, and it is the only thing that separates
//two plates of an 8-ball from each other.
static const float IcePlateWhite = 0.35;

//Where the two halves of the figure stop being worth drawing, each measured against ITS OWN size - which
//is the whole point, and is #326's lesson stated the other way round. There the bomb's glow was faded on
//the finest thing in the pattern and the coarse read went down with it; here the two halves are different
//sizes by a factor of ten and are faded separately.
//
//The CRACK is faded against its own WIDTH, not against the cell it runs round: a border is a line of
//width/frequency in direction units, so footprint * frequency / width is that line measured in pixels (in
//pairs of them, since footprint is ddx plus ddy). It is faded against the WANDERED width rather than the
//mean, so the hairline stretches drop out first and the open ones hold - which is how a fracture recedes
//anyway. At the shipped figures this crosses over at about play distance, and that is not a defect but
//the design: what carries the ball there is the plate.
//
//The 1.5 IS THE POINT OF THAT CONSTANT and not a fudge: it holds the crack at full strength until the
//line is down to about a pixel across and only then fades it over the last octave. Written as a plain
//ramp from zero footprint - which is what every other limit in this file is, because every other one
//rides a whole WAVELENGTH - the crack came out 43 % faded on a ball ninety-nine pixels wide, where the
//line was still four and a half pixels across and perfectly sharp. A limit on a thin line has to start
//late; a limit on a wave can start at once.
//
//The PLATE is faded against the cell, and at the shipped frequency it never engages except on the
//smallest LOD - a plate is most of a ball, and a ball is off the screen before one is sub-pixel.
static const float IceCrackBandLimit = 1.5;
static const float IcePlateBandLimit = 0.5;

//How deep a crack cuts, in world units, and how brightly it glows along its length. The groove is what
//makes it read at every tint; the glow is what makes it read as ICE. The glow follows the stone's own
//lighting rather than being a fixed white, on the same argument MarbleVeinPale carries.
static const float IceCrackDepth = 0.012;
static const float IceCrackGlow = 0.55;

//The cool cast of both the rim and the crack glow. Not pure white: ice is blue because water absorbs red
//over a path length, and this is the one place that fact is worth stating rather than deriving.
static const float3 IceCold = float3(0.72, 0.88, 1.0);

//How sharply the rim crowds into the silhouette. Broader than a mirror's, because what is happening there
//is a long path through scattering ice and not a reflection off a face.
static const float IceRimPower = 2.6;

//Frost's own texture under the cracks: fine grain that keeps the surface from reading as polished, at a
//small fraction of a crack's depth.
//
//FOUR WAVES ALONG MIXED DIRECTIONS, and it was ONE until #337 - along a single axis, which is not grain
//but STRIPES. The dense crack net used to hide them; once the plates were large and smooth the hatching
//came straight out, in the same shot and for the same reason as the quilting it was drawn on top of. The
//ratios are irrational-ish so the four never settle into a weave, which is the trap the scene surfaces'
//own relief records (see SurfaceReliefWorld's header, and why it uses seven).
static const float IceFrostFrequency = 38.0;
static const float IceFrostDepth = 0.2;
static const float3 IceFrostA = float3(0.61, 0.55, -0.57);
static const float3 IceFrostB = float3(-0.48, 0.71, 0.51);
static const float3 IceFrostC = float3(0.39, -0.62, 0.68);
static const float3 IceFrostD = float3(-0.74, -0.36, 0.57);
static const float4 IceFrostRatio = float4(1.0, 1.37, 0.79, 1.91);

//What ice does with a highlight: present, but soft and wide. A frosted surface is not a mirror and not a
//matte one either - it is a mirror seen through a millimetre of scattering, which is exactly a broad lobe.
static const float IceHighlight = 0.55;
static const float IceEnvironment = 0.45;
static const float IceSmoothness = 0.5;

//ONE SEAM LINE: a narrow band either side of a wave's zero crossing, faded out on its own band-limit.
//Shared by the ice's cracks (#307) and the lava's plate seams (#310), which want the same LINE and
//opposite things from it - the ice brightens along it because a crack is an internal face catching the
//light, the lava glows through it because there is molten rock behind it, and both cut a groove.
//
//Written against the RAW SINE rather than through ReliefOctave DELIBERATELY - ReliefOctave fades its
//AMPLITUDE towards zero as the pixel grows, and a test on abs(v) < width would then read the whole ball
//as one seam the moment the wave stopped being resolvable. Fading the LINE is what is wanted.
float SeamLine(float3 direction, float3 waveDirection, float frequency, float width, float footprint)
{
    float v = sin(dot(direction, waveDirection) * frequency);
    float fade = saturate(1 - footprint * frequency / 3.14159265);

    return (1 - smoothstep(0, width, abs(v))) * fade;
}

//THE FRACTURE, in one call: the crack running between the plates and the plate the pixel is standing on.
//Returns (crack, this plate's own value in 0..1, how much of the plate figure the pixels can still carry),
//the crack already faded on its own terms and the plate handed over with its limit for the caller to
//fade towards ITS no-op, which is not zero but a half - a plate that cannot be resolved has to converge
//on the average of all of them, or the ball's whole value walks as it recedes.
//
//The bend goes through ReliefOctave and not a raw sine deliberately: a warp that cannot be resolved has
//to stop moving, or the edges crawl on the small distant balls exactly where the net should be settling
//down. It is the one place in this style where fading the field's AMPLITUDE is right - what fades then is
//the bend, and a straight crack is a perfectly good crack, where a faded SeamLine would have turned the
//whole ball into one seam (the trap that comment records two functions up).
float3 IceFracture(float3 direction, float footprint)
{
    float3 bend = float3(
        ReliefOctave(direction, IceWarpA, IceFractureBendFrequency, footprint),
        ReliefOctave(direction, IceWarpB, IceFractureBendFrequency * 1.31, footprint),
        ReliefOctave(direction, IceWarpC, IceFractureBendFrequency * 0.83, footprint));

    float2 fracture = VoronoiEdgeCell3((direction + bend * IceFractureBend) * IceCrackFrequency);

    //A pixel measured in cells - the unit both band limits below are written in, and the reason neither
    //has to be told how big a ball is or how far away it stands.
    float cells = footprint * IceCrackFrequency;

    //How far this stretch of the crack has opened. Wandering along the crack rather than across it, so
    //one edge goes from a gap to a hairline and back over its own length instead of the whole net
    //breathing together.
    float wander = 0.5 + 0.5 * ReliefOctave(direction, IceCrackOpenAxis, IceCrackOpenFrequency, footprint);
    float width = IceCrackWidth * lerp(IceCrackOpenMin, IceCrackOpenMax, wander);

    float crack = (1 - smoothstep(0, width, fracture.x))
        * saturate(IceCrackBandLimit - cells / max(width, 1e-3));

    float plateLimit = saturate(1 - cells * IcePlateBandLimit);

    return float3(crack, lerp(0.5, fracture.y, plateLimit), plateLimit);
}

float4 IcePS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //The fracture, in OBJECT space (contract point 6): the plates and the cracks between them are INSIDE
    //the ice and they turn with it.
    float3 fracture = IceFracture(direction, footprint);
    float cracks = fracture.x;
    float plate = fracture.y;

    //Frost grain under them, so the surface is not polished between the cracks. Summed and not multiplied,
    //on the moulded vinyl's own lesson two hundred lines up: multiplying sines lays down a crosshatch.
    float frost = (ReliefOctave(direction, IceFrostA, IceFrostFrequency * IceFrostRatio.x, footprint)
        + ReliefOctave(direction, IceFrostB, IceFrostFrequency * IceFrostRatio.y, footprint)
        + ReliefOctave(direction, IceFrostC, IceFrostFrequency * IceFrostRatio.z, footprint)
        + ReliefOctave(direction, IceFrostD, IceFrostFrequency * IceFrostRatio.w, footprint))
        * (IceFrostDepth * 0.25);

    //The crack is a GROOVE first. Normal-space figure, so it reads at every tint - the lesson #305 and
    //#311 paid for between them.
    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition,
        (frost - cracks) * IceCrackDepth);

    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The plate's darkening half. Taken out of the BODY rather than off the finished pixel so it darkens
    //what the ball is made of and not the sky sitting on it - a plate is a thickness of ice with more
    //frozen water in front of it, not a shadow.
    float3 body = primary * (1 - IcePlateShade * (1 - plate));

    //A frosted surface is a mirror seen through a millimetre of scattering: the highlight is present but
    //broad, and the environment is picked up softly rather than sharply.
    SurfaceSpecular surface;
    surface.Highlight = IceHighlight;
    surface.Environment = IceEnvironment;
    surface.Smoothness = IceSmoothness;

    //The cracks take ambient like any other crevice - a plane inside the ice sees very little sky.
    float cavity = 1 - 0.5 * cracks;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(body, 1), 1, cavity, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //THE STYLE'S WHOLE READ, and the term a screenshot from the sun's own side cannot show: light that
    //went in, scattered and came back out. A ball with the key behind it glows through in its own colour
    //instead of going flatly black, which is what says "solid but not opaque to light". Same shape as the
    //vinyl skin's translucency and far stronger, which is the difference between a skin and a solid.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float through = pow(saturate(dot(-worldNormal, towardsKey)), 2);

    shaded.rgb += through * TranslucencyStrength * DirLight0DiffuseColor * primary * occlusion;

    //The cracks catch the light along their length: internal faces, so they BRIGHTEN where the vinyl's
    //welds darken. Cool-cast and following the ball's own colour rather than a fixed white, on the
    //argument MarbleVeinPale carries.
    shaded.rgb += cracks * IceCrackGlow * IceCold * primary * occlusion;

    //The plate's brightening half, and the half that carries the style at play distance: it is a figure
    //the size of a PLATE, so it is still there when the thread across one has gone sub-pixel. Cool and
    //part white for the reason the rim below is - a plate of an 8-ball has to differ from the plate next
    //to it, and nothing scaled by that tint can.
    shaded.rgb += plate * IcePlateContrast * IceCold * lerp(primary, 1.0, IcePlateWhite) * occlusion;

    //And the cold rim: a long path through scattering ice at the silhouette. Occluded, so a ball buried
    //in the pile does not outline itself.
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float rim = pow(1 - saturate(dot(worldNormal, eyeVector)), IceRimPower);

    shaded.rgb += rim * IceRim * IceCold * lerp(primary, 1.0, 0.3) * occlusion;

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //Contract point 3, both meanings, PatternPS's arithmetic.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);
        float peak = max(primary.r, max(primary.g, primary.b));

        float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelIce
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL IcePS();
    }
};

//===================================================================================================
//KEEPING THIRTEEN TINTS APART IN A STYLE THAT DOES NOT SHADE THEM DIRECTLY (#315). Two helpers shared
//by the gem, the plasma and the lava, which are the three styles measured tighter on some pair than the
//MOULDED VINYL is on its own worst one - the line every style has to clear, because the vinyl is what
//the thirteen tints were tuned against.
//
//THE FAULT IS THE PEAK NORMALISATION, AND IT IS THE SAME LINE IN BOTH EMISSIVE STYLES. Both the plasma
//and the lava take the tint NORMALISED to its peak channel, which is what makes the 8-ball glow white
//instead of not at all, and both headers are right that it is necessary. What neither saw is that it
//also throws away the VALUE axis - and the thirteen are separated on value quite as much as on hue, so
//every pair sharing a hue family arrives as one ball:
//
//    black  (0.045,0.045,0.05) / 0.05 -> (0.90,0.90,1.00) | silver (0.50,0.53,0.58) / 0.58 -> (0.86,0.91,1.00)
//    orange (1.00,0.50,0.03)  / 1.00 -> (1.00,0.50,0.03)  | brown  (0.42,0.24,0.11) / 0.42 -> (1.00,0.57,0.26)
//
//Measured whole-disc CIEDE2000 on Thirteen_Colors, each style under its own chapter's scene and dome:
//lava orange/brown 2.6 and black/silver 4.2, plasma black/silver 4.7, gem black/brown 4.9 and
//black/silver 4.5 - against a vinyl control whose OWN tightest pair is 7.9 on the same cluster. All
//five are now 8.0 or better.
//
//TintEmission is the answer to it: leave the normalisation alone - it decides the HUE, and both styles
//are right about it - and scale the emission's BRIGHTNESS by the tint's own luminance. Black keeps its
//white-hot discharge and burns dimmer than silver's; brown keeps its orange seam and burns dimmer than
//orange's. Compressed (a square root) and floored, because the tints span better than a hundred to one
//in linear luminance and a proportional map would leave the 8-ball with nothing to be seen by - which is
//the failure the normalisation was introduced to fix in the first place. The floor is the guarantee that
//it is still there.
//
//A SCALAR over the whole emission and not a factor on the hue, deliberately: the lava carries a seam's
//hottest core towards LavaIncandescent, a fixed near-white, so anything applied to the hue alone leaves
//those cores burning identically on all thirteen - and a narrow bright core is a large share of what the
//eye integrates over a ball at play size.
//
//SaturateTint is the gem's, and it is a different fault needing a different cure. Nothing there
//normalises anything: the stone drowns in an UNCOLOURED environment mirror (GemEnvironment 1.3 at full
//smoothness), so a tint with little chroma of its own barely reaches the pixel and black, brown and
//silver all arrive as the same grey pebble. Lifting their brightness would only make them MORE alike, so
//this lifts their CHROMA at constant luminance - the tint pushed out towards its own fully saturated
//form with its Rec. 709 luminance held exactly where it was. On a tint that is already neutral it is the
//identity by construction, and that is the property the gem needs: neutrals cannot be told apart by hue,
//and inflating them is precisely what would push them together.
//===================================================================================================

//The darkest tint's share of the brightest one's emission. Below about 0.15 the 8-ball's seams stop
//carrying it; above about 0.3 there is not enough value left between the pairs to tell them apart.
static const float TintEmissionFloor = 0.18;

//How brightly a tint's own emission burns: 1 at the brightest of the thirteen, TintEmissionFloor at the
//darkest, compressed so the fall is spread over the range rather than spent on the first stop.
float TintEmission(float3 primary)
{
    float luminance = saturate(dot(primary, float3(0.2126, 0.7152, 0.0722)));

    return lerp(TintEmissionFloor, 1.0, sqrt(luminance));
}

//The same tint pushed towards its own fully saturated form at UNCHANGED luminance. amount 0 is the tint
//as it stands, 1 is as saturated as that hue goes. A neutral tint is its own saturated form, so this
//cannot move one - which is the property the gem needs and the reason it is written this way round.
float3 SaturateTint(float3 primary, float amount)
{
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 pure = primary / max(peak, 1e-3);

    float luminance = dot(primary, float3(0.2126, 0.7152, 0.0722));
    float pureLuminance = max(dot(pure, float3(0.2126, 0.7152, 0.0722)), 1e-3);

    return lerp(primary, pure * (luminance / pureLuminance), amount);
}

//===================================================================================================
//CUT GEM (#308): a brilliant-cut stone. Flat faces that each catch the light separately, hard glints
//where a face turns through the highlight, deep saturated colour in the body.
//
//THE FACETS ARE SHADED, NEVER BUILT, AND THAT IS #271'S RULING RATHER THAN A PREFERENCE. #231 cut the
//crystal trophy's mesh to 24 segments with flat per-facet normals and defended it as reading like
//brues; the owner's word was that the cup is HRANATY and should be PLYNULY BEZ OSTRYCH HRAN, and that
//"the intent of 'crystal sharp' was never to see the sharp edges". At a coarse segment count the
//OUTLINE itself goes polygonal, and NO SHADING FIXES A FACETED RIM. #271 named two ways out and this
//takes the second: keep the smooth surface and put the sharpness entirely into the shading.
//
//So SphereMesh is untouched - the same LOD ladder, the same instances, the same silhouette, no new
//geometry anywhere - and the faceting is a HEIGHT FIELD. That choice is not cosmetic either:
//
//  A pixel shader here CANNOT ROTATE A VECTOR FROM OBJECT SPACE INTO WORLD SPACE. The instance streams
//  carry no tangents and the object-to-world rotation never reaches this stage (PerturbNormalFromHeight
//  exists for exactly that reason). So "snap the object-space normal to the nearest facet and use it"
//  cannot be written: the snapped vector has no way home. What CAN be written is a SCALAR that depends
//  on the object-space direction, handed to PerturbNormalFromHeight, which takes its gradient in SCREEN
//  space and therefore does the object-to-world mapping for free, exactly as the vinyl's moulding and
//  the wool's winding already do.
//
//The scalar is the distance from the ball's surface to the plane of the facet it belongs to. Inside one
//facet that is a smooth function whose gradient points along the facet's own normal, so tilting by it
//drives the shading normal towards that facet - constant over the face, stepping at the boundary, with
//the silhouette left exactly circular.
//
//It is also a figure in the NORMAL rather than in the colour, which is what #305 and #311 established
//between them (see WoolPS's header), and on a sphere it is geometrically honest: a cut sphere really
//would have flat faces at those normals. The trophy's problem was that its profile was not a sphere.
//===================================================================================================

//How finely the object-space direction is quantized, and so how many faces the stone is cut into. Each
//step up adds a shell of lattice directions; 2 is a chunky brilliant, 4 is close to a disco ball.
float GemFacetCount;

//How hard the height field drives the shading normal towards its facet's own. Under about 0.5 the faces
//only bend the light and the stone reads as dimpled; over about 2 they are flat to the pixel and the
//edges between them are as hard as this can make them without touching the mesh.
float GemFacetDepth;

//How deeply the body absorbs its own colour along the view. The one figure the C# side states, because
//it is what decides whether the four dark types stay apart from one another - see the note in the
//constant that carries it.
float GemAbsorption;

//The three axes the direction is quantized against. Orthonormal by construction and deliberately not
//aligned to the sphere's own poles, so the cut never agrees with the mesh's seams or with the LOD
//ladder's coarsest rings.
static const float3 GemAxisA = float3(0.8017837, 0.2672612, 0.5345225);
static const float3 GemAxisB = float3(-0.3812464, 0.9174414, 0.1131828);
static const float3 GemAxisC = float3(-0.4605661, -0.2955083, 0.8368858);

//The girdle: how brightly the rim piles light up where a real stone's total internal reflection does,
//and how tightly. Bright and narrow - it is the one thing that says CUT rather than merely shiny.
static const float GemGirdle = 0.9;
static const float GemGirdlePower = 4.0;

//A facet is polished glass, so its highlight is a pinpoint. Tighter even than the marble's, because a
//facet is optically flat where a ground stone surface is merely smooth.
static const float GemGloss = 260.0;
static const float GemGlossStrength = 1.4;

//How much of the environment a facet mirrors, and the floor under the absorbed body so the darkest types
//do not converge on black. THE FLOOR IS NOT A NICETY: absorption tuned for the bright hues takes Type8,
//Type10, Type12 and Type13 to the same near-black, which is four of thirteen lost at once.
static const float GemEnvironment = 1.3;
static const float GemBodyFloor = 0.32;

//How far the body and the girdle are pushed towards their own fully saturated hue (#315). The floor
//above stops the dark types converging on BLACK; it cannot stop them converging on the GREY the
//uncoloured mirror leaves, which is what they measured as doing - black, brown and silver within 4.5 to
//7.3 dE of one another where the vinyl's own worst pair is 7.9. This is the other half of that floor,
//and it costs the saturated stones nothing visible: at constant luminance a red stone is already close
//to its own saturated form, and a neutral one IS it.
static const float GemChromaLift = 1.0;

//How much of the mirror a stone gets, as a share of its own luminance (the compressed map TintEmission
//uses). The mirror is uncoloured, so at full strength on every stone it is simply the SAME light added
//to all thirteen - which is why black and silver arrived 4.5 apart despite one being sixty times darker
//than the other in tint. A dark stone is a light trap; it gives back less of what falls on it.
static const float GemEnvironmentFloor = 0.22;

//Where the facets stop being drawn honestly. Quantized normals put HARD EDGES IN SCREEN SPACE where no
//geometric edge exists, and hard shading edges alias; once a pixel spans a facet there is nothing to
//resolve, so the faceting is faded back to the smooth sphere rather than left to boil. Measured against
//the pixel's reach across the BALL, which is what a facet's size is expressed in - and note #258's trap
//here, which is the same shape: its first band-limit measured the wrong quantity and the whole effect
//existed only in the arithmetic.
static const float GemFacetFadeStart = 0.05;
static const float GemFacetFadeEnd = 0.16;

float4 GemPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Which face this pixel is on: the direction quantized in the gem's own basis (contract point 6 - the
    //cut is object space, so the facets turn with the ball and a rolling stone flashes face after face
    //through the highlight, which IS the style).
    float3 local = float3(dot(direction, GemAxisA), dot(direction, GemAxisB), dot(direction, GemAxisC));
    float3 facet = normalize(round(local * GemFacetCount) / max(GemFacetCount, 1e-3));

    //...and the scalar that carries it into world space for free: how far this point of the surface lies
    //from the plane of its own facet. Smooth within a face, stepping between faces, and its gradient
    //points along the facet normal - see the header for why this cannot be done as a vector.
    float faceted = 1 - smoothstep(GemFacetFadeStart, GemFacetFadeEnd, footprint);
    float height = (dot(local, facet) - 1) * GemFacetDepth * faceted;

    float3 smoothNormal = normalize(input.WorldNormal);
    float3 worldNormal = PerturbNormalFromHeight(smoothNormal, input.WorldPosition, height);

    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The stone's own colour, saturated at constant luminance (#315). Both the body and the girdle are
    //built off this rather than off the raw tint: the mirror below is uncoloured and swamps whatever
    //chroma a dark tint has, so what little it has must count for more. See SaturateTint - a neutral tint
    //is unmoved by construction, which is what keeps black where it is while brown moves off it.
    float3 stone = SaturateTint(primary, GemChromaLift);

    //The body: absorbed along the view, so the stone is deepest where it is thickest and lightens towards
    //the rim where the path through it is short. Floored so the dark types keep their hue instead of all
    //arriving at black together.
    float thickness = saturate(dot(smoothNormal, eyeVector));
    float3 body = max(stone * exp(-GemAbsorption * thickness), stone * GemBodyFloor);

    //A facet is polished glass: barely any broad highlight, a strong mirror, and full smoothness so
    //Fresnel still runs to a mirror at the edge. The mirror is scaled by the stone's own luminance
    //(#315): uncoloured light added equally to all thirteen is light that tells none of them apart, and
    //it was most of why the darkest four measured as one grey.
    SurfaceSpecular surface;
    surface.Highlight = 0.25;
    surface.Environment = GemEnvironment
        * lerp(GemEnvironmentFloor, 1.0, sqrt(saturate(dot(primary, float3(0.2126, 0.7152, 0.0722)))));
    surface.Smoothness = 1;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(body, 1), 1, 1, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //The glint: a pinpoint off the key light, on the FACET's normal, so it snaps from face to face as the
    //stone turns rather than sliding over it. That snapping is the whole read of a cut stone in motion.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float3 halfway = normalize(towardsKey + eyeVector);

    shaded.rgb += DirLight0SpecularColor * GemGlossStrength
        * pow(saturate(dot(worldNormal, halfway)), GemGloss) * occlusion;

    //The girdle, where a real stone's total internal reflection piles light up along the rim.
    float girdle = pow(1 - thickness, GemGirdlePower);

    shaded.rgb += girdle * GemGirdle * stone * occlusion;

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //Contract point 3, both meanings, PatternPS's arithmetic.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);
        float peak = max(primary.r, max(primary.g, primary.b));

        float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelGem
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL GemPS();
    }
};

//===================================================================================================
//PLASMA ORB (#309): the desktop plasma-ball toy - a dark, nearly empty globe with thin bright filaments
//of ionised gas crawling across the inside of it, tinted by the type colour.
//
//IT IS THE ONLY STYLE WHOSE READ IS MOTION, and that is its whole reason to exist beside the other
//seven. Every one of them is a still material photographed under a moving camera; this one is alive
//whether anything is happening or not. A screenshot says almost nothing about it, which is a fact about
//how it must be JUDGED and not a complaint.
//
//THE FILAMENTS ARE A DOMAIN WARP: one field displaces the sample point of a second, and the narrow band
//where the second is near zero is drawn as a line. Warping the INPUT is what makes an arc WRITHE rather
//than merely ripple - the same reason the marble warps its vein coordinate - and it is what costs this
//style its place as the dearest of the eight: two noise evaluations where every other has one, animated,
//so nothing about it is cacheable.
//
//ITS COLOUR IS EMISSION, WHICH MAKES IT THE STRONGEST OF ALL EIGHT ON HUE AND THE WEAKEST ON VALUE.
//Emissive thin lines are read directly - not diluted by ambient, not filtered by transmission, not
//tinted by a backdrop, which are the four ways the other styles lose their hue. But the colour lives in
//thin bright lines over a dark shell, so a CLUSTER reads dark, and against a bright dome the balls
//become dark discs with faint colour in them. This style is scene-bound, exactly as the metal is, and
//that is a legitimate thing for a style to be: a level names its own.
//
//THE 8-BALL SOLVES ITSELF HERE, and it is worth saying because it did not in three of the other styles.
//The filament colour is the tint NORMALISED to its peak channel - the ripple's own trick - so a
//saturated red gives red filaments and Type8's 0.045 grey gives WHITE ones. A white-hot discharge in a
//black globe is not a compromise; it is what that toy actually looks like.
//===================================================================================================

//How far the first field displaces the second's sample point. The whole character of the arcs: at zero
//they are smooth rings, and it is the warp alone that makes them writhe, fork and rejoin.
float PlasmaWarp;

//How brightly a filament burns. The one figure the C# side states, because it is the whole of the
//style's colour and the only thing standing between a cluster and darkness.
float PlasmaGlow;

//How fast the arcs crawl, in radians of phase a second. Slow: a plasma ball's discharges wander, and
//anything quick enough to notice as ANIMATION stops reading as something alive and starts reading as a
//loop.
float PlasmaSpeed;

//The directions the warp and the filament field are read along, and their frequencies. Low, because a
//filament has to be resolvable at the stand-off a level is played from - the lesson the metal's brush
//and the wool's strands both record.
static const float3 PlasmaWarpA = float3(0.66, 0.49, -0.57);
static const float3 PlasmaWarpB = float3(-0.41, 0.81, 0.42);
static const float3 PlasmaWarpC = float3(0.52, -0.36, 0.77);
static const float3 PlasmaField = float3(0.34, 0.79, 0.51);

static const float PlasmaWarpFrequency = 3.1;
static const float PlasmaFieldFrequency = 3.5;

//How thin an arc is. High: a discharge is a filament and not a band, and this exponent is most of what
//separates "plasma ball" from "marbled sphere".
static const float PlasmaSharpness = 11.0;

//What the globe is worth between the filaments: nearly nothing, because the thing is mostly empty. The
//dark shell is what the arcs have to be bright against.
static const float PlasmaShell = 0.10;

//The core the arcs reach out of, and how hard it is crowded into the middle of the disc. It is what
//makes them read as REACHING rather than as a wire cage painted on the outside.
static const float PlasmaCore = 2.2;
static const float PlasmaCorePower = 3.0;

//The globe's own boundary: a faint Fresnel edge, so the thing has a surface even where no arc is
//touching it. In the ball's own colour, never the sky's - a plasma globe reflects almost nothing.
static const float PlasmaEdge = 0.35;
static const float PlasmaEdgePower = 3.5;

//A band-limited wave with a PHASE, which is what ReliefOctave cannot take and this style cannot do
//without: the arcs move by advancing the phase, and the band-limit still has to fade a wave out once a
//pixel spans it or a cluster of these boils.
float PlasmaWave(float3 position, float3 waveDirection, float frequency, float phase, float footprint)
{
    return sin(dot(position, waveDirection) * frequency + phase)
        * saturate(1 - footprint * frequency / 3.14159265);
}

float4 PlasmaPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;
    float time = PulseTime * PlasmaSpeed;

    //The warp, in OBJECT space (contract point 6): the arcs are inside the globe and they turn with it.
    //Three components read along three directions at three rates, so the displacement never settles.
    float3 warp = float3(
        PlasmaWave(direction, PlasmaWarpA, PlasmaWarpFrequency, time, footprint),
        PlasmaWave(direction, PlasmaWarpB, PlasmaWarpFrequency * 1.27, time * 1.31, footprint),
        PlasmaWave(direction, PlasmaWarpC, PlasmaWarpFrequency * 0.83, time * 0.74, footprint)) * PlasmaWarp;

    //...displacing the field the filament is cut out of. Thin lines at its zero crossings, the same
    //profile the marble's veins use, at an exponent that makes them filaments rather than bands.
    float field = PlasmaWave(direction + warp, PlasmaField, PlasmaFieldFrequency, time * 0.91, footprint);
    float filament = pow(saturate(1 - abs(field)), PlasmaSharpness);

    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float3 worldNormal = normalize(input.WorldNormal);
    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The discharge's colour: the tint's hue at full saturation, which for the 8-ball's grey IS white -
    //see the header, a white-hot discharge in a black globe is what that toy looks like. Its BRIGHTNESS
    //is the tint's own luminance (#315). The header's claim that the darkest type needs no special case
    //here was true of black alone and false of the PAIR: SILVER normalises to the same near-white, and
    //the two measured 4.7 dE apart. Black still burns white; it burns dimmer than silver's now.
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 hue = primary / max(peak, 1e-3);
    float3 discharge = hue * TintEmission(primary);

    //The arcs reach out of a core, so they brighten towards the middle of the disc rather than lying
    //evenly over the sphere.
    float centre = pow(saturate(dot(worldNormal, eyeVector)), PlasmaCorePower);

    //Contract point 2, and it RIDES ON THE FILAMENTS rather than standing beside them. A plasma ball has
    //an obvious reason to pulse, and a second independent brightness on top of an already-moving surface
    //reads as two effects fighting.
    float beat = Heartbeat(PulseTime * PulseSpeed - dot(input.WorldPosition, PulseDirection) / max(PulseWavelength, 1e-4));

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Occluded, and for the reason #258 measured on the film: every ball in a pile of these reaches the
    //eye and a pixel shows the sum over four or five of them, which at full strength turns the middle of
    //a cluster into a flat wash with no ball in it. Since #303 the W channel also carries burial, so this
    //same line makes a deep plasma dim progressively - linearly, this style's identity being its glow,
    //where the opaque styles' resting emission goes with the square (see BallEmission).
    float3 glow = discharge * filament * PlasmaGlow * lerp(1, PlasmaCore, centre)
        * lerp(1 - PulseDepth, 1, beat) * occlusion;

    //The globe: nearly empty between the arcs, with a faint edge so it has a surface at all.
    float edge = pow(1 - saturate(dot(worldNormal, eyeVector)), PlasmaEdgePower);
    float3 shell = primary * (PlasmaShell + edge * PlasmaEdge) * occlusion;

    float4 shaded = float4(glow + shell, 1);

    //Contract point 3, both meanings, PatternPS's arithmetic. Over a dark shell with thin bright lines
    //the alarm reads immediately; the landing flare washes the filaments out at its peak, which is right
    //- the ball flares - but it is the thing to look at first if this style is ever retuned.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        float3 lit = shaded.rgb + lerp(hue, 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelPlasma
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PlasmaPS();
    }
};

//===================================================================================================
//MOLTEN CRUST (#310): a cooling lump of lava. A near-black basalt crust cracked into plates, with the
//molten interior glowing through the seams in the type colour. The crust is matte and rough; the seams
//are the only bright thing on the ball, and they breathe.
//
//IT IS THE INVERSE CONSTRUCTION OF THE PLASMA ORB, and the two should be kept that way: plasma is thin
//bright lines over an EMPTY dark shell that WRITHE; lava is a solid heavy crust whose seams BREATHE.
//One is electricity, the other is heat. They share the emissive-colour trick and nothing else - this
//one keeps its relief, its lighting and its weight.
//
//ITS COLOUR LIVES IN THE EMISSION, which is the argument for building it and it is a strong one: an
//emissive seam is read directly, not diluted by ambient, not filtered by a backdrop, not tinted by a
//reflection and not transmitted through anything - the four ways every other style can lose its hue.
//
//THE 8-BALL AGAIN, AND THE PLASMA'S ANSWER AGAIN. A black glow in a black crust is nothing, and this
//style is worse placed than most because the crust is ALREADY black. The seam colour is the tint
//NORMALISED to its peak channel, so the 8-ball's grey glows WHITE-HOT - which for lava is not even a
//departure: the hottest part of a real flow is the whitest.
//
//WHAT IT GIVES UP KNOWINGLY: value separation. The crust is the same darkness on all thirteen, so what
//tells them apart is the seam colour alone. The crust carries a little of the tint (LavaCrustTint) so
//they are not literally identical, but this style leans on hue harder than any other except the plasma.
//===================================================================================================

//How many plate seams run over the ball. Low: a cooling crust breaks into a handful of big plates, and
//a dense net reads as gravel rather than as a cracked shell.
float LavaSeamFrequency;

//How wide a seam is. Wider than the ice's cracks, deliberately - a crack is a plane seen edge-on, and
//this is a GAP with molten rock at the bottom of it.
float LavaSeamWidth;

//How brightly the molten interior glows through. The one figure the C# side states: it is the whole of
//this style's colour, and over a black crust there is nothing else to see the ball by.
float LavaGlow;

//The three line fields' directions. Not aligned to the sphere's poles or to each other, so the plates
//come out irregular rather than as a lattice.
static const float3 LavaSeamA = float3(0.71, 0.48, -0.52);
static const float3 LavaSeamB = float3(-0.39, 0.83, 0.40);
static const float3 LavaSeamC = float3(0.55, -0.34, 0.76);
static const float2 LavaSeamRatio = float2(1.29, 1.77);

//How far the seams wander off the great circles three plain sine fields would cut, and at what scale.
//See the note where it is used: without this the style is a wire cage, which is what it looked like when
//it was first built.
static const float LavaSeamWander = 0.30;
static const float LavaSeamWanderFrequency = 2.1;

//How dark the crust is, and how much of the ball's own tint it keeps. Basalt is nearly black; the tint
//that survives is what stops thirteen crusts being literally the same object.
static const float LavaCrustDark = 0.16;
static const float LavaCrustTint = 0.62;

//THE HEAT ROUND A SEAM, and #338's answer to "the black crust takes up too much of the ball". The
//owner's complaint is about AREA - the type colour was confined to the seams, which are a fifteenth of
//the surface, so twelve of every thirteen pixels on the ball were black whatever colour it was.
//
//The obvious fixes are both wrong, and stating why is most of this decision. LIGHTENING THE CRUST throws
//away the argument the style is built on: an emissive seam is the one colour in this game that arrives
//undiluted - not filtered by ambient, a backdrop, a reflection or a transmission - and a crust carrying
//real tint is a diffuse surface again, which is thirteen dark grey balls under a dusk dome. Simply
//WIDENING THE SEAM until it covers the ball turns a cracked shell into a coloured ball with black spots,
//and the cracked shell is the read.
//
//So the colour comes from heat instead, which costs the style nothing it was built for: crust within a
//plate-width of a crack is THIN AND HOT and glows dully, and that glow is emission like the seam's. It
//grades out from the seam rather than flooding, so the plates stay plates and the ball gains a coloured
//half without a single diffuse pixel changing hue. The halo is never carried towards LavaIncandescent -
//only the seam's own core is white-hot, and cooler rock keeps its hue, which is also what stops the
//halo from undoing #315's separation.
//
//The width is a MULTIPLE of the seam's, capped by what SeamLine can express: it tests |sin| against the
//width, so a width at or over one never resolves to zero anywhere and the whole ball would glow. At the
//shipped figures this lands at 0.68.
//
//THE FALLOFF IS THE PART THAT WAS NOT OPTIONAL, and the first pass proved it by leaving it out. Written
//as (wide field MINUS seam) the halo is a PLATEAU - uniformly lit right out to its edge - and the ball
//came back a fat-banded NEON CAGE with the plates reduced to black islands between the bars. That is the
//PLASMA, which this style's own header says at length to keep it away from. A power over the wide field
//makes it a gradient instead: bright against the gap, dark a plate-width away, which is what a
//temperature falling off through rock looks like and what leaves the plates reading as plates.
static const float LavaHeatWidth = 1.9;
static const float LavaHeatGlow = 0.34;
static const float LavaHeatFalloff = 2.6;

//The crust's own roughness, on the same octave sum the vinyl skin uses for its moulding. This is one of
//the few styles that WANTS that relief kept rather than removed: plates have to read as broken stone.
static const float LavaCrustRelief = 0.024;

//How deep a seam is cut into the crust, so the plates stand proud of the gaps between them.
static const float LavaSeamDepth = 0.03;

//What the hottest core of a seam is carried towards. Incandescence runs to white through yellow, so the
//middle of a seam loses its hue while its edges keep it - which is what makes it read as HOT rather
//than as a coloured line painted in a groove.
static const float3 LavaIncandescent = float3(1.0, 0.86, 0.62);
static const float LavaCorePower = 6.5;

//The crust's specular: weak and broad. Basalt is matte, and a shine on it turns the whole thing into
//painted plastic faster than any other error here.
static const float LavaHighlight = 0.35;
static const float LavaEnvironment = 0.2;
static const float LavaSmoothness = 0.2;

float4 LavaPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //THE SEAMS HAVE TO WANDER OR THEY ARE A CAGE. Three sine fields on a sphere cut great circles, and
    //three great circles read as wire wrapped round a ball rather than as rock that has cracked - it was
    //built that way first and that is exactly what it looked like. Displacing the coordinate they are
    //read at is the fix, and it is the plasma's domain warp at a fraction of the strength: enough to make
    //a seam wander and fork, not enough to make it writhe. Unlike the plasma's it does not move.
    float3 wander = float3(
        ReliefOctave(direction, LavaSeamB, LavaSeamWanderFrequency, footprint),
        ReliefOctave(direction, LavaSeamC, LavaSeamWanderFrequency * 1.23, footprint),
        ReliefOctave(direction, LavaSeamA, LavaSeamWanderFrequency * 0.79, footprint)) * LavaSeamWander;

    //The plate seams, in OBJECT space (contract point 6). A heavy crusted ball turning is very readable,
    //which makes this one of the better rotation cues in the set.
    float3 seamPosition = direction + wander;

    float seam = saturate(
        SeamLine(seamPosition, LavaSeamA, LavaSeamFrequency, LavaSeamWidth, footprint)
        + SeamLine(seamPosition, LavaSeamB, LavaSeamFrequency * LavaSeamRatio.x, LavaSeamWidth, footprint)
        + SeamLine(seamPosition, LavaSeamC, LavaSeamFrequency * LavaSeamRatio.y, LavaSeamWidth, footprint));

    //The same three lines read again at a wider width: the hot crust either side of a gap (#338). Taken as
    //a MAX and not a sum, which is the whole difference between a halo and a wash - three wide fields
    //added together saturate over most of the ball and the plates stop being plates, where the nearest of
    //them is a distance to the nearest crack, which is what heat actually follows. Shaped by a power into
    //a gradient rather than a plateau (see LavaHeatFalloff) and cut off inside the seam, so the two never
    //pay twice for the same pixel.
    float heatWidth = LavaSeamWidth * LavaHeatWidth;

    float hot = max(SeamLine(seamPosition, LavaSeamA, LavaSeamFrequency, heatWidth, footprint),
        max(SeamLine(seamPosition, LavaSeamB, LavaSeamFrequency * LavaSeamRatio.x, heatWidth, footprint),
            SeamLine(seamPosition, LavaSeamC, LavaSeamFrequency * LavaSeamRatio.y, heatWidth, footprint)));

    float halo = pow(hot, LavaHeatFalloff) * (1 - seam);

    //The crust's own broken-stone grain, plus the seams cut into it. Kept rather than removed - see the
    //header; this is the one new style that wants the vinyl's moulding machinery.
    float height = SurfaceRelief(direction, footprint) * LavaCrustRelief - seam * LavaSeamDepth;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);
    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //Basalt: nearly black, keeping just enough of the tint that thirteen crusts are not one object.
    float3 crust = primary * LavaCrustTint * LavaCrustDark + LavaCrustDark * (1 - LavaCrustTint);

    SurfaceSpecular surface;
    surface.Highlight = LavaHighlight;
    surface.Environment = LavaEnvironment;
    surface.Smoothness = LavaSmoothness;

    //A seam sees almost no sky - it is a gap in a thick shell - so it takes the ambient down with it.
    float cavity = 1 - 0.7 * seam;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(crust, 1), 1, cavity, surface);

    //Contract point 2, ROUTED INTO THE SEAMS rather than added beside them. Lava has an obvious reason to
    //pulse and the balls already share a wave through the cluster, so the beat IS the breath. The risk
    //this replaces is double-applying it - a flat emission plus a breathing seam is a ball that pulses
    //twice as hard as its neighbours, which is why EmissiveStrength is zero for this style.
    float beat = Heartbeat(PulseTime * PulseSpeed - dot(input.WorldPosition, PulseDirection) / max(PulseWavelength, 1e-4));

    //The molten interior. Normalised to the tint's peak so the 8-ball glows white-hot rather than not at
    //all (the plasma's answer, and for lava it is not even a departure), and carried towards incandescent
    //white at the hottest core of each seam so the middle loses its hue while the edges keep it.
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 hue = primary / max(peak, 1e-3);

    //BOTH THE BRIGHTNESS AND THE INCANDESCENT CARRY ARE SCALED BY THE TINT'S OWN LUMINANCE (#315), and
    //the header's "what it gives up knowingly is value separation" gave up more than it meant to. With
    //the seams the only coloured thing on the ball, discarding value discards the axis that separates
    //every pair sharing a hue family: ORANGE AND BROWN MEASURED 2.6 dE APART, the tightest pair anywhere
    //in this game against a vinyl control at 7.9, and black and silver 4.2.
    //
    //The carry needs the same scaling as the glow and not only the glow, because dE2000 forgives a pure
    //lightness gap and lerp(hue, LavaIncandescent) drives both warm tints onto the SAME near-white
    //wherever the seam is hottest - the brightest and so most heavily weighted part of the disc. A cooler
    //flow glows less brightly AND does not run to white at its core; that is all this is, and it costs
    //the style nothing it was built for.
    float emission = TintEmission(primary);

    float core = pow(seam, LavaCorePower) * emission;
    float3 molten = lerp(hue, LavaIncandescent, core);

    //Occluded LINEARLY, the plasma's own power and deliberately not BallEmission's square (#303): this
    //style's identity IS its glow, so a buried ball dims with the pile - the flat-wash correction the
    //plasma already carries - without its seams ever going out the way a resting vinyl breath now does.
    //The beat dims inside the pile with the rest of the glow, which the plasma also already accepted:
    //the seams ARE the breath here, and there is no resting half to split it from.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    float breath = lerp(1 - PulseDepth, 1, beat);

    shaded.rgb += molten * seam * LavaGlow * emission * breath * occlusion;

    //And the hot crust round the gap (#338): the same glow at a fraction of the strength over several
    //times the area, in the ball's own hue and never carried to white - see LavaHeatWidth's note. It
    //breathes with the seam it belongs to, because it is the same heat seen through more rock.
    shaded.rgb += hue * halo * LavaGlow * LavaHeatGlow * emission * breath * occlusion;

    //Contract point 3, both meanings, PatternPS's arithmetic.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        float3 lit = shaded.rgb + lerp(hue, 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelLava
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL LavaPS();
    }
};

//===================================================================================================
//CRACKLED PORCELAIN (#312): a glazed ceramic sphere - a deep, wet-looking coloured glaze over a white
//ceramic body, crazed all over with the fine hairline network an old glaze develops. Hard, cool and
//expensive-looking, and THE PATTERN IS THE TELL: a crackle net is instantly readable as ceramic and as
//nothing else.
//
//WHAT MAKES IT PORCELAIN RATHER THAN A SHINY BALL is the GLAZE DEPTH - the colour appears to sit
//slightly BELOW the surface, because the glaze is a clear layer over it. Two lobes do that: a very tight
//bright one for the glaze's own face, and the diffuse body underneath it. It is one of the cheapest
//convincing material tricks there is, and it is the whole difference between this and painted plastic.
//There is NO RELIEF: a fired glaze is glass-smooth, so like the marble this style spends the vinyl's
//moulding budget elsewhere.
//
//HOW IT STAYS APART FROM THE TWO STYLES IT NEIGHBOURS, which is a real risk with nine of them:
//  - Against the MARBLE (also opaque, also hard, also polished, also object-space patterned): marble's
//    figure is BROAD SOFT VEINS in a lighter tint; this is a FINE DARK HAIRLINE NET. Marble's specular
//    is a hard surface highlight; this is a coat over a body.
//  - Against the ICE (which uses the same SeamLine): ice cracks are BRIGHT, because they are internal
//    faces catching light inside a translucent solid. These are gaps in an opaque glaze.
//
//THE BODY IS WHITE AND THE GLAZE IS WHAT CARRIES THE COLOUR, and #339 is here because until it the code
//had neither. Everything above was true of the shader except the one sentence the whole material rests
//on: the tint WAS the body. A coloured diffuse sphere under a tight highlight is PAINTED PLASTIC, and
//"it isn't clear what this style is supposed to be at all" is exactly what painted plastic looks like -
//the owner could not name the material because the shader was not drawing one.
//
//Porcelain is white clay. The colour is a layer lying ON it, and every tell of the material follows from
//that one fact rather than from the crack net this header used to rest the whole read on:
//  - where the glaze runs THIN the clay reads through, so the ball is not one flat tint. Dipped ware is
//    never evenly coated, and that unevenness is most of the difference between a fired object and a
//    painted one;
//  - a craze line is the glaze PARTED DOWN TO THE CLAY, so what shows in it is white - stained the brown
//    an old piece takes up over a century (see PorcelainStain);
//  - and the clay is thin enough to pass light, which is the tell nobody mistakes: a held-up teacup
//    glows. That is what the translucency term is for, and it goes through the CLAY's colour and not the
//    glaze's, because it is the body that scatters.
//
//THE 8-BALL, AND THIS TIME THE ANSWER FALLS OUT OF THE MATERIAL RATHER THAN BEING A RULE. Dark cracks on
//a black glaze are no cracks at all, and the normalise-to-peak trick the plasma and the lava use does not
//help because a crack is not an emission. It used to be answered with an inversion keyed on the glaze's
//own luminance - a bright glaze crazed with DARKER lines, a dark one with LIGHTER ones - which was one
//rule instead of thirteen constants and was right about the problem. The stained clay does the same
//crossover for a reason instead of a rule: against a pale glaze the stain is the darker of the two, and
//against a dark one the clay is the lighter, with no test anywhere to say so.
//
//AND THE CRACKS CUT A GROOVE AS WELL AS TAKING A TONE, because #305 and #311 established that a figure
//in the colour alone can be swamped by the shading while one in the normal cannot. The groove is very
//shallow - a crack in a glaze is a parting, not a channel - but it is what keeps the net readable on the
//tints where the tone step is smallest.
//===================================================================================================

//Wave count of the crazing over the ball. The highest of any figure in this file, because craquelure IS
//fine - but still bounded by what a ball a few dozen pixels across can resolve, which is the lesson the
//metal's brush recorded at the cost of an invisible one.
float PorcelainCrackFrequency;

//How wide a hairline is. The thinnest in the file: a crack in a glaze has no area at all, and a wide one
//reads as a broken egg rather than as an antique.
float PorcelainCrackWidth;

//How deep and wet the glaze looks - how much of the environment its face mirrors. The one figure the C#
//side states, because it is what decides whether a dark glaze under a bright dome washes out to sky.
float PorcelainGlaze;

//The crazing's line directions and their ratios, and the wander that keeps the net off the great circles
//three plain sine fields would cut. The lava paid for that lesson; craquelure is even less forgiving,
//since a REGULAR crack net reads as a printed pattern instantly.
static const float3 PorcelainCrackA = float3(0.74, 0.44, -0.51);
static const float3 PorcelainCrackB = float3(-0.36, 0.85, 0.38);
static const float3 PorcelainCrackC = float3(0.50, -0.40, 0.77);
static const float2 PorcelainCrackRatio = float2(1.33, 1.87);
static const float PorcelainWander = 0.22;
static const float PorcelainWanderFrequency = 3.3;

//Where the craze stops being worth drawing, measured against its own LINE WIDTH - #337's lesson, and
//porcelain is where it bites hardest. SeamLine fades on the WAVELENGTH, which here is twenty times wider
//than the line riding it, so a hairline is left fully drawn long after a pixel can hold it. What that
//looks like is not a faint net: it is WHITE SPARKLE. The crack's groove tilts a normal inside a pixel,
//the glaze's mirror-smooth Fresnel fires on whichever pixels the tilt happens to catch, and the ball
//comes out speckled as if it were dirty - which is its own answer to "it does not read as any material".
//
//A line of |sin| < w at frequency f is 2w/f wide in direction units, so footprint * f / 2w is that line
//in pixel-pairs; the constant holds it at full strength until it is about a pixel and fades it over the
//last octave, exactly as IceCrackBandLimit does. The craze is therefore a CLOSE-RANGE figure and gone by
//play distance - which is why this style could not be left resting its whole read on it.
static const float PorcelainCrazeBandLimit = 1.5;

//The white clay under the glaze - the body of the thing, and the one addition #339 turns on. Slightly
//warm and slightly under one: porcelain is white, not paper, and a body at 1.0 clips its own highlight
//roll-off away before the tonemap ever sees it.
static const float3 PorcelainClay = float3(0.90, 0.895, 0.86);

//How much of the tint the glaze lays over that clay where it is thickest, and how far the coat varies
//over the ball. The cover stays HIGH: the clay is meant to read through where the glaze thins, not to
//wash the tint out - thirteen colours have to stay thirteen colours, and this term is the one in this
//change that could take that away. The variation is three sine octaves and nothing more.
static const float PorcelainGlazeCover = 0.94;
static const float PorcelainGlazePool = 0.14;
static const float PorcelainGlazeFrequency = 2.3;
static const float3 PorcelainGlazeA = float3(0.66, 0.51, -0.55);
static const float3 PorcelainGlazeB = float3(-0.42, 0.79, 0.45);
static const float3 PorcelainGlazeC = float3(0.47, -0.58, 0.67);

//What shows in a craze line: the clay, stained the brown an old glaze takes up. Both halves matter - the
//clay is what makes the line read on a DARK glaze and the stain is what makes it read on a PALE one, and
//between them they do the crossover the luminance test used to do explicitly.
static const float3 PorcelainStain = float3(0.34, 0.24, 0.15);
static const float PorcelainStainAmount = 0.62;

//===================================================================================================
//THE ORNAMENT: A HILBERT MEANDER IN WHITE ENAMEL, round the ball's own equator. The owner's idea, and
//it is the answer to "what IS this style" that no amount of shading was going to give: china is known
//by its DECORATION. A material read can be argued about; a painted band cannot be mistaken for anything
//but tableware, and it tells this style from the other nine across a whole cluster at a glance.
//
//WHY A HILBERT CURVE AND NOT A DRAWN MOTIF. It is the one ornament of this kind that can be evaluated
//rather than stored - no texture, no UVs, nothing in the content pipeline - and it happens to TILE: the
//standard curve enters its square at one corner of the bottom edge and leaves at the other, so laying
//tiles side by side round the equator joins them into ONE continuous meander that closes on itself. That
//is exactly the construction of a border on a plate, and it comes out of the curve's own definition
//rather than being arranged.
//
//HOW A PIXEL FINDS IT, which is the part worth knowing. The curve visits every cell of its grid, so the
//pixel's own cell is enough: take the cell's index along the curve (HilbertIndex), ask where the curve
//was one step before and one step after (HilbertPoint), and the curve inside this cell is the polyline
//from the entry edge's midpoint through the cell's centre to the exit edge's midpoint. Two segment
//distances and it is drawn. Neighbouring cells cannot be nearer than half a cell unless the curve
//crosses into this one - in which case they are the entry or the exit and are already accounted for - so
//this is exact for any ribbon under half a cell wide, which is every ribbon that looks like ornament.
//The first and last cells of a tile step OUTSIDE it for their missing neighbour, which is what welds one
//tile to the next.
//===================================================================================================

//The grid a tile is divided into: a power of two, and the curve's order follows from it (4 = order two).
//Higher is finer china and lower is a bolder motif, and the ceiling is what a ball a few dozen pixels
//across can still resolve - the lesson the metal's brush paid for and the craze above pays for again.
static const int PorcelainHilbertN = 4;

//How many tiles run round the equator. Together with the grid this decides the motif's size: a tile spans
//2*pi/tiles of arc, so a cell is about 0.39 of a ball radius at four tiles of four - coarse enough to
//survive play distance, which is the whole reason the ornament and not the craze carries this style.
static const float PorcelainOrnamentTiles = 4.0;

//How far the band reaches either side of the equator, in the direction's own y. A band and not a full
//covering: a sphere has no seamless square parameterisation, and a border round the middle is what china
//actually wears. Balls turn, so across a cluster it is seen at every angle, which is also true of a
//plate on a shelf.
static const float PorcelainOrnamentBand = 0.58;

//Half the ribbon's width, in cells, and how far the enamel stands proud of the glaze. Applied decoration
//sits ON the fired surface, so it has a real edge to catch the light - and that relief is what makes the
//ornament read on the WHITE ball, where white enamel on a white glaze has no colour step at all (#305
//and #311's lesson, arriving here as the reason the band is not just painted on).
static const float PorcelainOrnamentWidth = 0.135;
static const float PorcelainOrnamentRelief = 0.010;

//The enamel itself: a touch brighter and cooler than the clay, because it is a second, whiter slip laid
//over the glaze rather than the body showing through.
static const float3 PorcelainEnamel = float3(0.96, 0.955, 0.94);

//How much of the glaze shows through the enamel, and it is here for a MEASURED reason rather than a
//painterly one. A pure white band over a fifth of the disc lifts every ball towards white by the same
//amount, and what that costs is the DARK end of the palette: black against brown went from 9.3 dE to
//6.0, which is under the moulded vinyl's own worst pair (6.4) - the line #315 says every style has to
//clear. A slip laid this thin genuinely does let its ground through, so carrying a fraction of the
//glaze's own colour into it is both what the material does and what gives each ball back a share of the
//separation the band was taking. It still reads as white; it reads as white ON something.
static const float PorcelainEnamelSheer = 0.22;

//Where the ribbon stops being worth drawing, on the same rule as the craze and with a great deal more
//room: the ribbon is about a fifteenth of the ball wide against the craze's hairline, so it is still
//several pixels across at play distance and this only engages on the smallest LOD.
static const float PorcelainOrnamentBandLimit = 1.5;

//How deep a crack parts the glaze. Very shallow: it is there so the net survives on the tints where the
//tone step is smallest, not to be seen as relief.
static const float PorcelainCrackDepth = 0.007;

//The glaze's own face: a tight bright lobe over the body, which is what puts the colour UNDER a surface
//rather than on it.
static const float PorcelainGloss = 210.0;
static const float PorcelainGlossStrength = 1.1;

//What the body under the glaze does with light: a normal diffuse ceramic, with the broad highlight cut
//back because the glaze above it answers that instead.
static const float PorcelainHighlight = 0.3;

//The quadrant flip/swap both Hilbert conversions turn on, written with selects rather than an if: it is
//per-pixel varying, and while nothing in here takes a derivative, this file's rule is that a divergent
//branch inside a shader that uses ddx/ddy elsewhere is not worth the risk for two instructions.
int2 HilbertRotate(int n, int2 p, int rx, int ry)
{
    int2 flipped = rx == 1 ? int2(n - 1 - p.x, n - 1 - p.y) : p;

    return ry == 0 ? flipped.yx : p;
}

//Where a cell sits along the curve, and the inverse. Both are the textbook pair; the loops are bounded by
//a compile-time constant so they unroll to a couple of dozen integer instructions.
int HilbertIndex(int2 p)
{
    int d = 0;

    [unroll]
    for (int s = PorcelainHilbertN / 2; s > 0; s /= 2)
    {
        int rx = (p.x & s) > 0 ? 1 : 0;
        int ry = (p.y & s) > 0 ? 1 : 0;

        d += s * s * ((3 * rx) ^ ry);
        p = HilbertRotate(PorcelainHilbertN, p, rx, ry);
    }

    return d;
}

int2 HilbertPoint(int d)
{
    int2 p = int2(0, 0);
    int t = d;

    [unroll]
    for (int s = 1; s < PorcelainHilbertN; s *= 2)
    {
        int rx = 1 & (t / 2);
        int ry = 1 & (t ^ rx);

        p = HilbertRotate(s, p, rx, ry);
        p += s * int2(rx, ry);
        t /= 4;
    }

    return p;
}

float SegmentDistance(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));

    return length(pa - ba * h);
}

//The ornament's signed profile: how far this pixel is from the meander, in cells, and 8 when it is not in
//the band at all. Returned as a distance rather than a mask so the caller can make both a colour and a
//rounded bead out of it without evaluating the curve twice.
float PorcelainOrnamentDistance(float3 direction)
{
    //The band, in the ball's OWN frame (contract point 6): the border turns with the ball, which on a
    //style this static is most of what says the thing is rotating at all.
    float v = direction.y / PorcelainOrnamentBand;

    if (abs(v) > 1) return 8;

    //Azimuth into tiles. The atan2 cut falls between two tiles and not inside one, so the seam there is
    //the same weld that joins every other pair of tiles - which is the whole reason the tiling had to run
    //round the equator an INTEGER number of times.
    float u = (atan2(direction.z, direction.x) / (2 * 3.14159265) + 0.5) * PorcelainOrnamentTiles;

    float2 f = float2(frac(u), (v + 1) * 0.5) * PorcelainHilbertN;
    int2 cell = clamp(int2(floor(f)), 0, PorcelainHilbertN - 1);

    int d = HilbertIndex(cell);
    int last = PorcelainHilbertN * PorcelainHilbertN - 1;

    //The missing neighbour at either end of a tile is the cell across the tile boundary, which is what
    //welds the tiles into one continuous meander.
    int2 previous = d == 0 ? cell + int2(-1, 0) : HilbertPoint(d - 1);
    int2 following = d == last ? cell + int2(1, 0) : HilbertPoint(d + 1);

    float2 centre = cell + 0.5;
    float2 entry = (centre + previous + 0.5) * 0.5;
    float2 exit = (centre + following + 0.5) * 0.5;

    return min(SegmentDistance(f, entry, centre), SegmentDistance(f, centre, exit));
}

float4 PorcelainPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //The wander first, for the reason the lava's header gives at length: three sine fields on a sphere cut
    //great circles, and a regular net reads as a printed pattern rather than as a glaze that has crazed.
    float3 wander = float3(
        ReliefOctave(direction, PorcelainCrackB, PorcelainWanderFrequency, footprint),
        ReliefOctave(direction, PorcelainCrackC, PorcelainWanderFrequency * 1.19, footprint),
        ReliefOctave(direction, PorcelainCrackA, PorcelainWanderFrequency * 0.81, footprint)) * PorcelainWander;

    //The crazing itself, in OBJECT space (contract point 6): the net is IN the glaze and turns with it.
    float3 crackPosition = direction + wander;

    float craze = saturate(
        SeamLine(crackPosition, PorcelainCrackA, PorcelainCrackFrequency, PorcelainCrackWidth, footprint)
        + SeamLine(crackPosition, PorcelainCrackB, PorcelainCrackFrequency * PorcelainCrackRatio.x, PorcelainCrackWidth, footprint)
        + SeamLine(crackPosition, PorcelainCrackC, PorcelainCrackFrequency * PorcelainCrackRatio.y, PorcelainCrackWidth, footprint));

    //...faded on the LINE and not on the wave it rides - see PorcelainCrazeBandLimit, and the sparkle that
    //note describes is what leaving this out looks like.
    craze *= saturate(PorcelainCrazeBandLimit
        - footprint * PorcelainCrackFrequency / (2 * max(PorcelainCrackWidth, 1e-3)));

    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 clay = SrgbToLinear(PorcelainClay);

    //THE GLAZE OVER THE CLAY (#339). How thick the coat lies varies over the ball, so the body reads
    //through where it thins - which is what says a fired, dipped object rather than a painted one, and is
    //the whole of why this ball is no longer a flat tint with a dot of white on it.
    float coat = 0.5 + 0.5 * (ReliefOctave(direction, PorcelainGlazeA, PorcelainGlazeFrequency, footprint)
        + ReliefOctave(direction, PorcelainGlazeB, PorcelainGlazeFrequency * 1.41, footprint)
        + ReliefOctave(direction, PorcelainGlazeC, PorcelainGlazeFrequency * 0.83, footprint)) / 3;

    float3 glaze = lerp(clay, primary, saturate(PorcelainGlazeCover - PorcelainGlazePool * (1 - coat)));

    //And the craze: the glaze parted down to the clay, stained. No luminance test - see the header; the
    //crossover is what these two colours DO against a pale ground and against a dark one.
    float3 color = lerp(glaze, lerp(clay, SrgbToLinear(PorcelainStain), PorcelainStainAmount), craze);

    //THE ORNAMENT. Antialiased and band-limited off the FOOTPRINT rather than off fwidth of the distance,
    //deliberately twice over: the distance jumps to 8 outside the band, which would smear that edge, and
    //it is read through an atan2 whose cut would put one bad column down the ball. A cell is 2*pi over
    //tiles times grid of arc, so the footprint divided by that IS the pixel measured in cells.
    float ornament = PorcelainOrnamentDistance(direction);
    float cellArc = 6.28318531 / (PorcelainOrnamentTiles * PorcelainHilbertN);
    float pixelCells = footprint / cellArc;

    //A quarter of that for the ANTIALIAS: footprint is ddx plus ddy, so it is about two pixels, and using
    //the whole of it as a smoothstep half-width made the transition wider than the ribbon itself - the
    //band came out a soft embossed shadow instead of a painted line, which is not what enamel does.
    float edge = max(pixelCells * 0.25, 1e-4);

    float ribbon = (1 - smoothstep(PorcelainOrnamentWidth - edge, PorcelainOrnamentWidth + edge, ornament))
        * saturate(PorcelainOrnamentBandLimit - pixelCells / max(2 * PorcelainOrnamentWidth, 1e-3));

    color = lerp(color, lerp(SrgbToLinear(PorcelainEnamel), glaze, PorcelainEnamelSheer), ribbon);

    //...and the groove, so the net is a figure in the NORMAL as well as in the colour (#305, #311). Shallow
    //on purpose: a crack in a glaze is a parting, not a channel. The enamel goes the other way and stands
    //PROUD of it, on a parabolic dome - which is what "rounded" means here, and what gives the band a lit
    //edge on the white ball where its colour step is nothing.
    float beadEdge = saturate(ornament / PorcelainOrnamentWidth);
    float bead = (1 - beadEdge * beadEdge) * ribbon;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition,
        bead * PorcelainOrnamentRelief - craze * PorcelainCrackDepth);

    //The body under the glaze: an ordinary diffuse ceramic, its own broad highlight cut back because the
    //glaze's face answers that instead. Smooth, because a fired glaze is glass.
    SurfaceSpecular surface;
    surface.Highlight = PorcelainHighlight;
    surface.Environment = PorcelainGlaze;
    surface.Smoothness = 1;

    //A crack sees less sky than the face around it, which is what makes the net read even where the tone
    //step is small.
    float cavity = 1 - 0.35 * craze;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, cavity, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //THE GLAZE'S OWN FACE, and the whole of what puts the colour under a surface rather than on it: a
    //tight bright lobe sitting ON TOP of a body that is already shaded. Taken off the SMOOTH normal and
    //not the crazed one, deliberately - the glaze is continuous over a hairline crack, and a highlight
    //that broke at every crack would say the surface was chipped rather than crazed.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float3 halfway = normalize(towardsKey + eyeVector);

    shaded.rgb += DirLight0SpecularColor * PorcelainGlossStrength
        * pow(saturate(dot(normalize(input.WorldNormal), halfway)), PorcelainGloss) * occlusion;

    //THE TELL NOBODY MISTAKES, and the second half of #339: fine porcelain passes light. A held-up teacup
    //glows, and no plastic does. Same construction as the ice's, at a third of the strength - this is a
    //dense fired body and not a scattering solid - and through the CLAY's colour rather than the glaze's,
    //because what light crosses is the body: a red cup lit from behind glows warm WHITE, not red.
    float through = pow(saturate(dot(-worldNormal, towardsKey)), 2);

    shaded.rgb += through * TranslucencyStrength * DirLight0DiffuseColor * lerp(clay, primary, 0.35) * occlusion;

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //Contract point 3, both meanings, PatternPS's arithmetic.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);
        float peak = max(primary.r, max(primary.g, primary.b));

        float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelPorcelain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PorcelainPS();
    }
};

//===================================================================================================
//ROUGH GRANITE (#324) - the ball that is NOT one of the thirteen colours.
//
//Every other ball technique in this file takes PatternPrimaryColor and does something with it, because
//every other one draws a ball the player matches. This one draws the ROCK (BallKind.Rock): a ball that
//matches with nothing and that no colour removes. So it ignores the tint entirely and is the only
//shading here whose colour is a constant - and that is a rule, not a saving. A rock wearing one of the
//thirteen colours is a lie the player acts on: they would aim a colour at it and it would refuse.
//
//It is also the one shading a MAP cannot name. BallStyle says what a level's balls are MADE of; the rock
//opts out of it and is stone on a bubble level and on a lava level alike, because "that one is
//different" has to survive all ten materials or it is not a signal.
//
//What makes it read as stone, in the order the eye picks it up:
//  1. It is NOT ROUND. Every other ball in this game was cast, blown, wound, ground or glazed, and all
//     of them are smooth spheres with a figure drawn on them; this one is CARVED OUT OF ROUND in its own
//     vertex shader (#340 - see the block above StoneShape), and carries the largest relief in the file
//     on top of that. The silhouette is the first thing the eye reads and the only thing that survives
//     any distance, so it is where "that one is different" is now said. This paragraph used to end
//     "nothing here builds geometry" and rested the whole read on shading a perfect sphere; the owner's
//     playtest asked for the opposite in as many words, and confirmed it when asked.
//  2. It is SPECKLED. Granite is an aggregate of grains large enough to see: pale quartz and feldspar,
//     dark mica and hornblende. Three high-frequency waves MULTIPLIED give a field concentrated near
//     zero with isolated peaks of both signs, which is a fleck field - a sum would give creases, which
//     is what the marble's veining is made of and what this must not look like.
//  3. It is MATTE. No polish, no pinpoint, almost no environment. The marble is what a stone looks like
//     when it has been worked; this is what it looks like when it has not.
//
//And it does NOT BREATHE. BallRenderSet draws the rocks in their own bucket plane at PulseDepth zero, so
//contract point 2 comes out as a STEADY floor: a rock stands dead still inside a cluster that pulses,
//which is worth more than any figure drawn on it, because motion is the first thing the eye reads and its
//absence names the odd one out across a whole field, at any distance and under any dome.
//
//⚠ It is NOT drawn without emission, and the reason is worth reading before anyone takes it back out. The
//player looks UP at the cluster from the island - at its UNLIT side - and every other ball lights that
//side by two routes this material has neither of: its own glow, and TranslucencyStrength, the key light
//carried THROUGH a hollow shell from behind. Stone is solid and does not glow, so a physically honest rock
//was the only genuinely dark ball in the frame and read as the 8-ball. Raising the body tint did not fix
//it and neither did the strongest ambient in the palette; those two missing terms were worth about as much
//as everything else together. The emission stands in for the translucency. What it must never buy back is
//the beat.
//===================================================================================================

//Wave count of the mineral grain over the ball. High: a grain is what separates rock from a grey ball,
//and at a low count the speckle turns into blotches and reads as a mouldy marble.
float StoneGrainFrequency;

//How far a grain carries from the body grey - towards black one way and a pale quartz the other. The one
//figure this shading spends anything on, which is why the C# side states it.
float StoneGrainContrast;

//Amplitude of the coarse relief, in world units. The largest ball figure in this file; see point 1.
float StoneRoughness;

//The stone itself, in sRGB like every other colour written here.
//
//⚠ THE VALUE HAS TO BE HIGH FOR A REASON THAT STILL STANDS, and the first attempt at this style got it
//wrong by being physically reasonable: a 0.42 grey is a fair granite albedo and it came out BLACK on
//screen, next to the 8-ball rather than next to the silver. Two things take a rock down that no other
//ball here pays:
//it is the only ball in the game with NO EMISSION (every other one adds 0.3-0.5 of its own colour on top of
//its shading, so the whole cluster around this one is lifted), and it is the only one that barely mirrors
//the sky (see StoneEnvironment). A neutral surface next to thirteen lit ones reads far darker than its
//albedo says, and the tonemap then compresses what is left. So this is a LIGHT stone: measured against the
//cluster, not against a rock.
//
//Warm, deliberately: Type11 (silver) is a COOL slate at (0.5, 0.53, 0.58) and is the one type a grey ball
//can be confused with. Same value, opposite hue, and the two never meet.
//
//⚠ 0.63 UNTIL #340, and what bought the drop was the carving. The owner's second sentence was that the
//ball is too light in value, and the argument above says why it could not simply be darkened - so it was
//not darkened simply. A carved ball has real form: its own gouges shade it, which is a source of contrast
//a smooth sphere with no emission and no mirror had nowhere to get, and value that used to have to come
//out of the albedo now comes out of the shape. MEASURED against the pair this constant exists to sit
//between, on the meadow at dome 1: rock 84 and 75 mean luminance on two rocks, Type8 (black) 42, Type11
//(silver) 81. Under dome 13: rock 66 and 67, black 29, silver 80. Still beside the silver and still at
//twice the 8-ball on both, which is the whole of what the paragraph above asks for.
static const float3 StoneBody = float3(0.54, 0.51, 0.465);

//What the grains are: the pale one is quartz catching the light, the dark one mica. Asymmetric on purpose -
//a real granite's dark minerals sit further from the matrix than its light ones, and a symmetric pair reads
//as noise rather than as an aggregate.
static const float3 StoneGrainPale = float3(0.90, 0.89, 0.86);
static const float3 StoneGrainDark = float3(0.17, 0.16, 0.155);

//The three directions the grain waves run along, and the ratios between their frequencies. Irrational
//ratios so the product never settles into a repeating lattice, and none of them near an axis of the
//sphere so the grains do not agree with the mesh's own poles - the LOD ladder's coarsest spheres would
//otherwise have something to line up with.
static const float3 StoneGrainAxisA = float3(0.63, 0.49, -0.60);
static const float3 StoneGrainAxisB = float3(-0.42, 0.77, 0.48);
static const float3 StoneGrainAxisC = float3(0.51, -0.38, 0.77);
static const float3 StoneGrainRatio = float3(1.0, 1.137, 0.874);

//How hard the fleck field is squeezed before it counts as a grain. The product of three waves spends most
//of its range near zero, so this is what turns "mostly nothing with occasional peaks" into discrete
//grains with clean matrix between them rather than a continuous haze.
static const float StoneGrainGate = 2.6;
static const float StoneGrainSharpness = 1.7;

//The coarse lumps, as a fraction of the relief: how much of the roughness is broad shaping (a chipped
//boulder) against fine pitting (weathering). Both are needed - fine alone reads as sandpaper on a
//perfect sphere, and that is a texture rather than a rock.
static const float StoneLumpShare = 0.62;

//How far the lump field displaces the phase the grain is read at, in radians. Enough to move a grain by
//most of its own width, which is what it takes to destroy the lattice; much more and the grains smear
//into streaks and the aggregate turns into a marble's veining.
static const float StoneGrainWarp = 2.2;

//How much the lumps darken the body between the grains. Small - it is there so a rock is not one flat
//grey, and anything more competes with the grain itself. Free: the field is evaluated once for all three
//of its jobs.
static const float StoneMottle = 0.35;

//Matte, and the three figures do not all say the same thing. The broad direct highlight is cut hard and
//SMOOTHNESS is cut hardest: short of 1 it stops Fresnel raising a mirror rim along the silhouette, which
//on a sphere is most of what can be seen of one and is exactly what the polished marble is FOR.
//
//⚠ The ENVIRONMENT is NOT cut with them, and the first build cut it to 0.20 as though "matte" meant "sees
//no sky". It does not: at this smoothness the reflection is blurred all the way to the sky's average, so
//what this term delivers is diffuse SKYLIGHT, which is most of what lights a real rock outdoors. Cutting
//it made the rock the only ball in the game that ignored the dome, and it read near-black in the map
//editor while every colour around it was washed with sky. At 0.55 the editor and the Testbed agree on it
//(117/100/81 against 106/94/79, same level file and the same D1 view). Blurred, yes; blind, no.
static const float StoneBroadHighlight = 0.22;
static const float StoneEnvironment = 0.55;
static const float StoneSmoothness = 0.25;

//===================================================================================================
//THE SILHOUETTE IS NOT A CIRCLE ANY MORE (#340), AND THAT IS A RULING RATHER THAN A TUNING. The header
//above says "nothing here builds geometry" and point 1 rests the whole read on shading a perfect sphere;
//that stood as a rule until the owner's playtest asked for the opposite in as many words - a rock may be
//visibly less round than the other balls, sharper, more like an actual chunk of stone - and confirmed it
//when asked. #271 decided the same question the other way for the GEM, and both rulings are right for
//their own style: a gem is CUT, so a polygonal outline on it is a defect in the cut, while a rock is
//BROKEN, and an outline that is not round is the single clearest thing that can be said about one.
//
//SO THE VERTEX IS MOVED, and this is the only ball technique in the file with a vertex shader of its own.
//Three things follow from that and each is a decision:
//
//  - IT CARVES INWARD ONLY. The radius runs from (1 - depth) up to 1 and never past it, because the
//    lattice packs these balls at exactly two radii apart: a rock that grew would push into the cell its
//    neighbour occupies, and a cluster would knit into itself. Carving also happens to be what makes a
//    rock: stone is what is LEFT after pieces came off.
//  - THE NORMAL IS ANALYTIC, not the sphere's. Once a radius varies over the surface the direction is no
//    longer the normal, and reusing it would light a carved ball exactly like a round one - the whole
//    change would then be a silhouette and nothing else. For a radial surface r(d)*d the normal is
//    d - (tangential gradient of r)/r, which is closed form here because the field is a sum of sines: its
//    gradient is the same sum with cos and a factor of the wave vector. That is why this field is written
//    out separately from StoneLumps rather than reusing it - StoneLumps is rectified (abs), and abs has
//    no gradient at its own zeros.
//  - IT IS COARSER THAN EVERYTHING ELSE ON THE BALL. Frequencies of 1.7 to 5.7 against the lumps' 3.5 to
//    47, so the two tiers do not double-count: this one is the SHAPE and what the pixel shader perturbs
//    on top of it is the SURFACE.
//
//⚠ Every rock carries the SAME carving, because the instance stream has no free channel for a per-ball
//seed (world matrix, occlusion, dissolve and ripple fill it) and the only per-instance value available -
//the ball's position - moves, which would make the shape swim as a rock fell. What saves it is that the
//field is in OBJECT space: one stone shown at a hundred orientations is a hundred rocks, which is what a
//pile of one quarry's rubble looks like anyway.
//
//THAT ORIENTATION IS SUPPLIED DELIBERATELY, AND UNTIL #356 IT WAS NOT. This block used to say "the physics
//gives every rock its own orientation" and that was measured false: BallsConstraintsBuilder creates every
//body at identity, and a lattice hanging from a ceiling never turns one. On Cairn - 209 rocks, the densest
//of the five stone levels - the mean tilt from identity over sixty seconds was 0.05 deg and the largest
//0.28 deg, only the first second after the build reaching 6.3 deg as the constraints took up. So a heap was
//one solid at one angle, down to the same pale swirl in the same place on every rock in it. RockTurns (C#
//side) now folds a fixed per-cell turn into the world matrix that is ALREADY in the stream, which makes the
//paragraph above true instead of hoped for - and costs nothing, so the fifth instance element this block
//used to name as the fix is not needed and would buy nothing over it.
//===================================================================================================

//How deep the carving cuts, as a fraction of the radius. The one figure of it the C# side states, because
//it is the whole of how un-round a rock is and it is bounded by the lattice: at 0.2 a ball's narrowest
//axis is four fifths of its cell, which is as far as it can go before the gaps between rocks start to
//read as holes in the cluster.
float StoneShapeDepth;

//How sharply the carving bites. The field is a sum of sines, so raw it is a gentle undulation and the
//first build of this came out a smooth POTATO - rounded, obviously not spherical, and not what was asked
//for, which was sharper and more like an actual chunk. A power over the carve fraction leaves most of the
//surface near full radius and drives the rest deep and narrow, which turns undulation into GOUGES: stone
//is what is left after pieces came off, and pieces come off in bites rather than in ripples.
static const float StoneShapePower = 1.8;

//The four waves the carving is built from. Low frequencies, irrational-ish ratios, none near an axis of
//the sphere - the same three rules every field in this file follows, for the same three reasons.
static const float4 StoneShapeFrequency = float4(1.7, 2.6, 3.9, 5.7);
static const float4 StoneShapeWeight = float4(0.40, 0.28, 0.19, 0.13);
static const float3 StoneShapeAxisA = float3(0.68, 0.47, -0.56);
static const float3 StoneShapeAxisB = float3(-0.39, 0.81, 0.44);
static const float3 StoneShapeAxisC = float3(0.53, -0.35, 0.77);
static const float3 StoneShapeAxisD = float3(-0.77, -0.33, 0.55);

//The carving field and its gradient in one pass, because the vertex shader needs both and the sines are
//the expensive half. Returns roughly -1..1; the gradient is with respect to the direction itself, so the
//caller has to take its tangential part.
float StoneShape(float3 direction, out float3 gradient)
{
    float4 phase = float4(dot(direction, StoneShapeAxisA), dot(direction, StoneShapeAxisB),
        dot(direction, StoneShapeAxisC), dot(direction, StoneShapeAxisD)) * StoneShapeFrequency;

    float4 wave = sin(phase);
    float4 slope = cos(phase) * StoneShapeFrequency * StoneShapeWeight;

    gradient = slope.x * StoneShapeAxisA + slope.y * StoneShapeAxisB
        + slope.z * StoneShapeAxisC + slope.w * StoneShapeAxisD;

    return dot(wave, StoneShapeWeight);
}

//The rock's own vertex shader: PatternVS with the carving in it. Everything else about the output is
//PatternVS's, and deliberately so - the two must not drift apart, since every contract point the pixel
//shader answers is read off these same fields.
PatternVertexShaderOutput StoneVS(VertexShaderInput input, InstanceInput instance)
{
    PatternVertexShaderOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

    float4 bonePosition = mul(input.Position, Bone);

    float radius = max(length(bonePosition.xyz), 1e-5);
    float3 direction = bonePosition.xyz / radius;

    float3 gradient;
    float shape = StoneShape(direction, gradient);

    //Inward only: 1 at the shallowest, 1 - depth at the deepest. Through the power, so it gouges rather
    //than undulates - see StoneShapePower.
    float bite = saturate(0.5 + 0.5 * shape);
    float bitten = pow(bite, StoneShapePower);
    float carve = 1 - StoneShapeDepth * bitten;

    //n proportional to d - (tangential gradient of r)/r, with r = radius * carve. The carve's own
    //derivative supplies the minus sign, which is why this reads as a plus; the power supplies the chain
    //rule factor beside it, and dropping THAT would light a gouged ball as though it were rippled.
    float slope = StoneShapeDepth * 0.5 * StoneShapePower * pow(max(bite, 1e-4), StoneShapePower - 1);

    float3 tangential = gradient - dot(gradient, direction) * direction;
    float3 objectNormal = normalize(direction + (slope / carve) * tangential);

    float3 carved = direction * (radius * carve);
    float4 worldPosition = mul(float4(carved, 1), world);

    output.ObjectPosition = carved;
    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = mul(mul(float4(objectNormal, 0), Bone), world).xyz;
    output.OcclusionData = instance.Custom;
    output.Dissolve = instance.Dissolve;
    output.Ripple = instance.Ripple;

    return output;
}

//The coarse shaping field: rectified octaves, the marble's construction at a quarter of its frequency.
//Amplitudes sum to one so StoneRoughness stays the peak height in world units.
//
//SIX OCTAVES SINCE #340, WHERE THERE WERE FOUR, and the reason is the owner's "the surface's resolution
//is too low". Four octaves an octave apart do not describe a surface - they interfere into a regular
//weave, which is the trap the scene relief's own header records at length (see SurfaceReliefWorld, and
//why that one uses seven). The two added are the FINEST, so they cost nothing at distance: each octave
//band-limits against its own wavelength, so they are present exactly while a pixel can hold them and
//gone silently when it cannot, and what carries a rock across the arena is still the coarse end.
float StoneLumps(float3 direction, float footprint)
{
    return 0.36 * abs(ReliefOctave(direction, float3(0.71, 0.52, -0.47), 3.5, footprint))
        + 0.24 * abs(ReliefOctave(direction, float3(-0.36, 0.83, 0.42), 6.0, footprint))
        + 0.16 * abs(ReliefOctave(direction, float3(0.55, -0.44, 0.71), 11.0, footprint))
        + 0.11 * abs(ReliefOctave(direction, float3(-0.82, -0.31, 0.48), 19.0, footprint))
        + 0.08 * abs(ReliefOctave(direction, float3(0.29, -0.86, -0.42), 31.0, footprint))
        + 0.05 * abs(ReliefOctave(direction, float3(-0.64, 0.31, -0.70), 49.0, footprint));
}

float4 StonePS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //The coarse shaping field, first, because the grain is warped by it below. It does triple duty: it
    //shapes the surface, it mottles the body, and it breaks the grain's lattice - and it is evaluated once.
    float lumps = StoneLumps(direction, footprint);

    //Contract point 6: the grain is in OBJECT space, so it turns with the ball. It is a stronger rotation
    //cue than the gores it replaces, because it is aperiodic - a rolling beach ball shows the same five
    //stripes coming round again, and there is no such thing as coming round again on this one.
    float3 grainFrequency = StoneGrainFrequency * StoneGrainRatio;

    //⚠ WARPED BY THE LUMPS, and without this the ball is a GOLF BALL. Three pure waves multiplied give a
    //regular three-dimensional lattice of blobs, and a lattice of identical dimples in even rows is exactly
    //what a golf ball is - it was the first thing wrong with this style after the value. Displacing the
    //phase each wave is read at, by a field an octave or two coarser, is the marble's own construction for
    //its veins (warp the INPUT, not the output) and it costs nothing here because the field is already in
    //hand. What comes out is granite's actual figure: patches of coarser and finer grain, none of them
    //lining up with the next.
    float warp = (lumps - 0.5) * StoneGrainWarp;

    //⚠ BAND-LIMITED ONCE, ON THE PRODUCT, and ReliefOctave is deliberately NOT used to build it. Every other
    //figure in this file is a SUM of octaves, where per-octave attenuation is the right and only construction
    //(see ReliefOctave's own comment). A product is not a sum: three attenuated factors multiply their
    //attenuations, so a field that should fade to 45 % at a given distance fades to 45³ = 9 % instead and the
    //grain is simply gone one ball-width from the camera. Measured, not reasoned about - the first build of
    //this style drew no grain at all at play distance and this was why.
    //
    //What the one factor is measured against is the SUM of the three frequencies, because that is where a
    //product's finest content actually lies: sin(a)·sin(b) carries a+b as well as a-b.
    float grainBandLimit = saturate(1 - footprint * (grainFrequency.x + grainFrequency.y + grainFrequency.z)
        / 3.14159265);

    float fleck = sin(dot(direction, StoneGrainAxisA) * grainFrequency.x + warp)
        * sin(dot(direction, StoneGrainAxisB) * grainFrequency.y - warp * 1.31)
        * sin(dot(direction, StoneGrainAxisC) * grainFrequency.z + warp * 0.77)
        * grainBandLimit;

    //Two sides of one field: the peaks above zero are the pale mineral, the troughs below it the dark
    //one. Reading both off the SAME product is what keeps them interlocked the way an aggregate's
    //grains are, instead of two speckle patterns laid over each other.
    float pale = pow(saturate(fleck * StoneGrainGate), StoneGrainSharpness);
    float dark = pow(saturate(-fleck * StoneGrainGate), StoneGrainSharpness);

    float3 color = SrgbToLinear(StoneBody) * (1 - StoneMottle * lumps);

    color = lerp(color, SrgbToLinear(StoneGrainPale), pale * StoneGrainContrast);
    color = lerp(color, SrgbToLinear(StoneGrainDark), dark * StoneGrainContrast);

    //The surface. Lumps for the shaping and the fleck field for the pitting, in one height field so a single
    //perturbation covers both - the vinyl skin's construction, at several times its amplitude. The fleck is
    //signed here rather than split into its two minerals: a pit and a raised grain are both real, and the
    //colour above already decided which mineral is which.
    //
    //The LUMPS are what survives distance. They run at 3.5 to 19 waves where the grain runs at three times
    //that, so at the stand-off a level is played from the grain has faded out and this has not - which is the
    //whole reason the roughness is not left to the speckle. A rock has to still be a rock across the arena.
    float height = (StoneLumpShare * lumps + (1 - StoneLumpShare) * fleck) * StoneRoughness;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);

    //Matte, and every figure of it stated rather than inherited: this is the only ball in the set that
    //must not pick the dome up, and a smoothness left at 1 would put a mirror rim round it.
    SurfaceSpecular surface;
    surface.Highlight = StoneBroadHighlight;
    surface.Environment = StoneEnvironment;
    surface.Smoothness = StoneSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2, answered with ZERO and this is the one technique that may. BallEmission is still
    //called rather than dropped, and with the stone's own colour: EmissiveStrength is a renderer uniform,
    //so a rock plane drawn with it left at some other style's figure would otherwise glow grey, and this
    //way the answer is arithmetic instead of a promise about a caller. See the header.
    shaded.rgb += BallEmission(SrgbToLinear(StoneBody), input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately. A rock is part of the
    //cluster the wave walks through, and it has to carry both: the landing wave, because a wave that
    //stops at a rock tells the player the rock is not connected when it is, and the ceiling's ALARM,
    //because every ball in that wave has to say the same thing - a field of rocks staying calm while the
    //glass comes down would be the one place the alarm could be missed.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //The flare is white here with none of the ball's hue carried into it, and RippleWhiten is not
        //consulted: that term exists to lift the channels a COLOURED ball is missing, and a neutral grey
        //has none missing. What it means for a rock is simply that it lights up.
        float3 lit = shaded.rgb + RippleStrength * amount;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelStone
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL StoneVS();
        PixelShader = compile PS_SHADERMODEL StonePS();
    }
};

//===================================================================================================
//CLEAR GLASS (#325) — the twelfth ball technique, and the second of the two that belong to a KIND
//rather than to a style. The stone above draws the ball that can never be matched; this draws the ball
//that has NO COLOUR YET: BallKind.Transparent, hanging in the map until a shot lands beside it and
//gives it one.
//
//WHAT IT HAS TO READ AS IS AN ABSENCE, and that is the whole design problem the issue set. The game
//already has a transparent ball material — the bubble — so "transparent" was taken; on a Bubble level
//every ball is a see-through film and this kind would have had nothing left to say. What it says
//instead is that it is EMPTY: no dye anywhere in it, no gores, no film colour, no interference
//rainbow, and a body that hides almost nothing. Beside a dyed film it reads as the one shell nobody
//has coloured in, which is exactly what it is.
//
//So this is not "BubblePS with the tint set to white". A white film still carries the film — the
//iridescence, the drainage, the dyed transmission — and the whole of what this technique is, is those
//terms being ABSENT. What is left is a rim, a highlight, a mould seam and the sky bent through the
//middle, which is what an undyed glass sphere actually looks like.
//
//THE SIX-POINT CONTRACT, in the order the header states it:
//  1. The dissolve clip, both signs, over DissolvePixelSize cells — and on this technique it is also
//     LOAD-BEARING RATHER THAN COSMETIC: the clear-to-colour crossing draws this shell at +d and the
//     new colour's own technique at -d, so the two partition the ball's pixels between them and the
//     glass turns into a coloured ball with no blend, no sorting and no third program. See
//     BallDrawFrame.Route, which is the other half of it.
//  2. The heartbeat, through BallEmission like everything else — at HOLLOW_EMISSION and PulseDepth 0,
//     so it is a floor rather than a beat. A colourless ball pulsing would be announcing a colour it
//     does not have.
//  3. The ripple in both meanings. The landing wave is white here (there is no own colour to carry
//     towards white) and the alarm is the flat alarm colour every ball wears; both raise the ALPHA as
//     well, the film's own lesson — a wave through a transparent ball that only changed its colour
//     would be invisible against open sky.
//  4. SurfaceOcclusion, including #303's burial depth, which arrives inside it.
//  5. ApplySeaSubmerge then ApplyKillPlaneFade on the way out.
//  6. A rotation cue in OBJECT space, and this is the one that had to be designed rather than
//     inherited: everything else about clear glass is a function of the eye and the sky, so without
//     this a spinning glass ball is a still one — the mirror trap the header names, arriving on the
//     one material that is mostly not there. The answer is the seam a cast glass marble carries, at
//     HollowSeamFrequency: object-space, so it turns with the ball, and diegetic rather than drawn on.
//===================================================================================================

//How many mould seams run over the shell — the rotation cue's dial. See the renderer's own property.
float HollowSeamFrequency;

//How wide a seam is, as a fraction of the band between two of them, and how much it bends the surface.
//A cast seam is a RIDGE: it catches the highlight and refracts what is behind it, which is what makes
//it visible on a body that is otherwise almost nothing.
static const float HollowSeamWidth = 0.055;
static const float HollowSeamRelief = 0.5;

//How much the glass bends what is behind it, as a fraction of the screen. Small: a solid glass sphere
//is a lens and would invert the world, which is a different object from the thin shell this is - and
//an inverted image inside every ball of a cluster is a picture nobody can read. What this does is
//pull the background sideways along the rim, which is the cue that says "something is there".
static const float HollowRefraction = 0.35;

//The rim's own power and how hard it burns. Higher than the film's, and this is the read: an undyed
//shell has nothing BUT its edge, so the edge has to be sharp and bright enough to draw the sphere on
//its own.
static const float HollowRimPower = 2.6;
static const float HollowRimStrength = 0.55;

float4 HollowPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Point 1, character for character what every other ball technique does. On this one it is also the
    //crossing itself - see the technique's header.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    //Turned to face the eye, so both walls of the shell go through one piece of arithmetic - the film's
    //own trick, and the reason this technique can share its two-pass draw.
    float3 normal = normalize(input.WorldNormal) * BubbleShell;
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    //POINT 6, THE MOULD SEAM, and it is applied to the NORMAL rather than to a colour on purpose: this
    //material has no colour to draw a figure in, and a figure in the normal is shading, which cannot be
    //swamped by shading (#311's rule, arriving on the one material with nothing else to say). Two
    //half-mould seams meet at the poles of the ball's own object space, so the ridge turns with it.
    //
    //Band-limited against the pixel footprint like every other ball figure, or a distant glass ball's
    //seam aliases into a crawling sparkle - and this one is thin, so it would be the first to go.
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;
    float seamPhase = frac(atan2(direction.z, direction.x) / 6.2831853 * HollowSeamFrequency);
    float seamDistance = abs(seamPhase - 0.5) * 2.0;
    float seam = saturate((seamDistance - (1.0 - HollowSeamWidth)) / max(HollowSeamWidth, 1e-4))
        * saturate(1 - footprint * HollowSeamFrequency / 3.14159265);

    //The ridge tilts the normal along the seam's own direction. A height field rather than a modelled
    //bulge, exactly as the gem's facets are, because a pixel shader cannot move a vertex and does not
    //have to: PerturbNormalFromHeight gives the mapping for free.
    normal = normalize(PerturbNormalFromHeight(normal, input.WorldPosition, seam * HollowSeamRelief));

    float facing = saturate(dot(normal, eyeVector));

    //Point 4. The burial depth of #303 rides inside this, so a glass ball deep in the pile is as dark
    //as everything around it - which matters more here than anywhere: an unoccluded clear shell in the
    //middle of a cluster would be a hole in the picture.
    float occlusion = SurfaceOcclusion(input.WorldPosition, normal, input.OcclusionData);

    //WHAT IT REFLECTS. A dielectric like the film, so 4 % head-on rising to a full mirror at the rim -
    //and no dye multiplying it, which is the whole difference. This is the term that puts the sky, the
    //city or the cavern's crystals into the ball and is most of what says "glass" rather than "hole".
    float3 fresnel = FresnelSchlick(DielectricF0.xxx, facing, 1.0);
    float3 reflected = SkyRadiance(reflect(-eyeVector, normal)) * fresnel * SpecularAmbientStrength * occlusion;

    //The lamps' pinpoints, and they are doing a job here they only decorate elsewhere: on a body this
    //empty the highlight is one of the three things naming the sphere.
    float3 hotspot = 0;
    AddBubbleHighlight(normalize(KeyLightPosition - input.WorldPosition),
        DirLight0SpecularColor * CloudSunlight(input.WorldPosition, SunDirection), normal, eyeVector, hotspot);
    AddBubbleHighlight(-DirLight1Direction, DirLight1SpecularColor, normal, eyeVector, hotspot);
    AddBubbleHighlight(-DirLight2Direction, DirLight2SpecularColor, normal, eyeVector, hotspot);
    hotspot *= DirLightStrength * occlusion;

    //And the scene's own lamps.
    float3 lampWash = 0;
    AddSceneLights(input.WorldPosition, normal, eyeVector, lampWash, hotspot);

    //WHAT IT HIDES. Almost nothing face-on (HOLLOW_BODY_OPACITY) and much more along the rim, where the
    //eye looks the long way through the glass. The far wall is worth less than the near one for the
    //film's own reason.
    float rim = pow(1.0 - facing, HollowRimPower);
    float wall = BubbleShell > 0 ? 1.0 : BubbleInnerWall;
    float alpha = saturate(BubbleBodyOpacity + (1.0 - BubbleBodyOpacity) * rim) * wall;

    //WHAT COMES THROUGH IT, and it is NOT dyed - which is the sentence this whole technique exists to
    //be able to write. Taken as a brightness the way the film's transmission is (the same departure,
    //the same reason: a backdrop's hue may not get a veto over what a ball looks like), so a clear ball
    //carries the light behind it without carrying its colour.
    //
    //The refraction offset is what makes it read as a lens rather than as a grey wash: the sky is
    //sampled along a direction bent by the surface normal, so the background slides as the ball turns
    //and the seam above bends it visibly where it crosses.
    float3 bent = refract(-eyeVector, normal, 1.0 - HollowRefraction);
    float skyThrough = dot(SkyRadiance(any(bent) ? bent : -eyeVector), float3(0.2126, 0.7152, 0.0722));
    float lampThrough = dot(lampWash, float3(0.2126, 0.7152, 0.0722));

    //Times the alpha for the film's own reason - what a wall transmits is what it took out of the
    //picture behind it - so a shell that hides nothing tints nothing and cannot fog the open sky.
    float3 through = (skyThrough * alpha + lampThrough) * occlusion;

    //AND THE EDGE, which on a colourless ball is the whole silhouette. White by construction: there is
    //no own colour to carry towards white, which is what the film does, and that is the point.
    float3 rimGlow = float3(1, 1, 1) * (rim * HollowRimStrength * wall * occlusion);

    //Point 2. White, faint, and at PulseDepth zero from the draw - a floor, never a beat.
    float3 emitted = BallEmission(float3(1, 1, 1), input.WorldPosition, occlusion) * wall;

    float4 shaded = float4(reflected + hotspot + through + emitted + rimGlow, alpha);

    //The pinpoint closes the glass under itself, as it does the film's.
    shaded.a = saturate(shaded.a + max(hotspot.r, max(hotspot.g, hotspot.b)) * BubbleHighlightOpacity * wall);

    //Point 3, in both meanings, and both raising the alpha - a transparent ball that only changed
    //colour would carry a wave nobody can see against the sky.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //The landing wave has no own colour to whiten, so it IS white; the alarm is the flat colour
        //every ball in that wave wears whatever it is made of.
        float3 lit = shaded.rgb + RippleStrength * amount * wall;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness * wall, amount * RippleAlarmCoverage);

        //A select and not an if: the sign varies per instance and a branch would diverge in one draw.
        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
        shaded.a = saturate(shaded.a + amount * wall * (input.Ripple < 0 ? RippleAlarmCoverage : 0.5));
    }

    //Point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelHollow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL HollowPS();
    }
};

//===================================================================================================
//THE LIVE BOMB (#326) — the thirteenth ball technique, and the third of the three that belong to a KIND
//rather than to a style. The stone draws the ball that can never be matched and the clear glass the ball
//that has no colour yet; this draws the ball that is about to take a hole out of the cluster.
//
//IT HAS TO READ AS ARMED BEFORE IT IS HIT, at play distance, on all ten materials — a bomb the player
//does not notice until it goes off is a bomb that feels like a bug.
//
//⚠ THAT BAR WAS MISSED AT PLAY DISTANCE UNTIL THE OWNER LOOKED AT IT, and what was wrong was the size of
//the figure rather than anything about the light. Their words: "the red lines are really too narrow, so
//from a distance the bomb does not look like a bomb - the red blinking is not visible". Three things were
//wrong at once and all three are fixed below:
//
//  a. THE GROOVES WERE TOO NARROW AND TOO SHARP (0.22 at a sharpness of 2.4). The comment that stood on
//     them argued for narrow, because the charge has to read as coming out of a JOIN - true of a bomb held
//     at arm's length, and wrong about a ball two dozen pixels across that has to say ARMED before any
//     join can be resolved at all.
//  b. THE BAND LIMIT FADED THE PATTERN OUT SIX TIMES TOO EARLY (a factor of 2.0 on the band count), so the
//     casing dissolved into a flat glow while its bands were still several pixels wide. See its own note.
//  c. THE BEAT WAS TOO FAST TO BE SEEN. Heartbeat's lit window is a fixed FRACTION of its cycle, so a
//     faster beat blinks SHORTER, not more: at 2.6 the flash was about 30 ms. See BOMB_PULSE_SPEED.
//
//Measured after, on the hanging bomb of Bombs.json through a fixed camera at the stand-off a level is
//played from, eight captures across one cycle: the casing runs 106 to 151 codes of red - a 1.43x swing
//that several of the eight caught, where before the same sampling caught the flash once in six.
//
//⚠ AND IT STILL READ AS THE WRONG COLOUR: "a dark banded casing whose bands go from dim brown to bright
//ORANGE and back" is what that same session wrote down, and the owner's verdict on it was that a bomb
//blinks red. That is #341 and it is a hue fault only - the size of the figure, the band limit and the beat
//are all as this section leaves them, and the red channel measures identical before and after the fix. See
//BombCharge, which is where the whole of it lives.
//
//⚠ AND ONE MEASURING NOTE THAT INVALIDATES PART OF THE SWEEP ABOVE: this beat CANNOT BE PHASE-SWEPT WITH
//F5 HELD ON. Freezing the simulation is the standing trick for stopping the cluster swaying between
//captures, and #326's rig used it - but it pins the pulse clock too, so a settle sweep then photographs one
//phase over and over. Measured during #341: twenty captures across a cycle WITH F5 spanned 109 to 115
//codes and read as a beat that had collapsed; twelve captures WITHOUT it, same build, same camera, spanned
//96 to 142. The cluster hangs still enough by nine seconds that the sway costs nothing on a ball this size.
//
//⚠ AND A PROCESS WARNING WORTH MORE THAN ANY OF THE ABOVE. Three rounds of "measurements" before this one
//were taken against a Testbed that was NOT running the shader being edited: the captures come from
//bin\net10.0-windows (the Debug output), the builds had been made with -c Release, and MGCB additionally
//SKIPS an .fx whose .xnb is newer and then copies nothing. Every conclusion drawn from those rounds was
//withdrawn - including one written into five files. Before believing any capture of a shader change, check
//that bin\<tfm>\Content\Shaders\InstancedModel.xnb is NEWER than the .fx. A green-tinted constant is the
//two-minute way to prove the pixels on screen came from the file on disk.
//
//What makes it read as a bomb, in the order the eye picks it up:
//  1. IT IS BANDED. The casing is cut into latitude segments by deep grooves, evenly spaced in ANGLE
//     (through acos) rather than in height, or the bands would bunch at the poles and read as a wound
//     ball rather than a cast shell. Nothing else in this file is banded that way: the vinyl's gores are
//     meridians, the wool's bands lie at changing angles, and the marble's veins are not bands at all.
//  2. THE GROOVES ARE LIT FROM INSIDE. The charge is in the seams, not on the surface — a dark shell with
//     light coming out of its joins is the one figure that says "there is something in there", and it is
//     also what lets the pulse be visible without the whole ball flashing, which at this depth and speed
//     would strobe.
//  3. THE CASING IS DARK AND SLIGHTLY WARM. Dark so the charge has something to be bright against, and
//     warm so it is not the 8-ball — Type8 is the one colour a near-black ball can be confused with, and
//     the rock's own header records the same trap from the grey end.
//
//⚠ THE CHARGE COLOUR IS A CONSTANT AND THE TINT IS IGNORED, which is the rule the stone states first and
//the reason this is a kind rather than a style: a bomb wearing one of the thirteen is a lie the player
//acts on — they would aim that colour at it and it would not match. It is a bomb on a bubble level and on
//a lava level alike, because "that one is different" has to survive all ten materials or it is not a
//signal.
//
//THE SIX-POINT CONTRACT, in the order the ball-technique header states it:
//  1. The dissolve clip, both signs, first and branchless.
//  2. The heartbeat through BallEmission — and here it is the WHOLE READ rather than a floor, carried by
//     the charge colour scaled by the seam mask, so the light comes out of the grooves and not the shell.
//  3. The ripple in both meanings. The landing wave is warm-white (the casing has no hue of its own worth
//     carrying) and the alarm is the flat alarm colour every ball wears — a field of bombs staying calm
//     while the glass comes down would be the one place the alarm could be missed.
//  4. SurfaceOcclusion, including #303's burial depth.
//  5. ApplySeaSubmerge then ApplyKillPlaneFade on the way out.
//  6. A rotation cue in OBJECT space, and the bands are it: they turn with the ball, so a spinning bomb is
//     visibly spinning. It is the one thing a smooth dark sphere cannot say for itself.
//===================================================================================================

//How many bands the casing is cut into from pole to pole. FIVE since the owner looked at it: six thin
//bands is a thread, and a thread reads as a screw rather than as a bomb. Fewer bands is half of what makes
//each glowing line bigger; the other half is the width below.
static const float BombBandCount = 5.0;

//How much of a band's width the glowing groove takes, and how sharply it cuts.
//
//⚠ THESE TWO WERE 0.22 AND 2.4, AND THAT IS THE FAULT THE OWNER NAMED: "the red lines are really too
//narrow, so from a distance the bomb does not look like a bomb - the red blinking is not visible". The
//comment that stood here argued for narrow ("the charge has to read as coming out of a JOIN, and a wide
//groove is a stripe painted on"), and that argument is right about a bomb held at arm's length and wrong
//about the object this actually is: a ball a couple of dozen pixels across, which has to say ARMED before
//the player can see any join at all. A join nobody can resolve is not a join, it is a missing signal.
//
//Both halves had to move, and the second is the one that is easy to miss. The WIDTH is how far the mask
//reaches from a groove's centre; the SHARPNESS is a power on top of it, so a high one pinches the bright
//core back down however wide the reach is - 2.4 was throwing away most of what the width bought. Widened
//and softened together the glowing line is several times its old area, which is also the only thing that
//helps once the bands start to blur: the mask's own mean is what survives the band limit, and that mean is
//exactly what these two set.
static const float BombGrooveWidth = 0.35;
static const float BombGrooveSharpness = 1.5;

//Depth of the grooves in world units. The second largest ball figure in this file after the stone's
//roughness, because a groove that only changes colour reads as paint and this one has to read as a gap
//between two pieces of metal.
static const float BombGrooveDepth = 0.030;

//The casing, in sRGB like every other colour written here. Dark, and WARM on purpose: a neutral near-black
//ball is Type8, which is the one colour this could be confused with, and the same trap the stone records
//from the grey end. Not black either — a body at zero has nothing for the sky to sit on and the silhouette
//disappears against a dark dome.
static const float3 BombCasing = float3(0.115, 0.098, 0.092);

//And the charge that burns in the seams.
//
//⚠ IT WAS (1.0, 0.46, 0.13) AND THE OWNER'S VERDICT ON IT WAS ONE WORD: it blinks ORANGE, and it should
//blink RED. The comment that stood here defended that value with an argument rather than a measurement —
//"well past white in the red channel so it survives the tonemap as a HOT thing rather than as an orange
//one; the emission below multiplies it, so this is a direction more than a colour". The measurement that
//refutes it was standing twenty lines up this same file the whole time: the casing runs 106 to 151 codes of
//red and reads "from dim brown to bright orange and back". A casing that peaks at 151 never clips, so
//nothing is ever pushed past white and the value written here IS the colour that reaches the player, at
//full strength. An intent stated in a comment does not become true by being multiplied.
//
//⚠ AND WHAT MADE IT ORANGE WAS THE GREEN, NOT THE RED, which is why "push the red harder" could not have
//fixed it: red was already pinned at 1.0 and had nowhere to go. Hue at this end of the wheel is
//60 * (G-B) / (R-B), so with red pinned the only lever left is green, and 0.46 of it is nearly a fifth of
//the red in linear light. Blue is not taken to zero along with it, on purpose: a channel pinned at zero
//makes the deepest part of the charge a one-channel colour, which aliases hard along a groove's edge and
//reads as a decal rather than as light.
//
//MEASURED BOTH WAYS IN ONE SESSION, twelve phases of one beat through the fixed camera below (meadow,
//sky 1, nopost, nooverc, ssaa 2, campos=0,4,30 camtarget=0,5.5,0, the top-right bomb of Bombs.json, mean
//over a disc of radius 8):
//
//                       casing floor        casing on the beat      hue
//    orange (0.46 G)    96 / 39 / 17        142 / 60 / 19           17 to 20 degrees
//    red    (0.15 G)    96 / 20 / 15        142 / 23 / 15           3.8 degrees, flat across the beat
//
//THE RED CHANNEL DOES NOT MOVE BY ONE CODE, at either end, and the beat's swing is 1.48x both times. Only
//green does, which is the whole of the hue and — because green carries 0.7152 of a colour's luminance
//against red's 0.2126 — also about a third of the charge's light. That loss is real and it is invisible
//here: the bomb is read by its red against a near-black casing, so what was lost is the part that was
//making it amber. See BombRestingGlow for the compensation that was tried on the strength of the luminance
//figure alone, and measured to be unnecessary.
static const float3 BombCharge = float3(1.0, 0.15, 0.05);

//A ring of studs round the casing's waist — rivets. Cheap (one more sine pair) and worth it: they are the
//only part of the figure that survives when the ball is small enough that the bands blur together, and a
//studded sphere is unmistakably a made object rather than a dark ball.
static const float BombStudCount = 12.0;
static const float BombStudSize = 0.16;
static const float BombStudDepth = 0.016;

//What the charge falls back to once the bands are under a pixel — see the note in BombPS. Well under the
//groove's own peak of 1, so a bomb at arm's length is still a dark casing with light in its joins rather
//than a glowing marble, and well over the mask's honest mean of about 0.065, so a bomb across the arena is
//a live thing rather than an 8-ball. Measured at the game camera's stand-off on Bombs.json.
static const float BombFarGlow = 0.35;

//How much of the charge burns whatever the beat is doing and whatever the ball is buried under - the floor
//the heartbeat rides on. See the note at its use for why it cannot be BallEmission's own resting term.
//Half, so a bomb at rest is unmistakably lit and a bomb on the beat is still visibly brighter: the swing
//has to survive as well as the floor, or the odd one out stops being the one that moves.
//
//⚠ THIS WAS RAISED TO 0.65 DURING #341 AND PUT BACK, and the reason is worth more than the constant. The
//red recolour above drops about a third of the charge's LUMINANCE (green carries 0.7152 of it against red's
//0.2126), so it looked obvious that the casing needed the loss handed back. It did not: measured across
//twelve phases before and after the recolour, on the same camera in the same session, the casing's floor is
//96 codes of red BOTH TIMES. Luminance fell; the red channel the bomb is actually read by did not move,
//because red was pinned at 1.0 before and after. The 10 % shortfall being compensated for was against the
//figure 106 quoted in #326's own note — measured through a different rig, in a different session, and
//therefore a citation rather than a measurement. Raising this term also costs the beat: it lifts the floor
//and the flash by the same linear amount, so the swing falls (1.48x to 1.36x at 0.65), and the swing is
//what #326 was protecting.
static const float BombRestingGlow = 0.5;

//Machined metal: a tight highlight and a real mirror of the dome, which is what separates a casing from
//the stone's matte aggregate at a glance. The environment term is what draws the sky along the silhouette
//and is most of why a dark ball is visible at all.
static const float BombHighlight = 0.55;
static const float BombEnvironment = 0.70;
static const float BombSmoothness = 0.62;

//Where the seam mask is read from: the fraction of a band, folded so 0 is the middle of a groove.
float BombSeams(float3 direction)
{
    //Even in ANGLE, not in height — the poles are where a height-banded sphere crowds its bands together.
    float latitude = acos(clamp(direction.y, -1.0, 1.0)) / 3.14159265;

    float band = frac(latitude * BombBandCount);
    float toSeam = abs(band - 0.5) * 2.0;

    return pow(saturate(1.0 - toSeam / max(BombGrooveWidth, 1e-4)), BombGrooveSharpness);
}

//The rivets, as a second mask on the same construction: a ring of them round the equator.
float BombStuds(float3 direction)
{
    float azimuth = atan2(direction.z, direction.x) / 6.28318531;
    float ring = 1.0 - saturate(abs(direction.y) / BombStudSize);
    float around = 1.0 - saturate(abs(frac(azimuth * BombStudCount) - 0.5) * 2.0 / BombStudSize);

    return saturate(ring * around);
}

float4 BombPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Band-limited on the band count, the same argument ReliefOctave makes: past the point where one band is
    //under a pixel the grooves are noise, and noise on a dark ball is what makes a cluster shimmer.
    //
    //⚠ THE FACTOR WAS 2.0 AND IT FADED THE PATTERN OUT SIX TIMES TOO EARLY, which is most of what the owner
    //was looking at when they said the bomb does not read as one from a distance. A band spans about
    //pi / BombBandCount of the surface parameter - some 0.63 radians at five bands - so the grooves only
    //start aliasing when the footprint approaches that. At 2.0 the limit reached zero at a footprint of 0.1,
    //while a band was still several pixels wide and perfectly resolvable: the casing dissolved into a flat
    //glow at exactly the range the figure was supposed to be doing its work. Half the band count puts the
    //fade where Nyquist actually is, so the bands stay drawn as long as they can be seen.
    float bandLimit = saturate(1 - footprint * BombBandCount * 0.5);

    float seam = BombSeams(direction) * bandLimit;
    float stud = BombStuds(direction) * bandLimit;

    //⚠ THE CHARGE IS NOT BAND-LIMITED WITH THEM, and the first build of this technique was: the emission
    //below was multiplied by the band-limited `seam`, so as the bands went under a pixel the glow went with
    //them and the beat — which is the WHOLE armed read — was limited out of existence at exactly the
    //distance it exists to work at. Measured on Bombs.json through the game camera: warmth on the bomb's
    //pixels fell from 168 to 65 codes of R-B against a close-up, and the ball read as a plain black sphere.
    //
    //So the charge converges to a FLOOR instead of to nothing: once the figure cannot be resolved the whole
    //casing carries the glow. That is deliberately NOT energy-conserving — the grooves are about a
    //fifteenth of the surface, so spreading their light honestly over the ball leaves it as dark as before —
    //and it is the same call the stone's own header records making, in the other direction: a rock had to be
    //given emission it does not physically have because it read as the 8-ball without it. What has to
    //survive distance is the SIGNAL, and the signal is "this one is live".
    float charge = lerp(BombFarGlow, seam, bandLimit);

    //The casing, darkened in the grooves: a joint is in shadow before it is lit from inside, and skipping
    //that made the seams read as painted-on stripes when the charge was at the bottom of its beat.
    float3 color = SrgbToLinear(BombCasing) * (1 - 0.45 * seam) * (1 + 0.35 * stud);

    //Contract point 6. Both figures cut into one height field, so a single perturbation covers them - the
    //vinyl skin's construction. The studs stand PROUD and the grooves cut IN, which is the sign difference
    //that makes them read as two different features rather than as one dented surface.
    float height = (stud * BombStudDepth - seam * BombGrooveDepth);

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);

    SurfaceSpecular surface;
    surface.Highlight = BombHighlight;
    surface.Environment = BombEnvironment;
    surface.Smoothness = BombSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2, and on this technique it is the whole read rather than a floor. The charge is
    //multiplied by the SEAM MASK, so what beats is the light coming out of the joins and not the shell -
    //at the depth and speed DrawBombs pushes, a whole ball flashing this hard would strobe. The studs take
    //a share of it too, at a fraction, so a bomb small enough that its bands have blurred out is still
    //visibly alive.
    //⚠ THE FLOOR IS ADDED HERE AND NOT THROUGH BallEmission, and that is the second measured fault of this
    //technique. BallEmission's resting half is multiplied by OCCLUSION SQUARED - #303's burial rule, so that
    //the cluster is not sitting on an emissive floor no ambient occlusion can take it below - and a bomb
    //standing inside a pile is precisely the ball that most has to be seen. Six captures through the game
    //camera at arbitrary phases read the bomb at R-B 1.9 (dead black) five times out of six, first at pulse
    //depth 1.0 and then AGAIN at 0.6, which is what proved the depth was never the dial: the resting term was
    //being occluded away whatever its size. So the charge gets a light the burial rule does not reach, and
    //the heartbeat rides on top of it - the beat is unoccluded by BallEmission's own design, which is what
    //keeps it legible in the cluster's interior.
    shaded.rgb += SrgbToLinear(BombCharge) * charge * BombRestingGlow;

    shaded.rgb += BallEmission(SrgbToLinear(BombCharge) * (charge + 0.35 * stud), input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //Warm-white rather than the casing's own hue: RippleWhiten exists to lift the channels a COLOURED
        //ball is missing, and a near-black shell is missing all of them - carrying its hue into the flare
        //would make the one ball in the wave that stays dark.
        float3 lit = shaded.rgb + RippleStrength * amount;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelBomb
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL BombPS();
    }
};

//===================================================================================================
//THE ZAP (#327) - a dark shell caged in electric arcs, for the kind that takes a whole colour off the
//field. It is the fourth technique that belongs to a BallKind rather than to a BallStyle, and the
//fourth with no type colour, on the stone's rule: a special wearing one of the thirteen is a lie the
//player acts on.
//
//⚠ ITS PROBLEM IS NOT BEING SEEN, IT IS BEING TOLD APART FROM TWO THINGS THAT ALREADY EXIST, and both
//collisions are structural rather than tonal - which is why none of the three is solved by brightness:
//
//  - THE PLASMA STYLE (#309) is already this game's crawling-filament look, and on a plasma level EVERY
//    ball is one. What separates them: a plasma ball GLOWS ALL OVER, in its own type colour, with soft
//    filaments DRIFTING through a lit body that brightens towards the middle of its disc. A zap is a
//    DARK shell in one fixed cold blue-white, and what moves on it is a handful of hard thin arcs on
//    FIXED GREAT CIRCLES that flicker on and off rather than a field that wanders. Lit body against
//    dark body, drifting against snapping, type colour against no colour.
//  - THE BOMB is the other dark special, and a player has to know which one they are landing beside
//    before they land. Its figure is LATITUDE BANDS - horizontal rings, evenly spaced in angle - and
//    it breathes slow and deep. This is OBLIQUE GREAT CIRCLES crossing each other, and it flickers
//    fast and shallow. Opposite corners of the same two dials, plus a warm dark against a cold one
//    (BasicEffectParamsProvider.Bomb against .Zap).
//  - THE VINYL'S GORES are meridians, so a pole-to-pole figure was out before it was drawn.
//
//What makes it read as a CAGE rather than as three stripes is that the circles are mutually oblique and
//therefore cross at six points, and the crossings are where the eye goes. The two POLES of the first
//axis are lit as electrodes on top, so the figure has ends as well as a middle - an arc has to come
//from somewhere.
//
//The bomb's own hard lesson applies over all of it: WHAT A SPECIAL IS READ BY AT PLAY DISTANCE IS THE
//SIZE OF THE LIT FIGURE, NOT THE AMOUNT OF LIGHT IN IT. So the arcs are wide enough to survive a ball
//two dozen pixels across, band-limited on the ice crack's rule (a limit on a thin LINE has to start
//late - see IceCrackBandLimit), and carried by a floor the burial rule cannot reach, which is the
//measured fault #326 records twice.
//
//THE SIX-POINT CONTRACT, in the ball-technique header's order:
//  1. The dissolve clip, both signs, first and branchless.
//  2. The heartbeat through BallEmission - here a FLICKER on top of a floor rather than the whole read,
//     which is the opposite of the bomb's arrangement and is why fast is right here and was wrong there.
//  3. The ripple in both meanings: cold-white for the landing wave (the shell has no hue to carry) and
//     the flat alarm colour for the ceiling.
//  4. SurfaceOcclusion, #303's burial depth included.
//  5. ApplySeaSubmerge then ApplyKillPlaneFade on the way out.
//  6. A rotation cue in OBJECT space - the arcs are fixed to the ball, so a spinning zap visibly spins.
//===================================================================================================

//The three axes the arcs ring. Mutually oblique on purpose: perpendicular ones would cross at the same
//six points as the coordinate planes and read as a wireframe globe, which is a diagram rather than a
//discharge. Unit length, and the first is also where the electrodes sit.
static const float3 ZapAxisA = float3(0.0, 1.0, 0.0);
static const float3 ZapAxisB = float3(0.87, 0.34, 0.36);
static const float3 ZapAxisC = float3(-0.42, 0.38, 0.82);

//How wide an arc's band is, in the dot product it is cut from, and the power that shapes it. WIDE and
//SOFT by the standards of a line, for the bomb's measured reason: 0.05 at a sharpness of 3 is a hairline
//that exists only in a close-up. The pair works the way the bomb's groove pair does - the width is the
//mask's reach and the sharpness is a power over it, so a high sharpness pinches the bright core back
//however far the reach goes.
static const float ZapArcWidth = 0.115;
static const float ZapArcSharpness = 2.0;

//How far an arc wanders off its own great circle, and in how many lobes. Without it the figure is three
//perfect circles, which reads as a wireframe again; with it each arc bends the way a discharge does
//while still going all the way round. The wander is driven by the OTHER axes' dots, so it costs no new
//trigonometry beyond one sine each and stays in object space with everything else (contract point 6).
static const float ZapArcWander = 0.085;
static const float ZapArcWaves = 4.3;

//The band limit, on the ice crack's rule rather than on the ordinary one: an arc is a thin line, so a
//ramp that begins at zero footprint has already halved it while it is still several pixels wide and
//perfectly sharp. Hold full strength until the line is about a pixel, then fade. See IceCrackBandLimit,
//which is the same figure for the same reason.
static const float ZapArcBandLimit = 1.6;

//The electrodes at the ends of ZapAxisA: where the dot product with it exceeds this, the shell is lit
//whatever the arcs are doing. An arc has to come FROM somewhere, and two fixed bright caps are what say
//so - they are also the part of the figure that survives longest as the ball shrinks, which is the
//bomb's rivets doing the same job by the same argument.
static const float ZapPoleStart = 0.92;
static const float ZapPolePower = 2.2;

//The shell. Dark so the arcs have something to be bright against, and COLD - the one thing that tells
//this dark ball from the bomb's warm one before either lights up. Not neutral: a near-black neutral ball
//is Type8, the trap the stone's header records from the grey end.
static const float3 ZapShell = float3(0.075, 0.086, 0.112);

//And the discharge. Blue-white rather than white: a spark is hotter than anything else in this game's
//palette and the eye reads blue as the hot end, which is also what keeps it clear of the bomb's red
//(#341) at any brightness. Blue is not taken to 1 with the rest - a fully white arc is a scratch on the
//lens rather than a current.
static const float3 ZapCharge = float3(0.62, 0.86, 1.0);

//What the arcs fall back to once they are under a pixel, as a fraction of their own peak. The bomb's
//BombFarGlow in every respect including the reason it exists: band-limiting the LIGHT along with the
//figure takes the whole "this one is live" read away at exactly the distance it is needed, so the charge
//converges to a floor rather than to nothing. Lower than the bomb's, because a cage covers far more of
//the ball than a set of grooves does and the same floor would read as a lit ball rather than a lit cage.
static const float ZapFarGlow = 0.22;

//The always-burning half of the arcs, under the flicker. The bomb's BombRestingGlow and for its reason -
//BallEmission's resting term is multiplied by occlusion SQUARED (#303's burial rule) and a special
//standing inside a pile is the one that most has to be seen, so the floor has to be a term the burial
//cannot reach.
static const float ZapRestingGlow = 0.62;

//Machined and slightly wet-looking: a tight highlight and a real mirror of the dome. Sharper than the
//bomb's, which is what makes the shell read as glass-hard rather than as cast iron.
static const float ZapHighlight = 0.62;
static const float ZapEnvironment = 0.55;
static const float ZapSmoothness = 0.74;

//How deep the arcs are cut into the shell, in world units. Shallow: an arc is LIGHT lying on a surface,
//not a groove in it, and cutting it deep is what would make it read as the bomb's seam.
static const float ZapArcDepth = 0.008;

//One arc's mask: the thin band around the great circle perpendicular to `axis`, wandering off it by
//ZapArcWander in ZapArcWaves lobes so it is a discharge rather than a wireframe.
float ZapArc(float3 direction, float3 axis, float3 along, float phase)
{
    float wander = ZapArcWander * sin(ZapArcWaves * dot(direction, along) * 3.14159265 + phase);
    float toArc = abs(dot(direction, axis) + wander);

    return pow(saturate(1.0 - toArc / max(ZapArcWidth, 1e-4)), ZapArcSharpness);
}

float4 ZapPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //An arc spans about ZapArcWidth of the surface parameter, so the limit is measured against THAT and
    //not against the whole sphere - the ice crack's rule, and the constant carries the late start.
    float arcLimit = saturate(ZapArcBandLimit - footprint / max(ZapArcWidth, 1e-3));

    //The three arcs, each wandering off a DIFFERENT partner axis so they do not bend in step, and each on
    //its own phase so the figure never lines up into symmetry.
    float arcs = max(ZapArc(direction, ZapAxisA, ZapAxisB, 0.0),
                 max(ZapArc(direction, ZapAxisB, ZapAxisC, 2.1),
                     ZapArc(direction, ZapAxisC, ZapAxisA, 4.3)));

    //MAX and not a sum, deliberately: where two arcs cross, a sum doubles the light and the crossing
    //becomes a blob twice as bright as anything else on the ball. What should read at a crossing is the
    //SHAPE - two lines meeting - and max is what keeps the lines at one brightness all the way through.
    arcs *= arcLimit;

    //The electrodes, which do not band-limit: they are caps rather than lines, so they are already
    //resolvable at any size the ball is drawn at, and they are what is left of the figure when it is not.
    float poles = pow(saturate((abs(dot(direction, ZapAxisA)) - ZapPoleStart) / (1.0 - ZapPoleStart)),
        ZapPolePower);

    //Converging to a floor rather than to nothing - BombFarGlow's argument in full, and the same shape.
    float charge = max(lerp(ZapFarGlow, arcs, arcLimit), poles);

    //The shell, darkened where an arc runs over it: a discharge scorches what it touches, and skipping
    //this made the arcs read as painted-on stripes at the bottom of the flicker.
    float3 color = SrgbToLinear(ZapShell) * (1.0 - 0.35 * arcs) * (1.0 + 0.5 * poles);

    //Contract point 6. Shallow, and the poles stand PROUD where the arcs cut IN - the sign difference is
    //what makes the two figures read as two things rather than as one dented shell.
    float height = poles * ZapArcDepth * 0.5 - arcs * ZapArcDepth;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);

    SurfaceSpecular surface;
    surface.Highlight = ZapHighlight;
    surface.Environment = ZapEnvironment;
    surface.Smoothness = ZapSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2. The floor is added HERE and not through BallEmission, which is the bomb's measured
    //fault written down twice and inherited rather than rediscovered: BallEmission's resting half is
    //multiplied by occlusion squared, and a special buried in the pile is the one that most has to be
    //seen. The flicker then rides on top, unoccluded by BallEmission's own design.
    shaded.rgb += SrgbToLinear(ZapCharge) * charge * ZapRestingGlow;

    shaded.rgb += BallEmission(SrgbToLinear(ZapCharge) * charge, input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //Cold-white rather than the shell's own hue, for the bomb's reason: RippleWhiten lifts the
        //channels a COLOURED ball is missing, and a near-black shell is missing all of them.
        float3 lit = shaded.rgb + RippleStrength * amount;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelZap
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL ZapPS();
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
