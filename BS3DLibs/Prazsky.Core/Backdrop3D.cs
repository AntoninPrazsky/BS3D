using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Tools;

namespace Prazsky.Core
{
	/// <summary>
	/// Představuje trojrozměrnou kulisu tvořenou trojrozměrným modelem kdekoliv v trojrozměrném světě.
	/// </summary>
	public class Backdrop3D : Object3D
	{
		/// <summary>
		/// Konstruktor trojrozměrné kulisy.
		/// </summary>
		/// <param name="model3D">Trojrozměrný model pro vykreslování.</param>
		public Backdrop3D(Model model3D)
		{
			Model = model3D;

			BoundingSphere = Geometry.GetBoundingSphere(Model);

			Transformations = new Matrix[Model.Bones.Count];
			Model.CopyAbsoluteBoneTransformsTo(Transformations);

			World = Matrix.CreateTranslation(Position);

			BoundingSphere = new BoundingSphere(Position, BoundingSphere.Radius);
		}

		public void UpdateWorldMatrix(Matrix world)
		{
			World = world;
		}
	}
}