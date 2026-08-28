using BS3D.Platform;
using Prazsky.Core.Render;
using System;

namespace BS3D
{
    /// <summary>
    /// The <b>adaptive-quality probe</b> and the tier it settles on: the frame-rate floor taken from the
    /// monitor the window is actually on, the warmup that skips the opening hitch, the <b>ramp gate</b> that
    /// holds the verdict until the frame rate has stopped climbing (a GPU spinning its clocks up reads slow),
    /// the two latches that stop it oscillating, and what a tier actually changes.
    /// </summary>
    /// <remarks>
    /// Split out of <c>BS3DGame.cs</c> in #71, where it was a region nested inside the menu's — which is what
    /// the file outgrowing its own map looks like. The Win32 side of the refresh reading left with it, to
    /// <see cref="DisplayRefresh"/>.
    /// </remarks>
    public partial class BS3DGame
    {
        #region Adaptive quality

        /// <summary>
        /// Which bundle of detail the frame is being drawn at, and the setting the player sees. It starts at
        /// <see cref="QualityLevel.High"/> — the look the game is authored at — and only ever comes down, either
        /// because the player asked or because <see cref="TuneQualityToFrameRate"/> measured this machine.
        /// </summary>
        private QualityLevel _quality = QualityLevel.High;

        internal QualityLevel Quality => _quality;

        /// <summary>
        /// True once the quality tier is not to be touched again: the player named one on the command line, the
        /// player set one in Settings, the machine proved fast enough, or there is nothing left to lower. It is a
        /// one-way latch on purpose — a dial that keeps moving under the player is worse than one that is merely
        /// wrong once.
        /// </summary>
        private bool _qualitySettled;

        /// <summary>
        /// Whether the tier was fixed by the player (the command line or the Settings row) rather than reached by
        /// the probe. A player-fixed tier is the player's decision and is never re-measured; a probe-reached one
        /// is only this machine's answer for <i>this</i> back-buffer size, so a fullscreen switch — which moves
        /// the back buffer from 1600×900 to the display's native resolution and back, a fill-rate change of
        /// several times — re-opens it (see <see cref="ToggleFullscreen"/>).
        /// </summary>
        private bool _qualityPinnedByPlayer;

        private float _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;
        private float _qualityWindowSeconds;
        private int _qualityWindowFrames;

        /// <summary>
        /// The frame rate the previous verdict window came out at, or 0 before there is one. The downward step
        /// is gated on this: a window more than <see cref="QUALITY_RAMP_FRACTION"/> faster than the one before
        /// it is a GPU still spinning up rather than the machine's steady answer, so it only rolls the
        /// measurement forward. Zeroed whenever a fresh measurement phase begins — a tier step re-arming the
        /// warm-up, or the probe being re-opened.
        /// </summary>
        private float _qualityPrevWindowFps;

        /// <summary>
        /// The frame rate below which the probe spends image quality. Derived from the display's refresh rather
        /// than fixed, so the target tracks the monitor the player is actually on: a 75 Hz panel wants ~75, a
        /// 60 Hz one ~60, a 144 Hz one ~144. The probe settles when the machine reaches <see cref="QUALITY_REFRESH_FRACTION"/>
        /// of it, not when it merely clears 45 — the old fixed floor was tuned to a 60 Hz laptop and left a fast
        /// card on a 75 Hz panel pinned to High at 37 FPS because 37 was never going to clear a verdict it was
        /// never measured against (a windowed run had settled the latch first).
        /// </summary>
        private float _qualityMinFps = DEFAULT_QUALITY_MIN_FPS;

        /// <summary>
        /// Ignored before this much of the run has passed. The opening frames are shader compiles, the first
        /// touch of every render target and the window settling, and none of them are what this machine costs
        /// to draw. Counted in <b>seconds</b> rather than frames deliberately: a fixed frame count is itself a
        /// function of the frame rate, so on the slow hardware this exists for it would wait the longest. It no
        /// longer has to cover the GPU's clock ramp — that is the ramp gate's job (<see cref="QUALITY_RAMP_FRACTION"/>),
        /// because the ramp is not a fixed length and this is.
        /// </summary>
        private const float QUALITY_WARMUP_SECONDS = 1.5f;

        /// <summary>How long a verdict is averaged over. Long enough that a single hitched frame cannot cause one.</summary>
        private const float QUALITY_WINDOW_SECONDS = 1.5f;

        /// <summary>
        /// Below this, the frame rate is judged bad enough to be worth spending image quality on. Derived from
        /// the display's refresh at startup (see <see cref="SetQualityMinFpsFromRefresh"/>), so it tracks the
        /// monitor the player is actually on rather than a single fixed floor. Comfortably under that refresh,
        /// so a limiter-capped machine (the normal case) never trips it — a 75 Hz panel reads 78, not as "only
        /// just enough". This said "vsync-capped" until #270 replaced the vsync with <see cref="Platform.FrameLimiter"/>,
        /// whose target sits a few per cent <i>above</i> the refresh and so only widens the gap.
        /// </summary>
        private const float DEFAULT_QUALITY_MIN_FPS = 45f;

        /// <summary>
        /// The probe asks for this fraction of the display's refresh. The floor is the refresh <i>minus</i> this
        /// margin: 75 Hz → 67.5, 60 Hz → 54, 144 Hz → 129.6. The margin keeps a limiter-capped machine from
        /// tripping the probe on the jitter of its own cap.
        /// </summary>
        private const float QUALITY_REFRESH_MARGIN = 0.1f;

        /// <summary>
        /// A sanity floor on the refresh-derived target, for an adapter that reports nothing sensible (headless,
        /// a remote session). The number is the old fixed floor — below any common refresh — so behaviour there
        /// is unchanged.
        /// </summary>
        private const float QUALITY_MIN_FPS_FLOOR = 45f;

        /// <summary>
        /// How much faster than the previous verdict window a window may be and still be judged the machine's
        /// steady frame rate rather than a GPU still ramping its clocks. Below this, consecutive windows differ
        /// only by measurement and orbit noise — the plateau; a genuine spin-up climbs far faster (measured at
        /// 10 %+ per window on the desktop this was tuned against, against a plateau noise of about 2 %). The
        /// tier is stepped down only once the frame rate has flattened to within it, so the probe never spends
        /// image quality on a ramp-up window — which a fixed warm-up alone could not prevent, the warm-up being
        /// a fixed length and the ramp not (see <see cref="TuneQualityToFrameRate"/>).
        /// </summary>
        private const float QUALITY_RAMP_FRACTION = 0.03f;

        #endregion

        /// <summary>
        /// Sets the probe's frame-rate floor from the display's refresh, less <see cref="QUALITY_REFRESH_MARGIN"/>
        /// so a limiter-capped machine does not trip it on the jitter of its own cap. The floor is clamped to
        /// <see cref="QUALITY_MIN_FPS_FLOOR"/> so a headless or remote adapter that reports no refresh keeps the
        /// old fixed number rather than settling at zero (which would make every run "fast enough" instantly).
        /// </summary>
        private void SetQualityMinFpsFromRefresh()
        {
            //The window's own monitor, not the primary one: passing null asks a different question on any
            //machine with two monitors, which is #81. The Win32 side of it is DisplayRefresh's (#71); a window
            //that cannot be resolved yet reads as Zero there and falls back to the primary display.
            float refresh = 0f;
            if (DisplayRefresh.TryGetForWindow(Window?.Handle ?? IntPtr.Zero, out int hz)) refresh = hz;
            _qualityMinFps = Math.Max(refresh * (1f - QUALITY_REFRESH_MARGIN), QUALITY_MIN_FPS_FLOOR);

            //The frame limiter's target comes off the SAME reading (#270), so there is one place that answers
            //"what is this monitor's refresh" and the two cannot drift apart. Kept raw rather than floored the
            //way the probe's number is: a floor is right for a verdict about the machine and wrong for a cap,
            //where an adapter reporting nothing must mean "do not limit" and not "limit to 45".
            //TryGetForWindow leaves hz at 0 when it has no answer, which is exactly the "do not limit" value
            _displayRefreshHz = hz;
        }

        /// <summary>
        /// Lowers supersampling on a machine that visibly cannot afford it, measured rather than guessed.
        /// Driven by the <see cref="BackdropScreen"/>, which updates exactly while the front end is what is
        /// being drawn — and the front end is a fair probe on its own: it draws the same city, clouds, glare
        /// and tonemap the game does, at the same factor, and it is the fixed scene cost rather than the ball
        /// count that dominates on the hardware this exists for (#64).
        /// <para>
        /// It notices by <b>timing the frames it is already drawing</b>, not by recognising the adapter. A
        /// name or vendor list is wrong on the first machine nobody tested — plenty of AMD parts are discrete,
        /// plenty of Intel ones now are too — and it cannot see the other reasons a frame is slow: a 4K
        /// display, a laptop on battery, something else eating the GPU.
        /// </para>
        /// <para>
        /// One step per verdict and never upwards. Raising it again on a machine that recovered would put the
        /// player back where they started, and a quality dial that oscillates is worse than one set too low.
        /// </para>
        /// </summary>
        /// <param name="elapsed">The frame's real time, unscaled — a slowed world still costs what it costs.</param>
        /// <param name="where">
        /// What is being judged, for the log line: "menu" or "level". Named by the caller because the probe
        /// cannot see which screen is on top, and the distinction is the whole point of the second caller.
        /// </param>
        internal void TuneQualityToFrameRate(float elapsed, string where)
        {
            if (_qualitySettled) return;

            if (_qualityWarmupLeft > 0f)
            {
                _qualityWarmupLeft -= elapsed;
                return;
            }

            _qualityWindowSeconds += elapsed;
            _qualityWindowFrames++;

            if (_qualityWindowSeconds < QUALITY_WINDOW_SECONDS) return;

            float fps = _qualityWindowFrames / _qualityWindowSeconds;

            _qualityWindowSeconds = 0f;
            _qualityWindowFrames = 0;

            if (fps >= _qualityMinFps)
            {
                //Fast enough for what is on screen NOW. The latch closes here rather than the probe watching
                //for ever — a dial that keeps moving under the player is worse than one merely set wrong once
                //— and it is re-opened deliberately, by whoever changes what a frame costs: the fullscreen
                //switch, and the building of a level (see ReopenQualityProbe).
                //
                //It used to close for the whole session, on the reasoning that "the only thing that could trip
                //it is the player alt-tabbing away". That reasoning is still wrong and the re-open still earns
                //its keep: the verdict lands once the frame rate settles, on the MENU, which has no cluster in
                //it, while the heaviest thing the game draws is a level — and the shipped set runs from 225
                //balls to 959.
                //
                //⚠ The MEASUREMENT this used to cite has been retracted (#209). It read "Onion played the
                //whole level at 37.5 FPS on a 75 Hz panel, exactly half refresh, with nothing left watching to
                //take it down to the Medium that holds 75" — and half refresh for a frame that is nowhere near
                //that dear is #270's vsync signature, not a cost. Re-measured after #270 removed the vsync, on
                //the machine it was reported from (5900X / 6900XT, fullscreen 3840×1600, level loaded): Onion
                //costs 5.96 ms at High, so it holds the refresh with better than twice the headroom, and run
                //un-pinned it now draws NO verdict at all — the probe leaves it on High and it sits flat on the
                //limiter. The re-open stays because its argument stands on the shipped ball counts; what does
                //not stand is the idea that this level ever needed a tier.
                //
                //⚠ AND THAT LAST SENTENCE IS A FACT ABOUT THE DESKTOP, WHICH #298 THEN MEASURED THE OTHER WAY
                //ROUND ON THE MACHINE THIS PROBE EXISTS FOR. On the reference APU (5700U + integrated Radeon,
                //nocap, windowed 1600x900) every shipped level measured misses the panel's 16.1 ms at High by
                //1.4x to 2.4x — Ziggurat 37.9, Ten 39.1, Turbine 32.3, One 26.0, Spring 22.3 — and Medium is
                //worth 46 to 58 % of the frame on the scenes whose pass scales with the pixel count. So the
                //probe and the ladder are load-bearing here even though nothing on the desktop needs them, and
                //neither of those two readings can be quoted for the other machine. See "The quality tier" in
                //docs/game-shell.md for the whole matrix and for the hole it found in the ladder.
                _qualitySettled = true;
                return;
            }

            //Below the floor — but a GPU still spinning its clocks up reads below it too, and stepping the tier
            //on a ramp-up window is the wrong drop this gate exists to stop. Measured on the desktop this was
            //tuned against: the Forest holds High at the 60 FPS vsync cap once warm, yet climbs 46 → 50 → 57 →
            //60 over the first four seconds, and a verdict taken at three read 52 and spent a tier the machine
            //did not need. So a downward step waits until the frame rate has PLATEAUED — a window still more
            //than QUALITY_RAMP_FRACTION faster than the one before it is the ramp, not the machine, and only
            //rolls the measurement forward. The first window has no predecessor (prev is 0), so it always reads
            //as still-climbing: a drop therefore takes at least two windows, never one. The warm-up cannot do
            //this on its own — it is a fixed length and the ramp is not, and the slow hardware this exists for
            //is the slowest to climb.
            if (fps > _qualityPrevWindowFps * (1f + QUALITY_RAMP_FRACTION))
            {
                _qualityPrevWindowFps = fps;
                return;
            }

            //Steps the TIER, not supersampling alone, which is the whole point of #63: on the two city scenes
            //supersampling is only the first of three measured levers, and stepping it alone left the neon city
            //at 30 FPS with 40% still on the table.
            QualityLevel lowered = _quality == QualityLevel.High ? QualityLevel.Medium : QualityLevel.Low;

            //Named by the caller rather than assumed to be the menu, which is what this line used to say: the
            //probe judges a level's frame too now, and which of the two it caught is the first thing a reader
            //of this line wants — a step taken in play means the level is heavier than the front end, which is
            //a fact about the level and not about the machine.
            Console.WriteLine($"[quality] {fps:F0} FPS in the {where} at {_quality} (floor {_qualityMinFps:F0}) — lowering to {lowered}");

            ApplyQuality(lowered);
            ShowQualityNotice(lowered);

            //A tier step resizes the scene target, which hitches a frame or two (it would also rebuild the
            //city if the tiers still differed in radius — since the sort none does, see QualityLevel). Left
            //unarmed, the very next window would measure that hitch and step again on the strength of it. The
            //ramp tracker is cleared with it, so the new tier's first window is judged from a clean slate
            //rather than against the old tier's plateau.
            _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;
            _qualityPrevWindowFps = 0f;

            //Nothing left to give: Low is the bottom of the ladder.
            if (_quality == QualityLevel.Low) _qualitySettled = true;
        }

        /// <summary>
        /// Re-opens the latch so the next window is judged afresh, for a change that makes a frame cost
        /// something other than what the last verdict was measured against. <b>A tier the player chose is never
        /// re-opened</b> — that is their decision, and <see cref="_qualityPinnedByPlayer"/> is what says so.
        /// <para>
        /// Called when a <b>level is built</b>, which is the case the probe could not see at all while it only
        /// ran under the front end: the menu has no cluster in it, the shipped set runs from 225 balls to 959,
        /// and the tier that clears the lightest frame of the session was being kept for the heaviest.
        /// </para>
        /// <para>
        /// Deliberately unconditional on the current tier, unlike the fullscreen switch's own re-open, which
        /// takes it only when the tier is already below <see cref="QualityLevel.High"/>. High is precisely the
        /// tier this case has to be able to catch.
        /// </para>
        /// </summary>
        internal void ReopenQualityProbe()
        {
            if (_qualityPinnedByPlayer) return;

            _qualitySettled = false;
            _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;
            _qualityWindowSeconds = 0f;
            _qualityWindowFrames = 0;
            _qualityPrevWindowFps = 0f;
        }

        /// <summary>
        /// Tells the player what was changed and where to change it back. Once per run, on the main menu —
        /// which is where they will see it: the verdict lands once the frame rate settles, a few seconds in on
        /// the front end (longer on a machine that ramps slowly), but a level's own verdict lands while they are
        /// playing and the notice waits on the menu for them.
        /// </summary>
        private void ShowQualityNotice(QualityLevel quality) => _mainMenuPage.ShowQualityNotice(quality);

        /// <summary>
        /// Steps the quality tier, which is the setting the player sees. Wraps Low → Medium → High → Low.
        /// </summary>
        internal void CycleQuality()
        {
            ApplyQuality(_quality switch { QualityLevel.Low => QualityLevel.Medium, QualityLevel.Medium => QualityLevel.High, _ => QualityLevel.Low });

            //The player has now said what they want, so the adaptive path stops second-guessing them — and the
            //notice about what it did has been answered and goes away. Marked as pinned, so a later fullscreen
            //switch does not re-open the probe and walk the tier back off their choice.
            _qualityPinnedByPlayer = true;
            _qualitySettled = true;

            _mainMenuPage.ClearQualityNotice();
        }

        /// <summary>
        /// The one place the tier changes — <see cref="SetScene"/>'s rule applied to quality: everything a tier
        /// touches is written here and nowhere else, so the adaptive probe, the settings row and the command line
        /// cannot disagree about what a tier means.
        /// <para>
        /// The city's dials are pushed to the shader on every city draw (the renderer holds the config by
        /// reference), so writing them is enough; only the block radius is baked into the generated buildings and
        /// needs the city rebuilt. The renderer itself is <b>not</b> recreated, so its sky palette survives and no
        /// <see cref="ApplySkyLighting"/> is owed here.
        /// </para>
        /// </summary>
        internal void ApplyQuality(QualityLevel quality)
        {
            _quality = quality;

            QualityPreset preset = QualityPreset.Presets[(int)quality];

            _cityConfig.FacadeGrainStrength = preset.FacadeGrainStrength;
            _cityConfig.WindowFrameWidth = preset.WindowFrameWidth;

            //Rebuilt only when the count actually changes: the generator walks a block grid and the instance
            //array is re-uploaded, which is a frame's hitch and not something a tier step should pay for twice.
            //Null until BuildScene has run, which is the case when the command line pins a tier at startup.
            if (_city != null && _cityConfig.RadiusBlocks != preset.CityRadiusBlocks)
            {
                _cityConfig.RadiusBlocks = preset.CityRadiusBlocks;
                _city = new City(seed: CITY_SEED, arenaHalfExtent: ArenaIsland.RADIUS, config: _cityConfig);
            }
            else _cityConfig.RadiusBlocks = preset.CityRadiusBlocks;

            //The forest floor's two expensive extras: the authored look at High, and given up below it. The
            //first thing in this project a tier changes INSIDE a scene shader rather than around it — until
            //now the tier reached the frame only through supersampling and the city's two per-pixel dials, and
            //the one per-scene switch the docs mention (QualityPreset.CloudSlices) belonged to a cloud
            //experiment that was rejected and never existed in the code. See SceneRenderer.SceneDetail for
            //the measurement, and why it is one switch over both extras rather than a dial per feature.
            //Null until BuildScene has run, exactly like the city above.
            //⚠ LOW ALONE since #298, and it used to be everything below High. That was the hole: with the
            //extras already gone at Medium there was nothing left between the two lower rungs but the city's
            //two dials, worth nothing at all in thirteen of the sixteen scenes. Spent here instead, Medium
            //keeps every scene's authored look and Low is where the detail goes — which is also the shape the
            //owner asked for, a tier that drops effects.
            if (_sceneRenderer != null) _sceneRenderer.SceneDetail = quality == QualityLevel.Low ? 0f : 1f;

            //And the arena's stone cap, which is the first thing the tier reaches that is NOT a scene — it is
            //in all fifteen of them and under the gun in every frame of every level, and #151 measured it at
            //88 % of the arena's cost. Reduced, its height field is three relief octaves instead of seven:
            //0.336 ms of a 10.971 ms frame on the reference desktop, the coursed slab joints untouched. See
            //ArenaIsland.SurfaceDetail. Null until BuildScene has run, exactly like the two above.
            //Low alone since #298, for the reason above. Safe to hand back to Medium, and that was checked
            //rather than assumed: at Medium's own resolution and supersampling this measures 0.00-0.05 ms on
            //the weak machine, so returning it costs that rung nothing it can feel.
            if (_island != null) _island.SurfaceDetail = quality == QualityLevel.Low ? 0f : 1f;

            //The samples the scene target carries below High (#298) — the first entry that reaches every scene
            //without touching a pixel count, which is the one thing a tier may never do (see QualityLevel).
            //BEFORE the factor below, because both size or rebuild the same target and the last writer wins:
            //SetSupersampleFactor ends by asking the pipeline for a target, so the sample count has to be
            //standing by then or the rebuild happens twice on every tier step.
            _pipeline.MsaaSamples = preset.MsaaSamples;

            //The tier owns supersampling unless the command line pinned it, which is the one case the tier must
            //not write over — see _supersampleOverride. A tier step still changes everything else it owns, so a
            //pinned factor does not freeze the rest of the ladder.
            SetSupersampleFactor(_supersampleOverride ?? preset.SupersampleFactor);
        }

        /// <summary>
        /// The one place the factor changes: the scene target's size is derived from it, and the tonemap has to
        /// be told how many samples its box filter is averaging.
        /// </summary>
        private void SetSupersampleFactor(int factor)
        {
            _supersampleFactor = Math.Clamp(factor, 1, 4);

            //The pipeline's setter writes the tonemap uniform and recreates the scene target in one move —
            //the factor is the target's size, so changing it is exactly what makes the recreate happen
            _pipeline.SupersampleFactor = _supersampleFactor;

            //The space scene sizes its stars in OUTPUT pixels rather than in texels, so it has to be told the
            //factor too — sized in texels a star would come out four times dimmer on High than on Medium.
            //Guarded only as insurance against a future caller: every path that reaches here today
            //(LoadContent's ApplyQuality, the settings row, the adaptive step) runs after the renderer is
            //constructed. Unlike the settings page below, this is not a live case.
            if (_sceneRenderer != null) _sceneRenderer.SupersampleFactor = _supersampleFactor;

            //And the balls, for the dissolve's dither: its cell is authored in DISPLAY pixels and the shader can
            //only measure the target it draws into, so a cell that is not scaled by the factor is averaged
            //straight back into a smooth fade by the tonemap's box filter — which is the whole effect gone at
            //exactly the quality tier that turns supersampling up. Guarded like the renderer above, and for the
            //same reason: every path that reaches here today runs after the set is constructed.
            if (_balls != null) _balls.SupersampleFactor = _supersampleFactor;

            //Null-conditional because the tier is applied during LoadContent, before the menu pages exist — a
            //command-line quality= reaches here well before there is a settings row to write the value onto.
            _settingsPage?.Refresh();
        }
    }
}
