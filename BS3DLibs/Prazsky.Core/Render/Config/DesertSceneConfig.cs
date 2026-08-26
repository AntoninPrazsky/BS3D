using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the desert backdrop: rolling sand dunes rising beyond a flat clearing, fine wind
    /// ripples, a veil of blown dust, and the shared flock of birds circling overhead.
    /// </summary>
    public sealed class DesertSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Desert;

        /// <summary>
        /// A desert sky is empty, and that is the whole of why the scene reads as one - dunes under unbroken sun. It is also the honest use of Clear: a scene that wants no weather rather than a scene that cannot have any.
        /// </summary>
        public DesertSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>Mean sand level in the clearing (the island's foot).</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>Roughly the peak dune height above the mean level out in the far field.</summary>
        public float DuneAmplitude { get; set; } = 14f;

        /// <summary>Radius of the flat clearing the island stands in, before dunes rise.</summary>
        public float ClearingRadius { get; set; } = 80f;

        /// <summary>Transition band over which the flat clearing rises into dunes.</summary>
        public float ClearingTransition { get; set; } = 120f;

        /// <summary>Fine wind ripples: peak height (world units).</summary>
        public float RippleAmplitude { get; set; } = 0.10f;

        /// <summary>Fine wind ripples: ripples per world unit.</summary>
        public float RippleFrequency { get; set; } = 1.6f;

        /// <summary>Fine wind ripples: how fast they crawl downwind.</summary>
        public float RippleSpeed { get; set; } = 1.4f;

        /// <summary>Blown dust veil: strength.</summary>
        public float DustStrength { get; set; } = 0.3f;

        /// <summary>Blown dust veil: drift speed.</summary>
        public float DustSpeed { get; set; } = 6f;

        /// <summary>Blown dust veil: distance over which it thickens towards the horizon.</summary>
        public float DustStart { get; set; } = 240f;

        /// <summary>Warm sand reflectance (linear) — the ochre of the ripple troughs. Also reused in-shader as the dust colour.</summary>
        public Rgb SandColor { get; set; } = new(0.55f, 0.38f, 0.18f);

        /// <summary>
        /// The bleached pale tone (linear) the sand mixes towards in patches — the colour of a sun-scoured
        /// crest. Sand is never one colour, and one colour is exactly what this scene looked like before:
        /// a broad noise field mixes the two, so the dunes carry patches instead of reading as a lit floor.
        /// Warmer than white on purpose, so a patch of it is still sand rather than a bald spot.
        /// </summary>
        public Rgb SandColorPale { get; set; } = new(0.82f, 0.68f, 0.44f);

        /// <summary>
        /// How hard the sun glints off the sand at a grazing angle. Quartz grains are little mirrors, so sand
        /// is near-matte rather than matte: with the sun low, a dune's crest lines carry a sheen. A wide lobe,
        /// because the reflection is off a million grains facing every way — 0 is the old flat-matte look.
        /// </summary>
        public float SheenStrength { get; set; } = 0.35f;

        /// <summary>How much of the sky's hemisphere light fills the flats.</summary>
        public float AmbientStrength { get; set; } = 0.65f;

        /// <summary>The wind, a direction in the XZ plane.</summary>
        public Vec2 Wind { get; set; } = new(0.86f, 0.51f);

        /// <summary>World distance over which the dunes melt into the skyline haze.</summary>
        public float HorizonHazeDistance { get; set; } = 420f;

        /// <summary>The shared flock of birds circling overhead.</summary>
        public BirdsConfig Birds { get; set; } = new();
    }
}
