using FontStashSharp;
using FontStashSharp.RichText;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Screens;
using System;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// One of the game's Myra screens on the <see cref="ScreenManager"/>'s stack: the splash, the main menu,
    /// the level picker, the pause menu, settings, the scene picker, about, the end-of-level result.
    /// <para>
    /// <b>What the stack buys here is the relationships, not the swapping.</b> Settings opened from a pause
    /// used to return to the pause because the code asked <c>_state == Paused</c>, and it dimmed the frame for
    /// the same reason — the screen had to work out where it had come from. On a stack it does not have to:
    /// the pause is literally underneath it, so backing out is a pop and "am I over a stopped game" is a
    /// question about the stack rather than about a state enum somewhere else.
    /// </para>
    /// <para>
    /// The play loop is a screen too now (<see cref="GameplayScreen"/>, #65), and the two flags below are
    /// what the whole stack was built for: every page shows the world behind it — the orbiting scene, or a
    /// stopped game — so it draws what is underneath; and whether what is underneath still <i>runs</i> is
    /// exactly whether that is a scene or a game.
    /// </para>
    /// </summary>
    internal abstract class MenuPage : Screen, IFrameBlurSource
    {
        protected readonly BS3DGame Game;

        /// <summary>
        /// Always: a menu page is an overlay, never the whole picture. The front end shows the scene turning
        /// behind it, and a pause shows the game it stopped — the manager goes on drawing down the stack.
        /// </summary>
        public override bool DrawsUnderlying => true;

        /// <summary>
        /// Whether what is underneath keeps running, and the stack itself answers: over the front end's
        /// backdrop the scene goes on turning, while any page over a <see cref="GameplayScreen"/> — the
        /// pause, settings opened over it — freezes it. This is the case the flag exists for: a pause has to
        /// <i>draw</i> the game it stopped while stopping it <i>updating</i>.
        /// <para>
        /// <see cref="ResultPage"/> overrides it back to true (#241), and so does the campaign's finale in front of it
        /// (<see cref="CampaignFinalePage"/>, #880): a level that has ended is not a level put down, and the arena has to
        /// go on living behind the numbers and the lettering.
        /// </para>
        /// </summary>
        public override bool UpdatesUnderlying => Manager != null && !Manager.Contains<GameplayScreen>();

        /// <summary>
        /// The shared menu frame — the pointer, Escape and the display hotkeys, the pad navigation — is the
        /// host's, and exactly one page a frame asks for it: the one the player is actually on. A page under
        /// another (the pause under settings) still updates, but its input has been taken over.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            if (IsActive) Game.UpdateMenuChrome(gameTime);
        }

        private Widget _root;
        private int _builtForLayout = -1;

        protected MenuPage(BS3DGame game) => Game = game;

        /// <summary>
        /// The widget tree, built on demand and rebuilt when the viewport has changed size enough to matter.
        /// <para>
        /// Lazy rather than eager: every size is a 2160p design figure resolved at build time (see
        /// <see cref="BS3DGame.MenuLayoutGeneration"/>), so a resize invalidates every page — but a page the
        /// player never opens never builds a tree at all, where the old code rebuilt all seven on every step
        /// of a window drag.
        /// </para>
        /// </summary>
        internal Widget Root
        {
            get
            {
                if (_root != null && _builtForLayout == Game.MenuLayoutGeneration) return _root;

                _builtForLayout = Game.MenuLayoutGeneration;
                _root = BuildTree();

                //A fresh tree starts at its authored defaults, and everything the page writes onto it — the
                //resume entry, the setting values, the marked scene, the score — was written onto the tree
                //that has just been thrown away
                Refresh();

                return _root;
            }
        }

        /// <summary>Whether this page's tree has been built at least once — what a testing lever waits for (#548).</summary>
        internal bool IsBuilt => _root != null;

        /// <summary>Builds this page's widgets at the current layout size. Called again after a resize.</summary>
        protected abstract Widget BuildTree();

        /// <summary>
        /// Throws this page's tree away, so the next read of <see cref="Root"/> builds it again (#427). For a
        /// page whose <b>content</b> changes and not only its size — the Help screen, which is several pages
        /// behind one menu entry and swaps which of them it is showing.
        /// <para>
        /// It goes through the same door a resize does rather than round it: the rebuild re-runs
        /// <see cref="Refresh"/> and re-collects the pad's navigation entries, which a page that merely
        /// hid and showed widgets would have to do for itself and would eventually forget.
        /// </para>
        /// </summary>
        protected void InvalidateTree() => _builtForLayout = -1;

        /// <summary>
        /// Whether Escape and the pad's B leave this page. False where there is nowhere to go back <i>to</i>:
        /// the main menu (quitting is an entry, and a front end that closes on a tapped key closes by
        /// accident) and the result screen (the level has already ended, so "back one level" means nothing).
        /// </summary>
        internal virtual bool CanGoBack => true;

        /// <summary>
        /// Re-reads everything that has to agree with the game's state — whether there is a session to resume,
        /// what each setting says, which scene is marked, the score just earned. Called when the page becomes
        /// the active one, because that is the only time it can change while nobody is looking at it, and
        /// again whenever the tree is rebuilt.
        /// </summary>
        internal virtual void Refresh() { }

        /// <summary>
        /// The pad/arrow focus cursor has moved onto <paramref name="focused"/> (null: it went down — the
        /// pointer is driving, whose hover the page's own events see). Raised by the host's
        /// <c>ApplyNavHighlight</c>, for the page that presents the focused entry somewhere other than on the
        /// entry itself — the level picker's detail line (#91). Most pages need nothing here.
        /// </summary>
        internal virtual void NavFocusChanged(Button focused) { }

        /// <summary>
        /// The entry the pad's and the arrow keys' cursor arrives on when the page comes up with the cursor already
        /// showing (#740), or null for the top entry. The host reveals it once the page is laid out, scrolling a list
        /// to it if need be.
        /// </summary>
        internal virtual Button NavArrival => null;

        /// <summary>
        /// The mouse wheel turned by <paramref name="delta"/> notches this frame while this page is the active
        /// one (#352, #517) — positive away from the player, negative towards them, the raw
        /// <c>MouseState.ScrollWheelValue</c> delta rather than a sign alone, in case a future page ever wants
        /// to weight a fast flick against a slow one. Called from <c>BS3DGame.UpdateMenuChrome</c>, which holds
        /// it to the same <c>EdgeInputAllowed</c> gate every other edge input here already answers to — a wheel
        /// notch that landed the instant before a refocus must not fire twice. Most pages need nothing here.
        /// </summary>
        internal virtual void OnScrollWheel(int delta) { }

        /// <summary>
        /// The pad's right stick (<paramref name="stick"/>, up positive, zero inside its dead zone) and Page Up/Page
        /// Down this frame (<paramref name="pageSteps"/>, down positive) while this page is the active one — for a
        /// page whose scroller holds text rather than entries, which the focus cursor has nothing in to walk to
        /// (#606, the Help pages). Most pages need nothing here.
        /// </summary>
        internal virtual void OnScrollAxis(float stick, int pageSteps, float elapsed) { }

        /// <summary>
        /// Left or right on the arrow keys, the D-pad or the left stick (−1 / +1) — <b>the page's own axis</b>,
        /// where up and down are always the focus cursor's (on a <see cref="PadWalksGrid"/> page the pad's left and right
        /// are the cursor's too, #848, and only the arrow keys and the shoulders reach this). Six pages claim it: the level picker turns to the
        /// previous or next chapter (#273), Help to its previous or next tab (#606, #857), Settings to its previous or
        /// next tab (#686), High Scores to its previous or next chapter (#685), a level's online boards to their previous
        /// or next ranks (#799) and About to its previous or next page (#807).
        /// Raised by the host's <c>UpdateMenuNavigation</c> with the same fire-once-then-walk treatment a held direction gets
        /// on the other axis. The pad's shoulder buttons, one press a page, reach the page through <see cref="ShoulderTurn"/>,
        /// which is this unless a page says otherwise.
        /// <para>
        /// Returns whether it acted, which is what decides whether the menu ticks: a page that ignores the axis
        /// — every page but those six — must not sound as though something moved, and neither must a picker whose set
        /// has one chapter to turn between. A page that declines leaves the press to the row the cursor stands in (#805).
        /// </para>
        /// </summary>
        internal virtual bool PageSideways(int direction) => false;

        /// <summary>
        /// The pad's left or right shoulder (−1 / +1), one press a page (#686): <see cref="PageSideways"/> by default, so a
        /// page that turns on the arrows turns on the shoulders alike. About's player page is the one that tells them apart
        /// (#807): its arrows walk Play and Next and turn the page only past the row's ends, and its shoulders turn the page
        /// from anywhere.
        /// Returns whether it acted, as <see cref="PageSideways"/> does.
        /// </summary>
        internal virtual bool ShoulderTurn(int direction) => PageSideways(direction);

        /// <summary>
        /// Whether this page draws something into the frame's sharp foreground layer this frame (#856) — the layer the
        /// host composites over everything, the UI included, at the end of its Draw (#242, the cup's and the confetti's):
        /// Help's live balls beside their entries. Asked by the host every frame a page is up; a page that presents is
        /// drawn through <see cref="DrawForeground"/> and keeps the layer allocated (<c>BS3DGame.BeginSceneDraw</c>'s
        /// release count).
        /// </summary>
        internal virtual bool PresentsForeground => false;

        /// <summary>
        /// The back-buffer rectangle the page's foreground is cut to (#856): what it drew outside it is erased before the
        /// composite, so a ball beside an entry scrolled past a frame's edge is clipped as its text is. Empty for no cut.
        /// </summary>
        internal virtual Rectangle ForegroundClip => Rectangle.Empty;

        /// <summary>
        /// Draws the page's foreground (#856). The host has bound the layer and cleared it transparent with its own depth,
        /// stated opaque, depth-tested, counter-clockwise states and restores its own afterwards; on the Potato path, which
        /// has no layer, it is the back buffer after the desktop, on a cleared depth buffer, and nothing is cut.
        /// </summary>
        internal virtual void DrawForeground() { }

        /// <summary>
        /// Draws what the page puts <b>over</b> its own tree (#926), into the host's overlay batch after the desktop has been laid out and
        /// drawn and before the focus prompt: the result page's "NEW BEST", whose rainbow fill is a colour that changes across each letter,
        /// which a Myra label cannot draw, and whose bounce overshoots the lines around it on purpose. Asked every frame the page is the active
        /// one; a page that draws nothing leaves it alone. The positions to draw at are the tree's own (<c>Widget.ToGlobal</c> of a widget's
        /// <c>ActualBounds</c>), which are laid out by then.
        /// </summary>
        internal virtual void DrawOverUi() { }

        /// <summary>
        /// Whether this page is laid out as a grid the pad walks in the direction pressed (#848): the D-pad and the left
        /// stick move the focus cursor to the entry beside, above or below it, and its left and right are the cursor's,
        /// not <see cref="PageSideways"/>' (the shoulders still turn the page). The arrow keys keep walking the list in
        /// order and turning the page. The level picker, where the owner, pad in hand, expected "down" to light the
        /// level under the lit one and found the D-pad turning chapters the prompts gave to LB and RB.
        /// </summary>
        internal virtual bool PadWalksGrid => false;

        /// <summary>
        /// Whether this page has taken the keyboard to type into (#548) — the settings page while a nickname is
        /// being typed. While it has, the host's menu frame reads no key of its own: no Escape back, no arrow walk,
        /// no Enter activation — a Space, an arrow or a letter is the page's, and it arrives through
        /// <see cref="OnTextInput"/>. The pad's A and B still come, through <see cref="TypingButtons"/>.
        /// </summary>
        internal virtual bool CapturesKeyboard => false;

        /// <summary>
        /// A character the window typed, handed to the active page while <see cref="CapturesKeyboard"/> (#548).
        /// Enter, Escape and Backspace arrive here as the characters Windows sends for them.
        /// </summary>
        internal virtual void OnTextInput(char character) { }

        /// <summary>The pad's A (<paramref name="keep"/>) and B (<paramref name="drop"/>) while the page is typing (#548).</summary>
        internal virtual void TypingButtons(bool keep, bool drop) { }

        /// <summary>
        /// Whether the page has stepped out of the way of something playing over the frame (#861: the Scene page while the
        /// scene's own tour flies). The host then draws none of it — the focus prompt (#799) included, which the host draws
        /// itself and which hiding the page's plate left standing — feeds Myra nothing, and hands the page no walk, no Enter,
        /// no wheel and no Back: a press of a skip key goes to <see cref="SkipPressed"/>, so the press that skips cannot also
        /// choose the entry the cursor stood on. The display keys still work. The click is disarmed on the frame the page is
        /// drawn again, as for a page that has just arrived, until the button is seen up.
        /// </summary>
        internal virtual bool StandsAside => false;

        /// <summary>
        /// A skip key while the page <see cref="StandsAside"/> (#861): Space, Enter, Escape, the left button, or the pad's A,
        /// B or right trigger — the takeover's own skips (Space, the click, A and the trigger) and the menu's yes and back.
        /// </summary>
        internal virtual void SkipPressed() { }

        /// <summary>What the page draws instead of itself while it <see cref="StandsAside"/> (#861), into the host's overlay
        /// batch after the frame: the Scene page's skip hint.</summary>
        internal virtual void DrawAside() { }

        /// <summary>
        /// The brush a <b>resting</b> entry on this page is painted with — the one the host's
        /// <c>ApplyNavHighlight</c> rests every unfocused entry to. It has to be asked here rather than assumed,
        /// because that highlight repaints each entry's background every pass and would otherwise paint the one
        /// shared grey over any page that wanted something else. The front end overrides it to almost nothing —
        /// its entries are large unplated Anton that carries itself like the title beside it — where every other
        /// page keeps <see cref="BS3DGame.MENU_BUTTON_BRUSH"/>, the slab a control needs to read as one on a plate.
        /// </summary>
        internal virtual IBrush EntryRestBrush => BS3DGame.MENU_BUTTON_BRUSH;

        /// <summary>
        /// The brush <b>this</b> resting entry is painted with (#898), where <see cref="EntryRestBrush"/> is the page's one.
        /// <c>ApplyNavHighlight</c> asks it of every unfocused entry on each pass, because a page that tints its entries
        /// one by one (the High Scores page's rows of levels the player has not finished) would otherwise have every tint
        /// painted over with the page-wide rest the moment the pad or the pointer moved. The default is the page's
        /// <see cref="EntryRestBrush"/>, so no other page changes. It runs on a change of focus or of entries, never per
        /// frame, and must answer with a brush built once (a static, or one the page holds): no allocation here.
        /// </summary>
        internal virtual IBrush EntryRestBrushFor(Button entry) => EntryRestBrush;

        /// <summary>
        /// Whether this page dims the frame behind it. A pause dims hard, because what is behind it is a
        /// stopped game; the front end does not dim at all, because the rotating scene is the point of that
        /// screen. A page shared between the two — settings, scene, about — asks the <b>stack</b> whether a
        /// pause is underneath it, which is the question it was really asking all along.
        /// </summary>
        internal virtual bool DimsFrame => Manager != null && Manager.Contains<PausePage>();

        /// <summary>
        /// How far the scene behind this page has gone <b>out of focus</b>, 0–1 — the frame's own defocus,
        /// mixed in by the tonemap (<see cref="Prazsky.Core.Render.PostProcessPipeline.Resolve"/>). Two pages
        /// answer with something: the result screen, which lets the arena go soft a few seconds after a level
        /// ends, and the pause, which does it at once (#178).
        /// <para>
        /// The default is <b>the pause's own answer if a pause is underneath</b> — the shape
        /// <see cref="DimsFrame"/> already uses, and the same question: Settings or the scene picker opened
        /// from a pause is still standing over a stopped game, and a frame that snapped back into focus as
        /// the panel opened would undo the point of blurring it. It reads the pause's ramp rather than a flat
        /// 1 so that opening a panel <i>during</i> that third of a second does not skip it.
        /// </para>
        /// <para>
        /// Asked of the <b>active</b> screen only, at resolve time. That is why the two ramps that exist are
        /// clocked the way they are: the result screen is the only screen still updating while it is up, and
        /// the pause is stamped against the wall clock precisely because a panel over it stops it updating.
        /// </para>
        /// </summary>
        internal virtual float FrameBlur => Manager?.Find<PausePage>()?.FrameBlur ?? 0f;

        //The IFrameBlurSource surface: a page's blur is the virtual above, and a page always blurs the
        //whole frame alike — the periphery-only shape is the session's (see GameplayScreen's own answer)
        float IFrameBlurSource.FrameBlur => FrameBlur;
        float IFrameBlurSource.FrameBlurFocus => 0f;

        /// <summary>
        /// Putting the tree into the shared Myra desktop is done here, on <see cref="Screen.CoveredChanged"/>
        /// alone, and that is enough for every transition: the manager raises it on whichever page ends up on
        /// top after a frame's pushes and pops have been applied, so a push, a pop, a replace and a reset all
        /// arrive through the one path. There is one desktop and it holds one root, so only the top page's
        /// tree is ever in it.
        /// </summary>
        public override void CoveredChanged() => Game.ShowPage(this);

        #region The shared look, forwarded so a page's own build reads as its own

        //Every page is built out of the same handful of pieces, and they live on BS3DGame because the fonts,
        //the layout scale and the palette are the frame's rather than any one page's. Forwarding them here is
        //what lets a page's BuildTree read as a description of that page instead of as calls through a host.

        protected int Scaled(int designUnits) => Game.Scaled(designUnits);

        protected Thickness ScaledThickness(int horizontal, int vertical) => Game.ScaledThickness(horizontal, vertical);

        protected Thickness ScaledThickness(int left, int top, int right, int bottom) =>
            Game.ScaledThickness(left, top, right, bottom);

        protected Button MenuButton(string text, Action onClick) => Game.MenuButton(text, onClick);

        protected Button MenuButton(string text, Action onClick, out Label label) => Game.MenuButton(text, onClick, out label);

        protected Button MenuButton(Widget content, Action onClick) => Game.MenuButton(content, onClick);

        /// <summary>A prompt (#799): what a pad player and a keyboard player press, drawn for whichever hand is on the menus.</summary>
        protected PromptGlyph Prompt(string pad, string keys) => Game.Prompt(pad, keys);

        /// <summary>
        /// A worded button with its prompt at one end of the slab (#799) - see <c>BS3DGame.MenuButton</c>'s prompted form.
        /// </summary>
        protected Button MenuButton(string text, Action onClick, PromptGlyph prompt, HorizontalAlignment side) =>
            Game.MenuButton(text, onClick, prompt, side, out _);

        /// <summary>
        /// What turns a page, standing beside the entry that turns it rather than in it (#799): LB or ← beside Previous, RB
        /// or → beside Next, dimmed with the entry when there is no page that way.
        /// </summary>
        protected PromptGlyph PageTurnPrompt(string pad, string keys, Widget entry)
        {
            PromptGlyph prompt = Prompt(pad, keys);
            prompt.StandsFor = entry;
            return prompt;
        }

        /// <summary>Between a page-turn arrow's slab and the shoulder under it, in design units.</summary>
        private const int ARROW_SHOULDER_GAP = 10;

        /// <summary>
        /// A page-turn arrow with the pad's shoulder that turns it drawn under its slab (#848): the owner's answer, the
        /// button glyph in or under the arrow's plate so it reads as that arrow's key. <b>No key for the keyboard</b>
        /// (the owner, 2026-10-12): an arrow key beside an arrow only repeated it. The glyph keeps its place while the
        /// keyboard is the hand, drawing nothing, so nothing moves when the hand changes; it dims with an arrow that has
        /// nowhere to go. Stacked from the top, so a row aligned to its top keeps the slab level with what stands beside it.
        /// </summary>
        protected Widget ArrowWithShoulder(Button arrow, string shoulder) => PageTurnUnder(arrow, shoulder, string.Empty);

        /// <summary>
        /// A page-turn entry with what turns it drawn under it (#848): <see cref="ArrowWithShoulder"/>'s layout for an entry
        /// that is a word rather than an arrow — the level boards' Previous and Next, too narrow to carry a shoulder's pill
        /// beside their word inside them — where the arrow keys repeat nothing and keep their prompt.
        /// </summary>
        protected Widget PageTurnUnder(Button entry, string pad, string keys)
        {
            VerticalStackPanel stack = new()
            {
                Spacing = Scaled(ARROW_SHOULDER_GAP),
                VerticalAlignment = VerticalAlignment.Top,
            };

            stack.Widgets.Add(entry);

            PromptGlyph prompt = PageTurnPrompt(pad, keys, entry);
            prompt.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Widgets.Add(prompt);

            return stack;
        }

        /// <summary>
        /// The Back entry every sub-page ends on, with B and Escape on it (#799), the two presses that do what it does
        /// from anywhere on the page.
        /// </summary>
        protected Button BackButton() => BackButton("Back", GoBack);

        /// <summary>
        /// An entry that does what B and Escape do on this page, worded for it: the pause's Resume, the Quit confirm's
        /// Cancel (#799).
        /// </summary>
        protected Button BackButton(string text, Action onClick) =>
            MenuButton(text, onClick, Prompt(PromptGlyph.PAD_B, PromptGlyph.KEY_ESCAPE), HorizontalAlignment.Left);

        /// <summary>
        /// A media symbol (#784) sized to stand in for a word on a menu button: the display face's cap height in a widget
        /// as tall as the face's line, so the button keeps a worded one's height.
        /// </summary>
        protected MediaGlyph ButtonGlyph(MediaGlyph.Symbol symbol) =>
            new(symbol, (int)MathF.Round(FontBody.LineHeight * ANTON_CAP_OF_LINE), FontBody.LineHeight);

        /// <summary>
        /// The height of a display-face font's capitals in pixels (Anton's, off its own tables): what a drawn shape standing
        /// beside a word of that face is sized to so it reads at the word's weight. Measured on the level picker's heading
        /// at 3840×1600 (#847): 53 px, which is this.
        /// </summary>
        protected static int CapHeightOf(FontStashSharp.SpriteFontBase font) => (int)MathF.Round(font.LineHeight * ANTON_CAP_OF_LINE);

        /// <summary>
        /// A checkbox (#816): a menu button around a <see cref="CheckGlyph"/> and the label beside it, so it is a member of
        /// the menu's button family in all but its content. The slab, the brushes the pad's focus swaps, the click sound,
        /// the pad's entry and #800's haptics all come with <see cref="MenuButton(Widget, Action)"/>. For a row with two
        /// states whose words would be On and Off; a row with more values, or two states of other names, keeps a button
        /// that cycles its words (<c>docs/game-shell/settings.md</c>, "Two kinds of control").
        /// </summary>
        /// <param name="text">What the tick says, beside the box; empty for a row whose caption already says it, and the
        /// label is then hidden until a page writes a qualifier into it.</param>
        protected Button CheckBox(string text, Action onClick, out CheckGlyph glyph, out Label label)
        {
            glyph = new CheckGlyph((int)MathF.Round(FontBody.LineHeight * ANTON_CAP_OF_LINE), FontBody.LineHeight);
            label = new Label
            {
                Text = text,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = !string.IsNullOrEmpty(text),
            };

            //The box a word's space from its label, both centred on the slab as a worded button's label is
            HorizontalStackPanel content = new()
            {
                Spacing = Scaled(CHECK_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            content.Widgets.Add(glyph);
            content.Widgets.Add(label);

            return MenuButton(content, onClick);
        }

        /// <summary>Between a checkbox's box and its label, in design units: about a space of the body face.</summary>
        private const int CHECK_GAP = 24;

        //Anton's capitals as a fraction of its line, off the face's own tables: sCapHeight 1760 units in a line of
        //ascent 2409 + descent 674 (no line gap). Taken of the line rather than the em, so it holds whichever of the two
        //FontStashSharp sizes a font by
        private const float ANTON_CAP_OF_LINE = 1760f / (2409f + 674f);

        protected Button FrontEndEntry(string text, Action onClick) => Game.FrontEndEntry(text, onClick);

        protected Button FrontEndEntry(string text, Action onClick, out Label label) =>
            Game.FrontEndEntry(text, onClick, out label);

        protected VerticalStackPanel MenuColumn() => Game.MenuColumn();

        protected ScrollViewer MenuScroll(Widget content, int reservedDesignUnits, bool clearEdges = false) =>
            Game.MenuScroll(content, reservedDesignUnits, clearEdges);

        protected Label ScreenHeading(string text) => Game.ScreenHeading(text);

        /// <summary>
        /// The width a label with a fixed <see cref="Widget.Width"/> lays its text out to: Myra wraps at its
        /// <c>ActualBounds</c>, the width less its margin, border and padding. Not the width itself, even for a label
        /// with no side margin of its own: <see cref="ScaledThickness(int, int, int, int)"/> makes a zero a pixel
        /// (<c>Scaled</c> never returns less than 1), so the privacy note of 473 px laid its text out to 471, and a
        /// line <see cref="SpaceWrap"/> had measured at exactly 473 lost its last word to a line of its own (#769).
        /// </summary>
        protected static int LaidOutWidth(Label label) => label.Width.GetValueOrDefault()
            - label.Margin.Left - label.Margin.Right
            - label.BorderThickness.Left - label.BorderThickness.Right
            - label.Padding.Left - label.Padding.Right;

        /// <summary>
        /// A paragraph of running text (#806): the small face in the body grey, its lines chosen for the paragraph's
        /// shape (<see cref="SpaceWrap"/>), set at the prose leading, with <see cref="ParagraphGap"/> under it. One place
        /// for what a paragraph is, so the pages' paragraphs cannot drift apart again the way they did when each set its own
        /// face, colour and gap. <paramref name="width"/> is the column's: prose wider than <see cref="ProseMeasure"/> is a
        /// measure too long to read, so a page with a wider column sets its paragraphs at that instead ("Typesetting" in
        /// <c>docs/game-shell/menu-look.md</c>). Help is the one exception, set across its whole text width (#865).
        /// </summary>
        protected Label Paragraph(string text, int width) =>
            Prose(text, width, BS3DGame.MENU_TEXT_BODY, Game.ProseLeading, new Thickness(0, 0, 0, ParagraphGap));

        /// <summary>
        /// A paragraph that opens on a lead-in (#996): <paramref name="lead"/> run into its first line (<see cref="LeadIn"/>),
        /// so a player who reads only the leads down a page has its point. Set as <see cref="Paragraph(string, int)"/>
        /// otherwise, and no taller than it.
        /// </summary>
        protected Label Paragraph(string lead, string text, int width) => Paragraph(LeadIn(lead) + " " + text, width);

        /// <summary>
        /// A lead-in (#996): a few words in the display face's capitals and the loud white, the small face's size, with a
        /// little more than a space after them — the way a page's block says what it is about without a heading's line of
        /// its own. One label still: the face and the colour are FontStashSharp's own commands (<c>/f</c>, <c>/c</c>), the
        /// face reached by name (<see cref="BS3DGame.PROSE_FONT_LEAD"/>). The words are tied, so a lead never breaks.
        /// </summary>
        protected string LeadIn(string words) =>
            $"/f[{BS3DGame.PROSE_FONT_LEAD}]/c[{LOUD}]{words.ToUpperInvariant().Replace(' ', SpaceWrap.TIE)}/cd/fd/s[{Scaled(LEAD_GAP)}]";

        /// <summary>
        /// A strong word or phrase inside a sentence (#1001): the reading face's bold cut in the loud white, for the thing on
        /// the screen the sentence tells the player to look at. Each word is marked on its own, so the line breaks between
        /// them as between any words and every line the breaker measures carries its own commands.
        /// </summary>
        protected static string Strong(string words)
        {
            string[] parts = words.Split(' ');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = $"/f[{BS3DGame.PROSE_FONT_STRONG}]/c[{LOUD}]{parts[i]}/cd/fd";

            return string.Join(" ", parts);
        }

        //The lead-in's and the strong word's colour, MENU_TEXT as FontStashSharp's colour command reads one (#RRGGBBAA)
        private static readonly string LOUD =
            $"#{BS3DGame.MENU_TEXT.R:X2}{BS3DGame.MENU_TEXT.G:X2}{BS3DGame.MENU_TEXT.B:X2}{BS3DGame.MENU_TEXT.A:X2}";

        //What a lead-in leaves after itself on top of the space, in design units: a third of the small face's em, so the
        //lead reads as a label for the sentence and not as its first words
        private const int LEAD_GAP = 16;

        /// <summary>
        /// An aside (#806): set as <see cref="Paragraph(string, int)"/> but in the dim grey — a caption over or under a block, a note
        /// beside the rows, what a field wants.
        /// </summary>
        protected Label Aside(string text, int width) =>
            Prose(text, width, BS3DGame.MENU_TEXT_DIM, Game.ProseLeading, new Thickness(0, 0, 0, ParagraphGap));

        /// <summary>
        /// The prose label <see cref="Paragraph(string, int)"/> and <see cref="Aside"/> are made of, for a page that places it otherwise:
        /// its own colour, leading and margin. The margin is set before the lines are measured, and in true pixels rather
        /// than through <see cref="ScaledThickness(int, int, int, int)"/>, whose zero is a pixel (see <see cref="LaidOutWidth"/>).
        /// </summary>
        protected Label Prose(string text, int width, Color color, int leading, Thickness margin)
        {
            Label label = new()
            {
                Font = FontSmall,
                TextColor = color,
                Wrap = true,
                Width = width,
                VerticalSpacing = leading,
                Margin = margin,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            SetProse(label, text);
            return label;
        }

        /// <summary>
        /// Writes <paramref name="text"/> into a prose label, broken into lines for its face and its laid-out width — also
        /// for a label whose text changes after it is built (the Settings note, the nickname plate's hint, the boards'
        /// status lines). <b>Every label that wraps is written through this, never through <c>Text</c></b> (#855): the
        /// label's own wrap breaks after a full stop as well as at a space, and when the glyph that overflows the line is
        /// the space after a sentence it breaks after the stop and starts the next line with that space (seen with Help's
        /// copy laid out raw in a 1600×900 window). <c>SpaceWrap</c> breaks at spaces only, so a label it has set starts
        /// every line on a word; <c>Wrap</c> stays on as the backstop for a line it left wider than the label.
        /// <c>ProseLabelsTests</c> reads the pages' sources and holds every <c>Wrap = true</c> initializer to it.
        /// </summary>
        protected static void SetProse(Label label, string text) =>
            label.Text = SpaceWrap.Wrap(text, LaidOutWidth(label), line => LineWidth(label.Font, line));

        /// <summary>
        /// A line's width as the label will draw it: the font's own measure for plain copy, and FontStashSharp's rich-text
        /// layout's — the class the label lays its text out with — for a line that carries a command (a lead-in's face and
        /// colour, #996; a doubled slash), whose letters the font's measure would count as glyphs and whose other faces
        /// it would not know. Called while a tree is built, never per frame, so the one layout is reused.
        /// </summary>
        private static float LineWidth(SpriteFontBase font, string line)
        {
            if (line.IndexOf('/') < 0) return font.MeasureString(line).X;

            //A command the layout cannot read throws inside it (an unknown face, a malformed /s[]), where the label itself,
            //drawing the same text, would only show its own error: text the score server sends (a nickname's refusal reason)
            //reaches SetProse, so a slash in it must not take a page down. Measured as plain glyphs then, which is wider
            //than the label will draw it, so the line still fits
            try
            {
                LINE_LAYOUT.Font = font;
                LINE_LAYOUT.Text = line;
                return LINE_LAYOUT.Size.X;
            }
            catch (Exception)
            {
                return font.MeasureString(line).X;
            }
        }

        private static readonly RichTextLayout LINE_LAYOUT = new() { SupportsCommands = true };

        /// <summary>
        /// The height a prose label needs to show the tallest of <paramref name="texts"/> without moving what stands under
        /// it as its text changes (#769): each laid out by a probe with the label's own face, width, margin and leading, as
        /// it will be drawn. Measured rather than counted in <c>LineHeight</c>s, because Myra counts a margin inside an
        /// explicit height (counting cut the Settings note's last clause).
        /// </summary>
        protected static int ProseHeight(Label label, params string[] texts)
        {
            int width = label.Width.GetValueOrDefault();
            int tallest = 0;

            foreach (string text in texts)
            {
                Label probe = new()
                {
                    Font = label.Font,
                    Wrap = true,
                    Width = width,
                    Margin = label.Margin,
                    VerticalSpacing = label.VerticalSpacing,
                };
                SetProse(probe, text);

                tallest = Math.Max(tallest, probe.Measure(new Point(width, int.MaxValue / 2)).Y);
            }

            return tallest;
        }

        /// <summary>The space a paragraph leaves under itself (#806): one em of the small face.</summary>
        protected int ParagraphGap => Game.ParagraphGap;

        /// <summary>The widest prose is set (#806): about 67 characters of the small face.</summary>
        protected int ProseMeasure => Game.ProseMeasure;

        /// <summary>The space added between two lines of prose (#806).</summary>
        protected int ProseLeading => Game.ProseLeading;

        /// <summary>The build's name, small, in the bottom-right corner — on the front end and About only.</summary>
        protected Label VersionTag() => Game.VersionTag();

        protected Panel Plate(Widget content, bool prose = false) => Game.Plate(content, prose);

        /// <summary>
        /// The width every entry in a menu column is cut to, in pixels at the layout in force — for anything a
        /// page puts in that same stack that has to line up with them. See <see cref="BS3DGame.MenuColumnWidth"/>.
        /// </summary>
        protected int ColumnWidth => Game.MenuColumnWidth;

        /// <summary>
        /// The page over the whole frame — one centred column on every page but the front end, which pins
        /// several children by their own alignment instead (#217). It carries no scrim of its own: whether the
        /// frame dims is still <see cref="DimsFrame"/>'s answer, but the wash itself is the host's own quad
        /// since #114 — Myra's background paint stops short of the viewport's bottom edge, and the strip it
        /// left undimmed was the bug.
        /// </summary>
        protected Panel ScreenRoot(params Widget[] content) => BS3DGame.ScreenRoot(content);

        /// <summary>One level back off the stack — what every sub-page's "Back" entry does.</summary>
        protected void GoBack() => Manager.Pop();

        protected SpriteFontBase FontBody => Game.MenuFontBody;

        /// <summary>
        /// The heading size (124 against body's 80). Exposed for #238: the pages had every other size and not
        /// this one, so a line that had to be read as loudly as a heading could only be set at body size.
        /// </summary>
        protected SpriteFontBase FontHeading => Game.MenuFontHeading;

        /// <summary>
        /// The section-label size (96, the display face) — a heading for a group of rows rather than for a page.
        /// Added by #243; see <c>MENU_FONT_SECTION</c> for why the small face's size could not just be raised.
        /// </summary>
        protected SpriteFontBase FontSection => Game.MenuFontSection;
        protected SpriteFontBase FontSmall => Game.MenuFontSmall;
        protected SpriteFontBase FontTitle => Game.MenuFontTitle;
        protected SpriteFontBase FontStars => Game.MenuFontStars;

        /// <summary>The level picker's tile face (46, Inter) — see <c>MENU_FONT_TILE</c> for why it is its own size.</summary>
        protected SpriteFontBase FontTile => Game.MenuFontTile;

        //The rating's two glyphs, shared so the picker's small rows and the result's headline cannot drift
        //apart. They must be drawn with FontStars or FontSmall: the glyphs live in Inter, and the display face
        //the loud type is set in carries neither — FontStashSharp would silently draw blanks.
        protected const char STAR_FILLED = '★';
        protected const char STAR_HOLLOW = '☆';

        /// <summary>
        /// The filled run of a rating — one ★ per star earned. It is drawn <b>separately from the hollow
        /// remainder</b> (<see cref="StarsRemaining"/>) rather than as one string, because since #139 the two
        /// halves are different colours everywhere they appear: the earned run carries the tier's colour
        /// (<see cref="BS3DGame.StarTierColor"/>) and the remainder recedes to <see cref="BS3DGame.STAR_EMPTY"/>.
        /// </summary>
        protected static string StarsEarned(int earned) =>
            new(STAR_FILLED, Math.Clamp(earned, 0, StarRating.MAX));

        /// <summary>The hollow remainder of a rating — what is left of <see cref="StarRating.MAX"/>.</summary>
        protected static string StarsRemaining(int earned) =>
            new(STAR_HOLLOW, StarRating.MAX - Math.Clamp(earned, 0, StarRating.MAX));

        #endregion

        //What is chosen reads as unmistakably chosen, inverted: the near-white of the type as the slab and a dark grey as the
        //type. Still the greyscale rule (BS3DGame's palette) — brightness, not hue — and the opposite of a row's focus
        //highlight, which lifts a grey slab a step and so can never be taken for it. The tab that is up (TabRow) and the
        //period High Scores shows (#1003) wear it, so a choice looks the same wherever the player makes one
        protected static readonly IBrush SELECTED_BRUSH = new FlatBrush(BS3DGame.MENU_TEXT);
        protected static readonly Color SELECTED_TEXT = new(30, 30, 30);

        /// <summary>
        /// A row of tabs over a page area (#686, #857): one button per page, all cut to the widest name's width, the chosen
        /// one inverted, with what turns them at the row's two ends. Buttons, so the mouse gets the shared hover, press and
        /// click sound — but with nothing on their <c>Tag</c>, which is what keeps the host's walk off them: five tabs ahead
        /// of a page's first row would cost five presses, so the tabs are turned, wrapping, by <see cref="PageSideways"/>
        /// (the arrow keys, the D-pad, the left stick), the shoulders (<see cref="ShoulderTurn"/>) and the wheel over the row
        /// (<see cref="OnScrollWheel"/>, which asks <see cref="IsMouseOver"/>). Settings and Help share it, so a tab looks and
        /// answers the same on both; what each page shows under it, and what it does on a turn, is the page's own.
        /// </summary>
        protected sealed class TabRow
        {
            //The padding round a name, the space between tabs and the gap under the row, in design units
            private const int PADDING_X = 40;
            private const int PADDING_Y = 14;
            private const int SPACING = 16;
            private const int GAP = 20;

            private readonly Button[] _tabs;
            private readonly Label[] _labels;

            /// <param name="page">The page the row stands on: its faces, its button family and its scale.</param>
            /// <param name="names">The tabs' names, in the display face's capitals.</param>
            /// <param name="select">Asked with the tab's index on a click. A page puts the tab up from its <c>Update</c>
            /// rather than here, because a click arrives from inside Myra's own processing of the tree it would be changing.</param>
            internal TabRow(MenuPage page, string[] names, Action<int> select)
            {
                int nameWidth = 0;
                foreach (string name in names)
                    nameWidth = Math.Max(nameWidth, (int)MathF.Ceiling(page.FontSection.MeasureString(name).X));

                HorizontalStackPanel row = new()
                {
                    Spacing = page.Scaled(SPACING),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = page.ScaledThickness(0, 0, 0, GAP),
                };

                //What turns the tabs, at the row's two ends (#799, the owner's "LB ◂ AUDIO · CONTROLS ▸ RB"): the shoulders
                //for a pad player, the arrow keys for a keyboard one. Outside the tabs, so the pages below stay as wide as
                //the tabs themselves (#770, Width) and only the row reaches past them.
                row.Widgets.Add(page.Prompt(PromptGlyph.PAD_LB, PromptGlyph.KEY_LEFT));

                _tabs = new Button[names.Length];
                _labels = new Label[names.Length];
                int tabWidth = nameWidth + 2 * page.Scaled(PADDING_X);

                for (int t = 0; t < names.Length; t++)
                {
                    //Captured per iteration, so five tabs are five destinations
                    int tab = t;

                    Button button = page.MenuButton(names[t], () => select(tab), out Label label);
                    button.Tag = null;
                    button.Padding = page.ScaledThickness(PADDING_X, PADDING_Y);
                    button.Width = tabWidth;
                    label.Font = page.FontSection;

                    _tabs[t] = button;
                    _labels[t] = label;
                    row.Widgets.Add(button);
                }

                row.Widgets.Add(page.Prompt(PromptGlyph.PAD_RB, PromptGlyph.KEY_RIGHT));

                Root = row;
                Width = names.Length * tabWidth + (names.Length - 1) * row.Spacing;
            }

            /// <summary>The row, prompts included, to add to the page's column.</summary>
            internal Widget Root { get; }

            /// <summary>The tabs' own width in pixels, the prompts at the ends excluded: what a page's area is cut to (#770).</summary>
            internal int Width { get; }

            /// <summary>Whether the pointer is over one of the tabs — the wheel there turns them rather than the page.</summary>
            internal bool IsMouseOver
            {
                get
                {
                    foreach (Button tab in _tabs)
                        if (tab.IsMouseInside) return true;

                    return false;
                }
            }

            /// <summary>
            /// Paints the chosen tab inverted and the rest as plain entries, each in <paramref name="restColour"/>'s colour
            /// for its index (the body grey when null) — Settings keeps ONLINE red from any other tab while the boards still
            /// want something done (#687).
            /// </summary>
            internal void Show(int chosen, Func<int, Color> restColour = null)
            {
                for (int t = 0; t < _tabs.Length; t++)
                {
                    bool on = t == chosen;

                    _tabs[t].Background = on ? SELECTED_BRUSH : BS3DGame.MENU_BUTTON_BRUSH;
                    _tabs[t].OverBackground = on ? SELECTED_BRUSH : BS3DGame.MENU_BUTTON_OVER_BRUSH;
                    _labels[t].TextColor = on ? SELECTED_TEXT : restColour?.Invoke(t) ?? BS3DGame.MENU_TEXT_BODY;
                }
            }
        }
    }
}
