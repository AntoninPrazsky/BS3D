using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A soaring bird as real 3D geometry: a spindle body with a head on a neck, a fanned tail, and two
    /// wings that end in separated finger-like primaries. One mesh, shared by every bird in the flock —
    /// what differs between them is the world matrix and the two flap uniforms <c>Birds.fx</c> animates it
    /// from, so the shape below is the <b>rest pose</b> and nothing here is per-bird.
    /// <para>
    /// It replaces the camera-facing billboard whose pixel shader drew the entire bird (#235), for the same
    /// reason the acacias stopped being billboards in #202: a flat quad only ever shows one silhouette. On
    /// a tree that read as a paper cutout; on the one thing in this world that <i>moves</i> it also meant a
    /// bird could never bank into the turn it was flying, and its wings could only ever be the straight V
    /// that two line segments make. Both of those were the complaint.
    /// </para>
    /// <para>
    /// <b>Body space</b> follows XNA's own convention, so <see cref="Matrix.CreateWorld"/> can place a bird
    /// with no hand-rolled basis: <b>−Z is forward</b> (the beak), <b>+Y is up</b> (the back), <b>+X is the
    /// right wing</b>. The wingspan is exactly 1, so a bird is scaled by <see cref="BirdsConfig.Wingspan"/>
    /// and by nothing else.
    /// </para>
    /// <para>
    /// <b>Every vertex carries a texture coordinate the vertex shader animates from</b>, and it is the whole
    /// contract between this file and <c>Birds.fx</c>:
    /// <list type="bullet">
    /// <item><description><c>x</c> — the <b>signed spanwise station</b>, −1 at the left wing tip through 0
    /// at the spine to +1 at the right. Its magnitude is how far out along the wing the flap has travelled;
    /// its sign is which way that wing lifts. Body and tail vertices carry 0, which is how they take no
    /// flap at all without needing a flag of their own.</description></item>
    /// <item><description><c>y</c> — the vertex's <b>distance forward of the wing's mean line</b>, in body
    /// units. The wing's twist is a rotation about that line, and this is the only thing such a rotation
    /// needs, so the shader never has to be told where the line runs.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class BirdMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private const float HALF_SPAN = 0.5f;

        //The wing sheet starts INSIDE the body rather than at its surface, so the flap cannot open a seam
        //where the two meet: at this station the shader's bend curve has barely begun, and the little the
        //root does move stays under a body radius that swallows it.
        private const float ROOT_T = 0.055f;

        //Where the hand's sheet stops and the primaries take over. Past it the wing is not one surface.
        private const float FINGER_T = 0.72f;

        private const int SPAN_SEGMENTS = 8;
        private const int FINGERS = 5;
        private const int FINGER_SEGMENTS = 3;

        //The sheet's chordwise panels are TIED to the finger count on purpose: the fingers begin on the
        //sheet's last row, so if the two disagreed the finger roots would land between the sheet's own
        //vertices and the camber's curve would open a hairline crack along the join — at the one place on
        //the bird that has open sky behind it.
        private const int CHORD_SEGMENTS = FINGERS;

        //Wing camber as a fraction of the local chord, and where along the chord it peaks (a wing is
        //deepest nearer its leading edge than its middle; the exponent is what moves the peak forward).
        private const float CAMBER = 0.085f;
        private const float CAMBER_PEAK_BIAS = 0.75f;

        //The primaries. They RADIATE FROM THE WRIST rather than being placed one by one: each runs from its
        //own slice of the hand's last chord out to a tip on a fan of one radius about that chord's middle.
        //Placed that way the tips trace an arc and the slots between them open by themselves, and no finger
        //can be handed a length or a bearing that puts it outside the shape the wing was going to have.
        private const float FINGER_FAN_RADIUS = 0.132f;
        private const float FINGER_FAN_FIRST = -0.14f;  //radians off straight out — the foremost primary leads
        private const float FINGER_FAN_LAST = 0.52f;    //and the hindmost sweeps back across the trailing edge
        private const float FINGER_TIP_HALF_WIDTH = 0.010f;
        private const float FINGER_LIFT = 0.018f;

        //The tail: a flat fan off the body's rear, its outer corners pulled forward so it ends in a curve.
        private const float TAIL_ROOT_Z = 0.132f;
        private const float TAIL_TIP_Z = 0.272f;
        private const float TAIL_ROOT_HALF_WIDTH = 0.026f;
        private const float TAIL_TIP_HALF_WIDTH = 0.086f;
        private const float TAIL_CORNER_SWEEP = 0.026f;
        private const float TAIL_DROOP = 0.004f;
        private const int TAIL_COLUMNS = 6;
        private const int TAIL_ROWS = 3;

        private const int BODY_SEGMENTS = 10;

        //The body's profile from the beak to the tail base: (z along the body, radius there). The head is a
        //bump held off the breast by a narrower neck, which is what makes the silhouette read as a bird
        //rather than as a fuselage with wings on it.
        private static readonly Vector2[] BodyProfile =
        {
            new(-0.175f, 0.000f),
            new(-0.152f, 0.017f),
            new(-0.130f, 0.026f),
            new(-0.108f, 0.024f),
            new(-0.082f, 0.017f),
            new(-0.040f, 0.037f),
            new(0.008f, 0.044f),
            new(0.058f, 0.038f),
            new(0.104f, 0.026f),
            new(0.150f, 0.013f),
        };

        //How far the belly hangs below the wing plane, and the length of body it hangs over.
        private const float BELLY_DROP = 0.014f;
        private const float BELLY_LENGTH = 0.06f;

        public BirdMesh(GraphicsDevice device)
        {
            MeshBuilder builder = new();

            BuildBody(builder);
            BuildTail(builder);
            BuildWing(builder, side: 1f);
            BuildWing(builder, side: -1f);

            (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;

            //Sized for the ANIMATED bird rather than the rest pose: the flap carries the tips well above
            //the plane they are modelled in, and a sphere drawn round the rest pose alone would cull a
            //bird in mid-beat.
            BoundingSphere = new BoundingSphere(new Vector3(0f, 0f, 0.05f), 0.62f);
        }

        #region The wing's planform

        //The wing seen from above, as two smooth curves rather than a taper between straight edges: the
        //leading edge sweeps back with the square of the span and the trailing edge comes forward with the
        //cube of it, so the chord narrows towards the hand the way a real wing's does.
        private static float LeadingEdgeZ(float t) => -0.090f + 0.075f * t * t;

        private static float TrailingEdgeZ(float t) => 0.098f - 0.115f * t * t * t;

        private static float MeanZ(float t) => 0.5f * (LeadingEdgeZ(t) + TrailingEdgeZ(t));

        /// <summary>A point on the wing sheet: <paramref name="t"/> along the span, <paramref name="chord"/>
        /// from the leading edge (0) to the trailing edge (1).</summary>
        private static Vector3 WingPoint(float t, float chord)
        {
            float z = MathHelper.Lerp(LeadingEdgeZ(t), TrailingEdgeZ(t), chord);
            float camber = CAMBER * (TrailingEdgeZ(t) - LeadingEdgeZ(t))
                * MathF.Sin(MathF.PI * MathF.Pow(chord, CAMBER_PEAK_BIAS));

            return new Vector3(t * HALF_SPAN, camber, z);
        }

        /// <summary>The sheet's normal taken from the surface itself rather than assumed to be straight up,
        /// so the camber shades. The cross order is chordwise × spanwise, which is +Z × +X — up.</summary>
        private static Vector3 WingNormal(float t, float chord)
        {
            const float H = 0.01f;

            Vector3 alongSpan = WingPoint(MathF.Min(t + H, 1f), chord) - WingPoint(MathF.Max(t - H, 0f), chord);
            Vector3 alongChord = WingPoint(t, MathF.Min(chord + H, 1f)) - WingPoint(t, MathF.Max(chord - H, 0f));

            return Vector3.Normalize(Vector3.Cross(alongChord, alongSpan));
        }

        /// <summary>The pair every wing vertex carries: its signed spanwise station and its distance forward
        /// of the mean line. See the class remarks — this is the contract with <c>Birds.fx</c>.</summary>
        private static Vector2 WingData(float t, float side, float z) => new(t * side, MeanZ(t) - z);

        #endregion

        private void BuildWing(MeshBuilder builder, float side)
        {
            Vector3 Mirror(Vector3 v) => new(v.X * side, v.Y, v.Z);

            for (int i = 0; i < SPAN_SEGMENTS; i++)
            {
                float t0 = MathHelper.Lerp(ROOT_T, FINGER_T, i / (float)SPAN_SEGMENTS);
                float t1 = MathHelper.Lerp(ROOT_T, FINGER_T, (i + 1) / (float)SPAN_SEGMENTS);

                for (int j = 0; j < CHORD_SEGMENTS; j++)
                {
                    float c0 = j / (float)CHORD_SEGMENTS;
                    float c1 = (j + 1) / (float)CHORD_SEGMENTS;

                    Vector3 p00 = WingPoint(t0, c0), p10 = WingPoint(t1, c0);
                    Vector3 p11 = WingPoint(t1, c1), p01 = WingPoint(t0, c1);
                    Vector3 n00 = WingNormal(t0, c0), n10 = WingNormal(t1, c0);
                    Vector3 n11 = WingNormal(t1, c1), n01 = WingNormal(t0, c1);

                    builder.AddQuad(
                        Mirror(p00), Mirror(p10), Mirror(p11), Mirror(p01),
                        Mirror(n00), Mirror(n10), Mirror(n11), Mirror(n01),
                        WingData(t0, side, p00.Z), WingData(t1, side, p10.Z),
                        WingData(t1, side, p11.Z), WingData(t0, side, p01.Z),
                        Mirror(Vector3.Normalize(n00 + n10 + n11 + n01)));
                }
            }

            BuildPrimaries(builder, side, Mirror);
        }

        /// <summary>
        /// The fingered wing tip. Each primary leaves the sheet's last row across its own slice of the chord
        /// — the same slice the sheet's own vertices already bound, so the join is seamless — and runs out to
        /// its own tip on a fan about the wrist. Because the tips fan and the roots do not, the slots between
        /// them open by themselves towards the end of the wing.
        /// </summary>
        private void BuildPrimaries(MeshBuilder builder, float side, Func<Vector3, Vector3> mirror)
        {
            Vector3 wrist = new(FINGER_T * HALF_SPAN, 0f, MeanZ(FINGER_T));

            for (int finger = 0; finger < FINGERS; finger++)
            {
                Vector3 rootFront = WingPoint(FINGER_T, finger / (float)FINGERS);
                Vector3 rootBack = WingPoint(FINGER_T, (finger + 1) / (float)FINGERS);

                float fan = MathHelper.Lerp(FINGER_FAN_FIRST, FINGER_FAN_LAST, finger / (float)(FINGERS - 1));
                Vector3 tip = wrist + new Vector3(MathF.Cos(fan), 0f, MathF.Sin(fan)) * FINGER_FAN_RADIUS;
                float tipT = tip.X / HALF_SPAN;

                Vector3 tipFront = new(tip.X, 0f, tip.Z - FINGER_TIP_HALF_WIDTH);
                Vector3 tipBack = new(tip.X, 0f, tip.Z + FINGER_TIP_HALF_WIDTH);

                //A primary bends up along its length: the outer feathers of a soaring bird carry load and
                //curve under it, and a tip left dead flat is the last piece of plank in the wing.
                Vector3 Front(float u) => Lift(Vector3.Lerp(rootFront, tipFront, u), u);
                Vector3 Back(float u) => Lift(Vector3.Lerp(rootBack, tipBack, u), u);

                for (int i = 0; i < FINGER_SEGMENTS; i++)
                {
                    float u0 = i / (float)FINGER_SEGMENTS;
                    float u1 = (i + 1) / (float)FINGER_SEGMENTS;

                    Vector3 f0 = Front(u0), f1 = Front(u1), b1 = Back(u1), b0 = Back(u0);

                    Vector3 normal = Vector3.Normalize(Vector3.Cross(b0 - f0, f1 - f0));
                    if (normal.Y < 0f) normal = -normal;
                    normal = mirror(normal);

                    float t0 = MathHelper.Lerp(FINGER_T, tipT, u0);
                    float t1 = MathHelper.Lerp(FINGER_T, tipT, u1);

                    builder.AddQuad(
                        mirror(f0), mirror(f1), mirror(b1), mirror(b0),
                        normal, normal, normal, normal,
                        WingData(t0, side, f0.Z), WingData(t1, side, f1.Z),
                        WingData(t1, side, b1.Z), WingData(t0, side, b0.Z),
                        normal);
                }
            }
        }

        private static Vector3 Lift(Vector3 point, float u)
            => new(point.X, point.Y + FINGER_LIFT * u * u, point.Z);

        private void BuildBody(MeshBuilder builder)
        {
            for (int station = 0; station < BodyProfile.Length - 1; station++)
            {
                for (int i = 0; i < BODY_SEGMENTS; i++)
                {
                    float a0 = i / (float)BODY_SEGMENTS * MathHelper.TwoPi;
                    float a1 = (i + 1) / (float)BODY_SEGMENTS * MathHelper.TwoPi;

                    Vector3 p00 = BodyPoint(station, a0), p01 = BodyPoint(station, a1);
                    Vector3 p10 = BodyPoint(station + 1, a0), p11 = BodyPoint(station + 1, a1);
                    Vector3 n00 = BodyNormal(station, a0), n01 = BodyNormal(station, a1);
                    Vector3 n10 = BodyNormal(station + 1, a0), n11 = BodyNormal(station + 1, a1);

                    Vector3 face = Vector3.Normalize(n00 + n01 + n10 + n11);

                    //The beak is a point, so its ring collapses and a quad there would be a degenerate pair.
                    if (BodyProfile[station].Y <= 0f)
                        builder.AddTriangle(p00, p10, p11, n00, n10, n11, face);
                    else
                        builder.AddQuad(p00, p10, p11, p01, n00, n10, n11, n01, face);
                }
            }

            //Close the rear. The tail is a zero-thickness sheet, so without this cap the body is an open
            //tube and the sky shows through the ring around it whenever a bird is seen from below.
            Vector2 last = BodyProfile[^1];
            Vector3 centre = new(0f, BodyCentreY(last.X), last.X);

            for (int i = 0; i < BODY_SEGMENTS; i++)
            {
                float a0 = i / (float)BODY_SEGMENTS * MathHelper.TwoPi;
                float a1 = (i + 1) / (float)BODY_SEGMENTS * MathHelper.TwoPi;

                builder.AddTriangle(
                    centre, BodyPoint(BodyProfile.Length - 1, a0), BodyPoint(BodyProfile.Length - 1, a1),
                    Vector3.Backward, Vector3.Backward, Vector3.Backward, Vector3.Backward);
            }
        }

        private static Vector3 BodyPoint(int station, float angle)
        {
            Vector2 profile = BodyProfile[station];

            return new Vector3(
                profile.Y * MathF.Sin(angle),
                BodyCentreY(profile.X) + profile.Y * MathF.Cos(angle),
                profile.X);
        }

        private static Vector3 BodyNormal(int station, float angle)
        {
            const float H = 0.02f;

            int before = Math.Max(station - 1, 0);
            int after = Math.Min(station + 1, BodyProfile.Length - 1);

            Vector3 alongBody = BodyPoint(after, angle) - BodyPoint(before, angle);
            Vector3 around = BodyPoint(station, angle + H) - BodyPoint(station, angle - H);

            Vector3 normal = Vector3.Cross(alongBody, around);

            //The beak's ring has no radius, so there is no "around" there and no surface to take a normal
            //from: the tip faces where the bird is pointing.
            if (normal.LengthSquared() < 1e-12f) return Vector3.Forward;

            normal.Normalize();

            //Point it out of the body rather than into it. The cross product's sign follows the profile's
            //slope, which reverses wherever the body stops widening, so the radial direction is what has to
            //say which way is outward.
            Vector3 radial = new(MathF.Sin(angle), MathF.Cos(angle), 0f);

            return Vector3.Dot(normal, radial) < 0f ? -normal : normal;
        }

        private static float BodyCentreY(float z)
            => -BELLY_DROP * MathF.Exp(-(z * z) / (BELLY_LENGTH * BELLY_LENGTH));

        private void BuildTail(MeshBuilder builder)
        {
            for (int row = 0; row < TAIL_ROWS; row++)
            {
                float v0 = row / (float)TAIL_ROWS;
                float v1 = (row + 1) / (float)TAIL_ROWS;

                for (int column = 0; column < TAIL_COLUMNS; column++)
                {
                    float x0 = column / (float)TAIL_COLUMNS * 2f - 1f;
                    float x1 = (column + 1) / (float)TAIL_COLUMNS * 2f - 1f;

                    Vector3 p00 = TailPoint(x0, v0), p10 = TailPoint(x0, v1);
                    Vector3 p11 = TailPoint(x1, v1), p01 = TailPoint(x1, v0);

                    Vector3 normal = Vector3.Normalize(Vector3.Cross(p01 - p00, p10 - p00));
                    if (normal.Y < 0f) normal = -normal;

                    builder.AddQuad(p00, p10, p11, p01, normal, normal, normal, normal, normal);
                }
            }
        }

        /// <summary>A point on the tail fan: <paramref name="x"/> runs −1..1 across it, <paramref name="v"/>
        /// 0 at the body to 1 at the end. The sweep term pulls the outer corners forward, so the fan ends in
        /// a curve rather than in a square.</summary>
        private static Vector3 TailPoint(float x, float v)
        {
            float halfWidth = MathHelper.Lerp(TAIL_ROOT_HALF_WIDTH, TAIL_TIP_HALF_WIDTH, MathF.Sqrt(v));
            float z = MathHelper.Lerp(TAIL_ROOT_Z, TAIL_TIP_Z, v) - TAIL_CORNER_SWEEP * v * x * x;

            return new Vector3(x * halfWidth, -TAIL_DROOP * v, z);
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose(); VertexBuffer = null;
            IndexBuffer?.Dispose(); IndexBuffer = null;
        }
    }
}
