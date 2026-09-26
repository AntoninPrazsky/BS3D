namespace BS3D
{
    /// <summary>
    /// The value this run uses for each of the ten rows that are <b>only</b> the player's answer — the five
    /// levels of the mix (master, effects, music, atmosphere, the pad's rumble) and the five taste toggles
    /// (aberration, grain, motion blur, the drop cinematic, the tutorial) — read straight off the settings file
    /// with the run's one launch override layered over it (#583).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it exists: those ten were kept twice.</b> Until #583 each was a field on <c>BS3DGame</c> copied from
    /// <see cref="GameSettings"/> in the constructor, and every settings verb wrote both copies. Nothing but a
    /// verb ever changed one without the other, so the pair agreed — by discipline rather than by construction,
    /// and a new row had to remember both halves. Here there is one copy, the file's, and this object only says
    /// what is laid over it.
    /// </para>
    /// <para>
    /// <b>The rule <see cref="GameSettings"/> states still holds, and it holds structurally: a value that arrived
    /// from <see cref="Program"/> is applied but never written back.</b> The one launch argument that reaches any
    /// of these rows is <c>mute</c>, which puts the master at zero <i>without</i> a click (#354). It is kept here
    /// as a flag and never stored into the file, so a scripted run that clicks some other row saves the player's
    /// own master, not the run's silence. Clicking the master row itself is the player answering it: the step is
    /// taken from what they hear (zero, which the ladder wraps to full), stored, and the flag cleared — exactly
    /// what the mirrored field did.
    /// </para>
    /// <para>
    /// The rows that have a launch argument of their own and a run value shaped differently from the file's
    /// (the exposure, the sky, fullscreen, the frame-rate cap, the quality tier) stay as the host's own fields for
    /// now: each is resolved from argument-or-file once, and their verbs already write the file alone.
    /// </para>
    /// </remarks>
    internal sealed class EffectiveSettings
    {
        private readonly GameSettings _file;

        //The "mute" argument (#354), until the player clicks the master row
        private bool _muted;

        /// <summary>Layers the run's overrides over the player's file. <paramref name="mute"/> is the <c>mute</c> launch argument.</summary>
        internal EffectiveSettings(GameSettings file, bool mute)
        {
            _file = file;
            _muted = mute;
        }

        /// <summary>The master level this run plays at: zero while <c>mute</c> holds it, the player's row otherwise.</summary>
        internal float MasterVolume
        {
            get => _muted ? 0f : _file.MasterVolume;

            //A click on the row is the player's answer, so it lifts the run's mute along with storing the value
            set
            {
                _muted = false;
                _file.MasterVolume = value;
            }
        }

        internal float SfxVolume { get => _file.SfxVolume; set => _file.SfxVolume = value; }

        internal float MusicVolume { get => _file.MusicVolume; set => _file.MusicVolume = value; }

        internal float AmbienceVolume { get => _file.AmbienceVolume; set => _file.AmbienceVolume = value; }

        /// <summary>The pad's row (#378), on the volumes' ladder.</summary>
        internal float RumbleStrength { get => _file.RumbleStrength; set => _file.RumbleStrength = value; }

        internal bool Aberration { get => _file.Aberration; set => _file.Aberration = value; }

        internal bool Grain { get => _file.Grain; set => _file.Grain = value; }

        /// <summary>The player's half of the motion blur (#402); the tier's half is <c>QualityPreset.MotionBlur</c>.</summary>
        internal bool MotionBlur { get => _file.MotionBlur; set => _file.MotionBlur = value; }

        /// <summary>Whether a big collapse still takes the camera (#290).</summary>
        internal bool DropCinematic { get => _file.DropCinematic; set => _file.DropCinematic = value; }

        /// <summary>Whether the first chapter's cards are shown (#189). What has been taught is the save's, never this.</summary>
        internal bool Tutorial { get => _file.Tutorial; set => _file.Tutorial = value; }
    }
}
