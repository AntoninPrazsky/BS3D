using System;
using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the space backdrop: the island floating in deep space under a long-exposure sky —
    /// a dense starfield, the Milky Way with its dust lanes, three coloured emission nebulae, scattered
    /// distant galaxies and one large planet.
    /// <para>
    /// The ninth scene, and the only one that replaces the <b>sky</b> rather than the ground, so it carries
    /// no terrain level, no clearing radius and no horizon haze — there is no horizon. Angles are in
    /// <b>degrees</b> here and converted to radians where they are pushed at the shader: a designer types
    /// "the planet is twelve degrees across", not "0.2094".
    /// </para>
    /// Every colour is <b>linear radiance</b> like every other scene config's, and everything <b>small</b> is
    /// authored to stay under the glare threshold (0.55 on luminance) — see <c>Space.fx</c> for why a point of
    /// light must not bloom here while the balls in front of it still do. The planet's lit limb is the one
    /// deliberate exception, being a long coherent arc rather than a point (see <see cref="SpacePlanetConfig.RimStrength"/>).
    /// </summary>
    public sealed class SpaceSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Space;

        /// <summary>
        /// The empty sky between everything else (linear). Deliberately not pure black: a real deep-sky frame
        /// has an airglow and zodiacal floor, and a sky that goes to zero reads as a hole rather than as
        /// distance.
        /// </summary>
        public Rgb VoidColor { get; set; } = new(0.0030f, 0.0038f, 0.0068f);

        /// <summary>The starfield: three layers, coarse to fine.</summary>
        public SpaceStarsConfig Stars { get; set; } = new();

        /// <summary>The volume the island is inside — the one layer with depth rather than only a direction.</summary>
        public SpaceVolumeConfig Volume { get; set; } = new();

        /// <summary>The galactic band and its dust lanes.</summary>
        public SpaceMilkyWayConfig MilkyWay { get; set; } = new();

        /// <summary>The dominant nebula — hydrogen-alpha, the deep red of an emission region.</summary>
        public SpaceNebulaConfig NebulaOne { get; set; } = new()
        {
            Direction = new Vec3(-0.18f, 0.26f, -0.95f),
            Color = new Rgb(1.00f, 0.23f, 0.38f),
            AngularRadiusDegrees = 27f,
            Strength = 0.78f,
            DetailScale = 5.4f,
            Warp = 1.5f,
        };

        /// <summary>The second nebula — doubly ionised oxygen, the teal an astrophoto is half made of.</summary>
        public SpaceNebulaConfig NebulaTwo { get; set; } = new()
        {
            Direction = new Vec3(0.74f, 0.14f, 0.66f),
            Color = new Rgb(0.16f, 0.88f, 0.80f),
            AngularRadiusDegrees = 21f,
            Strength = 0.52f,
            DetailScale = 7.1f,
            Warp = 1.8f,
        };

        /// <summary>The third nebula — a blue reflection region, dust lit by the stars inside it.</summary>
        public SpaceNebulaConfig NebulaThree { get; set; } = new()
        {
            Direction = new Vec3(-0.55f, -0.42f, 0.72f),
            Color = new Rgb(0.34f, 0.48f, 1.00f),
            AngularRadiusDegrees = 17f,
            Strength = 0.44f,
            DetailScale = 8.6f,
            Warp = 1.3f,
        };

        /// <summary>The field of distant galaxies.</summary>
        public SpaceGalaxyConfig Galaxies { get; set; } = new();

        /// <summary>The planet hanging over the arena. An angular radius of 0 removes it.</summary>
        public SpacePlanetConfig Planet { get; set; } = new();

        /// <summary>What lights the island, the gun and the balls here, since there is no dome to derive it from.</summary>
        public SpaceLightingConfig Lighting { get; set; } = new();
    }

    /// <summary>
    /// The light rig of the space scene — the one scene that states its own rather than deriving it from the
    /// sky dome, because it draws no dome. Two things forced this rather than the sea's and savanna's trick of
    /// simply defaulting to a suitable dome:
    /// <list type="number">
    /// <item>Picking the darkest dome to get a dark sky <b>also halves the sun</b>. The rig's key tint is
    /// <c>lerp(white, horizon, SKY_TINT_STRENGTH)</c>, so a near-black horizon multiplies the key, fill and
    /// back lights by about a half — the scene comes out grey and flat, which is the exact opposite of the
    /// hard-sun, deep-shadow look space wants.</item>
    /// <item>The drain's gold beads are drawn <b>metallic</b>, so their reflectance is almost entirely the
    /// hemisphere ambient. Take that to near-black and the two rings that mark the drain go black with it.</item>
    /// </list>
    /// So the ambient is stated here — low, cold and deliberately not zero, because the island, the gun and the
    /// glass are not emissive and at zero ambient their shadow sides reach code 0 and the island reads as a
    /// hole cut in the starfield. The balls would survive it; the stone would not.
    /// </summary>
    public sealed class SpaceLightingConfig
    {
        /// <summary>
        /// The hemisphere ambient from above (linear) — integrated starlight and the Milky Way, cold and blue.
        /// About a fifth of a daylight dome's, which is what makes the key-to-fill ratio read as vacuum.
        /// </summary>
        public Rgb SkyAmbient { get; set; } = new(0.052f, 0.060f, 0.098f);

        /// <summary>The bounce from below (linear): the island's own stone, dimmer and more neutral.</summary>
        public Rgb GroundAmbient { get; set; } = new(0.026f, 0.026f, 0.032f);

        /// <summary>
        /// What the key light is tinted by (linear, ~1 per channel). Barely tinted at all and very slightly
        /// blue: there is no atmosphere out here to redden the sun on its way in.
        /// </summary>
        public Rgb KeyTint { get; set; } = new(1.00f, 1.00f, 1.06f);

        /// <summary>What the back/fill light is tinted by (linear, ~1 per channel).</summary>
        public Rgb BackTint { get; set; } = new(0.70f, 0.76f, 1.00f);

        /// <summary>
        /// Planetshine: the light the planet throws back on the island's flank, as a real scene point light so
        /// it also puts a highlight back into the gold drain beads. It is a point light standing very far off
        /// along the planet's own direction, so across a 26-unit island it is near enough directional.
        /// Zero switches it off.
        /// </summary>
        public float PlanetshineStrength { get; set; } = 0.16f;

        /// <summary>How far out the planetshine light stands. Its range is set from this, so the two move
        /// together and the fill stays even across the island.</summary>
        public float PlanetshineDistance { get; set; } = 420f;
    }

    /// <summary>
    /// The starfield. Three layers of a cube-face lattice, one star per cell: a coarse layer of bright stars
    /// (the only one that draws diffraction spikes — its cells are wide enough that a spike cannot run out of
    /// the one cell the shader samples), a middle layer, and a fine layer of the thousands of faint ones a
    /// deep sky is actually made of. Cell scale is cells per unit of cube-face chart, so it is a density: the
    /// count over the whole sphere is roughly <c>6 · (2 · CellScale)² · Chance</c>.
    /// </summary>
    /// <summary>
    /// The volume the island floats <b>inside</b>: a folded kaleidoscopic field marched along the view ray from
    /// the camera's own position, so it has parallax and the rest of this scene does not. Every other layer here
    /// is a function of the view direction alone, which is what made the sky read as a painted dome; this is the
    /// one thing in it with a front and a back. See <c>Space.fx</c>'s <c>StarNestVolume</c> for the field itself
    /// and for what the step and iteration counts cost.
    /// </summary>
    public sealed class SpaceVolumeConfig
    {
        /// <summary>
        /// Overall brightness of the web. <b>Zero switches the march off entirely</b> — a uniform branch skips
        /// it, so nothing is paid for it — and restores the scene to exactly the painted sky it was before.
        /// </summary>
        public float Strength { get; set; } = 1.0f;

        /// <summary>
        /// World units to field units on the way in, and so <b>the parallax dial</b>. The field's cells are 0.85
        /// units across and the march covers about one cell of depth, so this sets how much of a cell the camera
        /// crosses as it moves: at this value the ~66 units of a full menu orbit come out as about a third of a
        /// cell, which is enough that the near web visibly slides against the far one without the structure
        /// changing identity as you watch. Raise it and the field boils as the camera moves; drop it to zero and
        /// the layer is back to being a picture.
        /// </summary>
        public float Scale { get; set; } = 0.0042f;

        /// <summary>
        /// Field units per second the eye drifts through the volume. It is what sells "in space" while the
        /// camera is standing still — with the play camera parked, parallax has nothing to work with. Slow
        /// enough that it is never the thing being watched: at this rate the eye crosses a cell in about four
        /// minutes.
        /// </summary>
        public float Drift { get; set; } = 0.0035f;

        /// <summary>
        /// How much of the field's own colour survives against its luminance. The depth ramp inside the march
        /// (near cool, far warm) is violent, and left raw the web is a rainbow rather than a nebula.
        /// </summary>
        public float Saturation { get; set; } = 0.85f;

        /// <summary>
        /// How hard the web swallows the sky behind it. It is the difference between something the sky is seen
        /// through and a decal laid over it — the same argument the nebulae's own transmittance is there for —
        /// and it is deliberately gentle, because the stars behind are half of what says the web is close.
        /// </summary>
        public float Opacity { get; set; } = 2.6f;

        /// <summary>
        /// Tint over the whole layer (linear), for pulling it towards the palette the rest of the scene is
        /// authored in. Cool and a little violet: the field's own ramp already supplies the warm end.
        /// </summary>
        public Rgb Tint { get; set; } = new(0.78f, 0.72f, 1.05f);
    }

    public sealed class SpaceStarsConfig
    {
        /// <summary>Cells per chart unit of the bright layer. Rounded to a whole number on set: a star can
        /// be held clear of a cell boundary only when the face edge lands ON one, which takes an integer cell
        /// scale — the no-straddle guarantee the whole lattice rests on (#149, docs/scenes.md).</summary>
        private float _brightCellScale = 14f;
        public float BrightCellScale
        {
            get => _brightCellScale;
            set => _brightCellScale = MathF.Round(value);
        }

        /// <summary>Fraction of the bright layer's cells carrying a star.</summary>
        public float BrightChance { get; set; } = 0.30f;

        /// <summary>Peak linear radiance of the bright layer's brightest star.</summary>
        public float BrightPeak { get; set; } = 0.50f;

        /// <summary>Cells per chart unit of the middle layer. Rounded to a whole number on set, for the same
        /// no-straddle reason as <see cref="BrightCellScale"/> (#149).</summary>
        private float _mediumCellScale = 42f;
        public float MediumCellScale
        {
            get => _mediumCellScale;
            set => _mediumCellScale = MathF.Round(value);
        }

        /// <summary>Fraction of the middle layer's cells carrying a star.</summary>
        public float MediumChance { get; set; } = 0.32f;

        /// <summary>Peak linear radiance of the middle layer's brightest star.</summary>
        public float MediumPeak { get; set; } = 0.30f;

        /// <summary>Cells per chart unit of the faint layer. Rounded to a whole number on set, for the same
        /// no-straddle reason as <see cref="BrightCellScale"/> (#149).</summary>
        private float _faintCellScale = 110f;
        public float FaintCellScale
        {
            get => _faintCellScale;
            set => _faintCellScale = MathF.Round(value);
        }

        /// <summary>Fraction of the faint layer's cells carrying a star.</summary>
        public float FaintChance { get; set; } = 0.40f;

        /// <summary>Peak linear radiance of the faint layer's brightest star.</summary>
        public float FaintPeak { get; set; } = 0.16f;

        /// <summary>
        /// Core radius of a star in <b>output</b> pixels — not texels and not radians. The scene is rendered
        /// supersampled and box-filtered on resolve, so a star sized in texels would come out four times
        /// dimmer at 2× than at 1×, i.e. the same sky on two quality settings.
        /// <para>
        /// 0.62 is chosen so that it exactly meets the shader's own anti-aliasing floor at 1× — below that a
        /// star is drawn narrower than the texel it sits in and the field crawls as the camera turns, and in
        /// vacuum a star is the one thing that must <b>not</b> twinkle. Meeting the floor rather than sitting
        /// under it is what makes the field measurably the same at either factor: at 0.45 the floor took over
        /// at 1× and drew the stars 38 % wider there than at 2×.
        /// </para>
        /// </summary>
        public float Spread { get; set; } = 0.62f;

        /// <summary>
        /// Steepness of the brightness law (<c>brightness = hash^Falloff</c>). Real star counts climb steeply
        /// towards the faint end; at 1 the field is a wall of identical dots and reads as noise.
        /// </summary>
        public float Falloff { get; set; } = 3.4f;

        /// <summary>Fraction of the bright layer's peak past which a star also gets diffraction spikes.</summary>
        public float SpikeThreshold { get; set; } = 0.86f;

        /// <summary>How far the spikes reach, in the star's own core radii.</summary>
        public float SpikeLength { get; set; } = 7f;
    }

    /// <summary>
    /// The Milky Way: a band across the sky with a bright bulge at one end and dark dust lanes cutting
    /// across it. The plane is given by its pole and the bulge by the core direction; the core is
    /// orthogonalised against the pole when it is applied, so a hand-typed pair never has to be exactly
    /// perpendicular.
    /// </summary>
    public sealed class SpaceMilkyWayConfig
    {
        /// <summary>Pole of the galactic plane. Tilted, so the band crosses the sky at an angle rather than
        /// lying level with the arena — a level band reads as a seam in the sky.</summary>
        public Vec3 Pole { get; set; } = new(0.36f, 0.79f, 0.50f);

        /// <summary>Towards the galactic bulge. Pointed roughly where the play camera looks, so the brightest
        /// part of the band is behind the hanging cluster rather than at the player's back.</summary>
        public Vec3 CoreDirection { get; set; } = new(0.42f, -0.22f, -0.88f);

        /// <summary>Half-width of the band, as the sine of galactic latitude (0.135 ≈ 30° across).</summary>
        public float Width { get; set; } = 0.135f;

        /// <summary>
        /// Peak linear radiance of the band at the bulge. Low on purpose: a Milky Way is a faint glow, and the
        /// first build at 0.30 tonemapped to a 75 %-grey wash that dominated the frame and buried the stars it
        /// is supposed to be made of.
        /// </summary>
        public float Brightness { get; set; } = 0.115f;

        /// <summary>The cooler outer arms (linear, a tint the brightness multiplies).</summary>
        public Rgb Color { get; set; } = new(0.62f, 0.70f, 0.92f);

        /// <summary>The warmer, denser bulge (linear, a tint the brightness multiplies).</summary>
        public Rgb CoreColor { get; set; } = new(1.00f, 0.86f, 0.64f);

        /// <summary>
        /// How hard the dust lanes cut, 0–1. Never take it to 1: at full strength the band is cut into
        /// detached islands instead of being crossed by rifts.
        /// </summary>
        public float Dust { get; set; } = 0.88f;

        /// <summary>
        /// How much denser the starfield is inside the band (0 = the same everywhere). Carries most of the
        /// band's weight now that the glow itself is faint, which is the right way round: the Milky Way is
        /// stars, and what a long exposure resolves of it is stars.
        /// </summary>
        public float StarBoost { get; set; } = 2.6f;
    }

    /// <summary>
    /// One emission nebula: a region of the sky at a direction, filled with domain-warped noise so it has
    /// filaments and cavities rather than being a soft blob. <see cref="Color"/> is the hue and
    /// <see cref="Strength"/> what its brightest filament peaks at, in linear radiance.
    /// </summary>
    public sealed class SpaceNebulaConfig
    {
        /// <summary>Where on the sky it sits (normalized when applied).</summary>
        public Vec3 Direction { get; set; } = new(0f, 0f, -1f);

        /// <summary>Its hue (linear). Saturated colours are safe here: the glare threshold is measured on
        /// luminance, so a red that peaks at 1.0 still sits well under it.</summary>
        public Rgb Color { get; set; } = new(1.00f, 0.23f, 0.38f);

        /// <summary>How much of the sky it covers, as an angular radius in degrees.</summary>
        public float AngularRadiusDegrees { get; set; } = 24f;

        /// <summary>Peak linear radiance of its brightest filament. Zero removes it.</summary>
        public float Strength { get; set; } = 0.6f;

        /// <summary>Detail frequency of the gas structure (higher = finer filaments).</summary>
        public float DetailScale { get; set; } = 6f;

        /// <summary>
        /// How far the domain warp drags the noise. This is the single biggest quality lever in the whole
        /// scene — at 0 the nebula is a blob of clouds, and at 1.5 it is filaments and cavities.
        /// </summary>
        public float Warp { get; set; } = 1.5f;
    }

    /// <summary>
    /// The field of distant galaxies: small elongated smudges with a bright core and a faint halo, each at
    /// its own size, elongation and angle, on one coarse cube lattice.
    /// </summary>
    public sealed class SpaceGalaxyConfig
    {
        /// <summary>Cells per chart unit. Coarse — galaxies are rare compared with stars. Rounded to a whole
        /// number on set for the same no-straddle reason as the stars' own cell scales (#149).</summary>
        private float _cellScale = 9f;
        public float CellScale
        {
            get => _cellScale;
            set => _cellScale = MathF.Round(value);
        }

        /// <summary>Fraction of cells carrying a galaxy.</summary>
        public float Chance { get; set; } = 0.16f;

        /// <summary>Base angular radius in degrees; each galaxy scales it by its own random factor.</summary>
        public float AngularSizeDegrees { get; set; } = 0.12f;

        /// <summary>Peak linear radiance of a galaxy's core.</summary>
        public float Brightness { get; set; } = 0.20f;

        /// <summary>The warm white of the larger ellipticals (linear). The small ones are shaded towards
        /// blue in the shader, so a field of them does not read as one texture.</summary>
        public Rgb Color { get; set; } = new(1.00f, 0.93f, 0.82f);
    }

    /// <summary>
    /// The planet: a banded gas giant, solved analytically in the sky pass rather than drawn as geometry,
    /// and lit by the very sun the island and the balls are lit by — which is what ties it to the frame
    /// instead of leaving it a sticker on the sky.
    /// </summary>
    public sealed class SpacePlanetConfig
    {
        /// <summary>
        /// Where it hangs (normalized when applied). Up and to one side of where the play camera looks, so it
        /// frames the hanging cluster rather than sitting behind it: the play camera rests looking down −Z and
        /// its frame spans roughly 0°–44° of elevation, so this is 32° of azimuth off that bearing at 24° up.
        /// <para>
        /// The other thing this direction decides is the <b>phase</b>, and it is worth re-checking after any
        /// move: the lit fraction of the disc is <c>(1 − dot(Direction, SunDirection)) / 2</c>, and the dot
        /// wants to land in −0.4…−0.7 (here −0.51, a fat gibbous). A full disc is flat and shows nothing of
        /// the terminator; a thin crescent hides the bands the planet is there for.
        /// </para>
        /// </summary>
        public Vec3 Direction { get; set; } = new(-0.484f, 0.407f, -0.775f);

        /// <summary>
        /// How big it is, as an angular radius in degrees. The play camera's vertical field of view is about
        /// 43°, so 10° is a disc about half the frame high — dominant, which is the point of it, without
        /// crowding the cluster it stands behind. Zero removes the planet altogether.
        /// </summary>
        public float AngularRadiusDegrees { get; set; } = 10f;

        /// <summary>Its pole: the axis the cloud bands run around.</summary>
        public Vec3 Axis { get; set; } = new(0.16f, 0.96f, 0.22f);

        /// <summary>
        /// The pale zones (linear radiance — this is what a fully lit band actually emits). Its luminance is
        /// 0.437, deliberately under the 0.55 glare threshold: the disc's own body must not bloom, or the
        /// planet stops being an object and becomes a lamp. Only the limb (see <see cref="RimStrength"/>) is
        /// allowed over.
        /// </summary>
        public Rgb ColorLight { get; set; } = new(0.50f, 0.43f, 0.32f);

        /// <summary>The dark belts (linear radiance).</summary>
        public Rgb ColorDark { get; set; } = new(0.26f, 0.19f, 0.13f);

        /// <summary>The one big storm oval (linear radiance).</summary>
        public Rgb StormColor { get; set; } = new(0.58f, 0.25f, 0.15f);

        /// <summary>The limb's atmosphere, seen edge-on at the rim and standing off it as a halo (linear).</summary>
        public Rgb RimColor { get; set; } = new(0.52f, 0.64f, 0.92f);

        /// <summary>How many band pairs run from pole to pole.</summary>
        public float BandScale { get; set; } = 7f;

        /// <summary>
        /// How strongly the limb and its halo glow. This is the one dial in the whole scene aimed deliberately
        /// <b>over</b> the glare threshold, and only in the outermost ~2 % of the disc's radius: a limb arc is
        /// long and smooth, so the glare's sparse sampling lands on it many times and it blooms steadily,
        /// where an isolated bright point would pop in and out. Raise it and the bloom widens; there is no
        /// stability cliff here, only taste.
        /// </summary>
        public float RimStrength { get; set; } = 0.30f;

        /// <summary>
        /// What the unlit side keeps. Not zero: a night side at pure black is a hole punched in the
        /// starfield rather than the dark half of a sphere.
        /// </summary>
        public float NightAmbient { get; set; } = 0.045f;
    }
}
