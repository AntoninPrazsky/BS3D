namespace BS3D.Platform
{
    /// <summary>
    /// What the sound engine under this build does that the game's code has to follow (#790). Here, the Windows build,
    /// it is XAudio2 (MonoGame WindowsDX): a voice can be placed in 3D before it plays, and has to be, because the
    /// placement writes the voice's frequency ratio and a pitch set before it would be wiped (see
    /// <c>ProceduralAudio.Speak</c>). GamePi brings its own file of this name for OpenAL Soft (MonoGame DesktopGL),
    /// which places a voice only while it holds a source - and it takes one at Play.
    /// </summary>
    internal static class AudioBackend
    {
        /// <summary>
        /// Whether a voice must be placed (<c>Apply3D</c>) after <c>Play</c> rather than before it. False here.
        /// </summary>
        internal static bool PlacesAfterPlay => false;
    }
}
