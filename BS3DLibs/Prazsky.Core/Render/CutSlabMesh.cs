using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A slab cut as crystal (#541): the box <see cref="BoxMesh"/> is, with every horizontal edge bevelled at 45°
    /// and the four vertical corners cut across — an octagonal plate whose top and bottom faces are inset by the
    /// bevel, flat-shaded facet by facet. Centred at the origin like the box, and never outside it: the widest
    /// ring is the box's own footprint and the two caps are its two faces, so a figure read off the box (the
    /// footprint, the thickness, the upper face) still describes this.
    /// <para>
    /// It exists for the ceiling's glass, and the geometry is half of why that glass can bend what is behind it
    /// (see <see cref="CeilingPlate"/>): a screen-space refraction moves the image by the surface's normal, and a
    /// flat face has one normal — the whole backdrop slides and nothing reads as bent. A bevel is a prism along
    /// the plate's edge, which is where the bend is seen strongest; the procedural cut on the top face
    /// (<c>InstancedGlass</c> in InstancedModel.fx) is the other half.
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

        //The octagon's corners in order round the slab, starting on the +Z edge and turning towards -X
        private const int CORNERS = 8;

        /// <param name="sizeX">Full size along X — the widest ring's, which is the box's.</param>
        /// <param name="sizeY">Full thickness along Y.</param>
        /// <param name="sizeZ">Full size along Z.</param>
        /// <param name="bevel">How far each horizontal edge is cut back, along both of its faces (a 45° bevel).
        /// Under half the thickness, or the side band between the two bevels vanishes.</param>
        /// <param name="cornerCut">How far each vertical corner is cut back along both of its sides. At least
        /// <paramref name="bevel"/> × (2 − √2), or the inset caps' own corner cut would go negative.</param>
        public CutSlabMesh(GraphicsDevice device, float sizeX, float sizeY, float sizeZ, float bevel, float cornerCut)
        {
            float hx = sizeX * Constants.HALF, hy = sizeY * Constants.HALF, hz = sizeZ * Constants.HALF;

            bevel = Math.Clamp(bevel, 0f, hy * 0.9f);
            cornerCut = Math.Clamp(cornerCut, bevel * (2f - MathF.Sqrt(2f)), Math.Min(hx, hz));

            //Four rings from the bottom up: the underside's outline, inset by the bevel; the full outline where
            //the lower bevel meets the side; the same where the side meets the upper bevel; the top's outline
            Vector3[][] rings =
            {
                Ring(hx, hz, cornerCut, bevel, -hy),
                Ring(hx, hz, cornerCut, 0f, -hy + bevel),
                Ring(hx, hz, cornerCut, 0f, hy - bevel),
                Ring(hx, hz, cornerCut, bevel, hy)
            };

            MeshBuilder builder = new();

            //The underside the cluster hangs from and the top face, as fans from their centres
            Cap(builder, rings[0], new Vector3(0f, -hy, 0f), Vector3.Down);
            Cap(builder, rings[3], new Vector3(0f, hy, 0f), Vector3.Up);

            //The lower bevel, the side band and the upper bevel: one flat facet per edge of the octagon, each
            //corresponding pair of edges being parallel, so every facet is a planar trapezoid
            for (int r = 0; r < rings.Length - 1; r++)
            {
                for (int i = 0; i < CORNERS; i++)
                {
                    int j = (i + 1) % CORNERS;

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

            BoundingSphere = new BoundingSphere(Vector3.Zero, new Vector3(hx, hy, hz).Length());
        }

        /// <summary>
        /// One horizontal ring of the octagon at height <paramref name="y"/>: the box's rectangle with its corners
        /// cut by <paramref name="cornerCut"/>, then every edge moved <paramref name="inset"/> inwards along its own
        /// normal — which moves the axis edges by the inset and the diagonal ones by the same distance, so their
        /// corner cut shrinks by inset × (2 − √2). That is what keeps a bevel at one angle all the way round.
        /// </summary>
        private static Vector3[] Ring(float hx, float hz, float cornerCut, float inset, float y)
        {
            float a = hx - inset, b = hz - inset;
            float k = cornerCut - inset * (2f - MathF.Sqrt(2f));

            return new[]
            {
                new Vector3(a - k, y, b),
                new Vector3(-(a - k), y, b),
                new Vector3(-a, y, b - k),
                new Vector3(-a, y, -(b - k)),
                new Vector3(-(a - k), y, -b),
                new Vector3(a - k, y, -b),
                new Vector3(a, y, -(b - k)),
                new Vector3(a, y, b - k)
            };
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
