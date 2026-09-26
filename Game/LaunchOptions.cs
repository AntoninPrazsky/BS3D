using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;
using System.Globalization;

namespace BS3D
{
    /// <summary>
    /// Everything the command line said to this run (#583), parsed once in <c>Program.Main</c> and handed to
    /// <see cref="BS3DGame"/> as one object.
    /// <para>
    /// Until #583 the list existed three times: <c>Program</c> declared a local per argument and filled it from a
    /// long <c>else if</c> chain, passed forty-four of them to the game's constructor by name, and the
    /// constructor copied them into its <c>_startup*</c> fields, so a new lever cost an edit in four places. Now
    /// an argument is one property here and one row of <see cref="Rows"/>, and the row carries the argument's
    /// spelling, its kind and the note on why it exists.
    /// </para>
    /// <para>
    /// The values are what was <i>said</i>, nothing more. What an argument implies about another (<c>level=</c>
    /// implies <c>play</c>, <c>lost</c> implies <c>result</c>, <c>shot=</c> implies <c>nofocuspause</c>) and how an
    /// argument ranks against the settings file are the game's decisions, taken in its constructor as before.
    /// </para>
    /// </summary>
    internal sealed class LaunchOptions
    {
        //Said rather than dropped (#574). Most rows test their value as well as their name, so a typo
        //("quality=ultr", "fpscap=abc", "levle=3") matches none of them, and the run used to go on as though it had
        //never been asked - the width/height case of #137 was exactly this. Still not a refusal: this is a
        //diagnostic and must never be the reason a scripted run fails to start.
        private const string IGNORED_FORMAT = "[args] Ignored '{0}': not an argument this build takes, or its value did not parse";

        #region The values

        //Null means "nobody said", which is what lets the settings file answer instead (#354). A plain
        //false could not say the difference between "the player asked for a window" and "no argument was
        //given", so a stored fullscreen would have been silently ignored on every launch.
        //
        //Both sides are wired since #455: the stored default became fullscreen, so a run on a machine with
        //no settings file — a fresh checkout, a downloaded release, a scripted capture on either — had no
        //way left to ask for a window at all. "windowed" is that way, and it is the false this has always
        //been able to carry.
        internal bool? Fullscreen { get; private set; }
        internal bool? UncappedFps { get; private set; }

        //Testing only: "fpscap=N" is "nocap" with a ceiling — it presents immediately, so nothing
        //quantizes the reading, and idles out the rest of each frame's period, so a cheap frame does not
        //leave the card flat out. The Testbed has had it since #250; the Game needed it for #270, where a
        //vsync-capped level could only ever say "dearer than one refresh". Zero means no cap.
        internal int FpsCap { get; private set; }
        internal int? SceneSeed { get; private set; }
        internal bool Tour { get; private set; }
        internal int WindowWidth { get; private set; }
        internal int WindowHeight { get; private set; }
        internal float LineLoss { get; private set; }

        //Zero when "exposure=" is absent: the game then takes the settings file's, and then its own default
        internal float Exposure { get; private set; }

        //Left null when absent, so the game keeps doing what it normally does: a random one of the fifteen
        //scenes, and whatever dome that scene wants. Pinning both is what makes a frame-cost measurement
        //repeatable (see BS3DGame.LogFrameRate) — without it every run measures a different backdrop.
        internal SceneKind? Scene { get; private set; }
        internal byte? SkyDome { get; private set; }

        //Write one frame-rate line a second to stdout
        internal bool LogFrameRate { get; private set; }

        //Null means "nobody said", so the adaptive path is free to measure this machine and step the tier
        //down (BS3DGame.TuneQualityToFrameRate), starting at High — the look the game is authored at. Naming one
        //settles it, exactly as naming ssaa= does.
        internal QualityLevel? Quality { get; private set; }

        //Left null when "ssaa=" is absent, which is how the game tells "the player wants two" from "nobody
        //said" — only the latter may be lowered for a machine that cannot afford the default. An explicit
        //ssaa= is never overridden.
        internal int? SupersampleFactor { get; private set; }

        //Testing only: start the victory display on the front end, which is otherwise reachable only by
        //clearing a level — and clearing one cannot be scripted, so this is how the fireworks get
        //screenshotted and measured at all.
        internal bool Celebrate { get; private set; }

        //Testing only: pin the floor alarm's laser net on (BS3DGame.ForceLaserWarning). Reaching it honestly
        //means playing a level to within two ceiling steps of losing it, which can no more be scripted than
        //clearing one can.
        internal bool Lasers { get; private set; }

        //Testing only: offer every tutorial card as if none had been taught, and record none (#189). The
        //cards are gated on the save — a lesson done is never shown again — so on a save that finished the
        //chapter months ago they are otherwise unreachable, and a run that taught them for real would write
        //to the owner's save, which no scripted run may do. "tutorial" keeps the real detection (play the
        //level and the cards answer); "tutorial=demo" is a reel that runs every card on a clock, for a run
        //nothing can press a key in. Null means the argument was absent. See BS3DGame.TutorialMode.
        internal string Tutorial { get; private set; }

        //Testing only: start with the master volume at zero. A scripted screenshot or benchmark run has
        //no business making noise. There IS a settings file since #354, and this deliberately does not
        //touch it — mute is a run's instruction, so it silences this run and is never written back. The
        //settings rows can still raise it.
        internal bool Mute { get; private set; }

        //Testing only: start with the FPS overlay hidden, whatever the settings file says. Exactly "mute"'s
        //argument in the other sense (#452): a picture meant for somebody else — the README's screenshot,
        //a capture filed with an issue — has no business carrying a debug readout, and until this existed
        //the only way to take one was F10, which WRITES the player's settings file on both presses. A run's
        //instruction, never written back; F10 still toggles it from here, and the settings row still owns
        //what a player sees.
        internal bool NoFpsOverlay { get; private set; }

        //Testing only: keep a level running when the window loses focus (#355). Same population as "mute"
        //— a run nobody is sitting at — and for the mirror-image reason: a level that pauses itself while
        //unattended stops producing the frames the run was started to collect.
        internal bool NoFocusPause { get; private set; }

        //Testing only: draw the ceiling as the plain translucent pane it was before #541, with no bend. The other
        //half of a paired cost measurement of the refraction, since the Game cannot sweep variants in one
        //process the way the Testbed's alt= does.
        internal bool PlainCeiling { get; private set; }

        //Testing only: drop straight into the first level, skipping the title card and the menu. The
        //session's placement and physics write their figures to stdout only once a level is built, and
        //building one honestly takes a mouse on a Myra button — which a scripted run does not have. The stack
        //ends up exactly as a player's Play click leaves it, so nothing downstream can tell the difference.
        internal bool Play { get; private set; }

        //Testing only: which level "play" should open, as a 1-based place in the set or as a name. Null
        //means the argument was absent, and then "play" opens the first level as it always has.
        internal string Level { get; private set; }
        //Testing only: a level file outside the set (#332). See the argument's own row below.
        internal string LevelFile { get; private set; }

        //Testing only: which level the FRONT END should hang over the island instead of rolling one at
        //random (#254). Null means the argument was absent, and then the backdrop rolls as a player's does.
        //Named the way Level is: a 1-based place in the set, or a name.
        internal string Preview { get; private set; }

        //Testing only: open the level picker at boot, on a named chapter or on the one it chooses itself
        //(#273). Null means the argument was absent. Empty string means "pick" without a chapter, which is
        //why this is a string and not an int? — the two are different requests.
        internal string Pick { get; private set; }

        //Testing only: open the About page at boot, and with "about=play" start its player (#443). Null means
        //the argument was absent; empty means the page alone.
        internal string About { get; private set; }

        //Testing only: open the Settings page at boot (#189), on "about"'s and "pick"'s reasoning — three
        //presses reach it on a machine somebody is sitting at, and none reach it from a script.
        internal bool Settings { get; private set; }
        internal string SettingsRows { get; private set; }
        //Testing only: open one level's online boards at boot (#547), 1-based as the picker numbers it. Null means absent.
        internal int? Board { get; private set; }
        internal int BoardPage { get; private set; } = 1;

        //Testing only: open the Help screen at boot, and "help=<n>" on its nth page (#427). Null means
        //the argument was absent; the number is 1-based because that is what the page prints about itself.
        internal int? Help { get; private set; }

        //Testing only: draw every ball in one style whatever the level files say (#258). Null means the
        //argument was absent, and then each map is drawn in what it is authored in, as a player sees it.
        //It exists because the two styles can otherwise only be compared across two DIFFERENT levels —
        //and a look is judged by putting the same cluster, at the same stand-off, under the same dome,
        //in one material and then the other.
        internal BallStyle? BallStyle { get; private set; }

        //Testing only: put a cleared level's result screen up at startup. A level's ending — the released
        //camera, the stars landing, the arena going out of focus — is otherwise only reachable by winning
        //or losing one, which cannot be scripted any more than clearing one can.
        internal bool Result { get; private set; }

        //Testing only: make that result page a BLOCK milestone rather than an ordinary clear (#184). A block
        //is finished only when every level of a five-level chapter is cleared, so where a single clear merely
        //cannot be scripted, this cannot be reached at all without playing the campaign.
        internal bool BlockDone { get; private set; }
        internal bool Lost { get; private set; }

        //Testing only: start the campaign's closing confetti (#215). Reaching it honestly means clearing the
        //last level of the whole set, which is further out of a script's reach than a block milestone is.
        internal bool Confetti { get; private set; }

        //Testing only: the rating the "result" page reports, and so which of the four trophy cups it
        //presents (#183). Null leaves the authored three — the only one a test could otherwise reach.
        internal int? ResultStars { get; private set; }

        //Testing only: shut the "result" page's next level, by "stars" or by "sequence" (#397). Null leaves
        //it open, which is what the page has always been photographed as.
        internal string NextLocked { get; private set; }

        //Testing only: what the HUD's multiplier readout shows (#180). The capped state takes five
        //consecutive scoring shots to reach honestly, so it is otherwise unphotographable.
        internal int? Streak { get; private set; }
        internal int WildcardEvery { get; private set; }

        //Testing only: this level's power-up charges (#392), "kind:count" pairs separated by commas
        //("powerups=swap:1"). Null grants nothing, since no shipped or generated level authors one yet.
        internal string Powerups { get; private set; }

        //Testing only: wall-clock seconds at which the game saves a PNG of its own frame. Null means the
        //argument was absent, which is every run but a scripted one. F12 does the same thing by hand — but
        //only this trigger survives a LOCKED desktop, which takes no keystrokes at all (#191). It is also the
        //one that makes a shot repeatable; see BS3DGame.Screenshot.cs.
        internal float[] ShotSeconds { get; private set; }

        //Testing only: wall-clock seconds at which the level sets off one of its bombs (#389), on the clock
        //ShotSeconds counts. A blast takes a shot landed beside a bomb, which a script cannot aim. See
        //BS3DGame.TryTakeForcedDetonation.
        internal float[] DetonateSeconds { get; private set; }

        //Testing only: a folder to keep every one of this player's files in for this run, instead of
        //%LOCALAPPDATA%\BS3D (#546). Null means the argument was absent. See UserData.UseForTesting, which
        //Program calls with it before anything resolves UserData.Directory.
        internal string UserDataDirectory { get; private set; }

        #endregion

        /// <summary>
        /// Reads <paramref name="args"/> in order, a later argument overwriting an earlier one of the same name,
        /// and prints an <c>[args] Ignored</c> line for each one no row (and not <see cref="ScriptedPlay"/>) took.
        /// </summary>
        internal static LaunchOptions Parse(string[] args)
        {
            LaunchOptions options = new();
            foreach (string arg in args)
                if (!options.TryApply(arg)) Console.WriteLine(string.Format(CultureInfo.InvariantCulture, IGNORED_FORMAT, arg));
            return options;
        }

        //The rows in order, the first that takes the argument wins. A row whose name matches but whose value
        //does not parse (or is refused) does NOT take it, and the walk goes on - which, since no two rows share a
        //spelling, ends at the Ignored line. Scripted play is asked last; see its own row note.
        private bool TryApply(string arg)
        {
            foreach (Row row in Rows)
                if (row.TryApply(this, arg)) return true;

            //"sweep=", "rmb=" and "fire=" are the hands of a run nobody is sitting at (#402): the barrel swung,
            //precise aim held, a shot fired, on the wall clock "shot=" counts. See ScriptedPlay, which keeps its
            //own parse because it is a static the session reads, not a value the game is handed.
            return ScriptedPlay.TryParse(arg);
        }

        #region The table

        //One row per argument. A flag is the bare word; every other kind is "name=value", its value parsed as
        //the kind says and then offered to the row's accept test, if it has one. Spellings are case-insensitive.
        private static readonly Row[] Rows =
        [
            Row.Flag("fullscreen", o => o.Fullscreen = true),
            //Its pair (#455). Neither is written back to the settings file — both are a run's instruction,
            //like "mute" — so a windowed capture run leaves a player's stored fullscreen exactly as it was.
            Row.Flag("windowed", o => o.Fullscreen = false),
            //"nocap" lifts the frame limiter's ceiling so real rendering headroom can be measured. It meant
            //"disables vsync" until #270 — the game presents immediately in EVERY mode now, so all this
            //picks is FrameLimiter's target.
            Row.Flag("nocap", o => o.UncappedFps = true),
            //"fpscap=N" is that same ceiling set by hand, and it wins over "nocap" outright rather than
            //being reconciled with it (BS3DGame.FrameLimitHz), so the two cannot be given inconsistently.
            Row.Int("fpscap", (o, v) => o.FpsCap = v, v => v > 0),

            //"sceneseed=N" pins every scene's procedural arrangement, which each launch otherwise rolls.
            //It is what makes a capture pair or a measured A/B comparable at all once the roll is the
            //default, and 0 is the arrangement everything before the feature was photographed against.
            Row.Int("sceneseed", (o, v) => o.SceneSeed = v),

            //"tour" opens the scene menu with the current scene's establishing flight already running
            //(#406) - the only way a replayed tour can be photographed, since a synthetic click never
            //reaches this window.
            Row.Flag("tour", o => o.Tour = true),

            //"lineloss=SECONDS" stages the line's loss that far into a level (#434). A real one needs a
            //descending ceiling and a couple of dozen shots, and the Game takes no synthetic input, so
            //without this the one moment the feature exists for cannot be photographed.
            Row.Float("lineloss", (o, v) => o.LineLoss = v, v => v > 0f),

            //"width=N"/"height=N" pin the WINDOWED back buffer, as the Testbed's own pair does. Until they
            //were added here the Game ignored them silently, so a capture asked for at the owner's panel
            //came back at the default window and looked entirely plausible.
            Row.Int("width", (o, v) => o.WindowWidth = v, v => v > 0),
            Row.Int("height", (o, v) => o.WindowHeight = v, v => v > 0),
            //"ssaa=<n>" trades sharpness against fill rate; "exposure=<f>" is the renderer's shutter speed
            Row.Int("ssaa", (o, v) => o.SupersampleFactor = v),
            Row.Float("exposure", (o, v) => o.Exposure = v),
            //"logfps" writes one frame-rate line a second to stdout; "scene="/"sky=" pick the backdrop the
            //FRONT END hangs. They do NOT survive a level: a Level file names its own scene, dome and
            //weather and GameplayScreen applies all three over these, so "play level=X scene=meadow" draws
            //whatever X names. #270 read five runs as a scene comparison that were one scene measured
            //twice because of it. The [fps] line prints the LIVE scene for exactly this reason - it is the
            //authority, not this argument. The scene names are the Testbed's, so one benchmark script
            //drives either executable: the spellings are SceneRenderer.TryParseScene's since #75 — the
            //Testbed grew an if/else chain, this a switch, and the two had to be kept in step by hand. That
            //is exactly the agreement one shared parse cannot break.
            Row.Flag("logfps", o => o.LogFrameRate = true),
            Row.Value<SceneKind>("scene", SceneRenderer.TryParseScene, (o, v) => o.Scene = v),
            Row.Value<byte>("sky", TryParseSkyDome, (o, v) => o.SkyDome = v),
            //"quality=" pins the whole detail tier; "ssaa=" then overrides just its supersample entry. By
            //name (low/medium/high/ultra); IsDefined because TryParse also takes any number, and a tier past
            //the last one would index off the end of QualityPreset.Presets.
            Row.Value<QualityLevel>("quality", TryParseQuality, (o, v) => o.Quality = v),
            //"celebrate" fires the victory display at startup. Clearing a level is the only thing that
            //normally starts it, and clearing one cannot be scripted, so without this the fireworks can be
            //neither screenshotted nor measured — the same reason autoshoot and aimshoot exist.
            Row.Flag("celebrate", o => o.Celebrate = true),
            //"confetti" starts the campaign's closing fall (#215) — one step further along that reasoning
            //than "blockdone": a block milestone needs five levels played, this needs the whole campaign.
            Row.Flag("confetti", o => o.Confetti = true),
            //"stars=<0..4>" rates that result page, which is the only way to see the other three trophy
            //cups (#183): reaching a four-star clear honestly means being good at the game, not scripting it.
            Row.Int("stars", (o, v) => o.ResultStars = v),
            //"nextlocked=<stars|sequence>" shuts that page's next level by one lock or the other (#397), so the
            //note explaining the missing Next Level can be looked at. Neither was reachable from a test: the
            //page hardcoded the next level open, and the one time the note was seen it was stating a reason
            //its own numbers refuted, and running off the edge of its plate. An unknown lock is ignored.
            Row.Text("nextlocked", (o, v) => o.NextLocked = v, BS3DGame.IsStartupNextLock),
            //"streak=<n>" pins what the HUD's multiplier shows (#180) — the display only, never the
            //scoring, so the lever cannot alter the thing it is there to look at.
            Row.Int("streak", (o, v) => o.Streak = v),
            //"wildcard=<n>" makes every Nth loaded ball a wildcard (#330) on whatever level is played. It
            //DOES change play, unlike the levers above it — it has to, because no shipped level hands one
            //out and a wildcard cannot be authored into a map: it is the gun's ball, not the cluster's.
            Row.Int("wildcard", (o, v) => o.WildcardEvery = v),
            //"powerups=<kind:count,...>" grants power-up charges (#392) on whatever level is played, in
            //wildcard='s own shape and for the same reason: no shipped or generated level authors one yet.
            //Passed through as a raw string — GameplayScreen does its own parsing, since only it knows
            //the PowerupKind enum this names.
            Row.Text("powerups", (o, v) => o.Powerups = v),
            //"lasers" pins the floor alarm's laser net on while a level is played, for the same reason.
            Row.Flag("lasers", o => o.Lasers = true),
            //"tutorial" offers every tutorial card and records nothing, "tutorial=demo" reels them (#189) —
            //see the property's own note.
            Row.Flag("tutorial", o => o.Tutorial = "force"),
            Row.Text("tutorial", (o, v) => o.Tutorial = v),
            //"mute" starts silent, for the harnesses; the settings rows can still raise it.
            Row.Flag("mute", o => o.Mute = true),
            //"nofps" hides the FPS overlay for this run, for a picture somebody else will look at (#452).
            Row.Flag("nofps", o => o.NoFpsOverlay = true),
            //"play" skips the front end into the first level, so a session's figures can be measured at all.
            Row.Flag("play", o => o.Play = true),
            //"level=<n|name>" does the same for any entry of the set — its 1-based place, as the title bar
            //numbers it, or its name. "play" reaches the first level only, which is the lightest one there
            //is, so a frame with a real cluster in it could not be measured at all: #167 asked for a level
            //"near the shipped set's 959-ball end" and there was no way to ask for one.
            Row.Text("level", (o, v) => o.Level = v),
            //"levelfile=<path>" plays a level file that is NOT IN THE SET, which is the one thing "level="
            //cannot do (#332). The set is what the campaign is, and a level built to try a mechanic out is
            //not part of it — the six special ball kinds built so far all ship in no level at all, so the
            //only way to hold one in the hands was to author a level and edit the campaign around it.
            //It pins the whole run to that file, which is what a testing argument should do and is the
            //same shape "preview=" takes for the front end. Implies "play" for "level="'s own reason.
            Row.Text("levelfile", (o, v) => o.LevelFile = v),
            //"result" puts a cleared level's result screen up; with "celebrate" that is the whole
            //end-of-level moment, fireworks and all, over an arena that goes out of focus behind it.
            Row.Flag("result", o => o.Result = true),
            //"blockdone" makes that page a BLOCK milestone rather than an ordinary clear (#184). One step
            //further along the same reasoning: finishing a block needs every level of a five-level chapter
            //cleared, so unlike a single clear it cannot be reached in a scripted run at all.
            Row.Flag("blockdone", o => o.BlockDone = true),
            //"lost" makes that page a FAILED level rather than a clear (#238). Without it the fail state was
            //unreachable from a test at all: "result" hardcoded a clear, so "stars=0" gave a starless CLEARED
            //page and nobody had ever looked at the reason line the player is told they lost by.
            Row.Flag("lost", o => o.Lost = true),
            //"shot=<t1,t2,…>" saves a PNG of the frame at those wall-clock seconds. Parsed leniently on
            //purpose — a malformed entry is dropped and the rest stand, the way an unknown scene name
            //falls back rather than throwing: this is a diagnostic, and it must never be the reason a
            //scripted run fails to start.
            //The parse is ScreenshotWriter's since #371, for the reason scene= is SceneRenderer's: the
            //Testbed takes shot= too now, and a list one executable reads and the other does not would be
            //exactly the drift one shared parse cannot have.
            Row.List("shot", (o, v) => o.ShotSeconds = v),
            //"detonate=<t1,t2,…>" sets off one of the level's bombs at those wall-clock seconds (#389), on the
            //clock "shot=" counts so the two can be written against each other. A blast needs a shot landed
            //beside a bomb, which no script can aim, and the effect it answers with is the Game's alone. It
            //DOES change play, like "wildcard=": the bomb really goes. Parsed by the same lenient list.
            Row.List("detonate", (o, v) => o.DetonateSeconds = v),
            //"pick" puts the LEVEL PICKER up at boot, and "pick=<n>" puts it up on that chapter (#273). The
            //page itself is two keypresses away for anyone sitting at the machine and unreachable on a
            //locked desktop, which takes no keystrokes — and since #273 it is a pager, so its other eight
            //chapters are several presses in and "a shot of the picker" means nothing without saying which.
            Row.Flag("pick", o => o.Pick = string.Empty),
            Row.Text("pick", (o, v) => o.Pick = v),
            //"about" puts the About page up at boot and "about=play" starts its player of the original score
            //(#443) — pick's reasoning, plus the press a visualizer needs before there is anything to see.
            Row.Flag("about", o => o.About = string.Empty),
            Row.Text("about", (o, v) => o.About = v),
            //"settings" puts the Settings page up at boot (#189), for photographing a row.
            Row.Flag("settings", o => o.Settings = true),
            //"settings=<row,...>" also activates those rows once the page is up (#548) — online, nickname, remove —
            //through the page's own click handlers, since a run nobody is sitting at cannot click one. "remove" is
            //refused outside a userdata= folder: it would take the player's own scores off the server.
            Row.Text("settings", (o, v) => o.SettingsRows = v),
            //"board=<n>" opens level n's online boards at boot (#547): the picker's button needs a level the pointer or
            //the cursor rested on, and a run nobody is sitting at has neither
            //"board=<n>:<page>" opens it on that page, so the paging can be photographed too. Taken whatever its
            //value: a half that does not parse is left as it was, and the argument is not reported.
            Row.Text("board", ApplyBoard),
            //"help" opens the Help screen and "help=<n>" opens it on that page (#427) - the same reasoning
            //one turn further, since Help is six pages behind one entry and its Previous/Next stand side
            //by side, so a scripted walk has to guess a focus order to reach page four at all.
            Row.Flag("help", o => o.Help = 1),
            Row.Int("help", (o, v) => o.Help = v),
            //"preview=<n|name>" pins which map the FRONT END hangs, the way "level=" pins which one is
            //played. The menu's camera is framed for that map since #254, so without this two shots of
            //the front end are two shots of different maps at different stand-offs.
            Row.Text("preview", (o, v) => o.Preview = v),
            //"balls=<beach|bubble>" overrides what every level says it is made of (#258), the way "scene="
            //overrides the backdrop each one names. Parsed through BallStyles, so the spellings are the
            //ones a level file takes; an unknown one leaves the levels' own answers standing.
            Row.Value<BallStyle>("balls", BallStyles.TryParse, (o, v) => o.BallStyle = v),
            //"nofocuspause" stops a level pausing itself when the window loses focus (#355). For any run
            //nobody is sitting at: a benchmark measuring 70 seconds of frames, a capture rig on a locked
            //desktop. "shot=" implies it on its own — see BS3DGame.PauseOnFocusLoss.
            Row.Flag("nofocuspause", o => o.NoFocusPause = true),
            //"plainceiling" draws the ceiling's glass unbent (#541), for the pair a cost measurement needs.
            Row.Flag("plainceiling", o => o.PlainCeiling = true),
            //"userdata=<dir>" keeps the save, the settings, the online identity and the outbox in <dir> for
            //this run (#546), so a scripted run that clears a level or needs a setting never touches the
            //player's own files. Applied by Program, before the game exists, because every one of them
            //resolves through UserData on first use.
            Row.Text("userdata", (o, v) => o.UserDataDirectory = v),
        ];

        private static bool TryParseSkyDome(string text, out byte dome) =>
            byte.TryParse(text, out dome) && dome >= 1 && dome <= BS3DGame.SKY_DOME_COUNT;

        private static bool TryParseQuality(string text, out QualityLevel quality) =>
            Enum.TryParse(text, ignoreCase: true, out quality) && Enum.IsDefined(quality);

        private static void ApplyBoard(LaunchOptions options, string value)
        {
            string[] parts = value.Split(':');
            if (int.TryParse(parts[0], out int board)) options.Board = board;
            if (parts.Length > 1 && int.TryParse(parts[1], out int page) && page > 0) options.BoardPage = page;
        }

        /// <summary>A parse the way the framework's own <c>TryParse</c> methods are shaped.</summary>
        private delegate bool ValueParser<T>(string text, out T value);

        /// <summary>What an argument's value is read as. <c>Flag</c> is the one kind that takes no value.</summary>
        private enum Kind
        {
            /// <summary>The bare word, compared whole.</summary>
            Flag,
            /// <summary><c>int.TryParse</c> in the current culture, as every integer argument always was.</summary>
            Int,
            /// <summary><c>float.TryParse</c> in the invariant culture, so a decimal point is always a point.</summary>
            Float,
            /// <summary>The value as written, empty included.</summary>
            Text,
            /// <summary>A lenient list of seconds, <see cref="ScreenshotWriter.ParseSeconds"/>'s.</summary>
            List,
            /// <summary>A named value parsed by the type that owns the spellings (a scene, a dome, a tier, a style).</summary>
            Value,
        }

        /// <summary>
        /// One argument: its name, its kind, and what taking it does. <see cref="TryApply"/> answers whether the
        /// argument was this row's AND its value was accepted; only then has anything been set.
        /// </summary>
        private sealed class Row
        {
            private readonly string _name;
            private readonly string _prefix;
            private readonly Kind _kind;
            private readonly Func<LaunchOptions, string, bool> _apply;

            private Row(string name, Kind kind, Func<LaunchOptions, string, bool> apply)
            {
                _name = name;
                _prefix = name + "=";
                _kind = kind;
                _apply = apply;
            }

            internal bool TryApply(LaunchOptions options, string arg)
            {
                if (_kind == Kind.Flag)
                    return string.Equals(arg, _name, StringComparison.OrdinalIgnoreCase) && _apply(options, null);

                return arg.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)
                    && _apply(options, arg.Substring(_prefix.Length));
            }

            internal static Row Flag(string name, Action<LaunchOptions> set) =>
                new(name, Kind.Flag, (o, _) => { set(o); return true; });

            internal static Row Int(string name, Action<LaunchOptions, int> set, Func<int, bool> accept = null) =>
                new(name, Kind.Int, (o, text) =>
                {
                    if (!int.TryParse(text, out int value) || (accept != null && !accept(value))) return false;
                    set(o, value);
                    return true;
                });

            internal static Row Float(string name, Action<LaunchOptions, float> set, Func<float, bool> accept = null) =>
                new(name, Kind.Float, (o, text) =>
                {
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                        || (accept != null && !accept(value))) return false;
                    set(o, value);
                    return true;
                });

            internal static Row Text(string name, Action<LaunchOptions, string> set, Func<string, bool> accept = null) =>
                new(name, Kind.Text, (o, text) =>
                {
                    if (accept != null && !accept(text)) return false;
                    set(o, text);
                    return true;
                });

            internal static Row List(string name, Action<LaunchOptions, float[]> set) =>
                new(name, Kind.List, (o, text) => { set(o, ScreenshotWriter.ParseSeconds(text)); return true; });

            internal static Row Value<T>(string name, ValueParser<T> parse, Action<LaunchOptions, T> set) =>
                new(name, Kind.Value, (o, text) =>
                {
                    if (!parse(text, out T value)) return false;
                    set(o, value);
                    return true;
                });
        }

        #endregion
    }
}
