using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A baobab for the savanna (#451, the owner's ask off the plants reference sheet): a huge smooth
    /// bottle of a trunk — widest at the foot, narrowing to a neck — under a crown of short thick bare
    /// limbs that fork once into stubby twigs, and a few sparse tufts of leaf at their ends. The reference
    /// sheet's baobab is bare and the trunk is the whole tree; a baobab is a landmark the way a kopje is,
    /// and it stands alone.
    /// <para>
    /// The trunk is a <see cref="LatheMesh"/> with a small irregularity (a bottle, not a boulder); the limbs
    /// are <see cref="TubeGeometry"/> sticks; the leaf tufts a few small <see cref="FoliageStyle.Scrub"/>
    /// masses in one mesh, so the tree is two draws — wood and foliage — like an acacia.
    /// </para>
    /// </summary>
    public sealed class BaobabMesh : IDisposable
    {
        public IProceduralMesh Wood { get; }
        public IProceduralMesh Foliage { get; }

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="height">The tree's full height, to the top of the crown.</param>
        /// <param name="seed">Rolls the limbs and the tufts.</param>
        public BaobabMesh(GraphicsDevice device, float height, int seed)
        {
            Random rng = new(seed);
            Wood = new WoodMesh(device, height, rng);
            Foliage = new TuftsMesh(device, height, ((WoodMesh)Wood).TwigTips, seed);
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Foliage as IDisposable)?.Dispose();
        }

        /// <summary>The bottle trunk and the bare limbs, one material.</summary>
        private sealed class WoodMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            /// <summary>Where the twigs end, for the leaf tufts to sit on.</summary>
            public List<Vector3> TwigTips { get; } = new();

            public WoodMesh(GraphicsDevice device, float height, Random rng)
            {
                //The trunk: a bottle traced top → outside → underside. Widest low down, a slow taper, a
                //neck at the top where the limbs leave it. The wobble is slight - a baobab is smooth.
                float top = height * 0.6f;
                float r = height * 0.19f * (0.9f + 0.2f * (float)rng.NextDouble());
                var profile = new (float radius, float y, float wobble)[]
                {
                    (0f,        top,            0f),
                    (r * 0.45f, top,            0.3f),
                    (r * 0.55f, height * 0.5f,  0.6f),
                    (r * 0.78f, height * 0.32f, 1f),
                    (r * 0.95f, height * 0.14f, 1f),
                    (r,         height * 0.03f, 1f),
                    (r * 1.15f, 0f,             0.8f),
                    (0f,        0f,             0f)
                };
                var v = new List<VertexPositionNormalTexture>(profile.Length * 15 + 600);
                var idx = new List<short>(profile.Length * 90 + 1800);
                TubeGeometry.AddRevolved(v, idx, 14, profile, irregularityAmplitude: r * 0.06f,
                    irregularityPhase: (float)rng.NextDouble() * 6f);

                //The limbs: five to seven from the neck, thick, leaning well out, each forking into three or
                //four twigs and each twig into two twiglets - the reference crown is a wide fist of bare
                //branching over the trunk, as wide as the trunk is tall. The first cut had short limbs and
                //two twigs each, and the tufts on their ends read as balloons on a bottle.
                int limbs = 5 + rng.Next(3);
                float baseAngle = (float)rng.NextDouble() * MathHelper.TwoPi;
                Vector3 neck = new(0f, top, 0f);
                float reach = r;
                for (int b = 0; b < limbs; b++)
                {
                    float a = baseAngle + MathHelper.TwoPi * b / limbs + (float)(rng.NextDouble() - 0.5) * 0.5f;
                    Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
                    float len = height * (0.24f + 0.1f * (float)rng.NextDouble());
                    Vector3 mid = neck + dir * (len * 0.45f) + Vector3.Up * (len * 0.6f);
                    Vector3 tip = mid + dir * (len * 0.5f) + Vector3.Up * (len * 0.3f);
                    TubeGeometry.AddTube(v, idx, 7, neck + dir * (r * 0.2f), r * 0.28f, mid, r * 0.16f);
                    TubeGeometry.AddTube(v, idx, 6, mid, r * 0.16f, tip, r * 0.09f);

                    int twigs = 3 + rng.Next(2);
                    for (int k = 0; k < twigs; k++)
                    {
                        float ta = a + (float)(rng.NextDouble() - 0.5) * 2.0f;
                        float tl = height * (0.08f + 0.06f * (float)rng.NextDouble());
                        Vector3 tdir = Vector3.Normalize(new Vector3(MathF.Cos(ta) * 0.9f, 0.3f + 0.6f * (float)rng.NextDouble(), MathF.Sin(ta) * 0.9f));
                        Vector3 from = Vector3.Lerp(mid, tip, 0.5f + 0.5f * (float)rng.NextDouble());
                        Vector3 to = from + tdir * tl;
                        TubeGeometry.AddTube(v, idx, 5, from, r * 0.07f, to, r * 0.03f);
                        for (int t = 0; t < 2; t++)
                        {
                            float wa = ta + (t == 0 ? 0.7f : -0.7f) + (float)(rng.NextDouble() - 0.5) * 0.5f;
                            float wl = height * (0.05f + 0.04f * (float)rng.NextDouble());
                            Vector3 wdir = Vector3.Normalize(new Vector3(MathF.Cos(wa) * 0.9f, 0.2f + 0.7f * (float)rng.NextDouble(), MathF.Sin(wa) * 0.9f));
                            Vector3 end = to + wdir * wl;
                            TubeGeometry.AddTube(v, idx, 4, to, r * 0.03f, end, r * 0.01f);
                            TwigTips.Add(end);
                            reach = MathF.Max(reach, new Vector2(end.X, end.Z).Length());
                        }
                    }
                }

                PrimitiveCount = idx.Count / 3;
                BoundingSphere = new BoundingSphere(new Vector3(0f, height * 0.5f, 0f), reach + height * 0.5f);
                (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
            }

            public void Dispose()
            {
                VertexBuffer?.Dispose(); VertexBuffer = null;
                IndexBuffer?.Dispose(); IndexBuffer = null;
            }
        }

        /// <summary>The sparse leaf: a small scrub-style mass on about half the twig ends, one mesh.</summary>
        private sealed class TuftsMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            public TuftsMesh(GraphicsDevice device, float height, List<Vector3> tips, int seed)
            {
                Random rng = new(seed * 7 + 3);
                var v = new List<VertexPositionNormalTexture>();
                var idx = new List<short>();
                float reach = 0f;
                int k = 0;
                foreach (Vector3 tip in tips)
                {
                    if (rng.NextDouble() > 0.35) continue;
                    float rad = height * (0.028f + 0.02f * (float)rng.NextDouble());
                    FoliageMesh.Generate(v, idx, rad, rad * 0.8f, tip + Vector3.Up * (rad * 0.4f), seed * 13 + k++, FoliageStyle.Scrub);
                    reach = MathF.Max(reach, tip.Length() + rad);
                }
                //A baobab with no leaf at all is a valid tree too, but an empty buffer is not a valid draw:
                //give it one tuft rather than a zero-length buffer.
                if (v.Count == 0 && tips.Count > 0)
                {
                    float rad = height * 0.035f;
                    FoliageMesh.Generate(v, idx, rad, rad * 0.8f, tips[0] + Vector3.Up * (rad * 0.4f), seed * 13, FoliageStyle.Scrub);
                    reach = tips[0].Length() + rad;
                }

                PrimitiveCount = idx.Count / 3;
                BoundingSphere = new BoundingSphere(new Vector3(0f, height * 0.8f, 0f), reach);
                (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
            }

            public void Dispose()
            {
                VertexBuffer?.Dispose(); VertexBuffer = null;
                IndexBuffer?.Dispose(); IndexBuffer = null;
            }
        }
    }
}
