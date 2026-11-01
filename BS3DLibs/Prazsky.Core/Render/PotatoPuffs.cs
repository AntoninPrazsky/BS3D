using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One kind of puff on the Potato path (#1044, <c>PotatoModel.fx</c>'s <c>PotatoPuffs</c>): soft round billboards
    /// that rise from their emitters, grow and fade on a clock. The steam over the volcano's vents, the ash out of its
    /// crater, the savanna's flames and their smoke. The buffer is built once, every puff's life is the vertex shader's,
    /// and the kind is one draw.
    /// </summary>
    public sealed class PotatoPuffs : IDisposable
    {
        /// <summary>How a kind of puff moves and looks: linear colour, the rise and the drift over a life, the life in
        /// seconds, the growth over it (negative tapers, a flame), the half-size at birth and the opacity at the thickest.
        /// Additive for light (flame), alpha-blended for smoke. The edge is how hard the rim is: 1 a soft puff, more a disc.
        /// A still kind never ages: every puff is held at its thickest and at its size, a body in the sky (the aurora's Moon).</summary>
        public readonly record struct Look(Vector3 Color, float Rise, float Life, float Growth, float Size, float Opacity, Vector2 Wind, bool Additive,
            float Edge = 1f, bool Still = false);

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectTechnique _technique;
        private readonly EffectParameter _view, _projection, _time, _color, _motion, _wind, _size, _edge;
        private VertexBuffer _vertices;
        private IndexBuffer _indices;
        private readonly int _count;

        /// <summary>Where in its life a still puff stands: past the shader's fade-in (0.12) and before its fade-out (0.55).</summary>
        public const float STILL_PHASE = 0.3f;

        /// <summary>The kind's look.</summary>
        public Look Kind { get; }

        private PotatoPuffs(GraphicsDevice device, Effect effect, EffectTechnique technique, Look look, VertexPositionColorTexture[] vertices)
        {
            _device = device;
            _effect = effect;
            _technique = technique;
            Kind = look;
            EffectParameterCollection p = effect.Parameters;
            _view = p["View"]; _projection = p["Projection"]; _time = p["PuffTime"]; _color = p["PuffColor"];
            _motion = p["PuffMotion"]; _wind = p["PuffWind"]; _size = p["PuffSize"]; _edge = p["PuffEdge"];

            _count = vertices.Length / 4;
            _vertices = new VertexBuffer(device, VertexPositionColorTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            _vertices.SetData(vertices);
            var indices = new int[_count * 6];
            for (int i = 0; i < _count; i++)
            {
                int v = i * 4, k = i * 6;
                indices[k] = v; indices[k + 1] = v + 1; indices[k + 2] = v + 2;
                indices[k + 3] = v; indices[k + 4] = v + 2; indices[k + 5] = v + 3;
            }
            _indices = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            _indices.SetData(indices);
        }

        /// <summary>
        /// <paramref name="perEmitter"/> puffs over each of <paramref name="emitters"/> (a position and its strength), their
        /// phases spread evenly over a life and their size, rise and strength rolled off <paramref name="seed"/>; null when the
        /// effect has no <c>PotatoPuffs</c> yet or there is nothing to draw.
        /// </summary>
        public static PotatoPuffs Create(GraphicsDevice device, Effect effect, Look look, IReadOnlyList<(Vector3 At, float Strength)> emitters, int perEmitter, int seed)
        {
            EffectTechnique technique = effect.Techniques["PotatoPuffs"];
            if (technique == null || emitters.Count == 0 || perEmitter <= 0) return null;

            return new PotatoPuffs(device, effect, technique, look, Build(emitters, perEmitter, seed, look.Still));
        }

        /// <summary>The puffs' quads, four corners each, with no device: for the tests. A still puff is born at
        /// <see cref="STILL_PHASE"/> (the shader's fade is whole there, and <see cref="Draw"/> stops its clock) at its size.</summary>
        public static VertexPositionColorTexture[] Build(IReadOnlyList<(Vector3 At, float Strength)> emitters, int perEmitter, int seed, bool still = false)
        {
            var rng = new Random(seed);
            Vector2[] corners = { new(-1f, -1f), new(1f, -1f), new(1f, 1f), new(-1f, 1f) };
            var vertices = new VertexPositionColorTexture[emitters.Count * perEmitter * 4];
            int n = 0;
            foreach ((Vector3 at, float strength) in emitters)
            {
                float offset = (float)rng.NextDouble();
                for (int i = 0; i < perEmitter; i++)
                {
                    float phase = (offset + (i + 0.5f * (float)rng.NextDouble()) / perEmitter) % 1f;
                    var look = still ? new Color(STILL_PHASE, 0.5f, 0.5f, Math.Clamp(strength, 0f, 1f))
                        : new Color(phase, (float)rng.NextDouble(), (float)rng.NextDouble(), Math.Clamp(strength, 0f, 1f));
                    foreach (Vector2 corner in corners) vertices[n++] = new VertexPositionColorTexture(at, look, corner);
                }
            }

            return vertices;
        }

        /// <summary>
        /// The kind's puffs at <paramref name="time"/>, depth-tested and writing no depth, blended by the look. Leaves the
        /// depth state default and the blend state alpha.
        /// </summary>
        public void Draw(ICamera camera, float time)
        {
            _view?.SetValue(camera.View);
            _projection?.SetValue(camera.Projection);
            _time?.SetValue(Kind.Still ? 0f : time);
            _color?.SetValue(Kind.Color);
            _motion?.SetValue(new Vector4(Kind.Rise, MathF.Max(Kind.Life, 0.01f), Kind.Growth, Kind.Opacity));
            _wind?.SetValue(Kind.Wind);
            _size?.SetValue(Kind.Size);
            _edge?.SetValue(MathF.Max(Kind.Edge, 1f));

            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.BlendState = Kind.Additive ? BlendState.Additive : BlendState.NonPremultiplied;
            _device.RasterizerState = RasterizerState.CullNone;

            EffectTechnique previous = _effect.CurrentTechnique;
            _effect.CurrentTechnique = _technique;
            _technique.Passes[0].Apply();
            _device.SetVertexBuffer(_vertices);
            _device.Indices = _indices;
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _count * 2);
            _effect.CurrentTechnique = previous;

            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.AlphaBlend;
        }

        public void Dispose()
        {
            _vertices?.Dispose(); _vertices = null;
            _indices?.Dispose(); _indices = null;
        }
    }
}
