using FontStashSharp.Interfaces;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// Draws FontStashSharp text with a colour that <b>changes across each glyph</b> (#926): a pastel rainbow as the letters' fill, which
    /// Myra's label cannot give (it draws a string in one colour) and which <c>SpriteBatch</c> cannot either (it takes one colour a
    /// sprite). It is the font's own <see cref="IFontStashRenderer2"/>: FontStashSharp hands it each glyph as a quad of four vertices, and it
    /// <b>recolours every vertex</b> from where the vertex is, so the colour is a function of position and a glyph is shaded across itself.
    /// <para>
    /// No shader is added: the quads are drawn with MonoGame's own <c>BasicEffect</c> (a texture and a vertex colour), which every
    /// backend carries, so the Potato path and GamePi draw it as they draw everything else. A frame costs two draws, the outline and the
    /// fill; the vertex and index arrays are held and reused, so it allocates nothing.
    /// </para>
    /// <para>
    /// <b>The glyph atlas is premultiplied</b> (the same atlas <c>SpriteBatch</c> draws, under <c>BlendState.AlphaBlend</c>), so the vertex
    /// colours written here are too: the colour times its own alpha, with the alpha the text was asked for taken from the colour handed to
    /// <c>DrawText</c> (<c>Color.White * opacity</c>).
    /// </para>
    /// </summary>
    internal sealed class GradientTextRenderer : IFontStashRenderer2
    {
        //The banner's thirteen draws of eight letters - twelve of the outline's ring (#986) and the fill - with room for a longer word: a
        //flush is one draw call, so the limit is only how many quads wait, and a word that outgrows it costs a draw call more, not a fault
        private const int QUADS = 128;

        private static readonly short[] _indices = BuildIndices();

        private readonly GraphicsDevice _device;
        private readonly BasicEffect _effect;
        private EffectPass _pass;
        private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[QUADS * 4];

        private Texture2D _texture;
        private int _quads;

        //The viewport the projection was last built for: rebuilt only when the frame changes size
        private int _width, _height;

        //How the next quads are coloured: a flat colour, or the rainbow over the box the word stands in
        private bool _flat;
        private Color _flatColour;
        private float _left, _top, _boxWidth, _boxHeight, _clock;

        public GradientTextRenderer(GraphicsDevice device)
        {
            _device = device;
            _effect = new BasicEffect(device)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false,
                FogEnabled = false,
            };

            //The effect picks the technique its flags ask for (a texture and a vertex colour) when it is first applied, not when they are set:
            //a pass taken before that belongs to the plain technique, and drew every glyph as a flat white box. Applied once here, then taken.
            _effect.CurrentTechnique.Passes[0].Apply();
            _pass = _effect.CurrentTechnique.Passes[0];
        }

        public GraphicsDevice GraphicsDevice => _device;

        /// <summary>Whether this renderer can still draw: false once its device has been disposed.</summary>
        public bool IsUsable => !_device.IsDisposed && !_effect.IsDisposed;

        /// <summary>
        /// Starts a run of draws: the states a text quad wants (premultiplied blend, no depth, no culling, a linear sampler for the glyphs'
        /// minification) and a projection that maps one unit to one pixel of the viewport, as <c>SpriteBatch</c>'s does.
        /// </summary>
        public void Begin()
        {
            Viewport viewport = _device.Viewport;

            if (viewport.Width != _width || viewport.Height != _height)
            {
                _width = viewport.Width;
                _height = viewport.Height;
                _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, _width, _height, 0f, 0f, 1f);
            }

            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.LinearClamp;
        }

        /// <summary>Colours what follows with one colour, <paramref name="colour"/> premultiplied, times each text's own opacity.</summary>
        public void Flat(Color colour)
        {
            _flat = true;
            _flatColour = colour;
        }

        /// <summary>
        /// Colours what follows with the pastel rainbow over the box (<paramref name="left"/>, <paramref name="top"/>, <paramref name="width"/>,
        /// <paramref name="height"/>) in pixels, as <see cref="NewBestCelebration.Fill"/> has it at <paramref name="clock"/>: the left of the box
        /// is the start of the rainbow and the right its end.
        /// </summary>
        public void Rainbow(float left, float top, float width, float height, float clock)
        {
            _flat = false;
            _left = left;
            _top = top;
            _boxWidth = MathF.Max(1f, width);
            _boxHeight = MathF.Max(1f, height);
            _clock = clock;
        }

        /// <summary>Draws whatever is waiting: call after the last <c>DrawText</c> of a run.</summary>
        public void End() => Flush();

        public void DrawQuad(Texture2D texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight,
            ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight)
        {
            if (!ReferenceEquals(texture, _texture) || _quads == QUADS)
            {
                Flush();
                _texture = texture;
            }

            int at = _quads * 4;
            _vertices[at] = Painted(topLeft);
            _vertices[at + 1] = Painted(topRight);
            _vertices[at + 2] = Painted(bottomLeft);
            _vertices[at + 3] = Painted(bottomRight);
            _quads++;
        }

        private VertexPositionColorTexture Painted(VertexPositionColorTexture vertex)
        {
            //The opacity the caller asked the text for rides in the alpha of the colour it handed DrawText
            float alpha = vertex.Color.A / 255f;

            if (_flat)
            {
                vertex.Color = _flatColour * alpha;
                return vertex;
            }

            Vector3 rgb = NewBestCelebration.Fill((vertex.Position.X - _left) / _boxWidth, (vertex.Position.Y - _top) / _boxHeight, _clock);
            vertex.Color = new Color(rgb.X * alpha, rgb.Y * alpha, rgb.Z * alpha, alpha);
            return vertex;
        }

        private void Flush()
        {
            if (_quads == 0 || _texture == null) return;

            _effect.Texture = _texture;
            _pass.Apply();
            _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, _quads * 4, _indices, 0, _quads * 2);
            _quads = 0;
        }

        //Two triangles a quad, over the vertices in the order DrawQuad is handed them: top left, top right, bottom left, bottom right
        private static short[] BuildIndices()
        {
            short[] indices = new short[QUADS * 6];

            for (int quad = 0; quad < QUADS; quad++)
            {
                short corner = (short)(quad * 4);
                int at = quad * 6;
                indices[at] = corner;
                indices[at + 1] = (short)(corner + 1);
                indices[at + 2] = (short)(corner + 2);
                indices[at + 3] = (short)(corner + 2);
                indices[at + 4] = (short)(corner + 1);
                indices[at + 5] = (short)(corner + 3);
            }

            return indices;
        }
    }
}
