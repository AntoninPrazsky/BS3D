using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The falling snow the mountain and the aurora share (#205): one static flake buffer, one quad per flake at a
    /// fixed point in the unit cube, animated entirely in <c>Snow.fx</c>'s vertex shader. A service since #580
    /// (<see cref="BackdropServices.Snowfall"/>) so that a scene moved into its own <see cref="Backdrop"/> can
    /// still snow: the buffer is the renderer's to build (sized off the mountain's own flake count), and the
    /// effect is not shared at all — each scene draws through its own clone of <c>Snow.fx</c>, its look pushed
    /// once at load by <see cref="Prepare"/>, which also caches the parameters a frame sets (the by-name indexer
    /// is a linear scan, and <c>Draw</c> used to run it six times a frame).
    /// <para>
    /// <b>Two layers from one buffer since #654</b>: the near box, and the same flakes again in a box
    /// <see cref="SnowConfig.FarLayerScale"/> times larger, which perspective alone turns into the distant veil a
    /// real snowfall lays over a range. The flakes are the ones the references drew — see <c>Snow.fx</c>'s header.
    /// </para>
    /// </summary>
    internal sealed class Snowfall : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;

        private readonly VertexBuffer _snowVertexBuffer;
        private readonly IndexBuffer _snowIndexBuffer;

        //The buffer's own true size — the renderer still sizes it off the mountain's own FlakeCount
        //(the mountain being where the dial lives and gets tuned), but Draw takes a SnowConfig and an
        //effect so a second scene can ask for its own look without a second buffer (#205 — the aurora's
        //gentle snow; its own effect clone since #580). A caller's own FlakeCount is clamped to this, never
        //exceeded, since drawing past the buffer's own capacity would read off the end of it.
        private readonly int _snowFlakeCapacity;

        //Snowfall parameters (flake count/size/colour/opacity, the lens, box, fall speed, wind, sway) live in
        //each caller's own SnowConfig (MountainSceneConfig.Snow, AuroraSceneConfig.Snow) and are pushed into
        //that caller's own clone once at load by Prepare (#580); Draw pushes only the camera, the clock, the
        //pixel's size and each layer's scale.

        /// <summary>
        /// Takes the flake buffer the renderer built through its billboard builder, and the count it was built
        /// at; the snowfall owns (and disposes) both buffers from here.
        /// </summary>
        public Snowfall(GraphicsDevice graphicsDevice, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int flakeCapacity)
        {
            _graphicsDevice = graphicsDevice;
            _snowVertexBuffer = vertexBuffer;
            _snowIndexBuffer = indexBuffer;
            _snowFlakeCapacity = flakeCapacity;
        }

        /// <summary>One scene's clone of <c>Snow.fx</c>, with the parameters a frame sets cached off it.</summary>
        public sealed class Look
        {
            internal readonly Effect Effect;
            internal readonly EffectParameter View, Projection, CameraPosition, CameraRight, CameraUp, Time, Pixel, LayerScale;

            internal Look(Effect effect)
            {
                Effect = effect;
                View = effect.Parameters["View"];
                Projection = effect.Parameters["Projection"];
                CameraPosition = effect.Parameters["CameraPosition"];
                CameraRight = effect.Parameters["CameraRight"];
                CameraUp = effect.Parameters["CameraUp"];
                Time = effect.Parameters["SnowTime"];
                Pixel = effect.Parameters["SnowPixel"];
                LayerScale = effect.Parameters["SnowLayerScale"];
            }
        }

        /// <summary>
        /// Pushes one scene's snowfall look into that scene's own clone of <c>Snow.fx</c>, once at load (#580),
        /// and hands back the clone with the per-frame parameters cached for <see cref="Draw"/>.
        /// </summary>
        public static Look Prepare(Effect effect, SnowConfig config)
        {
            effect.Parameters["SnowBoxSize"].SetValue(config.BoxSize.ToVector3());
            effect.Parameters["SnowFallSpeed"].SetValue(config.FallSpeed);
            effect.Parameters["SnowWind"].SetValue(config.Wind.ToVector2());
            effect.Parameters["SnowSway"].SetValue(config.Sway);
            effect.Parameters["FlakeSize"].SetValue(config.FlakeSize);
            effect.Parameters["SnowNearFade"].SetValue(config.NearFade);
            effect.Parameters["SnowFocus"].SetValue(config.Focus);
            effect.Parameters["SnowAperture"].SetValue(config.Aperture);
            effect.Parameters["SnowShutter"].SetValue(config.Shutter);
            effect.Parameters["SnowColor"].SetValue(config.FlakeColor.ToVector3());
            effect.Parameters["SnowOpacity"].SetValue(config.Opacity);

            return new Look(effect);
        }

        /// <summary>
        /// Draws the falling snow: the static flake buffer animated in the shader, in a box that follows the
        /// camera. Alpha-blended and depth-read (so the terrain and the cluster occlude the flakes behind
        /// them) but writing no depth.
        /// <para>
        /// Shared by the mountain and the aurora (#205), each with its own <see cref="SnowConfig"/> — the
        /// buffer is one for both (built at the mountain's own <see cref="SnowConfig.FlakeCount"/>, since
        /// that is where the dial has always lived), so <paramref name="config"/>'s own count is clamped to
        /// <see cref="_snowFlakeCapacity"/> rather than trusted outright. The look uniforms are already in
        /// <paramref name="look"/>, the scene's own clone, pushed once by <see cref="Prepare"/> (#580); only the
        /// camera, the clock, a pixel's size and each layer's scale are pushed here.
        /// </para>
        /// </summary>
        public void Draw(in SceneFrame frame, Look look, SnowConfig config)
        {
            Matrix inverseView = Matrix.Invert(frame.Camera.View);
            Matrix projection = frame.Camera.Projection;

            look.View.SetValue(frame.Camera.View);
            look.Projection.SetValue(projection);
            look.CameraPosition.SetValue(frame.Camera.Position);
            look.CameraRight.SetValue(inverseView.Right);
            look.CameraUp.SetValue(inverseView.Up);
            look.Time.SetValue(frame.Time);

            //One pixel's world size at unit distance: the projection's vertical scale spans the viewport's
            //height over two units of clip space, so a unit at depth z covers M22 * height / 2 / z pixels
            int height = Math.Max(1, _graphicsDevice.Viewport.Height);
            look.Pixel.SetValue(projection.M22 > 0f ? 2f / (projection.M22 * height) : 0f);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_snowVertexBuffer);
            _graphicsDevice.Indices = _snowIndexBuffer;
            int flakes = Math.Min(config.FlakeCount, _snowFlakeCapacity);

            //The far layer first, so the near flakes blend over it
            if (config.FarLayerScale > 1f)
            {
                look.LayerScale.SetValue(config.FarLayerScale);
                look.Effect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, flakes * 2);
            }

            look.LayerScale.SetValue(1f);
            look.Effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, flakes * 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>Frees the flake buffer. The effects are their scenes' own clones, disposed by their owners.</summary>
        public void Dispose()
        {
            _snowVertexBuffer?.Dispose();
            _snowIndexBuffer?.Dispose();
        }
    }
}
