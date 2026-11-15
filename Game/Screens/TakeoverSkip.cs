using Microsoft.Xna.Framework.Input;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>Which click skips a camera takeover</b> (#1106, the owner: the intro is skippable always). Space, the pad's A or
    /// right trigger and a touch skip on their own press edges; this is the left button's rule, kept apart from the screen so
    /// the tests can hold it without a device. Every press the player makes once the takeover's own lockout has passed
    /// skips it; three presses are refused, each by one guard:
    /// <list type="bullet">
    /// <item><description><b>The click that brought the window forward</b>: <c>BS3DGame.EdgeInputAllowed</c> is false for the
    /// frame focus returns on.</description></item>
    /// <item><description><b>A button already down</b>, the press that started the level among them: only a press edge
    /// against the previous frame counts, and the screen's arrival takes that snapshot with the menu's press still in it
    /// (<c>GameplayScreen.CoveredChanged</c>).</description></item>
    /// <item><description><b>A click outside the picture while the cursor is free</b>: a <see cref="MouseState"/> reads a
    /// press over the title bar or another window quite happily, and those are the desktop's. A taken cursor is the
    /// game's wherever it stands.</description></item>
    /// </list>
    /// No aim is asked for. A shot needs the aim's baseline (<c>MouseAim.Initialized</c>), which a free cursor never has,
    /// and the skip used to ask for it too: with the cursor free, the first click in the picture took the cursor and skipped
    /// nothing. That click now does both.
    /// </summary>
    internal static class TakeoverSkip
    {
        /// <summary>Whether this frame's left button skips the takeover running now.</summary>
        internal static bool MouseSkips(bool edgeInputAllowed, bool cursorCaptured, MouseState mouse, MouseState previous,
            int viewportWidth, int viewportHeight) =>
            edgeInputAllowed
            && mouse.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Released
            && (cursorCaptured || InPicture(mouse, viewportWidth, viewportHeight));

        /// <summary>Whether the pointer stands inside the viewport, the bounds the capture click is held to.</summary>
        internal static bool InPicture(MouseState mouse, int viewportWidth, int viewportHeight) =>
            mouse.X >= 0 && mouse.X < viewportWidth && mouse.Y >= 0 && mouse.Y < viewportHeight;
    }
}
