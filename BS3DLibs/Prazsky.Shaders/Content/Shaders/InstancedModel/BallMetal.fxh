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

//===================================================================================================
//THE BRUSH A PIXEL CANNOT HOLD STREAKS THE REFLECTION (#1216). Each brush octave fades out as the ball
//shrinks (ReliefOctave's band limit, which is what keeps the lines from boiling), and before this the
//tilt it took away went with it: the reflection of a far ball was a clean mirror with a pinpoint sun,
//while the same ball close up was brushed. Measured, three of the four octaves are gone at the play
//camera, so a metal cluster in play showed almost none of what made it brushed metal.
//
//A brushed finish seen from afar is what its scratches AVERAGE to, and a pixel over many scratches
//averages their reflections: the highlight is smeared ACROSS the scratches into a streak, which is the
//look a brushed sphere has from across a room. So the slope each octave loses to its band limit is
//handed to the reflection as a spread along the brush's own gradient (the direction the scratch tilts
//the normal), and the sun and the horizon are blurred by exactly that much. What the pixels can hold is
//drawn as lines and what they cannot is drawn as streak, and the two add up to the same surface at
//every size, because the lost and the kept variance sum to the brush's whole.
//
//The streak is per-axis Gaussian: a Phong lobe pow(cos, n) is, near its peak, a Gaussian of variance
//1 / (4n) in the slope of the normal that would reflect the light into the eye, and blurring it by the
//lost slope adds the two variances along the brush and leaves the other axis alone. Scaled by the ratio
//of the widths, so the streak carries the energy the pinpoint did instead of brightening as it grows.
//===================================================================================================

//A Phong lobe of exponent `power`, blurred by `spread` (a slope variance) along the axis whose slope is
//`along`; `across` is the slope on the other axis. Equal to exp(-2 power theta^2), which is
//pow(cos(2 theta), power) near its peak, when nothing is spread.
float MetalStreakLobe(float along, float across, float power, float spread)
{
    float own = 0.25 / power;
    float wide = own + spread;

    return sqrt(own / wide) * exp(-0.5 * (along * along / wide + across * across / own));
}

//MetalSky for the brushed ball: the sky with its horizon blurred by `horizonSpread` (a deviation in the
//reflected direction's y) and the sun as two streak lobes. `along` and `across` are the half vector's
//slopes off the normal along the brush's gradient and across it, and `spread` the lost slope variance.
float3 MetalSkyBrushed(float3 direction, float along, float across, float spread, float horizonSpread)
{
    //A smoothstep from -e to e is close to a Gaussian edge of deviation e / 2.3, so the two widths add
    //in quadrature there
    float edge = sqrt(MetalHorizonEdge * MetalHorizonEdge + 5.29 * horizonSpread * horizonSpread);

    float3 sky = lerp(GroundColor * MetalGroundDarkening, SkyColor, smoothstep(-edge, edge, direction.y));

    float disc = MetalStreakLobe(along, across, MetalSunSharpness, spread)
        + MetalSunHaloWeight * MetalStreakLobe(along, across, MetalSunHaloSharpness, spread);

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

//The metal's whole shading, with the colour cue's switch (#737) passed as a literal, as PatternShade takes it:
//MetalPS below compiles to what it was before the cue existed.
float4 MetalShade(PatternVertexShaderOutput input, bool motifCue, ColourCuePaint cue)
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
    float bite = lerp(1 - MetalScratchVariation, 1,
        0.5 + 0.5 * ReliefOctave(direction, MetalBrushB, MetalScratchLengthFrequency, footprint));

    grain *= bite;

    float3 smoothNormal = normalize(input.WorldNormal);
    float3 worldNormal = PerturbNormalFromHeight(smoothNormal, input.WorldPosition, grain * MetalBrushDepth);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    //THE SLOPE THE BAND LIMIT TOOK (#1216, see MetalSkyBrushed's header): each octave's slope variance
    //(amplitude times frequency, squared, halved for a sine) times the share its fade removed, 1 - fade^2.
    //The fades are ReliefOctave's own, so what is kept above and what is lost here are one octave split.
    float4 brushFrequency = f * float4(1, 1.61, 2.63, 4.27);
    float4 brushSlope = float4(0.36, 0.28, 0.21, 0.15) * brushFrequency;
    float4 brushKept = saturate(1 - footprint * brushFrequency / 3.14159265);
    float lostOctaves = 0.5 * dot(brushSlope * brushSlope, 1 - brushKept * brushKept);

    //Into world slope: the octaves are waves in the brush coordinate s = dot(direction, MetalBrushA),
    //whose gradient over the surface is the tangential part of MetalBrushA over the radius - known in
    //object space without a rotation (its length), while its DIRECTION in world space is read off the
    //screen the way PerturbNormalFromHeight reads a height's. Where the gradient vanishes (the brush's two
    //poles, which the scratches circle) nothing is spread, so the direction's own breakdown there is moot.
    float brushCoordinate = dot(direction, MetalBrushA);
    float gradientSquared = saturate(1 - brushCoordinate * brushCoordinate) / (radius * radius);
    float spread = lostOctaves * gradientSquared * (MetalBrushDepth * bite) * (MetalBrushDepth * bite);

    float3 dpdx = ddx(input.WorldPosition);
    float3 dpdy = ddy(input.WorldPosition);
    float3 r1 = cross(dpdy, smoothNormal);
    float3 r2 = cross(smoothNormal, dpdx);
    float3 brushAxis = sign(dot(dpdx, r1)) * (ddx(brushCoordinate) * r1 + ddy(brushCoordinate) * r2);
    brushAxis -= worldNormal * dot(brushAxis, worldNormal);
    brushAxis *= rsqrt(max(dot(brushAxis, brushAxis), 1e-30));
    float3 brushAcross = cross(worldNormal, brushAxis);

    //The sun's half vector as slopes off the normal along and across that axis, and how far the spread
    //swings the reflected direction's height, which is what blurs the horizon: a tilt e along the axis
    //turns the mirror direction by 2e((axis.eye)n + (n.eye)axis).
    float3 sunHalfway = SunDirection + eyeVector;
    sunHalfway *= rsqrt(max(dot(sunHalfway, sunHalfway), 1e-12));
    float sunFacing = max(dot(sunHalfway, worldNormal), 1e-3);
    float sunAlong = dot(sunHalfway, brushAxis) / sunFacing;
    float sunAcross = dot(sunHalfway, brushAcross) / sunFacing;
    float horizonSpread = 2 * sqrt(spread)
        * abs(dot(brushAxis, eyeVector) * worldNormal.y + dot(worldNormal, eyeVector) * brushAxis.y);

    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The alloy: the tint as reflectance at normal incidence, floored along its own hue so the dark types
    //are gunmetal rather than holes. See MetalF0Floor.
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 f0 = max(primary, primary / max(peak, 1e-3) * MetalF0Floor);

    //A painted cue (#737) is ENAMEL in the alloy: the figure and the ground are the reflectance scaled, a darker or a
    //brighter metal, because a metal's colour is what it reflects and there is no body under it to dye. Scaled from
    //the FLOORED reflectance, not dyed before the floor: floored afterwards, the dark types' figure and ground would
    //both come up to the one gunmetal and the figure would be gone. The scales average to 1 over the ball, and the
    //glow takes the same scale, so the ball as a whole reflects and radiates what it did.
    float3 glowDye = primary;

    if (motifCue)
    {
        float cueScale = ColourCueInkScale(ColourCueMotif(direction, footprint, cue), cue);

        glowDye = primary * cueScale;
        f0 = min(f0 * cueScale, 1);
    }

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
    float3 environment = MetalSkyBrushed(reflect(-eyeVector, worldNormal), sunAlong, sunAcross, spread, horizonSpread);
    float grazing = pow(1 - saturate(dot(worldNormal, eyeVector)), MetalGrazingPower);

    float3 reflection = lerp(f0 * dot(environment, float3(0.2126, 0.7152, 0.0722)), environment, grazing);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    float4 shaded = float4((reflection * MetalReflectance + specular * f0 * MetalHighlight) * occlusion, 1);

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through. A painted cue glows in its inks (#737), as on the wool: see WoolShade.
    shaded.rgb += BallEmission(glowDye, input.WorldPosition, occlusion);

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

float4 MetalPS(PatternVertexShaderOutput input) : COLOR
{
    return MetalShade(input, false, (ColourCuePaint)0);
}

technique InstancedModelMetal
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL MetalPS();
    }
};
