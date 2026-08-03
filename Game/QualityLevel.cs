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
    /// the most expensive of the eleven scenes and therefore the one a tier has to be chosen against. The two
    /// city scenes sat at 77 and 103 ms when the tiers were chosen, and since the front-to-back sort (see
    /// "Drawing the city near to far" in docs/rendering.md) sit at 23.0 and 27.5 — still the two dearest
    /// scenes, and still under the probe's floor at <c>High</c> on this machine, so the tier stays. Of the nine
    /// terrain scenes, the <b>five</b> that existed when this was measured — sea, savanna, desert, mountains,
    /// meadow — all sat between 15 and 19 ms a frame at <c>High</c> and never needed a tier; forest, space,
    /// dream and cavern arrived later and have not been through the same measurement, so nothing here claims a
    /// figure for them.
    /// <para>
    /// <b>The order of magnitude is the scene, not the tier</b>: the neon city against the sea was a spread of
    /// 6.8× on identical settings when the tiers were chosen, and is 1.9× since the sort. That is why the
    /// adaptive probe measures rather than assuming, and why it steps this tier rather than supersampling alone.
    /// </para>
    /// </summary>
    internal readonly struct QualityPreset
    {
        /// <summary>
        /// Supersampling, and by far the biggest single lever: it is the whole frame's shaded pixel count. On the
        /// neon city 2× → 1× is 27.5 ms → 10.5 (it was 103 → 33 before the city's draw order was fixed). At 1×
        /// the scene target falls back to 8× MSAA, so geometry edges stay clean and it is shading detail that is
        /// given up rather than antialiasing as such.
        /// <para>
        /// <b>Two is the look this game is authored for</b>, which is why <c>High</c> carries it and why the
        /// probe exists to take it away rather than the reverse: the balls' procedural relief is *shading*,
        /// which MSAA does not touch, so the extra samples are what keep the fine octaves alive. It is also, on
        /// a weak GPU, by far the most expensive thing in the frame — measured on an integrated Vega 10,
        /// dropping it to 1 nearly tripled the frame rate, while cutting the ball count threefold barely moved
        /// it. Hence <see cref="BS3DGame.TuneQualityToFrameRate"/>. (This paragraph was the doc on a
        /// <c>DEFAULT_SUPERSAMPLE_FACTOR</c> constant in the host that nothing read — the tier has been what
        /// sets the factor for some time. #71 deleted the constant and kept the reasoning here, where the
        /// number it was documenting actually lives.)
        /// </para>
        /// </summary>
        public readonly int SupersampleFactor;

        /// <summary>
        /// The city's plaster grain — three noise octaves per city pixel. Measured at 11.3 ms of the 103 ms
        /// pre-sort neon frame (11%); the front-to-back sort then removed the overdraw that was multiplying
        /// every per-pixel term, and the grain and the moulding <b>together</b> now measure 0.9 ms at 1×
        /// (<c>Low</c> against <c>Medium</c> at the same skyline). Kept in the tier: on machines weaker than
        /// the reference APU the same fraction is real milliseconds. Band-limited to nothing on the skyline
        /// already, so what is lost is the mottling on the near ring of towers and the distant city is
        /// untouched.
        /// </summary>
        public readonly float FacadeGrainStrength;

        /// <summary>
        /// The raised moulding around every window pane, its lit crest and its cast shadow — four profile
        /// evaluations per city pixel. Measured at 9.1 ms of the same pre-sort frame (9%); see
        /// <see cref="FacadeGrainStrength"/> for what the sort did to both. Zeroed, a pane is cut flat into the
        /// wall, which the shader's own comment warns "reads as a hole in it" — so this is the second thing to
        /// give up, not the first.
        /// </summary>
        public readonly float WindowFrameWidth;

        /// <summary>
        /// The city's radius in blocks. It was a tier entry once: before the city was drawn near to far,
        /// <b>overdraw</b> — not per-pixel complexity — was the city's largest cost, and pulling the skyline in
        /// to the measured knee at 10 blocks saved 19.4 ms of hidden facades (12 → 10 alone). The front-to-back
        /// sort removed exactly that cost, and the re-measured sweep on the same machine is flat: 14/12/10/8
        /// blocks → 27.5/28.6/28.3/26.4 ms at <c>High</c>, run-to-run noise and nothing else (it read
        /// 102.9/101.2/83.5/67.3 before). So every tier draws the authored skyline now, and the dial stays only
        /// as the mechanism a future tier would use — <c>ApplyQuality</c> still rebuilds the city when it
        /// changes.
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
        /// Measured on the neon city since the city's draw-order fix: <c>High</c> 36.4 FPS, <c>Medium</c> 95.6,
        /// <c>Low</c> 103.7. On the ordinary city: 43.4, 108.0, 119.2. (When the tiers were chosen, before the
        /// sort, the same runs read 9.7/30.5/43.1 and 13.1/38.4/55.4.) On the five terrain scenes <c>Medium</c>
        /// alone already gives 80–124 FPS, so <c>Low</c> is there for the cities and for machines weaker than
        /// this one.
        /// </para>
        /// </summary>
        internal static readonly QualityPreset[] Presets =
        {
            //Low — ~104 FPS on the worst scene since the sort (43 when the tier was chosen). Supersampling and
            //the city's two per-pixel luxuries off. The skyline is the authored 14 again: the reduced radius
            //existed to cut overdraw, and the sort cut it instead (see CityRadiusBlocks).
            new(supersampleFactor: 1, facadeGrainStrength: 0f, windowFrameWidth: 0f, cityRadiusBlocks: 14),

            //Medium — 30 FPS on the worst scene, and every scene's full detail. Only supersampling is given up,
            //which is the one change that reaches all eleven scenes.
            new(supersampleFactor: 1, facadeGrainStrength: 0.018f, windowFrameWidth: 0.1f, cityRadiusBlocks: 14),

            //High — the look the game was authored at, unchanged.
            new(supersampleFactor: 2, facadeGrainStrength: 0.018f, windowFrameWidth: 0.1f, cityRadiusBlocks: 14),
        };
    }
}
