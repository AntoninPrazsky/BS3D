using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The Grid's light cycles on the Potato path (#1044): the desktop's three riders (<see cref="LightCycles.Rider"/>, on
    /// the same seeds, so the same runs at the same moment), each a cycle the backdrop draws as lit pieces
    /// (<see cref="World"/>) and the wall behind it, added through <c>PotatoModel.fx</c>'s <c>PotatoTrail</c>: a strip
    /// rewritten every frame into one dynamic buffer, as the desktop's is.
    /// </summary>
    public sealed class PotatoLightCycles : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectTechnique _technique;
        private readonly EffectParameter _view, _projection, _color;
        private readonly LightCycles.Rider[] _riders = new LightCycles.Rider[LightCycles.RIDERS];
        private readonly VertexPositionTexture[] _trail = new VertexPositionTexture[LightCycles.RIDERS * LightCycles.TRAIL_POINTS * 2];
        private readonly int[] _counts = new int[LightCycles.RIDERS];
        private DynamicVertexBuffer _vertices;
        private readonly float _floorY;

        //LightCycle.fx's TRAIL_RADIANCE: the wall's middle against the rider's colour
        private const float TRAIL_RADIANCE = 0.9f;

        private PotatoLightCycles(GraphicsDevice device, Effect effect, EffectTechnique technique, GridSceneConfig config)
        {
            _device = device;
            _effect = effect;
            _technique = technique;
            _floorY = config.Terrain.LevelY;
            EffectParameterCollection p = effect.Parameters;
            _view = p["View"]; _projection = p["Projection"]; _color = p["TrailColor"];
            for (int i = 0; i < _riders.Length; i++) _riders[i] = new LightCycles.Rider(LightCycles.RiderSeed(config, i));
            _vertices = new DynamicVertexBuffer(device, VertexPositionTexture.VertexDeclaration, _trail.Length, BufferUsage.WriteOnly);
        }

        /// <summary>The cycles on <paramref name="config"/>; null when the effect has no <c>PotatoTrail</c> yet.</summary>
        public static PotatoLightCycles Create(GraphicsDevice device, Effect effect, GridSceneConfig config)
        {
            EffectTechnique technique = effect.Techniques["PotatoTrail"];
            return technique == null ? null : new PotatoLightCycles(device, effect, technique, config);
        }

        /// <summary>How many riders there are.</summary>
        public static int Count => LightCycles.RIDERS;

        /// <summary>Rider <paramref name="index"/>'s colour, linear radiance (the desktop's, past the glare threshold).</summary>
        public static Vector3 Color(int index) => LightCycles.COLORS[index];

        /// <summary>
        /// Where cycle <paramref name="index"/> stands at <paramref name="time"/>, its own +X along its heading and its foot on
        /// the floor: the frame its pieces are built in (LightCycles' own turn).
        /// </summary>
        public Matrix World(int index, float time)
        {
            _riders[index].At(time * LightCycles.SPEED, out Vector2 position, out Vector2 heading);
            Matrix world = Matrix.CreateRotationY(MathF.Atan2(-heading.Y, heading.X));
            world.Translation = new Vector3(position.X, _floorY, position.Y);
            return world;
        }

        /// <summary>
        /// The walls at <paramref name="time"/>, added over the frame, depth-tested and writing none; after the island, which
        /// would otherwise draw over a wall in front of it. Leaves the depth state default and the blend state alpha.
        /// </summary>
        public void DrawWalls(ICamera camera, float time)
        {
            float distance = time * LightCycles.SPEED;
            int written = 0;
            for (int i = 0; i < _riders.Length; i++)
            {
                _counts[i] = _riders[i].Trail(distance, _trail, written, _floorY);
                written += _counts[i];
            }
            //MonoGame's SetData throws on a count of nought: in the clock's first moments no rider has cleared its back wheel
            if (written == 0) return;
            _vertices.SetData(_trail, 0, written, SetDataOptions.Discard);

            _view?.SetValue(camera.View);
            _projection?.SetValue(camera.Projection);
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.BlendState = BlendState.Additive;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SetVertexBuffer(_vertices);

            EffectTechnique previous = _effect.CurrentTechnique;
            _effect.CurrentTechnique = _technique;
            int first = 0;
            for (int i = 0; i < _riders.Length; i++)
            {
                if (_counts[i] >= 4)
                {
                    _color?.SetValue(LightCycles.COLORS[i] * TRAIL_RADIANCE);
                    _technique.Passes[0].Apply();
                    _device.DrawPrimitives(PrimitiveType.TriangleStrip, first, _counts[i] - 2);
                }
                first += _counts[i];
            }
            _effect.CurrentTechnique = previous;

            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.AlphaBlend;
        }

        public void Dispose()
        {
            _vertices?.Dispose();
            _vertices = null;
        }
    }
}
