using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;

namespace Testbed.Backdrops
{
	internal class Castle : Backdrop3D
	{
		public Castle(Model model3D, Vector3 position, float yRotation = 0f) : base(model3D)
		{
			Position = position;

			//Rotation first, then translation: the castle turns around its own axis and stands at the
			//given position (the reversed order used to orbit the position itself around the world origin)
			Matrix worldMatrix = Matrix.CreateRotationY(yRotation) * Matrix.CreateTranslation(position);

			UpdateWorldMatrix(worldMatrix);
		}
	}
}
