using BS3D.Online;
using BS3D.Screens;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1141: a phone types through the system's text dialog (#1052), and its answer was typed into the page key by key:
    /// 2000 backspaces to clear the field and then each character, every key a refresh of the page, which on the note
    /// re-laid the field and re-wrapped its hint over a draft of up to 1000 characters. Now the page takes the whole text
    /// as one edit and refreshes once (<c>MenuPage.TakeTypedText</c>). What a field may then hold is held here, through the
    /// two device-free editors the pages share, against what the old keystrokes made of the same answer; that the phone's
    /// typing step (<c>PhoneTyping</c>, #1182) hands the answer over in one call is held on its source, the rule seen to fire
    /// on the loop it replaced, and stepped in <c>PhoneTypingStepTests</c>.
    /// </summary>
    public class PhoneTypingDialogTests
    {
        //What the old path sent first: more backspaces than any field holds
        private const int OLD_CLEAR = 2000;

        [Theory]
        [InlineData("", "Novak")]
        [InlineData("Tester", "Novák")]
        [InlineData("Tester", "")]
        [InlineData("x", "a name far longer than any name may be")]
        [InlineData("Old", "no!marks?here")]
        public void A_nickname_answer_is_what_its_keystrokes_made(string before, string answer)
        {
            NicknameEntry typed = new();
            typed.Begin(before);
            for (int i = 0; i < OLD_CLEAR; i++) typed.Type('\b');
            foreach (char c in answer) typed.Type(c);

            NicknameEntry taken = new();
            taken.Begin(before);
            taken.Replace(answer);

            Assert.Equal(typed.Draft, taken.Draft);
            Assert.True(taken.Draft.Length <= Nickname.MaxLength);
        }

        [Fact]
        public void A_nickname_answer_ends_nothing_and_clears_the_complaint()
        {
            NicknameEntry entry = new();
            entry.Begin("ab");
            Assert.False(entry.TryKeep(out _));
            Assert.NotNull(entry.Problem);

            //Enter, Escape and Backspace inside the text are not keys: the page decides what OK means
            entry.Replace("No\rva\u001bk\b");
            Assert.Equal("Novak", entry.Draft);
            Assert.Null(entry.Problem);
            Assert.True(entry.CaretShown);
        }

        [Theory]
        [InlineData("", "What I saw: the ball went through the wall.")]
        [InlineData("an older draft", "Two lines\nof a note")]
        [InlineData("", "Not drawn: € and ✓, drawn: ž and ſ")]
        public void A_note_answer_is_what_its_keystrokes_made(string before, string answer)
        {
            StringBuilder typed = new(before);
            for (int i = 0; i < OLD_CLEAR; i++) NoteText.Edit(typed, '\b', OnlineNotes.MaxTextLength);
            foreach (char c in answer) NoteText.Edit(typed, c, OnlineNotes.MaxTextLength);

            StringBuilder taken = new(before);
            NoteText.Replace(taken, answer, OnlineNotes.MaxTextLength);

            Assert.Equal(typed.ToString(), taken.ToString());
        }

        [Fact]
        public void A_note_answer_keeps_its_lines_and_stops_at_the_limit()
        {
            StringBuilder draft = new("old");

            //The dialog's line breaks are the note's, a carriage return included and CR LF one break: OK does not send a note (TypingDialogEnters)
            NoteText.Replace(draft, "one\r\ntwo\rthree", OnlineNotes.MaxTextLength);
            Assert.Equal("one\ntwo\nthree", draft.ToString());

            NoteText.Replace(draft, new string('a', OnlineNotes.MaxTextLength + 50), OnlineNotes.MaxTextLength);
            Assert.Equal(OnlineNotes.MaxTextLength, draft.Length);

            //Escape and Backspace inside the text are not keys
            NoteText.Replace(draft, "a\u001bb\bc", OnlineNotes.MaxTextLength);
            Assert.Equal("abc", draft.ToString());
        }

        //The old path: the answer typed into the page key by key, a backspace first
        private static readonly Regex KeyByKey = new(@"OnTextInput\('\\b'\)|foreach\s*\(char \w+ in text\)\s*page\.OnTextInput", RegexOptions.Compiled);

        [Fact]
        public void The_dialogs_answer_is_handed_to_the_page_in_one_call()
        {
            string menu = File.ReadAllText(Path.Combine(FindUnderRepository(Path.Combine("Game", "Screens")), "PhoneTyping.cs"));

            Assert.DoesNotMatch(KeyByKey, menu);
            Assert.Contains("page.TakeTypedText(text);", menu, StringComparison.Ordinal);
        }

        [Fact]
        public void The_rule_fires_on_the_loop_it_replaced()
        {
            Assert.Matches(KeyByKey, "                for (int i = 0; i < TYPING_DIALOG_CLEAR; i++) page.OnTextInput('\\b');");
            Assert.Matches(KeyByKey, "                foreach (char c in text) page.OnTextInput(c);");
            Assert.DoesNotMatch(KeyByKey, "                if (page.TypingDialogEnters) page.OnTextInput('\\r');");
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
