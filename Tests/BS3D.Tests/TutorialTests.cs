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
        private const string SWAP = "Swap the next two balls";

        private static LevelSet ShippedSet() =>
            LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

        /// <summary>A save as a set of keys, and the tutorial reading and writing it.</summary>
        private static Tutorial Fresh(HashSet<string> save) =>
            new(key => save.Contains(key), key => save.Add(key), Tutorial.Mode.Normal);

        /// <summary>Begins the set's entry <paramref name="index"/> and returns every caption the level shows, in order.</summary>
        private static List<string> Play(Tutorial tutorial, LevelSet set, int index, bool lightStreak = false, bool swapOffered = false)
        {
            bool placed = Tutorial.TryPlace(set, index, out int chapter, out int levelInChapter, out int length);
            tutorial.BeginLevel(placed ? chapter : -1, levelInChapter, length, ceilingStep: 6, swapOffered: swapOffered);

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

            Assert.True(Tutorial.TryPlace(set, 0, out int chapter, out int level, out int length));
            Assert.Equal((0, 0, 10), (chapter, level, length));

            //Amphora closes the first chapter since #649, and the Gallery opens the second
            Assert.Equal("Amphora", set.Levels[9].Name);
            Assert.True(Tutorial.TryPlace(set, 9, out chapter, out level, out _));
            Assert.Equal((0, 9), (chapter, level));

            Assert.True(Tutorial.TryPlace(set, 10, out chapter, out level, out _));
            Assert.Equal((1, 0), (chapter, level));

            //Nothing past the second chapter
            set.BlockRange(10, out _, out int lastOfSecond);
            Assert.True(Tutorial.TryPlace(set, lastOfSecond, out _, out _, out _));
            Assert.False(Tutorial.TryPlace(set, lastOfSecond + 1, out _, out _, out _));
            Assert.False(Tutorial.TryPlace(null, 0, out _, out _, out _));
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

            Assert.Equal(new[] { LINE_RULE, "The glass steps down every 6 shots", SEND_OFF }, shown.GetRange(shown.Count - 3, 3));
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

            //And once both are taught nothing more is (a save taught them before #666 moved them is the same case)
            Assert.Empty(Play(tutorial, set, 12, lightStreak: true));
        }

        [Fact]
        public void TheSwapIsTaughtOnTheSecondChaptersThirdLevelAndOnlyWhereThereIsOne()
        {
            LevelSet set = ShippedSet();
            HashSet<string> everythingElse = new()
                { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine", "linerule", "graduated", "streak", "budget" };

            //The Gallery's third level (#213), with a swap to press: the card, and it is recorded once taught by doing
            HashSet<string> save = new(everythingElse);
            Tutorial tutorial = Fresh(save);
            Assert.Contains(SWAP, Play(tutorial, set, 12, swapOffered: true));

            //Without one (a set with no chapters, the testing argument's zero) it would be a card about a key that does
            //nothing, so it is not shown
            Assert.DoesNotContain(SWAP, Play(Fresh(new HashSet<string>(everythingElse)), set, 12, swapOffered: false));

            //Not before its level: the Gallery's first two teach the score, a level each
            Assert.DoesNotContain(SWAP, Play(Fresh(new HashSet<string>(everythingElse)), set, 10, swapOffered: true));
            Assert.DoesNotContain(SWAP, Play(Fresh(new HashSet<string>(everythingElse)), set, 11, swapOffered: true));

            //And never in the first chapter, whatever the level grants
            for (int index = 0; index < 10; index++)
                Assert.DoesNotContain(SWAP, Play(Fresh(new HashSet<string>()), set, index, swapOffered: true));
        }

        [Fact]
        public void APressOfTheSwapCompletesItsCardAndRecordsItAsTaught()
        {
            LevelSet set = ShippedSet();
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine", "linerule", "graduated", "streak", "budget" };
            Tutorial tutorial = Fresh(save);

            bool placed = Tutorial.TryPlace(set, 12, out int chapter, out int levelInChapter, out int length);
            Assert.True(placed);
            tutorial.BeginLevel(chapter, levelInChapter, length, ceilingStep: 6, swapOffered: true);

            //Up, and waiting for the action: an action card stands until it is done or times out
            for (int frame = 0; frame < 60 && tutorial.Caption != SWAP; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Equal(SWAP, tutorial.Caption);
            Assert.DoesNotContain("swap", save);

            //The press: the card praises, and once it has left the lesson is in the save
            tutorial.Report(Tutorial.Lesson.Swap);
            tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.True(tutorial.Praising);
            Assert.Equal("Swapped!", tutorial.Praise);

            for (int frame = 0; frame < 200; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Contains("swap", save);

            //A taught swap is not offered again on the next level
            Assert.DoesNotContain(SWAP, Play(tutorial, set, 13, swapOffered: true));
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

        [Fact]
        public void TheLineRuleOpensSaturnAndTheSendOffWaitsForAmphora()
        {
            LevelSet set = ShippedSet();
            Assert.Equal("Saturn", set.Levels[7].Name);

            List<string> gem = Play(Fresh(new HashSet<string>()), set, 8);
            Assert.Contains(LINE_RULE, gem);
            Assert.DoesNotContain(SEND_OFF, gem);

            Assert.DoesNotContain(LINE_RULE, Play(Fresh(new HashSet<string>()), set, 6));
        }

        [Fact]
        public void ASendOffNeverReachedFollowsThePlayerIntoTheSecondChapter()
        {
            //Amphora lost and skipped before its cards came up: the second chapter still owes the rule and the
            //send-off, and the player is sent off before the score is taught - the streak lighting on that level
            //waits for the next one
            LevelSet set = ShippedSet();
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine" };
            Tutorial tutorial = Fresh(save);

            List<string> heart = Play(tutorial, set, 10, lightStreak: true);

            Assert.Equal(new[] { LINE_RULE, SEND_OFF }, heart);
            Assert.Contains("graduated", save);
            Assert.DoesNotContain("streak", save);

            List<string> smiley = Play(tutorial, set, 11, lightStreak: true);
            Assert.Contains(STREAK, smiley);
            Assert.Contains(BUDGET, smiley);
        }

        [Fact]
        public void AShortChapterCrowdsItsCardsRatherThanCuttingAny()
        {
            //A first chapter of two levels: every one of its lessons is still shown in it, and the send-off last
            LevelSet set = new();
            for (int i = 0; i < 2; i++) set.Levels.Add(new LevelSetEntry { File = $"a{i}.json", Name = $"A{i}", Block = "A" });
            for (int i = 0; i < 3; i++) set.Levels.Add(new LevelSetEntry { File = $"b{i}.json", Name = $"B{i}", Block = "B" });

            Tutorial tutorial = Fresh(new HashSet<string>());
            List<string> shown = Play(tutorial, set, 0, lightStreak: false);
            List<string> second = Play(tutorial, set, 1);
            shown.AddRange(second);

            foreach (string caption in new[] { "Click to fire", "Hold to look down the barrel", "Walk the gun round the field",
                         "Step in for a steeper shot", "Hold the close-up and turn with it", LINE_RULE })
                Assert.Contains(caption, shown);
            Assert.Equal(SEND_OFF, second[^1]);
        }

        [Fact]
        public void TheClosingCardsAreCountedFromTheChaptersEnd()
        {
            //A chapter of twelve: the send-off is on its twelfth level, not its tenth, and the rule on its tenth
            LevelSet set = new();
            for (int i = 0; i < 12; i++) set.Levels.Add(new LevelSetEntry { File = $"a{i}.json", Name = $"A{i}", Block = "A" });
            for (int i = 0; i < 3; i++) set.Levels.Add(new LevelSetEntry { File = $"b{i}.json", Name = $"B{i}", Block = "B" });

            HashSet<string> taught = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine" };
            Assert.DoesNotContain(SEND_OFF, Play(Fresh(new HashSet<string>(taught)), set, 9));
            Assert.Equal(new[] { LINE_RULE }, Play(Fresh(new HashSet<string>(taught)), set, 9));
            Assert.Equal(new[] { LINE_RULE, SEND_OFF }, Play(Fresh(new HashSet<string>(taught)), set, 11));
        }

        [Fact]
        public void AnActionDoneWhileTheCardArrivesDoesNotSnapItToFullSize()
        {
            //#673: a player already moving the mouse when the aim card pops completes it inside the arrival.
            //The praise's frame set the presence to full, and the pop-in's scale — the instruction's — with it.
            LevelSet set = ShippedSet();
            Tutorial tutorial = Fresh(new HashSet<string>());
            Assert.True(Tutorial.TryPlace(set, 0, out int chapter, out int level, out int length));
            tutorial.BeginLevel(chapter, level, length, ceilingStep: 6, swapOffered: false);

            const float frame = 1f / 60f;
            for (int i = 0; i < 600 && tutorial.Presence <= 0f; i++)
                tutorial.Update(frame, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Equal("Move the mouse to aim", tutorial.Caption);

            //Two frames in: the aim baselined, then swung past the lesson's travel
            tutorial.Update(frame, enabled: true, takeoverEngaged: false, levelDecided: false);
            tutorial.NoteAim(0f, 0f);
            tutorial.NoteAim(0.2f, 0f);
            Assert.True(tutorial.Praising);

            float before = tutorial.Presence;
            Assert.True(before < 0.5f, $"the card was already {before} of the way in");

            tutorial.Update(frame, enabled: true, takeoverEngaged: false, levelDecided: false);
            float after = tutorial.Presence;
            Assert.True(after - before < 0.1f, $"the praise's frame took the card from {before} to {after}");

            //And the arrival still finishes under the praise
            for (int i = 0; i < 60; i++) tutorial.Update(frame, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Equal(1f, tutorial.Presence, 3);
        }
    }
}
