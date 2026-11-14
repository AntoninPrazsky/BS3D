using System;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What the graphics device can do that the desktop takes for granted (#1059), asked once and remembered. The
    /// desktop's renderer on a phone (OpenGL ES, MonoGame Android) is the reason: there a feature can be missing from the
    /// framework rather than from the GPU, and the draw that needs it is left out rather than the run ending.
    /// </summary>
    public static class GraphicsSupport
    {
        private static bool? _volumeTextures;

        /// <summary>
        /// Whether a <see cref="Texture3D"/> can be made: false under MonoGame's Android build, whose OpenGL ES backend
        /// refuses one outright ("OpenGL ES 2.0 doesn't support 3D textures") whatever the GPU's version. The cloud
        /// volumes and the savanna fire's volume march read their noise from one; without it the sky keeps its deck
        /// and the fire its billboard flames. One 1x1x1 texture the first time it is asked, disposed at once.
        /// </summary>
        public static bool VolumeTextures(GraphicsDevice device)
        {
            if (_volumeTextures is bool known) return known;

            try
            {
                using var probe = new Texture3D(device, 1, 1, 1, false, SurfaceFormat.Color);
                _volumeTextures = true;
            }
            catch (NotSupportedException ex)
            {
                _volumeTextures = false;
                Console.WriteLine($"[render] no 3D textures ({ex.Message}): the cloud volumes and the fire's volume are left out");
            }

            return _volumeTextures.Value;
        }

        /// <summary>
        /// <paramref name="wanted"/> multisamples for a render target, or none where the framework cannot resolve one:
        /// MonoGame's Android backend never loads <c>glBlitFramebuffer</c>, so binding the next target after a
        /// multisampled one throws a NullReferenceException inside <c>SetRenderTarget</c> (#1059), whatever OpenGL ES
        /// the phone has. Every multisampled target is asked for through this.
        /// </summary>
        public static int MultiSamples(int wanted) => OperatingSystem.IsAndroid() ? 0 : wanted;

        /// <summary>
        /// Whether a blend can take the smaller or larger of two values (<c>BlendFunction.Min</c>/<c>Max</c>): not under
        /// MonoGame's Android backend, which throws an ArgumentException from <c>GetBlendEquationMode</c> on the first draw
        /// with one (#1061), though OpenGL ES 3 has both. The sun's shadow map keeps its nearest caster by a min blend
        /// on the desktop and by a depth buffer of its own here (<c>SunShadowMap</c>).
        /// </summary>
        public static bool MinMaxBlend => !OperatingSystem.IsAndroid();

        /// <summary>
        /// Whether the city's windows mirror the towers round them (#1065): not under OpenGL ES, where the effect compiles
        /// the reflect technique as the towers' plain program (the march is a dynamic loop over a texture, the desktop's).
        /// There the map it would read is not built at all.
        /// </summary>
        public static bool CityReflections => !OperatingSystem.IsAndroid();

        /// <summary>
        /// Whether a host may lay the island's depth into the frame before the environment (#1098,
        /// <c>SceneRenderer.EnvironmentFollowsIslandDepth</c>): not under MonoGame's Android backend. The pass is taken
        /// only where it was measured to pay, the desktop's GPU. A phone's is a tiler, where the island's second draw
        /// costs its vertex work and binning a second time and the hidden ground may already be rejected tile by tile,
        /// so a phone draws the frame without it until a phone run shows that it pays there.
        /// </summary>
        public static bool IslandDepthFirst => !OperatingSystem.IsAndroid();

        /// <summary>
        /// Whether a host draws the sky dome behind the opaque scene, at the far plane, and shades it at display resolution on
        /// a supersampled frame (#1104, <see cref="SkyPass"/>): not under MonoGame's Android backend. Both were measured on
        /// the desktop's GPU only. A phone's is a tiler that may already reject hidden pixels tile by tile, and it draws at
        /// supersampling 1, so it keeps the dome as the frame's first draw until a phone run shows the order pays there.
        /// </summary>
        public static bool SkyBehindScene => !OperatingSystem.IsAndroid();
    }
}
