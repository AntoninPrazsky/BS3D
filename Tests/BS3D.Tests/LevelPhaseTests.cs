using Prazsky.BS3D.Levels;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The level's flow as <see cref="LevelPhases"/> states it (#582): the Game's one transition method refuses
    /// whatever <see cref="LevelPhases.CanEnter"/> refuses, so this is the whole table of what may follow what,
    /// and the two gates every door of the session asks.
    /// </summary>
    public class LevelPhaseTests
    {
        private static readonly LevelPhase[] All = Enum.GetValues<LevelPhase>();

        /// <summary>
        /// Exactly the five forward transitions and a build back to <see cref="LevelPhase.Playing"/> from
        /// anywhere — nothing out of <see cref="LevelPhase.Over"/> but a build, no second ending over a first.
        /// </summary>
        [Fact]
        public void OnlyTheLevelsOwnPathsAreAllowed()
        {
            (LevelPhase From, LevelPhase To)[] allowed =
            {
                (LevelPhase.Playing, LevelPhase.ClearedBeat),
                (LevelPhase.Playing, LevelPhase.LossHold),
                (LevelPhase.Playing, LevelPhase.Over),
                (LevelPhase.ClearedBeat, LevelPhase.Over),
                (LevelPhase.LossHold, LevelPhase.Over),
            };

            foreach (LevelPhase from in All)
                foreach (LevelPhase to in All)
                {
                    bool expected = to == LevelPhase.Playing || Array.IndexOf(allowed, (from, to)) >= 0;
                    Assert.True(expected == LevelPhases.CanEnter(from, to), $"{from} -> {to}");
                }
        }

        /// <summary>Walks both endings from a fresh build and back, the way a Retry does.</summary>
        [Theory]
        [InlineData(LevelPhase.ClearedBeat)]
        [InlineData(LevelPhase.LossHold)]
        public void AnEndingRunsForwardToOverAndABuildStartsOver(LevelPhase ending)
        {
            Assert.True(LevelPhases.CanEnter(LevelPhase.Playing, ending));
            Assert.True(LevelPhases.CanEnter(ending, LevelPhase.Over));
            Assert.False(LevelPhases.CanEnter(LevelPhase.Over, ending));
            Assert.True(LevelPhases.CanEnter(LevelPhase.Over, LevelPhase.Playing));
        }

        /// <summary>
        /// Decided is everything past play; final is the line's hold and the page, and not the cleared beat —
        /// a late landing still scores there, because the page has not taken the figures yet.
        /// </summary>
        [Fact]
        public void TheGatesReadThePhase()
        {
            Assert.False(LevelPhases.IsDecided(LevelPhase.Playing));
            Assert.True(LevelPhases.IsDecided(LevelPhase.ClearedBeat));
            Assert.True(LevelPhases.IsDecided(LevelPhase.LossHold));
            Assert.True(LevelPhases.IsDecided(LevelPhase.Over));

            Assert.False(LevelPhases.IsFinal(LevelPhase.Playing));
            Assert.False(LevelPhases.IsFinal(LevelPhase.ClearedBeat));
            Assert.True(LevelPhases.IsFinal(LevelPhase.LossHold));
            Assert.True(LevelPhases.IsFinal(LevelPhase.Over));
        }
    }
}
