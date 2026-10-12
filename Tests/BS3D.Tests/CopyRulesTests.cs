using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The copy's characters (#806, "Typesetting" in <c>docs/game-shell/menu-look.md</c>): every string a tutorial card
    /// draws is held to them, and the check itself is shown to fire on each fault it names. The cards failed it before
    /// #806 (seen): "That's the basics", "Stone can't", "your shot's colour" and "passes it on - match it early".
    /// </summary>
    public class CopyRulesTests
    {
        [Fact]
        public void Every_tutorial_card_is_set_by_the_rules()
        {
            int checkedStrings = 0;

            foreach (string text in Tutorial.Copy())
            {
                Assert.Null(CopyRules.Fault(text) is { } fault ? $"'{text}': {fault}" : null);
                checkedStrings++;
            }

            //Every lesson has at least a caption: a copy accessor that came back empty would pass the loop above
            Assert.True(checkedStrings > 30, $"only {checkedStrings} strings came back from Tutorial.Copy");
        }

        [Theory]
        [InlineData("That's the basics")]
        [InlineData("The \"rainbow\" ball")]
        [InlineData("It passes it on - match it early")]
        [InlineData("Asking the server...")]
        [InlineData("drops them—severing everything")]
        [InlineData("Shot 1, at ×1:  5×10")]
        public void Each_fault_is_caught(string text)
        {
            Assert.NotNull(CopyRules.Fault(text));
        }

        [Theory]
        [InlineData("That’s the basics — you know how to play")]
        [InlineData("3 to 16 letters, digits, spaces, _ or -. Enter confirms.")]
        [InlineData("Glass — clear, with no colour of its own yet…")]
        [InlineData("MonoGame\u00A03.8.5 —\u00A0the engine")]
        [InlineData("")]
        public void Copy_set_by_the_rules_passes(string text)
        {
            Assert.Null(CopyRules.Fault(text));
        }
    }
}
