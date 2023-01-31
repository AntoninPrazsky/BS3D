using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;
using System;

namespace Prazsky.BS3D.GameObjects
{
	public class Cannon : Object3D, IUpdateable
	{
		private const float DEFAULT_ROTATION_SPEED = 0.001f;
		private float ACCELERATION_DELTA = 0.001f;
		public float RotationSpeed { get; set; } = DEFAULT_ROTATION_SPEED;

		public bool Enabled => throw new NotImplementedException();

		public int UpdateOrder => throw new NotImplementedException();

		public Vector3 ShootTarget;
		public float OrbitRadius;
		public Vector3 Direction;
		public float FloorHeight;

		private float _parametricVariable = MathHelper.Pi / 2f;

		private float _rotationX = 0f;
		private float _rotationY = 0f;

		private float _delta = 0f;
		private float _deltaLastSet = 0f;
		private float _acceleration = 0f;
		private bool _braking = false;

		public event EventHandler<EventArgs> EnabledChanged;
		public event EventHandler<EventArgs> UpdateOrderChanged;

		public Cannon(Model model, Vector3 initialShootTarget, float floorHeight, float orbitRadius = 20f)
		{ 
			Model = model;
			Transformations = new Matrix[Model.Bones.Count];
			Model.CopyBoneTransformsTo(Transformations);

			ShootTarget = initialShootTarget;
			FloorHeight = floorHeight;
			OrbitRadius = orbitRadius;

			Initialize();
		}

		private void Initialize()
		{
			CalculateDefaultPosition();
			RecalculateDirection();
			RecalculateWorldMatrix();
		}

		public void Update(GameTime gameTime)
		{
			if (_acceleration <= 0f) _braking = false;

			if (Math.Sign(_delta) != 0)
			{
				Move(gameTime);
				if (_acceleration < 1f) _acceleration += ACCELERATION_DELTA * (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			}
			
			if (_delta == 0f && _acceleration > 0f)
			{
				Move(gameTime);
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

		private void Move(GameTime gameTime)
		{
			_parametricVariable += RotationSpeed * _acceleration * _deltaLastSet * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			EnsureParametricVariableInBounds();

			var x = ShootTarget.X + (OrbitRadius * (float)Math.Cos(_parametricVariable));
			var z = ShootTarget.Z + (OrbitRadius * (float)Math.Sin(_parametricVariable));

			Position = new(x, Position.Y, z);
			RecalculateDirection();
			RecalculateWorldMatrix();
		}

		private void CalculateDefaultPosition()
		{
			Position = new Vector3(ShootTarget.X, FloorHeight, ShootTarget.Z + OrbitRadius);
			RecalculateWorldMatrix();
		}

		private void RecalculateDirection()
		{
			var direction = Position - ShootTarget;
			Direction = direction == Vector3.Zero ? direction : Vector3.Normalize(direction);

			RecalculateRotation();
		}

		private void RecalculateRotation()
		{
			_rotationX = (float)Math.Asin(-Direction.Y);
			_rotationY = (float)Math.Atan2(Direction.X, Direction.Z);
		}

		private void EnsureParametricVariableInBounds()
		{
			while (_parametricVariable > MathHelper.TwoPi) _parametricVariable -= MathHelper.TwoPi;
			while (_parametricVariable < 0f) _parametricVariable += MathHelper.TwoPi;
		}

		private void RecalculateWorldMatrix()
		{
			World = Matrix.CreateRotationX(_rotationX) * Matrix.CreateRotationY(_rotationY) * Matrix.CreateTranslation(Position);
		}
	}
}
