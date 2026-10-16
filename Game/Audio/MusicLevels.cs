namespace BS3D.Audio
{
    /// <summary>
    /// The music's levels in one place (#919): the figures a recording is decoded and played at, in a file of its own so
    /// that <c>OggTrack</c>, <c>GameMusic</c>, the Jukebox's <c>RecordingPlayer</c>, <c>Tools/MusicBake</c> and the
    /// tests compile the same lines (<c>GameMusic</c> itself holds a voice and a device, which a test cannot have).
    /// <para>
    /// <b>A recording is decoded 1.5 dB down and its voice is played that much up.</b> <c>MusicBake</c> brings a theme to
    /// −15 dBFS RMS and keeps every sample under full scale in the PCM it encodes, but Ogg Vorbis is lossy and overshoots:
    /// the shipped set decodes to as much as +0.90 dBFS (<c>pulse-synthpop</c>), 41 960 samples over full scale in all, on
    /// 78 of the 140 recordings, and a sample over full scale has nowhere to go but the rail, which is a run of clipped
    /// samples wherever the music is sparse and peaky (music box, celesta, piano, harp, waltz). Scaling the decoded float
    /// by <see cref="DECODE_HEADROOM"/> before it becomes 16 bits leaves the largest overshoot measured at −0.6 dBFS, and
    /// each voice's volume is its authored level over the same factor (<see cref="THEME_VOICE"/>), so what comes out of the
    /// speakers is what the authored level always meant: <c>decoded × voice = authored</c>, to rounding.
    /// </para>
    /// <para>
    /// <b>Only what is decoded through <c>OggTrack.DecodeMusic</c> is compensated.</b> The sound effects and the fanfare
    /// recordings (peak-normalised to 0.9 by <c>MusicBake --sfx</c>) are decoded at unity and their volumes are not
    /// touched, and the About page's procedural pieces never pass through the decoder, so they keep the authored level
    /// itself (<c>GameMusic.MUSIC_VOLUME</c>).
    /// </para>
    /// </summary>
    internal static class MusicLevels
    {
        /// <summary>
        /// The decode's headroom in dB. The largest overshoot measured on the shipped set is +0.90 dBFS, which 1.5 dB brings
        /// to −0.6 dBFS: under full scale with a margin of 0.6 dB, where −1.0 dB left 0.1 dB. The price is a quarter of a
        /// bit of the 16 in the PCM, which the lossy codec's own noise is far above, and it is also how far over full scale a
        /// future bake may overshoot before <c>MusicBake</c> refuses it (<see cref="MAX_CODEC_PEAK"/>).
        /// </summary>
        public const float DECODE_HEADROOM_DB = -1.5f;

        /// <summary>
        /// <see cref="DECODE_HEADROOM_DB"/> as the factor the decoded floats are multiplied by, 10^(−1.5 / 20): written out
        /// because <c>Math.Pow</c> is not a constant and the voice volumes below are. The tests hold the two together.
        /// </summary>
        public const float DECODE_HEADROOM = 0.84139514f;

        /// <summary>
        /// The largest sample the codec may hand back, as a fraction of full scale, and still decode under it once
        /// <see cref="DECODE_HEADROOM"/> is applied: 1 / headroom, +1.5 dBFS. <c>MusicBake</c> refuses a recording over it.
        /// </summary>
        public const float MAX_CODEC_PEAK = 1f / DECODE_HEADROOM;

        /// <summary>
        /// The authored level of the level's theme, under the effects (<c>GameMusic.MUSIC_VOLUME</c>, which says how it
        /// came to be 0.5). The loudness a listener hears, not the number a voice is given: see <see cref="THEME_VOICE"/>.
        /// </summary>
        public const float THEME_LEVEL = 0.5f;

        /// <summary>The front end's authored level (<c>GameMusic.MENU_VOLUME</c>).</summary>
        public const float MENU_LEVEL = 0.29f;

        /// <summary>The pause's authored level (<c>GameMusic.PAUSE_VOLUME</c>): the lobby's.</summary>
        public const float PAUSE_LEVEL = MENU_LEVEL;

        /// <summary>
        /// The theme's voice volume, for a recording decoded with the headroom: <see cref="THEME_LEVEL"/> over it, 0.594.
        /// The Jukebox's recordings play at it too.
        /// </summary>
        public const float THEME_VOICE = THEME_LEVEL / DECODE_HEADROOM;

        /// <summary>The front end's voice volume: <see cref="MENU_LEVEL"/> over the headroom.</summary>
        public const float MENU_VOICE = MENU_LEVEL / DECODE_HEADROOM;

        /// <summary>The pause's voice volume: <see cref="PAUSE_LEVEL"/> over the headroom.</summary>
        public const float PAUSE_VOICE = PAUSE_LEVEL / DECODE_HEADROOM;
    }
}
