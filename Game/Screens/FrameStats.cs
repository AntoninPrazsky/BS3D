using System;
using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// One benchmark segment's frame times (#988) and what they say: the mean, the median, the 95th and 99th percentiles, the
    /// "1 % low" (the frame rate of the slowest hundredth of the frames, averaged), the worst frame and the share of frames
    /// over the display's own budget. Pure, so <c>FrameStatsTests</c> holds the arithmetic; the list is reserved once, so a
    /// running segment allocates nothing per frame.
    /// </summary>
    internal sealed class FrameStats
    {
        private readonly List<float> _milliseconds;

        //How far past the budget a frame must run to count as over it (2 %): see Summarize
        internal const float OVER_SLACK = 1.02f;

        //Reserved for a segment of half a minute at a thousand frames a second, so the list never grows while one runs
        public FrameStats(int capacity = 32768) => _milliseconds = new List<float>(capacity);

        /// <summary>How many frames were recorded.</summary>
        public int Count => _milliseconds.Count;

        /// <summary>Records one frame of <paramref name="milliseconds"/>.</summary>
        public void Add(float milliseconds) => _milliseconds.Add(milliseconds);

        public void Clear() => _milliseconds.Clear();

        /// <summary>The figures over every frame recorded, the budget being one frame of the display's refresh in milliseconds.</summary>
        public Summary Summarize(float budgetMilliseconds)
        {
            int n = _milliseconds.Count;
            if (n == 0) return default;

            var sorted = new float[n];
            _milliseconds.CopyTo(sorted);
            Array.Sort(sorted);

            double sum = 0;
            int over = 0;
            foreach (float ms in sorted)
            {
                sum += ms;
                //A frame held to the refresh lands on the budget give or take the timer's jitter: over is past it by OVER_SLACK
                if (budgetMilliseconds > 0f && ms > budgetMilliseconds * OVER_SLACK) over++;
            }

            //The slowest hundredth, at least one frame
            int slowest = Math.Max(1, n / 100);
            double slowSum = 0;
            for (int i = n - slowest; i < n; i++) slowSum += sorted[i];

            return new Summary(n, (float)(sum / n), Percentile(sorted, 0.5f), Percentile(sorted, 0.95f), Percentile(sorted, 0.99f),
                (float)(1000.0 / (slowSum / slowest)), sorted[n - 1], (float)over / n);
        }

        /// <summary>The value below which <paramref name="share"/> of the sorted values lie, interpolated between neighbours.</summary>
        internal static float Percentile(float[] sorted, float share)
        {
            if (sorted.Length == 1) return sorted[0];
            float at = share * (sorted.Length - 1);
            int below = (int)MathF.Floor(at);
            int above = Math.Min(below + 1, sorted.Length - 1);
            return sorted[below] + (sorted[above] - sorted[below]) * (at - below);
        }

        /// <summary>A segment's figures. Frame times in milliseconds; <see cref="OnePercentLowFps"/> in frames a second.</summary>
        internal readonly record struct Summary(int Frames, float Mean, float Median, float P95, float P99, float OnePercentLowFps,
            float Worst, float OverBudgetShare)
        {
            /// <summary>The median's frame rate.</summary>
            public float MedianFps => Median > 0f ? 1000f / Median : 0f;
        }
    }
}
