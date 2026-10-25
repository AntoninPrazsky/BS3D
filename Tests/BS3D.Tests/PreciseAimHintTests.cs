using BS3D.Screens;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The precise-aim hint (#1012), the owner's ruling: a shot fired outside precise aim that drops nothing shows the
    /// player that they can aim precisely, and the hint comes back if they used the lean only for a moment or have not used
    /// it for a long time; a player who leans is never told. And the card: armed on any level the session asks, once a
    /// level, never written to the save, behind a card already up, done by holding the button.
    /// </summary>
    public class PreciseAimHintTests
    {
        //Shots fired outside the lean, each followed by the session's question after it dropped nothing
        private static int ShotsUntilDue(PreciseAimHint hint, int most)
        {
            for (int shot = 1; shot <= most; shot++)
            {
                hint.NoteShotFired(leaned: false);
                if (hint.DueAfterUnscoredShot) return shot;
            }

            return -1;
        }

        //A hold of the button for so many seconds at 120 frames a second, then let go
        private static void Hold(PreciseAimHint hint, float seconds)
        {
            for (int i = 0; i < (int)(seconds * 120f); i++) hint.NoteLean(true, 1f / 120f);
            hint.NoteLean(false, 1f / 120f);
        }

        [Fact]
        public void APlayerWhoHasNeverLeanedIsToldOnTheFirstShotThatDropsNothing()
        {
            var hint = new PreciseAimHint();
            Assert.Equal(1, ShotsUntilDue(hint, 100));
        }

        [Fact]
        public void AShotFiredFromTheLeanNeverCallsForIt()
        {
            var hint = new PreciseAimHint();
            hint.NoteShotFired(leaned: true);
            Assert.False(hint.DueAfterUnscoredShot);
        }

        [Fact]
        public void APlayerWhoLeansIsNotTold()
        {
            var hint = new PreciseAimHint();

            //A real look down the barrel every twenty shots, well inside the long stretch
            for (int round = 0; round < 10; round++)
            {
                Hold(hint, PreciseAimHint.REAL_LEAN_SECONDS + 0.2f);
                Assert.Equal(-1, ShotsUntilDue(hint, PreciseAimHint.LONG_SHOTS - 5));
            }
        }

        [Fact]
        public void LeaningOnlyForAMomentBringsItBackAfterTheGap()
        {
            var hint = new PreciseAimHint();
            Assert.Equal(1, ShotsUntilDue(hint, 100));
            hint.NoteShown();

            //Taps of the button, never a real look: shown again once the gap has passed, not sooner
            for (int i = 0; i < 20; i++) Hold(hint, PreciseAimHint.REAL_LEAN_SECONDS * 0.3f);
            Assert.Equal(PreciseAimHint.MIN_SHOTS_BETWEEN, ShotsUntilDue(hint, 100));
        }

        [Fact]
        public void NotLeaningForALongTimeBringsItBack()
        {
            var hint = new PreciseAimHint();
            Assert.Equal(1, ShotsUntilDue(hint, 100));
            hint.NoteShown();

            //A real lean right after the hint: the next one waits the long stretch, counted from the lean
            Hold(hint, PreciseAimHint.REAL_LEAN_SECONDS + 0.2f);
            Assert.Equal(PreciseAimHint.LONG_SHOTS, ShotsUntilDue(hint, 100));
            Assert.Equal(PreciseAimHint.LONG_SHOTS, hint.ShotsSinceRealLean);
        }

        //A save as a set of keys, and a tutorial over it that counts what it writes
        private static Tutorial Over(HashSet<string> save, List<string> written) =>
            new(key => save.Contains(key), key => { written.Add(key); save.Add(key); }, Tutorial.Mode.Normal);

        //Runs the tutorial for so many seconds at ten frames a second
        private static void Run(Tutorial tutorial, float seconds, bool holding = false)
        {
            for (int i = 0; i < (int)(seconds * 10f); i++)
            {
                if (holding) tutorial.NoteHold(Tutorial.Lesson.AimHint, true, 0.1f);
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);
            }
        }

        [Fact]
        public void TheCardIsArmedOnlyWhereTheSessionAsksAndOnAnyLevel()
        {
            var tutorial = Over(new HashSet<string>(), new List<string>());

            tutorial.BeginLevel(-1, 0, 0, ceilingStep: null);
            Assert.False(tutorial.IsArmed(Tutorial.Lesson.AimHint));

            //A level past the tutorial's chapters (no chapter) still carries it when asked
            tutorial.BeginLevel(-1, 0, 0, ceilingStep: null, aimHintArmed: true);
            Assert.True(tutorial.IsArmed(Tutorial.Lesson.AimHint));
        }

        [Fact]
        public void TheCardShowsOnceALevelAndIsNeverWrittenToTheSave()
        {
            var written = new List<string>();
            var tutorial = Over(new HashSet<string>(), written);

            for (int level = 0; level < 3; level++)
            {
                tutorial.BeginLevel(-1, 0, 0, ceilingStep: null, aimHintArmed: true);
                tutorial.Trigger(Tutorial.Lesson.AimHint);
                Run(tutorial, 1f);
                Assert.Equal("Hold to look down the barrel", tutorial.Caption);

                //Unheld, it goes once it has been read, and a second miss on the level cannot fire it again
                Run(tutorial, 9f);
                Assert.Null(tutorial.Caption);
                Assert.False(tutorial.IsArmed(Tutorial.Lesson.AimHint));
                tutorial.Trigger(Tutorial.Lesson.AimHint);
                Run(tutorial, 2f);
                Assert.Null(tutorial.Caption);
            }

            //Held, it is done and praised - and still not written, so it comes back on the next level that arms it
            tutorial.BeginLevel(-1, 0, 0, ceilingStep: null, aimHintArmed: true);
            tutorial.Trigger(Tutorial.Lesson.AimHint);
            Run(tutorial, 2f, holding: true);
            Assert.True(tutorial.Praising || tutorial.Caption == null);

            Assert.DoesNotContain("aimhint", written);
            tutorial.BeginLevel(-1, 0, 0, ceilingStep: null, aimHintArmed: true);
            Assert.True(tutorial.IsArmed(Tutorial.Lesson.AimHint));
        }

        [Fact]
        public void TheCardWaitsBehindACardAlreadyUp()
        {
            //A level with a kind card queued at its start: the glass's (#735), on a save that has not met it
            var tutorial = Over(new HashSet<string>(), new List<string>());
            var kinds = new Prazsky.BS3D.GameStructure.BallType[Tutorial.KIND_COUNT];
            kinds[(int)Prazsky.BS3D.GameStructure.BallKind.Transparent] = Prazsky.BS3D.GameStructure.BallType.Type2;
            tutorial.BeginLevel(-1, 0, 0, ceilingStep: null, kindsOnMap: kinds, aimHintArmed: true);

            Run(tutorial, 1.5f);
            string kindCaption = tutorial.Caption;
            Assert.NotNull(kindCaption);

            //The hint fires while the kind card is up: the kind card stays, and the hint follows it
            tutorial.Trigger(Tutorial.Lesson.AimHint);
            Run(tutorial, 0.5f);
            Assert.Equal(kindCaption, tutorial.Caption);

            var seen = new List<string>();
            for (int i = 0; i < 200; i++)
            {
                Run(tutorial, 0.1f);
                if (tutorial.Caption != null && (seen.Count == 0 || seen[^1] != tutorial.Caption)) seen.Add(tutorial.Caption);
            }
            Assert.Contains("Hold to look down the barrel", seen);
        }
    }
}
