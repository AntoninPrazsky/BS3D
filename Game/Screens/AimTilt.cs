using Microsoft.Xna.Framework;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The overview lens tilts with the aim</b> (#720): while precise aim is not held, the look-at leans up as the
    /// barrel is raised and down as it is lowered, so more of what the gun points at is in the frame. The owner's ask
    /// (2026-10-02): "the camera should move up and down with the aim ... the whole cannon is in view, but that is not
    /// always necessary" — and his condition on it: the lower part of the gun may leave the frame, but some of the
    /// barrel or the body must stay in it.
    /// <para>
    /// <b>A tilt and not a lift.</b> Only the look-at turns, about the lens, so nothing hides behind anything that did
    /// not before and the fit's stand-off, the walk's follow and the bearing's trail (#322, #366) are untouched. Its
    /// zero is the aim the level opens on (<see cref="Prazsky.BS3D.GameObjects.Cannon.RestElevation"/>, or the tall
    /// level's clamp if that is lower), so the opening frame — and the chapter intro's arrival, which is that frame
    /// verbatim — is the solved fit, bit for bit; the tilt is what the hand has done since.
    /// </para>
    /// <para>
    /// <b>Bounded at both ends by what the frame must keep.</b> Up, by the gun (<see cref="GunKeepLimit"/>, the owner's
    /// condition); down, by the cluster's top row (<see cref="TopKeepLimit"/>), so a low aim never pushes the top of
    /// what <see cref="Prazsky.BS3D.GameCameraFit"/> framed — on a tall level, the window #842's held band lives in —
    /// off the top of the frame. What the up-tilt may cost is the bottom: the gun's lower half, its wheels and the
    /// drain's near rim, which stand where the gun stands.
    /// </para>
    /// <para>
    /// <b>The overview pose only.</b> The caller tilts the overview look-at before
    /// <see cref="Prazsky.BS3D.PreciseAim.BlendedPose"/> lerps it, so precise aim's lean interpolates the tilt out as it
    /// comes in, and the chase below runs every frame whether the lean is in or out — a release lands on a tilt that
    /// has already followed the aim, and no frame of a press or a release moves anything by itself.
    /// </para>
    /// <para>
    /// Pure pieces and one piece of state: <see cref="TargetFor"/> (how far to tilt for an aim), the two keep limits and
    /// <see cref="Bounded"/> (how far the frame allows), <see cref="Tilted"/> (the look-at turned), and
    /// <see cref="Step"/>, a critically damped chase on real time. No allocation, a handful of trigonometric calls a
    /// frame. Game only: the Testbed's camera is its own and the shared fit has no reason to know (the #366 route).
    /// </para>
    /// </summary>
    internal sealed class AimTilt
    {
        /// <summary>
        /// How much the lens pitches per unit of barrel elevation near the opening aim. Under one by a wide margin: the
        /// barrel sweeps 86° from its floor to its ceiling and the frame is 43° tall, so a lens that followed it one to
        /// one would lose the field. At 0.3 a raise of ten degrees leans the frame three, which reads as the view
        /// following the gun rather than as the gun dragging the view.
        /// </summary>
        public const float GAIN = 0.3f;

        /// <summary>
        /// The most the frame leans <b>up</b>, in radians (9°, about a fifth of the frame's height): approached softly
        /// (<see cref="TargetFor"/>), never passed. Up is the side that matters — the cluster hangs over the gun, and a
        /// high shot's landing sits near the top edge — and it is the side that costs the gun's lower half, which the
        /// owner allowed. <see cref="GunKeepLimit"/> bounds it again by geometry.
        /// </summary>
        public static readonly float UP_MAX = MathHelper.ToRadians(9f);

        /// <summary>
        /// The most the frame leans <b>down</b>, in radians (5°), before <see cref="TopKeepLimit"/> has its say — and at a
        /// level's opening it does: the fit stands its framed top at its own margin, which leaves about half a degree,
        /// and the room grows only as the glass descends under that top. Smaller than <see cref="UP_MAX"/>: a low aim
        /// passes under the cluster towards ground the frame already holds, so leaning down shows little that was
        /// hidden and is mostly the lens answering the hand.
        /// </summary>
        public static readonly float DOWN_MAX = MathHelper.ToRadians(5f);

        /// <summary>
        /// The chase's time constant in seconds, <c>1/ω</c> of a critically damped follow: about 90 % of a sudden
        /// change is taken up in 0.5 s and 95 % in 0.6 s, and a steady sweep is trailed by twice this (0.25 s) — the
        /// bearing's own trail is 0.22 s (#366), so the two axes of the overview feel like one camera. Second order
        /// rather than the bearing's first: a flick of the mouse steps the target, and a first-order chase would start
        /// moving at full speed on that frame; this one accelerates into it, which is what keeps a camera fastened to a
        /// mouse axis from being the kind that makes people sick.
        /// </summary>
        public const float FOLLOW_SECONDS = 0.125f;

        /// <summary>
        /// How far above the frame's bottom edge the muzzle must stay, as a share of the frame's height, before the
        /// tilt is allowed further up (<see cref="GunKeepLimit"/>): the owner's condition that some of the barrel stays
        /// in view, with the queue's front ball (the muzzle point sits in it) a bonus. The guard for a fit that stands
        /// the gun low in the frame; how much it leaves on the shipped fits is in <c>docs/game-session/field-and-camera.md</c>.
        /// </summary>
        public const float GUN_KEEP_FRACTION = 0.12f;

        /// <summary>
        /// How far under the frame's top edge the cluster's top row must stay, as a share of the frame's height, before
        /// the tilt is allowed further down (<see cref="TopKeepLimit"/>): about a ball's radius at the field's near edge,
        /// so a top ball stays whole even where the row is measured at its cells' centres (the framed window) rather than
        /// at their tops (the glass's underside).
        /// </summary>
        public const float TOP_KEEP_FRACTION = 0.03f;

        //Short of the vertical either way, so the tilted look-at always keeps a horizontal forward and the camera's
        //world-up view never degenerates; nothing the overview frames comes within twenty degrees of it
        private static readonly float MAX_PITCH = MathHelper.ToRadians(80f);

        //Below these the chase has arrived: it lands on its target exactly, so a tilt of zero is a zero and the
        //overview pose comes back bit for bit
        private const float SETTLED_ANGLE = 1e-5f;
        private const float SETTLED_RATE = 1e-4f;

        /// <summary>The tilt this frame, in radians: positive leans the frame up.</summary>
        public float Angle { get; private set; }

        /// <summary>How fast <see cref="Angle"/> is moving, in radians a second — the chase's second state.</summary>
        public float Rate { get; private set; }

        private bool _seeded;

        /// <summary>
        /// Starts the next <see cref="Step"/> on its target rather than easing in from wherever the last level left it
        /// — a placed gun is a placed camera, the rule the bearing's trail follows (#366).
        /// </summary>
        public void Reseed() => _seeded = false;

        /// <summary>
        /// Moves <see cref="Angle"/> towards <paramref name="target"/> over <paramref name="elapsedSeconds"/> of real
        /// time and returns it. <b>Exact</b> for a target held over the step — the closed form of the critically damped
        /// follow, not an integration of it — so ten frames at 240 a second land where one at 24 does, and a long frame
        /// (a resumed pause) cannot make it ring or diverge. From rest it never passes its target.
        /// </summary>
        public float Step(float target, float elapsedSeconds)
        {
            if (!_seeded)
            {
                Angle = target;
                Rate = 0f;
                _seeded = true;
                return Angle;
            }

            float dt = MathF.Max(0f, elapsedSeconds);
            float omega = 1f / FOLLOW_SECONDS;

            //x(t) = (x0 + (v0 + ω x0) t) e^(-ωt) about the target, and its derivative
            float x0 = Angle - target;
            float c = Rate + omega * x0;
            float decay = MathF.Exp(-omega * dt);

            float x = (x0 + c * dt) * decay;
            Rate = (Rate - omega * c * dt) * decay;
            Angle = target + x;

            if (MathF.Abs(x) < SETTLED_ANGLE && MathF.Abs(Rate) < SETTLED_RATE)
            {
                Angle = target;
                Rate = 0f;
            }

            return Angle;
        }

        /// <summary>
        /// How far to tilt, in radians, for a barrel <paramref name="elevationAboveZero"/> radians above the aim the
        /// level opened on (negative below it). <see cref="GAIN"/> near zero, bending smoothly (a <c>tanh</c>, so there
        /// is no corner for the chase to feel) onto <see cref="UP_MAX"/> above and <see cref="DOWN_MAX"/> below. Zero is
        /// zero exactly, so an untouched aim leaves the solved frame as it is.
        /// </summary>
        public static float TargetFor(float elevationAboveZero)
        {
            //A float's worth of noise round the zero (the clamped tall-level aim is rest + (limit - rest)) is no aim
            if (MathF.Abs(elevationAboveZero) < 1e-6f) return 0f;

            float limit = elevationAboveZero > 0f ? UP_MAX : DOWN_MAX;
            return limit * MathF.Tanh(GAIN * elevationAboveZero / limit);
        }

        /// <summary>
        /// <paramref name="wanted"/> held inside the room the frame allows: no more than <paramref name="roomUp"/> up
        /// and <paramref name="roomDown"/> down, both in radians from the untilted pose. A room that is already gone
        /// (negative: the keep point is past its line at a tilt of zero) stops that side at zero and never pushes the
        /// lens the other way — the fit framed the whole subject at zero, and a tilt is never the fit's repair.
        /// </summary>
        public static float Bounded(float wanted, float roomUp, float roomDown) =>
            Math.Clamp(wanted, -MathF.Max(0f, roomDown), MathF.Max(0f, roomUp));

        /// <summary>
        /// The most the lens may tilt <b>up</b> from the pose <paramref name="lens"/> → <paramref name="lookAt"/> and
        /// still show <paramref name="keepPoint"/> (the muzzle) <see cref="GUN_KEEP_FRACTION"/> of the frame's height
        /// above its bottom edge, in radians; negative when the point is already under that line.
        /// <para>
        /// Exact for a point anywhere across the frame, not only on its centre line: a lens pitched by φ draws a point
        /// whose elevation in the view's vertical plane is α at <c>tan(α − φ) / tan(fov/2)</c> of the half-height from
        /// the centre, whatever its sideways offset — so the edge is a plain angle and the limit a subtraction.
        /// </para>
        /// </summary>
        public static float GunKeepLimit(Vector3 lens, Vector3 lookAt, Vector3 keepPoint, float verticalFov)
        {
            if (!ViewElevations(lens, lookAt, keepPoint, out float pitch, out float keepElevation)) return 0f;

            return keepElevation + EdgeAngle(GUN_KEEP_FRACTION, verticalFov) - pitch;
        }

        /// <summary>
        /// The most the lens may tilt <b>down</b> from the pose <paramref name="lens"/> → <paramref name="lookAt"/> and
        /// still show <paramref name="keepPoint"/> (a top corner of the cluster's top row) <see cref="TOP_KEEP_FRACTION"/>
        /// of the frame's height under its top edge, in radians; negative when the point is already over that line.
        /// The mirror of <see cref="GunKeepLimit"/>, on the same exact projection.
        /// </summary>
        public static float TopKeepLimit(Vector3 lens, Vector3 lookAt, Vector3 keepPoint, float verticalFov)
        {
            if (!ViewElevations(lens, lookAt, keepPoint, out float pitch, out float keepElevation)) return 0f;

            return pitch + EdgeAngle(TOP_KEEP_FRACTION, verticalFov) - keepElevation;
        }

        /// <summary>
        /// <paramref name="lookAt"/> turned about <paramref name="lens"/> by <paramref name="angle"/> radians of pitch,
        /// at the same distance and on the same bearing. A tilt of exactly zero returns the look-at untouched.
        /// </summary>
        public static Vector3 Tilted(Vector3 lens, Vector3 lookAt, float angle)
        {
            if (angle == 0f) return lookAt;

            Vector3 forward = lookAt - lens;
            float horizontal = MathF.Sqrt(forward.X * forward.X + forward.Z * forward.Z);
            if (horizontal < 1e-4f) return lookAt;

            float length = forward.Length();
            float pitch = Math.Clamp(MathF.Atan2(forward.Y, horizontal) + angle, -MAX_PITCH, MAX_PITCH);
            float across = MathF.Cos(pitch) * length / horizontal;

            return lens + new Vector3(forward.X * across, MathF.Sin(pitch) * length, forward.Z * across);
        }

        //The view's pitch and a point's elevation in the view's vertical plane (its height over its depth along the
        //horizontal forward), or false when the pose has no horizontal forward or the point is not in front of the lens
        private static bool ViewElevations(Vector3 lens, Vector3 lookAt, Vector3 point, out float pitch, out float elevation)
        {
            pitch = elevation = 0f;

            Vector3 forward = lookAt - lens;
            float horizontal = MathF.Sqrt(forward.X * forward.X + forward.Z * forward.Z);
            if (horizontal < 1e-4f) return false;

            Vector3 offset = point - lens;
            float depth = (offset.X * forward.X + offset.Z * forward.Z) / horizontal;
            if (depth < 1e-4f) return false;

            pitch = MathF.Atan2(forward.Y, horizontal);
            elevation = MathF.Atan2(offset.Y, depth);
            return true;
        }

        //The angle off the view axis at which a point sits the given share of the frame's height inside an edge
        private static float EdgeAngle(float fraction, float verticalFov) =>
            MathF.Atan((1f - 2f * fraction) * MathF.Tan(verticalFov * 0.5f));
    }
}
