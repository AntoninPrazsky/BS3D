using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the storm backdrop (#219): the arena hangs in open air among broken cumulus, with
    /// lightning breaking through the space between the cells.
    /// <para>
    /// <b>⚠ There is no ground in this scene, and that is the correction the first build needed.</b> It was
    /// drawn as a displaced grid — the same camera-centred terrain mesh every ground scene uses — with
    /// convective turrets standing out of it, and it read as landscape from every camera the game has: flat
    /// tops and steep walls photographed as mesas, and once those were rounded it photographed as snow. The
    /// fault was never the shape. A height field is a <b>surface</b>: one height per XZ, so it can be lumpy
    /// but it can never be <i>broken</i>, and torn cumulus with sky showing between the cells is not
    /// expressible in it at all. Nor can its silhouette be anything but a hard geometric edge, where a
    /// cloud's dissolves. See <c>StormClouds.fx</c>'s header for the whole argument and what replaced it.
    /// </para>
    /// <para>
    /// It is therefore <b>not</b> <see cref="SceneRenderer.IsSolidTerrainScene"/> — there is nothing here to
    /// cut the island's footprint out of and nothing to back the drain's glass with, so the drain looks
    /// straight through onto sky and cloud exactly as it does over the space scene. It is also <b>not</b>
    /// <see cref="SceneRenderer.ReplacesSky"/>: the open air above the cells is the point, so the dome stays
    /// up and lights the scene as it lights every sibling.
    /// </para>
    /// <para>
    /// Every colour is <b>linear radiance</b>, like every other scene config's.
    /// </para>
    /// </summary>
    public sealed class StormSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Storm;

        /// <summary>
        /// <b>Clear, and it is the one setting here that would be actively wrong left alone.</b> The base
        /// class defaults to <see cref="WeatherPreset.Scattered"/> because that reproduces the pre-#221 look
        /// byte for byte — but the shared cloud deck lives <i>overhead</i> at a plane of 140–215, so a scene
        /// whose whole subject is cloud around the arena would draw a second, unrelated layer of Earth
        /// cumulus over the top of it. Mars overrides this for the same class of reason.
        /// </summary>
        public StormSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>The cloud masses themselves — what this scene is made of.</summary>
        public StormCloudsConfig Clouds { get; set; } = new();

        /// <summary>What the cloud is made of, as a material.</summary>
        public StormSurfaceConfig Surface { get; set; } = new();

        /// <summary>The lightning.</summary>
        public StormFlashConfig Flash { get; set; } = new();

        /// <summary>The air the cells stand in.</summary>
        public StormAirConfig Air { get; set; } = new();
    }

    /// <summary>
    /// The cloud field: discrete cumulus cells scattered through a volume around and below the arena, each
    /// built from soft billboard puffs.
    /// <para>
    /// <b>⚠ The gaps are the subject.</b> What makes this scene read as cloud rather than as land is that
    /// the sky shows <i>between</i> the cells and <i>through</i> them — so the two dials that matter most
    /// are <see cref="MassCount"/> against <see cref="OuterRadius"/> (how much of the volume is filled) and
    /// <see cref="PuffOpacity"/> (how much of it can be seen through). Filling it evenly is what turns a
    /// cloudscape back into a ceiling.
    /// </para>
    /// <para>
    /// <b>The field is generated once at load and is static in the world</b>, drifting bodily downwind in
    /// the vertex shader. It is generated well past the far plane on purpose: a wrap-around would tear a
    /// mass in half, which is the one artefact a cloud cannot survive.
    /// </para>
    /// </summary>
    public sealed class StormCloudsConfig
    {
        /// <summary>
        /// How many cumulus cells stand in the field. The puff budget is the real ceiling: the buffer is
        /// 16-bit indexed, so cells × <see cref="PuffsPerMass"/> must stay under about 16 000 quads.
        /// </summary>
        public int MassCount { get; set; } = 150;

        /// <summary>How many billboard puffs build one cell. Under about twenty a cell reads as a clump of
        /// balls; over about eighty the extra ones are hidden inside the ones in front.</summary>
        public int PuffsPerMass { get; set; } = 78;

        /// <summary>
        /// How close to the arena a cell may stand. <b>Not decoration: it is what keeps a cloud from being
        /// drawn in front of the island.</b> The field is alpha-blended and depth-read but written before
        /// the arena, so a puff nearer the camera than the stone would be overpainted by it — the same
        /// limitation the sea's spray carries. Keeping every cell outside the play camera's own stand-off
        /// makes the case unreachable in play.
        /// </summary>
        public float InnerRadius { get; set; } = 105f;

        /// <summary>How far out the field is generated. Past the far plane deliberately, so the field can
        /// drift for a whole session without its edge ever entering the frame.</summary>
        public float OuterRadius { get; set; } = 780f;

        /// <summary>Lowest and highest a cell's BASE may sit (world Y). The island's own deck is at −8.5, so
        /// a field sitting under that is one the arena looks down on — which is the scene.</summary>
        public float BaseYMin { get; set; } = -64f;

        /// <inheritdoc cref="BaseYMin"/>
        public float BaseYMax { get; set; } = -20f;

        /// <summary>Smallest and largest a cell's own radius may be, in world units.</summary>
        public float MassRadiusMin { get; set; } = 24f;

        /// <inheritdoc cref="MassRadiusMin"/>
        public float MassRadiusMax { get; set; } = 62f;

        /// <summary>
        /// How tall a cell stands against its own radius, as a range. <b>Over one is what makes it cumulus
        /// rather than stratus</b>: a cell with vertical development is a tower with a cauliflower crown,
        /// and it is the towers that carry the lightning. Some cells reach past the arena's own level from
        /// bases well below it, which is what puts cloud in the play frame at all.
        /// </summary>
        public float HeightScaleMin { get; set; } = 0.85f;

        /// <inheritdoc cref="HeightScaleMin"/>
        public float HeightScaleMax { get; set; } = 2.30f;

        /// <summary>A single puff's radius as a fraction of its cell's. Small enough that a cell is built of
        /// visibly separate lobes and large enough that it has no holes punched through it.</summary>
        public float PuffScaleMin { get; set; } = 0.22f;

        /// <inheritdoc cref="PuffScaleMin"/>
        public float PuffScaleMax { get; set; } = 0.40f;

        /// <summary>
        /// How opaque one puff is at its middle. <b>The single most important dial in this file.</b> Near 1
        /// the field becomes a solid white wall — a ceiling, which is the failure the height field had. Low
        /// enough and the cells are veils with sky visible through their thin parts, which is what a real
        /// cumulus edge does and what makes the whole thing read as vapour.
        /// </summary>
        public float PuffOpacity { get; set; } = 0.30f;

        /// <summary>
        /// Where a puff's own falloff starts, as a fraction of its disc — so <b>low is soft and high is
        /// hard</b>, which is the opposite way round from how the name reads and is worth saying here
        /// because it was written down backwards once. At 0 the puff fades from its very middle and is all
        /// fringe; at 0.9 it is solid out to nine tenths of its radius and then stops, which is a disc with
        /// a rim on it — and a field of those is the bubble wrap the first cloud build photographed as.
        /// </summary>
        public float EdgeSoftness { get; set; } = 0.05f;

        /// <summary>
        /// How much a puff is shaded as part of its CELL rather than as its own little sphere.
        /// <para>
        /// <b>⚠ At 0 the field reads as bubble wrap and that is not a figure of speech</b> — it is the first
        /// thing the eye names. A billboard puff shaded from its own disc gets a complete light-to-dark
        /// gradient across it and a crisp circular edge, so a cell built of them is a heap of glossy balls.
        /// Blending that normal towards the one measured from the cell's own middle makes the cell shade as
        /// a body, which is what it is, and lets the individual puffs disappear into it. At 1 the puffs lose
        /// their own form entirely and a cell flattens into a blob.
        /// </para>
        /// </summary>
        public float MassNormalMix { get; set; } = 0.70f;

        /// <summary>
        /// How dark the bottom of the field is against its top. A storm is dark underneath because the cloud
        /// above is in the way, and that is a property of the whole field rather than of any one cell — so
        /// it is taken from a puff's height in the layer, not from its normal.
        /// </summary>
        public float UnderShade { get; set; } = 0.30f;

        /// <summary>The vertical span the shading gradient above is measured over. Wider than the cells
        /// themselves, so the darkest cloud is genuinely at the bottom of the sky rather than at the bottom
        /// of its own cell.</summary>
        public float LayerBottomY { get; set; } = -78f;

        /// <inheritdoc cref="LayerBottomY"/>
        public float LayerTopY { get; set; } = 16f;
    }

    /// <summary>What the cloud is made of. Every colour linear, and every one of them a <i>reflectance</i>
    /// the light rig then lights — a cloud has no colour of its own.</summary>
    public sealed class StormSurfaceConfig
    {
        /// <summary>
        /// The sunward crown of a puff (linear). Deliberately under 1: cloud is a bright diffuse surface,
        /// not a light, and the sun's own radiance is what takes it to white.
        /// </summary>
        public Rgb TopColor { get; set; } = new(0.58f, 0.60f, 0.66f);

        /// <summary>The deep blue-grey a puff's underside carries where no sun reaches it (linear) — the
        /// colour a storm cell is seen from below.</summary>
        public Rgb BaseColor { get; set; } = new(0.045f, 0.053f, 0.078f);

        /// <summary>
        /// How strongly a rim with the sun behind it silvers. Cloud is strongly forward-scattering, so its
        /// lit edge is the brightest thing in the sky — the single cue that separates a cloud from a hill of
        /// grey stone, and the sky's own shared deck carries the same term for the same reason.
        /// </summary>
        public float SilverStrength { get; set; } = 1.05f;

        /// <summary>How much of the sky's hemisphere light fills the cloud. High: cloud is a near-white
        /// diffuser with heavy multiple scattering, so its shaded side is sky-lit rather than black — and a
        /// shaded side that goes black is exactly what reads as rock.</summary>
        public float AmbientStrength { get; set; } = 0.58f;
    }

    /// <summary>
    /// The lightning. A flash is an <b>event</b>, and this scene is the first whose whole subject is one —
    /// so the schedule is a pure function of the wall clock with no state at all, exactly as
    /// <c>SceneRenderer.VolcanoEruption</c> is and for the same reason: the Game, the Testbed and the map
    /// editor then all see the same strike at the same second, and nothing has to be saved or synchronised.
    /// </summary>
    public sealed class StormFlashConfig
    {
        /// <summary>Mean seconds between strikes. Each strike's exact moment is jittered inside its own
        /// period off the period's index, so the storm never becomes a metronome.</summary>
        public float Period { get; set; } = 6f;

        /// <summary>
        /// How long one strike's envelope lasts, in seconds. Short — a flash is a flash — but not so short
        /// that a 60 Hz frame can fall between two of them: at 0.45 s every strike is a couple of dozen
        /// frames, and the flicker below subdivides it.
        /// </summary>
        public float Length { get; set; } = 0.45f;

        /// <summary>
        /// How many times a strike flickers within its envelope. Real lightning is a train of return
        /// strokes, and the flicker is most of what says "lightning" rather than "a light being switched
        /// on". At 0 the flash is one clean pulse, which reads as a camera flash.
        /// </summary>
        public float Flicker { get; set; } = 3f;

        /// <summary>The flash's own colour (linear, over 1 — it is a spark). Cool blue-white: a lightning
        /// channel runs far hotter than the sun, and its light on cloud is noticeably bluer than daylight.</summary>
        public Rgb Color { get; set; } = new(0.72f, 0.82f, 1.00f);

        /// <summary>
        /// How brightly the strike lights the cloud it is inside. The glare pass blooming a full-frame
        /// source is safe — the documented stochastic-flicker trap is a <i>small</i>-disc trap (a 30-pixel
        /// Earth, a 1.6° sun) — but the window is narrower than it looks and it was found by walking both
        /// walls: at <b>2.6</b> a strike moved the mean luminance by 3 of 255 and was invisible; at
        /// <b>14</b>, added flat, it washed the near cloud to featureless white and took its form with it.
        /// What made 7 work is that the term is weighted towards the <i>shaded</i> side in the shader (a
        /// discharge lights cloud from within, so it shows where the sun is not already), which spends the
        /// same energy where there is contrast to spend it on.
        /// </summary>
        public float CloudGlow { get; set; } = 7f;

        /// <summary>
        /// How far a strike's glow carries through the field, in world units, falling off quadratically
        /// from the cell that went off.
        /// <para>
        /// <b>⚠ Its own dial, and the first build's bug is why.</b> It was derived from the turret lattice's
        /// spacing as 136 units, which sounded like "its own cell and a little beyond" and was in fact a
        /// patch <i>smaller than the distance to the strike</i>, so the glow landed almost entirely off
        /// frame and the flash read as simply not working. A strike has to light a good sweep of cloud
        /// around itself, because that is what a discharge actually does to the cell beside it.
        /// </para>
        /// </summary>
        public float GlowReach { get; set; } = 430f;

        /// <summary>
        /// <b>What the flash is allowed to contribute to everything the shared instanced effect lights —
        /// the balls above all. This is the readability dial and it is the one number here that is not a
        /// matter of taste.</b> Thirteen ball colours hang in front of this sky, and
        /// <c>BallRenderSet.EMISSION</c> is already deliberately over the glare threshold, so a flash that
        /// lifts them further clips them into their own light and the reds stop being reds. Set the way the
        /// volcano's <c>LightStrength</c> (0.22) was: against the savanna's campfire, the only other lamp of
        /// this kind, which is (2.4, 1.0, 0.32) at range 32. Zero switches the arena's response off entirely
        /// and leaves the flash a sky effect.
        /// </summary>
        public float LightStrength { get; set; } = 0.30f;

        /// <summary>
        /// How far below the island the flash's lamp stands, along −Y. A lamp far along −Y gives
        /// <c>dot(N, L)</c> ≈ 1 on every downward-facing normal and ≈ 0 on every upward one, which is
        /// precisely "the cloud below flashed" — the undersides of the balls, the island's coping and the
        /// gun's carriage catch it and their tops do not.
        /// </summary>
        public float LightDistance { get; set; } = 150f;

        /// <summary>
        /// The visible discharge itself: how many forked channels one strike draws. <b>This is what #219
        /// asked for in as many words</b> — the light of a flash with no source in the frame reads as the
        /// sun blinking, which is the note the first build left standing. Zero leaves the glow alone.
        /// </summary>
        public int BoltCount { get; set; } = 3;

        /// <summary>How wide a bolt's own channel is drawn, in world units. Thin: the channel is a
        /// filament and everything that makes it read as bright is the glare pass blooming it.</summary>
        public float BoltWidth { get; set; } = 4.0f;

        /// <summary>The channel's own radiance (linear, far over 1 — it is a spark, and it is meant to
        /// bloom). Whiter than <see cref="Color"/>: the channel is the source and the blue is what the
        /// cloud does to its light.</summary>
        public Rgb BoltColor { get; set; } = new(9.0f, 9.6f, 12.0f);
    }

    /// <summary>The air the cells stand in: high, thin and clean, which is the whole reason the scene reads
    /// as being <i>among</i> cloud rather than under it.</summary>
    public sealed class StormAirConfig
    {
        /// <summary>
        /// What the distance haze is made of (linear), lit by the sky where it is applied.
        /// <b>Not optional, and the first build proved it.</b> Written as a plain fade to the dome's own
        /// <c>HorizonColor</c>, the cloud came out beige under dome 11's sandy horizon and the whole scene
        /// photographed as desert dunes — the outback's own recorded trap, that aerial perspective can
        /// behave correctly and still give the wrong picture. A cool cloud-white keeps the field
        /// cloud-coloured under any of the twenty domes.
        /// </summary>
        public Rgb HazeTint { get; set; } = new(0.72f, 0.78f, 0.92f);

        /// <summary>World distance over which a cell melts into the skyline.</summary>
        public float HorizonHazeDistance { get; set; } = 620f;

        /// <summary>How much of the haze is applied at its fullest. High air is clean, so this is gentler
        /// than any dusty scene's.</summary>
        public float HazeStrength { get; set; } = 0.62f;

        /// <summary>The wind, a direction in the XZ plane. Kept unit-length like every other scene's, since
        /// <see cref="DriftSpeed"/> carries the magnitude — a longer vector here would silently mean a
        /// faster sky.</summary>
        public Vec2 Wind { get; set; } = new(0.86f, 0.51f);

        /// <summary>How fast the field drifts downwind, in world units per second. Slow — a storm seen from
        /// inside it moves, but its cells are kilometre-scale objects and should not scud.</summary>
        public float DriftSpeed { get; set; } = 1.6f;
    }
}
