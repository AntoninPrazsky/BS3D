using System;
using System.Diagnostics;
using Myra.Graphics2D.UI;
using Color = Microsoft.Xna.Framework.Color;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>What the game is, how it is played, and what it is built on.</summary>
    internal sealed class AboutPage : MenuPage
    {
        private const int TEXT_WIDTH = 1860;

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
                      + "A/D traverses the carriage, W/S adjusts depth, Esc pauses, F11 toggles fullscreen, F12 hides the FPS counter."));
            column.Widgets.Add(Paragraph(
                text: "Built on MonoGame (DirectX 11) and BepuPhysics 2. Everything—from the spheres to the city—is procedural. "
                      + "No 3D models, only pure code. Typefaces Anton and Inter (both SIL OFL 1.1)."));
            column.Widgets.Add(Link("github.com/AntoninPrazsky/BS3D", "https://github.com/AntoninPrazsky/BS3D"));

            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
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
