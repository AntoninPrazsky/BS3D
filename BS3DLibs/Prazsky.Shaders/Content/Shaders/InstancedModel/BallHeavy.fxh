//---------------------------------------------------------------------------------------------------
// HEAVY (#333, redrawn in #631 from generated references)
//
// A sand-cast iron ball with the ball's own colour worn into its skin: a dark, MATTE, grainy casting,
// one mould flash standing proud round its waist, a machined boss stamped into one pole, and the type
// colour lying on the iron in PATCHES rather than tinting the whole sphere.
//
// ⚠ THE MASS IS NOT DRAWN HERE AND MUST NOT BE. What says "heavy" in this game is the BRANCH UNDER IT
// hanging lower -- the physics gives that feedback for free, which is the whole reason the kind is a
// mass and not an effect. This technique's job is narrower and is the half the physics cannot do: to
// say WHICH ball is the heavy one before it has done anything, on a still frame, at cluster distance.
//
// WHAT THE REFERENCES SAID (#631, nine studio renders of a cast sphere, three of them polished): weight
// is a MATERIAL, and the material is a casting. A polished sphere with a seam photographed as an anodised
// trackball whatever its tint; the sand-cast ones read as dense from across the room. Four things carry
// it, in the order the eye picks them up:
//  1. NO SKY IN IT. Every ordinary ball in this file mirrors the dome; a casting scatters it into a broad
//     dull sheen. At the overview stand-off the heavy ball is the one ball among sixty pixels of colour
//     with no highlight on it, which is a silhouette-grade cue and costs nothing to keep.
//     ⚠ THIS REVERSES #333's second build, which RAISED the sky reflection (1.30 / 1.25) to say "metal
//     rather than plastic". The references settle it the other way: metal that shines is a ball bearing,
//     and a ball bearing is not heavy. What separates this from a dark vinyl ball is now the grain, the
//     flash and the patches, none of which a vinyl ball wears.
//  2. THE COLOUR IS PAINT ON IRON. The tint is not crushed (#333's first build: a heavy yellow photographed
//     as chocolate brown) and not greyed (its second: a plum ball with a line on it, which the owner's sheet
//     could not tell from an ordinary dark ball). It stands at nearly full value in PATCHES over a cool
//     grey iron, the way the references' magenta sat on the casting - so the hue the player has to match
//     is on the ball undiluted, and the iron between the patches is what says the ball is not made of it.
//  3. THE FLASH. One raised ring where the two halves of the mould met, with a lip: lit along its top edge
//     and shadowed under it. A hard line survives to a pixel wide, which the cast grain never does.
//  4. THE BOSS. A flat machined disc stamped into the pole of the seam's axis, with a knurled rim - the
//     references' signature of a manufactured weight (a kettlebell's stamp, a sinker's). Close-up only; it
//     is under the band limit long before the flash is, and nothing else here depends on it.
//
// The flash, the boss, the grain and the patches are all in OBJECT space (contract point 6), so a heavy
// ball rolling is visibly rolling - a lip turning with the ball says so far better than the gores do.
//---------------------------------------------------------------------------------------------------

//The casting itself, in sRGB like every colour written here: a cool dark grey, lighter than the bomb's warm
//near-black (BombCasing) and cooler than the stone, so the three dark specials never share a value. Not the
//tint's own grey: iron is a colour of its own, and it is what shows between the patches.
//
//⚠ THE FIRST CUT WAS 0.235 AND PHOTOGRAPHED AS BLACK: on a matte body with a third of the sky and half the
//highlight, that albedo lit to nothing, and the ball read as a magenta ball with black blotches - a cow, not
//a casting. The references' iron is a MID grey in the light; what makes it read dark is the matte finish
//and the colour beside it, not the albedo.
static const float3 HeavyIronColor = float3(0.42, 0.41, 0.44);

//How much of the type colour's own value a patch keeps, and how far it is pulled towards grey on the way.
//The value is under the tint's own so a patch is paint ON the iron rather than a light beside it (the first
//cut at 0.82 was the brightest thing on the ball, and the iron between the patches read as holes in it);
//the desaturation is low ON PURPOSE - the patch is the hue the player matches, and both of #333's builds
//lost the hue by moving one of these too far.
static const float HeavyTintValue = 0.62;
static const float HeavyTintDesaturation = 0.28;

//The patches: two octaves of gradient noise over the object-space direction, cut SOFTLY at a threshold. A
//sharp cut on a low sine field was the first cut's cow: three hard blobs a hemisphere. The references' paint
//is a spray - soft-edged, irregular, thinned out by the grain - and gradient noise is what a spray's edge is.
//The field is centred on a half and its amplitude band-limits to zero, so the far limit converges on the
//coverage the threshold leaves: HeavyStainCover states that once rather than letting it walk.
static const float HeavyStainCells = 3.4;
static const float HeavyStainThreshold = 0.30;
static const float HeavyStainEdge = 0.10;
static const float HeavyStainCover = 0.78;

//How far the grain breaks a patch's edge: the paint is WORN, and wear shows first on what stands proud, so
//the mottle of the sand is stirred into the field the patches are cut from as well as into the paint itself.
static const float HeavyStainWear = 0.25;

//The sand: two octaves of gradient noise in object space, measured in CELLS across the unit direction (a
//cell is about a wavelength, so 24 is some fifty pits across the ball and the second octave twice that).
//Gradient noise rather than the stone's rectified sines deliberately - four sines at this frequency were a
//regular dot screen, StoneLumps' own golf-ball trap at a finer pitch; a hashed lattice has no lattice to
//show. Each octave band-limits itself against its own cell and converges on the field's mean, so a
//receding casting settles into a uniformly matte ball rather than into a smooth one - which is what sand at
//that distance is.
static const float HeavyGrainCells = 32.0;
static const float HeavyGrainDepth = 0.006;

//How much more of the paint the pits keep than the peaks: the colour is WORN. Modest, so a patch still reads
//as one patch and not as a stipple.
static const float HeavyGrainPaint = 0.2;

//The mould flash: the axis the two halves parted along, the half-width of the lip in dot(direction, axis),
//how far it stands proud of the casting, and the shadow band under it. The axis is deliberately NOT one of
//the gores' (the vinyl's welds run through the poles), so a heavy ball's line is one no ordinary ball wears.
static const float3 HeavySeamAxis = float3(0.0, 1.0, 0.0);
static const float HeavySeamWidth = 0.055;
static const float HeavySeamRelief = 0.045;
static const float HeavySeamUndercut = 0.035;
static const float HeavySeamUndercutDark = 0.45;

//The flash is metal that escaped between the mould halves and was never painted: how far it loses the patch,
//and how much lighter its bare metal is than the sand-cast skin - a flash is squeezed against the mould's
//face, so it is the one smooth-ish, brighter strip on a casting, and the reference's lip reads by it.
static const float HeavySeamBare = 0.8;
static const float HeavySeamLight = 1.45;

//The stamped boss at the +axis pole: its radius as a fraction of the ball's, the knurled rim's width in the
//same units, how many teeth the rim carries, how far the disc stands proud, and how much darker its
//machined face is than the sand around it.
static const float HeavyBossRadius = 0.26;
static const float HeavyBossRim = 0.05;
static const float HeavyBossTeeth = 40.0;
static const float HeavyBossRelief = 0.018;
static const float HeavyBossDark = 0.80;

//What the casting does with light: a broad dull sheen and very little of the sky. ⚠ ALL THREE UNDER 1, and
//that is the header's first point rather than the dull-plastic trap #333 recorded - the trap was a POLISHED
//ball asking for less light; a casting asks for less light because it scatters it. The boss is the one
//machined face on the ball and the one place a sharp reflection is right.
static const float HeavyHighlight = 1.25;
static const float HeavyEnvironment = 0.8;
static const float HeavySmoothness = 0.62;
static const float HeavyBossHighlight = 1.4;
static const float HeavyBossEnvironment = 1.1;
static const float HeavyBossSmoothness = 0.85;

//THE METAL (#631, the owner's verdict on the first cut: "less spotty, and more metallic - but matte"). A
//dielectric reflects ~4 % face-on whatever its Environment scale says, so no SurfaceSpecular figure can
//make a surface read as METAL: what does is a reflection tinted by the body colour and present face-on,
//which is what F0 = albedo means. So the casting carries its own: the sky along the reflected direction,
//blurred halfway to the sky's average (satin, not mirror - the "matte" of the verdict), times the painted
//colour, and the diffuse body is taken down by the same share so the ball does not simply get brighter.
static const float HeavyMetalSheen = 1.1;
static const float HeavyMetalBlur = 0.25;

//Two octaves of gradient noise over the unit direction, band-limited per octave and centred on zero, so a
//field cut from it converges on its own mean as the ball recedes rather than walking (the ice plates' trap:
//a relief nobody can resolve is rightly flat, but a COLOUR cut from a field that fades to zero drifts to
//whichever side of the threshold zero lies on). One cell is about a wavelength, so an octave of `cells`
//cells fades the way ReliefOctave would fade a wave of 2 * pi * cells. The domain is rotated between the
//octaves, Fbm3's own rule, so the two lattices never share an axis.
float HeavyMottle(float3 direction, float cells, float footprint)
{
    float coarse = GradientNoise3(direction * cells) * saturate(1 - footprint * cells * 2.0);
    float fine = GradientNoise3(mul(NOISE_ROTATE3, direction) * (cells * 2.1)) * saturate(1 - footprint * cells * 4.2);

    return 0.65 * coarse + 0.35 * fine;
}

//The sand, 0..1 about a half.
float HeavyGrain(float3 direction, float footprint)
{
    return saturate(0.5 + 0.9 * HeavyMottle(direction, HeavyGrainCells, footprint));
}

float4 HeavyPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    float3 normal = normalize(input.WorldNormal);

    //THE SAND, and the patches of paint lying on it. Both object space; both converge on their means.
    float grain = HeavyGrain(direction, footprint);

    float stainField = 0.5 + HeavyMottle(direction, HeavyStainCells, footprint) + HeavyStainWear * (grain - 0.5);
    float stainLimit = saturate(1 - footprint * HeavyStainCells * 4.2);
    float stain = lerp(HeavyStainCover,
        smoothstep(HeavyStainThreshold - HeavyStainEdge, HeavyStainThreshold + HeavyStainEdge, stainField), stainLimit);

    //THE FLASH: a band either side of the parting plane, and the shadow band under its lip on the -axis side.
    //Coverage rather than a step for the COLOUR, so it is one hard line at any distance; a rounded profile
    //for the HEIGHT, so the lip tilts the normal across its whole width - up on the +axis half, down on the
    //other - and the light draws it as a ridge, lit along the top and shadowed under. The first cut used the
    //coverage for both, and a plateau's edges are one pixel of gradient each: at four hundred pixels across
    //the ball, the "lip" was a flat grey belt.
    float along = dot(direction, HeavySeamAxis);
    float alongWidth = fwidth(along);
    float flash = BandCoverage(along, HeavySeamWidth, alongWidth);
    float lip = saturate(1.0 - (along * along) / (HeavySeamWidth * HeavySeamWidth));
    float undercut = BandCoverage(along + HeavySeamWidth + 0.5 * HeavySeamUndercut, 0.5 * HeavySeamUndercut, alongWidth);

    //THE BOSS: a disc round the -axis pole - the UNDERSIDE, because the player looks UP at the cluster and a
    //stamp on the top of a hanging ball is one nobody sees - measured in the pole's own plane (sin of the
    //polar angle), with a knurled rim. The teeth are a triangle wave round the axis, band-limited against
    //their own width and converging on their mean, so a distant boss is a plain darker disc and then nothing.
    float fromAxis = along < 0 ? sqrt(saturate(1 - along * along)) : 1.0;
    float fromAxisWidth = max(fwidth(fromAxis), 1e-5);
    float boss = saturate((HeavyBossRadius - fromAxis) / fromAxisWidth + 0.5);
    float bossFace = saturate((HeavyBossRadius - HeavyBossRim - fromAxis) / fromAxisWidth + 0.5);
    float rim = boss - bossFace;

    float teethWidth = 6.28318531 * HeavyBossRadius / HeavyBossTeeth;
    float teethFade = saturate(1 - footprint / teethWidth);
    float teeth = abs(frac(atan2(direction.z, direction.x) / 6.28318531 * HeavyBossTeeth) - 0.5) * 2.0;
    teeth = lerp(0.5, teeth, teethFade);

    //THE COLOUR: paint on iron. Patches keep the tint at nearly full value; the flash and the boss are bare.
    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 iron = SrgbToLinear(HeavyIronColor);

    float luminance = dot(primary, float3(0.2126, 0.7152, 0.0722));
    float3 tint = lerp(primary, luminance.xxx, HeavyTintDesaturation) * HeavyTintValue;

    float paint = stain * lerp(1.0 - HeavyGrainPaint, 1.0, grain) * (1.0 - HeavySeamBare * flash) * (1.0 - boss);
    float3 color = lerp(iron, tint, paint);
    color *= 1.0 + (HeavySeamLight - 1.0) * flash * (1.0 - paint);
    color *= 1.0 - HeavySeamUndercutDark * undercut;
    color = lerp(color, iron * HeavyBossDark, bossFace);

    //Contract point 6, and the relief that lights the lip: the flash and the boss stand PROUD (a flash is
    //metal that escaped between the mould halves), the sand is pitted, so the grain cuts IN.
    float height = lip * HeavySeamRelief + boss * HeavyBossRelief + rim * (teeth - 0.5) * HeavyBossRelief
        - grain * HeavyGrainDepth;
    float3 worldNormal = PerturbNormalFromHeight(normal, input.WorldPosition, height);

    //Matte everywhere but the machined face.
    SurfaceSpecular surface;
    surface.Highlight = lerp(HeavyHighlight, HeavyBossHighlight, bossFace);
    surface.Environment = lerp(HeavyEnvironment, HeavyBossEnvironment, bossFace);
    surface.Smoothness = lerp(HeavySmoothness, HeavyBossSmoothness, bossFace);

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData,
        float4(color * 0.35, 1), 1, 1, surface);

    //The metal's own tinted reflection - see HeavyMetalSheen. Occluded like everything the sky lights.
    float3 metalEye = normalize(EyePosition - input.WorldPosition);
    float3 metalSky = lerp(SkyRadiance(reflect(-metalEye, worldNormal)), (SkyColor + GroundColor) * 0.5, HeavyMetalBlur);
    shaded.rgb += metalSky * color * HeavyMetalSheen
        * SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2. A heavy ball carries no light of its own -- it is the one special here that emits
    //nothing at all, which is itself the reading: a bomb burns, a well pulls, this just sits there. What
    //it does keep is the cluster's own heartbeat through BallEmission, so it breathes with its neighbours
    //rather than standing outside them; the RenderSet gives it a slower, deeper beat than theirs. It rides
    //the painted colour, so what swells is the patches - the hue - and not the iron.
    shaded.rgb += BallEmission(color * 0.5, input.WorldPosition, occlusion);

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

technique InstancedModelHeavy
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL HeavyPS();
    }
};
