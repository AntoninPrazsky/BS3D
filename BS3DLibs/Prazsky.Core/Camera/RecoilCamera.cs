using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace Prazsky.Core.Camera
{
    /// <summary>
    /// The game camera: a pose the game sets every frame (<see cref="BasePosition"/> /
    /// <see cref="BaseTarget"/>) plus whatever <see cref="Shake"/> is adding on top of it this instant.
    /// <para>
    /// It is its own <see cref="ICamera"/> rather than a <c>BasicCamera3D</c> with jittered properties for
    /// one reason: <b>roll</b>. <c>BasicCamera3D</c> rebuilds its view with a fixed world up, which is
    /// exactly right for a camera that must keep a level horizon and exactly wrong for one that is
    /// supposed to be knocked sideways by a gun going off. Here the up vector is rolled about the view
    /// axis, so the whole frame tips.
    /// </para>
    /// <para>
    /// The base pose is kept separate from the shaken one on purpose: the shake is a pure offset, so it
    /// can never accumulate into the pose. At rest the camera sits precisely where the game asked.
    /// </para>
    /// </summary>
    public sealed class RecoilCamera : ICamera
    {
        /// <summary>Where the game wants the lens, before any kick.</summary>
        public Vector3 BasePosition { get; set; } = new(0f, 0f, 10f);

        /// <summary>What the game wants the lens pointed at, before any kick.</summary>
        public Vector3 BaseTarget { get; set; } = Vector3.Zero;

        /// <summary>Vertical field of view in radians, before the recoil's punch.</summary>
        public float FieldOfView { get; set; } = MathHelper.PiOver4;

        /// <summary>
        /// A deliberate roll of the base pose about the view axis, in radians — a dutch tilt, as opposed to
        /// the involuntary one <see cref="Shake"/> adds. Zero leaves the horizon level, which is what every
        /// pose the game sets wants; the drop cinematic is the one caller that tips the frame on purpose.
        /// <para>
        /// It rides on the same axis-angle the shake's roll does and is added to it, so a gun fired mid-tilt
        /// still reads as a kick on top of the tilt rather than replacing it.
        /// </para>
        /// </summary>
        public float BaseRoll { get; set; }

        public float AspectRatio { get; set; } = 16f / 9f;

        /// <summary>
        /// The near plane of a lens close to what it shows: every play view, the menu's flight, the result page and the
        /// drop cinematic. A lens far from both its target and <see cref="DepthSubject"/> draws with a farther one
        /// (<see cref="EffectiveNearPlane"/>, <see cref="NearPlaneAt"/>).
        /// </summary>
        public float NearPlane { get; set; } = 0.05f;

        public float FarPlane { get; set; } = BasicCamera3D.DEFAULT_FAR_PLANE_DISTANCE;

        /// <summary>
        /// While the lens stands within this of its target or of <see cref="DepthSubject"/> it keeps <see cref="NearPlane"/>;
        /// past it, from both, the near plane grows with the square of the nearer distance (#1193). A play view stands at most
        /// 32 from the nearer of the two (every shipped level at every window shape, <c>NearPlaneTests</c>) and the menu's
        /// widest frame of the island 38 to 45 from it, so none of them moves.
        /// </summary>
        public const float NEAR_HOLD_DISTANCE = 60f;

        /// <summary>
        /// The farthest the near plane goes, ten times <see cref="NearPlane"/> (#1193). It clips whatever stands nearer the lens
        /// than its corner, which at the widest window and the widest lens a chapter intro opens is about twice this; a lens far
        /// from both its target and the arena is an establishing shot, built with far more air than that round it (the meadow's
        /// valley flies 18 or more over the ground, <c>NearPlaneTests</c>).
        /// </summary>
        public const float NEAR_PLANE_CEILING = 0.5f;

        /// <summary>
        /// The point besides the target whose distance from the lens holds the near plane down (#1193): the middle of the
        /// arena, where the island, its drain and the cluster stand (the game's world is centred on it). A lens near it may
        /// be looking past it (a shot from the rim out over the land) and still has the island beside it.
        /// </summary>
        public Vector3 DepthSubject { get; set; } = new(0f, ArenaIsland.TOP_Y, 0f);

        /// <summary>The near plane <see cref="Projection"/> was last built with (<see cref="NearPlaneAt"/>).</summary>
        public float EffectiveNearPlane { get; private set; }

        /// <summary>The kick. Call <see cref="CameraShake.Kick"/> on it when something violent happens.</summary>
        public CameraShake Shake { get; } = new();

        public Vector3 Position { get; private set; }

        public Vector3 Target { get; private set; }

        public Vector3 Up { get; private set; } = Vector3.Up;

        public Matrix View { get; private set; }

        public Matrix Projection { get; private set; }

        public void Update(float elapsedSeconds)
        {
            Shake.Update(elapsedSeconds);
            Recalculate();
        }

        /// <summary>
        /// Rebuilds the view and projection from the base pose and the current kick. Cheap enough to run
        /// unconditionally every frame (three axis-angle matrices), and running it unconditionally is what
        /// keeps the camera correct on the frame the shake reaches zero.
        /// </summary>
        public void Recalculate()
        {
            Vector3 toTarget = BaseTarget - BasePosition;
            float distance = toTarget.Length();

            //A degenerate pose (lens sitting on its own target) has no view direction to speak of; fall
            //back to looking forward rather than producing a NaN view matrix that blanks the frame.
            Vector3 forward = distance > 1e-4f ? toTarget / distance : Vector3.Forward;
            if (distance <= 1e-4f) distance = 1f;

            //Right-handed: with forward down -Z and up +Y this gives +X, and the up recovered from it is
            //+Y again — so the basis matches what CreateLookAt expects without any sign juggling.
            Vector3 right = Vector3.Cross(forward, Vector3.Up);
            right = right.LengthSquared() > 1e-6f ? Vector3.Normalize(right) : Vector3.Right;
            Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

            Vector3 rotation = Shake.RotationOffset;
            Vector2 lens = Shake.LensOffset;

            //Pitch about the lens's own right axis, yaw about its own up: the jolt is in camera space, so
            //it looks the same whichever way the camera happens to be facing.
            Matrix jitter = Matrix.CreateFromAxisAngle(right, rotation.X) * Matrix.CreateFromAxisAngle(up, rotation.Y);

            Vector3 shakenForward = Vector3.Normalize(Vector3.TransformNormal(forward, jitter));
            Vector3 shakenUp = Vector3.TransformNormal(up, jitter);

            //Roll last, about the view axis after pitch and yaw, so it stays a roll of the finished frame.
            //The deliberate tilt and the kick's own roll are one rotation: they are the same degree of
            //freedom, and rolling twice about an axis that moved in between is not a roll of anything.
            shakenUp = Vector3.TransformNormal(shakenUp, Matrix.CreateFromAxisAngle(shakenForward, rotation.Z + BaseRoll));

            Position = BasePosition + right * lens.X + up * lens.Y - forward * Shake.RecoilBack;
            Target = Position + shakenForward * distance;
            Up = shakenUp;

            View = Matrix.CreateLookAt(Position, Target, Up);

            //The punch widens the frame briefly. Clamped well off π so a stacked-up kick cannot produce a
            //degenerate projection.
            float fov = MathHelper.Clamp(FieldOfView * (1f + Shake.FovPunch), 0.01f, MathHelper.Pi * 0.95f);
            EffectiveNearPlane = NearPlaneAt(NearPlane, distance, Vector3.Distance(BasePosition, DepthSubject));
            Projection = Matrix.CreatePerspectiveFieldOfView(fov, AspectRatio, EffectiveNearPlane, FarPlane);
        }

        /// <summary>
        /// The near plane for a lens <paramref name="toTarget"/> from what it looks at and <paramref name="toSubject"/> from
        /// <see cref="DepthSubject"/> (#1193): <paramref name="near"/> out to <see cref="NEAR_HOLD_DISTANCE"/> from the nearer
        /// of the two, then growing with its square up to <see cref="NEAR_PLANE_CEILING"/>.
        /// <para>
        /// <b>Why.</b> A 24-bit depth buffer's step at distance z is about z² / (near × 2^24): with the near plane at 0.05,
        /// 0.0024 at the far side of the drain from the play view, 45 off, and 0.05 from the meadow's valley shot, some 200
        /// off. The drain is built of surfaces closer than that: the glass and the dark pit sheath behind it part from nothing
        /// at the mouth to <c>ArenaIsland.PIT_SHEATH_CLEARANCE</c> at the hole, and the gold collar crosses both a hundredth or
        /// so off them. Seen from the valley the two landed on one depth value down the top of the glass and the sheath won
        /// it pixel by pixel: dark streaks down the inner sides and teeth along the rim, different in every frame of the
        /// flight (the owner's QA, #1193). Growing with the square holds the step at the nearer distance where it is at
        /// <see cref="NEAR_HOLD_DISTANCE"/>, until the ceiling; from the valley the band of glass the buffer cannot part from
        /// the sheath then spans half as much of the frame as it does from the play view (<c>NearPlaneTests</c>).
        /// </para>
        /// <para>
        /// <b>Why the nearer of the two.</b> The near plane clips whatever stands nearer the lens than it. A lens that looks
        /// at something close is a close shot, and close shots fly low among things (the orbit round the hay bales under a
        /// unit over the grass, the walk along the fence, a beetle on the sand); a lens near the arena has the island beside
        /// it. Both keep <paramref name="near"/>. Only a lens far from both, an establishing shot built with air all round it,
        /// draws with a farther one. Nothing else reads the plane: no pass of the game samples the depth buffer, and the passes
        /// that work out a depth of their own take the planes from this projection (the clouds' far layer, the motion blur's
        /// background).
        /// </para>
        /// </summary>
        public static float NearPlaneAt(float near, float toTarget, float toSubject)
        {
            float nearer = MathF.Min(toTarget, toSubject);
            if (!(nearer > NEAR_HOLD_DISTANCE)) return near;

            float share = nearer / NEAR_HOLD_DISTANCE;
            return MathF.Max(near, MathF.Min(near * share * share, NEAR_PLANE_CEILING));
        }
    }
}
