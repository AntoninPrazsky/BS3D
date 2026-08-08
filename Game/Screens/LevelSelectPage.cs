using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// What Play opens: the level set's entries in <b>play order</b>, which is the order the file lists them
    /// in and not a directory listing. Each carries what the set says about it — the budget, the gate, the
    /// ceiling — because a player choosing a level is choosing its rules as much as its layout.
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
        /// What the page needs around the list — the heading and its margin, the Back button, and the plate's
        /// padding top and bottom. In the same 2160p design units the rest of the menu is authored in.
        /// </summary>
        private const int LIST_SURROUNDINGS = 460;

        protected override Widget BuildTree()
        {
            VerticalStackPanel page = MenuColumn();
            page.Widgets.Add(ScreenHeading("LEVEL"));

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

                    column.Widgets.Add(MenuButton($"{i + 1}.  {Game.LevelDisplayName(i)}", () => Game.StartGameAt(index)));

                    column.Widgets.Add(new Label
                    {
                        Text = Game.LevelRulesText(i),
                        Font = FontSmall,
                        TextColor = BS3DGame.MENU_TEXT_DIM,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    });
                }
            }

            page.Widgets.Add(MenuScroll(column, LIST_SURROUNDINGS));
            page.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(page));
        }
    }
}
