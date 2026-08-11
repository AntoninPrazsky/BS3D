using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
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
    /// <b>The one accent is amber</b> (<see cref="HUD_ACCENT"/>). The HUD sits over twelve scene palettes whose
    /// colours are nothing alike, so a hue that reads as the game's own over one of them fights the next — the
    /// menu is greyscale for exactly this reason. Amber survives them all because it is the sun's own colour
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

        //A 1×1 white texel the cluster profile draws its bars and discs from. The old WhitePixel on BS3DGame was
        //retired in #76 (the crosshair got its own); rather than reach across to one, the HUD owns its own, made
        //lazily on the first draw when the device is guaranteed live. Tiny and lives with the HUD's lifetime.
        private Texture2D _pixel;

        internal PlayHud(BS3DGame game) => _game = game;

        private int Scaled(int designUnits) => _game.Scaled(designUnits);

        /// <summary>The HUD's own 1×1 white texel, created on first use.</summary>
        private Texture2D Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    _pixel = new Texture2D(_game.GraphicsDevice, 1, 1);
                    _pixel.SetData(new[] { Color.White });
                }
                return _pixel;
            }
        }

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
        /// A spring rather than a keyframed curve, for the reason <see cref="PreciseAim.Blend"/> is a blend and not a state
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

        #region The cluster profile (a side cut of the field)

        //The glass coming down at the cluster was its own warning by design, but in practice a translucent plate
        //sliding against a sky is nearly invisible while the eye is on the cluster — the ceiling flash and the
        //floor laser net both say so already, yet neither makes it obvious WHERE the glass stands relative to the
        //cluster and the death line. The side cut does: it is the one place the player can read the whole threat
        //at a glance. See docs/game-feedback.md "The in-play HUD" — the earlier "No ceiling counter" line is the
        //rule this exists to relax, for exactly the reason it foresaw.

        /// <summary>
        /// One ball's sample for the profile: where it sits in the cluster and what colour it is. Drawn straight
        /// off the live body poses the same frame the instanced balls are, so what the profile shows IS the
        /// simulation — a swaying cluster sways in the cut too.
        /// </summary>
        public struct BallMarker
        {
            public Vector3 World;
            public BallType Type;

            /// <summary>
            /// Whether this ball is still in the air — a shot on its way up to the cluster, or one the cluster
            /// has just let go of on its way down to the drain — rather than part of the hanging structure.
            /// Drawn as a RING instead of a filled disc, because the panel exists to show where the glass
            /// stands against the cluster and a ball that is not attached yet is not part of that answer: it
            /// has to be visible without being counted by the eye as cluster.
            /// </summary>
            public bool InFlight;

            /// <summary>
            /// Whether the body is actually moving <b>downwards</b> this frame. It is what tells a shot still
            /// climbing at the cluster from one that has been let go or has missed and is on its way to the
            /// drain — the two are otherwise the same thing to this panel, and they must not be drawn the same
            /// way under the death line (see the cull in <c>DrawClusterProfile</c>). Read from the live body
            /// rather than remembered, so the profile stays what it is: rebuilt from scratch every frame.
            /// </summary>
            public bool Falling;
        }

        /// <summary>
        /// Everything the profile needs to draw, built by the session from its own private state (this HUD owns
        /// no simulation). The live balls are passed alongside (as a span) because a span cannot live in a struct
        /// field — they are the session's reused backing array, handed in for this one draw.
        /// </summary>
        public struct ClusterProfile
        {
            /// <summary>World Y of the glass right now (it slides between the top and the death line).</summary>
            public float CeilingY;

            /// <summary>0…1, decaying: how recently the glass stepped. Drives the glass bar's flash.</summary>
            public float CeilingFlash;

            /// <summary>
            /// Whether that step was a <b>feed</b> — a tall level handing over more of its column because the
            /// player cleared a lot of it — rather than the shot count's pressure. The bar takes the alarm's
            /// red only for the latter; nothing has gone wrong in the former and the profile must not be the
            /// one place still shouting about it.
            /// </summary>
            public bool CeilingFeeding;

            /// <summary>World Y a ball centre must not cross — the loss line.</summary>
            public float DeathY;

            /// <summary>
            /// World Y of the glass at <b>rest</b> — the top of the panel, and the height the descent is read
            /// against. Handed over by the session rather than mirrored here, because it is <i>not</i> a
            /// constant: a field deep enough that its bottom level would start past the death line is raised
            /// bodily until it clears, and every level of it with it.
            /// <para>
            /// It used to be a private <c>FIELD_TOP_Y + CEILING_CLEARANCE</c> copied out of
            /// <c>GameplayScreen</c>, with a comment asking whoever retuned the original to remember this one.
            /// The copy was not merely fragile, it was already wrong: on Onion — 27 levels, raised some nine
            /// units — the whole cluster sat ABOVE this panel's top, and the per-ball cull below dropped
            /// nearly all of it, so the player saw a sliver of a bulb that fills the sky.
            /// </para>
            /// </summary>
            public float TopY;

            /// <summary>
            /// Half the field's diagonal in world units — the furthest a ball's projection onto the camera's right
            /// axis can reach from the centre. Derived from the field footprint by the session, so the horizontal
            /// axis maps to the cluster's actual width rather than a fudge that goes stale when a level's footprint
            /// changes (a ball at a field corner lands at the panel's edge, not bunched at its centre).
            /// </summary>
            public float HalfDepth;

            /// <summary>The gameplay camera's right axis (NOT the cinematic lens), so the cut does not turn while
            /// a drop cinematic swings the camera — the profile reads the pose the player aims with, frozen.</summary>
            public Vector3 CameraRight;
        }

        //Layout, in 2160p design units through Scaled() like everything else. A panel down the left edge — the
        //only space free in BOTH poses between the FPS line (top-left) and the balls-left readout (bottom-left).
        //FIXED size, so it does not shift as the cluster grows and shrinks: the glass's descent is what moves,
        //the frame that holds it does not. No border, no label, no backing plate — the balls and bars draw straight
        //over the scene, which keeps the panel from reading as a box plastered over the world.
        //
        //WIDTH IS THE ONLY LEVER on how big the cut reads, and #89 ("the panel reads as tiny") was the width
        //alone. The scale is isotropic — min(panelHeight/span, width/(2·halfDepth)) — and the two are nowhere
        //near each other: the field spans 13.16 world units vertically against a half-diagonal of 9.2 to 12.7,
        //so at the old 200 the width term gave 10.9 px per unit where the height term offered 98.8, and the
        //cut was drawn nine times smaller than its own panel. A ball came out 11 px across at 2160p; at 480 it
        //is 26, and the whole cut 344 px tall instead of 143. Raising the height did and does nothing at all.
        private const int PROFILE_WIDTH = 480;

        //Only ever a CAP, and one that cannot bite: it bounds panelHeight, which feeds a scale term the width
        //term always wins (see above) and a centre that works out to the viewport's own middle whatever this
        //is. It stays because it still bounds the one guard below — and because the day the width term stops
        //binding, this is what would take over.
        private const int PROFILE_HEIGHT = 1300;
        private const int PROFILE_MARGIN = HUD_MARGIN;       //clears the frame edge like the corner text does

        //How much of an in-flight ball's marker is hollowed out to make it a ring. Enough that the ring reads as
        //an outline rather than as a slightly dented disc, little enough that the type colour still carries.
        private const float PROFILE_FLIGHT_RING = 0.42f;

        /// <summary>
        /// How far below the death line a falling ball goes on being drawn, fading to nothing over the distance
        /// (#134). In <b>world units</b>, like everything else the panel measures, so it is the same fall at
        /// every resolution — about 22 px at a 1600×900 client and 52 at 2160p, which stays inside the space
        /// under the panel.
        /// <para>
        /// It is a distance and not a duration, which means the dissolve lasts as long as the ball's own speed
        /// says it should: a ball let go just over the line crawls out, one arriving from the top of the field
        /// snaps out. That is the honest reading, and it is also what keeps the panel stateless — a timed fade
        /// would have to remember each ball. A missed shot falling back out was measured crossing at <b>7.4
        /// units a second</b>, which is <b>16 frames</b> of visible dissolve at 60 — the alphas logged out of
        /// the draw ran 0.73 down to 0.04 in even steps. Faster arrivals are shorter in proportion.
        /// </para>
        /// </summary>
        private const float PROFILE_SINK_FADE = 2f;

        //The death line, in design units — thick enough to read as a deliberate mark at the panel's scale, since
        //a 2-pixel line over a lit skyline is sub-pixel and gone. It is a THRESHOLD and not a thing, so it has no
        //world thickness to be drawn at; the glass does, and is drawn at its own (see DrawClusterProfile).
        private const int PROFILE_DEATH_THICKNESS = 10;

        //The least the glass bar may thin to, whatever the panel's scale. Same argument as the death line's own
        //floor: on a short window pixelsPerUnit gets small, and the bar the whole panel is read against must not
        //be the thing that disappears.
        private const int PROFILE_GLASS_MIN_THICKNESS = 3;

        //The profile's red is a display-range red that shares the ceiling flash's hue: the 3D plate glows at
        //6.0/0.15/0.1 in LINEAR radiance because it has to out-shout the sky behind it, while this marker sits
        //over the scene and only has to read as the game's one alarm — so a hand-picked display colour in the same
        //red family, not a derived conversion. It shares that hue with the floor laser net and the 3D flash, so
        //the three are one warning said three ways.
        private static readonly Color PROFILE_ALARM = new(220, 60, 50);

        /// <summary>
        /// And the other thing a descent can mean, in the same display-space spirit: a deep blue for a step
        /// the game gave the player because they cleared a great deal of a tall column. The 3D plate and the
        /// cluster's wave already say it in linear radiance; this profile was the one place left still
        /// flashing red at good play.
        /// </summary>
        private static readonly Color PROFILE_FEED = new(58, 104, 235);

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
        /// Score, streak and balls left, in the corners, and the cluster's side cut down the left edge. The
        /// frame's centre belongs to the cluster and its bottom to the gun — and precise aim leans the lens in
        /// over the barrel, which fills more of it still — so the corners are the only space free in <b>both</b>
        /// poses and at every aspect, and the narrow strip between them down the left edge is where the side cut
        /// lives.
        /// <para>
        /// The side cut is the ceiling warning made spatial. The 3D glass sliding against a sky is nearly
        /// invisible (see docs/game-feedback.md "The ceiling announces itself"), the floor laser net says only
        /// that the end is near, and neither shows WHERE the glass stands. The cut does: the glass bar, the
        /// cluster hanging under it, and the death line are all in one frame, so a descent reads as the bar
        /// closing on the balls.
        /// </para>
        /// </summary>
        internal void Draw(ScoreKeeper score, ICamera camera, in ClusterProfile profile, ReadOnlySpan<BallMarker> balls)
        {
            _game.EnsureHudFonts();

            Viewport viewport = _game.GraphicsDevice.Viewport;
            int margin = Scaled(HUD_MARGIN);

            SpriteBatch batch = _game.OverlayBatch;
            batch.Begin();

            //The side cut, drawn first so the corner readouts and any incoming award pass over it rather than
            //under it — the same reason DrawAwards is last in this block.
            DrawClusterProfile(viewport, in profile, balls);

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
        /// <see cref="HUD_LOW_BALLS"/> the number grows a step and starts to breathe — motion is a stronger
        /// alarm than hue and it costs the HUD nothing over seven palettes. At <see cref="HUD_CRITICAL_BALLS"/>
        /// it takes the accent as well, which is the one place the HUD spends colour on something other than
        /// gain. (The step was a heavier weight until the readout moved to a single-weight display face; it is
        /// a size now, for the reason set out at <c>HUD_LOW_EMPHASIS</c>.)
        /// </para>
        /// </summary>
        private void DrawBallsLeft(ScoreKeeper score, Viewport viewport, int margin)
        {
            if (score.ShotsRemaining is not int left) return;

            bool low = left <= HUD_LOW_BALLS;
            bool critical = left <= HUD_CRITICAL_BALLS;

            SpriteFontBase font = low ? _game.HudFontScoreLoud : _game.HudFontScore;
            SpriteFontBase captionFont = low ? _game.HudFontLabelLoud : _game.HudFontLabel;

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

        /// <summary>
        /// The cluster's side cut: a narrow vertical strip down the left edge, showing the glass ceiling as a
        /// bar, the cluster's balls as dots, and the death line at the bottom. A descending ceiling reads here
        /// as the bar closing on the balls — the spatial warning the 3D glass sliding against a sky is not.
        /// <para>
        /// The horizontal position of each ball is its projection onto the camera's <b>right</b> axis (the same
        /// projection a sound is panned by), so the cut is a true side view from where the player stands: as the
        /// gun orbits, the cluster's outline turns in the cut the way it does on screen. The vertical position is
        /// world Y mapped linearly from the top of the field to the death line.
        /// </para>
        /// <para>
        /// Drawn inside the corner readouts' own <c>Begin/End</c> block (default SpriteBatch state) and first in
        /// it, so the numbers pass over it rather than under. The balls are drawn procedurally (no circle
        /// texture), and no per-frame allocations are made: the markers read straight off the span the session
        /// already filled.
        /// </para>
        /// </summary>
        private void DrawClusterProfile(Viewport viewport, in ClusterProfile profile, ReadOnlySpan<BallMarker> balls)
        {
            //Copied off the `in` parameter into plain locals: a local function cannot capture an `in` parameter,
            //and these are everything the projections below read off the profile.
            float ceilingY = profile.CeilingY;
            float deathY = profile.DeathY;
            Vector3 cameraRight = profile.CameraRight;
            float flash = profile.CeilingFlash;
            float halfDepth = profile.HalfDepth;

            //The panel spans the full field height, FIXED — it does not resize with the cluster. A panel that grew
            //and shrank as balls were shot and matched read as something wrong with the HUD rather than as the
            //field's frame, and it shifted under the eye every shot. The glass's descent is what moves; the panel
            //that frames it holds still. The vertical extent is the gameplay field the glass travels: its resting
            //place at the top down to the death line at the bottom — and that resting place is the SESSION's,
            //because a deep field is raised bodily off the death line (see ClusterProfile.TopY).
            float topY = profile.TopY;
            float bottomY = deathY;
            float span = topY - bottomY;
            if (span < 0.5f) return;

            int width = Scaled(PROFILE_WIDTH);
            int margin = Scaled(PROFILE_MARGIN);

            //Centred vertically in the gap between the FPS line and the balls-left readout, capped to fit.
            int maxHeight = viewport.Height - 2 * margin;
            int panelHeight = Math.Min(maxHeight, Scaled(PROFILE_HEIGHT));
            int panelX = margin;
            int panelTop = (viewport.Height - panelHeight) / 2;
            if (panelHeight <= width) return;

            //ISOTROPIC scale: the same pixels-per-world-unit on both axes, so the cluster keeps its true
            //proportions and the balls sit at their real spacing (a ball is 1 unit across, levels are 1/√2 apart).
            //halfDepth is the field's half-diagonal — the depth the side cut sees as the camera orbits.
            float safeDepth = halfDepth > 0.001f ? halfDepth : 1f;
            float scaleForHeight = panelHeight / span;
            float scaleForWidth = width / (2f * safeDepth);
            float pixelsPerUnit = MathF.Min(scaleForHeight, scaleForWidth);

            float centerX = panelX + width * 0.5f;
            float centerY = panelTop + panelHeight * 0.5f;
            float midY = (topY + bottomY) * 0.5f;

            float WorldToPanelY(float worldY) => centerY + (midY - worldY) * pixelsPerUnit;

            float WorldToPanelX(Vector3 world)
            {
                float offset = Vector3.Dot(world, cameraRight);
                return centerX + Math.Clamp(offset, -safeDepth, safeDepth) * pixelsPerUnit;
            }

            //A ball is 1 world unit across (radius 0.5). At this scale that is its drawn diameter, so the markers
            //sit edge-to-edge the way the real cluster actually packs.
            float markerRadius = pixelsPerUnit * 0.5f;

            SpriteBatch batch = _game.OverlayBatch;
            Texture2D pixel = Pixel;

            //No backing plate: the balls and the bars draw straight over the scene, which keeps the panel from
            //reading as a box plastered over the world. The markers carry their own contrast (their colours as
            //the lit ball actually shows them — see TypeColor — the dark types kept visible by the baked sheen).

            //The glass bar at its current height — NEUTRAL (the menu's own text colour) at rest, so it reads as
            //the ceiling the balls hang from rather than as a warning. It takes the alarm's red only on the flash,
            //squared exactly as the 3D plate's EmissiveTint is scaled by _ceilingFlash²: unmistakable on the frame
            //it steps, back to neutral before the slide finishes.
            //Drawn at the plate's OWN thickness, at the panel's own scale (#133). Everything else here is a
            //literal isotropic read of the world — a ball is one unit across and drawn one unit across — and this
            //bar was the one mark that was not: a fixed pixel figure with no relation to CeilingPlate.THICKNESS.
            //Being drawn thinner than the glass really is, its underside floated ABOVE where the glass's underside
            //actually is, and it was that shortfall alone that put a gap under the topmost balls: at rest they
            //hang FLUSH, the ball's top surface and the plate's underside agreeing to 0.001 of a unit (measured,
            //7.156 against 7.157 on One). At the 1600×900 client the bar came out 7 px where the plate is 11, and
            //the 2 px left over is exactly the gap the panel was showing.
            //
            //CLEARANCE is not what a first look suggests, and the trap is worth recording: it places the plate 2
            //units over the field's topmost level, but that is the layout's GRID position, and the constraints
            //then pull the cluster 0.272 up to hang off the glass. A probe on the first frame therefore reports a
            //gap of 0.273 that no player ever sees — sample after the cluster has settled.
            int glassThickness = Math.Max(PROFILE_GLASS_MIN_THICKNESS,
                (int)MathF.Round(CeilingPlate.THICKNESS * pixelsPerUnit));
            float flash2 = flash * flash;
            Color glassColor = Color.Lerp(BS3DGame.MENU_TEXT,
                profile.CeilingFeeding ? PROFILE_FEED : PROFILE_ALARM, flash2);
            int glassY = (int)MathF.Round(WorldToPanelY(ceilingY));
            batch.Draw(pixel, new Rectangle(panelX, glassY - glassThickness / 2, width, glassThickness), glassColor);

            //The cluster's balls, drawn as PROCEDURAL circles — no texture. Each is a stack of horizontal bars
            //(one per scanline of the disc), whose widths are the circle's half-chord at that height. A texture
            //needed premultiplied alpha to lose its white square and a separate asset to keep in step; a circle
            //built from the 1×1 pixel needs neither, and is exact at any size.
            for (int i = 0; i < balls.Length; i++)
            {
                BallMarker marker = balls[i];

                //Above the panel's window is a plain cull. Cluster balls cannot fail it, and a shot only ever
                //reaches up there past the glass, where it has nothing to say.
                if (marker.World.Y > topY) continue;

                //Under the death line, two different balls arrive here and only one of them may be drawn.
                //A shot on its way UP starts under the line — the gun stands on the island, well below it — and
                //drawing it there would say the one thing this panel's floor means: lost. A ball on its way
                //DOWN has crossed for real, and cutting it dead on the frame it crosses is the pop #134 is
                //about, when the ball is in fact still falling for another 36 units before KILL_PLANE_Y takes
                //it. So it goes on falling out of the panel and dissolves over PROFILE_SINK_FADE.
                //
                //Which of the two this is comes off the body's own velocity (BallMarker.Falling), not off the
                //list it came from: a shot that missed is still in _shotBalls on the way back down. Cluster
                //balls cannot get here at all — one below the death line has ended the level.
                float sink = bottomY - marker.World.Y;
                float alpha = 1f;

                if (sink > 0f)
                {
                    if (!marker.Falling || sink >= PROFILE_SINK_FADE) continue;

                    alpha = 1f - sink / PROFILE_SINK_FADE;
                }

                float px = WorldToPanelX(marker.World);
                float py = WorldToPanelY(marker.World.Y);

                //Multiplied through all four channels, which is the premultiplied alpha this batch's default
                //AlphaBlend wants — the same way the broken streak fades out.
                DrawDisc(pixel, batch, px, py, markerRadius, TypeColor(marker.Type) * alpha,
                    marker.InFlight ? markerRadius * (1f - PROFILE_FLIGHT_RING) : 0f);
            }

            //The death line at the bottom — the one place the alarm's red is ALWAYS shown, because it IS the
            //warning: the line a ball must not cross. Drawn last so it sits over anything that has reached it.
            int deathThickness = Math.Max(2, Scaled(PROFILE_DEATH_THICKNESS));
            int deathLineY = (int)MathF.Round(WorldToPanelY(deathY));
            batch.Draw(pixel, new Rectangle(panelX, deathLineY - deathThickness / 2, width, deathThickness), PROFILE_ALARM);
        }

        /// <summary>
        /// A filled disc drawn as a stack of horizontal bars — one per pixel row — each as wide as the circle's
        /// half-chord at that height. Built from the host's single white texel, so there is no circle texture to
        /// generate, premultiply or keep in step: the disc is exact at any radius and needs no alpha tricks to lose
        /// a square (it is only ever the bars it is made of).
        /// <para>
        /// <paramref name="innerRadius"/> above zero hollows it into a RING, which is how an in-flight ball is
        /// told apart from a settled one. It is the same scan, with the rows that cross the hole drawing their
        /// two remaining segments instead of one — so there is one primitive here rather than two that could
        /// drift apart, and a ring costs no more per row than a disc.
        /// </para>
        /// </summary>
        private static void DrawDisc(Texture2D pixel, SpriteBatch batch, float cx, float cy, float radius,
            Color color, float innerRadius = 0f)
        {
            int r = (int)MathF.Round(radius);
            if (r < 1)
            {
                //Sub-pixel: a single point is an honest marker, not a one-pixel circle that rounds away to nothing.
                //A ring has nothing left to hollow at this size either, so it draws the same point.
                batch.Draw(pixel, new Vector2(cx - 0.5f, cy - 0.5f), color);
                return;
            }

            float r2 = radius * radius;

            //Never let the ring close up into a disc or thin to nothing: below a pixel of wall the hole simply
            //is not drawn, which is the honest end of a ring rather than a row of stray dots.
            float inner = innerRadius > 0f && radius - innerRadius >= 1f ? innerRadius : 0f;
            float inner2 = inner * inner;

            for (int dy = -r; dy <= r; dy++)
            {
                //Half the chord at this row: √(r² − dy²). The bar spans the full width of the disc here.
                float half = MathF.Sqrt(r2 - dy * dy);
                int w = (int)MathF.Round(half * 2f);
                if (w <= 0) continue;

                int y = (int)MathF.Round(cy + dy);
                int left = (int)MathF.Round(cx - half);

                //Outside the hole (or no hole at all): one bar, the full chord.
                if (inner <= 0f || dy * dy >= inner2)
                {
                    batch.Draw(pixel, new Rectangle(left, y, w, 1), color);
                    continue;
                }

                //Across the hole: the two walls the row is left with, each the gap between the two chords.
                float innerHalf = MathF.Sqrt(inner2 - dy * dy);
                int wall = (int)MathF.Round(half - innerHalf);
                if (wall <= 0) continue;

                batch.Draw(pixel, new Rectangle(left, y, wall, 1), color);
                batch.Draw(pixel, new Rectangle((int)MathF.Round(cx + innerHalf), y, wall, 1), color);
            }
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
        /// The HUD colour of a ball type: what the ball's coloured gores actually show on screen, baked once
        /// through the render pipeline's own transform — the tint linearized the way the pattern shader does,
        /// lit at a nominal level, run through the tonemap's ACES curve and encoded to sRGB (#153). The old
        /// peak-normalize-and-lerp-to-white kept the hue but discarded every colour's relative darkness, and
        /// the 8-ball's (0.045, 0.045, 0.05) near-black came out an almost-white (0.93, 0.93, 1).
        /// </summary>
        private static Color TypeColor(BallType type)
        {
            int index = (int)type;
            return index < TYPE_COLORS.Length ? TYPE_COLORS[index] : Color.White;
        }

        //The nominal light the bake shines on a tint, plus the ambient sheen every ball wears on top of its
        //own colour. Calibrated against a screenshot of the real cluster (meadow, its default dome): the
        //baked red lands at (208, 46, 46) where the ball's gores measure (211, 44, 36), green (55, 208, 55)
        //against (38, 198, 35) — and the 8-ball keeps a readable (28, 28, 29) instead of vanishing, the sheen
        //standing in for the rim light the real ball is never seen without.
        private const float TYPE_LIT = 0.5f;
        private const float TYPE_SHEEN = 0.02f;

        //Indexed by the raw BallType byte (0 is unused — an empty cell is null, not a type), sized off the
        //enum so a ninth colour is baked the day it exists rather than falling to the fallback white.
        private static readonly Color[] TYPE_COLORS = BakeTypeColors();

        private static Color[] BakeTypeColors()
        {
            BallType[] types = Enum.GetValues<BallType>();

            byte highest = 0;
            foreach (BallType type in types) highest = Math.Max(highest, (byte)type);

            Color[] colors = new Color[highest + 1];

            foreach (BallType type in types)
            {
                Vector3 albedo = SrgbToLinear(BasicEffectParamsProvider.GetDiffuseTintByType(type));
                Vector3 mapped = AcesFilmic(albedo * TYPE_LIT + new Vector3(TYPE_SHEEN));

                colors[(byte)type] = new Color(LinearToSrgb(mapped.X), LinearToSrgb(mapped.Y), LinearToSrgb(mapped.Z));
            }

            return colors;
        }

        /// <summary>
        /// The pattern shader's own linearization of a gore colour (Jim Hejl's cubic fit of the sRGB curve,
        /// <c>InstancedModel.fx</c>) — the bake mirrors it so the HUD starts from the very albedo the ball is lit with.
        /// </summary>
        private static Vector3 SrgbToLinear(Vector3 c) =>
            c * (c * (c * 0.305306011f + new Vector3(0.682171111f)) + new Vector3(0.012522878f));

        /// <summary>Krzysztof Narkowicz's ACES fit, per channel — the tonemap resolve's curve (<c>Tonemap.fx</c>).</summary>
        private static Vector3 AcesFilmic(Vector3 x) => new(AcesChannel(x.X), AcesChannel(x.Y), AcesChannel(x.Z));

        private static float AcesChannel(float x) =>
            Math.Clamp(x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f), 0f, 1f);

        /// <summary>The resolve's exact piecewise sRGB encode (<c>Tonemap.fx</c>), one channel.</summary>
        private static float LinearToSrgb(float c) =>
            c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

        #endregion
    }
}
