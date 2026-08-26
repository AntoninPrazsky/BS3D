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
        internal void CycleExposure()
        {
            _exposure += EXPOSURE_STEP;

            //A command-line exposure can start anywhere, so this wraps on the ceiling rather than assuming
            //the value is already on the ladder
            if (_exposure > EXPOSURE_MAX + Constants.THOUSANDTH) _exposure = EXPOSURE_MIN;

            _pipeline.Exposure = _exposure;

            _settingsPage.Refresh();
        }

        internal void CycleSkyDome()
        {
            SetSkyDome((byte)(_skyDome == SKY_DOME_COUNT ? 1 : _skyDome + 1));
            _settingsPage.Refresh();
        }

        //The four volume rows (#46). Each steps its own gain and takes effect where it is made — the music
        //keeps playing under the settings page, so what the row does is heard as it is clicked. They were
        //four copies of the same three lines until #71; the row is what differs, and a row is a field.
        internal void CycleMasterVolume() => CycleVolume(ref _masterVolume);

        internal void CycleSfxVolume() => CycleVolume(ref _sfxVolume);

        internal void CycleMusicVolume() => CycleVolume(ref _musicVolume);

        internal void CycleAmbienceVolume() => CycleVolume(ref _ambienceVolume);

        private void CycleVolume(ref float volume)
        {
            volume = NextVolume(volume);
            ApplyVolumes();
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

        /// <summary>
        /// The one place the player's gains reach the audio: effects and music each take master times their
        /// own row, so the two subsystems cannot disagree about what the master row means.
        /// </summary>
        private void ApplyVolumes()
        {
            _audio.Gain = _masterVolume * _sfxVolume;
            _music.Gain = _masterVolume * _musicVolume;

            //The beds have a row of their own: how much atmosphere sits under the music is a taste, and
            //chaining it to the effects would turn the shot down with it.
            _ambience.Gain = _masterVolume * _ambienceVolume;
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

            _settingsPage.Refresh();
        }

        internal void ToggleFpsOverlay()
        {
            _info.Visible = !_info.Visible;
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
        /// </summary>
        internal void ToggleFpsLimit()
        {
            _uncappedFps = !_uncappedFps;
            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the lens's chromatic aberration. A taste setting: zero disables the shader's whole
        /// branch, so Off costs literally nothing. Not a per-frame path — the uniform persists on the
        /// effect, so it is written only here and at load.
        /// </summary>
        internal void ToggleAberration()
        {
            _aberration = !_aberration;
            _pipeline.ChromaticAberration = _aberration ? CHROMATIC_ABERRATION : 0f;
            _settingsPage.Refresh();
        }

        /// <summary>
        /// Toggles the film grain, the aberration's sibling: zero disables the shader's whole branch,
        /// so Off costs literally nothing. Only the strength is written here — the seed and the pixel
        /// grid go out per frame in ResolveSceneTarget regardless, and are no-ops while disabled.
        /// </summary>
        internal void ToggleGrain()
        {
            _grain = !_grain;
            _pipeline.FilmGrain = _grain ? FILM_GRAIN : 0f;
            _settingsPage.Refresh();
        }
    }
}
