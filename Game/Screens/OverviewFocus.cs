using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The overview lens focuses on the aim after a moment</b> (#1006). The owner's ruling (2026-10-24), after an outside
    /// player wanted "to look at the balls, not at the cannon's backside": when the overview takes the lens it shows
    /// everything as the fit framed it, then after a while it focuses on where the gun can shoot — a closer view, the field
    /// of view allowed to narrow — and stays there; whenever the player leaves the overview (precise aim, a camera takeover)
    /// and comes back, it opens on the full view and focuses again. And always "longer" than precise aim: the focused lens
    /// stays well behind the over-the-barrel one and its field of view wider than <see cref="PreciseAim.FOV"/>.
    /// <para>
    /// <b>One amount between two poses.</b> <see cref="Amount"/> is 0 on the full overview (the fit's pose, tilted by
    /// <see cref="AimTilt"/>) and 1 on the focused one (<see cref="Focused"/>); the caller lerps position, look-at and field
    /// of view by it before precise aim's own Lerp, the way the tilt is applied (the #366 route). At 0 the overview comes
    /// back bit for bit, so the opening frame — and the chapter intro's arrival, which is that frame — is the fit's.
    /// </para>
    /// <para>
    /// <b>The amount is a chase, not a timeline.</b> While anything else holds the lens (precise aim's blend, a takeover's)
    /// it falls quickly towards 0 and the hold starts over; once the overview has the lens alone for
    /// <see cref="HOLD_OPENING_SECONDS"/> (a level's opening) or <see cref="HOLD_RETURN_SECONDS"/> (coming back from a lean
    /// or a takeover), it rises towards 1 critically damped on real time, from rest, so it eases in and never overshoots.
    /// Exact for a held target, so every frame rate lands alike.
    /// </para>
    /// <para>
    /// <b>The aim point is followed, not read.</b> What the focus looks at is a point on the aim at a depth eased between
    /// the beam's first contact and the cluster's centre (<see cref="FollowAim"/>), chased again in space: the beam's
    /// contact jumps between near and far balls as the aim sweeps across holes, and a lens fastened to it would jerk.
    /// </para>
    /// </summary>
    internal sealed class OverviewFocus
    {
        /// <summary>
        /// How long the overview holds the full frame at a level's opening before it focuses, in seconds: well past the
        /// cluster's spring from the glass (<c>ClusterStartSwing</c>'s hold and ease, 1.8 s), which the owner likes and
        /// which the full frame shows whole (#617), and long enough to take the level in. 2.5 s until the owner's verdict of
        /// 2026-10-28, "the zoom should come somewhat later".
        /// </summary>
        public const float HOLD_OPENING_SECONDS = 4f;

        /// <summary>
        /// How long it holds the full frame after a lean or a takeover hands the lens back, in seconds: a glance at the whole
        /// field, which is what coming back to the overview is for, and short, because the player is about to aim. Doubled
        /// from 0.5 s with the opening's hold (the owner, 2026-10-28: later).
        /// </summary>
        public const float HOLD_RETURN_SECONDS = 1f;

        /// <summary>
        /// The rise's time constant, <c>1/ω</c> of a critically damped chase from rest: 90 % of the way in about 1.2 s,
        /// slow enough to read as the camera settling on the aim rather than as a zoom.
        /// </summary>
        public const float RISE_SECONDS = 0.3f;

        /// <summary>The fall's time constant while something else holds the lens: most of the way out within a quarter second.</summary>
        public const float FALL_SECONDS = 0.06f;

        /// <summary>
        /// How far the focused lens rises above the full one, in world units. <b>The rise is what takes the gun out of the
        /// frame</b>: the gun stands about 13 units in front of the lens and the cluster about 30, so a raise drops the gun in
        /// the frame against the cluster, and the lens looks over the breech instead of up at it. A narrower field of view
        /// alone cannot do it, since a zoom scales the gun and the cluster alike. Kept small, because the overview stands
        /// low on purpose, to see the cluster's underside (<c>GameCameraFit.CAMERA_HEIGHT</c>): on Zigzag the angle up to
        /// the cluster's centre goes from about 15° to about 11°.
        /// </summary>
        public const float RISE = 3f;

        /// <summary>
        /// How far the focused lens moves in along its horizontal bearing, in world units: the "somewhat closer" of the
        /// ruling, about a twelfth of the stand-off on the shipped fits (30.6 units on Zigzag). It also steepens the view up
        /// at the cluster, which gives back part of what <see cref="RISE"/> flattened. 4 units until the owner's verdict of
        /// 2026-10-28, "too close, it should be somewhat further".
        /// </summary>
        public const float ADVANCE = 2.5f;

        /// <summary>
        /// How much of the way from the overview's look-at to the followed aim point the focused look-at moves: enough
        /// that an aim at the field's edge or its top row stays well inside the frame, not so much that the lens swings
        /// with every sweep of the mouse (the frame turns at about this share of the aim's own turn).
        /// </summary>
        public const float PULL = 0.6f;

        /// <summary>
        /// The focused field of view as a share of the overview's: 42.86° becomes 39.43°, still wider than
        /// <see cref="PreciseAim.FOV"/> (36°), the owner's "longer than precise aim" (a test holds it); 0.88 until the owner's
        /// "too close" of 2026-10-28. Measured with the other three on the opening frames at 1920x1080 (<c>logtilt</c>): on
        /// Zigzag the muzzle goes from 57 % down the frame to the <see cref="AimTilt.GUN_KEEP_FRACTION"/> line at 88 %, on
        /// Hopper from 57 % and on Totem from 51 %, so the gun shows the barrel's front and the loaded ball rather than the
        /// lower half of the frame.
        /// </summary>
        public const float FOV_SCALE = 0.92f;

        /// <summary>The aim point's chase in space, <c>1/ω</c> of a critically damped follow per axis.</summary>
        public const float AIM_FOLLOW_SECONDS = 0.15f;

        /// <summary>The depth along the aim the point is eased at, a first-order time constant.</summary>
        public const float DEPTH_FOLLOW_SECONDS = 0.3f;

        //Below these the chase has arrived, so the full overview comes back bit for bit
        private const float SETTLED_AMOUNT = 1e-4f;
        private const float SETTLED_RATE = 1e-3f;

        /// <summary>0 on the full overview, 1 on the focused pose.</summary>
        public float Amount { get; private set; }

        /// <summary>How fast <see cref="Amount"/> is moving, a second — the chase's second state.</summary>
        public float Rate { get; private set; }

        //How long the overview has had the lens alone, whether the next hold is a return's (shorter) or the opening's, and
        //whether this level's opening hold has run out: a lean or a tour before it does not cut the opening short
        private float _held;
        private bool _returning;
        private bool _opened;

        //The followed aim point: its eased depth, the point and its velocity
        private float _depth;
        private Vector3 _aimPoint, _aimRate;
        private bool _aimSeeded;

        /// <summary>
        /// A level's load: the full overview again and the opening's hold, and the aim point taken afresh on its next
        /// follow — a placed gun is a placed camera (#366).
        /// </summary>
        public void Reset()
        {
            Amount = 0f;
            Rate = 0f;
            _held = 0f;
            _returning = false;
            _opened = false;
            _aimSeeded = false;
        }

        /// <summary>
        /// Moves <see cref="Amount"/> on by <paramref name="elapsedSeconds"/> of real time and returns it.
        /// <paramref name="overviewHasTheLens"/> is false while precise aim or any takeover has any weight in the pose;
        /// <paramref name="heldByTheOpening"/> says the takeover is the chapter's opening tour, whose end is the level's
        /// opening for the player, so the hold after it is the opening's and not a return's. A lean before the level has
        /// focused once keeps the opening's hold too, so the cluster's start swing is seen whole however early RMB is tried.
        /// </summary>
        public float Step(bool overviewHasTheLens, bool heldByTheOpening, float elapsedSeconds)
        {
            float dt = MathF.Max(0f, elapsedSeconds);

            if (!overviewHasTheLens)
            {
                _held = 0f;
                _returning = _opened && !heldByTheOpening;
                Chase(0f, FALL_SECONDS, dt);
                return Amount;
            }

            //The hold's end is split out of the frame it falls in, so a long frame rises only for the part of it past the
            //hold and every frame rate starts the focus at the same instant
            float hold = _returning ? HOLD_RETURN_SECONDS : HOLD_OPENING_SECONDS;
            float before = MathF.Min(dt, MathF.Max(0f, hold - _held));
            _held += dt;
            if (_held >= hold) _opened = true;

            if (before > 0f) Chase(0f, FALL_SECONDS, before);
            if (dt > before) Chase(1f, RISE_SECONDS, dt - before);

            return Amount;
        }

        //One closed-form step of the critically damped chase towards a held target: AimTilt.Step's,
        //x(t) = (x0 + (v0 + ω x0) t) e^(-ωt) about the target, and its derivative
        private void Chase(float target, float tau, float dt)
        {
            float omega = 1f / tau;
            float x0 = Amount - target;
            float c = Rate + omega * x0;
            float decay = MathF.Exp(-omega * dt);

            float x = (x0 + c * dt) * decay;
            Rate = (Rate - omega * c * dt) * decay;
            Amount = Math.Clamp(target + x, 0f, 1f);

            if (MathF.Abs(Amount - target) < SETTLED_AMOUNT && MathF.Abs(Rate) < SETTLED_RATE)
            {
                Amount = target;
                Rate = 0f;
            }
        }

        /// <summary>
        /// The point the focus looks at this frame: on the aim from the muzzle, at a depth eased towards
        /// <paramref name="targetDepth"/> (the beam's first contact, or the cluster's centre when the beam reaches nothing),
        /// and chased in space so a flick of the aim is taken up smoothly.
        /// </summary>
        public Vector3 FollowAim(Vector3 muzzle, Vector3 aim, float targetDepth, float elapsedSeconds)
        {
            float dt = MathF.Max(0f, elapsedSeconds);

            //Held to precise aim's own convergence range (an aim swung far off the cluster projects its centre to a depth
            //at or behind the muzzle), and a depth that is not a number leaves the last one standing: the chase feeds on
            //itself, so one NaN would hold for the rest of the level
            targetDepth = float.IsFinite(targetDepth)
                ? Math.Clamp(targetDepth, PreciseAim.CONVERGE_MIN, PreciseAim.CONVERGE_MAX)
                : (_aimSeeded ? _depth : PreciseAim.CONVERGE_MAX);

            if (!_aimSeeded)
            {
                _depth = targetDepth;
                _aimPoint = muzzle + aim * _depth;
                _aimRate = Vector3.Zero;
                _aimSeeded = true;
                return _aimPoint;
            }

            _depth += (targetDepth - _depth) * (1f - MathF.Exp(-dt / DEPTH_FOLLOW_SECONDS));
            Vector3 goal = muzzle + aim * _depth;

            float omega = 1f / AIM_FOLLOW_SECONDS;
            Vector3 x0 = _aimPoint - goal;
            Vector3 c = _aimRate + omega * x0;
            float decay = MathF.Exp(-omega * dt);

            _aimPoint = goal + (x0 + c * dt) * decay;
            _aimRate = (_aimRate - omega * c * dt) * decay;

            return _aimPoint;
        }

        /// <summary>
        /// The focused pose for the full overview <paramref name="lens"/> → <paramref name="lookAt"/> at
        /// <paramref name="fov"/>: the lens raised by <see cref="RISE"/> and moved in by <see cref="ADVANCE"/> along its
        /// horizontal bearing, the look-at taken <see cref="PULL"/> of the way to <paramref name="aimPoint"/>, the field of
        /// view narrowed to <see cref="FOV_SCALE"/> — and then turned down, if need be, so the muzzle stays
        /// <see cref="AimTilt.GUN_KEEP_FRACTION"/> of the frame over its bottom edge (#720's condition: some of the gun stays
        /// in view), and up, as far as that leaves room, so the cluster's top row (<paramref name="topRow"/>, the corners the
        /// tilt keeps) stays <see cref="AimTilt.TOP_KEEP_FRACTION"/> under the top edge. The gun keep wins: a low aim on a
        /// deep field may still lose the top row, which is where the focus is not looking.
        /// </summary>
        public static AimPose Focused(Vector3 lens, Vector3 lookAt, float fov, Vector3 aimPoint, Vector3 muzzle,
            ReadOnlySpan<Vector3> topRow)
        {
            Vector3 forward = lookAt - lens;
            Vector3 horizontal = new Vector3(forward.X, 0f, forward.Z);
            if (horizontal.LengthSquared() > 1e-8f) horizontal.Normalize();

            Vector3 focusedLens = lens + horizontal * ADVANCE + Vector3.Up * RISE;
            Vector3 focusedLookAt = Vector3.Lerp(lookAt, aimPoint, PULL);
            float focusedFov = fov * FOV_SCALE;

            float room = AimTilt.GunKeepLimit(focusedLens, focusedLookAt, muzzle, focusedFov);
            if (room < 0f)
            {
                focusedLookAt = AimTilt.Tilted(focusedLens, focusedLookAt, room);
                room = 0f;
            }

            float lost = 0f;
            for (int i = 0; i < topRow.Length; i++)
                lost = MathF.Max(lost, -AimTilt.TopKeepLimit(focusedLens, focusedLookAt, topRow[i], focusedFov));

            if (lost > 0f && room > 0f) focusedLookAt = AimTilt.Tilted(focusedLens, focusedLookAt, MathF.Min(lost, room));

            return new AimPose(focusedLens, focusedLookAt, focusedFov);
        }
    }
}
