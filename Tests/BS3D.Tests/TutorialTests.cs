using BS3D.Screens;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string STREAK = "Keep hitting to multiply your score";
        private const string BUDGET = "Save shots for a bonus";
        private const string LINE_RULE = "Keep the cluster above the red line";
        private const string SEND_OFF = "Fire a ball into empty space when stuck";
        private const string SWAP = "Swap the next two balls";
        private const string BRAKE = "Lift the glass back one step";
        private const string CUT = "Turn the next ball into a cutter";

        //The precise aim's card, and the combination's first step, which says the same (#930)
        private const string LEAN = "Hold to look down the barrel";
        private const string LEAN_PAD = "Hold the left trigger to look down the barrel";
        private const string WALK_ROUND = "Now walk the gun round";

        //PromptFont's codepoints, as Tutorial draws them: the mouse's right button, the two keys, the left trigger and stick
        private const string MOUSE_RIGHT = "⟶";
        private const string KEYS_A_D = "ＡＤ";
        private const string PAD_LEFT_TRIGGER = "↖";
        private const string PAD_LEFT_STICK_SIDEWAYS = "⇄";

        private static LevelSet ShippedSet() =>
            LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

        /// <summary>A save as a set of keys, and the tutorial reading and writing it.</summary>
        private static Tutorial Fresh(HashSet<string> save) =>
            new(key => save.Contains(key), key => save.Add(key), Tutorial.Mode.Normal);

        /// <summary>Begins the set's entry <paramref name="index"/> and returns every caption the level shows, in order.</summary>
        private static List<string> Play(Tutorial tutorial, LevelSet set, int index, bool lightStreak = false, bool swapOffered = false,
            bool retry = false, bool brakeOffered = false, bool cutOffered = false, int glassStepsAt = -1, bool holding = false,
            int stuckFrom = -1, bool everyShowing = false)
        {
            bool placed = Tutorial.TryPlace(set, index, out int chapter, out int levelInChapter, out int length);
            tutorial.BeginLevel(placed ? chapter : -1, levelInChapter, length, ceilingStep: 6, swapOffered: swapOffered, retry: retry,
                brakeOffered: brakeOffered, cutOffered: cutOffered);

            List<string> shown = new();
            string previous = null;
            for (int frame = 0; frame < 4000; frame++)
            {
                //A player stuck near the line from this frame on: the session asks, every frame the moment holds, whether the
                //throw-away lesson is armed, and fires it if so (GameplayScreen.UpdateLaserWarning)
                if (stuckFrom >= 0 && frame >= stuckFrom && tutorial.IsArmed(Tutorial.Lesson.ThrowAway))
                    tutorial.Trigger(Tutorial.Lesson.ThrowAway);

                //The streak lights a little way in, as it would after a couple of landings
                if (lightStreak && frame == 20) tutorial.Trigger(Tutorial.Lesson.Streak);

                //The glass's pressure step, which fires the glass's card and the Brake's (GameplayScreen.AnnounceCeilingStep)
                if (frame == glassStepsAt)
                {
                    tutorial.Trigger(Tutorial.Lesson.Ceiling);
                    tutorial.Trigger(Tutorial.Lesson.Brake);
                }

                //A player holding the close-up and walking the whole time: every hold lesson is done as it comes up
                if (holding)
                {
                    tutorial.NoteHold(Tutorial.Lesson.LeanIn, true, 0.1f);
                    tutorial.NoteHold(Tutorial.Lesson.Traverse, true, 0.1f);
                    tutorial.NoteHold(Tutorial.Lesson.Walk, true, 0.1f);
                    tutorial.NoteTwoStepHold(Tutorial.Lesson.Combine, true, true, 0.1f);
                }

                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                //A caption is recorded when it changes; with everyShowing also when a card comes up after a gap, so the same
                //card shown twice with nothing between is two entries
                string caption = tutorial.Caption;
                if (caption != null && (shown.Count == 0 || shown[^1] != caption || (everyShowing && previous == null))) shown.Add(caption);
                previous = caption;
            }

            return shown;
        }

        //Every lesson's key, as a save that has completed the whole tutorial holds them
        private static readonly string[] EVERY_LESSON =
            { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine", "linerule", "throwaway", "streak", "budget", "swap",
              "brake", "cut" };

        /// <summary>A save that has been through every card, and a count of what the tutorial writes to it.</summary>
        private static Tutorial Finished(out System.Func<int> writes)
        {
            HashSet<string> save = new(EVERY_LESSON);
            int written = 0;
            writes = () => written;
            return new Tutorial(key => save.Contains(key), key => { written++; save.Add(key); }, Tutorial.Mode.Normal);
        }

        [Fact]
        public void AFinishedSaveSeesTheOpenersOwnCardsAgainAndWritesNothing()
        {
            //#715: a level shows the cards it introduces again on every entry, the save notwithstanding, and a replay
            //records nothing - Lessons stays what the player really completed
            Tutorial tutorial = Finished(out System.Func<int> writes);

            List<string> pennant = Play(tutorial, ShippedSet(), 0);

            Assert.Equal(new[] { "Move the mouse to aim", "Click to fire", "Match three of a colour" }, pennant);
            Assert.Equal(0, writes());
        }

        [Fact]
        public void AReplayShowsTheLevelsOwnLessonsNotTheWholeLadder()
        {
            //The sixth level introduces the combined move and nothing else; on a fresh save it would also carry every
            //earlier card the player still owed, but a finished save owes none
            List<string> sixth = Play(Finished(out _), ShippedSet(), 5);

            Assert.Equal(new[] { LEAN }, sixth);
        }

        [Fact]
        public void AmphoraSendsAFinishedPlayerOffAgainOnEveryEntryButNotOnARetry()
        {
            //The send-off is the closing level's own card, so a replay shows it - and NothingAfterTheSendOff must not
            //cut it for being in the save. A retry has been through it, so it does not come back; the next entry into the
            //level (the picker, a later visit in the same launch) shows it again.
            LevelSet set = ShippedSet();
            Tutorial tutorial = Finished(out System.Func<int> writes);

            Assert.Contains(SEND_OFF, Play(tutorial, set, 9));
            Assert.DoesNotContain(SEND_OFF, Play(tutorial, set, 9, retry: true));
            Assert.Contains(SEND_OFF, Play(tutorial, set, 9));
            Assert.Equal(0, writes());
        }

        [Fact]
        public void TheSendOffsReplayDoesNotBringBackWhatTheChapterStillOwes()
        {
            //A save that went through the send-off without ever completing the walk (an action card that timed out): the
            //walk is owed, but the player has been sent off, so Amphora's replay is the send-off alone (the review of #715)
            HashSet<string> save = new(EVERY_LESSON);
            save.Remove("walk");
            Tutorial tutorial = new(key => save.Contains(key), key => save.Add(key), Tutorial.Mode.Normal);

            Assert.Equal(new[] { SEND_OFF }, Play(tutorial, ShippedSet(), 9));
        }

        //--- #936: the closing card is the way out of Amphora, and it comes back once at the moment it is about ---

        [Fact]
        public void AmphoraClosesOnTheWayOutAndIsTaughtUnderItsOwnKey()
        {
            //The card is Amphora's own and the last of a fresh run's queue; it is not the tail of the line card two levels
            //back (Saturn's) and says something none of the cards before it did
            HashSet<string> save = new();
            List<string> amphora = Play(Fresh(save), ShippedSet(), 9);

            Assert.Equal(SEND_OFF, amphora[^1]);
            Assert.Equal(1, amphora.Count(c => c == SEND_OFF));
            Assert.Contains("throwaway", save);
            Assert.DoesNotContain("graduated", save);
        }

        [Fact]
        public void TheWayOutComesBackOnceWhenThePlayerIsStuckAndNotBeforeTheOpeningCardIsRead()
        {
            LevelSet set = ShippedSet();

            //Stuck from the first frame: the moment waits for the opening card to be read, then shows it again, once
            List<string> stuck = Play(Fresh(new HashSet<string>()), set, 9, stuckFrom: 0, everyShowing: true);
            Assert.Equal(2, stuck.Count(c => c == SEND_OFF));
            Assert.Equal(SEND_OFF, stuck[^1]);

            //Not stuck: the opening card alone
            Assert.Equal(1, Play(Fresh(new HashSet<string>()), set, 9, everyShowing: true).Count(c => c == SEND_OFF));
        }

        [Fact]
        public void TheWayOutIsArmedAfterItsOpeningCardAndFiresOnceALevel()
        {
            LevelSet set = ShippedSet();
            Tutorial tutorial = Fresh(new HashSet<string>());
            Assert.True(Tutorial.TryPlace(set, 9, out int chapter, out int level, out int length));
            tutorial.BeginLevel(chapter, level, length, ceilingStep: 7);

            //Waiting for the opening card to be read: not armed yet
            Assert.False(tutorial.IsArmed(Tutorial.Lesson.ThrowAway));

            //Read it out, every card of a fresh run first: the moment is armed a rest after the card has gone, never while it
            //is up or leaving, so a player stuck just as it is read out is not shown the same words again at once
            int cardGoneAt = -1, armedAt = -1;
            bool wasUp = false;
            for (int frame = 0; frame < 4000 && armedAt < 0; frame++)
            {
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                if (tutorial.Caption == SEND_OFF) wasUp = true;
                else if (wasUp && cardGoneAt < 0 && tutorial.Caption == null) cardGoneAt = frame;
                if (tutorial.IsArmed(Tutorial.Lesson.ThrowAway)) armedAt = frame;
            }

            Assert.True(cardGoneAt > 0 && armedAt > cardGoneAt, $"gone at {cardGoneAt}, armed at {armedAt}");
            Assert.True((armedAt - cardGoneAt) * 0.1f >= 5f, $"armed only {(armedAt - cardGoneAt) * 0.1f} s after the card went");
            Assert.True(tutorial.IsArmed(Tutorial.Lesson.ThrowAway));

            //The moment fires it once; asked every frame after, there is nothing armed left to fire
            tutorial.Trigger(Tutorial.Lesson.ThrowAway);
            Assert.False(tutorial.IsArmed(Tutorial.Lesson.ThrowAway));

            List<string> shown = new();
            string previous = null;
            for (int frame = 0; frame < 400; frame++)
            {
                tutorial.Trigger(Tutorial.Lesson.ThrowAway);
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                string caption = tutorial.Caption;
                if (caption != null && previous == null) shown.Add(caption);
                previous = caption;
            }

            Assert.Equal(new[] { SEND_OFF }, shown);
        }

        [Fact]
        public void ARetryOfAmphoraArmsTheMomentAtOnceAndNoOtherLevelArmsItAtAll()
        {
            LevelSet set = ShippedSet();
            Tutorial tutorial = Fresh(new HashSet<string>());

            Play(tutorial, set, 9);

            //The retry has been through the opening card, so none is queued, and the moment is armed from the start
            tutorial.BeginLevel(0, 9, 10, ceilingStep: 7, retry: true);
            Assert.True(tutorial.IsArmed(Tutorial.Lesson.ThrowAway));

            //Saturn, Gem, the Gallery and a level past the tutorial arm nothing
            foreach (int index in new[] { 7, 8, 10, 25 })
            {
                Tutorial other = Fresh(new HashSet<string>());
                bool placed = Tutorial.TryPlace(set, index, out int chapter, out int level, out int length);
                other.BeginLevel(placed ? chapter : -1, level, length, ceilingStep: 7);
                for (int frame = 0; frame < 400; frame++)
                    other.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                Assert.False(other.IsArmed(Tutorial.Lesson.ThrowAway), $"level {index}");
            }
        }

        [Fact]
        public void APlayerTheFormerSendOffTaughtIsNotOwedTheNewCardBeyondAmphoraAndSeesItThereOnce()
        {
            //An older build recorded "graduated": that player was past the chapter's end, so the Gallery owes nothing on its
            //account - the streak is not held back behind a card nobody is owed - ...
            LevelSet set = ShippedSet();
            HashSet<string> save = new(EVERY_LESSON.Where(k => k != "throwaway" && k != "streak" && k != "budget")) { "graduated" };
            int writes = 0;
            Tutorial tutorial = new(key => save.Contains(key), key => { writes++; save.Add(key); }, Tutorial.Mode.Normal);

            Assert.Equal(new[] { STREAK }, Play(tutorial, set, 10, lightStreak: true));
            Assert.DoesNotContain("throwaway", save);
            writes = 0;

            //... and Amphora, entered, shows them the new card as the level's own and records it under the new key
            Assert.Equal(new[] { SEND_OFF }, Play(tutorial, set, 9));
            Assert.Contains("throwaway", save);
            Assert.Equal(1, writes);

            //In the save, it is an ordinary own-level card: a retry does not show it, the next entry does, neither writes
            Assert.DoesNotContain(SEND_OFF, Play(tutorial, set, 9, retry: true));
            Assert.Contains(SEND_OFF, Play(tutorial, set, 9));
            Assert.Equal(1, writes);
        }

        [Fact]
        public void TheWayOutIsDueOnlyWhereTheNetIsOnAndTheBoreHasNowhereToScore()
        {
            Assert.True(Tutorial.ThrowAwayIsDue(netOn: true, boreCanScore: false));
            Assert.False(Tutorial.ThrowAwayIsDue(netOn: true, boreCanScore: true));
            Assert.False(Tutorial.ThrowAwayIsDue(netOn: false, boreCanScore: false));
            Assert.False(Tutorial.ThrowAwayIsDue(netOn: false, boreCanScore: true));
        }

        [Fact]
        public void TheWayOutIsOneInstructingCardInBothHandsWithNoGlyph()
        {
            List<Tutorial.StepCopy> steps = Tutorial.Steps().Where(s => s.Key == "throwaway").ToList();

            Assert.Equal(2, steps.Count);
            Assert.All(steps, s =>
            {
                Assert.Equal((SEND_OFF, "Wasting a ball is fine", 0, true), (s.Caption, s.Detail, s.Step, s.Instructs));
                Assert.True(string.IsNullOrEmpty(s.Glyph));
            });
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
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine", "linerule", "throwaway" };
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

        /// <summary>
        /// One tool a chapter, each taught where its chip first stands (#705): the Swap's card on a level that grants one,
        /// past the ladder's two chapters as on them (the shipped third chapter's first level is past them); the Cut's
        /// likewise; the Brake's on the glass's first pressure step and not before; none where nothing is granted, so the
        /// Gallery - which grants nothing now - teaches none; and a save that holds the old "swap" key sees no second card.
        /// </summary>
        [Fact]
        public void EachToolIsTaughtWhereItsChipFirstStandsAndNowhereElse()
        {
            LevelSet set = ShippedSet();

            Assert.False(Tutorial.TryPlace(set, 20, out _, out _, out _));
            Assert.Contains(SWAP, Play(Fresh(new HashSet<string>()), set, 20, swapOffered: true));
            Assert.DoesNotContain(SWAP, Play(Fresh(new HashSet<string>()), set, 20, swapOffered: false));
            Assert.DoesNotContain(SWAP, Play(Fresh(new HashSet<string> { "swap" }), set, 20, swapOffered: true));

            //The Gallery grants nothing since #705, and so teaches no tool
            for (int index = 10; index < 20; index++)
            {
                Assert.Equal(0, set.SwapChargesAt(index));
                Assert.DoesNotContain(SWAP, Play(Fresh(new HashSet<string>()), set, index, swapOffered: set.SwapChargesAt(index) > 0));
            }

            Assert.Contains(CUT, Play(Fresh(new HashSet<string>()), set, 40, cutOffered: true));
            Assert.DoesNotContain(CUT, Play(Fresh(new HashSet<string>()), set, 40, cutOffered: false));

            //The Brake waits for the glass to step: no step, no card
            Assert.DoesNotContain(BRAKE, Play(Fresh(new HashSet<string>()), set, 30, brakeOffered: true));
            Assert.Contains(BRAKE, Play(Fresh(new HashSet<string>()), set, 30, brakeOffered: true, glassStepsAt: 100));
            Assert.DoesNotContain(BRAKE, Play(Fresh(new HashSet<string>()), set, 30, brakeOffered: false, glassStepsAt: 100));
        }

        /// <summary>
        /// A tool's card does not follow the player through the whole campaign (#705, the review): left unpressed until it
        /// times out it counts as read, and a key pressed before the card came up teaches it there and then. A ladder's
        /// action card, by contrast, still comes back within its chapter.
        /// </summary>
        [Fact]
        public void AToolsCardIsNotOfferedForEver()
        {
            LevelSet set = ShippedSet();

            //Shown, never pressed, timed out: recorded, and the next level offers it no more
            HashSet<string> save = new();
            Tutorial tutorial = Fresh(save);
            Assert.Contains(SWAP, Play(tutorial, set, 20, swapOffered: true));
            Assert.Contains("swap", save);
            Assert.DoesNotContain(SWAP, Play(tutorial, set, 21, swapOffered: true));

            //Pressed during the first-card delay, before its card is up: taught, and no card is shown at all
            HashSet<string> early = new();
            Tutorial eager = Fresh(early);
            eager.BeginLevel(-1, 0, 10, ceilingStep: 6, cutOffered: true);
            eager.Report(Tutorial.Lesson.Cut);
            for (int frame = 0; frame < 400; frame++)
            {
                eager.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
                Assert.NotEqual(CUT, eager.Caption);
            }
            Assert.Contains("cut", early);

            //A ladder's action card left undone is not recorded: the aim card times out and comes back on the next level
            HashSet<string> ladder = new();
            Tutorial first = Fresh(ladder);
            Play(first, set, 0);
            Assert.DoesNotContain("aim", ladder);
        }

        /// <summary>A press of the Brake's key and of the Cut's completes its card and records it (#705), as the Swap's does.</summary>
        [Fact]
        public void APressOfTheBrakeOrTheCutCompletesItsCard()
        {
            HashSet<string> save = new();
            Tutorial tutorial = Fresh(save);

            tutorial.BeginLevel(-1, 0, 10, ceilingStep: 6, cutOffered: true);
            for (int frame = 0; frame < 60 && tutorial.Caption != CUT; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Equal(CUT, tutorial.Caption);

            tutorial.Report(Tutorial.Lesson.Cut);
            tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Equal("Armed!", tutorial.Praise);
            for (int frame = 0; frame < 200; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Contains("cut", save);

            tutorial.BeginLevel(-1, 0, 10, ceilingStep: 6, brakeOffered: true);
            tutorial.Trigger(Tutorial.Lesson.Brake);
            for (int frame = 0; frame < 60 && tutorial.Caption != BRAKE; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Equal(BRAKE, tutorial.Caption);

            tutorial.Report(Tutorial.Lesson.Brake);
            tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Equal("Lifted!", tutorial.Praise);
            for (int frame = 0; frame < 200; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Contains("brake", save);
        }

        [Fact]
        public void APressOfTheSwapCompletesItsCardAndRecordsItAsTaught()
        {
            LevelSet set = ShippedSet();
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine", "linerule", "throwaway", "streak", "budget" };
            Tutorial tutorial = Fresh(save);

            //The Coil's first level, the first that grants a Swap since #705 - past the ladder's chapters
            bool placed = Tutorial.TryPlace(set, 20, out int chapter, out int levelInChapter, out int length);
            Assert.False(placed);
            tutorial.BeginLevel(-1, levelInChapter, length, ceilingStep: 6, swapOffered: true);

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
            Assert.DoesNotContain(SWAP, Play(tutorial, set, 21, swapOffered: true));
        }

        [Fact]
        public void AStreakThatNeverLightsFollowsThePlayerIntoTheNextLevelOfTheChapter()
        {
            LevelSet set = ShippedSet();
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine", "linerule", "throwaway" };
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

        //--- #933: the net is shown with the line's card, and at the start of every level from the one that card is first taught on ---

        [Fact]
        public void TheLinesCardIsUpExactlyWhileTheNetIsToBlinkAndNeverBeforeItIsRead()
        {
            LevelSet set = ShippedSet();
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine" };
            Tutorial tutorial = Fresh(save);

            Assert.True(Tutorial.TryPlace(set, 7, out int chapter, out int level, out int length));
            tutorial.BeginLevel(chapter, level, length, ceilingStep: 6);

            float shownSeconds = 0f;
            bool everShown = false;

            for (int frame = 0; frame < 400; frame++)
            {
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                //Only ever while it is that card: never for the glass's, the send-off or a frame with no card
                if (tutorial.ShowsTheLine)
                {
                    Assert.Equal(LINE_RULE, tutorial.Caption);
                    everShown = true;
                    shownSeconds += 0.1f;
                }
            }

            //For as long as the card is read (INFO_SECONDS, 7) and no longer, and it is recorded as read
            Assert.True(everShown);
            Assert.InRange(shownSeconds, 6.5f, 7.6f);
            Assert.Contains("linerule", save);
            Assert.False(tutorial.ShowsTheLine);
        }

        [Fact]
        public void TheNetDoesNotBlinkForACardThatIsSteppedAsideOrSwitchedOff()
        {
            LevelSet set = ShippedSet();
            HashSet<string> save = new() { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk", "combine" };
            Tutorial tutorial = Fresh(save);

            Assert.True(Tutorial.TryPlace(set, 7, out int chapter, out int level, out int length));
            tutorial.BeginLevel(chapter, level, length, ceilingStep: 6);

            for (int frame = 0; frame < 100 && !tutorial.ShowsTheLine; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.True(tutorial.ShowsTheLine);

            //The Tutorial row switched off: the card steps aside at once, and the net stops with it
            tutorial.Update(0.1f, enabled: false, takeoverEngaged: false, levelDecided: false);
            Assert.False(tutorial.ShowsTheLine);

            //On again: it comes back, and so does the blink
            for (int frame = 0; frame < 100 && !tutorial.ShowsTheLine; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.True(tutorial.ShowsTheLine);

            //A decided level drops the card outright
            tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: true);
            Assert.False(tutorial.ShowsTheLine);
        }

        [Fact]
        public void TheNetIsShownAtTheStartOfEveryLevelFromTheOneTheLinesCardIsFirstTaughtOn()
        {
            LevelSet set = ShippedSet();
            Assert.Equal("Saturn", set.Levels[7].Name);

            //Counted exactly as the card is: the level it first stands on, which a fresh save is shown it on, and every level after
            int firstCard = -1;
            for (int index = 0; index < set.Count; index++)
            {
                Tutorial.TryPlace(set, index, out int chapter, out int level, out int length);
                bool shown = Tutorial.NetIsShownAt(chapter, level, length);

                if (index < 7) Assert.False(shown, $"level {index}: the net is shown before the line's card is");
                else Assert.True(shown, $"level {index}: the net is not shown from the line's card on");

                //Whichever chapter the level is in, the card's own first level is the first the net is shown on
                if (firstCard < 0 && Play(Fresh(new HashSet<string>()), set, index).Contains(LINE_RULE)) firstCard = index;
            }

            Assert.Equal(7, firstCard);

            //No placement at all (no level set): none
            Assert.False(Tutorial.NetIsShownAt(-1, -1, 0));
        }

        [Fact]
        public void ACrowdedChapterShowsTheNetOnTheLevelItsLinesCardCrowdsInto()
        {
            //A first chapter of two levels: the card is shown on the first (a short chapter crowds its cards rather than cutting any),
            //so the net is too, and in the second chapter of any length from its first level
            LevelSet set = new();
            for (int i = 0; i < 2; i++) set.Levels.Add(new LevelSetEntry { File = $"a{i}.json", Name = $"A{i}", Block = "A" });
            for (int i = 0; i < 3; i++) set.Levels.Add(new LevelSetEntry { File = $"b{i}.json", Name = $"B{i}", Block = "B" });

            for (int index = 0; index < set.Count; index++)
            {
                Tutorial.TryPlace(set, index, out int chapter, out int level, out int length);
                Assert.True(Tutorial.NetIsShownAt(chapter, level, length), $"level {index}");
            }
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
            Assert.Contains("throwaway", save);
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
            List<string> shown = Play(tutorial, set, 0, lightStreak: false, holding: true);
            List<string> second = Play(tutorial, set, 1, holding: true);
            shown.AddRange(second);

            //The combination's first step says what the precise aim's own card does, so it is its second step that shows it came
            foreach (string caption in new[] { "Click to fire", LEAN, "Walk the gun round the field",
                         "Step closer to the cluster", WALK_ROUND, LINE_RULE })
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

        //--- #930: the combination is two steps, the second shown once the first is done ---

        private const float STEP = 0.1f;

        /// <summary>
        /// The sixth level's combination card, up and waiting at its first step, for a player who has been through every card
        /// before it (and not yet sent off, which would end what the chapter still owes).
        /// </summary>
        private static Tutorial CombinationCard(HashSet<string> save, bool pad = false)
        {
            save.UnionWith(new[] { "aim", "fire", "match", "lean", "ceiling", "line", "traverse", "walk" });
            Tutorial tutorial = Fresh(save);
            if (pad) tutorial.NoteDevice(Tutorial.Device.Gamepad);

            Assert.True(Tutorial.TryPlace(ShippedSet(), 5, out int chapter, out int level, out int length));
            tutorial.BeginLevel(chapter, level, length, ceilingStep: 6);
            for (int frame = 0; frame < 100 && tutorial.Presence < 1f; frame++)
                tutorial.Update(STEP, enabled: true, takeoverEngaged: false, levelDecided: false);

            Assert.Equal(1f, tutorial.Presence);
            return tutorial;
        }

        /// <summary>Holds the close-up (and, with <paramref name="moving"/>, the walk) for <paramref name="seconds"/>, a frame at a time.</summary>
        private static void Hold(Tutorial tutorial, float seconds, bool lean = true, bool moving = false)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += STEP)
            {
                tutorial.NoteTwoStepHold(Tutorial.Lesson.Combine, lean, lean && moving, STEP);
                tutorial.Update(STEP, enabled: true, takeoverEngaged: false, levelDecided: false);
            }
        }

        [Fact]
        public void TheCombinationAsksTheCloseUpFirstAndTheWalkOnceItIsDone()
        {
            HashSet<string> save = new();
            Tutorial tutorial = CombinationCard(save);

            //Step one stands alone: one glyph, one imperative, and walking without the close-up does nothing to it
            Assert.Equal((0, MOUSE_RIGHT, LEAN, (string)null), (tutorial.Step, tutorial.Glyph, tutorial.Caption, tutorial.Detail));
            Hold(tutorial, 2f, lean: false, moving: true);
            Assert.Equal(0, tutorial.Step);

            //The close-up held: the card turns to the walk, with the cue a done action gives, and records nothing yet
            Hold(tutorial, 0.5f);
            Assert.Equal(1, tutorial.Step);
            Assert.Equal((KEYS_A_D, WALK_ROUND, (string)null), (tutorial.Glyph, tutorial.Caption, tutorial.Detail));
            Assert.True(tutorial.TakePraiseCue());
            Assert.False(tutorial.Praising);
            Assert.DoesNotContain("combine", save);

            //Then the walk, with the close-up still held: done, praised, and recorded under the lesson's old key
            Hold(tutorial, 0.5f, moving: true);
            Assert.True(tutorial.Praising);
            Assert.Equal("Together!", tutorial.Praise);
            Assert.Equal(WALK_ROUND, tutorial.Caption);
            Assert.True(tutorial.TakePraiseCue());
            Assert.Contains("combine", save);
        }

        [Fact]
        public void TheCombinationOnThePadAsksTheTriggerThenTheStick()
        {
            Tutorial tutorial = CombinationCard(new HashSet<string>(), pad: true);

            Assert.Equal((PAD_LEFT_TRIGGER, LEAN_PAD), (tutorial.Glyph, tutorial.Caption));
            Hold(tutorial, 2f);
            Assert.Equal((PAD_LEFT_STICK_SIDEWAYS, "Now push the left stick sideways"), (tutorial.Glyph, tutorial.Caption));
        }

        [Fact]
        public void TheCombinationIsNotDoneByAlternatingItsTwoHalves()
        {
            HashSet<string> save = new();
            Tutorial tutorial = CombinationCard(save);

            //The button held, then the walk with the button let go, over and over: never both on one frame
            for (int i = 0; i < 20; i++)
            {
                Hold(tutorial, 0.3f);
                Hold(tutorial, 0.1f, lean: false, moving: true);
            }

            Assert.Equal(0, tutorial.Step);

            //And on the second step: the walk and the button together, but never for the whole hold at once
            Hold(tutorial, 2f);
            Assert.Equal(1, tutorial.Step);
            for (int i = 0; i < 20; i++)
            {
                Hold(tutorial, 0.3f, moving: true);
                Hold(tutorial, 0.1f);
            }

            Assert.False(tutorial.Praising);
            Assert.DoesNotContain("combine", save);
        }

        [Fact]
        public void TheFirstStepStandsLongEnoughToBeReadEvenIfTheButtonIsAlreadyDown()
        {
            Tutorial tutorial = CombinationCard(new HashSet<string>());

            //The player has held the button since before the card came up: the card still gives its first step a moment
            Hold(tutorial, 0.5f);
            Assert.Equal(0, tutorial.Step);
            Hold(tutorial, 0.5f);
            Assert.Equal(1, tutorial.Step);
        }

        [Fact]
        public void LettingTheCloseUpGoSendsTheCardBackToTheStepThatSaysWhatToHold()
        {
            Tutorial tutorial = CombinationCard(new HashSet<string>());
            Hold(tutorial, 2f);
            Assert.Equal(1, tutorial.Step);

            //A flicker is not a release; half a second is
            Hold(tutorial, 0.2f, lean: false);
            Assert.Equal(1, tutorial.Step);
            Hold(tutorial, 0.2f);
            Hold(tutorial, 0.6f, lean: false);
            Assert.Equal(0, tutorial.Step);
            Assert.Equal(LEAN, tutorial.Caption);
            Assert.False(tutorial.Praising);

            //And held again it comes straight back to the walk, without the first step's minimum
            Hold(tutorial, 0.5f);
            Assert.Equal(1, tutorial.Step);
        }

        [Fact]
        public void AHalfDoneCombinationIsNotRecordedAndIsOfferedAgainFromItsFirstStep()
        {
            //The second step reached, never finished: the card gives up on its clock like any action that times out
            HashSet<string> save = new();
            Tutorial tutorial = CombinationCard(save);
            Hold(tutorial, 2f);
            Assert.Equal(1, tutorial.Step);

            for (int frame = 0; frame < 400; frame++)
                tutorial.Update(STEP, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Null(tutorial.Caption);
            Assert.DoesNotContain("combine", save);

            //The next level offers it again, from the top
            Assert.Contains(LEAN, Play(tutorial, ShippedSet(), 6));
        }

        [Fact]
        public void ASaveThatTaughtTheCombinationBeforeItWasSplitIsNotTaughtItAgain()
        {
            //The lesson's key is unchanged, so a save that holds it owes nothing and sees the card only as the sixth level's own
            HashSet<string> save = new(EVERY_LESSON);
            Assert.Equal(new[] { LEAN }, Play(Fresh(save), ShippedSet(), 5));
            Assert.Empty(Play(Fresh(save), ShippedSet(), 6));
        }

        [Fact]
        public void TheReelShowsBothStepsOfTheCombinationOnItsClock()
        {
            Tutorial tutorial = Reel();
            List<string> shown = new();
            bool praised = false;

            for (int frame = 0; frame < 75 * 400; frame++)
            {
                //A window with the focus reads the hands every frame, and nobody is holding anything: that must not move the reel
                tutorial.NoteTwoStepHold(Tutorial.Lesson.Combine, false, false, FRAME);
                Step(tutorial, takeover: false, decided: false);

                string caption = tutorial.Caption;
                if (caption != null && (shown.Count == 0 || shown[^1] != caption)) shown.Add(caption);
                if (tutorial.Praise == "Together!") praised = true;
            }

            //The lean card and the combination's first step say the same, so the dedup folds them into one
            int walkRound = shown.IndexOf(WALK_ROUND);
            Assert.True(walkRound > 0 && shown.LastIndexOf(LEAN) == walkRound - 1, string.Join(" | ", shown));
            Assert.Equal(1, shown.Count(c => c == WALK_ROUND));
            Assert.True(praised, "the reel never finished the combination");
        }

        //--- #700: a card dealt with does not come back when a camera takeover lifts ---

        private const float FRAME = 1f / 75f;

        /// <summary>A demo reel's first level: every card counts itself done on its own clock, so no player is needed.</summary>
        private static Tutorial Reel()
        {
            Tutorial tutorial = new(key => false, key => { }, Tutorial.Mode.Demo);
            tutorial.BeginLevel(1, 1, 10, ceilingStep: null);
            return tutorial;
        }

        private static void Step(Tutorial tutorial, bool takeover, bool decided)
        {
            tutorial.Update(FRAME, enabled: true, takeoverEngaged: takeover, levelDecided: decided);
            tutorial.TakePraiseCue();
        }

        private static void UpToFull(Tutorial tutorial)
        {
            for (int frame = 0; frame < 2000 && tutorial.Presence < 0.99f; frame++) Step(tutorial, false, false);
            Assert.True(tutorial.Presence >= 0.99f, "no card came up");
        }

        /// <summary>
        /// The level ends with a camera takeover while a card is up (the drop cinematic after the clearing shot): every
        /// frame after the takeover lifts the card stays gone. It used to fade back in with the hiding and out again,
        /// about a third of the way up (0.35 in this harness), just before the result page.
        /// </summary>
        [Fact]
        public void ADecidedLevelsCardDoesNotFlashBackWhenTheTakeoverLifts()
        {
            Tutorial tutorial = Reel();
            UpToFull(tutorial);

            for (int frame = 0; frame < 150; frame++) Step(tutorial, takeover: true, decided: true);
            for (int frame = 0; frame < 225; frame++)
            {
                Step(tutorial, takeover: false, decided: true);
                Assert.Equal(0f, tutorial.Presence);
            }
        }

        /// <summary>
        /// A card already leaving when a takeover starts mid-level runs its fade to the end unseen: nothing of it comes
        /// back as the hiding lifts (it came back to 0.32 before #700).
        /// </summary>
        [Fact]
        public void ALeavingCardFinishesUnseenUnderATakeover()
        {
            Tutorial tutorial = Reel();
            UpToFull(tutorial);

            //Until the card starts to go: the reel counts it done on its own clock and it begins to leave
            float previous = tutorial.Presence;
            for (int frame = 0; frame < 20000 && tutorial.Presence >= previous; frame++)
            {
                previous = tutorial.Presence;
                Step(tutorial, false, false);
            }
            Assert.True(tutorial.Presence > 0.5f && tutorial.Presence < 1f, "the card did not start to leave");

            for (int frame = 0; frame < 150; frame++) Step(tutorial, takeover: true, decided: false);
            for (int frame = 0; frame < 45; frame++)
            {
                Step(tutorial, takeover: false, decided: false);
                Assert.Equal(0f, tutorial.Presence);
            }
        }

        /// <summary>And the other half of the rule, which must not go with it: a card still NEEDED - up, not leaving - is
        /// held under a takeover and comes back after it, a lesson shown to a player watching something else being no
        /// lesson at all.</summary>
        [Fact]
        public void ACardStillNeededComesBackAfterATakeover()
        {
            Tutorial tutorial = Reel();
            UpToFull(tutorial);

            for (int frame = 0; frame < 75; frame++) Step(tutorial, takeover: true, decided: false);
            Assert.Equal(0f, tutorial.Presence, 3);

            for (int frame = 0; frame < 40; frame++) Step(tutorial, takeover: false, decided: false);
            Assert.True(tutorial.Presence > 0.99f, $"the card did not come back ({tutorial.Presence})");
        }

        //THE KIND CARDS (#735)

        /// <summary>A map by kind, the way the session hands it over: the colour of the first ball of each kind, or zero.</summary>
        private static BallType[] MapWith(params BallKind[] kinds)
        {
            BallType[] map = new BallType[Tutorial.KIND_COUNT];
            foreach (BallKind kind in kinds) map[(int)kind] = BallType.Type3;
            return map;
        }

        /// <summary>Begins a level the ladder does not reach (chapter −1) with <paramref name="map"/>, steps it through, and
        /// returns every kind a card showed, in order; <paramref name="wildcardAt"/> fires the wildcard's event at that frame.</summary>
        private static List<BallKind> KindsShown(Tutorial tutorial, BallType[] map, int chapter = -1, int levelInChapter = 0,
            int chapterLength = 10, int wildcardAt = -1)
        {
            tutorial.BeginLevel(chapter, levelInChapter, chapterLength, ceilingStep: 6, kindsOnMap: map);

            List<BallKind> shown = new();
            for (int frame = 0; frame < 4000; frame++)
            {
                if (frame == wildcardAt) tutorial.Trigger(Tutorial.Lesson.KindWildcard);
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                if (tutorial.CardKind is BallKind kind && (shown.Count == 0 || shown[^1] != kind)) shown.Add(kind);
            }

            return shown;
        }

        /// <summary>
        /// Every kind a player can meet has its card (#735, in the manner of #584's per-kind check): each one a map can
        /// carry, shown on a level past the ladder's chapters the first time it is on the map, and the wildcard, which
        /// arrives in the gun, on its event. The Cutter is a power-up's round and #705's; Normal needs none.
        /// </summary>
        [Fact]
        public void EveryKindAPlayerCanMeetHasACard()
        {
            foreach (BallKind kind in System.Enum.GetValues<BallKind>())
            {
                if (kind is BallKind.Normal or BallKind.Cutter) continue;

                HashSet<string> save = new();
                Tutorial tutorial = Fresh(save);

                List<BallKind> shown = kind == BallKind.Wildcard
                    ? KindsShown(tutorial, MapWith(), wildcardAt: 30)
                    : KindsShown(tutorial, MapWith(kind));

                Assert.Equal(new[] { kind }, shown);
                Assert.Single(save);
                Assert.StartsWith("kind-", string.Join("", save));
            }
        }

        /// <summary>A kind card is shown where the kind is and nowhere else, taught once, and drawn in the map's colour.</summary>
        [Fact]
        public void AKindCardIsShownOnlyWhereItsKindIsAndOnlyOnce()
        {
            HashSet<string> save = new();
            Tutorial tutorial = Fresh(save);

            Assert.Empty(KindsShown(tutorial, MapWith()));
            Assert.Empty(KindsShown(tutorial, MapWith(BallKind.Normal)));

            Assert.Equal(new[] { BallKind.Bomb }, KindsShown(tutorial, MapWith(BallKind.Bomb)));
            Assert.Contains("kind-bomb", save);
            Assert.Empty(KindsShown(tutorial, MapWith(BallKind.Bomb)));

            //Two kinds new on one level: both, in the order the campaign meets them - glass (Umbrella, 42, #736) before
            //everything, buckshot (Juggler, 93) next, the zap before stone
            Assert.Equal(new[] { BallKind.Zap, BallKind.Rock }, KindsShown(tutorial, MapWith(BallKind.Rock, BallKind.Zap, BallKind.Bomb)));
            Assert.Equal(new[] { BallKind.Transparent, BallKind.Buckshot },
                KindsShown(Fresh(new HashSet<string>()), MapWith(BallKind.Buckshot, BallKind.Transparent)));

            //Its ball is drawn in the colour of the first one on the map
            tutorial.BeginLevel(-1, 0, 10, ceilingStep: 6, kindsOnMap: MapWith(BallKind.Heavy));
            for (int frame = 0; frame < 40 && tutorial.CardKind == null; frame++)
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            Assert.Equal(BallKind.Heavy, tutorial.CardKind);
            Assert.Equal(BallType.Type3, tutorial.CardColour);
        }

        /// <summary>
        /// The kind cards are listed in the order the shipped campaign meets the kinds, which is what Tutorial's own comment
        /// promises and the order two new on one level are shown in (#736 moved glass's first level from the Mirage to the
        /// Silhouettes' Umbrella, ninety levels earlier, and the list had to follow). Read off the maps in play order, so the
        /// next level that brings a kind forward fails here rather than leaving the list quietly out of order.
        /// </summary>
        [Fact]
        public void KindCardsComeInTheOrderTheShippedCampaignMeetsThem()
        {
            LevelSet set = ShippedSet();
            List<BallKind> met = new();

            foreach (LevelSetEntry entry in set.Levels)
            {
                StaticBall[,,] balls = new BallsMap(Level.Load(Shipped.Level(entry.File)).Map).GetStaticBallsArray();
                foreach (StaticBall ball in balls)
                    if (ball != null && ball.Kind != BallKind.Normal && !met.Contains(ball.Kind)) met.Add(ball.Kind);
            }

            Assert.True(met.Count >= 2, $"the shipped maps carry {met.Count} special kind(s); the order needs two");
            Assert.Equal(met, KindsShown(Fresh(new HashSet<string>()), MapWith(met.ToArray())));
        }

        /// <summary>
        /// The wildcard's card waits for its event and fires once a level; and a kind card is offered on a level of the
        /// ladder's chapters as on any other, ahead of the ladder's own cards, untouched by the send-off's rules - a save
        /// that has been sent off still meets the bomb on Amphora (#605's cut is for the ladder alone).
        /// </summary>
        [Fact]
        public void TheWildcardWaitsForTheMuzzleAndTheSendOffDoesNotCutAKindCard()
        {
            HashSet<string> save = new();
            Tutorial tutorial = Fresh(save);

            Assert.Empty(KindsShown(tutorial, MapWith()));
            Assert.DoesNotContain("kind-wildcard", save);
            Assert.Equal(new[] { BallKind.Wildcard }, KindsShown(tutorial, MapWith(), wildcardAt: 50));
            Assert.Empty(KindsShown(tutorial, MapWith(), wildcardAt: 50));

            Tutorial finished = Finished(out System.Func<int> _);
            LevelSet set = ShippedSet();
            Tutorial.TryPlace(set, 9, out int chapter, out int levelInChapter, out int length);

            Assert.Equal(new[] { BallKind.Bomb }, KindsShown(finished, MapWith(BallKind.Bomb), chapter, levelInChapter, length));
        }

        //THE DEAD WEIGHT'S CARD (#1023)

        private const string DEAD_WEIGHT = "This ball is already shot down";
        private const string BOMB = "Land a ball beside a bomb to set it off";

        /// <summary>
        /// Begins a level the ladder does not reach (chapter −1) with the dead weight's card armed or not, steps it through and
        /// returns every caption shown, in order. From frame <paramref name="deadAt"/> on (never when negative) a dead ball is
        /// reported every frame, as the session reports one for as long as it lies there: the first of <paramref name="colour"/>,
        /// every later one of the first colour. <paramref name="drawn"/> is the colour the card's ball was drawn in, zero if none.
        /// </summary>
        private static List<string> DeadWeightShown(Tutorial tutorial, int deadAt, BallType colour, out BallType drawn,
            bool armed = true, BallType[] map = null)
        {
            tutorial.BeginLevel(-1, 0, 10, ceilingStep: 6, kindsOnMap: map ?? MapWith(), deadWeightArmed: armed);

            List<string> shown = new();
            drawn = 0;
            for (int frame = 0; frame < 4000; frame++)
            {
                if (deadAt >= 0 && frame >= deadAt) tutorial.NoteDeadWeight(frame == deadAt ? colour : BallType.Type1);
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                //The card's ball is an ordinary one, dithered: the HUD keeps it a square because it has a kind
                if (tutorial.CardBallIsDead)
                {
                    Assert.Equal(BallKind.Normal, tutorial.CardKind);
                    drawn = tutorial.CardColour;
                }

                string caption = tutorial.Caption;
                if (caption != null && (shown.Count == 0 || shown[^1] != caption)) shown.Add(caption);
            }

            return shown;
        }

        /// <summary>
        /// The dead weight's card waits for a dead ball, shows it in that ball's colour (the first one's, not a later one's), and
        /// is taught once: armed on every level until a player has read it, then never again.
        /// </summary>
        [Fact]
        public void TheDeadWeightsCardWaitsForADeadBallAndIsTaughtOnce()
        {
            HashSet<string> save = new();
            Tutorial tutorial = Fresh(save);

            Assert.Empty(DeadWeightShown(tutorial, deadAt: -1, BallType.Type5, out _));
            Assert.DoesNotContain("deadweight", save);

            Assert.Equal(new[] { DEAD_WEIGHT }, DeadWeightShown(tutorial, deadAt: 30, BallType.Type5, out BallType drawn));
            Assert.Equal(BallType.Type5, drawn);
            Assert.Contains("deadweight", save);

            Assert.Empty(DeadWeightShown(tutorial, deadAt: 30, BallType.Type5, out _));
        }

        /// <summary>A level the session does not arm it on - the benchmark's - shows no card for a dead ball, and teaches nothing.</summary>
        [Fact]
        public void TheDeadWeightsCardIsShownOnlyWhereTheSessionArmsIt()
        {
            HashSet<string> save = new();

            Assert.Empty(DeadWeightShown(Fresh(save), deadAt: 30, BallType.Type5, out _, armed: false));
            Assert.Empty(save);
        }

        /// <summary>
        /// A dead ball under a card already up does not push it aside (the hint's rule, #1012): the card up is read out, the
        /// dead weight's follows it, and the card up does not come back a second time.
        /// </summary>
        [Fact]
        public void TheDeadWeightsCardWaitsBehindTheCardUp()
        {
            Tutorial tutorial = Fresh(new HashSet<string>());

            //The bomb's card is queued at the level's start and up from about frame 9 (FIRST_CARD_DELAY)
            Assert.Equal(new[] { BOMB, DEAD_WEIGHT },
                DeadWeightShown(tutorial, deadAt: 20, BallType.Type2, out BallType drawn, map: MapWith(BallKind.Bomb)));
            Assert.Equal(BallType.Type2, drawn);
        }
    }
}
