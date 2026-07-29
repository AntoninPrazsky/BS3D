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

        /// <summary>Peak height of the wooded hills that rise with distance (the treeline).</summary>
        public float HillHeight { get; set; } = 44f;

        /// <summary>Flat clearing radius around the arena centre before the hills begin.</summary>
        public float ClearingRadius { get; set; } = 95f;

        /// <summary>Distance over which the flat clearing ramps up into hills.</summary>
        public float ClearingTransition { get; set; } = 135f;

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
        public Rgb ForestColor { get; set; } = new(0.09f, 0.22f, 0.06f);

        /// <summary>Darker needle-litter and shadow green the floor varies towards in patches (linear).</summary>
        public Rgb ForestColorDark { get; set; } = new(0.045f, 0.12f, 0.03f);

        /// <summary>
        /// How much sky fills the flats (ambient strength). Lower than the meadow: a clearing is shaded by the
        /// trees around it, not open sky.
        /// </summary>
        public float AmbientStrength { get; set; } = 0.5f;

        /// <summary>Distance over which the clearing melts into the skyline.</summary>
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

        /// <summary>The scattered trees (phase 2 — multiple species). An empty scatter renders the clearing.</summary>
        public ForestTreeConfig Trees { get; set; } = new();

        /// <summary>The scattered rocks and boulders (phase 2).</summary>
        public ForestRockConfig Rocks { get; set; } = new();

        /// <summary>The scattered stumps and fallen logs (phase 2).</summary>
        public ForestStumpConfig Stumps { get; set; } = new();
    }

    /// <summary>
    /// The scattered trees of the forest. One instanced draw per kind (trunk and crown each), sharing one
    /// scatter of world matrices — the crown mesh is built sitting on its trunk's top, so a single position
    /// places both. Clustered, so the forest reads as groves.
    /// </summary>
    public sealed class ForestTreeConfig
    {
        /// <summary>Number of trees scattered across the clearing and its hills.</summary>
        public int Count { get; set; } = 140;

        /// <summary>Inner radius of the scatter ring (kept clear of the island).</summary>
        public float MinRadius { get; set; } = 44f;

        /// <summary>Outer radius of the scatter ring.</summary>
        public float MaxRadius { get; set; } = 360f;

        /// <summary>Number of cluster centres trees gather around.</summary>
        public int Clusters { get; set; } = 22;

        /// <summary>Spread of trees around each cluster centre.</summary>
        public float ClusterSpread { get; set; } = 34f;

        /// <summary>Smallest tree scale (a fraction of the mesh's authored size).</summary>
        public float MinScale { get; set; } = 0.7f;

        /// <summary>Largest tree scale.</summary>
        public float MaxScale { get; set; } = 1.4f;

        /// <summary>Trunk radius at the ground (world units, before per-instance scale).</summary>
        public float TrunkBaseRadius { get; set; } = 0.45f;

        /// <summary>Trunk radius at the top (a trunk tapers).</summary>
        public float TrunkTopRadius { get; set; } = 0.30f;

        /// <summary>Trunk height to the underside of the canopy.</summary>
        public float TrunkHeight { get; set; } = 4.5f;

        /// <summary>Canopy radius.</summary>
        public float CrownRadius { get; set; } = 2.4f;

        /// <summary>Canopy height above the trunk top. Taller than the radius reads conical; shorter, broadleaf.</summary>
        public float CrownHeight { get; set; } = 5.5f;

        /// <summary>Bark colour (linear), applied as the trunk renderer's diffuse tint.</summary>
        public Rgb TrunkColor { get; set; } = new(0.16f, 0.10f, 0.06f);

        /// <summary>Foliage colour (linear), applied as the crown renderer's diffuse tint.</summary>
        public Rgb FoliageColor { get; set; } = new(0.07f, 0.16f, 0.05f);
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

        /// <summary>Stone colour (linear), applied as the rock renderer's diffuse tint.</summary>
        public Rgb Color { get; set; } = new(0.30f, 0.29f, 0.27f);
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

        /// <summary>Stump wood colour (linear), applied as the stump renderer's diffuse tint.</summary>
        public Rgb Color { get; set; } = new(0.21f, 0.14f, 0.08f);
    }
}
