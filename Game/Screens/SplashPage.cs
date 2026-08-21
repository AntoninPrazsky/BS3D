using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

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
    /// <para>
    /// <b>Its tree is now empty, and that is the whole change #248 made here.</b> The card was the game's name
    /// as a Myra label centred in the frame, faded up and away on this page's own clock — and once the menu's
    /// title became 3D lettering standing in the scene, a flat centred label two and a half seconds earlier
    /// read as a different game's title card. So the name is the <i>same</i> 3D object on both screens now
    /// (<see cref="Effects.TitleWordmark"/>, drawn by <see cref="BackdropScreen"/>): it opens centred on one
    /// line, and when this page hands over it MOVES into the corner and re-flows into the menu's three lines.
    /// What is left here is the piece of time — how long the card is held, and what skips it — which was always
    /// the part that belonged to a screen rather than to a widget.
    /// </para>
    /// </summary>
    internal sealed class SplashPage : MenuPage
    {
        /// <summary>Long enough to read the name, short enough that nobody reaches for the skip.</summary>
        private const float SECONDS = 2.6f;

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

        /// <summary>
        /// Nothing. The card's one widget was the game's name and it left this tree with #248 — see the class
        /// remarks. An empty root is deliberate rather than a leftover: this page's whole substance is the
        /// piece of time it owns, and the thing being shown during it is drawn in the scene rather than over
        /// the frame. The fade-up that stopped a flat card cutting in went with the widget; the wordmark's own
        /// arrival is what answers that now.
        /// </summary>
        protected override Widget BuildTree() => ScreenRoot();

        public override void Enter()
        {
            //Sampled here rather than left at default, so a key already down when the game launched — or the
            //mouse button that started it from a shell — is not read as a fresh press on the first frame
            _previousKeyboard = Keyboard.GetState();
            _previousMouse = Mouse.GetState();
            _previousPad = GamePad.GetState(PlayerIndex.One);
        }

        public override void Update(GameTime gameTime)
        {
            //The shared menu frame first (the base): the display hotkeys work on the title card too
            base.Update(gameTime);

            _age += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_age >= SECONDS || (_age >= SKIP_AFTER && Skipped()))
            {
                //Replace rather than Pop: the splash is the only page over the backdrop at boot, so the front
                //end has to TAKE ITS PLACE — a pop would leave the backdrop standing with no menu on it at
                //all, and the game would open on a scene the player cannot do anything with.
                Manager.Replace(Game.MainMenuPage);
            }
        }

        /// <summary>
        /// Any deliberate press. Read as edges against this page's own snapshots rather than the host's, and
        /// that is still right now that <see cref="Update"/> runs the shared menu chrome (which does keep the
        /// host's keyboard and pad snapshots): the host tracks no <i>mouse</i> edge at all, and these are
        /// deliberately frozen at <see cref="Enter"/> until <see cref="SKIP_AFTER"/> passes, so a key already
        /// down at boot never reads as a skip. Sharing the host's, which move every frame, would lose both.
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
