using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The park bench under the meadow's old oak (#609, #891, #1022), built with no device: <see cref="Wood"/>, the slats of
    /// its seat and back, and <see cref="Iron"/>, the cast ends they are bolted to. Two meshes, because a bucket carries one
    /// colour and a bench of one brown read as the owner saw it (#1022: "not very detailed, and it does not look as if it
    /// would be good to sit on", while the intro's last shot circles it close and low).
    /// <para>
    /// <b>What a person would sit on.</b> In its own units (a metre each; <see cref="MeadowScatter"/> scales the mesh to the
    /// scene's metre) the bench is 1.9 long and faces +Z. The seat is five slats on a depth of 0.43 with their gaps, its front
    /// edge 0.47 over the ground and the plane tipped 5 degrees back, so a sitter settles into the back; the back is three
    /// slats raked 12 degrees, the lowest well clear of the seat; and each end is four round cast rods: an upright that
    /// leaves the ground at the rear, bends at the seat and runs up to the top of the back, a front leg that goes up past the
    /// seat's front edge into an armrest that curls down at its front and meets the upright, and a bar under the seat that
    /// carries the slats. The slats have their top edges chamfered (a plank is not a
    /// brick) and differ by a few hundredths in length, as boards sawn by hand do.
    /// </para>
    /// <para>
    /// <b>Closed solids, clockwise from outside</b> (CLAUDE.md, "Triangle winding"; <c>WindingCheck --openness</c> and
    /// <c>MeadowMeshClosureTests</c>): a slat is a prism whose ends are fanned from the ring's own corners, a rod is
    /// <see cref="TubeGeometry.AddSweep"/> with both ends capped from its rings. A rod's end that lies inside another rod or a
    /// slat (an armrest's rear end in the upright, the bar's ends in the legs) is closed all the same.
    /// </para>
    /// <para>
    /// <b>Each part stays under 400 triangles</b>: the Potato path draws a bucket of more than 1,200 indices as a blob of its
    /// bounding sphere (<c>PotatoBackdrop</c>), and a bench that became an ellipsoid there is the fault this is here to cure.
    /// <c>MeadowBenchTests</c> hold it.
    /// </para>
    /// </summary>
    internal static class MeadowBenchGeometry
    {
        //The bench's length in its own units (the seat's slats run a little under it and over it, see SeatLengths), and
        //where the cast ends stand: each end's rods lie in the plane x = +-END_X
        internal const float LENGTH = 1.9f;
        private const float END_X = 0.86f;

        //The seat: its front edge's top (z, y), the plane tipped back by SEAT_TILT, five slats of SLAT_WIDTH, SLAT_GAP apart,
        //SLAT_THICK thick with the top edges chamfered by CHAMFER
        private const float SEAT_FRONT_Z = 0.22f, SEAT_FRONT_Y = 0.47f;
        private const float SEAT_TILT_DEGREES = 5f;
        private const int SEAT_SLATS = 5;
        private const float SLAT_WIDTH = 0.078f, SLAT_GAP = 0.012f, SLAT_THICK = 0.034f, CHAMFER = 0.008f;

        //How long each seat slat is, to either side of the middle: boards sawn by hand, a few hundredths either way
        private static readonly float[] SEAT_HALF_LENGTHS = { 0.918f, 0.938f, 0.926f, 0.942f, 0.922f };

        //The back: three slats up an upright that leans RAKE_DEGREES from the vertical, between the two uprights
        private const float RAKE_DEGREES = 12f;
        private const float BACK_WIDTH = 0.096f, BACK_THICK = 0.028f, BACK_CHAMFER = 0.006f;
        private static readonly float[] BACK_CENTRES = { 0.115f, 0.235f, 0.355f };
        private static readonly float[] BACK_HALF_LENGTHS = { 0.849f, 0.839f, 0.851f };

        //How far the rods go under the bench's origin when no ground says otherwise (a bench on level ground; the tests'): the
        //feet are buried. On the knoll each foot is seated on the ground under it (see Iron), within these limits: the deepest a
        //leg is made (the Potato path's 15-unit cells ask for 0.44, the desktop's grid 0.26), and the highest its foot may stand,
        //short of the rod's next ring: an uphill foot is then buried by more than it must be, which nothing shows
        private const float FOOT_DEPTH = -0.16f, FOOT_HIGHEST = 0.02f;
        internal const float FOOT_LOWEST = -0.5f;

        //Where each rod meets the ground and its radius there: the rear upright's and the front leg's, at either end
        private const float REAR_FOOT_Z = -0.288f, FRONT_FOOT_Z = 0.305f, REAR_FOOT_RADIUS = 0.034f, FRONT_FOOT_RADIUS = 0.036f;

        //The rods are six-sided, as the fence's are; their normals are radial, so they shade round
        private const int SIDES = 6;

        /// <summary>The bench's own bounding sphere: on its middle, a metre over the ground at the scene's metre of two.</summary>
        internal static BoundingSphere Bounds(float scale) => new(new Vector3(0f, 0.5f * scale, 0f), 1.3f * scale);

        /// <summary>The slats of the seat and the back, <paramref name="scale"/> times the bench's own size.</summary>
        internal static (List<VertexPositionNormalTexture> Vertices, List<short> Indices) Wood(float scale)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();

            float tilt = MathHelper.ToRadians(SEAT_TILT_DEGREES);
            Vector3 down = new(0f, -MathF.Sin(tilt), -MathF.Cos(tilt));   //from the seat's front to its back, along the plane
            Vector3 up = new(0f, MathF.Cos(tilt), -MathF.Sin(tilt));      //the plane's normal: it leans back with the seat
            for (int i = 0; i < SEAT_SLATS; i++)
            {
                float s = SLAT_WIDTH * 0.5f + i * (SLAT_WIDTH + SLAT_GAP);
                Vector3 top = new(0f, SEAT_FRONT_Y, SEAT_FRONT_Z);
                Vector3 centre = top + down * s - up * (SLAT_THICK * 0.5f);
                AddSlat(v, idx, centre, Vector3.UnitX, down, up, SEAT_HALF_LENGTHS[i], SLAT_WIDTH * 0.5f, SLAT_THICK * 0.5f, CHAMFER);
            }

            float rake = MathHelper.ToRadians(RAKE_DEGREES);
            Vector3 along = new(0f, MathF.Cos(rake), -MathF.Sin(rake));   //up the back's slope
            Vector3 front = new(0f, MathF.Sin(rake), MathF.Cos(rake));    //the back's face
            (float rootZ, float rootY, _) = UprightRoot();
            for (int i = 0; i < BACK_CENTRES.Length; i++)
            {
                //Its rear face against the upright's front: the rod's radius there and half the slat's thickness
                Vector3 onRod = new Vector3(0f, rootY, rootZ) + along * BACK_CENTRES[i];
                Vector3 centre = onRod + front * (UprightRadiusAt(BACK_CENTRES[i]) + BACK_THICK * 0.5f);
                AddSlat(v, idx, centre, Vector3.UnitX, along, front, BACK_HALF_LENGTHS[i], BACK_WIDTH * 0.5f, BACK_THICK * 0.5f, BACK_CHAMFER);
            }

            return Scaled(v, idx, scale);
        }

        /// <summary>
        /// Where the four feet stand in the bench's own frame, <paramref name="scale"/> times its size, as (x, z): at the end
        /// at -x its upright's and its front leg's, then the end at +x's (the order <see cref="Iron"/> takes its feet in).
        /// </summary>
        internal static (float X, float Z)[] FootSites(float scale) => new[]
        {
            (-END_X * scale, REAR_FOOT_Z * scale), (-END_X * scale, FRONT_FOOT_Z * scale),
            (END_X * scale, REAR_FOOT_Z * scale), (END_X * scale, FRONT_FOOT_Z * scale),
        };

        /// <summary>The thickest a foot is (a rod's radius where it meets the ground), <paramref name="scale"/> times the bench's size.</summary>
        internal static float FootRadius(float scale) => MathF.Max(REAR_FOOT_RADIUS, FRONT_FOOT_RADIUS) * scale;

        /// <summary>
        /// The cast ends, <paramref name="scale"/> times the bench's own size: an upright, a front leg with its armrest and a seat
        /// bar at each end. <paramref name="feet"/> is how high each foot's bottom stands over the bench's origin (negative:
        /// under it), in the same units, in <see cref="FootSites"/>'s order, so that each leg is as long as the ground under it
        /// asks (#1022: on the knoll the front legs stand on ground that falls away, and one stood in the air); null, the level
        /// ground the tests stand it on, ends every foot at the same depth.
        /// </summary>
        internal static (List<VertexPositionNormalTexture> Vertices, List<short> Indices) Iron(float scale, float[] feet = null)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();

            float rake = MathHelper.ToRadians(RAKE_DEGREES);
            Vector2 along = new(-MathF.Sin(rake), MathF.Cos(rake));       //(z, y) up the back's slope
            (float rootZ, float rootY, float rootR) = UprightRoot();

            float Foot(int i) => feet == null ? FOOT_DEPTH : Math.Clamp(feet[i] / scale, FOOT_LOWEST, FOOT_HIGHEST);

            int end = 0;
            foreach (float x in new[] { -END_X, END_X })
            {
                //The upright: out of the ground behind the seat, a little forward as it rises, bending at the seat into the back's
                //rake and ending a hand over the top slat
                AddRod(v, idx, x,
                    (REAR_FOOT_Z, Foot(2 * end), REAR_FOOT_RADIUS), (-0.275f, 0.10f, 0.030f), (rootZ, rootY, rootR),
                    (rootZ + along.X * UPRIGHT_LENGTH, rootY + along.Y * UPRIGHT_LENGTH, UPRIGHT_TOP_RADIUS));

                //The front leg: out of the ground a little forward, up past the seat's front edge (it stands clear of the slat's
                //corner), and into the armrest's underside
                AddRod(v, idx, x,
                    (FRONT_FOOT_Z, Foot(2 * end + 1), FRONT_FOOT_RADIUS), (0.275f, 0.08f, 0.030f), (0.255f, 0.46f, 0.028f), (0.245f, 0.69f, 0.028f));

                //The armrest: it curls down at the front beyond the leg, runs back a little rising, thinning, and ends inside the upright
                (float armZ, float armY) = OnUpright(0.74f);
                AddRod(v, idx, x,
                    (0.315f, 0.585f, 0.030f), (0.282f, 0.682f, 0.036f), (0.190f, 0.715f, 0.038f), (armZ, armY, 0.022f));

                //The bar the seat's slats lie on, from the front leg to the upright, a rod's radius under the slats' underside
                (float frontZ, float frontY) = UnderSeat(0.255f);
                (float rearZ, float rearY) = UnderSeat(UprightRoot().Z);
                AddRod(v, idx, x, (frontZ, frontY, BAR_RADIUS), (rearZ, rearY, BAR_RADIUS));
                end++;
            }

            return Scaled(v, idx, scale);
        }

        /// <summary>
        /// The bench as one list, the wood then the iron: what the tests measure (its length, its sphere, its nearest corner to
        /// the trunk, whether it is shut), which do not care that the two are drawn in two colours.
        /// </summary>
        internal static (List<VertexPositionNormalTexture> Vertices, List<short> Indices) Both(float scale)
        {
            (List<VertexPositionNormalTexture> v, List<short> idx) = Wood(scale);
            (List<VertexPositionNormalTexture> ironV, List<short> ironIdx) = Iron(scale);
            int offset = v.Count;
            v.AddRange(ironV);
            foreach (short i in ironIdx) idx.Add((short)(i + offset));
            return (v, idx);
        }

        //The upright's root, where it bends at the seat (z, y, radius), its length from there to its top and the radius of its end
        private static (float Z, float Y, float Radius) UprightRoot() => (-0.262f, 0.46f, 0.028f);
        private const float UPRIGHT_LENGTH = 0.47f, UPRIGHT_TOP_RADIUS = 0.022f;

        //The upright's radius at a distance up from its root (it tapers to its top)
        private static float UprightRadiusAt(float up) =>
            MathHelper.Lerp(UprightRoot().Radius, UPRIGHT_TOP_RADIUS, up / UPRIGHT_LENGTH);

        //Where the upright's axis is at a height (z, y), from the root up the rake
        private static (float Z, float Y) OnUpright(float y)
        {
            float rake = MathHelper.ToRadians(RAKE_DEGREES);
            (float rootZ, float rootY, _) = UprightRoot();
            float up = (y - rootY) / MathF.Cos(rake);
            return (rootZ - MathF.Sin(rake) * up, y);
        }

        //The seat bar's radius, and where it lies under the seat at a z (z, y): the plane's underside less the bar's radius,
        //so the slats rest on it. The plane's normal leans back with the tilt, so under it is forward of straight down
        private const float BAR_RADIUS = 0.018f;

        private static (float Z, float Y) UnderSeat(float z)
        {
            float tilt = MathHelper.ToRadians(SEAT_TILT_DEGREES);
            float below = SLAT_THICK + BAR_RADIUS;
            float s = (SEAT_FRONT_Z + below * MathF.Sin(tilt) - z) / MathF.Cos(tilt);
            return (z, SEAT_FRONT_Y - s * MathF.Sin(tilt) - below * MathF.Cos(tilt));
        }

        /// <summary>
        /// One rod in the plane x = <paramref name="x"/>: a closed six-sided sweep through the (z, y, radius) points, its ends
        /// capped from its own rings.
        /// </summary>
        private static void AddRod(List<VertexPositionNormalTexture> v, List<short> idx, float x, params (float Z, float Y, float Radius)[] points)
        {
            var path = new Vector3[points.Length];
            var radii = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                path[i] = new Vector3(x, points[i].Y, points[i].Z);
                radii[i] = points[i].Radius;
            }

            TubeGeometry.AddSweep(v, idx, SIDES, path, radii, capStart: true, capEnd: true);
        }

        /// <summary>
        /// One slat: a prism along <paramref name="lengthwise"/>, <paramref name="halfLength"/> to either side of
        /// <paramref name="centre"/>, its cross-section a rectangle <paramref name="halfWidth"/> by <paramref name="halfThick"/>
        /// in the plane of <paramref name="across"/> and <paramref name="up"/> with the two edges on the <paramref name="up"/>
        /// side chamfered. Flat faces (a chamfer catches the light as a plank's edge does), the ends fanned from the corners the
        /// sides use, so every edge belongs to two triangles.
        /// </summary>
        private static void AddSlat(List<VertexPositionNormalTexture> v, List<short> idx, Vector3 centre, Vector3 lengthwise,
            Vector3 across, Vector3 up, float halfLength, float halfWidth, float halfThick, float chamfer)
        {
            //Counter-clockwise in (across, up), so an edge's outward normal is (e.y, -e.x)
            Vector2[] profile =
            {
                new(-halfWidth, -halfThick), new(halfWidth, -halfThick), new(halfWidth, halfThick - chamfer),
                new(halfWidth - chamfer, halfThick), new(-halfWidth + chamfer, halfThick), new(-halfWidth, halfThick - chamfer),
            };

            int m = profile.Length;
            var near = new Vector3[m];
            var far = new Vector3[m];
            for (int i = 0; i < m; i++)
            {
                Vector3 p = centre + across * profile[i].X + up * profile[i].Y;
                near[i] = p - lengthwise * halfLength;
                far[i] = p + lengthwise * halfLength;
            }

            for (int i = 0; i < m; i++)
            {
                int j = (i + 1) % m;
                Vector2 edge = profile[j] - profile[i];
                Vector3 normal = Vector3.Normalize(across * edge.Y - up * edge.X);
                TubeGeometry.AddRibbon(v, idx, near[i], near[j], far[j], far[i], normal);
            }

            AddPolygon(v, idx, near, -lengthwise);
            AddPolygon(v, idx, far, lengthwise);
        }

        /// <summary>A convex polygon fanned from its first corner, wound clockwise seen from along <paramref name="normal"/>'s tip (outside).</summary>
        private static void AddPolygon(List<VertexPositionNormalTexture> v, List<short> idx, Vector3[] ring, Vector3 normal)
        {
            short first = (short)v.Count;
            foreach (Vector3 p in ring) v.Add(new VertexPositionNormalTexture(p, normal, Vector2.Zero));

            //The front face here is clockwise from outside: (b - a) x (c - a) points against the normal
            bool flip = Vector3.Dot(Vector3.Cross(ring[1] - ring[0], ring[2] - ring[0]), normal) > 0f;
            for (int i = 1; i < ring.Length - 1; i++)
            {
                idx.Add(first);
                idx.Add((short)(first + (flip ? i + 1 : i)));
                idx.Add((short)(first + (flip ? i : i + 1)));
            }
        }

        private static (List<VertexPositionNormalTexture>, List<short>) Scaled(List<VertexPositionNormalTexture> v, List<short> idx, float scale)
        {
            for (int i = 0; i < v.Count; i++)
                v[i] = new VertexPositionNormalTexture(v[i].Position * scale, v[i].Normal, v[i].TextureCoordinate);

            return (v, idx);
        }
    }
}
