using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The forest's wind turbine's meshes (#1166, <see cref="ForestLampMeshes"/>'s turbine pieces), uploaded: what stands still,
    /// the rotor and the aviation light. Public so <c>WindingCheck</c> can build and judge them as every other scene's.
    /// </summary>
    public sealed class ForestTurbineMeshes : IDisposable
    {
        internal UploadedMesh Body { get; private set; }
        internal UploadedMesh Rotor { get; private set; }
        internal UploadedMesh Beacon { get; private set; }

        public ForestTurbineMeshes(GraphicsDevice device)
        {
            Body = Upload(device, ForestLampMeshes.TurbineBody());
            Rotor = Upload(device, ForestLampMeshes.TurbineRotor());
            Beacon = Upload(device, ForestLampMeshes.TurbineBeacon());
        }

        private static UploadedMesh Upload(GraphicsDevice device, MeshBuilder builder)
        {
            (List<VertexPositionNormalTexture> vertices, List<int> indices) = builder.Geometry;
            return new UploadedMesh(device, vertices, indices, new BoundingSphere(Vector3.Zero, ForestTurbineLayout.TOWER_HEIGHT + ForestTurbineLayout.ROTOR_RADIUS));
        }

        public void Dispose()
        {
            Body?.Dispose();
            Rotor?.Dispose();
            Beacon?.Dispose();
            Body = Rotor = Beacon = null;
        }
    }

    /// <summary>
    /// Draws the forest's wind turbine (#1166, <see cref="ForestTurbineLayout"/>) through <c>Forest.fx</c>'s <c>ForestTurbine</c>
    /// technique, after the far stand on the hills it stands on: lit as the stand is by the frame's sun under the deck and the
    /// dome's hemisphere, and hazed as the stand and the floor under it are (<c>ForestHaze</c>), so it stands in the same air
    /// on the same ridge rather than as a crisp white cut-out over a hazed hill. The rotor turns on the scene's clock
    /// (<see cref="ForestTurbineLayout.RotorAngle"/>) and the aviation light flashes on it (<see cref="ForestTurbineLayout.Beacon"/>),
    /// its glow added after the haze, which a red light pierces. Three draws a frame; nothing allocated.
    /// </summary>
    internal sealed class ForestTurbine : IDisposable
    {
        //The white of the tower and the blades (linear), a little grey with weather
        private static readonly Vector3 WHITE = new(0.60f, 0.61f, 0.62f);

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectTechnique _technique;
        private readonly EffectParameter _world, _albedo, _glow;
        private readonly Matrix _tower;
        private ForestTurbineMeshes _meshes;

        public ForestTurbine(GraphicsDevice device, Effect forestEffect, ForestSceneConfig config)
        {
            _device = device;
            _effect = forestEffect;
            _technique = forestEffect.Techniques["ForestTurbine"];
            _world = forestEffect.Parameters["TurbineWorld"];
            _albedo = forestEffect.Parameters["TurbineAlbedo"];
            _glow = forestEffect.Parameters["TurbineGlow"];
            _tower = ForestTurbineLayout.World(config);
            _meshes = new ForestTurbineMeshes(device);
        }

        /// <summary>Draws it at <paramref name="time"/>. The frame's view, sun, sky and deck are the floor's, stated by its pass.</summary>
        public void Draw(float time)
        {
            if (_meshes == null) return;

            EffectTechnique technique = _effect.CurrentTechnique;
            RasterizerState raster = _device.RasterizerState;
            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;

            _device.RasterizerState = RasterizerState.CullCounterClockwise;
            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
            _effect.CurrentTechnique = _technique;

            _albedo.SetValue(WHITE);
            _glow.SetValue(Vector3.Zero);
            DrawPart(_meshes.Body, _tower);
            DrawPart(_meshes.Rotor, ForestTurbineLayout.RotorWorld(_tower, time));

            //The light: black but for its glow
            _albedo.SetValue(Vector3.Zero);
            _glow.SetValue(ForestTurbineLayout.BEACON_COLOUR * ForestTurbineLayout.Beacon(time));
            DrawPart(_meshes.Beacon, _tower);

            _effect.CurrentTechnique = technique;
            _device.RasterizerState = raster;
            _device.BlendState = blend;
            _device.DepthStencilState = depth;
        }

        private void DrawPart(UploadedMesh mesh, Matrix world)
        {
            _world.SetValue(world);
            _technique.Passes[0].Apply();
            _device.SetVertexBuffer(mesh.VertexBuffer);
            _device.Indices = mesh.IndexBuffer;
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
        }

        public void Dispose()
        {
            _meshes?.Dispose();
            _meshes = null;
        }
    }
}
