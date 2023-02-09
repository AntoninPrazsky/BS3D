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
			Matrix worldMatrix = Matrix.CreateTranslation(position);

			if (yRotation > 0f) worldMatrix *= Matrix.CreateRotationY(yRotation);

			UpdateWorldMatrix(worldMatrix);
		}
	}
}
