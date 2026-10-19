using BS3D.Screens;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The "fire" chip for a player who has stopped (#925): when it comes, what takes it down, what restarts its count and
    /// when it may come back, held as numbers. The rules are <see cref="FireHint"/>'s alone, so nothing here needs a session;
    /// the session's wiring of its flags is checked in the run (docs/game-feedback/fire-hint.md).
    /// </summary>
    public class FireHintTests
    {
        //A frame at 60 Hz
        private const float FRAME = 1f / 60f;

        //The owner's own first chapter, 2026-10-18 (Stats.json): the five levels cleared in 46 shots over 231.8 s of play clock
        private const float MEASURED_SECONDS_PER_SHOT = 231.8f / 46f;

        private static FireHint Level(bool eligible = true)
        {
            FireHint hint = new();
            hint.BeginLevel(eligible);
            return hint;
        }

        /// <summary>Runs <paramref name="seconds"/> of frames under <paramref name="distractions"/>; stops early when the chip is wanted if asked.</summary>
        private static void Run(FireHint hint, float seconds, in FireHint.Distractions distractions = default)
        {
            int frames = (int)MathF.Round(seconds / FRAME);
            for (int i = 0; i < frames; i++) hint.Update(FRAME, in distractions);
        }

        [Fact]
        public void The_first_pause_is_twice_the_measured_gap_and_the_rest_follow_from_it()
        {
            Assert.True(FireHint.FIRST_PAUSE_SECONDS >= 2f * MEASURED_SECONDS_PER_SHOT - 0.1f,
                $"the first pause is {FireHint.FIRST_PAUSE_SECONDS} s against {MEASURED_SECONDS_PER_SHOT:F1} s a shot measured");
            Assert.True(FireHint.FIRST_PAUSE_SECONDS >= 5f, "a floor of several seconds");
            Assert.True(FireHint.LATER_PAUSE_SECONDS >= 2f * FireHint.FIRST_PAUSE_SECONDS, "'much longer' than the first");
            Assert.True(FireHint.STAND_SECONDS < FireHint.LATER_PAUSE_SECONDS, "told once, then left alone for longer than it stood");
        }

        [Fact]
        public void It_comes_after_the_first_pause_and_not_before()
        {
            FireHint hint = Level();

            Run(hint, FireHint.FIRST_PAUSE_SECONDS - 0.1f);
            Assert.False(hint.Showing, "up before the pause was over");
            Assert.Equal(0f, hint.Presence);

            Run(hint, 0.2f);
            Assert.True(hint.Showing, "not up after the pause");

            Run(hint, FireHint.ARRIVE_SECONDS + 0.05f);
            Assert.Equal(1f, hint.Presence);
        }

        [Fact]
        public void A_level_that_does_not_carry_it_never_shows_it()
        {
            FireHint hint = Level(eligible: false);

            Run(hint, 3f * FireHint.LATER_PAUSE_SECONDS);

            Assert.False(hint.Showing);
            Assert.Equal(0f, hint.Presence);
        }

        [Fact]
        public void The_first_five_levels_of_the_first_chapter_carry_it_and_no_others()
        {
            for (int level = 0; level < FireHint.LEVELS; level++)
                Assert.True(FireHint.CarriesHint(0, level), $"level {level + 1} of the first chapter");

            Assert.Equal(5, FireHint.LEVELS);
            Assert.False(FireHint.CarriesHint(0, 5), "Diabolo, the sixth");
            Assert.False(FireHint.CarriesHint(0, 9), "the first chapter's last");
            Assert.False(FireHint.CarriesHint(1, 0), "the second chapter's first");
            Assert.False(FireHint.CarriesHint(-1, 0), "a level the tutorial does not reach");
            Assert.False(FireHint.CarriesHint(0, -1));
        }

        [Fact]
        public void A_shot_ends_the_pause_and_the_count_starts_again()
        {
            FireHint hint = Level();

            Run(hint, FireHint.FIRST_PAUSE_SECONDS - 0.1f);
            hint.NoteShot();

            Run(hint, FireHint.FIRST_PAUSE_SECONDS - 0.1f);
            Assert.False(hint.Showing, "the count did not start again from the shot");

            Run(hint, 0.3f);
            Assert.True(hint.Showing);
        }

        [Fact]
        public void A_shot_takes_it_down_at_once_and_it_is_gone_in_the_leave_time()
        {
            FireHint hint = Level();
            Run(hint, FireHint.FIRST_PAUSE_SECONDS + 2f);
            Assert.Equal(1f, hint.Presence);

            hint.NoteShot();
            Assert.False(hint.Showing, "the shot did not take the chip down on its own frame");

            Run(hint, FireHint.LEAVE_SECONDS + 2 * FRAME);
            Assert.Equal(0f, hint.Presence);
        }

        [Fact]
        public void After_it_has_been_shown_it_returns_only_after_the_much_longer_pause()
        {
            FireHint hint = Level();
            Run(hint, FireHint.FIRST_PAUSE_SECONDS + 1f);
            Assert.True(hint.Showing);

            hint.NoteShot();
            Assert.Equal(FireHint.LATER_PAUSE_SECONDS, hint.Threshold);

            Run(hint, FireHint.LATER_PAUSE_SECONDS - 0.1f);
            Assert.False(hint.Showing, "back after a pause as short as the first");

            Run(hint, 0.3f);
            Assert.True(hint.Showing, "never back");
        }

        [Fact]
        public void A_shot_before_it_was_ever_shown_does_not_lengthen_the_next_pause()
        {
            FireHint hint = Level();

            Run(hint, 4f);
            hint.NoteShot();

            Assert.Equal(FireHint.FIRST_PAUSE_SECONDS, hint.Threshold);
        }

        [Fact]
        public void Told_and_ignored_it_leaves_by_itself_and_comes_back_only_after_the_longer_pause()
        {
            FireHint hint = Level();
            Run(hint, FireHint.FIRST_PAUSE_SECONDS + 0.1f);
            Assert.True(hint.Showing);

            Run(hint, FireHint.STAND_SECONDS + 0.1f);
            Assert.False(hint.Showing, "stood for ever");

            Run(hint, FireHint.LATER_PAUSE_SECONDS - 0.5f);
            Assert.False(hint.Showing, "nagged again before the longer pause");

            Run(hint, 1f);
            Assert.True(hint.Showing);
        }

        [Fact]
        public void A_new_level_starts_from_the_first_pause_again()
        {
            FireHint hint = Level();
            Run(hint, FireHint.FIRST_PAUSE_SECONDS + 1f);
            hint.NoteShot();
            Assert.Equal(FireHint.LATER_PAUSE_SECONDS, hint.Threshold);

            hint.BeginLevel(true);

            Assert.Equal(FireHint.FIRST_PAUSE_SECONDS, hint.Threshold);
            Assert.False(hint.Showing);
            Assert.Equal(0f, hint.Presence);
            Assert.Equal(0f, hint.Idle);
        }

        /// <summary>One flag at a time, so a rule dropped from <see cref="FireHint.Distractions.Any"/> is named.</summary>
        public static TheoryData<string> EveryDistraction() => new()
        {
            "TutorialOff", "LevelDecided", "CameraTakeover", "ReleaseWaiting", "CardStands", "CeilingAlarm", "FloorAlarm", "ShotInAir"
        };

        /// <summary>Exactly one flag set, by name: a switch with no default, so a flag renamed or removed fails to find its case.</summary>
        private static FireHint.Distractions Only(string name) => name switch
        {
            "TutorialOff" => new() { TutorialOff = true },
            "LevelDecided" => new() { LevelDecided = true },
            "CameraTakeover" => new() { CameraTakeover = true },
            "ReleaseWaiting" => new() { ReleaseWaiting = true },
            "CardStands" => new() { CardStands = true },
            "CeilingAlarm" => new() { CeilingAlarm = true },
            "FloorAlarm" => new() { FloorAlarm = true },
            "ShotInAir" => new() { ShotInAir = true },
            _ => throw new ArgumentException(name)
        };

        [Fact]
        public void Every_distraction_is_covered_by_the_theory()
        {
            //A flag added to Distractions without a case above would pass unseen
            int flags = 0;
            foreach (var property in typeof(FireHint.Distractions).GetProperties(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                if (property.PropertyType == typeof(bool) && property.Name != nameof(FireHint.Distractions.Any)) flags++;

            Assert.Equal(EveryDistraction().Count, flags);
        }

        [Theory]
        [MemberData(nameof(EveryDistraction))]
        public void A_distraction_holds_it_back_for_as_long_as_it_lasts(string name)
        {
            FireHint.Distractions distraction = Only(name);
            Assert.True(distraction.Any, $"{name} is not counted by Any");

            FireHint hint = Level();
            Run(hint, 3f * FireHint.LATER_PAUSE_SECONDS, in distraction);

            Assert.False(hint.Showing, $"shown over {name}");
            Assert.Equal(0f, hint.Presence);
            Assert.Equal(0f, hint.Idle);
        }

        [Theory]
        [MemberData(nameof(EveryDistraction))]
        public void A_distraction_restarts_the_count_so_the_chip_never_comes_on_its_heels(string name)
        {
            FireHint.Distractions distraction = Only(name);
            FireHint hint = Level();

            //Almost the whole pause passes, then the distraction comes for a second
            Run(hint, FireHint.FIRST_PAUSE_SECONDS - 0.1f);
            Run(hint, 1f, in distraction);

            //The pause is counted from when it is over
            Run(hint, FireHint.FIRST_PAUSE_SECONDS - 0.1f);
            Assert.False(hint.Showing, $"the pause was not counted from the end of {name}");

            Run(hint, 0.3f);
            Assert.True(hint.Showing);
        }

        [Theory]
        [MemberData(nameof(EveryDistraction))]
        public void A_distraction_takes_a_chip_that_is_up_down_and_it_returns_after_the_longer_pause(string name)
        {
            FireHint.Distractions distraction = Only(name);
            FireHint hint = Level();
            Run(hint, FireHint.FIRST_PAUSE_SECONDS + 2f);
            Assert.True(hint.Showing);

            hint.Update(FRAME, in distraction);
            Assert.False(hint.Showing, $"still wanted on the frame {name} began");

            Run(hint, FireHint.LEAVE_SECONDS + 2 * FRAME, in distraction);
            Assert.Equal(0f, hint.Presence);

            //Shown once in this level, so the longer pause applies
            Run(hint, FireHint.LATER_PAUSE_SECONDS - 0.1f);
            Assert.False(hint.Showing);
            Run(hint, 0.3f);
            Assert.True(hint.Showing);
        }

        [Fact]
        public void The_beat_kicks_once_a_second_and_settles_between()
        {
            Assert.Equal(1f, FireHint.Beat(0f), 3);
            Assert.Equal(1f, FireHint.Beat(1f), 2);
            Assert.Equal(1f, FireHint.Beat(7f), 2);

            //Settled before the next
            Assert.True(FireHint.Beat(0.9f) < 0.01f, $"{FireHint.Beat(0.9f)} just before the next beat");

            //Falls monotonically inside a second
            float last = 2f;
            for (float age = 0f; age < 0.999f; age += 0.01f)
            {
                float beat = FireHint.Beat(age);
                Assert.InRange(beat, 0f, 1f);
                Assert.True(beat <= last + 1e-6f, $"rises at {age}");
                last = beat;
            }

            //Exactly one kick in each of three seconds, however it is sampled
            int kicks = 0;
            float previous = 0f;
            for (float age = 0f; age < 3f; age += 0.004f)
            {
                float beat = FireHint.Beat(age);
                if (beat > previous + 0.5f) kicks++;
                previous = beat;
            }

            Assert.True(kicks is >= 2 and <= 3, $"{kicks} kicks in three seconds");
        }

        [Fact]
        public void The_beat_swell_leaves_the_chip_in_its_place()
        {
            Assert.InRange(FireHint.BEAT_SWELL, 0.05f, 0.3f);
            Assert.Equal(1f, FireHint.BEAT_SECONDS);
        }

        [Fact]
        public void The_word_is_one_plain_imperative_set_by_the_rules()
        {
            Assert.Null(CopyRules.Fault(FireHint.CAPTION));
            Assert.Equal("Fire!", FireHint.CAPTION);
            Assert.DoesNotContain(' ', FireHint.CAPTION);
        }

        [Fact]
        public void A_card_stands_from_the_level_start_through_the_gaps_between_cards_until_the_last_has_left()
        {
            //The opener's three cards, none of them done: each action card gives up on its own clock, one after another
            Tutorial tutorial = new(_ => false, _ => { }, Tutorial.Mode.Normal);
            tutorial.BeginLevel(0, 0, 10, ceilingStep: 6);
            Assert.True(tutorial.CardStands, "the opener's cards are queued and the chip would talk over them");

            bool gone = false;
            int cardsSeen = 0;
            string last = null;
            for (int frame = 0; frame < 4000; frame++)
            {
                tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: false);

                if (tutorial.Caption != null)
                {
                    Assert.True(tutorial.CardStands, "a card on screen that CardStands does not count");
                    if (tutorial.Caption != last) cardsSeen++;
                    last = tutorial.Caption;
                }

                //Once nothing stands it must stay so: a flicker in the gap between two cards would let the chip in
                if (!tutorial.CardStands) gone = true;
                else Assert.False(gone, "CardStands came back after the queue had emptied");
            }

            Assert.True(cardsSeen >= 3, $"{cardsSeen} cards walked");
            Assert.True(gone, "the queue never emptied");
        }

        [Fact]
        public void No_card_stands_on_a_level_the_tutorial_does_not_reach_or_after_a_decided_level()
        {
            Tutorial tutorial = new(_ => false, _ => { }, Tutorial.Mode.Normal);
            tutorial.BeginLevel(-1, -1, 0, ceilingStep: 6);
            Assert.False(tutorial.CardStands);

            tutorial.BeginLevel(0, 0, 10, ceilingStep: 6);
            Assert.True(tutorial.CardStands);

            tutorial.Update(0.1f, enabled: true, takeoverEngaged: false, levelDecided: true);
            Assert.False(tutorial.CardStands, "a decided level drops the queue (#700)");
        }
    }
}
