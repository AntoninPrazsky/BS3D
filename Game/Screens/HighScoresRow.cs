using BS3D.Online;

namespace BS3D.Screens
{
    /// <summary>
    /// What one row of the High Scores page says of its level (#898), and which tint the row rests in: strings and a rule
    /// only, written here and not inside the page's writing pass so a test can hold them without a device.
    /// <para>
    /// <b>Every level says its real board, whether or not the player may play it yet.</b> The page used to write "Locked"
    /// over a level ahead of the player and blank its scores, on #266's argument that a top score is a level's shape in
    /// numbers; the owner ruled on 2026-10-16 that the complete table of every level of every chapter is to be seen, and
    /// that what a row says of the player's own progress is its <i>tint</i> (<see cref="Cleared"/>) instead. So nothing
    /// here takes a lock: there is no way to ask for a row's text as a locked level's.
    /// </para>
    /// </summary>
    internal static class HighScoresRow
    {
        /// <summary>What a level's leader column says when the service answered and no board is on it: nobody has cleared it.</summary>
        internal const string NO_CLEARS = "No clears yet";

        /// <summary>
        /// <b>Whether the player has cleared the level, which is what decides a row's tint</b> - its best on the save has at
        /// least one star (a clear always earns one, <c>StarRating.Rate</c>'s floor). <b>A skipped level is not cleared</b>
        /// here, though <c>LevelFinished</c> (what moves the campaign's frontier on) counts it: the tint says whether the
        /// player has a place of their own on the level, and a skip has none. The level picker's own rule is the star count
        /// as well.
        /// </summary>
        internal static bool Cleared(int stars) => stars > 0;

        /// <summary>
        /// The four texts of a row, from the board the summary holds for the level (null when the service named none). Empty
        /// scores stay empty strings so the page lays out no figure beside them; the page sets the figures in tabular digits.
        /// </summary>
        /// <param name="answered">Whether the summary of this period has come: before it, and after a failure, a row says
        /// nothing in its leader column, and the status line says why.</param>
        internal static Texts Of(bool answered, BoardSummaryBody board)
        {
            if (!answered) return new Texts(string.Empty, string.Empty, string.Empty, string.Empty);

            if (board == null) return new Texts(NO_CLEARS, string.Empty, string.Empty, string.Empty);

            //An unfinished attempt (#716) is zero stars, which no clear has: the service ranks every clear above every
            //attempt, so a "#1" that is one says nobody has cleared the level yet
            string leader = PickerCopy.Leader(board.Top.Name, board.Top.Stars == 0);
            string leaderScore = ScoreText.Of(board.Top.Score);

            //"You" stays empty on a level the player has no place on - every level they have not played, whichever way it is tinted
            if (board.Me == null || board.Me.Rank <= 0) return new Texts(leader, leaderScore, string.Empty, string.Empty);

            return new Texts(leader, leaderScore,
                PickerCopy.Place(board.Me.Rank, board.Total, board.Me.Stars == 0), ScoreText.Of(board.Me.Score));
        }

        /// <summary>A row's texts: the leader and their score, the player's place and theirs.</summary>
        internal readonly record struct Texts(string Leader, string LeaderScore, string Place, string PlaceScore)
        {
            /// <summary>Whether a leader's score is written beside the leader.</summary>
            internal bool HasLeaderScore => !string.IsNullOrEmpty(LeaderScore);

            /// <summary>Whether a score of the player's own is written beside their place.</summary>
            internal bool HasPlaceScore => !string.IsNullOrEmpty(PlaceScore);
        }
    }
}
