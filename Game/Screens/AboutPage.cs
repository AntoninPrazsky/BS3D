using BS3D.Audio;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using Myra.Graphics2D.UI;
using Color = Microsoft.Xna.Framework.Color;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// What the game is, how it is played, and what it is built on — and, since #443, the game's original
    /// procedural score, kept as a small player once generated recordings replaced it as the level music: play
    /// and pause, the next piece, and a spectrum.
    /// </summary>
    internal sealed class AboutPage : MenuPage
    {
        private const int TEXT_WIDTH = 1860;

        //Two entries side by side where every other entry on a page is one column wide, so each is a little under
        //half of one and the row spans the same column the visualizer does.
        private const int PLAYER_BUTTON_WIDTH = 490;
        private const int PLAYER_BUTTON_GAP = 20;
        private const int VISUALIZER_HEIGHT = 240;
        private const int VISUALIZER_BAR_GAP = 10;

        private Label _pieceLabel;
        private Label _playLabel;

        //What the two labels last said, as one number, so their text is written when the player's state changes
        //rather than every frame — a Label's Text setter re-measures, and a string built per frame is garbage.
        private int _shownState = -1;

        public AboutPage(BS3DGame game) : base(game) { }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("ABOUT"));
            column.Widgets.Add(Paragraph(
                text: BS3DGame.GAME_TITLE + " is a 3D arcade puzzle. Aim, match, and collapse color clusters hanging beneath a glass ceiling. "
                                          + "Matching three or more drops them—severing everything anchored below."));
            column.Widgets.Add(Paragraph(
                text: "Controls: Mouse aims, Left Click or Space fires, Right Click leans along the barrel, "
                      + "A/D traverses the carriage, W/S adjusts depth, Esc pauses, F10 hides the FPS counter, "
                      + "F11 toggles fullscreen, F12 saves a screenshot. Gamepad: the right stick aims, "
                      + "the right trigger fires, the left trigger leans in, the left stick traverses and walks, "
                      + "Back pauses. The first chapter teaches all of this as you play."));
            column.Widgets.Add(Paragraph(
                text: "Built on MonoGame (DirectX 11) and BepuPhysics 2. Everything you see—from the spheres to the city—is procedural: "
                      + "no 3D models, only pure code. The music was generated locally with ACE-Step 1.5. "
                      + "Typefaces Anton and Inter (both SIL OFL 1.1)."));
            column.Widgets.Add(Link("github.com/AntoninPrazsky/BS3D", "https://github.com/AntoninPrazsky/BS3D"));

            column.Widgets.Add(Paragraph(
                text: "The game's original score was written as code too—oscillators and arrays, synthesized the moment it plays. "
                      + "It is still in here:"));

            ProceduralJukebox jukebox = Game.Jukebox;

            _pieceLabel = new Label
            {
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 20),
            };
            column.Widgets.Add(_pieceLabel);

            column.Widgets.Add(new MusicVisualizer(jukebox, ColumnWidth, Scaled(VISUALIZER_HEIGHT), Scaled(VISUALIZER_BAR_GAP))
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 24),
            });

            HorizontalStackPanel controls = new()
            {
                Spacing = Scaled(PLAYER_BUTTON_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            //Added left to right, the order the pad walks them in (CollectNavEntries follows insertion)
            Button play = MenuButton("Play", jukebox.PlayPause, out _playLabel);
            play.Width = Scaled(PLAYER_BUTTON_WIDTH);
            controls.Widgets.Add(play);

            Button next = MenuButton("Next", jukebox.Next);
            next.Width = Scaled(PLAYER_BUTTON_WIDTH);
            controls.Widgets.Add(next);

            column.Widgets.Add(controls);

            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
        }

        internal override void Refresh()
        {
            //A rebuilt tree starts at its authored defaults, so the labels are written again whatever they said
            _shownState = -1;
            ShowPlayer();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            ShowPlayer();
        }

        /// <summary>
        /// The piece stops when the page is left: the easter egg belongs to the page, and the game's own music,
        /// which stepped aside for it, comes back (see <see cref="GameMusic.Yielding"/>).
        /// </summary>
        public override void Leave()
        {
            Game.Jukebox?.Stop();
            base.Leave();
        }

        private void ShowPlayer()
        {
            ProceduralJukebox jukebox = Game.Jukebox;
            if (jukebox == null || _pieceLabel == null) return;

            int action = jukebox.IsComposing ? 1 : jukebox.IsPlaying ? 2 : jukebox.HoldsPiece ? 3 : 0;
            int state = jukebox.PieceNumber * 4 + action;

            if (state == _shownState) return;
            _shownState = state;

            _pieceLabel.Text = $"{jukebox.PieceName} · {jukebox.PieceNumber} / {ProceduralJukebox.PieceCount}";

            //ASCII dots: the display face is Anton, and a glyph it lacks is dropped without a word by FontStashSharp
            _playLabel.Text = action switch
            {
                1 => "Composing...",
                2 => "Pause",
                3 => "Resume",
                _ => "Play",
            };
        }

        /// <summary>
        /// The repository address, as something the pointer can open. Myra has no link widget, so this is a
        /// <see cref="Paragraph"/> with a touch handler — but sized to its own text rather than left at the
        /// prose width: the paragraphs are a fixed <see cref="TEXT_WIDTH"/> and centred, so a link keeping that
        /// width would light up and open the browser anywhere across the column, with nothing drawn to say why.
        /// It marks itself the way every other active thing in this menu does, by brightness and never by hue
        /// (see the palette in BS3DGame.Menu.cs): a step above the prose at rest, and the pointer lifts a slab
        /// under it — the same wash, off the same shared brush, that a menu entry answers with. Text alone
        /// could not carry the hover: at rest it is already MENU_TEXT, and the last eleven greys to white are
        /// not a step anyone sees. Pad and arrow keys do not reach it — the navigation walks the tree for
        /// buttons — which is why the address is written out in full, a thing to read before a thing to click.
        /// </summary>
        private Label Link(string text, string url)
        {
            Label label = Paragraph(text);

            //Shrink to the text: Paragraph is built for a column-wide block, and this has to be the hit area
            label.Width = null;
            label.Wrap = false;
            label.TextColor = BS3DGame.MENU_TEXT;

            //Room for the hover slab to sit off the glyphs rather than tight against them
            label.Padding = ScaledThickness(24, 10);

            label.MouseEntered += (_, _) =>
            {
                label.Background = BS3DGame.MENU_BUTTON_OVER_BRUSH;
                label.TextColor = Color.White;
            };

            label.MouseLeft += (_, _) =>
            {
                label.Background = null;
                label.TextColor = BS3DGame.MENU_TEXT;
            };

            label.TouchDown += (_, _) =>
            {
                try
                {
                    //UseShellExecute hands the address to whatever the desktop registered for it; without it
                    //.NET would try to run the URL as an executable and throw every time.
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch (Exception)
                {
                    //A machine with no handler registered throws rather than doing nothing, and an About page
                    //is no place to take the game down. Nothing opens; the address is on screen to be copied.
                }
            };

            return label;
        }

        private Label Paragraph(string text) => new()
        {
            Text = text,
            Font = FontSmall,
            TextColor = BS3DGame.MENU_TEXT_BODY,
            Wrap = true,
            Width = Scaled(TEXT_WIDTH),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ScaledThickness(0, 0, 0, 34),
        };
    }
}
