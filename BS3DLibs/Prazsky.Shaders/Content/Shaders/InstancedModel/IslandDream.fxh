//InstancedModel.fx: THE DREAM'S ISLAND (#779) - the melting island's own material, a technique of its own selected by
//InstancedModelRenderer.DreamSurface, on the cap and the drum of IslandShape.Melt in the dream and nowhere else.
//
//The owner: "the island does not fit the scene - it spoils an otherwise good scene", and he asked for a better shader. The
//island was the stone outcrop tinted pastel, in a world of glass solids and marbled colour. Every reference (779-zimage,
//779-klein) draws the platform as SWIRLED PASTEL MARBLE with a pearly, rainbow-shifting sheen and a thin glowing ring
//round its top, so this draws exactly that, from the scene's own cosine palette (DreamSceneConfig.Palette, pushed as
//DreamPalette*) so the island indexes the same ramp as the sky, the orbs and the solids.
//
//It is a separate technique, not a branch in TriplanarPS, for the reason that technique's header gives (#155): a runtime
//branch over another shading path costs the union of both register allocations in every wavefront of an occupancy-bound
//pass, and the stone cap is in every scene. And it has NO height field: a polished stone shows no relief, the swirl is
//its structure, and the relief was 0.66 ms of the stone cap's 1.49 (#151).

float3 DreamPaletteA;
float3 DreamPaletteB;
float3 DreamPaletteC;
float3 DreamPaletteD;

//How strongly the marble's bands colour the surface (the cap 1, the drum less: a pearly mass under a marbled top)
float DreamMarble;

//The thin film's strength: the rainbow sheen a pearl or a soap film shows at a grazing angle
float DreamSheen;

//The ring inlaid in the top: (its radius from the drain's axis, its half-width, its radiance). Zero radiance on the drum.
float3 DreamRing;

//A glow of the stone's own, as a fraction of its colour: an opal's light from inside. The drum takes it and the top does
//not, because half the drum faces down into the dream's dark teal bounce and drew near black without it, where every
//reference hangs a soft lit pearl under the disc.
float DreamGlow;

//The recess under the roll: (the radius from the axis where it starts, where it is full, how much light it takes). The
//drips run down the wall with their faces parallel to it, and the dream's light is a hemisphere that shades every
//vertical face alike, so a drip read only where it bulged; with the wall behind them in shade, as it is under every
//reference's rim, the tongues stand out bright against it. By the radius, on the side faces only (the top and the
//underside are at other radii and face up or down), and the wander moves wall and drip alike, so the lobes of the wall
//that swell out into the light are lit as a lump would be.
float3 DreamRecess;

//The swirl: how fine the marble is (world units to the field's), how far the noise bends it and how many turns of the
//palette one unit of the field runs through. The bands run ROUND the drain like the references' marbled records - the
//field grows with the radius - and twist a whole number of turns round it (MARBLE_TWIST, a whole number or the colour
//would jump along one radius where the angle wraps).
static const float MARBLE_SCALE = 0.085;
static const float MARBLE_WARP = 0.8;
static const float MARBLE_RADIAL = 0.2;
static const float MARBLE_TWIST = 2.0;
static const float MARBLE_HEIGHT = 0.16;

//The veins between the bands: how many to a turn of the palette and how sharp
static const float VEIN_COUNT = 3.0;
static const float VEIN_SHARPNESS = 0.86;

//The pastel: how far the palette's colour is carried towards white, and the factor that brings the mean back to 1 so the
//renderer's albedo stays the island's brightness
static const float PASTEL_WHITE = 0.45;
static const float PASTEL_GAIN = 1.5;

float3 DreamPalette(float t)
{
    return DreamPaletteA + DreamPaletteB * cos(6.2831853 * (DreamPaletteC * t + DreamPaletteD));
}

float4 TriplanarDreamPS(VertexShaderOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);
    float3 p = input.WorldPosition;

    //The swirl's field: round the drain's axis, twisted, bent by two warped octaves of noise so a band curls and pinches
    //as poured marble does. The noise is 2D, on the plan (x, z): the top takes it whole, and down the side, where the plan
    //barely moves, the bands run straight down - the stone poured over the edge - shifted along by the height term.
    //⚠ 3D noise drew the same top and cost the play frame 0.45 ms (five taps of eight corners each).
    float radius = length(p.xz);
    float angle = atan2(p.z, p.x);
    float2 q = p.xz * MARBLE_SCALE;
    float2 bend = float2(GradientNoise2(q + float2(1.7, 9.2)), GradientNoise2(q + float2(8.3, 2.8)));
    float warp = GradientNoise2(q * 1.9 + bend * 1.6) + 0.35 * GradientNoise2(q * 4.1 + bend * 2.2 + 3.1);
    float t = radius * MARBLE_RADIAL + angle * (MARBLE_TWIST / 6.2831853) + p.y * MARBLE_HEIGHT + warp * MARBLE_WARP;

    //Pastel bands from the scene's ramp, with paler veins where two meet
    float3 hue = DreamPalette(t);
    float3 marble = lerp(hue, float3(0.92, 0.90, 0.95), PASTEL_WHITE) * PASTEL_GAIN;
    float vein = smoothstep(VEIN_SHARPNESS, 1.0, 1.0 - abs(frac(t * VEIN_COUNT) * 2.0 - 1.0));
    marble = lerp(marble, float3(1.25, 1.22, 1.28), vein * 0.6);
    float3 texRgb = lerp(float3(1, 1, 1), marble, DreamMarble);
    float recess = smoothstep(DreamRecess.x, DreamRecess.y, radius) * (1.0 - abs(worldNormal.y)) * DreamRecess.z;
    texRgb *= 1.0 - recess;

    //Polished: the reflection is sharp, which is what makes it read as glassy stone and not as paint
    SurfaceSpecular surface = DefaultSurfaceSpecular();

    float4 shaded = ShadePixel(p, worldNormal, input.OcclusionData, float4(texRgb, 1), 1, 1, surface);
    shaded.rgb += texRgb * DreamGlow;

    //The thin film: a rainbow from the same ramp, indexed by how face-on the surface is (a film's colour is its
    //thickness seen at an angle) and shifted along the marble, strongest at a grazing angle and lit by the sky it
    //mirrors. The drum takes more of it than the top: it is the pearly mass the references hang under the disc. (Half
    //the marble's field: where the angle wraps the field jumps by MARBLE_TWIST, and half of 2 is a whole turn.)
    float3 eye = normalize(EyePosition - p);
    float facing = saturate(dot(worldNormal, eye));
    float3 film = DreamPalette(facing * 1.6 + t * 0.5 + 0.15);
    float grazing = 1.0 - facing;
    shaded.rgb += film * SkyRadiance(reflect(-eye, worldNormal)) * (DreamSheen * (0.25 + 0.75 * grazing * grazing * grazing) * (1.0 - recess));

    //The ring inlaid in the top, a thread of light round the floor's edge in the palette's colours, turning twice round
    //the island. Only on the top (by the geometric normal), and nothing on the drum, whose ring radiance is 0.
    float off = (radius - DreamRing.x) / max(DreamRing.y, 1e-3);
    float ring = exp(-off * off) * saturate(worldNormal.y * 4.0 - 3.0);
    float3 ringColor = lerp(DreamPalette(angle * (2.0 / 6.2831853) + 0.2), float3(1, 1, 1), 0.35);
    shaded.rgb += ringColor * (ring * DreamRing.z);

    return shaded;
}

technique InstancedModelTriplanarDream
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TriplanarDreamPS();
    }
};
