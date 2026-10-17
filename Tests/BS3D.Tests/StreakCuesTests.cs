using BS3D.Screens;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Scoring;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #845: when the game tells the player about the streak (<see cref="StreakCues"/>, compiled in from the Game). A break
    /// is told above ×1 and never once the level is decided or its last shot spent (the issue: never over the result); it
    /// scales with what was lost; and the Swap chip points at the cure only in the one state the owner named, narrowed by
    /// the conservative condition added to it (the round behind can score).
    /// </summary>
    public class StreakCuesTests
    {
        [Theory]
        [InlineData(3, false, false, true)]
        [InlineData(5, false, false, true)]
        [InlineData(1, false, false, false)]      //no streak, nothing lost
        [InlineData(5, true, false, false)]       //the level is decided: never over the result
        [InlineData(5, false, true, false)]       //the last shot is spent: the loss is the level's ending, not the streak
        [InlineData(3, true, true, false)]
        public void A_break_is_told_only_while_the_level_goes_on(int lost, bool levelDecided, bool outOfShots, bool told)
        {
            Assert.Equal(told, StreakCues.BreakIsTold(lost, levelDecided, outOfShots));
        }

        [Fact]
        public void A_lost_cap_is_more_severe_than_a_lost_three_and_both_are_in_range()
        {
            Assert.Equal(1f, StreakCues.Severity(ScoreKeeper.MaxMultiplier));
            Assert.Equal(0f, StreakCues.Severity(1));
            Assert.True(StreakCues.Severity(5) > StreakCues.Severity(3));
            Assert.InRange(StreakCues.Severity(3), 0.01f, 0.99f);
            Assert.Equal(1f, StreakCues.Severity(99));
        }

        /// <summary>
        /// #875: the caption over the aim point stands a few hundred milliseconds - long enough to read three words, short
        /// enough never to stand over the next aim - and a bigger loss stands longer and larger, but never as large as the
        /// smallest award zooms: the news must not be louder than a reward.
        /// </summary>
        [Fact]
        public void The_lost_caption_is_a_few_hundred_milliseconds_and_grows_with_the_loss()
        {
            for (int lost = 2; lost <= ScoreKeeper.MaxMultiplier; lost++)
            {
                Assert.InRange(StreakCues.LostCaptionSeconds(lost), 0.4f, 1.0f);
                Assert.True(StreakCues.LostCaptionSize(lost) < AwardZoom.PEAK_SMALL, $"x{lost} is louder than a bare match's award");
            }

            Assert.True(StreakCues.LostCaptionSeconds(ScoreKeeper.MaxMultiplier) > StreakCues.LostCaptionSeconds(2));
            Assert.True(StreakCues.LostCaptionSize(ScoreKeeper.MaxMultiplier) > StreakCues.LostCaptionSize(2));

            //Its fade is part of that time, not on top of it, and shorter than the whole
            Assert.True(StreakCues.LOST_CAPTION_FADE_SECONDS < StreakCues.LostCaptionSeconds(2));
        }

        /// <summary>
        /// #875: the words arrive with the sound, not after it. The sound's first note starts <c>LEAD_SECONDS</c> after the
        /// call that starts both, so the caption is in full on its very first frame (a fade in would put it behind the sound
        /// it explains) and only fades over its last <c>LOST_CAPTION_FADE_SECONDS</c>, to nothing at its end.
        /// </summary>
        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        public void The_lost_caption_is_in_full_from_its_first_frame_and_fades_only_at_its_end(int lost)
        {
            float length = StreakCues.LostCaptionSeconds(lost);

            Assert.Equal(1f, StreakCues.LostCaptionPresence(0f, length));
            Assert.Equal(1f, StreakCues.LostCaptionPresence(length - StreakCues.LOST_CAPTION_FADE_SECONDS, length));
            Assert.Equal(0f, StreakCues.LostCaptionPresence(length, length));

            //Never rising again once it has begun to fade
            float last = 1f;
            for (float age = 0f; age <= length; age += length / 200f)
            {
                float presence = StreakCues.LostCaptionPresence(age, length);
                Assert.InRange(presence, 0f, 1f);
                Assert.True(presence <= last + 1e-6f, $"the caption brightens again at {age:F3} s");
                last = presence;
            }

            //And a newer award cutting it short (PlayHud.AddAward: length = age + fade) leaves it exactly as bright as it was
            Assert.Equal(1f, StreakCues.LostCaptionPresence(0.3f, 0.3f + StreakCues.LOST_CAPTION_FADE_SECONDS));
        }

        /// <summary>
        /// #875: its bottom edge stands above where the crosshair reaches at its most swollen (full strain at the throb's peak),
        /// so the words are never over the aim point or its bars; the crosshair's own figure, so a retuned mark cannot walk into them.
        /// </summary>
        [Fact]
        public void The_lost_caption_stands_clear_of_the_crosshair_at_its_most_swollen()
        {
            float reach = Prazsky.Core.Render.Crosshair.FULL_STRAIN_REACH;

            Assert.True(StreakCues.LOST_CAPTION_CLEARANCE >= reach + StreakCues.LOST_CAPTION_GAP,
                $"{StreakCues.LOST_CAPTION_CLEARANCE} design units above the middle leaves under {StreakCues.LOST_CAPTION_GAP} units to the crosshair's {reach}");
        }

        [Fact]
        public void The_lost_caption_says_the_streak_is_lost_in_words()
        {
            Assert.Equal("STREAK LOST", StreakCues.LostCaption);
        }

        [Fact]
        public void Only_an_ordinary_round_needs_a_match()
        {
            Assert.True(StreakCues.NeedsAMatch(BallKind.Normal));
            Assert.False(StreakCues.NeedsAMatch(BallKind.Wildcard));
            Assert.False(StreakCues.NeedsAMatch(BallKind.Cutter));
        }

        /// <summary>
        /// The whole truth table: one row flashes, every other is dark. The owner's rule of 2026-10-11 is a Swap, a streak
        /// and a dead bore; the live round behind is the conservative condition added to it (<see cref="StreakCues.SwapIsTheCure"/>).
        /// </summary>
        [Theory]
        [InlineData(true, 3, false, true, true)]      //the rule: a Swap to spend, a streak, a dead bore, a live round behind
        [InlineData(true, 5, false, true, true)]
        [InlineData(false, 3, false, true, false)]    //no Swap that would act (spent, mid-takeover, level decided)
        [InlineData(true, 1, false, true, false)]     //no streak running: nothing to keep
        [InlineData(true, 3, true, true, false)]      //the bore can score: fire it
        [InlineData(true, 3, false, false, false)]    //the round behind has nowhere either: the Swap is no cure
        [InlineData(true, 3, true, false, false)]
        public void The_swap_flashes_only_when_it_is_the_cure(bool swapCanAct, int streak, bool boreCanScore, bool behindCanScore,
            bool flashes)
        {
            Assert.Equal(flashes, StreakCues.SwapIsTheCure(swapCanAct, streak, boreCanScore, behindCanScore));
        }
    }
}
