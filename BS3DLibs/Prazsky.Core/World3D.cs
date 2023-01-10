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
        /// A three-dimensional camera that observes the three-dimensional world.
        /// </summary>
        public ICamera Camera3D;

        /// <summary>
        /// Constructor of a three-dimensional world.
        /// </summary>
        /// <param name="camera">The default camera that observes the three-dimensional world.</param>
        public World3D(ICamera camera)
        {
            Camera3D = camera;
        }

        /// <param name="gameTime">Game time.</param>
        public void Update(GameTime gameTime)
        {
            throw new NotImplementedException(); 
        }

        /// <summary>
        /// Renders a single frame of a three-dimensional world.
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
        /// Adds a three-dimensional static object to a three-dimensional world.
        /// </summary>
        /// <param name="backdrop3D">A three-dimensional static object.</param>
        /// <returns>Returns <code>true</code> if the addition was successful and <code>false</code> if the same object has already been added.</returns>
        public bool AddBackdrop3D(Backdrop3D backdrop3D)
        {
            if (_backdrop3Ds.Contains(backdrop3D)) return false;

            _backdrop3Ds.Add(backdrop3D);
            return true;
        }

        /// <summary>
        /// Removes a three-dimensional static object from a three-dimensional world.
        /// </summary>
        /// <param name="backdrop3D">A three-dimensional static object to remove.</param>
        /// <returns>Returns <code>true</code> if the removal was successful and <code>false</code> if the object being removed does not exist.</returns>
        public bool RemoveBackdrop3D(Backdrop3D backdrop3D)
        {
            if (!_backdrop3Ds.Contains(backdrop3D)) return false;

            _backdrop3Ds.Remove(backdrop3D);
            return true;
        }

        /// <summary>
        /// Returns the coordinates of a point in a two-dimensional world based on the coordinates (<see cref="Vector2"/>) from a two-dimensional projection of 
        /// a three-dimensional world.
        /// </summary>
        /// <param name="screenCoordinates">Coordinates on a two-dimensional projection.</param>
        /// <param name="viewport">A two-dimensional representation of a three-dimensional world.</param>
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
        /// Returns the coordinates of a point in a 2D world based on the coordinates (<see cref="Point"/>) from a 2D projection of a 3D world.
        /// </summary>
        /// <param name="screenCoordinates">Coordinates on a two-dimensional projection.</param>
        /// <param name="viewport">A two-dimensional representation of a three-dimensional world.</param>
        /// <returns></returns>
        public Vector2 GetWorld2DCoordinatesFromScreen(Point screenCoordinates, Viewport viewport)
        {
            return GetWorld2DCoordinatesFromScreen(new Vector2(screenCoordinates.X, screenCoordinates.Y), viewport);
        }

        /// <summary>
        /// Removes all objects from the three-dimensional world.
        /// </summary>
        public void Clear()
        {
            _backdrop3Ds.Clear();
        }
    }
}