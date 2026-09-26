using Prazsky.Core.Render;

namespace BS3D
{
    /// <summary>
    /// How much of the frame's detail the machine is being asked to pay for. One bundled dial rather than the
    /// single antialiasing setting it replaces (#63), because supersampling was never a performance dial: it is
    /// tied to a look decision (it is what keeps the balls' procedural relief sharp), and it was the only thing
    /// that reached the rest of the frame at all.
    /// </summary>
    /// <remarks>
    /// <b>Ultra is last, and only a player picks it</b> (#484, the owner's ruling of 2026-09-25: "someone with a
    /// faster card than a 6900 XT may play this"). It sits above the look the game is authored at, so the
    /// adaptive probe — which starts at High and only ever steps down — can never reach it, and turning Auto
    /// quality back on over it hands the tier back at High. Appended rather than inserted so the numeric
    /// values of the three older tiers do not move; <c>Settings.json</c> stores the name anyway.
    /// </remarks>
    public enum QualityLevel
    {
        Low,
        Medium,
        High,
        Ultra,
    }

    /// <summary>
    /// What one tier turns down, and the measured reason for each entry.
    /// <para>
    /// <b>⚠ ONE THING A TIER MAY NEVER TURN DOWN: THE RESOLUTION.</b> The scene is always drawn at the
    /// display's native size, and a tier lowers <i>what is drawn</i> — a specular highlight may go, a relief
    /// octave may go, a scene's own extras may go — never how many pixels it is drawn into. That is the
    /// owner's standing ruling (#298, 2026-08-28) and it was given with the measurement in hand and against
    /// it: rendering at 0.85× of native is the cheapest lever this frame has (0.59–1.05 ms across four scenes
    /// on the reference desktop) and the only one that moves the cavern at all, and it is refused anyway,
    /// because the magnified picture looks ugly and no frame rate buys that back.
    /// <see cref="Prazsky.Core.Render.PostProcessPipeline.RenderScale"/> exists as a measuring instrument and
    /// must stay one. <b>Note the direction</b>: supersampling ABOVE native is a legitimate entry and
    /// <see cref="QualityLevel.High"/> carries one — it is only below 1× that is shut.
    /// </para>
    /// <para>
    /// Every figure below was measured on this
    /// project's weakest development machine — a Ryzen 7 5700U with integrated Radeon graphics, windowed
    /// 1600×900, vsync off, on the front end (#64) — and the numbers quoted are the <b>neon city</b>, which is
    /// the most expensive of the scenes and therefore the one a tier has to be chosen against. The two
    /// city scenes sat at 77 and 103 ms when the tiers were chosen, and since the front-to-back sort (see
    /// "Drawing the city near to far" in docs/rendering.md) sit at 23.0 and 27.5 — still the two dearest
    /// scenes, and still under the probe's floor at <c>High</c> on this machine, so the tier stays. Of the ten
    /// non-city scenes, the <b>five</b> that existed when this was measured — sea, savanna, desert, mountains,
    /// meadow — all sat between 15 and 19 ms a frame at <c>High</c> and never needed a tier; forest, space,
    /// dream, cavern and the Moon arrived later and have not been through the same measurement, so nothing here
    /// claims a figure for them (the Moon's own measured cost is in "The Moon (Game)" in docs/scenes.md).
    /// </para>
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
        /// give up, not the first. Since #435 it takes the pane's surround with it but not the sill, its shadow,
        /// the reveal or the glazing bars, which draw on every tier (see "The city's windows" in docs/rendering.md).
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

        /// <summary>
        /// Multisample samples on the scene target, and the <b>first entry that reaches every scene without
        /// touching a pixel count</b> (#298). It only bites below <c>High</c>: at <c>ssaa</c> 2 the supersample
        /// resolve averages geometry edges itself and the pipeline builds the target with none, so the figure
        /// here is ignored there and stated as the default for honesty rather than effect.
        /// <para>
        /// <b>Measured</b> on the reference desktop, 3840×1600, <c>ssaa</c> 1, a level's worth of balls: 8× →
        /// none is 1.12 ms on the mountain, 0.61 meadow, 0.58 neon city, 0.30 cavern — and 8× → 4× is free
        /// everywhere there (0.04–0.07).
        /// </para>
        /// <para>
        /// <b>⚠ 8× → 4× is NOT free on the machine the rungs exist for (#540)</b>, which is why <c>Medium</c>
        /// carries 4. Measured on the reference APU (Ryzen 7 5700U + integrated Radeon) in the Testbed, the
        /// Game's play vantage over 1000 balls, 1600×900, <c>ssaa</c> 1, full scene detail, the three counts
        /// alternated inside one process and differenced within each cycle: the mountain (ice balls) 8× 22.70 ms
        /// → 4× −1.38 (cheaper in 97 % of 30 cycles) → 2× −5.03; the neon city (gem balls) 31.88 → −0.65 (93 %)
        /// → −2.85. The desktop's "free" was a wide part with bandwidth to spare, the benchmark skill's
        /// "attribution does not travel" once more. What 4 gives up against 8 is edge gradation a pixel wide.
        /// </para>
        /// <para>
        /// <b><c>Low</c> takes 2 rather than 0, and that is a judgement call worth knowing about.</b> This
        /// game's frame is a thousand SPHERES: their silhouettes are most of the geometry edges in it, and with
        /// no supersampling and no multisampling at all they crawl as the cluster swings — <c>Low</c> is meant
        /// to look plainer, not to shimmer. Two samples keep the silhouettes stable and still collect a little
        /// over half of what dropping them all would (mountain 0.61 of the 1.12). Zero is one constant away if
        /// the owner ever wants the rest of it.
        /// </para>
        /// </summary>
        public readonly int MsaaSamples;

        /// <summary>
        /// The most texels a side the sun shadow map may have on this rung, or 0 for a scene's own
        /// <c>ShadowConfig.MapSize</c> (#484). <c>High</c> takes the authored 4096 (0.063 units a texel over the
        /// 260-unit extent); <c>Medium</c> and <c>Low</c> hold it at the 2048 the map shipped with, which is a
        /// quarter of the memory (33.5 MB against 134, eight bytes a texel) and the same nine taps a pixel.
        /// <para>
        /// <b>Measured</b> in the Testbed on the reference desktop, the savanna's play vantage at 1920×1080, ssaa
        /// 2, alternating in one process: 2048 3.45–3.49 ms, 4096 3.50–3.53, 8192 3.66–3.70 — the receiver's nine
        /// taps cost the same whatever the map, so a size costs its rasterisation and its cache and neither
        /// shows until 8192. <c>Low</c> skips the map through <c>SceneDetail</c> anyway; its figure is stated for
        /// honesty. A cap rather than a size because a tier only takes away (#298): a scene authored small must not
        /// come out larger on a lower rung. See "Sun shadows" in docs/rendering.md.
        /// </para>
        /// </summary>
        public readonly int ShadowMapCap;

        /// <summary>
        /// What a scene's <c>ShadowConfig.MapSize</c> is multiplied by on this rung before
        /// <see cref="ShadowMapCap"/> applies, and 1 everywhere but <c>Ultra</c> (#484), which doubles the
        /// authored 4096 to <b>8192</b> — 0.032 units a texel over the 260-unit extent, half High's step.
        /// A factor rather than a size so a scene authored small stays proportionally small.
        /// <para>
        /// <b>What it costs is memory first</b>: eight bytes a texel is <b>537 MB</b> of card memory against
        /// High's 134, which is why only the player can ask for it and the probe never does. The time is small on
        /// a fast card: +0.22 ms (desert) and +0.30 (savanna) over High at 3840×1600 in the Testbed on the
        /// reference desktop, paired in one process — see "The map's size, and the tier it follows" in
        /// docs/rendering.md. The nine-tap box stays three texels wide, so the edge gets finer, not softer.
        /// </para>
        /// </summary>
        public readonly int ShadowMapScale;

        /// <summary>
        /// Whether the ceiling's glass bends what is behind it (#541) - a copy of the frame taken mid-scene and a
        /// ray traced through the cut slab per pixel of glass - or is drawn as the plain translucent pane it was.
        /// <para>
        /// <b>Measured</b> on the reference APU (Ryzen 7 5700U + integrated Radeon, on AC), the Game at 1600×900,
        /// <c>nocap</c>, 45 s runs with the first eight readings dropped, the refracting pane against
        /// <c>plainceiling</c> alternated in two cycles, the better of each pair's medians: Heart (the savanna, the
        /// plate across the top of the frame) Low 13.61 → 14.90 ms, Medium 19.10 → 20.67, High 34.03 → 35.29;
        /// Ziggurat (the neon city) 12.95 → 14.29, 15.52 → 17.02, 37.41 → 38.86. <b>About 1.3–1.6 ms on every
        /// rung</b>, and nearly flat across them although High shades four times the pane's pixels — which points at
        /// the copy (the mid-frame resolve of a multisampled target, or the supersampled one's box filter) rather
        /// than at the pane, though the two were not separated by a measurement. Paid only while the plate is in frame (<c>BS3DGame.CeilingInView</c>): over a tall cluster the
        /// play camera frames it out, and then nothing is copied.
        /// </para>
        /// <para>
        /// <b>Every rung but <c>Low</c> carries it</b> (Medium since the owner's word of
        /// 2026-09-25, "ať má lom i medium"). <c>Low</c> is the rung for a machine like that APU, already short of
        /// its budget on the volcano (#540), so a millisecond and a half there is spent where the frame has none, and a
        /// bend is exactly the kind of thing a tier gives up (#298: a tier lowers what is drawn). <c>Medium</c> on the
        /// APU pays the 1.5 ms above; on the reference desktop (RX 6900 XT, 1920×1080, <c>nocap</c>, three alternated
        /// cycles, the eight first readings dropped, after the finer cut) it costs about 0.1 ms: Heart 1.77–1.81 →
        /// 1.85–1.96 ms, Ziggurat 1.53–1.54 → 1.61–1.62.
        /// </para>
        /// </summary>
        public readonly bool CeilingRefraction;

        /// <summary>
        /// Whether this rung can afford the motion blur (#402) — the velocity pass over the gun and whatever balls
        /// are moving, the four passes over the tiles and the reconstruction over the whole frame. The player's
        /// Settings row is the other half (<c>BS3DGame.MotionBlurActive</c> is the two together).
        /// <para>
        /// <b>Measured</b> on the reference desktop at 3840×1600 on Paroxysm, inside ONE process: <c>mbflip=4</c> turns
        /// it off and on every four seconds and the <c>[fps]</c> lines are split by state, because the machine was
        /// shared with other sessions' renders all afternoon and separate runs drifted by a factor of two. With the
        /// barrel sweeping (the velocity pass and the reconstruction both working): <c>Low</c> +0.42 ms, <c>Medium</c>
        /// +1.14 and +3.51 on two passes, <c>High</c> −0.30 and −0.35 (noise). Leaned in and sweeping, where the whole
        /// frame reconstructs: +0.50, +4.11, +1.29. Nothing moving: +0.54, −0.97, +1.05. So about a millisecond on a
        /// wide part, spent where something moves; the Medium figures are the least stable of the set.
        /// </para>
        /// <para>
        /// <b><c>Low</c> gives it up</b> (#298's rule, a tier drops effects): the rung exists for a machine like the
        /// reference APU, where it is short already, and the APU has not been measured — a wide part's millisecond is
        /// several there if #540's multisample ratio is any guide. <c>Medium</c> keeps it, <c>High</c> and
        /// <c>Ultra</c> carry it.
        /// </para>
        /// </summary>
        public readonly bool MotionBlur;

        public QualityPreset(int supersampleFactor, float facadeGrainStrength, float windowFrameWidth, int cityRadiusBlocks,
            int msaaSamples, int shadowMapCap, bool ceilingRefraction, bool motionBlur, int shadowMapScale = 1)
        {
            ShadowMapScale = shadowMapScale;
            CeilingRefraction = ceilingRefraction;
            MotionBlur = motionBlur;
            SupersampleFactor = supersampleFactor;
            FacadeGrainStrength = facadeGrainStrength;
            WindowFrameWidth = windowFrameWidth;
            CityRadiusBlocks = cityRadiusBlocks;
            MsaaSamples = msaaSamples;
            ShadowMapCap = shadowMapCap;
        }

        /// <summary>
        /// The four tiers, indexed by <see cref="QualityLevel"/>. The two city figures at <c>High</c> and
        /// <c>Medium</c> are <see cref="Prazsky.Core.Render.CitySceneConfig"/>'s own defaults restated, so
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
            //
            //⚠ THIS RUNG USED TO BE THE CITY'S TWO DIALS AND LITERALLY NOTHING ELSE, which is the hole #298
            //was opened on: measured on the machine the ladder exists for, it was worth 0.90-0.93 ms in the
            //two city scenes and NOTHING at all in the other thirteen (mountain +0.02 ms, cavern +0.04,
            //meadow -0.02 — noise, to two decimal places, on 70 s medians). It read as three rungs and was
            //two for most of the game, so a machine that could not hold Medium had nowhere to go — and two of
            //five shipped levels measured were in exactly that position.
            //
            //It is now a real rung, and everything in it reaches EVERY scene:
            //  · 2 multisample samples instead of 8 (this array)
            //  · the scene extras and the stone cap's fine relief, which ApplyQuality now gives up HERE
            //    rather than at Medium — see the note on Medium below for what moved and why
            //  · and the reduced programs the mountain and the cavern grew for it
            //The city's two dials stay, being the only entries that were ever worth anything here.
            new(supersampleFactor: 1, facadeGrainStrength: 0f, windowFrameWidth: 0f, cityRadiusBlocks: 14, msaaSamples: 2, shadowMapCap: 2048, ceilingRefraction: false, motionBlur: false),

            //Medium — 30 FPS on the worst scene. Supersampling is what this STRUCT gives up, and it is the one
            //change that reaches every scene: on the weak machine it is worth 46 to 58 % of the frame
            //(#298), which is the whole ladder.
            //
            //⚠ THIS TABLE IS NOT THE WHOLE TIER, and that is the thing to read before adding anything to it:
            //ApplyQuality also owns SceneRenderer.SceneDetail (the reduced scene programs) and
            //ArenaIsland.SurfaceDetail (the stone cap's coarse relief). Both used to be spent at ANYTHING
            //below High, which is what left nothing between Medium and Low; both are now spent at LOW ALONE,
            //so Medium runs every scene's authored detail and the full stone cap.
            //
            //What that costs Medium is bounded and was checked before it was done, not after: SurfaceDetail
            //measures 0.00-0.05 ms on the weak machine at this rung's own resolution and supersampling
            //(#298's own figure), and SceneDetail reached only the forest and the dream, neither of which is
            //among the levels that fail to hold Medium there. So the rung that was already over budget on
            //that hardware did not get dearer where it was hurting.
            //
            //4 samples rather than the pipeline's 8 since #540: on the weak machine the step is 0.65-1.38 ms
            //(see MsaaSamples), where the desktop had priced it at nothing and it had been left at 8 for that.
            new(supersampleFactor: 1, facadeGrainStrength: 0.018f, windowFrameWidth: 0.1f, cityRadiusBlocks: 14, msaaSamples: 4, shadowMapCap: 2048, ceilingRefraction: true, motionBlur: true),

            //High — the look the game was authored at, unchanged.
            new(supersampleFactor: 2, facadeGrainStrength: 0.018f, windowFrameWidth: 0.1f, cityRadiusBlocks: 14, msaaSamples: PostProcessPipeline.MSAA_SAMPLES, shadowMapCap: 0, ceilingRefraction: true, motionBlur: true),

            //Ultra (#484) — High, and the sun shadow map at twice the scene's authored size (8192). The one
            //rung ABOVE the authored look, so it is the player's alone: the probe starts at High and only steps
            //down. It carries nothing else, deliberately: every other entry is already at the authored maximum
            //on High (full scene detail, full stone cap, full city, the ceiling's refraction), and the one dial
            //that could go further — supersampling 3 — is 2.25x High's shaded pixels at 3840x1600, the whole
            //frame's biggest cost bought for relief detail High already resolves. See ShadowMapScale.
            new(supersampleFactor: 2, facadeGrainStrength: 0.018f, windowFrameWidth: 0.1f, cityRadiusBlocks: 14, msaaSamples: PostProcessPipeline.MSAA_SAMPLES, shadowMapCap: 0, ceilingRefraction: true, motionBlur: true, shadowMapScale: 2),
        };
    }
}
