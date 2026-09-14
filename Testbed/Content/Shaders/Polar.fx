//Draws a polar icesheet (#222): a flat white expanse to the horizon, carved by the wind into sastrugi, with
//a crevassed pressure ridge standing out of it at distance - and the whole picture made of ICE, which is the
//scene's real content. It is a scene variant like the desert, and it borrows that scene's machinery wholesale:
//a camera-centred CreateGridMesh grid, snapped to its own cell on the CPU so the surface does not swim,
//displaced in the vertex shader, with the base NORMAL taken PER PIXEL from the height field's gradient rather
//than interpolated from a coarse mesh (the Mach-band lesson that let the Sahara come back - see Desert.fx).
//
//WHAT MAKES IT READ AS ICE AND NOT AS "THE MOUNTAIN, FLATTER", which is the one failure #222 names:
//
//  1. WHITE IN REFLECTION, CYAN IN TRANSMISSION. That opposition is the look, and it is the whole reason the
//     scene exists: the sky reflects off the glaze as white, and what goes INTO the ice comes back out cyan,
//     deeper and greener the further it travelled. So a crevasse is not a dark slot - it is a slot that
//     GLOWS, and a wind-scoured flank is bright where it faces the sky and blue where it faces into itself.
//
//  2. THE SHADOWS ARE BLUE, and that is the answer to the trap #222 warns about and the clouds and the Moon
//     both learned the expensive way: an all-white field under ACES flattens into one tone, because every
//     lit part of it sits up where the curve has no contrast left. Snow in shadow is lit by the SKY ALONE,
//     so it is genuinely the zenith's blue rather than a grey version of the sunlit white - and once the
//     shadow carries a hue, the white has something to be white against.
//
//  3. THE SNOW IS NOT PAPER. A sparkle field (one hash per fine cell, faded out before the cells reach pixel
//     size, exactly as the desert's grain is) puts the glitter of ice crystals on the near snow. #222 asks
//     for diamond dust as this scene's own event; what is here is the GROUND half of it, the airborne
//     version being a separate overlay in the mountain's Snow.fx shape and not claimed by this pass.
//
//The terrain's shapes are the polar ones and each is wind: SASTRUGI are ridges carved ACROSS the wind and
//drawn out ALONG it, which is why the field is sampled in wind space and stretched (isotropic noise reads as
//gravel - the savanna's #117 lesson); the PRESSURE RIDGE is a belt rather than a wall across the view, so the
//expanse keeps its flat horizon in every direction but one and still has a skyline to read distance against.
//
//⚠ THE CREVASSES ARE SHADED DEEP AND DISPLACED SHALLOW, deliberately. A crevasse is metres across and tens
//deep; the grid's cell here is about 2.5 world units, and the desert's own rule is that a feature narrower
//than several cells falls between vertices and gets shaded onto a silhouette that is not there. So the
//geometry carries a broad trough and the MATERIAL carries the depth - the transmission darkens with the
//distance the light had to travel through the ice, which is what a crevasse actually looks like from above.
//
//Shared between the game and the map editor, and it borrows the scene toolkit like every other backdrop: the
//dome's gradient in linear radiance, band-limiting against the pixel footprint, and the one shared cloud
//shadow field (the editor sets no cloud uniforms, so CloudSunlight is a flat 1.0 there).

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, and the sun's own radiance. THE DIRECTION IS THE SCENE'S MOST IMPORTANT LIGHT: #222 is
//written around a sun that never climbs far off the horizon, and since #220 a dome carries its own, so a
//dusk dome lights this ice from the horizon and rakes the sastrugi the way the picture needs.
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell)
float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel reads
//as a drain into a pit rather than a bowl in flat ground. The map editor draws no island and leaves it 0.
float IslandHoleRadius;

float PolarLevelY;

//The sastrugi: how deep the wind-carved ridges are cut, how many per world unit across the wind, and how far
//they are drawn out ALONG it. The stretch is what separates a sastrugi field from a gravel field.
//
//⚠ THEY ARE A SURFACE AND NOT A TERRAIN, which is the desert's own division and was this scene's first real
//mistake: built as displacement at the scale a sastrugi actually has - a few metres across - they landed at
//about one grid cell each, so the mesh could not hold one and the field read as coarse dunes with facets on
//them. An icesheet IS flat; what the eye reads on it is texture. So the geometry carries the broad swells
//below and these are a normal perturbation with their own shading, band-limited like every other procedural
//surface here.
float DriftAmplitude;
float DriftFrequency;
float DriftStretch;

//The broad swells the geometry does carry: the long, low undulation of a sheet flowing over what is beneath it
float SwellAmplitude;

//The flat clearing the island stands in, and the band over which it rises into the open field
float ClearingRadius;
float ClearingTransition;

//The pressure ridge: how far out the front stands, how wide and how high it is, which way it lies from the
//arena (radians) and how much of the horizon it covers (radians of arc)
float RidgeRadius;
float RidgeWidth;
float RidgeHeight;
float RidgeBearing;
float RidgeSpan;

//The crevasses: how deep the geometry dips (shallow - see the header), and how far apart they run
float CrevasseDepth;
float CrevasseFrequency;

//Wall clock (seconds) and the wind, a unit direction in the XZ plane
float PolarTime;
float2 WindDirection;

//The snow's own reflectance (linear). NOT white: snow is the brightest thing in this game's world and a
//1,1,1 albedo under a sunlit dome is a surface with nowhere left to go - it clips, and every fold in it
//flattens into the same tone. A touch under one, and cool, so the sunlit white reads warm against it.
float3 SnowColor;

//What the ice carries INSIDE it (linear): the cyan the transmission converges to as the path lengthens.
//This is the scene's signature colour and the one that must not be tinted towards the snow.
float3 IceColor;

//How much of the sky's hemisphere light fills the flats
float AmbientStrength;

//How hard the glaze reflects the sun. Ice is polished where the wind has scoured it, so unlike the desert's
//broad sheen this is a tight lobe: a bright thin glare line along a flank rather than a haze over the field.
float SheenStrength;

//How much the near snow glitters, and how strong the ice's transmission is
float SparkleStrength;
float TransmissionStrength;

//World distance over which the field melts into the skyline
float HorizonHazeDistance;

//--- The icesheet -------------------------------------------------------------------------------------

//Wind space: how far along the wind, and how far across it. Every shape in this scene is one of those two,
//which is what makes them all read as the same wind having made them.
float2 WindSpace(float2 p)
{
    float2 along = WindDirection;
    float2 across = float2(-WindDirection.y, WindDirection.x);

    return float2(dot(p, along), dot(p, across));
}

//The icesheet's broad swells: the long, low undulation a sheet has where it flows over what is under it.
//THIS is what the geometry carries, and it is nearly all of what the geometry carries - see the header.
float Swells(float2 p)
{
    return GradientNoise2(p * 0.008) * 0.7 + GradientNoise2(p * 0.021 + 7.3) * 0.3;
}

//The shortest signed way round from one bearing to another, in radians. atan2 comes back on (-pi, pi], so a
//front centred near that seam would otherwise be cut in half by the arithmetic rather than by the ice.
float WrapAngle(float a)
{
    return a - 6.2831853 * floor((a + 3.14159265) / 6.2831853);
}

//The pressure ridge: where two sheets meet, the ice buckles up into a broken front. It stands at RidgeRadius
//across a SECTOR of the horizon rather than all the way round it - see the sector term below - and its
//distance wanders on a low-frequency field, or a band around the origin is a circle and reads as a fence.
float PressureRidge(float2 p)
{
    float dist = length(p);
    float bearing = atan2(p.y, p.x);

    //The wander: sampled on the belt's own circumference, so it varies along the ridge and not with distance
    float wander = GradientNoise2(float2(cos(bearing), sin(bearing)) * 3.1) * RidgeWidth * 1.4;

    //⚠ A FRONT, NOT A RING. Closed all the way round, the belt reads as an arena wall - the scene stops being
    //an expanse and becomes a room, which is the one thing #222 asks for above everything: a flat white
    //expanse TO THE HORIZON. So it covers a sector and fades out over its own edges, and the rest of the
    //circle is open ice to the skyline. The player who orbits the gun therefore gets both halves of the
    //postcard in one level rather than the same wall from every angle.
    float fromFront = abs(WrapAngle(bearing - RidgeBearing));
    float sector = 1.0 - smoothstep(RidgeSpan * 0.5, RidgeSpan * 0.5 + 0.5, fromFront);

    if (sector <= 0.0) return 0.0;

    float band = abs(dist - (RidgeRadius + wander)) / max(RidgeWidth, 1e-3);
    float crest = saturate(1.0 - band);

    //⚠ BLOCKS, NOT A CREST, and this is the difference between ice and water. Smoothed with a noise field the
    //belt photographed as a line of BREAKING WAVES - a rounded swell with a bright face - which is exactly
    //what a smooth ridge under a cyan material reads as, and the one thing this scene cannot afford, since a
    //frozen sea is the next scene over in the eye's filing system. Pressure ice is RUBBLE: slabs shoved up on
    //end, each standing at its own height with a flat top and a broken edge.
    //
    //So the belt is quantised into blocks along its own length and across its width, each raised by its own
    //hash. A block is a few grid cells across on purpose (see POLAR_GRID_N): quantising finer than the mesh
    //can hold is the crevasse's mistake from the other end - the blocks would fall between vertices and be
    //shaded onto a silhouette that is not there.
    float2 block = floor(float2(bearing * 26.0, (dist - RidgeRadius) / 22.0));
    float2 hash = NoiseHash22(block);

    //Each slab keeps its own height and its own footing, so the top edge is a broken line rather than a curve
    float height = 0.45 + 1.05 * hash.x;
    float lift = saturate(crest * (1.25 + 0.5 * hash.y));

    //A hard top with a soft foot: the slab's face is what carries the scene's colour, and a face needs an edge
    return saturate(lift * lift * (3.0 - 2.0 * lift)) * height * sector;
}

//The crevasse field: long parallel slots, opened where the sheet is stretched - here, running ACROSS the
//pressure ridge, which is where a real sheet tears. Returns 0..1, 1 in the middle of a slot.
//
//⚠ It is deliberately WIDE and shallow in the geometry: see the header for why a narrow slot cannot be held
//by this grid. The depth the eye reads comes from the transmission in PolarPS, not from the displacement.
//⚠ THE RIDGE IS PASSED IN RATHER THAN SAMPLED, and that is a measured saving and not tidiness. This field
//needs the ridge to know where the sheet is under strain, and PolarHeight needs both - so with the ridge
//sampled here, one height evaluation cost TWO ridges, the three taps the per-pixel normal takes cost six,
//and the pixel shader's own crevasse lookup two more. Handing it down takes a pixel from twelve ridge
//evaluations to four.
float CrevasseField(float2 p, float ridge)
{
    float2 w = WindSpace(p);

    //Along the wind, so they cross the sastrugi rather than lying in them
    float lines = sin(w.x * CrevasseFrequency + GradientNoise2(p * 0.012) * 2.5);

    float slot = saturate(1.0 - abs(lines) * 6.0);

    //Only where the sheet is under strain: on and just outside the pressure front. The flats stay flat -
    //an icesheet does not crack where nothing is pulling it.
    return slot * saturate(ridge * 1.6 + 0.25);
}

//The full ice height at a world point. Tapped both to displace the vertex and, thrice, for the per-pixel
//normal - one field, so the two can never drift apart.
float PolarHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float ridge = PressureRidge(p);

    float h = PolarLevelY;

    h += SwellAmplitude * ramp * Swells(p);
    h += RidgeHeight * ridge;
    h -= CrevasseDepth * CrevasseField(p, ridge);

    return h;
}

struct PolarVertexInput
{
    float4 Position : POSITION0;
};

struct PolarVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

PolarVertexOutput PolarVS(PolarVertexInput input)
{
    PolarVertexOutput output;

    float2 worldXZ = input.Position.xz + OriginXZ;
    float3 worldPosition = float3(worldXZ.x, PolarHeight(worldXZ), worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//--- The surface -------------------------------------------------------------------------------------

//The sastrugi, as the surface they are. Ridges run ACROSS the wind and are drawn out ALONG it, so the field
//is sampled on a domain stretched by DriftStretch - the anisotropy is the shape, not a decoration, and an
//isotropic field of the same amplitude reads as gravel (the savanna's #117 finding, on snow).
//
//`1 - |x|` puts a ridge at every zero crossing of the field, which is the desert's crest trick; the field is
//NOISE rather than a sine because a sine's ridges run dead straight for ever and sastrugi break, fork and
//restart. Two octaves: the long ridges and the shorter ones riding across them.
//
//Returns 0..1 and it is a HEIGHT in spirit - the normal comes off it through PerturbNormalFromHeight and the
//tone darkens in its troughs, which is what makes the field read on the faces the low sun is not raking.
//Band-limited on the standard rule, and it fades towards 0.5 (flat snow) rather than towards zero, so losing
//the ridges does not also darken the distance.
float Sastrugi(float2 p, float footprint)
{
    float f = DriftFrequency;
    float resolvable = saturate(1.0 - 2.0 * f * footprint);

    float2 w = WindSpace(p) * float2(1.0 / DriftStretch, 1.0) * f;

    float coarse = 1.0 - abs(GradientNoise2(w));
    float fine = 1.0 - abs(GradientNoise2(w * 2.7 + 11.7));

    float ridges = saturate(coarse * coarse * 0.72 + fine * 0.28);

    return lerp(0.5, ridges, resolvable);
}

float4 PolarPS(PolarVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain. 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The base normal, per pixel from the height field's gradient (three taps) - the desert's rule, and the
    //reason this terrain has no facet grid on it.
    float e = 1.2;
    float h = PolarHeight(worldPosition.xz);
    float hx = PolarHeight(worldPosition.xz + float2(e, 0.0));
    float hz = PolarHeight(worldPosition.xz + float2(0.0, e));

    float2 slope = float2(hx - h, hz - h) / e;
    float3 baseNormal = normalize(float3(-slope.x, 1.0, -slope.y));

    float sastrugi = Sastrugi(worldPosition.xz, footprint);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, sastrugi * DriftAmplitude);

    float3 towardsEye = normalize(CameraPosition - worldPosition);

    //--- Snow or ice -----------------------------------------------------------------------------------
    //Where the wind scours, the snow is gone and the glare ice is bare: the steep flanks, the pressure ridge
    //and the crevasse walls. Where it settles - the flats and the lee - there is snow. So `iceness` is the
    //slope and the crevasse field together, which is the same wind that made both.
    float steepness = saturate((1.0 - baseNormal.y) * 4.0);
    float crevasse = CrevasseField(worldPosition.xz, PressureRidge(worldPosition.xz));

    float iceness = saturate(steepness * 0.7 + crevasse * 1.2);

    //--- The light ------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);
    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light. THE SHADOW'S COLOUR IS THIS TERM, and on this scene it carries the picture: snow
    //the sun does not reach is lit by the sky alone, which is blue, and that is what keeps a white field
    //from flattening into one tone under the tonemap.
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    //The sastrugi's own shading: their troughs hold shade whatever the sun is doing. It is the cheapest
    //ambient occlusion there is, and on this scene it is load-bearing rather than a detail — under a HIGH
    //sun a flat field has a nearly constant ndotl, so the relief term says almost nothing and this is the
    //only thing left that makes the surface read as carved. Measured on the dome sweep: with it at ±0.12 the
    //field under dome 11 was a sheet of white paper.
    float3 snow = SnowColor * (0.72 + 0.42 * sastrugi) * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //--- The ice --------------------------------------------------------------------------------------
    //Reflection: the sky seen in the glaze, off the reflected direction rather than off the normal, so the
    //horizon appears in it at a grazing angle - which is where a real sheet of ice mirrors hardest.
    float3 reflected = reflect(-towardsEye, normal);
    float3 skyReflection = lerp(HorizonColor, ZenithColor, saturate(reflected.y * 0.5 + 0.5));

    //Schlick, at water/ice's own F0. The grazing rise is the whole reason an icesheet reads as glazed from a
    //low camera and as white from above.
    float fresnel = 0.02 + 0.98 * pow(saturate(1.0 - dot(normal, towardsEye)), 5.0);

    //Transmission: what went INTO the ice and came back out. The path it travelled is what colours it, so
    //the term is driven by how deep the ice is at this point - the crevasses, where the light crosses a wall
    //and comes out of the far side, are where a sheet shows what colour it really is.
    //
    //The sun's contribution is the BACK-scatter lobe: light entering the far side and leaving towards the
    //eye, which peaks when the eye looks along the sun's own direction through the ice. It is the balls'
    //TranslucencyStrength and the sea's subsurface term arriving on a surface that is made of the stuff.
    float thickness = saturate(crevasse * 1.4 + steepness * 0.35);
    float backScatter = pow(saturate(dot(towardsEye, -SunDirection)), 3.0);

    float3 inside = IceColor * (skyAmbient * 0.55 + SunColor * (0.35 + backScatter) * sunlight);
    float3 transmission = inside * thickness * TransmissionStrength;

    float3 ice = lerp(snow * 0.55 + transmission, skyReflection, fresnel);

    float3 color = lerp(snow, ice, iceness);

    //--- What sits on top of both ----------------------------------------------------------------------
    //The glare off a scoured flank: a TIGHT lobe, unlike the desert's broad sheen, because this surface is
    //polished rather than granular. Gated on ndotl so a face the sun never reaches cannot glint.
    float3 halfway = normalize(SunDirection + towardsEye);
    float glare = pow(saturate(dot(normal, halfway)), 90.0);

    color += SunColor * glare * SheenStrength * ndotl * sunlight * (0.3 + 0.7 * iceness);

    //The sparkle: ice crystals in the near snow catching the sun, one hash per fine cell. Faded out well
    //BEFORE the cells reach pixel size - a hard per-cell value is its own aliasing source, and the desert's
    //grain records what happens when that fade is finished at the cell size instead: a crawling speckle for
    //the last few metres. Only the crystals whose hash puts them near the mirror direction light up, so the
    //field twinkles as the camera moves rather than glowing evenly.
    float sparkleFade = saturate(1.0 - footprint * 90.0);
    float2 cell = floor(worldPosition.xz * 24.0);
    float2 hash = NoiseHash22(cell);
    float facet = saturate(dot(normalize(float3(hash.x - 0.5, 1.0, hash.y - 0.5)), halfway));

    color += SunColor * pow(facet, 220.0) * SparkleStrength * sparkleFade * sunlight * (1.0 - iceness);

    //The horizon: the finite grid melts into the skyline, so it has no edge and no seam with the dome. On a
    //white field this band is also the scene's depth cue - there is nothing else out there to judge it by.
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, HorizonColor, haze * haze);

    return float4(color, 1.0);
}

technique Polar
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PolarVS();
        PixelShader = compile PS_SHADERMODEL PolarPS();
    }
};
