using MapEditor.GUI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The map editor's key help (#1209): every line it has is drawn, the file keys first and on screen folded or not,
    /// and the panel lies inside the window at every size it can be dragged to, every line of it, with no two strings
    /// over each other. <c>EditorKeyHelp</c> and <c>KeyHelpLayout</c> are compiled in from the MapEditor. The help used to
    /// be one 39-line block that ran off the bottom of the window with load and save as its last lines.
    /// <para>
    /// The widths come from a stand-in for the editor's 48-point Segoe UI (its line spacing, 1.33 em, and advances of
    /// its order: narrow strokes about a quarter of an em, capitals about 0.62), and from a third wider one, so the
    /// fit is held for a face of another width too. The captures on the issue show the real font.
    /// </para>
    /// </summary>
    public class MapEditorKeyHelpTests
    {
        private const float EM = 64f;
        private const float LINE = 1.33f * EM;
        private const float EPSILON = 0.01f;

        private static float SegoeLike(string text)
        {
            float width = 0f;
            foreach (char c in text)
            {
                width += c switch
                {
                    'i' or 'l' or 'j' or 't' or 'f' or 'r' or 'I' or '.' or ',' or '\'' or ' ' or '-' or '/' or ':' or '(' or ')' or '1' => 0.27f,
                    'm' or 'w' or 'M' or 'W' => 0.85f,
                    >= '0' and <= '9' => 0.56f,
                    >= 'A' and <= 'Z' => 0.62f,
                    _ => 0.52f,
                };
            }

            return width * EM;
        }

        private static float Wider(string text) => SegoeLike(text) * 1.33f;

        public static TheoryData<int, int, bool, bool> Windows()
        {
            (int, int)[] sizes =
            {
                (320, 200), (640, 400), (800, 600), (1024, 768), (1280, 800), (1600, 900), (1920, 1080), (2560, 1440),
                (3840, 1600), (3840, 2160), (600, 1000), (200, 800), (2000, 300), (1280, 240), (100, 60),
            };

            TheoryData<int, int, bool, bool> data = new();
            foreach ((int w, int h) in sizes)
                foreach (bool expanded in new[] { true, false })
                    foreach (bool wider in new[] { false, true })
                        data.Add(w, h, expanded, wider);
            return data;
        }

        [Fact]
        public void Every_line_the_help_declares_is_in_the_table_it_draws_once()
        {
            KeyHelpRow[] declared = typeof(EditorKeyHelp).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(KeyHelpRow))
                .Select(f => (KeyHelpRow)f.GetValue(null))
                .ToArray();

            Assert.NotEmpty(declared);
            foreach (KeyHelpRow row in declared) Assert.Single(EditorKeyHelp.All, r => ReferenceEquals(r, row));
            Assert.Equal(declared.Length, EditorKeyHelp.All.Length);
        }

        [Fact]
        public void The_file_keys_come_first_and_every_group_is_drawn_together_in_order()
        {
            Assert.Same(EditorKeyHelp.Open, EditorKeyHelp.All[0]);

            KeyGroup[] order = EditorKeyHelp.All.Select(r => r.Group).ToArray();
            for (int i = 1; i < order.Length; i++)
                Assert.True(order[i] >= order[i - 1], $"line {i} ({EditorKeyHelp.All[i].Keys}) breaks the groups' order");

            Assert.Equal(KeyGroup.File, order[0]);
            Assert.Equal(new[] { "F2", "F4", "F1", "F3" }, EditorKeyHelp.All.Where(r => r.Group == KeyGroup.File).Select(r => r.Keys));
        }

        [Fact]
        public void Folded_it_keeps_load_save_new_and_the_key_that_unfolds_it()
        {
            IReadOnlyList<KeyHelpRow> folded = EditorKeyHelp.Visible(expanded: false);

            Assert.Equal(new[] { EditorKeyHelp.Open, EditorKeyHelp.SaveLevel, EditorKeyHelp.SaveMap, EditorKeyHelp.NewMap,
                EditorKeyHelp.ToggleHelp }, folded);
            Assert.Contains("drop", EditorKeyHelp.Open.Text);
            Assert.Same(EditorKeyHelp.All, EditorKeyHelp.Visible(expanded: true));
        }

        [Fact]
        public void No_line_uses_a_character_the_font_does_not_have()
        {
            //The spritefont's regions: printable ASCII, Latin-1 and Latin Extended-A
            static bool InFont(char c) => c is >= ' ' and <= '~' or >= ' ' and <= 'ſ';

            foreach (KeyHelpRow row in EditorKeyHelp.All)
            {
                Assert.All(row.Keys, c => Assert.True(InFont(c), $"'{c}' in {row.Keys}"));
                Assert.All(row.Text, c => Assert.True(InFont(c), $"'{c}' in {row.Text}"));
            }
        }

        [Theory]
        [MemberData(nameof(Windows))]
        public void Every_line_lies_inside_the_window_and_none_covers_another(int width, int height, bool expanded, bool wider)
        {
            Func<string, float> measure = wider ? Wider : SegoeLike;
            IReadOnlyList<KeyHelpRow> rows = EditorKeyHelp.Visible(expanded);
            KeyHelpLayout layout = KeyHelpLayout.Compute(rows, measure, LINE, width, height);

            Assert.NotEmpty(layout.Items);

            //The panel inside the window, and under the FPS and status lines
            float top = KeyHelpLayout.TOP * height / KeyHelpLayout.DESIGN_HEIGHT;
            Assert.True(layout.PanelPosition.X >= 0f && layout.PanelPosition.Y >= top - EPSILON);
            Assert.True(layout.PanelPosition.X + layout.PanelSize.X <= width + EPSILON,
                $"{width}x{height}: the panel ends at x {layout.PanelPosition.X + layout.PanelSize.X}");
            Assert.True(layout.PanelPosition.Y + layout.PanelSize.Y <= height + EPSILON,
                $"{width}x{height}: the panel ends at y {layout.PanelPosition.Y + layout.PanelSize.Y}");

            //Every string inside the panel, and no two over each other
            (float X, float Y, float Right, float Bottom)[] boxes = layout.Items
                .Select(i => (i.Position.X, i.Position.Y, i.Position.X + measure(i.Text) * layout.Scale,
                    i.Position.Y + LINE * layout.Scale))
                .ToArray();

            foreach (var box in boxes)
            {
                Assert.True(box.X >= layout.PanelPosition.X - EPSILON && box.Y >= layout.PanelPosition.Y - EPSILON);
                Assert.True(box.Right <= layout.PanelPosition.X + layout.PanelSize.X + EPSILON,
                    $"{width}x{height}: a string ends at x {box.Right}, past the panel");
                Assert.True(box.Bottom <= layout.PanelPosition.Y + layout.PanelSize.Y + EPSILON,
                    $"{width}x{height}: a string ends at y {box.Bottom}, past the panel");
            }

            for (int a = 0; a < boxes.Length; a++)
                for (int b = a + 1; b < boxes.Length; b++)
                {
                    bool apart = boxes[a].Right <= boxes[b].X + EPSILON || boxes[b].Right <= boxes[a].X + EPSILON
                        || boxes[a].Bottom <= boxes[b].Y + EPSILON || boxes[b].Bottom <= boxes[a].Y + EPSILON;
                    Assert.True(apart, $"{width}x{height}: '{layout.Items[a].Text}' covers '{layout.Items[b].Text}'");
                }
        }

        [Theory]
        [MemberData(nameof(Windows))]
        public void Every_line_is_drawn_whole(int width, int height, bool expanded, bool wider)
        {
            Func<string, float> measure = wider ? Wider : SegoeLike;
            IReadOnlyList<KeyHelpRow> rows = EditorKeyHelp.Visible(expanded);
            KeyHelpLayout layout = KeyHelpLayout.Compute(rows, measure, LINE, width, height);

            //Each key once, followed by its description's lines (wrapped, joined back with a space) before the next key
            List<(string Key, string Text)> drawn = new();
            foreach (KeyHelpItem item in layout.Items)
            {
                if (item.Piece == KeyHelpPiece.Key) drawn.Add((item.Text, null));
                else if (item.Piece == KeyHelpPiece.Text)
                {
                    (string key, string text) = drawn[^1];
                    drawn[^1] = (key, text == null ? item.Text : text + " " + item.Text);
                }
            }

            Assert.Equal(rows.Select(r => (r.Keys, r.Text)), drawn);

            //A heading over each group that has one
            string[] headings = layout.Items.Where(i => i.Piece == KeyHelpPiece.Heading).Select(i => i.Text).ToArray();
            string[] wanted = rows.Select(r => EditorKeyHelp.Heading(r.Group)).Where(h => h != null).Distinct().ToArray();
            Assert.Equal(wanted, headings);
        }

        [Theory]
        [InlineData(1280, 800)]
        [InlineData(1600, 900)]
        [InlineData(1920, 1080)]
        [InlineData(3840, 1600)]
        public void At_the_usual_windows_the_text_is_drawn_at_its_full_size(int width, int height)
        {
            KeyHelpLayout layout = KeyHelpLayout.Compute(EditorKeyHelp.All, SegoeLike, LINE, width, height);

            float wanted = MathF.Max(KeyHelpLayout.TEXT_SCALE * height / KeyHelpLayout.DESIGN_HEIGHT,
                KeyHelpLayout.MIN_LINE_PIXELS / LINE);
            Assert.Equal(wanted, layout.Scale, 4);
            Assert.True(layout.Scale * LINE >= KeyHelpLayout.MIN_LINE_PIXELS - EPSILON);
        }

        [Fact]
        public void A_wrapped_description_keeps_every_word_and_no_line_runs_past_the_width()
        {
            const string text = "Click: look around with the mouse, click again to stop. Hold: pan";
            float max = SegoeLike("Click: look around with the");

            List<string> lines = KeyHelpLayout.Wrap(text, SegoeLike, max);

            Assert.True(lines.Count > 1);
            Assert.Equal(text, string.Join(" ", lines));
            Assert.All(lines, l => Assert.True(SegoeLike(l) <= max + EPSILON || !l.Contains(' '), l));
        }

        [Fact]
        public void A_window_with_no_room_draws_nothing_rather_than_throwing()
        {
            Assert.Empty(KeyHelpLayout.Compute(EditorKeyHelp.All, SegoeLike, LINE, 0, 0).Items);
            Assert.Empty(KeyHelpLayout.Compute(Array.Empty<KeyHelpRow>(), SegoeLike, LINE, 1280, 800).Items);
        }
    }
}
