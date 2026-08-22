using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A coconut palm as real 3D geometry (#244), built the way <see cref="AcaciaMesh"/> is: two meshes,
    /// two materials, drawn as one instance. <see cref="Wood"/> is the trunk — a slim ring-scarred tube
    /// that bows over on a rolled curve — under the skirt of dead fronds that hangs from every grown
    /// palm's crown (a live crown over a bare trunk reads as a lollipop; the skirt is half of what says
    /// "palm" the way the acacia's forked boughs are what say "acacia"). <see cref="Fronds"/> is the live
    /// crown: eight to eleven fronds, each a tapered strip arcing out and drooping, radiating from the
    /// trunk's tip.
    /// <para>
    /// Every frond is a DOUBLE-SIDED sheet — each quad is added twice, once per face — because a frond is
    /// one leaf-thickness of material read from both sides, and the shared instancing path culls; the
    /// birds' wings solve the same problem with <c>CullNone</c>, which is not available to a draw that
    /// shares its rasterizer state with lathe-wound solids.
    /// </para>
    /// <para>
    /// The mesh's vertices carry a sway weight in their UV.x: 0 along the trunk, rising along each frond
    /// towards its tip. <c>Palm.fx</c> reads it so the wind moves the crown and not the trunk — a palm
    /// whose whole body waves reads as a kelp. The strip's UV.y is the across-frond coordinate, unused.
    /// </para>
    /// </summary>
    public sealed class PalmMesh : IDisposable
    {
        public IProceduralMesh Wood { get; }
        public IProceduralMesh Fronds { get; }

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="trunkRadius">Trunk radius at the crown end; the root flare is a multiple of it.</param>
        /// <param name="height">Height of the crown (the trunk's tip) above the ground.</param>
        /// <param name="frondLength">Base length of a crown frond — how wide the crown reads.</param>
        /// <param name="seed">Structural seed; every roll below comes off it, so no two variants are alike.</param>
        public PalmMesh(GraphicsDevice device, float trunkRadius, float height, float frondLength, int seed)
        {
            Random rng = new(seed);

            //The trunk's bow. Palms curve — grown on a shore wind, never machined straight up — and the
            //curve accumulates towards the crown (t², a palm bending under its own crown), which is why
            //the trunk cannot be a lathe: a lathe is strictly about the Y axis.
            float bow = height * (0.10f + 0.14f * (float)rng.NextDouble());
            float bowAngle = (float)rng.NextDouble() * MathHelper.TwoPi;
            Vector3 bowDir = new(MathF.Cos(bowAngle), 0f, MathF.Sin(bowAngle));
            Vector3 crown = bowDir * bow + Vector3.Up * height;

            Wood = new WoodMesh(device, trunkRadius, height, bowDir, bow, crown, frondLength, rng);
            Fronds = new FrondMesh(device, crown, frondLength, rng);
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Fronds as IDisposable)?.Dispose();
        }

        /// <summary>
        /// One drooping frond strip from <paramref name="crown"/> out along a horizontal direction: the
        /// spine arcs up briefly then droops (a palm frond leaves the crown pointing up and turns down
        /// under its own weight), the width swells mid-frond and tapers to a tip. Added twice per quad —
        /// once per face — with the top face's normal, so the sheet is double-sided.
        /// </summary>
        private static void AddFrondStrip(MeshBuilder builder, Vector3 crown, Vector3 dir, float length,
            float width, float rise, float droop, int segments)
        {
            Vector3 perp = new(-dir.Z, 0f, dir.X);

            Vector3 Spine(float t) =>
                crown + dir * (t * length) + Vector3.Up * (length * (rise * t - droop * t * t));

            //Swells to the full width two fifths out, keeps a third of it at the root and the tip: a
            //frond is narrow where it leaves the crown, broad in its blade, pointed at its end.
            float HalfWidth(float t) =>
                width * 0.5f * (0.35f + 0.65f * MathF.Sin(MathHelper.Pi * MathF.Min(t * 1.25f, 1f)));

            Vector3 prevC = Spine(0f);
            float prevW = HalfWidth(0f);
            for (int s = 0; s < segments; s++)
            {
                float t = (s + 1f) / segments;
                Vector3 center = Spine(t);
                float half = HalfWidth(t);

                //The top face's normal, off the quad's own edges: tilted the way the frond slopes, which
                //is what makes a drooping frond catch the sky on its upper side and shade under itself.
                Vector3 normal = Vector3.Normalize(Vector3.Cross(perp, center - prevC));

                Vector3 a = prevC - perp * prevW, b = prevC + perp * prevW;
                Vector3 c = center + perp * half, d = center - perp * half;

                float t0 = (float)s / segments;
                Vector2 uvA = new(t0, 0f), uvB = new(t0, 1f), uvC = new(t, 1f), uvD = new(t, 0f);

                //The top face, then the same quad again facing down: a leaf one material thick.
                builder.AddQuad(a, b, c, d, normal, normal, normal, normal, uvA, uvB, uvC, uvD, normal);
                builder.AddQuad(a, b, c, d, normal, normal, normal, normal, uvA, uvB, uvC, uvD, -normal);

                prevC = center;
                prevW = half;
            }
        }

        /// <summary>The trunk and the dead-frond skirt: the palm's wood, one material.</summary>
        private sealed class WoodMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            public WoodMesh(GraphicsDevice device, float trunkRadius, float height, Vector3 bowDir,
                float bow, Vector3 crown, float frondLength, Random rng)
            {
                MeshBuilder builder = new();
                const int RINGS = 9, SIDES = 7;

                //The trunk: a chain of tapered tube segments along the bow, flared at the root and scarred
                //with the rings old leaf bases leave (the radius modulation, a few percent — enough to
                //break the machined look, not enough to undulate).
                Vector3 prevCenter = Vector3.Zero;
                float prevRadius = RootRadius(trunkRadius, 0f);
                for (int r = 1; r < RINGS; r++)
                {
                    float t = r / (float)(RINGS - 1);
                    Vector3 center = bowDir * (bow * t * t) + Vector3.Up * (height * t);
                    float radius = RootRadius(trunkRadius, t);

                    AddTubeSegment(builder, prevCenter, prevRadius, center, radius, SIDES);

                    prevCenter = center;
                    prevRadius = radius;
                }

                //The skirt of dead fronds hanging from the crown: droop-heavy strips strung under it,
                //thinner than the live ones and pointing steeply down. They sway too (their sway weight
                //is the strip coordinate like any frond) — dead leaves are the palm's lightest moving part.
                int skirt = 4 + rng.Next(3);
                float skirtYaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                for (int f = 0; f < skirt; f++)
                {
                    float bearing = skirtYaw + MathHelper.TwoPi * f / skirt + (float)(rng.NextDouble() - 0.5) * 0.5f;
                    Vector3 dir = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));

                    AddFrondStrip(builder, crown, dir,
                        length: frondLength * 0.7f * (0.8f + 0.3f * (float)rng.NextDouble()),
                        width: frondLength * 0.09f,
                        rise: 0.04f + 0.03f * (float)rng.NextDouble(),
                        droop: 1.05f + 0.25f * (float)rng.NextDouble(),
                        segments: 5);
                }

                (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
                VertexBuffer = vertices;
                IndexBuffer = indices;
                PrimitiveCount = primitives;
                BoundingSphere = new BoundingSphere(
                    new Vector3(0f, height * 0.5f, 0f), height * 0.6f + frondLength * 0.5f);
            }

            //The trunk's radius along its run: flared to 1.55× at the root, slimming to 0.85× at the
            //crown, with the ring scars riding on it — eight of them along the trunk.
            private static float RootRadius(float trunkRadius, float t) =>
                trunkRadius * MathHelper.Lerp(1.55f, 0.85f, t)
                    * (1f + 0.05f * MathF.Sin(t * 8f * MathHelper.TwoPi));

            //One tube segment between two rings, smooth-shaded with radial normals and wound clockwise
            //seen from outside — the acacia trunk's own construction, shortened to a single ring pair.
            private static void AddTubeSegment(MeshBuilder builder, Vector3 a, float ra, Vector3 b, float rb, int sides)
            {
                Vector3 axis = b - a;
                if (axis.LengthSquared() < 1e-8f) return;

                Vector3 ref0 = MathF.Abs(Vector3.Normalize(axis).Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
                Vector3 u = Vector3.Normalize(Vector3.Cross(ref0, axis));
                Vector3 w = Vector3.Cross(axis, u);

                for (int s = 0; s < sides; s++)
                {
                    float a0 = MathHelper.TwoPi * s / sides;
                    float a1 = MathHelper.TwoPi * (s + 1) / sides;
                    Vector3 d0 = u * MathF.Cos(a0) + w * MathF.Sin(a0);
                    Vector3 d1 = u * MathF.Cos(a1) + w * MathF.Sin(a1);
                    Vector3 mid = Vector3.Normalize(d0 + d1);

                    //UV.x is the sway weight and the trunk does not sway; UV.y runs around the trunk.
                    builder.AddQuad(
                        a + d0 * ra, a + d1 * ra, b + d1 * rb, b + d0 * rb,
                        d0, d1, d1, d0,
                        Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero,
                        mid);
                }
            }

            public void Dispose()
            {
                VertexBuffer?.Dispose(); VertexBuffer = null;
                IndexBuffer?.Dispose(); IndexBuffer = null;
            }
        }

        /// <summary>The live crown: radiating drooping fronds, the palm's silhouette.</summary>
        private sealed class FrondMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            public FrondMesh(GraphicsDevice device, Vector3 crown, float frondLength, Random rng)
            {
                MeshBuilder builder = new();

                //Eight to eleven fronds, evenly spread with a jittered bearing. Each gets its own length,
                //width, rise and droop — a palm's crown is a shock of leaves at every angle it can hold,
                //not one shape revolved (the forest's variant lesson, within the one tree).
                int count = 8 + rng.Next(4);
                float baseYaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                for (int f = 0; f < count; f++)
                {
                    float bearing = baseYaw + MathHelper.TwoPi * f / count + (float)(rng.NextDouble() - 0.5) * 0.3f;
                    Vector3 dir = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));

                    AddFrondStrip(builder, crown, dir,
                        length: frondLength * (0.8f + 0.35f * (float)rng.NextDouble()),
                        width: frondLength * 0.16f * (0.85f + 0.3f * (float)rng.NextDouble()),
                        rise: 0.16f + 0.14f * (float)rng.NextDouble(),
                        droop: 0.52f + 0.22f * (float)rng.NextDouble(),
                        segments: 7);
                }

                (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
                VertexBuffer = vertices;
                IndexBuffer = indices;
                PrimitiveCount = primitives;
                BoundingSphere = new BoundingSphere(
                    crown + Vector3.Up * frondLength * 0.1f, frondLength * 1.1f);
            }

            public void Dispose()
            {
                VertexBuffer?.Dispose(); VertexBuffer = null;
                IndexBuffer?.Dispose(); IndexBuffer = null;
            }
        }
    }
}
