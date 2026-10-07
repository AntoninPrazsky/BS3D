using BS3D.Screens;
using Prazsky.BS3D.Levels;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The ceiling's brake (#213): which levels come with one, and what it does to the glass. The grant is a rule of the
    /// campaign's shape (<see cref="LevelSet.BrakeChargesAt"/>); the lift is <see cref="CeilingDescent"/>'s, a state
    /// machine over floats that needs no device, so it is driven here the way the session drives it — a step queued and
    /// let go through <c>Update</c>, the glass slid by physics steps — and what is held is that a brake gives back exactly
    /// a step, never more than the level hung, is refused (and so not spent) with the glass at rest, and leaves the
    /// rest of the descent's bookkeeping alone.
    /// </summary>
    public class BrakeTests
    {
        private const float STEP = CeilingDescent.CEILING_DESCENT_PER_STEP;
        private const float PHYSICS_STEP = 1f / 120f;

        private static LevelSet ShippedSet() =>
            LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

        private static CeilingDescent Hanging()
        {
            CeilingDescent glass = new();
            glass.Reset(ClusterHang.DEATH_Y + 8f, feedFloorLevel: 0);
            return glass;
        }

        /// <summary>Queues one pressure step and lets it go, the way a shot that owed one would.</summary>
        private static void TakeAStep(CeilingDescent glass)
        {
            glass.QueuePressureStep();
            Assert.True(glass.Update(1f, levelDecided: false, takeoverEngaged: false, out _, out _));
        }

        /// <summary>Slides the glass to wherever it is going, as the physics steps do.</summary>
        private static void SlideToArrival(CeilingDescent glass)
        {
            int steps = 0;
            while (glass.Slide(PHYSICS_STEP)) if (++steps > 10_000) Assert.Fail("the glass never arrived");
        }

        private static IEnumerable<int> Grants(LevelSet set)
        {
            for (int index = 0; index < set.Count; index++) yield return set.BrakeChargesAt(index);
        }

        [Fact]
        public void TheShippedFourthChapterOnwardHasABrakeWhereTheCeilingSteps()
        {
            LevelSet set = ShippedSet();

            for (int index = 0; index < set.Count; index++)
            {
                bool wanted = set.BlockNumber(index) >= LevelSet.BRAKE_FROM_BLOCK && set.Levels[index].CeilingStep.HasValue;
                Assert.Equal(wanted ? 1 : 0, set.BrakeChargesAt(index));
            }

            //And the boundary is pinned to the shipped set itself rather than to the constant (#705: one tool a chapter, the
            //Brake the fourth's): the Tower opens the fourth chapter at index 30, the Coil's last level before it grants none,
            //and the first of the Tower's levels whose glass steps grants one
            set.BlockRange(30, out int firstOfFourth, out _);
            Assert.Equal(30, firstOfFourth);
            for (int index = 0; index < 30; index++) Assert.Equal(0, set.BrakeChargesAt(index));
            int firstStepping = 30;
            while (!set.Levels[firstStepping].CeilingStep.HasValue) firstStepping++;
            Assert.True(firstStepping < 40, "no level of the Tower steps its glass");
            Assert.Equal(1, set.BrakeChargesAt(firstStepping));
            Assert.Contains(Grants(set), granted => granted > 0);
        }

        [Fact]
        public void ALevelWhoseCeilingHoldsHasNoBrakeAndNeitherHasAnIndexOutsideOrASetWithoutChapters()
        {
            LevelSet set = new();
            //Every block's ceiling steps except one level's in the fourth, so a constant set lower than the fourth block
            //would grant in the first three and fail here
            for (int i = 0; i < 3; i++) set.Levels.Add(new LevelSetEntry { File = $"a{i}.json", Name = $"A{i}", Block = "A", CeilingStep = 8 });
            for (int i = 0; i < 3; i++) set.Levels.Add(new LevelSetEntry { File = $"b{i}.json", Name = $"B{i}", Block = "B", CeilingStep = 8 });
            for (int i = 0; i < 3; i++) set.Levels.Add(new LevelSetEntry { File = $"c{i}.json", Name = $"C{i}", Block = "C", CeilingStep = 8 });
            for (int i = 0; i < 3; i++)
                set.Levels.Add(new LevelSetEntry { File = $"d{i}.json", Name = $"D{i}", Block = "D", CeilingStep = i == 1 ? null : 8 });
            for (int i = 0; i < 3; i++) set.Levels.Add(new LevelSetEntry { File = $"e{i}.json", Name = $"E{i}", Block = "E", CeilingStep = 8 });

            //The fourth block on grants where the glass steps and not where it holds still
            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1 }, new List<int>(Grants(set)).ToArray());

            Assert.Equal(0, set.BrakeChargesAt(-1));
            Assert.Equal(0, set.BrakeChargesAt(set.Count));
            Assert.Equal(0, new LevelSet().BrakeChargesAt(0));

            LevelSet loose = new();
            for (int i = 0; i < 25; i++) loose.Levels.Add(new LevelSetEntry { File = $"l{i}.json", Name = $"L{i}", CeilingStep = 8 });
            Assert.All(Grants(loose), granted => Assert.Equal(0, granted));
        }

        [Fact]
        public void ABrakeWithTheGlassAtRestIsRefusedAndMovesNothing()
        {
            CeilingDescent glass = Hanging();

            Assert.False(glass.CanBrake);
            Assert.False(glass.Brake());
            Assert.Equal(glass.RestY, glass.TargetY);
            Assert.Equal(glass.RestY, glass.Y);
            Assert.False(glass.Slide(PHYSICS_STEP));
            Assert.Equal(0f, glass.Flash);
        }

        [Fact]
        public void ABrakeGivesBackExactlyOneStepAndTheGlassSlidesUpToIt()
        {
            CeilingDescent glass = Hanging();
            float rest = glass.RestY;

            TakeAStep(glass);
            TakeAStep(glass);
            SlideToArrival(glass);
            Assert.Equal(rest - 2 * STEP, glass.Y, 4);

            Assert.True(glass.CanBrake);
            Assert.True(glass.Brake());

            //Up, and by a physics step's worth at a time: the first slide moves it less than the whole step
            float before = glass.Y;
            Assert.True(glass.Slide(PHYSICS_STEP));
            Assert.InRange(glass.Y - before, 1e-5f, STEP / 2f);

            SlideToArrival(glass);
            Assert.Equal(rest - STEP, glass.Y, 4);
            Assert.Equal(glass.TargetY, glass.Y);
        }

        [Fact]
        public void TheBrakeNeverLiftsTheGlassPastWhereTheLevelHungIt()
        {
            CeilingDescent glass = Hanging();
            TakeAStep(glass);
            SlideToArrival(glass);

            Assert.True(glass.Brake());
            SlideToArrival(glass);

            //Back at rest: a second brake has nothing to give, and the level's own rest height is the ceiling of it
            Assert.False(glass.CanBrake);
            Assert.False(glass.Brake());
            Assert.Equal(glass.RestY, glass.Y, 4);
        }

        [Fact]
        public void TheClampAtRestIsReachedWhenTheDeathLineLeftTheGlassAPartStepBelowIt()
        {
            //A glass hung 1.0 above the death line: the second step is clamped to 1.0 below rest rather than 1.2, so the
            //lifts land on 0.4 below rest and then have less than a step to give, which is where Math.Min bites
            CeilingDescent glass = new();
            glass.Reset(ClusterHang.DEATH_Y + 1f, feedFloorLevel: 0);
            float rest = glass.RestY;

            TakeAStep(glass);
            TakeAStep(glass);
            SlideToArrival(glass);
            Assert.Equal(ClusterHang.DEATH_Y, glass.Y, 4);

            Assert.True(glass.Brake());
            SlideToArrival(glass);
            Assert.Equal(ClusterHang.DEATH_Y + STEP, glass.Y, 4);

            Assert.True(glass.Brake());
            SlideToArrival(glass);
            Assert.Equal(rest, glass.Y, 4);
            Assert.True(glass.Y <= rest);
        }

        [Fact]
        public void TheBrakeGivesBackPressureStepsAndNeverAFeedStep()
        {
            CeilingDescent glass = Hanging();

            //A tall level's feed lowers the glass to keep the column where the level was authored for it; five levels
            //of the underside cleared (3.5 units) owe five whole steps, and they come down one at a time
            Assert.Equal(5, glass.Feed(lowest: 5, out _));
            for (int i = 0; i < 5; i++) Assert.True(glass.Update(1f, false, false, out bool feeding, out _) && feeding);
            SlideToArrival(glass);
            Assert.True(glass.TargetY < glass.RestY);

            //The glass is well below rest and the brake still has nothing to give: those were not the pressure's
            Assert.False(glass.CanBrake);
            Assert.False(glass.Brake());

            //One pressure step on top of them is one the brake can return, and only that one
            TakeAStep(glass);
            SlideToArrival(glass);
            float low = glass.Y;
            Assert.True(glass.Brake());
            SlideToArrival(glass);
            Assert.Equal(low + STEP, glass.Y, 4);
            Assert.False(glass.CanBrake);
        }

        [Fact]
        public void ABrakePressedMidDescentLiftsTheTargetBackFromWhereTheGlassIsGoing()
        {
            CeilingDescent glass = Hanging();
            float rest = glass.RestY;

            //Two steps begun and the glass only partway down when the brake is pressed: the target moves back up a
            //step from where the glass was going, and it arrives there rather than where it started
            TakeAStep(glass);
            TakeAStep(glass);
            for (int i = 0; i < 30; i++) glass.Slide(PHYSICS_STEP);

            Assert.True(glass.Brake());
            SlideToArrival(glass);
            Assert.Equal(rest - STEP, glass.Y, 4);
        }

        [Fact]
        public void AStepBegunWhileTheGlassIsRisingTurnsItRound()
        {
            CeilingDescent glass = Hanging();
            float rest = glass.RestY;

            TakeAStep(glass);
            TakeAStep(glass);
            SlideToArrival(glass);
            Assert.True(glass.Brake());
            for (int i = 0; i < 10; i++) glass.Slide(PHYSICS_STEP);

            //The ceiling's next step arrives mid-lift: the glass now heads to a target one step lower than the one it
            //was rising to, and goes there
            TakeAStep(glass);
            SlideToArrival(glass);
            Assert.Equal(rest - 2 * STEP, glass.Y, 4);
        }

        [Fact]
        public void TheBrakeAnnouncesInTheFeedsBlueAndNotThePressuresRed()
        {
            CeilingDescent glass = Hanging();
            TakeAStep(glass);
            Assert.False(glass.FlashIsFeed);
            Assert.Equal(CeilingDescent.CEILING_FLASH_COLOR, glass.FlashColor);

            glass.Brake();

            Assert.True(glass.FlashIsFeed);
            Assert.Equal(CeilingDescent.CEILING_FEED_COLOR, glass.FlashColor);
            Assert.Equal(1f, glass.Flash);
        }

        [Fact]
        public void TheBrakeLeavesAStepTheLastShotOwesAlone()
        {
            CeilingDescent glass = Hanging();
            TakeAStep(glass);
            SlideToArrival(glass);

            //A step the last shot owes, still waiting out its hold
            glass.QueuePressureStep();
            Assert.True(glass.Brake());

            //It comes down after the lift, as it would have: the brake bought back one step and the owed one is a
            //different debt, so the glass ends where it was
            Assert.True(glass.Update(1f, levelDecided: false, takeoverEngaged: false, out bool feeding, out _));
            Assert.False(feeding);
            SlideToArrival(glass);
            Assert.Equal(glass.RestY - STEP, glass.Y, 4);
        }
    }
}
