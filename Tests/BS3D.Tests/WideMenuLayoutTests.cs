using BS3D.Screens;
using Prazsky.Core.Render;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The phone's layouts of the Scene, Jukebox, About and High Scores pages (#1153) are built from
    /// <see cref="WideMenuLayout"/>'s figures, and the issue's promises are held to them here: the scenes in three rows of
    /// seven, a chapter's ten levels in two columns of five read down and then across, every tile and row a finger presses at
    /// least <see cref="WideMenuLayout.TOUCH_TARGET_MM"/> tall on the X300 Ultra at the scale its menus are drawn, and the
    /// grid, the widest of the four, inside the X300's width at that scale and inside any screen the layouts are built for at
    /// the design's own size, the least <c>BS3DGame.FitPage</c> brings a page down to.
    /// <para>
    /// Each layout is wider than a stacked page, and a screen narrower than the X300's 2.2:1 cut their right-hand columns off
    /// until each stated its width (found by review): that every page with such a layout (the four, and since #1184 the pause,
    /// Top Players and the benchmark) states it, and builds its layout only where <see cref="WideMenuLayout.MIN_ASPECT"/>
    /// allows, is held by reading their source, since the pages are Myra trees
    /// no test can construct. How the pages look is the owner's to judge on the phone; the pages at 16:9 and 2:1 are seen in
    /// a Game run (<c>inches=2.8 touch</c> in a 2560x1440 or 2160x1080 window).
    /// </para>
    /// </summary>
    public class WideMenuLayoutTests
    {
        //A chapter's levels
        private const int CHAPTER = 10;

        //The pages with a layout for the screen's width, under Game/Screens: #1153's four, and the pause beside its level card,
        //Top Players beside its rail and the benchmark's entries beside its reading (#1184)
        private static readonly string[] WIDE_PAGES =
        {
            "ScenePage.cs", "JukeboxPage.cs", "AboutPage.cs", "HighScoresPage.cs", "PausePage.cs", "TopPlayersPage.cs", "BenchmarkPage.cs",
        };

        [Fact]
        public void The_scenes_stand_in_three_rows_of_seven()
        {
            //The game's own count (SceneRenderer.SceneCount is the catalog's): a scene added past twenty-one opens a fourth row
            //the page's height was not laid out for
            int scenes = SceneCatalog.Count;
            Assert.Equal(7, WideMenuLayout.SCENE_COLUMNS);
            Assert.Equal(3, WideMenuLayout.Rows(scenes, WideMenuLayout.SCENE_COLUMNS));

            //Across and then down, in the list's order: the eighth scene opens the second row
            Assert.Equal((6, 0), WideMenuLayout.RowMajor(6, WideMenuLayout.SCENE_COLUMNS));
            Assert.Equal((0, 1), WideMenuLayout.RowMajor(7, WideMenuLayout.SCENE_COLUMNS));
            Assert.Equal((6, 2), WideMenuLayout.RowMajor(20, WideMenuLayout.SCENE_COLUMNS));
        }

        [Fact]
        public void A_chapter_stands_in_two_columns_of_five_read_down_then_across()
        {
            int rows = WideMenuLayout.Rows(CHAPTER, WideMenuLayout.BOARD_COLUMNS);
            Assert.Equal(5, rows);

            //Levels one to five down the first column, six to ten down the second
            for (int slot = 0; slot < CHAPTER; slot++)
                Assert.Equal((slot / 5, slot % 5), WideMenuLayout.ColumnMajor(slot, rows));

            //A shorter chapter keeps its order and leaves the second column's foot empty
            int sevenRows = WideMenuLayout.Rows(7, WideMenuLayout.BOARD_COLUMNS);
            Assert.Equal(4, sevenRows);
            Assert.Equal((1, 2), WideMenuLayout.ColumnMajor(6, sevenRows));
        }

        [Fact]
        public void What_a_finger_presses_is_at_least_nine_millimetres_tall_on_the_X300_Ultra()
        {
            AssertFingerTall(WideMenuLayout.SCENE_TILE_HEIGHT, "A scene's tile");
            AssertFingerTall(WideMenuLayout.BOARD_ROW_HEIGHT, "A level's row on High Scores");

            //Help's odd-ball card (#1183) is its ball's square and its name under it: the square alone is held
            AssertFingerTall(WideMenuLayout.ODD_BALL_SQUARE, "The ball on Help's odd-ball card");
        }

        /// <summary>
        /// Help's odd balls on the phone (#1183): the cluster's ten kinds in two rows of five, and the cannon's two with the dead
        /// weight in one row more, so the three rows fit the X300's height with the panel beside them.
        /// </summary>
        [Fact]
        public void The_odd_balls_stand_in_three_rows_of_five()
        {
            int cluster = 0, apart = 1;
            foreach (OddBallCopy.Entry entry in OddBallCopy.Entries)
                if (entry.InGun) apart++;
                else cluster++;

            Assert.Equal(3, WideMenuLayout.Rows(cluster, WideMenuLayout.ODD_BALL_COLUMNS) + WideMenuLayout.Rows(apart, WideMenuLayout.ODD_BALL_COLUMNS));
        }

        private static void AssertFingerTall(int designUnits, string what)
        {
            float mm = WideMenuLayout.Millimetres(designUnits, WideMenuLayout.X300_MENU_SCALE,
                WideMenuLayout.X300_HEIGHT_PIXELS, WideMenuLayout.X300_HEIGHT_INCHES);

            Assert.True(mm >= WideMenuLayout.TOUCH_TARGET_MM, $"{what} is {designUnits} design units, {mm:0.0} mm on the phone.");
        }

        /// <summary>
        /// The conversion itself, against the audit's own reading (#1150): the menus' entries, 152 design units at the front
        /// end's size (108 and a padding of 22 a side), measured about 6.5 mm on the phone; the whole screen is 2.8 inches.
        /// </summary>
        [Fact]
        public void A_design_unit_is_converted_to_millimetres_as_the_phone_measures_it()
        {
            float screen = WideMenuLayout.Millimetres(WideMenuLayout.DESIGN_HEIGHT, 1f,
                WideMenuLayout.X300_HEIGHT_PIXELS, WideMenuLayout.X300_HEIGHT_INCHES);
            Assert.Equal(2.8f * 25.4f, screen, 2);

            float entry = WideMenuLayout.Millimetres(152, WideMenuLayout.X300_MENU_SCALE,
                WideMenuLayout.X300_HEIGHT_PIXELS, WideMenuLayout.X300_HEIGHT_INCHES);
            Assert.InRange(entry, 6.3f, 6.8f);
        }

        /// <summary>
        /// The grid with its plate fits the X300 Ultra's width at the scale its menus are drawn there, inside the room a fitted
        /// page keeps, so on the phone the page is drawn at the menus' size and not brought down for width.
        /// </summary>
        [Fact]
        public void The_scene_grid_fits_the_X300_Ultras_width_at_the_menus_scale()
        {
            float pixelsPerUnit = WideMenuLayout.X300_MENU_SCALE * WideMenuLayout.X300_HEIGHT_PIXELS / WideMenuLayout.DESIGN_HEIGHT;
            float grid = GridPlateWidth() * pixelsPerUnit;
            float room = FitRoom() * WideMenuLayout.X300_WIDTH_PIXELS;

            Assert.True(grid <= room, $"The scene grid's plate is {grid:0} px across on the X300 Ultra, which has {room:0}.");
        }

        /// <summary>
        /// <c>FitPage</c> brings a page down no further than the design's own size, where a design unit is at most a 2160th of
        /// the screen's height: on the squarest screen the wide layouts are built for, the widest of them still fits there.
        /// 16:9 and the X300's 2.2:1 have them, a 16:10 tablet keeps the stacked pages.
        /// </summary>
        [Fact]
        public void The_wide_layouts_are_built_only_where_the_widest_fits_at_the_design_size()
        {
            float room = FitRoom() * WideMenuLayout.MIN_ASPECT * WideMenuLayout.DESIGN_HEIGHT;
            Assert.True(GridPlateWidth() <= room,
                $"The scene grid's plate is {GridPlateWidth()} design units across; a {WideMenuLayout.MIN_ASPECT}:1 screen has {room:0}.");

            Assert.True(WideMenuLayout.WideEnough(WideMenuLayout.X300_WIDTH_PIXELS, WideMenuLayout.X300_HEIGHT_PIXELS));
            Assert.True(WideMenuLayout.WideEnough(2160, 1080));
            Assert.True(WideMenuLayout.WideEnough(2560, 1440));
            Assert.False(WideMenuLayout.WideEnough(2560, 1600));
            Assert.False(WideMenuLayout.WideEnough(2048, 1536));
            Assert.False(WideMenuLayout.WideEnough(1920, 0));
        }

        /// <summary>
        /// <see cref="WideMenuLayout.DESIGN_HEIGHT"/> is the host's own design height, and the plate's padding and the fit's
        /// room the two checks above use are read from the host: a figure restated here could drift from the one drawn.
        /// </summary>
        [Fact]
        public void The_figures_are_the_hosts_own()
        {
            Assert.Equal(WideMenuLayout.DESIGN_HEIGHT, (int)HostConstant("int", "MENU_DESIGN_HEIGHT"));
            Assert.InRange(PlatePadding(), 1, 400);
            Assert.InRange(FitRoom(), 0.5f, 1f);
        }

        /// <summary>
        /// Every page with a layout for the screen's width states the width it needs (<c>MenuPage.RequiredWidth</c>), so
        /// <c>FitPage</c> brings it inside a screen narrower than the X300's, and decides whether to build that layout through
        /// <c>MenuPage.UsesTheWidth</c>, which holds it to <see cref="WideMenuLayout.MIN_ASPECT"/>. Without the first, a 16:9
        /// phone showed six of the grid's seven columns and lost the Jukebox's Next (found by review).
        /// </summary>
        [Fact]
        public void Every_wide_page_states_its_width_and_asks_whether_the_screen_is_wide_enough()
        {
            foreach (string page in WIDE_PAGES)
            {
                string code = WithoutComments(File.ReadAllText(Path.Combine(GameScreens(), page)));
                Assert.True(StatesItsWidth(code), $"{page} builds a layout wider than a stacked page without stating its width.");
                Assert.True(AsksForTheWidth(code), $"{page} builds its wide layout without asking UsesTheWidth.");
            }
        }

        [Fact]
        public void The_source_rules_fire_on_a_page_that_breaks_them()
        {
            string code = WithoutComments(File.ReadAllText(Path.Combine(GameScreens(), "ScenePage.cs")));
            Assert.True(StatesItsWidth(code) && AsksForTheWidth(code));

            //The page as review found it: no width stated, the grid built wherever the interface is drawn larger
            Assert.False(StatesItsWidth(Regex.Replace(code, @"internal\s+override\s+int\s+RequiredWidth[^;]*;", string.Empty)));
            Assert.False(AsksForTheWidth(code.Replace("AsGrid = UsesTheWidth;", "AsGrid = Game.UiScale > 1f;")));
        }

        private static bool StatesItsWidth(string code) => Regex.IsMatch(code, @"internal\s+override\s+int\s+RequiredWidth\b");

        private static bool AsksForTheWidth(string code) => Regex.IsMatch(code, @"\bUsesTheWidth\b");

        //The grid of the page's columns with the plate's padding either side, in design units
        private static int GridPlateWidth() =>
            WideMenuLayout.SceneGridWidth(WideMenuLayout.SCENE_COLUMNS) + 2 * PlatePadding();

        private static int PlatePadding() => (int)HostConstant("int", "MENU_PLATE_PADDING_X");

        private static float FitRoom() => (float)HostConstant("float", "FIT_ROOM");

        //A constant of the menu's host, read off BS3DGame.Menu.cs (the Game is net10.0-windows and not compiled here)
        private static double HostConstant(string type, string name)
        {
            string source = File.ReadAllText(Path.Combine(GameScreens(), "..", "BS3DGame.Menu.cs"));
            Match match = Regex.Match(source, $@"\bconst\s+{type}\s+{name}\s*=\s*([0-9.]+)f?\s*;");
            Assert.True(match.Success, $"BS3DGame.Menu.cs declares no {type} {name}; update this test with the host.");
            return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static string WithoutComments(string code) => Regex.Replace(code, @"//[^\r\n]*", string.Empty);

        private static string GameScreens()
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Game", "Screens");
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException("No Game/Screens was found above the test assembly.");
        }
    }
}
