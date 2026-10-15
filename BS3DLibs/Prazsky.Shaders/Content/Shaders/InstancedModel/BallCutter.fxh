//---------------------------------------------------------------------------------------------------
// THE CUTTER (#766): the Cut power-up's round, a steel ball with a circular saw blade through its equator
//
// It was drawn with the zap's technique until #766, and that cost three things: it crawled in the bore while every
// other loaded round held still (#252), it was colourless while the gun round it glowed in the colour of the slot it
// replaced, and a player who knew the zap read it as "removes a colour" when it severs a storey (#692). The design
// references (Z-Image and klein, C:\Users\panrd\AI\sd\out\766-*) agreed on one figure that reads at a glance: a
// polished dark steel ball with a circular saw blade through it, its teeth standing just past the surface and glowing
// white-hot, and a slit of the same light where the blade goes in.
//
// TWO TECHNIQUES, TWO MESHES, ONE INSTANCE FRAME. InstancedModelCutter is the ball (the sphere's mesh at its LOD);
// InstancedModelCutterBlade is the blade (BallRenderSet's SawBladeMesh), drawn after it with the same frame. The frame
// is BallRenderSet's, built on the CPU each draw: its Y axis is the blade's normal, facing the eye and tipped back
// towards level, so the saw is seen face-on as every reference and the strip's disc draw it - and the ball's face, the
// pole the eye looks at, carries the saw's face: a ring of glowing hooked teeth round a hub with three bolts. That is
// what reads in the bore, where the barrel hides the blade and only the ball's cap shows (photographed: a level blade
// was invisible there). The frame turns about its axis on the wall clock, slowly in the bore and fast in flight, so
// the round turns as one rigid saw: contract point 6, the cue that it is spinning, is in object space as every ball's
// is, and nothing crawls over a still ball (#252) - nothing here reads PulseTime.
//
// ITS ACCENT IS THE DRAW'S TINT (PatternPrimaryColor): BasicEffectParamsProvider.CutterAccent, the one place the
// strip's disc, the aim beam, the muzzle collar, the rays and the launch smear take it from too. The ball itself has
// no colour of the thirteen - a cutter does not match - and the accent is only ever light: the slit and the edge.
//
// Neither technique declares a uniform, so nothing after this include moves in the constant buffer.
//
// THE SIX-POINT CONTRACT, for the ball: 1 the dissolve clip; 2 BallEmission, a steady steel floor (the renderer's
// PulseDepth is 0 for this draw, so there is no heartbeat to ride); 3 the ripple in both meanings; 4 SurfaceOcclusion;
// 5 the sea and the kill plane; 6 the saw's face, in object space. The blade keeps 1, 4 and 5; it never stands
// in the cluster, so it has no ripple, and it has no heartbeat.
//---------------------------------------------------------------------------------------------------

//THE STEEL. Polished and darker than the silver ball (a cutter must not read as Type11 in a metal level): a neutral,
//slightly cool reflectance with the metal style's own sky (MetalSky) along the mirror, a lathe's rings round the axis.
static const float3 CutterSteel = float3(0.17, 0.18, 0.21);
static const float CutterReflectance = 1.0;
static const float CutterHighlight = 0.9;
static const float CutterGrazingPower = 5.0;
static const float CutterRingFrequency = 46.0;
static const float CutterRingDepth = 0.0025;

//THE SLIT: the band either side of the equator where the blade goes into the ball, a gap with light inside it, as a
//share of the unit direction's height. Since the blade faces the eye this is the ball's outline - a lit rim where the
//teeth come out.
static const float CutterSlitHalfWidth = 0.06;
static const float CutterSlitGlow = 2.4;
static const float CutterSlitWhite = 0.55;

//THE SAW'S FACE, round the pole the eye looks at: a ring of hooked teeth glowing through the steel, as a saw blade seen
//face-on - the figure that still reads where the barrel hides the blade itself (in the bore only the ball's cap shows).
//Polar angles from the pole: the ring's inner edge, the gullets and the tips; an even count, so the teeth meet across
//atan2's seam. Every tooth's height is nudged by a hash and two are chipped short (#640).
static const float CutterSawTeeth = 16.0;
static const float CutterSawInner = 0.50;
static const float CutterSawGullet = 0.60;
static const float CutterSawTip = 0.76;
static const float CutterSawGlow = 2.2;

//THE HUB inside it, the arbor a blade is bolted to: a washer of brighter steel in a groove, three bolt heads on it a
//little off their thirds (#640) and the arbor's dark hole. With the teeth, the bolts are what show the spin.
static const float CutterHubRadius = 0.30;
static const float CutterHubGroove = 0.04;
static const float CutterBoltPolar = 0.17;
static const float CutterBoltRadius = 0.055;
static const float CutterArborRadius = 0.05;

//THE NICKS (#640): a few chips out of the polish where it has cut before, darker and rougher
static const float CutterNickFrequency = 5.3;
static const float CutterNickThreshold = 0.42;

//The steady glow every ball has so its shadowed side is not a hole (contract point 2); steel-grey, not the accent
static const float3 CutterBodyGlow = float3(0.05, 0.055, 0.065);

//THE BLADE: polished saw steel with a grinder's circular marks, its toothed rim glowing white-hot. The glow starts at
//the gullets and is whitest at the tips; it carries a little steady noise along the rim (#640), never a flicker.
static const float3 CutterBladeSteel = float3(0.55, 0.57, 0.60);
static const float CutterBladeGrindFrequency = 210.0;
static const float CutterBladeGrindDepth = 0.0012;
static const float CutterBladeGlowFrom = 0.50;
static const float CutterBladeTip = 0.585;
static const float CutterBladeGlow = 2.8;
static const float CutterBladeGlowFloor = 0.2;
static const float CutterBladeSheen = 0.10;

//The steel's light: MetalShade's arithmetic with this kind's constants - the rig's specular only (a metal has no
//diffuse), the scene's own lights, the sky along the mirror tinted by luminance at the body and honest at the rim.
float3 CutterSteelLight(float3 worldPosition, float3 worldNormal, float3 eyeVector, float3 f0, float occlusion)
{
    float3 diffuse = 0;
    float3 specular = 0;

    AddLight(normalize(KeyLightPosition - worldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, worldNormal, eyeVector, diffuse, specular);
    specular *= KeySunlight(worldPosition, worldNormal);
    AddLight(-DirLight1Direction, DirLight1DiffuseColor, DirLight1SpecularColor, worldNormal, eyeVector, diffuse, specular);
    AddLight(-DirLight2Direction, DirLight2DiffuseColor, DirLight2SpecularColor, worldNormal, eyeVector, diffuse, specular);
    specular *= DirLightStrength;
    AddSceneLights(worldPosition, worldNormal, eyeVector, diffuse, specular);

    float3 environment = MetalSky(reflect(-eyeVector, worldNormal));
    float grazing = pow(1 - saturate(dot(worldNormal, eyeVector)), CutterGrazingPower);
    float3 reflection = lerp(f0 * dot(environment, float3(0.2126, 0.7152, 0.0722)), environment, grazing);

    return (reflection * CutterReflectance + specular * f0 * CutterHighlight) * occlusion;
}

float4 CutterPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Polar angle from the nearer pole - the face the eye looks at - and the angle round it
    float polar = acos(saturate(abs(direction.y)));
    float azimuth = atan2(direction.z, direction.x);
    float polarWidth = max(fwidth(polar), 1e-4);

    //The hub: its groove, the washer inside it, the bolts and the arbor's hole
    float groove = BandCoverage(polar - CutterHubRadius, CutterHubGroove * 0.5, polarWidth);
    float washer = 1 - smoothstep(CutterHubRadius - polarWidth, CutterHubRadius + polarWidth, polar);
    float arbor = 1 - smoothstep(CutterArborRadius - polarWidth, CutterArborRadius + polarWidth, polar);

    float bolt = 0;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float place = 2.0943951 * i + 0.17 * sin(4.1 * i + 1.3) + (direction.y < 0 ? 1.0472 : 0.0);
        float3 boltAt = float3(sin(CutterBoltPolar) * cos(place), cos(CutterBoltPolar) * sign(direction.y + 1e-5), sin(CutterBoltPolar) * sin(place));
        float d = length(direction - boltAt);
        bolt = max(bolt, 1 - smoothstep(CutterBoltRadius - fwidth(d), CutterBoltRadius + fwidth(d), d));
    }

    //The saw's teeth round the hub: which tooth, how far through its pitch, and the outline there - a long back rising to
    //the tip and a short face falling, each tooth's height its own. Under a pixel a tooth is noise, so the outline settles
    //on its mean as the ball recedes and the ring stays a ring.
    float phase = azimuth / 6.2831853 * CutterSawTeeth + CutterSawTeeth;
    float through = frac(phase);
    float tooth = fmod(floor(phase), CutterSawTeeth) + (direction.y < 0 ? 7.0 : 0.0);
    float height = frac(sin(tooth * 12.9898) * 43758.5453);
    height = (tooth == 5.0 || tooth == 12.0) ? 0.35 : 0.82 + 0.18 * height;
    float profile = through < 0.75 ? through / 0.75 : (1 - through) / 0.25;
    float toothLimit = saturate(2.0 - footprint * CutterSawTeeth * 0.35);
    profile = lerp(0.5, profile * height, toothLimit);
    float outline = CutterSawGullet + (CutterSawTip - CutterSawGullet) * profile;
    float saw = (1 - smoothstep(outline - polarWidth, outline + polarWidth, polar))
        * smoothstep(CutterSawInner - polarWidth, CutterSawInner + polarWidth, polar);
    float sawHot = saturate((polar - CutterSawInner) / (outline - CutterSawInner));

    //The lathe's rings round the axis and the nicks, as a height for the normal; band-limited by ReliefOctave
    float rings = 0.6 * ReliefOctave(direction, float3(0, 1, 0), CutterRingFrequency, footprint)
        + 0.4 * ReliefOctave(direction, float3(0, 1, 0), CutterRingFrequency * 2.31, footprint);
    float nick = saturate((GradientNoise3(direction * CutterNickFrequency + 17.3) - CutterNickThreshold) * 6.0)
        * saturate(1 - footprint * CutterNickFrequency * 2.0);

    float relief = rings * CutterRingDepth * (1 - groove) - groove * 0.006 + bolt * 0.004 - nick * 0.004 - saw * 0.003;
    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, relief);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //The washer is brighter steel than the ball, a groove, the arbor and a nick darker, a bolt head brighter still
    float3 f0 = CutterSteel * (1 + 1.2 * washer) * (1 - 0.7 * groove) * (1 - 0.8 * arbor) * (1 - 0.45 * nick) * (1 + 0.6 * bolt);

    float4 shaded = float4(CutterSteelLight(input.WorldPosition, worldNormal, eyeVector, f0, occlusion), 1);

    //Contract point 2: a steady floor, steel-grey, through the one helper (PulseDepth is 0 for this draw)
    shaded.rgb += BallEmission(CutterBodyGlow, input.WorldPosition, occlusion);

    //THE LIGHT: the accent, whitened where it is hottest, a steady noise along it so it is not a drawn line (#640) - in
    //the slit round the outline and in the saw's teeth, each one pixel soft on its own derivative
    float3 accent = SrgbToLinear(PatternPrimaryColor);
    float flicker = 0.82 + 0.18 * GradientNoise3(direction * 7.0 + 3.1);
    float3 hot = lerp(accent, float3(1, 1, 1), CutterSlitWhite);
    float slit = BandCoverage(direction.y, CutterSlitHalfWidth, max(fwidth(direction.y), 1e-4));
    float3 sawLight = lerp(accent, float3(1, 1, 1), sawHot * sawHot * 0.8) * (CutterSawGlow * lerp(0.45, 1.0, sawHot));

    shaded.rgb = shaded.rgb * (1 - slit) + hot * (CutterSlitGlow * slit * flicker);
    shaded.rgb = lerp(shaded.rgb, sawLight * flicker, saw);

    //Contract point 3, both meanings - a cutter never stands in the cluster, but a technique owes them all
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);
        float3 lit = shaded.rgb + lerp(accent, 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

float4 CutterBladePS(PatternVertexShaderOutput input) : COLOR
{
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    //The blade's own frame is the instance's, so its plane is object XZ and the distance from the arbor is the radius
    float3 p = input.ObjectPosition;
    float r = length(p.xz);
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / 0.5;

    //The grinder's circular marks: a profile in the radius, band-limited on its own wavelength
    float grind = sin(r * CutterBladeGrindFrequency) * saturate(1 - footprint * CutterBladeGrindFrequency * 0.5 / 3.14159265);
    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, grind * CutterBladeGrindDepth);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    float4 shaded = float4(CutterSteelLight(input.WorldPosition, worldNormal, eyeVector, CutterBladeSteel, occlusion), 1);

    //THE EDGE: hot from the gullets out, white at the tips; the noise is in the blade's own frame, so it turns with it
    float3 accent = SrgbToLinear(PatternPrimaryColor);
    //All of the blade that shows past the ball glows, the most at the tips: it is the round's outline from far off
    float edge = smoothstep(CutterBladeGlowFrom, CutterBladeTip, r);
    float shows = step(CutterBladeGlowFrom, r);
    float noise = 0.7 + 0.3 * GradientNoise3(float3(p.x, p.z, 0.37) * 17.0);
    float3 hot = lerp(accent, float3(1, 1, 1), edge * edge * 0.75);

    shaded.rgb += hot * (CutterBladeGlow * shows * lerp(CutterBladeGlowFloor, 1, edge * edge) * noise) + accent * CutterBladeSheen;

    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelCutter
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL CutterPS();
    }
};

technique InstancedModelCutterBlade
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL CutterBladePS();
    }
};
