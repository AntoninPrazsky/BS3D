using BS3D.Screens;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The precise-aim hint (#1012), the owner's rule of 2026-10-28: when a shot makes no match more than twice, show the
    /// hint to aim with the right button - the third unscored shot in a row fired outside precise aim shows it, and every
    /// third after it while the misses go on. And the card: armed on any level the session asks, as often as it is due,
    /// never written to the save, behind a card already up, done by holding the button.
    /// </summary>
    public class PreciseAimHintTests
    {
        //Shots fired outside the lean that drop nothing, until the hint is due; the session shows it when it is
        private static int MissesUntilDue(PreciseAimHint hint, int most)
        {
            for (int shot = 1; shot <= most; shot++)
            {
                hint.NoteShotFired(leaned: false);
                if (hint.NoteShotResolved(scored: false))
                {
                    hint.NoteShown();
                    return shot;
                }
            }

            return -1;
        }

        [Fact]
        public void TheThirdMissInARowShowsIt()
        {
            //"More than twice": not on the first or the second, on the third
            Assert.Equal(3, PreciseAimHint.MISSES_IN_A_ROW);
            Assert.Equal(PreciseAimHint.MISSES_IN_A_ROW, MissesUntilDue(new PreciseAimHint(), 100));
        }

        [Fact]
        public void ItComesBackEveryThirdMissWhileTheMissesGoOn()
        {
            var hint = new PreciseAimHint();

            for (int showing = 0; showing < 10; showing++)
                Assert.Equal(PreciseAimHint.MISSES_IN_A_ROW, MissesUntilDue(hint, 100));
        }

        [Fact]
        public void AMatchStartsTheCountAgain()
        {
            var hint = new PreciseAimHint();

            for (int round = 0; round < 10; round++)
            {
                hint.NoteShotFired(leaned: false);
                Assert.False(hint.NoteShotResolved(scored: false));
                hint.NoteShotFired(leaned: false);
                Assert.False(hint.NoteShotResolved(scored: false));
                hint.NoteShotFired(leaned: false);
                Assert.False(hint.NoteShotResolved(scored: true));
            }
        }

        [Fact]
        public void AMissFromTheLeanStartsTheCountAgainAndNeverCallsForIt()
        {
            var hint = new PreciseAimHint();

            for (int shot = 0; shot < 20; shot++)
            {
                hint.NoteShotFired(leaned: true);
                Assert.False(hint.NoteShotResolved(scored: false));
            }

            //Two misses outside, one from the lean: the next outside miss is the first of a new run
            hint.NoteShotFired(leaned: false);
            hint.NoteShotResolved(scored: false);
            hint.NoteShotFired(leaned: false);
            hint.NoteShotResolved(scored: false);
            hint.NoteShotFired(leaned: true);
            hint.NoteShotResolved(scored: false);
            Assert.Equal(PreciseAimHint.MISSES_IN_A_ROW, MissesUntilDue(hint, 100));
        }

        [Fact]
        public void TwoShotsInTheAirAreEachJudgedByTheirOwnLean()
        {
            var hint = new PreciseAimHint();
            hint.NoteShotFired(leaned: false);
            hint.NoteShotResolved(scored: false);
            hint.NoteShotFired(leaned: false);
            hint.NoteShotResolved(scored: false);

            //A leaned shot, then one from the hip fired before the first lands: the first's miss starts the count again, the
            //second's is the first of a new run - never the third of the old one
            hint.NoteShotFired(leaned: true);
            hint.NoteShotFired(leaned: false);
            Assert.False(hint.NoteShotResolved(scored: false));
            Assert.False(hint.NoteShotResolved(scored: false));
            Assert.Equal(1, hint.MissesInARow);
        }

        [Fact]
        public void ALevelStartsTheCountAgain()
        {
            var hint = new PreciseAimHint();
            hint.NoteShotFired(leaned: false);
            hint.NoteShotResolved(scored: false);
            hint.NoteShotFired(leaned: false);
            hint.NoteShotResolved(scored: false);

            hint.BeginLevel();
            Assert.Equal(0, hint.MissesInARow);
            Assert.Equal(PreciseAimHint.MISSES_IN_A_ROW, MissesUntilDue(hint, 100));
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

        private const string HINT_CAPTION = "Hold to aim precisely";

        [Fact]
        public void TheCardComesBackOnTheSameLevelAndIsNeverWrittenToTheSave()
        {
            var written = new List<string>();
            var tutorial = Over(new HashSet<string>(), written);
            tutorial.BeginLevel(-1, 0, 0, ceilingStep: null, aimHintArmed: true);

            for (int showing = 0; showing < 3; showing++)
            {
                Assert.True(tutorial.Trigger(Tutorial.Lesson.AimHint));
                Run(tutorial, 1f);
                Assert.Equal(HINT_CAPTION, tutorial.Caption);

                //Up already: a second call while it stands puts up nothing more
                Assert.False(tutorial.Trigger(Tutorial.Lesson.AimHint));

                //Unheld, it goes once it has been read, and stays armed for the next run of misses on the same level
                Run(tutorial, 9f);
                Assert.Null(tutorial.Caption);
                Assert.True(tutorial.IsArmed(Tutorial.Lesson.AimHint));
            }

            //Held, it is done and praised - and still not written, so it comes back
            Assert.True(tutorial.Trigger(Tutorial.Lesson.AimHint));
            Run(tutorial, 2f, holding: true);
            Assert.True(tutorial.Praising || tutorial.Caption == null);
            Run(tutorial, 3f);

            Assert.DoesNotContain("aimhint", written);
            Assert.True(tutorial.IsArmed(Tutorial.Lesson.AimHint));
            Assert.True(tutorial.Trigger(Tutorial.Lesson.AimHint));
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
            Assert.Contains(HINT_CAPTION, seen);
        }
    }
}
