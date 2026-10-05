//GamePi's Potato tier (#789): the one effect every InstancedModelRenderer draws with on the Raspberry Pi, in place of
//Prazsky.Shaders' InstancedModel.fx - the balls, the island, the gun, the ceiling's glass, the drain, the wordmark.
//
//Not a port of InstancedModel.fx and not trying to be one: that effect is Shader Model 5.0 with 35 techniques and
//about 150 uniforms, none of which MojoShader can take, and on V3D its relief marches alone would cost more than a
//whole Potato frame. This is its LIGHTING MODEL, kept - the same three-light rig the host tints from the sky dome,
//the same hemisphere ambient and occlusion from the instance stream, the same Blinn-Phong and Fresnel environment,
//so a scene's light reads the same - with everything a surface does on top of it given up: no relief, no ball
//patterns or materials (every BallShading is one lit, coloured ball, the lava's glow aside), no shadows, no clouds, no
//refraction. The scene's own point lights it keeps (#795), at the vertex, because on the scenes whose dome is dark they
//are the light. InstancedModelRenderer recognises this effect by its techniques and draws through DrawPotato, which
//sets the uniforms below and no others but the scene's lamps (SceneLights pushes those, once a frame, for every draw);
//a uniform named here that a draw does not set keeps the value the last draw left, so DrawPotato sets every one each
//draw, as InstancedModelRenderer.Draw does on the desktop.
//
//The instance stream is InstancedModel.fx's exactly (ModelInstance, 88 bytes): the world matrix's four rows in
//TEXCOORD1-4, the occluder direction and base occlusion in TEXCOORD5, the dissolve in TEXCOORD6, the ripple in
//TEXCOORD7. Every effect here ends in PotatoOutput.fxh's ToDisplay: there is no HDR target to tonemap later.

#include "PotatoOutput.fxh"

float4x4 View;
float4x4 Projection;
float3 EyePosition;

//WHAT IS CONSTANT OVER A DRAW IS COMPUTED BY THE CPU (#804). MonoGame compiles an effect with no preshader, so an
//expression of uniforms alone written in a pixel shader runs for every PIXEL: the sRGB decode of five material colours, the
//hemisphere's two lerps, the reflectance, the roughness, a ball's crust, its emission and its ripple's flash were about a
//fifth of the 390 instructions a ball's pixel cost on the Pi's V3D (shaderdb, #801). InstancedModelRenderer.DrawPotato now
//does that arithmetic once a draw, in the same order and with the same constants, and hands over its results - so the
//uniforms below are those results, and none of them is an authored colour any more.
//
//The material, LINEAR: decoded as PotatoOutput.fxh's SrgbToLinear decodes (Hejl's fit), the diffuse premultiplied by alpha
//BEFORE the decode, as BasicEffect premultiplies on the CPU and as InstancedModel.fx's ShadePixel reads it
float4 DiffuseColor;
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

//The scene's own point lights (#795): SceneLights pushes them here under the desktop's names and sizes (its arrays are
//MaxLights long, and SetValue writes every element), linear radiance, a range where each has fallen to nothing
#define MAX_SCENE_LIGHTS 8
float3 SceneLightPosition[MAX_SCENE_LIGHTS];
float3 SceneLightColor[MAX_SCENE_LIGHTS];
float SceneLightRange[MAX_SCENE_LIGHTS];
int SceneLightCount;

//The hemisphere, multiplied out. The dome's light from a direction is lerp(Ground, Sky, y / 2 + 1 / 2), which is its
//middle plus half its range times y - so the ambient a normal takes is AmbientMid + AmbientTilt * normal.y, both already
//times the material's linear ambient colour, and what a mirror direction shows is EnvironmentMid + EnvironmentTilt * its y,
//the tilt already scaled by how sharp the surface is (a rough one reflects the dome's average, which is its middle).
float3 AmbientMid;
float3 AmbientTilt;
float3 EnvironmentMid;
float3 EnvironmentTilt;

//The Fresnel reflectance, both already times SpecularAmbientStrength: at the normal (lerp(0.04 * specular, specular,
//Metalness)), and what a grazing view adds to it (max(1, that) - that), which the fifth power of (1 - facing) scales
float3 Reflectance;
float3 ReflectanceRise;

//The floor the ground occlusion fades towards
float GroundHeight;

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

//What every ball is whatever it is made of (BallCommon.fxh) - its colour, its heartbeat, its ripple - as DrawPotato
//multiplied it out from the ball's linear colour ("primary" below is SrgbToLinear of the tint it is drawn with):
//
//  BallCrust          what the light falls on. primary for every style but the lava (#795), whose colour is its own glow:
//                     a near-black crust, primary * tint * dark + dark * (1 - tint), BallLava.fxh's LavaCrustTint and Dark.
//  BallEmissionStill  primary * EmissiveStrength * StillEmission * (1 - PulseDepth): the resting glow, which the pixel
//                     occludes squared (#303).
//  BallEmissionBeat   primary * EmissiveStrength * StillEmission * PulseDepth: what the heartbeat adds, unoccluded.
//  BallGlowStill and
//  BallGlowBeat       the lava's seams as their average over the ball (#795): its hue cut by LavaHuePower, as bright as its
//                     tint's luminance lets it, times (1 - PulseDepth) and times PulseDepth, occluded linearly. Zero for
//                     every other style.
//  BallFlash          the landing ripple (#331): lerp(primary / its peak, 1, 1/2) * RippleStrength.
//  BallAlarm          the ceiling's alarm: rgb the colour a ball is carried towards (RippleAlarmColor * 1.7), a how far a
//                     full ripple carries it (0.95). Both ripples are zero when RippleStrength is.
//  PulsePhase         xyz PulseDirection / PulseWavelength, w PulseTime * PulseSpeed: the heartbeat's phase at a point is
//                     w - dot(point, xyz), taken at the VERTEX (PotatoBallVS), its two exp with it.
float3 BallCrust = float3(1, 1, 1);
float3 BallEmissionStill;
float3 BallEmissionBeat;
float3 BallGlowStill;
float3 BallGlowBeat;
float3 BallFlash;
float4 BallAlarm;
float4 PulsePhase;

//How wide one cell of the dissolve's dither is, in back-buffer pixels: BallCommon.fxh's DissolvePixelSize, the one
//uniform the dithered draw reads (#794)
float DissolvePixelSize = 1;

//The ball's rim (#804, PotatoBallRim below): x the world units one pixel of the target spans at a clip w of 1
//(2 / (Projection._22 * the target's height)), y the sphere mesh's radius in its own units, z the share of its radius the
//mesh's outline may fall short of the true circle (1 - cos(pi / slices): the strip reaches that far inside the limb, and a
//pixel more), w the ramp's width in pixels. And the secant of half a segment of the strip, which its outer row is pushed
//out by so that the CHORD between two of its vertices still covers the ramp.
float4 RimShape = float4(0.001, 0.5, 0.01, 1);
float RimSecant = 1.02;

//The fins (#804, PotatoFinLit and PotatoFinTextured below): x and y half the target's width and height in pixels, z the
//ramp's width in pixels
float4 FinShape = float4(960, 540, 1, 0);

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
    //x the dissolve, y the ripple, z the heartbeat (0 for everything that is not a ball)
    float3 DissolveRipple : TEXCOORD3;
    float3 SceneDiffuse : TEXCOORD4;
    float3 SceneSpecular : TEXCOORD5;
};

//The normal by the world matrix's cofactor, as Common.fxh's NormalToWorld: right under a non-uniform scale and a
//mirror. The desktop's Bone is always identity (every mesh is procedural), so it is left out.
float3 NormalToWorld(float3 objectNormal, float3 r0, float3 r1, float3 r2)
{
    float3 c0 = cross(r1, r2);
    float3 normal = objectNormal.x * c0 + objectNormal.y * cross(r2, r0) + objectNormal.z * cross(r0, r1);

    return normalize(dot(r0, c0) < 0.0 ? -normal : normal);
}

//Lighting.fxh's AddSceneLights, run PER VERTEX (PotatoVS) and interpolated: per pixel, six lamps took the volcano and the
//neon city from 21 to 34 ms a frame on the Pi, and a scene with none paid 1.4 ms on Girandole for the eight branches alone.
//Unrolled, each slot its own code behind a branch on the count (uniform for the whole draw), so the array indices are
//constants: a loop indexing three arrays by its counter is the dynamic indexing mgfxc and MojoShader disagree about (see
//compile.ps1). The specular is the same arithmetic at the vertex, a broad highlight rather than a sharp one.
void AddSceneLights(float3 worldPosition, float3 normal, float3 eye, inout float3 diffuse, inout float3 specular)
{
    [unroll]
    for (int i = 0; i < MAX_SCENE_LIGHTS; i++)
    {
        [branch]
        if (i < SceneLightCount)
        {
            float3 toLight = SceneLightPosition[i] - worldPosition;
            float dist = length(toLight);
            float3 towardsLight = toLight / max(dist, 1e-4);

            //Quadratic to the light's range, as the desktop's: fades gently and dies at the edge
            float atten = saturate(1.0 - dist / SceneLightRange[i]);
            atten *= atten;

            diffuse += SceneLightColor[i] * (saturate(dot(normal, towardsLight)) * atten);

            float dotH = saturate(dot(normal, normalize(towardsLight + eye)));
            specular += SceneLightColor[i] * (pow(dotH, SpecularPower) * atten);
        }
    }
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
    output.DissolveRipple = float3(instance.Dissolve, instance.Ripple, 0);

    //The scene's own lamps (#795), at the vertex: see AddSceneLights. The side facing the eye for an open surface, as Shade
    float3 normal = normalize(output.WorldNormal);
    float3 eye = normalize(EyePosition - output.WorldPosition);
    if (TwoSidedNormals > 0 && dot(normal, eye) < 0) normal = -normal;

    output.SceneDiffuse = 0;
    output.SceneSpecular = 0;
    AddSceneLights(output.WorldPosition, normal, eye, output.SceneDiffuse, output.SceneSpecular);

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

//Lighting.fxh's SurfaceOcclusion and its constants: the instance's own occluder, and the ground's
float SurfaceOcclusion(float3 worldPosition, float3 normal, float4 occlusionData)
{
    float occlusion = saturate(occlusionData.w - 1.1 * max(0, dot(normal, occlusionData.xyz)));
    float groundProximity = saturate(1 - (worldPosition.y - GroundHeight) / 2.0);

    return saturate(occlusion - 0.55 * groundProximity * saturate(-normal.y));
}

//What a surface puts on the screen, in two parts, because the desktop adds two kinds of light. Covered is the surface
//itself over the share of the pixel it covers, premultiplied by its alpha as the material is. Added is light laid OVER
//the background whatever the surface's alpha - its EmissiveTint (the ceiling's alarm flash, which on a half-clear plate
//must still read full red) and the share of its specular and reflections SpecularAlphaWeight leaves unscaled (the
//crystal cup's glints, #228). The desktop's resolve curves the sum; folding both into one premultiplied colour and
//curving that capped the added light at the surface's alpha (#789's review).
struct Shaded
{
    float4 Covered;
    float3 Added;
    //The surface's occlusion, which a ball's emission is occluded by too
    float Occlusion;
};

//Lighting.fxh's ShadePixel without its clouds, shadows and per-surface specular: the key, fill and back
//lights, the occluded hemisphere, the specular and the Fresnel environment, in linear radiance.
Shaded Shade(float3 worldPosition, float3 rawNormal, float4 occlusionData, float3 texRgb, float3 sceneDiffuse,
    float3 sceneSpecular)
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

    //The scene's lamps, from the vertex; not scaled by the rig's strength, as on the desktop: a lamp is not the sky
    diffuse += sceneDiffuse;
    specular += sceneSpecular;

    float occlusion = SurfaceOcclusion(worldPosition, normal, occlusionData);
    float burial = saturate((0.45 - occlusionData.w) / 0.35);
    float diffuseOcclusion = lerp(0.6, 1.0, occlusion) * lerp(1.0, 0.4, burial);

    Shaded shaded;
    shaded.Occlusion = occlusion;
    shaded.Covered = float4((diffuse * DiffuseColor.rgb * diffuseOcclusion
        + (AmbientMid + AmbientTilt * normal.y) * occlusion
        + EmissiveColor) * texRgb, DiffuseColor.a);

    //The specular and the environment: the sky dome reflected, rough surfaces seeing its average. The mirror direction
    //is reflect(-eye, normal), and its height is all the dome's gradient asks of it.
    float towardsEye = dot(normal, eye);
    float3 environment = EnvironmentMid + EnvironmentTilt * (2 * towardsEye * normal.y - eye.y);

    //How squarely the surface faces the eye, never less than it turns across half this pixel (#804). The Fresnel term
    //below is the fifth power of what is left, so within the last fifth of a pixel of a silhouette it climbs to 1: a pixel
    //whose centre happens to fall there is the dome's full reflection, its neighbour along the outline is not, and the
    //edge is a broken bright line that is the sampling's and not the surface's. What a pixel shows is the surface
    //across its whole width; half its own change in facing is the middle of that, and nowhere else is it reached.
    float facing = saturate(towardsEye);
    facing = max(facing, 0.5 * fwidth(facing));

    //Schlick's fifth power, as three products: a pow of a constant exponent is a log and an exp on this GPU
    float grazing = 1 - facing;
    float grazing2 = grazing * grazing;
    float3 fresnel = Reflectance + ReflectanceRise * (grazing2 * grazing2 * grazing);
    float3 glints = (specular * SpecularColor + environment * fresnel) * occlusion;

    //The desktop scales them by lerp(1, alpha, SpecularAlphaWeight): this splits that same total into the share the
    //alpha scales (covered) and the share it does not (added)
    shaded.Covered.rgb += glints * (SpecularAlphaWeight * DiffuseColor.a);
    shaded.Added = glints * (1 - SpecularAlphaWeight) + EmissiveTint;

    return shaded;
}

//The two parts onto the 8-bit back buffer, premultiplied for BlendState.AlphaBlend: the surface curved with its own added
//light over the share it covers, and the added light curved alone over the share the background shows through. Opaque
//(the uniform alpha is 1, so the branch is the same for the whole draw) is the first term alone.
float4 ToScreen(Shaded shaded)
{
    float alpha = shaded.Covered.a;
    float3 surface = ToDisplay(shaded.Covered.rgb / max(alpha, 1e-4) + shaded.Added);

    [branch]
    if (alpha < 0.999)
        return float4(surface * alpha + ToDisplay(shaded.Added) * (1 - alpha), alpha);

    return float4(surface, 1);
}

float4 LitPS(VertexOutput input) : COLOR0
{
    return ToScreen(Shade(input.WorldPosition, input.WorldNormal, input.OcclusionData, 1, input.SceneDiffuse, input.SceneSpecular));
}

//The island's stone at a place: what the detail texture multiplies the surface by there
float3 StoneDetail(float3 worldPosition, float3 normal)
{
    //Triplanar, as the desktop's: three taps, one along each world axis, blended by the sharpened normal. Each tap is
    //sampled from its own continuous coordinates - choosing ONE side projection per pixel before sampling (the first
    //cut, two taps) left a seam where the choice flipped and a lowest-mip column along it (#789's review)
    float3 blend = normal * normal;
    blend *= blend;
    blend /= blend.x + blend.y + blend.z;
    float3 p = worldPosition * DetailScale;
    float3 detail
        = SrgbToLinear(tex2D(TextureSampler, p.zy).rgb) * blend.x
        + SrgbToLinear(tex2D(TextureSampler, p.xz).rgb) * blend.y
        + SrgbToLinear(tex2D(TextureSampler, p.xy).rgb) * blend.z;

    return lerp(float3(1, 1, 1), detail * DetailBoost, DetailStrength);
}

float4 TexturedPS(VertexOutput input) : COLOR0
{
    float3 normal = normalize(input.WorldNormal);

    return ToScreen(Shade(input.WorldPosition, normal, input.OcclusionData, StoneDetail(input.WorldPosition, normal),
        input.SceneDiffuse, input.SceneSpecular));
}

//THE FINS (#804): a ball's rim (PotatoBallRim, below) for a surface whose outline is not a formula - the island, the
//drain, the ceiling's plate, the gun. EdgeFinMesh lays a quad, collapsed, on every edge of the mesh that can ever be an
//outline; here it is opened by FinShape.z pixels, outwards on the screen, where the edge IS one from this eye, and its
//alpha falls from 1 on the edge to 0 at its far side: the coverage of a pixel by the surface, blended (after the mesh, depth-
//tested, not depth-written) over what is really beyond the edge. The mesh grows by half a pixel.
//
//WHICH EDGES OPEN, decided per vertex from the two faces that meet at the edge (EdgeFinMesh.Owner is this rule on the
//CPU, and is what the tests hold): an OUTLINE - one face towards the eye and one away - belongs to the face that shows,
//and opens away from it. A CREASE - a sharp convex edge, both faces showing - belongs to the face whose neighbour falls
//away from the edge as the eye sees it: the fin lies at the edge's own depth, over that neighbour's first pixel, and
//for a convex edge at least one of the two faces always qualifies. Every other edge stays collapsed, a triangle of no
//area that costs its vertices' positions - and on V3D, whose binning runs this shader's position half for every vertex
//it is handed, that was the whole price of the fins (+1.05 to +1.43 ms). So a draw of one instance is handed only the
//fins the CPU found could be open (EdgeFinMesh.SelectLive), and this decides again for each of those.
//
//The colour is the surface's own at the edge, by the very pixel shader the mesh is drawn with, so a fin is the last
//pixel of its face carried one pixel on.
struct FinInput
{
    //xyz this end of the edge, w the row: 0 on the edge, 1 pushed out
    float4 Position : POSITION0;
    //xyz the other end, w 1 for a crease that opens when both faces show
    float4 Other : TEXCOORD0;
    float3 FaceA : NORMAL0;
    float3 FaceB : NORMAL1;
    float3 ShadeA : TANGENT0;
    float3 ShadeB : BINORMAL0;
    float3 OutA : TANGENT1;
    float3 OutB : BINORMAL1;
};

VertexOutput PotatoFinVS(FinInput input, InstanceInput instance)
{
    VertexOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
    float3 here = mul(float4(input.Position.xyz, 1), world).xyz;
    float3 there = mul(float4(input.Other.xyz, 1), world).xyz;

    float3 toEye = EyePosition - here;
    float facingA = dot(NormalToWorld(input.FaceA, instance.WorldRow1.xyz, instance.WorldRow2.xyz, instance.WorldRow3.xyz), toEye);
    float facingB = dot(NormalToWorld(input.FaceB, instance.WorldRow1.xyz, instance.WorldRow2.xyz, instance.WorldRow3.xyz), toEye);

    //Directions of the mesh, carried as the positions are (not as normals: they lie IN their faces)
    float3 outA = mul(float4(input.OutA, 0), world).xyz;
    float3 outB = mul(float4(input.OutB, 0), world).xyz;

    bool outline = facingA * facingB < 0;
    bool crease = input.Other.w > 0.5 && facingA > 0 && facingB > 0;
    bool ownerA = outline ? facingA > 0 : dot(toEye, outB) > 0;

    float3 outward = ownerA ? outA : outB;
    float3 shade = ownerA ? input.ShadeA : input.ShadeB;

    float4 clipHere = mul(mul(float4(here, 1), View), Projection);
    float4 clipThere = mul(mul(float4(there, 1), View), Projection);
    float4 clipOut = mul(mul(float4(outward, 0), View), Projection);

    //An edge with an end behind the eye has no place on the screen to stand a fin on
    float opened = (outline || crease) && clipHere.w > 1e-3 && clipThere.w > 1e-3 ? 1.0 : 0.0;

    //The edge on the screen, in pixels, and the direction across it - of the two, the one "outward" goes on the screen
    //(the derivative of xy / w along it)
    float2 screenEdge = (clipThere.xy / max(clipThere.w, 1e-3) - clipHere.xy / max(clipHere.w, 1e-3)) * FinShape.xy;
    float2 across = float2(-screenEdge.y, screenEdge.x);
    across /= max(length(across), 1e-6);
    float2 screenOut = (clipOut.xy * clipHere.w - clipHere.xy * clipOut.w) * FinShape.xy;
    if (dot(across, screenOut) < 0) across = -across;

    //Pushed out by the ramp's width, at the edge's own depth: pixels to clip space is over half the target and times w
    float row = input.Position.w;
    output.Position = clipHere;
    output.Position.xy += across * (row * FinShape.z * opened * clipHere.w) / FinShape.xy;

    //The surface at the edge, for both rows: PotatoVS's outputs at this end
    output.WorldPosition = here;
    output.WorldNormal = NormalToWorld(shade, instance.WorldRow1.xyz, instance.WorldRow2.xyz, instance.WorldRow3.xyz);
    output.OcclusionData = instance.Custom;

    //x the coverage, which the pixel shader multiplies its whole premultiplied colour by
    output.DissolveRipple = float3(1 - row, 0, 0);

    float3 normal = output.WorldNormal;
    float3 eye = normalize(toEye);
    if (TwoSidedNormals > 0 && dot(normal, eye) < 0) normal = -normal;

    output.SceneDiffuse = 0;
    output.SceneSpecular = 0;
    AddSceneLights(here, normal, eye, output.SceneDiffuse, output.SceneSpecular);

    return output;
}

float4 FinLitPS(VertexOutput input) : COLOR0
{
    return LitPS(input) * saturate(input.DissolveRipple.x);
}

float4 FinTexturedPS(VertexOutput input) : COLOR0
{
    return TexturedPS(input) * saturate(input.DissolveRipple.x);
}

//BallCommon.fxh's Heartbeat and BallEmission: lub-dub, travelling along PulseDirection
float Heartbeat(float t)
{
    float phase = frac(t);
    float lubOffset = (phase - 0.10) * 13.0;
    float dubOffset = (phase - 0.29) * 15.0;

    return saturate(exp(-lubOffset * lubOffset) + 0.55 * exp(-dubOffset * dubOffset));
}

//A ghost's size (#794), carried in the dissolve channel below -1 (ModelInstance.GhostDissolve): the radius as a share of
//a whole ball's, 1 for anything else. The aim preview is the only ghost; it is drawn SMALLER rather than dithered, round
//the ball's own centre (the object position scaled before the world matrix takes it), which is what BallCommon.fxh's
//PatternVS does on the desktop. The owner saw this look on the Pi first (#789, where every dissolve was a size) and kept it
//for the ghost alone.
float GhostScale(float dissolve)
{
    return dissolve < -1.0 ? -dissolve - 1.0 : 1.0;
}

//A ball with no dither: the settled ones and the ghost. No clip(), which is what the Pi's frame stands on (#789): a pixel
//shader that can discard stops the GPU from rejecting a hidden pixel by depth before shading it, and on a cluster that is
//most of the balls it draws (measured: they are two thirds of a heavy level's frame, and the dissolve as a size and not a
//clip halved Girandole's). Every ball that IS dithered - a detached one, a colour cross-fade - goes in a draw of its own
//through PotatoBallDither below, which InstancedModelRenderer.DrawPotato splits off, so only those few pay for the clip.
VertexOutput PotatoBallVS(VertexInput input, InstanceInput instance)
{
    input.Position.xyz *= GhostScale(instance.Dissolve);

    VertexOutput output = PotatoVS(input, instance);

    //The heartbeat, here and not per pixel (#804): its phase turns by a tenth across a ball and its two exp were a
    //pixel's dearest instructions; between two vertices it is as good as straight
    output.DissolveRipple.z = Heartbeat(PulsePhase.w - dot(output.WorldPosition, PulsePhase.xyz));

    return output;
}

//BallCommon.fxh's DissolveNoise, verbatim: a hash with no sin in it, over cells of the screen
float DissolveNoise(float2 cell)
{
    float3 p = frac(cell.xyx * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yzx + 33.33);

    return frac((p.x + p.y) * p.z);
}

//What a ball's pixel is, display-encoded: BallPS's whole body, a function because the rim's pixels (PotatoBallRim) are the
//same ball's and must come out of the same arithmetic, or the ring would show against the ball it finishes
float3 BallColour(VertexOutput input)
{
    float3 normal = normalize(input.WorldNormal);

    //A ball is opaque, so its added light simply joins it
    Shaded parts = Shade(input.WorldPosition, normal, input.OcclusionData, BallCrust, input.SceneDiffuse, input.SceneSpecular);
    float3 shaded = parts.Covered.rgb + parts.Added;
    float occlusion = parts.Occlusion;
    float beat = input.DissolveRipple.z;

    //BallCommon.fxh's BallEmission and, for the lava (#795), BallLava.fxh's seams as their average: the resting glow
    //occluded squared and the seams' linearly, the beat's own share of each riding through. See the uniforms for what
    //each of the four is; a style with no glow has zeros in two of them.
    shaded += (BallEmissionStill * occlusion + BallGlowStill) * occlusion + (BallEmissionBeat + BallGlowBeat * occlusion) * beat;

    //The ripple through the cluster (#331): a flash towards the ball's own hue, or the ceiling's alarm red
    float ripple = input.DissolveRipple.y;
    float amount = abs(ripple);
    float3 lit = shaded + BallFlash * amount;
    float3 alarmed = lerp(shaded, BallAlarm.rgb, amount * BallAlarm.a);
    shaded = ripple < 0 ? alarmed : lit;

    return ToDisplay(shaded);
}

float4 BallPS(VertexOutput input) : COLOR0
{
    return float4(BallColour(input), 1);
}

//Testing only (#804): a ball as its flat colour, no light at all. Drawn in place of BallPS (InstancedModelRenderer.
//PotatoFlatBalls, the Game's "ballflat"), it is the floor under any cheaper ball shader: what the frame costs when a
//ball's pixel costs nothing.
float4 BallFlatPS(VertexOutput input) : COLOR0
{
    return float4(BallCrust, 1);
}

//THE BALL'S RIM (#804): the anti-aliasing of a ball's outline, without a multisampled target and without a full-screen
//pass. A ball is a sphere, so where its outline is, is a formula: after every opaque thing is in the target, one thin ring
//is drawn over each ball's outline, blended, depth-TESTED and not depth-written, whose alpha falls from 1 on the outline to
//0 one pixel outside it. That is exact coverage of the pixel by the ball (a box filter over a straight edge), blended
//against what is really behind it - which a hard-edged mesh cannot have, having already replaced it. ("Discontinuity
//edge overdraw", Sander, Hoppe, Snyder and Gortler 2001, with no silhouette to search for.) The ball grows by half a pixel.
//
//THE GEOMETRY. From an eye at distance d a sphere of radius R shows the circle where the eye's rays touch it: nearer than
//the centre by R*R/d and smaller, R*sqrt(1 - (R/d)^2), in the plane across the line of sight. The strip is two rows of
//vertices on that plane: one a pixel and the mesh's own shortfall INSIDE the limb, one the ramp's width OUTSIDE it. Every
//point of it lies at the limb's own depth, which is what makes the depth test right with no bias at all: just inside its
//outline a ball's own surface is much nearer than its limb (a pixel inside, by 0.4 of the radius for a ball 12 pixels in
//radius), so the ring is hidden behind its own ball wherever the mesh covers, and shows in the slivers an inscribed LOD
//mesh leaves short of the true circle (filling them: the outline becomes a circle whatever the mesh) and outside; and a
//neighbour's surface is in front of this ball's limb exactly where that neighbour really is in front of it.
//
//THE ALPHA is measured per pixel, not interpolated: the strip carries its place on the limb's plane in units of the limb's
//radius, the pixel shader takes that vector's length, and the ramp is a true circle whatever the strip's segment count.
//
//THE COLOUR is BallColour's, at the normal the sphere has there: its own inside the limb, and at and beyond it the one
//half a pixel inside - what the part of the ball in a partly covered pixel looks like, not the limb's own grazing normal,
//where the Fresnel term is at its peak and a ring shaded so would be an outline drawn round the ball.
//
//A ball being dithered has no ring (its outline is not an edge) and neither has one stretched off round (the shot's
//smear): both collapse the strip to a point, here, so the instance stream is the ball draw's own, as uploaded.
struct RimVertexOutput
{
    float4 Position : POSITION0;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
    float3 DissolveRipple : TEXCOORD3;
    float3 SceneDiffuse : TEXCOORD4;
    float3 SceneSpecular : TEXCOORD5;
    //xy: the place on the limb's plane, in limb radii (1 is the outline). z: the limb's radius in pixels
    float3 Rim : TEXCOORD6;
};

//corner.xy is a point of the unit circle, corner.z the row: 0 inside the limb, 1 outside it
RimVertexOutput PotatoBallRimVS(float4 corner : POSITION0, InstanceInput instance)
{
    RimVertexOutput output;

    float3 centre = instance.WorldRow4.xyz;
    float3 axes = float3(dot(instance.WorldRow1.xyz, instance.WorldRow1.xyz),
        dot(instance.WorldRow2.xyz, instance.WorldRow2.xyz), dot(instance.WorldRow3.xyz, instance.WorldRow3.xyz));
    float dissolve = instance.Dissolve;

    //1 for a ball that has a ring: round, and whole or a ghost (below -1, a size and not a cut)
    float ringed = step(abs(axes.x - axes.y) + abs(axes.x - axes.z), 0.02 * axes.x)
        * (dissolve == 0 || dissolve < -1.0 ? 1.0 : 0.0);

    float radius = RimShape.y * sqrt(axes.x) * GhostScale(dissolve);

    float3 toEye = EyePosition - centre;
    float eyeDistance = max(length(toEye), 1e-4);
    float3 view = toEye / eyeDistance;
    float sine = min(radius / eyeDistance, 0.98);
    float3 limbCentre = centre + view * (radius * sine);
    float limbRadius = radius * sqrt(1 - sine * sine);

    //Any two directions across the line of sight: which is which only turns the ring about its own centre
    float3 across = normalize(cross(view, abs(view.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
    float3 along = cross(view, across);
    float3 radial = corner.x * across + corner.y * along;

    float pixel = mul(mul(float4(limbCentre, 1), View), Projection).w * RimShape.x;
    float limbPixels = limbRadius / max(pixel, 1e-6);

    float reachPixels = corner.z > 0.5
        ? (limbPixels + RimShape.w) * RimSecant
        : max(limbPixels * (1 - RimShape.z) - 1.0, 0);
    float reach = reachPixels / max(limbPixels, 1e-4) * ringed;

    float3 ringPosition = limbCentre + radial * (limbRadius * reach);
    output.Position = mul(mul(float4(ringPosition, 1), View), Projection);
    output.Rim = float3(corner.xy * reach, limbPixels);

    //The sphere's normal half a pixel inside its limb, on this radius (see THE COLOUR), and the point of the sphere
    //that has it. The same for both rows: the strip shows only where its own ball's mesh does not, which is at the
    //limb and beyond it, and a normal that turned across the strip would dim the ring towards its inner row - a dark
    //line between the ball's own bright last pixel and the ring (seen, in the first cut).
    float inside = 1 - 0.5 / max(limbPixels, 1.0);
    float3 normal = radial * inside + view * sqrt(saturate(1 - inside * inside));

    output.WorldNormal = normal;
    output.WorldPosition = centre + normal * radius;
    output.OcclusionData = instance.Custom;
    output.DissolveRipple = float3(dissolve, instance.Ripple,
        Heartbeat(PulsePhase.w - dot(output.WorldPosition, PulsePhase.xyz)));

    output.SceneDiffuse = 0;
    output.SceneSpecular = 0;
    AddSceneLights(output.WorldPosition, normal, normalize(EyePosition - output.WorldPosition), output.SceneDiffuse,
        output.SceneSpecular);

    return output;
}

//Premultiplied, for BlendState.AlphaBlend. The coverage is 1 everywhere inside the outline (the slivers a coarse mesh
//leaves) and falls to nothing across the ramp outside it.
float4 BallRimPS(RimVertexOutput input) : COLOR0
{
    VertexOutput ball;
    ball.Position = 0;
    ball.WorldPosition = input.WorldPosition;
    ball.WorldNormal = input.WorldNormal;
    ball.OcclusionData = input.OcclusionData;
    ball.DissolveRipple = input.DissolveRipple;
    ball.SceneDiffuse = input.SceneDiffuse;
    ball.SceneSpecular = input.SceneSpecular;

    float coverage = saturate(1 - (length(input.Rim.xy) - 1) * input.Rim.z / RimShape.w);

    return float4(BallColour(ball) * coverage, coverage);
}

technique PotatoLit
{
    pass P0
    {
        VertexShader = compile POTATO_VS PotatoVS();
        PixelShader = compile POTATO_PS LitPS();
    }
};

technique PotatoTextured
{
    pass P0
    {
        VertexShader = compile POTATO_VS PotatoVS();
        PixelShader = compile POTATO_PS TexturedPS();
    }
};

technique PotatoBall
{
    pass P0
    {
        VertexShader = compile POTATO_VS PotatoBallVS();
        PixelShader = compile POTATO_PS BallPS();
    }
};

//The dithered balls (#794): the desktop's dissolve, pixel for pixel - a ball going (+d) keeps the pixels whose noise is
//above d, one arriving (-d) those below |d|, so the two draws of a cross-fade partition the ball exactly. The cell is a
//block of the SCREEN, snapped with floor so every sample in it takes the same decision; VPOS is the pixel's position in
//the back buffer, which is what SV_POSITION is in a Shader Model 4 pixel shader - and there it is the only spelling:
//the DirectX build (#808) reads the position it was handed, as the desktop's own dissolve does, where OpenGL's Shader
//Model 3 may not read a position in a pixel shader at all. Drawn whole-sized (the vertex shader scales only a ghost,
//and a ghost is never in this draw).
#if OPENGL
float4 BallDitherPS(VertexOutput input, float2 vpos : VPOS) : COLOR0
{
#else
float4 BallDitherPS(VertexOutput input) : COLOR0
{
    float2 vpos = input.Position.xy;
#endif
    float noise = DissolveNoise(floor(vpos / DissolvePixelSize));
    float dissolve = input.DissolveRipple.x;

    clip(dissolve >= 0 ? noise - dissolve : -dissolve - noise);

    return BallPS(input);
}

technique PotatoBallDither
{
    pass P0
    {
        VertexShader = compile POTATO_VS PotatoBallVS();
        PixelShader = compile POTATO_PS BallDitherPS();
    }
};

technique PotatoFinLit
{
    pass P0
    {
        VertexShader = compile POTATO_VS PotatoFinVS();
        PixelShader = compile POTATO_PS FinLitPS();
    }
};

technique PotatoFinTextured
{
    pass P0
    {
        VertexShader = compile POTATO_VS PotatoFinVS();
        PixelShader = compile POTATO_PS FinTexturedPS();
    }
};

technique PotatoBallFlat
{
    pass P0
    {
        VertexShader = compile POTATO_VS PotatoBallVS();
        PixelShader = compile POTATO_PS BallFlatPS();
    }
};

technique PotatoBallRim
{
    pass P0
    {
        VertexShader = compile POTATO_VS PotatoBallRimVS();
        PixelShader = compile POTATO_PS BallRimPS();
    }
};
