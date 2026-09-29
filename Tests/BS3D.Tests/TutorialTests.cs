using BS3D.Screens;
using Prazsky.BS3D.Levels;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The tutorial's ladder over the shipped level set (#189, #649, #666): which chapter teaches what, and that the
    /// score waits for the second chapter. <c>Tutorial</c> is compiled in from the Game — it is rules and clocks, no
    /// device — and driven the way the session drives it: a level begun, frames stepped, the cards read off
    /// <c>Caption</c> as they come up. A card left up times out on its own clock (an action) or is read out (an
    /// informational one), so stepping long enough walks the whole queue without a player.
    /// </summary>
    public class TutorialTests
    {
        private const string STREAK = "Hit after hit multiplies your score";
        private const string BUDGET = "Spare shots pay a bonus at the end";
        private const string LINE_RULE = "If the cluster reaches the line, the level is lost";
        private const string SEND_OFF = "That's the basics — you know how to play";

        private static LevelSet ShippedSet() =>
            LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

        /// <summary>A save as a set of keys, and the tutorial reading and writing it.</summary>
        private static Tutorial Fresh(HashSet<string> save) =>
            new(key => save.Contains(key), key => save.Add(key), Tutorial.Mode.Normal);

        /// <summary>Begins the set's entry <paramref name="index"/> and returns every caption the level shows, in order.</summary>
        private static List<string> Play(Tutorial tutorial, LevelSet set, int index, bool lightStreak = false)
        {
            bool placed = Tutorial.TryPlace(set, index, out int chapter, out int levelInChapter);
            tutorial.BeginLevel(placed ? chapter : -1, levelInChapter, ceilingStep: 6);

            List<string> shown = new();
            for (int frame = 0; frame < 4000; frame++)
            {
                //The streak lights a little way in, as it would after a couple of landings
                if (lightStreak && frame == 20) tutorial.Trigger(Tutorial.Lesson.Streak);

                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                string caption = tutorial.Caption;
                if (caption != null && (shown.Count == 0 || shown[^1] != caption)) shown.Add(caption);
            }

            return shown;
        }

        [Fact]
        public void TheShippedChaptersArePlacedByTheirBlocks()
        {
            LevelSet set = ShippedSet();

            Assert.True(Tutorial.TryPlace(set, 0, out int chapter, out int level));
            Assert.Equal((0, 0), (chapter, level));

            //Amphora closes the first chapter since #649, and the Gallery opens the second
            Assert.Equal("Amphora", set.Levels[9].Name);
            Assert.True(Tutorial.TryPlace(set, 9, out chapter, out level));
            Assert.Equal((0, 9), (chapter, level));

            Assert.True(Tutorial.TryPlace(set, 10, out chapter, out level));
            Assert.Equal((1, 0), (chapter, level));

            //Nothing past the second chapter
            set.BlockRange(10, out _, out int lastOfSecond);
            Assert.True(Tutorial.TryPlace(set, lastOfSecond, out _, out _));
            Assert.False(Tutorial.TryPlace(set, lastOfSecond + 1, out _, out _));
            Assert.False(Tutorial.TryPlace(null, 0, out _, out _));
        }

        [Fact]
        public void TheFirstChapterNeverTeachesTheScore()
        {
            LevelSet set = ShippedSet();
            HashSet<string> save = new();
            Tutorial tutorial = Fresh(save);

            for (int index = 0; index < 10; index++)
            {
                List<string> shown = Play(tutorial, set, index, lightStreak: true);
                Assert.DoesNotContain(STREAK, shown);
                Assert.DoesNotContain(BUDGET, shown);
            }

            Assert.DoesNotContain("streak", save);
            Assert.DoesNotContain("budget", save);
        }

        [Fact]
        public void TheFirstChapterEndsOnTheRuleAndTheSendOff()
        {
            LevelSet set = ShippedSet();
            Tutorial tutorial = Fresh(new HashSet<string>());

            //Straight to Amphora with nothing taught: everything the chapter owes is caught up there — the glass's
            //cadence queued ahead of the send-off (#605) — and the line's rule and the send-off close it
            List<string> shown = Play(tutorial, set, 9);

            Assert.Equal(SEND_OFF, shown[^1]);
            Assert.Contains(LINE_RULE, shown);
            Assert.True(shown.IndexOf(LINE_RULE) < shown.Count - 1);
            Assert.DoesNotContain(STREAK, shown);
            Assert.DoesNotContain(BUDGET, shown);
        }

        [Fact]
        public void TheSecondChapterTeachesTheScoreAfterTheSendOff()
        {
            LevelSet set = ShippedSet();
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine", "linerule", "graduated" };
            Tutorial tutorial = Fresh(save);

            //The streak on the Gallery's first level, when it lights; the budget one level on
            List<string> first = Play(tutorial, set, 10, lightStreak: true);
            Assert.Equal(new[] { STREAK }, first);
            Assert.Contains("streak", save);

            List<string> second = Play(tutorial, set, 11);
            Assert.Equal(new[] { BUDGET }, second);
            Assert.Contains("budget", save);

            //And a player taught both before #666 moved them is taught nothing again
            Assert.Empty(Play(tutorial, set, 12, lightStreak: true));
        }

        [Fact]
        public void AStreakThatNeverLightsFollowsThePlayerIntoTheNextLevelOfTheChapter()
        {
            LevelSet set = ShippedSet();
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine", "linerule", "graduated" };
            Tutorial tutorial = Fresh(save);

            Assert.Empty(Play(tutorial, set, 10));
            Assert.Contains(STREAK, Play(tutorial, set, 11, lightStreak: true));
        }
    }
}
