using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the savanna backdrop: open golden grassland rolling into low rises, combed by
    /// wind, dotted with acacia trees, ringed by campfires around the island, under the shared flock of birds.
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

        /// <summary>The ring of campfires: their point lights and their visible flame billboards.</summary>
        public CampfireConfig Campfire { get; set; } = new();

        /// <summary>The shared flock of birds circling overhead.</summary>
        public BirdsConfig Birds { get; set; } = new();
    }

    /// <summary>Scattered acacia trees and low bushes over the savanna (upright billboards planted on the ground).</summary>
    public sealed class AcaciaConfig
    {
        /// <summary>
        /// Number of scattered acacia trees and low bushes. Was 8 from the scene's first days until #168 —
        /// over the same-sized ring the forest's 240 trees scatter across, eight plants (three of them
        /// bushes) read as an empty plain with a speck on it, and there were three times as many cluster
        /// centres as plants. The pass is one static billboard buffer, so the count is a look decision
        /// rather than a budget one (the forest's own doc says the same of its 240); 64 keeps the savanna
        /// about a quarter of the forest's density — dotted and open, the way a savanna reads — while a
        /// grove actually gets its 4–5 trees.
        /// </summary>
        public int Count { get; set; } = 64;

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

        /// <summary>
        /// Number of cluster centres plants gather around. Halved with #168's count raise (24 → 12): a
        /// cluster centre the scatter mostly lands around is a grove only if plants actually reach it, and
        /// at the old ratio most centres got zero — now each of the twelve takes ~4–5 of the 64, so the
        /// clumps read as groves rather than as pairs of trees standing near a place a grove could have
        /// been.
        /// </summary>
        public int Clusters { get; set; } = 12;

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
    /// The savanna's campfires: a ring of them around the island, each a real point light warming the grass
    /// and the stone plus its own visible additive flame billboard. Positions are XZ; every Y is derived live
    /// — SavannaTerrainHeight(x, z) + <see cref="HeightAboveTerrain"/> on every read of
    /// SavannaCampfirePosition — so a GroundXZ or terrain edit in the editor moves the fires without a
    /// re-apply.
    /// <para>
    /// Everything but the position is shared by the whole ring: they are the same kind of fire, and a range
    /// or a colour per fire would be a config nobody could tune. What is <b>not</b> shared is the phase — see
    /// <c>SceneRenderer.CampfireColor</c> and <c>Flame.fx</c>'s <c>FlameSeed</c>, which give each fire its own
    /// clock and its own gait so the ring does not beat in unison.
    /// </para>
    /// </summary>
    public sealed class CampfireConfig
    {
        /// <summary>
        /// Ground position (XZ) of the <b>first</b> fire, just off the island. The rest of the ring is derived
        /// from it — see <see cref="Count"/> — so this one value still says everything it used to: how far out
        /// the fires stand, and which way the ring is turned. A config saved when there was only ever one fire
        /// therefore still places that fire exactly where it stood.
        /// </summary>
        public Vec2 GroundXZ { get; set; } = new(28f, -18f);

        /// <summary>
        /// How many fires ring the island, evenly spaced on the circle <see cref="GroundXZ"/> sits on and
        /// starting at it. Each is a real point light as well as a flame, so this is <b>capped at
        /// <c>SceneLights.MaxLights</c></b> (8, the shader's own array size) — eight fires spend the whole
        /// scene-light budget, which the savanna can afford because it is the only thing in it that lights.
        /// <para>
        /// One is the old single campfire and still valid. The ring exists because a lone fire lit one flank
        /// of the island and left the rest of the walk the gun makes in flat dome light.
        /// </para>
        /// </summary>
        public int Count { get; set; } = 8;

        /// <summary>Height above the terrain at that spot (the light/flame sits just above the ground).</summary>
        public float HeightAboveTerrain { get; set; } = 0.2f;

        /// <summary>Point-light range (quadratic distance falloff).</summary>
        public float Range { get; set; } = 32f;

        /// <summary>Width of the visible additive flame billboard (its half-width in world units).</summary>
        public float FlameSize { get; set; } = 2.3f;

        /// <summary>
        /// The billboard's height as a multiple of <see cref="FlameSize"/>. <b>Separate from the width on
        /// purpose</b>, and the reason is the game's own camera: it sits low, about level with the island's
        /// stone, while the fires stand on grass roughly five units below it. At the shader's old fixed 2.4
        /// the flames were 5.5 units tall and cleared the stone by barely half a unit — from the play camera
        /// all that showed of a fire was the tip of one. Growing <see cref="FlameSize"/> instead would have
        /// raised the tips by widening the fires into bonfires.
        /// <para>
        /// Six was chosen from the play camera against 4.5 and 8: at 4.5 a flame is present but slight, and at
        /// 8 it stops reading as a fire and becomes a beam standing in the sky. It does make a flame ~14 units
        /// tall — taller than the acacias, which are 9 — and that is accepted rather than overlooked: these
        /// fires are 33 units out and the eye judges them against the island beside them, not against a tree
        /// at the horizon.
        /// </para>
        /// </summary>
        public float FlameHeightScale { get; set; } = 6.0f;

        /// <summary>Warm fire colour in LINEAR radiance, kept bright (over 1) so it casts real warm light, not a tint.</summary>
        public Rgb BaseColor { get; set; } = new(2.4f, 1.0f, 0.32f);
    }
}
