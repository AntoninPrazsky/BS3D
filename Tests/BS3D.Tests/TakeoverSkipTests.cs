using BS3D.Screens;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <b>The left button's skip</b> (#1106): every fresh press in the picture skips a camera takeover, the one that takes a
    /// free cursor included, and three presses do not: the click that brought the window forward, a button already down
    /// (the press that started the level), and a free pointer's click outside the picture (<see cref="TakeoverSkip"/>).
    /// </summary>
    public class TakeoverSkipTests
    {
        private const int WIDTH = 1920, HEIGHT = 1080;

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
    }
}
