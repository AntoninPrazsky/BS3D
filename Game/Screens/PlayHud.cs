using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BS3D.Screens
{
    /// <summary>
    /// The in-play readout — score, streak and balls left in the corners, and the numbers that fly from the
    /// balls they were earned on into the score. Drawn with <see cref="SpriteBatch"/> and the menu's own Inter
    /// after the tonemap resolve, in display space, like the crosshair and for the same reason: a HUD
    /// downsampled with the scene would be soft.
    /// <para>
    /// <b>Not Myra.</b> Myra is deliberately driven only while a menu page is up — <c>Desktop.Render</c> also
    /// consumes the mouse and the keyboard, and the game captures the cursor while playing — so putting the HUD
    /// in it would mean unpicking exactly the arrangement that keeps the two from fighting. Myra has no
    /// animation system to reach for either: it lays out widgets, and every motion below would have to be
    /// driven from outside it frame by frame anyway.
    /// </para>
    /// <para>
    /// <b>Its own class</b>, and for the reason <see cref="ScoreKeeper"/> is: this is the feel of the thing,
    /// the part most likely to be argued about and retuned, and it has no business being another few hundred
    /// lines inside <see cref="GameplayScreen"/> where the dials cannot be seen together. It owns no score — it
    /// is handed the keeper each frame and animates what it reads there.
    /// </para>
    /// <para>
    /// <b>The one accent is amber</b> (<see cref="HUD_ACCENT"/>). The HUD sits over seven scene palettes whose
    /// colours are nothing alike, so a hue that reads as the game's own over one of them fights the next — the
    /// menu is greyscale for exactly this reason. Amber survives all seven because it is the sun's own colour
    /// and every scene already has it, and it is spent on one thing only: <i>gain</i>. Score flash, the streak,
    /// the destination a popup turns into, and the last two balls, which is the other moment worth looking at.
    /// The only other colour in the HUD is the popup's at birth — the colour of the balls that were actually
    /// cut, so it is never a decoration; it is the cluster's own language, and the flight into the corner is
    /// literally those balls becoming those points.
    /// </para>
    /// </summary>
    internal sealed class PlayHud
    {
        private readonly BS3DGame _game;

        internal PlayHud(BS3DGame game) => _game = game;

        private int Scaled(int designUnits) => _game.Scaled(designUnits);

        #region Layout and palette

        //Every figure here is a 2160p design unit through Scaled(), like the menu and InfoRenderer's text. The
        //margin has to clear the glow as well as the text — a halo drawn hard against the frame's edge is a
        //halo cut in half by it, which reads as a rendering fault rather than as light.
        private const int HUD_MARGIN = 92;
        private const int HUD_LINE_GAP = 12;

        //Text over a bright sky or a lit skyline needs its own contrast, and a plate behind a HUD reads as
        //furniture. One offset dark copy is what a plate would have cost in attention and does not box the
        //number in — the same problem the menu solves with a plate, solved the way a HUD has to solve it.
        private const int HUD_SHADOW_OFFSET = 7;
        private static readonly Color HUD_SHADOW = new(0, 0, 0, 200);

        /// <summary>
        /// The HUD's one accent, and it means <i>gain</i> everywhere it appears. See the class doc for why it
        /// is amber and why there is only one.
        /// </summary>
        internal static readonly Color HUD_ACCENT = new(255, 201, 92);

        /// <summary>Balls at or under which the count changes weight and starts to breathe.</summary>
        private const int HUD_LOW_BALLS = 5;

        /// <summary>And at or under which it takes the accent — the last shots are worth looking at.</summary>
        private const int HUD_CRITICAL_BALLS = 2;

        //The glow behind a number that has just changed: the same string rendered through FontStashSharp's own
        //Blurry effect, which rasterizes a blurred variant into the atlas rather than costing anything per
        //frame. The amount is in glyph pixels, so it is a design unit like everything else — and because it is
        //resolved off the same quantized viewport height the fonts are, it asks for one extra atlas variant per
        //window size and not one per frame.
        private const int HUD_GLOW_BLUR = 30;
        private const float HUD_GLOW_SCALE = 1.14f;     //larger than the text, so it reads as light around it

        //A blur spreads a glyph's energy over its own area, so one pass of it is always fainter than the text
        //it came from — which is the wrong way round for a flash that is meant to be the loudest thing on the
        //frame. Drawn several times instead: the alpha compounds towards opaque where the halo is densest and
        //stays soft at its edge, which is what a bloom does and what a single pass at any alpha cannot.
        private const int HUD_GLOW_PASSES = 3;

        #endregion

        #region The pulse — "this number just changed"

        /// <summary>
        /// A scalar resting at 1 that is displaced by a kick and springs back — the whole of the HUD's "this
        /// number just changed" language, and the one primitive the score, the streak and the ball count share.
        /// <para>
        /// A spring rather than a keyframed curve, for the reason <c>_adsBlend</c> is a blend and not a state
        /// machine: a second kick landing mid-settle simply adds to the one already in flight, so a burst of
        /// scoring shots reads as one rising swell instead of restarting an animation the eye was following.
        /// The displacement is applied to the <i>position</i> and not to the velocity, so the number jumps at
        /// once and settles afterwards — a kick that has to accelerate first reads as a wobble, not as a hit.
        /// </para>
        /// <para>
        /// It is snapped to rest under an epsilon, exactly as <c>CameraShake</c>'s decay ends rather than
        /// trailing off: an exponential settle never quite arrives, and a permanent sub-pixel tremble on a
        /// number the player reads every second is worse than no motion at all.
        /// </para>
        /// </summary>
        private struct Pulse
        {
            //Displacement from the resting 1, so default(Pulse) is already at rest and a fresh HUD does not
            //have to be told what "no pulse" is
            private float _offset;
            private float _velocity;

            /// <summary>0…1, "how recently was this kicked" — what the glow reads. Linear, so it ends.</summary>
            private float _heat;

            //ω = √320 ≈ 17.9 rad/s (a ~0.35 s period) at ζ ≈ 0.45, which is one visible undershoot of about a
            //fifth of the kick and then done — a snap rather than a wobble
            private const float STIFFNESS = 320f;
            private const float DAMPING = 16f;

            private const float HEAT_DECAY_PER_SECOND = 1.5f;
            private const float REST_EPSILON = 0.0015f;

            //Kicks accumulate — a burst of scoring shots lands inside one settle — so they saturate rather
            //than add without limit, the same shape CameraShake's trauma has. Without it two awards in quick
            //succession throw the score off the top of the frame.
            private const float MAX_OFFSET = 0.95f;

            //An explicit spring this stiff goes unstable as the step approaches 2/ω ≈ 0.11 s, and this game
            //really does run at ten frames a second on weak hardware until the adaptive tier steps in. Sub-
            //stepped so the pop looks the same there as at 240 FPS — the same rule the physics timestep follows.
            private const float MAX_SUB_STEP = 1f / 120f;

            public readonly float Scale => 1f + _offset;
            public readonly float Heat => _heat;

            public void Kick(float displacement)
            {
                _offset = Math.Clamp(_offset + displacement, -MAX_OFFSET, MAX_OFFSET);
                _heat = 1f;
            }

            public void Update(float elapsed)
            {
                if (_heat > 0f) _heat = MathF.Max(0f, _heat - HEAT_DECAY_PER_SECOND * elapsed);

                if (_offset == 0f && _velocity == 0f) return;

                while (elapsed > 0f)
                {
                    float step = MathF.Min(elapsed, MAX_SUB_STEP);
                    elapsed -= step;

                    //Semi-implicit: the position is advanced by the velocity this step already produced, which
                    //is what keeps a stiff spring from gaining energy at a long step
                    _velocity += (-STIFFNESS * _offset - DAMPING * _velocity) * step;
                    _offset += _velocity * step;
                }

                if (MathF.Abs(_offset) < REST_EPSILON && MathF.Abs(_velocity) < REST_EPSILON * 20f)
                {
                    _offset = 0f;
                    _velocity = 0f;
                }
            }

            public void Reset() => this = default;
        }

        private Pulse _scorePulse, _streakPulse, _ballsPulse;

        //How far each number is thrown. The score grows because it went up and the ball count shrinks because
        //it went down — the direction IS the information, which is why they are not one shared figure.
        private const float SCORE_KICK = 0.7f;
        private const float STREAK_KICK = 0.9f;
        private const float BALLS_KICK = -0.45f;

        #endregion

        #region The corner readouts

        /// <summary>
        /// The score the HUD is <i>showing</i>, which chases what has been delivered to it rather than matching
        /// the keeper. A number that ticks up is what makes a big collapse feel big; one that snaps says the
        /// same thing in a single frame nobody sees.
        /// </summary>
        private float _scoreShown;

        /// <summary>How fast that chase closes: a fraction of the gap per second, plus a floor so it lands.</summary>
        private const float SCORE_CHASE_PER_SECOND = 6f;
        private const float SCORE_MIN_RATE = 400f;

        //The strings, rebuilt only when their number changes. Formatting three of them per frame is three
        //managed allocations per frame on the gameplay path, which the render-hygiene rules rule out — and the
        //score's own count-up means it genuinely does change on most frames of an award, so it has to be the
        //value and not the frame that decides.
        private int _scoreTextFor = -1;
        private string _scoreText = "0";
        private int _ballsTextFor = -1;
        private string _ballsText = "0";
        private int _streakTextFor = -1;
        private string _streakText = "×1";

        /// <summary>
        /// Balls left as of the previous frame, so a decrement can be seen. −1 means "no baseline yet", which is
        /// what a level that has just been built has: seeding the count would otherwise punch on frame one.
        /// </summary>
        private int _ballsShown = -1;

        private int _multiplierShown = 1;

        //A streak that merely vanishes is feedback the player can miss entirely — it is the one thing on the
        //HUD whose LOSS is the event. So the broken value is held for a moment, fading and dropping away.
        private int _streakBroken;
        private float _streakBrokenAge;
        private const float STREAK_BREAK_TIME = 0.45f;
        private const int STREAK_BREAK_FALL = 44;       //design units it slides down as it goes

        /// <summary>
        /// Seconds this level has been played, for the low-ball breathing. On the play clock, so it holds still
        /// behind a pause rather than being found mid-stride when the player comes back.
        /// </summary>
        private float _clock;

        private const float BREATHE_PERIOD = 1.05f;
        private const float BREATHE_AMPLITUDE = 0.1f;

        #endregion

        #region The flying awards

        /// <summary>
        /// A score earned at a cell, on its way to the corner. It is born on the balls it came from, is read
        /// there, and is then flown into the score readout, which is what ties the two together: the player
        /// sees where the points came from <i>and</i> watches them arrive, and the corner's count-up starts on
        /// the frame the number lands rather than a second before it, when it would mean nothing.
        /// </summary>
        private struct Popup
        {
            /// <summary>Where it was earned. Reprojected every frame — the cluster sways and the recoil knocks
            /// the camera about, so a number pinned to a pixel comes adrift from its ball within a few frames.</summary>
            public Vector3 World;

            /// <summary>The last valid projection of <see cref="World"/>, and the flight's source once that
            /// point has left the frustum — a shot can be the last thing on screen before a cinematic swings
            /// the camera right off it.</summary>
            public Vector2 Screen;
            public bool Projected;

            public string Text;             //"+1,200"
            public string Suffix;           //"×3", or null at ×1

            /// <summary>What this is carrying, withheld from the readout until it lands there.</summary>
            public int Points;

            /// <summary>The colour of the balls that were cut — see the class doc.</summary>
            public Color Tint;

            public float Age;
        }

        private readonly List<Popup> _popups = new();

        //The two legs, in seconds. Hold is the reading time — it is the whole reason the number exists at the
        //ball at all — and the flight is short, because past about half a second an object crossing the screen
        //stops reading as thrown and starts reading as drifting.
        private const float POPUP_HOLD = 0.5f;
        private const float POPUP_FLIGHT = 0.55f;
        private const float POPUP_LIFETIME = POPUP_HOLD + POPUP_FLIGHT;

        private const float POPUP_BIRTH = 0.2f;         //the pop-in, inside the hold
        private const float POPUP_BIRTH_SCALE = 0.15f;  //where that pop starts from — near nothing, so it bursts in
        private const float POPUP_RISE = 2.2f;          //world units over the hold, so it drifts with the cluster
        private const float POPUP_ARRIVE_SCALE = 0.62f; //shrinking as it goes is most of what reads as distance
        private const float POPUP_TINT_TO_WHITE = 0.3f; //how far a ball's hue is carried to white to stay legible
        private const int POPUP_SUFFIX_GAP = 10;

        //Motion blur. A real per-pixel blur would want a shader over the resolved frame, and SpriteBatch will
        //take one (Begin(effect:) puts a pixel shader over the glyph quads) — but it is the wrong tool here:
        //each glyph is its own quad, so a blur that reaches outside the glyph is clipped at the quad's edge and
        //the smear stops dead at the letterforms. Drawing the string a few times along the path it just
        //travelled has no such edge, costs nothing when the number is not moving, and is the same trick the
        //launch smear uses for the same reason.
        private const float SMEAR_LOOKBACK = 0.1f;      //in flight-parameter units, so the smear IS the local speed
        private const int SMEAR_SPACING = 18;           //design units between ghosts
        private const int SMEAR_MAX = 12;
        private const float SMEAR_ALPHA = 0.8f;

        #endregion

        #region Lifetime

        /// <summary>
        /// Wipes the HUD for a level about to start. The score begins at zero without counting down to it, a
        /// popup from the level just finished must not float over the one just built, and the baselines are
        /// seeded from the fresh keeper so the first frame does not read a new budget as a spent ball.
        /// </summary>
        internal void Reset(ScoreKeeper score)
        {
            _scoreShown = 0f;
            _popups.Clear();

            _scorePulse.Reset();
            _streakPulse.Reset();
            _ballsPulse.Reset();

            _ballsShown = score.ShotsRemaining ?? -1;
            _multiplierShown = score.Multiplier;
            _streakBroken = 0;
            _clock = 0f;
        }

        /// <summary>
        /// A shot has scored. <paramref name="type"/> is the colour of the group it completed — the shot ball's
        /// own, since a match is by definition three of one colour touching.
        /// </summary>
        internal void AddAward(Vector3 world, ScoreAward award, BallType type)
        {
            _popups.Add(new Popup
            {
                World = world,

                //Invariant, like every other figure the game prints: the machine's locale decides what "N0"
                //groups with, and a popup reading "+2 960" beside a corner score reading "2,960" is one number
                //written two ways on the same frame. It happened.
                Text = "+" + award.Points.ToString("N0", CultureInfo.InvariantCulture),

                //The multiplier PRINTED is the one that was applied, which is why Landed returns it rather than
                //leaving this to read the property afterwards — by then it has been raised for the next shot
                Suffix = award.Multiplier > 1 ? "×" + award.Multiplier.ToString(CultureInfo.InvariantCulture) : null,

                Points = award.Points,
                Tint = TypeColor(type),
                Age = 0f,
            });
        }

        /// <summary>
        /// Hits the score readout without an award having flown into it — for points that arrive with no popup
        /// to carry them, which is the completion bonus and nothing else.
        /// </summary>
        internal void FlashScore() => _scorePulse.Kick(SCORE_KICK);

        #endregion

        #region Update

        /// <summary>
        /// Advances the count-up, the springs and the popups. On the play clock, so all three freeze with a
        /// pause rather than running on behind a menu the player has stopped the game with.
        /// </summary>
        internal void Update(float elapsed, ScoreKeeper score)
        {
            _clock += elapsed;

            AgePopups(elapsed);
            ChaseScore(elapsed, score);
            WatchStreak(elapsed, score);
            WatchBalls(score);

            _scorePulse.Update(elapsed);
            _streakPulse.Update(elapsed);
            _ballsPulse.Update(elapsed);
        }

        /// <summary>
        /// Ages the popups and <b>delivers</b> the ones that have arrived: reaching the corner is what pays the
        /// points into the readout and kicks it. Cause and effect land on the same frame, which is the whole
        /// point of flying them there.
        /// </summary>
        private void AgePopups(float elapsed)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];
                popup.Age += elapsed;

                if (popup.Age >= POPUP_LIFETIME)
                {
                    _popups.RemoveAt(i);
                    _scorePulse.Kick(SCORE_KICK);
                }
                else _popups[i] = popup;
            }
        }

        private void ChaseScore(float elapsed, ScoreKeeper score)
        {
            //What the readout is allowed to know: everything the keeper has, less whatever is still in the air
            //on its way here. Recomputed from the list rather than carried as a running total, so it is exactly
            //self-correcting — once the last popup has landed the two agree by construction, whatever happened
            //in between (a level torn down mid-flight, a completion bonus awarded with no popup at all).
            int withheld = 0;
            for (int i = 0; i < _popups.Count; i++) withheld += _popups[i].Points;

            float target = MathF.Max(0f, score.Score - withheld);

            if (_scoreShown < target)
            {
                //A fraction of the remaining gap, which makes a big jump start fast and settle, plus a floor so
                //a small one still lands instead of creeping at it for ever
                float step = MathF.Max((target - _scoreShown) * SCORE_CHASE_PER_SECOND, SCORE_MIN_RATE) * elapsed;
                _scoreShown = MathF.Min(target, _scoreShown + step);
            }
            else _scoreShown = target; //a new level resets the score downwards, and that is not a count-up
        }

        private void WatchStreak(float elapsed, ScoreKeeper score)
        {
            int multiplier = score.Multiplier;

            if (multiplier > _multiplierShown && multiplier > 1) _streakPulse.Kick(STREAK_KICK);

            if (multiplier == 1 && _multiplierShown > 1)
            {
                _streakBroken = _multiplierShown;
                _streakBrokenAge = 0f;
            }
            else if (multiplier > 1) _streakBroken = 0;

            _multiplierShown = multiplier;

            if (_streakBroken > 0)
            {
                _streakBrokenAge += elapsed;
                if (_streakBrokenAge >= STREAK_BREAK_TIME) _streakBroken = 0;
            }
        }

        private void WatchBalls(ScoreKeeper score)
        {
            if (score.ShotsRemaining is not int remaining) return;

            //Only downwards: the count is only ever spent within a level, and a level change reseeds through
            //Reset, so an upward move here would be a bug rather than an event worth announcing
            if (_ballsShown >= 0 && remaining < _ballsShown) _ballsPulse.Kick(BALLS_KICK);

            _ballsShown = remaining;
        }

        #endregion

        #region Draw

        /// <summary>
        /// Score, streak and balls left, in the corners. The frame's centre belongs to the cluster and its
        /// bottom to the gun — and precise aim leans the lens in over the barrel, which fills more of it still —
        /// so the corners are the only space free in <b>both</b> poses and at every aspect.
        /// <para>
        /// No ceiling counter. The glass coming down at the cluster is meant to be its own warning; a number
        /// counting to the next descent is a second way of saying what the player can already see.
        /// </para>
        /// </summary>
        internal void Draw(ScoreKeeper score, ICamera camera)
        {
            _game.EnsureHudFonts();

            Viewport viewport = _game.GraphicsDevice.Viewport;
            int margin = Scaled(HUD_MARGIN);

            SpriteBatch batch = _game.OverlayBatch;
            batch.Begin();

            //The score, top right — the FPS line owns the top left (InfoRenderer draws after this, in
            //base.Draw). Its right edge is the pivot everything above hangs off, so the margin holds while the
            //number pulses and while it grows a digit.
            int shown = (int)MathF.Round(_scoreShown);
            if (shown != _scoreTextFor)
            {
                _scoreTextFor = shown;
                _scoreText = shown.ToString("N0", CultureInfo.InvariantCulture);
            }

            float heat = _scorePulse.Heat;
            Vector2 scoreSize = _game.HudFontScore.MeasureString(_scoreText);
            Vector2 scoreAnchor = new(viewport.Width - margin, margin + scoreSize.Y * 0.5f);

            DrawPulsed(_game.HudFontScore, _scoreText, scoreAnchor, new Vector2(1f, 0.5f), _scorePulse.Scale,
                Color.Lerp(BS3DGame.MENU_TEXT, HUD_ACCENT, heat), heat);

            DrawStreak(score, viewport, margin, scoreAnchor.Y + scoreSize.Y * 0.5f + Scaled(HUD_LINE_GAP));
            DrawBallsLeft(score, viewport, margin);

            //Last, so the numbers coming in pass over the readouts rather than under them
            DrawAwards(camera, viewport, scoreAnchor - new Vector2(scoreSize.X * 0.5f, 0f));

            batch.End();
        }

        /// <summary>
        /// The streak, under the score, and only while it is actually running — held back at ×1 so it reads as
        /// something the player is carrying rather than as furniture. It warms towards the accent as it climbs,
        /// so ×5 is visibly hotter than ×2 without a second number to read; and when it goes it is held for a
        /// moment, sliding down and fading, because its <i>loss</i> is the event and a thing that simply
        /// vanishes between frames is a thing the player never saw.
        /// </summary>
        private void DrawStreak(ScoreKeeper score, Viewport viewport, int margin, float top)
        {
            int multiplier = score.Multiplier > 1 ? score.Multiplier : _streakBroken;
            if (multiplier <= 1) return;

            bool broken = score.Multiplier <= 1;
            float fade = broken ? 1f - _streakBrokenAge / STREAK_BREAK_TIME : 1f;

            if (multiplier != _streakTextFor)
            {
                _streakTextFor = multiplier;
                _streakText = "×" + multiplier.ToString(CultureInfo.InvariantCulture);
            }

            //How hot the streak is running, over the whole range the rule allows — so the ramp keeps meaning
            //the same thing if MaxMultiplier is ever retuned
            float climb = (multiplier - 1f) / MathF.Max(1f, ScoreKeeper.MaxMultiplier - 1f);
            Color colour = Color.Lerp(BS3DGame.MENU_TEXT, HUD_ACCENT, climb);

            Vector2 size = _game.HudFontLabel.MeasureString(_streakText);
            float drop = broken ? (1f - fade) * Scaled(STREAK_BREAK_FALL) : 0f;
            Vector2 anchor = new(viewport.Width - margin, top + size.Y * 0.5f + drop);

            float scale = _streakPulse.Scale * (broken ? MathHelper.Lerp(0.8f, 1f, fade) : 1f);

            DrawPulsed(_game.HudFontLabel, _streakText, anchor, new Vector2(1f, 0.5f), scale,
                colour * fade, broken ? 0f : _streakPulse.Heat);
        }

        /// <summary>
        /// Balls left, bottom left — near the gun without being behind it. Nothing at all on a level that grants
        /// an unlimited budget: a resource that cannot run out is not one to plan against.
        /// <para>
        /// The states escalate in three steps rather than one, and only the last of them is a colour. At
        /// <see cref="HUD_LOW_BALLS"/> the number turns bold and starts to breathe — motion is a stronger alarm
        /// than hue and it costs the HUD nothing over seven palettes. At <see cref="HUD_CRITICAL_BALLS"/> it
        /// takes the accent as well, which is the one place the HUD spends colour on something other than gain.
        /// </para>
        /// </summary>
        private void DrawBallsLeft(ScoreKeeper score, Viewport viewport, int margin)
        {
            if (score.ShotsRemaining is not int left) return;

            bool low = left <= HUD_LOW_BALLS;
            bool critical = left <= HUD_CRITICAL_BALLS;

            SpriteFontBase font = low ? _game.HudFontScoreBold : _game.HudFontScore;
            SpriteFontBase captionFont = low ? _game.HudFontLabelBold : _game.HudFontLabel;

            if (left != _ballsTextFor)
            {
                _ballsTextFor = left;
                _ballsText = left.ToString(CultureInfo.InvariantCulture);
            }

            string caption = left == 1 ? "ball left" : "balls left";
            Vector2 captionSize = captionFont.MeasureString(caption);
            float bottom = viewport.Height - margin;

            //Breathing, not blinking: a slow swell reads as urgency, a hard flash reads as a fault. It runs off
            //the play clock, so it stops when the game does. −1…1, kept as the raw wave so both the size and the
            //glow can be driven from the one phase and cannot drift apart.
            float wave = low ? MathF.Sin(_clock * MathHelper.TwoPi / BREATHE_PERIOD) : 0f;

            Color colour = critical
                ? Color.Lerp(BS3DGame.MENU_TEXT, HUD_ACCENT, 0.85f)
                : BS3DGame.MENU_TEXT;

            //A shrink kick still has to be seen at the critical count, so the halo never drops to nothing there:
            //it breathes between half and full rather than pulsing on and off
            float glow = critical ? 0.5f + wave * 0.5f : _ballsPulse.Heat;

            DrawString(captionFont, caption, new Vector2(margin, bottom - captionSize.Y),
                critical ? Color.Lerp(BS3DGame.MENU_TEXT_DIM, HUD_ACCENT, 0.5f) : BS3DGame.MENU_TEXT_DIM);

            //Pivoted on its bottom-left corner: the margin and the caption under it stay exactly where they are
            //while the number itself shrinks and swells, so the layout never moves — only the figure does
            DrawPulsed(font, _ballsText,
                new Vector2(margin, bottom - captionSize.Y - Scaled(HUD_LINE_GAP)), new Vector2(0f, 1f),
                _ballsPulse.Scale * (1f + BREATHE_AMPLITUDE * wave), colour, glow);
        }

        /// <summary>
        /// The awards, on their way in. Each is born on the cell it was earned in, is held there long enough to
        /// be read, and is then flown into the score with a smear behind it. The source is <b>reprojected every
        /// frame</b> and the flight is a lerp from it, so at the handover the blend weight is zero and the
        /// number is exactly where it was — the same one-reversible-scalar idiom precise aim and the drop
        /// cinematic use, and for the same reason: there is no frame on which anything snaps.
        /// </summary>
        private void DrawAwards(ICamera camera, Viewport viewport, Vector2 destination)
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];

                Vector2 source;

                if (TryProject(camera, viewport, popup, out Vector2 projected))
                {
                    source = projected;

                    //Remembered, because the flight has to start from somewhere even after the camera has
                    //swung the cell out of frame — which a drop cinematic does routinely
                    popup.Screen = projected;
                    popup.Projected = true;
                    _popups[i] = popup;
                }
                else if (popup.Projected) source = popup.Screen;
                else if (popup.Age < POPUP_HOLD) continue;  //never seen, and still in the leg where it would be at the cell
                else source = destination;                  //only the flight is left, and it ends where it ends

                float flight = MathF.Max(0f, popup.Age - POPUP_HOLD) / POPUP_FLIGHT;

                if (flight <= 0f)
                {
                    //The reading leg: still on the balls, popping in
                    float birth = MathHelper.Clamp(popup.Age / POPUP_BIRTH, 0f, 1f);
                    float scale = MathHelper.Lerp(POPUP_BIRTH_SCALE, 1f, EaseOutBack(birth));

                    DrawAwardAt(popup, source, scale, popup.Tint, 1f);
                    continue;
                }

                DrawAwardFlight(popup, source, destination, flight);
            }
        }

        /// <summary>
        /// One award crossing the frame. The smear is sampled off the curve itself rather than off the last
        /// frame's position: the path is known in closed form, so looking back a fixed distance <i>in flight
        /// parameter</i> gives a trail whose length is the curve's own local speed — nothing to store, nothing
        /// to go stale, and identical at any frame rate.
        /// </summary>
        private void DrawAwardFlight(in Popup popup, Vector2 source, Vector2 destination, float flight)
        {
            Vector2 at = FlightPosition(source, destination, flight);
            Vector2 before = FlightPosition(source, destination, MathF.Max(0f, flight - SMEAR_LOOKBACK));

            float ease = MathHelper.SmoothStep(0f, 1f, flight);
            float scale = MathHelper.Lerp(1f, POPUP_ARRIVE_SCALE, ease);

            //It stops being the balls' colour and becomes the score's — which is the whole sentence the flight
            //is there to say. Faster than the motion, so it has clearly turned by the time it lands.
            Color colour = Color.Lerp(popup.Tint, HUD_ACCENT, MathF.Pow(flight, 0.65f));

            //Held opaque nearly all the way in. It is absorbed rather than faded out: the corner's own flash is
            //what finishes the gesture, and a number that dissolves before it arrives never delivered anything.
            float alpha = 1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((flight - 0.88f) / 0.12f, 0f, 1f)) * 0.55f;

            float distance = Vector2.Distance(before, at);
            int ghosts = Math.Min(SMEAR_MAX, (int)(distance / Scaled(SMEAR_SPACING)));

            for (int g = 1; g <= ghosts; g++)
            {
                float t = g / (ghosts + 1f);

                //Squared, so the smear is dense right behind the number and thins away — which is what a
                //shutter actually integrates, and what stops the trail reading as a row of copies
                DrawAwardAt(popup, Vector2.Lerp(before, at, t), scale * MathHelper.Lerp(0.85f, 1f, t),
                    colour, alpha * SMEAR_ALPHA * t * t, shadow: false);
            }

            DrawAwardAt(popup, at, scale, colour, alpha);
        }

        /// <summary>
        /// The flight path: a lerp from the cell to the corner through an ease that pulls <i>backwards</i> first.
        /// That anticipation is what makes the throw read as a throw — the number gathers itself, then goes, and
        /// arrives at its greatest speed, which is exactly where the smear should be longest and where the
        /// corner is about to be hit.
        /// </summary>
        private static Vector2 FlightPosition(Vector2 source, Vector2 destination, float flight) =>
            Vector2.Lerp(source, destination, EaseInBack(flight));

        /// <summary>One award — its points, and the multiplier it was won at, kept in the accent beside them.</summary>
        private void DrawAwardAt(in Popup popup, Vector2 centre, float scale, Color colour, float alpha, bool shadow = true)
        {
            SpriteFontBase font = _game.HudFontPopup;
            SpriteFontBase suffixFont = _game.HudFontLabel;

            Vector2 size = font.MeasureString(popup.Text);
            Vector2 suffixSize = popup.Suffix != null ? suffixFont.MeasureString(popup.Suffix) : Vector2.Zero;
            float gap = popup.Suffix != null ? Scaled(POPUP_SUFFIX_GAP) : 0f;

            float width = size.X + gap + suffixSize.X;
            float height = MathF.Max(size.Y, suffixSize.Y);
            Vector2 corner = centre - new Vector2(width, height) * 0.5f * scale;

            DrawString(font, popup.Text, corner + new Vector2(0f, (height - size.Y) * 0.5f * scale),
                colour * alpha, scale, shadow);

            if (popup.Suffix == null) return;

            //The multiplier is the streak's colour wherever it appears, so the badge on a popup and the number
            //in the corner are recognisably the same thing
            DrawString(suffixFont, popup.Suffix,
                corner + new Vector2(size.X + gap, (height - suffixSize.Y) * 0.5f) * scale,
                HUD_ACCENT * alpha, scale, shadow);
        }

        private static bool TryProject(ICamera camera, Viewport viewport, in Popup popup, out Vector2 screen)
        {
            //It rises over the reading leg and then stops: past that the flight owns where it goes, and a source
            //still climbing would fight the curve
            float rise = MathHelper.Clamp(popup.Age / POPUP_HOLD, 0f, 1f) * POPUP_RISE;
            Vector3 projected = viewport.Project(popup.World + Vector3.Up * rise,
                camera.Projection, camera.View, Matrix.Identity);

            screen = new Vector2(projected.X, projected.Y);

            //Behind the lens, or past the far plane: Project still returns a point, and drawing it would put the
            //number somewhere on screen that has nothing to do with where the ball was
            return projected.Z >= 0f && projected.Z <= 1f;
        }

        #endregion

        #region Drawing primitives

        /// <summary>
        /// One readout, scaled about <paramref name="pivot"/> (0…1 within its own box) so the corner it is
        /// aligned to holds still while the figure pulses, and haloed by <paramref name="glow"/> — a blurred
        /// copy of the same string, which is FontStashSharp's own <see cref="FontSystemEffect.Blurry"/> and so
        /// costs one atlas variant rather than anything per frame.
        /// </summary>
        private void DrawPulsed(SpriteFontBase font, string text, Vector2 anchor, Vector2 pivot, float scale,
            Color colour, float glow)
        {
            Vector2 size = font.MeasureString(text);
            Vector2 at = anchor - size * pivot * scale;

            if (glow > 0.01f)
            {
                Vector2 glowScale = new(scale * HUD_GLOW_SCALE);
                int blur = Math.Max(1, Scaled(HUD_GLOW_BLUR));

                //Centred on the text rather than pivoted with it: a halo is light coming off the glyphs, so it
                //grows about them, where anchoring it to the same corner would slide it off as the number
                //swells. It has to be centred by its OWN measurement and not by the text's: a blurred glyph is
                //a larger bitmap with its own render offset, so drawing it from the text's origin puts the halo
                //down and to the right of the number it belongs to — which looked like a stray glow beside the
                //score rather than the score glowing.
                Vector2 haloSize = font.MeasureString(text, null, 0f, 0f, FontSystemEffect.Blurry, blur);
                Vector2 glowAt = at + (size * scale - haloSize * glowScale) * 0.5f;

                //Squared, so the halo is a flare on the frame the number lands and has thinned well before the
                //spring finishes settling — it marks the hit, it does not accompany the motion
                Color halo = HUD_ACCENT * (glow * glow);

                for (int i = 0; i < HUD_GLOW_PASSES; i++)
                    _game.OverlayBatch.DrawString(font, text, glowAt, halo, 0f, Vector2.Zero,
                        glowScale, 0f, 0f, 0f, TextStyle.None, FontSystemEffect.Blurry, blur);
            }

            DrawString(font, text, at, colour, scale);
        }

        /// <summary>
        /// One string with a dark copy behind it. Colours are premultiplied by <c>Color * float</c> — SpriteBatch's
        /// default AlphaBlend expects premultiplied colour, and that operator scales all four channels — so a
        /// faded popup's shadow fades with it rather than outliving the text it was drawn for.
        /// </summary>
        private void DrawString(SpriteFontBase font, string text, Vector2 position, Color colour,
            float scale = 1f, bool shadow = true)
        {
            Vector2 scaling = new(scale);

            if (shadow)
            {
                float alpha = colour.A / 255f;
                _game.OverlayBatch.DrawString(font, text, position + new Vector2(Scaled(HUD_SHADOW_OFFSET) * scale),
                    HUD_SHADOW * alpha, 0f, Vector2.Zero, scaling);
            }

            _game.OverlayBatch.DrawString(font, text, position, colour, 0f, Vector2.Zero, scaling);
        }

        #endregion

        #region Curves and colour

        /// <summary>Overshoots past 1 and settles back — the pop-in.</summary>
        private static float EaseOutBack(float t)
        {
            const float C1 = 2.8f;
            const float C3 = C1 + 1f;

            float u = t - 1f;
            return 1f + C3 * u * u * u + C1 * u * u;
        }

        /// <summary>Dips below 0 first and then accelerates — the wind-up before the throw.</summary>
        private static float EaseInBack(float t)
        {
            const float C1 = 1.35f;
            const float C3 = C1 + 1f;

            return C3 * t * t * t - C1 * t * t;
        }

        /// <summary>
        /// The HUD colour of a ball type: its own hue, lifted to full brightness and carried part of the way to
        /// white. Taken raw, half the eight are far too dark to read as text over a lit skyline — the 8-ball is
        /// a 0.045 grey — and the launch smear hit this exact wall and solved it the same way: keep the hue,
        /// raise the peak. Black has no hue to keep and comes out near-white, which is honest.
        /// </summary>
        private static Color TypeColor(BallType type)
        {
            Vector3 tint = BasicEffectParamsProvider.GetDiffuseTintByType(type);
            float peak = MathF.Max(tint.X, MathF.Max(tint.Y, tint.Z));

            if (peak > 0.001f) tint /= peak;

            tint = Vector3.Lerp(tint, Vector3.One, POPUP_TINT_TO_WHITE);

            return new Color(tint.X, tint.Y, tint.Z);
        }

        #endregion
    }
}
