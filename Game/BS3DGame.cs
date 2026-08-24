using BS3D.Audio;
using BS3D.Effects;
using BS3D.Platform;
using BS3D.Screens;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Screens;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D
{
    /// <summary>
    /// The <b>host</b> — what outlives a session (#65). It owns the window and the device, the content, the
    /// shared setting the menus and the game both stand in (the sky, the scene backdrops, the city, the
    /// island and its drain), the linear-radiance render pipeline, the screen stack and the Myra desktop the
    /// menu pages share. The game itself — the simulation, the cluster, the gun, the shot, the HUD — is
    /// <see cref="GameplayScreen"/>, a screen on that same stack; a pause is a page pushed over it.
    /// <para>
    /// The frame is not "draw the screens back to front" naively: the pipeline binds an HDR target, draws
    /// the world into it and resolves once, and gameplay draws in slots <i>inside</i> that sequence. So the
    /// screens own the frame and the host owns the pieces — <see cref="BeginSceneDraw"/>,
    /// <see cref="DrawSettingGlass"/> and <see cref="FinishSceneDraw"/> are the setting's slices, and each
    /// bottom screen (the backdrop, or the gameplay screen) runs the sequence with its own work in the gaps.
    /// </para>
    /// <para>
    /// <b>Partial across five files</b> since #71, along the seams the responsibilities already had. This one
    /// is the host as a host: the fields it does not share, the constructor, <see cref="Initialize"/>,
    /// <see cref="LoadContent"/>, <see cref="Update"/>, <see cref="Draw"/>, <see cref="UnloadContent"/>, the
    /// level set and the session's place on the stack. <c>.Scene.cs</c> has the setting and the frame's
    /// slices, <c>.Menu.cs</c> the Myra front end and everything a page may ask of the host,
    /// <c>.Quality.cs</c> the adaptive probe, <c>.Settings.cs</c> the verbs a settings row invokes.
    /// <see cref="LoadContent"/> stays whole and stays here: it is one ordered startup script whose order is
    /// load-bearing (the scene before the startup dome before the quality tier, the menu last), and this is
    /// the one place that order can be read. Partials hide coupling rather than removing it — the fan-out
    /// through <c>_settingsPage.Refresh()</c>, the three writers of <see cref="Game.IsMouseVisible"/>, the
    /// input snapshots the menu and the play loop share — so this is staging for extractions, not a
    /// substitute for them.
    /// </para>
    /// </summary>
    public partial class BS3DGame : Microsoft.Xna.Framework.Game
    {
        #region The frame

        //The windowed default is 16:9, the narrowest aspect the game targets, so what is framed in a window
        //is the tightest case and a wider display only adds width (CreatePerspectiveFieldOfView takes the
        //vertical FOV, so this is Hor+).
        private const int WINDOW_WIDTH = 1600;
        private const int WINDOW_HEIGHT = 900;

        private const float DEFAULT_EXPOSURE = 1.1f;

        //The camera's whole shake, scaled off CameraShake's defaults through this one dial. The gun throws
        //itself back visibly when it fires, so the camera does not have to carry the shot's force on its
        //own — and carrying all of it read as the lens being hit rather than as a gun going off.
        private const float CAMERA_SHAKE_SCALE = 0.45f;

        private readonly GraphicsDeviceManager _graphics;

        //Whether presentation is uncapped (vsync off). Seeded by the "nocap" launch argument — which stays,
        //as the benchmark's way of starting with the cap off without a trip through the menus — and a
        //Settings row since #124 (ToggleFpsLimit), which is why it is no longer readonly. Both writers go
        //through SetGraphics: SynchronizeWithVerticalRetrace and the PresentationInterval each read this
        //flag, the latter through PreparingDeviceSettings when ApplyChanges resets the device.
        private bool _uncappedFps;

        //Pinned from the command line for a reproducible measurement. The scene is otherwise a different one of
        //the twelve every launch, which makes any A/B of the frame's cost meaningless — they are nothing like
        //each other in what they cost — and two of them bring a dome of their own, so the dome has to be
        //pinnable too. Null in both means "as the game normally does it".
        private readonly SceneKind? _startupScene;
        private readonly byte? _startupSkyDome;

        //Seeded from the command line and then owned by the settings screen: supersampling resizes the scene
        //target (EnsureSceneTarget compares dimensions, so changing the factor is what recreates it) and the
        //exposure is one uniform on the tonemap. Both are the dials a weak machine and a bright monitor reach
        //for first, which is exactly why they are in the menu rather than only in argv.
        private int _supersampleFactor;

        //An explicit ssaa= from the command line, and null when nobody said. The tier owns supersampling, so
        //ApplyQuality writes the factor — and used to write it over the override on the very next line of
        //startup, because the constructor honoured ssaa= and LoadContent then applied the tier on top. That
        //made the flag the benchmark and screenshot harnesses exist to use a no-op whenever a tier was named
        //with it, and worse than a no-op: two runs differing only in ssaa= rendered identical frames and read
        //as "supersampling is free here" (#122).
        private readonly int? _supersampleOverride;

        private float _exposure;
        private bool _fullscreen;

        //The client size the window last had while windowed — what SetGraphics restores instead of the
        //windowed default (#137). Seeded with that default, which is therefore what the constructor's own
        //pass applies, before there is a window whose size could be read.
        private Point _windowedSize = new(WINDOW_WIDTH, WINDOW_HEIGHT);

        private RecoilCamera _camera;

        //Procedurally synthesized SFX (shot, landing). Built once in LoadContent and shared by the gameplay
        //screen and, later, the menu — same pattern as the camera.
        private ProceduralAudio _audio;

        //The victory display. The frame's, not the session's: it has to go on running once the result screen
        //covers the gameplay screen, which is exactly when the player is watching it.
        private Fireworks _fireworks;

        //The campaign's closing confetti (#215), on the frame for the same reason and alongside the display
        //rather than instead of it: finishing the whole set is the one ending that gets both.
        private Confetti _confetti;

        //The cup the player just won (#183). On the frame because it is drawn inside the scene's own HDR pass,
        //which is the host's; the result page owns only the decision to show one.
        private TrophyPodium _trophy;

        //The game's name in 3D over the front end (#248). On the host because its meshes and renderers outlive
        //every screen and because it is enrolled in the scene's light rig like the rest of the setting; the
        //backdrop screen owns only the decision to draw it, which is what keeps it to the main menu.
        private TitleWordmark _titleWordmark;

        //The level theme. On the host with the rest of the audio, because it outlives any one session and a
        //track that restarted from the top on every retry would be exhausting.
        private ProceduralMusic _music;

        //Whether the front end's music is on — the edge detector for the stack question in Update (#46).
        private bool _menuMusicOn;

        //The scenes' ambient beds (#46): one looping texture per backdrop, crossfaded by SetScene. On the
        //host with the rest of the audio — the scene is the host's, and its sound runs pause included.
        private ProceduralAmbience _ambience;

        //Testing only: the "celebrate" argument, fired once the display exists.
        private readonly bool _startupCelebrate;

        //Testing only: the "confetti" argument, fired once the field exists (#215).
        private readonly bool _startupConfetti;

        //Testing only: the "stars=" argument, which sets the rating the test result page reports (#183) and so
        //which of the four trophy cups it presents. Null leaves the authored three.
        private readonly int? _startupResultStars;

        //Testing only: the "streak=" argument, which pins what the HUD's multiplier readout shows (#180).
        private readonly int? _startupStreak;

        //Testing only: the "lasers" argument, read by the session's warning check every frame.
        private readonly bool _startupLasers;

        //Testing only: the "play" argument. Consumed on the first Update rather than at the end of
        //LoadContent, because the screen manager queues its mutations: BuildMenu's pushes are still pending
        //there, so StartGame's PopTo<BackdropScreen> would test an empty live stack, silently skip, and
        //leave the splash buried under the session for the rest of the run.
        private bool _startupPlay;

        //Testing only: the "level=" argument — which entry _startupPlay should open, as a 1-based place in the
        //set or as a name. Null means "the first one", which is what play alone has always done.
        private readonly string _startupLevel;

        //Testing only: the "preview=" argument — which entry the FRONT END should hang instead of rolling one
        //at random. Null means the roll, which is what a player always gets. It is the same reasoning as
        //scene= and sky=: the menu's camera is now framed for the map hanging under it (#254), so two shots of
        //the front end are only comparable if they are shots of the same map.
        private readonly string _startupPreview;

        //Testing only: the "result" argument, consumed on the first Update for the "play" reason above — the
        //page has to go over a stack that exists. It is how the end-of-level moment gets looked at at all:
        //everything about it (the released camera, the star reveal, the arena going out of focus) only happens
        //once a level has been won or lost, and neither can be scripted — the "celebrate" reasoning again.
        private bool _startupResult;

        //Testing only: the "blockdone" argument, which makes the "result" page above a BLOCK milestone instead of
        //an ordinary clear (#184). Same reasoning as "celebrate" and "result", one step further along: finishing a
        //block needs every level of a five-level chapter cleared, so the moment cannot be reached in a scripted
        //run at all — and it is the one thing about the milestone that has to be LOOKED at rather than asserted.
        private readonly bool _startupBlockDone;

        /// <summary>
        /// <c>lost</c>: makes the startup result page a FAILED one rather than a clear (#238). It exists because
        /// the page could not be photographed at all — <c>result</c> hardcoded <c>cleared: true</c>, so
        /// <c>stars=0</c> gave a starless CLEARED page and the fail state, its reason line included, had never
        /// been looked at outside a real loss. Same reasoning as <c>blockdone</c> and <c>stars=</c>: a state a
        /// test cannot reach is a state nobody checks.
        /// </summary>
        private readonly bool _startupLost;

        //Wall clock. Everything alive in the scene runs off it — the balls' heartbeat, the city's windows —
        //so none of it is tied to a simulation that may later be paused.
        private float _wallClock;

        private bool _wasActive = true;

        /// <summary>The one camera. The front end's backdrop orbits it; the gameplay screen poses it while playing.</summary>
        internal RecoilCamera Camera => _camera;

        /// <summary>Procedurally generated SFX, shared by the gameplay screen and the menu.</summary>
        internal ProceduralAudio Audio => _audio;

        /// <summary>
        /// The victory display. On the host rather than on the session because a cleared level puts the result
        /// screen over the session, and what the session goes on running under that page is its world alone
        /// (#241) — see <see cref="Fireworks"/>.
        /// </summary>
        internal Fireworks Fireworks => _fireworks;

        /// <summary>
        /// The campaign's closing confetti (#215). On the host for the reason <see cref="Fireworks"/> is, and
        /// separate from it because the campaign's ending is a different <i>kind</i> of celebration rather than
        /// a bigger one — see <see cref="Confetti"/>.
        /// </summary>
        internal Confetti Confetti => _confetti;

        /// <summary>
        /// The trophy cup shown on the result screen (#183). On the host because it is drawn in the scene's
        /// HDR pass; the result page decides which tier and when — see <see cref="TrophyPodium"/>.
        /// </summary>
        internal TrophyPodium Trophy => _trophy;

        /// <summary>
        /// The game's name as 3D lettering (#248). On the host because it is drawn in the scene's HDR pass and
        /// is lit by the scene's own rig; the backdrop screen decides <i>when</i>, which is while the main menu
        /// is the page on top — see <see cref="TitleWordmark"/>.
        /// </summary>
        internal TitleWordmark TitleWordmark => _titleWordmark;

        /// <summary>The level theme, synthesized at load and looped while a level is being played.</summary>
        internal ProceduralMusic Music => _music;

        /// <summary>The wall clock everything alive runs off, paused or not.</summary>
        internal float WallClock => _wallClock;

        /// <summary>
        /// Testing only (the <c>lasers</c> argument): pins the floor alarm's laser net on. Reaching it
        /// honestly means playing a level to within two ceiling steps of losing it, which can no more be
        /// scripted than clearing one can — the <c>celebrate</c> reasoning, for the session-owned effect.
        /// </summary>
        internal bool ForceLaserWarning => _startupLasers;

        /// <summary>
        /// Testing only (the <c>streak=</c> argument): the multiplier the HUD's streak readout should show,
        /// or null for the keeper's own. It exists because #180's capped state takes five consecutive scoring
        /// shots to reach, which is the <c>celebrate</c> reasoning again — and it changes the display only,
        /// never the scoring, so the lever cannot alter the thing it is there to look at.
        /// </summary>
        internal int? ForcedStreak => _startupStreak;

        /// <summary>
        /// Whether edge-driven input (presses, clicks) may act this frame. False for one frame after focus
        /// returns: the very click that refocuses a windowed game would otherwise read as a fresh press
        /// against a stale "released" state and fire an unintended shot, since input is not sampled while
        /// inactive. Computed once per <see cref="Update"/> and read by whichever screen is running.
        /// </summary>
        internal bool EdgeInputAllowed { get; private set; }

        /// <summary>
        /// Whether Myra may be fed mouse input this frame. Cleared whenever the top screen changes and set again
        /// once the left button is seen released, so a page cannot be clicked by a button that was already down
        /// when it arrived — the menu's counterpart to <see cref="EdgeInputAllowed"/>, and needed for the same
        /// reason by a different route: Myra holds its own previous-state and only sees input while a page is up,
        /// so its idea of "released" can be arbitrarily old. Keyboard and pad navigation are deliberately not
        /// gated on it, so a page under a held button is still operable, just not clickable.
        /// </summary>
        private bool _menuClickArmed = true;

        //What the flag above is watching for a change of. Held rather than compared against a screen count, so a
        //pop straight into a different page of the same depth is still a change.
        private Screen _lastActiveScreen;

        #endregion

        #region The overlay (display space, after the resolve)

        //FPS. A DrawableGameComponent, so the component list draws it in base.Draw — last of everything, in
        //display space, with its own SpriteBatch. F10 hides it (it was F12, which the screenshot took in #191;
        //the Testbed's own text overlay is still on F12).
        private InfoRenderer _info;

        //One SpriteBatch for everything drawn over the resolve: the gameplay screen's HUD and its crosshair
        //both go into this one. The white texel that used to sit beside it went with the crosshair in #76 —
        //Prazsky.Core.Render.Crosshair makes its own, and it was the texel's only consumer, so the host no
        //longer holds a texture for a mark it does not draw.
        private SpriteBatch _spriteBatch;

        internal SpriteBatch OverlayBatch => _spriteBatch;

        #endregion

        #region Post-processing (linear radiance in, sRGB out — the shared pipeline, #74)

        //The HDR scene target, the bloom pyramid, the tonemap resolve and every cached parameter live in
        //Prazsky.Core.Render.PostProcessPipeline now — one copy for all three executables. What stays here
        //is what is this executable's to decide: the look figures below and the Settings toggles that write
        //them. The underwater amount used to be here too, as this file's own arithmetic; it is
        //SceneRenderer.LensSubmergedAmount since #159, because the ball shader's submerge fade is released by
        //the same figure and two effects that hand over cannot read two copies of one expression.
        private PostProcessPipeline _pipeline;

        private EffectParameter _skyCameraPositionParam;

        private static readonly float GLARE_THRESHOLD = 0.55f;

        //Peak red/blue channel displacement at the frame CORNERS, as a fraction of the frame (the shader
        //grows it quadratically from zero at the centre, so the cluster and the gun stay registered and
        //only the periphery fringes). Off in Settings sets the uniform to 0, which also skips the shader's
        //whole branch.
        //
        //THIS NUMBER HAS BEEN ROUND THE HOUSES. It shipped at 0.0016 - under two corner pixels - and was
        //raised to 0.004 because nobody ever noticed it, which defeats a taste effect with its own Settings
        //row. At 0.004 it was noticed for the wrong reason: a ~5 px corner shift, with the red-to-blue split
        //twice that, tears any line thinner than a pixel into three separate coloured copies, and the island
        //cap's slab joints are a whole regular FIELD of such lines. That is what #126 was filed about.
        //
        //So it is back near the figure that was once called invisible - but the effect it drives is not the
        //same one. Tonemap.fx samples the spectrum across the shift now instead of taking one point per
        //channel, so green is averaged over the whole span and the red and blue lobes land at two thirds of
        //it: a soft colour wash at the periphery rather than three thin copies. Measured by eye on the
        //island's joints from a fixed camera: clean at 0.0015, fringing again at 0.002, and at 0.004 the
        //spectral version is still much too strong. What was NOT measured is the other half of the old
        //argument - whether this now reads at a glance the way 0.0016 failed to - so if it wants to be
        //louder, this constant is the dial and the joints are what to check it against.
        private static readonly float CHROMATIC_ABERRATION = 0.0015f;

        //On by default; a taste toggle in Settings, like the FPS counter (nothing persists — see docs).
        private bool _aberration = true;

        //Peak film-grain modulation at 50% grey, as a fraction of the display value. The shader weights
        //the grain by 4*luma*(1-luma), so it peaks in the mid-tones — about ±12/255 at its strongest
        //here (raised from 0.05, which was invisible at arm's length) — and vanishes into both black
        //and white: texture on the print, never sensor noise. One monochrome grain per OUTPUT pixel,
        //re-rolled every frame, applied after the tonemap curve. Off in Settings sets the uniform to 0,
        //which skips the shader's branch.
        private static readonly float FILM_GRAIN = 0.10f;

        //On by default, the aberration's sibling taste toggle
        private bool _grain = true;

        //Far lower than the streak star's 0.9: the pyramid ACCUMULATES on the way up, so the head carries
        //its own halo plus every wider level's, and the same subjective glow needs a fraction of the gain.
        private static readonly float GLARE_INTENSITY = 0.5f;

        #endregion

        #region Balls (the render set; the balls themselves are the session's)

        //Everything it takes to draw a ball is BallRenderSet's since #76: the three procedural sphere LODs and
        //the distances they are picked by, the three renderers built on them, every figure of the look (the
        //albedo, the five-gore beach pattern, the emission and translucency, the heartbeat's rate, depth,
        //direction and wavelength, the ripple's strength) and the (type × LOD) instance buckets each of which
        //becomes one instanced draw call. It stood here, in the Testbed's LoadContent and in the map editor,
        //with the bucket bookkeeping written a fourth time inside the session's own collect walk. Every one of
        //those figures kept its value and its reasoning; the pulse depth is now picked BY the ripple flag,
        //which is what keeps the breath from being left loud enough to drown the wave out.
        private BallRenderSet _balls;

        /// <summary>
        /// The ball meshes, renderers and instance buckets — content, like the barrel, so it is built once with
        /// the device up and outlives every session. The session opens one frame of collection on it per draw
        /// (<see cref="BallRenderSet.BeginFrame"/>) and hands it every ball there is.
        /// </summary>
        internal BallRenderSet Balls => _balls;

        #endregion

        #region The gun's hardware (the barrel; the gun's pose and magazine are the session's)

        //The procedural barrel and the renderer that draws it, with every figure the tube is cut to — all of it
        //CannonRig's since #76, shared with the Testbed. It outlives a session because it is content: the mesh
        //and the instance buffer are built once with the device up and disposed on the way out.
        private CannonRig _cannonRig;

        /// <summary>The barrel, for the session to draw with its own pose and to size the queue's place in the
        /// bore off (<see cref="CannonRig.PivotToFrontBall"/>).</summary>
        internal CannonRig CannonRig => _cannonRig;

        #endregion

        #region Levels

        /// <summary>
        /// The levels and the order they are played in, read from <c>Levels/Levels.json</c> beside the exe.
        /// Null when no set could be read at all, which is the one case the procedural fallback covers.
        /// </summary>
        private LevelSet _levelSet;

        /// <summary>
        /// The player's bests over the set — the stars and scores unlocks are gated on (#92, #111). Loaded
        /// beside the set and null exactly when the set is: progress measures a campaign, and the fallback
        /// pyramid is not one.
        /// </summary>
        private PlayerProgress _progress;

        private const string LEVELS_DIRECTORY = "Levels";

        /// <summary>The set the session installs its levels from. Null when none could be read.</summary>
        internal LevelSet LevelSet => _levelSet;

        #endregion

        #region The in-play HUD's fonts

        //The HUD is the GameplayScreen's, but its type is the frame's — the same display face the menu's own
        //loud type is set in, resolved per viewport height exactly as the menu's sizes are. Separate from EnsureMenuLayout,
        //which only runs while a menu is up, and quantized the same way so a window being dragged does not
        //ask the font system for a new atlas every frame.
        //Authored large on purpose. A HUD in a game is not a data readout — the score and the ball count are
        //two of the three things the player is actually tracking, and they have to land at a glance while the
        //eye is in the middle of the frame on the cluster; the corners are empty in both poses, so size costs
        //nothing here. HUD_FONT_SCORE leaves headroom for the pulse: the score is pivoted on its vertical
        //centre at HUD_MARGIN + height/2, so at PlayHud's clamped pulse ceiling it must still clear the frame.
        private const int HUD_FONT_SCORE = 140;
        private const int HUD_FONT_LABEL = 76;
        //The awards are read at a glance out on the cluster and then shrink as they fly to the corner, so they
        //are authored larger still than the score they land on
        private const int HUD_FONT_POPUP = 112;

        //The balls-left alarm's first step, which used to be a heavier weight and cannot be: the display face
        //has ONE weight, so a bold slot would resolve to the very same glyphs and the step would vanish in
        //silence — a documented escalation quietly reduced from three steps to two. It is a size step instead.
        //Kept modest: it has to read as the number leaning in, not as a second number, and the count is
        //pivoted on its bottom-left corner (see PlayHud.DrawBallsLeft), so growing it cannot push it off frame
        //the way the centre-pivoted score would be.
        private const float HUD_LOW_EMPHASIS = 1.14f;

        private SpriteFontBase _hudFontScore, _hudFontLabel, _hudFontPopup;
        private SpriteFontBase _hudFontScoreLoud, _hudFontLabelLoud;
        private int _hudFontsForHeight = -1;

        internal SpriteFontBase HudFontScore => _hudFontScore;
        internal SpriteFontBase HudFontLabel => _hudFontLabel;
        internal SpriteFontBase HudFontPopup => _hudFontPopup;

        /// <summary>The balls-left readout once the budget is low — the same face a step larger. See <see cref="HUD_LOW_EMPHASIS"/>.</summary>
        internal SpriteFontBase HudFontScoreLoud => _hudFontScoreLoud;

        /// <summary>The caption under a low balls-left readout, grown to match <see cref="HudFontScoreLoud"/>.</summary>
        internal SpriteFontBase HudFontLabelLoud => _hudFontLabelLoud;

        /// <summary>
        /// Resolves the HUD's fonts for the viewport they are about to be drawn into. Called by the gameplay
        /// screen at the top of its HUD pass.
        /// </summary>
        internal void EnsureHudFonts()
        {
            int quantized = GraphicsDevice.Viewport.Height / MENU_REBUILD_QUANTUM;
            if (quantized == _hudFontsForHeight) return;

            _hudFontsForHeight = quantized;
            _menuScale = GraphicsDevice.Viewport.Height / (float)MENU_DESIGN_HEIGHT;

            _hudFontScore = _menuFontSystemDisplay.GetFont(Scaled(HUD_FONT_SCORE));
            _hudFontLabel = _menuFontSystemDisplay.GetFont(Scaled(HUD_FONT_LABEL));
            _hudFontPopup = _menuFontSystemDisplay.GetFont(Scaled(HUD_FONT_POPUP));
            _hudFontScoreLoud = _menuFontSystemDisplay.GetFont(Scaled((int)(HUD_FONT_SCORE * HUD_LOW_EMPHASIS)));
            _hudFontLabelLoud = _menuFontSystemDisplay.GetFont(Scaled((int)(HUD_FONT_LABEL * HUD_LOW_EMPHASIS)));
        }

        #endregion

        #region Shared input state (menu ⇄ play handover)

        //One set of previous-frame snapshots for BOTH the menu chrome and the gameplay screen, and that is
        //load-bearing: the Escape that paused the game is still down on the first menu frame and must not be
        //seen as a second press by the menu, and the Escape that resumed it must likewise not be seen again
        //by the play loop. Two separate snapshots would each see the other's press as fresh — pause and
        //instant resume, forever.
        //The one white texel the host's scrim is stretched from (#114). The host used to keep one for the
        //crosshair and retired it when the shared Crosshair brought its own; this is its return, for the one
        //quad Myra may not draw — see the scrim block in Draw.
        private Texture2D _scrimTexel;

        private KeyboardState _previousKeyboard;
        private GamePadState _previousPad;

        internal KeyboardState PreviousKeyboard
        {
            get => _previousKeyboard;
            set => _previousKeyboard = value;
        }

        internal GamePadState PreviousPad
        {
            get => _previousPad;
            set => _previousPad = value;
        }

        /// <summary>A key pressed this frame that was not pressed last frame.</summary>
        internal bool IsKeyEdge(KeyboardState keyboard, Keys key) =>
            keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

        #endregion

        /// <param name="supersampleFactor">
        /// <c>null</c> when the player did not say — which is what lets <see cref="TuneQualityToFrameRate"/>
        /// lower it on hardware that cannot afford the default. An explicit <c>ssaa=</c> is never overridden.
        /// </param>
        /// <param name="scene">
        /// The backdrop to start in, or <c>null</c> for the usual random one of the twelve. Pinning it is what
        /// makes a frame-cost measurement repeatable — see <see cref="LogFrameRate"/>.
        /// </param>
        /// <param name="skyDome">The dome to start under, or <c>null</c> to let the scene choose as it normally does.</param>
        /// <param name="logFrameRate">Write one frame-rate line a second to stdout (the <c>logfps</c> argument).</param>
        /// <param name="quality">
        /// The tier to start at, or <c>null</c> to start at <see cref="QualityLevel.High"/> — the look the game is
        /// authored at — and let <see cref="TuneQualityToFrameRate"/> measure this machine.
        /// </param>
        /// <param name="celebrate">
        /// Testing only (the <c>celebrate</c> argument): fire the victory display on the front end. Clearing a
        /// level is the only thing that normally starts it and clearing one cannot be scripted, so this is how
        /// the fireworks get screenshotted and measured at all.
        /// </param>
        /// <param name="confetti">
        /// Testing only (the <c>confetti</c> argument): start the campaign's closing confetti on the front end
        /// (#215). One step further along <paramref name="celebrate"/>'s reasoning than <paramref name="blockDone"/>
        /// is: a block milestone needs five levels played to reach honestly, where this needs the whole campaign.
        /// </param>
        /// <param name="lasers">
        /// Testing only (the <c>lasers</c> argument): pin the floor alarm's laser net on while a level is
        /// being played — see <see cref="ForceLaserWarning"/>.
        /// </param>
        /// <param name="mute">
        /// Testing only (the <c>mute</c> argument): start with the master volume at zero — a scripted
        /// screenshot or benchmark run has no business making noise. The settings rows can still raise it.
        /// </param>
        /// <param name="play">
        /// Testing only (the <c>play</c> argument): drop straight into the first level, skipping the title
        /// card and the menu. The session's placement and fit figures only reach stdout once a level is
        /// built, and building one honestly needs a mouse on a Myra button — which a scripted run does not
        /// have. The stack ends up exactly as a player's Play click leaves it, so nothing downstream can
        /// tell the difference.
        /// </param>
        /// <param name="preview">
        /// Testing only (the <c>preview=</c> argument): which entry of the set the front end hangs over the
        /// island, instead of the one it rolls at random. The menu's camera is framed for the map under it
        /// since #254 — its stand-off, its aim height and the reach of its fly-in all come off that map's own
        /// size — so a shot of the front end says nothing next to another shot of it unless both hung the same
        /// map. Named the way <paramref name="level"/> is: a 1-based place in the set, or a name.
        /// </param>
        /// <param name="result">
        /// Testing only (the <c>result</c> argument): put a cleared level's result screen over the front end.
        /// Everything that happens at a level's end — the camera letting go of the gun, the stars landing one
        /// at a time, the arena going out of focus behind the page — can otherwise only be reached by winning
        /// or losing a level, which can no more be scripted than clearing one can. Pair it with
        /// <paramref name="celebrate"/> for the whole moment: fireworks over an arena going soft.
        /// </param>
        /// <param name="shotSeconds">
        /// Testing only (the <c>shot=</c> argument): wall-clock seconds after start at which to save a PNG of
        /// the frame, or null for none. It is the trigger F12 cannot be — a locked desktop takes no keystrokes
        /// — and the one that makes a shot repeatable. See <c>BS3DGame.Screenshot.cs</c>.
        /// </param>
        public BS3DGame(bool fullscreen = false, int? supersampleFactor = null, float exposure = DEFAULT_EXPOSURE,
            bool uncappedFps = false, SceneKind? scene = null, byte? skyDome = null, bool logFrameRate = false,
            QualityLevel? quality = null, bool celebrate = false, bool confetti = false, bool lasers = false,
            bool mute = false, bool play = false, bool result = false, bool blockDone = false, bool lost = false,
            int? resultStars = null, int? streak = null, float[] shotSeconds = null, string level = null,
            string preview = null, BallStyle? ballStyle = null)
        {
            BallStyleOverride = ballStyle;

            _fullscreen = fullscreen;
            _startupCelebrate = celebrate;
            _startupConfetti = confetti;
            _startupResultStars = resultStars;
            _startupStreak = streak;
            _startupLasers = lasers;
            _startupLevel = level;
            _startupPreview = preview;

            //Naming a level means playing it, so "level=" implies "play" rather than needing it alongside
            _startupPlay = play || level != null;
            //Asking for a FAILED result page means asking for the result page, so "lost" implies "result" rather
            //than needing it alongside — the same rule "level=" implies "play" by. Written here and not left to
            //the caller because the first thing `lost` did on its own was put the main menu up and say nothing.
            _startupResult = result || lost;
            _startupBlockDone = blockDone;
            _startupLost = lost;
            _shotSchedule = shotSeconds;
            if (mute) _masterVolume = 0f;

            //The tier owns supersampling, so the tier's factor is taken first and an explicit ssaa= then
            //overrides that one entry of it — the expert override the benchmark and the screenshot harness use.
            //The rest of the tier is applied in LoadContent, once the city it also sizes exists.
            if (quality.HasValue) _quality = quality.Value;
            _supersampleFactor = QualityPreset.Presets[(int)_quality].SupersampleFactor;

            //Kept as well as applied: the tier is applied again in LoadContent (and again on every adaptive
            //step), and each of those would otherwise put the tier's factor back over this one.
            if (supersampleFactor.HasValue)
            {
                _supersampleOverride = Math.Clamp(supersampleFactor.Value, 1, 4);
                _supersampleFactor = _supersampleOverride.Value;
            }

            _startupScene = scene;
            _startupSkyDome = skyDome;
            _logFrameRate = logFrameRate;

            //Either one is the player's decision and settles the question for good; with neither, the adaptive
            //path is free to measure this machine and step the tier down. The distinction matters on a fullscreen
            //switch: a player-pinned tier stays put, a probe-reached one is only this machine's answer for this
            //back-buffer size and gets re-measured (see ToggleFullscreen).
            _qualityPinnedByPlayer = supersampleFactor.HasValue || quality.HasValue;
            _qualitySettled = _qualityPinnedByPlayer;
            _exposure = exposure > 0f ? exposure : DEFAULT_EXPOSURE;
            _uncappedFps = uncappedFps;

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreparingDeviceSettings += Graphics_PreparingDeviceSettings;

            Content.RootDirectory = "Content";

            Window.AllowUserResizing = true;
            Window.Title = GAME_TITLE;
            Window.ClientSizeChanged += (_, _) => OnClientSizeChanged();

            SetGraphics();
        }

        private void Graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = _uncappedFps ? PresentInterval.Immediate : PresentInterval.One;
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;

            //Nothing but one already-resolved full-screen quad ever reaches the back buffer, so multisampling
            //it would cost memory and antialias nothing.
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 0;
        }

        private void SetGraphics()
        {
            //The adapter's mode, not the device's: this is called from the constructor as well, before the
            //GraphicsDeviceManager has made a device at all — so reading GraphicsDevice there fell through to
            //the windowed default and `BS3D.exe fullscreen` mode-switched the display down to 1600×900
            //instead of filling it. GraphicsAdapter is valid with no device.
            DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

            //The probe's frame-rate floor follows the refresh of the monitor the window is on, so a 75 Hz panel
            //asks for ~75 and a 60 Hz one ~60 rather than the same fixed floor for both. Re-derived on every
            //fullscreen switch, and the constructor call resolves properly too — MonoGame has built the window
            //by the time this runs, measured (see TryGetWindowDisplayRefresh).
            SetQualityMinFpsFromRefresh();

            //Windowed, the size is the PLAYER'S: they can drag the window's edge or maximize it from the title
            //bar, and neither of those is _fullscreen — that flag is F11's own switch and nothing else. Stating
            //the windowed default here on every pass instead meant any caller of this method (found on the
            //FPS-limit toggle, #137) shrank the swap chain back to 1600×900 underneath a window the OS does not
            //shrink with it: the maximized window stayed, the resolution inside it dropped, and moving the
            //window — which resyncs the swap chain to the client area — was what appeared to "fix" it.
            _graphics.PreferredBackBufferWidth = _fullscreen ? display.Width : _windowedSize.X;
            _graphics.PreferredBackBufferHeight = _fullscreen ? display.Height : _windowedSize.Y;

            //Borderless fullscreen, not a DXGI mode switch (#157): minimizing exclusive fullscreen tears
            //down the swap chain's fullscreen state, and with no Activated/Deactivated handling anywhere
            //the window never came back. The back buffer is the display's size above either way, so
            //borderless shows the identical picture — there is just no display mode to lose.
            _graphics.HardwareModeSwitch = false;
            _graphics.IsFullScreen = _fullscreen;
            _graphics.SynchronizeWithVerticalRetrace = !_uncappedFps;

            _graphics.ApplyChanges();

            //Null-conditional for the constructor's call, which runs before LoadContent has built the
            //pipeline (the old in-class EnsureSceneTarget guarded on GraphicsDevice == null the same way)
            _pipeline?.EnsureTarget();

            //A fullscreen switch resizes the back buffer without necessarily going through the window's own
            //resize event, and both the overlay's scale and the camera's framing are derived from the viewport
            _info?.RecomputeScale();
            UpdateCameraAspect();

            //The cursor is captured for aiming only while a game is actually being played; everywhere else it
            //is the pointer the player clicks with. This runs from the constructor too, before any screen
            //exists, where an empty stack correctly reads as "not playing".
            IsMouseVisible = _screens.Active is not GameplayScreen;
            IsFixedTimeStep = false;
        }

        private void OnClientSizeChanged()
        {
            //Where the size SetGraphics restores comes from (#137): whatever the player last left the window
            //at, maximized or dragged to any size at all. Only while windowed — the pass that goes fullscreen
            //raises this event at the display's size, and recording that would bring the game back OUT of
            //fullscreen into a window the size of the screen. The zero guard is the minimize, which raises it
            //too and would otherwise leave a zero-sized back buffer waiting for the next settings toggle.
            if (!_fullscreen && Window.ClientBounds.Width > 0 && Window.ClientBounds.Height > 0)
                _windowedSize = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);

            UpdateCameraAspect();

            //A window that changed size may also have changed monitor, and the probe's floor is derived from the
            //refresh of the one it is on (#81). This is the cheap hook rather than the complete one: a window
            //DRAGGED between two monitors of the same size raises no resize, so its floor stays on the old
            //panel's rate until the next resize or fullscreen switch. Tracking the move itself would want a
            //WM_DISPLAYCHANGE/position hook into the WinForms host, which is a lot of surface for a case that
            //corrects itself the moment anything else about the window changes.
            SetQualityMinFpsFromRefresh();

            //The overlay is authored for 2160p and scaled to the viewport, so a resize has to re-derive it.
            //The menu is authored the same way, and refits itself in Draw (EnsureMenuLayout).
            _info?.RecomputeScale();

            _pipeline?.EnsureTarget();
        }

        /// <summary>
        /// Re-derives the camera's aspect and hands the change to the session, which owns the framing: the
        /// fit is checked on <b>both</b> frustum axes, and only the vertical one is aspect-independent — a
        /// narrow window or a tall one flips which binds — so a resize has to re-solve the stand-off and not
        /// just the projection. The session also drops its mouse-aim baseline there, since the viewport's
        /// centre just moved (see <see cref="GameplayScreen.OnViewportChanged"/>).
        /// </summary>
        private void UpdateCameraAspect()
        {
            if (_camera == null) return;

            _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            _gameplayScreen?.OnViewportChanged();
        }

        protected override void Initialize()
        {
            _camera = new RecoilCamera
            {
                AspectRatio = GraphicsDevice.Viewport.AspectRatio,
                FieldOfView = GameplayScreen.GAME_FOV
            };

            //Every dial of the kick scaled by the one factor: the shape of the response is kept — the
            //directional throw, the rattle, the roll that is what reads as "the camera was hit" — and only
            //its ceiling comes down, now that the gun itself takes most of the shot's force.
            CameraShake shake = _camera.Shake;
            shake.MaxPitch *= CAMERA_SHAKE_SCALE;
            shake.MaxYaw *= CAMERA_SHAKE_SCALE;
            shake.MaxRoll *= CAMERA_SHAKE_SCALE;
            shake.MaxOffset *= CAMERA_SHAKE_SCALE;
            shake.MaxRecoilBack *= CAMERA_SHAKE_SCALE;
            shake.MaxRecoilPitch *= CAMERA_SHAKE_SCALE;
            shake.MaxFovPunch *= CAMERA_SHAKE_SCALE;

            //Added before base.Initialize, which is what initializes the component and loads its font
            _info = new InfoRenderer(this, "Content/Fonts/segoeui") { DrawOrder = int.MaxValue };
            Components.Add(_info);

            base.Initialize();

            //After base.Initialize, so the window's handle exists and MonoGame's own icon assignment has
            //already been published — this has to win that race, not lose it.
            WindowIcon.Apply(Window.Handle);
        }

        protected override void LoadContent()
        {
            _instancingEffect = Content.Load<Effect>("Shaders/InstancedModel");

            //The pipeline caches its parameters and sets each look value exactly once through the required
            //initializer — the aberration/grain toggles and the exposure ladder write the same properties
            //later, and everything else about the resolve lives in the class (see #74)
            _pipeline = new PostProcessPipeline(GraphicsDevice,
                Content.Load<Effect>("Shaders/Tonemap"), Content.Load<Effect>("Shaders/Glare"))
            {
                GlareThreshold = GLARE_THRESHOLD,
                GlareIntensity = GLARE_INTENSITY,
                Exposure = _exposure,
                ChromaticAberration = _aberration ? CHROMATIC_ABERRATION : 0f,
                FilmGrain = _grain ? FILM_GRAIN : 0f,
                SupersampleFactor = _supersampleFactor,
            };

            //Off the shared instanced effect, because one push of the scene's lights has to reach the balls, the
            //island, the gun and the city alike. It caches its four parameter references here, for the same
            //per-frame reason it always did: the campfire flickers, so this is not a set-once.
            _sceneLights = new SceneLights(_instancingEffect);

            //Nothing is built here — the plate's footprint is the loaded field's, so the first Fit is a level's
            _ceilingPlate = new CeilingPlate(GraphicsDevice, _instancingEffect);

            //The menu's own glass over the backdrop's preview map (#249) — a second plate, so the front end
            //never refits the session's: a kept session continues drawing the one its level fitted.
            _menuCeilingPlate = new CeilingPlate(GraphicsDevice, _instancingEffect);

            //The overlay's own batch, shared by the HUD and the crosshair the session draws into it
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            //And the texel the host's scrim quad is stretched from (#114) — see the scrim block in Draw
            _scrimTexel = new Texture2D(GraphicsDevice, 1, 1);
            _scrimTexel.SetData(new[] { Color.White });

            #region Balls

            //Off the same shared instanced effect as everything else in the scene, which is why it is handed in
            //rather than loaded: the effect stays the content manager's. ripples: true is this executable alone
            //— the Testbed and the map editor run no wave through their cluster — and it both switches the
            //shader's ripple term on and picks the shallower resting breath that keeps the wave visible over
            //it. The ground the balls' bellies darken against is the island's own top, which is the set's
            //default and the one thing every ball in this game hangs over.
            _balls = new BallRenderSet(GraphicsDevice, _instancingEffect, ripples: true);

            #endregion

            #region The gun

            //The bore is cut to the loaded queue: the rig derives the tube's length, its two lips and the
            //pivot-to-front-ball distance from the queue's size and spacing, which are Magazine's own figures,
            //so the barrel that is built and the muzzle a shot leaves from cannot disagree. The instancing
            //effect is handed in and stays the content manager's — the rig disposes its mesh and renderer only.
            _cannonRig = new CannonRig(GraphicsDevice, _instancingEffect, Magazine.SIZE, Magazine.SPACING);

            #endregion

            //The nine self-lit backdrops, shared with the Testbed and the map editor — one copy of every
            //scene shader, built out of the Testbed's content directory. The hole radius is fixed (the island
            //never moves or resizes here), so it is set once rather than per frame.
            _sceneRenderer = new SceneRenderer(GraphicsDevice, Content)
            {
                TerrainHoleRadius = ArenaIsland.TERRAIN_HOLE_RADIUS,
                SupersampleFactor = _supersampleFactor,

                //Seeded here as well as written by ApplyQuality, for the same reason the factor beside it is:
                //the tier is applied once in LoadContent BEFORE this exists (a command-line quality= reaches it
                //long before there is a renderer to write onto), so a startup at anything but High would
                //otherwise draw the full-price floor until the next tier change — which on a pinned tier never
                //comes.
                SceneDetail = _quality == QualityLevel.High ? 1f : 0f
            };

            //After the scene renderer, which the rig consults for the scenes that state their own lighting. The
            //cloud hook is captured ONCE here rather than per frame: a method group written at the call site
            //builds a fresh delegate every time it is evaluated, and this one used to be evaluated in Draw.
            _rig = new SkyLightRig(_sceneRenderer) { CloudHook = _clouds.ApplyTo };

            BuildScene();

            //Note the simulation, the ceiling body and the cluster are NOT built here: they are the expensive
            //part of starting up and belong to a play session, so the GameplayScreen builds them on the first
            //"Play" and rebuilds them per level. Everything above is the scene, which the menu also stands in.

            _skyEffect = Content.Load<Effect>("Shaders/Sky");
            _skyCameraPositionParam = _skyEffect.Parameters["CameraPosition"];
            _sky = new SkyDome(GraphicsDevice, _skyDome, linearVertexColors: true)
            {
                Effect = _skyEffect
            };

            //Everything about the clouds that does not change frame to frame, pushed once. The per-frame half —
            //the clock and the camera — goes out in BeginSceneDraw, right before the dome; the colours and the
            //sun's own direction follow the dome and are ApplySkyLighting's business.
            _clouds.ApplyStaticParameters(_skyEffect);

            //The game's name as 3D lettering over the front end (#248). Its twenty-two meshes are built here,
            //once — eleven letters at the stroke weight and eleven again fatter, for the keyline behind them.
            //
            //BEFORE THE SetScene BELOW, AND THAT IS NOT TIDINESS: SetScene ends in ApplySkyLighting, which is
            //what first hands the dome's light rig to every enrolled renderer, and the wordmark's are enrolled
            //(SkyLitRenderers). Built after it, the letters would stand under the library's default rig until
            //the next scene or dome change — the very fault the ceiling glass had, and the island's own comment
            //records the same ordering for the same reason. The inset is the front end's own figure converted
            //to a fraction of the frame's height; MainMenuPage.FRONT_INSET is the one copy of it.
            _titleWordmark = new TitleWordmark(GraphicsDevice, _instancingEffect, GAME_TITLE,
                SCENE_AMBIENT_INTENSITY, Screens.MainMenuPage.FRONT_INSET / (float)MENU_DESIGN_HEIGHT);

            //The trophy cups (#183), for the identical reason and at the identical point: since #232's second
            //half they reflect the dome the way the cannon does (TrophyPodium.Renderers, enrolled below), so
            //they have to exist before the SetScene two lines down or stand under the library's default rig
            //until the next scene or dome change. Both meshes are built once here — a few thousand triangles
            //apiece — so a cleared level costs one draw call and nothing else; it borrows the scene's own
            //instancing effect and the flat ambient constant every renderer shares.
            _trophy = new TrophyPodium(GraphicsDevice, _instancingEffect, SCENE_AMBIENT_INTENSITY);

            //A different one of the twelve every launch, so the front end is not the same picture twice — unless
            //the command line pinned one. It also sets the dome and the city's lighting, and ends in
            //ApplySkyLighting, which is why nothing derives the light rig before this point.
            SetScene(_startupScene ?? (SceneKind)RANDOM.Next(SceneRenderer.SceneCount));

            //After SetScene and never before: the sea and the savanna each replace the dome with one of their
            //own, so an explicit sky= would be silently overridden the other way round. The Testbed's rule.
            if (_startupSkyDome.HasValue) SetSkyDome(_startupSkyDome.Value);

            //The rest of the tier, now that the city it also sizes exists. The constructor could only take the
            //supersample factor, since the target and the city are both built here. At the default High this
            //writes the config's own defaults back over themselves and rebuilds nothing.
            ApplyQuality(_quality);

            _pipeline.EnsureTarget();

            //The order the levels are played in, read once here: a broken set is reported at startup rather
            //than at the moment the player presses Play, and the maps themselves are only parsed per level.
            LoadLevelSet();

            //The building count only belongs on this line for the scenes that actually draw the city; for the
            //other nine it is just how many sit in memory, and printing it beside an unrelated scene name reads
            //as though that scene has buildings in it (the draw call gates on the same check: BS3DGame.Scene.cs).
            string city = (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
                ? $"{_city.Buildings.Length} buildings, "
                : string.Empty;
            Console.WriteLine($"[game] {city}scene {_scene}, dome {_skyDome}");

            //The two non-page screens. The gameplay screen loads its own content (the shot-trail effect), so
            //it is made here with the device up; the backdrop is the scene-only frame the menus stand over.
            _backdrop = new BackdropScreen(this);
            _gameplayScreen = new GameplayScreen(this);

            //The SFX are synthesized from raw PCM here, once, so the per-event paths only ever play a buffer —
            //no asset files, no pipeline step.
            _audio = new ProceduralAudio();

            //The victory display. Its one static buffer is built here too, so a cleared level costs nothing
            //but a handful of uniforms.
            _fireworks = new Fireworks(GraphicsDevice, Content.Load<Effect>("Shaders/Fireworks"), _audio);

            //And the campaign's confetti, whose one static buffer is built here for the same reason (#215).
            _confetti = new Confetti(GraphicsDevice, Content.Load<Effect>("Shaders/Confetti"));

            //Testing only, and deliberately long: it has to outlast a scripted screenshot burst.
            if (_startupCelebrate) _fireworks.Celebrate(90f);

            //"confetti" asks for the campaign's ending on the front end. Clearing the last level of the set is
            //the only thing that normally starts it, and that cannot be scripted at all — it is `celebrate`'s
            //reasoning one step further along again, past even `blockdone`: a block milestone needs five levels
            //played, where this needs the whole campaign.
            if (_startupConfetti) _confetti.Celebrate(90f);

            //The level theme. The constructor only starts the synthesis — two minutes of PCM is a couple of
            //seconds of arithmetic, and it runs on a background thread while the player is still looking at
            //the splash and the menu (see ProceduralMusic).
            _music = new ProceduralMusic();

            //The scene beds. The scene was picked before the audio existed (SetScene runs early in
            //LoadContent, and its _ambience hook is null-conditional for exactly that), so the pick is
            //handed over here; every later change reaches it through SetScene like everything else scenic.
            _ambience = new ProceduralAmbience();
            _ambience.SetScene(_scene);

            //A muted start (the mute argument) has to reach the freshly made subsystems; every later change
            //comes through the settings rows.
            ApplyVolumes();

            BuildMenu();
        }

        private static readonly Random RANDOM = new();

        /// <summary>
        /// Replays the current entry by tearing the session down and building the same level again, so the
        /// score, the multiplier, the budget and the cluster all start over. <see cref="GameplayScreen.BuildLevel"/>
        /// is the real reload the result screen offers as Retry.
        /// </summary>
        internal void RetryLevel()
        {
            _gameplayScreen.BuildLevel(_gameplayScreen.LevelIndex);
            EnterPlaying();
        }

        /// <summary>
        /// Builds the next entry of the set and drops straight into it. Only ever called from the "Next Level"
        /// button, which is itself only shown when a next entry exists — see <see cref="ResultPage"/>.
        /// </summary>
        internal void AdvanceLevel()
        {
            _gameplayScreen.BuildLevel(_gameplayScreen.LevelIndex + 1);
            EnterPlaying();
        }

        /// <summary>
        /// Puts the result screen over the level that has just ended, in the same state a pause uses <b>but
        /// two</b>: the session goes on being drawn underneath (the page draws what is under it) and Myra owns
        /// the input, but the frame behind is <i>not</i> dimmed and the session is <i>not</i> stopped — an
        /// ending is worth watching where a pause is not, so the arena stays lit
        /// (see <see cref="ResultPage.DimsFrame"/>) and stays alive (#241, and
        /// <see cref="ResultPage.UpdatesUnderlying"/>). Called by the session when a level ends, and by the
        /// <c>result</c> test argument.
        /// </summary>
        internal void PresentResult(LevelResult result)
        {
            //The figures arrive as a SNAPSHOT taken at the level's end, not read by the screen when it draws —
            //see LevelResult for why that arithmetic has to be frozen.
            _resultPage.Take(result);

            _screens.Push(_resultPage);
        }

        #region Levels

        /// <summary>
        /// Reads the level set — the file that says which level is first and which is second — from the
        /// <c>Levels</c> directory beside the executable. Run once at load, so a broken set is reported before
        /// the player ever presses Play rather than at the moment they do.
        /// <para>
        /// A missing or broken set is <b>not</b> fatal: <see cref="_levelSet"/> stays null and the session
        /// falls back to the procedural pyramid the game shipped with, so the thing still plays. That is
        /// deliberate — the levels are loose data files beside the binary precisely so they can be edited, and
        /// a typo in one of them should cost a level, not the game.
        /// </para>
        /// </summary>
        private void LoadLevelSet()
        {
            //AppContext.BaseDirectory, not the working directory: the game is launched from a shortcut, from
            //a shell somewhere else and from the debugger, and only the first of those has them agree
            string path = Path.Combine(AppContext.BaseDirectory, LEVELS_DIRECTORY, LevelSet.DefaultFileName);

            try
            {
                _levelSet = LevelSet.Load(path);

                Console.WriteLine($"[levels] '{_levelSet.Name ?? LevelSet.DefaultFileName}': {_levelSet.Count} level(s)");

                //The player's bests, from beside the set — which is also why it is in this try and not its
                //own: with no set there is no campaign to have progressed through. Lenient where the set's
                //loader throws (a first run has no save, and a corrupt one must cost stars, never the game).
                _progress = PlayerProgress.Load(Path.Combine(_levelSet.Directory, PlayerProgress.DefaultFileName));

                Console.WriteLine($"[progress] {_progress.TotalStars} star(s), best total {_progress.TotalScore}"
                    + $" over {_progress.Levels.Count} cleared level(s)");

                //Every entry's rules, at load rather than as each level comes up: an inconsistently authored
                //set is then obvious in one place before a single level is played.
                for (int i = 0; i < _levelSet.Count; i++)
                    Console.WriteLine($"[levels]   {i + 1}. '{_levelSet.DisplayName(i)}' — {_levelSet.DescribeRules(i)}"
                        + (LevelMinStars(i) > 0 ? $", unlocks at {LevelMinStars(i)} star(s)" : ""));
            }
            catch (Exception e)
            {
                //Anything at all: no file, no directory, malformed JSON, a set that lists nothing. The game
                //has a level of its own to fall back on, so this is a log line and not a crash.
                Console.WriteLine($"[levels] No level set at '{path}' ({e.Message}); using the built-in level");
                _levelSet = null;
            }
        }

        /// <summary>
        /// Records a cleared level's score and stars into the player's progress and writes it to disk, there
        /// and then — the game has no other save point, and progress lost to a crash later would be progress
        /// the player watched themselves earn. Each best only ever rises (<see cref="PlayerProgress.Record"/>).
        /// </summary>
        /// <returns>Whether anything improved — what the result screen's "new best" line reads.</returns>
        internal bool RecordLevelResult(int index, int score, int stars)
        {
            if (_progress == null || _levelSet == null || index < 0 || index >= _levelSet.Count) return false;

            bool improved = _progress.Record(_levelSet.Levels[index].File, score, stars);

            if (improved) SaveProgress();

            return improved;
        }

        /// <summary>
        /// Back to zero stars and no bests — the settings row's action (#92: useful for testing as much as
        /// for a player who wants a fresh start). The locks in the picker follow the totals, so levels close
        /// again with it; the session being played is left alone.
        /// </summary>
        internal void ResetProgress()
        {
            if (_progress == null) return;

            _progress.Reset();
            SaveProgress();

            Console.WriteLine("[progress] Reset to zero");
        }

        /// <summary>
        /// One writer for the save, so the failure handling is in one place: an unwritable file costs the
        /// write and is said in the log, never the session — the progress object stands and the next record
        /// tries again.
        /// </summary>
        private void SaveProgress()
        {
            try
            {
                _progress.Save();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[progress] Could not save '{_progress.Path}': {e.Message}");
            }
        }

        #endregion

        #region The session on the stack (menu ⇄ play)

        /// <summary>
        /// Starts playing. The world — the level's map, the simulation, the ceiling body, the drain's
        /// collision mesh and the cluster — is built by the session screen rather than at load, so the menu
        /// comes up without paying for it and a machine that never presses Play never pays at all.
        /// </summary>
        /// <param name="newGame">
        /// True to throw away a session in progress and deal the first level again; false to carry on with
        /// whatever is already standing (and to build it, if this is the first time).
        /// </param>
        private void StartGame(bool newGame)
        {
            if (newGame || !_gameplayScreen.IsBuilt) _gameplayScreen.BuildLevel(0);

            EnterPlaying();
        }

        /// <summary>
        /// Starts the level the player picked. Always a fresh build, even if that entry is the one already
        /// standing: choosing a level off the picker means starting it, not resuming a half-played attempt at
        /// it — resuming is what Continue is for.
        /// </summary>
        internal void StartGameAt(int index)
        {
            _gameplayScreen.BuildLevel(index);
            EnterPlaying();
        }

        /// <summary>
        /// Which entry the <c>level=</c> argument names: a <b>1-based place</b> in the set, as the title bar
        /// numbers it and the picker lists it, or a <b>name</b> — either the entry's own or its file's, matched
        /// case-insensitively. Out of range or unrecognised falls back to the first level and says so on a
        /// <c>[levels]</c> line, the same leniency the rest of the command line takes: a diagnostic must never
        /// be the reason a scripted run fails to start.
        /// </summary>
        /// <summary>
        /// Which entry the front end's backdrop should hang, or <c>null</c> for the roll a player gets — the
        /// <c>preview=</c> argument, resolved the way <see cref="ResolveStartupLevel"/> resolves
        /// <c>level=</c>. Asked on every roll rather than resolved once, which costs a walk of the set on an
        /// event that happens when a player returns to the menu and is what keeps this a one-liner.
        /// </summary>
        /// <summary>
        /// What every ball is to be drawn as whatever its level says — the <c>balls=</c> argument (#258) — or
        /// <c>null</c> for each map in the material it is authored in, which is what a player gets. The front
        /// end's preview and a played session both consult it, so one run photographs both places in one style.
        /// <para>
        /// A testing lever and not a setting, deliberately, and the distinction is the same one <c>scene=</c>
        /// makes: the style is a property of the MAP, chosen by whoever built it, and a player who could
        /// override it globally would be overriding the author. What it is for is the only way to judge the two
        /// looks honestly — the same cluster, the same stand-off, the same dome, once in each material.
        /// </para>
        /// </summary>
        internal BallStyle? BallStyleOverride { get; }

        internal int? PinnedPreviewLevel =>
            _startupPreview == null ? null : ResolveStartupLevel(_startupPreview);

        private int ResolveStartupLevel(string level)
        {
            int count = LevelCount;

            if (int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out int place))
            {
                if (place >= 1 && place <= count) return place - 1;
            }
            else
            {
                for (int i = 0; i < count; i++)
                    if (string.Equals(_levelSet.DisplayName(i), level, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Path.GetFileNameWithoutExtension(_levelSet.Levels[i].File), level, StringComparison.OrdinalIgnoreCase))
                        return i;
            }

            Console.WriteLine($"[levels] No level '{level}' in the set of {count}; starting the first instead");
            return 0;
        }

        /// <summary>
        /// Puts the session on the stack, over the standing backdrop and with nothing above it — whatever the
        /// player navigated through to get here is popped on the way. The input hygiene a (re)entry needs —
        /// the cleared mouse-aim baseline, the re-polled pad — lives on the screen itself
        /// (<see cref="GameplayScreen.CoveredChanged"/>), which the manager raises on whichever screen ends up
        /// on top, so a fresh push and the pop of a pause arrive through the one path.
        /// </summary>
        private void EnterPlaying()
        {
            _screens.PopTo<BackdropScreen>();
            _screens.Push(_gameplayScreen);

            //And the title bar names the level. Here rather than in BuildLevel because this — not the build —
            //is what "a level is being played" means: every entry into play comes through it (a first Play, a
            //pick off the list, Continue, Retry, Next Level), and only ReturnToMainMenu undoes it.
            ShowLevelInTitle(_gameplayScreen.LevelIndex);
        }

        /// <summary>Puts the pause menu over the frame the player was looking at; the stack freezes the game under it.</summary>
        internal void PauseGame() => _screens.Push(_pausePage);

        /// <summary>
        /// Back into the game: the pause pops off and the gameplay screen is the top again — its
        /// <see cref="GameplayScreen.CoveredChanged"/> re-captures the cursor and re-baselines the input.
        /// </summary>
        internal void ResumeGame() => _screens.Pop();

        /// <summary>
        /// Back to the front end. The session is <b>kept</b>, not discarded — the gameplay screen leaves the
        /// stack but stands, so the main menu offers to resume it and to start a new game, which is the
        /// difference between a mis-click costing a click and costing a game. The camera is the backdrop's
        /// again from here: its orbit takes over the moment it updates.
        /// </summary>
        internal void ReturnToMainMenu()
        {
            //Down to the backdrop rather than a pop: the player may be anywhere — the pause, a settings panel
            //over it, the result screen — and the front end sits directly over the backdrop, not on top of
            //wherever they wandered.
            _screens.PopTo<BackdropScreen>();
            _screens.Push(_mainMenuPage);

            //A fresh promise on every return: the backdrop rolls another random map, so the front end never
            //hangs the map just played back over the player as if it were the next one (#249).
            _backdrop.RollPreviewMap();

            //The title bar stops naming a level, even though the session is kept: it names what the window is
            //SHOWING, and this window is showing the front end. The kept session is offered as Continue, which
            //comes back through EnterPlaying and puts the name back up.
            ShowLevelInTitle(-1);
        }

        #endregion

        protected override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _wallClock += elapsed;

            //The ears, before anything can make a noise this frame. Up here and unconditional for the same
            //reason the fireworks are: sound is made with no session standing — a whole celebration of it over
            //a frozen gameplay screen, and the menu's own clicks over an orbiting backdrop — so the listener
            //cannot belong to the session either. There is one camera for the whole process, so there is one
            //listener, valid across every level rebuild.
            //
            //It is posed from the pose the previous frame was DRAWN from (the stack poses the camera further
            //down), which is exactly the staleness the panning it replaces already had: half a unit at sixty
            //frames a second. Nothing is gained by moving it, and correctness would be lost.
            _audio?.UpdateListener(_camera);

            //Advanced here, with the wall clock and above the stack, because the celebration outlives the
            //screen that started it: clearing a level pushes the result page over the gameplay screen, whose
            //own covered frame runs the world and nothing else (#241), and Main Menu tears the session down
            //while the display is still going. Driven off the frame's own elapsed time rather than a play
            //clock, so it does not stop when the game does.
            _fireworks?.Update(elapsed);
            _confetti?.Update(elapsed);
            _trophy?.Update(elapsed);

            //The music's handover: a pass is played once rather than looped, and this is what puts the next
            //freshly synthesized variation on when the current one ends (see ProceduralMusic.Update). Up here
            //with the fireworks and for the same reason — it has to keep running whatever is on the stack.
            //It takes the frame's own time since #211: the switches' fades move on it.
            _music?.Update(elapsed);

            //The scene's bed and its crossfade, on the wall clock's frame like the clouds: the scene is on
            //screen whether or not a session stands, so its sound is too, pause included.
            _ambience?.Update(elapsed);

            //Which music the moment wants is the stack question (#46): the front end's loop plays exactly
            //while no session screen is on it. The theme's own lifecycle stays the session's — BuildLevel
            //starts it, TearDown and the level's endings stop it — this only closes the one gap that had no
            //owner: leaving to the main menu keeps the session but must not keep its music ("it plays while a
            //level is being played", docs/game-feedback.md), and Continue re-wants the theme because it comes
            //back WITHOUT a BuildLevel. A fresh build's own Play a moment later is the "already sounding"
            //no-op, so the two writers cannot fight. Both directions are a REPLACEMENT, so both take the
            //fading stop (#211): the theme leaves under the loop's held pads, the loop under the theme's
            //prelude — the level endings' dead stop stays the endings' own, where the silence is the message.
            if (_music != null)
            {
                bool onFrontEnd = !_screens.Contains<GameplayScreen>();

                if (onFrontEnd != _menuMusicOn)
                {
                    _menuMusicOn = onFrontEnd;

                    if (onFrontEnd)
                    {
                        _music.FadeOut();
                        _music.PlayMenu();
                    }
                    else
                    {
                        _music.StopMenu();
                        if (_gameplayScreen != null && _gameplayScreen.IsBuilt) _music.Play();
                    }
                }
            }

            //The fireworks give way to the fanfare. Both arrive on the frame a level ends, and a report is
            //broadband and loud enough to bury a tune under it — the bang is an event, the fanfare is the
            //point. Read per frame rather than latched, so the ducking lifts by itself when the piece ends.
            if (_audio != null && _music != null)
                _audio.FireworkDuck = _music.IsFanfarePlaying ? ProceduralAudio.FIREWORK_DUCKED : 1f;

            //The very click that refocuses a windowed game would otherwise read as a fresh press against a
            //stale "released" state and fire an unintended shot, since input is not sampled while inactive.
            EdgeInputAllowed = IsActive && _wasActive;

            //The whole frame is the stack's now: pending pushes and pops are applied, then the update walks
            //top-down until a screen freezes what is under it — which is how a pause stops the game and how
            //the front end keeps the backdrop turning under itself. Myra runs its click handlers in Draw, so
            //a page opened by a click lands here on the following frame, which is the deferred design.
            _screens.Update(gameTime);

            //Testing only (the play argument): jump into the first level through the very pop-and-push a
            //player's click takes. After the stack update above, so BuildMenu's queued pushes have been
            //applied and PopTo<BackdropScreen> sees the backdrop it pops to — the splash is drawn for the
            //one frame this costs, exactly as a very fast click would leave it. At the end of LoadContent
            //those pushes were still pending, the PopTo tested an empty live stack and silently skipped,
            //and the splash stayed buried under the session for the rest of the run.
            if (_startupPlay)
            {
                _startupPlay = false;

                if (_startupLevel == null) StartGame(newGame: true);
                else StartGameAt(ResolveStartupLevel(_startupLevel));
            }

            //And the same for the result screen, over whatever is on the stack — the front end, unless "play"
            //above has just put a level under it. The figures are a plausible clear rather than zeros: the page
            //lays out its breakdown from them, and a screen of dashes would not be the screen being looked at.
            //
            //Held back until the TITLE CARD has gone, which is not a nicety: the splash hands over with a
            //Replace (it is the only page over the backdrop at boot, so it has to take its own place), and a
            //Replace pops whatever is on top — so a result page pushed at boot was silently swallowed by the
            //main menu arriving 2.6 s later. Measured that way round, which is how it is known.
            if (_startupResult && !_screens.Contains<SplashPage>())
            {
                _startupResult = false;

                //The rating is 3 unless "stars=" said otherwise (#183). It exists because the trophy cup is a
                //function of the rating and there are four of them: three is the only one a test could reach,
                //so the other three cups could be neither photographed nor compared against each other. Same
                //reasoning as "blockdone" — a state a real play-through reaches only by being GOOD at the game.
                int testStars = Math.Clamp(_startupResultStars ?? 3, 0, Prazsky.BS3D.Scoring.StarRating.MAX);

                //"lost" flips it to a FAILED page (#238). The text is the real one GameplayScreen.Rules would
                //produce for the cluster reaching the line rather than a placeholder, because the whole reason
                //this exists is to look at the line the player is actually shown; a stand-in of a different
                //length would be a different layout. On a fail the page shows no stars and no breakdown, so the
                //figures below simply go unread — and newBest is refused outright, a lost level having no best.
                bool lost = _startupLost;

                PresentResult(new LevelResult(cleared: !lost,
                    failureText: lost ? "The cluster reached the line." : null,
                    stars: lost ? 0 : testStars, newBest: !lost,
                    hasNextLevel: true, nextLevelUnlocked: true, nextLevelMinStars: 1, totalStars: testStars,
                    campaignComplete: false,
                    //"blockdone" borrows the third block's own name, so the milestone is looked at with a real
                    //chapter's title in it rather than a placeholder that would read as one
                    blockComplete: _startupBlockDone,
                    blockName: _startupBlockDone ? (LevelBlockName(12) ?? "The Tower") : null,
                    blockNumber: 3, blockCount: Math.Max(3, BlockCount),
                    score: 4820, matchedBalls: 96, orphanedBalls: 24, streakBonus: 640,
                    hadBudget: true, unusedShotsAwarded: 7, completionBonusAwarded: 350));
            }

            //A page that has just arrived must not be handed a mouse button that was already held down when it
            //did. Myra keeps its own previous-state and is only fed input while a menu page is on top, so the
            //first frame a page is drawn it compares a held button against whatever the state was the last time
            //a menu had input — frames or minutes ago — and reads a press that never happened here. Pausing with
            //the fire button held does it: Escape pushes the pause page and the still-held click lands on
            //whichever entry is under the cursor. So the button has to be seen RELEASED once before Myra is fed
            //anything, which is the same rule _padTriggerReleased applies to a held trigger.
            //
            //Polled only while a menu is up, which is exactly when nothing else polls the mouse — the gameplay
            //screen takes its own snapshot in UpdateAim, and two reads of one device in a frame is the thing
            //BestPractices.md #5 forbids.
            if (_screens.Active != _lastActiveScreen)
            {
                _lastActiveScreen = _screens.Active;
                _menuClickArmed = false;
            }

            if (!_menuClickArmed && _screens.Active is MenuPage && Mouse.GetState().LeftButton == ButtonState.Released)
                _menuClickArmed = true;

            _wasActive = IsActive;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            //Bottom-up down the stack to the lowest uncovered screen: the backdrop (or the gameplay screen)
            //runs the whole pipeline — the HDR target, the setting, its own 3D, the resolve — and the pages
            //above it draw nothing themselves, because their picture is the Myra desktop below.
            _screens.Draw(gameTime);

            //The FPS overlay: a component, in display space, after the resolve
            base.Draw(gameTime);

            //The Myra GUI renders last, on top of everything, straight to the back buffer (base.Draw and the
            //resolve leave it bound) — and only while a menu page is the active screen: Render also processes
            //Myra's own mouse and keyboard input, which may only happen where the game's own input has stood
            //down. There is one desktop and it holds the top page's tree, so nothing stale can be drawn.
            //
            //While the window is not focused it is laid out and drawn but NOT fed input: Mouse.GetState
            //reports the button and a window-relative position whether the window has focus or not, so a
            //click meant for another application that happens to land over where a menu entry is would
            //otherwise press it — and "Quit" would close a game nobody was looking at.
            if (_desktop != null && _screens.Active is MenuPage page)
            {
                //The scrim under a page that dims a stopped game, drawn by the HOST's own batch rather than
                //as the Myra root's background (#114). Myra's paint stops short of the viewport's bottom edge
                //— its root lays out at the full height (measured: root 1600×900) and yet an identical plain
                //SpriteBatch quad reaches row 899 where the Myra-drawn background stops at 897, two rows at a
                //900 client and one at 700 — which left a thin strip of undimmed arena across the bottom of
                //every dimmed page. Drawing the scrim here keeps everything else as it was: the stack still
                //decides (DimsFrame), the desktop still puts the page's widgets over it, and the FPS overlay
                //(base.Draw, above) stays under it exactly as before.
                if (page.DimsFrame)
                {
                    OverlayBatch.Begin();
                    OverlayBatch.Draw(_scrimTexel,
                        new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
                        PAUSE_SCRIM);
                    OverlayBatch.End();
                }

                //Refitted here rather than off the resize event, so the one place it can be out of date is
                //the frame that is about to draw it — a fullscreen switch and a window drag both land here
                EnsureMenuLayout();

                //_menuClickArmed is the second reason to draw a page without feeding it input, and the reason it
                //reuses this branch rather than adding one: a page that has just arrived under a held button is
                //in exactly the position an unfocused window is in — it must be visible and it must not be
                //clickable yet. See where the flag is set for what it is guarding against.
                if (IsActive && _menuClickArmed) _desktop.Render();
                else
                {
                    _desktop.UpdateLayout();
                    _desktop.RenderVisual();
                }
            }

            //THE SHARP FOREGROUND LAYER GOES OVER EVERYTHING, the UI included (#242). It used to be composited
            //the moment the resolve finished, deliberately, so the HUD and the page's panels sat over the cup —
            //the owner ruled the other way on both halves of that: the sparkles should fall over the UI, and it
            //is fine for the cup to cover a panel. So the last picture the frame draws is this one, after the
            //Myra desktop above and before the screenshot writer below, which reads the back buffer and would
            //otherwise save a frame without the thing the page is about. A no-op on every frame that presented
            //nothing, which is nearly all of them.
            CompositeForegroundLast();

            //Last, so it counts a frame that has actually been drawn end to end
            if (_logFrameRate) LogFrameRate((float)gameTime.ElapsedGameTime.TotalSeconds);

            //And after even that, so a saved frame is the finished one — the scene, the overlay and whatever
            //page is over them (#191). It reads the back buffer, so it has to be the last thing in the frame
            //that touches the device.
            ServiceScreenshots();
        }

        #region The frame-rate log (benchmarking)

        //Deliberately NOT TuneQualityToFrameRate's window, which is a latching probe: it measures the menu
        //until the frame rate settles and then stops watching on purpose (#62), which is exactly what a
        //benchmark must not do. This counts presented frames for as long as the process lives, in the menu and
        //in a level alike.
        //
        //It exists because nothing could measure this game from a script at all: the frame rate the player sees
        //is drawn and never logged, and InfoRenderer freezes its counter while the overlay is hidden. #64 asks
        //which part of the fixed per-frame cost is the expensive one, and that question cannot be answered
        //without a number a script can read.
        private readonly bool _logFrameRate;
        private float _fpsWindow;
        private int _fpsFrames;

        /// <summary>
        /// Writes one line a second: the frame rate and every setting that changes what it means, so two runs —
        /// or two machines — can be compared without having to remember what each was launched with.
        /// </summary>
        private void LogFrameRate(float elapsed)
        {
            _fpsWindow += elapsed;
            _fpsFrames++;

            if (_fpsWindow < 1f) return;

            //Divided by the window actually measured rather than assumed to be a second. At the frame rates this
            //exists to measure a single frame overshoots by more than a tenth of it, and calling that "frames
            //this second" would be wrong by the same tenth.
            //The city's drawn/total is on the line for the same reason everything else here is: it changes what
            //the number means, and it is the one figure that says whether the frustum cull is doing anything
            //from where the camera happens to be standing (see City.PrepareVisible).
            string city = (_scene == SceneKind.City || _scene == SceneKind.NeonCity) && _city != null
                ? $", city {_cityVisible}/{_city.Buildings.Length}"
                : string.Empty;

            Console.WriteLine($"[fps] {_fpsFrames / _fpsWindow:F1} — {_scene}, dome {_skyDome}, ssaa {_supersampleFactor}x"
                + $", {GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}"
                + $", vsync {(_uncappedFps ? "off" : "on")}{city}");

            _fpsWindow = 0f;
            _fpsFrames = 0;
        }

        #endregion

        protected override void UnloadContent()
        {
            _pipeline?.Dispose();
            _spriteBatch?.Dispose();
            _scrimTexel?.Dispose();

            //The three sphere meshes and the three renderers' instance buffers, in one call — but not the
            //shared instancing effect they draw through, which the content manager owns
            _balls?.Dispose();

            //The barrel's mesh and its instance buffer, in one call — but not the shared instancing effect,
            //which the content manager owns and the balls, the city, the island and the ceiling all use
            _cannonRig?.Dispose();

            //The dome's two buffers and its owned BasicEffect — not the sky effect, which the content
            //manager owns
            _sky?.Dispose();
            _unitBox?.Dispose();
            _cityRenderer?.Dispose();

            //The island's three meshes, both of its procedural textures and all five of its renderers, in one
            //call — everything the component made
            _island?.Dispose();

            //Every mesh, renderer and procedural texture of the forest scatter, likewise in one call
            _forestScatter?.Dispose();
            _ceilingPlate?.Dispose();

            //The nine self-lit backdrops own their own meshes, particle buffers and effects
            _sceneRenderer?.Dispose();

            //The menu: the Desktop holds the widget tree, and each FontSystem holds the glyph atlases it
            //rasterized for the sizes that were asked of it
            _desktop?.Dispose();
            _menuFontSystem?.Dispose();
            _menuFontSystemBold?.Dispose();
            _menuFontSystemDisplay?.Dispose();

            //The session: the simulation, the contact events, the dispatcher, the pool and the shot-trail
            //buffers all live on the gameplay screen now, which disposes them in the order they need
            _gameplayScreen?.DisposeResources();

            //The synthesized SFX buffers were built in LoadContent and outlive every session.
            _audio?.Dispose();
            _ambience?.Dispose();
            _fireworks?.Dispose();
            _confetti?.Dispose();
            _trophy?.Dispose();
            _titleWordmark?.Dispose();
            _music?.Dispose();

            base.UnloadContent();
        }
    }
}
