using BS3D.Screens;
using System;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What the campaign's finale says (#880): every string set by the typesetting rules (#806), every message the same three
    /// parts (the thanks, the promise of more, one hint), and every hint pointing at an easter egg the game has
    /// (<c>CampaignFinaleCopy.Egg</c>, the list a hint is checked against) in a riddle that names no key and no action.
    /// </summary>
    public class CampaignFinaleCopyTests
    {
        //Words that would make a hint an instruction: the keys, the verbs of input and the names of the codes. A hint says what
        //to look at, never what to press
        private static readonly string[] INSTRUCTION_WORDS =
        {
            "press", "click", "type", "tap", "hold", "button", "key", "arrow", "keyboard", "mouse", "pad", "konami",
            "up, up", "left", "right", "enter", "space", "escape",
        };

        [Fact]
        public void Every_string_is_set_by_the_rules()
        {
            int strings = 0;

            foreach (string text in CampaignFinaleCopy.Copy())
            {
                Assert.Null(CopyRules.Fault(text) is { } fault ? $"'{text}': {fault}" : null);
                strings++;
            }

            Assert.Equal(1 + 2 * CampaignFinaleCopy.Messages.Length, strings);
        }

        [Fact]
        public void There_are_six_to_eight_messages_and_none_is_empty()
        {
            Assert.InRange(CampaignFinaleCopy.Messages.Length, 6, 8);

            foreach (CampaignFinaleCopy.Message message in CampaignFinaleCopy.Messages)
            {
                Assert.False(string.IsNullOrWhiteSpace(message.Thanks));
                Assert.False(string.IsNullOrWhiteSpace(message.Hint));
            }
        }

        [Fact]
        public void Every_message_says_thanks_and_that_more_is_coming()
        {
            foreach (CampaignFinaleCopy.Message message in CampaignFinaleCopy.Messages)
            {
                string thanks = message.Thanks.ToLowerInvariant();

                Assert.Contains("thank", thanks);
                Assert.Contains("level", thanks);
                Assert.True(thanks.Contains("feature") || thanks.Contains("trick"), $"no promise of more features: '{message.Thanks}'");
            }
        }

        [Fact]
        public void Every_message_carries_exactly_one_hint_and_the_thanks_carries_none()
        {
            //The hint is its own field, so "one" is by construction; what a mistake would break is a second riddle written into
            //the thanks, which names an egg's own subject (the gun, the title, a code, the credits) and is a second hint
            string[] subjects = { "gun", "barrel", "title", "calendar", "credits", "wormhole", "sky", "password", "front door" };

            foreach (CampaignFinaleCopy.Message message in CampaignFinaleCopy.Messages)
                foreach (string subject in subjects)
                    Assert.DoesNotContain(subject, message.Thanks.ToLowerInvariant());
        }

        [Fact]
        public void Every_hint_points_at_an_egg_in_the_list_and_every_egg_has_a_hint()
        {
            foreach (CampaignFinaleCopy.Message message in CampaignFinaleCopy.Messages)
                Assert.True(Enum.IsDefined(message.Egg), $"'{message.Hint}' names no egg");

            foreach (CampaignFinaleCopy.Egg egg in Enum.GetValues<CampaignFinaleCopy.Egg>())
                Assert.Contains(CampaignFinaleCopy.Messages, m => m.Egg == egg);
        }

        [Fact]
        public void No_hint_names_a_key_or_an_action()
        {
            foreach (CampaignFinaleCopy.Message message in CampaignFinaleCopy.Messages)
            {
                //Whole words: "keep an eye" and "pad" in "padlock" are not instructions
                string[] words = message.Hint.ToLowerInvariant()
                    .Split(new[] { ' ', ',', '.', ';', ':', '—', '’', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string forbidden in INSTRUCTION_WORDS.Where(w => !w.Contains(' ')))
                    Assert.DoesNotContain(forbidden, words);

                Assert.DoesNotContain("up, up", message.Hint.ToLowerInvariant());
            }
        }

        [Fact]
        public void A_message_is_held_long_enough_to_read_and_never_longer_than_the_bound()
        {
            foreach (CampaignFinaleCopy.Message message in CampaignFinaleCopy.Messages)
            {
                float hold = CampaignFinaleCopy.HoldSeconds(message);

                Assert.InRange(hold, 8f, CampaignFinaleCopy.MAX_HOLD);

                //No faster than 5 words a second: the pace a reader keeps up with without being hurried
                Assert.True(CampaignFinaleCopy.Words(message) / hold <= 5f, $"{CampaignFinaleCopy.Words(message)} words in {hold} s");
            }
        }

        [Fact]
        public void The_headline_is_the_result_pages_own_verdict()
        {
            Assert.Equal("CAMPAIGN COMPLETE", CampaignFinaleCopy.TITLE);
        }
    }
}
