using System;

namespace Prazsky.BS3D.Scoring
{
    /// <summary>
    /// <b>How a level's par time is made</b> (#1045, the owner's decision of 2026-11-17): <see cref="BASE_SECONDS"/> and
    /// <see cref="SECONDS_A_SHOT"/> for every shot a clear takes, rounded up to <see cref="ROUND_TO_SECONDS"/>. The par is the
    /// seconds the time bonus is measured against (<see cref="ScoreKeeper.TimeBonusAt"/>), written into every entry of the
    /// campaign by <c>Tools/LevelGen</c>, whose casual player supplies the shots (its <c>ParTime</c> says why that player).
    /// <para>
    /// The figures are the ones the owner's own clears were fitted to: about 10 s and 2 s a shot on the 23 levels his save
    /// cleared every time, against the casual player's shots at a ratio of 1.1; the half second more a shot is a margin for
    /// a player slower than the author. A par of 20 s to 115 s across the 140 levels, 40 s in the middle.
    /// </para>
    /// </summary>
    public static class ParRule
    {
        /// <summary>The seconds a level takes before its first shot is counted: reading the cluster, turning the gun.</summary>
        public const float BASE_SECONDS = 10f;

        /// <summary>The seconds every shot costs: aim, fire, watch it land.</summary>
        public const float SECONDS_A_SHOT = 2.5f;

        /// <summary>A par is a round figure, rounded up, so the rule never asks for less than it states.</summary>
        public const int ROUND_TO_SECONDS = 5;

        /// <summary>The par of a level a clear takes <paramref name="shots"/> shots of: <see cref="BASE_SECONDS"/> +
        /// <see cref="SECONDS_A_SHOT"/> × shots, rounded up to <see cref="ROUND_TO_SECONDS"/>.</summary>
        public static int FromShots(int shots)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(shots);
            return RoundUp(BASE_SECONDS + SECONDS_A_SHOT * shots);
        }

        /// <summary><paramref name="seconds"/> rounded up to a whole <see cref="ROUND_TO_SECONDS"/>, never under one of them.</summary>
        public static int RoundUp(float seconds) =>
            ROUND_TO_SECONDS * Math.Max(1, (int)Math.Ceiling(seconds / ROUND_TO_SECONDS - 1e-6f));
    }
}
