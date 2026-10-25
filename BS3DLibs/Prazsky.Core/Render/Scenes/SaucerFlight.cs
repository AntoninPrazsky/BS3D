using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The flying saucer in space (#955): one rigid craft on a slow loop far out round the station, a lathed hull with a ring
    /// of lights round its rim, a glass dome with its crew in it and a glow under its belly (<c>Saucer.fx</c>). Placed off
    /// the wall clock, so it flies on while the simulation is paused, and drawn after the sky's quad writing depth, so the
    /// station, the gun and the cluster stand in front of it.
    /// <para>
    /// <b>Where it flies.</b> <see cref="ORBIT_RADIUS"/> out, over twice the chapter intro's widest lens (the establishing
    /// shot stands ninety units off, <c>SpaceIntroShots</c>), so nothing that films the station can meet it, and well inside
    /// the camera's far plane (<see cref="Camera.BasicCamera3D.DEFAULT_FAR_PLANE_DISTANCE"/>). At that distance it is some
    /// fifty pixels across at 1080p: a thing noticed passing, not a prop that fights the cluster.
    /// </para>
    /// </summary>
    internal sealed class SaucerFlight : IDisposable
    {
        //The loop: its radius, its height over the world's origin and how far it rises and falls, how long one turn takes and
        //where on it the craft starts; the bank into the turn, a slow rock about it and the craft's spin about its own axis
        private const float ORBIT_RADIUS = 240f;
        private const float ORBIT_Y = 30f;
        private const float ORBIT_BOB = 14f;
        private const float ORBIT_SECONDS = 70f;
        private const float ORBIT_PHASE = 2.2f;
        private const float BANK = 0.22f;
        private const float ROCK = 0.06f;
        private const float SPIN = 0.8f;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly EffectTechnique _hullTechnique, _domeTechnique;
        private readonly EffectParameter _world, _view, _projection, _cameraPosition, _time, _sunDirection, _sunColor;
        private readonly EffectParameter _skyAmbient, _groundAmbient;
        private readonly SaucerMesh _mesh;

        public SaucerFlight(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _effect = content.Load<Effect>("Shaders/Saucer");
            _hullTechnique = _effect.Techniques["SaucerHull"];
            _domeTechnique = _effect.Techniques["SaucerDome"];
            _world = _effect.Parameters["World"];
            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _time = _effect.Parameters["Time"];
            _sunDirection = _effect.Parameters["SunDirection"];
            _sunColor = _effect.Parameters["SunColor"];

            _skyAmbient = _effect.Parameters["SkyAmbient"];
            _groundAmbient = _effect.Parameters["GroundAmbient"];
            _effect.Parameters["RimRadius"].SetValue(SaucerMesh.RIM_RADIUS);
            _effect.Parameters["RimBand"].SetValue(SaucerMesh.RIM_BAND);
            _effect.Parameters["UnderY"].SetValue(SaucerMesh.UNDER_Y);
            _effect.Parameters["DomeSpan"].SetValue(new Vector2(SaucerMesh.DOME_BASE, SaucerMesh.DOME_HEIGHT));
            _effect.Parameters["TopRing"].SetValue(SaucerMesh.TOP_RING);

            _mesh = new SaucerMesh(graphicsDevice);
        }

        /// <summary>Where the craft is and how it lies at <paramref name="time"/>: on its loop, banked into the turn, rocking,
        /// spinning about its own axis.</summary>
        internal static Matrix WorldAt(float time)
        {
            float angle = ORBIT_PHASE + time * MathHelper.TwoPi / ORBIT_SECONDS;
            var centre = new Vector3(MathF.Cos(angle) * ORBIT_RADIUS, ORBIT_Y + ORBIT_BOB * MathF.Sin(time * 0.21f), MathF.Sin(angle) * ORBIT_RADIUS);

            //After the turn about Y by -angle the craft's own X points out from the station, so a turn about Z tips its outer
            //side up: banked into the turn
            Matrix world = Matrix.CreateRotationY(-time * SPIN)
                * Matrix.CreateRotationZ(BANK + ROCK * MathF.Sin(time * 0.7f))
                * Matrix.CreateRotationY(-angle);
            world.Translation = centre;
            return world;
        }

        /// <summary>Draws the craft, opaque and writing depth, lit by the scene's own rig (<paramref name="lighting"/>, the
        /// config's, which a level may replace). Leaves the depth and blend states as it found them.</summary>
        public void Draw(in SceneFrame frame, SpaceLightingConfig lighting)
        {
            _skyAmbient.SetValue(lighting.SkyAmbient.ToVector3());
            _groundAmbient.SetValue(lighting.GroundAmbient.ToVector3());
            _world.SetValue(WorldAt(frame.Time));
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(frame.Camera.Position);
            _time.SetValue(frame.Time);
            _sunDirection.SetValue(frame.SunDirection);
            _sunColor.SetValue(frame.SunColor);

            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState rasterizer = _graphicsDevice.RasterizerState;
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            DrawMesh(_mesh.Hull, _hullTechnique);
            DrawMesh(_mesh.Dome, _domeTechnique);

            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = rasterizer;
        }

        private void DrawMesh(LatheMesh mesh, EffectTechnique technique)
        {
            _graphicsDevice.SetVertexBuffer(mesh.VertexBuffer);
            _graphicsDevice.Indices = mesh.IndexBuffer;
            _effect.CurrentTechnique = technique;
            technique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
        }

        public void Dispose()
        {
            _mesh?.Dispose();
        }
    }
}
