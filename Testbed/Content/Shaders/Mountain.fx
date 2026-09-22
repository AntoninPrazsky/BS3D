//Draws a snowy mountain range: a basin the arena sits in, ringed by craggy snow-capped peaks rising with
//distance into an alpine haze. Fifth scene variant (NumPad2); the island stands in the basin and snow
//falls over it (Snow.fx).
//
//A camera-centred grid (360 a side, drawn through a 32-bit index buffer - see CreateGridMesh; a 16-bit one
//wrapped at 65k vertices and stretched the far peaks into dark bands across the sky) carries the massing,
//its per-vertex finite-difference normal interpolated as the smooth base. On top of it a fine ROCK RELIEF
//perturbs the normal per pixel (band-limited against the footprint) so the rock faces read as rough stone,
//not flat shading. And the snow is no longer a clean slope line: it lies by slope AND altitude, with an
//irregular, noisy snowline and patchy rock beneath, so the range reads as rock with snow on it rather than
//one white blanket. Camera-centred grid snapped to its cell so it does not swim. Shader Model 5.0.
//
//What decides how the peaks read is the FIELD, not the grid - see MountainField, and #86, which was filed
//against the tessellation and turned out to be about the field the grid was oversampling ninefold.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
//The sun's cast shadows (#469, in this scene since #471): SceneRenderer renders the map before the scene
//pass and this reads it. See Shadows.fxh for what casts and what a receiver owes.
#include "Shadows.fxh"

//The shared noise library, for the ridged fractal field the massing is built from. Its opening states this
//scene's own defect: a sum of plane-wave sines keeps its planes however many terms it has, and that is what
//the range was made of until #86.
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin, so the drain funnel below
//the island reads as a drain into a pit rather than a bowl in flat ground (the flat clearing otherwise slices
//across the funnel just below its rim, hiding its depth and swallowing the balls falling through). The Testbed
//sets this to the island's radius; the map editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

float MountainLevelY;
float MountainHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

//Snow (near white) and two bare-rock tones (dark and a lighter grey-brown), varied in patches. The
//surface-normal Y band the snow lies over (flat/gentle keeps snow, steep sheds it), and the altitude band
//the snowline sits in (below it is rock, above it snow) - both linear.
float3 SnowColor;
float3 RockColor;
float3 RockColorLight;
float RockSlope;
float SnowSlope;
float SnowlineLow;
float SnowlineHigh;

//Fine rock relief: a normal-tilting height field on the rock faces, its amplitude and features per world unit
float RockReliefStrength;
float RockReliefFrequency;

float AmbientStrength;
float HorizonHazeDistance;

//#509's companion pass on the range (#504), from references: how much snow the fall-line flutes hold against
//the ribs between them, and the altitude band a low sun's light climbs through - below AlpenglowLow a slope is
//in the shadow of the range across the basin, above AlpenglowHigh it takes the whole sun.
float FluteSnow;
float AlpenglowLow;
float AlpenglowHigh;

//How far apart the first octave's ridges run, in world units; each octave after it halves that. Five puts
//the finest at about 8 units, which the vertex grid's 3.34-unit cell (MOUNTAIN_EXTENT / (MOUNTAIN_GRID_N - 1))
//still carries - a crest the grid cannot hold does not come out finer, it comes out jagged.
static const float MOUNTAIN_RIDGE_SPACING = 130.0;
static const int MOUNTAIN_FIELD_OCTAVES = 5;

//RidgedFbm2 sits high and narrow: measured over the annulus the peaks occupy, mean 0.70 with a standard
//deviation of 0.13, against the plane-wave field's 0.36 and 0.17. Used raw it therefore lifts the whole
//basin and leaves shallow dips in it - a white blanket, not a range. Dropping the floor out and cubing what
//is left puts the terrain back on the height distribution the rest of the scene is tuned against (median
//0.30 and p95 0.67, against the old field's 0.35 and 0.66, so MountainHeight and the snowline both stand),
//and the curve is the ridged look itself: valleys flatten, crests keep their height. Measured slope bears
//that out - the median falls from 0.77 to 0.67 while the p99 rises from 2.48 to 3.72.
static const float MOUNTAIN_FIELD_FLOOR = 0.24;
static const float MOUNTAIN_FIELD_SPAN = 0.72;

//Ridged mountain field, roughly [0,1]: a network of crests with the detail gathered onto them, big enough in
//its first octave to mass the range and fine enough in its last to break the skyline.
//
//This was four octaves of 1-|sin(dot(p, direction))| until #86, and both of that construction's failures
//were on the screen at once. Plane waves keep their planes: every face carried the same long straight
//corrugations, which read as sand ripples rather than rock. And the finest octave's ridges ran about thirty
//units apart - so an eighty-two-unit peak had three features from base to summit and its outline against the
//sky was a smooth simple cone. Nothing about that was the tessellation's fault: the vertex grid samples every
//3.34 units and was oversampling the field ninefold. Noise.fxh's header calls this exact construction out and
//exists to replace it; the mountain was the scene that never got converted.
float MountainField(float2 p)
{
    float ridged = RidgedFbm2(p / MOUNTAIN_RIDGE_SPACING, MOUNTAIN_FIELD_OCTAVES);
    float shaped = saturate((ridged - MOUNTAIN_FIELD_FLOOR) / MOUNTAIN_FIELD_SPAN);

    return shaped * shaped * shaped;
}

//MASSIFS (#504). Every range the references drew has a hierarchy - a few dominant massifs, lower sections
//and saddles between them - where the ridged field alone draws a skyline of crests all of one height, a saw
//round the whole basin. One octave of gradient noise at several ridge spacings scales the field's height
//between MASSIF_LOW and MASSIF_HIGH, with a mean near one so MountainHeight and the snowline keep meaning what
//they were tuned to mean. Vertex work only - three taps a vertex - and nothing on the CPU mirrors this height.
static const float MASSIF_SPACING = 300.0;
static const float MASSIF_LOW = 0.35;
static const float MASSIF_HIGH = 1.6;

//The terrain displacement at a world XZ: a gentle basin around the arena (world origin) rising into peaks
//with distance. Evaluated three times per vertex for the finite-difference normal.
float TerrainHeight(float2 p)
{
    float dist = length(p);
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

    float basin = ClearingRelief * (sin(dot(p, float2(0.06, 0.04))) + 0.6 * sin(dot(p, float2(-0.05, 0.08)) + 2.0));
    float massif = lerp(MASSIF_LOW, MASSIF_HIGH, saturate(0.5 + 0.8 * GradientNoise2(p / MASSIF_SPACING + 7.3)));

    return MountainLevelY + basin + MountainHeight * ramp * massif * MountainField(p);
}

//THE FLUTES (#504). Every steep face the references drew is striped straight down: ribs of dark rock and
//couloirs of snow, the pattern water, rockfall and avalanches cut. So the snow's facing threshold is shifted by
//a field that varies fast ALONG the face and slowly DOWN it, which draws exactly those stripes.
//
//⚠ Not by combing the rock relief down the fall line, which was the first cut. A combed domain is rotated about
//the world's origin, so where the direction changes - and a fall line changes at every facet of the grid - the
//sampled coordinate jumps by the distance from the origin times the angle, and the faces came out covered in
//zig-zag chevrons that followed the mesh. This never rotates anything: one noise over (x, y) and one over
//(z, y), each stretched down the world's Y, blended by which way the face looks - the one that runs along the
//face wins. A face at 45 degrees shows both faintly, which reads as rock.
//
//⚠ PER VERTEX, and that is the whole of its price. In the pixel shader the two noise taps cost 0.9 ms of a
//10.0 ms frame (23 Mpix, the reference desktop) and still 0.3 behind a data branch on steep ground - this is
//the scene the project already calls marginal at 4K, and an occupancy-bound pass keeps the registers a branch
//skips. A couloir is coarse enough for the grid to carry: at FLUTE_SPACING 10 the 3.34-unit cell puts three
//vertices across a period, the field is interpolated to the pixel, and the snow's sharp edge is drawn per
//pixel off the interpolated value. What that costs is a slightly polygonal couloir edge up close.
static const float FLUTE_SPACING = 10.0;  //world units between one couloir and the next, roughly
static const float FLUTE_STRETCH = 5.0;   //how many times longer a flute is down the face than across it

float Flutes(float3 worldPosition, float3 baseNormal)
{
    float3 q = worldPosition / FLUTE_SPACING;
    float alongX = GradientNoise2(float2(q.x, q.y / FLUTE_STRETCH));
    float alongZ = GradientNoise2(float2(q.z + 17.1, q.y / FLUTE_STRETCH));

    float2 facing = abs(baseNormal.xz) + 1e-4;
    float flute = lerp(alongX, alongZ, facing.x / (facing.x + facing.y));

    //Only on the steep ground they belong to.
    return flute * saturate((1.0 - baseNormal.y) * 3.0);
}

struct MountainVertexInput
{
    float4 Position : POSITION0;
};

struct MountainVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float Flute : TEXCOORD2;          //the couloirs' field, per vertex (see Flutes)
};

MountainVertexOutput MountainVS(MountainVertexInput input)
{
    MountainVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float height = TerrainHeight(xz);

    //Base normal per vertex (finite differences) and interpolated, not per pixel. On this mostly-distant,
    //steep range a per-pixel finite-difference normal aliases into shimmer on the far faces; the smooth
    //per-vertex normal reads cleaner and the finer grid plus the per-pixel rock relief carry the detail.
    float e = 2.0;
    float hx = TerrainHeight(xz + float2(e, 0.0));
    float hz = TerrainHeight(xz + float2(0.0, e));
    output.WorldNormal = normalize(float3(-(hx - height) / e, 1.0, -(hz - height) / e));

    float3 worldPosition = float3(xz.x, height, xz.y);
    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);
    output.Flute = Flutes(worldPosition, output.WorldNormal);

    return output;
}

//Rough rock texture, and it is FRACTAL NOISE and not crossed sines - which it was until #170, the third and
//last instance of that defect this project has found. #86 took MountainField's own massing off the same
//construction and #117 did the same for the savanna's GrassRelief; this was the one left, and #140 even added
//a fourth sine ONTO it rather than replacing it.
//
//A family of plane waves keeps its planes however many terms it has (Noise.fxh's own header says it), so the
//four octaves - periods 2*pi/f, i.e. about 10.5, 5.5, 2.9 and 1.5 world units at RockReliefFrequency's 0.6 -
//interfered into a legible PLAID over the snow. Captured before it was touched: a cross-hatch in the interior
//of the snowfield, which is also what separates it from snowNoise below (that one would wobble the snow's
//EDGE, not lay a lattice over its middle). It reached the snow because the slope term that scales this floors
//at 0.4 rather than at zero on up-facing ground - see the call site - so flat snow took 40 % of it, and a
//relief feeds the NORMAL, which is the strongest way to make a pattern visible.
//
//Four octaves of ROTATED gradient noise instead: no shared axis, so there is no lattice to read. COMBED along
//one axis (Fbm2Combed), because isotropic noise has no grain and a relief without one reads as gravel rather
//than as rock lying in beds - the direction the dominant sine used to supply for free, kept as ROCK_GRAIN.
//Four and not the two or three the report suggested: the finest sine was added to reach crag scale, and it is
//the fourth octave that still reaches it (coarsest ~10.5 world units down to ~1.3, the old spectrum's span).
static const float2 ROCK_GRAIN = float2(0.9, 0.4);
static const float ROCK_STRETCH = 2.4;
static const float TWO_PI = 6.2831853;

//The amplitude the swap had to buy back, and it is a GAIN here rather than a new RockReliefStrength - #117's
//own choice in the savanna, and for a reason this scene has too: levels pin that figure in their scene config
//(two of the shipped ones do, and a hand-built level nobody has seen may), so moving it would leave them all
//holding a number that now means a fifth of the relief.
//
//About three, and both halves of it are real. The four sines' weights (0.46/0.28/0.16/0.10) put their RMS near
//0.41 where four octaves of Fbm2BandLimited come to about 0.20 - gradient noise spends its time well inside
//its extremes where a sine sits near them - and for equal amplitude and period a sine is half again as steep,
//which matters because it is the field's SLOPE and not its height that tilts the normal. Confirmed by eye
//against the old field from the same camera: the relief reads as strongly as it did, and irregularly.
//
//⚠ SIX since #504, applied once, where it was three applied twice: #208 split the normal's perturbation in two
//and so tilted the lit normal by this relief TWICE from 2026-08-25 on (see the call site). The range has been
//seen at that doubled relief ever since, and the references ask for more rock detail, not less - so the figure
//that was on the screen is kept, and the second PerturbNormalFromHeight a pixel is not.
static const float ROCK_FBM_GAIN = 6.0;

//The authored frequency is CONVERTED rather than reinterpreted, and that is the one thing here that could
//quietly change the look: the config's figure is a sine's angular frequency (period 2*pi/f) where a noise
//field wants a domain scale (a feature is about 1/scale across), so it goes in divided by 2*pi and the
//coarsest feature stays exactly where it was authored. Passing f straight in would make every feature 6.3x
//finer and take the broad undulation out of the terrain.
float RockRelief(float2 xz, float footprint, uniform int octaves)
{
    float scale = RockReliefFrequency / TWO_PI;

    //The footprint goes in scaled by the same figure, which is Fbm2BandLimited's contract: it wants the
    //pixel's size in the domain's own units, and that is what fades each octave out as its period approaches
    //the pixel - the job RockOctave's own `resolvable` guard used to do for the sines.
    return Fbm2Combed(xz * scale, ROCK_GRAIN, ROCK_STRETCH, octaves, footprint * scale)
        * (RockReliefStrength * ROCK_FBM_GAIN);
}

//How sharp the snow's edge is (#504), as half the width of the band either side of a threshold's middle that
//the edge is drawn over - see the call site.
static const float SNOW_EDGE = 0.16;

//THE SNOW'S OWN SURFACE, which #208 found missing: the snowfields were flat SnowColor under a 40 % share of
//the ROCK relief, and rock beside them carried grain, patches and per-pixel hash while the snow carried
//nothing - the range read as "beautiful peaks with airbrushed white on them". Two things, both snow-only and
//both band-limited against the footprint like everything else in this shader, because a relief that reaches
//pixel size checkerboards (#170's whole lesson) and a glint that does is a crawling speckle:
//
//⚠ THE DRIFT RELIEF IS GONE (#296), and this is the record of what it was and what it cost. It was
//WIND-STREAKED SASTRUGI: combed fbm like the rock's but on its own grain, crossed to it, stretched harder
//and half its strength - three octaves, snow-only, band-limited. It was the single most expensive term in
//this shader and it was very nearly invisible: measured on the reference desktop at 3840x1600, ssaa 2, it
//cost 1.08 ms of a 10.99 ms frame - close to a TENTH of the mountain's whole frame - and photographed on
//and off from a mid-range camera over the snowfields the two frames cannot be told apart.
//
//It went because the owner asked for something to be given up at High, with the menu in front of him: the
//sparkle 0.20 ms, the rock's fourth octave 0.31, this 1.08. Best saving, least seen. The cut is
//all-or-nothing on purpose - 3 octaves to 2 saved only 0.27 and to 1 only 0.63, the occupancy signature
//this project keeps meeting: partial cuts buy proportionally less, and only removing the call crosses back
//over the threshold. If the character is ever wanted back, ONE octave is the 0.63 ms option.
//
//What #208 was actually complaining about survives: it found the snowfields flat SnowColor under a 40 %
//share of the rock relief, with no surface of their own. They still have the SPARKLE, which is the half a
//photograph of snow is recognised by, and they still take that share of the rock's relief - so this is
//lighter than #208's state, not a return to it.

//AND SPARKLE, the albedo's half: snow is ice crystals, and what the eye forgives a photograph of snow for
//not resolving is the GLINT - a sparse dust of points bright enough to be specular, laid on a lattice of
//crystal-sized cells. The cells here are ~0.3 world units, NOT crystal-sized, on purpose: at the footprint
//the mid slopes actually draw (~0.08 world/pixel) crystal-scale cells are sub-pixel and a hash over them is
//noise, while a third-of-a-metre cell lands 3-4 pixels wide - the size a glint reads at. Thresholded hard
//(the top ~1.5 % of cells), so it is a dusting and not a wash; sun-facing only, because a glint is a
//reflection; and faded by the footprint the same way as everything else, so the far ranges stay the clean
//hazy shapes the aerial perspective wants.
//⚠ A GLINT IS A POINT INSIDE ITS CELL, NEVER THE WHOLE CELL, and getting that wrong is what the owner saw
//as "snow on the ground looks like squares". The lattice reasoning above is sound and stands; what did not
//was lighting the entire cell it picked. A cell is fixed in WORLD units, so its size on screen is the
//footprint's business: 0.3 world units is the intended 3-4 pixels at the mid slopes' ~0.08 world/pixel and
//is THIRTY pixels on the basin floor beside the arena, where the footprint is ~0.01 - and a hard `step` over
//it fills every one of those pixels. Photographed from the play camera it is a scatter of flat white
//parallelograms lying on the ground, which is exactly what a filled world-space quad looks like in
//perspective. So the cell now only says WHERE a glint is; how big it is comes from the footprint, which is
//what keeps it the same few pixels at every distance.
static const float SNOW_SPARKLE_DENSITY = 0.985;
static const float SNOW_SPARKLE_SCALE = 3.33;    //cells ~0.3 world units across, the lattice above
static const float SNOW_SPARKLE_RADIUS = 0.02;   //the smallest a glint is allowed to be, in world units

float SnowSparkle(float2 xz, float3 normal, float footprint)
{
    float fade = saturate(1.0 - footprint * 3.0);
    if (fade <= 0.0) return 0.0;

    float2 cellId = floor(xz * SNOW_SPARKLE_SCALE);
    float2 hash = NoiseHash22(cellId);

    //Which cells glint at all. NoiseHash22 answers in [-1, 1] rather than [0, 1], so this threshold takes the
    //top 0.75 % of them - half what the comment above has always claimed, and the sparser reading is the one
    //that was tuned by eye, so the number stays and the arithmetic is written down instead.
    float lit = step(SNOW_SPARKLE_DENSITY, hash.x);

    //A glint is about a pixel and a half across at any distance, with a floor so it does not vanish underfoot
    //where the footprint goes tiny, and a ceiling of half a cell for the reason below.
    float halfCell = 0.5 / SNOW_SPARKLE_SCALE;
    float radius = clamp(footprint * 1.5, SNOW_SPARKLE_RADIUS, halfCell);

    //Jittered off its cell's centre by whatever the radius leaves over, so the dust does not sit on a visible
    //lattice AND cannot cross into the neighbouring cell - a neighbour computes a different cellId and so a
    //different centre, so anything crossing the boundary would be cut off flat against it and the squares
    //would be back at the far end. As the radius grows into the cell the jitter goes to zero and the glint
    //centres, which is the old filled-cell look arrived at gracefully rather than by accident.
    //The offset is taken off the same hash through large multipliers because hash.x is confined to the top of
    //its range by the test above and would otherwise place every glint in the same corner.
    float2 centre = (cellId + 0.5) / SNOW_SPARKLE_SCALE
        + (frac(hash * float2(431.7, 197.3)) * 2.0 - 1.0) * (halfCell - radius);

    float glint = 1.0 - smoothstep(radius * 0.35, radius, distance(xz, centre));

    return lit * glint * fade * saturate(normal.y);
}

float4 MountainSurface(MountainVertexOutput input, uniform bool fullDetail)
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //Smooth per-vertex base normal (see the vertex shader for why not per pixel)
    float3 baseNormal = normalize(input.WorldNormal);

    //Fine rock relief roughens the faces, band-limited against the footprint so it fades to smooth towards the
    //horizon - which also keeps it off the distant faces, leaving the clean per-vertex normal to do the work there
    float relief = RockRelief(worldPosition.xz, footprint, fullDetail ? 4 : 3) * (1.0 - 0.6 * saturate(baseNormal.y));
    float3 rockNormal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    //Snow lies where the surface is BOTH up-facing (flats/shoulders keep it, cliffs shed it) AND high enough
    //(above an irregular, noisy snowline); rock shows on the steep faces and below the line. Noise breaks the
    //snowline so it is drifts and patches, not a clean contour. Read off the ROCK-perturbed normal: the
    //terrain's own slope decides what holds snow, and the snow's own relief is about to be added on top of
    //that decision rather than feeding back into it.
    //The FLUTES hold the snow (#504): in a couloir the threshold drops and on a rib it climbs, so a steep face
    //is striped dark and white the way every face in the references is, where a threshold on the facing angle
    //alone laid the snow on as a smooth film with the rock showing only where a whole face turned steep.
    //
    //And the edge is SHARP: the band from RockSlope to SnowSlope is read through a second, narrow smoothstep
    //(SNOW_EDGE either side of its middle). The references draw snow and rock as two materials with a crisp
    //edge between them; the band read linearly MIXED them across every mid-steep face, which is most of a
    //range, and a 40/60 blend of white and near-black under a violet sky is the uniform lilac this scene was.
    float slopeSnow = smoothstep(0.5 - SNOW_EDGE, 0.5 + SNOW_EDGE,
        smoothstep(RockSlope, SnowSlope, rockNormal.y + input.Flute * saturate(1.0 - footprint / FLUTE_SPACING * 2.0) * FluteSnow));
    float snowNoise = CloudNoise(worldPosition.xz * 0.03) * 9.0;
    float altSnow = smoothstep(SnowlineLow + snowNoise, SnowlineHigh + snowNoise, worldPosition.y);
    altSnow = smoothstep(0.5 - SNOW_EDGE, 0.5 + SNOW_EDGE, altSnow);
    //The BASIN is snow (#504). The scene has always called itself a snow basin, and every cirque the references
    //drew has a white floor under its walls - but the floor lies below the snowline, so it took only the 0.15
    //shoulder below and was bare rock round the island, which the darker rock of #504 turned near black. The
    //clearing's own ramp is what says "floor": flat and inside it, snow; up the range's foot, the snowline rules.
    float floorSnow = 1.0 - smoothstep(ClearingRadius, ClearingRadius + ClearingTransition * 0.6, length(worldPosition.xz));
    float snowDetailed = slopeSnow * saturate(altSnow + 0.15 + floorSnow); //a little snow even on lower shoulders

    //Fade the snow/rock DETAIL to a smooth snowy value with distance (footprint): the sharp rock/snow split
    //aliases into a crawl on the far, stacked ranges, so smoothing it there leaves clean snowy peaks while the
    //near and mid slopes keep the rock/snow detail.
    float detailFade = saturate(1.0 - footprint * 0.05);
    float snow = lerp(0.72, snowDetailed, detailFade);

    //The snow's second surface, the DRIFT RELIEF, used to be added here (#208) and was cut in #296 - see
    //SnowRelief's headstone above for what it was and the three figures that decided it. The sparkle below
    //is the half that stayed, and it still dies with detailFade like everything else the near slopes carry,
    //so the far ranges stay clean shapes in haze.
    //
    //⚠ And the rock relief is applied ONCE (#504). #208 split the perturbation in two - rockNormal for the
    //snow's decision, then `normal = Perturb(rockNormal, relief + snowRelief)` for the lighting - and so tilted
    //the lit normal by the rock relief TWICE; #296 cut the snow's half and left `Perturb(rockNormal, relief)`
    //standing. ROCK_FBM_GAIN was matched to the old sines by eye in #170 with one application, so from #208 on
    //the range was shaded at about twice that relief - and that doubled relief is the one that has been on the
    //screen, so it is kept, as a doubled gain, and the second PerturbNormalFromHeight a pixel is not.
    float3 normal = rockNormal;

    //Rock varies between a dark and a lighter grey-brown in patches, so the faces are not one flat colour
    float rockPatch = saturate(CloudNoise(worldPosition.xz * 0.08 + 21.0) * 0.5 + 0.5);
    float3 rock = lerp(RockColor, RockColorLight, rockPatch * detailFade);

    //Fine per-pixel rock grain, the cue Desert.fx's sand added that "a floor with no fine albedo change still
    //looks airbrushed however it is lit" (docs/scenes.md). One hash per pixel over a fine world lattice,
    //band-limited against the footprint the same way as the relief so it fades to smooth before its cells reach
    //pixel size (a hard-edged per-cell value is its own aliasing source) and stays off the distant ranges with
    //the rest of the detail. Only on the rock, not the snow it sits beside.
    float rockGrainFade = saturate(1.0 - footprint * 120.0) * detailFade;
    rock *= 1.0 + NoiseHash22(floor(worldPosition.xz * 60.0)).x * 0.14 * rockGrainFade;

    float3 albedo = lerp(rock, SnowColor, snow);

    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows (#471), into the same sunlight factor the clouds dim, so everything read off it
    //is shadowed at once. What casts here: the island and the gun — the peaks shade nothing but themselves,
    //and the map's height range has to span them or the fit is wrong.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

    //ALPENGLOW (#504): at a low sun the basin and the lower slopes lie in the shadow of the range across from
    //them and only the heights still take the light - the pink summits over blue snow that every dusk reference
    //draws, where this scene lit the whole range evenly and so drew it one lilac from floor to crest. Not a cast
    //shadow (the map covers the basin, not the range) but the altitude the light has climbed to, scaled by how
    //low the sun is, so a high sun lights everything as before. The floor keeps the basin from going to the
    //ambient alone: the island stands in it lit by the rig's sun, and a black ground round it would argue.
    float lowSun = 1.0 - smoothstep(0.2, 0.55, SunDirection.y);
    float reach = lerp(0.35, 1.0, smoothstep(AlpenglowLow, AlpenglowHigh, worldPosition.y));
    sunlight *= lerp(1.0, reach, lowSun);

    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light: up-facing takes mostly the zenith, slopes more of the horizon. The blue zenith filling
    //the shadows gives the snow its cold cast where the sun misses it.
    //
    //MOSTLY, not wholly (#504): a surface facing straight up still sees the whole dome down to the skyline, and
    //the horizon ring is a good quarter of what reaches it. At the old weights up-facing took the zenith alone,
    //which was invisible while the basin floor was rock - and once #504 laid snow on it, dome 8's intensely
    //saturated violet zenith turned the whole floor into a blue-violet sea where the references' shadowed
    //snow is a greyed blue.
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, 0.25 + 0.5 * saturate(normal.y * 0.5 + 0.5));

    float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

    //The glint, ON TOP of the lit snow: specular in character, additive in code - a glint is bright enough
    //that tonemapping it down a little is the look, not a loss. Scaled by the direct sun only (ndotl and the
    //cloud shadow with it), because a glint IS reflected sun.
    //...and it is gated on the SNOW MASK, which this line said in words and did not do. Without the factor
    //the glint dusts bare rock as readily as snowfield, and the basin floor beside the arena was rock carrying
    //the `altSnow + 0.15` shoulder and nothing more - so the brightest thing in the scene was landing on the
    //darkest ground the player stands closest to, which is why the squares above were impossible to miss.
    //Since #504 that floor is snow, so the glint does reach the ground beside the arena now - as the
    //footprint-sized point #278 made it, which is what a snowfield underfoot should do.
    if (fullDetail)
        color += SunColor * SnowSparkle(worldPosition.xz, normal, footprint)
            * snow * ndotl * sunlight * detailFade * 3.0;

    //Alpine haze: the distant range fades into the skyline, the strong aerial perspective of a lot of cold air
    float haze = saturate(dist / HorizonHazeDistance);
    color = lerp(color, HorizonColor, haze * haze);

    return float4(color, 1.0);
}

//Two programs from one body, the idiom Forest.fx established (#298). "Mountain" is the authored range;
//"MountainReduced" is the same range without the snow's SPARKLE and with the rock's relief on three octaves
//instead of four. A PAIR, because a lone reduction buys nothing on a pass that is occupancy-bound (see
//SceneRenderer.SceneDetail for the measurement that taught this project that) — and this is its second pair,
//the first having been #208's two snow surfaces. #296 then cut the drift relief out of the AUTHORED scene
//altogether, so half of that pair was no longer here to drop and the reduced program needed a new partner
//for the sparkle. The rock's fourth octave is it: measured 0.31 ms on its own at ssaa 2, against the
//sparkle's 0.20, and what it costs is crag-scale roughness on the NEAR faces alone — the band limiter has
//already faded it out everywhere else. Everything the range is made of stays: the massing, the rock relief
//itself, the grain, the patches, the snowline's own noise, the haze.
//
//A `uniform bool` argument and not a shader constant the body branches on: the compiler folds it at compile
//time, so each program pays for only what it keeps. That is the whole point — the reduced one has to be
//SMALLER, not the same program skipping work, or it saves the occupancy it was written to save.
float4 MountainPS(MountainVertexOutput input) : COLOR { return MountainSurface(input, true); }
float4 MountainReducedPS(MountainVertexOutput input) : COLOR { return MountainSurface(input, false); }

technique Mountain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MountainVS();
        PixelShader = compile PS_SHADERMODEL MountainPS();
    }
};

technique MountainReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MountainVS();
        PixelShader = compile PS_SHADERMODEL MountainReducedPS();
    }
};
