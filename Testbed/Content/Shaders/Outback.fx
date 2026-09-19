//Draws the Australian outback (#112): weathered red-orange monoliths standing on a wide, near-flat spinifex
//plain, the round stone island in a clearing among them. It is the arid sibling of the Sahara next door in
//the enum, and the two are deliberately nothing alike to look at - the desert is golden dunes with a braided
//ripple surface and no silhouette but its own crests, this is RED ground under ROCK: a skyline of Uluru-style
//bornhardts, ribbed by the gullies water has cut down their flanks and streaked black where it ran.
//
//The three things that carry the read, in the order the eye takes them:
//
//  1. THE MONOLITHS ARE GEOMETRY, not a painted horizon. They are displaced into the same camera-centred grid
//     the desert, savanna, mountains, meadow and Moon all use, so they occlude, silhouette and cast the scene's
//     own aerial perspective. RockLayer places them on a jittered single-cell lattice, the trick the Moon's
//     craters, the space starfield and the meadow's wildflowers all use: a formation is held inside its own
//     cell by its own reach, so only the pixel's own cell is ever read (three to five hashes) rather than the
//     nine a neighbourhood walk would cost - and this field is evaluated FOUR times per pixel (the vertex tap
//     plus the normal's three), which is what makes that difference the whole budget.
//  2. THE GROUND IS RED, and its texture is spinifex: round hummocks of spiky grass on bare red sand, spaced
//     out the way plants competing for water actually space themselves. That is a CELLULAR field by nature -
//     Voronoi2 gives closed round cells with bare ground between them, where a noise mottle would only give
//     patches - under a broad patch mask, so the plain has bare stretches and dense ones rather than a tiling.
//  3. THE GULLIES ARE GEOMETRY TOO, and that is not where they started. They were a per-pixel field combed
//     along the terrain's own downhill direction, driving the normal through PerturbNormalFromHeight - which
//     drew the rock in HORIZONTAL TERRACES, one band per row of grid triangles. Both of that path's inputs
//     are screen derivatives (ddx/ddy of the world position, and fwidth for the band limit), and a screen
//     derivative is constant across a triangle and JUMPS at every facet edge. On the gentle terrain of every
//     other scene here adjacent facets very nearly agree and nothing shows; on a flank falling forty-four
//     units over eight cells they do not, and the jump lands straight in the shading. So the ribs moved into
//     the height field, where the terrain normal's own finite differences pick them up and no screen
//     derivative is involved at all - and they buy the silhouette as well, which the normal never could.
//     They are radial by construction (a gully runs downhill, and downhill on a dome is radial), sampled on
//     the unit circle of the formation's own frame: a closed curve in the noise's domain, so it comes back
//     round to itself with no seam and no pole - which is what a rib count taken off an atan2 bearing cannot
//     do, the failure the cavern's mineral veins shipped with once.
//
//REDRAWN AGAINST REFERENCES rendered locally for this pass. What a monolith out there actually is, seen from
//its plain: a LOAF, not a whaleback - walls standing near vertical to a rounded crown (see Bornhardt) - hung
//with dense black water streaks from the crown's edge down, pale where the skin has flaked off, dark hollows
//at the foot; and the ground is a vivid red sand with separate tussocks on it, not a mottle. Two new fields pay
//their own way: the streaks (a GradientNoise3, behind a branch, since only a wall reads it) and the tussocks'
//frayed outline (a GradientNoise2). The streaks run on the formation's own BEARING rather than on the world,
//for the gullies' reason (see RockLayer).
//
//Shader Model 5.0, built out of this one directory by all three executables. It borrows the scene toolkit:
//the sky is the current dome's two-colour gradient in linear radiance, every procedural feature band-limits
//against the pixel footprint, and the cloud shadow is the one shared field in Clouds.fxh - so the plain
//darkens under the very cloud the sky shows overhead (the map editor never sets the cloud uniforms, so there
//CloudSunlight is a flat 1.0 - full sun, no shadow).

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
//The sun's cast shadows (#469, in this scene since #471): SceneRenderer renders the map before the scene
//pass and this reads it. See Shadows.fxh for what casts and what a receiver owes.
#include "Shadows.fxh"

//The shared noise library: gradient noise for the plain's swell and the rock's flutes, Voronoi for the
//spinifex hummocks. Its hashes are the sine-free ones and its gradient noise fades QUINTICALLY, which matters
//here for the same reason it matters in the desert - these fields end up driving a normal, and the cubic
//fade's discontinuous second derivative shows as faint lattice creases the moment it does.
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, and the sun's own radiance (the lit-cloud colour the weather uses, tinted by the dome)
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), so the terrain sits still in the
//world while the mesh slides under it
float2 OriginXZ;

//Radius of the island's footprint cut out of the terrain around the world origin, so the drain funnel reads
//as a drain into a pit rather than a bowl in flat ground. The map editor draws no island and leaves it 0.
float IslandHoleRadius;

//The plain: its mean level (the island's foot) and the amplitude of the broad swell that keeps it from being
//a snooker table. Deliberately small - a gibber plain IS flat, and the drama here is what stands on it.
float OutbackLevelY;
float PlainRelief;

//The clearing the island stands in. It gates the MONOLITHS only (per formation, from the formation's own
//centre - see RockLayer) and never the plain's swell, so there is no ramped mean anywhere to read as a bowl.
float ClearingRadius;
float ClearingTransition;

//The monoliths: how far apart their lattice cells sit, how many cells carry one, and how tall the tallest
//stands over the plain. The small outcrops are the same three dials an octave down.
float RockSpacing;
float RockChance;
float RockHeight;
float OutcropSpacing;
float OutcropChance;
float OutcropHeight;

//The gullies: roughly how many run round a formation, how deeply they cut into its radius, and how black the
//water-stained streaks down the walls run.
float RibCount;
float RibDepth;
float VarnishStrength;

//The flaking: how pale the rock is where its oxidised skin has come away, and how much of the lower wall it
//claims. And how dark the hollows at a wall's foot are - 0 leaves the foot lit like the face above it.
float3 FlakeColor;
float FlakeStrength;
float CaveShade;

//How rough the rock's own face is, in world units of relief — the scale below the gullies, which is what
//stops a near monolith reading as an airbrushed dome.
float RockRelief;

//The spinifex: world units between tussocks, how big they grow where they grow (a radius, not an opacity -
//see the ground in OutbackPS), and how high they stand (they are relief as well as colour - a tussock catches
//the sun on its own dome).
float SpinifexSpacing;
float SpinifexCover;
float SpinifexRelief;

//Reflectances, all LINEAR radiance. The rock runs between an oxidised iron red in the shade and a bright
//orange where the sun rakes it; the ground between a deep red sand and the pale dust that gathers on it.
float3 RockColorDeep;
float3 RockColorBright;
float3 VarnishColor;
float3 SoilColor;
float3 SoilColorPale;
float3 SpinifexColor;

//How much of the sky's hemisphere light fills the flats
float AmbientStrength;

//Sunlight thrown back off the lit red sand onto the faces turned towards it - the desert's SandBounce, here
//for the walls. It is what keeps a monolith's shaded side a deep red with its streaks readable in every
//reference, where the sky's light alone left the loaf's near-vertical walls close to black under a low sun.
float SoilBounce;

//How hard the desert varnish glints. Varnish is a genuinely glossy mineral skin - it is the one part of this
//scene that is not matte - so the term is gated on the rock mask and stays off the ground entirely.
float VarnishGloss;

//Airborne red dust: how far the haze is carried from the dome's own horizon colour towards HazeTint. Not a
//drifting veil like the Sahara's (that costs a noise evaluation across the whole frame and the outback's air
//is still); it is aerial perspective through dust, so it rides the distance fade and costs nothing.
float3 HazeTint;
float DustStrength;
float HorizonHazeDistance;

//How far the last stretch of the fade leans from the dome's HorizonColor towards the dust's own hue - the
//desert's HazeWarmth, and for the desert's reason (see the air, at the end of OutbackPS).
float HazeWarmth;

//Heat shimmer over the middle distance, and the wall clock and wind that drive it. The one thing in this
//scene that moves besides the cloud shadows and the birds.
float HeatShimmer;
float OutbackTime;
float2 WindDirection;

//--- The land ------------------------------------------------------------------------------------------

//How far past its base line a formation's talus apron reaches, as a multiple of its own radius. It is the
//figure the single-cell lattice below is kept honest by: a formation's margin inside its cell is its full
//reach, apron and elongation included, so nothing it draws can ever cross into a cell that is not read.
#define TALUS_REACH 1.3

//How many streak features run round a formation's unit circle, as a radius in the noise's domain: about
//2*pi*32 of them, i.e. a streak a unit or two wide on a monolith 27-43 units in radius.
#define STREAK_RING 32.0

static const float PI = 3.14159265;

//The profile of a BORNHARDT - Uluru's, and not the whaleback this scene shipped with. Out on its plain a
//monolith is a loaf: its walls stand near vertical and its crown is rounded over, which the references drew
//at every angle and the old smoothstep profile could not - run backwards it gives a flank that leans back all
//the way down, a hill rather than a rock.
//
//Two factors. The CROWN is a superellipse, sqrt(1 - t^p): p = 2 is a round dome, and the higher p goes the
//flatter the top and the more abruptly it breaks over into the wall, which is how the squareness roll makes
//some formations domes and others loaves. The WALL multiplies it down to nothing over the last `wall` of the
//radius, which is what makes the foot meet the plain rather than stand on it on a knife edge.
//
//HOW STEEP THE WALL IS, IS THE MESH'S DECISION and not taste's - the desert's crest rule at monolith scale.
//The grid cell is 2.5 world units; the monoliths' wall of 0.30 of a 27-43 unit radius falls over 8 to 13
//units, three to five cells. Narrower does not hold: the second lobe's wall, first cut to its own smaller
//radius, came to two cells, and the mesh's facets wandered back and forth across it - every field the pixel
//shader reads off the height field then drew horizontal ripples, one per row of the grid (see the lobe in
//RockLayer). The pow is kept off an exact zero, which some compilers turn into a NaN through log(0).
float Bornhardt(float t, float squareness, float wall)
{
    float clamped = saturate(t);

    return sqrt(saturate(1.0 - pow(max(clamped, 1e-4), squareness))) * smoothstep(1.0, 1.0 - wall, clamped);
}

//Rotate p into the frame whose +X is `axis` (a unit vector). Taken as a vector rather than an angle because
//the angle would only ever be turned straight back into its sine and cosine: a hash gives the vector for one
//rsqrt where sin/cos of a hashed angle costs two transcendentals, and this runs per formation per tap.
float2 RotateInto(float2 p, float2 axis)
{
    return float2(dot(p, axis), dot(p, float2(-axis.y, axis.x)));
}

//A unit vector out of two rolls in 0..1. Guarded against the exact centre of the square, which would
//normalize to a NaN and take the whole formation's shape with it.
float2 RollDirection(float2 roll)
{
    float2 v = roll * 2.0 - 1.0;

    return v * rsqrt(max(dot(v, v), 1e-4));
}

//One lattice of rock formations. Returns the rise in world units above the plain; `shape` comes back as the
//0..1 coverage the pixel shader blends rock material in by, `body` as the profile itself (0 at the foot, 1 on
//the crown - how far up its own wall a point is), and `bearing` as the domain the wall's streaks run in.
//
//`wall` and the squareness range are the Bornhardt profile's: a monolith is a loaf and a boulder is not, so
//the two lattices pass their own.
//
//THE SINGLE-CELL READ IS THE WHOLE BUDGET. This runs four times per pixel (the vertex tap and the normal's
//three), so a 3x3 neighbourhood walk would be nine times the hashes answering "no" for formations that by
//construction cannot reach the point - the mistake the Moon's first crater field made, which cost it an
//order of magnitude. Every formation is held inside its own cell by `margin`, which is its full reach: the
//radius, times the talus apron, times the elongation that stretches it along its own axis.
//
//That margin is charged in CELL fractions and it does bite - the largest, most elongated formation is left a
//jitter box only a quarter of a cell wide. It reads as random anyway, because the visible irregularity is in
//WORLD units: a quarter of a 420-unit cell is a hundred units of wander for a rock a hundred units long. The
//empty cells (RockChance) are what break the lattice for the small formations, where the box is generous.
float RockLayer(float2 p, float cellSize, float seed, float chance, float height,
    float minRadius, float maxRadius, float maxElongation, float ribDepth,
    float wall, float minSquareness, float maxSquareness,
    out float shape, out float rib, out float body, out float2 bearing)
{
    shape = 0.0;
    rib = 0.0;
    body = 0.0;
    bearing = 0.0;

    float2 q = p / cellSize;
    float2 cellId = floor(q);
    float2 f = q - cellId;

    //Five independent rolls per candidate: existence and squareness, radius and height, where in the
    //cell, which way it lies, and where its second lobe sits. Separate hashes rather than one reused - a
    //size correlated with a bearing is a texture nobody would name but the eye would still catch.
    float2 rollA = NoiseHash22(cellId + seed) * 0.5 + 0.5;

    //Not every cell carries a formation. A plain with a rock in every cell of a lattice IS the lattice,
    //however hard the jitter works - and an outback whose monoliths came evenly spaced would be an orchard.
    if (rollA.x > chance) return 0.0;

    float2 rollB = NoiseHash22(cellId + seed + 23.7) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seed + 57.1) * 0.5 + 0.5;
    float2 rollD = NoiseHash22(cellId + seed + 91.3) * 0.5 + 0.5;

    float radius = lerp(minRadius, maxRadius, rollB.x);
    float elongation = lerp(1.0, maxElongation, rollD.y);

    //The full reach, apron and stretch included - see the note above on why this is what the margin must be.
    //The gullies cut INTO the radius, so at their deepest they also push the base line OUT by the same
    //fraction — which is reach the margin has to be told about, or a ribbed flank crosses into a cell nobody
    //reads and is cut off along a straight line.
    //
    //The clamp is not what keeps a formation inside its cell (the shipped ranges do that: 0.16 * 1.3 * 1.18 *
    //1.8 is 0.44); it is what stops a jitter box from going NEGATIVE if the ranges are ever widened, which
    //would place centres outside their own cell and break the single-cell read silently rather than loudly.
    float margin = min(radius * TALUS_REACH * (1.0 + ribDepth) * elongation, 0.45);
    float2 centre = margin + rollC * (1.0 - 2.0 * margin);

    //THE CLEARING IS DECIDED PER FORMATION, from its own centre, and never per pixel. Per pixel the ramp is a
    //radial gradient sliced across the rock, which draws a monolith sunk to one side like a ship going down;
    //taken from the centre, a formation is either standing whole or is not there at all.
    float2 centreWorld = (cellId + centre) * cellSize;
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, length(centreWorld));

    if (ramp <= 0.0) return 0.0;

    //The formation's own frame: turned to its own bearing and stretched along it, so no two are the same dome
    //seen from a different side.
    float2 local = RotateInto(f - centre, RollDirection(rollD));
    local.x /= elongation;

    float reach = length(local);
    float d1 = reach / radius;

    //THE GULLIES. Sampled on the UNIT CIRCLE of the formation's own frame, so the noise domain is a closed
    //curve and the pattern meets itself with no seam and no pole — the whole reason this is a direction and
    //not a bearing angle. RibCount is a radius in the noise's domain, so the circle's circumference is what
    //sets how many ribs there are: about 2*pi*RibCount of them round the rock.
    //
    //They cut the RADIUS rather than the height, which is why they reach the silhouette: where the field is
    //HIGH the formation's edge pulls in (a gully), where it is low the edge stands out (a spur) - measured
    //round a flank, height and `rib` correlate at -0.68, which is the sign every shading term below is written
    //to, and which the shipped varnish had backwards, staining the spurs. Faded out towards the crest,
    //because a bornhardt's top is bald rock — and because the ribs converge there, so their world width
    //shrinks with the radius and past a point they would fall between grid cells.
    float2 radial = local * rsqrt(max(dot(local, local), 1e-6));
    rib = GradientNoise2(radial * RibCount + cellId * 13.1 + seed);

    float ribbed = d1 * (1.0 + rib * ribDepth * smoothstep(0.20, 0.62, d1));

    //A SECOND LOBE, because a bornhardt comes in shoulders - Kata Tjuta is thirty-six domes leaning on each
    //other, and even Uluru is two humps with a saddle. A field of single domes is the plane-wave-sine failure
    //in dome form: identical units summed over a lattice. The offset and the smaller radius are bounded so
    //the lobe's own apron still lands inside the reach `margin` was computed from.
    float2 rollE = NoiseHash22(cellId + seed + 131.9) * 0.5 + 0.5;
    float lobeScale = lerp(0.40, 0.60, rollE.y);
    float2 lobeCentre = RollDirection(rollE) * radius * 0.5;

    float d2 = length(local - lobeCentre) / (radius * lobeScale);

    //The loaf (see Bornhardt), its squareness its own roll, so some formations are rounded domes and others
    //are flat-topped blocks.
    //
    //THE LOBE'S WALL IS WIDENED BY ITS OWN SCALE, so it falls over the same WORLD width the main wall does. Cut
    //to its own radius it came to half that, two grid cells, and the mesh could not hold it: the facets
    //crossed the crease where the lobe leans on the main body back and forth, and the steepness, the height
    //and the streaks read off the field there all drew horizontal ripples, one per row of the grid.
    float squareness = lerp(minSquareness, maxSquareness, rollA.y);
    float mainBody = Bornhardt(ribbed, squareness, wall);
    float lobeBody = Bornhardt(d2, squareness, min(wall / lobeScale, 0.9)) * lerp(0.55, 0.90, rollE.x);

    body = max(mainBody, lobeBody);

    //THE STREAKS RUN ON THE BEARING, for the gullies' reason and one of their own. Water runs straight down a
    //wall, so a streak must hold still as a point moves down it - and down a wall that leans back, a point
    //moves OUTWARD across the plain as well as down. A field of world position (stretched however tall)
    //therefore broke every streak into a column of blobs, one per few units of that outward travel; the
    //formation's own radial direction does not change down a wall at all. The lobe's walls take the main
    //body's bearing too, so they streak a little diagonally - and a second bearing for them measured as no
    //better a picture than one, while its switch-over would have been a seam.
    bearing = radial * STREAK_RING + cellId * 17.3 + seed;

    //The talus apron: the broken rock that piles at the foot of every one of these, and the reason a monolith
    //meets the plain in a skirt rather than in a machined crease.
    //
    //It is taken off the UNRIBBED radius, and that is not a detail. The apron is wide and only a tenth of the
    //height, so on a small formation it is most of what can be seen — and cut with the gullies it came out as
    //a flat many-pointed fan lying on the sand, a starfish rather than a rock, which is exactly how it drew.
    //Scree fills a gully in anyway; it does not follow it out.
    float apron = smoothstep(TALUS_REACH, 1.0, min(d1, d2));

    //The material mask reaches further than the height does, so the scree at the foot is still rock-coloured
    shape = saturate(body + apron * 0.45);

    return (body * 0.9 + apron * 0.1) * height * lerp(0.62, 1.0, rollB.y) * ramp;
}

//The land at a world point: its height, and what the pixel shader shades the rock by. A struct rather than
//a packed vector since it grew past three fields; the vertex tap and the normal's two offset taps read the
//height alone, and with the function inlined the compiler drops everything they do not read - so the four
//evaluations per pixel still pay for the extra outputs only once.
struct OutbackLand
{
    //World Y of the ground
    float Height;

    //0..1 rock coverage, apron included
    float Rock;

    //The gully field of the formation the point is on: high in a gully, low on a spur
    float Rib;

    //How far up its own formation the point stands: 0 at the foot of the wall, 1 on the crown
    float Body;

    //Where on the formation's circuit the point is, in the streaks' noise domain
    float2 Bearing;
};

//
//The plain's swell is NOT gated by the clearing, and the monoliths are - which is what keeps the mean honest.
//A field with a mean multiplied by a radial ramp rises with the ramp and draws a shallow bowl with the island
//at the bottom of it (the trap the desert's DuneSum carries a trailing constant to avoid). Here the one field
//with a big mean is the rock, and its ramp is per formation rather than per pixel, so nothing is ever
//partially raised.
OutbackLand OutbackHeight(float2 p)
{
    //Two octaves of gradient noise, genuinely two and not a sine pair: a sum of plane waves keeps its planes
    //however many terms it has, which three scenes in this project learned the expensive way.
    float swell = GradientNoise2(p * 0.0062) * 0.70 + GradientNoise2(p * 0.0185 + 5.1) * 0.30;

    float rockShape, outcropShape, rockRib, outcropRib, rockBody, outcropBody;
    float2 rockBearing, outcropBearing;

    //The monoliths are loaves: a wall of 0.30 of the radius (the most the mesh holds, see Bornhardt), and
    //crowns from a rounded dome to a flat block.
    float rock = RockLayer(p, RockSpacing, 13.7, RockChance, RockHeight,
        0.10, 0.16, 1.8, RibDepth, 0.30, 2.2, 5.0, rockShape, rockRib, rockBody, rockBearing);

    //The small outcrops sit on a lattice ROTATED against the monoliths', so the two grids share no lines and
    //a boulder field can never come out ranked along a big formation's rows.
    //
    //Their radii are held near their own HEIGHT rather than scaled off the cell, and that is what makes them
    //boulders: at a fifth of a 110-unit cell they came out twenty-five units across and five tall, and a disc
    //at that aspect with gullies cut round it is not a rock but a starfish — five radiating spines lying flat
    //on the sand, which is exactly what it drew. And they carry NO gullies at all (ribDepth 0), which is right
    //twice over: a boulder is a rounded thing, water having never run far enough down one to cut it, and at
    //this size the ribs' world width falls under the grid cell anyway.
    //
    //And they are NOT loaves. A boulder is rounded, and at 5-9 units of radius a 0.30 wall would be a cell
    //wide. Their wall and squareness are the least-squares fit to the whaleback profile they shipped with,
    //which they hold to within a tenth of their height at every radius and every roll - so the change to the
    //monoliths left the boulders as they were.
    float outcrop = RockLayer(RotateInto(p, float2(0.8253, 0.5647)), OutcropSpacing, 71.3, OutcropChance, OutcropHeight,
        0.045, 0.085, 1.35, 0.0, 0.475, 1.5, 5.5, outcropShape, outcropRib, outcropBody, outcropBearing);

    //The gully value, the profile and the bearing belong to whichever formation the point is actually ON, so
    //the pixel shader can streak it without paying for a field of its own. Handed back here rather than
    //recomputed there because the per-pixel version of exactly this is what drew the terraces (see the header).
    //
    //A monolith's BODY always wins, whatever the coverages say. A boulder can land on a monolith's lower wall,
    //where the monolith's coverage is under 1 and the boulder's centre is 1 - and there the streaks took the
    //boulder's bearing and fanned out of its centre across the wall, a starburst of hairlines. Off the body
    //(the apron and the plain) the larger coverage decides, as it always has.
    bool onRock = rockShape >= outcropShape || rockBody > 0.0;

    OutbackLand land;
    land.Height = OutbackLevelY + PlainRelief * swell + rock + outcrop;
    land.Rock = saturate(rockShape + outcropShape);
    land.Rib = onRock ? rockRib : outcropRib;
    land.Body = onRock ? rockBody : outcropBody;
    land.Bearing = onRock ? rockBearing : outcropBearing;

    return land;
}

struct OutbackVertexInput
{
    float4 Position : POSITION0;
};

struct OutbackVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

OutbackVertexOutput OutbackVS(OutbackVertexInput input)
{
    OutbackVertexOutput output;

    //Local grid position + the snapped origin gives the world XZ; the land is sampled there, so it sits still
    //in the world while the grid slides under it
    float2 worldXZ = input.Position.xz + OriginXZ;
    float3 worldPosition = float3(worldXZ.x, OutbackHeight(worldXZ).Height, worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//--- The surface ---------------------------------------------------------------------------------------

float4 OutbackPS(OutbackVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The base normal, taken PER PIXEL from the height field's own gradient (three taps) rather than
    //interpolated from a per-vertex normal. Interpolating a coarse displaced grid's per-vertex normal is what
    //left a Mach-band grid across the old dunes and a soft-focus haze across the forest floor; evaluated per
    //pixel the shading is smooth whatever the tessellation. Here it does a second job for free - the two
    //offset taps give the DOWNHILL direction the flutes are combed along, below.
    float e = 1.5;
    OutbackLand here = OutbackHeight(worldPosition.xz);
    float hx = OutbackHeight(worldPosition.xz + float2(e, 0.0)).Height;
    float hz = OutbackHeight(worldPosition.xz + float2(0.0, e)).Height;

    float2 slope = float2(hx - here.Height, hz - here.Height) / e;
    float3 baseNormal = normalize(float3(-slope.x, 1.0, -slope.y));

    float rockMask = smoothstep(0.03, 0.30, here.Rock);

    //The pixel's size measured off the VIEW RAY rather than off the surface. `footprint` is fwidth of the world
    //position, which is constant across a mesh facet and jumps at its edge - harmless on the plain, but on a
    //wall three cells tall it steps once per row of the grid, and a band limit keyed to it draws those rows.
    //The ray through a pixel is the same whichever facet it lands on, so this one has no rows in it; it
    //under-reads a surface seen edge-on, which is why it is used for the walls' streaks and nothing else.
    float3 towardsEye = normalize(CameraPosition - worldPosition);
    float rayFootprint = dist * length(fwidth(towardsEye));

    //A broad field, one evaluation doing three jobs: the rock's shade-to-sun colour, the ground's sand-to-dust
    //colour, and (offset) where the spinifex grows thick and where the ground is bare. One octave, because
    //everything above this scale is carried by the flutes, the hummocks and the grain, and nothing below the
    //pixel frequency at any distance this is drawn from - so it needs no band-limiting either.
    float broad = GradientNoise2(worldPosition.xz * 0.021);

    //--- The rock ------------------------------------------------------------------------------------
    //The gully field comes back OUT of the height field (see the header): the very value that cut the
    //formation's radius, so the streaks below crowd into the very gullies it cut, and nothing here takes a
    //screen derivative of it.
    float rib = here.Rib;

    //How steep it is here, which is what decides whether water could have run down it at all: the crown of a
    //monolith is bare bald rock, the walls are streaked, and the difference is most of what says "weathered".
    float steep = saturate(1.0 - baseNormal.y * baseNormal.y);
    float wallGate = smoothstep(0.45, 0.85, steep);

    //The rock's own surface. HEIGHT is folded into the domain, because a field of the XZ plane alone is
    //constant down a vertical face and a monolith is mostly vertical face — it would draw the near flank as
    //one smeared vertical streak, which is worse than no field at all.
    //
    //It is the height FIELD's height, not the mesh's. The two differ between vertices by up to a few units on a
    //wall this steep, and the difference is a different shape on every facet - folded in, the interpolated Y
    //drew chevrons down the walls, one per cell, in a CPU heightfield render of this very field.
    float rockDomain = 0.28;
    float rockSurface = Fbm2BandLimited(worldPosition.xz * rockDomain + here.Height * 0.19, 3,
        footprint * rockDomain);

    //DESERT VARNISH: the black mineral skin left where water has run repeatedly down the same line, which on a
    //monolith is not a few gullies but the whole wall, hung with streaks from the crown's edge down. A
    //threshold of 3D noise on (bearing, height): the bearing holds a streak still down the wall (see RockLayer),
    //the height, stretched long, starts and ends it. The gullies (`rib` high) and the broad field lower the
    //threshold, so the streaks crowd where water gathers.
    //
    //Band-limited the cellular way: the edge widens with the pixel, and as a streak narrows towards a pixel the
    //whole field fades to its own average coverage rather than to clean rock - `1 - smoothstep(-0.4, 0.4, t)`
    //is that average for this noise, measured to within 0.02 over every threshold the gullies can push it to.
    //So a far monolith keeps the darker face its streaks give it instead of brightening as it recedes.
    const float streakDown = 0.03;
    const float streakWidth = 1.4;

    //Behind a branch, because the streaks exist only on a wall and a wall is a small part of any frame this scene
    //is drawn in: everywhere else both of their uses (the varnish and the flaking) are multiplied by `wallGate`
    //and come out 0 whatever this is. Nothing inside takes a derivative - the footprint is read above.
    float streak = 0.0;

    [branch]
    if (wallGate * rockMask > 0.0)
    {
        float streakField = GradientNoise3(float3(here.Bearing, here.Height * streakDown));
        float streakFootprint = rayFootprint / streakWidth;
        float streakThreshold = 0.08 - rib * 0.30 + broad * 0.12;
        float streakEdge = streakFootprint * 0.35 + 0.03;

        streak = lerp(1.0 - smoothstep(-0.4, 0.4, streakThreshold),
            smoothstep(streakThreshold - streakEdge, streakThreshold + streakEdge, streakField),
            saturate(1.0 - 1.5 * streakFootprint));
    }

    //Only down a wall, and not in its lowest reach, where the references show the rock flaking and hollowed.
    float varnish = streak * wallGate * smoothstep(0.12, 0.45, here.Body) * VarnishStrength;

    //THE TONE MUST NOT SATURATE, and it did: broad, the gullies and the surface field were summed at weights
    //whose worst case ran well past 1, so on a near formation the whole face pinned to RockColorBright and the
    //rock came out as ONE airbrushed orange dome with two creases on it — every field present in the shader
    //and none of them reaching the screen. The weights are cut to land inside 0..1 for all but the extremes.
    //The gullies are the deeper red, and lightly: at the old weight each one drew a dirty smear down the wall.
    float tone = saturate(0.5 + broad * 0.45 - rib * 0.20 + rockSurface * 0.9);

    float3 rock = lerp(RockColorDeep, RockColorBright, tone);

    //FLAKING: pale patches where the oxidised skin has come away, off the surface field the rock already pays
    //for. On the walls only (a crown weathers smooth), mostly low on them, and never under a streak.
    float flake = smoothstep(0.12, 0.26, rockSurface + broad * 0.2) * wallGate * (1.0 - streak)
        * lerp(0.35, 1.0, smoothstep(0.8, 0.3, here.Body)) * FlakeStrength;

    rock = lerp(rock, FlakeColor, flake);
    rock = lerp(rock, VarnishColor, varnish);

    //THE HOLLOWS at a wall's foot: a band of shade whose top wanders with the broad field (no hollow at all
    //where it runs low) and is ragged with the surface field. Albedo only - it is shade, and costs nothing.
    float caveTop = saturate(broad * 1.4 + 0.1) * 0.32 + rockSurface * 0.12;
    float cave = smoothstep(caveTop, caveTop - 0.08, here.Body) * wallGate;

    //--- The ground ----------------------------------------------------------------------------------
    //SPINIFEX. Round tussocks of it, spaced out the way plants competing for the same water actually space
    //themselves - which is a CELLULAR field by nature and not a noise one: Voronoi2's low values are closed
    //round cells with bare ground between them, where an fBm mottle only ever gives patches with no edge. The
    //tussocks come and go under a patch mask, so the plain has bare stretches and thick stands rather than one
    //even tiling of them.
    //
    //SEPARATE TUSSOCKS ON BARE SAND, which is what the references drew and what this was not: it cut a soft
    //0.3-wide band out of the field and then scaled the result by the cover and the patch mask, so a sparse
    //stand came out as FAINT tussocks rather than as fewer of them, and the ground read as an olive mottle over
    //brown. The mask and the cover now set each tussock's RADIUS - where the patch runs thin there are small
    //ones and then none - and the edge is a narrow band. Sized up to be unmistakable, the first cut read as
    //polka dots, the savanna's tuft-disc failure, so they stay small and patchy. Seen in the game they then read
    //as BEANS - see the fray, the dome and SpinifexColor below for the three things that took that away.
    //
    //A RAW VORONOI FIELD IS A HONEYCOMB, and it drew one: its sites are one per cell jittered by only 0.4, so
    //the tussocks came out on a near-perfect hexagonal packing with the bare ground between them reading as a
    //connected web — the plane-wave-sine failure in cellular form. Two things break it and both are needed.
    //The domain is WARPED by a field running at about a third of the hummock spacing, which slides
    //neighbouring sites off their lattice rows; and the same field jitters the THRESHOLD, so hummocks vary in
    //size instead of tiling at one radius. (One noise evaluation does both: a second one for a properly
    //isotropic warp measured as no better a picture than shearing the one.)
    float2 spinifexP = worldPosition.xz / SpinifexSpacing;
    float wander = GradientNoise2(spinifexP * 0.34 + 17.0);

    spinifexP += float2(wander, wander * 0.62 - 0.31) * 1.15;

    //The patch mask and the cover set the radius, in cells; the wander jitters it so no two are one size.
    //Below a radius of a twelfth of a cell a tussock is taken away outright rather than left as a dot.
    float patch = saturate(GradientNoise2(worldPosition.xz * 0.035 + 31.4) * 2.8 + 0.15);
    float tussockRadius = SpinifexCover * patch * (0.28 + wander * 0.14);
    float present = saturate(tussockRadius * 12.0);

    //The RIM is band-limited, not the tussock. A cellular field's finest feature is the width of the band its
    //threshold cuts, not the cell - so a fade keyed to the cell would leave the rim aliasing for the whole
    //distance in between. Widening the band by the pixel keeps a distant tussock a soft blob of the right
    //average brightness; the whole field then fades out once the CELLS approach pixel size, and finishes before
    //they reach it - the desert's grain rule, since a hard-edged cellular field is its own aliasing source.
    //It fades to the field's own AVERAGE cover (a disc's area in a unit cell), not to bare sand, or the far
    //plain would turn redder than the near one exactly where the tussocks give out.
    float aa = min(footprint / SpinifexSpacing, 0.35);
    float detail = saturate(1.0 - footprint * 2.5 / SpinifexSpacing);
    float cellDistance = Voronoi2(spinifexP);

    //RAGGED, not round: spinifex is a ball of spines, and an exact disc of it is a pebble - which is what a field
    //of them read as, grey beans on the sand. A fine noise pushes the distance in and out a few times round
    //each tussock so its outline frays; faded with the pixel like everything else here, so a far tussock is a
    //soft blob rather than a shimmer.
    const float frayFrequency = 2.4;
    cellDistance += GradientNoise2(worldPosition.xz * frayFrequency) * 0.16 * saturate(1.0 - footprint * frayFrequency * 2.0);

    float tussock = (1.0 - smoothstep(tussockRadius - 0.14 - aa, tussockRadius + 0.05 + aa * 2.0, cellDistance)) * present;
    float hummock = lerp(saturate(PI * tussockRadius * tussockRadius), tussock, detail);

    //The shade a tussock throws on the sand round its foot - a ring rather than a cast shadow, because a cast
    //one would be a second Voronoi at an offset point, nine more hashes, for something this small.
    float ringRadius = tussockRadius + 0.09;
    float ring = lerp(saturate(PI * ringRadius * ringRadius) * present,
        (1.0 - smoothstep(tussockRadius, tussockRadius + 0.18 + aa * 2.0, cellDistance)) * present, detail);

    //A tussock is a little lighter at its crown, where the spinifex's pale tips catch the sky. Only a little:
    //darkened hard at its skirt it drew a dark-rimmed disc, and under a low sun a field of those read as seeds
    //or as pits in the sand. The dome below is what gives it a lit side and a shaded one.
    float tip = 1.0 - smoothstep(0.0, tussockRadius * 0.9 + aa, cellDistance);
    float3 tussockColor = SpinifexColor * lerp(0.95, 1.15, lerp(0.5, tip, detail));

    //THE TUSSOCK IS A DOME, not a plateau. Its relief used to be the colour mask itself - flat on top with a
    //cliff at the rim - so the normal turned only in a thin ring, and every tussock came out as a flat pill
    //with a dark crescent on one edge. (1 - x^2)^2 is round over the top and meets the sand with no crease,
    //so the whole clump takes the sun on one side and turns away from it on the other.
    float domeReach = saturate(cellDistance / max(tussockRadius + 0.03, 1e-3));
    float dome = saturate(1.0 - domeReach * domeReach);
    dome *= dome * present;

    float3 soil = lerp(SoilColor, SoilColorPale, saturate(broad * -1.7 + 0.5));

    //The gibber: the pebble litter that covers a real outback plain, one hash per pixel over a fine lattice,
    //gone within a few units. Same rule as the grain - the fade finishes while a cell is still two pixels wide.
    float grainFade = saturate(1.0 - footprint * 90.0);
    soil *= 1.0 + NoiseHash22(floor(worldPosition.xz * 45.0)).x * 0.20 * grainFade;
    soil *= 1.0 - 0.3 * saturate(ring - hummock);

    float3 ground = lerp(soil, tussockColor, hummock);

    //--- Normal and colour -----------------------------------------------------------------------------
    //ONE height field for the fine relief and ONE perturbation off it, the two materials' reliefs lerped by the
    //same mask their colours are. Two PerturbNormalFromHeight calls would be two more pairs of screen
    //derivatives for a result that is a lerp of the inputs anyway. The tussocks' relief takes the dome faded by
    //the detail, not the averaged cover - an average cover is a colour, and has no relief to give.
    float relief = lerp(dome * detail * SpinifexRelief, rockSurface * RockRelief, rockMask);
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    float3 albedo = lerp(ground, rock, rockMask);

    //Shading in the gullies, independent of the sun: the cheapest ambient occlusion there is, and what makes
    //the rock read on the faces the sun is NOT raking. Without it a shadowed flank goes back to being a flat
    //patch of colour, which is exactly how the desert's dunes read before it. Deeper where `rib` is high,
    //because that is where the gullies are (see RockLayer) - the shipped term lit them and shaded the spurs.
    //The hollows at the foot take their shade here too. The ground carries its own shading now, in the ring
    //and the tussock's crown, so it is left out of this.
    albedo *= lerp(1.0, (0.72 + 0.36 * saturate(0.5 - rib * 0.6 + rockSurface * 1.4)) * (1.0 - cave * CaveShade),
        rockMask);

    //--- Lighting ------------------------------------------------------------------------------------
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows (#471), into the same sunlight factor the clouds dim, so everything read off it
    //is shadowed at once. What casts here: the island and the gun on the spinifex — the monoliths are the
    //terrain itself and already shade their own lee.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light: up-facing ground takes the zenith, faces turned to the skyline take the horizon
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    //The bounce off the lit sand (see SoilBounce). It goes where the sun goes, since it is the sun's light at
    //second hand; it is strongest on the faces the sun does not reach; and it is weighted by how far a face
    //turns towards the GROUND, which is where it comes from - so the walls take it and the flat plain, which
    //faces the sky, takes none and keeps exactly the colour it had.
    float3 bounce = SunColor * saturate(SunDirection.y + 0.2) * SoilColorPale * SoilBounce
        * saturate(1.0 - normal.y) * (1.0 - ndotl * sunlight);

    float3 color = albedo * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight + bounce);

    //The varnish glint. Desert varnish is a genuinely glossy skin and the only thing in this scene that is not
    //matte, so the lobe is tighter than the desert's grain sheen and it is gated on BOTH the varnish and the
    //rock mask - a glossy plain would read as wet ground, which is the one thing the outback is not. Gated on
    //ndotl as well, or a lee face would glint in light that never reaches it.
    //
    //VarnishGloss came down from 0.5 when the varnish moved onto the whole wall: a glint that was a highlight
    //on a few gully lines painted every streak on a sunlit face WHITE, the exact opposite of a water stain.
    float3 halfway = normalize(SunDirection + towardsEye);
    float gloss = pow(saturate(dot(normal, halfway)), 34.0);

    color += SunColor * gloss * VarnishGloss * varnish * rockMask * ndotl * sunlight;

    //--- The air -------------------------------------------------------------------------------------
    //Aerial perspective through red dust. No drifting veil (the Sahara's costs a noise evaluation over the
    //whole frame and the outback's air is still) — this rides the distance fade and is free.
    //
    //THE FADE IS IN TWO STAGES, the forest's arrangement and for the forest's reason: one colour cannot do
    //both jobs. The mid-distance has to keep the scene's own red, or a dome with a teal horizon paints the far
    //plain and the shadowed flank of every monolith green — which is aerial perspective behaving correctly and
    //still the wrong picture. The last stretch has to meet the sky, or the terrain's own edge shows as a seam —
    //which is what a single fade to a dust colour drew, a red plain butting straight onto a green sky. So the
    //dust builds up quadratically, and then a murk takes over on a quartic that only bites in the final fifth.
    //
    //THE MURK IS NOT HorizonColor, which is what that last stage faded to, on the argument that matching it
    //hides the grid's edge against the dome. It does not hold, and the desert found out why first: under dome 13
    //the uniform is teal while the dome DRAWS its horizon lilac-grey, so the far plain stood against the sky as
    //a teal band that matched nothing. The murk is the horizon's brightness in the dust's own hue, leaned
    //towards by HazeWarmth - what stands there now is a plain going into its own red haze.
    //
    //The dust takes the sky's BRIGHTNESS in full and only half of its hue, which is the forest's sky-tint trick
    //run the other way round. Lit by the sky's colour outright — `HazeTint * skyLight`, the obvious spelling —
    //the product keeps whichever channel the dome happens to be strongest in, so a teal-horizoned dome handed
    //back a green dust however hard DustStrength was pushed: a multiply cannot be out-weighted by a blend whose
    //other end carries the same hue.
    float3 skyLight = HorizonColor + SunColor * 0.35;
    float skyLuminance = dot(skyLight, float3(0.2126, 0.7152, 0.0722));

    float3 dustLit = HazeTint * lerp(skyLuminance.xxx, skyLight, 0.45);

    float haze = saturate(dist / HorizonHazeDistance);

    //HEAT SHIMMER, and it is keyed to the VIEW RAY rather than to the surface — which is the whole of what was
    //wrong with it first time. Written against `worldPosition.y` it painted the noise ONTO whatever it hit, and
    //on a near-vertical monolith flank that is a set of horizontal stripes one world unit apart: fine, regular,
    //strongest exactly where the haze is half done, and reading as a shading bug rather than as air. The
    //distortion belongs to the atmosphere the ray passes THROUGH, so its domain is the ray's own direction —
    //features of a fixed angular size, sliding upward against the clock the way hot air rises, and sticking to
    //nothing. It belongs to the MIDDLE distance alone: `haze * (1 - haze)` peaks where the fade is half done
    //and dies at both ends, so the near ground stays rock-steady and the skyline stays clean.
    float3 ray = -towardsEye;
    float2 shimmerP = float2(dot(ray.xz, WindDirection) * 44.0 + ray.x * 30.0,
        ray.y * 90.0 - OutbackTime * 0.55);
    float shimmer = GradientNoise2(shimmerP) * HeatShimmer * haze * (1.0 - haze) * 4.0;

    haze = saturate(haze + shimmer);

    const float3 luminanceWeights = float3(0.2126, 0.7152, 0.0722);
    float3 hazeHue = HazeTint / max(dot(HazeTint, luminanceWeights), 1e-3);
    float3 murk = lerp(HorizonColor, hazeHue * dot(HorizonColor, luminanceWeights), HazeWarmth);

    color = lerp(color, dustLit, DustStrength * haze * haze);
    color = lerp(color, murk, haze * haze * haze * haze);

    return float4(color, 1.0);
}

technique Outback
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL OutbackVS();
        PixelShader = compile PS_SHADERMODEL OutbackPS();
    }
};
