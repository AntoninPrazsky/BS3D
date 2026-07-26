using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Configuration of the procedural city backdrop: the block grid, tower heights, and the window/facade
    /// look. The same city serves two <see cref="SceneKind"/>s — the ordinary warm/cool daytime city and
    /// the <see cref="SceneKind.NeonCity"/> night relight — selected by <see cref="Neon"/>; the neon-only
    /// look lives in <see cref="NeonLook"/>.
    ///
    /// The config is wired to all three of its sites: <see cref="City"/>'s constructor reads the layout,
    /// <see cref="InstancedModelRenderer.CityConfig"/> pushes the window look to the shader on every city
    /// draw, and the caller reads <see cref="WindowBrightness"/> / <see cref="NeonLook"/> for the day/neon
    /// relight — which is what lets the map editor's PropertyGrid edit the city live.
    /// </summary>
    public sealed class CitySceneConfig : SceneConfig
    {
        /// <summary>City or NeonCity, chosen by <see cref="Neon"/>.</summary>
        [JsonIgnore]
        public override SceneKind Kind => Neon ? SceneKind.NeonCity : SceneKind.City;

        /// <summary>Neon night relight instead of the ordinary warm/cool daytime city (drives <see cref="Kind"/>).</summary>
        public bool Neon { get; set; } = false;

        /// <summary>Centre-to-centre spacing of the street grid, in world units.</summary>
        public float BlockPitch { get; set; } = 30f;

        /// <summary>Width of the streets between blocks; the rest of the pitch is buildable.</summary>
        public float StreetWidth { get; set; } = 9f;

        /// <summary>How far the city reaches from the arena, in blocks (drives the building count).</summary>
        public int RadiusBlocks { get; set; } = 14;

        /// <summary>Roofline height; sits well above the play surface so towers stand against the sky.</summary>
        public float RooflineY { get; set; } = 34f;

        /// <summary>How far above and below the roofline the tops are allowed to wander.</summary>
        public float RooflineSpread { get; set; } = 26f;

        /// <summary>Where every building starts; far down so streets read as canyons falling into darkness.</summary>
        public float BaseY { get; set; } = -420f;

        /// <summary>Towers taper down away from the centre; how much shorter per block outward.</summary>
        public float TaperPerBlock { get; set; } = 1.8f;

        /// <summary>Highest a building directly under the arena is allowed to reach.</summary>
        public float UnderArenaTopY { get; set; } = -78f;

        /// <summary>How much further down the under-arena towers scatter.</summary>
        public float UnderArenaSpread { get; set; } = 90f;

        /// <summary>
        /// Albedo of the plaster between the windows by day, in <b>linear</b> radiance. A real albedo, not a
        /// dark one: neither specular term is multiplied by albedo, so with a near-black facade the whole
        /// brightness of a tower was its white highlight and its grazing sky reflection — a dark surface under
        /// a bright mirror, which is exactly how glass reads. The shine belongs to the windows
        /// (<see cref="WindowReflectionBoost"/>), and what lights the wall instead is diffuse light.
        /// </summary>
        public Rgb FacadeColor { get; set; } = new(0.17f, 0.17f, 0.168f);

        /// <summary>
        /// Albedo of the same plaster under the neon relight — dark, so the neon carries the scene whatever the
        /// dome, but not black: a matte wall with nothing to reflect would otherwise be a silhouette.
        /// </summary>
        public Rgb FacadeNeonColor { get; set; } = new(0.045f, 0.044f, 0.052f);

        /// <summary>
        /// How far a building's plaster tone wanders from <see cref="FacadeColor"/>, as a fraction either way
        /// (0 = every tower the same shade). Real renders are mixed and painted and weathered per building —
        /// and once the wall is matte its tone is the only variety it has, the tonal spread across the skyline
        /// having come from the mirror that is now gone.
        /// </summary>
        public float FacadeColorVariation { get; set; } = 0.25f;

        /// <summary>
        /// Peak height of the plaster's grain in world units (0 = a mathematically flat wall). This, not the
        /// reflectance, is what makes a facade read as rendered plaster: a flat box face takes the light as a
        /// flat box face however its specular is tuned. Only tilts the normal, so silhouettes stay clean, and
        /// every octave band-limits itself against the pixel footprint — full on the towers around the arena,
        /// silently gone on the skyline behind them.
        /// </summary>
        public float FacadeGrainStrength { get; set; } = 0.018f;

        /// <summary>
        /// Cells of the coarsest noise octave per world unit: larger is a finer grain. Three finer octaves ride
        /// on top, each fading out on its own once a screen pixel grows past half its cell.
        /// </summary>
        public float FacadeGrainFrequency { get; set; } = 2.2f;

        /// <summary>
        /// How much the grain also <i>shades</i> — the ambient it keeps out of its own hollows, and the mottling
        /// of the plaster's tone (0 = the normal is tilted and nothing else). It is what makes the grain visible
        /// at all at a tower's distance, where a few degrees of tilt move a matte surface by a percent: relief
        /// by normal alone has its bumps lit and its hollows just as bright, which is why it reads as a painted
        /// texture rather than as a surface. The mottling is the stronger of the two cues at this range.
        /// </summary>
        public float FacadeGrainShading { get; set; } = 0.35f;

        /// <summary>
        /// How polished the plaster is: 0 = fully rough (the sky reaches it as its average, and the Fresnel
        /// grazing mirror is gone), 1 = a polished slab, which is what the whole facade used to be. This is the
        /// dial that decides whether the city is built of plaster or of glass.
        /// </summary>
        public float FacadeSmoothness { get; set; } = 0.05f;

        /// <summary>How much of the direct lights' highlight the plaster shows (1 = as much as any other surface).</summary>
        public float FacadeHighlight { get; set; } = 0.1f;

        /// <summary>Direct highlight of the window glass, against the plaster's <see cref="FacadeHighlight"/>.</summary>
        public float WindowHighlightBoost { get; set; } = 1.6f;

        /// <summary>
        /// Albedo of the glass, in <b>linear</b> radiance — dark, because what is behind a pane is a dim room
        /// and not a rendered wall. This is the other half of why the windows read as glass while the plaster
        /// does not: a dark surface under a bright mirror is what glass looks like, which is the same
        /// combination that was wrong on the wall. It is also what gives a facade its variation back, a pane
        /// being dark where it faces nothing and bright where it catches the sky.
        /// </summary>
        public Rgb WindowGlassColor { get; set; } = new(0.02f, 0.025f, 0.032f);

        /// <summary>
        /// How much more of the sky a window mirrors than the scene's own
        /// <see cref="InstancedModelRenderer.SpecularAmbientStrength"/> asks for — a multiple, because the
        /// panes are the one part of a facade that really is a mirror, and that dial is set low for the wall.
        /// </summary>
        public float WindowReflectionBoost { get; set; } = 5f;

        /// <summary>Horizontal spacing of a window column, in world units (target pitch).</summary>
        public float WindowPitchX { get; set; } = 1.7f;

        /// <summary>Vertical spacing of a window row, in world units (target pitch).</summary>
        public float WindowPitchY { get; set; } = 2.2f;

        /// <summary>How much of each cell is glass horizontally (rest is wall).</summary>
        public float WindowFillX { get; set; } = 0.46f;

        /// <summary>How much of each cell is glass vertically (rest is wall).</summary>
        public float WindowFillY { get; set; } = 0.52f;

        /// <summary>
        /// Half-width of the plaster frame moulding around each pane, as a fraction of the cell beyond
        /// <see cref="WindowFillX"/>/<see cref="WindowFillY"/> (0 = no frame, just a flat pane). A pane cut
        /// straight into a flat wall reads as a hole in it; a real window is set into a frame, and the frame is
        /// the same render carried proud of the wall. The moulding lives in the ring between the glass edge and
        /// this width past it, so it is plaster -- it catches light and casts its own shadow -- not a second
        /// surface pasted on. ~0.1 is a clear middle weight that reads as a frame from across the arena.
        /// </summary>
        public float WindowFrameWidth { get; set; } = 0.1f;

        /// <summary>
        /// How high the moulding stands proud of the surrounding wall, in world units (0 = a flat pane with no
        /// frame). This is the bead's own relief, not its reflectance: what makes a frame read as standing off
        /// the wall is that its top catches light, its flanks lean the normal, AND it throws a cast shadow on
        /// the wall in the sun's lee. The shadow length scales with this height, so a taller trim reads as more
        /// strongly raised -- a fraction of a unit is enough to cast a visible shadow at the city's scale.
        /// </summary>
        public float WindowFrameHeight { get; set; } = 0.06f;

        /// <summary>
        /// How much the moulding's own relief also <i>shades</i> — the lit flat top lightened and the wall in
        /// its cast shadow darkened (0 = the normal is tilted and nothing else). The same lesson
        /// <see cref="FacadeGrainShading"/> already taught: a tilted normal alone is invisible at a tower's
        /// distance, so the fillet's top must also lighten and its shadow darken for the trim to read as a
        /// raised body rather than a painted stripe.
        /// </summary>
        public float WindowFrameShading { get; set; } = 0.55f;

        /// <summary>Wall border kept clear of glass at every building edge, in world units.</summary>
        public float WindowMargin { get; set; } = 0.9f;

        /// <summary>Fraction of the windows that are lit.</summary>
        public float WindowLitFraction { get; set; } = 0.42f;

        /// <summary>Warm lamp colour a lit window can take.</summary>
        public Rgb WindowWarm { get; set; } = new(1.0f, 0.78f, 0.44f);

        /// <summary>Cool lamp colour a lit window can take.</summary>
        public Rgb WindowCool { get; set; } = new(0.52f, 0.82f, 1.0f);

        /// <summary>How long a window holds one state before deciding again.</summary>
        public float WindowHoldSeconds { get; set; } = 7.0f;

        /// <summary>How much the hold interval varies from window to window.</summary>
        public float WindowHoldVariation { get; set; } = 24.0f;

        /// <summary>How much of an interval the on/off switch itself takes.</summary>
        public float WindowSwitchFade { get; set; } = 0.06f;

        /// <summary>How brightly a lit window burns; kept under the glare threshold so it does not veil its tower.</summary>
        public float WindowBrightness { get; set; } = 0.35f;

        /// <summary>The neon-night look, used when <see cref="Neon"/> is true.</summary>
        public NeonConfig NeonLook { get; set; } = new();
    }

    /// <summary>The neon night relight of the city: bloom-bright windows plus a ring of magenta/cyan point lights.</summary>
    public sealed class NeonConfig
    {
        /// <summary>Neon window brightness, well over the glare threshold so each lit sign blooms.</summary>
        public float WindowBrightness { get; set; } = 0.9f;

        /// <summary>Number of alternating magenta/cyan point lights ringing the island.</summary>
        public int LightCount { get; set; } = 6;

        /// <summary>Falloff range of each neon point light, in world units.</summary>
        public float LightRange { get; set; } = 58f;

        /// <summary>Radius of the ring of neon point lights around the island.</summary>
        public float LightRadius { get; set; } = 46f;

        /// <summary>Height (Y) of the neon point-light ring.</summary>
        public float LightHeight { get; set; } = -6f;

        /// <summary>Magenta neon point-light colour (linear radiance, over 1).</summary>
        public Rgb Magenta { get; set; } = new(2.6f, 0.25f, 2.2f);

        /// <summary>Cyan neon point-light colour (linear radiance, over 1).</summary>
        public Rgb Cyan { get; set; } = new(0.25f, 2.2f, 2.8f);
    }
}
