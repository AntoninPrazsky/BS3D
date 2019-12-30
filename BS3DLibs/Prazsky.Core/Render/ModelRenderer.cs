using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;

namespace Prazsky.Render
{
	/// <summary>
	/// Slouží pro vykreslování trojrozměrného modelu.
	/// </summary>
	public static class ModelRenderer
	{
		/// <summary>
		/// Vykreslí trojrozměrný model na základě poskytnutých parametrů.
		/// </summary>
		/// <param name="model">Trojrozměrný model k vykreslení.</param>
		/// <param name="transformations">Transformace modelu.</param>
		/// <param name="camera">Kamera, která se na výsledné vykreslení modelu dívá.</param>
		/// <param name="world">Matice světa, která se má použít k vykreslení.</param>
		/// <param name="basicEffectParams">Parametry pro třídu <see cref="BasicEffect"/>.</param>
		/// <param name="enableDefaultLighting">Použití tříbodového osvětelní trojrozměrného modelu, které je
		/// definováno frameworkem MonoGame.</param>
		/// <param name="preferPerPixelLighting">Výpočet osvětlení pro každý pixel, pokud je toto podporováno grafickým
		/// hardwarem.</param>
		public static void Render(
				Model model,
				Matrix[] transformations,
				ref ICamera camera,
				Matrix world,
				BasicEffectParams basicEffectParams,
				bool enableDefaultLighting,
				bool preferPerPixelLighting)
		{
			foreach (ModelMesh mesh in model.Meshes)
			{
				foreach (BasicEffect effect in mesh.Effects)
				{
					effect.PreferPerPixelLighting = preferPerPixelLighting;

					if (enableDefaultLighting)
					{
						effect.EnableDefaultLighting();
					}

					if (basicEffectParams != null)
					{
						effect.LightingEnabled = true;

						#region Aplikace světelných parametrů, pokud jsou nastaveny

						if (basicEffectParams.AmbientLightColor != null)
							effect.AmbientLightColor = basicEffectParams.AmbientLightColor;
						if (basicEffectParams.SpecularColor != null)
						{
							effect.SpecularColor = basicEffectParams.SpecularColor;
							effect.SpecularPower = basicEffectParams.SpecularPower;
						}
						if (basicEffectParams.EmmisiveColor != null)
							effect.EmissiveColor = basicEffectParams.EmmisiveColor;

						if (basicEffectParams.DirectionalLight0 != null)
						{
							effect.DirectionalLight0.Direction =
									basicEffectParams.DirectionalLight0.Direction;
							effect.DirectionalLight0.DiffuseColor =
									basicEffectParams.DirectionalLight0.DiffuseColor;
							effect.DirectionalLight0.SpecularColor =
									basicEffectParams.DirectionalLight0.SpecularColor;
							effect.DirectionalLight0.Enabled = true;
						}

						if (basicEffectParams.DirectionalLight1 != null)
						{
							effect.DirectionalLight1.Direction =
									basicEffectParams.DirectionalLight1.Direction;
							effect.DirectionalLight1.DiffuseColor =
									basicEffectParams.DirectionalLight1.DiffuseColor;
							effect.DirectionalLight1.SpecularColor =
									basicEffectParams.DirectionalLight1.SpecularColor;
							effect.DirectionalLight1.Enabled = true;
						}

						if (basicEffectParams.DirectionalLight2 != null)
						{
							effect.DirectionalLight2.Direction =
									basicEffectParams.DirectionalLight2.Direction;
							effect.DirectionalLight2.DiffuseColor =
									basicEffectParams.DirectionalLight2.DiffuseColor;
							effect.DirectionalLight2.SpecularColor =
									basicEffectParams.DirectionalLight2.SpecularColor;
							effect.DirectionalLight2.Enabled = true;
						}

						#endregion Aplikace světelných parametrů, pokud jsou nastaveny

						#region Aplikace efektu mlhy, pokud je nastaven

						if (basicEffectParams.Fog != null)
						{
							effect.FogColor = basicEffectParams.Fog.FogColor;
							effect.FogStart = basicEffectParams.Fog.FogStart;
							effect.FogEnd = basicEffectParams.Fog.FogEnd;
							effect.FogEnabled = true;
						}

						#endregion Aplikace efektu mlhy, pokud je nastaven
					}

					effect.World = transformations[mesh.ParentBone.Index] * world;
					effect.View = camera.View;
					effect.Projection = camera.Projection;
				}
				mesh.Draw();
			}
		}
	}
}