using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the forest backdrop: a mossy needle-strewn clearing ringed by dark wooded hills,
    /// combed by wind, shaded by the trees around it. The eighth scene; a temperate woodland glade to the
    /// meadow's open lawn - cooler, darker, and floorless of blooms.
    /// </summary>
    public sealed class ForestSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Forest;

        /// <summary>
        /// Under a canopy the sky is glimpsed rather than seen, and a broken deck is what those glimpses are of.
        /// </summary>
        public ForestSceneConfig()
        {
            Weather = WeatherPreset.Broken;

            //The sun's cast shadows (#471), at the savanna's own figures — 0.9 is a shadow that is dark without
            //reading as a hole, and 260 units at 2048 is 0.13 units a texel. The densest scatter in the
            //project: 380 trees, boulders and stumps, which until #471 stood on an evenly lit floor. Under a
            //broken deck the sun is intermittent, which is what a wood looks like.
            Shadows = new ShadowConfig(strength: 0.9f);
        }

        /// <summary>Basin the arena sits in, rising into wooded hills with distance; below the island top.</summary>
        public float LevelY { get; set; } = -14f;

        /// <summary>Peak height of the wooded hills that rise with distance (the treeline). Taller than the
        /// meadow's rolls: the ring of hills is the forest's whole backdrop, so their crests have to stand
        /// against the sky from the low play camera.</summary>
        public float HillHeight { get; set; } = 58f;

        /// <summary>Flat clearing radius around the arena centre before the hills begin.</summary>
        public float ClearingRadius { get; set; } = 95f;

        /// <summary>
        /// Distance over which the flat clearing ramps up into hills. Short enough that the treeline arrives
        /// well inside <see cref="HorizonHazeDistance"/> — with a long ramp the canopy colour only reaches
        /// full strength where the haze has already washed it out, and the "wooded" hills read bare.
        /// </summary>
        public float ClearingTransition { get; set; } = 90f;

        /// <summary>Gentle basin relief within the clearing.</summary>
        public float ClearingRelief { get; set; } = 1.5f;

        /// <summary>
        /// Low lumps across the clearing floor - root bulges and the uneven ground a real clearing has, finer
        /// and closer than the rolling hills. A meadow is mown flat; a forest floor is not.
        /// </summary>
        public float FloorLumpStrength { get; set; } = 1.2f;

        /// <summary>Frequency of the floor lumps (lumps per ~world-unit band).</summary>
        public float FloorLumpFrequency { get; set; } = 0.06f;

        /// <summary>
        /// The lit green of the moss cushions on the floor (linear). It was the floor's own dominant colour until
        /// #281, which the owner's playtest found far too green for a forest floor; the floor is needle litter now
        /// and this is the moss laid over it.
        /// </summary>
        public Rgb ForestColor { get; set; } = new(0.05f, 0.1f, 0.028f);

        /// <summary>The shaded green of the moss cushions (linear).</summary>
        public Rgb ForestColorDark { get; set; } = new(0.022f, 0.048f, 0.016f);

        /// <summary>The rusty spruce-needle litter the floor is a carpet of (linear).</summary>
        public Rgb LitterColor { get; set; } = new(0.13f, 0.078f, 0.04f);

        /// <summary>The darker, decayed litter it mottles towards (linear).</summary>
        public Rgb LitterColorDark { get; set; } = new(0.055f, 0.037f, 0.024f);

        /// <summary>Bare earth where the litter has worn through (linear).</summary>
        public Rgb EarthColor { get; set; } = new(0.035f, 0.025f, 0.017f);

        /// <summary>The low bilberry undergrowth (linear).</summary>
        public Rgb UndergrowthColor { get; set; } = new(0.034f, 0.062f, 0.022f);

        /// <summary>Dry grass where the clearing stands open to the sun (linear).</summary>
        public Rgb DryGrassColor { get; set; } = new(0.22f, 0.17f, 0.08f);

        /// <summary>How much of the flats the moss cushions cover, roughly (0 = none).</summary>
        public float MossCoverage { get; set; } = 0.4f;

        /// <summary>How much of the floor the bilberry patches cover, roughly (0.5 = half).</summary>
        public float UndergrowthCoverage { get; set; } = 0.45f;

        /// <summary>How much of the open clearing the dry grass covers, roughly (0.5 = half).</summary>
        public float DryGrassCoverage { get; set; } = 0.35f;

        /// <summary>How high a moss cushion rises in the floor's relief, in world units.</summary>
        public float MossHeight { get; set; } = 0.12f;

        /// <summary>
        /// The dark conifer-canopy green the hills dress in past the clearing (linear). The scattered tree
        /// meshes stop well before the horizon, so the hills carry the forest as a colour: the floor blends
        /// towards this canopy field as the hill ramp rises, mottled at grove and crown scale so it reads as
        /// treetops seen from below rather than as dark paint.
        /// </summary>
        public Rgb TreelineColor { get; set; } = new(0.012f, 0.034f, 0.010f);

        /// <summary>How completely the hills dress in the canopy (0 = bare grass hills, 1 = solid treeline).</summary>
        public float TreelineStrength { get; set; } = 0.95f;

        /// <summary>
        /// How much sky fills the flats (ambient strength). Lower than the meadow: a clearing is shaded by the
        /// trees around it, not open sky.
        /// </summary>
        public float AmbientStrength { get; set; } = 0.5f;

        /// <summary>
        /// Distance over which the clearing melts into the skyline. Must complete before the terrain grid
        /// runs out (its extent over two), or the mesh edge shows against the dome; the wooded hills keep
        /// their colour through the ramp because the haze passes through a forest murk first (see Forest.fx).
        /// </summary>
        public float HorizonHazeDistance { get; set; } = 520f;

        /// <summary>Wind direction combing the undergrowth and drifting the fine relief.</summary>
        public Vec2 Wind { get; set; } = new(0.82f, 0.57f);

        /// <summary>How fast the bright/dark wind bands travel.</summary>
        public float WindRippleSpeed { get; set; } = 1.2f;

        /// <summary>How far apart the wind bands are.</summary>
        public float WindRippleFrequency { get; set; } = 0.14f;

        /// <summary>How deep the wind bands cut.</summary>
        public float WindRippleStrength { get; set; } = 0.1f;

        /// <summary>Fine needle/moss texture amplitude (a normal-tilting height field).</summary>
        public float NeedleReliefStrength { get; set; } = 0.10f;

        /// <summary>Fine needle/moss texture frequency.</summary>
        public float NeedleReliefFrequency { get; set; } = 1.6f;

        /// <summary>The scattered trees (conifers and broadleaves). An empty scatter renders the clearing.</summary>
        public ForestTreeConfig Trees { get; set; } = new();

        /// <summary>The scattered rocks and boulders.</summary>
        public ForestRockConfig Rocks { get; set; } = new();

        /// <summary>The scattered stumps and fallen logs.</summary>
        public ForestStumpConfig Stumps { get; set; } = new();

        /// <summary>
        /// Standing dead spruces (#462, <see cref="SnagMesh"/>) — none in the daytime forest, a few in the
        /// aurora's boreal wood. <see cref="ForestDeadwoodConfig.Length"/> is a snag's height.
        /// </summary>
        public ForestDeadwoodConfig Snags { get; set; } = new() { Length = 11f, Radius = 0.3f };

        /// <summary>
        /// Fallen trunks lying on the floor (#462, the savanna's <see cref="DeadwoodMesh"/>) — none in the
        /// daytime forest, a few in the aurora's. <see cref="ForestDeadwoodConfig.Length"/> is end to end.
        /// </summary>
        public ForestDeadwoodConfig Logs { get; set; } = new() { Length = 8f, Radius = 0.38f, MaxScale = 1.3f };

        /// <summary>The firefly-like lights over the floor (#487).</summary>
        public ForestFireflyConfig Fireflies { get; set; } = new();
    }

    /// <summary>
    /// The scattered trees of the forest: two species, conifers (a spruce - a tall irregular cone on a short
    /// trunk) and broadleaves (a lumpy bulged canopy on a taller trunk), split by <see cref="ConiferFraction"/>.
    /// The figures here are each species' <b>nominal</b> proportions: the caller builds a few mesh variants
    /// around them (see <see cref="ForestScatterRenderer"/>) so a grove is not one tree stamped out fifty times,
    /// and every variant is its own instanced draw per material — a trunk and a crown, sharing that variant's
    /// scatter of world matrices, since the crown mesh is built sitting on its trunk's top. Clustered, so the
    /// forest reads as groves.
    /// </summary>
    public sealed class ForestTreeConfig
    {
        /// <summary>Number of trees scattered across the clearing and its hills (both species together).
        /// Dense enough that the stands read as woodland rather than parkland — the scatter is instanced and
        /// measured near-free, so the count is a look decision, not a budget one.</summary>
        public int Count { get; set; } = 240;

        /// <summary>Fraction of the trees that are conifers; the rest are broadleaves.</summary>
        public float ConiferFraction { get; set; } = 0.65f;

        /// <summary>Inner radius of the scatter ring (kept clear of the island).</summary>
        public float MinRadius { get; set; } = 44f;

        /// <summary>Outer radius of the scatter ring.</summary>
        public float MaxRadius { get; set; } = 340f;

        /// <summary>Number of cluster centres trees gather around (per species).</summary>
        public int Clusters { get; set; } = 20;

        /// <summary>Spread of trees around each cluster centre.</summary>
        public float ClusterSpread { get; set; } = 34f;

        /// <summary>Smallest tree scale (a fraction of the mesh's authored size).</summary>
        public float MinScale { get; set; } = 0.7f;

        /// <summary>Largest tree scale.</summary>
        public float MaxScale { get; set; } = 1.5f;

        /// <summary>Trunk radius up the flank (world units, before per-instance scale). The mesh flares wider
        /// than this where the roots meet the ground.</summary>
        public float TrunkBaseRadius { get; set; } = 0.45f;

        /// <summary>Trunk radius at the top (a trunk tapers).</summary>
        public float TrunkTopRadius { get; set; } = 0.30f;

        /// <summary>Broadleaf trunk height to the underside of the canopy. The crown carries most of the
        /// tree's height — a taller bare trunk under a ball of leaves reads as a lollipop.</summary>
        public float TrunkHeight { get; set; } = 3.6f;

        /// <summary>Broadleaf canopy radius.</summary>
        public float CrownRadius { get; set; } = 3.1f;

        /// <summary>Broadleaf canopy height, trunk top to crown top (the crown spans this, give or take its
        /// own noise swell).</summary>
        public float CrownHeight { get; set; } = 5.6f;

        /// <summary>Conifer trunk height to the skirt of the cone (a forest spruce is clothed low).</summary>
        public float ConiferTrunkHeight { get; set; } = 1.7f;

        /// <summary>Conifer crown (cone) radius at the skirt.</summary>
        public float ConiferCrownRadius { get; set; } = 2.6f;

        /// <summary>Conifer crown height, skirt to tip.</summary>
        public float ConiferCrownHeight { get; set; } = 9.5f;

        /// <summary>
        /// The fewest branch whorls a spruce crown is built with; each mesh variant rolls between this and
        /// <c>ConiferTiers + ConiferTierSpread - 1</c> (<see cref="TreeMesh"/>). Four to six is the daytime
        /// forest's broad garden spruce. A boreal spruce (the aurora's, #462) is a narrow spire of many short
        /// whorls, and it is the count of them far more than the proportions that says so — the same crown
        /// at four tiers reads as a stack of cones.
        /// </summary>
        public int ConiferTiers { get; set; } = 4;

        /// <summary>How many different whorl counts the variants roll between, from <see cref="ConiferTiers"/> up (at least 1).</summary>
        public int ConiferTierSpread { get; set; } = 3;

        /// <summary>
        /// How uneven a spruce's whorls are — the spread of their widths from tier to tier and the wobble of
        /// each skirt's edge round the stem, together (<see cref="TreeMesh"/>). 1 is the forest as it was
        /// built. A boreal spruce (#462) is ragged, its branches broken and uneven, where a garden one is
        /// trim; a regular stack of many whorls reads as a pagoda.
        /// </summary>
        public float ConiferRaggedness { get; set; } = 1f;

        /// <summary>
        /// Bark colour (linear radiance), applied as the trunk renderers' diffuse tint. Only mildly warm: the
        /// bark texture carries a warm tint of its own, and a saturated brown under it reads as a terracotta
        /// pipe rather than as a trunk.
        /// </summary>
        public Rgb TrunkColor { get; set; } = new(0.058f, 0.045f, 0.033f);

        /// <summary>Broadleaf foliage colour (linear radiance) — a warmer, lighter green than the conifers'.</summary>
        public Rgb FoliageColor { get; set; } = new(0.042f, 0.115f, 0.026f);

        /// <summary>Conifer foliage colour (linear radiance) — the dark blue-green a spruce reads as.</summary>
        public Rgb ConiferColor { get; set; } = new(0.028f, 0.075f, 0.026f);
    }

    /// <summary>The scattered rocks and boulders of the forest floor.</summary>
    public sealed class ForestRockConfig
    {
        /// <summary>Number of boulders scattered across the floor.</summary>
        public int Count { get; set; } = 60;

        /// <summary>Inner radius of the scatter ring (kept clear of the island).</summary>
        public float MinRadius { get; set; } = 42f;

        /// <summary>Outer radius of the scatter ring.</summary>
        public float MaxRadius { get; set; } = 340f;

        /// <summary>Number of cluster centres rocks gather around.</summary>
        public int Clusters { get; set; } = 14;

        /// <summary>Spread of rocks around each cluster centre.</summary>
        public float ClusterSpread { get; set; } = 18f;

        /// <summary>Smallest rock scale.</summary>
        public float MinScale { get; set; } = 0.6f;

        /// <summary>Largest rock scale.</summary>
        public float MaxScale { get; set; } = 1.6f;

        /// <summary>Rock radius at the base (world units, before per-instance scale).</summary>
        public float Radius { get; set; } = 1.1f;

        /// <summary>Rock height above the base.</summary>
        public float Height { get; set; } = 0.7f;

        /// <summary>
        /// Stone colour (linear radiance), applied as the rock renderers' diffuse tint. Cool and dark: the
        /// stone detail texture is warm (it is the island's dressed stone), and over a light warm tint the
        /// boulders came out as pale buns lying in the grass rather than as granite.
        /// </summary>
        public Rgb Color { get; set; } = new(0.075f, 0.077f, 0.08f);
    }

    /// <summary>
    /// Dead wood scattered through the forest (#462): the standing snags and the fallen logs share this shape,
    /// each as its own group on <see cref="ForestSceneConfig"/>. <b>None by default</b> — the daytime forest
    /// never had any, and a count of zero plants nothing — so it is a scene that asks for them.
    /// </summary>
    public sealed class ForestDeadwoodConfig
    {
        /// <summary>Number scattered across the floor. Zero plants none.</summary>
        public int Count { get; set; }

        /// <summary>Inner radius of the scatter ring (kept clear of the island).</summary>
        public float MinRadius { get; set; } = 46f;

        /// <summary>Outer radius of the scatter ring.</summary>
        public float MaxRadius { get; set; } = 340f;

        /// <summary>Number of cluster centres they gather around.</summary>
        public int Clusters { get; set; } = 10;

        /// <summary>Spread around each cluster centre.</summary>
        public float ClusterSpread { get; set; } = 30f;

        /// <summary>Smallest scale.</summary>
        public float MinScale { get; set; } = 0.7f;

        /// <summary>Largest scale.</summary>
        public float MaxScale { get; set; } = 1.4f;

        /// <summary>A snag's height, or a log's length end to end (world units, before per-instance scale).</summary>
        public float Length { get; set; } = 10f;

        /// <summary>Radius at the thick end.</summary>
        public float Radius { get; set; } = 0.35f;

        /// <summary>
        /// Dead wood colour (linear radiance), applied as the renderers' diffuse tint over the bark texture.
        /// Grey and paler than living bark: wood that has stood or lain dead for years loses its bark and
        /// weathers silver, which is what makes a snag read against the dark spruces round it.
        /// </summary>
        public Rgb Color { get; set; } = new(0.11f, 0.105f, 0.10f);
    }

    /// <summary>The scattered stumps and fallen logs of the forest floor.</summary>
    public sealed class ForestStumpConfig
    {
        /// <summary>Number of stumps scattered across the floor.</summary>
        public int Count { get; set; } = 35;

        /// <summary>Inner radius of the scatter ring (kept clear of the island).</summary>
        public float MinRadius { get; set; } = 42f;

        /// <summary>Outer radius of the scatter ring.</summary>
        public float MaxRadius { get; set; } = 340f;

        /// <summary>Number of cluster centres stumps gather around.</summary>
        public int Clusters { get; set; } = 12;

        /// <summary>Spread of stumps around each cluster centre.</summary>
        public float ClusterSpread { get; set; } = 14f;

        /// <summary>Smallest stump scale.</summary>
        public float MinScale { get; set; } = 0.7f;

        /// <summary>Largest stump scale.</summary>
        public float MaxScale { get; set; } = 1.5f;

        /// <summary>Stump radius at the base (world units, before per-instance scale).</summary>
        public float Radius { get; set; } = 0.6f;

        /// <summary>Stump height (the cut).</summary>
        public float Height { get; set; } = 1.3f;

        /// <summary>Stump wood colour (linear radiance), applied as the stump renderers' diffuse tint. Lighter
        /// than the bark it shares a texture with — a stump is mostly the pale sawn face.</summary>
        public Rgb Color { get; set; } = new(0.095f, 0.072f, 0.048f);
    }

    /// <summary>
    /// A handful of firefly-like lights hovering over the floor among the trees (#487): each one blinks ON
    /// and then OFF on its own wall-clock period, in the spirit of the city roof beacon's hard flash
    /// (<see cref="CityRooftops"/>, #436) rather than the campfire's continuous flicker or the city window's
    /// soft cross-fade — a naturalistic scene's own answer to the same idiom.
    /// </summary>
    public sealed class ForestFireflyConfig
    {
        /// <summary>How many fireflies hover over the clearing.</summary>
        public int Count { get; set; } = 5;

        /// <summary>Nearest a firefly may sit to the island (world units from the origin).</summary>
        public float MinRadius { get; set; } = 40f;

        /// <summary>Farthest a firefly may sit from the island — kept well inside the tree scatter's own
        /// <see cref="ForestTreeConfig.MaxRadius"/>, so every one stays in range of a camera in the clearing.</summary>
        public float MaxRadius { get; set; } = 75f;

        /// <summary>How far a firefly hovers above the ground it was planted on.</summary>
        public float HoverHeight { get; set; } = 0.5f;

        /// <summary>
        /// Radius of the small glowing sphere each firefly is (world units). Measured rather than guessed at
        /// a real firefly's own scale (a centimetre or so): at 40-75 units out that came back under a pixel
        /// at 1600x900 and the bloom had nothing to spread from, which read as nothing lit at all rather than
        /// as a dim one. This is a stylised size — bigger than life, the same liberty the roof beacon's own
        /// sphere already takes on a mast — chosen to hold a few pixels pre-bloom at the far end of
        /// <see cref="MaxRadius"/> so the bloom pass has a seed to work from.
        /// </summary>
        public float BodyRadius { get; set; } = 0.3f;

        /// <summary>Shortest blink period, in seconds (a randomly rolled period between this and
        /// <see cref="MaxPeriod"/> per firefly, so the handful of them never beat in unison).</summary>
        public float MinPeriod { get; set; } = 2.2f;

        /// <summary>Longest blink period, in seconds.</summary>
        public float MaxPeriod { get; set; } = 4.0f;

        /// <summary>Share of its own period a firefly spends lit, the beacon's own figure (#436).</summary>
        public float OnFraction { get; set; } = 0.22f;

        /// <summary>
        /// Peak emissive brightness (linear radiance) at the centre of the ON pulse — the beacon's own figure
        /// (<see cref="RooftopConfig.BeaconBrightness"/>), high enough over <c>GLARE_THRESHOLD</c> that so
        /// small a body still blooms and reads from a distance.
        /// </summary>
        public float Brightness { get; set; } = 3.2f;

        /// <summary>The glow's colour (linear radiance) — a firefly's own warm yellow-green, not the beacon's
        /// warning red.</summary>
        public Rgb Color { get; set; } = new(0.55f, 0.95f, 0.25f);
    }
}
