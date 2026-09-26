//---------------------------------------------------------------------------------------------------
// GRAVITY (#332, redrawn in #630 from generated references)
//
// A well: the ball's own colour, swirled. A whirlpool of light and dark streaks spirals INWARD across the
// face of the ball into a dark eye at its centre, and the whole figure turns towards the eye that looks at
// it, so a well seen from any side shows what "falling in" looks like from outside.
//
// ⚠ THE FIGURE'S JOB IS TO SAY "THIS IS WHY YOUR SHOT WENT THERE", and #332 states the alternative in
// one line: a curve the player cannot see coming is a bug with a physics explanation. So what is drawn
// is not a glow but a MOTION, and its direction is the whole message -- streaks winding in towards the
// centre at a steady rate. The bomb's beat swells outward and the acid's liquid stands at the bottom;
// both say "this will happen when you touch it". This one says "this is happening now, to anything that
// comes near", which is the only honest thing a field can say.
//
// WHAT THE REFERENCES SAID (#630, nine renders: a swirled sphere, a sphere with a disc round it, a sphere in
// a cage of field lines): the disc and the field lines live OUTSIDE the ball and a drawn ball may not leave
// its cell, so they were never drawable here; the swirl is the one that is, and it answers the question the
// issue asked -- CONCENTRIC RINGS ARE A TARGET, AND A SPIRAL IS NOT. #332's rings photographed on the owner's
// #620 sheet as a bullseye ("aim here"), the one reading a well must not have. A spiral converging on a
// dark eye reads as a drain: the eye is led IN, and nothing about it invites a shot.
//
// Three things carry it:
//  1. THE STREAKS. Light and dark bands of the ball's OWN colour, a logarithmic spiral in the disc the ball
//     presents to the eye, winding tighter towards the centre and drifting inward on PulseTime. They are
//     band-limited on their own screen-space derivative, so a distant well is a plain ball of its colour.
//  2. THE EYE. A dark core at the centre of the disc, the one part of the figure that does not band-limit:
//     at the overview stand-off a well is a ball with a hole in it, which is a well.
//  3. THE RIM. The shell darkens towards its limb and bends the environment reflection outward at the
//     grazing angle (the lens, a fake -- the scene is not re-sampled), so the ball reads as dense and as
//     bending what is behind it.
//
// The streaks and the eye are VIEW-relative on purpose, for the reason #332 gave: refraction and a pull
// towards the observer are facts about the eye, and a mirror that turned with the ball would read as paint.
// Contract point 6 (a rolling cue in object space) is answered by a faint object-space mottle under the
// streaks -- the reference's glass has a body, and the body turns.
//---------------------------------------------------------------------------------------------------

//The shell's own darkening towards the rim: a well is a heavy thing and it holds its own light in. It
//multiplies the ball's colour rather than replacing it, which is what keeps the colour readable -- the
//counterplay is shooting the well out, and that means matching its colour.
static const float GravityRimDarkening = 0.55;
static const float GravityRimPower = 2.2;

//The spiral: how many arms, how tightly they wind (radians of phase per natural log of the radius --
//higher is a tighter coil towards the centre), and how fast the pattern drifts INWARD, in radians of
//phase per second. Three arms and a moderate wind so the streaks are broad bands rather than a thread.
static const float GravityArms = 5.0;
static const float GravityWind = 4.0;
static const float GravitySpeed = 1.4;

//A floor under the log, so the phase is finite at the exact centre; it also sets where the coil stops
//tightening, which is inside the eye anyway.
static const float GravitySpiralFloor = 0.03;

//How much the light streaks lift the ball's colour, and how much the dark ones crush it. The lift is towards
//a PALER version of the ball's own colour rather than towards white (GravityStreakPale is how far that pale
//sits from the hue), so a swirled yellow stays a yellow: the first cut lifted to white and photographed as
//cream. Asymmetric: the reference's swirl is a glass marble with a lighter thread in it, not a striped ball.
static const float GravityStreakLight = 0.42;
static const float GravityStreakPale = 0.65;
static const float GravityStreakDark = 0.32;

//How sharply a streak is edged: a power on the raised sine. 1 is a plain sine; higher pinches the light
//thread thinner. Kept moderate so the figure survives to a few dozen pixels as bands and not as lines.
static const float GravityStreakSharpness = 1.8;

//The eye: its radius as a fraction of the disc's, how far it crushes the colour, and how soft its edge is.
static const float GravityEyeRadius = 0.15;
static const float GravityEyeDark = 0.85;
static const float GravityEyeEdge = 0.06;

//Where the streaks stop being worth drawing, measured against their OWN screen-space derivative (fwidth
//of the phase, in radians per pixel): a streak is one wavelength per 2 * pi of phase, so it is gone when a
//pixel spans about a half of that.
static const float GravityBandLimit = 3.14159265;

//The lens: how much the shell bends what is behind it, and how sharply that grows towards the rim. It is
//a fake -- the scene is not re-sampled -- so what it actually does is push the ENVIRONMENT reflection
//outward, which at a grazing angle is most of what a refracting sphere shows anyway.
static const float GravityLensStrength = 0.55;
static const float GravityLensPower = 1.8;

//A dense, polished body: a well is not wet like an acid and not frosted like ice.
static const float GravityHighlight = 0.70;
static const float GravityEnvironment = 0.66;
static const float GravitySmoothness = 0.88;

//How deep the dark streaks cut into the shell, in world units. NEGATIVE relief -- the streaks are troughs,
//light falling INTO the ball, where the acid's meniscus is a bead lying on it. The sign is the second half
//of the inward reading.
static const float GravityStreakRelief = 0.006;

//The object-space body under the streaks (contract point 6): cells across the unit direction and how much
//it moves the colour. Faint on purpose -- it is there to turn with the ball, not to be a figure.
static const float GravityBodyCells = 3.0;
static const float GravityBodyStrength = 0.07;

float4 GravityPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    float3 normal = normalize(input.WorldNormal);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    //THE DISC THE BALL PRESENTS TO THE EYE. `across` is the pixel's distance from the disc's centre as a
    //fraction of its radius (sin of the angle off the eye, straight out of the dot product -- #332's
    //re-spacing lesson, kept: a figure spaced in 1 - cos crowds into the limb), and `azimuth` is its angle
    //round that centre, measured in a frame hung off the eye vector so the spiral stands still on the
    //screen while the ball turns under it.
    float facing = saturate(dot(normal, eyeVector));
    float around = 1.0 - facing;
    float across = sqrt(saturate(1.0 - facing * facing));

    float3 planar = normal - eyeVector * dot(normal, eyeVector);
    float3 right = normalize(cross(float3(0.0, 1.0, 0.0), eyeVector) + float3(1e-4, 0.0, 1e-4));
    float3 up = cross(eyeVector, right);
    float azimuth = atan2(dot(planar, up), dot(planar, right));

    //THE SPIRAL. A logarithmic spiral's phase is arms * azimuth + wind * ln(r): adding time to it moves
    //every streak to a smaller r, i.e. INWARD. Reversing that one sign is the difference between a well and
    //a fountain. Band-limited on the phase's own screen-space derivative, which is exact for any spiral: the
    //coil tightens towards the centre and the limit tightens with it.
    float phase = GravityArms * azimuth + GravityWind * log(across + GravitySpiralFloor) + PulseTime * GravitySpeed;
    float limit = saturate(1.0 - fwidth(phase) / GravityBandLimit);

    float wave = 0.5 + 0.5 * sin(phase);
    float light = pow(wave, GravityStreakSharpness) * limit;
    float dark = pow(1.0 - wave, GravityStreakSharpness) * limit;

    //THE EYE: the one part of the figure that does not band-limit.
    float eye = 1.0 - smoothstep(GravityEyeRadius - GravityEyeEdge, GravityEyeRadius + GravityEyeEdge, across);

    //The body that turns with the ball (contract point 6).
    float body = GradientNoise3(direction * GravityBodyCells) * saturate(1.0 - footprint * GravityBodyCells * 2.0);

    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //THE COLOUR: the ball's own, swirled -- lifted towards white along the light streaks, crushed along the
    //dark ones and into the eye, darkened towards the rim. Every term multiplies or lerps the primary, so
    //WHICH colour this is never stops being readable: that is the shot that removes it.
    float rim = pow(around, GravityRimPower);
    float3 color = primary * (1.0 + GravityBodyStrength * body);
    color = lerp(color, lerp(primary, 1.0, GravityStreakPale), GravityStreakLight * light);
    color *= 1.0 - GravityStreakDark * dark;
    color *= 1.0 - GravityEyeDark * eye;
    color *= 1.0 - GravityRimDarkening * rim;

    //Contract point 6's other half: the dark streaks are troughs cut INTO the shell. Negative, where the
    //acid's meniscus is positive, and the sign is the second half of what makes this read as a pull.
    float height = -dark * GravityStreakRelief;
    float3 worldNormal = PerturbNormalFromHeight(normal, input.WorldPosition, height);

    //The lens, and it is a fake: the scene is not re-sampled. What it does is bend the normal the
    //environment term reflects about, outward and hardest at the rim, which at a grazing angle is most of
    //what a refracting sphere actually shows.
    float3 outward = normalize(worldNormal - eyeVector * dot(worldNormal, eyeVector) + 1e-5);
    worldNormal = normalize(worldNormal + outward * (GravityLensStrength * pow(around, GravityLensPower)));

    SurfaceSpecular surface;
    surface.Highlight = GravityHighlight;
    surface.Environment = GravityEnvironment;
    surface.Smoothness = GravitySmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2. The heartbeat rides the swirled colour, so a well still breathes with the cluster and
    //is still visibly ITS colour; the eye stays dark through the beat because it is in the colour, not added
    //after it.
    shaded.rgb += BallEmission(color, input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately.
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

technique InstancedModelGravity
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL GravityPS();
    }
};
