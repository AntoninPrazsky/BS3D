using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The big top's spot beams on the Potato path (#1044): the desktop's five spots (<c>CircusBackdrop.SpotAimOf</c>, the
    /// same lamps turning on the same clock), the roving pair's each a cone from its lens to where it aims (<see cref="FIRST_BEAM"/>), in its tint, added over the frame
    /// through <c>PotatoModel.fx</c>'s <c>PotatoTrail</c> and fading down its length. <c>Circus.fx</c> integrates every
    /// beam along the view ray through the dusty air; here a cone's two sides, front and back, are what the eye looks
    /// through, so a beam is brighter down its middle than at its edge. Rewritten every frame into one dynamic buffer.
    /// </summary>
    public sealed class PotatoSpotBeams : IDisposable
    {
        //A beam's sides round its axis, its radius at the lens, and how far down its length it has faded at its foot
        //(0 at the lens, 1 gone: Circus.fx's beam reaches the pool it throws)
        private const int SIDES = 16;
        private const float LENS_RADIUS = 0.35f, FOOT_FADE = 0.6f;

        /// <summary>
        /// The first spot with a beam: the two that rove the ring (3 and 4). ⚠ The three that hold on the island come down
        /// past the play camera, and their cones, added over the frame front and back, cost 3 ms at native on BigTop (1.7
        /// with one side culled); the roving pair's cost 0.27. The island's three keep their lenses.
        /// </summary>
        public const int FIRST_BEAM = 3;

        /// <summary>A beam's light against its spot's (the tint at <see cref="CircusLightsConfig.SpotIntensity"/>), by eye on the
        /// Pi's captures: the desktop's is <see cref="CircusLightsConfig.BeamStrength"/> integrated through its haze.</summary>
        public const float BEAM_GAIN = 0.04f;

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectTechnique _technique;
        private readonly EffectParameter _view, _projection, _color;
        private readonly CircusSceneConfig _config;
        private readonly VertexPositionTexture[] _vertices = new VertexPositionTexture[(CircusBackdrop.SPOT_COUNT - FIRST_BEAM) * (SIDES + 1) * 2];
        private DynamicVertexBuffer _buffer;

        private PotatoSpotBeams(GraphicsDevice device, Effect effect, EffectTechnique technique, CircusSceneConfig config)
        {
            _device = device;
            _effect = effect;
            _technique = technique;
            _config = config;
            EffectParameterCollection p = effect.Parameters;
            _view = p["View"]; _projection = p["Projection"]; _color = p["TrailColor"];
            _buffer = new DynamicVertexBuffer(device, VertexPositionTexture.VertexDeclaration, _vertices.Length, BufferUsage.WriteOnly);
        }

        /// <summary>The beams on <paramref name="config"/>; null when the effect has no <c>PotatoTrail</c> yet.</summary>
        public static PotatoSpotBeams Create(GraphicsDevice device, Effect effect, CircusSceneConfig config)
        {
            EffectTechnique technique = effect.Techniques["PotatoTrail"];
            return technique == null ? null : new PotatoSpotBeams(device, effect, technique, config);
        }

        /// <summary>Spot <paramref name="i"/>'s lens at <paramref name="time"/>, turned along its beam: the frame its lamp is drawn in.</summary>
        public Matrix Lens(int i, float time)
        {
            CircusBackdrop.SpotAimOf(_config, i, time, out Vector3 pivot, out Vector3 target);
            Vector3 direction = Vector3.Normalize(target - pivot);
            Matrix lens = Matrix.CreateWorld(pivot + direction * CircusBackdrop.PIVOT_TO_APEX, direction, MathF.Abs(direction.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
            return lens;
        }

        /// <summary>Spot <paramref name="i"/>'s colour, linear: its tint at the config's intensity, the roving pair at their share.</summary>
        public Vector3 Color(int i) =>
            CircusBackdrop.SPOT_TINTS[i] * (_config.Lights.SpotIntensity * (i < 3 ? 1f : CircusBackdrop.ROVING_SHARE));

        /// <summary>
        /// The beams at <paramref name="time"/>, added over the frame, depth-tested and writing none, after the island.
        /// Leaves the depth state default and the blend state alpha.
        /// </summary>
        public void DrawBeams(ICamera camera, float time)
        {
            float tan = MathF.Tan(MathHelper.ToRadians(_config.Lights.SpotHalfAngleDegrees));
            int k = 0;
            for (int i = FIRST_BEAM; i < CircusBackdrop.SPOT_COUNT; i++)
            {
                CircusBackdrop.SpotAimOf(_config, i, time, out Vector3 pivot, out Vector3 target);
                Vector3 direction = Vector3.Normalize(target - pivot);
                Vector3 apex = pivot + direction * CircusBackdrop.PIVOT_TO_APEX;
                float foot = Vector3.Distance(apex, target) * tan;
                Vector3 u = Vector3.Normalize(Vector3.Cross(direction, MathF.Abs(direction.Y) > 0.99f ? Vector3.Right : Vector3.Up));
                Vector3 v = Vector3.Cross(direction, u);
                for (int s = 0; s <= SIDES; s++)
                {
                    float a = MathHelper.TwoPi * s / SIDES;
                    Vector3 off = u * MathF.Cos(a) + v * MathF.Sin(a);
                    _vertices[k++] = new VertexPositionTexture(apex + off * LENS_RADIUS, new Vector2(0f, 0.5f));
                    _vertices[k++] = new VertexPositionTexture(target + off * foot, new Vector2(FOOT_FADE, 0.5f));
                }
            }
            _buffer.SetData(_vertices, 0, k, SetDataOptions.Discard);

            _view?.SetValue(camera.View);
            _projection?.SetValue(camera.Projection);
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.BlendState = BlendState.Additive;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_buffer);

            EffectTechnique previous = _effect.CurrentTechnique;
            _effect.CurrentTechnique = _technique;
            for (int i = FIRST_BEAM; i < CircusBackdrop.SPOT_COUNT; i++)
            {
                _color?.SetValue(Color(i) * BEAM_GAIN);
                _technique.Passes[0].Apply();
                _device.DrawPrimitives(PrimitiveType.TriangleStrip, (i - FIRST_BEAM) * (SIDES + 1) * 2, SIDES * 2);
            }
            _effect.CurrentTechnique = previous;

            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.AlphaBlend;
        }

        public void Dispose()
        {
            _buffer?.Dispose();
            _buffer = null;
        }
    }
}
