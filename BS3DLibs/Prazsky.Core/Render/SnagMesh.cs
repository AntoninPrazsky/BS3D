using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A standing dead spruce — a snag (#462): a tall trunk that tapers to a snapped top, slightly bent, with
    /// the stubs of its dead branches still on it, short and hanging, thinning towards the top where the
    /// crown broke off. Every reference rendered for the aurora's wood put a few of these among the living
    /// spruces, and they are what makes a stand read as a real boreal wood rather than a planted one: bare,
    /// pale and crooked against dark, full and straight. Built from <see cref="TubeGeometry"/>'s sticks like
    /// <see cref="DeadwoodMesh"/>, its lying counterpart. The base sits at Y = 0, so a scatter plants it as it
    /// plants a tree.
    /// </summary>
    public sealed class SnagMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="height">Ground to the snapped top.</param>
        /// <param name="radius">At the base; the trunk tapers to about a third of it where it broke.</param>
        /// <param name="seed">Rolls the bend, the break and the branch stubs.</param>
        public SnagMesh(GraphicsDevice device, float height, float radius, int seed)
        {
            Random rng = new(seed);
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            const int SEG = 7;
            const int STUB_SEG = 4;

            //Two runs with a kink between them: a snag is never quite straight, and the kink is where the
            //trunk bends back under the weight it no longer carries. Rolled per mesh, so no two variants bow
            //the same way.
            float bendAngle = (float)rng.NextDouble() * MathHelper.TwoPi;
            float bend = height * (0.015f + 0.03f * (float)rng.NextDouble());
            Vector3 bendDir = new(MathF.Cos(bendAngle), 0f, MathF.Sin(bendAngle));
            float kinkAt = 0.45f + 0.2f * (float)rng.NextDouble();
            float topRadius = radius * (0.28f + 0.12f * (float)rng.NextDouble());
            float kinkRadius = MathHelper.Lerp(radius, topRadius, kinkAt);

            Vector3 foot = new(0f, -0.2f * radius, 0f);   //a little below ground, so a lump in the floor cannot lift it clear
            Vector3 kink = new Vector3(0f, height * kinkAt, 0f) + bendDir * bend;
            Vector3 top = new Vector3(0f, height, 0f) - bendDir * bend * 0.4f;

            //The root flare: a short fat cone at the foot, so the trunk grips the snow rather than stands in it
            //like a pole.
            TubeGeometry.AddTube(v, idx, SEG, foot, radius * 1.45f, foot + new Vector3(0f, radius * 1.6f, 0f), radius);
            TubeGeometry.AddTube(v, idx, SEG, foot + new Vector3(0f, radius * 1.6f, 0f), radius, kink, kinkRadius);
            TubeGeometry.AddTube(v, idx, SEG, kink, kinkRadius, top, topRadius);

            //The break: the top is snapped, not sawn, so it ends in a short splinter leaning off the axis rather
            //than a flat cap facing up.
            Vector3 splinterDir = Vector3.Normalize(top - kink + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.6f);
            Vector3 splinterTip = top + splinterDir * radius * (1.5f + 2f * (float)rng.NextDouble());
            TubeGeometry.AddTube(v, idx, 4, top, topRadius, splinterTip, topRadius * 0.15f);
            TubeGeometry.AddCap(v, idx, 4, splinterTip, topRadius * 0.15f, splinterDir);

            //The dead branches: stubs in rough whorls up the upper three quarters, pointing out and a little
            //down the way a spruce's branches hang, longest low and shortest near the break. A few are longer
            //than the rest — a snag keeps one or two whole boughs and loses the others.
            int stubs = 10 + rng.Next(8);
            float reach = 0f;
            for (int s = 0; s < stubs; s++)
            {
                float t = 0.25f + 0.72f * (float)rng.NextDouble();
                Vector3 from = t < kinkAt
                    ? Vector3.Lerp(foot, kink, t / kinkAt)
                    : Vector3.Lerp(kink, top, (t - kinkAt) / (1f - kinkAt));

                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float droop = -(0.15f + 0.45f * (float)rng.NextDouble());   //radians below horizontal
                Vector3 dir = Vector3.Normalize(new Vector3(MathF.Cos(yaw) * MathF.Cos(droop), MathF.Sin(droop), MathF.Sin(yaw) * MathF.Cos(droop)));

                bool bough = rng.NextDouble() < 0.18;
                float length = height * (1f - t) * (bough ? 0.55f : 0.25f) * (0.6f + 0.8f * (float)rng.NextDouble())
                    + radius * 1.2f;
                float r0 = MathHelper.Lerp(radius, topRadius, t) * (bough ? 0.34f : 0.24f);

                Vector3 to = from + dir * length;
                TubeGeometry.AddTube(v, idx, STUB_SEG, from, r0, to, r0 * 0.25f);
                TubeGeometry.AddCap(v, idx, STUB_SEG, to, r0 * 0.25f, dir);
                reach = MathF.Max(reach, length);
            }

            PrimitiveCount = idx.Count / 3;
            BoundingSphere = new BoundingSphere(new Vector3(0f, height * 0.5f, 0f), height * 0.5f + bend + reach + radius * 3f);
            (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose(); VertexBuffer = null;
            IndexBuffer?.Dispose(); IndexBuffer = null;
        }
    }
}
