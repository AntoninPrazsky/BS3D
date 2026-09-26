using Microsoft.Xna.Framework;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Tools;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The ceiling's descent</b> — the state machine that walks the glass plate down: a step is queued (by the
    /// shot count or by a tall level's feed), held, gated behind any cinematic already running, started, slid
    /// and flashed. It owns every figure of that sequence and nothing else: the kinematic body the cluster hangs
    /// from stays with <see cref="GameplayScreen"/>, which writes <see cref="Y"/> into it when
    /// <see cref="Slide"/> says the plate moved, and so do the step's announcements (the wake, the ripple, the
    /// sound, the rumble, the tutorial card and the log line), which <see cref="Update"/> hands back as "a step
    /// began" for the screen to perform.
    /// </summary>
    /// <remarks>
    /// Extracted out of <c>GameplayScreen.Ceiling.cs</c> in #582, where thirteen fields across two files and a
    /// hand-written reset split between <c>FitFieldToMap</c> and <c>BuildLevel</c> carried it. A level's reset is
    /// now one call, <see cref="Reset"/>. Device-free: it reads no simulation and draws nothing.
    /// </remarks>
    internal sealed class CeilingDescent
    {
        //The descending ceiling — the second of the two pressures that can lose a level, made visible where the
        //shot budget is made numerical. Every ceilingStep shots the glass steps down by CEILING_DESCENT_PER_STEP,
        //and with it the cluster (the top level is held to the body by BallSocket constraints, so moving the body
        //drags the structure along — Bepu does the work). The level is lost the moment any ball crosses the death
        //line, which is above the gun and the drain so a cluster reaching into them reads as a loss before it
        //reads as a bug.
        //
        //The descent is animated at constant velocity per step rather than teleported: a hundred constrained
        //bodies jerked in one write can throw the solver, and a short slide lets the contact between a descending
        //cluster and anything below it resolve. The body is kinematic and this build's integrator does not move
        //kinematics from their velocity (PoseIntegratorCallbacks.IntegrateVelocityForKinematics is false), so the
        //slide is driven by writing the pose — in small steps, which is what makes it tolerable to the solver.
        internal const float CEILING_DESCENT_PER_STEP = 0.6f;        //world units the glass drops each step
        private const float CEILING_DESCENT_SPEED = 1.5f;           //units/sec while a step is sliding in

        //The line the glass is never walked past — the very figure GameplayScreen.CEILING_DEATH_Y decides the
        //loss by, asked of ClusterHang rather than copied (see that constant for why it lives there).
        private const float DEATH_Y = ClusterHang.DEATH_Y;

        //Linear, so it genuinely ends — the CameraShake rule. Long enough to be seen even if the eye is on the
        //other half of the frame when it fires, short enough not to sit there as decoration.
        private const float CEILING_FLASH_SECONDS = 1.1f;

        //Linear radiance, well over GLARE_THRESHOLD so the plate blooms rather than merely turning pink. Red
        //with almost nothing in the other two channels: this is the game's one alarm COLOUR — the ceiling
        //flash and the floor net both take it (LaserGrid is handed this very constant), so the two read as
        //one warning at two heights — and it should not be mistakable for anything the scene does on its own.
        //Far over 1, and it has to be: the plate is 35 % opaque, so most of what is seen where the glass is is
        //the sky BEHIND it. At 1.5 the red merely tinted that blue-white and the plate came out pink. The
        //emissive is added on top of the composite, so it is the number that has to out-shout the sky.
        internal static readonly Vector3 CEILING_FLASH_COLOR = new(6f, 0.15f, 0.1f);

        /// <summary>
        /// What the glass says instead when the descent is a <b>feed</b> — a tall level handing the player
        /// more of its column because they have just cleared a lot of it. Cold blue-white rather than red,
        /// and the reason is the whole point of separating them: nothing has gone wrong. The player has
        /// played well and the game is answering; a red flash there tells them off for it.
        /// <para>
        /// Balanced the same way <see cref="CEILING_FLASH_COLOR"/> is and for the same reason — the plate is
        /// 35 % opaque and the emissive is added over the sky behind it — so this is bright enough to read as
        /// the glass lighting up rather than as the sky changing.
        /// </para>
        /// <para>
        /// <b>Deep blue and not a bright one.</b> The first pairing carried enough green (2.2 against the
        /// blue's 6) to come out cyan-white over a lit sky, which reads as the glass being <i>blown out</i>
        /// rather than as it saying something — the same washing the red's own doc warns about from the other
        /// end. Nearly all the green is gone, and the blue keeps the level it needs to be seen through a
        /// 35 %-opaque plate.
        /// </para>
        /// </summary>
        internal static readonly Vector3 CEILING_FEED_COLOR = new(0.04f, 0.5f, 5f);

        //How long a step waits after the shot that earned it. Long enough for that shot to have landed and a
        //drop cinematic to have engaged if it is going to — the shot leaves at SHOOT_SPEED and lands in about
        //a tenth of a second, so this is generous — and short enough that on an ordinary shot the descent
        //still reads as the answer to firing.
        private const float CEILING_STEP_HOLD = 0.45f;

        /// <summary>
        /// Where the glass hangs now: <c>CeilingPlate.CentreYAbove</c> the field's top level at rest — the
        /// plate's own clearance (<c>CeilingPlate.CLEARANCE</c>, which carries the note about the cluster coming
        /// to rest a unit under the plate rather than settling on its lattice) applied to the base the session
        /// picks — and lower by every descent since. The kinematic body and the drawn glass box both sit here,
        /// and the box is drawn straight from the body's pose (see <c>KinematicBody</c>), so the collidable and
        /// the thing the player sees cannot drift apart.
        /// </summary>
        public float Y { get; private set; }

        /// <summary>
        /// Where the glass hangs at REST, i.e. <see cref="Y"/> before the level's first descent — solved per level
        /// with the field's top, so it carries the raise a deep field gets off the death line. The HUD's cluster
        /// profile frames itself against this, and kept its own hardcoded copy of the unraised figure until a
        /// 27-level field put the whole cluster above the panel's top (see <c>PlayHud.ClusterProfile.TopY</c>).
        /// </summary>
        public float RestY { get; private set; }

        //Where the glass body sits now (Y) and where it is sliding to (TargetY). Equal while at rest; TargetY is
        //lowered by BeginStep and Y catches up in Slide, step by step.
        /// <summary>Where the glass is sliding to — <see cref="Y"/> while it is at rest.</summary>
        public float TargetY { get; private set; }

        /// <summary>
        /// How hard the glass is glowing right now, 1 at the moment of a step and decaying to nothing. It is
        /// the descent's announcement: a translucent plate sliding down against the sky is close to invisible
        /// while the player's eye is on the cluster, so the pressure the rule exists to apply was arriving
        /// without being noticed at all.
        /// </summary>
        public float Flash { get; private set; }

        /// <summary>
        /// Which of the two the glass and the wave are currently saying. Set the instant a descent is started
        /// and read while it is on screen, because the flash and the ripple outlive the call that began them.
        /// </summary>
        public Vector3 FlashColor { get; private set; } = CEILING_FLASH_COLOR;

        /// <summary>
        /// Whether the flash currently on screen is a feed rather than pressure. <see cref="FlashColor"/> is what
        /// the 3D plate needs; this is what the <b>HUD</b> needs, which picks its own display-space colour rather
        /// than converting a linear radiance — see <c>PlayHud.PROFILE_ALARM</c> for why.
        /// </summary>
        public bool FlashIsFeed { get; private set; }

        //Ceiling steps that have come due but are waiting for their moment — see Update. A count, because a
        //ceilingStep of 1 steps on every shot and two of them inside the hold must not lose one.
        private int _stepsPending;
        private float _stepHold;
        private float _stepWaited;
        private bool _descending;

        /// <summary>
        /// The lowest occupied level the tall level was authored with — the height its underside is <b>fed
        /// back down to</b> as the player clears it. See <see cref="Feed"/>.
        /// </summary>
        private byte _feedFloorLevel;

        /// <summary>How many descents the feed has already asked for, so it never asks for the same one twice.</summary>
        private int _feedStepsQueued;

        /// <summary>
        /// How many of the steps still waiting in <c>_stepsPending</c> were asked for by the feed rather than by
        /// the shot count — which is what decides whether the glass flashes red or blue when one of them comes
        /// down. A count and not a flag: the two kinds can be queued together (a landing that both spends a shot
        /// and clears a band), and they come down one at a time.
        /// </summary>
        private int _feedStepsPending;

        /// <summary>
        /// Starts a level's descent over: the glass at rest at <paramref name="restY"/> with nothing to slide to,
        /// and the tall-level feed measured from <paramref name="feedFloorLevel"/>.
        /// </summary>
        /// <param name="restY">Where this level's glass hangs before its first descent.</param>
        /// <param name="feedFloorLevel">The installed map's lowest occupied level — see <see cref="Feed"/>.</param>
        public void Reset(float restY, byte feedFloorLevel)
        {
            Y = restY;
            //Kept, because Y is about to start descending and the HUD's profile has to go on framing the whole
            //fall against where the glass STARTED — raise included.
            RestY = restY;
            //At rest to start: target equals current, so nothing slides until a step is taken.
            TargetY = restY;
            _descending = false;

            //The glass is a fresh plate at the top of a fresh field, so nothing about the last level's last
            //descent should still be glowing on it — nor should a step it queued and never got to take come
            //down on the new one.
            Flash = 0f;
            _stepsPending = 0;
            _stepHold = 0f;
            _stepWaited = 0f;

            //Where this level hangs its underside is what a tall one is fed back down to, so it is read off
            //the installed map rather than being a constant — see Feed
            _feedFloorLevel = feedFloorLevel;
            _feedStepsQueued = 0;
            _feedStepsPending = 0;
            FlashColor = CEILING_FLASH_COLOR;
            FlashIsFeed = false;
        }

        /// <summary>
        /// Queues the step the shot count just forced. <b>Queued</b> rather than started, because the shot has
        /// not landed yet and the descent must not collide with what it is about to do — see <see cref="Update"/>.
        /// </summary>
        public void QueuePressureStep()
        {
            _stepsPending++;
            _stepHold = CEILING_STEP_HOLD;
        }

        /// <summary>
        /// Queues however many descents a tall column's underside, now at <paramref name="lowest"/>, is owed —
        /// the arithmetic of <c>GameplayScreen.FeedTallColumn</c>, which says why the feed exists and when it is
        /// asked.
        /// </summary>
        /// <param name="lowest">The cluster's lowest occupied level right now.</param>
        /// <param name="risen">How far, in world units, the underside has climbed above where the level hung it.</param>
        /// <returns>How many steps were queued by this call; zero when nothing new is owed.</returns>
        public int Feed(byte lowest, out float risen)
        {
            risen = 0f;
            if (lowest <= _feedFloorLevel) return 0;

            //How far the underside has climbed out of reach, in world units, and how many whole descents
            //cover it. Whole ones only: a part-step owed now is owed again next landing, and rounding up
            //would walk the glass down a little further than the level was ever cleared.
            risen = (lowest - _feedFloorLevel) / Constants.SQRT_TWO;
            int owed = (int)(risen / CEILING_DESCENT_PER_STEP) - _feedStepsQueued;

            if (owed <= 0) return 0;

            _feedStepsQueued += owed;
            _stepsPending += owed;
            _stepHold = CEILING_STEP_HOLD;

            //And these ones do not read as an alarm. A descent the ceiling forces on the player is a threat
            //and burns red; this one is the game handing over more of the column BECAUSE they cleared a lot
            //of it, so the glass and the wave go cold blue instead. Counted here rather than decided at the
            //descent, because by the time a queued step comes down the reason it was queued is gone.
            _feedStepsPending += owed;

            return owed;
        }

        /// <summary>
        /// Lets a queued ceiling step go, once it will not be read as a punishment for the shot that earned it,
        /// and fades the glass's glow.
        /// <para>
        /// The step comes due on the <b>frame the shot is fired</b>, but the shot leaves at 200 u/s and lands
        /// about a tenth of a second later — so the glass flashing red and driving its alarm wave down the
        /// cluster landed on top of the drop cinematic, and a player who had just cut a large group loose was
        /// shown the game's one punishment animation while watching their reward. It read as having done
        /// something wrong. Nothing was wrong; only the order was.
        /// </para>
        /// <para>
        /// So a step waits for two things: a short hold, long enough for the shot to land and a cinematic to
        /// engage if one is going to, and then for that cinematic to be over. It is a <b>count</b> rather than
        /// a flag because a level with <c>ceilingStep</c> of 1 steps on every shot, and two shots inside the
        /// hold must not lose one of them; and the hold is re-armed per release rather than shared, so queued
        /// steps come down one at a time instead of as a single double-height lurch.
        /// </para>
        /// </summary>
        /// <param name="elapsed">Wall-clock seconds since the last frame.</param>
        /// <param name="levelDecided">Whether the level is already won or lost (#563).</param>
        /// <param name="takeoverEngaged">Whether a camera takeover (a cinematic, the chapter intro) has the lens.</param>
        /// <param name="feeding">When a step began: whether it was one the feed asked for rather than the pressure.</param>
        /// <param name="waited">When a step began: the seconds it spent queued before it was let go.</param>
        /// <returns>
        /// True when a step began this frame — the caller's cue to announce it (the wake, the ripple, the sound,
        /// the rumble, the tutorial and the log line).
        /// </returns>
        public bool Update(float elapsed, bool levelDecided, bool takeoverEngaged, out bool feeding, out float waited)
        {
            bool began = Release(elapsed, levelDecided, takeoverEngaged, out feeding, out waited);

            //The glass's glow fades on the wall clock whether or not it moves — after the release, so a step let
            //go this frame has already begun its fade by the frame's own elapsed, as it always has.
            if (Flash > 0f) Flash = MathF.Max(0f, Flash - elapsed / CEILING_FLASH_SECONDS);

            return began;
        }

        /// <summary>The release half of <see cref="Update"/>, which carries its doc.</summary>
        private bool Release(float elapsed, bool levelDecided, bool takeoverEngaged, out bool feeding, out float waited)
        {
            feeding = false;
            waited = 0f;

            if (_stepsPending <= 0) return false;

            //Nothing comes down once the level is decided (#563). A step is queued when the shot LEAVES, so
            //the winning shot can carry one: released, it burned the alarm red, sounded the step and slid the
            //glass over the fanfare — the moment the drop cinematic let go, or CEILING_STEP_HOLD after the landing with
            //the cinematic turned off. Held rather than cleared, because nothing reads the count after this.
            if (levelDecided) return false;

            _stepWaited += elapsed;

            if (_stepHold > 0f) _stepHold -= elapsed;

            //Held out through a camera takeover for the drop cinematic's own reason — a step sliding down
            //while the camera is elsewhere would arrive unannounced. Unreachable through the chapter intro in
            //practice (nothing can be owed before a shot has landed, and the intro is over well before the
            //first one can), but the same rule either way costs nothing to state once.
            if (_stepHold > 0f || takeoverEngaged) return false;

            _stepsPending--;
            _stepHold = CEILING_STEP_HOLD;

            waited = _stepWaited;
            _stepWaited = 0f;

            return BeginStep(out feeding);
        }

        /// <summary>
        /// Begins one step of the ceiling's descent: lowers the target by <see cref="CEILING_DESCENT_PER_STEP"/>,
        /// clamped at the death line so an overlong level cannot drive the glass through the gun. The body itself
        /// does not move here — <see cref="Slide"/> slides it to the target, which is what keeps a hundred
        /// constrained bodies from being jerked in a single write.
        /// </summary>
        /// <returns>False when the glass is already as low as it goes, and so nothing began.</returns>
        private bool BeginStep(out bool feeding)
        {
            feeding = false;

            //No target to reach if the glass is already as low as it can go — further steps would be a no-op and
            //a needless log, and clamping here is what stops an inconsistent level (more steps than the geometry
            //allows) from scraping the body past the death line.
            if (TargetY <= DEATH_Y) return false;

            TargetY = MathF.Max(DEATH_Y, TargetY - CEILING_DESCENT_PER_STEP);
            _descending = true;

            //The descent itself is a slow slide of a translucent plate against a sky, which is very nearly
            //invisible while the player is watching the cluster — the pressure the whole rule exists to apply
            //was arriving unnoticed. So the glass says it: it lights up, and (the caller's half) drives a wave
            //down through every ball hanging on it.
            //
            //In WHICH colour is the difference between a threat and a reward. A step the shot count forced is
            //the pressure and burns red. A step the FEED asked for is a tall level handing over more of its
            //column because the player just cleared a great deal of it — nothing has gone wrong, and a red
            //flash there tells them off for playing well. Feed steps are spent first, so a landing that
            //queues both kinds says the good news first.
            feeding = _feedStepsPending > 0;
            if (feeding) _feedStepsPending--;

            FlashIsFeed = feeding;
            FlashColor = feeding ? CEILING_FEED_COLOR : CEILING_FLASH_COLOR;

            Flash = 1f;
            return true;
        }

        /// <summary>
        /// Slides the glass toward <see cref="TargetY"/> at <see cref="CEILING_DESCENT_SPEED"/> by one physics
        /// step's worth. Called by <c>GameplayScreen.SlideCeiling</c> before each physics step (#577), so the
        /// solver works against the moved body and the contact between a descending cluster and anything below it
        /// resolves rather than interpenetrates — and so the plate moves exactly as far as the world it drags lives
        /// through, whatever the frame rate.
        /// </summary>
        /// <param name="step">The physics step's length, in seconds.</param>
        /// <returns>True when <see cref="Y"/> changed and the body has to be moved to it.</returns>
        public bool Slide(float step)
        {
            if (!_descending) return false;

            //Equal within a hair means the slide is done — a step that would otherwise move a thousandth of a
            //unit and never quite arrive. Snap, stop, and the matrix reflects the final pose exactly.
            if (MathF.Abs(Y - TargetY) <= CEILING_DESCENT_SPEED * step)
            {
                Y = TargetY;
                _descending = false;
            }
            else
            {
                Y -= CEILING_DESCENT_SPEED * step;
            }

            return true;
        }
    }
}
