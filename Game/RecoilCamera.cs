using Microsoft.Xna.Framework;
using Prazsky.Core.Camera;
using System;

namespace BS3D
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

        public float AspectRatio { get; set; } = 16f / 9f;

        public float NearPlane { get; set; } = 0.05f;

        public float FarPlane { get; set; } = 500f;

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

            //Roll last, about the view axis after pitch and yaw, so it stays a roll of the finished frame
            shakenUp = Vector3.TransformNormal(shakenUp, Matrix.CreateFromAxisAngle(shakenForward, rotation.Z));

            Position = BasePosition + right * lens.X + up * lens.Y - forward * Shake.RecoilBack;
            Target = Position + shakenForward * distance;
            Up = shakenUp;

            View = Matrix.CreateLookAt(Position, Target, Up);

            //The punch widens the frame briefly. Clamped well off π so a stacked-up kick cannot produce a
            //degenerate projection.
            float fov = MathHelper.Clamp(FieldOfView * (1f + Shake.FovPunch), 0.01f, MathHelper.Pi * 0.95f);
            Projection = Matrix.CreatePerspectiveFieldOfView(fov, AspectRatio, NearPlane, FarPlane);
        }
    }
}
