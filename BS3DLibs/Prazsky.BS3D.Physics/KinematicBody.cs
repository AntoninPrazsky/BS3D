using BepuPhysics;
using Microsoft.Xna.Framework;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// A Bepu kinematic body paired with the world matrix captured from its pose. Deliberately <b>not</b> an
    /// <see cref="Prazsky.Core.Object3D"/>: the only kinematic body is the ceiling, which carries no model of
    /// its own and is drawn procedurally through its own <see cref="Prazsky.Core.Render.InstancedModelRenderer"/>
    /// (see <c>RecreateCeilingRenderer</c>) using <see cref="World"/> — so the whole <c>Object3D</c> drawing
    /// apparatus (model, transformations, <c>Draw</c>, bounding volumes) was dead weight. What is actually used
    /// is the body handle/reference and the pose's world matrix, which is all this holds.
    /// </summary>
    public class KinematicBody
    {
        public BodyReference BodyReference { get; }
        public BodyHandle BodyHandle { get; }

        /// <summary>
        /// World matrix built from the body's pose (rotation and translation). Rebuilt by
        /// <see cref="RefreshWorld"/> whenever the pose moves — the body is kinematic and may be driven by hand
        /// (the descending ceiling), so what is drawn has to follow what the simulation holds, or the collidable
        /// and the glass plate drift apart.
        /// </summary>
        public Matrix World { get; private set; }

        public KinematicBody(BodyReference bodyReference, BodyHandle bodyHandle)
        {
            BodyReference = bodyReference;
            BodyHandle = bodyHandle;

            World = BuildWorld(bodyReference.Pose);
        }

        /// <summary>
        /// Rebuilds <see cref="World"/> from the body's current pose. Called each frame the body has moved, after
        /// the integrator has placed it — the pose is the source of truth, this keeps the drawn matrix in step.
        /// </summary>
        public void RefreshWorld() => World = BuildWorld(BodyReference.Pose);

        private static Matrix BuildWorld(BepuPhysics.RigidPose pose) =>
            Matrix.CreateFromQuaternion(
                new Quaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W))
                * Matrix.CreateTranslation(pose.Position.X, pose.Position.Y, pose.Position.Z);
    }
}
