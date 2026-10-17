using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The beat between the campaign's last cleared level and its result page (#880): <b>CAMPAIGN COMPLETE</b> across the
    /// whole frame for a moment, dissolving into one message to the player (<see cref="CampaignFinaleCopy"/>: thanks, more
    /// is coming, and one cryptic hint at an easter egg), and then the result page as it always was.
    /// <para>
    /// <b>It is pushed in the result page's place and hands over to it</b> (<see cref="BS3DGame.PresentResult"/>, and
    /// <c>BS3DGame.ShowResultAfterFinale</c> at the end): the page's <c>Enter</c> captures the camera and starts the star reveal,
    /// the defocus and the boards' plate, and none of that may run behind a message being read. Like the result page it lets
    /// the session under it go on (<see cref="UpdatesUnderlying"/>, #241), so the fireworks, the confetti and the cluster's
    /// last fall carry on behind the lettering; the frame is not dimmed by the page but by a scrim of its own that follows
    /// the text, because the lettering has to read over a lit, moving arena.
    /// </para>
    /// <para>
    /// <b>It stands aside</b> (<see cref="StandsAside"/>, #861): the host draws and feeds none of a tree and hands the page
    /// no walk, no Enter and no Back, so the press that skips cannot also choose a button of the page that comes next, and
    /// Escape skips rather than pausing a game that is over. It draws with the host's overlay batch in
    /// <see cref="DrawAside"/>, in the front end's own faces (Anton for the headline, Inter for the message) and tokens
    /// (<see cref="BS3DGame.MENU_TEXT"/>, <see cref="BS3DGame.MENU_TEXT_BODY"/>), at sizes it fits to the frame.
    /// </para>
    /// <para>
    /// <b>Bounded, and never the accidental skip #347 removed.</b> The player is firing at the clear's last balls while the
    /// beat before this page runs, so no input counts for <see cref="SKIP_GRACE"/> seconds. A skip during the headline goes
    /// on to the message, a skip during the message goes on to the result page, and a message cannot be skipped for
    /// <see cref="MIN_READ"/> seconds after it appears, so one press that skipped the headline cannot also skip the message.
    /// Nothing waits on a press: the headline holds <see cref="TITLE_SECONDS"/> and the message holds for its length
    /// (<see cref="CampaignFinaleCopy.HoldSeconds"/>), at most <see cref="CampaignFinaleCopy.MAX_HOLD"/>.
    /// </para>
    /// </summary>
    internal sealed class CampaignFinalePage : MenuPage
    {
        //=== THE SEQUENCE, in seconds ===
        //
        //A starting point: the issue left the durations to the eye. The headline is up for the first TITLE_SECONDS
        //(its fade in included) and then dissolves into the message over DISSOLVE; the message stays for as long as a
        //reader needs and fades away over FADE_OUT into the result page.

        private const float TITLE_FADE_IN = 0.6f;
        private const float TITLE_SECONDS = 3.2f;
        private const float DISSOLVE = 0.9f;
        private const float FADE_OUT = 0.8f;

        /// <summary>The fade out when the player has skipped: quick enough to read as the press being obeyed.</summary>
        private const float FADE_OUT_SKIPPED = 0.35f;

        /// <summary>
        /// How long after the page arrives no input counts: the player fires through the beat before it (the shot that
        /// cleared the field and those after it), and a click that landed on the first frame is a shot, not a skip.
        /// </summary>
        internal const float SKIP_GRACE = 1.0f;

        /// <summary>How long a message is on the frame before a skip may take it off (see the class remarks).</summary>
        internal const float MIN_READ = 1.5f;

        //=== THE LOOK ===

        //The headline's width as a share of the frame's, and its height cap as a share of the frame's: the width binds
        //on every frame wider than about 3:1, the height on a narrow window
        private const float TITLE_WIDTH_SHARE = 0.88f;
        private const float TITLE_HEIGHT_SHARE = 0.62f;

        //Anton's line over its em (ascent + descent, the FontStashSharp size convention: "Typesetting" in menu-look.md)
        private const float ANTON_LINE_OF_EM = 1.51f;

        //The message's face, in design units (a body-size step of the scale, in Inter) and the width of its column, which
        //holds 45 to 75 characters at every frame size (the measure rule), capped by the frame's width
        private const int MESSAGE_FONT = 80;
        private const int MESSAGE_COLUMN = 2300;
        private const float MESSAGE_COLUMN_MAX_SHARE = 0.86f;
        private const int MESSAGE_LEADING = 16;

        //The scrim behind the lettering, as the alpha of black: light over the headline, so the arena and its fireworks
        //stay the picture, heavier under the message, which is reading
        private const float SCRIM_TITLE = 0.34f;
        private const float SCRIM_MESSAGE = 0.66f;

        //The shadow under every line, in design units: the lift every other line of type over the arena has
        private const int SHADOW_OFFSET = 5;

        private enum Phase { Title, Message, Out }

        private readonly PlayHud _skipHint;

        private LevelResult _result;
        private int _messageIndex;
        private float _hold;

        private Phase _phase;
        private float _age, _phaseTime;
        private bool _skipped, _handedOver;
        private float _titleAlpha, _messageAlpha, _scrimAlpha;

        //The layout, rebuilt when the frame's size changes: fonts, the lines of each paragraph and where they stand
        private int _laidOutWidth, _laidOutHeight;
        private SpriteFontBase _titleFont, _messageFont;
        private Vector2 _titleAt;
        private string[] _thanksLines, _hintLines;
        private float _lineStep, _paragraphGap, _messageTop, _messageLeft, _shadowOffset;

        public CampaignFinalePage(BS3DGame game) : base(game) => _skipHint = new PlayHud(game);

        internal override bool CanGoBack => false;

        /// <summary>Yes, as the result page's is (#241): the celebration and the cluster's last fall go on behind the lettering.</summary>
        public override bool UpdatesUnderlying => true;

        internal override bool DimsFrame => false;

        /// <summary>The page has no tree: it stands aside and draws its own lettering (see the class remarks).</summary>
        protected override Widget BuildTree() => ScreenRoot();

        internal override bool StandsAside => true;

        /// <summary>The result this beat holds back, for <c>BS3DGame</c> to put its page up at the end.</summary>
        internal LevelResult Result => _result;

        /// <summary>The message on show, an index into <see cref="CampaignFinaleCopy.Messages"/>.</summary>
        internal int MessageIndex => _messageIndex;

        /// <summary>
        /// Takes the ending this beat comes before and the message it shows, and logs which one (the run log's line is
        /// how a capture says what it photographed). <paramref name="pinned"/> is the testing lever's choice
        /// (<c>finalemessage=</c>), or null for a random one.
        /// </summary>
        internal void Take(LevelResult result, int? pinned)
        {
            _result = result;

            CampaignFinaleCopy.Message[] messages = CampaignFinaleCopy.Messages;
            _messageIndex = pinned is int pin ? Math.Clamp(pin, 0, messages.Length - 1) : Random.Shared.Next(messages.Length);

            _hold = CampaignFinaleCopy.HoldSeconds(messages[_messageIndex]);
            _laidOutWidth = 0;

            Console.WriteLine($"[finale] message {_messageIndex + 1} of {messages.Length}, hint at {messages[_messageIndex].Egg}, held {_hold:0.0} s");
        }

        public override void Enter()
        {
            _phase = Phase.Title;
            _age = _phaseTime = 0f;
            _skipped = _handedOver = false;
            _titleAlpha = _messageAlpha = _scrimAlpha = 0f;
        }

        public override void Update(GameTime gameTime)
        {
            //The shared menu frame, which reads this page's skip keys and hands them to SkipPressed
            base.Update(gameTime);

            if (_handedOver) return;

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _age += elapsed;
            _phaseTime += elapsed;

            if (_phase == Phase.Title && _phaseTime >= TITLE_SECONDS) Begin(Phase.Message);
            else if (_phase == Phase.Message && _phaseTime >= _hold) Begin(Phase.Out);

            //Each layer eases towards where the phase wants it, at the rate of its own leg, so a skip that changes the
            //phase half way through a fade carries on from where the fade had got to rather than popping
            float outRate = 1f / (_skipped ? FADE_OUT_SKIPPED : FADE_OUT);

            _titleAlpha = Approach(_titleAlpha, _phase == Phase.Title ? 1f : 0f,
                elapsed / (_phase == Phase.Title ? TITLE_FADE_IN : DISSOLVE));
            _messageAlpha = Approach(_messageAlpha, _phase == Phase.Message ? 1f : 0f,
                elapsed * (_phase == Phase.Message ? 1f / DISSOLVE : outRate));
            _scrimAlpha = Approach(_scrimAlpha, _phase == Phase.Out ? 0f : 1f,
                elapsed * (_phase == Phase.Out ? outRate : 1f / TITLE_FADE_IN));

            //The hint that says how to skip, the chapter flythrough's own line: it comes up after its delay, so never in
            //the grace's first second, and goes with the message
            _skipHint.UpdateSkipHint(elapsed, _phase != Phase.Out, chapterIntro: true, _age, float.MaxValue);

            if (_phase == Phase.Out && _messageAlpha <= 0f && _scrimAlpha <= 0f)
            {
                _handedOver = true;
                Game.ShowResultAfterFinale(this);
            }
        }

        private void Begin(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        private static float Approach(float value, float target, float step) =>
            value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);

        /// <summary>
        /// A skip key (see <see cref="MenuPage.SkipPressed"/>). Ignored for <see cref="SKIP_GRACE"/> after the page
        /// arrives; the headline goes on to the message, and the message, once it has been up for
        /// <see cref="MIN_READ"/>, goes on to the result page.
        /// </summary>
        internal override void SkipPressed()
        {
            if (_handedOver || _age < SKIP_GRACE) return;

            if (_phase == Phase.Title)
            {
                Console.WriteLine("[skip] campaign finale headline");
                Begin(Phase.Message);
            }
            else if (_phase == Phase.Message && _phaseTime >= MIN_READ)
            {
                Console.WriteLine("[skip] campaign finale message");
                _skipped = true;
                Begin(Phase.Out);
            }
        }

        internal override void DrawAside()
        {
            if (_scrimAlpha <= 0f && _titleAlpha <= 0f && _messageAlpha <= 0f) return;

            Viewport viewport = Game.GraphicsDevice.Viewport;
            if (viewport.Width != _laidOutWidth || viewport.Height != _laidOutHeight) Layout(viewport);

            //The scrim first: the lettering reads over it. Smoothstepped like the lettering, so the three fades are one gesture
            float messageShare = MathHelper.SmoothStep(0f, 1f, _messageAlpha);
            float scrim = MathHelper.SmoothStep(0f, 1f, _scrimAlpha) * MathHelper.Lerp(SCRIM_TITLE, SCRIM_MESSAGE, messageShare);

            SpriteBatch batch = Game.OverlayBatch;
            batch.Begin();

            if (scrim > 0f) batch.Draw(Game.Texel, viewport.Bounds, Color.Black * scrim);

            float titleShare = MathHelper.SmoothStep(0f, 1f, _titleAlpha);
            if (titleShare > 0f) DrawLine(_titleFont, CampaignFinaleCopy.TITLE, _titleAt, BS3DGame.MENU_TEXT, titleShare);

            if (messageShare > 0f)
            {
                float y = _messageTop;

                y = DrawParagraph(_thanksLines, y, BS3DGame.MENU_TEXT_BODY, messageShare) + _paragraphGap;
                DrawParagraph(_hintLines, y, BS3DGame.MENU_TEXT, messageShare);
            }

            batch.End();

            _skipHint.DrawSkipHintAlone(Game.PromptsForPad);
        }

        private float DrawParagraph(string[] lines, float y, Color colour, float alpha)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                DrawLine(_messageFont, lines[i], new Vector2(_messageLeft, y), colour, alpha);
                y += _lineStep;
            }

            return y;
        }

        //One line with the shadow's lift under it: the shadow's alpha is the line's, so it fades with it
        private void DrawLine(SpriteFontBase font, string text, Vector2 at, Color colour, float alpha)
        {
            SpriteBatch batch = Game.OverlayBatch;

            batch.DrawString(font, text, at + new Vector2(_shadowOffset), Color.Black * (0.7f * alpha));
            batch.DrawString(font, text, at, colour * alpha);
        }

        /// <summary>
        /// Fits the lettering to the frame: the headline's face sized so the line spans <see cref="TITLE_WIDTH_SHARE"/> of the
        /// width (or fills <see cref="TITLE_HEIGHT_SHARE"/> of the height, on a narrow window) and centred a little above the
        /// middle, and the message's two paragraphs set by <see cref="SpaceWrap"/> into a column standing in the middle,
        /// left-aligned and ragged right (the typesetting rules: prose is not centred). Run when the frame's size changes,
        /// which is the only time it allocates.
        /// </summary>
        private void Layout(Viewport viewport)
        {
            _laidOutWidth = viewport.Width;
            _laidOutHeight = viewport.Height;
            _shadowOffset = Math.Max(2, Scaled(SHADOW_OFFSET));

            //The headline: a probe size to read the line's proportion off, then the size that makes it as wide as asked
            const float probe = 100f;
            float probeWidth = Game.DisplayFontAt(probe).MeasureString(CampaignFinaleCopy.TITLE).X;
            float size = MathF.Min(probe * viewport.Width * TITLE_WIDTH_SHARE / probeWidth,
                viewport.Height * TITLE_HEIGHT_SHARE / ANTON_LINE_OF_EM);

            _titleFont = Game.DisplayFontAt(size);
            Vector2 titleSize = _titleFont.MeasureString(CampaignFinaleCopy.TITLE);
            _titleAt = new Vector2((viewport.Width - titleSize.X) * 0.5f, viewport.Height * 0.46f - titleSize.Y * 0.5f);

            //The message: the column, then each paragraph wrapped to it, then the block centred on the frame
            CampaignFinaleCopy.Message message = CampaignFinaleCopy.Messages[_messageIndex];
            _messageFont = Game.ReadingFontAt(Scaled(MESSAGE_FONT));

            float column = MathF.Min(Scaled(MESSAGE_COLUMN), viewport.Width * MESSAGE_COLUMN_MAX_SHARE);
            Func<string, float> measure = line => _messageFont.MeasureString(line).X;

            _thanksLines = SpaceWrap.Wrap(message.Thanks, column, measure).Split('\n');
            _hintLines = SpaceWrap.Wrap(message.Hint, column, measure).Split('\n');

            _lineStep = _messageFont.LineHeight + Scaled(MESSAGE_LEADING);
            _paragraphGap = _messageFont.LineHeight;

            float widest = 0f;
            foreach (string line in _thanksLines) widest = MathF.Max(widest, measure(line));
            foreach (string line in _hintLines) widest = MathF.Max(widest, measure(line));

            float height = (_thanksLines.Length + _hintLines.Length) * _lineStep - Scaled(MESSAGE_LEADING) + _paragraphGap;

            _messageLeft = (viewport.Width - widest) * 0.5f;
            _messageTop = (viewport.Height - height) * 0.5f;
        }
    }
}
