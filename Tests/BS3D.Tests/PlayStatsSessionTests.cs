using Prazsky.BS3D.Levels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What the game's play counts hand to the online client (#876): a run kept off the boards - one that used the dev build's
    /// debug row - is counted in the local file like any other and handed to nothing online. A recording stand-in takes the
    /// place of the client, so the question is asked of the real <see cref="PlayStatsSession"/> and no device.
    /// </summary>
    public class PlayStatsSessionTests
    {
        private static readonly LevelIdentity One = new("One.json", "0123456789abcdef");

        [Fact]
        public void A_run_kept_off_the_boards_is_counted_in_the_file_and_handed_to_nothing_online()
        {
            using TempDirectory temp = new();
            string path = temp.File(PlayStats.DefaultFileName);
            List<LevelPlayCounts> sent = new();

            using (PlayStatsSession session = new(path, sent.Add))
            {
                //The start goes before the debug row can have been used, so it is handed over as ever
                session.Started(One);
                session.Ended(PlayEnding.Cleared, shots: 20, seconds: 60f, firstClear: true, online: false);

                session.Started(One);
                session.Abandoned(shots: 3, seconds: 5f, online: false);

                session.Started(One);
                session.Ended(PlayEnding.OutOfBalls, shots: 9, seconds: 30f, firstClear: false, online: false);
            }

            //Only the three starts left the machine: no clear, no loss and no abandon
            Assert.Equal(3, sent.Count);
            Assert.All(sent, change =>
            {
                Assert.Equal(1, change.Starts);
                Assert.Equal(0, change.Clears + change.LostOutOfBalls + change.LostLine + change.Abandons);
            });

            //The player's own file counts all of it, as the save keeps the record
            LevelPlayCounts row = Assert.Single(PlayStats.Load(path).Levels);
            Assert.Equal((3, 1, 1, 1, 20 + 3 + 9), (row.Starts, row.Clears, row.Abandons, row.LostOutOfBalls, row.Shots));
        }

        [Fact]
        public void An_ordinary_run_is_handed_to_the_online_client_ending_and_all()
        {
            using TempDirectory temp = new();
            List<LevelPlayCounts> sent = new();

            using (PlayStatsSession session = new(temp.File(PlayStats.DefaultFileName), sent.Add))
            {
                session.Started(One);
                session.Ended(PlayEnding.Cleared, shots: 20, seconds: 60f, firstClear: true);

                session.Started(One);
                session.Abandoned(shots: 3, seconds: 5f);
            }

            //Two starts, one clear and one abandon: every event of an ordinary run is handed over
            Assert.Equal(4, sent.Count);
            Assert.Equal(2, sent.Sum(change => change.Starts));
            Assert.Equal(1, sent.Sum(change => change.Clears));
            Assert.Equal(1, sent.Sum(change => change.Abandons));
        }

        [Fact]
        public void A_session_with_no_online_client_still_counts()
        {
            using TempDirectory temp = new();
            string path = temp.File(PlayStats.DefaultFileName);

            using (PlayStatsSession session = new(path, null))
            {
                session.Started(One);
                session.Ended(PlayEnding.Cleared, shots: 5, seconds: 6f, firstClear: true);
            }

            Assert.Equal(1, Assert.Single(PlayStats.Load(path).Levels).Clears);
        }
    }
}
