using BS3D.Screens;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// How far a mouse-wheel notch moves a menu list (#952), held as numbers. The rules are <see cref="WheelScroll"/>'s
    /// alone; the widget that applies them (<c>MenuScrollViewer</c>) needs Myra and a device, and what it does with a real
    /// list at three resolutions is in "The mouse wheel over a list" in docs/game-shell/menu-navigation.md.
    /// </summary>
    public class WheelScrollTests
    {
        private const float NOTCH = 120f;

        [Fact]
        public void ANotchTowardsThePlayerMovesAFifthOfTheViewportDownTheList()
        {
            Assert.Equal(180f, WheelScroll.Pixels(-NOTCH, 900), 3);
        }

        [Fact]
        public void ANotchAwayFromThePlayerMovesTheSameDistanceUp()
        {
            Assert.Equal(-180f, WheelScroll.Pixels(NOTCH, 900), 3);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(513)]
        [InlineData(1141)]
        [InlineData(2000)]
        public void ANotchIsTheSameShareOfTheViewportWhateverItsSize(int viewport)
        {
            //Resolution independence: the share is the number, not the pixels. Myra's step was the same ~20 px at 1080p and at 4K
            Assert.Equal(0.2f, WheelScroll.Pixels(-NOTCH, viewport) / viewport, 4);
        }

        [Fact]
        public void TheStepDoesNotDependOnHowLongTheContentIs()
        {
            //What raising Myra's ScrollMultiplier would not have fixed: its step was ten times content over viewport, so a long
            //list jumped further than a short one. Here the content only sets where the list stops.
            float carry = 0f;
            int shortList = WheelScroll.Move(0, 400, WheelScroll.Pixels(-NOTCH, 900), ref carry);

            carry = 0f;
            int longList = WheelScroll.Move(0, 40_000, WheelScroll.Pixels(-NOTCH, 900), ref carry);

            Assert.Equal(180, shortList);
            Assert.Equal(180, longList);
        }

        [Fact]
        public void SeveralNotchesInOneFrameMoveSeveralSteps()
        {
            //A free-spinning wheel lands 3 detents in one frame, one delta of 360; Myra moved one step for it
            Assert.Equal(3f * WheelScroll.Pixels(-NOTCH, 900), WheelScroll.Pixels(-3 * NOTCH, 900), 3);
        }

        [Fact]
        public void AHalfNotchMovesHalfAStep()
        {
            Assert.Equal(WheelScroll.Pixels(-NOTCH, 900) / 2f, WheelScroll.Pixels(-NOTCH / 2f, 900), 3);
        }

        [Fact]
        public void SmallDeltasAddUpToTheWholeStep()
        {
            //A high-resolution wheel reports eighths of a notch: eight of them are one notch to the pixel, and none rounds to nothing
            const int viewport = 1203;
            float carry = 0f;
            int position = 0;

            for (int i = 0; i < 8; i++)
                position = WheelScroll.Move(position, 10_000, WheelScroll.Pixels(-NOTCH / 8f, viewport), ref carry);

            float carry1 = 0f;
            int oneNotch = WheelScroll.Move(0, 10_000, WheelScroll.Pixels(-NOTCH, viewport), ref carry1);

            Assert.Equal(oneNotch, position);
        }

        [Fact]
        public void TheListStopsAtItsTwoEnds()
        {
            float carry = 0f;

            Assert.Equal(500, WheelScroll.Move(450, 500, 300f, ref carry));
            Assert.Equal(0, WheelScroll.Move(100, 500, -300f, ref carry));
        }

        [Fact]
        public void ATurnBackFromAnEndStartsFromTheEndNotFromWhereTheWheelWouldHaveBeen()
        {
            //A wheel turned 100 px past the bottom moved nothing past it, so 50 px back is 50 px above the bottom, not 50 px above where the push would have taken it
            float carry = 0f;
            int position = WheelScroll.Move(450, 500, 100.4f, ref carry);

            Assert.Equal(500, position);
            Assert.Equal(0f, carry);

            position = WheelScroll.Move(position, 500, -50f, ref carry);

            Assert.Equal(450, position);
        }

        [Fact]
        public void AListThatDoesNotScrollStaysAtZero()
        {
            float carry = 0f;

            Assert.Equal(0, WheelScroll.Move(0, 0, 300f, ref carry));
            Assert.Equal(0, WheelScroll.Move(0, -20, 300f, ref carry));
        }

        [Fact]
        public void TheCarryIsTheFractionThatWasNotPlaced()
        {
            float carry = 0f;

            int position = WheelScroll.Move(0, 1000, 10.75f, ref carry);

            Assert.Equal(10, position);
            Assert.Equal(0.75f, carry, 4);
        }

        [Fact]
        public void TheBoostIsNeutralForTheFirstEventAndForOnesThatComeApartSlowly()
        {
            WheelScroll.Momentum momentum = new();

            Assert.Equal(1f, momentum.Take(10.0, -NOTCH));
            //A notch a few times a second is a deliberate scroll: every one is a slow step
            Assert.Equal(1f, momentum.Take(10.0 + 2 * WheelScroll.STREAK_WINDOW, -NOTCH));
            Assert.Equal(1f, momentum.Take(10.0 + 4 * WheelScroll.STREAK_WINDOW, -NOTCH));
        }

        [Fact]
        public void NotchesFollowingEachOtherQuicklyGrowTheBoostToItsCap()
        {
            WheelScroll.Momentum momentum = new();
            double now = 5.0;
            float last = 0f;

            for (int i = 0; i < 12; i++)
            {
                float boost = momentum.Take(now, -NOTCH);

                Assert.True(boost >= last, "the boost never falls inside a spin");
                Assert.InRange(boost, 1f, WheelScroll.MAX_BOOST);
                last = boost;
                now += 0.04;
            }

            Assert.Equal(WheelScroll.MAX_BOOST, last);
        }

        [Fact]
        public void TheBoostGrowsByHalfAStepForEachNotchAlreadyTurned()
        {
            WheelScroll.Momentum momentum = new();

            Assert.Equal(1f, momentum.Take(1.00, -NOTCH));
            Assert.Equal(1.5f, momentum.Take(1.05, -NOTCH));
            Assert.Equal(2f, momentum.Take(1.10, -NOTCH));
            Assert.Equal(2.5f, momentum.Take(1.15, -NOTCH));
        }

        [Fact]
        public void APauseStartsTheSpinAgain()
        {
            WheelScroll.Momentum momentum = new();
            momentum.Take(1.00, -NOTCH);
            momentum.Take(1.04, -NOTCH);
            Assert.True(momentum.Take(1.08, -NOTCH) > 1f);

            Assert.Equal(1f, momentum.Take(1.08 + WheelScroll.STREAK_WINDOW * 1.5, -NOTCH));
        }

        [Fact]
        public void TurningTheWheelTheOtherWayStartsTheSpinAgain()
        {
            WheelScroll.Momentum momentum = new();
            momentum.Take(1.00, -NOTCH);
            momentum.Take(1.04, -NOTCH);
            Assert.True(momentum.Take(1.08, -NOTCH) > 1f);

            //Back up the list at once: a reversal is a correction and must not be thrown four steps at a time
            Assert.Equal(1f, momentum.Take(1.12, NOTCH));
        }

        [Fact]
        public void ThreeNotchesLandingInOneFrameAreThreeSlowStepsAndSpeedTheNextEventUp()
        {
            WheelScroll.Momentum momentum = new();

            //The event's own notches are paid at the rate before it, so one frame's burst is not squared
            Assert.Equal(1f, momentum.Take(1.00, -3 * NOTCH));
            Assert.Equal(2.5f, momentum.Take(1.016, -NOTCH));
        }

        [Fact]
        public void AZeroDeltaChangesNothing()
        {
            WheelScroll.Momentum momentum = new();
            momentum.Take(1.00, -NOTCH);

            Assert.Equal(1f, momentum.Take(1.02, 0f));
            Assert.Equal(1.5f, momentum.Take(1.04, -NOTCH));
        }

        [Fact]
        public void ABoostedStepIsTheSlowStepTimesTheBoost()
        {
            Assert.Equal(2.5f * WheelScroll.Pixels(-NOTCH, 770), WheelScroll.Pixels(-NOTCH, 770, 2.5f), 3);
        }

        [Fact]
        public void TheFastestSpinStillLeavesAFifthOfTheViewportOnScreen()
        {
            //No line may pass unseen between two events: the cap is the page-key share (HelpPage.PAGE_KEY_SHARE, 0.8)
            Assert.True(WheelScroll.VIEWPORT_SHARE * WheelScroll.MAX_BOOST <= 0.8f + 1e-6f);
        }

        /// <summary>
        /// The Scene list as the running game laid it out (a probe in <c>MenuScrollViewer</c>, 2026-10-22, Myra 1.6.3): at
        /// 1920x1080 the viewport is 770 px and the range 708, at 3840x1600 1127 and 1038. A notch of Myra's was 19 px in
        /// both, 38 and 55 notches to cross the list; a slow notch of this step is 154 and 225 px, five of them.
        /// </summary>
        [Theory]
        [InlineData(770, 708, 154, 5)]
        [InlineData(1127, 1038, 225, 5)]
        public void TheSceneListIsCrossedInFiveSlowNotches(int viewport, int maximum, int notchPixels, int notches)
        {
            Assert.Equal(notchPixels, (int)Math.Round(WheelScroll.Pixels(-NOTCH, viewport)));

            float carry = 0f;
            int position = 0;
            int turned = 0;

            while (position < maximum)
            {
                position = WheelScroll.Move(position, maximum, WheelScroll.Pixels(-NOTCH, viewport), ref carry);
                turned++;
            }

            Assert.Equal(notches, turned);
        }

        /// <summary>
        /// The same two lists under a fast spin, a notch every 40 ms: the boost brings the end to four events instead of five.
        /// </summary>
        [Theory]
        [InlineData(770, 708, 4)]
        [InlineData(1127, 1038, 4)]
        public void AFastSpinCrossesTheSceneListInFourNotches(int viewport, int maximum, int notches)
        {
            WheelScroll.Momentum momentum = new();
            float carry = 0f;
            int position = 0;
            int turned = 0;
            double now = 3.0;

            while (position < maximum)
            {
                float boost = momentum.Take(now, -NOTCH);
                position = WheelScroll.Move(position, maximum, WheelScroll.Pixels(-NOTCH, viewport, boost), ref carry);
                turned++;
                now += 0.04;
            }

            Assert.Equal(notches, turned);
        }

        /// <summary>
        /// The other measured lists, with the whole-notch count that crossed each before: Help's Odd balls tab took 41 and 58
        /// notches at 23 and 22 pixels each, the Jukebox's recordings 21 and 28 at 13. Slow notches of this step from the top
        /// reach the end of every one in no more than seven.
        /// </summary>
        [Theory]
        //Help's Odd balls tab, the longest body, at 1920x1080 and 3840x1600 (Myra: 23 and 22 px)
        [InlineData(694, 941, 7)]
        [InlineData(1015, 1267, 7)]
        //The Jukebox's recordings at 3840x1600, and its chapters beside them (Myra: 13 px, so 21 and 28 notches)
        [InlineData(717, 268, 2)]
        [InlineData(1127, 358, 2)]
        public void TheOtherMeasuredListsAreCrossedInAFewSlowNotches(int viewport, int maximum, int notches)
        {
            float carry = 0f;
            int position = 0;
            int turned = 0;

            while (position < maximum)
            {
                position = WheelScroll.Move(position, maximum, WheelScroll.Pixels(-NOTCH, viewport), ref carry);
                turned++;
            }

            Assert.Equal(notches, turned);
        }
    }
}
