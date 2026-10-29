using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment;

namespace BS3D.Screens
{
    /// <summary>
    /// The main menu's Extras (#704): Write to the Author (#813), Top Players (#827), the Benchmark (#988), the Jukebox and the
    /// Scene picker, which stood in the main menu itself until #704, in the owner's order of 2026-10-29, with room for more.
    /// The owner wanted no ninth entry in the main menu, so Scene gave its slot up to this page.
    /// <para>
    /// It wears the front end's composition rather than a plate (<see cref="MainMenuPage"/>): the entries hang off the
    /// bottom-left corner over the turning scene, at the same inset, so it reads as the menu going one level deeper
    /// rather than as a dialog. The pause page keeps its own Scene entry: the request was about the front end.
    /// </para>
    /// </summary>
    internal sealed class ExtrasPage : MenuPage
    {
        public ExtrasPage(BS3DGame game) : base(game) { }

        internal override bool DimsFrame => false;

        //The front end's resting slab, as on the main menu: big unplated type over the scene
        internal override IBrush EntryRestBrush => BS3DGame.MENU_FRONT_BUTTON_BRUSH;

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();
            column.HorizontalAlignment = HorizontalAlignment.Left;
            column.VerticalAlignment = VerticalAlignment.Bottom;
            column.Margin = ScaledThickness(MainMenuPage.FRONT_INSET, 0, 0, MainMenuPage.FRONT_INSET);

            //The way to the author from the front end, beside About's (#813, the owner's ask): Extras is where a player
            //looks for what else there is, and it heads the page (the owner, 2026-10-29)
            column.Widgets.Add(Game.NoteEntry("extras", frontEnd: true));
            column.Widgets.Add(FrontEndEntry(RankingCopy.TITLE, Game.OpenTopPlayers));
            column.Widgets.Add(FrontEndEntry("Benchmark", Game.OpenBenchmark));
            column.Widgets.Add(FrontEndEntry("Jukebox", Game.OpenJukebox));
            column.Widgets.Add(FrontEndEntry("Scene", Game.OpenSceneSelect));
            column.Widgets.Add(Game.FrontEndEntry("Back", GoBack, Prompt(PromptGlyph.PAD_B, PromptGlyph.KEY_ESCAPE)));

            return ScreenRoot(column, VersionTag());
        }
    }
}
