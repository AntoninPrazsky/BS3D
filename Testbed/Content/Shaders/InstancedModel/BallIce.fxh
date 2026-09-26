//===================================================================================================
//FROSTED ICE (#307): a ball of cloudy frozen water. Light goes IN, scatters a short way and comes back
//out, so the colour arrives from inside the ball rather than off its face; a network of internal cracks
//catches the light in bright threads, and a cool rim sits on the silhouette.
//
//IT IS OPAQUE, AND THAT IS THE ONE DECISION THAT MATTERS HERE. #272 framed this as "a cold cousin of
//the glass bubble", and building it that way would have been the expensive mistake: a second
//transparent style costs the whole two-pass opposite-cull machinery in BallRenderSet.Draw, doubles the
//ball pass, and re-opens every argument BUBBLE_BODY_OPACITY settled about what a film hides. It would
//also stand next to the bubble needing to be told apart from it at a glance, or one of the two is
//redundant.
//
//And it is not even the right physics. FROSTED ice is not CLEAR ice: frosting IS short-range subsurface
//scattering, which is an opaque phenomenon - you cannot see a level through a frosted marble. So the
//body is lit as a diffuse solid with a real translucency term through it, and the result is a genuinely
//different MATERIAL from the film rather than a recolour of it: one is a hollow thing you see the level
//through, the other is a solid you cannot. Two styles, two reads, one ball pass each.
//
//THE FIGURE IS BUILT IN THE NORMAL FIRST AND IN THE COLOUR SECOND, which is #305 and #311's shared
//lesson applied deliberately (see WoolPS's header): a crack cuts a groove that catches the light, and
//only then adds a bright thread along itself. The groove reads at every tint because it is shading; the
//thread is what makes it ICE rather than a scratch.
//
//THE NET IS A FRACTURE AND NOT A LATTICE, and that is #337's whole correction. Until it, the cracks were
//three SeamLine fields crossing: three sets of straight, evenly spaced bands around great circles. Three
//regular line families over a surface is the construction a quilted hide is stitched on, and the owner's
//playtest read the ball as exactly that - padded upholstery, not ice. NO WIDTH AND NO FREQUENCY COULD
//HAVE FIXED IT, because what was wrong was the REGULARITY and not the size of the figure; a finer
//quilting is still quilting. Ice does not crack in a lattice. It breaks into irregular polygonal plates
//that meet three at a point, along edges of unequal length that open, taper and die out - which is a
//Voronoi diagram, so the net is one (VoronoiEdgeCell3, over the object-space direction, seamless on a
//sphere where any azimuth/elevation lookup would pinch at the poles and tear at the branch cut).
//
//AND AT PLAY DISTANCE IT IS THE PLATES THAT CARRY THE READ, NOT THE CRACKS. A hairline net is a
//high-frequency figure and it is the first thing to go sub-pixel - the lesson #326 paid for on the bomb,
//in the same file. So each plate also takes its own value from the cell's id: a figure the size of a
//PLATE, which is still resolvable long after the thread between two of them is not. Where the net has
//faded, the ball is a thing broken into facets of frozen water rather than the smooth pale sphere that
//kept being mistaken for the porcelain (#339).
//
//A per-plate NORMAL TILT was tried first and dropped, and the reason is worth keeping: a piecewise
//constant field has no gradient inside a plate and an infinite one at its border, so pushing it through
//PerturbNormalFromHeight - which differentiates - puts a one-pixel spike along every crack, which is the
//hard-checkerboard trap ReliefOctave's comment in SceneRelief.fxh warns about. The value figure below carries the
//same read with no derivative in it.
//===================================================================================================

//How many fracture cells the ball is broken into - the plate size. Cells sit one unit apart in the
//field's own space and the ball is a unit sphere scaled by this, so the count goes as its AREA: about
//4*pi*f^2 over the whole ball, a dozen or so on the hemisphere facing the camera. Reading that as a great
//circle (2*pi*f) is what put fifty pebbles on a ball in this change's first pass. Far over it the ball is
//shattered gravel, far under it two or three continents. Named "crack frequency" still, because that is
//what the C# side has always called it.
float IceCrackFrequency;

//How wide a crack opens, as a fraction of a cell. This is the MEAN and not the width: every stretch of
//crack multiplies it by its own wander (IceCrackOpenMin..IceCrackOpenMax), because a net drawn at one
//width all over a ball is a drawn net - which is what the seam lines were, and half of why they read as
//stitching rather than as fracture.
float IceCrackWidth;

//How strongly each plate differs from its neighbours: the low-frequency half of the figure, and the half
//that survives to play distance. Stated by the C# side rather than fixed here because it is the same kind
//of decision MARBLE_VEIN_CONTRAST is - how loud the figure is against thirteen tints that must stay apart.
float IcePlateContrast;

//How brightly the silhouette goes cool and pale - the cold, and the one figure the C# side states,
//because at full strength it eats the tint on the rim of every ball at once and a cluster is mostly rims.
float IceRim;

//The bend put through the fracture before the cells are read, and the three wave directions it is built
//from. A Voronoi's borders are dead straight, and a ball tiled in straight-edged polygons reads as a
//geodesic DOME - a made object - rather than as something broken; bowing the domain a little breaks that
//without touching what makes the net irregular, which is the cells themselves and not their edges.
static const float IceFractureBend = 0.09;
static const float IceFractureBendFrequency = 3.4;
static const float3 IceWarpA = float3(0.77, 0.41, -0.49);
static const float3 IceWarpB = float3(-0.33, 0.86, 0.39);
static const float3 IceWarpC = float3(0.52, -0.38, 0.77);

//How far a stretch of crack opens or closes against IceCrackWidth, and how quickly it changes along its
//length. The floor is well under one: a crack that thins to a fifth of the mean is a hairline that
//disappears, which is where the dead ends and the branch stubs come from - a fracture stops, and a
//stitched seam never does.
static const float IceCrackOpenMin = 0.18;
static const float IceCrackOpenMax = 1.55;
static const float IceCrackOpenFrequency = 7.0;
static const float3 IceCrackOpenAxis = float3(0.44, -0.72, 0.53);

//THE PLATE FIGURE IS TWO TERMS BECAUSE ONE OF THEM ALWAYS HAS A BLIND TINT, which is #305 and #311's
//lesson arriving here in its own shape. A plate that is only ever LIGHTENED is invisible on white, where
//the tonemap's shoulder eats the difference; a plate that is only ever DARKENED is invisible on the
//8-ball, whose tint is a 0.045 grey with nothing under it. So a plate does both, from opposite ends:
//IcePlateShade takes value OUT of the body for the plates whose id is low, and IcePlateContrast (which
//the C# side states) adds cool light back for the ones whose id is high. Every tint is on one of those
//two axes and the pale ones are on both.
static const float IcePlateShade = 0.16;

//How much white rides in the cool light a plate adds, so a dark tint gets a plate figure at all - the
//rim below carries the same 0.3-ish term for the same reason, and it is the only thing that separates
//two plates of an 8-ball from each other.
static const float IcePlateWhite = 0.35;

//Where the two halves of the figure stop being worth drawing, each measured against ITS OWN size - which
//is the whole point, and is #326's lesson stated the other way round. There the bomb's glow was faded on
//the finest thing in the pattern and the coarse read went down with it; here the two halves are different
//sizes by a factor of ten and are faded separately.
//
//The CRACK is faded against its own WIDTH, not against the cell it runs round: a border is a line of
//width/frequency in direction units, so footprint * frequency / width is that line measured in pixels (in
//pairs of them, since footprint is ddx plus ddy). It is faded against the WANDERED width rather than the
//mean, so the hairline stretches drop out first and the open ones hold - which is how a fracture recedes
//anyway. At the shipped figures this crosses over at about play distance, and that is not a defect but
//the design: what carries the ball there is the plate.
//
//The 1.5 IS THE POINT OF THAT CONSTANT and not a fudge: it holds the crack at full strength until the
//line is down to about a pixel across and only then fades it over the last octave. Written as a plain
//ramp from zero footprint - which is what every other limit in this file is, because every other one
//rides a whole WAVELENGTH - the crack came out 43 % faded on a ball ninety-nine pixels wide, where the
//line was still four and a half pixels across and perfectly sharp. A limit on a thin line has to start
//late; a limit on a wave can start at once.
//
//The PLATE is faded against the cell, and at the shipped frequency it never engages except on the
//smallest LOD - a plate is most of a ball, and a ball is off the screen before one is sub-pixel.
static const float IceCrackBandLimit = 1.5;
static const float IcePlateBandLimit = 0.5;

//How deep a crack cuts, in world units, and how brightly it glows along its length. The groove is what
//makes it read at every tint; the glow is what makes it read as ICE. The glow follows the stone's own
//lighting rather than being a fixed white, on the same argument MarbleVeinPale carries.
static const float IceCrackDepth = 0.012;
static const float IceCrackGlow = 0.55;

//The cool cast of both the rim and the crack glow. Not pure white: ice is blue because water absorbs red
//over a path length, and this is the one place that fact is worth stating rather than deriving.
static const float3 IceCold = float3(0.72, 0.88, 1.0);

//How sharply the rim crowds into the silhouette. Broader than a mirror's, because what is happening there
//is a long path through scattering ice and not a reflection off a face.
static const float IceRimPower = 2.6;

//Frost's own texture under the cracks: fine grain that keeps the surface from reading as polished, at a
//small fraction of a crack's depth.
//
//FOUR WAVES ALONG MIXED DIRECTIONS, and it was ONE until #337 - along a single axis, which is not grain
//but STRIPES. The dense crack net used to hide them; once the plates were large and smooth the hatching
//came straight out, in the same shot and for the same reason as the quilting it was drawn on top of. The
//ratios are irrational-ish so the four never settle into a weave, which is the trap the scene surfaces'
//own relief records (see SurfaceReliefWorld's header, and why it uses seven).
static const float IceFrostFrequency = 38.0;
static const float IceFrostDepth = 0.2;
static const float3 IceFrostA = float3(0.61, 0.55, -0.57);
static const float3 IceFrostB = float3(-0.48, 0.71, 0.51);
static const float3 IceFrostC = float3(0.39, -0.62, 0.68);
static const float3 IceFrostD = float3(-0.74, -0.36, 0.57);
static const float4 IceFrostRatio = float4(1.0, 1.37, 0.79, 1.91);

//What ice does with a highlight: present, but soft and wide. A frosted surface is not a mirror and not a
//matte one either - it is a mirror seen through a millimetre of scattering, which is exactly a broad lobe.
static const float IceHighlight = 0.55;
static const float IceEnvironment = 0.45;
static const float IceSmoothness = 0.5;

//ONE SEAM LINE: a narrow band either side of a wave's zero crossing, faded out on its own band-limit.
//Shared by the ice's cracks (#307) and the lava's plate seams (#310), which want the same LINE and
//opposite things from it - the ice brightens along it because a crack is an internal face catching the
//light, the lava glows through it because there is molten rock behind it, and both cut a groove.
//
//Written against the RAW SINE rather than through ReliefOctave DELIBERATELY - ReliefOctave fades its
//AMPLITUDE towards zero as the pixel grows, and a test on abs(v) < width would then read the whole ball
//as one seam the moment the wave stopped being resolvable. Fading the LINE is what is wanted.
float SeamLine(float3 direction, float3 waveDirection, float frequency, float width, float footprint)
{
    float v = sin(dot(direction, waveDirection) * frequency);
    float fade = saturate(1 - footprint * frequency / 3.14159265);

    return (1 - smoothstep(0, width, abs(v))) * fade;
}

//THE FRACTURE, in one call: the crack running between the plates and the plate the pixel is standing on.
//Returns (crack, this plate's own value in 0..1, how much of the plate figure the pixels can still carry),
//the crack already faded on its own terms and the plate handed over with its limit for the caller to
//fade towards ITS no-op, which is not zero but a half - a plate that cannot be resolved has to converge
//on the average of all of them, or the ball's whole value walks as it recedes.
//
//The bend goes through ReliefOctave and not a raw sine deliberately: a warp that cannot be resolved has
//to stop moving, or the edges crawl on the small distant balls exactly where the net should be settling
//down. It is the one place in this style where fading the field's AMPLITUDE is right - what fades then is
//the bend, and a straight crack is a perfectly good crack, where a faded SeamLine would have turned the
//whole ball into one seam (the trap that comment records two functions up).
float3 IceFracture(float3 direction, float footprint)
{
    float3 bend = float3(
        ReliefOctave(direction, IceWarpA, IceFractureBendFrequency, footprint),
        ReliefOctave(direction, IceWarpB, IceFractureBendFrequency * 1.31, footprint),
        ReliefOctave(direction, IceWarpC, IceFractureBendFrequency * 0.83, footprint));

    float2 fracture = VoronoiEdgeCell3((direction + bend * IceFractureBend) * IceCrackFrequency);

    //A pixel measured in cells - the unit both band limits below are written in, and the reason neither
    //has to be told how big a ball is or how far away it stands.
    float cells = footprint * IceCrackFrequency;

    //How far this stretch of the crack has opened. Wandering along the crack rather than across it, so
    //one edge goes from a gap to a hairline and back over its own length instead of the whole net
    //breathing together.
    float wander = 0.5 + 0.5 * ReliefOctave(direction, IceCrackOpenAxis, IceCrackOpenFrequency, footprint);
    float width = IceCrackWidth * lerp(IceCrackOpenMin, IceCrackOpenMax, wander);

    float crack = (1 - smoothstep(0, width, fracture.x))
        * saturate(IceCrackBandLimit - cells / max(width, 1e-3));

    float plateLimit = saturate(1 - cells * IcePlateBandLimit);

    return float3(crack, lerp(0.5, fracture.y, plateLimit), plateLimit);
}

float4 IcePS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //The fracture, in OBJECT space (contract point 6): the plates and the cracks between them are INSIDE
    //the ice and they turn with it.
    float3 fracture = IceFracture(direction, footprint);
    float cracks = fracture.x;
    float plate = fracture.y;

    //Frost grain under them, so the surface is not polished between the cracks. Summed and not multiplied,
    //on the moulded vinyl's own lesson two hundred lines up: multiplying sines lays down a crosshatch.
    float frost = (ReliefOctave(direction, IceFrostA, IceFrostFrequency * IceFrostRatio.x, footprint)
        + ReliefOctave(direction, IceFrostB, IceFrostFrequency * IceFrostRatio.y, footprint)
        + ReliefOctave(direction, IceFrostC, IceFrostFrequency * IceFrostRatio.z, footprint)
        + ReliefOctave(direction, IceFrostD, IceFrostFrequency * IceFrostRatio.w, footprint))
        * (IceFrostDepth * 0.25);

    //The crack is a GROOVE first. Normal-space figure, so it reads at every tint - the lesson #305 and
    //#311 paid for between them.
    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition,
        (frost - cracks) * IceCrackDepth);

    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The plate's darkening half. Taken out of the BODY rather than off the finished pixel so it darkens
    //what the ball is made of and not the sky sitting on it - a plate is a thickness of ice with more
    //frozen water in front of it, not a shadow.
    float3 body = primary * (1 - IcePlateShade * (1 - plate));

    //A frosted surface is a mirror seen through a millimetre of scattering: the highlight is present but
    //broad, and the environment is picked up softly rather than sharply.
    SurfaceSpecular surface;
    surface.Highlight = IceHighlight;
    surface.Environment = IceEnvironment;
    surface.Smoothness = IceSmoothness;

    //The cracks take ambient like any other crevice - a plane inside the ice sees very little sky.
    float cavity = 1 - 0.5 * cracks;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(body, 1), 1, cavity, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //THE STYLE'S WHOLE READ, and the term a screenshot from the sun's own side cannot show: light that
    //went in, scattered and came back out. A ball with the key behind it glows through in its own colour
    //instead of going flatly black, which is what says "solid but not opaque to light". Same shape as the
    //vinyl skin's translucency and far stronger, which is the difference between a skin and a solid.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float through = pow(saturate(dot(-worldNormal, towardsKey)), 2);

    shaded.rgb += through * TranslucencyStrength * DirLight0DiffuseColor * primary * occlusion;

    //The cracks catch the light along their length: internal faces, so they BRIGHTEN where the vinyl's
    //welds darken. Cool-cast and following the ball's own colour rather than a fixed white, on the
    //argument MarbleVeinPale carries.
    shaded.rgb += cracks * IceCrackGlow * IceCold * primary * occlusion;

    //The plate's brightening half, and the half that carries the style at play distance: it is a figure
    //the size of a PLATE, so it is still there when the thread across one has gone sub-pixel. Cool and
    //part white for the reason the rim below is - a plate of an 8-ball has to differ from the plate next
    //to it, and nothing scaled by that tint can.
    shaded.rgb += plate * IcePlateContrast * IceCold * lerp(primary, 1.0, IcePlateWhite) * occlusion;

    //And the cold rim: a long path through scattering ice at the silhouette. Occluded, so a ball buried
    //in the pile does not outline itself.
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float rim = pow(1 - saturate(dot(worldNormal, eyeVector)), IceRimPower);

    shaded.rgb += rim * IceRim * IceCold * lerp(primary, 1.0, 0.3) * occlusion;

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

technique InstancedModelIce
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL IcePS();
    }
};
