using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>The kinds of old tree the meadow grows (#609's third round).</summary>
    internal enum MeadowTreeKind
    {
        /// <summary>A field oak: a short massive trunk, heavy limbs reaching out low, a broad dome wider than it is tall.</summary>
        Oak,
        /// <summary>A lime: the tallest, a higher trunk and steeper limbs under a tall dome.</summary>
        Lime,
        /// <summary>A willow by the water: a leaning trunk, upright limbs, a rounded head, silver-green.</summary>
        Willow,
    }

    /// <summary>
    /// <b>An old field tree</b> (#609's third round). The owner, after the second round: <i>"trees are still missing —
    /// the meadow wants some realistic, old, taller trees"</i>. The references (<c>C:\Users\panrd\AI\sd\out\609c-klein</c>
    /// and <c>609c-zimage</c>: an old oak at a path's end, an ancient oak in a meadow, limes and willows by a brook) all
    /// draw the same build, and the lumpy oak of the second round had none of it: <b>a massive short trunk on surface
    /// roots, forking into a few heavy limbs that fork again as they spread, and a broad dome of many small clumps of
    /// leaves</b> on the ends of their branches, the sky showing between clumps at the rim and the dark limbs under them.
    /// <para>
    /// <b>Grown from the crown inwards.</b> The dome is laid out first — clump centres spread evenly over an
    /// ellipsoid's upper part, a share of them deeper inside it, so the outline is the references' dome whatever the
    /// dice say — and the wood is grown to it: two to five limbs out of the fork, each to a knee a third of the way
    /// out, two or three boughs out of every knee to knees of their own, and from those a branch into every clump on
    /// their side. So no branch ends in the air, no clump floats, and the wood forks as a tree's does rather than
    /// spreading from one point like the fingers of a hand, which is what one fork of six limbs looked like.
    /// </para>
    /// <para>
    /// <b>Three crowns from one layout</b>, the acacia's split (#610): <see cref="Crown"/>, the clumps as solid lumps
    /// (the Low tier's); <see cref="Core"/>, the same lumps drawn smaller; and <see cref="Leaves"/>, cards over each
    /// clump's shell that <c>Acacia.fx</c> cuts into twigs of broad leaves (<c>BroadLeafMask</c>) and lights through,
    /// drawn round the core at every other tier. The core keeps the dome dense where a card-only crown would show the
    /// sky through its middle; the cards give every clump the leafy, broken edge.
    /// </para>
    /// </summary>
    internal sealed class MeadowTreeMesh : IDisposable
    {
        public IProceduralMesh Wood { get; }
        public IProceduralMesh Crown { get; }
        public IProceduralMesh Core { get; }
        public IProceduralMesh Leaves { get; }

        /// <summary>How far the crown reaches from the axis at scale 1.</summary>
        public float CrownReach { get; }

        /// <summary>The root flare's radius at scale 1.</summary>
        public float BaseRadius { get; }

        //How much smaller the core's lumps are than the Low tier's, inside the leaf cards
        private const float CORE_SHARE = 0.66f;

        //The clumps' tessellation, coarser than a lone bush's (FoliageMesh's own 16 by 11): fifty-odd of them a tree
        private const int LUMP_SLICES = 10, LUMP_STACKS = 7;

        //The leaf cards: a card's side against its clump's radius (square, so the leaves keep their shape), how many
        //cards a clump takes (its shell's cover, before the leaves are cut out of them - a card is about a third leaf),
        //and the lowest point of the shell they cover (-1 the bottom pole): a clump's underside is its shade, and the
        //lumps inside fill it
        private const float CARD_SIZE = 0.7f, CARD_COVER = 1.5f, CARD_LOWEST = -1f;

        //The share of the clumps set deeper in the dome than its shell, for the depth seen through the gaps
        private const float INNER_SHARE = 0.22f;

        private readonly struct Figure(float height, float trunk, float fork, float reach, float crownY, float crownHalf,
            int limbsMin, int limbsMax, float elevationMin, float elevationMax, int clumps, float clumpMin, float clumpMax, float lean)
        {
            public readonly float Height = height;         //ground to the crown's top
            public readonly float Trunk = trunk;           //the trunk's radius above the flare
            public readonly float Fork = fork;             //where the limbs leave it, as a share of the height
            public readonly float Reach = reach;           //the dome's half-width
            public readonly float CrownY = crownY;         //the dome's centre, as a share of the height
            public readonly float CrownHalf = crownHalf;    //the dome's half-height, as a share of the height
            public readonly int LimbsMin = limbsMin, LimbsMax = limbsMax;
            public readonly float ElevationMin = elevationMin, ElevationMax = elevationMax;   //a limb's rise, degrees
            public readonly int Clumps = clumps;
            public readonly float ClumpMin = clumpMin, ClumpMax = clumpMax;                 //a clump's radius against the reach
            public readonly float Lean = lean;             //the trunk's lean from upright, radians
        }

        //Read off the references: the oak half as wide again as it is tall, its limbs out low; the lime the tallest,
        //narrower, steeper; the willow shorter and leaning, a rounder head
        private static Figure FigureOf(MeadowTreeKind kind) => kind switch
        {
            MeadowTreeKind.Lime => new Figure(29f, 1.1f, 0.24f, 10.5f, 0.6f, 0.36f, 3, 4, 50f, 70f, 46, 0.16f, 0.22f, 0.03f),
            MeadowTreeKind.Willow => new Figure(15f, 1.15f, 0.26f, 8f, 0.62f, 0.34f, 3, 5, 45f, 70f, 30, 0.17f, 0.24f, 0.14f),
            _ => new Figure(24f, 1.3f, 0.24f, 15.5f, 0.6f, 0.36f, 2, 4, 20f, 42f, 58, 0.14f, 0.2f, 0.04f),
        };

        public MeadowTreeMesh(GraphicsDevice device, MeadowTreeKind kind, int seed)
        {
            Figure f = FigureOf(kind);
            Random rng = new(seed);

            float height = f.Height * (0.92f + 0.16f * (float)rng.NextDouble());
            float reach = f.Reach * (0.9f + 0.2f * (float)rng.NextDouble());
            float crownY = height * f.CrownY;
            float crownHalf = height * f.CrownHalf;
            float forkY = height * f.Fork;
            float leanBearing = (float)rng.NextDouble() * MathHelper.TwoPi;
            Vector3 leanDir = new(MathF.Cos(leanBearing), 0f, MathF.Sin(leanBearing));
            Vector3 Lean(float y) => leanDir * (y * MathF.Tan(f.Lean));
            //The fork stands off the axis by the lean; the crown follows it half-way, as a leaning tree's crown rights itself
            Vector3 fork = new Vector3(0f, forkY, 0f) + Lean(forkY);
            Vector3 crownCentre = new Vector3(0f, crownY, 0f) + Lean(forkY) * 0.5f;

            //--- The dome: clump centres spread over the ellipsoid's upper part, area-evenly (a height uniform in the
            //cap is area-uniform on a sphere) on the golden angle, then shaken a little, and pulled inside the envelope
            //by their own radius so the lumps' outer faces are what reaches it; a share set deeper, for the gaps
            var clumps = new List<(Vector3 Centre, float Radius, bool Inner)>();
            const float LOWEST = -0.5f;
            float golden = MathF.PI * (3f - MathF.Sqrt(5f));
            float spin = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int i = 0; i < f.Clumps; i++)
            {
                float y = 1f - (1f - LOWEST) * (i + 0.5f) / f.Clumps;
                float ring = MathF.Sqrt(1f - y * y);
                float a = spin + golden * i + 0.35f * ((float)rng.NextDouble() - 0.5f);
                float r = reach * (f.ClumpMin + (f.ClumpMax - f.ClumpMin) * (float)rng.NextDouble());
                bool inner = rng.NextDouble() < INNER_SHARE;
                float inset = inner ? 0.5f + 0.2f * (float)rng.NextDouble()
                    : 1f - r / reach * 0.8f + 0.1f * ((float)rng.NextDouble() - 0.5f);
                Vector3 centre = crownCentre + new Vector3(MathF.Cos(a) * ring * reach, y * crownHalf, MathF.Sin(a) * ring * reach) * inset;
                clumps.Add((centre, r, inner));
            }

            //--- The wood
            var wv = new List<VertexPositionNormalTexture>();
            var widx = new List<short>();
            float trunk = f.Trunk * (0.92f + 0.16f * (float)rng.NextDouble());

            //The trunk: a flared foot sunk into the ground, a massive stem that bends once on its way to the fork, and on
            //up into the limbs so its top is inside them - one swept skin (TubeGeometry.AddSweep), where three straight
            //tubes met at the bend as a step and a sleeve
            Vector3 knee = new Vector3(0f, forkY * 0.45f, 0f) + Lean(forkY * 0.45f)
                + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * (trunk * 0.3f);
            Vector3 flareTop = new Vector3(0f, trunk * 0.9f, 0f) + Lean(trunk * 0.9f);
            Vector3 top = fork + (fork - knee) * (trunk * 0.7f / Vector3.Distance(fork, knee));
            var stem = new[]
            {
                new Vector3(0f, -0.6f, 0f), new Vector3(0f, trunk * 0.3f, 0f) + Lean(trunk * 0.3f), flareTop,
                Vector3.Lerp(flareTop, knee, 0.5f), knee, Vector3.Lerp(knee, fork, 0.5f), fork, top,
            };
            var girth = new[] { trunk * 1.6f, trunk * 1.3f, trunk * 1.08f, trunk * 1.03f, trunk, trunk * 0.93f, trunk * 0.84f, trunk * 0.6f };
            TubeGeometry.AddSweep(wv, widx, 12, stem, girth);
            TubeGeometry.AddCap(wv, widx, 12, top, trunk * 0.6f, top - fork);

            //Surface roots: the old tree's foot spreads into the ground, a few buttresses diving in
            int roots = 5 + rng.Next(3);
            float rootPhase = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int i = 0; i < roots; i++)
            {
                float a = rootPhase + MathHelper.TwoPi * i / roots + 0.4f * ((float)rng.NextDouble() - 0.5f);
                Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
                float spread = trunk * (2.3f + 0.9f * (float)rng.NextDouble());
                TubeGeometry.AddTube(wv, widx, 7, dir * (trunk * 0.7f) + new Vector3(0f, trunk * 0.9f, 0f), trunk * 0.42f,
                    dir * spread + new Vector3(0f, -0.45f, 0f), trunk * 0.1f);
            }

            //A bough from one point to another, bowing up a little on the way, as wood grown towards the light does: one
            //swept skin through a quarter, the middle and three quarters of the bow
            void Bough(Vector3 from, float fromRadius, Vector3 to, float toRadius, int segments, float bow)
            {
                float rise = bow * Vector3.Distance(from, to);
                Vector3 At(float f) => Vector3.Lerp(from, to, f) + new Vector3(0f, rise * 4f * f * (1f - f), 0f);
                TubeGeometry.AddSweep(wv, widx, segments, new[] { from, At(0.25f), At(0.5f), At(0.75f), to },
                    new[] { fromRadius, MathHelper.Lerp(fromRadius, toRadius, 0.25f), MathHelper.Lerp(fromRadius, toRadius, 0.5f),
                        MathHelper.Lerp(fromRadius, toRadius, 0.75f), toRadius });
            }

            //The limbs: out of the fork at evenly turned bearings, each to a knee a third of the way out and a little up
            int limbs = f.LimbsMin + rng.Next(f.LimbsMax - f.LimbsMin + 1);
            float limbPhase = (float)rng.NextDouble() * MathHelper.TwoPi;
            var knees = new List<(Vector3 At, float Bearing, float Radius)>();
            for (int l = 0; l < limbs; l++)
            {
                float bearing = limbPhase + MathHelper.TwoPi * l / limbs + 0.5f * ((float)rng.NextDouble() - 0.5f);
                float elevation = MathHelper.ToRadians(f.ElevationMin + (f.ElevationMax - f.ElevationMin) * (float)rng.NextDouble());
                float spread = reach * (0.28f + 0.1f * (float)rng.NextDouble());
                Vector3 limbKnee = fork + new Vector3(MathF.Cos(bearing) * spread, spread * MathF.Tan(elevation), MathF.Sin(bearing) * spread);
                float limbRadius = trunk * (limbs <= 2 ? 0.7f : 0.58f);
                Bough(fork, limbRadius, limbKnee, limbRadius * 0.75f, 9, 0.1f);

                //Out of every limb's knee, two or three boughs fanning over its sector to knees of their own
                int boughs = 2 + rng.Next(2);
                float sector = MathHelper.TwoPi / limbs;
                for (int b = 0; b < boughs; b++)
                {
                    float turn = bearing + sector * ((b + 0.5f) / boughs - 0.5f) * 0.9f + 0.2f * ((float)rng.NextDouble() - 0.5f);
                    float boughReach = reach * (0.5f + 0.15f * (float)rng.NextDouble());
                    float rise = (limbKnee.Y - fork.Y) * 0.6f + crownHalf * (0.05f + 0.35f * (float)rng.NextDouble());
                    Vector3 boughKnee = new Vector3(crownCentre.X + MathF.Cos(turn) * boughReach, limbKnee.Y + rise, crownCentre.Z + MathF.Sin(turn) * boughReach);
                    Bough(limbKnee, limbRadius * 0.62f, boughKnee, trunk * 0.2f, 7, 0.08f);
                    knees.Add((boughKnee, turn, trunk * 0.2f));
                }
            }
            //And a leader up the middle, for the clumps over the top
            Vector3 leaderTop = crownCentre + new Vector3(0f, crownHalf * 0.25f, 0f);
            Bough(fork, trunk * 0.5f, leaderTop, trunk * 0.22f, 8, 0.02f);

            //The branches: from the nearest knee by bearing into every clump, or off the leader over the middle
            foreach ((Vector3 centre, float r, bool inner) in clumps)
            {
                Vector3 offset = centre - crownCentre;
                float horizontal = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
                Vector3 from;
                float fromRadius;
                if (horizontal < reach * 0.28f)
                {
                    from = Vector3.Lerp(fork, leaderTop, 0.55f + 0.45f * (float)rng.NextDouble());
                    fromRadius = trunk * 0.2f;
                }
                else
                {
                    float bearing = MathF.Atan2(offset.Z, offset.X);
                    int best = 0;
                    float bestGap = float.MaxValue;
                    for (int k = 0; k < knees.Count; k++)
                    {
                        float gap = MathF.Abs(MathHelper.WrapAngle(bearing - knees[k].Bearing));
                        if (gap < bestGap) { bestGap = gap; best = k; }
                    }
                    from = knees[best].At;
                    fromRadius = knees[best].Radius * 0.8f;
                }

                //Into the clump to a third of its radius short of the centre
                Vector3 to = centre - Vector3.Normalize(centre - from + new Vector3(0f, 1e-3f, 0f)) * (r * 0.33f);
                Bough(from, fromRadius, to, trunk * 0.06f, 5, 0.06f);
            }

            //--- The crowns
            var cv = new List<VertexPositionNormalTexture>();
            var cidx = new List<short>();
            var kv = new List<VertexPositionNormalTexture>();
            var kidx = new List<short>();
            var lv = new List<VertexPositionNormalTexture>();
            var lidx = new List<int>();
            Random leafRng = new(seed * 41 + 3);
            for (int i = 0; i < clumps.Count; i++)
            {
                (Vector3 centre, float r, bool inner) = clumps[i];
                FoliageMesh.Generate(cv, cidx, r, r * 0.8f, centre, seed * 17 + i, FoliageStyle.Crown, LUMP_SLICES, LUMP_STACKS);
                FoliageMesh.Generate(kv, kidx, r * CORE_SHARE, r * 0.8f * CORE_SHARE, centre, seed * 17 + i, FoliageStyle.Crown,
                    LUMP_SLICES, LUMP_STACKS);
                AddLeafShell(lv, lidx, centre, r, r * 0.8f, inner, leafRng);
            }

            CrownReach = reach;
            BaseRadius = trunk * 1.55f;
            BoundingSphere bounds = new(crownCentre - new Vector3(0f, crownHalf * 0.3f, 0f), MathF.Max(reach, crownY) * 1.15f);
            Wood = new UploadedMesh(device, wv, widx, bounds);
            Crown = new UploadedMesh(device, cv, cidx, bounds);
            Core = new UploadedMesh(device, kv, kidx, bounds);
            Leaves = new UploadedMesh(device, lv, lidx, bounds);
        }

        /// <summary>
        /// Leaf cards over one clump's shell, the acacia's cards (<see cref="LeafSprays"/>) laid round a lump instead of
        /// in a flat tier: each card tangent to the shell give or take a tilt, some standing proud of it so the edge is
        /// broken, its normal the shell's own so the clump shades as one rounded mass, lit on top and dark beneath. The
        /// texture coordinate carries the card and its layer as <see cref="LeafSprays"/> does — the top of the clump the
        /// top layer, its flanks the next, its underside and every clump inside the dome the deepest — which is what
        /// <c>Acacia.fx</c> darkens by.
        /// </summary>
        private static void AddLeafShell(List<VertexPositionNormalTexture> v, List<int> idx, Vector3 centre, float radius,
            float halfHeight, bool inner, Random rng)
        {
            float size = radius * CARD_SIZE;
            //The shell's cap from CARD_LOWEST up is 2 pi r^2 (1 - lowest) on a sphere of the clump's radius
            int count = (int)(CARD_COVER * MathHelper.TwoPi * radius * radius * (1f - CARD_LOWEST) / (size * size));

            for (int i = 0; i < count; i++)
            {
                float y = CARD_LOWEST + (1f - CARD_LOWEST) * (float)rng.NextDouble();
                float ring = MathF.Sqrt(1f - y * y);
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                Vector3 d = new(MathF.Cos(a) * ring, y, MathF.Sin(a) * ring);
                float proud = 0.85f + 0.3f * (float)rng.NextDouble();
                Vector3 at = centre + new Vector3(d.X * radius, d.Y * halfHeight, d.Z * radius) * proud;

                //The shell's normal (an ellipsoid's), and the card turned about it at random, tilted off it a little
                Vector3 normal = Vector3.Normalize(new Vector3(d.X / radius, d.Y / halfHeight, d.Z / radius));
                Vector3 tilted = Vector3.Normalize(normal + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f,
                    (float)rng.NextDouble() - 0.5f) * 0.9f);
                Vector3 reference = MathF.Abs(tilted.Y) < 0.9f ? Vector3.Up : Vector3.UnitX;
                Vector3 along = Vector3.Normalize(Vector3.Cross(tilted, reference));
                float turn = (float)rng.NextDouble() * MathHelper.TwoPi;
                along = Vector3.Normalize(along * MathF.Cos(turn) + Vector3.Cross(tilted, along) * MathF.Sin(turn));
                //along x across = tilted, so the card's front (the side its triangles face) is the shell's outside
                Vector3 across = Vector3.Cross(tilted, along);
                //Shaded mostly as the shell and a little as the card itself, so neighbouring twigs catch the light
                //differently and the clump is not one smooth ball of leaves
                Vector3 shading = Vector3.Normalize(normal * 0.65f + tilted * 0.35f);

                float scale = 0.75f + 0.5f * (float)rng.NextDouble();
                Vector3 stem = at - along * (size * scale * 0.5f);
                Vector3 tip = at + along * (size * scale * 0.5f);
                Vector3 half = across * (size * scale * 0.5f);

                float layerCode = 2f * (inner || y < -0.2f ? 2 : y > 0.25f ? 0 : 1);
                int b = v.Count;
                v.Add(new VertexPositionNormalTexture(stem - half, shading, new Vector2(0f, layerCode)));
                v.Add(new VertexPositionNormalTexture(stem + half, shading, new Vector2(0f, layerCode + 1f)));
                v.Add(new VertexPositionNormalTexture(tip + half, shading, new Vector2(1f, layerCode + 1f)));
                v.Add(new VertexPositionNormalTexture(tip - half, shading, new Vector2(1f, layerCode)));
                idx.Add(b); idx.Add(b + 1); idx.Add(b + 2);
                idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
            }
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Crown as IDisposable)?.Dispose();
            (Core as IDisposable)?.Dispose();
            (Leaves as IDisposable)?.Dispose();
        }
    }
}
