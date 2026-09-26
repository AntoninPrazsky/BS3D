//===================================================================================================
//ROUGH GRANITE (#324) - the ball that is NOT one of the thirteen colours.
//
//Every other ball technique in this file takes PatternPrimaryColor and does something with it, because
//every other one draws a ball the player matches. This one draws the ROCK (BallKind.Rock): a ball that
//matches with nothing and that no colour removes. So it ignores the tint entirely and is the only
//shading here whose colour is a constant - and that is a rule, not a saving. A rock wearing one of the
//thirteen colours is a lie the player acts on: they would aim a colour at it and it would refuse.
//
//It is also the one shading a MAP cannot name. BallStyle says what a level's balls are MADE of; the rock
//opts out of it and is stone on a bubble level and on a lava level alike, because "that one is
//different" has to survive all ten materials or it is not a signal.
//
//What makes it read as stone, in the order the eye picks it up:
//  1. It is NOT ROUND. Every other ball in this game was cast, blown, wound, ground or glazed, and all
//     of them are smooth spheres with a figure drawn on them; this one is CARVED OUT OF ROUND in its own
//     vertex shader (#340 - see the block above StoneShape), and carries the largest relief in the file
//     on top of that. The silhouette is the first thing the eye reads and the only thing that survives
//     any distance, so it is where "that one is different" is now said. This paragraph used to end
//     "nothing here builds geometry" and rested the whole read on shading a perfect sphere; the owner's
//     playtest asked for the opposite in as many words, and confirmed it when asked.
//  2. It is SPECKLED. Granite is an aggregate of grains large enough to see: pale quartz and feldspar,
//     dark mica and hornblende. Three high-frequency waves MULTIPLIED give a field concentrated near
//     zero with isolated peaks of both signs, which is a fleck field - a sum would give creases, which
//     is what the marble's veining is made of and what this must not look like.
//  3. It is MATTE. No polish, no pinpoint, almost no environment. The marble is what a stone looks like
//     when it has been worked; this is what it looks like when it has not.
//
//And it does NOT BREATHE. BallRenderSet draws the rocks in their own bucket plane at PulseDepth zero, so
//contract point 2 comes out as a STEADY floor: a rock stands dead still inside a cluster that pulses,
//which is worth more than any figure drawn on it, because motion is the first thing the eye reads and its
//absence names the odd one out across a whole field, at any distance and under any dome.
//
//⚠ It is NOT drawn without emission, and the reason is worth reading before anyone takes it back out. The
//player looks UP at the cluster from the island - at its UNLIT side - and every other ball lights that
//side by two routes this material has neither of: its own glow, and TranslucencyStrength, the key light
//carried THROUGH a hollow shell from behind. Stone is solid and does not glow, so a physically honest rock
//was the only genuinely dark ball in the frame and read as the 8-ball. Raising the body tint did not fix
//it and neither did the strongest ambient in the palette; those two missing terms were worth about as much
//as everything else together. The emission stands in for the translucency. What it must never buy back is
//the beat.
//===================================================================================================

//Wave count of the mineral grain over the ball. High: a grain is what separates rock from a grey ball,
//and at a low count the speckle turns into blotches and reads as a mouldy marble.
float StoneGrainFrequency;

//How far a grain carries from the body grey - towards black one way and a pale quartz the other. The one
//figure this shading spends anything on, which is why the C# side states it.
float StoneGrainContrast;

//Amplitude of the coarse relief, in world units. The largest ball figure in this file; see point 1.
float StoneRoughness;

//The stone itself, in sRGB like every other colour written here.
//
//⚠ THE VALUE HAS TO BE HIGH FOR A REASON THAT STILL STANDS, and the first attempt at this style got it
//wrong by being physically reasonable: a 0.42 grey is a fair granite albedo and it came out BLACK on
//screen, next to the 8-ball rather than next to the silver. Two things take a rock down that no other
//ball here pays:
//it is the only ball in the game with NO EMISSION (every other one adds 0.3-0.5 of its own colour on top of
//its shading, so the whole cluster around this one is lifted), and it is the only one that barely mirrors
//the sky (see StoneEnvironment). A neutral surface next to thirteen lit ones reads far darker than its
//albedo says, and the tonemap then compresses what is left. So this is a LIGHT stone: measured against the
//cluster, not against a rock.
//
//Warm, deliberately: Type11 (silver) is a COOL slate at (0.5, 0.53, 0.58) and is the one type a grey ball
//can be confused with. Same value, opposite hue, and the two never meet.
//
//⚠ 0.63 UNTIL #340, and what bought the drop was the carving. The owner's second sentence was that the
//ball is too light in value, and the argument above says why it could not simply be darkened - so it was
//not darkened simply. A carved ball has real form: its own gouges shade it, which is a source of contrast
//a smooth sphere with no emission and no mirror had nowhere to get, and value that used to have to come
//out of the albedo now comes out of the shape. MEASURED against the pair this constant exists to sit
//between, on the meadow at dome 1: rock 84 and 75 mean luminance on two rocks, Type8 (black) 42, Type11
//(silver) 81. Under dome 13: rock 66 and 67, black 29, silver 80. Still beside the silver and still at
//twice the 8-ball on both, which is the whole of what the paragraph above asks for.
static const float3 StoneBody = float3(0.54, 0.51, 0.465);

//What the grains are: the pale one is quartz catching the light, the dark one mica. Asymmetric on purpose -
//a real granite's dark minerals sit further from the matrix than its light ones, and a symmetric pair reads
//as noise rather than as an aggregate.
static const float3 StoneGrainPale = float3(0.90, 0.89, 0.86);
static const float3 StoneGrainDark = float3(0.17, 0.16, 0.155);

//The three directions the grain waves run along, and the ratios between their frequencies. Irrational
//ratios so the product never settles into a repeating lattice, and none of them near an axis of the
//sphere so the grains do not agree with the mesh's own poles - the LOD ladder's coarsest spheres would
//otherwise have something to line up with.
static const float3 StoneGrainAxisA = float3(0.63, 0.49, -0.60);
static const float3 StoneGrainAxisB = float3(-0.42, 0.77, 0.48);
static const float3 StoneGrainAxisC = float3(0.51, -0.38, 0.77);
static const float3 StoneGrainRatio = float3(1.0, 1.137, 0.874);

//How hard the fleck field is squeezed before it counts as a grain. The product of three waves spends most
//of its range near zero, so this is what turns "mostly nothing with occasional peaks" into discrete
//grains with clean matrix between them rather than a continuous haze.
static const float StoneGrainGate = 2.6;
static const float StoneGrainSharpness = 1.7;

//The coarse lumps, as a fraction of the relief: how much of the roughness is broad shaping (a chipped
//boulder) against fine pitting (weathering). Both are needed - fine alone reads as sandpaper on a
//perfect sphere, and that is a texture rather than a rock.
static const float StoneLumpShare = 0.62;

//How far the lump field displaces the phase the grain is read at, in radians. Enough to move a grain by
//most of its own width, which is what it takes to destroy the lattice; much more and the grains smear
//into streaks and the aggregate turns into a marble's veining.
static const float StoneGrainWarp = 2.2;

//How much the lumps darken the body between the grains. Small - it is there so a rock is not one flat
//grey, and anything more competes with the grain itself. Free: the field is evaluated once for all three
//of its jobs.
static const float StoneMottle = 0.35;

//Matte, and the three figures do not all say the same thing. The broad direct highlight is cut hard and
//SMOOTHNESS is cut hardest: short of 1 it stops Fresnel raising a mirror rim along the silhouette, which
//on a sphere is most of what can be seen of one and is exactly what the polished marble is FOR.
//
//⚠ The ENVIRONMENT is NOT cut with them, and the first build cut it to 0.20 as though "matte" meant "sees
//no sky". It does not: at this smoothness the reflection is blurred all the way to the sky's average, so
//what this term delivers is diffuse SKYLIGHT, which is most of what lights a real rock outdoors. Cutting
//it made the rock the only ball in the game that ignored the dome, and it read near-black in the map
//editor while every colour around it was washed with sky. At 0.55 the editor and the Testbed agree on it
//(117/100/81 against 106/94/79, same level file and the same D1 view). Blurred, yes; blind, no.
static const float StoneBroadHighlight = 0.22;
static const float StoneEnvironment = 0.55;
static const float StoneSmoothness = 0.25;

//===================================================================================================
//THE SILHOUETTE IS NOT A CIRCLE ANY MORE (#340), AND THAT IS A RULING RATHER THAN A TUNING. The header
//above says "nothing here builds geometry" and point 1 rests the whole read on shading a perfect sphere;
//that stood as a rule until the owner's playtest asked for the opposite in as many words - a rock may be
//visibly less round than the other balls, sharper, more like an actual chunk of stone - and confirmed it
//when asked. #271 decided the same question the other way for the GEM, and both rulings are right for
//their own style: a gem is CUT, so a polygonal outline on it is a defect in the cut, while a rock is
//BROKEN, and an outline that is not round is the single clearest thing that can be said about one.
//
//SO THE VERTEX IS MOVED, and this is the only ball technique in the file with a vertex shader of its own.
//Three things follow from that and each is a decision:
//
//  - IT CARVES INWARD ONLY. The radius runs from (1 - depth) up to 1 and never past it, because the
//    lattice packs these balls at exactly two radii apart: a rock that grew would push into the cell its
//    neighbour occupies, and a cluster would knit into itself. Carving also happens to be what makes a
//    rock: stone is what is LEFT after pieces came off.
//  - THE NORMAL IS ANALYTIC, not the sphere's. Once a radius varies over the surface the direction is no
//    longer the normal, and reusing it would light a carved ball exactly like a round one - the whole
//    change would then be a silhouette and nothing else. For a radial surface r(d)*d the normal is
//    d - (tangential gradient of r)/r, which is closed form here because the field is a sum of sines: its
//    gradient is the same sum with cos and a factor of the wave vector. That is why this field is written
//    out separately from StoneLumps rather than reusing it - StoneLumps is rectified (abs), and abs has
//    no gradient at its own zeros.
//  - IT IS COARSER THAN EVERYTHING ELSE ON THE BALL. Frequencies of 1.7 to 5.7 against the lumps' 3.5 to
//    47, so the two tiers do not double-count: this one is the SHAPE and what the pixel shader perturbs
//    on top of it is the SURFACE.
//
//⚠ Every rock carries the SAME carving, because the instance stream has no free channel for a per-ball
//seed (world matrix, occlusion, dissolve and ripple fill it) and the only per-instance value available -
//the ball's position - moves, which would make the shape swim as a rock fell. What saves it is that the
//field is in OBJECT space: one stone shown at a hundred orientations is a hundred rocks, which is what a
//pile of one quarry's rubble looks like anyway.
//
//THAT ORIENTATION IS SUPPLIED DELIBERATELY, AND UNTIL #356 IT WAS NOT. This block used to say "the physics
//gives every rock its own orientation" and that was measured false: BallsConstraintsBuilder creates every
//body at identity, and a lattice hanging from a ceiling never turns one. On Cairn - 209 rocks, the densest
//of the five stone levels - the mean tilt from identity over sixty seconds was 0.05 deg and the largest
//0.28 deg, only the first second after the build reaching 6.3 deg as the constraints took up. So a heap was
//one solid at one angle, down to the same pale swirl in the same place on every rock in it. RockTurns (C#
//side) now folds a fixed per-cell turn into the world matrix that is ALREADY in the stream, which makes the
//paragraph above true instead of hoped for - and costs nothing, so the fifth instance element this block
//used to name as the fix is not needed and would buy nothing over it.
//===================================================================================================

//How deep the carving cuts, as a fraction of the radius. The one figure of it the C# side states, because
//it is the whole of how un-round a rock is and it is bounded by the lattice: at 0.2 a ball's narrowest
//axis is four fifths of its cell, which is as far as it can go before the gaps between rocks start to
//read as holes in the cluster.
float StoneShapeDepth;

//How sharply the carving bites. The field is a sum of sines, so raw it is a gentle undulation and the
//first build of this came out a smooth POTATO - rounded, obviously not spherical, and not what was asked
//for, which was sharper and more like an actual chunk. A power over the carve fraction leaves most of the
//surface near full radius and drives the rest deep and narrow, which turns undulation into GOUGES: stone
//is what is left after pieces came off, and pieces come off in bites rather than in ripples.
static const float StoneShapePower = 1.8;

//The four waves the carving is built from. Low frequencies, irrational-ish ratios, none near an axis of
//the sphere - the same three rules every field in this file follows, for the same three reasons.
static const float4 StoneShapeFrequency = float4(1.7, 2.6, 3.9, 5.7);
static const float4 StoneShapeWeight = float4(0.40, 0.28, 0.19, 0.13);
static const float3 StoneShapeAxisA = float3(0.68, 0.47, -0.56);
static const float3 StoneShapeAxisB = float3(-0.39, 0.81, 0.44);
static const float3 StoneShapeAxisC = float3(0.53, -0.35, 0.77);
static const float3 StoneShapeAxisD = float3(-0.77, -0.33, 0.55);

//The carving field and its gradient in one pass, because the vertex shader needs both and the sines are
//the expensive half. Returns roughly -1..1; the gradient is with respect to the direction itself, so the
//caller has to take its tangential part.
float StoneShape(float3 direction, out float3 gradient)
{
    float4 phase = float4(dot(direction, StoneShapeAxisA), dot(direction, StoneShapeAxisB),
        dot(direction, StoneShapeAxisC), dot(direction, StoneShapeAxisD)) * StoneShapeFrequency;

    float4 wave = sin(phase);
    float4 slope = cos(phase) * StoneShapeFrequency * StoneShapeWeight;

    gradient = slope.x * StoneShapeAxisA + slope.y * StoneShapeAxisB
        + slope.z * StoneShapeAxisC + slope.w * StoneShapeAxisD;

    return dot(wave, StoneShapeWeight);
}

//The rock's own vertex shader: PatternVS with the carving in it. Everything else about the output is
//PatternVS's, and deliberately so - the two must not drift apart, since every contract point the pixel
//shader answers is read off these same fields.
PatternVertexShaderOutput StoneVS(VertexShaderInput input, InstanceInput instance)
{
    PatternVertexShaderOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

    float4 bonePosition = mul(input.Position, Bone);

    float radius = max(length(bonePosition.xyz), 1e-5);
    float3 direction = bonePosition.xyz / radius;

    float3 gradient;
    float shape = StoneShape(direction, gradient);

    //Inward only: 1 at the shallowest, 1 - depth at the deepest. Through the power, so it gouges rather
    //than undulates - see StoneShapePower.
    float bite = saturate(0.5 + 0.5 * shape);
    float bitten = pow(bite, StoneShapePower);
    float carve = 1 - StoneShapeDepth * bitten;

    //n proportional to d - (tangential gradient of r)/r, with r = radius * carve. The carve's own
    //derivative supplies the minus sign, which is why this reads as a plus; the power supplies the chain
    //rule factor beside it, and dropping THAT would light a gouged ball as though it were rippled.
    float slope = StoneShapeDepth * 0.5 * StoneShapePower * pow(max(bite, 1e-4), StoneShapePower - 1);

    float3 tangential = gradient - dot(gradient, direction) * direction;
    float3 objectNormal = normalize(direction + (slope / carve) * tangential);

    float3 carved = direction * (radius * carve);
    float4 worldPosition = mul(float4(carved, 1), world);

    output.ObjectPosition = carved;
    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(objectNormal, world);
    output.OcclusionData = instance.Custom;
    output.Dissolve = instance.Dissolve;
    output.Ripple = instance.Ripple;

    return output;
}

//The coarse shaping field: rectified octaves, the marble's construction at a quarter of its frequency.
//Amplitudes sum to one so StoneRoughness stays the peak height in world units.
//
//SIX OCTAVES SINCE #340, WHERE THERE WERE FOUR, and the reason is the owner's "the surface's resolution
//is too low". Four octaves an octave apart do not describe a surface - they interfere into a regular
//weave, which is the trap the scene relief's own header records at length (see SurfaceReliefWorld, and
//why that one uses seven). The two added are the FINEST, so they cost nothing at distance: each octave
//band-limits against its own wavelength, so they are present exactly while a pixel can hold them and
//gone silently when it cannot, and what carries a rock across the arena is still the coarse end.
float StoneLumps(float3 direction, float footprint)
{
    return 0.36 * abs(ReliefOctave(direction, float3(0.71, 0.52, -0.47), 3.5, footprint))
        + 0.24 * abs(ReliefOctave(direction, float3(-0.36, 0.83, 0.42), 6.0, footprint))
        + 0.16 * abs(ReliefOctave(direction, float3(0.55, -0.44, 0.71), 11.0, footprint))
        + 0.11 * abs(ReliefOctave(direction, float3(-0.82, -0.31, 0.48), 19.0, footprint))
        + 0.08 * abs(ReliefOctave(direction, float3(0.29, -0.86, -0.42), 31.0, footprint))
        + 0.05 * abs(ReliefOctave(direction, float3(-0.64, 0.31, -0.70), 49.0, footprint));
}

float4 StonePS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //The coarse shaping field, first, because the grain is warped by it below. It does triple duty: it
    //shapes the surface, it mottles the body, and it breaks the grain's lattice - and it is evaluated once.
    float lumps = StoneLumps(direction, footprint);

    //Contract point 6: the grain is in OBJECT space, so it turns with the ball. It is a stronger rotation
    //cue than the gores it replaces, because it is aperiodic - a rolling beach ball shows the same five
    //stripes coming round again, and there is no such thing as coming round again on this one.
    float3 grainFrequency = StoneGrainFrequency * StoneGrainRatio;

    //⚠ WARPED BY THE LUMPS, and without this the ball is a GOLF BALL. Three pure waves multiplied give a
    //regular three-dimensional lattice of blobs, and a lattice of identical dimples in even rows is exactly
    //what a golf ball is - it was the first thing wrong with this style after the value. Displacing the
    //phase each wave is read at, by a field an octave or two coarser, is the marble's own construction for
    //its veins (warp the INPUT, not the output) and it costs nothing here because the field is already in
    //hand. What comes out is granite's actual figure: patches of coarser and finer grain, none of them
    //lining up with the next.
    float warp = (lumps - 0.5) * StoneGrainWarp;

    //⚠ BAND-LIMITED ONCE, ON THE PRODUCT, and ReliefOctave is deliberately NOT used to build it. Every other
    //figure in this file is a SUM of octaves, where per-octave attenuation is the right and only construction
    //(see ReliefOctave's own comment). A product is not a sum: three attenuated factors multiply their
    //attenuations, so a field that should fade to 45 % at a given distance fades to 45³ = 9 % instead and the
    //grain is simply gone one ball-width from the camera. Measured, not reasoned about - the first build of
    //this style drew no grain at all at play distance and this was why.
    //
    //What the one factor is measured against is the SUM of the three frequencies, because that is where a
    //product's finest content actually lies: sin(a)·sin(b) carries a+b as well as a-b.
    float grainBandLimit = saturate(1 - footprint * (grainFrequency.x + grainFrequency.y + grainFrequency.z)
        / 3.14159265);

    float fleck = sin(dot(direction, StoneGrainAxisA) * grainFrequency.x + warp)
        * sin(dot(direction, StoneGrainAxisB) * grainFrequency.y - warp * 1.31)
        * sin(dot(direction, StoneGrainAxisC) * grainFrequency.z + warp * 0.77)
        * grainBandLimit;

    //Two sides of one field: the peaks above zero are the pale mineral, the troughs below it the dark
    //one. Reading both off the SAME product is what keeps them interlocked the way an aggregate's
    //grains are, instead of two speckle patterns laid over each other.
    float pale = pow(saturate(fleck * StoneGrainGate), StoneGrainSharpness);
    float dark = pow(saturate(-fleck * StoneGrainGate), StoneGrainSharpness);

    float3 color = SrgbToLinear(StoneBody) * (1 - StoneMottle * lumps);

    color = lerp(color, SrgbToLinear(StoneGrainPale), pale * StoneGrainContrast);
    color = lerp(color, SrgbToLinear(StoneGrainDark), dark * StoneGrainContrast);

    //The surface. Lumps for the shaping and the fleck field for the pitting, in one height field so a single
    //perturbation covers both - the vinyl skin's construction, at several times its amplitude. The fleck is
    //signed here rather than split into its two minerals: a pit and a raised grain are both real, and the
    //colour above already decided which mineral is which.
    //
    //The LUMPS are what survives distance. They run at 3.5 to 19 waves where the grain runs at three times
    //that, so at the stand-off a level is played from the grain has faded out and this has not - which is the
    //whole reason the roughness is not left to the speckle. A rock has to still be a rock across the arena.
    float height = (StoneLumpShare * lumps + (1 - StoneLumpShare) * fleck) * StoneRoughness;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);

    //Matte, and every figure of it stated rather than inherited: this is the only ball in the set that
    //must not pick the dome up, and a smoothness left at 1 would put a mirror rim round it.
    SurfaceSpecular surface;
    surface.Highlight = StoneBroadHighlight;
    surface.Environment = StoneEnvironment;
    surface.Smoothness = StoneSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2, answered with ZERO and this is the one technique that may. BallEmission is still
    //called rather than dropped, and with the stone's own colour: EmissiveStrength is a renderer uniform,
    //so a rock plane drawn with it left at some other style's figure would otherwise glow grey, and this
    //way the answer is arithmetic instead of a promise about a caller. See the header.
    shaded.rgb += BallEmission(SrgbToLinear(StoneBody), input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately. A rock is part of the
    //cluster the wave walks through, and it has to carry both: the landing wave, because a wave that
    //stops at a rock tells the player the rock is not connected when it is, and the ceiling's ALARM,
    //because every ball in that wave has to say the same thing - a field of rocks staying calm while the
    //glass comes down would be the one place the alarm could be missed.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //The flare is white here with none of the ball's hue carried into it, and RippleWhiten is not
        //consulted: that term exists to lift the channels a COLOURED ball is missing, and a neutral grey
        //has none missing. What it means for a rock is simply that it lights up.
        float3 lit = shaded.rgb + RippleStrength * amount;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelStone
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL StoneVS();
        PixelShader = compile PS_SHADERMODEL StonePS();
    }
};
