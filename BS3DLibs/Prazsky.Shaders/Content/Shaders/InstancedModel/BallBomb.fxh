//===================================================================================================
//⚠ REDRAWN AS A MINE IN #625, and the paragraphs below the next rule are the banded casing's history
//(#326, #341), kept because every measurement in them still governs how this reads: the charge colour, the
//far-glow floor, the resting glow the burial rule cannot reach, the slow deep beat. What changed is the
//FIGURE. The generated references offered the banded lantern (today's bomb, heavier) and a naval mine - a
//matte black sphere split by two glowing red seams crossing at right angles, a red eye where they meet and
//a few blunt studs - and the owner picked the mine ("vypadá lépe"). It is a cleaner far read: two lines
//and a point are resolvable long after five bands have blurred into a striped ball, and a crossed sphere
//with a lit eye is armed in a way no ordinary ball ever looks.
//
//  - THE SEAMS: the equator and one meridian (object space, so a rolling mine visibly rolls), each a soft
//    band of MineSeamWidth in the direction's own component; the charge burns in them as it burned in the
//    grooves, and they converge to BombFarGlow past their band limit exactly as the grooves did.
//  - THE EYE: where the two seams cross, on both sides (+Z and -Z; a level-placed ball shows -Z to the
//    player), a disc of MineEyeRadius at MineEyeGain times the charge - the hot point the references put there.
//  - THE STUDS: eight blunt bumps on the cube's diagonals, as far from both seams as a stud can be. One
//    abs() folds all eight octants onto one test, so they cost one dot product. They stand proud in the
//    relief only; the silhouette stays the sphere (a drawn ball may not leave its cell).
//  - THE CASING: matte, warm near-black - a mine is cast and painted, not machined. The warmth is still
//    what keeps it off Type8.
//THE LIVE BOMB (#326) — the thirteenth ball technique, and the third of the three that belong to a KIND
//rather than to a style. The stone draws the ball that can never be matched and the clear glass the ball
//that has no colour yet; this draws the ball that is about to take a hole out of the cluster.
//
//IT HAS TO READ AS ARMED BEFORE IT IS HIT, at play distance, on all ten materials — a bomb the player
//does not notice until it goes off is a bomb that feels like a bug.
//
//⚠ THAT BAR WAS MISSED AT PLAY DISTANCE UNTIL THE OWNER LOOKED AT IT, and what was wrong was the size of
//the figure rather than anything about the light. Their words: "the red lines are really too narrow, so
//from a distance the bomb does not look like a bomb - the red blinking is not visible". Three things were
//wrong at once and all three are fixed below:
//
//  a. THE GROOVES WERE TOO NARROW AND TOO SHARP (0.22 at a sharpness of 2.4). The comment that stood on
//     them argued for narrow, because the charge has to read as coming out of a JOIN - true of a bomb held
//     at arm's length, and wrong about a ball two dozen pixels across that has to say ARMED before any
//     join can be resolved at all.
//  b. THE BAND LIMIT FADED THE PATTERN OUT SIX TIMES TOO EARLY (a factor of 2.0 on the band count), so the
//     casing dissolved into a flat glow while its bands were still several pixels wide. See its own note.
//  c. THE BEAT WAS TOO FAST TO BE SEEN. Heartbeat's lit window is a fixed FRACTION of its cycle, so a
//     faster beat blinks SHORTER, not more: at 2.6 the flash was about 30 ms. See BOMB_PULSE_SPEED.
//
//Measured after, on the hanging bomb of Bombs.json through a fixed camera at the stand-off a level is
//played from, eight captures across one cycle: the casing runs 106 to 151 codes of red - a 1.43x swing
//that several of the eight caught, where before the same sampling caught the flash once in six.
//
//⚠ AND IT STILL READ AS THE WRONG COLOUR: "a dark banded casing whose bands go from dim brown to bright
//ORANGE and back" is what that same session wrote down, and the owner's verdict on it was that a bomb
//blinks red. That is #341 and it is a hue fault only - the size of the figure, the band limit and the beat
//are all as this section leaves them, and the red channel measures identical before and after the fix. See
//BombCharge, which is where the whole of it lives.
//
//⚠ AND ONE MEASURING NOTE THAT INVALIDATES PART OF THE SWEEP ABOVE: this beat CANNOT BE PHASE-SWEPT WITH
//F5 HELD ON. Freezing the simulation is the standing trick for stopping the cluster swaying between
//captures, and #326's rig used it - but it pins the pulse clock too, so a settle sweep then photographs one
//phase over and over. Measured during #341: twenty captures across a cycle WITH F5 spanned 109 to 115
//codes and read as a beat that had collapsed; twelve captures WITHOUT it, same build, same camera, spanned
//96 to 142. The cluster hangs still enough by nine seconds that the sway costs nothing on a ball this size.
//
//⚠ AND A PROCESS WARNING WORTH MORE THAN ANY OF THE ABOVE. Three rounds of "measurements" before this one
//were taken against a Testbed that was NOT running the shader being edited: the captures come from
//bin\net10.0-windows (the Debug output), the builds had been made with -c Release, and MGCB additionally
//SKIPS an .fx whose .xnb is newer and then copies nothing. Every conclusion drawn from those rounds was
//withdrawn - including one written into five files. Before believing any capture of a shader change, check
//that bin\<tfm>\Content\Shaders\InstancedModel.xnb is NEWER than the .fx. A green-tinted constant is the
//two-minute way to prove the pixels on screen came from the file on disk.
//
//What makes it read as a bomb, in the order the eye picks it up:
//  1. IT IS BANDED. The casing is cut into latitude segments by deep grooves, evenly spaced in ANGLE
//     (through acos) rather than in height, or the bands would bunch at the poles and read as a wound
//     ball rather than a cast shell. Nothing else in this file is banded that way: the vinyl's gores are
//     meridians, the wool's bands lie at changing angles, and the marble's veins are not bands at all.
//  2. THE GROOVES ARE LIT FROM INSIDE. The charge is in the seams, not on the surface — a dark shell with
//     light coming out of its joins is the one figure that says "there is something in there", and it is
//     also what lets the pulse be visible without the whole ball flashing, which at this depth and speed
//     would strobe.
//  3. THE CASING IS DARK AND SLIGHTLY WARM. Dark so the charge has something to be bright against, and
//     warm so it is not the 8-ball — Type8 is the one colour a near-black ball can be confused with, and
//     the rock's own header records the same trap from the grey end.
//
//⚠ THE CHARGE COLOUR IS A CONSTANT AND THE TINT IS IGNORED, which is the rule the stone states first and
//the reason this is a kind rather than a style: a bomb wearing one of the thirteen is a lie the player
//acts on — they would aim that colour at it and it would not match. It is a bomb on a bubble level and on
//a lava level alike, because "that one is different" has to survive all ten materials or it is not a
//signal.
//
//THE SIX-POINT CONTRACT, in the order the ball-technique header states it:
//  1. The dissolve clip, both signs, first and branchless.
//  2. The heartbeat through BallEmission — and here it is the WHOLE READ rather than a floor, carried by
//     the charge colour scaled by the seam mask, so the light comes out of the grooves and not the shell.
//  3. The ripple in both meanings. The landing wave is warm-white (the casing has no hue of its own worth
//     carrying) and the alarm is the flat alarm colour every ball wears — a field of bombs staying calm
//     while the glass comes down would be the one place the alarm could be missed.
//  4. SurfaceOcclusion, including #303's burial depth.
//  5. ApplySeaSubmerge then ApplyKillPlaneFade on the way out.
//  6. A rotation cue in OBJECT space, and the bands are it: they turn with the ball, so a spinning bomb is
//     visibly spinning. It is the one thing a smooth dark sphere cannot say for itself.
//===================================================================================================

//How many bands the casing is cut into from pole to pole. FIVE since the owner looked at it: six thin
//bands is a thread, and a thread reads as a screw rather than as a bomb. Fewer bands is half of what makes
//each glowing line bigger; the other half is the width below.
static const float BombBandCount = 5.0;

//How much of a band's width the glowing groove takes, and how sharply it cuts.
//
//⚠ THESE TWO WERE 0.22 AND 2.4, AND THAT IS THE FAULT THE OWNER NAMED: "the red lines are really too
//narrow, so from a distance the bomb does not look like a bomb - the red blinking is not visible". The
//comment that stood here argued for narrow ("the charge has to read as coming out of a JOIN, and a wide
//groove is a stripe painted on"), and that argument is right about a bomb held at arm's length and wrong
//about the object this actually is: a ball a couple of dozen pixels across, which has to say ARMED before
//the player can see any join at all. A join nobody can resolve is not a join, it is a missing signal.
//
//Both halves had to move, and the second is the one that is easy to miss. The WIDTH is how far the mask
//reaches from a groove's centre; the SHARPNESS is a power on top of it, so a high one pinches the bright
//core back down however wide the reach is - 2.4 was throwing away most of what the width bought. Widened
//and softened together the glowing line is several times its old area, which is also the only thing that
//helps once the bands start to blur: the mask's own mean is what survives the band limit, and that mean is
//exactly what these two set.
static const float BombGrooveWidth = 0.35;
static const float BombGrooveSharpness = 1.5;

//Depth of the grooves in world units. The second largest ball figure in this file after the stone's
//roughness, because a groove that only changes colour reads as paint and this one has to read as a gap
//between two pieces of metal.
static const float BombGrooveDepth = 0.030;

//The casing, in sRGB like every other colour written here. Dark, and WARM on purpose: a neutral near-black
//ball is Type8, which is the one colour this could be confused with, and the same trap the stone records
//from the grey end. Not black either — a body at zero has nothing for the sky to sit on and the silhouette
//disappears against a dark dome.
static const float3 BombCasing = float3(0.115, 0.098, 0.092);

//And the charge that burns in the seams.
//
//⚠ IT WAS (1.0, 0.46, 0.13) AND THE OWNER'S VERDICT ON IT WAS ONE WORD: it blinks ORANGE, and it should
//blink RED. The comment that stood here defended that value with an argument rather than a measurement —
//"well past white in the red channel so it survives the tonemap as a HOT thing rather than as an orange
//one; the emission below multiplies it, so this is a direction more than a colour". The measurement that
//refutes it was standing twenty lines up this same file the whole time: the casing runs 106 to 151 codes of
//red and reads "from dim brown to bright orange and back". A casing that peaks at 151 never clips, so
//nothing is ever pushed past white and the value written here IS the colour that reaches the player, at
//full strength. An intent stated in a comment does not become true by being multiplied.
//
//⚠ AND WHAT MADE IT ORANGE WAS THE GREEN, NOT THE RED, which is why "push the red harder" could not have
//fixed it: red was already pinned at 1.0 and had nowhere to go. Hue at this end of the wheel is
//60 * (G-B) / (R-B), so with red pinned the only lever left is green, and 0.46 of it is nearly a fifth of
//the red in linear light. Blue is not taken to zero along with it, on purpose: a channel pinned at zero
//makes the deepest part of the charge a one-channel colour, which aliases hard along a groove's edge and
//reads as a decal rather than as light.
//
//MEASURED BOTH WAYS IN ONE SESSION, twelve phases of one beat through the fixed camera below (meadow,
//sky 1, nopost, nooverc, ssaa 2, campos=0,4,30 camtarget=0,5.5,0, the top-right bomb of Bombs.json, mean
//over a disc of radius 8):
//
//                       casing floor        casing on the beat      hue
//    orange (0.46 G)    96 / 39 / 17        142 / 60 / 19           17 to 20 degrees
//    red    (0.15 G)    96 / 20 / 15        142 / 23 / 15           3.8 degrees, flat across the beat
//
//THE RED CHANNEL DOES NOT MOVE BY ONE CODE, at either end, and the beat's swing is 1.48x both times. Only
//green does, which is the whole of the hue and — because green carries 0.7152 of a colour's luminance
//against red's 0.2126 — also about a third of the charge's light. That loss is real and it is invisible
//here: the bomb is read by its red against a near-black casing, so what was lost is the part that was
//making it amber. See BombRestingGlow for the compensation that was tried on the strength of the luminance
//figure alone, and measured to be unnecessary.
static const float3 BombCharge = float3(1.0, 0.15, 0.05);

//A ring of studs round the casing's waist — rivets. Cheap (one more sine pair) and worth it: they are the
//only part of the figure that survives when the ball is small enough that the bands blur together, and a
//studded sphere is unmistakably a made object rather than a dark ball.
//THE MINE'S FIGURE (#625): the seams' half-width in the direction's component, how sharply the charge
//peaks in them, how deep they cut, the eye's radius (in the same units, measured as distance on the unit
//sphere) and how much hotter than a seam it burns, and the eight studs' angular radius and height.
static const float MineSeamWidth = 0.05;

//The two seam planes' normals, TILTED off the object axes on purpose. Square to the axes, a level-placed
//mine seen face-on is a red "+" centred on the ball - a crosshair, which says "aim here", the one reading a
//special must not have (the gravity well's bullseye, #630). Tilted, the seams cross off-centre at a slant
//and read as the joins of a casing. The eye sits where they cross: along the cross product of the two.
static const float3 MineSeamNormalA = float3(0.20, 0.95, 0.24);
static const float3 MineSeamNormalB = float3(0.93, -0.18, 0.32);
static const float3 MineEyeCore = float3(1.0, 0.75, 0.55);
static const float MineSeamSharpness = 1.4;
static const float MineSeamDepth = 0.022;
static const float MineEyeRadius = 0.13;
static const float MineEyeGain = 1.8;
static const float MineStudRadius = 0.16;
static const float MineStudHeight = 0.03;

static const float BombStudCount = 12.0;
static const float BombStudSize = 0.16;
static const float BombStudDepth = 0.016;

//What the charge falls back to once the bands are under a pixel — see the note in BombPS. Well under the
//groove's own peak of 1, so a bomb at arm's length is still a dark casing with light in its joins rather
//than a glowing marble, and well over the mask's honest mean of about 0.065, so a bomb across the arena is
//a live thing rather than an 8-ball. Measured at the game camera's stand-off on Bombs.json.
static const float BombFarGlow = 0.35;

//How much of the charge burns whatever the beat is doing and whatever the ball is buried under - the floor
//the heartbeat rides on. See the note at its use for why it cannot be BallEmission's own resting term.
//Half, so a bomb at rest is unmistakably lit and a bomb on the beat is still visibly brighter: the swing
//has to survive as well as the floor, or the odd one out stops being the one that moves.
//
//⚠ THIS WAS RAISED TO 0.65 DURING #341 AND PUT BACK, and the reason is worth more than the constant. The
//red recolour above drops about a third of the charge's LUMINANCE (green carries 0.7152 of it against red's
//0.2126), so it looked obvious that the casing needed the loss handed back. It did not: measured across
//twelve phases before and after the recolour, on the same camera in the same session, the casing's floor is
//96 codes of red BOTH TIMES. Luminance fell; the red channel the bomb is actually read by did not move,
//because red was pinned at 1.0 before and after. The 10 % shortfall being compensated for was against the
//figure 106 quoted in #326's own note — measured through a different rig, in a different session, and
//therefore a citation rather than a measurement. Raising this term also costs the beat: it lifts the floor
//and the flash by the same linear amount, so the swing falls (1.48x to 1.36x at 0.65), and the swing is
//what #326 was protecting.
static const float BombRestingGlow = 0.5;

//Machined metal: a tight highlight and a real mirror of the dome, which is what separates a casing from
//the stone's matte aggregate at a glance. The environment term is what draws the sky along the silhouette
//and is most of why a dark ball is visible at all.
//⚠ Matte since #625: the mine is cast and painted, and the references' casing is a soft sheen with no
//mirror in it. Enough environment is kept to put the sky along the silhouette on a dark dome.
static const float BombHighlight = 0.40;
static const float BombEnvironment = 0.50;
static const float BombSmoothness = 0.40;

//Where the seam mask is read from: the fraction of a band, folded so 0 is the middle of a groove.
float BombSeams(float3 direction)
{
    //Even in ANGLE, not in height — the poles are where a height-banded sphere crowds its bands together.
    float latitude = acos(clamp(direction.y, -1.0, 1.0)) / 3.14159265;

    float band = frac(latitude * BombBandCount);
    float toSeam = abs(band - 0.5) * 2.0;

    return pow(saturate(1.0 - toSeam / max(BombGrooveWidth, 1e-4)), BombGrooveSharpness);
}

//The rivets, as a second mask on the same construction: a ring of them round the equator.
float BombStuds(float3 direction)
{
    float azimuth = atan2(direction.z, direction.x) / 6.28318531;
    float ring = 1.0 - saturate(abs(direction.y) / BombStudSize);
    float around = 1.0 - saturate(abs(frac(azimuth * BombStudCount) - 0.5) * 2.0 / BombStudSize);

    return saturate(ring * around);
}

//The mine's two seams, 0..1 with the peak on the seam: the equator (y = 0) and the x = 0 meridian.
float MineSeams(float3 direction)
{
    float equator = pow(saturate(1.0 - abs(dot(direction, normalize(MineSeamNormalA))) / MineSeamWidth), MineSeamSharpness);
    float meridian = pow(saturate(1.0 - abs(dot(direction, normalize(MineSeamNormalB))) / MineSeamWidth), MineSeamSharpness);

    return max(equator, meridian);
}

//The eye where the seams cross, on both sides of the ball.
float MineEye(float3 direction)
{
    float3 crossing = normalize(cross(normalize(MineSeamNormalA), normalize(MineSeamNormalB)));
    float away = length(direction - crossing * sign(dot(direction, crossing)));

    return 1.0 - smoothstep(MineEyeRadius * 0.6, MineEyeRadius, away);
}

//The eight studs on the cube's diagonals, folded onto one octant: the angular distance from (1,1,1)/sqrt 3.
float MineStuds(float3 direction)
{
    float toDiagonal = acos(saturate(dot(abs(direction), float3(0.57735, 0.57735, 0.57735))));
    float t = saturate(1.0 - toDiagonal / MineStudRadius);

    return t * t * (3.0 - 2.0 * t);
}

float4 BombPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Band-limited on the band count, the same argument ReliefOctave makes: past the point where one band is
    //under a pixel the grooves are noise, and noise on a dark ball is what makes a cluster shimmer.
    //
    //⚠ THE FACTOR WAS 2.0 AND IT FADED THE PATTERN OUT SIX TIMES TOO EARLY, which is most of what the owner
    //was looking at when they said the bomb does not read as one from a distance. A band spans about
    //pi / BombBandCount of the surface parameter - some 0.63 radians at five bands - so the grooves only
    //start aliasing when the footprint approaches that. At 2.0 the limit reached zero at a footprint of 0.1,
    //while a band was still several pixels wide and perfectly resolvable: the casing dissolved into a flat
    //glow at exactly the range the figure was supposed to be doing its work. Half the band count puts the
    //fade where Nyquist actually is, so the bands stay drawn as long as they can be seen.
    //⚠ The limit is the mine's since #625: a seam is 2 * MineSeamWidth of the unit direction across, so it
    //goes under a pixel when the footprint reaches about that. The banded casing's own argument, above.
    float bandLimit = saturate(1 - footprint / (MineSeamWidth * 2.0));

    float seam = MineSeams(direction) * bandLimit;
    float stud = MineStuds(direction) * saturate(1 - footprint / (MineStudRadius * 2.0));
    float eye = MineEye(direction);

    //⚠ THE CHARGE IS NOT BAND-LIMITED WITH THEM, and the first build of this technique was: the emission
    //below was multiplied by the band-limited `seam`, so as the bands went under a pixel the glow went with
    //them and the beat — which is the WHOLE armed read — was limited out of existence at exactly the
    //distance it exists to work at. Measured on Bombs.json through the game camera: warmth on the bomb's
    //pixels fell from 168 to 65 codes of R-B against a close-up, and the ball read as a plain black sphere.
    //
    //So the charge converges to a FLOOR instead of to nothing: once the figure cannot be resolved the whole
    //casing carries the glow. That is deliberately NOT energy-conserving — the grooves are about a
    //fifteenth of the surface, so spreading their light honestly over the ball leaves it as dark as before —
    //and it is the same call the stone's own header records making, in the other direction: a rock had to be
    //given emission it does not physically have because it read as the 8-ball without it. What has to
    //survive distance is the SIGNAL, and the signal is "this one is live".
    //The eye is not band-limited: it is a point of light, and a point of light survives any distance.
    float charge = max(lerp(BombFarGlow, seam, bandLimit), eye * MineEyeGain);

    //The casing, darkened in the grooves: a joint is in shadow before it is lit from inside, and skipping
    //that made the seams read as painted-on stripes when the charge was at the bottom of its beat.
    float3 color = SrgbToLinear(BombCasing) * (1 - 0.45 * seam) * (1 + 0.25 * stud);

    //Contract point 6. Both figures cut into one height field, so a single perturbation covers them - the
    //vinyl skin's construction. The studs stand PROUD and the grooves cut IN, which is the sign difference
    //that makes them read as two different features rather than as one dented surface.
    float height = (stud * MineStudHeight - seam * MineSeamDepth);

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, height);

    SurfaceSpecular surface;
    surface.Highlight = BombHighlight;
    surface.Environment = BombEnvironment;
    surface.Smoothness = BombSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2, and on this technique it is the whole read rather than a floor. The charge is
    //multiplied by the SEAM MASK, so what beats is the light coming out of the joins and not the shell -
    //at the depth and speed DrawBombs pushes, a whole ball flashing this hard would strobe. The studs take
    //a share of it too, at a fraction, so a bomb small enough that its bands have blurred out is still
    //visibly alive.
    //⚠ THE FLOOR IS ADDED HERE AND NOT THROUGH BallEmission, and that is the second measured fault of this
    //technique. BallEmission's resting half is multiplied by OCCLUSION SQUARED - #303's burial rule, so that
    //the cluster is not sitting on an emissive floor no ambient occlusion can take it below - and a bomb
    //standing inside a pile is precisely the ball that most has to be seen. Six captures through the game
    //camera at arbitrary phases read the bomb at R-B 1.9 (dead black) five times out of six, first at pulse
    //depth 1.0 and then AGAIN at 0.6, which is what proved the depth was never the dial: the resting term was
    //being occluded away whatever its size. So the charge gets a light the burial rule does not reach, and
    //the heartbeat rides on top of it - the beat is unoccluded by BallEmission's own design, which is what
    //keeps it legible in the cluster's interior.
    shaded.rgb += SrgbToLinear(BombCharge) * charge * BombRestingGlow;

    shaded.rgb += BallEmission(SrgbToLinear(BombCharge) * charge, input.WorldPosition, occlusion);

    //The eye's hot core: a point of light is paler at its centre, and it is what the far read keeps.
    shaded.rgb += SrgbToLinear(MineEyeCore) * pow(eye, 3.0) * 0.8;

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //Warm-white rather than the casing's own hue: RippleWhiten exists to lift the channels a COLOURED
        //ball is missing, and a near-black shell is missing all of them - carrying its hue into the flare
        //would make the one ball in the wave that stays dark.
        float3 lit = shaded.rgb + RippleStrength * amount;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness, amount * RippleAlarmCoverage);

        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
    }

    //Contract point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelBomb
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL BombPS();
    }
};
