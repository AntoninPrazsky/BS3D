using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using Prazsky.Render;
using System;

namespace MapEditor.GUI
{
    /// <summary>
    /// Translucent outline of the play field of the map, generated procedurally so that it always matches
    /// the field exactly. It is drawn thin and see-through, so it marks out the volume without getting in
    /// the way of the balls inside it.
    /// </summary>
    internal class AABB : IDisposable
    {
        private const float BEAM_THICKNESS = 0.06f;
        private const float ALPHA = 0.35f;
        private static readonly Vector3 COLOR = new(0.55f, 0.8f, 1f); //Pale blue, to stay apart from the ball colours

        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private WireBoxMesh _mesh;
        private Matrix _world = Matrix.Identity;

        public AABB(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;

            _effect = new BasicEffect(graphicsDevice)
            {
                LightingEnabled = false, //A flat colour reads better than shading on beams this thin
                VertexColorEnabled = false,
                TextureEnabled = false,
                DiffuseColor = COLOR,
                Alpha = ALPHA
            };
        }

        /// <summary>
        /// Rebuilds the outline so that it encloses the play field of the given map.
        /// </summary>
        public void FitToMap(BallsMap map)
        {
            float height = (map.Levels - 1) / Constants.SQRT_TWO + 1f; //From the bottom of level 0 balls to the top of top-level balls

            _mesh?.Dispose();
            _mesh = new WireBoxMesh(_graphicsDevice, map.StageSizeX, height, map.StageSizeZ, BEAM_THICKNESS);

            //The mesh is centred at the origin, the field starts at the centre of the ball at [0, 0, 0]
            _world = Matrix.CreateTranslation(new Vector3(map.StageSizeX / 2f, (height / 2f) - 0.5f, map.StageSizeZ / 2f));
        }

        public void Draw(ICamera camera)
        {
            if (_mesh == null) return;

            _effect.World = _world;
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            _graphicsDevice.SetVertexBuffer(_mesh.VertexBuffer);
            _graphicsDevice.Indices = _mesh.IndexBuffer;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _mesh.PrimitiveCount);
            }
        }

        public void Dispose()
        {
            _mesh?.Dispose();
            _mesh = null;
            _effect?.Dispose();
        }
    }
}
