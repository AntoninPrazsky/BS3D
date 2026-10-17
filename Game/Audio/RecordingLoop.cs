using System;

namespace BS3D.Audio
{
    /// <summary>
    /// The arithmetic of a recording that plays round and round (#899): where the next chunk of it starts and how long it
    /// is, and where the position clock stands after the end. <see cref="RecordingPlayer"/> queues a recording in
    /// half-second chunks, and once the last frame is queued the next chunk is the first again, so the voice never runs dry
    /// at the seam; the clock the visualizer reads wraps with it and is put right by <see cref="ChunkQueue"/> whenever the
    /// voice finishes a chunk. Plain integers and doubles, no device, so the tests walk the seam without one.
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

    /// <summary>
    /// Which frame each chunk waiting in a voice's queue starts at, oldest first (#899): the book the visualizer's clock
    /// is put right by. A clock that only adds up wall time drifts from the audible music for good the moment a frame
    /// stalls (a window drag, a suspend, a queue that ran dry and a voice that stood silent meanwhile), and the voice has
    /// no play position to ask. What it does say is how many buffers are still queued
    /// (<c>DynamicSoundEffectInstance.PendingBufferCount</c>): a buffer leaves the queue when it has been played, so the
    /// chunk at the head is the one sounding and its start frame is where the recording audibly is, to within the time
    /// since it began. <see cref="Retire"/> brings the book in line with the voice's count, and the player pins the clock
    /// to <see cref="HeadStart"/> whenever a chunk has left or a chunk has joined an empty queue; the clock is then off
    /// by at most the time since the last chunk boundary was seen, and never by more than one chunk.
    /// <para>
    /// A ring over one array allocated up front: nothing here allocates per frame.
    /// </para>
    /// </summary>
    internal sealed class ChunkQueue
    {
        private readonly int[] _starts;
        private int _head;
        private int _count;

        /// <summary>A book for a voice that queues at most <paramref name="capacity"/> chunks.</summary>
        public ChunkQueue(int capacity)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            _starts = new int[capacity];
        }

        /// <summary>How many chunks the book believes are queued.</summary>
        public int Count => _count;

        /// <summary>The start frame of the chunk now sounding; only meaningful while <see cref="Count"/> is above zero.</summary>
        public int HeadStart => _starts[_head];

        /// <summary>
        /// Writes down a chunk just handed to the voice, starting at <paramref name="startFrame"/>. A full book forgets its
        /// oldest chunk rather than overflowing: <see cref="Retire"/> runs before every submit, so it is only a voice that
        /// miscounts that gets there.
        /// </summary>
        public void Push(int startFrame)
        {
            if (_count == _starts.Length)
            {
                _head = (_head + 1) % _starts.Length;
                _count--;
            }

            _starts[(_head + _count) % _starts.Length] = startFrame;
            _count++;
        }

        /// <summary>
        /// Forgets the chunks the voice has finished: oldest first, until no more are written down than the voice
        /// reports <paramref name="pendingBufferCount"/> queued. Returns how many were forgotten - above zero is the
        /// moment the sounding chunk changed, and so the moment to put the clock right.
        /// </summary>
        public int Retire(int pendingBufferCount)
        {
            int keep = Math.Max(pendingBufferCount, 0);
            int retired = 0;

            while (_count > keep)
            {
                _head = (_head + 1) % _starts.Length;
                _count--;
                retired++;
            }

            return retired;
        }

        /// <summary>Forgets every chunk: the voice was let go.</summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
