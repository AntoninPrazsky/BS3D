//===================================================================================================
//WOUND WOOL (#311): a ball of yarn - one strand crossing itself over and over, wrapped in bands that
//lie at changing angles, fibrous and soft with no highlight worth the name and a fuzzy halo at the
//silhouette. THE ONLY SOFT MATERIAL among the ball styles, which is the whole of why it exists: the
//vinyl is an air-filled skin, the film a bubble around nothing and the marble a polished stone, and all
//three are HARD. This one changes what a cluster feels like rather than what it is made of.
//
//WHAT MAKES IT READ AS WOOL, in the order the eye picks it up:
//  1. THE WINDING, and specifically that the strand DIRECTION CHANGES ACROSS THE BALL. A single wrap
//     direction is a spool of thread, not a ball of yarn - the thing that says "wound by hand" is that
//     one region runs one way and its neighbour another, with the strands crossing where they meet.
//     Three winding axes, blended by a low-frequency mask that mostly picks one, does exactly that.
//  2. THE SOFTNESS OF THE LIGHT. Fibre scatters, so the terminator is soft and light carries past 90
//     degrees. A wrapped diffuse term over the key light is one expression and is most of the read.
//  3. THE FUZZ at the silhouette - loose fibres lit from behind. This is the Fresnel slot every other
//     style spends on a mirror, spent instead on a halo IN THE BALL'S OWN COLOUR. It is deliberately
//     not a sky reflection: two Fresnel sky terms on one sphere is what bleached the vinyl balls out
//     before ShadePixel took the job over, and a wool ball must not pick the dome up at all.
//  4. NO SPECULAR TO SPEAK OF. Resisting the highlight is most of the work of making this convincing,
//     and it is the one thing that will look wrong first if anyone "improves" it later.
//
//IT IS THE SAFEST STYLE ON THE THIRTEEN COLOURS, and unlike the marble that is not a claim that needed
//measuring - it falls out of the construction. The tint is dyed wool: a full-saturation diffuse body
//with no reflection diluting it, no transmission filtering it, no emission blowing it out and no
//backdrop entering it anywhere. What the map says the ball is, the ball is, under all eighteen domes.
//
//AND ITS FIGURE CANNOT FAIL THE WAY THE MARBLE'S NEARLY DID. The vein was a COLOUR change competing
//against the shading, which is why it went invisible on the bright saturated types and had to be
//rebuilt as a second mineral (see MarblePS's header). The strands are a NORMAL change - they ARE
//shading - so they read at every tint by construction, and on the darkest types too, where a colour
//figure has the least room.
//===================================================================================================

//How many strands are wound across the ball. Read as a wave count over the object-space direction, so
//the diameter shows about a third of this many crossings; under about 12 the strands read as fat tubes
//and over about 40 they cross into a felt that no longer has a strand in it.
float WoolStrandFrequency;

//Peak height of a strand's ridge in world units, exactly as PatternReliefStrength is for the moulding.
//It only tilts the normal, so the silhouette stays the sphere's - a wool ball with a lumpy outline
//would need geometry, and the LOD ladder's coarsest spheres have nothing to spare.
float WoolStrandDepth;

//How brightly the loose fibres at the silhouette catch the light, in the ball's own colour. The one
//figure the C# side states and defends: it is added light over every ball's rim at once, so a cluster
//is where it is judged and never a single ball.
float WoolHalo;

//The three axes the bands are wound around, and the low-frequency directions that choose between them.
//Neither set is aligned to the sphere's own poles or to each other, so the winding never agrees with
//the mesh and the coarsest LODs have nothing to line up with.
static const float3 WoolWindA = float3(0.36, 0.86, -0.36);
static const float3 WoolWindB = float3(-0.79, 0.24, 0.56);
static const float3 WoolWindC = float3(0.48, -0.31, 0.82);
static const float3 WoolPickA = float3(0.83, -0.42, 0.37);
static const float3 WoolPickB = float3(-0.28, 0.71, 0.65);
static const float3 WoolPickC = float3(0.55, 0.63, -0.55);

//How decisively the mask picks ONE winding rather than mixing all three. The whole design is here: at 0
//every region shows all three directions at once, which is a crosshatched net and not a wound ball; far
//too high and the regions get hard edges the yarn has no reason to have. Around 3 leaves broad patches
//running one way with the strands visibly crossing in the seams between them.
static const float WoolWindSelect = 5.0;

//Wave count of that mask. Low, deliberately: it sets how BIG a patch of parallel winding is, and a ball
//of yarn has three or four of them, not thirty.
static const float WoolWindPatchFrequency = 1.1;

//The fibre on top of the strand: two much finer octaves at a fraction of the depth, which is what makes
//the surface read as spun wool rather than as extruded plastic tubing. Their band-limit is ReliefOctave's
//own, so they fade out as the ball shrinks instead of boiling.
static const float WoolFibreFrequency = 47.0;
static const float WoolFibreDepth = 0.22;

//How far light is carried past the terminator (0 = ordinary Lambert). Fibrous materials scatter, so the
//lit side runs round further than a hard surface's and the shadow edge is soft rather than a line. This
//is the single biggest cue after the winding itself.
static const float WoolWrap = 0.35;

//...and how strongly that extra light is added. It is only ever the DIFFERENCE between the wrapped
//response and the Lambert one, so it cannot brighten the surface facing the light - it fills the
//terminator and the far side, which is exactly where a hard ball goes flatly black.
static const float WoolWrapStrength = 0.5;

//How tight the fuzz is at the silhouette. Much broader than a mirror's Fresnel, because loose fibre
//stands out from the surface over a wide band rather than turning at the very edge.
static const float WoolHaloPower = 2.2;

//What a strand's own crevice takes from the ambient. The valleys between wound strands are deep and
//narrow and see very little sky, and without this the ball reads as a smooth sphere with a pattern
//drawn on it however well the normals are tilted.
static const float WoolCrevice = 0.35;

//The specular a fibre surface is allowed: almost none, and rough with it. Wool has no gloss, and every
//attempt to give it "just a little" is what turns it back into plastic.
static const float WoolHighlight = 0.12;
static const float WoolEnvironment = 0.2;
static const float WoolSmoothness = 0.15;

//The wound height field: three wrappings, one of them chosen per region, plus the fibre on top.
//Returns roughly [-1, 1], so the depth uniforms above are peak heights in world units.
float WoolHeight(float3 direction, float footprint)
{
    //Which winding this part of the ball is wrapped in. exp2 of a low-frequency wave is a cheap soft
    //maximum: it is smooth everywhere, never negative, and WoolWindSelect turns the mixing down without
    //ever producing an edge.
    float3 pick = exp2(WoolWindSelect * float3(
        ReliefOctave(direction, WoolPickA, WoolWindPatchFrequency, footprint),
        ReliefOctave(direction, WoolPickB, WoolWindPatchFrequency * 1.31, footprint),
        ReliefOctave(direction, WoolPickC, WoolWindPatchFrequency * 1.73, footprint)));

    pick /= (pick.x + pick.y + pick.z);

    float strands = pick.x * ReliefOctave(direction, WoolWindA, WoolStrandFrequency, footprint)
        + pick.y * ReliefOctave(direction, WoolWindB, WoolStrandFrequency * 1.09, footprint)
        + pick.z * ReliefOctave(direction, WoolWindC, WoolStrandFrequency * 0.94, footprint);

    float fibre = 0.6 * ReliefOctave(direction, WoolPickB, WoolFibreFrequency, footprint)
        + 0.4 * ReliefOctave(direction, WoolPickC, WoolFibreFrequency * 1.63, footprint);

    return strands + fibre * WoolFibreDepth;
}

float4 WoolPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1, first, and branchless for the reason PatternPS gives.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Contract point 6 lives here and costs nothing extra: the winding is evaluated in OBJECT space, so
    //it turns with the ball, and a ball of yarn rolling is about the most legible rotation there is
    //because the strand direction visibly sweeps across the surface.
    float profile = WoolHeight(direction, footprint);
    float3 worldNormal = PerturbNormalFromHeight(normalize(input.WorldNormal), input.WorldPosition, profile * WoolStrandDepth);

    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //Dyed wool: the tint is the body, undiluted. The only thing that touches it is the crevice between
    //strands, which is a depth and not a colour, so no hue is lost at any tint.
    float crevice = 1 - WoolCrevice * saturate(-profile);
    float3 color = primary * crevice;

    //Matte and rough. The specular ambient in particular is nearly off: a wool ball that picks up the
    //dome is a wool ball made of plastic.
    SurfaceSpecular surface;
    surface.Highlight = WoolHighlight;
    surface.Environment = WoolEnvironment;
    surface.Smoothness = WoolSmoothness;

    //The crevice is handed in as the CAVITY too, which is what it physically is - a valley between two
    //strands sees a slice of sky and not the whole hemisphere.
    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(color, 1), 1, crevice, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //Scattering: light carried past the terminator. Only ever the DIFFERENCE between the wrapped
    //response and the Lambert one ShadePixel already applied, so this cannot brighten the side facing
    //the light - it fills the soft band around the shadow edge and the far side, where a hard ball goes
    //flatly black and a fibrous one does not.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float ndl = dot(worldNormal, towardsKey);
    float scattered = saturate((ndl + WoolWrap) / (1 + WoolWrap)) - saturate(ndl);

    shaded.rgb += scattered * WoolWrapStrength * DirLight0DiffuseColor * color * occlusion;

    //The fuzz: loose fibres standing out from the silhouette, in the BALL'S OWN COLOUR and never the
    //sky's. Occluded, so a ball buried in the pile does not put a halo on its neighbours.
    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float fuzz = pow(1 - saturate(dot(worldNormal, eyeVector)), WoolHaloPower);

    shaded.rgb += fuzz * WoolHalo * primary * occlusion;

    //Contract point 2, through BallEmission (#303): the resting glow follows the occlusion, the beat's
    //swing punches through.
    shaded.rgb += BallEmission(primary, input.WorldPosition, occlusion);

    //Contract point 3, in BOTH meanings, and PatternPS's arithmetic deliberately: a landing has to look
    //the same whatever the cluster is made of.
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

technique InstancedModelWool
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL WoolPS();
    }
};
