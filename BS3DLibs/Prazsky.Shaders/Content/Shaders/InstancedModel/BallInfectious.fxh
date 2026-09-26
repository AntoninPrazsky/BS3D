//---------------------------------------------------------------------------------------------------
// INFECTIOUS (#331, redrawn in #629 from generated references)
//
// A sick ball: the ball's own colour, clean and glossy, with a CRUST of growth eating one side of it - an
// olive, lumpy bloom with a ragged wet edge, luminous spore heads studded through it, and a few outlying
// spots where it is spreading. The ball is otherwise itself, which is the counterplay made visible.
//
// ⚠ THE FIGURE IS IN OBJECT SPACE AND THE ACID'S IS IN WORLD SPACE, and that opposition is the whole of
// what keeps the two green kinds apart. An acid is a statement about GRAVITY -- it eats downward, so its
// liquid stands level whatever the body is doing, and it reads as a dark ball whose lower half is full.
// An infection is a statement about the SURFACE IT IS ON: it is stuck to the ball and turns with it,
// which is contract point 6 answered by the figure itself rather than by something added for it. So one
// has an axis and the other has none, and that is legible at any distance - and a sick ball's crust sits
// on its UPPER side (the object-space axis a hanging ball keeps pointing up), where the acid's fill is
// at the bottom, so the two are opposites on the sheet as well as in the mechanics.
//
// WHAT THE REFERENCES SAID (#629, six renders: a bloom on a glossy ball, and a growth climbing off the
// top of a matte one): the growth is ONE contiguous crust on one side, not a scatter of spots - a thick,
// textured mass with a defined edge, glowing heads inside it, the clean part of the ball untouched and
// shining. #331's scattered blotches read on the owner's #620 sheet as a green ball with yellow dots,
// which is a pattern; a crust eating one side is a condition, which is what "sick" has to look like.
//
// The bead at the bottom is the one world-space term and it is deliberately SMALL: slime does run
// downward, and taking that away entirely would leave the crust reading as paint. It is a hint under an
// object-space figure, where the acid's pool is the figure.
//---------------------------------------------------------------------------------------------------

//What the spore heads glow: a sharp, luminous yellow-green, well clear of both the leafy ball greens and
//the acid's acrid one. Yellow is what does the separating -- an acid is green going to black, this is green
//going to WARM, which is what "sick" looks like on anything.
static const float3 InfectSlime = float3(0.68, 0.92, 0.16);

//What the crust itself is made of: a dull olive, matte, well under the slime's value, so the heads glow
//OUT of it and the crust reads as growth rather than as paint.
static const float3 InfectCrust = float3(0.34, 0.38, 0.09);

//And the wet rim along the crust's edge, brighter and paler than either: what makes the crust read as
//something LYING ON the ball with a surface tension of its own, rather than as a stain in its material.
static const float3 InfectRim = float3(0.86, 1.0, 0.52);

//Where the crust sits: the object-space axis its cap is centred on, how far down from that pole its edge
//reaches (in dot(direction, axis): 1 is the pole, 0 the equator), how far that edge wanders (from low
//gradient noise, so it is ragged and not a hat brim), and how soft it is. The extent and the wander are
//set so the crust takes somewhat under half the ball: the colour underneath is the counterplay, and a
//figure that takes it away takes the kind's only counterplay with it (#331's measured trap).
//
//⚠ THE AXIS POINTS DOWN AND FORWARD, NOT UP. The mechanic climbs towards the ceiling and the first cut put
//the crust on the top of the ball for that reason - which is the one side of a hanging ball the player,
//who looks UP at the cluster, never sees. A ball placed by a level keeps its identity orientation, so an
//object-space axis is a world direction in practice: this one faces the player from below and from the
//front, and leaves the crust reading as "one side eaten" rather than as a cap or a base.
//⚠ AND IT POINTS MORE TO THE SIDE THAN AT THE PLAYER: the first cut faced the cap straight at the camera and
//the tile photographed as a lime ball with a green sliver - the crust took four fifths of the visible disc
//and the colour, which is the counterplay, was gone. Seen half edge-on, the cap is one side of the ball.
static const float3 InfectCrustAxis = float3(0.72, -0.35, -0.60);
static const float InfectCrustExtent = 0.50;
static const float InfectCrustRagged = 0.30;
static const float InfectCrustRaggedCells = 2.2;
static const float InfectCrustEdge = 0.03;

//How much the crust's edge CREEPS, and how fast. It is the one animated term in the figure and it rides
//PulseTime like every other beat in this file, so it stays in step with the cluster rather than running
//on a clock of its own. Slow: what it has to say is "this is alive", not "this is electrical".
static const float InfectCrawlDepth = 0.05;
static const float InfectCrawlSpeed = 0.42;

//The crust's texture: lumps in object space (cells across the unit direction), how much they move its
//colour, and how proud the crust stands off the shell - POSITIVE like the acid's meniscus and for the
//same reason: it is matter lying on the ball.
static const float InfectLumpCells = 13.0;
static const float InfectLumpContrast = 0.8;
static const float InfectRelief = 0.05;

//The spore heads: hashed cells over the crust, the fraction of them that carry a head, a head's radius
//as a fraction of its cell, how far its glow spills round it, and how bright it glows. Few and large, with
//a halo: many small hard discs photographed as dots, which is the pattern #331 already had. A few also lie
//OUTSIDE the crust, past its edge, at a much lower fraction: the infection spreading.
static const float InfectHeadCells = 6.0;
static const float InfectHeadFraction = 0.30;
static const float InfectHeadRadius = 0.34;
static const float InfectHeadHalo = 0.30;
static const float InfectHeadGlow = 1.15;
static const float InfectOutlierFraction = 0.06;
static const float InfectOutlierReach = 0.22;

//How wide the wet rim along the edge is (in the same dot units as the extent) and how bright.
static const float InfectRimWidth = 0.045;
static const float InfectRimGain = 0.9;

//The bead at the underside: the one world-space term (see the header). A cap rather than a line, so it
//survives to the distance a level is played from, and small enough that it never becomes the figure.
static const float InfectBeadStart = 0.84;
static const float InfectBeadPower = 2.4;
static const float InfectBeadGain = 0.45;

//The crust's own faint light under the heads, added under the heartbeat for BallEmission's reason: a sick
//ball buried in a pile is the one that most has to be seen, and BallEmission's resting half is multiplied
//by occlusion squared.
static const float InfectCrustGlow = 0.05;

//Two surfaces: the clean ball is as glossy as any ball, the crust is matte and scattering.
static const float InfectCleanHighlight = 1.0;
static const float InfectCleanEnvironment = 0.85;
static const float InfectCleanSmoothness = 0.9;
static const float InfectCrustHighlight = 0.6;
static const float InfectCrustEnvironment = 0.2;
static const float InfectCrustSmoothness = 0.45;

//How far the crust damps the ball's own heartbeat where it lies: a growth is not lit from within, its
//heads are. The first cut let the crust breathe with the ball and it photographed as a lighter lime patch
//rather than as a darker olive one.
static const float InfectCrustDamp = 0.65;

//How much the crust darkens the ball's colour where it lies (the little of it that shows through): the
//crust's own colour is what is seen, the ball's is only the ground under it.
static const float InfectCrustCover = 0.9;

//A 3D cell hash to one value in 0..1, for the heads.
float InfectHash(float3 cell)
{
    return 0.5 + 0.5 * NoiseHash33(cell).x;
}

float4 InfectiousPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //THE CRUST: a cap round the axis with a ragged, creeping edge. Object space throughout (see the header).
    //The wander is band-limited to zero so a distant crust is a clean cap; the cap itself never band-limits.
    float pole = dot(direction, InfectCrustAxis);
    float wander = GradientNoise3(direction * InfectCrustRaggedCells) * saturate(1.0 - footprint * InfectCrustRaggedCells * 2.0);
    float crawl = InfectCrawlDepth * sin(PulseTime * InfectCrawlSpeed);
    float field = pole + InfectCrustRagged * wander + crawl;
    float fieldWidth = max(fwidth(field), 1e-4);
    float edge = max(InfectCrustEdge, fieldWidth);
    float crust = smoothstep(InfectCrustExtent - edge, InfectCrustExtent + edge, field);

    //The wet rim: a band along the edge, on the crust's side of it.
    float rim = crust * (1.0 - smoothstep(InfectCrustExtent, InfectCrustExtent + InfectRimWidth, field));

    //The lumps the crust is made of, band-limited to their mean.
    float lumps = 0.5 + 0.5 * (0.6 * GradientNoise3(direction * InfectLumpCells) * saturate(1.0 - footprint * InfectLumpCells * 2.0)
        + 0.4 * GradientNoise3(mul(NOISE_ROTATE3, direction) * (InfectLumpCells * 2.1)) * saturate(1.0 - footprint * InfectLumpCells * 4.2));

    //THE HEADS: a disc in each hashed cell that carries one, inside the crust at one fraction and just past
    //its edge at a far lower one. Band-limited to the fraction lit, so a distant crust keeps its glow.
    float3 headCoord = direction * InfectHeadCells;
    float3 headCell = floor(headCoord);
    float headHash = InfectHash(headCell);
    float headDistance = length(frac(headCoord) - 0.5);
    float headDisc = 1.0 - smoothstep(InfectHeadRadius - 0.05, InfectHeadRadius + 0.05, headDistance);
    float headHalo = 1.0 - smoothstep(InfectHeadRadius, InfectHeadRadius + InfectHeadHalo, headDistance);
    float headFade = saturate(1.0 - footprint * InfectHeadCells * 1.5);

    float outlierZone = (1.0 - crust) * (1.0 - smoothstep(InfectCrustExtent - InfectOutlierReach, InfectCrustExtent, field)) * step(InfectCrustExtent - InfectOutlierReach, field);
    float headWhere = crust * step(1.0 - InfectHeadFraction, headHash) + outlierZone * step(1.0 - InfectOutlierFraction, headHash);
    float heads = lerp(InfectHeadFraction * 0.28 * crust, headWhere * headDisc, headFade);
    float halo = lerp(InfectHeadFraction * 0.2 * crust, headWhere * headHalo * 0.5, headFade);

    //The bead at the underside, in world space (see the header). 0 at the top of the ball, 1 underneath.
    float3 normal = normalize(input.WorldNormal);
    float toward = saturate(0.5 - 0.5 * normal.y);
    float bead = pow(saturate((toward - InfectBeadStart) / (1.0 - InfectBeadStart)), InfectBeadPower);

    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 slimeColor = SrgbToLinear(InfectSlime);
    float3 crustColor = SrgbToLinear(InfectCrust) * (1.0 - InfectLumpContrast * 0.5 + InfectLumpContrast * lumps);

    //THE BALL'S OWN COLOUR IS STILL THE BODY where the crust is not, and that is the counterplay made
    //visible: a sick ball can be matched and shot out, so the player has to be able to read which colour
    //it is. Where the crust lies, the crust is what is seen.
    float3 color = lerp(primary, crustColor, crust * InfectCrustCover);
    color = lerp(color, slimeColor * 0.6, heads);

    //Contract point 6's other half, and the reason the relief is positive: the crust is matter lying on
    //the shell, lumpy, and the heads and the rim stand proudest.
    float height = (crust * (0.4 + 0.6 * lumps) + heads * 0.8 + rim * 0.5 + bead * 0.5) * InfectRelief;
    float3 worldNormal = PerturbNormalFromHeight(normal, input.WorldPosition, height);

    SurfaceSpecular surface;
    surface.Highlight = lerp(InfectCleanHighlight, InfectCrustHighlight, crust);
    surface.Environment = lerp(InfectCleanEnvironment, InfectCrustEnvironment, crust);
    surface.Smoothness = lerp(InfectCleanSmoothness, InfectCrustSmoothness, crust * (1.0 - heads));

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2. The heads and the crust's floor are added HERE as well as through BallEmission -- the
    //bomb's measured fault, inherited rather than rediscovered a fourth time -- and the rim and the bead get
    //their own share, because the lip of a growth is where a luminous film is brightest.
    shaded.rgb += slimeColor * ((heads + halo) * InfectHeadGlow + crust * InfectCrustGlow + bead * InfectBeadGain);
    shaded.rgb += SrgbToLinear(InfectRim) * rim * InfectRimGain * occlusion;

    //⚠ THE HEARTBEAT IS THE BALL'S OWN COLOUR WHERE THE CRUST IS NOT, AND THE SLIME'S WHERE IT IS -- a lerp
    //and not a substitution, which is what the first cut of this line was in #331. Every ordinary ball in
    //this game radiates ITS OWN colour on the beat, and that glow is a large part of how the thirteen are
    //told apart; handing this technique the slime's colour instead took it away, and all thirteen
    //photographed as dark greenish balls with yellow spots. A sick ball has to be shot out by matching its
    //colour, so anything that costs it its colour costs the kind its only counterplay. The heads ride the
    //beat hardest: they are what pulses.
    shaded.rgb += BallEmission(lerp(primary, slimeColor, saturate(crust * 0.35 + heads)) * (1.0 - InfectCrustDamp * crust * (1.0 - heads)),
        input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately. The lit branch carries
    //the BALL's colour towards white rather than the slime's: the wave is the cluster answering a landing
    //and it has to look the same on every ball it passes through.
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

technique InstancedModelInfectious
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL InfectiousPS();
    }
};
