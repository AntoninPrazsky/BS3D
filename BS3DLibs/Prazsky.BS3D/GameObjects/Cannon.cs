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

		private readonly float _floorHeight;
		private readonly float _orbitRadius;
		private readonly Vector3 _orbitCenter;

		private float _parametricVariable = Constants.HALF_PI;

		private Vector2 _rotationToOrbitCenter = Vector2.Zero;
		private Vector2 _rotationAim = Vector2.Zero;

		private float _delta = 0f;
		private float _deltaLastSet = 0f;
		private float _acceleration = 0f;
		private bool _braking = false;

		public event EventHandler<EventArgs> EnabledChanged;
		public event EventHandler<EventArgs> UpdateOrderChanged;

		public Cannon(Model model, Vector3 orbitCenter, float floorHeight, float orbitRadius = 20f)
		{ 
			Model = model;
			Transformations = new Matrix[Model.Bones.Count];
			Model.CopyBoneTransformsTo(Transformations);

			_orbitCenter = orbitCenter;
			_floorHeight = floorHeight;
			_orbitRadius = orbitRadius;

			Initialize();
		}

		private void Initialize()
		{
			CalculateInitialPositionAndAimTarget();
			Recalculate();
			RecalculateWorldMatrix();
		}

		public void Update(GameTime gameTime)
		{
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
		}

		public void Orbit(float delta)
		{
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

			EnsureAimInBounds();

#if DEBUG
			Console.WriteLine("Canon position: " + Position);
			Console.WriteLine("Aim direction:  " + AimTarget);
#endif

			Recalculate();
			RecalculateWorldMatrix();
		}

		public void Restart()
		{
			_parametricVariable = Constants.HALF_PI;
			_rotationToOrbitCenter = Vector2.Zero;
			_rotationAim = Vector2.Zero;
			_acceleration = 0f;

			Initialize();
		}

		private void MoveCircular(GameTime gameTime)
		{
			_parametricVariable += RotationSpeed * _acceleration * _deltaLastSet * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			EnsureParametricVariableInBounds();

			var x = _orbitCenter.X + (_orbitRadius * (float)Math.Cos(_parametricVariable));
			var z = _orbitCenter.Z + (_orbitRadius * (float)Math.Sin(_parametricVariable));

			Position = new(x, Position.Y, z);
			Recalculate();
			RecalculateWorldMatrix();
		}

		private void CalculateInitialPositionAndAimTarget()
		{
			Position = new Vector3(_orbitCenter.X, _floorHeight, _orbitCenter.Z + _orbitRadius);
			AimTarget = Vector3.Normalize(_orbitCenter);

			RecalculateWorldMatrix();
		}

		private void Recalculate()
		{
			var directionToOrbitCenter = Position - _orbitCenter;
			var normalized = directionToOrbitCenter == Vector3.Zero ? directionToOrbitCenter : Vector3.Normalize(directionToOrbitCenter);
			_rotationToOrbitCenter = new Vector2((float)Math.Asin(-normalized.Y), (float)Math.Atan2(normalized.X, normalized.Z));

			var finalRotationX = _rotationToOrbitCenter.X + _rotationAim.X - Constants.HALF_PI;
			var finalRotationY = _rotationToOrbitCenter.Y + _rotationAim.Y;

			Matrix rotationMatrix = Matrix.CreateRotationX(finalRotationX) * Matrix.CreateRotationY(finalRotationY);
			AimTarget = Position + Vector3.Transform(_orbitCenter, rotationMatrix);
		}

		private void EnsureParametricVariableInBounds()
		{
			while (_parametricVariable > MathHelper.TwoPi) _parametricVariable -= MathHelper.TwoPi;
			while (_parametricVariable < 0f) _parametricVariable += MathHelper.TwoPi;
		}

		private void EnsureAimInBounds()
		{
			var actualXRotation = _rotationAim.X + _rotationToOrbitCenter.X;

			if (actualXRotation >= -Constants.HALF
				&& actualXRotation <= Constants.HALF_PI
				&& _rotationAim.Y >= -Constants.HALF_PI
				&& _rotationAim.Y <= Constants.HALF_PI) return;

			var x = Math.Clamp(actualXRotation, -Constants.HALF, Constants.HALF_PI) - _rotationToOrbitCenter.X;
			var y = Math.Clamp(_rotationAim.Y, -Constants.HALF_PI, Constants.HALF_PI);

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
