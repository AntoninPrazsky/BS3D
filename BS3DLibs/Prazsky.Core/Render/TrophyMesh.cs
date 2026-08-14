using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A trophy cup (#183): a plinth, a stem with a knop, a flaring bowl turned over at the lip, and — on the
    /// top tier only — a handle on each side. Built procedurally like everything else here rather than loaded,
    /// for the reason the sky domes stopped being eighteen <c>.dae</c> files in #113.
    /// <para>
    /// The body is a surface of revolution and could have been a <see cref="LatheMesh"/>, which is what the
    /// issue's own sketch proposed. It is built here on <see cref="MeshBuilder"/> instead for one reason: the
    /// handles are <b>not</b> surfaces of revolution about the cup's axis, and a trophy that arrives as two
    /// meshes is two renderers, two materials to keep in step and two draw calls for one object. Built together
    /// they are one buffer, one material and one draw — and the builder's winding correction covers the swept
    /// handles as readily as the revolved body, which is the whole point of it being a service.
    /// </para>
    /// <para>
    /// The cup stands on <b>y = 0</b> and is <see cref="HEIGHT"/> tall in mesh units, so a caller scales by the
    /// height it wants and translates to where the foot should sit. Nothing here knows how big a trophy is.
    /// </para>
    /// </summary>
    public sealed class TrophyMesh : IProceduralMesh, IDisposable
    {
        /// <summary>The cup's overall height in mesh units, foot to lip. The profile below is authored to it.</summary>
        public const float HEIGHT = 1f;

        //Facets around the axis. A trophy is presented CLOSE and is the one object on screen the player is
        //asked to look at, so it cannot be the twelve or sixteen a scattered prop gets away with — a faceted
        //silhouette on a mirror-finish surface reads as a low-poly model rather than as metal.
        private const int SEGMENTS = 48;

        //Facets around the handle's own tube, and steps along its arc. Fewer than the body's: a handle is a
        //fifth of the bowl's diameter, so the same angular error is a fifth of the pixels.
        private const int HANDLE_SIDES = 14, HANDLE_STEPS = 22;

        /// <summary>
        /// One ring of the body's cross-section. <paramref name="Crease"/> keeps the runs above and below it
        /// on their own normals — a lip turning back on itself, or a plinth meeting its own wall, has to break
        /// or the flat above it reads as curved for the last span before the edge.
        /// </summary>
        private readonly record struct Ring(float Radius, float Y, bool Crease = false);

        //THE PROFILE, traced as one continuous polyline: from the centre of the underside, out along the foot,
        //up the plinth, in and up the stem, over the knop, out into the bowl, over the lip, and back DOWN THE
        //INSIDE to the centre of the bowl's floor. Both ends touch the axis, so the solid is closed by the
        //profile itself and needs no separate caps.
        //
        //The bowl being hollow is not decoration: this thing is shown from above as it turns, and a cup filled
        //in flat at the brim reads as a lamp.
        //It was authored coarser than this and photographed badly, which is worth keeping: with a single point
        //at the knop and one flat disc for a foot, the cup read as an eggcup on a washer — the knop came out
        //as a hard bowtie because two straight runs met at a point, and a base with no step to it has no
        //weight. Neither is a shading problem, and no material fixed either: a silhouette is read before a
        //surface is, and this thing is shown a third of the frame high.
        private static readonly Ring[] PROFILE =
        {
            new(0.000f, 0.000f),
            new(0.330f, 0.000f, true),   //the foot's underside, out to its edge
            new(0.330f, 0.045f, true),   //and its wall
            new(0.278f, 0.078f, true),   //a step in — a plinth has two courses, which is what gives it weight
            new(0.278f, 0.108f, true),
            new(0.152f, 0.140f, true),   //taper up off the plinth into the stem
            new(0.082f, 0.195f),
            new(0.068f, 0.300f),
            new(0.098f, 0.342f),         //the knop, in four points rather than one so it is round
            new(0.124f, 0.380f),
            new(0.101f, 0.418f),
            new(0.070f, 0.456f),
            new(0.112f, 0.500f),         //the bowl's own foot flares off the stem
            new(0.198f, 0.572f),
            new(0.288f, 0.680f),
            new(0.358f, 0.800f),
            new(0.408f, 0.910f),
            new(0.440f, 0.995f, true),   //the lip, turned out
            new(0.404f, 0.978f, true),   //and back down inside
            new(0.358f, 0.880f),
            new(0.268f, 0.720f),
            new(0.148f, 0.610f),
            new(0.000f, 0.575f)          //the bowl's inside floor
        };

        //THE HANDLE IS A QUADRATIC BÉZIER, not the circular arc it was first built as, and the reason is that
        //a handle has to meet the bowl at BOTH ends whatever the bowl's profile does between them. Struck as a
        //circle it did not: the ends landed where the arc's own geometry put them, the upper one buried and
        //the lower one hanging in the air off the side of the cup. A Bézier is stated by its ENDS — both are
        //written just inside the bowl's radius at their own heights, so they are buried by construction — and
        //the control point is then free to say how far the handle bows out without moving either.
        //
        //It also came out too small the first time. Clearing the bowl by a tenth of a unit reads as a rib
        //stuck on the side; the bow reaches about 0.54 against a bowl of 0.35 at that height, so there is real
        //daylight under it and it reads as something a hand could go through.
        private static readonly Vector2 HANDLE_LOW = new(0.225f, 0.620f);    //buried in the bowl's lower wall
        private static readonly Vector2 HANDLE_HIGH = new(0.400f, 0.930f);   //and in its upper wall
        private static readonly Vector2 HANDLE_BOW = new(0.860f, 0.780f);    //the control point, well outboard
        private const float HANDLE_TUBE_RADIUS = 0.042f;

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="handles">
        /// Whether to fit the two side handles. They are the top tier's own detail (#183): the three cups below
        /// it are plain, so the difference is read as a shape before the colour has said anything.
        /// </param>
        public TrophyMesh(GraphicsDevice graphicsDevice, bool handles)
        {
            MeshBuilder builder = new();

            BuildBody(builder);

            if (handles)
            {
                BuildHandle(builder, +1f);
                BuildHandle(builder, -1f);
            }

            (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(graphicsDevice);

            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;

            //Centred on the cup's own middle rather than on its foot, so a culling test is not lopsided. The
            //radius takes the handles whether they are fitted or not — half a mesh unit on a bounding sphere
            //costs a cull that would not have happened, and getting it wrong the other way pops the subject of
            //the whole moment out of frame.
            BoundingSphere = new BoundingSphere(new Vector3(0f, HEIGHT * 0.5f, 0f), 0.80f);
        }

        /// <summary>
        /// Revolves <see cref="PROFILE"/> about the Y axis. Normals are computed from the profile rather than
        /// from the triangles: for a segment running <c>(dr, dy)</c> in the (radius, y) plane the outward
        /// normal is <c>(dy, -dr)</c> — the tangent turned so its radial part points away from the axis, which
        /// is what makes an underside face down and the inside of the bowl face up and in.
        /// </summary>
        private static void BuildBody(MeshBuilder builder)
        {
            int spans = PROFILE.Length - 1;

            //Per-span normal in the (radial, y) plane, then per-ring normals either side of each ring. They
            //differ only at a crease, which is exactly what a crease is.
            Vector2[] spanNormal = new Vector2[spans];
            for (int s = 0; s < spans; s++)
            {
                float dr = PROFILE[s + 1].Radius - PROFILE[s].Radius;
                float dy = PROFILE[s + 1].Y - PROFILE[s].Y;
                spanNormal[s] = Vector2.Normalize(new Vector2(dy, -dr));
            }

            Vector2[] below = new Vector2[PROFILE.Length];   //the normal a ring shows to the span under it
            Vector2[] above = new Vector2[PROFILE.Length];   //and to the span over it

            for (int i = 0; i < PROFILE.Length; i++)
            {
                Vector2 lower = spanNormal[Math.Max(0, i - 1)];
                Vector2 upper = spanNormal[Math.Min(spans - 1, i)];

                if (PROFILE[i].Crease)
                {
                    below[i] = lower;
                    above[i] = upper;
                }
                else
                {
                    Vector2 mean = lower + upper;
                    Vector2 smooth = mean.LengthSquared() > 1e-8f ? Vector2.Normalize(mean) : upper;
                    below[i] = smooth;
                    above[i] = smooth;
                }
            }

            for (int s = 0; s < spans; s++)
            {
                Ring low = PROFILE[s], high = PROFILE[s + 1];

                //A span with no length in either direction would give a degenerate quad and a zero normal
                if (low.Radius == high.Radius && low.Y == high.Y) continue;

                for (int i = 0; i < SEGMENTS; i++)
                {
                    float a0 = i / (float)SEGMENTS * MathHelper.TwoPi;
                    float a1 = (i + 1) / (float)SEGMENTS * MathHelper.TwoPi;

                    (float s0, float c0) = MathF.SinCos(a0);
                    (float s1, float c1) = MathF.SinCos(a1);

                    Vector3 p00 = new(low.Radius * c0, low.Y, low.Radius * s0);
                    Vector3 p10 = new(low.Radius * c1, low.Y, low.Radius * s1);
                    Vector3 p11 = new(high.Radius * c1, high.Y, high.Radius * s1);
                    Vector3 p01 = new(high.Radius * c0, high.Y, high.Radius * s0);

                    Vector2 nLow = above[s], nHigh = below[s + 1];

                    Vector3 n00 = new(nLow.X * c0, nLow.Y, nLow.X * s0);
                    Vector3 n10 = new(nLow.X * c1, nLow.Y, nLow.X * s1);
                    Vector3 n11 = new(nHigh.X * c1, nHigh.Y, nHigh.X * s1);
                    Vector3 n01 = new(nHigh.X * c0, nHigh.Y, nHigh.X * s0);

                    //The face normal handed to the builder is the span's own, swung to the middle of this
                    //facet: it decides the winding, and it must not be one of the smoothed vertex normals —
                    //at the bowl's floor those tilt far enough to flip a quad the wrong way round.
                    Vector2 mid = spanNormal[s];
                    float ac = MathF.Cos((a0 + a1) * 0.5f), asn = MathF.Sin((a0 + a1) * 0.5f);
                    Vector3 face = new(mid.X * ac, mid.Y, mid.X * asn);

                    //Degenerate at the axis: the first and last rings are single points, so one edge of the
                    //quad collapses and it is a triangle. Adding it as a quad would add a zero-area triangle,
                    //which is harmless but pointless — take the triangle instead.
                    if (low.Radius <= 0f) builder.AddTriangle(p01, p11, p00, n01, n11, n00, face);
                    else if (high.Radius <= 0f) builder.AddTriangle(p00, p10, p01, n00, n10, n01, face);
                    else builder.AddQuad(p00, p10, p11, p01, n00, n10, n11, n01, face);
                }
            }
        }

        /// <summary>
        /// One handle: a circular tube swept along a Bézier arc standing in the plane of the cup's axis and
        /// <paramref name="side"/>'s X. The sweep's frame is fixed rather than parallel-transported, because
        /// the path is planar — Z is always perpendicular to it, so there is no twist to accumulate and none
        /// of a Frenet frame's trouble where the curvature would flip.
        /// </summary>
        private static void BuildHandle(MeshBuilder builder, float side)
        {
            Vector3[,] ring = new Vector3[HANDLE_STEPS + 1, HANDLE_SIDES];
            Vector3[,] ringNormal = new Vector3[HANDLE_STEPS + 1, HANDLE_SIDES];

            for (int step = 0; step <= HANDLE_STEPS; step++)
            {
                float t = step / (float)HANDLE_STEPS;
                float u = 1f - t;

                //The quadratic Bézier and its own derivative, both in the plane z = 0, then mirrored onto the
                //wanted side. The derivative is exact rather than a difference of neighbouring samples, so the
                //frame is stable at the ends where a difference would have nothing on one side of it.
                Vector2 path = u * u * HANDLE_LOW + 2f * u * t * HANDLE_BOW + t * t * HANDLE_HIGH;
                Vector2 slope = 2f * u * (HANDLE_BOW - HANDLE_LOW) + 2f * t * (HANDLE_HIGH - HANDLE_BOW);

                Vector3 centre = new(side * path.X, path.Y, 0f);
                Vector3 tangent = Vector3.Normalize(new Vector3(side * slope.X, slope.Y, 0f));

                Vector3 axis1 = Vector3.UnitZ;                              //out of the arc's plane
                Vector3 axis2 = Vector3.Normalize(Vector3.Cross(tangent, axis1));

                for (int i = 0; i < HANDLE_SIDES; i++)
                {
                    float phi = i / (float)HANDLE_SIDES * MathHelper.TwoPi;
                    (float sp, float cp) = MathF.SinCos(phi);

                    Vector3 normal = axis1 * cp + axis2 * sp;
                    ringNormal[step, i] = normal;
                    ring[step, i] = centre + normal * HANDLE_TUBE_RADIUS;
                }
            }

            //The wall. The ends are left OPEN on purpose: both are buried inside the bowl's wall, and a cap
            //there is triangles nobody can see.
            for (int step = 0; step < HANDLE_STEPS; step++)
                for (int i = 0; i < HANDLE_SIDES; i++)
                {
                    int j = (i + 1) % HANDLE_SIDES;

                    Vector3 face = Vector3.Normalize(
                        ringNormal[step, i] + ringNormal[step, j]
                        + ringNormal[step + 1, i] + ringNormal[step + 1, j]);

                    builder.AddQuad(
                        ring[step, i], ring[step, j], ring[step + 1, j], ring[step + 1, i],
                        ringNormal[step, i], ringNormal[step, j],
                        ringNormal[step + 1, j], ringNormal[step + 1, i],
                        face);
                }
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();

            VertexBuffer = null;
            IndexBuffer = null;
        }
    }
}
