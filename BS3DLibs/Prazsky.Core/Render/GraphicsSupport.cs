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
    }
}
