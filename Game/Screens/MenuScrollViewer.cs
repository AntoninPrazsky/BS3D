using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using Myra.Events;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;
using System.Diagnostics;

namespace BS3D.Screens
{
    /// <summary>
    /// The scroller every scrolling menu stands in (<c>BS3DGame.MenuScroll</c>: Help's six bodies, the Jukebox's two lists,
    /// the Scene list, the level picker's grid), with one change to Myra's: the mouse wheel moves it
    /// <see cref="WheelScroll.VIEWPORT_SHARE"/> of its own height a notch, scaled by the notches the frame reported and
    /// boosted while a spin goes on (<see cref="WheelScroll.Momentum"/>) (#952). Myra's step was ten times content height
    /// over viewport height in pixels, whatever the delta.
    /// <para>
    /// <b>Nothing else is changed:</b> the scrollbar's thumb, a drag on it, the pad's walk (<c>ScrollNavEntryIntoView</c>)
    /// and the right stick and Page Up/Down on Help all write <c>ScrollPosition</c> as they did, and
    /// <c>KeepNavFocusInView</c> follows the position this writes the way it follows theirs. Myra hands a wheel delta only
    /// to the widget under the pointer that <c>AcceptsMouseWheel</c>, which a scroller does while its content is taller than
    /// it, so a list that fits takes no wheel and leaves it to the page (<c>OnScrollWheel</c>).
    /// </para>
    /// <para>
    /// <b>And a finger drags it</b> (#1052): Myra scrolls a list by touch only through its scrollbar's thumb, so a phone's
    /// Help, High Scores and lists could not be read past their first screen. A finger put down on the list and moved
    /// further than <see cref="DRAG_SLOP_SHARE"/> of the screen's height carries the list with it, and the press it began
    /// on is not a click (<see cref="SwallowsClick"/>, which <c>MenuClickable</c> asks). Only a finger: the mouse's press
    /// reaches Myra as a touch too, and the desktop's lists keep their wheel and their thumb.
    /// </para>
    /// <para>
    /// <b>And it clips its list at its own box</b> (#1002, <see cref="InternalRender"/>): Myra cuts a scroller's content at
    /// the edge of its margin, so the margin <c>MenuScroll</c> gives it to stand the cut clear of the tabs and of Back
    /// would otherwise fill with the very lines it is there to keep away (photographed: a line of Help drawn a margin past
    /// the scroll bar's end).
    /// </para>
    /// </summary>
    internal sealed class MenuScrollViewer : ScrollViewer
    {
        //The part of a pixel the last step could not place (WheelScroll.Move)
        private float _carry;

        //How fast the notches are arriving (WheelScroll.Momentum)
        private WheelScroll.Momentum _momentum;

        //The first wheel turn of a run says what a notch is worth, once: the figure that tells a log reader whether a
        //"the wheel is too slow" report was this step or something else
        private static bool _logged;

        /// <summary>How far a finger travels, as a share of the screen's height, before it drags the list rather than presses.</summary>
        internal const float DRAG_SLOP_SHARE = 0.02f;

        //The part of the box at its right edge that is the scrollbar's, left to Myra's own drag of the thumb
        private const float SCROLLBAR_SHARE = 0.06f;

        //A drag under way: where the finger came down and where the list stood then
        private bool _dragging;
        private int _dragFromY, _dragScrollY;

        /// <summary>
        /// Whether the last finger that went down has dragged a list: the press it began on is then not a click. Cleared by
        /// the next finger going down anywhere.
        /// </summary>
        internal static bool SwallowsClick { get; private set; }

        /// <summary>A finger went down somewhere: whatever the last one dragged, this one's press is its own.</summary>
        internal static void NewGesture() => SwallowsClick = false;

        public override void OnTouchDown()
        {
            base.OnTouchDown();

            if (Desktop?.TouchPosition is not Point at || ScrollMaximum.Y <= 0) return;

            //A finger, not the mouse, whose button Myra reads as a touch as well
            if (TouchPanel.GetState().Count == 0) return;

            Point local = ToLocal(at);
            if (ShowVerticalScrollBar && local.X > ActualBounds.Width * (1f - SCROLLBAR_SHARE)) return;

            _dragging = true;
            _dragFromY = at.Y;
            _dragScrollY = ScrollPosition.Y;
            Desktop.TouchMoved += OnDragMoved;
            Desktop.TouchUp += OnDragEnded;
        }

        private void OnDragMoved(object sender, MyraEventArgs args)
        {
            if (!_dragging || Desktop?.TouchPosition is not Point at) return;

            int travel = at.Y - _dragFromY;
            if (!SwallowsClick && Math.Abs(travel) < DRAG_SLOP_SHARE * Math.Max(1, TouchPanel.DisplayHeight)) return;

            SwallowsClick = true;
            int target = Math.Clamp(_dragScrollY - travel, 0, ScrollMaximum.Y);
            if (target != ScrollPosition.Y) ScrollPosition = new Point(ScrollPosition.X, target);
        }

        private void OnDragEnded(object sender, MyraEventArgs args)
        {
            _dragging = false;
            if (Desktop == null) return;
            Desktop.TouchMoved -= OnDragMoved;
            Desktop.TouchUp -= OnDragEnded;
        }

        /// <summary>
        /// Draws the scroller with the scissor narrowed to its own box, margin excluded (#1002), and puts the scissor back.
        /// The box is where the desktop laid it out, in the back buffer's pixels (<c>ToGlobal</c>, as Help's odd balls are
        /// cut, <c>HelpPage.ForegroundClip</c>), so a ball and the words beside it are cut on the same line.
        /// </summary>
        public override void InternalRender(RenderContext context)
        {
            Rectangle box = ActualBounds;
            Point corner = ToGlobal(new Point(box.X, box.Y));
            Rectangle scissor = context.Scissor;

            context.Scissor = Rectangle.Intersect(scissor, new Rectangle(corner.X, corner.Y, box.Width, box.Height));
            base.InternalRender(context);
            context.Scissor = scissor;
        }

        /// <summary>
        /// Moves the list by the wheel's delta. <c>base.OnMouseWheel</c> is not called: it is Myra's own step, and the
        /// <c>Widget</c> under it, which that calls first, has nothing in it.
        /// </summary>
        public override void OnMouseWheel(float delta)
        {
            int maximum = ScrollMaximum.Y;
            if (delta == 0f || maximum <= 0) return;

            int viewport = ActualBounds.Height;
            float boost = _momentum.Take(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, delta);

            Point at = ScrollPosition;
            int target = WheelScroll.Move(at.Y, maximum, WheelScroll.Pixels(delta, viewport, boost), ref _carry);

            if (target != at.Y) ScrollPosition = new Point(at.X, target);

            if (_logged) return;
            _logged = true;

            float notch = Math.Abs(WheelScroll.Pixels(WheelScroll.NOTCH, viewport));
            Console.WriteLine($"[menu] wheel: a slow notch moves {notch:0} px, {100f * notch / Math.Max(1, viewport):0} % of the {viewport} px viewport"
                + $" (range {maximum} px, up to {WheelScroll.MAX_BOOST:0}x while a spin goes on)");
        }
    }
}
