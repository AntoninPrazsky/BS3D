//---------------------------------------------------------------------------------------------------
// FROZEN (#329)
//
// A coloured ball sealed in a block of ice: a pale, frosted ROUNDED CUBE with the ball's own colour
// glowing out of the middle of it.
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
// Contract point 6 is answered twice over: the block itself turns with the body (a rotating cube is the
// strongest rolling cue in this file), and the frost grain and the internal feather under it are in
// OBJECT space on top of that. The only view-space term is how much colour comes through, which is what
// a solid seen through scattering ice actually does.
//---------------------------------------------------------------------------------------------------

//How square the block is: the exponent of the superellipsoid |x|^p + |y|^p + |z|^p the sphere is cut
//back to. 2 would be the sphere again and infinity a hard cube; this is an ice cube out of a tray,
//with edges rounded enough to catch a highlight along their length.
static const float FrozenCubePower = 4.5;

//The pale ice the block is made of. Cold and light, but well under white: what is white on this ball is
//the frost and the edges, and a body already at white leaves them nothing to be.
static const float3 FrozenIceTint = float3(0.62, 0.74, 0.84);

//How much of the ball's own colour reaches the eye through the ice, looking straight into a face, and
//how fast that falls off towards the silhouette. The falloff is what makes the block read as SOLID --
//a thin shell would show its colour evenly.
static const float FrozenThrough = 0.85;
static const float FrozenThroughPower = 1.6;

//The frost on the faces: fine grain in object space, its amplitude in world units. Faces only -- the
//edges of a real cube are the first thing to melt, and a polished edge beside a frosted face is most of
//what says "ice" rather than "white plastic".
//
//⚠ BOTH FIGURES ARE CALIBRATED AGAINST THE ICE STYLE'S OWN (IceFrostFrequency / IceFrostDepth), and the
//first cut of them was wrong in both directions at once: frequency 26 and amplitude 0.010, which
//photographed as regular DIAGONAL CORDUROY on every block rather than as grain. The arithmetic says why
//and it is the trap the word "frequency" hides here -- ReliefOctave is evaluated on a UNIT direction, so
//26 is 26 radians across the whole ball, i.e. FOUR cycles, and four cycles at a tenth of a ball radius
//of relief is a set of ridges. The Ice style runs 30-72 at a quarter of the amplitude, and it looks like
//frost because of it. Four waves rather than three for the same reason: three left one of them dominant.
static const float FrozenFrostFrequency = 41.0;
static const float4 FrozenFrostRatio = float4(1.0, 1.43, 0.71, 1.87);
static const float FrozenFrostDepth = 0.0028;

//Where the flat part of a face ends, measured on the largest component of the object-space direction:
//1 at a face centre and 1/sqrt(3) = 0.577 at a corner. Everything under this is the rounded edge.
static const float FrozenBevelStart = 0.84;

//The light along the rounded edges, and how tightly it sits on them. This is the figure that survives
//to play distance after the frost has gone sub-pixel: an edge is a whole edge long.
static const float FrozenEdgeGlow = 0.55;
static const float FrozenEdgePower = 2.0;

//The feather of trapped air frozen into the middle of the block -- the white cloud every ice cube from
//a tray has at its core. Object space, so it turns with the block, and low frequency so it is a shape
//rather than a texture.
//
//⚠ THREE OCTAVES AND NOT ONE, and the single wave it replaces was photographed before it was believed: a
//rectified plane wave is a set of PARALLEL BANDS, so every block came out ribbed like corduroy, in the
//same direction on all thirteen. It is the moulded vinyl's lesson and the stone's arriving a third time
//-- one wave is a stripe, and a field needs several running different ways at frequencies sharing no
//common factor. The amplitudes sum to one so FrozenFeatherStrength stays the peak.
static const float FrozenFeatherFrequency = 3.4;
static const float FrozenFeatherStrength = 0.42;

//A frosted surface is a mirror seen through scattering: present highlight, softly picked-up
//environment. Sharper than the Ice style's, because a flat face IS a mirror where a frosted sphere
//never has two pixels facing the same way.
static const float FrozenHighlight = 0.62;
static const float FrozenEnvironment = 0.44;
static const float FrozenSmoothness = 0.74;

//The cold rim at the silhouette: a long path through scattering ice. Weaker than the Ice style's, since
//this ball has hard edges doing that job already.
static const float FrozenRim = 0.30;
static const float FrozenRimPower = 3.0;

//<inheritdoc cref="FrozenFeatherFrequency"/> -- the cloud of air at the block's core, as a field rather
//than a wave. Rectified so it is lumps and not ripples, which is StoneLumps' own construction.
float FrozenFeather(float3 direction, float footprint)
{
    return 0.45 * abs(ReliefOctave(direction, float3(0.31, 0.62, 0.72), FrozenFeatherFrequency, footprint))
        + 0.33 * abs(ReliefOctave(direction, float3(-0.77, 0.24, 0.59), FrozenFeatherFrequency * 1.61, footprint))
        + 0.22 * abs(ReliefOctave(direction, float3(0.58, -0.79, 0.19), FrozenFeatherFrequency * 2.63, footprint));
}

//The ball's own vertex shader: PatternVS with the block cut out of the sphere. Everything about the
//output that is not the shape is PatternVS's, deliberately -- StoneVS's own rule, and for its reason:
//every contract point the pixel shader answers is read off these same fields.
PatternVertexShaderOutput FrozenVS(VertexShaderInput input, InstanceInput instance)
{
    PatternVertexShaderOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

    float4 bonePosition = mul(input.Position, Bone);

    float radius = max(length(bonePosition.xyz), 1e-5);
    float3 direction = bonePosition.xyz / radius;

    //Floored rather than taken raw, so pow() is never handed a zero base on a vertex sitting exactly on
    //an axis. The floor is far below anything the shape can see: 1e-5 raised to this power is zero to
    //every figure below.
    float3 axis = max(abs(direction), 1e-5);

    //THE CUT. r(d) = (|dx|^p + |dy|^p + |dz|^p)^(-1/p) is the superellipsoid through the unit sphere's
    //six axis points; dividing by its value at a CORNER (3^(1/2 - 1/p), the largest it ever takes) puts
    //the corners on the sphere and everything else inside it. So the carve is inward-only, exactly as
    //the rock's is, and for the same reason: a drawn ball that left its cell would overlap a neighbour
    //the simulation says it is not touching. Both pow()s on literals are folded by the compiler.
    float sum = pow(axis.x, FrozenCubePower) + pow(axis.y, FrozenCubePower) + pow(axis.z, FrozenCubePower);
    float carve = pow(sum, -1.0 / FrozenCubePower) / pow(3.0, 0.5 - 1.0 / FrozenCubePower);

    //AND THE NORMAL IS EXACT, which is what a solid of revolution buys over the rock's noise field. Work
    //the rock's own n = d - tangential(grad r)/r through for this r and the two terms cancel outright:
    //grad(r)/r is -g/S with g = |d|^(p-1) sign(d), and g.d = S, so the tangential part is g/S - d and
    //the normal falls out as g itself. No chain-rule factor, no slope, no near-pole guard -- which is
    //also why the flat faces come out FLAT rather than very slightly domed.
    float3 objectNormal = normalize(pow(axis, FrozenCubePower - 1.0) * sign(direction));

    float3 cut = direction * (radius * carve);
    float4 worldPosition = mul(float4(cut, 1), world);

    output.ObjectPosition = cut;
    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(objectNormal, world);
    output.OcclusionData = instance.Custom;
    output.Dissolve = instance.Dissolve;
    output.Ripple = instance.Ripple;

    return output;
}

float4 FrozenPS(PatternVertexShaderOutput input) : COLOR
{
    //Contract point 1, first and branchless, for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float radius = max(length(input.ObjectPosition), 1e-5);

    //The SPHERE's direction, not the cut surface's normal: the carve only scales along d, so this is the
    //same parameter the vertex shader shaped the block with and the figures below line up with the faces
    //instead of drifting across them.
    float3 direction = input.ObjectPosition / radius;
    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Where on the block this pixel is: 1 in the middle of a face, falling through the rounded edge to
    //0.577 at a corner. Object space (contract point 6) -- the faces turn with the ball.
    float3 axis = abs(direction);
    float top = max(axis.x, max(axis.y, axis.z));
    float face = smoothstep(FrozenBevelStart - 0.10, FrozenBevelStart + 0.06, top);
    float edge = 1.0 - face;

    //The frost, on the faces alone. Summed octaves rather than multiplied, on the moulded vinyl's own
    //lesson: multiplying sines lays down a regular crosshatch. The band limit is each octave's own, so
    //the grain leaves silently as a block goes small instead of aliasing into a moire.
    float frost = (ReliefOctave(direction, float3(0.71, 0.52, -0.47), FrozenFrostFrequency * FrozenFrostRatio.x, footprint)
        + ReliefOctave(direction, float3(-0.36, 0.83, 0.42), FrozenFrostFrequency * FrozenFrostRatio.y, footprint)
        + ReliefOctave(direction, float3(0.55, -0.44, 0.71), FrozenFrostFrequency * FrozenFrostRatio.z, footprint)
        + ReliefOctave(direction, float3(-0.82, -0.31, 0.48), FrozenFrostFrequency * FrozenFrostRatio.w, footprint))
        * (FrozenFrostDepth * 0.25) * face;

    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, frost);

    //The feather of air at the core, in OBJECT space. A field of three rectified octaves so it is a cloud
    //rather than a set of bands (see FrozenFeatherFrequency), and band-limited by the octaves themselves
    //so a distant block is plain ice instead of boiling.
    float feather = saturate(FrozenFeather(direction, footprint) * FrozenFeatherStrength);

    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 ice = SrgbToLinear(FrozenIceTint);

    //HOW MUCH COLOUR COMES THROUGH. Straight into a face the eye looks down the thickest part of the
    //block and sees the ball; towards the silhouette the path through scattering ice is long and what
    //comes back is the ice itself. This is the one view-space term in the technique and it is the one a
    //solid seen through ice actually has -- the shape and every figure on it are object space.
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float through = FrozenThrough * pow(saturate(dot(worldNormal, eyeVector)), FrozenThroughPower);

    //The air in the middle hides what is behind it, which is what makes it read as being INSIDE the
    //block rather than painted on the outside of it.
    float3 body = lerp(ice, primary, through * (1.0 - feather));

    SurfaceSpecular surface;
    surface.Highlight = FrozenHighlight;
    surface.Environment = FrozenEnvironment;
    surface.Smoothness = FrozenSmoothness;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(body, 1), 1, 1, surface);

    //Contract point 4.
    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //THE EDGES, and this is the figure that carries the kind at play distance: an edge is a whole edge
    //long, so it is still there when the frost and the feather have gone sub-pixel. Cold and part white
    //for the Ice style's reason -- an edge of a navy block has to differ from the block next to it, and
    //nothing scaled by that tint can.
    shaded.rgb += pow(edge, FrozenEdgePower) * FrozenEdgeGlow * IceCold * lerp(primary, 1.0, 0.65) * occlusion;

    //The cold rim under it: a long path through scattering ice at the silhouette. Occluded, so a block
    //buried in the pile does not outline itself.
    float rim = pow(1 - saturate(dot(worldNormal, eyeVector)), FrozenRimPower);
    shaded.rgb += rim * FrozenRim * IceCold * occlusion;

    //Contract point 2, through BallEmission (#303), and IN THE BALL'S OWN COLOUR carried by `through`:
    //what is glowing is the ball inside the ice, not the ice. A block buried in the pile is the one that
    //most has to be seen -- it is the one the player is planning around -- and the resting half of the
    //emission follows the occlusion squared, so the figure above is what reaches it there.
    shaded.rgb += BallEmission(primary * through, input.WorldPosition, occlusion);

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
