using Microsoft.Xna.Framework;
using Prazsky.Core.Camera;

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
        public static Matrix Basis(ICamera camera)
        {
            Matrix view = camera.View;
            Matrix projection = camera.Projection;

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
    }
}
