using BS3D.Effects;
using Microsoft.Xna.Framework;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The unused balls fired off and counted (#881): every ball leaves once, the count climbs with them to exactly the bonus,
    /// a skip lands on the same figure, a whole budget is fired within the cap, and the count is done after the last. And the
    /// lens is not let go when it is (#1198): it stays beside the gun, where the result page takes it from, and never comes
    /// back over the play pose in between.
    /// </summary>
    public class SpareVolleyTests
    {
        //The play lens the volley takes the frame from, standing still: what GameplayScreen.UpdateCamera lerps the volley's pose over
        private static readonly Vector3 PLAY_EYE = new(0f, 3f, 12f);
        private static readonly Vector3 PLAY_TARGET = new(0f, 2f, -10f);

        //Longer than anything the cleared beat waits after the count (GameplayScreen's AFTER_VOLLEY_BEAT) before the result page
        private const float AFTER_COUNT_CHECKED = 5f;

        private const float STEP = 1f / 144f;

        private static SpareVolley Begun(int balls, int bonus)
        {
            var volley = new SpareVolley();
            volley.Begin(balls, bonus, Vector3.Zero, new Vector3(0f, 1f, -2f), Vector3.Normalize(new Vector3(0f, 0.3f, -1f)),
                PLAY_EYE, PLAY_TARGET, MathHelper.ToRadians(55f));
            return volley;
        }

        /// <summary>The pose the camera is given: the play pose with the volley's lerped over it by its blend, as the screen composes it.</summary>
        private static Vector3 Composed(SpareVolley volley) => Vector3.Lerp(PLAY_EYE, volley.Position, volley.Blend);

        /// <summary>Where the lens stands beside the gun once it has arrived, read off a volley left to arrive.</summary>
        private static Vector3 SidePose()
        {
            SpareVolley volley = Begun(29, 1624);
            volley.Update(SpareVolley.LEAD_IN + 0.01f);
            return volley.Position;
        }

        [Theory]
        [InlineData(1, 160)]
        [InlineData(7, 1120)]
        [InlineData(29, 1624)]
        [InlineData(60, 3001)]
        [InlineData(90, 4500)]
        public void EveryBallLeavesOnceAndTheCountEndsOnTheBonus(int balls, int bonus)
        {
            SpareVolley volley = Begun(balls, bonus);
            int fired = 0, lastCount = 0;
            float t = 0f;
            while (volley.Running && t < 20f)
            {
                fired += volley.Update(STEP);
                t += STEP;
                Assert.True(volley.Counted >= lastCount, "the count went down");
                lastCount = volley.Counted;
            }

            Assert.Equal(balls, fired);
            Assert.Equal(bonus, volley.Counted);
            Assert.False(volley.Running);
            Assert.True(t <= SpareVolley.Duration(balls) + 0.01f, $"counted for {t:0.00} s");
        }

        [Fact]
        public void AWholeBudgetFitsTheCapAndAFewKeepTheirCadence()
        {
            //The longest budget the campaign deals is 90
            Assert.True(SpareVolley.Duration(90) <= SpareVolley.LEAD_IN + SpareVolley.MAX_SECONDS + SpareVolley.TAIL + 0.03f);
            Assert.Equal(SpareVolley.MAX_INTERVAL, SpareVolley.Interval(5), 4);
            Assert.True(SpareVolley.Interval(200) >= SpareVolley.MIN_INTERVAL);
        }

        [Fact]
        public void ASkipLandsOnTheSameFigureAndKeepsTheLens()
        {
            SpareVolley volley = Begun(29, 1624);
            int fired = 0;
            for (int i = 0; i < 100; i++) fired += volley.Update(1f / 60f);
            Assert.True(fired > 0 && fired < 29, $"{fired} fired before the skip");

            Assert.True(volley.TrySkip());
            Assert.Equal(1624, volley.Counted);
            Assert.Equal(fired, volley.Launched);
            Assert.False(volley.Running);
            Assert.False(volley.TrySkip());

            //No more leave after it, and the lens stays beside the gun for the result page to take (#1198)
            int after = 0;
            for (int i = 0; i < 120; i++) after += volley.Update(1f / 60f);
            Assert.Equal(0, after);
            Assert.True(volley.Engaged, "a skip let the lens go");
            Assert.Equal(1f, volley.Blend);
        }

        [Fact]
        public void ASkipInTheFirstMomentsIsRefused()
        {
            SpareVolley volley = Begun(29, 1624);
            volley.Update(0.1f);
            Assert.False(volley.TrySkip());
            Assert.True(volley.Running);
            volley.Update(SpareVolley.SKIP_LOCKOUT);
            Assert.True(volley.TrySkip());
        }

        [Fact]
        public void TheCountIsEachShotsShareRoundedDown()
        {
            Assert.Equal(0, SpareVolley.CountAt(0, 29, 1624));
            Assert.Equal(56, SpareVolley.CountAt(1, 29, 1624));
            Assert.Equal(1568, SpareVolley.CountAt(28, 29, 1624));
            Assert.Equal(1624, SpareVolley.CountAt(29, 29, 1624));
            Assert.Equal(0, SpareVolley.CountAt(0, 0, 0));
        }

        /// <summary>
        /// #1198, the owner: the camera looked at the gun from the side through the volley, then returned part way to the play
        /// pose and drove away again for the result page. From the volley's first frame to long after its count the lens moves
        /// only away from the play pose (out to the gun's side, then held there), it never jumps, and it ends beside the gun,
        /// where the result page captures it.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(30)]
        [InlineData(90)]
        public void TheLensNeverComesBackTowardsThePlayPose(int balls)
        {
            SpareVolley volley = Begun(balls, balls * 15);
            Vector3 side = SidePose();
            float farthest = 0f, end = SpareVolley.Duration(balls) + AFTER_COUNT_CHECKED;
            Vector3 previous = Composed(volley);

            for (float t = STEP; t <= end; t += STEP)
            {
                volley.Update(STEP);
                Vector3 lens = Composed(volley);
                float away = Vector3.Distance(lens, PLAY_EYE);

                Assert.True(away >= farthest - 1e-4f, $"at {t:0.000} s the lens came {farthest - away:0.000} back towards the play pose");
                //The arrival's own peak is about 0.5 a step here (its two smoothsteps over 11 units at 144 a second): a jump is a snap
                Assert.True(Vector3.Distance(lens, previous) < 1f, $"at {t:0.000} s the lens jumped {Vector3.Distance(lens, previous):0.000}");

                farthest = MathF.Max(farthest, away);
                previous = lens;
            }

            Assert.False(volley.Running);
            Assert.Equal(1f, volley.Blend);
            Assert.True(Vector3.Distance(Composed(volley), side) < 1e-3f, $"the lens ended {Vector3.Distance(Composed(volley), side):0.000} off the gun's side");
        }

        /// <summary>
        /// A skip taken while the lens is still arriving (<see cref="SpareVolley.SKIP_LOCKOUT"/> is under <see cref="SpareVolley.LEAD_IN"/>)
        /// lets the lens finish its arrival beside the gun and stay there (#1198): it used to turn round where it was and go back.
        /// </summary>
        [Fact]
        public void ASkipWhileTheLensArrivesGoesOnToTheGunsSide()
        {
            SpareVolley volley = Begun(29, 1624);
            Vector3 side = SidePose();
            float farthest = 0f, t = 0f;

            while (t < SpareVolley.SKIP_LOCKOUT)
            {
                volley.Update(STEP);
                t += STEP;
                farthest = MathF.Max(farthest, Vector3.Distance(Composed(volley), PLAY_EYE));
            }

            Assert.True(volley.Blend < 1f, "the skip was meant to land while the lens is still arriving");
            Assert.True(volley.TrySkip());

            for (; t < SpareVolley.LEAD_IN + AFTER_COUNT_CHECKED; t += STEP)
            {
                volley.Update(STEP);
                float away = Vector3.Distance(Composed(volley), PLAY_EYE);
                Assert.True(away >= farthest - 1e-4f, $"at {t:0.000} s the lens came {farthest - away:0.000} back towards the play pose");
                farthest = MathF.Max(farthest, away);
            }

            Assert.Equal(1f, volley.Blend);
            Assert.True(Vector3.Distance(Composed(volley), side) < 1e-3f);
        }

        /// <summary>
        /// The count is what the cleared beat waits for, and it ends on schedule; the lens outlasts it, held until a new level's
        /// <see cref="SpareVolley.Reset"/> (#1198). So the screen's countdown runs on <see cref="SpareVolley.Running"/>, with the
        /// lens still beside the gun, and the gun still not answering while it is (<see cref="SpareVolley.Engaged"/>).
        /// </summary>
        [Fact]
        public void TheCountEndsOnScheduleAndTheLensIsHeldUntilReset()
        {
            SpareVolley volley = Begun(7, 1120);
            float t = 0f;
            while (volley.Running && t < 20f)
            {
                volley.Update(STEP);
                t += STEP;
            }

            Assert.InRange(t, SpareVolley.Duration(7) - STEP, SpareVolley.Duration(7) + STEP);

            for (float held = 0f; held < AFTER_COUNT_CHECKED; held += STEP)
            {
                volley.Update(STEP);
                Assert.False(volley.Running);
                Assert.True(volley.Engaged, $"the lens was let go {held:0.00} s after the count");
            }

            volley.Reset();
            Assert.False(volley.Engaged);
            Assert.Equal(0f, volley.Blend);
            Assert.Equal(0, volley.Update(STEP));
            Assert.Equal(0f, volley.Blend);
        }
    }
}
