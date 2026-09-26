//===================================================================================================
//CRACKLED PORCELAIN (#312): a glazed ceramic sphere - a deep, wet-looking coloured glaze over a white
//ceramic body, crazed all over with the fine hairline network an old glaze develops. Hard, cool and
//expensive-looking, and THE PATTERN IS THE TELL: a crackle net is instantly readable as ceramic and as
//nothing else.
//
//WHAT MAKES IT PORCELAIN RATHER THAN A SHINY BALL is the GLAZE DEPTH - the colour appears to sit
//slightly BELOW the surface, because the glaze is a clear layer over it. Two lobes do that: a very tight
//bright one for the glaze's own face, and the diffuse body underneath it. It is one of the cheapest
//convincing material tricks there is, and it is the whole difference between this and painted plastic.
//There is NO RELIEF: a fired glaze is glass-smooth, so like the marble this style spends the vinyl's
//moulding budget elsewhere.
//
//HOW IT STAYS APART FROM THE TWO STYLES IT NEIGHBOURS, which is a real risk with nine of them:
//  - Against the MARBLE (also opaque, also hard, also polished, also object-space patterned): marble's
//    figure is BROAD SOFT VEINS in a lighter tint; this is a FINE DARK HAIRLINE NET. Marble's specular
//    is a hard surface highlight; this is a coat over a body.
//  - Against the ICE (which uses the same SeamLine): ice cracks are BRIGHT, because they are internal
//    faces catching light inside a translucent solid. These are gaps in an opaque glaze.
//
//THE BODY IS WHITE AND THE GLAZE IS WHAT CARRIES THE COLOUR, and #339 is here because until it the code
//had neither. Everything above was true of the shader except the one sentence the whole material rests
//on: the tint WAS the body. A coloured diffuse sphere under a tight highlight is PAINTED PLASTIC, and
//"it isn't clear what this style is supposed to be at all" is exactly what painted plastic looks like -
//the owner could not name the material because the shader was not drawing one.
//
//Porcelain is white clay. The colour is a layer lying ON it, and every tell of the material follows from
//that one fact rather than from the crack net this header used to rest the whole read on:
//  - where the glaze runs THIN the clay reads through, so the ball is not one flat tint. Dipped ware is
//    never evenly coated, and that unevenness is most of the difference between a fired object and a
//    painted one;
//  - a craze line is the glaze PARTED DOWN TO THE CLAY, so what shows in it is white - stained the brown
//    an old piece takes up over a century (see PorcelainStain);
//  - and the clay is thin enough to pass light, which is the tell nobody mistakes: a held-up teacup
//    glows. That is what the translucency term is for, and it goes through the CLAY's colour and not the
//    glaze's, because it is the body that scatters.
//
//THE 8-BALL, AND THIS TIME THE ANSWER FALLS OUT OF THE MATERIAL RATHER THAN BEING A RULE. Dark cracks on
//a black glaze are no cracks at all, and the normalise-to-peak trick the plasma and the lava use does not
//help because a crack is not an emission. It used to be answered with an inversion keyed on the glaze's
//own luminance - a bright glaze crazed with DARKER lines, a dark one with LIGHTER ones - which was one
//rule instead of thirteen constants and was right about the problem. The stained clay does the same
//crossover for a reason instead of a rule: against a pale glaze the stain is the darker of the two, and
//against a dark one the clay is the lighter, with no test anywhere to say so.
//
//AND THE CRACKS CUT A GROOVE AS WELL AS TAKING A TONE, because #305 and #311 established that a figure
//in the colour alone can be swamped by the shading while one in the normal cannot. The groove is very
//shallow - a crack in a glaze is a parting, not a channel - but it is what keeps the net readable on the
//tints where the tone step is smallest.
//===================================================================================================

//Wave count of the crazing over the ball. The highest of any figure in this file, because craquelure IS
//fine - but still bounded by what a ball a few dozen pixels across can resolve, which is the lesson the
//metal's brush recorded at the cost of an invisible one.
float PorcelainCrackFrequency;

//How wide a hairline is. The thinnest in the file: a crack in a glaze has no area at all, and a wide one
//reads as a broken egg rather than as an antique.
float PorcelainCrackWidth;

//How deep and wet the glaze looks - how much of the environment its face mirrors. The one figure the C#
//side states, because it is what decides whether a dark glaze under a bright dome washes out to sky.
float PorcelainGlaze;

//The crazing's line directions and their ratios, and the wander that keeps the net off the great circles
//three plain sine fields would cut. The lava paid for that lesson; craquelure is even less forgiving,
//since a REGULAR crack net reads as a printed pattern instantly.
static const float3 PorcelainCrackA = float3(0.74, 0.44, -0.51);
static const float3 PorcelainCrackB = float3(-0.36, 0.85, 0.38);
static const float3 PorcelainCrackC = float3(0.50, -0.40, 0.77);
static const float2 PorcelainCrackRatio = float2(1.33, 1.87);
static const float PorcelainWander = 0.22;
static const float PorcelainWanderFrequency = 3.3;

//Where the craze stops being worth drawing, measured against its own LINE WIDTH - #337's lesson, and
//porcelain is where it bites hardest. SeamLine fades on the WAVELENGTH, which here is twenty times wider
//than the line riding it, so a hairline is left fully drawn long after a pixel can hold it. What that
//looks like is not a faint net: it is WHITE SPARKLE. The crack's groove tilts a normal inside a pixel,
//the glaze's mirror-smooth Fresnel fires on whichever pixels the tilt happens to catch, and the ball
//comes out speckled as if it were dirty - which is its own answer to "it does not read as any material".
//
//A line of |sin| < w at frequency f is 2w/f wide in direction units, so footprint * f / 2w is that line
//in pixel-pairs; the constant holds it at full strength until it is about a pixel and fades it over the
//last octave, exactly as IceCrackBandLimit does. The craze is therefore a CLOSE-RANGE figure and gone by
//play distance - which is why this style could not be left resting its whole read on it.
static const float PorcelainCrazeBandLimit = 1.5;

//The white clay under the glaze - the body of the thing, and the one addition #339 turns on. Slightly
//warm and slightly under one: porcelain is white, not paper, and a body at 1.0 clips its own highlight
//roll-off away before the tonemap ever sees it.
static const float3 PorcelainClay = float3(0.90, 0.895, 0.86);

//How much of the tint the glaze lays over that clay where it is thickest, and how far the coat varies
//over the ball. The cover stays HIGH: the clay is meant to read through where the glaze thins, not to
//wash the tint out - thirteen colours have to stay thirteen colours, and this term is the one in this
//change that could take that away. The variation is three sine octaves and nothing more.
static const float PorcelainGlazeCover = 0.94;
static const float PorcelainGlazePool = 0.14;
static const float PorcelainGlazeFrequency = 2.3;
static const float3 PorcelainGlazeA = float3(0.66, 0.51, -0.55);
static const float3 PorcelainGlazeB = float3(-0.42, 0.79, 0.45);
static const float3 PorcelainGlazeC = float3(0.47, -0.58, 0.67);

//What shows in a craze line: the clay, stained the brown an old glaze takes up. Both halves matter - the
//clay is what makes the line read on a DARK glaze and the stain is what makes it read on a PALE one, and
//between them they do the crossover the luminance test used to do explicitly.
static const float3 PorcelainStain = float3(0.34, 0.24, 0.15);
static const float PorcelainStainAmount = 0.62;

//===================================================================================================
//THE ORNAMENT: A HILBERT MEANDER IN WHITE ENAMEL, round the ball's own equator. The owner's idea, and
//it is the answer to "what IS this style" that no amount of shading was going to give: china is known
//by its DECORATION. A material read can be argued about; a painted band cannot be mistaken for anything
//but tableware, and it tells this style from the other nine across a whole cluster at a glance.
//
//WHY A HILBERT CURVE AND NOT A DRAWN MOTIF. It is the one ornament of this kind that can be evaluated
//rather than stored - no texture, no UVs, nothing in the content pipeline - and it happens to TILE: the
//standard curve enters its square at one corner of the bottom edge and leaves at the other, so laying
//tiles side by side round the equator joins them into ONE continuous meander that closes on itself. That
//is exactly the construction of a border on a plate, and it comes out of the curve's own definition
//rather than being arranged.
//
//HOW A PIXEL FINDS IT, which is the part worth knowing. The curve visits every cell of its grid, so the
//pixel's own cell is enough: take the cell's index along the curve (HilbertIndex), ask where the curve
//was one step before and one step after (HilbertPoint), and the curve inside this cell is the polyline
//from the entry edge's midpoint through the cell's centre to the exit edge's midpoint. Two segment
//distances and it is drawn. Neighbouring cells cannot be nearer than half a cell unless the curve
//crosses into this one - in which case they are the entry or the exit and are already accounted for - so
//this is exact for any ribbon under half a cell wide, which is every ribbon that looks like ornament.
//The first and last cells of a tile step OUTSIDE it for their missing neighbour, which is what welds one
//tile to the next.
//===================================================================================================

//The grid a tile is divided into: a power of two, and the curve's order follows from it (4 = order two).
//Higher is finer china and lower is a bolder motif, and the ceiling is what a ball a few dozen pixels
//across can still resolve - the lesson the metal's brush paid for and the craze above pays for again.
static const int PorcelainHilbertN = 4;

//How many tiles run round the equator. Together with the grid this decides the motif's size: a tile spans
//2*pi/tiles of arc, so a cell is about 0.39 of a ball radius at four tiles of four - coarse enough to
//survive play distance, which is the whole reason the ornament and not the craze carries this style.
static const float PorcelainOrnamentTiles = 4.0;

//How far the band reaches either side of the equator, in the direction's own y. A band and not a full
//covering: a sphere has no seamless square parameterisation, and a border round the middle is what china
//actually wears. Balls turn, so across a cluster it is seen at every angle, which is also true of a
//plate on a shelf.
static const float PorcelainOrnamentBand = 0.58;

//Half the ribbon's width, in cells, and how far the enamel stands proud of the glaze. Applied decoration
//sits ON the fired surface, so it has a real edge to catch the light - and that relief is what makes the
//ornament read on the WHITE ball, where white enamel on a white glaze has no colour step at all (#305
//and #311's lesson, arriving here as the reason the band is not just painted on).
static const float PorcelainOrnamentWidth = 0.135;
static const float PorcelainOrnamentRelief = 0.010;

//The enamel itself: a touch brighter and cooler than the clay, because it is a second, whiter slip laid
//over the glaze rather than the body showing through.
static const float3 PorcelainEnamel = float3(0.96, 0.955, 0.94);

//How much of the glaze shows through the enamel, and it is here for a MEASURED reason rather than a
//painterly one. A pure white band over a fifth of the disc lifts every ball towards white by the same
//amount, and what that costs is the DARK end of the palette: black against brown went from 9.3 dE to
//6.0, which is under the moulded vinyl's own worst pair (6.4) - the line #315 says every style has to
//clear. A slip laid this thin genuinely does let its ground through, so carrying a fraction of the
//glaze's own colour into it is both what the material does and what gives each ball back a share of the
//separation the band was taking. It still reads as white; it reads as white ON something.
static const float PorcelainEnamelSheer = 0.22;

//Where the ribbon stops being worth drawing, on the same rule as the craze and with a great deal more
//room: the ribbon is about a fifteenth of the ball wide against the craze's hairline, so it is still
//several pixels across at play distance and this only engages on the smallest LOD.
static const float PorcelainOrnamentBandLimit = 1.5;

//How deep a crack parts the glaze. Very shallow: it is there so the net survives on the tints where the
//tone step is smallest, not to be seen as relief.
static const float PorcelainCrackDepth = 0.007;

//The glaze's own face: a tight bright lobe over the body, which is what puts the colour UNDER a surface
//rather than on it.
static const float PorcelainGloss = 210.0;
static const float PorcelainGlossStrength = 1.1;

//What the body under the glaze does with light: a normal diffuse ceramic, with the broad highlight cut
//back because the glaze above it answers that instead.
static const float PorcelainHighlight = 0.3;

//The quadrant flip/swap both Hilbert conversions turn on, written with selects rather than an if: it is
//per-pixel varying, and while nothing in here takes a derivative, this file's rule is that a divergent
//branch inside a shader that uses ddx/ddy elsewhere is not worth the risk for two instructions.
int2 HilbertRotate(int n, int2 p, int rx, int ry)
{
    int2 flipped = rx == 1 ? int2(n - 1 - p.x, n - 1 - p.y) : p;

    return ry == 0 ? flipped.yx : p;
}

//Where a cell sits along the curve, and the inverse. Both are the textbook pair; the loops are bounded by
//a compile-time constant so they unroll to a couple of dozen integer instructions.
int HilbertIndex(int2 p)
{
    int d = 0;

    [unroll]
    for (int s = PorcelainHilbertN / 2; s > 0; s /= 2)
    {
        int rx = (p.x & s) > 0 ? 1 : 0;
        int ry = (p.y & s) > 0 ? 1 : 0;

        d += s * s * ((3 * rx) ^ ry);
        p = HilbertRotate(PorcelainHilbertN, p, rx, ry);
    }

    return d;
}

int2 HilbertPoint(int d)
{
    int2 p = int2(0, 0);
    int t = d;

    [unroll]
    for (int s = 1; s < PorcelainHilbertN; s *= 2)
    {
        int rx = 1 & (t / 2);
        int ry = 1 & (t ^ rx);

        p = HilbertRotate(s, p, rx, ry);
        p += s * int2(rx, ry);
        t /= 4;
    }

    return p;
}

float SegmentDistance(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));

    return length(pa - ba * h);
}

//The ornament's signed profile: how far this pixel is from the meander, in cells, and 8 when it is not in
//the band at all. Returned as a distance rather than a mask so the caller can make both a colour and a
//rounded bead out of it without evaluating the curve twice.
float PorcelainOrnamentDistance(float3 direction)
{
    //The band, in the ball's OWN frame (contract point 6): the border turns with the ball, which on a
    //style this static is most of what says the thing is rotating at all.
    float v = direction.y / PorcelainOrnamentBand;

    if (abs(v) > 1) return 8;

    //Azimuth into tiles. The atan2 cut falls between two tiles and not inside one, so the seam there is
    //the same weld that joins every other pair of tiles - which is the whole reason the tiling had to run
    //round the equator an INTEGER number of times.
    float u = (atan2(direction.z, direction.x) / (2 * 3.14159265) + 0.5) * PorcelainOrnamentTiles;

    float2 f = float2(frac(u), (v + 1) * 0.5) * PorcelainHilbertN;
    int2 cell = clamp(int2(floor(f)), 0, PorcelainHilbertN - 1);

    int d = HilbertIndex(cell);
    int last = PorcelainHilbertN * PorcelainHilbertN - 1;

    //The missing neighbour at either end of a tile is the cell across the tile boundary, which is what
    //welds the tiles into one continuous meander.
    int2 previous = d == 0 ? cell + int2(-1, 0) : HilbertPoint(d - 1);
    int2 following = d == last ? cell + int2(1, 0) : HilbertPoint(d + 1);

    float2 centre = cell + 0.5;
    float2 entry = (centre + previous + 0.5) * 0.5;
    float2 exit = (centre + following + 0.5) * 0.5;

    return min(SegmentDistance(f, entry, centre), SegmentDistance(f, centre, exit));
}

float4 PorcelainPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //The wander first, for the reason the lava's header gives at length: three sine fields on a sphere cut
    //great circles, and a regular net reads as a printed pattern rather than as a glaze that has crazed.
    float3 wander = float3(
        ReliefOctave(direction, PorcelainCrackB, PorcelainWanderFrequency, footprint),
        ReliefOctave(direction, PorcelainCrackC, PorcelainWanderFrequency * 1.19, footprint),
        ReliefOctave(direction, PorcelainCrackA, PorcelainWanderFrequency * 0.81, footprint)) * PorcelainWander;

    //The crazing itself, in OBJECT space (contract point 6): the net is IN the glaze and turns with it.
    float3 crackPosition = direction + wander;

    float craze = saturate(
        SeamLine(crackPosition, PorcelainCrackA, PorcelainCrackFrequency, PorcelainCrackWidth, footprint)
        + SeamLine(crackPosition, PorcelainCrackB, PorcelainCrackFrequency * PorcelainCrackRatio.x, PorcelainCrackWidth, footprint)
        + SeamLine(crackPosition, PorcelainCrackC, PorcelainCrackFrequency * PorcelainCrackRatio.y, PorcelainCrackWidth, footprint));

    //...faded on the LINE and not on the wave it rides - see PorcelainCrazeBandLimit, and the sparkle that
    //note describes is what leaving this out looks like.
    craze *= saturate(PorcelainCrazeBandLimit
        - footprint * PorcelainCrackFrequency / (2 * max(PorcelainCrackWidth, 1e-3)));

    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 clay = SrgbToLinear(PorcelainClay);

    //THE GLAZE OVER THE CLAY (#339). How thick the coat lies varies over the ball, so the body reads
    //through where it thins - which is what says a fired, dipped object rather than a painted one, and is
    //the whole of why this ball is no longer a flat tint with a dot of white on it.
    float coat = 0.5 + 0.5 * (ReliefOctave(direction, PorcelainGlazeA, PorcelainGlazeFrequency, footprint)
        + ReliefOctave(direction, PorcelainGlazeB, PorcelainGlazeFrequency * 1.41, footprint)
        + ReliefOctave(direction, PorcelainGlazeC, PorcelainGlazeFrequency * 0.83, footprint)) / 3;

    float3 glaze = lerp(clay, primary, saturate(PorcelainGlazeCover - PorcelainGlazePool * (1 - coat)));

    //And the craze: the glaze parted down to the clay, stained. No luminance test - see the header; the
    //crossover is what these two colours DO against a pale ground and against a dark one.
    float3 color = lerp(glaze, lerp(clay, SrgbToLinear(PorcelainStain), PorcelainStainAmount), craze);

    //THE ORNAMENT. Antialiased and band-limited off the FOOTPRINT rather than off fwidth of the distance,
    //deliberately twice over: the distance jumps to 8 outside the band, which would smear that edge, and
    //it is read through an atan2 whose cut would put one bad column down the ball. A cell is 2*pi over
    //tiles times grid of arc, so the footprint divided by that IS the pixel measured in cells.
    float ornament = PorcelainOrnamentDistance(direction);
    float cellArc = 6.28318531 / (PorcelainOrnamentTiles * PorcelainHilbertN);
    float pixelCells = footprint / cellArc;

    //A quarter of that for the ANTIALIAS: footprint is ddx plus ddy, so it is about two pixels, and using
    //the whole of it as a smoothstep half-width made the transition wider than the ribbon itself - the
    //band came out a soft embossed shadow instead of a painted line, which is not what enamel does.
    float edge = max(pixelCells * 0.25, 1e-4);

    float ribbon = (1 - smoothstep(PorcelainOrnamentWidth - edge, PorcelainOrnamentWidth + edge, ornament))
        * saturate(PorcelainOrnamentBandLimit - pixelCells / max(2 * PorcelainOrnamentWidth, 1e-3));

    color = lerp(color, lerp(SrgbToLinear(PorcelainEnamel), glaze, PorcelainEnamelSheer), ribbon);

    //...and the groove, so the net is a figure in the NORMAL as well as in the colour (#305, #311). Shallow
    //on purpose: a crack in a glaze is a parting, not a channel. The enamel goes the other way and stands
    //PROUD of it, on a parabolic dome - which is what "rounded" means here, and what gives the band a lit
    //edge on the white ball where its colour step is nothing.
    float beadEdge = saturate(ornament / PorcelainOrnamentWidth);
    float bead = (1 - beadEdge * beadEdge) * ribbon;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition,
        bead * PorcelainOrnamentRelief - craze * PorcelainCrackDepth);

    //The body under the glaze: an ordinary diffuse ceramic, its own broad highlight cut back because the
    //glaze's face answers that instead. Smooth, because a fired glaze is glass.
    SurfaceSpecular surface;
    surface.Highlight = PorcelainHighlight;
    surface.Environment = PorcelainGlaze;
    surface.Smoothness = 1;

    //A crack sees less sky than the face around it, which is what makes the net read even where the tone
    //step is small.
    float cavity = 1 - 0.35 * craze;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, cavity, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //THE GLAZE'S OWN FACE, and the whole of what puts the colour under a surface rather than on it: a
    //tight bright lobe sitting ON TOP of a body that is already shaded. Taken off the SMOOTH normal and
    //not the crazed one, deliberately - the glaze is continuous over a hairline crack, and a highlight
    //that broke at every crack would say the surface was chipped rather than crazed.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float3 halfway = normalize(towardsKey + eyeVector);

    shaded.rgb += DirLight0SpecularColor * PorcelainGlossStrength
        * pow(saturate(dot(normalize(input.WorldNormal), halfway)), PorcelainGloss) * occlusion;

    //THE TELL NOBODY MISTAKES, and the second half of #339: fine porcelain passes light. A held-up teacup
    //glows, and no plastic does. Same construction as the ice's, at a third of the strength - this is a
    //dense fired body and not a scattering solid - and through the CLAY's colour rather than the glaze's,
    //because what light crosses is the body: a red cup lit from behind glows warm WHITE, not red.
    float through = pow(saturate(dot(-worldNormal, towardsKey)), 2);

    shaded.rgb += through * TranslucencyStrength * DirLight0DiffuseColor * lerp(clay, primary, 0.35) * occlusion;

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //Contract point 3, both meanings, PatternPS's arithmetic.
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

technique InstancedModelPorcelain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL PorcelainPS();
    }
};
