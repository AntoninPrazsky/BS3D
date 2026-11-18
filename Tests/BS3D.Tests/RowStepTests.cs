using BS3D.Screens;
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

        private static readonly float[] SENSITIVITY = [0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f];

        [Theory]
        [InlineData(1f, -1, 0.75f)]
        [InlineData(1f, 1, 1.25f)]
        [InlineData(0.5f, -1, 0.5f)]
        [InlineData(3f, 1, 3f)]
        //A rung read back from a settings file a hair off itself is still that rung, not the one before it
        [InlineData(0.99999f, 1, 1.25f)]
        [InlineData(1.00001f, -1, 0.75f)]
        public void ASensitivityStepsARungEitherWayAndHoldsAtItsEnds(float rung, int direction, float expected) =>
            Assert.Equal(expected, RowStep.OnRungs(SENSITIVITY, rung, direction), 5);

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
