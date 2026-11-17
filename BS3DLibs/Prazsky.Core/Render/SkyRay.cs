using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The view ray of the full-screen sky passes — the space, Moon, Mars, aurora, grid, dream and cavern skies, which
    /// paint the backdrop per pixel from a direction rather than from geometry. The shaders take it as
    /// <c>ViewRayBasis</c> and read a corner's ray as <c>mul(float4(ndc.xy, 1, 0), ViewRayBasis).xyz</c>.
    /// </summary>
    /// <remarks>
    /// <b>Why a basis and not the inverse view × projection it replaced.</b> Every one of these passes recovered the
    /// ray by carrying the corner out to the far plane through <c>inverse(View × Projection)</c> and subtracting the
    /// camera: a point 2000 units away (#551) divided by a <c>w</c> the 0.05 : 2000 depth range leaves the size of a
    /// rounding error, in single precision on the CPU's inverse and again on the GPU. The owner saw the Earth and the
    /// stars over the Moon shake left and right while the menu camera turned, and the ground under them did not
    /// (that goes through the ordinary transform). Measured in the Testbed, the camera turned in twelve steps of
    /// 0.03° that should each move the sky 0.41 px at 900 lines: the Earth moved <b>0.05 to 0.60 px</b> a step and a
    /// star beside it the same amounts step for step — one error in the direction, a different one at every pose.
    /// The basis is the lens's own axes scaled by the projection's slopes, so a direction is three multiply-adds of
    /// well-conditioned numbers and there is nothing to cancel.
    /// </remarks>
    public static class SkyRay
    {
        /// <summary>
        /// The matrix that takes a clip-space corner <c>(x, y, 1, 0)</c> to the world direction through it: the
        /// camera's right axis over <c>Projection.M11</c>, its up axis over <c>M22</c>, and forward with the
        /// projection's off-centre terms (zero for this project's lenses) folded in.
        /// </summary>
        public static Matrix Basis(ICamera camera) => Basis(camera.View, camera.Projection);

        /// <summary><see cref="Basis(ICamera)"/> from the lens's two matrices.</summary>
        internal static Matrix Basis(Matrix view, Matrix projection)
        {
            //Row vectors: the view's upper 3×3 holds the camera's axes in its COLUMNS
            Vector3 right = new(view.M11, view.M21, view.M31);
            Vector3 up = new(view.M12, view.M22, view.M32);
            Vector3 back = new(view.M13, view.M23, view.M33);

            //A view-space point at depth -1 projects to x = M11·vx − M31 (and likewise y), so the corner at (x, y)
            //looks along vx = (x + M31) / M11, vy = (y + M32) / M22, vz = −1
            Vector3 xAxis = right / projection.M11;
            Vector3 yAxis = up / projection.M22;
            Vector3 zAxis = right * (projection.M31 / projection.M11) + up * (projection.M32 / projection.M22) - back;

            return new Matrix(
                xAxis.X, xAxis.Y, xAxis.Z, 0f,
                yAxis.X, yAxis.Y, yAxis.Z, 0f,
                zAxis.X, zAxis.Y, zAxis.Z, 0f,
                0f, 0f, 0f, 1f);
        }

        /// <summary>
        /// The most a pixel's angular footprint can be anywhere in <paramref name="viewport"/>, as the sky passes take it:
        /// the Frobenius norm of <c>ddx</c> and <c>ddy</c> of the normalised ray (Space.fx's, Mars.fx's <c>pixelAngle</c>).
        /// </summary>
        /// <remarks>
        /// The ray through NDC <c>(x, y)</c> is <c>x·X + y·Y + Z</c> (<see cref="Basis(Matrix, Matrix)"/>), whose part along the
        /// lens's back axis is always the unit <c>-back</c>, so the ray is never shorter than 1. The normalised ray therefore moves
        /// at most <c>|X| = 1 / M11</c> per unit of NDC x, and a pixel is <c>2 / width</c> of it: one pixel's step across is at
        /// most <c>2 / (width·M11)</c>, a step down <c>2 / (height·M22)</c>. The bound is the footprint at the centre of the
        /// frame; off the axis the ray turns more slowly.
        /// </remarks>
        public static float MaxPixelAngle(Matrix projection, Viewport viewport)
        {
            float across = 2f / (MathF.Max(viewport.Width, 1) * MathF.Abs(projection.M11));
            float down = 2f / (MathF.Max(viewport.Height, 1) * MathF.Abs(projection.M22));
            return MathF.Sqrt(across * across + down * down);
        }

        /// <summary>
        /// The pixels of <paramref name="viewport"/>, in the render target's own coordinates (what
        /// <see cref="GraphicsDevice.ScissorRectangle"/> takes), whose rays can fall within <paramref name="halfAngle"/> of
        /// <paramref name="axis"/>: <see cref="Basis(Matrix, Matrix)"/> run backwards, so a full-screen sky pass that draws
        /// something small (Mars's moons, #1138) can be scissored to it. Empty where the cone misses the frame, behind the lens
        /// or beside it, by more than its polygon stands past it (below); the whole viewport only for a cone a half-turn or more
        /// across.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The cone's section at <c>cos(halfAngle)</c> along its axis is a circle; the <see cref="RIM_POINTS"/>-gon round it (its
        /// corners <c>1 / cos(π / RIM_POINTS)</c> further out) holds it. The polygon is cut to the frustum's four sides first,
        /// each a plane through the eye (the rays whose screen <c>x</c> or <c>y</c> is ±1): what is left is every ray of the
        /// polygon's cone that reaches the frame, all of it in front of the lens, since the four sides meet behind it only at
        /// the eye. Nothing is left of a polygon whose cone misses the frame, wherever it points, the plane through the eye
        /// square to the lens's axis included, where an outline on the screen would have no bound. The projection of what is
        /// left is projective on a region wholly in front of the lens, so it keeps the cut polygon convex with the circle's part
        /// inside it, and the box round its corners' projections holds every pixel of the cone. Rounded outwards to whole
        /// pixels and widened by <see cref="MARGIN_PIXELS"/>, for the rounding of the corners' sums against the shader's own.
        /// </para>
        /// <para>
        /// A pixel the box leaves out is one whose ray is outside the cone, so a pass whose program writes nothing there (an
        /// alpha of 0 under a premultiplied blend leaves the target as it was) draws the same picture scissored to the box. A
        /// pixel inside it is shaded exactly as before: the quad's corners and so its interpolants are the full screen's, and a
        /// 2×2 block cut by the scissor still has its derivatives from the lanes the scissor turned into helpers.
        /// </para>
        /// </remarks>
        public static Rectangle PixelsWithin(Vector3 axis, float halfAngle, Matrix view, Matrix projection, Viewport viewport)
        {
            Rectangle whole = new(viewport.X, viewport.Y, viewport.Width, viewport.Height);
            if (halfAngle >= MathHelper.PiOver2) return whole;

            Vector3 right = new(view.M11, view.M21, view.M31);
            Vector3 up = new(view.M12, view.M22, view.M32);
            Vector3 forward = -new Vector3(view.M13, view.M23, view.M33);

            //Two axes across the cone, and its section's circle out to the polygon's corners
            axis = Vector3.Normalize(axis);
            Vector3 across = Vector3.Normalize(Vector3.Cross(MathF.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY, axis));
            Vector3 along = Vector3.Cross(axis, across);
            Vector3 centre = axis * MathF.Cos(halfAngle);
            float corner = MathF.Sin(MathF.Max(halfAngle, 0f)) / MathF.Cos(MathF.PI / RIM_POINTS);

            //Each cut adds at most one corner to a convex polygon: RIM_POINTS and one a side
            Span<Vector3> polygon = stackalloc Vector3[RIM_POINTS + 4];
            Span<Vector3> cut = stackalloc Vector3[RIM_POINTS + 4];
            for (int i = 0; i < RIM_POINTS; i++) polygon[i] = centre + (across * RIM[i].X + along * RIM[i].Y) * corner;

            //The frustum's sides, each normal pointing in. The ray at screen x looks along vx = (x + M31) / M11 at a depth of 1
            //(Basis), so the right side is vx = (1 + M31) / M11 · depth, and a ray is inside it while that less vx is positive
            float rightSlope = (1f + projection.M31) / projection.M11, leftSlope = (projection.M31 - 1f) / projection.M11;
            float topSlope = (1f + projection.M32) / projection.M22, bottomSlope = (projection.M32 - 1f) / projection.M22;
            int count = Cut(polygon, RIM_POINTS, cut, forward * rightSlope - right);
            count = Cut(cut, count, polygon, right - forward * leftSlope);
            count = Cut(polygon, count, cut, forward * topSlope - up);
            count = Cut(cut, count, polygon, up - forward * bottomSlope);
            if (count < 0) return whole;
            if (count == 0) return Rectangle.Empty;

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 ray = polygon[i];
                float depth = Vector3.Dot(ray, forward);

                //Every corner left is in front of the lens; only rounding, for a cone within a hair of a half-turn, puts one
                //on the eye's plane
                if (depth <= 0f) return whole;

                //Basis's own mapping turned round: x = M11·vx − M31 at a depth of 1
                float x = projection.M11 * Vector3.Dot(ray, right) / depth - projection.M31;
                float y = projection.M22 * Vector3.Dot(ray, up) / depth - projection.M32;
                minX = MathF.Min(minX, x);
                maxX = MathF.Max(maxX, x);
                minY = MathF.Min(minY, y);
                maxY = MathF.Max(maxY, y);
            }

            //Clamped to the frame first: a corner on a side can round a hair past it
            minX = Math.Clamp(minX, -1f, 1f);
            maxX = Math.Clamp(maxX, -1f, 1f);
            minY = Math.Clamp(minY, -1f, 1f);
            maxY = Math.Clamp(maxY, -1f, 1f);
            int left = (int)MathF.Floor(viewport.X + (minX * 0.5f + 0.5f) * viewport.Width) - MARGIN_PIXELS;
            int rightEdge = (int)MathF.Ceiling(viewport.X + (maxX * 0.5f + 0.5f) * viewport.Width) + MARGIN_PIXELS;
            int top = (int)MathF.Floor(viewport.Y + (0.5f - maxY * 0.5f) * viewport.Height) - MARGIN_PIXELS;
            int bottom = (int)MathF.Ceiling(viewport.Y + (0.5f - minY * 0.5f) * viewport.Height) + MARGIN_PIXELS;
            return Rectangle.Intersect(new Rectangle(left, top, rightEdge - left, bottom - top), whole);
        }

        //The first `count` corners of the convex polygon `from`, cut to the side of the plane through the eye that `inward`
        //points to, into `to` (one pass of Sutherland and Hodgman's clipping); how many corners are left, or -1 where rounding
        //on a corner a hair off the plane gave more than `to` holds (a convex polygon gains at most one a cut), passed on
        private static int Cut(ReadOnlySpan<Vector3> from, int count, Span<Vector3> to, Vector3 inward)
        {
            if (count <= 0) return count;

            int kept = 0;
            Vector3 previous = from[count - 1];
            float previousSide = Vector3.Dot(previous, inward);
            for (int i = 0; i < count; i++)
            {
                Vector3 current = from[i];
                float side = Vector3.Dot(current, inward);
                if ((side >= 0f) != (previousSide >= 0f))
                {
                    if (kept == to.Length) return -1;
                    to[kept++] = Vector3.Lerp(previous, current, previousSide / (previousSide - side));
                }

                if (side >= 0f)
                {
                    if (kept == to.Length) return -1;
                    to[kept++] = current;
                }

                previous = current;
                previousSide = side;
            }

            return kept;
        }

        /// <summary>The corners of the polygon round a cone's section in <see cref="PixelsWithin"/>.</summary>
        internal const int RIM_POINTS = 16;

        /// <summary>Pixels <see cref="PixelsWithin"/> adds on every side, past the box's rounding outwards.</summary>
        internal const int MARGIN_PIXELS = 1;

        //The corners' cosine and sine round the rim, worked out once
        private static readonly Vector2[] RIM = BuildRim();

        private static Vector2[] BuildRim()
        {
            Vector2[] rim = new Vector2[RIM_POINTS];
            for (int i = 0; i < RIM_POINTS; i++)
            {
                float angle = i * MathHelper.TwoPi / RIM_POINTS;
                rim[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            return rim;
        }
    }
}
