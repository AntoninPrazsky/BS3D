using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One open-ground scene's camera grid and the pass that draws it (#580): the skeleton the polar icesheet,
    /// the desert and the outback each carried a copy of, identical but for their field names and the name of
    /// the time uniform. Snaps the grid to a cell round the camera (so the ground does not swim), pushes the
    /// camera, the sky and the clock, lets the frame's cloud deck at the effect, draws opaque and
    /// <c>CullNone</c> with the far fade stated first, then the far ring over the same effect, and restores the
    /// scene block's states.
    /// <para>
    /// <b>What varies between scenes is exactly what it takes</b>: the effect (its config already pushed by its
    /// owner, once at load), the grid's density and extent, and the time uniform's name (none, for a ground on
    /// which nothing moves). Its per-frame parameters are resolved once here (BestPractices §1) where the
    /// copies set them by name every frame. Mars, the mountains, the meadow and the forest joined it as they
    /// moved (#580). The volcano did not: its flank has always left the blend state as it found it, where this
    /// states opaque and restores alpha-blend, and the fountains draw straight after it. The sea joined it with
    /// what it has more wrapped round the call — the pool's radius set before and the depth-read state around
    /// it, since the pass touches no depth state. The savanna and the beach still run the skeleton on the
    /// renderer with more in it — point lights, a second pass — and join it when they move and it can be shown
    /// what they need.
    /// </para>
    /// <para>
    /// <b>Mars was the first to bring something</b>: a reduced program on a coarser grid (#540), so the grid can
    /// be retaken at another density (<see cref="SetGrid"/>), and a second technique on the same effect (its
    /// moons), so it states the ground's technique as the current one before each <see cref="Draw"/> — which
    /// applies whatever technique is current, exactly as the copies did.
    /// </para>
    /// <para>
    /// The grid comes from the renderer's <see cref="TerrainGridCache"/> through
    /// <see cref="BackdropServices.AcquireGridMesh"/> and is never disposed here.
    /// </para>
    /// </summary>
    internal sealed class TerrainPass
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly FarField _farField;
        private readonly BackdropServices _services;

        //Not readonly: a scene whose reduced program runs on a coarser grid retakes it (SetGrid, #540)
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private int _indexCount;
        private int _gridN;
        private readonly float _extent;
        private float _cell;

        private readonly EffectParameter _originXZ, _islandHoleRadius, _view, _projection, _cameraPosition,
            _sunDirection, _zenithColor, _horizonColor, _time, _sunColor;

        /// <summary>The scene's terrain effect, loaded (and owned) by the backdrop that draws through this pass.</summary>
        public Effect Effect { get; }

        /// <summary>
        /// Takes a grid of <paramref name="gridN"/> vertices a side over <paramref name="extent"/> and resolves
        /// <paramref name="effect"/>'s per-frame parameters, <paramref name="timeParameter"/> being the name of
        /// the scene's own clock uniform (<c>PolarTime</c>, <c>DesertTime</c>, …), or null for a ground on which
        /// nothing moves (Mars).
        /// </summary>
        public TerrainPass(BackdropServices services, Effect effect, int gridN, float extent, string timeParameter)
        {
            _graphicsDevice = services.GraphicsDevice;
            _farField = services.FarField;
            _services = services;
            Effect = effect;
            _extent = extent;
            _cell = extent / (gridN - 1);
            _gridN = gridN;

            services.AcquireGridMesh(gridN, extent, out _vertexBuffer, out _indexBuffer, out _indexCount);

            _originXZ = effect.Parameters["OriginXZ"];
            _islandHoleRadius = effect.Parameters["IslandHoleRadius"];
            _view = effect.Parameters["View"];
            _projection = effect.Parameters["Projection"];
            _cameraPosition = effect.Parameters["CameraPosition"];
            _sunDirection = effect.Parameters["SunDirection"];
            _zenithColor = effect.Parameters["ZenithColor"];
            _horizonColor = effect.Parameters["HorizonColor"];
            _time = timeParameter == null ? null : effect.Parameters[timeParameter];
            _sunColor = effect.Parameters["SunColor"];
        }

        /// <summary>
        /// Retakes the grid at <paramref name="gridN"/> vertices a side when it is not that already — Mars's reduced
        /// program runs on a coarser one (#540), so this runs when the tier crosses <see cref="SceneRenderer.SceneDetail"/>'s
        /// line and never per frame.
        /// </summary>
        public void SetGrid(int gridN)
        {
            if (_gridN == gridN) return;

            //Given back rather than disposed: the grid cache owns it, and at full detail another scene draws the same one
            _services.ReleaseGridMesh(_gridN, _extent);
            _services.AcquireGridMesh(gridN, _extent, out _vertexBuffer, out _indexBuffer, out _indexCount);
            _gridN = gridN;
            _cell = _extent / (gridN - 1);
        }

        /// <summary>
        /// Draws the grid round the camera and the far ring past it, the island's footprint cut out to
        /// <paramref name="holeRadius"/>. Leaves the blend state alpha and the rasterizer culling
        /// counter-clockwise, which is what the scene block expects after every terrain.
        /// </summary>
        public void Draw(in SceneFrame frame, float holeRadius)
        {
            float originX = MathF.Round(frame.Camera.Position.X / _cell) * _cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / _cell) * _cell;

            _originXZ.SetValue(new Vector2(originX, originZ));
            _islandHoleRadius.SetValue(holeRadius);
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(frame.Camera.Position);
            _sunDirection.SetValue(frame.SunDirection);
            _zenithColor.SetValue(frame.ZenithLinear);
            _horizonColor.SetValue(frame.HorizonLinear);
            _time?.SetValue(frame.Time);
            _sunColor.SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(Effect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _farField.Begin(Effect, frame, _extent);
            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;
            Effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _indexCount / 3);
            _farField.DrawRing(Effect, new Vector2(originX, originZ), _extent);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }
    }
}
