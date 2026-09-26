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
    /// once at load by <see cref="ApplyParameters"/>.
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

        //Snowfall parameters (flake count/size/shape/colour/opacity, box, fall speed, wind, sway) live in
        //each caller's own SnowConfig (MountainSceneConfig.Snow, AuroraSceneConfig.Snow) and are pushed into
        //that caller's own clone once at load by ApplyParameters (#580); Draw pushes only the camera
        //and the clock.

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

        /// <summary>
        /// Pushes one scene's snowfall look into that scene's own clone of <c>Snow.fx</c>, once at load (#580).
        /// </summary>
        public static void ApplyParameters(Effect effect, SnowConfig config)
        {
            effect.Parameters["SnowBoxSize"].SetValue(config.BoxSize.ToVector3());
            effect.Parameters["SnowFallSpeed"].SetValue(config.FallSpeed);
            effect.Parameters["SnowWind"].SetValue(config.Wind.ToVector2());
            effect.Parameters["SnowSway"].SetValue(config.Sway);
            effect.Parameters["FlakeSize"].SetValue(config.FlakeSize);
            effect.Parameters["SnowSpin"].SetValue(config.Spin);
            effect.Parameters["SnowLobing"].SetValue(config.Lobing);
            effect.Parameters["SnowNearFade"].SetValue(config.NearFade);
            effect.Parameters["SnowTwinkle"].SetValue(config.Twinkle);
            effect.Parameters["SnowColor"].SetValue(config.FlakeColor.ToVector3());
            effect.Parameters["SnowOpacity"].SetValue(config.Opacity);
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
        /// <paramref name="effect"/>, the scene's own clone, pushed once by <see cref="ApplyParameters"/>
        /// (#580); only the camera and the clock are pushed here.
        /// </para>
        /// </summary>
        public void Draw(in SceneFrame frame, Effect effect, SnowConfig config)
        {
            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            effect.Parameters["View"].SetValue(frame.Camera.View);
            effect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            effect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            effect.Parameters["CameraRight"].SetValue(inverseView.Right);
            effect.Parameters["CameraUp"].SetValue(inverseView.Up);
            effect.Parameters["SnowTime"].SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_snowVertexBuffer);
            _graphicsDevice.Indices = _snowIndexBuffer;
            effect.CurrentTechnique.Passes[0].Apply();
            int flakes = Math.Min(config.FlakeCount, _snowFlakeCapacity);
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
