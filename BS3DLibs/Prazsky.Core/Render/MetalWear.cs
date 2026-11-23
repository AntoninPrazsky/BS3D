using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One corner of a turned part's profile (#1199): where it stands along the axis and off it, and which kind it is. An
    /// <see cref="Edge"/> is an outside corner, a crest the hands, the rounds and the rags pass over, which the wear rubs
    /// back to bright metal; otherwise it is an inside corner, a join where a ring meets the tube, where tarnish and grime
    /// gather.
    /// </summary>
    public readonly struct WearCorner
    {
        public readonly float Axial;
        public readonly float Radial;
        public readonly bool Edge;

        public WearCorner(float axial, float radial, bool edge)
        {
            Axial = axial;
            Radial = radial;
            Edge = edge;
        }
    }

    /// <summary>
    /// How one part's metal is worn by use (#1199), for <see cref="InstancedModelRenderer.Wear"/>: the
    /// <c>InstancedWornMetal</c> technique (<c>InstancedModel/WornMetal.fxh</c>) draws the plain material with a patina's
    /// mottle, oxidation, grime, rubbed edges, scratches and pits laid into it, in the mesh's own space. The figures are
    /// the uniforms that technique reads; the look itself is the shader's constants.
    /// <para>
    /// A part's profile corners are what put the tarnish in its joins and the polish on its crests: every worn part of the
    /// gun and the emitters but the carriage's plates and the yoke is a solid of revolution, so a corner is a point of its
    /// profile and the shader measures to it in two floats. A screen-space curvature cannot find them (derivatives never
    /// cross a triangle's edge, and a crease is two triangles), and a vertex attribute would be a second vertex layout.
    /// </para>
    /// </summary>
    public sealed class MetalWear
    {
        /// <summary>The corner slots the shader reads (<c>WEAR_CORNER_SLOTS</c>), two corners to a slot.</summary>
        public const int CORNER_SLOTS = 8;

        /// <summary>The most corners a part may name.</summary>
        public const int MAX_CORNERS = 2 * CORNER_SLOTS;

        /// <summary>
        /// How much of each flaw the part carries, 0 to 1: X the use (scratches and rubbed edges), Y the oxidation, Z the
        /// grime, W the patina's mottle (<c>WearAmount</c>).
        /// </summary>
        public Vector4 Amount { get; }

        /// <summary>
        /// The way the scratches run in the mesh's own space, unit (XYZ), zero for every way; W 1 turns them round the
        /// profile's axis on the faces that look along it, a machined disc's rings (<c>WearGrain</c>).
        /// </summary>
        public Vector4 Grain { get; }

        /// <summary>
        /// The axis the part is turned about, through the mesh's origin (XYZ, unit); W 0 for no profile, 1 for one, 2 for
        /// a pair mirrored across the origin along the axis (<c>WearProfile</c>).
        /// </summary>
        public Vector4 Profile { get; }

        /// <summary>
        /// How far the corners' wear reaches, in world units: X from a join (the tarnish and grime), Y from an edge (the
        /// rub) (<c>WearBands</c>). As wide as the steps the part's corners are on: the barrel's rings stand 0.055 proud and
        /// their dirt lies out onto the tube, a wheel's hub face is a few hundredths high.
        /// </summary>
        public Vector4 Bands { get; }

        /// <summary>The corners packed as the shader reads them: two to a slot, (axial, ±radial, axial, ±radial), the
        /// radial positive for an edge and negative for a join, zero for an empty place (<c>WearCorners</c>).</summary>
        public Vector4[] Corners { get; }

        /// <summary>The corners as they were named, for a test or a reader.</summary>
        public IReadOnlyList<WearCorner> Named { get; }

        /// <param name="amount">See <see cref="Amount"/>.</param>
        /// <param name="grain">The scratches' direction in mesh space; zero for every way (normalised here).</param>
        /// <param name="aroundAxis">Whether the scratches turn round <paramref name="axis"/> on the faces that look along it.</param>
        /// <param name="axis">The profile's axis in mesh space, or zero for a part with no profile.</param>
        /// <param name="mirrored">Whether the profile is a pair mirrored across the origin (a wheel's two faces): the
        /// corners are then named at their distance along the axis, both sides at once.</param>
        /// <param name="corners">The profile's corners; at most <see cref="MAX_CORNERS"/>.</param>
        /// <param name="joinWidth">How far a join's tarnish and grime reach (see <see cref="Bands"/>).</param>
        /// <param name="edgeWidth">How far an edge's rub reaches.</param>
        public MetalWear(Vector4 amount, Vector3 grain, bool aroundAxis = false, Vector3 axis = default, bool mirrored = false,
            IReadOnlyList<WearCorner> corners = null, float joinWidth = 0.045f, float edgeWidth = 0.02f)
        {
            if (joinWidth <= 0f || edgeWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(joinWidth), "A band needs a width.");

            corners ??= Array.Empty<WearCorner>();
            if (corners.Count > MAX_CORNERS)
                throw new ArgumentException($"{corners.Count} corners, the shader reads {MAX_CORNERS}.", nameof(corners));
            if (corners.Count > 0 && axis == Vector3.Zero)
                throw new ArgumentException("Corners need the axis they lie about.", nameof(axis));

            Amount = amount;
            Grain = new Vector4(grain == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(grain), aroundAxis ? 1f : 0f);

            //A part with no profile still states a unit axis: the shader builds a basis round it whether or not it reads
            //a corner, and the normalisation of a zero vector is a NaN
            bool hasProfile = axis != Vector3.Zero;
            Profile = new Vector4(hasProfile ? Vector3.Normalize(axis) : Vector3.Up, !hasProfile ? 0f : mirrored ? 2f : 1f);

            Bands = new Vector4(joinWidth, edgeWidth, 0f, 0f);
            Named = corners;
            Corners = Pack(corners);
        }

        /// <summary>
        /// The corners two to a slot, the kind in the radial's sign. A corner on the axis itself has no ring to wear and
        /// would read as an empty place, so it is refused.
        /// </summary>
        public static Vector4[] Pack(IReadOnlyList<WearCorner> corners)
        {
            var packed = new Vector4[CORNER_SLOTS];

            for (int i = 0; i < corners.Count; i++)
            {
                WearCorner c = corners[i];
                if (c.Radial <= 0f) throw new ArgumentException($"Corner {i} lies on the axis.", nameof(corners));

                float radial = c.Edge ? c.Radial : -c.Radial;
                int slot = i / 2;

                if (i % 2 == 0) { packed[slot].X = c.Axial; packed[slot].Y = radial; }
                else { packed[slot].Z = c.Axial; packed[slot].W = radial; }
            }

            return packed;
        }

        /// <summary>
        /// The corners of a turned profile: every creased point that is not on the axis, an edge where the profile turns
        /// away from the outside and a join where it turns into it. <paramref name="points"/> are (axial, radial);
        /// <paramref name="latheWinding"/> says which side is out: true for <see cref="LatheMesh"/>'s convention (a wall
        /// described down the axis faces away from it), false for one described up the axis that faces away from it
        /// (<see cref="CannonMesh"/>'s outer profile, muzzle to breech). The ends of the list are not corners: what
        /// lies beyond them is not known here, and a caller that knows names them itself.
        /// </summary>
        public static void AddProfileCorners(List<WearCorner> into, IReadOnlyList<Vector2> points, IReadOnlyList<bool> crease,
            bool latheWinding)
        {
            float side = latheWinding ? 1f : -1f;

            for (int k = 1; k < points.Count - 1; k++)
            {
                if (!crease[k] || points[k].Y <= 1e-5f) continue;

                Vector2 t1 = points[k] - points[k - 1];
                Vector2 t2 = points[k + 1] - points[k];

                //The run arriving here: its outward normal in (axial, radial), and whether the next run turns away from it
                Vector2 normal = side * new Vector2(t1.Y, -t1.X);
                float turn = Vector2.Dot(normal, t2);
                if (MathF.Abs(turn) < 1e-6f) continue;

                into.Add(new WearCorner(points[k].X, points[k].Y, edge: turn < 0f));
            }
        }

        /// <summary>
        /// Corners of one kind nearer than this to one already named are one band of wear and are named once: a chamfer a
        /// hundredth of a unit wide is one rubbed edge, not two. The parts merged with it, the wheels and the emitters, take
        /// their bands from <c>CannonRig.SMALL_EDGE_WIDTH</c> and <c>SMALL_JOIN_WIDTH</c>, each reaching more than half this
        /// from its corner, so the bands of two corners of one kind nearer than it would overlap into one anyway.
        /// </summary>
        public const float MERGE_DISTANCE = 0.02f;

        /// <summary>The corners in order with every one dropped that lies within <paramref name="distance"/> of one of its
        /// own kind already kept.</summary>
        public static List<WearCorner> Merge(IEnumerable<WearCorner> named, float distance)
        {
            var kept = new List<WearCorner>();

            foreach (WearCorner corner in named)
            {
                bool known = false;
                foreach (WearCorner k in kept)
                    known |= k.Edge == corner.Edge
                        && Vector2.Distance(new Vector2(k.Axial, k.Radial), new Vector2(corner.Axial, corner.Radial)) < distance;

                if (!known) kept.Add(corner);
            }

            return kept;
        }

        /// <summary><see cref="AddProfileCorners(List{WearCorner}, IReadOnlyList{Vector2}, IReadOnlyList{bool}, bool)"/>
        /// over a <see cref="LatheMesh"/> profile: (radius, height) as the lathe takes it, height along the axis.</summary>
        public static void AddLatheCorners(List<WearCorner> into, IReadOnlyList<LathePoint> profile)
        {
            var points = new Vector2[profile.Count];
            var crease = new bool[profile.Count];

            for (int i = 0; i < profile.Count; i++)
            {
                points[i] = new Vector2(profile[i].Y, profile[i].Radius);
                crease[i] = profile[i].Crease;
            }

            AddProfileCorners(into, points, crease, latheWinding: true);
        }
    }
}
