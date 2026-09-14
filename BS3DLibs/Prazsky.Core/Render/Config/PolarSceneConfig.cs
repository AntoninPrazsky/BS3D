using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the polar backdrop (#222): a flat icesheet carved into sastrugi by the wind, a
    /// crevassed pressure ridge standing out of it at distance, and — the scene's real content — ice that is
    /// white where it reflects the sky and cyan where the light goes through it.
    /// <para>
    /// It is the desert's machinery with polar shapes on it (a camera-centred displaced grid, per-pixel
    /// normals from the height field), and the two scenes differ in what they are made of rather than in how
    /// they are drawn. What it must never read as is the mountain flattened: that scene is an alpine basin
    /// with relief and falling snow, this one is a plain whose subject is the material.
    /// </para>
    /// </summary>
    public sealed class PolarSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Polar;

        /// <summary>
        /// A polar sky at its most legible is a clear one: the low sun has to reach the ice, and every shape
        /// in this scene is read by the shadow it casts. The overcast version of this place exists and is a
        /// real look — a whiteout with no shadows at all — but it is the one weather in which this scene has
        /// nothing to show, so it is not the default.
        /// </summary>
        public PolarSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>Mean ice level in the clearing (the island's foot).</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>
        /// How deep the wind-carved sastrugi are cut into the surface, in world units of relief.
        /// <para>
        /// ⚠ <b>They are a surface and not a terrain</b>, which is this scene's first real mistake corrected:
        /// built as displacement at the scale a sastrugi actually has — a few metres across — each one landed
        /// at about one grid cell, so the mesh could not hold it and the field photographed as coarse dunes
        /// with facets on them. An icesheet <i>is</i> flat; what the eye reads on it is texture. So the
        /// geometry carries <see cref="SwellAmplitude"/> and these are a normal perturbation, exactly as the
        /// desert's dunes are geometry and its ripples are not.
        /// </para>
        /// </summary>
        public float DriftAmplitude { get; set; } = 0.22f;

        /// <summary>Sastrugi across the wind, in ridges per world unit.</summary>
        public float DriftFrequency { get; set; } = 0.28f;

        /// <summary>
        /// How high the broad swells stand — the long, low undulation a sheet has where it flows over what is
        /// under it, and the only shape the geometry carries out on the open field.
        /// </summary>
        public float SwellAmplitude { get; set; } = 4.5f;

        /// <summary>
        /// How far the ridges are drawn out <b>along</b> the wind against their spacing across it. The
        /// anisotropy is the shape and not a decoration: an isotropic field of the same amplitude reads as
        /// gravel, which is the savanna's own finding (#117) arriving on snow.
        /// </summary>
        public float DriftStretch { get; set; } = 4.5f;

        /// <summary>Radius of the flat clearing the island stands in, before the field rises.</summary>
        public float ClearingRadius { get; set; } = 70f;

        /// <summary>Transition band over which the flat clearing rises into the open field.</summary>
        public float ClearingTransition { get; set; } = 90f;

        /// <summary>
        /// Where the pressure ridge stands, as a distance from the arena. A <b>belt</b> rather than a wall
        /// across the view, so the expanse keeps its flat horizon in every direction but one and still has a
        /// skyline to read distance against — on a white plain there is nothing else to read it by.
        /// </summary>
        public float RidgeRadius { get; set; } = 300f;

        /// <summary>How wide the pressure ridge belt is.</summary>
        public float RidgeWidth { get; set; } = 70f;

        /// <summary>How high the pressure ridge stands above the plain.</summary>
        public float RidgeHeight { get; set; } = 26f;

        /// <summary>
        /// Which way the front lies from the arena, in degrees, and how much of the horizon it covers.
        /// <para>
        /// ⚠ <b>A front and not a ring</b>, which is the correction a photograph forced: closed all the way
        /// round, the belt read as an arena wall and the scene stopped being an expanse. #222 asks for a flat
        /// white expanse <i>to the horizon</i> above everything else, so the ice covers a sector and the rest
        /// of the circle is open to the skyline — and a player orbiting the gun gets both halves of the
        /// postcard in one level instead of the same wall from every angle.
        /// </para>
        /// </summary>
        public float RidgeBearingDegrees { get; set; } = 200f;

        /// <inheritdoc cref="RidgeBearingDegrees"/>
        public float RidgeSpanDegrees { get; set; } = 150f;

        /// <summary>
        /// How deep the crevasses dip in the <b>geometry</b>. Deliberately shallow: the grid's cell is a
        /// couple of world units and a feature narrower than several cells falls between vertices, which is
        /// the desert's own rule. The depth the eye reads is carried by the transmission instead — a crevasse
        /// is a slot that glows cyan and darkens with its own depth, which is what one looks like from above.
        /// </summary>
        public float CrevasseDepth { get; set; } = 6f;

        /// <summary>How close together the crevasses run, in slots per world unit.</summary>
        public float CrevasseFrequency { get; set; } = 0.035f;

        /// <summary>
        /// Snow reflectance (linear). <b>Not white</b>, and that is the scene's central trap answered: snow
        /// is the brightest thing this game draws, and an albedo of one under a sunlit dome has nowhere left
        /// to go — it clips, and every fold in it flattens into the same tone under the tonemap. A touch
        /// under one and slightly cool, so the sunlit white has something to be white against.
        /// </summary>
        public Rgb SnowColor { get; set; } = new(0.72f, 0.76f, 0.82f);

        /// <summary>
        /// What the ice carries <b>inside</b> it (linear): the cyan the transmission converges to as the
        /// light's path through the ice lengthens. The scene's signature colour, and the one thing here that
        /// must not be tinted towards the snow — white in reflection against cyan in transmission is the
        /// whole look.
        /// </summary>
        public Rgb IceColor { get; set; } = new(0.17f, 0.55f, 0.62f);

        /// <summary>How much of the sky's hemisphere light fills the flats.</summary>
        public float AmbientStrength { get; set; } = 0.5f;

        /// <summary>
        /// How hard the glaze reflects the sun. A <b>tight</b> lobe rather than the desert's broad sheen:
        /// scoured ice is polished, so it carries a thin glare line along a flank instead of a haze over the
        /// whole field.
        /// </summary>
        public float SheenStrength { get; set; } = 0.35f;

        /// <summary>
        /// How much the near snow glitters. #222 asks for diamond dust as this scene's own event; this is the
        /// ground half of it — crystals in the snow catching the sun — and the airborne version is a separate
        /// overlay in <c>Snow.fx</c>'s shape that this pass does not claim.
        /// </summary>
        public float SparkleStrength { get; set; } = 1.2f;

        /// <summary>How strongly the ice carries light through itself.</summary>
        public float TransmissionStrength { get; set; } = 1.3f;

        /// <summary>The wind, a direction in the XZ plane. Every shape in this scene is made by it.</summary>
        public Vec2 Wind { get; set; } = new(0.94f, 0.34f);

        /// <summary>World distance over which the field melts into the skyline.</summary>
        public float HorizonHazeDistance { get; set; } = 900f;
    }
}
