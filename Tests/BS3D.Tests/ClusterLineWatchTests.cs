using Prazsky.BS3D.Levels;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The death line as a rule over time (#239, #710): a crossing has to be HELD for the grace before the level is
    /// lost, a frame back above the line starts the count again, and a ball further under than any swing goes is lost
    /// at once. These are the promises the player's last-chance window rests on, stated without a window - and in
    /// seconds, so the same crossing is the same window at 30 Hz and at 240.
    /// </summary>
    public class ClusterLineWatchTests
    {
        //Under the line but inside the swing allowance: the case the grace is for
        private const float SHALLOW = ClusterHang.DEATH_Y - 0.5f * ClusterHang.SWING_ALLOWANCE;

        private static float HeldFor(float seconds, float framesPerSecond, out ClusterLineVerdict verdict)
        {
            ClusterLineWatch watch = default;
            float dt = 1f / framesPerSecond;
            verdict = ClusterLineVerdict.Alive;
            float t = 0f;

            while (t < seconds - 1e-4f && verdict == ClusterLineVerdict.Alive)
            {
                verdict = watch.Update(SHALLOW, dt);
                t += dt;
            }

            return t;
        }

        [Fact]
        public void AboveTheLineIsAlwaysAliveAndCountsNothing()
        {
            ClusterLineWatch watch = default;

            Assert.Equal(ClusterLineVerdict.Alive, watch.Update(ClusterHang.DEATH_Y + 1f, 5f));
            Assert.Equal(0f, watch.BelowLineSeconds);
        }

        [Theory]
        [InlineData(30f)]
        [InlineData(60f)]
        [InlineData(240f)]
        public void ACrossingHeldShorterThanTheGraceIsForgiven(float framesPerSecond)
        {
            //A second and a quarter: the owner's last-chance window is longer than the one second it used to be
            HeldFor(1.25f, framesPerSecond, out ClusterLineVerdict verdict);

            Assert.Equal(ClusterLineVerdict.Alive, verdict);
        }

        [Theory]
        [InlineData(30f)]
        [InlineData(60f)]
        [InlineData(240f)]
        public void ACrossingHeldForTheWholeGraceIsLostWithinAFrame(float framesPerSecond)
        {
            float held = HeldFor(ClusterHang.BELOW_LINE_GRACE + 1f, framesPerSecond, out ClusterLineVerdict verdict);

            Assert.Equal(ClusterLineVerdict.HeldTooLong, verdict);
            Assert.InRange(held, ClusterHang.BELOW_LINE_GRACE - 1e-3f, ClusterHang.BELOW_LINE_GRACE + 2f / framesPerSecond);
        }

        [Fact]
        public void AFrameBackAboveTheLineStartsTheCountAgain()
        {
            ClusterLineWatch watch = default;

            //Nearly the whole grace under the line, one frame back up, then the grace again from nothing
            Assert.Equal(ClusterLineVerdict.Alive, watch.Update(SHALLOW, ClusterHang.BELOW_LINE_GRACE - 0.05f));
            Assert.Equal(ClusterLineVerdict.Alive, watch.Update(ClusterHang.DEATH_Y + 0.1f, 1f / 60f));
            Assert.Equal(0f, watch.BelowLineSeconds);
            Assert.Equal(ClusterLineVerdict.Alive, watch.Update(SHALLOW, ClusterHang.BELOW_LINE_GRACE - 0.05f));
        }

        [Fact]
        public void ABallFurtherUnderThanAnySwingGoesIsLostAtOnce()
        {
            ClusterLineWatch watch = default;

            Assert.Equal(ClusterLineVerdict.PastAllowance,
                watch.Update(ClusterHang.DEATH_Y - ClusterHang.SWING_ALLOWANCE - 0.01f, 1f / 60f));
        }

        [Fact]
        public void TheGraceIsLongerThanTheLongestSwingEverMeasured()
        {
            //Chest, #239: 35 swings, the longest 0.76 s. The grace is also the player's window now, so it only grows
            Assert.True(ClusterHang.BELOW_LINE_GRACE > 0.76f);
            Assert.True(ClusterHang.BELOW_LINE_GRACE >= 1f);
        }
    }
}
