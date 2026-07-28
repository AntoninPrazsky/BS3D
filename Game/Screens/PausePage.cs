using Myra.Graphics2D.UI;

namespace BS3D.Screens
{
    /// <summary>
    /// Over a stopped game. Its own back is not a pop but a resume — the game has to start running again, and
    /// only the game knows that — so <see cref="BS3DGame.MenuBack"/> routes it.
    /// </summary>
    internal sealed class PausePage : MenuPage
    {
        public PausePage(BS3DGame game) : base(game) { }

        internal override bool DimsFrame => true;

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("PAUSED"));
            column.Widgets.Add(MenuButton("Resume", Game.ResumeGame));
            column.Widgets.Add(MenuButton("Settings", Game.OpenSettings));
            column.Widgets.Add(MenuButton("Scene", Game.OpenSceneSelect));
            column.Widgets.Add(MenuButton("Main Menu", Game.ReturnToMainMenu));
            column.Widgets.Add(MenuButton("Quit", Game.Exit));

            return ScreenRoot(column);
        }
    }
}
