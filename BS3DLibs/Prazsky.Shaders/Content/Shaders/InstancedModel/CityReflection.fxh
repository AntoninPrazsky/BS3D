//InstancedModel.fx: the city's window reflections (#1065) - InstancedCityReflect, the towers' program with each pane
//mirroring the tower across the street where it mirrored only the sky. The owner: the windows should shine by
//reflecting the buildings round them, sharply, on High and Ultra.
//
//Included LAST: its uniforms close the constant buffer, so no other technique's layout moves (InstancedModel.fx's
//first rule), and CityShade, which it builds on, is in scope above it. A technique of its own and not a branch in
//CityPS, which is occupancy-bound: a runtime switch would cost every tier the union of both programs' registers.

#if OPENGL

//Not on OpenGL ES: the march is a dynamic loop over a texture, the desktop's. The technique is the towers' own
//program, so a renderer that selects it draws the city exactly as InstancedCity does (and the map's uniforms are not
//declared at all - GL drops what no program reads, and the renderer looks each one up null-safe).
technique InstancedCityReflect
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CityVS();
        PixelShader = compile PS_SHADERMODEL CityPS();
    }
};

#else

//The city's towers seen from above, a texel CityReflectTexel square (CityReflectionMap.cs, built once per city):
//(r) the highest roof over the texel, or the street level where no tower stands; (g) that tower's building id,
//floor(anchor * 0.37) as CityPS takes it from Style.zw, both parts packed in one float; (b) its facade-kind roll;
//(a) how many texels away the nearest tower texel is (Chebyshev; 0 on one). Point-sampled: a texel is a fact about
//one tower, and a blend of two is neither.
texture CityReflectMap;
sampler2D CityReflectSampler = sampler_state
{
    Texture = <CityReflectMap>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

float2 CityReflectOrigin;   //world xz of the map's outer corner (texel (0, 0)'s)
float2 CityReflectSize;     //texels across x and z
float CityReflectTexel;     //world units a texel
float CityReflectTop;       //the highest roof: a ray climbing past it meets nothing more
float CityReflectGround;    //the street level
float CityReflectStrength;  //how strongly a pane mirrors the tower it faces: 1 is glass (Fresnel alone), 0 is off
float CityReflectReach;     //how far from the lens a pane mirrors the towers; past it, only the sky
float CityReflectSteps;     //how many steps (a jump or a texel each) a mirror ray takes before it keeps the sky

//The loop's own bound, which CityReflectSteps stops inside
#define CITY_REFLECT_MAX_STEPS 64

//CityReflectionMap.ID_BIAS and ID_SPAN: the packed id is (x + BIAS) * SPAN + (z + BIAS)
#define CITY_REFLECT_ID_BIAS 512.0
#define CITY_REFLECT_ID_SPAN 1024.0

//How much more a glass curtain wall mirrors than a punched window (#678's kind, Style.x in [0.5, 0.68)): a curtain wall's
//panes are coated, and the owner's picture was of towers that were mirrors. Its mirror is capped at a whole one
#define CITY_REFLECT_CURTAIN_COATING 4.0

//Marches the mirror ray from a pane through the map. 1 and the face it meets, or 0 where it climbs over the roofs, leaves
//the map, comes down to the street or runs out of steps: the pane keeps the sky there.
//
//⚠ Two kinds of step. Through the empty texels the ray JUMPS as far as the nearest tower allows (the map's .a, a texel
//short of it), so a street is crossed in a step or two and a grazing ray down a street, where glass mirrors most, in a
//handful; a texel at a time it could not reach across the street at all. Beside a tower it steps a TEXEL at a time (a 2D
//DDA), so the face it enters is known: a jump never lands in a tower, because a move of (a - 1) texels from anywhere in
//a texel ends in one whose index is less than a from it.
float CityReflectMarch(float3 origin, float3 faceNormal, float3 dir, out float3 hitPosition, out float3 hitNormal,
    out float4 hitTexel)
{
    hitPosition = 0;
    hitNormal = 0;
    hitTexel = 0;

    //Out from the pane a texel and a half along its normal before the first look, so the march starts outside the
    //pane's own tower. A ray running almost along the facade never gets out of it, and keeps the sky.
    float away = dot(dir.xz, faceNormal.xz);
    if (away < 0.02) return 0;
    float3 start = origin + dir * (CityReflectTexel * 1.5 / away);

    float2 perUnit = dir.xz / CityReflectTexel;                 //texels crossed a unit of the ray, each axis
    float across = max(max(abs(perUnit.x), abs(perUnit.y)), 1e-5);
    float2 tPerTexel = 1.0 / max(abs(perUnit), 1e-5);
    float2 stepDir = float2(perUnit.x >= 0 ? 1.0 : -1.0, perUnit.y >= 0 ? 1.0 : -1.0);
    float axis = abs(faceNormal.x) > 0.5 ? 0.0 : 1.0;
    float t = 0;

    [loop]
    for (int i = 0; i < CITY_REFLECT_MAX_STEPS; i++)
    {
        if (i >= CityReflectSteps) return 0;

        float3 p = start + dir * t;
        if (p.y < CityReflectGround || (dir.y >= 0 && p.y > CityReflectTop)) return 0;

        float2 g = (p.xz - CityReflectOrigin) / CityReflectTexel;
        float2 cell = floor(g);
        if (cell.x < 0 || cell.y < 0 || cell.x >= CityReflectSize.x || cell.y >= CityReflectSize.y) return 0;

        float4 texel = tex2Dlod(CityReflectSampler, float4((cell + 0.5) / CityReflectSize, 0, 0));

        //Far from every tower: jump, a texel short of the nearest
        if (texel.a >= 2.0)
        {
            t += (texel.a - 1.0) / across;
            continue;
        }

        //Into a tower's side, entered across `axis` by the step before
        if (texel.r > p.y && texel.r > CityReflectGround)
        {
            hitPosition = p;
            hitNormal = axis < 0.5 ? float3(-stepDir.x, 0, 0) : float3(0, 0, -stepDir.y);
            hitTexel = texel;
            return 1;
        }

        //A texel step: to where the ray leaves this texel
        float2 toEdge = float2(stepDir.x > 0 ? cell.x + 1.0 - g.x : g.x - cell.x,
            stepDir.y > 0 ? cell.y + 1.0 - g.y : g.y - cell.y) * tPerTexel;
        float tStep = min(toEdge.x, toEdge.y);
        float yExit = p.y + dir.y * tStep;

        //Down onto the roof inside the texel
        if (dir.y < 0 && texel.r > yExit && texel.r > CityReflectGround)
        {
            hitPosition = p + dir * ((texel.r - p.y) / dir.y);
            hitNormal = float3(0, 1, 0);
            hitTexel = texel;
            return 1;
        }

        axis = toEdge.x < toEdge.y ? 0.0 : 1.0;
        t += tStep + 1e-3;
    }

    return 0;
}

//The face the mirror ray met: the tower's own tone and kind of facade, its windows on a world grid at the kind's pitch
//(the tower's real grid is laid out on boxes the map does not keep; in a pane's mirror nobody counts), each lit or
//dark by its own hash and the lit ones in the tower's lamp or neon, under the sky and the sun. A roof is the wall's
//stone with no windows. `footprintCells` is the pixel's extent at the hit in cells: past one, the windows are their
//average, the rule every feature of the city is held to.
float3 CityReflectedFacade(float3 p, float3 n, float4 texel, float footprintWorld)
{
    float idX = floor(texel.g / CITY_REFLECT_ID_SPAN);
    float2 buildingId = float2(idX, texel.g - idX * CITY_REFLECT_ID_SPAN) - CITY_REFLECT_ID_BIAS;
    float facadeKind = texel.b;
    float side = 1.0 - step(0.5, n.y);

    float2 fill = float2(WindowFillX, WindowFillY);
    float2 pitch = float2(WindowPitchX, WindowPitchY);
    fill = facadeKind < 0.5 ? fill : (facadeKind < 0.68 ? float2(0.9, 0.84) : (facadeKind < 0.84 ? float2(0.97, 0.5) : float2(0.34, 0.88)));
    pitch.x *= facadeKind < 0.5 ? 1.0 : (facadeKind < 0.68 ? 0.85 : (facadeKind < 0.84 ? 1.0 : 0.8));

    float2 along = float2(abs(n.x) > 0.5 ? p.z : p.x, p.y - CityReflectGround);
    float2 grid = along / pitch;
    float2 cell = floor(grid);
    float2 within = abs(frac(grid) - 0.5) * 2.0;
    float window = step(within.x, fill.x) * step(within.y, fill.y) * side;

    HASH seed = WindowSeed(buildingId, cell);
    float lit = step(1.0 - WindowLitFraction, WindowRoll(seed, HASH_SALT(floor(HashBits(seed, 20u, 12u) * 37.0))));
    float3 lamp = lerp(WindowWarm, WindowCool, step(0.5, HashBits(seed, 11u, 1u)));
    float3 facadeColor = FacadeColor * (1.0 + FacadeColorVariation * (BuildingRoll(buildingId, 1u) - 0.5) * 2.0);

    //The neon city's tower colours, as CityPS picks them (a uniform branch, as there)
    [branch]
    if (CityNeon > 0.0)
    {
        float3 neonMagenta = float3(1.0, 0.04, 0.85);
        float3 neonCyan = float3(0.05, 0.85, 1.0);
        float pickBuilding = BuildingRoll(buildingId, 2u);
        float3 buildingNeon = pickBuilding < 0.45 ? neonMagenta : (pickBuilding < 0.9 ? neonCyan : HueToRGB(BuildingRoll(buildingId, 3u)));
        float3 contrast = buildingNeon.r > buildingNeon.b ? neonCyan : neonMagenta;
        lamp = lerp(lamp, lerp(buildingNeon, contrast, step(0.83, HashBits(seed, 3u, 8u))), CityNeon);
        facadeColor = lerp(facadeColor, FacadeNeonColor, CityNeon);
    }

    float resolvable = saturate(1.0 - max(footprintWorld / pitch.x, footprintWorld / pitch.y));
    float glassShare = lerp(fill.x * fill.y * side, window, resolvable);
    float litShare = lerp(fill.x * fill.y * WindowLitFraction * side, window * lit, resolvable);

    float3 light = SkyRadiance(n) * SrgbToLinear(AmbientColor)
        + DirLight0DiffuseColor * (saturate(dot(n, normalize(KeyLightPosition - p))) * DirLightStrength);
    return lerp(facadeColor, WindowGlassColor, glassShare) * light + litShare * lamp * CityWindowBrightness;
}

float4 CityReflectPS(CityVSOutput input) : COLOR
{
    CityReflectInputs facade;
    float4 shaded = CityShade(input, facade);

    //Only where there is glass to mirror it. A divergent branch (the panes and the wall between them share a
    //wavefront), with no gradient inside: the march reads its texels at an explicit level, and the footprint it
    //needs was taken by CityShade outside it
    float eyeDistance = max(distance(EyePosition, input.WorldPosition), 1.0);

    [branch]
    if (facade.Glass > 0.01 && eyeDistance < CityReflectReach)
    {
        float3 eye = normalize(EyePosition - input.WorldPosition);
        float3 dir = reflect(-eye, facade.Normal);

        float3 hitPosition, hitNormal;
        float4 hitTexel;
        if (CityReflectMarch(input.WorldPosition, facade.Normal, dir, hitPosition, hitNormal, hitTexel) > 0)
        {
            //The pixel's extent at the hit: the mirror ray spreads as the eye's does, so a far tower in a pane is as
            //coarse as the pane is small
            float travelled = distance(hitPosition, input.WorldPosition);
            float3 met = CityReflectedFacade(hitPosition, hitNormal, hitTexel, facade.Footprint * (1.0 + travelled / eyeDistance));

            //What ShadePixel mirrored for this pane, the sky along the same ray, swapped for the tower it meets, over the
            //same roughness and Fresnel: a pane head-on shows little and one at a grazing angle a whole facade, as glass
            //does. ⚠ The tower is mirrored as GLASS mirrors (Fresnel alone), and only the sky it replaces keeps the city's
            //dimmer (SpecularAmbientStrength, 0.07, times WindowReflectionBoost): that dimmer exists so a bright sky does
            //not bleach a thousand grazing facades, and a tower, darker than the sky by day, cannot. Through the dimmer the
            //tower was a hundredth of itself and could not be seen at all.
            float roughness = saturate(lerp(1.0, sqrt(2.0 / (SpecularPower + 2.0)), WindowSmoothness));
            float3 linearSpecular = SrgbToLinear(SpecularColor);
            float3 fresnel = FresnelSchlick(lerp(DielectricF0 * linearSpecular, linearSpecular, Metalness),
                dot(facade.Normal, eye), WindowSmoothness);
            float3 sky = SkyRadiance(dir);

            //Faded out over the last third of the reach, so where the march stops is not a line across the city
            float near = saturate(3.0 - 3.0 * eyeDistance / CityReflectReach);
            float3 mirror = fresnel * ((1.0 - roughness) * facade.Glass * facade.Cavity * near);
            float coating = facade.Kind >= 0.5 && facade.Kind < 0.68 ? CITY_REFLECT_CURTAIN_COATING : 1.0;
            shaded.rgb += met * min(mirror * (coating * CityReflectStrength), 1.0)
                - mirror * (sky * (SpecularAmbientStrength * WindowReflectionBoost));
        }
    }

    return shaded;
}

technique InstancedCityReflect
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CityVS();
        PixelShader = compile PS_SHADERMODEL CityReflectPS();
    }
};

#endif
