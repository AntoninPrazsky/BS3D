using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One orthographic shadow map from the sun (#469): the target the casters are drawn into and the
    /// matrix they are drawn with, fitted every frame round a point of interest — the camera — over a fixed
    /// extent of ground. Infrastructure rather than a savanna feature: the first scene to use it is the
    /// savanna, whose scatter the owner saw floating on an evenly lit floor, and the forest, the meadow and
    /// the beach have the same shape.
    /// <para>
    /// <b>The window is snapped to its own texels.</b> The sun is fixed per dome, so the light-space size of
    /// the fitted box never changes while the camera moves — only its centre does — and a centre that moves
    /// by fractions of a texel re-rasterises every caster's edge differently each frame, which reads as
    /// shadows swimming over the grass. Snapping the centre to whole texels in light space (the same idea
    /// as the terrain grid snapped to its cell) keeps every edge where it was until the camera has moved a
    /// whole texel, which the eye does not see.
    /// </para>
    /// <para>
    /// A 32-bit single-channel colour target carrying the map's clip depth rather than the hardware depth
    /// buffer: MonoGame does not expose a depth buffer for sampling, and a Single target read with a plain
    /// sampler is what <c>Shadows.fxh</c>'s nine-tap PCF reads. Cleared to 1 (the far plane) before each
    /// caster pass, so an empty map shadows nothing.
    /// </para>
    /// </summary>
    public sealed class SunShadowMap : IDisposable
    {
        public RenderTarget2D Target { get; private set; }
        public int Size { get; }

        /// <summary>World → the map's clip space, for the casters and the receivers alike.</summary>
        public Matrix ViewProjection { get; private set; }

        /// <summary>The size of one texel in the map's UV.</summary>
        public float Texel => 1f / Size;

        /// <summary>How many world units the map's depth range spans, so a bias in world units can be
        /// turned into the map's own depth units (<c>units / DepthRange</c>).</summary>
        public float DepthRange { get; private set; } = 1f;

        public SunShadowMap(GraphicsDevice device, int size)
        {
            Size = size;
            Target = new RenderTarget2D(device, size, size, false, SurfaceFormat.Single, DepthFormat.Depth24,
                0, RenderTargetUsage.DiscardContents);
        }

        /// <summary>
        /// Fits the map round <paramref name="centre"/>: a box <paramref name="extent"/> world units square
        /// in XZ and <paramref name="yMin"/>..<paramref name="yMax"/> tall, seen from <paramref name="sunDirection"/>
        /// (the direction <b>towards</b> the sun), its light-space window snapped to whole texels.
        /// </summary>
        public void Fit(Vector3 centre, Vector3 sunDirection, float extent, float yMin, float yMax)
        {
            Vector3 dir = Vector3.Normalize(sunDirection);
            Vector3 up = MathF.Abs(dir.Y) > 0.99f ? Vector3.UnitZ : Vector3.Up;

            //The light looks at the centre from far out along the sun's direction; far enough that the
            //whole box is in front of it whatever the sun's elevation.
            const float EYE_DISTANCE = 2000f;
            Matrix view = Matrix.CreateLookAt(centre + dir * EYE_DISTANCE, centre, up);

            float half = extent * 0.5f;
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new(
                    centre.X + ((i & 1) == 0 ? -half : half),
                    (i & 2) == 0 ? yMin : yMax,
                    centre.Z + ((i & 4) == 0 ? -half : half));
                Vector3 v = Vector3.Transform(corner, view);
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            //Snap the window's centre to whole texels in light space (see the class remarks).
            float width = max.X - min.X, height = max.Y - min.Y;
            float texelX = width / Size, texelY = height / Size;
            float cx = MathF.Round((min.X + max.X) * 0.5f / texelX) * texelX;
            float cy = MathF.Round((min.Y + max.Y) * 0.5f / texelY) * texelY;

            //View space looks down -Z: the nearest point of the box is at the largest Z.
            float zNear = -max.Z, zFar = -min.Z;
            DepthRange = zFar - zNear;

            ViewProjection = view * Matrix.CreateOrthographicOffCenter(
                cx - width * 0.5f, cx + width * 0.5f, cy - height * 0.5f, cy + height * 0.5f, zNear, zFar);
        }

        public void Dispose()
        {
            Target?.Dispose();
            Target = null;
        }
    }
}
