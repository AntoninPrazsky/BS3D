using Myra.Graphics2D.UI;
using System.Collections.Generic;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// What Play opens: the level set's entries in <b>play order</b>, which is the order the file lists them
    /// in and not a directory listing. Each carries what the set says about it — the budget, the ceiling —
    /// and what the player has made of it: the best star rating earned, or the star total the entry is still
    /// locked behind (#92, #111). A locked entry is shown rather than hidden, because a gate the player
    /// cannot see is a campaign that appears to simply end.
    /// <para>
    /// It is a page rather than a jump straight into the first level because a campaign is a list the player
    /// chooses from — and because the alternative, showing it only when the set has more than one entry, makes
    /// the flow depend on the data and surprises the player the day a second level is authored.
    /// </para>
    /// </summary>
    internal sealed class LevelSelectPage : MenuPage
    {
        public LevelSelectPage(BS3DGame game) : base(game) { }

        /// <summary>
        /// What the page needs around the list — the heading with its star total and margin, the Back button,
        /// and the plate's padding top and bottom. In the same 2160p design units the rest of the menu is
        /// authored in.
        /// </summary>
        private const int LIST_SURROUNDINGS = 530;

        //The rows, kept so Refresh can write the live state onto them: which entries the star total has
        //unlocked moves between two showings of this page (a level was just cleared), while the tree itself
        //is only rebuilt on a resize. Cleared and refilled by BuildTree, since a rebuild makes new widgets.
        private readonly List<Button> _entryButtons = new();
        private readonly List<Label> _entryTitles = new();
        private readonly List<Label> _entryNotes = new();
        private Label _totalStars;

        protected override Widget BuildTree()
        {
            _entryButtons.Clear();
            _entryTitles.Clear();
            _entryNotes.Clear();

            VerticalStackPanel page = MenuColumn();
            page.Widgets.Add(ScreenHeading("LEVEL"));

            //The campaign's star total, under the heading: the currency the locks below are weighed in, so
            //the number to compare their "Unlocks at" against is on the same screen. Inter, for the glyph.
            _totalStars = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 26),
            };
            page.Widgets.Add(_totalStars);

            //The entries scroll; the heading and Back do not — a campaign is as long as it is, and the way
            //out of the page must not be the thing that scrolled off the bottom of it
            VerticalStackPanel column = MenuColumn();

            int count = Game.LevelCount;

            if (count == 0)
            {
                //A missing or broken set is not fatal anywhere else either — the game falls back to the
                //built-in cluster — so the picker offers that rather than an empty list and no way forward
                column.Widgets.Add(MenuButton("Built-in level", () => Game.StartGameAt(0)));
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    //Captured per iteration, not off the loop variable's final value
                    int index = i;

                    Button button = MenuButton($"{i + 1}.  {Game.LevelDisplayName(i)}", () => Game.StartGameAt(index), out Label title);
                    column.Widgets.Add(button);

                    //The line under the button: the entry's rules and best stars, or what still locks it —
                    //written by Refresh, which is what knows the current totals
                    Label note = new()
                    {
                        Font = FontSmall,
                        TextColor = BS3DGame.MENU_TEXT_DIM,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    };
                    column.Widgets.Add(note);

                    _entryButtons.Add(button);
                    _entryTitles.Add(title);
                    _entryNotes.Add(note);
                }
            }

            page.Widgets.Add(MenuScroll(column, LIST_SURROUNDINGS));
            page.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(page));
        }

        /// <summary>
        /// Writes what has moved since the page was last up: a level cleared elsewhere raises the star total,
        /// which can open entries and change their notes — while the tree they are written onto only changes
        /// on a resize. Disabling the button is the whole of a lock's mechanics: Myra does not press a
        /// disabled button and the pad walk skips it (<c>CollectNavEntries</c> collects enabled ones), so
        /// there is no path left on which a locked level can be started.
        /// </summary>
        internal override void Refresh()
        {
            //The tree may not exist yet: the page is only built when it is first shown
            if (_totalStars == null) return;

            _totalStars.Text = $"★ {Game.TotalStars}";
            _totalStars.Visible = Game.LevelCount > 0;

            for (int i = 0; i < _entryButtons.Count; i++)
            {
                bool unlocked = Game.IsLevelUnlocked(i);
                int stars = Game.LevelStars(i);

                _entryButtons[i].Enabled = unlocked;

                //A locked entry is present but visibly not an offer: the title drops to the dim grey the
                //palette keeps for asides, which on this menu IS the disabled state — emphasis is brightness,
                //never hue (see the palette comment in BS3DGame.Menu.cs).
                _entryTitles[i].TextColor = unlocked ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;

                //Under an open entry, the earned stars lead the rules — and lead only once there are any, so
                //an untouched campaign is not eleven rows of hollow glyphs. A locked one says its price
                //instead of rules it cannot be played under yet.
                _entryNotes[i].Text = !unlocked
                    ? $"Unlocks at {Game.LevelMinStars(i)} ★"
                    : stars > 0
                        ? $"{StarText(stars)}   {Game.LevelRulesText(i)}"
                        : Game.LevelRulesText(i);
            }
        }
    }
}
