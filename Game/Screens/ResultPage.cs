using BS3D.Online;
using BS3D.Audio;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using System.Collections.Generic;
using Prazsky.Core.Camera;
using Prazsky.BS3D.Scoring;
using System;
using System.Globalization;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;
using TextHorizontalAlignment = FontStashSharp.RichText.TextHorizontalAlignment;

namespace BS3D.Screens
{
    /// <summary>
    /// The end-of-level screen: the one place both ways a level ends (cleared, #56; failed, #58) land, and the
    /// one place a player is told which happened and chooses what to do about it.
    /// <para>
    /// No back, for a different reason from the main menu's: the level has already ended, so there is nothing
    /// to resume into and "back one level" has no meaning. Retry, Next Level and Main Menu are the only ways
    /// off it.
    /// </para>
    /// </summary>
    internal sealed class ResultPage : MenuPage
    {
        private Label _heading, _milestone, _levelLine, _newBest, _reason, _bareScore, _skipNote;

        //THE UPPER STACK CARRIES ITS OWN BACKING (#465), because it is the one part of this page that has no
        //plate and cannot have the scrim back. Everything from the heading down to "New best" stands on the
        //LIVE arena — #178 swapped the darkening scrim for the defocus and gave the breakdown a plate at the
        //same time, and the defocus is deliberately late (BLUR_DELAY_SECONDS, 16 s of sharp frame first since #721) so
        //the fireworks and the star reveal arrive in focus. Which means that for the first several seconds,
        //exactly when the page is READ, its own text is white type over whatever the level happened to be
        //played under.
        //
        //Brightness was the lever three times — #238's failure reason, #313's level line and #199's "New
        //best" each walked from MENU_TEXT_DIM up to MENU_TEXT_BODY after a capture — and _newBest's own note
        //already says why it cannot go a fourth: "Backdrop-dependent legibility is not a dimmer shade of
        //legible." The owner's report is the body white itself vanishing over a bright sky, and photographed
        //over the tropical beach it is not only the two small lines: the HEADING goes too, white on white
        //cloud, which is why the shadow is put under every line of the stack rather than under the two that
        //were named.
        //
        //A SHADOW AND NOT A PLATE, of the three answers the issue offered. A plate under the whole stack puts
        //a dark block over the very fireworks the page exists to show; a plate under only the body lines has
        //to be TWO plates, because the star row stands between the level line and "New best", and three dark
        //blocks on one page (the breakdown makes a third) read as furniture. The shadow is also the HUD's own
        //answer to the identical problem one screen over — "the text carries its backing and its shadow like
        //every other readout" (PlayHud's tutorial card) — so it is this game's existing vocabulary rather
        //than a fourth idea. Both were photographed before choosing, which is what the issue asked for.
        //
        //It was TWO LABELS from #465 to #521 — a dark copy in the same panel, offset, drawn first, and a
        //SyncShadows loop copying each line's text and visibility down onto its copy — because Myra draws a
        //label in one colour and has no outline. It is ONE label per line now, drawing its own backing
        //(ShadowedLabel): the same string a second time through FontStashSharp's Stroked glyph effect, which
        //dilates each glyph into an outline SHADOW_STROKE design pixels wide (scaled with the fonts), in the
        //shadow colour, under the type. One widget, one Text, one Visible, no panel and no loop. The cost is
        //what it was — one more string draw per line on a page with six of them and no animation in the type —
        //from glyph variants rasterised into the atlas once.
        private const int SHADOW_STROKE = 3;

        //Not black: a hard black edge on white type reads as a printing fault on a bright sky, where a
        //softened one reads as depth. Alpha rather than a grey, so what shows through is the scene's own
        //colour darkened rather than a grey halo the backdrop cannot tint.
        private static readonly Color TEXT_SHADOW = new(0, 0, 0, 190);

        /// <summary>The backing every line of the upper stack draws under itself — built where <see cref="MenuPage.Scaled"/>
        /// is valid, once per tree.</summary>
        private ShadowedLabel.Style ShadowStyle() => new(FontSystemEffect.Stroked, Scaled(SHADOW_STROKE), Point.Zero, TEXT_SHADOW);

        //One widget per slot rather than one string of glyphs: a Label's glyphs cannot be scaled, coloured or
        //timed apart from each other, and the reveal needs all three per star (#139).
        private readonly Label[] _starSlots = new Label[StarRating.MAX];
        private HorizontalStackPanel _starRow;
        private Label _matchedDetail, _matchedValue;
        private Label _orphanedDetail, _orphanedValue;
        private Label _streakValue;
        private Label _unusedDetail, _unusedValue;
        private Label _totalValue, _nextStarNote, _unlockNote;
        private Widget _breakdown;

        //The grid inside the breakdown's plate, held so its columns can be pinned to the final figures (#665)
        private Grid _breakdownGrid;

        //What each breakdown label reads once its row has landed, worked out once per result in Take (#665). One
        //description of the final text for the two things that need it: ApplyBreakdownReveal, which writes it, and
        //PinBreakdownColumns, which measures it — so the widths that are pinned are the widths that arrive.
        private string _matchedDetailText, _matchedValueText, _orphanedDetailText, _orphanedValueText;
        private string _streakValueText, _unusedDetailText, _unusedValueText, _totalValueText;

        private Button _retryButton, _nextLevelButton, _skipButton;

        //The Next button's own caption, so Refresh can put the level's name on it (#313). Held rather than
        //walked to off the Button, for the reason every other label on this page is held: the tree is built
        //once and written to many times, and a page that searched its own widgets for a label would be doing
        //that work on every ending.
        private Label _nextLevelLabel, _skipLabel;

        //Frozen at the end of the level. Held rather than read from the session on every showing - see
        //LevelResult for why that arithmetic has to be a snapshot.
        private LevelResult _result;

        public ResultPage(BS3DGame game) : base(game) { }

        internal override bool CanGoBack => false;

        /// <summary>
        /// <b>Yes, and this page is the only one that says so over a level</b> (#241). Every other page over a
        /// session freezes it, which is <see cref="MenuPage.UpdatesUnderlying"/>'s whole rule and is right for
        /// a pause: a game put down mid-move should be exactly where it was left. An ending is not that. The
        /// player has just watched a collapse go down the drain, and the frame it landed on used to stop dead
        /// — a cluster hanging perfectly still, balls halted in mid-fall — while the fireworks climbed and
        /// this page's own camera swung out around it, which read as the arena having been replaced by a
        /// photograph of itself.
        /// <para>
        /// The session decides what that permission actually buys: its world goes on, its rules do not. See
        /// <c>GameplayScreen.UpdateUnderResult</c>.
        /// </para>
        /// </summary>
        public override bool UpdatesUnderlying => true;

        /// <summary>
        /// <b>No</b> (#178). It dimmed hard once, on the pause screen's argument — a page over a stopped game
        /// — and that argument does not hold here: a pause is a game put down mid-move, where this is the game's
        /// own ending playing out. The fireworks are climbing, the camera is swinging out around the island and
        /// the cluster is still falling through the drain, and a scrim at
        /// <see cref="BS3DGame.PAUSE_SCRIM"/>'s weight put all of it behind smoked glass at the exact moment it
        /// was worth watching. What holds the numbers legible over a lit, moving arena instead is the frame
        /// going out of focus a few seconds in — see the region below.
        /// </summary>
        internal override bool DimsFrame => false;

        /// <summary>Takes the figures the level ended on. Called once, as the page is pushed.</summary>
        internal void Take(LevelResult result)
        {
            _result = result;

            CacheBreakdownText();

            Refresh();
        }

        #region The camera lets go of the gun

        //The level is over, so there is no longer any reason for the lens to sit behind a gun that cannot
        //fire: it eases back and out onto the front end's own slow orbit, and the arena turns behind the
        //numbers. That is the whole of the moment — the player has stopped playing and is being shown what
        //they did, and a frame frozen at eye level behind the barrel says nothing about it.
        //
        //Driven from HERE and not from the session, even though the session is still updating under this page
        //(#241): a pose has one writer or it has none, and the session's own UpdateCamera would be putting the
        //lens back behind the gun on every frame this one eased it away. Which is why the session does not run
        //that half of its frame at all while it is covered — the split is UpdateUnderResult's, and the camera
        //is the line it is drawn on.

        /// <summary>
        /// How long the release takes. Long enough to read as the camera being let go rather than as a cut,
        /// short enough that it is over well before anyone has finished reading the breakdown.
        /// </summary>
        private const float ORBIT_EASE_SECONDS = 2.5f;

        //THE GLANCE UP AT THE FIREWORKS (#430). The shells burst 44 to 122 units over the island, a ceiling
        //tuned against the PLAY camera - which looks up at the cluster, so anything lower falls behind it and
        //is never seen. The result page then hands the camera to the menu orbit, which looks level across the
        //arena at the trophy, and the show goes off above the frame. Two cameras, one burst height, and the
        //height was tuned for the other one.
        //
        //The answer is the first of the two the issue offers: the page looks UP now and then, rather than the
        //shells being brought down. Bringing them down would break the camera they were tuned for - they are
        //launched on the clear, while the gun's view is still up - and a player who has just won should be
        //shown the arena AND the sky, not made to choose.
        //SLOWER since #480: the owner found the 1.6 s rise read as the camera falling onto its back, a comfort
        //complaint rather than a framing one. The rise (and the matching fall) is doubled, which halves the
        //peak rate of the SmoothStep — 43 world units a second at the top of the ease became 22 — and the
        //period grows by the same 3.2 s so the level stretch between glances is what it was; the hold stays.
        private const float GLANCE_PERIOD = 12.7f;
        private const float GLANCE_RISE = 3.2f;
        private const float GLANCE_HOLD = 3.2f;

        //How far the aim point is lifted at the top of a glance, in world units. It was 46 until #722 - the burst
        //zone's own floor, which put the whole zone in shot (#430) - and 46 measured, from the camera's own base
        //position and target, as a pitch of 52 degrees at the top on a 60 degree lens: the top EDGE of the frame at
        //82, which is what the owner meant by "almost 90 degrees upward" and "as if we had fallen on our backs"
        //(the orbit's own pitch at rest is under 4 degrees, so it is the glance and only the glance). 28 is a pitch
        //of 38 degrees, and the lens widens by GLANCE_FOV_WIDEN_DEGREES as the aim rises so the frame's top edge
        //stays at 72 and most of the burst zone with it; what the smaller pitch costs is the highest shells, which
        //now leave the top of the frame. Chosen by eye on a bright and a dark scene against the candidates 34, 28 and
        //22 with the lens widened by 0, 8 and 10 degrees (the pictures are on the issue).
        private const float GLANCE_HEIGHT = 28f;

        //How much the lens widens at the top of a glance, in degrees, on the same smoothstep as the lift so there is
        //nothing new to feel: the zone fits at the smaller pitch, and the page's own text and the cup are unaffected.
        private const float GLANCE_FOV_WIDEN_DEGREES = 8f;

        private float _glanceClock;

        private float _orbitBlend;
        private Vector3 _fromPosition, _fromTarget;
        private float _fromFov, _fromRoll;

        /// <summary>
        /// Captures the pose the level ended on. The move is a plain Lerp away from it, so there is no first
        /// frame on which anything jumps — the same one-reversible-scalar shape precise aim and the drop
        /// cinematic use, and for the same reason.
        /// </summary>
        public override void Enter()
        {
            RecoilCamera camera = Game.Camera;

            _fromPosition = camera.BasePosition;
            _fromTarget = camera.BaseTarget;
            _fromFov = camera.FieldOfView;
            _fromRoll = camera.BaseRoll;
            _orbitBlend = 0f;

            //And the arena is sharp again on every arrival, for the reveal's reason: a retry lands back here
            //through this same page, and it owes the next ending the same few seconds in focus as the first
            _blurClock = 0f;

            //The reveal is timed from the page opening, so it restarts on every arrival — a retry that earned
            //a different rating has to show that rating being earned, not a row already sitting there.
            _revealClock = 0f;
            _starsAnnounced = 0;
            _revealSettled = false;

            ApplyStars();
            ApplyBreakdownReveal();

            //The boards (#547): whether this ending offers them to a player who has not opted in is decided now and
            //stands while the page is up — once a session, on a clear (see OnlineSession.TakeHint)
            _offerOnlineHint = _result.Cleared && Game.Online.TakeHint();
            _boardsClock = 0f;
            _boardsShownGeneration = -1;
            _boardsRevealed = false;
            ApplyBoards();

            //⚠ A CLEARED field has nothing hanging any more (#639). The orbit was framed for the level's map
            //when it was built (FrameOrbitFor from the session's install), so on a clear it went on aiming at
            //the middle of a cluster that had gone down the drain — a tour of an empty ceiling at the height
            //and stand-off of what used to hang under it. Framed as the bare island instead, the one the front
            //end flies with no map: the drain's mouth, the island round it and the cup and the fireworks over
            //it. Before AlignOrbitTo, which snaps the framing to its target, so the release eases straight onto
            //it rather than onto the old cluster's. A loss keeps the level's framing — its cluster still hangs.
            if (_result.Cleared) Game.Backdrop.FrameOrbitFor(null, 0f);

            //Started at the bearing the lens is already on, so the release is straight out from the arena
            Game.Backdrop.AlignOrbitTo(_fromPosition);

            //And the cup (#183), on the rating this ending earned. Presented from HERE rather than from
            //CheckLevelCleared, which is where the fireworks and the fanfare start, and the difference is the
            //point: those two answer the level ENDING, which happens a beat before this page exists, where the
            //cup answers the RATING — and the rating is not decided until ShowResultScreen has written the
            //record. A loss shows nothing, because LevelResult.Stars is zero on one.
            Game.Trophy?.Present(_result.Cleared ? _result.Stars : 0);
        }

        /// <summary>
        /// The cup belongs to this page and goes with it (#183). <c>BuildLevel</c> and <c>TearDown</c> already
        /// take it away on the two routes a player leaves by, but neither runs on the one route a <b>test</b>
        /// leaves by — the <c>result</c> argument puts this page over the front end with no session under it at
        /// all — and a cup left turning over the main menu would be exactly the fault the fireworks' own
        /// <c>TearDown</c> exists to prevent.
        /// </summary>
        public override void Leave()
        {
            base.Leave();
            Game.Trophy?.Hide();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            //The defocus's whole state: FrameBlur is a pure function of it, so the host reads what this frame
            //left rather than a value written from two places (see the region above)
            _blurClock += elapsed;

            if (!_revealSettled)
            {
                _revealClock += elapsed;

                AnnounceLandedStars();
                ApplyStars();
                ApplyBreakdownReveal();

                //One last pass has just run at or past the end, so the row is on its exact resting values
                if (_revealClock >= RevealTotalSeconds) _revealSettled = true;
            }

            //The plate's status line is up from the first frame an ending was sent, its rows come in after the reveal has
            //settled and punch in on their own clock (#547, #734); the signal in front of the status line keeps time
            //with it (#683)
            if (_boardsPlate != null && _boardsPlate.Visible)
            {
                _boardsClock += elapsed;
                _signal.Advance(elapsed);
            }
            ApplyBoards();

            Game.Backdrop.AdvanceOrbit(elapsed, out Vector3 position, out Vector3 target, out float fieldOfView);

            _orbitBlend = MathF.Min(1f, _orbitBlend + elapsed / ORBIT_EASE_SECONDS);

            //Smoothstep, whose derivative is zero at BOTH ends: the camera leaves at rest, because it was
            //standing still, and arrives at the orbit's own rate, because by then the blend has stopped
            //changing and the only thing still moving is the orbit itself. A linear blend would start with a
            //lurch and end with the camera visibly changing speed as it settled.
            float eased = MathHelper.SmoothStep(0f, 1f, _orbitBlend);

            RecoilCamera camera = Game.Camera;

            //The glance (#430), on top of the orbit's own aim and only while something is actually in the
            //air: a camera that pitches up at an empty sky reads as a fault, and the shells are finite.
            //Folded into the ORBIT's target before the release blend, so the first seconds - when the page is
            //still easing off the gun's pose - are not yanked upward as well.
            float glance = Glance(elapsed);
            target.Y += glance;

            camera.BasePosition = Vector3.Lerp(_fromPosition, position, eased);
            camera.BaseTarget = Vector3.Lerp(_fromTarget, target, eased);
            camera.FieldOfView = MathHelper.Lerp(_fromFov, fieldOfView, eased)
                + MathHelper.ToRadians(GLANCE_FOV_WIDEN_DEGREES) * (glance / GLANCE_HEIGHT);

            //Back to a level horizon: a level can end mid-tilt if a drop cinematic was running, and a result
            //screen read over a dutched frame reads as a fault
            camera.BaseRoll = MathHelper.Lerp(_fromRoll, 0f, eased);

            //Also what settles the recoil: the last shot's kick decays here rather than being frozen into the
            //pose the player is left looking at
            camera.Update(elapsed);
        }

        /// <summary>
        /// How far the aim is lifted towards the burst zone this frame (#430): level, then up over
        /// <see cref="GLANCE_RISE"/>, held for <see cref="GLANCE_HOLD"/>, and back down the same way - a
        /// look, not a pitch that stays. Zero whenever the sky is empty, and the clock is <b>reset</b> then
        /// rather than left running, so the first glance after a burst begins at its start instead of
        /// wherever the cycle happened to stand.
        /// <para>
        /// Smoothstep at both ends, for the reason the release blend above gives: a linear rise starts with
        /// a lurch, and a camera that lurches upward at the sky reads as the frame being dragged rather than
        /// as somebody looking.
        /// </para>
        /// </summary>
        private float Glance(float elapsed)
        {
            if (Game.Fireworks == null || !Game.Fireworks.Active)
            {
                _glanceClock = 0f;
                return 0f;
            }

            _glanceClock += elapsed;
            if (_glanceClock >= GLANCE_PERIOD) _glanceClock -= GLANCE_PERIOD;

            //Up, hold, down, then the rest of the period looking at the arena and its trophy.
            float t = _glanceClock;
            float up = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(t / GLANCE_RISE, 0f, 1f));
            float down = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp((t - GLANCE_RISE - GLANCE_HOLD) / GLANCE_RISE, 0f, 1f));

            return (up - down) * GLANCE_HEIGHT;
        }

        #endregion

        #region The arena goes out of focus

        //The frame behind this page is not dimmed (see DimsFrame) — so for the first few seconds the ending is
        //simply WATCHED: the shells go up, the camera lets go of the gun and swings out around the island, and
        //the page's own lines sit over a lit arena. Then the arena softens out of focus underneath them, until
        //what is left is colour and glow with no edges to compete with the text. The frame's own light is doing
        //it (PostProcessPipeline's defocus, mixed in before the tonemap curve), which is why a blurred firework
        //stays a glowing orb rather than a grey smudge.
        //
        //Driven from here, like the camera release and the star reveal above: the defocus is this page making
        //room for its own text, so it is timed from the page opening and belongs to the page. The session
        //under it goes on running (#241) but knows nothing about being read over.

        /// <summary>
        /// How long the arena stays sharp. It sits past both of the things this page does on arrival — the
        /// camera's release (<see cref="ORBIT_EASE_SECONDS"/>) and the last star landing
        /// (<see cref="RevealTotalSeconds"/>, about 2 s) — so nothing is blurred while it is still arriving,
        /// and the softening reads as the moment settling rather than as a transition out of it. Well past
        /// both since #639 (it was 3.4 s): the owner wanted the ending watched for a good while before it
        /// goes soft — a loss's field to be read, a clear's fireworks and cup to be seen. Twice that again
        /// since #721 (8 s, then 16): "about twice as long" on the next playtest, the ease untouched, so the
        /// frame is fully soft 32 s after the page opens.
        /// </summary>
        private const float BLUR_DELAY_SECONDS = 16f;

        /// <summary>
        /// How long the frame takes to go fully soft (#200: 4 s once, 4× that now). Slow on purpose, and the
        /// reason it had to get slower is in the shape of the ramp rather than its length: the blurred copy
        /// has fully taken over by PostProcessPipeline's <c>DEFOCUS_MIX_IN</c> (0.3), so the
        /// part the eye reads as the transition — one image arriving in place of another — all happens in
        /// the first third of the ease. At 4 s that was ~1.2 s of visible change, which read as a switch
        /// (#200); at 16 s the takeover alone breathes for ~5 s and the radius keeps growing after it.
        /// </summary>
        private const float BLUR_EASE_SECONDS = 16f;

        /// <summary>
        /// Since the page opened. A clock of its own rather than the reveal's, which latches
        /// (<see cref="_revealSettled"/>) long before this has started.
        /// </summary>
        private float _blurClock;

        /// <summary>
        /// Smoothstepped, so the frame leaves focus and arrives at full blur with the rate at zero at both
        /// ends: a linear ramp starts with a visible lurch out of a still image, and the eye reads the moment
        /// it stops as sharply as the moment it starts. <see cref="MathHelper.SmoothStep"/> clamps its own
        /// input, so the delay before it and the rest of the page's life after it both come out flat.
        /// </summary>
        internal override float FrameBlur =>
            MathHelper.SmoothStep(0f, 1f, (_blurClock - BLUR_DELAY_SECONDS) / BLUR_EASE_SECONDS);

        #endregion

        #region The stars arrive one at a time

        //A rating that is simply THERE when the page opens is a line of text; the same rating landing one star
        //at a time, each with its own cue, is the reward the level was played for (#139). The row is four slots
        //wide from the first frame and only the glyph, the colour and the scale change, so nothing under it
        //moves as the stars arrive — a breakdown that shuffled down the screen mid-reveal would undo the point.
        //
        //Driven from here for the reason the defocus is: the reveal is this page's own arrival playing out, so
        //it is timed from the page opening and restarts on every one. The session under it neither knows nor
        //cares that a rating is being struck over it.

        //A slot holds one glyph, so the shared chars are wanted as strings here. Built once rather than per
        //frame: ApplyStars runs every frame the page is up, and Label.Text takes a string.
        private static readonly string HOLLOW = STAR_HOLLOW.ToString();
        private static readonly string FILLED = STAR_FILLED.ToString();

        /// <summary>A beat before the first star, so the verdict above it is read first rather than competing.</summary>
        private const float REVEAL_DELAY_SECONDS = 0.45f;

        /// <summary>
        /// Between one star landing and the next: long enough to count them, short enough not to wait. A fixed
        /// pace since #613 — #158 made it one beat of the victory fanfare (0.51 s at the recording's 117.5 BPM),
        /// on the premise that every star lands inside the piece, and the owner's ruling is that it no longer
        /// does (the fanfare has had its moment before the stars begin). Between the pre-#158 0.30 s, which
        /// read as a rattle, and that beat, which read as a wait.
        /// </summary>
        private const float REVEAL_STEP_SECONDS = 0.4f;

        /// <summary>
        /// The chord tones each successive star sounds, as semitones over the chime's baked pitch — a major
        /// triad and then the octave: A5, C♯6, E6, A6. A <b>fixed</b> key since #613, chosen for how it sounds
        /// rather than taken from the fanfare (#158 shifted the run into the sounding piece's key every time).
        /// The chime's own baked A5 is the root, the register it was voiced for and the brightest a four-step
        /// run can reach inside the ±12 semitones the platform's pitch can express — and A major is also the
        /// shipped victory recording's own key, so if the two ever do overlap, the arpeggio is its tonic triad
        /// and consonant with it by construction. What #158 fixed stays fixed: an interval in no key at all
        /// (the old ~2.3-semitone step) is not coming back.
        /// </summary>
        private static readonly int[] CHIME_TRIAD = { 0, 4, 7, 12 };

        /// <summary>One star's own travel, from oversized to seated.</summary>
        private const float REVEAL_PUNCH_SECONDS = 0.34f;

        //The pad's own tick per star (#378) — light, and mostly the right motor's buzz, matching a medal
        //struck rather than a landing's thump or a shot's kick. The one trigger of the five that fires while
        //the gameplay screen is covered rather than active — see BS3DGame.Update's own allowed condition.
        private const float STAR_RUMBLE_LEFT = 0.15f;
        private const float STAR_RUMBLE_RIGHT = 0.35f;
        private const float STAR_RUMBLE_SECONDS = 0.15f;

        /// <summary>How large a star starts, as a multiple of its seated size.</summary>
        private const float REVEAL_START_SCALE = 2.4f;

        //Where the punch settles back FROM: it overshoots a little past its resting size so it lands rather
        //than merely stopping. Kept small - a big rebound reads as rubber, not as a medal being struck.
        private const float REVEAL_UNDERSHOOT = 0.92f;
        private const float REVEAL_SETTLE_FROM = 0.66f;

        /// <summary>
        /// When the last star has finished settling. Named for the star row specifically now that
        /// <see cref="RevealTotalSeconds"/> covers the breakdown's own reveal too (#479) — the breakdown's
        /// timing is built on top of this one, never the other way round.
        /// </summary>
        private static float StarRevealTotalSeconds =>
            REVEAL_DELAY_SECONDS + (StarRating.MAX - 1) * REVEAL_STEP_SECONDS + REVEAL_PUNCH_SECONDS;

        /// <summary>
        /// Past this, nothing on the page is still moving — which is what <see cref="_revealSettled"/> uses
        /// to stop touching either reveal. On a clear that shows a breakdown this runs past the last star into
        /// the sum settling behind it (#479, <see cref="BreakdownTotalRevealTime"/>); on a fail, or the built-in
        /// pyramid's bare-score fallback, there is no breakdown to wait for and the star row's own total stands.
        /// </summary>
        private float RevealTotalSeconds => _result.ShowsBreakdown
            ? BreakdownTotalRevealTime + BREAKDOWN_PUNCH_SECONDS
            : StarRevealTotalSeconds;

        private float _revealClock;
        private int _starsAnnounced;

        //Once the row has settled it is left alone. Writing a Label's Text and TextColor every frame for the
        //rest of the page's life is per-frame work for a row that has stopped changing — and Myra invalidates
        //a measure on a text write, so it is not free (BestPractices.md's per-frame hygiene). The page then
        //sits on the result screen doing nothing but the camera, which is what it did before this existed.
        private bool _revealSettled;

        /// <summary>
        /// One star's scale at <paramref name="progress"/> through its own punch. Nearly all the travel is
        /// spent in the first few frames — that is what makes it read as a star being <i>struck</i> into the
        /// slot rather than drifting down into it — and the last third eases the overshoot out so it comes to
        /// rest instead of stopping dead.
        /// </summary>
        private static float PunchScale(float progress)
        {
            if (progress < REVEAL_SETTLE_FROM)
            {
                //Ease-out cubic over the drop from oversized down through the resting size
                float k = progress / REVEAL_SETTLE_FROM;
                return MathHelper.Lerp(REVEAL_START_SCALE, REVEAL_UNDERSHOOT, 1f - MathF.Pow(1f - k, 3f));
            }

            return MathHelper.SmoothStep(REVEAL_UNDERSHOOT, 1f,
                (progress - REVEAL_SETTLE_FROM) / (1f - REVEAL_SETTLE_FROM));
        }

        /// <summary>When star <paramref name="index"/> (0-based) lands, in seconds since the page opened.</summary>
        private static float RevealTimeOf(int index) => REVEAL_DELAY_SECONDS + index * REVEAL_STEP_SECONDS;


        /// <summary>How far off the baked pitch star <paramref name="index"/> sounds — see <see cref="CHIME_TRIAD"/>.</summary>
        private static float ChimeSemitones(int index) => CHIME_TRIAD[Math.Min(index, CHIME_TRIAD.Length - 1)];

        /// <summary>
        /// Writes the whole row from <see cref="_revealClock"/>, so it is the clock and not a per-frame edit
        /// that decides what is on screen — which is what lets a resize rebuild the tree mid-reveal and have
        /// the new one come up exactly where the old one was.
        /// </summary>
        private void ApplyStars()
        {
            if (_starRow == null) return;

            //A failed level shows NO row rather than four hollow glyphs: a loss is not a rating of zero, and an
            //empty rating under "FAILED" reads as scorn.
            _starRow.Visible = _result.Cleared;
            if (!_result.Cleared) return;

            //The whole earned row takes the tier's colour, so the rating reads as one achievement at a glance
            //instead of as four glyphs that have to be counted
            Color tier = BS3DGame.StarTierColor(_result.Stars);

            for (int i = 0; i < _starSlots.Length; i++)
            {
                Label slot = _starSlots[i];
                bool landed = i < _result.Stars && _revealClock >= RevealTimeOf(i);

                if (!landed)
                {
                    slot.Text = HOLLOW;
                    slot.TextColor = BS3DGame.STAR_EMPTY;
                    slot.Scale = Vector2.One;
                    continue;
                }

                slot.Text = FILLED;
                slot.TextColor = tier;
                slot.Scale = new Vector2(PunchScale(MathF.Min(1f, (_revealClock - RevealTimeOf(i)) / REVEAL_PUNCH_SECONDS)));
            }
        }

        /// <summary>
        /// Plays one cue per star as it lands, at most one per star for the life of the page. A while loop
        /// rather than a per-frame equality test because a frame long enough to skip a whole step still owes
        /// the player every sound — on the class of machine the quality probe exists for, that frame happens.
        /// </summary>
        private void AnnounceLandedStars()
        {
            if (!_result.Cleared) return;

            while (_starsAnnounced < _result.Stars && _revealClock >= RevealTimeOf(_starsAnnounced))
            {
                Game.Audio?.PlayStarEarned(_starsAnnounced, _result.Stars, ChimeSemitones(_starsAnnounced));
                Game.Rumble?.Kick(STAR_RUMBLE_LEFT, STAR_RUMBLE_RIGHT, STAR_RUMBLE_SECONDS);
                _starsAnnounced++;
            }
        }

        #endregion

        #region The sum adds up (#479)

        //The breakdown used to be written once, whole, the instant Refresh ran — every row and the total
        //sitting there together from the first frame, which is exactly what read as a filled-in form rather
        //than a game telling you what you earned. It now lands the same way the row above it does: each
        //value arrives with the star row's own punch (PunchScale), staggered after the stars settle, from a
        //placeholder dash to its earned number — the caption and the plate are there from the first frame
        //(there is a sum coming), only the numbers themselves perform. And the grid they land in is at its final
        //size from the first frame (#665, PinBreakdownColumns), so a number arriving changes what is written into
        //a cell and never where the cells are.

        /// <summary>Held on screen before a value has landed — the row is there, its number is not yet.</summary>
        private const string PENDING_MARK = "—";

        /// <summary>A beat after the last star, so the rating is read before the arithmetic behind it starts.</summary>
        private const float BREAKDOWN_REVEAL_GAP_SECONDS = 0.3f;

        /// <summary>Between one row landing and the next.</summary>
        private const float BREAKDOWN_ROW_STEP_SECONDS = 0.14f;

        /// <summary>One row's own travel — quicker than a star's <see cref="REVEAL_PUNCH_SECONDS"/>: four of
        /// these plus the total run in the time one star does, and a punch that size on a line of body text
        /// would overshoot further than the text is tall.</summary>
        private const float BREAKDOWN_PUNCH_SECONDS = 0.22f;

        /// <summary>Matched, orphaned, streak bonus, shots unused — the total lands one step after the last.</summary>
        private const int BREAKDOWN_ROW_COUNT = 4;

        /// <summary>When row <paramref name="row"/> (0-based, in <see cref="BuildBreakdown"/>'s own order) lands.</summary>
        private float BreakdownRowRevealTime(int row) =>
            StarRevealTotalSeconds + BREAKDOWN_REVEAL_GAP_SECONDS + row * BREAKDOWN_ROW_STEP_SECONDS;

        /// <summary>When the total lands — one step past the last detail row, the sum arriving after its terms.</summary>
        private float BreakdownTotalRevealTime => BreakdownRowRevealTime(BREAKDOWN_ROW_COUNT);

        /// <summary>
        /// Writes one row from <see cref="_revealClock"/>: a dash and an empty detail before its own reveal
        /// time, the real figures and the star row's own punch from it. <paramref name="detail"/> is null for
        /// the streak-bonus row, which has no "count × worth" to show.
        /// </summary>
        private void WriteBreakdownRow(int row, Label detail, string detailText, Label value, string valueText)
        {
            float progress = (_revealClock - BreakdownRowRevealTime(row)) / BREAKDOWN_PUNCH_SECONDS;

            if (progress < 0f)
            {
                if (detail != null) detail.Text = string.Empty;
                value.Text = PENDING_MARK;
                value.Scale = Vector2.One;
                return;
            }

            if (detail != null) detail.Text = detailText;
            value.Text = valueText;
            value.Scale = new Vector2(PunchScale(progress));
        }

        /// <summary>
        /// Writes the whole breakdown from <see cref="_revealClock"/>, exactly as <see cref="ApplyStars"/>
        /// writes the row above it and for the identical reason: a resize can rebuild this page's tree
        /// mid-reveal, and the new tree has to come up wherever the clock already stands rather than at the
        /// reveal's first frame. A no-op on a fail (<see cref="LevelResult.ShowsBreakdown"/> is <c>Cleared</c>),
        /// where the grid is hidden and there is nothing here to write.
        /// <para>
        /// The total takes the earned rating's own colour (<see cref="BS3DGame.StarTierColor"/>) — safe here
        /// specifically because it sits on the breakdown's own plate, the one place on this page the
        /// greyscale-chrome rule already carries a licensed exception for a rating's own readout (see
        /// <c>STAR_EMPTY</c>'s remarks in <c>BS3DGame</c>). It is not a second accent; it is the same one,
        /// tying the number that earned the rating to the rating it earned.
        /// </para>
        /// </summary>
        private void ApplyBreakdownReveal()
        {
            if (_breakdown == null || !_result.ShowsBreakdown) return;

            WriteBreakdownRow(0, _matchedDetail, _matchedDetailText, _matchedValue, _matchedValueText);
            WriteBreakdownRow(1, _orphanedDetail, _orphanedDetailText, _orphanedValue, _orphanedValueText);
            WriteBreakdownRow(2, null, null, _streakValue, _streakValueText);
            WriteBreakdownRow(3, _unusedDetail, _unusedDetailText, _unusedValue, _unusedValueText);

            float totalProgress = (_revealClock - BreakdownTotalRevealTime) / BREAKDOWN_PUNCH_SECONDS;

            _totalValue.TextColor = BS3DGame.StarTierColor(_result.Stars);
            if (totalProgress < 0f)
            {
                _totalValue.Text = PENDING_MARK;
                _totalValue.Scale = Vector2.One;
            }
            else
            {
                _totalValue.Text = _totalValueText;
                _totalValue.Scale = new Vector2(PunchScale(totalProgress));
            }
        }

        /// <summary>
        /// Works out, once per result, what every breakdown label reads when its row has landed. The figures are
        /// known the moment the page opens, so nothing about the final text has to be discovered along the reveal
        /// (#665) — and writing them here rather than per frame also stops the reveal building a handful of
        /// strings on every tick it runs.
        /// </summary>
        private void CacheBreakdownText()
        {
            _matchedDetailText = $"{_result.MatchedBalls} × {ScoreKeeper.MatchedBallPoints}";
            _matchedValueText = ScoreText.Of(_result.MatchedBalls * ScoreKeeper.MatchedBallPoints);

            _orphanedDetailText = $"{_result.OrphanedBalls} × {ScoreKeeper.OrphanedBallPoints}";
            _orphanedValueText = ScoreText.Of(_result.OrphanedBalls * ScoreKeeper.OrphanedBallPoints);

            _streakValueText = ScoreText.Of(_result.StreakBonus);

            _unusedDetailText = _result.HadBudget
                ? $"{_result.UnusedShotsAwarded} × {ScoreKeeper.UnusedShotPoints}"
                : "—";
            _unusedValueText = ScoreText.Of(_result.CompletionBonusAwarded);

            _totalValueText = ScoreText.Of(_result.Score);
        }

        /// <summary>
        /// Fixes the breakdown grid's three columns at the width of their <b>final</b> contents, so the layout is
        /// the finished one from the first frame and the reveal only changes what is written into it (#665).
        /// <para>
        /// The columns were Auto, Auto and Part on a grid centred in its plate, so each one sized to whatever was in
        /// it that frame: a row landing widened the detail column (an empty label became "96 × 10"), the values
        /// column grew from a dash to "1 280", and the total in the heading font widened it again last — and each
        /// change re-centred the whole grid, so every caption slid sideways as the numbers arrived. Measured
        /// (1080p, <c>result stars=3</c>, one reveal): the captions' left edge moved 852, 809, 782 px and the
        /// values' right edge 1066, 1110, 1136 — 70 px each way.
        /// </para>
        /// <para>
        /// The widths come from the same strings <see cref="ApplyBreakdownReveal"/> writes, measured with the
        /// fonts the labels use; the pending dash is included in the values column so it cannot be the widest.
        /// The two notes that span the grid stay whole: the next-star line is measured as it will read, and the
        /// unlock note is a fixed width by construction (see <see cref="BuildBreakdown"/>), so a note wider than
        /// the columns lengthens the detail column — the one between the captions on the left and the right-aligned
        /// values — instead of overflowing its cell. Written by <see cref="Refresh"/>, so a rebuild (a resize) comes
        /// up pinned too.
        /// </para>
        /// </summary>
        private void PinBreakdownColumns()
        {
            if (_breakdownGrid == null || !_result.ShowsBreakdown) return;

            SpriteFontBase body = FontBody;
            SpriteFontBase heading = Game.MenuFontHeading;

            float caption = Widest(body, "matched", "orphaned", "streak bonus", "shots unused");
            float detail = Widest(body, _matchedDetailText, _orphanedDetailText, _unusedDetailText);
            float value = MathF.Max(
                Widest(body, PENDING_MARK, _matchedValueText, _orphanedValueText, _streakValueText, _unusedValueText),
                Widest(heading, PENDING_MARK, _totalValueText));

            //A note wider than the three columns lengthens the middle one, so captions and values stay at the edges
            float gaps = 2f * _breakdownGrid.ColumnSpacing;
            float notes = MathF.Max(Widest(_nextStarNote.Font, _nextStarNote.Text), _unlockNote.Visible ? _unlockNote.Width ?? 0 : 0);
            detail += MathF.Max(0f, notes - (caption + detail + value + gaps));

            _breakdownGrid.ColumnsProportions[0] = new Proportion(ProportionType.Pixels, caption);
            _breakdownGrid.ColumnsProportions[1] = new Proportion(ProportionType.Pixels, detail);
            _breakdownGrid.ColumnsProportions[2] = new Proportion(ProportionType.Pixels, value);
        }

        /// <summary>The widest of <paramref name="texts"/> in <paramref name="font"/>, rounded up to a whole pixel.</summary>
        private static float Widest(SpriteFontBase font, params string[] texts)
        {
            float widest = 0f;

            foreach (string text in texts)
                if (!string.IsNullOrEmpty(text)) widest = MathF.Max(widest, font.MeasureString(text).X);

            return MathF.Ceiling(widest);
        }

        #endregion

        protected override Widget BuildTree()
        {
            ShadowedLabel.Style shadow = ShadowStyle();

            VerticalStackPanel column = MenuColumn();

            //CLEARED / FAILED / CAMPAIGN COMPLETE — a title's size, like the main menu's name, because this is
            //the line the screen exists to state.
            _heading = new ShadowedLabel(shadow)
            {
                Text = string.Empty,
                Font = FontTitle,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 30),
            };
            column.Widgets.Add(_heading);

            //A finished block's own line, under the chapter's name in the heading and only on the milestone
            //(#184). It is where the block gets to be a place rather than a number: the heading says THE TOWER
            //and this says which of how many that was, so the player learns the campaign's shape from finishing
            //one of it rather than from counting tiles in the picker. Held back on every ordinary clear.
            _milestone = new ShadowedLabel(shadow)
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 26),
            };
            column.Widgets.Add(_milestone);

            //WHICH LEVEL THIS WAS (#313), and it is on every ending rather than only on a clear: "CLEARED" over
            //a lit arena told a player who had just spent several minutes on a level nothing about which one it
            //had been, and after a FAIL the question is asked at least as often. Under the milestone rather than
            //over it, because on the one ending that shows both, the heading is the CHAPTER's name and the
            //milestone elaborates the heading — the level is the smaller unit and follows them.
            //
            //MENU_TEXT_BODY at body size, and the colour is #238's ruling rather than a preference. It was
            //MENU_TEXT_DIM in the first cut and the capture settled it in one frame: the palette's own rule for
            //that grey is "asides, ALWAYS on a dark plate", and this page has neither a plate nor a scrim under
            //its heading — over a bright tropical sky the line came out the least legible thing on the screen,
            //which is the identical fault the failure reason line was photographed committing. Body brightness
            //keeps it subordinate to the verdict above without making it an aside nobody can read.
            _levelLine = new ShadowedLabel(shadow)
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 26),
            };
            column.Widgets.Add(_levelLine);

            //The star rating, straight under the verdict — the headline a player reads at a glance where the
            //score below is the arithmetic (#111). Set in Inter (FontStars), not the display face: Anton has
            //no ★/☆ glyphs at all, and FontStashSharp would draw blanks. Opened up so four glyphs read as a
            //rating rather than as a word — by the row's own spacing now that they are four widgets.
            _starRow = new HorizontalStackPanel
            {
                //Opened with the glyphs (#199): at 140 px each, the same 26 of spacing read as a word again —
                //the gaps have to grow with the type for four stars to read as a rating.
                Spacing = Scaled(34),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };

            for (int i = 0; i < _starSlots.Length; i++)
            {
                _starSlots[i] = new Label
                {
                    Text = HOLLOW,
                    Font = FontStars,
                    TextColor = BS3DGame.STAR_EMPTY,

                    //About its own centre, or a star punching in from 2.4× would swing in from its top-left
                    //corner and shoulder the row along instead of growing in place.
                    TransformOrigin = new Vector2(0.5f, 0.5f),
                };
                _starRow.Widgets.Add(_starSlots[i]);
            }

            column.Widgets.Add(_starRow);

            //Under the stars, and only when a best actually moved: a line that is always there says nothing.
            //Body, not small (#199): the one label on this screen that says the run beat every run before it
            //was the quietest thing on it, so it took a size that carries at play distance.
            //
            //AND NOW THE COLOUR TOO (#313), which #199 left at MENU_TEXT_DIM and is the third time this page has
            //been caught by the same rule. The palette's own line for that grey is "asides, ALWAYS on a dark
            //plate", and nothing above this page's breakdown has a plate or a scrim: over the neon city it read
            //perfectly and over the bright tropical sky it vanished, which is exactly how the failure reason
            //line (#238) and #313's own identity line each came to be photographed as the least legible thing on
            //the screen. Backdrop-dependent legibility is not a dimmer shade of legible.
            //
            //Secondary is now carried by POSITION and by RARITY — it is one short line under the rating, shown
            //only on the runs that earned it — rather than by a brightness that only works over half the
            //backdrops. It sits at the same MENU_TEXT_BODY as the milestone and the identity line above it, a
            //shade under the heading, which is what those two ranks are for.
            _newBest = new ShadowedLabel(shadow)
            {
                Text = "New best",
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };
            column.Widgets.Add(_newBest);

            //Which limit ran out, said plainly — only on a fail. Held back (Visible = false) on a cleared level.
            //
            //AT HEADING SIZE AND FULL BRIGHTNESS (#238), which is a correctness fix and not only a size bump. It
            //was FontBody at MENU_TEXT_DIM, and the palette's own rule for that grey is "asides, always on a dark
            //plate" — while this page, on a FAIL, has neither a plate nor a scrim. Photographed the day the
            //`lost` argument made the page reachable at all: dim grey 146 at body size, over the lit arena and
            //the ball cluster, came out the LEAST legible thing on the screen, under even the score line beside
            //it. It is the one sentence saying why the level ended, so it reads at the weight of the heading
            //above it; white is what the "FAILED" heading over the same backdrop already proves carries.
            _reason = new ShadowedLabel(shadow)
            {
                Text = string.Empty,
                Font = FontHeading,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };
            column.Widgets.Add(_reason);

            //The score reached, on a fail. The breakdown below is rightly held back — a failed level is awarded
            //no completion bonus and its partial rows would explain a total nobody is being offered — but the
            //total itself still has to be said, or the player is told they lost and nothing about how they did.
            _bareScore = new ShadowedLabel(shadow)
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 30),
            };
            column.Widgets.Add(_bareScore);

            //What a skip COSTS, on the page that offers one (#347). Its own label rather than a longer caption
            //on the button, for two reasons: a button carries a destination and not a sentence, and a Myra
            //button smaller than its label does not clip it — it lets it overflow, so the plate the player
            //sees and the rectangle the mouse hits quietly stop being the same thing (#348's trap). Outside
            //the breakdown grid, because the grid is a clear's and this line is a failure's.
            //⚠ THE PALETTE'S ASIDE GREY IS NOT AVAILABLE HERE, and the page's own notes say why: small text
            //over a lit, turning arena needs a backing of its own, which is what the breakdown has a plate
            //for. This line has no plate — it belongs to the FAILURE page, which has no grid to sit in — so
            //it is written in the body white instead. Photographed dim first over the tropical sky and it was
            //not readable at all.
            _skipNote = new Label
            {
                Text = string.Empty,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 20),
            };
            column.Widgets.Add(_skipNote);

            //The breakdown: caption · detail · value, the same three-column shape the settings screen uses, so a
            //number lines up under the number above it and reads at a glance. A plate behind it, because small
            //text over a lit, turning arena needs a backing of its own — and since #178 there is no scrim under
            //it at all, so the plate is the whole of what holds these numbers.
            _breakdown = Plate(BuildBreakdown());

            //Cut to the same width as the entries below it (#179). It is the one plate in the game that stands
            //BESIDE buttons rather than under them (see BS3DGame.Plate), and left to size itself it wrapped a
            //few short numbers — a panel visibly narrower than every control under it, in the same centred
            //stack, which read as a fourth button that could not be pressed. Myra applies MinWidth to the
            //measured size with the padding already in, so at the minimum the plate's edges land exactly on the
            //buttons'. It was chosen over Width so the unlock note could run longer than the column — and what
            //that bought in play was a sentence running out past the plate's right edge (#397). The note wraps
            //inside the plate now; see BuildBreakdown.
            _breakdown.MinWidth = ColumnWidth;

            column.Widgets.Add(_breakdown);

            //Built in this order — Retry, Next Level, Main Menu — which is also FAILED's own order and so
            //needs no runtime rearranging on a loss. A CLEAR reorders the two in Refresh (#263): Retry stays
            //a field for exactly that, not for anything read off it here.
            column.Widgets.Add(_retryButton = MenuButton("Retry", Game.RetryLevel));

            //Absent rather than disabled when there is no next level to go to or the score did not clear the
            //gate. Retry stays: it is the one thing that always makes sense at a level's end.
            //
            //Its caption is written in Refresh (#313), because it NAMES the level it leads to: the player used
            //to commit to starting one with no idea what it was called until it was already loading. The text
            //here is only what the button reads as before any result has been presented.
            column.Widgets.Add(_nextLevelButton = MenuButton("Next Level", Game.AdvanceLevel, out _nextLevelLabel));

            //The campaign's relief valve, under Retry on a failure and absent everywhere else (#347). Under
            //rather than over: retrying is what the player should try first, and a skip that sat above it
            //would be offering to spend their chapter's only skip before they had lost twice. It names the
            //level it leads to for #313's reason, and what it COSTS is said by the note above the buttons —
            //a button cannot carry a sentence.
            column.Widgets.Add(_skipButton = MenuButton("Skip Level", Game.SkipLevel, out _skipLabel));

            column.Widgets.Add(MenuButton("Main Menu", Game.EndSessionAndReturnToMainMenu));

            //The online boards (#547), BESIDE the column rather than in it: the column is the ending's own and stands
            //close to its height budget, while the width either side of it is empty at every landscape aspect — at 4:3
            //each side still holds about 940 design units, and the plate is BOARD_WIDTH of them.
            _boardsPlate = BuildBoards();

            return ScreenRoot(column, _boardsPlate);
        }

        /// <summary>
        /// The score breakdown grid: each row a caption, the detail that earned it, and the points it was
        /// worth. The labels are kept on fields so <see cref="Refresh"/> can write the numbers onto them.
        /// </summary>
        private Grid BuildBreakdown()
        {
            Grid grid = new()
            {
                ColumnSpacing = Scaled(48),
                RowSpacing = Scaled(12),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _breakdownGrid = grid;

            //Auto, Auto and Part only until the first Refresh: PinBreakdownColumns fixes all three at the width of
            //their final contents, because a column that sizes to what is in it that frame slides the whole
            //centred grid sideways as each row lands (#665)
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));   //caption
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));   //detail (count × worth)
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Part));   //value, right-aligned by the cell

            AddRow(grid, 0, "matched", out _matchedDetail, out _matchedValue);
            AddRow(grid, 1, "orphaned", out _orphanedDetail, out _orphanedValue);
            AddRow(grid, 2, "streak bonus", out _, out _streakValue);
            AddRow(grid, 3, "shots unused", out _unusedDetail, out _unusedValue);

            //A hairline rule was tried here (#479) — under the four rows, over the total — and dropped. Three
            //shapes of it (an empty Panel, a Pixels-sized row under it, an empty-then-space-text Label with
            //the same background and row) all measured, laid out and positioned correctly by every other
            //signal on the page — the total shifted down to make room for each attempt exactly as asked — and
            //not one of them painted a single pixel. Whatever Myra (1.6.3) needs of a Background-only line
            //that carries no visible content, this was not it, and the extra top margin below is what is left
            //of the rule's own spacing rather than a stray number: the total still wants a beat of air before
            //it, rule or no rule.
            //
            //The total sits on its own line under the rows — in the value column, so it lines up under the row
            //totals — in the heading weight, so it reads as the answer rather than as another line of the sum.
            //TransformOrigin centres its own punch (#479, see ApplyBreakdownReveal): it lands like a star does,
            //oversized and settling, rather than simply appearing.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _totalValue = new Label
            {
                Text = string.Empty,
                Font = Game.MenuFontHeading,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                TransformOrigin = new Vector2(0.5f, 0.5f),
                Margin = ScaledThickness(0, 24, 0, 0),
            };
            Grid.SetColumn(_totalValue, 2);
            Grid.SetRow(_totalValue, 4);
            grid.Widgets.Add(_totalValue);

            //What the NEXT star of THIS level would cost (#385), an aside directly under the total — this
            //clear's own rating first, the campaign's gate (below) second. Absent rather than zero at four
            //stars: there is nothing above the top to project towards, and a row reading "+0" would look like
            //an error rather than a ceiling.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _nextStarNote = new Label
            {
                Text = string.Empty,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_nextStarNote, 0);
            Grid.SetColumnSpan(_nextStarNote, 3);
            Grid.SetRow(_nextStarNote, 5);
            grid.Widgets.Add(_nextStarNote);

            //The campaign's gate, as an aside under that: why the NEXT level is shut, shown only when it is —
            //which is also exactly when the Next Level button is absent, so the note is what explains the
            //absence. Spanning the grid, because it is a sentence about the campaign rather than another line
            //of the sum.
            //
            //⚠ WRAPPED, AGAINST THE PLATE'S CONTENT WIDTH (#397). Unwrapped, the price sentence ran out past the
            //plate's right edge in play ("…you have 306" cut off), and the sequence lock's sentence names a
            //level, so it is longer still. A Myra label wraps only against a width it is given, and the plate
            //this sits in is the one cut to the menu column, whose content width is therefore known.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _unlockNote = new Label
            {
                Text = string.Empty,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                Wrap = true,
                Width = Game.MenuColumnPlateContentWidth,
                TextAlign = TextHorizontalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_unlockNote, 0);
            Grid.SetColumnSpan(_unlockNote, 3);
            Grid.SetRow(_unlockNote, 6);
            grid.Widgets.Add(_unlockNote);

            return grid;
        }

        private void AddRow(Grid grid, int row, string caption, out Label detail, out Label value)
        {
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            Label captionLabel = new()
            {
                Text = caption,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(captionLabel, 0);
            Grid.SetRow(captionLabel, row);
            grid.Widgets.Add(captionLabel);

            detail = new Label
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(detail, 1);
            Grid.SetRow(detail, row);
            grid.Widgets.Add(detail);

            value = new Label
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,

                //Centres the punch ApplyBreakdownReveal drives it with (#479) — the same reason the star
                //slots set it, and for the identical effect: the number pops in place rather than growing
                //from a corner.
                TransformOrigin = new Vector2(0.5f, 0.5f),
            };
            Grid.SetColumn(value, 2);
            Grid.SetRow(value, row);
            grid.Widgets.Add(value);
        }

        internal override void Refresh()
        {
            //The tree may not exist yet: the page is only built when it is first shown
            if (_heading == null) return;

            //Brightness, not colour: "FAILED" is the same grey as "CLEARED", and the reason below is what tells
            //them apart — see the palette comment for why the chrome carries no hue. The star row below is the
            //one deliberate exception on this page, and the reason it is one is recorded there.
            //A finished BLOCK takes the chapter's own name as the heading (#184) — "THE TOWER" where an ordinary
            //clear says "CLEARED". It is the milestone's whole presentation problem in one line: a heading that
            //said "BLOCK COMPLETE" would be the same shape of change as CAMPAIGN COMPLETE was, a different string
            //over an identical page, and the thing worth telling the player is WHICH chapter they just closed.
            //Upper-cased here rather than authored in caps, so a set can name its blocks in prose.
            _heading.Text = _result.CampaignComplete ? "CAMPAIGN COMPLETE"
                : _result.BlockComplete && !string.IsNullOrWhiteSpace(_result.BlockName) ? _result.BlockName.ToUpperInvariant()
                : _result.Cleared ? "CLEARED" : "FAILED";

            //And under it, where that chapter sat. Only on the milestone: on any other ending the line would be
            //stating a fact about a block nobody has just finished.
            _milestone.Text = _result.BlockComplete
                ? $"Block {_result.BlockNumber} of {_result.BlockCount} complete"
                : string.Empty;
            _milestone.Visible = _result.BlockComplete;

            //And which level it was (#313) — on a clear, on a fail and on a milestone alike. Hidden only when
            //there is no name to say at all, which off a level set is the built-in pyramid.
            _levelLine.Text = _result.LevelLine;
            _levelLine.Visible = _levelLine.Text.Length > 0;

            //Stars only on a clear, and written from the reveal clock rather than set here — see ApplyStars
            ApplyStars();

            _newBest.Visible = _result.Cleared && _result.NewBest;

            //The reason and the score reached are only on a fail. Hidden rather than left blank on a clear, so
            //they take no space. The reason is worded where the result is built and not where the loss was
            //detected: a message built at the point of detection carries the figures that were convenient
            //there — which is how "a ball at -5,58 <= -5,50" once reached a player.
            _reason.Text = _result.FailureText;
            _reason.Visible = _result.Failed;

            _bareScore.Text = _result.ShowsBareScore
                ? $"Score {ScoreText.Of(_result.Score)}"
                : string.Empty;
            _bareScore.Visible = _result.ShowsBareScore;

            //And what the button under the buttons would cost, said before it is pressed rather than
            //discovered afterwards by its absence (#347). Shown on exactly the condition the button is, so the
            //two can never disagree about whether a skip is on offer.
            _skipNote.Text = _result.CanSkip ? "Skipping spends this chapter's one skip" : string.Empty;
            _skipNote.Visible = _result.CanSkip;

            _breakdown.Visible = _result.ShowsBreakdown;

            if (_result.ShowsBreakdown)
            {
                //Only below four stars — NextStarScore's own -1 says there is nothing left to project towards,
                //the same sentinel LevelResult itself uses, so this can never show a note the total contradicts.
                bool hasNextStar = _result.NextStarScore >= 0;
                _nextStarNote.Text = hasNextStar
                    ? $"Next star at {ScoreText.Of(_result.NextStarScore)} (+{ScoreText.Of(_result.NextStarGap)})"
                    : string.Empty;
                _nextStarNote.Visible = hasNextStar;

                //Only when the road ahead is actually shut — which is also when the Next Level button below
                //is absent, so this line is the absence explained rather than a number always on display.
                //It is a clear's note and only a clear's, because it is a row of the breakdown grid and the
                //grid is hidden on a failure; the failure page's own version is _skipNote, outside the grid.
                //
                //⚠ AND #347's SEQUENCE RULE DOES REACH IT (#397). This said the opposite — "after a clear the
                //frontier has already moved past this level" — which holds only when the level cleared WAS the
                //frontier. A replay of a level cleared out of order leaves the frontier far behind, and the note
                //quoted the star gate anyway: "unlocks at 150 ★ — you have 306". LevelResult.UnlockNote names
                //whichever lock actually holds.
                string unlockNote = _result.UnlockNote;
                _unlockNote.Text = unlockNote;
                _unlockNote.Visible = unlockNote.Length > 0;

                //The columns at their final widths before anything of the reveal is written (#665), and after the
                //two notes above, whose widths they take account of
                PinBreakdownColumns();
            }

            //The rows and the total are the reveal's to write, not this method's (#479) — see
            //ApplyBreakdownReveal, which this also primes immediately so nothing is a blank frame before
            //Update's first tick (ApplyStars' own reason, above).
            ApplyBreakdownReveal();

            //Next Level is shown only when the level was cleared, there is another entry to go to AND the
            //campaign opens it. Absent, not disabled, when any of that fails — a greyed-out button over a
            //frozen frame is a thing the player cannot do, which reads as the game being broken rather than
            //as the campaign asking for something (the note above says what, in words).
            _nextLevelButton.Visible = _result.Cleared && _result.HasNextLevel && _result.NextLevelUnlocked;

            //Named, not merely offered (#313). Written whether or not it is visible: the caption is a function
            //of the result and nothing else, so there is no state in which the button is shown carrying the
            //previous level's successor.
            _nextLevelLabel.Text = _result.NextLevelLabel;

            //The skip, on the same terms: absent rather than disabled, since a greyed button over a frozen
            //frame reads as a broken game (the note above says in words why it is not there). Its own
            //condition is the whole of the rule — CanSkip is false on a clear, on a level already finished,
            //on the last entry and on a chapter whose one skip is spent — so this line adds nothing to it.
            _skipButton.Visible = _result.CanSkip;
            _skipLabel.Text = _result.SkipLabel;

            //And PRIMARY on a clear (#263): the player's obvious next step after winning is the next level,
            //not replaying the one they just beat, so the most likely action belongs in the most prominent
            //slot rather than the least. A fail leaves the two exactly where they were built — Retry first,
            //Next Level absent — so this only ever has to swap them, never a third arrangement.
            ReorderPrimaryAction();

            _boardsShownGeneration = -1;
            ApplyBoards();
        }

        #region The online boards (#547)

        //The plate is at most this wide — ranks, nicknames and scores in the small face, sixteen characters of a name
        //with room — and gives way to the width actually beside the column (see BuildBoards): at 16:9 there are about
        //1420 design units either side of it; at 4:3 about 940, and the first cut, which assumed the full width would
        //always fit, was photographed laid across the score breakdown.
        //⚠ CENTRED IN THAT STRIP since #701, the same free width towards the column and towards the edge. It was pinned
        //to the right edge with a margin capped at 150 units, so every unit a wider screen gave went between the column
        //and the plate: centred at 4:3, and leaning ever further right on the owner's 3840x1600. The two floors below
        //are the least it may stand off either side before it narrows.
        private const int BOARD_WIDTH = 780;
        private const int BOARD_MIN_WIDTH = 520;
        private const int BOARD_MIN_EDGE_MARGIN = 24;
        private const int BOARD_COLUMN_GAP = 24;
        private const int BOARD_PADDING = 56;
        private const int BOARD_SECTION_GAP = 40;
        private int _boardWidth;

        //The signal in front of the status line (#683): the small face's whole line height, so it scales with the
        //letters beside it at every layout - at their cap height (0.72) it was some twenty pixels at 1080p and easy to
        //miss, which is the one thing it exists not to be - and a gap of design units between them
        private const float SIGNAL_SIZE = 1.0f;
        private const int SIGNAL_GAP = 22;

        private Panel _boardsPlate;
        private Label _boardsStatus;
        private HorizontalStackPanel _statusRow;
        private OnlineSignal _signal;
        private BoardView _month, _allTime;
        private bool _offerOnlineHint;
        private float _boardsClock;
        private int _boardsShownGeneration = -1;

        //The reveal's state the plate was last written against (#734): the rows are held until it settles, so a flip
        //rewrites the plate even when the service has said nothing new
        private bool _boardsRevealed;

        private Panel BuildBoards()
        {
            //The strip beside the column, in pixels at the layout in force, and the plate centred in it (#701): as wide as
            //it may be with the larger of the two floors on both sides, never under its own floor, and the margin to the
            //edge half of what is left. The column's plate is cut to ColumnWidth (MenuColumnPlateContentWidth), so its
            //right edge is the strip's left one; where the strip is narrower than the plate at its floor, the edge
            //margin holds its floor and the plate is what crosses the gap, as it always did
            int beside = (Game.GraphicsDevice.PresentationParameters.BackBufferWidth - ColumnWidth) / 2;
            int padding = 2 * Scaled(BOARD_PADDING);
            int sideFloor = Math.Max(Scaled(BOARD_COLUMN_GAP), Scaled(BOARD_MIN_EDGE_MARGIN));
            _boardWidth = Math.Clamp(beside - padding - 2 * sideFloor, Scaled(BOARD_MIN_WIDTH), Scaled(BOARD_WIDTH));
            int margin = Math.Max((beside - _boardWidth - padding) / 2, Scaled(BOARD_MIN_EDGE_MARGIN));

            //Held to the width worked out above: a stack sizes to its widest child, and one long line — the first cut's
            //"Every clear since the boards began" — widened the whole plate back across the column at 4:3
            VerticalStackPanel stack = new() { Spacing = Scaled(10), Width = _boardWidth, ClipToBounds = true };

            _month = new BoardView(FontBody, FontSmall, Game.MenuFontPromptSmall, Scaled, OnlineSession.RESULT_BOARD_ROWS, _boardWidth);
            _allTime = new BoardView(FontBody, FontSmall, Game.MenuFontPromptSmall, Scaled, OnlineSession.RESULT_BOARD_ROWS, _boardWidth);
            _allTime.Root.Margin = ScaledThickness(0, BOARD_SECTION_GAP, 0, 0);
            stack.Widgets.Add(_month.Root);
            stack.Widgets.Add(_allTime.Root);

            //What the boards cannot show yet or at all — sending, offline, refused, or the offer to a player who has not
            //opted in — in the plate's own small print, behind the signal that says whether anything is happening
            //(#683): a still "Sending your score..." read as a stuck line
            int signalSize = (int)(FontSmall.LineHeight * SIGNAL_SIZE);
            int signalGap = Scaled(SIGNAL_GAP);
            _signal = new OnlineSignal(signalSize);
            _boardsStatus = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                Wrap = true,
                Width = _boardWidth - signalSize - signalGap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _statusRow = new HorizontalStackPanel { Spacing = signalGap };
            _statusRow.Widgets.Add(_signal);
            _statusRow.Widgets.Add(_boardsStatus);
            stack.Widgets.Add(_statusRow);

            Panel plate = Plate(stack);
            plate.HorizontalAlignment = HorizontalAlignment.Right;
            plate.Padding = ScaledThickness(BOARD_PADDING, BOARD_PADDING);
            plate.Margin = new Myra.Graphics2D.Thickness(0, 0, margin, 0);
            plate.Visible = false;
            return plate;
        }

        /// <summary>
        /// Writes the plate from what the game has heard (#547) — only when that changed or the plate is coming into
        /// view, because a Label's Text setter re-measures. Never waits for anything: a board that is slow is a board
        /// that is late, not a page that is.
        /// </summary>
        private void ApplyBoards()
        {
            if (_boardsPlate == null) return;

            //THIS ending's plate or none (#707, #716): the boards when this ending was handed to the service - a clear,
            //or a loss sent as an unfinished attempt - and the offer to opt in on a clear. Online.Result is otherwise the
            //answer to an EARLIER ending, which is how a loss came to stand under the last clear's "Sending your
            //score...". And written against the widget's own Visible, every frame: a flag kept beside it could disagree
            //with it (a rebuilt tree's new plate, a page entered again), and the plate then outlived its result.
            bool online = _result.Submitted && Game.Online.Enabled;

            //⚠ The plate of an ending that went to the service STANDS FROM THE PAGE'S FIRST FRAME (#734). It waited for the
            //reveal to settle, so "Sending your score..." came up two to three seconds after the page - the one moment it
            //exists for - and a player who left in those seconds (Retry, Next Level, the menu) never saw it. What waits
            //for the reveal is the boards' rows and the offer to opt in, which are things to read and would compete with
            //the stars for the eye; the status row is a small plate beside the column and does not
            bool wanted = online || (_revealSettled && _result.Cleared && _offerOnlineHint);

            if (wanted != _boardsPlate.Visible)
            {
                _boardsPlate.Visible = wanted;
                _boardsClock = 0f;
                _boardsShownGeneration = -1;
            }

            if (!wanted) return;

            //The rows come in the moment the reveal settles, and the player's own lines punch in on a clock started then,
            //not at the page's first frame, which is long gone by then
            if (_revealSettled != _boardsRevealed)
            {
                _boardsRevealed = _revealSettled;
                _boardsClock = 0f;
                _boardsShownGeneration = -1;
            }

            //The player's own lines punch in over their first moments, on the plate's own clock
            float punch = PunchScale(MathF.Min(1f, _boardsClock / REVEAL_PUNCH_SECONDS));
            _month.You.Scale = _allTime.You.Scale = new Vector2(punch);

            if (_boardsShownGeneration == Game.Online.ResultGeneration) return;
            _boardsShownGeneration = Game.Online.ResultGeneration;

            if (!online)
            {
                ShowSections(false);

                //The switch ON and nothing sent is a build with no score server (a local build: only a release names one,
                //see OnlineScores.DefaultServer), which is by design and exactly what "I no longer see the plate" looks
                //like to a player who switched from a release to one built by hand (#734). "Off" would tell them to turn
                //on what is already on
                SetStatus(Game.Online.IsOn
                    ? "Online scores are on, but this build has no score server to send to."
                    : "Online leaderboards are off. Turn on Online scores in Settings to see where your clears rank.", signal: null);
                return;
            }

            //What this ending is to the boards: a clear, or an unfinished attempt (#716)
            string what = _result.Cleared ? "clear" : "attempt";

            OnlineAnswer? answer = Game.Online.Result;

            if (answer is not { Outcome: OnlineOutcome.Accepted } accepted)
            {
                ShowSections(false);

                //No answer yet is a request in flight, and the signal says so by moving (#683); an answer that is
                //not a delivery leaves it still
                SetStatus(answer?.Outcome switch
                {
                    //Not "Offline": the outcome is the score SERVICE not answering, and since #691 that includes a
                    //network that works but reaches something else (the owner, online, read "Offline" while a stale
                    //DNS record sent the game to a web host). Say what is known
                    OnlineOutcome.Offline => $"The score server did not answer. This {what} is saved and goes out with your next score.",
                    OnlineOutcome.Refused => $"The score server did not take this {what}.",
                    _ => "Sending your score...",
                }, answer == null ? OnlineSignal.SignalMode.Working : OnlineSignal.SignalMode.Idle);
                return;
            }

            //Taken, but the stars are still landing: the rows wait for them (above), and the line says what is true now
            //- that this ending is safe with the service, so the player may leave - in the gold of a thing done
            if (!_revealSettled)
            {
                ShowSections(false);
                SetStatus($"Your {what} was sent.", OnlineSignal.SignalMode.Done);
                return;
            }

            ShowSections(true);

            _month.Fill("THIS MONTH", BoardView.MonthName(Game.Online.ResultMonthBoard?.Month), Game.Online.ResultMonthBoard,
                accepted.MonthRank, accepted.MonthTotal, unfinished: !_result.Cleared);
            _allTime.Fill("ALL TIME", BoardView.AllTimePeriod, Game.Online.ResultAllTimeBoard,
                accepted.AllTimeRank, accepted.AllTimeTotal, unfinished: !_result.Cleared);

            //Accepted: the boards still loading keep the signal moving; once they are in, it turns gold for the line
            //that is left (a personal best), and the line goes when there is nothing to add to the boards themselves
            bool loading = Game.Online.ResultMonthBoard == null || Game.Online.ResultAllTimeBoard == null;
            string best = _result.Cleared ? "A personal best on this level." : "Your best attempt at this level so far.";
            SetStatus(loading ? "Loading the boards..." : accepted.PersonalBest ? best : string.Empty,
                loading ? OnlineSignal.SignalMode.Working : OnlineSignal.SignalMode.Done);
        }

        private void ShowSections(bool visible)
        {
            _month.Root.Visible = visible;
            _allTime.Root.Visible = visible;
        }

        /// <summary>
        /// The status line under the boards and the signal in front of it (#683): the line is shown only when it says
        /// something, and the signal only beside a line about the online client - not beside the offer to opt in.
        /// </summary>
        private void SetStatus(string text, OnlineSignal.SignalMode? signal)
        {
            _boardsStatus.Text = text;
            _statusRow.Visible = text.Length > 0;

            _signal.Visible = signal != null;
            if (signal is OnlineSignal.SignalMode mode) _signal.Show(mode);
        }

        #endregion

        /// <summary>
        /// Swaps Retry and Next Level so the button the player actually wants next leads the column — see the
        /// call site. Reads <see cref="_nextLevelButton"/>'s own <see cref="Widget.Visible"/>, so it can never
        /// disagree with what is actually offered: a clear with the gate still shut leaves Next Level absent
        /// and Retry stays primary, exactly as a fail does. Idempotent — a Refresh that runs again on the same
        /// outcome (a re-entered page, a retried star reveal) finds the pair already in the wanted order and
        /// does nothing.
        /// </summary>
        private void ReorderPrimaryAction()
        {
            if (_retryButton.Parent is not Container buttons) return;

            var widgets = buttons.Widgets;
            int retryIndex = widgets.IndexOf(_retryButton);
            int nextIndex = widgets.IndexOf(_nextLevelButton);

            if (_nextLevelButton.Visible && nextIndex > retryIndex)
            {
                widgets.Remove(_nextLevelButton);
                widgets.Insert(retryIndex, _nextLevelButton);
            }
            else if (!_nextLevelButton.Visible && retryIndex > nextIndex)
            {
                widgets.Remove(_retryButton);
                widgets.Insert(nextIndex, _retryButton);
            }
        }
    }
}
