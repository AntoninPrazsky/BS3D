using Microsoft.Xna.Framework;

namespace Prazsky.Core.Camera
{
	/// <summary>
	/// Vlastnosti, které musí mít každá kamera existující v trojrozměrném světě.
	/// </summary>
	public interface ICamera
	{
		/// <summary>
		/// Matice pohledu.
		/// </summary>
		Matrix View { get; }

		/// <summary>
		/// Matice projekce.
		/// </summary>
		Matrix Projection { get; }

		/// <summary>
		/// Pozice kamery v trojrozměrném světě.
		/// </summary>
		Vector3 Position { get; }

		/// <summary>
		/// Směr, kterým se kamera dívá.
		/// </summary>
		Vector3 Target { get; }

		/// <summary>
		/// Vektor představující směr nahoru kamery.
		/// </summary>
		Vector3 Up { get; }

		/// <summary>
		/// Přední ořezová plocha.
		/// </summary>
		float NearPlane { get; set; }

		/// <summary>
		/// Zadní ořezová plocha.
		/// </summary>
		float FarPlane { get; set; }
	}
}