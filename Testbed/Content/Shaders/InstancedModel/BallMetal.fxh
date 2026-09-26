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

    specular *= KeySunlight(input.WorldPosition, worldNormal);

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
