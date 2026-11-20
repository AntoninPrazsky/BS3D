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
        /// The sea is the moodiest backdrop in the set and its own doc says a bright dome gives it a breezy mood rather than the intended one - a deck of broken cloud over open water is the weather that makes it read as weather at sea rather than as a postcard.
        /// <para>
        /// <see cref="WeatherPreset.Mixed"/> since #1172, Broken's clouds with as much open sky between them: under Broken
        /// itself the sun stood behind cloud from almost everywhere, and the owner: "the cloud cover seems too large to me - it
        /// does not quite correspond to the sun's reflection on the water".
        /// </para>
        /// </summary>
        public SeaSceneConfig() => Weather = WeatherPreset.Mixed;

        /// <summary>Mean water level (world Y). Kept well below the platform so a rough sea has headroom for its crests.</summary>
        public float LevelY { get; set; } = -13f;

        /// <summary>
        /// Deep-body water colour (linear reflectance, multiplied by skylight in the shader). Both body colours are
        /// <see cref="BODY_DARKENING"/> of what they were until #1171 (0.02, 0.05, 0.07 and 0.07, 0.17, 0.19), the hue kept:
        /// the owner, "the sea is actually darker", and the sea's own dome kept 13's brightness for the island's sake, so the
        /// sea's own darkness is its body's.
        /// </summary>
        public Rgb WaterDeep { get; set; } = new(0.02f * BODY_DARKENING, 0.05f * BODY_DARKENING, 0.07f * BODY_DARKENING);

        /// <summary>Paler up-facing water colour (linear reflectance), darkened with <see cref="WaterDeep"/>.</summary>
        public Rgb WaterShallow { get; set; } = new(0.07f * BODY_DARKENING, 0.17f * BODY_DARKENING, 0.19f * BODY_DARKENING);

        /// <summary>
        /// How much of the body's reflectance the sea kept in #1171. The body is a reflectance times the dome's light plus a
        /// twentieth of its zenith (<c>Sea.fx</c>), so its luminance falls by less than this: to about seven tenths of what it
        /// was under dome 13 (<c>SeaSkyTests</c>). At a grazing look the water is mostly the sky's mirror, which this leaves alone.
        /// </summary>
        public const float BODY_DARKENING = 0.55f;

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

        /// <summary>Camera distance at which the waves are fully flat (clean hazed horizon), and the distance from the arena past
        /// which they are flat whoever looks (<see cref="CalmFrom"/>).</summary>
        public float WaveFadeEnd { get; set; } = 780f;

        /// <summary>
        /// Distance from the ARENA at which the swell begins to calm, flat by <see cref="WaveFadeEnd"/> from it whatever the
        /// camera's distance (#1124). The oil platforms stand past <see cref="WaveFadeEnd"/>, cut at a flat waterline, and the
        /// chapter intro flies a lens out to them: with the camera's fade alone the swell came with it, and their legs stood
        /// in front of its crests and over its troughs. A view from round the arena that looks in at it is unchanged; one that
        /// looks out to sea sees the last stretch before <see cref="WaveFadeEnd"/> a little calmer (see <c>Sea.fx</c>'s
        /// <c>CalmFrom</c>). 0 turns it off.
        /// </summary>
        public float CalmFrom { get; set; } = 600f;

        /// <summary>
        /// Distance from the ARENA at which the swell stops heaving the surface (#1170): from here to <see cref="StillAt"/> its
        /// displacement fades to nothing whatever the camera's distance, and only its displacement. The lighthouse, the ship and
        /// the yacht (<c>SeaTrafficLayout</c>) stand past <see cref="StillAt"/>, cut at a flat waterline, so no hull stands
        /// over a trough or before a crest. Their light (the normal, the crest's foam and colour, the chop) is left to the
        /// camera's fade: a calm as near as this one that took the light as well, as <see cref="CalmFrom"/> does, would lay
        /// glass over the play view's far water. See <c>Sea.fx</c>'s <c>StillAt</c>. 0 at <see cref="StillAt"/> turns it off.
        /// </summary>
        public float StillFrom { get; set; } = 260f;

        /// <summary>Distance from the arena past which the surface stands still, its swell's light still playing on it
        /// (<see cref="StillFrom"/>).</summary>
        public float StillAt { get; set; } = 360f;

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
        public float FoamCrestStart { get; set; } = 0.52f;

        /// <summary>Strength of the crest-height foam.</summary>
        public float FoamCrestStrength { get; set; } = 1.0f;

        /// <summary>Foam colour (linear).</summary>
        public Rgb FoamColor { get; set; } = new(0.97f, 0.98f, 1.0f);

        /// <summary>
        /// Strength of the subsurface crest glow when the sun is behind a wave. 0.7 until #1171: over the slate water of the
        /// sea's own dome (22) that laid a turquoise sheet over the whole sunward quarter of the view (hue 177, saturation 0.45
        /// at the Testbed's low view into the sun), where the references of a dusk sea draw dark water with the light only in
        /// the thin crests.
        /// </summary>
        public float SssStrength { get; set; } = 0.35f;

        /// <summary>Green-blue colour light takes coming through the water (linear): a sea's, bluer than the lagoon's green
        /// (0.15, 0.55, 0.50 until #1171).</summary>
        public Rgb SssColor { get; set; } = new(0.12f, 0.42f, 0.46f);

        /// <summary>
        /// How far the sun's glint and the crests' glow take the sun's own beam rather than the light under the deck
        /// (<c>Sea.fx</c>'s <c>SunBeamGate</c>, #1172): 1, so a glitter path lies on the water only while the sun is out of
        /// cloud. The light under the deck never falls under the weather's floor, and at that floor the glint drew a path
        /// under a sky with no sun in it. The tropical lagoon, which shares the shader, pushes 0 and keeps the floored light.
        /// </summary>
        public float SunBeamGate { get; set; } = 1f;

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
        public int ParticleCount { get; set; } = 3000;

        /// <summary>Slab size: wide in XZ (follows the camera), thin in Y (clings to the surface).</summary>
        public Vec3 BoxSize { get; set; } = new(200f, 10f, 200f);

        /// <summary>Slab centre height above the mean sea level.</summary>
        public float LevelYAboveSea { get; set; } = 2f;

        /// <summary>Downwind drift, aligned with the sea wind but much faster.</summary>
        public Vec2 Wind { get; set; } = new(30f, 11f);

        /// <summary>Slow vertical churn.</summary>
        public float Rise { get; set; } = 3f;

        /// <summary>Per-particle turbulent sway.</summary>
        public float Turbulence { get; set; } = 1.6f;

        /// <summary>Droplet size.</summary>
        public float DropletSize { get; set; } = 0.10f;

        /// <summary>
        /// Spray colour (linear). Its luminance is deliberately kept just under the glare threshold — at a
        /// grazing angle the view ray stacks hundreds of particles to full opacity, so a brighter colour
        /// would bloom into a starfield. See SceneRenderer / CLAUDE.md before raising it.
        /// <para>
        /// Since #675 the shader premultiplies, so this is what a full stack of droplets converges ON and no longer
        /// what each droplet ADDS: luminance 0.535 against the 0.55 threshold, the most the colour can be. Until
        /// then it was (0.33, 0.37, 0.43) and every droplet drew it whole.
        /// </para>
        /// </summary>
        public Rgb Color { get; set; } = new(0.50f, 0.54f, 0.58f);

        /// <summary>Per-particle opacity. 0.38 until #675, when premultiplying the shader made the same figure far
        /// fainter; 0.70 with the brighter colour, a larger <see cref="DropletSize"/> and more particles brings the
        /// spray back to visible soft streaks without the hard outline the straight alpha gave every droplet.</summary>
        public float Opacity { get; set; } = 0.70f;
    }
}
