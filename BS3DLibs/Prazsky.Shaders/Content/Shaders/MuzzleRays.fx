//The special round's rays (#820): while the round in the bore is a special one - the wildcard (the owner's
//"chameleon", the rainbow ball) or the Cutter - a burst of light turns slowly round the muzzle, so the player knows
//the next shot is something rare and makes a moment of firing it. The owner's words: "whenever I have any special
//ball loaded, I want a strong rotating glow from the cannon, in rays".
//
//One quad bolted to the barrel, NOT facing the lens: it lies in the plane square to the bore at the muzzle face, and
//turns with the gun like the collar it radiates from (#425 took the camera-facing halo out because its visible shape
//was whatever the barrel happened not to cover from the current view). From the play camera, behind the gun, that
//plane is seen nearly face on; the barrel in front of it covers the middle, and the rays come out round the tube.
//
//Everything is in polar coordinates round the bore's axis, in world units in the quad's plane:
//  the ROOT   - nothing inside the collar's crest (RayShape.x), so the round in the bore is never washed over;
//  the CORONA - an even glow just outside the crest, so the burst reads as light from the muzzle rather than spokes
//               stuck on it;
//  the RAYS   - two sets turning opposite ways, broad and fine, each ray a length of its own (the owner's rule that a
//               perfect surface looks synthetic, #640), fading to nothing at the reach (RayShape.y).
//The wildcard's burst is a rainbow, one hue a broad ray turning with it, because the ball counts as any colour; every
//other round's is its own colour (RayLight).
//
//Premultiplied alpha (BlendState.AlphaBlend) with an alpha of 0, so it ADDS to what is behind and leaves the target's
//alpha alone; depth-read, writing no depth; linear radiance. SM 5.0. No Potato port: the Pi draws no rays.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 World;        //the quad's plane: the barrel's pose with the muzzle face's offset along the bore
float4x4 View;
float4x4 Projection;

//RayShape.x  the root: the collar's crest radius, world units
//RayShape.y  the reach: where the rays end, world units
//RayShape.z  the spin, radians - the broad set's; the fine set turns FINE_SPIN times as fast the other way
//RayShape.w  the rainbow, 0 or 1: 1 colours the burst by angle instead of by RayLight's hue
float4 RayShape;

//The radiance at a broad ray's root, linear, the caller's strength included. Its hue for a plain-coloured burst; for a
//rainbow only its brightness counts (the caller hands a grey).
float3 RayLight;

static const float TAU = 6.2831853;

//The broad set: few, wide lobes - and the fine set between them, narrower, dimmer and turning the other way, so the
//burst never reads as one printed star rotating
static const float BROAD_RAYS = 7.0;
static const float BROAD_SHARPNESS = 6.0;
static const float FINE_RAYS = 13.0;
static const float FINE_SHARPNESS = 26.0;
static const float FINE_SPIN = -0.65;
static const float FINE_SHARE = 0.6;

//How much shorter a ray may be than the reach: each ray's own, from a hash of its index, so no two match
static const float LENGTH_JITTER = 0.4;

//The corona: its width as a share of the root-to-reach run, and its brightness against a broad ray's root
static const float CORONA_WIDTH = 0.14;
static const float CORONA_SHARE = 0.35;

struct RayVertexInput
{
    float2 Corner : TEXCOORD0;   //-1..1 across the quad
};

struct RayVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Plane : TEXCOORD0;    //the corner in world units in the quad's plane
};

RayVertexOutput MuzzleRaysVS(RayVertexInput input)
{
    RayVertexOutput output;

    float2 plane = input.Corner * RayShape.y;
    float4 world = mul(float4(plane, 0.0, 1.0), World);
    output.Position = mul(mul(world, View), Projection);
    output.Plane = plane;
    return output;
}

float Hash(float n)
{
    return frac(sin(n * 12.9898 + 4.1414) * 43758.5453);
}

//One set of rays at this angle: the lobe of the nearest ray, faded along its own length. The index is wrapped into
//0..count-1, so the ray straddling atan2's seam is one ray with one length on both sides of it (count is whole)
float RaySet(float angle, float count, float sharpness, float salt, float along)
{
    float phase = angle * count / TAU;
    float index = floor(phase + 0.5);
    float id = index - count * floor(index / count);

    float lobe = pow(saturate(cos(TAU * (phase - index))), sharpness);
    float rayLength = 1.0 - LENGTH_JITTER * Hash(id + salt);
    float fade = saturate(1.0 - along / rayLength);

    return lobe * fade * fade;
}

//A fully saturated hue round the wheel, 0..1
float3 Rainbow(float h)
{
    return saturate(abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
}

float4 MuzzleRaysPS(RayVertexOutput input) : COLOR0
{
    float root = RayShape.x;
    float reach = RayShape.y;
    float spin = RayShape.z;

    float r = length(input.Plane);
    float along = (r - root) / max(reach - root, 1e-3);

    //Nothing inside the crest, eased in over a sliver past it so the root is soft rather than cut
    float start = smoothstep(-0.03, 0.05, along);

    float theta = atan2(input.Plane.y, input.Plane.x);
    float broadAngle = theta - spin;

    float rays = RaySet(broadAngle, BROAD_RAYS, BROAD_SHARPNESS, 3.0, along)
        + FINE_SHARE * RaySet(theta - FINE_SPIN * spin, FINE_RAYS, FINE_SHARPNESS, 17.0, along);

    float corona = CORONA_SHARE * exp(-(along * along) / (CORONA_WIDTH * CORONA_WIDTH));

    float light = start * (rays + corona) * step(along, 1.0);

    //The rainbow turns with the broad rays: one hue a broad ray. Never whitened: the first capture's hot white roots took
    //the hues with them, and a white burst says nothing about a ball that counts as every colour
    float3 hue = Rainbow(frac(broadAngle / TAU));
    float3 colour = RayLight * lerp(float3(1.0, 1.0, 1.0), hue, RayShape.w);

    return float4(colour * light, 0.0);
}

technique MuzzleRays
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MuzzleRaysVS();
        PixelShader = compile PS_SHADERMODEL MuzzleRaysPS();
    }
};
