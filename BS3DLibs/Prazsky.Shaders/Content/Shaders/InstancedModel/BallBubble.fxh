//===================================================================================================
//THE GLASS BUBBLE (#258): the same sphere, the same instances, the same once-per-frame walk and the
//same vertex shader, shaded as a soap film blown round a pocket of air instead of as a moulded vinyl
//skin. A map names which of the two it hangs (Level.Balls), and nothing else about the game changes -
//the lattice, the physics, the match rule and the score are all about a ball's TYPE, which this does
//not touch.
//
//It is a second TECHNIQUE and not a branch inside PatternPS, for the reason measured on this project's
//other big shaders: a runtime branch over a whole alternative shading model costs the union of both
//register allocations in every wavefront, and these passes are occupancy-bound. Two techniques compile
//two programs and each draw picks one.
//
//WHAT MAKES IT READ AS A BUBBLE, in the order the eye picks them up:
//  1. It is TRANSPARENT, which is what the drawing states have to buy: the shell writes no depth on the
//     way through and the two walls are drawn as two passes with opposite cull modes (BallRenderSet.Draw
//     carries that whole argument). Everything below assumes the output is PREMULTIPLIED - rgb is the
//     light the film sends the eye, alpha only how much of the picture behind it that film hides.
//  2. The RIM. A film is crossed nearly edge-on along the silhouette, so it both mirrors most and hides
//     most there. One expression gives both, which is why a bubble is a bright ring around a nearly
//     empty middle rather than a tinted disc.
//  3. The IRIDESCENCE - the soap rainbow. Light reflected off the film's two faces interferes, and which
//     wavelengths survive depends on how far apart those faces are along the line of sight. That is a
//     real thin-film computation here rather than a rainbow texture, so it moves correctly: it stretches
//     towards the rim, drains downward under gravity and marbles with the film's own thickness.
//  4. The ball's COLOUR, which the game cannot do without: thirteen types have to stay apart at a glance
//     across a whole cluster. Physical soap is very nearly colourless, so this is where the model is
//     deliberately left: the film is dyed, and it carries its type's hue in what it transmits, in what it
//     radiates on the heartbeat and in what it flares with when a ripple reaches it.
//===================================================================================================

//Which wall of the shell this draw is putting out: +1 the near one, seen from outside, -1 the far one,
//seen from inside. A per-PASS uniform rather than anything on the instance because it IS a pass - the two
//walls are separate draw calls with opposite cull modes, which is how a hollow shell gets drawn without
//sorting three thousand balls against one another every frame.
float BubbleShell;

//Optical thickness of the film seen face-on, in whole waves of the reference wavelength - the pitch of the
//interference, and so the whole character of the rainbow. Under about 1 the film is in its first order and
//shows broad single-colour washes; over about 4 the fringes crowd together into a fine oily marbling.
float BubbleFilmThickness;

//How much of its type's colour the film carries into what it transmits. The one dial that decides whether
//a cluster of thirteen colours is still readable at a glance, which is why it is a uniform the C# side
//states and defends rather than a constant buried here.
float BubbleTintStrength;

//What the film hides where it is seen face-on, before the rim adds its own - and the one figure that decides
//whether a ball's colour is nameable, because what a film does NOT hide is the backdrop arriving UNTINTED.
//The C# side states and defends it (BallRenderSet.BUBBLE_BODY_OPACITY carries the arithmetic and the history).
float BubbleBodyOpacity;

//Ratios of the reference wavelength to the three the eye samples - red 680 nm over red, green and blue
//(550 and 450 nm). The interference phase scales with 1/wavelength, so these ARE the phase ratios, and
//using them rather than a hand-picked triple is what makes the fringe order come out in spectral sequence
//instead of as three unrelated sinusoids.
static const float3 BubbleWavelengthRatio = float3(1.0, 1.236, 1.511);

//A hanging film drains: gravity pulls the soap down, so the crown thins towards the black spot a real
//bubble shows just before it bursts and the underside carries the heavy fringes. Measured against the
//WORLD normal and not the object one, deliberately, because gravity does not turn with the ball.
static const float BubbleFilmDrain = 0.7;

//...and how far the film's own turbulence marbles that, on the same octave sum the vinyl skin uses for its
//moulding. This one IS in object space, so it turns with the ball - it is what replaces the beach ball's
//gores as the cue that a bubble is rolling.
static const float BubbleFilmVariation = 0.5;

//How hard the shell crowds its light and its coverage into the silhouette. 3 to 4 is a soap film's own
//falloff closely enough; lower reads as thick blown glass, higher as a wire ring with nothing in it.
static const float BubbleRimPower = 3.5;

//The bright edge on top of that: tighter than the coverage rim, so it reads as a RING drawn on the ball
//rather than as the ball being darker in the middle - how strong it is, and how far it is carried to white.
static const float BubbleEdgePower = 6.0;
static const float BubbleEdgeStrength = 1.5;
static const float BubbleEdgeWhiten = 0.12;

//How much of the rainbow survives into the reflection. Short of 1 so a white highlight stays white at its
//core - a fully iridised specular has no neutral in it and stops reading as a light source.
static const float BubbleIridescence = 0.8;

//The film's own highlight: far tighter than the vinyl skin's 40, because a soap film is a mirror and not a
//gloss coat, and correspondingly brighter so the pinpoint survives being that small.
static const float BubbleGloss = 220.0;
static const float BubbleGlossStrength = 1.6;

//How much of the highlight counts as coverage. A specular is light coming OFF the front of the film, so it
//is not something transparency takes away (SpecularAlphaWeight makes exactly this argument for the crystal
//cup) - but a pinpoint the picture behind shows straight through does not read as a highlight at all, so
//the brightest part of it closes the film underneath itself.
static const float BubbleHighlightOpacity = 0.5;

//What the far wall is worth against the near one. Both walls are drawn for every bubble and the far one is
//seen THROUGH the near one, so at parity a bubble would carry two full rims and read as a double-walled
//jar. Half is enough to say the thing is hollow.
static const float BubbleInnerWall = 0.5;

//How far the occlusion vector's reading is stretched before it counts as "screened by the pile". That vector
//is a SUM of up to twelve unit vectors over twelve, and neighbours on opposite sides cancel — a ball with a
//full layer of them towards the camera reads about a quarter, so a quarter has to mean most of the way in.
static const float BubbleScreenReach = 3.0;

//...and how much of a shell that reading is allowed to take away. Short of 1 deliberately: a ball deep in the
//pile should go faint, not vanish, or the cluster reads as a hollow shell of balls around nothing and the
//holes a played field is full of stop being holes.
static const float BubbleScreenFade = 0.8;

//How near the silhouette the interference is still evaluated honestly. The path through a film goes as
//1/cos, which runs away at the rim into fringes finer than a pixel; clamping the cosine caps the pitch
//there instead, and the band-limit below fades what is left.
static const float BubbleGrazingClamp = 0.2;

//THE ONE PLACE THE FILM PARTS COMPANY WITH THE SKIN OVER WHAT THE NEIGHBOURS TAKE, and it is not a
//preference. An opaque ball hides the ones behind it, so occlusion there only has to darken the crevices
//between touching spheres; a film hides nothing, so every ball in the pile reaches the eye and what a
//pixel shows is the SUM over four or five of them. At the skin's falloff a cluster's interior added up to
//a lamp: photographed on the space scene, where the sky contributes nothing and the balls' own light is
//all there is, the middle of a 438-ball cluster came out a flat pastel wash with no ball in it anywhere.
//Squaring the occlusion is the whole correction - a fully surrounded bubble keeps about a fifth of its
//light rather than half - and it is applied to the EMISSION as well, which is the second half of the same
//point: "a light buried in the pile is the one that should still show" is an argument about a ball you can
//only see one of, and it is false of a pile you can see all the way into.
static const float BubbleOcclusionPower = 2.0;

//One lamp's pinpoint on the film. The rig's own AddLight is not it: that is Blinn-Phong over the vinyl's
//SpecularPower with a Lambert diffuse beside it, and a film has neither - it mirrors, and everything else
//it does with light it does by transmitting.
void AddBubbleHighlight(float3 towardsLight, float3 lightSpecular, float3 worldNormal, float3 eyeVector,
    inout float3 hotspot)
{
    float3 halfway = normalize(towardsLight + eyeVector);

    hotspot += lightSpecular * pow(saturate(dot(worldNormal, halfway)), BubbleGloss);
}

float4 BubblePS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //The dissolve cut, character for character what PatternPS does and for its reasons: a bubble being
    //transmuted in the bore has to go away in blocks of the screen like every other ball. Branchless.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    //Turned to face the eye. On the far wall the geometric normal points away from the camera - it is the
    //inside of the shell - and every term below is about the film as the eye meets it, so one multiply by
    //the pass's own sign puts both walls through the same arithmetic.
    float3 normal = normalize(input.WorldNormal) * BubbleShell;
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float facing = saturate(dot(normal, eyeVector));

    float3 tint = SrgbToLinear(PatternPrimaryColor);

    //How much surface one screen pixel covers, over the ball radius - the same yardstick the vinyl skin
    //band-limits its moulding against, and here what keeps the fringes from strobing on a distant ball.
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //THE FILM. Thin at the crown, heavy underneath, marbled by its own turbulence.
    float drain = lerp(1.0 - BubbleFilmDrain, 1.0 + BubbleFilmDrain, saturate(0.5 - 0.5 * normal.y));
    float thickness = BubbleFilmThickness * drain
        * (1.0 + BubbleFilmVariation * SurfaceRelief(direction, footprint));

    //Interference. The two faces of the film are `thickness` apart along the normal and the eye crosses
    //them at `facing`, so the path between them - and with it the phase - goes as 1/cos: the fringes stretch
    //and multiply towards the rim, which is exactly where a real bubble shows its bands.
    float path = thickness / max(facing, BubbleGrazingClamp);
    float3 interference = 0.5 + 0.5 * cos(6.2831853 * path * BubbleWavelengthRatio);

    //Faded out where one pixel starts to span a whole fringe, or the rainbow turns into coloured noise that
    //crawls as the camera moves. Band-limited against the SCREEN-SPACE derivative of the path — how much of
    //a fringe one pixel actually covers — and not against the pixel footprint times the path, which is what
    //this said first and what is wrong: that measures the pixel's reach across the BALL, and a ball nine
    //tenths of a fringe wide is perfectly resolvable while one pixel of it is not. Measured, that fade was
    //already fully closed at the stand-off a level is played from, so the whole effect existed only in the
    //arithmetic. Nyquist puts the limit at half a fringe per pixel; this holds full colour to a fifth of one.
    float fringe = fwidth(path);
    float bands = saturate((0.5 - fringe) * 3.3);

    //The interference averages 0.5, so doubling it leaves a tint that averages white and the fade lands on
    //exactly that.
    float3 film = lerp(float3(1, 1, 1), interference * 2.0, BubbleIridescence * bands);

    //What the neighbours take, and they take it harder than they take it from a skin — see
    //BubbleOcclusionPower, which is the whole of why.
    float occlusion = pow(SurfaceOcclusion(input.WorldPosition, normal, input.OcclusionData),
        BubbleOcclusionPower);

    //What it mirrors. A soap film is a dielectric like every other surface here, so it reflects the same
    //4 % head-on and rises to a full mirror along the rim - and being smooth, it shows an IMAGE of the sky
    //rather than the sky's average.
    float3 fresnel = FresnelSchlick(DielectricF0.xxx, facing, 1.0);
    float3 reflected = SkyRadiance(reflect(-eyeVector, normal)) * fresnel * film
        * SpecularAmbientStrength * occlusion;

    //The lamps' pinpoints, the key one under the weather like every other surface in the scene.
    float3 hotspot = 0;
    AddBubbleHighlight(normalize(KeyLightPosition - input.WorldPosition),
        DirLight0SpecularColor * KeySunlight(input.WorldPosition, normal), normal, eyeVector, hotspot);
    AddBubbleHighlight(-DirLight1Direction, DirLight1SpecularColor, normal, eyeVector, hotspot);
    AddBubbleHighlight(-DirLight2Direction, DirLight2SpecularColor, normal, eyeVector, hotspot);
    hotspot *= DirLightStrength * BubbleGlossStrength * occlusion;

    //And the scene's own lamps - the campfire, the neon ring, the cavern's crystals. Their diffuse share is
    //kept: a nearby fire does not just spark off a bubble, it fills it with warm light.
    float3 lampWash = 0;
    AddSceneLights(input.WorldPosition, normal, eyeVector, lampWash, hotspot);

    hotspot *= film;

    //HOW MUCH OF THE PILE STANDS BETWEEN THIS SHELL AND THE EYE, and it is the answer to the one thing that
    //looked wrong about a bubble cluster: several layers of balls all showing through one another with EQUAL
    //clarity, where the eye expects each layer behind the last to be fainter than it. Nothing was fading them,
    //and the reason is in DrawShell's own note — the far walls are drawn with no depth write, so every ball in
    //the pile puts its inner rim into the picture whether or not four other balls stand in front of it, and
    //bucket order decides which of those lands on top.
    //
    //The proper cure is a back-to-front sort, which this renderer structurally cannot do (colour is a per-draw
    //uniform). This is the order-INDEPENDENT stand-in, and it costs one dot product: the occlusion vector
    //already carries the DIRECTION this ball's occupied neighbours lie in, so dotting it against the eye asks
    //exactly "are my neighbours between me and the camera?". Positive means the ball is screened by the pile
    //and is faded; a ball on the cluster's near face has its neighbours BEHIND it, so the dot goes negative and
    //it is left alone. A shot in flight and a loaded round carry a zero vector and are untouched by
    //construction.
    //
    //It is a per-BALL figure, so a shell fades as a whole rather than pixel by pixel, which is right: what is
    //being modelled is the depth of the pile in front of it, not the shape of its own surface.
    float screened = saturate(dot(input.OcclusionData.xyz, eyeVector) * BubbleScreenReach);

    //WHAT THE FILM HIDES. Face-on almost nothing; along the rim, where the eye looks the long way through
    //it, nearly everything. The far wall is worth half, for the reason BubbleInnerWall gives.
    float wall = (BubbleShell > 0 ? 1.0 : BubbleInnerWall) * (1.0 - screened * BubbleScreenFade);
    float rim = pow(1.0 - facing, BubbleRimPower);
    float alpha = saturate(BubbleBodyOpacity + (1.0 - BubbleBodyOpacity) * rim) * wall;

    //WHAT COMES THROUGH IT, in the ball's own colour: the film is dyed, so the light it passes is dyed too.
    //Tied to `alpha` on purpose - what a wall transmits is what it took out of the picture behind it, so a
    //film that hides nothing tints nothing, and the two cannot drift into a coloured haze over open sky.
    //
    //WHAT ARRIVES IS TAKEN AS A BRIGHTNESS AND NOT AS A COLOUR, and that is a deliberate departure from the
    //physics, made for the one constraint this game cannot trade away: thirteen types have to stay apart at a
    //glance under eighteen domes. Multiplied as a colour, a dye can only pass what the backdrop happens to
    //contain - and a RED film over the meadow's BLUE sky passes almost nothing, so the opening block's red
    //pyramid came out pink whatever the dye was set to. The backdrop's hue does not get a veto over the
    //ball's; how much light there is does. Rec. 709 luminance, the same weights the rest of the pipeline uses.
    float skyThrough = dot(SkyRadiance(-eyeVector), float3(0.2126, 0.7152, 0.0722));

    //The scene's own lamps take the same reading, and for the same reason: a campfire seen through a green
    //film is green light, not a warm cast on a green ball.
    float lampThrough = dot(lampWash, float3(0.2126, 0.7152, 0.0722));

    float3 through = (skyThrough * alpha + lampThrough) * tint * BubbleTintStrength * occlusion;

    //AND THE EDGE, which is what actually makes a bubble a bubble rather than a tinted disc. Along the
    //silhouette the eye looks the long way ALONG the film, and the same path length that makes it opaque
    //there is the path everything it carries has been dyed over - so a bubble's rim is both its brightest
    //part and its most saturated one.
    //
    //It is here and not left to the Fresnel mirror above because that mirror shows the SKY, and four of the
    //scenes have next to none: under space, the cavern, a night dome or the pit, the reflection returns
    //nothing at all and every ball came out a flat coloured circle with no edge on it. Photographed on the
    //space scene, which is the case that found it.
    //
    //Carried to white the way the ripple's flare is, and normalised to peak 1 first for the ripple's other
    //reason: the dark types (black, navy, brown) would otherwise have no edge to speak of, and how brightly
    //a film catches light along its rim has nothing to do with what colour it was dyed.
    float tintPeak = max(tint.r, max(tint.g, tint.b));
    float edge = pow(1.0 - facing, BubbleEdgePower);
    float3 rimGlow = lerp(tint / max(tintPeak, 1e-3), float3(1, 1, 1), BubbleEdgeWhiten)
        * (edge * BubbleEdgeStrength * wall * occlusion * film);

    //Emission on the heartbeat, the vinyl skin's own - but OCCLUDED, which its is deliberately not. See
    //BubbleOcclusionPower: that rule is an argument about a ball whose neighbours you cannot see past, and
    //it is exactly false of a pile of films. Times the wall weight, so the two shells together radiate one
    //ball's worth.
    float beat = Heartbeat(PulseTime * PulseSpeed - dot(input.WorldPosition, PulseDirection) / max(PulseWavelength, 1e-4));
    float3 emitted = tint * EmissiveStrength * StillEmission * lerp(1 - PulseDepth, 1, beat) * wall * occlusion;

    float4 shaded = float4(reflected + hotspot + through + emitted + rimGlow, alpha);

    //The pinpoint closes the film under itself (see BubbleHighlightOpacity). After the colour, because it
    //is coverage the highlight ADDS rather than a share of it that the highlight is scaled by.
    shaded.a = saturate(shaded.a + max(hotspot.r, max(hotspot.g, hotspot.b)) * BubbleHighlightOpacity * wall);

    //The landing ripple, the same wave PatternPS carries and switched off by the same uniform. Two
    //differences, both forced by the film being transparent: the flare has to raise the ALPHA as well or a
    //wave running through open sky is invisible, and the alarm - which replaces the ball's colour rather
    //than adding to it - has to close the film almost completely, or a cluster "turning red" would only be
    //tinting the sky behind it.
    [branch]
    if (RippleStrength > 0)
    {
        float amount = abs(input.Ripple);
        float peak = max(tint.r, max(tint.g, tint.b));

        float3 lit = shaded.rgb + lerp(tint / max(peak, 1e-3), 1.0, RippleWhiten) * (RippleStrength * amount * wall);
        float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * RippleAlarmBrightness * wall, amount * RippleAlarmCoverage);

        //A select and not an if: the sign varies PER INSTANCE, so a branch on it would diverge inside one
        //draw call.
        shaded.rgb = input.Ripple < 0 ? alarmed : lit;
        shaded.a = saturate(shaded.a + amount * wall * (input.Ripple < 0 ? RippleAlarmCoverage : 0.5));
    }

    //The sea's submerge fade and the kill plane's, both exactly as the vinyl skin takes them - a bubble
    //that misses sinks into the same dark water and is culled under the same island.
    shaded = ApplySeaSubmerge(shaded, input.WorldPosition);

    return ApplyKillPlaneFade(shaded, input.WorldPosition);
}

technique InstancedModelBubble
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL BubblePS();
    }
};
