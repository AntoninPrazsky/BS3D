using Myra.Graphics2D.UI;

namespace BS3D.Screens
{
    //The six pages the game has today. They are thin on purpose at this step: each names which tree it shows,
    //what has to be re-read when the player arrives at it, and whether there is anywhere to go back to. The
    //widget building itself still lives in BS3DGame and moves into these classes next — moving the flow and
    //moving a thousand lines of tree building are separate changes, and doing them together would leave no way
    //to tell which one broke something.

    /// <summary>
    /// The front end. No back — quitting is an entry on it, and a menu that closes when a key is tapped is one
    /// that closes by accident. It never dims: the scene turning behind it is the whole point of the screen.
    /// </summary>
    internal sealed class MainMenuPage : MenuPage
    {
        public MainMenuPage(BS3DGame game) : base(game) { }

        internal override Widget Root => Game.MainMenuRoot;
        internal override bool CanGoBack => false;
        internal override bool DimsFrame => false;

        internal override void Refresh() => Game.RefreshMainMenu();
    }

    /// <summary>
    /// Over a stopped game. Its own back is not a pop but a resume — the game has to start running again, and
    /// only the game knows that — so <see cref="BS3DGame"/> routes it.
    /// </summary>
    internal sealed class PausePage : MenuPage
    {
        public PausePage(BS3DGame game) : base(game) { }

        internal override Widget Root => Game.PauseRoot;
        internal override bool DimsFrame => true;
    }

    internal sealed class SettingsPage : MenuPage
    {
        public SettingsPage(BS3DGame game) : base(game) { }

        internal override Widget Root => Game.SettingsRoot;

        internal override void Refresh() => Game.RefreshSettingsLabels();
    }

    internal sealed class ScenePage : MenuPage
    {
        public ScenePage(BS3DGame game) : base(game) { }

        internal override Widget Root => Game.SceneSelectRoot;

        internal override void Refresh() => Game.MarkSelectedScene();
    }

    internal sealed class AboutPage : MenuPage
    {
        public AboutPage(BS3DGame game) : base(game) { }

        internal override Widget Root => Game.AboutRoot;
    }

    /// <summary>
    /// The end of a level. No back, for a different reason from the main menu's: the level has already ended,
    /// so there is nothing to resume into and "back one level" has no meaning. Retry, Next Level and Main Menu
    /// are the only ways off it.
    /// </summary>
    internal sealed class ResultPage : MenuPage
    {
        public ResultPage(BS3DGame game) : base(game) { }

        internal override Widget Root => Game.ResultRoot;
        internal override bool CanGoBack => false;
        internal override bool DimsFrame => true;

        internal override void Refresh() => Game.RefreshResultScreen();
    }
}
