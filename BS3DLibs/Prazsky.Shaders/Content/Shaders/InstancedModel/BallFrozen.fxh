//---------------------------------------------------------------------------------------------------
// FROZEN (#329, redrawn in #628 from generated references)
//
// A coloured ball sealed in a block of CLEAR ice: a rounded cube, glassy in the middle of each face and
// frosted along its edges and corners, with the ball itself visible inside it as a SPHERE - its own edge,
// its own shading, its own parallax as the view moves - a crack split through the block, and the block's
// edges catching the light.
//
// THE SHAPE IS THE WHOLE POINT, and it is not decoration. On a level whose BallStyle is already Ice,
// every ordinary ball is a frosted ball with its colour inside it, so no amount of frost, crazing or
// cold rim could tell a frozen ball from an ordinary one. Flat faces can, and nothing else in this game
// has any -- so the silhouette carries the kind, at any distance, under any style and any dome.
//
// It is cut the way the ROCK is cut (#340, StoneVS), and that is why this kind needed no second mesh,
// no draw order and no transparency sort: the same sphere every other ball uses, displaced INWARD in
// the vertex shader, so the physics body stays a sphere and the drawn block never leaves the cell the
// simulation gives it. What differs from the rock is that the target surface is an exact quadric-like
// solid rather than a noise field, which buys an exact analytic normal -- see FrozenVS.
//
// WHAT THE REFERENCES SAID (#628, six renders of a blue ball in an ice cube, three clear and three frosted):
// what makes it read as SEALED rather than as frosted or as sugar is that the ball inside is a BALL - a
// sharp sphere with its own highlight, seen through clear ice, sitting inside the block rather than
// painted on its face. #329's feathered colour disc read on the owner's sheet as a soft blue smudge on a
// sugar cube. So the ball inside is now ray-cast: the pixel's eye ray, in object space, is tested against
// a sphere of FrozenCoreRadius at the block's centre, and where it hits, that is what the pixel shows -
// the ball's colour shaded as a sphere, dimmed by the length of ice the ray crossed to reach it. The
// frost moved to where the references keep it, the edges and corners, and the faces went clear; a crack
// splits the block because every reference had one.
//
// Contract point 6 is answered twice over: the block itself turns with the body (a rotating cube is the
// strongest rolling cue in this file), and the frost, the crack and the sphere's parallax are all in
// OBJECT space on top of that. The only view-space terms are the ray itself and the rim.
//---------------------------------------------------------------------------------------------------

//How square the block is: the exponent of the superellipsoid |x|^p + |y|^p + |z|^p the sphere is cut
//back to. 2 would be the sphere again and infinity a hard cube; this is an ice cube out of a tray,
//with edges rounded enough to catch a highlight along their length.
static const float FrozenCubePower = 4.5;

//The pale ice the block is made of where the ray misses the ball. Cold and light, but well under white:
//what is white on this ball is the frost and the edges, and a body already at white leaves them nothing
//to be. Bluer than #329's, because the clear faces show more of it.
static const float3 FrozenIceTint = float3(0.58, 0.72, 0.86);

//The ball inside: its radius as a fraction of the sphere the block was cut from (the block's inscribed
//radius is 3^(1/p - 1/2) of it, 0.76 at p = 4.5, so 0.62 leaves a rim of ice all round), how much of
//its colour reaches the eye through the ice at the surface, and how fast the ice takes it back per unit
//of path - the block's depth is what makes the ball sit INSIDE it rather than on its face.
static const float FrozenCoreRadius = 0.66;
static const float FrozenCoreThrough = 0.92;
static const float FrozenIceAbsorb = 0.25;

//How the ball inside is shaded: a dome lit from the eye (the only light whose direction is known in
//object space), with a HIGH floor so its limb stays the colour and the sphere keeps a hard edge (a floor
//of 0.45 photographed as a soft-edged disc: the limb darkened into the ice), and a small highlight.
static const float FrozenCoreShadeFloor = 0.72;
static const float FrozenCoreHighlight = 0.35;
static const float FrozenCoreHighlightPower = 24.0;

//The frost, on the EDGES and CORNERS of the block rather than on the faces (#628): where the flat part of
//a face ends, measured on the largest component of the object-space direction (1 at a face centre and
//1/sqrt(3) = 0.577 at a corner), how far into the face the frost reaches, and the grain's frequency and
//amplitude. Calibrated against the Ice style's own figures (IceFrostFrequency / IceFrostDepth); #329's
//note records what 26 at ten times the amplitude looked like (corduroy).
static const float FrozenBevelStart = 0.84;
static const float FrozenFrostReach = 0.14;
static const float FrozenFrostFrequency = 41.0;
static const float4 FrozenFrostRatio = float4(1.0, 1.43, 0.71, 1.87);
static const float FrozenFrostDepth = 0.0028;

//How white the frost is, and how far it takes the surface towards white where it lies.
static const float3 FrozenFrostColor = float3(0.86, 0.92, 0.98);
static const float FrozenFrostWhiten = 0.75;

//The light along the rounded edges, and how tightly it sits on them. This is the figure that survives
//to play distance after the frost has gone sub-pixel: an edge is a whole edge long.
static const float FrozenEdgeGlow = 0.55;
static const float FrozenEdgePower = 2.0;

//The crack: one plane through the block, bent by low noise so it is a fracture and not a cut, brightened
//along its length because a crack is an internal face catching the light. SeamLine's own band limit.
static const float3 FrozenCrackAxis = float3(0.62, 0.53, -0.58);
static const float FrozenCrackFrequency = 1.4;
static const float FrozenCrackWidth = 0.05;
static const float FrozenCrackBend = 0.35;
static const float FrozenCrackGlow = 0.6;

//A polished surface where the face is clear, a scattering one where the frost lies: the frost lerps
//between the two.
//⚠ The environment is held down on purpose: at 0.8 the sky's reflection lay over the face as a veil that
//grew towards the face's edges, and the ball inside photographed as fading into the ice instead of ending.
static const float FrozenHighlight = 0.9;
static const float FrozenEnvironment = 0.5;
static const float FrozenSmoothness = 0.92;
static const float FrozenFrostSmoothness = 0.45;

//The cold rim at the silhouette: a long path through scattering ice. Weaker than the Ice style's, since
//this ball has hard edges doing that job already.
static const float FrozenRim = 0.30;
static const float FrozenRimPower = 3.0;

//The superellipsoid cut, as a scale on the unit direction: 1 at a corner, less everywhere else. Shared by
//the vertex shader (to cut) and the pixel shader (to recover the sphere's radius from the cut one).
float FrozenCarve(float3 direction)
{
    //Floored rather than taken raw, so pow() is never handed a zero base on a vertex sitting exactly on
    //an axis. The floor is far below anything the shape can see.
    float3 axis = max(abs(direction), 1e-5);
    float sum = pow(axis.x, FrozenCubePower) + pow(axis.y, FrozenCubePower) + pow(axis.z, FrozenCubePower);

    return pow(sum, -1.0 / FrozenCubePower) / pow(3.0, 0.5 - 1.0 / FrozenCubePower);
}

//The ball's own vertex shader: PatternVS with the block cut out of the sphere. Everything about the
//output that is not the shape is PatternVS's, deliberately -- StoneVS's own rule, and for its reason:
//every contract point the pixel shader answers is read off these same fields.
EyeRayVertexShaderOutput FrozenVS(VertexShaderInput input, InstanceInput instance)
{
    EyeRayVertexShaderOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

    float4 bonePosition = mul(input.Position, Bone);

    float radius = max(length(bonePosition.xyz), 1e-5);
    float3 direction = bonePosition.xyz / radius;

    //THE CUT. r(d) = (|dx|^p + |dy|^p + |dz|^p)^(-1/p) is the superellipsoid through the unit sphere's
    //six axis points; dividing by its value at a CORNER (3^(1/2 - 1/p), the largest it ever takes) puts
    //the corners on the sphere and everything else inside it. So the carve is inward-only, exactly as
    //the rock's is, and for the same reason: a drawn ball that left its cell would overlap a neighbour
    //the simulation says it is not touching.
    float carve = FrozenCarve(direction);

    //AND THE NORMAL IS EXACT, which is what a solid of revolution buys over the rock's noise field. Work
    //the rock's own n = d - tangential(grad r)/r through for this r and the two terms cancel outright:
    //grad(r)/r is -g/S with g = |d|^(p-1) sign(d), and g.d = S, so the tangential part is g/S - d and
    //the normal falls out as g itself. No chain-rule factor, no slope, no near-pole guard -- which is
    //also why the flat faces come out FLAT rather than very slightly domed.
    float3 axis = max(abs(direction), 1e-5);
    float3 objectNormal = normalize(pow(axis, FrozenCubePower - 1.0) * sign(direction));

    float3 cut = direction * (radius * carve);
    float4 worldPosition = mul(float4(cut, 1), world);

    //THE EYE IN OBJECT SPACE, for the ray the pixel shader casts at the ball inside - see EyeInObjectSpace.
    output.EyeObject = EyeInObjectSpace(instance);

    output.ObjectPosition = cut;
    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(objectNormal, world);
    output.OcclusionData = instance.Custom;
    output.Dissolve = instance.Dissolve;
    output.Ripple = instance.Ripple;

    return output;
}

float4 FrozenPS(EyeRayVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);

    //The SPHERE's direction, not the cut surface's normal: the carve only scales along d, so this is the
    //same parameter the vertex shader shaped the block with and the figures below line up with the faces
    //instead of drifting across them. The sphere's own radius comes back out of the carve.
    float3 direction = input.ObjectPosition / radius;
    float sphereRadius = radius / max(FrozenCarve(direction), 1e-4);
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Where on the block this pixel is: 1 in the middle of a face, falling through the rounded edge to
    //0.577 at a corner. Object space (contract point 6) -- the faces turn with the ball.
    float3 axis = abs(direction);
    float top = max(axis.x, max(axis.y, axis.z));
    float face = smoothstep(FrozenBevelStart - 0.10, FrozenBevelStart + 0.06, top);
    float edge = 1.0 - face;

    //THE FROST, on the edges and reaching a little into each face, nowhere near a face's middle: the
    //references' ice is clear where it is flat and white where it was cut. Summed octaves rather than
    //multiplied, on the moulded vinyl's own lesson, each band-limited on its own wavelength.
    float frostWhere = 1.0 - smoothstep(FrozenBevelStart, FrozenBevelStart + FrozenFrostReach, top);
    float frostGrain = (ReliefOctave(direction, float3(0.71, 0.52, -0.47), FrozenFrostFrequency * FrozenFrostRatio.x, footprint)
        + ReliefOctave(direction, float3(-0.36, 0.83, 0.42), FrozenFrostFrequency * FrozenFrostRatio.y, footprint)
        + ReliefOctave(direction, float3(0.55, -0.44, 0.71), FrozenFrostFrequency * FrozenFrostRatio.z, footprint)
        + ReliefOctave(direction, float3(-0.82, -0.31, 0.48), FrozenFrostFrequency * FrozenFrostRatio.w, footprint)) * 0.25;
    float frost = frostWhere * saturate(0.55 + 0.45 * frostGrain);

    //THE CRACK: one bent plane, brightened, with SeamLine's own fade so it leaves cleanly.
    float3 bent = direction + FrozenCrackBend * float3(
        ReliefOctave(direction, float3(0.31, 0.62, 0.72), 2.3, footprint),
        ReliefOctave(direction, float3(-0.77, 0.24, 0.59), 2.9, footprint),
        ReliefOctave(direction, float3(0.58, -0.79, 0.19), 2.1, footprint));
    float crack = SeamLine(bent, FrozenCrackAxis, FrozenCrackFrequency, FrozenCrackWidth, footprint);

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition,
        frostGrain * frostWhere * FrozenFrostDepth);

    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 ice = SrgbToLinear(FrozenIceTint);
    float3 frostColor = SrgbToLinear(FrozenFrostColor);

    //THE BALL INSIDE, ray-cast in object space: from the eye to this pixel and on into the block, against
    //a sphere of FrozenCoreRadius at the centre. Where the ray hits, the pixel shows the ball - shaded as
    //a dome from the eye, its highlight where the dome faces the eye - dimmed by the ice it crossed. The
    //hit's edge is one pixel soft on the closest-approach distance, so the ball inside has an EDGE, which
    //is the whole difference between a sealed sphere and a smudge.
    float3 rayOrigin = input.EyeObject;
    float3 rayDirection = normalize(input.ObjectPosition - rayOrigin);
    float along = -dot(rayOrigin, rayDirection);
    float closest = dot(rayOrigin, rayOrigin) - along * along;
    float core = FrozenCoreRadius * sphereRadius;
    float coreSquared = core * core;
    float closestWidth = max(fwidth(closest), 1e-6);
    float hit = 1.0 - smoothstep(coreSquared - closestWidth, coreSquared + closestWidth, closest);

    float depthIn = sqrt(max(coreSquared - closest, 0.0));
    float3 hitPoint = rayOrigin + (along - depthIn) * rayDirection;
    float3 hitNormal = hitPoint / max(core, 1e-4);
    float facingEye = saturate(dot(hitNormal, -rayDirection));
    float shade = FrozenCoreShadeFloor + (1.0 - FrozenCoreShadeFloor) * facingEye;
    float highlight = pow(facingEye, FrozenCoreHighlightPower) * FrozenCoreHighlight;

    //The ice the ray crossed: from the pixel on the surface to the hit, in radii, so a ball seen through a
    //corner is dimmer than one seen straight through a face.
    float iceLength = max((along - depthIn) - length(input.ObjectPosition - rayOrigin), 0.0) / max(sphereRadius, 1e-4);
    float through = FrozenCoreThrough * exp(-FrozenIceAbsorb * iceLength);

    float3 inside = lerp(ice, primary * shade + highlight, through);

    //THE BODY: the ball where the ray hits it, ice where it misses, frost over both where the frost lies,
    //the crack brightened through.
    float3 body = lerp(ice, inside, hit);
    body = lerp(body, frostColor, frost * FrozenFrostWhiten);

    SurfaceSpecular surface;
    surface.Highlight = FrozenHighlight;
    surface.Environment = FrozenEnvironment;
    surface.Smoothness = lerp(FrozenSmoothness, FrozenFrostSmoothness, frost);

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(body, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //THE EDGES, and this is the figure that carries the kind at play distance: an edge is a whole edge
    //long, so it is still there when the frost and the crack have gone sub-pixel. Cold and part white
    //for the Ice style's reason -- an edge of a navy block has to differ from the block next to it, and
    //nothing scaled by that tint can.
    shaded.rgb += pow(edge, FrozenEdgePower) * FrozenEdgeGlow * IceCold * lerp(primary, 1.0, 0.65) * occlusion;

    //The crack catches the light along its length.
    shaded.rgb += crack * FrozenCrackGlow * IceCold * occlusion;

    //The cold rim under it: a long path through scattering ice at the silhouette. Occluded, so a block
    //buried in the pile does not outline itself.
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float rim = pow(1 - saturate(dot(worldNormal, eyeVector)), FrozenRimPower);
    shaded.rgb += rim * FrozenRim * IceCold * occlusion;

    //Contract point 2, through BallEmission (#303), and IN THE BALL'S OWN COLOUR where the ball is: what
    //is glowing is the ball inside the ice, not the ice. A block buried in the pile is the one that most
    //has to be seen -- it is the one the player is planning around -- and the resting half of the
    //emission follows the occlusion squared, so the figure above is what reaches it there.
    shaded.rgb += BallEmission(primary * hit * through, input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately.
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

technique InstancedModelFrozen
{
    pass P0
    {
        //THE ONE BALL TECHNIQUE BESIDE THE ROCK'S THAT DOES NOT USE PatternVS, and the reason is the
        //whole design of the kind: what says "frozen" is a shape, and a shape is cut in the vertex
        //stage. See FrozenVS.
        VertexShader = compile VS_SHADERMODEL FrozenVS();
        PixelShader = compile PS_SHADERMODEL FrozenPS();
    }
};
