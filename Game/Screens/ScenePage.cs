using Myra.Graphics2D.UI;
using Prazsky.Core.Render;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// The seven settings the game is played in. What the player picks here is what they then play in — it is
    /// a real scene, not a menu backdrop — so the change takes effect at once and the screen behind the panel
    /// is the preview.
    /// </summary>
    internal sealed class ScenePage : MenuPage
    {
        //One label per SceneKind, indexed by the enum's own value — the count and the names are
        //SceneRenderer's since #75, so a twelfth scene reaches this list without being added to it
        private readonly Label[] _sceneLabels = new Label[SceneRenderer.SceneCount];

        public ScenePage(BS3DGame game) : base(game) { }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SCENE"));

            for (int i = 0; i < SceneRenderer.SceneCount; i++)
            {
                //Captured per iteration, not off the loop variable's final value
                SceneKind scene = (SceneKind)i;
                column.Widgets.Add(MenuButton(SceneRenderer.SceneName(scene), () => Choose(scene), out _sceneLabels[i]));
            }

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
