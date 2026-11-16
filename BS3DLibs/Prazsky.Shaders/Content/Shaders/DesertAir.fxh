//The desert's air (#1111): the murk the far erg fades into and how much of it a distance takes, in one copy for the dunes
//(Desert.fx) and the foot of the pyramids that stand on them (Pyramid.fx). Where the sand meets the stone the two must be
//hazed alike, or the join shows: the pyramids' own air is lighter, and a foot in it stands dark and saturated behind dune
//crests the murk has already made pale. (The stone does not take the dunes' drifting dust veil, Desert.fx's CloudNoise: at
//most DustStrength of it, and from HorizonHazeDistance out the murk covers it whole.)
//
//Declare HorizonColor, SandColor, HazeWarmth and HorizonHazeDistance before including it.

static const float3 DESERT_LUMA = float3(0.2126, 0.7152, 0.0722);

//The warm murk the far erg fades into (see HazeWarmth): the horizon's brightness in the sand's hue, leaned towards by
//HazeWarmth. Not HorizonColor itself: under dome 13 that uniform is teal while the dome draws its horizon lilac-grey, and
//the far dunes stood against the sky as a teal band that matched nothing.
float3 DesertMurk()
{
    float horizonLuma = dot(HorizonColor, DESERT_LUMA);
    float3 sandHue = SandColor / max(dot(SandColor, DESERT_LUMA), 1e-3);
    return lerp(HorizonColor, sandHue * horizonLuma, HazeWarmth);
}

//How much of the murk the sand takes at a distance: none at the lens, all of it from HorizonHazeDistance out, eased in
//as a square so the near and middle erg keep their light
float DesertHaze(float dist)
{
    float haze = saturate(dist / HorizonHazeDistance);
    return haze * haze;
}
