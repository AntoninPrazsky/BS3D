using Microsoft.Xna.Framework;
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
