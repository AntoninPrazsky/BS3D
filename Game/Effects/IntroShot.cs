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
        private readonly Vector3[] _lookAtPath;
        private readonly float _lookAhead;
        private readonly float _pitchDown;
        private readonly float _pitchDamping;

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
        /// <param name="lookAtPath">A MOVING point to keep the lens on (#559) — a subject that drifts while the
        /// shot runs, the dream's glass solid or a storm cell, or a truck along a front with the look carried
        /// beside it. Walked on the same clock as the path, so its points are spaced evenly in time; it wins
        /// over <paramref name="lookAt"/>.</param>
        /// <param name="pitchDamping">How much of the path's own rise and fall the view refuses to follow, 0–1
        /// (#645): 0 looks along the travel, 1 keeps the view level. A lens that hugs rolling ground and looks
        /// along its own travel tips down every dune's slip face and up its next slope, and no smoothing of the
        /// PATH can stop that, because it is the path's slope that is the pitch — measured on the desert's crest
        /// as 29 degrees a second rms and up to 80 with the height already smoothed. Only the travel-following
        /// look uses it; a shot on a fixed or moving point looks where it is told.</param>
        public IntroShot(string name, Vector3[] path, float seconds, float fieldOfView,
            Vector3? lookAt = null, float lookAhead = 12f, float pitchDownDegrees = 0f, Vector3[] lookAtPath = null,
            float pitchDamping = 0f)
        {
            if (path == null || path.Length < 2) throw new ArgumentException("A shot needs at least two points.", nameof(path));

            Name = name;
            _path = path;
            Seconds = seconds;
            FieldOfView = fieldOfView;
            _lookAt = lookAt;
            _lookAtPath = lookAtPath != null && lookAtPath.Length >= 2 ? lookAtPath : null;
            _lookAhead = lookAhead;
            _pitchDown = MathHelper.ToRadians(pitchDownDegrees);
            _pitchDamping = MathHelper.Clamp(pitchDamping, 0f, 1f);
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

            position = At(_path, eased);

            if (_lookAtPath != null)
            {
                target = At(_lookAtPath, eased);
                return;
            }

            if (_lookAt is Vector3 fixedTarget)
            {
                target = fixedTarget;
                return;
            }

            //Along the travel: the point a look-ahead further on, or on past the end in the last leg's own
            //direction, so the final frames do not swing to face a point the lens is about to overtake.
            //
            //⚠ PAST THE END THE POINT IS EXTRAPOLATED, NOT REPLACED (#645). It was the last leg's direction
            //itself, which is where the point at the end would have been looked AT only if the lens were on that
            //leg — so on the frame the look-ahead crossed the end the view swung from "towards the path's end" to
            //"along its last segment" in one step. Wherever a path bends near its end that is a jerk: on the
            //mountains' terrain-hugging pass the look-at jumped 0.34 units in height in one frame against 0.016 in
            //its neighbours (140 degrees a second for one frame). Carried on along the last leg by the distance it
            //overshoots, the point is the path's own end on the crossing frame, and moves smoothly after it.
            float spacing = Vector3.Distance(_path[0], _path[1]);
            float length = MathF.Max(spacing * (_path.Length - 1), 1e-3f);
            float ahead = eased + _lookAhead / length;
            Vector3 forward = ahead <= 1f
                ? At(_path, ahead) - position
                : _path[^1] + SafeNormalize(_path[^1] - _path[^2]) * ((ahead - 1f) * length) - position;

            if (forward.LengthSquared() < 1e-6f) forward = _path[^1] - _path[0];

            //Held towards the level (#645): the horizontal part is untouched, so the heading is the travel's own.
            if (_pitchDamping > 0f)
            {
                forward.Y *= 1f - _pitchDamping;
                if (forward.LengthSquared() < 1e-6f) forward = Vector3.UnitZ;
            }

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

        private static Vector3 SafeNormalize(Vector3 v) => v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : Vector3.Zero;

        //A path at s (0–1 of its length); the points are evenly spaced, so index space is length space.
        //
        //⚠ THROUGH the points on a Catmull-Rom, not along the polyline between them (#645). Linear, the lens
        //changed velocity at every point — invisible on a straight line, a judder on a path bent to hug terrain:
        //on the mountains' pass at 36 units a second the view turned at 5 and 15 degrees a second on alternate
        //frames, one kink per vertex. A Catmull-Rom passes through the same points with a continuous velocity,
        //so every shot keeps its course and loses its corners.
        private static Vector3 At(Vector3[] path, float s)
        {
            float index = MathHelper.Clamp(s, 0f, 1f) * (path.Length - 1);
            int i = Math.Min((int)index, path.Length - 2);

            return Vector3.CatmullRom(path[Math.Max(i - 1, 0)], path[i], path[i + 1],
                path[Math.Min(i + 2, path.Length - 1)], index - i);
        }
    }
}
