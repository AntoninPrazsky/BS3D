using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.Scoring;
using System;
using Color = Microsoft.Xna.Framework.Color;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// How to play, what the words on the result page mean, what the odd balls do, and which key does what
    /// (#427) — the reference a player reads once, beside About rather than inside it.
    /// <para>
    /// <b>It is several pages behind one menu entry</b>, which is the owner's own framing and is forced by the
    /// material: six subjects that each want a screen and share no shape. They are walked with Previous and Next,
    /// or sideways on the arrows, the D-pad or the left stick (<see cref="PageSideways"/>).
    /// </para>
    /// <para>
    /// <b>Each page's body scrolls</b> (#606): the owner's 3840×1600 cut the Scoring page off top and bottom with
    /// Back off the screen, and any shorter window is worse. The body sits in the shared <c>MenuScroll</c>, with the
    /// heading, the page counter, Previous/Next and Back outside it so the way out never scrolls away. The body is
    /// text with no entry in it for the focus cursor to walk to, so the wheel scrolls it (Myra's own), and so do
    /// Page Up/Page Down and the pad's right stick (<see cref="OnScrollAxis"/>).
    /// </para>
    /// <para>
    /// <b>⚠ The worked example is COMPUTED, not written.</b> Every number on the scoring page comes out of
    /// <see cref="ScoreKeeper"/>'s own constants at build time — the ten a matched ball is worth, the twenty an
    /// orphaned one is, the streak's step and cap, and the unused shot's value as the real formula derives it
    /// from a level's own size and budget. A hand-written example that looked plausible would be wrong the
    /// first time the scoring is tuned and nothing would say so; this one cannot drift, because there is no
    /// second copy of the arithmetic to drift from.
    /// </para>
    /// <para>
    /// <b>The controls page draws real keycaps</b> out of PromptFont, the face the tutorial's cards already
    /// use, rather than letters in drawn boxes. #463's ruling is that the controls paragraph leaves About; this
    /// is where it lands.
    /// </para>
    /// </summary>
    internal sealed class HelpPage : MenuPage
    {
        private const int TEXT_WIDTH = 1860;

        //The two walking buttons stand side by side, where every other entry on a page is one column wide
        private const int WALK_BUTTON_WIDTH = 490;
        private const int WALK_BUTTON_GAP = 20;

        //PromptFont's own glyphs, the tutorial's constants exactly (Tutorial.cs) — one face, one set of
        //codepoints, so a key drawn here and the same key drawn on a card cannot come out different
        private const string KEY_W = "Ｗ", KEY_A = "Ａ", KEY_S = "Ｓ", KEY_D = "Ｄ";
        private const string MOUSE = "⟼", MOUSE_LEFT = "⟵", MOUSE_RIGHT = "⟶";

        //The level the worked example is played on: the campaign's first, so a reader can go and reproduce it.
        //Its two figures are the ones the scoring actually reads — the balls it hangs and the shots it allows.
        private const string EXAMPLE_LEVEL = "One";
        private const int EXAMPLE_BALLS = 385;
        private const int EXAMPLE_SHOTS = 30;

        private static readonly string[] TITLES =
        {
            "HOW IT IS PLAYED",
            "SCORING",
            "THE ODD BALLS",
            "THE CAMPAIGN",
            "THE GLASS AND THE LINE",
            "CONTROLS",
        };

        private int _page;

        //What sits around the scroller, in 2160p design units: the heading, the page counter, the walking row,
        //Back and the plate's padding (#606)
        private const int BODY_SURROUNDINGS = 820;

        //Design pixels a second the right stick scrolls the body at full tilt, and one Page Up/Down step's share
        //of the scroller's own height
        private const int STICK_SCROLL_SPEED = 2400;
        private const float PAGE_KEY_SHARE = 0.8f;

        //Built with the tree, so a turn can put the focus back on the button that made it and the pad can scroll
        private Button _previous, _next;
        private ScrollViewer _body;

        //A turn waiting for Update to put the rebuilt tree up — +1/-1, 0 for none (see Turn)
        private int _pendingTurn;
        private float _scrollCarry;

        public HelpPage(BS3DGame game) : base(game) { }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading(TITLES[_page]));
            column.Widgets.Add(Caption($"{_page + 1} of {TITLES.Length}"));

            VerticalStackPanel body = MenuColumn();

            switch (_page)
            {
                case 0: BuildPlaying(body); break;
                case 1: BuildScoring(body); break;
                case 2: BuildBalls(body); break;
                case 3: BuildCampaign(body); break;
                case 4: BuildCeiling(body); break;
                default: BuildControls(body); break;
            }

            _body = MenuScroll(body, BODY_SURROUNDINGS);
            column.Widgets.Add(_body);

            HorizontalStackPanel walk = new()
            {
                Spacing = Scaled(WALK_BUTTON_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            //Added left to right, the order the pad walks them in
            Button previous = MenuButton("Previous", () => Turn(-1));
            previous.Width = Scaled(WALK_BUTTON_WIDTH);
            previous.Enabled = _page > 0;
            walk.Widgets.Add(previous);
            _previous = previous;

            Button next = MenuButton("Next", () => Turn(1));
            next.Width = Scaled(WALK_BUTTON_WIDTH);
            next.Enabled = _page < TITLES.Length - 1;
            walk.Widgets.Add(next);
            _next = next;

            column.Widgets.Add(walk);
            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
        }

        /// <summary>
        /// Walks to another page. The index is clamped rather than wrapped: a reference is read front to back, and
        /// a Next that silently returns to page one reads as having lost the player's place.
        /// <para>
        /// ⚠ <b>Invalidating the tree is not enough, and was all this did until #606</b>: a rebuilt tree only
        /// reaches the screen when something puts <see cref="MenuPage.Root"/> into the desktop, which the stack
        /// does on a push, a pop and a resize — so Next and Previous changed nothing until the page was left and
        /// entered again (every <c>help=</c> capture worked, because that goes in before the page is first shown).
        /// The rebuild is left to <see cref="Update"/> rather than done here, because a click arrives from inside
        /// Myra's own processing of the desktop whose root it would be replacing.
        /// </para>
        /// </summary>
        private void Turn(int by)
        {
            int wanted = Math.Clamp(_page + by, 0, TITLES.Length - 1);
            if (wanted == _page) return;

            _page = wanted;
            _pendingTurn = by;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_pendingTurn == 0) return;

            int by = _pendingTurn;
            _pendingTurn = 0;
            _scrollCarry = 0f;

            InvalidateTree();

            //The button that turned the page keeps the focus, as the level picker's chapter turn does — or its
            //neighbour, when the turn reached the end and disabled it
            Game.RebuildPage(this, () => by > 0 ? (_next.Enabled ? _next : _previous) : (_previous.Enabled ? _previous : _next));
        }

        /// <summary>Sideways on the arrows, the D-pad or the left stick turns the page (#606).</summary>
        internal override bool PageSideways(int direction)
        {
            int before = _page;
            Turn(direction);
            return _page != before;
        }

        /// <summary>
        /// Scrolls the body by the pad's right stick (<paramref name="stick"/>, up positive) and by Page Up/Page
        /// Down (<paramref name="pageSteps"/>, down positive), over <paramref name="elapsed"/> seconds (#606).
        /// </summary>
        internal override void OnScrollAxis(float stick, int pageSteps, float elapsed)
        {
            if (_body == null) return;

            float pixels = -stick * Scaled(STICK_SCROLL_SPEED) * elapsed + _scrollCarry
                           + pageSteps * _body.ActualBounds.Height * PAGE_KEY_SHARE;

            int whole = (int)pixels;
            _scrollCarry = pixels - whole;
            if (whole == 0) return;

            Point at = _body.ScrollPosition;
            _body.ScrollPosition = new Point(at.X, Math.Clamp(at.Y + whole, 0, Math.Max(0, _body.ScrollMaximum.Y)));
        }

        /// <summary>
        /// Opens on a stated page, 1-based as the page prints itself — the <c>help=</c> argument's way in
        /// (#427). Out of range is clamped rather than refused: a capture script asking for page nine should
        /// photograph the last page, not crash the game it was pointed at.
        /// </summary>
        internal void ShowPage(int page)
        {
            int wanted = Math.Clamp(page - 1, 0, TITLES.Length - 1);
            if (wanted == _page) return;

            _page = wanted;
            InvalidateTree();
        }

        private void BuildPlaying(VerticalStackPanel column)
        {
            column.Widgets.Add(Paragraph(
                "A cluster of coloured balls hangs from a sheet of glass above a stone island. You stand under "
                + "it with a cannon and fire balls up into it."));
            column.Widgets.Add(Paragraph(
                "A shot sticks where it lands. Land it so that three or more of the same colour touch, and the "
                + "whole group falls — and everything that was hanging from it falls with it. Those two "
                + "sentences are the game: the group you match is usually the smaller half of what you bring "
                + "down."));
            column.Widgets.Add(Paragraph(
                "So the shot to look for is rarely the biggest clump of your colour. It is the one ball that is "
                + "holding a lot of other balls up."));
            column.Widgets.Add(Paragraph(
                "You have a fixed number of balls per level, shown at the bottom left. The queue at the bottom "
                + "right shows what you are about to fire and what comes after it, so you can plan two shots "
                + "ahead. Clear every ball that can be matched and the level is done."));
        }

        private void BuildScoring(VerticalStackPanel column)
        {
            //Every figure below is ScoreKeeper's own. Written through string interpolation rather than typed
            //out, so a tuned constant cannot leave a stale number standing on this page.
            column.Widgets.Add(Paragraph(
                $"Matched — the balls in the group you completed. {ScoreKeeper.MatchedBallPoints} points each."));
            column.Widgets.Add(Paragraph(
                $"Orphaned — the balls that fell because the group was the last thing holding them up. "
                + $"{ScoreKeeper.OrphanedBallPoints} points each, twice a matched one: bringing a mass down by "
                + "cutting its anchor is the better shot and the score says so."));
            column.Widgets.Add(Paragraph(
                $"Streak — every landing that scores raises your multiplier by {ScoreKeeper.MultiplierStep}, up "
                + $"to ×{ScoreKeeper.MaxMultiplier}. It multiplies everything that shot scores. A shot that "
                + "matches nothing puts it back to ×1."));
            column.Widgets.Add(Paragraph(
                "Shots unused — what you did not need. Worth a great deal, and worth more on a big level: one "
                + $"unused shot is priced at {ScoreKeeper.UnusedShotWorthInShots} average shots of that level, "
                + $"i.e. {ScoreKeeper.UnusedShotWorthInShots} × {ScoreKeeper.MatchedBallPoints} × (balls ÷ "
                + "shots)."));

            //The example, arithmetic and all. UnusedShotValue is the formula ScoreKeeper itself applies when a
            //level states both its size and its budget, which every shipped level does.
            int unusedShotValue =
                ScoreKeeper.UnusedShotWorthInShots * ScoreKeeper.MatchedBallPoints * EXAMPLE_BALLS / EXAMPLE_SHOTS;

            int firstShot = (5 * ScoreKeeper.MatchedBallPoints + 3 * ScoreKeeper.OrphanedBallPoints) * 1;
            int secondShot = (4 * ScoreKeeper.MatchedBallPoints) * (1 + ScoreKeeper.MultiplierStep);
            int thirdShot = (3 * ScoreKeeper.MatchedBallPoints + 11 * ScoreKeeper.OrphanedBallPoints)
                            * ScoreKeeper.MaxMultiplier;
            int bonus = 6 * unusedShotValue;

            column.Widgets.Add(Caption("A worked example, on " + EXAMPLE_LEVEL
                + $" — {EXAMPLE_BALLS} balls, {EXAMPLE_SHOTS} shots"));
            column.Widgets.Add(Paragraph(
                $"Shot 1 matches 5 and orphans 3, at ×1:  5×{ScoreKeeper.MatchedBallPoints} + "
                + $"3×{ScoreKeeper.OrphanedBallPoints} = {firstShot}."));
            column.Widgets.Add(Paragraph(
                $"Shot 2 matches 4 and orphans nothing, now at ×{1 + ScoreKeeper.MultiplierStep}:  "
                + $"4×{ScoreKeeper.MatchedBallPoints} × {1 + ScoreKeeper.MultiplierStep} = {secondShot}."));
            column.Widgets.Add(Paragraph(
                $"Shot 3 cuts an anchor — 3 matched, 11 orphaned — at the ×{ScoreKeeper.MaxMultiplier} cap:  "
                + $"(3×{ScoreKeeper.MatchedBallPoints} + 11×{ScoreKeeper.OrphanedBallPoints}) × "
                + $"{ScoreKeeper.MaxMultiplier} = {thirdShot}. Three balls matched, and the shot is worth more "
                + "than the first two together."));
            column.Widgets.Add(Paragraph(
                $"Finish with 6 shots in hand and each is worth {unusedShotValue} here:  6 × {unusedShotValue} "
                + $"= {bonus}, which on this level is more than the three shots above put together. Efficiency "
                + "outweighs a streak you could farm by taking longer — that is deliberate."));
        }

        private void BuildBalls(VerticalStackPanel column)
        {
            column.Widgets.Add(Paragraph(
                "Most balls are ordinary. Four kinds are not, and you will meet them as the campaign goes on."));
            column.Widgets.Add(Paragraph(
                "Stone — colourless, and it cannot be matched. It never falls to a colour, only to losing "
                + "whatever was holding it up. A wall of stone is a wall you have to go around."));
            column.Widgets.Add(Paragraph(
                "Glass — clear, with no colour of its own yet. The first ball that lands against it colours it, "
                + "and from that moment it is an ordinary ball of that colour. It is a cell you get to choose."));
            column.Widgets.Add(Paragraph(
                "Bomb — dark, and it beats harder than the cluster around it. Set it off and it destroys its "
                + "neighbours outright, colour regardless."));
            column.Widgets.Add(Paragraph(
                "Zap — dark and flickering, with hard bright arcs across it. It fires along a colour rather "
                + "than a shape."));
            column.Widgets.Add(Paragraph(
                "And one that arrives in your cannon rather than in the cluster: the wildcard, a ball that "
                + "never settles on a colour while it is loaded. It becomes whatever completes a group where it "
                + "lands. The last chapter hands them out."));
            column.Widgets.Add(Caption("Power-ups are coming later and are not in the game yet."));
        }

        private void BuildCampaign(VerticalStackPanel column)
        {
            //Read from the level set rather than typed: the page said 120 levels in 12 chapters and 236 stars for
            //the last level while the game shipped 130, 13 and 256 (#606)
            column.Widgets.Add(Paragraph(
                $"The campaign is {Game.LevelCount} levels in {Game.BlockCount} chapters. Each chapter is ten levels against one backdrop, "
                + "and each has its own shape of puzzle — a chapter of towers, a chapter of things hidden "
                + "inside other things, a chapter of mathematics."));
            column.Widgets.Add(Paragraph(
                "Every level you clear is rated from one to four stars. One star is clearing it at all; the "
                + "rest are score, and the thresholds are multiples of what the level is worth if you simply "
                + "finish it. Four stars means you cleared it well under the shots it allows."));
            column.Widgets.Add(Paragraph(
                "Stars are also a key. Later levels ask for a total across everything you have played, so a "
                + $"chapter you rushed can be gone back to. The last level of the campaign asks for {Game.LevelMinStars(Game.LevelCount - 1)}."));
            column.Widgets.Add(Paragraph(
                "A cleared level presents a cup: bronze, silver, gold, or — for four stars — crystal. It is the "
                + "same information as the stars, handed over as an object."));
        }

        private void BuildCeiling(VerticalStackPanel column)
        {
            column.Widgets.Add(Paragraph(
                "The cluster does not hang still for ever. Every few shots the glass it hangs from steps down, "
                + "and the whole cluster comes with it. The panel on the left of the screen shows the field, "
                + "the glass at the top of it, and how far down it has come."));
            column.Widgets.Add(Paragraph(
                "At the bottom of that panel is a red line. If the cluster reaches it, the level is lost. You "
                + "get a moment's warning — the floor lights up — and under the line you have about a second "
                + "to drop something and lift the cluster back."));
            column.Widgets.Add(Paragraph(
                "So the clock is your own shots. Every shot brings the glass nearer, whether it matched "
                + "anything or not, which is the other reason a miss is expensive. Clearing from the bottom "
                + "buys height; clearing from the top does not."));
        }

        private void BuildControls(VerticalStackPanel column)
        {
            column.Widgets.Add(KeyLine(MOUSE, "Move the mouse to aim"));
            column.Widgets.Add(KeyLine(MOUSE_LEFT, "Click to fire — Space fires too"));
            column.Widgets.Add(KeyLine(MOUSE_RIGHT, "Hold to look down the barrel, for the precise shot"));
            column.Widgets.Add(KeyLine(KEY_A + KEY_D, "Walk the gun round the field"));
            column.Widgets.Add(KeyLine(KEY_W + KEY_S, "Step in and out — closer means a steeper shot"));
            column.Widgets.Add(Paragraph(
                "Escape pauses. F11 is fullscreen, F12 saves a screenshot, F10 hides the frame-rate counter."));
            column.Widgets.Add(Caption("A gamepad plays all of it too; the first chapter teaches the bindings "
                + "for whichever you are holding."));
        }

        /// <summary>
        /// One control: the key as PromptFont draws it, and what it does beside it. The glyph is its own label
        /// in its own face, because PromptFont fills its em with a rounded keycap while a line of body text
        /// stands well short of one — set as a single string they would not sit on the same line.
        /// </summary>
        private Widget KeyLine(string glyph, string what)
        {
            HorizontalStackPanel line = new()
            {
                Spacing = Scaled(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 26),
            };

            line.Widgets.Add(new Label
            {
                Text = glyph,
                Font = Game.MenuFontPrompt,
                TextColor = BS3DGame.MENU_TEXT,
                VerticalAlignment = VerticalAlignment.Center,
            });

            line.Widgets.Add(new Label
            {
                Text = what,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                VerticalAlignment = VerticalAlignment.Center,
            });

            return line;
        }

        private Label Caption(string text) => new()
        {
            Text = text,
            Font = FontSmall,
            TextColor = BS3DGame.MENU_TEXT_DIM,
            Wrap = true,
            Width = Scaled(TEXT_WIDTH),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ScaledThickness(0, 0, 0, 30),
        };

        private Label Paragraph(string text) => new()
        {
            Text = text,
            Font = FontSmall,
            TextColor = BS3DGame.MENU_TEXT_BODY,
            Wrap = true,
            Width = Scaled(TEXT_WIDTH),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ScaledThickness(0, 0, 0, 30),
        };
    }
}
