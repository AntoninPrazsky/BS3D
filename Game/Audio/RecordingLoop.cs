using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The arithmetic of a recording that plays round and round (#899): where the next chunk of it starts and how long it
    /// is, and where the position clock stands after the end. <see cref="RecordingPlayer"/> queues a recording in
    /// half-second chunks, and once the last frame is queued the next chunk is the first again, so the voice never runs dry
    /// at the seam; the clock the visualizer reads wraps with it. Plain integers and doubles, no device, so the tests
    /// walk the seam without one.
    /// </summary>
    internal static class RecordingLoop
    {
        /// <summary>
        /// How many frames the chunk starting at <paramref name="submittedFrames"/> takes: a whole chunk, or what is left of
        /// the recording when that is less - a chunk never reads past the last frame, the next one starts over at the first.
        /// </summary>
        public static int ChunkFrames(int submittedFrames, int totalFrames, int chunkFrames) =>
            Math.Min(totalFrames - submittedFrames, chunkFrames);

        /// <summary>
        /// Where the chunk after the one of <paramref name="count"/> frames from <paramref name="submittedFrames"/> starts:
        /// right behind it, or at the recording's first frame once the last has been queued.
        /// </summary>
        public static int NextStart(int submittedFrames, int count, int totalFrames)
        {
            int next = submittedFrames + count;
            return next >= totalFrames ? 0 : next;
        }

        /// <summary>
        /// The playing position, 0 up to but not including <paramref name="durationSeconds"/>: a position at or past the end
        /// is that much into the next round, however far past (a frame that hitched for longer than the recording is long
        /// still lands inside it). A recording with no length, and a position before the start, have no position but the start.
        /// </summary>
        public static double WrapPosition(double seconds, double durationSeconds) =>
            durationSeconds > 0 && seconds > 0 ? seconds % durationSeconds : 0;
    }
}
