using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the volcano backdrop (#223): the flank of an erupting cone — black basalt and heaped
    /// scoria cut by gullies, rivers of red-orange lava running down them and past the arena, lava fountains
    /// spurting from the crater and its side vents, and drifting ash over all of it.
    /// <para>
    /// It is the first scene whose <b>ground is the light</b>. Every other backdrop takes its light from the
    /// sky and the strongest lamp in the game is the savanna's campfire; here the rivers and the vents are
    /// real <see cref="SceneLights"/> point lights throwing red-orange up onto the underside of the cluster,
    /// while the dome keeps lighting its tops. That is also the whole readability constraint — red and orange
    /// balls over red-orange lava — so <see cref="LightStrength"/> caps what the ground may contribute and
    /// the rivers are kept narrow rather than made bright.
    /// </para>
    /// </summary>
    public sealed class VolcanoSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Volcano;

        /// <summary>
        /// The one scene whose own sky should be ugly: ash and heat over black basalt want a heavy low deck with the light nearly out of it, and it is the only backdrop in the set that gains rather than loses by going dark.
        /// </summary>
        public VolcanoSceneConfig()
        {
            Weather = WeatherPreset.Storm;

            //The sun's cast shadows (#471), at the savanna's own figures — 0.9 is a shadow that is dark without
            //reading as a hole, and 260 units at 2048 is 0.13 units a texel. The island and the gun on the
            //flank. The lava's own light is not a shadow caster — the map is the sun's, and this scene's sun is
            //what the storm deck leaves of it.
            Shadows = new ShadowConfig(strength: 0.9f);
        }

        /// <summary>Mean ground level in the clearing (the island's foot), as in the desert and the outback.</summary>
        public float LevelY { get; set; } = -13.5f;

        /// <summary>Radius of the flat clearing the island stands in, before the flank rises.</summary>
        public float ClearingRadius { get; set; } = 88f;

        /// <summary>Transition band over which the flat clearing rises into the flank.</summary>
        public float ClearingTransition { get; set; } = 110f;

        /// <summary>
        /// Where the cone's axis stands in the XZ plane. Off to one side and well behind the arena rather than
        /// over it: the crater has to be a thing in the frame with a summit against the sky, and a volcano the
        /// arena sits inside is a caldera, which is a different scene. The rivers run from here outwards, so
        /// this point also decides which way "downhill" is everywhere in the scene.
        /// </summary>
        public Vec2 ConeCenter { get; set; } = new(-45f, -250f);

        /// <summary>Radius of the cone's foot. Its base reaches to within a few dozen units of the clearing.</summary>
        public float ConeRadius { get; set; } = 215f;

        /// <summary>Height of the summit above <see cref="LevelY"/>.</summary>
        public float ConeHeight { get; set; } = 140f;

        /// <summary>
        /// How the flank's profile bends. Above 1 the slope steepens towards the summit (a young, steep
        /// stratovolcano); at 1 it is a straight-sided cone; below 1 it flattens into a shield.
        /// </summary>
        public float ConeProfile { get; set; } = 1.8f;

        /// <summary>Radius of the crater at the summit.</summary>
        public float CraterRadius { get; set; } = 30f;

        /// <summary>How deep the crater bites into the summit, below the rim.</summary>
        public float CraterDepth { get; set; } = 16f;

        /// <summary>Depth of the radial gullies raked down the flank — the channels the lava runs in.</summary>
        public float GullyDepth { get; set; } = 11f;

        /// <summary>Roughly how many gullies run down the flank.</summary>
        public float GullyCount { get; set; } = 22f;

        /// <summary>Amplitude of the broken scoria relief over the whole field, on top of the cone's massing.</summary>
        public float ScoriaRelief { get; set; } = 3.2f;

        /// <summary>How many lava rivers run down the flank. The first one is aimed to pass the arena.</summary>
        public int RiverCount { get; set; } = 5;

        /// <summary>Half-width of a river in world units, at the flank. Narrow on purpose — see the class note
        /// on readability: a wide river is a red floor, and the cluster has red balls in it.</summary>
        public float RiverWidth { get; set; } = 7.5f;

        /// <summary>How far a river wanders off its bearing on the way down, in radians.</summary>
        public float RiverWander { get; set; } = 0.06f;

        /// <summary>How fast the crust on a river visibly moves downhill, in world units per second.</summary>
        public float RiverSpeed { get; set; } = 3.5f;

        /// <summary>
        /// The bearing (radians) the first river is offset by from the line joining the cone to the arena.
        /// Zero would send it straight at the island; a few degrees walks it past the near edge instead, which
        /// is what puts moving lava alongside the play field rather than under it.
        /// </summary>
        public float RiverArenaOffset { get; set; } = 0.16f;

        /// <summary>Black basalt (linear). Well under the rivers rather than a shade under: ACES has plenty of
        /// contrast to give and a dark crust is what lets a narrow river read as incandescent.</summary>
        public Rgb RockColor { get; set; } = new(0.018f, 0.016f, 0.017f);

        /// <summary>Weathered grey scoria (linear), mixed against the basalt in patches.</summary>
        public Rgb RockColorLight { get; set; } = new(0.085f, 0.078f, 0.074f);

        /// <summary>
        /// The oxidised scoria round the summit (linear): a dark rust-red, which is what every flank the #509
        /// references drew turns towards the crater, where the hot gases have been at the rock. Patchy, off the
        /// same broad field as the grey scoria, so it reads as ground and not as a ring painted on the cone.
        /// </summary>
        public Rgb ScoriaColor { get; set; } = new(0.060f, 0.022f, 0.014f);

        /// <summary>
        /// The hottest lava (linear radiance). Over 1, so the glare pass blooms it — and <b>not far</b> over
        /// 1, which is the whole difference between a river of lava and a river of light. The first pass ran
        /// this at (7.5, 2.4, 0.3) and ACES took it straight to white-yellow: past a point, adding radiance
        /// to a saturated colour only desaturates it, and a white river is a lit crack, not molten rock.
        /// </summary>
        public Rgb LavaHot { get; set; } = new(3.40f, 0.85f, 0.10f);

        /// <summary>Cooling lava at the crusted edge of a flow (linear radiance).</summary>
        public Rgb LavaCool { get; set; } = new(0.85f, 0.12f, 0.012f);

        /// <summary>
        /// The chilled skin a flow carries (linear albedo): dark, a shade off the basalt and a touch bluer,
        /// because crust is glass and every reference drew it grey under the sky rather than black. Most of a
        /// flow is this — the incandescence is its core and its cracks (#509).
        /// </summary>
        public Rgb CrustColor { get; set; } = new(0.030f, 0.029f, 0.032f);

        /// <summary>What the crust itself still radiates, as a fraction of <see cref="LavaCool"/>: a dull red
        /// where it is young and thin under the vent, fading down the run.</summary>
        public float CrustGlow { get; set; } = 0.08f;

        /// <summary>How brightly the fissures in a flow's bed glow — in full where a lead between the crust
        /// rafts uncovers them, a trace under a raft; they stay put while the rafts roll over them (#554) — and,
        /// three times over, the seams between the plates on the crater's lava lake. 0 leaves an unbroken skin.</summary>
        public float CrackGlow { get; set; } = 0.25f;

        /// <summary>
        /// How strongly cooled lava reflects the sky. Black pahoehoe and a flow's crust are glass, and the
        /// slate sheen they take off the sky is the one thing that tells a lava field from a heap of soot at
        /// night — every ground-level reference in #509 drew it. Fresnel-weighted in the shader, so it is the
        /// grazing ground the play camera looks across that takes it; the scoria and the oxidised summit are
        /// rough and take none.
        /// </summary>
        public float SheenStrength { get; set; } = 1.2f;

        /// <summary>
        /// The thin incandescent threads running down the cone from its crater — a few long and most short,
        /// each wandering on its own way down. Every eruption #509 drew had a cone streaked with fine glowing
        /// lines rather than banded by five wide rivers. 0 turns them off; the reduced program never draws them.
        /// </summary>
        public float RivuletStrength { get; set; } = 2.4f;

        /// <summary>
        /// The glowing cracks wandering through the lava field: a few patches of them, and more beside the
        /// flows, where the ground is still hot underneath. 0 turns them off; the reduced program never draws them.
        /// </summary>
        public float FieldCrackStrength { get; set; } = 0.7f;

        /// <summary>
        /// How far either side of a flow the ground is visibly heated, as a multiple of the river's own
        /// half-width. This is the dial the scene's first pass got wrong by a mile: at six widths, and with
        /// the arena standing on the river that passes it, the halo covered the whole foreground and the
        /// entire plain glowed. A flow heats a band beside itself, not a county.
        /// </summary>
        public float HaloWidth { get; set; } = 3.0f;

        /// <summary>The scale of a flow's crust pattern in world units: one cell of the fixed fissure network in
        /// its bed is this across and 3.2 of it along, the rafts the melt carries over it are drawn from a noise
        /// 1.4 of it across and 5 along (#554), and the plates on the crater's lake are two and a half of it.</summary>
        public float PlateSize { get; set; } = 1.8f;

        /// <summary>How much of the sky's hemisphere light fills the ground.</summary>
        public float AmbientStrength { get; set; } = 0.55f;

        /// <summary>
        /// World distance over which the flank melts into the skyline haze. Far longer than any other terrain
        /// scene's, and the reason is this scene's own: haze is the horizon's colour, and every other backdrop
        /// here stands under a sky whose horizon is roughly its own ground's tone. Black basalt under dome
        /// 16's cream horizon at the desert's 420 painted the entire cone the colour of sand — the volcano
        /// vanished and a dune took its place. Aerial perspective at night over cold ground is slight; this
        /// says so.
        /// </summary>
        public float HorizonHazeDistance { get; set; } = 900f;

        /// <summary>
        /// What the haze is made of, as a multiplier on the dome's horizon colour: an ash pall, so the far
        /// flank greys out into something dark and warm rather than into the lit sky behind it.
        /// </summary>
        public Rgb HazeTint { get; set; } = new(0.30f, 0.26f, 0.24f);

        /// <summary>How much of the haze is applied at its fullest, out at the horizon.</summary>
        public float HazeStrength { get; set; } = 0.7f;

        /// <summary>The wind, a direction in the XZ plane: it leans the fountains and drives the ash.</summary>
        public Vec2 Wind { get; set; } = new(0.78f, 0.62f);

        /// <summary>
        /// How many point lights the scene pushes, capped to <see cref="SceneLights.MaxLights"/>. Slot 0 is
        /// the crater; the rest follow the brightest river fronts down the flank.
        /// </summary>
        public int LightCount { get; set; } = 6;

        /// <summary>Point-light range (quadratic falloff) of a vent or a river front.</summary>
        public float LightRange { get; set; } = 130f;

        /// <summary>
        /// What the ground is allowed to contribute to everything the shared instanced effect lights — the
        /// balls above all. This is the readability dial: the cluster's tops must stay the dome's, so the red
        /// underside reads as under-light and a red ball is still a red ball. Raising it tints the scene.
        /// <para>
        /// Worth reading against the savanna's campfire, the only other lamp of this kind in the game: that
        /// one is (2.4, 1.0, 0.32) at a range of 32. Multiplied out, <see cref="LavaHot"/> at this strength is
        /// a little under the campfire's colour — but at four times its range, which is where the energy
        /// actually is. The first pass ran 0.55 at range 190 and turned the island's stone gold.
        /// </para>
        /// </summary>
        public float LightStrength { get; set; } = 0.22f;

        /// <summary>
        /// How strongly the crater lights the underside of the cloud deck over it, as a multiple of the lava's
        /// colour a shade up from <see cref="LavaCool"/> — and it swells with each burst. Every eruption the #509 references drew
        /// has the cloud above it lit orange from below; the deck is the sky's (<c>Sky.fx</c>), so this reaches
        /// it through <see cref="CloudField.SetGroundGlow"/>, and the map editor, which draws no deck, cannot
        /// show it. 0 turns it off.
        /// <para>
        /// <b>Held low for the cluster's sake</b>, the same readability constraint as <see cref="LightStrength"/>:
        /// from the Game's play pose the cluster hangs directly in front of the deck over the crater, where this
        /// glow is brightest, so the deck behind the red and yellow balls is exactly what it paints. At 0.22 the
        /// sky there went a saturated red-orange in the Game (brighter than the Testbed's lower camera showed);
        /// at this figure it is a warm cast that a burst lifts to a glow and then lets go of.
        /// </para>
        /// </summary>
        public float DeckGlow { get; set; } = 0.11f;

        /// <summary>How far along the deck from the column over the crater that glow reaches (1/e), in world units.</summary>
        public float DeckGlowRange { get; set; } = 190f;

        /// <summary>The lava fountains at the crater and the side vents.</summary>
        public LavaFountainConfig Fountains { get; set; } = new();

        /// <summary>The eruption events the fountains and the crater light run on.</summary>
        public EruptionConfig Eruption { get; set; } = new();

        /// <summary>The drifting ash.</summary>
        public AshConfig Ash { get; set; } = new();
    }

    /// <summary>
    /// The lava fountains: a static buffer of billboard blobs, every one of them on a real ballistic arc
    /// computed in the vertex shader from its vent, so the jets taper and fall back rather than streaming.
    /// Additive and over 1 in radiance, so the glare pass blooms them for free — <c>Flame.fx</c>'s answer for
    /// the campfire, scaled up and thrown.
    /// </summary>
    public sealed class LavaFountainConfig
    {
        /// <summary>How many blobs are in flight across all vents.</summary>
        public int ParticleCount { get; set; } = 4200;

        /// <summary>Launch speed at the crater, in world units per second, before each blob's own variation.</summary>
        public float Speed { get; set; } = 46f;

        /// <summary>Downward acceleration on a blob. Not Earth's: a fountain that reads has a slower, heavier
        /// arc than the physics of a 40-unit-per-second throw would give at this scale.</summary>
        public float Gravity { get; set; } = 34f;

        /// <summary>Half-angle of the launch cone, in radians.</summary>
        public float Spread { get; set; } = 0.34f;

        /// <summary>How long a blob flies before it is recycled, in seconds.</summary>
        public float Life { get; set; } = 2.6f;

        /// <summary>Blob size in world units, before each blob's own variation. Large, and it has to be: the
        /// cone stands a good 250 units off, where a metre-wide blob is a sub-pixel spark.</summary>
        public float BlobSize { get; set; } = 3.8f;

        /// <summary>How far the wind leans a jet over as it climbs.</summary>
        public float WindDrag { get; set; } = 0.22f;

        /// <summary>
        /// The exposure a blob is seen with, in seconds: each is drawn as a streak along its own velocity,
        /// its speed across the line of sight times this long (#509). Every fountain the references drew was
        /// a spray of bright arcs, and a field of round blobs read as confetti. 0 draws round blobs.
        /// </summary>
        public float StreakTime { get; set; } = 0.07f;

        /// <summary>The smoke plume standing over the crater. 0 turns it off.</summary>
        public float PlumeStrength { get; set; } = 1f;

        /// <summary>
        /// The blaze where the jets leave the vent, as a multiple of <see cref="VolcanoSceneConfig.LavaHot"/>:
        /// one soft additive quad over the crater, brighter in a burst. Every eruption the #509 references drew
        /// has it, and no count of streaks adds up to it. 0 turns it off.
        /// </summary>
        public float GlowStrength { get; set; } = 0.3f;

        /// <summary>How many of the blobs are spent on the plume rather than the jets, as a fraction.</summary>
        public float PlumeFraction { get; set; } = 0.25f;

        /// <summary>Ash-grey smoke (linear). Under the glare threshold on purpose — smoke that blooms is
        /// steam.</summary>
        public Rgb PlumeColor { get; set; } = new(0.050f, 0.045f, 0.042f);

        /// <summary>
        /// How strongly the crater lights the plume's underside, as a multiple of <see cref="VolcanoSceneConfig.LavaCool"/>
        /// — strongest in the stem, fading up into the head, and brighter in a burst. The column lit orange
        /// from below is the brightest large thing in every eruption the #509 references drew.
        /// </summary>
        public float PlumeGlow { get; set; } = 3f;
    }

    /// <summary>
    /// The eruption events: the schedule the crater runs on, so the scene <i>does</i> something on its own
    /// rather than idling at one rate. One deterministic function of the wall clock (no state, so the map
    /// editor and the game see the same eruption at the same second), driving the jets' reach and the crater
    /// light together — light first, which is #219's lightning pattern and where an eruption <i>sound</i>
    /// would hang when the two scenes get one.
    /// </summary>
    public sealed class EruptionConfig
    {
        /// <summary>Mean seconds between bursts. Each burst's exact moment is jittered inside its own period,
        /// so the eruption never becomes a metronome.</summary>
        public float Period { get; set; } = 19f;

        /// <summary>How long one burst lasts, in seconds.</summary>
        public float Length { get; set; } = 4.5f;

        /// <summary>How much a burst multiplies the jets' launch speed at its peak.</summary>
        public float Boost { get; set; } = 1.5f;

        /// <summary>How much a burst multiplies the crater's light at its peak.</summary>
        public float LightBoost { get; set; } = 2.2f;
    }

    /// <summary>
    /// The drifting ash: the mountain snow's argument in grey — a boxful of specks around the camera, animated
    /// entirely in the vertex shader, wrapping so the fall never ends. It is a <b>separate shader</b> from the
    /// snow rather than that one retuned, because ash is not a crystal: it has no six arms to cut, no glint as
    /// it turns, and it tumbles as a flake of soot rather than falling as one.
    /// </summary>
    public sealed class AshConfig
    {
        /// <summary>Number of ash specks in the buffer.</summary>
        public int FlakeCount { get; set; } = 1800;

        /// <summary>The volume the specks fill around the camera.</summary>
        public Vec3 BoxSize { get; set; } = new(80f, 60f, 80f);

        /// <summary>How fast the ash falls. Slower than snow: it is finer and it is still burning off the
        /// column's heat.</summary>
        public float FallSpeed { get; set; } = 3.4f;

        /// <summary>The wind that carries the ash sideways.</summary>
        public Vec2 Wind { get; set; } = new(5.5f, 3.5f);

        /// <summary>How far a speck sways as it falls.</summary>
        public float Sway { get; set; } = 2.2f;

        /// <summary>Speck size in world units.</summary>
        public float FlakeSize { get; set; } = 0.075f;

        /// <summary>How fast a speck tumbles, in radians per second.</summary>
        public float Spin { get; set; } = 1.8f;

        /// <summary>Distance from the lens a speck reaches full strength at; nearer than a quarter of it, it is
        /// invisible. The snow's #85 lesson, and it costs nothing to inherit.</summary>
        public float NearFade { get; set; } = 7f;

        /// <summary>How many of the specks are still glowing embers rather than cold ash, as a fraction.</summary>
        public float EmberFraction { get; set; } = 0.06f;

        /// <summary>
        /// Cold ash grey (linear). Its luminance sits far under GLARE_THRESHOLD, which is the point: ash that
        /// blooms is snow. Dark enough that a speck reads as a fleck against the basalt behind it (2 %
        /// reflectance) without reading as a snowflake, and dark enough to visibly dirty the sky it crosses.
        /// </summary>
        public Rgb AshColor { get; set; } = new(0.075f, 0.070f, 0.067f);

        /// <summary>A live ember (linear radiance, over 1 so the few of them bloom).</summary>
        public Rgb EmberColor { get; set; } = new(2.4f, 0.55f, 0.08f);

        /// <summary>Ash speck opacity.</summary>
        public float Opacity { get; set; } = 0.55f;
    }
}
