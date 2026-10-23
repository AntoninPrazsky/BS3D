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
        /// Under a canopy the sky is a lid and not a view. It was a broken deck until #961, and a broken deck is
        /// the brightest thing in a dark wood whatever dome it hangs under (its lit side takes the sun's light
        /// and no dome is darker than that: the upper sky's mean was 89 under dome 15 and 90 under dome 13), which
        /// is what the owner meant by "the sky is too bright". The overcast lid is 55, and the ground under it is
        /// lit evenly instead of being swept by cloud shadows.
        /// </summary>
        public ForestSceneConfig()
        {
            Weather = WeatherPreset.Overcast;

            //The sun's cast shadows (#471), at the savanna's own figures — 0.9 is a shadow that is dark without
            //reading as a hole, and 260 units at 2048 is 0.13 units a texel. The densest scatter in the
            //project (335 trees, boulders and stumps at the base, more on High and Ultra, #962), which until #471
            //stood on an evenly lit floor.
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

        /// <summary>
        /// The scattered trees (conifers and broadleaves). An empty scatter renders the clearing. High and Ultra stand a
        /// wood twice as thick round the clearing and a young growth among it (#962, <see cref="ForestTreeConfig.FineCount"/>
        /// and <see cref="ForestTreeConfig.SaplingCount"/>); the references' wall round a clearing is crowded trees of every
        /// age, and two hundred and forty old ones over the whole ring read as a park.
        /// </summary>
        public ForestTreeConfig Trees { get; set; } = new() { FineCount = 400, FineMinRadius = 54f, FineMaxRadius = 290f, SaplingCount = 160 };

        /// <summary>The scattered rocks and boulders; High and Ultra lay more of them (#962).</summary>
        public ForestRockConfig Rocks { get; set; } = new() { FineCount = 40 };

        /// <summary>The scattered stumps; High and Ultra cut a few more (#962).</summary>
        public ForestStumpConfig Stumps { get; set; } = new() { FineCount = 15 };

        /// <summary>
        /// Standing dead spruces (#462, <see cref="SnagMesh"/>) — a few in the aurora's boreal wood, and in the daytime
        /// forest on High and Ultra only (#962: the references' wood keeps its dead, and a snag with a broken top is the
        /// imperfection that says the trees grew rather than were placed). <see cref="ForestDeadwoodConfig.Length"/> is a
        /// snag's height.
        /// </summary>
        public ForestDeadwoodConfig Snags { get; set; } = new()
        {
            Length = 11f, Radius = 0.3f, FineCount = 16, Clusters = 8, MaxRadius = 260f,
            Color = new(0.075f, 0.068f, 0.062f)
        };

        /// <summary>
        /// Fallen trunks lying on the floor (#462, the savanna's <see cref="DeadwoodMesh"/>) — a few in the aurora's wood,
        /// and in the daytime forest on High and Ultra only (#962). <see cref="ForestDeadwoodConfig.Length"/> is end to end.
        /// </summary>
        public ForestDeadwoodConfig Logs { get; set; } = new()
        {
            Length = 8f, Radius = 0.38f, MaxScale = 1.3f, FineCount = 26, Clusters = 10, MaxRadius = 240f,
            Color = new(0.062f, 0.058f, 0.044f)
        };

        /// <summary>The firefly-like lights over the floor (#487).</summary>
        public ForestFireflyConfig Fireflies { get; set; } = new();

        /// <summary>
        /// High and Ultra's wood on the hills past the planted trees, out to the last ridges in the haze (#962,
        /// <c>ForestFarStand</c>). Low and Medium keep the hills' canopy colour (<see cref="TreelineColor"/>), which is
        /// under the stand at every tier.
        /// </summary>
        public ForestFarStandConfig FarStand { get; set; } = new();
    }

    /// <summary>
    /// The daytime forest's far stand (#962): the hills' wood past the planted trees as thousands of cheap spires and rounded
    /// broadleaf crowns, instanced, so every ridge's skyline is tree tips — every reference of a wooded hill (#962's set, both
    /// models) draws the ridges jagged with them, darker spruce stands and lighter beech stands in patches, each ridge paler
    /// and bluer than the one before. The aurora's far stand is the pattern (<see cref="AuroraFarStandConfig"/>). Planted on a
    /// jittered grid, so the canopy closes as a real wood's does instead of clumping and leaving holes, and never inside the
    /// planted wood's own trees. Drawn on High and Ultra only, lit and hazed as the floor under it.
    /// </summary>
    public sealed class ForestFarStandConfig
    {
        /// <summary>Where it starts, from the arena's axis: past the High and Ultra stand round the clearing
        /// (<see cref="ForestTreeConfig.FineMaxRadius"/>), where the hills have risen.</summary>
        public float InnerRadius { get; set; } = 270f;

        /// <summary>Where the finer meshes give way to the coarser ones.</summary>
        public float NearRadius { get; set; } = 420f;

        /// <summary>Where it ends: past it the haze has carried everything into the sky (<see cref="ForestSceneConfig.HorizonHazeDistance"/>
        /// from a camera on the island, and <c>FarFadeToSky</c> after it).</summary>
        public float OuterRadius { get; set; } = 560f;

        /// <summary>The grid a tree is jittered in, world units, inside <see cref="NearRadius"/> and past it.</summary>
        public float Spacing { get; set; } = 6.5f;
        public float FarSpacing { get; set; } = 8f;

        /// <summary>How much of the ground is left open (glades, rides, scree), 0 to 1.</summary>
        public float Glades { get; set; } = 0.22f;

        /// <summary>The share of the stand that is broadleaf, laid in patches a grove or two across.</summary>
        public float BroadleafShare { get; set; } = 0.4f;

        /// <summary>A spire's height and skirt radius at scale 1: the planted spruces' proportions.</summary>
        public float SpruceHeight { get; set; } = 19f;
        public float SpruceRadius { get; set; } = 3.6f;

        /// <summary>A broadleaf crown's height (ground to top) and radius at scale 1.</summary>
        public float CrownHeight { get; set; } = 17f;
        public float CrownRadius { get; set; } = 5f;

        /// <summary>The smallest and the largest scale.</summary>
        public float MinScale { get; set; } = 0.75f;
        public float MaxScale { get; set; } = 1.35f;

        /// <summary>The spruces' needles (linear): the planted spruces' green, a shade darker for a stand's crowded crowns.</summary>
        public Rgb SpruceColor { get; set; } = new(0.011f, 0.030f, 0.014f);

        /// <summary>The broadleaves' leaves (linear): lighter and warmer than the spruces, as the references' beech stands are.</summary>
        public Rgb BroadleafColor { get; set; } = new(0.020f, 0.044f, 0.014f);

        /// <summary>How much lighter the outer edge of a tier's or a crown's upper face is, its tips catching the sky.</summary>
        public float TipLight { get; set; } = 0.5f;

        /// <summary>The planting's seed.</summary>
        public int Seed { get; set; } = 96201;
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
        /// <summary>Number of trees scattered across the clearing and its hills (both species together), at every
        /// tier. High and Ultra add <see cref="FineCount"/> and <see cref="SaplingCount"/> to them.</summary>
        public int Count { get; set; } = 240;

        /// <summary>
        /// How many more full-grown trees High and Ultra stand (#962, <c>SceneRenderer.FineMeshes</c>), split between the
        /// species as <see cref="Count"/> is and planted after everything else (<see cref="ForestScatter.FineConifers"/>), so
        /// the wood the lower tiers draw does not move. Between <see cref="FineMinRadius"/> and <see cref="FineMaxRadius"/>:
        /// round the clearing, where the camera sees them, and not on the far hills, which the far stand dresses. 0 plants
        /// none (the class default, and the aurora's).
        /// </summary>
        public int FineCount { get; set; }

        /// <summary>
        /// The share of <see cref="FineCount"/> and <see cref="SaplingCount"/> that is spruce. Higher than <see cref="ConiferFraction"/>:
        /// the references' wood round a clearing is mostly spruce, and a broadleaf in leaf is the dearest tree on the screen (its
        /// alpha-tested cards, #962, measured).
        /// </summary>
        public float FineConiferFraction { get; set; } = 0.8f;

        /// <summary>The ring <see cref="FineCount"/> and <see cref="SaplingCount"/> are planted in.</summary>
        public float FineMinRadius { get; set; } = 52f;
        public float FineMaxRadius { get; set; } = 340f;

        /// <summary>The cluster centres the High and Ultra trees gather round: three times the base wood's, so they close it into a
        /// wall round the clearing rather than piling into its groves.</summary>
        public int FineClusters { get; set; } = 60;

        /// <summary>
        /// How many young trees High and Ultra stand among the old ones (#962): the same meshes at
        /// <see cref="SaplingMinScale"/> to <see cref="SaplingMaxScale"/> of their size, the understorey of every age the
        /// references draw. 0 plants none.
        /// </summary>
        public int SaplingCount { get; set; }

        /// <summary>The smallest and the largest a sapling is, as a share of a grown tree's mesh.</summary>
        public float SaplingMinScale { get; set; } = 0.16f;
        public float SaplingMaxScale { get; set; } = 0.42f;

        /// <summary>Fraction of the trees that are conifers; the rest are broadleaves.</summary>
        public float ConiferFraction { get; set; } = 0.65f;

        /// <summary>Inner radius of the scatter ring (kept clear of the island). 44 until #647, whose crowns reach half
        /// as far again: a broadleaf at the ring's edge hung its crown over the play camera's shoulder.</summary>
        public float MinRadius { get; set; } = 52f;

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

        //THE TREES ARE 1.6 TIMES WHAT THEY WERE, since #647 — every figure below that sizes one. The owner: "the
        //trees are too low and don't read as real." Against the island (52 across) a spruce of 11 units and an
        //oak of 9 were shrubs; every reference of a real wood stands its trees two to four times the height of the
        //gap between them, a wall round a clearing.

        /// <summary>Trunk radius up the flank (world units, before per-instance scale). The mesh flares wider
        /// than this where the roots meet the ground. 0.45 until #647.</summary>
        public float TrunkBaseRadius { get; set; } = 0.62f;

        /// <summary>Trunk radius at the top (a trunk tapers). 0.30 until #647.</summary>
        public float TrunkTopRadius { get; set; } = 0.40f;

        /// <summary>Broadleaf trunk height to the underside of the canopy. The crown carries most of the
        /// tree's height — a taller bare trunk under a ball of leaves reads as a lollipop. 3.6 until #647.</summary>
        public float TrunkHeight { get; set; } = 5.8f;

        /// <summary>Broadleaf canopy radius. 3.1 until #647.</summary>
        public float CrownRadius { get; set; } = 4.6f;

        /// <summary>Broadleaf canopy height, trunk top to crown top (the crown spans this, give or take its
        /// own noise swell). 5.6 until #647.</summary>
        public float CrownHeight { get; set; } = 9f;

        /// <summary>Conifer trunk height to the skirt of the cone (a forest spruce is clothed low). 1.7 until #647.</summary>
        public float ConiferTrunkHeight { get; set; } = 2.6f;

        /// <summary>Conifer crown (cone) radius at the skirt. 2.6 until #647.</summary>
        public float ConiferCrownRadius { get; set; } = 3.8f;

        /// <summary>Conifer crown height, skirt to tip. 9.5 until #647.</summary>
        public float ConiferCrownHeight { get; set; } = 15f;

        /// <summary>
        /// How much longer the bare stem is under two of the six spruce variants — the narrow tall one and the
        /// spire — than under the rest (#647). Every reference of a real wood shows both: the trees at a clearing's
        /// edge clothed to the ground, and behind them the stand's crowded stems, bare for a third of their height
        /// where they shed their lower branches for want of light. 1 is every spruce clothed low, as before.
        /// </summary>
        public float ConiferStandTrunk { get; set; } = 2.5f;

        /// <summary>
        /// How much of a crown's up-facing faces lie under snow (#647), 0 none — the daytime forest's. The aurora's
        /// wood is a winter one, and every reference of it drew the spruces' whorls white along their tops.
        /// </summary>
        public float CrownSnow { get; set; }

        /// <summary>
        /// The share of <see cref="CrownSnow"/>'s dust term High and Ultra's boughs take (#733), over the lumps of snow they
        /// carry as geometry: a frost at the forest's 0.25, a heavier coat in the aurora's moonlit winter wood (#963).
        /// </summary>
        public float BoughFrost { get; set; } = 0.25f;

        /// <summary>The snow on the crowns (linear radiance), when there is any — see <see cref="CrownSnow"/>.</summary>
        public Rgb CrownSnowColor { get; set; } = new(0.58f, 0.64f, 0.76f);

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

        /// <summary>How many more High and Ultra lay (#962), after everything else is planted. 0 lays none.</summary>
        public int FineCount { get; set; }

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

        /// <summary>Snow lying on its top (the triplanar dust term, in <see cref="ForestTreeConfig.CrownSnowColor"/>), 0 to 1: none in the daytime forest, a cap in the aurora's snow (#963).</summary>
        public float TopSnow { get; set; }
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

        /// <summary>How many more High and Ultra plant (#962), after everything else. 0 plants none.</summary>
        public int FineCount { get; set; }

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

        /// <summary>Snow lying on its top (the triplanar dust term, in <see cref="ForestTreeConfig.CrownSnowColor"/>), 0 to 1: none in the daytime forest, a cap in the aurora's snow (#963).</summary>
        public float TopSnow { get; set; }
    }

    /// <summary>The scattered stumps and fallen logs of the forest floor.</summary>
    public sealed class ForestStumpConfig
    {
        /// <summary>Number of stumps scattered across the floor.</summary>
        public int Count { get; set; } = 35;

        /// <summary>How many more High and Ultra cut (#962), after everything else is planted. 0 cuts none.</summary>
        public int FineCount { get; set; }

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

        /// <summary>Snow lying on its top (the triplanar dust term, in <see cref="ForestTreeConfig.CrownSnowColor"/>), 0 to 1: none in the daytime forest, a cap in the aurora's snow (#963).</summary>
        public float TopSnow { get; set; }
    }

    /// <summary>
    /// A scatter of firefly-like lights drifting over the floor among the trees (#487, reshaped in #961): each
    /// one swells up out of the dark and fades back into it on its own wall-clock period, and wanders a little
    /// between glows. Not the city roof beacon's hard flash (<see cref="CityRooftops"/>, #436) it began as: that
    /// read as one bare light switching on and off, on the far floor, with nothing to say what it was.
    /// </summary>
    public sealed class ForestFireflyConfig
    {
        /// <summary>How many fireflies hover over the clearing: enough that a few are always glowing and the eye
        /// takes them for a swarm, where five read as one odd light that blinks now and then.</summary>
        public int Count { get; set; } = 12;

        /// <summary>Nearest a firefly may sit to the island (world units from the origin): just outside the
        /// island and its berm, so the nearest ones are a few strides from the gun.</summary>
        public float MinRadius { get; set; } = 32f;

        /// <summary>Farthest a firefly may sit from the island — kept well inside the tree scatter's own
        /// <see cref="ForestTreeConfig.MaxRadius"/>, so every one stays in range of a camera in the clearing.</summary>
        public float MaxRadius { get; set; } = 72f;

        /// <summary>How far a firefly hovers above the ground it was planted on: well off it, so a glow reads as
        /// a thing in the air and not as a spark on the ground.</summary>
        public float HoverHeight { get; set; } = 1.2f;

        /// <summary>
        /// Radius of the small glowing sphere each firefly is (world units). Measured rather than guessed at
        /// a real firefly's own scale (a centimetre or so): at 40-75 units out that came back under a pixel
        /// at 1600x900 and the bloom had nothing to spread from, which read as nothing lit at all rather than
        /// as a dim one. This is a stylised size — bigger than life, the same liberty the roof beacon's own
        /// sphere already takes on a mast — chosen to hold a few pixels pre-bloom at the far end of
        /// <see cref="MaxRadius"/> so the bloom pass has a seed to work from.
        /// </summary>
        public float BodyRadius { get; set; } = 0.25f;

        /// <summary>Shortest glow period, in seconds (a randomly rolled period between this and
        /// <see cref="MaxPeriod"/> per firefly, so the handful of them never beat in unison).</summary>
        public float MinPeriod { get; set; } = 2.4f;

        /// <summary>Longest glow period, in seconds.</summary>
        public float MaxPeriod { get; set; } = 4.5f;

        /// <summary>Share of its own period a firefly spends glowing, 0.4: a glow of one to two seconds, long
        /// enough to be seen to swell and fade (the roof beacon's 0.22 was a flash of two thirds of a second).</summary>
        public float OnFraction { get; set; } = 0.4f;

        /// <summary>
        /// Peak emissive brightness (linear radiance) at the centre of the glow. Far over <c>GLARE_THRESHOLD</c>
        /// on purpose: the body is a few pixels and the soft halo round it is the bloom's, whose energy goes
        /// with how far over the threshold the body is.
        /// </summary>
        public float Brightness { get; set; } = 10f;

        /// <summary>How far, in world units, a firefly wanders sideways of where it hovers (a slow circuit of
        /// fifteen to forty seconds, so a glow moves a little and the next one is elsewhere).</summary>
        public float WanderRadius { get; set; } = 1.6f;

        /// <summary>How far it bobs up and down; never more than <see cref="HoverHeight"/>, so it stays off the
        /// floor.</summary>
        public float WanderBob { get; set; } = 0.4f;

        /// <summary>The glow's colour (linear radiance) — a firefly's own warm yellow-green, not the beacon's
        /// warning red.</summary>
        public Rgb Color { get; set; } = new(0.55f, 0.95f, 0.25f);
    }
}
