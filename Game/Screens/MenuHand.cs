namespace BS3D.Screens
{
    /// <summary>
    /// Which hand the menus' prompts are drawn for when a run starts (#799, #1052, #1181): the keyboard's, unless the run
    /// says otherwise. A phone starts on a finger's: it has no keys, and the keyboard's Esc drawn on Cancel and Back until
    /// the first touch was a key the player cannot press (the phone pass of 2026-11-18, the Quit confirm and Settings).
    /// The <c>pad</c> argument pins the pad's and wins; <c>touch</c> pins a finger's on any screen. Pure, so a test holds it.
    /// </summary>
    internal static class MenuHand
    {
        /// <summary>Whether the menus' prompts start drawn for a finger: on a phone, or pinned by <c>touch</c>, never under <c>pad</c>.</summary>
        internal static bool StartsOnTouch(bool onPhone, bool touchArgument, bool padArgument) =>
            !padArgument && (touchArgument || onPhone);
    }
}
