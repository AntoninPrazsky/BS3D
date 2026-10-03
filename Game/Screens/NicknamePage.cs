using BS3D.Online;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;
using VerticalAlignment = Myra.Graphics2D.UI.VerticalAlignment;

namespace BS3D.Screens
{
    /// <summary>
    /// The question the game opens with the first time (#763): a nickname for the leaderboards. It stands over the
    /// front end the moment the title card has handed over, before anything else can be reached, and it is
    /// <b>only</b> that question - <b>the name field is the whole consent</b> (the owner's ruling): no privacy
    /// sentence, no line about what is sent, no word about online, a server or a switch. A nickname confirmed turns
    /// the score boards on behind it (<see cref="OnlineSession.AnswerNickname"/>); what that sends is said where it
    /// has always been, on the Settings page's Online tab and on About.
    /// <para>
    /// <b>Three ways out, and only two are answers.</b> Enter on a name is the yes, and so is the button while it reads
    /// <b>OK</b>. <b>Skip</b> is an explicit no, which is never asked about again. <b>The one button is OK or Skip by what
    /// the field holds</b> (the owner's ruling, after he typed a name and found Skip under it): OK when it holds a name,
    /// Skip when it holds nothing or something that is not one (<see cref="NicknameEntry.IsKeepable"/>), and the pad's
    /// A does what the button says. Esc, or the pad's B, is neither answer: the first leaves the setting undecided so
    /// the next launch asks again, and the second counts as Skip (<see cref="NicknamePrompt.Dismiss"/>), so the
    /// question is put to a player at most twice without an answer.
    /// </para>
    /// <para>
    /// <b>The first answer is the only one.</b> A pop off the screen stack only queues, and the manager applies it to
    /// whatever is on top the next frame, so two answers between two frames (an Enter and a click, a key's auto-repeat
    /// under a slow frame) would queue two pops and the second would take the front end off with the dialog, leaving
    /// the scene with no menu; two Esc would also be two dismissals. <c>_answered</c> closes the dialog to input from the
    /// moment it answers.
    /// </para>
    /// <para>
    /// <b>It has the keyboard from the first frame</b> (<see cref="CapturesKeyboard"/>), because a field that has to be
    /// clicked before it takes a letter is one more step in front of the very thing the plate is for. The editing is
    /// <see cref="NicknameEntry"/>'s, the Settings page's own, so the two cannot disagree about what a nickname is. A pad
    /// cannot type, which the hint says; its A and B are the only keys of its that reach the page while it types.
    /// </para>
    /// <para>
    /// <b>Nothing typed in the first <see cref="INPUT_GRACE_SECONDS"/> counts.</b> The title card is skipped by any
    /// key, and a player who mashes keys through it would otherwise hand the plate a burst of letters, an Enter that
    /// complains about an empty name, or an Esc that uses up one of its two dismissals before they have seen it.
    /// </para>
    /// </summary>
    internal sealed class NicknamePage : MenuPage
    {
        //Padding as MenuButton's, so the field reads as the same slab as every control
        private const int FIELD_PADDING_X = 43, FIELD_PADDING_Y = 18;

        //Above the field and under the hint, so the question, the field and the way out read as three things
        private const int FIELD_GAP = 24;

        //A fixed number of the small face's lines, so a complaint in place of the hint cannot move Skip under the
        //pointer: the field is where the player is looking, and the button below it is where they might click. Four,
        //for a hint of three lines: the line pitch is more than the face's LineHeight (the Settings note's finding,
        //#548), and three of them showed two lines and a half of this one (photographed at 1920x1080)
        private const int HINT_LINES = 4;

        /// <summary>How long a key or pad button typed after the plate arrives is ignored - see the class remarks.</summary>
        private const float INPUT_GRACE_SECONDS = 0.8f;

        private readonly NicknameEntry _entry = new();
        private Label _field, _hint, _buttonLabel;
        private float _arrivedAt;

        //Whether the dialog has answered and is leaving: nothing it is given after that counts (see the class remarks)
        private bool _answered;

        public NicknamePage(BS3DGame game) : base(game) { }

        //Nowhere to go back to, and Esc is not a back here but the question's own "not now" (OnTextInput)
        internal override bool CanGoBack => false;

        internal override bool CapturesKeyboard => true;

        public override void Enter()
        {
            _arrivedAt = Game.WallClock;
            _answered = false;
            _entry.Begin(null);
        }

        /// <summary>The caret's blink - a label's text written twice a second, never per frame.</summary>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_field != null && _entry.Tick((float)gameTime.ElapsedGameTime.TotalSeconds)) ShowField();
        }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("NICKNAME"));

            column.Widgets.Add(new Label
            {
                Text = "Pick a nickname for the leaderboards.",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                Wrap = true,
                Width = ColumnWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, FIELD_GAP),
            });

            //Not a button: nothing happens when it is clicked, and a slab that lifts under the pointer promises it does.
            //Its label is a line tall even when empty, so the plate does not change height as the caret blinks
            _field = new Label
            {
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Height = FontBody.LineHeight,
            };

            Panel field = new()
            {
                Background = BS3DGame.MENU_BUTTON_BRUSH,
                Padding = ScaledThickness(FIELD_PADDING_X, FIELD_PADDING_Y),
                Width = ColumnWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            field.Widgets.Add(_field);
            column.Widgets.Add(field);

            _hint = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                Wrap = true,
                Width = ColumnWidth,
                Height = FontSmall.LineHeight * HINT_LINES,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, FIELD_GAP, 0, 0),
            };
            column.Widgets.Add(_hint);

            //One button, OK or Skip by what the field holds (Refresh), through the grace too: a click that skipped the title
            //card must not land on it
            column.Widgets.Add(MenuButton("Skip", () => { if (!InGrace) AnswerWithButton(); }, out _buttonLabel));

            return ScreenRoot(Plate(column));
        }

        internal override void Refresh()
        {
            if (_field == null) return;

            ShowField();

            //What the field wants, or what was wrong with what was kept: the complaint is in the alert colour, the way
            //Settings' "Not set" is, so it reads as something to fix and not as one more line of small print
            _hint.Text = _entry.Problem
                ?? $"{Nickname.MinLength} to {Nickname.MaxLength} letters, digits, spaces, _ or -. Enter confirms. A pad cannot type; its A presses the button.";
            _hint.TextColor = _entry.Problem != null ? BS3DGame.MENU_TEXT_ALERT : BS3DGame.MENU_TEXT_DIM;

            //Skip until there is a name to keep; the button's slab does not change size with its word
            _buttonLabel.Text = _entry.IsKeepable ? "OK" : "Skip";
        }

        private void ShowField() => _field.Text = _entry.Field;

        /// <summary>Whether the plate has been up long enough to be typed into - see <see cref="INPUT_GRACE_SECONDS"/>.</summary>
        internal bool InGrace => Game.WallClock - _arrivedAt < INPUT_GRACE_SECONDS;

        internal override void OnTextInput(char character)
        {
            if (InGrace || _answered) return;

            switch (_entry.Type(character))
            {
                case NicknameKey.Enter:
                    Confirm();
                    break;

                case NicknameKey.Escape:
                    Dismiss();
                    break;

                case NicknameKey.Edited:
                    Refresh();
                    break;
            }
        }

        /// <summary>
        /// The pad's half (#548): A does what the button says - keeps a name that has been typed (a keyboard is still the
        /// only way to type one) and otherwise is Skip, which is the only way a pad reaches the button; B is Esc.
        /// </summary>
        internal override void TypingButtons(bool keep, bool drop)
        {
            if (InGrace || _answered) return;

            if (drop) Dismiss();
            else if (keep) AnswerWithButton();
        }

        /// <summary>What the button does, and the pad's A: OK on a name, Skip on nothing or on something that is not one.</summary>
        private void AnswerWithButton()
        {
            if (_entry.IsKeepable) Confirm();
            else Skip();
        }

        /// <summary>Enter: a name is the answer. One that is not says what is wrong and the dialog stays up.</summary>
        private void Confirm()
        {
            if (_answered) return;

            if (!_entry.TryKeep(out string name))
            {
                Refresh();
                return;
            }

            _answered = true;
            Game.Online.AnswerNickname(name);
            GoBack();
        }

        private void Skip()
        {
            if (_answered) return;

            _answered = true;
            Game.Online.SkipNickname();
            GoBack();
        }

        private void Dismiss()
        {
            if (_answered) return;

            _answered = true;
            Game.Online.DismissNickname();
            GoBack();
        }

        /// <summary>
        /// Testing only (<c>nickprompt=&lt;steps&gt;</c>, #763): <c>type:&lt;text&gt;</c>, <c>enter</c>, <c>esc</c> and
        /// <c>skip</c> in order, through the very handlers the keyboard and the button run, so a run nobody is sitting at
        /// can answer the question the ways a player can. Like <see cref="SettingsPage.ActivateForTesting"/>'s, and for
        /// the same reason: a synthetic keystroke lands in whatever window has the focus and never in this one.
        /// </summary>
        internal void ActivateForTesting(string steps)
        {
            foreach (string step in steps.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
            {
                if (step.StartsWith("type:", System.StringComparison.OrdinalIgnoreCase))
                {
                    foreach (char c in step[5..]) OnTextInput(c);
                    System.Console.WriteLine($"[nickname] Testing: typed '{step[5..]}'");
                    continue;
                }

                switch (step.ToLowerInvariant())
                {
                    case "enter": OnTextInput('\r'); break;
                    case "esc": OnTextInput('\x1b'); break;
                    case "skip": Skip(); break;
                    default:
                        System.Console.WriteLine($"[nickname] Testing: no step '{step}' (type:<text>, enter, esc, skip)");
                        continue;
                }

                System.Console.WriteLine($"[nickname] Testing: did '{step}'");
            }
        }
    }
}
