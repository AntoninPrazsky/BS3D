using Myra.Graphics2D.UI;
using System;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// "Quit BS3D?" — asked before the game closes, from the pause page and the main menu alike (#826). A player wrote
    /// from the pause on v0.3.6 that the game should always ask first, and this reverses #383's "no confirm dialog": the
    /// machinery that comment declined is this one page, pushed over the page that opened it the way Settings and the
    /// note are, so backing out of it is the stack's ordinary pop.
    /// <para>
    /// <b>Cancel is first and is where the cursor lands</b> (<see cref="NavArrival"/>): the key or button that opened
    /// this page, pressed again without reading, cancels rather than quits. Escape and the pad's B are the stack's own
    /// back, which here is Cancel. Only the Quit entry closes the game.
    /// </para>
    /// <para>
    /// <b>The line under the heading says what is lost, and only when something is.</b> A session lives in memory: the
    /// level being played, or one kept behind the main menu (#650), is gone after a relaunch, where Continue starts that
    /// level again from the save. With no session there is nothing to warn about, and a warning that is always there is
    /// one nobody reads. The window's close button and Alt+F4 still close the game at once: they are the operating
    /// system's gestures, made on purpose, and the player's note was about the menu entry.
    /// </para>
    /// </summary>
    internal sealed class QuitConfirmPage : MenuPage
    {
        private Button _cancel;
        private Label _loss;

        public QuitConfirmPage(BS3DGame game) : base(game) { }

        internal override Button NavArrival => _cancel;

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            column.Widgets.Add(ScreenHeading("QUIT BS3D?"));

            //Hidden rather than blanked when there is nothing to lose (Refresh), so the page is shorter instead of
            //holding an empty line under the heading
            _loss = new Label
            {
                Text = "This run will not be kept.",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                Wrap = true,
                Width = ColumnWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                //Under a centred heading and over centred entries, a short line set flush left read as a stray
                TextAlign = FontStashSharp.RichText.TextHorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 43),
            };
            column.Widgets.Add(_loss);

            _cancel = MenuButton("Cancel", Cancel);
            column.Widgets.Add(_cancel);
            column.Widgets.Add(MenuButton("Quit", Quit));

            //On a plate, because over the main menu nothing dims the turning scene behind it (the pause's scrim only
            //darkens a stopped game), and a dialog should read as one whichever page opened it
            return ScreenRoot(Plate(column));
        }

        /// <summary>The Cancel entry: back to the page that asked, the same pop Escape makes.</summary>
        internal void Cancel() => GoBack();

        /// <summary>The Quit entry, the one way off this page that closes the game.</summary>
        internal void Quit()
        {
            Console.WriteLine("[quit] Quit confirmed");
            Game.Exit();
        }

        internal override void Refresh()
        {
            if (_loss == null) return;

            _loss.Visible = Game.HasSession;
        }
    }
}
