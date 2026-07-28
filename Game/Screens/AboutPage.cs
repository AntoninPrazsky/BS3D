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
                BS3DGame.GAME_TITLE + " is a 3D bubble shooter: shoot coloured balls at a cluster hanging from a glass ceiling. "
                + "Three or more of one colour let go and fall — and take with them everything they were the "
                + "last anchor for."));
            column.Widgets.Add(Paragraph(
                "Controls:  the mouse aims,  left button or space fires,  right button leans in along the "
                + "barrel,  A/D traverses the carriage,  Esc pauses,  F11 toggles fullscreen,  F12 hides the "
                + "FPS counter."));
            column.Widgets.Add(Paragraph(
                "Built on MonoGame (DirectX 11) and BepuPhysics 2. The scenes, the balls and the city are all "
                + "procedural — no models, only code. Typeface Inter (SIL OFL 1.1)."));
            column.Widgets.Add(Paragraph("github.com/AntoninPrazsky/BS3D"));

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
