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
        public PolarSceneConfig()
        {
            Weather = WeatherPreset.Clear;

            //The sun's cast shadows (#471), at the savanna's own figures — 0.9 is a shadow that is dark without
            //reading as a hole, and 260 units at 2048 is 0.13 units a texel. The shadow lands in the sky's own
            //blue here, which is the colour this scene's shadows have always been — the ambient term is what
            //carries the picture (see Polar.fx).
            Shadows = new ShadowConfig(strength: 0.9f);
        }

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
        /// <para>
        /// 0.22 → <b>0.48</b> (#511): both low-sun references are gold on the crests against deep blue in the
        /// troughs, and that contrast is <i>form</i> shading — a lit face and a shaded one. At 0.22 the
        /// perturbation tilted the normal so little that N·L barely varied across a drift, so the plain read
        /// as one tone with a pattern printed on it rather than as carved snow.
        /// </para>
        /// </summary>
        public float DriftAmplitude { get; set; } = 0.48f;

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

        /// <summary>
        /// Radius of the flat clearing the island stands in, before the field rises.
        /// <para>
        /// ⚠ It also gates the crevasses (<c>CrevasseField</c> holds them off out to <c>0.8 ×</c> this and
        /// opens them fully at <c>1.5 ×</c>), and at 70 that put the scene's signature feature entirely
        /// outside <b>56 units</b> — on an island only 26 in radius (#511). The owner's original report was
        /// that the cracked ice glowing blue is nowhere to be seen; ungating it from the distant front was
        /// half the answer and this was the other half. 42 keeps the arena whole for the stated reason — a
        /// crack under the island would be a hole the player cannot fall into — and lets the nearest slot
        /// open at about 34 units, where it can actually be looked at.
        /// </para>
        /// </summary>
        public float ClearingRadius { get; set; } = 42f;

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

        /// <summary>
        /// How high the pressure front stands above the plain. ⚠ <b>Low against its own width on purpose</b>:
        /// a plate taller than it is broad is a column, and a row of columns is what made the first front
        /// unreadable. The belt is <see cref="RidgeWidth"/> deep, so this is well under a tenth of it.
        /// </summary>
        public float RidgeHeight { get; set; } = 11f;

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
        /// <para>
        /// ⚠ It said "deliberately shallow" at <b>8</b>, against a slot 3.3 units wide — deeper than wide, on
        /// a 2.5-unit grid (#511). The dip was smeared over two or three cells into a broad V whose flanks
        /// came out near-vertical, so <c>steepness</c> saturated along them, <c>iceness</c> went to 1 and
        /// they drew as bare ice <i>brighter</i> than the snow: measured at luminance 155–169 against the
        /// snow's 137–162, which is the bright welt this scene's comments record arguing out once already.
        /// </para>
        /// </summary>
        public float CrevasseDepth { get; set; } = 3f;

        /// <summary>How close together the crevasses run, in slots per world unit.</summary>
        public float CrevasseFrequency { get; set; } = 0.075f;

        /// <summary>
        /// How narrowly each crevasse is cut — higher is a thinner slot. ⚠ Crevasses are on the <b>plain</b>
        /// now and not only at the pressure front, which is the correction the owner's report forced: gated
        /// on the front's strain they existed only hundreds of units out, where no camera ever goes, so the
        /// cracked ice this scene is <i>for</i> was nowhere to be seen. They open in fields, and the clearing
        /// the island stands on is kept whole.
        /// <para>
        /// 8 → <b>4</b> (#511): the field is <c>sin(x · CrevasseFrequency)</c> cut at <c>|lines| &lt; 1/s</c>,
        /// so 8 is a slot about 3.3 world units across and 4 is about 6.7 — two and a half grid cells, which
        /// is what the desert's rule asks for. See <see cref="CrevasseDepth"/> for what the narrow one cost.
        /// </para>
        /// </summary>
        public float CrevasseSharpness { get; set; } = 4f;

        /// <summary>
        /// How far the inside of a slot goes down against its own lip (1 is no darkening at all).
        /// <para>
        /// ⚠ <b>Without it a crevasse reads as a welt rather than a crack.</b> With the transmission alone the
        /// slot came out <i>brighter</i> than the snow around it, and a bright band on a white plain is a
        /// ridge standing proud of it — the same inversion the dead ball's tint and the rock's relief both had
        /// to be argued out of. The light down there is genuinely dim: what reaches the eye crossed metres of
        /// ice, and what makes it beautiful is that the little which arrives is pure colour.
        /// </para>
        /// </summary>
        public float CrevasseDarkening { get; set; } = 0.22f;

        /// <summary>
        /// Where the bare glacier starts showing through, as the threshold on the field that decides it — so a
        /// <b>lower</b> number scours more of the sheet bare.
        /// <para>
        /// ⚠ <b>This is the scene's cyan, and the first build had nowhere to put it.</b> Transmission needs
        /// thick ice in the line of sight, which on a flat plain means neither a crevasse wall nor a steep
        /// flank — so the signature colour lived only inside the distant front. A blue-ice area is the real
        /// polar answer rather than an invention: whole regions of a sheet are swept bare by the wind, they
        /// are glass-hard, and they are the blue in every polar photograph that has blue in it.
        /// </para>
        /// </summary>
        public float BlueIceThreshold { get; set; } = 0.12f;

        /// <summary>
        /// How big one plate of the pressure front is across, and how far it leans.
        /// <para>
        /// ⚠ <b>Quantised in world space and tilted, which is the second try at this front.</b> Cut in polar
        /// coordinates the cells were wedges that grew with distance, so their hashed heights met along radial
        /// seams and the belt read as a picket fence of crooked needles — the owner's word was "a graphical
        /// glitch", which is the right word for a silhouette nobody can name. Real pressure ice is plates:
        /// wider than they are tall, shoved up and leaning, chaotic at the top and continuous along the front.
        /// </para>
        /// </summary>
        public float SlabSize { get; set; } = 17f;

        /// <inheritdoc cref="SlabSize"/>
        public float SlabTilt { get; set; } = 0.55f;

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

        /// <summary>
        /// How much of the sky's hemisphere light fills the flats. 0.5 → <b>0.40</b> (#511): at 0.5 the fill
        /// lifted what little shade <see cref="DriftAmplitude"/> was making and the troughs came out the
        /// average of everything rather than the dome's blue, which is the colour a snow shadow is.
        /// </summary>
        public float AmbientStrength { get; set; } = 0.40f;

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
