using BepuPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;

namespace Testbed
{
    public class KinematicBody : Object3D //TODO: I don't think that this should be Object3D
    {
        public BodyReference BodyReference { get; private set; }
        public BodyHandle BodyHandle { get; private set; }

        /// <param name="model">Model drawn through <see cref="Object3D.Draw(Prazsky.Core.Camera.ICamera)"/>;
        /// null for bodies drawn some other way (e.g. the ceiling, a procedurally generated mesh).</param>
        public KinematicBody(Model model, BodyReference bodyReference, BodyHandle bodyHandle, Vector3? modelScale = null)
        {
            Model = model;
            BodyReference = bodyReference;
            BodyHandle = bodyHandle;

            if (model != null)
            {
                Transformations = new Matrix[model.Bones.Count];
                model.CopyAbsoluteBoneTransformsTo(Transformations);
            }

            World = Matrix.CreateScale(modelScale ?? Vector3.One)
                * Matrix.CreateFromQuaternion(
                new Quaternion(bodyReference.Pose.Orientation.X, bodyReference.Pose.Orientation.Y, bodyReference.Pose.Orientation.Z, bodyReference.Pose.Orientation.W))
                * Matrix.CreateTranslation(bodyReference.Pose.Position.X, bodyReference.Pose.Position.Y, bodyReference.Pose.Position.Z);
        }
    }
}