//Draws a forest clearing: a mossy needle-strewn floor whose hills dress in a dark conifer canopy, the
//round stone island standing in the middle of the open ground. The eighth SceneKind, and past the end of
//the NumPad2/V cycle (which still runs % 7, over the seven scenes a map is authored against), so
//"scene=forest" on the command line is how it is reached. The look is a temperate woodland glade - cool
//low green undergrowth, darker and more shadowed than the meadow, combed by the same wind and shaded by
//the same drifting clouds. No wildflowers: a forest floor is leaf litter and moss, not a lawn of blooms,
//so the colour work is in the patchy undergrowth, the treeline and the fine needle relief rather than in
//scattered rosettes.
//
//Real geometry like the meadow - a camera-centred grid (shared CreateGridMesh on the C# side) displaced
//by a smooth rolling field, low around the arena and rising into tree-covered hills with distance, its
//normal taken by finite differences. The scattered trees, rocks and stumps that stand ON this floor are
//the Game's own instanced draws (ForestScatter over TerrainMirror.Forest, a CPU mirror of
//TerrainHeight below - keep the two in one change), so the other two executables draw the bare clearing.
//Drawn in all three executables, Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"
//The sun's cast shadows (#469, in this scene since #471): SceneRenderer renders the map before the scene
//pass and this reads it. See Shadows.fxh for what casts and what a receiver owes.
#include "Shadows.fxh"
//For WindGust (#276) — the one copy of the gust field the meadow and the savanna also read the wind off.
//Since #281 the floor also draws its litter, moss, bilberry and dry grass from Fbm2BandLimited, Fbm2Combed and
//GradientNoise2 here; the four-sine needle field and the private ForestFbm it replaced are gone.
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

#include "FarField.fxh"
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

float ForestLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

//Low-amplitude lumps across the floor - root bulges and the uneven ground a real clearing has, finer and
//closer than the rolling hills. Sampled three times per vertex for the finite-difference normal.
float FloorLumpStrength;
float FloorLumpFrequency;

float ForestTime;
float2 WindDirection;

//Undergrowth (linear): the cool low green the floor varies towards in patches, and the darker shade of the
//needle litter and shadow, how much sky fills the flats (less than the meadow - a clearing is shaded), and
//the distance over which the field melts into the skyline
float3 ForestColor;
float3 ForestColorDark;
float AmbientStrength;
float HorizonHazeDistance;

//The wooded hills: past the clearing the ground dresses in a dark conifer-canopy colour (linear), mottled
//at grove and crown scale so it reads as treetops rather than dark paint. The scattered tree meshes stop
//well before the horizon; this is what carries the forest to the skyline. Strength 0 leaves bare hills.
float3 TreelineColor;
float TreelineStrength;

//Wind combing the undergrowth: how fast the bright/dark bands travel, how far apart they are, how deep they cut
float WindRippleSpeed;
float WindRippleFrequency;
float WindRippleStrength;

//Fine needle/moss texture (a normal-tilting height field), its amplitude and blades-per-world-unit - stronger
//than the meadow's grass relief, because a needle floor reads coarser than a lawn
float NeedleReliefStrength;
float NeedleReliefFrequency;

//What lies on the floor (#281), read off references rendered for it: a carpet of rusty needle litter with darker
//decayed patches and bare earth, cushions of moss (ForestColor/ForestColorDark are their lit and shaded greens),
//dark patches of bilberry, and pale dry grass where the clearing stands open to the sun. Linear albedo.
float3 LitterColor;
float3 LitterColorDark;
float3 EarthColor;
float3 UndergrowthColor;
float3 DryGrassColor;
float MossCoverage;
float UndergrowthCoverage;
float DryGrassCoverage;
float MossHeight;

//The floor's two expensive extras - the triplanar normal variation and the procedural tree shadows - are
//switched by TECHNIQUE rather than by a uniform, and they go together. Both facts are measured rather than
//tidy. Front end, 1600x900, desktop GPU, forest under dome 13, nocap: all on 2.69 ms, both gone 2.09.
//Cutting either one ALONE saves NOTHING - 2.71 without the normal variation, 2.72 without the shadows - and
//dropping the floor's FBM from four octaves to two saves nothing either (2.71). The pass is occupancy-bound
//rather than work-bound, which is what makes a per-feature dial useless here, and what makes a runtime
//branch useless too: see ForestPS's `detail` parameter for the 2.72-against-2.09 that settled it.

//Procedural tree-shadow tuning (ForestShadow). The cell is the spacing of the virtual trees the hash grid
//plants; it sits inside the scattered wood's own spacing so the two read as the same forest rather than two
//different ones laid over each other. A tree is a crown, a SPHERE (#648): centred FOREST_CROWN_CENTRE of the
//tree's height above the ground and as wide as the hash says (FOREST_CROWN_WIDE_MIN..MAX of the height, capped at
//FOREST_CROWN_MAX_RADIUS so the 3x3 cells searched always hold every crown that can shade a point). Strength is the
//darkest a crown's middle can make the floor, and it and whether the cell holds a tree at all (FOREST_CELL_PLANTED
//of them do) are the hash's too: a grid of equal crowns with equal shadows reads as polka dots, not as a wood.
static const float FOREST_SHADOW_CELL = 9.0;
static const float FOREST_SHADOW_MIN_H = 5.0;
static const float FOREST_SHADOW_MAX_H = 11.0;
static const float FOREST_CROWN_CENTRE = 0.62;
static const float FOREST_CROWN_WIDE_MIN = 0.40;
static const float FOREST_CROWN_WIDE_MAX = 0.60;
static const float FOREST_CROWN_MAX_RADIUS = 5.5;
static const float FOREST_CELL_PLANTED = 0.62;
static const float FOREST_SHADOW_STRENGTH_MIN = 0.40;
static const float FOREST_SHADOW_STRENGTH_MAX = 0.80;

#include "ForestGround.fxh"

struct ForestVertexInput
{
    float4 Position : POSITION0;
};

struct ForestVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

ForestVertexOutput ForestVS(ForestVertexInput input)
{
    ForestVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float3 worldPosition = float3(xz.x, TerrainHeight(xz), xz.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//Procedural tree shadows on the forest floor, after dr2's ObjSShadow but without a distance field: the trees
//are VIRTUAL, placed by a hash grid (CloudHash22) the way dr2's SetTrParms places one per hex cell, rather than
//the instanced meshes' real positions - the terrain shader does not know where those stand, and passing ~240 of
//them in is a limit and a cost this does not need. What the floor gets is shadow that READS as woods: dappled
//where the canopy is open, denser under a stand, swept down-sun the way a real shadow is. The scatter's real
//trees stand closer in (inside ClearingRadius) where density is 0, so the two never argue about who shadows whom.
//
//A cell holds one tree: a hash picks its offset within the cell, its height, its crown's width, whether it is
//there at all and how dark its shadow lands. The crown is a sphere. A floor point is shaded by the crown its ray
//to the sun passes through, and how deep by the length of the chord the ray cuts through it (nothing at the rim,
//most through the middle), so a shadow is a soft ellipse - the way a real crown's is - and not a hard-edged strip.
//
//⚠ THE FIRST BUILD DREW SQUARES (#648). It made the crown a cylinder, so a shadow was a parallelogram with straight
//sides that no crown throws, and it marched the sun ray in steps and tested only the tree of the cell each step
//landed in, so a crown wider than the distance to its cell's edge (most of them) was cut off along that edge:
//dark trapezoids with a straight end on the grid, on ground with no tree to explain them. Its heights were also
//taken against the world's y and not the ground's, and the hash's -1..1 was used as if it were 0..1 (half the
//heights were negative), so the wood shaded only the higher hills. Now: a fixed 3x3 block of cells around the
//point the ray reaches at an average crown's height - every crown that can shade the point is in it, whichever
//cell the ray happens to pass through - each crown's height and width read off the hash remapped to 0..1, and
//heights taken above the ground at the shaded point (a wooded slope's trees stand on the slope).
float ForestShadow(float3 worldPosition, float3 sunDir, float density)
{
    //A flat clearing (density 0) is in full sun: the wood's trees stand outside it, and the procedural wood
    //begins where density rises. This one is NOT the uniform branch CLAUDE.md's convention describes - density
    //is canopyRamp, which varies per pixel - so it does diverge, along the one ring of pixels at the clearing's
    //edge where neighbours disagree. It is still the right shape: nothing below has a gradient op in it (no
    //sampling, only arithmetic and CloudHash22), so nothing here needs neighbouring lanes to have taken it, and
    //the whole clearing interior - most of the floor the player ever sees up close - skips the crowns outright.
    [branch]
    if (density <= 0.001) return 1.0;

    float grid = FOREST_SHADOW_CELL;

    //Where the ray from the floor point climbs to an average crown's centre, and the cell that is over. A low sun
    //carries that a long way across the ground; the floor on the sun's ray is never asked to climb less than 0.1.
    float averageCentre = 0.5 * (FOREST_SHADOW_MIN_H + FOREST_SHADOW_MAX_H) * FOREST_CROWN_CENTRE;
    float2 reach = worldPosition.xz + sunDir.xz * (averageCentre / max(sunDir.y, 0.1));
    float2 baseCell = floor(reach / grid);

    float shadow = 1.0;

    [unroll]
    for (int j = -1; j <= 1; j++)
    {
        [unroll]
        for (int i = -1; i <= 1; i++)
        {
            float2 cell = baseCell + float2(i, j);
            float2 h = CloudHash22(cell * 7.0 + 13.0);
            float2 unit = h * 0.5 + 0.5;

            //Whether the cell holds a tree and how dark its shadow is, from the same hash's low bits: a second hash
            //a crown would cost about what the whole crown does
            float2 other = frac(h * float2(43.7, 91.3) + h.yx * 17.9);

            float2 treeXZ = (cell + 0.5 + 0.36 * h) * grid;
            float treeHeight = lerp(FOREST_SHADOW_MIN_H, FOREST_SHADOW_MAX_H, unit.x);
            float centreHeight = treeHeight * FOREST_CROWN_CENTRE;
            float crownRadius = min(treeHeight * lerp(FOREST_CROWN_WIDE_MIN, FOREST_CROWN_WIDE_MAX, unit.y), min(FOREST_CROWN_MAX_RADIUS, centreHeight * 0.95));

            //The crown's centre relative to the floor point, and the sun's ray from it: the ray's distance from the
            //centre is what is left of the offset once its component along the ray is taken out, and the crown is
            //towards the sun when that component is positive
            float3 toCrown = float3(treeXZ.x - worldPosition.x, centreHeight, treeXZ.y - worldPosition.z);
            float along = dot(toCrown, sunDir);
            float missSquared = dot(toCrown, toCrown) - along * along;
            float chord = sqrt(saturate(crownRadius * crownRadius - missSquared)) / crownRadius;

            //Only a crown towards the sun from the point shades it, soft at the rim, and only in a cell that holds a
            //tree; how dark it is is that tree's own
            float occlusion = smoothstep(0.0, 0.8, chord) * step(0.0, along) * step(other.x, FOREST_CELL_PLANTED);
            float strength = lerp(FOREST_SHADOW_STRENGTH_MIN, FOREST_SHADOW_STRENGTH_MAX, other.y);
            shadow = min(shadow, 1.0 - strength * occlusion);
        }
    }

    //Density shapes how deep the shadow lands: a clearing (0) is untouched, full wood (1) gets the lot.
    return lerp(1.0, shadow, density);
}

//`detail` is an ordinary function argument passed a LITERAL by each of the two entry points below, so it is
//constant-folded at compile time and each of them comes out a SEPARATE PROGRAM - the reduced one with the
//expensive halves gone and, crucially, with its own register allocation.
//
//It began as a `float FloorDetail` uniform with two [branch]es on it, which measured EXACTLY NOTHING: 2.72 ms
//against the full look's 2.70, where deleting the same two pieces of code measures 2.09. That is what an
//occupancy-bound pass does - the register footprint is decided for the whole shader when it is compiled, so
//a runtime branch skips the WORK and keeps the REGISTERS, and the occupancy that was the real limit never
//rises. A cost of that shape can only be bought back by compiling a different program.
//
//And it is two entry points rather than the tidier `compile PS_SHADERMODEL ForestPS(true)`: MGFX cannot
//parse a uniform argument in a compile statement ("Unexpected token 't' found. Expected CloseParenthesis").
float4 ForestFloor(ForestVertexOutput input, bool detail)
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the terrain (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);
    FarRingClip(worldPosition.xz);

    float3 baseNormal = TerrainNormal(worldPosition.xz);
    float footprint = length(fwidth(worldPosition.xz));
    float2 xz = worldPosition.xz;

    //Slope drives what grows where, the way it does on a real bank: the flats keep the moss and the bilberry,
    //the banks wash to litter and bare earth. Without it the floor reads as one material however it is lit.
    float slope = smoothstep(0.55, 0.85, baseNormal.y);

    //WHAT LIES ON THE FLOOR (#281), read off references rendered for it. The first build gave every layer a field
    //of its own and cost 0.64-1.04 ms more than the green floor it replaced (3200x1800, three pairs); three fields
    //read for several things each draw the same picture. `broad` is how damp the ground is - bilberry and moss
    //gather where it is high, dry grass where it is low, which is also how a real clearing sorts them; `tone` is
    //the litter's own mottle, and where it is lowest the litter has worn through to earth; `fine` breaks the
    //cushions' edges, clumps the bilberry and varies the moss, and fades out before it could shimmer.
    float broad = Fbm2BandLimited(xz * 0.06 + 19.0, 2, footprint * 0.06);
    float tone = Fbm2BandLimited(xz * 0.22 + 7.0, 2, footprint * 0.22);
    float fine = GradientNoise2(xz * 1.6 + 67.0) * saturate(1.0 - footprint * 1.6);

    //MOSS CUSHIONS: rounded islands off a thresholded noise, their outline broken by `fine` because a clean
    //threshold drew them as paint splotches. Worked out before the normal, because a cushion is a low dome and
    //rides in the relief. Past a few cushions a pixel the mask fades to its own average rather than to speckle.
    float mossResolved = saturate(1.6 - footprint * 0.45);
    float mossField = GradientNoise2(xz * 0.2 + 13.0) + 0.45 * GradientNoise2(xz * 0.47 + 41.0) + 0.22 * fine;
    float mossThreshold = 0.45 - MossCoverage - 0.6 * broad;
    float moss = lerp(MossCoverage * 0.5, smoothstep(mossThreshold, mossThreshold + 0.3, mossField), mossResolved) * slope;

    //The litter's own relief and the cushions' rise tilt the normal, so the floor catches the light unevenly.
    //There is no second, finer normal layer any more: VaryNormal's triplanar FBM (sixteen noise taps a pixel)
    //was added over the sine relief to break it up, and the relief is noise itself now.
    float relief = NeedleRelief(xz, footprint) + moss * mossResolved * MossHeight;
    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

    //THE FLOOR IS NOT GREEN. It was moss green wherever it was flat, and the owner's playtest said a real forest
    //floor is not that green; the references agree - under spruce the ground is a carpet of rusty needles, with
    //the green in cushions and patches on top of it. So the base is the litter, in two tones, and everything green
    //is laid over it.
    float3 floor = lerp(LitterColorDark, LitterColor, saturate(0.55 + 0.9 * tone));

    //The needles themselves, near the lens: noise drawn out along a direction that wanders with the litter's mottle,
    //so the carpet reads as strands lying every which way rather than as smooth brown. Gone well before a strand
    //is under a pixel. Behind FloorDetail with the tree shadows - the pair the reduced tier gives up.
    if (detail)
    {
        float needles = Fbm2Combed(xz * 5.0, float2(cos(tone * 12.0), sin(tone * 12.0)), 6.0, 1, footprint * 5.0);
        floor *= 1.0 + 0.35 * needles * saturate(1.5 - footprint * 4.0);
    }

    //Bare dark earth where the litter is thinnest, more of it on the banks
    float earth = smoothstep(0.3, 0.55, -tone) * (1.0 - 0.5 * slope);
    floor = lerp(floor, EarthColor, earth * 0.8);

    //Bilberry: low dark-green patches several units across on the damp ground, broken into leafy clumps up close
    float leafy = 0.75 + 0.5 * fine;
    float undergrowth = smoothstep(-0.1, 0.15, broad + UndergrowthCoverage - 0.5) * lerp(0.5, 1.0, slope);
    floor = lerp(floor, UndergrowthColor * leafy, undergrowth);

    //Pale dry grass on the dry ground where the clearing stands open to the sun, fading out towards its edge
    float open = 1.0 - smoothstep(ClearingRadius * 0.45, ClearingRadius * 0.85, length(xz));
    float dry = smoothstep(-0.1, 0.2, DryGrassCoverage - 0.5 - broad) * open * (1.0 - undergrowth);
    floor = lerp(floor, DryGrassColor * (0.85 + 0.3 * leafy), dry);

    //The moss last, over everything, with a darker contact rim in the litter round each cushion - the cue that it
    //stands on the floor rather than being painted on it
    float rim = saturate(moss * (1.0 - moss) * 4.0) * mossResolved;
    floor *= 1.0 - 0.3 * rim;
    floor = lerp(floor, lerp(ForestColorDark, ForestColor, saturate(0.5 + 0.8 * fine)), moss);

    //Wind over the clearing, and on a forest floor this is the ONLY thing the wind does: patches of shade
    //running across the ground as the canopy moves over it. Same dial and the same range it always had; what
    //it is applied to is a travelling gust rather than an infinite plane wave 42 world units across, which
    //at this scene's scale meant the whole clearing dimming and brightening under one straight edge
    //(#276 — see WindGust in Noise.fxh). The speed is the old wave's own phase speed, so it crosses the
    //clearing at the rate the dials always meant.
    float wind = WindGust(worldPosition.xz, WindDirection, ForestTime, WindRippleFrequency,
        WindRippleSpeed / max(WindRippleFrequency, 1e-4), footprint);
    floor *= 1.0 + wind * WindRippleStrength;

    //Needle-scale colour grain, the finest layer: twigs, cones and litter flecks at arm's length, gone by
    //the middle distance (band-limited to nothing before it can shimmer). This is the layer whose absence
    //read as "out of focus" up close - the relief tilts the light, but a floor with no fine ALBEDO change
    //still looks airbrushed however it is lit. Spread hard, the CloudNoise rule above.
    float grainFade = saturate(1.0 - footprint * 2.4);
    float grain = CloudNoise(worldPosition.xz * 2.6) * 1.8;
    floor *= 1.0 + 0.22 * grain * grainFade;

    //The wooded hills: away from the clearing the undergrowth gives way to the dark canopy of the trees
    //covering them. The treeline has its own ramp, tighter and NEARER than the hills': the slopes that
    //fill the frame are the hills' transition band itself, and a canopy keyed to that full ramp only
    //arrives where the haze already owns the colour - so this one starts INSIDE the clearing, three
    //quarters of the way out (under the first scattered trees, which stand in front of it) and is
    //complete a little past the clearing's edge. Its edge rides the grove noise, so the woods begin on a
    //ragged line rather than a drawn circle. Two mottle scales: broad grove-sized patches (lit stands
    //against shadowed ones) and a finer crown-sized grain. Multiplied rather than blended so the canopy
    //keeps its darks.
    float grove = saturate(CloudNoise(worldPosition.xz * 0.035 + 11.0) * 1.7 + 0.5);
    float crowns = saturate(CloudNoise(worldPosition.xz * 0.17 + 73.0) * 1.7 + 0.5);
    float treeDist = length(worldPosition.xz) + (grove - 0.5) * 40.0;
    float canopyRamp = smoothstep(ClearingRadius * 0.75, ClearingRadius + ClearingTransition * 0.45, treeDist);
    //Wide multiplicative swings: after the ACES curve a timid mottle flattens into one tone, and it is
    //the swing between sunlit stands and shadowed ones that says "treetops" at this distance. A third,
    //crown-top grain under a band limit gives the near slopes individual treetops without turning the far
    //ridge into shimmer - the two broad scales alone read soft-focus exactly where the hills fill the frame.
    float crownTops = saturate(CloudNoise(worldPosition.xz * 0.45 + 31.0) * 1.7 + 0.5);
    float crownFade = saturate(1.0 - footprint * 0.45);
    float3 canopy = TreelineColor * (0.4 + 1.2 * grove) * (0.55 + 0.9 * crowns)
        * (1.0 - (0.25 - 0.5 * crownTops) * crownFade);
    floor = lerp(floor, canopy, canopyRamp * TreelineStrength);

    //Matte forest floor: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds
    //that drift across the sky sweep their shadows over the clearing, AND by the procedural tree shadow so the
    //woods cast their own dapple. The tree shadow's density rides the canopy ramp - 0 in the open clearing (the
    //scatter's real trees stand there, and theirs is the only shadow that should show), rising to full under the
    //procedural wood beyond it. Lower ambient than the meadow: a clearing is shaded by the trees around it.
    float sunlight = CloudSunlight(worldPosition, SunDirection);

    //The sun's cast shadows (#471), into the same sunlight factor the clouds dim, so everything read off it
    //is shadowed at once. What casts here: the wood's own trees, boulders and stumps, which stand in the
    //clearing the procedural canopy shadow below deliberately leaves alone.
    [branch]
    if (ShadowStrength > 0.0)
        sunlight *= SunShadow(worldPosition, baseNormal, SunDirection);

    //The other extra behind FloorDetail. The cloud shadow above is NOT given up with it: that one is shared
    //with every other scene and costs a fraction of this, and a clearing with no drifting shade at all reads
    //as a different weather rather than as a cheaper frame.
    float treeShadow = 1.0;

    if (detail) treeShadow = ForestShadow(worldPosition, SunDirection, canopyRamp);
    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = floor * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight * treeShadow);

    //Horizon haze in two stages. Straight to the horizon colour - the one stage every other terrain
    //uses - the dark wooded hills bleach cream long before the skyline and read as bare slopes; what
    //distant forested ridges actually do is turn BLUE, because the air between scatters skylight. So
    //the hills first recede into a murk built mostly from the zenith (tinted green so the canopy keeps
    //reading through it; a fraction of the dome's own colours, so a dusk's murk is dark), and only the
    //last stretch melts into the horizon itself - reaching it exactly at the haze distance, which is
    //what keeps the terrain grid's edge invisible against the dome behind it.
    float dist = distance(CameraPosition, worldPosition);
    float haze = saturate(dist / HorizonHazeDistance);
    float3 murk = (ZenithColor * 0.7 + HorizonColor * 0.3) * float3(0.5, 0.65, 0.58);
    color = lerp(color, murk, saturate(haze * haze * 1.2) * 0.6);
    float skyward = haze * haze;
    skyward *= skyward;
    color = lerp(color, HorizonColor, skyward);

    return float4(FarFadeToSky(color, worldPosition), 1.0);
}

//Two programs from one body. "Forest" is the authored floor; "ForestReduced" is the same floor without the
//triplanar normal variation and without the procedural tree shadows - the pair that has to go together,
//since removing either alone saves nothing. The caller picks by tier; SceneRenderer.TerrainDetail decides.
float4 ForestPS(ForestVertexOutput input) : COLOR { return ForestFloor(input, true); }
float4 ForestReducedPS(ForestVertexOutput input) : COLOR { return ForestFloor(input, false); }

technique Forest
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ForestVS();
        PixelShader = compile PS_SHADERMODEL ForestPS();
    }
};

technique ForestReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ForestVS();
        PixelShader = compile PS_SHADERMODEL ForestReducedPS();
    }
};

//--- The height probe (#590) ----------------------------------------------------------------------------

//TerrainMirror.Forest's field, for the Testbed's mirrorcheck (see HeightProbe.fxh).
#define HEIGHT_PROBE_MIRRORED(p) TerrainHeight(p)
#include "HeightProbe.fxh"
