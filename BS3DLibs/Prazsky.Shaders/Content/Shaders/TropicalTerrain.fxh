//THE TROPICAL BEACH'S LAND (#968): its profile's uniforms and its height, in one copy for the two shaders that need it.
//Tropical.fx draws the land off it; Sea.fx's lagoon reads the same bed under the water, so the shallows know how deep
//they are and the waterline is where the land says it is. TerrainMirror.Tropical on the CPU mirrors TropicalHeight
//term for term (mirrorcheck); keep the three in one change.

//The land's profile (Tropical.fx's header): the dry sand's level and undulation, the waterline's mean radius
//and wiggle, the beach's rise and run either side of it, the lagoon bed, and the far shore ridge's
//radius, wiggle, width, height, channel and hills.
float TropicalLevelY;
float ClearingRelief;
float ShoreRadius;
float CoastNoise;
float BeachRise;
float BeachRun;
float SeabedY;
float RingRadius;
float RingNoise;
float RingWidth;
float HillHeight;
float ChannelBearing;
float ChannelSharpness;

//The lagoon's mean level. The terrain shader reads it to know where the waterline is (the wet sand band, and the
//sand fringe on the far shore), and the lagoon's own shore its depth (Sea.fx; its surface is SeaLevelY, the same value).
float WaterLevelY;

//The waterline's radius at a bearing: the mean wobbled by three sine octaves (integer multipliers of
//the bearing, so the wobble comes back round the circle continuous). The weights sum to 1, so
//CoastNoise is the full peak wiggle in world units.
float CoastRadius(float b)
{
    return ShoreRadius + CoastNoise * (0.45 * sin(2.0 * b + 0.7)
        + 0.35 * sin(3.0 * b + 1.3)
        + 0.20 * sin(5.0 * b + 4.1));
}

//The far shore's own coastline, on its own octaves and phases so the two coasts share no lines.
float ShoreRingRadius(float b)
{
    return RingRadius + RingNoise * (0.40 * sin(2.0 * b + 2.9)
        + 0.34 * sin(3.0 * b + 0.6)
        + 0.26 * sin(7.0 * b + 3.4));
}

//The one channel through the far ridge: a cosine bump of the bearing, sharp enough to read as a pass
//rather than a dip, that takes the ridge's rise back out of the ground inside it. pow(max(0, cos))
//is continuous and zero almost everywhere — no pole, no seam, and the open sea shows through to the
//horizon in the gap.
float ChannelMask(float b)
{
    return pow(max(0.0, cos(b - ChannelBearing)), ChannelSharpness);
}

//The land's height at a world point. Sines and hermite ramps only — TerrainMirror.Tropical on the CPU
//mirrors this term for term to plant the scatter, and a gradient noise in here would plant palms in
//the air the day the two drifted. Keep the two in one change.
float TropicalHeight(float2 p)
{
    float r = length(p);
    float b = atan2(p.y, p.x);

    //The dry sand's gentle undulation — a beach is not a snooker table. Amplitudes are kept well under
    //the sand's height above water, so no dip ever puddles below a waterline it is not at.
    float gentle = ClearingRelief * 0.5 * (sin(p.x * 0.043 + p.y * 0.031)
        + 0.6 * sin(-p.x * 0.052 + p.y * 0.046 + 2.1));

    //The beach, as two hermite ramps meeting at the coast: the first takes the flat sand down THROUGH
    //the water level exactly at the coast radius (the waterline is where the coast is, by construction
    //rather than by tuning), the second carries the slope on to the bed. Past the second ramp the
    //ground is under the lagoon's own colour (Sea.fx's shore shows only the shallows, #968).
    float d = r - CoastRadius(b);

    float toWaterline = smoothstep(-BeachRise, 0.0, d);
    float toBed = smoothstep(0.0, BeachRun, d);

    float h = lerp(TropicalLevelY + gentle, WaterLevelY, toWaterline);
    h = lerp(h, SeabedY, toBed);

    //The far shore ridge, rising out of the bed past its own wiggling coastline, scaled by rolling
    //hills and pierced by the one channel. The hills are DOMAIN-WARPED sines — summed plane waves keep
    //their planes however many terms they have, and the warp is what bends their wavefronts into
    //ridges (the forest's own trick, and exact to mirror for the same reason everything else here is:
    //a warp by a sine is still a sine).
    float ring = smoothstep(0.0, RingWidth, r - ShoreRingRadius(b)) * (1.0 - ChannelMask(b));

    float qx = p.x + 26.0 * sin(p.y * 0.011 + 2.0);
    float qz = p.y + 26.0 * sin(p.x * 0.013 + 5.0);

    float rolling = 0.40 * sin(qx * 0.020 + qz * 0.015)
        + 0.27 * sin(-qx * 0.013 + qz * 0.024 + 1.5)
        + 0.19 * sin(qx * 0.031 + qz * 0.026 + 3.0)
        + 0.14 * sin(-qx * 0.056 + qz * 0.041 + 0.7);

    h += ring * HillHeight * (0.55 + 0.45 * (0.5 + 0.5 * rolling));

    return h;
}
