using BS3D.Online;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;
using System;
using System.Text;
using System.Text.Json.Nodes;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Image = Myra.Graphics2D.UI.Image;
using Label = Myra.Graphics2D.UI.Label;
using VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>Send a Note</b> (#813): the player types a line about what they just saw, and it goes to the author with the
    /// game's context and, unless they untick it, a picture of that moment (the owner's answers, 2026-10-06: anyone may
    /// send one, and the picture is theirs to leave out). Opened from the pause menu and from About.
    /// <para>
    /// <b>The picture is the frame the player was looking at, not the menu over it.</b> The page's first frame draws the
    /// game sharp, with no scrim and no widgets (<see cref="WantsShot"/>), the host reads that frame back at the end of
    /// its Draw (<c>BS3DGame.ServiceNoteShot</c>) and hands it here; only then does the page's own blur ramp in and its
    /// widgets appear, so to the player it reads as a shutter: the frame snaps into focus, then the note arrives with
    /// the picture on it. The pause it came from had already blurred and dimmed that frame, and a shot of the back
    /// buffer as presented would have been of the menu.
    /// </para>
    /// <para>
    /// <b>Typing</b> is the nickname plate's way (#763): the characters Windows sends, the keyboard captured from the
    /// first frame, the letters the menu's faces draw. <b>Enter sends</b>, which is what a short message box in a game
    /// does; Shift+Enter or Ctrl+Enter starts a new line. Esc goes back, and the draft waits for the next time the page
    /// opens in this run. A pad cannot type, which the hint says; its A sends and its B goes back.
    /// </para>
    /// </summary>
    internal sealed class NotePage : MenuPage
    {
        private enum State { Capturing, Writing, Sent }

        //Padding as MenuButton's, so the field reads as the same slab as every control
        private const int FIELD_PADDING_X = 43, FIELD_PADDING_Y = 18;
        private const int GAP = 24;

        /// <summary>Lines of the field: enough for a few sentences to stay in view while they are written.</summary>
        private const int FIELD_LINES = 6;

        /// <summary>
        /// The end of a long note the field shows, with an ellipsis before it: the caret is always at the end, and a
        /// field that scrolled would be machinery for a few hundred characters. Sized to fit <see cref="FIELD_LINES"/>.
        /// </summary>
        private const int FIELD_TAIL = 220;

        /// <summary>The picture's width on the page, in design units: the text column's own.</summary>
        private const int PICTURE_WIDTH = 1000;

        /// <summary>As the pause's: short enough to be over before the hand leaves the key.</summary>
        private const float BLUR_SECONDS = 0.35f;

        /// <summary>
        /// How long a key typed after the picture is ignored: an Enter that opened the page and is still held would
        /// otherwise repeat into it and send a restored draft before the player has seen it.
        /// </summary>
        private const float INPUT_GRACE_SECONDS = 0.4f;

        private const string CARET = "_";
        private const float CARET_PERIOD = 1.0f;

        /// <summary>The highest code point typed: the end of Latin Extended-A, which both menu faces carry (see <see cref="Nickname"/>).</summary>
        private const char LAST_DRAWABLE = 'ſ';
        private const char MISSING_FROM_INTER = 'ŉ';

        private State _state;
        private string _where;
        private JsonObject _context;
        private byte[] _picture;
        private Texture2D _thumbnail;
        private bool _attach;
        private float _shotAt;
        private Guid _sentId;
        private NoteOutcome? _outcome;

        private readonly StringBuilder _draft = new();
        private float _caretClock;
        private bool _caretShown = true;
        private string _problem;

        private Label _field, _hint, _attachLabel, _status;
        private Image _image;
        private Button _send, _back;

        public NotePage(BS3DGame game) : base(game) { }

        /// <summary>
        /// Arms the page for a note from <paramref name="where"/> (<c>pause</c>, <c>about</c>) with the context the
        /// host gathered, before it is pushed: its first frame is the picture's.
        /// </summary>
        internal void Begin(string where, JsonObject context)
        {
            _where = where;
            _context = context;
            _picture = null;
            _thumbnail?.Dispose();
            _thumbnail = null;
            _attach = false;
            _outcome = null;
            _problem = null;
            _state = State.Capturing;
            _draft.Clear().Append(Game.NoteDraft ?? string.Empty);
            InvalidateTree();
        }

        /// <summary>Whether the frame about to be drawn is the picture's: the host reads it back at the end of its Draw.</summary>
        internal bool WantsShot => _state == State.Capturing;

        /// <summary>Whether the note is being written, past the picture - what <c>note=</c>'s steps wait for.</summary>
        internal bool IsWriting => _state == State.Writing && IsBuilt && Game.WallClock - _shotAt >= INPUT_GRACE_SECONDS;

        /// <summary>
        /// The picture taken, or null when the read-back failed. Ticked to go with the note when there is one: the
        /// player leaves it out by unticking it.
        /// </summary>
        internal void TakeShot(byte[] jpeg, Texture2D thumbnail)
        {
            _picture = jpeg;
            _thumbnail = thumbnail;
            _attach = jpeg != null;
            _state = State.Writing;
            _shotAt = Game.WallClock;
            _caretClock = 0f;
            _caretShown = true;
            Rebuild();
        }

        /// <summary>
        /// The tree again, put up without a screen change: the page changes what it shows while it stands (the picture
        /// taken, the note sent), and a tree thrown away is not on the desktop until it is put there (#606's way).
        /// </summary>
        private void Rebuild()
        {
            InvalidateTree();
            if (IsActive) Game.RebuildPage(this, () => _state == State.Sent ? _back : _send);
        }

        internal override bool CapturesKeyboard => _state == State.Writing;

        //No scrim and no blur on the picture's frame; after it, the scrim and a blur of its own over a game, as the pause has
        internal override bool DimsFrame => _state != State.Capturing && Manager != null && Manager.Contains<GameplayScreen>();

        internal override float FrameBlur => _state == State.Capturing || Manager == null || !Manager.Contains<GameplayScreen>() ? 0f
            : MathHelper.SmoothStep(0f, 1f, (Game.WallClock - _shotAt) / BLUR_SECONDS);

        public override void Leave()
        {
            base.Leave();
            //The draft waits for the next time the page opens in this run; a sent note leaves nothing to wait
            Game.NoteDraft = _state == State.Sent ? null : _draft.ToString();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_state == State.Writing && _field != null)
            {
                _caretClock += (float)gameTime.ElapsedGameTime.TotalSeconds;
                bool shown = _caretClock % CARET_PERIOD < CARET_PERIOD * 0.5f;
                if (shown != _caretShown)
                {
                    _caretShown = shown;
                    ShowField();
                }
            }

            //Every outcome is drained while the page is up; one for an earlier note is simply not this one's
            while (Game.Notes != null && Game.Notes.TryTakeOutcome(out NoteOutcome outcome))
                if (_state == State.Sent && outcome.NoteId == _sentId)
                {
                    _outcome = outcome;
                    Refresh();
                }
        }

        protected override Widget BuildTree()
        {
            _field = _hint = _attachLabel = _status = null;
            _image = null;
            _send = _back = null;

            //The picture's frame: nothing of the page over the game
            if (_state == State.Capturing) return ScreenRoot();

            VerticalStackPanel column = MenuColumn();

            if (_state == State.Sent)
            {
                column.Widgets.Add(ScreenHeading("THANK YOU"));
                column.Widgets.Add(_status = BodyLabel(string.Empty, BS3DGame.MENU_TEXT_BODY, ColumnWidth));
                column.Widgets.Add(_back = MenuButton("Back", GoBack));
                return ScreenRoot(Plate(column));
            }

            column.Widgets.Add(ScreenHeading("SEND A NOTE"));
            column.Widgets.Add(BodyLabel("Tell the author what you just saw: what was unclear, what went wrong, what you liked.",
                BS3DGame.MENU_TEXT_BODY, ColumnWidth + Scaled(GAP) + Scaled(PICTURE_WIDTH)));

            HorizontalStackPanel row = new() { Spacing = Scaled(GAP), HorizontalAlignment = HorizontalAlignment.Center };

            VerticalStackPanel left = new() { Spacing = Scaled(GAP), VerticalAlignment = VerticalAlignment.Top };
            //Inter, the face sentences are read in (About's paragraphs), not the display face a nickname is set in
            _field = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT,
                Wrap = true,
                Width = ColumnWidth - Scaled(FIELD_PADDING_X) * 2,
                Height = FontSmall.LineHeight * FIELD_LINES,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Panel field = new()
            {
                Background = BS3DGame.MENU_BUTTON_BRUSH,
                Padding = ScaledThickness(FIELD_PADDING_X, FIELD_PADDING_Y),
                Width = ColumnWidth,
            };
            field.Widgets.Add(_field);
            left.Widgets.Add(field);
            left.Widgets.Add(_hint = BodyLabel(string.Empty, BS3DGame.MENU_TEXT_DIM, ColumnWidth));
            left.Widgets.Add(_send = MenuButton("Send", Send));
            left.Widgets.Add(_back = MenuButton("Back", GoBack));
            row.Widgets.Add(left);

            VerticalStackPanel right = new() { Spacing = Scaled(GAP), VerticalAlignment = VerticalAlignment.Top };
            if (_thumbnail != null)
            {
                int width = Scaled(PICTURE_WIDTH);
                _image = new Image
                {
                    Renderable = new TextureRegion(_thumbnail),
                    Width = width,
                    Height = width * _thumbnail.Height / _thumbnail.Width,
                };
                right.Widgets.Add(_image);
                Button toggle = MenuButton("Picture: sent with the note", ToggleAttach, out _attachLabel);
                toggle.Width = width;
                right.Widgets.Add(toggle);
            }
            else right.Widgets.Add(BodyLabel("No picture: the frame could not be read.", BS3DGame.MENU_TEXT_DIM, Scaled(PICTURE_WIDTH)));
            row.Widgets.Add(right);

            column.Widgets.Add(row);
            return ScreenRoot(Plate(column));
        }

        private Label BodyLabel(string text, Color color, int width) => new()
        {
            Text = text,
            Font = FontSmall,
            TextColor = color,
            Wrap = true,
            Width = width,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        internal override void Refresh()
        {
            if (_state == State.Sent && _status != null)
            {
                _status.Text = _outcome switch
                {
                    null => "Sending…",
                    { Delivery: NoteDelivery.Sent } o when _picture != null && _attach && !o.PictureStored =>
                        "The author has your note. Its picture was not kept: the server holds as many as it can.",
                    { Delivery: NoteDelivery.Sent } => "The author has your note.",
                    { Delivery: NoteDelivery.Kept } => "The game could not reach the server. Your note is saved and goes the next time it can.",
                    { Delivery: NoteDelivery.NoServer } => "This build has no server to send to. Your note is saved in the Notes folder.",
                    _ => "The server did not take this note.",
                };
                return;
            }

            if (_field == null) return;
            ShowField();

            _hint.Text = _problem
                ?? $"Enter sends. Shift+Enter starts a new line. {_draft.Length} / {OnlineNotes.MaxTextLength}. A pad cannot type.";
            _hint.TextColor = _problem != null ? BS3DGame.MENU_TEXT_ALERT : BS3DGame.MENU_TEXT_DIM;

            if (_attachLabel != null)
            {
                _attachLabel.Text = _attach ? "Picture: sent with the note" : "Picture: left out";
                _image.Opacity = _attach ? 1f : 0.35f;
            }
        }

        private void ShowField()
        {
            string text = _draft.Length > FIELD_TAIL ? "…" + _draft.ToString(_draft.Length - FIELD_TAIL, FIELD_TAIL) : _draft.ToString();
            _field.Text = _caretShown ? text + CARET : text;
        }

        private void ToggleAttach()
        {
            if (_picture == null) return;
            _attach = !_attach;
            Refresh();
        }

        internal override void OnTextInput(char character)
        {
            if (_state != State.Writing || Game.WallClock - _shotAt < INPUT_GRACE_SECONDS) return;

            switch (character)
            {
                case '\r' when !ShiftHeld():
                    Send();
                    return;

                case '\r':
                case '\n':
                    if (_draft.Length < OnlineNotes.MaxTextLength) _draft.Append('\n');
                    break;

                case '\x1b':
                    GoBack();
                    return;

                case '\b':
                    if (_draft.Length > 0) _draft.Length--;
                    break;

                default:
                    if (!IsTypable(character) || _draft.Length >= OnlineNotes.MaxTextLength) return;
                    _draft.Append(character);
                    break;
            }

            _problem = null;
            _caretClock = 0f;
            _caretShown = true;
            Refresh();
        }

        //Read on a key's character, not per frame: the one place the page asks the keyboard anything
        private static bool ShiftHeld()
        {
            KeyboardState keyboard = Keyboard.GetState();
            return keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        }

        /// <summary>A letter, digit, space or mark the menu's faces draw: Basic Latin, Latin-1 and Latin Extended-A.</summary>
        internal static bool IsTypable(char c) => c >= ' ' && c <= LAST_DRAWABLE && c != MISSING_FROM_INTER && !char.IsControl(c);

        internal override void TypingButtons(bool keep, bool drop)
        {
            if (drop) GoBack();
            else if (keep) Send();
        }

        private void Send()
        {
            if (_state != State.Writing) return;

            string text = _draft.ToString().Trim();
            if (text.Length == 0)
            {
                _problem = "Write something first.";
                Refresh();
                return;
            }

            _context["where"] = _where;
            _context["picture"] = _attach && _picture != null;
            _sentId = Game.Notes.Send(text, _context, _attach ? _picture : null);
            _draft.Clear();
            _state = State.Sent;
            _outcome = null;
            Rebuild();
        }

        /// <summary>
        /// Testing only (<c>note=&lt;seconds&gt;:&lt;steps&gt;</c>, #813): <c>type:&lt;text&gt;</c>, <c>newline</c>,
        /// <c>untick</c> and <c>send</c> in order, through the very handlers the keyboard and the buttons run, as the
        /// nickname plate's <c>nickprompt=</c> does and for its reason: a synthetic keystroke lands in whatever window
        /// has the focus.
        /// </summary>
        internal void ActivateForTesting(string steps)
        {
            foreach (string step in steps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (step.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (char c in step[5..]) OnTextInput(c);
                    Console.WriteLine($"[note] Testing: typed '{step[5..]}'");
                    continue;
                }

                switch (step.ToLowerInvariant())
                {
                    case "newline": OnTextInput('\n'); break;
                    case "untick": if (_attach) ToggleAttach(); break;
                    case "send": Send(); break;
                    default:
                        Console.WriteLine($"[note] Testing: no step '{step}' (type:<text>, newline, untick, send)");
                        continue;
                }
                Console.WriteLine($"[note] Testing: did '{step}'");
            }
        }
    }
}
