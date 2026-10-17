using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A savanna bush as thorn scrub (#883): a few thin grey stems that rise out of one spot, fork into branches that fan up and
    /// out into a mound, and hang their leaf on twigs grown from those branches (<see cref="LeafSprays.HangOnTwigs"/>), so the wood
    /// shows through the leaf and the leaf sits on the wood. Two meshes, two materials (wood, leaf cards), drawn off one set of
    /// instances like an acacia's wood and <see cref="AcaciaMesh.Leaves"/>.
    /// <para>
    /// <b>Why it exists.</b> The savanna's bushes were two <see cref="FoliageMesh"/> in <see cref="FoliageStyle.Crown"/>: a 16 by 11
    /// sphere pushed out into lobes with the sphere's normals, nothing inside the silhouette, so from the plain's own ground they
    /// were the owner's "giant green crumpled melons". The references (a low, open thorn shrub: wiry grey branches, small
    /// silver-green leaf clusters on them, a tenth to a third of a tree's height) have structure inside the outline and are
    /// smaller than the lobed dome was.
    /// </para>
    /// <para>
    /// <b>High and Ultra only.</b> The dome stays the Low and Medium tiers' bush, and the Potato path's, off the same instances
    /// (<see cref="ScatterBucket.FineMeshes"/>, as <see cref="AcaciaMesh.FineWood"/>): the plain is planted once, by the dome's
    /// volume, and a bush that is smaller than it stands clear of everything the dome stood clear of.
    /// </para>
    /// <para>
    /// Built by a static <see cref="Build"/> that returns the lists and a skeleton with no device, as an acacia is, so
    /// <c>LeafAttachmentTests</c> holds every card's stem to its wood and its direction to its twig's.
    /// </para>
    /// </summary>
    public sealed class ThornBushMesh : IDisposable
    {
        /// <summary>The stems, branches and twigs: one material, bark.</summary>
        public IProceduralMesh Wood { get; }

        /// <summary>The leaf sprays on the twigs, <c>Acacia.fx</c>'s leaf cards (leaves 1), drawn <c>CullNone</c>.</summary>
        public IProceduralMesh Leaves { get; }

        //The stems: how many leave the root (4 to 6), how thick one is at its foot against the bush's radius, and how far
        //apart their feet stand (a share of the radius)
        private const int STEMS_MIN = 4, STEMS_EXTRA = 2;
        private const float STEM_RADIUS = 0.04f, STEM_FOOT = 0.06f;

        //Where the leaf cloud sits, as shares of the bush's height: its half-thickness (the unit the layers of
        //SHAPE are measured in) and its centre. They are the pair that puts the cloud's highest card at the bush's
        //height and its lowest rim a little above the ground: the top layer's centre stands 0.4 half-thicknesses over the
        //cloud's, its noise another 0.09, and the lowest layer's rim 0.6 + 0.09 + 0.9 x (its reach squared) under it
        private const float CLOUD_HALF = 0.49f, CLOUD_CENTRE = 0.76f;

        //How far a card reaches past the cloud's own radius and over its top, as multiples of them: a stem stands up to 1.12 of
        //the radius out (its noise) and the spray runs on from it by its length (SHAPE's 0.22 of the radius, rolled up to 1.25
        //times: 0.275 at the top of its roll), which would add to 1.4 if every card lay along the radius; they lean off it, so
        //the figure is under that sum, and the cards at the top climb a little. Measured over the bushes the scatter builds, with
        //a little over
        private const float CARD_REACH = 1.3f, CARD_RISE = 1.18f;

        //A twig's radius against the bush's, and how many sprays it carries: fine twigs (a spray's own stem is what the
        //eye reads, not the twig), and more sprays on each than a tree's eight since the bush is small and its twigs few
        private const float TWIG_RADIUS = 0.012f;
        private const int SPRAYS_PER_TWIG = 6;

        //How the sprays lie over the wood: a mound, not a plate. Dense on top, a thinner layer under it, a ragged fringe under
        //that, each layer reaching less far and drooping hard to the rim so the whole is a dome that comes down to the stems;
        //a spray is a smaller share of the radius than a tree's (a bush's leaves are not bigger than a tree's, it is the
        //bush that is smaller)
        private static readonly LeafSprays.LeafShape SHAPE = new(
            new (float Y, float Reach, float Cover)[]
            {
                (0.40f, 1.00f, 2.20f),
                (-0.10f, 0.92f, 1.50f),
                (-0.60f, 0.78f, 0.75f),
            }, sprayLength: 0.22f, droop: 0.9f);

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="reach">How far from its axis anything of the bush stands, at scale 1.</param>
        /// <param name="top">How high it stands, at scale 1.</param>
        /// <param name="seed">Rolls the stems, the branches and the twigs.</param>
        public ThornBushMesh(GraphicsDevice device, float reach, float top, int seed)
            : this(device, Build(reach, top, seed))
        {
        }

        private ThornBushMesh(GraphicsDevice device, ThornBushGeometry geometry)
        {
            Wood = new UploadedMesh(device, geometry.WoodVertices, geometry.WoodIndices, geometry.Bounds);
            Leaves = new UploadedMesh(device, geometry.LeafVertices, geometry.LeafIndices, geometry.Bounds);
        }

        /// <summary>
        /// The bush as lists, with no device: what the constructor uploads, and what a test reads to hold every leaf to the wood
        /// it grows from. <paramref name="reach"/> is how far from its axis anything of it stands and <paramref name="top"/> how
        /// high, at scale 1 (the cloud the twigs grow to is a little inside both, by what a card reaches past its stem: it is the
        /// outer figures a volume is kept by, so they are what the caller states).
        /// </summary>
        internal static ThornBushGeometry Build(float reach, float top, int seed)
        {
            var geometry = new ThornBushGeometry();
            Random rng = new(seed);

            float radius = reach / CARD_REACH, height = top / CARD_RISE;

            float halfHeight = height * CLOUD_HALF;
            float centreY = height * CLOUD_CENTRE;
            float stemRadius = radius * STEM_RADIUS;

            //The middle layer's surface over a spot at this distance from the axis: where a branch ends, so the twigs reach the
            //layer above it as well as the ones under
            float SurfaceAt(float d) => centreY + halfHeight * (SHAPE.Layers[1].Y - SHAPE.Droop * (d / radius) * (d / radius));

            void Sweep(Vector3[] points, float[] radii)
            {
                TubeGeometry.AddSweep(geometry.WoodVertices, geometry.WoodIndices, 5, points, radii);
                for (int k = 0; k + 1 < points.Length; k++) geometry.Skeleton.Add((points[k], points[k + 1]));
            }

            var supports = new List<Vector3>();
            int stems = STEMS_MIN + rng.Next(STEMS_EXTRA + 1);
            float baseAngle = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int s = 0; s < stems; s++)
            {
                float a = baseAngle + MathHelper.TwoPi * s / stems + (float)(rng.NextDouble() - 0.5) * 0.7f;
                Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));

                //The stem: out of the ground a hair apart from its neighbours, leaning out, to its fork
                Vector3 root = dir * (radius * STEM_FOOT) + Vector3.Down * (height * 0.05f);
                Vector3 fork = dir * (radius * (0.10f + 0.14f * (float)rng.NextDouble())) + Vector3.Up * (height * (0.22f + 0.12f * (float)rng.NextDouble()));
                Vector3 stemMid = Vector3.Lerp(root, fork, 0.5f) + dir * (radius * 0.02f);
                Sweep(new[] { root, stemMid, fork }, new[] { stemRadius * 1.3f, stemRadius, stemRadius * 0.8f });

                //Two or three branches from the fork, fanned about the stem's own bearing and each ending under the cloud's
                //middle layer; one in two forks again part-way, the wiry look of the references
                int branches = 2 + rng.Next(2);
                for (int b = 0; b < branches; b++)
                {
                    float ba = a + (b - (branches - 1) * 0.5f) * 0.75f + (float)(rng.NextDouble() - 0.5) * 0.4f;
                    float d = radius * (0.32f + 0.5f * (float)rng.NextDouble());
                    Vector3 end = new(MathF.Cos(ba) * d, SurfaceAt(d), MathF.Sin(ba) * d);
                    Vector3 mid = Vector3.Lerp(fork, end, 0.5f) + Vector3.Up * (Vector3.Distance(fork, end) * 0.08f);
                    Sweep(new[] { fork, mid, end }, new[] { stemRadius * 0.8f, stemRadius * 0.5f, stemRadius * 0.2f });
                    supports.Add(end);

                    if (rng.NextDouble() < 0.5)
                    {
                        float fa = ba + (rng.NextDouble() < 0.5 ? -1f : 1f) * (0.35f + 0.3f * (float)rng.NextDouble());
                        float fd = Math.Min(d * (0.8f + 0.35f * (float)rng.NextDouble()), radius * 0.9f);
                        Vector3 forkEnd = new(MathF.Cos(fa) * fd, SurfaceAt(fd), MathF.Sin(fa) * fd);
                        Vector3 forkMid = Vector3.Lerp(mid, forkEnd, 0.5f) + Vector3.Up * (Vector3.Distance(mid, forkEnd) * 0.08f);
                        Sweep(new[] { mid, forkMid, forkEnd }, new[] { stemRadius * 0.5f, stemRadius * 0.35f, stemRadius * 0.15f });
                        supports.Add(forkEnd);
                    }
                }
            }
            geometry.Supports.AddRange(supports);

            //The leaf: twigs grown from the branch ends to points over the cloud's layers, the sprays hung along them
            Random leafRng = new(seed * 37 + 11);
            LeafSprays.HangOnTwigs(geometry.WoodVertices, geometry.WoodIndices, geometry.Skeleton, geometry.LeafVertices,
                geometry.LeafIndices, supports, new Vector3(0f, centreY, 0f), radius, halfHeight, radius * TWIG_RADIUS,
                SPRAYS_PER_TWIG, leafRng, shape: SHAPE);

            //One sphere round the lot, from the vertices
            Vector3 centre = new(0f, top * 0.5f, 0f);
            float sphere = 0f;
            foreach (VertexPositionNormalTexture vertex in geometry.WoodVertices) sphere = MathF.Max(sphere, Vector3.Distance(vertex.Position, centre));
            foreach (VertexPositionNormalTexture vertex in geometry.LeafVertices) sphere = MathF.Max(sphere, Vector3.Distance(vertex.Position, centre));
            geometry.Bounds = new BoundingSphere(centre, sphere);
            return geometry;
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Leaves as IDisposable)?.Dispose();
        }
    }

    /// <summary>A thorn bush as lists, before any device (<see cref="ThornBushMesh.Build"/>'s answer).</summary>
    internal sealed class ThornBushGeometry
    {
        public readonly List<VertexPositionNormalTexture> WoodVertices = new();
        public readonly List<short> WoodIndices = new();

        public readonly List<VertexPositionNormalTexture> LeafVertices = new();
        public readonly List<int> LeafIndices = new();

        /// <summary>Every piece of wood as the segment it was swept along, the twigs' curves included: what a leaf is measured against.</summary>
        public readonly List<(Vector3 From, Vector3 To)> Skeleton = new();

        /// <summary>The branch ends the twigs grow from.</summary>
        public readonly List<Vector3> Supports = new();

        public BoundingSphere Bounds;
    }
}
