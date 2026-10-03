using Prazsky.Core.Tools;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The peak caps of the spectrum display (#730), without a window: a cap holds, then falls under gravity in seconds
    /// and not in frames, is never below its level, and with nothing pushing it ends on the floor. The frame-rate promise
    /// is the one the issue asked to be checked at two frame caps, stated here once and for all.
    /// </summary>
    public class PeakHoldTests
    {
        private const float HOLD = 0.33f;
        private const float GRAVITY = 5.5f;

        /// <summary>One band pushed up to <paramref name="top"/>, then its level dropped to <paramref name="level"/> and run for
        /// <paramref name="seconds"/> in steps of <paramref name="step"/>.</summary>
        private static float CapAfter(float top, float level, float seconds, float step)
        {
            PeakHold caps = new(1, HOLD, GRAVITY);
            caps.Step(new[] { top }, step);

            float[] levels = { level };
            int steps = (int)MathF.Round(seconds / step);
            for (int i = 0; i < steps; i++) caps.Step(levels, step);

            return caps[0];
        }

        [Fact]
        public void ACapRidesItsColumnUpAndHoldsWhenTheColumnDrops()
        {
            //A step short of the hold: still where the column left it
            Assert.Equal(0.8f, CapAfter(0.8f, 0.2f, HOLD - 0.02f, 0.01f), 4);
        }

        [Fact]
        public void AfterTheHoldACapFallsUnderGravityFromRest()
        {
            float seconds = 0.2f;
            float expected = 0.8f - 0.5f * GRAVITY * seconds * seconds;

            Assert.Equal(expected, CapAfter(0.8f, 0.2f, HOLD + seconds, 0.001f), 3);
        }

        [Fact]
        public void TheFallIsTheSameAtSixtyAndAtTwoHundredFortyHertz()
        {
            float at60 = CapAfter(0.9f, 0.1f, 0.6f, 1f / 60f);
            float at240 = CapAfter(0.9f, 0.1f, 0.6f, 1f / 240f);
            float at1000 = CapAfter(0.9f, 0.1f, 0.6f, 0.001f);

            Assert.Equal(at1000, at60, 3);
            Assert.Equal(at1000, at240, 3);
        }

        [Fact]
        public void ACapFallsOntoItsColumnAndRestsThere()
        {
            Assert.Equal(0.2f, CapAfter(0.8f, 0.2f, 3f, 1f / 60f), 5);
        }

        [Fact]
        public void WithNothingPushingItACapEndsOnTheFloor()
        {
            Assert.Equal(0f, CapAfter(1f, 0f, 3f, 1f / 60f), 5);
        }

        [Fact]
        public void ACapIsNeverBelowItsLevelWhateverTheLevelsDo()
        {
            PeakHold caps = new(3, HOLD, GRAVITY);
            Random random = new(730);
            float[] levels = new float[3];

            for (int frame = 0; frame < 5000; frame++)
            {
                for (int b = 0; b < levels.Length; b++)
                    if (random.Next(20) == 0) levels[b] = (float)random.NextDouble();

                caps.Step(levels, 1f / 60f);

                for (int b = 0; b < levels.Length; b++)
                    Assert.True(caps[b] >= levels[b], $"frame {frame}, band {b}: cap {caps[b]} under level {levels[b]}");
            }
        }

        [Fact]
        public void ALouderLevelPushesTheCapUpAndRestartsTheHold()
        {
            PeakHold caps = new(1, HOLD, GRAVITY);
            caps.Step(new[] { 0.5f }, 0.01f);

            //Most of the hold gone, then pushed higher: held again from the start
            for (int i = 0; i < 30; i++) caps.Step(new[] { 0.1f }, 0.01f);
            caps.Step(new[] { 0.7f }, 0.01f);
            for (int i = 0; i < 30; i++) caps.Step(new[] { 0.1f }, 0.01f);

            Assert.Equal(0.7f, caps[0], 4);
        }
    }
}
