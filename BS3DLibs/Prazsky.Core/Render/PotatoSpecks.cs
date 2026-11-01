using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Points of light a pixel or two wide at any distance on the Potato path (#1044, <c>PotatoModel.fx</c>'s
    /// <c>PotatoSpecks</c>): the cavern's glowworms, which the Pi drew none of. The quads are the desktop's own
    /// (<see cref="CavernGeometry.BuildGlowworms"/>, a corner, the breath's phase and the size at each vertex); the size on
    /// screen, the lift towards the lens, the breath and the fog are the vertex shader's, and the whole field is one draw.
    /// </summary>
    public sealed class PotatoSpecks : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectTechnique _technique;
        private readonly EffectParameter _view, _projection, _eye, _color, _scale, _fog, _time;
        private VertexBuffer _vertices;
        private IndexBuffer _indices;
        private readonly int _count;
        private readonly Vector3 _colour;
        private readonly float _pixels, _fogDensity;

        private PotatoSpecks(GraphicsDevice device, Effect effect, EffectTechnique technique, CavernGlowwormVertex[] vertices, int[] indices,
            Vector3 colour, float pixels, float fogDensity)
        {
            _device = device;
            _effect = effect;
            _technique = technique;
            _colour = colour;
            _pixels = pixels;
            _fogDensity = fogDensity;
            EffectParameterCollection p = effect.Parameters;
            _view = p["View"]; _projection = p["Projection"]; _eye = p["EyePosition"];
            _color = p["SpeckColor"]; _scale = p["SpeckScale"]; _fog = p["SpeckFog"]; _time = p["SpeckTime"];

            _count = indices.Length / 6;
            _vertices = new VertexBuffer(device, CavernGlowwormVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _vertices.SetData(vertices);
            _indices = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            _indices.SetData(indices);
        }

        /// <summary>
        /// The specks of <paramref name="vertices"/> in <paramref name="colour"/> (linear), <paramref name="pixels"/> in radius
        /// at 1080 lines for a size of 1, fading by <paramref name="fogDensity"/> on the way to the lens; null when the effect
        /// has no <c>PotatoSpecks</c> yet or there is nothing to draw.
        /// </summary>
        public static PotatoSpecks Create(GraphicsDevice device, Effect effect, CavernGlowwormVertex[] vertices, int[] indices,
            Vector3 colour, float pixels, float fogDensity)
        {
            EffectTechnique technique = effect.Techniques["PotatoSpecks"];
            if (technique == null || indices.Length == 0) return null;
            return new PotatoSpecks(device, effect, technique, vertices, indices, colour, pixels, fogDensity);
        }

        /// <summary>
        /// The specks at <paramref name="time"/>, added, depth-tested and writing no depth. Leaves the depth state default
        /// and the blend state alpha.
        /// </summary>
        public void Draw(ICamera camera, float time)
        {
            //A size in pixels, the same share of the picture at every resolution (Cavern.fx's GlowwormPixels is at 1080 lines)
            Viewport viewport = _device.Viewport;
            float pixels = _pixels * viewport.Height / 1080f;
            _view?.SetValue(camera.View);
            _projection?.SetValue(camera.Projection);
            _eye?.SetValue(camera.Position);
            _color?.SetValue(_colour);
            _scale?.SetValue(new Vector2(2f * pixels / viewport.Width, 2f * pixels / viewport.Height));
            _fog?.SetValue(_fogDensity);
            _time?.SetValue(time);

            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.BlendState = BlendState.Additive;
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
