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

		//Elevation (pitch off horizontal) the barrel is allowed to reach, in radians, as a TOTAL angle -
		//resting pitch plus the aimed offset (see EnsureAimInBounds). The whole target cluster hangs overhead,
		//so the useful range is strongly upward: a hair below horizontal (aiming down only wastes shots into
		//the ground) up to steep, kept off vertical so the ADS over-barrel up-vector never degenerates. The old
		//range ([-28.6°, +45°]) let the gun plunge into the ground yet could not lift onto a large map's top
		//cells while facing them (~52-64° needed) - a third of a wide map was unreachable, so levels could not
		//be finished. ~80° up covers the top corners of every supported map with margin; -5.7° down is a hair
		//of feel with no more shots into the ground. (aimcheck logs the facing-elevation reachability per map.)
		public const float MinElevation = -0.10f;
		public const float MaxElevation = 1.40f;

		//Traverse (yaw) the aim may swing either side of the resting heading, in radians (±45°).
		public const float MaxTraverse = Constants.QUARTER_PI;

		public Vector3 AimTarget;
		public readonly Vector3 OrbitCenter;

		private readonly float _trunnionHeight;
		private float _orbitRadius;

		private float _orbitAngle = Constants.HALF_PI;

		private Vector2 _rotationToOrbitCenter = Vector2.Zero;
		private Vector2 _rotationAim = Vector2.Zero;

		private float _delta = 0f;
		private float _deltaLastSet = 0f;
		private float _acceleration = 0f;
		private bool _braking = false;

		private bool _resettingAim = false;
		private float _resetAimStep = 0f;
		private Vector2 _resetAimFrom = Vector2.Zero;

		/// <param name="trunnionHeight">
		/// Height of <see cref="Object3D.Position"/>, which is the barrel's pivot - the trunnions a carriage
		/// would hold it by - and not a point on the barrel's surface, so this sits an axle's height above
		/// whatever the gun ends up standing on rather than at the floor itself.
		/// </param>
		public Cannon(Vector3 orbitCenter, float trunnionHeight, float orbitRadius = 20f)
		{
			//The cannon is drawn procedurally now (a CannonMesh with the loaded balls shown in its magazine),
			//so this holds only the pose: where it orbits, where it aims and its World matrix. Position is
			//the trunnions, at the barrel's midpoint - elevating turns the barrel about them, raising the
			//muzzle and dropping the breech, so the gun stays where it stands. The renderer builds its own
			//look-at world from Position and AimTarget, and derives the muzzle from them.
			OrbitCenter = orbitCenter;
			_trunnionHeight = trunnionHeight;
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
			//Orbiting. Within game mode the mouse owns the aim and it is simply held; the only thing that moves the
			//aim on its own is a queued reset (see ResetAim), eased below when leaving game mode.
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

			//Eased aim reset (queued by ResetAim on leaving game mode): swing the barrel smoothly back to its rest
			//direction over ~1s rather than snapping - the smooth return the orbit parking used to have. Runs in any
			//mode (the cannon is a prop in free mode); a mouse Aim interrupts it. SmoothStep clamps its amount, so at
			//step >= 1 the aim sits exactly at rest.
			if (_resettingAim)
			{
				_rotationAim = Vector2.SmoothStep(_resetAimFrom, Vector2.Zero, _resetAimStep);
				RecalculateRotation();
				RecalculateWorldMatrix();

				if (_resetAimStep >= 1f) _resettingAim = false;
				else _resetAimStep += DEFAULT_ROTATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			}
		}

		/// <summary>
		/// Orbits the carriage to stand on the same side of the field as <paramref name="worldTarget"/> — facing it —
		/// so a following <see cref="AimAt"/> is the clean, steep facing shot rather than one fired across the cluster.
		/// Used by the aim-and-shoot test; in play the carriage is orbited by hand (A/D).
		/// </summary>
		public void OrbitToFace(Vector3 worldTarget)
		{
			float dx = worldTarget.X - OrbitCenter.X;
			float dz = worldTarget.Z - OrbitCenter.Z;
			if (dx * dx + dz * dz < 1e-6f) return;

			_orbitAngle = (float)Math.Atan2(dz, dx);
			MoveToOrbitAngle();
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
			_resettingAim = false; //taking the aim by hand interrupts any eased return in progress
			_rotationAim += RotationSpeed * rotation * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			EnsureAimInBounds();
			RecalculateRotation();
			RecalculateWorldMatrix();
		}

		/// <summary>
		/// Eases the aim back to its rest direction - the barrel pointing at the orbit centre - over about a second,
		/// the smooth return the orbit parking used to have, leaving the orbit position alone. Called when leaving
		/// game mode so the gun swings back rather than snapping; a mouse <see cref="Aim"/> interrupts it.
		/// </summary>
		public void ResetAim()
		{
			if (_rotationAim == Vector2.Zero) { _resettingAim = false; return; }

			_resettingAim = true;
			_resetAimFrom = _rotationAim;
			_resetAimStep = 0f;
		}

		public void Restart()
		{
			_orbitAngle = Constants.HALF_PI;
			_rotationToOrbitCenter = Vector2.Zero;
			_rotationAim = Vector2.Zero;
			_acceleration = 0f;
			_resettingAim = false;

			Initialize();
		}

		/// <summary>
		/// How far out from <see cref="OrbitCenter"/> the gun stands. Set per map rather than fixed: it has to
		/// clear the play field's footprint, and it also decides how steeply the barrel looks up at the
		/// cluster — <see cref="EnsureAimInBounds"/> tops the elevation out at 45°, so standing too close
		/// puts the resting aim outside the clamp. Setting it slides the gun along its current orbit angle.
		/// </summary>
		public float OrbitRadius
		{
			get => _orbitRadius;
			set
			{
				_orbitRadius = value;
				MoveToOrbitAngle();
			}
		}

		private void MoveCircular(GameTime gameTime)
		{
			_orbitAngle += RotationSpeed * _acceleration * _deltaLastSet * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			EnsureOrbitAngleInBounds();
			MoveToOrbitAngle();
		}

		private void MoveToOrbitAngle()
		{
			var x = OrbitCenter.X + (_orbitRadius * (float)Math.Cos(_orbitAngle));
			var z = OrbitCenter.Z + (_orbitRadius * (float)Math.Sin(_orbitAngle));

			Position = new(x, Position.Y, z);

			RecalculateRotation();
			RecalculateWorldMatrix();
		}

		private void CalculateInitialPositionAndAimTarget()
		{
			Position = new Vector3(OrbitCenter.X, _trunnionHeight, OrbitCenter.Z + _orbitRadius);
			AimTarget = Vector3.Normalize(OrbitCenter);

			RecalculateWorldMatrix();
		}

		private void RecalculateRotation()
		{
			UpdateRestRotation();

			var finalRotationX = _rotationToOrbitCenter.X + _rotationAim.X - Constants.HALF_PI;
			var finalRotationY = _rotationToOrbitCenter.Y + _rotationAim.Y;

			Matrix rotationMatrix = Matrix.CreateRotationX(finalRotationX) * Matrix.CreateRotationY(finalRotationY);
			AimTarget = Position + Vector3.Transform(OrbitCenter, rotationMatrix);
		}

		/// <summary>
		/// The barrel's resting pose in (pitch, yaw): the rotation that points it from <see cref="Object3D.Position"/>
		/// straight at <see cref="OrbitCenter"/>. Depends only on where the gun stands, so it is recomputed whenever
		/// the pose is rebuilt and is the reference the aimed offset (<c>_rotationAim</c>) and the clamps add onto.
		/// </summary>
		private void UpdateRestRotation()
		{
			var directionToOrbitCenter = Position - OrbitCenter;
			var normalized = directionToOrbitCenter == Vector3.Zero ? directionToOrbitCenter : Vector3.Normalize(directionToOrbitCenter);
			_rotationToOrbitCenter = new Vector2((float)Math.Asin(-normalized.Y), (float)Math.Atan2(normalized.X, normalized.Z));
		}

		/// <summary>
		/// Whether the barrel can be pointed straight at <paramref name="worldTarget"/> within the elevation and
		/// traverse clamps from where the gun currently stands. Outputs the total elevation off horizontal and the
		/// traverse off the resting heading such an aim needs, so a caller can report by how far a target is out of
		/// reach. Pure geometry: the shot leaves along the barrel, so this is the aim direction, before gravity.
		/// </summary>
		public bool CanAimAt(Vector3 worldTarget, out float requiredElevation, out float requiredTraverse)
		{
			UpdateRestRotation();

			Vector3 d = worldTarget - Position;
			float horizontal = (float)Math.Sqrt(d.X * d.X + d.Z * d.Z);
			requiredElevation = (float)Math.Atan2(d.Y, horizontal);

			//The aim direction's heading comes out as (restYaw + aimYaw + PI) in RecalculateRotation, so the aimed
			//yaw needed to face the target is its heading, less PI, less the resting heading (wrapped to ±PI).
			float desiredHeading = (float)Math.Atan2(d.X, d.Z);
			requiredTraverse = MathHelper.WrapAngle(desiredHeading - MathHelper.Pi - _rotationToOrbitCenter.Y);

			return requiredElevation >= MinElevation && requiredElevation <= MaxElevation
				&& requiredTraverse >= -MaxTraverse && requiredTraverse <= MaxTraverse;
		}

		/// <summary>
		/// Points the barrel at <paramref name="worldTarget"/> as nearly as the clamps allow, from where the gun
		/// currently stands. Returns whether the target was reachable without being clamped short. Used by the aim
		/// diagnostics/tests (and available for any auto-aim); the mouse path still goes through <see cref="Aim"/>.
		/// </summary>
		public bool AimAt(Vector3 worldTarget)
		{
			bool reachable = CanAimAt(worldTarget, out float requiredElevation, out float requiredTraverse);

			_resettingAim = false;
			_rotationAim = new Vector2(requiredElevation - _rotationToOrbitCenter.X, requiredTraverse);

			EnsureAimInBounds();
			RecalculateRotation();
			RecalculateWorldMatrix();

			return reachable;
		}

		private void EnsureOrbitAngleInBounds()
		{
			while (_orbitAngle > MathHelper.TwoPi) _orbitAngle -= MathHelper.TwoPi;
			while (_orbitAngle < 0f) _orbitAngle += MathHelper.TwoPi;
		}

		private void EnsureAimInBounds()
		{
			var actualXRotation = _rotationAim.X + _rotationToOrbitCenter.X;

			if (actualXRotation >= MinElevation
				&& actualXRotation <= MaxElevation
				&& _rotationAim.Y >= -MaxTraverse
				&& _rotationAim.Y <= MaxTraverse) return;

			var x = Math.Clamp(actualXRotation, MinElevation, MaxElevation) - _rotationToOrbitCenter.X;
			var y = Math.Clamp(_rotationAim.Y, -MaxTraverse, MaxTraverse);

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
