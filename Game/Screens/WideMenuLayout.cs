namespace BS3D.Screens
{
    /// <summary>
    /// The figures and the arithmetic the phone's layouts of four pages are built from (#1153): the Scene page as a grid of
    /// tiles, the Jukebox in three columns, About and High Scores with a rail of their switches beside what they show. A
    /// phone held landscape is about 2.2 times as wide as it is tall, and the owner's ruling (2026-11-18) is to use the sides
    /// rather than scroll: "It is widescreen, so we can use the space at the sides too - better to use it than to have
    /// frequent scrolling." Every one of these layouts is the enlarged interface's (<c>BS3DGame.UiScale</c> over 1), so a
    /// monitor's pages stay as they were.
    /// <para>
    /// Plain arithmetic and design units, kept out of the pages so a test can hold the figures to what they promise (a
    /// target a finger can hit, twenty-one scenes in three rows of seven, ten levels in two columns of five) without a
    /// graphics device: <c>WideMenuLayoutTests</c>.
    /// </para>
    /// </summary>
    internal static class WideMenuLayout
    {
        /// <summary>
        /// The least a phone's target is tall, in millimetres (#1150's audit): about what a fingertip covers. The scene tiles
        /// and the boards' rows are drawn at least this tall on the X300 Ultra, where the menu's own entries are about 6.5.
        /// </summary>
        internal const float TOUCH_TARGET_MM = 9f;

        /// <summary>
        /// The scale the X300 Ultra draws the menus at (the front end's fit, which every page starts from): 1.31 on the phone
        /// itself (<c>docs/game-shell/menu-look.md</c>), 1.33 in the desktop's emulation of it (<c>inches=2.8</c> in a 3168x1440
        /// window). The lower, so a figure held to <see cref="TOUCH_TARGET_MM"/> by <see cref="Millimetres"/> holds on both.
        /// </summary>
        internal const float X300_MENU_SCALE = 1.31f;

        /// <summary>The X300 Ultra's screen held landscape: its height in pixels and in inches.</summary>
        internal const int X300_HEIGHT_PIXELS = 1440;
        internal const float X300_HEIGHT_INCHES = 2.8f;

        /// <summary>The 2160p design height every menu size is authored for (<c>BS3DGame.MENU_DESIGN_HEIGHT</c>'s figure).</summary>
        internal const int DESIGN_HEIGHT = 2160;

        /// <summary>Between a rail of a page's switches and what the page shows beside it, in design units.</summary>
        internal const int RAIL_GAP = 120;

        /// <summary>
        /// A scene's tile on the Scene page's grid, in design units: wide enough for the longest chapter's name in the small
        /// face ("The Silhouettes", 353 units) inside the tile's padding, and tall enough for the scene's name in the display
        /// face over that line with room to spare, and for a finger (<see cref="TOUCH_TARGET_MM"/>).
        /// </summary>
        internal const int SCENE_TILE_WIDTH = 440;
        internal const int SCENE_TILE_HEIGHT = 230;

        /// <summary>The Scene page's columns: twenty-one scenes in three rows of seven, which fit a phone's height whole.</summary>
        internal const int SCENE_COLUMNS = 7;

        /// <summary>Between two tiles, both ways, in design units: the level picker's own gap.</summary>
        internal const int TILE_GAP = 26;

        /// <summary>
        /// A level's row on the High Scores page in three lines (its name, the board's leader, the player's place), in design
        /// units: the leader's block's width (a sixteen-letter name beside its score), and tall enough for the three lines of
        /// the small face and for a finger (<see cref="TOUCH_TARGET_MM"/>).
        /// </summary>
        internal const int BOARD_ROW_WIDTH = 860;
        internal const int BOARD_ROW_HEIGHT = 230;

        /// <summary>The High Scores page's columns of rows: a chapter's ten levels in two columns of five.</summary>
        internal const int BOARD_COLUMNS = 2;

        /// <summary>How many rows <paramref name="count"/> entries take in <paramref name="columns"/> columns.</summary>
        internal static int Rows(int count, int columns) => columns <= 0 ? 0 : (count + columns - 1) / columns;

        /// <summary>
        /// Where entry <paramref name="index"/> stands in a grid filled row by row, <paramref name="columns"/> wide: the Scene
        /// page's tiles, in the campaign's order across and then down.
        /// </summary>
        internal static (int Column, int Row) RowMajor(int index, int columns) => (index % columns, index / columns);

        /// <summary>
        /// Where entry <paramref name="index"/> stands in columns filled top to bottom, <paramref name="rows"/> deep: the High
        /// Scores page's levels, one to five down the first column and six to ten down the second, as a list reads on.
        /// </summary>
        internal static (int Column, int Row) ColumnMajor(int index, int rows) => (index / rows, index % rows);

        /// <summary>
        /// A length in design units in millimetres on a screen <paramref name="heightPixels"/> tall and
        /// <paramref name="heightInches"/> tall, with the menus drawn at <paramref name="menuScale"/> times their design size
        /// for that height (a design unit is <c>menuScale × heightPixels / DESIGN_HEIGHT</c> pixels).
        /// </summary>
        internal static float Millimetres(int designUnits, float menuScale, int heightPixels, float heightInches) =>
            designUnits * menuScale * heightPixels / DESIGN_HEIGHT * (heightInches * 25.4f / heightPixels);
    }
}
