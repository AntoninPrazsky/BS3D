//How much of a scene point light (SceneLights) reaches a point: one copy for every loop over the slots that may hold a
//shaded lamp, the instanced effect's (InstancedModel/Lighting.fxh), the forest floor's (Forest.fx) and the plant
//material's (Acacia.fx). The Potato path's copy (GamePi/Shaders/PotatoModel.fx) does not read it, so no host may push a
//shaded lamp there.
//
//The quadratic falloff to the light's range every scene light has, saturate(1 - d / range)^2. And a NEGATIVE range marks a
//SHADED LAMP (#1166, the forest's old lamps on their poles, ForestLampLayout): its bulb hangs in an enamel shade, which
//lets its light out only on the open side of the shade's rim, so what stands above the bulb and behind it takes none of
//it. Without that the lamps lit the spruces' crowns over them and the wood round the clearing to its tops, and the wood's
//dusk went flat: the trees nearest a lamp take it on their trunks and lower boughs and stay dark above, as a real wood
//does under a street lamp.
//
//The shade is tipped towards the arena's axis by LAMP_TILT (ForestLampLayout.SHADE_TILT, the meshes tipped the same), so
//the rim's plane rises towards the island and the light reaches the cluster, which hangs well above the bulbs, while it
//falls away behind the pole. The shade's axis is read off the light's own position (every shaded lamp faces the axis,
//give or take its arm's few degrees of skew), so no slot carries a direction. The cut is soft over a few degrees either
//side of the rim's plane (LAMP_RIM_GAIN, LAMP_RIM_SPILL): the lining spills a little light sideways past the rim.
//
//toLight is the vector from the point to the light (not normalised), dist its length.
//
//Every loop walks the slots behind SceneLightReaches, a [branch] on exactly the points where the falloff is zero: past the
//range, and on the shut side of a shaded lamp's soft cut. The forest's five lamps each reach a sector of the wood and none
//of the crowns over them, and without it every pixel of the wood paid for all five; the savanna's campfires, out of reach
//of most of its plain, gave back about a third of a millisecond (docs/scenes/forest.md). The same zero set, not an
//approximation, so no pixel moves.

static const float LAMP_RIM_GAIN = 5.0;
static const float LAMP_RIM_SPILL = 0.45;

//sin and cos of ForestLampLayout.SHADE_TILT (25 degrees); ForestLampsTests reads them back against it
static const float LAMP_TILT_SIN = 0.42262;
static const float LAMP_TILT_COS = 0.90631;

//How far below a shaded lamp's rim plane the point stands, times dist: the light-to-point vector along the shade's axis,
//which points down and tips LAMP_TILT towards the arena's axis
float SceneLampDepth(float3 lightPosition, float3 toLight)
{
    float2 inward = -lightPosition.xz * rsqrt(max(dot(lightPosition.xz, lightPosition.xz), 1e-4));
    float3 axis = float3(inward.x * LAMP_TILT_SIN, -LAMP_TILT_COS, inward.y * LAMP_TILT_SIN);
    return -dot(toLight, axis);
}

float SceneLightFalloff(float3 lightPosition, float3 toLight, float dist, float range)
{
    float atten = saturate(1.0 - dist / abs(range));
    atten *= atten;

    //Positive below the rim, on the open side: lit
    float below = saturate(SceneLampDepth(lightPosition, toLight) / max(dist, 1e-4) * LAMP_RIM_GAIN + LAMP_RIM_SPILL);
    return range < 0.0 ? atten * below : atten;
}

//Whether SceneLightFalloff is above zero at the point: inside the range, and for a shaded lamp on the open side of the
//cut, where depth / dist * LAMP_RIM_GAIN + LAMP_RIM_SPILL > 0 (multiplied through by dist)
bool SceneLightReaches(float3 lightPosition, float3 toLight, float dist, float range)
{
    return dist < abs(range) && (range > 0.0 || SceneLampDepth(lightPosition, toLight) * LAMP_RIM_GAIN + dist * LAMP_RIM_SPILL > 0.0);
}
