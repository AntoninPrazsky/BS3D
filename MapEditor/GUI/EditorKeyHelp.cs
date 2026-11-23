using System.Collections.Generic;

namespace MapEditor.GUI
{
    /// <summary>The groups the editor's key help is drawn in, in the order they are drawn.</summary>
    public enum KeyGroup
    {
        File,
        Help,
        Selector,
        Ball,
        Camera,
        Scene,
        Window,
    }

    /// <summary>
    /// One line of the editor's key help: the key or keys as a person reads them on the keyboard, and what they do.
    /// Several bindings can share one line (the four arrows are one "Arrows" line).
    /// </summary>
    public sealed record KeyHelpRow(KeyGroup Group, string Keys, string Text);

    /// <summary>
    /// What the map editor's key help says (#1209). It is the authority for the screen's wording; the bindings in
    /// <c>MapEditor.Initialize</c> each name the line that documents them, so a key cannot be bound without one,
    /// and <see cref="All"/> is what is drawn (the tests hold every line here to being in it).
    /// <para>
    /// <b>The file keys come first and are always on screen</b>, folded or not: the help used to be one 39-line
    /// block in binding order with load and save as its last five lines, below the bottom edge of every window, and
    /// the owner could not find how to open a level. Dropping a file on the window is named on the open line.
    /// </para>
    /// <para>
    /// Plain strings and no device, so <c>Tests/BS3D.Tests</c> compiles it in. The font has Latin-1 and Latin
    /// Extended-A only: an en dash or an ellipsis here draws as a question mark.
    /// </para>
    /// </summary>
    public static class EditorKeyHelp
    {
        /// <summary>
        /// The colour names, ordered by <c>BallType</c> value (index = value - 1). One copy in the editor: the
        /// colour line below and the editor's on-screen answer to a colour key both read it.
        /// </summary>
        public static readonly string[] BallTypeNames =
        {
            "red", "green", "blue", "white", "cyan", "magenta", "yellow", "black",
            "orange", "brown", "silver", "navy blue", "olive green",
        };

        //How many colours have a number key of their own (NumPad1-9); the rest are reached by cycling
        public const int DIRECT_COLOUR_KEYS = 9;

        public static readonly KeyHelpRow Open = new(KeyGroup.File, "F2", "Open a map or level, or drop one on the window");
        public static readonly KeyHelpRow SaveLevel = new(KeyGroup.File, "F4", "Save a level (map, scene, sky, material)");
        public static readonly KeyHelpRow SaveMap = new(KeyGroup.File, "F1", "Save the map alone (no scene or sky)");
        public static readonly KeyHelpRow NewMap = new(KeyGroup.File, "F3", "New map (choose the play field's size)");

        public static readonly KeyHelpRow ToggleHelp = new(KeyGroup.Help, "H", "Show or hide the other keys");

        public static readonly KeyHelpRow MoveSelector = new(KeyGroup.Selector, "Arrows", "Move the selector forward, back, left, right");
        public static readonly KeyHelpRow MoveSelectorVertically = new(KeyGroup.Selector, "PgUp / PgDn", "Move the selector up, down");
        public static readonly KeyHelpRow PutBall = new(KeyGroup.Selector, "Space", "Put a ball of the colour and kind below");
        public static readonly KeyHelpRow RemoveBall = new(KeyGroup.Selector, "Delete", "Remove the ball");
        public static readonly KeyHelpRow FillMap = new(KeyGroup.Selector, "N", "Fill the whole play field with balls");
        public static readonly KeyHelpRow ClearMap = new(KeyGroup.Selector, "M", "Clear the whole play field");

        public static readonly KeyHelpRow PickColour = new(KeyGroup.Ball, "Num 1-9", ColourLine());
        public static readonly KeyHelpRow CycleColour = new(KeyGroup.Ball, "Num + / -",
            $"Next or previous of all {BallTypeNames.Length} colours");
        public static readonly KeyHelpRow CycleKind = new(KeyGroup.Ball, "K", "Next kind of ball (named at the top)");
        public static readonly KeyHelpRow CycleStyle = new(KeyGroup.Ball, "L", "Next material of all balls (named at the top)");

        public static readonly KeyHelpRow Fly = new(KeyGroup.Camera, "W A S D", "Fly forward, left, back, right");
        public static readonly KeyHelpRow FlyVertically = new(KeyGroup.Camera, "Q / E", "Fly down, up (Shift: faster)");
        //Each press toggles look and a hold pans on top of it (CameraInputHelper), so a pan also leaves look flipped:
        //the line says both rather than promise a pan that leaves look alone
        public static readonly KeyHelpRow MouseLook = new(KeyGroup.Camera, "Right mouse", "Mouse look on or off at each press; pan while held");
        public static readonly KeyHelpRow Views = new(KeyGroup.Camera, "1-6", "Six views that frame the whole field");
        public static readonly KeyHelpRow ResetCamera = new(KeyGroup.Camera, "R", "Reset the camera");

        public static readonly KeyHelpRow CycleScene = new(KeyGroup.Scene, "V", "Next scene (named at the top)");
        public static readonly KeyHelpRow CycleSky = new(KeyGroup.Scene, "B", "Next sky dome");
        //The three view switches (#1210): what the game draws round the field, each on or off, in the order of the keys. One
        //line for the three, as the arrows are one: three more lines took the help into a second column at 1600 x 900. A view,
        //not data: F4 writes the same level either way, and the status line names what a press turned on or off.
        public static readonly KeyHelpRow ToggleSetting = new(KeyGroup.Scene, "G / I / C", "Game's sky, island, ceiling glass: on or off");

        public static readonly KeyHelpRow Screenshot = new(KeyGroup.Window, "F8", "Save a screenshot");
        public static readonly KeyHelpRow Fullscreen = new(KeyGroup.Window, "F11", "Fullscreen or windowed");
        public static readonly KeyHelpRow HideText = new(KeyGroup.Window, "F12", "Hide or show all text");
        public static readonly KeyHelpRow Quit = new(KeyGroup.Window, "Esc", "Quit");

        /// <summary>Every line, in the order drawn: grouped, each group's lines together, the file keys first.</summary>
        public static readonly KeyHelpRow[] All =
        {
            Open, SaveLevel, SaveMap, NewMap,
            ToggleHelp,
            MoveSelector, MoveSelectorVertically, PutBall, RemoveBall, FillMap, ClearMap,
            PickColour, CycleColour, CycleKind, CycleStyle,
            Fly, FlyVertically, MouseLook, Views, ResetCamera,
            CycleScene, CycleSky, ToggleSetting,
            Screenshot, Fullscreen, HideText, Quit,
        };

        /// <summary>What a group's heading says, or null for a group drawn without one.</summary>
        public static string Heading(KeyGroup group) => group switch
        {
            KeyGroup.File => "FILE",
            KeyGroup.Help => null,
            KeyGroup.Selector => "SELECTOR",
            KeyGroup.Ball => "BALLS",
            KeyGroup.Camera => "CAMERA",
            KeyGroup.Scene => "SCENE",
            KeyGroup.Window => "WINDOW",
            _ => group.ToString().ToUpperInvariant(),
        };

        /// <summary>Whether a group stays on screen when the help is folded: the file keys and the key that unfolds it.</summary>
        public static bool IsPinned(KeyGroup group) => group is KeyGroup.File or KeyGroup.Help;

        /// <summary>The lines drawn in one state: all of them, or only the pinned groups' when folded.</summary>
        public static IReadOnlyList<KeyHelpRow> Visible(bool expanded)
        {
            if (expanded) return All;

            List<KeyHelpRow> pinned = new();
            foreach (KeyHelpRow row in All) if (IsPinned(row.Group)) pinned.Add(row);
            return pinned;
        }

        //"Colour of the next ball: red, green, ..., orange" in key order, off the name table, so a renamed colour cannot leave the help behind
        private static string ColourLine()
        {
            string names = string.Join(", ", BallTypeNames, 0, DIRECT_COLOUR_KEYS);
            return $"Colour of the next ball: {names}";
        }
    }
}
