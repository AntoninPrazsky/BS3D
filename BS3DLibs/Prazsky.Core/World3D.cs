using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Core
{
	public class World3D
	{
		private BoundingFrustum _boundingFrustum;
		private List<Backdrop3D> _backdrop3Ds = new List<Backdrop3D>();

		/// <summary>
		/// Trojrozměrná kamera, která trojrozměrný svět pozoruje.
		/// </summary>
		public ICamera Camera3D;

		/// <summary>
		/// Konstruktor trojrozměrného světa.
		/// </summary>
		/// <param name="camera">Výchozí kamera, která trojrozměrný svět pozoruje.</param>
		public World3D(ICamera camera)
		{
			Camera3D = camera;
		}

		/// <param name="gameTime">Herní čas.</param>
		public void Update(GameTime gameTime)
		{
			throw new NotImplementedException(); 
		}

		/// <summary>
		/// Vykreslí jeden snímek trojrozměrného světa.
		/// </summary>
		public void Draw()
		{
			if (_backdrop3Ds.Count > 0)
				_boundingFrustum = new BoundingFrustum(Camera3D.View * Camera3D.Projection);

			if (_backdrop3Ds.Count > 0)
				for (int i = 0; i < _backdrop3Ds.Count; i++)
					if (_boundingFrustum.Contains(_backdrop3Ds[i].BoundingSphere) != ContainmentType.Disjoint)
						_backdrop3Ds[i].Draw(Camera3D);
		}

		/// <summary>
		/// Přidá trojrozměrný statický objekt do trojrozměrného světa.
		/// </summary>
		/// <param name="backdrop3D">Trojrozměrný statický objekt.</param>
		/// <returns>Vrací <code>true</code>, pokud se přidání podařilo, a <code>false</code>, pokud již stejný objekt
		/// byl přidán.</returns>
		public bool AddBackdrop3D(Backdrop3D backdrop3D)
		{
			if (_backdrop3Ds.Contains(backdrop3D)) return false;

			_backdrop3Ds.Add(backdrop3D);
			return true;
		}

		/// <summary>
		/// Odebere trojrozměrný statický objekt z trojrozměrného světa.
		/// </summary>
		/// <param name="backdrop3D">Trojrozměrný statický objekt k odebrání.</param>
		/// <returns>Vrací <code>true</code>, pokud se odebrání podařilo, a <code>false</code>, pokud odebíraný objekt
		/// neexistuje.</returns>
		public bool RemoveBackdrop3D(Backdrop3D backdrop3D)
		{
			if (!_backdrop3Ds.Contains(backdrop3D)) return false;

			_backdrop3Ds.Remove(backdrop3D);
			return true;
		}

		/// <summary>
		/// Vrátí souřadnice bodu v dvourozměrném světě na základě souřadnic (<see cref="Vector2"/>) z dvourozměrné
		/// projekce trojrozměrného světa.
		/// </summary>
		/// <param name="screenCoordinates">Souřadnice na dvourozměrné projekci.</param>
		/// <param name="viewport">Dvourozměrné zobrazení trojrozměrného světa.</param>
		/// <returns></returns>
		public Vector2 GetWorld2DCoordinatesFromScreen(Vector2 screenCoordinates, Viewport viewport)
		{
			Vector3 nearPoint = viewport.Unproject(
					new Vector3(screenCoordinates, 0f), Camera3D.Projection, Camera3D.View, Matrix.Identity);
			Vector3 farPoint = viewport.Unproject(
					new Vector3(screenCoordinates, 1f), Camera3D.Projection, Camera3D.View, Matrix.Identity);

			Vector3 direction = (farPoint - nearPoint);
			direction.Normalize();

			Ray ray = new Ray(nearPoint, direction);
			Plane plane = new Plane(Vector3.Backward, 0);

			float? intersection = ray.Intersects(plane);
			Vector3 computed = ray.Position + ray.Direction * intersection.GetValueOrDefault(1f);

			return new Vector2(computed.X, computed.Y);
		}

		/// <summary>
		/// Vrátí souřadnice bodu v dvourozměrném světě na základě souřadnic (<see cref="Point"/>) z dvourozměrné
		/// projekce trojrozměrného světa.
		/// </summary>
		/// <param name="screenCoordinates">Souřadnice na dvourozměrné projekci.</param>
		/// <param name="viewport">Dvourozměrné zobrazení trojrozměrného světa.</param>
		/// <returns></returns>
		public Vector2 GetWorld2DCoordinatesFromScreen(Point screenCoordinates, Viewport viewport)
		{
			return GetWorld2DCoordinatesFromScreen(new Vector2(screenCoordinates.X, screenCoordinates.Y), viewport);
		}

		/// <summary>
		/// Odebere všechny objekty ze simulovaného světa.
		/// </summary>
		public void Clear()
		{
			_backdrop3Ds.Clear();
		}
	}
}