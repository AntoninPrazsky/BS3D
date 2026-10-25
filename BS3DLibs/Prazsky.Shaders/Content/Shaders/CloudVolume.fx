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

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

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
//How much light a heap takes away per world unit at full density: a heap a hundred units across is all but solid
static const float CLOUD_SIGMA = 0.09;
//How bright the sunlit side is against the deck's lit colour, and how much of the sky's light reaches the shaded side
static const float CLOUD_SUN_GAIN = 1.25;
static const float CLOUD_SKY_GAIN = 0.55;
//The shape: where the heap is widest (a share of its height), how wide its base is against that, and the billows' size
//as a share of its radius
static const float CLOUD_WAIST = 0.3;
static const float CLOUD_BASE_WIDTH = 0.78;
static const float CLOUD_BILLOW = 0.45;

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
    float4 Cloud : TEXCOORD1;      //per instance: the base's centre (x, the deck's height, z) and the heap's radius
    float4 Shape : TEXCOORD2;      //per instance: its height, its seed, its density and how far it has faded in
};

struct CloudVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    nointerpolation float4 Cloud : TEXCOORD1;
    nointerpolation float4 Shape : TEXCOORD2;
};

CloudVertexOutput CloudVolumeVS(CloudVertexInput input)
{
    CloudVertexOutput output;
    float3 extent = float3(input.Cloud.w, input.Shape.x, input.Cloud.w);
    float3 world = input.Cloud.xyz + input.Position.xyz * extent;
    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.WorldPosition = world;
    output.Cloud = input.Cloud;
    output.Shape = input.Shape;
    return output;
}

//How far inside the heap's outline p stands, in radii (negative outside): from the flat base out to its waist, then
//rounding in to the top; h is how high up it the point stands (0 at the base, 1 at the top)
float HeapInside(float3 p, float4 cloud, float4 shape, out float h)
{
    h = (p.y - cloud.y) / shape.x;
    float waist = h < CLOUD_WAIST ? lerp(CLOUD_BASE_WIDTH, 1.0, h / CLOUD_WAIST)
        : sqrt(saturate(1.0 - (h - CLOUD_WAIST) * (h - CLOUD_WAIST) / ((1.0 - CLOUD_WAIST) * (1.0 - CLOUD_WAIST))));
    return waist - length(p.xz - cloud.xz) / cloud.w;
}

//The heap's density at p (0 to its own density). `fine` 0 leaves the rim's erosion out: the sun's tap, where only the
//billows' shade matters, reads one octave
float HeapDensity(float3 p, float4 cloud, float4 shape, float fine, out float h)
{
    float radius = cloud.w;
    float inside = HeapInside(p, cloud, shape, h);

    //One return: an inlined early return inside a branch is the X4000 pattern Clouds.fxh records
    float density = 0.0;
    [branch]
    if (inside > -0.5 && h > 0.0 && h < 1.0)
    {
        //The billows, in the heap's own size, rolling slowly upward, then a finer octave that erodes the rim most
        float3 q = p / (radius * CLOUD_BILLOW) + shape.y * float3(13.7, 5.3, 9.1) + float3(0.0, -CloudTime * 0.004, 0.0);
        float billow = VolumeNoise(q);
        float rim = fine > 0.0 ? VolumeNoise(q * 3.1 + 11.3) : 0.0;
        float body = inside + 0.6 * billow + 0.25 * rim * saturate(1.0 - inside * 1.5);
        //The base flat, the top let go
        body *= smoothstep(0.0, 0.04, h);
        density = saturate(body * 9.0) * shape.z;
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
    float radius = cloud.w;

    float3 rd = normalize(input.WorldPosition - CameraPosition);
    //The cylinder the heap's outline stands in (its billows reach a little past it): round the axis, then between the
    //base and the top. The box's corners are outside it, and a sample there would only find nothing
    float reachR = radius * 1.12;
    float3 o = CameraPosition - cloud.xyz;
    float a = max(dot(rd.xz, rd.xz), 1e-6);
    float b = dot(o.xz, rd.xz);
    float disc = b * b - a * (dot(o.xz, o.xz) - reachR * reachR);
    clip(disc);
    float root = sqrt(max(disc, 0.0));
    float ry = abs(rd.y) > 1e-5 ? rd.y : 1e-5;
    float ya = -o.y / ry, yb = (shape.x - o.y) / ry;
    float t0 = max(max((-b - root) / a, min(ya, yb)), 0.0);
    float t1 = min((-b + root) / a, max(ya, yb));
    clip(t1 - t0);

    float dt = (t1 - t0) / CLOUD_STEPS;
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
    for (int i = 0; i < CLOUD_STEPS; i++)
    {
        float3 p = CameraPosition + rd * (t0 + (i + jitter) * dt);
        float h;
        float density = HeapDensity(p, cloud, shape, 1.0, h);
        [branch]
        if (density > 0.02)
        {
            //The sun through the heap: one tap close by through the billows, then the outline alone further on, which is
            //the heap's broad shading (its sunward side lit, its belly in its own shade) for no noise at all
            float hs;
            float near = HeapDensity(p + SunDirection * (radius * 0.12), cloud, shape, 0.0, hs);
            float far = saturate(HeapInside(p + SunDirection * (radius * 0.5), cloud, shape, hs) * 4.0) * shape.z;
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
//a cloud is soft, and the supersample's four samples a pixel would only march the same soft edge four times - and laid over
//the scene target here, right after the sky, premultiplied. Everything drawn after it (the ground, the island, the balls)
//covers it where it stands in front, as it covers the deck.
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
