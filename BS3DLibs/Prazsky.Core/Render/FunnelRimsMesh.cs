using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The two metal rims of the drain funnel: a flat gold band around the wide top circle and another around
    /// the small bottom hole, in one mesh so a single (gold-metal) renderer draws both. Built in the funnel's
    /// own local space — the top rim on the plane y = 0, the bottom rim on y = -<paramref name="height"/> — so
    /// it shares the funnel's world matrix. Meant to be drawn opaque, before the translucent funnel glass, and
    /// with culling off (which is how <c>ArenaIsland</c> draws it): each band is a zero-thickness annulus, so
    /// <c>CullNone</c> is what lets the top one read from under the island as well as from above it.
    /// <para>
    /// <b>Each band lies ON the surface that actually meets it there, rather than standing proud of it as a
    /// bead (#94).</b> It used to be a torus — revolved around the ring and around its own tube — which put a
    /// raised half-unit lip exactly where a released ball is guaranteed to cross, at the one junction the
    /// drain's whole job funnels balls through. Nothing underneath it agreed: <c>FunnelPhysics.Build</c>
    /// collides the smooth cone and the smooth dished stone and says so in its own class doc, so the bead was
    /// a bump the ball rolled straight through. Laid flat the gold reads the same at a glance — it is the
    /// colour and the metal that make the drain legible, not the cross-section — and it now agrees with the
    /// surface a ball is on. It is also far less geometry than the tori: a few rings of quads.
    /// </para>
    /// <para>
    /// <b>Since #109 each band is a shallow crown rather than a plane</b>: its two edges sink
    /// <see cref="EDGE_SINK"/> into the surfaces they meet and its mid-width rises <see cref="SURFACE_LIFT"/>,
    /// because a flat band floating at its anti-z-fight lift showed, from the shallow angle the play camera
    /// actually has, a sliver of the surface under its inner edge — a stone/shadow strip between the gold and
    /// the glass, at the very junction the gold exists to mark. A buried edge cannot be seen under from any
    /// angle; the crown keeps the lift, so nothing is coplanar and the z-fight stays solved; and at a tenth
    /// of the old bead's height it is still nothing a crossing ball shows a step on.
    /// </para>
    /// </summary>
    public class FunnelRimsMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>
        /// How far each band's <b>crown</b> — its mid-width line — rises off the surface it lies on, along
        /// that surface's own normal. Coplanar geometry z-fights, and the fight is the whole width of the
        /// ring rather than a fringe — so some lift is not optional. It is a tenth of a ball radius: far too
        /// little to read as a bead (the tube it replaced was ten times this), and far too little for a ball
        /// crossing it to show a step.
        /// </summary>
        private const float SURFACE_LIFT = 0.05f;

        /// <summary>
        /// How far each band's two <b>edges</b> sink below that same surface (#109). The whole band used to
        /// float at <see cref="SURFACE_LIFT"/>, and a floating edge is a shelf: seen at the shallow angle the
        /// play camera actually has, the parallax under the inner edge exposed a sliver of the surface below
        /// — a stone or shadow strip between the gold and the glass, at the one junction the gold exists to
        /// mark. Buried edges cannot be seen under from any angle, and the band between them is a shallow
        /// crown rather than a plane, so nowhere is it coplanar with the surface it crosses — the z-fight the
        /// lift exists for stays solved. Deep enough that the coarser facets of the surfaces it meets
        /// (the island's bore is a finer polygon than the funnel's — chord dips of a few thousandths) stay
        /// under it everywhere.
        /// </summary>
        private const float EDGE_SINK = 0.04f;

        /// <param name="topRadius">Inner radius of the top band — the funnel mouth, where the stone ends.</param>
        /// <param name="holeRadius">Inner radius of the bottom band — the drain hole.</param>
        /// <param name="height">Vertical drop between the two rims (top at y = 0, hole at y = -height).</param>
        /// <param name="topWidth">Radial width of the top band, outwards from <paramref name="topRadius"/> across the stone.</param>
        /// <param name="holeWidth">Radial width of the bottom band, outwards from <paramref name="holeRadius"/> up the cone.</param>
        /// <param name="dishGrade">
        /// Rise per unit of radius of the stone dish the top band lies on, going outwards from the mouth. It
        /// has to be passed in because it is the one surface here that is not this mesh's own: the bottom
        /// band's grade is the cone's, and the cone is exactly what the three arguments above describe.
        /// </param>
        /// <param name="segments">Facets around each ring (more = rounder circle).</param>
        public FunnelRimsMesh(GraphicsDevice graphicsDevice, float topRadius, float holeRadius, float height,
            float topWidth, float holeWidth, float dishGrade, int segments)
        {
            var vertices = new VertexPositionNormalTexture[segments * 3 * 2];
            var indices = new short[segments * 12 * 2];
            int v = 0, i = 0;

            //The cone's own rise per unit of radius, which is the grade the bottom band lies at. Guarded
            //because a funnel whose two radii met would be a cylinder, and this would be the slope of a
            //vertical wall.
            float coneGrade = topRadius > holeRadius ? height / (topRadius - holeRadius) : 0f;

            AddBand(0f, topRadius, topWidth, dishGrade);
            AddBand(-height, holeRadius, holeWidth, coneGrade);

            //Three rings per band, not two (#109): the edges sink EDGE_SINK below the surface and the
            //mid-width crown rises SURFACE_LIFT above it, so the band is a shallow crown whose edges are
            //buried in the stone and the glass — a buried edge cannot be seen under from any angle, which is
            //what retires the stone/shadow strip the old floating plane showed at the mouth. The cross-
            //section's own slope is folded into each ring's normal, so the crown shades as the low rounded
            //metal band it now is.
            void AddBand(float innerY, float innerRadius, float width, float grade)
            {
                int baseV = v;

                float halfWidth = width * Constants.HALF;
                float archSlope = (SURFACE_LIFT + EDGE_SINK) / halfWidth;

                float crownRadius = innerRadius + halfWidth;
                float crownY = innerY + halfWidth * grade;
                float outerRadius = innerRadius + width;
                float outerY = innerY + width * grade;

                //The surface rises by `grade` per unit outwards, so its tangent along the radial is
                //(1, grade) in the (radius, y) plane and the upward normal is (-grade, 1), normalised. The
                //surface normal carries the sink/lift offsets; the ring normals below add the crown's own
                //slope on the inner half and subtract it on the outer, so the arch reads in the light.
                float invLength = 1f / (float)Math.Sqrt(1.0 + grade * (double)grade);
                float surfaceRadial = -grade * invLength, surfaceY = invLength;

                RingNormal(grade + archSlope, out float innerNormalRadial, out float innerNormalY);
                RingNormal(grade, out float crownNormalRadial, out float crownNormalY);
                RingNormal(grade - archSlope, out float outerNormalRadial, out float outerNormalY);

                for (int s = 0; s < segments; s++)
                {
                    float u = (float)(s / (double)segments * Math.PI * 2.0);
                    float cosU = (float)Math.Cos(u), sinU = (float)Math.Sin(u);

                    Vector3 sink = new Vector3(surfaceRadial * cosU, surfaceY, surfaceRadial * sinU) * -EDGE_SINK;
                    Vector3 lift = new Vector3(surfaceRadial * cosU, surfaceY, surfaceRadial * sinU) * SURFACE_LIFT;

                    Vector3 inner = new Vector3(innerRadius * cosU, innerY, innerRadius * sinU) + sink;
                    Vector3 crown = new Vector3(crownRadius * cosU, crownY, crownRadius * sinU) + lift;
                    Vector3 outer = new Vector3(outerRadius * cosU, outerY, outerRadius * sinU) + sink;

                    float uv = s / (float)segments;
                    vertices[v++] = new VertexPositionNormalTexture(inner,
                        new Vector3(innerNormalRadial * cosU, innerNormalY, innerNormalRadial * sinU), new Vector2(uv, 0f));
                    vertices[v++] = new VertexPositionNormalTexture(crown,
                        new Vector3(crownNormalRadial * cosU, crownNormalY, crownNormalRadial * sinU), new Vector2(uv, 0.5f));
                    vertices[v++] = new VertexPositionNormalTexture(outer,
                        new Vector3(outerNormalRadial * cosU, outerNormalY, outerNormalRadial * sinU), new Vector2(uv, 1f));
                }

                for (int s = 0; s < segments; s++)
                {
                    int sNext = (s + 1) % segments;
                    int i00 = baseV + s * 3, i01 = baseV + s * 3 + 1, i02 = baseV + s * 3 + 2;
                    int i10 = baseV + sNext * 3, i11 = baseV + sNext * 3 + 1, i12 = baseV + sNext * 3 + 2;

                    indices[i++] = (short)i00; indices[i++] = (short)i10; indices[i++] = (short)i11;
                    indices[i++] = (short)i00; indices[i++] = (short)i11; indices[i++] = (short)i01;
                    indices[i++] = (short)i01; indices[i++] = (short)i11; indices[i++] = (short)i12;
                    indices[i++] = (short)i01; indices[i++] = (short)i12; indices[i++] = (short)i02;
                }
            }

            //The upward normal of a surface rising `grade` per unit of radius: (-grade, 1) in the
            //(radius, y) plane, normalised — the same construction the flat band used for its one grade.
            static void RingNormal(float grade, out float radial, out float y)
            {
                float invLength = 1f / (float)Math.Sqrt(1.0 + grade * (double)grade);
                radial = -grade * invLength;
                y = invLength;
            }

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            float outer = topRadius + topWidth;
            BoundingSphere = new BoundingSphere(new Vector3(0f, -height * Constants.HALF, 0f),
                new Vector2(outer, height * Constants.HALF).Length());
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
