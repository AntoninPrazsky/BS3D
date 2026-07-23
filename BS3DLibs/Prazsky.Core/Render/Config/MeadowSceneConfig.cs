using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the meadow backdrop: the vivid green "Bliss" hill rolling to a crisp horizon,
    /// combed by wind, scattered with wildflowers.
    /// </summary>
    public sealed class MeadowSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Meadow;

        /// <summary>Basin the arena sits in, rising into rolling hills with distance; stays below the platform glass (about -10.7).</summary>
        public float LevelY { get; set; } = -14f;

        /// <summary>Peak height of the rolling hills that rise with distance.</summary>
        public float HillHeight { get; set; } = 40f;

        /// <summary>Flat clearing radius around the arena centre before the hills begin.</summary>
        public float ClearingRadius { get; set; } = 95f;

        /// <summary>Distance over which the flat clearing ramps up into hills.</summary>
        public float ClearingTransition { get; set; } = 140f;

        /// <summary>Gentle basin relief within the clearing.</summary>
        public float ClearingRelief { get; set; } = 1.5f;

        /// <summary>Lush green (linear).</summary>
        public Rgb GrassColor { get; set; } = new(0.14f, 0.46f, 0.05f);

        /// <summary>Darker green the grass varies towards in patches (linear).</summary>
        public Rgb GrassColorDark { get; set; } = new(0.08f, 0.27f, 0.04f);

        /// <summary>How much sky fills the flats (ambient strength).</summary>
        public float AmbientStrength { get; set; } = 0.7f;

        /// <summary>Distance over which the field melts into the skyline.</summary>
        public float HorizonHazeDistance { get; set; } = 580f;

        /// <summary>Wind direction combing the grass and drifting the fine relief.</summary>
        public Vec2 Wind { get; set; } = new(0.82f, 0.57f);

        /// <summary>How fast the bright/dark wind bands travel.</summary>
        public float WindRippleSpeed { get; set; } = 1.4f;

        /// <summary>How far apart the wind bands are.</summary>
        public float WindRippleFrequency { get; set; } = 0.15f;

        /// <summary>How deep the wind bands cut.</summary>
        public float WindRippleStrength { get; set; } = 0.12f;

        /// <summary>Fine grass texture amplitude (a normal-tilting height field).</summary>
        public float GrassReliefStrength { get; set; } = 0.05f;

        /// <summary>Fine grass texture blades-per-world-unit.</summary>
        public float GrassReliefFrequency { get; set; } = 2f;

        /// <summary>The scattered wildflowers.</summary>
        public FlowersConfig Flowers { get; set; } = new();
    }

    /// <summary>
    /// Wildflowers scattered through the meadow. Petal count, rotation and the petal colours are driven by
    /// per-cell hashes inside the shader and are not exposed here; these are the named density/size dials.
    /// </summary>
    public sealed class FlowersConfig
    {
        /// <summary>How many of the grid cells carry a wildflower.</summary>
        public float Density { get; set; } = 0.16f;

        /// <summary>How far apart the wildflower grid cells are.</summary>
        public float Spacing { get; set; } = 2.2f;

        /// <summary>The wildflower size.</summary>
        public float Size { get; set; } = 0.22f;
    }
}
