//How much of a scene point light (SceneLights) reaches a point: one copy for every loop over the slots that may hold a
//shaded lamp, the instanced effect's (InstancedModel/Lighting.fxh), the forest floor's (Forest.fx) and the plant
//material's (Acacia.fx).
//
//The quadratic falloff to the light's range every scene light has, saturate(1 - d / range)^2. And a NEGATIVE range marks a
//SHADED LAMP (#1166, the forest's old lamps on their poles, ForestLampLayout): its bulb hangs under an enamel shade, which
//lets its light out only below the shade's rim, so what stands above the bulb takes none of it. Without that the lamps lit
//the spruces' crowns over them and the wood round the clearing to its tops, and the wood's dusk went flat: the trees
//nearest a lamp take it on their trunks and lower boughs and stay dark above, as a real wood does under a street lamp.
//The cut is soft over a few degrees either side of level (LAMP_RIM_GAIN, LAMP_RIM_SPILL): the lining spills a little light
//sideways past the rim.
//
//toLight is the vector from the point to the light (not normalised), dist its length.
//
//Every loop walks the slots behind SceneLightReaches, a [branch] on exactly the points where the falloff is zero: past the
//range, and above a shaded lamp's soft cut. The forest's five lamps each reach a sector of the wood and none of the crowns
//over them, and without it every pixel of the wood paid for all five (the lamps cost 1.05 ms of the Testbed's forest frame
//without the skip and 0.59 with it, #1166; the savanna's campfires, out of reach of most of its plain, gave back 0.38). The
//same zero set, not an approximation, so no pixel moves.

static const float LAMP_RIM_GAIN = 5.0;
static const float LAMP_RIM_SPILL = 0.45;

float SceneLightFalloff(float3 toLight, float dist, float range)
{
    float atten = saturate(1.0 - dist / abs(range));
    atten *= atten;

    //toLight.y > 0 where the light stands above the point: below the shade, lit
    float below = saturate(toLight.y / max(dist, 1e-4) * LAMP_RIM_GAIN + LAMP_RIM_SPILL);
    return range < 0.0 ? atten * below : atten;
}

//Whether SceneLightFalloff is above zero at the point: inside the range, and for a shaded lamp under the cut, where
//toLight.y / dist * LAMP_RIM_GAIN + LAMP_RIM_SPILL > 0 (multiplied through by dist)
bool SceneLightReaches(float3 toLight, float dist, float range)
{
    return dist < abs(range) && (range > 0.0 || toLight.y * LAMP_RIM_GAIN + dist * LAMP_RIM_SPILL > 0.0);
}
