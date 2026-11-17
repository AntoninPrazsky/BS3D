using BS3D.Screens;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Time in the score (#1045, rules v2, the owner's option B of 2026-11-17): the time bonus and its ceiling, the star
    /// lines, the par rule and the par every shipped level carries, the par in a level's identity and in the set's loader, a
    /// save's best tagged with its rules, and the readout the HUD drains under the score. Every figure is the classes' own:
    /// the rule is pinned through its constants, and <c>Tools/ScoreSim</c> judges whether it rates the levels the right way
    /// round.
    /// </summary>
    public class TimeInTheScoreTests
    {
        private const int BALLS = 238, BUDGET = 30, PAR = 20;

        private static ScoreKeeper Level(int? par = PAR) => new(BUDGET, null, BALLS, par);

        private static int Floor => ScoreKeeper.MatchedBallPoints * BALLS;

        [Fact]
        public void The_bonus_is_ten_floors_at_the_start_half_at_par_and_none_at_twice_par()
        {
            ScoreKeeper level = Level();

            Assert.Equal(ScoreKeeper.TimeBonusWorthInFloors * Floor, level.TimeBonusMax);
            Assert.Equal(level.TimeBonusMax, level.TimeBonusAt(0f));
            Assert.Equal(level.TimeBonusMax / 2, level.TimeBonusAt(PAR));
            Assert.Equal(0, level.TimeBonusAt(ScoreKeeper.TimeBonusZeroAtPars * PAR));
            Assert.Equal(0, level.TimeBonusAt(10_000f));
        }

        /// <summary>The seconds are counted whole, every second begun, so the figure steps once a second and a clear in it is
        /// paid exactly what the readout showed through it.</summary>
        [Theory]
        [InlineData(0.01f, 1)]
        [InlineData(0.99f, 1)]
        [InlineData(1f, 1)]
        [InlineData(1.01f, 2)]
        [InlineData(13.5f, 14)]
        public void The_bonus_counts_whole_seconds_begun(float seconds, int whole)
        {
            ScoreKeeper level = Level();
            Assert.Equal(level.TimeBonusAt(whole), level.TimeBonusAt(seconds));
        }

        [Fact]
        public void A_faster_clear_is_never_paid_less()
        {
            ScoreKeeper level = Level();
            int previous = int.MaxValue;

            for (float t = 0f; t <= ScoreKeeper.TimeBonusZeroAtPars * PAR + 5; t += 0.25f)
            {
                int bonus = level.TimeBonusAt(t);
                Assert.True(bonus <= previous, $"{t} s pays {bonus}, more than {previous} a moment earlier");
                Assert.True(bonus >= 0);
                previous = bonus;
            }
        }

        [Fact]
        public void A_level_with_no_par_or_no_balls_has_no_time_bonus()
        {
            Assert.Equal(0, Level(par: null).TimeBonusAt(0f));
            Assert.Equal(0, Level(par: 0).TimeBonusAt(0f));
            Assert.Equal(0, new ScoreKeeper(BUDGET, null, 0, PAR).TimeBonusAt(0f));
            Assert.Null(Level(par: null).ParSeconds);
        }

        [Fact]
        public void The_bonus_is_awarded_into_the_score_and_the_base_but_not_the_streak()
        {
            ScoreKeeper level = Level();
            level.Landed(3, 0);
            level.Landed(3, 0);
            int streak = level.StreakBonus, before = level.Score;

            int bonus = level.AwardTimeBonus(12.3f);

            Assert.Equal(level.TimeBonusAt(12.3f), bonus);
            Assert.Equal(bonus, level.TimeBonusAwarded);
            Assert.Equal(12.3f, level.TimeBonusSeconds);
            Assert.Equal(before + bonus, level.Score);
            Assert.Equal(streak, level.StreakBonus);
        }

        /// <summary>The unused balls pay one average shot each since rules v2, and the version says the rules changed.</summary>
        [Fact]
        public void An_unused_ball_is_worth_one_average_shot_under_rules_two()
        {
            Assert.Equal(2, ScoreKeeper.RulesVersion);
            Assert.Equal(1, ScoreKeeper.UnusedShotWorthInShots);
            Assert.Equal(ScoreKeeper.MatchedBallPoints * BALLS / BUDGET, Level().UnusedShotValue);
        }

        /// <summary>
        /// The ceiling holds the whole time bonus, and at a claimed time only what that time is paid: a perfect clear (the
        /// whole level orphaned at once, every other ball unused) at any time stays under the ceiling at that time.
        /// </summary>
        [Theory]
        [InlineData(0.5f)]
        [InlineData(5f)]
        [InlineData(PAR)]
        [InlineData(3 * PAR)]
        public void A_perfect_clear_is_under_the_ceiling_at_its_own_time(float seconds)
        {
            ScoreKeeper keeper = Level();
            keeper.Shot();
            keeper.Landed(matched: 3, orphaned: BALLS - 3);
            keeper.AwardCompletionBonus();
            keeper.AwardTimeBonus(seconds);

            int ceiling = ScoreKeeper.ScoreCeilingAt(BALLS, BUDGET, PAR, seconds);
            Assert.True(keeper.Score < ceiling, $"{keeper.Score} at {seconds} s is not under the ceiling of {ceiling} there");
            Assert.True(ceiling <= ScoreKeeper.ScoreCeiling(BALLS, BUDGET, PAR));
        }

        [Fact]
        public void The_ceiling_gains_the_whole_time_bonus_and_falls_with_the_claimed_time()
        {
            ScoreKeeper level = Level();
            Assert.Equal(ScoreKeeper.ScoreCeiling(BALLS, BUDGET) + level.TimeBonusMax, ScoreKeeper.ScoreCeiling(BALLS, BUDGET, PAR));
            Assert.Equal(ScoreKeeper.ScoreCeiling(BALLS, BUDGET),
                ScoreKeeper.ScoreCeilingAt(BALLS, BUDGET, PAR, ScoreKeeper.TimeBonusZeroAtPars * PAR));
            Assert.True(ScoreKeeper.ScoreCeilingAt(BALLS, BUDGET, PAR, PAR) < ScoreKeeper.ScoreCeiling(BALLS, BUDGET, PAR));
        }

        /// <summary>
        /// The star lines of option B, rising, and the reason four stars need time: a clean clear of matched balls scores at
        /// most five floors at the cap and less than one more for its unused balls, under even the three-star line, so the
        /// difference to four stars is the time bonus's.
        /// </summary>
        [Fact]
        public void The_star_lines_are_option_b_and_four_stars_need_time()
        {
            Assert.Equal((2.5f, 6.5f, 11f),
                (StarRating.TWO_STAR_FLOOR_MULTIPLE, StarRating.THREE_STAR_FLOOR_MULTIPLE, StarRating.FOUR_STAR_FLOOR_MULTIPLE));

            //Every ball matched at the cap, every ball of the budget but one unused: no clean clear of matched balls beats it
            int capped = ScoreKeeper.MaxMultiplier * Floor + (BUDGET - 1) * Level().UnusedShotValue;

            Assert.True(StarRating.Rate(capped, BALLS) < StarRating.MAX);
            Assert.Equal(StarRating.MAX, StarRating.Rate(capped + Level().TimeBonusAt(PAR * 0.5f), BALLS));
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 15)]
        [InlineData(4, 20)]
        [InlineData(5, 25)]
        [InlineData(6, 25)]
        [InlineData(24, 70)]
        public void The_par_is_ten_seconds_and_two_and_a_half_a_shot_rounded_up_to_five(int shots, int par) =>
            Assert.Equal(par, ParRule.FromShots(shots));

        [Theory]
        [InlineData(20f, 20)]
        [InlineData(20.01f, 25)]
        [InlineData(0.5f, 5)]
        public void A_measured_par_is_rounded_up_to_five(float seconds, int par) => Assert.Equal(par, ParRule.RoundUp(seconds));

        /// <summary>Every shipped level with a budget has a par the rule could have made: a multiple of five, at least the rule's
        /// for one shot. A level without one could earn no time bonus and no four stars.</summary>
        [Fact]
        public void Every_shipped_level_has_a_par()
        {
            LevelSet set = LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

            foreach (LevelSetEntry entry in set.Levels.Where(e => e.Shots.HasValue))
            {
                Assert.True(entry.Par.HasValue, $"{entry.File} has no par");
                Assert.True(entry.Par.Value % ParRule.ROUND_TO_SECONDS == 0 && entry.Par.Value >= ParRule.FromShots(1),
                    $"{entry.File}'s par of {entry.Par} s is not one the rule makes");
            }
        }

        [Fact]
        public void The_par_is_part_of_a_levels_identity()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("{\"format\":\"bs3d-level\"}");
            LevelSetEntry entry = new() { File = "One.json", Shots = 30, CeilingStep = 5 };

            string none = LevelIdentity.Of(entry, bytes).Hash;
            entry.Par = 40;
            string forty = LevelIdentity.Of(entry, bytes).Hash;
            entry.Par = 45;
            string fortyFive = LevelIdentity.Of(entry, bytes).Hash;

            Assert.NotEqual(none, forty);
            Assert.NotEqual(forty, fortyFive);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void A_set_with_a_par_of_no_seconds_is_refused(int par)
        {
            using TempDirectory temp = new();
            string path = temp.File(LevelSet.DefaultFileName);
            File.WriteAllText(path, "{\"format\":\"bs3d-levels\",\"version\":1,\"levels\":[{\"file\":\"One.json\",\"shots\":30,\"par\":"
                + par + "}]}");

            Assert.Throws<InvalidDataException>(() => LevelSet.Load(path));
        }

        /// <summary>The readout under the score shows exactly what a clear now is paid, its bar drains from full to empty over
        /// twice par, and it shows nothing on a level with no par or once the level is decided.</summary>
        [Fact]
        public void The_readout_shows_what_a_clear_now_is_paid()
        {
            ScoreKeeper level = Level();

            for (float t = 0f; t < 50f; t += 0.37f)
                Assert.Equal(level.TimeBonusAt(t), TimeBonusReadout.Shown(level, t));

            Assert.Equal(1f, TimeBonusReadout.Share(level, 0f));
            Assert.Equal(0.5f, TimeBonusReadout.Share(level, PAR), 3);
            Assert.Equal(0f, TimeBonusReadout.Share(level, ScoreKeeper.TimeBonusZeroAtPars * PAR + 1));

            Assert.Equal(-1, TimeBonusReadout.Shown(level, -1f));
            Assert.Equal(-1, TimeBonusReadout.Shown(Level(par: null), 3f));
            Assert.Equal(0f, TimeBonusReadout.Share(Level(par: null), 3f));
        }

        /// <summary>
        /// A save's best carries its rules (#1045): the first clear under rules v2 replaces a v1 best whatever the two figures,
        /// keeps the stars, and from then on only a better v2 score is a best.
        /// </summary>
        [Fact]
        public void A_v1_best_gives_way_to_the_first_v2_clear_and_keeps_its_stars()
        {
            using TempDirectory temp = new();
            PlayerProgress progress = PlayerProgress.Load(temp.File(PlayerProgress.DefaultFileName));

            Assert.True(progress.Record("One.json", 5000, 4, rulesVersion: 1));
            Assert.True(progress.Record("One.json", 1200, 2, rulesVersion: 2));

            Assert.Equal(1200, progress.ScoreFor("One.json", 2));
            Assert.Equal(0, progress.ScoreFor("One.json", 1));
            Assert.Equal(4, progress.StarsFor("One.json"));

            Assert.False(progress.Record("One.json", 1100, 3, rulesVersion: 2));
            Assert.True(progress.Record("One.json", 1300, 3, rulesVersion: 2));
            Assert.Equal(1300, progress.ScoreFor("One.json"));
        }

        /// <summary>A save written before the rules were recorded loads as it did, every best version 1, and a best written now
        /// says its version in the file.</summary>
        [Fact]
        public void An_old_save_loads_its_bests_as_rules_one()
        {
            using TempDirectory temp = new();
            string path = temp.File(PlayerProgress.DefaultFileName);
            File.WriteAllText(path, "{\"format\":\"bs3d-progress\",\"version\":1,\"levels\":{\"One.json\":{\"score\":4820,\"stars\":4}}}");

            PlayerProgress progress = PlayerProgress.Load(path);
            Assert.Equal(ProgressLoad.Loaded, progress.Outcome);
            Assert.Equal(4820, progress.ScoreFor("One.json", 1));
            Assert.Equal(0, progress.ScoreFor("One.json", ScoreKeeper.RulesVersion));
            Assert.Equal(4, progress.TotalStars);

            progress.Record("Five.json", 900, 2);
            progress.Save();
            Assert.Contains("\"rules\": " + ScoreKeeper.RulesVersion, File.ReadAllText(path));
        }

        /// <summary>
        /// The readout's clock through a level's phases: the play clock while it is played, frozen at the clear through the
        /// cleared beat (so the readout stands at the figure the clear was paid and fades with the play readouts, and the streak
        /// under it does not jump up), and none on a loss or under the page.
        /// </summary>
        [Fact]
        public void The_readout_stands_at_the_clears_figure_through_the_beat_and_goes_on_a_loss()
        {
            Assert.Equal(12.5f, TimeBonusReadout.Clock(LevelPhase.Playing, 12.5f, 0f));
            Assert.Equal(9.2f, TimeBonusReadout.Clock(LevelPhase.ClearedBeat, 12.5f, 9.2f));
            Assert.Equal(-1f, TimeBonusReadout.Clock(LevelPhase.LossHold, 12.5f, 9.2f));
            Assert.Equal(-1f, TimeBonusReadout.Clock(LevelPhase.Over, 12.5f, 9.2f));

            ScoreKeeper level = Level();
            Assert.Equal(level.TimeBonusAt(9.2f), TimeBonusReadout.Shown(level, TimeBonusReadout.Clock(LevelPhase.ClearedBeat, 12.5f, 9.2f)));
        }

        /// <summary>
        /// A level cleared only under rules v1 (#1045): its tile on the picker keeps its stars and shows no score, not a "0", and
        /// the career total leaves its v1 best out. The first v2 clear puts its score on the tile and into the total.
        /// </summary>
        [Fact]
        public void A_level_cleared_only_under_older_rules_shows_its_stars_and_no_score()
        {
            using TempDirectory temp = new();
            string path = temp.File(PlayerProgress.DefaultFileName);
            File.WriteAllText(path, "{\"format\":\"bs3d-progress\",\"version\":1,\"levels\":{"
                + "\"Pennant.json\":{\"score\":4820,\"stars\":4},\"Rainbow.json\":{\"score\":3100,\"stars\":3}}}");
            PlayerProgress progress = PlayerProgress.Load(path);

            foreach (string file in new[] { "Pennant.json", "Rainbow.json" })
            {
                bool rated = progress.StarsFor(file) > 0;
                Assert.True(rated, "the stars are kept");
                Assert.Equal(string.Empty, PickerCopy.TileScore(rated, progress.ScoreFor(file)));
            }
            Assert.Equal(0, progress.TotalScore);

            progress.Record("Pennant.json", 2554, 3);
            Assert.Equal(ScoreText.Of(2554), PickerCopy.TileScore(progress.StarsFor("Pennant.json") > 0, progress.ScoreFor("Pennant.json")));
            Assert.Equal(4, progress.StarsFor("Pennant.json"));
            Assert.Equal(2554, progress.TotalScore);

            //And no rating, no score, whatever the figure
            Assert.Equal(string.Empty, PickerCopy.TileScore(false, 2554));
        }

        [Fact]
        public void The_readout_and_the_award_and_the_result_row_say_the_same_words()
        {
            Assert.Equal(AwardZoom.TIME_BONUS_CAPTION, TimeBonusReadout.LABEL.ToUpperInvariant());
        }
    }
}
