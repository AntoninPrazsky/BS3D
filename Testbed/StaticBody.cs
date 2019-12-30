using BepuPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;
using Prazsky.Tools;

namespace Testbed
{
	public class StaticBody : Object3D
	{
		public StaticBody(Model model, StaticReference staticReference)
		{
			Model = model;
			Transformations = new Matrix[Model.Bones.Count];
			Model.CopyAbsoluteBoneTransformsTo(Transformations);

			World = Matrix.CreateFromQuaternion(
				new Quaternion(staticReference.Pose.Orientation.X, staticReference.Pose.Orientation.Y, staticReference.Pose.Orientation.Z, staticReference.Pose.Orientation.W))
				* Matrix.CreateTranslation(staticReference.Pose.Position.X, staticReference.Pose.Position.Y, staticReference.Pose.Position.Z);

			//Pozici ani BoundingSphere ani nepotřebuju, ale když už to má Object3D...
			Position = new Vector3(staticReference.Pose.Position.X, staticReference.Pose.Position.Y, staticReference.Pose.Position.Z);
			BoundingSphere = Geometry.GetBoundingSphere(Model);
			BoundingSphere = new BoundingSphere(Position, BoundingSphere.Radius);
		}
	}
}