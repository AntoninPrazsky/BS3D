using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What a press on the level picker's tile does for each hand (#1152): the desktop's click, Enter and the pad's A play
    /// the level at once, as they always have; on the phone's layout a finger's first tap selects the tile, so its rules
    /// and boards are seen in the side panel first, and a tap on the selected tile starts it.
    /// </summary>
    public class PickerTapTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public void Without_the_panel_every_press_starts_the_level(bool finger, bool selectedAlready)
        {
            //A monitor's page has no panel for a first tap to fill: a finger on a touch laptop starts the level too
            int level = 4;
            Assert.Equal(PickerTap.Outcome.Start, PickerTap.OnTile(panel: false, finger, selectedAlready ? level : 7, level));
        }

        [Fact]
        public void On_the_panel_a_pointer_a_key_or_a_pad_still_starts_the_level_at_once() =>
            //The pointer, the cursor and the pad selected the tile by standing on it; their press is the start
            Assert.Equal(PickerTap.Outcome.Start, PickerTap.OnTile(panel: true, finger: false, selected: 7, level: 4));

        [Fact]
        public void A_fingers_first_tap_on_the_panel_selects_the_tile() =>
            Assert.Equal(PickerTap.Outcome.Select, PickerTap.OnTile(panel: true, finger: true, selected: 7, level: 4));

        [Fact]
        public void A_fingers_tap_on_the_selected_tile_starts_it() =>
            Assert.Equal(PickerTap.Outcome.Start, PickerTap.OnTile(panel: true, finger: true, selected: 4, level: 4));

        [Fact]
        public void A_first_tap_with_nothing_selected_selects() =>
            Assert.Equal(PickerTap.Outcome.Select, PickerTap.OnTile(panel: true, finger: true, selected: -1, level: 0));

        [Fact]
        public void Two_taps_on_one_tile_select_it_and_then_start_it()
        {
            //The page's own sequence: the first outcome is applied (the level becomes the selected one) before the second tap
            int selected = 2;
            const int tapped = 5;

            PickerTap.Outcome first = PickerTap.OnTile(true, true, selected, tapped);
            if (first == PickerTap.Outcome.Select) selected = tapped;
            PickerTap.Outcome second = PickerTap.OnTile(true, true, selected, tapped);

            Assert.Equal(PickerTap.Outcome.Select, first);
            Assert.Equal(PickerTap.Outcome.Start, second);
        }

        [Fact]
        public void The_pointer_selects_for_a_mouse_and_not_for_a_finger()
        {
            Assert.True(PickerTap.HoverSelects(finger: false));
            Assert.False(PickerTap.HoverSelects(finger: true));
        }

        [Theory]
        //The frontier on the chapter: it is the one the panel offers, so one tap on it or on Play starts it
        [InlineData(10, 19, 14, 14)]
        [InlineData(10, 19, 10, 10)]
        [InlineData(10, 19, 19, 19)]
        //Elsewhere, behind or ahead, and a finished campaign (the frontier past the set's end): the chapter's first level
        [InlineData(10, 19, 9, 10)]
        [InlineData(10, 19, 20, 10)]
        [InlineData(0, 9, 140, 0)]
        public void A_chapter_put_up_offers_its_frontier_or_its_first_level(int first, int last, int frontier, int expected) =>
            Assert.Equal(expected, PickerTap.DefaultSelection(first, last, frontier));
    }
}
