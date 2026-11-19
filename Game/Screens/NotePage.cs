using BS3D.Online;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;
using System;
using System.Collections.Generic;
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
    /// send one, and the picture is theirs to leave out). Opened from the pause menu, About and Extras, by the entry
    /// <see cref="BS3DGame.NoteEntry"/> builds (its words say to whom, and it is teal).
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
    /// opens in this run. A pad cannot type; its A sends and its B goes back. A phone types through its own dialog, which
    /// a finger on the field opens (<c>BS3DGame.StepPhoneTyping</c>), and its OK only puts the text in the field.
    /// </para>
    /// <para>
    /// <b>The line under the field speaks to the hand in use</b> (#1149, <see cref="NoteHint"/>): a finger's on a phone
    /// (tap the field, then Send), the keys for a keyboard, a finger off the phone's included, and for a pad the keys with
    /// the sentence that it cannot type. It follows the hand while the page stands, at a height measured for the tallest
    /// of the lines this platform can show, so a hand picked up does not move Send under the pointer or the finger.
    /// </para>
    /// </summary>
    internal sealed class NotePage : MenuPage
    {
        private enum State { Capturing, Writing, Sent }

        //Padding as MenuButton's, so the field reads as the same slab as every control
        private const int FIELD_PADDING_X = 43, FIELD_PADDING_Y = 18;
        private const int GAP = 24;

        /// <summary>
        /// Lines of the field: enough for a few sentences to stay in view while they are written. A longer note shows
        /// its last lines with an ellipsis before them (<see cref="FieldTail"/>): the caret is always at the end, and a
        /// field that scrolled would be machinery for a few hundred characters.
        /// </summary>
        private const int FIELD_LINES = 6;

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

        //The draft laid out in the field's lines with the caret at its end (FieldTail), null when the draft or the field
        //has changed since: laid out once per change, never per caret blink
        private string _laid;
        private int _fieldWidth;

        private float _caretClock;
        private bool _caretShown = true;
        private string _problem;

        private Label _field, _hint, _status;

        //The hand the hint was last written for (NoteHint.HandFor), so a hand picked up rewrites it once and not per frame
        private NoteHint.Hand _hintHand;

        /// <summary>The picture's checkbox (#816): ticked while the picture goes with the note.</summary>
        private CheckGlyph _attachCheck;
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
            _laid = null;
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

                //A hand picked up while the page stands: its words, once
                if (_problem == null && HintHand != _hintHand) Refresh();
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
            _field = _hint = _status = null;
            _attachCheck = null;
            _image = null;
            _send = _back = null;

            //The picture's frame: nothing of the page over the game
            if (_state == State.Capturing) return ScreenRoot();

            VerticalStackPanel column = MenuColumn();

            if (_state == State.Sent)
            {
                column.Widgets.Add(ScreenHeading("THANK YOU"));
                column.Widgets.Add(_status = BodyLabel(string.Empty, BS3DGame.MENU_TEXT_BODY, ColumnWidth));
                column.Widgets.Add(_back = PhoneTarget(BackButton()));
                return ScreenRoot(Plate(column));
            }

            column.Widgets.Add(ScreenHeading("WRITE TO THE AUTHOR"));
            column.Widgets.Add(BodyLabel("Tell the author what you just saw: what was unclear, what went wrong, what you liked.",
                BS3DGame.MENU_TEXT_BODY, ColumnWidth + Scaled(GAP) + Scaled(PICTURE_WIDTH)));

            HorizontalStackPanel row = new() { Spacing = Scaled(GAP), HorizontalAlignment = HorizontalAlignment.Center };

            VerticalStackPanel left = new() { Spacing = Scaled(GAP), VerticalAlignment = VerticalAlignment.Top };
            //Inter, the face sentences are read in (About's paragraphs), not the display face a nickname is set in.
            //Every line FieldTail hands it is measured to fit, so its own wrap is only a backstop
            _fieldWidth = ColumnWidth - Scaled(FIELD_PADDING_X) * 2;
            _laid = null;
            _field = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT,
                Wrap = true,
                Width = _fieldWidth,
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
            //As tall as the tallest thing it can say here, measured as it is laid out (#806's way), at the count's widest:
            //the line changes with the hand and with the complaint, and Send under it must not move. Only this platform's
            //hands (NoteHint.HandsFor), so a phone keeps no row for the pad's longer line
            _hint = BodyLabel(string.Empty, BS3DGame.MENU_TEXT_DIM, ColumnWidth);
            _hint.VerticalAlignment = VerticalAlignment.Top;
            IReadOnlyList<NoteHint.Hand> hands = NoteHint.HandsFor(PhoneHand);
            string[] hintLines = new string[hands.Count + 1];
            for (int i = 0; i < hands.Count; i++)
                hintLines[i] = NoteHint.Line(hands[i], OnlineNotes.MaxTextLength, OnlineNotes.MaxTextLength);
            hintLines[hands.Count] = NoteHint.NOTHING_WRITTEN;
            _hint.Height = ProseHeight(_hint, hintLines);
            left.Widgets.Add(_hint);
            //Each a touch target tall on the phone (#1181)
            left.Widgets.Add(_send = PhoneTarget(MenuButton("Send", Send)));
            left.Widgets.Add(_back = PhoneTarget(BackButton()));
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
                //A checkbox since #816: the tick says whether it goes, where two sentences took turns on the button
                Button toggle = CheckBox("Send the picture", ToggleAttach, out _attachCheck, out _);
                toggle.Width = width;
                right.Widgets.Add(PhoneTarget(toggle));
            }
            else right.Widgets.Add(BodyLabel("No picture: the frame could not be read.", BS3DGame.MENU_TEXT_DIM, Scaled(PICTURE_WIDTH)));
            row.Widgets.Add(right);

            column.Widgets.Add(row);
            return ScreenRoot(Plate(column));
        }

        //A line of prose under the field or the sent note: its text is written with SetProse, here and whenever it changes,
        //like every label that wraps (#855)
        private Label BodyLabel(string text, Color color, int width)
        {
            Label label = new()
            {
                Font = FontSmall,
                TextColor = color,
                Wrap = true,
                Width = width,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            SetProse(label, text);
            return label;
        }

        internal override void Refresh()
        {
            if (_state == State.Sent && _status != null)
            {
                SetProse(_status, _outcome switch
                {
                    null => "Sending…",
                    { Delivery: NoteDelivery.Sent } o when _picture != null && _attach && !o.PictureStored =>
                        "The author has your note. Its picture was not kept: the server holds as many as it can.",
                    { Delivery: NoteDelivery.Sent } => "The author has your note.",
                    { Delivery: NoteDelivery.Kept } => "The game could not reach the server. Your note is saved and goes the next time it can.",
                    { Delivery: NoteDelivery.NoServer } => "This build has no server to send to. Your note is saved in the Notes folder.",
                    _ => "The server did not take this note.",
                });
                return;
            }

            if (_field == null) return;
            ShowField();

            //Chosen into lines on every keystroke, once for a phone's dialog answer (TakeTypedText) and once for a hand picked
            //up (Update), which is when Refresh runs: a few hundred measurements, never per frame.
            //The count is tied to its limit, so a line never ends on "5" and starts on "/ 1000" (seen at 1080p)
            _hintHand = HintHand;
            SetProse(_hint, _problem ?? NoteHint.Line(_hintHand, _draft.Length, OnlineNotes.MaxTextLength));
            _hint.TextColor = _problem != null ? BS3DGame.MENU_TEXT_ALERT : BS3DGame.MENU_TEXT_DIM;

            if (_attachCheck != null)
            {
                _attachCheck.Checked = _attach;
                _image.Opacity = _attach ? 1f : 0.35f;
            }
        }

        //The hand the line speaks to, a phone's always a finger's (NoteHint.HandFor)
        private NoteHint.Hand HintHand => NoteHint.HandFor(Game.PromptsForPad, PhoneHand);

        //The phone, or the "touch" argument laying its hand out on a monitor: fixed for the run, so the hint's measured
        //height and the line it shows agree
        private bool PhoneHand => BS3DGame.OnPhone || Game.TouchPinned;

        private void ShowField()
        {
            //Counted in measured lines, not characters (#813): the 220-character tail this replaced ran past six lines on
            //a note of short lines or long words, and the line being typed went on below the field. The caret is laid out
            //with the text, so its line is the last one shown, and a blink only takes it off the end.
            _laid ??= FieldTail.Show(_draft + CARET, _fieldWidth, FIELD_LINES, line => FontSmall.MeasureString(line).X);
            _field.Text = _caretShown ? _laid : _laid[..^CARET.Length];
        }

        private void ToggleAttach()
        {
            if (_picture == null) return;
            _attach = !_attach;
            Refresh();
        }

        internal override (string Title, string Description, string Text) TypingDialog =>
            ("Write to the Author", "What you want the author to know", _draft.ToString());

        internal override bool TypingDialogEnters => false;

        internal override Widget TypingField => _field;

        //Whether a key or the phone's dialog may edit the draft: past the picture and its grace
        private bool TakesTyping => _state == State.Writing && Game.WallClock - _shotAt >= INPUT_GRACE_SECONDS;

        internal override void OnTextInput(char character)
        {
            if (!TakesTyping) return;

            switch (character)
            {
                case '\r' when !ShiftHeld():
                    Send();
                    return;

                case '\x1b':
                    GoBack();
                    return;
            }

            //A new line, Backspace or a character the faces draw (NoteText); a refused one changes nothing
            if (!NoteText.Edit(_draft, character, OnlineNotes.MaxTextLength)) return;
            Edited();
        }

        internal override void TakeTypedText(string text)
        {
            if (!TakesTyping) return;

            NoteText.Replace(_draft, text, OnlineNotes.MaxTextLength);
            Edited();
        }

        //The draft changed: the complaint goes, the caret is on again, and the field is laid out afresh
        private void Edited()
        {
            _problem = null;
            _laid = null;
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
                _problem = NoteHint.NOTHING_WRITTEN;
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
        /// <c>untick</c>, <c>send</c> and <c>back</c> in order, through the very handlers the keyboard and the buttons run, as the
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
                    case "back":
                        Console.WriteLine("[note] Testing: did 'back'");
                        GoBack();
                        return;
                    default:
                        Console.WriteLine($"[note] Testing: no step '{step}' (type:<text>, newline, untick, send, back)");
                        continue;
                }
                Console.WriteLine($"[note] Testing: did '{step}'");
            }
        }
    }
}
