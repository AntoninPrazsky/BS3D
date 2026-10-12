using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Scoring;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>When the game tells the player about the streak</b> (#845): whether a lost streak is announced, how loud, and
    /// when the Swap chip points at the cure. Pure rules over plain values, so the tests hold them without a session (the
    /// session asks them, <c>PlayHud</c> and <c>ProceduralAudio</c> carry them out).
    /// <para>
    /// The owner's playtest of 2026-10-11: losing the multiplier passed almost unnoticed, and he wanted the player to see
    /// it, so as to chain matches, or to reach for the Swap. So a break is an event with a sound (<see cref="BreakIsTold"/>,
    /// <see cref="Severity"/>), and while a streak runs the Swap chip flashes when the round in the bore has nowhere to
    /// score and the Swap would load one that has (<see cref="SwapIsTheCure"/>, the owner's answer of the same day).
    /// </para>
    /// </summary>
    internal static class StreakCues
    {
        /// <summary>
        /// Whether losing a streak of <paramref name="lost"/> is told to the player (the badge breaks and the falling run
        /// sounds): only a streak above ×1, and <b>never once the level is decided or its last shot is spent</b>. The
        /// issue's own rule: a break must not play over the result, and a lost streak with no shot left to win it back
        /// is not news, the level's ending is. A break that is not told simply ends: the badge goes.
        /// </summary>
        public static bool BreakIsTold(int lost, bool levelDecided, bool outOfShots) =>
            lost > 1 && !levelDecided && !outOfShots;

        /// <summary>
        /// How much a break lost, 0 to 1 over the multipliers the rule allows: 0.5 for ×3 and 1 for the cap (×5). Every
        /// figure of the break scales by it, so a lost ×5 shakes harder, throws more pieces, lasts longer and sounds louder
        /// and longer than a lost ×3 (the issue's "x5 louder and longer than x3").
        /// </summary>
        public static float Severity(int lost) =>
            Math.Clamp((lost - 1f) / MathF.Max(1f, ScoreKeeper.MaxMultiplier - 1f), 0f, 1f);

        /// <summary>
        /// Whether a round of <paramref name="kind"/> needs a match to score at all. Only an ordinary ball does: a wildcard
        /// takes the colour of whatever it lands beside and a Cutter takes a storey, so neither has "nowhere to score".
        /// </summary>
        public static bool NeedsAMatch(BallKind kind) => kind == BallKind.Normal;

        /// <summary>
        /// Whether the Swap chip flashes (#845, the owner's rule of 2026-10-11): the player has a Swap that would act now
        /// (<paramref name="swapCanAct"/>), a streak above ×1 is running, the round in the bore has nowhere to score, and
        /// the round behind it, which the Swap would load, has somewhere.
        /// <para>
        /// The last condition is the conservative half of the rule as the owner gave it: a swap that would load a second
        /// round with nowhere to score is no cure, and a chip pointing at it would teach the player to spend the Swap for
        /// nothing. "Nowhere to score" is <see cref="MatchReach"/>'s certain "none", so the chip never asks for the Swap
        /// while the bore has a match to be had.
        /// </para>
        /// </summary>
        public static bool SwapIsTheCure(bool swapCanAct, int streak, bool boreCanScore, bool behindCanScore) =>
            swapCanAct && streak > 1 && !boreCanScore && behindCanScore;
    }
}
