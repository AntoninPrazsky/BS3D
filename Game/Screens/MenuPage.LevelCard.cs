using Myra.Graphics2D.UI;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    internal abstract partial class MenuPage
    {
        /// <summary>
        /// What a level is, in a card's few lines (#1152, #1184): which level of the set it is, its name in the heading face, the
        /// player's stars on it and their best, and the rules it is played under one to a line (or, for a locked level, what
        /// keeps it shut). The level picker's side panel on the phone stands it over its boards and its Play; the pause on the
        /// phone stands it beside its entries with the run's own lines under it, so the two cards cannot drift apart. Every
        /// word is <see cref="PickerCopy"/>'s.
        /// </summary>
        protected sealed class LevelCard
        {
            /// <summary>Between the card's lines, in design units.</summary>
            internal const int LINE_GAP = 14;

            private readonly MenuPage _page;
            private readonly Label _caption, _name, _starsEarned, _starsRest, _best, _rules;

            //The caption's row and the stars' run, hidden with the best and the rules for the built-in level (Show)
            private readonly Grid _captionRow;
            private readonly HorizontalStackPanel _stars;

            /// <param name="page">The page the card stands on: its faces, its scale and its game.</param>
            /// <param name="width">The card's width in pixels at the layout in force.</param>
            /// <param name="captionBeside">What stands opposite the caption, flush right (the picker's star total), or null.</param>
            internal LevelCard(MenuPage page, int width, Widget captionBeside = null)
            {
                _page = page;
                Root = new VerticalStackPanel { Spacing = page.Scaled(LINE_GAP), Width = width };

                //Which level of the set it is, and opposite it whatever the page puts there
                _captionRow = new Grid { Width = width };
                _captionRow.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
                _captionRow.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
                _caption = new Label { Font = page.FontSmall, TextColor = BS3DGame.MENU_TEXT_DIM };
                _captionRow.Widgets.Add(_caption);
                if (captionBeside != null)
                {
                    captionBeside.Margin = new Myra.Graphics2D.Thickness(0);
                    captionBeside.HorizontalAlignment = HorizontalAlignment.Right;
                    Grid.SetColumn(captionBeside, 1);
                    _captionRow.Widgets.Add(captionBeside);
                }
                Root.Widgets.Add(_captionRow);

                //The heading face, cut with the one-character ellipsis where a name would not fit (none of the shipped ones)
                _name = new Label
                {
                    Font = page.FontHeading,
                    TextColor = BS3DGame.MENU_TEXT,
                    Width = width,
                    AutoEllipsisMethod = FontStashSharp.RichText.AutoEllipsisMethod.Character,
                    AutoEllipsisString = "…",
                };
                Root.Widgets.Add(_name);

                //The result page's rating face: the earned run in the tier's colour and the rest hollow (#139)
                _stars = new HorizontalStackPanel();
                _starsEarned = new Label { Font = page.FontStars, TextColor = BS3DGame.MENU_TEXT_DIM };
                _starsRest = new Label { Font = page.FontStars, TextColor = BS3DGame.STAR_EMPTY };
                _stars.Widgets.Add(_starsEarned);
                _stars.Widgets.Add(_starsRest);
                Root.Widgets.Add(_stars);

                _best = new Label { Font = page.FontBody, TextColor = BS3DGame.MENU_TEXT_BODY };
                Root.Widgets.Add(_best);

                //The rules, one to a line, or the lock as prose (Show)
                _rules = new Label
                {
                    Font = page.FontSmall,
                    TextColor = BS3DGame.MENU_TEXT_BODY,
                    Width = width,
                    Wrap = true,
                    VerticalSpacing = page.ProseLeading,
                    Margin = page.ScaledThickness(0, LINE_GAP, 0, 0),
                };
                Root.Widgets.Add(_rules);
            }

            /// <summary>The card's lines, for the page to stand where it wants and to add its own under.</summary>
            internal VerticalStackPanel Root { get; }

            /// <summary>
            /// Writes level <paramref name="level"/> (0-based) onto the card: open, with its stars, its best and its rules; or
            /// locked (<paramref name="open"/> false), with what keeps it shut, which the page words (<paramref name="lockProse"/>).
            /// A level outside the set (the built-in one a game without a readable set plays, which the pause shows) has its
            /// name and nothing else: no place in a set to number, no record and no rules. On a change of level, never per frame.
            /// </summary>
            internal void Show(int level, bool open, string lockProse = null)
            {
                BS3DGame game = _page.Game;
                _name.Text = game.LevelDisplayName(level).ToUpperInvariant();

                bool inSet = game.LevelInSet(level);
                _captionRow.Visible = _stars.Visible = _best.Visible = _rules.Visible = inSet;
                if (!inSet) return;

                int stars = open ? game.LevelStars(level) : 0;
                _caption.Text = PickerCopy.PanelCaption(level + 1);

                _starsEarned.Text = StarsEarned(stars);
                _starsEarned.TextColor = BS3DGame.StarTierColor(stars);
                _starsRest.Text = StarsRemaining(stars);

                int best = stars > 0 ? game.LevelBestScore(level) : 0;
                _best.Text = !open ? PickerCopy.PANEL_LOCKED
                    : stars > 0 ? (best > 0 ? PickerCopy.PanelBest(best) : PickerCopy.PANEL_OLD_RULES)
                    : game.LevelSkipped(level) ? PickerCopy.TILE_SKIPPED
                    : PickerCopy.PANEL_NOT_CLEARED;
                _best.TextColor = open && stars > 0 ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;

                //The rules one to a line, as a list rather than the detail line's sentence; a lock's arithmetic as prose
                SetProse(_rules, open ? PickerCopy.PanelRules(game.LevelRulesText(level)) : lockProse ?? string.Empty);
            }
        }
    }
}
