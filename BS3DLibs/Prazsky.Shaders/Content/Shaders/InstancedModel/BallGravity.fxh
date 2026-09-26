//---------------------------------------------------------------------------------------------------
// GRAVITY (#332)
//
// A well: the ball's own colour behind a lensed shell, with rings of light falling INWARD across it.
//
// ⚠ THE FIGURE'S JOB IS TO SAY "THIS IS WHY YOUR SHOT WENT THERE", and #332 states the alternative in
// one line: a curve the player cannot see coming is a bug with a physics explanation. So what is drawn
// is not a glow but a MOTION, and its direction is the whole message -- rings contracting towards the
// centre at a steady rate. The bomb's beat swells outward and the acid's liquid runs downward; both
// say "this will happen when you touch it". This one says "this is happening now, to anything that
// comes near", which is the only honest thing a field can say.
//
// The rings are in OBJECT space (contract point 6) and so is the shell's darkening, so a well rolling
// is visibly rolling. What is NOT object space is the lens: refraction is a fact about the eye, and a
// mirror that turned with the ball would read as paint.
//---------------------------------------------------------------------------------------------------

//The shell's own darkening towards the rim: a well is a heavy thing and it holds its own light in. It
//multiplies the ball's colour rather than replacing it, which is what keeps the colour readable -- the
//counterplay is shooting the well out, and that means matching its colour.
static const float GravityRimDarkening = 0.62;
static const float GravityRimPower = 2.2;

//The rings: how many are visible at once across the DISC, and how tight each one is. Few and broad, so
//they read as a pulse travelling rather than as stripes -- a set of hard rings is a target, not a field.
//
//⚠ THREE AT 0.30 WAS THE FIRST SET AND THE OWNER'S REPORT KILLED IT: "the blue bands are too narrow --
//from any distance they are almost invisible". Two things were wrong and the count was the smaller one.
//The parameter (see `across` in GravityPS) crowded every ring into the rim, so at three they landed at
//55 %, 87 % and 99 % of the drawn radius and the outer two were thinner than the inner one; photographed
//at 14, 26 and 40 units, what survived past about 20 was a single thin crescent on the limb and by 40
//the ball was flat violet. Spaced across the disc instead, TWO rings at 0.42 fill it -- measured on the
//well's own pixels, the figure's luminance contrast goes 19.8 -> 23.3 at 26 units and 18.9 -> 20.5 at 40,
//and what the eye gets back is a target contracting inward rather than a lit edge.
static const float GravityRingCount = 2.0;
static const float GravityRingWidth = 0.42;

//How fast they fall inward, in rings per second. NEGATIVE is the whole point of this technique: the
//pattern moves towards the centre. Slow enough to read as a pull and not as a strobe.
static const float GravityRingSpeed = 0.85;

//What the rings are made of: a cold violet-white, well clear of every one of the thirteen ball colours
//and clear of the zap's electric blue. What separates it from the zap at a glance is that this figure is
//SMOOTH and continuous where an arc is thin and broken.
static const float3 GravityRingColor = float3(0.72, 0.60, 1.0);

//How much light the rings carry, and the floor the figure converges to once they are under a pixel.
//BombFarGlow's argument once more: a distant well has to still be nameable, and what survives is a ball
//with a dark rim and a violet cast. The gain went 0.85 -> 1.0 with the re-spacing above, which is the
//smallest half of that change and is here so the widened band does not read softer than the thin one did.
static const float GravityRingGain = 1.0;
static const float GravityFarGlow = 0.28;

//Where the rings stop being worth drawing, measured against a RING's own width -- the ice crack's rule,
//which the zap's arcs, the acid's lanes and the infection's blotches all follow.
static const float GravityBandLimit = 0.95;

//The lens: how much the shell bends what is behind it, and how sharply that grows towards the rim. It is
//a fake -- the scene is not re-sampled -- so what it actually does is push the ENVIRONMENT reflection
//outward, which at a grazing angle is most of what a refracting sphere shows anyway.
static const float GravityLensStrength = 0.55;
static const float GravityLensPower = 1.8;

//A dense, polished body: a well is not wet like an acid and not frosted like ice.
static const float GravityHighlight = 0.70;
static const float GravityEnvironment = 0.66;
static const float GravitySmoothness = 0.88;

//How deep the rings cut into the shell, in world units. NEGATIVE relief -- the rings are troughs, light
//falling INTO the ball, where the acid's drips are beads lying on it. The sign is the second half of the
//inward reading.
static const float GravityRingRelief = 0.010;

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

    //How far around the ball from the point facing the eye. THE rings' parameter, and it is the one thing
    //here that is deliberately view-relative: a well seen from any side has to show its rings converging
    //on the point the player is looking at, because that is what "falling in" looks like from outside.
    float facing = saturate(dot(normal, eyeVector));
    float around = 1.0 - facing;

    //⚠ THE RINGS ARE SPACED IN THE DISC'S OWN RADIUS AND NOT IN `around`, AND THAT IS A FIX RATHER THAN A
    //PREFERENCE. `around` is 1 - cos(theta) and a sphere's screen radius is sin(theta), so rings evenly
    //spaced in it are NOT evenly spaced on screen: at three rings they land at 55 %, 87 % and 99 % of the
    //drawn radius, i.e. two of the three inside the outer eighth of the ball, each of them thinner than
    //the last. The owner's report was that the bands are too narrow to see from a distance, and that
    //crowding is the whole of why. `across` is sin(theta) straight out of the same dot product, so a ring
    //is a band of the DISC and the count means what it says.
    float across = sqrt(saturate(1.0 - facing * facing));

    //One ring's width in that parameter is 1/GravityRingCount, so the limit is measured against that.
    //⚠ Measured after the re-spacing, on five CONSECUTIVE frames at 40, 60 and 80 units: the figure fades
    //out on this limit without crawling first. The well's frame-to-frame change is about twice an ordinary
    //ball's in the same frames (mean 12-16 codes against 7-9), which is the rings MOVING and not speckle -
    //blown up eight times, a distant well is a smooth ball with no figure left on it at all.
    float limit = saturate(GravityBandLimit - footprint * GravityRingCount);

    //INWARD: the phase SUBTRACTS time, so a ring's position decreases and the pattern travels towards
    //the centre. Reversing this one sign is the difference between a well and a beacon.
    float phase = across * GravityRingCount - PulseTime * GravityRingSpeed;
    float ring = pow(saturate(1.0 - abs(frac(phase) - 0.5) / max(GravityRingWidth, 1e-4)), 2.0);

    //Converging to a floor rather than to nothing, so a distant well is still a well.
    float rings = lerp(GravityFarGlow, ring, limit);

    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 ringColor = SrgbToLinear(GravityRingColor);

    //The shell darkens towards the rim, multiplying the ball's own colour rather than replacing it: the
    //player has to be able to read WHICH colour this is, because that is the shot that removes it.
    float rim = pow(around, GravityRimPower);
    float3 color = primary * (1.0 - GravityRimDarkening * rim);

    //Contract point 6's other half: the rings are troughs cut INTO the shell. Negative, where the acid's
    //drips are positive, and the sign is the second half of what makes this read as a pull.
    float height = -ring * GravityRingRelief;
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

    //Contract point 2. The rings carry their own light on a floor, the bomb's measured lesson inherited
    //rather than rediscovered a fifth time, and the heartbeat rides the ball's own colour on top -- so a
    //well still breathes with the cluster and is still visibly ITS colour.
    shaded.rgb += ringColor * rings * GravityRingGain * occlusion;
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

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
