//GamePi's Potato tier (#789): the one effect every InstancedModelRenderer draws with on the Raspberry Pi, in place of
//Prazsky.Shaders' InstancedModel.fx - the balls, the island, the gun, the ceiling's glass, the drain, the wordmark.
//
//Not a port of InstancedModel.fx and not trying to be one: that effect is Shader Model 5.0 with 35 techniques and
//about 150 uniforms, none of which MojoShader can take, and on V3D its relief marches alone would cost more than a
//whole Potato frame. This is its LIGHTING MODEL, kept - the same three-light rig the host tints from the sky dome,
//the same hemisphere ambient and occlusion from the instance stream, the same Blinn-Phong and Fresnel environment,
//so a scene's light reads the same - with everything a surface does on top of it given up: no relief, no ball
//patterns or materials (every BallShading is one lit, coloured ball), no shadows, no clouds, no refraction, no
//scene lights. InstancedModelRenderer recognises this effect by its techniques and draws through DrawPotato, which
//sets the uniforms below and no others; a uniform named here that a draw does not set keeps the value the last
//draw left, so DrawPotato sets every one each draw, as InstancedModelRenderer.Draw does on the desktop.
//
//The instance stream is InstancedModel.fx's exactly (ModelInstance, 88 bytes): the world matrix's four rows in
//TEXCOORD1-4, the occluder direction and base occlusion in TEXCOORD5, the dissolve in TEXCOORD6, the ripple in
//TEXCOORD7. Every effect here ends in PotatoOutput.fxh's ToDisplay: there is no HDR target to tonemap later.

#include "PotatoOutput.fxh"

float4x4 View;
float4x4 Projection;
float3 EyePosition;

//The material, as InstancedModelRenderer hands it over: sRGB, the diffuse and the ambient premultiplied by alpha
//(as BasicEffect does on the CPU), decoded to linear here the way InstancedModel.fx's ShadePixel does it
float4 DiffuseColor;
float3 AmbientColor;
float3 EmissiveColor;
float3 SpecularColor;
float SpecularPower;

//The light rig, linear (LinearLightRig): the positional key light, and the fill and back lights by direction
float3 KeyLightPosition;
float3 DirLight0DiffuseColor;
float3 DirLight0SpecularColor;
float3 DirLight1Direction;
float3 DirLight1DiffuseColor;
float3 DirLight1SpecularColor;
float3 DirLight2Direction;
float3 DirLight2DiffuseColor;
float3 DirLight2SpecularColor;
float DirLightStrength;

//The hemisphere ambient, linear, and the floor the ground occlusion fades towards
float3 SkyColor;
float3 GroundColor;
float GroundHeight;

float SpecularAmbientStrength;
float Metalness;
float TwoSidedNormals;
float SpecularAlphaWeight;
float3 EmissiveTint;

//The island's stone (PotatoTextured): the same detail texture as the desktop's triplanar, tapped once from above
//and once from the side instead of three times, and with none of its relief, dust, bands or joints
texture Texture;
sampler2D TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};
float DetailScale;
float DetailStrength;
float DetailBoost;

//What every ball is whatever it is made of (BallCommon.fxh): its colour, its heartbeat, its ripple, its dissolve
float3 PatternPrimaryColor;
float EmissiveStrength;
float StillEmission = 1;
float PulseTime;
float PulseSpeed;
float PulseDepth;
float3 PulseDirection;
float PulseWavelength;
float RippleStrength;
float3 RippleAlarmColor;
float DissolvePixelSize;

struct VertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
};

struct InstanceInput
{
    float4 WorldRow1 : TEXCOORD1;
    float4 WorldRow2 : TEXCOORD2;
    float4 WorldRow3 : TEXCOORD3;
    float4 WorldRow4 : TEXCOORD4;
    float4 Custom : TEXCOORD5;
    float Dissolve : TEXCOORD6;
    float Ripple : TEXCOORD7;
};

struct VertexOutput
{
    float4 Position : POSITION0;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
    float2 DissolveRipple : TEXCOORD3;
};

//The normal by the world matrix's cofactor, as Common.fxh's NormalToWorld: right under a non-uniform scale and a
//mirror. The desktop's Bone is always identity (every mesh is procedural), so it is left out.
float3 NormalToWorld(float3 objectNormal, float3 r0, float3 r1, float3 r2)
{
    float3 c0 = cross(r1, r2);
    float3 normal = objectNormal.x * c0 + objectNormal.y * cross(r2, r0) + objectNormal.z * cross(r0, r1);

    return normalize(dot(r0, c0) < 0.0 ? -normal : normal);
}

VertexOutput PotatoVS(VertexInput input, InstanceInput instance)
{
    VertexOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
    float4 worldPosition = mul(input.Position, world);

    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(input.Normal, instance.WorldRow1.xyz, instance.WorldRow2.xyz, instance.WorldRow3.xyz);
    output.OcclusionData = instance.Custom;
    output.DissolveRipple = float2(instance.Dissolve, instance.Ripple);

    return output;
}

//Lighting.fxh's AddLight: a hard terminator, Blinn-Phong on the lit side only
void AddLight(float3 towardsLight, float3 lightDiffuse, float3 lightSpecular, float3 normal, float3 eye,
    inout float3 diffuse, inout float3 specular)
{
    float dotL = dot(normal, towardsLight);
    float lit = step(0, dotL);

    diffuse += lightDiffuse * (dotL * lit);

    float dotH = max(dot(normal, normalize(towardsLight + eye)), 0);
    specular += lightSpecular * pow(dotH * lit, SpecularPower);
}

float3 SkyRadiance(float3 direction)
{
    return lerp(GroundColor, SkyColor, direction.y * 0.5 + 0.5);
}

//Lighting.fxh's SurfaceOcclusion and its constants: the instance's own occluder, and the ground's
float SurfaceOcclusion(float3 worldPosition, float3 normal, float4 occlusionData)
{
    float occlusion = saturate(occlusionData.w - 1.1 * max(0, dot(normal, occlusionData.xyz)));
    float groundProximity = saturate(1 - (worldPosition.y - GroundHeight) / 2.0);

    return saturate(occlusion - 0.55 * groundProximity * saturate(-normal.y));
}

//Lighting.fxh's ShadePixel without its clouds, shadows, scene lights and per-surface specular: the key, fill and back
//lights, the occluded hemisphere, the specular and the Fresnel environment, in linear radiance. The returned colour
//is premultiplied, as the material is.
float4 Shade(float3 worldPosition, float3 rawNormal, float4 occlusionData, float3 texRgb)
{
    float3 eye = normalize(EyePosition - worldPosition);
    float3 normal = normalize(rawNormal);

    //An open surface lit from behind (the drain's glass and its pit): the side facing the eye. By the eye rather than
    //by VFACE, which on DesktopGL follows whatever cull state the previous draw left (#785's renderer audit).
    if (TwoSidedNormals > 0 && dot(normal, eye) < 0) normal = -normal;

    float3 diffuse = 0;
    float3 specular = 0;
    AddLight(normalize(KeyLightPosition - worldPosition), DirLight0DiffuseColor, DirLight0SpecularColor, normal, eye, diffuse, specular);
    AddLight(-DirLight1Direction, DirLight1DiffuseColor, DirLight1SpecularColor, normal, eye, diffuse, specular);
    AddLight(-DirLight2Direction, DirLight2DiffuseColor, DirLight2SpecularColor, normal, eye, diffuse, specular);
    diffuse *= DirLightStrength;
    specular *= DirLightStrength;

    float occlusion = SurfaceOcclusion(worldPosition, normal, occlusionData);
    float burial = saturate((0.45 - occlusionData.w) / 0.35);
    float diffuseOcclusion = lerp(0.6, 1.0, occlusion) * lerp(1.0, 0.4, burial);

    float4 color = float4((diffuse * SrgbToLinear(DiffuseColor.rgb) * diffuseOcclusion
        + SkyRadiance(normal) * SrgbToLinear(AmbientColor) * occlusion
        + SrgbToLinear(EmissiveColor)) * texRgb, DiffuseColor.a);

    float specularAlpha = lerp(1.0, color.a, SpecularAlphaWeight);
    float3 linearSpecular = SrgbToLinear(SpecularColor);
    color.rgb += specular * linearSpecular * specularAlpha * occlusion;

    //The environment: the sky dome reflected, rough surfaces seeing its average
    float roughness = sqrt(2.0 / (SpecularPower + 2.0));
    float3 environment = lerp(SkyRadiance(reflect(-eye, normal)), (SkyColor + GroundColor) * 0.5, saturate(roughness));
    float3 reflectanceAtNormal = lerp(0.04 * linearSpecular, linearSpecular, Metalness);
    float3 fresnel = reflectanceAtNormal + (max(1.0, reflectanceAtNormal) - reflectanceAtNormal) * pow(1 - saturate(dot(normal, eye)), 5);
    color.rgb += environment * fresnel * SpecularAmbientStrength * specularAlpha * occlusion;

    color.rgb += EmissiveTint;

    return color;
}

float4 LitPS(VertexOutput input) : COLOR0
{
    return ToDisplayPremultiplied(Shade(input.WorldPosition, input.WorldNormal, input.OcclusionData, 1));
}

float4 TexturedPS(VertexOutput input) : COLOR0
{
    //Two taps rather than the desktop's three: from above for the cap, and along whichever horizontal axis faces the
    //surface for the drum, blended by the normal
    float3 normal = normalize(input.WorldNormal);
    float3 p = input.WorldPosition * DetailScale;
    float3 top = SrgbToLinear(tex2D(TextureSampler, p.xz).rgb);
    float3 side = SrgbToLinear(tex2D(TextureSampler, abs(normal.x) > abs(normal.z) ? p.zy : p.xy).rgb);
    float up = pow(abs(normal.y), 4);
    float3 detail = lerp(side, top, up / (up + pow(1 - abs(normal.y), 4) + 1e-4));
    float3 texRgb = lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);

    return ToDisplayPremultiplied(Shade(input.WorldPosition, normal, input.OcclusionData, texRgb));
}

//BallCommon.fxh's Heartbeat and BallEmission: lub-dub, travelling along PulseDirection
float Heartbeat(float t)
{
    float phase = frac(t);
    float lubOffset = (phase - 0.10) * 13.0;
    float dubOffset = (phase - 0.29) * 15.0;

    return saturate(exp(-lubOffset * lubOffset) + 0.55 * exp(-dubOffset * dubOffset));
}

//BallCommon.fxh's DissolveNoise: a hash of the screen cell, so a dissolving ball is a dither in blocks
float DissolveNoise(float2 cell)
{
    float3 p = frac(cell.xyx * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yzx + 33.33);

    return frac((p.x + p.y) * p.z);
}

float4 BallPS(VertexOutput input, float2 screen : VPOS) : COLOR0
{
    //The dissolve first, as the desktop's balls have it: a ball appearing (+) or going (-) is a dither, settled at 0
    float dissolve = input.DissolveRipple.x;
    float noise = DissolveNoise(floor(screen / max(DissolvePixelSize, 1)));
    clip(dissolve >= 0 ? noise - dissolve : -dissolve - noise);

    float3 primary = SrgbToLinear(PatternPrimaryColor);
    float3 normal = normalize(input.WorldNormal);
    float4 shaded = Shade(input.WorldPosition, normal, input.OcclusionData, primary);
    float occlusion = SurfaceOcclusion(input.WorldPosition, normal, input.OcclusionData);

    float beat = Heartbeat(PulseTime * PulseSpeed - dot(input.WorldPosition, PulseDirection) / max(PulseWavelength, 1e-4));
    shaded.rgb += primary * EmissiveStrength * StillEmission * ((1 - PulseDepth) * occlusion * occlusion + PulseDepth * beat);

    //The ripple through the cluster (#331): a flash towards the ball's own hue, or the ceiling's alarm red
    float ripple = input.DissolveRipple.y;
    float amount = abs(ripple) * step(1e-4, RippleStrength);
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 lit = shaded.rgb + lerp(primary / max(peak, 1e-3), 1.0, 0.5) * (RippleStrength * amount);
    float3 alarmed = lerp(shaded.rgb, RippleAlarmColor * 1.7, amount * 0.95);
    shaded.rgb = ripple < 0 ? alarmed : lit;

    return ToDisplayPremultiplied(shaded);
}

technique PotatoLit
{
    pass P0
    {
        VertexShader = compile vs_3_0 PotatoVS();
        PixelShader = compile ps_3_0 LitPS();
    }
};

technique PotatoTextured
{
    pass P0
    {
        VertexShader = compile vs_3_0 PotatoVS();
        PixelShader = compile ps_3_0 TexturedPS();
    }
};

technique PotatoBall
{
    pass P0
    {
        VertexShader = compile vs_3_0 PotatoVS();
        PixelShader = compile ps_3_0 BallPS();
    }
};
