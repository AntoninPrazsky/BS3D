namespace BS3D.Platform
{
    /// <summary>
    /// Tells the GPU that the depth of the render target being left will not be read again (#801), so a tiling GPU need
    /// not write it out to memory. The Windows build's half of the seam does nothing: only the Raspberry Pi's Potato path
    /// draws its 3D into a target of its own and scales it up, and Direct3D 11 on a desktop GPU is not a tiler.
    /// GamePi's namesake is <c>GamePi/Platform/TargetDiscard.cs</c>.
    /// </summary>
    internal static class TargetDiscard
    {
        /// <summary>Nothing to discard on this platform.</summary>
        internal static void DiscardDepth()
        {
        }
    }
}
