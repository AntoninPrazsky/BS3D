using Microsoft.Xna.Framework;

namespace Prazsky.Core.Camera
{
    /// <summary>
    /// Properties that every camera existing in the three-dimensional world must have.
    /// </summary>
    public interface ICamera
	{
		/// <summary>
		/// View matrix.
		/// </summary>
		Matrix View { get; }

		/// <summary>
		/// Projection matrix.
		/// </summary>
		Matrix Projection { get; }

        /// <summary>
        /// Camera position in a three-dimensional world.
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// The direction the camera is looking.
        /// </summary>
        Vector3 Target { get; }

        /// <summary>
        /// A vector representing the up direction of the camera.
        /// </summary>
        Vector3 Up { get; }

		/// <summary>
		/// Near clipping plane.
		/// </summary>
		float NearPlane { get; set; }

        /// <summary>
        /// Far clipping plane.
        /// </summary>
        float FarPlane { get; set; }
	}
}