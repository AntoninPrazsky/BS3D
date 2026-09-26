using BS3D.Screens;
using System;
using System.Globalization;

namespace BS3D
{
    /// <summary>
    /// The command line's one-shot actions (#583): the testing levers that each do one thing, once, at their
    /// moment — play a level, fire the celebrations, open a page at boot, put a staged result page up — and
    /// the order and the gating they are done in.
    /// <para>
    /// Until #583 this was seventeen <c>_startup*</c> fields on <see cref="BS3DGame"/>, seeded in its
    /// constructor from the arguments and consumed by a block of about a hundred and fifty lines in its
    /// <c>Update</c>. The block is <see cref="Step"/> now, called from the same place in <c>Update</c>, and
    /// what one argument implies about another (<c>level=</c> means <c>play</c>, <c>lost</c> means
    /// <c>result</c>, <c>settings=</c> means <c>settings</c>) is decided here, in the constructor, rather than
    /// by the parser or the host.
    /// </para>
    /// <para>
    /// The <b>standing</b> levers stay with the host, because they are not actions but a state of the run that
    /// something reads for its whole length: <c>scene=</c> and <c>sky=</c> (read by <c>LoadContent</c>),
    /// <c>preview=</c> (asked on every roll), and the session's own — <c>levelfile=</c>, <c>streak=</c>,
    /// <c>wildcard=</c>, <c>powerups=</c>, <c>lasers</c>, <c>lineloss</c>, <c>detonate=</c>, <c>tutorial</c>,
    /// <c>balls=</c> and <c>seed=</c> — which are one <see cref="SessionTestOptions"/> since #582. So does
    /// <c>shot=</c>'s implication of <c>nofocuspause</c>, which is the host's <c>PauseOnFocusLoss</c>.
    /// </para>
    /// <para>
    /// Every action is guarded by a latch it clears as it runs, so after the last one has fired <see cref="Step"/>
    /// is a row of false tests. Nothing here allocates per frame; the one <see cref="LevelResult"/> is made
    /// once, by the action that presents it.
    /// </para>
    /// </summary>
    internal sealed class StartupScript
    {
        private const string LOCK_STARS = "stars";
        private const string LOCK_SEQUENCE = "sequence";

        /// <summary>Whether <paramref name="lockName"/> is one of the locks <c>nextlocked=</c> takes.</summary>
        internal static bool IsNextLock(string lockName) =>
            string.Equals(lockName, LOCK_STARS, StringComparison.OrdinalIgnoreCase)
            || string.Equals(lockName, LOCK_SEQUENCE, StringComparison.OrdinalIgnoreCase);

        //Testing only: the "celebrate" argument. Consumed on the first Update, AFTER any startup level — see
        //StartCelebrations, which is the one place either of these is read.
        private bool _celebrate;

        //Testing only: the "confetti" argument (#215), consumed the same way and for the same reason.
        private bool _confetti;

        //Testing only: the "stars=" argument, which sets the rating the test result page reports (#183) and so
        //which of the four trophy cups it presents. Null leaves the authored three.
        private readonly int? _resultStars;

        //Testing only: the "play" argument. Consumed on the first Update, after the stack has applied
        //BuildMenu's pushes. That once mattered: StartGame's PopTo<BackdropScreen> was decided against the live
        //stack at the call, found the backdrop's push still pending, skipped, and left the splash buried under
        //the session. The manager resolves it when it is applied since #576, so the order no longer carries
        //that; the first Update is simply where a click would come from.
        private bool _play;

        //Testing only: the "level=" argument — which entry _play should open, as a 1-based place in the
        //set or as a name. Null means "the first one", which is what play alone has always done.
        private readonly string _level;

        //Testing only: the "result" argument, consumed on the first Update for the "play" reason above — the
        //page has to go over a stack that exists. It is how the end-of-level moment gets looked at at all:
        //everything about it (the released camera, the star reveal, the arena going out of focus) only happens
        //once a level has been won or lost, and neither can be scripted — the "celebrate" reasoning again.
        private bool _result;

        //Testing only: the "blockdone" argument, which makes the "result" page above a BLOCK milestone instead of
        //an ordinary clear (#184). Same reasoning as "celebrate" and "result", one step further along: finishing a
        //block needs every level of a five-level chapter cleared, so the moment cannot be reached in a scripted
        //run at all — and it is the one thing about the milestone that has to be LOOKED at rather than asserted.
        private readonly bool _blockDone;

        /// <summary>
        /// <c>lost</c>: makes the startup result page a FAILED one rather than a clear (#238). It exists because
        /// the page could not be photographed at all — <c>result</c> hardcoded <c>cleared: true</c>, so
        /// <c>stars=0</c> gave a starless CLEARED page and the fail state, its reason line included, had never
        /// been looked at outside a real loss. Same reasoning as <c>blockdone</c> and <c>stars=</c>: a state a
        /// test cannot reach is a state nobody checks.
        /// </summary>
        private readonly bool _lost;

        /// <summary>
        /// <c>nextlocked=&lt;stars|sequence&gt;</c>: shuts the startup result page's next level by that lock (#397),
        /// or null to leave it open. The note that explains a missing Next Level had never been looked at from a
        /// test — the page hardcoded the next level open — and the one time it was seen, in play, it quoted a
        /// star price the player already held and ran past the right edge of its plate.
        /// </summary>
        private readonly string _nextLocked;

        /// <summary>
        /// <c>pick</c> / <c>pick=&lt;chapter&gt;</c>: put the level picker up at boot, on that chapter (1-based)
        /// or on whichever one the page itself would open (#273). Null for neither.
        /// <para>
        /// Two keypresses reach the picker on a machine somebody is sitting at, and none reach it on a locked
        /// desktop, which takes no keystrokes at all — the same wall <c>shot=</c> exists on the other side of.
        /// The chapter half is the part that could not be scripted even unlocked: the page is a <b>pager</b>
        /// since #273, so its other eight chapters are each several presses in, and a shot of "the picker" is a
        /// shot of one chapter unless the run can say which. Same reasoning as <c>preview=</c> — a page whose
        /// content is chosen for you cannot be compared with a second shot of itself.
        /// </para>
        /// </summary>
        private string _pick;

        //Testing only: the "tour" argument (#406) - the scene menu with the current scene's establishing
        //flight already running, which is the only way this can be photographed from a script.
        private bool _tour;

        /// <summary>
        /// <c>about</c> / <c>about=play</c>: put the About page up at boot, and with <c>play</c> start its player
        /// on the first piece (#443). Null for neither. The page is two presses away for someone at the machine
        /// and none on a locked desktop — <c>pick</c>'s wall — and its visualizer is only worth a shot while a
        /// piece is actually sounding, which takes a third press nobody is there to make.
        /// </summary>
        private string _about;

        //Testing only: the "settings" argument (#189) — the Settings page at boot, on _about's reasoning
        private bool _settings;

        //Testing only: the settings rows to activate once the page is up (settings=<row,...>, #548). Null for none.
        private string _settingsRows;

        //Testing only: a level whose online boards open at boot (board=<n>, #547), 1-based. Null for none.
        private int? _board;
        private readonly int _boardPage;

        //Which Help page to open at boot, 1-based, or null for "not asked" (#427)
        private int? _help;

        /// <param name="launch">What the command line said to this run.</param>
        /// <param name="levelFile">
        /// <see cref="SessionTestOptions.StartupLevelFile"/>: <c>levelfile=</c> as the host keeps it, null when
        /// it was not given or was blank — passed rather than re-read so the two cannot disagree about whether a
        /// file was named.
        /// </param>
        internal StartupScript(LaunchOptions launch, string levelFile)
        {
            _celebrate = launch.Celebrate;
            _confetti = launch.Confetti;
            _resultStars = launch.ResultStars;
            _level = launch.Level;

            //Naming a level means playing it, so "level=" implies "play" rather than needing it alongside
            _play = launch.Play || launch.Level != null || levelFile != null;
            //Asking for a FAILED result page means asking for the result page, so "lost" implies "result" rather
            //than needing it alongside — the same rule "level=" implies "play" by. Written here and not left to
            //the caller because the first thing `lost` did on its own was put the main menu up and say nothing.
            //"nextlocked=" (#397) is a statement about that same page, so it implies it for the same reason.
            _result = launch.Result || launch.Lost || launch.NextLocked != null;
            _blockDone = launch.BlockDone;
            _lost = launch.Lost;
            _nextLocked = launch.NextLocked;
            _pick = launch.Pick;
            _about = launch.About;
            _settings = launch.Settings || launch.SettingsRows != null;
            _settingsRows = launch.SettingsRows;
            _board = launch.Board;
            _boardPage = launch.BoardPage;
            _help = launch.Help;
            _tour = launch.Tour;
        }

        /// <summary>
        /// Performs whichever of the startup actions are due this frame, each exactly once. Called from
        /// <c>BS3DGame.Update</c> after the screen stack has updated, which is where a click would land.
        /// </summary>
        internal void Step(BS3DGame game)
        {
            //Testing only (the play argument): jump into the first level through the very pop-and-push a
            //player's click takes. The pop to the backdrop takes the splash off with it — the splash is drawn
            //for the one frame this costs, exactly as a very fast click would leave it, and being off the stack
            //it never asks for its hand-over to the menu (see _play for why this is no longer order-bound).
            if (_play)
            {
                _play = false;

                if (_level == null) game.StartGame(newGame: true);
                else game.StartGameAt(game.ResolveStartupLevel(_level));
            }

            //AFTER the startup level, and that order is the whole point — see the method.
            StartCelebrations(game);

            //The level picker, over the front end (#273). Held back until the title card has gone, as every page
            //below is — see the result page's note for why that is still wanted now that it is not needed.
            if (_pick != null && !game.IsSplashUp)
            {
                //A chapter given as a number pins the page to it; anything else (bare "pick") leaves the page
                //to open where it would for a player, which is the other thing worth photographing.
                if (int.TryParse(_pick, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pickChapter))
                    game.PinLevelSelectChapter(pickChapter);

                _pick = null;

                game.OpenLevelSelect();
            }

            //The About page and its player (#443), held back past the title card for the same reason.
            if (_about != null && !game.IsSplashUp)
            {
                if (string.Equals(_about, "play", StringComparison.OrdinalIgnoreCase)) game.Jukebox?.PlayPause();

                _about = null;

                game.OpenAbout();
            }

            //The scene menu with its tour already flying (#406), held back past the title card for the same
            //reason. It is the argument that makes a replayed tour photographable at all: the menu cannot be
            //driven from a script here (a synthetic click lands in whatever window has focus, never in this
            //one), and it is also what the owner will page through to review the twenty.
            if (_tour && !game.IsSplashUp)
            {
                _tour = false;

                game.OpenSceneSelect();
                game.Backdrop?.PlayTour();
            }

            //And the Settings page (#189), held back past the title card for the same reason
            if (_settings && !game.IsSplashUp)
            {
                _settings = false;

                game.OpenSettings();
            }

            //Its rows, once the page is the one on top and its tree has been built — a frame after the push lands
            else if (_settingsRows != null && game.IsSettingsPageReady)
            {
                string rows = _settingsRows;
                _settingsRows = null;

                game.ActivateSettingsRowsForTesting(rows);
            }

            //A level's online boards (#547), past the title card for the same reason
            if (_board is int boardLevel && !game.IsSplashUp)
            {
                _board = null;
                game.OpenLevelBoard(boardLevel - 1, _boardPage - 1);
            }

            //And the Help screen, on whichever of its pages was asked for (#427)
            if (_help is int helpPage && !game.IsSplashUp)
            {
                _help = null;

                game.OpenHelp(helpPage);
            }

            //And the same for the result screen, over whatever is on the stack — the front end, unless "play"
            //above has just put a level under it. The figures are a plausible clear rather than zeros: the page
            //lays out its breakdown from them, and a screen of dashes would not be the screen being looked at.
            //
            //Held back until the TITLE CARD has gone. That began as a workaround: the splash hands over with a
            //Replace, and a Replace used to take off whatever was on top — so a result page pushed at boot was
            //silently swallowed by the main menu arriving a few seconds later (SplashPage.SECONDS; measured at
            //2.6 s, before #454's logo intro made it longer and #601 cut it back). Since #576 the splash replaces ITSELF and a page
            //over it survives, so the gate no longer protects the stack. It stays, here and on every startup
            //page above, because the page would otherwise open over the intro: the splash goes on updating and
            //drawing its black and its logo under a page (MenuPage.UpdatesUnderlying/DrawsUnderlying), and
            //reading its skip keys — a frame no player can reach, since no page can be opened before the menu.
            if (_result && !game.IsSplashUp)
            {
                _result = false;
                PresentResult(game);
            }
        }

        /// <summary>
        /// Fires the two testing-only celebration levers — <c>celebrate</c> (the victory fireworks) and
        /// <c>confetti</c> (the campaign's closing fall). Runs once, from <see cref="Step"/>, and both displays are
        /// asked for deliberately long: they have to outlast a scripted screenshot burst.
        /// </summary>
        /// <remarks>
        /// <b>This is called AFTER the <c>play</c>/<c>level=</c> level is built, and that order is the whole
        /// point.</b> Both levers used to fire at the end of <c>LoadContent</c>, where they were silently
        /// useless in combination with <c>play</c>: <c>BuildLevel</c> stops both displays on purpose (a
        /// celebration left in the air would burst over the opening seconds of a level nobody has played yet),
        /// so a run asking for a level AND a display got the level and an empty sky. Nothing reported the
        /// conflict — the display was started and then correctly stopped a frame later — so the combination
        /// read as "the fireworks are not visible from in-play", which is a finding rather than a broken lever,
        /// and #299 spent a round of screenshots on it before the <c>Stop</c> was found.
        /// <para>
        /// That combination is the only way to photograph either display from the PLAY camera, low behind the
        /// gun and looking up at the cluster through the ceiling's glass — which is the vantage a player is at
        /// when a level actually clears, and the one #299's occlusion showed up from. From the front end the
        /// camera orbits ABOVE the plate, so nothing of the glass is ever between the lens and a burst there.
        /// </para>
        /// </remarks>
        private void StartCelebrations(BS3DGame game)
        {
            if (_celebrate)
            {
                _celebrate = false;
                game.Fireworks?.Celebrate(90f);
            }

            //"confetti" asks for the campaign's ending. Clearing the last level of the set is the only thing
            //that normally starts it, and that cannot be scripted at all — it is `celebrate`'s reasoning one
            //step further along again, past even `blockdone`: a block milestone needs five levels played,
            //where this needs the whole campaign.
            if (_confetti)
            {
                _confetti = false;
                game.Confetti?.Celebrate(90f);
            }
        }

        /// <summary>
        /// The staged result page of <c>result</c>, <c>lost</c>, <c>stars=</c>, <c>blockdone</c> and
        /// <c>nextlocked=</c>, put over whatever is on the stack.
        /// </summary>
        private void PresentResult(BS3DGame game)
        {
            //The rating is 3 unless "stars=" said otherwise (#183). It exists because the trophy cup is a
            //function of the rating and there are four of them: three is the only one a test could reach,
            //so the other three cups could be neither photographed nor compared against each other. Same
            //reasoning as "blockdone" — a state a real play-through reaches only by being GOOD at the game.
            int testStars = Math.Clamp(_resultStars ?? 3, 0, Prazsky.BS3D.Scoring.StarRating.MAX);

            //"lost" flips it to a FAILED page (#238). The text is the real one GameplayScreen.Rules would
            //produce for the cluster reaching the line rather than a placeholder, because the whole reason
            //this exists is to look at the line the player is actually shown; a stand-in of a different
            //length would be a different layout. On a fail the page shows no stars and no breakdown, so the
            //figures below simply go unread — and newBest is refused outright, a lost level having no best.
            bool lost = _lost;

            //"nextlocked=" shuts the next level by one lock or the other (#397). Both figures are the widest
            //the note can actually get, for the "real entries" reason below: the price is the set's LAST gate
            //against a total just short of it, and the frontier named is the longest name standing ahead of
            //this page's level — ahead, because a frontier past the level just cleared is not a state play
            //can reach.
            bool lockedBySequence = string.Equals(_nextLocked, LOCK_SEQUENCE, StringComparison.OrdinalIgnoreCase);
            bool lockedByStars = _nextLocked != null && !lockedBySequence;
            int levelCount = game.LevelCount;
            int lastGate = levelCount > 0 ? game.LevelMinStars(levelCount - 1) : 0;
            int shownGate = lastGate > 0 ? lastGate : 150;
            int frontier = 0;

            for (int i = 1; i < Math.Min(12, levelCount); i++)
                if (game.LevelDisplayName(i).Length > game.LevelDisplayName(frontier).Length) frontier = i;

            game.PresentResult(new LevelResult(cleared: !lost,
                failureText: lost ? "The cluster reached the line." : null,
                stars: lost ? 0 : testStars, newBest: !lost,

                //96 matched + 24 orphaned below (#385): what a real clear's own field would have started
                //with, since emptying it is what matched and orphaned them in the first place. Chosen for
                //that agreement rather than picked separately, so the new "Next star" note projects off
                //the same clear the rest of the page is already photographed showing.
                levelBalls: 96 + 24,

                //Real entries rather than placeholders (#313), for the reason the failure text above is the
                //real one: this page exists to be PHOTOGRAPHED, and a stand-in of a different length is a
                //different layout. Two of the longest names in the shipped set, so what is looked at is the
                //widest the identity line and the Next button can actually get. Falls back off a set.
                levelName: levelCount > 12 ? game.LevelDisplayName(12) : "Trilithon", levelNumber: 13,
                hasNextLevel: true, nextLevelUnlocked: _nextLocked == null,
                nextLevelMinStars: lockedByStars ? shownGate : 1, totalStars: lockedByStars ? shownGate - 2 : testStars,
                nextLevelBeyondReach: lockedBySequence,
                frontierLevelNumber: frontier + 1,
                frontierLevelName: levelCount > 0 ? game.LevelDisplayName(frontier) : "Pinwheel",

                //The skip is offered on the photographed FAILURE and nowhere else (#347), which is where it
                //is offered in play — so "result lost" is a shot of the page a stuck player sees, button
                //and cost line and all, and "result" alone is still the clear's own page.
                canSkip: _lost,
                nextLevelName: levelCount > 13 ? game.LevelDisplayName(13) : "Girandole",
                campaignComplete: false,
                //"blockdone" borrows a real chapter's own name and place, so the milestone is looked at with a
                //real title in it rather than a placeholder that would read as one. Both DERIVED from the
                //same entry now: the number was a hardcoded 3 against a name taken from index 12, which was
                //the third block while blocks were five entries long and has been the SECOND since they grew
                //to ten — so the photographed page said "THE GALLERY" over "Block 3 of 9 complete", a heading
                //and a subtitle contradicting each other on the one screen this flag exists to look at.
                blockComplete: _blockDone,
                blockName: _blockDone ? (game.LevelBlockName(12) ?? "The Tower") : null,
                blockNumber: game.BlockCount > 0 ? game.LevelBlockNumber(12) : 3, blockCount: Math.Max(3, game.BlockCount),
                score: 4820, matchedBalls: 96, orphanedBalls: 24, streakBonus: 640,
                hadBudget: true, unusedShotsAwarded: 7, completionBonusAwarded: 350));
        }
    }
}
