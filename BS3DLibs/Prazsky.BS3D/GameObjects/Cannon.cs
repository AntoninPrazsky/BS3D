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

		/// <summary>
		/// The up-elevation the gun may actually be aimed to, which is <see cref="MaxElevation"/> — what the
		/// hardware can do — unless a level lowers it. Everything that clamps or tests an aim uses this;
		/// <see cref="MaxElevation"/> stays the gun's own capability and is what a reachability check measures
		/// the mount against.
		/// <para>
		/// It exists for the <b>tall</b> levels, whose column reaches up out of the camera's frame. There the
		/// full 80° lets the player aim at balls they cannot see and shoot blind into the part of the level
		/// that has not arrived yet; the game lowers this to the steepest aim that still reaches the top of
		/// what is framed. Clamped into the gun's own range on the way in, so a caller cannot hand it an
		/// elevation the mount does not have — or, by passing something huge, quietly remove the limit.
		/// </para>
		/// </summary>
		public float ElevationLimit
		{
			get => _elevationLimit;
			set
			{
				_elevationLimit = Math.Clamp(value, MinElevation, MaxElevation);

				//The standing aim may already be steeper than the new limit — a level installed under a gun
				//left pointing up, or a mid-level re-solve — and nothing else would bring it down until the
				//next mouse movement
				EnsureAimInBounds();
			}
		}

		private float _elevationLimit = MaxElevation;

		//Traverse (yaw) the aim may swing either side of the resting heading, in radians (±45°).
		public const float MaxTraverse = Constants.QUARTER_PI;

		/// <summary>
		/// How far from either end of the advance range the carriage starts to cushion, in world units: within
		/// this zone the stroke's speed scales down linearly with the room left, which makes the approach to an
		/// end <b>exponential</b> — the gun eases asymptotically onto the stop and never strikes it. The rubber
		/// is one-sided by construction: reversing out of a cushion reads the room towards the <i>other</i> end,
		/// which is large, so the way back answers at full speed with no dead travel.
		/// </summary>
		public const float ADVANCE_EASE_ZONE = 1.5f;

		//Full advance speed in units per millisecond (~5 u/s): the whole of a typical 8-unit range in under
		//two seconds, ramped by the same acceleration idiom the orbit uses so the two movements feel like one
		//carriage. The speed is a scale on the same _delta protocol Orbit takes, so a caller passes ±1 per
		//held frame here exactly as it does there.
		private static readonly float ADVANCE_SPEED = 0.005f;

		public Vector3 AimTarget;
		public readonly Vector3 OrbitCenter;

		private float _orbitRadius;

		private float _orbitAngle = Constants.HALF_PI;

		private Vector2 _rotationToOrbitCenter = Vector2.Zero;
		private Vector2 _rotationAim = Vector2.Zero;

		private float _delta = 0f;
		private float _deltaLastSet = 0f;
		private float _acceleration = 0f;
		private bool _braking = false;

		//The advance stroke's own glide, the orbit's idiom over again on purpose (see Advance). The range is
		//seeded degenerate (min = max = the seed radius), so a caller that never hands one over has a gun that
		//simply does not walk — there is no meaningful default: the range is solved per level off the field's
		//footprint (GameCameraFit), which this type never learns.
		private float _advanceMin, _advanceMax;
		private float _advanceDelta = 0f;
		private float _advanceDeltaLastSet = 0f;
		private float _advanceAcceleration = 0f;
		private bool _advanceBraking = false;

		private bool _resettingAim = false;
		private float _resetAimStep = 0f;
		private Vector2 _resetAimFrom = Vector2.Zero;

		public Cannon(Vector3 orbitCenter, float orbitRadius = 20f)
		{
			//The cannon is drawn procedurally now (a CannonMesh with the loaded balls shown in its magazine),
			//so this holds only the pose: where it orbits, where it aims and its World matrix. Position is
			//the trunnions, at the barrel's midpoint - elevating turns the barrel about them, raising the
			//muzzle and dropping the breech, so the gun stays where it stands. Its height is not a caller's
			//figure any more: the gun stands on the island's dished stone, so Position.Y is re-seated off
			//the orbit radius on every move (CannonRig.TrunnionHeightAt - the local floor plus the stack
			//the carriage hangs below the pins). The renderer builds its own look-at world from Position
			//and AimTarget, and derives the muzzle from them.
			OrbitCenter = orbitCenter;
			_orbitRadius = orbitRadius;

			//Degenerate until SetAdvanceRange: a gun nobody gave room to walk in stays where it is put
			_advanceMin = orbitRadius;
			_advanceMax = orbitRadius;

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

				//Floored: the decrement overshoots zero on the glide's last frame, and a negative value left
				//standing is a wrong-way factor in the next press's first steps — the reversal guard reads
				//"acceleration > 0" and lets the new sign in while the old momentum is still below zero
				if (_acceleration < 0f) _acceleration = 0f;
			}

			_delta = 0f;

			//The advance stroke, stepped by the very idiom the orbit above uses — ramp while held, glide out
			//on release, brake harder on a reversal — so walking and turning read as one carriage
			if (_advanceAcceleration <= 0f) _advanceBraking = false;

			if (Math.Sign(_advanceDelta) != 0)
			{
				MoveRadial(gameTime);
				if (_advanceAcceleration < 1f) _advanceAcceleration += ACCELERATION_DELTA * (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			}

			if (_advanceDelta == 0f && _advanceAcceleration > 0f)
			{
				MoveRadial(gameTime);
				_advanceAcceleration -= ACCELERATION_DELTA * (_advanceBraking ? 4f : 2f) * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

				//Same floor as the orbit's above, and here the leak was visible: a negative remnant drove the
				//gun and rolled the wheels against the next held key for its first frames
				if (_advanceAcceleration < 0f) _advanceAcceleration = 0f;
			}

			_advanceDelta = 0f;

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

		/// <summary>
		/// Walks the carriage in towards the field's centre — for a positive <paramref name="delta"/> (W) —
		/// and back out for a negative one (S), by sliding the orbit radius within the range
		/// <see cref="SetAdvanceRange"/> granted. The walk is radial (along the line the gun rests facing),
		/// not along a traversed barrel: traverse turns the aim, never the ground the carriage covers.
		/// Standing closer steepens the resting aim (the whole point: the underside cells want a shot from
		/// below — and the dish steepens it a second way, carrying the trunnions downhill as the gun walks
		/// in); the camera does not follow, not even in height (<c>GameCameraFit</c> floors its lens at the
		/// arris-stance height), so the gun visibly advances on the cluster in frame and sinks down the dish
		/// within a steady frame. Same per-held-frame ±1 protocol as <see cref="Orbit"/>, same ramp, glide
		/// and reversal brake — and the ends are rubber, not stops: see <see cref="ADVANCE_EASE_ZONE"/>.
		/// </summary>
		public void Advance(float delta)
		{
			if (Math.Sign(delta) != Math.Sign(_advanceDeltaLastSet) && _advanceAcceleration > 0f)
			{
				_advanceBraking = true;
				return;
			}

			_advanceDelta = delta;
			_advanceDeltaLastSet = delta;
		}

		/// <summary>
		/// The radii the advance stroke may walk between, solved per level (see
		/// <c>GameCameraFit.Solve</c> — the near end keeps the gun clear of the field's footprint, and the
		/// stroke is capped so the magazine keeps a readable size against a lens that does not follow).
		/// Clamps the gun into the range immediately, so a range handed over after
		/// <see cref="OrbitRadius"/> cannot leave it standing outside its own walk.
		/// </summary>
		public void SetAdvanceRange(float min, float max)
		{
			_advanceMin = min;
			_advanceMax = Math.Max(min, max);

			//A fresh range means a fresh stand: any glide still running belonged to the walk the old range
			//framed, and left alive it would carry the gun off the rest the caller has just parked it at
			_advanceAcceleration = 0f;
			_advanceDelta = 0f;
			_advanceBraking = false;

			float clamped = Math.Clamp(_orbitRadius, _advanceMin, _advanceMax);
			if (clamped != _orbitRadius)
			{
				_orbitRadius = clamped;
				MoveToOrbitAngle();
			}
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
			_advanceAcceleration = 0f;

			//A restarted gun stands at rest: a stroke caught mid-flight by a teardown must not carry into
			//the next session's first frame
			_recoilPhase = 0f;

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
			float step = RotationSpeed * _acceleration * _deltaLastSet * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

			_orbitAngle += step;

			//The arc the carriage's own centre covers, which is what the sideways ground under it measures.
			//Accumulated from the STEP and not from the angle, so EnsureOrbitAngleInBounds' wrap below cannot
			//reach it — the same reason AdvanceTravel is accumulated from the move rather than read off the
			//radius. Both wheels see the same arc: the axle is the tangent, so the pair sits at one radius.
			OrbitTravel += _orbitRadius * step;

			EnsureOrbitAngleInBounds();
			MoveToOrbitAngle();
		}

		/// <summary>
		/// One frame of the advance stroke: the glide's step, scaled by how much room is left towards the end
		/// being approached. The scale is <b>linear in the room</b> over the last <see cref="ADVANCE_EASE_ZONE"/>
		/// units, which makes the position converge on the end exponentially — the rubber the ends are made of:
		/// the closer the gun gets the slower it moves, it never arrives and it never jolts. A step away from a
		/// cushion reads the room towards the opposite end instead, so backing out answers at once.
		/// </summary>
		private void MoveRadial(GameTime gameTime)
		{
			//Positive step walks toward the field, i.e. shrinks the radius
			float step = ADVANCE_SPEED * _advanceAcceleration * _advanceDeltaLastSet
				* (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			if (step == 0f) return;

			float room = step > 0f ? _orbitRadius - _advanceMin : _advanceMax - _orbitRadius;
			if (room <= 0f) return;

			float cushion = Math.Min(room / ADVANCE_EASE_ZONE, 1f);

			//The exponential approach cannot cross the end at ordinary frame times; the clamp catches float
			//dust and the one real crossing an oversized step can make — a hitched frame's dt is unbounded,
			//and past step = ADVANCE_EASE_ZONE the cushioned step exceeds the room it was scaled by
			float moved = _orbitRadius;
			_orbitRadius = Math.Clamp(_orbitRadius - step * cushion, _advanceMin, _advanceMax);

			//What the wheels see: ground actually covered, cushion and all, signed + toward the field
			AdvanceTravel += moved - _orbitRadius;

			MoveToOrbitAngle();
		}

		/// <summary>
		/// Ground the advance stroke has covered, in world units, signed — positive toward the field — and
		/// accumulated over the walk's whole life, cushioned ends included. It exists for the carriage's
		/// wheels: rolled by <c>travel / wheel radius</c> they turn exactly as fast as the ground passes under
		/// them, and they slow into the rubber at an end precisely because this is the <i>moved</i> distance,
		/// not the held time. Deliberately not advanced by the orbit (A/D): a carriage slewed sideways is
		/// dragged, not rolled, and wheels that spin while the gun crabs sideways read as broken.
		/// <para>
		/// That split is a fact about <b>this</b> wheel and not about the walk, and since #129 the other half
		/// is measured rather than discarded — see <see cref="OrbitTravel"/>. A spoked wheel has nothing to do
		/// with sideways ground; a wheel that has (rollers around its rim) wants exactly that figure, and the
		/// two together are the carriage's whole ground velocity in its own frame.
		/// </para>
		/// </summary>
		public float AdvanceTravel { get; private set; }

		/// <summary>
		/// Ground the <b>orbit</b> walk has covered sideways, in world units, signed — positive for a growing
		/// orbit angle — and accumulated over the walk's whole life. <see cref="AdvanceTravel"/>'s counterpart
		/// for the other axis, and it exists for the same reason: it is the <i>moved</i> distance, so anything
		/// driven off it slows into a stop exactly as the carriage does.
		/// <para>
		/// <b>The carriage's ground velocity in its own frame is these two and nothing else.</b> The axle is
		/// local X and the walk toward the field is local −Z; the orbit's tangent <i>is</i> the axle, so the
		/// advance is the rolling direction and the orbit is the sideways one. That is exactly the split an
		/// omnidirectional wheel is built around, which is what #129 wants it for: the wheel body's spin comes
		/// off <see cref="WheelTravel"/> and its rollers' spin off this. <b>Nothing draws it yet</b> — the
		/// wheel is still the spoked one, which cannot move sideways at all, and <see cref="AdvanceTravel"/>'s
		/// note above says why it is therefore right that the wheels stand still through an orbit today.
		/// </para>
		/// <para>
		/// <b>It measures the walk and not the pose</b>, so the two ways the orbit angle can be <i>set</i>
		/// rather than walked — <see cref="OrbitToFace"/> and the <see cref="OrbitRadius"/> setter — leave it
		/// alone, precisely as they leave <see cref="AdvanceTravel"/> alone. A gun that is placed does not roll
		/// there. And no recoil term belongs here, unlike <see cref="WheelTravel"/>: the shot's shove is back
		/// along the heading, which is the advance's axis and not this one.
		/// </para>
		/// </summary>
		public float OrbitTravel { get; private set; }

		#region The recoil stroke

		//The gun's answer to its own shot, and DRAWING ONLY — the true pose (AimDirection, MuzzlePosition)
		//never takes it, so nothing about where a ball goes can depend on it. It lives on the gun rather than
		//with a caller because two parts of the gun answer the one event (#115): the tube slides in its
		//cradle, and the undercarriage takes a smaller, later share of the same shock — two responses off one
		//clock, which only stays one clock if the gun owns it. The caller keeps the clock's ticks: each
		//executable calls KickRecoil when it fires and StepRecoil where it ages the magazine's glide.

		/// <summary>How far the tube is thrown back along the bore at the stroke's peak, in world units — a
		/// little over one ball diameter.</summary>
		public const float RECOIL_BACK = 1.15f;

		/// <summary>How fast the stroke plays out, per second: 1 ÷ this is the whole stroke, ~0.24 s.</summary>
		public const float RECOIL_DECAY = 4.2f;

		/// <summary>
		/// How far the whole undercarriage is shoved back at <i>its</i> peak, in world units — deliberately a
		/// fraction of <see cref="RECOIL_BACK"/> (#115): the recoil mechanism the tube slides in swallows most
		/// of the shock, and the carriage and wheels show only what leaks past it.
		/// </summary>
		public const float CARRIAGE_RECOIL_BACK = 0.22f;

		//1 at the shot, eased linearly to 0 by StepRecoil — linear so the stroke genuinely ends rather than
		//approaching zero for ever and leaving the gun permanently a hair out of place
		private float _recoilPhase;

		/// <summary>
		/// A round has left the barrel. Set, not accumulated: a recoil stroke restarts from the top with every
		/// round, it does not stack up over a burst the way a camera's trauma does.
		/// </summary>
		public void KickRecoil() => _recoilPhase = 1f;

		/// <summary>
		/// Plays the stroke out. Each executable calls it where it ages the gun's other animation, the
		/// magazine's glide — the gun owns the strokes' shapes, the caller the clock they run on.
		/// </summary>
		public void StepRecoil(float elapsedSeconds)
		{
			if (_recoilPhase > 0f) _recoilPhase = MathF.Max(0f, _recoilPhase - RECOIL_DECAY * elapsedSeconds);
		}

		/// <summary>
		/// How far back along the bore the tube is displaced this instant, in world units, exactly zero at
		/// rest. Squared rather than linear in the phase, so the shot throws the tube back at once and the
		/// return eases off — the shape a recoiling barrel has (the same reasoning as <c>CameraShake</c>'s:
		/// a linear amplitude spends most of its life mid-stroke and reads as a wobble instead of a jolt).
		/// </summary>
		public float BarrelRecoilBack => RECOIL_BACK * _recoilPhase * _recoilPhase;

		/// <summary>
		/// How far back along its heading the undercarriage is shoved this instant, in world units (#115).
		/// Zero at the shot itself and zero again at rest, peaking about 0.07 s in: the hump <c>4s(1−s)</c>
		/// over the tube's own normalized stroke <c>s</c>, so the carriage answers a beat <b>after</b> the
		/// tube — the mechanism takes the hit first and the frame shows what leaks through — and settles
		/// softly while the tube is still sliding home. One phase drives both, so they cannot drift.
		/// </summary>
		public float CarriageRecoilBack
		{
			get
			{
				float s = _recoilPhase * _recoilPhase;

				return CARRIAGE_RECOIL_BACK * 4f * s * (1f - s);
			}
		}

		/// <summary>
		/// What the wheels roll by: the advance walk's ground plus the recoil's own backward shove — the
		/// carriage genuinely moves, and wheels that held still through it would read as skidding. This is
		/// what a caller hands <c>CannonRig.DrawCarriage</c> instead of raw <see cref="AdvanceTravel"/>.
		/// </summary>
		public float WheelTravel => AdvanceTravel - CarriageRecoilBack;

		/// <summary>
		/// The undercarriage's displacement as a world-space vector: back along the same stance-projected
		/// heading the carriage's basis is built from, so the wheels slide along the stone they stand on.
		/// The tube rides its carriage, so <see cref="BarrelWorld"/> and <see cref="DrawnMuzzlePosition"/>
		/// take this too — displaced off any other point, the trunnion pins would tear out of the cheeks
		/// that visibly hold them.
		/// </summary>
		private Vector3 CarriageRecoilOffset()
		{
			float back = CarriageRecoilBack;
			if (back <= 0f) return Vector3.Zero;

			StanceBasis(out Vector3 heading, out _);

			return heading * -back;
		}

		#endregion

		private void MoveToOrbitAngle()
		{
			var x = OrbitCenter.X + (_orbitRadius * (float)Math.Cos(_orbitAngle));
			var z = OrbitCenter.Z + (_orbitRadius * (float)Math.Sin(_orbitAngle));

			//Y is re-seated with every move: the stone under the gun is the island's dish, so walking in
			//(W) carries the carriage downhill toward the drain and walking out (S) back up — the wheels
			//stay on the stone instead of grazing a plane the dish has fallen away from. An orbit (A/D)
			//holds the radius, so it recomputes the same height and moves nothing vertically.
			Position = new(x, CannonRig.TrunnionHeightAt(_orbitRadius), z);

			//The gun just moved, and the resting pitch moved with it — walking in steepens it (now twice
			//over: the radius shortens AND the trunnions ride the dish down) — while the aimed offset
			//stayed put. Re-clamped against the fresh rest, or a maxed aim walked inward rides the rising
			//rest past MaxElevation until the next mouse twitch snaps it back; near enough the centre the
			//total would cross vertical, where the barrel's world-up basis degenerates.
			UpdateRestRotation();
			EnsureAimInBounds();

			RecalculateRotation();
			RecalculateWorldMatrix();
		}

		private void CalculateInitialPositionAndAimTarget()
		{
			Position = new Vector3(OrbitCenter.X, CannonRig.TrunnionHeightAt(_orbitRadius), OrbitCenter.Z + _orbitRadius);
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

			return requiredElevation >= MinElevation && requiredElevation <= ElevationLimit
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
				&& actualXRotation <= _elevationLimit
				&& _rotationAim.Y >= -MaxTraverse
				&& _rotationAim.Y <= MaxTraverse) return;

			var x = Math.Clamp(actualXRotation, MinElevation, _elevationLimit) - _rotationToOrbitCenter.X;
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
		/// It deliberately takes <b>no recoil</b> — neither does <see cref="MuzzlePosition"/>, and only the
		/// drawn pose (<see cref="BarrelWorld"/>, <see cref="CarriageWorld"/>,
		/// <see cref="DrawnMuzzlePosition"/>) does: the recoil is <b>drawing only</b>. A shot leaves along the
		/// true aim on the frame it is fired, before the barrel has moved, so nothing about where a ball goes
		/// may depend on it — and the launch smear stays anchored at the un-recoiled muzzle, about a unit from
		/// the drawn one at the peak of the stroke, which is invisible against a seven-unit streak. Feeding the
		/// recoil in here is precisely the thing a reader would "fix" (see docs/game-session.md, "Recoil and
		/// camera shake").
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
		public Vector3 MuzzlePosition(float pivotToFrontBall) =>
			Position + AimDirection * pivotToFrontBall;

		/// <summary>
		/// The muzzle as <b>drawn</b> this instant: the true <see cref="MuzzlePosition"/> carried back by the
		/// whole recoil — the tube's slide along the bore and the undercarriage's shove under it (#115). It is
		/// the point the loaded queue hangs off (<c>Magazine.Pose</c>), because the queue rides in the bore
		/// that is drawn; everything that decides where a shot goes keeps reading <see cref="MuzzlePosition"/>,
		/// which takes no recoil at all — see the note on <see cref="AimDirection"/>.
		/// </summary>
		public Vector3 DrawnMuzzlePosition(float pivotToFrontBall) =>
			//The bore-axis offsets summed as scalars first (one normalize, one scale); the carriage's shove
			//is along a different axis and has to come in as the vector it is
			Position + AimDirection * (pivotToFrontBall - BarrelRecoilBack) + CarriageRecoilOffset();

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
		/// The matrix the carriage is drawn with: seated on the stone under the trunnions and yawed to the
		/// aim's heading, but never pitched with the <b>aim</b> — the whole point of trunnions is that the
		/// tube elevates in the cradle while the carriage sits on its wheels. What does pitch it is the
		/// <b>ground</b>: the island's walkable top is a dish, so the stance's up is the stone's own normal
		/// where the gun stands (<c>CannonRig.StanceGradeAt</c> — nose down toward the drain, easing level
		/// across the arris over the carriage's own footprint) rather than world up. Traversing still slews
		/// the carriage whole (a field gun is turned whole; slewed oblique to the dish's radial grade the
		/// basis takes the slope as part pitch, part roll, exactly as a rigid carriage standing oblique on a
		/// grade does), and elevating does not touch it. The recoil <b>does</b>, since #115, as its own
		/// smaller and later response: the tube slides in the cradle (<see cref="BarrelWorld"/> takes that
		/// stroke) and the whole undercarriage takes <see cref="CarriageRecoilBack"/>'s shove under it.
		/// <para>
		/// The heading comes off <see cref="AimDirection"/> flattened rather than off the yaw angles, so the
		/// carriage faces exactly where the drawn barrel faces by construction; the elevation clamps keep the
		/// aim well off vertical, so the flattened vector never degenerates — and dropping it onto the stance
		/// plane cannot degenerate either, the grade being a few degrees at most. Same fourth-row translation
		/// trick as <see cref="BarrelWorld"/>, same reasoning (BestPractices.md §6).
		/// </para>
		/// </summary>
		public Matrix CarriageWorld()
		{
			StanceBasis(out Vector3 heading, out Vector3 up);

			Matrix world = Matrix.CreateWorld(Vector3.Zero, heading, up);

			//The recoil's shove, in the translation alone: the whole undercarriage slides back and home
			//along the heading it faces (#115). The seat's height is deliberately not re-derived over the
			//slide — the dish's grade over CARRIAGE_RECOIL_BACK is under three hundredths of a unit, far
			//below anything the eye holds a briefly lurching carriage to.
			Vector3 pivot = Position + heading * -CarriageRecoilBack;

			world.M41 = pivot.X;
			world.M42 = pivot.Y;
			world.M43 = pivot.Z;

			return world;
		}

		/// <summary>
		/// The stance's basis: the heading the carriage faces (the aim flattened) and the up it stands on
		/// (the dished stone's own normal), each projected against the other — extracted from
		/// <see cref="CarriageWorld"/> unchanged, so the recoil's displacement
		/// (<see cref="CarriageRecoilOffset"/>) rides the very axes the carriage is drawn on.
		/// </summary>
		private void StanceBasis(out Vector3 heading, out Vector3 up)
		{
			heading = AimDirection;
			heading.Y = 0f;
			heading = Vector3.Normalize(heading);

			//The dished stone rises outward by the grade, so its normal leans inward off world up by exactly
			//that much (a height field's normal is Up minus its gradient), and the heading is dropped onto
			//the stance plane so the nose pitches downhill instead of the leading wheels digging into stone.
			//Off the dish the grade is zero and this is world up untouched.
			float grade = CannonRig.StanceGradeAt(_orbitRadius);

			up = Vector3.Up;

			if (grade != 0f)
			{
				up = Vector3.Normalize(Vector3.Up - StandBearing * grade);
				heading = Vector3.Normalize(heading - up * Vector3.Dot(heading, up));
			}
		}

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
		public Matrix BarrelWorld()
		{
			Vector3 aim = AimDirection;

			//The tube's own slide in the cradle, back along the bore — and under it the whole gun's shove,
			//back along the carriage's heading: the tube rides its carriage (#115), so the pins the cheeks
			//visibly hold it by stay married to the cradle through the stroke. Both are the gun's own state
			//(BarrelRecoilBack/CarriageRecoilBack), and both are drawing only — see AimDirection.
			Vector3 pivot = Position - aim * BarrelRecoilBack + CarriageRecoilOffset();

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
