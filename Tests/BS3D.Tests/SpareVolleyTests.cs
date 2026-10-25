using BS3D.Effects;
using Microsoft.Xna.Framework;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The unused balls fired off and counted (#881): every ball leaves once, the count climbs with them to exactly the bonus,
    /// a skip lands on the same figure, a whole budget is fired within the cap, and the lens is let go after the last.
    /// </summary>
    public class SpareVolleyTests
    {
        private static SpareVolley Begun(int balls, int bonus)
        {
            var volley = new SpareVolley();
            volley.Begin(balls, bonus, Vector3.Zero, new Vector3(0f, 1f, -2f), Vector3.Normalize(new Vector3(0f, 0.3f, -1f)),
                new Vector3(0f, 3f, 12f), new Vector3(0f, 2f, -10f), MathHelper.ToRadians(55f));
            return volley;
        }

        [Theory]
        [InlineData(1, 160)]
        [InlineData(7, 1120)]
        [InlineData(29, 1624)]
        [InlineData(60, 3001)]
        public void EveryBallLeavesOnceAndTheCountEndsOnTheBonus(int balls, int bonus)
        {
            SpareVolley volley = Begun(balls, bonus);
            int fired = 0, lastCount = 0;
            float t = 0f;
            while (volley.Engaged && t < 20f)
            {
                fired += volley.Update(1f / 144f);
                t += 1f / 144f;
                Assert.True(volley.Counted >= lastCount, "the count went down");
                lastCount = volley.Counted;
            }

            Assert.Equal(balls, fired);
            Assert.Equal(bonus, volley.Counted);
            Assert.False(volley.Engaged);
            Assert.True(t <= SpareVolley.Duration(balls) + 0.5f + 0.01f, $"held {t:0.00} s");
        }

        [Fact]
        public void AWholeBudgetFitsTheCapAndAFewKeepTheirCadence()
        {
            Assert.True(SpareVolley.Duration(60) <= SpareVolley.LEAD_IN + SpareVolley.MAX_SECONDS + SpareVolley.TAIL + 1e-4f);
            Assert.Equal(SpareVolley.MAX_INTERVAL, SpareVolley.Interval(5), 4);
            Assert.True(SpareVolley.Interval(200) >= SpareVolley.MIN_INTERVAL);
        }

        [Fact]
        public void ASkipLandsOnTheSameFigureAndLetsTheLensGo()
        {
            SpareVolley volley = Begun(29, 1624);
            int fired = 0;
            for (int i = 0; i < 100; i++) fired += volley.Update(1f / 60f);
            Assert.True(fired > 0 && fired < 29, $"{fired} fired before the skip");

            Assert.True(volley.TrySkip());
            Assert.Equal(1624, volley.Counted);
            Assert.False(volley.Running);
            Assert.False(volley.TrySkip());

            //No more leave after it, and the lens goes back
            int after = 0;
            for (int i = 0; i < 120; i++) after += volley.Update(1f / 60f);
            Assert.Equal(0, after);
            Assert.False(volley.Engaged);
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
    }
}
