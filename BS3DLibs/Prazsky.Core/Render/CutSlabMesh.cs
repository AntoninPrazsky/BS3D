using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A slab cut as crystal (#541): the box <see cref="BoxMesh"/> is, with every horizontal edge ground back in a
    /// run of facets and every vertical corner cut round in a fan of them — a faceted plate whose top and bottom
    /// faces are inset by the grind, flat-shaded facet by facet. Centred at the origin like the box, and never
    /// outside it: the widest ring is the box's own footprint (its corners cut) and the two caps lie in the box's
    /// two faces, so a figure read off the box (the footprint, the thickness, the upper face) still describes this.
    /// <para>
    /// It exists for the ceiling's glass, and the geometry is half of why that glass can bend what is behind it
    /// (see <see cref="CeilingPlate"/>): a screen-space refraction moves the image by the surface's normal, and a
    /// flat face has one normal — the whole backdrop slides and nothing reads as bent. A bevel is a prism along
    /// the plate's edge, which is where the bend is seen strongest, and a bevel ground in several facets is several
    /// prisms, each throwing its own strip of the scene; the procedural cut on the top face (<c>InstancedGlass</c>
    /// in InstancedModel.fx) is the other half.
    /// </para>
    /// <para>
    /// <b>The shape is two things, both the caller's.</b> The <b>profile</b>: the slab's edge seen in section, as
    /// points (inset from the footprint's outline, height) from the underside's rim up to the top's, one ring of
    /// facets between each consecutive pair — so a profile of seven points is six bands of facets round the plate.
    /// And the <b>outline</b>: the footprint rectangle with each corner rounded by a radius in
    /// <c>cornerFacets</c> flat cuts — every cut tangent to that corner's circle, which is what lets an inset ring
    /// be the same outline at a smaller radius, so a bevel keeps one angle all the way round a corner. The
    /// refracting shader traces the same outline analytically (<c>GlassOutline</c>), from the same figures.
    /// </para>
    /// <para>
    /// Wound through <see cref="MeshBuilder"/>, which corrects every facet against the normal it is meant to show
    /// (the winding convention in CLAUDE.md), so the cut cannot come out inside-out a facet at a time.
    /// </para>
    /// </summary>
    public sealed class CutSlabMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="sizeX">Full size along X — the widest ring's, which is the box's.</param>
        /// <param name="sizeZ">Full size along Z.</param>
        /// <param name="profile">The edge in section, from the underside's rim to the top's: X is how far in from
        /// the footprint's outline, Y the height about the slab's centre. The first point is the underside's rim and
        /// the last the top's, so their heights are the slab's two faces; the widest point should be at inset zero.
        /// Every inset must stay under <paramref name="cornerRadius"/>, or a corner's ring would turn inside out.</param>
        /// <param name="cornerRadius">Radius of the circle every corner's cuts are tangent to, clamped to the
        /// smaller half-size.</param>
        /// <param name="cornerFacets">How many flat cuts round each corner, between the two axis edges.</param>
        public CutSlabMesh(GraphicsDevice device, float sizeX, float sizeZ, Vector2[] profile, float cornerRadius,
            int cornerFacets)
        {
            float hx = sizeX * Constants.HALF, hz = sizeZ * Constants.HALF;
            cornerRadius = Math.Min(cornerRadius, Math.Min(hx, hz));

            Vector3[][] rings = new Vector3[profile.Length][];
            for (int r = 0; r < profile.Length; r++)
                rings[r] = Ring(hx, hz, cornerRadius, cornerFacets, profile[r].X, profile[r].Y);

            MeshBuilder builder = new();

            //The underside the cluster hangs from and the top face, as fans from their centres
            Cap(builder, rings[0], new Vector3(0f, profile[0].Y, 0f), Vector3.Down);
            Cap(builder, rings[^1], new Vector3(0f, profile[^1].Y, 0f), Vector3.Up);

            //The bands between: one flat facet per edge of the outline, each corresponding pair of edges being
            //parallel (every ring is the same outline moved in), so every facet is a planar trapezoid
            int corners = rings[0].Length;
            for (int r = 0; r < rings.Length - 1; r++)
            {
                for (int i = 0; i < corners; i++)
                {
                    int j = (i + 1) % corners;

                    Vector3 a = rings[r][i], b = rings[r][j], c = rings[r + 1][j], d = rings[r + 1][i];

                    Vector3 normal = Vector3.Cross(b - a, d - a);
                    Vector3 outward = (a + b + c + d) * 0.25f;
                    outward.Y = 0f;
                    if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
                    normal = Vector3.Normalize(normal);

                    builder.AddQuad(a, b, c, d, normal, normal, normal, normal, normal);
                }
            }

            (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;

            float hy = Math.Max(Math.Abs(profile[0].Y), Math.Abs(profile[^1].Y));
            BoundingSphere = new BoundingSphere(Vector3.Zero, new Vector3(hx, hy, hz).Length());
        }

        /// <summary>
        /// One horizontal ring of the outline at height <paramref name="y"/>, moved <paramref name="inset"/> in
        /// along every edge's normal. Every cut is tangent to its corner's circle, centred
        /// <paramref name="radius"/> in from both of the corner's sides, so moving each one in by the inset is the
        /// same outline at radius − inset about the same centres: a corner's vertices sit on that smaller circle's
        /// circumscribed polygon, half way between the angles of consecutive cuts. The axis edges are the cuts at
        /// 0° and 90° and need no vertices of their own — they run from one corner's last vertex to the next's first.
        /// </summary>
        private static Vector3[] Ring(float hx, float hz, float radius, int facets, float inset, float y)
        {
            int perCorner = facets + 1;
            float step = MathHelper.PiOver2 / perCorner;
            float reach = (radius - inset) / MathF.Cos(step * Constants.HALF);

            Vector3[] ring = new Vector3[4 * perCorner];

            //The corners in turn round +Y, starting at +X+Z: each one's centre, and its fan from the angle it starts at
            for (int q = 0; q < 4; q++)
            {
                float sx = q == 0 || q == 3 ? 1f : -1f, sz = q < 2 ? 1f : -1f;
                Vector2 centre = new(sx * (hx - radius), sz * (hz - radius));
                float start = q * MathHelper.PiOver2;

                for (int k = 0; k < perCorner; k++)
                {
                    float angle = start + (k + Constants.HALF) * step;
                    ring[q * perCorner + k] = new Vector3(centre.X + reach * MathF.Cos(angle), y, centre.Y + reach * MathF.Sin(angle));
                }
            }

            return ring;
        }

        private static void Cap(MeshBuilder builder, Vector3[] ring, Vector3 centre, Vector3 normal)
        {
            for (int i = 0; i < ring.Length; i++)
                builder.AddTriangle(centre, ring[i], ring[(i + 1) % ring.Length], normal, normal, normal, normal);
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }
}
