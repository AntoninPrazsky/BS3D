using BS3D.Online;
using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What a High Scores row says of its level and which kind of row it is (#898). The page used to write "Locked" over a level
    /// ahead of the player and blank its scores; the owner ruled (2026-10-16) that every level shows its real board and the
    /// player's progress is the row's tint. These hold the half of that which needs no device: the texts, whatever the
    /// player has played, and the rule for a cleared level.
    /// </summary>
    public class HighScoresRowTests
    {
        private static BoardSummaryBody Board(string leader, int leaderScore, int leaderStars, int total, int myRank = 0,
            int myScore = 0, int myStars = 0) => new()
            {
                File = "Zigzag.json",
                Hash = "0123456789abcdef",
                Rules = 1,
                Total = total,
                Top = new BoardEntryBody { Rank = 1, Name = leader, Score = leaderScore, Stars = leaderStars },
                Me = myRank > 0 ? new BoardMeBody { Rank = myRank, Score = myScore, Stars = myStars } : null,
            };

        [Fact]
        public void A_level_the_player_has_not_played_shows_its_leader_and_score_and_no_place_of_theirs()
        {
            //The service names no place for a player who is not on the board - the case of every level ahead of them
            HighScoresRow.Texts row = HighScoresRow.Of(true, Board("Tereza", 41200, 4, total: 17));

            Assert.Equal("#1 Tereza", row.Leader);
            Assert.Equal("41 200", row.LeaderScore);
            Assert.True(row.HasLeaderScore);
            Assert.Equal(string.Empty, row.Place);
            Assert.Equal(string.Empty, row.PlaceScore);
            Assert.False(row.HasPlaceScore);
        }

        [Fact]
        public void A_place_with_no_rank_is_no_place()
        {
            //A summary that carries "me" with rank 0 says the player is off the board, as an absent "me" does
            HighScoresRow.Texts row = HighScoresRow.Of(true, Board("Tereza", 41200, 4, 17, myRank: 0));
            Assert.Equal(string.Empty, row.Place);

            BoardSummaryBody off = Board("Tereza", 41200, 4, 17);
            off.Me = new BoardMeBody { Rank = 0, Score = 0, Stars = 0 };
            Assert.Equal(string.Empty, HighScoresRow.Of(true, off).Place);
        }

        [Fact]
        public void A_level_the_player_is_on_shows_their_place_and_score()
        {
            HighScoresRow.Texts row = HighScoresRow.Of(true, Board("Tereza", 41200, 4, 17, myRank: 7, myScore: 30500, myStars: 3));

            Assert.Equal("You: 7th of 17", row.Place);
            Assert.Equal("30 500", row.PlaceScore);
            Assert.True(row.HasPlaceScore);
        }

        [Fact]
        public void The_player_leading_a_level_says_so()
        {
            HighScoresRow.Texts row = HighScoresRow.Of(true, Board("RDT", 41200, 4, 17, myRank: 1, myScore: 41200, myStars: 4));

            Assert.Equal("You lead", row.Place);
            Assert.Equal("#1 RDT", row.Leader);
        }

        [Fact]
        public void A_level_nobody_has_played_keeps_no_clears_yet_with_no_scores()
        {
            HighScoresRow.Texts row = HighScoresRow.Of(true, null);

            Assert.Equal("No clears yet", row.Leader);
            Assert.False(row.HasLeaderScore);
            Assert.False(row.HasPlaceScore);
            Assert.Equal(string.Empty, row.Place);
        }

        [Fact]
        public void Before_the_service_has_answered_a_row_says_nothing()
        {
            //The status line under the rows says why; an answer that has not come must not read as "No clears yet"
            HighScoresRow.Texts row = HighScoresRow.Of(false, null);

            Assert.Equal(string.Empty, row.Leader);
            Assert.False(row.HasLeaderScore);
            Assert.Equal(string.Empty, row.Place);
            Assert.False(row.HasPlaceScore);
        }

        [Fact]
        public void An_unfinished_attempt_is_marked_wherever_it_stands()
        {
            //Zero stars is an attempt (#716): the leader of a board nobody has cleared, and a player who has only lost on it
            HighScoresRow.Texts row = HighScoresRow.Of(true, Board("Rudi", 9000, 0, 2, myRank: 2, myScore: 4000, myStars: 0));

            Assert.Equal("#1 Rudi, not finished", row.Leader);
            Assert.Equal("You: 2nd of 2, not finished", row.Place);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(3, true)]
        [InlineData(4, true)]
        public void A_level_is_cleared_with_a_star(int stars, bool cleared) => Assert.Equal(cleared, HighScoresRow.Cleared(stars));

        [Fact]
        public void Every_text_a_row_writes_is_set_by_the_rules()
        {
            BoardSummaryBody[] boards =
            {
                null,
                Board("Tereza", 41200, 4, 17),
                Board("Tereza", 41200, 4, 17, myRank: 1, myScore: 41200, myStars: 4),
                Board("Cannonball-Kuba", 355230, 3, 130, myRank: 112, myScore: 123456, myStars: 2),
                Board("Rudi", 9000, 0, 2, myRank: 2, myScore: 4000, myStars: 0),
            };

            foreach (bool answered in new[] { false, true })
                foreach (BoardSummaryBody board in boards)
                {
                    HighScoresRow.Texts row = HighScoresRow.Of(answered, board);

                    foreach (string text in new[] { row.Leader, row.LeaderScore, row.Place, row.PlaceScore })
                        Assert.Null(CopyRules.Fault(text) is { } fault ? $"'{text}': {fault}" : null);
                }
        }
    }
}
