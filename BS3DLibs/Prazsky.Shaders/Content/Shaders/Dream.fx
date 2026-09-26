//Draws the dream: a hallucinatory skyscape - slow marbled colour flowing across the whole sphere of the
//sky, hard glassy solids that tumble, morph and melt into one another, soft luminous orbs breathing in and
//out of the murk, and fast sparks whipping between them. The tenth scene, and deliberately a scene of
//CONTRASTS: sharp against blurred (raymarched surfaces with crisp silhouettes against pure gaussian glows),
//fast against slow (the background marbling drifts over minutes while the sparks cross the sky in seconds),
//near against far. It is the picture of a hallucination the way Space.fx is the picture of space.
//
//Like Space it replaces the SKY, and everything structural follows from that: one full-screen pass over a
//quad already in normalized device coordinates, the view ray recovered per pixel through
//ViewRayBasis (SkyRay.Basis), drawn with the depth state off so the island, the cluster and the gun draw over it.
//The caller draws no dome and no cloud deck, suppresses the cloud shadow on the instanced effect, and takes
//the scene's own light rig (DreamLightingConfig) instead of a dome's.
//
//Everything here is ANALYTIC 3D - fields of sines on the view direction, ray-sphere tests, closest-approach
//glows - and never a 2D chart of the sphere, so there are no pole seams and nothing to hide. The floating
//solids are the one raymarched element, and the march is GATED: each shape carries an analytic bounding
//sphere, the ray is tested against those first (six quadratics), and only a shape whose bound the ray
//actually crosses is marched, inside its own [t0, t1] interval. Most pixels march nothing.
//
//Levels against the glare (GLARE_THRESHOLD 0.55 on luminance): the background marbling stays well under it
//- it is the CANVAS, and a canvas that blooms buries everything hung on it, which is exactly what the first
//build did at a higher brightness - the orbs are allowed over it deliberately (smooth areas hundreds of
//pixels wide, the planet's lit-limb reasoning, so they bloom steadily), while the SPARKS, which are small,
//stay at the threshold's edge and read fast through their trails rather than through bloom (a small point
//over the threshold is sampled stochastically by the glare's sparse grid and flickers, which reads as a fault).
//
//Built by all three executables out of this directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

//How many of each element the sky carries. Fixed at compile time (the loops unroll); the config dials
//scale their look, not their count.
#define SHAPE_COUNT 8
#define ORB_COUNT 7
#define SPARK_COUNT 14

//What the reduced program carries instead - see DreamScene.
#define SPARK_COUNT_REDUCED 4

float4x4 ViewRayBasis;  //the lens's axes over the projection's slopes - see SkyRay.Basis
float3 CameraPosition;
float DreamTime;

//--- The palette ---------------------------------------------------------------------------------------
//A cosine palette: colour(t) = A + B * cos(2pi * (C * t + D)). Four vectors are the scene's whole colour
//identity - every element below indexes the same ramp at a different phase, which is what keeps a frame
//full of saturated colour reading as ONE hallucination rather than as a box of crayons.
float3 PaletteA;
float3 PaletteB;
float3 PaletteC;
float3 PaletteD;

//--- The background marbling -----------------------------------------------------------------------------
float SwirlScale;          //bands per unit of direction - how fine the marbling is
float SwirlWarp;           //how far the field bends its own sampling direction; 0 is straight bands
float SwirlSpeedSlow;      //the broad marbling's drift (slow - it should read over minutes)
float SwirlSpeedFast;      //the sharp ribbons' travel (fast - they cross in seconds)
float RibbonSharpness;     //exponent on the fine layer; higher is thinner, sharper ribbons
float BackgroundBrightness;

//--- The floating solids ---------------------------------------------------------------------------------
float ShapeOrbitRadius;    //how far out the solids roam; they must stay outside the play space (see C#)
float ShapeSize;           //base half-size of a solid, world units
float ShapeMorphSpeed;     //how fast a solid melts between its forms
float ShapeEmission;       //how much of the palette glows from inside a solid
float ShapeReflection;     //how much of the marbled sky a solid mirrors
float ShapeAbsorption;     //how deeply a solid's body stains what is seen through it (Beer, #506)

//--- The soft orbs and the sparks ------------------------------------------------------------------------
float OrbRadius;           //gaussian sigma of a soft orb, world units
float OrbBrightness;       //peak linear radiance of an orb's core (allowed over the glare threshold)
float SparkBrightness;     //peak linear radiance of a spark's head (kept AT the threshold, see header)
float SparkSpeed;

//The empty sky between everything else - not black: a dream has no void, only deeper colour.
float3 DeepColor;

static const float PI = 3.14159265;

float3 Palette(float t)
{
    return PaletteA + PaletteB * cos(2.0 * PI * (PaletteC * t + PaletteD));
}

//--- The marbling ----------------------------------------------------------------------------------------
//TWICE-warped fractal noise on the 3D view direction. The first build was a sum of plane-wave sines under
//a sine warp, and it read as exactly what it was - the plasma effect of a 1994 demo, smooth blobs sliding
//over each other. Plane waves keep their planes however many are summed; fractal noise has no planes to
//keep, and feeding one fBm's output into another's DOMAIN (twice) is what produces the filaments, eddies
//and mixing that real turbulence has. The time rides inside the domains, so the fluid itself evolves
//rather than a fixed pattern scrolling.
//`warpOctaves` is 3 on the authored scene and 2 on the reduced program. It is the ONE reduction in this file
//that pays on its own - measured 4.40 -> 4.08 ms - and the reason is that it is the only one touching EVERY
//pixel rather than only the pixels a solid was hit on. See DreamScene for the rest of that sweep.
float3 Background(float3 d, int warpOctaves)
{
    float t = DreamTime;
    float3 p = d * SwirlScale;

    //The first warp: three offset fBms driving the domain apart - slow, the broad circulation.
    float3 q;
    q.x = Fbm3(p + float3(0.0, 1.7, t * SwirlSpeedSlow), warpOctaves);
    q.y = Fbm3(p + float3(5.2, 8.3, -t * SwirlSpeedSlow * 0.8), warpOctaves);
    q.z = Fbm3(p + float3(9.1, 2.8, t * SwirlSpeedSlow * 0.6), warpOctaves);

    //The second warp, off the already-warped domain - the layer that turns smooth drift into mixing.
    float3 warped = p + SwirlWarp * q;
    float3 r;
    r.x = Fbm3(warped * 1.3 + float3(1.7, 9.2, t * SwirlSpeedSlow * 0.5), warpOctaves);
    r.y = Fbm3(warped * 1.3 + float3(8.3, 2.8, -t * SwirlSpeedSlow * 0.4), warpOctaves);
    r.z = Fbm3(warped * 1.3 + float3(4.1, 6.9, t * SwirlSpeedSlow * 0.7), warpOctaves);

    float field = Fbm3(p + SwirlWarp * r, 4);

    //Colour: the palette rides the field, and the WARP INTERMEDIATES shade it - where the domain was
    //dragged furthest the fluid "mixed", so q picks the second palette phase and r darkens the folds.
    //Colouring by the intermediates rather than the final value alone is most of why warped fBm reads as
    //a substance with an inside instead of as a flat pattern.
    float3 color = Palette(field * 0.55 + t * 0.004);
    color = lerp(color, Palette(field * 0.55 + 0.38 + t * 0.004) * 1.35, saturate(dot(q, q) * 2.5));

    //THE MEDIUM (#506). Every reference this scene was redrawn against - ink dropped into water, a lava
    //lamp, luminous orbs in a dark room, glass on a dark ground - is mostly DARK, with the colour gathered
    //into plumes and filaments standing out of that dark. This marbling was the opposite: it covered the
    //frame at ONE value, and everything hung on it - the orbs, the sparks, the solids, the cluster itself -
    //had nothing to stand against.
    //
    //The reason is the palette, and it cannot be fixed there: a cosine ramp whose three channels sit a third
    //of a cycle apart is a hue wheel at CONSTANT lightness (mean A per channel, 0.42, whatever t is), so
    //rotating the hue can never darken it and lowering the brightness only greys the whole frame down
    //together - which is what the 0.32 -> 0.24 step recorded below did. The darkness has to be a DENSITY
    //over the palette, and the warp intermediates already say where the fluid gathered and where it thinned.
    //Squared, so most of the sphere is near-empty and the plumes are what carry the colour.
    float density = saturate(length(r) * 2.4 - 0.18);
    density *= density;
    color *= 0.10 + 1.6 * density;

    //The ribbons: ridged fBm racing through - thin sharp filament networks, the fast half of the sky,
    //in a palette phase far from the ground they cross.
    //The ribbons: ridged fBm racing through - thin sharp filament networks, the fast half of the sky, in a
    //palette phase far from the ground they cross. They carry the LIGHT of this sky since #506 rather than
    //a filigree over it: against a medium that reaches zero, the strands are what the eye reads, the way
    //the lit threads of ink are what one reads in the water.
    //
    //⚠ The threshold is what makes them STRANDS, and it has to be set against the field's real distribution
    //rather than a guessed one. A three-octave RidgedFbm3 is not centred: its octaves are each `(1-|n|)^2`,
    //which is near 1 for the small |n| that dominates a gradient field, so it runs about 0.63 TYPICAL
    //against a 0.875 maximum - a narrow band high up, not an 0..1 spread. Thresholding at 0.30 (which is
    //below almost every pixel) turned the layer into a continuous pale-green wash covering the whole sphere
    //with the medium showing through it as holes - mould rather than ink. 0.58 with the gain that maps the
    //rest of the band onto 0..1 leaves the typical pixel dark and lights only the crests.
    //
    //Mostly IN the medium, too: a thread of ink is lit where the ink is, and a ribbon crossing empty sky
    //was the other half of what filled the voids. A quarter of it is allowed out there, because the
    //references do have wisps reaching into clear water.
    //
    //The core stays under the glare threshold (0.68 palette x 1.4 x Brightness): the canvas must not bloom,
    //only what hangs on it.
    float ribbon = RidgedFbm3(d * SwirlScale * 2.2 + float3(0.0, 0.0, t * SwirlSpeedFast), 3);
    ribbon = pow(saturate((ribbon - 0.58) * 3.4), RibbonSharpness * 0.45);
    color += Palette(field * 0.3 + 0.61) * (ribbon * 1.4 * (0.25 + 0.75 * density));

    return DeepColor + color * BackgroundBrightness;
}

//--- The floating solids ---------------------------------------------------------------------------------
//Where solid i stands now: a slow independent orbit, each at its own radius, height swing and rate, so the
//constellation never repeats and two solids occasionally drift close enough to melt together.
float3 ShapeCenter(float i, float t)
{
    float a = t * (0.020 + 0.011 * frac(i * 0.371)) + i * 2.399;
    float r = ShapeOrbitRadius * (0.78 + 0.22 * sin(i * 5.3));
    float y = 26.0 + 46.0 * sin(t * 0.013 + i * 2.7);

    return float3(cos(a) * r, y, sin(a) * r);
}

//One solid's distance field, in its own tumbling frame: a sphere, a rounded box and a torus, melted into
//one another on a slow cycle. The morph is the point - a shape that is never quite any one thing is what
//"the shapes keep changing" means, and the smooth lerp of distance fields is what makes the change a MELT
//rather than a swap.
float ShapeSdf(float3 p, float i, out float sizeOut)
{
    float t = DreamTime;
    float3 q = p - ShapeCenter(i, t);

    //A slow two-axis tumble. Rotations only - the SDF stays exact under them.
    float ya = t * (0.11 + 0.05 * frac(i * 0.73)) + i;
    float ca = cos(ya), sa = sin(ya);
    q.xz = float2(q.x * ca - q.z * sa, q.x * sa + q.z * ca);
    float xa = t * 0.07 + i * 1.7;
    float cb = cos(xa), sb = sin(xa);
    q.yz = float2(q.y * cb - q.z * sb, q.y * sb + q.z * cb);

    float size = ShapeSize * (0.7 + 0.3 * sin(i * 9.1));
    sizeOut = size;

    float m = 0.5 + 0.5 * sin(t * ShapeMorphSpeed + i * 11.3);
    float blend = smoothstep(0.12, 0.88, m);

    float dSphere = length(q) - size;

    float3 b = abs(q) - size * 0.72;
    float dBox = length(max(b, 0.0)) + min(max(b.x, max(b.y, b.z)), 0.0) - size * 0.14;

    float2 tor = float2(length(q.xz) - size * 0.78, q.y);
    float dTorus = length(tor) - size * 0.30;

    //Which pair this solid melts between is its own: half cycle sphere<->box, half box<->torus.
    return frac(i * 0.381) < 0.5 ? lerp(dSphere, dBox, blend) : lerp(dBox, dTorus, blend);
}

float3 ShapeNormal(float3 p, float i)
{
    float s;
    const float e = 0.25;
    float2 k = float2(1.0, -1.0);

    //The tetrahedral four-tap gradient - four SDF evaluations instead of six.
    return normalize(
        k.xyy * ShapeSdf(p + k.xyy * e, i, s) +
        k.yyx * ShapeSdf(p + k.yyx * e, i, s) +
        k.yxy * ShapeSdf(p + k.yxy * e, i, s) +
        k.xxy * ShapeSdf(p + k.xxy * e, i, s));
}

//The glow of a point along the ray, from the ray's closest approach to it - a pure gaussian, no march. This
//is the whole of an orb and the whole of a spark: the analytic soft half of the scene's sharp/soft contrast.
float RayGlow(float3 origin, float3 direction, float3 center, float sigma)
{
    float3 toCenter = center - origin;
    float along = max(dot(toCenter, direction), 0.0);
    float3 nearest = toCenter - direction * along;
    float d2 = dot(nearest, nearest);

    return exp(-d2 / (2.0 * sigma * sigma));
}

struct DreamVertexInput
{
    float4 Position : POSITION0;
};

struct DreamVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
};

DreamVertexOutput DreamVS(DreamVertexInput input)
{
    DreamVertexOutput output;
    output.Position = float4(input.Position.xy, 0.0, 1.0);

    //The corner unprojected to the far plane; the pixel shader normalizes the interpolated ray.
    output.Ray = mul(float4(input.Position.xy, 1.0, 0.0), ViewRayBasis).xyz;

    return output;
}

//`detail` is an ordinary argument passed a LITERAL by each entry point below, so it constant-folds and each
//comes out a separate program with its own register allocation - the forest's and the cavern's mechanism,
//and for the same measured reason. Front end, 1600x900, desktop GPU, dome 13, nocap, against 4.40 ms:
//
//  Background() dropped from the reflection   4.42 ms   (nothing)
//  sparks 14 -> 4                             4.47      (nothing)
//  spark trail 3 -> 1 sample                  4.43      (nothing)
//  warp octaves 3 -> 2                        4.08      (0.32 - the one single that pays)
//  reflection + sparks                        3.62
//  reflection + trail                         3.72
//  all four                                   3.15
//
//The pass is occupancy-bound like the other two, so single reductions are worth nothing - except the warp
//octaves, which are the only thing here evaluated on every pixel rather than only where a solid was hit.
//The reduced program therefore takes all four.
float4 DreamScene(DreamVertexOutput input, bool detail)
{
    float3 direction = normalize(input.Ray);
    float t = DreamTime;

    float3 color = Background(direction, detail ? 3 : 2);

    //--- The soft orbs: huge, slow, blurred - luminous presences breathing through the marbling. Each takes
    //its own palette phase and swells on its own cycle, so at any moment some are waxing while others fade.
    [unroll]
    for (int o = 0; o < ORB_COUNT; o++)
    {
        float fo = (float)o;
        float3 center = float3(
            cos(t * 0.009 + fo * 2.1) * ShapeOrbitRadius * 1.25,
            15.0 + 60.0 * sin(t * 0.007 + fo * 3.3),
            sin(t * 0.011 + fo * 1.3) * ShapeOrbitRadius * 1.25);

        float breath = 0.35 + 0.65 * (0.5 + 0.5 * sin(t * 0.05 + fo * 2.6));
        float glow = RayGlow(CameraPosition, direction, center, OrbRadius * (0.7 + 0.3 * sin(fo * 7.0)));

        color += Palette(fo * 0.21 + t * 0.006) * (glow * OrbBrightness * breath);
    }

    //--- The sparks: small, fast, sharp - they cross between the orbs in seconds. Three samples down each
    //spark's own recent path make the head a comet: the trail is what reads as speed at any frame rate, the
    //fireworks' lesson.
    [unroll]
    for (int s = 0; s < (detail ? SPARK_COUNT : SPARK_COUNT_REDUCED); s++)
    {
        float fs = (float)s;
        float rate = SparkSpeed * (0.7 + 0.6 * frac(fs * 0.617));

        [unroll]
        for (int trail = 0; trail < (detail ? 3 : 1); trail++)
        {
            float tt = t - (float)trail * 0.06;
            float3 center = float3(
                sin(tt * rate + fs * 9.7) * 130.0,
                30.0 + 55.0 * sin(tt * rate * 0.7 + fs * 5.1),
                cos(tt * rate * 1.3 + fs * 3.9) * 130.0);

            float amp = SparkBrightness * (trail == 0 ? 1.0 : (trail == 1 ? 0.4 : 0.16));
            color += Palette(fs * 0.13 + 0.37) * (RayGlow(CameraPosition, direction, center, 2.2) * amp);
        }
    }

    //--- The solids: the sharp half of the scene. Each carries an analytic bounding sphere; the ray is
    //tested against those first, and only a crossed bound is marched, inside its own interval. The bounds
    //are generous (the smooth morph never leaves them) and the branch is coherent across a shape's screen
    //area, which is what makes it worth having.
    float bestT = 1e9;
    float bestShape = -1.0;

    [unroll]
    for (int i = 0; i < SHAPE_COUNT; i++)
    {
        float fi = (float)i;
        float3 center = ShapeCenter(fi, t);
        float bound = ShapeSize * 1.9;

        float3 oc = CameraPosition - center;
        float b = dot(oc, direction);
        float c = dot(oc, oc) - bound * bound;
        float disc = b * b - c;

        [branch]
        if (disc > 0.0)
        {
            float t0 = max(-b - sqrt(disc), 0.0);
            float t1 = -b + sqrt(disc);

            //Sphere-trace just this shape inside [t0, t1]. The interval is a few shape-widths, so the
            //march converges in far fewer steps than a whole-scene trace would.
            float rayT = t0;
            float size;

            [loop]
            for (int march = 0; march < 28; march++)
            {
                float d = ShapeSdf(CameraPosition + direction * rayT, fi, size);
                if (d < 0.02 || rayT > t1) break;
                rayT += d * 0.9;
            }

            if (rayT <= t1 && rayT < bestT)
            {
                bestT = rayT;
                bestShape = fi;
            }
        }
    }

    [branch]
    if (bestShape >= 0.0)
    {
        float3 hit = CameraPosition + direction * bestT;
        float3 normal = ShapeNormal(hit, bestShape);

        //Ambient occlusion off the solid's own field: four probes up the normal, each asking how much
        //less room there is than an open surface would have. It is what darkens the torus's inner ring
        //and the melt seams mid-morph - the CONTACT the first build lacked, whose absence is half of what
        //separates a modern render from a screensaver (nothing in one ever shades anything else).
        float occlusion = 0.0;
        float probe = 1.4;
        float sizeUnused;

        [unroll]
        for (int a = 1; a <= 4; a++)
        {
            float reach = probe * (float)a;
            occlusion += (reach - ShapeSdf(hit + normal * reach, bestShape, sizeUnused)) / reach * pow(0.55, (float)a);
        }

        float ao = saturate(1.0 - 1.3 * occlusion);

        //GLASS, and not painted plastic (#506). Every glass reference is the same three things: a body one
        //sees THROUGH, a narrow bright rim, and hard glints where a light lands on the curvature. These
        //solids had none of them and read as matte pastel plastic hanging in the sky - a 0.55 emission floor
        //over the whole body IS opaque paint, and a cubed fresnel is a broad wash rather than an edge.
        float3 own = Palette(bestShape * 0.17 + t * 0.010);
        float fresnel = pow(1.0 - saturate(dot(normal, -direction)), 4.5);

        //What is behind it, STAINED BY ITS OWN THICKNESS. `color` is this ray's sky as already gathered -
        //the marbling, and any orb or spark along it - so an orb drifting behind a solid now shines through
        //it, which is the lava-lamp reference exactly, and for nothing. No refraction: these are thin-walled
        //dream glass and not lenses, and a second march to bend the ray costs more than the bend resolves at
        //this curvature.
        //
        //The stain is Beer-Lambert over how FACE-ON the surface is, which is the one thickness available
        //without a second march: a ray meeting the body square crosses the most glass, one grazing the
        //silhouette crosses almost none. That single term is what turned these from plastic into glass. The
        //first attempt tinted the transmission by a flat factor, and it came back exactly as flat as the
        //paint it replaced - the sky behind a solid barely changes across the twenty degrees it covers, so a
        //body shaded by the sky alone has no internal gradient at all. Absorbing by thickness gives it the
        //one every photograph of glass has: a deep saturated core, clearing towards the rim.
        float facing = saturate(dot(normal, -direction));
        float3 absorb = exp(-facing * ShapeAbsorption * (1.0 - saturate(own)));
        float3 through = color * absorb * (0.45 + 0.55 * ao);

        //The reduced program mirrors a flat DeepColor rather than re-running the whole background. It is half of
        //the cheapest pair that pays anything at all (see DreamScene), and the solids are rounded and
        //semi-matte - a first-order warp is already past what a reflection at that curvature resolves.
        float3 mirrored = DeepColor;

        if (detail) mirrored = Background(reflect(direction, normal), 3);

        //The reflection answers SUPERLINEARLY, which is where the glints come from: about half the linear
        //reflection in the empty sky and four times it in a ribbon core, so a bright ribbon landing on a
        //curved face reads as a hard highlight rather than as a smear. That is what a glint in a studio
        //photograph of glass actually is - a light source reflected - and it means this sky needed no light
        //source inventing for it, having no sun. Free: `mirrored` is already in hand.
        float3 specular = mirrored * (0.5 + 4.0 * mirrored) * ShapeReflection * (0.25 + 0.75 * fresnel) * ao;

        float3 shapeColor = through * (1.0 - 0.6 * fresnel)
            + own * ShapeEmission * (0.05 + 0.95 * fresnel) * (0.35 + 0.65 * ao)
            + specular;

        //The far solids sink into the marbling rather than popping against it - a touch of the background
        //over distance, the haze idea with colour instead of grey.
        float fade = saturate(bestT / 600.0);
        color = lerp(shapeColor, color, fade * 0.5);
    }

    return float4(color, 1.0);
}

//Two programs from one body, as the forest floor and the cavern have. "DreamReduced" drops the background's
//second evaluation in the reflection, most of the sparks, two thirds of each spark's trail and one octave
//off both warp layers. The caller picks by tier through SceneRenderer.SceneDetail.
float4 DreamPS(DreamVertexOutput input) : COLOR { return DreamScene(input, true); }
float4 DreamReducedPS(DreamVertexOutput input) : COLOR { return DreamScene(input, false); }

technique Dream
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL DreamVS();
        PixelShader = compile PS_SHADERMODEL DreamPS();
    }
};

technique DreamReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL DreamVS();
        PixelShader = compile PS_SHADERMODEL DreamReducedPS();
    }
};
