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
    /// buffer: MonoGame does not expose a depth buffer for sampling, and a Single target read without a comparison
    /// sampler is what <c>Shadows.fxh</c>'s 4 x 4 PCF reads (four gathers on DirectX). Cleared to 1 (the far plane) before each
    /// caster pass, so an empty map shadows nothing.
    /// </para>
    /// <para>
    /// <b>No depth buffer beside it (#591).</b> The nearest caster is kept by the blend instead of a depth test:
    /// the caster pass draws with <c>BlendFunction.Min</c> (<c>SceneRenderer.ShadowCasterBlend</c>), so each texel
    /// keeps the smallest depth anything wrote to it, in whatever order — exactly what a less-equal test against a
    /// depth buffer kept, and what the red channel holds anyway. The buffer that test needed was a D24S8 the size of
    /// the map, used for nothing else: 64 MB at High's 4096 and 256 MB at Ultra's 8192. ⚠ Except under MonoGame's Android
    /// backend, which has no min blend (<c>GraphicsSupport.MinMaxBlend</c>): there the map has a 24-bit depth buffer and
    /// the casters a depth test, as before #591 (#1061).
    /// </para>
    /// </summary>
    public sealed class SunShadowMap : IDisposable
    {
        public RenderTarget2D Target { get; private set; }
        public int Size { get; }

        //This frame's fit, stored whole by Fit: the matrix, the depth range and the texel's width come out of
        //FitWindow together and go in together, so no one of them can be left a frame behind the others (#1101)
        private SunShadowWindow _window = new(default, 1f, 0f);

        /// <summary>World → the map's clip space, for the casters and the receivers alike.</summary>
        public Matrix ViewProjection => _window.ViewProjection;

        /// <summary>The size of one texel in the map's UV.</summary>
        public float Texel => 1f / Size;

        /// <summary>The width of one texel in world units, across the map's x axis: the fitted window's width over
        /// <see cref="Size"/>, set by <see cref="Fit"/>. The receivers lift their lookup along the normal by a multiple
        /// of it (<c>Shadows.fxh</c>'s <c>ShadowTexelWorld</c>, #642), and it is a constant of the frame, so it is worked
        /// out here once rather than out of the matrix per pixel (#1101).</summary>
        public float TexelWorld => _window.TexelWorld;

        /// <summary>How many world units the map's depth range spans, so a bias in world units can be
        /// turned into the map's own depth units (<c>units / DepthRange</c>).</summary>
        public float DepthRange => _window.DepthRange;

        public SunShadowMap(GraphicsDevice device, int size)
        {
            Size = size;
            //A depth buffer only where no min blend can keep the nearest caster (GraphicsSupport.MinMaxBlend: the phone)
            Target = new RenderTarget2D(device, size, size, false, SurfaceFormat.Single,
                GraphicsSupport.MinMaxBlend ? DepthFormat.None : DepthFormat.Depth24, 0, RenderTargetUsage.DiscardContents);
        }

        /// <summary>
        /// Fits the map round <paramref name="centre"/>: a box <paramref name="extent"/> world units square
        /// in XZ and <paramref name="yMin"/>..<paramref name="yMax"/> tall, seen from <paramref name="sunDirection"/>
        /// (the direction <b>towards</b> the sun), its light-space window snapped to whole texels.
        /// </summary>
        public void Fit(Vector3 centre, Vector3 sunDirection, float extent, float yMin, float yMax) =>
            _window = FitWindow(Size, centre, sunDirection, extent, yMin, yMax);

        /// <summary>
        /// <see cref="Fit"/>'s arithmetic for a map of <paramref name="size"/> texels a side, apart from the target so
        /// it can be checked without a device: the view-projection, the depth range in world units and a texel's width
        /// in world units.
        /// </summary>
        internal static SunShadowWindow FitWindow(int size, Vector3 centre, Vector3 sunDirection, float extent, float yMin, float yMax)
        {
            Vector3 dir = Vector3.Normalize(sunDirection);
            Vector3 up = MathF.Abs(dir.Y) > 0.99f ? Vector3.UnitZ : Vector3.Up;

            //⚠ The light's view is anchored on the WORLD ORIGIN, never on the centre (#566). Anchored on the
            //centre it moved with the camera, so the fitted box sat at the same light-space place every frame
            //and the snap below rounded a constant: the window slid by fractions of a texel whenever the
            //camera moved and the shadows crawled. From a fixed origin the view is a pure rotation of the
            //world, the box's light-space position moves with the camera, and it is THAT position the snap
            //holds to whole texels. Far enough out that every box a scene fits is in front of the eye.
            const float EYE_DISTANCE = 2000f;
            Matrix view = Matrix.CreateLookAt(dir * EYE_DISTANCE, Vector3.Zero, up);

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

            //Snap the window's centre to whole texels in light space (see the class remarks). The size is
            //the same every frame for one sun and one box, since the view is a rotation, so a texel is too.
            float width = max.X - min.X, height = max.Y - min.Y;
            float texelX = width / size, texelY = height / size;
            float cx = MathF.Round((min.X + max.X) * 0.5f / texelX) * texelX;
            float cy = MathF.Round((min.Y + max.Y) * 0.5f / texelY) * texelY;

            //View space looks down -Z: the nearest point of the box is at the largest Z.
            float zNear = -max.Z, zFar = -min.Z;

            //The map's x axis is the one the receivers measure a texel along (a low sun stretches the window's
            //height, not its width, so the two differ)
            return new SunShadowWindow(
                view * Matrix.CreateOrthographicOffCenter(
                    cx - width * 0.5f, cx + width * 0.5f, cy - height * 0.5f, cy + height * 0.5f, zNear, zFar),
                zFar - zNear,
                texelX);
        }

        public void Dispose()
        {
            Target?.Dispose();
            Target = null;
        }
    }

    /// <summary>
    /// One frame's fit of a <see cref="SunShadowMap"/>, as <see cref="SunShadowMap.FitWindow"/> works it out and
    /// <see cref="SunShadowMap.Fit"/> stores it: world → the map's clip space, the depth range in world units, and
    /// one texel's width in world units across the map's x axis.
    /// </summary>
    internal readonly record struct SunShadowWindow(Matrix ViewProjection, float DepthRange, float TexelWorld);
}
