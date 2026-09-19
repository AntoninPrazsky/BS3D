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

        /// <summary>
        /// Fair-weather cumulus over green hills under the one clear blue dome - the game's opening chapter plays here and its sky is the one the whole feature's numbers were tuned against.
        /// </summary>
        public MeadowSceneConfig()
        {
            Weather = WeatherPreset.Scattered;

            //The sun's cast shadows (#471), at the savanna's own figures — 0.9 is a shadow that is dark without
            //reading as a hole, and 260 units at 2048 is 0.13 units a texel. The island and the gun are the
            //only things standing on the hill, and this is the chapter a new player opens the game on — the one
            //cast shadow they see first.
            Shadows = new ShadowConfig(strength: 0.9f);
        }

        /// <summary>Basin the arena sits in, rising into rolling hills with distance; stays below the island
        /// top (<see cref="ArenaIsland.TOP_Y"/>, -8.5 — the old -10.7 referent was the removed recessed glass
        /// bath).</summary>
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

        /// <summary>
        /// The light, drier colour a blade's tip shows where the relief stands high (#281), linear. Grass that is
        /// one green from root to tip is the "green stone" the issue names.
        /// </summary>
        public Rgb GrassTipColor { get; set; } = new(0.36f, 0.50f, 0.11f);

        /// <summary>How far the tips lighten towards <see cref="GrassTipColor"/> and the hollows between them darken, 0..1.</summary>
        public float GrassTipStrength { get; set; } = 0.7f;

        /// <summary>World units across one clump of grass (#281).</summary>
        public float GrassClumpSize { get; set; } = 0.55f;

        /// <summary>How much one clump's shade differs from the next, and how dark the seams between them are, 0..1.</summary>
        public float GrassClumpStrength { get; set; } = 0.6f;

        /// <summary>How far the broad dry patches pull the grass towards yellow-green, 0..1.</summary>
        public float GrassDryPatchStrength { get; set; } = 0.45f;

        /// <summary>
        /// The velvet (#281): light the field gives back where it is seen edge-on, which is what makes a far slope
        /// read as a soft carpet rather than as a painted hill.
        /// </summary>
        public float GrassSheenStrength { get; set; } = 0.35f;

        /// <summary>The glow of blades lit from behind, looking towards the sun (#281).</summary>
        public float GrassTranslucency { get; set; } = 0.9f;

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
