using BS3D.Screens;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What the result page says (#806): every string <c>LevelResult</c> hands it, on every ending, is held to the
    /// typesetting rules (<c>CopyRules</c>), and the unlock note — prose set through <c>SpaceWrap</c> in a plate cut to the
    /// menu column — never parts a price from its star or "Level" from its number. Before #806 the note had plain spaces
    /// there, and set at a narrow measure it broke "unlocks at 196" from "★" (seen in the tie test below).
    /// </summary>
    public class ResultCopyTests
    {
        private static LevelResult Result(bool cleared = true, int stars = 2, bool campaign = false, bool block = false,
            bool hasNext = true, bool nextUnlocked = true, bool beyondReach = false, bool canSkip = false, bool budget = true,
            string frontier = "Pinwheel") =>
            new(cleared: cleared, failureText: cleared ? null : "The cluster reached the line.", stars: cleared ? stars : 0,
                newBest: cleared, levelBalls: 120, levelName: "Star", levelNumber: 13,
                hasNextLevel: hasNext, nextLevelUnlocked: nextUnlocked, nextLevelMinStars: 196, totalStars: 194,
                nextLevelBeyondReach: beyondReach, frontierLevelNumber: 5, frontierLevelName: frontier,
                canSkip: canSkip, nextLevelName: "Elephant", campaignComplete: campaign,
                blockComplete: block, blockName: "The Gallery", blockNumber: 2, blockCount: 14,
                score: 2430, matchedBalls: 96, orphanedBalls: 24, streakBonus: 640,
                hadBudget: budget, unusedShotsAwarded: 7, completionBonusAwarded: 350);

        private static IEnumerable<LevelResult> Endings()
        {
            foreach (int stars in new[] { 1, 2, 3, 4 })
                yield return Result(stars: stars);

            yield return Result(cleared: false, canSkip: true);
            yield return Result(cleared: false, canSkip: false);
            yield return Result(campaign: true);
            yield return Result(block: true);
            yield return Result(nextUnlocked: false);
            yield return Result(nextUnlocked: false, beyondReach: true);
            yield return Result(nextUnlocked: false, beyondReach: true, frontier: null);
            yield return Result(hasNext: false);
            yield return Result(budget: false);
        }

        private static IEnumerable<string> Strings(LevelResult r)
        {
            yield return r.Heading;
            yield return r.MilestoneLine;
            yield return r.LevelLine;
            yield return r.FailureText;
            yield return r.BareScoreLine;
            yield return r.NextStarNote;
            yield return r.MatchedDetail;
            yield return r.OrphanedDetail;
            yield return r.UnusedDetail;
            yield return r.NextLevelLabel;
            yield return r.SkipLabel;
            yield return r.UnlockNote;
            yield return LevelResult.NEW_BEST;
            yield return LevelResult.SKIP_NOTE;
        }

        [Fact]
        public void Every_string_the_page_shows_is_set_by_the_rules()
        {
            int checkedStrings = 0;

            foreach (LevelResult ending in Endings())
                foreach (string text in Strings(ending))
                {
                    //The note's own line break is a break, not a character
                    foreach (string line in text.Split('\n'))
                        Assert.Null(CopyRules.Fault(line) is { } fault ? $"'{line}': {fault}" : null);
                    checkedStrings++;
                }

            Assert.True(checkedStrings > 100, $"only {checkedStrings} strings were checked");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void The_unlock_note_keeps_a_price_with_its_star_and_a_level_with_its_number(bool beyondReach)
        {
            string note = Result(nextUnlocked: false, beyondReach: beyondReach).UnlockNote;

            //Every measure from the narrowest the words allow to one wider than the note: no line ends on "at 196" or on
            //"Level", and none starts with the star or the number
            for (int width = 6; width <= 40; width++)
                foreach (string line in SpaceWrap.Wrap(note, width, l => l.Length).Split('\n'))
                {
                    Assert.False(line.StartsWith('★'), $"'{line}' starts with the star at a measure of {width}");
                    Assert.False(line.EndsWith("Level"), $"'{line}' ends on 'Level' at a measure of {width}");
                    Assert.False(line.StartsWith("5 "), $"'{line}' starts with the level's number at a measure of {width}");
                }
        }

        [Fact]
        public void The_breakdown_details_are_count_times_worth() =>
            Assert.Equal(new[] { "96 × 10", "24 × 20", "7 × 50", "—" },
                new[] { Result().MatchedDetail, Result().OrphanedDetail, Result().UnusedDetail, Result(budget: false).UnusedDetail });
    }
}
