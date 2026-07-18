using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core;
using Prazsky.Core.Tools;

namespace MapEditor.GUI
{
    internal class AABB : Object3D
    {
        //Dimensions the AABB model is modelled at: a 10×10 stage with 10 levels ((10 - 1)/√2 + ball diameter high)
        private const float MODEL_HEIGHT = 7.3639610306789f;
        private const float MODEL_SIZE_XZ = 10f;

        public AABB(ContentManager contentManager)
        {
            Model = contentManager.Load<Model>("AABB");
            Transformations = new Matrix[Model.Bones.Count];
            Model.CopyAbsoluteBoneTransformsTo(Transformations);

            Position = new Vector3(MODEL_SIZE_XZ / 2f, (MODEL_HEIGHT / 2f) - 0.5f, MODEL_SIZE_XZ / 2f);

            BasicEffectParams = BasicEffectParamsProvider.ColorWhite; //BasicEffectParamsProvider will always create a new instance, it would make more sense to remember them
            World = Matrix.CreateTranslation(Position);
        }

        /// <summary>
        /// Scales and positions the box so it outlines the play field of the given map.
        /// </summary>
        public void FitToMap(BallsMap map)
        {
            float height = (map.Levels - 1) / Constants.SQRT_TWO + 1f; //From the bottom of level 0 balls to the top of top-level balls

            Position = new Vector3(map.StageSizeX / 2f, (height / 2f) - 0.5f, map.StageSizeZ / 2f);

            World = Matrix.CreateScale(map.StageSizeX / MODEL_SIZE_XZ, height / MODEL_HEIGHT, map.StageSizeZ / MODEL_SIZE_XZ)
                * Matrix.CreateTranslation(Position);
        }
    }
}