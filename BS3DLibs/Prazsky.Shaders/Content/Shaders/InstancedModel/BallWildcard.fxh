//---------------------------------------------------------------------------------------------------
// WILDCARD (#330's crossing, a technique of its own since #632)
//
// The joker: a glossy marbled ball in which the two colours the shared cycle is crossing between flow
// through each other along swirled veins, with an opal play of colour along the veins and the rim.
//
// ⚠ WHY IT IS NO LONGER TWO DITHERED DRAWS. From #330 to #632 a wildcard was two ordinary balls partitioned
// pixel by pixel through the dissolve dither - the colour it was leaving at +d and the one it was going to
// at -d. Since #620 that dither means "this ball is not really there" (the landing ghost, dead weight), and
// a loaded wildcard at the muzzle, the aim ghost and a dead ball could all be on screen at once saying the
// same thing in the same pixels. The generated references (#632: a fire opal, an oil-slick marble) said what
// a wildcard is instead: colours MIXING, never settling. So the crossing is drawn as a marble: the colour it
// is going to (PatternSecondaryColor) spreads along the veins as WildcardProgress runs 0 -> 1, over the one it
// is leaving (PatternPrimaryColor), and at progress 1 the next crossing starts from exactly that picture.
// The dissolve stays what it is everywhere else - the instance's own, so a wildcard ghost dithers like any
// ghost and the #437 lock-in still crosses out of this into an ordinary colour.
//
// It opts out of the level's BallStyle, like every kind with a technique of its own: a wildcard is the one
// ball the player SHOOTS rather than one a level places, and it has to be the same object on every level.
//
// Contract point 6: the veins are in OBJECT space and turn with the ball; they also drift slowly on
// PulseTime, which is the "never the same twice" the references asked for.
//---------------------------------------------------------------------------------------------------

//How far through the current crossing the shared cycle is, 0 at the colour it is leaving and 1 at the one it
//is going to. Set by the render set once a frame, off WildcardCycle, for every wildcard at once (#330).
float WildcardProgress;

//The marble: the axis its bands run across, how many bands, how hard the warp swirls them, the warp's cell
//size, and how fast the whole figure drifts.
static const float3 WildcardVeinAxis = float3(0.36, 0.84, -0.41);
static const float WildcardVeinBands = 2.4;
static const float WildcardVeinWarp = 2.6;
static const float WildcardWarpCells = 1.7;
static const float WildcardDrift = 0.12;

//The opal sheen: how bright the play of colour is along the veins' boundary and round the rim, how wide the
//boundary band is (in the marble field), and how fast the hues travel.
static const float WildcardOpalVein = 0.55;
static const float WildcardOpalRim = 0.45;
static const float WildcardOpalWidth = 0.06;
static const float WildcardOpalSpeed = 0.35;

//A glossy marble: the full highlight and mirror every ordinary ball gets, a touch more polish.
static const float WildcardHighlight = 1.15;
static const float WildcardEnvironment = 1.1;
static const float WildcardSmoothness = 1.0;

//A cheap rainbow: cosine palette over a hue in 0..1, the three channels a third of a turn apart.
float3 WildcardRainbow(float hue)
{
    return saturate(0.5 + 0.5 * cos(6.2831853 * (hue + float3(0.0, 0.333, 0.667))));
}

float4 WildcardPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives - and on this technique it is
    //the instance's OWN dissolve, not the crossing: see the header.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    float3 normal = normalize(input.WorldNormal);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    //THE MARBLE: bands across an axis, their phase swirled by two octaves of gradient noise - the marble
    //style's own construction (warp the input, not the output). Band-limited on the warp so a distant
    //wildcard's veins straighten and settle rather than boil.
    float drift = PulseTime * WildcardDrift;
    float warpLimit = saturate(1.0 - footprint * WildcardWarpCells * 4.0);
    float warp = (GradientNoise3(direction * WildcardWarpCells + drift)
        + 0.5 * GradientNoise3(mul(NOISE_ROTATE3, direction) * (WildcardWarpCells * 2.1) - drift)) * warpLimit;
    float field = 0.5 + 0.5 * sin((dot(direction, WildcardVeinAxis) * WildcardVeinBands + warp * WildcardVeinWarp) * 3.14159265);

    //THE CROSSING: the colour it is going to fills every part of the field under the progress, so it
    //arrives ALONG the veins rather than as a wipe. One pixel soft on the field's own derivative.
    float fieldWidth = max(fwidth(field), 1e-4);
    float arrived = smoothstep(field - fieldWidth, field + fieldWidth, WildcardProgress);

    float3 leaving = SrgbToLinear(PatternPrimaryColor);
    float3 going = SrgbToLinear(PatternSecondaryColor);
    float3 color = lerp(leaving, going, arrived);

    //THE OPAL: a play of colour along the boundary where the new colour is arriving, and round the rim,
    //its hue travelling with the view and the clock - so even at the instant the crossing sits at one end,
    //the ball still shows it is never one colour. Band-limited to a faint mean on the vein.
    float facing = saturate(dot(normal, eyeVector));
    float boundary = BandCoverage(field - WildcardProgress, WildcardOpalWidth, fieldWidth) * warpLimit;
    float hue = frac(field * 1.7 + (1.0 - facing) * 0.9 + PulseTime * WildcardOpalSpeed);
    float3 opal = WildcardRainbow(hue);
    float rim = pow(1.0 - facing, 2.5);

    SurfaceSpecular surface;
    surface.Highlight = WildcardHighlight;
    surface.Environment = WildcardEnvironment;
    surface.Smoothness = WildcardSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, normal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, normal, input.OcclusionData);

    shaded.rgb += opal * (boundary * WildcardOpalVein + rim * WildcardOpalRim) * occlusion;

    //Contract point 2: the cluster's own heartbeat on the mixed colour, and the still plane's StillEmission
    //when the wildcard is a loaded round (#252, #395) - both carried by BallEmission.
    shaded.rgb += BallEmission(color, input.WorldPosition, occlusion);

    //Contract point 3, in both meanings, and PatternPS's arithmetic deliberately.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);
        float peak = max(color.r, max(color.g, color.b));

        float3 lit = shaded.rgb + lerp(color / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelWildcard
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL WildcardPS();
    }
};
