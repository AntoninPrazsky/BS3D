using BS3D.Screens;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <b>The left button's skip</b> (#1106): every fresh press in the picture skips a camera takeover, the one that takes a
    /// free cursor included, and three presses do not: the click that brought the window forward, a button already down
    /// (the press that started the level), and a free pointer's click outside the picture (<see cref="TakeoverSkip"/>).
    /// And the screen asks that rule alone: <c>GameplayScreen</c> needs the graphics device and cannot be compiled into this
    /// project, so <c>UpdateAim</c>'s takeover branch is held on its source, and the rule is also run over the condition
    /// that branch had before #1106 (the aim's baseline ANDed in, which dropped a free cursor's first click), so its failing
    /// branch is seen to fire (BestPractices section 10).
    /// </summary>
    public class TakeoverSkipTests
    {
        private const int WIDTH = 1920, HEIGHT = 1080;

        private const string UPDATE_AIM = "private void UpdateAim(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)";

        //An if that skips a takeover, its whole condition captured with its parentheses balanced, over the branch's code
        //joined into one line
        private static readonly Regex SkipIf = new(
            @"\bif \((?<cond>(?:[^()]|(?<o>\()|(?<-o>\)))*(?(o)(?!)))\)\s*\{?\s*SkipCameraTakeover\(\);", RegexOptions.Compiled);

        //A condition that is one call of the rule and nothing else
        private static readonly Regex WholeRule = new(
            @"^TakeoverSkip\.MouseSkips\((?:[^()]|(?<o>\()|(?<-o>\)))*(?(o)(?!))\)$", RegexOptions.Compiled);

        //What makes a skip the mouse's: it reads this frame's or the previous frame's mouse
        private static readonly Regex ReadsMouse = new(@"\bmouse\b|\b_previousMouse\b", RegexOptions.Compiled);

        private static MouseState Mouse(int x, int y, bool down) =>
            new(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released);

        private static readonly MouseState Up = Mouse(WIDTH / 2, HEIGHT / 2, down: false);

        [Theory]
        [InlineData(true, WIDTH / 2, HEIGHT / 2)]
        //A free cursor's first click in the picture: it takes the cursor, and it skips as well
        [InlineData(false, WIDTH / 2, HEIGHT / 2)]
        [InlineData(false, 0, 0)]
        [InlineData(false, WIDTH - 1, HEIGHT - 1)]
        public void A_fresh_press_in_the_picture_skips(bool cursorCaptured, int x, int y) =>
            Assert.True(TakeoverSkip.MouseSkips(true, cursorCaptured, Mouse(x, y, down: true), Up, WIDTH, HEIGHT));

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void The_click_that_brought_the_window_forward_does_not_skip(bool cursorCaptured) =>
            Assert.False(TakeoverSkip.MouseSkips(edgeInputAllowed: false, cursorCaptured, Mouse(WIDTH / 2, HEIGHT / 2, down: true), Up,
                WIDTH, HEIGHT));

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void A_button_already_down_does_not_skip(bool cursorCaptured)
        {
            //The press that started the level: the screen's arrival takes the previous frame's state with it still down
            MouseState held = Mouse(WIDTH / 2, HEIGHT / 2, down: true);
            Assert.False(TakeoverSkip.MouseSkips(true, cursorCaptured, held, held, WIDTH, HEIGHT));

            //And a button let go skips nothing either
            Assert.False(TakeoverSkip.MouseSkips(true, cursorCaptured, Up, held, WIDTH, HEIGHT));
        }

        [Theory]
        [InlineData(200, -12)]
        [InlineData(-300, 500)]
        [InlineData(WIDTH, 500)]
        [InlineData(500, HEIGHT)]
        public void A_free_pointers_click_outside_the_picture_does_not_skip(int x, int y) =>
            Assert.False(TakeoverSkip.MouseSkips(true, cursorCaptured: false, Mouse(x, y, down: true), Up, WIDTH, HEIGHT));

        [Fact]
        public void The_screen_skips_on_the_rule_alone()
        {
            string[] method = ChapterIntroSkipTests.MethodBody(ChapterIntroSkipTests.Source("GameplayScreen.Input.cs"), UPDATE_AIM);

            string fault = MouseSkipFault(method);
            Assert.True(fault == null, $"UpdateAim's takeover branch does not skip on the rule alone: {fault}");

            //The condition the branch had before #1106 fails the rule
            Assert.NotNull(MouseSkipFault(WithMouseSkip(method,
                "                if (edgeInputAllowed && _mouseAim.Initialized",
                "                    && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)",
                "                    SkipCameraTakeover();")));

            //As does that gate put back beside the rule, inside one of its arguments, around it, or as a way out ahead of it
            Assert.NotNull(MouseSkipFault(WithMouseSkip(method,
                "                if (_mouseAim.Initialized && TakeoverSkip.MouseSkips(edgeInputAllowed, _cursorCaptured, mouse, _previousMouse,",
                "                    GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height))",
                "                    SkipCameraTakeover();")));
            Assert.NotNull(MouseSkipFault(WithMouseSkip(method,
                "                if (TakeoverSkip.MouseSkips(edgeInputAllowed && _mouseAim.Initialized, _cursorCaptured, mouse, _previousMouse,",
                "                    GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height))",
                "                    SkipCameraTakeover();")));
            Assert.NotNull(MouseSkipFault(WithMouseSkip(method,
                "                if (_mouseAim.Initialized)",
                "                    if (TakeoverSkip.MouseSkips(edgeInputAllowed, _cursorCaptured, mouse, _previousMouse,",
                "                        GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height))",
                "                        SkipCameraTakeover();")));
            Assert.NotNull(MouseSkipFault(WithMouseSkip(method,
                "                if (!_mouseAim.Initialized) return;",
                "                if (TakeoverSkip.MouseSkips(edgeInputAllowed, _cursorCaptured, mouse, _previousMouse,",
                "                    GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height))",
                "                    SkipCameraTakeover();")));

            //And a branch with no skip on the left button at all
            Assert.NotNull(MouseSkipFault(WithMouseSkip(method)));
        }

        /// <summary>
        /// Why <paramref name="method"/> (UpdateAim's lines) does not skip on the rule alone, or null when it does: inside the
        /// <c>if (CameraTakeoverEngaged)</c> branch exactly one if that reads the mouse skips the takeover, its whole condition
        /// is one call of <see cref="TakeoverSkip.MouseSkips"/> with no condition in its arguments, it stands at the branch's
        /// own depth and no return comes ahead of it.
        /// </summary>
        private static string MouseSkipFault(string[] method)
        {
            string[] code = method.Select(line => ChapterIntroSkipTests.CodeOf(line).TrimEnd()).ToArray();

            int open = Array.FindIndex(code, line => line.Trim() == "if (CameraTakeoverEngaged)");
            if (open < 0 || code[open + 1].Trim() != "{") return "UpdateAim has no takeover branch";

            string indent = new(code[open].TakeWhile(char.IsWhiteSpace).ToArray());
            int close = Array.FindIndex(code, open + 1, line => line == indent + "}");
            if (close < 0) return "the takeover branch has no end";

            string[] branch = code[(open + 2)..close];
            Match[] skips = SkipIf.Matches(string.Join(" ", branch.Select(line => line.Trim())))
                .Where(skip => ReadsMouse.IsMatch(skip.Groups["cond"].Value)).ToArray();
            if (skips.Length != 1) return $"{skips.Length} ifs in the branch skip on the mouse";

            string condition = skips[0].Groups["cond"].Value.Trim();
            if (!WholeRule.IsMatch(condition)) return $"the click's skip asks something beside the rule: '{condition}'";
            if (Regex.IsMatch(condition, @"&&|\|\||\?|_mouseAim")) return $"an argument of the rule carries a condition: '{condition}'";

            int at = Array.FindIndex(branch, line => line.Contains("TakeoverSkip.MouseSkips(", StringComparison.Ordinal));
            if (at < 0 || !branch[at].StartsWith(indent + "    if (TakeoverSkip.MouseSkips(", StringComparison.Ordinal))
                return "the click's skip sits inside another condition";

            if (branch.Take(at).Any(line => Regex.IsMatch(line, @"\breturn\b"))) return "the branch can return before the click is asked";

            return null;
        }

        /// <summary>
        /// <paramref name="method"/> with the mouse's skip (from the line that asks the rule to its <c>SkipCameraTakeover();</c>)
        /// replaced by <paramref name="with"/>.
        /// </summary>
        private static string[] WithMouseSkip(string[] method, params string[] with)
        {
            int at = Array.FindIndex(method, line => line.Contains("TakeoverSkip.MouseSkips(", StringComparison.Ordinal));
            Assert.True(at >= 0, "UpdateAim does not ask TakeoverSkip.MouseSkips");

            int skip = Array.FindIndex(method, at, line => line.Trim() == "SkipCameraTakeover();");
            Assert.True(skip > at, "the mouse's skip has no SkipCameraTakeover() under it");

            return method[..at].Concat(with).Concat(method[(skip + 1)..]).ToArray();
        }
    }
}
