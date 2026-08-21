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
    /// with culling off (which is how <c>ArenaIsland</c> draws it): each band is a zero-thickness ribbon, so
    /// <c>CullNone</c> is what lets the top one read from under the island as well as from above it.
    /// <para>
    /// <b>Each band lies ON the surface that actually meets it there, rather than standing proud of it as a
    /// bead (#94).</b> It used to be a torus — revolved around the ring and around its own tube — which put a
    /// raised half-unit lip exactly where a released ball is guaranteed to cross, at the one junction the
    /// drain's whole job funnels balls through. Nothing underneath it agreed: <c>FunnelPhysics.Build</c>
    /// collides the smooth cone and the smooth dished stone and says so in its own class doc, so the bead was
    /// a bump the ball rolled straight through. Laid flat the gold reads the same at a glance — it is the
    /// colour and the metal that make the drain legible, not the cross-section — and it now agrees with the
    /// surface a ball is on.
    /// </para>
    /// <para>
    /// <b>A band is a cross-section of rings, not a plane</b> (#109): its edges sink <see cref="EDGE_SINK"/>
    /// into the surface they lie on and its mid-width crown rises <see cref="SURFACE_LIFT"/> off it, because
    /// a flat band floating at its anti-z-fight lift showed, from the shallow angle the play camera actually
    /// has, a sliver of the surface under its edge. A buried edge cannot be seen under from any angle; the
    /// crown keeps the lift, so nothing is coplanar and the z-fight stays solved; and at a tenth of the old
    /// bead's height it is still nothing a crossing ball shows a step on.
    /// </para>
    /// <para>
    /// <b>⚠ But at the MOUTH that fix bought the very strip it was buying off (#237), and the arithmetic says
    /// why.</b> Burying an edge means the surface is drawn in FRONT of the gold there, so the gold's visible
    /// boundary moves inward from the edge to wherever the crown carries it back above the surface — at
    /// <c>halfWidth * EDGE_SINK / (EDGE_SINK + SURFACE_LIFT)</c>, which for the top band is <b>0.22 units of
    /// bare stone</b>, ringing the mouth between the glass and the gold. #109 saw a stone strip under a
    /// floating edge and buried the edge; burying it drew a stone strip over the edge instead. The two states
    /// look the same and neither is the answer: <b>the stone has to be covered, not hidden behind</b>.
    /// </para>
    /// <para>
    /// So the top band <b>wraps the lip</b>: past its mouth ring it turns the crease and runs
    /// <see cref="COLLAR_DROP"/> on down the glass cone, ending sunk into the GLASS rather than into the
    /// stone. Nothing stone-coloured can appear between the gold and the glass any more, because there is no
    /// longer a radius at the mouth where stone is the front-most thing — and the collar's own buried edge is
    /// inside the throat, under translucent glass that shows it as a soft gold under-lip rather than hiding
    /// it. This is the trim piece #237 asked for in as many words. The outer edge keeps its sink: outside the
    /// band the front-most thing is stone, which is exactly what belongs there.
    /// </para>
    /// </summary>
    public class FunnelRimsMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>
        /// How far a band's <b>crown</b> — its mid-width line — rises off the surface it lies on, along that
        /// surface's own normal. Coplanar geometry z-fights, and the fight is the whole width of the ring
        /// rather than a fringe — so some lift is not optional. It is a tenth of a ball radius: far too
        /// little to read as a bead (the tube it replaced was ten times this), and far too little for a ball
        /// crossing it to show a step.
        /// </summary>
        private const float SURFACE_LIFT = 0.05f;

        /// <summary>
        /// How far a band's <b>buried</b> edges sink below the surface they lie on (#109). A floating edge is
        /// a shelf: seen at the shallow angle the play camera actually has, the parallax under it exposes a
        /// sliver of the surface below. Deep enough that the coarser facets of the surfaces it meets stay
        /// under it everywhere (the island's bore is a finer polygon than the funnel's — chord dips of a few
        /// thousandths).
        /// <para>
        /// It is applied to the edges that have SURFACE to hide under them — the outer edges of both bands
        /// and the collar's end down the glass. <b>Not to the mouth ring</b>, which is the one edge where
        /// burying it costs more than it buys (see the class remarks and #237).
        /// </para>
        /// </summary>
        private const float EDGE_SINK = 0.04f;

        /// <summary>
        /// How far the top band's <b>mouth ring</b> stands off the stone at the lip. It is not buried (#237)
        /// and it cannot be flush either: the lip is the crease where the stone dish meets the glass cone, so
        /// a ring sitting exactly on it is coplanar with both and z-fights along the one line the gold exists
        /// to draw. A twentieth of the crown's lift is enough to own the crease and far too little to be a
        /// step under a ball.
        /// </summary>
        private const float LIP_LIFT = 0.012f;

        /// <summary>
        /// How far past the mouth the top band's collar runs on down the glass cone, measured as a drop in
        /// radius. It has to be long enough to read as gold turning the lip rather than as a fringe, and
        /// short enough to stay in the throat's shadow rather than becoming a bead inside the funnel. About a
        /// third of the band's width across the stone.
        /// </summary>
        private const float COLLAR_DROP = 0.35f;

        /// <summary>
        /// One ring of a band's cross-section: where it sits in the funnel's (radius, y) half-plane, the
        /// grade of the surface it lies on there, and how far it stands off that surface along the surface's
        /// own normal — positive out of the material, negative into it.
        /// </summary>
        private readonly struct BandRing
        {
            public readonly float Radius, Y, Grade, Offset;

            public BandRing(float radius, float y, float grade, float offset)
            {
                Radius = radius;
                Y = y;
                Grade = grade;
                Offset = offset;
            }

            /// <summary>The upward normal of a surface rising <see cref="Grade"/> per unit of radius:
            /// <c>(-grade, 1)</c> in the (radius, y) plane, normalised.</summary>
            public Vector2 SurfaceNormal
            {
                get
                {
                    float invLength = 1f / (float)Math.Sqrt(1.0 + Grade * (double)Grade);
                    return new Vector2(-Grade * invLength, invLength);
                }
            }

            /// <summary>Where the ring actually lands, once its offset is taken along that normal.</summary>
            public Vector2 Point => new Vector2(Radius, Y) + SurfaceNormal * Offset;
        }

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
            //The cone's own rise per unit of radius, which is the grade the bottom band lies at and the grade
            //the top band's collar turns on to. Guarded because a funnel whose two radii met would be a
            //cylinder, and this would be the slope of a vertical wall.
            float coneGrade = topRadius > holeRadius ? height / (topRadius - holeRadius) : 0f;

            //The top band, from inside the throat outwards: the collar sunk into the glass, the mouth ring
            //standing on the lip, the crown, and the outer edge buried in the stone. The collar is what
            //keeps stone out from between the gold and the glass (#237); everything from the mouth ring
            //outwards is #109's crown.
            float halfTop = topWidth * Constants.HALF;
            BandRing[] topRings =
            {
                new(topRadius - COLLAR_DROP, -COLLAR_DROP * coneGrade, coneGrade, -EDGE_SINK),
                new(topRadius, 0f, dishGrade, LIP_LIFT),
                new(topRadius + halfTop, halfTop * dishGrade, dishGrade, SURFACE_LIFT),
                new(topRadius + topWidth, topWidth * dishGrade, dishGrade, -EDGE_SINK),
            };

            //The bottom band keeps the plain three-ring crown: inside its inner edge is the drain hole, so
            //there is no surface there for a buried edge to hand to the eye instead of the gold.
            float halfHole = holeWidth * Constants.HALF;
            BandRing[] holeRings =
            {
                new(holeRadius, -height, coneGrade, -EDGE_SINK),
                new(holeRadius + halfHole, -height + halfHole * coneGrade, coneGrade, SURFACE_LIFT),
                new(holeRadius + holeWidth, -height + holeWidth * coneGrade, coneGrade, -EDGE_SINK),
            };

            //Sized from the cross-sections rather than from a literal, so a ring added to either band cannot
            //silently overrun the buffers.
            int ringCount = topRings.Length + holeRings.Length;
            int quadCount = (topRings.Length - 1) + (holeRings.Length - 1);

            var vertices = new VertexPositionNormalTexture[segments * ringCount];
            var indices = new short[segments * quadCount * 6];
            int v = 0, i = 0;

            AddBand(topRings);
            AddBand(holeRings);

            //Revolves one cross-section into a band. The ring normals are taken from the cross-section
            //POLYLINE rather than derived by hand from the surface grade and the crown's slope, so a band's
            //shading cannot disagree with the shape it was actually given — which matters now that the top
            //band turns a crease in the middle of its own run.
            void AddBand(BandRing[] rings)
            {
                int baseV = v;
                int last = rings.Length - 1;

                var points = new Vector2[rings.Length];
                var normals = new Vector2[rings.Length];

                for (int r = 0; r < rings.Length; r++) points[r] = rings[r].Point;

                for (int r = 0; r < rings.Length; r++)
                {
                    //Central difference inside the run, one-sided at the two ends.
                    Vector2 tangent = points[Math.Min(r + 1, last)] - points[Math.Max(r - 1, 0)];
                    tangent.Normalize();

                    //Perpendicular to the cross-section, turned to the side the surface faces.
                    Vector2 normal = new(-tangent.Y, tangent.X);
                    if (Vector2.Dot(normal, rings[r].SurfaceNormal) < 0f) normal = -normal;

                    normals[r] = normal;
                }

                for (int s = 0; s < segments; s++)
                {
                    float u = (float)(s / (double)segments * Math.PI * 2.0);
                    float cosU = (float)Math.Cos(u), sinU = (float)Math.Sin(u);
                    float uv = s / (float)segments;

                    for (int r = 0; r < rings.Length; r++)
                    {
                        vertices[v++] = new VertexPositionNormalTexture(
                            new Vector3(points[r].X * cosU, points[r].Y, points[r].X * sinU),
                            new Vector3(normals[r].X * cosU, normals[r].Y, normals[r].X * sinU),
                            new Vector2(uv, r / (float)last));
                    }
                }

                for (int s = 0; s < segments; s++)
                {
                    int here = baseV + s * rings.Length;
                    int next = baseV + (s + 1) % segments * rings.Length;

                    for (int r = 0; r < last; r++)
                    {
                        indices[i++] = (short)(here + r);
                        indices[i++] = (short)(next + r);
                        indices[i++] = (short)(next + r + 1);
                        indices[i++] = (short)(here + r);
                        indices[i++] = (short)(next + r + 1);
                        indices[i++] = (short)(here + r + 1);
                    }
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
