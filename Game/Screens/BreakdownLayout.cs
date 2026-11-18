using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The arithmetic of the result page's score breakdown (#1073, #1148): the share its faces are drawn at, how wide the
    /// next-star note may run and where it breaks, and how wide each of the grid's three columns is pinned. Pure over measured
    /// widths handed in, so a test pins its promises without a window (<c>BreakdownLayoutTests</c>) the way
    /// <see cref="BoardLayout"/> pins the boards'; <c>ResultPage</c> hands it the widths of the real faces.
    /// <para>
    /// <b>Every line of the grid is measured</b>: the three columns with the gaps between them, and the next-star note under the
    /// total. The note is in the small face, which the faces' fit does not scale, and under rules v2 (#1045) it is the grid's
    /// widest line ("Next star at 26 180 (+1 781), or 3 s faster"). Measured apart from the fit, it lengthened the detail column
    /// after the faces had been fitted, and on the phone the total and the note's end ran out of the plate and under the online
    /// boards beside it (#1148).
    /// </para>
    /// <para>
    /// <b>The faces fit the column; the note takes the width there is, then a second line.</b> The rows' faces are fitted to the
    /// plate's content width at the menu column's width (#1073), which also holds the page to the height it was budgeted for: on
    /// a monitor the page stands within a few rows of the frame's height, and rows drawn larger would spend it. The note's room
    /// (<see cref="NoteRoom"/>) is wider where the page has width to give: on a monitor the plate stands alone in its column with
    /// free width either side, so the note may widen it until it comes within a gap of the boards' plate. On the phone's split
    /// page the plate is held to its column beside the buttons, with the boards in the strip from its edge, while the height has
    /// room: there the note goes onto two lines at its comma (its copy ties every other space, <c>LevelResult.NextStarNote</c>),
    /// and nothing is drawn smaller for it.
    /// </para>
    /// </summary>
    internal static class BreakdownLayout
    {
        /// <summary>The three columns' widths at one size of the faces: the captions, the "count × worth" details and the values.</summary>
        internal readonly record struct Columns(float Caption, float Detail, float Value)
        {
            /// <summary>The three together, without the gaps between them.</summary>
            internal float Sum => Caption + Detail + Value;
        }

        /// <summary>
        /// How the grid is laid: the share of their size the rows' faces are drawn at, the columns pinned at that size (the
        /// detail column lengthened to hold the notes under them), the next-star note as it is set (its lines joined by
        /// <c>'\n'</c>) and the width of its widest line.
        /// </summary>
        internal readonly record struct Fit(float Scale, Columns Pinned, string Note, float NoteWidth);

        //A font's sizes round, so a face scaled by the share that fits can still measure a pixel over: measured again at most
        //this many times, each pass taking the share of what is left
        private const int FIT_PASSES = 4;

        //The least share the faces are taken down to: a room narrower than the gaps would otherwise scale them to nothing
        private const float MIN_SCALE = 0.25f;

        /// <summary>
        /// The widest the next-star note may run, in pixels at the layout in force. <paramref name="heldToColumn"/> (the phone's
        /// split page, #1052) is the plate's own content width: the plate stands beside the buttons, cut to their column, and the
        /// boards' strip starts at the column's edge. Otherwise the plate is centred on <paramref name="screenWidth"/> alone in
        /// its column, and it may widen past the column, centred, until its edge is <paramref name="gap"/> short of the boards'
        /// plate, whose left edge is <paramref name="boardsLeft"/>; never less than the column's own content width.
        /// </summary>
        /// <param name="columnWidth">The menu column's width: the plate's least width.</param>
        /// <param name="columnContentWidth">What a plate of <paramref name="columnWidth"/> holds inside its padding.</param>
        internal static int NoteRoom(int screenWidth, int columnWidth, int columnContentWidth, bool heldToColumn, int boardsLeft, int gap)
        {
            if (heldToColumn) return columnContentWidth;

            int padding = columnWidth - columnContentWidth;
            int plate = 2 * (boardsLeft - gap) - screenWidth;
            return Math.Max(columnContentWidth, plate - padding);
        }

        /// <summary>
        /// Lays the grid. The rows' faces are drawn at their size where the columns and the gaps fit <paramref name="columnRoom"/>,
        /// and where they do not at the one share that fits, measured again at that share (<paramref name="columnsAt"/> is the
        /// three columns' widths with the faces at a share of their size). The next-star note is set by <see cref="SpaceWrap"/> at
        /// <paramref name="noteRoom"/>, so it stays one line where the room holds it and breaks where its copy lets it (at its
        /// comma) where it does not. The detail column is then lengthened by whatever the widest note (this one's widest line, or
        /// <paramref name="otherNotes"/>, the unlock note's width when it shows) is wider than the columns, so the captions and the
        /// values stay at the edges.
        /// </summary>
        /// <param name="columnRoom">The plate's content width at the menu column's width.</param>
        /// <param name="noteRoom">The widest the note may run (<see cref="NoteRoom"/>), never less than <paramref name="columnRoom"/>.</param>
        /// <param name="gaps">The two gaps between the three columns.</param>
        /// <param name="noteWidth">A line of the note as its face draws it.</param>
        internal static Fit Lay(float columnRoom, float noteRoom, float gaps, Func<float, Columns> columnsAt, string note,
            Func<string, float> noteWidth, float otherNotes)
        {
            float scale = 1f;
            Columns columns = columnsAt(scale);

            for (int pass = 0; pass < FIT_PASSES && columns.Sum + gaps > columnRoom && scale > MIN_SCALE; pass++)
            {
                scale = MathF.Max(MIN_SCALE, scale * (columnRoom - gaps) / columns.Sum);
                columns = columnsAt(scale);
            }

            string set = string.IsNullOrEmpty(note) ? string.Empty : SpaceWrap.Wrap(note, noteRoom, noteWidth);
            float widestLine = 0f;
            if (set.Length > 0)
                foreach (string line in set.Split('\n'))
                    widestLine = MathF.Max(widestLine, MathF.Ceiling(noteWidth(line)));

            float notes = MathF.Max(widestLine, otherNotes);
            Columns pinned = columns with { Detail = columns.Detail + MathF.Max(0f, notes - (columns.Sum + gaps)) };

            return new Fit(scale, pinned, set, widestLine);
        }
    }
}
