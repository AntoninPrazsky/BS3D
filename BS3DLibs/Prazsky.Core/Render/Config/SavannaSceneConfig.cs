using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the savanna backdrop: open golden grassland rolling into low rises, combed by
    /// wind, dotted with acacia trees, warmed by a campfire, under the shared flock of birds.
    /// </summary>
    public sealed class SavannaSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Savanna;

        /// <summary>Mean grass level, sitting at the island's foot (world origin clearing).</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>Height of the low rises the grassland rolls into with distance (flatter than the meadow).</summary>
        public float HillHeight { get; set; } = 34f;

        /// <summary>Radius of the flat clearing the island stands in around the world origin.</summary>
        public float ClearingRadius { get; set; } = 90f;

        /// <summary>Distance over which the terrain rises from the flat clearing into low rises.</summary>
        public float ClearingTransition { get; set; } = 130f;

        /// <summary>A soft undulation present even inside the clearing.</summary>
        public float ClearingRelief { get; set; } = 1.6f;

        /// <summary>Lush green grass (linear, dominant).</summary>
        public Rgb GrassSavanna { get; set; } = new(0.13f, 0.33f, 0.06f);

        /// <summary>Dry golden grass (linear, patches).</summary>
        public Rgb GrassDry { get; set; } = new(0.40f, 0.31f, 0.10f);

        /// <summary>Bare reddish earth (linear).</summary>
        public Rgb GrassBare { get; set; } = new(0.26f, 0.15f, 0.08f);

        /// <summary>How much sky fills the flats (ambient hemisphere strength).</summary>
        public float AmbientStrength { get; set; } = 0.7f;

        /// <summary>Wind direction combing the grass.</summary>
        public Vec2 Wind { get; set; } = new(0.86f, 0.51f);

        /// <summary>Distance over which the grass melts into the skyline.</summary>
        public float HorizonHazeDistance { get; set; } = 520f;

        /// <summary>Wind combing the grass: travelling band speed.</summary>
        public float WindRippleSpeed { get; set; } = 1.2f;

        /// <summary>Wind combing the grass: band frequency.</summary>
        public float WindRippleFrequency { get; set; } = 0.14f;

        /// <summary>Wind combing the grass: band strength.</summary>
        public float WindRippleStrength { get; set; } = 0.1f;

        /// <summary>Fine grass texture (normal-tilting height field) strength.</summary>
        public float GrassReliefStrength { get; set; } = 0.05f;

        /// <summary>Fine grass texture frequency.</summary>
        public float GrassReliefFrequency { get; set; } = 2f;

        /// <summary>Scattered acacia trees and low bushes.</summary>
        public AcaciaConfig Acacia { get; set; } = new();

        /// <summary>The campfire point light and its visible flame billboard.</summary>
        public CampfireConfig Campfire { get; set; } = new();

        /// <summary>The shared flock of birds circling overhead.</summary>
        public BirdsConfig Birds { get; set; } = new();
    }

    /// <summary>Scattered acacia trees and low bushes over the savanna (upright billboards planted on the ground).</summary>
    public sealed class AcaciaConfig
    {
        /// <summary>Number of scattered acacia trees and low bushes.</summary>
        public int Count { get; set; } = 120;

        /// <summary>Fraction of the scatter that are low bushes rather than trees.</summary>
        public float BushFraction { get; set; } = 0.45f;

        /// <summary>Base half-width of a tree crown.</summary>
        public float Width { get; set; } = 6f;

        /// <summary>Base height of a tree billboard.</summary>
        public float Height { get; set; } = 9f;

        /// <summary>Inner radius of the scatter ring (clear of the island).</summary>
        public float MinRadius { get; set; } = 42f;

        /// <summary>Outer radius of the scatter ring.</summary>
        public float MaxRadius { get; set; } = 340f;

        /// <summary>Number of cluster centres plants gather around.</summary>
        public int Clusters { get; set; } = 24;

        /// <summary>Spread of plants around each cluster centre.</summary>
        public float ClusterSpread { get; set; } = 30f;

        /// <summary>Acacia canopy green (linear).</summary>
        public Rgb CanopyColor { get; set; } = new(0.09f, 0.17f, 0.05f);

        /// <summary>Drier yellow-green canopy (linear).</summary>
        public Rgb CanopyDry { get; set; } = new(0.22f, 0.22f, 0.07f);

        /// <summary>Dark brown trunk (linear).</summary>
        public Rgb TrunkColor { get; set; } = new(0.09f, 0.06f, 0.035f);
    }

    /// <summary>
    /// The savanna's campfire: a real point light warming the grass and the island, plus the visible
    /// additive flame billboard. Ground position is XZ; the Y is derived live — SavannaTerrainHeight(x, z)
    /// + <see cref="HeightAboveTerrain"/> on every read of SavannaCampfirePosition — so a GroundXZ or
    /// terrain edit in the editor moves the fire without a re-apply.
    /// </summary>
    public sealed class CampfireConfig
    {
        /// <summary>Campfire ground position (XZ) just off the island, chosen to sit in the low game camera's view.</summary>
        public Vec2 GroundXZ { get; set; } = new(28f, -18f);

        /// <summary>Height above the terrain at that spot (the light/flame sits just above the ground).</summary>
        public float HeightAboveTerrain { get; set; } = 0.2f;

        /// <summary>Point-light range (quadratic distance falloff).</summary>
        public float Range { get; set; } = 32f;

        /// <summary>Size of the visible additive flame billboard.</summary>
        public float FlameSize { get; set; } = 2.3f;

        /// <summary>Warm fire colour in LINEAR radiance, kept bright (over 1) so it casts real warm light, not a tint.</summary>
        public Rgb BaseColor { get; set; } = new(2.4f, 1.0f, 0.32f);
    }
}
