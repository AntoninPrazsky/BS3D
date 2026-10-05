namespace BS3D.Platform
{
    /// <summary>
    /// The quality tier this build is held at, which the player cannot change (#788): none here, in the Windows
    /// build, where the Quality row, the probe and <c>quality=</c> decide as they always have. GamePi brings its own
    /// file of this name answering <see cref="QualityLevel.Potato"/>, the Raspberry Pi's only tier (#785) - a
    /// platform seam like <see cref="CrashDialog"/>'s rather than an <c>#if</c>, so neither build compiles the
    /// other's answer and the Windows one stays what it was.
    /// </summary>
    internal static class QualityLock
    {
        /// <summary>The tier this build is locked to, or null where the player picks.</summary>
        internal static QualityLevel? Tier => null;
    }
}
