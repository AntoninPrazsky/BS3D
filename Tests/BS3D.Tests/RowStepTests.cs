using System;
using BS3D.Screens;
using Prazsky.Core;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What a cycling Settings row's − and + promise (#1151): one rung less or more either way, held at the ladder's ends
    /// where the row's own press wraps, so the audit's Volume that went 100, 75, 50, 25, Off and back to 100 on a touch
    /// screen is one + from 75 back to 100, and a + never lands on Off.
    /// </summary>
    public class RowStepTests
    {
        private const float QUARTER = 0.25f;

        [Theory]
        [InlineData(1f, -1, 0.75f)]
        [InlineData(0.75f, 1, 1f)]
        [InlineData(0.25f, -1, 0f)]
        [InlineData(0f, 1, 0.25f)]
        //Held at the ends, where the row's own press wraps round
        [InlineData(1f, 1, 1f)]
        [InlineData(0f, -1, 0f)]
        public void AGainStepsAQuarterEitherWayAndHoldsAtOffAndFull(float gain, int direction, float expected) =>
            Assert.Equal(expected, RowStep.Gain(gain, QUARTER, direction), 5);

        [Fact]
        public void FromSeventyFiveBackToFullIsOnePlus()
        {
            //The audit's complaint: by the row's own press, 75 % went 50, 25, Off and only then 100
            Assert.Equal(1f, RowStep.Gain(0.75f, QUARTER, 1), 5);
        }

        [Theory]
        [InlineData(1f, 1, false)]
        [InlineData(1f, -1, true)]
        [InlineData(0f, -1, false)]
        [InlineData(0f, 1, true)]
        [InlineData(0.5f, -1, true)]
        [InlineData(0.5f, 1, true)]
        public void AnEndDimsWhereItCanStepNoFurther(float gain, int direction, bool can) =>
            Assert.Equal(can, RowStep.CanStepGain(gain, direction));

        [Theory]
        [InlineData(100, -1, 90)]
        [InlineData(100, 1, 110)]
        [InlineData(70, -1, 70)]
        [InlineData(130, 1, 130)]
        //A command line's exposure=1.0 is 91 %, off the ladder: either end steps onto it, as the row's press does
        [InlineData(91, 1, 100)]
        [InlineData(91, -1, 90)]
        public void TheBrightnessStepsARungEitherWayAndHoldsAtItsEnds(int percent, int direction, int expected) =>
            Assert.Equal(expected, RowStep.OnRungs(BrightnessLadder.Rungs, percent, direction));

        [Theory]
        [InlineData(1f, -1, 0.75f)]
        [InlineData(1f, 1, 1.25f)]
        [InlineData(0.5f, -1, 0.5f)]
        [InlineData(3f, 1, 3f)]
        //A rung read back from a settings file a hair off itself is still that rung, not the one before it
        [InlineData(0.99999f, 1, 1.25f)]
        [InlineData(1.00001f, -1, 0.75f)]
        //1.25 is the rung #477 added between 1 and 1.5: a step from either side lands on it, not past it
        [InlineData(1.5f, -1, 1.25f)]
        [InlineData(1.25f, 1, 1.5f)]
        public void ASensitivityStepsARungEitherWayAndHoldsAtItsEnds(float rung, int direction, float expected) =>
            Assert.Equal(expected, RowStep.OnRungs(SensitivityLadder.Rungs, rung, direction), 5);

        [Fact]
        public void TheSensitivityLadderIsWalkedEndToEndByItsPlusesAndBackByItsMinuses()
        {
            //Every rung reached from the bottom one + at a time, the top held, and back down the same way: the − and + ends
            //cover the game's whole ladder and nothing else
            ReadOnlySpan<float> rungs = SensitivityLadder.Rungs;
            float at = rungs[0];
            for (int i = 1; i < rungs.Length; i++)
            {
                at = RowStep.OnRungs(rungs, at, 1);
                Assert.Equal(rungs[i], at);
            }
            Assert.Equal(at, RowStep.OnRungs(rungs, at, 1));

            for (int i = rungs.Length - 2; i >= 0; i--)
            {
                at = RowStep.OnRungs(rungs, at, -1);
                Assert.Equal(rungs[i], at);
            }
            Assert.Equal(at, RowStep.OnRungs(rungs, at, -1));
        }

        [Theory]
        //The ladder stands native first, largest down (RenderResolution.Heights): + is a rung larger, − a rung smaller
        [InlineData(1600, 1, 1600)]
        [InlineData(1600, -1, 1330)]
        [InlineData(1330, 1, 1600)]
        [InlineData(1070, -1, 800)]
        [InlineData(530, -1, 530)]
        [InlineData(530, 1, 800)]
        //A height off the ladder (a command line's or an old settings file's) steps onto the nearest rung that way
        [InlineData(1000, 1, 1070)]
        [InlineData(1000, -1, 800)]
        public void TheResolutionStepsARungEitherWayAndHoldsAtNativeAndAtTheSmallest(int height, int direction, int expected) =>
            Assert.Equal(expected, RowStep.OnRungs(RenderResolution.Heights(3840, 1600), height, direction));

        [Theory]
        [InlineData(1, -1, 1)]
        [InlineData(1, 1, 2)]
        [InlineData(5, -1, 4)]
        [InlineData(SkyDome.Count, -1, SkyDome.Count - 1)]
        //Held at the last dome, where the row's own press wraps round to the first
        [InlineData(SkyDome.Count, 1, SkyDome.Count)]
        public void TheSkyStepsADomeEitherWayAndHoldsAtTheFirstAndTheLast(int dome, int direction, int expected) =>
            Assert.Equal(expected, RowStep.InRange(dome, 1, SkyDome.Count, direction));

        [Theory]
        //Less is less of what the tier draws: Potato under Low, though the row's own press comes to Potato after Ultra
        [InlineData(QualityLevel.Low, -1, QualityLevel.Potato)]
        [InlineData(QualityLevel.Potato, -1, QualityLevel.Potato)]
        [InlineData(QualityLevel.Potato, 1, QualityLevel.Low)]
        [InlineData(QualityLevel.Medium, 1, QualityLevel.High)]
        [InlineData(QualityLevel.High, -1, QualityLevel.Medium)]
        //The desktop offers Ultra (the tests run as one), and nothing is past it: a + never wraps to Potato
        [InlineData(QualityLevel.High, 1, QualityLevel.Ultra)]
        [InlineData(QualityLevel.Ultra, 1, QualityLevel.Ultra)]
        public void TheQualityStepsATierOfWhatItDrawsAndHoldsAtItsEnds(QualityLevel quality, int direction, QualityLevel expected) =>
            Assert.Equal(expected, quality.StepOnLadder(direction));
    }
}
