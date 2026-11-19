namespace BS3D.Screens
{
    /// <summary>
    /// What the phone's typing step asks of a page (<see cref="PhoneTyping"/>, #1182): <see cref="MenuPage"/>'s own members of
    /// the same names, handed over through the step's interface so the step stays free of Myra and a test can drive it.
    /// </summary>
    internal abstract partial class MenuPage : PhoneTyping.IPage
    {
        bool PhoneTyping.IPage.CapturesKeyboard => CapturesKeyboard;
        (string Title, string Description, string Text) PhoneTyping.IPage.TypingDialog => TypingDialog;
        void PhoneTyping.IPage.TakeTypedText(string text) => TakeTypedText(text);
        bool PhoneTyping.IPage.TypingDialogEnters => TypingDialogEnters;
        bool PhoneTyping.IPage.TypingDialogEscapes => TypingDialogEscapes;
        void PhoneTyping.IPage.OnTextInput(char character) => OnTextInput(character);
    }
}
