//The outback's heat shimmer (#1135): the far horizon refracted in a thin strip at the skyline. Not a colour laid on the
//plain (#1123 took that one out: it crept over the middle plain as a pale fog), but the frame already drawn read again a
//little above or below where each pixel looks, so the plain's edge and the feet of the far formations waver by about a
//pixel, and under the skyline a sliver of the sky and of the rock standing on it is mirrored, as hot ground mirrors it.
//
//THE BAND is a ring round the lens (HorizonShimmer.cs builds it and its vertex buffer), at seven tenths of the distance
//where the plain's last fade begins, from just under where the plain meets that radius to REACH over the skyline's axis:
//the plain where the fade begins, which is the line the eye reads as the horizon. Both passes draw it depth-tested and
//write no depth, so the island, the cluster, the gun, every nearer formation and the whole plain nearer than the radius
//hide it: only the far plain, the formations on the skyline and the sky take it.
//
//  Mark     writes an alpha of 0 where the band passes, and nothing else. The far grab that follows
//           (Tonemap.fx, SceneGrabFar) weighs each sample by that mark, so the copy holds the far samples' colour alone,
//           premultiplied by how much of each pixel is far.
//  Refract  reads the copy at angles above and below the pixel's own and composites what it finds over the pixel's own
//           far colour by its far share, so a read that lands on a near thing (the edge of a formation nearer than the
//           radius, the island's rim) brings nothing of it back. Opaque, so it writes the alpha back to 1.
//
//⚠ THE OFFSET IS AN ANGLE, never pixels: a direction's elevation moved by radians and projected back through the camera,
//so the waver is the same piece of the world at any resolution and a narrower lens shows it larger, as a telephoto does.
//The one figure in pixels, GrabSize, only finds the pixel's own texel of the copy. The clock is the scene's (the Game's wall
//clock, the Testbed's scene clock), never a frame count.
//
//Shader Model 5.0, and 3.0 for OpenGL ES (ShaderTarget.fxh), though no phone draws it (GraphicsSupport.HorizonShimmer).

#include "ShaderTarget.fxh"
#include "Noise.fxh"

float4x4 ViewProjection;
float3 CameraPosition;

//The ring: its radius round the lens and the tangents of its lowest and highest elevation (a vertex's height over the
//lens is the radius times one of them)
float3 Ring;

//The band's elevations over the horizontal, radians: where the plain at the ring's radius lies (the shimmer grows from
//nothing there), the skyline's axis (the plain where its last fade begins, where the shimmer is strongest and the mirage's
//mirror runs) and the top it has faded out by
float3 Band;

//The largest offset this frame, radians (weaker the steeper the lens looks down at the skyline, HorizonShimmer.GRAZE)
float Amplitude;

//The mirage under the axis: how deep it reaches, radians, and how much of the mirrored image shows at the axis this frame
float2 Mirage;

//Seconds on the scene's clock, wrapped by the host (HorizonShimmer.CLOCK_WRAP)
float Time;

//The copy's size in texels
float2 GrabSize;

texture Grab;
sampler2D GrabSampler = sampler_state
{
    Texture = <Grab>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//The waver's field. Gradient noise of the HEADING on a circle (an atan2 of it would seam behind the lens) and of the
//height over the skyline's axis, the second coordinate sliding down the field as the clock runs, so the pattern rises
//through the band; a finer octave rises faster. Cells per radian of heading (ACROSS) and of elevation (UP), cells a
//second (TEMPO): an angle and a time, nothing on the screen.
static const float ACROSS = 230.0;
static const float UP = 380.0;
static const float TEMPO = 3.5;
static const float FINE_SCALE = 2.0;
static const float FINE_TEMPO = 9.0;

//The two octaves' sum spreads about 0.15 either way (0.28 at the 95th percentile): scaled so the offset reaches its
//largest about one sample in twenty and clamped there
static const float WOBBLE_GAIN = 3.3;

float Wobble(float2 heading, float above)
{
    float coarse = GradientNoise3(float3(heading * ACROSS, above * UP - Time * TEMPO));
    float fine = GradientNoise3(float3(heading * (ACROSS * FINE_SCALE), above * (UP * FINE_SCALE) - Time * FINE_TEMPO + 31.7));

    return clamp((coarse * 0.7 + fine * 0.3) * WOBBLE_GAIN, -1.0, 1.0);
}

//How far off the camera a direction is projected to find its place in the copy: far enough that the camera's position,
//hundreds of units from the origin, costs the direction nothing in a float
static const float PROJECTED_DISTANCE = 1000.0;

//Where the direction at this heading and elevation lands in the copy
float2 GrabUv(float2 heading, float elevation)
{
    float3 direction = float3(heading.x * cos(elevation), sin(elevation), heading.y * cos(elevation));
    float4 clip = mul(float4(CameraPosition + direction * PROJECTED_DISTANCE, 1.0), ViewProjection);

    //The z row read for nothing: MojoShader keeps only the registers a program reads, and a hole in the middle of the
    //matrix would put every uniform after it one slot late (ShaderTarget.fxh)
    return clip.xy / clip.w * float2(0.5, -0.5) + 0.5 KEEP_READ(clip.z);
}

struct BandVertex
{
    float3 Position : POSITION0;
};

struct BandPixel
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
    float4 Clip : TEXCOORD1;
};

//One vertex program for both passes, so the two cover the very same samples
BandPixel BandVS(BandVertex input)
{
    BandPixel output;

    //x and z the unit circle, y 0 on the lower edge and 1 on the upper
    float3 ray = float3(input.Position.x, lerp(Ring.y, Ring.z, input.Position.y), input.Position.z) * Ring.x;

    output.Position = mul(float4(CameraPosition + ray, 1.0), ViewProjection);
    output.Ray = ray;
    output.Clip = output.Position;

    return output;
}

float4 MarkPS(BandPixel input) : COLOR
{
    return 0;
}

float4 RefractPS(BandPixel input) : COLOR
{
    float3 ray = normalize(input.Ray);
    float2 heading = normalize(ray.xz);
    float elevation = asin(ray.y);

    //The pixel's own texel of the copy, at its centre: every sample a display pixel resolves reads the same one, so where
    //nothing moves the samples are written their own box average and the resolve gives back exactly what it had. Every
    //read below is that centre moved by how far its direction lands from the pixel's own, so an offset of nothing reads
    //the own texel again rather than a blend of it with its neighbours
    float2 uv = input.Clip.xy / input.Clip.w * float2(0.5, -0.5) + 0.5;
    float2 centre = (floor(uv * GrabSize) + 0.5) / GrabSize;
    float2 here = GrabUv(heading, elevation);
    float4 own = tex2Dlod(GrabSampler, float4(centre, 0, 0));
    float3 color = own.rgb / max(own.a, 1e-4);

    //The waver: nothing where the band's plain begins, growing over the far plain to the axis and fading out above it
    float reach = smoothstep(Band.x, Band.y, elevation) * (1.0 - smoothstep(Band.y, Band.z, elevation));
    float2 shiftedUv = centre + GrabUv(heading, elevation + Amplitude * reach * Wobble(heading, elevation - Band.y)) - here;
    float4 shifted = tex2Dlod(GrabSampler, float4(shiftedUv, 0, 0));
    color = color * (1.0 - shifted.a) + shifted.rgb;

    //The mirage: under the axis, which wavers with the rest, the sky and what stands on it mirrored, fading out with depth.
    //A point on the axis is its own mirror, so the image meets what is above it without a seam
    float axis = Band.y + Amplitude * Wobble(heading, 0.0);
    float below = axis - elevation;

    [branch]
    if (below > 0.0 && below < Mirage.x)
    {
        float4 mirrored = tex2Dlod(GrabSampler, float4(centre + GrabUv(heading, axis + below) - here, 0, 0));
        float3 image = color * (1.0 - mirrored.a) + mirrored.rgb;
        float sheet = Mirage.y * (1.0 - smoothstep(0.0, Mirage.x, below));
        color = lerp(color, image, sheet);
    }

    return float4(color, 1.0);
}

technique Mark
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL BandVS();
        PixelShader = compile PS_SHADERMODEL MarkPS();
    }
};

technique Refract
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL BandVS();
        PixelShader = compile PS_SHADERMODEL RefractPS();
    }
};
