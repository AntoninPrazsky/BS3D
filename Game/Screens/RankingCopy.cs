using System.Globalization;

namespace BS3D.Screens
{
    /// <summary>
    /// What the Top Players page says (#827), strings only and held to the copy rules as the picker's are (<see cref="PickerCopy"/>):
    /// the player's own line under the table, the pager's line and the states the page can be in.
    /// </summary>
    internal static class RankingCopy
    {
        /// <summary>The page's name and its Extras entry (the owner's decision 4).</summary>
        internal const string TITLE = "Top Players";

        /// <summary>
        /// The player's own line under the table: where they stand, with their levels, stars and total beside it, or why
        /// they are not ranked.
        /// </summary>
        internal static string YouLine(int rank, int total, int levels, int stars, long score) =>
            rank <= 0 ? (total > 0 ? "You are not ranked yet: clear a level to the end to join" : "Nobody is ranked yet")
            : (rank == 1 ? "You lead" : $"You: {PickerCopy.Ordinal(rank)} of {N(total)}")
                + $" · {N(levels)} {(levels == 1 ? "level" : "levels")}, {PickerCopy.STAR} {N(stars)}, {ScoreText.Of(score)}";

        /// <summary>"Ranks 11 to 20 of 37", or nothing when one page holds everyone.</summary>
        internal static string PageLine(int offset, int rows, int total) =>
            total > rows ? $"Ranks {N(offset + 1)} to {N(System.Math.Min(offset + rows, total))} of {N(total)}" : " ";

        internal const string LOADING = "Loading the ranking…";
        internal const string OFF = "Online leaderboards are off. Turn on Online scores in Settings to see the players ranked across every level, and where you stand.";
        internal const string NO_SERVER = "No score server to ask: set a nickname in Settings, or this build has no server.";
        internal const string NOT_OFFERED = "The score server does not offer the ranking yet. Each level’s boards are in Select Level.";
        internal const string NO_ANSWER = "The score server did not answer. Try again in a moment.";

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}
