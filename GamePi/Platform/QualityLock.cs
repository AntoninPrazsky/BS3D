namespace BS3D.Platform
{
    /// <summary>
    /// GamePi's <c>Game/Platform/QualityLock.cs</c>: the Raspberry Pi build is held at
    /// <see cref="QualityLevel.Potato"/>, its only tier, and the player cannot change it (#788, the owner's decision
    /// of 2026-10-05 in #785). The Quality and Auto quality rows read it and do nothing, <c>quality=</c> and
    /// <c>ssaa=</c> are ignored, and the probe never runs.
    /// </summary>
    internal static class QualityLock
    {
        /// <summary>Potato, always.</summary>
        internal static QualityLevel? Tier => QualityLevel.Potato;
    }
}
