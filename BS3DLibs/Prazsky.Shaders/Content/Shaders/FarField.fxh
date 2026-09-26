//The far field (#551): how an open-ground scene reaches the horizon without its geometry visibly ending.
//
//Every open-ground scene draws a grid pinned to the camera, 1000-1600 units across, and until #551 every camera
//clipped at 500. Between them they cut the backdrop off in two ways the owner could see: the far plane sliced
//through whatever stood past 500 (the volcano's plain, the icesheet, the sea's horizon), and where the haze had
//already reached the dome's horizon colour, the grid's own edge left the landforms on it cut out of the sky as
//flat cards (Mars's mesas, the desert's dunes). Two things here fix both, and a scene takes both:
//
//  * THE RING. After its own grid the scene draws its terrain again over one shared mesh: a polar ring centred on
//    the ARENA (not the camera), from inside the grid's edge out to FarField.FAR_RING_OUTER, with rings spaced
//    geometrically so every cell subtends about the same angle from the arena. It is the same technique with the
//    same height function - the host only sets OriginXZ to zero, because the ring's vertices are already world
//    positions - so the land past the grid is the same land, not a painted horizon. Being fixed to the world, it
//    never swims; being fixed to the arena, it relies on every camera the Game uses standing within a hundred or so
//    units of it (FarField.FAR_RING_INNER says why that radius is enough). FarRingClip throws away the ring's
//    pixels inside the camera grid, so there is one surface everywhere and the fine one wins where both exist.
//  * THE FADE. FarFadeToSky carries the last stretch of distance to the colour the DOME is drawn in behind that
//    pixel - not to the light rig's HorizonColor, which is an average over the bottom fifth of the capture and is
//    not the colour of the sky at the horizon (on Mars it is yellow against a pink sky, which is exactly why a fully
//    hazed mesa read as a card). Fully faded ground is therefore the sky: whatever the far plane or the ring's edge
//    does beyond that distance cannot be seen. The dome's colour depends only on the direction's height, so it is
//    handed over as a short table over direction.y (SkyLightRig.FarSky) and interpolated here - the dome itself is
//    64 rings interpolated linearly, so this is the same curve, not an approximation of a different one.

#define FAR_SKY_STEPS 9

//xy: the camera grid's snapped origin; z: the half-width inside which the ring gives way to it; w: 1 while the ring
//draws, 0 while the camera grid does (then nothing is clipped).
float4 FarRing;

//x: the distance the fade to the sky begins; y: where it is complete. y <= x means no fade at all.
float2 FarFade;

//The drawn dome in linear radiance at direction.y = FAR_SKY_Y0 + i * FAR_SKY_DY (SkyLightRig.FAR_SKY_*).
float3 FarSky[FAR_SKY_STEPS];

static const float FAR_SKY_Y0 = -0.10;
static const float FAR_SKY_DY = 0.05;

void FarRingClip(float2 worldXZ)
{
    [branch]
    if (FarRing.w > 0.5)
    {
        float2 d = abs(worldXZ - FarRing.xy);
        clip(max(d.x, d.y) - FarRing.z);
    }
}

float3 FarSkyAt(float directionY)
{
    float t = saturate((directionY - FAR_SKY_Y0) / (FAR_SKY_DY * (FAR_SKY_STEPS - 1))) * (FAR_SKY_STEPS - 1);
    int i = min((int)t, FAR_SKY_STEPS - 2);
    return lerp(FarSky[i], FarSky[i + 1], t - i);
}

//Takes the scene's finished colour, haze included, the last step to the sky behind it. The scene's own haze keeps
//its authored look over the near and middle distance; this only ever acts past FarFade.x.
float3 FarFadeToSky(float3 color, float3 worldPosition)
{
    [branch]
    if (FarFade.y > FarFade.x)
    {
        float3 toPixel = worldPosition - CameraPosition;
        float dist = length(toPixel);
        float t = smoothstep(FarFade.x, FarFade.y, dist);
        color = lerp(color, FarSkyAt(toPixel.y / max(dist, 1e-3)), t);
    }

    return color;
}
