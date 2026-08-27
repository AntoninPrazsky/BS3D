using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the storm backdrop (#219): the arena hangs in clear high air over an unbroken deck
    /// of storm cloud, with convective turrets towering out of it and lightning flashing inside them.
    /// <para>
    /// <b>It is an <see cref="SceneRenderer.IsSolidTerrainScene"/>, and that contradicts the issue's own fix
    /// sketch on purpose.</b> #219 reasoned "not solid terrain — the deck is weather, not ground", which is
    /// true of what the deck <i>is</i> and false of everything the flag actually decides. Mechanically the
    /// deck <b>is</b> this world's floor: it is drawn as the same camera-centred displaced grid every terrain
    /// sibling uses, it sits at the same level theirs do (see <see cref="StormDeckConfig.LevelY"/>), the
    /// island's footprint has to be cut out of it exactly as it is cut out of sand, and the drain's
    /// ~55 %-opaque glass needs the dark pit shaft behind it or it reads as a glass ring lying on cloud —
    /// which is the very failure the shaft exists for. Declining the flag would also make
    /// <see cref="SceneRenderer.OpenBelow"/> true and hand the drop cinematic a dive to about y = −66, i.e.
    /// <i>under</i> the deck, filming the cluster through cloud with none of the three mitigations the sea
    /// needed to survive that shot (they are all gated to <see cref="SceneKind.Sea"/>). The flag's own doc
    /// says to split the two families only if a scene wants the shaft <i>and</i> the dive; this one wants
    /// the shaft and not the dive, which is what the flag already gives.
    /// </para>
    /// <para>
    /// It is <b>not</b> <see cref="SceneRenderer.ReplacesSky"/>: the air above the deck is the point, so the
    /// dome stays up and lights the scene as it lights every terrain sibling.
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
        /// whose whole subject is a storm below the arena would draw Earth cumulus above it at the same
        /// time, and the player would be sandwiched between two unrelated weathers. Mars overrides this for
        /// the same class of reason. The air above this deck is high and clean, which is what makes the deck
        /// read as something the arena is *above*.
        /// </summary>
        public StormSceneConfig() => Weather = WeatherPreset.Clear;

        /// <summary>The cloud deck and the turrets standing out of it.</summary>
        public StormDeckConfig Deck { get; set; } = new();

        /// <summary>What the cloud is made of.</summary>
        public StormSurfaceConfig Surface { get; set; } = new();

        /// <summary>The lightning.</summary>
        public StormFlashConfig Flash { get; set; } = new();

        /// <summary>The air above the deck.</summary>
        public StormAirConfig Air { get; set; } = new();
    }

    /// <summary>
    /// The deck: a flat floor of cloud at the island's foot, rising into billow with distance, with
    /// convective turrets towering out of it.
    /// <para>
    /// <b>⚠ The turrets are not decoration — they are the only part of this scene the play camera can
    /// see, and the geometry says so rather than taste.</b> The gameplay lens is pinned at
    /// <c>GameCameraFit.LENS_FLOOR_Y</c> = −7.9, a bare 0.6 units over the island's own deck plane
    /// (<c>ArenaIsland.TOP_Y</c> = −8.5), and the island's disc is wider than the frame — so the stone
    /// occludes everything below about 0.6° of depression, and a flat surface at height C is first visible
    /// roughly 96 × (−7.9 − C) units out. At this deck's own level that is past the 500-unit far plane.
    /// Measured rather than derived: the <b>sea</b>, an entire ocean 4.5 units below the arris, is
    /// <b>8 pixels of a 939-pixel frame</b> from the real play camera — 0.85 % of its height. So a deck
    /// authored at a physically honest altitude below the arena is invisible in play while looking superb
    /// in the map editor and in every free-camera screenshot, which is exactly the defect the Moon shipped
    /// ("a black sky over bare stone"). What clears the deck plane is relief standing <b>above the lens</b>,
    /// the Moon's 40-unit highland belt and the desert's 14-unit dunes being the two fixes on record.
    /// <see cref="TurretHeight"/> is that dial here, and <see cref="LightningFlashesInside"/> is the second
    /// channel — light reaches the play frame even when the geometry throwing it does not.
    /// </para>
    /// </summary>
    public sealed class StormDeckConfig
    {
        /// <summary>
        /// Mean cloud-top level in the clearing (world Y) — the island's foot, the same level every other
        /// scene's surface sits at (they run −13 to −15.5; only the sky-replacing cavern goes deeper).
        /// <b>Raising it does not make the deck more visible and lowering it makes it invisible</b>: see the
        /// class note. The deck is at the ground's level because that is where a surface can be seen at all.
        /// </summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>Radius of the flat clearing the island stands in, before the deck starts to billow.</summary>
        public float ClearingRadius { get; set; } = 52f;

        /// <summary>Transition band over which the flat clearing rises into billowing cloud.</summary>
        public float ClearingTransition { get; set; } = 55f;

        /// <summary>
        /// Peak-to-trough height of the deck's own billow — the lumpy cloud-top carpet between the turrets.
        /// <para>
        /// <b>⚠ 11 photographed as a snowfield too, and the owner rejected the scene on it.</b> The first
        /// build ran 7 and was called one; 11 was not enough of a change, because at these feature sizes the
        /// deck is being read at a grazing angle from five units above it and eleven units of swell over a
        /// 133-unit lobe is a slope of under five degrees. The field itself is now a folded four-octave
        /// billow rather than two octaves of plain noise (see <c>DeckBillow</c> in <c>Storm.fx</c>), and it
        /// is ramped out of the clearing, so this is free to be the real relief a cloud carpet has: half of
        /// it up and half down about <see cref="LevelY"/>. The note it replaces — that too tall a billow
        /// only fights the turrets for the horizon — is still true and is what stops this going further.
        /// </para>
        /// </summary>
        public float BillowHeight { get; set; } = 36f;

        /// <summary>How far apart the turrets' lattice cells sit. The outback's own single-cell lattice
        /// (<c>RockLayer</c>, ported into <c>Storm.fx</c>), so a turret is held inside its own cell by its
        /// own reach and only the pixel's own cell is ever read.</summary>
        public float TurretSpacing { get; set; } = 170f;

        /// <summary>Fraction of lattice cells that carry a turret. The empty ones are what break the grid —
        /// a storm with a tower in every cell is a lattice, however hard the jitter works.</summary>
        public float TurretChance { get; set; } = 0.72f;

        /// <summary>
        /// Height of the tallest turret above <see cref="LevelY"/>, in world units. <b>This is the figure
        /// the scene lives or dies on</b> (see the class note): the play lens sits 5.6 units above this
        /// deck, so anything under that never breaks the island's occluding line, and a turret has to crest
        /// well clear of it to read as a silhouette rather than as a bump. At 60 a turret's own profile
        /// (<c>body · 0.94 + anvil</c>, peaking at 1.10) times its rolled size (0.62–1.0) puts the tallest
        /// crest about 66 units over <see cref="LevelY"/>, i.e. <b>y ≈ +52</b>, and the median one at
        /// y ≈ +40 — so they clear the lens by 48 to 60 units, which is the order of the Moon's own 40-unit
        /// highland belt at 4.5° over it.
        /// </summary>
        public float TurretHeight { get; set; } = 60f;

        /// <summary>How much of the flat clearing's own level the anvil tops spread over, as a fraction of a
        /// turret's radius. A mature storm cell flattens where it hits the tropopause and spreads downwind;
        /// at 1 the turrets are plain domes and read as hills rather than as weather.</summary>
        public float AnvilSpread { get; set; } = 1.45f;

        /// <summary>Whether the lightning glow is drawn inside the turrets as well as thrown as light. Off,
        /// the flash is a lighting event with no visible source, which reads as the sun blinking.</summary>
        public bool LightningFlashesInside { get; set; } = true;
    }

    /// <summary>
    /// What the cloud is made of. Storm cloud is not white: its top is blinding where the sun rakes it and
    /// its shaded flanks and the deck between the towers run to a deep blue-grey, and that spread is most of
    /// what says "storm" rather than "cotton wool".
    /// </summary>
    public sealed class StormSurfaceConfig
    {
        /// <summary>
        /// The sunlit cloud top (linear). Bright and only just under the glare threshold's own luminance:
        /// a sunlit cumulonimbus top is the brightest thing in a real sky, but a deck that blooms wholesale
        /// takes the cluster's silhouette with it.
        /// </summary>
        public Rgb TopColor { get; set; } = new(0.62f, 0.645f, 0.70f);

        /// <summary>
        /// The shaded flank and the deck's own hollows (linear). <b>Well</b> under the top rather than a
        /// shade under: ACES eats cloud contrast — two linear values close together in the highlights
        /// tonemap to the same white — which is the documented reason the sky's own deck runs its shaded
        /// underside at 0.18 against a 1.7 top.
        /// </summary>
        public Rgb ShadeColor { get; set; } = new(0.175f, 0.205f, 0.275f);

        /// <summary>The deep blue-grey the deck's own base carries where no sun reaches it at all (linear) —
        /// the colour a storm's underside is, seen from above through a gap.</summary>
        public Rgb BaseColor { get; set; } = new(0.030f, 0.036f, 0.055f);

        /// <summary>How hard the cloud's own relief breaks up its shading (world units of relief). Cloud has
        /// no hard edges, so this is the whole of what gives a turret its billowed surface.</summary>
        public float BillowRelief { get; set; } = 1.0f;

        /// <summary>
        /// The silver lining: how strongly a grazing angle brightens the cloud's rim towards
        /// <see cref="TopColor"/>. Cloud is forward-scattering, so its edge against the sun is its brightest
        /// part — the one cue that separates a cloud from a hill of grey stone.
        /// </summary>
        public float SilverStrength { get; set; } = 0.75f;

        /// <summary>How much of the sky's hemisphere light fills the deck.</summary>
        public float AmbientStrength { get; set; } = 0.62f;
    }

    /// <summary>
    /// The lightning. A flash is an <b>event</b>, and this scene is the first whose whole subject is one —
    /// so the schedule is a pure function of the wall clock with no state at all, exactly as
    /// <c>SceneRenderer.VolcanoEruption</c> is and for the same reason: the Game, the Testbed and the map
    /// editor then all see the same strike at the same second, and nothing has to be saved or synchronised.
    /// <para>
    /// Seen from above a deck, most lightning is a <b>glow inside the cloud</b> rather than a bolt — truer
    /// and cheaper both. Visible bolt geometry is explicitly unclaimed by #219 and is not built.
    /// </para>
    /// </summary>
    public sealed class StormFlashConfig
    {
        /// <summary>Mean seconds between strikes. Each strike's exact moment is jittered inside its own
        /// period off the period's index, so the storm never becomes a metronome.</summary>
        public float Period { get; set; } = 7.5f;

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
        /// Earth, a 1.6° sun) — but the window here is narrower than it looks and it was found by walking
        /// both walls: at <b>2.6</b> a strike moved the deck's mean luminance by 3 of 255 and was invisible;
        /// at <b>14</b>, added flat, it washed the whole near deck to featureless white and took the cloud's
        /// form with it. What made 7 work is that the term is weighted towards the <i>shaded</i> side in the
        /// shader (a discharge lights cloud from within, so it shows where the sun is not already), which
        /// spends the same energy where there is contrast to spend it on.
        /// <para>
        /// <b>Verified deterministically rather than by catching a strike</b>, because nothing in
        /// <c>TestOptions</c> pins the wall clock and a 0.45 s strike is shorter than the capture harness's
        /// own timing slop: the period was temporarily collapsed so a strike is always running, and the
        /// scene shot with this at 7 and at 0. Deck mean luminance <b>189.4 against 155.5</b> — a 33.9
        /// difference of 255, independent of when the frame landed.
        /// </para>
        /// </summary>
        public float DeckGlow { get; set; } = 7f;

        /// <summary>
        /// How far a strike's glow carries across the deck, in world units, falling off quadratically from
        /// the cell that went off.
        /// <para>
        /// <b>⚠ Its own dial, and the first build's bug is why.</b> It was derived as
        /// <c>TurretSpacing × 0.8</c> = 136 units, which sounded like "its own cell and a little beyond" and
        /// was in fact a patch <i>smaller than the distance to the strike</i> — a strike stands 173 to 348
        /// units out (see the flash's own placement), so the glow landed almost entirely off frame and the
        /// flash read as simply not working. Diagnosed by deleting the falloff outright: the identical build
        /// then drove the deck's mean luminance from 189 to 255, which is what said the plumbing was sound
        /// and only the radius was wrong. A strike has to light a good sweep of deck around itself, because
        /// that is what a cell going off actually does to the cloud beside it.
        /// </para>
        /// </summary>
        public float GlowReach { get; set; } = 430f;

        /// <summary>
        /// <b>What the flash is allowed to contribute to everything the shared instanced effect lights —
        /// the balls above all. This is the readability dial and it is the one number here that is not a
        /// matter of taste.</b> Thirteen ball colours hang in front of this deck, and
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
        /// precisely "the deck below flashed" — the undersides of the balls, the island's coping and the
        /// gun's carriage catch it and their tops do not.
        /// </summary>
        public float LightDistance { get; set; } = 150f;
    }

    /// <summary>The air above the deck: high, thin and clean, which is the whole reason the scene reads as
    /// being <i>above</i> something rather than in it.</summary>
    public sealed class StormAirConfig
    {
        /// <summary>
        /// What the distance haze is made of (linear), lit by the sky where it is applied.
        /// <b>Not optional, and the first build proved it.</b> Written as a plain two-stage fade to the
        /// dome's own <c>HorizonColor</c>, the deck came out beige under dome 11's sandy horizon and the
        /// whole scene photographed as desert dunes — the outback's own recorded trap, that aerial
        /// perspective can behave correctly and still give the wrong picture. A cool cloud-white keeps the
        /// deck cloud-coloured under any of the nineteen domes.
        /// </summary>
        public Rgb HazeTint { get; set; } = new(0.72f, 0.78f, 0.92f);

        /// <summary>
        /// World distance over which the deck melts into the skyline. Must stay inside the terrain grid's
        /// own half-extent or the mesh's edge shows as a seam against the dome — the trap eleven scene
        /// shaders already close this way.
        /// </summary>
        public float HorizonHazeDistance { get; set; } = 620f;

        /// <summary>
        /// How much of the haze is applied at its fullest. High air is clean, so this is gentler than any
        /// dusty scene's — but it has to reach the dome's own horizon colour by the grid's edge, which is
        /// what the fade's last stage does.
        /// </summary>
        public float HazeStrength { get; set; } = 0.62f;

        /// <summary>The wind, a direction in the XZ plane: it drifts the deck and leans the anvil tops.
        /// Kept unit-length like every other scene's, since <see cref="DriftSpeed"/> carries the magnitude —
        /// a longer vector here would silently mean a faster deck.</summary>
        public Vec2 Wind { get; set; } = new(0.86f, 0.51f);

        /// <summary>How fast the deck drifts downwind, in world units per second. Slow — a storm deck seen
        /// from above moves, but it is a hundred-kilometre object and should not scud.</summary>
        public float DriftSpeed { get; set; } = 1.6f;
    }
}
