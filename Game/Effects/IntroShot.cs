using Microsoft.Xna.Framework;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// One shot of a chapter intro's prologue (#488): a camera move along a fixed path, cut to at its start
    /// and cut away from at its end. What <see cref="ChapterIntro"/>'s own tour cannot be — the tour is one
    /// continuous spline round the arena, so every frame of it is a blend of its neighbours, and a street
    /// ninety units under the island or the far side of a tower corner is not somewhere a spline round the
    /// arena can pass through without going through the city on its way.
    /// <para>
    /// <b>The path is a dense polyline, not a curve.</b> A shot that turns a street corner has to keep off the
    /// block it turns round, and a spline through three points cuts that corner — the midpoint of a quadratic
    /// through a street, its intersection and the next street lies deep inside the block. So the builder lays
    /// the path out exactly (straight, a fillet arc, straight) and this only walks it; points are spaced evenly
    /// along the path, so a uniform clock is a uniform speed.
    /// </para>
    /// <para>
    /// <b>Built once when the intro begins</b>, never per frame: the arrays are allocated there and only read
    /// here, so a running shot allocates nothing.
    /// </para>
    /// </summary>
    internal sealed class IntroShot
    {
        /// <summary>What the shot is of, for the intro's one log line.</summary>
        public string Name { get; }

        /// <summary>How long the shot holds before the cut to the next.</summary>
        public float Seconds { get; }

        /// <summary>The shot's vertical field of view, in radians — a wider lens reads speed and height.</summary>
        public float FieldOfView { get; }

        private readonly Vector3[] _path;
        private readonly Vector3? _lookAt;
        private readonly float _lookAhead;
        private readonly float _pitchDown;

        /// <param name="name">What the shot is of.</param>
        /// <param name="path">The lens's path, at least two points, evenly spaced along it.</param>
        /// <param name="seconds">How long the shot runs.</param>
        /// <param name="fieldOfView">Vertical field of view, radians.</param>
        /// <param name="lookAt">A fixed point to keep the lens on (a crane over a plaza), or null to look
        /// along the direction of travel.</param>
        /// <param name="lookAhead">How far ahead along the path the lens looks when it follows the travel —
        /// far enough that a corner is turned into rather than snapped round.</param>
        /// <param name="pitchDownDegrees">How far below the direction of travel the lens looks — the street's
        /// paint is drawn flat and reads only from above it, never edge-on.</param>
        public IntroShot(string name, Vector3[] path, float seconds, float fieldOfView,
            Vector3? lookAt = null, float lookAhead = 12f, float pitchDownDegrees = 0f)
        {
            if (path == null || path.Length < 2) throw new ArgumentException("A shot needs at least two points.", nameof(path));

            Name = name;
            _path = path;
            Seconds = seconds;
            FieldOfView = fieldOfView;
            _lookAt = lookAt;
            _lookAhead = lookAhead;
            _pitchDown = MathHelper.ToRadians(pitchDownDegrees);
        }

        /// <summary>
        /// The pose at <paramref name="t"/> (0–1 of the shot). The clock is eased only a little at the ends —
        /// a cut lands on a camera already moving, which is what makes it read as a cut between two moving
        /// shots rather than a slideshow of starts and stops.
        /// </summary>
        public void Pose(float t, out Vector3 position, out Vector3 target)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            float eased = MathHelper.Lerp(t, t * t * (3f - 2f * t), 0.3f);

            position = At(eased);

            if (_lookAt is Vector3 fixedTarget)
            {
                target = fixedTarget;
                return;
            }

            //Along the travel: the point a look-ahead further on, or on past the end in the last leg's own
            //direction, so the final frames do not swing to face a point the lens is about to overtake.
            float spacing = Vector3.Distance(_path[0], _path[1]);
            float ahead = eased + _lookAhead / MathF.Max(spacing * (_path.Length - 1), 1e-3f);
            Vector3 forward = ahead <= 1f
                ? At(ahead) - position
                : _path[^1] - _path[^2];

            if (forward.LengthSquared() < 1e-6f) forward = _path[^1] - _path[0];
            forward.Normalize();

            //Tilted down about the horizontal axis across the travel, so the street is in the lower half
            //of the frame and the towers above it.
            Vector3 across = Vector3.Cross(forward, Vector3.Up);
            if (across.LengthSquared() > 1e-6f)
            {
                across.Normalize();
                forward = Vector3.Transform(forward, Matrix.CreateFromAxisAngle(across, -_pitchDown));
            }

            target = position + forward * 10f;
        }

        //The path at s (0–1 of its length); the points are evenly spaced, so index space is length space.
        private Vector3 At(float s)
        {
            float index = MathHelper.Clamp(s, 0f, 1f) * (_path.Length - 1);
            int i = Math.Min((int)index, _path.Length - 2);

            return Vector3.Lerp(_path[i], _path[i + 1], index - i);
        }
    }
}
