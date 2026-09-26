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
