using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The silhouettes an acacia is planted in (#451). One shape stamped out in four proportions is what the
    /// savanna had, and the eye counts the repeat before it counts the trees; a plain reads as a place when
    /// the trees on it have lived different lives.
    /// </summary>
    public enum AcaciaKind
    {
        /// <summary>The umbrella tree: a forked trunk under a wide, thin, flat-topped plate of foliage — and
        /// about half of them carry a second, smaller tier above the first, the layered crown the reference
        /// acacias show with sky between the layers.</summary>
        Mature,

        /// <summary>A storm-broken tree: the one tier it has left sits off-centre, and a bare splintered spar
        /// stands up out of the far side where the rest of the crown used to be.</summary>
        Broken,

        /// <summary>A young tree: a slender trunk forking high under one small crown.</summary>
        Young,

        /// <summary>A dead tree: no foliage at all, the boughs branching on into twigs, bleached — drawn in
        /// the deadwood colour rather than the bark's.</summary>
        Dead
    }

    /// <summary>
    /// A savanna acacia as real 3D geometry: a trunk that forks into several boughs (<see cref="Wood"/>)
    /// holding one or two wide, thin, flat-topped tiers of foliage (<see cref="Canopy"/>, one mesh however
    /// many tiers — a tree is one instanced draw per material). Two meshes, two materials (bark, leaves),
    /// drawn off one set of instance matrices. The boughs are the point — a single tapering stick under a
    /// disc reads as a lollipop, where a fork spreading into the canopy reads as an acacia.
    /// <para>
    /// Since #451 the tree is built in one of the <see cref="AcaciaKind"/>s, and the canopy is a
    /// <see cref="FoliageStyle.Tier"/> plate rather than the domed crown of #202: the reference acacias are
    /// thin flat layers, wide against the tree's height, with the bough structure showing under them and a
    /// ragged rim — the dome was the shape of a bush held up on a stick.
    /// </para>
    /// </summary>
    public sealed class AcaciaMesh : IDisposable
    {
        public AcaciaKind Kind { get; }
        public IProceduralMesh Wood { get; }

        /// <summary>
        /// How far the trunk's flat underside reaches from the axis, at scale 1 (#658): the root flare's ring at the
        /// pivot. What a scatter sinks the tree by, so the lowest ground under that circle is where the trunk stands and
        /// no side of it hangs in the air on a slope.
        /// </summary>
        public float BaseRadius { get; }

        /// <summary>
        /// What the tree is made of, as slabs at scale 1 (#653): its trunk to the fork, and one slab a tier of crown,
        /// on the axis the tier sits on. A dead tree has no tier, so its bare crown of boughs and twigs is one slab
        /// from the fork up. A crown slab's radius is a little inside the leaf plate's — the rim is thin leaf, and a
        /// neighbouring branch reaching into it is what a real stand does (<see cref="PlantVolumes.TOLERANCE"/> does
        /// the rest).
        /// </summary>
        public Slab[] Volume { get; }

        //The trunk's radius at the root, as a multiple of the radius it holds to the fork
        private const float ROOT_FLARE = 1.5f;

        //The surface roots (#670), as multiples of the flare's radius: how many, how far out they reach on the ground and
        //how high up the trunk they leave it; and how steeply their tips dive, as a share of the reach past the flare —
        //in proportion, because the scatter sinks a tree by its flare's ring and a long root on a slope's downhill
        //side would otherwise end above the plain it was drawn to enter
        private const int ROOTS_MIN = 4;
        private const int ROOTS_SPREAD = 3;
        private const float ROOT_REACH = 2.3f;
        private const float ROOT_RISE = 1.1f;
        private const float ROOT_DIVE = 0.45f;

        //The trunk's mean radius over its length as a multiple of the radius at the fork (a flare from ROOT_FLARE down
        //to 0.9), and how much of a tier's leaf plate counts as solid
        private const float TRUNK_MEAN = 1.2f;
        private const float CROWN_SOLID = 0.92f;

        /// <summary>Every tier of foliage in one mesh; <c>null</c> for a <see cref="AcaciaKind.Dead"/> tree.</summary>
        public IProceduralMesh Canopy { get; }

        /// <summary>
        /// The same tiers as <see cref="Canopy"/>, as leaf sprays rather than a plate (#610) — drawn instead of it
        /// at scene detail, where <see cref="Canopy"/> is the Low tier's. <c>null</c> for a dead tree. See
        /// <see cref="LeafSprays"/>.
        /// </summary>
        public IProceduralMesh Leaves { get; }

        public AcaciaMesh(GraphicsDevice device, AcaciaKind kind, float trunkRadius, float treeHeight, float canopyRadius, int seed)
        {
            Kind = kind;
            BaseRadius = trunkRadius * ROOT_FLARE;
            Random rng = new(seed);

            //Where the tiers of foliage sit: each a flat plate at a height, a radius, and a sideways offset
            //from the trunk's axis, and the boughs are grown up to them.
            var tiers = new List<TierSpec>();
            float forkY;
            int boughs;
            bool twigs = false;
            var spars = new List<Vector2>(); //bare boughs (bearing, top height) that rise clear of every tier

            switch (kind)
            {
                case AcaciaKind.Young:
                    forkY = treeHeight * (0.48f + 0.10f * (float)rng.NextDouble());
                    boughs = 2 + rng.Next(2);
                    tiers.Add(new TierSpec(Vector2.Zero, treeHeight * 0.84f, canopyRadius * (0.5f + 0.12f * (float)rng.NextDouble()),
                        treeHeight * 0.11f));
                    break;

                case AcaciaKind.Dead:
                    forkY = treeHeight * (0.30f + 0.15f * (float)rng.NextDouble());
                    boughs = 4 + rng.Next(2);
                    twigs = true;
                    break;

                case AcaciaKind.Broken:
                {
                    forkY = treeHeight * (0.30f + 0.10f * (float)rng.NextDouble());
                    boughs = 3 + rng.Next(2);
                    float side = (float)rng.NextDouble() * MathHelper.TwoPi;
                    Vector2 offset = new Vector2(MathF.Cos(side), MathF.Sin(side)) * (canopyRadius * 0.38f);
                    tiers.Add(new TierSpec(offset, treeHeight * 0.76f, canopyRadius * 0.8f, treeHeight * 0.13f));
                    //The spar stands where the crown is not: opposite the surviving tier, and above it.
                    spars.Add(new Vector2(side + MathF.PI + (float)(rng.NextDouble() - 0.5) * 0.6f, treeHeight * (1.02f + 0.08f * (float)rng.NextDouble())));
                    break;
                }

                default: //Mature
                {
                    forkY = treeHeight * (0.30f + 0.10f * (float)rng.NextDouble());
                    boughs = 3 + rng.Next(3);
                    tiers.Add(new TierSpec(Vector2.Zero, treeHeight * 0.78f, canopyRadius, treeHeight * 0.14f));
                    if (rng.NextDouble() < 0.55)
                    {
                        float side = (float)rng.NextDouble() * MathHelper.TwoPi;
                        Vector2 offset = new Vector2(MathF.Cos(side), MathF.Sin(side)) * (canopyRadius * 0.3f);
                        tiers.Add(new TierSpec(offset, treeHeight * 0.94f, canopyRadius * (0.4f + 0.15f * (float)rng.NextDouble()),
                            treeHeight * 0.10f));
                    }
                    break;
                }
            }

            Wood = new WoodMesh(device, trunkRadius, treeHeight, canopyRadius, forkY, boughs, tiers, spars, twigs, rng);

            var volume = new List<Slab> { new(0f, forkY, trunkRadius * TRUNK_MEAN) };
            foreach (TierSpec tier in tiers)
                volume.Add(new Slab(tier.CentreY - tier.HalfHeight, tier.CentreY + tier.HalfHeight, tier.Radius * CROWN_SOLID, tier.Offset.X, tier.Offset.Y));
            if (tiers.Count == 0) volume.Add(new Slab(forkY, treeHeight, canopyRadius * 0.55f));
            Volume = volume.ToArray();

            if (tiers.Count > 0)
            {
                var v = new List<VertexPositionNormalTexture>();
                var idx = new List<short>();
                for (int t = 0; t < tiers.Count; t++)
                {
                    TierSpec tier = tiers[t];
                    FoliageMesh.Generate(v, idx, tier.Radius, tier.HalfHeight,
                        new Vector3(tier.Offset.X, tier.CentreY, tier.Offset.Y), seed * 31 + 7 + t * 13, FoliageStyle.Tier);
                }
                Canopy = new UploadedMesh(device, v, idx,
                    new BoundingSphere(new Vector3(0f, treeHeight * 0.85f, 0f), canopyRadius * 1.6f + treeHeight * 0.2f));

                var lv = new List<VertexPositionNormalTexture>();
                var lidx = new List<int>();
                Random leafRng = new(seed * 37 + 11);
                foreach (TierSpec tier in tiers)
                    LeafSprays.Generate(lv, lidx, new Vector3(tier.Offset.X, tier.CentreY, tier.Offset.Y), tier.Radius, tier.HalfHeight, leafRng);
                Leaves = new UploadedMesh(device, lv, lidx, Canopy.BoundingSphere);
            }
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Canopy as IDisposable)?.Dispose();
            (Leaves as IDisposable)?.Dispose();
        }

        /// <summary>One flat plate of foliage: where it sits off the trunk's axis, its height, radius and thickness.</summary>
        private readonly struct TierSpec
        {
            public readonly Vector2 Offset;
            public readonly float CentreY, Radius, HalfHeight;
            public TierSpec(Vector2 offset, float centreY, float radius, float halfHeight)
            { Offset = offset; CentreY = centreY; Radius = radius; HalfHeight = halfHeight; }
        }

        /// <summary>Trunk + boughs: a flared trunk to the fork, then several tapered boughs fanning up and out
        /// to the tiers' undersides, each with a slight upward bend so the crown sits on spread arms. A dead
        /// tree's boughs branch on into twigs instead, and a broken tree's spar rises bare past the crown.</summary>
        private sealed class WoodMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            public WoodMesh(GraphicsDevice device, float trunkRadius, float treeHeight, float canopyRadius,
                float forkY, int boughs, List<TierSpec> tiers, List<Vector2> spars, bool twigs, Random rng)
            {
                var v = new List<VertexPositionNormalTexture>();
                var idx = new List<short>();
                const int SEG = 7;

                //The trunk, flared at the root and holding most of its girth to the fork.
                TubeGeometry.AddTube(v, idx, SEG, new Vector3(0f, 0f, 0f), trunkRadius * ROOT_FLARE,
                    new Vector3(0f, forkY, 0f), trunkRadius * 0.9f);

                //The foot (#670): surface roots leaving the flare, running out along the ground and diving into it,
                //which is what every reference's trunk does where it meets the soil — a clean cylinder cut at the
                //ground read as a pole stood on the plain. Each in two runs, steep off the trunk and then shallower,
                //and off its own dice, so everything above the ground is the tree it was.
                Random rootDice = new(unchecked(BitConverter.SingleToInt32Bits(forkY) * 31 + boughs));
                float foot = trunkRadius * ROOT_FLARE;
                int roots = ROOTS_MIN + rootDice.Next(ROOTS_SPREAD);
                float rootBearing = (float)rootDice.NextDouble() * MathHelper.TwoPi;
                for (int r = 0; r < roots; r++)
                {
                    float ra = rootBearing + MathHelper.TwoPi * r / roots + (float)(rootDice.NextDouble() - 0.5) * 0.8f;
                    Vector3 rd = new(MathF.Cos(ra), 0f, MathF.Sin(ra));
                    float out_ = foot * ROOT_REACH * (0.7f + 0.6f * (float)rootDice.NextDouble());
                    Vector3 leave = rd * (foot * 0.3f) + Vector3.Up * (foot * ROOT_RISE * (0.8f + 0.4f * (float)rootDice.NextDouble()));
                    Vector3 knee = rd * (foot * 1.1f) + Vector3.Up * (foot * 0.12f);
                    Vector3 end = rd * out_ - Vector3.Up * ((out_ - foot) * ROOT_DIVE + foot * 0.2f);
                    TubeGeometry.AddTube(v, idx, 5, leave, foot * 0.42f, knee, foot * 0.24f);
                    TubeGeometry.AddTube(v, idx, 5, knee, foot * 0.24f, end, foot * 0.08f);
                }

                //The boughs: evenly spread with a jittered bearing, each rising in two bent segments to a point
                //out under a tier. They taper hard, so the wood thins into the leaves it carries. With no tier
                //(a dead tree) they rise to where a crown would have been and branch into twigs.
                float baseAngle = (float)rng.NextDouble() * MathHelper.TwoPi;
                Vector3 fork = new(0f, forkY, 0f);
                float reach = canopyRadius;
                for (int b = 0; b < boughs; b++)
                {
                    float a = baseAngle + MathHelper.TwoPi * b / boughs + (float)(rng.NextDouble() - 0.5) * 0.6f;
                    Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));

                    //The tier this bough carries: the one whose offset lies nearest its own bearing, so a
                    //crown that sits to one side is held up by the boughs under it and not by thin air.
                    float spread, tipY;
                    if (tiers.Count > 0)
                    {
                        TierSpec tier = tiers[0];
                        float best = float.NegativeInfinity;
                        for (int t = 0; t < tiers.Count; t++)
                        {
                            float toward = Vector2.Dot(tiers[t].Offset, new Vector2(dir.X, dir.Z)) + (t == 0 ? 0.01f : 0f);
                            if (toward > best) { best = toward; tier = tiers[t]; }
                        }
                        float along = Vector2.Dot(tier.Offset, new Vector2(dir.X, dir.Z));
                        spread = along + tier.Radius * (0.5f + 0.35f * (float)rng.NextDouble());
                        tipY = tier.CentreY - tier.HalfHeight * (0.2f + 0.4f * (float)rng.NextDouble());
                    }
                    else
                    {
                        spread = canopyRadius * (0.5f + 0.4f * (float)rng.NextDouble());
                        tipY = treeHeight * (0.82f + 0.2f * (float)rng.NextDouble());
                    }
                    reach = MathF.Max(reach, spread);

                    Vector3 mid = fork + dir * (spread * 0.5f) + Vector3.Up * ((tipY - forkY) * 0.55f);
                    Vector3 tip = fork + dir * spread + Vector3.Up * (tipY - forkY);
                    TubeGeometry.AddTube(v, idx, SEG, fork, trunkRadius * 0.6f, mid, trunkRadius * 0.42f);
                    TubeGeometry.AddTube(v, idx, SEG, mid, trunkRadius * 0.42f, tip, trunkRadius * 0.14f);

                    //The second forking (#451): from the bough's middle two thinner limbs splay to either side
                    //and reach the rim, the spokes an umbrella acacia shows under its crown — the references
                    //fork twice at least, and one bough per spoke read as a stick under a lid.
                    if (!twigs)
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float sa = a + side * (0.3f + 0.25f * (float)rng.NextDouble());
                            float sSpread = spread * (0.85f + 0.3f * (float)rng.NextDouble());
                            Vector3 sDir = new(MathF.Cos(sa), 0f, MathF.Sin(sa));
                            Vector3 sTip = fork + sDir * sSpread + Vector3.Up * (tipY - forkY + (float)(rng.NextDouble() - 0.3) * treeHeight * 0.04f);
                            TubeGeometry.AddTube(v, idx, 5, mid, trunkRadius * 0.3f, sTip, trunkRadius * 0.08f);
                            reach = MathF.Max(reach, sSpread);
                        }
                    }

                    if (twigs)
                    {
                        //Two twigs off the upper half of each bough, splayed off its bearing and rising, each
                        //tapering to nearly nothing - the bare crown of a dead tree is all twigs.
                        int count = 1 + rng.Next(2);
                        for (int k = 0; k < count; k++)
                        {
                            Vector3 from = Vector3.Lerp(mid, tip, 0.35f + 0.5f * (float)rng.NextDouble());
                            float ta = a + (float)(rng.NextDouble() - 0.5) * 1.6f;
                            float len = canopyRadius * (0.25f + 0.25f * (float)rng.NextDouble());
                            Vector3 to = from + new Vector3(MathF.Cos(ta), 0f, MathF.Sin(ta)) * (len * 0.7f) + Vector3.Up * (len * 0.7f);
                            TubeGeometry.AddTube(v, idx, 5, from, trunkRadius * 0.22f, to, trunkRadius * 0.05f);
                            reach = MathF.Max(reach, len + spread);
                        }
                    }
                }

                //A broken tree's spar: bare, rising past the crown, splintered to a point.
                for (int s = 0; s < spars.Count; s++)
                {
                    float a = spars[s].X;
                    Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
                    float spread = canopyRadius * 0.35f;
                    Vector3 mid = fork + dir * (spread * 0.6f) + Vector3.Up * ((spars[s].Y - forkY) * 0.5f);
                    Vector3 top = fork + dir * spread + Vector3.Up * (spars[s].Y - forkY);
                    TubeGeometry.AddTube(v, idx, SEG, fork, trunkRadius * 0.55f, mid, trunkRadius * 0.38f);
                    TubeGeometry.AddTube(v, idx, SEG, mid, trunkRadius * 0.38f, top, trunkRadius * 0.06f);
                }

                PrimitiveCount = idx.Count / 3;
                BoundingSphere = new BoundingSphere(new Vector3(0f, treeHeight * 0.55f, 0f), reach * 1.2f + treeHeight * 0.6f);

                (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
            }

            public void Dispose()
            {
                VertexBuffer?.Dispose(); VertexBuffer = null;
                IndexBuffer?.Dispose(); IndexBuffer = null;
            }
        }
    }

    /// <summary>
    /// Straight tapered tubes and their end caps, appended to a vertex/index list — the wood of the acacias,
    /// the deadwood logs and anything else built from sticks. Radial normals; wound clockwise seen from
    /// outside, MonoGame's front face. One copy since #451 (it was a private method of the acacia's wood).
    /// </summary>
    /// <summary>
    /// <b>An acacia's crown as leaf sprays</b> (#610): flat cards, each one spray of bipinnate leaves whose
    /// leaflets <c>Acacia.fx</c> cuts out of it (<c>LeafStrength</c>), laid in the tier's disc in three layers —
    /// the references (#610's, a crown seen from under it against the sun and a grove at eye level) show the
    /// umbrella as thin flat tiers of fine foliage the sky shows through, denser on top and ragged beneath, never
    /// the solid plate with a mottle the tier was. Two-sided, drawn <c>CullNone</c>; a card's normal is its
    /// upper face's, and the shader turns it for the side it is seen from.
    /// <para>
    /// <b>The texture coordinate carries the card:</b> X along the spray from its stem (0) to its tip (1), Y across
    /// it (0–1) plus twice the layer (0 on top, 1, 2 underneath), so the shader knows how deep in the crown a leaf
    /// hangs without a per-vertex attribute of its own — every savanna mesh is already
    /// <see cref="VertexPositionNormalTexture"/>.
    /// </para>
    /// </summary>
    internal static class LeafSprays
    {
        //The three layers: height in the tier's half-thickness, the disc's reach, and the share of the disc the
        //layer's cards cover (before the leaflets are cut out of them). Dense on top, a thinner layer under it,
        //a ragged fringe beneath that droops at the rim.
        private static readonly (float Y, float Reach, float Cover)[] LAYERS =
        {
            (0.45f, 1.00f, 2.10f),
            (-0.05f, 0.92f, 1.30f),
            (-0.55f, 0.80f, 0.70f),
        };

        //A spray's length as a share of the tier's radius, and its width against its length.
        private const float SPRAY_LENGTH = 0.20f, SPRAY_ASPECT = 0.45f;

        public static void Generate(List<VertexPositionNormalTexture> v, List<int> idx, Vector3 centre, float radius,
            float halfHeight, Random rng)
        {
            for (int layer = 0; layer < LAYERS.Length; layer++)
            {
                (float layerY, float reach, float cover) = LAYERS[layer];
                float length = radius * SPRAY_LENGTH;
                float width = length * SPRAY_ASPECT;
                float disc = MathF.PI * radius * reach * radius * reach;
                int count = (int)(cover * disc / (length * width));

                for (int i = 0; i < count; i++)
                {
                    //Uniform over the disc, and a ragged edge: the rim's sprays reach out or fall short
                    float r = radius * reach * MathF.Sqrt((float)rng.NextDouble()) * (0.88f + 0.24f * (float)rng.NextDouble());
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float rim = r / radius;

                    Vector3 at = centre + new Vector3(MathF.Cos(a) * r,
                        halfHeight * (layerY + 0.35f * ((float)rng.NextDouble() - 0.5f)) - halfHeight * 0.5f * rim * rim,
                        MathF.Sin(a) * r);

                    //Laid near flat, pointing roughly outward, drooping at the rim and tilting a little at random
                    float yaw = a + ((float)rng.NextDouble() - 0.5f) * 1.6f;
                    Vector3 along = new(MathF.Cos(yaw), -0.25f * rim + 0.3f * ((float)rng.NextDouble() - 0.5f), MathF.Sin(yaw));
                    along.Normalize();
                    Vector3 across = Vector3.Normalize(Vector3.Cross(Vector3.Up, along));
                    across = Vector3.Normalize(across + Vector3.Up * (0.8f * ((float)rng.NextDouble() - 0.5f)));
                    Vector3 normal = Vector3.Normalize(Vector3.Cross(along, across));
                    if (normal.Y < 0f) normal = -normal;

                    float scale = 0.75f + 0.5f * (float)rng.NextDouble();
                    Vector3 stem = at - along * (length * scale * 0.5f);
                    Vector3 tip = at + along * (length * scale * 0.5f);
                    Vector3 half = across * (width * scale * 0.5f);

                    float layerCode = 2f * layer;
                    int b = v.Count;
                    v.Add(new VertexPositionNormalTexture(stem - half, normal, new Vector2(0f, layerCode)));
                    v.Add(new VertexPositionNormalTexture(stem + half, normal, new Vector2(0f, layerCode + 1f)));
                    v.Add(new VertexPositionNormalTexture(tip + half, normal, new Vector2(1f, layerCode + 1f)));
                    v.Add(new VertexPositionNormalTexture(tip - half, normal, new Vector2(1f, layerCode)));
                    idx.Add(b); idx.Add(b + 1); idx.Add(b + 2);
                    idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
                }
            }
        }
    }

    internal static class TubeGeometry
    {
        /// <summary>A straight tapered cylinder a(ra) → b(rb), side faces only.</summary>
        public static void AddTube(List<VertexPositionNormalTexture> v, List<short> idx, int seg,
            Vector3 a, float ra, Vector3 b, float rb)
        {
            Vector3 axis = b - a;
            float len = axis.Length();
            if (len < 1e-4f) return;
            axis /= len;

            Vector3 ref0 = MathF.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 u = Vector3.Normalize(Vector3.Cross(ref0, axis));
            Vector3 w = Vector3.Cross(axis, u);

            short baseIdx = (short)v.Count;
            for (int s = 0; s <= seg; s++)
            {
                float ang = MathHelper.TwoPi * s / seg;
                Vector3 dir = u * MathF.Cos(ang) + w * MathF.Sin(ang);
                v.Add(new VertexPositionNormalTexture(a + dir * ra, dir, new Vector2(s / (float)seg, 0f)));
                v.Add(new VertexPositionNormalTexture(b + dir * rb, dir, new Vector2(s / (float)seg, 1f)));
            }
            for (int s = 0; s < seg; s++)
            {
                int i0 = baseIdx + s * 2;
                idx.Add((short)i0); idx.Add((short)(i0 + 1)); idx.Add((short)(i0 + 2));
                idx.Add((short)(i0 + 2)); idx.Add((short)(i0 + 1)); idx.Add((short)(i0 + 3));
            }
        }

        /// <summary>
        /// One continuous tube along a polyline, a ring at every point (#609's third round): each ring square to the
        /// mean of the two segments it joins and turned by parallel transport from the one before, so a bent limb is one
        /// skin. Straight tubes laid end to end at an angle meet with their end rings in two different planes — a step
        /// on one side and an overlap on the other — and on the meadow's thick old trunks that read as a sleeve pulled
        /// over the wood. Side faces only, wound as <see cref="AddTube"/> is.
        /// </summary>
        public static void AddSweep(List<VertexPositionNormalTexture> v, List<short> idx, int seg,
            IReadOnlyList<Vector3> points, IReadOnlyList<float> radii)
        {
            int count = points.Count;
            if (count < 2) return;

            Vector3 Tangent(int k)
            {
                Vector3 t = points[Math.Min(k + 1, count - 1)] - points[Math.Max(k - 1, 0)];
                return t.LengthSquared() > 1e-10f ? Vector3.Normalize(t) : Vector3.Up;
            }

            Vector3 axis = Tangent(0);
            Vector3 ref0 = MathF.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 u = Vector3.Normalize(Vector3.Cross(ref0, axis));

            short baseIdx = (short)v.Count;
            for (int k = 0; k < count; k++)
            {
                axis = Tangent(k);
                //Parallel transport: the last ring's reference direction, with what now lies along the axis taken out
                u = Vector3.Normalize(u - axis * Vector3.Dot(u, axis));
                Vector3 w = Vector3.Cross(axis, u);
                for (int s = 0; s <= seg; s++)
                {
                    float ang = MathHelper.TwoPi * s / seg;
                    Vector3 dir = u * MathF.Cos(ang) + w * MathF.Sin(ang);
                    v.Add(new VertexPositionNormalTexture(points[k] + dir * radii[k], dir, new Vector2(s / (float)seg, k / (count - 1f))));
                }
            }
            for (int k = 0; k < count - 1; k++)
            {
                int ring = baseIdx + k * (seg + 1), next = ring + seg + 1;
                for (int s = 0; s < seg; s++)
                {
                    idx.Add((short)(ring + s)); idx.Add((short)(next + s)); idx.Add((short)(ring + s + 1));
                    idx.Add((short)(ring + s + 1)); idx.Add((short)(next + s)); idx.Add((short)(next + s + 1));
                }
            }
        }

        /// <summary>A flat disc closing a tube's end, facing along <paramref name="outward"/> (the sawn end of a log).</summary>
        public static void AddCap(List<VertexPositionNormalTexture> v, List<short> idx, int seg,
            Vector3 centre, float radius, Vector3 outward)
        {
            outward = Vector3.Normalize(outward);
            Vector3 ref0 = MathF.Abs(outward.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 u = Vector3.Normalize(Vector3.Cross(ref0, outward));
            Vector3 w = Vector3.Cross(outward, u);

            short centreIdx = (short)v.Count;
            v.Add(new VertexPositionNormalTexture(centre, outward, new Vector2(0.5f, 0.5f)));
            for (int s = 0; s < seg; s++)
            {
                float ang = MathHelper.TwoPi * s / seg;
                Vector3 dir = u * MathF.Cos(ang) + w * MathF.Sin(ang);
                v.Add(new VertexPositionNormalTexture(centre + dir * radius, outward, new Vector2(0.5f + 0.5f * MathF.Cos(ang), 0.5f + 0.5f * MathF.Sin(ang))));
            }
            for (int s = 0; s < seg; s++)
            {
                //Clockwise seen from outside (looking back along `outward`): the winding that faces the cap out.
                idx.Add(centreIdx);
                idx.Add((short)(centreIdx + 1 + (s + 1) % seg));
                idx.Add((short)(centreIdx + 1 + s));
            }
        }

        /// <summary>
        /// One face of a ribbon (a grass blade, a palm's fan blade), wound so that it FACES <paramref name="n"/>:
        /// MonoGame's front face is clockwise seen from outside, i.e. (b - a) × (c - a) pointing away from the
        /// viewer, so the winding is checked against the normal rather than assumed (see the triangle-winding
        /// convention in CLAUDE.md). Call it twice with ±n for a two-sided sheet under ordinary culling.
        /// </summary>
        public static void AddRibbon(List<VertexPositionNormalTexture> v, List<short> idx,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            short i0 = (short)v.Count;
            v.Add(new VertexPositionNormalTexture(a, n, new Vector2(0f, 0f)));
            v.Add(new VertexPositionNormalTexture(b, n, new Vector2(1f, 0f)));
            v.Add(new VertexPositionNormalTexture(c, n, new Vector2(1f, 1f)));
            v.Add(new VertexPositionNormalTexture(d, n, new Vector2(0f, 1f)));
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f;
            idx.Add(i0); idx.Add(flip ? (short)(i0 + 2) : (short)(i0 + 1)); idx.Add(flip ? (short)(i0 + 1) : (short)(i0 + 2));
            idx.Add(i0); idx.Add(flip ? (short)(i0 + 3) : (short)(i0 + 2)); idx.Add(flip ? (short)(i0 + 2) : (short)(i0 + 3));
        }

        /// <summary>
        /// A surface of revolution about the Y axis appended to the lists — a trunk that is not a straight
        /// tube (the baobab's bottle) — from a profile of (radius, y) points traced <b>top → outside →
        /// underside</b> as <see cref="LatheMesh"/> takes it. Smooth normals off the profile's own tangent,
        /// an optional <see cref="LatheMesh.Irregularity"/> wobble scaled per ring, wound like
        /// <see cref="AddTube"/>. A lathe in the acacia's own lists rather than a <see cref="LatheMesh"/>
        /// read back, because the lathe's buffers are write-only and cannot be read back at all.
        /// </summary>
        /// <param name="flutes">How many vertical flutes the solid is folded into (#610, the baobab's trunk): broad
        /// rounded lobes between narrow grooves, 0 for none. Weighted by each ring's wobble like the irregularity,
        /// so they fade where the profile keeps the surface smooth.</param>
        /// <param name="fluteDepth">How deep a groove cuts, as a share of the radius.</param>
        public static void AddRevolved(List<VertexPositionNormalTexture> v, List<short> idx, int seg,
            IReadOnlyList<(float radius, float y, float wobble)> profile, float irregularityAmplitude, float irregularityPhase,
            int flutes = 0, float fluteDepth = 0f)
        {
            int rings = profile.Count;
            Vector3 u = Vector3.UnitZ, w = Vector3.UnitX;   //AddTube's own basis for an axis pointing up

            //Per-ring profile normal: the tangent turned a quarter so it points out of the solid for a
            //profile traced downward on the outside (and up on a top run out from the axis).
            var normal2 = new Vector2[rings];
            for (int i = 0; i < rings; i++)
            {
                int i0 = Math.Max(i - 1, 0), i1 = Math.Min(i + 1, rings - 1);
                Vector2 t = new(profile[i1].radius - profile[i0].radius, profile[i1].y - profile[i0].y);
                if (t.LengthSquared() < 1e-8f) t = new Vector2(0f, -1f);
                t.Normalize();
                normal2[i] = new Vector2(-t.Y, t.X);
            }

            short baseIdx = (short)v.Count;
            for (int i = 0; i < rings; i++)
            {
                (float radius, float y, float wobble) = profile[i];
                for (int s = 0; s <= seg; s++)
                {
                    float ang = MathHelper.TwoPi * s / seg;
                    Vector3 dir = u * MathF.Cos(ang) + w * MathF.Sin(ang);
                    float r = radius + irregularityAmplitude * wobble * LatheMesh.Irregularity(ang + irregularityPhase, y);
                    Vector3 n = Vector3.Normalize(dir * normal2[i].X + Vector3.Up * normal2[i].Y);

                    if (flutes > 0 && radius > 0f)
                    {
                        //Lobes between grooves: 1 - |sin| is 1 in a groove and 0 on a lobe's crown, and cubing it
                        //keeps the grooves narrow. The normal leans off the radial by the radius's own slope round
                        //the axis, so a groove's two walls face each other the way the folds of the trunk do.
                        float Fold(float at) => 1f - fluteDepth * wobble * MathF.Pow(1f - MathF.Abs(MathF.Sin(flutes * at * 0.5f)), 3f);
                        const float E = 0.01f;
                        float fold = Fold(ang);
                        float slope = (Fold(ang + E) - Fold(ang - E)) / (2f * E) / fold;
                        r *= fold;
                        Vector3 around = -u * MathF.Sin(ang) + w * MathF.Cos(ang);
                        n = Vector3.Normalize((dir - around * slope) * normal2[i].X + Vector3.Up * normal2[i].Y);
                    }
                    v.Add(new VertexPositionNormalTexture(dir * r + Vector3.Up * y, n, new Vector2(s / (float)seg, i / (float)(rings - 1))));
                }
            }
            //Between ring i (upper) and ring i+1 (lower): AddTube's index pattern with a = the lower ring.
            for (int i = 0; i < rings - 1; i++)
            {
                int upper = baseIdx + i * (seg + 1);
                int lower = upper + seg + 1;
                for (int s = 0; s < seg; s++)
                {
                    idx.Add((short)(lower + s)); idx.Add((short)(upper + s)); idx.Add((short)(lower + s + 1));
                    idx.Add((short)(lower + s + 1)); idx.Add((short)(upper + s)); idx.Add((short)(upper + s + 1));
                }
            }
        }

        public static (VertexBuffer, IndexBuffer) Upload(GraphicsDevice device, List<VertexPositionNormalTexture> v, List<short> idx)
        {
            //A short index names at most 65 536 vertices (read unsigned); past that it wraps and draws the wrong ones
            //without a word, which is how a baobab's leaves lost a sixth of their cards for weeks (#670)
            if (v.Count > 65536)
                throw new InvalidOperationException($"{v.Count} vertices under 16-bit indices: build the mesh with int indices.");

            var vb = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, v.Count, BufferUsage.WriteOnly);
            vb.SetData(v.ToArray());
            var ib = new IndexBuffer(device, IndexElementSize.SixteenBits, idx.Count, BufferUsage.WriteOnly);
            ib.SetData(idx.ToArray());
            return (vb, ib);
        }

        /// <summary>
        /// <see cref="Upload(GraphicsDevice, List{VertexPositionNormalTexture}, List{short})"/> for a mesh built with int
        /// indices — the leaf sprays, which a baobab's crown takes past 65 536 vertices: 32-bit indices when the vertices
        /// need them, 16-bit otherwise.
        /// </summary>
        public static (VertexBuffer, IndexBuffer) Upload(GraphicsDevice device, List<VertexPositionNormalTexture> v, List<int> idx)
        {
            var vb = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, v.Count, BufferUsage.WriteOnly);
            vb.SetData(v.ToArray());
            IndexBuffer ib;
            if (v.Count > 65536)
            {
                ib = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, idx.Count, BufferUsage.WriteOnly);
                ib.SetData(idx.ToArray());
            }
            else
            {
                var narrow = new ushort[idx.Count];
                for (int i = 0; i < narrow.Length; i++) narrow[i] = (ushort)idx[i];
                ib = new IndexBuffer(device, IndexElementSize.SixteenBits, idx.Count, BufferUsage.WriteOnly);
                ib.SetData(narrow);
            }
            return (vb, ib);
        }
    }

    /// <summary>A mesh over vertex and index lists somebody else generated — the several tiers of an acacia's
    /// canopy in one draw. Owns the buffers.</summary>
    internal sealed class UploadedMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        public UploadedMesh(GraphicsDevice device, List<VertexPositionNormalTexture> v, List<short> idx, BoundingSphere bounds)
        {
            (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
            PrimitiveCount = idx.Count / 3;
            BoundingSphere = bounds;
        }

        /// <summary>The same over int indices (the leaf sprays), 32-bit when the vertices need them.</summary>
        public UploadedMesh(GraphicsDevice device, List<VertexPositionNormalTexture> v, List<int> idx, BoundingSphere bounds)
        {
            (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
            PrimitiveCount = idx.Count / 3;
            BoundingSphere = bounds;
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose(); VertexBuffer = null;
            IndexBuffer?.Dispose(); IndexBuffer = null;
        }
    }

    /// <summary>
    /// How a <see cref="FoliageMesh"/> is shaped: the lobes it is pushed out into and how flat its top and
    /// tucked its underside are. Four named styles (#451), each a different plant off the one generator.
    /// </summary>
    public readonly struct FoliageStyle
    {
        /// <summary>How much the very top is flattened (0 = a full dome, 1 = the top pole sits at the equator).</summary>
        public readonly float TopFlatten;
        /// <summary>How much the underside is flattened the same way (0 = a full dome below, 1 = a flat
        /// underside) — an umbrella crown is a low dome over a flat underside, and the tuck alone cannot
        /// give that: it narrows the lower half towards the pole, which is a cone, not a floor.</summary>
        public readonly float BottomFlatten;
        /// <summary>How hard the underside narrows towards the bottom pole (0 = a full sphere below).</summary>
        public readonly float TuckStrength;
        /// <summary>The base unevenness's share of the radius — the rim's raggedness.</summary>
        public readonly float RimNoise;
        /// <summary>How many lobes, rolled between these.</summary>
        public readonly int LobesMin, LobesMax;
        /// <summary>Each lobe's height as a share of the radius, and its sharpness (the exponent), rolled between these.</summary>
        public readonly float LobeWeightMin, LobeWeightMax, LobeSharpMin, LobeSharpMax;
        /// <summary>The range a lobe's direction is drawn from, as the height on the unit sphere (−1..1).</summary>
        public readonly float LobeYMin, LobeYMax;

        public FoliageStyle(float topFlatten, float bottomFlatten, float tuckStrength, float rimNoise, int lobesMin, int lobesMax,
            float lobeWeightMin, float lobeWeightMax, float lobeSharpMin, float lobeSharpMax, float lobeYMin, float lobeYMax)
        {
            TopFlatten = topFlatten; BottomFlatten = bottomFlatten; TuckStrength = tuckStrength; RimNoise = rimNoise;
            LobesMin = lobesMin; LobesMax = lobesMax;
            LobeWeightMin = lobeWeightMin; LobeWeightMax = lobeWeightMax;
            LobeSharpMin = lobeSharpMin; LobeSharpMax = lobeSharpMax;
            LobeYMin = lobeYMin; LobeYMax = lobeYMax;
        }

        /// <summary>The #202 crown, unchanged: a billowing mass of overlapping lobes, flattened a little on top.
        /// The savanna's bushes are still this.</summary>
        public static readonly FoliageStyle Crown = new(0.35f, 0f, 0.5f, 0.18f, 7, 10, 0.14f, 0.42f, 2.2f, 5.7f, -0.1f, 0.9f);

        /// <summary>An acacia's umbrella of foliage, read off the #451 references: a low dome on top over a
        /// nearly flat underside, so it is a thin layer at the rim and thickest at the middle; its rim more
        /// ragged and its lobes flatter and more numerous than a crown's — a layer of fine foliage seen from
        /// the side, not a ball on a stick.</summary>
        public static readonly FoliageStyle Tier = new(0.4f, 0.75f, 0.3f, 0.30f, 9, 13, 0.10f, 0.32f, 3f, 7f, -0.2f, 0.7f);

        /// <summary>Low thorny scrub: rounder than a crown, its lobes small and everywhere.</summary>
        public static readonly FoliageStyle Scrub = new(0.1f, 0f, 0.3f, 0.2f, 8, 12, 0.15f, 0.35f, 2f, 4f, -0.2f, 1f);

        /// <summary>A bunch of tall grass: many sharp lobes all pointing up and out, so the silhouette is
        /// spiked rather than rounded, over a sphere the caller buries to its waist.</summary>
        public static readonly FoliageStyle Tuft = new(0f, 0f, 0f, 0.25f, 14, 20, 0.25f, 0.55f, 6f, 12f, 0.1f, 0.9f);
    }

    /// <summary>
    /// A billowing mass of foliage: a UV sphere pushed out into a cluster of overlapping lobes over a base
    /// unevenness, scaled wide and low so it reads as an acacia's flat-topped crown (or, small, a bush). The
    /// lobes are what make it a cluster of leaf masses rather than one smooth ball. Normals stay spherical so
    /// the light wraps the lobes softly the way lit foliage does. Rolled from a seed, so no two are alike.
    /// <para>
    /// The shape is a <see cref="FoliageStyle"/> since #451, and <see cref="Generate(List{VertexPositionNormalTexture}, List{short}, float, float, Vector3, int, FoliageStyle)"/> appends the geometry
    /// to a caller's lists so that several masses can go into one mesh — an acacia's tiers are one draw.
    /// </para>
    /// </summary>
    public sealed class FoliageMesh : IProceduralMesh, IDisposable
    {
        //The mass's tessellation: rings round it and bands from pole to pole
        private const int SLICES = 16, STACKS = 11;

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        public FoliageMesh(GraphicsDevice device, float radius, float halfHeight, float centreY, int seed)
            : this(device, radius, halfHeight, centreY, seed, FoliageStyle.Crown) { }

        public FoliageMesh(GraphicsDevice device, float radius, float halfHeight, float centreY, int seed, FoliageStyle style)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            float reach = Generate(v, idx, radius, halfHeight, new Vector3(0f, centreY, 0f), seed, style);

            PrimitiveCount = idx.Count / 3;
            (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
            BoundingSphere = new BoundingSphere(new Vector3(0f, centreY, 0f), reach);
        }

        /// <summary>
        /// Appends one lobed mass to the lists and returns how far from <paramref name="centre"/> it reaches.
        /// Sixteen slices by eleven stacks: enough for the lobes to read, and small enough that a plain of a
        /// few hundred of them is nothing.
        /// </summary>
        public static float Generate(List<VertexPositionNormalTexture> v, List<short> idx,
            float radius, float halfHeight, Vector3 centre, int seed, FoliageStyle style) =>
            Generate(v, idx, radius, halfHeight, centre, seed, style, SLICES, STACKS);

        /// <summary>
        /// The same mass at a coarser tessellation (#609's third round): the meadow's old trees build their crowns of
        /// fifty-odd small clumps, and at the full sixteen by eleven those lumps alone were a quarter of a million
        /// triangles a pass across the meadow's fifteen trees. The normals stay the sphere's, so the shading keeps its
        /// round falloff; only the silhouette's facets coarsen, on a lump a couple of units across.
        /// </summary>
        public static float Generate(List<VertexPositionNormalTexture> v, List<short> idx,
            float radius, float halfHeight, Vector3 centre, int seed, FoliageStyle style, int slices, int stacks)
        {
            Random rng = new(seed);
            float phase = seed * 2.39996f;

            int lobeCount = style.LobesMin + rng.Next(style.LobesMax - style.LobesMin + 1);
            var lobeDir = new Vector3[lobeCount];
            var lobeWeight = new float[lobeCount];
            var lobeSharp = new float[lobeCount];
            for (int k = 0; k < lobeCount; k++)
            {
                float ly = style.LobeYMin + (style.LobeYMax - style.LobeYMin) * (float)rng.NextDouble();
                float la = (float)rng.NextDouble() * MathHelper.TwoPi;
                float lr = MathF.Sqrt(MathF.Max(0f, 1f - ly * ly));
                lobeDir[k] = new Vector3(MathF.Cos(la) * lr, ly, MathF.Sin(la) * lr);
                lobeWeight[k] = style.LobeWeightMin + (style.LobeWeightMax - style.LobeWeightMin) * (float)rng.NextDouble();
                lobeSharp[k] = style.LobeSharpMin + (style.LobeSharpMax - style.LobeSharpMin) * (float)rng.NextDouble();
            }

            float Bulge(Vector3 d)
            {
                float a = MathF.Atan2(d.Z, d.X) + phase;
                float n = 0.5f * LatheMesh.Irregularity(a, d.Y * 3f + phase)
                    + 0.3f * LatheMesh.Irregularity(a + 2.1f, d.Y * 3f + 1.7f + phase)
                    + 0.2f * LatheMesh.Irregularity(a + 4.3f, d.Y * 3f + 3.4f + phase);
                float swell = style.RimNoise * n;
                for (int k = 0; k < lobeCount; k++)
                    swell += lobeWeight[k] * MathF.Pow(MathF.Max(0f, Vector3.Dot(d, lobeDir[k])), lobeSharp[k]);
                return swell;
            }

            int vertexCount = (stacks - 1) * slices + 2;
            short baseIdx = (short)v.Count;
            float reach = 0f;

            VertexPositionNormalTexture Build(Vector3 dir)
            {
                float swell = 1f + Bulge(dir);
                float tuck = BottomTuck(dir.Y, style.TuckStrength);
                float rXZ = radius * swell * (1f - tuck);
                //Flatten the top so the mass reads flat-topped rather than domed, and the underside so it
                //reads as a layer rather than a ball, each by the style's amount.
                float yScale = dir.Y > 0f ? halfHeight * (1f - style.TopFlatten * dir.Y) : halfHeight * (1f + style.BottomFlatten * dir.Y);
                Vector3 pos = new(centre.X + dir.X * rXZ, centre.Y + dir.Y * yScale * swell, centre.Z + dir.Z * rXZ);
                reach = MathF.Max(reach, (pos - centre).Length());
                return new VertexPositionNormalTexture(pos, dir, new Vector2(dir.X * 0.5f + 0.5f, dir.Z * 0.5f + 0.5f));
            }

            v.Add(Build(Vector3.Up));
            for (int stack = 1; stack < stacks; stack++)
            {
                float phi = MathF.PI * stack / stacks;
                float y = MathF.Cos(phi);
                float ringRadius = MathF.Sin(phi);
                for (int slice = 0; slice < slices; slice++)
                {
                    float theta = MathHelper.TwoPi * slice / slices;
                    v.Add(Build(new Vector3(ringRadius * MathF.Cos(theta), y, ringRadius * MathF.Sin(theta))));
                }
            }
            int bottomPole = baseIdx + vertexCount - 1;
            v.Add(Build(Vector3.Down));

            for (int slice = 0; slice < slices; slice++)
            {
                idx.Add(baseIdx);
                idx.Add((short)(baseIdx + 1 + slice));
                idx.Add((short)(baseIdx + 1 + (slice + 1) % slices));
            }
            for (int stack = 0; stack < stacks - 2; stack++)
            {
                int upper = baseIdx + 1 + stack * slices;
                int lower = upper + slices;
                for (int slice = 0; slice < slices; slice++)
                {
                    int next = (slice + 1) % slices;
                    idx.Add((short)(upper + slice));
                    idx.Add((short)(lower + slice));
                    idx.Add((short)(upper + next));
                    idx.Add((short)(upper + next));
                    idx.Add((short)(lower + slice));
                    idx.Add((short)(lower + next));
                }
            }
            int lastRing = baseIdx + 1 + (stacks - 2) * slices;
            for (int slice = 0; slice < slices; slice++)
            {
                idx.Add((short)bottomPole);
                idx.Add((short)(lastRing + (slice + 1) % slices));
                idx.Add((short)(lastRing + slice));
            }

            return reach;
        }

        //The mass narrows towards its underside so it settles over the branches instead of balancing on them.
        private static float BottomTuck(float y, float strength)
        {
            float t = MathHelper.Clamp((-y - 0.15f) / 0.85f, 0f, 1f);
            return strength * t * t;
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose(); VertexBuffer = null;
            IndexBuffer?.Dispose(); IndexBuffer = null;
        }
    }
}
