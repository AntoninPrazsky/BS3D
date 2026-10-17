using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Scoring;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>When the game tells the player about the streak</b> (#845): whether a lost streak is announced, how loud, and
    /// when the Swap chip points at the cure; and, since #875, how it is said in words over the aim point
    /// (<see cref="LostCaption"/>). Pure rules over plain values, so the tests hold them without a session (the
    /// session asks them, <c>PlayHud</c> and <c>ProceduralAudio</c> carry them out).
    /// <para>
    /// The owner's playtest of 2026-10-11: losing the multiplier passed almost unnoticed, and he wanted the player to see
    /// it, so as to chain matches, or to reach for the Swap. So a break is an event with a sound (<see cref="BreakIsTold"/>,
    /// <see cref="Severity"/>), and while a streak runs the Swap chip flashes when the round in the bore has nowhere to
    /// score (<see cref="SwapIsTheCure"/>: the owner's answer of the same day, with one condition added to it, below).
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

        // ---- The loss, said where the eye is (#875) ----
        //
        // The owner's playtest of 2026-10-15: he heard the break's falling run repeatedly without knowing what it meant, and
        // never saw the shatter at the top right of a 3840x1600 monitor, because while aiming his eye rests on the middle of
        // the frame. So the break is also SAID in words, over the aim point, on the frame the run starts: "STREAK LOST" and the
        // multiplier that went, in the awards' own type. The corner's shatter stays as the record. The figures are here, pure,
        // so the tests hold them (a few hundred milliseconds, in full on the first frame, clear of the crosshair); PlayHud
        // lays them out and draws them.

        /// <summary>The words said over the aim point when a streak is lost, beside the multiplier that was lost (in the accent, as an award carries its own).</summary>
        public const string LostCaption = "STREAK LOST";

        /// <summary>How long the caption stands, in seconds of the play clock, for a lost ×2 and for a lost cap (<see cref="LostCaptionSeconds"/>).
        /// A few hundred milliseconds: enough to read three words at a glance, short enough never to stand over the next aim.</summary>
        public const float LOST_CAPTION_LOW_SECONDS = 0.7f, LOST_CAPTION_HIGH_SECONDS = 0.9f;

        /// <summary>The last of its time the caption fades out over, in seconds. It is in full from its first frame.</summary>
        public const float LOST_CAPTION_FADE_SECONDS = 0.25f;

        /// <summary>How big the caption settles for the lowest and the highest streak lost, in scores (multiples of the corner score's type, as
        /// an award's size is, <see cref="AwardZoom"/>): under the 1.5 a bare match zooms to, so the news is read at a glance but is never louder than a
        /// reward.</summary>
        public const float LOST_CAPTION_LOW_SIZE = 1.2f, LOST_CAPTION_HIGH_SIZE = 1.45f;

        /// <summary>
        /// How far above the middle of the frame the caption's bottom edge stands, in design units (the 2160-unit height the HUD is
        /// authored in): clear of the crosshair at its most swollen (<see cref="Prazsky.Core.Render.Crosshair.FULL_STRAIN_REACH"/>, 132) by
        /// <see cref="LOST_CAPTION_GAP"/> and more, so the words never cover the aim point and its bars, and off the line the shot has just flown.
        /// </summary>
        public const int LOST_CAPTION_CLEARANCE = 170;

        /// <summary>The least clear space, in design units, between the crosshair at its most swollen and the caption's bottom edge: a visible gap
        /// (12 pixels at 900p), not a touch. What <see cref="LOST_CAPTION_CLEARANCE"/> is held to.</summary>
        public const int LOST_CAPTION_GAP = 30;

        /// <summary>How long the caption stands for a streak of <paramref name="lost"/>, in seconds: longer for a bigger loss, in proportion to its <see cref="Severity"/>.</summary>
        public static float LostCaptionSeconds(int lost) =>
            LOST_CAPTION_LOW_SECONDS + (LOST_CAPTION_HIGH_SECONDS - LOST_CAPTION_LOW_SECONDS) * Severity(lost);

        /// <summary>How big the caption is for a streak of <paramref name="lost"/>, in scores: bigger for a bigger loss.</summary>
        public static float LostCaptionSize(int lost) =>
            LOST_CAPTION_LOW_SIZE + (LOST_CAPTION_HIGH_SIZE - LOST_CAPTION_LOW_SIZE) * Severity(lost);

        /// <summary>
        /// How much of the caption is there, 0 to 1, <paramref name="age"/> seconds into its <paramref name="length"/>: <b>1 from its
        /// first frame</b> (it arrives with the sound, not after it, and a fade in would make it late) until the last
        /// <see cref="LOST_CAPTION_FADE_SECONDS"/>, which it fades out over, to 0 at its end.
        /// </summary>
        public static float LostCaptionPresence(float age, float length)
        {
            float t = Math.Clamp(1f - (length - age) / LOST_CAPTION_FADE_SECONDS, 0f, 1f);
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Whether a round of <paramref name="kind"/> needs a match to score at all. Only an ordinary ball does: a wildcard
        /// takes the colour of whatever it lands beside and a Cutter takes a storey, so neither has "nowhere to score".
        /// </summary>
        public static bool NeedsAMatch(BallKind kind) => kind == BallKind.Normal;

        /// <summary>
        /// Whether the Swap chip flashes (#845): the player has a Swap that would act now (<paramref name="swapCanAct"/>), a
        /// streak above ×1 is running and the round in the bore has nowhere to score, which is the owner's rule of
        /// 2026-10-11; and the round behind it, which the Swap would load, has somewhere.
        /// <para>
        /// The last condition is <b>not the owner's</b>: it is a conservative reading added to his rule,
        /// because a swap that would load a second round with nowhere to score is no cure, and a chip pointing at it would
        /// teach the player to spend the Swap for nothing. "Nowhere to score" is <see cref="MatchReach"/>'s certain "none",
        /// so the chip never asks for the Swap while the bore has a match to be had.
        /// </para>
        /// </summary>
        public static bool SwapIsTheCure(bool swapCanAct, int streak, bool boreCanScore, bool behindCanScore) =>
            swapCanAct && streak > 1 && !boreCanScore && behindCanScore;
    }
}
