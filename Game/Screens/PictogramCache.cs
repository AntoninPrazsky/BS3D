using Microsoft.Xna.Framework;
using Prazsky.Core.Tools;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BS3D.Screens
{
    /// <summary>What a set of chip pictograms is drawn for: <see cref="PowerupPictograms.RasterWithBacking"/>'s own arguments.</summary>
    internal readonly record struct PictogramKey(int Size, int Pad, float Soft)
    {
        /// <summary>The square's side, the margin included.</summary>
        internal int Side => Size + 2 * Pad;
    }

    /// <summary>
    /// The chips' three pictograms and their backings at one <see cref="PictogramKey"/>, indexed as
    /// <see cref="PictogramCache.KINDS"/>, and where they came from.
    /// </summary>
    internal sealed class PictogramSet
    {
        internal PictogramSet(PictogramKey key, Color[][] pictures, Color[][] backings, bool baked, double milliseconds)
        {
            Key = key;
            Pictures = pictures;
            Backings = backings;
            Baked = baked;
            Milliseconds = milliseconds;
        }

        internal PictogramKey Key { get; }
        internal Color[][] Pictures { get; }
        internal Color[][] Backings { get; }

        /// <summary>Rastered by this run, rather than read from the cache.</summary>
        internal bool Baked { get; }

        /// <summary>How long the read or the raster took, wherever it ran.</summary>
        internal double Milliseconds { get; }
    }

    /// <summary>
    /// <b>The chips' pictograms, baked once a size and kept</b> (#1146): <see cref="PowerupPictograms"/> rasters them at the
    /// size the HUD shows them, and this keeps what it drew in the player's <c>Cache</c> folder, so a launch after the first
    /// at that size reads a file instead and no run draws a size twice.
    /// <para>
    /// <b>Asked for before it is needed.</b> <see cref="Prefetch"/> reads or bakes on a worker; the game calls it whenever the
    /// HUD's fonts are resolved for a new size, which the load does first thing (<c>BS3DGame.LoadContent</c>) and the menu
    /// does on a resize. <see cref="Get"/>, from the level's build, then finds it done, and only waits, or does the work
    /// itself, for a size nobody asked for ahead (a window resized during play).
    /// </para>
    /// <para>
    /// Not a level-of-detail scheme and not a set of sizes shipped with the game: the HUD asks for any size its layout height
    /// gives (a window can be any height), and every size drawn here is the raster's own, never a scaled one, so the
    /// pictures are pixel for pixel what <see cref="PowerupPictograms"/> draws. A file holds one byte a pixel (the pictures
    /// are white coverage, premultiplied: every channel equals alpha), the key and the drawing's version it was made under
    /// (<see cref="PowerupPictograms.RASTER_VERSION"/>) in its header and name, and a checksum: a file that does not read
    /// as exactly what was asked for is baked over. At most <see cref="KEPT"/> sizes are kept, the least recently used
    /// dropped, and files of another version go at the next write.
    /// </para>
    /// </summary>
    internal sealed class PictogramCache
    {
        /// <summary>The kinds a set holds, in its order.</summary>
        internal static readonly PowerupKind[] KINDS = { PowerupKind.Swap, PowerupKind.Brake, PowerupKind.Cut };

        /// <summary>The sizes kept: a window dragged through many heights in the menu leaves no more files than this.</summary>
        internal const int KEPT = 12;

        /// <summary>The folder in the player's directory (<c>UserData</c>) the game keeps them in: nothing a player keeps.</summary>
        internal const string FOLDER_NAME = "Cache";

        private const string PREFIX = "chip-pictograms-";
        private const string EXTENSION = ".bin";
        private static readonly byte[] MAGIC = "BS3DPICT"u8.ToArray();
        private const int FORMAT = 1;

        //The header: the magic, the format, the drawing's version, size, pad, soft's bits and the kinds' count
        private const int HEADER = 8 + 6 * sizeof(int);

        private readonly string _directory;
        private readonly int _version;

        //The latest set asked for (Prefetch or Get), the work that answers it, and the last file still being written
        private PictogramKey _pendingKey;
        private Task<PictogramSet> _pending;
        private Task _writing = Task.CompletedTask;
        private readonly object _writeLock = new();

        private int _rasters;

        /// <param name="directory">Where the files are kept; null keeps nothing (every size is baked).</param>
        /// <param name="version">The drawing's version the files are named for: <see cref="PowerupPictograms.RASTER_VERSION"/>
        /// but in a test.</param>
        internal PictogramCache(string directory, int version = PowerupPictograms.RASTER_VERSION)
        {
            _directory = directory;
            _version = version;
        }

        /// <summary>How many sets this cache has rastered rather than read (a test's question).</summary>
        internal int Rasters => Volatile.Read(ref _rasters);

        /// <summary>
        /// Whether <paramref name="key"/>'s set has been read or baked already, for <see cref="Get"/> to take without doing the
        /// work (a test's question: that <see cref="Prefetch"/> did it ahead).
        /// </summary>
        internal bool IsReady(PictogramKey key) => _pending != null && _pendingKey == key && _pending.IsCompleted;

        /// <summary>
        /// Reads or bakes <paramref name="key"/>'s set on a worker, unless it is the set already asked for. Returns at once;
        /// <see cref="Get"/> takes the result.
        /// </summary>
        internal void Prefetch(PictogramKey key)
        {
            if (_pending != null && _pendingKey == key) return;

            _pendingKey = key;
            _pending = Task.Run(() => ReadOrBake(key));
        }

        /// <summary>
        /// <paramref name="key"/>'s set: the one <see cref="Prefetch"/> started, waited for if it is not done yet, or read or
        /// baked here and now when nothing asked for it ahead.
        /// </summary>
        internal PictogramSet Get(PictogramKey key)
        {
            if (_pending != null && _pendingKey == key)
            {
                try
                {
                    return _pending.GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    //Nothing in the work throws by design (a file it cannot read or write is a miss); if something did, the
                    //pictures are still drawn, here
                    Console.WriteLine($"[pictograms] the read-ahead failed, baked here: {e.GetType().Name}: {e.Message}");
                }
            }

            PictogramSet set = ReadOrBake(key);
            _pendingKey = key;
            _pending = Task.FromResult(set);
            return set;
        }

        /// <summary>Waits for the files still being written (a test's step, and nothing the game waits for).</summary>
        internal void WaitForWrites()
        {
            Task writing;
            lock (_writeLock) writing = _writing;
            writing.Wait();
        }

        /// <summary>The file <paramref name="key"/>'s set is kept in, under this cache's version.</summary>
        internal string PathFor(PictogramKey key) => Path.Combine(_directory, FileName(key, _version));

        /// <summary>A set's file name: the key and the drawing's version, so a set of another version is never read.</summary>
        internal static string FileName(PictogramKey key, int version) => string.Create(CultureInfo.InvariantCulture,
            $"{PREFIX}r{version}-{key.Size}-{key.Pad}-{key.Soft:R}{EXTENSION}");

        private PictogramSet ReadOrBake(PictogramKey key)
        {
            long start = Stopwatch.GetTimestamp();
            string path = _directory == null ? null : PathFor(key);

            PictogramSet read = path == null ? null : TryRead(path, key, start);
            if (read != null)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[pictograms] the chips' pictograms at {0} px read from the cache in {1:F1} ms", key.Size, read.Milliseconds));
                return read;
            }

            Interlocked.Increment(ref _rasters);
            var pictures = new Color[KINDS.Length][];
            var backings = new Color[KINDS.Length][];
            for (int k = 0; k < KINDS.Length; k++)
                (pictures[k], backings[k]) = PowerupPictograms.RasterWithBacking(KINDS[k], key.Size, key.Pad, key.Soft);

            var baked = new PictogramSet(key, pictures, backings, baked: true, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "[pictograms] the chips' pictograms at {0} px baked in {1:F1} ms{2}", key.Size, baked.Milliseconds,
                path == null ? ", not kept" : ", kept in the cache"));

            //Written on a worker, whoever baked: a write is flushed to the disk, which on a Pi's card is no thing for a frame.
            //One after another, as a read-ahead and the build may both have baked
            if (path != null)
                lock (_writeLock)
                    _writing = _writing.ContinueWith(_ =>
                    {
                        TryWrite(path, baked);
                        Prune();
                    }, TaskScheduler.Default);

            return baked;
        }

        //--- The file -----------------------------------------------------------------------------------------------------

        private PictogramSet TryRead(string path, PictogramKey key, long start)
        {
            byte[] bytes;
            try
            {
                if (!File.Exists(path)) return null;
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            int plane = key.Side * key.Side;
            if (bytes.Length != HEADER + KINDS.Length * (1 + 2 * plane) + sizeof(uint)) return null;
            if (!bytes.AsSpan(0, MAGIC.Length).SequenceEqual(MAGIC)) return null;
            if (Checksum(bytes.AsSpan(0, bytes.Length - sizeof(uint))) != BitConverter.ToUInt32(bytes, bytes.Length - sizeof(uint))) return null;

            int at = MAGIC.Length;
            if (ReadInt(bytes, ref at) != FORMAT || ReadInt(bytes, ref at) != _version) return null;
            if (ReadInt(bytes, ref at) != key.Size || ReadInt(bytes, ref at) != key.Pad) return null;
            if (ReadInt(bytes, ref at) != BitConverter.SingleToInt32Bits(key.Soft) || ReadInt(bytes, ref at) != KINDS.Length) return null;

            var pictures = new Color[KINDS.Length][];
            var backings = new Color[KINDS.Length][];
            for (int k = 0; k < KINDS.Length; k++)
            {
                if (bytes[at++] != (byte)KINDS[k]) return null;
                pictures[k] = Expand(bytes, at, plane);
                backings[k] = Expand(bytes, at + plane, plane);
                at += 2 * plane;
            }

            //Kept among the sizes in use: the least recently used are the ones Prune drops
            try
            {
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }

            return new PictogramSet(key, pictures, backings, baked: false, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        private static Color[] Expand(byte[] bytes, int at, int count)
        {
            var pixels = new Color[count];
            for (int i = 0; i < count; i++)
            {
                byte a = bytes[at + i];
                pixels[i] = new Color(a, a, a, a);
            }
            return pixels;
        }

        private void TryWrite(string path, PictogramSet set)
        {
            int plane = set.Key.Side * set.Key.Side;
            var bytes = new byte[HEADER + KINDS.Length * (1 + 2 * plane) + sizeof(uint)];
            MAGIC.CopyTo(bytes, 0);
            int at = MAGIC.Length;
            WriteInt(bytes, ref at, FORMAT);
            WriteInt(bytes, ref at, _version);
            WriteInt(bytes, ref at, set.Key.Size);
            WriteInt(bytes, ref at, set.Key.Pad);
            WriteInt(bytes, ref at, BitConverter.SingleToInt32Bits(set.Key.Soft));
            WriteInt(bytes, ref at, KINDS.Length);
            for (int k = 0; k < KINDS.Length; k++)
            {
                bytes[at++] = (byte)KINDS[k];
                //One byte a pixel holds only white coverage: a picture of any other colour is not kept, and is baked again
                if (!Flatten(set.Pictures[k], bytes, at) || !Flatten(set.Backings[k], bytes, at + plane)) return;
                at += 2 * plane;
            }
            BitConverter.TryWriteBytes(bytes.AsSpan(at), Checksum(bytes.AsSpan(0, at)));

            try
            {
                AtomicFile.WriteBytes(path, bytes, null);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                //Two runs writing the same size at once, or a folder that cannot be written: baked again next time
                Console.WriteLine($"[pictograms] not kept: {e.GetType().Name}: {e.Message}");
            }
        }

        private static bool Flatten(Color[] pixels, byte[] bytes, int at)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.R != c.A || c.G != c.A || c.B != c.A) return false;
                bytes[at + i] = c.A;
            }
            return true;
        }

        //Files of another version of the drawing, and the least recently used sizes past KEPT
        private void Prune()
        {
            try
            {
                string current = string.Create(CultureInfo.InvariantCulture, $"{PREFIX}r{_version}-");
                var kept = new System.Collections.Generic.List<FileInfo>();
                foreach (FileInfo file in new DirectoryInfo(_directory).EnumerateFiles(PREFIX + "*"))
                {
                    if (file.Name.StartsWith(current, StringComparison.Ordinal) && file.Name.EndsWith(EXTENSION, StringComparison.Ordinal)) kept.Add(file);
                    //A temporary file is another run's write in flight for a minute, and a torn one after that
                    else if (!file.Name.EndsWith(".tmp", StringComparison.Ordinal) || DateTime.UtcNow - file.LastWriteTimeUtc > TimeSpan.FromMinutes(1)) file.Delete();
                }

                kept.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = KEPT; i < kept.Count; i++) kept[i].Delete();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static int ReadInt(byte[] bytes, ref int at)
        {
            int value = BitConverter.ToInt32(bytes, at);
            at += sizeof(int);
            return value;
        }

        private static void WriteInt(byte[] bytes, ref int at, int value)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(at), value);
            at += sizeof(int);
        }

        //FNV-1a, 32 bits: a torn or damaged file is told from a whole one
        private static uint Checksum(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261;
            foreach (byte b in bytes) hash = (hash ^ b) * 16777619;
            return hash;
        }
    }
}
