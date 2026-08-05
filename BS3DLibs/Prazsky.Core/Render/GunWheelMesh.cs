using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One spoked gun-carriage wheel: a torus rim, a cylindrical hub and a ring of flat spokes, as a single
    /// mesh — so a wheel is one instance, and the pair on an axle is one two-instance draw. The wheel lies in
    /// the local <b>YZ plane</b> with the axle along <b>X</b>, which makes its spin a plain
    /// <see cref="Matrix.CreateRotationX(float)"/> — and the spokes are the whole reason the spin is worth
    /// drawing: a rolling torus alone reads as standing still.
    /// <para>
    /// Origin is the hub's centre. Built with <see cref="MeshBuilder"/>, so every face is wound against the
    /// normal it shows (the CLAUDE.md winding trap cannot bite piece by piece).
    /// </para>
    /// </summary>
    public class GunWheelMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        //Angular tessellation: enough for a round silhouette on a prop a few units from the lens, in the
        //CannonMesh spirit (its wall runs 24 segments); the tube is coarser, being a hand's width across.
        private const int RIM_SEGMENTS = 24;
        private const int TUBE_SEGMENTS = 10;
        private const int HUB_SEGMENTS = 12;

        /// <param name="radius">Outer radius of the wheel — rim centreline plus tube.</param>
        /// <param name="tubeRadius">Radius of the rim's tube (half the felloe's thickness).</param>
        /// <param name="hubRadius">Radius of the hub cylinder the spokes grow from.</param>
        /// <param name="hubHalfWidth">Half the hub's width along the axle.</param>
        /// <param name="spokes">How many spokes. Flat bars, a hair thinner than the hub is wide.</param>
        public GunWheelMesh(GraphicsDevice graphicsDevice, float radius, float tubeRadius, float hubRadius,
            float hubHalfWidth, int spokes)
        {
            MeshBuilder builder = new();

            float major = radius - tubeRadius; //the rim tube's centreline circle

            //The rim: a torus about the X axis. u walks the wheel's circle in the YZ plane, v the tube's own
            //circle; the tube-radial direction is the vertex normal, which smooth-shades the felloe.
            for (int u = 0; u < RIM_SEGMENTS; u++)
            {
                float u0 = u / (float)RIM_SEGMENTS * MathHelper.TwoPi;
                float u1 = (u + 1) / (float)RIM_SEGMENTS * MathHelper.TwoPi;

                Vector3 ring0 = new(0f, MathF.Cos(u0), MathF.Sin(u0));
                Vector3 ring1 = new(0f, MathF.Cos(u1), MathF.Sin(u1));

                for (int v = 0; v < TUBE_SEGMENTS; v++)
                {
                    float v0 = v / (float)TUBE_SEGMENTS * MathHelper.TwoPi;
                    float v1 = (v + 1) / (float)TUBE_SEGMENTS * MathHelper.TwoPi;

                    Vector3 n00 = TubeNormal(ring0, v0);
                    Vector3 n01 = TubeNormal(ring0, v1);
                    Vector3 n10 = TubeNormal(ring1, v0);
                    Vector3 n11 = TubeNormal(ring1, v1);

                    builder.AddQuad(
                        ring0 * major + n00 * tubeRadius,
                        ring1 * major + n10 * tubeRadius,
                        ring1 * major + n11 * tubeRadius,
                        ring0 * major + n01 * tubeRadius,
                        n00, n10, n11, n01, n00 + n10 + n11 + n01);
                }
            }

            //The hub, with its end caps showing on both sides of the wheel
            builder.AddTubeX(Vector3.Zero, hubHalfWidth, hubRadius, HUB_SEGMENTS);

            //The spokes: flat bars from the hub out to the inside of the rim, evenly around the wheel. Each is
            //an oriented box — the radial direction carries its length, the axle its thickness.
            float spokeInner = hubRadius * 0.6f; //buried in the hub, so the joint never shows a crack
            float spokeOuter = major;            //buried in the rim tube likewise

            for (int s = 0; s < spokes; s++)
            {
                float angle = s / (float)spokes * MathHelper.TwoPi;
                Vector3 radial = new(0f, MathF.Cos(angle), MathF.Sin(angle));
                Vector3 tangent = new(0f, -MathF.Sin(angle), MathF.Cos(angle));

                Vector3 centre = radial * ((spokeInner + spokeOuter) * 0.5f);

                builder.AddBox(centre,
                    new Vector3(hubHalfWidth * 0.55f, 0f, 0f),      //thickness along the axle
                    tangent * (tubeRadius * 0.55f),                  //width across the wheel's plane
                    radial * ((spokeOuter - spokeInner) * 0.5f));    //length, hub to rim
            }

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);
            BoundingSphere = new BoundingSphere(Vector3.Zero, radius);
        }

        //The tube's outward direction at ring position `ring` and tube angle v: the ring direction carries
        //cos v (out of the wheel's circle) and the axle carries sin v (out of the wheel's plane)
        private static Vector3 TubeNormal(Vector3 ring, float v) =>
            ring * MathF.Cos(v) + Vector3.UnitX * MathF.Sin(v);

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }
}
