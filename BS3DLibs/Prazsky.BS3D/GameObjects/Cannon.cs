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
		/// cluster — <see cref="EnsureAimInBounds"/> tops the elevation out at <see cref="MaxElevation"/>
		/// (~80°), so standing too close puts the resting aim outside the clamp. Setting it slides the gun
		/// along its current orbit angle.
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

		#region The barrel's pose

		//Everything below is read off the pose rather than stored: it stood in both executables as a private
		//helper apiece, value-identical, until #76. It all runs per frame and several times per frame, so none
		//of it allocates and none of it composes a matrix it could write a row of instead - see the whole
		//discipline in BestPractices.md.
		//
		//Note that none of it is the inherited Object3D.World, which is built from the same pose but as an
		//Euler pair (RecalculateWorldMatrix above) and is not what the barrel is drawn with: the mesh is
		//modelled looking down local -Z with its slot on local +Y, which is the basis CreateWorld gives and not
		//the one CreateRotationX * CreateRotationY does. World is unused by both executables and stays as it is.

		/// <summary>
		/// The direction the gun fires: from the trunnions (<see cref="Object3D.Position"/>) towards
		/// <see cref="AimTarget"/>. Pure geometry, and the shot's own direction — a ball leaves along the barrel,
		/// before gravity. The muzzle lies on this very line, so deriving the muzzle from the pivot rather than
		/// the other way round moved nothing about where a shot goes.
		/// <para>
		/// It deliberately takes <b>no recoil</b>, unlike <see cref="MuzzlePosition"/> and
		/// <see cref="BarrelWorld"/>: the recoil is <b>drawing only</b>. A shot leaves along the true aim on the
		/// frame it is fired, before the barrel has moved, so nothing about where a ball goes may depend on it —
		/// and the launch smear stays anchored at the un-recoiled muzzle, about a unit from the drawn one at the
		/// peak of the stroke, which is invisible against a seven-unit streak. Feeding the recoil in here is
		/// precisely the thing a reader would "fix" (see docs/game-session.md, "Recoil and camera shake").
		/// </para>
		/// </summary>
		public Vector3 AimDirection => Vector3.Normalize(AimTarget - Position);

		/// <summary>
		/// The horizontal direction from <see cref="OrbitCenter"/> out to where the gun stands — the way a
		/// third-person camera behind it stands back. Deliberately <b>flattened to the horizontal</b>: taken
		/// straight from <c>Position - OrbitCenter</c> it tilts down by however far the gun stands below the
		/// cluster (about 30° as both executables place it), which ate most of the camera's back-off downwards,
		/// cancelled its height and left the lens sitting on the barrel's own axis, seeing the gun end-on with
		/// the magazine slot along its top edge-on. Flat, the camera's own height is the only thing that decides
		/// how far the view looks down onto the barrel, which is the point of having one.
		/// <para>
		/// Falls back to <see cref="Vector3.Backward"/> in the degenerate case of the gun standing exactly on
		/// the centre it orbits, so a caller never normalizes a zero vector.
		/// </para>
		/// </summary>
		public Vector3 StandBearing
		{
			get
			{
				Vector3 back = Position - OrbitCenter;
				back.Y = 0f;

				return back == Vector3.Zero ? Vector3.Backward : Vector3.Normalize(back);
			}
		}

		/// <summary>
		/// The centre the gun orbits, at ground level: what a third-person camera stands off from and turns
		/// about. The field's centre, in other words, with the cluster's height dropped out of it.
		/// </summary>
		public Vector3 OrbitCenterGround => new(OrbitCenter.X, 0f, OrbitCenter.Z);

		/// <summary>
		/// Where the ball at the head of the magazine sits, and so where a shot spawns: on the barrel axis,
		/// <paramref name="pivotToFrontBall"/> ahead of the trunnions along <see cref="AimDirection"/>. Unlike
		/// the pivot it swings with the aim — it is the muzzle end of a barrel that turns about its middle — so
		/// the shot leaves from the ball the player was watching rather than from the middle of the tube.
		/// </summary>
		/// <param name="pivotToFrontBall">Half the loaded queue's length: how far the head-of-queue ball sits
		/// ahead of the trunnions. It is the barrel <i>hardware</i>'s figure, not the pose's, so it is handed in
		/// rather than stored — <see cref="CannonRig.PivotToFrontBall"/> derives it from the magazine the bore
		/// was sized to, and the magazine itself belongs to the caller.</param>
		/// <param name="recoilBack">How far back along the bore the barrel is displaced by its own recoil this
		/// instant, in world units, 0 at rest. A caller-passed scalar on purpose: one executable animates a
		/// recoil and the other does not, and this owns neither the stroke's shape nor its decay. Drawing only —
		/// see the note on <see cref="AimDirection"/>. The queue rides in the bore, so a caller drawing the
		/// loaded balls passes the same value it drew the barrel with, or the balls float out of it.</param>
		public Vector3 MuzzlePosition(float pivotToFrontBall, float recoilBack = 0f) =>
			//One normalize and one scale: the two offsets are along the same axis, so they are summed as
			//scalars first rather than as two vectors
			Position + AimDirection * (pivotToFrontBall - recoilBack);

		/// <summary>
		/// The barrel's orientation: forward down the aim, with the magazine slot (the mesh's local +Y) pinned
		/// to <b>world</b> up, so the slit stays on the barrel's upper face and never rolls about the bore.
		/// <para>
		/// An earlier version rolled the slot to face the <b>camera</b> so the loaded queue was always readable,
		/// but the roll looked wrong in motion: the gun is to sit on a stand that only elevates and traverses,
		/// and a barrel that spins about its own axis to track the eye reads as unreal. The cost is accepted —
		/// from the low game camera the player sees the barrel's underside and not always the queue (precise
		/// aim, whose lens rides over the barrel, still looks into the slot). Note it no longer depends on the
		/// camera at all.
		/// </para>
		/// <para>
		/// <see cref="Matrix.CreateWorld(Vector3, Vector3, Vector3)"/> orthogonalises world up against the aim,
		/// so the slit stays on the upper face as the barrel elevates, and the bore is clamped well off vertical
		/// (<see cref="MinElevation"/>/<see cref="MaxElevation"/>) so world up and the aim are never parallel and
		/// this never degenerates. The loaded balls take <b>this same basis</b>, which is what stops them
		/// skewing: drawn unrotated they would hold a fixed world orientation while the barrel tilted around
		/// them, and the eye reads that mismatch as each ball twisting in its slot.
		/// </para>
		/// </summary>
		public Matrix BarrelOrientation() => Matrix.CreateWorld(Vector3.Zero, AimDirection, Vector3.Up);

		/// <summary>
		/// The matrix the barrel is drawn with: <see cref="BarrelOrientation"/> about the trunnions. Built from
		/// the pose rather than being the inherited <see cref="Object3D.World"/>, because the mesh is modelled
		/// with its muzzle towards local -Z and its slot towards local +Y — which is what
		/// <see cref="Matrix.CreateWorld(Vector3, Vector3, Vector3)"/> maps to the aim and the up vector, and
		/// what an Euler pair does not.
		/// <para>
		/// The translation is the <b>trunnions</b>: the pins a carriage holds a real gun by, at the barrel's
		/// point of balance. Aiming leaves <see cref="Object3D.Position"/> alone and only moves
		/// <see cref="AimTarget"/>, so whatever Position means is what the barrel turns about — with it at the
		/// muzzle the whole tube and its loaded queue swung from the tip, which is the one place a gun does not
		/// pivot. The mesh is therefore laid out about its own midpoint (see <see cref="CannonRig"/>), so this
		/// matrix's translation row <i>is</i> the pivot.
		/// </para>
		/// </summary>
		/// <param name="recoilBack">As for <see cref="MuzzlePosition"/>: the recoil stroke this instant, in
		/// world units back along the bore, 0 at rest.</param>
		public Matrix BarrelWorld(float recoilBack = 0f)
		{
			Vector3 aim = AimDirection;
			Vector3 pivot = Position - aim * recoilBack;

			//The orientation with the translation written straight into its fourth row, rather than
			//orientation * CreateTranslation(pivot). CreateWorld(Vector3.Zero, ...) carries no translation of
			//its own, so its fourth row is (0,0,0,1) and the product is EXACTLY the orientation with that row
			//set - bit-exact, not an approximation, and it saves a whole 4x4 multiply on a per-frame path
			//(BestPractices.md, "Write a translation, do not multiply one in"). The substitution silently breaks
			//if anyone ever gives the orientation a translation of its own, so it has to stay built here.
			Matrix world = Matrix.CreateWorld(Vector3.Zero, aim, Vector3.Up);

			world.M41 = pivot.X;
			world.M42 = pivot.Y;
			world.M43 = pivot.Z;

			return world;
		}

		#endregion
	}
}
