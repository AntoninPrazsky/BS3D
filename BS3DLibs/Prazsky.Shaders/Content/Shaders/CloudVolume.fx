//THE CLOUDS AS VOLUMES (#989), High and Ultra. The owner: "On high/ultra we need dramatically better clouds that are
//spatial, dynamic, massive, real, believable ... The clouds must be dense!" The deck (Sky.fx) is a flat field on a plane:
//seen from under it, it is an underside and nothing more, and towards the skyline it has no height at all, so it is
//faded out before it reaches there. Here each heap of cumulus the deck's weather layer holds near the lens is a volume of
//its own: a box round it (CloudVolumes), every pixel of which marches its view ray through the heap's density - a flat
//base on the deck's plane, a rounded body widest a third of the way up, billowed and eroded by a baked 3D noise - lit by
//the sun through the heap itself (a short march towards the sun), by the sky from above and the deck's underside colour
//from below, scattering forward towards the sun. The cost is the clouds' own pixels, not the whole sky's, which is where
//#40's marched slab paid and lost.
//
//The deck stays: it is what the shadows and the light rig read (one field, Clouds.fxh), and past the volumes' reach it is
//the far layer. A volume stands where the deck's own weather says there is cloud, so what shades the ground is still
//what is overhead.

#include "ShaderTarget.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

//The deck's own light (CloudField pushes them to the sky shader; CloudVolumes reads them back): the lit cloud's colour,
//the underside's, and the sun
float3 SunDirection;
float3 CloudSunColor;
float3 CloudShadowColor;
float CloudTime;

//How far the volumes reach: they dissolve into the sky between these distances, inside the camera's far plane
float FadeNear;
float FadeFar;

//The steps a ray takes through a heap, and the two taps towards the sun each lit sample takes
static const int CLOUD_STEPS = 10;
//And a far heap's: a few pixels tall past STEPS_FAR_FROM, it has no detail ten steps could find, and the band over the
//skyline is where the heaps stand thickest on the screen
static const int CLOUD_STEPS_FAR = 5;
static const float STEPS_FAR_FROM = 1500.0, STEPS_FAR_AT = 3000.0;
//How much light a heap takes away per world unit at full density: a heap a hundred units across is all but solid
static const float CLOUD_SIGMA = 0.09;
//How bright the sunlit side is against the deck's lit colour, and how much of the sky's light reaches the shaded side
static const float CLOUD_SUN_GAIN = 1.25;
static const float CLOUD_SKY_GAIN = 0.55;
//The shape: where the heap is widest (a share of its height) and how wide its base is against that
static const float CLOUD_WAIST = 0.3;
static const float CLOUD_BASE_WIDTH = 0.78;
//The billows' size: CLOUD_BILLOW of the heap's size (the mean of its radius and its height) on a heap of BILLOW_RADIUS,
//growing slower than the heap (#1145), so a big heap carries more of them and climbs in turrets, and a small one is a
//lump or two, not the same bun at another size
static const float CLOUD_BILLOW = 0.45;
static const float BILLOW_RADIUS = 90.0;
static const float BILLOW_GROWTH = 0.6;
//How deep a heap's edge is, in world units, from clear to solid: the same for every heap (#1145), so a big one keeps a
//crisp rim and a small fragment, whose radius is not many edges, stays ragged and thin. ⚠ As a share of the radius (a
//ninth) a heap three times the size had an edge three times as soft, and the big heaps overhead read as smoke
static const float CLOUD_EDGE = 10.0;
//How far the billows and the rim's erosion push the outline, in radii for a unit of noise. The billows are the noise's
//magnitude (2|n| less CLOUD_PUFF_BIAS): rounded puffs where it is strong, creases where it crosses nought, the
//cauliflower every reference's top is, where the signed noise left a smooth pillow (#1145). ⚠ Only near the outline and
//up the heap (from CLOUD_PUFF_FROM to CLOUD_PUFF_TO of its height; below, the signed noise's rounder billows): the bias is
//gone a third of a radius in, or every crease through the body was a thin clear sheet, and a heap overhead, seen from
//under its belly, read as marbled smoke
static const float CLOUD_BILLOW_PUSH = 0.8;
static const float CLOUD_PUFF_BIAS = 0.35;
static const float CLOUD_PUFF_FROM = 0.35, CLOUD_PUFF_TO = 0.7;
static const float CLOUD_RIM_PUSH = 0.25;
//THE MARGIN (#1145): how far past its outline a heap may stand, in radii. The march is bounded by the outline grown by
//it - a cylinder round the axis up to the waist, the cap's ellipsoid over it, the box round both, CloudVolumePS - and the
//billows' push fades out across the last of it (from CLOUD_PUSH_FADE_FROM of the way out, HeapInside's `e`), so the
//density is zero on every face the march stops at, whatever the noise does there. ⚠ Cut at the march's bound instead (a
//cylinder 1.12 radii round the axis, the box's sides at 1), a billow rolling out to it was solid there: a straight
//vertical edge on a heap's flank, in the phases a lobe stood at it, and seen near a world axis the box's own sides cut
//every heap at its waist.
static const float CLOUD_MARGIN = 0.45;
static const float CLOUD_PUSH_FADE_FROM = 0.9;

//The heaps' noise, baked (ProxyVolumes.GradientNoiseTexels), repeating every NOISE_PERIOD units
static const float NOISE_PERIOD = 8.0;
Texture3D CloudNoiseVolume;
sampler3D CloudNoiseSampler = sampler_state
{
    Texture = <CloudNoiseVolume>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = NONE;
    AddressU = WRAP;
    AddressV = WRAP;
    AddressW = WRAP;
};

float VolumeNoise(float3 p)
{
    return tex3Dlod(CloudNoiseSampler, float4(p / NOISE_PERIOD, 0.0)).r;
}

struct CloudVertexInput
{
    float4 Position : POSITION0;   //a corner of ProxyVolumes' unit box
    float3 Unused : TEXCOORD0;
    float4 Cloud : TEXCOORD1;      //per instance: the base's centre (x, the deck's height, z) and the heap's radius across
    float4 Shape : TEXCOORD2;      //per instance: its height, its seed, its density and how far it has faded in
    float4 Form : TEXCOORD3;       //per instance: its axis (x, z, a unit vector), how much longer it is along it, its lean
};

struct CloudVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    nointerpolation float4 Cloud : TEXCOORD1;
    nointerpolation float4 Shape : TEXCOORD2;
    nointerpolation float4 Form : TEXCOORD3;
};

//A HEAP'S OWN MEASURE (#1145): radii, x along its axis, z across it and y up from its base, its lean taken out, so its
//footprint at the waist is the unit circle and its height `aspect` (Shape.x over the radius). Every heap is one shape in
//it but for that height; in the world one is longer along its axis (form.z times its radius across: a heap drawn out
//down the wind, as a real one is) and leans along it (form.w radii at the top of its outline). One unit is one radius in
//every direction, so the billows push a flat heap's top as far as its flank. The map is affine, so a ray stays a ray in
//it, with the same parameter.
float3 HeapLocalDirection(float3 v, float4 cloud, float4 form, float aspect)
{
    float along = dot(v.xz, form.xy) - form.w * v.y / aspect;
    float across = dot(v.xz, float2(-form.y, form.x));
    return float3(along / form.z, v.y, across) / cloud.w;
}

float3 HeapLocal(float3 p, float4 cloud, float4 form, float aspect)
{
    return HeapLocalDirection(p - cloud.xyz, cloud, form, aspect);
}

CloudVertexOutput CloudVolumeVS(CloudVertexInput input)
{
    CloudVertexOutput output;
    //The box round the margin, not the outline: the billows stand past the outline, and a pixel outside the box is never
    //marched at all. Laid into the world from the heap's own measure (HeapLocal undone): along its axis, stretched and
    //leaned with it, so it stays as tight round a long heap as round a round one. The map keeps the box's winding.
    float radius = input.Cloud.w;
    float aspect = input.Shape.x / radius;
    float3 l = input.Position.xyz * float3(1.0 + CLOUD_MARGIN, aspect + CLOUD_MARGIN, 1.0 + CLOUD_MARGIN);
    float along = (l.x * input.Form.z + l.y * input.Form.w / aspect) * radius;
    float across = l.z * radius;
    float3 world = input.Cloud.xyz + float3(along * input.Form.x - across * input.Form.y, l.y * radius,
        along * input.Form.y + across * input.Form.x);
    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.WorldPosition = world;
    output.Cloud = input.Cloud;
    output.Shape = input.Shape;
    output.Form = input.Form;
    return output;
}

//How far inside the heap's outline a point stands, in radii (negative outside), from where it stands in the heap's own
//measure (l, HeapLocal): from the flat base out to its waist, then rounding in to the top, a cap of the radius across and
//the rest of the height up (iq's first-order estimate of the distance to an ellipsoid: exact on it, near enough off it).
//h is how high up the heap the point stands (0 at the base, 1 at the top of the outline), e how far out towards the
//margin's bound (1 on it: the cylinder below the waist, the cap's ellipsoid grown by CLOUD_MARGIN above)
float HeapInside(float3 l, float aspect, out float h, out float e)
{
    float waistY = CLOUD_WAIST * aspect, capY = (1.0 - CLOUD_WAIST) * aspect;
    h = l.y / aspect;
    float waist = lerp(CLOUD_BASE_WIDTH, 1.0, h / CLOUD_WAIST);
    float3 c = float3(l.x, l.y - waistY, l.z);
    float3 axes = float3(1.0, capY, 1.0);
    float k0 = length(c / axes), k1 = max(length(c / (axes * axes)), 1e-6);
    bool below = h < CLOUD_WAIST;
    e = length(float3(l.x, max(c.y, 0.0) * (1.0 + CLOUD_MARGIN) / (capY + CLOUD_MARGIN), l.z)) / (1.0 + CLOUD_MARGIN);
    return below ? waist - length(l.xz) : -k0 * (k0 - 1.0) / k1;
}

//The heap's density at a point (0 to its own density): l where it stands in the heap's own measure, p in the world (the
//billows are the world's, so a heap drifts through them and they never repeat from heap to heap), `billow` their size,
//`edge` how many radii its edge takes to go from clear to solid (CLOUD_EDGE over the radius). `fine` 0 leaves the rim's
//erosion out: the sun's tap, where only the billows' shade matters, reads one octave
float HeapDensity(float3 l, float3 p, float4 shape, float aspect, float billowSize, float edge, float fine, out float h)
{
    float e;
    float inside = HeapInside(l, aspect, h, e);

    //One return: an inlined early return inside a branch is the X4000 pattern Clouds.fxh records
    float density = 0.0;
    [branch]
    if (e < 1.0 && h > 0.0)
    {
        //The billows, rolling slowly upward, then a finer octave that erodes the rim most. Their push fades out towards
        //the margin's bound, so on it the outline alone (negative there) is all there is
        float3 q = p / billowSize + shape.y * float3(13.7, 5.3, 9.1) + float3(0.0, -CloudTime * 0.004, 0.0);
        float n = VolumeNoise(q);
        float billow = lerp(n, abs(n) * 2.0 - CLOUD_PUFF_BIAS * saturate(1.0 - inside * 3.0), smoothstep(CLOUD_PUFF_FROM, CLOUD_PUFF_TO, h));
        float rim = fine > 0.0 ? VolumeNoise(q * 3.1 + 11.3) : 0.0;
        float push = 1.0 - smoothstep(CLOUD_PUSH_FADE_FROM, 1.0, e);
        float body = inside + push * (CLOUD_BILLOW_PUSH * billow + CLOUD_RIM_PUSH * rim * saturate(1.0 - inside * 1.5));
        //The base flat, the top let go
        body *= smoothstep(0.0, 0.04, h);
        density = saturate(body / edge) * shape.z;
    }
    return density;
}

//Henyey-Greenstein, normalised so that isotropic scattering is 1
float Phase(float cosAngle, float g)
{
    float g2 = g * g;
    return (1.0 - g2) / pow(max(1.0 + g2 - 2.0 * g * cosAngle, 1e-4), 1.5);
}

float4 CloudVolumePS(CloudVertexOutput input) : COLOR
{
    float4 cloud = input.Cloud;
    float4 shape = input.Shape;
    float4 form = input.Form;
    float radius = cloud.w;
    float billowSize = CLOUD_BILLOW * pow(sqrt(radius * shape.x), BILLOW_GROWTH) * pow(BILLOW_RADIUS, 1.0 - BILLOW_GROWTH);
    float edge = CLOUD_EDGE / radius;

    float3 rd = normalize(input.WorldPosition - CameraPosition);
    //The ray in the heap's own measure (HeapLocal), where the margin's bound (CLOUD_MARGIN) is: a cylinder round the axis
    //from the base up to the waist, and above it the cap's ellipsoid, both grown by the margin. It is marched from where it
    //first enters either to where it last leaves; the box's corners are outside both, and a sample there would only find
    //nothing
    float aspect = shape.x / radius;
    float3 o = HeapLocal(CameraPosition, cloud, form, aspect);
    float3 d = HeapLocalDirection(rd, cloud, form, aspect);
    float dy = abs(d.y) > 1e-9 ? d.y : 1e-9;
    float reach = 1.0 + CLOUD_MARGIN;
    float waistY = CLOUD_WAIST * aspect, capY = (1.0 - CLOUD_WAIST) * aspect + CLOUD_MARGIN;

    float a = max(dot(d.xz, d.xz), 1e-12);
    float b = dot(o.xz, d.xz);
    float disc = b * b - a * (dot(o.xz, o.xz) - reach * reach);
    float root = sqrt(max(disc, 0.0));
    float ya = -o.y / dy, yb = (waistY - o.y) / dy;
    float c0 = max((-b - root) / a, min(ya, yb));
    float c1 = min((-b + root) / a, max(ya, yb));
    bool cylinder = disc >= 0.0 && c1 > c0;

    float squash = reach / capY;
    float3 oe = float3(o.x, (o.y - waistY) * squash, o.z);
    float3 de = float3(d.x, d.y * squash, d.z);
    float ea = max(dot(de, de), 1e-12), eb = dot(oe, de);
    float ed = eb * eb - ea * (dot(oe, oe) - reach * reach);
    float eroot = sqrt(max(ed, 0.0));
    float yc = (waistY + capY - o.y) / dy;
    float e0 = max((-eb - eroot) / ea, min(ya, yc));
    float e1 = min((-eb + eroot) / ea, max(ya, yc));
    bool ellipsoid = ed >= 0.0 && e1 > e0;

    float t0 = max(min(cylinder ? c0 : 1e30, ellipsoid ? e0 : 1e30), 0.0);
    float t1 = max(cylinder ? c1 : -1e30, ellipsoid ? e1 : -1e30);
    clip(t1 - t0);
    float3 sunLocal = HeapLocalDirection(SunDirection, cloud, form, aspect);

    int steps = (int)round(lerp((float)CLOUD_STEPS, (float)CLOUD_STEPS_FAR, saturate((t0 - STEPS_FAR_FROM) / (STEPS_FAR_AT - STEPS_FAR_FROM))));
    float dt = (t1 - t0) / steps;
    float jitter = frac(52.9829189 * frac(dot(input.Position.xy, float2(0.06711056, 0.00583715))));

    //Forward scattering towards the sun and a little back towards the lens: a heap with the sun behind it glows at its rim.
    //⚠ At 0.6 two thirds of the light the forward lobe peaked ten times the side's, and against a backlit dome (the sea's)
    //every rim lit as a white vein
    float cosSun = dot(rd, SunDirection);
    float phase = lerp(Phase(cosSun, -0.15), Phase(cosSun, 0.3), 0.4);
    float3 skyTop = CloudSunColor * CLOUD_SKY_GAIN + CloudShadowColor;

    float transmit = 1.0;
    float3 color = 0.0;
    [loop]
    for (int i = 0; i < steps; i++)
    {
        float t = t0 + (i + jitter) * dt;
        float3 p = CameraPosition + rd * t;
        float3 l = o + d * t;
        float h;
        float density = HeapDensity(l, p, shape, aspect, billowSize, edge, 1.0, h);
        [branch]
        if (density > 0.02)
        {
            //The sun through the heap: one tap close by through the billows, then the outline alone further on, which is
            //the heap's broad shading (its sunward side lit, its belly in its own shade) for no noise at all
            float hs, es;
            float near = HeapDensity(l + sunLocal * (radius * 0.12), p + SunDirection * (radius * 0.12), shape, aspect, billowSize, edge, 0.0, hs);
            float far = saturate(HeapInside(l + sunLocal * (radius * 0.5), aspect, hs, es) * 4.0) * shape.z;
            float depth = (near * 0.12 + far * 0.38) * radius;
            float sun = exp(-CLOUD_SIGMA * depth);
            //The powder term: a thin edge facing away from the sun is darker than Beer-Lambert alone says
            sun *= 1.0 - 0.6 * exp(-CLOUD_SIGMA * depth * 4.0);
            //And a heap's lower part greys, as every reference's does: the light that fills its sides is scattered down out of
            //the body above, and near the base there is little body above. ⚠ Lit as brightly as its top, the lower side of every
            //farther heap showed as a white seam under the flat dark base of the nearer one
            sun *= lerp(0.3, 1.0, smoothstep(0.0, 0.45, h));

            //The sky from above, the deck's underside colour from below
            float3 ambient = lerp(CloudShadowColor, skyTop, saturate(h * 1.3));
            float3 light = CloudSunColor * (sun * phase * CLOUD_SUN_GAIN) + ambient;

            float alpha = 1.0 - exp(-CLOUD_SIGMA * density * dt);
            color += transmit * alpha * light;
            transmit *= 1.0 - alpha;
        }
        if (transmit < 0.03)
            break;
    }

    //Dissolved into the sky before the far plane, by how far in it has faded (shape.w: a heap the weather grows or thins)
    float fade = (1.0 - smoothstep(FadeNear, FadeFar, t0)) * shape.w;
    clip((1.0 - transmit) * fade - 0.002);
    return float4(color, 1.0 - transmit) * fade;
}

//--- The composite: the heaps are marched into a target the display's own size (CloudVolumes), not the supersampled scene's -
//a cloud is soft, and the supersample's four samples a pixel would only march the same soft edge four times - before the
//scene target is bound, and laid over the deck here, premultiplied, with the sky (SkyPass: behind the opaque scene, at the
//far plane). Whatever stands in front of the sky covers it, as it covers the deck.
Texture2D CloudLayer;
sampler CloudLayerSampler = sampler_state
{
    Texture = <CloudLayer>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//A corner of the full-screen quad, in clip space (the BillboardVertex layout, without the box's instance stream)
struct CompositeVertexInput
{
    float4 Position : POSITION0;
    float3 Unused : TEXCOORD0;
};

struct CompositeVertexOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};

CompositeVertexOutput CompositeVS(CompositeVertexInput input)
{
    CompositeVertexOutput output;
    output.Position = float4(input.Position.xy, 0.0, 1.0);
    output.UV = float2(input.Position.x * 0.5 + 0.5, 0.5 - input.Position.y * 0.5);
    return output;
}

float4 CompositePS(CompositeVertexOutput input) : COLOR
{
    return tex2D(CloudLayerSampler, input.UV);
}

technique CloudVolume
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CloudVolumeVS();
        PixelShader = compile PS_SHADERMODEL CloudVolumePS();
    }
};

technique CloudComposite
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CompositeVS();
        PixelShader = compile PS_SHADERMODEL CompositePS();
    }
};
