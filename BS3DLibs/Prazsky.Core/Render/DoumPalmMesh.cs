using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A doum palm for the savanna (#451, the owner's ask off the plants reference sheet): the one palm
    /// whose trunk <b>forks</b> — once, and often again — each fork ending in a head of stiff fan-shaped
    /// leaves on stalks. That forking is the whole silhouette, which is why it is not the tropical beach's
    /// <see cref="PalmMesh"/> (a single bowed trunk under drooping pinnate fronds) planted somewhere else.
    /// Two meshes, two materials: the wood (trunks, stalks) and the fans.
    /// </summary>
    public sealed class DoumPalmMesh : IDisposable
    {
        public IProceduralMesh Wood { get; }
        public IProceduralMesh Fronds { get; }

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="height">The palm's height to the top of its heads.</param>
        /// <param name="seed">Rolls the forks, the heads and every leaf.</param>
        public DoumPalmMesh(GraphicsDevice device, float height, int seed)
        {
            Random rng = new(seed);
            var wood = new List<VertexPositionNormalTexture>();
            var woodIdx = new List<short>();
            var leaf = new List<VertexPositionNormalTexture>();
            var leafIdx = new List<short>();

            float trunkR = height * 0.032f * (0.9f + 0.2f * (float)rng.NextDouble());
            const int SEG = 7;

            //The trunk to the first fork, leaning a little the way the tree will spread.
            float forkY = height * (0.38f + 0.12f * (float)rng.NextDouble());
            float leanA = (float)rng.NextDouble() * MathHelper.TwoPi;
            Vector3 fork = new(MathF.Cos(leanA) * height * 0.03f, forkY, MathF.Sin(leanA) * height * 0.03f);
            TubeGeometry.AddTube(wood, woodIdx, SEG, new Vector3(0f, -height * 0.02f, 0f), trunkR * 1.25f, fork, trunkR);

            //Two arms from the fork, opposite each other and leaning apart; one of them may fork again
            //half-way up. Every arm ends in a head.
            var heads = new List<(Vector3 at, Vector3 lean)>();
            float armA = (float)rng.NextDouble() * MathHelper.TwoPi;
            float reach = 0f;
            for (int arm = 0; arm < 2; arm++)
            {
                float a = armA + arm * MathF.PI + (float)(rng.NextDouble() - 0.5) * 0.5f;
                Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
                float rise = height - forkY;
                float spread = height * (0.12f + 0.1f * (float)rng.NextDouble());
                bool again = arm == 0 && rng.NextDouble() < 0.6;
                Vector3 top = fork + dir * spread + Vector3.Up * (rise * (again ? 0.55f : 1f));
                Vector3 mid = fork + dir * (spread * 0.6f) + Vector3.Up * (rise * (again ? 0.3f : 0.55f));
                TubeGeometry.AddTube(wood, woodIdx, SEG, fork, trunkR, mid, trunkR * 0.92f);
                TubeGeometry.AddTube(wood, woodIdx, SEG, mid, trunkR * 0.92f, top, trunkR * 0.8f);
                if (again)
                {
                    for (int sub = 0; sub < 2; sub++)
                    {
                        float sa = a + (sub == 0 ? 0.9f : -0.9f) + (float)(rng.NextDouble() - 0.5) * 0.4f;
                        Vector3 sdir = new(MathF.Cos(sa), 0f, MathF.Sin(sa));
                        Vector3 stop = top + sdir * (height * 0.1f) + Vector3.Up * (rise * (0.35f + 0.1f * (float)rng.NextDouble()));
                        TubeGeometry.AddTube(wood, woodIdx, SEG, top, trunkR * 0.8f, stop, trunkR * 0.65f);
                        heads.Add((stop, sdir));
                        reach = MathF.Max(reach, new Vector2(stop.X, stop.Z).Length());
                    }
                }
                else
                {
                    heads.Add((top, dir));
                    reach = MathF.Max(reach, new Vector2(top.X, top.Z).Length());
                }
            }

            //A head: seven to ten stalks radiating up and out, each carrying a fan of stiff blades that
            //spreads in the plane of the stalk — a doum's leaf is a fan, held up rather than hanging.
            float stalkLen = height * 0.13f;
            float bladeLen = height * 0.17f;
            foreach ((Vector3 at, Vector3 lean) in heads)
            {
                int leaves = 7 + rng.Next(4);
                float baseYaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                for (int l = 0; l < leaves; l++)
                {
                    float yaw = baseYaw + MathHelper.TwoPi * l / leaves + (float)(rng.NextDouble() - 0.5) * 0.4f;
                    float pitch = 0.15f + 0.9f * (float)rng.NextDouble(); //radians above horizontal
                    Vector3 d = Vector3.Normalize(new Vector3(MathF.Cos(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Sin(yaw) * MathF.Cos(pitch)));
                    Vector3 stalkEnd = at + d * (stalkLen * (0.8f + 0.4f * (float)rng.NextDouble()));
                    TubeGeometry.AddTube(wood, woodIdx, 4, at, trunkR * 0.18f, stalkEnd, trunkR * 0.08f);

                    //The fan: blades from the stalk's end, spread ±65° about the stalk's own direction in a
                    //plane rolled about it, each blade drooping a little the further from the middle it is.
                    Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.Up, d));
                    Vector3 lift = Vector3.Cross(d, side);
                    float roll = (float)(rng.NextDouble() - 0.5) * 1.2f;
                    Vector3 spreadAxis = side * MathF.Cos(roll) + lift * MathF.Sin(roll);
                    Vector3 fanNormal = Vector3.Normalize(Vector3.Cross(d, spreadAxis));
                    int blades = 7;
                    float len = bladeLen * (0.85f + 0.3f * (float)rng.NextDouble());
                    for (int b = 0; b < blades; b++)
                    {
                        float ang = MathHelper.ToRadians(-65f + 130f * b / (blades - 1));
                        Vector3 bd = Vector3.Normalize(d * MathF.Cos(ang) + spreadAxis * MathF.Sin(ang));
                        float droop = 0.12f * (1f - MathF.Cos(ang));
                        Vector3 tip = stalkEnd + bd * len - Vector3.Up * (len * droop);
                        Vector3 across = Vector3.Normalize(Vector3.Cross(bd, fanNormal)) * (len * 0.055f);
                        Vector3 n = Vector3.Normalize(Vector3.Cross(across, tip - stalkEnd));
                        TubeGeometry.AddRibbon(leaf, leafIdx, stalkEnd - across * 0.6f, stalkEnd + across * 0.6f, tip + across * 0.15f, tip - across * 0.15f, n);
                        TubeGeometry.AddRibbon(leaf, leafIdx, stalkEnd - across * 0.6f, stalkEnd + across * 0.6f, tip + across * 0.15f, tip - across * 0.15f, -n);
                    }
                    reach = MathF.Max(reach, new Vector2(stalkEnd.X, stalkEnd.Z).Length() + len);
                }
            }

            Wood = new UploadedMesh(device, wood, woodIdx, new BoundingSphere(new Vector3(0f, height * 0.5f, 0f), reach + height * 0.55f));
            Fronds = new UploadedMesh(device, leaf, leafIdx, new BoundingSphere(new Vector3(0f, height * 0.85f, 0f), reach + height * 0.3f));
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Fronds as IDisposable)?.Dispose();
        }
    }
}
