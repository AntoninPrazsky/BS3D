//---------------------------------------------------------------------------------------------------
// ACID (#328)
//
// The corrosive kind: a dark, wet green shell with liquid running DOWN it into a pool at its underside.
//
// ⚠ THE WHOLE FIGURE IS BUILT IN WORLD SPACE, OFF THE WORLD NORMAL, and that is the one thing about this
// technique that is not taste. Every other ball figure in this file is drawn in OBJECT space, so it turns
// with the ball; an acid's figure is a statement about GRAVITY, and a drip that rotated with the body would
// say nothing. On a sphere the world normal IS the outward radial direction, so `-n.y` is "how far down this
// point faces" with no rotation matrix needed and no dependence on how the body happens to be lying — which
// also means it is right for the acid's whole life, hanging in the lattice and again tumbling down the drain.
//
// It is the first ball kind with an AXIS, which is what #328 asks the look to carry: a player has to be able
// to tell, before firing, that this one does something downward.
//---------------------------------------------------------------------------------------------------

//The shell: a dark, slightly yellowed green. Dark for the bomb's and the zap's reason — everything this kind
//says it says with the liquid on it, and a lifted shell washes that out — and GREEN because that is the whole
//of what separates the three dark specials at a glance: the bomb is a warm dark, the zap a cold one, this an
//acrid one.
static const float3 AcidShell = float3(0.075, 0.105, 0.055);

//And what runs on it: a hot acid green, well clear of the thirteen ball colours' greens, which are leafy where
//this is chemical.
static const float3 AcidLiquid = float3(0.52, 0.95, 0.18);

//How many drip lanes there are around the ball. Enough that one is in view from any angle, few enough that
//each reads as a run of liquid rather than as a stripe pattern.
static const float AcidDripLanes = 9.0;

//Half the width of one lane, as a fraction of the lane's own spacing. Under a half, so the lanes are separated
//by shell rather than meeting into a skirt.
static const float AcidDripHalfWidth = 0.30;

//How far up the ball the drips reach, as a fraction of the surface from the bottom pole, and how much of that
//varies from lane to lane. The shortest ones stay near the underside; the longest climb past the equator.
static const float AcidDripReach = 0.42;
static const float AcidDripSpread = 0.30;

//How much a lane's reach breathes, and how fast, so the liquid CREEPS instead of standing still. It is the one
//animated term here and it rides PulseTime like every other beat in this file, so it stays in step with the
//cluster rather than running on a clock of its own.
static const float AcidCrawlDepth = 0.10;
static const float AcidCrawlSpeed = 0.55;

//The pool at the underside: where every drip is heading, and the part of the figure that does NOT band-limit.
//It is a cap rather than a line, so it is still resolvable when the ball is a dozen pixels across and the
//lanes are long gone - the zap's electrodes, arriving at the one place gravity puts them.
static const float AcidPoolStart = 0.72;
static const float AcidPoolPower = 2.2;

//What the figure converges to once the lanes are under a pixel: not nothing, or a distant acid would read as a
//plain dark ball. BombFarGlow's argument, and the same shape.
static const float AcidFarGlow = 0.22;

//Where the lanes stop being worth drawing, measured against a LANE's own width rather than the whole sphere -
//the ice crack's rule, which the zap's arcs also follow.
static const float AcidLaneBandLimit = 0.95;

//The liquid's own light, added under the heartbeat for the reason BallEmission's remarks give: an acid buried
//in a pile is the one that most has to be seen, and BallEmission's resting half is multiplied by occlusion
//squared.
static const float AcidRestingGlow = 0.55;

//Wet, and wetter than either of the other two dark kinds: a tight bright highlight and a real mirror of the
//sky is what makes a surface read as running with liquid rather than as painted.
static const float AcidHighlight = 0.78;
static const float AcidEnvironment = 0.62;
static const float AcidSmoothness = 0.86;

//How proud the liquid stands off the shell, in world units. POSITIVE, unlike the zap's arcs, which cut in: a
//drip is a bead lying ON the surface, and the sign is what makes it read as liquid rather than as a groove.
static const float AcidDripRelief = 0.012;

//A cheap 1D hash for the per-lane variation. Its own because the city's Hash21 was not in scope here when it
//was written; Hash21 is Noise.fxh's since #581 and could be called, but the lanes were tuned on these values.
float AcidHash(float lane)
{
    return frac(sin(lane * 78.233) * 43758.5453);
}

float4 AcidPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float3 normal = normalize(input.WorldNormal);

    //0 at the top of the ball, 1 at its underside. THE figure's parameter, and it is world space (see the
    //header): the drips hang downward whatever the body's orientation.
    float toward = saturate(0.5 - 0.5 * normal.y);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //One lane's width in surface parameter is 1/AcidDripLanes, so the limit is measured against that
    float laneLimit = saturate(AcidLaneBandLimit - footprint * AcidDripLanes);

    //Which lane this pixel is in, and where across it. atan2 on the world XZ, so the lanes stand still in the
    //world while the ball turns under them.
    float angle = atan2(normal.z, normal.x) / 6.28318531 + 0.5;
    float lanes = angle * AcidDripLanes;
    float lane = floor(lanes);
    float across = 1.0 - saturate(abs(frac(lanes) - 0.5) / max(AcidDripHalfWidth, 1e-4));

    //How far up this lane runs, varied per lane and creeping on the shared clock
    float seed = AcidHash(lane);
    float reach = AcidDripReach + AcidDripSpread * seed
        + AcidCrawlDepth * sin(PulseTime * AcidCrawlSpeed + seed * 6.28318531);

    //The run itself: present from where the lane starts down to the underside, with a soft head so it looks
    //poured rather than cut off
    float along = saturate((toward - (1.0 - reach)) / max(reach, 1e-4));
    float drips = across * smoothstep(0.0, 0.22, along) * laneLimit;

    //The pool, which does not band-limit: what is left of the figure when the lanes are not resolvable, and
    //the part that says "downward" at any distance
    float pool = pow(saturate((toward - AcidPoolStart) / (1.0 - AcidPoolStart)), AcidPoolPower);

    //Converging to a floor rather than to nothing, and MAX rather than a sum so a drip running into the pool
    //stays one brightness instead of doubling where the two overlap - the zap's arcs' own rule.
    float liquid = max(lerp(AcidFarGlow, drips, laneLimit), pool);

    //The shell, tinted by what is running on it: wet stone is darker than dry, so the lanes deepen the shell
    //they lie on before the liquid's own light is added on top.
    float3 color = SrgbToLinear(AcidShell) * (1.0 - 0.30 * drips) + SrgbToLinear(AcidLiquid) * 0.10 * liquid;

    //Contract point 6. Positive: the liquid lies ON the shell (see AcidDripRelief), and the pool swells a
    //little more than a single lane does because it is where the runs collect.
    float height = (drips + 0.6 * pool) * AcidDripRelief;

    float3 worldNormal = PerturbNormalFromHeight(normal, input.WorldPosition, height);

    SurfaceSpecular surface;
    surface.Highlight = AcidHighlight;
    surface.Environment = AcidEnvironment;
    surface.Smoothness = AcidSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2. The floor is added HERE rather than through BallEmission alone - the bomb's measured
    //fault, inherited rather than rediscovered a third time.
    shaded.rgb += SrgbToLinear(AcidLiquid) * liquid * AcidRestingGlow;

    shaded.rgb += BallEmission(SrgbToLinear(AcidLiquid) * liquid, input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //Cold-white rather than the shell's own hue, for the bomb's and the zap's reason: RippleWhiten lifts
        //the channels a COLOURED ball is missing, and a near-black shell is missing all of them.
        float3 lit = shaded.rgb + RippleStrength * amount;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelAcid
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL AcidPS();
    }
};
