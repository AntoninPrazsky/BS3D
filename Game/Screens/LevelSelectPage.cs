using Myra.Graphics2D.UI;
using System.Collections.Generic;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// What Play opens: the level set's entries in <b>play order</b> — as a grid of tiles rather than a
    /// scrolling list (#91), so a player sees the shape of the whole campaign at a glance: which levels are
    /// cleared and to how many stars, which are still locked and at what price. A tile carries the number,
    /// the name and the player's stars (or the lock); what does <b>not</b> fit a tile — the entry's rules,
    /// a lock's arithmetic — moves to the one detail line under the grid, spoken for whichever tile the
    /// pointer or the focus cursor stands on.
    /// <para>
    /// A locked entry is shown rather than hidden, because a gate the player cannot see is a campaign that
    /// appears to simply end. It is a page rather than a jump straight into the first level because a
    /// campaign is a list the player chooses from — and because the alternative, showing it only when the
    /// set has more than one entry, makes the flow depend on the data and surprises the player the day a
    /// second level is authored.
    /// </para>
    /// </summary>
    internal sealed class LevelSelectPage : MenuPage
    {
        public LevelSelectPage(BS3DGame game) : base(game) { }

        //The tile: wide enough for the longest shipped name in the small face with room to spare, tall
        //enough for the number, the name and the star row without crowding — in the menu's 2160p design
        //units, resolved at build time like every other size here.
        private const int TILE_COLUMNS = 4;
        private const int TILE_WIDTH = 440;
        private const int TILE_HEIGHT = 300;

        /// <summary>
        /// What the page needs around the grid — the heading with the star total, the detail line, the Back
        /// button and the plate's padding — so the scroller only ever gives up what those actually take.
        /// </summary>
        private const int LIST_SURROUNDINGS = 700;

        //The tiles' widgets, kept so Refresh can write the live state onto them: which entries the star
        //total has unlocked moves between two showings of this page (a level was just cleared), while the
        //tree itself is only rebuilt on a resize. Cleared and refilled by BuildTree, since a rebuild makes
        //new widgets.
        private readonly List<Button> _tiles = new();
        private readonly List<Label> _tileNumbers = new();
        private readonly List<Label> _tileNames = new();
        private readonly List<Label> _tileStarsEarned = new();
        private readonly List<Label> _tileStarsRest = new();
        private Label _totalStars, _detail;

        //Which entry the detail line is speaking for, -1 for none. Held so a MouseLeft can tell "the pointer
        //left the tile being described" from "the pointer left a tile the focus cursor had already replaced".
        private int _detailIndex = -1;

        protected override Widget BuildTree()
        {
            _tiles.Clear();
            _tileNumbers.Clear();
            _tileNames.Clear();
            _tileStarsEarned.Clear();
            _tileStarsRest.Clear();
            _detailIndex = -1;

            VerticalStackPanel page = MenuColumn();
            page.Widgets.Add(ScreenHeading("LEVEL"));

            //The campaign's star total, under the heading: the currency the locks below are weighed in, so
            //the number to compare their price against is on the same screen. Inter, for the glyph.
            _totalStars = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 26),
            };
            page.Widgets.Add(_totalStars);

            int count = Game.LevelCount;

            if (count == 0)
            {
                //A missing or broken set is not fatal anywhere else either — the game falls back to the
                //built-in cluster — so the picker offers that rather than an empty grid and no way forward
                page.Widgets.Add(MenuButton("Built-in level", () => Game.StartGameAt(0)));
            }
            else
            {
                //The grid scrolls; the heading, the detail line and Back do not — a campaign is as long as
                //it is, and the way out of the page must not be the thing that scrolled off the bottom
                Grid grid = new()
                {
                    ColumnSpacing = Scaled(26),
                    RowSpacing = Scaled(26),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                for (int c = 0; c < TILE_COLUMNS; c++)
                    grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

                for (int i = 0; i < count; i++)
                {
                    if (i % TILE_COLUMNS == 0)
                        grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

                    Button tile = BuildTile(i);

                    Grid.SetColumn(tile, i % TILE_COLUMNS);
                    Grid.SetRow(tile, i / TILE_COLUMNS);
                    grid.Widgets.Add(tile);
                }

                page.Widgets.Add(MenuScroll(grid, LIST_SURROUNDINGS));
            }

            //The one detail line (#91): the rules a tile has no room for, spoken for the tile under the
            //pointer or under the focus cursor. A fixed height, so the page does not jump as it fills and
            //empties.
            _detail = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Height = Scaled(70),
                Margin = ScaledThickness(0, 13, 0, 13),
            };
            page.Widgets.Add(_detail);

            page.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(page));
        }

        /// <summary>
        /// One tile: the number loud, the name under it, the player's stars (or the lock's price) at the
        /// bottom. Built over the host's <see cref="MenuPage.Game"/>.<c>MenuTile</c>, so it is a real menu
        /// entry — same brushes, same click sound, same pad activation — merely tile-shaped.
        /// </summary>
        private Button BuildTile(int index)
        {
            VerticalStackPanel content = new()
            {
                Spacing = Scaled(6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Label number = new()
            {
                Text = (index + 1).ToString(),
                Font = Game.MenuFontHeading,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            content.Widgets.Add(number);

            Label name = new()
            {
                Text = Game.LevelDisplayName(index),
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            content.Widgets.Add(name);

            //Stars, the lock's price, or empty — written by Refresh. Inter for the ★/☆ glyphs, which the
            //display face above does not carry.
            //
            //Two labels rather than one string, because since #139 a rating's earned run and its hollow
            //remainder are different colours — the tier's and the empty grey — and one Label is one colour.
            //The lock's price goes in the first and leaves the second empty: it is a sentence, not a rating.
            HorizontalStackPanel starRow = new() { HorizontalAlignment = HorizontalAlignment.Center };

            Label starsEarned = new() { Font = FontSmall, TextColor = BS3DGame.MENU_TEXT_DIM };
            Label starsRest = new() { Font = FontSmall, TextColor = BS3DGame.STAR_EMPTY };

            starRow.Widgets.Add(starsEarned);
            starRow.Widgets.Add(starsRest);
            content.Widgets.Add(starRow);

            Button tile = Game.MenuTile(content, () => Game.StartGameAt(index), TILE_WIDTH, TILE_HEIGHT);

            //The pointer's half of the detail line; the focus cursor's half is NavFocusChanged. Leaving only
            //clears a description this tile still owns — the cursor may already have replaced it.
            tile.MouseEntered += (_, _) => ShowDetail(index);
            tile.MouseLeft += (_, _) => { if (_detailIndex == index) ShowDetail(-1); };

            _tiles.Add(tile);
            _tileNumbers.Add(number);
            _tileNames.Add(name);
            _tileStarsEarned.Add(starsEarned);
            _tileStarsRest.Add(starsRest);

            return tile;
        }

        /// <summary>
        /// Writes what has moved since the page was last up: a level cleared elsewhere raises the star total,
        /// which can open tiles and change their bottom lines — while the tree they are written onto only
        /// changes on a resize. Disabling the tile is the whole of a lock's mechanics: Myra does not press a
        /// disabled button and the pad walk skips it (<c>CollectNavEntries</c> collects enabled ones), so
        /// there is no path left on which a locked level can be started.
        /// </summary>
        internal override void Refresh()
        {
            //The tree may not exist yet: the page is only built when it is first shown
            if (_totalStars == null) return;

            _totalStars.Text = $"★ {Game.TotalStars}";
            _totalStars.Visible = Game.LevelCount > 0;

            ShowDetail(-1);

            for (int i = 0; i < _tiles.Count; i++)
            {
                bool unlocked = Game.IsLevelUnlocked(i);
                int stars = Game.LevelStars(i);

                _tiles[i].Enabled = unlocked;

                //A locked tile is present but visibly not an offer: its type drops to the dim grey the
                //palette keeps for asides, which on this menu IS the disabled state — emphasis is brightness,
                //never hue (see the palette comment in BS3DGame.Menu.cs).
                _tileNumbers[i].TextColor = unlocked ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
                _tileNames[i].TextColor = unlocked ? BS3DGame.MENU_TEXT_BODY : BS3DGame.MENU_TEXT_DIM;

                //The bottom line earns its place or stays empty: stars once there are any (an untouched
                //campaign is not a wall of hollow glyphs), the price on a lock, nothing on an open level
                //not yet cleared. The earned run carries the tier's colour, the same one the result screen
                //struck those stars in — so a level the player took to gold still reads gold here.
                bool rated = unlocked && stars > 0;

                _tileStarsEarned[i].Text = !unlocked
                    ? $"Locked · {Game.LevelMinStars(i)} {STAR_FILLED}"
                    : rated ? StarsEarned(stars) : string.Empty;
                _tileStarsEarned[i].TextColor = rated ? BS3DGame.StarTierColor(stars) : BS3DGame.MENU_TEXT_DIM;

                _tileStarsRest[i].Text = rated ? StarsRemaining(stars) : string.Empty;
            }
        }

        /// <summary>The focus cursor's half of the detail line — see <c>BS3DGame.ApplyNavHighlight</c>.</summary>
        internal override void NavFocusChanged(Button focused)
        {
            //The Back button (or another page's entry, should the stack change under the call) describes
            //nothing; IndexOf's -1 is exactly the "clear it" the detail line wants then.
            ShowDetail(focused == null ? -1 : _tiles.IndexOf(focused));
        }

        /// <summary>
        /// Points the detail line at one entry: the rules the tile has no room for, or a lock's full
        /// arithmetic — the price next to what the player actually holds.
        /// </summary>
        private void ShowDetail(int index)
        {
            _detailIndex = index;

            if (_detail == null) return;

            if (index < 0 || index >= Game.LevelCount)
            {
                _detail.Text = string.Empty;
                return;
            }

            _detail.Text = Game.IsLevelUnlocked(index)
                ? $"{Game.LevelDisplayName(index)} — {Game.LevelRulesText(index)}"
                : $"{Game.LevelDisplayName(index)} — unlocks at {Game.LevelMinStars(index)} ★, you have {Game.TotalStars}";
        }
    }
}
