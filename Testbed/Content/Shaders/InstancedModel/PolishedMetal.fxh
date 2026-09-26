//InstancedModel.fx: the result page's metal cups (#602) - polished metal, the one plain material in the effect that
//SHOWS its environment instead of integrating it. Drawn by the bronze, silver and gold trophies and the metal of
//their jewellery (InstancedModelRenderer.PolishedMetal); nothing else selects it, and the crystal cup stays on
//MainPS, so its look is untouched by this file existing.
//
//WHY THE CUPS READ AS MATTE ON MainPS, measured on the result page across thirteen scenes before this was written
//(pose and orbit frozen, 1600x900): inside a fixed patch of the bowl 80-100 % of the pixels sat within 0.1 of the
//patch's median luminance on every metal, and on the bright domes its p99/p5 contrast was 1.1-2.3 (0.38-0.60 and
//1.6-3.1 for silver and gold after this). Three causes:
//
//  - THE REFLECTION WAS A RAMP. SkyRadiance is a linear lerp from the ground colour to the sky colour across the
//    whole sphere of directions - right for an AMBIENT term, which is what it is for, and the reason a polished
//    cup reflecting it had nothing sharp anywhere on it: a smooth surface of revolution mirroring a smooth gradient
//    is a smooth gradient. It is BallMetal.fxh's #336 finding again, on the plain material - a polished surface is
//    recognised by the HORIZON and the SUN it shows, and a ramp has neither. ShadePixel then lerped even that ramp
//    a further 11-16 % towards the sky's flat average (the roughness it derives from the Blinn-Phong power).
//  - THE FLOOR IS A FLAT GREY. On a dark dome (cavern, space, moon, volcano) SkyLightRig.ApplyToPresented lifts the
//    sky ambient to a luminance of 0.35 by ADDING grey, so the upper half of the ramp was ~0.35 grey whatever the
//    scene; half-mirrored through the ramp, that is a uniformly lit grey solid - plastic, not chrome. And on the
//    volcano, whose lava bounce (0.48, 0.26, 0.23) outshines the floored sky, the ramp's lower half was the
//    brighter one, so every cup there was lit from below - milky.
//  - MUCH OF WHAT WAS DRAWN WAS DIFFUSE. The tiers' dark diffuse plus the hemisphere ambient on it, with the
//    reflection held down to 0.30-0.42 "so a highlight has room" - on a metal, which has no diffuse at all. Gold's
//    diffuse decodes to 0.19 of red under a unit key against a reflection of about 0.12 in the cavern: the painted
//    body outweighed the mirror.
//
//So this technique is those three answered: the environment is SHARP (a horizon that is an edge, a sky brighter
//at the horizon than overhead, a ground with a dark band under the horizon, and the sun as a glint), the
//body has NO diffuse (the tier's SpecularColor is its F0, Schlick takes it to white at the silhouette), and the
//reflection runs at full strength, because there is nothing under it left to wash out. The ground/sky contrast
//alone is what makes chrome read in a dark scene too: the floor's flat grey stops being a lit body and becomes the
//bright half of a horizon.
//
//It is a TECHNIQUE, not a uniform in ShadePixel, for two reasons: every surface in the game ends in ShadePixel and
//sharpening SkyRadiance there would draw a horizon across the ground, the island and the gun (BallMetal.fxh's own
//argument for keeping MetalSky local); and a branch would cost every one of those passes the union of both
//register allocations. No uniform is declared here, so the $Globals layout every other program compiles against
//is unchanged (the rule in InstancedModel.fx's include note).

//How sharp the horizon is in the reflection, in the direction's y. Tighter than MetalSky's 0.075: a cup is a far
//bigger mirror on the screen than a ball, and the line sweeping across it as it turns is the whole show.
static const float PolishedHorizonEdge = 0.04;

//The sky's own gradient in the reflection: at the horizon it is this many times the dome's sky ambient, overhead
//the second figure. A real clear sky is palest at the horizon and deepest at the zenith, and a band of light
//along the horizon line is exactly what a chrome object is recognised by. Their mean over the upper hemisphere is
//0.84, so the cup mirrors the dome at about its own brightness, re-shaped rather than turned up.
static const float PolishedHorizonGain = 1.9;
static const float PolishedZenithGain = 0.55;

//How fast the horizon's brightness falls to the zenith's, in the direction's y. Small, so the bright band is a
//band and not a second ramp.
static const float PolishedHorizonFalloff = 0.22;

//The ground half. GroundColor alone is wrong in both directions: it is an ambient BOUNCE colour, brighter than the
//ground's own face (MetalSky's reason for taking it down), and on the dark domes it is near black (the floor holds
//it at a luminance of only 0.05) - and most of a cup's bowl mirrors the GROUND, because it narrows to the stem and
//the result page looks at it from a little above. Taken down as MetalSky takes it, the first cut of this drew a
//black bowl with a bright rim on every dark dome. A ground in a mirror is ground lit by the sky, so its level is
//part bounce colour and part the sky over it.
static const float PolishedGroundBounce = 0.5;
static const float PolishedGroundSkyLight = 0.25;

//And the ground has a SHAPE, or the bowl is one flat colour again: the second cut lifted it evenly and the silver
//came out as white paint. The chrome signature is a dark band right under the horizon line (the near ground seen
//edge-on, against the brightest sky there is), brightening further down into the lit ground, and falling off
//again towards the nadir (what is under the cup itself). As factors of the ground level above, and where along
//-y each one is reached.
static const float PolishedGroundBelowHorizon = 0.12;
static const float PolishedGroundLit = 1.4;
static const float PolishedGroundLitAt = 0.35;
static const float PolishedGroundNadir = 0.45;

//How much of the dome's HUE the reflection keeps. A metal multiplies what it mirrors by its own colour, and gold's
//F0 has almost no blue in it: mirroring the meadow's cyan sky whole, the gold cup came out olive green, and under the
//sea's violet one a pinkish copper - and the ground's bounce colour, the meadow's grass, put a green foot under it
//as well, when what is really under a cup presented over the arena is the island's grey stone. Real gold reads as
//gold because it is seen under warm or neutral light; half the dome's saturation keeps each scene's cast in the cup
//(a dusk dome still warms it) and leaves the alloy's hue the one that is read. The sun is added after and keeps its
//own colour whole.
static const float PolishedEnvironmentSaturation = 0.5;

//The sun in the reflection, against the key light's own specular colour so a dusk dome reflects a dusk sun: a
//tight core inside a broader lobe. The lobe is wider than MetalSky's, for the size reason above - on a cup the
//glint has to survive being a few pixels of a turning surface.
static const float PolishedSunSharpness = 1400.0;
static const float PolishedSunHaloSharpness = 60.0;
static const float PolishedSunHaloWeight = 0.18;
static const float PolishedSunGain = 12.0;

//The three-light rig's highlight on top of the reflected sun, tinted by the alloy. At 1 - unlike the metal ball's
//0.6 - because the rig's key IS the cup's studio light on a result page that stands the cup against the lens.
static const float PolishedHighlight = 1.0;

//The sharp environment a polished surface mirrors along a direction. See the constants above.
float3 PolishedSky(float3 direction)
{
    float up = saturate(direction.y);
    float3 sky = SkyColor * lerp(PolishedZenithGain, PolishedHorizonGain, exp(-up / PolishedHorizonFalloff));

    float down = saturate(-direction.y);
    float groundShape = lerp(PolishedGroundBelowHorizon, PolishedGroundLit, smoothstep(0.0, PolishedGroundLitAt, down));
    groundShape = lerp(groundShape, PolishedGroundNadir, smoothstep(PolishedGroundLitAt, 1.0, down));
    float3 ground = (GroundColor * PolishedGroundBounce + SkyColor * PolishedGroundSkyLight) * groundShape;
    float3 environment = lerp(ground, sky,
        smoothstep(-PolishedHorizonEdge, PolishedHorizonEdge, direction.y));

    environment = lerp(dot(environment, float3(0.2126, 0.7152, 0.0722)), environment, PolishedEnvironmentSaturation);

    float towards = saturate(dot(direction, SunDirection));
    float disc = pow(towards, PolishedSunSharpness) + PolishedSunHaloWeight * pow(towards, PolishedSunHaloSharpness);

    return environment + DirLight0SpecularColor * disc * PolishedSunGain;
}

float4 PolishedMetalPS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    //The alloy: SpecularColor is the tier's reflectance at normal incidence, exactly as the Metalness = 1 end of
    //ShadePixel reads it
    float3 f0 = SrgbToLinear(SpecularColor);

    //The rig and the scene's point lights, SPECULAR ONLY - a metal has no diffuse. No cast or cloud shadow on the
    //key: the cup is placed against the frame, not in the world (TrophyPodium), so a shadow the island or a cloud
    //happens to lay across the spot in front of the lens would dim the glint for a reason the frame cannot show.
    float3 diffuse = 0;
    float3 specular = 0;

    AddLight(normalize(KeyLightPosition - input.WorldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, worldNormal, eyeVector, diffuse, specular);
    AddLight(-DirLight1Direction, DirLight1DiffuseColor, DirLight1SpecularColor, worldNormal, eyeVector, diffuse, specular);
    AddLight(-DirLight2Direction, DirLight2DiffuseColor, DirLight2SpecularColor, worldNormal, eyeVector, diffuse, specular);

    specular *= DirLightStrength;

    AddSceneLights(input.WorldPosition, worldNormal, eyeVector, diffuse, specular);

    //The environment along the mirror direction, through Schlick with the alloy as F0 - so the body mirrors in the
    //metal's colour and the silhouette turns to an honest white mirror, which is the Fresnel every metal has
    float3 fresnel = FresnelSchlick(f0, dot(worldNormal, eyeVector), 1.0);
    float3 reflection = PolishedSky(reflect(-eyeVector, worldNormal)) * fresnel * SpecularAmbientStrength;

    return float4(reflection + specular * f0 * PolishedHighlight, 1.0);
}

technique InstancedPolishedMetal
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL PolishedMetalPS();
    }
};
