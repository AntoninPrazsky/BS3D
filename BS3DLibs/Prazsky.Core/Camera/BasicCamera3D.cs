using Microsoft.Xna.Framework;
using System;
using System.Globalization;

namespace Prazsky.Core.Camera
{
    /// <summary>
    /// Represents a basic perspective camera. Allows movement along the X, Y and Z axes and rotation around the X and Y axes.
    /// </summary>
    public class BasicCamera3D : ICamera
	{
		private const float DEFAULT_FAR_PLANE_DISTANCE = 500f;
		private const float DEFAULT_MOVE_SPEED = 0.01f;
		private const float DEFAULT_NEAR_PLANE_DISTANCE = 0.01f;
		private const float DEFAULT_ROTATION_SPEED = 0.001f;
		private const float DEFAULT_FIELD_OF_VIEW = MathHelper.PiOver4;

		private Vector3 _defaultTarget = new(0, 0, -1);
		private Vector3 _defaultUp = Vector3.Up;
		private float _rotationX = 0f;
		private float _rotationY = 0f;
		private Vector3 _target;

		private float _fieldOfView;
		private float _aspectRatio;
		private float _farPlane = DEFAULT_FAR_PLANE_DISTANCE;
		private float _nearPlane = DEFAULT_NEAR_PLANE_DISTANCE;
		private Vector3 _up = Vector3.Up;

		private Vector3 _circleOrigin = new(0, 0, -1);
		private float _circleRadius = 30f;
		private float _t = MathHelper.Pi / 2f;

		/// <summary>
		/// Constructor of basic perspective camera.
		/// </summary>
		/// <param name="position">Default camera position in the 3D world.</param>
		/// <param name="aspectRatio">Display aspect ratio (width ÷ length).</param>
		/// <param name="fieldOfView">Default camera field of view angle in radians (default is π ÷ 4).</param>
		public BasicCamera3D(Vector3 position, float aspectRatio, float fieldOfView = DEFAULT_FIELD_OF_VIEW)
		{
			Position = position;
			_aspectRatio = aspectRatio;
			FieldOfView = fieldOfView;
			_target = _defaultTarget;

			Recalculate();
		}

        /// <summary>
        /// The increment or decrement for moving the camera along the X, Y, and Z axes expressed as a three-dimensional vector.
        /// </summary>
        /// <param name="shift">The X, Y, and Z components of the vector express the increment or decrement of movement along the corresponding axes (typically -1f to 1f).</param>
        /// <param name="gameTime">Game time.</param>
        public void Move(Vector3 shift, GameTime gameTime)
		{
			//Moving the camera in the direction it is looking
			Position += MoveSpeed *
					Vector3.Transform(shift, GetCameraRotation()) *
					(float)gameTime.ElapsedGameTime.TotalMilliseconds;

			Recalculate();
		}

        /// <summary>
        /// Increment or decrement for moving the camera along the X, Y, and Z axes.
        /// </summary>
        /// <param name="X">Increment or decrement of movement along the X axis (typically -1f to 1f).</param>
        /// <param name="Y">Increment or decrement of movement along the Y axis (typically -1f to 1f).</param>
        /// <param name="Z">Increment or decrement of movement along the Z axis (typically -1f to 1f).</param>
        /// <param name="gameTime">Game time.</param>
        public void Move(float X, float Y, float Z, GameTime gameTime)
		{
			Move(new Vector3(X, Y, Z), gameTime);
		}

		public void MoveCircular(float delta, GameTime gameTime)
		{
			// x = a + r * cos(t)
			// y = b + r * sin(t)
			// t: 0 → 2π
			// (a, b): origin
			// r: radius

			// TODO: Compute t from current camera position?

			Vector3 direction = Position - Target;
			Vector3 normDirection = direction == Vector3.Zero ? direction : Vector3.Normalize(direction);
			Vector3 computedOrigin = Position - (normDirection * _circleRadius);

			_circleOrigin = computedOrigin;

			_t += MoveSpeed * delta * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			while (_t > MathHelper.TwoPi) _t -= MathHelper.TwoPi;
			while (_t < 0f) _t += MathHelper.TwoPi;

			float x = _circleOrigin.X + _circleRadius * (float)Math.Cos(_t);
			float z = _circleOrigin.Y + _circleRadius * (float)Math.Sin(_t);

			Position = new(x, Position.Y, z);
			Target = _circleOrigin;

#if DEBUG
			Console.WriteLine("Camera position: " + Position);
			Console.WriteLine("Camera target:   " + Target);
#endif
		}

		/// <summary>
		/// Increment or decrement for rotating the camera around the X and Y axes expressed as a two-dimensional vector.
		/// </summary>
		/// <param name="rotation">The X and Y components of the vector express the increment or decrement of the rotation around the corresponding axes (typically -1f to 1f).</param>
		/// <param name="gameTime">Game time.</param>
		public void Rotate(Vector2 rotation, GameTime gameTime)
		{
			_rotationX += RotationSpeed * rotation.X * (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			_rotationY += RotationSpeed * rotation.Y * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

            //Rotation values could theoretically reach System.Single.MaxValue or System.Single.MinValue
            if (_rotationX >= MathHelper.TwoPi || _rotationX <= -MathHelper.TwoPi) _rotationX = 0f;
			if (_rotationY >= MathHelper.TwoPi || _rotationY <= -MathHelper.TwoPi) _rotationY = 0f;

			Recalculate();
		}

        /// <summary>
        /// Increment or decrement for rotating the camera around the X and Y axes.
        /// </summary>
        /// <param name="X">Increment or decrement of rotation around the X axis (typically -1f to 1f).</param>
        /// <param name="Y">Increment or decrement of rotation around the Y axis (typically -1f to 1f).</param>
        /// <param name="gameTime">Game time.</param>
        public void Rotate(float X, float Y, GameTime gameTime)
		{
			Rotate(new Vector2(X, Y), gameTime);
		}

		/// <summary>
		/// Sets properties used for circular camera movement.
		/// </summary>
		/// <param name="circleOrigin">Circle origin (the camera will look at this point).</param>
		/// <param name="circleRadius">Circle radius (the camera will be this far from the circle origin). Must be greater than 0.</param>
		/// <param name="t">Parametric variable in the range from 0 to 2π (different values are clamped).</param>
		public void SetCircularMovementProperties(Vector3 circleOrigin, float circleRadius = 30f, float t = MathHelper.Pi / 2f)
		{
			if (circleRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(circleRadius), "Circle radius must be greater than 0.");

			_circleOrigin = circleOrigin;
			_circleRadius = circleRadius;
			_t = Math.Clamp(t, 0f, MathHelper.TwoPi);
		}

		public void Recalculate()
		{
            //Camera rotation around the X and Y axis
            Matrix rotation = GetCameraRotation();

            //Recalculating the point the camera is looking at based on the camera rotation
            _target = Position + Vector3.Transform(_defaultTarget, rotation);

            //Recalculating the vector pointing relatively up from the camera
            _up = Vector3.Transform(_defaultUp, rotation);

			RecalculateViewProjection(_up);
		}

		private void RecalculateViewProjection(Vector3 upVector)
		{
            //Recalculation of view matrix and projection matrix
            View = Matrix.CreateLookAt(Position, _target, upVector);
			Projection = Matrix.CreatePerspectiveFieldOfView(_fieldOfView, _aspectRatio, _nearPlane, _farPlane);
		}

		private Matrix GetCameraRotation()
		{
			return Matrix.CreateRotationX(_rotationX) * Matrix.CreateRotationY(_rotationY);
		}

		#region ICamera members

		/// <summary>
		/// The distance of the rear clipping plane from the camera position. Objects behind this plane are not displayed.
		/// </summary>
		public float FarPlane
		{
			get => _farPlane;
			set
			{
				if (value <= _nearPlane)
					throw new ArgumentException("The distance of the far cutting plane cannot be less than or equal to the distance of the near cutting plane.", "FarPlane");
				_farPlane = value;
			}
		}

        /// <summary>
        /// The distance of the near clipping plane from the camera position. Objects in front of this plane are not displayed.
        /// </summary>
        public float NearPlane
		{
			get => _nearPlane;
			set
			{
				if (value >= _farPlane)
					throw new ArgumentException("The distance of the near clipping plane cannot be greater than or equal to the distance of the far clipping plane.", "NearPlane");
				if (value <= 0)
					throw new ArgumentException("The distance of the near clipping plane cannot be less than or equal to zero.", "NearPlane");
				_nearPlane = value;
			}
		}

        /// <summary>
        /// Camera position in a three-dimensional world.
        /// </summary>
        public Vector3 Position { get; set; }

		/// <summary>
		/// Projection matrix.
		/// </summary>
		public Matrix Projection { get; private set; }

        /// <summary>
        /// The point in three-dimensional space that the camera is looking at.
        /// </summary>
        public Vector3 Target
		{
			get => _target;
			set
			{
				_target = value;

				Vector3 direction = Position - _target;
				direction.Normalize();
				_rotationX = (float)Math.Asin(-direction.Y);
				_rotationY = (float)Math.Atan2(direction.X, direction.Z);

				RecalculateViewProjection(_defaultUp);
			}
		}

        /// <summary>
        /// A vector indicating the upward direction relative to the camera.
        /// </summary>
        public Vector3 Up { get => _up; }

		/// <summary>
		/// View matrix.
		/// </summary>
		public Matrix View { get; private set; }

        #endregion ICamera members

        #region Perspective and moving camera features

        /// <summary>
        /// Display aspect ratio.
        /// </summary>
        public float AspectRatio 
		{
			get => _aspectRatio;
			set
			{
				_aspectRatio = value;
				Recalculate();
			}
		}

        /// <summary>
        /// Camera field of view. It must be greater than 0 and less than π (0° - 180°). Other values are clipped to this interval.
        /// </summary>
        public float FieldOfView
		{
			get => _fieldOfView; set => _fieldOfView = MathHelper.Clamp(value, float.Epsilon, MathHelper.Pi);
		}

        /// <summary>
        /// Relative speed of incremental camera movement.
        /// </summary>
        public float MoveSpeed { get; set; } = DEFAULT_MOVE_SPEED;

        /// <summary>
        /// Relative speed of incremental camera rotation.
        /// </summary>
        public float RotationSpeed { get; set; } = DEFAULT_ROTATION_SPEED;

        #endregion Perspective and moving camera features
    }
}