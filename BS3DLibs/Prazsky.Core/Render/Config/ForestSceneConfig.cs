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

        /// <summary>Cool low undergrowth green (linear, dominant).</summary>
        public Rgb ForestColor { get; set; } = new(0.075f, 0.19f, 0.05f);

        /// <summary>Darker needle-litter and shadow green the floor varies towards in patches (linear).</summary>
        public Rgb ForestColorDark { get; set; } = new(0.04f, 0.10f, 0.026f);

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
        public float NeedleReliefStrength { get; set; } = 0.07f;

        /// <summary>Fine needle/moss texture frequency.</summary>
        public float NeedleReliefFrequency { get; set; } = 1.6f;

        /// <summary>The scattered trees (conifers and broadleaves). An empty scatter renders the clearing.</summary>
        public ForestTreeConfig Trees { get; set; } = new();

        /// <summary>The scattered rocks and boulders.</summary>
        public ForestRockConfig Rocks { get; set; } = new();

        /// <summary>The scattered stumps and fallen logs.</summary>
        public ForestStumpConfig Stumps { get; set; } = new();
    }

    /// <summary>
    /// The scattered trees of the forest: two species, conifers (a spruce - a tall irregular cone on a short
    /// trunk) and broadleaves (a lumpy bulged canopy on a taller trunk), split by <see cref="ConiferFraction"/>.
    /// The figures here are each species' <b>nominal</b> proportions: the caller builds a few mesh variants
    /// around them (see the Game's forest scatter region) so a grove is not one tree stamped out fifty times,
    /// and every variant is its own instanced draw per material — a trunk and a crown, sharing that variant's
    /// scatter of world matrices, since the crown mesh is built sitting on its trunk's top. Clustered, so the
    /// forest reads as groves.
    /// </summary>
    public sealed class ForestTreeConfig
    {
        /// <summary>Number of trees scattered across the clearing and its hills (both species together).</summary>
        public int Count { get; set; } = 170;

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
}
