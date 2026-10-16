using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The sea's fish never turn tail first, however long the session has run (#903). <c>Fish.fx</c> built the ring
    /// angle as <c>Time * SwimSpeed / radius</c> with a radius that itself breathes with <c>Time</c>, so the angle's rate
    /// was S/r - Time*S*r'/r^2, whose second term grows with the clock and ran the angle backwards for the fish whose
    /// radius was growing (the nose is the tangent of the growing angle, so those swam tail first). The shader now
    /// builds the angle from the BASE radius. These tests are a CPU mirror of that one expression, kept in step with
    /// the shader by hand (the constants the school owns are read from <see cref="FishSchool"/>); the real shader was
    /// checked on frames with a throwaway probe colouring every fish whose place a fifth of a second later lies behind
    /// its own nose. The old expression is mirrored too, so the sweep is seen to fire on the case it exists for.
    /// </summary>
    public class FishRingAngleTests
    {
        //Fish.fx's own figures, not the school's: the wobble 0.08 * sin(0.7 t + fish) and the radius's breathing
        //0.8 * sin(0.31 t + 1.7 fish)
        private const double WOBBLE_AMPLITUDE = 0.08, WOBBLE_RATE = 0.7;
        private const double BREATH_AMPLITUDE = 0.8, BREATH_RATE = 0.31, BREATH_PHASE = 1.7;

        private const int FISH_COUNT = 110;

        private static double Wobble(double t, int fish) => WOBBLE_AMPLITUDE * Math.Sin(WOBBLE_RATE * t + fish);

        private static double BaseRadius(double hx) =>
            FishSchool.SCHOOL_RADIUS_IN + (FishSchool.SCHOOL_RADIUS_OUT - FishSchool.SCHOOL_RADIUS_IN) * hx;

        private static double Radius(double hx, double t, int fish) =>
            BaseRadius(hx) + BREATH_AMPLITUDE * Math.Sin(BREATH_RATE * t + fish * BREATH_PHASE);

        /// <summary>The angle round the ring as the shader builds it now: the base radius, so no clock term in it.</summary>
        private static double AngleNow(double hx, double t, int fish) =>
            t * FishSchool.SWIM_SPEED / Math.Max(BaseRadius(hx), 1.0) + Wobble(t, fish);

        /// <summary>The angle as the shader built it before #903: the breathing radius under the clock.</summary>
        private static double AngleBefore(double hx, double t, int fish) =>
            t * FishSchool.SWIM_SPEED / Math.Max(Radius(hx, t, fish), 1.0) + Wobble(t, fish);

        /// <summary>The slowest rate of the angle over a clock span, by differences over a tenth of a second.</summary>
        private static double SlowestRate(Func<double, double, int, double> angle, double from, double span)
        {
            const double STEP = 0.1;
            double slowest = double.MaxValue;
            for (int fish = 0; fish < FISH_COUNT; fish++)
            {
                for (double hx = 0.0; hx <= 1.0; hx += 0.125)
                {
                    for (double t = from; t < from + span; t += 0.37)
                        slowest = Math.Min(slowest, (angle(hx, t + STEP, fish) - angle(hx, t, fish)) / STEP);
                }
            }
            return slowest;
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(60.0)]
        [InlineData(600.0)]
        [InlineData(3600.0)]
        [InlineData(36000.0)]
        public void TheRingAngleAlwaysAdvances(double from)
        {
            Assert.True(SlowestRate(AngleNow, from, 120.0) > 0.0);
        }

        [Fact]
        public void TheRateIsTheSameAtAnyClock()
        {
            //Constant per fish: the slowest rate of a late window is the slowest rate of an early one
            double early = SlowestRate(AngleNow, 0.0, 120.0);
            double late = SlowestRate(AngleNow, 36000.0, 120.0);
            Assert.InRange(late, early - 0.01, early + 0.01);
        }

        [Fact]
        public void TheWobbleCannotTurnTheRingBackAtTheSchoolsSpeed()
        {
            //S / baseRadius at the outer edge against the wobble's fastest backward rate: positive for S above 21 * 0.056,
            //the 1.18 a second the issue names. A slower school (step 3 of #903) must keep S above it.
            double wobbleBackwards = WOBBLE_AMPLITUDE * WOBBLE_RATE;
            double slowest = FishSchool.SWIM_SPEED / FishSchool.SCHOOL_RADIUS_OUT - wobbleBackwards;
            Assert.True(slowest > 0.0, $"the outer ring turns at {slowest} rad/s at its slowest");
            Assert.InRange(FishSchool.SCHOOL_RADIUS_OUT * wobbleBackwards, 1.17, 1.19);
        }

        [Fact]
        public void TheOldAngleWentBackwardsWithTheClock()
        {
            //The sweep above has to be seen to fire (BestPractices.md, section 10): the old expression turns back
            //at a late clock and not at the start
            Assert.True(SlowestRate(AngleBefore, 600.0, 120.0) < 0.0);
            Assert.True(SlowestRate(AngleBefore, 0.0, 20.0) > 0.0);
        }
    }
}
