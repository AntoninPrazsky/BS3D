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
        private const float BALL_RADIUS = 0.5f;
        private const float ALPHA = 0.35f;
        private static readonly Vector3 COLOR = new(0.55f, 0.8f, 1f); //Pale blue, to stay apart from the ball colours

        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private WireBoxMesh _mesh;
        private volatile bool _meshOutOfDate;
        private Matrix _world = Matrix.Identity;

        /// <summary>
        /// Centre of the play field the outline was last fitted to.
        /// </summary>
        public Vector3 Center { get; private set; }

        /// <summary>
        /// Full size of the play field the outline was last fitted to.
        /// </summary>
        public Vector3 Size { get; private set; }

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
            //The field reaches half a ball past the centres of the outermost cells on every side. Along X and Z
            //the far side reaches half a ball further still, because the odd levels are shifted by +0.5
            float height = (map.Levels - 1) / Constants.SQRT_TWO + 2f * BALL_RADIUS;

            Size = new Vector3(map.StageSizeX + BALL_RADIUS, height, map.StageSizeZ + BALL_RADIUS);

            //The mesh is centred at the origin, the field starts at the centre of the ball at [0, 0, 0]
            Center = new Vector3(
                (map.StageSizeX - BALL_RADIUS) / 2f,
                (height / 2f) - BALL_RADIUS,
                (map.StageSizeZ - BALL_RADIUS) / 2f);

            _world = Matrix.CreateTranslation(Center);

            //Maps are loaded on a worker thread, and the mesh holds graphics resources: releasing the old one
            //here would pull the buffers out from under the draw that is running on the main thread meanwhile
            _meshOutOfDate = true;
        }

        public void Draw(ICamera camera)
        {
            if (_meshOutOfDate) RecreateMesh();

            WireBoxMesh mesh = _mesh;
            if (mesh == null) return;

            _effect.World = _world;
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            _graphicsDevice.SetVertexBuffer(mesh.VertexBuffer);
            _graphicsDevice.Indices = mesh.IndexBuffer;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
            }
        }

        private void RecreateMesh()
        {
            //Cleared first, so that a map arriving while the mesh is being built is not missed
            _meshOutOfDate = false;

            _mesh?.Dispose();
            _mesh = new WireBoxMesh(_graphicsDevice, Size.X, Size.Y, Size.Z, BEAM_THICKNESS);
        }

        public void Dispose()
        {
            _mesh?.Dispose();
            _mesh = null;
            _effect?.Dispose();
        }
    }
}
