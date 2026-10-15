using Prazsky.BS3D.Levels;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The play counts (#709): what each event adds (<see cref="PlayStatsRecorder"/>), and the file
    /// (<see cref="PlayStats"/>) held to every player file's rules — lenient, atomic, an unreadable one kept aside.
    /// </summary>
    public class PlayStatsTests
    {
        private static readonly LevelIdentity One = new("One.json", "0123456789abcdef");
        private static readonly LevelIdentity Two = new("Two.json", "fedcba9876543210");

        private static PlayStatsRecorder Fresh() => new(new PlayStats());

        [Fact]
        public void A_session_of_events_counts_what_was_done()
        {
            PlayStatsRecorder r = Fresh();

            r.Started(One);
            r.Ended(PlayEnding.OutOfBalls, shots: 30, seconds: 95.5, firstClear: false);
            r.Started(One);                                   //Retry after a loss
            r.Abandoned(shots: 4, seconds: 10);               //the pause's Restart
            r.Started(One);                                   //and the restart itself
            r.Ended(PlayEnding.Cleared, shots: 20, seconds: 60, firstClear: true);
            r.Started(One);                                   //a replay after a clear is not a retry
            r.Ended(PlayEnding.ClusterReachedLine, shots: 12, seconds: 40, firstClear: false);
            r.Skipped(One);
            r.Started(Two);                                   //another level is never a retry
            r.Abandoned(shots: 1, seconds: 2);                //the game closed mid-level

            LevelPlayCounts one = r.Totals.Find(One.File, One.Hash, Prazsky.BS3D.Scoring.ScoreKeeper.RulesVersion);
            Assert.Equal(4, one.Starts);
            Assert.Equal(2, one.Retries);
            Assert.Equal(1, one.Clears);
            Assert.Equal(1, one.LostOutOfBalls);
            Assert.Equal(1, one.LostLine);
            Assert.Equal(1, one.Abandons);
            Assert.Equal(1, one.Skips);
            Assert.Equal(1, one.FirstClears);
            Assert.Equal(3, one.StartsToFirstClear);          //the first clear took three starts
            Assert.Equal(30 + 4 + 20 + 12, one.Shots);
            Assert.Equal(95.5 + 10 + 60 + 40, one.Seconds, 3);

            LevelPlayCounts two = r.Totals.Find(Two.File, Two.Hash, Prazsky.BS3D.Scoring.ScoreKeeper.RulesVersion);
            Assert.Equal(1, two.Starts);
            Assert.Equal(0, two.Retries);
            Assert.Equal(1, two.Abandons);
        }

        [Fact]
        public void A_closed_attempt_is_counted_once_and_no_attempt_counts_nothing()
        {
            PlayStatsRecorder r = Fresh();

            Assert.Null(r.Abandoned(5, 5));
            Assert.Null(r.Ended(PlayEnding.Cleared, 5, 5, firstClear: true));

            r.Started(One);
            Assert.NotNull(r.Ended(PlayEnding.OutOfBalls, 10, 10, firstClear: false));

            //TearDown after the result page's Main Menu, and the exit after it: the attempt had ended
            Assert.Null(r.Abandoned(10, 10));
            Assert.Null(r.Abandoned(10, 10));

            LevelPlayCounts one = Assert.Single(r.Totals.Levels);
            Assert.Equal(0, one.Abandons);
            Assert.Equal(1, one.LostOutOfBalls);
        }

        [Fact]
        public void A_level_on_no_board_is_not_counted()
        {
            PlayStatsRecorder r = Fresh();

            Assert.Null(r.Started(null));
            Assert.False(r.AttemptOpen);
            Assert.Null(r.Abandoned(3, 3));
            Assert.Null(r.Skipped(null));
            Assert.Empty(r.Totals.Levels);
        }

        [Fact]
        public void A_retry_is_a_start_of_the_last_board_while_it_is_uncleared()
        {
            PlayStatsRecorder r = Fresh();

            Assert.Equal(0, r.Started(One).Retries);
            r.Abandoned(0, 0);
            Assert.Equal(1, r.Started(One).Retries);          //the level left and picked again
            r.Ended(PlayEnding.Cleared, 1, 1, firstClear: false);
            Assert.Equal(0, r.Started(One).Retries);          //replayed for a better score
            r.Ended(PlayEnding.OutOfBalls, 1, 1, firstClear: false);
            Assert.Equal(1, r.Started(One).Retries);

            //Another rules version is another board
            r.Abandoned(0, 0);
            Assert.Equal(0, r.Started(One, rules: 99).Retries);
        }

        [Fact]
        public void Each_change_is_its_own_row_and_the_totals_add_them()
        {
            PlayStatsRecorder r = Fresh();

            LevelPlayCounts start = r.Started(One);
            LevelPlayCounts end = r.Ended(PlayEnding.Cleared, 7, 12.346, firstClear: true);

            Assert.Equal(1, start.Starts);
            Assert.Equal(0, start.Clears);
            Assert.Equal(0, end.Starts);
            Assert.Equal(1, end.Clears);
            Assert.Equal(7, end.Shots);
            Assert.Equal(12.35, end.Seconds, 3);              //rounded to hundredths, as the boards' time is
            Assert.Equal(1, end.StartsToFirstClear);
            Assert.True(start.SameBoard(end));
        }

        [Fact]
        public void The_file_round_trips_and_a_missing_one_is_a_fresh_start()
        {
            using TempDirectory temp = new();
            string path = temp.File(PlayStats.DefaultFileName);

            PlayStats fresh = PlayStats.Load(path);
            Assert.Equal(ProgressLoad.Fresh, fresh.Outcome);
            Assert.Empty(fresh.Levels);

            PlayStatsRecorder r = new(fresh);
            r.Started(One);
            r.Ended(PlayEnding.ClusterReachedLine, 9, 33, firstClear: false);
            fresh.Save();
            r.Skipped(One);
            fresh.Save();

            PlayStats loaded = PlayStats.Load(path);
            Assert.Equal(ProgressLoad.Loaded, loaded.Outcome);
            LevelPlayCounts row = Assert.Single(loaded.Levels);
            Assert.Equal((1, 1, 1, 9), (row.Starts, row.LostLine, row.Skips, row.Shots));
            Assert.True(File.Exists(path + PlayStats.BackupSuffix), "the last file is kept as the backup");
        }

        [Fact]
        public void An_unreadable_file_is_kept_aside_and_its_backup_answers()
        {
            using TempDirectory temp = new();
            string path = temp.File(PlayStats.DefaultFileName);

            PlayStats stats = PlayStats.Load(path);
            new PlayStatsRecorder(stats).Started(One);
            stats.Save();
            stats.Save();                                     //now the backup holds a good copy too

            File.WriteAllText(path, "{ not json");
            PlayStats recovered = PlayStats.Load(path);

            Assert.Equal(ProgressLoad.RecoveredFromBackup, recovered.Outcome);
            Assert.Single(recovered.Levels);
            Assert.NotNull(recovered.KeptUnreadable);
            Assert.Equal("{ not json", File.ReadAllText(recovered.KeptUnreadable));
            Assert.Equal(path, recovered.Path);
        }

        [Fact]
        public void A_newer_builds_file_is_not_read_and_not_lost()
        {
            using TempDirectory temp = new();
            string path = temp.File(PlayStats.DefaultFileName);
            File.WriteAllText(path, $$"""{ "format": "{{PlayStats.FormatMarker}}", "version": {{PlayStats.CurrentVersion + 1}}, "levels": [] }""");

            PlayStats stats = PlayStats.Load(path);

            Assert.Equal(ProgressLoad.Discarded, stats.Outcome);
            Assert.NotNull(stats.KeptUnreadable);
            Assert.Empty(stats.Levels);
        }

        [Fact]
        public void The_file_holds_counters_and_nothing_that_names_a_player()
        {
            PlayStatsRecorder r = Fresh();
            r.Started(One);
            r.Ended(PlayEnding.Cleared, 3, 4, firstClear: true);

            string json = r.Totals.Serialize();

            //The row's fields are what the anonymous batch sends, field for field: none of them is an id, a name or a time
            foreach (string word in new[] { "player", "name", "token", "date", "device", "address" })
                Assert.DoesNotContain(word, json, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
