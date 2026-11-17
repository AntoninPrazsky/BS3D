using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>What the hands ask of the precise-aim lean</b>, a frame at a time: the toggle and the press that flips it (#1050,
    /// #1054), or the hold Settings' <i>Precise aim: Hold</i> makes of the right button and the left trigger (#520). Kept apart
    /// from the screen so the tests can hold it without a device. <c>GameplayScreen.UpdateAim</c> reads it once a frame, and the
    /// frame's answer (<c>_adsHeld</c>, <c>_adsPull</c>) is the screen's, which a camera takeover, a lost focus, a covered screen
    /// and a scripted run's <c>rmb=</c> override.
    /// <list type="bullet">
    /// <item><description><b>One toggle</b>, <see cref="Toggled"/>. The touch screen's button flips it in either mode; the right
    /// button and the left trigger flip it only while the mode toggles. Toggled in, the lean is the toggle's alone: a held
    /// button or a part-pulled trigger is only the press that flipped it.</description></item>
    /// <item><description><b>A press, not a hold.</b> The trigger counts as down past <see cref="PreciseAim.TRIGGER_THRESHOLD"/>
    /// and as up only under half of it, so a slow pull that wavers on the threshold flips the toggle once. A button or a trigger
    /// still down when play comes back from a menu or a takeover has to be let go first (<see cref="SpendPress"/>).</description></item>
    /// <item><description><b>Let go</b> at a level's start and at its decision (<see cref="Release"/>, #1076), and once the other
    /// mode is seen, by the frame's read or by the door back into play (#1140). Settings switches the mode from the pause, and in Hold the right button and the
    /// trigger no longer flip the toggle: a lean toggled in and kept through the switch would stay in, with nothing to take it
    /// out, until the level ended.</description></item>
    /// <item><description><b>Kept</b> through a pause, a lost focus and a camera takeover. The screen lets go of the frame's hold
    /// and pull there, so the lens eases out, and a lean toggled in comes back as it was.</description></item>
    /// </list>
    /// </summary>
    internal sealed class LeanInput
    {
        /// <summary>
        /// Whether the lean is toggled in. Under Hold only the touch screen's button can have put it there: the mode's switch
        /// lets go of a toggle the right button or the trigger made.
        /// </summary>
        internal bool Toggled { get; private set; }

        //Whether the right button or the left trigger was down last frame, so a press is told from a hold. It starts down: the
        //press that opened the level is not one
        private bool _buttonWasDown = true;

        //The mode last seen, by a frame's read or a spent press, so the other one is noticed wherever it first arrives
        private bool _toggles;

        /// <summary>
        /// One frame's read. <paramref name="toggles"/> is Settings' mode as it stands now. <paramref name="edgeInputAllowed"/>
        /// gates the presses, as it gates every edge in play; the hold is read straight off the frame's state, since leaning
        /// the camera in is not an action that can go off by accident. With the cursor free (<paramref name="cursorCaptured"/>
        /// false) the right button is the desktop's, so only the pad's trigger leans.
        /// </summary>
        /// <param name="touchPressed">Whether a finger landed on the touch screen's precise-aim button this frame.</param>
        /// <param name="held">Whether the lean is asked for: what the lessons and the dozing gun read.</param>
        /// <param name="pull">How far, 0 to 1: what the lens follows (<see cref="PreciseAim.LeanAmount"/> under Hold).</param>
        internal void Read(bool toggles, bool edgeInputAllowed, bool touchPressed, bool cursorCaptured,
            in MouseState mouse, in GamePadState pad, out bool held, out float pull)
        {
            NoticeMode(toggles);

            if (edgeInputAllowed && touchPressed) Toggled = !Toggled;

            bool down = (cursorCaptured && mouse.RightButton == ButtonState.Pressed)
                || (pad.IsConnected
                    && pad.Triggers.Left > (_buttonWasDown ? PreciseAim.TRIGGER_THRESHOLD * 0.5f : PreciseAim.TRIGGER_THRESHOLD));
            if (edgeInputAllowed && toggles && down && !_buttonWasDown) Toggled = !Toggled;
            _buttonWasDown = down;

            if (Toggled)
            {
                held = true;
                pull = 1f;
            }
            else if (toggles)
            {
                held = false;
                pull = 0f;
            }
            else
            {
                MouseState hand = cursorCaptured ? mouse : default;
                held = PreciseAim.ButtonHeld(hand, pad);
                pull = PreciseAim.LeanAmount(hand, pad);
            }
        }

        /// <summary>The toggle off: at a level's start, and at its decision and every frame after it (#1076).</summary>
        internal void Release() => Toggled = false;

        /// <summary>
        /// A button or a trigger down now is not a press until it has been let go: play coming back from a menu or the pause,
        /// and every frame of a camera takeover. The toggle is kept, unless <paramref name="toggles"/>, Settings' mode as it
        /// stands now, is not the one last seen: the switch is seen here as in <see cref="Read"/>, since a takeover's frames read
        /// nothing and the touch screen's button is drawn off <see cref="Toggled"/> through them.
        /// </summary>
        internal void SpendPress(bool toggles)
        {
            NoticeMode(toggles);
            _buttonWasDown = true;
        }

        //A switch of the mode lets go of the toggle (#1140), wherever it is first seen
        private void NoticeMode(bool toggles)
        {
            if (toggles == _toggles) return;
            Toggled = false;
            _toggles = toggles;
        }
    }
}
