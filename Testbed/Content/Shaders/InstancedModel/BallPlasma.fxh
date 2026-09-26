//===================================================================================================
//PLASMA ORB (#309): the desktop plasma-ball toy - a dark, nearly empty globe with thin bright filaments
//of ionised gas crawling across the inside of it, tinted by the type colour.
//
//IT IS THE ONLY STYLE WHOSE READ IS MOTION, and that is its whole reason to exist beside the other
//seven. Every one of them is a still material photographed under a moving camera; this one is alive
//whether anything is happening or not. A screenshot says almost nothing about it, which is a fact about
//how it must be JUDGED and not a complaint.
//
//THE FILAMENTS ARE A DOMAIN WARP: one field displaces the sample point of a second, and the narrow band
//where the second is near zero is drawn as a line. Warping the INPUT is what makes an arc WRITHE rather
//than merely ripple - the same reason the marble warps its vein coordinate - and it is what costs this
//style its place as the dearest of the eight: two noise evaluations where every other has one, animated,
//so nothing about it is cacheable.
//
//ITS COLOUR IS EMISSION, WHICH MAKES IT THE STRONGEST OF ALL EIGHT ON HUE AND THE WEAKEST ON VALUE.
//Emissive thin lines are read directly - not diluted by ambient, not filtered by transmission, not
//tinted by a backdrop, which are the four ways the other styles lose their hue. But the colour lives in
//thin bright lines over a dark shell, so a CLUSTER reads dark, and against a bright dome the balls
//become dark discs with faint colour in them. This style is scene-bound, exactly as the metal is, and
//that is a legitimate thing for a style to be: a level names its own.
//
//THE 8-BALL SOLVES ITSELF HERE, and it is worth saying because it did not in three of the other styles.
//The filament colour is the tint NORMALISED to its peak channel - the ripple's own trick - so a
//saturated red gives red filaments and Type8's 0.045 grey gives WHITE ones. A white-hot discharge in a
//black globe is not a compromise; it is what that toy actually looks like.
//===================================================================================================

//How far the first field displaces the second's sample point. The whole character of the arcs: at zero
//they are smooth rings, and it is the warp alone that makes them writhe, fork and rejoin.
float PlasmaWarp;

//How brightly a filament burns. The one figure the C# side states, because it is the whole of the
//style's colour and the only thing standing between a cluster and darkness.
float PlasmaGlow;

//How fast the arcs crawl, in radians of phase a second. Slow: a plasma ball's discharges wander, and
//anything quick enough to notice as ANIMATION stops reading as something alive and starts reading as a
//loop.
float PlasmaSpeed;

//The directions the warp and the filament field are read along, and their frequencies. Low, because a
//filament has to be resolvable at the stand-off a level is played from - the lesson the metal's brush
//and the wool's strands both record.
static const float3 PlasmaWarpA = float3(0.66, 0.49, -0.57);
static const float3 PlasmaWarpB = float3(-0.41, 0.81, 0.42);
static const float3 PlasmaWarpC = float3(0.52, -0.36, 0.77);
static const float3 PlasmaField = float3(0.34, 0.79, 0.51);

static const float PlasmaWarpFrequency = 3.1;
static const float PlasmaFieldFrequency = 3.5;

//How thin an arc is. High: a discharge is a filament and not a band, and this exponent is most of what
//separates "plasma ball" from "marbled sphere".
static const float PlasmaSharpness = 11.0;

//What the globe is worth between the filaments: nearly nothing, because the thing is mostly empty. The
//dark shell is what the arcs have to be bright against.
static const float PlasmaShell = 0.10;

//The core the arcs reach out of, and how hard it is crowded into the middle of the disc. It is what
//makes them read as REACHING rather than as a wire cage painted on the outside.
static const float PlasmaCore = 2.2;
static const float PlasmaCorePower = 3.0;

//The globe's own boundary: a faint Fresnel edge, so the thing has a surface even where no arc is
//touching it. In the ball's own colour, never the sky's - a plasma globe reflects almost nothing.
static const float PlasmaEdge = 0.35;
static const float PlasmaEdgePower = 3.5;

//A band-limited wave with a PHASE, which is what ReliefOctave cannot take and this style cannot do
//without: the arcs move by advancing the phase, and the band-limit still has to fade a wave out once a
//pixel spans it or a cluster of these boils.
float PlasmaWave(float3 position, float3 waveDirection, float frequency, float phase, float footprint)
{
    return sin(dot(position, waveDirection) * frequency + phase)
        * saturate(1 - footprint * frequency / 3.14159265);
}

float4 PlasmaPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;
    float time = PulseTime * PlasmaSpeed;

    //The warp, in OBJECT space (contract point 6): the arcs are inside the globe and they turn with it.
    //Three components read along three directions at three rates, so the displacement never settles.
    float3 warp = float3(
        PlasmaWave(direction, PlasmaWarpA, PlasmaWarpFrequency, time, footprint),
        PlasmaWave(direction, PlasmaWarpB, PlasmaWarpFrequency * 1.27, time * 1.31, footprint),
        PlasmaWave(direction, PlasmaWarpC, PlasmaWarpFrequency * 0.83, time * 0.74, footprint)) * PlasmaWarp;

    //...displacing the field the filament is cut out of. Thin lines at its zero crossings, the same
    //profile the marble's veins use, at an exponent that makes them filaments rather than bands.
    float field = PlasmaWave(direction + warp, PlasmaField, PlasmaFieldFrequency, time * 0.91, footprint);
    float filament = pow(saturate(1 - abs(field)), PlasmaSharpness);

    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float3 worldNormal = normalize(input.WorldNormal);
    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The discharge's colour: the tint's hue at full saturation, which for the 8-ball's grey IS white -
    //see the header, a white-hot discharge in a black globe is what that toy looks like. Its BRIGHTNESS
    //is the tint's own luminance (#315). The header's claim that the darkest type needs no special case
    //here was true of black alone and false of the PAIR: SILVER normalises to the same near-white, and
    //the two measured 4.7 dE apart. Black still burns white; it burns dimmer than silver's now.
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 hue = primary / max(peak, 1e-3);
    float3 discharge = hue * TintEmission(primary);

    //The arcs reach out of a core, so they brighten towards the middle of the disc rather than lying
    //evenly over the sphere.
    float centre = pow(saturate(dot(worldNormal, eyeVector)), PlasmaCorePower);

    //Contract point 2, and it RIDES ON THE FILAMENTS rather than standing beside them. A plasma ball has
    //an obvious reason to pulse, and a second independent brightness on top of an already-moving surface
    //reads as two effects fighting.
    float beat = Heartbeat(PulseTime * PulseSpeed - dot(input.WorldPosition, PulseDirection) / max(PulseWavelength, 1e-4));

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Occluded, and for the reason #258 measured on the film: every ball in a pile of these reaches the
    //eye and a pixel shows the sum over four or five of them, which at full strength turns the middle of
    //a cluster into a flat wash with no ball in it. Since #303 the W channel also carries burial, so this
    //same line makes a deep plasma dim progressively - linearly, this style's identity being its glow,
    //where the opaque styles' resting emission goes with the square (see BallEmission).
    float3 glow = discharge * filament * PlasmaGlow * lerp(1, PlasmaCore, centre)
        * lerp(1 - PulseDepth, 1, beat) * StillEmission * occlusion;

    //The globe: nearly empty between the arcs, with a faint edge so it has a surface at all.
    float edge = pow(1 - saturate(dot(worldNormal, eyeVector)), PlasmaEdgePower);
    float3 shell = primary * (PlasmaShell + edge * PlasmaEdge) * occlusion;

    float4 shaded = float4(glow + shell, 1);

    //Contract point 3, both meanings, PatternPS's arithmetic. Over a dark shell with thin bright lines
    //the alarm reads immediately; the landing flare washes the filaments out at its peak, which is right
    //- the ball flares - but it is the thing to look at first if this style is ever retuned.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        float3 lit = shaded.rgb + lerp(hue, 1.0, RippleWhiten) * (RippleStrength * amount);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelPlasma
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PlasmaPS();
    }
};
