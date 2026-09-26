using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The intro the game opens on (#454): the <b>2D logo bitmap</b> fading up out of black and held; then the
    /// <b>black</b> fades away, revealing the front end's scene behind the still-visible picture, and a beat
    /// later the <b>picture</b> fades out while the 3D wordmark grows in, in its menu corner (#601, #621).
    /// It hands over to the main menu on its own at the end of that, or sooner when the player asks it to. The
    /// player can turn it off altogether (Settings, "Intro logo") and a run can skip it (<c>nosplash</c>), in
    /// which case it is never pushed and the game opens on the main menu (#621).
    /// <para>
    /// It is the first page that does anything in its own <see cref="Update"/> rather than merely showing a
    /// tree, which is what makes it worth having beyond the issue asking for one: a screen that owns a piece
    /// of time is exactly what the stack exists to hold, and it proves the lifecycle end to end — pushed at
    /// boot, ticking, and replacing itself.
    /// </para>
    /// <para>
    /// <b>There is no hand-over into the 3D letters any more, and that is the owner's decision (#601).</b> From
    /// #454 until then the black cross-faded away into the scene under the logo, the logo then cross-faded into
    /// the 3D lettering standing in the picture's own layout in the middle of the frame, and the letters flew
    /// to the corner as the menu arrived. The owner ruled the cross-fade and the fly out: the wordmark is in its
    /// menu position from the start. The page then cut from the logo on black straight to the menu, and the
    /// owner asked for that cut to be a fade instead (#621): the black goes first, so for a moment the picture
    /// stands alone over the running scene, and then the picture goes. <b>The wordmark is held back until the
    /// picture starts to go and arrives over the same leg</b> (<see cref="WordmarkPresence"/>): with both at
    /// full strength at once the flat logo's centre lines ran into the corner block's and read as one garbled
    /// word ("BUBBLEOTER", photographed at 1600x900), so the two titles cross over rather than overlap. Nothing
    /// morphs or moves: the 2D logo only fades, and the wordmark only grows in about its own anchored corner
    /// (it is opaque geometry, so presence scales its size — see <c>TitleWordmark.Draw</c>). The fade
    /// <i>up</i> out of black is kept: a picture that cuts in on the window's first frame reads as a stutter.
    /// </para>
    /// <para>
    /// <b>The menu becomes interactive when the picture has gone, and not before</b>: the page stays on top of
    /// the stack through all five legs, so the main menu's entries appear, and take input, on the frame it
    /// replaces itself — and <see cref="BS3DGame.IsSplashUp"/>, which the startup script's pages wait on, stays
    /// true until then too. While the black is going the scene is live behind the picture (the backdrop draws
    /// under this page every frame, black or not); the wordmark under the splash is drawn at
    /// <see cref="WordmarkPresence"/>, which is zero — no draw at all — until the picture starts to go.
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
    /// </summary>
    internal sealed class SplashPage : MenuPage
    {
        //=== THE SEQUENCE, in seconds ===
        //
        //A starting point rather than a measurement: the issue offered figures of this shape and said the
        //owner's eye decides. Five legs since #621 — up out of black, held, the black away, a beat, the picture
        //away — and the page then replaces itself with the menu the last two legs have already been showing.

        //Black to the logo over black
        private const float FADE_UP_SECONDS = 0.7f;

        //The logo alone on black — long enough to be read, short enough that nobody reaches for the skip. It
        //was 1.0 with a second leg after it that kept the picture fully up over the arriving scene; that leg
        //went with the hand-over (#601), and this took half of it back so the picture is not gone sooner than
        //it can be read.
        private const float HOLD_SECONDS = 1.5f;

        //The black lifts off the scene, the picture still fully up over it (#621). Slower than the fade up: this
        //is the leg that shows the player a whole new picture — the island, the sky, the city — and a quicker
        //lift reads as the cut it replaced.
        private const float BLACK_OUT_SECONDS = 0.8f;

        //The picture alone over the live scene: long enough to register the scene's arrival and the title's
        //hand-over as two steps rather than one fade smeared into the next, short enough not to read as a second
        //hold.
        private const float BEAT_SECONDS = 0.4f;

        //The picture away, and the 3D wordmark in over the same leg (WordmarkPresence). A shade quicker than the
        //black: the scene is already there, and the flat logo only has to hand over to the corner title.
        private const float LOGO_OUT_SECONDS = 0.7f;

        /// <summary>When the black starts to lift — the end of the part of the sequence that is on black.</summary>
        private const float EXIT_START = FADE_UP_SECONDS + HOLD_SECONDS;

        /// <summary>When the picture starts to go.</summary>
        private const float LOGO_OUT_START = EXIT_START + BLACK_OUT_SECONDS + BEAT_SECONDS;

        /// <summary>The whole sequence; the front end takes the page's place at the end of it.</summary>
        private const float SECONDS = LOGO_OUT_START + LOGO_OUT_SECONDS;

        /// <summary>
        /// How much faster the sequence runs once the player has asked it to go (see <see cref="Update"/>): the
        /// exit's 1.9 seconds pass in about 0.6, which is quick enough to read as the press being obeyed and
        /// slow enough to still be the fade rather than the cut #621 replaced.
        /// </summary>
        private const float SKIP_RATE = 3f;

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

        //Whether the player has asked the page to go: the rest of the sequence then runs at SKIP_RATE, with the
        //hold (if it was still to come) left out, and a second press cuts
        private bool _skipping;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;
        private GamePadState _previousPad;

        public SplashPage(BS3DGame game) : base(game) { }

        //Nothing to go back to, and nothing to dim: the scene turning behind it is the picture
        internal override bool CanGoBack => false;
        internal override bool DimsFrame => false;

        /// <summary>
        /// Nothing. The picture is not a widget — see the class remarks for why it is drawn by this page's own
        /// <see cref="Draw"/> rather than laid out by Myra — so the root stays empty on purpose.
        /// </summary>
        protected override Widget BuildTree() => ScreenRoot();

        /// <summary>
        /// How present the 3D wordmark is under this page, 0 to 1 (#621), for <c>BackdropScreen.DrawWordmark</c>
        /// to hand to <c>TitleWordmark.Draw</c>'s <c>presence</c>: zero through the legs on black, the black's lift
        /// and the beat — the picture is the only title while it is fully up — then a smoothstep to one over
        /// exactly the leg the picture fades out on, so the two cross over. It is one when the page leaves at the
        /// end of the sequence, and a skip runs through that leg too; a second press cuts to the main menu, where
        /// the title is always at one, as it is with no splash at all.
        /// </summary>
        internal float WordmarkPresence => MathHelper.SmoothStep(0f, 1f, (_age - LOGO_OUT_START) / LOGO_OUT_SECONDS);

        public override void Enter()
        {
            _age = 0f;
            _skipping = false;

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

            //Wall seconds, so the legs last the same at any frame rate; a skip only scales them
            _age += (float)gameTime.ElapsedGameTime.TotalSeconds * (_skipping ? SKIP_RATE : 1f);

            //A SKIP FAST-FORWARDS, and a second one cuts (#621). The first deliberate press finishes the fade
            //up (quickly), leaves the hold out and runs the exit — black away, beat, picture away — at SKIP_RATE,
            //so a player who asked for the menu gets it in well under a second and still sees it arrive the way
            //it always does, rather than the one-frame cut the owner asked to be rid of. Jumping straight to the
            //start of the exit instead would pop a half-faded logo to full on the press. A player who presses
            //again is impatient with even that, and gets the menu on that frame.
            bool cut = _age >= SECONDS;

            if (_age >= SKIP_AFTER && Skipped())
            {
                if (_skipping) cut = true;
                else _skipping = true;
            }

            if (_skipping && _age >= FADE_UP_SECONDS && _age < EXIT_START) _age = EXIT_START;

            if (cut)
            {
                //Replace rather than Pop: the splash is the only page over the backdrop at boot, so the front
                //end has to TAKE ITS PLACE — a pop would leave the backdrop standing with no menu on it at
                //all, and the game would open on a scene the player cannot do anything with. It names ITSELF as
                //the screen replaced (#576): a Replace of whatever was on top swallowed a page pushed over the
                //splash, and one that asks after the splash has already been popped (the play argument) is
                //dropped by the manager rather than putting a menu over the level.
                //
                //At the end of the sequence the black and the picture are already gone, so this frame changes
                //only what Myra draws: the main menu's entries arrive, and with them its input. After a second
                //press the page leaves wherever it was, and the title is standing in its corner on the menu —
                //it never stands anywhere else.
                Manager.Replace(this, Game.MainMenuPage);
            }
        }

        /// <summary>
        /// The black and the picture, over the resolved frame. Two sprites in the host's overlay batch, black
        /// first, so the logo fades up <i>over</i> it — and so, when the black lifts (#621), the picture is left
        /// standing over the scene until its own fade.
        /// <para>
        /// The fade is a plain scale on <c>Color</c>: the texel and the logo are premultiplied (the content
        /// pipeline premultiplies on build, <c>PremultiplyAlpha=True</c>), which is what the batch's default
        /// <c>AlphaBlend</c> expects, and scaling all four channels keeps a premultiplied colour correct. Loading
        /// the logo any other way would fringe its edges — the note is in <c>Images/logo/README.md</c>.
        /// </para>
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            //Each leg a smoothstep of its own share of the clock; SmoothStep clamps its amount, so outside its leg
            //each term is simply its end value. The picture: up out of the black, held, then away. The black:
            //solid, then away — and gone for good before the picture starts to go (the beat between them).
            float logo = MathHelper.SmoothStep(0f, 1f, _age / FADE_UP_SECONDS)
                * (1f - MathHelper.SmoothStep(0f, 1f, (_age - LOGO_OUT_START) / LOGO_OUT_SECONDS));
            float black = 1f - MathHelper.SmoothStep(0f, 1f, (_age - EXIT_START) / BLACK_OUT_SECONDS);

            Rectangle frame = Game.GraphicsDevice.Viewport.Bounds;
            SpriteBatch batch = Game.OverlayBatch;

            batch.Begin();
            if (black > 0f) batch.Draw(Game.Texel, frame, Color.Black * black);
            if (logo > 0f) batch.Draw(Game.Logo, LogoRectangle(frame), Color.White * logo);
            batch.End();
        }

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
