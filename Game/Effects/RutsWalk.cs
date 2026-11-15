using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>A rover's ruts, walked for a lens to run along them</b> (the Moon's #1114, Mars's #1115): the curve the shader's
    /// ruts are drawn off (<see cref="RoverTracks.Curve"/>), finely, with the distance along it to each point. So a tracks
    /// shot lays its plan a stated distance along the ruts and finds how far along them a thing stands beside them (the
    /// Moon's flag, the point of Mars's swing nearest the arena), and its lens stays over the ruts' middle line, where no
    /// rock stands. Built once when the intro begins.
    /// </summary>
    internal sealed class RutsWalk
    {
        //How finely the curve is walked: points a span of the traverse's controls
        private const int STEPS_PER_SPAN = 64;

        private readonly Vector2[] _curve;
        private readonly float[] _along;

        /// <param name="controls">The traverse as designed (<see cref="MoonPlanting.TRAVERSE"/>, <see cref="MarsTracks.TRAVERSE"/>).</param>
        public RutsWalk(IReadOnlyList<Vector2> controls)
        {
            _curve = RoverTracks.Curve(controls, STEPS_PER_SPAN);
            _along = new float[_curve.Length];
            for (int i = 1; i < _curve.Length; i++) _along[i] = _along[i - 1] + Vector2.Distance(_curve[i - 1], _curve[i]);
        }

        /// <summary>How long the ruts are, from their first control to their last.</summary>
        public float Length => _along[^1];

        /// <summary>How far along the ruts the point of the walked curve nearest <paramref name="point"/> stands.</summary>
        public float AlongNearest(Vector2 point)
        {
            int nearest = 0;
            for (int i = 1; i < _curve.Length; i++)
                if (Vector2.DistanceSquared(_curve[i], point) < Vector2.DistanceSquared(_curve[nearest], point)) nearest = i;

            return _along[nearest];
        }

        /// <summary>The point <paramref name="distance"/> along the ruts, between the two walked points either side of it.</summary>
        public Vector2 At(float distance)
        {
            int i = Array.BinarySearch(_along, distance);
            if (i >= 0) return _curve[i];

            i = Math.Clamp(~i, 1, _curve.Length - 1);
            float span = _along[i] - _along[i - 1];
            return Vector2.Lerp(_curve[i - 1], _curve[i], span > 1e-6f ? (distance - _along[i - 1]) / span : 0f);
        }

        /// <summary><paramref name="points"/> points evenly spaced along the ruts, from <paramref name="from"/> units along them to
        /// <paramref name="to"/>: a plan for <see cref="AridIntroPaths.Hug"/> to lay a lens on.</summary>
        public Vector2[] Plan(float from, float to, int points)
        {
            var plan = new Vector2[points];
            for (int i = 0; i < points; i++) plan[i] = At(MathHelper.Lerp(from, to, i / (float)(points - 1)));

            return plan;
        }
    }
}
