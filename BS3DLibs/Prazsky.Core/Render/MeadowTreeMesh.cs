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
    /// <b>Two crowns from one layout</b>, the acacia's split (#610): <see cref="Crown"/>, the clumps as solid lumps (the
    /// Low tier's), and at every other tier <b>the clumps as what they are, twigs with leaves</b> (#697):
    /// <see cref="Twigs"/>, a spray of thin twigs fanning out of every branch end through its clump to the shell, and
    /// <see cref="Leaves"/>, cards hung along those twigs by their stems that <c>Acacia.fx</c> cuts into sprigs of broad
    /// leaves (<c>BroadLeafMask</c>) and lights through.
    /// </para>
    /// <para>
    /// ⚠ <b>There is no solid core inside the leaves any more.</b> Until #697 the cards were laid round each clump's shell
    /// over a smaller lump, and the owner, close up: the leaves did not grow from the branches but were "stuck onto those
    /// balls" like scraps of paper - the balls should not be there at all, only branches with leaves, the lumps kept for the
    /// Low tier if that saves anything. So a card's stem now starts ON a twig, and the cards cast the crown's shade
    /// themselves, cut by their own leaves. The lump stayed a shadow-only caster for one merge, and a review found a third
    /// of the leaf area inside it: anything inside a caster is in the sun's shadow from every direction.
    /// </para>
    /// </summary>
    internal sealed class MeadowTreeMesh : IDisposable
    {
        /// <summary>
        /// The trunk, roots, limbs, boughs, leader and branches as Low, Medium and the Potato path draw them: swept tubes,
        /// every limb and the leader starting on the trunk's axis at the fork, every bough at its limb's end. Each tube is
        /// closed at both ends from its own rings (#889): an open end is a hole under back-face culling, and every limb's
        /// knee showed the sky through it. <see cref="FineWood"/> is the same tree at High and Ultra's detail.
        /// </summary>
        public IProceduralMesh Wood { get; }

        /// <summary>
        /// The same wood grown rather than assembled (#889, the acacia's #758 fork), for High and Ultra
        /// (<see cref="SceneRenderer.FineMeshes"/>): the trunk swells into a crotch shaped by the limbs it divides into,
        /// closed by a low dome whose saddles dip between them; every limb roots just inside it and leaves along its
        /// surface, the leader rises out of the dome on a flared foot, and every bough and branch grows out of its parent
        /// from inside it. The ends are <see cref="Wood"/>'s, so the twigs and leaves hang from either. <c>null</c> for a
        /// forest broadleaf (<see cref="ForForest"/>), which the forest's own renderer draws.
        /// </summary>
        public IProceduralMesh FineWood { get; }

        public IProceduralMesh Crown { get; }
        public IProceduralMesh Twigs { get; }
        public IProceduralMesh Leaves { get; }

        /// <summary>How far the crown reaches from the axis at scale 1.</summary>
        public float CrownReach { get; }

        /// <summary>The whole tree's height at scale 1, ground to the crown's top.</summary>
        public float Height { get; }

        /// <summary>The root flare's radius at scale 1.</summary>
        public float BaseRadius { get; }

        //The clumps' tessellation, coarser than a lone bush's (FoliageMesh's own 16 by 11): fifty-odd of them a tree
        private const int LUMP_SLICES = 10, LUMP_STACKS = 7;

        //The leaf cards: a card's side against its clump's radius (square, so the leaves keep their shape), how many
        //cards a clump takes (its shell's cover, before the leaves are cut out of them - a card is about a third leaf),
        //and the lowest point of the shell they cover (-1 the bottom pole)
        private const float CARD_SIZE = 0.7f, CARD_COVER = 1.5f, CARD_LOWEST = -1f;

        //A clump's spray (#697): how many twigs fan out of its branch end, how thick they leave it against the trunk, and
        //from how far along a twig its leaves start - the inner third is bare wood, as a twig's base is
        private const int TWIGS_MIN = 5, TWIGS_MAX = 8;
        private const float TWIG_RADIUS = 0.035f, LEAVES_FROM = 0.3f;

        //How much of a sprig's lean OUT through the clump's shell is taken back, so it lies along the shell as a leaf held
        //to the light does (#697's review): pointing straight out it kept no outward face once its own length was taken
        //out of the face, and a third of the cards faced into the clump and shaded dark on its sunny side
        private const float SPRIG_FLATTEN = 0.7f;

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
            : this(device, Build(kind, seed))
        {
        }

        /// <summary>
        /// A meadow tree as lists, with no device (#889): what the constructor uploads, and what a test reads.
        /// <paramref name="plugged"/> false leaves the coarse wood's tubes open as they were before #889 — only for the test
        /// that has to see its check find those holes.
        /// </summary>
        internal static MeadowTreeGeometry Build(MeadowTreeKind kind, int seed, bool plugged = true) =>
            Build(FigureOf(kind), seed, cards: true, LUMP_SLICES, LUMP_STACKS, plugged);

        /// <summary>
        /// A broadleaf of the daytime forest (#647), grown the same way to the forest's own figures: a tall bare
        /// bole to <paramref name="trunkHeight"/> under a crown <paramref name="crownRadius"/> wide and
        /// <paramref name="crownHeight"/> tall, its limbs steep as a forest-grown tree's are. <b>Wood and crown
        /// only</b> — the forest draws through <c>InstancedModel.fx</c>, which cuts no leaf cards, so its crown is
        /// the solid clumps, coarser still since a forest holds eighty of them. <see cref="Twigs"/> and
        /// <see cref="Leaves"/> are null.
        /// </summary>
        public static MeadowTreeMesh ForForest(GraphicsDevice device, float trunkRadius, float trunkHeight,
            float crownRadius, float crownHeight, int seed)
        {
            float height = trunkHeight + crownHeight;
            var figure = new Figure(height, trunkRadius, trunkHeight * 0.82f / height, crownRadius,
                (trunkHeight + crownHeight * 0.55f) / height, crownHeight * 0.48f / height,
                limbsMin: 3, limbsMax: 4, elevationMin: 50f, elevationMax: 72f,
                clumps: FOREST_CLUMPS, clumpMin: 0.24f, clumpMax: 0.32f, lean: 0.03f);
            return new MeadowTreeMesh(device, Build(figure, seed, cards: false, FOREST_LUMP_SLICES, FOREST_LUMP_STACKS, plugged: true));
        }

        //A forest broadleaf's clumps: fewer and coarser than a meadow tree's, eighty trees standing together
        private const int FOREST_CLUMPS = 30, FOREST_LUMP_SLICES = 8, FOREST_LUMP_STACKS = 6;

        private MeadowTreeMesh(GraphicsDevice device, MeadowTreeGeometry g)
        {
            CrownReach = g.CrownReach;
            Height = g.Height;
            BaseRadius = g.BaseRadius;
            Wood = new UploadedMesh(device, g.WoodVertices, g.WoodIndices, g.Bounds);
            Crown = new UploadedMesh(device, g.CrownVertices, g.CrownIndices, g.Bounds);
            if (g.HasFineWood) FineWood = new UploadedMesh(device, g.FineWoodVertices, g.FineWoodIndices, g.Bounds);
            if (g.HasCards)
            {
                Twigs = new UploadedMesh(device, g.TwigVertices, g.TwigIndices, g.Bounds);
                Leaves = new UploadedMesh(device, g.LeafVertices, g.LeafIndices, g.Bounds);
            }
        }

        private static MeadowTreeGeometry Build(Figure f, int seed, bool cards, int lumpSlices, int lumpStacks, bool plugged)
        {
            var g = new MeadowTreeGeometry { HasCards = cards, HasFineWood = cards };
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

            //--- The wood, and beside it every roll the fine wood is grown from (#889): it is built after the coarse wood
            //from these alone and takes no roll of its own, so the coarse wood, the crowns and the leaves are what they were
            var wv = g.WoodVertices;
            var widx = g.WoodIndices;
            var grown = new GrownRolls();
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
            if (plugged)
                TubeGeometry.AddSweep(wv, widx, 12, stem, girth, capStart: true, capEnd: true);
            else
            {
                TubeGeometry.AddSweep(wv, widx, 12, stem, girth);
                TubeGeometry.AddCap(wv, widx, 12, top, trunk * 0.6f, top - fork);
            }
            grown.Trunk = trunk;
            grown.Stem = stem;
            grown.Girth = girth;
            grown.Fork = fork;
            grown.Knee = knee;

            //Surface roots: the old tree's foot spreads into the ground, a few buttresses diving in
            int roots = 5 + rng.Next(3);
            float rootPhase = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int i = 0; i < roots; i++)
            {
                float a = rootPhase + MathHelper.TwoPi * i / roots + 0.4f * ((float)rng.NextDouble() - 0.5f);
                Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
                float spread = trunk * (2.3f + 0.9f * (float)rng.NextDouble());
                Vector3 rootFrom = dir * (trunk * 0.7f) + new Vector3(0f, trunk * 0.9f, 0f), rootTo = dir * spread + new Vector3(0f, -0.45f, 0f);
                TubeGeometry.AddTube(wv, widx, 7, rootFrom, trunk * 0.42f, rootTo, trunk * 0.1f);
                //The tip, closed in AddTube's own frame (AddCap's basis for the same axis): it dives into the ground, but a
                //slope can bare it
                if (plugged) TubeGeometry.AddCap(wv, widx, 7, rootTo, trunk * 0.1f, rootTo - rootFrom);
                grown.Roots.Add((dir, rootFrom, rootTo));
            }

            //A bough from one point to another, bowing up a little on the way, as wood grown towards the light does: one
            //swept skin through a quarter, the middle and three quarters of the bow, closed at both ends from its own
            //rings (#889) - at a knee the next bough starts thinner than this one ends and at an angle to it, so an open end
            //showed the sky through the wood. Unplugged only for the test that must see that hole.
            void Bough(Vector3 from, float fromRadius, Vector3 to, float toRadius, int segments, float bow)
            {
                float rise = bow * Vector3.Distance(from, to);
                Vector3 At(float f) => Vector3.Lerp(from, to, f) + new Vector3(0f, rise * 4f * f * (1f - f), 0f);
                TubeGeometry.AddSweep(wv, widx, segments, new[] { from, At(0.25f), At(0.5f), At(0.75f), to },
                    new[] { fromRadius, MathHelper.Lerp(fromRadius, toRadius, 0.25f), MathHelper.Lerp(fromRadius, toRadius, 0.5f),
                        MathHelper.Lerp(fromRadius, toRadius, 0.75f), toRadius }, capStart: plugged, capEnd: plugged);
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
                Bough(fork, limbRadius, limbKnee, limbRadius * 0.75f, 9, LIMB_BOW);
                var grownLimb = new GrownLimb(limbKnee, limbRadius);
                grown.Limbs.Add(grownLimb);
                g.Knees.Add((limbKnee, limbRadius * 0.75f));

                //Out of every limb's knee, two or three boughs fanning over its sector to knees of their own
                int boughs = 2 + rng.Next(2);
                float sector = MathHelper.TwoPi / limbs;
                for (int b = 0; b < boughs; b++)
                {
                    float turn = bearing + sector * ((b + 0.5f) / boughs - 0.5f) * 0.9f + 0.2f * ((float)rng.NextDouble() - 0.5f);
                    float boughReach = reach * (0.5f + 0.15f * (float)rng.NextDouble());
                    float rise = (limbKnee.Y - fork.Y) * 0.6f + crownHalf * (0.05f + 0.35f * (float)rng.NextDouble());
                    Vector3 boughKnee = new Vector3(crownCentre.X + MathF.Cos(turn) * boughReach, limbKnee.Y + rise, crownCentre.Z + MathF.Sin(turn) * boughReach);
                    Bough(limbKnee, limbRadius * 0.62f, boughKnee, trunk * 0.2f, 7, BOUGH_BOW);
                    knees.Add((boughKnee, turn, trunk * 0.2f));
                    grownLimb.Boughs.Add(grown.Boughs.Count);
                    grown.Boughs.Add((limbKnee, limbRadius * 0.62f, boughKnee, trunk * 0.2f));
                    g.Knees.Add((boughKnee, trunk * 0.2f));
                }
            }
            //And a leader up the middle, for the clumps over the top
            Vector3 leaderTop = crownCentre + new Vector3(0f, crownHalf * 0.25f, 0f);
            Bough(fork, trunk * 0.5f, leaderTop, trunk * 0.22f, 8, LEADER_BOW);
            grown.LeaderTop = leaderTop;
            g.Knees.Add((leaderTop, trunk * 0.22f));

            //The branches: from the nearest knee by bearing into every clump, or off the leader over the middle
            var sprayBases = new List<Vector3>(clumps.Count);
            foreach ((Vector3 centre, float r, bool inner) in clumps)
            {
                Vector3 offset = centre - crownCentre;
                float horizontal = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
                Vector3 from;
                float fromRadius;
                int parent = -1;
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
                    parent = best;
                }

                //Into the clump to a third of its radius short of the centre - where its spray of twigs starts (#697)
                Vector3 to = centre - Vector3.Normalize(centre - from + new Vector3(0f, 1e-3f, 0f)) * (r * 0.33f);
                Bough(from, fromRadius, to, trunk * 0.06f, 5, BRANCH_BOW);
                //Capped: no lump hides the branch's end any more, and an open tube is a hole seen from inside the crown
                //(Bough closes it from its own ring since #889; this disc, turned against that ring, left slivers open)
                if (!plugged) TubeGeometry.AddCap(wv, widx, 5, to, trunk * 0.06f, to - from);
                sprayBases.Add(to);
                grown.Branches.Add((from, fromRadius, to, parent));
            }
            g.SprayBases.AddRange(sprayBases);
            if (cards) BuildFineWood(g, grown);

            //--- The crowns
            var cv = g.CrownVertices;
            var cidx = g.CrownIndices;
            var tv = g.TwigVertices;
            var tidx = g.TwigIndices;
            var lv = g.LeafVertices;
            var lidx = g.LeafIndices;
            Random leafRng = new(seed * 41 + 3);
            for (int i = 0; i < clumps.Count; i++)
            {
                (Vector3 centre, float r, bool inner) = clumps[i];
                FoliageMesh.Generate(cv, cidx, r, r * 0.8f, centre, seed * 17 + i, FoliageStyle.Crown, lumpSlices, lumpStacks);
                if (!cards) continue;
                AddSpray(tv, tidx, lv, lidx, sprayBases[i], centre, crownCentre, r, r * 0.8f, inner, trunk * TWIG_RADIUS, leafRng);
            }

            g.CrownReach = reach;
            g.Height = crownCentre.Y + crownHalf;
            g.BaseRadius = trunk * 1.55f;
            g.Trunk = trunk;
            g.Bounds = new(crownCentre - new Vector3(0f, crownHalf * 0.3f, 0f), MathF.Max(reach, crownY) * 1.15f);
            return g;
        }

        //=== The fine wood (#889): High and Ultra's, the acacia's grown fork (#758, AcaciaMesh.FineWood) on the meadow's
        //bent, leaning trunk. Its sides: the trunk's, a limb's, a bough's or the leader's, a branch's and a root's (the coarse
        //wood has 12, 9, 7 or 8, 5 and 7)
        private const int FINE_TRUNK_SIDES = 20, FINE_LIMB_SIDES = 14, FINE_BOUGH_SIDES = 10, FINE_BRANCH_SIDES = 6, FINE_ROOT_SIDES = 8;

        //The crotch: how far under the fork the trunk starts to swell and to take the limbs' shape, in fork radii, and never
        //nearer the trunk's bend than this share of the way from it to the fork; how much wider than its taper it is at
        //the fork; the dome closing it, in fork radii; and how deep a saddle between two limbs dips, as a share of the dome.
        //Higher and dipping less than the acacia's (0.35, 0.6): two or three heavy limbs leave wide gaps between their root
        //circles, and at the acacia's figures the trunk's face between two of them ended in a ledge at the fork
        private const float CROTCH_HEIGHT = 1.8f, CROTCH_FLOOR = 0.25f, CROTCH_SWELL = 1.18f, CROTCH_DOME = 0.6f, SADDLE_DIP = 0.35f;

        //A limb's root ring: this many fork radii under the fork, its outer side this share of the way out to the trunk's
        //surface (flush, two coincident surfaces fight for the depth); how far it leans out of the trunk's own surface
        //(radians), so that it comes out through the trunk's side under the fork and covers the rim - leaving along the
        //surface, as the acacia's boughs do, it fell behind the crotch's swell, which is fastest at the fork, and the rim
        //stood round it as a ledge, or flush with it and fighting for the depth; how far its first control point stands
        //out, as a share of the limb's length - the run over which its underside carries on the trunk's outline before it
        //turns out, the references' flare under a low oak limb
        private const float LIMB_ROOT_DEPTH = 0.45f, LIMB_FLUSH = 0.97f, LIMB_SPLAY = 0.21f, LIMB_LEAVE = 0.22f;

        //How far out of the trunk's surface a limb's vertex still takes some of the trunk's normal, as a share of the limb's
        //radius: where the limb comes out through the trunk the two surfaces cross, and with their own normals the line
        //they cross on shaded as a crease (and the acacia's boughs a step lighter than the trunk), so the limb starts with
        //the trunk's shading and fades into its own
        private const float LIMB_BLEND = 0.35f;

        //...but only where the parent's surface faces the way the vertex's own does, within this cosine: in the crotch
        //between a piece and its parent the two face each other, and the parent's normal there shaded the piece from behind
        private const float BLEND_FACING = 0.3f;

        //An offshoot - a bough out of its limb, a branch out of its bough or the leader - roots this far along its parent's
        //curve, inside it, its outer side this share of the way out to the parent's surface on the side it leaves for, and
        //leaves along the parent's own axis for this share of its length: so the boughs part at a limb's end in saddles of
        //their own rather than starting square out of its cut end
        private const float OFFSHOOT_ROOT_AT = 0.86f, OFFSHOOT_FLUSH = 0.9f, OFFSHOOT_LEAVE = 0.3f;

        //The most an offshoot leaves off the straight way to its end (radians): past it the curve turns back on itself
        private const float OFFSHOOT_TURN = 1.05f;

        //An offshoot shorter than this many of its root radii is a stub and runs straight
        private const float OFFSHOOT_STUB = 4f;

        //How far a limb's, a bough's and the leader's ends bulge past their last ring, as a share of its radius: the knuckle
        //the offshoots leave round (at 0.7 it stood out of them as a ball)
        private const float KNEE_DOME = 0.45f;

        //The leader: it roots this many fork radii under the fork on the trunk's axis and flares by up to this share of the
        //trunk's radius over this share of its length, so it rises out of the dome on a collar and not as a pole through a lid
        private const float LEADER_ROOT_DEPTH = 0.25f, LEADER_FLARE = 0.35f, LEADER_FLARE_RUN = 0.1f;

        //Where the rings stand: the trunk's over the crotch and over the dome (as shares of each), and along a limb, an
        //offshoot, the leader and a branch - close together where each turns out of its parent
        private static readonly float[] CROTCH_RUN = { 0.25f, 0.5f, 0.75f, 1f };
        private static readonly float[] DOME_RUN = { 0.2f, 0.4f, 0.6f, 0.8f, 1f };
        private static readonly float[] LIMB_RUN = { 0f, 0.05f, 0.1f, 0.16f, 0.23f, 0.31f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f };
        private static readonly float[] OFFSHOOT_RUN = { 0f, 0.1f, 0.2f, 0.32f, 0.45f, 0.6f, 0.75f, 0.88f, 1f };
        private static readonly float[] LEADER_RUN = { 0f, 0.03f, 0.06f, 0.1f, 0.16f, 0.25f, 0.4f, 0.55f, 0.7f, 0.85f, 1f };
        private static readonly float[] BRANCH_RUN = { 0f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1f };

        //How far each piece of the coarse wood bows up on its way, as a share of its length (Bough's bow): a fine piece arrives
        //at the coarse one's end along the coarse one's direction there
        private const float LIMB_BOW = 0.1f, BOUGH_BOW = 0.08f, LEADER_BOW = 0.02f, BRANCH_BOW = 0.06f;

        /// <summary>What the coarse wood rolled, recorded for the fine wood (#889): the trunk's stem and girth, the roots, every
        /// limb with its boughs, the leader's top and every branch with the knee it leaves (-1 the leader).</summary>
        private sealed class GrownRolls
        {
            public float Trunk;
            public Vector3[] Stem;
            public float[] Girth;
            public Vector3 Fork, Knee, LeaderTop;
            public readonly List<(Vector3 Dir, Vector3 From, Vector3 To)> Roots = new();
            public readonly List<GrownLimb> Limbs = new();
            public readonly List<(Vector3 From, float Radius, Vector3 To, float EndRadius)> Boughs = new();
            public readonly List<(Vector3 From, float Radius, Vector3 To, int Parent)> Branches = new();
        }

        private sealed class GrownLimb(Vector3 knee, float radius)
        {
            public readonly Vector3 Knee = knee;
            public readonly float Radius = radius;
            public readonly List<int> Boughs = new();
        }

        /// <summary>A cubic Bezier: a fine piece's centreline.</summary>
        private readonly struct Curve(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            private readonly Vector3 p0 = a, p1 = b, p2 = c, p3 = d;

            public Vector3 At(float t)
            {
                float u = 1f - t;
                return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            }

            public Vector3 Tangent(float t)
            {
                float u = 1f - t;
                Vector3 slope = 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
                return slope.LengthSquared() > 1e-12f ? Vector3.Normalize(slope) : Vector3.Normalize(p3 - p0);
            }
        }

        /// <summary>
        /// The fine trunk's top as a surface (#889, <c>AcaciaMesh</c>'s <c>GrownTrunk</c> on a leaning axis): along the
        /// straight run from the trunk's bend (<c>h</c> 0) to the fork (1) it holds the coarse taper until
        /// <see cref="From"/>, then swells to <see cref="CROTCH_SWELL"/> times it and takes the shape of the limbs' root
        /// circles together (<see cref="Shape"/>) - each as wide as its limb, its outer side on the swollen circle - so a
        /// groove runs down from every saddle between two; over the fork (1 to 2) a low dome closes it, its saddles dipping
        /// between neighbours and fading to one apex. Angles are in the frame (<c>u</c>, <c>w</c>) square to the axis, the
        /// one the rings are laid in.
        /// </summary>
        private sealed class Crotch
        {
            private readonly Vector3 _knee, _fork, _axis, _u, _w;
            private readonly float _length, _kneeRadius, _midRadius, _forkRadius, _topRadius, _dome;
            private readonly (float Angle, float Radius)[] _roots;
            private readonly float[] _angles;   //sorted, in [0, 2pi)
            public readonly float From;

            public Crotch(Vector3 knee, Vector3 fork, Vector3 u, float kneeRadius, float midRadius, float forkRadius,
                IReadOnlyList<(Vector3 Toward, float Radius)> limbs)
            {
                _knee = knee;
                _fork = fork;
                _length = Vector3.Distance(knee, fork);
                _axis = (fork - knee) / _length;
                _u = Vector3.Normalize(u - _axis * Vector3.Dot(u, _axis));
                _w = Vector3.Cross(_axis, _u);
                _kneeRadius = kneeRadius;
                _midRadius = midRadius;
                _forkRadius = forkRadius;
                _topRadius = forkRadius * CROTCH_SWELL;
                _dome = CROTCH_DOME * forkRadius;
                From = MathF.Max(1f - CROTCH_HEIGHT * forkRadius / _length, CROTCH_FLOOR);
                _roots = new (float, float)[limbs.Count];
                _angles = new float[limbs.Count];
                for (int i = 0; i < limbs.Count; i++)
                {
                    _roots[i] = (AngleOf(limbs[i].Toward), limbs[i].Radius);
                    _angles[i] = Wrap(_roots[i].Angle);
                }
                Array.Sort(_angles);
            }

            public Vector3 Axis => _axis;
            public Vector3 U => _u;
            public Vector3 Fork => _fork;
            public float ForkRadius => _forkRadius;

            /// <summary>The profile parameter at a distance <paramref name="depth"/> under the fork along the axis.</summary>
            public float Under(float depth) => 1f - depth / _length;

            /// <summary>The frame's angle of a direction (its part square to the axis).</summary>
            public float AngleOf(Vector3 direction) => MathF.Atan2(Vector3.Dot(direction, _w), Vector3.Dot(direction, _u));

            public Vector3 Direction(float angle) => _u * MathF.Cos(angle) + _w * MathF.Sin(angle);

            public Vector3 Centre(float h) => _knee + _axis * (MathHelper.Clamp(h, 0f, 1f) * _length);

            /// <summary>How far off the axis a limb root circle this thick is centred: its outer side on the swollen circle.</summary>
            public float RootOffset(float radius) => MathF.Max(_topRadius - radius, 0f);

            private static float Wrap(float angle)
            {
                float a = angle % MathHelper.TwoPi;
                return a < 0f ? a + MathHelper.TwoPi : a;
            }

            /// <summary>+1 on a limb's angle, -1 midway between two neighbours, a cosine across each gap: where the saddles dip.</summary>
            private float Lobe(float angle)
            {
                int n = _angles.Length;
                if (n < 2) return 0f;
                float a = Wrap(angle);
                int k = n - 1;
                for (int i = 0; i < n; i++) if (_angles[i] <= a) k = i;
                float from = _angles[k];
                float to = k + 1 < n ? _angles[k + 1] : _angles[0] + MathHelper.TwoPi;
                if (a < from) a += MathHelper.TwoPi;
                return MathF.Cos(MathHelper.TwoPi * (a - from) / MathF.Max(to - from, 1e-4f));
            }

            /// <summary>The top ring's reach at an angle, as a share of the swollen circle: the outline of the limbs' root circles.</summary>
            private float Shape(float angle)
            {
                float reach = 0f;
                foreach ((float rootAngle, float radius) in _roots)
                {
                    float centre = RootOffset(radius);
                    float off = angle - rootAngle;
                    float across = centre * MathF.Sin(off);
                    if (MathF.Abs(across) > radius) continue;
                    reach = MathF.Max(reach, centre * MathF.Cos(off) + MathF.Sqrt(radius * radius - across * across));
                }
                return reach > 0f ? MathF.Min(reach / _topRadius, 1f) : 1f;
            }

            /// <summary>The coarse trunk's taper along the run: its girth at the bend, half way and at the fork.</summary>
            private float Cone(float h) => h < 0.5f
                ? MathHelper.Lerp(_kneeRadius, _midRadius, h * 2f)
                : MathHelper.Lerp(_midRadius, _forkRadius, (h - 0.5f) * 2f);

            /// <summary>The trunk's radius on its side (<paramref name="h"/> 0 to 1) at an angle: the crotch eases in from the
            /// taper with no crease and is still widening at the fork, so a limb leaving along it leans out with it.</summary>
            public float Radius(float h, float angle)
            {
                h = MathHelper.Clamp(h, 0f, 1f);
                float t = MathHelper.Clamp((h - From) / MathF.Max(1f - From, 1e-4f), 0f, 1f);
                float crotch = t * t;
                return Cone(h) * (1f + (CROTCH_SWELL - 1f) * crotch) * MathHelper.Lerp(1f, Shape(angle), crotch);
            }

            public Vector3 Point(float h, float angle)
            {
                Vector3 radial = Direction(angle);
                if (h <= 1f) return Centre(h) + radial * Radius(h, angle);
                float psi = MathHelper.Clamp(h - 1f, 0f, 1f) * MathHelper.PiOver2;
                float rise = MathF.Sin(psi);
                float height = _dome * (1f - SADDLE_DIP * 0.5f * (1f - Lobe(angle)) * (1f - rise));
                return _fork + radial * (Radius(1f, angle) * MathF.Cos(psi)) + _axis * (height * rise);
            }

            /// <summary>The surface's outward normal, from its two tangents; the axis at the dome's apex.</summary>
            public Vector3 Normal(float h, float angle)
            {
                const float STEP = 1e-3f;
                if (h >= 2f - 1e-4f) return _axis;
                Vector3 up = Point(MathF.Min(h + STEP, 2f), angle) - Point(MathF.Max(h - STEP, 0f), angle);
                Vector3 around = Point(h, angle + STEP) - Point(h, angle - STEP);
                Vector3 n = Vector3.Cross(around, up);
                if (n.LengthSquared() < 1e-12f) return _axis;
                n.Normalize();
                return Vector3.Dot(n, Direction(angle)) < 0f ? -n : n;
            }

            /// <summary>Up the trunk's side at a height and an angle, taken from below so that at the fork it is the side's.</summary>
            public Vector3 Upward(float h, float angle)
            {
                const float STEP = 1e-3f;
                return Vector3.Normalize(Point(h, angle) - Point(h - STEP, angle));
            }

            /// <summary>
            /// The surface's place nearest a point, roughly (its profile parameter and angle, over the dome as well), and how
            /// far out of it the point stands (negative inside): measured square to the axis on the side and the dome's
            /// flanks, along it over the dome's top. The saddles' dip is left out; it is for blending a normal by.
            /// </summary>
            public (float H, float Angle, float Outside) Surface(Vector3 p)
            {
                float along = Vector3.Dot(p - _knee, _axis) / _length;
                float angle = AngleOf(p - _fork);
                float off = (p - _knee - _axis * (along * _length)).Length();
                if (along <= 1f) return (MathF.Max(along, 0f), angle, off - Radius(along, angle));
                float rise = (along - 1f) * _length / _dome;
                if (rise >= 1f) return (2f, angle, (along - 1f) * _length - _dome + off);
                float psi = MathF.Asin(rise);
                return (1f + psi / MathHelper.PiOver2, angle, off - Radius(1f, angle) * MathF.Cos(psi));
            }

            /// <summary>Whether a point is inside the grown trunk between its bend and the dome's top.</summary>
            public bool Inside(Vector3 p, float tolerance)
            {
                float along = Vector3.Dot(p - _knee, _axis) / _length;
                if (along < 0f || along > 1f + _dome / _length) return false;
                float angle = AngleOf(p - _fork);
                Vector3 off = p - _knee - _axis * (along * _length);
                if (along <= 1f) return off.Length() <= Radius(along, angle) + tolerance;
                //Over the fork: under the dome's surface at this angle (its height off the profile, its reach shrinking)
                float rise = MathHelper.Clamp((along - 1f) * _length / _dome, 0f, 1f);
                float psi = MathF.Asin(rise);
                return off.Length() <= Radius(1f, angle) * MathF.Cos(psi) + tolerance;
            }
        }

        /// <summary>
        /// The fine wood (#889) into <paramref name="g"/>'s lists, from the coarse wood's rolls alone: the grown trunk
        /// (<see cref="Crotch"/>), the roots as bent runs out of the flare, every limb on a curve out of the crotch to the
        /// coarse limb's knee, every bough out of its limb and every branch out of its bough or the leader to the same ends
        /// as the coarse wood's, and the leader on its collar. Every piece is closed: the limbs, boughs and leader in a
        /// knuckle, the branches and roots flat, the trunk's foot flat.
        /// </summary>
        private static void BuildFineWood(MeadowTreeGeometry g, GrownRolls r)
        {
            List<VertexPositionNormalTexture> v = g.FineWoodVertices;
            List<short> idx = g.FineWoodIndices;
            List<(Vector3 From, Vector3 To)> skeleton = g.FineSkeleton;
            float trunk = r.Trunk;
            Vector3 fork = r.Fork, knee = r.Knee;

            //--- The trunk's lower stations, the coarse stem's up to its bend, in AddSweep's frames
            const int LOWER = 4;   //the ground, the flare's foot, the flare's top, half way to the bend; then the bend
            var stem = r.Stem;
            Vector3 axis0 = Vector3.Normalize(stem[1] - stem[0]);
            Vector3 u = Vector3.Normalize(Vector3.Cross(MathF.Abs(axis0.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY, axis0));
            var lowerFrames = new (Vector3 Axis, Vector3 U)[LOWER + 1];
            for (int k = 0; k <= LOWER; k++)
            {
                Vector3 axis = Vector3.Normalize(stem[k + 1] - stem[Math.Max(k - 1, 0)]);
                u = Vector3.Normalize(u - axis * Vector3.Dot(u, axis));
                lowerFrames[k] = (axis, u);
            }

            var limbs = new List<(Vector3 Toward, float Radius)>();
            foreach (GrownLimb limb in r.Limbs) limbs.Add((limb.Knee - fork, limb.Radius));
            var crotch = new Crotch(knee, fork, lowerFrames[LOWER].U, r.Girth[4], r.Girth[5], r.Girth[6], limbs);
            Vector3 trunkAxis = crotch.Axis;

            //The profile's stations over the bend (whose own ring is the lower run's last, square to the bend's mean as a
            //sweep's is: square to the run above, it folded against the ring under it on a bent trunk): the coarse half-way
            //ring if the crotch starts above it, the crotch, the dome
            var stations = new List<float>();
            if (crotch.From > 0.5f) stations.Add(0.5f);
            stations.Add(crotch.From);
            foreach (float t in CROTCH_RUN) stations.Add(crotch.From + (1f - crotch.From) * t);
            foreach (float t in DOME_RUN) stations.Add(1f + t);

            const int SIDES = FINE_TRUNK_SIDES;
            short trunkBase = (short)v.Count;
            for (int k = 0; k <= LOWER; k++)
            {
                (Vector3 axis, Vector3 ku) = lowerFrames[k];
                Vector3 kw = Vector3.Cross(axis, ku);
                //The cone's normal: radial, tipped back by the taper to the next station
                Vector3 prev = stem[Math.Max(k - 1, 0)], next = stem[k + 1];
                float slope = (r.Girth[k + 1] - r.Girth[Math.Max(k - 1, 0)]) / MathF.Max(Vector3.Distance(next, prev), 1e-4f);
                for (int s = 0; s <= SIDES; s++)
                {
                    float ang = MathHelper.TwoPi * s / SIDES;
                    Vector3 dir = ku * MathF.Cos(ang) + kw * MathF.Sin(ang);
                    v.Add(new VertexPositionNormalTexture(stem[k] + dir * r.Girth[k], Vector3.Normalize(dir - axis * slope),
                        new Vector2(s / (float)SIDES, 0f)));
                }
            }
            foreach (float h in stations)
            {
                for (int s = 0; s <= SIDES; s++)
                {
                    float ang = MathHelper.TwoPi * s / SIDES;
                    v.Add(new VertexPositionNormalTexture(crotch.Point(h, ang), crotch.Normal(h, ang), new Vector2(s / (float)SIDES, h * 0.5f)));
                }
            }
            int trunkRings = LOWER + 1 + stations.Count;
            for (int k = 0; k + 1 < trunkRings; k++)
            {
                int ring = trunkBase + k * (SIDES + 1), next = ring + SIDES + 1;
                for (int s = 0; s < SIDES; s++)
                {
                    idx.Add((short)(ring + s)); idx.Add((short)(next + s)); idx.Add((short)(ring + s + 1));
                    idx.Add((short)(ring + s + 1)); idx.Add((short)(next + s)); idx.Add((short)(next + s + 1));
                }
            }
            TubeGeometry.AddFan(v, idx, trunkBase, SIDES, stem[0], -lowerFrames[0].Axis, reversed: true);
            for (int k = 0; k < LOWER; k++) skeleton.Add((stem[k], stem[k + 1]));
            skeleton.Add((knee, fork));
            g.InsideFineTrunk = p => crotch.Inside(p, trunk * 0.02f);
            for (int s = 0; s < SIDES; s++)
            {
                float ang = MathHelper.TwoPi * s / SIDES;
                g.FineTrunkRim.Add((crotch.Point(1f, ang), ang));
            }

            //--- The roots: one bent run each out of the flare, from inside it, over the coarse root's line to its tip
            foreach ((Vector3 dir, Vector3 from, Vector3 to) in r.Roots)
            {
                Vector3 inside = dir * (trunk * 0.35f) + new Vector3(0f, trunk * 1.25f, 0f);
                Vector3 middle = Vector3.Lerp(from, to, 0.5f) + new Vector3(0f, Vector3.Distance(from, to) * 0.08f, 0f);
                var points = new[] { inside, from, middle, to };
                TubeGeometry.AddSweep(v, idx, FINE_ROOT_SIDES, points, new[] { trunk * 0.45f, trunk * 0.42f, trunk * 0.25f, trunk * 0.1f },
                    capStart: true, capEnd: true);
                for (int p = 0; p + 1 < points.Length; p++) skeleton.Add((points[p], points[p + 1]));
            }

            //--- The limbs out of the crotch, each with its boughs out of its end
            var boughCurves = new Curve[r.Boughs.Count];
            float limbRootH = crotch.Under(LIMB_ROOT_DEPTH * crotch.ForkRadius);
            foreach (GrownLimb limb in r.Limbs)
            {
                float angle = crotch.AngleOf(limb.Knee - fork);
                float offset = MathF.Min(crotch.RootOffset(limb.Radius), crotch.Radius(limbRootH, angle) - limb.Radius);
                Vector3 root = crotch.Centre(limbRootH) + crotch.Direction(angle) * MathF.Max(offset, 0f);
                float length = Vector3.Distance(root, limb.Knee);
                Vector3 arriving = Arriving(fork, limb.Knee, LIMB_BOW);
                Vector3 upward = crotch.Upward(limbRootH, angle);
                Vector3 outward = crotch.Direction(angle);
                outward = Vector3.Normalize(outward - upward * Vector3.Dot(outward, upward));
                Vector3 leave = upward * MathF.Cos(LIMB_SPLAY) + outward * MathF.Sin(LIMB_SPLAY);
                var curve = new Curve(root, root + leave * (length * LIMB_LEAVE), limb.Knee - arriving * (length * 0.33f), limb.Knee);
                int firstRing = v.Count;
                Sweep(v, idx, skeleton, curve, LIMB_RUN, limb.Radius * LIMB_FLUSH, limb.Radius * 0.75f, FINE_LIMB_SIDES, KNEE_DOME);
                int limbVertices = SideVertices(LIMB_RUN, FINE_LIMB_SIDES, KNEE_DOME);
                for (int s = 0; s < FINE_LIMB_SIDES; s++) g.FineLimbRoots.Add(v[firstRing + s].Position);
                {
                    var points = new Vector3[LIMB_RUN.Length];
                    var radii = new float[LIMB_RUN.Length];
                    for (int p = 0; p < points.Length; p++)
                    {
                        points[p] = curve.At(LIMB_RUN[p]);
                        radii[p] = LimbRadius(limb.Radius, LIMB_RUN[p]);
                    }
                    g.FineLimbs.Add((points, radii));
                    float centre = crotch.RootOffset(limb.Radius);
                    g.FineLimbBearings.Add((angle, centre > limb.Radius ? MathF.Asin(limb.Radius / centre) : MathHelper.Pi));
                }

                //Shaded as the trunk where it comes out of it, fading into its own over LIMB_BLEND of its radius (#758's
                //lesson, by distance here rather than by ring: the limb crosses the trunk's side at a slant)
                for (int i = 0; i < limbVertices; i++)
                {
                    VertexPositionNormalTexture vertex = v[firstRing + i];
                    (float h, float a, float outside) = crotch.Surface(vertex.Position);
                    float own = BlendOwn(outside, limb.Radius * LIMB_BLEND);
                    if (own >= 1f) continue;
                    Vector3 trunkNormal = crotch.Normal(h, a);
                    if (Vector3.Dot(trunkNormal, vertex.Normal) < BLEND_FACING) continue;
                    vertex.Normal = Vector3.Normalize(Vector3.Lerp(trunkNormal, vertex.Normal, own));
                    v[firstRing + i] = vertex;
                }

                float limbAt = LimbRadius(limb.Radius, OFFSHOOT_ROOT_AT);
                foreach (int b in limb.Boughs)
                {
                    (Vector3 from, float radius, Vector3 to, float endRadius) = r.Boughs[b];
                    boughCurves[b] = Offshoot(v, idx, skeleton, curve, OFFSHOOT_ROOT_AT, limbAt, from, to, radius, endRadius,
                        BOUGH_BOW, OFFSHOOT_RUN, FINE_BOUGH_SIDES, KNEE_DOME);
                }
            }

            //--- The leader, out of the dome on its collar: its foot never wider than the trunk is anywhere round it there
            float leaderRootH = crotch.Under(LEADER_ROOT_DEPTH * crotch.ForkRadius);
            float narrowest = float.MaxValue;
            for (int s = 0; s < 72; s++) narrowest = MathF.Min(narrowest, crotch.Radius(leaderRootH, MathHelper.TwoPi * s / 72f));
            float leaderFoot = trunk * 0.5f, leaderEnd = trunk * 0.22f;
            float flare = MathHelper.Clamp(narrowest * 0.92f - leaderFoot, 0f, LEADER_FLARE * trunk);
            Vector3 leaderRoot = crotch.Centre(leaderRootH);
            float leaderLength = Vector3.Distance(leaderRoot, r.LeaderTop);
            Vector3 leaderArriving = Arriving(fork, r.LeaderTop, LEADER_BOW);
            var leader = new Curve(leaderRoot, leaderRoot + trunkAxis * (leaderLength * 0.3f),
                r.LeaderTop - leaderArriving * (leaderLength * 0.3f), r.LeaderTop);
            float LeaderRadius(float t)
            {
                float collar = MathF.Max(1f - t / LEADER_FLARE_RUN, 0f);
                return MathHelper.Lerp(leaderFoot, leaderEnd, t) + flare * collar * collar;
            }
            {
                var points = new Vector3[LEADER_RUN.Length];
                var radii = new float[LEADER_RUN.Length];
                for (int p = 0; p < points.Length; p++)
                {
                    points[p] = leader.At(LEADER_RUN[p]);
                    radii[p] = LeaderRadius(LEADER_RUN[p]);
                }
                TubeGeometry.AddSweep(v, idx, FINE_BOUGH_SIDES, points, radii, capStart: true, capEnd: true, endDome: KNEE_DOME);
                for (int p = 0; p + 1 < points.Length; p++) skeleton.Add((points[p], points[p + 1]));
            }

            //--- The branches: out of their bough near its knee, or out of the leader where the coarse one left it, each to
            //the coarse branch's end, where its twigs start (#697)
            foreach ((Vector3 from, float radius, Vector3 to, int parent) in r.Branches)
            {
                if (parent >= 0)
                {
                    (_, float boughRadius, _, float boughEnd) = r.Boughs[parent];
                    float at = MathHelper.Lerp(boughRadius, boughEnd, OFFSHOOT_ROOT_AT);
                    Offshoot(v, idx, skeleton, boughCurves[parent], OFFSHOOT_ROOT_AT, at, from, to, radius, trunk * 0.06f,
                        BRANCH_BOW, BRANCH_RUN, FINE_BRANCH_SIDES, 0f);
                }
                else
                {
                    //The point of the fine leader nearest the coarse branch's start
                    float best = 0f, bestGap = float.MaxValue;
                    for (int s = 0; s <= 64; s++)
                    {
                        float gap = Vector3.DistanceSquared(leader.At(s / 64f), from);
                        if (gap < bestGap) { bestGap = gap; best = s / 64f; }
                    }
                    Offshoot(v, idx, skeleton, leader, best, LeaderRadius(best), from, to, radius, trunk * 0.06f,
                        BRANCH_BOW, BRANCH_RUN, FINE_BRANCH_SIDES, 0f);
                }
            }

            float LimbRadius(float limbRadius, float t) => MathHelper.Lerp(limbRadius * LIMB_FLUSH, limbRadius * 0.75f, t);
        }

        /// <summary>The direction a coarse piece from <paramref name="from"/> to <paramref name="to"/> arrives in, bowing up
        /// by <paramref name="bow"/> of its length as <c>Bough</c> bows it: the fine piece arrives the same way.</summary>
        private static Vector3 Arriving(Vector3 from, Vector3 to, float bow)
        {
            float rise = bow * Vector3.Distance(from, to);
            return Vector3.Normalize(to - from - new Vector3(0f, 4f * rise, 0f));
        }

        /// <summary>A curve swept at the given stations from one radius to another, closed at its start (inside its parent)
        /// and at its end, in a knuckle <paramref name="dome"/> of its radius high or (0) flat.</summary>
        private static void Sweep(List<VertexPositionNormalTexture> v, List<short> idx, List<(Vector3 From, Vector3 To)> skeleton,
            Curve curve, float[] run, float fromRadius, float toRadius, int sides, float dome)
        {
            var points = new Vector3[run.Length];
            var radii = new float[run.Length];
            for (int p = 0; p < points.Length; p++)
            {
                points[p] = curve.At(run[p]);
                radii[p] = MathHelper.Lerp(fromRadius, toRadius, run[p]);
            }
            TubeGeometry.AddSweep(v, idx, sides, points, radii, capStart: true, capEnd: true, endDome: dome);
            for (int p = 0; p + 1 < points.Length; p++) skeleton.Add((points[p], points[p + 1]));
        }

        /// <summary>
        /// An offshoot out of its parent's curve at <paramref name="along"/> (where the parent is <paramref name="parentRadius"/>
        /// thick): rooted inside it, set off its axis towards where it is going so its outer side is nearly the parent's,
        /// leaving along the parent's own axis and turning on one Bezier to arrive at the coarse piece's end the way the
        /// coarse piece (from <paramref name="coarseFrom"/>) does. The parent's end bulges round the offshoots leaving it.
        /// </summary>
        private static Curve Offshoot(List<VertexPositionNormalTexture> v, List<short> idx, List<(Vector3 From, Vector3 To)> skeleton,
            Curve parent, float along, float parentRadius, Vector3 coarseFrom, Vector3 to, float rootRadius, float endRadius,
            float bow, float[] run, int sides, float dome)
        {
            Vector3 at = parent.At(along), leave = parent.Tangent(along);
            Vector3 toward = to - at;
            Vector3 lateral = toward - leave * Vector3.Dot(toward, leave);
            Vector3 root = lateral.LengthSquared() > 1e-8f
                ? at + Vector3.Normalize(lateral) * (MathF.Max(parentRadius - rootRadius, 0f) * OFFSHOOT_FLUSH)
                : at;
            float length = Vector3.Distance(root, to);
            //Leaving and arriving no more than OFFSHOOT_TURN off the way to its end: a branch to a clump behind its knee,
            //leaving along the bough and turning back (or arriving the coarse branch's way from a knee past its clump),
            //folded its rings through each other. A stub - a clump next to its knee - runs straight: bent, a tube shorter
            //than a few of its radii folds whatever it does.
            Vector3 chord = (to - root) / MathF.Max(length, 1e-5f);
            Vector3 arriving = Arriving(coarseFrom, to, bow);
            if (length < OFFSHOOT_STUB * rootRadius) leave = arriving = chord;
            leave = WithinTurn(leave, chord);
            arriving = WithinTurn(arriving, chord);
            var curve = new Curve(root, root + leave * (length * OFFSHOOT_LEAVE), to - arriving * (length * 0.33f), to);
            int first = v.Count;
            Sweep(v, idx, skeleton, curve, run, rootRadius, endRadius, sides, dome);
            int last = first + SideVertices(run, sides, dome);

            //Shaded as its parent where it comes out of it, fading into its own, as a limb out of the trunk (LIMB_BLEND):
            //the parent as a tube round the stretch of its curve the offshoot leaves from, to its end
            Span<Vector3> centres = stackalloc Vector3[PARENT_SAMPLES];
            for (int k = 0; k < PARENT_SAMPLES; k++)
                centres[k] = parent.At(along + (1f - along) * k / (PARENT_SAMPLES - 1f) - 0.1f * (1f - k / (PARENT_SAMPLES - 1f)));
            for (int i = first; i < last; i++)
            {
                VertexPositionNormalTexture vertex = v[i];
                Vector3 nearest = centres[0];
                float gap = float.MaxValue;
                for (int k = 0; k + 1 < PARENT_SAMPLES; k++)
                {
                    Vector3 span = centres[k + 1] - centres[k];
                    float t = MathHelper.Clamp(Vector3.Dot(vertex.Position - centres[k], span) / MathF.Max(span.LengthSquared(), 1e-12f), 0f, 1f);
                    Vector3 c = centres[k] + span * t;
                    float d = Vector3.DistanceSquared(vertex.Position, c);
                    if (d < gap) { gap = d; nearest = c; }
                }
                float own = BlendOwn(MathF.Sqrt(gap) - parentRadius, rootRadius * LIMB_BLEND);
                if (own >= 1f || gap < 1e-12f) continue;
                Vector3 parentNormal = Vector3.Normalize(vertex.Position - nearest);
                if (Vector3.Dot(parentNormal, vertex.Normal) < BLEND_FACING) continue;
                vertex.Normal = Vector3.Normalize(Vector3.Lerp(parentNormal, vertex.Normal, own));
                v[i] = vertex;
            }
            return curve;
        }

        /// <summary><paramref name="direction"/>, turned towards <paramref name="chord"/> until it is at most
        /// <see cref="OFFSHOOT_TURN"/> off it.</summary>
        private static Vector3 WithinTurn(Vector3 direction, Vector3 chord)
        {
            float cos = Vector3.Dot(direction, chord);
            if (cos >= MathF.Cos(OFFSHOOT_TURN)) return direction;
            Vector3 across = direction - chord * cos;
            if (across.LengthSquared() < 1e-10f) return chord;
            return chord * MathF.Cos(OFFSHOOT_TURN) + Vector3.Normalize(across) * MathF.Sin(OFFSHOOT_TURN);
        }

        //How many points of its parent's curve an offshoot's shading is measured against
        private const int PARENT_SAMPLES = 6;

        /// <summary>The vertices of a sweep's side rings (its rounded end's among them), which come first: not the discs
        /// closing it, whose normals are their own facing.</summary>
        private static int SideVertices(float[] run, int sides, float dome) =>
            (run.Length + (dome > 0f ? TubeGeometry.DOME_RINGS : 0)) * (sides + 1);

        /// <summary>
        /// How much of its own normal a vertex <paramref name="outside"/> its parent's surface keeps: none at the surface,
        /// all from <paramref name="band"/> out, smoothly between - and all of it deeper inside the parent than the band,
        /// where nothing is seen and the parent's normal can face anywhere against the piece's own surface.
        /// </summary>
        private static float BlendOwn(float outside, float band)
        {
            if (outside < -band) return 1f;
            float own = MathHelper.Clamp(outside / band, 0f, 1f);
            return own * own * (3f - 2f * own);
        }

        /// <summary>
        /// One clump as twigs with leaves (#697): <see cref="TWIGS_MIN"/> to <see cref="TWIGS_MAX"/> twigs fan out of
        /// its branch end at <paramref name="basePoint"/> to points spread over its shell - the side facing out of the
        /// crown and up first, as a spray reaches for the light - each a thin swept tube bowing up on its way, and the
        /// clump's cards hung along their outer two thirds, every card's stem on the twig (a card's <c>u</c> = 0 end is
        /// its stem's base, <c>Acacia.fx</c>'s <c>BroadLeafMask</c>) and its sprig leaving it forwards, turned round the
        /// twig leaf by leaf. The card count is the shell's cover it had, so the leaf mass and the cost are what they were.
        /// The shading normal is still mostly the clump's shell, so a spray shades as one rounded mass of leaves, lit on
        /// top and dark beneath; the layer code the same as the shell's (top, flanks, underside and inside).
        /// <para>
        /// A meadow shrub's clumps are the same sprays (<see cref="MeadowShrubMesh"/>, #890), denser and finer: <paramref name="cover"/>
        /// is the shell's cover, <paramref name="cardSize"/> a card's side against the clump's radius and
        /// <paramref name="twigsMin"/> to <paramref name="twigsMax"/> how many twigs carry them, a tree's <see cref="CARD_COVER"/>,
        /// <see cref="CARD_SIZE"/> and <see cref="TWIGS_MIN"/> to <see cref="TWIGS_MAX"/> unless a caller states its own.
        /// </para>
        /// </summary>
        internal static void AddSpray(List<VertexPositionNormalTexture> tv, List<short> tidx,
            List<VertexPositionNormalTexture> lv, List<int> lidx, Vector3 basePoint, Vector3 centre, Vector3 crownCentre,
            float radius, float halfHeight, bool inner, float twigRadius, Random rng, float cover = CARD_COVER, float cardSize = CARD_SIZE,
            int twigsMin = TWIGS_MIN, int twigsMax = TWIGS_MAX)
        {
            float size = radius * cardSize;
            int cardsLeft = (int)(cover * MathHelper.TwoPi * radius * radius * (1f - CARD_LOWEST) / (size * size));
            int twigs = twigsMin + rng.Next(twigsMax - twigsMin + 1);

            //Out of the crown and a little up: the side of the shell the twigs reach for first
            Vector3 outward = centre - crownCentre;
            outward.Y = MathF.Max(outward.Y, 0f) + radius;
            outward = Vector3.Normalize(outward);

            float golden = MathF.PI * (3f - MathF.Sqrt(5f));
            float spin = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int t = 0; t < twigs; t++)
            {
                //The tip: spread evenly over the shell's cap facing outward (a cosine from 1 down to -0.35 off the outward
                //direction, area-even, on the golden angle), shaken a little, and out to the shell give or take
                float c = 1f - 1.35f * (t + 0.5f) / twigs;
                float s = MathF.Sqrt(MathF.Max(1f - c * c, 0f));
                float a = spin + golden * t + 0.4f * ((float)rng.NextDouble() - 0.5f);
                Vector3 side = Vector3.Normalize(Vector3.Cross(outward, MathF.Abs(outward.Y) < 0.95f ? Vector3.Up : Vector3.UnitX));
                Vector3 side2 = Vector3.Cross(outward, side);
                Vector3 d = outward * c + (side * MathF.Cos(a) + side2 * MathF.Sin(a)) * s;
                float reach = 0.85f + 0.25f * (float)rng.NextDouble();
                Vector3 tip = centre + new Vector3(d.X * radius, d.Y * halfHeight, d.Z * radius) * reach;

                //Bowing up on the way, as wood grown towards the light does, through a middle point shaken off the chord
                float length = Vector3.Distance(basePoint, tip);
                Vector3 jitter = new((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                Vector3 mid = Vector3.Lerp(basePoint, tip, 0.5f) + new Vector3(0f, length * 0.12f, 0f) + jitter * (length * 0.15f);
                Vector3 At(float f) => (1f - f) * (1f - f) * basePoint + 2f * f * (1f - f) * mid + f * f * tip;
                Vector3 TangentAt(float f) => Vector3.Normalize(2f * (1f - f) * (mid - basePoint) + 2f * f * (tip - mid));

                TubeGeometry.AddSweep(tv, tidx, 4, new[] { basePoint, At(0.33f), At(0.67f), tip },
                    new[] { twigRadius, twigRadius * 0.75f, twigRadius * 0.5f, twigRadius * 0.25f });

                //This twig's share of the clump's cards, along its outer part, turned round it leaf by leaf
                int cards = cardsLeft / (twigs - t);
                cardsLeft -= cards;
                float roll = (float)rng.NextDouble() * MathHelper.TwoPi;
                for (int k = 0; k < cards; k++)
                {
                    float f = LEAVES_FROM + (1f - LEAVES_FROM) * (k + 0.3f + 0.4f * (float)rng.NextDouble()) / cards;
                    Vector3 at = At(f);
                    Vector3 tangent = TangentAt(f);
                    Vector3 across = Vector3.Normalize(Vector3.Cross(tangent, MathF.Abs(tangent.Y) < 0.95f ? Vector3.Up : Vector3.UnitX));
                    Vector3 across2 = Vector3.Cross(tangent, across);
                    float turn = roll + golden * k + 0.3f * ((float)rng.NextDouble() - 0.5f);
                    Vector3 outFromTwig = across * MathF.Cos(turn) + across2 * MathF.Sin(turn);

                    //The sprig leaves the twig forwards, at forty-odd degrees, and the last one carries straight on
                    float lean = k == cards - 1 ? 0.25f : 0.8f + 0.3f * (float)rng.NextDouble();
                    Vector3 along = Vector3.Normalize(tangent + outFromTwig * lean);
                    Vector3 fromCentre = at - centre;
                    Vector3 shellAt = Vector3.Normalize(new Vector3(fromCentre.X / radius, fromCentre.Y / halfHeight, fromCentre.Z / radius)
                        + new Vector3(0f, 1e-4f, 0f));
                    along = Vector3.Normalize(along - shellAt * (Vector3.Dot(along, shellAt) * SPRIG_FLATTEN));

                    //The card's face towards the shell's outside and the sky, turned square to its own length
                    float scale = 0.75f + 0.5f * (float)rng.NextDouble();
                    Vector3 middle = at + along * (size * scale * 0.5f);
                    Vector3 o = middle - centre;
                    Vector3 shell = Vector3.Normalize(new Vector3(o.X / radius, o.Y / halfHeight, o.Z / radius) + new Vector3(0f, 1e-4f, 0f));
                    Vector3 face = shell * 0.6f + Vector3.Up * 0.4f
                        + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.6f;
                    face -= Vector3.Dot(face, along) * along;
                    if (face.LengthSquared() < 1e-6f) face = outFromTwig - Vector3.Dot(outFromTwig, along) * along;
                    face = Vector3.Normalize(face);

                    //The front out of the clump whatever the jitter did: Acacia.fx shades a card from behind by its flipped
                    //normal, so a front turned in would be a dark leaf on the sunny side (#697's review)
                    if (Vector3.Dot(face, shell) < 0f) face = -face;

                    //face x along = across, so along x across = face: the card's front is the side the face points to
                    Vector3 width = Vector3.Cross(face, along);
                    Vector3 shading = Vector3.Normalize(shell * 0.65f + face * 0.35f);
                    Vector3 half = width * (size * scale * 0.5f);
                    Vector3 stem = at;
                    Vector3 end = at + along * (size * scale);

                    float height = o.Y / halfHeight;
                    float layerCode = 2f * (inner || height < -0.2f ? 2 : height > 0.25f ? 0 : 1);
                    int b = lv.Count;
                    lv.Add(new VertexPositionNormalTexture(stem - half, shading, new Vector2(0f, layerCode)));
                    lv.Add(new VertexPositionNormalTexture(stem + half, shading, new Vector2(0f, layerCode + 1f)));
                    lv.Add(new VertexPositionNormalTexture(end + half, shading, new Vector2(1f, layerCode + 1f)));
                    lv.Add(new VertexPositionNormalTexture(end - half, shading, new Vector2(1f, layerCode)));
                    lidx.Add(b); lidx.Add(b + 1); lidx.Add(b + 2);
                    lidx.Add(b); lidx.Add(b + 2); lidx.Add(b + 3);
                }
            }
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (FineWood as IDisposable)?.Dispose();
            (Crown as IDisposable)?.Dispose();
            (Twigs as IDisposable)?.Dispose();
            (Leaves as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// A meadow tree as lists, before any device (#889): <see cref="MeadowTreeMesh"/>'s build, which the mesh uploads and a
    /// test reads. The fine wood is there for a meadow tree only (<see cref="HasFineWood"/>), not a forest broadleaf.
    /// </summary>
    internal sealed class MeadowTreeGeometry
    {
        public bool HasCards, HasFineWood;
        public readonly List<VertexPositionNormalTexture> WoodVertices = new(), FineWoodVertices = new(), CrownVertices = new(),
            TwigVertices = new(), LeafVertices = new();
        public readonly List<short> WoodIndices = new(), FineWoodIndices = new(), CrownIndices = new(), TwigIndices = new();
        public readonly List<int> LeafIndices = new();
        public BoundingSphere Bounds;
        public float CrownReach, Height, BaseRadius, Trunk;

        /// <summary>Every point the coarse wood forks again at, with the radius the wood ends in there: each limb's knee, each
        /// bough's, and the leader's top. Where the coarse wood's open ends were (#889).</summary>
        public readonly List<(Vector3 At, float Radius)> Knees = new();

        /// <summary>Every branch's end, where its clump's twigs start (#697): the same in both woods.</summary>
        public readonly List<Vector3> SprayBases = new();

        /// <summary>The fine wood as the segments it was swept along: trunk, roots, limbs, boughs, leader and branches.</summary>
        public readonly List<(Vector3 From, Vector3 To)> FineSkeleton = new();

        /// <summary>Whether a point is inside the fine wood's trunk between its bend and its dome's top.</summary>
        public Func<Vector3, bool> InsideFineTrunk;

        /// <summary>Every fine limb's first ring, which must lie inside the grown trunk.</summary>
        public readonly List<Vector3> FineLimbRoots = new();

        /// <summary>Every fine limb as the centreline its rings stand on and their radii.</summary>
        public readonly List<(Vector3[] Points, float[] Radii)> FineLimbs = new();

        /// <summary>The fine trunk's ring at the fork, where its side ends and its dome begins, and the frame's angle of each
        /// of its vertices: what the limbs must cover at their own bearings, or the rim stands round them as a lip.</summary>
        public readonly List<(Vector3 At, float Angle)> FineTrunkRim = new();

        /// <summary>The frame's angle of each fine limb's bearing and the half-angle its root circle spans round the trunk.</summary>
        public readonly List<(float Angle, float HalfSpan)> FineLimbBearings = new();
    }
}
