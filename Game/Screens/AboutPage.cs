using Myra.Graphics2D.UI;
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
                text: "Built on MonoGame (DirectX 11) and BepuPhysics 2. Everything—scenes, spheres, and the city—is procedural. "
                      + "No 3D models, only pure code. Typeface Inter (SIL OFL 1.1)."));
            column.Widgets.Add(Paragraph(text: "github.com/AntoninPrazsky/BS3D"));

            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
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
