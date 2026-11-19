using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A fallen tree for the savanna (#451): a tapered, slightly bent log lying along +X with its ends sawn
    /// (capped — the cut end is what faces a camera walking past), and a few broken branch stubs standing
    /// off it. Built from <see cref="TubeGeometry"/>'s sticks like the acacia's wood, and drawn in the same
    /// bleached deadwood colour as a dead tree. Since #462 the aurora's boreal wood lays a few in the snow
    /// too, through <see cref="ForestScatterRenderer"/>, beside the standing <see cref="SnagMesh"/>.
    /// </summary>
    public sealed class DeadwoodMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>End to end along +X, at scale 1: what a planter lays the log on the ground by (#938).</summary>
        public float Length { get; }

        /// <summary>The thick end's radius, at scale 1.</summary>
        public float Radius { get; }

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="length">End to end.</param>
        /// <param name="radius">At the thick (root) end; the log tapers to about half of it.</param>
        /// <param name="seed">Rolls the bend, the taper and the stubs.</param>
        public DeadwoodMesh(GraphicsDevice device, float length, float radius, int seed)
        {
            (List<VertexPositionNormalTexture> v, List<short> idx, BoundingSphere bounds) = Build(length, radius, seed);
            PrimitiveCount = idx.Count / 3;
            BoundingSphere = bounds;
            Length = length;
            Radius = radius;
            (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
        }

        /// <summary>
        /// How far the log of <paramref name="length"/> built with <paramref name="seed"/> bows sideways at its middle (local
        /// +Z): the first roll of its seed, so the aurora's drift along a log (<c>AuroraCollars</c>) follows the log drawn.
        /// </summary>
        public static float Bend(float length, int seed) => RollBend(new Random(seed), length);

        private static float RollBend(Random rng, float length) =>
            length * (0.04f + 0.06f * (float)rng.NextDouble()) * (rng.Next(2) == 0 ? 1f : -1f);

        /// <summary>
        /// The log as lists, with no device (#938): what the constructor uploads, and what a test reads to hold the log shut.
        /// The log is one sweep through its bend, so its two halves have no gap between them on the bend's outer side (two tubes
        /// laid end to end at an angle opened a slit there, as the meadow's knees did), and each stub is one capped sweep.
        /// </summary>
        internal static (List<VertexPositionNormalTexture> Vertices, List<short> Indices, BoundingSphere Bounds) Build(float length, float radius, int seed)
        {
            Random rng = new(seed);
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            const int SEG = 8;

            (Vector3 root, Vector3 middle, Vector3 tip, float thinEnd) = Spine(rng, length, radius);

            //Both ends sawn: discs fanned from the rings' own vertices, so no sliver opens between a disc and its ring
            TubeGeometry.AddSweep(v, idx, SEG, new[] { root, middle, tip }, new[] { radius, radius * 0.82f, thinEnd }, capStart: true, capEnd: true);

            //Broken stubs: two to four, off the upper half of the log, each rising and leaning its own way and
            //snapped short - a fallen tree keeps the stumps of its boughs and little else.
            int stubs = 2 + rng.Next(3);
            float reach = 0f;
            for (int s = 0; s < stubs; s++)
            {
                float t = 0.15f + 0.7f * (float)rng.NextDouble();
                Vector3 from = t < 0.5f ? Vector3.Lerp(root, middle, t * 2f) : Vector3.Lerp(middle, tip, (t - 0.5f) * 2f);
                float yaw = (float)(rng.NextDouble() - 0.5) * 2.4f;              //mostly upward, leaning either side
                float pitch = 0.5f + 0.9f * (float)rng.NextDouble();             //radians above the log's axis plane
                float len = radius * (1.4f + 2.2f * (float)rng.NextDouble());
                Vector3 dir = new(MathF.Sin(yaw) * 0.4f, MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
                dir.Normalize();
                Vector3 to = from + dir * len;
                float r0 = radius * (0.3f + 0.15f * (float)rng.NextDouble());
                TubeGeometry.AddSweep(v, idx, 6, new[] { from, to }, new[] { r0, r0 * 0.3f }, capEnd: true);
                reach = MathF.Max(reach, len);
            }

            return (v, idx, new BoundingSphere(new Vector3(0f, radius, 0f), length * 0.5f + radius + reach));
        }

        //The log's spine, off the first rolls of its seed: the root end, the bent middle and the tip, and the tip's radius.
        //The log sits a quarter of its own thickness into the ground (the planter puts its origin on the terrain), so it reads
        //as lying in the grass rather than balanced on it.
        private static (Vector3 Root, Vector3 Middle, Vector3 Tip, float ThinEnd) Spine(Random rng, float length, float radius)
        {
            float bend = RollBend(rng, length);
            float thinEnd = radius * (0.5f + 0.15f * (float)rng.NextDouble());
            return (new Vector3(-length * 0.5f, radius * 0.75f, 0f), new Vector3(0f, radius * 0.7f, bend),
                new Vector3(length * 0.5f, thinEnd * 0.8f, 0f), thinEnd);
        }

        /// <summary>
        /// The log as slabs at scale 1 in its own space (#1157): one at every <see cref="BODY_STEP"/> of its root's radius along
        /// its spine, through the bend, as thick as the log is there and from its underside to its top. Its broken stubs are left
        /// out: they rise off its top, thinner than half of it. The offsets are the log's own, along +X, turned with it by the
        /// world it is planted at.
        /// </summary>
        internal static Slab[] Body(float length, float radius, int seed)
        {
            (Vector3 root, Vector3 middle, Vector3 tip, float thinEnd) = Spine(new Random(seed), length, radius);
            int count = Math.Max(2, (int)MathF.Ceiling(length / (radius * BODY_STEP)) + 1);
            var body = new Slab[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector3 at = t < 0.5f ? Vector3.Lerp(root, middle, t * 2f) : Vector3.Lerp(middle, tip, t * 2f - 1f);
                float r = t < 0.5f ? MathHelper.Lerp(radius, radius * 0.82f, t * 2f) : MathHelper.Lerp(radius * 0.82f, thinEnd, t * 2f - 1f);
                body[i] = new Slab(at.Y - r, at.Y + r, r, at.X, at.Z);
            }
            return body;
        }

        /// <summary>How far apart <see cref="Body"/>'s slabs stand along the log, in its root's radii: each one's circle overlaps
        /// the next well inside the log's own width, so no gap between two of them leaves room for a thing to stand in the log.</summary>
        internal const float BODY_STEP = 1f;

        public void Dispose()
        {
            VertexBuffer?.Dispose(); VertexBuffer = null;
            IndexBuffer?.Dispose(); IndexBuffer = null;
        }
    }
}
