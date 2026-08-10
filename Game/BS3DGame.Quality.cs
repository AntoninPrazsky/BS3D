using BS3D.Platform;
using Prazsky.Core.Render;
using System;

namespace BS3D
{
    /// <summary>
    /// The <b>adaptive-quality probe</b> and the tier it settles on: the frame-rate floor taken from the
    /// monitor the window is actually on, the warmup that keeps a cold shader cache from reading as a slow
    /// machine, the two latches that stop it oscillating, and what a tier actually changes.
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
        /// function of the frame rate, so on the slow hardware this exists for it would wait the longest.
        /// </summary>
        private const float QUALITY_WARMUP_SECONDS = 1.5f;

        /// <summary>How long a verdict is averaged over. Long enough that a single hitched frame cannot cause one.</summary>
        private const float QUALITY_WINDOW_SECONDS = 1.5f;

        /// <summary>
        /// Below this, the frame rate is judged bad enough to be worth spending image quality on. Derived from
        /// the display's refresh at startup (see <see cref="SetQualityMinFpsFromRefresh"/>), so it tracks the
        /// monitor the player is actually on rather than a single fixed floor. Comfortably under that refresh,
        /// so a vsync-capped machine (the normal case) never trips it — 75 Hz reads as 75, not as "only just
        /// enough".
        /// </summary>
        private const float DEFAULT_QUALITY_MIN_FPS = 45f;

        /// <summary>
        /// The probe asks for this fraction of the display's refresh. The floor is the refresh <i>minus</i> this
        /// margin: 75 Hz → 67.5, 60 Hz → 54, 144 Hz → 129.6. The margin keeps a vsync-capped machine from
        /// tripping the probe on the rounding of its own cap.
        /// </summary>
        private const float QUALITY_REFRESH_MARGIN = 0.1f;

        /// <summary>
        /// A sanity floor on the refresh-derived target, for an adapter that reports nothing sensible (headless,
        /// a remote session). The number is the old fixed floor — below any common refresh — so behaviour there
        /// is unchanged.
        /// </summary>
        private const float QUALITY_MIN_FPS_FLOOR = 45f;

        #endregion

        /// <summary>
        /// Sets the probe's frame-rate floor from the display's refresh, less <see cref="QUALITY_REFRESH_MARGIN"/>
        /// so a vsync-capped machine does not trip it on the rounding of its own cap. The floor is clamped to
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
                //it is the player alt-tabbing away". That was wrong by a factor of two on the real thing: the
                //verdict lands about three seconds in, on the MENU, which has no cluster in it, while the
                //heaviest thing the game draws is a level — and the shipped set runs from 225 balls to 959.
                //Onion cleared the menu at High and then played the whole level at 37.5 FPS on a 75 Hz panel,
                //exactly half refresh, with nothing left watching to take it down to the Medium that holds 75.
                _qualitySettled = true;
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
            //unarmed, the very next window would measure that hitch and step again on the strength of it.
            _qualityWarmupLeft = QUALITY_WARMUP_SECONDS;

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
        }

        /// <summary>
        /// Tells the player what was changed and where to change it back. Once per run, on the main menu —
        /// which is where they will see it: the verdict usually lands about three seconds in, on the front end,
        /// but a level's own verdict lands while they are playing and the notice waits on the menu for them.
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
            //experiment that was rejected and never existed in the code. See SceneRenderer.TerrainDetail for
            //the measurement, and why it is one switch over both extras rather than a dial per feature.
            //Null until BuildScene has run, exactly like the city above.
            if (_sceneRenderer != null) _sceneRenderer.TerrainDetail = quality == QualityLevel.High ? 1f : 0f;

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
