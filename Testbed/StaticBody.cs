using BepuPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;

namespace Testbed
{
    public class StaticBody : Object3D //TODO: I don't thing this should be Object3D
    {
        public StaticReference StaticReference { get; private set; }

        public StaticBody(Model model, StaticReference staticReference)
        {
            Model = model;
            StaticReference = staticReference;
            Transformations = new Matrix[Model.Bones.Count];
            Model.CopyAbsoluteBoneTransformsTo(Transformations);

            World = Matrix.CreateFromQuaternion(
                new Quaternion(staticReference.Pose.Orientation.X, staticReference.Pose.Orientation.Y, staticReference.Pose.Orientation.Z, staticReference.Pose.Orientation.W))
                * Matrix.CreateTranslation(staticReference.Pose.Position.X, staticReference.Pose.Position.Y, staticReference.Pose.Position.Z);

            //Position = new Vector3(staticReference.Pose.Position.X, staticReference.Pose.Position.Y, staticReference.Pose.Position.Z);
            //BoundingSphere = Geometry.GetBoundingSphere(Model);
            //BoundingSphere = new BoundingSphere(Position, BoundingSphere.Radius);
        }
    }
}