//---------------------------------------------------------------------------------------------------
// HEAVY (#333)
//
// Cast iron poured in the ball's colour: a dense, dark, sand-cast sphere that is obviously the same
// colour as the ball beside it and obviously not made of the same stuff.
//
// ⚠ THE MASS IS NOT DRAWN HERE AND MUST NOT BE. What says "heavy" in this game is the BRANCH UNDER IT
// hanging lower -- the physics gives that feedback for free, which is the whole reason the kind is a
// mass and not an effect. This technique's job is narrower and is the half the physics cannot do: to
// say WHICH ball is the heavy one before it has done anything, on a still frame, at cluster distance.
//
// WHAT MAKES IT READ AS CAST IRON, in the order the eye picks it up:
//  1. THE VALUE. The body is crushed towards black and the colour survives as a tint rather than as a
//     brightness. Density reads as darkness, and it is the one cue that works at any size -- a ball
//     four pixels across has a value and nothing else.
//  2. THE POLISH OF METAL. A tight highlight over a raised sky reflection, which is the marble's own
//     measured lesson (a dark ball with a hard small sky in it reads as metal; the same ball with the
//     vinyl's broad sheen reads as a dark plastic ball). This is what separates it from Type8 -- black
//     is a COLOUR and is lit like every other ball, where this is a MATERIAL.
//  3. THE MOULD SEAM. One raised ring where the two halves of the mould met, with the metal flashed
//     along it -- a cast cannonball's own signature, and the cheapest true thing this surface can say.
//     ⚠ IT REPLACED A CAST GRAIN, and the reason is worth keeping: the grain was a mid-frequency octave
//     sum roughening the polish, and it was photographed at three strengths -- invisible at play
//     distance at all of them, and at the strongest, from one ball away, reading as faint FACETS rather
//     than as sand. A figure that only appears as an artefact is worse than no figure and it is paid
//     for on every heavy pixel. A hard line is the opposite: it survives being one pixel wide, which is
//     the property SeamLine was written for.
//
// The seam is in OBJECT space (contract point 6), so a heavy ball rolling is visibly rolling -- a ring
// turning with the ball says so far better than the gores do.
//---------------------------------------------------------------------------------------------------

//How much of the ball's own light is left in the body, and how far the tint is carried towards its own
//grey on the way. ⚠ BOTH ARE NEEDED AND THE FIRST ALONE WAS THE FIRST TRY'S FAULT: a tint that is only
//DARKENED keeps all of its hue, so the first build of this photographed as a chocolate-brown ball --
//an ordinary Type10 with the lights off, which is the one reading this must not have. Desaturation is
//the axis that says "metal" rather than "dark colour", and it is the marble header's finding once more:
//a figure has to move the colour along an axis the shading does not.
static const float HeavyBodyValue = 0.30;
static const float HeavyDesaturation = 0.40;

//The cold floor under the whole thing: cast iron is a colour of its own, and without this the dark tints
//(Type8, Type12) crush to a hole in the cluster rather than to a casting. Added rather than mixed, so a
//bright tint keeps its identity and a dark one is lifted onto the iron -- which is what a real casting
//does to both.
static const float3 HeavyIronColor = float3(0.22, 0.23, 0.26);
static const float HeavyIronFloor = 0.25;

//The mould seam: the axis the two halves parted along, and how wide the flash is. One ring and not a
//lattice of them -- a mould has two halves, and a second ring would read as a pattern rather than as a
//join. The axis is deliberately NOT one of the gores' (the vinyl's welds run through the poles), so a
//heavy ball's line is one no ordinary ball wears.
static const float3 HeavySeamAxis = float3(0.0, 1.0, 0.0);
static const float HeavySeamWidth = 0.06;

//How far the flash stands PROUD of the casting. Positive, and it is the one relief in this file that is
//-- the well's rings are troughs and the acid's beads sit on the surface; a flash is metal that escaped
//between the mould halves, so it is a ridge, and being a ridge is what catches the light along it.
static const float HeavySeamRelief = 0.05;

//What the flash does to the polish and to the colour: rougher than the casting either side of it (it was
//never dressed) and a touch darker, which is what a hand reads as a ridge even where the light does not
//catch it. SeamLine fades itself out on its own band-limit, so both converge to the plain casting as the
//ball recedes rather than turning the whole sphere into one seam.
static const float HeavySeamRoughening = 0.70;
static const float HeavySeamDarkening = 0.40;

//A dense polished body under the grain, and these three are the MARBLE'S measured move rather than a
//guess: cut the broad direct sheen back and raise the reflected sky instead. ⚠ Both figures are SCALES
//on what every other surface gets, where 1 is unchanged -- the first build of this set them to 0.86 and
//0.80, i.e. asked for LESS light than an ordinary ball, and photographed as dull plastic. A polished
//casting picks the sky up; that is the whole of what separates it from a dark vinyl ball at any size.
static const float HeavyHighlight = 1.30;
static const float HeavyEnvironment = 1.25;
static const float HeavySmoothness = 1.0;

float4 HeavyPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    float3 normal = normalize(input.WorldNormal);

    //ONE ring where the mould parted. SeamLine fades the LINE rather than the wave's amplitude, which is
    //the property that makes it survive to a pixel wide and then leave -- see its own comment for the trap
    //on the other side of that choice.
    float seam = SeamLine(direction, HeavySeamAxis, 1.0, HeavySeamWidth, footprint);

    //The body: the ball's own colour greyed, crushed, and set on a cold iron floor -- so the tint survives
    //as a TINT and never as a brightness. Linear throughout; these are radiances, not swatches.
    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 iron = SrgbToLinear(HeavyIronColor);

    float luminance = dot(primary, float3(0.2126, 0.7152, 0.0722));
    float3 greyed = lerp(primary, luminance.xxx, HeavyDesaturation);
    float3 color = (greyed * HeavyBodyValue + iron * HeavyIronFloor) * (1.0 - HeavySeamDarkening * seam);

    //Contract point 6: the flash stands PROUD of the casting, unlike the well's rings and the ice's
    //cracks, which are cut in. Metal that escaped between the mould halves is a ridge.
    float height = seam * HeavySeamRelief;
    float3 worldNormal = PerturbNormalFromHeight(normal, input.WorldPosition, height);

    SurfaceSpecular surface;
    surface.Highlight = HeavyHighlight;
    surface.Environment = HeavyEnvironment;

    //The figure, and the whole of it: the casting is polished and the undressed flash along the join is
    //not, which is what makes the line read even where no highlight is standing on it.
    surface.Smoothness = HeavySmoothness * (1.0 - HeavySeamRoughening * seam);

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Contract point 2. A heavy ball carries no light of its own -- it is the one special here that emits
    //nothing at all, which is itself the reading: a bomb burns, a well pulls, this just sits there. What
    //it does keep is the cluster's own heartbeat through BallEmission, so it breathes with its neighbours
    //rather than standing outside them; the RenderSet gives it a slower, deeper beat than theirs.
    shaded.rgb += BallEmission(color, input.WorldPosition, occlusion);

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
