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

        /// <summary>Why the tier is held, for the line the run prints when it is.</summary>
        internal static string Why => "this build's only tier";

        /// <summary>The Windows build's <c>potato</c> argument (#808) asks for what this build already is: false,
        /// nothing was switched, and this build's effects are where they always were.</summary>
        internal static bool HoldAtPotato() => false;
    }
}
