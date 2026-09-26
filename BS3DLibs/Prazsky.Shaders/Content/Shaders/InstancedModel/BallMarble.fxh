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
