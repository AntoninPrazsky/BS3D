using Microsoft.Xna.Framework;
using System;

namespace Prazsky.BS3D.GameObjects
{
    /// <summary>
    /// The shape of the floor net (#941), in one copy: where its corners stand, how many beams run each way, and where every
    /// point of every beam lies on the net's plane. The grid draws from it and the emitters aim at its corners
    /// (<see cref="LaserEmitterRing.NetCorner"/>), so the picture and the aim cannot disagree about where the net ends.
    /// <para>
    /// <b>The net ends on a boundary through its four corners, and the boundary is a beam.</b> The lattice is the ball
    /// lattice's own pitch (<see cref="BEAM_PITCH"/>), a beam on every whole step from the centre out to the corners'
    /// half-extents (<see cref="CornerFor"/>), which are where the four emitters aim. Every beam is then a line of that
    /// lattice carried through <see cref="Place"/>, which bows the lattice: the corners stay where they are and
    /// everything between them is drawn in towards the middle (<see cref="BOW"/> is negative), most at the middle of each
    /// side, so the net is a pincushion whose four points are where the emitters' cones arrive. The outermost beams are the
    /// images of the lattice's own edge, so they are the boundary - a pair of parabolas through every corner - and every
    /// other beam stops exactly where it meets one of them, so no line goes past the edge it ends on. The net reads as a
    /// sheet the four emitters throw, and not as a grid ruled on paper and cut off wider than the lines it rules.
    /// </para>
    /// <para>
    /// Everything stays in one plane: a point's height is the caller's (<c>PlaneY</c>), and the bend is in plan only.
    /// A bowed beam is a polyline of <see cref="BEAM_SEGMENTS"/> segments (a parabola's sag over one of them is a fraction
    /// of a beam's own width).
    /// </para>
    /// </summary>
    public static class LaserNetShape
    {
        /// <summary>Beam spacing is the ball lattice's own cell pitch, so the net reads as sized to the balls it waits for.</summary>
        public const float BEAM_PITCH = 1f;

        /// <summary>
        /// How far past a field's half-footprint a corner may stand: the corner is the last whole pitch within it, so the
        /// outermost cells do not sit on the net's very edge. The whole net must stay inside the drain's mouth (radius 14);
        /// the footprint of every authored level does, by a wide margin.
        /// </summary>
        public const float FOOTPRINT_MARGIN = 0.75f;

        /// <summary>
        /// How far the middle of a side stands off the straight line between its corners, as a share of the side's
        /// half-extent: a parabola through the two corners, <b>negative to draw the side in</b> (positive swells it out).
        /// Gentle, and the same for every shape of net, so a long net bows more in length and a short one less. The first
        /// build swelled out by 0.08; the owner chose the same bow drawn in, -0.08, on 2026-10-23 (#941), at the cost of
        /// the middle of a side standing up to 0.08 of its half-extent short of the footprint's edge.
        /// </summary>
        public const float BOW = -0.08f;

        /// <summary>The segments a beam is cut into (the bow's parabola is a polyline of them); even, so a beam has a vertex at its middle.</summary>
        public const int BEAM_SEGMENTS = 8;

        /// <summary>
        /// Where the net's outermost beams stand on one axis, for a field whose half-footprint on that axis is
        /// <paramref name="halfFootprint"/>: the last whole <see cref="BEAM_PITCH"/> within it and
        /// <see cref="FOOTPRINT_MARGIN"/> over it. The corner of the net, and what the emitters aim at.
        /// </summary>
        public static float CornerFor(float halfFootprint) => (int)((halfFootprint + FOOTPRINT_MARGIN) / BEAM_PITCH) * BEAM_PITCH;

        /// <summary>How many beams run across one axis whose corner stands at <paramref name="corner"/>: a centre beam and as many more each side as whole pitches fit.</summary>
        public static int BeamCount(float corner) => (int)(corner / BEAM_PITCH) * 2 + 1;

        /// <summary>Where beam <paramref name="index"/> of <paramref name="count"/> stands on the lattice's own axis, symmetric about the centre.</summary>
        public static float BeamOffset(int index, int count) => (index - (count - 1) / 2) * BEAM_PITCH;

        /// <summary>
        /// The lattice point (<paramref name="x"/>, <paramref name="z"/>), inside the corners' rectangle, carried onto the
        /// bowed net: each coordinate is stretched by <see cref="BOW"/> of itself, most where the other is at its centre
        /// and not at all where it is at the corner's, so the corners stay put and the sides bow between them.
        /// </summary>
        public static Vector2 Place(float x, float z, float cornerX, float cornerZ)
        {
            float u = x / cornerX;
            float v = z / cornerZ;

            return new Vector2(x * (1f + BOW * (1f - v * v)), z * (1f + BOW * (1f - u * u)));
        }

        /// <summary>
        /// Point <paramref name="step"/> (0 to <see cref="BEAM_SEGMENTS"/>) along a beam, in plan. A beam <paramref name="alongX"/>
        /// runs the net's length at lattice height <paramref name="offset"/> in Z, the other the width at <paramref name="offset"/> in X.
        /// </summary>
        public static Vector2 BeamPoint(bool alongX, float offset, int step, float cornerX, float cornerZ)
        {
            float t = -1f + 2f * step / BEAM_SEGMENTS;

            return alongX ? Place(t * cornerX, offset, cornerX, cornerZ) : Place(offset, t * cornerZ, cornerX, cornerZ);
        }

        /// <summary>How far the boundary stands out on the X side at plan height <paramref name="z"/>: the corner's, plus the bow (less, where it is negative).</summary>
        public static float EdgeX(float z, float cornerX, float cornerZ)
        {
            float v = z / cornerZ;

            return cornerX * (1f + BOW * (1f - v * v));
        }

        /// <summary>How far the boundary stands out on the Z side at plan position <paramref name="x"/>.</summary>
        public static float EdgeZ(float x, float cornerX, float cornerZ)
        {
            float u = x / cornerX;

            return cornerZ * (1f + BOW * (1f - u * u));
        }

        /// <summary>Whether a plan point is within the boundary (<paramref name="tolerance"/> over it is still within), which is all that is ever lit.</summary>
        public static bool IsInside(Vector2 p, float cornerX, float cornerZ, float tolerance = 1e-4f) =>
            MathF.Abs(p.X) <= EdgeX(p.Y, cornerX, cornerZ) + tolerance
            && MathF.Abs(p.Y) <= EdgeZ(p.X, cornerX, cornerZ) + tolerance;

        /// <summary>Whether a plan point is on the boundary, to within <paramref name="tolerance"/>: on one of the two sides' arcs.</summary>
        public static bool IsOnBoundary(Vector2 p, float cornerX, float cornerZ, float tolerance = 1e-4f) =>
            IsInside(p, cornerX, cornerZ, tolerance)
            && (MathF.Abs(MathF.Abs(p.X) - EdgeX(p.Y, cornerX, cornerZ)) <= tolerance
                || MathF.Abs(MathF.Abs(p.Y) - EdgeZ(p.X, cornerX, cornerZ)) <= tolerance);
    }
}
