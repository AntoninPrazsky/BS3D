using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One point of a lathe's cross-section: a radius and a height, plus how the surface behaves there.
    /// </summary>
    public readonly struct LathePoint
    {
        /// <summary>Distance from the axis.</summary>
        public readonly float Radius;

        /// <summary>Height along the axis. The caller's own frame — <see cref="LatheMesh"/> does not shift it.</summary>
        public readonly float Y;

        /// <summary>
        /// True for a hard arris: the two runs meeting here keep their own normals instead of being
        /// smoothed into one. A chamfer meeting a flat top has to break, or the flat reads as curved for
        /// the last span before its edge.
        /// </summary>
        public readonly bool Crease;

        /// <summary>
        /// How much of the mesh's radial irregularity this ring takes, 0 to 1. Two lathes that share an
        /// edge must agree here as well as on the radius and height, or the seam opens (see
        /// <see cref="Irregularity"/>).
        /// </summary>
        public readonly float Wobble;

        public LathePoint(float radius, float y, bool crease = false, float wobble = 0f)
        {
            Radius = radius;
            Y = y;
            Crease = crease;
            Wobble = wobble;
        }
    }

    /// <summary>
    /// A surface of revolution built from a cross-section polyline — the general shape a fixed set of
    /// quads cannot express: chamfers, bullnoses, undercuts, corbels, anything whose silhouette is the
    /// point of it. Runs from the first <see cref="LathePoint"/> to the last, one ring of quads per span.
    /// <para>
    /// The <b>profile direction sets which way the surface faces</b>: the outward normal is the profile
    /// tangent turned a quarter left in the (radius, y) plane, so a polyline that starts at the axis, runs
    /// outward along the top, down the outside and back in along the underside faces up, out and down in
    /// turn — trace it the other way and the whole solid is inside out. Triangles are wound clockwise
    /// seen from outside, MonoGame's default front face (see <see cref="SphereMesh"/>), so a closed
    /// profile can be drawn with ordinary back-face culling.
    /// </para>
    /// </summary>
    public class LatheMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>
        /// The radial irregularity, as a multiple of the mesh's amplitude: what stops a lathed solid from
        /// reading as a machined part. A function of the <b>angle and the world height</b> and of nothing
        /// else, which is what lets two lathes meet without a crack — they share the height along their
        /// common edge, so they are displaced identically there whatever each one's profile does next.
        /// <para>
        /// The angular frequencies must be <b>whole numbers</b> or the ring does not close: the last
        /// segment would meet the first at a different radius, splitting the solid down one seam. The
        /// amplitudes sum to 1, so the result stays within ±1 and the caller's amplitude is a true bound.
        /// </para>
        /// </summary>
        public static float Irregularity(float angle, float y) =>
            0.55f * MathF.Sin(3f * angle + 1.7f + 0.31f * y)
            + 0.30f * MathF.Sin(7f * angle + 4.1f - 0.53f * y)
            + 0.15f * MathF.Sin(13f * angle + 2.3f + 0.19f * y);

        /// <param name="profile">The cross-section, in order along the surface. At least two points.</param>
        /// <param name="segments">Facets around the axis (minimum 3).</param>
        /// <param name="irregularityAmplitude">
        /// Peak radial displacement, in world units, scaled per ring by <see cref="LathePoint.Wobble"/>.
        /// Zero leaves a true circle of revolution.
        /// </param>
        public LatheMesh(GraphicsDevice graphicsDevice, IReadOnlyList<LathePoint> profile, int segments,
            float irregularityAmplitude = 0f)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Count < 2) throw new ArgumentOutOfRangeException(nameof(profile));
            if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));

            int rings = profile.Count;
            int spans = rings - 1;

            //Every ring's world position, so the normals below can be taken from the geometry that is
            //actually drawn rather than from the ideal profile — with the irregularity on, the two differ,
            //and it is exactly the difference that makes the undulation read.
            var positions = new Vector3[rings, segments];
            var cos = new float[segments];
            var sin = new float[segments];

            for (int s = 0; s < segments; s++)
            {
                float angle = MathHelper.TwoPi * s / segments;
                cos[s] = MathF.Cos(angle);
                sin[s] = MathF.Sin(angle);

                for (int k = 0; k < rings; k++)
                {
                    LathePoint point = profile[k];

                    float radius = point.Radius;
                    if (irregularityAmplitude != 0f && point.Wobble != 0f)
                        radius += irregularityAmplitude * point.Wobble * Irregularity(angle, point.Y);

                    positions[k, s] = new Vector3(radius * cos[s], point.Y, radius * sin[s]);
                }
            }

            //One flat normal per quad. Outward is the tangent around the ring crossed into the tangent
            //along the profile, in that order — check it against the top of a disc: running outward with
            //the ring going counter-clockwise gives +Y, which is the face that should see the sky.
            //
            //The ring tangent is taken from whichever of the span's two rings is NOT on the axis. A ring
            //at radius 0 has all its segments at one point, so its tangent is the zero vector and the
            //cross product collapses — and the fallback then hands the whole span a +Y normal. That is
            //correct for a flat top (a disc's first ring is its centre) and wrong for anything that comes
            //to a point: a cone's tip span took it, and the top third of every conifer was shaded as
            //though it faced the sky. The two rings of a span cannot both be on the axis unless the span
            //is degenerate, which the length test below still catches.
            var faceNormals = new Vector3[spans, segments];

            for (int span = 0; span < spans; span++)
            {
                int tangentRing = profile[span].Radius > 1e-6f ? span : span + 1;

                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;

                    Vector3 alongRing = positions[tangentRing, next] - positions[tangentRing, s];
                    Vector3 alongProfile = positions[span + 1, s] - positions[span, s];
                    Vector3 normal = Vector3.Cross(alongRing, alongProfile);

                    faceNormals[span, s] = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.Up;
                }
            }

            //Each span owns its two rings of vertices. That costs a duplicated ring at every smooth
            //junction — a few hundred vertices on the shapes this draws — and buys creases for free: a
            //hard edge is simply two spans that do not average their normals together.
            int vertexCount = spans * segments * 2;
            var vertices = new VertexPositionNormalTexture[vertexCount];
            var indices = new int[spans * segments * 6];

            //Texture coordinates run around the lathe and along the profile by arc length, so a texture
            //mapped through them is not stretched by a long span. Nothing needs them while the island is
            //projected triplanar, but a lathe with no UVs at all is a trap for the next caller.
            var arcLength = new float[rings];
            for (int k = 1; k < rings; k++)
            {
                arcLength[k] = arcLength[k - 1] + new Vector2(
                    profile[k].Radius - profile[k - 1].Radius,
                    profile[k].Y - profile[k - 1].Y).Length();
            }

            float totalArc = MathF.Max(arcLength[rings - 1], 1e-6f);
            int v = 0, i = 0;

            for (int span = 0; span < spans; span++)
            {
                int ringBase = v;

                for (int side = 0; side < 2; side++)
                {
                    int k = span + side;

                    for (int s = 0; s < segments; s++)
                    {
                        vertices[v++] = new VertexPositionNormalTexture(
                            positions[k, s],
                            VertexNormal(span, side, s),
                            new Vector2((float)s / segments, arcLength[k] / totalArc));
                    }
                }

                //Wound as SphereMesh's bands are: (upper, lower, upper+1) and (upper+1, lower, lower+1),
                //whose (b - a) x (c - a) points into the solid, which is the front face once the viewport
                //transform has flipped Y.
                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;
                    int upper = ringBase + s, upperNext = ringBase + next;
                    int lower = ringBase + segments + s, lowerNext = ringBase + segments + next;

                    indices[i++] = upper;
                    indices[i++] = lower;
                    indices[i++] = upperNext;

                    indices[i++] = upperNext;
                    indices[i++] = lower;
                    indices[i++] = lowerNext;
                }
            }

            //The normal at one corner of one span: this span's own face normals, smoothed around the ring,
            //plus those of the span across the junction unless the profile creases there. Both spans at a
            //smooth junction compute the same sum, so the shading crosses it without a seam.
            Vector3 VertexNormal(int span, int side, int s)
            {
                Vector3 normal = SpanNormal(span, s);

                int k = span + side;
                if (!profile[k].Crease)
                {
                    if (side == 0 && span > 0) normal += SpanNormal(span - 1, s);
                    else if (side == 1 && span < spans - 1) normal += SpanNormal(span + 1, s);
                }

                return normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.Up;
            }

            //Smoothed around the ring: the two facets meeting at this segment. With the irregularity on,
            //this is where its tangential slope enters the shading — a ring of flat facet normals would
            //draw the undulation's silhouette but light the surface as though it were still a cylinder.
            Vector3 SpanNormal(int span, int s)
            {
                Vector3 sum = faceNormals[span, (s - 1 + segments) % segments] + faceNormals[span, s];

                return sum.LengthSquared() > 1e-12f ? Vector3.Normalize(sum) : faceNormals[span, s];
            }

            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration,
                vertexCount, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            //Sixteen-bit indices silently wrap past 65 535 vertices and send far triangles to the wrong
            //corners of the mesh - the bug that cost a long hunt on the mountain grid. A lathe crosses
            //that line at a few hundred segments, so the width is chosen rather than assumed.
            if (vertexCount <= ushort.MaxValue)
            {
                var narrow = new short[indices.Length];
                for (int n = 0; n < indices.Length; n++) narrow[n] = (short)indices[n];

                IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, narrow.Length, BufferUsage.WriteOnly);
                IndexBuffer.SetData(narrow);
            }
            else
            {
                IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
                IndexBuffer.SetData(indices);
            }

            float lowest = float.MaxValue, highest = float.MinValue, widest = 0f;

            for (int k = 0; k < rings; k++)
            {
                lowest = MathF.Min(lowest, profile[k].Y);
                highest = MathF.Max(highest, profile[k].Y);
                widest = MathF.Max(widest, profile[k].Radius + MathF.Abs(irregularityAmplitude * profile[k].Wobble));
            }

            float centreY = (lowest + highest) * 0.5f;

            BoundingSphere = new BoundingSphere(new Vector3(0f, centreY, 0f),
                new Vector2(widest, (highest - lowest) * 0.5f).Length());
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
