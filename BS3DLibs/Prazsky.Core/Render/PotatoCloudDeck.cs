using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The cloud deck on the Potato path (#1044): the desktop's weather over the seven scenes that stand under a dome, which
    /// had their dome's bare gradient on the Pi. The deck is the same field <see cref="CloudField"/> drives on the desktop,
    /// with the same plane, wind, cover, opacity and colours; <c>PotatoSky.fx</c>'s <c>PotatoCloudDeck</c> draws it.
    /// <para>
    /// <b>Built for the price of a texture read.</b> The desktop's <c>Sky.fx</c> evaluates gradient noise per pixel,
    /// octave by octave. Per-pixel noise on the Pi cost 12 ms at 1080p (#829's skies). Here the field is baked once into
    /// a tiling texture (<see cref="BakeField"/>): the weather layer's two octaves in red and three finer octaves in green.
    /// The pixel reads it twice, once at the weather's scale and once at a finer one that drifts apart, so the edges keep
    /// moving and the tile does not repeat to the eye.
    /// </para>
    /// <para>
    /// <b>A real plane, drawn small.</b> The mesh is a disc on the plane one unit over the eye, rings closer together
    /// near the middle, out to where the deck is <see cref="HORIZON_RISE"/> over the horizon. It is drawn round the camera,
    /// scaled by <see cref="DRAW_SCALE"/>, inside the far plane and with no depth test, as the dome is. A uniform scale
    /// about the eye projects to the same picture, and the triangles stay flat, so the texture's coordinates (the
    /// plane's world position, taken in the vertex shader) are interpolated exactly. Nothing the deck stands behind
    /// needs a depth for it: the backdrop and everything else are drawn after.
    /// </para>
    /// </summary>
    public sealed class PotatoCloudDeck : IDisposable
    {
        /// <summary>The baked field's side in texels.</summary>
        public const int TEXTURE_SIZE = 256;

        /// <summary>
        /// The weather layer's features across one tile: the shader divides the weather's scale by this. Four at
        /// <see cref="TEXTURE_SIZE"/> is a texel for every seven units of the deck at Scattered's 450-unit feature.
        /// </summary>
        public const int TILE_FEATURES = 4;

        /// <summary>The green channel's base period across a tile, finer than the weather's: three octaves on it, doubling.</summary>
        public const int DETAIL_FEATURES = 8;

        /// <summary>
        /// How far out the disc goes: to where the plane is this much over the horizon, as the sine of the angle (about
        /// 1.4 degrees). The desktop's deck fades to nothing at the horizon (<c>CloudHorizonFade</c>), and the last
        /// stretch of it holds only a sliver of cloud.
        /// </summary>
        public const float HORIZON_RISE = 0.025f;

        /// <summary>Rings and segments of the disc: smooth enough for the fade and the silver, which are taken per vertex.</summary>
        public const int RINGS = 32, SEGMENTS = 64;

        /// <summary>
        /// What the disc (one unit over the eye, out to about 40 across) is scaled by round the camera: 10 to 400 units
        /// away, well inside the camera's planes, which the true deck's thousands of units are not.
        /// </summary>
        public const float DRAW_SCALE = 10f;

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectTechnique _technique, _firstTechnique;
        private readonly EffectParameter _world, _view, _projection, _cameraPosition, _texture;
        private VertexBuffer _vertices;
        private IndexBuffer _indices;
        private Texture2D _field;
        private readonly int _triangles;

        private PotatoCloudDeck(GraphicsDevice device, Effect effect, EffectTechnique technique)
        {
            _device = device;
            _effect = effect;
            _technique = technique;
            _firstTechnique = effect.Techniques[0];

            EffectParameterCollection parameters = effect.Parameters;
            _world = parameters["World"];
            _view = parameters["View"];
            _projection = parameters["Projection"];
            _cameraPosition = parameters["CameraPosition"];
            _texture = parameters["CloudTexture"];

            (VertexPositionColor[] vertices, int[] indices) = BuildDisc(RINGS, SEGMENTS, HORIZON_RISE);
            _vertices = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            _vertices.SetData(vertices);
            _indices = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            _indices.SetData(indices);
            _triangles = indices.Length / 3;

            Color[][] levels = MipChain(BakeField(TEXTURE_SIZE), TEXTURE_SIZE);
            _field = new Texture2D(device, TEXTURE_SIZE, TEXTURE_SIZE, mipmap: true, SurfaceFormat.Color);
            for (int level = 0; level < levels.Length && level < _field.LevelCount; level++) _field.SetData(level, null, levels[level], 0, levels[level].Length);
        }

        /// <summary>
        /// The coverage bias at or below which nothing can be drawn. The weather layer stays within one either way, which is
        /// what the desktop's Clear (-1.2) rests on. The finer octaves move an edge by at most <c>CLOUD_DETAIL_REACH</c>
        /// (0.06) times the detail strength, which is 0.14 at Clear's 2.3. So at -1.15 the thickest point is still under
        /// the threshold. A fade out of Clear passes this soon after it starts.
        /// </summary>
        public const float NOTHING_AT_OR_BELOW = -1.15f;

        /// <summary>Whether the sky that is up has any cloud to draw (<see cref="NOTHING_AT_OR_BELOW"/>).</summary>
        public static bool Shows(CloudField clouds) => clouds.CoverageBias > NOTHING_AT_OR_BELOW;

        /// <summary>
        /// The deck drawn through <paramref name="effect"/> (<c>PotatoSky</c>), or null when the compiled effect has no
        /// <c>PotatoCloudDeck</c> technique yet, which keeps the dome's gradient alone.
        /// </summary>
        public static PotatoCloudDeck Create(GraphicsDevice device, Effect effect)
        {
            EffectTechnique technique = effect.Techniques["PotatoCloudDeck"];
            return technique == null ? null : new PotatoCloudDeck(device, effect, technique);
        }

        /// <summary>
        /// The disc on the plane one unit over the eye: a vertex in the middle, then <paramref name="rings"/> rings out to
        /// where the plane is <paramref name="horizonRise"/> over the horizon, spaced by the square of their place so they
        /// are closest overhead. The colour is unused. No device, for the tests.
        /// </summary>
        public static (VertexPositionColor[] Vertices, int[] Indices) BuildDisc(int rings, int segments, float horizonRise)
        {
            //The plane is one unit up, so a point r out is at a rise of 1 / sqrt(1 + r^2)
            float reach = MathF.Sqrt(1f / (horizonRise * horizonRise) - 1f);

            var vertices = new VertexPositionColor[1 + rings * segments];
            vertices[0] = new VertexPositionColor(Vector3.UnitY, Color.White);
            for (int ring = 1; ring <= rings; ring++)
            {
                float t = (float)ring / rings;
                float r = reach * t * t;
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = MathHelper.TwoPi * segment / segments;
                    vertices[1 + (ring - 1) * segments + segment] = new VertexPositionColor(new Vector3(r * MathF.Cos(angle), 1f, r * MathF.Sin(angle)), Color.White);
                }
            }

            var indices = new int[segments * 3 + (rings - 1) * segments * 6];
            int k = 0;
            for (int segment = 0; segment < segments; segment++)
            {
                indices[k++] = 0;
                indices[k++] = 1 + segment;
                indices[k++] = 1 + (segment + 1) % segments;
            }

            for (int ring = 1; ring < rings; ring++)
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = 1 + (ring - 1) * segments + segment, b = 1 + (ring - 1) * segments + (segment + 1) % segments;
                    int c = a + segments, d = b + segments;
                    indices[k++] = a; indices[k++] = c; indices[k++] = b;
                    indices[k++] = b; indices[k++] = c; indices[k++] = d;
                }

            return (vertices, indices);
        }

        /// <summary>
        /// The field one tile holds, <paramref name="size"/> texels a side, tiling at its edges. Red is the weather layer:
        /// <c>Clouds.fxh</c>'s two octaves (0.62 and 0.38) with its gradient noise, taken on a lattice that wraps every
        /// <see cref="TILE_FEATURES"/> cells, and the second octave at three times the first so it wraps with it (the
        /// desktop's is 2.7). Green is three finer octaves for the edges, on <see cref="DETAIL_FEATURES"/>. Both are stored
        /// as half the value plus a half. No device, for the tests.
        /// </summary>
        public static Color[] BakeField(int size)
        {
            var texels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;

                    float weather = 0.62f * WrappedNoise(u * TILE_FEATURES, v * TILE_FEATURES, TILE_FEATURES, 0)
                        + 0.38f * WrappedNoise(u * TILE_FEATURES * 3, v * TILE_FEATURES * 3, TILE_FEATURES * 3, 17);

                    float detail = 0.5f * WrappedNoise(u * DETAIL_FEATURES, v * DETAIL_FEATURES, DETAIL_FEATURES, 41)
                        + 0.3f * WrappedNoise(u * DETAIL_FEATURES * 2, v * DETAIL_FEATURES * 2, DETAIL_FEATURES * 2, 59)
                        + 0.2f * WrappedNoise(u * DETAIL_FEATURES * 4, v * DETAIL_FEATURES * 4, DETAIL_FEATURES * 4, 83);

                    texels[y * size + x] = new Color(Encode(weather), Encode(detail), (byte)0, (byte)255);
                }

            return texels;
        }

        private static byte Encode(float value) => (byte)Math.Clamp((int)MathF.Round((value * 0.5f + 0.5f) * 255f), 0, 255);

        /// <summary>
        /// <c>Clouds.fxh</c>'s gradient noise (<see cref="ShaderMath.Noise"/>'s lines), its lattice taken modulo
        /// <paramref name="period"/> before the hash so the field repeats every period cells; <paramref name="offset"/>
        /// moves the lattice so two octaves are not the same cells.
        /// </summary>
        public static float WrappedNoise(float px, float py, int period, int offset)
        {
            float cellX = MathF.Floor(px), cellY = MathF.Floor(py);
            float fx = px - cellX, fy = py - cellY;
            float ux = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float uy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);

            int x0 = Wrap((int)cellX, period), y0 = Wrap((int)cellY, period);
            int x1 = Wrap(x0 + 1, period), y1 = Wrap(y0 + 1, period);

            float a = Dot(ShaderMath.Hash22(x0 + offset, y0 + offset), fx, fy);
            float b = Dot(ShaderMath.Hash22(x1 + offset, y0 + offset), fx - 1f, fy);
            float c = Dot(ShaderMath.Hash22(x0 + offset, y1 + offset), fx, fy - 1f);
            float d = Dot(ShaderMath.Hash22(x1 + offset, y1 + offset), fx - 1f, fy - 1f);

            return MathHelper.Lerp(MathHelper.Lerp(a, b, ux), MathHelper.Lerp(c, d, ux), uy);
        }

        private static int Wrap(int value, int period) => ((value % period) + period) % period;

        private static float Dot(Vector2 gradient, float x, float y) => gradient.X * x + gradient.Y * y;

        /// <summary>The field and its mips, each a box filter of the one before, wrapping as the field does.</summary>
        private static Color[][] MipChain(Color[] top, int size)
        {
            int count = 1;
            for (int s = size; s > 1; s >>= 1) count++;

            var levels = new Color[count][];
            levels[0] = top;
            for (int level = 1, s = size >> 1; level < count; level++, s >>= 1)
            {
                Color[] above = levels[level - 1];
                int aboveSize = s * 2;
                var texels = new Color[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        Color p = above[2 * y * aboveSize + 2 * x], q = above[2 * y * aboveSize + 2 * x + 1];
                        Color r = above[(2 * y + 1) * aboveSize + 2 * x], t = above[(2 * y + 1) * aboveSize + 2 * x + 1];
                        texels[y * s + x] = new Color((p.R + q.R + r.R + t.R + 2) / 4, (p.G + q.G + r.G + t.G + 2) / 4, 0, 255);
                    }

                levels[level] = texels;
            }

            return levels;
        }

        /// <summary>
        /// Draws the deck round the camera over the dome, alpha-blended, with no depth test. The weather's uniforms are
        /// <see cref="CloudField"/>'s, pushed by the host before this (<see cref="CloudField.ApplyTo"/> and
        /// <see cref="CloudField.ApplyDome"/>); the deck itself sets the camera, the matrices and its texture. Leaves the
        /// depth state default and the blend state alpha, as BeginPotatoSceneDraw states them after the dome.
        /// </summary>
        public void Draw(ICamera camera)
        {
            _world?.SetValue(Matrix.CreateScale(DRAW_SCALE) * Matrix.CreateTranslation(camera.Position));
            _view?.SetValue(camera.View);
            _projection?.SetValue(camera.Projection);
            _cameraPosition?.SetValue(camera.Position);
            _texture?.SetValue(_field);

            _device.DepthStencilState = DepthStencilState.None;
            _device.BlendState = BlendState.NonPremultiplied;
            _device.RasterizerState = RasterizerState.CullNone;

            _effect.CurrentTechnique = _technique;
            _technique.Passes[0].Apply();
            _device.SamplerStates[0] = SamplerState.LinearWrap;
            _device.SetVertexBuffer(_vertices);
            _device.Indices = _indices;
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triangles);

            _device.Textures[0] = null;
            _effect.CurrentTechnique = _firstTechnique;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.AlphaBlend;
        }

        public void Dispose()
        {
            _vertices?.Dispose(); _vertices = null;
            _indices?.Dispose(); _indices = null;
            _field?.Dispose(); _field = null;
        }
    }
}
