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
    /// The readouts <see cref="PlayHud.Draw"/> can be asked for, in the three groups the endings treat apart: a lost
    /// level keeps the <see cref="Field"/> under its result page (#639), a won one keeps the <see cref="Account"/> through
    /// its cleared beat while the rest fades (#862).
    /// </summary>
    [Flags]
    internal enum HudParts
    {
        /// <summary>The side cut of the cluster and the magazine's queue: what the field is.</summary>
        Field = 1,

        /// <summary>The streak, balls left, the power-up chips, the tutorial card and the sleeping gun's letters: playing. (A won
        /// level's beat fades these, but the card is gone before it: a decided level ends it, #700.)</summary>
        Play = 2,

        /// <summary>The score, the awards flying into it and the takeover's skip hint: what the celebration still reads.</summary>
        Account = 4,

        All = Field | Play | Account,
    }

    /// <summary>
    /// The in-play readout — score, streak and balls left in the corners, and the numbers that fly from the
    /// balls they were earned on into the score. Drawn with <see cref="SpriteBatch"/> and the menu's own Inter
    /// after the tonemap resolve, in display space, like the crosshair and for the same reason: a HUD
    /// downsampled with the scene would be soft. Under a page's blur it goes into the pipeline's overlay
    /// layer instead and is softened with the frame (#438) — <see cref="GameplayScreen.Draw"/>'s business,
    /// not this class's: it draws the same picture into whatever is bound.
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
    /// <b>The one accent is amber</b> (<see cref="HUD_ACCENT"/>). The HUD sits over fifteen scene palettes whose
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

        /// <summary>
        /// Testing only (the <c>streak=</c> argument): the multiplier the streak readout should <b>show</b>,
        /// overriding the keeper's. Null — every real run — reads the keeper as it always did.
        /// <para>
        /// It is deliberately a <i>display</i> override and touches no scoring: pinning the keeper's own
        /// multiplier would change what a shot is worth, and a test lever that alters the thing being tested is
        /// worse than no lever. It exists because the capped state added in #180 cannot be reached by a script
        /// at all — it needs <see cref="ScoreKeeper.MaxMultiplier"/> consecutive scoring shots, i.e. somebody
        /// actually being good at the game — so without it the top tier could be neither photographed nor
        /// compared against the tier below it. Same reasoning as <c>celebrate</c>, <c>lasers</c> and
        /// <c>stars=</c>.
        /// </para>
        /// <para>
        /// <b>It holds until a shot breaks it</b> (#845): the first shot that resolves without scoring breaks the pinned
        /// streak exactly as it would break a real one (<see cref="BreakStreak"/> or <see cref="EndStreakQuietly"/> lift the
        /// pin), so a script can photograph the break of a ×5 it could never have earned.
        /// </para>
        /// </summary>
        internal int? ForcedMultiplier { get; set; }

        /// <summary>The multiplier the HUD works from — the keeper's, unless a test has pinned one.</summary>
        private int MultiplierOf(ScoreKeeper score) => ForcedMultiplier ?? score.Multiplier;

        /// <summary>
        /// The streak the readout shows (#845): the keeper's multiplier, or the <c>streak=</c> lever's while it holds. What
        /// a break says was lost and what the Swap's hint asks is running, so the two agree with the badge on screen.
        /// </summary>
        internal int StreakShown(ScoreKeeper score) => MultiplierOf(score);

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

        //⚠ AN OFFSET COPY DARKENS ONE SIDE OF A GLYPH, AND THAT IS ONLY HALF THE JOB. Over a bright sky the
        //bullet above is right, because a sky is smooth and the offset alone separates the figure from it. Over
        //the island's own CONCRETE DECK it is not: the deck reads around 150 grey, the readout is 244, and the
        //three sides the offset does not cover are near-white on near-white with a texture running under them.
        //The bottom-left ball count sits on that deck in every scene and the owner reported it as hard
        //to read there — which it was, on the capture, at a glance.
        //
        //So every readout is also backed by a blurred black copy of its own glyphs, centred on them, which puts
        //the separation on all four sides. It is FontStashSharp's Blurry, the same effect the gain flash uses,
        //so it costs one more cached atlas variant and nothing per frame. Deliberately TIGHTER than that flash
        //(which runs HUD_GLOW_BLUR at 1.14 of the text): a flash is light coming off the glyph and grows around
        //it, this is the ground behind the glyph and has to hug the letterform or it becomes the plate the
        //bullet above refuses. Two passes because one pass of a blur is always fainter than the glyph it came
        //from — the same arithmetic HUD_GLOW_PASSES is set by, in the dark direction.
        private const int HUD_BACKING_BLUR = 12;
        private const int HUD_BACKING_PASSES = 2;
        private static readonly Color HUD_BACKING = new(0, 0, 0, 130);

        //The caption under the ball count. NOT MENU_TEXT_DIM, whose own comment says what it is for — asides
        //"always on a dark plate" — and this one has no plate within a thousand pixels of it: at 146 grey on a
        //150 deck it was the least legible thing the game draws. Bright enough to hold its own over the
        //concrete, still plainly subordinate to the number it labels, which is the hierarchy it is there for.
        private static readonly Color HUD_CAPTION = new(206, 206, 206);

        //--- The magazine strip (#236) -----------------------------------------------------------------------
        //The loaded queue as flat discs, next shot first. It exists because the queue CANNOT be read off the
        //gun from the pose the game plays: CannonRig's own note records that drawn opaque, the pane fills the
        //whole slot from this camera — the barrel hides its own muzzle end, so the round in the notch shows
        //only as the small ellipse of its cap and the four behind it are read through glass that keeps about
        //0.38 of what is behind it. Naming a colour off a small dark ellipse is the difficulty, and no palette
        //change makes an ellipse bigger (#246 measured the palette half and could only fix that half).
        //
        //The head is LARGER and carries a ring, which is the whole of "this one, right now" — the thing #175
        //used to spend a brightness pulse on the 3D ball to say. That pulse is gone: it said "this one" by
        //washing the round towards white, which is the very colour the player was trying to read (#236). What
        //replaced it is a coloured mark on the gun (BallGlow's halo, the muzzle collar since #425), and this
        //strip. Since #252 no loaded round pulses at all — the breathing belongs to the gun's mark, in one place.
        private const int HUD_MAG_HEAD_RADIUS = 56;
        private const float HUD_MAG_REST_SCALE = 0.64f;

        /// <summary>
        /// The clear space between one disc and the next — <b>between what is drawn</b>, so a disc's dark halo
        /// counts and the head's ring counts, and this is the daylight the eye actually sees.
        /// <para>
        /// It measured fill edge to fill edge until the owner asked for the row to breathe, and that is why it
        /// had to be re-based rather than simply raised: at 24 the halos left only 24 − 2·<see cref="HUD_MAG_RIM"/>
        /// = 10 between them, so four rounds read as one run of touching blobs — and the head was worse than
        /// tight, it was <i>wrong</i>. Its ring stands <see cref="HUD_MAG_RING_GAP"/> + <see cref="HUD_MAG_RING_THICKNESS"/>
        /// outside the halo, 18 units past the fill the gap was measured from, so the mark that says "this one,
        /// right now" overlapped the next round's halo by a unit. A constant named for a gap cannot leave the
        /// biggest thing in the row eating three quarters of it.
        /// </para>
        /// </summary>
        private const int HUD_MAG_GAP = 32;

        //There was a HUD_MAG_INSET here for one revision — how far clear of the ball count the strip started,
        //once the owner saw the first halo sitting almost on the "balls left" caption. Moving the strip to the
        //other corner made it unnecessary rather than made it right: nothing is beside the strip there to be
        //clear of, and it is anchored to the frame's own margin like every other corner readout.

        //The dark halo every disc sits in. The strip has to read over a white glacier and a neon skyline alike,
        //and a light ball on a light sky is the case a coloured disc alone loses — the same duty HUD_SHADOW
        //does for the text, in the shape a disc needs.
        private const int HUD_MAG_RIM = 7;
        private static readonly Color HUD_MAG_RIM_COLOR = new(0, 0, 0, 190);

        //The head's ring, outside its halo. Light rather than accent-coloured: the accent is the score's and
        //the low-ammo warning's, and a third meaning on it would make all three vaguer. The gap is what stops
        //it reading as a fat outline on the disc instead of a mark around it.
        private const int HUD_MAG_RING_GAP = 5;
        private const int HUD_MAG_RING_THICKNESS = 6;

        //What the head's own outline is worth. A near-black round is the case this carries: TypeColor keeps
        //each type's real darkness on purpose (#153 refused peak-normalising), so the 8-ball prints at about
        //(22, 20, 16) and a filled disc of it inside a dark halo is a hole. Outlined, it reads exactly the way
        //the ball itself does — by its white gores against the black — which is the same answer arrived at
        //from the same constraint.
        private const int HUD_MAG_OUTLINE = 3;
        private static readonly Color HUD_MAG_OUTLINE_COLOR = new(244, 244, 244, 150);

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
        /// A spring rather than a keyframed curve, for the reason <see cref="Prazsky.BS3D.PreciseAim.Blend"/> is a blend and not a state
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

        //The same string split into single-character strings, rebuilt only when the text is (#180). The capped
        //badge is drawn glyph by glyph so a sweep can travel along it, and a substring per character per frame
        //would be exactly the per-frame allocation the rest of this block exists to avoid.
        private string[] _streakGlyphs = { "×", "1" };

        /// <summary>
        /// Balls left as of the previous frame, so a decrement can be seen. −1 means "no baseline yet", which is
        /// what a level that has just been built has: seeding the count would otherwise punch on frame one.
        /// </summary>
        private int _ballsShown = -1;

        private int _multiplierShown = 1;

        //THE BREAK IS AN EVENT (#845), because the streak is the one thing on the HUD whose LOSS is the news. Until #845 the
        //lost value slid 44 units and faded in 0.45 s, in a corner the eyes are not on, a lost x5 the same as a lost x3, and
        //the owner's playtest said it passed almost unnoticed. Now the badge CRACKS (it swells, jolts, flashes white-hot and
        //dark cracks run across it) and SHATTERS: its glyphs fly apart at the swollen size and tumble down with shards of it,
        //cooling to grey, while "x5 lost" stands where it stood and the falling run in ProceduralAudio sounds. Every figure
        //scales with what was lost (StreakCues.Severity, 0.5 at x3 and 1 at the cap): a x5 swells and shakes more, throws more
        //pieces further and holds longer. The SESSION starts it (BreakStreak) rather than this class reading the keeper,
        //because only the session knows when a break must stay silent: the level decided, or its last shot spent, where it
        //would play over the result (StreakCues.BreakIsTold).
        //
        //⚠ SIZE IS WHAT MAKES IT SEEN. The first cut squeezed the badge as it cracked (the ball count's inward kick) and threw
        //label-sized pieces that cooled to grey at once: captured at 1600x900 the whole break was a smudge twenty pixels high
        //and pale on a pale sky, the very thing the issue was about. The badge is the HUD's smallest figure, so it grows as it
        //breaks, the pieces keep its size and its colour for a while, and every shard carries a dark backing of its own.
        //
        //Every piece is a closed-form throw (a start, a velocity and gravity) from a hash of the break's seed and its index,
        //so a frame of the break draws from the age alone: nothing is listed, nothing allocated, and every frame rate draws
        //the same flight.
        private int _breakLost;                         //the multiplier lost, 0 while nothing is breaking
        private float _breakAge;
        private float _breakLength;                     //seconds the whole break lasts, by what was lost
        private float _breakPhase;                      //the cap's sweep where it stood, so a x5's pieces keep their colours
        private int _breakSeed;                         //a new one per break, so no two shatter alike
        private string _breakText = string.Empty;       //"x5", the badge that broke
        private string[] _breakGlyphs = System.Array.Empty<string>();
        private string _breakCaption = string.Empty;    //"x5 lost"; all three built once per break and never per frame

        private const float STREAK_BREAK_CRACK = 0.16f;         //the swell and the jolt, before the pieces leave
        private const float STREAK_BREAK_TIME_LOW = 0.8f;       //seconds, at a severity of 0 ...
        private const float STREAK_BREAK_TIME_HIGH = 1.4f;      //... and of 1 (x3: 1.1 s, x5: 1.4 s)
        private const float STREAK_BREAK_SWELL_LOW = 0.6f;      //how much the badge grows as it cracks, at a severity of 0.5 ...
        private const float STREAK_BREAK_SWELL_HIGH = 0.9f;     //... and of 1 (x3 to 1.6 times, x5 to 1.9 times)
        private const int STREAK_BREAK_SHAKE_LOW = 8;           //design units of the jolt's swing
        private const int STREAK_BREAK_SHAKE_HIGH = 20;
        private const float STREAK_BREAK_SHAKE_HZ = 31f;
        private const int STREAK_CRACK_THICKNESS = 3;           //design units, the dark lines across the badge
        private static readonly Color STREAK_CRACK = new(10, 8, 6, 230);

        //The pieces, in design units and seconds: thrown sideways and up, then pulled down; the shards faster and spun
        //harder than the glyphs, since they are smaller and lighter. Everything keeps the colour it wore for a beat and cools
        //to STREAK_LOST over STREAK_BREAK_COOL, shrinks to STREAK_BREAK_SHRINK of the swollen size, and goes out over the last
        //STREAK_BREAK_FADE of the flight
        private const float STREAK_BREAK_GRAVITY = 2600f;
        private const float STREAK_BREAK_THROW = 520f;
        private const float STREAK_BREAK_LIFT = 560f;
        private const float STREAK_BREAK_SPIN = 4.5f;           //radians a second at most, a glyph
        private const float STREAK_BREAK_COOL = 0.6f;
        private const float STREAK_BREAK_SHRINK = 0.75f;
        private const float STREAK_BREAK_FADE = 0.45f;          //a fraction of the flight
        private const int STREAK_SHARDS_LOW = 6;
        private const int STREAK_SHARDS_HIGH = 16;
        private const float STREAK_SHARD_SPEED = 1050f;
        private const float STREAK_SHARD_SPIN = 14f;
        private const int STREAK_SHARD_LENGTH = 46;
        private const int STREAK_SHARD_THICKNESS = 13;
        private const float STREAK_SHARD_BACKING = 1.5f;        //the dark copy under each shard, this much larger

        //And what is left, "x5 lost": in by STREAK_CAPTION_IN, held, and out over the last STREAK_CAPTION_OUT of the break,
        //sinking STREAK_CAPTION_SINK as it goes, in the cap's loud face and a little larger for a lost cap. It is born in the
        //badge's own colour and cools to STREAK_LOST, the streak's warmth going out of it: never the alarm's red, which
        //belongs to the glass
        private const float STREAK_CAPTION_IN = 0.12f;
        private const float STREAK_CAPTION_OUT = 0.35f;
        private const int STREAK_CAPTION_SINK = 14;
        private const float STREAK_CAPTION_CAP_SCALE = 1.15f;
        private static readonly Color STREAK_LOST = new(214, 214, 222);

        //THE LOSS IS ALSO SAID WHERE THE EYE IS (#875). The corner shatter above is the record, but the owner's playtest of
        //2026-10-15 never saw it on a 3840x1600 monitor: while aiming the eye rests on the middle of the frame, so the falling
        //run sounded with nothing to explain it. So the same call that starts the run also puts "STREAK LOST" and the lost
        //multiplier over the aim point (StreakCues.LostCaption), laid out as an award is (LayOutAward: its face, its blurred
        //backing, the multiplier in the accent beside the words) and drawn LAST in its call, over the awards, because it is the news
        //(a won level's cleared beat draws the awards in a later call: see the end of Draw).
        //
        //It is in full on its FIRST frame, with the sound, and only fades at its end (StreakCues.LostCaptionPresence): a
        //fade in would put it behind the very sound it explains. It slams in swollen and settles (LOST_CAPTION_POP_FROM,
        //over LOST_CAPTION_POP), is white-hot and cools to the badge's grey (LOST_CAPTION_COOL), and drifts up and away from the
        //aim as it goes. A few hundred milliseconds (StreakCues.LostCaptionSeconds), never blurred, its BOTTOM edge fixed
        //above the crosshair at its most swollen (StreakCues.LOST_CAPTION_CLEARANCE) so the pop grows upwards and the aim
        //point is never under it.
        //
        //It is the break's own clock, not the corner's: the corner's gives way at once to a new streak, but the words stay
        //to be read. It gives way to a new AWARD instead (AddAward): the newer number takes the read, as every award does
        //(#846), and the reverse holds too, BreakStreak sends any award still being read on its way (HandOffReads), so a
        //number born over a cell is never left under the words.
        private float _lostAge;
        private float _lostLength;                      //seconds the caption stands, 0 while there is none
        private int _lostMultiplier;                    //the multiplier lost, for its size
        private Popup _lostCaption;                     //"STREAK LOST" and "x3", built once per break and never per frame

        private const float LOST_CAPTION_POP_FROM = 1.3f;       //how swollen it arrives, as a share of its size ...
        private const float LOST_CAPTION_POP = 0.12f;           //... and the seconds it takes to settle
        private const float LOST_CAPTION_COOL = 0.3f;           //seconds from white-hot to STREAK_LOST
        private const int LOST_CAPTION_LIFT = 70;               //design units it drifts up over its time, away from the aim

        //The multiplier stands at the words' own size, where an award's is two thirds of its figure's: the × is a small glyph
        //in this face and at two thirds it read as a bullet ("STREAK LOST •3", photographed at 1600x900)
        private const float LOST_CAPTION_SUFFIX_SHARE = 1f;

        //One shard of the badge, a sliver baked once at this many texels long (STREAK_SHARD_TEXELS by a third of it), drawn
        //scaled and turned: a 1x1 texel stretched into a rectangle would have hard, stepped edges at every angle
        private const int STREAK_SHARD_TEXELS = 48;
        private Texture2D _shard;

        //THE CAP IS ITS OWN TIER (#180). Below MaxMultiplier the streak warms towards the one accent and that is
        //the whole of it, which is why a x5 used to read as a slightly hotter x2 — climb is normalised 0..1
        //against the cap, so the ramp has nowhere higher to go BY CONSTRUCTION. What it needed was not a hotter
        //amber but a different state, and this is it: at the cap the number changes FACE (the loud label, so it
        //stops being the smallest figure on the HUD), takes a travelling spectrum instead of a flat colour, and
        //flares once on arrival.
        //
        //A spectrum here does NOT reopen the palette-clash problem the single-accent rule was written against,
        //and the reason is that the rule is about what the HUD WEARS. Amber is worn continuously over fifteen
        //scenes and so has to survive all of them; this is two glyphs in one corner, only at the cap, only while
        //a perfect streak is being held, and it is gone the moment a shot misses. It is the rarest state the
        //in-play HUD has, and the one moment worth spending colour the HUD does not otherwise own.
        private bool _streakCapped;
        private float _streakCapFlare;                  //1 at the moment of capping, decaying to 0

        //Seconds for one full sweep of the ramp, and the phase each glyph is offset by. The stagger is what
        //makes it TRAVEL rather than the whole number changing colour together — a figure that pulses through
        //hues in unison reads as a fault, where a sweep running along it reads as energy moving through it.
        private const float STREAK_CAP_CYCLE = 1.15f;

        //A sixth of the ramp per glyph. It was a third, and at that spacing the two characters of a badge sat
        //two full stops apart and read as two unrelated colours rather than as one sweep passing through — the
        //stagger has to be small enough that neighbouring glyphs are neighbours on the ramp as well.
        private const float STREAK_CAP_STAGGER = 0.16f;

        //The one-shot flare when the cap is reached: an extra kick on the spring the streak already uses, and
        //a brief compounding of the glow on top of it. Short, because it marks the arrival — the sweep is what
        //carries the state afterwards.
        private const float STREAK_CAP_KICK = 1.5f;
        private const float STREAK_CAP_FLARE_TIME = 0.6f;
        private const int STREAK_CAP_GLOW_PASSES = 4;   //on top of HUD_GLOW_PASSES, at the flare's peak

        //And the glow a capped streak carries once the flare has gone. The pulse's own heat ENDS — it is the
        //"this just changed" signal — but the cap does not, so without a floor a held x5 would stop glowing
        //between shots and drop back to looking like an ordinary readout in a brighter font.
        private const float STREAK_CAP_GLOW_FLOOR = 0.58f;

        //THE RAMP IS A HOT ARC AND NOT A RAINBOW, and that is a measured decision rather than a tasteful one.
        //It ran the full wheel first — red, orange, yellow, green, cyan, violet — and the cool half FAILED
        //exactly as the single-accent rule predicts: photographed over the meadow, the cyan stop was very
        //nearly invisible against the sky, so the badge blinked out for a third of every sweep. That is the
        //palette-clash problem the class doc records, arriving on the one element allowed to break the rule.
        //
        //So the arc runs red -> orange -> gold -> white-hot -> pink -> magenta, which is the warm half plus the
        //magentas: every stop holds against a blue sky, against the meadow's green, against grey stone and
        //against the Moon's black, and there is no stop that any backdrop can swallow. It still reads as the
        //accent BOILING OVER rather than as an unrelated rainbow bolted onto the corner, which is the right
        //thing for it to say — and the white-hot stop is what makes it read as heat rather than as decoration.
        private static readonly Color[] STREAK_CAP_RAMP =
        {
            new(255, 104, 92),     //red
            new(255, 168, 60),     //orange
            new(255, 226, 118),    //gold
            new(255, 252, 236),    //white-hot
            new(255, 150, 214),    //pink
            new(240, 108, 176)     //magenta
        };

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

            public string Text;             //"+1 200"
            public string Suffix;           //"×3" beside the figure, the completion bonus's "12 UNUSED BALLS" over it, or null at ×1

            /// <summary>What this is carrying, withheld from the readout until it lands there.</summary>
            public int Points;

            /// <summary>The colour of the balls that were cut — see the class doc. The completion bonus, which no ball
            /// earned, is born in the HUD's own white.</summary>
            public Color Tint;

            /// <summary>Seconds since birth on the play clock. <b>Negative</b> while it waits its turn - the completion bonus
            /// (<see cref="AddBonus"/>), a cut's award (<see cref="WorldWait"/>): not drawn yet, though its points are
            /// already withheld from the readout.</summary>
            public float Age;

            /// <summary>A cut's award (#767): the WORLD's seconds it still waits before it is born (<see cref="AwardZoom.CUT_WORLD_WAIT"/>),
            /// its <see cref="Age"/> held negative and still meanwhile. Zero for every other award.</summary>
            public float WorldWait;

            /// <summary>Its zoom (#846): the top size in scores, how long the zoom in takes, and how long it is read
            /// before it leaves — set from its points at birth, the hold cut short when a newer award is born
            /// (<see cref="AwardZoom.HandOff"/>).</summary>
            public float Peak, Birth, Hold;

            /// <summary>The completion bonus (#846, #881), which is what the unused balls were worth: born over the aim
            /// point in screen space rather than projected from a cell, with its caption stacked over the figure instead
            /// of beside it, and waiting its turn (<see cref="AddBonus"/>).</summary>
            public bool IsBonus;
        }

        private readonly List<Popup> _popups = new();

        //The size and the legs of each award are AwardZoom's (#846); what stays here is how it is placed on the screen
        private const float POPUP_RISE = 2.2f;          //world units over the read, so it drifts with the cluster
        private const int POPUP_SUFFIX_GAP = 10;        //design units at the old popup type's size, scaled with the award

        /// <summary>How far the completion bonus drifts up over its read, in design units, away from the aim point it is born
        /// over: the lost streak's words drift the same way for the same reason (<see cref="LOST_CAPTION_LIFT"/>).</summary>
        private const int POPUP_BONUS_RISE = 60;

        /// <summary>The bonus's caption, over its figure, as a share of the figure's size: the figure is the hero and the
        /// words are what it counts. A multiplier stands at two thirds (<see cref="POPUP_SUFFIX_SHARE"/>) beside its figure;
        /// the fifteen letters of "29 UNUSED BALLS" over a six-glyph figure at a third of its size make a block as wide as
        /// the figure (241 px of its 282 at 1600x900), where at two thirds beside it the row would be about half the frame.</summary>
        private const float POPUP_BONUS_CAPTION_SHARE = 0.32f;

        /// <summary>The least an award keeps between itself and the frame's edge, in design units (#846): a zoomed number
        /// born on a cell near the top of the frame is held inside it rather than cut off.</summary>
        private const int POPUP_EDGE = 40;

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
            /// Whether the ball <b>came down</b> to where it is: the cluster let it go, or it is a shot that has
            /// been above the death line. It is what tells a shot still climbing at the cluster from one that has
            /// been let go or has missed and is on its way to the drain — the two are otherwise the same thing to
            /// this panel, and they must not be drawn the same way under the death line (see the cull in
            /// <c>DrawClusterProfile</c>). Decided once per ball and remembered on it (#822), not read off the
            /// body's velocity each frame as it was: in the funnel every contact turns the velocity's sign over, so
            /// a ball rolling down the wall was drawn on one frame and culled on the next, and one at rest was not
            /// drawn at all.
            /// </summary>
            public bool FromAbove;

            /// <summary>
            /// Whether the ball is a Cutter (#766): its ring is the Cutter's accent, never <see cref="Type"/>, the colour
            /// dealt into its slot, which nothing shows.
            /// </summary>
            public bool Cutter;
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
        /// The alarm-red ring on a cluster ball that hangs under the death line (#643), as a share of the marker's
        /// radius — heavier than a shot's ring, because it has to read as a warning over the ball's own colour.
        /// </summary>
        private const float PROFILE_CROSSED_RING = 0.3f;

        /// <summary>
        /// The <b>least</b> distance below the death line a falling ball goes on being drawn, fading to nothing
        /// over it (#134). In <b>world units</b>, like everything else the panel measures.
        /// <para>
        /// <b>Since #428 it is only the floor</b>: the fall actually drawn runs down to just above the balls-left
        /// readout (see <see cref="DrawClusterProfile"/>), which is a screen distance converted at the panel's own
        /// scale. It was the whole fall until then, about 22 px at a 1600×900 client and 52 at 2160p — and the
        /// owner's report was exactly that: a ball "vanishes almost as soon as it crosses the line", when it is
        /// still falling for another 36 units. This stays as the answer for a window so short the readout sits
        /// above the line.
        /// </para>
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

        /// <summary>
        /// How much of that fall a ball is drawn at full strength before it starts to dissolve (#428). The owner
        /// asked to SEE the balls falling on towards the drain, so the first part of the fall is plain falling;
        /// a fade that started at the line would dim them the moment they crossed it and read as the old vanish,
        /// only slower.
        /// </summary>
        private const float PROFILE_SINK_HOLD = 0.6f;

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

            //The magazine strip's figures for this level's style (#737), baked now rather than mid-play
            PrepareFigures();

            _scorePulse.Reset();
            _streakPulse.Reset();
            _ballsPulse.Reset();
            _tutorialPulse.Reset();

            _ballsShown = score.ShotsRemaining ?? -1;
            _multiplierShown = score.Multiplier;
            _breakLost = 0;
            _lostLength = 0f;
            _swapHint = false;
            _swapHintAge = 0f;
            _swapPulse.Reset();
            _cutPulse.Reset();
            _streakCapped = score.Multiplier >= ScoreKeeper.MaxMultiplier;
            _streakCapFlare = 0f;
            _clock = 0f;
        }

        /// <summary>
        /// A shot has scored. <paramref name="type"/> is the colour of the group it completed — the shot ball's
        /// own, since a match is by definition three of one colour touching.
        /// </summary>
        /// <remarks>
        /// <b>Several at once are handed on, not stacked or merged</b> (#846). One landing is one award, a bomb's chain
        /// included (its victims are scored with the landing that set it off), so two awards on screen together are two
        /// shots landing close behind each other. Each still has to be read on its own cell, with its own colour and
        /// the multiplier it was won at; a zoomed number standing over another one is not readable, and a merged total
        /// would have to say one colour and one multiplier for two. So the newer one takes the read and every award
        /// still being read leaves for the corner now (<see cref="AwardZoom.HandOff"/>): only one big number at a time,
        /// in the order they landed, each paid as it arrives.
        /// </remarks>
        /// <param name="cutter">True for a cutter's strike (#766): tinted with the Cutter's accent, never its dealt colour, and born only
        /// once its severing has been seen (<see cref="AwardZoom.CUT_WORLD_WAIT"/>, #767).</param>
        internal void AddAward(Vector3 world, ScoreAward award, BallType type, bool cutter = false)
        {
            HandOffReads();

            FadeLostCaption();

            _popups.Add(new Popup
            {
                World = world,

                //Through ScoreText, like every other figure the game prints — the popup and the corner score
                //are on screen at the same moment, so they have to be one number written one way. The class
                //carries the incident that says why.
                Text = "+" + ScoreText.Of(award.Points),

                //The multiplier PRINTED is the one that was applied, which is why Landed returns it rather than
                //leaving this to read the property afterwards — by then it has been raised for the next shot
                Suffix = award.Multiplier > 1 ? "×" + award.Multiplier.ToString(CultureInfo.InvariantCulture) : null,

                Points = award.Points,
                Tint = cutter ? CutterTint : TypeColor(type),

                //A cut's award waits for its severing to be seen (#767): born at once it stood, zoomed to its largest, exactly
                //over the slash running across the storey it was paid for (photographed, Elephant). It waits on the world's
                //clock, as the slash lives (WorldWait); every other award is born on its landing, as #846 has it.
                Age = cutter ? -1f : 0f,
                WorldWait = cutter ? AwardZoom.CUT_WORLD_WAIT : 0f,
                Peak = AwardZoom.Peak(award.Points),
                Birth = AwardZoom.Birth(award.Points),
                Hold = AwardZoom.Hold(award.Points),
            });
        }

        /// <summary>
        /// The newer number takes the read from the words over the aim point too (#875): they begin to fade now, from
        /// wherever they have got to, rather than standing under an award zooming in. An award over a cell (<see cref="AddAward"/>)
        /// and the completion bonus, which stands over the aim point itself (<see cref="AddBonus"/>), both ask.
        /// </summary>
        private void FadeLostCaption()
        {
            if (_lostLength > 0f)
                _lostLength = MathF.Min(_lostLength, _lostAge + StreakCues.LOST_CAPTION_FADE_SECONDS);
        }

        /// <summary>
        /// A level-ending cut's award, born now (#767): called when the field has just emptied and the cut's award still waits
        /// on the world's clock (<see cref="AwardZoom.CutWaitLeft"/>). With no drop cinematic over the clear, that wait would run
        /// under the cut's slow motion and put the number's birth, its read and its flight past the cleared beat, over the
        /// completion bonus and the result page. Under a drop cinematic the world runs at game time and the beat is held
        /// for the shot, so the award keeps its wait.
        /// </summary>
        internal void BirthWaitingAwards(bool dropCinematic)
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];
                if (popup.WorldWait <= 0f) continue;

                popup.WorldWait = AwardZoom.CutWaitLeft(popup.WorldWait, levelCleared: true, dropCinematic);
                if (popup.WorldWait <= 0f) popup.Age = 0f;

                _popups[i] = popup;
            }
        }

        /// <summary>
        /// The completion bonus (#846, #881): what the unused balls were worth, said in words over the aim point and flown
        /// into the score like any award. It is born <b>over the middle of the frame</b>, where the player's eye rests while
        /// aiming and clear of the crosshair (<see cref="BonusSource"/>), not out of the balls-left count in the corner, which
        /// is neither where the eye is nor where the figure is going. It is captioned over its figure with what it counts
        /// (<see cref="AwardZoom.BonusCaption"/>: <c>12 UNUSED BALLS</c> over <c>+1 560</c>) and read for
        /// <see cref="AwardZoom.BONUS_HOLD"/> before it shrinks and goes. It is often the biggest figure of the level, and
        /// the one the player earned by playing well, so it is shown like one.
        /// <para>
        /// It <b>waits a moment</b> (<see cref="AwardZoom.BonusWait"/>, a tenth of a second at most): the shot that cleared the
        /// field has just been awarded, and the bonus's birth sends that award on its way as a newer award would
        /// (<see cref="AgePopups"/>), once it has been read for <see cref="AwardZoom.MIN_READ"/>. Its points are withheld from
        /// the readout from now, so the score does not count up ahead of it, and the words of a streak just lost, which stand
        /// where it is born, begin to fade (<see cref="FadeLostCaption"/>).
        /// </para>
        /// </summary>
        /// <param name="points">What was awarded, never zero (a level with no budget awards none and adds no popup).</param>
        /// <param name="unusedBalls">The balls the points were awarded for, <c>ScoreKeeper.UnusedShotsAwarded</c>.</param>
        internal void AddBonus(int points, int unusedBalls)
        {
            float readLeft = 0f;
            for (int i = 0; i < _popups.Count; i++)
                readLeft = MathF.Max(readLeft, _popups[i].Hold - _popups[i].Age);

            FadeLostCaption();

            _popups.Add(new Popup
            {
                Text = "+" + ScoreText.Of(points),
                Suffix = AwardZoom.BonusCaption(unusedBalls),
                Points = points,
                Tint = BS3DGame.MENU_TEXT,
                Age = -AwardZoom.BonusWait(readLeft),
                Peak = AwardZoom.Peak(points),
                Birth = AwardZoom.Birth(points),
                Hold = AwardZoom.BONUS_HOLD,
                IsBonus = true,
            });
        }

        /// <summary>Every award still being read leaves for the corner, once it has been read for
        /// <see cref="AwardZoom.MIN_READ"/>: a newer one is taking the read (see <see cref="AddAward"/>) — or the
        /// completion bonus is, and is spared (<paramref name="sparingTheBonus"/>).</summary>
        private void HandOffReads(bool sparingTheBonus = false)
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];
                if (popup.Age >= popup.Hold || (sparingTheBonus && popup.IsBonus)) continue;

                popup.Hold = AwardZoom.HandOff(popup.Age, popup.Hold);
                _popups[i] = popup;
            }
        }

        /// <summary>
        /// A streak of <paramref name="lost"/> has just been lost (#845): the badge cracks and shatters, and "×5 lost" stands
        /// where it stood. Called by the session, which has already decided the player is to be told
        /// (<see cref="StreakCues.BreakIsTold"/>); a second break over one still playing starts afresh. Lifts the
        /// <c>streak=</c> pin (<see cref="ForcedMultiplier"/>), which held until this shot.
        /// </summary>
        internal void BreakStreak(int lost)
        {
            ForcedMultiplier = null;
            if (lost <= 1) return;

            //Where the cap's sweep stands now, so a broken x5's glyphs keep the colours they were wearing
            _breakPhase = _clock / STREAK_CAP_CYCLE;
            _breakLost = lost;
            _breakAge = 0f;
            _breakLength = MathHelper.Lerp(STREAK_BREAK_TIME_LOW, STREAK_BREAK_TIME_HIGH, StreakCues.Severity(lost));
            _breakSeed++;

            _breakText = "×" + lost.ToString(CultureInfo.InvariantCulture);
            _breakCaption = _breakText + " lost";
            _breakGlyphs = new string[_breakText.Length];
            for (int i = 0; i < _breakText.Length; i++) _breakGlyphs[i] = _breakText[i].ToString();

            //And said over the aim point, on this same call as the sound (#875): an award still being read leaves for the
            //corner now, as a newer award would send it, so no number stands under the words
            HandOffReads();
            _lostCaption = new Popup { Text = StreakCues.LostCaption, Suffix = _breakText };
            _lostMultiplier = lost;
            _lostAge = 0f;
            _lostLength = StreakCues.LostCaptionSeconds(lost);
        }

        /// <summary>
        /// How much a lost streak of <paramref name="lost"/> grows as it cracks (#845): <see cref="STREAK_BREAK_SWELL_LOW"/>
        /// for a lost ×3 and <see cref="STREAK_BREAK_SWELL_HIGH"/> for a lost cap, in proportion between.
        /// </summary>
        private static float BreakSwell(int lost) =>
            MathHelper.Lerp(STREAK_BREAK_SWELL_LOW, STREAK_BREAK_SWELL_HIGH, Math.Clamp(StreakCues.Severity(lost) * 2f - 1f, 0f, 1f));

        /// <summary>
        /// A streak has ended where the player is not to be told (#845, <see cref="StreakCues.BreakIsTold"/>: the level
        /// decided, or its last shot spent): the badge simply goes, and the <c>streak=</c> pin lifts as a break would lift it.
        /// </summary>
        internal void EndStreakQuietly()
        {
            ForcedMultiplier = null;
            _breakLost = 0;

            //Nor are the words over the aim point left standing over the result (#875): a caption of a break a moment ago goes too
            _lostLength = 0f;
        }

        #endregion

        #region Update

        /// <summary>
        /// Advances the count-up, the springs and the popups. On the play clock, so all three freeze with a
        /// pause rather than running on behind a menu the player has stopped the game with.
        /// </summary>
        /// <param name="swapHint">Whether the Swap chip points at the cure this frame (#845, <see cref="StreakCues.SwapIsTheCure"/>
        /// as the session reads it): a streak is running, the round in the bore has nowhere to score and the Swap would load
        /// one that has.</param>
        /// <param name="worldElapsed">The frame's seconds of the world's time (slow motion included), which a cut's award waits
        /// on (<see cref="Popup.WorldWait"/>).</param>
        internal void Update(float elapsed, float worldElapsed, ScoreKeeper score, bool swapHint = false)
        {
            _clock += elapsed;

            AgePopups(elapsed, worldElapsed);
            AgeLostCaption(elapsed);
            ChaseScore(elapsed, score);
            WatchStreak(elapsed, score);
            WatchBalls(score);
            WatchSwapHint(elapsed, swapHint);

            _scorePulse.Update(elapsed);
            _streakPulse.Update(elapsed);
            _ballsPulse.Update(elapsed);
            _tutorialPulse.Update(elapsed);
            _swapPulse.Update(elapsed);
            _cutPulse.Update(elapsed);
        }

        /// <summary>
        /// Ages the popups and <b>delivers</b> the ones that have arrived: reaching the corner is what pays the
        /// points into the readout and kicks it. Cause and effect land on the same frame, which is the whole
        /// point of flying them there.
        /// </summary>
        private void AgePopups(float elapsed, float worldElapsed)
        {
            bool waitedBorn = false;

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];

                //A cut's award counts the world's seconds down instead, its age held, and is born when they run out
                if (popup.WorldWait > 0f)
                {
                    popup.WorldWait -= worldElapsed;
                    if (popup.WorldWait <= 0f)
                    {
                        popup.WorldWait = 0f;
                        popup.Age = 0f;
                        waitedBorn = true;
                    }

                    _popups[i] = popup;
                    continue;
                }

                waitedBorn |= popup.Age < 0f && popup.Age + elapsed >= 0f;
                popup.Age += elapsed;

                if (popup.Age >= AwardZoom.Lifetime(popup.Hold))
                {
                    _popups.RemoveAt(i);
                    _scorePulse.Kick(SCORE_KICK);
                }
                else _popups[i] = popup;
            }

            //The completion bonus, or a cut's award (#767), has waited its turn, and its birth takes the read as a newer award's
            //does: an award still being read leaves for the corner now (the bonus itself spared)
            if (waitedBorn) HandOffReads(sparingTheBonus: true);
        }

        /// <summary>The words over the aim point age on the play clock, with everything else the HUD says, and are gone at their end (#875).</summary>
        private void AgeLostCaption(float elapsed)
        {
            if (_lostLength <= 0f) return;

            _lostAge += elapsed;
            if (_lostAge >= _lostLength) _lostLength = 0f;
        }

        private void ChaseScore(float elapsed, ScoreKeeper score)
        {
            //What the readout is allowed to know: everything the keeper has, less whatever is still in the air
            //on its way here. Recomputed from the list rather than carried as a running total, so it is exactly
            //self-correcting — once the last popup has landed the two agree by construction, whatever happened
            //in between (a level torn down mid-flight, the completion bonus still waiting for its turn).
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
            int multiplier = MultiplierOf(score);

            if (multiplier > _multiplierShown && multiplier > 1) _streakPulse.Kick(STREAK_KICK);

            //REACHING THE CAP is its own event, and it is taken on the transition rather than on the state so a
            //streak held at x5 across a dozen shots flares once and then simply runs. The extra kick lands on
            //the same spring the ordinary climb kicked a line above, and springs ADD (see Pulse) — so capping
            //reads as one bigger swell rather than as a second animation restarting the first.
            bool capped = multiplier >= ScoreKeeper.MaxMultiplier;

            if (capped && !_streakCapped) _streakCapFlare = 1f;
            if (capped && !_streakCapped) _streakPulse.Kick(STREAK_CAP_KICK);

            _streakCapped = capped;

            if (_streakCapFlare > 0f)
                _streakCapFlare = MathF.Max(0f, _streakCapFlare - elapsed / STREAK_CAP_FLARE_TIME);

            _multiplierShown = multiplier;

            //A break plays out on its own clock, and gives way at once to a new streak (a second shot already in the air
            //that scores): the badge belongs to the streak that is running. A streak dropping to x1 WITHOUT a break (the
            //level decided, its last shot spent) simply goes, because the session judged there was no one to tell.
            if (_breakLost > 0)
            {
                _breakAge += elapsed;
                if (_breakAge >= _breakLength || multiplier > 1) _breakLost = 0;
            }
        }

        /// <summary>
        /// Whether the Swap chip is pointing at the cure (#845), and for how long: the session's answer. Its onset heats the
        /// chip's own pulse once, a flare that is what catches the eye; the breathing that follows keeps it found.
        /// </summary>
        private void WatchSwapHint(float elapsed, bool swapHint)
        {
            if (swapHint && !_swapHint)
            {
                _swapHintAge = 0f;
                _swapPulse.Kick(0f);
            }

            _swapHint = swapHint;
            if (swapHint) _swapHintAge += elapsed;
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
        /// <param name="queue">The loaded rounds, slot 0 first — <see cref="DrawMagazine"/>'s subject (#236).
        /// A span over a buffer the caller keeps, like <paramref name="balls"/>, so a frame costs nothing.</param>
        /// <param name="parts">
        /// Which readouts this call draws (<see cref="HudParts"/>): all of them in play. A lost level's ending (#639)
        /// draws only the <b>field</b> — the side cut and the magazine — so the player can read, under the result page,
        /// what beat them. A won level's cleared beat (#862) draws the <b>account</b> — the score, the awards flying into
        /// it and the drop's skip hint — while the rest fades out through the overlay layer in a call of its own.
        /// </param>
        /// <param name="snores">The sleeping gun's letters in the air (#230), drawn over the arena under the corner
        /// readouts. Empty on every frame but a nap.</param>
        /// <param name="swapCharges">The level's Swap charges left (#213), or −1 for a level that offers none — which
        /// draws nothing, so a first-chapter player is shown no key that does nothing.</param>
        /// <param name="brakeCharges">The level's Brake charges left (#213), or −1 for a level that offers none — the ceiling's
        /// brake chip, drawn on the row above the Swap's.</param>
        /// <param name="cutCharges">The level's Cut charges left (#213), or −1 for a level that offers none — the anchor cut's
        /// chip, drawn on the row above the Brake's.</param>
        /// <param name="brakeBlock">Why the brake cannot act now, if it cannot (#692, #844): the glass is moving, or there is
        /// nothing it would lift (no pressure step to give back, or on a tall level a lift out of reach, #843).</param>
        /// <param name="cutBlocked">Whether the round in the bore cannot be turned into a cutter now (#844).</param>
        /// <param name="kinds">What kind each loaded round is, slot 0 first, beside <paramref name="queue"/>: a special one
        /// (the wildcard, the Cutter) turns a burst of rays behind its disc (#820). Empty draws none.</param>
        /// <param name="queueFrom">The colour each round is dissolving out of (#705), beside <paramref name="queue"/>.</param>
        /// <param name="queueFade">How far each has come, 0 just begun to 1 settled: the disc cross-fades from
        /// <paramref name="queueFrom"/> to <paramref name="queue"/> as the ball in the bore dithers. Empty fades none.</param>
        internal void Draw(ScoreKeeper score, ICamera camera, in ClusterProfile profile, ReadOnlySpan<BallMarker> balls,
            ReadOnlySpan<BallType> queue, Tutorial tutorial, HudParts parts = HudParts.All,
            ReadOnlySpan<BS3D.Effects.DozingGun.Z> snores = default, int swapCharges = -1, int brakeCharges = -1, int cutCharges = -1,
            BrakeBlock brakeBlock = BrakeBlock.None, bool cutBlocked = false, ReadOnlySpan<BallKind> kinds = default, ReadOnlySpan<BallType> queueFrom = default,
            ReadOnlySpan<float> queueFade = default)
        {
            //No card's ball unless this frame's card asks for one (#735) - asked by the call that draws the card
            if ((parts & HudParts.Play) != 0) _tutorialBallRadius = 0f;

            _game.EnsureHudFonts();

            Viewport viewport = _game.GraphicsDevice.Viewport;
            int margin = Scaled(HUD_MARGIN);

            SpriteBatch batch = _game.OverlayBatch;
            batch.Begin();

            //The sleeping gun's Zs (#230), first of all: they belong to the arena, so every readout passes over them
            if ((parts & HudParts.Play) != 0) DrawSnores(camera, viewport, snores);

            //The side cut, drawn first so the corner readouts and any incoming award pass over it rather than
            //under it — the same reason DrawAwards is last in this block.
            if ((parts & HudParts.Field) != 0) DrawClusterProfile(viewport, in profile, balls, BallsLeftTop(score, viewport, margin));

            //The score, top right — the FPS line owns the top left (InfoRenderer draws after this, in
            //base.Draw). Its right edge is the pivot everything above hangs off, so the margin holds while the
            //number pulses and while it grows a digit.
            int shown = (int)MathF.Round(_scoreShown);
            if (shown != _scoreTextFor)
            {
                _scoreTextFor = shown;
                _scoreText = ScoreText.Of(shown);
            }

            float heat = _scorePulse.Heat;
            Vector2 scoreSize = _game.HudFontScore.MeasureString(_scoreText);
            Vector2 scoreAnchor = new(viewport.Width - margin, margin + scoreSize.Y * 0.5f);

            //Measured whichever parts are drawn: the card and the awards are placed off it
            if ((parts & HudParts.Account) != 0)
                DrawPulsed(_game.HudFontScore, _scoreText, scoreAnchor, new Vector2(1f, 0.5f), _scorePulse.Scale,
                    Color.Lerp(BS3DGame.MENU_TEXT, HUD_ACCENT, heat), heat);

            if ((parts & HudParts.Play) != 0)
            {
                DrawStreak(score, viewport, margin, scoreAnchor.Y + scoreSize.Y * 0.5f + Scaled(HUD_LINE_GAP));
                DrawBallsLeft(score, viewport, margin);
            }
            if ((parts & HudParts.Field) != 0) DrawMagazine(queue, kinds, queueFrom, queueFade, score, viewport, margin);
            if ((parts & HudParts.Play) != 0)
            {
                DrawSwap(swapCharges, brakeCharges, cutCharges, brakeBlock, cutBlocked, tutorial.OnGamepad, MultiplierOf(score), viewport, margin);

                //The card is given the score's left edge rather than measuring it again: it is what bounds the
                //strip the card may stand in (#461), and one measurement cannot disagree with the other.
                DrawTutorial(tutorial, viewport, margin, scoreAnchor.X - scoreSize.X);
            }

            if ((parts & HudParts.Account) != 0)
            {
                DrawSkipHint(tutorial, viewport, margin);

                //Last, so the numbers coming in pass over the readouts rather than under them
                DrawAwards(camera, viewport, scoreAnchor - new Vector2(scoreSize.X * 0.5f, 0f));
            }

            //The lost streak, said over the aim point (#875), last of what this call draws: over the awards in flight when one call
            //draws both (all of play). A won level's cleared beat draws the Play and the Account parts in two calls (#862), the
            //account's, sharp, after the playing readouts' fading layer, so an award there passes over what is left of the
            //caption - the right way round: the caption cannot be born once the level is decided (EndStreakQuietly), and a clear's
            //own award has already started its fade (FadeLostCaption), so the newer number takes the read
            if ((parts & HudParts.Play) != 0) DrawStreakLost(viewport);

            batch.End();
        }

        /// <summary>
        /// The streak, under the score, and only while it is actually running — held back at ×1 so it reads as
        /// something the player is carrying rather than as furniture. It warms towards the accent as it climbs,
        /// so ×5 is visibly hotter than ×2 without a second number to read; and when it is lost it breaks
        /// (<see cref="DrawStreakBreak"/>, #845), because its <i>loss</i> is the event and a thing that simply
        /// vanishes between frames is a thing the player never saw.
        /// </summary>
        private void DrawStreak(ScoreKeeper score, Viewport viewport, int margin, float top)
        {
            int multiplier = MultiplierOf(score);
            if (multiplier <= 1)
            {
                if (_breakLost > 0) DrawStreakBreak(viewport, margin, top);
                return;
            }

            if (multiplier != _streakTextFor)
            {
                _streakTextFor = multiplier;
                _streakText = "×" + multiplier.ToString(CultureInfo.InvariantCulture);

                //Split once, here, rather than per frame: the capped badge is drawn glyph by glyph, and a
                //substring per character per frame would be a managed allocation on the gameplay path.
                _streakGlyphs = new string[_streakText.Length];
                for (int i = 0; i < _streakText.Length; i++) _streakGlyphs[i] = _streakText[i].ToString();
            }

            //THE CAP IS A DIFFERENT STATE, not a hotter shade of the same one (#180).
            bool capped = multiplier >= ScoreKeeper.MaxMultiplier;

            //The loud face at the cap, so the multiplier stops being the smallest figure on the HUD at exactly
            //the moment it is the most worth reading — which was half of what #180 was about.
            SpriteFontBase font = capped ? _game.HudFontLabelLoud : _game.HudFontLabel;

            Vector2 size = font.MeasureString(_streakText);
            Vector2 anchor = new(viewport.Width - margin, top + size.Y * 0.5f);
            float scale = _streakPulse.Scale;

            if (!capped)
            {
                DrawPulsed(font, _streakText, anchor, new Vector2(1f, 0.5f), scale, StreakColour(multiplier), _streakPulse.Heat);
                return;
            }

            //Capped: the glow first, compounded by the arrival flare and tinted from the sweep's own current
            //colour so the halo belongs to the number rather than staying amber under a violet glyph. The glow
            //is floored at a value of its own, because the pulse's heat ends and the cap does not — a capped
            //streak that stopped glowing between shots would drop back to looking ordinary.
            Vector2 at = anchor - size * new Vector2(1f, 0.5f) * scale;
            float phase = _clock / STREAK_CAP_CYCLE;

            float flare = _streakCapFlare * _streakCapFlare;
            float glow = MathF.Max(_streakPulse.Heat, STREAK_CAP_GLOW_FLOOR + flare * (1f - STREAK_CAP_GLOW_FLOOR));
            int passes = HUD_GLOW_PASSES + (int)MathF.Round(STREAK_CAP_GLOW_PASSES * flare);

            DrawGlow(font, _streakText, at, size, scale, glow, SampleCapRamp(phase), passes);
            DrawGradientString(font, _streakGlyphs, at, scale, phase, 1f);
        }

        /// <summary>
        /// How hot a streak of <paramref name="multiplier"/> is running below the cap: from the readout's white towards the
        /// accent over the whole range the rule allows, so the ramp keeps meaning the same thing if
        /// <see cref="ScoreKeeper.MaxMultiplier"/> is ever retuned.
        /// </summary>
        private static Color StreakColour(int multiplier) =>
            Color.Lerp(BS3DGame.MENU_TEXT, HUD_ACCENT, (multiplier - 1f) / MathF.Max(1f, ScoreKeeper.MaxMultiplier - 1f));

        /// <summary>The colour glyph <paramref name="index"/> of the broken badge wore: the cap's sweep where it stood, or the streak's warmth.</summary>
        private Color BreakGlyphColour(int index, bool capped) =>
            capped ? SampleCapRamp(_breakPhase - index * STREAK_CAP_STAGGER) : StreakColour(_breakLost);

        /// <summary>
        /// A lost streak breaking where it stood (#845; the field notes above <see cref="_breakLost"/>). First the CRACK
        /// (<see cref="STREAK_BREAK_CRACK"/>): the badge swells about its right edge, jolts sideways, flashes white-hot and
        /// dark cracks run across it. Then it SHATTERS: its glyphs are thrown apart at the swollen size (the × out to the
        /// left, the digit dropping) and tumble down with shards of it, keeping the colours they wore for a beat and then
        /// cooling to grey and going out, while "×5 lost" rises where the badge stood. Every throw is closed-form in the
        /// break's age, so a frame costs draws and nothing else.
        /// </summary>
        private void DrawStreakBreak(Viewport viewport, int margin, float top)
        {
            SpriteBatch batch = _game.OverlayBatch;

            //Pixels per design unit as a float: the throws are fractional figures, which Scaled's whole units would round
            float unit = Scaled(1000) / 1000f;
            float severity = StreakCues.Severity(_breakLost);
            bool capped = _breakLost >= ScoreKeeper.MaxMultiplier;
            SpriteFontBase font = capped ? _game.HudFontLabelLoud : _game.HudFontLabel;

            Vector2 size = font.MeasureString(_breakText);
            Vector2 anchor = new(viewport.Width - margin, top + size.Y * 0.5f);
            float swollen = 1f + BreakSwell(_breakLost);

            //The words first, so the pieces fly over them
            DrawBreakCaption(anchor, capped);

            float age = _breakAge;

            if (age < STREAK_BREAK_CRACK)
            {
                //The swell, fast and then easing, with a sideways shake dying over it: white-hot at the instant and cooling
                //back towards the colour it wore
                float k = age / STREAK_BREAK_CRACK;
                float scale = MathHelper.Lerp(1f, swollen, 1f - (1f - k) * (1f - k));
                float swing = MathHelper.Lerp(STREAK_BREAK_SHAKE_LOW, STREAK_BREAK_SHAKE_HIGH, severity) * unit;
                float shake = MathF.Sin(age * MathHelper.TwoPi * STREAK_BREAK_SHAKE_HZ) * swing * (1f - k);
                float hot = 1f - k;

                Vector2 at = anchor + new Vector2(shake, 0f) - size * new Vector2(1f, 0.5f) * scale;
                DrawGlow(font, _breakText, at, size, scale, 0.6f + 0.4f * hot, Color.White,
                    HUD_GLOW_PASSES + (int)MathF.Round(2f * severity));

                float x = at.X;
                for (int i = 0; i < _breakGlyphs.Length; i++)
                {
                    DrawString(font, _breakGlyphs[i], new Vector2(x, at.Y),
                        Color.Lerp(BreakGlyphColour(i, capped), Color.White, 0.75f * hot), scale);
                    x += font.MeasureString(_breakGlyphs[i]).X * scale;
                }

                //The cracks come in the second half of the swell, once the flash has been seen
                if (k > 0.45f) DrawCracks(batch, at, size * scale, severity, unit);
                return;
            }

            float flight = age - STREAK_BREAK_CRACK;
            float life = flight / MathF.Max(0.01f, _breakLength - STREAK_BREAK_CRACK);
            float fade = 1f - MathHelper.SmoothStep(0f, 1f, (life - (1f - STREAK_BREAK_FADE)) / STREAK_BREAK_FADE);
            float cool = MathHelper.SmoothStep(0f, 1f, flight / STREAK_BREAK_COOL);
            float fall = 0.5f * STREAK_BREAK_GRAVITY * unit * flight * flight;
            float reach = 0.7f + 0.6f * severity;
            float pieceScale = swollen * MathHelper.Lerp(1f, STREAK_BREAK_SHRINK, life);

            //Where the swollen badge stood: the throws start from its glyphs
            Vector2 swollenSize = size * swollen;
            Vector2 origin = anchor - swollenSize * new Vector2(1f, 0.5f);

            //The shards, under the glyphs: thrown from anywhere on the badge, up and out to the left over to up and right
            //(the badge stands against the frame's right edge, so the room for the throw is to its left), then pulled down.
            //Each on a dark copy of itself, larger, so a pale shard still reads against a pale sky
            Texture2D shard = ShardTexture();
            Vector2 shardOrigin = new(shard.Width * 0.5f, shard.Height * 0.5f);
            Vector2 centre = origin + swollenSize * 0.5f;
            int shards = (int)MathF.Round(MathHelper.Lerp(STREAK_SHARDS_LOW, STREAK_SHARDS_HIGH, severity));

            for (int j = 0; j < shards; j++)
            {
                float a = Hash01(j, 11), b = Hash01(j, 12), c = Hash01(j, 13), d = Hash01(j, 14), e = Hash01(j, 15);

                float angle = MathHelper.Lerp(-0.95f * MathHelper.Pi, -0.2f * MathHelper.Pi, a);
                float speed = STREAK_SHARD_SPEED * unit * (0.35f + 0.65f * b) * reach;
                Vector2 start = centre + new Vector2((c - 0.5f) * swollenSize.X, (d - 0.5f) * swollenSize.Y) * 0.8f;
                Vector2 at = start + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * flight + new Vector2(0f, fall);

                float turn = e * MathHelper.TwoPi + (b - 0.5f) * 2f * STREAK_SHARD_SPIN * flight;
                Vector2 stretch = new Vector2(STREAK_SHARD_LENGTH * (0.5f + 0.9f * c) / shard.Width,
                    STREAK_SHARD_THICKNESS * (0.6f + 0.6f * d) / shard.Height) * unit * MathHelper.Lerp(1f, STREAK_BREAK_SHRINK, life);
                Color tint = Color.Lerp(BreakGlyphColour(j % Math.Max(1, _breakGlyphs.Length), capped), STREAK_LOST, cool) * fade;

                batch.Draw(shard, at, null, HUD_BACKING * fade, turn, shardOrigin, stretch * STREAK_SHARD_BACKING, SpriteEffects.None, 0f);
                batch.Draw(shard, at, null, tint, turn, shardOrigin, stretch, SpriteEffects.None, 0f);
            }

            //The glyphs: the first (the ×) thrown out to the left, the rest dropping with a little sideways drift, each
            //turning as it goes
            float left = origin.X;
            for (int i = 0; i < _breakGlyphs.Length; i++)
            {
                Vector2 glyphSize = font.MeasureString(_breakGlyphs[i]);
                Vector2 start = new(left + glyphSize.X * swollen * 0.5f, origin.Y + glyphSize.Y * swollen * 0.5f);
                left += glyphSize.X * swollen;

                float side = i == 0 ? -1f : 0.3f;
                float vx = side * STREAK_BREAK_THROW * unit * (0.6f + 0.4f * Hash01(i, 2)) * reach;
                float vy = -STREAK_BREAK_LIFT * unit * (0.5f + 0.5f * Hash01(i, 3)) * reach;
                Vector2 at = start + new Vector2(vx, vy) * flight + new Vector2(0f, fall);

                float turn = (i == 0 ? -1f : 1f) * STREAK_BREAK_SPIN * (0.4f + 0.6f * Hash01(i, 4)) * flight;
                Color tint = Color.Lerp(BreakGlyphColour(i, capped), STREAK_LOST, cool) * fade;

                DrawTurnedGlyph(font, _breakGlyphs[i], at, glyphSize, turn, pieceScale, tint);
            }
        }

        /// <summary>
        /// "×5 lost", where the badge stood (#845): in over <see cref="STREAK_CAPTION_IN"/> once the crack is done, held, and
        /// out over the last <see cref="STREAK_CAPTION_OUT"/> of the break, sinking a little as it goes; in the cap's loud face,
        /// a lost cap's a little larger, born in the colour the badge wore and cooling to <see cref="STREAK_LOST"/>.
        /// </summary>
        private void DrawBreakCaption(Vector2 anchor, bool capped)
        {
            float age = _breakAge - STREAK_BREAK_CRACK;
            if (age <= 0f) return;

            float rise = MathHelper.SmoothStep(0f, 1f, age / STREAK_CAPTION_IN);
            float leave = MathHelper.SmoothStep(0f, 1f, 1f - (_breakLength - _breakAge) / STREAK_CAPTION_OUT);
            float alpha = rise * (1f - leave);
            if (alpha <= 0.01f) return;

            SpriteFontBase font = _game.HudFontLabelLoud;
            Vector2 size = font.MeasureString(_breakCaption);

            //A small pop as it arrives, the HUD's own way of saying "this just happened"
            float scale = MathHelper.Lerp(1.15f, 1f, rise) * (capped ? STREAK_CAPTION_CAP_SCALE : 1f);
            float sink = leave * Scaled(STREAK_CAPTION_SINK);

            Color tint = Color.Lerp(BreakGlyphColour(0, capped), STREAK_LOST, MathHelper.SmoothStep(0f, 1f, age / STREAK_BREAK_COOL));
            Vector2 at = new(anchor.X - size.X * scale, anchor.Y - size.Y * 0.5f * scale + sink);

            DrawString(font, _breakCaption, at, tint * alpha, scale);
        }

        /// <summary>
        /// "STREAK LOST" and the multiplier that went, over the aim point (#875; the field notes above <see cref="_lostLength"/>):
        /// laid out as an award is (<see cref="LayOutAward"/>, <see cref="DrawAwardAt"/>), in full from its first frame, slamming in
        /// swollen and settling, white-hot cooling to grey, its bottom edge held <see cref="StreakCues.LOST_CAPTION_CLEARANCE"/> above the
        /// middle of the frame (so the crosshair, wherever it swells to, is never under it) and drifting up as it fades. <b>The
        /// clearance is in the crosshair's scale, not the HUD's</b> (<see cref="StreakCues.ClearancePixels"/>): the HUD's own shrinks
        /// with the width of a narrow window and the crosshair's does not, so at any window shape the caption clears the mark's full
        /// strain. Nothing is allocated: the two strings were made when the break began.
        /// </summary>
        private void DrawStreakLost(Viewport viewport)
        {
            if (_lostLength <= 0f) return;

            float presence = StreakCues.LostCaptionPresence(_lostAge, _lostLength);
            if (presence <= 0.01f) return;

            float pop = MathHelper.Lerp(LOST_CAPTION_POP_FROM, 1f,
                HudEase.OutBack(MathHelper.Clamp(_lostAge / LOST_CAPTION_POP, 0f, 1f)));
            AwardLayout layout = LayOutAward(_lostCaption, StreakCues.LostCaptionSize(_lostMultiplier) * pop, LOST_CAPTION_SUFFIX_SHARE);
            Vector2 extent = layout.Extent;

            //Rising a little over its time, fast and then easing, away from the aim; the BOTTOM edge is what is placed, so the
            //pop grows upwards and never towards the crosshair
            float life = MathHelper.Clamp(_lostAge / _lostLength, 0f, 1f);
            float lift = 1f - (1f - life) * (1f - life);
            float bottom = viewport.Height * 0.5f - StreakCues.ClearancePixels(viewport.Height) - Scaled(LOST_CAPTION_LIFT) * lift;
            Vector2 centre = KeepInFrame(new Vector2(viewport.Width * 0.5f, bottom - extent.Y * 0.5f), extent, viewport);

            Color tint = Color.Lerp(Color.White, STREAK_LOST, MathHelper.SmoothStep(0f, 1f, _lostAge / LOST_CAPTION_COOL));
            DrawAwardAt(_lostCaption, layout, centre, tint, presence);
        }

        /// <summary>
        /// The dark cracks across the badge at the end of its jolt (#845): two for a lost ×3, three for a lost cap, each a
        /// jagged line of three strokes from its top to its bottom, placed by the break's own hash.
        /// </summary>
        private void DrawCracks(SpriteBatch batch, Vector2 at, Vector2 size, float severity, float unit)
        {
            int cracks = (int)(1.5f + 1.5f * severity);
            float thickness = MathF.Max(1f, STREAK_CRACK_THICKNESS * unit);

            for (int c = 0; c < cracks; c++)
            {
                float across = (c + 0.5f) / cracks + (Hash01(c, 21) - 0.5f) * 0.25f / cracks;
                Vector2 from = new(at.X + across * size.X, at.Y + size.Y * 0.12f);

                for (int s = 1; s <= 3; s++)
                {
                    float jag = (Hash01(c * 4 + s, 22) - 0.5f) * 0.3f * size.X / cracks;
                    Vector2 to = new(at.X + across * size.X + jag, at.Y + size.Y * (0.12f + 0.76f * s / 3f));

                    Vector2 stroke = to - from;
                    batch.Draw(Pixel, from, null, STREAK_CRACK, MathF.Atan2(stroke.Y, stroke.X), new Vector2(0f, 0.5f),
                        new Vector2(stroke.Length(), thickness), SpriteEffects.None, 0f);

                    from = to;
                }
            }
        }

        /// <summary>
        /// One glyph of the broken badge, turned by <paramref name="turn"/> about its own centre at <paramref name="centre"/> and
        /// drawn at <paramref name="scale"/>, on the HUD's blurred black backing
        /// (<see cref="DrawString(SpriteFontBase, string, Vector2, Color, float, bool)"/>'s, turned with it; the offset shadow
        /// is left out, a tumbling piece has no up for it to fall from).
        /// </summary>
        private void DrawTurnedGlyph(SpriteFontBase font, string glyph, Vector2 centre, Vector2 size, float turn, float scale,
            Color colour)
        {
            float alpha = colour.A / 255f;
            if (alpha <= 0.01f) return;

            int blur = Math.Max(1, Scaled(HUD_BACKING_BLUR));
            Vector2 backingSize = font.MeasureString(glyph, null, 0f, 0f, FontSystemEffect.Blurry, blur);
            Vector2 scaling = new(scale);

            for (int i = 0; i < HUD_BACKING_PASSES; i++)
                _game.OverlayBatch.DrawString(font, glyph, centre, HUD_BACKING * alpha, turn, backingSize * 0.5f, scaling,
                    0f, 0f, 0f, TextStyle.None, FontSystemEffect.Blurry, blur);

            _game.OverlayBatch.DrawString(font, glyph, centre, colour, turn, size * 0.5f, scaling);
        }

        /// <summary>0 to 1 from the break's seed, a piece's index and a salt for which figure of it is asked: the same every frame.</summary>
        private float Hash01(int index, int salt)
        {
            unchecked
            {
                uint h = (uint)(_breakSeed * 73856093) ^ (uint)(index * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>
        /// The shard's sliver (#845): a long, uneven four-sided splinter with antialiased edges, baked once into a small
        /// premultiplied white texture and tinted per draw, the way <see cref="DiscTexture"/> bakes the discs.
        /// </summary>
        private Texture2D ShardTexture()
        {
            if (_shard != null) return _shard;

            int width = STREAK_SHARD_TEXELS, height = STREAK_SHARD_TEXELS / 3;

            //The splinter's corners, wound one way round: a point at each end, the long edges broken at different places
            Vector2[] corners =
            {
                new(0.5f, height * 0.55f), new(width * 0.38f, 0.5f), new(width - 0.5f, height * 0.45f), new(width * 0.55f, height - 0.5f)
            };

            const int SAMPLES = 4;
            byte[] data = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < SAMPLES; sy++)
                        for (int sx = 0; sx < SAMPLES; sx++)
                            if (InsideConvex(corners, new Vector2(x + (sx + 0.5f) / SAMPLES, y + (sy + 0.5f) / SAMPLES))) covered++;

                    byte value = (byte)(covered * 255 / (SAMPLES * SAMPLES));
                    int o = (y * width + x) * 4;
                    data[o] = value;
                    data[o + 1] = value;
                    data[o + 2] = value;
                    data[o + 3] = value;
                }

            _shard = new Texture2D(_game.GraphicsDevice, width, height);
            _shard.SetData(data);
            return _shard;
        }

        /// <summary>Whether <paramref name="point"/> is inside a convex polygon: on the same side of every edge.</summary>
        private static bool InsideConvex(Vector2[] polygon, Vector2 point)
        {
            int sign = 0;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                float cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
                if (cross == 0f) continue;

                int side = cross > 0f ? 1 : -1;
                if (sign == 0) sign = side;
                else if (side != sign) return false;
            }

            return true;
        }

        /// <summary>
        /// Where the top of the balls-left readout stands, in pixels — the bottom of the frame's margin when there
        /// is no budget and so no readout. Measured the way <see cref="DrawBallsLeft"/> lays it out (the louder
        /// fonts once the count is low), with a digit and the plural caption, whose heights do not change with
        /// the count. The side cut's falling balls dissolve down to here (#428).
        /// </summary>
        private float BallsLeftTop(ScoreKeeper score, Viewport viewport, int margin)
        {
            float bottom = viewport.Height - margin;
            if (score.ShotsRemaining is not int left) return bottom;

            bool low = left <= HUD_LOW_BALLS;
            SpriteFontBase font = low ? _game.HudFontScoreLoud : _game.HudFontScore;
            SpriteFontBase captionFont = low ? _game.HudFontLabelLoud : _game.HudFontLabel;

            return bottom - captionFont.MeasureString("balls left").Y - Scaled(HUD_LINE_GAP)
                   - font.MeasureString("0").Y;
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
                critical ? Color.Lerp(HUD_CAPTION, HUD_ACCENT, 0.5f) : HUD_CAPTION);

            //Pivoted on its bottom-left corner: the margin and the caption under it stay exactly where they are
            //while the number itself shrinks and swells, so the layout never moves — only the figure does
            float numberTop = bottom - captionSize.Y - Scaled(HUD_LINE_GAP);

            DrawPulsed(font, _ballsText,
                new Vector2(margin, numberTop), new Vector2(0f, 1f),
                _ballsPulse.Scale * (1f + BREATHE_AMPLITUDE * wave), colour, glow);
        }

        /// <summary>
        /// The loaded queue as flat discs in the bottom RIGHT, next shot first and largest (#236). The gun cannot
        /// answer "which colour fires next" from the pose the game plays — <c>CannonRig</c>'s own note records
        /// that drawn opaque, the pane fills the whole slot from this camera, so the round in the notch shows as
        /// the small ellipse of its cap and the rest are read through glass keeping about 0.38 of what is behind
        /// it. This is that answer, at a size the eye can name a colour off.
        /// <para>
        /// <b>It shipped BEFORE #175's muzzle pulse came out, and that order was the point.</b> <c>CannonRig</c>
        /// warns in as many words that the mark must not be dropped as redundant on the strength of the pane, so
        /// the strip had to be seen to read first. It was, and then the pulse went — replaced by a coloured halo
        /// on the gun (<c>BallGlow</c>, and since #425 the muzzle collar), which says the same thing in the
        /// round's own colour instead of washing it towards white. Since #252 no loaded round pulses at all.
        /// So there are two signals now and neither is a brightness animation on the ball: this, and the gun.
        /// </para>
        /// <para>
        /// Colours come through <see cref="TypeColor"/>, which bakes from
        /// <c>BasicEffectParamsProvider.GetDiffuseTintByType</c> — so the strip tracks the balls by
        /// construction and there is no second palette here to drift out of step with them.
        /// </para>
        /// <para>
        /// <b>The bottom right, in firing order, the next round leftmost.</b> It started beside the ball count
        /// in the bottom left and the owner moved it twice: that corner already has the count, and the left edge
        /// also carries the cluster profile down its middle — then, having played it with the head in the
        /// corner, they asked for the queue to read the way it will be spent.
        /// </para>
        /// <para>
        /// The order is the owner's; what the layout owes it is that the <b>head does not move</b>. The row
        /// shortens as the shot budget runs down (see <c>shown</c> below), so a head placed by measuring back
        /// from the frame's right edge would slide along the bottom of the screen over the last five shots,
        /// which is exactly when the player is reading it. So the head's origin is solved for a <i>full</i>
        /// magazine ending flush with the margin: a shorter queue empties from the far end and leaves a gap at
        /// the corner, which is the truth about what is happening, and the disc that matters stays put.
        /// </para>
        /// </summary>
        private void DrawMagazine(ReadOnlySpan<BallType> queue, ReadOnlySpan<BallKind> kinds, ReadOnlySpan<BallType> queueFrom,
            ReadOnlySpan<float> queueFade, ScoreKeeper score, Viewport viewport, int margin)
        {
            if (queue.Length == 0) return;

            //Never more discs than there are shots left to take. The magazine holds five whatever happens, so
            //once the budget is down to two a five-disc strip is simply a lie about the level — and the last
            //shots are exactly when the player is counting.
            int shown = score.ShotsRemaining is int remaining ? Math.Min(queue.Length, remaining) : queue.Length;
            if (shown <= 0) return;

            SpriteBatch batch = _game.OverlayBatch;

            int head = Scaled(HUD_MAG_HEAD_RADIUS);
            int rest = (int)MathF.Round(head * HUD_MAG_REST_SCALE);
            int rim = Scaled(HUD_MAG_RIM);
            int gap = Scaled(HUD_MAG_GAP);

            //The head's full reach, ring and all — what the corner has to clear so no part of it is cut by the
            //frame's edge. The same argument HUD_MARGIN's own comment makes about a halo drawn hard against the
            //edge: a mark with a slice missing reads as a rendering fault rather than as a mark.
            int headOuter = MagazineHeadOuter();

            //Laid out rightwards in FIRING order, so the round about to leave is the leftmost and the queue
            //reads the way it will be spent. The owner asked for that order after playing it the other way
            //round, and the layout has to earn it rather than just mirror: the row shortens as the shot budget
            //runs down (`shown` above), so a head placed by measuring back from the right edge would slide
            //along the bottom of the screen over the last five shots — exactly when the player is reading it.
            //
            //So the HEAD's origin is what is fixed, and it is solved for a FULL magazine ending flush with the
            //margin. A shorter queue then empties from the far end and leaves a gap at the corner, which is the
            //truth about what is happening; the disc that matters has not moved.
            //
            //Every step is measured between what is DRAWN — halo to halo, and the head's ring to the next halo
            //— which is what HUD_MAG_GAP now means. The first step is longer than the others for that reason
            //and not by a fudge: the head reaches `headOuter` where a resting round reaches `restOuter`, so the
            //row spaces itself off each disc's own reach and the ring can no longer eat into the gap. The right
            //edge was already anchored this way (rest + rim below), which is what makes it one rule now.
            int restOuter = rest + rim;
            float rowStep = 2 * restOuter + gap;
            float toLastCentre = queue.Length > 1 ? headOuter + gap + restOuter + (queue.Length - 2) * rowStep : 0f;

            //Both coordinates are whole pixels: the discs' coverage textures are built on the texel grid, and
            //a centre on it is a 1:1 blit whose antialiased ramp lands where it was sampled. (The first scanline
            //build could COMB outright on a half-pixel centre — the texture replaces that arithmetic entirely.)
            float x = MathF.Round(viewport.Width - margin - restOuter - toLastCentre);
            float middle = MathF.Round(viewport.Height - margin - headOuter);

            //A special round's rays (#820), every one of them before any disc: a burst reaches past its own disc into its
            //neighbours' room, and it goes under them rather than over. The muzzle's burst, in the strip's own layer, so a
            //player reading the HUD sees the special round coming before it reaches the bore.
            float rayX = x;
            for (int i = 0; i < shown && i < kinds.Length; i++)
            {
                if (kinds[i] == BallKind.Wildcard || kinds[i] == BallKind.Cutter)
                    DrawRoundRays(batch, rayX, middle, (i == 0 ? head : rest) + rim, kinds[i] == BallKind.Wildcard);

                rayX += StepRight(i);
            }

            //The colour cue (#737): a plain round carries its colour's figure in the bore, so its disc carries the same
            //one, painted the way the ball is - the ground in the ground's ink, the figure in its own. Not where the balls
            //draw none (colourcue=none), and not on a special round, which is that kind first.
            bool figures = _game.Balls?.DrawsColourCue == true;

            for (int i = 0; i < shown; i++)
            {
                bool next = i == 0;
                int radius = next ? head : rest;
                bool figured = figures && (i >= kinds.Length || kinds[i] == BallKind.Normal);
                bool cutter = i < kinds.Length && kinds[i] == BallKind.Cutter;
                Color colour = DiscFill(queue[i], figured);

                //A disc whose round is dissolving into another colour - a swap's or a re-colour's (#705) - fades with it,
                //smoothed, so the strip shows the trade the bore shows instead of jumping on the frame of the press
                float fade = 1f;
                if (i < queueFade.Length && i < queueFrom.Length && queueFade[i] < 1f)
                {
                    fade = queueFade[i] * queueFade[i] * (3f - 2f * queueFade[i]);
                    colour = Color.Lerp(DiscFill(queueFrom[i], figured), colour, fade);
                }

                //The dark halo first, then the fill: one is what makes a pale round read over a white glacier,
                //the other is the answer the strip exists to give. A cutter's fill is the round itself (#766), steel with
                //its blade through it, and never the colour dealt into its slot, which nothing shows any more.
                DrawDisc(batch, x, middle, radius + rim, HUD_MAG_RIM_COLOR);
                if (cutter) DrawCutterDisc(batch, x, middle, radius);
                else DrawDisc(batch, x, middle, radius, colour);

                //The figure over the fill, under the outline; a fading disc's two figures cross-fade with its colours
                if (figured)
                {
                    if (fade < 1f) DrawFigure(batch, x, middle, radius, queueFrom[i], 1f - fade);
                    DrawFigure(batch, x, middle, radius, queue[i], fade);
                }

                //A light outline INSIDE the fill's edge, drawn as a ring: without it a near-black round is a
                //hole in the halo rather than a disc. TypeColor keeps each type's real darkness on purpose
                //(#153 refused peak-normalising), so this is the strip's version of the white gores that are
                //the only thing making the 8-ball itself legible.
                int outline = Scaled(HUD_MAG_OUTLINE);
                if (radius > outline)
                    DrawDisc(batch, x, middle, radius, HUD_MAG_OUTLINE_COLOR, radius - outline);

                //And the head gets a ring OUTSIDE its halo — the whole of "this one, right now", in the layer
                //the issue asks for it in rather than as brightness animation on the 3D ball.
                if (next)
                {
                    int ringOuter = radius + rim + Scaled(HUD_MAG_RING_GAP) + Scaled(HUD_MAG_RING_THICKNESS);
                    DrawDisc(batch, x, middle, ringOuter, HUD_MAG_OUTLINE_COLOR,
                        ringOuter - Scaled(HUD_MAG_RING_THICKNESS));
                }

                x += StepRight(i);
            }

            //Rightwards from slot i: clear its own drawn reach, the gap, and the next one's. Written from both reaches
            //rather than from one, because the head is bigger than the rest — and from the REACH rather than the fill
            //radius, because the halo and the head's ring are drawn there and a gap measured inside them is not the gap
            //anyone sees. Everything after the head is a resting round. One copy for the rays' walk and the discs'.
            float StepRight(int i) => (i == 0 ? headOuter : restOuter) + gap + (i + 1 < shown ? restOuter : 0);
        }

        //THE SPECIAL ROUND'S RAYS in the strip (#820): the burst the muzzle turns, behind the round's disc - a rainbow for
        //the wildcard, which counts as any colour, and the Cutter's accent for the Cutter (#766).

        /// <summary>How far a burst reaches, as a multiple of its disc's halo radius.</summary>
        private const float HUD_RAYS_REACH = 2.1f;

        /// <summary>How fast it turns, radians a second: the muzzle's own wheel, a little quicker at this size.</summary>
        private const float HUD_RAYS_SPIN = 0.9f;

        /// <summary>How many rays a burst has; two sets of lengths alternate, so it reads as rays and not a cog.</summary>
        private const int HUD_RAYS_COUNT = 10;

        /// <summary>The Cutter's burst: its accent pushed as the muzzle's collar and rays push it (<c>CannonRig.CollarHue</c>).</summary>
        private static readonly Color HUD_CUTTER_RAYS = new(Prazsky.BS3D.CannonRig.CollarHue(BasicEffectParamsProvider.CutterAccent));

        /// <summary>
        /// The Cutter's accent as the HUD writes it (#766): its ring on the profile panel in flight and its award's number -
        /// the two places that showed its dealt colour, which nothing shows.
        /// </summary>
        private static readonly Color CutterTint = new(BasicEffectParamsProvider.CutterAccent);

        /// <summary>One special round's burst, centred on its disc and turning on the wall clock.</summary>
        private void DrawRoundRays(SpriteBatch batch, float cx, float cy, int discReach, bool rainbow)
        {
            int reach = Math.Max(4, (int)MathF.Round(discReach * HUD_RAYS_REACH));
            Texture2D burst = RayBurstTexture(reach, rainbow);

            batch.Draw(burst, new Vector2(cx, cy), null, rainbow ? Color.White : HUD_CUTTER_RAYS,
                _game.WallClock * HUD_RAYS_SPIN, new Vector2(reach + 1f), 1f, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The burst's texture for one reach, baked once like <see cref="DiscTexture"/>: <see cref="HUD_RAYS_COUNT"/>
        /// tapered rays, every other one shorter, with a glow at their roots, premultiplied. A rainbow one carries its own
        /// hues, one a ray round the wheel; a plain one is white, for the draw to tint.
        /// </summary>
        private Texture2D RayBurstTexture(int reach, bool rainbow)
        {
            int key = reach * 2 + (rainbow ? 1 : 0);
            if (_rayBursts.TryGetValue(key, out Texture2D cached)) return cached;

            int size = 2 * reach + 2;
            float centre = reach + 1f;
            byte[] data = new byte[size * size * 4];

            //The disc's halo, as a share of the reach: where the rays come out from under it
            const float DISC = 1f / HUD_RAYS_REACH;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - centre, dy = y + 0.5f - centre;
                    float t = MathF.Sqrt(dx * dx + dy * dy) / reach;
                    if (t >= 1f) continue;

                    float angle = MathF.Atan2(dy, dx);
                    float phase = angle * HUD_RAYS_COUNT / MathF.Tau;
                    float index = MathF.Floor(phase + 0.5f);
                    float length = ((int)index & 1) == 0 ? 1f : 0.72f;

                    //Measured from the disc's halo outwards, where the burst is seen: everything inside it is under the
                    //disc, and the first cut, faded from the centre, had spent most of each ray there and showed slivers
                    float along = (t - DISC) / (length - DISC);
                    float lobe = MathF.Pow(MathF.Max(0f, MathF.Cos(MathF.Tau * (phase - index))), 5f);
                    float ray = along < 0f ? 1f : along >= 1f ? 0f : MathF.Pow(1f - along, 1.5f);
                    float glow = 0.6f * MathF.Exp(-MathF.Pow(MathF.Max(0f, t - DISC) / 0.12f, 2f));
                    float alpha = Math.Min(1f, lobe * ray + glow);

                    Vector3 colour = Vector3.One;
                    if (rainbow)
                    {
                        float h = (angle / MathF.Tau + 1f) % 1f;
                        colour = new Vector3(Hue(h), Hue(h + 2f / 3f), Hue(h + 1f / 3f));
                    }

                    int o = (y * size + x) * 4;
                    data[o] = (byte)(255f * colour.X * alpha);
                    data[o + 1] = (byte)(255f * colour.Y * alpha);
                    data[o + 2] = (byte)(255f * colour.Z * alpha);
                    data[o + 3] = (byte)(255f * alpha);
                }

            Texture2D texture = new(_game.GraphicsDevice, size, size);
            texture.SetData(data);

            _rayBursts[key] = texture;
            return texture;

            //One channel of a fully saturated hue round the wheel, MuzzleRays.fx's Rainbow
            static float Hue(float h) => Math.Clamp(MathF.Abs((h % 1f) * 6f - 3f) - 1f, 0f, 1f);
        }

        //The bursts baked so far, keyed by reach and kind - a couple of small textures per window size
        private readonly Dictionary<int, Texture2D> _rayBursts = new();

        //THE CUTTER'S DISC (#766): the round as the bore shows it, at a size a glance can take - a dark steel ball lit from
        //above, with the saw's face on it: a ring of hooked teeth glowing white-hot round a washer with three bolts. It
        //replaces the fill, never the halo or the outline, so the strip's layout and its "this one next" ring are a plain
        //round's.

        /// <summary>The saw's ring on the disc, as shares of its radius: the ring's inner edge, the gullets and the tips.</summary>
        private const float HUD_CUTTER_SAW_INNER = 0.48f;
        private const float HUD_CUTTER_SAW_GULLET = 0.58f;
        private const float HUD_CUTTER_SAW_TIP = 0.80f;

        /// <summary>How many teeth go round (the ball's face has sixteen; fewer read at this size).</summary>
        private const int HUD_CUTTER_TEETH = 11;

        /// <summary>The amber the teeth's light cools to at the ring's inner edge, a multiplier on the accent (the shader's <c>CutterEmber</c>).</summary>
        private static readonly Vector3 HUD_CUTTER_EMBER = new(1f, 0.55f, 0.22f);

        /// <summary>One cutter's disc, centred on the round's place in the strip.</summary>
        private void DrawCutterDisc(SpriteBatch batch, float cx, float cy, int radius)
        {
            if (radius < 2) return;

            Texture2D disc = CutterDiscTexture(radius);
            float half = disc.Width * 0.5f;

            batch.Draw(disc, new Vector2(cx - half, cy - half), Color.White);
        }

        /// <summary>
        /// The cutter's disc at one whole-pixel radius, baked once and premultiplied like <see cref="DiscTexture"/>, in the
        /// display's colours: the steel graded from the light above to a dark below (so it is not the black ball's disc), the
        /// blade the accent whitened to its tips.
        /// </summary>
        private Texture2D CutterDiscTexture(int radius)
        {
            if (_cutterDiscs.TryGetValue(radius, out Texture2D cached)) return cached;

            //Even-sided, the centre on the corner of the four middle texels, as DiscTexture's
            int half = radius + 1;
            int size = 2 * half;
            const int SAMPLES = 4;

            Vector3 accent = BasicEffectParamsProvider.CutterAccent;
            byte[] data = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector3 sum = Vector3.Zero;
                    int covered = 0;

                    for (int sy = 0; sy < SAMPLES; sy++)
                        for (int sx = 0; sx < SAMPLES; sx++)
                        {
                            float px = (x + (sx + 0.5f) / SAMPLES - half) / radius;
                            float py = (y + (sy + 0.5f) / SAMPLES - half) / radius;

                            if (CutterDiscSample(px, py, accent, out Vector3 colour))
                            {
                                sum += colour;
                                covered++;
                            }
                        }

                    //Premultiplied: the summed colour over every sample is the colour times the coverage
                    Vector3 c = sum / (SAMPLES * SAMPLES);
                    int o = (y * size + x) * 4;
                    data[o] = (byte)(255f * Math.Clamp(c.X, 0f, 1f));
                    data[o + 1] = (byte)(255f * Math.Clamp(c.Y, 0f, 1f));
                    data[o + 2] = (byte)(255f * Math.Clamp(c.Z, 0f, 1f));
                    data[o + 3] = (byte)(255 * covered / (SAMPLES * SAMPLES));
                }

            Texture2D texture = new(_game.GraphicsDevice, size, size);
            texture.SetData(data);

            _cutterDiscs[radius] = texture;
            return texture;
        }

        /// <summary>
        /// What one point of the cutter's disc is, in units of the disc's radius with y down, or false outside it: the steel,
        /// the saw's ring of teeth on it, and the hub inside the ring.
        /// </summary>
        private static bool CutterDiscSample(float px, float py, Vector3 accent, out Vector3 colour)
        {
            float distance = MathF.Sqrt(px * px + py * py);
            if (distance > 1f)
            {
                colour = Vector3.Zero;
                return false;
            }

            //The steel: light from above and to the left, a dark underside, a highlight
            float light = Math.Clamp(0.36f - 0.30f * py - 0.08f * px, 0.08f, 0.66f);
            float spot = MathF.Exp(-((px + 0.40f) * (px + 0.40f) + (py + 0.52f) * (py + 0.52f)) / 0.03f);
            colour = new Vector3(light * 0.92f, light * 0.95f, light) + new Vector3(0.5f * spot);

            //The hub: a brighter washer in a dark groove and three bolts on it
            if (distance < 0.34f) colour *= 1.5f;
            if (MathF.Abs(distance - 0.34f) < 0.04f) colour *= 0.4f;

            for (int bolt = 0; bolt < 3; bolt++)
            {
                float angle = bolt * MathF.Tau / 3f - MathF.PI / 2f;
                float bx = px - 0.19f * MathF.Cos(angle), by = py - 0.19f * MathF.Sin(angle);
                if (bx * bx + by * by < 0.065f * 0.065f) colour = new Vector3(0.78f, 0.80f, 0.84f);
            }

            //The saw's ring of teeth: a long back rising to the tip and a short face falling, as the blade's own, white-hot
            //at the tips
            float phase = (MathF.Atan2(py, px) / MathF.Tau + 1f) * HUD_CUTTER_TEETH;
            float through = phase - MathF.Floor(phase);
            float rise = through < 0.75f ? through / 0.75f : (1f - through) / 0.25f;
            float outline = HUD_CUTTER_SAW_GULLET + (HUD_CUTTER_SAW_TIP - HUD_CUTTER_SAW_GULLET) * rise;

            if (distance >= HUD_CUTTER_SAW_INNER && distance <= outline)
            {
                //Cooling from the white-hot tips to amber at the ring's inner edge, as the ball's own (BallCutter.fxh's
                //CutterEmber)
                float hot = (distance - HUD_CUTTER_SAW_INNER) / (outline - HUD_CUTTER_SAW_INNER);
                colour = Vector3.Lerp(accent * HUD_CUTTER_EMBER, accent, Math.Min(1f, hot * 1.4f));
            }

            //And the slit of light round the edge, where the blade goes in
            float slit = MathF.Exp(-MathF.Pow((distance - 0.97f) / 0.035f, 2f));
            colour = Vector3.Lerp(colour, Vector3.Lerp(accent, Vector3.One, 0.5f), slit);
            return true;
        }

        //The cutter's discs, keyed by whole-pixel radius - two per window size
        private readonly Dictionary<int, Texture2D> _cutterDiscs = new();

        /// <summary>
        /// How far the magazine's head reaches from its centre, ring and all — the one figure the strip's own layout and
        /// whatever stands above it (the swap's chip, #213) are both measured from, so the two cannot drift apart.
        /// </summary>
        private int MagazineHeadOuter() =>
            Scaled(HUD_MAG_HEAD_RADIUS) + Scaled(HUD_MAG_RIM) + Scaled(HUD_MAG_RING_GAP) + Scaled(HUD_MAG_RING_THICKNESS);

        //The Swap chip's keycap and button (PromptFont's own codepoints, as the tutorial's cards name them) and its words
        private const string SWAP_GLYPH_KEY = "Ｅ";
        private const string SWAP_GLYPH_PAD = "⇐";
        private const string SWAP_READY = "Swap";
        private const string SWAP_SPENT = "Swap used";

        //The Brake chip's (#213): Q and the pad's Y
        private const string BRAKE_GLYPH_KEY = "Ｑ";
        private const string BRAKE_GLYPH_PAD = "⇑";
        private const string BRAKE_READY = "Brake";
        private const string BRAKE_SPENT = "Brake used";

        //The Brake chip's two "not now" captions (#844): the glass is moving, or there is nothing it would lift (no pressure
        //step to give back, or on a tall level a lift that would take the column out of reach)
        private const string BRAKE_WAIT = "Brake · wait";
        private const string BRAKE_NOT_NEEDED = "Brake · not needed";

        //The Cut chip's (#213): R and the pad's right bumper. U+2199 is PromptFont's RB; the U+21B1 this held was its
        //PlayStation "R1", which the owner's Xbox pad has no button called
        private const string CUT_GLYPH_KEY = "Ｒ";
        private const string CUT_GLYPH_PAD = "↙";
        private const string CUT_READY = "Cut";
        private const string CUT_SPENT = "Cut used";

        //The Cut chip's "not now" (#844): the round in the bore cannot be turned into a cutter (a special one, a cutter
        //already, or no shots left)
        private const string CUT_NOT_NOW = "Cut · not now";

        //The space between a keycap and its words, and between two rows, a fifth under what they were beside the card-sized
        //fonts (14 and 8): they scale with the text they separate (#703)
        private const int HUD_SWAP_GLYPH_GAP = 11;
        private const int HUD_SWAP_ROW_GAP = 6;

        //The chips' lowest row stands the margin above the queue's head, the same air as under it (#703): the head's ring
        //is flush on HUD_MARGIN above the frame's bottom, so this is that figure and not a second hand-picked one, and the
        //two cannot drift apart again. It was 26, which left the queue 92 units of air under it and 26 over it
        private const int HUD_SWAP_ABOVE_STRIP = HUD_MARGIN;
        private const float HUD_SWAP_SPENT_ALPHA = 0.42f;

        //THE SWAP POINTS AT THE CURE (#845): while a streak runs and the round in the bore has nowhere to score (the owner's
        //rule of 2026-10-11), and the Swap would load one that has (a conservative condition added to his rule;
        //StreakCues.SwapIsTheCure, asked by the session), the Swap chip FLASHES and says what it is for: "Swap · keep ×3". The flash is the streak's own amber, a halo behind the
        //keycap and the words and the two tinted towards it, so the chip reads as belonging to the badge it would save:
        //a flare at the onset (the chip's pulse heated once), then a breath every SWAP_HINT_PERIOD between SWAP_HINT_FLOOR
        //and full, which keeps it found without blinking. Amber is the HUD's colour for gain, and keeping a streak is one.
        private bool _swapHint;
        private float _swapHintAge;
        private Pulse _swapPulse;

        //The Cut chip's flare as its charge is spent (#767): the press's pulse on the chip, in the cutter's own white-hot
        //rather than the amber that means gain, falling off at the pulse's own rate into the spent chip's dim
        private Pulse _cutPulse;
        private static readonly Color HUD_CUT_PULSE = new(255, 245, 224);
        private const string SWAP_KEEP = "Swap · keep ";
        private const float SWAP_HINT_PERIOD = 0.6f;
        private const float SWAP_HINT_FLOOR = 0.3f;

        //"Swap · keep ×3", built when the streak it names changes and never per frame
        private int _swapKeepFor = -1;
        private string _swapKeepText = SWAP_KEEP;

        //Every keycap or button a chip draws, either device's (#694): the column is as wide as the widest of them, so
        //it holds when the hand moves between the keyboard and the pad
        private static readonly string[] CHIP_GLYPHS =
            { SWAP_GLYPH_KEY, SWAP_GLYPH_PAD, BRAKE_GLYPH_KEY, BRAKE_GLYPH_PAD, CUT_GLYPH_KEY, CUT_GLYPH_PAD };

        //Every caption a chip can show, "×9" standing for any single-digit count (#694): the caption column is as
        //wide as the widest, so it holds when "Swap" becomes "Swap used" or "Swap · keep ×5". A wider one (a testing
        //powerups= of ten or more) widens it for the frames it is up rather than running past the strip's edge.
        private static readonly string[] CHIP_CAPTIONS =
        {
            SWAP_READY, SWAP_SPENT, SWAP_READY + " ×9", SWAP_KEEP + "×9", BRAKE_READY, BRAKE_SPENT, BRAKE_READY + " ×9",
            BRAKE_WAIT, BRAKE_NOT_NEEDED, CUT_READY, CUT_SPENT, CUT_READY + " ×9", CUT_NOT_NOW,
        };

        //The two widths above, measured once per pair of fonts (a resize rebuilds the HUD's fonts), never per frame
        private SpriteFontBase _chipGlyphFontFor, _chipCaptionFontFor;
        private float _chipGlyphColumn, _chipCaptionColumn;

        //Where the chip glyphs' ink is centred down their line box, and where a caption's capitals are centred down
        //theirs, measured with the widths. A keycap is set so the two centres meet: by its ink, not its line box. Set by
        //line boxes, every keycap stood about a tenth of itself under its caption (two fonts, two boxes - 12 px at
        //3840x1600), and it showed worst on the Cut chip's RB, a short pill whose drop under "Cut" the eye reads at once.
        //One centre for all of them: PromptFont draws every glyph a chip uses - the X and Y buttons, the RB pill, the E,
        //Q and R keycaps - with its ink centred on the same line (rendered and measured: 127.5 of 200 px for each). It is
        //read off the Y button, whose stored box is its ink; the pill's stored box runs well below its ink, so a per-glyph
        //TextBounds set RB 6 px too high.
        private float _chipGlyphInkCentre, _chipCaptionCapCentre;

        //The count's text, built when the count changes and not per frame
        private int _swapTextFor = -1;
        private string _swapText = SWAP_READY;
        private int _brakeTextFor = -1;
        private string _brakeText = BRAKE_READY;
        private int _cutTextFor = -1;
        private string _cutText = CUT_READY;

        //The "not now" caption each chip's words were last built for (#844), beside the count: a change of either rebuilds them
        private string _brakeNotNowFor, _cutNotNowFor;

        /// <summary>
        /// The power-up chips (#213), above the magazine they act on, the widest caption ending on its right edge and every keycap in one column (#694) — one row each, the Swap's
        /// nearest the strip and the ceiling's Brake on the row above it (or on the first when a level offers a brake and no
        /// swap): the key that does it, the word, and how many are left. They are the things in the HUD the player
        /// <i>carries</i> and may spend, so a spent one says so — dimmed and "Swap used" — rather than vanishing, which
        /// would read as a fault the second time the key is pressed. A chip is drawn only on a level that offers its
        /// power-up (<paramref name="swapCharges"/> or <paramref name="brakeCharges"/> ≥ 0), so the first chapter shows
        /// nothing to press. The glyph is the pad's button when the hand was last on the pad
        /// (<see cref="Tutorial.OnGamepad"/>), as every prompt outside the cards picks.
        /// </summary>
        /// <param name="brakeBlock">Why the brake cannot act now, if it cannot (#692, #844): a press is then refused with the gun's
        /// dry "no" and keeps its charge, so the chip is not shown ready — the owner's playtest met that "no" from a chip at full
        /// brightness and did not understand the brake — and, since #844, not shown spent either: it says to wait.</param>
        /// <param name="cutBlocked">Whether the round in the bore cannot be turned into a cutter now (#844).</param>
        /// <param name="streak">The streak the readout shows, which the Swap's hint names ("Swap · keep ×3", #845).</param>
        private void DrawSwap(int swapCharges, int brakeCharges, int cutCharges, BrakeBlock brakeBlock, bool cutBlocked, bool onGamepad,
            int streak, Viewport viewport, int margin)
        {
            if (swapCharges < 0 && brakeCharges < 0 && cutCharges < 0) return;

            SpriteFontBase glyphFont = _game.HudFontChipPrompt;
            SpriteFontBase captionFont = _game.HudFontChipDetail;
            MeasureChipColumns(glyphFont, captionFont);

            //A chip with a charge it cannot spend now says why instead of how many (#844), and a chip with none left says so
            string brakeNotNow = brakeCharges > 0
                ? brakeBlock switch { BrakeBlock.Wait => BRAKE_WAIT, BrakeBlock.NotNeeded => BRAKE_NOT_NEEDED, _ => null }
                : null;
            string cutNotNow = cutCharges > 0 && cutBlocked ? CUT_NOT_NOW : null;

            string noReason = null;
            if (swapCharges >= 0) ChipText(swapCharges, SWAP_READY, SWAP_SPENT, null, ref _swapTextFor, ref noReason, ref _swapText);
            if (brakeCharges >= 0) ChipText(brakeCharges, BRAKE_READY, BRAKE_SPENT, brakeNotNow, ref _brakeTextFor, ref _brakeNotNowFor, ref _brakeText);
            if (cutCharges >= 0) ChipText(cutCharges, CUT_READY, CUT_SPENT, cutNotNow, ref _cutTextFor, ref _cutNotNowFor, ref _cutText);

            //The Swap pointing at the cure (#845) says what it is for in place of its count, for as long as it flashes
            bool hint = _swapHint && swapCharges > 0;
            if (hint && streak != _swapKeepFor)
            {
                _swapKeepFor = streak;
                _swapKeepText = SWAP_KEEP + "×" + streak.ToString(CultureInfo.InvariantCulture);
            }

            string swapText = hint ? _swapKeepText : _swapText;

            //One column for every chip on the frame (#694): the widest caption a chip can show, or a wider one that is
            //up now, ends on the strip's right edge, and every keycap stands in the column left of the captions
            float captionColumn = _chipCaptionColumn;
            if (swapCharges >= 0) captionColumn = MathF.Max(captionColumn, captionFont.MeasureString(swapText).X);
            if (brakeCharges >= 0) captionColumn = MathF.Max(captionColumn, captionFont.MeasureString(_brakeText).X);
            if (cutCharges >= 0) captionColumn = MathF.Max(captionColumn, captionFont.MeasureString(_cutText).X);

            ChipColumnLayout layout = ChipColumnLayout.Compute(viewport.Width - margin, _chipGlyphColumn, captionColumn,
                Scaled(HUD_SWAP_GLYPH_GAP));

            int row = 0;

            if (swapCharges >= 0)
                DrawChip(row++, onGamepad ? SWAP_GLYPH_PAD : SWAP_GLYPH_KEY, swapText, State(swapCharges, null), in layout, viewport, margin,
                    hint ? SwapHintGlow() : 0f);

            if (brakeCharges >= 0)
                DrawChip(row++, onGamepad ? BRAKE_GLYPH_PAD : BRAKE_GLYPH_KEY, _brakeText, State(brakeCharges, brakeNotNow), in layout,
                    viewport, margin);

            if (cutCharges >= 0)
                DrawChip(row, onGamepad ? CUT_GLYPH_PAD : CUT_GLYPH_KEY, _cutText, State(cutCharges, cutNotNow), in layout, viewport, margin,
                    _cutPulse.Heat, HUD_CUT_PULSE);
        }

        /// <summary>The Cut's press (#767): the chip flares in the cutter's white-hot as its charge goes into the round.</summary>
        internal void PulseCutChip() => _cutPulse.Kick(0f);

        /// <summary>
        /// The keycap column's and the caption column's widths (#694), measured over every glyph and every caption a chip
        /// can show - again only when the HUD's fonts are rebuilt, which a resize does.
        /// </summary>
        private void MeasureChipColumns(SpriteFontBase glyphFont, SpriteFontBase captionFont)
        {
            if (ReferenceEquals(glyphFont, _chipGlyphFontFor) && ReferenceEquals(captionFont, _chipCaptionFontFor)) return;

            _chipGlyphFontFor = glyphFont;
            _chipCaptionFontFor = captionFont;
            _chipGlyphColumn = 0f;
            _chipCaptionColumn = 0f;

            foreach (string glyph in CHIP_GLYPHS) _chipGlyphColumn = MathF.Max(_chipGlyphColumn, glyphFont.MeasureString(glyph).X);

            _chipGlyphInkCentre = InkCentreY(glyphFont, BRAKE_GLYPH_PAD);

            //A capital with nothing above the cap height or below the baseline stands for every caption's capitals
            _chipCaptionCapCentre = InkCentreY(captionFont, "H");

            foreach (string caption in CHIP_CAPTIONS) _chipCaptionColumn = MathF.Max(_chipCaptionColumn, captionFont.MeasureString(caption).X);
        }

        /// <summary>Where a glyph's ink is centred, down from the top of its line box, as the font rasterises it.</summary>
        private static float InkCentreY(SpriteFontBase font, string glyph)
        {
            FontStashSharp.Bounds ink = font.TextBounds(glyph, Vector2.Zero);
            return (ink.Y + ink.Y2) * 0.5f;
        }

        /// <summary>Why the brake cannot act now (#844), as the session reads it: nothing in the way, the glass moving, or nothing to lift.</summary>
        internal enum BrakeBlock : byte { None, Wait, NotNeeded }

        /// <summary>A chip's three states (#844): it can act, a charge is kept but cannot act now, or none is left.</summary>
        private enum ChipState : byte { Ready, NotNow, Spent }

        private static ChipState State(int charges, string notNow) =>
            charges <= 0 ? ChipState.Spent : notNow != null ? ChipState.NotNow : ChipState.Ready;

        /// <summary>
        /// A chip's words for <paramref name="charges"/> and the reason it cannot act now, if any (<paramref name="notNow"/>,
        /// #844), rebuilt only when either changes (never per frame): the reason replaces the count while it holds, and none
        /// left says so whatever else is true.
        /// </summary>
        private static void ChipText(int charges, string ready, string spent, string notNow, ref int textFor, ref string notNowFor,
            ref string text)
        {
            if (charges == textFor && ReferenceEquals(notNow, notNowFor)) return;

            textFor = charges;
            notNowFor = notNow;
            text = charges == 0 ? spent
                : notNow ?? (charges == 1 ? ready : ready + " ×" + charges.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// One power-up chip on <paramref name="row"/> above the magazine (0 nearest it), <see cref="DrawSwap"/>'s subject:
        /// its keycap centred in the shared column and its caption from the shared left edge (<see cref="ChipColumnLayout"/>,
        /// #694), so the keycaps of every chip stand in one column whatever their captions say.
        /// </summary>
        /// <param name="hint">How brightly the chip is pointing at itself (#845, the Swap's hint), 0 for not at all: an amber
        /// halo behind the keycap and the words, and both tinted towards the amber, by this much.</param>
        /// <param name="hintColour">The halo's colour when it is not the amber of gain: the Cut's white-hot flare (#767).</param>
        private void DrawChip(int row, string glyph, string text, ChipState state, in ChipColumnLayout layout, Viewport viewport,
            int margin, float hint = 0f, Color hintColour = default)
        {
            Color halo = hintColour.A == 0 ? HUD_ACCENT : hintColour;

            //Three states told apart by brightness, the HUD's grey and not a colour (amber means gain, see the class doc):
            //ready is the keycap and its word in full white; NOT NOW dims the keycap - the key will do nothing yet - and keeps
            //the words in full, since they say why (#844); used up dims the whole chip, the way it always has
            bool ready = state == ChipState.Ready;
            float alpha = ready ? 1f : HUD_SWAP_SPENT_ALPHA;
            float captionAlpha = state == ChipState.Spent ? HUD_SWAP_SPENT_ALPHA : 1f;

            SpriteFontBase glyphFont = _game.HudFontChipPrompt;
            SpriteFontBase captionFont = _game.HudFontChipDetail;

            Vector2 glyphSize = glyphFont.MeasureString(glyph);
            Vector2 captionSize = captionFont.MeasureString(text);
            float height = MathF.Max(glyphSize.Y, captionSize.Y);

            //Bottom a little over the head's ring: the head is the tallest thing in the strip and its top is where the
            //chip has to clear. Each row further up is a chip's height and a gap higher.
            float stripTop = viewport.Height - margin - 2 * MagazineHeadOuter();
            float rise = Scaled(HUD_SWAP_ABOVE_STRIP) + row * (height + Scaled(HUD_SWAP_ROW_GAP));
            float top = MathF.Round(stripTop - rise - height);

            //The caption by its line box as ever, and the keycap so its ink is centred on the caption's capitals (above)
            float captionTop = top + (height - captionSize.Y) * 0.5f;
            float glyphTop = captionTop + _chipCaptionCapCentre - _chipGlyphInkCentre;

            Vector2 glyphAt = new(MathF.Round(layout.GlyphLeft(glyphSize.X)), MathF.Round(glyphTop));
            Vector2 captionAt = new(MathF.Round(layout.CaptionLeft), captionTop);
            Color glyphColour = BS3DGame.MENU_TEXT * alpha;
            Color captionColour = (ready ? BS3DGame.MENU_TEXT : HUD_CAPTION) * captionAlpha;

            if (hint > 0f)
            {
                //The halo first, under both, the way the readouts flash; then the two tinted towards it
                DrawGlow(glyphFont, glyph, glyphAt, glyphSize, 1f, hint, halo, HUD_GLOW_PASSES);
                DrawGlow(captionFont, text, captionAt, captionSize, 1f, hint, halo, HUD_GLOW_PASSES);
                glyphColour = Color.Lerp(glyphColour, halo, 0.8f * hint);
                captionColour = Color.Lerp(captionColour, halo, 0.8f * hint);
            }

            DrawString(glyphFont, glyph, glyphAt, glyphColour, 1f);
            DrawString(captionFont, text, captionAt, captionColour, 1f);
        }

        /// <summary>
        /// How brightly the Swap's hint is flashing now (#845): a flare at its onset (the chip's pulse heated once, falling
        /// off at the pulse's own rate), then a breath every <see cref="SWAP_HINT_PERIOD"/> from full down to
        /// <see cref="SWAP_HINT_FLOOR"/> and back, on the play clock like every other HUD motion.
        /// </summary>
        private float SwapHintGlow()
        {
            float breath = 0.5f + 0.5f * MathF.Cos(MathHelper.TwoPi * _swapHintAge / SWAP_HINT_PERIOD);
            return MathF.Max(MathHelper.Lerp(SWAP_HINT_FLOOR, 1f, breath), _swapPulse.Heat);
        }

        /// <summary>
        /// The awards, on their way in. Each is born on the cell it was earned in (the completion bonus over the aim point,
        /// <see cref="BonusSource"/>), zooms well past the score's size and is held there long enough to be read, then shrinks where it
        /// stands and is thrown into the score with a smear behind it (#846, <see cref="AwardZoom"/>). The source is
        /// <b>reprojected every frame</b> and the flight is a lerp from it, so at the handover the blend weight is zero
        /// and the number is exactly where it was — the same one-reversible-scalar idiom precise aim and the drop
        /// cinematic use, and for the same reason: there is no frame on which anything snaps.
        /// </summary>
        private void DrawAwards(ICamera camera, Viewport viewport, Vector2 destination)
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];

                //The completion bonus, still waiting for the award of the shot that cleared the field to be read, or a cut's
                //award waiting for its severing to be seen (#767)
                if (popup.Age < 0f) continue;

                Vector2 source;

                if (popup.IsBonus)
                {
                    //Over the aim point, in screen space: the middle of the frame is where the eye is, and no cell is
                    source = BonusSource(popup, viewport);
                }
                else if (TryProject(camera, viewport, popup, out Vector2 projected))
                {
                    source = projected;

                    //Remembered, because the flight has to start from somewhere even after the camera has
                    //swung the cell out of frame — which a drop cinematic does routinely
                    popup.Screen = projected;
                    popup.Projected = true;
                    _popups[i] = popup;
                }
                else if (popup.Projected) source = popup.Screen;
                else if (popup.Age < popup.Hold) continue;  //never seen, and still in the leg where it would be at the cell
                else source = destination;                  //only the flight is left, and it ends where it ends

                float size = AwardZoom.Size(popup.Age, popup.Hold, popup.Birth, popup.Peak);
                AwardLayout layout = LayOutAward(popup, size);

                //Inside the frame at the size it has now. A zoomed number born on a cell near the top would otherwise be
                //cut off by the edge; it loosens as the number shrinks, so the source eases back to the cell without a step.
                source = KeepInFrame(source, layout.Extent, viewport);

                float flight = AwardZoom.Flight(popup.Age, popup.Hold);

                if (flight <= 0f)
                {
                    //The reading leg: on the balls, zooming in and then held
                    DrawAwardAt(popup, layout, source, popup.Tint, 1f);
                    continue;
                }

                DrawAwardFlight(popup, layout, size, source, destination, flight, viewport);
            }
        }

        /// <summary>
        /// Where the completion bonus stands while it is read (#881): centred over the middle of the frame, where the player's eye
        /// rests while aiming, <b>with the bottom edge of its tallest drawing</b> (the top of the zoom's overshoot, or the slow
        /// swell at the end of the read: <see cref="AwardZoom.ReadMax"/>) <see cref="StreakCues.LOST_CAPTION_CLEARANCE"/> above it,
        /// which is where the lost streak's words stand and for the same reason: clear of the crosshair at its most swollen
        /// (<c>Crosshair.FULL_STRAIN_REACH</c>), which the player may still be holding on the beat the field empties - in pixels at
        /// the crosshair's own scale, as the words are (<see cref="StreakCues.ClearancePixels"/>). The block
        /// zooms about this centre, so it grows both ways but never down into the aim; it drifts up over the read
        /// (<see cref="POPUP_BONUS_RISE"/>), away from it. The point the flight starts from, and so the one the throw's wind-up
        /// gathers behind.
        /// </summary>
        private Vector2 BonusSource(in Popup popup, Viewport viewport)
        {
            AwardLayout tallest = LayOutAward(popup, AwardZoom.ReadMax(popup.Peak, popup.Birth, popup.Hold));
            float bottom = viewport.Height * 0.5f - StreakCues.ClearancePixels(viewport.Height)
                           - Scaled(POPUP_BONUS_RISE) * AwardZoom.Rise(popup.Age, popup.Hold);

            return new Vector2(viewport.Width * 0.5f, bottom - tallest.Extent.Y * 0.5f);
        }

        /// <summary>
        /// One award crossing the frame. The smear is sampled off the curve itself rather than off the last
        /// frame's position: the path is known in closed form, so looking back a fixed distance <i>in flight
        /// parameter</i> gives a trail whose length is the curve's own local speed — nothing to store, nothing
        /// to go stale, and identical at any frame rate.
        /// </summary>
        private void DrawAwardFlight(in Popup popup, in AwardLayout layout, float size, Vector2 source, Vector2 destination,
            float flight, Viewport viewport)
        {
            float back = MathF.Max(0f, flight - SMEAR_LOOKBACK);
            Vector2 at = FlightPosition(source, destination, flight);
            Vector2 before = FlightPosition(source, destination, back);

            //The wind-up gathers the number behind its source, away from the corner, so the dip can carry a number born near
            //an edge half off the frame (#846). Held inside the frame as the source is, until the throw passes the source:
            //there the position IS the source, already held, so nothing steps.
            if (HudEase.InBack(flight) < 0f) at = KeepInFrame(at, layout.Extent, viewport);
            if (HudEase.InBack(back) < 0f) before = KeepInFrame(before, layout.Extent, viewport);

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
                DrawAwardAt(popup, LayOutAward(popup, size * MathHelper.Lerp(0.85f, 1f, t)), Vector2.Lerp(before, at, t),
                    colour, alpha * SMEAR_ALPHA * t * t, shadow: false);
            }

            DrawAwardAt(popup, layout, at, colour, alpha);
        }

        /// <summary>
        /// The flight path: a lerp from the cell to the corner through an ease that pulls <i>backwards</i> first.
        /// That anticipation is what makes the throw read as a throw — the number gathers itself (and, since #846,
        /// shrinks from its zoom while it does), then goes, and arrives at its greatest speed, which is exactly where
        /// the smear should be longest and where the corner is about to be hit.
        /// </summary>
        private static Vector2 FlightPosition(Vector2 source, Vector2 destination, float flight) =>
            Vector2.Lerp(source, destination, HudEase.InBack(flight));

        /// <summary>
        /// One award laid out at one size (#846): the face and scale of its figure and of its caption, the backing each is
        /// drawn with, and the room the pair takes, in pixels. A value, built per draw: nothing is kept or allocated.
        /// </summary>
        private readonly struct AwardLayout
        {
            public readonly SpriteFontBase Font, SuffixFont;
            public readonly float Scale, SuffixScale;
            public readonly int Blur, SuffixBlur;
            public readonly Vector2 TextSize, SuffixSize;
            public readonly float Gap, Shadow;

            /// <summary>The offset of the caption's own shadow: the figure's (<see cref="Shadow"/>) beside it, where the two are
            /// near one size, and the figure's scaled by the caption's share when it stands over it, small beside a large
            /// figure, where the figure's offset would draw it a second, dark copy of itself a letter's height away.</summary>
            public readonly float SuffixShadow;

            /// <summary>The caption stands over the figure, centred on it, instead of beside it: the completion bonus's
            /// (<see cref="Popup.IsBonus"/>).</summary>
            public readonly bool Stacked;

            public AwardLayout(SpriteFontBase font, float scale, int blur, Vector2 textSize, SpriteFontBase suffixFont,
                float suffixScale, int suffixBlur, Vector2 suffixSize, float gap, float shadow, float suffixShadow, bool stacked = false)
            {
                Stacked = stacked;
                SuffixShadow = suffixShadow;
                Font = font;
                Scale = scale;
                Blur = blur;
                TextSize = textSize;
                SuffixFont = suffixFont;
                SuffixScale = suffixScale;
                SuffixBlur = suffixBlur;
                SuffixSize = suffixSize;
                Gap = gap;
                Shadow = shadow;
            }

            /// <summary>The figure and its caption side by side, or the caption over the figure when stacked, in pixels.</summary>
            public Vector2 Extent => Stacked
                ? new Vector2(MathF.Max(TextSize.X, SuffixSize.X), SuffixSize.Y + Gap + TextSize.Y)
                : new Vector2(TextSize.X + Gap + SuffixSize.X, MathF.Max(TextSize.Y, SuffixSize.Y));
        }

        //The caption beside an award's figure, as a share of the figure's size: the label's type against the old popup's,
        //the relation the two were drawn at before the zoom (#846)
        private const float POPUP_SUFFIX_SHARE = BS3DGame.HUD_FONT_LABEL / (float)BS3DGame.HUD_FONT_POPUP;

        /// <summary>
        /// Lays an award out at <paramref name="size"/> scores (<see cref="AwardZoom.Size"/>). Its gap and its shadow's
        /// offset scale with the figure exactly as they did with the old popup's one face, so the look at any size is the
        /// old popup's at that size. <paramref name="suffixShare"/> is the caption's size as a share of the figure's: an award's
        /// (<see cref="POPUP_SUFFIX_SHARE"/>) unless the caller says otherwise (the lost streak's words and multiplier stand
        /// at one size, <see cref="LOST_CAPTION_SUFFIX_SHARE"/>). The completion bonus (<see cref="Popup.IsBonus"/>) is laid out
        /// stacked, its caption over its figure at <see cref="POPUP_BONUS_CAPTION_SHARE"/>, whatever the caller says.
        /// </summary>
        private AwardLayout LayOutAward(in Popup popup, float size, float suffixShare = POPUP_SUFFIX_SHARE)
        {
            if (popup.IsBonus) suffixShare = POPUP_BONUS_CAPTION_SHARE;

            float units = size * BS3DGame.HUD_FONT_SCORE;
            float share = units / BS3DGame.HUD_FONT_POPUP;
            float shadow = Scaled(HUD_SHADOW_OFFSET) * share;

            SpriteFontBase font = AwardFace(units, out float scale, out int blur);
            Vector2 textSize = font.MeasureString(popup.Text) * scale;

            if (popup.Suffix == null)
                return new AwardLayout(font, scale, blur, textSize, null, 0f, 0, Vector2.Zero, 0f, shadow, shadow);

            SpriteFontBase suffixFont = AwardFace(units * suffixShare, out float suffixScale, out int suffixBlur);
            Vector2 suffixSize = suffixFont.MeasureString(popup.Suffix) * suffixScale;

            return new AwardLayout(font, scale, blur, textSize, suffixFont, suffixScale, suffixBlur, suffixSize,
                Scaled(POPUP_SUFFIX_GAP) * share, shadow, popup.IsBonus ? shadow * suffixShare : shadow, popup.IsBonus);
        }

        /// <summary>
        /// The face an award is drawn from at <paramref name="units"/> design units: the smallest of the ladder at least
        /// that big (<c>BS3DGame.HUD_FONT_POPUP</c>), so a glyph is never stretched and never shrunk to much under half
        /// its raster, but for the zoom's first few frames under the label's size, gone too fast to shimmer.
        /// <paramref name="blur"/> is the backing's for that face, the same share of its size the old popup's
        /// was of 112, so the backing neither tightens nor swells at a step between faces.
        /// </summary>
        private SpriteFontBase AwardFace(float units, out float scale, out int blur)
        {
            SpriteFontBase font;
            int nominal;

            if (units <= BS3DGame.HUD_FONT_LABEL) (font, nominal) = (_game.HudFontLabel, BS3DGame.HUD_FONT_LABEL);
            else if (units <= BS3DGame.HUD_FONT_POPUP) (font, nominal) = (_game.HudFontPopup, BS3DGame.HUD_FONT_POPUP);
            else if (units <= BS3DGame.HUD_FONT_TUTORIAL) (font, nominal) = (_game.HudFontTutorial, BS3DGame.HUD_FONT_TUTORIAL);
            else if (units <= BS3DGame.HUD_FONT_POPUP_LARGE) (font, nominal) = (_game.HudFontPopupLarge, BS3DGame.HUD_FONT_POPUP_LARGE);
            else (font, nominal) = (_game.HudFontPopupPeak, BS3DGame.HUD_FONT_POPUP_PEAK);

            scale = units / nominal;
            blur = Math.Max(1, Scaled(HUD_BACKING_BLUR * nominal / BS3DGame.HUD_FONT_POPUP));
            return font;
        }

        /// <summary>A centre moved just enough that a box of <paramref name="extent"/> around it stays
        /// <see cref="POPUP_EDGE"/> inside the frame; centred on an axis the box is too big for.</summary>
        private Vector2 KeepInFrame(Vector2 centre, Vector2 extent, Viewport viewport)
        {
            float edge = Scaled(POPUP_EDGE);
            return new Vector2(Within(centre.X, extent.X * 0.5f + edge, viewport.Width - extent.X * 0.5f - edge),
                Within(centre.Y, extent.Y * 0.5f + edge, viewport.Height - extent.Y * 0.5f - edge));

            static float Within(float value, float least, float most) =>
                least > most ? (least + most) * 0.5f : Math.Clamp(value, least, most);
        }

        /// <summary>One award — its points, and the multiplier it was won at (or the bonus's caption), kept in the
        /// accent beside them, or over them when the layout is stacked.</summary>
        private void DrawAwardAt(in Popup popup, in AwardLayout layout, Vector2 centre, Color colour, float alpha,
            bool shadow = true)
        {
            Vector2 extent = layout.Extent;
            Vector2 corner = centre - extent * 0.5f;

            if (layout.Stacked)
            {
                //The words over the figure, each centred on the block's axis: what it counts, then how much
                float figureTop = corner.Y + layout.SuffixSize.Y + layout.Gap;

                DrawString(layout.SuffixFont, popup.Suffix, new Vector2(corner.X + (extent.X - layout.SuffixSize.X) * 0.5f, corner.Y),
                    HUD_ACCENT * alpha, layout.SuffixScale, shadow, layout.SuffixBlur, layout.SuffixShadow);
                DrawString(layout.Font, popup.Text, new Vector2(corner.X + (extent.X - layout.TextSize.X) * 0.5f, figureTop),
                    colour * alpha, layout.Scale, shadow, layout.Blur, layout.Shadow);
                return;
            }

            DrawString(layout.Font, popup.Text, corner + new Vector2(0f, (extent.Y - layout.TextSize.Y) * 0.5f),
                colour * alpha, layout.Scale, shadow, layout.Blur, layout.Shadow);

            if (popup.Suffix == null) return;

            //The multiplier is the streak's colour wherever it appears, so the badge on a popup and the number
            //in the corner are recognisably the same thing
            DrawString(layout.SuffixFont, popup.Suffix,
                corner + new Vector2(layout.TextSize.X + layout.Gap, (extent.Y - layout.SuffixSize.Y) * 0.5f),
                HUD_ACCENT * alpha, layout.SuffixScale, shadow, layout.SuffixBlur, layout.SuffixShadow);
        }

        /// <summary>
        /// The sleeping gun's letters (#230): each a Z out of the muzzle, climbing and wandering a little aside,
        /// growing as it goes and fading at both ends of its life — the cartoon convention, which is the point. In the
        /// popup's font and the menu's white, projected like a popup, and skipped when behind the lens.
        /// </summary>
        private void DrawSnores(ICamera camera, Viewport viewport, ReadOnlySpan<BS3D.Effects.DozingGun.Z> snores)
        {
            if (snores.IsEmpty) return;

            Matrix view = camera.View;
            Vector3 right = new(view.M11, view.M21, view.M31);
            SpriteFontBase font = _game.HudFontPopup;

            for (int i = 0; i < snores.Length; i++)
            {
                if (!snores[i].Live) continue;

                Vector3 projected = viewport.Project(snores[i].Position(right), camera.Projection, view, Matrix.Identity);
                if (projected.Z < 0f || projected.Z > 1f) continue;

                float t = snores[i].Age / BS3D.Effects.DozingGun.Z_LIFE;
                float alpha = MathHelper.Clamp(t / 0.15f, 0f, 1f) * MathHelper.Clamp((1f - t) / 0.35f, 0f, 1f);
                float scale = SNORE_SCALE_FROM + (SNORE_SCALE_TO - SNORE_SCALE_FROM) * t;

                Vector2 size = font.MeasureString("Z") * scale;
                DrawString(font, "Z", new Vector2(projected.X, projected.Y) - size * 0.5f,
                    BS3DGame.MENU_TEXT * (alpha * SNORE_ALPHA), scale, shadow: true);
            }
        }

        //How big a Z is when it leaves the muzzle and when it has climbed away, as a fraction of the popup font, and
        //how opaque it gets: a whisper next to the score, since nobody is playing while it shows
        private const float SNORE_SCALE_FROM = 1.1f, SNORE_SCALE_TO = 2.2f, SNORE_ALPHA = 0.9f;

        private static bool TryProject(ICamera camera, Viewport viewport, in Popup popup, out Vector2 screen)
        {
            //It rises over the reading leg and then stops: past that the flight owns where it goes, and a source
            //still climbing would fight the curve
            float rise = AwardZoom.Rise(popup.Age, popup.Hold) * POPUP_RISE;
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
        /// it, so the numbers pass over it rather than under. The balls are procedural through and through —
        /// coverage textures generated from their own radii, no content asset — and no per-frame allocations
        /// are made: the markers read straight off the span the session already filled.
        /// </para>
        /// </summary>
        private void DrawClusterProfile(Viewport viewport, in ClusterProfile profile, ReadOnlySpan<BallMarker> balls,
            float readoutTop)
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

            //HOW FAR UNDER THE LINE A FALLING BALL IS STILL DRAWN (#428): down to just above the balls-left
            //readout, the one thing under the panel it must not run into — so the player watches the balls go on
            //falling towards the drain rather than vanishing at the line. A screen distance turned into world
            //units at the panel's own scale, so the fade stays a distance and the panel stays stateless (see
            //PROFILE_SINK_FADE); never shorter than that floor, and never longer than the fall the game really
            //draws, since a ball the kill plane takes mid-fade would pop out exactly as #134's did.
            float floorPx = readoutTop - Scaled(HUD_LINE_GAP) - markerRadius;
            float sinkFade = Math.Clamp((floorPx - WorldToPanelY(bottomY)) / pixelsPerUnit,
                PROFILE_SINK_FADE, bottomY - GameplayScreen.KILL_PLANE_Y);
            float sinkHold = sinkFade * PROFILE_SINK_HOLD;

            SpriteBatch batch = _game.OverlayBatch;
            Texture2D pixel = Pixel;

            //No backing plate: the balls and the bars draw straight over the scene, which keeps the panel from
            //reading as a box plastered over the world. The markers carry their own contrast (their colours as
            //the lit ball actually shows them — see TypeColor — the dark types kept visible by the baked sheen).

            //The glass bar at its current height — NEUTRAL (the menu's own text colour) at rest, so it reads as
            //the ceiling the balls hang from rather than as a warning. It takes the alarm's red only on the flash,
            //squared exactly as the 3D plate's EmissiveTint is scaled by CeilingDescent.Flash²: unmistakable on the frame
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

            //The cluster's balls, each ONE quad — a small procedural texture whose texels carry the disc's
            //exact area coverage, cached per whole-pixel geometry and scaled by the radius's fraction (see
            //DrawDisc). The antialiased rim is the whole point: the profile's markers are the magazine
            //strip's discs at a third of the size, and a staircase edge at that scale reads as noise rather
            //than as a ball.
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
                //it. So it goes on falling out of the panel, whole for the first PROFILE_SINK_HOLD of sinkFade
                //and dissolving over the rest (#428).
                //
                //Which of the two this is is where the ball came from (BallMarker.FromAbove), decided once per ball
                //(#822): read off the sign of its vertical velocity every frame, it flipped at every contact in the
                //funnel, and the ball blinked. Not the list alone either: a shot that missed is still in _shotBalls
                //on the way back down.
                //
                //⚠ A CLUSTER ball under the line is a third case, and it is DRAWN (#643). It gets there in a swing
                //the line forgives (#239) and, above all, when the cluster has reached the line and lost: the panel
                //stays up through a loss so the player can read what beat them (#639), and it used to cull exactly
                //the balls that had crossed — the owner's report, "whatever is under the red line isn't drawn". So
                //they stand where they hang, whole, ringed in the alarm's red: those are the ones that crossed.
                //Down to the bottom of the frame and no further, which on a loss is free — nothing else is drawn
                //under the panel then.
                float sink = bottomY - marker.World.Y;
                float alpha = 1f;
                bool crossed = false;

                if (sink > 0f)
                {
                    if (!marker.InFlight) crossed = true;
                    else if (!marker.FromAbove || sink >= sinkFade) continue;
                    else alpha = sink <= sinkHold ? 1f : 1f - (sink - sinkHold) / (sinkFade - sinkHold);
                }

                float px = WorldToPanelX(marker.World);
                float py = WorldToPanelY(marker.World.Y);

                if (crossed && py - markerRadius > viewport.Height) continue;

                //Multiplied through all four channels, which is the premultiplied alpha this batch's default
                //AlphaBlend wants — the same way the broken streak fades out.
                DrawDisc(batch, px, py, markerRadius, (marker.Cutter ? CutterTint : TypeColor(marker.Type)) * alpha,
                    marker.InFlight ? markerRadius * (1f - PROFILE_FLIGHT_RING) : 0f);

                if (crossed)
                    DrawDisc(batch, px, py, markerRadius, PROFILE_ALARM, markerRadius * (1f - PROFILE_CROSSED_RING));
            }

            //The death line at the bottom — the one place the alarm's red is ALWAYS shown, because it IS the
            //warning: the line a ball must not cross. Drawn last so it sits over anything that has reached it.
            int deathThickness = Math.Max(2, Scaled(PROFILE_DEATH_THICKNESS));
            int deathLineY = (int)MathF.Round(WorldToPanelY(deathY));
            batch.Draw(pixel, new Rectangle(panelX, deathLineY - deathThickness / 2, width, deathThickness), PROFILE_ALARM);
        }

        /// <summary>
        /// A filled disc — or, with <paramref name="innerRadius"/> above zero, a RING — drawn from a small
        /// procedural texture the HUD generates once per geometry and caches, whose texels carry the disc's
        /// exact <b>area coverage</b> as premultiplied white. That coverage is the antialiased edge: the
        /// scanlines this replaces were one-pixel bars, each either drawn or not, so every rim was a staircase
        /// — at the magazine strip's sizes the owner read the discs as rough, and the profile's markers are
        /// the same primitive at a third of the size.
        /// <para>
        /// The texture objection this primitive was originally built around was to a <i>content asset</i> —
        /// something to premultiply, ship and keep in step. A disc baked in code from its own radii has none
        /// of that: nothing to keep in step (the geometry IS the generator's arguments), premultiplied at
        /// birth exactly the way <c>Crosshair</c>'s colour is, and exact at any size the supersample
        /// resolves. It is also cheaper to draw — one quad per disc where the scanlines cost one per row,
        /// which on a full cluster profile was hundreds of quads a frame for its markers alone.
        /// </para>
        /// <para>
        /// <paramref name="innerRadius"/> hollows the disc into a ring, which is how an in-flight ball is
        /// told apart from a settled one in the profile; a ring costs no more than a disc — the same one
        /// quad — because the hole is baked into the same coverage.
        /// </para>
        /// </summary>
        /// <param name="cx">The disc's centre. Whole pixels keep the coverage grid aligned with the pixel
        /// grid; the fractional positions the profile's markers arrive at are handled by the bilinear
        /// resample of the sprite below rather than snapped.</param>
        /// <param name="radius">The outer radius in pixels.</param>
        /// <param name="innerRadius">The hole's radius; zero for a filled disc. Below a pixel of wall the
        /// hole is not drawn at all — the honest end of a ring, which would otherwise thin into stray
        /// dots.</param>
        private void DrawDisc(SpriteBatch batch, float cx, float cy, float radius, Color color,
            float innerRadius = 0f)
        {
            int r = (int)MathF.Round(radius);
            if (r < 1)
            {
                //Sub-pixel: a single point is an honest marker, not a one-pixel circle that rounds away to nothing.
                //A ring has nothing left to hollow at this size either, so it draws the same point.
                batch.Draw(Pixel, new Vector2(cx - 0.5f, cy - 0.5f), color);
                return;
            }

            //Never let the ring close up into a disc or thin to nothing: below a pixel of wall the hole simply
            //is not drawn, which is the honest end of a ring rather than a row of stray dots.
            float inner = innerRadius > 0f && radius - innerRadius >= 1f ? innerRadius : 0f;

            Texture2D disc = DiscTexture(r, (int)MathF.Round(inner));

            //The fraction the rounding dropped is spent on the sprite's scale rather than thrown away: the
            //profile's marker radius breathes with the orbit (its pixels-per-unit follows the field's
            //turning half-depth), and a disc that stepped between whole pixels would tick once a turn where
            //one that scales ticks never. The magazine's radii are whole pixels by construction, so its
            //scale is exactly 1 and its draw is a clean 1:1 blit.
            float scale = radius / r;

            //The texture is even-sided with the disc's centre on a texel corner, so (r + 1)·scale is the
            //centre's offset from the sprite's top-left whatever the scale, and the coverage stays
            //four-fold symmetric about it.
            batch.Draw(disc, new Vector2(cx - (r + 1) * scale, cy - (r + 1) * scale), null, color,
                0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The cached disc texture for one whole-pixel geometry — the pair the dictionary is keyed on, packed
        /// into an int because the inner radius never reaches the outer. One texture per geometry, built on
        /// first use and living with the HUD's lifetime exactly as its 1×1 pixel does: the magazine strip's
        /// radii come straight off <see cref="Scaled"/> constants and are stable between resizes, and the
        /// profile's marker radius rounds to one of the few whole sizes its breathing spans.
        /// </summary>
        private Texture2D DiscTexture(int radius, int innerRadius)
        {
            int key = radius * 256 + innerRadius;
            if (_discs.TryGetValue(key, out Texture2D cached)) return cached;

            //Even-sided, with the disc's centre on the corner where the four middle texels meet: symmetric
            //about both axes, and the destination offset in DrawDisc comes out exact for a whole-pixel centre.
            int size = 2 * radius + 2;
            float centre = radius + 1f;
            float outer2 = radius * radius;
            float inner2 = innerRadius * innerRadius;

            //Eight samples to a side — sixty-four a texel — quantise the coverage to finer steps than a
            //one-pixel ramp has pixels to show. One build per geometry, so the cost is nothing per frame.
            const int SAMPLES = 8;

            byte[] data = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int covered = 0;

                    for (int sy = 0; sy < SAMPLES; sy++)
                        for (int sx = 0; sx < SAMPLES; sx++)
                        {
                            float px = x + (sx + 0.5f) / SAMPLES - centre;
                            float py = y + (sy + 0.5f) / SAMPLES - centre;
                            float d2 = px * px + py * py;

                            if (d2 <= outer2 && d2 >= inner2) covered++;
                        }

                    //Premultiplied white — the coverage scales all four channels, so the batch's default
                    //AlphaBlend composes the tint over whatever is behind it correctly at every fraction
                    byte a = (byte)(covered * 255 / (SAMPLES * SAMPLES));
                    int o = (y * size + x) * 4;
                    data[o] = a;
                    data[o + 1] = a;
                    data[o + 2] = a;
                    data[o + 3] = a;
                }

            Texture2D texture = new(_game.GraphicsDevice, size, size);
            texture.SetData(data);

            _discs[key] = texture;
            return texture;
        }

        //Every disc and ring the HUD draws, keyed by whole-pixel geometry — see DiscTexture. Never emptied:
        //a handful of small textures over the program's life, a few more per distinct window size.
        private readonly Dictionary<int, Texture2D> _discs = new();

        /// <summary>
        /// A round's disc fill: its colour as <see cref="TypeColor"/> bakes it, or, where the disc carries its colour's
        /// figure (#737), the ground's ink baked the same way - the ball's ground is the plain colour scaled to pay for
        /// the figure, and the strip shows the ball as it is painted.
        /// </summary>
        private Color DiscFill(BallType type, bool figured)
        {
            if (!figured) return TypeColor(type);

            ColourCuePaint paint = _game.Balls.ColourCueOf(type);
            return paint.IsNone ? TypeColor(type) : InkColor(paint.GroundInk);
        }

        /// <summary>
        /// The round's figure (#737) over its disc, in the figure's ink: the ball seen face-on, as
        /// <see cref="ColourCueField.BakeDisc"/> paints it, from a texture baked once per figure and radius.
        /// </summary>
        private void DrawFigure(SpriteBatch batch, float cx, float cy, int radius, BallType type, float alpha)
        {
            ColourCuePaint paint = _game.Balls.ColourCueOf(type);
            if (paint.IsNone || alpha <= 0f || radius < 1) return;

            Texture2D figure = FigureTexture(type, radius);
            batch.Draw(figure, new Vector2(cx - (radius + 1), cy - (radius + 1)), InkColor(paint.FigureInk) * alpha);
        }

        /// <summary>
        /// The cached figure texture of one colour at one whole-pixel radius, in the figure set the balls draw now
        /// (<see cref="ColourCues.SetFor"/> - each style its own). Baked on first use like <see cref="DiscTexture"/>,
        /// or ahead of the level by <see cref="PrepareFigures"/>.
        /// </summary>
        private Texture2D FigureTexture(BallType type, int radius)
        {
            int set = ColourCues.SetFor(_game.Balls.ColourCue, _game.Balls.Style);
            int key = ((set + 1) * 32 + (int)type) * 4096 + radius;
            if (_figures.TryGetValue(key, out Texture2D cached)) return cached;

            int size = 2 * radius + 2;
            Texture2D texture = new(_game.GraphicsDevice, size, size);
            texture.SetData(ColourCueField.BakeDisc(ColourCues.FigureOf(set, type), radius));

            _figures[key] = texture;
            return texture;
        }

        /// <summary>
        /// Bakes every colour's figure at the strip's two radii for the figure set the balls draw now, so the first
        /// time a colour reaches the strip does not bake it in the middle of play: the frame a round is chambered in
        /// is one the player is watching (the reason <c>PrepareLanded</c> bakes a material's sounds on the way in).
        /// Called from <see cref="Reset"/>, after the level has stated its style; a set already baked costs nothing.
        /// </summary>
        private void PrepareFigures()
        {
            if (_game.Balls?.DrawsColourCue != true) return;

            int head = Scaled(HUD_MAG_HEAD_RADIUS);
            int rest = (int)MathF.Round(head * HUD_MAG_REST_SCALE);

            foreach (BallType type in Enum.GetValues<BallType>())
            {
                FigureTexture(type, head);
                FigureTexture(type, rest);
            }
        }

        //The figure textures, keyed by figure set, colour and whole-pixel radius - see FigureTexture. Never emptied: at
        //most four sets of thirteen colours at two radii, a few more per distinct window size.
        private readonly Dictionary<int, Texture2D> _figures = new();

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

            DrawGlow(font, text, at, size, scale, glow, HUD_ACCENT, HUD_GLOW_PASSES);
            DrawString(font, text, at, colour, scale);
        }

        /// <summary>
        /// The halo behind a number that has just changed — a blurred copy of the same string, which is
        /// FontStashSharp's own <see cref="FontSystemEffect.Blurry"/> and so costs one atlas variant rather than
        /// anything per frame. Split out of <see cref="DrawPulsed"/> for #180: the capped streak draws its own
        /// text glyph by glyph, so it needs the glow without the flat-coloured string that used to come with it.
        /// </summary>
        private void DrawGlow(SpriteFontBase font, string text, Vector2 at, Vector2 size, float scale,
            float glow, Color tint, int passes)
        {
            if (glow <= 0.01f || passes <= 0) return;

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
            Color halo = tint * (glow * glow);

            for (int i = 0; i < passes; i++)
                _game.OverlayBatch.DrawString(font, text, glowAt, halo, 0f, Vector2.Zero,
                    glowScale, 0f, 0f, 0f, TextStyle.None, FontSystemEffect.Blurry, blur);
        }

        /// <summary>
        /// One string drawn <b>glyph by glyph</b>, each taking its own point on <see cref="STREAK_CAP_RAMP"/>,
        /// so a sweep travels along the number instead of the whole of it changing colour together (#180).
        /// </summary>
        /// <remarks>
        /// Advancing by each glyph's own measured width drops kerning between the pairs, which for the two or
        /// three characters of a multiplier badge is under a pixel and is the price of the effect — it is not
        /// worth a glyph-positioning API to recover, and it is stated here so the next person does not go
        /// looking for a bug when a capped badge sits a hair wider than the same string drawn flat.
        /// </remarks>
        private void DrawGradientString(SpriteFontBase font, string[] glyphs, Vector2 position, float scale,
            float phase, float alpha)
        {
            float x = position.X;

            for (int i = 0; i < glyphs.Length; i++)
            {
                Color colour = SampleCapRamp(phase - i * STREAK_CAP_STAGGER) * alpha;
                DrawString(font, glyphs[i], new Vector2(x, position.Y), colour, scale);

                x += font.MeasureString(glyphs[i]).X * scale;
            }
        }

        /// <summary>
        /// Samples the capped streak's ramp at a wrapped phase, interpolating between neighbours so the sweep
        /// is continuous rather than stepping between six flat colours.
        /// </summary>
        private static Color SampleCapRamp(float phase)
        {
            int count = STREAK_CAP_RAMP.Length;

            //Wrapped into 0…1 the way that survives a negative phase, which the per-glyph stagger produces
            float wrapped = phase - MathF.Floor(phase);
            float scaled = wrapped * count;

            int index = (int)scaled;
            float blend = scaled - index;

            return Color.Lerp(STREAK_CAP_RAMP[index % count], STREAK_CAP_RAMP[(index + 1) % count], blend);
        }

        /// <summary>
        /// One string with a dark copy behind it. Colours are premultiplied by <c>Color * float</c> — SpriteBatch's
        /// default AlphaBlend expects premultiplied colour, and that operator scales all four channels — so a
        /// faded popup's shadow fades with it rather than outliving the text it was drawn for.
        /// </summary>
        private void DrawString(SpriteFontBase font, string text, Vector2 position, Color colour,
            float scale = 1f, bool shadow = true) =>
            DrawString(font, text, position, colour, scale, shadow, Math.Max(1, Scaled(HUD_BACKING_BLUR)),
                Scaled(HUD_SHADOW_OFFSET) * scale);

        /// <summary>
        /// The same, with the backing's blur and the offset copy's distance given rather than derived from
        /// <paramref name="scale"/>: an award is drawn from several faces as it zooms (#846, <see cref="AwardFace"/>), and
        /// its backing and shadow have to follow the size it is drawn at, not the scale of whichever face drew it.
        /// </summary>
        private void DrawString(SpriteFontBase font, string text, Vector2 position, Color colour,
            float scale, bool shadow, int blur, float shadowOffset)
        {
            Vector2 scaling = new(scale);

            if (shadow)
            {
                float alpha = colour.A / 255f;

                //The soft backing first, then the offset copy on top of it: the offset is the crisp lift and
                //has to stay crisp, the backing is the ground it lifts off. Centred by its OWN measurement,
                //because a blurred glyph is a larger bitmap with its own render offset — measured from the
                //text's origin it lands down and to the right of what it backs, which is the identical trap
                //DrawGlow's comment records for the halo, and it looks the same way: a smudge beside the
                //number rather than under it.
                Vector2 size = font.MeasureString(text) * scale;
                Vector2 backingSize = font.MeasureString(text, null, 0f, 0f, FontSystemEffect.Blurry, blur) * scale;
                Vector2 backingAt = position + (size - backingSize) * 0.5f;

                for (int i = 0; i < HUD_BACKING_PASSES; i++)
                    _game.OverlayBatch.DrawString(font, text, backingAt, HUD_BACKING * alpha, 0f, Vector2.Zero,
                        scaling, 0f, 0f, 0f, TextStyle.None, FontSystemEffect.Blurry, blur);

                _game.OverlayBatch.DrawString(font, text, position + new Vector2(shadowOffset),
                    HUD_SHADOW * alpha, 0f, Vector2.Zero, scaling);
            }

            _game.OverlayBatch.DrawString(font, text, position, colour, 0f, Vector2.Zero, scaling);
        }

        #endregion

        #region The tutorial card (#189)

        //The card: a keycap, a mouse or a trigger out of the prompt font, the line of what to do beside it and
        //a smaller line under that, top centre — the one strip along the top that is empty in both poses (the
        //FPS line owns the top left and the score the top right), and the place the eye passes between the
        //cluster and the score. No plate, on the HUD's own rule: the text carries its backing and its shadow
        //like every other readout, so the card reads as a line spoken over the scene rather than a box nailed
        //to it. Its state is Tutorial's; this draws what it reads there, the way the rest of the HUD reads the
        //score keeper.
        private const int HUD_TUTORIAL_GLYPH_GAP = 48;
        private const int HUD_TUTORIAL_LINE_GAP = 10;

        //Between the detail line and the praise word that lands under it (#466). Wider than the line gap above
        //it, because that gap binds two halves of one instruction and this one separates the instruction from
        //the answer to it.
        private const int HUD_TUTORIAL_PRAISE_GAP = 26;

        //How much of the top strip the card leaves between itself and the corner readouts (#461). The card is
        //CENTRED, so what bounds it is the nearer top corner mirrored about the centre — and that corner is the
        //score's, not the FPS line's: the score is the wider of the two at every size the game runs (five
        //digits of the display face measure 237 design units against the FPS line's ~145 of Segoe UI), so a
        //card that clears the score clears the line as well. Stated as a distance rather than as an aspect, so
        //the owner's 21.6:9 panel simply gets a wider strip than 16:9 does and a narrow window a tighter one.
        private const int HUD_TUTORIAL_CLEARANCE = 80;

        //The pop-in starts this small and overshoots to full through HudEase.OutBack: a card that lands with a
        //bounce reads as the game offering something, where one that fades in reads as a notice. The praise
        //kicks the same spring the score takes, so a lesson done is answered the way a point scored is.
        private const float HUD_TUTORIAL_ARRIVE_FROM = 0.6f;
        private const float HUD_TUTORIAL_PRAISE_KICK = 0.3f;

        //A slow bob while a card waits — motion is what says "this is asking you" over a still frame, and it
        //is far smaller and slower than the balls-left breath, which is the alarm's shape and must stay it
        private const int HUD_TUTORIAL_BOB = 5;
        private const float HUD_TUTORIAL_BOB_PERIOD = 2.8f;

        private Pulse _tutorialPulse;

        /// <summary>
        /// An action on the card was just done: its praise word pops the way the score does on a hit — the word
        /// alone, so the instruction over it stays put (#673); the send-off, which has no praise row, pops whole.
        /// </summary>
        internal void KickTutorial() => _tutorialPulse.Kick(HUD_TUTORIAL_PRAISE_KICK);

        #region The skip hint (#499, #699)

        //Every camera takeover a click can skip says so, a moment after it starts (#699): the drop cinematic
        //(DropCinematic.TrySkip) and the chapter's flythrough (ChapterIntro.TrySkip), both past a 0.3 s lockout so the
        //button that started them cannot skip them. #499 showed it on a session's first three drop cinematics or until
        //the player skipped one, and then never again - "it does not nag"; the owner's verdict was the opposite: a
        //reminder is wanted every time one is sat through, so the counter and the retirement are gone. The line loss
        //is not skippable (LineLossCinematic: it is the explanation of the loss) and so has none, and nor has the drop of
        //the shot that cleared the level (the owner, 2026-10-04: the player has finished, there is nothing to skip to),
        //though that one still skips - GameplayScreen hands it in as not running.
        //
        //The drop's waits a second - past the lockout, and long enough for the player to have seen what they did. The
        //tour's a second and a half: it is a flight of 9.5 s (4.5 s after a prologue), and a hint over its first
        //instant is a hint over the scene's own arrival. Neither appears with less than SKIP_HINT_MIN_REMAINING left,
        //where it would come up only to go.
        private const float SKIP_HINT_DELAY = 1.0f;
        private const float SKIP_HINT_INTRO_DELAY = 1.5f;
        private const float SKIP_HINT_MIN_REMAINING = 1.0f;
        private const float SKIP_HINT_FADE = 0.35f;
        private const string SKIP_HINT_MOUSE = "Click or Space to skip";
        private const string SKIP_HINT_PAD = "to skip";
        private const int HUD_SKIP_HINT_GAP = 10;

        private float _skipHintAlpha;
        private bool _skipHintShown;

        /// <summary>
        /// Once a frame from the screen, with the running takeover's own state — the HUD's Update knows no takeover.
        /// <paramref name="remaining"/> is how long the takeover still runs when that is known (the tour's), and
        /// <see cref="float.MaxValue"/> when it is not (a drop ends when the drop is over).
        /// </summary>
        internal void UpdateSkipHint(float elapsed, bool running, bool chapterIntro, float sinceStart, float remaining)
        {
            float delay = chapterIntro ? SKIP_HINT_INTRO_DELAY : SKIP_HINT_DELAY;
            bool wanted = running && sinceStart >= delay && (_skipHintShown || remaining >= SKIP_HINT_MIN_REMAINING);

            //Once up, it stays up to the takeover's end however little is left
            if (wanted) _skipHintShown = true;
            if (!running) _skipHintShown = false;

            float target = wanted ? 1f : 0f;
            float step = elapsed / SKIP_HINT_FADE;
            _skipHintAlpha = _skipHintAlpha < target ? MathF.Min(target, _skipHintAlpha + step) : MathF.Max(target, _skipHintAlpha - step);
        }

        /// <summary>Whether the hint is on the frame at all (#699).</summary>
        internal bool SkipHintShowing => _skipHintAlpha > 0.005f;

        /// <summary>
        /// The hint and nothing else, into the overlay batch: the chapter's tour hides the whole HUD (#693), and the
        /// hint is the one line of it that belongs over the tour (#699).
        /// </summary>
        internal void DrawSkipHintAlone(Tutorial tutorial) => DrawSkipHintAlone(tutorial.OnGamepad, tutorial.SkipGlyph);

        /// <summary>
        /// The same hint over a takeover no session owns (#861: the Scene page's replayed tour), in the hand the menus last
        /// saw: the very line the chapter's own flythrough shows, so the two read as one thing.
        /// </summary>
        internal void DrawSkipHintAlone(bool onGamepad) => DrawSkipHintAlone(onGamepad, Tutorial.SkipGlyphFor(onGamepad));

        private void DrawSkipHintAlone(bool onGamepad, string glyph)
        {
            _game.EnsureHudFonts();

            SpriteBatch batch = _game.OverlayBatch;
            batch.Begin();
            DrawSkipHint(onGamepad, glyph, _game.GraphicsDevice.Viewport, Scaled(HUD_MARGIN));
            batch.End();
        }

        //Bottom centre, under the action and clear of the queue and the score: a glyph and a short line in the
        //tutorial's detail font, faded by the hint's own presence. The glyph is the fire button's, since that is
        //what skips, in whichever device the tutorial last saw.
        private void DrawSkipHint(Tutorial tutorial, Viewport viewport, int margin) =>
            DrawSkipHint(tutorial.OnGamepad, tutorial.SkipGlyph, viewport, margin);

        private void DrawSkipHint(bool onGamepad, string glyph, Viewport viewport, int margin)
        {
            float alpha = _skipHintAlpha;
            if (alpha <= 0.005f) return;

            string caption = onGamepad ? SKIP_HINT_PAD : SKIP_HINT_MOUSE;

            SpriteFontBase glyphFont = _game.HudFontPrompt;
            SpriteFontBase captionFont = _game.HudFontTutorialDetail;

            Vector2 glyphSize = glyphFont.MeasureString(glyph);
            Vector2 captionSize = captionFont.MeasureString(caption);
            float gap = Scaled(HUD_SKIP_HINT_GAP);
            float width = glyphSize.X + gap + captionSize.X;
            float height = MathF.Max(glyphSize.Y, captionSize.Y);

            Vector2 origin = new(viewport.Width * 0.5f - width * 0.5f, viewport.Height - margin - height);

            DrawString(glyphFont, glyph, origin + new Vector2(0f, (height - glyphSize.Y) * 0.5f), BS3DGame.MENU_TEXT * alpha, 1f);
            DrawString(captionFont, caption, origin + new Vector2(glyphSize.X + gap, (height - captionSize.Y) * 0.5f), HUD_CAPTION * alpha, 1f);
        }

        #endregion

        //THE KIND CARD'S BALL (#735): a card introducing a kind shows the ball itself where a keycap would stand, drawn LIVE
        //in the frame by the session (GameplayScreen.CollectTutorialBall) rather than as a picture, so the card always shows
        //the kind's current look. This HUD only says where: the glyph's square, kept here as the frame's own coordinates
        //(-1..1, y up) for the next frame's 3D draw. One frame late, which no eye reads on a card that bobs this slowly.
        private Vector2 _tutorialBallAt;
        private float _tutorialBallRadius;

        //The ball's square on the card, design units: between the caption's 200 and a keycap's 228
        private const int HUD_TUTORIAL_BALL = 200;

        /// <summary>
        /// Where the card's ball goes (#735), in the frame's normalised coordinates (x and y −1..1, y up), and its radius
        /// as a share of the frame's half-height; false while no kind card is up. What the HUD drew last.
        /// </summary>
        internal bool TryGetTutorialBall(out Vector2 at, out float radius)
        {
            at = _tutorialBallAt;
            radius = _tutorialBallRadius;
            return radius > 0f;
        }

        /// <summary>
        /// The tutorial's card, if one is up: the glyphs, the line and the smaller line laid out as one block
        /// and scaled about its own centre, so the arrival's bounce swells it in place rather than shouldering
        /// it along the top of the frame; the praise word hangs under that block and takes the praise's kick
        /// about its own centre (<see cref="TutorialCardLayout"/>, #673). Every colour is premultiplied by the
        /// card's presence, as every fading readout here is, so the backing and the shadow fade with the text.
        /// <para>
        /// <b>The praise word is a third line under the detail since #466</b>, where it used to <i>be</i> the
        /// caption — the card flipped on the frame the action landed and took its detail line away with it, so
        /// the lessons a player does fastest showed their instruction for a fraction of a second. It goes
        /// underneath rather than above, which is the arrangement the issue offered: a row above has to be
        /// <i>reserved</i> from the card's arrival or it shoves the caption down the frame at the instant the
        /// player has earned the right to re-read it — and reserved, it pushes every instruction a line deeper
        /// into the cluster for a row that is empty most of the time. Built both ways and photographed.
        /// Underneath, the card grows into the space below it and the order reads the way it happens: do this,
        /// then well done. It costs height and not width, so the strip clamp below is unaffected.
        /// </para>
        /// <para>
        /// <b>Nothing already read moves when it lands, since #673</b> — which #466 promised and the layout did
        /// not keep: the praise was a member of the block, so on its frame the block re-centred (beside the aim
        /// card's taller glyph the text rose until it was the taller of the two, beside the fire card's taller
        /// text the glyph sank), the idle bob stopped dead, and the kick swelled everything by 1.3× about a
        /// centre that now included the praise row. An action done during the card's arrival also snapped it to
        /// full size on the praise's frame (<c>Tutorial.Complete</c>). The owner's report was that the player reads both lines again. Now the
        /// instruction is laid out from its own geometry alone, the bob keeps its clock, and only the praise
        /// word springs; <c>TutorialCardLayoutTests</c> pins it, and failed on the old layout by 18 to 35 pixels.
        /// </para>
        /// </summary>
        /// <param name="scoreLeft">The left edge of the score block, which is what the card may not reach.</param>
        private void DrawTutorial(Tutorial tutorial, Viewport viewport, int margin, float scoreLeft)
        {
            float presence = tutorial.Presence;
            if (presence <= 0.005f) return;

            string caption = tutorial.Caption;
            if (string.IsNullOrEmpty(caption)) return;

            string glyph = tutorial.Glyph;
            string detail = tutorial.Detail;
            string praise = tutorial.Praise;

            SpriteFontBase glyphFont = _game.HudFontPrompt;
            SpriteFontBase captionFont = _game.HudFontTutorial;
            SpriteFontBase detailFont = _game.HudFontTutorialDetail;

            //A kind card keeps a square for its ball where the keycap would be (#735)
            bool ballCard = tutorial.CardKind != null;
            Vector2 glyphSize = ballCard ? new Vector2(Scaled(HUD_TUTORIAL_BALL))
                : string.IsNullOrEmpty(glyph) ? Vector2.Zero : glyphFont.MeasureString(glyph);
            Vector2 captionSize = captionFont.MeasureString(caption);
            Vector2 detailSize = string.IsNullOrEmpty(detail) ? Vector2.Zero : detailFont.MeasureString(detail);
            Vector2 praiseSize = string.IsNullOrEmpty(praise) ? Vector2.Zero : captionFont.MeasureString(praise);

            float gap = glyphSize.X > 0f ? Scaled(HUD_TUTORIAL_GLYPH_GAP) : 0f;
            float lineGap = detailSize.Y > 0f ? Scaled(HUD_TUTORIAL_LINE_GAP) : 0f;
            float praiseGap = praiseSize.Y > 0f ? Scaled(HUD_TUTORIAL_PRAISE_GAP) : 0f;

            float alpha = MathHelper.Clamp(presence, 0f, 1f);
            float arrive = MathHelper.Lerp(HUD_TUTORIAL_ARRIVE_FROM, 1f, HudEase.OutBack(alpha));

            //THE CARD IS SCALED TO THE STRIP IT HAS, and at the shipped sizes this never bites (#461): the
            //widest card of the fifteen measures 2552 design units against ~3020 of strip at 16:9, and the
            //arrival's overshoot swells it far less than that. What it is for is the two cases no authored
            //figure can answer — a window narrower than 16:9, and a caption someone adds later — where a card
            //would otherwise walk into the score. It caps the animated scale rather than the layout, so a
            //clamped card cannot bounce past the number either; the praise word has its own (#673).
            float halfStrip = MathF.Max(0f, scoreLeft - Scaled(HUD_TUTORIAL_CLEARANCE) - viewport.Width * 0.5f);
            TutorialCardLayout card = TutorialCardLayout.Compute(glyphSize, captionSize, detailSize, praiseSize,
                gap, lineGap, praiseGap, viewport.Width * 0.5f, margin, tutorial.Age, Scaled(HUD_TUTORIAL_BOB),
                HUD_TUTORIAL_BOB_PERIOD, arrive, _tutorialPulse.Scale, halfStrip);
            float scale = card.Scale;

            if (ballCard)
            {
                //Shrunk with the card's presence as well as scaled with it: the ball is opaque, so it cannot fade with
                //the text and leaves by growing small instead
                Vector2 centre = card.GlyphAt + glyphSize * (scale * 0.5f);
                _tutorialBallAt = new Vector2(centre.X / viewport.Width * 2f - 1f, 1f - centre.Y / viewport.Height * 2f);
                _tutorialBallRadius = glyphSize.Y * scale * 0.5f * alpha / (viewport.Height * 0.5f);
            }
            else if (glyphSize.X > 0f)
                DrawString(glyphFont, glyph, card.GlyphAt, BS3DGame.MENU_TEXT * alpha, scale);

            Vector2 captionAt = card.CaptionAt;

            //The send-off that closes the ladder wears the praise's dress from the moment it lands (#459):
            //there is nothing left on it to earn, so it arrives celebrating instead of turning amber later.
            if (tutorial.Celebrating)
                DrawGlow(captionFont, caption, captionAt, captionSize, scale, tutorial.PraiseHeat, HUD_ACCENT,
                    HUD_GLOW_PASSES);

            DrawString(captionFont, caption, captionAt,
                (tutorial.Celebrating ? HUD_ACCENT : BS3DGame.MENU_TEXT) * alpha, scale);

            if (detailSize.Y > 0f)
                DrawString(detailFont, detail, card.DetailAt, HUD_CAPTION * alpha, scale);

            //The praise word, once the action has been done (#466): the accent, because a lesson done IS gain —
            //the one thing the accent means here — and it flares the way the score does on a hit, the same
            //amber and the same blurred halo, gone as the word settles.
            if (praiseSize.Y > 0f)
            {
                DrawGlow(captionFont, praise, card.PraiseAt, praiseSize, card.PraiseScale, tutorial.PraiseHeat,
                    HUD_ACCENT, HUD_GLOW_PASSES);
                DrawString(captionFont, praise, card.PraiseAt, HUD_ACCENT * alpha, card.PraiseScale);
            }
        }

        #endregion

        #region Curves and colour

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

        /// <summary>
        /// A colour-cue ink (#737), linear already, baked for the HUD exactly as <see cref="BakeTypeColors"/> bakes a
        /// type's colour - lit at the nominal level with the sheen, tonemapped, encoded - so a figure's ground and ink
        /// sit beside the plain colours on the same terms.
        /// </summary>
        private static Color InkColor(Vector3 linearInk)
        {
            Vector3 mapped = AcesFilmic(linearInk * TYPE_LIT + new Vector3(TYPE_SHEEN));
            return new Color(LinearToSrgb(mapped.X), LinearToSrgb(mapped.Y), LinearToSrgb(mapped.Z));
        }

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
