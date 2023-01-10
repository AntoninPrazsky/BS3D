using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Render;
using Prazsky.Core.Camera;

namespace Prazsky.Core
{
	/// <summary>
	/// An abstract class representing a three-dimensional object.
	/// </summary>
	public abstract class Object3D
	{
		/// <summary>
		/// Matrix of transformations to be applied to the model before it is rendered.
		/// </summary>
		protected Matrix[] Transformations;

		/// <summary>
		/// World matrix.
		/// </summary>
		protected Matrix World { get; set; } = Matrix.Identity;

		/// <summary>
		/// Three-dimensional model.
		/// </summary>
		protected Model Model { get; set; }

		/// <summary>
		/// The position of the three-dimensional model in the three-dimensional world.
		/// </summary>
		public Vector3 Position { get; set; } = Vector3.Zero;

		/// <summary>
		/// Activation or deactivation of the default three-point lighting, which is defined by the MonoGame framework.
		/// It consists of a main, additional and rear light.
		/// https://blogs.msdn.microsoft.com/shawnhar/2007/04/09/the-standard-lighting-rig/
		/// </summary>
		public bool EnableDefaultLighting { set; get; } = true;

		/// <summary>
		/// Enable or disable lighting calculation for each pixel when rendering.
		/// When disabled or if the graphics device does not support this method, lighting is applied based on each vertex of the model (vertex lighting).
		/// </summary>
		public bool PreferPerPixelLighting { set; get; } = true;

		/// <summary>
		/// Parameters for the (<see cref="BasicEffect"/>).
		/// Used if the default lighting activated by the parameter (<see cref="EnableDefaultLighting"/>) is not used.
		/// </summary>
		public BasicEffectParams BasicEffectParams { get; set; }

		/// <summary>
		/// Bounding sphere of the three-dimensional model. It moves in the three-dimensional world together with the object.
		/// </summary>
		public BoundingSphere BoundingSphere { get; set; }

		/// <summary>
		/// Axis-aligned bounding box (AABB) of a three-dimensional model. It does not move with the object.
		/// </summary>
		public BoundingBox BoundingBox { get; set; }

		/// <summary>
		/// Renders one frame of the three-dimensional model at the corresponding position and with the corresponding rotation.
		/// Applies either the default lighting effect if enabled by the <see cref="EnableDefaultLighting"/> parameter, the effect defined by the
		/// <see cref="BasicEffectParams"/> parameter, or no effect at all.
		/// </summary>
		/// <param name="camera">The camera to be used to render the model.</param>
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