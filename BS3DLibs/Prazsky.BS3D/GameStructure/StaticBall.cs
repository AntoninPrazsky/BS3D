using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;
using Prazsky.Render;

namespace Prazsky.BS3D.GameStructure
{
	public class StaticBall : Object3D
	{
		public eBallType Type { get; }

		//Grafiku budu řešit později, tohle je jenom dočasné
		private readonly BasicEffectParams colorRed = new BasicEffectParams(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0f, 0f), 0.5f, Vector3.Zero);
		private readonly BasicEffectParams colorGreen = new BasicEffectParams(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 1f, 0f), 0.5f, Vector3.Zero);
		private readonly BasicEffectParams colorBlue = new BasicEffectParams(new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 1f), 0.5f, Vector3.Zero);

		public StaticBall(Vector3 position, eBallType type)
		{
			Type = type;
			Position = position;

			switch (type)
			{
				case eBallType.Type1:
					BasicEffectParams = colorRed;
					break;

				case eBallType.Type2:
					BasicEffectParams = colorGreen;
					break;

				case eBallType.Type3:
					BasicEffectParams = colorBlue;
					break;

				default:
					BasicEffectParams = null;
					break;
			}
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