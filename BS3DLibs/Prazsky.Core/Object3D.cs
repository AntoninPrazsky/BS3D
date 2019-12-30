using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Render;
using Prazsky.Core.Camera;

namespace Prazsky.Core
{
	/// <summary>
	/// Abstraktní třída představující trojrozměrný objekt.
	/// </summary>
	public abstract class Object3D
	{
		/// <summary>
		/// Matice transformací, které mají být aplikovány na model před jeho vykreslením.
		/// </summary>
		protected Matrix[] Transformations;

		/// <summary>
		/// Matice světa.
		/// </summary>
		protected Matrix World { get; set; } = Matrix.Identity;

		/// <summary>
		/// Trojrozměrný model.
		/// </summary>
		protected Model Model { get; set; }

		/// <summary>
		/// Pozice trojrozměrného modelu v trojrozměrném světě.
		/// </summary>
		public Vector3 Position { get; set; } = Vector3.Zero;

		/// <summary>
		/// Aktivace nebo deaktivace defaultního tříbodového osvětelní trojrozměrného modelu, které je definováno
		/// frameworkem MonoGame.
		/// Skládá se z hlavního, doplňkového a zadního světla.
		/// https://blogs.msdn.microsoft.com/shawnhar/2007/04/09/the-standard-lighting-rig/
		/// </summary>
		public bool EnableDefaultLighting { set; get; } = true;

		/// <summary>
		/// Aktivace nebo deaktivace výpočtu osvětlení pro každý pixel při vykreslování.
		/// Při deaktivaci nebo pokud grafické zařízení tento způsob nepodporuje, je osvětlení aplikováno na základě
		/// každého vrcholu modelu (vertex lighting).
		/// </summary>
		public bool PreferPerPixelLighting { set; get; } = true;

		/// <summary>
		/// Parametry třídy (<see cref="BasicEffect"/>) frameworku MonoGame pro vykreslování obsažené ve třídě
		/// (<see cref="Render.BasicEffectParams"/>).
		/// Používá se, pokud se nepoužívá defaultní osvětlení aktivované parametrem
		/// (<see cref="EnableDefaultLighting"/>).
		/// </summary>
		public BasicEffectParams BasicEffectParams { get; set; }

		/// <summary>
		/// Opsaná sféra trojrozměrného modelu. Pohybuje se v trojrozměrném světě společně s objektem.
		/// </summary>
		public BoundingSphere BoundingSphere { get; set; }

		/// <summary>
		/// Opsaný kvádr typu AABB (axis-aligned bounding box) trojrozměrného modelu. Nepohybuje se společně s
		/// objektem.
		/// </summary>
		public BoundingBox BoundingBox { get; set; }

		/// <summary>
		/// Vykreslí jeden snímek trojrozměrného modelu na odpovídající pozici a s odpovídající rotací na základě
		/// dvourozměrné fyzikální simulace.
		/// Aplikuje buď vychozí efekt osvětlení, pokud je povolen parametrem <see cref="EnableDefaultLighting"/>, nebo
		/// efekt definovaný parametrem <see cref="BasicEffectParams"/>, nebo žádný efekt.
		/// </summary>
		/// <param name="camera">Kamera, která má být k vykreslení modelu použita.</param>
		public void Draw(ICamera camera)
		{
			ModelRenderer.Render(Model, Transformations, ref camera, World, BasicEffectParams,
					EnableDefaultLighting, PreferPerPixelLighting);
		}

		public void Draw(ICamera camera, Model model)
		{
			ModelRenderer.Render(model, Transformations, ref camera, World, BasicEffectParams,
				EnableDefaultLighting, PreferPerPixelLighting);
		}
	}
}