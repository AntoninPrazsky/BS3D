using BepuPhysics;
using Microsoft.Xna.Framework;

namespace Testbed
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

        /// <summary>World matrix built from the body's pose (rotation and translation) at construction.</summary>
        public Matrix World { get; }

        public KinematicBody(BodyReference bodyReference, BodyHandle bodyHandle)
        {
            BodyReference = bodyReference;
            BodyHandle = bodyHandle;

            World = Matrix.CreateFromQuaternion(
                new Quaternion(bodyReference.Pose.Orientation.X, bodyReference.Pose.Orientation.Y, bodyReference.Pose.Orientation.Z, bodyReference.Pose.Orientation.W))
                * Matrix.CreateTranslation(bodyReference.Pose.Position.X, bodyReference.Pose.Position.Y, bodyReference.Pose.Position.Z);
        }
    }
}
