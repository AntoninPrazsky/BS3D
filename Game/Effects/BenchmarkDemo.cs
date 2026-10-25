using Microsoft.Xna.Framework;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The benchmark's action (#1018): the gun firing at a hanging level by itself while a lens that is not the play camera
    /// films it. The owner: "the benchmark should also contain scenes where something happens - the cannon fires at the map,
    /// balls fall - with a camera other than the game's, since the player is not playing". This is the camera and the clock;
    /// the session picks what the gun aims at and fires (<c>GameplayScreen.StepBenchmarkDemo</c>).
    /// <para>
    /// <b>The lens circles the gun</b>, close and low, so the barrel firing fills the foreground and the cluster it fires at
    /// hangs over it: <see cref="RADIUS_SHARE"/> of the play camera's distance out from the gun and <see cref="HEIGHT"/> over
    /// it, looking at a point half way from the gun to the cluster's centre. It starts <see cref="START_ANGLE"/> round to the
    /// gun's side (0 is straight behind it, looking along the shot) and travels <see cref="SWEEP"/> over the run, through the
    /// view over the gun's shoulder to its other side, eased at both ends, and is blended in from the play pose over
    /// <see cref="BLEND_IN"/>. Pure: <c>BenchmarkDemoTests</c> hold it.
    /// </para>
    /// </summary>
    internal sealed class BenchmarkDemo
    {
        /// <summary>How long the action runs: enough shots for several groups to fall and both cuts to strike.</summary>
        internal const float SECONDS = 24f;

        /// <summary>The blend from the play pose to the lens, smoothstepped.</summary>
        internal const float BLEND_IN = 1.5f;

        /// <summary>The lens's start, in radians round the gun from straight behind it, to its side.</summary>
        internal const float START_ANGLE = 1.25f;

        /// <summary>How far round the gun the lens travels over the run, in radians: past behind it to its other side.</summary>
        internal const float SWEEP = -1.9f;

        /// <summary>The lens's distance from the gun, as a share of the play camera's distance from the centre.</summary>
        internal const float RADIUS_SHARE = 0.27f;

        /// <summary>The lens's height over the gun, in units.</summary>
        internal const float HEIGHT = 3.2f;

        /// <summary>Where the lens looks, between a point over the gun (0) and the cluster's centre (1).</summary>
        internal const float LOOK_SHARE = 0.5f;

        /// <summary>How high over the gun the near end of the look is, in units.</summary>
        internal const float LOOK_OVER_GUN = 1.5f;

        /// <summary>A gentle rise and fall of the lens, in units, and its period in seconds.</summary>
        internal const float BOB = 0.6f, BOB_PERIOD = 9f;

        /// <summary>The lens's field of view, as a multiple of the play camera's: wider, so the gun and the cluster both fit.</summary>
        internal const float FOV_SCALE = 1.25f;

        private Vector3 _centre, _gun, _toCluster, _side;
        private float _radius, _fov;

        internal bool Running { get; private set; }

        /// <summary>The seconds since <see cref="Begin"/>.</summary>
        internal float Age { get; private set; }

        /// <summary>Whether the run has had its <see cref="SECONDS"/>.</summary>
        internal bool Done => Running && Age >= SECONDS;

        internal Vector3 Position { get; private set; }
        internal Vector3 Target { get; private set; }
        internal float FieldOfView { get; private set; }

        /// <summary>How far the lens has taken the camera from the play pose, 0 to 1.</summary>
        internal float Blend => Running ? MathHelper.SmoothStep(0f, 1f, Math.Clamp(Age / BLEND_IN, 0f, 1f)) : 0f;

        /// <param name="centre">The cluster's centre: the field's middle at the cluster's height.</param>
        /// <param name="gun">Where the gun stands.</param>
        /// <param name="distance">The play camera's distance from the centre.</param>
        /// <param name="fov">The play camera's field of view.</param>
        internal void Begin(Vector3 centre, Vector3 gun, float distance, float fov)
        {
            _centre = centre;
            _gun = gun;
            Vector3 flat = new(centre.X - gun.X, 0f, centre.Z - gun.Z);
            _toCluster = flat.LengthSquared() > 1e-6f ? Vector3.Normalize(flat) : Vector3.Forward;
            _side = Vector3.Normalize(Vector3.Cross(_toCluster, Vector3.Up));
            _radius = distance * RADIUS_SHARE;
            _fov = fov;
            Running = true;
            Age = 0f;
            Pose(0f, out Vector3 position, out Vector3 target, out float fieldOfView);
            Position = position;
            Target = target;
            FieldOfView = fieldOfView;
        }

        internal void Stop() => Running = false;

        internal void Step(float elapsed)
        {
            if (!Running) return;
            Age += elapsed;
            Pose(Age, out Vector3 position, out Vector3 target, out float fieldOfView);
            Position = position;
            Target = target;
            FieldOfView = fieldOfView;
        }

        /// <summary>The angle round the gun at <paramref name="age"/> seconds, 0 straight behind it.</summary>
        internal float AngleAt(float age) => START_ANGLE + SWEEP * MathHelper.SmoothStep(0f, 1f, Math.Clamp(age / SECONDS, 0f, 1f));

        /// <summary>The lens at <paramref name="age"/> seconds into the run.</summary>
        internal void Pose(float age, out Vector3 position, out Vector3 target, out float fieldOfView)
        {
            float angle = AngleAt(age);
            Vector3 round = -_toCluster * MathF.Cos(angle) + _side * MathF.Sin(angle);
            position = _gun + round * _radius + Vector3.Up * (HEIGHT + BOB * MathF.Sin(MathF.Tau * age / BOB_PERIOD));
            target = Vector3.Lerp(_gun + Vector3.Up * LOOK_OVER_GUN, _centre, LOOK_SHARE);
            fieldOfView = _fov * FOV_SCALE;
        }
    }
}
