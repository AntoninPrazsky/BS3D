using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the sea backdrop: the Gerstner swell, fine wind chop, whitecap foam, subsurface
    /// crest glow, sky/sun reflection and the blown spray riding over it. Defaults match the constants
    /// that drove the scene before it became configurable.
    /// </summary>
    public sealed class SeaSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Sea;

        /// <summary>
        /// The sea is the moodiest backdrop in the set and its own doc says a bright dome gives it a breezy mood rather than the intended one - a broken deck over open water is the weather that makes it read as weather at sea rather than as a postcard.
        /// </summary>
        public SeaSceneConfig() => Weather = WeatherPreset.Broken;

        /// <summary>Mean water level (world Y). Kept well below the platform so a rough sea has headroom for its crests.</summary>
        public float LevelY { get; set; } = -13f;

        /// <summary>Deep-body water colour (linear reflectance, multiplied by skylight in the shader).</summary>
        public Rgb WaterDeep { get; set; } = new(0.02f, 0.05f, 0.07f);

        /// <summary>Paler up-facing water colour (linear reflectance).</summary>
        public Rgb WaterShallow { get; set; } = new(0.07f, 0.17f, 0.19f);

        /// <summary>
        /// How much of the body colour is <see cref="WaterShallow"/> regardless of which way the surface
        /// faces. <b>Zero here, and it must stay zero:</b> the open sea has nothing under it, so its paler
        /// colour belongs only to the up-facing faces of the swell — which is the rule <c>Sea.fx</c> was
        /// written with and this scene's whole look is tuned against. It exists for the tropical lagoon,
        /// which shares this shader and is shallow everywhere (see <see cref="TropicalWaterConfig"/>), and
        /// at 0 the shader's mix is bit-exactly what it always was.
        /// </summary>
        public float ShallowBias { get; set; } = 0f;

        /// <summary>Dominant swell height in world units. Kept low enough that the tallest crests stay below the platform.</summary>
        public float WaveAmplitude { get; set; } = 0.6f;

        /// <summary>Crest sharpness (0..1 Gerstner steepness).</summary>
        public float WaveSteepness { get; set; } = 0.95f;

        /// <summary>Multiplier on the dispersion-derived wave speed.</summary>
        public float WaveSpeed { get; set; } = 1.2f;

        /// <summary>Camera distance at which the waves begin fading towards flat.</summary>
        public float WaveFadeStart { get; set; } = 340f;

        /// <summary>Camera distance at which the waves are fully flat (clean hazed horizon).</summary>
        public float WaveFadeEnd { get; set; } = 780f;

        /// <summary>Fine per-pixel wind-chop peak height in world units.</summary>
        public float ChopAmplitude { get; set; } = 0.16f;

        /// <summary>Wind-chop ripples per world unit.</summary>
        public float ChopFrequency { get; set; } = 1.0f;

        /// <summary>Wind-chop scroll speed.</summary>
        public float ChopSpeed { get; set; } = 1.6f;

        /// <summary>Wind direction the swell and chop travel along (roughly a unit vector in XZ).</summary>
        public Vec2 Wind { get; set; } = new(0.94f, 0.34f);

        /// <summary>Strength of the sharp sun glint on the water.</summary>
        public float SunGlintStrength { get; set; } = 12f;

        /// <summary>Sun-glint specular power (higher = tighter spark).</summary>
        public float SunGlintPower { get; set; } = 500f;

        /// <summary>How far the wave Jacobian must fold before foam shows (nearer 1 = more foam).</summary>
        public float FoamJacobianThreshold { get; set; } = 0.5f;

        /// <summary>Strength of the fold-driven foam.</summary>
        public float FoamStrength { get; set; } = 1.1f;

        /// <summary>
        /// Where on a crest the height-driven foam begins (0..1, on the combined swell height). Lowered from
        /// 0.72 with #128: at 0.72 the gate only opened where several waves constructively interfered — which
        /// is a localized round bump, and is why the old height-thresholded foam painted round white blobs —
        /// while 0.6 opens on the dominant swell's own ordinary crests. The gate no longer draws any shape of
        /// its own (it only sets the density of the streak field in <c>Sea.fx</c>), so opening it wider makes
        /// whitecap lanes more frequent, not discs bigger.
        /// </summary>
        public float FoamCrestStart { get; set; } = 0.6f;

        /// <summary>Strength of the crest-height foam.</summary>
        public float FoamCrestStrength { get; set; } = 0.7f;

        /// <summary>Foam colour (linear).</summary>
        public Rgb FoamColor { get; set; } = new(0.8f, 0.85f, 0.9f);

        /// <summary>Strength of the subsurface crest glow when the sun is behind a wave.</summary>
        public float SssStrength { get; set; } = 0.7f;

        /// <summary>Warm green-blue colour light takes coming through the water (linear).</summary>
        public Rgb SssColor { get; set; } = new(0.15f, 0.55f, 0.50f);

        /// <summary>Distance over which the finite water fades into the skyline.</summary>
        public float HorizonHazeDistance { get; set; } = 700f;

        /// <summary>Blown spray and spindrift riding over the waves.</summary>
        public SprayConfig Spray { get; set; } = new();
    }

    /// <summary>
    /// Blown spray / spindrift: a thin slab of billboards hugging the sea surface, whipped downwind.
    /// (<see cref="ParticleCount"/> is a look/mood dial like the mountain's snowflake count; changing it
    /// at runtime requires rebuilding the spray vertex buffer.)
    /// </summary>
    public sealed class SprayConfig
    {
        /// <summary>Number of spray billboards.</summary>
        public int ParticleCount { get; set; } = 2000;

        /// <summary>Slab size: wide in XZ (follows the camera), thin in Y (clings to the surface).</summary>
        public Vec3 BoxSize { get; set; } = new(200f, 16f, 200f);

        /// <summary>Slab centre height above the mean sea level.</summary>
        public float LevelYAboveSea { get; set; } = 2f;

        /// <summary>Downwind drift, aligned with the sea wind but much faster.</summary>
        public Vec2 Wind { get; set; } = new(30f, 11f);

        /// <summary>Slow vertical churn.</summary>
        public float Rise { get; set; } = 3f;

        /// <summary>Per-particle turbulent sway.</summary>
        public float Turbulence { get; set; } = 1.6f;

        /// <summary>Droplet size.</summary>
        public float DropletSize { get; set; } = 0.12f;

        /// <summary>
        /// Spray colour (linear). Its luminance is deliberately kept just under the glare threshold — at a
        /// grazing angle the view ray stacks hundreds of particles to full opacity, so a brighter colour
        /// would bloom into a starfield. See SceneRenderer / CLAUDE.md before raising it.
        /// </summary>
        public Rgb Color { get; set; } = new(0.33f, 0.37f, 0.43f);

        /// <summary>Per-particle opacity.</summary>
        public float Opacity { get; set; } = 0.38f;
    }
}
