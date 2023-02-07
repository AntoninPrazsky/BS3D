using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;

namespace Testbed.Backdrops
{
	internal class Castle : Backdrop3D
	{
		public Castle(Model model3D, Vector3 position) : base(model3D)
		{
			Position = position;
			UpdateWorldMatrix(Matrix.CreateTranslation(Position));
		}
	}
}
