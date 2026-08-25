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
        /// The hottest lava (linear radiance). Over 1, so the glare pass blooms it — and <b>not far</b> over
        /// 1, which is the whole difference between a river of lava and a river of light. The first pass ran
        /// this at (7.5, 2.4, 0.3) and ACES took it straight to white-yellow: past a point, adding radiance
        /// to a saturated colour only desaturates it, and a white river is a lit crack, not molten rock.
        /// </summary>
        public Rgb LavaHot { get; set; } = new(3.40f, 0.85f, 0.10f);

        /// <summary>Cooling lava at the crusted edge of a flow (linear radiance).</summary>
        public Rgb LavaCool { get; set; } = new(0.85f, 0.12f, 0.012f);

        /// <summary>How strongly the crackle seams between the crust plates glow away from the rivers — the
        /// "stone that cracks", which is what makes the ground read as crust over liquid rather than as painted
        /// rock. 0 leaves a cold basalt field.</summary>
        public float SeamGlow { get; set; } = 0.18f;

        /// <summary>
        /// How far either side of a flow the ground is visibly heated, as a multiple of the river's own
        /// half-width. This is the dial the scene's first pass got wrong by a mile: at six widths, and with
        /// the arena standing on the river that passes it, the halo covered the whole foreground and the
        /// entire plain glowed. A flow heats a band beside itself, not a county.
        /// </summary>
        public float HaloWidth { get; set; } = 3.0f;

        /// <summary>Size of one crust plate in world units, for the crackle seams.</summary>
        public float PlateSize { get; set; } = 2.6f;

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
        public int ParticleCount { get; set; } = 2600;

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
        public float BlobSize { get; set; } = 3.2f;

        /// <summary>How far the wind leans a jet over as it climbs.</summary>
        public float WindDrag { get; set; } = 0.22f;

        /// <summary>The smoke plume standing over the crater. 0 turns it off.</summary>
        public float PlumeStrength { get; set; } = 1f;

        /// <summary>How many of the blobs are spent on the plume rather than the jets, as a fraction.</summary>
        public float PlumeFraction { get; set; } = 0.35f;

        /// <summary>Ash-grey smoke (linear). Under the glare threshold on purpose — smoke that blooms is
        /// steam.</summary>
        public Rgb PlumeColor { get; set; } = new(0.085f, 0.078f, 0.076f);
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
