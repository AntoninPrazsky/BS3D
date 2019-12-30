using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;

namespace Prazsky.BS3D.GameStructure
{
	public class StaticBall : Object3D
	{
		public eBallType Type { get; }

		public StaticBall(Vector3 position, eBallType type)
		{
			Type = type;
			Position = position;
			BasicEffectParams = BasicEffectParamsProvider.GetEffectByType(type);
		}

		public void RecomputeTransformations(Model model)
		{
			Transformations = new Matrix[model.Bones.Count];
			model.CopyAbsoluteBoneTransformsTo(Transformations);
		}

		public void RecomputeWorldMatrix()
		{
			World = Matrix.CreateTranslation(Position);
		}

		public System.Numerics.Vector3 GetPosition()
		{
			return new System.Numerics.Vector3(Position.X, Position.Y, Position.Z);
		}
	}
}