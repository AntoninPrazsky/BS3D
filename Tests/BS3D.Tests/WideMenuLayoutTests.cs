using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The phone's layouts of the Scene, Jukebox, About and High Scores pages (#1153) are built from
    /// <see cref="WideMenuLayout"/>'s figures, and the issue's promises are held to them here: the scenes in three rows of
    /// seven, a chapter's ten levels in two columns of five read down and then across, and every tile and row a finger
    /// presses at least <see cref="WideMenuLayout.TOUCH_TARGET_MM"/> tall on the X300 Ultra at the scale its menus are drawn.
    /// How the pages look is the owner's to judge on the phone; that they fit its screen without scrolling is seen in a Game
    /// run (no "[ui] … laid out at" line for the page), since the pages are Myra trees no test can construct.
    /// </summary>
    public class WideMenuLayoutTests
    {
        //The scenes the game has (SceneRenderer.SceneCount, which this project does not compile) and a chapter's levels
        private const int SCENES = 21;
        private const int CHAPTER = 10;

        [Fact]
        public void Twenty_one_scenes_stand_in_three_rows_of_seven()
        {
            Assert.Equal(7, WideMenuLayout.SCENE_COLUMNS);
            Assert.Equal(3, WideMenuLayout.Rows(SCENES, WideMenuLayout.SCENE_COLUMNS));

            //Across and then down, in the list's order: the eighth scene opens the second row
            Assert.Equal((6, 0), WideMenuLayout.RowMajor(6, WideMenuLayout.SCENE_COLUMNS));
            Assert.Equal((0, 1), WideMenuLayout.RowMajor(7, WideMenuLayout.SCENE_COLUMNS));
            Assert.Equal((6, 2), WideMenuLayout.RowMajor(SCENES - 1, WideMenuLayout.SCENE_COLUMNS));
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
    }
}
