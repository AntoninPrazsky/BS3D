using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the mountain backdrop: a snow basin the arena sits in, ringed by ridged
    /// snow-capped peaks that rise with distance and fade into an alpine haze, with falling snow.
    /// </summary>
    public sealed class MountainSceneConfig : SceneConfig
    {
        [JsonIgnore]
        public override SceneKind Kind => SceneKind.Mountain;

        /// <summary>
        /// High ground makes its own cloud: a broken deck sitting on the range is what a mountain sky does, and the holes are what let the peaks catch light in patches.
        /// </summary>
        public MountainSceneConfig()
        {
            Weather = WeatherPreset.Broken;

            //The sun's cast shadows (#471), at the savanna's own figures — 0.9 is a shadow that is dark without
            //reading as a hole, and 260 units at 2048 is 0.13 units a texel. The island and the gun on the snow
            //of the basin. The peaks shade nothing but themselves — they are terrain, and the map's box is
            //fitted to the basin rather than to them (TryShadowFit).
            Shadows = new ShadowConfig(strength: 0.9f);
        }

        /// <summary>Basin floor level Y (the basin stays below the island top,
        /// <see cref="ArenaIsland.TOP_Y"/> = -8.5; the old -10.7 referent was the square plaza's recessed glass
        /// bath, removed with the panel arena).</summary>
        public float LevelY { get; set; } = -14f;

        /// <summary>Peak height far out in the field.</summary>
        public float Height { get; set; } = 82f;

        /// <summary>Flat clearing radius the arena sits in.</summary>
        public float ClearingRadius { get; set; } = 95f;

        /// <summary>Distance over which the peaks rise beyond the clearing radius.</summary>
        public float ClearingTransition { get; set; } = 110f;

        /// <summary>Gentle basin relief amplitude around the arena.</summary>
        public float ClearingRelief { get; set; } = 1.5f;

        /// <summary>Snow-cap colour (linear, near white).</summary>
        public Rgb SnowColor { get; set; } = new(0.90f, 0.93f, 0.99f);

        /// <summary>
        /// Dark bare rock (linear). Darker since #504 (from 0.08/0.07/0.065): every range the references drew is
        /// near-black rock against bright snow, and under this scene's violet dome the old rock took the sky's
        /// lilac about as strongly as the snow beside it did, so the two read as one material in two tints.
        /// </summary>
        public Rgb RockColor { get; set; } = new(0.035f, 0.032f, 0.033f);

        /// <summary>Lighter grey rock (linear), mixed against <see cref="RockColor"/> in patches. Darker and less
        /// brown since #504, from 0.20/0.17/0.14, for the same reason.</summary>
        public Rgb RockColorLight { get; set; } = new(0.095f, 0.088f, 0.085f);

        /// <summary>Lower normal.y of the snow-slope band; below this the face sheds snow to bare rock.</summary>
        public float RockSlope { get; set; } = 0.30f;

        /// <summary>Upper normal.y of the snow-slope band; flat/gentle faces above this keep snow. Was 0.95,
        /// which needed a face within 18° of flat — fine on the old smooth massing, but #86's ridged field is
        /// steeper wherever it matters (p99 slope 3.72 against 2.48) and at 0.95 the snow retreated into the
        /// gullies, leaving a bare rock range in a scene it snows in.</summary>
        public float SnowSlope { get; set; } = 0.78f;

        /// <summary>Altitude below which the snowline is bare rock.</summary>
        public float SnowlineLow { get; set; } = -15f;

        /// <summary>Altitude above which the snowline is snow.</summary>
        public float SnowlineHigh { get; set; } = 50f;

        /// <summary>
        /// Fine rock relief: peak height of the normal-tilting height field on the rock faces.
        /// <para>
        /// <b>Unchanged by #170 on purpose</b>, though the field under it is not the one it was authored
        /// against: the four crossed sines became rotated fBm, whose amplitude for the same nominal figure is
        /// some three times lower, and that conversion is a named gain in the shader (<c>ROCK_FBM_GAIN</c>)
        /// rather than a new value here. #117 settled it that way round in the savanna and the reason is this
        /// number's <i>other</i> readers: a level pins it in its scene config — two of the shipped ones do —
        /// and a hand-built level nobody has seen may pin it too, so moving the default would leave every one
        /// of them holding a figure that now means a fifth of the relief.
        /// </para>
        /// </summary>
        public float RockReliefStrength { get; set; } = 0.5f;

        /// <summary>
        /// Fine rock relief: the coarsest feature's <b>angular</b> frequency, so a feature is <c>2π/f</c> world
        /// units across — about 10.5 at 0.6, with each of the four octaves halving that. It is stated here
        /// because the units are not obvious and the comment that stood said "features per world unit", which
        /// would make it 1.7: the field was four sines of <c>sin(dot(xz, dir) · f)</c> and is fBm on a domain
        /// scaled by <c>f/2π</c> since #170, so the figure means the same thing in both and neither is
        /// per-unit. A level that pins it in its scene config is pinning this.
        /// </summary>
        public float RockReliefFrequency { get; set; } = 0.6f;

        /// <summary>Sky-hemisphere ambient strength.</summary>
        public float AmbientStrength { get; set; } = 0.6f;

        /// <summary>
        /// How much snow the flutes on a steep face hold against the ribs between them (#504): a field that
        /// varies fast along the face and slowly down it shifts the snow's facing threshold, lower in a couloir
        /// and higher on a rib. Every face the references drew is striped so — dark ribs, white couloirs. 0 lays
        /// the snow on by facing angle alone, as it was.
        /// </summary>
        public float FluteSnow { get; set; } = 0.35f;

        /// <summary>
        /// The world height below which a slope lies in the shadow of the range across the basin when the sun is
        /// low (#504) — it takes a third of the sun there, rising to the whole of it at
        /// <see cref="AlpenglowHigh"/>. The pink summits over blue snow of every dusk reference. A high sun
        /// lights everything regardless.
        /// </summary>
        public float AlpenglowLow { get; set; } = 5f;

        /// <summary>The world height above which a slope takes the whole of a low sun. See <see cref="AlpenglowLow"/>.</summary>
        public float AlpenglowHigh { get; set; } = 45f;

        /// <summary>Distance over which the distant range fades into the alpine haze.</summary>
        public float HorizonHazeDistance { get; set; } = 500f;

        /// <summary>
        /// The falling snow's veil over the range (#654): the distance over which the snowfall takes about two
        /// thirds of a ridge's own light away, exponentially, into the skyline — what every reference of real
        /// snowfall drew first, before any flake. 0 is no veil.
        /// </summary>
        public float SnowVisibility { get; set; } = 650f;

        /// <summary>The falling snow.</summary>
        public SnowConfig Snow { get; set; } = new();
    }

    /// <summary>The mountain's falling snow (and the aurora's, with its own figures): a static buffer of billboard
    /// flakes animated in the vertex shader, drawn as a lens sees snow since #654 — see <c>Snow.fx</c>.</summary>
    public sealed class SnowConfig
    {
        /// <summary>Number of falling snow flakes in the buffer, drawn once in each layer. Was 1400, then 2800
        /// once a flake was halved (#85); 6000 since #654, because every reference of real snowfall is thousands
        /// of flakes of which most are tiny, and a few hundred sharp ones in view read as a sprinkle (9000 once
        /// the flakes were drawn at the alpha they state, which made each one far fainter).</summary>
        public int FlakeCount { get; set; } = 9000;

        /// <summary>The volume the flakes fill around the camera.</summary>
        public Vec3 BoxSize { get; set; } = new(70f, 55f, 70f);

        /// <summary>How fast the flakes fall. Was 9, a sleet's speed: snow drifts down (#654).</summary>
        public float FallSpeed { get; set; } = 6f;

        /// <summary>The wind that drifts the flakes sideways.</summary>
        public Vec2 Wind { get; set; } = new(4f, 1.5f);

        /// <summary>How far a flake sways as it falls.</summary>
        public float Sway { get; set; } = 1.2f;

        /// <summary>The flake size in world units, before each flake's own spread (0.4 to 2 times it, most of
        /// them small). Was 0.13, which put a flake 23 pixels across at eighteen units out — a ball is one unit
        /// wide, and that is the size a flake was competing with.</summary>
        public float FlakeSize { get; set; } = 0.1f;

        /// <summary>Distance from the lens a flake reaches full strength at; nearer than a quarter of it, it is
        /// invisible. Was 7, which hid the near flakes a camera in falling snow always has; they are defocused
        /// into faint discs now (<see cref="Aperture"/>) rather than hidden (#654).</summary>
        public float NearFade { get; set; } = 2.5f;

        /// <summary>
        /// Where the lens is focused: a flake nearer than this is out of focus, the more so the nearer it is
        /// (#654). The cluster stands about thirty units out, so the flakes between the lens and it are what
        /// blurs — which is what every reference's near flakes do.
        /// </summary>
        public float Focus { get; set; } = 12f;

        /// <summary>
        /// How far, in world units, a flake at the lens is blurred. The blurred disc grows in quadrature with the
        /// flake and its alpha falls with its area, so a near flake is a big faint disc and never a white coin —
        /// the answer to #85's snowball, where the six-armed crystal that answered it before read as an icon.
        /// </summary>
        public float Aperture { get; set; } = 0.12f;

        /// <summary>
        /// Seconds a flake is drawn out over along its own motion on screen — a camera's shutter, and what says
        /// falling rather than floating. At the fall speed it adds about one flake's length (#654).
        /// </summary>
        public float Shutter { get; set; } = 1f / 90f;

        /// <summary>
        /// The far layer: the same flakes drawn again in a box this many times larger, which perspective makes
        /// tiny — the veil a snowfall lays over a range (#654). 1 or less draws the near box alone.
        /// </summary>
        public float FarLayerScale { get; set; } = 3.2f;

        /// <summary>A lit grey-white, dimmer than the sky and the snow and brighter than rock: every reference drew
        /// flakes as dark specks against a bright sky and white ones against a dark face, which a flake does when
        /// its own radiance sits between the two (#654 — it was 0.72-0.82, and wherever it met the bright sky it
        /// vanished into it). Under GLARE_THRESHOLD (0.55), so no flake blooms.</summary>
        public Rgb FlakeColor { get; set; } = new(0.55f, 0.58f, 0.64f);

        /// <summary>Snow flake opacity, before the defocus and the streak spread it. Was 0.9 — near enough opaque
        /// that a flake was a solid white coin, and the feathered rim that was meant to soften it gets crushed
        /// back to white by the tonemap wherever the flake crosses something dark (#85).</summary>
        public float Opacity { get; set; } = 1f;
    }
}
