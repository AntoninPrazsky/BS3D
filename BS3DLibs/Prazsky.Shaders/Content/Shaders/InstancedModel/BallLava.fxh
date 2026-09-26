//===================================================================================================
//MOLTEN CRUST (#310): a cooling lump of lava. A near-black basalt crust cracked into plates, with the
//molten interior glowing through the seams in the type colour. The crust is matte and rough; the seams
//are the only bright thing on the ball, and they breathe.
//
//IT IS THE INVERSE CONSTRUCTION OF THE PLASMA ORB, and the two should be kept that way: plasma is thin
//bright lines over an EMPTY dark shell that WRITHE; lava is a solid heavy crust whose seams BREATHE.
//One is electricity, the other is heat. They share the emissive-colour trick and nothing else - this
//one keeps its relief, its lighting and its weight.
//
//ITS COLOUR LIVES IN THE EMISSION, which is the argument for building it and it is a strong one: an
//emissive seam is read directly, not diluted by ambient, not filtered by a backdrop, not tinted by a
//reflection and not transmitted through anything - the four ways every other style can lose its hue.
//
//THE 8-BALL AGAIN, AND THE PLASMA'S ANSWER AGAIN. A black glow in a black crust is nothing, and this
//style is worse placed than most because the crust is ALREADY black. The seam colour is the tint
//NORMALISED to its peak channel, so the 8-ball's grey glows WHITE-HOT - which for lava is not even a
//departure: the hottest part of a real flow is the whitest.
//
//WHAT IT GIVES UP KNOWINGLY: value separation. The crust is the same darkness on all thirteen, so what
//tells them apart is the seam colour alone. The crust carries a little of the tint (LavaCrustTint) so
//they are not literally identical, but this style leans on hue harder than any other except the plasma.
//===================================================================================================

//How many plate seams run over the ball. Low: a cooling crust breaks into a handful of big plates, and
//a dense net reads as gravel rather than as a cracked shell.
float LavaSeamFrequency;

//How wide a seam is. Wider than the ice's cracks, deliberately - a crack is a plane seen edge-on, and
//this is a GAP with molten rock at the bottom of it.
float LavaSeamWidth;

//How brightly the molten interior glows through. The one figure the C# side states: it is the whole of
//this style's colour, and over a black crust there is nothing else to see the ball by.
float LavaGlow;

//The three line fields' directions. Not aligned to the sphere's poles or to each other, so the plates
//come out irregular rather than as a lattice.
static const float3 LavaSeamA = float3(0.71, 0.48, -0.52);
static const float3 LavaSeamB = float3(-0.39, 0.83, 0.40);
static const float3 LavaSeamC = float3(0.55, -0.34, 0.76);
static const float2 LavaSeamRatio = float2(1.29, 1.77);

//How far the seams wander off the great circles three plain sine fields would cut, and at what scale.
//See the note where it is used: without this the style is a wire cage, which is what it looked like when
//it was first built.
static const float LavaSeamWander = 0.30;
static const float LavaSeamWanderFrequency = 2.1;

//How dark the crust is, and how much of the ball's own tint it keeps. Basalt is nearly black; the tint
//that survives is what stops thirteen crusts being literally the same object.
static const float LavaCrustDark = 0.16;
static const float LavaCrustTint = 0.62;

//THE HEAT ROUND A SEAM, and #338's answer to "the black crust takes up too much of the ball". The
//owner's complaint is about AREA - the type colour was confined to the seams, which are a fifteenth of
//the surface, so twelve of every thirteen pixels on the ball were black whatever colour it was.
//
//The obvious fixes are both wrong, and stating why is most of this decision. LIGHTENING THE CRUST throws
//away the argument the style is built on: an emissive seam is the one colour in this game that arrives
//undiluted - not filtered by ambient, a backdrop, a reflection or a transmission - and a crust carrying
//real tint is a diffuse surface again, which is thirteen dark grey balls under a dusk dome. Simply
//WIDENING THE SEAM until it covers the ball turns a cracked shell into a coloured ball with black spots,
//and the cracked shell is the read.
//
//So the colour comes from heat instead, which costs the style nothing it was built for: crust within a
//plate-width of a crack is THIN AND HOT and glows dully, and that glow is emission like the seam's. It
//grades out from the seam rather than flooding, so the plates stay plates and the ball gains a coloured
//half without a single diffuse pixel changing hue. The halo is never carried towards LavaIncandescent -
//only the seam's own core is white-hot, and cooler rock keeps its hue, which is also what stops the
//halo from undoing #315's separation.
//
//The width is a MULTIPLE of the seam's, capped by what SeamLine can express: it tests |sin| against the
//width, so a width at or over one never resolves to zero anywhere and the whole ball would glow. At the
//shipped figures this lands at 0.68.
//
//THE FALLOFF IS THE PART THAT WAS NOT OPTIONAL, and the first pass proved it by leaving it out. Written
//as (wide field MINUS seam) the halo is a PLATEAU - uniformly lit right out to its edge - and the ball
//came back a fat-banded NEON CAGE with the plates reduced to black islands between the bars. That is the
//PLASMA, which this style's own header says at length to keep it away from. A power over the wide field
//makes it a gradient instead: bright against the gap, dark a plate-width away, which is what a
//temperature falling off through rock looks like and what leaves the plates reading as plates.
//⚠ #395 WIDENED THE SEAM AND LOWERED THIS IN THE SAME BREATH, and the pair is the point. The owner asked
//for thicker lines; the halo's width is a MULTIPLE of the seam's, so widening the seam alone would have
//carried the halo from 0.68 to 0.87 of SeamLine's ceiling and flooded the plates - the "coloured ball
//with black spots" this comment already warns about, arriving through the back door. Lowering the
//multiple from 1.9 holds the halo's absolute width where it was measured (0.36 x 1.9 = 0.684 before,
//0.46 x 1.55 = 0.713 now) while the bright line inside it gets a quarter wider.
static const float LavaHeatWidth = 1.55;
static const float LavaHeatGlow = 0.34;
static const float LavaHeatFalloff = 2.6;

//The crust's own roughness, on the same octave sum the vinyl skin uses for its moulding. This is one of
//the few styles that WANTS that relief kept rather than removed: plates have to read as broken stone.
static const float LavaCrustRelief = 0.024;

//How deep a seam is cut into the crust, so the plates stand proud of the gaps between them.
static const float LavaSeamDepth = 0.03;

//What the hottest core of a seam is carried towards. Incandescence runs to white through yellow, so the
//middle of a seam loses its hue while its edges keep it - which is what makes it read as HOT rather
//than as a coloured line painted in a groove.
static const float3 LavaIncandescent = float3(1.0, 0.86, 0.62);
static const float LavaCorePower = 6.5;

//HOW FAR THE CORE IS ALLOWED TO GO, and #395's whole substance. The carry above was UNCAPPED: at the
//middle of a seam on a bright tint it reached LavaIncandescent outright, so the brightest pixels on the
//ball - the ones the eye locks onto across a cluster - were the same near-white on every colour that
//could get there. The disc average never showed it, and that is why it stood for three issues: measured
//-Whole under the volcano the palette looks ordinary (tightest pair orange/brown 7.4 dE, against the
//vinyl's own 6-7), because the average is dominated by a crust that is the same darkness on all thirteen
//and reports a colour the eye never isolates. MEASURED OVER THE BRIGHTEST TENTH OF EACH DISC - which is
//what a glance actually reads on this style - the cores came back at a mean saturation of 0.28 across
//the Eruption's eight inks, with silver at 0.00, black 0.06, blue 0.07 and white 0.11: eight of thirteen
//balls wearing a neutral net. The owner reported it from play as colours that "look almost the same",
//and on Volley as a suspected BUG IN THE MATCH RULE - a cluster that would not release, when what had
//actually happened was a shot at a colour that only looked similar.
//
//So the core keeps the ball's hue and gets its heat from BRIGHTNESS instead. The two figures are a pair
//and have to be read together: the carry says how much hue the core may lose, the lift restores the cue
//that loss was carrying. Without the lift a capped carry is simply a duller ball - the hottest point
//stops being hot as well as stopping being white, which is the opposite of the ask.
//
//⚠ THE CARRY IS NOT ZERO, and that is deliberate rather than timid. A seam whose core is exactly its own
//hue reads as a coloured line painted in a groove, which is the failure the comment above this one names
//and the reason the carry exists at all. A third of the way there keeps the "metal at temperature" read
//- the core is perceptibly warmer and less saturated than the seam's edge - while leaving the hue in
//charge of which ball it is.
static const float LavaCoreCarry = 0.30;

//What the hottest core burns at, as a multiple of the seam's own glow (#395) - the cue that replaces the
//desaturation. Heat now reads as INTENSITY rather than as loss of colour, which is the direction the
//owner's own words point: "the hottest point still reads as the ball's colour, just brighter".
static const float LavaCoreLift = 1.4;

//HOW DEEP THE SEAM'S OWN HUE IS CUT (#395). The molten colour is the tint normalised to its peak channel,
//which sets the brightest channel to 1 and leaves the other two at whatever ratio the tint had - and those
//ratios are what thirteen inks chosen for a DIFFUSE ball happen to carry, not what a glowing line needs.
//Raising the normalised hue to a power above one holds the peak channel exactly where it is and pulls the
//other two down, so a warm ink gets warmer and a cool one cooler.
//
//⚠ It is deliberately NOT SaturateTint, which is the obvious call and does nothing here: that function's
//first step is primary/peak, which is precisely what this style already had - it rescales chroma rather
//than deepening it, and on a hue already normalised it is the identity. Worth knowing before reaching for
//it again.
//
//A neutral tint is (1,1,1) once normalised, and any power of one is one - so silver, white and black do
//not move, which is the same property that made SaturateTint right for the gem. They cannot be separated
//by hue in any case; what separates them is TintEmission's luminance term.
static const float LavaHuePower = 1.7;

//HOW HARD THE TINT'S OWN LUMINANCE IS SPENT ON SEPARATING THE WARM INKS (#395), and the third of the three
//levers. #315 already made the glow ride the tint's luminance, which is what value separation this style
//has - the crust is the same darkness on all thirteen by design, so the glow is the only place value can
//live. But it rides it through TintEmission's sqrt, which COMPRESSES: over the Eruption's warm three the
//shared curve puts orange at 0.80, red 0.68 and brown 0.61, a spread of 1.3x across inks whose diffuse
//luminances differ by 2.1x.
//
//This is that curve with the compression opened up, and 1 is no compression at all. The measurement that
//asked for it: once the white carry was capped, the tightest pair on the block stopped being yellow/white
//and became brown/orange/red - the warm family, which cannot be separated by hue because they ARE one hue
//family. Nothing but value was left to spend.
//
//⚠ It is LAVA'S OWN and deliberately not a change to TintEmission, which the PLASMA also calls. That style
//has its own palette, measured under its own scenes, and #395 did not measure it - a shared curve moved
//here would retune a style nobody looked at.
static const float LavaValuePower = 1.0;

//<summary>TintEmission with LavaValuePower's compression in place of the shared sqrt - see there.</summary>
float LavaTintEmission(float3 primary)
{
    float luminance = saturate(dot(primary, float3(0.2126, 0.7152, 0.0722)));

    return lerp(TintEmissionFloor, 1.0, pow(luminance, LavaValuePower));
}

//The crust's specular: weak and broad. Basalt is matte, and a shine on it turns the whole thing into
//painted plastic faster than any other error here.
static const float LavaHighlight = 0.35;
static const float LavaEnvironment = 0.2;
static const float LavaSmoothness = 0.2;

float4 LavaPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //THE SEAMS HAVE TO WANDER OR THEY ARE A CAGE. Three sine fields on a sphere cut great circles, and
    //three great circles read as wire wrapped round a ball rather than as rock that has cracked - it was
    //built that way first and that is exactly what it looked like. Displacing the coordinate they are
    //read at is the fix, and it is the plasma's domain warp at a fraction of the strength: enough to make
    //a seam wander and fork, not enough to make it writhe. Unlike the plasma's it does not move.
    float3 wander = float3(
        ReliefOctave(direction, LavaSeamB, LavaSeamWanderFrequency, footprint),
        ReliefOctave(direction, LavaSeamC, LavaSeamWanderFrequency * 1.23, footprint),
        ReliefOctave(direction, LavaSeamA, LavaSeamWanderFrequency * 0.79, footprint)) * LavaSeamWander;

    //The plate seams, in OBJECT space (contract point 6). A heavy crusted ball turning is very readable,
    //which makes this one of the better rotation cues in the set.
    float3 seamPosition = direction + wander;

    float seam = saturate(
        SeamLine(seamPosition, LavaSeamA, LavaSeamFrequency, LavaSeamWidth, footprint)
        + SeamLine(seamPosition, LavaSeamB, LavaSeamFrequency * LavaSeamRatio.x, LavaSeamWidth, footprint)
        + SeamLine(seamPosition, LavaSeamC, LavaSeamFrequency * LavaSeamRatio.y, LavaSeamWidth, footprint));

    //The same three lines read again at a wider width: the hot crust either side of a gap (#338). Taken as
    //a MAX and not a sum, which is the whole difference between a halo and a wash - three wide fields
    //added together saturate over most of the ball and the plates stop being plates, where the nearest of
    //them is a distance to the nearest crack, which is what heat actually follows. Shaped by a power into
    //a gradient rather than a plateau (see LavaHeatFalloff) and cut off inside the seam, so the two never
    //pay twice for the same pixel.
    float heatWidth = LavaSeamWidth * LavaHeatWidth;

    float hot = max(SeamLine(seamPosition, LavaSeamA, LavaSeamFrequency, heatWidth, footprint),
        max(SeamLine(seamPosition, LavaSeamB, LavaSeamFrequency * LavaSeamRatio.x, heatWidth, footprint),
            SeamLine(seamPosition, LavaSeamC, LavaSeamFrequency * LavaSeamRatio.y, heatWidth, footprint)));

    float halo = pow(hot, LavaHeatFalloff) * (1 - seam);

    //The crust's own broken-stone grain, plus the seams cut into it. Kept rather than removed - see the
    //header; this is the one new style that wants the vinyl's moulding machinery.
    float height = SurfaceRelief(direction, footprint) * LavaCrustRelief - seam * LavaSeamDepth;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);
    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //Basalt: nearly black, keeping just enough of the tint that thirteen crusts are not one object.
    float3 crust = primary * LavaCrustTint * LavaCrustDark + LavaCrustDark * (1 - LavaCrustTint);

    SurfaceSpecular surface;
    surface.Highlight = LavaHighlight;
    surface.Environment = LavaEnvironment;
    surface.Smoothness = LavaSmoothness;

    //A seam sees almost no sky - it is a gap in a thick shell - so it takes the ambient down with it.
    float cavity = 1 - 0.7 * seam;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(crust, 1), 1, cavity, surface);

    //Contract point 2, ROUTED INTO THE SEAMS rather than added beside them. Lava has an obvious reason to
    //pulse and the balls already share a wave through the cluster, so the beat IS the breath. The risk
    //this replaces is double-applying it - a flat emission plus a breathing seam is a ball that pulses
    //twice as hard as its neighbours, which is why EmissiveStrength is zero for this style.
    float beat = Heartbeat(PulseTime * PulseSpeed - dot(input.WorldPosition, PulseDirection) / max(PulseWavelength, 1e-4));

    //The molten interior. Normalised to the tint's peak so the 8-ball glows white-hot rather than not at
    //all (the plasma's answer, and for lava it is not even a departure), then cut deeper by
    //LavaHuePower (#395) - see there for why the off-peak channels, and not SaturateTint, are the lever.
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 hue = pow(saturate(primary / max(peak, 1e-3)), LavaHuePower);

    //BOTH THE BRIGHTNESS AND THE INCANDESCENT CARRY ARE SCALED BY THE TINT'S OWN LUMINANCE (#315), and
    //the header's "what it gives up knowingly is value separation" gave up more than it meant to. With
    //the seams the only coloured thing on the ball, discarding value discards the axis that separates
    //every pair sharing a hue family: ORANGE AND BROWN MEASURED 2.6 dE APART, the tightest pair anywhere
    //in this game against a vinyl control at 7.9, and black and silver 4.2.
    //
    //The carry needs the same scaling as the glow and not only the glow, because dE2000 forgives a pure
    //lightness gap and lerp(hue, LavaIncandescent) drives both warm tints onto the SAME near-white
    //wherever the seam is hottest - the brightest and so most heavily weighted part of the disc. A cooler
    //flow glows less brightly AND does not run to white at its core; that is all this is, and it costs
    //the style nothing it was built for.
    float emission = LavaTintEmission(primary);

    //#395: the carry is CAPPED and the heat it used to say is said by brightness instead - see
    //LavaCoreCarry for the measurement that forced it. The lift multiplies the hue the carry left in
    //place, so a hotter core is a brighter one OF THE BALL'S OWN COLOUR rather than a whiter one.
    float core = pow(seam, LavaCorePower) * emission;
    float3 molten = lerp(hue, LavaIncandescent, core * LavaCoreCarry) * (1 + core * LavaCoreLift);

    //Occluded LINEARLY, the plasma's own power and deliberately not BallEmission's square (#303): this
    //style's identity IS its glow, so a buried ball dims with the pile - the flat-wash correction the
    //plasma already carries - without its seams ever going out the way a resting vinyl breath now does.
    //The beat dims inside the pile with the rest of the glow, which the plasma also already accepted:
    //the seams ARE the breath here, and there is no resting half to split it from.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    float breath = lerp(1 - PulseDepth, 1, beat) * StillEmission;

    shaded.rgb += molten * seam * LavaGlow * emission * breath * occlusion;

    //And the hot crust round the gap (#338): the same glow at a fraction of the strength over several
    //times the area, in the ball's own hue and never carried to white - see LavaHeatWidth's note. It
    //breathes with the seam it belongs to, because it is the same heat seen through more rock.
    shaded.rgb += hue * halo * LavaGlow * LavaHeatGlow * emission * breath * occlusion;

    //Contract point 3, both meanings, PatternPS's arithmetic.
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

technique InstancedModelLava
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL LavaPS();
    }
};
