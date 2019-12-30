using Microsoft.Xna.Framework;

namespace Prazsky.Render
{
	/// <summary>
	/// Představuje parametry světelných efektů pro třídu <see cref="Microsoft.Xna.Framework.Graphics.BasicEffect"/>.
	/// </summary>
	public class BasicEffectParams
	{
		/// <summary>
		/// Parametry světelného efektu.
		/// </summary>
		/// <param name="ambientLightColor">Barva ambientního osvětlení.</param>
		/// <param name="specularColor">Barva odlesku.</param>
		/// <param name="specularPower">Intenzita odlesku.</param>
		/// <param name="emmisiveColor">Barva vyzařovaného světla.</param>
		/// <param name="directionalLight0">Parametry prvního směrového osvětlení.</param>
		/// <param name="directionalLight1">Parametry druhého směrového odvětlení.</param>
		/// <param name="directionalLight2">Parametry třetího směrového osvětlení.</param>
		/// <param name="fog">Parametry efektu mlhy.</param>
		public BasicEffectParams(
				Vector3 ambientLightColor,
				Vector3 specularColor,
				float specularPower,
				Vector3 emmisiveColor,
				DirectionalLightParams directionalLight0 = null,
				DirectionalLightParams directionalLight1 = null,
				DirectionalLightParams directionalLight2 = null,
				FogParams fog = null)
		{
			AmbientLightColor = ambientLightColor;
			SpecularColor = specularColor;
			SpecularPower = specularPower;
			EmmisiveColor = emmisiveColor;
			DirectionalLight0 = directionalLight0;
			DirectionalLight1 = directionalLight1;
			DirectionalLight2 = directionalLight2;
			Fog = fog;
		}

		/// <summary>
		/// Barva ambientního osvětlení.
		/// </summary>
		public Vector3 AmbientLightColor { get; set; }

		/// <summary>
		/// Barva odlesku.
		/// </summary>
		public Vector3 SpecularColor { get; set; }

		/// <summary>
		/// Intenzita odlesku.
		/// </summary>
		public float SpecularPower { get; set; }

		/// <summary>
		/// Barva vyzařovaného světla.
		/// </summary>
		public Vector3 EmmisiveColor { get; set; }

		/// <summary>
		/// Parametry prvního směrového osvětlení.
		/// </summary>
		public DirectionalLightParams DirectionalLight0 { get; set; }

		/// <summary>
		/// Parametry druhého směrového osvětlení.
		/// </summary>
		public DirectionalLightParams DirectionalLight1 { get; set; }

		/// <summary>
		/// Parametry třetího směrového osvětlení.
		/// </summary>
		public DirectionalLightParams DirectionalLight2 { get; set; }

		/// <summary>
		/// Parametry efektu mlhy.
		/// </summary>
		public FogParams Fog { get; set; }
	}

	/// <summary>
	/// Představuje parametry směrového světla.
	/// </summary>
	public class DirectionalLightParams
	{
		/// <summary>
		/// Parametry směrového světla.
		/// </summary>
		/// <param name="direction">Směr světla.</param>
		/// <param name="diffuseColor">Barva základní (rozptylové) složky světla.</param>
		/// <param name="specularColor">Barva odlesku světla.</param>
		public DirectionalLightParams(Vector3 direction, Vector3 diffuseColor, Vector3 specularColor)
		{
			Direction = direction;
			DiffuseColor = diffuseColor;
			SpecularColor = specularColor;
		}

		/// <summary>
		/// Směr světla.
		/// </summary>
		public Vector3 Direction { get; set; }

		/// <summary>
		/// Barva základní (rozptylové) složky světla.
		/// </summary>
		public Vector3 DiffuseColor { get; set; }

		/// <summary>
		/// Barva odlesku světla.
		/// </summary>
		public Vector3 SpecularColor { get; set; }
	}

	/// <summary>
	/// Představuje parametry efektu mlhy.
	/// </summary>
	public class FogParams
	{
		/// <summary>
		/// Parametry efektu mlhy.
		/// </summary>
		/// <param name="fogColor">Barva mlhy.</param>
		/// <param name="fogStart">Začátek mlhy v trojrozměrném světě jako vzdálenost od kamery
		/// (<see cref="Simulation.Camera.ICamera"/>). Objekty před touto vzdáleností jsou plně viditelné.</param>
		/// <param name="fogEnd">Konec mlhy v trojrozměrném světě jako vzdálenost od kamery
		/// (<see cref="Simulation.Camera.ICamera"/>). Objekty za touto vzdáleností jsou zcela neviditelné.</param>
		public FogParams(Vector3 fogColor, float fogStart, float fogEnd)
		{
			FogColor = fogColor;
			FogStart = fogStart;
			FogEnd = fogEnd;
		}

		/// <summary>
		/// Barva mlhy.
		/// </summary>
		public Vector3 FogColor { get; set; }

		/// <summary>
		/// Začátek mlhy v trojrozměrném světě jako vzdálenost od kamery (<see cref="Simulation.Camera.ICamera"/>).
		/// Objekty před touto vzdáleností jsou plně viditelné.
		/// </summary>
		public float FogStart { get; set; }

		/// <summary>
		/// Konec mlhy v trojrozměrném světě jako vzdálenost od kamery (<see cref="Simulation.Camera.ICamera"/>).
		/// Objekty za touto vzdáleností jsou zcela neviditelné.
		/// </summary>
		public float FogEnd { get; set; }
	}
}