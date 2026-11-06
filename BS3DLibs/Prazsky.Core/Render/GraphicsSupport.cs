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
        /// Whether the ceiling's glass bends a copy of the frame (#541): not under MonoGame's Android backend for now, where
        /// the bent pane drew as an opaque, overexposed pink slab at Medium and up (#1063, cause not found yet: the target's
        /// format, the copy's sampling and the screen convention all read right). There the plate draws as at Low, blended
        /// over the frame, which reads correctly.
        /// </summary>
        public static bool CeilingRefraction => !OperatingSystem.IsAndroid();

        /// <summary>
        /// Whether the motion blur runs (#402): not under MonoGame's Android backend for now, where the result page's camera
        /// move smeared screen-tile blocks of the wrong part of the frame across the island at Medium and up (#1063). The
        /// cause is not found. Not the screen position's row order, as first suspected: MonoGame flips rendering into a
        /// target so its rows match DirectX's, and the tile passes draw into targets. Off until it is found; a phone also
        /// gives up a full-screen pass a frame.
        /// </summary>
        public static bool MotionBlur => ForceMotionBlur || !OperatingSystem.IsAndroid();

        /// <summary>Runs the motion blur where <see cref="MotionBlur"/> leaves it off: the Game's <c>motionblur</c>, to look for
        /// #1063's cause on the phone.</summary>
        public static bool ForceMotionBlur { get; set; }
    }
}
