using BS3D.Screens;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The tutorial's copy is one thing a step (#930, the owner: "players have a short attention span - they respond to quick,
    /// clear instructions, they do not enjoy deciphering text"). Every step of every card that tells the player to do something,
    /// for each hand, is held to what a glance can take in: one control on the glyph, one clause in the caption and in the
    /// detail, and not many words. The rule is shown to fire on the copy it replaced (the strings are the old cards', as they
    /// stood on main before #930), or "no card asks two things" would be a loop that cannot fail.
    /// <para>
    /// Not held to it: the kind cards (#735), which say what a ball is and are not steps - their copy is checked against the
    /// Help page's by hand ("A card for every kind" in <c>docs/game-feedback.md</c>). The closing card is held to it since
    /// #936: it was a reward that asked nothing (a send-off), and is now advice to do one thing.
    /// </para>
    /// </summary>
    public class TutorialCopyTests
    {
        //A glyph names one control, or one pair of keys on one axis (A and D walk round, W and S step in and out)
        private static readonly string[] KEY_PAIRS = { "ＡＤ", "ＷＳ" };

        //What fits a glance at the size the card is set in: the longest card now is the pad's "Hold the left trigger to look
        //down the barrel", nine words, and the longest detail is "Shoot beside two of the same colour", six
        private const int MAX_CAPTION_WORDS = 9;
        private const int MAX_DETAIL_WORDS = 8;

        //A second clause: a conjunction, a comma or colon or slash or plus, or a dash set between two thoughts
        private static readonly Regex SECOND_CLAUSE = new(@"\b(and|or|then|while|but|plus|also)\b|[,;:+/&]|\s[-–—]\s", RegexOptions.IgnoreCase);

        //A full stop, exclamation or question mark with more after it: a second sentence
        private static readonly Regex SECOND_SENTENCE = new(@"[.!?]\s+\S");

        /// <summary>What is wrong with one step's words, or null if a glance can take it in.</summary>
        private static string Fault(string glyph, string caption, string detail)
        {
            if (!string.IsNullOrEmpty(glyph) && glyph.Length > 1 && !KEY_PAIRS.Contains(glyph))
                return $"the glyph '{glyph}' names several controls";

            foreach ((string text, string what, int words) in new[] { (caption, "caption", MAX_CAPTION_WORDS), (detail, "detail", MAX_DETAIL_WORDS) })
            {
                if (string.IsNullOrEmpty(text)) continue;

                if (SECOND_CLAUSE.Match(text) is { Success: true } clause) return $"the {what} '{text}' joins a second clause at '{clause.Value.Trim()}'";
                if (SECOND_SENTENCE.IsMatch(text)) return $"the {what} '{text}' is two sentences";

                int count = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length;
                if (count > words) return $"the {what} '{text}' is {count} words, more than {words}";
            }

            return null;
        }

        [Fact]
        public void Every_step_that_instructs_asks_one_thing_in_few_words()
        {
            List<string> faults = new();
            int checkedSteps = 0;

            foreach (Tutorial.StepCopy step in Tutorial.Steps())
            {
                if (!step.Instructs) continue;

                checkedSteps++;
                if (Fault(step.Glyph, step.Caption, step.Detail) is { } fault)
                    faults.Add($"{step.Key} step {step.Step}, {(step.Pad ? "pad" : "keyboard")}: {fault}");
            }

            //Two hands of every instructing card at least: a Steps() that came back empty would pass the loop above
            Assert.True(checkedSteps >= 30, $"only {checkedSteps} steps were checked");
            Assert.True(faults.Count == 0, string.Join("\n", faults));
        }

        /// <summary>The cards as they stood before #930, every one of them a fault the rule has to name.</summary>
        [Theory]
        [InlineData("⟶ＡＤ", "Hold the close-up and turn with it", "Line the shot up from inside the close-up")]
        [InlineData("↖⇄", "Hold the left trigger and push the left stick", "Line the shot up from inside the close-up")]
        [InlineData(null, "The cluster is near the line!", "Drop a group to lift it. Under the line you have one second")]
        [InlineData(null, "If the cluster reaches the line, the level is lost", "Keep it light — a heavy cluster hangs low and swings lower")]
        [InlineData("Ｒ", "Turn the next ball into a cutter", "It takes the whole storey it hits, and all that hangs on it")]
        [InlineData("Ｅ", "Swap the next two balls", "One swap a level, for when the queue lets you down")]
        [InlineData("Ｑ", "Lift the glass back one step", "One brake a level, once the glass has come down")]
        [InlineData(null, "Stuck? Fire a ball into empty space — you lose the streak, not the level", null)]
        [InlineData(null, "That’s the basics — you know how to play", "On to the adventure!")]
        [InlineData(null, "Stuck? Throw a ball away", "Fire it into empty space, or onto another colour")]
        public void The_rule_names_each_card_it_replaced(string glyph, string caption, string detail)
        {
            Assert.NotNull(Fault(glyph, caption, detail));
        }

        /// <summary>And it lets the new cards through: a pair of keys, one clause, a short line.</summary>
        [Theory]
        [InlineData("ＡＤ", "Walk the gun round the field", null)]
        [InlineData("⟶", "Hold to look down the barrel", null)]
        [InlineData("⟵", "Click to fire", "Space fires too")]
        [InlineData(null, "Match three of a colour", "Shoot beside two of the same colour")]
        [InlineData(null, "Fire a ball into empty space when stuck", "Wasting a ball is fine")]
        public void The_rule_lets_a_plain_card_through(string glyph, string caption, string detail)
        {
            Assert.Null(Fault(glyph, caption, detail));
        }

        [Fact]
        public void The_combination_is_two_steps_in_each_hand_and_every_other_card_is_one()
        {
            foreach (Tutorial.StepCopy step in Tutorial.Steps())
            {
                bool combination = step.Key == "combine";
                Assert.True(step.Step == 0 || combination, $"{step.Key} has a step {step.Step}");
            }

            foreach (bool pad in new[] { false, true })
            {
                List<Tutorial.StepCopy> steps = Tutorial.Steps().Where(s => s.Key == "combine" && s.Pad == pad).ToList();
                Assert.Equal(2, steps.Count);

                //Each step has a glyph and a caption of its own: the second is not the first said again
                Assert.All(steps, s => Assert.False(string.IsNullOrEmpty(s.Glyph) || string.IsNullOrEmpty(s.Caption)));
                Assert.NotEqual(steps[0].Glyph, steps[1].Glyph);
                Assert.NotEqual(steps[0].Caption, steps[1].Caption);
            }
        }
    }
}
