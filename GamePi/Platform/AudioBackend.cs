namespace BS3D.Platform
{
    /// <summary>
    /// GamePi's <c>Game/Platform/AudioBackend.cs</c>: the sound engine is OpenAL Soft, through MonoGame DesktopGL (#790).
    /// A <c>SoundEffectInstance</c> holds an OpenAL source only while it plays: <c>Apply3D</c> on a stopped one has
    /// nowhere to write and does nothing, and <c>Play</c> takes a fresh source and places it from the instance's Pan -
    /// the centre. So every positional sound, placed before Play as the Windows build must, played centred (read out of
    /// MonoGame 3.8.5's <c>SoundEffectInstance.PlatformApply3D</c> and <c>PlatformPlay</c>). Here the placement comes after
    /// Play; OpenAL's placement writes no pitch, so a pitch set before Play survives it.
    /// </summary>
    internal static class AudioBackend
    {
        /// <summary>Whether a voice must be placed after <c>Play</c> rather than before it. True: see the class doc.</summary>
        internal static bool PlacesAfterPlay => true;
    }
}
