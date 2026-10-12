using BS3D.Audio;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// The game's original procedural score, kept as a small player since #443 once generated recordings replaced it as the
    /// level music (play and pause, the next piece, and a spectrum), and what the game is, what it is built on and what it
    /// was made with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four pages behind one menu entry (#807)</b>, the owner's "nobody reads that much text": the two columns of #463 had
    /// grown to some 160 words of running text plus the privacy sentence, in one face, one colour, with nothing to say where
    /// to start. The player comes first, because the first thing a player sees should be the thing they can use; then what
    /// the game is with the privacy line, what it is built on and what it was made with. The words are
    /// <see cref="AboutCopy"/>'s, where a test holds them to the typesetting rules and the credits to the full list.
    /// </para>
    /// <para>
    /// <b>The pages turn the way the picker's chapters and High Scores' do</b>: an arrow either side of the page's name with
    /// the pad's shoulder under it (<see cref="MenuPage.ArrowWithShoulder"/>, #848), and the arrow keys, the D-pad and the
    /// stick (<see cref="PageSideways"/>). <b>Except on the player's row</b>: Play and Next stand side by side, so while the
    /// cursor is on one of them ◀ ▶ walk the two (the host's row rule, #805) and only the shoulders turn the page
    /// (<see cref="ShoulderTurn"/>) — the issue's "decide it once".
    /// </para>
    /// <para>
    /// <b>Every page stands in the same frame</b>, the biggest page's, measured (<see cref="BuildFrame"/>, Help's pattern
    /// since #729), so the arrows, Back and the plate's edges stay where they are as the pages turn. No scroller: the
    /// tallest page with the heading, the pager and Back fits the design height with room to spare.
    /// </para>
    /// </remarks>
    internal sealed class AboutPage : MenuPage
    {
        //The arrows: the level picker's figures (#847) - a square plate of the picker's size round a triangle 1.2 of the
        //heading's capitals, drawn as a shape so it sits on its plate to the pixel
        private const int ARROW_SIZE = 160;
        private const float ARROW_OF_CAP = 1.2f;
        private const int ARROW_GAP = 20;

        //Between the page's name and the "2 of 4" under it: ArrowWithShoulder's own gap between an arrow and its shoulder,
        //so the counter stands level with the two shoulders
        private const int COUNTER_GAP = 10;

        //Between a credit's name and what it does, in a credit list: a little over two spaces of the small face, so the two
        //columns read as columns and not as one line
        private const int CREDIT_GAP = 60;

        //Two entries side by side where every other entry on a page is one column wide, so each is a little under half of
        //one - the size of Help's walking buttons
        private const int PLAYER_BUTTON_WIDTH = 490;
        private const int PLAYER_BUTTON_GAP = 20;

        //The spectrum is the page's mass now that it has a page of its own: the prose measure wide and nearly twice as tall
        //as it was in its column (240 there)
        private const int VISUALIZER_HEIGHT = 440;
        private const int VISUALIZER_BAR_GAP = 10;

        //Between the repository's entry and the way to the author: the menu column's own spacing
        private const int LINK_GAP = 26;

        private const string REPOSITORY = "https://github.com/AntoninPrazsky/BS3D";

        //Which page is up, 0-based, and a turn waiting for Update to put the rebuilt tree up (+1/-1, 0 for none)
        private int _page;
        private int _pendingTurn;

        //The current tree's entries: the arrows, so a turn can put the focus back on the one that made it, and the player's
        //two, so the arrows know when they are the player's row's
        private Button _previousArrow, _nextArrow, _play, _next;

        //Where the pad's and the arrow keys' cursor stands (null while the pointer drives)
        private Button _focused;

        private Label _pieceLabel;
        private MediaGlyph _playGlyph;

        //What the two labels last said, as one number, so their text is written when the player's state changes
        //rather than every frame — a Label's Text setter re-measures, and a string built per frame is garbage.
        private int _shownState = -1;

        public AboutPage(BS3DGame game) : base(game) { }

        protected override Widget BuildTree()
        {
            //The frame first: the pager is as wide as it is, so the arrows stand over its edges
            Panel frame = BuildFrame();

            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("ABOUT"));

            //Added in this order, and that IS the walk's order: the arrows, the page's own entries, Back
            column.Widgets.Add(BuildPager(frame.Width.GetValueOrDefault()));
            column.Widgets.Add(frame);
            column.Widgets.Add(BackButton());

            //The same tag, in the same corner, as the front end's — About is where a player looks for which version
            //they run, once they have found the entry
            return ScreenRoot(Plate(column), VersionTag());
        }

        /// <summary>
        /// The page's name between two arrows, with "1 of 4" under it level with the shoulders under the arrows. The arrows
        /// stop at the ends rather than wrap, as Help's Previous and Next do: a page read front to back that silently comes
        /// round to the first reads as having lost the player's place. An arrow with nowhere to go keeps its slab and dims
        /// its triangle, as High Scores' do, and its shoulder dims with it.
        /// </summary>
        private Widget BuildPager(int width)
        {
            HorizontalStackPanel row = new() { Spacing = Scaled(ARROW_GAP), HorizontalAlignment = HorizontalAlignment.Center };

            _previousArrow = PageArrow(MediaGlyph.Symbol.Back, -1, _page > 0);
            _nextArrow = PageArrow(MediaGlyph.Symbol.Play, 1, _page < AboutCopy.TITLES.Length - 1);

            //Aligned to the top, so the name stands level with the arrows' slabs and the counter level with the shoulders
            VerticalStackPanel name = new()
            {
                Width = Math.Max(0, width - 2 * (Scaled(ARROW_SIZE) + Scaled(ARROW_GAP))),
                Spacing = Scaled(COUNTER_GAP),
                VerticalAlignment = VerticalAlignment.Top,
            };

            //A label given a width of its own draws its text from the left, so each is centred in a block of the width
            Panel title = new() { Height = Scaled(ARROW_SIZE) };
            title.Widgets.Add(new Label
            {
                Text = AboutCopy.TITLES[_page],
                Font = FontSection,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            name.Widgets.Add(title);

            //As tall as the shoulder's glyph under an arrow (a prompt is the body face's line tall), the counter centred in it
            Panel counter = new() { Height = FontBody.LineHeight };
            counter.Widgets.Add(new Label
            {
                Text = $"{_page + 1} of {AboutCopy.TITLES.Length}",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            name.Widgets.Add(counter);

            //What turns the page, under the two arrows (#799, #848): the pad's shoulders, and nothing for the keyboard — an
            //arrow key beside an arrow only repeated it (the owner, 2026-10-12)
            row.Widgets.Add(ArrowWithShoulder(_previousArrow, PromptGlyph.PAD_LB));
            row.Widgets.Add(name);
            row.Widgets.Add(ArrowWithShoulder(_nextArrow, PromptGlyph.PAD_RB));

            return row;
        }

        /// <summary>
        /// One page arrow, the level picker's (#847): a <c>MenuTile</c>, so a real menu entry with the menu's brushes, click
        /// and pad activation, round a triangle drawn as a shape. Off at an end, with its slab kept and its triangle in the
        /// aside grey: Myra's own disabled slab reads as a lock, which is not what "no further this way" means.
        /// </summary>
        private Button PageArrow(MediaGlyph.Symbol symbol, int direction, bool enabled)
        {
            int height = (int)MathF.Round(CapHeightOf(FontHeading) * ARROW_OF_CAP);
            MediaGlyph triangle = new(symbol, height, height) { Tint = enabled ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM };

            Button arrow = Game.MenuTile(triangle, () => Turn(direction), ARROW_SIZE, ARROW_SIZE);
            arrow.DisabledBackground = BS3DGame.MENU_BUTTON_BRUSH;
            arrow.Enabled = enabled;

            return arrow;
        }

        /// <summary>
        /// The page's body in a frame as big as the biggest page's, every page measured in turn (Help's pattern, #729). Myra
        /// lays out only what is shown, so without this the plate would shrink round a short page and the arrows and Back
        /// would jump on every turn. The page that is up is built last, so the player's labels and buttons the page keeps
        /// are the shown page's and not a measured copy's.
        /// </summary>
        private Panel BuildFrame()
        {
            Point huge = new(int.MaxValue / 2, int.MaxValue / 2);
            Point size = Point.Zero;

            for (int page = 0; page < AboutCopy.TITLES.Length; page++)
            {
                if (page == _page) continue;

                Point measured = BuildBody(page).Measure(huge);
                size = new Point(Math.Max(size.X, measured.X), Math.Max(size.Y, measured.Y));
            }

            _pieceLabel = null;
            _playGlyph = null;
            _play = _next = null;

            Widget body = BuildBody(_page);
            Point shown = body.Measure(huge);
            size = new Point(Math.Max(size.X, shown.X), Math.Max(size.Y, shown.Y));

            Panel frame = new() { Width = size.X, Height = size.Y, HorizontalAlignment = HorizontalAlignment.Center };
            frame.Widgets.Add(body);

            return frame;
        }

        private Widget BuildBody(int page)
        {
            //Each block carries the space under it (#806), so the stack adds none of its own. Against the frame's left edge
            //and top, so the text stands where it does on every page and the rag runs into the right
            VerticalStackPanel body = new()
            {
                Spacing = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            switch (page)
            {
                case 0:
                    BuildPlayer(body);
                    body.HorizontalAlignment = HorizontalAlignment.Center;
                    break;
                case 1: BuildGame(body); break;
                case 2: BuildBuiltOn(body); break;
                default: BuildMadeWith(body); break;
            }

            return body;
        }

        /// <summary>
        /// Page one, the original procedural score's player: its one line, the piece's name, the spectrum at the prose
        /// measure's width, and Play and Next — the visualizer and its two buttons one unit, as since #463.
        /// </summary>
        private void BuildPlayer(VerticalStackPanel body)
        {
            ProceduralJukebox jukebox = Game.Jukebox;

            //One line that stands alone over the player, so centred (a line on its own is, prose is not)
            body.Widgets.Add(new Label
            {
                Text = AboutCopy.SCORE,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, ParagraphGap),
            });

            //Written now and not only by ShowPlayer, so the page measures at the height it is drawn at (an empty label
            //measures as nothing, and the frame would come out a line short)
            _pieceLabel = new Label
            {
                Text = jukebox != null ? PieceText(jukebox) : string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 20),
            };
            body.Widgets.Add(_pieceLabel);

            //The LED meter, and on the game's birthday the rainbow (#764, the owner's easter egg): no label and no
            //setting, the day the gun wears its hat (BS3DGame.Birthday), so the game has one birthday and the
            //birthday launch argument shows both
            body.Widgets.Add(new MusicVisualizer(jukebox, ProseMeasure, Scaled(VISUALIZER_HEIGHT), Scaled(VISUALIZER_BAR_GAP))
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 24),
                Palette = Game.Birthday ? MusicVisualizer.VisualizerPalette.Spectrum : MusicVisualizer.VisualizerPalette.Led,
            });

            HorizontalStackPanel controls = new()
            {
                Spacing = Scaled(PLAYER_BUTTON_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            //Added left to right, the order the pad walks them in (CollectNavEntries follows insertion)
            //Symbols, not words (#784, the owner: "one symbol is enough"): ▶ / ❚❚ on the one, ⏭ on the other
            _playGlyph = ButtonGlyph(MediaGlyph.Symbol.Play);
            _play = MenuButton(_playGlyph, () => Game.Jukebox?.PlayPause());
            _play.Width = Scaled(PLAYER_BUTTON_WIDTH);
            controls.Widgets.Add(_play);

            _next = MenuButton(ButtonGlyph(MediaGlyph.Symbol.Next), () => Game.Jukebox?.Next());
            _next.Width = Scaled(PLAYER_BUTTON_WIDTH);
            controls.Widgets.Add(_next);

            body.Widgets.Add(controls);
        }

        /// <summary>
        /// Page two, what the game is, and the online scores' privacy line under it as an aside: the Settings page's own
        /// sentence, from the one place both read it (#548), so the two pages cannot say different things about the same data.
        /// </summary>
        private void BuildGame(VerticalStackPanel body)
        {
            body.Widgets.Add(Paragraph(AboutCopy.Game(BS3DGame.GAME_TITLE), ProseMeasure));
            body.Widgets.Add(Aside(Game.Online.PrivacySentence, ProseMeasure));
        }

        /// <summary>Page three, what it is built on: the one thing about it a player can see, then the libraries and typefaces.</summary>
        private void BuildBuiltOn(VerticalStackPanel body)
        {
            body.Widgets.Add(Paragraph(AboutCopy.BUILT_ON, ProseMeasure));
            body.Widgets.Add(CreditList(AboutCopy.LIBRARIES));

            //TODO(#463): MonoGame's and BepuPhysics' logos go here, beside their credit above — waiting on the
            //owner's own artwork, which this issue does not carry. An image widget each, through the content
            //pipeline the splash logo already goes through (Content.mgcb, PremultiplyAlpha), plus each
            //project's logo-usage terms recorded beside the asset the way the fonts' OFL files sit beside the
            //TTFs — see the issue for the full note.
        }

        /// <summary>
        /// Page four, what it was made with, then the repository — a real menu entry (#463) rather than a label a pointer
        /// could reach and a pad could not — and the way to the author (#813): the main menu has no ninth entry (#704), and
        /// About is where a player looks for who made the game. Extras has it too; in a level it is the pause menu's.
        /// </summary>
        private void BuildMadeWith(VerticalStackPanel body)
        {
            body.Widgets.Add(Paragraph(AboutCopy.MADE_WITH, ProseMeasure));
            body.Widgets.Add(CreditList(AboutCopy.GENERATED));

            VerticalStackPanel links = new()
            {
                Spacing = Scaled(LINK_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            links.Widgets.Add(MenuButton("github.com/AntoninPrazsky/BS3D", OpenRepository));
            links.Widgets.Add(Game.NoteEntry("about"));
            body.Widgets.Add(links);
        }

        /// <summary>
        /// A credit list: what is credited in the bright type, what it does beside it in the body grey, two columns at the
        /// small face's prose pitch. The names' column is as wide as the widest name, measured; what it does is set as prose
        /// in what is left of the measure, so a long one breaks as a paragraph does.
        /// </summary>
        private Widget CreditList(AboutCopy.Credit[] credits)
        {
            //A pixel over the widest name, so a name measured to a fraction is never laid out a pixel short of itself
            int nameWidth = 0;
            foreach (AboutCopy.Credit credit in credits)
            {
                nameWidth = Math.Max(nameWidth, (int)MathF.Ceiling(FontSmall.MeasureString(credit.Name).X) + 1);
            }

            int gap = Scaled(CREDIT_GAP);

            VerticalStackPanel list = new()
            {
                Spacing = ProseLeading,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, ParagraphGap),
            };

            foreach (AboutCopy.Credit credit in credits)
            {
                HorizontalStackPanel line = new() { Spacing = gap };

                line.Widgets.Add(new Label
                {
                    Text = credit.Name,
                    Font = FontSmall,
                    TextColor = BS3DGame.MENU_TEXT,
                    Width = nameWidth,
                    VerticalAlignment = VerticalAlignment.Top,
                });
                line.Widgets.Add(Prose(credit.Role, ProseMeasure - nameWidth - gap, BS3DGame.MENU_TEXT_BODY, ProseLeading,
                    new Thickness(0)));

                list.Widgets.Add(line);
            }

            return list;
        }

        /// <summary>
        /// Hands the repository's address to whatever the desktop registered for it (#463) — the click a real
        /// menu entry gives for free, which the address written out as a plain label never could.
        /// </summary>
        private static void OpenRepository()
        {
            try
            {
                //UseShellExecute hands the address to whatever the desktop registered for it; without it .NET
                //would try to run the URL as an executable and throw every time.
                Process.Start(new ProcessStartInfo { FileName = REPOSITORY, UseShellExecute = true });
            }
            catch (Exception)
            {
                //A machine with no handler registered throws rather than doing nothing, and an About page is
                //no place to take the game down. The button's own text is the address, to be read or copied.
            }
        }

        /// <summary>
        /// Walks to another page, clamped at the ends, and says whether it moved — what decides whether the menu ticks.
        /// <para>
        /// ⚠ The rebuild is left to <see cref="Update"/>, as Help's is (#606): a click arrives from inside Myra's own
        /// processing of the desktop whose root the rebuild replaces, and invalidating the tree alone puts nothing on
        /// screen until something puts <see cref="MenuPage.Root"/> into the desktop again.
        /// </para>
        /// </summary>
        private bool Turn(int by)
        {
            int wanted = Math.Clamp(_page + by, 0, AboutCopy.TITLES.Length - 1);
            if (wanted == _page) return false;

            _page = wanted;
            _pendingTurn = by;
            return true;
        }

        /// <summary>
        /// Opens on a stated page, 1-based as the page counts itself: the host's way in (#807) — page one from the main
        /// menu, so About always opens on the player, and any page for the <c>about=&lt;n&gt;</c> argument. Out of range is
        /// clamped.
        /// </summary>
        internal void ShowPage(int page)
        {
            _pendingTurn = 0;

            int wanted = Math.Clamp(page - 1, 0, AboutCopy.TITLES.Length - 1);
            if (wanted == _page) return;

            _page = wanted;
            InvalidateTree();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_pendingTurn != 0)
            {
                int by = _pendingTurn;
                _pendingTurn = 0;

                InvalidateTree();

                //The arrow that way keeps the focus, as Help's walking button does — or the other, when the turn reached
                //the end and switched it off
                Game.RebuildPage(this, () => by > 0
                    ? (_nextArrow.Enabled ? _nextArrow : _previousArrow)
                    : (_previousArrow.Enabled ? _previousArrow : _nextArrow));
            }

            ShowPlayer();
        }

        internal override void NavFocusChanged(Button focused) => _focused = focused;

        /// <summary>
        /// The cursor arrives on Play on the player's page — the thing the page is for — and on the arrow onwards on any
        /// other, as Help's arrives on Next.
        /// </summary>
        internal override Button NavArrival => _play ?? (_nextArrow != null && _nextArrow.Enabled ? _nextArrow : _previousArrow);

        /// <summary>
        /// Left or right on the arrow keys, the D-pad or the stick turns the page — but not while the cursor is on Play or
        /// Next: there they walk the player's row (#805), the host's rule for a sideways press a page declines.
        /// </summary>
        internal override bool PageSideways(int direction)
        {
            if (_focused != null && (_focused == _play || _focused == _next)) return false;
            return Turn(direction);
        }

        /// <summary>The pad's shoulders turn the page from anywhere on it, the player's row included.</summary>
        internal override bool ShoulderTurn(int direction) => Turn(direction);

        internal override void Refresh()
        {
            //A rebuilt tree starts at its authored defaults, so the labels are written again whatever they said
            _shownState = -1;
            ShowPlayer();
        }

        /// <summary>
        /// The piece stops when the page is left: the easter egg belongs to the page, and the game's own music,
        /// which stepped aside for it, comes back (see <see cref="GameMusic.Yielding"/>). Turning to another of About's
        /// pages is not leaving it, so a piece goes on playing under the text.
        /// </summary>
        public override void Leave()
        {
            Game.Jukebox?.Stop();
            base.Leave();
        }

        private void ShowPlayer()
        {
            ProceduralJukebox jukebox = Game.Jukebox;
            if (jukebox == null || _pieceLabel == null || _playGlyph == null) return;

            int action = jukebox.IsComposing ? 1 : jukebox.IsPlaying ? 2 : jukebox.HoldsPiece ? 3 : 0;
            int state = jukebox.PieceNumber * 4 + action;

            if (state == _shownState) return;
            _shownState = state;

            _pieceLabel.Text = PieceText(jukebox);

            //Pause while it sounds, play otherwise (resume is play again). While a piece is still being composed the
            //button cannot act yet (ProceduralJukebox.PlayPause waits for the render), so it shows play in the aside
            //grey - the words "Composing..." it used to say were the one state a symbol has to show by tone
            _playGlyph.Shape = action == 2 ? MediaGlyph.Symbol.Pause : MediaGlyph.Symbol.Play;
            _playGlyph.Tint = action == 1 ? BS3DGame.MENU_TEXT_DIM : BS3DGame.MENU_TEXT;
        }

        private static string PieceText(ProceduralJukebox jukebox) =>
            $"{jukebox.PieceName} · {jukebox.PieceNumber} / {ProceduralJukebox.PieceCount}";
    }
}
