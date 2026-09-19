//The sun's cast shadows, one copy (#469): sampling of the orthographic shadow map SceneRenderer renders from
//the sun's direction before the scene pass (SunShadowMap). A receiver includes this, declares nothing else,
//and multiplies its SUN term by SunShadow() - the dome's ambient is untouched, which is what a shadow is: the
//sky still lights it, the sun does not.
//
//Nothing in the project cast a shadow until #469: a tree was lit by dot(N, Sun) and the grass under it by the
//same rule with nothing in between, so a plain full of solids floated on an evenly lit floor. The map is a
//single 32-bit depth target fitted round the camera (a fixed extent, the window snapped to its own texels so
//the shadows do not swim as the camera moves), so shadows exist within ShadowExtent of the lens and fade out
//at the map's edge rather than ending in a line.
//
//Every caller gates on ShadowStrength with a [branch]: 0 means no map is bound this frame (the scene has
//none, the tier dropped it, or the sun is below the horizon), and the samples must not run - they would
//read an unbound texture, and on a scene without shadows they would cost nine taps for nothing.

texture ShadowMap;
sampler ShadowSampler = sampler_state
{
    Texture = <ShadowMap>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

//World -> the map's clip space (view * orthographic projection), the size of one texel in the map's UV, how
//dark a full shadow is (1 = the sun term gone entirely; a little under it keeps a shadow from reading as a
//hole), and the depth bias in the map's own depth units, already scaled to the map's depth range.
float4x4 ShadowViewProjection;
float ShadowTexel;
float ShadowStrength;
float ShadowBias;

//The map is sampled by hand with a nine-tap box (PCF) rather than a comparison sampler: MonoGame's effect
//path gives no comparison state, and nine point taps on a 2048 map are cheap against a full-screen terrain
//shader's other work. tex2Dlod rather than tex2D, because this runs under a [branch] and a gradient
//instruction inside divergent flow is what the compiler refuses.
//
//Returns the sunlight factor: 1 lit, down to (1 - ShadowStrength) in full shadow. Outside the map, or past
//its far plane, everything is lit, and the last few per cent of the map's width fade the shadow out.
float SunShadow(float3 worldPosition, float3 normal, float3 sunDirection)
{
    float4 lp = mul(float4(worldPosition, 1.0), ShadowViewProjection);
    float2 uv = float2(lp.x * 0.5 + 0.5, 0.5 - lp.y * 0.5);
    float depth = lp.z;

    if (any(uv < 0.0) || any(uv > 1.0) || depth > 1.0) return 1.0;

    //Slope-scaled: a surface turned away from the sun needs more bias than one facing it, or its own map
    //texels, quantised across it, put it in and out of its own shadow in stripes (acne).
    float ndotl = saturate(dot(normal, sunDirection));
    float bias = ShadowBias * (1.0 + 2.5 * (1.0 - ndotl));
    float reference = depth - bias;

    float lit = 0.0;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float stored = tex2Dlod(ShadowSampler, float4(uv + float2(x, y) * ShadowTexel, 0.0, 0.0)).r;
            lit += reference <= stored ? 1.0 : 0.0;
        }
    }
    lit /= 9.0;

    float2 edge = min(uv, 1.0 - uv);
    float fade = saturate(min(edge.x, edge.y) / 0.06);
    return 1.0 - (1.0 - lit) * ShadowStrength * fade;
}
