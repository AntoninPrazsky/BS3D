using BS3D.Audio;
using BS3D.Screens;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace BS3D
{
    /// <summary>
    /// The <b>verbs the settings page invokes</b> — the cyclers and the toggles, plus the volume plumbing
    /// they drive. Each is named for what the player asked for rather than for how it is done, so the page
    /// reads as a list of choices; the matching read-only surface is in <c>BS3DGame.Menu.cs</c> with the rest
    /// of what a page may ask.
    /// </summary>
    /// <remarks>
    /// Every one of them ends by refreshing the page, because a setting can be changed from the keyboard as
    /// well as from its own row and the row has to agree with the state either way. Split out of
    /// <c>BS3DGame.cs</c> in #71.
    /// </remarks>
    public partial class BS3DGame
    {
        /// <summary>
        /// Steps the brightness up to the next rung and wraps from the top to the bottom (#711, see
        /// <see cref="BrightnessLadder"/>). A command-line exposure can start anywhere (<c>exposure=1.0</c> is 91 %), and
        /// from there the next click lands on 100, so the row is back on its ladder after one.
        /// </summary>
        internal void CycleExposure() => SetBrightness(BrightnessLadder.NextAbove(BrightnessPercent));

        /// <summary>The Brightness row's − and + (#1151): a rung dimmer or brighter, held at the ladder's ends.</summary>
        internal void StepExposure(int direction) => SetBrightness(RowStep.OnRungs(BrightnessLadder.Rungs, BrightnessPercent, direction));

        internal bool CanStepExposure(int direction) =>
            RowStep.OnRungs(BrightnessLadder.Rungs, BrightnessPercent, direction) != BrightnessPercent;

        private void SetBrightness(int percent)
        {
            _exposure = BrightnessLadder.ExposureOf(percent);

            //The Potato path (#789) has no resolve: each of its effects takes the exposure itself
            if (PotatoPath) ApplyPotatoExposure();
            else _pipeline.Exposure = _exposure;

            _settings.Exposure = _exposure;
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Steps the aim dial up a rung and wraps at the top (#384) — upwards like the exposure and unlike the
        /// volumes, because a sensitivity has no direction a player is more likely to want: the volume rows go
        /// down since the reason to click one is almost always "quieter", and there is no such lean here.
        /// <para>
        /// Nothing is applied to anything: the aim path reads <see cref="MouseSensitivity"/> per frame (see its
        /// own remarks), so this row takes effect on the very next frame — including in a level standing
        /// paused behind this page.
        /// </para>
        /// </summary>
        internal void CycleSensitivity() => SetSensitivity(NextSensitivityRung(_mouseSensitivity));

        /// <summary>The lean's own rung (#497), walked exactly as <see cref="CycleSensitivity"/> walks the general one.</summary>
        internal void CycleAimSensitivity() => SetAimSensitivity(NextSensitivityRung(_aimSensitivity));

        /// <summary>The touch screen's rung (#1051), walked as <see cref="CycleSensitivity"/> walks the mouse's.</summary>
        internal void CycleTouchSensitivity() => SetTouchSensitivity(NextSensitivityRung(_touchSensitivity));

        //The three sensitivity rows' − and + (#1151): a rung slower or faster, held at the ladder's ends
        internal void StepSensitivity(int direction) => SetSensitivity(RowStep.OnRungs(SensitivityLadder.Rungs, _mouseSensitivity, direction));
        internal void StepAimSensitivity(int direction) => SetAimSensitivity(RowStep.OnRungs(SensitivityLadder.Rungs, _aimSensitivity, direction));
        internal void StepTouchSensitivity(int direction) => SetTouchSensitivity(RowStep.OnRungs(SensitivityLadder.Rungs, _touchSensitivity, direction));
        internal bool CanStepSensitivity(int direction) => RowStep.OnRungs(SensitivityLadder.Rungs, _mouseSensitivity, direction) != _mouseSensitivity;
        internal bool CanStepAimSensitivity(int direction) => RowStep.OnRungs(SensitivityLadder.Rungs, _aimSensitivity, direction) != _aimSensitivity;
        internal bool CanStepTouchSensitivity(int direction) => RowStep.OnRungs(SensitivityLadder.Rungs, _touchSensitivity, direction) != _touchSensitivity;

        /// <summary>
        /// The rung after <paramref name="rung"/>, wrapping at the top. -1 cannot happen while the constructor snaps the
        /// file's value onto the ladder, and +1 on it is still the first rung, so an unfindable value walks to the bottom
        /// rather than throwing.
        /// </summary>
        private static float NextSensitivityRung(float rung)
        {
            ReadOnlySpan<float> rungs = SensitivityLadder.Rungs;
            return rungs[(rungs.IndexOf(rung) + 1) % rungs.Length];
        }

        private void SetSensitivity(float rung)
        {
            _mouseSensitivity = rung;

            _settings.Sensitivity = _mouseSensitivity;
            SaveSettings();

            _settingsPage.Refresh();
        }

        private void SetAimSensitivity(float rung)
        {
            _aimSensitivity = rung;

            _settings.AimSensitivity = _aimSensitivity;
            SaveSettings();

            _settingsPage.Refresh();
        }

        private void SetTouchSensitivity(float rung)
        {
            _touchSensitivity = rung;

            _settings.TouchSensitivity = _touchSensitivity;
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>Precise aim between a toggle and a hold (#1054).</summary>
        internal void TogglePreciseAimMode()
        {
            _settings.PreciseAimToggle = !_settings.PreciseAimToggle;
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>Swaps the touch screen's two sides (#1050).</summary>
        internal void ToggleTouchSides()
        {
            _settings.TouchAimOnRight = !_settings.TouchAimOnRight;
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// The rung nearest <paramref name="stored"/> <b>by ratio</b>, which is the only comparison that means
        /// anything on a ladder whose steps are ratios. The general form rather than an example, because the
        /// two rules differ everywhere and always the same way: <b>by difference the boundary between two rungs
        /// is their arithmetic mean, by ratio it is their geometric mean, and the geometric mean is never the
        /// larger</b> — so a difference rule drags every in-between value down a rung. Between 2 and 3 those
        /// boundaries are 2.5 and 2.449; a stored 2.47 is 1.235× the one rung and 0.823× the other, so 3 is the
        /// nearer feel and only the ratio rule says so. Anything unusable (zero, negative, NaN out of a
        /// corrupted file) comes back as the shipped 1.
        /// </summary>
        private static float NearestSensitivityRung(float stored)
        {
            if (!(stored > 0f)) return 1f;   //written against NaN, which fails every ordinary comparison

            float best = 1f;
            float bestDistance = float.MaxValue;

            foreach (float rung in SensitivityLadder.Rungs)
            {
                float distance = MathF.Abs(MathF.Log(stored / rung));

                if (distance < bestDistance) { bestDistance = distance; best = rung; }
            }

            return best;
        }

        internal void CycleSkyDome() => ChooseSkyDome((byte)(_skyDome == SKY_DOME_COUNT ? 1 : _skyDome + 1));

        /// <summary>The Sky row's − and + (#1151): the dome before or after, held at the first and the last.</summary>
        internal void StepSkyDome(int direction) => ChooseSkyDome((byte)RowStep.InRange(_skyDome, 1, SKY_DOME_COUNT, direction));

        internal bool CanStepSkyDome(int direction) => RowStep.InRange(_skyDome, 1, SKY_DOME_COUNT, direction) != _skyDome;

        private void ChooseSkyDome(byte dome)
        {
            SetSkyDome(dome);

            _settings.SkyDome = _skyDome;
            SaveSettings();

            _settingsPage.Refresh();
        }

        //The four volume rows (#46). Each steps its own gain and takes effect where it is made — the music
        //keeps playing under the settings page, so what the row does is heard as it is clicked. They were
        //four copies of the same three lines until #71; the row is what differs.
        //Each row writes its own entry and no other, because `mute` puts the master at zero WITHOUT a click
        //(#354). A stepper that wrote all four would carry a scripted run's silence into the player's file,
        //which is exactly the leak the rule "an argument is applied but never written back" exists to stop.
        //Since #583 the row IS the file's value (EffectiveSettings), with mute held over the master as a
        //flag that the master's own click lifts — so the step is taken from what the player hears.
        internal void CycleMasterVolume()
        {
            _effective.MasterVolume = NextVolume(_effective.MasterVolume);
            OnVolumeRowChanged();
        }

        internal void CycleSfxVolume()
        {
            _effective.SfxVolume = NextVolume(_effective.SfxVolume);
            OnVolumeRowChanged();
        }

        internal void CycleMusicVolume()
        {
            _effective.MusicVolume = NextVolume(_effective.MusicVolume);
            OnVolumeRowChanged();
        }

        internal void CycleAmbienceVolume()
        {
            _effective.AmbienceVolume = NextVolume(_effective.AmbienceVolume);
            OnVolumeRowChanged();
        }

        /// <summary>The pad's own row (#378) — not audio, but cycled and stored exactly like the four above it.</summary>
        internal void CycleRumbleStrength()
        {
            _effective.RumbleStrength = NextVolume(_effective.RumbleStrength);
            OnVolumeRowChanged();
        }

        //The same five rows' − and + (#1151): a quarter quieter or louder (or weaker or stronger), held at Off and at full,
        //where the row's own press wraps from Off to full
        internal void StepMasterVolume(int direction)
        {
            _effective.MasterVolume = RowStep.Gain(_effective.MasterVolume, VOLUME_STEP, direction);
            OnVolumeRowChanged();
        }

        internal void StepSfxVolume(int direction)
        {
            _effective.SfxVolume = RowStep.Gain(_effective.SfxVolume, VOLUME_STEP, direction);
            OnVolumeRowChanged();
        }

        internal void StepMusicVolume(int direction)
        {
            _effective.MusicVolume = RowStep.Gain(_effective.MusicVolume, VOLUME_STEP, direction);
            OnVolumeRowChanged();
        }

        internal void StepAmbienceVolume(int direction)
        {
            _effective.AmbienceVolume = RowStep.Gain(_effective.AmbienceVolume, VOLUME_STEP, direction);
            OnVolumeRowChanged();
        }

        internal void StepRumbleStrength(int direction)
        {
            _effective.RumbleStrength = RowStep.Gain(_effective.RumbleStrength, VOLUME_STEP, direction);
            OnVolumeRowChanged();
        }

        /// <summary>
        /// Toggles the keyboard's lights (#987). <c>UpdateKeyboardLights</c> hands the switch to the backend every frame, which
        /// closes the keyboard (giving it back to Windows) or opens it again off the frame.
        /// </summary>
        internal void ToggleKeyboardLighting()
        {
            _effective.KeyboardLighting = !_effective.KeyboardLighting;

            SaveSettings();

            _settingsPage.Refresh();
        }

        private void OnVolumeRowChanged()
        {
            //The one place rows become gains is the director's (#583), which reads them through the file
            _audioDirector.ApplyVolumes();
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Steps a volume down a quarter and wraps back to full under zero. Downwards, unlike the exposure
        /// ladder, because the reason to click a volume row at all is almost always "quieter" — upwards, the
        /// first click would be a jump to silence. The epsilon is the exposure wrap's: a mute's 0 sits on the
        /// ladder already, so only a step below it wraps.
        /// </summary>
        private static float NextVolume(float current)
        {
            float next = current - VOLUME_STEP;
            return next < -Constants.THOUSANDTH ? 1f : Math.Max(next, 0f);
        }

        internal void ToggleFullscreen()
        {
            _fullscreen = !_fullscreen;
            SetGraphics();

            //A fullscreen switch moves the back buffer between 1600×900 and the display's native resolution — a
            //fill-rate change of several times — so a tier the probe reached for the old size can be wrong for
            //the new one (the neon city goes from ~110 to ~37 FPS on a 3840×1600 panel, the same machine).
            //
            //It used to re-open only when the tier was already BELOW High (#123), which excluded the one tier
            //that most needs re-measuring after the back buffer grows five-fold: High is where the frame is
            //dearest and where 2× supersampling sits on top of it. Nothing in the reasoning above was ever
            //specific to Medium or Low — a High verdict is just as much "this machine's answer for THIS back
            //buffer size". The same shape as #121, where the latch closed over the lightest scene of the
            //session and kept that verdict for the heaviest.
            //
            //ReopenQualityProbe carries the rest: a tier the player set in Settings or on the command line is
            //their decision and is never overridden, and the warm-up lets the new target's first frames settle
            //before the window counts them.
            ReopenQualityProbe();

            _settings.Fullscreen = _fullscreen;
            SaveSettings();

            _settingsPage.Refresh();
        }

        internal void ToggleFpsOverlay()
        {
            _info.Visible = !_info.Visible;

            _settings.FpsOverlay = _info.Visible;
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// The 10-bit colour row (#859): the back buffer's depth where the window's monitor takes 10 bits, applied the
        /// way Fullscreen is, through <see cref="SetGraphics"/> and a device reset. Saved whatever the panel: a player
        /// who turned it off on an 8-bit laptop keeps it off on the 10-bit monitor at home.
        /// </summary>
        internal void ToggleTenBitColour()
        {
            _tenBitWanted = !_tenBitWanted;
            SetGraphics();

            _settings.TenBitColour = _tenBitWanted;
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Switches the frame-rate cap between the monitor's refresh (the default: frames nobody can see cost
        /// only heat) and unlimited (#124). The <c>nocap</c> launch argument seeds the same flag and stays —
        /// the benchmark wants the cap off without a trip through the menus.
        /// <para>
        /// <b>No <c>SetGraphics</c> pass any more (#270).</b> "Capped" used to mean vsync, so the flag reached
        /// the device through <c>SynchronizeWithVerticalRetrace</c> and the <c>PresentationInterval</c> and
        /// flipping it reset the device. The game now presents immediately in every mode and the rate is held
        /// by <see cref="Platform.FrameLimiter"/>, which is handed its target fresh every frame — so this is a
        /// plain field write that takes effect on the next frame, and the device is left alone.
        /// </para>
        /// <para>
        /// Deliberately no <c>ReopenQualityProbe</c>, unlike the fullscreen toggle: the cap changes what the
        /// frame rate is allowed to <i>read</i>, not what a frame costs — uncapping can only raise the
        /// measurement, and capping floors it at the refresh, which the probe's floor sits comfortably under
        /// by construction (<c>SetQualityMinFpsFromRefresh</c>).
        /// </para>
        /// <para>
        /// <b>On a phone the second state is 60 Hz, not Unlimited</b> (#1074, the owner's choice): Android presents with
        /// vsync, so no limit means nothing there, and the row instead asks the display for its 60 Hz mode
        /// (<see cref="Platform.DisplayRefresh.AskRefresh"/>) - a cooler phone and a clean frame where a tier cannot hold
        /// 120 - and for its fastest again on the next click. The display answers within a second, and the refresh watch
        /// (<c>OnWindowScaleChanged</c>) re-derives the floors and the limiter from what it presents, as it does when the
        /// phone itself switches the rate (#1070).
        /// </para>
        /// </summary>
        internal void ToggleFpsLimit()
        {
            if (PhoneTiers.Phone)
            {
                _phoneSixtyHz = !_phoneSixtyHz;
                _settings.PhoneSixtyHz = _phoneSixtyHz;
                Platform.DisplayRefresh.AskRefresh(_phoneSixtyHz ? PhoneTiers.TARGET_FPS : 0);
            }
            else
            {
                _uncappedFps = !_uncappedFps;
                _settings.UncappedFps = _uncappedFps;
            }
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// The Resolution row (#801): the height the 3D is drawn at, one rung of the display's ladder down per click and
        /// round to native again - and on the Raspberry Pi, round to Auto, the build's own choice, before native. Every
        /// rung keeps the display's aspect, so nothing is stretched; the menus and the HUD stay at the display's size.
        /// <para>
        /// A click is the player's answer and is written (#354), native as 0 and Auto as nothing, and it switches off the
        /// Pi's automatic step as Auto quality's row does the desktop's probe (#390). It re-opens the probe like a
        /// fullscreen switch does: a frame at another size costs something else.
        /// </para>
        /// </summary>
        internal void CycleRenderResolution()
        {
            if (_renderLadder.Length == 0) return;

            int current = CurrentRenderRung;
            int next = RenderResolution.Next(_renderLadder, current);

            if (IsResolutionAutomatic)
            {
                //Auto -> native, chosen: the first click from the build's choice is the display's own
                _renderHeight = 0;
                _renderHeightChosen = true;
            }
            else if (ResolutionHasAuto && next == _renderLadder[0])
            {
                //Round the bottom of the ladder back to Auto, which starts native and may step again. Only where there is
                //an Auto: a Windows player's chosen Potato comes round to native, chosen, as every desktop tier does
                _renderHeight = 0;
                _renderHeightChosen = false;
                _resolutionStepped = false;
            }
            else
            {
                _renderHeight = next == _renderLadder[0] ? 0 : next;
                _renderHeightChosen = true;
            }

            ApplyRenderChoice();
        }

        /// <summary>
        /// The Resolution row's − and + (#1151): a rung smaller or larger, held at the smallest and at native. Always the
        /// player's choice, from Auto as well, as the row's own first press from Auto is; Auto itself is reached only by
        /// that press, past the ladder's bottom.
        /// </summary>
        internal void StepRenderResolution(int direction)
        {
            if (_renderLadder.Length == 0) return;

            //The ladder is native first, largest down: OnRungs takes the nearest rung that way whatever the order
            int next = RowStep.OnRungs(_renderLadder, CurrentRenderRung, direction);
            _renderHeight = next == _renderLadder[0] ? 0 : next;
            _renderHeightChosen = true;

            ApplyRenderChoice();
        }

        internal bool CanStepRenderResolution(int direction) =>
            _renderLadder.Length > 0 && RowStep.OnRungs(_renderLadder, CurrentRenderRung, direction) != CurrentRenderRung;

        //The rung the row stands on: the display's own (the ladder's first) when no height is set
        private int CurrentRenderRung => EffectiveRenderHeight > 0 ? EffectiveRenderHeight : _renderLadder[0];

        /// <summary>What every choice of the Resolution row does once the height is set: applied, said, written, and the probe re-opened.</summary>
        private void ApplyRenderChoice()
        {
            ApplyRenderResolution();
            Console.WriteLine($"[resolution] {(IsResolutionAutomatic ? "Auto" : "Chosen")}: the 3D at {_renderSize.X}x{_renderSize.Y}");

            _settings.RenderHeight = _renderHeightChosen ? _renderHeight : null;
            SaveSettings();

            ReopenQualityProbe();
            _mainMenuPage.ClearQualityNotice();
            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the lens's chromatic aberration. A taste setting: zero disables the shader's whole
        /// branch, so Off costs literally nothing. Not a per-frame path — the uniform persists on the
        /// effect, so it is written only here and at load.
        /// </summary>
        internal void ToggleAberration()
        {
            //The Potato path (#789) has no resolve to fringe, and its row shows "(tier)" by an unticked box (#796, #816): nothing to toggle,
            //and the player's stored preference stays what it was
            if (!HasLensLooks) return;

            _effective.Aberration = !_effective.Aberration;
            _pipeline.ChromaticAberration = _effective.Aberration ? CHROMATIC_ABERRATION : 0f;

            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the film grain, the aberration's sibling: zero disables the shader's whole branch,
        /// so Off costs literally nothing. Only the strength is written here — the seed and the pixel
        /// grid go out per frame in ResolveSceneTarget regardless, and are no-ops while disabled.
        /// </summary>
        internal void ToggleGrain()
        {
            //Nothing to toggle on the Potato path, for the aberration's reason above
            if (!HasLensLooks) return;

            _effective.Grain = !_effective.Grain;
            _pipeline.FilmGrain = _effective.Grain ? FILM_GRAIN : 0f;

            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Whether this frame draws the scene's scenery (#808, the owner's verdict of 2026-10-07): the backdrop, the city,
        /// the wood, the weather, what stands on the ground and the shaft under the drain, and the shadows of all of it.
        /// On by default on every desktop tier; turned off on the Scenery row, it leaves the island, the cluster, the gun,
        /// the glass and the sky (or a sky-replacing scene's own background colour), and the Quality row reads "Custom".
        /// Never on the Potato path, which draws none of the desktop's scenery: its own low-poly backdrops
        /// (<see cref="PotatoBackdrop"/>, #829) are drawn by <see cref="BeginPotatoSceneDraw"/> whatever this says (see
        /// <c>BS3DGame.Potato.cs</c>).
        /// </summary>
        internal bool SceneryShown => !PotatoPath && (_settings.Scenery != false || _benchmarking);

        /// <summary>
        /// Whether the clouds stand as volumes this frame (#989): High and Ultra (the finer meshes' switch), a scene that draws
        /// the sky's deck at all, and not the Potato path. Part of the sky, so the Scenery row leaves them, as it leaves the deck.
        /// </summary>
        internal bool CloudVolumesShown => _cloudVolumes != null && _sceneRenderer.FineMeshes && !SceneRenderer.ReplacesSky(_scene);

        /// <summary>
        /// What the Scenery row says. On the Potato path "On (Potato)", ticked and held (#897): the scenes do have their
        /// scenery there, the path's own, and the row cannot turn it off.
        /// </summary>
        internal string SceneryLabel => PotatoPath ? "On (Potato)" : SceneryShown ? "On" : "Off";

        /// <summary>The Scenery row (#808): off is the player's Custom; on is the tier's own look again.</summary>
        internal void ToggleScenery()
        {
            if (PotatoPath) return;

            _settings.Scenery = SceneryShown ? false : null;
            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the motion blur (#402). Nothing to write to the pipeline: the session asks
        /// <see cref="MotionBlurActive"/> each frame and simply opens no velocity pass while it is off, which costs
        /// nothing at all.
        /// </summary>
        internal void ToggleMotionBlur()
        {
            _effective.MotionBlur = !_effective.MotionBlur;

            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles whether a big collapse takes the camera (#290): the flourish stays the game's default and
        /// this is the opt-out for a player who would rather keep shooting.
        /// <para>
        /// It is read where the takeover is <b>decided</b> (<c>GameplayScreen.TryBeginDropCinematic</c>) and
        /// nowhere else, so a cinematic already running is left to finish rather than cut off mid-swing — and
        /// nothing about the drop itself changes either way: the same balls come off the cluster, fall the
        /// same way, and are scored the same. Only the lens stays where it was.
        /// </para>
        /// </summary>
        internal void ToggleDropCinematic()
        {
            _effective.DropCinematic = !_effective.DropCinematic;

            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the tutorial (#189): the tutorial's cards stay the game's default and this is the opt-out
        /// for a player who does not want to be told. Read by the session every frame
        /// (<see cref="IsTutorialEnabled"/>), so a card up when this is turned off from the pause comes down at
        /// once, and turning it back on resumes with the lesson that was up. It touches nothing already taught
        /// — that is the save's record, and Reset progress is what clears it.
        /// </summary>
        internal void ToggleTutorial()
        {
            _effective.Tutorial = !_effective.Tutorial;

            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the intro logo (#621): whether the game opens on the 2D logo or straight on the main menu. The
        /// splash is decided once, when the stack is built at boot, so this changes nothing on screen now — it is
        /// written for the next launch, which is what the row's player expects of a switch about how the game
        /// starts. A run's <c>nosplash</c> is not this row and is not touched by it.
        /// </summary>
        internal void ToggleIntroLogo()
        {
            _effective.IntroLogo = !_effective.IntroLogo;

            SaveSettings();

            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the debug unlock (#349): every level selectable regardless of the star total, for reaching
        /// one to test it without earning the stars first. It overrides <see cref="IsLevelUnlocked"/> and
        /// writes nothing — <c>PlayerProgress</c> is untouched, so turning it off restores the real state
        /// exactly, and it is off again at the next launch.
        /// <para>
        /// The level select is refreshed alongside the settings page. It would pick this up on its own when
        /// next shown — <c>Refresh</c> rewrites every tile's locked state — but doing it here means the two
        /// pages cannot be looked at in the same breath and disagree.
        /// </para>
        /// <para>
        /// <b>Inert in a release build (#854)</b>: a tag build (<see cref="BuildVersion.Release"/> set) draws no
        /// Unlock all row, and the switch refuses here as well, so that no argument, test hook or future caller
        /// can open a shipped campaign through a row the player cannot see. The gate is the build's own word,
        /// not <c>#if DEBUG</c>: the owner plays Release-configuration builds of main, and those keep the row.
        /// </para>
        /// </summary>
        internal void ToggleUnlockAll()
        {
            if (BuildVersion.Release != null) return;

            _unlockAll = !_unlockAll;
            _settingsPage.Refresh();
            _levelSelectPage?.Refresh();
        }
    }
}
