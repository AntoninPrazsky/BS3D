using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core;
using System.Data;

namespace MapEditor.GUI
{
	internal class AABB : Object3D
	{
		private const float Y_POS = (7.3639610306789f / 2f) - 0.5f;
		private const float XZ_POS = (10f / 2f);

		public AABB(ContentManager contentManager)
		{
			Model = contentManager.Load<Model>("AABB");
			Transformations = new Matrix[Model.Bones.Count];
			Model.CopyAbsoluteBoneTransformsTo(Transformations);

			Position = new Vector3(XZ_POS, Y_POS, XZ_POS);

			BasicEffectParams = BasicEffectParamsProvider.ColorWhite; //BasicEffectParamsProvider udělá vždycky novou instanci, dávalo by spíš smysl si je pamatovat
			World = Matrix.CreateTranslation(Position);
		}
	}
}