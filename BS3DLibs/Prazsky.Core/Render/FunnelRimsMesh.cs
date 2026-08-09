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
    /// surface a ball is on. It is also 16× less geometry: two rings of quads instead of two full double-
    /// revolved tori.
    /// </para>
    /// </summary>
    public class FunnelRimsMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>
        /// How far each band is lifted off the surface it lies on, along that surface's own normal. Coplanar
        /// geometry z-fights, and the fight is the whole width of the ring rather than a fringe — so this is
        /// not optional. It is a tenth of a ball radius: far too little to read as a bead (the tube it
        /// replaced was ten times this), and far too little for a ball crossing it to show a step.
        /// </summary>
        private const float SURFACE_LIFT = 0.05f;

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
            var vertices = new VertexPositionNormalTexture[segments * 2 * 2];
            var indices = new short[segments * 6 * 2];
            int v = 0, i = 0;

            //The cone's own rise per unit of radius, which is the grade the bottom band lies at. Guarded
            //because a funnel whose two radii met would be a cylinder, and this would be the slope of a
            //vertical wall.
            float coneGrade = topRadius > holeRadius ? height / (topRadius - holeRadius) : 0f;

            AddBand(0f, topRadius, topWidth, dishGrade);
            AddBand(-height, holeRadius, holeWidth, coneGrade);

            void AddBand(float innerY, float innerRadius, float width, float grade)
            {
                int baseV = v;

                //The surface rises by `grade` per unit outwards, so its tangent along the radial is
                //(1, grade) in the (radius, y) plane and the upward normal is (-grade, 1), normalised.
                float invLength = 1f / (float)Math.Sqrt(1.0 + grade * (double)grade);
                float normalRadial = -grade * invLength, normalY = invLength;

                float outerRadius = innerRadius + width;
                float outerY = innerY + width * grade;

                for (int s = 0; s < segments; s++)
                {
                    float u = (float)(s / (double)segments * Math.PI * 2.0);
                    float cosU = (float)Math.Cos(u), sinU = (float)Math.Sin(u);

                    Vector3 normal = new(normalRadial * cosU, normalY, normalRadial * sinU);
                    Vector3 lift = normal * SURFACE_LIFT;

                    Vector3 inner = new Vector3(innerRadius * cosU, innerY, innerRadius * sinU) + lift;
                    Vector3 outer = new Vector3(outerRadius * cosU, outerY, outerRadius * sinU) + lift;

                    float uv = s / (float)segments;
                    vertices[v++] = new VertexPositionNormalTexture(inner, normal, new Vector2(uv, 0f));
                    vertices[v++] = new VertexPositionNormalTexture(outer, normal, new Vector2(uv, 1f));
                }

                for (int s = 0; s < segments; s++)
                {
                    int sNext = (s + 1) % segments;
                    int i00 = baseV + s * 2, i01 = baseV + s * 2 + 1;
                    int i10 = baseV + sNext * 2, i11 = baseV + sNext * 2 + 1;

                    indices[i++] = (short)i00; indices[i++] = (short)i10; indices[i++] = (short)i11;
                    indices[i++] = (short)i00; indices[i++] = (short)i11; indices[i++] = (short)i01;
                }
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
