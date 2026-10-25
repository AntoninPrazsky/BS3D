using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The benchmark's live graph (#988): the last <see cref="CAPACITY"/> frames, oldest first, each marked when it belongs to
    /// a scene's first seconds (reported apart, so drawn apart), and how tall each stands. The graph's top is
    /// <see cref="HEADROOM"/> budgets, so the budget's line runs across its middle and a frame that took twice the display's
    /// time or more fills it. Pure and reserved once, so <c>FrameTraceTests</c> holds the arithmetic and a running
    /// benchmark allocates nothing per frame.
    /// </summary>
    internal sealed class FrameTrace
    {
        /// <summary>How many frames the graph shows: four seconds at 60 Hz, a little over three at 75.</summary>
        internal const int CAPACITY = 240;

        /// <summary>The graph's top, in budgets.</summary>
        internal const float HEADROOM = 2f;

        private readonly float[] _milliseconds = new float[CAPACITY];
        private readonly bool[] _apart = new bool[CAPACITY];
        private int _next, _count;

        /// <summary>How many frames are kept, up to <see cref="CAPACITY"/>.</summary>
        internal int Count => _count;

        internal void Clear() => _next = _count = 0;

        /// <summary>
        /// Records one frame of <paramref name="milliseconds"/>, <paramref name="apart"/> when it is a first-use frame the
        /// figures report apart; the oldest is given up once the graph is full.
        /// </summary>
        internal void Add(float milliseconds, bool apart = false)
        {
            _milliseconds[_next] = milliseconds;
            _apart[_next] = apart;
            _next = (_next + 1) % CAPACITY;
            if (_count < CAPACITY) _count++;
        }

        /// <summary>The <paramref name="i"/>-th oldest frame kept, 0 the oldest and <see cref="Count"/> - 1 the newest.</summary>
        internal float this[int i] => _milliseconds[Slot(i)];

        /// <summary>Whether the <paramref name="i"/>-th oldest frame kept was recorded apart.</summary>
        internal bool IsApart(int i) => _apart[Slot(i)];

        private int Slot(int i) => (_next - _count + i + CAPACITY) % CAPACITY;

        /// <summary>A frame's height in the graph, 0 to 1: <paramref name="milliseconds"/> over <see cref="HEADROOM"/> budgets.</summary>
        internal static float Height(float milliseconds, float budget) =>
            budget > 0f ? Math.Clamp(milliseconds / (budget * HEADROOM), 0f, 1f) : 0f;

        /// <summary>Whether a frame missed the budget, by the same slack the figures count one over it with (<see cref="FrameStats.OVER_SLACK"/>).</summary>
        internal static bool Over(float milliseconds, float budget) => milliseconds > budget * FrameStats.OVER_SLACK;
    }
}
