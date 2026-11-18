namespace BS3D.Screens
{
    /// <summary>
    /// What a press on one of the level picker's tiles does, and which level the picker's side panel speaks for (#1152).
    /// Pure, so a test can hold the promises the page makes to each hand: the desktop's click, Enter and the pad's A play
    /// the level at once, as they always have; on a phone's layout a finger's first tap only <b>selects</b> the tile, so
    /// the panel beside the grid can show its rules, its best and its boards before anything starts, and a second tap
    /// on the same tile (or the panel's Play) starts it.
    /// </summary>
    internal static class PickerTap
    {
        /// <summary>What a press on a tile does.</summary>
        internal enum Outcome
        {
            /// <summary>The level starts.</summary>
            Start,

            /// <summary>The tile becomes the selected one and the panel speaks for it; nothing starts.</summary>
            Select,
        }

        /// <summary>
        /// A press on the tile of <paramref name="level"/>. Only a finger on the panel's layout selects first, and only a
        /// tile that is not the selected one already: without a panel there is nothing a first tap could show, and a
        /// pointer, a key or a pad has already shown the panel the level by hovering or standing on the tile.
        /// </summary>
        /// <param name="panel">Whether the page stands with its side panel (the phone's layout).</param>
        /// <param name="finger">Whether the menus' hand is a finger on the screen.</param>
        /// <param name="selected">The level the panel speaks for, or -1 for none.</param>
        /// <param name="level">The level whose tile was pressed.</param>
        internal static Outcome OnTile(bool panel, bool finger, int selected, int level) =>
            panel && finger && level != selected ? Outcome.Select : Outcome.Start;

        /// <summary>
        /// Whether the pointer resting on a tile selects it. Not for a finger: a phone's touch moves no pointer, and the
        /// desktop's stand-in for one (the <c>touch</c> argument with the mouse as the finger) must answer as the phone
        /// does, where only a tap selects. The pad's and the keys' cursor always selects, as the pointer does for a mouse.
        /// </summary>
        internal static bool HoverSelects(bool finger) => !finger;

        /// <summary>
        /// Which level the panel speaks for when a chapter is put up and nothing on it is chosen
        /// (<see cref="SelectionOnShow"/>): the campaign's frontier when it is in that chapter (the level the player is most
        /// likely about to play, so a single tap on it or on Play starts it), otherwise the chapter's first level.
        /// </summary>
        /// <param name="first">The chapter's first level.</param>
        /// <param name="last">The chapter's last level, inclusive.</param>
        /// <param name="frontier">The first level the player has neither cleared nor skipped; past the set's end once every
        /// level is behind them.</param>
        internal static int DefaultSelection(int first, int last, int frontier) =>
            frontier >= first && frontier <= last ? frontier : first;

        /// <summary>
        /// Which level the panel speaks for whenever the page writes a chapter: on every showing of the page (back from the
        /// level's board, from Settings, from a level played) and on a turn. The level chosen before stays chosen while it
        /// is on the chapter written and has not been locked since it was chosen (a progress reset, the unlock-all switch
        /// turned off); a locked level chosen to read its lock stays too. Otherwise, a turn to another chapter included,
        /// <see cref="DefaultSelection"/>. So the panel, the marked tile, Play and the Leaderboard button never move off a
        /// level the player picked because a page was put over the picker and taken off again.
        /// </summary>
        /// <param name="first">The chapter's first level.</param>
        /// <param name="last">The chapter's last level, inclusive.</param>
        /// <param name="frontier">As <see cref="DefaultSelection"/> takes it.</param>
        /// <param name="selected">The level chosen before, or -1 for none.</param>
        /// <param name="selectedWasOpen">Whether it was open when it was chosen.</param>
        /// <param name="selectedIsOpen">Whether it is open now.</param>
        internal static int SelectionOnShow(int first, int last, int frontier, int selected, bool selectedWasOpen,
            bool selectedIsOpen) =>
            selected >= first && selected <= last && (selectedIsOpen || !selectedWasOpen)
                ? selected
                : DefaultSelection(first, last, frontier);
    }
}
