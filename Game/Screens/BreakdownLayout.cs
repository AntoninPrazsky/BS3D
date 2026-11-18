using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The arithmetic of the result page's score breakdown (#1073, #1148): the share its faces are drawn at, how wide the
    /// next-star note may run and how it is fitted to that, and how wide each of the grid's three columns is pinned. Pure over
    /// measured widths handed in, so a test pins its promises without a window (<c>BreakdownLayoutTests</c>) the way
    /// <see cref="BoardLayout"/> pins the boards'; <c>ResultPage</c> hands it the widths of the real faces.
    /// <para>
    /// <b>Every line of the grid is measured</b>: the three columns with the gaps between them, and the next-star note under the
    /// total. The note is in the small face, which the rows' fit does not scale, and under rules v2 (#1045) it is the grid's
    /// widest line ("Next star at 26 180 (+1 781), or 3 s faster"). Measured apart from the fit, it lengthened the detail column
    /// after the faces had been fitted, and on the phone the total and the note's end ran out of the plate and under the online
    /// boards beside it (#1148).
    /// </para>
    /// <para>
    /// <b>The rows fit the column; the note takes the width there is, and then what the page has to spare.</b> The rows' faces
    /// are fitted to the plate's content width at the menu column's width (#1073), which also holds the page to the height it
    /// was budgeted for. The note's room (<see cref="NoteRoom"/>) is wider where the page has width to give: on a monitor the
    /// plate stands alone in its column with free width either side, so the note may widen it until it comes within a gap of
    /// the boards' plate. Where that is not enough the page gives what it has: on the phone's split page, where the plate is
    /// held to its column beside the buttons and the height has room, the note goes onto two lines at its comma (its copy ties
    /// every other space, <c>LevelResult.NextStarNote</c>) and nothing is drawn smaller for it; on a monitor, where the height is
    /// the budget (a block's milestone with NEW BEST already stands to the frame's bottom edge at 1080 rows), the note stays one
    /// line in its face scaled down to the room (at 4:3 and 5:4 from about 0.85 of it to 0.75 for the widest note).
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
        /// <c>'\n'</c>), the share of its size the note's face is drawn at, and the width of its widest line in that face.
        /// </summary>
        internal readonly record struct Fit(float Scale, Columns Pinned, string Note, float NoteScale, float NoteWidth);

        //A font's sizes round, so a face scaled by the share that fits can still measure a pixel over: measured again at most
        //this many times, each pass taking the share of what is left
        private const int FIT_PASSES = 4;

        //The least share the rows' faces are taken down to: a room narrower than the gaps would otherwise scale them to nothing
        private const float MIN_SCALE = 0.25f;

        //The least share the note's face is taken down to on a page with no height to spare. At 4:3 and 5:4, where the plate
        //cannot widen, Pennant's note needs 0.85 of it and the widest a shipped level gives ("Next star at 132 440 (+92 440),
        //or 177 s faster", on a level of 1 204 balls) about 0.75 by BreakdownLayoutTests' stand-in face; below this it would
        //stop reading as the page's small print, and it breaks at its comma instead
        private const float MIN_NOTE_SCALE = 0.7f;

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
        /// three columns' widths with the faces at a share of their size). The next-star note stays one line in its own face where
        /// <paramref name="noteRoom"/> holds it; where it does not, with <paramref name="heightToSpare"/> it is set by
        /// <see cref="SpaceWrap"/> at the room, which breaks it where its copy lets it (at its comma), and without, its face is
        /// scaled down to the room, to <see cref="MIN_NOTE_SCALE"/> at the least and broken only past that. The detail column is
        /// then lengthened by whatever the widest note (this one's widest line, or <paramref name="otherNotes"/>, the unlock note's
        /// width when it shows) is wider than the columns, so the captions and the values stay at the edges.
        /// </summary>
        /// <param name="columnRoom">The plate's content width at the menu column's width.</param>
        /// <param name="noteRoom">The widest the note may run (<see cref="NoteRoom"/>), never less than <paramref name="columnRoom"/>.</param>
        /// <param name="heightToSpare">Whether the page can take the note's second line (the phone's split page).</param>
        /// <param name="gaps">The two gaps between the three columns.</param>
        /// <param name="noteWidthAt">A line of the note as its face draws it at a share of its size.</param>
        internal static Fit Lay(float columnRoom, float noteRoom, bool heightToSpare, float gaps, Func<float, Columns> columnsAt,
            string note, Func<float, string, float> noteWidthAt, float otherNotes)
        {
            float scale = 1f;
            Columns columns = columnsAt(scale);

            for (int pass = 0; pass < FIT_PASSES && columns.Sum + gaps > columnRoom && scale > MIN_SCALE; pass++)
            {
                scale = MathF.Max(MIN_SCALE, scale * (columnRoom - gaps) / columns.Sum);
                columns = columnsAt(scale);
            }

            (string set, float noteScale) = SetNote(note, noteRoom, heightToSpare, noteWidthAt);

            float widestLine = 0f;
            if (set.Length > 0)
                foreach (string line in set.Split('\n'))
                    widestLine = MathF.Max(widestLine, MathF.Ceiling(noteWidthAt(noteScale, line)));

            float notes = MathF.Max(widestLine, otherNotes);
            Columns pinned = columns with { Detail = columns.Detail + MathF.Max(0f, notes - (columns.Sum + gaps)) };

            return new Fit(scale, pinned, set, noteScale, widestLine);
        }

        /// <summary>The next-star note's lines and its face's share, as <see cref="Lay"/> describes.</summary>
        private static (string Set, float Scale) SetNote(string note, float room, bool heightToSpare, Func<float, string, float> widthAt)
        {
            if (string.IsNullOrEmpty(note)) return (string.Empty, 1f);

            //One line, every tie drawn as the space it is (SpaceWrap's own rule), where it fits as it is
            string line = note.Replace(SpaceWrap.TIE, ' ');
            float width = widthAt(1f, line);
            if (width <= room) return (line, 1f);

            if (heightToSpare) return (SpaceWrap.Wrap(note, room, l => widthAt(1f, l)), 1f);

            float scale = 1f;
            for (int pass = 0; pass < FIT_PASSES && width > room && scale > MIN_NOTE_SCALE; pass++)
            {
                scale = MathF.Max(MIN_NOTE_SCALE, scale * room / width);
                width = widthAt(scale, line);
            }

            return width <= room ? (line, scale) : (SpaceWrap.Wrap(note, room, l => widthAt(scale, l)), scale);
        }
    }
}
