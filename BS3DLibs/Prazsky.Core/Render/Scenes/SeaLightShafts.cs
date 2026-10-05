using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The sun's light coming down through the sea's surface, seen from under the water (#760): the rays that light
    /// every one of the references (<c>C:\Users\panrd\AI\sd\out\760-klein</c>: fanning down round a floating platform's
    /// dark underside, the school in them). Without them the water under the island was one flat murk, the underwater
    /// post's single tint (<c>Tonemap.fx</c>), and the shot under it read as a dull blue room.
    /// <para>
    /// A static buffer of quads, one a shaft, placed, turned and shimmered in <c>LightShafts.fx</c> off a hash of the
    /// shaft's index: one draw, no CPU work per shaft. They stand round the island and not under it, where the stone
    /// shades the water. Drawn only with the lens under the water, like the school (<see cref="FishSchool"/>).
    /// </para>
    /// </summary>
    internal sealed class SeaLightShafts : IDisposable
    {
        //How many, where (an annulus round the island's axis: from just outside its rim, 26, out to where the water's
        //own haze takes them) and how big
        private const int SHAFT_COUNT = 40;
        private const float SHAFT_RADIUS_IN = 28f;
        private const float SHAFT_RADIUS_OUT = 110f;
        private const float SHAFT_LENGTH = 70f;
        private const float SHAFT_WIDTH_MIN = 3f;
        private const float SHAFT_WIDTH_MAX = 9f;

        //How much the water straightens the sun's slant: the horizontal part of the light's path, against the
        //vertical, is this share of what it was in the air (refraction into water at about 1.33, rounded down so a
        //low sun still reads as light coming DOWN)
        private const float REFRACTION_SLANT = 0.5f;

        //Their light: this share of the sun's, cooled a little, as the water does
        private const float SHAFT_STRENGTH = 0.6f;
        private static readonly Vector3 SHAFT_TINT = new(0.8f, 0.95f, 1f);

        //How near the lens a shaft is gone, so one through the lens is never a wall of light
        private const float NEAR_FADE = 8f;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly VertexBuffer _vertices;
        private readonly IndexBuffer _indices;

        private readonly EffectParameter _view, _projection, _time, _cameraPosition, _surfaceY, _direction, _color;

        public SeaLightShafts(GraphicsDevice graphicsDevice, ContentManager content, BackdropServices services)
        {
            _graphicsDevice = graphicsDevice;
            _effect = content.Load<Effect>("Shaders/LightShafts");

            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _time = _effect.Parameters["Time"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _surfaceY = _effect.Parameters["SurfaceY"];
            _direction = _effect.Parameters["ShaftDirection"];
            _color = _effect.Parameters["ShaftColor"];

            _effect.Parameters["ShaftRadii"].SetValue(new Vector2(SHAFT_RADIUS_IN, SHAFT_RADIUS_OUT));
            _effect.Parameters["ShaftLength"].SetValue(SHAFT_LENGTH);
            _effect.Parameters["ShaftWidths"].SetValue(new Vector2(SHAFT_WIDTH_MIN, SHAFT_WIDTH_MAX));
            _effect.Parameters["NearFade"].SetValue(NEAR_FADE);

            //Four corners a shaft, in the order the shared quad index buffer expects (the storm's bolts' order)
            VertexPosition[] corners = new VertexPosition[SHAFT_COUNT * 4];
            for (int s = 0; s < SHAFT_COUNT; s++)
            {
                corners[s * 4] = new VertexPosition(new Vector3(s, -1f, 0f));
                corners[s * 4 + 1] = new VertexPosition(new Vector3(s, 1f, 0f));
                corners[s * 4 + 2] = new VertexPosition(new Vector3(s, -1f, 1f));
                corners[s * 4 + 3] = new VertexPosition(new Vector3(s, 1f, 1f));
            }

            _vertices = new VertexBuffer(graphicsDevice, VertexPosition.VertexDeclaration, corners.Length, BufferUsage.WriteOnly);
            _vertices.SetData(corners);
            _indices = services.BuildQuadIndexBuffer(SHAFT_COUNT);
        }

        /// <summary>Draws the shafts hanging from a surface at <paramref name="surfaceY"/>, additive and depth-read: drawn
        /// with the overlays, after everything opaque, so the island hides the shafts behind it and not the ones in front.</summary>
        public void Draw(in SceneFrame frame, float surfaceY)
        {
            //The sun's light, bent down by the water; a sun under the horizon puts none down
            Vector3 sun = frame.SunDirection;
            float day = MathHelper.Clamp(sun.Y * 3f, 0f, 1f);
            if (day <= 0f) return;

            Vector3 down = Vector3.Normalize(new Vector3(-sun.X * REFRACTION_SLANT, -1f, -sun.Z * REFRACTION_SLANT));

            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _time.SetValue(frame.Time);
            _cameraPosition.SetValue(frame.Camera.Position);
            _surfaceY.SetValue(surfaceY);
            _direction.SetValue(down);
            _color.SetValue(frame.SunColor * SHAFT_TINT * (SHAFT_STRENGTH * day));

            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_vertices);
            _graphicsDevice.Indices = _indices;
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, SHAFT_COUNT * 2);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        public void Dispose()
        {
            _vertices?.Dispose();
            _indices?.Dispose();
        }
    }
}
