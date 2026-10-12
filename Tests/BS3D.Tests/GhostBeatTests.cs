using BS3D.Screens;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The landing preview's beat (#831): the aim ghost reaches exactly a whole ball every beat and never goes past it,
    /// bottoms out at about three tenths of one, and is the same curve at every frame rate. The owner's words are what is
    /// held: a whole ball at the top (a smaller one reads as farther away), never above 1 (it would push into the
    /// neighbours), and a rubber bounce rather than a sway.
    /// </summary>
    public class GhostBeatTests
    {
        private const float PERIOD = 1f / GhostBeat.HZ;

        //Forty beats from a clock at zero, and forty from ten minutes into a session, where a float's seconds are coarser
        private static readonly float[] Starts = { 0f, 600f };
        private const int BEATS = 40;
        private const float STEP = 1e-4f;

        private static IEnumerable<(int Beat, float Size)> Sampled(float start)
        {
            int samples = (int)(BEATS * PERIOD / STEP);
            for (int i = 0; i < samples; i++)
            {
                float t = start + i * STEP;
                yield return ((int)MathF.Floor((t - start) / PERIOD), GhostBeat.Scale(t));
            }
        }

        [Fact]
        public void NeverAboveAWholeBall()
        {
            foreach (float start in Starts)
                foreach ((int beat, float size) in Sampled(start))
                    Assert.True(size <= 1f, $"beat {beat} from {start} s: {size} is bigger than a whole ball");
        }

        [Fact]
        public void ReachesExactlyAWholeBallEveryBeat()
        {
            foreach (float start in Starts)
            {
                float[] top = new float[BEATS];
                foreach ((int beat, float size) in Sampled(start))
                    if (beat < BEATS) top[beat] = MathF.Max(top[beat], size);

                for (int beat = 0; beat < BEATS; beat++)
                    Assert.True(top[beat] == 1f, $"beat {beat} from {start} s tops out at {top[beat]}, not at a whole ball");
            }
        }

        [Fact]
        public void BottomsOutAtAboutThreeTenthsOfABallEveryBeat()
        {
            foreach (float start in Starts)
            {
                float[] bottom = new float[BEATS];
                Array.Fill(bottom, float.MaxValue);
                foreach ((int beat, float size) in Sampled(start))
                    if (beat < BEATS) bottom[beat] = MathF.Min(bottom[beat], size);

                for (int beat = 0; beat < BEATS; beat++)
                    Assert.InRange(bottom[beat], 0.28f, 0.32f);
            }
        }

        [Fact]
        public void ContinuousAcrossEveryJoint()
        {
            //The fastest the curve moves is about 14 sizes a second, on the rise. A jump anywhere - at the wrap from one beat
            //into the next, or where the settle takes over from the rise - shows as a step far steeper than this allows
            const float MAX_RATE = 20f;

            foreach (float start in Starts)
            {
                float previous = GhostBeat.Scale(start);
                for (int i = 1; i < BEATS * PERIOD / STEP; i++)
                {
                    float t0 = start + (i - 1) * STEP, t1 = start + i * STEP;
                    float size = GhostBeat.Scale(t1);
                    float allowed = MAX_RATE * (t1 - t0) + 1e-5f;

                    Assert.True(MathF.Abs(size - previous) <= allowed,
                        $"{t1:0.0000} s: the size steps from {previous} to {size}");
                    previous = size;
                }
            }
        }

        [Theory]
        [InlineData(24)]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(75)]
        [InlineData(144)]
        [InlineData(240)]
        public void EveryFrameRateSeesAWholeBallAndTheSmallEndEveryBeat(int fps)
        {
            //What a player sees is the curve sampled once a frame. The hold at a whole ball and the deep end are both long
            //enough that no frame rate from 24 up can step over either, whatever phase its frames fall on
            for (int offset = 0; offset < 7; offset++)
            {
                float start = 3f + offset * 0.0137f;
                float[] top = new float[BEATS], bottom = new float[BEATS];
                Array.Fill(bottom, float.MaxValue);

                for (int frame = 0; frame / (float)fps < BEATS * PERIOD; frame++)
                {
                    float since = frame / (float)fps;
                    int beat = (int)MathF.Floor(since / PERIOD);
                    if (beat >= BEATS) break;

                    float size = GhostBeat.Scale(start + since);
                    top[beat] = MathF.Max(top[beat], size);
                    bottom[beat] = MathF.Min(bottom[beat], size);
                }

                //The first and the last beat are partial: the frames start and stop at an arbitrary phase
                for (int beat = 1; beat < BEATS - 1; beat++)
                {
                    Assert.True(top[beat] == 1f, $"{fps} fps, offset {offset}: beat {beat} never shows a whole ball ({top[beat]})");
                    Assert.True(bottom[beat] <= GhostBeat.LOW + 0.05f,
                        $"{fps} fps, offset {offset}: beat {beat} never shows the small end ({bottom[beat]})");
                }
            }
        }

        [Fact]
        public void TheSameSecondIsTheSameSizeWhateverCameBefore()
        {
            //No state on the ghost: asked in any order, at any rate, a second gives one size
            float[] times = new float[500];
            for (int i = 0; i < times.Length; i++) times[i] = 7f + i * 0.00731f;

            float[] forwards = new float[times.Length];
            for (int i = 0; i < times.Length; i++) forwards[i] = GhostBeat.Scale(times[i]);

            for (int i = times.Length - 1; i >= 0; i--)
            {
                GhostBeat.Scale(times[i] + 0.3f);
                Assert.Equal(forwards[i], GhostBeat.Scale(times[i]));
            }
        }

        [Fact]
        public void RisesFastAndSettlesSlowly()
        {
            //Within one beat: the climb from half a ball to nearly a whole one, against the fall back between the same two
            float up50 = float.NaN, up95 = float.NaN, down95 = float.NaN, down50 = float.NaN;
            float previous = GhostBeat.Scale(0f);
            for (float t = STEP; t < PERIOD; t += STEP)
            {
                float size = GhostBeat.Scale(t);
                if (previous < 0.5f && size >= 0.5f && float.IsNaN(up50)) up50 = t;
                if (previous < 0.95f && size >= 0.95f && float.IsNaN(up95)) up95 = t;
                if (previous >= 0.95f && size < 0.95f) down95 = t;
                if (previous >= 0.5f && size < 0.5f) down50 = t;
                previous = size;
            }

            float rise = up95 - up50, fall = down50 - down95;
            Assert.True(rise > 0f && fall > 0f, $"rise {rise}, fall {fall}");
            Assert.True(fall >= 1.5f * rise, $"the fall ({fall:0.000} s) is not slower than the rise ({rise:0.000} s)");
        }

        [Fact]
        public void BouncesUnderTheCeilingAndSpringsBackFromTheBottom()
        {
            //The rubber: once at a whole ball it sags under it and comes back before it leaves (a bounce against the
            //ceiling, not past it), and the settle falls past where it rests and climbs back before the next beat
            float firstTop = float.NaN, sagDeepest = 1f, deepest = 1f, deepestAt = 0f;
            bool backOnTop = false;
            for (float t = 0f; t < PERIOD; t += STEP)
            {
                float size = GhostBeat.Scale(t);
                if (size == 1f && float.IsNaN(firstTop)) firstTop = t;
                //Up to a few milliseconds into the settle, which leaves the ceiling from exactly a whole ball
                if (!float.IsNaN(firstTop) && t < GhostBeat.TOP_SECONDS + 0.005f)
                {
                    sagDeepest = MathF.Min(sagDeepest, size);
                    if (sagDeepest < 0.95f && size >= 0.999f) backOnTop = true;
                }
                if (size < deepest) { deepest = size; deepestAt = t; }
            }

            Assert.True(sagDeepest < 0.95f, $"no sag under the ceiling: {sagDeepest}");
            Assert.True(backOnTop, "the sag never came back to a whole ball");
            Assert.True(deepestAt < PERIOD - 0.05f, $"the deepest point is at the beat's very end ({deepestAt:0.000} s)");
            Assert.True(GhostBeat.Scale(PERIOD - STEP) - deepest >= 0.05f, "the settle never springs back from its deepest point");
        }

        [Fact]
        public void BeatsAtItsRate()
        {
            int rises = 0;
            float previous = GhostBeat.Scale(0.5f);
            for (float t = 0.5f + STEP; t < 10.5f; t += STEP)
            {
                float size = GhostBeat.Scale(t);
                if (previous < 0.65f && size >= 0.65f) rises++;
                previous = size;
            }

            Assert.Equal((int)MathF.Round(10f * GhostBeat.HZ), rises);
        }
    }
}
