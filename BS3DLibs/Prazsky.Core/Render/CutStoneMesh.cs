using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>A cut stone</b> for the trophy cup's settings (#1250): a round brilliant's crown, girdle radius 1 at y = 0 on its
    /// own +Y like every ornament (<see cref="TrophyOrnaments"/>), so <see cref="TrophyMesh.Ornament"/> places it as it placed
    /// the cabochon. The owner asked for cut stones in place of the domes, "like we have the glass ceiling", and the cut is
    /// what makes a stone throw a different light from every facet as the cup turns.
    /// <para>
    /// <b>The crown of a round brilliant, eight-fold</b>: an octagonal TABLE on top, eight STAR facets round it, eight kite
    /// BEZEL facets down to the girdle and sixteen UPPER GIRDLE facets between them, 33 in all; then a sixteen-sided GIRDLE
    /// wall and a flat back that the setting hides. Every facet is a plane with its own normal (split vertices), so the
    /// light breaks at every edge: a kite's four corners are laid in one plane (<see cref="KiteSlope"/>), not two
    /// triangles folded along a diagonal. The pavilion under the girdle is never seen (the collet holds the stone to y =
    /// -0.30), so it is not cut.
    /// </para>
    /// <para>
    /// ⚠ <b>The crown stands higher than a real brilliant's</b> (a 40° bezel against the trade's 34.5°, and the girdle wall
    /// up to <see cref="GIRDLE_TOP"/>): the collet's rolled rim laps the girdle to about 0.24 above it, and at the trade's
    /// angle the rim covered half the bezels.
    /// </para>
    /// </summary>
    public sealed class CutStoneMesh : IProceduralMesh, IDisposable
    {
        /// <summary>The table's corners' radius, as a share of the girdle's: the trade's 53-57 %.</summary>
        public const float TABLE_RADIUS = 0.56f;

        /// <summary>How far out the star facets reach, along their own bearing (between two table corners).</summary>
        public const float STAR_RADIUS = 0.78f;

        /// <summary>The top of the girdle's wall, where the bezels and the upper girdle facets begin.</summary>
        public const float GIRDLE_TOP = 0.10f;

        /// <summary>The bezels' angle to the girdle plane, in degrees.</summary>
        public const float BEZEL_ANGLE = 40f;

        /// <summary>The bottom of the stone, under the setting (the cabochon's skirt went as deep).</summary>
        public const float BACK = -0.30f;

        private const int MAINS = 8;

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        public CutStoneMesh(GraphicsDevice device)
        {
            MeshBuilder builder = new();
            Build(builder);

            (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;
            BoundingSphere = new BoundingSphere(Vector3.Zero, 1.05f);
        }

        /// <summary>The table's height: the bezels rise at <see cref="BEZEL_ANGLE"/> from the girdle to the table's corners.</summary>
        public static float TableY => GIRDLE_TOP + KiteSlope * (1f - TABLE_RADIUS);

        //How much a bezel rises per unit it runs in along its own meridian
        private static float KiteSlope => MathF.Tan(MathHelper.ToRadians(BEZEL_ANGLE));

        /// <summary>The facets, for the mesh and for a test with no device.</summary>
        internal static void Build(MeshBuilder builder)
        {
            float step = MathHelper.TwoPi / MAINS;
            float tableY = TableY;

            Vector3 At(float radius, float angle, float y) => new(radius * MathF.Cos(angle), y, radius * MathF.Sin(angle));

            //A star's tip lies on the plane of BOTH kites beside it (each is symmetric about its own main's meridian), so
            //its height is the bezel plane's at its distance along a main's meridian: cos of half a step out
            float starY = GIRDLE_TOP + KiteSlope * (1f - STAR_RADIUS * MathF.Cos(step * 0.5f));

            Vector3[] table = new Vector3[MAINS], star = new Vector3[MAINS], girdle = new Vector3[2 * MAINS];
            for (int k = 0; k < MAINS; k++)
            {
                table[k] = At(TABLE_RADIUS, k * step, tableY);
                star[k] = At(STAR_RADIUS, (k + 0.5f) * step, starY);
            }

            for (int j = 0; j < 2 * MAINS; j++) girdle[j] = At(1f, j * step * 0.5f, GIRDLE_TOP);

            //The table, a fan about its centre
            Vector3 top = new(0f, tableY, 0f);
            for (int k = 0; k < MAINS; k++) Facet(builder, top, table[k], table[(k + 1) % MAINS]);

            for (int k = 0; k < MAINS; k++)
            {
                int next = (k + 1) % MAINS, previous = (k + MAINS - 1) % MAINS;

                //The star between two table corners
                Facet(builder, table[k], table[next], star[k]);

                //The kite under table corner k: its corner, the stars either side and the girdle's main point below it,
                //one plane, laid as two triangles that share its normal
                Vector3 kiteNormal = Normal(table[k], star[previous], girdle[2 * k]);
                Facet(builder, table[k], star[previous], girdle[2 * k], kiteNormal);
                Facet(builder, table[k], girdle[2 * k], star[k], kiteNormal);

                //The two upper girdle facets under star k
                Facet(builder, star[k], girdle[2 * k], girdle[2 * k + 1]);
                Facet(builder, star[k], girdle[2 * k + 1], girdle[(2 * k + 2) % (2 * MAINS)]);
            }

            //The girdle's wall, sixteen flats down to the back, and the back itself
            Vector3 back = new(0f, BACK, 0f);
            for (int j = 0; j < 2 * MAINS; j++)
            {
                Vector3 a = girdle[j], b = girdle[(j + 1) % (2 * MAINS)];
                Vector3 c = new(b.X, BACK, b.Z), d = new(a.X, BACK, a.Z);
                Vector3 outward = Vector3.Normalize(new Vector3(a.X + b.X, 0f, a.Z + b.Z));

                builder.AddQuad(a, b, c, d, outward, outward, outward, outward, outward);
                builder.AddTriangle(back, d, c, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY);
            }
        }

        //A facet facing out and up from the stone's centre, flat: every corner takes the plane's normal
        private static void Facet(MeshBuilder builder, Vector3 a, Vector3 b, Vector3 c) => Facet(builder, a, b, c, Normal(a, b, c));

        private static void Facet(MeshBuilder builder, Vector3 a, Vector3 b, Vector3 c, Vector3 normal) =>
            builder.AddTriangle(a, b, c, normal, normal, normal, normal);

        //The plane's normal, turned to face away from the stone's heart (a little under the table, on the axis)
        private static Vector3 Normal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            Vector3 centre = (a + b + c) / 3f - new Vector3(0f, GIRDLE_TOP, 0f);
            return Vector3.Dot(n, centre) < 0f ? -n : n;
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
        }
    }
}
