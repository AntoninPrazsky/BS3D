using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace BS3D.Capture
{
    /// <summary>
    /// The frames of a capture written off the render thread (#946): the render thread reads a frame back into a buffer it
    /// <see cref="Rent"/>s, hands it to <see cref="Submit"/> and goes on to the next frame; a few threads of this class's own
    /// encode each to a numbered PNG (<see cref="CapturePlan.FrameFileName"/>) and give the buffer back.
    /// <para>
    /// <b>The queue is bounded by the buffers.</b> There are a few more of them than there are threads, so a writer that
    /// cannot keep up holds the render thread at <see cref="Rent"/> and the run slows to the rate frames can be written; no
    /// frame is dropped and memory does not grow with the length of the clip. That is safe only because game time no longer
    /// follows the wall clock in a capture run (<see cref="CapturePlan"/>): waiting costs real time and nothing else.
    /// </para>
    /// The threads run below normal priority, so the simulation's own workers and the render thread keep their cores.
    /// </summary>
    internal sealed class CaptureWriter
    {
        private readonly string _directory;
        private readonly int _width, _height;
        private readonly SemaphoreSlim _free;
        private readonly ConcurrentBag<Color[]> _pool = new();
        private readonly BlockingCollection<(Color[] Pixels, int Number)> _work = new();
        private readonly Thread[] _threads;
        private int _written, _failed;
        private string _firstFailure;

        /// <param name="directory">Where the frames go, created if it is missing.</param>
        /// <param name="width">The frame's width in pixels.</param>
        /// <param name="height">The frame's height in pixels.</param>
        /// <param name="threads">How many threads encode; two buffers each are in flight.</param>
        internal CaptureWriter(string directory, int width, int height, int threads)
        {
            _directory = directory;
            _width = width;
            _height = height;
            _free = new SemaphoreSlim(Math.Max(1, threads) * 2);
            Directory.CreateDirectory(directory);

            _threads = new Thread[Math.Max(1, threads)];
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(Run) { Name = $"capture writer {i + 1}", IsBackground = true, Priority = ThreadPriority.BelowNormal };
                _threads[i].Start();
            }
        }

        internal int Width => _width;

        internal int Height => _height;

        /// <summary>A buffer for one frame's pixels, waiting for a free one when the writers are behind.</summary>
        internal Color[] Rent()
        {
            _free.Wait();
            return _pool.TryTake(out Color[] pixels) ? pixels : new Color[_width * _height];
        }

        /// <summary>Hands a filled buffer over to be written as frame <paramref name="number"/>; it comes back to the pool when it is.</summary>
        internal void Submit(Color[] pixels, int number) => _work.Add((pixels, number));

        /// <summary>Waits for every frame handed over to be written and the threads to end; how many were, and how many failed.</summary>
        internal (int Written, int Failed, string FirstFailure) Complete()
        {
            _work.CompleteAdding();
            foreach (Thread thread in _threads) thread.Join();

            return (_written, _failed, _firstFailure);
        }

        private void Run()
        {
            PngEncoder encoder = new();

            foreach ((Color[] pixels, int number) in _work.GetConsumingEnumerable())
            {
                try
                {
                    using FileStream file = new(Path.Combine(_directory, CapturePlan.FrameFileName(number)), FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
                    encoder.Encode(MemoryMarshal.AsBytes(pixels.AsSpan()), _width, _height, file);
                    Interlocked.Increment(ref _written);
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref _failed);
                    Interlocked.CompareExchange(ref _firstFailure, $"frame {number}: {exception.Message}", null);
                }
                finally
                {
                    _pool.Add(pixels);
                    _free.Release();
                }
            }
        }
    }
}
