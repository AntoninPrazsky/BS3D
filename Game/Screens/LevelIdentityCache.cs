using Prazsky.BS3D.Levels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BS3D.Screens
{
    /// <summary>
    /// Every level's board key (<see cref="LevelIdentity"/>), read off the files <b>off the frame thread</b> (#898, the
    /// cross-review of 2026-10-17): <see cref="LevelIdentity.Of(LevelSet, int)"/> reads a level file and hashes it, and the
    /// High Scores page asks it of every level of the set - 142 files, about 100 ms warm and more on a cold disk or the laptop,
    /// which on the frame thread was a hitch when the first summary came in. <see cref="Start"/> reads them on a pool thread
    /// once; the frame thread only ever <see cref="TryGet">asks what is ready</see>, with no lock and no allocation.
    /// <para>
    /// <b>A level that is not read yet is not an error:</b> <see cref="TryGet"/> says false, and the page writes such a row the
    /// way it writes one before the boards have answered. A level whose file will not read is <i>read</i> - its identity is null
    /// and <see cref="TryGet"/> says true, as the frame-thread lookup it replaces cached its null.
    /// </para>
    /// <para>
    /// <b>Memory order:</b> the worker writes a level's slot and then publishes it with a volatile write of its flag; the reader
    /// takes the flag with a volatile read before it touches the slot, so a reader that sees the flag sees the identity. The
    /// slots are written once each, by the one worker.
    /// </para>
    /// </summary>
    internal sealed class LevelIdentityCache
    {
        private readonly Func<int, LevelIdentity> _read;
        private readonly LevelIdentity[] _identity;
        private readonly int[] _done;
        private readonly Lazy<Task> _warm;
        private int _ready;

        /// <param name="count">How many levels the set has.</param>
        /// <param name="read">One level's identity, or null when its file will not read; run on the worker thread, so it may
        /// touch nothing the frame thread changes (the set is read-only once loaded).</param>
        internal LevelIdentityCache(int count, Func<int, LevelIdentity> read)
        {
            ArgumentNullException.ThrowIfNull(read);
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            _read = read;
            _identity = new LevelIdentity[count];
            _done = new int[count];
            _warm = new Lazy<Task>(() => Task.Run(Warm));
        }

        /// <summary>How many levels there are to read.</summary>
        internal int Count => _identity.Length;

        /// <summary>How many levels have been read so far.</summary>
        internal int Ready => Volatile.Read(ref _ready);

        /// <summary>Whether every level has been read.</summary>
        internal bool IsComplete => Ready >= _identity.Length;

        /// <summary>
        /// Starts reading every level on a pool thread - once: the later calls return the same running (or finished) task, so a
        /// page may call it on every entry. The set does not change during a run, so what is read is kept for the run.
        /// </summary>
        internal Task Start() => _warm.Value;

        private void Warm()
        {
            for (int level = 0; level < _identity.Length; level++)
            {
                LevelIdentity identity;

                try
                {
                    identity = _read(level);
                }
                catch (Exception)
                {
                    //Whatever the read threw, the level is read and has no identity, so the page is never left waiting on it
                    identity = null;
                }

                _identity[level] = identity;
                Volatile.Write(ref _done[level], 1);
                Interlocked.Increment(ref _ready);
            }
        }

        /// <summary>
        /// A level's identity if it has been read: true with the identity (null when the file would not read), false while it
        /// is still to be read or the level is out of range. Safe from any thread, takes no lock and allocates nothing.
        /// </summary>
        internal bool TryGet(int level, out LevelIdentity identity)
        {
            if ((uint)level >= (uint)_identity.Length || Volatile.Read(ref _done[level]) == 0)
            {
                identity = null;
                return false;
            }

            identity = _identity[level];
            return true;
        }
    }
}
