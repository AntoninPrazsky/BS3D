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

        /// <summary>
        /// The leaf cap as leaf sprays on the twig ends (#610) — drawn instead of <see cref="Foliage"/> at scene
        /// detail, where the tufts are the Low tier's. The references' cap is sparse fine foliage on the thin
        /// twigs, never the round clumps the tufts are.
        /// </summary>
        public IProceduralMesh Leaves { get; }

        /// <summary>
        /// How far the trunk reaches from the axis at its pivot, at scale 1 (#658): what a scatter sinks the tree by, so
        /// the lowest ground under that circle is where the trunk stands and no side of it hangs in the air. The foot's
        /// ring at the ground plus the irregularity it is wobbled by there; since #670 the foot flares on under the
        /// ground past it, which stays buried on any slope the plain has.
        /// </summary>
        public float BaseRadius { get; }

        /// <summary>
        /// What the tree is made of, as slabs at scale 1 (#653): the bottle trunk in two (the swollen foot, then the taper to
        /// the neck) and the crown of bare limbs, taken from the tufts' sphere.
        /// </summary>
        public Slab[] Volume { get; }

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="height">The tree's full height, to the top of the crown.</param>
        /// <param name="seed">Rolls the limbs and the tufts.</param>
        public BaobabMesh(GraphicsDevice device, float height, int seed)
            : this(device, Build(height, seed), height, seed)
        {
        }

        private BaobabMesh(GraphicsDevice device, BaobabGeometry geometry, float height, int seed)
        {
            Wood = new UploadedMesh(device, geometry.WoodVertices, geometry.WoodIndices, geometry.WoodBounds);
            Foliage = new TuftsMesh(device, height, geometry.TwigTips, seed);
            BaseRadius = geometry.BaseRadius;

            //The trunk as the profile has it: r at the foot, 0.78r a third of the way up and 0.55r by the neck (BuildWood)
            float r = geometry.TrunkRadius;
            BoundingSphere crown = Foliage.BoundingSphere;
            Volume = new Slab[]
            {
                new(0f, height * 0.14f, r * 1.05f),
                new(height * 0.14f, height * 0.6f, r * 0.75f),
                new(crown.Center.Y - crown.Radius * 0.5f, crown.Center.Y + crown.Radius * 0.5f, crown.Radius * 0.85f, crown.Center.X, crown.Center.Z),
            };

            Leaves = new UploadedMesh(device, geometry.LeafVertices, geometry.LeafIndices, Foliage.BoundingSphere);
        }

        /// <summary>
        /// The tree as lists, with no device (#782): the wood, its skeleton, the twig ends and the leaf sprays — what the
        /// constructor uploads, and what a test reads to hold every leaf to the wood it grows from.
        /// </summary>
        internal static BaobabGeometry Build(float height, int seed)
        {
            BaobabGeometry geometry = new();
            Random rng = new(seed);
            BuildWood(geometry, height, rng);

            //Int indices: a crown of twenty-six leafy twig ends or more passes 65 536 vertices, and under short indices
            //the cards past that wrapped onto the first ones — a sixth of them never drawn, as many drawn twice (#670)
            //Each leafy twig end a spray of twiglets with the leaf hung on them (#782): the sprays used to be a disc round the
            //end with nothing in it, and on the savanna's baobabs nine in ten stood clear of any wood
            Random leafRng = new(seed * 41 + 5);
            Vector3[] support = new Vector3[1];
            foreach (Vector3 tip in geometry.TwigTips)
            {
                if (leafRng.NextDouble() > LEAFY_TWIGS) continue;
                float radius = height * (0.045f + 0.03f * (float)leafRng.NextDouble());
                support[0] = tip;
                LeafSprays.HangOnTwigs(geometry.WoodVertices, geometry.WoodIndices, geometry.Skeleton, geometry.LeafVertices,
                    geometry.LeafIndices, support, tip + Vector3.Up * (radius * 0.15f), radius, radius * 0.3f,
                    geometry.TrunkRadius * TWIGLET_RADIUS, SPRAYS_PER_TWIGLET, leafRng);
            }

            return geometry;
        }

        //The twiglets a leafy end carries its sprays on (#782): how thick one leaves the twig end against the trunk (the end
        //itself is 0.01 of it) and how many sprays each holds - many, since a tuft is small and its twiglets fill it
        private const float TWIGLET_RADIUS = 0.008f;
        private const int SPRAYS_PER_TWIGLET = 24;

        //The share of the twig ends that carry leaves: sparse, as the references' cap is
        private const double LEAFY_TWIGS = 0.7;

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Foliage as IDisposable)?.Dispose();
            (Leaves as IDisposable)?.Dispose();
        }

        //The trunk's facets round the axis (14 until #610; the flutes want several a lobe) and how deep a flute's
        //groove cuts into the radius
        private const int TRUNK_SEGMENTS = 48;
        private const float TRUNK_FLUTE_DEPTH = 0.2f;

        //The bottle's bottom ring, as a multiple of the trunk radius, and the wobble it is irregular by, likewise
        //(#658: BaseRadius is their sum, so the shape and what a scatter sinks the tree by cannot part). The ring was
        //1.15 until #670, a vase's foot stood on the plain; the references' baobab spreads into the ground in folds,
        //so the foot flares to this at the ground and on below it, and the flutes deepen there by FOOT_FOLD (which, being
        //the ring's wobble weight, scales its irregularity by as much) — the trunk's own lobes carried out into
        //buttresses, where tubes stuck on read as boards
        private const float BOTTOM_RING = 1.45f;
        private const float IRREGULARITY = 0.06f;
        private const float FOOT_FOLD = 2.3f;

        /// <summary>
        /// The bottle trunk and the bare limbs, one material: into <paramref name="geometry"/>'s wood lists, every limb and
        /// twig also a segment of its skeleton (#782), and the twig ends into <see cref="BaobabGeometry.TwigTips"/>.
        /// </summary>
        private static void BuildWood(BaobabGeometry geometry, float height, Random rng)
        {
                //The trunk: a bottle traced top → outside → underside. Widest low down, a slow taper, a
                //neck at the top where the limbs leave it. The wobble is slight - a baobab is smooth.
                float top = height * 0.6f;
                float r = height * 0.19f * (0.9f + 0.2f * (float)rng.NextDouble());
                geometry.BaseRadius = r * (BOTTOM_RING + IRREGULARITY * FOOT_FOLD);
                geometry.TrunkRadius = r;
                var profile = new (float radius, float y, float wobble)[]
                {
                    (0f,        top,            0f),
                    (r * 0.45f, top,            0.3f),
                    (r * 0.55f, height * 0.5f,  0.6f),
                    (r * 0.78f, height * 0.32f, 1f),
                    (r * 0.95f, height * 0.14f, 1f),
                    (r * 1.01f, height * 0.05f, 1.2f),
                    (r * 1.13f, height * 0.016f, FOOT_FOLD * 0.8f),
                    (r * BOTTOM_RING, 0f,       FOOT_FOLD),
                    (r * (BOTTOM_RING + 0.15f), -r * 0.2f, FOOT_FOLD),
                    (0f,        -r * 0.2f,      0f)
                };
                List<VertexPositionNormalTexture> v = geometry.WoodVertices;
                List<short> idx = geometry.WoodIndices;

                void Tube(int seg, Vector3 from, float fromRadius, Vector3 to, float toRadius)
                {
                    TubeGeometry.AddTube(v, idx, seg, from, fromRadius, to, toRadius);
                    geometry.Skeleton.Add((from, to));
                }

                //The trunk in the skeleton too: its axis from the ground to the neck
                geometry.Skeleton.Add((Vector3.Zero, new Vector3(0f, top, 0f)));
                //Folded into flutes since #610, the references' trunk: broad lobes with deep narrow grooves
                //between them running up the bottle, fading out at the neck where the wobble does
                TubeGeometry.AddRevolved(v, idx, TRUNK_SEGMENTS, profile, irregularityAmplitude: r * IRREGULARITY,
                    irregularityPhase: (float)rng.NextDouble() * 6f,
                    flutes: 6 + rng.Next(3), fluteDepth: TRUNK_FLUTE_DEPTH);

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
                    Tube(7, neck + dir * (r * 0.2f), r * 0.28f, mid, r * 0.16f);
                    Tube(6, mid, r * 0.16f, tip, r * 0.09f);

                    int twigs = 3 + rng.Next(2);
                    for (int k = 0; k < twigs; k++)
                    {
                        float ta = a + (float)(rng.NextDouble() - 0.5) * 2.0f;
                        float tl = height * (0.08f + 0.06f * (float)rng.NextDouble());
                        Vector3 tdir = Vector3.Normalize(new Vector3(MathF.Cos(ta) * 0.9f, 0.3f + 0.6f * (float)rng.NextDouble(), MathF.Sin(ta) * 0.9f));
                        Vector3 from = Vector3.Lerp(mid, tip, 0.5f + 0.5f * (float)rng.NextDouble());
                        Vector3 to = from + tdir * tl;
                        Tube(5, from, r * 0.07f, to, r * 0.03f);
                        for (int t = 0; t < 2; t++)
                        {
                            float wa = ta + (t == 0 ? 0.7f : -0.7f) + (float)(rng.NextDouble() - 0.5) * 0.5f;
                            float wl = height * (0.05f + 0.04f * (float)rng.NextDouble());
                            Vector3 wdir = Vector3.Normalize(new Vector3(MathF.Cos(wa) * 0.9f, 0.2f + 0.7f * (float)rng.NextDouble(), MathF.Sin(wa) * 0.9f));
                            Vector3 end = to + wdir * wl;
                            Tube(4, to, r * 0.03f, end, r * 0.01f);
                            geometry.TwigTips.Add(end);
                            reach = MathF.Max(reach, new Vector2(end.X, end.Z).Length());
                        }
                    }
                }

                geometry.WoodBounds = new BoundingSphere(new Vector3(0f, height * 0.5f, 0f), reach + height * 0.5f);
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

    /// <summary>A baobab as lists, before any device (#782): <see cref="BaobabMesh.Build"/>'s answer.</summary>
    internal sealed class BaobabGeometry
    {
        public float BaseRadius;
        public float TrunkRadius;

        public readonly List<VertexPositionNormalTexture> WoodVertices = new();
        public readonly List<short> WoodIndices = new();
        public BoundingSphere WoodBounds;

        public readonly List<Vector3> TwigTips = new();

        public readonly List<VertexPositionNormalTexture> LeafVertices = new();
        public readonly List<int> LeafIndices = new();

        public readonly List<(Vector3 From, Vector3 To)> Skeleton = new();
    }
}
