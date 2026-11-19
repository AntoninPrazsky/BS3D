using BS3D.Screens;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1182: on a phone a page types through the system's text dialog (#1052), and a tap on the dialog's Cancel was the
    /// page's Esc everywhere, so on Write to the Author it closed the whole page under a player who only wanted to read it
    /// or think again about a word. The step is <see cref="PhoneTyping"/>, with the dialog behind the functions it is handed;
    /// here it is stepped with a dialog of the test's own, answered OK or Cancel, over a page that answers as the note and the
    /// nickname pages do. That the note page says a cancel is not its Esc is held on its source, since the page builds Myra
    /// widgets no test constructs.
    /// </summary>
    public class PhoneTypingStepTests
    {
        //A page as the step sees it: what it was handed, and what its Esc and Enter did
        private sealed class Page : PhoneTyping.IPage
        {
            public Page(bool enters, bool escapes, string text)
            {
                TypingDialogEnters = enters;
                TypingDialogEscapes = escapes;
                Text = text;
            }

            public string Text { get; private set; }
            public int Edits { get; private set; }
            public bool Left { get; private set; }
            public bool Entered { get; private set; }
            public bool OnField { get; set; }

            public bool CapturesKeyboard => !Left;
            public (string Title, string Description, string Text) TypingDialog => ("Write to the Author", "", Text);
            public bool TypingDialogEnters { get; }
            public bool TypingDialogEscapes { get; }

            public void TakeTypedText(string text)
            {
                Text = text;
                Edits++;
            }

            //The note's Esc and the plate's leave the page; Enter is a send or a keep
            public void OnTextInput(char character)
            {
                if (character == '\x1b') Left = true;
                else if (character == '\r') Entered = true;
            }
        }

        //The system's dialog: how often it was opened, on what, and its answer still to come
        private sealed class Dialog
        {
            public int Opened { get; private set; }
            public string OpenedOn { get; private set; }
            public TaskCompletionSource<string> Answer { get; private set; }

            public bool Up => Answer != null && !Answer.Task.IsCompleted;

            public Task<string> Show(string title, string description, string text)
            {
                Opened++;
                OpenedOn = text;
                Answer = new TaskCompletionSource<string>();
                return Answer.Task;
            }
        }

        private static PhoneTyping Step(Dialog dialog) => new(dialog.Show, () => dialog.Up, page => ((Page)page).OnField);

        //The note's: OK puts the text in the field and sends nothing, a cancel is not its Esc
        private static Page Note(string text) => new(enters: false, escapes: false, text);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_cancel_leaves_the_note_page_and_its_text_as_they_were(bool cancelledTask)
        {
            Dialog dialog = new();
            PhoneTyping typing = Step(dialog);
            Page note = Note("an older draft");

            typing.Step(note);
            Assert.Equal(1, dialog.Opened);
            Assert.Equal("an older draft", dialog.OpenedOn);

            //MonoGame answers a cancel with null; a dialog torn down answers with a cancelled task
            if (cancelledTask) dialog.Answer.SetCanceled();
            else dialog.Answer.SetResult(null);
            typing.Step(note);

            Assert.False(note.Left);
            Assert.True(note.CapturesKeyboard);
            Assert.Equal("an older draft", note.Text);
            Assert.Equal(0, note.Edits);
            Assert.False(note.Entered);
        }

        [Fact]
        public void A_cancelled_dialog_is_not_asked_again_until_a_finger_is_on_the_field()
        {
            Dialog dialog = new();
            PhoneTyping typing = Step(dialog);
            Page note = Note("Hello");

            typing.Step(note);
            dialog.Answer.SetResult(null);
            for (int frame = 0; frame < 10; frame++) typing.Step(note);
            Assert.Equal(1, dialog.Opened);

            //A finger on the field opens it again, on the text as it stood
            note.OnField = true;
            typing.Step(note);
            Assert.Equal(2, dialog.Opened);
            Assert.Equal("Hello", dialog.OpenedOn);
        }

        [Fact]
        public void OK_puts_the_text_in_the_note_in_one_edit_and_sends_nothing()
        {
            Dialog dialog = new();
            PhoneTyping typing = Step(dialog);
            Page note = Note("old");

            typing.Step(note);
            dialog.Answer.SetResult("What I saw: the ball went through the wall.");
            typing.Step(note);

            Assert.Equal("What I saw: the ball went through the wall.", note.Text);
            Assert.Equal(1, note.Edits);
            Assert.False(note.Entered);
            Assert.False(note.Left);
        }

        [Fact]
        public void On_the_nickname_a_cancel_is_its_Esc_and_OK_its_Enter()
        {
            Dialog dialog = new();
            PhoneTyping typing = Step(dialog);

            Page cancelled = new(enters: true, escapes: true, "Tester");
            typing.Step(cancelled);
            dialog.Answer.SetResult(null);
            typing.Step(cancelled);
            Assert.True(cancelled.Left);
            Assert.Equal("Tester", cancelled.Text);

            Page kept = new(enters: true, escapes: true, "Tester");
            typing.Step(kept);
            dialog.Answer.SetResult("Novak");
            typing.Step(kept);
            Assert.Equal("Novak", kept.Text);
            Assert.True(kept.Entered);
            Assert.False(kept.Left);
        }

        [Fact]
        public void An_answer_for_a_page_that_has_gone_is_nobodys()
        {
            Dialog dialog = new();
            PhoneTyping typing = Step(dialog);
            Page note = Note("draft"), other = Note("other");

            typing.Step(note);
            dialog.Answer.SetResult("late words");
            typing.Step(other);

            Assert.Equal("draft", note.Text);
            Assert.Equal("other", other.Text);
            Assert.Equal(0, other.Edits);
        }

        //The note page's own word on it, read off its source: the page is a Myra tree no test constructs
        private static readonly Regex NoteKeepsOnCancel = new(@"override bool TypingDialogEscapes => false;", RegexOptions.Compiled);

        [Fact]
        public void The_note_page_says_a_cancel_is_not_its_Esc()
        {
            string note = File.ReadAllText(Path.Combine(FindUnderRepository(Path.Combine("Game", "Screens")), "NotePage.cs"));
            Assert.Matches(NoteKeepsOnCancel, note);
        }

        [Fact]
        public void The_rule_fires_on_a_page_that_does_not_say_it()
        {
            Assert.DoesNotMatch(NoteKeepsOnCancel, "        internal override bool TypingDialogEnters => false;");
            Assert.DoesNotMatch(NoteKeepsOnCancel, "        internal override bool TypingDialogEscapes => true;");
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
