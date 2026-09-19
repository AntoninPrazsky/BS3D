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

        /// <summary>Every tier of foliage in one mesh; <c>null</c> for a <see cref="AcaciaKind.Dead"/> tree.</summary>
        public IProceduralMesh Canopy { get; }

        public AcaciaMesh(GraphicsDevice device, AcaciaKind kind, float trunkRadius, float treeHeight, float canopyRadius, int seed)
        {
            Kind = kind;
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
            }
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Canopy as IDisposable)?.Dispose();
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
                TubeGeometry.AddTube(v, idx, SEG, new Vector3(0f, 0f, 0f), trunkRadius * 1.5f,
                    new Vector3(0f, forkY, 0f), trunkRadius * 0.9f);

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

        public static (VertexBuffer, IndexBuffer) Upload(GraphicsDevice device, List<VertexPositionNormalTexture> v, List<short> idx)
        {
            var vb = new VertexBuffer(device, VertexPositionNormalTexture.VertexDeclaration, v.Count, BufferUsage.WriteOnly);
            vb.SetData(v.ToArray());
            var ib = new IndexBuffer(device, IndexElementSize.SixteenBits, idx.Count, BufferUsage.WriteOnly);
            ib.SetData(idx.ToArray());
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

        public FoliageStyle(float topFlatten, float tuckStrength, float rimNoise, int lobesMin, int lobesMax,
            float lobeWeightMin, float lobeWeightMax, float lobeSharpMin, float lobeSharpMax, float lobeYMin, float lobeYMax)
        {
            TopFlatten = topFlatten; TuckStrength = tuckStrength; RimNoise = rimNoise;
            LobesMin = lobesMin; LobesMax = lobesMax;
            LobeWeightMin = lobeWeightMin; LobeWeightMax = lobeWeightMax;
            LobeSharpMin = lobeSharpMin; LobeSharpMax = lobeSharpMax;
            LobeYMin = lobeYMin; LobeYMax = lobeYMax;
        }

        /// <summary>The #202 crown, unchanged: a billowing mass of overlapping lobes, flattened a little on top.
        /// The savanna's bushes are still this.</summary>
        public static readonly FoliageStyle Crown = new(0.35f, 0.5f, 0.18f, 7, 10, 0.14f, 0.42f, 2.2f, 5.7f, -0.1f, 0.9f);

        /// <summary>An acacia's plate of foliage: flat on top, tucked hard underneath so it is a thin layer at
        /// the rim, its rim more ragged and its lobes flatter and more numerous — a layer of fine foliage
        /// seen from the side, not a ball.</summary>
        public static readonly FoliageStyle Tier = new(0.6f, 0.65f, 0.30f, 9, 13, 0.10f, 0.32f, 3f, 7f, -0.2f, 0.7f);

        /// <summary>Low thorny scrub: rounder than a crown, its lobes small and everywhere.</summary>
        public static readonly FoliageStyle Scrub = new(0.1f, 0.3f, 0.2f, 8, 12, 0.15f, 0.35f, 2f, 4f, -0.2f, 1f);

        /// <summary>A bunch of tall grass: many sharp lobes all pointing up and out, so the silhouette is
        /// spiked rather than rounded, over a sphere the caller buries to its waist.</summary>
        public static readonly FoliageStyle Tuft = new(0f, 0f, 0.25f, 14, 20, 0.25f, 0.55f, 6f, 12f, 0.1f, 0.9f);
    }

    /// <summary>
    /// A billowing mass of foliage: a UV sphere pushed out into a cluster of overlapping lobes over a base
    /// unevenness, scaled wide and low so it reads as an acacia's flat-topped crown (or, small, a bush). The
    /// lobes are what make it a cluster of leaf masses rather than one smooth ball. Normals stay spherical so
    /// the light wraps the lobes softly the way lit foliage does. Rolled from a seed, so no two are alike.
    /// <para>
    /// The shape is a <see cref="FoliageStyle"/> since #451, and <see cref="Generate"/> appends the geometry
    /// to a caller's lists so that several masses can go into one mesh — an acacia's tiers are one draw.
    /// </para>
    /// </summary>
    public sealed class FoliageMesh : IProceduralMesh, IDisposable
    {
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
            float radius, float halfHeight, Vector3 centre, int seed, FoliageStyle style)
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

            const int SLICES = 16, STACKS = 11;
            int vertexCount = (STACKS - 1) * SLICES + 2;
            short baseIdx = (short)v.Count;
            float reach = 0f;

            VertexPositionNormalTexture Build(Vector3 dir)
            {
                float swell = 1f + Bulge(dir);
                float tuck = BottomTuck(dir.Y, style.TuckStrength);
                float rXZ = radius * swell * (1f - tuck);
                //Flatten the top so the mass reads flat-topped rather than domed, by the style's amount.
                float yScale = dir.Y > 0f ? halfHeight * (1f - style.TopFlatten * dir.Y) : halfHeight;
                Vector3 pos = new(centre.X + dir.X * rXZ, centre.Y + dir.Y * yScale * swell, centre.Z + dir.Z * rXZ);
                reach = MathF.Max(reach, (pos - centre).Length());
                return new VertexPositionNormalTexture(pos, dir, new Vector2(dir.X * 0.5f + 0.5f, dir.Z * 0.5f + 0.5f));
            }

            v.Add(Build(Vector3.Up));
            for (int stack = 1; stack < STACKS; stack++)
            {
                float phi = MathF.PI * stack / STACKS;
                float y = MathF.Cos(phi);
                float ringRadius = MathF.Sin(phi);
                for (int slice = 0; slice < SLICES; slice++)
                {
                    float theta = MathHelper.TwoPi * slice / SLICES;
                    v.Add(Build(new Vector3(ringRadius * MathF.Cos(theta), y, ringRadius * MathF.Sin(theta))));
                }
            }
            int bottomPole = baseIdx + vertexCount - 1;
            v.Add(Build(Vector3.Down));

            for (int slice = 0; slice < SLICES; slice++)
            {
                idx.Add(baseIdx);
                idx.Add((short)(baseIdx + 1 + slice));
                idx.Add((short)(baseIdx + 1 + (slice + 1) % SLICES));
            }
            for (int stack = 0; stack < STACKS - 2; stack++)
            {
                int upper = baseIdx + 1 + stack * SLICES;
                int lower = upper + SLICES;
                for (int slice = 0; slice < SLICES; slice++)
                {
                    int next = (slice + 1) % SLICES;
                    idx.Add((short)(upper + slice));
                    idx.Add((short)(lower + slice));
                    idx.Add((short)(upper + next));
                    idx.Add((short)(upper + next));
                    idx.Add((short)(lower + slice));
                    idx.Add((short)(lower + next));
                }
            }
            int lastRing = baseIdx + 1 + (STACKS - 2) * SLICES;
            for (int slice = 0; slice < SLICES; slice++)
            {
                idx.Add((short)bottomPole);
                idx.Add((short)(lastRing + (slice + 1) % SLICES));
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
