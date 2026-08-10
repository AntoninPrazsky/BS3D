using Myra.Graphics2D.UI;
using Prazsky.Core.Render;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// Every setting the game is played in. What the player picks here is what they then play in — it is
    /// a real scene, not a menu backdrop — so the change takes effect at once and the screen behind the panel
    /// is the preview.
    /// </summary>
    internal sealed class ScenePage : MenuPage
    {
        //One label per SceneKind, indexed by the enum's own value — the count and the names are
        //SceneRenderer's since #75, so a new scene reaches this list without being added to it
        private readonly Label[] _sceneLabels = new Label[SceneRenderer.SceneCount];

        /// <summary>
        /// What the page needs around the list — the heading, the note under it, the Back button and the
        /// plate's padding — so the scroller only gives up what those actually take (the level picker's
        /// figure, for the same arrangement).
        /// </summary>
        private const int LIST_SURROUNDINGS = 620;

        public ScenePage(BS3DGame game) : base(game) { }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SCENE"));

            //THE LIST SCROLLS, because this page grows by one row every time a scene is added and nothing was
            //bounding it: the entries plus the heading, the note and Back ran past the bottom of a short
            //window with no way to reach what fell off. The heading, note and Back stay outside the scroller,
            //so the way back is never the thing that scrolls away.
            VerticalStackPanel list = MenuColumn();

            for (int i = 0; i < SceneRenderer.SceneCount; i++)
            {
                //Captured per iteration, not off the loop variable's final value
                SceneKind scene = (SceneKind)i;
                list.Widgets.Add(MenuButton(SceneRenderer.SceneName(scene), () => Choose(scene), out _sceneLabels[i]));
            }

            column.Widgets.Add(MenuScroll(list, LIST_SURROUNDINGS));

            column.Widgets.Add(new Label
            {
                Text = "Applies at once — the menu and the game both play in it.",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 29, 0, 29),
            });
            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
        }

        private void Choose(SceneKind scene)
        {
            Game.SetScene(scene);
            Refresh();
        }

        /// <summary>
        /// Marks the scene in use, so the screen says where you are as well as where you can go. Brightness,
        /// not colour: the one in use is stated white and the rest step back to grey, which reads the same over
        /// a neon city and over a snowfield.
        /// </summary>
        internal override void Refresh()
        {
            if (_sceneLabels[0] == null) return;

            for (int i = 0; i < SceneRenderer.SceneCount; i++)
                _sceneLabels[i].TextColor = (SceneKind)i == Game.Scene ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
        }
    }
}
