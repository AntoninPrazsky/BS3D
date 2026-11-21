using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MapEditor.GUI
{
    /// <summary>What a piece of the key help is, which decides its colour.</summary>
    public enum KeyHelpPiece
    {
        Heading,
        Key,
        Text,
    }

    /// <summary>One string of the key help and where its top-left corner goes, in pixels.</summary>
    public readonly record struct KeyHelpItem(string Text, Vector2 Position, KeyHelpPiece Piece);

    /// <summary>
    /// Where the map editor's key help goes in a window of a given size (#1209): its groups flowed into as many
    /// columns as it takes, and the text shrunk only when no column count fits, so <b>no line is ever below the
    /// window's edge</b>, at any size the window is dragged to.
    /// <para>
    /// A group is never split between two columns, so a heading always stands over its own lines. The keys stand in
    /// a column of their own, as wide as the widest key, so every description starts at the same x (the old block
    /// padded the key names with spaces in a proportional font, and its descriptions started at ragged positions).
    /// </para>
    /// <para>
    /// Arithmetic over widths handed in through a measure function, no device, so <c>Tests/BS3D.Tests</c> compiles
    /// it in and holds the panel inside the window over a sweep of sizes. Every spacing figure is in units of the
    /// font's line height, so it scales with the text; the placement on screen is in 2160p design units scaled with
    /// the window's height, as <c>InfoRenderer</c>'s FPS line and status line above it are.
    /// </para>
    /// </summary>
    public sealed class KeyHelpLayout
    {
        //In the font's line heights
        public const float KEY_GAP = 0.6f;
        public const float COLUMN_GAP = 1.2f;
        public const float GROUP_GAP = 0.35f;
        public const float PADDING = 0.45f;

        //The widest a description runs before it wraps onto a line of its own, in line heights. Wraps the long lines
        //(the colours, the mouse) rather than letting one of them make its whole column wide.
        public const float WRAP_WIDTH = 19f;

        //In 2160p design units, like InfoRenderer's: where the panel starts (under its FPS and status lines, which
        //stand at 20) and the margin kept from the window's other edges
        public const float DESIGN_HEIGHT = 2160f;
        public const float TOP = 150f;
        public const float MARGIN = 30f;

        //The text's size against the font's own, at a 2160-line window and in proportion below it (InfoRenderer drew
        //the old block at 0.7), and the smallest line it is drawn at while the window has room for that
        public const float TEXT_SCALE = 0.6f;
        public const float MIN_LINE_PIXELS = 18f;

        //Two fits closer than this are the same fit, and the one with fewer columns wins
        private const float FIT_EPSILON = 1e-4f;

        /// <summary>Nothing to draw: no lines, or a window with no room at all (minimized).</summary>
        public static readonly KeyHelpLayout Empty = new(0f, 0, Vector2.Zero, Vector2.Zero, Array.Empty<KeyHelpItem>());

        /// <summary>The scale the font is drawn at.</summary>
        public float Scale { get; }

        /// <summary>How many columns the groups were flowed into.</summary>
        public int Columns { get; }

        /// <summary>The backing panel's top-left corner, in pixels.</summary>
        public Vector2 PanelPosition { get; }

        /// <summary>The backing panel's size, in pixels; every item lies inside it.</summary>
        public Vector2 PanelSize { get; }

        /// <summary>Every string to draw, headings, keys and descriptions, in drawing order.</summary>
        public KeyHelpItem[] Items { get; }

        private KeyHelpLayout(float scale, int columns, Vector2 panelPosition, Vector2 panelSize, KeyHelpItem[] items)
        {
            Scale = scale;
            Columns = columns;
            PanelPosition = panelPosition;
            PanelSize = panelSize;
            Items = items;
        }

        //One drawn line inside a group: a heading, or a key (null on a wrapped description's later lines) and its text
        private readonly record struct Line(string Key, string Text, bool IsHeading);

        //A group's lines, and its size in font units
        private sealed class Block
        {
            public readonly List<Line> Lines = new();
            public float Width;
            public float Height;
        }

        /// <summary>
        /// Lays the lines out in a window of <paramref name="viewportWidth"/> × <paramref name="viewportHeight"/>
        /// pixels. Called when the window's size or the help's fold changes, never per frame: it allocates.
        /// </summary>
        /// <param name="rows">The lines to draw, each group's together (<see cref="EditorKeyHelp.Visible"/>).</param>
        /// <param name="measure">A string's width at scale 1 (the font's <c>MeasureString(...).X</c>).</param>
        /// <param name="lineHeight">The font's line spacing at scale 1.</param>
        /// <param name="viewportWidth">The window's client width, in pixels.</param>
        /// <param name="viewportHeight">The window's client height, in pixels.</param>
        public static KeyHelpLayout Compute(IReadOnlyList<KeyHelpRow> rows, Func<string, float> measure, float lineHeight,
            int viewportWidth, int viewportHeight)
        {
            if (rows == null || rows.Count == 0 || lineHeight <= 0f || viewportWidth <= 0 || viewportHeight <= 0) return Empty;

            float designScale = viewportHeight / DESIGN_HEIGHT;
            float top = TOP * designScale;
            float margin = MARGIN * designScale;
            float roomWidth = viewportWidth - 2f * margin;
            float roomHeight = viewportHeight - top - margin;
            if (roomWidth <= 0f || roomHeight <= 0f) return Empty;

            List<Block> blocks = BuildBlocks(rows, measure, lineHeight, out float textX);

            float wanted = MathF.Max(TEXT_SCALE * designScale, MIN_LINE_PIXELS / lineHeight);
            ChooseColumns(blocks, lineHeight, wanted, roomWidth, roomHeight, out int cuts, out float scale);

            return Place(blocks, cuts, lineHeight, textX, scale, new Vector2(margin, top));
        }

        //The groups as blocks of lines, descriptions wrapped; textX is where every description starts in a column
        private static List<Block> BuildBlocks(IReadOnlyList<KeyHelpRow> rows, Func<string, float> measure, float lineHeight,
            out float textX)
        {
            float keyWidth = 0f;
            foreach (KeyHelpRow row in rows) keyWidth = MathF.Max(keyWidth, measure(row.Keys));
            textX = keyWidth + KEY_GAP * lineHeight;

            List<Block> blocks = new();
            Block block = null;
            KeyGroup group = default;

            foreach (KeyHelpRow row in rows)
            {
                if (block == null || row.Group != group)
                {
                    block = new Block();
                    blocks.Add(block);
                    group = row.Group;

                    string heading = EditorKeyHelp.Heading(group);
                    if (heading != null)
                    {
                        block.Lines.Add(new Line(null, heading, true));
                        block.Width = measure(heading);
                    }
                }

                List<string> wrapped = Wrap(row.Text, measure, WRAP_WIDTH * lineHeight);
                for (int i = 0; i < wrapped.Count; i++)
                {
                    block.Lines.Add(new Line(i == 0 ? row.Keys : null, wrapped[i], false));
                    block.Width = MathF.Max(block.Width, textX + measure(wrapped[i]));
                }
            }

            foreach (Block b in blocks) b.Height = b.Lines.Count * lineHeight;
            return blocks;
        }

        /// <summary>
        /// Every way to cut the groups, in order, into columns (a bit per gap between two groups: at most eight
        /// groups, so 128 ways), and the one whose panel can be drawn largest, up to <paramref name="wanted"/>. Two
        /// equally large ones go to fewer columns, then to the shorter panel.
        /// </summary>
        private static void ChooseColumns(List<Block> blocks, float lineHeight, float wanted, float roomWidth,
            float roomHeight, out int bestCuts, out float bestScale)
        {
            int gaps = blocks.Count - 1;
            bestCuts = 0;
            bestScale = -1f;
            int bestColumns = int.MaxValue;
            float bestHeight = float.MaxValue;

            for (int cuts = 0; cuts < 1 << gaps; cuts++)
            {
                Measure(blocks, cuts, lineHeight, out float width, out float height, out int columns);
                float scale = MathF.Min(wanted, MathF.Min(roomWidth / width, roomHeight / height));

                bool better = scale > bestScale + FIT_EPSILON
                    || (scale > bestScale - FIT_EPSILON
                        && (columns < bestColumns || (columns == bestColumns && height < bestHeight)));
                if (!better) continue;

                bestCuts = cuts;
                bestScale = scale;
                bestColumns = columns;
                bestHeight = height;
            }
        }

        //The panel's size in font units, padding included, for one way of cutting the groups into columns
        private static void Measure(List<Block> blocks, int cuts, float lineHeight, out float width, out float height,
            out int columns)
        {
            width = 0f;
            height = 0f;
            columns = 1;

            float columnWidth = 0f, columnHeight = 0f;
            for (int i = 0; i < blocks.Count; i++)
            {
                bool newColumn = i > 0 && (cuts & (1 << (i - 1))) != 0;
                if (newColumn)
                {
                    width += columnWidth + COLUMN_GAP * lineHeight;
                    height = MathF.Max(height, columnHeight);
                    columnWidth = 0f;
                    columnHeight = 0f;
                    columns++;
                }

                if (columnHeight > 0f) columnHeight += GROUP_GAP * lineHeight;
                columnHeight += blocks[i].Height;
                columnWidth = MathF.Max(columnWidth, blocks[i].Width);
            }

            width += columnWidth + 2f * PADDING * lineHeight;
            height = MathF.Max(height, columnHeight) + 2f * PADDING * lineHeight;
        }

        private static KeyHelpLayout Place(List<Block> blocks, int cuts, float lineHeight, float textX, float scale,
            Vector2 panelPosition)
        {
            Measure(blocks, cuts, lineHeight, out float width, out float height, out int columns);

            List<KeyHelpItem> items = new();
            float line = lineHeight * scale;
            float left = panelPosition.X + PADDING * line;
            float firstY = panelPosition.Y + PADDING * line;
            float x = left, y = firstY, columnWidth = 0f;

            for (int i = 0; i < blocks.Count; i++)
            {
                bool newColumn = i > 0 && (cuts & (1 << (i - 1))) != 0;
                if (newColumn)
                {
                    x += (columnWidth + COLUMN_GAP * lineHeight) * scale;
                    y = firstY;
                    columnWidth = 0f;
                }
                else if (i > 0) y += GROUP_GAP * line;

                Block block = blocks[i];
                columnWidth = MathF.Max(columnWidth, block.Width);

                foreach (Line l in block.Lines)
                {
                    if (l.IsHeading) items.Add(new KeyHelpItem(l.Text, new Vector2(x, y), KeyHelpPiece.Heading));
                    else
                    {
                        if (l.Key != null) items.Add(new KeyHelpItem(l.Key, new Vector2(x, y), KeyHelpPiece.Key));
                        items.Add(new KeyHelpItem(l.Text, new Vector2(x + textX * scale, y), KeyHelpPiece.Text));
                    }

                    y += line;
                }
            }

            return new KeyHelpLayout(scale, columns, panelPosition, new Vector2(width, height) * scale, items.ToArray());
        }

        /// <summary>
        /// A description broken at spaces into lines no wider than <paramref name="maxWidth"/>. A single word wider
        /// than that keeps a line to itself rather than being cut; the block is measured from the lines as broken.
        /// </summary>
        public static List<string> Wrap(string text, Func<string, float> measure, float maxWidth)
        {
            List<string> lines = new();
            if (string.IsNullOrEmpty(text) || measure(text) <= maxWidth)
            {
                lines.Add(text ?? string.Empty);
                return lines;
            }

            string current = null;
            foreach (string word in text.Split(' '))
            {
                string candidate = current == null ? word : current + " " + word;
                if (current != null && measure(candidate) > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else current = candidate;
            }

            if (current != null) lines.Add(current);
            return lines;
        }
    }
}
