//---------------------------------------------------------------------------------------------------
// INFECTIOUS (#331)
//
// A sick ball: the ball's own colour under a film of luminous slime. Blotches with wet rims lie on the
// shell, a bead of it hangs at the underside, and the whole thing creeps.
//
// ⚠ THE FIGURE IS IN OBJECT SPACE AND THE ACID'S IS IN WORLD SPACE, and that opposition is the whole of
// what keeps the two dark green kinds apart. An acid is a statement about GRAVITY -- it eats downward, so
// its drips must hang the same way whatever the body is doing, and it reads as a dark ball with a lit
// underside. An infection is a statement about the SURFACE IT IS ON: it is stuck to the ball and turns
// with it, which is contract point 6 answered by the figure itself rather than by something added for it.
// So one has an axis and the other has none, and that is legible at any distance.
//
// The bead at the bottom is the one world-space term and it is deliberately SMALL: slime does run
// downward, and taking that away entirely would leave the film reading as paint. It is a hint under an
// object-space figure, where the acid's pool is the figure.
//---------------------------------------------------------------------------------------------------

//What the slime is: a sharp, luminous yellow-green, well clear of both the leafy ball greens and the
//acid's acrid one. Yellow is what does the separating -- an acid is green going to black, this is green
//going to WARM, which is what "sick" looks like on anything.
static const float3 InfectSlime = float3(0.68, 0.92, 0.16);

//And the wet rim around each blotch, brighter and paler than the body of it: what makes a patch read as
//something LYING ON the ball rather than as a stain in its material.
static const float3 InfectRim = float3(0.86, 1.0, 0.52);

//How big the blotches are and how much of the ball they cover. The coverage is the figure's whole read at
//a distance: too little and a sick ball is an ordinary ball with a speck on it, too much and the colour
//underneath -- which is the counterplay -- stops being readable.
//
//⚠ IT IS A THRESHOLD ON A FIELD WHOSE MEAN IS NOT A HALF, and the first value was set as though it were:
//0.42 photographed as slime over the WHOLE ball with the type colour gone, which is the one thing this
//figure may not do. The field is a sum of RECTIFIED sines whose amplitudes total one, and the mean of
//|sin| is 2/pi = 0.64 -- so a threshold at 0.42 is well BELOW the average and passes four fifths of the
//surface. Anything cutting a rectified field has to be measured against 0.64 and not against 0.5.
static const float InfectBlotchFrequency = 3.6;
static const float InfectCoverage = 0.80;

//How hard the edge of a blotch is, as a fraction of the field it is cut out of. Small, so the film has a
//DEFINED edge: a soft one reads as dirt and a hard one as something with a surface tension of its own.
//
//⚠ AND SMALL IS NOT TASTE HERE, IT IS THE COUNTERPLAY. At 0.12 the smoothstep's band covered most of the
//field's own spread, so the film was PARTIALLY present nearly everywhere rather than in patches — which
//tinted and darkened the whole ball, and photographed across all thirteen colours as thirteen dark green
//balls with yellow spots. A sick ball has to be shot out by matching its colour, so a figure that takes
//the colour away is a figure that removes the only counterplay this kind has. The film is binary now, and
//between the patches the ball is simply itself.
static const float InfectEdge = 0.05;

//How far the wet rim reaches inside that edge, and how bright it is against the body of the slime.
static const float InfectRimWidth = 0.16;
static const float InfectRimGain = 0.9;

//How much the blotches CREEP, and how fast. It is the one animated term in the figure and it rides
//PulseTime like every other beat in this file, so it stays in step with the cluster rather than running
//on a clock of its own. Slow: what it has to say is "this is alive", not "this is electrical".
static const float InfectCrawlDepth = 0.10;
static const float InfectCrawlSpeed = 0.42;

//The bead at the underside: the one world-space term (see the header). A cap rather than a line, so it
//survives to the distance a level is played from, and small enough that it never becomes the figure.
static const float InfectBeadStart = 0.82;
static const float InfectBeadPower = 2.4;
static const float InfectBeadGain = 0.55;

//What the film converges to once the blotches are under a pixel. Not nothing, for BombFarGlow's reason: a
//distant sick ball has to still be nameable, and at that size what is left is a ball with a green cast
//and a bright bottom.
static const float InfectFarGlow = 0.30;

//Where the blotches stop being worth drawing, measured against a BLOTCH's own width -- the ice crack's
//rule, which the zap's arcs and the acid's lanes both follow.
static const float InfectBandLimit = 0.95;

//The slime's own light, added under the heartbeat for BallEmission's reason: a sick ball buried in a pile
//is the one that most has to be seen, and BallEmission's resting half is multiplied by occlusion squared.
static const float InfectRestingGlow = 0.62;

//How proud the film stands off the shell, in world units. POSITIVE like the acid's drips and for the same
//reason: it is something LYING ON the ball, and the sign is what makes it read as matter rather than as a
//groove cut into the surface.
static const float InfectRelief = 0.014;

//Wet, but less mirror-like than an acid: slime is a scattering film, not a running liquid.
static const float InfectHighlight = 0.62;
static const float InfectEnvironment = 0.40;
static const float InfectSmoothness = 0.74;

//The blotch field: three rectified octaves in object space at frequencies sharing no common factor, so it
//is a scatter of patches rather than a set of bands. Rectified for StoneLumps' reason -- abs() of a sum of
//waves makes lumps with edges, which is what a blotch is; the raw sum makes ripples.
float InfectBlotches(float3 direction, float footprint, float crawl)
{
    return 0.45 * abs(ReliefOctave(direction, float3(0.62, -0.31, 0.72), InfectBlotchFrequency * (1.0 + crawl), footprint))
        + 0.33 * abs(ReliefOctave(direction, float3(-0.44, 0.79, 0.43), InfectBlotchFrequency * 1.73, footprint))
        + 0.22 * abs(ReliefOctave(direction, float3(0.81, 0.51, -0.29), InfectBlotchFrequency * 2.61, footprint));
}

float4 InfectiousPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Contract point 6 answered by the figure itself: the blotches are in the ball's OWN frame, so a sick
    //ball rolling is unmistakably rolling. The crawl only breathes the first octave's frequency, which
    //makes the patches swell and shrink where a phase shift would make them slide -- and something
    //sliding over a ball is the one thing that would undo the rotation cue.
    float crawl = InfectCrawlDepth * sin(PulseTime * InfectCrawlSpeed);
    float blotches = InfectBlotches(direction, footprint, crawl);

    //One blotch is about 1/InfectBlotchFrequency of the surface across, so the limit is measured against
    //that -- and past it the film converges to a flat cast rather than to nothing.
    float limit = saturate(InfectBandLimit - footprint * InfectBlotchFrequency);

    //The film: everything above the coverage threshold, with a defined edge. `inside` is how far past the
    //edge this pixel is, which the rim below reads back off.
    float film = smoothstep(InfectCoverage - InfectEdge, InfectCoverage + InfectEdge, blotches);
    float inside = saturate((blotches - InfectCoverage) / max(InfectRimWidth, 1e-4));

    //The wet rim: present at the boundary and gone in the middle of a patch. It is the term that says the
    //film has a THICKNESS, and it is what the eye reads as "wet" more than the highlight is.
    float rim = film * (1.0 - smoothstep(0.0, 1.0, inside));

    //The bead at the underside, in world space (see the header). 0 at the top of the ball, 1 underneath.
    float3 normal = normalize(input.WorldNormal);
    float toward = saturate(0.5 - 0.5 * normal.y);
    float bead = pow(saturate((toward - InfectBeadStart) / (1.0 - InfectBeadStart)), InfectBeadPower);

    //MAX rather than a sum, the acid's own rule: where the bead runs under a blotch the two must be one
    //brightness instead of doubling. Converging to the far glow keeps a distant sick ball nameable.
    float slime = max(lerp(InfectFarGlow, film, limit), bead * InfectBeadGain);

    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 slimeColor = SrgbToLinear(InfectSlime);

    //THE BALL'S OWN COLOUR IS STILL THE BODY, and that is the counterplay made visible: a sick ball can be
    //matched and shot out, so the player has to be able to read which colour it is. The film darkens and
    //tints what it lies on rather than replacing it -- a wet surface is darker than a dry one -- and the
    //slime's own light is added on top below.
    float3 color = primary * (1.0 - 0.28 * film) + slimeColor * 0.16 * slime;

    //Contract point 6's other half, and the reason the relief is positive: the film is matter lying on the
    //shell. The rim stands proudest, which is what gives a patch its lip.
    float height = (film * 0.7 + rim * 0.6 + bead * 0.5) * InfectRelief;

    float3 worldNormal = PerturbNormalFromHeight(normal, input.WorldPosition, height);

    SurfaceSpecular surface;
    surface.Highlight = InfectHighlight;
    surface.Environment = InfectEnvironment;
    surface.Smoothness = InfectSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2. The floor is added HERE as well as through BallEmission -- the bomb's measured
    //fault, inherited rather than rediscovered a fourth time -- and the rim gets its own share on top,
    //because the lip of a patch is where a luminous film is brightest.
    shaded.rgb += slimeColor * slime * InfectRestingGlow;
    shaded.rgb += SrgbToLinear(InfectRim) * rim * InfectRimGain * occlusion;

    //⚠ THE HEARTBEAT IS THE BALL'S OWN COLOUR WHERE THE FILM IS NOT, AND THE FILM'S WHERE IT IS — a lerp
    //and not a substitution, which is what the first cut of this line was. Every ordinary ball in this game
    //radiates ITS OWN colour on the beat, and that glow is a large part of how the thirteen are told apart;
    //handing this technique the slime's colour instead took it away, and all thirteen photographed as dark
    //greenish balls with yellow spots. A sick ball has to be shot out by matching its colour, so anything
    //that costs it its colour costs the kind its only counterplay. It is an ordinary ball that is sick, and
    //this line is where that sentence is either true or a claim.
    shaded.rgb += BallEmission(lerp(primary, slimeColor, film), input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately. The lit branch carries
    //the BALL's colour towards white rather than the slime's: the wave is the cluster answering a landing
    //and it has to look the same on every ball it passes through.
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

technique InstancedModelInfectious
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL InfectiousPS();
    }
};
