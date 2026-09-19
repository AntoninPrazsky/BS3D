using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The intro the game opens on (#454): the <b>2D logo bitmap</b> fading up out of black, the front end's
    /// scene cross-fading in behind it, and the picture then cross-fading into the <b>3D wordmark</b> standing
    /// in the same place — which flies to its menu corner as this page hands over. It hands over on its own
    /// after the sequence, or the moment the player asks it to.
    /// <para>
    /// It is the first page that does anything in its own <see cref="Update"/> rather than merely showing a
    /// tree, which is what makes it worth having beyond the issue asking for one: a screen that owns a piece
    /// of time is exactly what the stack exists to hold, and it proves the lifecycle end to end — pushed at
    /// boot, ticking, and replacing itself.
    /// </para>
    /// <para>
    /// <b>The sequence is five legs, and the order of the middle three is the point.</b> The window opens
    /// black; the logo fades up out of it; the <i>black</i> then cross-fades away into the scene, which has
    /// been turning underneath from frame one; only then does the <i>logo</i> go — and it goes by becoming the
    /// wordmark: <see cref="Effects.TitleWordmark.BeginHandover"/> stands the 3D letters in the picture's own
    /// layout under it on the frame the bitmap starts thinning, so what is left when the picture has gone is
    /// the same title as geometry. The scene arrives first, under the logo, and the logo leaves last, into
    /// the letters. Then the menu takes the page's place and the letters fly to the corner (the wordmark's
    /// own move, started by the menu asking for its composition).
    /// </para>
    /// <para>
    /// <b>Its tree is empty and it draws with the host's overlay batch instead.</b> The black and the picture
    /// are two sprites over the resolved frame, drawn in this page's own <see cref="Draw"/> — after the backdrop
    /// has run the whole pipeline underneath (the stack draws bottom-up) and before the FPS component and the
    /// Myra desktop. A Myra image widget could have held the logo, and did not for two reasons: the black has
    /// to cover the <i>frame</i> and Myra's paint stops short of the viewport's bottom rows (the scrim's own
    /// finding, #114), and a widget is laid out in the menu's scaled design units where the picture has to be
    /// placed in <b>pixels</b> (see <see cref="LogoRectangle"/>).
    /// </para>
    /// <para>
    /// <b>Until #454 this page held nothing but the piece of time</b>, the 3D title having opened centred on
    /// its own account and moved to the corner when this page handed over (#248's second pass). The bitmap is
    /// the game's first impression now, so the wordmark starts in its corner and the only time it stands in
    /// the middle of the frame is the hand-over above — a `play` boot, or a skip before the hand-over began,
    /// never sees it there.
    /// </para>
    /// </summary>
    internal sealed class SplashPage : MenuPage
    {
        //=== THE SEQUENCE, each leg after the one before, in seconds ===
        //
        //A starting point rather than a measurement: the issue offered figures of this shape and said the
        //owner's eye decides. What is fixed is the ORDER (see the class remarks) and that nothing here cuts —
        //every leg is a smoothstep, since a picture that cuts in reads as a stutter and one that cuts out
        //reads as a fault.

        //Black to the logo over black
        private const float FADE_UP_SECONDS = 0.7f;

        //The logo alone on black — long enough to be read, short enough that nobody reaches for the skip
        private const float HOLD_SECONDS = 1.0f;

        //The black away into the scene, the logo still over it
        private const float SCENE_IN_SECONDS = 1.0f;

        //The logo into the wordmark. The letters' own arrival swell runs under this (TitleWordmark's
        //REVEAL_SECONDS is of the same length on purpose), so the picture inflates into geometry across it.
        private const float HANDOVER_SECONDS = 0.8f;

        //The wordmark alone, centred, before the menu takes the page's place and it flies
        private const float BEAT_SECONDS = 0.35f;

        //Where each leg begins on this page's clock
        private const float SCENE_IN_AT = FADE_UP_SECONDS + HOLD_SECONDS;
        private const float HANDOVER_AT = SCENE_IN_AT + SCENE_IN_SECONDS;
        private const float BEAT_AT = HANDOVER_AT + HANDOVER_SECONDS;

        /// <summary>The whole sequence; the front end takes the page's place at the end of it.</summary>
        private const float SECONDS = BEAT_AT + BEAT_SECONDS;

        /// <summary>
        /// How long any input is ignored for. A splash that can be skipped on frame one is skipped by the very
        /// click that gave the window focus, and the player never sees it at all.
        /// </summary>
        private const float SKIP_AFTER = 0.35f;

        //=== WHERE THE PICTURE GOES ===

        //THE BAND OF RESOLUTIONS THE BITMAP IS SHOWN PIXEL FOR PIXEL ACROSS: one source pixel to one display
        //pixel from 3840 x 1600 up to 3840 x 2160 — the owner's two monitors — and scaled in proportion
        //everywhere else. One rule gives exactly that with no special case: the scale is the smaller of the
        //frame's width over 3840 and its height over 1600. The 1267-row logo already fills 79% of a 1600-row
        //frame, which is what makes 1600 the binding edge of the band; at 2160 rows the height would allow
        //1.35 and the width holds it at 1. So 2560 x 1440 gets 0.667 (1365 x 845) and 1920 x 1080 gets a half
        //(1024 x 634), which is exactly a 2 x 2 box filter through the batch's bilinear sampler. Above 3840
        //wide the rule scales PAST 1:1 and a 2048-pixel bitmap upscaled is soft; the master in Images/logo is
        //4864 x 3328, so a larger export costs nothing but file size when a frame that wide turns up.
        private const int BAND_WIDTH = 3840, BAND_HEIGHT = 1600;

        private float _age;
        private bool _handedOver;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;
        private GamePadState _previousPad;

        public SplashPage(BS3DGame game) : base(game) { }

        //Nothing to go back to, and nothing to dim: the scene turning behind it is the picture
        internal override bool CanGoBack => false;
        internal override bool DimsFrame => false;

        /// <summary>
        /// Whether the 3D wordmark is on show under this page yet — <c>true</c> from the frame the hand-over
        /// begins. <see cref="BackdropScreen"/> reads it to know when to start drawing the title: not a moment
        /// earlier, or the letters would stand in the scene behind the picture from the first frame and show
        /// round its edges as the black went.
        /// </summary>
        internal bool WordmarkShown => _handedOver;

        /// <summary>
        /// Nothing. The picture is not a widget — see the class remarks for why it is drawn by this page's own
        /// <see cref="Draw"/> rather than laid out by Myra — so the root stays empty on purpose.
        /// </summary>
        protected override Widget BuildTree() => ScreenRoot();

        public override void Enter()
        {
            _age = 0f;
            _handedOver = false;

            //Sampled here rather than left at default, so a key already down when the game launched — or the
            //mouse button that started it from a shell — is not read as a fresh press on the first frame
            _previousKeyboard = Keyboard.GetState();
            _previousMouse = Mouse.GetState();
            _previousPad = GamePad.GetState(PlayerIndex.One);
        }

        public override void Update(GameTime gameTime)
        {
            //The shared menu frame first (the base): the display hotkeys work on the intro too
            base.Update(gameTime);

            _age += (float)gameTime.ElapsedGameTime.TotalSeconds;

            //The hand-over, once, on the frame the picture starts to thin: the wordmark is stood in the
            //picture's own place — its share of the frame is this launch's, since the picture is placed in
            //pixels — and its arrival swell starts. From here the backdrop draws it under this page.
            if (!_handedOver && _age >= HANDOVER_AT)
            {
                _handedOver = true;

                Rectangle frame = Game.GraphicsDevice.Viewport.Bounds;
                Rectangle logo = LogoRectangle(frame);
                Game.TitleWordmark?.BeginHandover(logo.Width / (float)frame.Width, logo.Height / (float)frame.Height);
            }

            if (_age >= SECONDS || (_age >= SKIP_AFTER && Skipped()))
            {
                //Replace rather than Pop: the splash is the only page over the backdrop at boot, so the front
                //end has to TAKE ITS PLACE — a pop would leave the backdrop standing with no menu on it at
                //all, and the game would open on a scene the player cannot do anything with.
                //
                //A skip is a cut, deliberately: the black and the picture stop being drawn on the frame this
                //page leaves, whichever leg it was in. A player who pressed a key asked for the menu, not for
                //a faster fade — and the title is already in its corner if the hand-over had not begun, or
                //flies there from wherever the hand-over left it if it had.
                Manager.Replace(Game.MainMenuPage);
            }
        }

        /// <summary>
        /// The black and the picture, over the resolved frame. Two sprites in the host's overlay batch, black
        /// first so the logo fades up <i>over</i> it and stays over the scene as the black goes.
        /// <para>
        /// Both fades are a plain scale on <c>Color</c>: the texel and the logo are premultiplied (the content
        /// pipeline premultiplies on build, <c>PremultiplyAlpha=True</c>), which is what the batch's default
        /// <c>AlphaBlend</c> expects, and scaling all four channels keeps a premultiplied colour correct. Loading
        /// the logo any other way would fringe its edges — the note is in <c>Images/logo/README.md</c>.
        /// </para>
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            float black = BlackOpacity(_age);
            float logo = LogoOpacity(_age);
            if (black <= 0f && logo <= 0f) return;

            Rectangle frame = Game.GraphicsDevice.Viewport.Bounds;
            SpriteBatch batch = Game.OverlayBatch;

            batch.Begin();
            if (black > 0f) batch.Draw(Game.Texel, frame, Color.Black * black);
            if (logo > 0f) batch.Draw(Game.Logo, LogoRectangle(frame), Color.White * logo);
            batch.End();
        }

        /// <summary>Solid until the scene leg, then away over it.</summary>
        private static float BlackOpacity(float age) =>
            age < SCENE_IN_AT ? 1f : 1f - MathHelper.SmoothStep(0f, 1f, (age - SCENE_IN_AT) / SCENE_IN_SECONDS);

        /// <summary>Up out of the black, held through the scene leg, then away into the wordmark.</summary>
        private static float LogoOpacity(float age) =>
            age < FADE_UP_SECONDS ? MathHelper.SmoothStep(0f, 1f, age / FADE_UP_SECONDS)
            : age < HANDOVER_AT ? 1f
            : 1f - MathHelper.SmoothStep(0f, 1f, (age - HANDOVER_AT) / HANDOVER_SECONDS);

        /// <summary>
        /// Where the picture sits in <paramref name="frame"/>: centred, at the band rule's scale (see
        /// <see cref="BAND_WIDTH"/>), and <b>rounded to whole pixels</b> — a 1:1 frame that landed the quad on a
        /// half pixel would resample what was meant to be exact, and the sampler would then soften every edge
        /// of a picture placed precisely so that it would not.
        /// </summary>
        private Rectangle LogoRectangle(Rectangle frame)
        {
            Texture2D logo = Game.Logo;
            float scale = MathF.Min(frame.Width / (float)BAND_WIDTH, frame.Height / (float)BAND_HEIGHT);

            int width = (int)MathF.Round(logo.Width * scale);
            int height = (int)MathF.Round(logo.Height * scale);

            return new Rectangle(frame.X + (frame.Width - width) / 2, frame.Y + (frame.Height - height) / 2, width, height);
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
