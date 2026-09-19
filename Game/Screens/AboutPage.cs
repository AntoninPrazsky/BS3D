using BS3D.Audio;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// What the game is, how it is played, and what it is built on — and, since #443, the game's original
    /// procedural score, kept as a small player once generated recordings replaced it as the level music: play
    /// and pause, the next piece, and a spectrum.
    /// </summary>
    /// <remarks>
    /// <b>Two columns (#463), on Settings' own pattern</b> (<see cref="SettingsPage"/>'s class remarks explain
    /// why a split beats a scroller): left, what the game is and what it is built on; right, what it was made
    /// with and the score's player, since that is the one thing on this page with a fixed width of its own
    /// (<see cref="ColumnWidth"/>) that a narrower single column would have had to fight. Every paragraph is
    /// cut to <see cref="ColumnWidth"/> now rather than a page-specific figure, so the credits text lines up
    /// with the player's own widgets and with a button's width everywhere else in the menu — one number
    /// instead of two that could drift apart.
    /// </remarks>
    internal sealed class AboutPage : MenuPage
    {
        //The gap between the two columns, on GROUP_GAP's own figure from SettingsPage — wide enough that it
        //reads as the seam between two groups rather than one more paragraph's own margin.
        private const int COLUMN_GAP = 110;

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

            HorizontalStackPanel columns = new()
            {
                Spacing = Scaled(COLUMN_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            //Added in this order, and that IS the nav order — see SettingsPage's own remarks on why
            columns.Widgets.Add(BuildAboutColumn());
            columns.Widgets.Add(BuildCreditsColumn());

            column.Widgets.Add(columns);
            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
        }

        /// <summary>
        /// The left column: what the game is, and what it is built on — everything a player reads before
        /// anything they can click. The repository is a real menu entry now (#463) rather than a label a
        /// pointer could reach and a pad could not.
        /// </summary>
        private VerticalStackPanel BuildAboutColumn()
        {
            VerticalStackPanel left = SubColumn();

            left.Widgets.Add(Paragraph(
                text: BS3DGame.GAME_TITLE + " is a 3D arcade puzzle. Aim, match, and collapse color clusters hanging beneath a glass ceiling. "
                                          + "Matching three or more drops them—severing everything anchored below. "
                                          + "The first chapter teaches the controls as you play."));
            left.Widgets.Add(Paragraph(
                text: "Built on .NET 10, MonoGame 3.8.5 (DirectX 11) and BepuPhysics 2.5, with Myra for the interface, "
                      + "FontStashSharp for the text and NVorbis for the music. Everything you see—from the spheres "
                      + "to the city—is procedural: no 3D models, only pure code. Typefaces Anton, Inter and "
                      + "PromptFont (all SIL OFL 1.1)."));

            //TODO(#463): MonoGame's and BepuPhysics' logos go here, beside their credit above — waiting on the
            //owner's own artwork, which this issue does not carry. An image widget each, through the content
            //pipeline the splash logo already goes through (Content.mgcb, PremultiplyAlpha), plus each
            //project's logo-usage terms recorded beside the asset the way the fonts' OFL files sit beside the
            //TTFs — see the issue for the full note.

            left.Widgets.Add(MenuButton("github.com/AntoninPrazsky/BS3D", OpenRepository));

            return left;
        }

        /// <summary>
        /// The right column: what the game was made with, and the one thing on this page with a fixed width
        /// of its own — the original procedural score's player, moved here whole rather than split, since the
        /// visualizer and its two buttons are one unit (#463).
        /// </summary>
        private VerticalStackPanel BuildCreditsColumn()
        {
            VerticalStackPanel right = SubColumn();

            right.Widgets.Add(Paragraph(
                text: "Made with Claude (Anthropic), through Claude Code—the code, the shaders, the level designs, "
                      + "the tools and the documentation. The rest was generated locally: the music with ACE-Step 1.5, "
                      + "the opening logo and every design reference with Z-Image-Turbo (the logo upscaled with "
                      + "RealESRGAN), and small local models—Gemma, nomic-embed—as development tools."));

            right.Widgets.Add(Paragraph(
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
            right.Widgets.Add(_pieceLabel);

            right.Widgets.Add(new MusicVisualizer(jukebox, ColumnWidth, Scaled(VISUALIZER_HEIGHT), Scaled(VISUALIZER_BAR_GAP))
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

            right.Widgets.Add(controls);

            return right;
        }

        /// <summary>One of the page's two side-by-side groups — MenuColumn's own spacing, unaligned to it.</summary>
        private VerticalStackPanel SubColumn() => new()
        {
            Spacing = Scaled(24),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

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
                Process.Start(new ProcessStartInfo { FileName = "https://github.com/AntoninPrazsky/BS3D", UseShellExecute = true });
            }
            catch (Exception)
            {
                //A machine with no handler registered throws rather than doing nothing, and an About page is
                //no place to take the game down. The button's own text is the address, to be read or copied.
            }
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
        /// One paragraph of prose, cut to <see cref="ColumnWidth"/> (#463) — the same figure a button and the
        /// player's own widgets are cut to, so a column's text lines up with everything under it instead of
        /// keeping a page-specific width of its own.
        /// </summary>
        private Label Paragraph(string text) => new()
        {
            Text = text,
            Font = FontSmall,
            TextColor = BS3DGame.MENU_TEXT_BODY,
            Wrap = true,
            Width = ColumnWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ScaledThickness(0, 0, 0, 34),
        };
    }
}
