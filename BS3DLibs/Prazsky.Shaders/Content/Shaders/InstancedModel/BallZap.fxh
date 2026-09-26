//===================================================================================================
//⚠ REDRAWN AS A BRANCHING DISCHARGE IN #626, and the paragraphs below the next rule are the arc cage's
//history (#327). What they measured still governs this one: the cold shell, the blue-white charge, the far
//glow floor, the resting glow the burial rule cannot reach, the fast shallow flicker. What changed is the
//FIGURE. The generated references ("lightning trapped in dark glass", both models) drew one discharge
//branching out of a single hot point - klein as a star of forking bolts across the whole ball - and the
//owner asked for that shader. It is the charged object at rest: a cage of three great circles reads as a
//wound ball or a wireframe; a tree of forks out of one node reads as electricity and nothing else.
//
//  - THE NODE: one hot point on the shell (ZapRootAxis, object space, facing -Z so a level-placed zap shows
//    it to the player), never band-limited - it is what the far read keeps, the bomb's eye by its argument.
//  - THE BRANCHES: six jagged main bolts radiating from the node across the sphere, each forking twice, all
//    drawn in the node's azimuthal-equidistant plane (angle off the node as a radius), so a bolt is a
//    straight-ish segment there and wraps round the ball on the shell. Each tapers from the node, wanders
//    in two octaves, and flickers on its own phase. MAX over them, the cage's crossing rule.
//  - THE BAND LIMIT is each bolt's own width against the footprint, converging to ZapFarGlow.
//
//The plasma style is still the collision: plasma glows all over in its own colour with soft drifting
//filaments; this is a dark shell with one fixed cold node and hard forks that snap.
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

//THE DISCHARGE TREE (#626). The node's direction, how many main bolts leave it and how far round the ball
//they reach (radians off the node), their width at the node and at the tip, how far and how finely they
//wander, where along a bolt its two forks leave, at what angle and for what share of its length, the node's
//radius and gain, and how fast the bolts flicker.
static const float3 ZapRootAxis = float3(0.18, 0.22, -0.96);
static const float ZapBoltCount = 6.0;
static const float ZapBoltReach = 2.25;
static const float ZapBoltWidthRoot = 0.075;
static const float ZapBoltWidthTip = 0.018;
static const float ZapBoltWander = 0.10;
static const float ZapForkAt1 = 0.36;
static const float ZapForkAt2 = 0.62;
static const float ZapForkAngle = 0.62;
static const float ZapForkLength = 0.38;
static const float ZapNodeRadius = 0.16;
static const float ZapNodeGain = 1.6;
static const float ZapFlickerSpeed = 6.3;

//One arc's mask: the thin band around the great circle perpendicular to `axis`, wandering off it by
//ZapArcWander in ZapArcWaves lobes so it is a discharge rather than a wireframe.
float ZapArc(float3 direction, float3 axis, float3 along, float phase)
{
    float wander = ZapArcWander * sin(ZapArcWaves * dot(direction, along) * 3.14159265 + phase);
    float toArc = abs(dot(direction, axis) + wander);

    return pow(saturate(1.0 - toArc / max(ZapArcWidth, 1e-4)), ZapArcSharpness);
}

//A cheap 1D hash for the per-bolt variation, the acid's own.
float ZapHash(float n)
{
    return frac(sin(n * 91.713) * 47453.5453);
}

//One jagged bolt in the node's plane: from `start` along the unit `heading` for `length`, wandering off its
//line in two octaves (seeded, so each bolt bends its own way), tapering from `widthStart` to the tip width.
//Returns the mask (1 on the bolt's core) and, through `widthOut`, the bolt's width at the pixel for the
//band limit.
float ZapBolt(float2 p, float2 start, float2 heading, float span, float widthStart, float seed, out float widthOut)
{
    float2 normal = float2(-heading.y, heading.x);
    float2 local = p - start;
    float along = dot(local, heading);
    float t = saturate(along / max(span, 1e-4));

    float wander = ZapBoltWander * (sin(along * 9.0 + seed * 6.2831853) * 0.65 + sin(along * 23.0 + seed * 17.0) * 0.35)
        * saturate(along * 6.0);
    float across = abs(dot(local, normal) - wander);

    widthOut = lerp(widthStart, ZapBoltWidthTip, t);
    float inside = step(0.0, along) * step(along, span);
    float core = saturate(1.0 - across / max(widthOut, 1e-4));

    //The tip fades over its last fifth rather than stopping square - a spark thins out, it is not cut off.
    return core * core * inside * (1.0 - smoothstep(0.8, 1.0, t));
}

//The whole tree: ZapBoltCount main bolts from the node, two forks off each. Returns the mask and the
//narrowest width it was drawn at this pixel, for the band limit.
float ZapTree(float3 direction, float3 rootAxis, float treeSeed, out float widthHere)
{
    float3 root = normalize(rootAxis);

    //The node's azimuthal-equidistant plane: angle off the node as the radius, round it as the azimuth.
    float3 helper = abs(root.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 u = normalize(cross(helper, root));
    float3 v = cross(root, u);
    float off = acos(clamp(dot(direction, root), -1.0, 1.0));
    float2 planar = float2(dot(direction, u), dot(direction, v));
    float2 p = off * planar / max(length(planar), 1e-5);

    float mask = 0.0;
    widthHere = ZapBoltWidthRoot;

    [unroll]
    for (int k = 0; k < 6; k++)
    {
        float seed = ZapHash(k + 1.0 + treeSeed);
        float angle = (k + 0.35 * (seed - 0.5)) * 6.2831853 / ZapBoltCount;
        float2 heading = float2(cos(angle), sin(angle));
        float boltLength = ZapBoltReach * (0.72 + 0.28 * ZapHash(k + 11.0 + treeSeed));
        float flicker = 0.7 + 0.3 * sin(PulseTime * ZapFlickerSpeed + seed * 6.2831853);

        float w;
        float bolt = ZapBolt(p, float2(0, 0), heading, boltLength, ZapBoltWidthRoot, seed, w);
        if (bolt > mask) { widthHere = w; }
        mask = max(mask, bolt * flicker);

        //Two forks, one each side, leaving at their share of the bolt from where its wander has it.
        [unroll]
        for (int f = 0; f < 2; f++)
        {
            float at = (f == 0 ? ZapForkAt1 : ZapForkAt2) * boltLength;
            float side = (f == 0 ? 1.0 : -1.0) * (seed > 0.5 ? 1.0 : -1.0);
            float forkAngle = angle + side * ZapForkAngle;
            float2 forkHeading = float2(cos(forkAngle), sin(forkAngle));
            float2 forkStart = heading * at;
            float forkWidth = lerp(ZapBoltWidthRoot, ZapBoltWidthTip, at / boltLength) * 0.8;

            float fw;
            float fork = ZapBolt(p, forkStart, forkHeading, boltLength * ZapForkLength, forkWidth, seed + f + 3.0, fw);
            if (fork > mask) { widthHere = fw; }
            mask = max(mask, fork * flicker);
        }
    }

    return mask;
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
    //⚠ The figure is the discharge tree since #626; the three arcs' construction below is gone. The limit
    //is each bolt's own width at the pixel, on the same late-start rule.
    //TWO TREES, one from the node and one from the point opposite it (the owner's verdict on the first cut:
    //the density and width are right "if the bolts are visible from the other side too"). One tree reaches
    //ZapBoltReach round the ball and left a dark cap at the antipode; the second, seeded apart and turned
    //by its own seeds, fills it, so a zap seen from any side shows a node and its forks.
    float boltWidth, backWidth;
    float front = ZapTree(direction, ZapRootAxis, 0.0, boltWidth);
    float back = ZapTree(direction, -ZapRootAxis, 23.0, backWidth);
    float tree = max(front, back);
    if (back > front) boltWidth = backWidth;
    float arcLimit = saturate(ZapArcBandLimit - footprint / max(boltWidth * 2.0, 1e-3));

    //The three arcs, each wandering off a DIFFERENT partner axis so they do not bend in step, and each on
    //its own phase so the figure never lines up into symmetry.
    float arcs = tree;

    //MAX and not a sum, deliberately: where two arcs cross, a sum doubles the light and the crossing
    //becomes a blob twice as bright as anything else on the ball. What should read at a crossing is the
    //SHAPE - two lines meeting - and max is what keeps the lines at one brightness all the way through.
    arcs *= arcLimit;

    //The electrodes, which do not band-limit: they are caps rather than lines, so they are already
    //resolvable at any size the ball is drawn at, and they are what is left of the figure when it is not.
    float nodeOff = acos(abs(clamp(dot(direction, normalize(ZapRootAxis)), -1.0, 1.0)));
    float poles = pow(saturate(1.0 - nodeOff / ZapNodeRadius), 1.5) * ZapNodeGain;

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
