using BS3D.Screens;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Prazsky.BS3D;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Render;
using Prazsky.Core.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D
{
    /// <summary>
    /// The <b>front end</b> — the Myra desktop the pages share, the fonts and the greyscale palette, the
    /// widget factories every page is built out of, the page plumbing, and the pad/arrow navigation that
    /// makes the menu operable without a pointer.
    /// </summary>
    /// <remarks>
    /// The read-only surface the pages ask the host about itself through lives here too, all of it, rather
    /// than being dealt out to the partial that owns each field: it is one deliberate interface with one rule
    /// (a page shows state and asks for an action, and cannot write one), and that rule is only visible while
    /// the surface is in one place. The verbs it names are in <c>BS3DGame.Settings.cs</c>. Split out of
    /// <c>BS3DGame.cs</c> in #71.
    /// </remarks>
    public partial class BS3DGame
    {
        #region The menu (Myra)

        //The screens, on a stack (Prazsky.Core.Screens). The BackdropScreen sits at the very bottom for the
        //life of the program and draws the setting; the menu pages sit over it (DrawsUnderlying) and the
        //GameplayScreen goes over it while a level is played. What the stack replaced was a GameState enum
        //and a switch that had to work out where the player had come from: "Settings backs out to the pause
        //it was opened from" and "it dims the frame because that pause is a stopped game" were both asked as
        //_state == Paused, from two places. On a stack the pause is simply UNDERNEATH settings, so backing
        //out is a pop and the dimming is a question about the stack — and "is the game paused" is whether the
        //gameplay screen is covered, asked by nobody because the manager's traversal simply stops updating it.
        //
        //The pages are held rather than made per navigation, so Contains<PausePage>() means something and
        //nothing is allocated on a button press.
        private readonly ScreenManager _screens = new();
        private BackdropScreen _backdrop;
        private GameplayScreen _gameplayScreen;
        private SplashPage _splashPage;
        private MainMenuPage _mainMenuPage;
        private PausePage _pausePage;
        private SettingsPage _settingsPage;
        private LevelSelectPage _levelSelectPage;
        private ScenePage _scenePage;
        private AboutPage _aboutPage;
        private ResultPage _resultPage;

        /// <summary>
        /// The bottom of the stack, exposed for its <b>orbit</b> alone: the result screen flies the camera out
        /// onto it when a level ends, so the two share one orbit and one angle rather than each keeping its
        /// own — see <see cref="BackdropScreen.AdvanceOrbit"/>.
        /// </summary>
        internal BackdropScreen Backdrop => _backdrop;

        //The Myra host. Rendered as the very last thing in Draw (after the tonemap resolve and base.Draw),
        //straight to the back buffer — the same place the map editor puts it. Render() also processes Myra's
        //own mouse/keyboard input, so it is only called while a menu page is the active screen, where the
        //game's own input stands down.
        private Desktop _desktop;

        //Each page owns its own widgets. What is here is the host's share of the menu: the desktop the top
        //page's tree is put into, the fonts, the layout scale and the palette — all of which belong to the
        //frame rather than to any one page.

        //The current screen's entries in the order they are drawn, and which one a pad or the arrow keys have
        //landed on. Collected by walking the shown tree rather than registered per button: a list that has to
        //be kept in step by hand is one an added entry silently falls out of, and the walk is run only on a
        //screen change. -1 means the focus cursor is not up at all — the pointer is driving, and two entries
        //lit at once (one hovered, one focused) reads as a bug.
        private readonly List<Button> _navEntries = new();
        private int _navIndex = -1;
        private float _navRepeatDelay;
        private int _navDirection;
        private Point _navMouseAt;

        //Anton and Inter (both SIL OFL), through FontStashSharp. Myra's embedded stylesheet carries a small
        //bitmap font that is fine for a tool panel and much too coarse for a game's title, so the menu brings
        //its own; they are embedded in the assembly, so there is no path to get wrong and nothing to install.
        //Each size is a separate rasterized atlas, so they are resolved once at load rather than per label.
        //
        //TWO TYPEFACES BY ROLE, not by weight. Anton is a display face — condensed, black, one weight — and it
        //is what everything meant to be loud is set in: the title, the menu entries and the HUD. It is the
        //shippable form of the Impact look (Impact itself is bundled with Windows and not redistributable,
        //which is the same rule that keeps Segoe UI out of this assembly). Inter stays for the small print —
        //About's paragraphs, a level's rules, the adaptive-quality note — because a display face is drawn for
        //headlines and turns to mush at body sizes, which is exactly where those live.
        //
        //Three systems, not three fonts in one: FontStashSharp falls back through a system's fonts glyph by
        //glyph, so a second face added beside the first would never be reached — the first has every glyph.
        //Picking a face means picking a system.
        private FontSystem _menuFontSystem, _menuFontSystemBold, _menuFontSystemDisplay;
        private SpriteFontBase _menuFontBody, _menuFontSmall, _menuFontHeading, _menuFontTitle, _menuFontStars;

        //The menu is deliberately GREYSCALE — no hue anywhere, and no coloured frames. It has to sit over
        //twelve backdrops whose palettes are nothing alike (a neon city, an ochre desert, a blue sea, white
        //peaks, green meadow), and any accent colour that reads as the game's own over one of them fights
        //the next. Neutral black-to-white belongs over all of them equally: emphasis is carried by
        //brightness and by opacity, which is legible against any hue.
        //
        //Display-space sRGB throughout: Myra draws to the back buffer, after the frame's one and only exit
        //from linear light.
        internal static readonly Color MENU_TEXT = new(244, 244, 244);        //the active thing on a screen
        internal static readonly Color MENU_TEXT_BODY = new(208, 208, 208);   //prose, a shade under a heading
        internal static readonly Color MENU_TEXT_DIM = new(146, 146, 146);    //asides, always on a dark plate

        //The ONE deliberate exception to the greyscale rule above: the star rating (#139). Three things make
        //it safe where an accent anywhere else is not. It is a READOUT of what the player earned rather than
        //menu chrome — the hue IS the information, and brightness cannot carry it, because all four tiers have
        //to read as "good" and a scale that dims towards the best one says the opposite. It never floats over
        //a backdrop: the result screen dims the whole frame (ResultPage.DimsFrame) and the picker's stars sit
        //on a tile's own near-opaque slab, so these are read against black in both places rather than against
        //a neon city or a desert. And it is the medal vocabulary the player already knows, which is worth more
        //here than consistency with the chrome — four neutral glyphs cannot say "bronze" or "diamond" at all.
        internal static readonly Color STAR_EMPTY = new(92, 92, 92);          //a slot not earned; recedes under the plate
        private static readonly Color STAR_BRONZE = new(205, 116, 58);
        private static readonly Color STAR_SILVER = new(190, 201, 216);       //cooled off MENU_TEXT, or it reads as plain type
        private static readonly Color STAR_GOLD = new(247, 199, 74);
        private static readonly Color STAR_DIAMOND = new(140, 236, 255);      //above gold: the one cold, bright tier

        /// <summary>
        /// The colour a rating of <paramref name="stars"/> is drawn in — the whole earned row takes it, so the
        /// tier reads at a glance rather than having to be counted. Anything outside 1..<see cref="StarRating.MAX"/>
        /// is not a rating (a failed level is shown no stars at all) and takes the empty slot's grey.
        /// </summary>
        internal static Color StarTierColor(int stars) => stars switch
        {
            1 => STAR_BRONZE,
            2 => STAR_SILVER,
            3 => STAR_GOLD,
            4 => STAR_DIAMOND,
            _ => STAR_EMPTY,
        };

        //Buttons: a dark slab at rest that the pointer lifts a step up the grey ramp, so the highlight is
        //brightness and not hue. Each of these REPLACES the one before it rather than being laid over it
        //(Myra picks one brush per state), so they all have to be opaque enough to hide the scene behind
        //them — a translucent hover would show the backdrop through the entry the pointer is on, which is
        //exactly the one that has to read clearly. They stay dark, because the label on top of them is white.
        private static readonly Color MENU_BUTTON = new(11, 11, 11, 212);
        private static readonly Color MENU_BUTTON_OVER = new(72, 72, 72, 232);
        private static readonly Color MENU_BUTTON_PRESSED = new(120, 120, 120, 240);

        //Built once and shared by every entry. A brush holds no per-widget state, and the focus highlight
        //swaps an entry between the first two of these rather than minting a brush per frame.
        private static readonly IBrush MENU_BUTTON_BRUSH = new SolidBrush(MENU_BUTTON);
        private static readonly IBrush MENU_BUTTON_PRESSED_BRUSH = new SolidBrush(MENU_BUTTON_PRESSED);

        //The one of the three that is not a button's alone: AboutPage's link is a Label, not a Button, and it
        //answers the pointer with this same wash so the two read as one gesture rather than two inventions.
        internal static readonly IBrush MENU_BUTTON_OVER_BRUSH = new SolidBrush(MENU_BUTTON_OVER);

        //A pause dims the whole frame, because what is behind it is a stopped game and the menu is the thing
        //to look at. The front end does NOT: there the rotating scene is the point of the screen, and a
        //full-screen wash over it throws away the one thing that screen exists to show. Its legibility comes
        //from the widgets instead — the entries are near-opaque slabs and the prose sits on a plate.
        //
        //Drawn by the HOST's own batch since #114, not as the Myra root's background: Myra's paint stops a
        //couple of rows short of the viewport's bottom edge (see the scrim block in Draw for the measurement),
        //which left a thin strip of undimmed arena across the bottom of every dimmed page.
        internal static readonly Color PAUSE_SCRIM = new(0, 0, 0, 176);

        //Behind prose, where a slab alone cannot hold a line of small text steady over a moving scene
        internal static readonly Color MENU_PLATE = new(0, 0, 0, 190);


        /// <summary>
        /// The height the menu is laid out for, in the same spirit as <see cref="InfoRenderer"/>'s overlay:
        /// every size in the menu is a 2160p figure put through <see cref="Scaled"/> at build time. Myra
        /// measures in pixels, so without this the menu would keep its pixel size and shrink to a postage
        /// stamp on a 4K screen — against the game's own resolution policy, and against the FPS line beside
        /// it, which is authored exactly this way.
        /// <para>
        /// It is done by re-resolving the sizes rather than by <c>Desktop.Scale</c>: scaling the desktop also
        /// scales the full-screen scrim, which then stops covering the frame.
        /// </para>
        /// </summary>
        private const int MENU_DESIGN_HEIGHT = 2160;

        /// <summary>
        /// How much the viewport has to change before the widget tree is rebuilt at the new size, in pixels
        /// of height. A live window drag reports a new size every frame, and each rebuild asks the font
        /// system for glyphs at another size; quantizing bounds a drag across the whole screen to a couple of
        /// dozen rebuilds instead of hundreds, and the sizes are never more than this far out meanwhile.
        /// </summary>
        private const int MENU_REBUILD_QUANTUM = 32;

        private float _menuScale = 1f;
        private int _menuBuiltForHeight = -1;

        /// <summary>
        /// The game's name, as the player sees it — on the menu and in the window's title bar. <c>BS3D</c>
        /// stays the shorthand the repository, the assembly and this file's namespace are named for; it is
        /// not what the game is called.
        /// </summary>
        internal const string GAME_TITLE = "Bubble Shooter 3D";

        private const int MENU_BUTTON_WIDTH = 1000;
        private const int MENU_COLUMN_SPACING = 26;

        //Held direction repeats: one step, a pause long enough that a deliberate single press stays single,
        //then a steady walk. Both are wall-clock seconds and not frame counts, or the list would run faster on
        //a faster machine — the same rule the rattle's phase and the menu's orbit follow.
        private const float NAV_REPEAT_DELAY = 0.42f;
        private const float NAV_REPEAT_INTERVAL = 0.11f;

        //Well past a resting stick's drift, and far enough in that a diagonal push does not step the list
        private const float NAV_STICK_DEADZONE = 0.55f;

        //A pointer has to move by more than its own jitter before it takes the focus back off the pad
        private const int NAV_MOUSE_WAKE_PIXELS = 6;

        //Entries are read at a glance and from across a room, so the type is set large. The title is sized to
        //the whole of GAME_TITLE on one line at the narrowest targeted aspect (16:9, i.e. 3840 design units
        //wide) with room to spare — a short acronym would take far more, but the name is what is set.
        private const int MENU_FONT_SMALL = 58;
        private const int MENU_FONT_BODY = 80;
        private const int MENU_FONT_HEADING = 124;
        private const int MENU_FONT_TITLE = 170;

        //The result screen's star rating — a headline, but set in INTER at a heading's size rather than in
        //the display face like every other loud thing here: Anton carries no ★/☆ glyphs at all (checked in
        //the font, not assumed), and FontStashSharp would draw blanks where the rating should be. A size is
        //its own atlas, hence its own constant and its own GetFont below.
        private const int MENU_FONT_STARS = 116;

        //The exposure ladder the settings button walks. Centred on DEFAULT_EXPOSURE, wide enough either way
        //to matter on a dim laptop panel and on a bright monitor without ever crushing or blowing the frame.
        private const float EXPOSURE_MIN = 0.7f;
        private const float EXPOSURE_MAX = 1.5f;
        private const float EXPOSURE_STEP = 0.2f;

        //The ladder the three volume rows walk: quarters from the authored mix down to silence, then back to
        //full. 100 % is the mix as tuned (the BASE/MUSIC/FANFARE constants in ProceduralAudio and
        //ProceduralMusic), and the player's gains only ever scale it — so retuning the mix never invalidates
        //what a setting means.
        private const float VOLUME_STEP = 0.25f;

        //1 is the authored mix; the "mute" argument starts the master at 0 (see the constructor).
        private float _masterVolume = 1f;
        private float _sfxVolume = 1f;
        private float _musicVolume = 1f;
        private float _ambienceVolume = 1f;

        #endregion

        #region The menu's screens

        /// <summary>
        /// Boots Myra and builds the pages. Called once at the end of <see cref="LoadContent"/>: the menu is
        /// drawn over the live scene, so everything it stands on has to exist first. Each page is built once
        /// here; its tree is swapped into <see cref="_desktop"/>.Root as the player navigates, which is what
        /// keeps the menu out of the frame loop's allocation path.
        /// </summary>
        private void BuildMenu()
        {
            MyraEnvironment.Game = this;

            //Anton and Inter (both SIL OFL 1.1), embedded in the assembly so there is no path to get wrong and
            //nothing to install. Myra's own stylesheet carries a small bitmap font, which is fine for a tool
            //panel and far too coarse for a title. Each size is rasterized into its own atlas by GetFont, so
            //they are resolved once here rather than per label.
            _menuFontSystemDisplay = LoadEmbeddedFont("BS3D.Content.Fonts.Anton-Regular.ttf");
            _menuFontSystem = LoadEmbeddedFont("BS3D.Content.Fonts.Inter-Regular.ttf");
            _menuFontSystemBold = LoadEmbeddedFont("BS3D.Content.Fonts.Inter-Bold.ttf");

            _desktop = new Desktop();

            //Held for the life of the game rather than made per navigation: nothing is allocated on a button
            //press, and Contains<PausePage>() — which is how a shared page knows it is over a stopped game —
            //means something only if there is one pause page rather than a new one each time.
            _splashPage = new SplashPage(this);
            _mainMenuPage = new MainMenuPage(this);
            _pausePage = new PausePage(this);
            _settingsPage = new SettingsPage(this);
            _levelSelectPage = new LevelSelectPage(this);
            _scenePage = new ScenePage(this);
            _aboutPage = new AboutPage(this);
            _resultPage = new ResultPage(this);

            EnsureMenuLayout();

            //The backdrop is the bottom of the stack for the life of the program — the world every menu page
            //shows behind itself — and the game opens on the title card over it, which replaces itself with
            //the front end once its beat is up. Pushed after the trees exist, since a page puts its own tree
            //into the desktop the moment the stack makes it active.
            _screens.Push(_backdrop);
            _screens.Push(_splashPage);
        }

        /// <summary>
        /// Builds the widget tree at the viewport's current size, and rebuilds it when that size has moved
        /// far enough to matter. Called from <see cref="Draw"/> right before the menu is rendered rather than
        /// hooked onto the resize event, so there is one place it can be out of date and it is the frame that
        /// is about to draw it — a fullscreen switch, a window drag and the first frame all come through here.
        /// </summary>
        private void EnsureMenuLayout()
        {
            int height = GraphicsDevice.Viewport.Height;
            int quantized = height / MENU_REBUILD_QUANTUM;

            if (quantized == _menuBuiltForHeight) return;

            _menuBuiltForHeight = quantized;
            _menuScale = height / (float)MENU_DESIGN_HEIGHT;

            //Each size is its own rasterized atlas, so they are asked for once per rebuild and not per label.
            //Quantizing the rebuild is what keeps a drag from asking for a hundred slightly different sizes.
            //
            //Split by role, not by weight: the title, the entries a player picks from and a result's headings
            //are the loud type and are set in the display face; SMALL stays in Inter, because it is the size
            //About's paragraphs and a level's rules are read at and a condensed display face closes up there.
            _menuFontSmall = _menuFontSystem.GetFont(Scaled(MENU_FONT_SMALL));
            _menuFontBody = _menuFontSystemDisplay.GetFont(Scaled(MENU_FONT_BODY));
            _menuFontHeading = _menuFontSystemDisplay.GetFont(Scaled(MENU_FONT_HEADING));
            _menuFontTitle = _menuFontSystemDisplay.GetFont(Scaled(MENU_FONT_TITLE));
            _menuFontStars = _menuFontSystem.GetFont(Scaled(MENU_FONT_STARS));

            //The trees themselves are NOT rebuilt here: each page rebuilds its own the next time it is asked
            //for one, against this generation (MenuLayoutGeneration). A page the player never opens never
            //builds a tree at all, where this used to rebuild all eight on every step of a window drag.
            //
            //Re-asserts the page the player was on, which is what pulls its tree through at the new size, and
            //with it everything ShowPage keeps in step. Not a menu page while a level is being played, when
            //this is not called at all.
            if (_screens.Active is MenuPage page) ShowPage(page);
        }

        /// <summary>
        /// Bumped whenever the menu is laid out at a new size. A page holds the generation its tree was built
        /// for and rebuilds when they differ — which is how a rebuild reaches a page that is not on screen at
        /// the moment the window is resized.
        /// </summary>
        internal int MenuLayoutGeneration => _menuBuiltForHeight;

        /// <summary>A 2160p design figure at the viewport's actual size. Never below one pixel.</summary>
        internal int Scaled(int designUnits) => Math.Max(1, (int)MathF.Round(designUnits * _menuScale));

        internal Thickness ScaledThickness(int horizontal, int vertical) =>
            new(Scaled(horizontal), Scaled(vertical));

        internal Thickness ScaledThickness(int left, int top, int right, int bottom) =>
            new(Scaled(left), Scaled(top), Scaled(right), Scaled(bottom));

        /// <summary>
        /// Reads one TTF out of the assembly's own resources into a <see cref="FontSystem"/>. Throws with the
        /// resource name in the message if it is not there, rather than letting the copy below fault on a null
        /// stream: the name is a string that has to agree with the .csproj's <c>EmbeddedResource</c> items and
        /// with the file's own path, and a bare <see cref="NullReferenceException"/> on startup says nothing
        /// about which of the three moved.
        /// </summary>
        private static FontSystem LoadEmbeddedFont(string resourceName)
        {
            FontSystem system = new();

            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded font resource not found: {resourceName}", resourceName);
            using MemoryStream buffer = new();

            stream.CopyTo(buffer);
            system.AddFont(buffer.ToArray());

            return system;
        }

        /// <summary>The page the splash hands over to when its beat is up.</summary>
        internal MainMenuPage MainMenuPage => _mainMenuPage;

        //The fonts and the palette are the frame's, not any one page's — every page is set in the same type
        internal SpriteFontBase MenuFontBody => _menuFontBody;
        internal SpriteFontBase MenuFontSmall => _menuFontSmall;
        internal SpriteFontBase MenuFontHeading => _menuFontHeading;
        internal SpriteFontBase MenuFontTitle => _menuFontTitle;
        internal SpriteFontBase MenuFontStars => _menuFontStars;

        //What the pages ask the game about itself. Read-only: a page shows state and asks for an action, and
        //nothing here lets it write one directly.
        internal bool HasSession => _gameplayScreen != null && _gameplayScreen.IsBuilt;
        internal bool IsFullscreen => _fullscreen;
        internal int SupersampleFactor => _supersampleFactor;
        internal float Exposure => _exposure;
        internal byte SkyDomeNumber => _skyDome;
        internal bool IsFpsOverlayVisible => _info.Visible;
        internal bool IsFpsUncapped => _uncappedFps;
        internal float MasterVolume => _masterVolume;
        internal float SfxVolume => _sfxVolume;
        internal float MusicVolume => _musicVolume;
        internal float AmbienceVolume => _ambienceVolume;
        internal bool IsAberrationEnabled => _aberration;
        internal bool IsGrainEnabled => _grain;
        internal SceneKind Scene => _scene;

        internal int LevelCount => _levelSet?.Count ?? 0;
        internal string LevelDisplayName(int index) => _levelSet.DisplayName(index);
        internal string LevelRulesText(int index) => _levelSet.DescribeRules(index);

        /// <summary>All the stars collected across the campaign — the currency unlocks are weighed in.</summary>
        internal int TotalStars => _progress?.TotalStars ?? 0;

        /// <summary>The best stars one entry has earned, or 0 for a level never cleared.</summary>
        internal int LevelStars(int index) =>
            _progress != null && _levelSet != null && index >= 0 && index < _levelSet.Count
                ? _progress.StarsFor(_levelSet.Levels[index].File)
                : 0;

        /// <summary>
        /// The total stars the entry demands before it unlocks. Zero — an absent rule, a missing set and an
        /// index outside it — means open from the start, which is the read site the nullable rule is
        /// documented against, like the budget's and the ceiling's in <c>GameplayScreen.Rules.cs</c>.
        /// </summary>
        internal int LevelMinStars(int index) =>
            _levelSet != null && index >= 0 && index < _levelSet.Count
                ? _levelSet.Levels[index].MinStars.GetValueOrDefault()
                : 0;

        /// <summary>
        /// Whether the entry may be played yet. A missing set (the built-in level) and an unauthored gate are
        /// both open, so the gate only ever bites where a set said it should.
        /// </summary>
        internal bool IsLevelUnlocked(int index) => TotalStars >= LevelMinStars(index);

        /// <summary>
        /// Names the level being played in the window's title bar, and restores the plain
        /// <see cref="GAME_TITLE"/> when there is none. Numbered as the picker numbers it, so "the fourth
        /// one" means the same thing in both places.
        /// <para>
        /// It is for <b>talking about</b> a level rather than for playing one: the HUD deliberately carries
        /// no level name — a name is not something a player reads mid-shot — but a title bar is legible in a
        /// screenshot and in a window list, which is exactly what is wanted when a level is being reported on.
        /// </para>
        /// <para>
        /// Out-of-range falls back to the plain title, which is the fallback pyramid's case: no set was read,
        /// so there is no entry to name and the picker's own "Built-in level" wording is not repeated here.
        /// A level whose <i>file</i> failed to parse still shows its entry's name while the pyramid plays,
        /// and that is the level the player chose — the failure has its own <c>[levels]</c> line.
        /// </para>
        /// </summary>
        internal void ShowLevelInTitle(int index) =>
            Window.Title = index >= 0 && index < LevelCount
                ? $"{GAME_TITLE} — {index + 1}. {LevelDisplayName(index)}"
                : GAME_TITLE;

        //The actions a page's entries invoke. Named for what the player asked for rather than for how it is
        //done, so a page reads as a list of choices.
        internal void ContinueGame() => StartGame(newGame: false);
        internal void OpenLevelSelect() => OpenPage(_levelSelectPage);
        internal void OpenSceneSelect() => OpenPage(_scenePage);
        internal void OpenSettings() => OpenPage(_settingsPage);
        internal void OpenAbout() => OpenPage(_aboutPage);

        /// <summary>
        /// What the result screen's "Main Menu" does. The session is torn down, not kept: a level that has
        /// ended is not one to "Continue" into, and the front end should offer "Play" rather than "Continue"
        /// into a level that is already finished.
        /// </summary>
        internal void EndSessionAndReturnToMainMenu()
        {
            _gameplayScreen.TearDown();
            ReturnToMainMenu();
        }

        /// <summary>
        /// Puts a page's tree into the shared Myra desktop and brings it up to date. Called by the page itself
        /// when the stack makes it the active one — which covers a push, a pop, a replace and a reset alike,
        /// so there is one path here rather than a branch per way of arriving.
        /// </summary>
        internal void ShowPage(MenuPage page)
        {
            //Everything that has to agree with the game's state — the resume entry, the setting values, the
            //marked scene, the score just earned — is re-read here rather than per frame, because a page can
            //only change while nobody is looking at it.
            page.Refresh();

            //Whether the frame behind is dimmed still belongs to where the page was opened from, and the
            //stack is still what knows (DimsFrame) — but the scrim itself is the host's quad since #114,
            //asked per frame in Draw rather than set onto the root here: Myra's own background paint stops
            //short of the viewport's bottom edge, and the strip it left undimmed is the very bug.
            _desktop.Root = page.Root;

            //Last: Refresh above has finished deciding which entries this page actually shows — the resume
            //entry, Next Level — and the walk reads what is up rather than what was built.
            CollectNavEntries();
        }

        /// <summary>Opens a page over whatever is up. Backing out of it is a pop; nothing has to remember where it came from.</summary>
        private void OpenPage(MenuPage page) => _screens.Push(page);

        /// <summary>
        /// One level back, wherever Escape or the pad's B is pressed: out of a sub-screen to the screen that
        /// opened it, out of the pause menu into the game.
        /// <para>
        /// The main menu has no back — quitting is a menu item, and a front end that closes when a key is
        /// tapped is one that closes by accident. The result screen has none either, and for a different
        /// reason: the level has already ended, so there is nothing to resume into and "back one level" has no
        /// meaning; Retry, Next Level and Main Menu are the only ways off it.
        /// </para>
        /// </summary>
        private void MenuBack()
        {
            if (_screens.Active is not MenuPage page || !page.CanGoBack) return;

            //After the guard, so a screen with no back stays silent as well as still.
            _audio.PlayUiBack();

            //The pause's own back is not a plain pop but a resume: the game underneath has to start running
            //again, and ResumeGame is the one door back into it. Everything else is one level off the stack,
            //whatever opened it.
            if (page is PausePage) ResumeGame();
            else _screens.Pop();
        }

        #region Menu navigation (pad and arrow keys)

        /// <summary>
        /// Re-reads the entries of the screen that is up, in the order they are drawn. Walking the shown tree
        /// is deliberate: a list registered per button is one that an entry added later silently falls out of,
        /// and a screen whose entries come and go — the resume entry, Next Level — would need the registration
        /// repeating anyway. It runs on a screen change and on a layout rebuild, never per frame.
        /// </summary>
        private void CollectNavEntries()
        {
            //The INDEX does not survive a screen change — entry 3 of the settings screen is not entry 3 of the
            //one it returns to — but whether the cursor was up does. A pad player who opens Settings and backs
            //out again would otherwise land on a screen with no cursor and have to press a direction to get one
            //back, every time; and putting it up unconditionally would light an entry for a player who is
            //using the pointer and never asked for one.
            bool cursorWasUp = _navIndex >= 0;

            _navEntries.Clear();
            _navIndex = -1;
            _navDirection = 0;

            CollectNavEntries(_desktop.Root);

            if (cursorWasUp && _navEntries.Count > 0) _navIndex = 0;

            //Re-baseline the pointer, or the very next frame reads a move the PLAYER did not make and takes
            //the cursor straight back down: the game recentres the mouse every frame while it is being played,
            //so a pause always arrives with the pointer somewhere other than where the menu last saw it. One
            //poll on a screen change, which is not a per-frame path.
            MouseState mouse = Mouse.GetState();
            _navMouseAt = new Point(mouse.X, mouse.Y);

            ApplyNavHighlight();
        }

        private void CollectNavEntries(Widget widget)
        {
            //An entry that is not shown is not an entry — this is what keeps the focus off the resume entry
            //before there is a session, and off Next Level when the level was not passed
            if (widget == null || !widget.Visible) return;

            if (widget is Button button)
            {
                if (button.Enabled) _navEntries.Add(button);

                //A button's content is its label, never another entry
                return;
            }

            //A scroller's content is not among an IContainer's Widgets, so it has to be descended into by
            //name — without this the level picker's entries were invisible to the pad and the arrow keys,
            //and the first Down landed on Back, the one button outside the scroller (#91 is where that was
            //finally SEEN, on a page whose focus is spelled out in a detail line; the scrolling list before
            //it had the same hole and nothing that made it visible).
            if (widget is ScrollViewer scroller)
            {
                CollectNavEntries(scroller.Content);
                return;
            }

            //Myra keeps a container's real child list internal, so the walk goes through the public
            //multiple-items interface — which is every container this menu is built from (Panel,
            //VerticalStackPanel, Grid). A Label or an Image simply has no children and ends the branch.
            if (widget is IContainer container)
                foreach (Widget child in container.Widgets) CollectNavEntries(child);
        }

        /// <summary>
        /// Lights the focused entry with the pointer's own hover brush, so a pad-driven menu reads exactly as a
        /// moused one rather than growing a second visual language. Myra will not do this on its own — its
        /// <c>OverBackground</c> follows the pointer and nothing else — so the focused entry's resting
        /// background is swapped instead, which the hover brush then still overrides for the pointer.
        /// </summary>
        private void ApplyNavHighlight()
        {
            //Exactly one device drives at a time. While the cursor is up the pointer's own hover is switched
            //OFF, because the pointer does not have to move to sit over an entry — a pause recentres it, and
            //it lands wherever the column happens to be, lighting a second entry beside the focused one. That
            //reads as a bug, not as two input devices. Moving the pointer puts the cursor down again (see
            //UpdateMenuNavigation), which restores the hover in the same pass.
            bool cursorUp = _navIndex >= 0;

            for (int i = 0; i < _navEntries.Count; i++)
            {
                IBrush rest = i == _navIndex ? MENU_BUTTON_OVER_BRUSH : MENU_BUTTON_BRUSH;

                _navEntries[i].Background = rest;
                _navEntries[i].OverBackground = cursorUp ? rest : MENU_BUTTON_OVER_BRUSH;
            }

            //A page may present the focused entry somewhere other than on the entry itself — the level
            //picker's detail line spells out the tile the cursor stands on (#91) — so the active page is told
            //which button that is. Null when the pointer took over; the page's own hover events carry that
            //half, so between the two exactly one device feeds the detail at a time, like the highlight.
            if (_screens.Active is MenuPage page)
                page.NavFocusChanged(cursorUp && _navIndex < _navEntries.Count ? _navEntries[_navIndex] : null);
        }

        /// <summary>
        /// The menu's directional input: the D-pad, the left stick and the arrow keys move the focus, A or
        /// Enter presses the focused entry, B backs out exactly as Escape does.
        /// <para>
        /// This is what makes the game shippable on a pad at all — it is otherwise fully playable on one (aim
        /// on the right stick, fire on the right trigger, precise aim on the left), and <c>Buttons.Back</c>
        /// already opens the pause menu, so without this the pad could open a menu it could not then use.
        /// </para>
        /// <para>
        /// Called only while the window is focused, since a pad reports through XInput whether it is or not —
        /// the same gate the precise-aim trigger has.
        /// </para>
        /// </summary>
        private void UpdateMenuNavigation(float elapsed, KeyboardState keyboard, GamePadState pad, bool edgeInputAllowed)
        {
            if (_navEntries.Count == 0) return;

            //Moving the pointer puts the focus cursor away again: the hover and the cursor use the same
            //highlight, and two entries lit at once reads as a bug rather than as two input devices
            MouseState mouse = Mouse.GetState();

            if (Math.Abs(mouse.X - _navMouseAt.X) > NAV_MOUSE_WAKE_PIXELS
                || Math.Abs(mouse.Y - _navMouseAt.Y) > NAV_MOUSE_WAKE_PIXELS)
            {
                _navMouseAt = new Point(mouse.X, mouse.Y);

                if (_navIndex >= 0)
                {
                    _navIndex = -1;
                    ApplyNavHighlight();
                }
            }

            int direction = 0;

            if (keyboard.IsKeyDown(Keys.Down) || pad.IsButtonDown(Buttons.DPadDown)
                || pad.ThumbSticks.Left.Y < -NAV_STICK_DEADZONE) direction = 1;
            else if (keyboard.IsKeyDown(Keys.Up) || pad.IsButtonDown(Buttons.DPadUp)
                || pad.ThumbSticks.Left.Y > NAV_STICK_DEADZONE) direction = -1;

            //A held direction steps once, waits, then walks — a stick read per frame would cross the whole
            //list before the player let go
            if (direction == 0)
            {
                _navDirection = 0;
            }
            else if (direction != _navDirection)
            {
                _navDirection = direction;
                _navRepeatDelay = NAV_REPEAT_DELAY;
                StepNavFocus(direction);
            }
            else
            {
                _navRepeatDelay -= elapsed;

                if (_navRepeatDelay <= 0f)
                {
                    _navRepeatDelay = NAV_REPEAT_INTERVAL;
                    StepNavFocus(direction);
                }
            }

            if (!edgeInputAllowed) return;

            //B is Escape. Read before A, since backing out changes the screen the accept below would act on.
            if (pad.IsButtonDown(Buttons.B) && !_previousPad.IsButtonDown(Buttons.B))
            {
                MenuBack();
                return;
            }

            if ((pad.IsButtonDown(Buttons.A) && !_previousPad.IsButtonDown(Buttons.A)) || IsKeyEdge(keyboard, Keys.Enter))
            {
                //Pressing accept with the cursor down only raises it. Firing the top entry instead would make
                //the pad's first press mean whatever happened to be first — "New Game" over a session in
                //progress, on the very screen that exists to offer Continue.
                if (_navIndex < 0) StepNavFocus(1);
                else ActivateNavEntry();
            }
        }

        private void StepNavFocus(int direction)
        {
            //The first press only shows the cursor, at the top of the list, rather than stepping off nothing
            _navIndex = _navIndex < 0
                ? (direction > 0 ? 0 : _navEntries.Count - 1)
                : (_navIndex + direction + _navEntries.Count) % _navEntries.Count;

            //Only user input reaches here — a screen change restores the cursor in CollectNavEntries by
            //assignment, deliberately, so arriving on a page does not tick.
            _audio.PlayUiTick();

            ApplyNavHighlight();
        }

        /// <summary>
        /// Presses the focused entry. The action is carried on the button's own <c>Tag</c> because the entries
        /// are found by walking the widget tree, which has no way back to the delegate handed to
        /// <see cref="MenuButton"/> — and reaching into Myra's own click plumbing would tie this to a version
        /// of it. Returns immediately after: the action may swap the screen or tear the session down, and
        /// <see cref="_navEntries"/> is not the same list afterwards.
        /// </summary>
        private void ActivateNavEntry()
        {
            if (_navIndex < 0 || _navIndex >= _navEntries.Count) return;

            (_navEntries[_navIndex].Tag as Action)?.Invoke();
        }

        #endregion

        /// <summary>
        /// A dark plate behind a column that carries actual prose. A scrim alone is not enough for small text
        /// over a moving scene — every line lands on a different background — and darkening the whole scrim
        /// would throw away the scene the screen is standing in. No frame: the edge of the plate is the tone
        /// step itself, which is all it takes, and a drawn border is one more shape to fight the backdrop.
        /// The button screens need no plate at all — a button carries its own background.
        /// </summary>
        internal Panel Plate(Widget content)
        {
            Panel plate = new()
            {
                Background = new SolidBrush(MENU_PLATE),
                Padding = ScaledThickness(106, 67),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            plate.Widgets.Add(content);

            return plate;
        }

        /// <summary>
        /// A screen: the column of widgets centred over the whole frame. It carries <b>no background</b> —
        /// the scrim a dimming page wants is the host's own quad since #114 (see the scrim block in Draw) —
        /// but the panel still stretches, because it is what centres the column and what Myra hit-tests.
        /// </summary>
        internal static Panel ScreenRoot(Widget content)
        {
            Panel panel = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            panel.Widgets.Add(content);

            return panel;
        }

        /// <summary>
        /// Wraps a column that may be taller than the window in a scroller, bounded to what is left of the
        /// viewport once the things around it have their room. Everything else on a page is short by
        /// construction; a level list is as long as the campaign, and the tenth level made the picker overrun
        /// the window — its last two entries <i>and the Back button</i> were off the bottom with no way to
        /// reach any of them.
        /// <para>
        /// The heading and Back deliberately stay <b>outside</b> it: the way out of a page must not be the
        /// thing that scrolled away. Bounded at build time off the live viewport, which is correct because a
        /// resize rebuilds the tree (see <c>MenuPage.Root</c>).
        /// </para>
        /// <para>
        /// <b>The pad and the arrow keys do not scroll it.</b> They reach the entries inside it —
        /// <see cref="CollectNavEntries(Widget)"/> descends into the scroller's content by name, which it did
        /// not until #91 (the content is not among an <c>IContainer</c>'s <c>Widgets</c>, so everything in
        /// here was simply invisible to the walk and the first Down landed on Back) — but a focused entry out
        /// of view stays out of view. The mouse wheel is the way through an overlong page today; making the
        /// walk scroll its entry into view is the fix, and it belongs with the navigation rather than here.
        /// </para>
        /// </summary>
        /// <param name="reservedDesignUnits">Height the page needs around the scroller — its heading, its
        /// Back button and the plate's padding — in the same 2160p design units everything else here uses.</param>
        internal ScrollViewer MenuScroll(Widget content, int reservedDesignUnits)
        {
            //Floored, so a window too short for the reserve gives a usable scroller rather than a zero-height
            //one that hides the list completely
            int available = GraphicsDevice.Viewport.Height - Scaled(reservedDesignUnits);

            return new ScrollViewer
            {
                Content = content,
                ShowHorizontalScrollBar = false,
                MaxHeight = Math.Max(Scaled(MENU_SCROLL_MIN_HEIGHT), available),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        /// <summary>The least a <see cref="MenuScroll"/> may be bounded to, however short the window.</summary>
        private const int MENU_SCROLL_MIN_HEIGHT = 400;

        internal VerticalStackPanel MenuColumn() => new()
        {
            Spacing = Scaled(MENU_COLUMN_SPACING),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        internal Label ScreenHeading(string text) => new()
        {
            Text = text,
            Font = _menuFontHeading,
            TextColor = MENU_TEXT,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = ScaledThickness(0, 0, 0, 43),
        };

        internal Button MenuButton(string text, Action onClick) => MenuButton(text, onClick, out _);

        /// <summary>
        /// One menu entry. Myra's default button style is a framed grey tool button, so every brush is stated
        /// here instead: dark glass at rest, and the pointer <b>lifts</b> it with a wash of white rather than
        /// tinting it — see the palette above for why nothing in this menu carries a hue. No border either:
        /// the tone step at the button's edge is enough to read it as a control, and a drawn frame over seven
        /// different backdrops is a shape competing with all of them.
        /// </summary>
        internal Button MenuButton(string text, Action onClick, out Label label)
        {
            label = new Label
            {
                Text = text,
                Font = _menuFontBody,
                TextColor = MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Button button = MenuClickable(label, onClick);

            button.Width = Scaled(MENU_BUTTON_WIDTH);
            button.Padding = ScaledThickness(43, 18);

            return button;
        }

        /// <summary>
        /// A tile-shaped menu entry (#91): <see cref="MenuButton"/>'s behaviour around the caller's own
        /// content, sized by the caller — the level picker's grid is built out of these. What makes it a menu
        /// entry rather than merely a button is everything <see cref="MenuClickable"/> carries: the shared
        /// brushes the focus highlight swaps by identity, the click sound, and the <c>Tag</c> the pad
        /// activates through.
        /// </summary>
        internal Button MenuTile(Widget content, Action onClick, int designWidth, int designHeight)
        {
            Button button = MenuClickable(content, onClick);

            button.Width = Scaled(designWidth);
            button.Height = Scaled(designHeight);
            button.Padding = ScaledThickness(18, 12);

            return button;
        }

        /// <summary>
        /// The behaviour every clickable menu entry shares, whatever its shape: the palette's brushes, the
        /// cleared border, and one press wrapper for every input device.
        /// </summary>
        private Button MenuClickable(Widget content, Action onClick)
        {
            Button button = new()
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Center,
                //Shared rather than one brush per button: a brush is a paint recipe with no state of its own,
                //and the focus highlight swaps between these two by identity (see ApplyNavHighlight)
                Background = MENU_BUTTON_BRUSH,
                OverBackground = MENU_BUTTON_OVER_BRUSH,
                PressedBackground = MENU_BUTTON_PRESSED_BRUSH,

                //Myra's stylesheet gives a button a border of its own, so it has to be explicitly cleared
                Border = null,
                BorderThickness = new Thickness(0),
            };

            //One wrapper for every input device (#46): the mouse reaches it through Myra's Click, the pad and
            //the arrow keys through the Tag that ActivateNavEntry invokes — so the press sounds once wherever
            //it came from, and an entry added later cannot forget its click.
            Action pressed = () =>
            {
                _audio.PlayUiClick();
                onClick();
            };

            button.Click += (_, _) => pressed();

            //The pad and the arrow keys find their entries by walking the widget tree, which has no way back
            //to this delegate — so it rides on the widget itself. See ActivateNavEntry.
            button.Tag = pressed;

            return button;
        }

        #endregion

        /// <summary>
        /// The menu's shared frame, run by the <b>active</b> page from its <see cref="MenuPage.Update"/>: the
        /// pointer, the keys the menu does not carry as buttons — Escape, and the two display toggles that are
        /// useful from anywhere — and the pad navigation. Myra consumes the mouse and the keyboard itself (in
        /// <c>Desktop.Render</c>, at the end of <see cref="Draw"/>), so this owes it nothing else.
        /// <para>
        /// The keyboard snapshot is stored into the very same <see cref="_previousKeyboard"/> the play loop
        /// uses, which is what makes the two hand over cleanly: the Escape that paused the game is still down
        /// on the first menu frame and is correctly not seen as a second press, and the Escape that resumed it
        /// is likewise not seen again by the play loop.
        /// </para>
        /// </summary>
        internal void UpdateMenuChrome(GameTime gameTime)
        {
            //The cursor is the pointer here, not the aim; nothing recentres it while a menu is up
            IsMouseVisible = true;

            if (!IsActive) return;

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            KeyboardState keyboard = Keyboard.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            if (EdgeInputAllowed)
            {
                //Escape backs out one level; MenuBack owns which screens have a back at all
                if (IsKeyEdge(keyboard, Keys.Escape)) MenuBack();

                //The same two display keys the game has, because a player who wants windowed mode or no
                //FPS line wants them before pressing Play as much as after
                if (IsKeyEdge(keyboard, Keys.F11)) ToggleFullscreen();
                if (IsKeyEdge(keyboard, Keys.F12)) ToggleFpsOverlay();
            }

            //After the keys above, so an Escape and a B press in the same frame cannot both act, and
            //before the snapshots below, which are what its own edge tests are read against
            UpdateMenuNavigation(elapsed, keyboard, pad, EdgeInputAllowed);

            _previousKeyboard = keyboard;
            _previousPad = pad;
        }
    }
}
