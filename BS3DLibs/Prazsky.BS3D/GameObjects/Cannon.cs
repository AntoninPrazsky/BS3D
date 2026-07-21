using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D.GameObjects
{
	public class Cannon : Object3D
	{
		private static readonly float DEFAULT_ROTATION_SPEED = Constants.THOUSANDTH;
		private static readonly float ACCELERATION_DELTA = Constants.THOUSANDTH;

		public float RotationSpeed { get; set; } = DEFAULT_ROTATION_SPEED;

		public Vector3 AimTarget;
		public readonly Vector3 OrbitCenter;

		private readonly float _floorHeight;
		private readonly float _orbitRadius;

		private float _orbitAngle = Constants.HALF_PI;

		private Vector2 _rotationToOrbitCenter = Vector2.Zero;
		private Vector2 _rotationAim = Vector2.Zero;

		private float _delta = 0f;
		private float _deltaLastSet = 0f;
		private float _acceleration = 0f;
		private bool _braking = false;
		private bool _aimParkingStarted = false;
		private float _aimParkingStep = 0f;
		private bool _aiming = false;
		private Vector2 _beforeAnimationRotationAim = Vector2.Zero;

		public Cannon(Vector3 orbitCenter, float floorHeight, float orbitRadius = 20f)
		{
			//The cannon is drawn procedurally now (a CannonMesh with the loaded balls shown in its magazine),
			//so this holds only the pose: where it orbits, where it aims and its World matrix. The renderer
			//builds its own look-at world from Position and AimTarget.
			OrbitCenter = orbitCenter;
			_floorHeight = floorHeight;
			_orbitRadius = orbitRadius;

			Initialize();
		}

		private void Initialize()
		{
			CalculateInitialPositionAndAimTarget();
			RecalculateRotation();
			RecalculateWorldMatrix();
		}

		public void Update(GameTime gameTime)
		{
			#region Orbiting

			if (_acceleration <= 0f) _braking = false;

			if (Math.Sign(_delta) != 0)
			{
				MoveCircular(gameTime);
				if (_acceleration < 1f) _acceleration += ACCELERATION_DELTA * (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			}

			if (_delta == 0f && _acceleration > 0f)
			{
				MoveCircular(gameTime);
				_acceleration -= ACCELERATION_DELTA * (_braking ? 4f : 2f) * (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			}

			_delta = 0f;

			#endregion

			#region Aim parking

			if (!_aiming && _aimParkingStarted && _rotationAim != Vector2.Zero)
			{
				_rotationAim = Vector2.SmoothStep(_beforeAnimationRotationAim, Vector2.Zero, _aimParkingStep);
				RecalculateRotation();
				RecalculateWorldMatrix();
				_aimParkingStep += DEFAULT_ROTATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

				if (_aimParkingStep > 1f)
				{
					_aimParkingStep = 0f;
					_aimParkingStarted = false;
				}
			}

			_aiming = false;

			#endregion
		}

		public void Orbit(float delta)
		{
			_aimParkingStarted = true;
			_beforeAnimationRotationAim = _rotationAim;

			if (Math.Sign(delta) != Math.Sign(_deltaLastSet) && _acceleration > 0f)
			{
				_braking = true;
				return;
			}

			_delta = delta;
			_deltaLastSet = delta;
		}

		public void Aim(Vector2 rotation, GameTime gameTime)
		{
			_rotationAim += RotationSpeed * rotation * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			_aiming = true;
			_aimParkingStarted = false;
			_aimParkingStep = 0f;

			EnsureAimInBounds();
			RecalculateRotation();
			RecalculateWorldMatrix();
		}

		public void Restart()
		{
			_orbitAngle = Constants.HALF_PI;
			_rotationToOrbitCenter = Vector2.Zero;
			_rotationAim = Vector2.Zero;
			_acceleration = 0f;

			Initialize();
		}

		private void MoveCircular(GameTime gameTime)
		{
			_orbitAngle += RotationSpeed * _acceleration * _deltaLastSet * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			EnsureOrbitAngleInBounds();

			var x = OrbitCenter.X + (_orbitRadius * (float)Math.Cos(_orbitAngle));
			var z = OrbitCenter.Z + (_orbitRadius * (float)Math.Sin(_orbitAngle));

			Position = new(x, Position.Y, z);

			RecalculateRotation();
			RecalculateWorldMatrix();
		}

		private void CalculateInitialPositionAndAimTarget()
		{
			Position = new Vector3(OrbitCenter.X, _floorHeight, OrbitCenter.Z + _orbitRadius);
			AimTarget = Vector3.Normalize(OrbitCenter);

			RecalculateWorldMatrix();
		}

		private void RecalculateRotation()
		{
			var directionToOrbitCenter = Position - OrbitCenter;
			var normalized = directionToOrbitCenter == Vector3.Zero ? directionToOrbitCenter : Vector3.Normalize(directionToOrbitCenter);
			_rotationToOrbitCenter = new Vector2((float)Math.Asin(-normalized.Y), (float)Math.Atan2(normalized.X, normalized.Z));

			var finalRotationX = _rotationToOrbitCenter.X + _rotationAim.X - Constants.HALF_PI;
			var finalRotationY = _rotationToOrbitCenter.Y + _rotationAim.Y;

			Matrix rotationMatrix = Matrix.CreateRotationX(finalRotationX) * Matrix.CreateRotationY(finalRotationY);
			AimTarget = Position + Vector3.Transform(OrbitCenter, rotationMatrix);
		}

		private void EnsureOrbitAngleInBounds()
		{
			while (_orbitAngle > MathHelper.TwoPi) _orbitAngle -= MathHelper.TwoPi;
			while (_orbitAngle < 0f) _orbitAngle += MathHelper.TwoPi;
		}

		private void EnsureAimInBounds()
		{
			var actualXRotation = _rotationAim.X + _rotationToOrbitCenter.X;

			if (actualXRotation >= -Constants.HALF
				&& actualXRotation <= Constants.QUARTER_PI
				&& _rotationAim.Y >= -Constants.QUARTER_PI
				&& _rotationAim.Y <= Constants.QUARTER_PI) return;

			var x = Math.Clamp(actualXRotation, -Constants.HALF, Constants.QUARTER_PI) - _rotationToOrbitCenter.X;
			var y = Math.Clamp(_rotationAim.Y, -Constants.QUARTER_PI, Constants.QUARTER_PI);

			_rotationAim = new Vector2(x, y);
		}

		private void RecalculateWorldMatrix()
		{
			World
				= Matrix.CreateRotationX(_rotationToOrbitCenter.X + _rotationAim.X)
				* Matrix.CreateRotationY(_rotationToOrbitCenter.Y + _rotationAim.Y)
				* Matrix.CreateTranslation(Position);
		}
	}
}
