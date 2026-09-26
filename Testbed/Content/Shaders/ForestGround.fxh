//The forest floor, which the daytime forest and the aurora's night forest stand on (#581). The aurora is the
//forest at night - the same clearing, the same trees planted by the same CPU mirror - so its ground has to be
//this ground, and until #581 it was a copy: "identical to Forest.fx's TerrainHeight, kept in ONE change with
//it" by comment, which is exactly the promise #579 found broken on NeedleRelief within days. One copy now.
//Each includer declares the uniforms it reads (ClearingRadius, ClearingTransition, ClearingRelief,
//FloorLumpFrequency, FloorLumpStrength, ForestLevelY, HillHeight, NeedleReliefFrequency and
//NeedleReliefStrength) before including it, and Noise.fxh for Fbm2BandLimited.

//Rolling hills behind the trees, low around the arena centre (world origin) and rising into wooded hills
//with distance, so the clearing is flat where the arena stands and rolls up towards the treeline. Kept in
//ONE change with TerrainMirror.Forest, its CPU mirror - the scatter plants trees on this.
float TerrainHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    //A domain warp bends the octaves' straight wavefronts before they are summed. Without it the hills
    //read as a regular swell - the "row of smooth mounds" the first build had - because summed plane
    //waves keep their planes however many there are; the warp is what breaks the planes themselves. It
    //is two long sines, so the CPU mirror stays exact.
    float2 q = p + 26.0 * float2(sin(p.y * 0.011 + 2.0), sin(p.x * 0.013 + 5.0));

    //Five octaves rather than three, amplitudes summing to 1 so the ramp's height stays the authored
    //HillHeight: the two added octaves fill the gap between hill and lump scales, which is exactly the
    //band a "smooth blob with bumps on it" is missing.
    float rolling = 0.40 * sin(dot(q, float2(0.020, 0.015)))
        + 0.26 * sin(dot(q, float2(-0.013, 0.024)) + 1.5)
        + 0.17 * sin(dot(q, float2(0.031, 0.026)) + 3.0)
        + 0.10 * sin(dot(q, float2(0.056, -0.041)) + 0.7)
        + 0.07 * sin(dot(q, float2(-0.083, 0.062)) + 2.4);

    float basin = ClearingRelief * sin(dot(p, float2(0.05, 0.035)));

    //Floor lumps are present even inside the clearing - the uneven ground the trees stand on - and fade
    //out with the rolling hills so the distant hills stay smooth (their detail is the trees, not the
    //floor). Three waves under a broad mask: unmasked, two sines interfere into an even weave across the
    //whole floor, which reads as a pattern rather than as ground; masked, the roughness comes in patches
    //the way roots and hollows do.
    float mask = 0.55 + 0.45 * sin(dot(p, float2(0.021, -0.017)) + 4.0);
    float lumps = sin(dot(p, float2(FloorLumpFrequency, FloorLumpFrequency * 0.7)))
        + 0.5 * sin(dot(p, float2(-FloorLumpFrequency * 0.8, FloorLumpFrequency * 1.1)) + 2.0)
        + 0.35 * sin(dot(p, float2(FloorLumpFrequency * 1.9, FloorLumpFrequency * 1.4)) + 5.1);
    float lumpHeight = FloorLumpStrength * lumps * mask * (1.0 - ramp * 0.5);

    return ForestLevelY + basin + lumpHeight + HillHeight * ramp * (rolling * 0.5 + 0.5);
}

//The fine litter texture, band-limited PER OCTAVE against the footprint (the ball relief's rule: one global
//fade has to be tuned for the finest octave and flattens the lot at arm's length, while per-octave fades let
//each drop out exactly where the pixels stop resolving it). A needle floor is fine, sharp and directionless.
//⚠ A FOREST FLOOR DOES NOT DRIFT (#276). This sampled at `xz + WindDirection * ForestTime * 0.7` — the same
//flat 0.7 world units a second the meadow and the savanna carried, character for character, and the same
//fault in all three: the floor's own texture slid across the ground for ever. Grass at least bends, so those
//two lean by the gust field now; needles, moss and leaf litter do neither, they simply lie there. So the
//field is STATIC here and the wind reads on this scene where it actually would — in the light coming
//through the canopy, which the gust darkens further down.
float NeedleRelief(float2 xz, float footprint)
{
    //Isotropic noise, not sines (#281). This was four plane waves at fixed angles, and summed plane waves keep
    //their planes however many there are: the owner's playtest read the floor as "lines and waves", which is
    //exactly what it drew. A litter of needles is directionless, so the relief is band-limited gradient noise
    //whose octaves are rotated against each other (Fbm2BandLimited), and each octave fades where the pixels
    //stop resolving it.
    return Fbm2BandLimited(xz * NeedleReliefFrequency, 3, footprint * NeedleReliefFrequency) * NeedleReliefStrength;
}

//The terrain's base normal, per pixel from the height field's own gradient - the savanna's fix, and the
//reason it exists here too: interpolating a coarse displaced grid's per-vertex normal leaves a Mach band
//at every cell edge, and on this scene it read as the whole floor being slightly out of focus. Three taps
//per pixel; the grid keeps the silhouette, the gradient does the shading.
float3 TerrainNormal(float2 p)
{
    float e = 1.2;
    float h = TerrainHeight(p);
    float hx = TerrainHeight(p + float2(e, 0.0));
    float hz = TerrainHeight(p + float2(0.0, e));

    return normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));
}
