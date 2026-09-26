//InstancedModel.fx: the moulded vinyl beach ball (BallShading.Vinyl, technique InstancedModelPattern). Its own
//uniforms (PatternGoreCount and the rest) are declared in BallCommon.fxh, where they always stood: the
//declaration order is the constant buffer's layout, so a uniform stays where it was first declared.

float4 PatternPS(PatternVertexShaderOutput input) : COLOR
{
    //The object-space radius is the ball's own radius, which turns the pixel footprint below into the
    //units the relief is written in without the shader having to be told how big a ball is
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //The dissolve cut, before anything else is worth computing. Branchless — a select rather than an if,
    //because Dissolve varies per instance and a branch on it would diverge inside a single draw call.
    //At the settled value of 0 this reduces to clip(noise), and the hash is never negative, so every ball
    //that is not transmuting keeps all of its pixels and pays a handful of ALU for the privilege.
    //
    //The cell is a block of the SCREEN, so input.Position is read as what SV_POSITION is in a pixel shader:
    //the pixel's centre in target pixels. Snapped to the block grid with floor, so every target sample
    //inside one block hashes the same and the resolve cannot average the dither away (see DissolvePixelSize).
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    //sin(N * azimuth) stays continuous across the atan2 branch cut for integer N, so neither the
    //value nor its screen-space derivative jumps there and the seam needs no special handling
    float azimuth = atan2(direction.z, direction.x);
    float gore = sin(PatternGoreCount * azimuth);

    //Linearized before the blends: these crossfades run along the antialiased gore edges, so they are
    //averaging light across a pixel and have to do it in linear
    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 secondary = SrgbToLinear(PatternSecondaryColor);

    float3 color = lerp(primary, secondary, AntialiasedStep(PatternGoreThreshold, gore));

    //Discs at the poles, where the gores would otherwise converge into an aliasing mess
    float pole = abs(direction.y);

    color = lerp(color, primary, AntialiasedStep(PatternCapExtent, pole));
    color = lerp(color, secondary, AntialiasedStep(PatternCapExtent + PatternRingWidth, pole));

    //The panels are welded together, not painted on: press a groove in along every gore boundary and
    //around the rim of each polar disc. The gore grooves fade out towards the poles, where the
    //boundaries crowd together and the disc takes over anyway.
    float goreSeam = (1 - smoothstep(0, PatternSeamGoreWidth, abs(gore - PatternGoreThreshold))) * saturate((PatternCapExtent - pole) * 8);
    float capSeam = 1 - smoothstep(0, PatternSeamCapWidth, abs(pole - PatternCapExtent));

    //How much surface one screen pixel covers, over the ball radius — the yardstick every feature is
    //band-limited against. It shrinks when the scene is supersampled, which is exactly why raising the
    //render resolution buys back the fine octaves instead of just making the same mush smoother.
    //Kept branchless: ddx/ddy need every pixel of a quad to have taken the same path.
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Relief and welds ride in one height field, so a single perturbation covers both
    float seams = (goreSeam + capSeam) * saturate(1 - footprint * PatternSeamFrequency / 3.14159265);
    float height = SurfaceRelief(direction, footprint) * PatternReliefStrength - seams * PatternSeamDepth;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);

    //The balls carry their own relief; the scene cavity term is not it
    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1);

    //Light carried through the shell from behind. A ball lit from the far side glows around its rim
    //instead of going flatly black, which is what tells the eye the thing is a skin around a volume
    //rather than a painted solid — and is half of why it can read as alive.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float throughShell = pow(saturate(dot(-worldNormal, towardsKey)), 2);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    shaded.rgb += throughShell * TranslucencyStrength * DirLight0DiffuseColor * color * occlusion;

    //Emission: the ball radiates its own color rather than only reflecting what falls on it, and does it
    //on a heartbeat whose phase runs with world position - a wave through the cluster, not a strobe.
    //The resting part follows the occlusion since #303 and the beat's swing does not: this line said
    //"emission is not occluded" for a long time, on an argument BallEmission's own comment now answers.
    //
    //The ball glows with its own color, not with the pattern's: the gores and the polar discs are white,
    //and emitting through them made half of every ball radiate white light, which is both the wrong color
    //and the reason they read as washed out. What is alive here is the ball, not its paint job.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //And on top of the resting breath, the ripple: the light that runs out through the cluster from
    //wherever a ball has just landed. WHEN this ball takes its turn was decided on the CPU by walking the
    //balls that touch each other outwards from the impact, so what arrives here is only how brightly it is
    //flaring this frame - the walk is a question about the cluster's connectivity, and a wave evaluated
    //from a world-space distance here would run straight through the holes a played cluster is full of
    //instead of around them.
    //
    //Branched on the UNIFORM, not on the per-instance value: the strength is the same for every instance in
    //a draw call, so the branch cannot diverge, and a renderer that never ripples pays nothing at all.
    [branch]
    if (RippleStrength > 0)
    {
        //The flare is mostly WHITE with the ball's hue in it, and that is not a stylistic preference - it is
        //the only thing that reads. Adding light in the ball's own colour piles it into the one channel that
        //is already near the top of the ACES curve, so a red ball taking a full-strength flare goes from
        //bright red to very slightly brighter red and the wave is invisible; measured, it was there in the
        //instance data at 0.97 and could not be seen on screen at all. Lifting the channels the ball does
        //NOT have is what turns it white-hot, which is what "lighting up" looks like.
        //
        //Normalising the hue to peak 1 first also settles the dark types: primary runs from a full-strength
        //red down to the 8-ball's 0.045 grey, and multiplying that raw would leave the black balls out of
        //the wave entirely. Light passing through a cluster does not care what colour the ball under it is.
        float amount = abs(input.Ripple);
        float peak = max(primary.r, max(primary.g, primary.b));

        float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount);

        //The alarm REPLACES the ball's colour rather than adding to it, and that is the whole difference
        //between a warning and a wash. Added, a red flare on a green ball is green plus red, which is yellow;
        //on a red one it is a slightly brighter red, and on black a pale grey - every ball came out a
        //different pastel and none of them said "red". Blended, the cluster momentarily TURNS red, which is
        //a thing the player cannot mistake for the scene doing something of its own.
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        //A select and not an if, for the reason the dissolve's clip is one: the sign varies PER INSTANCE, so
        //a branch on it would diverge inside a single draw call. Both sides are a handful of ops.
        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //The hand-rolled vinyl sheen that used to sit here is gone: it was a Fresnel reflection of the sky,
    //which ShadePixel's specular ambient now does for every surface with a real dielectric F0 behind it.
    //Two Fresnel sky terms stacked on one sphere - where a grazing angle covers most of what you can see
    //of it - is what was bleaching the balls out under a bright dome.

    //Submerge fade: a ball below the sea level dims into the deep-water tint and becomes transparent over a
    //shallow band, so it reads as sinking into dark water rather than being cut off by the opaque surface
    //(see SeaLevelY). Disabled (a no-op) off the sea scene, where SeaFadeDepth is pushed <= 0.
    //
    //The colour is scaled towards zero WITH the alpha, not only lerped to the tint: this output rides
    //premultiplied alpha, and a fade that leaves rgb standing turns every faded pixel ADDITIVE. One sinking
    //ball hides it (the residue is the near-black tint, once), but a released cluster piles hundreds of
    //half-sunk balls into the pool standing in the drain (#132), and their residues stack into a pale glowing
    //mush over the dark water. Found the moment the pool gave them something dark to stack against.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    //And the kill plane's own fade (#192) on top of it, for the ball about to be culled under the island.
    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelPattern
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PatternPS();
    }
};
