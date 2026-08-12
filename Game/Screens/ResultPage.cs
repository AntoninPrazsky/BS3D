using BS3D.Audio;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.Core.Camera;
using Prazsky.BS3D.Scoring;
using System;
using System.Globalization;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

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
        private Label _heading, _newBest, _reason, _bareScore;

        //One widget per slot rather than one string of glyphs: a Label's glyphs cannot be scaled, coloured or
        //timed apart from each other, and the reveal needs all three per star (#139).
        private readonly Label[] _starSlots = new Label[StarRating.MAX];
        private HorizontalStackPanel _starRow;
        private Label _matchedDetail, _matchedValue;
        private Label _orphanedDetail, _orphanedValue;
        private Label _streakValue;
        private Label _unusedDetail, _unusedValue;
        private Label _totalValue, _unlockNote;
        private Widget _breakdown;
        private Button _nextLevelButton;

        //Frozen at the end of the level. Held rather than read from the session on every showing - see
        //LevelResult for why that arithmetic has to be a snapshot.
        private LevelResult _result;

        public ResultPage(BS3DGame game) : base(game) { }

        internal override bool CanGoBack => false;

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

            Refresh();
        }

        #region The camera lets go of the gun

        //The level is over, so there is no longer any reason for the lens to sit behind a gun that cannot
        //fire: it eases back and out onto the front end's own slow orbit, and the arena turns behind the
        //numbers. That is the whole of the moment — the player has stopped playing and is being shown what
        //they did, and a frame frozen at eye level behind the barrel says nothing about it.
        //
        //Driven from HERE and not from the session, because the session is COVERED by this page and so is no
        //longer updated at all — it is frozen mid-frame, which is exactly what a pause wants and exactly what
        //this moment does not. This page is the only screen still running, so it is the only one that can
        //move anything. (A pause is left alone deliberately: it shows the game exactly as the player left it.)

        /// <summary>
        /// How long the release takes. Long enough to read as the camera being let go rather than as a cut,
        /// short enough that it is over well before anyone has finished reading the breakdown.
        /// </summary>
        private const float ORBIT_EASE_SECONDS = 2.5f;

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

            //The authored cadence stands until there is a fanfare to take one from.
            _revealStep = REVEAL_STEP_SECONDS;
            _revealDelay = REVEAL_DELAY_SECONDS;
            _chimeRootOffset = 0;
            _chimeInKey = false;
            _cadenceSettled = false;

            TakeCadenceFromFanfare();
            ApplyStars();

            //Started at the bearing the lens is already on, so the release is straight out from the arena
            Game.Backdrop.AlignOrbitTo(_fromPosition);
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

                //Asked again until the first star lands, because Enter can run before StartFanfare has even
                //been called on some paths — but it no longer WAITS for the sound, which was the first fix
                //and the wrong one: the bake takes over three seconds on this machine, so holding the row
                //until the music arrived held it far longer than any player would sit through.
                if (!_cadenceSettled && _starsAnnounced == 0) TakeCadenceFromFanfare();

                AnnounceLandedStars();
                ApplyStars();

                //One last pass has just run at or past the end, so the row is on its exact resting values
                if (_revealClock >= RevealTotalSeconds) _revealSettled = true;
            }

            Game.Backdrop.AdvanceOrbit(elapsed, out Vector3 position, out Vector3 target, out float fieldOfView);

            _orbitBlend = MathF.Min(1f, _orbitBlend + elapsed / ORBIT_EASE_SECONDS);

            //Smoothstep, whose derivative is zero at BOTH ends: the camera leaves at rest, because it was
            //standing still, and arrives at the orbit's own rate, because by then the blend has stopped
            //changing and the only thing still moving is the orbit itself. A linear blend would start with a
            //lurch and end with the camera visibly changing speed as it settled.
            float eased = MathHelper.SmoothStep(0f, 1f, _orbitBlend);

            RecoilCamera camera = Game.Camera;

            camera.BasePosition = Vector3.Lerp(_fromPosition, position, eased);
            camera.BaseTarget = Vector3.Lerp(_fromTarget, target, eased);
            camera.FieldOfView = MathHelper.Lerp(_fromFov, fieldOfView, eased);

            //Back to a level horizon: a level can end mid-tilt if a drop cinematic was running, and a result
            //screen read over a dutched frame reads as a fault
            camera.BaseRoll = MathHelper.Lerp(_fromRoll, 0f, eased);

            //Also what settles the recoil: the last shot's kick decays here rather than being frozen into the
            //pose the player is left looking at
            camera.Update(elapsed);
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
        //Driven from here, like the camera release and the star reveal above and for the same reason: the
        //session under this page is covered and therefore frozen, so this is the only screen still updating.

        /// <summary>
        /// How long the arena stays sharp. It sits past both of the things this page does on arrival — the
        /// camera's release (<see cref="ORBIT_EASE_SECONDS"/>) and the last star landing
        /// (<see cref="RevealTotalSeconds"/>, about 2 s) — so nothing is blurred while it is still arriving,
        /// and the softening reads as the moment settling rather than as a transition out of it.
        /// </summary>
        private const float BLUR_DELAY_SECONDS = 3.4f;

        /// <summary>
        /// How long the frame takes to go fully soft. Slow on purpose: at half this the arena reads as being
        /// snatched away, where over four seconds the eye follows one image losing its edges — which is the
        /// effect, and it is only worth having if it is watchable.
        /// </summary>
        private const float BLUR_EASE_SECONDS = 4f;

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
        //Driven from here for the same reason the camera release is: this page is the only screen still being
        //updated, the session under it being covered and therefore frozen.

        //A slot holds one glyph, so the shared chars are wanted as strings here. Built once rather than per
        //frame: ApplyStars runs every frame the page is up, and Label.Text takes a string.
        private static readonly string HOLLOW = STAR_HOLLOW.ToString();
        private static readonly string FILLED = STAR_FILLED.ToString();

        /// <summary>A beat before the first star, so the verdict above it is read first rather than competing.</summary>
        private const float REVEAL_DELAY_SECONDS = 0.45f;

        /// <summary>
        /// Between one star landing and the next, when there is no fanfare to take the cadence from. Long
        /// enough to count them, short enough not to wait.
        /// </summary>
        private const float REVEAL_STEP_SECONDS = 0.3f;

        //Where the stars land and what they sound at, once the victory fanfare underneath is taken into
        //account (#158). The KEY is available as soon as the fanfare is asked for; the BEAT only once it is
        //audible, which on a slow machine is several seconds later — so the row takes the key always and the
        //grid when it can, rather than waiting for a piece that arrives long after the player has read the
        //screen. Frozen once the first star lands, so the row cannot change cadence half way down.
        private float _revealStep = REVEAL_STEP_SECONDS;
        private float _revealDelay = REVEAL_DELAY_SECONDS;
        private int _chimeRootOffset;
        private bool _chimeInKey;

        /// <summary>
        /// The chord tones each successive star sounds, as semitones over the rating's root — a major triad
        /// and then the octave. It replaces a fixed ~2.3-semitone step which was <b>an interval in no key at
        /// all</b> (#158): four of those over a tonal fanfare is why the run read as cheap. An arpeggio of the
        /// piece's own tonic triad is consonant against every chord of its I–V–vi–IV by construction.
        /// </summary>
        private static readonly int[] CHIME_TRIAD = { 0, 4, 7, 12 };

        /// <summary>One star's own travel, from oversized to seated.</summary>
        private const float REVEAL_PUNCH_SECONDS = 0.34f;

        /// <summary>How large a star starts, as a multiple of its seated size.</summary>
        private const float REVEAL_START_SCALE = 2.4f;

        //Where the punch settles back FROM: it overshoots a little past its resting size so it lands rather
        //than merely stopping. Kept small - a big rebound reads as rubber, not as a medal being struck.
        private const float REVEAL_UNDERSHOOT = 0.92f;
        private const float REVEAL_SETTLE_FROM = 0.66f;

        /// <summary>
        /// When the last slot has finished settling — past this nothing in the row is moving, which is what
        /// <see cref="_revealSettled"/> uses to stop touching it. It reads the <b>live</b> delay and step and
        /// not the defaults: since #158 those come from the fanfare's tempo, and a constant computed off the
        /// authored figures would latch the row early and freeze it mid-reveal at any slower beat.
        /// </summary>
        private float RevealTotalSeconds =>
            _revealDelay + (StarRating.MAX - 1) * _revealStep + REVEAL_PUNCH_SECONDS;

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
        private float RevealTimeOf(int index) => _revealDelay + index * _revealStep;

        /// <summary>
        /// Takes the reveal's cadence and the chime's key from the fanfare that is already sounding (#158).
        /// <para>
        /// <b>The stars land on its beats</b>: the step becomes one beat of whatever tempo it rolled, and the
        /// first star is pushed out to the next beat boundary — measured from when the fanfare became
        /// <i>audible</i>, which is why `ProceduralMusic` times it from the frame the instance plays rather
        /// than from where the bake was asked for.
        /// </para>
        /// <para>
        /// <b>And the chime is pitched into its key.</b> The buffer is baked at A5, and `SoundEffect.Play`
        /// takes its pitch in octaves over −1…1 — so the whole run has to fit inside ONE octave of shift. The
        /// root is therefore placed in the octave <i>below</i> the baked pitch (an offset of −12…−1), which
        /// leaves room for the triad above it to reach +11 at the top star and never runs out of range. Get
        /// that backwards and the fourth star of a four-star clear silently clamps to the wrong note.
        /// </para>
        /// </summary>
        /// <summary>
        /// Whether the cadence has been settled. It is <b>not</b> settled in <see cref="Enter"/>, and that is
        /// the trap this whole fix nearly fell into: the fanfare is baked on a background thread and realized
        /// whenever that finishes, while this page is pushed on the frame the level cleared — so at Enter
        /// there is usually no fanfare sounding yet and the answer would be "no music", every time. It is
        /// therefore asked again each frame until the first star lands, after which it is frozen so the row
        /// cannot change cadence half way down.
        /// </summary>
        private bool _cadenceSettled;

        private void TakeCadenceFromFanfare()
        {
            if (Game.Music == null) return;
            if (!Game.Music.TryGetFanfare(out ProceduralMusic.FanfareShape shape, out float sounding)) return;
            if (!shape.Victory) { _cadenceSettled = true; return; }

            _chimeInKey = true;
            _cadenceSettled = true;

            //THE KEY FIRST, because it is the fix and it needs nothing but the roll. The buffer is baked at A5
            //and the fanfare's root is brought into the octave BELOW it, which leaves the triad above room to
            //reach +11 at the top star without running past the ±12 the platform's pitch can express. Placing
            //the root above the baked pitch instead would silently clamp the fourth star of a four-star clear.
            const int BakedPitch = 81;   //A5

            int pitchClass = ((shape.Root % 12) + 12) % 12;
            int bakedClass = BakedPitch % 12;

            _chimeRootOffset = ((pitchClass - bakedClass + 12) % 12) - 12;   //-12…-1

            //One beat a star. At the victory fanfare's 128-142 BPM that is 0.42-0.47 s, close to the 0.30 the
            //reveal used before and slow enough to still read as one star at a time.
            float beat = 60f / MathF.Max(1f, shape.Bpm);
            _revealStep = beat;

            //The BEAT GRID is the one part that needs the piece to be AUDIBLE, and usually it is not yet — the
            //key above never did. So the row takes the key and the tempo either way and only lines up to a
            //boundary when there is one; a negative "sounding" is TryGetFanfare saying the bake has not landed.
            if (sounding < 0f) return;

            float toNextBeat = beat - (sounding % beat);
            while (_revealClock + toNextBeat < REVEAL_DELAY_SECONDS) toNextBeat += beat;

            _revealDelay = _revealClock + toNextBeat;
        }

        /// <summary>How far off the baked pitch star <paramref name="index"/> sounds — see the remarks above.</summary>
        private float ChimeSemitones(int index) =>
            _chimeInKey
                ? _chimeRootOffset + CHIME_TRIAD[Math.Min(index, CHIME_TRIAD.Length - 1)]
                : CHIME_TRIAD[Math.Min(index, CHIME_TRIAD.Length - 1)];

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
                _starsAnnounced++;
            }
        }

        #endregion

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();

            //CLEARED / FAILED / CAMPAIGN COMPLETE — a title's size, like the main menu's name, because this is
            //the line the screen exists to state.
            _heading = new Label
            {
                Text = string.Empty,
                Font = FontTitle,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 30),
            };
            column.Widgets.Add(_heading);

            //The star rating, straight under the verdict — the headline a player reads at a glance where the
            //score below is the arithmetic (#111). Set in Inter (FontStars), not the display face: Anton has
            //no ★/☆ glyphs at all, and FontStashSharp would draw blanks. Opened up so four glyphs read as a
            //rating rather than as a word — by the row's own spacing now that they are four widgets.
            _starRow = new HorizontalStackPanel
            {
                Spacing = Scaled(26),
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
            _newBest = new Label
            {
                Text = "New best",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };
            column.Widgets.Add(_newBest);

            //Which limit ran out, said plainly — only on a fail. Held back (Visible = false) on a cleared level.
            _reason = new Label
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 12),
            };
            column.Widgets.Add(_reason);

            //The score reached, on a fail. The breakdown below is rightly held back — a failed level is awarded
            //no completion bonus and its partial rows would explain a total nobody is being offered — but the
            //total itself still has to be said, or the player is told they lost and nothing about how they did.
            _bareScore = new Label
            {
                Text = string.Empty,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 30),
            };
            column.Widgets.Add(_bareScore);

            //The breakdown: caption · detail · value, the same three-column shape the settings screen uses, so a
            //number lines up under the number above it and reads at a glance. A plate behind it, because small
            //text over a frozen scene needs the backing the scrim does not give.
            _breakdown = Plate(BuildBreakdown());
            column.Widgets.Add(_breakdown);

            column.Widgets.Add(MenuButton("Retry", Game.RetryLevel));

            //Absent rather than disabled when there is no next level to go to or the score did not clear the
            //gate. Retry stays: it is the one thing that always makes sense at a level's end.
            column.Widgets.Add(_nextLevelButton = MenuButton("Next Level", Game.AdvanceLevel));

            column.Widgets.Add(MenuButton("Main Menu", Game.EndSessionAndReturnToMainMenu));

            return ScreenRoot(column);
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
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));   //caption
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));   //detail (count × worth)
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Part));   //value, right-aligned by the cell

            AddRow(grid, 0, "matched", out _matchedDetail, out _matchedValue);
            AddRow(grid, 1, "orphaned", out _orphanedDetail, out _orphanedValue);
            AddRow(grid, 2, "streak bonus", out _, out _streakValue);
            AddRow(grid, 3, "shots unused", out _unusedDetail, out _unusedValue);

            //The total sits on its own line under the rows — in the value column, so it lines up under the row
            //totals — in the heading weight, so it reads as the answer rather than as another line of the sum.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _totalValue = new Label
            {
                Text = string.Empty,
                Font = Game.MenuFontHeading,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_totalValue, 2);
            Grid.SetRow(_totalValue, 4);
            grid.Widgets.Add(_totalValue);

            //The one gate left, as an aside under the total: the NEXT level's star requirement, shown only
            //when the total falls short of it — which is also exactly when the Next Level button is absent,
            //so the note is what explains the absence. Spanning the grid, because it is a sentence about the
            //campaign rather than another line of the sum.
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            _unlockNote = new Label
            {
                Text = string.Empty,
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_unlockNote, 0);
            Grid.SetColumnSpan(_unlockNote, 3);
            Grid.SetRow(_unlockNote, 5);
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
            _heading.Text = _result.CampaignComplete ? "CAMPAIGN COMPLETE" : (_result.Cleared ? "CLEARED" : "FAILED");

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
                ? $"Score {_result.Score.ToString("N0", CultureInfo.InvariantCulture)}"
                : string.Empty;
            _bareScore.Visible = _result.ShowsBareScore;

            _breakdown.Visible = _result.ShowsBreakdown;

            if (_result.ShowsBreakdown)
            {
                _matchedDetail.Text = $"{_result.MatchedBalls} × {ScoreKeeper.MatchedBallPoints}";
                _matchedValue.Text = (_result.MatchedBalls * ScoreKeeper.MatchedBallPoints).ToString("N0", CultureInfo.InvariantCulture);
                _orphanedDetail.Text = $"{_result.OrphanedBalls} × {ScoreKeeper.OrphanedBallPoints}";
                _orphanedValue.Text = (_result.OrphanedBalls * ScoreKeeper.OrphanedBallPoints).ToString("N0", CultureInfo.InvariantCulture);
                _streakValue.Text = _result.StreakBonus.ToString("N0", CultureInfo.InvariantCulture);

                _unusedDetail.Text = _result.HadBudget
                    ? $"{_result.UnusedShotsAwarded} × {ScoreKeeper.UnusedShotPoints}"
                    : "—";
                _unusedValue.Text = _result.CompletionBonusAwarded.ToString("N0", CultureInfo.InvariantCulture);
                _totalValue.Text = _result.Score.ToString("N0", CultureInfo.InvariantCulture);

                //Only when the road ahead is actually shut — which is also when the Next Level button below
                //is absent, so this line is the absence explained rather than a number always on display.
                bool nextLocked = _result.HasNextLevel && !_result.NextLevelUnlocked;
                _unlockNote.Text = nextLocked
                    ? $"Next level unlocks at {_result.NextLevelMinStars} ★ — you have {_result.TotalStars}"
                    : string.Empty;
                _unlockNote.Visible = nextLocked;
            }

            //Next Level is shown only when the level was cleared, there is another entry to go to AND the
            //star total opens it. Absent, not disabled, when any of that fails — a greyed-out button over a
            //frozen frame is a thing the player cannot do, which reads as the game being broken rather than
            //as the campaign asking for more stars (the note above says that in words).
            _nextLevelButton.Visible = _result.Cleared && _result.HasNextLevel && _result.NextLevelUnlocked;
        }
    }
}
