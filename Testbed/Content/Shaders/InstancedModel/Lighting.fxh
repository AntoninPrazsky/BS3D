//InstancedModel.fx: how a pixel is lit - the BasicEffect-style rig, the scene's point lights, the sea's submerge
//and the kill plane's fades, the analytic sky and the Fresnel term, occlusion, the sun's cloud and cast shadow,
//and ShadePixel, which every surface but three ball styles ends in. InstancedModel (MainPS), the plain lit
//material, is at the foot.

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
//What reaches a point of the key light past the weather and past everything the sun casts: the cloud field's
//shadow times the cast shadow map (and, inside SunShadow, the ceiling glass's own). ONE copy because until #567
//the key term of MetalPS, BubblePS and HollowPS multiplied by the cloud alone, so those three styles of ball
//took no shadow from the island, the gun, the trees or the ceiling while every other surface took it here.
//
//Uniform branch: ShadowStrength is 0 whenever no map is bound (the sea, the storm, a sky-replacing scene,
//the Low tier, a sun near the horizon), so a wavefront takes one side and nothing inside takes a derivative.
float KeySunlight(float3 worldPosition, float3 worldNormal)
{
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, worldNormal, SunDirection);

    return sunlight;
}

float4 ShadePixel(float3 worldPosition, float3 rawWorldNormal, float4 occlusionData, float4 texColor, float keyShadow, float cavity, SurfaceSpecular surface)
{
    float3 worldNormal = normalize(rawWorldNormal);
    float3 eyeVector = normalize(EyePosition - worldPosition);

    //The key light is accumulated on its own so a shadow can be applied to it without touching the fill
    //and back lights, which stand in for bounced light and are not blocked by anything. keyShadow is the
    //caller's own share of that; every caller passes 1 since the relief's self-shadow march, the one thing
    //that passed anything else, was deleted with the dead textured path (#581).
    float3 keyDiffuse = 0;
    float3 keySpecular = 0;

    //The rig arrives linear, decoded once on the CPU along with the tints applied to it
    AddLight(normalize(KeyLightPosition - worldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, worldNormal, eyeVector, keyDiffuse, keySpecular);

    //The cloud shadow rides on the same multiplier the relief's own bumps use, which is why one line here
    //puts weather across the whole scene at once - balls, city, floor and cannon all come through here.
    //
    //And the sun's CAST shadow rides the very same multiplier (#470), which is why one line here puts the
    //island's shadow on the grass, the gun's on the stone and the trees' on both. It is the sun term alone -
    //the fill and back lights stand in for bounced light and a shadow does not take that away.
    //Savanna.fx folds its own tap into the same factor, so the grass
    //beside the island and the island itself are shadowed by one rule and cannot disagree. Both are
    //KeySunlight, which the three ball styles that do not come through here call too.
    float sunlight = keyShadow * KeySunlight(worldPosition, worldNormal);

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
