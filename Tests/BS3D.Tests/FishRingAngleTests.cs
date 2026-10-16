using Prazsky.Core.Render;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The sea's fish never turn tail first, however long the session has run (#903). <c>Fish.fx</c> built the ring
    /// angle as <c>Time * SwimSpeed / radius</c> with a radius that itself breathes with <c>Time</c>, so the angle's rate
    /// was S/r - Time*S*r'/r^2, whose second term grows with the clock and ran the angle backwards for the fish whose
    /// radius was growing (the nose is the tangent of the growing angle, so those swam tail first). The shader now
    /// builds the angle from the BASE radius. The sweeps are a CPU mirror of that one expression (the constants the
    /// school owns are read from <see cref="FishSchool"/>), so on their own they would stay green if the shader were
    /// reverted; the source tests below read <c>Fish.fx</c> and pin the three lines the mirror copies, so the mirror
    /// cannot drift from the shader unseen. The real shader was also checked on frames with a throwaway probe colouring
    /// every fish whose place a fifth of a second later lies behind its own nose. The old expression is mirrored too,
    /// and the source check is run on the old text, so both are seen to fire on the case they exist for.
    /// </summary>
    public class FishRingAngleTests
    {
        //Fish.fx's own figures, not the school's: the wobble 0.08 * sin(0.7 t + fish) and the radius's breathing
        //0.8 * sin(0.31 t + 1.7 fish)
        private const double WOBBLE_AMPLITUDE = 0.08, WOBBLE_RATE = 0.7;
        private const double BREATH_AMPLITUDE = 0.8, BREATH_RATE = 0.31, BREATH_PHASE = 1.7;

        private const int FISH_COUNT = 110;

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        /// <summary>The shader's code with its comments taken out and every run of white space made one space.</summary>
        private static string Code(string shader) => Regex.Replace(Regex.Replace(shader, @"//[^\r\n]*", ""), @"\s+", " ");

        /// <summary>
        /// Whether the code still has the three lines the mirror above copies: the base radius, the breathing radius
        /// round it and the angle built from the BASE one with the wobble's figures. A change to any of them (the
        /// breathing radius back under the clock, a different wobble or breath) has to be made here too.
        /// </summary>
        private static bool HasTheRingTheMirrorCopies(string shader)
        {
            string code = Code(shader);
            return code.Contains("float baseRadius = lerp(SchoolRadii.x, SchoolRadii.y, h.x);", StringComparison.Ordinal)
                && code.Contains("float radius = baseRadius + 0.8 * sin(Time * 0.31 + fish * 1.7);", StringComparison.Ordinal)
                && code.Contains("float angle = h.z * 6.2831853 + Time * SwimSpeed / max(baseRadius, 1.0) + 0.08 * sin(Time * 0.7 + fish);", StringComparison.Ordinal);
        }

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
        public void TheShaderStillBuildsTheAngleFromTheBaseRadius()
        {
            string shader = File.ReadAllText(FindUnderRepository(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Fish.fx")));
            Assert.True(HasTheRingTheMirrorCopies(shader), "Fish.fx no longer has the ring lines FishRingAngleTests mirrors: change the mirror with them");
        }

        [Fact]
        public void TheSourceCheckFiresOnTheOldAngleAndOnAChangedWobble()
        {
            //Seen to fire (BestPractices.md, section 10), on the shader text as it stood and on the likeliest drifts
            string shader = File.ReadAllText(FindUnderRepository(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Fish.fx")));
            const string now = "Time * SwimSpeed / max(baseRadius, 1.0)";
            Assert.Contains(now, Code(shader), StringComparison.Ordinal);
            Assert.False(HasTheRingTheMirrorCopies(shader.Replace(now, "Time * SwimSpeed / max(radius, 1.0)", StringComparison.Ordinal)));
            Assert.False(HasTheRingTheMirrorCopies(shader.Replace("0.08 * sin(Time * 0.7", "0.3 * sin(Time * 0.7", StringComparison.Ordinal)));
            Assert.False(HasTheRingTheMirrorCopies(shader.Replace("0.8 * sin(Time * 0.31", "1.8 * sin(Time * 0.31", StringComparison.Ordinal)));
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
