using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;

namespace Prazsky.Core
{
    /// <summary>
    /// It represents a three-dimensional backdrop formed by a three-dimensional model anywhere in the three-dimensional world.
    /// </summary>
    public class Backdrop3D : Object3D
    {
        /// <summary>
        /// Constructor of three-dimensional backdrop.
        /// </summary>
        /// <param name="model3D">Three-dimensional model for rendering.</param>
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