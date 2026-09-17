//Draws the street level the city's towers stand on (#399): asphalt with lane lines, stop lines and zebra
//crossings, concrete sidewalks with a curb that rounds each corner, stone paving where a block carries no tower,
//and parked traffic - by day under the dome, by night in the neon city under sodium lamps and the shops' spill.
//Until #399 the towers ran 420 units down to nothing and the dome showed between them as bright slits.
//
//One flat quad at the city's GroundY, drawn AFTER the towers and their roofs so the depth test rejects every
//pixel a tower stands in front of before this shader runs. Nothing here is stored: the pattern is the city's
//own block grid evaluated per pixel (BlockPitch, StreetWidth - the same two numbers City lays the blocks out
//on, so a street can never run through a tower), and the one thing the grid cannot say - whether a block has
//a tower on it at all - comes from a small occupancy texture, one texel per block.
//
//That texture also stands in for the shadows this project does not draw. A street between two towers 100 units
//tall and 9 wide sees a few percent of the sky and almost no sun; left fully lit the canyon floors glowed like
//the rooftops. Sampled bilinearly (and once more a block out each way) it gives how built-up the neighbourhood
//of a pixel is, and that density scales the sky and the sun the pixel receives.
//
//Built by all three executables out of this directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//--- The grid (the city's own; see City.BlockPitch) ------------------------------------------------------------
float GroundY;
float BlockPitch;
float StreetWidth;
float RadiusBlocks;

//One texel per block, red = 1 where the block carries a tower. Two samplers over it: POINT for the block a pixel
//is in, LINEAR for the neighbourhood density the canyon light is taken from.
Texture2D OccupancyTexture;
sampler OccupancyPoint = sampler_state
{
    Texture = <OccupancyTexture>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};
sampler OccupancyLinear = sampler_state
{
    Texture = <OccupancyTexture>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//--- The look (CityStreetsConfig) -------------------------------------------------------------------------------
float SidewalkWidth;
float CornerRadius;
float3 AsphaltColor;
float3 SidewalkColor;
float3 PlazaColor;
float3 CurbColor;
float3 MarkingColor;
float AmbientStrength;
float CanyonSkyView;
float CanyonSunView;
float FacadeBounce;
float HazeDistance;
float CarChance;
float3 TreeColor;
float TreeChance;

//The neon city: 1 there, 0 by day. Not a branch - the night terms are cheap and the day ones are multiplied out.
float Neon;
float3 NeonAmbient;
float3 LampColor;
float LampSpacing;
float NeonSpill;
float3 NeonMagenta;
float3 NeonCyan;
float3 NeonHazeColor;

struct StreetVertexInput
{
    float4 Position : POSITION0;
};

struct StreetVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

StreetVertexOutput StreetVS(StreetVertexInput input)
{
    StreetVertexOutput output;

    float3 world = float3(input.Position.x, GroundY, input.Position.z);
    output.WorldPosition = world;
    output.Position = mul(mul(float4(world, 1.0), View), Projection);

    return output;
}

//0..1 (NoiseHash22 itself is signed)
float Hash12(float2 p)
{
    return NoiseHash22(p).x * 0.5 + 0.5;
}

//1 where x lies in [a, b], softened across one pixel's footprint so an edge never aliases
float Band(float x, float a, float b, float footprint)
{
    return saturate(min(x - a, b - x) / footprint + 0.5);
}

//1 inside a shape whose signed distance is d (negative inside), with the same one-pixel softening
float Inside(float d, float footprint)
{
    return saturate(0.5 - d / footprint);
}

//A square wave of the given period and duty (the lit fraction), box-filtered over the pixel's footprint: the
//integral of the wave across the footprint divided by its width. It resolves to the stripes up close and to
//the duty - their average - once a pixel covers several periods, which is what keeps a crossing's zebra from
//turning into moire across the far streets.
float StripeIntegral(float t, float period, float duty)
{
    float cycles = floor(t / period);
    return cycles * duty * period + min(t - cycles * period, duty * period);
}

float Stripes(float x, float period, float duty, float footprint)
{
    float w = max(footprint, 1e-3);
    return (StripeIntegral(x + 0.5 * w, period, duty) - StripeIntegral(x - 0.5 * w, period, duty)) / w;
}

//Joints of a square slab grid: 1 on the mortar line, faded out once the lines are under a pixel apart
float SlabJoints(float2 p, float slab, float joint, float footprint)
{
    float2 d = abs(frac(p / slab + 0.5) - 0.5) * slab;
    float mortar = max(Inside(d.x - joint, footprint), Inside(d.y - joint, footprint));
    return mortar * saturate(1.0 - footprint / (slab * 0.35));
}

//The body of a car painted on the road, seen from above: the paint, a dark windscreen and rear window, a
//pale roof between them. `along` runs nose to tail. Returns the colour; `mask` is how much of the pixel it covers.
float3 CarBody(float2 local, float3 paint, float footprint, out float mask)
{
    const float halfLength = 1.3;
    const float halfWidth = 0.6;

    float2 q = abs(local) - float2(halfLength, halfWidth) + 0.25;
    float body = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - 0.25;
    mask = Inside(body, footprint);

    //Glass across the car at both ends of the cabin, the roof between them
    float glass = (Band(local.x, 0.35, 0.85, footprint) + Band(local.x, -0.95, -0.6, footprint))
        * Band(local.y, -halfWidth + 0.12, halfWidth - 0.12, footprint);

    return lerp(paint, float3(0.012, 0.014, 0.018), saturate(glass) * saturate(1.0 - footprint * 2.0));
}

//The cars' paints (linear albedo): white, black, silver, dark blue, red, taxi yellow, dark grey
static const float3 CAR_PAINTS[7] =
{
    float3(0.62, 0.62, 0.60),
    float3(0.018, 0.018, 0.02),
    float3(0.30, 0.31, 0.32),
    float3(0.02, 0.035, 0.10),
    float3(0.30, 0.02, 0.02),
    float3(0.75, 0.50, 0.02),
    float3(0.07, 0.07, 0.075)
};

float4 StreetPS(StreetVertexOutput input) : COLOR
{
    float3 world = input.WorldPosition;
    float2 xz = world.xz;

    //One pixel's reach on the ground, per axis and then the larger: the grazing axis is the one that aliases
    float2 dx = ddx(xz);
    float2 dy = ddy(xz);
    float footprint = max(max(abs(dx.x) + abs(dy.x), abs(dx.y) + abs(dy.y)), 1e-3);

    //--- Where on the grid ----------------------------------------------------------------------------------
    float2 block = floor(xz / BlockPitch + 0.5);
    float2 local = xz - block * BlockPitch;
    float halfBuildable = (BlockPitch - StreetWidth) * 0.5;
    float halfPaved = halfBuildable + SidewalkWidth;

    float blocks = 2.0 * RadiusBlocks + 1.0;
    float2 blockUv = (block + RadiusBlocks + 0.5) / blocks;
    float inCity = step(max(abs(block.x), abs(block.y)), RadiusBlocks);
    float built = tex2Dlod(OccupancyPoint, float4(blockUv, 0, 0)).r * inCity;

    //The paved square of the block, its corners rounded where the curb turns: negative inside
    float2 q = abs(local) - (halfPaved - CornerRadius);
    float pavedDistance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - CornerRadius;
    float paved = Inside(pavedDistance, footprint);

    //A street segment's own frame: `across` from the street's centre line, `along` it from the middle of the
    //block. Chosen by which axis leaves the buildable square, which switches only under the sidewalk or inside
    //the crossing, where nothing below is drawn - so the switch never cuts a marking.
    float streetAlongZ = step(abs(local.y), abs(local.x));
    float across = lerp(local.y - sign(local.y) * BlockPitch * 0.5, local.x - sign(local.x) * BlockPitch * 0.5, streetAlongZ);
    float along = lerp(local.x, local.y, streetAlongZ);
    float inCrossing = step(halfBuildable, abs(local.x)) * step(halfBuildable, abs(local.y));
    float inSegment = (1.0 - inCrossing) * (1.0 - paved);

    float halfRoad = StreetWidth * 0.5 - SidewalkWidth;

    //--- The road ----------------------------------------------------------------------------------------------
    //Two scales of mottling and a darker line where tyres run, both band-limited by the footprint
    float mottle = Fbm2BandLimited(xz * 0.45, 3, footprint * 0.45);
    float3 asphalt = AsphaltColor * (0.88 + 0.22 * mottle);
    float laneCentre = halfRoad * 0.5;
    float tyres = Band(abs(abs(across) - laneCentre), 0.35, 0.75, footprint) * inSegment;
    asphalt *= 1.0 - 0.14 * tyres * saturate(1.0 - footprint);

    //The gutter: a darker line of grime along the curb
    asphalt *= 1.0 - 0.35 * Band(pavedDistance, 0.0, 0.3, footprint);

    //Paint. Worn: every marking is broken up by the same mottling rather than drawn solid.
    float wear = saturate(0.75 + 0.5 * Fbm2BandLimited(xz * 2.3, 2, footprint * 2.3));
    float centreLine = Band(across, -0.07, 0.07, footprint) * Stripes(along + 100.0, 3.0, 0.5, footprint)
        * Band(abs(along), -1.0, halfPaved - CornerRadius - 3.6, footprint);
    float crossingDepth = abs(along);
    float zebra = Stripes(across + 100.25, 1.0, 0.5, footprint)
        * Band(crossingDepth, halfPaved - CornerRadius - 2.6, halfPaved - CornerRadius + 0.2, footprint)
        * Band(abs(across), -1.0, halfRoad - 0.15, footprint);
    //The stop line only across the lanes that approach the crossing - traffic keeps to the right
    float approaching = step(0.0, across * sign(along));
    float stopLine = Band(crossingDepth, halfPaved - CornerRadius - 3.3, halfPaved - CornerRadius - 2.95, footprint)
        * Band(abs(across), 0.1, halfRoad - 0.15, footprint) * approaching;
    float marking = saturate(centreLine + zebra + stopLine) * inSegment * wear;
    float3 road = lerp(asphalt, MarkingColor, marking);

    //A manhole in each segment, somewhere down one lane
    float2 segmentId = block * 2.0 + float2(streetAlongZ, 1.0 - streetAlongZ) * sign(local);
    float manholeAlong = (Hash12(segmentId + 7.1) - 0.5) * 8.0;
    float manholeSide = sign(Hash12(segmentId + 3.3) - 0.5) * laneCentre;
    float manhole = Inside(length(float2(along - manholeAlong, across - manholeSide)) - 0.38, footprint) * inSegment;
    road = lerp(road, AsphaltColor * 0.55, manhole * saturate(1.0 - footprint * 2.0));

    //--- Traffic --------------------------------------------------------------------------------------------------
    //Three slots per lane between the crossings. Each rolls whether it holds a car and what paint, and the car
    //sits a little off the slot's centre. The lane decides the direction it faces, which only the night uses.
    float lane = step(0.0, across);
    float slot = clamp(floor(along / 3.4 + 0.5), -1.0, 1.0);
    float2 slotId = segmentId * 5.0 + float2(lane, slot);
    float carRoll = Hash12(slotId + 1.7);
    float hasCar = step(carRoll, CarChance) * inSegment * inCity;
    float carCentre = slot * 3.4 + (Hash12(slotId + 9.2) - 0.5) * 0.5;
    float facing = lane * 2.0 - 1.0;
    float2 carLocal = float2((along - carCentre) * facing, across - (lane * 2.0 - 1.0) * laneCentre);
    float3 paint = CAR_PAINTS[(int)min(Hash12(slotId + 4.4) * 7.0, 6.0)];
    float carMask;
    float3 car = CarBody(carLocal, paint, footprint, carMask);
    carMask *= hasCar;

    //Past a few pixels a car is a smudge of its own average, not an aliasing rectangle
    float carResolved = saturate(1.6 - footprint * 1.5);
    road = lerp(road, lerp(road * 0.8, car, carResolved), carMask);
    //The ground under and around a car is darker - a contact shadow, soft and cheap
    float carShadow = Inside(length(max(abs(carLocal) - float2(1.1, 0.45), 0.0)) - 0.55, footprint + 0.4) * hasCar * (1.0 - carMask);
    road *= 1.0 - 0.3 * carShadow;

    //--- The paving ------------------------------------------------------------------------------------------------
    //A built block's sidewalk is square concrete slabs; a block with no tower is a square in larger stone, with a
    //darker border course. The curb is a lighter band along the edge either way.
    float slabTone = 0.93 + 0.14 * Hash12(floor(local / lerp(3.0, 1.5, built)) + block * 13.0);
    float joints = SlabJoints(local, lerp(3.0, 1.5, built), 0.035, footprint);
    float3 sidewalk = lerp(PlazaColor, SidewalkColor, built) * slabTone * (1.0 - 0.28 * joints);
    float border = Band(pavedDistance, -1.6, -0.9, footprint) * (1.0 - built);
    sidewalk *= 1.0 - 0.18 * border;
    float curb = Band(pavedDistance, -0.3, 0.0, footprint);
    sidewalk = lerp(sidewalk, CurbColor, curb);

    //Trees in the squares, one per cell of a TREE_SPACING grid, kept a crown's width inside the curb. Seen from
    //above a tree is its crown: a disc shaded as a dome towards the sun, broken into clumps, over the shadow it
    //throws. The cell is wide enough that a crown, its jitter and its shadow never reach the next cell's edge.
    const float TREE_SPACING = 7.0;
    float2 treeCell = floor(local / TREE_SPACING + 0.5);
    float2 treeSeed = treeCell + block * 17.0;
    float treeRoll = Hash12(treeSeed);
    float treeRadius = 1.1 + 0.4 * Hash12(treeSeed + 2.9);
    float2 treeCentre = treeCell * TREE_SPACING + NoiseHash22(treeSeed + 8.3) * 0.4;
    float2 treeQ = abs(treeCentre) - (halfPaved - CornerRadius);
    float treeCurbDistance = length(max(treeQ, 0.0)) + min(max(treeQ.x, treeQ.y), 0.0) - CornerRadius;
    float hasTree = step(treeRoll, TreeChance) * step(treeCurbDistance, -treeRadius - 1.2) * (1.0 - built) * inCity;

    float2 fromTree = local - treeCentre;
    float crown = Inside(length(fromTree) - treeRadius, footprint) * hasTree;
    float2 dome = fromTree / treeRadius;
    float3 crownNormal = float3(dome.x, sqrt(saturate(1.0 - dot(dome, dome))), dome.y);
    float leaves = 0.8 + 0.4 * Fbm2BandLimited(local * 1.7 + block * 5.0, 2, footprint * 1.7);
    float3 treeAlbedo = TreeColor * leaves * (0.55 + 0.6 * saturate(dot(crownNormal, SunDirection)));

    float2 shadowReach = SunDirection.xz / max(SunDirection.y, 0.25) * 3.0;
    shadowReach *= min(1.4 / max(length(shadowReach), 1e-3), 1.0);
    float treeShadow = Inside(length(fromTree + shadowReach) - treeRadius, footprint + 0.5) * hasTree * (1.0 - crown);
    sidewalk *= 1.0 - 0.45 * treeShadow;

    float3 albedo = lerp(road, sidewalk, paved);
    albedo = lerp(albedo, treeAlbedo, crown * paved);

    //Past the city's last block the grid has nothing to explain it: the ground there fades to its own average
    float outskirts = saturate((max(abs(xz.x), abs(xz.y)) / BlockPitch - RadiusBlocks - 0.5) / 2.0);
    albedo = lerp(albedo, lerp(AsphaltColor, SidewalkColor, 0.45), outskirts);

    //--- The canyon's light -----------------------------------------------------------------------------------------
    //How built-up the neighbourhood is: the bilinear occupancy at the pixel, and the same a block out each way
    float2 cityUv = (xz / BlockPitch + RadiusBlocks + 0.5) / blocks;
    float2 blockStep = float2(1.0 / blocks, 0.0);
    float density = 0.5 * tex2Dlod(OccupancyLinear, float4(cityUv, 0, 0)).r
        + 0.125 * (tex2Dlod(OccupancyLinear, float4(cityUv + blockStep.xy, 0, 0)).r
            + tex2Dlod(OccupancyLinear, float4(cityUv - blockStep.xy, 0, 0)).r
            + tex2Dlod(OccupancyLinear, float4(cityUv + blockStep.yx, 0, 0)).r
            + tex2Dlod(OccupancyLinear, float4(cityUv - blockStep.yx, 0, 0)).r);
    density *= 1.0 - outskirts;

    //Right at a tower's foot the wall takes half the sky again: darker towards the buildable square of a built block
    float buildableDistance = max(abs(local.x), abs(local.y)) - halfBuildable;
    float footShade = lerp(1.0, lerp(0.55, 1.0, saturate((buildableDistance + 2.0) / 5.0)), built);
    //A crossing is open along both streets
    float openness = lerp(1.0, 1.6, inCrossing);

    float skyView = min(lerp(1.0, CanyonSkyView, density) * openness, 1.0) * footShade;
    float sunView = lerp(1.0, CanyonSunView * openness, density * density);

    float sunlight = CloudSunlight(world, SunDirection);
    float sunUp = saturate(SunDirection.y);

    float3 dayLight = ZenithColor * AmbientStrength * skyView
        + SunColor * sunUp * sunlight * sunView
        + SunColor * sunUp * FacadeBounce * density * footShade;

    //--- The night --------------------------------------------------------------------------------------------------
    //Sodium lamps along every curb, as two lattices (one per street direction) so a pool never meets a seam
    float2 cell = frac(xz / BlockPitch) * BlockPitch;
    float2 toCurb = min(abs(cell - (BlockPitch * 0.5 - halfRoad - 0.4)), abs(cell - (BlockPitch * 0.5 + halfRoad + 0.4)));
    float2 toLamp = abs(frac(xz / LampSpacing + 0.5) - 0.5) * LampSpacing;
    float poolAlongZ = exp(-(toCurb.x * toCurb.x + toLamp.y * toLamp.y) / 3.2);
    float poolAlongX = exp(-(toCurb.y * toCurb.y + toLamp.x * toLamp.x) / 3.2);
    float3 lamps = LampColor * saturate(poolAlongZ + poolAlongX) * inCity;

    //The shops' signs spill their colour onto the sidewalk at a tower's foot, magenta or cyan by block, broken
    //along the frontage. Gone by the middle of the street, where the next block's colour begins.
    float spillReach = saturate(1.0 - (buildableDistance + 0.5) / 3.5) * saturate(buildableDistance + 2.5);
    float3 spillColor = lerp(NeonMagenta, NeonCyan, step(0.5, Hash12(block + 21.7)));
    float frontage = saturate(0.5 + 1.2 * GradientNoise2(xz * 0.35 + block * 3.7));
    float3 spill = spillColor * NeonSpill * spillReach * spillReach * frontage * built * step(Hash12(block + 5.9), 0.7);

    float3 nightLight = NeonAmbient + lamps + spill;

    float3 color = albedo * lerp(dayLight, nightLight, Neon);

    //Head- and tail-lights at night, as small glows at either end of each car, and the beam on the road ahead.
    //Kept under GLARE_THRESHOLD: a glow a few pixels across is exactly what the glare's sparse taps catch on some
    //frames and miss on others (#401 was that, in the sky).
    float2 lightOffset = float2(abs(carLocal.x) - 1.25, abs(abs(carLocal.y) - 0.38));
    float lampGlow = exp(-dot(lightOffset, lightOffset) / 0.06) * hasCar;
    float3 carLights = lerp(float3(0.45, 0.02, 0.01), float3(0.48, 0.44, 0.34), step(0.0, carLocal.x)) * lampGlow;
    float2 beamOffset = float2(carLocal.x - 3.2, carLocal.y);
    float beam = exp(-(beamOffset.x * beamOffset.x / 3.2 + beamOffset.y * beamOffset.y / 0.5)) * hasCar * (1.0 - carMask);
    color += Neon * (carLights + albedo * float3(1.2, 1.05, 0.8) * beam * 1.5);

    //--- Distance -------------------------------------------------------------------------------------------------------
    //Only past the city's last block. The towers take no haze, and a street under a haze the towers standing on
    //it are not in reads as a veil over the ground: the first build faded from the eye, and at the canyon shot's
    //130 units it laid a tenth of the horizon's radiance over asphalt that reflects a twentieth of the light.
    float eyeDistance = length(world - CameraPosition);
    float cityReach = (RadiusBlocks + 0.5) * BlockPitch;
    float haze = 1.0 - exp(-max(eyeDistance - cityReach, 0.0) / HazeDistance);
    color = lerp(color, lerp(HorizonColor, NeonHazeColor, Neon), haze);

    return float4(color, 1.0);
}

technique CityStreets
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL StreetVS();
        PixelShader = compile PS_SHADERMODEL StreetPS();
    }
};
