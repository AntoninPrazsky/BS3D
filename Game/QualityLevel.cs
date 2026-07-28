namespace BS3D
{
    /// <summary>
    /// How much of the frame's detail the machine is being asked to pay for. One bundled dial rather than the
    /// single antialiasing setting it replaces (#63), because supersampling was never a performance dial: it is
    /// tied to a look decision (it is what keeps the balls' procedural relief sharp), and it was the only thing
    /// that reached the rest of the frame at all.
    /// </summary>
    public enum QualityLevel
    {
        Low,
        Medium,
        High,
    }

    /// <summary>
    /// What one tier turns down, and the measured reason for each entry. Every figure below was measured on this
    /// project's weakest development machine — a Ryzen 7 5700U with integrated Radeon graphics, windowed
    /// 1600×900, vsync off, on the front end (#64) — and the numbers quoted are the <b>neon city</b>, which is
    /// the most expensive of the seven scenes by a wide margin and therefore the one a tier has to be chosen
    /// against. The five terrain scenes all sit between 15 and 19 ms a frame at <c>High</c> and never needed a
    /// tier; the two city scenes sit at 77 and 103 ms, which is what this exists for.
    /// <para>
    /// <b>The order of magnitude is the scene, not the tier</b>: 103 ms for the neon city against 14.6 ms for
    /// the sea, a spread of 6.8×, on identical settings. That is why the adaptive probe measures rather than
    /// assuming, and why it steps this tier rather than supersampling alone.
    /// </para>
    /// </summary>
    internal readonly struct QualityPreset
    {
        /// <summary>
        /// Supersampling, and by far the biggest single lever: it is the whole frame's shaded pixel count. On the
        /// neon city 2× → 1× is 103 ms → 33 ms. At 1× the scene target falls back to 8× MSAA, so geometry edges
        /// stay clean and it is shading detail that is given up rather than antialiasing as such.
        /// </summary>
        public readonly int SupersampleFactor;

        /// <summary>
        /// The city's plaster grain — three noise octaves per city pixel. Measured at 11.3 ms of a 103 ms neon
        /// frame (11%). Band-limited to nothing on the skyline already, so what is lost is the mottling on the
        /// near ring of towers and the distant city is untouched.
        /// </summary>
        public readonly float FacadeGrainStrength;

        /// <summary>
        /// The raised moulding around every window pane, its lit crest and its cast shadow — four profile
        /// evaluations per city pixel. Measured at 9.1 ms (9%). Zeroed, a pane is cut flat into the wall, which
        /// the shader's own comment warns "reads as a hole in it" — so this is the second thing to give up, not
        /// the first.
        /// </summary>
        public readonly float WindowFrameWidth;

        /// <summary>
        /// The city's radius in blocks, and the surprise of the measurement: <b>overdraw</b>, not per-pixel
        /// complexity, is the city's largest cost. 14 → 12 saves almost nothing (1.7 ms — the towers still fill
        /// the frame), while 12 → 10 saves 19.4 ms and 10 → 8 another 16. The knee is at 10, which is why
        /// <c>Low</c> stops there: at 8 the horizon has moved so far in, and the outer roofline risen so much
        /// (the taper is per block), that it reads as a different city rather than a cheaper one.
        /// </summary>
        public readonly int CityRadiusBlocks;

        public QualityPreset(int supersampleFactor, float facadeGrainStrength, float windowFrameWidth, int cityRadiusBlocks)
        {
            SupersampleFactor = supersampleFactor;
            FacadeGrainStrength = facadeGrainStrength;
            WindowFrameWidth = windowFrameWidth;
            CityRadiusBlocks = cityRadiusBlocks;
        }

        /// <summary>
        /// The three tiers, indexed by <see cref="QualityLevel"/>. The two city figures at <c>High</c> and
        /// <c>Medium</c> are <see cref="Prazsky.Core.Render.Config.CitySceneConfig"/>'s own defaults restated, so
        /// those tiers reproduce today's look exactly rather than approximately — which is the same rule every
        /// <c>SceneConfig</c> default follows.
        /// <para>
        /// Measured on the neon city: <c>High</c> 9.7 FPS, <c>Medium</c> 30.5, <c>Low</c> 43.1. On the ordinary
        /// city: 13.1, 38.4, 55.4. On the five terrain scenes <c>Medium</c> alone already gives 80–124 FPS, so
        /// <c>Low</c> is there for the cities and for machines weaker than this one.
        /// </para>
        /// </summary>
        internal static readonly QualityPreset[] Presets =
        {
            //Low — 43 FPS on the worst scene. Supersampling off, the city's two per-pixel luxuries off, and the
            //skyline pulled in to the measured knee.
            new(supersampleFactor: 1, facadeGrainStrength: 0f, windowFrameWidth: 0f, cityRadiusBlocks: 10),

            //Medium — 30 FPS on the worst scene, and every scene's full detail. Only supersampling is given up,
            //which is the one change that reaches all seven scenes.
            new(supersampleFactor: 1, facadeGrainStrength: 0.018f, windowFrameWidth: 0.1f, cityRadiusBlocks: 14),

            //High — the look the game was authored at, unchanged.
            new(supersampleFactor: 2, facadeGrainStrength: 0.018f, windowFrameWidth: 0.1f, cityRadiusBlocks: 14),
        };
    }
}
