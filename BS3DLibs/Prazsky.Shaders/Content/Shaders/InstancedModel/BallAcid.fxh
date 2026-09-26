//---------------------------------------------------------------------------------------------------
// ACID (#328, redrawn in #627 from generated references)
//
// The corrosive kind: a dark, dry shell whose LOWER HALF is full of glowing acid. A level surface of liquid
// stands across the ball at its waist, eating upward in rounded tongues with foam along the front, and the
// pool is brightest at the bottom, where every drop is heading.
//
// ⚠ THE WHOLE FIGURE IS BUILT IN WORLD SPACE, OFF THE WORLD NORMAL, and that is the one thing about this
// technique that is not taste. Every other ball figure in this file is drawn in OBJECT space, so it turns
// with the ball; an acid's figure is a statement about GRAVITY, and a liquid that rotated with the body would
// say nothing. On a sphere the world normal IS the outward radial direction, so `-n.y` is "how far down this
// point faces" with no rotation matrix needed and no dependence on how the body happens to be lying - which
// also means it is right for the acid's whole life, hanging in the lattice and again tumbling down the drain.
//
// WHAT THE REFERENCES SAID (#627, nine renders of a dark sphere with acid on it, in three framings): every one
// of the nine put the read in the same place - THE LOWER HALF IS FULL, with a horizontal surface at the waist
// and a dark, dry top. The lanes this replaced were the wrong half of a drip: a run of liquid down a ball is
// a stripe, and nine stripes are a watermelon (the owner's sheet, and the reason this issue exists). What
// says "down" is not the direction a stripe runs but WHERE THE LIQUID IS: a level surface with everything
// under it wet and glowing is gravity drawn as a fact rather than as an arrow. Three things carry it:
//  1. THE FILL. Half the ball, the half that faces the floor. It does not band-limit - a half-disc is a
//     half-disc at any size - which is why the far read is safe for the first time on this kind.
//  2. THE FRONT. Where the liquid meets the dry shell: level, sloshing a little, with rounded tongues eating
//     upward and foam seething along it. Close up this is what makes it a liquid rather than a paint line.
//  3. THE POOL. The glow deepens towards the underside, so the bottom of the ball is the brightest point on
//     it and the eye is led DOWN - the reference's pool under the ball, drawn on the ball.
//
// It keeps the three dark specials' rule: the shell is a dark ACRID green where the bomb's is warm and the
// zap's cold, so they separate by hue at a glance, and the liquid's green is chemical where Type2's is leafy.
//---------------------------------------------------------------------------------------------------

//The shell: a dark, slightly yellowed green. Dark for the bomb's and the zap's reason - everything this kind
//says it says with the liquid on it, and a lifted shell washes that out - and GREEN because that is the whole
//of what separates the three dark specials at a glance: the bomb is a warm dark, the zap a cold one, this an
//acrid one.
static const float3 AcidShell = float3(0.075, 0.105, 0.055);

//And what fills it: a hot acid green, well clear of the thirteen ball colours' greens, which are leafy where
//this is chemical.
static const float3 AcidLiquid = float3(0.52, 0.95, 0.18);

//Where the surface of the acid stands, in the world-space parameter below (0 at the top of the ball, 1 at
//its underside): a half, so the ball is half full and the read is a HALF - the one figure that survives to a
//dozen pixels untouched. The slosh is how much it rises and falls, riding PulseTime like every other beat in
//this file so it stays in step with the cluster rather than running on a clock of its own.
static const float AcidFillLevel = 0.50;
static const float AcidSloshDepth = 0.02;
static const float AcidSloshSpeed = 0.55;

//The tongues eating UP from the front: how many round the ball, how far the tallest reaches above the level,
//how pointed each is (a power on a raised cosine - higher is narrower), and how much each one breathes.
static const float AcidTongueCount = 7.0;
static const float AcidTongueReach = 0.11;
static const float AcidTonguePower = 3.0;
static const float AcidTongueCrawl = 0.35;

//The meniscus: a bright line where the liquid meets the shell, its half-width in the fill parameter, how
//proud it stands (a liquid's edge is a bead, so positive), and its own light.
static const float AcidMeniscusWidth = 0.016;
static const float AcidMeniscusRelief = 0.014;
static const float AcidMeniscusGlow = 0.9;

//The pool: what the glow is at the front and how fast it deepens towards the underside.
static const float AcidPoolFloor = 0.45;
static const float AcidPoolPower = 1.6;

//Corrosion under the liquid: object-space pits seen through it, in cells across the unit direction, and how
//much they modulate the glow. Object space - the pits are in the SHELL, and they are the one part of this
//figure that turns with the ball (contract point 6).
static const float AcidPitCells = 9.0;
static const float AcidPitStrength = 0.22;
static const float AcidPitDepth = 0.004;

//Foam seething along the front, inside the liquid: how deep a band below the front it fills, how many cells
//round the ball, what fraction of the cells carry a bubble, and how often the bubbles change.
static const float AcidFoamBand = 0.07;
static const float AcidFoamCells = 26.0;
static const float AcidFoamThreshold = 0.78;
static const float AcidFoamRate = 1.7;
static const float AcidFoamGlow = 1.3;

//The liquid's own light, added under the heartbeat for the reason BallEmission's remarks give: an acid buried
//in a pile is the one that most has to be seen, and BallEmission's resting half is multiplied by occlusion
//squared.
static const float AcidRestingGlow = 0.55;

//Wet under the surface, dry above it: the fill chooses between the two. The wet half is wetter than either
//of the other two dark kinds - a tight highlight and a real mirror of the sky is what reads as liquid.
static const float AcidWetHighlight = 0.85;
static const float AcidWetEnvironment = 0.65;
static const float AcidWetSmoothness = 0.90;
static const float AcidDryHighlight = 0.40;
static const float AcidDryEnvironment = 0.30;
static const float AcidDrySmoothness = 0.40;

//How much of the liquid's colour the wet shell takes as BODY colour (the rest is emission): enough that the
//wet half is green in the shade too, not so much that the shading swallows the glow's gradient.
static const float AcidWetBody = 0.35;

//A cheap 1D hash for the per-tongue variation. Its own because the city's Hash21 was not in scope here when it
//was written; Hash21 is Noise.fxh's since #581 and could be called, but the tongues were tuned on these values.
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
    //header): the liquid stands level whatever the body's orientation.
    float toward = saturate(0.5 - 0.5 * normal.y);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Round the world's vertical, 0..1, so the tongues and the foam stand still in the world while the ball
    //turns under them.
    float angle = atan2(normal.z, normal.x) / 6.28318531 + 0.5;

    //THE FRONT: the level, sloshing, with one rounded tongue per lane reaching up by its own amount and
    //breathing on its own phase. Band-limited against a tongue's width, so a distant front is a level line.
    float level = AcidFillLevel + AcidSloshDepth * sin(PulseTime * AcidSloshSpeed);

    float lanes = angle * AcidTongueCount;
    float seed = AcidHash(floor(lanes));
    float bump = pow(0.5 + 0.5 * cos((frac(lanes) - 0.5) * 6.28318531), AcidTonguePower);
    float reach = AcidTongueReach * (0.55 + 0.45 * seed)
        * (1.0 + AcidTongueCrawl * sin(PulseTime * AcidSloshSpeed * 1.3 + seed * 6.28318531));
    float tongueLimit = saturate(1.0 - footprint * AcidTongueCount * 1.5);
    float front = level - bump * reach * tongueLimit;

    //THE FILL: everything under the front is wet. One pixel soft, and never band-limited - see the header.
    float towardWidth = max(fwidth(toward), 1e-4);
    float fill = smoothstep(front - towardWidth, front + towardWidth, toward);

    //THE MENISCUS along the front, as coverage so it is one line at any distance. (`line` is an HLSL keyword.)
    float meniscus = BandCoverage(toward - front, AcidMeniscusWidth, towardWidth);

    //THE POOL: the glow deepens from the front to the underside.
    float depth = saturate((toward - front) / max(1.0 - front, 1e-3));
    float pool = fill * lerp(AcidPoolFloor, 1.0, pow(depth, AcidPoolPower));

    //Corrosion pits in the shell, seen through the liquid - object space, band-limited to their mean.
    float pits = GradientNoise3(direction * AcidPitCells) * saturate(1.0 - footprint * AcidPitCells * 2.0);
    float glow = pool * (1.0 + AcidPitStrength * pits);

    //FOAM along the front: hashed cells in (round the ball, below the front), a fraction of them lit, the set
    //changing a few times a second so the front seethes. Band-limited against a cell's own width, converging
    //on the fraction lit.
    float below = toward - front;
    float foamBand = fill * (1.0 - smoothstep(0.0, AcidFoamBand, below));
    float2 foamCoord = float2(angle * AcidFoamCells, below / AcidFoamBand * 2.0);
    float2 foamCell = floor(foamCoord) + float2(0.0, floor(PulseTime * AcidFoamRate) * 7.0);
    float foamFade = saturate(1.0 - footprint * AcidFoamCells * 0.6);

    //A bubble is a DISC in its cell, not the cell: the first cut lit whole cells and the front was tiled.
    float bubble = step(AcidFoamThreshold, Hash21(foamCell)) * (1.0 - smoothstep(0.22, 0.42, length(frac(foamCoord) - 0.5)));
    float foam = foamBand * lerp((1.0 - AcidFoamThreshold) * 0.4, bubble, foamFade);

    //THE COLOUR: the dry shell above, the wet shell tinted by what is standing on it below.
    float3 shell = SrgbToLinear(AcidShell);
    float3 liquid = SrgbToLinear(AcidLiquid);
    float3 color = lerp(shell, liquid * AcidWetBody, fill);

    //Contract point 6: the pits turn with the ball and are cut IN; the meniscus is a bead and stands proud.
    float height = meniscus * AcidMeniscusRelief - fill * pits * AcidPitDepth;
    float3 worldNormal = PerturbNormalFromHeight(normal, input.WorldPosition, height);

    SurfaceSpecular surface;
    surface.Highlight = lerp(AcidDryHighlight, AcidWetHighlight, fill);
    surface.Environment = lerp(AcidDryEnvironment, AcidWetEnvironment, fill);
    surface.Smoothness = lerp(AcidDrySmoothness, AcidWetSmoothness, fill);

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2. The floor is added HERE rather than through BallEmission alone - the bomb's measured
    //fault, inherited rather than rediscovered a third time. The meniscus and the foam carry their own light
    //on top; the heartbeat rides the pool, so what swells is the liquid.
    float3 lit = liquid * (glow * AcidRestingGlow + meniscus * AcidMeniscusGlow + foam * AcidFoamGlow);
    shaded.rgb += lit;
    shaded.rgb += BallEmission(liquid * glow, input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //Cold-white rather than the shell's own hue, for the bomb's and the zap's reason: RippleWhiten lifts
        //the channels a COLOURED ball is missing, and a near-black shell is missing all of them.
        float3 wave = shaded.rgb + RippleStrength * amount;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : wave;
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
