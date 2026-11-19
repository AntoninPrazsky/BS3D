using System;
using System.Threading.Tasks;

namespace BS3D.Screens
{
    /// <summary>
    /// A phone's typing (#1052): the window sends no characters on Android, and Myra's text box opens no soft keyboard, so a
    /// page that takes the keyboard (the nickname plate, the Settings nickname row, the note) gets the system's own text
    /// dialog, opening on what is typed so far. Kept apart from the host with the dialog behind two functions handed in
    /// (<c>KeyboardInput.Show</c> and <c>IsVisible</c> on the phone), so a test steps it with a dialog of its own
    /// (<c>PhoneTypingStepTests</c>).
    /// <para>
    /// <b>The answer takes the field's place in one edit</b> (<see cref="IPage.TakeTypedText"/>, #1141), then comes the page's
    /// Enter where OK means it (<see cref="IPage.TypingDialogEnters"/>). <b>A cancel is the page's Esc only where the page
    /// says so</b> (<see cref="IPage.TypingDialogEscapes"/>, #1182): the nickname plate's Esc dismisses it and the Settings
    /// row's keeps the name there was, which is what a cancel means there; the note's Esc leaves the page, and a cancel that
    /// did that threw the whole page away under a player who only wanted to read it or change their mind about one word, so
    /// on the note a cancel leaves the page and its text as they were before the dialog opened.
    /// </para>
    /// <para>
    /// <b>Asked once per page</b> until the page stops taking the keyboard or a finger lands on its field again, so a name the
    /// page refuses, or a cancelled dialog, is not asked for in a loop.
    /// </para>
    /// </summary>
    internal sealed class PhoneTyping
    {
        /// <summary>What the step asks of the page that is up: <c>MenuPage</c>'s members of the same names.</summary>
        internal interface IPage
        {
            /// <summary>Whether the page has taken the keyboard to type into.</summary>
            bool CapturesKeyboard { get; }

            /// <summary>The dialog's title, the line under it and the text it opens on.</summary>
            (string Title, string Description, string Text) TypingDialog { get; }

            /// <summary>The dialog's answer, the field's whole text, as one edit.</summary>
            void TakeTypedText(string text);

            /// <summary>Whether the dialog's OK is the page's Enter.</summary>
            bool TypingDialogEnters { get; }

            /// <summary>Whether the dialog's Cancel is the page's Esc.</summary>
            bool TypingDialogEscapes { get; }

            /// <summary>A key the page reads as typed: the Enter and the Esc above.</summary>
            void OnTextInput(char character);
        }

        private readonly Func<string, string, string, Task<string>> _show;
        private readonly Func<bool> _shown;
        private readonly Func<IPage, bool> _touchOnField;

        //The page the dialog was opened for, and the dialog's answer while it is up
        private IPage _for;
        private Task<string> _dialog;

        /// <param name="show">Opens the dialog on a title, a line under it and a text, and answers with the text or null for
        /// a cancel (<c>KeyboardInput.Show</c>).</param>
        /// <param name="shown">Whether a dialog of the system's is up already (<c>KeyboardInput.IsVisible</c>).</param>
        /// <param name="touchOnField">Whether a finger is down on the page's field: asked only of the page the dialog was
        /// last opened for, while it still takes the keyboard, so the touch panel is read only when a finger could reopen it.</param>
        internal PhoneTyping(Func<string, string, string, Task<string>> show, Func<bool> shown, Func<IPage, bool> touchOnField)
        {
            _show = show;
            _shown = shown;
            _touchOnField = touchOnField;
        }

        /// <summary>One frame of the menus: <paramref name="page"/> is the page that is up, null for none that types.</summary>
        internal void Step(IPage page)
        {
            if (_dialog != null)
            {
                if (!_dialog.IsCompleted) return;

                string text = _dialog.Status == TaskStatus.RanToCompletion ? _dialog.Result : null;
                _dialog = null;

                //An answer for a page that has gone, or no longer types, is nobody's
                if (page == null || page != _for || !page.CapturesKeyboard) return;

                if (text == null)
                {
                    if (page.TypingDialogEscapes) page.OnTextInput('\x1b');
                    return;
                }

                page.TakeTypedText(text);
                if (page.TypingDialogEnters) page.OnTextInput('\r');
                return;
            }

            //Another page, or a finger on the field again, asks afresh: not a finger anywhere, which on the note is its Send
            if (page == null || !page.CapturesKeyboard || page != _for || _touchOnField(page)) _for = null;

            if (page == null || !page.CapturesKeyboard || _for == page || _shown()) return;

            _for = page;
            (string title, string description, string current) = page.TypingDialog;
            _dialog = _show(title, description, current);
        }
    }
}
