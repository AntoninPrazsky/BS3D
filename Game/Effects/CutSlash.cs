using Microsoft.Xna.Framework;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>Where the Cut's slash runs</b> (#767's verdict): the white-hot line <c>Blasts.Cut</c> draws through the storey a cutter
    /// severed. The owner saw the first one (level and square to the LENS, pulled towards it) as "neither perpendicular to the
    /// shot nor the way the storey is really cut": the overview's lens does not turn with the barrel, so a line square to it
    /// was square to the shot only when the gun aimed straight ahead, and pulled off the storey towards the lens it no longer
    /// lay where the cut was.
    /// <para>
    /// <b>The derivation.</b> The cut destroys the struck ball's storey (<c>BallsConstraintsBuilder.CutBall</c>: the struck
    /// ball's own level, flooded over its same-level neighbours) and drops what hung on it. A storey is a horizontal layer of
    /// the lattice, so the cut is a horizontal plane through the struck ball's centre; the blade comes in along the shot, so
    /// the edge it cuts with lies across the shot. The one line that is both is the intersection of the storey's plane with
    /// the plane square to the shot's level heading: through the struck ball, level (<see cref="Axis"/>.Y is 0) and square to
    /// the shot's XZ direction - which faces the player, who fired along that heading. Along it, the slash spans what was
    /// severed: the destroyed storey's balls projected onto the line, from the nearest end to the farthest, plus
    /// <see cref="OVERRUN"/> at each end so the tapered points run out past the silhouette; never less than
    /// <see cref="MIN_HALF_LENGTH"/> either side of the strike, so a cut through a storey of one still reads as a cut.
    /// </para>
    /// <para>
    /// The line is drawn where it is (<c>Blast.fx</c> pulls only its DEPTH towards the lens, so the storey's own balls
    /// either side of it, released and still in place for the first frames, do not hide it; on screen it stands exactly on
    /// the storey).
    /// </para>
    /// </summary>
    internal readonly struct CutSlash
    {
        /// <summary>How far the slash runs out past the outermost severed ball's centre at each end, world units: a ball's
        /// width, so the taper's points clear the silhouette.</summary>
        public const float OVERRUN = 1f;

        /// <summary>The shortest slash either side of the strike, world units: tapered to points, about three balls of it read
        /// bright, so a storey of one is still a cut and not a spark.</summary>
        public const float MIN_HALF_LENGTH = 2f;

        /// <summary>The line's direction: level, unit, square to the shot's level heading, and turned so that
        /// <c>Up × Axis</c> points back along the shot, at the gun (the side the sparks are thrown to).</summary>
        public readonly Vector3 Axis;

        /// <summary>Where the slash's middle is along <see cref="Axis"/> from the struck ball's centre, world units.</summary>
        public readonly float Offset;

        /// <summary>How far the slash reaches either side of its middle, world units.</summary>
        public readonly float HalfLength;

        public CutSlash(Vector3 axis, float offset, float halfLength)
        {
            Axis = axis;
            Offset = offset;
            HalfLength = halfLength;
        }

        /// <summary>
        /// The slash's direction for a shot travelling along <paramref name="shot"/> (any length): level and square to its XZ
        /// heading, <c>Up × heading</c>, so <c>Up × Axis</c> is the heading reversed. A shot with no level heading to speak
        /// of (the elevation clamp keeps every real one well off vertical, and a strike that knows none passes zero) takes
        /// <paramref name="fallback"/>'s heading instead, and failing that world X.
        /// </summary>
        public static Vector3 AxisFor(Vector3 shot, Vector3 fallback)
        {
            Vector3 heading = new(shot.X, 0f, shot.Z);
            if (heading.LengthSquared() < 1e-6f) heading = new Vector3(fallback.X, 0f, fallback.Z);
            if (heading.LengthSquared() < 1e-6f) return Vector3.UnitX;

            heading.Normalize();
            return Vector3.Normalize(Vector3.Cross(Vector3.Up, heading));
        }

        /// <summary>
        /// The slash along <paramref name="axis"/> that spans the severed storey, whose balls' centres project onto it from
        /// <paramref name="nearest"/> to <paramref name="farthest"/> (world units from the struck ball, which itself is at 0):
        /// <see cref="OVERRUN"/> past each end, and at least <see cref="MIN_HALF_LENGTH"/> either side of the strike.
        /// </summary>
        public static CutSlash Across(Vector3 axis, float nearest, float farthest)
        {
            float low = MathF.Min(MathF.Min(nearest, 0f) - OVERRUN, -MIN_HALF_LENGTH);
            float high = MathF.Max(MathF.Max(farthest, 0f) + OVERRUN, MIN_HALF_LENGTH);

            return new CutSlash(axis, (low + high) * 0.5f, (high - low) * 0.5f);
        }

        /// <summary>The slash's middle, given the struck ball's centre.</summary>
        public Vector3 Middle(Vector3 struck) => struck + Axis * Offset;
    }
}
