using BS3D.Screens;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// An online board's row (#934, #977): a name never draws over the score beside it, a row that did not finish gives its name
    /// no less room than a clear, and the board takes the room a wide display adds. <c>BoardLayout</c> is compiled in from the
    /// Game; the face is a stand-in whose advances are design units of the menu's small face, read off captures: a capital W
    /// about 50, a narrow letter about 12, the other letters about 30 and a digit about 32 (the owner's report was a long
    /// nickname printed over "not finished", a word of 264 that was a column of the row until #977). The pages work in
    /// pixels, so the figures are scaled the way the menu scales its design units: by the window's height over 2160.
    /// <para>
    /// Seen to fail on the layout the owner reported (a name column left to the Fill column with the text unclipped): the premise
    /// test below states what that layout drew, a name wider than its column, and each assertion that the cut name fits is false
    /// when <c>Ellipsize</c> returns its text as it was. And on #977's: with a column of 264 for the mark put back into
    /// <c>NameWidth</c>, the name column of a board at its base width is 102 units and the room test fails.
    /// </para>
    /// </summary>
    public class BoardLayoutTests
    {
        private const int DESIGN_HEIGHT = 2160;

        //The board's own base width and floor in design units (ResultPage's BOARD_WIDTH and BOARD_MIN_WIDTH), the gap between columns
        private const int BOARD_WIDTH = 780, BOARD_MIN_WIDTH = 520, GAP = 30;

        private const int CUP = 56, RANK = 32 * 2, SCORE = 32 * 5 + 14;

        private static int Scaled(int units, int windowHeight) => Math.Max(1, (int)MathF.Round(units * (windowHeight / (float)DESIGN_HEIGHT)));

        //The stand-in face, in design units: what a string draws as, whole
        private static int Face(string text) => text.Sum(c => c switch
        {
            'W' or 'M' => 50,
            'i' or 'l' or 'j' or 'I' or ' ' => 12,
            >= '0' and <= '9' => 32,
            _ => 30,
        });

        //The face at a window's height, in pixels, as BoardView measures it: whole pixels, rounded up
        private static Func<string, int> FaceAt(int windowHeight) =>
            text => (int)MathF.Ceiling(Face(text) * (windowHeight / (float)DESIGN_HEIGHT));

        /// <summary>A row of a board at a window's height, with the columns' pixel widths and where each starts.</summary>
        private readonly record struct Row(int NameLeft, int NameWidth, int ScoreLeft)
        {
            public static Row At(int windowHeight, int boardWidth)
            {
                int cup = Scaled(CUP, windowHeight), rank = Scaled(RANK, windowHeight);
                int score = Scaled(SCORE, windowHeight), gap = Scaled(GAP, windowHeight);

                int name = BoardLayout.NameWidth(boardWidth, cup, rank, score, gap);
                int nameLeft = cup + gap + rank + gap;
                return new Row(nameLeft, name, nameLeft + name + gap);
            }
        }

        public static TheoryData<int, int> Windows() => new()
        {
            { 1280, 720 },
            { 1920, 1080 },
            { 2560, 1440 },
            { 3840, 1600 },
            { 3840, 2160 },
            { 1024, 768 },
        };

        private static string LongName(int length) => new('W', length);

        [Theory]
        [MemberData(nameof(Windows))]
        public void A_long_name_never_reaches_the_score(int width, int height)
        {
            int board = Scaled(BOARD_WIDTH, height);
            Row row = Row.At(height, board);
            Func<string, int> face = FaceAt(height);

            foreach (int length in new[] { 10, 16, 24, 40 })
            {
                string name = LongName(length);

                //The premise: this is what the owner saw. The column cannot hold the name whole, so a label that is not cut draws on
                Assert.True(face(name) > row.NameWidth, $"{length} W fit a {row.NameWidth} px column at {height} p: not a long name");

                string shown = BoardLayout.Ellipsize(name, row.NameWidth, face);

                int right = row.NameLeft + face(shown);
                Assert.True(right <= row.ScoreLeft - Scaled(GAP, height),
                    $"{length} W at {width}x{height}: the name ends at {right}, the score starts at {row.ScoreLeft}");
                Assert.EndsWith(BoardLayout.ELLIPSIS, shown);
            }
        }

        [Fact]
        public void A_40_character_name_at_1280_by_720_never_overlaps_the_score_column()
        {
            //The brief's figure, spelled out: 40 characters of ordinary letters as well as of capitals, on the smallest window the game runs in
            int board = Scaled(BOARD_WIDTH, 720);
            Row row = Row.At(720, board);
            Func<string, int> face = FaceAt(720);

            foreach (string name in new[] { LongName(40), new string('n', 40), "Nonexistent Pal Nonexistent Pal Nonexistent", string.Concat(Enumerable.Repeat("Wi", 20)) })
            {
                string shown = BoardLayout.Ellipsize(name, row.NameWidth, face);

                Assert.True(row.NameLeft + face(shown) + Scaled(GAP, 720) <= row.ScoreLeft, name);
                Assert.True(face(shown) <= row.NameWidth, name);
            }
        }

        [Theory]
        [MemberData(nameof(Windows))]
        public void On_a_board_at_its_base_width_the_name_has_room_for_a_real_nickname_whether_or_not_the_row_finished(int width, int height)
        {
            //#977: "not finished" was a column of 264 units and left a row that wore it 102 of the 780, three or four letters. The mark
            //is no column now, so the room is the one every row has: eleven ordinary letters and six capital Ws
            int board = Scaled(BOARD_WIDTH, height);
            Row row = Row.At(height, board);
            Func<string, int> face = FaceAt(height);

            Assert.True(row.NameWidth >= face(new string('n', 11)), $"{width}x{height}: {row.NameWidth} px holds fewer than eleven letters");
            Assert.True(row.NameWidth >= face(LongName(6)), $"{width}x{height}: {row.NameWidth} px holds fewer than six Ws");
            Assert.Equal("Kuba", BoardLayout.Ellipsize("Kuba", row.NameWidth, face));
        }

        [Theory]
        [InlineData(1, 4, "Cup")]
        [InlineData(2, 3, "Cup")]
        [InlineData(3, 1, "Cup")]
        [InlineData(4, 4, "None")]
        [InlineData(17, 1, "None")]
        [InlineData(0, 2, "None")]
        [InlineData(1, 0, "Unfinished")]
        [InlineData(3, 0, "Unfinished")]
        [InlineData(4, 0, "Unfinished")]
        [InlineData(17, 0, "Unfinished")]
        public void A_cup_is_for_a_clear_in_the_first_three_places_and_an_attempt_wears_the_hollow_star_instead(int rank, int stars, string expected)
        {
            //Where a board holds so few clears that an attempt ranks among the first three, it does not wear the cup
            Assert.Equal(Enum.Parse<BoardLayout.Sign>(expected), BoardLayout.SignOf(rank, stars));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(37)]
        [InlineData(100)]
        [InlineData(10_000)]
        public void A_name_is_cut_to_the_measure_and_never_by_more_than_it_has_to_be(int measure)
        {
            foreach (string name in new[] { "Kuba", "Nonexistent Pal", LongName(16), "Rudi  ", "ABCDEFGH\U0001F3C6\U0001F3C6IJ", "Šárka Přibylová-Černá" })
            {
                string shown = BoardLayout.Ellipsize(name, measure, Face);

                if (Face(name) <= measure)
                {
                    Assert.Same(name, shown);
                    continue;
                }

                //Cut: it fits, or is nothing at all where not even the ellipsis does
                Assert.True(shown.Length == 0 || Face(shown) <= measure, $"'{name}' in {measure}: '{shown}' is {Face(shown)}");

                if (shown.Length == 0)
                {
                    Assert.True(Face(BoardLayout.ELLIPSIS) > measure);
                    continue;
                }

                //Not through the middle of a surrogate pair, and no space before the ellipsis
                string kept = shown[..^BoardLayout.ELLIPSIS.Length];
                Assert.False(kept.Length > 0 && char.IsHighSurrogate(kept[^1]), $"'{name}' cut through a pair");
                Assert.False(kept.EndsWith(' '), $"'{name}' kept a space before the ellipsis");
                Assert.StartsWith(kept, name);

                //The longest that fits: no longer beginning of the name, whole characters, fits with the ellipsis after it
                for (int length = kept.Length + 1; length < name.Length; length++)
                {
                    if (char.IsHighSurrogate(name[length - 1])) continue;

                    string longer = name[..length].TrimEnd();
                    if (longer.Length <= kept.Length) continue;

                    Assert.True(Face(longer + BoardLayout.ELLIPSIS) > measure, $"'{name}' in {measure}: '{shown}' left room for '{longer}'");
                }
            }
        }

        [Fact]
        public void A_name_that_fits_is_the_same_string_and_nothing_is_nothing()
        {
            Assert.Same("Kuba", BoardLayout.Ellipsize("Kuba", 1000, Face));
            Assert.Null(BoardLayout.Ellipsize(null, 10, Face));
            Assert.Equal(string.Empty, BoardLayout.Ellipsize(string.Empty, 10, Face));
        }

        [Theory]
        [InlineData(1920, 1080)]
        [InlineData(1280, 720)]
        [InlineData(1024, 768)]
        [InlineData(1600, 1000)]
        public void A_display_no_wider_than_16_by_9_keeps_the_boards_base_width(int width, int height)
        {
            int baseWidth = Scaled(BOARD_WIDTH, height);

            Assert.Equal(baseWidth, BoardLayout.Ceiling(width, height, baseWidth, fullWidth: baseWidth * 4));
        }

        [Fact]
        public void On_3840_by_1600_the_board_is_wider_and_takes_half_of_what_the_display_adds()
        {
            int baseWidth = Scaled(BOARD_WIDTH, 1600);
            int full = baseWidth * 4;

            int ceiling = BoardLayout.Ceiling(3840, 1600, baseWidth, full);

            //1600 p is 2844 wide at 16:9, the window is 996 wider and the strip beside the column gains half of that
            Assert.Equal(baseWidth + (3840 - 1600 * 16 / 9) / 2, ceiling);

            //In design units, the board 1920 by 1080 has is the base one; this one is at least half as wide again
            float units = ceiling / (1600 / (float)DESIGN_HEIGHT);
            Assert.True(units > BOARD_WIDTH * 1.5f, $"{units:0} design units is not notably wider than {BOARD_WIDTH}");
        }

        [Fact]
        public void The_boards_ceiling_follows_the_viewports_width_unit_for_unit_and_stops_at_the_full_width()
        {
            int height = 1600;
            int baseWidth = Scaled(BOARD_WIDTH, height);
            int full = Scaled(1450, height);
            int previous = BoardLayout.Ceiling(2844, height, baseWidth, full);

            for (int width = 2900; width <= 5200; width += 100)
            {
                int ceiling = BoardLayout.Ceiling(width, height, baseWidth, full);

                Assert.True(ceiling >= previous, $"{width}: {ceiling} under {previous}");
                if (ceiling < full) Assert.Equal(baseWidth + (width - height * 16 / 9) / 2, ceiling);
                Assert.True(ceiling <= full);
                previous = ceiling;
            }

            Assert.Equal(full, previous);
        }

        [Fact]
        public void A_full_width_board_shows_the_longest_nickname_whole_beside_its_rank_and_score()
        {
            foreach (int height in new[] { 720, 1080, 1600, 2160 })
            {
                Func<string, int> face = FaceAt(height);
                int cup = Scaled(CUP, height), rank = Scaled(RANK, height);
                int score = Scaled(SCORE, height), gap = Scaled(GAP, height);
                string widest = LongName(16);

                int full = BoardLayout.FullWidth(cup, rank, face(widest), score, gap);
                int name = BoardLayout.NameWidth(full, cup, rank, score, gap);

                Assert.Equal(face(widest), name);
                Assert.Equal(widest, BoardLayout.Ellipsize(widest, name, face));

                //One pixel less and the widest name is cut: the width is exactly what it needs
                int tight = BoardLayout.NameWidth(full - 1, cup, rank, score, gap);
                Assert.NotEqual(widest, BoardLayout.Ellipsize(widest, tight, face));
            }
        }

        [Fact]
        public void The_boards_view_writes_every_name_through_Ellipsize_and_gives_every_column_a_width_of_its_own()
        {
            //BoardView is a Myra tree no test can build, so it is held the way ProseLabelsTests holds the wrapping labels: by its
            //source. The name column is never left to the grid's Fill or Auto (the layout that drew a name over the mark), and the
            //one place a name's text is written cuts it to the column first
            string source = File.ReadAllText(Path.Combine(ScreensDirectory(), "BoardView.cs"));

            Assert.DoesNotMatch(@"ColumnsProportions\.Add\(new Proportion\(ProportionType\.(Fill|Auto)", source);

            //#977: the unfinished mark is no column of the row and no word in it - the row says it with the hollow star where a cup
            //would stand and with the recessed slab High Scores' unfinished rows rest in, and the one rule for which row wears the
            //star is BoardLayout's, so the page and the test cannot disagree about it
            Assert.DoesNotContain("_mark", source);
            Assert.Contains("BoardLayout.SignOf(", source);
            Assert.Contains("MENU_ROW_UNFINISHED_BRUSH", source);

            string[] writes = source.Split("_name[i].Text =");
            Assert.Equal(2, writes.Length);
            Assert.Contains("BoardLayout.Ellipsize(", writes[1].Split(';')[0]);

            //And the three pages that build a board take its ceiling from BoardLayout, none a constant of its own
            foreach (string page in new[] { "ResultPage.cs", "LevelSelectPage.cs", "LevelBoardPage.cs" })
                Assert.Contains("BoardLayout.Ceiling(", File.ReadAllText(Path.Combine(ScreensDirectory(), page)));
        }

        private static string ScreensDirectory()
        {
            string relative = Path.Combine("Game", "Screens");

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException($"No {relative} was found above the test assembly.");
        }

        [Fact]
        public void At_the_boards_floor_the_name_still_has_a_column()
        {
            //BOARD_MIN_WIDTH (520 units) is wider than the cup, rank and score and their gaps together (384 here), so the name keeps
            //a column on the smallest plate the pages draw, on every row; and where a board is narrower than even those it has none
            //and draws none rather than draw over the score
            foreach (int height in new[] { 540, 720, 1080 })
            {
                int floor = Scaled(BOARD_MIN_WIDTH, height);
                Row row = Row.At(height, floor);

                Assert.True(row.NameWidth > 0, $"{height} p: the floor leaves the name nothing");
                Assert.NotEqual(string.Empty, BoardLayout.Ellipsize("Kuba", row.NameWidth, FaceAt(height)));
            }

            Assert.Equal(0, BoardLayout.NameWidth(10, 5, 5, 5, 5));
            Assert.Equal(string.Empty, BoardLayout.Ellipsize("Kuba", 0, Face));
        }
    }
}
