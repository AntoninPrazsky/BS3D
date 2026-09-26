//===================================================================================================
//CLEAR GLASS (#325) — the twelfth ball technique, and the second of the two that belong to a KIND
//rather than to a style. The stone above draws the ball that can never be matched; this draws the ball
//that has NO COLOUR YET: BallKind.Transparent, hanging in the map until a shot lands beside it and
//gives it one.
//
//WHAT IT HAS TO READ AS IS AN ABSENCE, and that is the whole design problem the issue set. The game
//already has a transparent ball material — the bubble — so "transparent" was taken; on a Bubble level
//every ball is a see-through film and this kind would have had nothing left to say. What it says
//instead is that it is EMPTY: no dye anywhere in it, no gores, no film colour, no interference
//rainbow, and a body that hides almost nothing. Beside a dyed film it reads as the one shell nobody
//has coloured in, which is exactly what it is.
//
//So this is not "BubblePS with the tint set to white". A white film still carries the film — the
//iridescence, the drainage, the dyed transmission — and the whole of what this technique is, is those
//terms being ABSENT. What is left is a rim, a highlight, a mould seam and the sky bent through the
//middle, which is what an undyed glass sphere actually looks like.
//
//THE SIX-POINT CONTRACT, in the order the header states it:
//  1. The dissolve clip, both signs, over DissolvePixelSize cells — and on this technique it is also
//     LOAD-BEARING RATHER THAN COSMETIC: the clear-to-colour crossing draws this shell at +d and the
//     new colour's own technique at -d, so the two partition the ball's pixels between them and the
//     glass turns into a coloured ball with no blend, no sorting and no third program. See
//     BallDrawFrame.Route, which is the other half of it.
//  2. The heartbeat, through BallEmission like everything else — at HOLLOW_EMISSION and PulseDepth 0,
//     so it is a floor rather than a beat. A colourless ball pulsing would be announcing a colour it
//     does not have.
//  3. The ripple in both meanings. The landing wave is white here (there is no own colour to carry
//     towards white) and the alarm is the flat alarm colour every ball wears; both raise the ALPHA as
//     well, the film's own lesson — a wave through a transparent ball that only changed its colour
//     would be invisible against open sky.
//  4. SurfaceOcclusion, including #303's burial depth, which arrives inside it.
//  5. ApplySeaSubmerge then ApplyKillPlaneFade on the way out.
//  6. A rotation cue in OBJECT space, and this is the one that had to be designed rather than
//     inherited: everything else about clear glass is a function of the eye and the sky, so without
//     this a spinning glass ball is a still one — the mirror trap the header names, arriving on the
//     one material that is mostly not there. The answer is the seam a cast glass marble carries, at
//     HollowSeamFrequency: object-space, so it turns with the ball, and diegetic rather than drawn on.
//===================================================================================================

//How many mould seams run over the shell — the rotation cue's dial. See the renderer's own property.
float HollowSeamFrequency;

//How wide a seam is, as a fraction of the band between two of them, and how much it bends the surface.
//A cast seam is a RIDGE: it catches the highlight and refracts what is behind it, which is what makes
//it visible on a body that is otherwise almost nothing.
static const float HollowSeamWidth = 0.055;
static const float HollowSeamRelief = 0.5;

//How much the glass bends what is behind it, as a fraction of the screen. Small: a solid glass sphere
//is a lens and would invert the world, which is a different object from the thin shell this is - and
//an inverted image inside every ball of a cluster is a picture nobody can read. What this does is
//pull the background sideways along the rim, which is the cue that says "something is there".
static const float HollowRefraction = 0.35;

//The rim's own power and how hard it burns. Higher than the film's, and this is the read: an undyed
//shell has nothing BUT its edge, so the edge has to be sharp and bright enough to draw the sphere on
//its own.
static const float HollowRimPower = 2.6;
static const float HollowRimStrength = 0.55;

float4 HollowPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Point 1, character for character what every other ball technique does. On this one it is also the
    //crossing itself - see the technique's header.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    //Turned to face the eye, so both walls of the shell go through one piece of arithmetic - the film's
    //own trick, and the reason this technique can share its two-pass draw.
    float3 normal = normalize(input.WorldNormal) * BubbleShell;
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);

    //POINT 6, THE MOULD SEAM, and it is applied to the NORMAL rather than to a colour on purpose: this
    //material has no colour to draw a figure in, and a figure in the normal is shading, which cannot be
    //swamped by shading (#311's rule, arriving on the one material with nothing else to say). Two
    //half-mould seams meet at the poles of the ball's own object space, so the ridge turns with it.
    //
    //Band-limited against the pixel footprint like every other ball figure, or a distant glass ball's
    //seam aliases into a crawling sparkle - and this one is thin, so it would be the first to go.
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;
    float seamPhase = frac(atan2(direction.z, direction.x) / 6.2831853 * HollowSeamFrequency);
    float seamDistance = abs(seamPhase - 0.5) * 2.0;
    float seam = saturate((seamDistance - (1.0 - HollowSeamWidth)) / max(HollowSeamWidth, 1e-4))
        * saturate(1 - footprint * HollowSeamFrequency / 3.14159265);

    //The ridge tilts the normal along the seam's own direction. A height field rather than a modelled
    //bulge, exactly as the gem's facets are, because a pixel shader cannot move a vertex and does not
    //have to: PerturbNormalFromHeight gives the mapping for free.
    normal = normalize(PerturbNormalFromHeight(normal, input.WorldPosition, seam * HollowSeamRelief));

    float facing = saturate(dot(normal, eyeVector));

    //Point 4. The burial depth of #303 rides inside this, so a glass ball deep in the pile is as dark
    //as everything around it - which matters more here than anywhere: an unoccluded clear shell in the
    //middle of a cluster would be a hole in the picture.
    float occlusion = SurfaceOcclusion(input.WorldPosition, normal, input.OcclusionData);

    //WHAT IT REFLECTS. A dielectric like the film, so 4 % head-on rising to a full mirror at the rim -
    //and no dye multiplying it, which is the whole difference. This is the term that puts the sky, the
    //city or the cavern's crystals into the ball and is most of what says "glass" rather than "hole".
    float3 fresnel = FresnelSchlick(DielectricF0.xxx, facing, 1.0);
    float3 reflected = SkyRadiance(reflect(-eyeVector, normal)) * fresnel * SpecularAmbientStrength * occlusion;

    //The lamps' pinpoints, and they are doing a job here they only decorate elsewhere: on a body this
    //empty the highlight is one of the three things naming the sphere.
    float3 hotspot = 0;
    AddBubbleHighlight(normalize(KeyLightPosition - input.WorldPosition),
        DirLight0SpecularColor * KeySunlight(input.WorldPosition, normal), normal, eyeVector, hotspot);
    AddBubbleHighlight(-DirLight1Direction, DirLight1SpecularColor, normal, eyeVector, hotspot);
    AddBubbleHighlight(-DirLight2Direction, DirLight2SpecularColor, normal, eyeVector, hotspot);
    hotspot *= DirLightStrength * occlusion;

    //And the scene's own lamps.
    float3 lampWash = 0;
    AddSceneLights(input.WorldPosition, normal, eyeVector, lampWash, hotspot);

    //WHAT IT HIDES. Almost nothing face-on (HOLLOW_BODY_OPACITY) and much more along the rim, where the
    //eye looks the long way through the glass. The far wall is worth less than the near one for the
    //film's own reason.
    float rim = pow(1.0 - facing, HollowRimPower);
    float wall = BubbleShell > 0 ? 1.0 : BubbleInnerWall;
    float alpha = saturate(BubbleBodyOpacity + (1.0 - BubbleBodyOpacity) * rim) * wall;

    //WHAT COMES THROUGH IT, and it is NOT dyed - which is the sentence this whole technique exists to
    //be able to write. Taken as a brightness the way the film's transmission is (the same departure,
    //the same reason: a backdrop's hue may not get a veto over what a ball looks like), so a clear ball
    //carries the light behind it without carrying its colour.
    //
    //The refraction offset is what makes it read as a lens rather than as a grey wash: the sky is
    //sampled along a direction bent by the surface normal, so the background slides as the ball turns
    //and the seam above bends it visibly where it crosses.
    float3 bent = refract(-eyeVector, normal, 1.0 - HollowRefraction);
    float skyThrough = dot(SkyRadiance(any(bent) ? bent : -eyeVector), float3(0.2126, 0.7152, 0.0722));
    float lampThrough = dot(lampWash, float3(0.2126, 0.7152, 0.0722));

    //Times the alpha for the film's own reason - what a wall transmits is what it took out of the
    //picture behind it - so a shell that hides nothing tints nothing and cannot fog the open sky.
    float3 through = (skyThrough * alpha + lampThrough) * occlusion;

    //AND THE EDGE, which on a colourless ball is the whole silhouette. White by construction: there is
    //no own colour to carry towards white, which is what the film does, and that is the point.
    float3 rimGlow = float3(1, 1, 1) * (rim * HollowRimStrength * wall * occlusion);

    //Point 2. White, faint, and at PulseDepth zero from the draw - a floor, never a beat.
    float3 emitted = BallEmission(float3(1, 1, 1), input.WorldPosition, occlusion) * wall;

    float4 shaded = float4(reflected + hotspot + through + emitted + rimGlow, alpha);

    //The pinpoint closes the glass under itself, as it does the film's.
    shaded.a = saturate(shaded.a + max(hotspot.r, max(hotspot.g, hotspot.b)) * BubbleHighlightOpacity * wall);

    //Point 3, in both meanings, and both raising the alpha - a transparent ball that only changed
    //colour would carry a wave nobody can see against the sky.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);

        //The landing wave has no own colour to whiten, so it IS white; the alarm is the flat colour
        //every ball in that wave wears whatever it is made of.
        float3 lit = shaded.rgb + RippleStrength * amount * wall;
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness * wall, amount * RippleAlarmCoverage);

        //A select and not an if: the sign varies per instance and a branch would diverge in one draw.
        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
        shaded.a = saturate(shaded.a + amount * wall * (input.Ripple < 0 ? RippleAlarmCoverage : 0.5));
    }

    //Point 5.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelHollow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL HollowPS();
    }
};
