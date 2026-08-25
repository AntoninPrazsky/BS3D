using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The gun carriage's <b>omnidirectional wheel</b> (#129), in two meshes that are drawn separately
    /// because they have to move separately: this one is the <b>body</b> — the two side plates and the hub —
    /// and <see cref="OmniRollerMesh"/> is one of the barrel rollers that ring it. The seats the rollers sit
    /// in are <see cref="RollerSeats"/>, here rather than with the roller, because the seat is a fact about
    /// the wheel's envelope and the two must be derived from one set of figures.
    /// <para>
    /// It replaced a spoked wagon wheel, and the reason was not only the look. The carriage's ground velocity
    /// in its own frame is exactly two numbers — <c>Cannon.WheelTravel</c> along the rolling direction and
    /// <c>Cannon.OrbitTravel</c> along the axle — and those are exactly the two axes this wheel has. The old
    /// wheel could only answer the first, so the gun crabbed sideways on wheels that stood still; this one
    /// answers both, with the body spinning for the advance and the rollers for the orbit.
    /// </para>
    /// <para>
    /// <b>Why two rows of rollers and not one.</b> A single row cannot both fill the circle and clear itself:
    /// the no-collision condition is <c>L ≤ a·tan(π/N)</c> (see <see cref="RollerSeats"/>), and at the roller
    /// length the envelope wants, that caps N at six — six rollers leave visible gaps at their ends, which the
    /// wheel would bump over as it rolled. Two rows half a pitch apart cover each other's gaps, and that is
    /// also what a real omni wheel looks like.
    /// </para>
    /// <para>
    /// The wheel lies in the local <b>YZ plane</b> with the axle along <b>X</b>, exactly as the spoked wheel
    /// did, so the body's spin is still a plain <see cref="Matrix.CreateRotationX(float)"/>. Origin is the
    /// hub's centre. Built with <see cref="MeshBuilder"/>, so every face is wound against the normal it shows.
    /// </para>
    /// </summary>
    public sealed class OmniWheelMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        //Angular tessellation of the plates, in the spoked wheel's spirit — enough for a round silhouette on
        //a prop that fills a quarter of the frame's height from the play camera.
        private const int PLATE_SEGMENTS = 24;
        private const int HUB_SEGMENTS = 12;

        /// <param name="radius">Outer radius of the wheel — the circle the rollers' envelope must trace.</param>
        /// <param name="rollerRadius">The rollers' fattest radius. The plates stop at <c>radius - this</c>, so
        /// they sit inside the envelope and never touch the ground.</param>
        /// <param name="rollerRowOffset">How far each row of rollers sits off the wheel's mid-plane.</param>
        /// <param name="hubRadius">Radius of the hub cylinder between the plates.</param>
        /// <param name="plateThickness">Thickness of one side plate.</param>
        public OmniWheelMesh(GraphicsDevice graphicsDevice, float radius, float rollerRadius,
            float rollerRowOffset, float hubRadius, float plateThickness)
        {
            MeshBuilder builder = new();

            float plateRadius = radius - rollerRadius;                       //the roller axes' circle
            float plateInnerX = rollerRowOffset + rollerRadius;              //clear of the rollers' fattest point
            float plateOuterX = plateInnerX + plateThickness;

            //The two side plates the roller axles run between: an annulus each, hub to the roller circle.
            //Solid rather than spoked on purpose — an omni wheel's plates ARE solid, and the rollers are what
            //the eye is meant to read on this wheel.
            AddPlate(builder, plateInnerX, plateOuterX, hubRadius, plateRadius);
            AddPlate(builder, -plateOuterX, -plateInnerX, hubRadius, plateRadius);

            //The hub between them, its ends showing on both sides
            builder.AddTubeX(Vector3.Zero, plateOuterX, hubRadius, HUB_SEGMENTS);

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);
            BoundingSphere = new BoundingSphere(Vector3.Zero, radius);
        }

        //One annular plate spanning x in [x0, x1]: the outer rim wall, the inner bore wall and the two annular
        //faces. Each face takes its own normal, so the plate reads as a machined disc rather than a soft one.
        private static void AddPlate(MeshBuilder builder, float x0, float x1, float innerRadius, float outerRadius)
        {
            for (int i = 0; i < PLATE_SEGMENTS; i++)
            {
                float a0 = i / (float)PLATE_SEGMENTS * MathHelper.TwoPi;
                float a1 = (i + 1) / (float)PLATE_SEGMENTS * MathHelper.TwoPi;

                Vector3 d0 = new(0f, MathF.Cos(a0), MathF.Sin(a0));
                Vector3 d1 = new(0f, MathF.Cos(a1), MathF.Sin(a1));
                Vector3 mid = Vector3.Normalize(d0 + d1);

                Vector3 o0 = d0 * outerRadius, o1 = d1 * outerRadius;
                Vector3 i0 = d0 * innerRadius, i1 = d1 * innerRadius;

                Vector3 left = new(x0, 0f, 0f), right = new(x1, 0f, 0f);

                //Outer wall, facing out of the wheel's circle
                builder.AddQuad(o0 + left, o1 + left, o1 + right, o0 + right, d0, d1, d1, d0, mid);

                //Inner bore, facing in
                builder.AddQuad(i0 + left, i1 + left, i1 + right, i0 + right, -d0, -d1, -d1, -d0, -mid);

                //The two annular faces, each along its own side of the axle
                builder.AddQuad(i0 + left, i1 + left, o1 + left, o0 + left,
                    Vector3.Left, Vector3.Left, Vector3.Left, Vector3.Left, Vector3.Left);
                builder.AddQuad(i0 + right, i1 + right, o1 + right, o0 + right,
                    Vector3.Right, Vector3.Right, Vector3.Right, Vector3.Right, Vector3.Right);
            }
        }

        /// <summary>
        /// Where each roller sits in the wheel's own frame: a matrix taking the roller's model space (its axis
        /// along <b>X</b>, as <see cref="OmniRollerMesh"/> builds it) onto its seat. Constant per wheel, so the
        /// caller builds them once and only multiplies the spin through them per frame.
        /// <para>
        /// A roller's axis is <b>tangent to the circle of radius <c>a = radius - rollerRadius</c>, in the
        /// wheel's plane</b> — which is what makes the roller's own rolling direction the wheel's axle, i.e.
        /// the sideways one. The rows are offset half a pitch from each other in angle and to opposite sides of
        /// the mid-plane in X, so each row's rollers cover the gaps between the other's.
        /// </para>
        /// <para>
        /// <b>The no-collision condition is <c>rollerHalfLength ≤ a·tan(π/rollersPerRow)</c></b> — two
        /// neighbouring tangents in a row cross at radius <c>a/cos(π/N)</c>, and a roller must end before its
        /// axis reaches that crossing. Checked here rather than trusted: the constructor throws if a caller's
        /// figures break it, because the failure is rollers growing through each other, which reads as a
        /// modelling mistake rather than as a wheel.
        /// </para>
        /// </summary>
        public static Matrix[] RollerSeats(float radius, float rollerRadius, float rollerHalfLength,
            float rollerRowOffset, int rows, int rollersPerRow)
        {
            float axisRadius = radius - rollerRadius;

            float clearance = axisRadius * MathF.Tan(MathF.PI / rollersPerRow);
            if (rollerHalfLength > clearance)
                throw new ArgumentException(
                    $"Omni wheel rollers collide within a row: half-length {rollerHalfLength} exceeds " +
                    $"a·tan(π/N) = {clearance}.", nameof(rollerHalfLength));

            //And the OTHER collision, which the within-row one does not cover and which is the reason the rows
            //are pushed apart along the axle at all. Rows sit half a pitch from each other, so their tangents
            //cross much sooner — at t = a·tan(π/(N·rows)) — and there they are both still fat. The row offset
            //has to exceed the profile's radius at that crossing or the two rows grow through each other.
            if (rows > 1)
            {
                float crossing = axisRadius * MathF.Tan(MathF.PI / (rollersPerRow * rows));
                float fatThere = radius - MathF.Sqrt(axisRadius * axisRadius + crossing * crossing);

                if (rollerRowOffset <= fatThere)
                    throw new ArgumentException(
                        $"Omni wheel rows collide: offset {rollerRowOffset} does not clear the rollers' radius " +
                        $"{fatThere} where neighbouring rows' axes cross.", nameof(rollerRowOffset));
            }

            Matrix[] seats = new Matrix[rows * rollersPerRow];

            for (int row = 0; row < rows; row++)
            {
                //Rows alternate to either side of the mid-plane, and step half a pitch round for each one
                float x = rollerRowOffset * (row % 2 == 0 ? 1f : -1f);
                float phaseStep = MathHelper.TwoPi / rollersPerRow / rows;

                for (int i = 0; i < rollersPerRow; i++)
                {
                    float phi = i * MathHelper.TwoPi / rollersPerRow + row * phaseStep;

                    float cos = MathF.Cos(phi), sin = MathF.Sin(phi);

                    Vector3 axis = new(0f, -sin, cos);   //tangent, in the wheel's plane — the roller's own X
                    Vector3 radial = new(0f, cos, sin);  //outward — the roller's own Y
                    Vector3 third = Vector3.Cross(axis, radial); //right-handed by construction, so no mirror

                    Matrix seat = new(
                        axis.X, axis.Y, axis.Z, 0f,
                        radial.X, radial.Y, radial.Z, 0f,
                        third.X, third.Y, third.Z, 0f,
                        x, axisRadius * cos, axisRadius * sin, 1f);

                    seats[row * rollersPerRow + i] = seat;
                }
            }

            return seats;
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }

    /// <summary>
    /// One barrel roller of the omnidirectional wheel (#129) — a surface of revolution about its own <b>X</b>
    /// axis, so its spin is a plain <see cref="Matrix.CreateRotationX(float)"/> just as the wheel body's is.
    /// The wheel carries <c>rows × rollersPerRow</c> of them, all one mesh under
    /// <see cref="OmniWheelMesh.RollerSeats"/>, and they are drawn as their own instances precisely because
    /// they must turn independently of the body.
    /// <para>
    /// <b>The profile is not a cylinder and cannot be.</b> The wheel has to roll smoothly, so the rollers'
    /// surfaces, swept about the wheel's axle, must trace the wheel's own circle. A roller's axis is tangent
    /// at radius <c>a</c>, so the axis point at distance <c>t</c> along it is <c>√(a² + t²)</c> from the wheel
    /// axis, and the roller's radius there must make up the difference:
    /// </para>
    /// <code>ρ(t) = wheelRadius − √(a² + t²)</code>
    /// <para>
    /// That is the whole shape: fattest at its middle (<c>ρ(0) = wheelRadius − a</c>, i.e. the roller radius
    /// the wheel was specified with) and thinning towards its ends. A cylindrical roller would make the wheel
    /// polygonal and it would visibly bump as it rolled.
    /// </para>
    /// </summary>
    public sealed class OmniRollerMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        //Around the roller and along it. Eight and eight: a roller is a hand's width across and reads at about
        //thirty pixels from the play camera, so this is already past what the silhouette can show.
        private const int RING_SEGMENTS = 8;
        private const int LENGTH_STEPS = 8;

        /// <param name="wheelRadius">The wheel's outer radius — the circle this roller's surface must trace.</param>
        /// <param name="rollerRadius">The roller's fattest radius, at its middle.</param>
        /// <param name="halfLength">Half the roller's length along its own axis. Kept short of where the
        /// profile would reach zero, so a roller ends in a small flat rather than in a needle.</param>
        public OmniRollerMesh(GraphicsDevice graphicsDevice, float wheelRadius, float rollerRadius, float halfLength)
        {
            MeshBuilder builder = new();

            float axisRadius = wheelRadius - rollerRadius;

            for (int s = 0; s < LENGTH_STEPS; s++)
            {
                float t0 = MathHelper.Lerp(-halfLength, halfLength, s / (float)LENGTH_STEPS);
                float t1 = MathHelper.Lerp(-halfLength, halfLength, (s + 1) / (float)LENGTH_STEPS);

                float r0 = Profile(wheelRadius, axisRadius, t0);
                float r1 = Profile(wheelRadius, axisRadius, t1);

                //dρ/dt = -t/√(a²+t²), so the outward normal leans along the axis by its negative
                float slope0 = t0 / MathF.Sqrt(axisRadius * axisRadius + t0 * t0);
                float slope1 = t1 / MathF.Sqrt(axisRadius * axisRadius + t1 * t1);

                for (int i = 0; i < RING_SEGMENTS; i++)
                {
                    float a0 = i / (float)RING_SEGMENTS * MathHelper.TwoPi;
                    float a1 = (i + 1) / (float)RING_SEGMENTS * MathHelper.TwoPi;

                    Vector3 d0 = new(0f, MathF.Cos(a0), MathF.Sin(a0));
                    Vector3 d1 = new(0f, MathF.Cos(a1), MathF.Sin(a1));

                    Vector3 n00 = Vector3.Normalize(new Vector3(slope0, 0f, 0f) + d0);
                    Vector3 n01 = Vector3.Normalize(new Vector3(slope0, 0f, 0f) + d1);
                    Vector3 n10 = Vector3.Normalize(new Vector3(slope1, 0f, 0f) + d0);
                    Vector3 n11 = Vector3.Normalize(new Vector3(slope1, 0f, 0f) + d1);

                    Vector3 p00 = d0 * r0 + Vector3.UnitX * t0;
                    Vector3 p01 = d1 * r0 + Vector3.UnitX * t0;
                    Vector3 p10 = d0 * r1 + Vector3.UnitX * t1;
                    Vector3 p11 = d1 * r1 + Vector3.UnitX * t1;

                    builder.AddQuad(p00, p10, p11, p01, n00, n10, n11, n01, n00 + n10 + n11 + n01);
                }
            }

            //The ends are DOMED and not flat, and that is a fix rather than a flourish. A flat cap's normal is
            //the roller's own axis, which after the seat is the wheel's tangent — horizontal all round the rim.
            //Lit by a sky above and a sun off to one side, a horizontal face gets almost nothing, and the first
            //pass came out with a black ellipse on every roller end that read as an open tube. A dome's normals
            //sweep from the axis round to the barrel's, so an end catches light from wherever the barrel does.
            AddEndDome(builder, -halfLength, Profile(wheelRadius, axisRadius, -halfLength), -1f);
            AddEndDome(builder, halfLength, Profile(wheelRadius, axisRadius, halfLength), 1f);

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);
            BoundingSphere = new BoundingSphere(Vector3.Zero, MathF.Max(halfLength, rollerRadius));
        }

        /// <summary>The envelope condition, and the one line this whole mesh is: see the class note.</summary>
        private static float Profile(float wheelRadius, float axisRadius, float t) =>
            MathF.Max(wheelRadius - MathF.Sqrt(axisRadius * axisRadius + t * t), 0f);

        //A half-dome closing the barrel at x, of the radius the profile has there. `side` is +1 for the end
        //the dome bulges towards +X and -1 for the other. Squashed to a third of its radius along the axis:
        //a full hemisphere would lengthen the roller by its own radius and push it into its neighbour.
        private static void AddEndDome(MeshBuilder builder, float x, float radius, float side)
        {
            const int RINGS = 3;
            float bulge = radius * 0.34f;

            for (int r = 0; r < RINGS; r++)
            {
                //Latitude as a quarter-circle, so the dome meets the barrel tangentially at r = 0
                float p0 = r / (float)RINGS * MathHelper.PiOver2;
                float p1 = (r + 1) / (float)RINGS * MathHelper.PiOver2;

                float r0 = radius * MathF.Cos(p0), r1 = radius * MathF.Cos(p1);
                float x0 = x + side * bulge * MathF.Sin(p0), x1 = x + side * bulge * MathF.Sin(p1);

                for (int i = 0; i < RING_SEGMENTS; i++)
                {
                    float a0 = i / (float)RING_SEGMENTS * MathHelper.TwoPi;
                    float a1 = (i + 1) / (float)RING_SEGMENTS * MathHelper.TwoPi;

                    Vector3 d0 = new(0f, MathF.Cos(a0), MathF.Sin(a0));
                    Vector3 d1 = new(0f, MathF.Cos(a1), MathF.Sin(a1));

                    //The normal of a squashed dome still leans out along the axis with latitude, which is all
                    //this needs: what it must not be is constant along the axis, as the flat cap's was
                    Vector3 axial = new(side * MathF.Sin(p0), 0f, 0f);
                    Vector3 axial1 = new(side * MathF.Sin(p1), 0f, 0f);

                    Vector3 n00 = Vector3.Normalize(d0 * MathF.Cos(p0) + axial);
                    Vector3 n01 = Vector3.Normalize(d1 * MathF.Cos(p0) + axial);
                    Vector3 n10 = Vector3.Normalize(d0 * MathF.Cos(p1) + axial1);
                    Vector3 n11 = Vector3.Normalize(d1 * MathF.Cos(p1) + axial1);

                    Vector3 p00 = d0 * r0 + Vector3.UnitX * x0;
                    Vector3 p01 = d1 * r0 + Vector3.UnitX * x0;
                    Vector3 p10 = d0 * r1 + Vector3.UnitX * x1;
                    Vector3 p11 = d1 * r1 + Vector3.UnitX * x1;

                    builder.AddQuad(p00, p10, p11, p01, n00, n10, n11, n01, n00 + n10 + n11 + n01);
                }
            }
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
