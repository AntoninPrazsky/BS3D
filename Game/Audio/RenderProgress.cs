using System.Threading;

namespace BS3D.Audio
{
    /// <summary>
    /// One streamed render's progress (#464), published by the background thread doing the synthesis and read
    /// by whoever wants to play what is already final. <see cref="Mix"/> is set once, before the step loop
    /// starts — <c>NewMix</c> already knows the whole piece's own length, so a caller can hold a reference to
    /// the (still mostly silent) buffer from the very first frame and read the samples the writer has already
    /// finished with no lock of its own: <see cref="SafeFrames"/> is the one thing that changes after that,
    /// and it only ever grows.
    /// <para>
    /// <b>The one guarantee this class makes is the one a reader needs</b>: once <see cref="SafeFrames"/>
    /// reports N, every frame before N in <see cref="Mix"/> is finished — driven, saturated, and never written
    /// to again. That is <see cref="Publish"/>'s whole contract, and it is why the renderer must only ever call
    /// it with a number it has already stopped writing behind (see <c>ProceduralMusic.LOOKBACK_STEPS</c>).
    /// Nothing here makes an element write visible to a reader through anything but the published count — a
    /// reader that peeked past it would be racing the writer, which is exactly the bug a lock-free design like
    /// this one has to not have.
    /// </para>
    /// </summary>
    internal sealed class RenderProgress
    {
        private int _safeFrames;
        private bool _cancelled;

        /// <summary>
        /// The whole piece's own buffer, sized once to its final length and never replaced or resized — set by
        /// the renderer before its step loop starts, so this reference is good from the caller's very first
        /// look at it even though most of it is still silence.
        /// </summary>
        public float[] Mix;

        /// <summary>
        /// How many FRAMES (interleaved stereo pairs) from the start of <see cref="Mix"/> are finished and
        /// safe to read — 0 until the renderer's first <see cref="Publish"/>. A plain volatile read rather
        /// than a lock: one writer, any number of readers, and a stale read only ever under-reports what is
        /// actually ready, never over-reports it.
        /// </summary>
        public int SafeFrames => Volatile.Read(ref _safeFrames);

        /// <summary>
        /// Called by the renderer as it goes, and once more with the whole piece's frame count when it is
        /// done. Monotonic by construction — the renderer only ever computes this forward from the step it is
        /// on — so this does not defend against a caller publishing backwards; that would be the renderer's
        /// own bug, not a race this class exists to close.
        /// </summary>
        public void Publish(int safeFrames) => Volatile.Write(ref _safeFrames, safeFrames);

        /// <summary>
        /// Set by the reader that no longer wants this render (#464 — the About page's Next, or the page being
        /// left): the renderer reads it once a bar, at the publish it would have made there, and unwinds through
        /// <see cref="System.OperationCanceledException"/>. A bar of synthesis is milliseconds of work, so a
        /// cancelled render is gone before the one replacing it has published anything. Volatile for the reason
        /// the count is; one-way, since a render that has unwound cannot be resumed.
        /// </summary>
        public bool Cancelled => Volatile.Read(ref _cancelled);

        public void Cancel() => Volatile.Write(ref _cancelled, true);
    }
}
