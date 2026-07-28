using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// The title card the game opens on, over the scene the front end will keep turning behind it. It hands
    /// over to the main menu on its own after <see cref="SECONDS"/>, or the moment the player asks it to.
    /// <para>
    /// It is the first page that does anything in its own <see cref="Update"/> rather than merely showing a
    /// tree, which is what makes it worth having beyond the issue asking for one: a screen that owns a piece
    /// of time is exactly what the stack exists to hold, and it proves the lifecycle end to end — pushed at
    /// boot, ticking, and replacing itself.
    /// </para>
    /// </summary>
    internal sealed class SplashPage : MenuPage
    {
        /// <summary>Long enough to read the name, short enough that nobody reaches for the skip.</summary>
        private const float SECONDS = 2.6f;

        /// <summary>Fade up and fade away, in seconds at each end. A card that cuts in reads as a stutter.</summary>
        private const float FADE = 0.55f;

        /// <summary>
        /// How long any input is ignored for. A splash that can be skipped on frame one is skipped by the very
        /// click that gave the window focus, and the player never sees it at all.
        /// </summary>
        private const float SKIP_AFTER = 0.35f;

        private float _age;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;
        private GamePadState _previousPad;

        public SplashPage(BS3DGame game) : base(game) { }

        //Nothing to go back to, and nothing to dim: the scene turning behind it is the picture
        internal override bool CanGoBack => false;
        internal override bool DimsFrame => false;

        //Held so the fade can be written onto it every frame
        private Label _title;

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            _title = new Label
            {
                Text = BS3DGame.GAME_TITLE,
                Font = FontTitle,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            column.Widgets.Add(_title);

            return ScreenRoot(column);
        }

        /// <summary>
        /// Alpha rather than <c>Visible</c>, and on the colour rather than on the widget, because a colour is
        /// the one thing here that is certain to be honoured by every Myra version this has been built against.
        /// </summary>
        private void Fade(float amount)
        {
            if (_title != null) _title.TextColor = BS3DGame.MENU_TEXT * MathHelper.Clamp(amount, 0f, 1f);
        }

        public override void Enter()
        {
            //Sampled here rather than left at default, so a key already down when the game launched — or the
            //mouse button that started it from a shell — is not read as a fresh press on the first frame
            _previousKeyboard = Keyboard.GetState();
            _previousMouse = Mouse.GetState();
            _previousPad = GamePad.GetState(PlayerIndex.One);

            Fade(0f);
        }

        public override void Update(GameTime gameTime)
        {
            _age += (float)gameTime.ElapsedGameTime.TotalSeconds;

            //Up over the first FADE, held, then away over the last. Written every frame because the tree is
            //rebuilt whenever the window changes size and the fresh label starts at full.
            Fade(MathHelper.Clamp(_age / FADE, 0f, 1f) * MathHelper.Clamp((SECONDS - _age) / FADE, 0f, 1f));

            if (_age >= SECONDS || (_age >= SKIP_AFTER && Skipped()))
            {
                //Replace rather than Pop: the splash is the BOTTOM of the stack at boot, and the front end
                //takes its place rather than being revealed under it
                Manager.Replace(Game.MainMenuPage);
            }
        }

        /// <summary>
        /// Any deliberate press. Read as edges against this page's own snapshots rather than through the
        /// game's, which are the play loop's and are not being kept while a menu is up.
        /// </summary>
        private bool Skipped()
        {
            KeyboardState keyboard = Keyboard.GetState();
            MouseState mouse = Mouse.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            bool skipped =
                (keyboard.GetPressedKeyCount() > 0 && _previousKeyboard.GetPressedKeyCount() == 0)
                || (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
                || (pad.IsButtonDown(Buttons.A) && !_previousPad.IsButtonDown(Buttons.A))
                || (pad.IsButtonDown(Buttons.Start) && !_previousPad.IsButtonDown(Buttons.Start));

            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            _previousPad = pad;

            return skipped;
        }
    }
}
