using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace MapEditor.GUI
{
    /// <summary>
    /// A small axis cross drawn in a corner of the window, showing the world X/Y/Z axes as the camera
    /// currently sees them (X red, Y green, Z blue, the usual convention) so the user can tell which way
    /// they are looking while building a map. It is a screen overlay, not part of the scene: it lives in its
    /// own little square viewport, reflects only the camera's orientation (not its position or the world),
    /// and is drawn after the tonemap resolve in display space, like the selector and the text overlay, so
    /// its colours stay exactly as authored.
    /// </summary>
    internal class AxisGizmo : IDisposable
    {
        /// <summary>Pixel size of the square the gizmo occupies, and its inset from the window corner.</summary>
        private const int SIZE = 120;
        private const int MARGIN = 14;

        /// <summary>How far the bright positive arm and the dim negative stub of each axis reach.</summary>
        private const float POSITIVE_LENGTH = 1.0f;
        private const float NEGATIVE_LENGTH = 0.4f;

        /// <summary>Where the letter sits along its axis, a little past the bright arm's tip.</summary>
        private const float LABEL_DISTANCE = 1.34f;

        /// <summary>Target on-screen height of a label, in pixels; the font is scaled to hit it.</summary>
        private const float LABEL_PIXELS = 18f;

        private static readonly Vector3 X_COLOR = new(0.92f, 0.29f, 0.24f);
        private static readonly Vector3 Y_COLOR = new(0.46f, 0.80f, 0.28f);
        private static readonly Vector3 Z_COLOR = new(0.30f, 0.50f, 0.98f);

        //The dim end of each axis, so the cross reads as a cross while the bright end still marks the positive
        //direction without needing to read the letter
        private const float NEGATIVE_DIM = 0.4f;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;
        private readonly VertexPositionColor[] _lines;

        public AxisGizmo(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _font = content.Load<SpriteFont>("Fonts/segoeui");

            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = false
            };

            //Six lines from the origin: a bright positive arm and a dim negative stub per axis. Negatives
            //first, so the bright arms draw over them where they meet at the origin.
            _lines = new[]
            {
                Vertex(Vector3.Zero, X_COLOR * NEGATIVE_DIM), Vertex(-Vector3.UnitX * NEGATIVE_LENGTH, X_COLOR * NEGATIVE_DIM),
                Vertex(Vector3.Zero, Y_COLOR * NEGATIVE_DIM), Vertex(-Vector3.UnitY * NEGATIVE_LENGTH, Y_COLOR * NEGATIVE_DIM),
                Vertex(Vector3.Zero, Z_COLOR * NEGATIVE_DIM), Vertex(-Vector3.UnitZ * NEGATIVE_LENGTH, Z_COLOR * NEGATIVE_DIM),

                Vertex(Vector3.Zero, X_COLOR), Vertex(Vector3.UnitX * POSITIVE_LENGTH, X_COLOR),
                Vertex(Vector3.Zero, Y_COLOR), Vertex(Vector3.UnitY * POSITIVE_LENGTH, Y_COLOR),
                Vertex(Vector3.Zero, Z_COLOR), Vertex(Vector3.UnitZ * POSITIVE_LENGTH, Z_COLOR)
            };
        }

        private static VertexPositionColor Vertex(Vector3 position, Vector3 color) =>
            new(position, new Color(color));

        public void Draw(ICamera camera)
        {
            Viewport full = _graphicsDevice.Viewport;

            //Only the camera's orientation matters — strip the translation so the cross sits at a fixed point
            //in front of the eye and merely turns as the camera turns
            Matrix rotation = camera.View;
            rotation.Translation = Vector3.Zero;

            Matrix view = rotation * Matrix.CreateTranslation(0f, 0f, -3f);

            //Orthographic and square (the viewport is square), so the axes do not foreshorten and the cross
            //keeps its shape whatever direction it points
            Matrix projection = Matrix.CreateOrthographic(3f, 3f, 0.1f, 6f);

            //A little square in the bottom-left corner, clear of the top-left help text
            Viewport corner = new(MARGIN, full.Height - SIZE - MARGIN, SIZE, SIZE);
            _graphicsDevice.Viewport = corner;

            _effect.World = Matrix.Identity;
            _effect.View = view;
            _effect.Projection = projection;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _lines, 0, _lines.Length / 2);
            }

            //Project the label anchors while the corner viewport is still bound, so the letters land on the
            //axis tips; the projection returns absolute back-buffer pixels, so they are drawn once the full
            //viewport is restored
            Vector3 xLabel = corner.Project(Vector3.UnitX * LABEL_DISTANCE, projection, view, Matrix.Identity);
            Vector3 yLabel = corner.Project(Vector3.UnitY * LABEL_DISTANCE, projection, view, Matrix.Identity);
            Vector3 zLabel = corner.Project(Vector3.UnitZ * LABEL_DISTANCE, projection, view, Matrix.Identity);

            _graphicsDevice.Viewport = full;

            float scale = LABEL_PIXELS / _font.MeasureString("X").Y;

            _spriteBatch.Begin();
            DrawLabel("X", xLabel, X_COLOR, scale);
            DrawLabel("Y", yLabel, Y_COLOR, scale);
            DrawLabel("Z", zLabel, Z_COLOR, scale);
            _spriteBatch.End();
        }

        private void DrawLabel(string text, Vector3 projected, Vector3 color, float scale)
        {
            Vector2 half = _font.MeasureString(text) * scale * 0.5f;
            Vector2 position = new(projected.X - half.X, projected.Y - half.Y);

            _spriteBatch.DrawString(_font, text, position, new Color(color), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        public void Dispose()
        {
            _effect?.Dispose();
            _spriteBatch?.Dispose();
        }
    }
}
