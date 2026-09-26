//===================================================================================================
//CUT GEM (#308): a brilliant-cut stone. Flat faces that each catch the light separately, hard glints
//where a face turns through the highlight, deep saturated colour in the body.
//
//THE FACETS ARE SHADED, NEVER BUILT, AND THAT IS #271'S RULING RATHER THAN A PREFERENCE. #231 cut the
//crystal trophy's mesh to 24 segments with flat per-facet normals and defended it as reading like
//brues; the owner's word was that the cup is HRANATY and should be PLYNULY BEZ OSTRYCH HRAN, and that
//"the intent of 'crystal sharp' was never to see the sharp edges". At a coarse segment count the
//OUTLINE itself goes polygonal, and NO SHADING FIXES A FACETED RIM. #271 named two ways out and this
//takes the second: keep the smooth surface and put the sharpness entirely into the shading.
//
//So SphereMesh is untouched - the same LOD ladder, the same instances, the same silhouette, no new
//geometry anywhere - and the faceting is a HEIGHT FIELD. That choice is not cosmetic either:
//
//  A pixel shader here CANNOT ROTATE A VECTOR FROM OBJECT SPACE INTO WORLD SPACE. The instance streams
//  carry no tangents and the object-to-world rotation never reaches this stage (PerturbNormalFromHeight
//  exists for exactly that reason). So "snap the object-space normal to the nearest facet and use it"
//  cannot be written: the snapped vector has no way home. What CAN be written is a SCALAR that depends
//  on the object-space direction, handed to PerturbNormalFromHeight, which takes its gradient in SCREEN
//  space and therefore does the object-to-world mapping for free, exactly as the vinyl's moulding and
//  the wool's winding already do.
//
//The scalar is the distance from the ball's surface to the plane of the facet it belongs to. Inside one
//facet that is a smooth function whose gradient points along the facet's own normal, so tilting by it
//drives the shading normal towards that facet - constant over the face, stepping at the boundary, with
//the silhouette left exactly circular.
//
//It is also a figure in the NORMAL rather than in the colour, which is what #305 and #311 established
//between them (see WoolPS's header), and on a sphere it is geometrically honest: a cut sphere really
//would have flat faces at those normals. The trophy's problem was that its profile was not a sphere.
//===================================================================================================

//How finely the object-space direction is quantized, and so how many faces the stone is cut into. Each
//step up adds a shell of lattice directions; 2 is a chunky brilliant, 4 is close to a disco ball.
float GemFacetCount;

//How hard the height field drives the shading normal towards its facet's own. Under about 0.5 the faces
//only bend the light and the stone reads as dimpled; over about 2 they are flat to the pixel and the
//edges between them are as hard as this can make them without touching the mesh.
float GemFacetDepth;

//How deeply the body absorbs its own colour along the view. The one figure the C# side states, because
//it is what decides whether the four dark types stay apart from one another - see the note in the
//constant that carries it.
float GemAbsorption;

//The three axes the direction is quantized against. Orthonormal by construction and deliberately not
//aligned to the sphere's own poles, so the cut never agrees with the mesh's seams or with the LOD
//ladder's coarsest rings.
static const float3 GemAxisA = float3(0.8017837, 0.2672612, 0.5345225);
static const float3 GemAxisB = float3(-0.3812464, 0.9174414, 0.1131828);
static const float3 GemAxisC = float3(-0.4605661, -0.2955083, 0.8368858);

//The girdle: how brightly the rim piles light up where a real stone's total internal reflection does,
//and how tightly. Bright and narrow - it is the one thing that says CUT rather than merely shiny.
static const float GemGirdle = 0.9;
static const float GemGirdlePower = 4.0;

//A facet is polished glass, so its highlight is a pinpoint. Tighter even than the marble's, because a
//facet is optically flat where a ground stone surface is merely smooth.
static const float GemGloss = 260.0;
static const float GemGlossStrength = 1.4;

//How much of the environment a facet mirrors, and the floor under the absorbed body so the darkest types
//do not converge on black. THE FLOOR IS NOT A NICETY: absorption tuned for the bright hues takes Type8,
//Type10, Type12 and Type13 to the same near-black, which is four of thirteen lost at once.
static const float GemEnvironment = 1.3;
static const float GemBodyFloor = 0.32;

//How far the body and the girdle are pushed towards their own fully saturated hue (#315). The floor
//above stops the dark types converging on BLACK; it cannot stop them converging on the GREY the
//uncoloured mirror leaves, which is what they measured as doing - black, brown and silver within 4.5 to
//7.3 dE of one another where the vinyl's own worst pair is 7.9. This is the other half of that floor,
//and it costs the saturated stones nothing visible: at constant luminance a red stone is already close
//to its own saturated form, and a neutral one IS it.
static const float GemChromaLift = 1.0;

//How much of the mirror a stone gets, as a share of its own luminance (the compressed map TintEmission
//uses). The mirror is uncoloured, so at full strength on every stone it is simply the SAME light added
//to all thirteen - which is why black and silver arrived 4.5 apart despite one being sixty times darker
//than the other in tint. A dark stone is a light trap; it gives back less of what falls on it.
static const float GemEnvironmentFloor = 0.22;

//Where the facets stop being drawn honestly. Quantized normals put HARD EDGES IN SCREEN SPACE where no
//geometric edge exists, and hard shading edges alias; once a pixel spans a facet there is nothing to
//resolve, so the faceting is faded back to the smooth sphere rather than left to boil. Measured against
//the pixel's reach across the BALL, which is what a facet's size is expressed in - and note #258's trap
//here, which is the same shape: its first band-limit measured the wrong quantity and the whole effect
//existed only in the arithmetic.
static const float GemFacetFadeStart = 0.05;
static const float GemFacetFadeEnd = 0.16;

float4 GemPS(PatternVertexShaderOutput input) : COLOR
{
    float radius = max(length(input.ObjectPosition), 1e-5);
    float3 direction = input.ObjectPosition / radius;

    //Contract point 1.
    float dissolveNoise = DissolveNoise(floor(input.Position.xy / DissolvePixelSize));
    clip(input.Dissolve >= 0 ? dissolveNoise - input.Dissolve : -input.Dissolve - dissolveNoise);

    float footprint = (length(ddx(input.WorldPosition)) + length(ddy(input.WorldPosition))) / radius;

    //Which face this pixel is on: the direction quantized in the gem's own basis (contract point 6 - the
    //cut is object space, so the facets turn with the ball and a rolling stone flashes face after face
    //through the highlight, which IS the style).
    float3 local = float3(dot(direction, GemAxisA), dot(direction, GemAxisB), dot(direction, GemAxisC));
    float3 facet = normalize(round(local * GemFacetCount) / max(GemFacetCount, 1e-3));

    //...and the scalar that carries it into world space for free: how far this point of the surface lies
    //from the plane of its own facet. Smooth within a face, stepping between faces, and its gradient
    //points along the facet normal - see the header for why this cannot be done as a vector.
    float faceted = 1 - smoothstep(GemFacetFadeStart, GemFacetFadeEnd, footprint);
    float height = (dot(local, facet) - 1) * GemFacetDepth * faceted;

    float3 smoothNormal = normalize(input.WorldNormal);
    float3 worldNormal = PerturbNormalFromHeight(smoothNormal, input.WorldPosition, height);

    float3 eyeVector = normalize(EyePosition - input.WorldPosition);
    float3 primary = SrgbToLinear(PatternPrimaryColor);

    //The stone's own colour, saturated at constant luminance (#315). Both the body and the girdle are
    //built off this rather than off the raw tint: the mirror below is uncoloured and swamps whatever
    //chroma a dark tint has, so what little it has must count for more. See SaturateTint - a neutral tint
    //is unmoved by construction, which is what keeps black where it is while brown moves off it.
    float3 stone = SaturateTint(primary, GemChromaLift);

    //The body: absorbed along the view, so the stone is deepest where it is thickest and lightens towards
    //the rim where the path through it is short. Floored so the dark types keep their hue instead of all
    //arriving at black together.
    float thickness = saturate(dot(smoothNormal, eyeVector));
    float3 body = max(stone * exp(-GemAbsorption * thickness), stone * GemBodyFloor);

    //A facet is polished glass: barely any broad highlight, a strong mirror, and full smoothness so
    //Fresnel still runs to a mirror at the edge. The mirror is scaled by the stone's own luminance
    //(#315): uncoloured light added equally to all thirteen is light that tells none of them apart, and
    //it was most of why the darkest four measured as one grey.
    SurfaceSpecular surface;
    surface.Highlight = 0.25;
    surface.Environment = GemEnvironment
        * lerp(GemEnvironmentFloor, 1.0, sqrt(saturate(dot(primary, float3(0.2126, 0.7152, 0.0722)))));
    surface.Smoothness = 1;

    float4 shaded = ShadePixel(input.WorldPosition, worldNormal, input.OcclusionData, float4(body, 1), 1, 1, surface);

    float occlusion = SurfaceOcclusion(input.WorldPosition, worldNormal, input.OcclusionData);

    //The glint: a pinpoint off the key light, on the FACET's normal, so it snaps from face to face as the
    //stone turns rather than sliding over it. That snapping is the whole read of a cut stone in motion.
    float3 towardsKey = normalize(KeyLightPosition - input.WorldPosition);
    float3 halfway = normalize(towardsKey + eyeVector);

    shaded.rgb += DirLight0SpecularColor * GemGlossStrength
        * pow(saturate(dot(worldNormal, halfway)), GemGloss) * occlusion;

    //The girdle, where a real stone's total internal reflection piles light up along the rim.
    float girdle = pow(1 - thickness, GemGirdlePower);

    shaded.rgb += girdle * GemGirdle * stone * occlusion;

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

technique InstancedModelGem
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PatternVS();
        PixelShader = compile PS_SHADERMODEL GemPS();
    }
};
