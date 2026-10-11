using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// Which menu entry lies in the direction pressed (#848): the geometry the pad's walk over a grid page uses
    /// (<c>BS3DGame.StepNavToward</c>, <see cref="MenuPage.PadWalksGrid"/>), apart from the widgets so it can be tested.
    /// <para>
    /// Of the entries whose centre is ahead along the axis pressed, one <b>in line</b> with the current one wins over any
    /// that is not — its centre inside the current entry's span across the axis, or the current centre inside its span —
    /// and among those the nearest, a step across the axis counting twice. So down from a tile is the tile under it, up
    /// from a tile is the pip nearest above it, and an entry with nothing in line ahead still goes to the nearest one that
    /// way within 45° of the direction, edge to edge (a pip at the row's end down to the grid's corner, a corner tile
    /// down to Back) — never to one further across than ahead, which is how right from the grid's last tile would have
    /// jumped up to the next-chapter arrow. Nothing that way: −1, and nothing moves.
    /// </para>
    /// </summary>
    internal static class NavWalk
    {
        /// <param name="entries">Every entry's rectangle on the screen, in the walk's order.</param>
        /// <param name="from">The index of the entry the cursor stands on.</param>
        /// <param name="dx">−1 or +1 for left or right, 0 for up and down.</param>
        /// <param name="dy">−1 or +1 for up or down, 0 for left and right.</param>
        /// <returns>The index of the entry to move to, or −1 when there is none that way.</returns>
        internal static int Toward(IReadOnlyList<Rectangle> entries, int from, int dx, int dy)
        {
            if (from < 0 || from >= entries.Count || (dx == 0) == (dy == 0)) return -1;

            Rectangle here = entries[from];
            int centreX = here.Center.X, centreY = here.Center.Y;

            int found = -1;
            long best = long.MaxValue;
            bool bestInLine = false;

            for (int i = 0; i < entries.Count; i++)
            {
                if (i == from) continue;

                Rectangle other = entries[i];
                int otherX = other.Center.X, otherY = other.Center.Y;

                int ahead = dx != 0 ? (otherX - centreX) * dx : (otherY - centreY) * dy;
                if (ahead <= 0) continue;

                int across = dx != 0 ? Math.Abs(otherY - centreY) : Math.Abs(otherX - centreX);
                bool inLine = dx != 0
                    ? (otherY >= here.Top && otherY <= here.Bottom) || (centreY >= other.Top && centreY <= other.Bottom)
                    : (otherX >= here.Left && otherX <= here.Right) || (centreX >= other.Left && centreX <= other.Right);

                //Out of line, only within 45° of the direction pressed, measured between the two spans' edges rather than
                //their centres: down from the grid's corner tile reaches the wide Back its span overlaps, while right from
                //the grid's last tile does not reach the arrow standing far above it
                int gap = dx != 0
                    ? Math.Max(0, Math.Max(here.Top, other.Top) - Math.Min(here.Bottom, other.Bottom))
                    : Math.Max(0, Math.Max(here.Left, other.Left) - Math.Min(here.Right, other.Right));
                if (!inLine && gap > ahead) continue;

                long score = ahead + 2L * across;
                if ((inLine && !bestInLine) || (inLine == bestInLine && score < best))
                {
                    found = i;
                    best = score;
                    bestInLine = inLine;
                }
            }

            return found;
        }
    }
}
