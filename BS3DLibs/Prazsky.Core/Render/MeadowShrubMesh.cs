using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>A free-standing meadow shrub</b> (#890): hawthorn, hazel or elder, as High and Ultra draw it. A handful of woody stems
    /// rise out of one spot in the grass, lean out as a vase does and carry a dome of leaf clumps on branches grown from them; each
    /// clump is the old trees' spray (#697, <see cref="MeadowTreeMesh.AddSpray"/>): twigs fanning out of its branch's end, broad-leaf
    /// cards hung along them. Two meshes, two materials (wood, leaf cards), drawn off one set of instances.
    /// <para>
    /// <b>Why it exists.</b> The owner's playtest note: the shrubs were "green loaves - on High and Ultra these should be more
    /// detailed shrubs". A shrub was a <see cref="FoliageMesh"/> in <see cref="FoliageStyle.Scrub"/>: a 16 by 11 sphere pushed out into
    /// lobes, spherical normals, nothing inside its outline. The references (<c>C:\Users\panrd\AI\sd\out\890-klein</c>,
    /// <c>890-zimage</c>, <c>890b-klein</c>, <c>890b-zimage</c>) draw the same build every time: a rounded but lobed mass of separate
    /// leaf clusters, the sky between them at the rim, the inside and the underside in shade, and under it thin dark stems springing
    /// from one spot in the grass, the rim's leaves hanging down nearly to it.
    /// </para>
    /// <para>
    /// <b>Grown from the crown inwards</b>, as the old trees are: the clumps are laid over the dome first, the stems are grown to the
    /// clumps they lead to, and every other clump takes a branch from the nearest stem below it, so no clump floats and every stem ends
    /// inside a clump's spray. The stems come out of the ground within <see cref="ROOT_SPREAD"/> of the axis and reach under the pivot,
    /// which the planting sinks under the ground, so the base is one rooted spot with no lip.
    /// </para>
    /// <para>
    /// <b>High and Ultra only, and only the free-standing shrubs.</b> The loaf stays the Low and Medium tiers' shrub, the Potato path's,
    /// and every tier's hedgerow (<see cref="ScatterBucket.FineMeshes"/>, <see cref="MeadowScatter"/>'s shrub draws). The mesh is fitted
    /// to the reach and the height of the loaf it stands in for (<see cref="Build"/>), so the meadow is planted once and what a camera
    /// keeps out of is the loaf's sphere either way.
    /// </para>
    /// <para>
    /// <b>The tropical beach's sea grape is this shrub too</b> (#753), fitted to that beach's scrub domes, with cards twice the side and
    /// round leaves (<c>Acacia.fx</c>'s leaves 3): the card size and the cover are arguments, the meadow's by default.
    /// </para>
    /// </summary>
    internal sealed class MeadowShrubMesh : IDisposable
    {
        /// <summary>The stems, the branches and the twigs: one material, bark.</summary>
        public IProceduralMesh Wood { get; }

        /// <summary>The broad-leaf cards (<c>Acacia.fx</c>'s leaves 2, <c>BroadLeafMask</c>), drawn <c>CullNone</c>.</summary>
        public IProceduralMesh Leaves { get; }

        /// <summary>How far from its axis anything of it stands, at scale 1.</summary>
        public float Reach { get; }

        //=== The dome the clumps are laid over, as shares of the shrub's height: its centre and its half-height (the clumps' outer
        //faces reach about its top), and the lowest point of it a clump's centre is laid at, in half-heights under the centre - the
        //rim's clumps hang down towards the grass, the middle of the underside is open over the stems
        private const float DOME_CENTRE = 0.62f, DOME_HALF = 0.42f, DOME_LOWEST = -0.7f;

        //The dome's half-width against the reach asked for: the sprays reach out past their clumps further than they rise over
        //them, so a dome as wide as the reach is fitted down by its width and stands lower than the loaf it replaces
        private const float DOME_WIDTH = 0.88f;

        //How many clumps, and a clump's radius against the shrub's reach: more and smaller than a tree's crown, which is the dense
        //mass of lobes the references draw rather than a tree's clumps with the sky between them
        private const int CLUMPS_MIN = 13, CLUMPS_EXTRA = 4;
        private const float CLUMP_MIN = 0.3f, CLUMP_MAX = 0.4f;

        //The share of the clumps set deeper inside the dome, for the depth and the shade seen through the gaps (their cards take the
        //inside's layer code, the darkest ambient), and how far in they are set
        private const float INNER_SHARE = 0.3f, INNER_FROM = 0.35f, INNER_SPREAD = 0.25f;

        //Under this height on the dome (in half-heights from its centre) a clump is the shrub's underside: its cards take the
        //inside's layer code too, the darkest ambient, as the references' shrubs are dark under their skirt
        private const float UNDERSIDE = -0.15f;

        //The outline's irregularity: a few lobes round the dome push its clumps out or pull them in by up to this share, and one or
        //two clumps on top are lifted, so no two shrubs share the hemisphere's silhouette
        private const float OUTLINE_LOBES = 0.16f, TOP_LIFT = 0.12f;

        //A clump's leaf cover and a card's side against the clump's radius, a tree's being 1.5 and 0.7 (MeadowTreeMesh's CARD_COVER
        //and CARD_SIZE): a shrub's mass is dense, the light does not show through its middle, and its leaves are small against it.
        //And how many twigs carry a clump's cards, a tree's being 5 to 8: a shrub's clump is small, and its twigs are hidden in it
        internal const float SHRUB_COVER = 2.4f, SHRUB_CARD = 0.55f;
        private const int SHRUB_TWIGS_MIN = 3, SHRUB_TWIGS_MAX = 5;

        //=== The stems: how many (5 to 7), how far from the axis they leave the ground (a share of the reach), how deep the foot is
        //under the pivot (a share of the height; the planting sinks the pivot under the ground besides), and a stem's radius at the
        //ground against the height. A branch leaves its stem at this share of the stem's radius there, and a twig is this thin
        private const int STEMS_MIN = 5, STEMS_EXTRA = 2;
        internal const float ROOT_SPREAD = 0.06f, FOOT_DEPTH = 0.08f;
        private const float STEM_RADIUS = 0.02f, BRANCH_FROM = 0.65f, TWIG_RADIUS = 0.0055f;

        //A branch may leave its stem only above this share of the stem's length (the bare stems under the dome are what the eye
        //reads as a shrub on stems), and prefers a point under its clump: a point over it costs this much more per unit it is over
        private const float BRANCH_LOWEST = 0.3f, BRANCH_DOWNHILL = 2f;

        //How many straight pieces a stem's curve is swept in (its rings), and a branch's
        private const int STEM_PIECES = 7, BRANCH_PIECES = 4;

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="reach">How far from its axis anything of the shrub stands, at scale 1.</param>
        /// <param name="top">How high it stands over its pivot, at scale 1.</param>
        /// <param name="seed">Rolls the clumps, the stems and the sprays.</param>
        /// <param name="cardSize">A leaf card's side against its clump's radius; the meadow's by default. Bigger is bigger leaves
        /// and fewer cards: the tropical beach's sea grape (#753) has leaves several times a hawthorn's.</param>
        /// <param name="cover">How many cards' worth of leaf a clump's shell is covered with; the meadow's by default.</param>
        public MeadowShrubMesh(GraphicsDevice device, float reach, float top, int seed, float cardSize = SHRUB_CARD, float cover = SHRUB_COVER)
            : this(device, Build(reach, top, seed, cardSize, cover))
        {
        }

        private MeadowShrubMesh(GraphicsDevice device, MeadowShrubGeometry geometry)
        {
            Reach = geometry.Reach;
            Wood =new UploadedMesh(device, geometry.WoodVertices, geometry.WoodIndices, geometry.Bounds);
            //A shrub's cards are a few hundred: 16-bit indices, the half of the buffer a tree's 32-bit ones take
            var leafIndices = new List<short>(geometry.LeafIndices.Count);
            foreach (int index in geometry.LeafIndices) leafIndices.Add((short)index);
            Leaves = new UploadedMesh(device, geometry.LeafVertices, leafIndices, geometry.Bounds);
        }

        /// <summary>
        /// The shrub as lists, with no device: what the constructor uploads and what <c>MeadowShrubTests</c> reads. It is grown at
        /// its nominal size and then scaled as a whole so that the farthest of its vertices from the axis is <paramref name="reach"/>
        /// out or the highest is <paramref name="top"/> up, whichever binds first: the loaf it stands in for is the volume the
        /// meadow was planted by, and its sphere is what a camera keeps out of.
        /// </summary>
        internal static MeadowShrubGeometry Build(float reach, float top, int seed, float cardSize = SHRUB_CARD, float cover = SHRUB_COVER)
        {
            var g = new MeadowShrubGeometry();
            Random rng = new(seed);

            //The nominal figure: the caller's reach and height, which the cards then overshoot a little (fitted below)
            float R = reach, H = top;
            Vector3 domeCentre = new(0f, H * DOME_CENTRE, 0f);
            float domeHalf = H * DOME_HALF;

            //--- The outline: a few broad lobes round the bearing, rolled per shrub
            int lobes = 2 + rng.Next(2);
            float lobePhase = (float)rng.NextDouble() * MathHelper.TwoPi;
            float lobeTilt = (float)rng.NextDouble() * MathHelper.TwoPi;
            float Outline(float a) => 1f + OUTLINE_LOBES * (0.65f * MathF.Sin(lobes * a + lobePhase) + 0.35f * MathF.Sin((lobes + 1) * a + lobeTilt));

            //--- The clumps: over the dome's upper part and down its flanks, area-evenly on the golden angle, each pulled inside
            //the outline by its own radius so its outer face is what reaches it; a share set deeper, and the top one or two lifted
            int clumpCount = CLUMPS_MIN + rng.Next(CLUMPS_EXTRA + 1);
            float golden = MathF.PI * (3f - MathF.Sqrt(5f));
            float spin = (float)rng.NextDouble() * MathHelper.TwoPi;
            var clumps = new List<(Vector3 Centre, float Radius, bool Inner)>(clumpCount);
            var shaded = new bool[clumpCount];
            for (int i = 0; i < clumpCount; i++)
            {
                float y = 1f - (1f - DOME_LOWEST) * (i + 0.5f) / clumpCount;
                float ring = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
                float a = spin + golden * i + 0.4f * ((float)rng.NextDouble() - 0.5f);
                float r = R * (CLUMP_MIN + (CLUMP_MAX - CLUMP_MIN) * (float)rng.NextDouble());
                bool inner = rng.NextDouble() < INNER_SHARE;
                float outline = Outline(a);
                float inset = inner ? INNER_FROM + INNER_SPREAD * (float)rng.NextDouble()
                    : 1f - r / R * 0.6f + 0.08f * ((float)rng.NextDouble() - 0.5f);
                float lift = !inner && i < 2 ? TOP_LIFT * H * (float)rng.NextDouble() : 0f;
                Vector3 centre = domeCentre + new Vector3(MathF.Cos(a) * ring * R * DOME_WIDTH * outline, y * (domeHalf - r * 0.4f),
                    MathF.Sin(a) * ring * R * DOME_WIDTH * outline) * inset
                    + new Vector3(0f, lift, 0f);
                clumps.Add((centre, r, inner));
                shaded[i] = inner || y < UNDERSIDE;
            }

            //--- The stems: out of one spot, leaning out as a vase does, each grown to the clump it leads to - the outer clump
            //nearest its bearing whose centre stands in the dome's middle band, so the stem's top is inside the leaf
            int stemCount = STEMS_MIN + rng.Next(STEMS_EXTRA + 1);
            float stemPhase = (float)rng.NextDouble() * MathHelper.TwoPi;
            float stemRadius = H * STEM_RADIUS;
            var stems = new List<(Vector3[] Points, float[] Radii)>(stemCount);
            var led = new bool[clumpCount];
            var sprayBases = new Vector3[clumpCount];
            var sprayBacks = new Vector3[clumpCount];   //back along the stem or branch from its end, for the twigs' buried bases
            for (int s = 0; s < stemCount; s++)
            {
                float a = stemPhase + MathHelper.TwoPi * s / stemCount + 0.6f * ((float)rng.NextDouble() - 0.5f);
                Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));

                //The clump it leads to: the nearest by bearing among those not yet led to, weighted to the dome's middle height
                int lead = -1;
                float best = float.MaxValue;
                for (int c = 0; c < clumpCount; c++)
                {
                    if (led[c]) continue;
                    Vector3 o = clumps[c].Centre - domeCentre;
                    float gap = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(o.Z, o.X) - a));
                    float score = gap + 1.2f * MathF.Abs(o.Y / domeHalf - 0.15f) + (clumps[c].Inner ? 0.3f : 0f);
                    if (score < best) { best = score; lead = c; }
                }
                led[lead] = true;

                Vector3 foot = dir * (R * ROOT_SPREAD * 0.5f) + Vector3.Down * (H * FOOT_DEPTH);
                Vector3 target = clumps[lead].Centre;
                //Into the clump to a third of its radius short of the centre, where its twigs fan out (the tree's rule)
                Vector3 spray = target - Vector3.Normalize(target - foot) * (clumps[lead].Radius * 0.33f);
                sprayBases[lead] = spray;

                //A quadratic curve: up out of the ground near the axis, the control point over the foot so the stem leaves steeply
                //and leans out above, as a vase's sides do
                Vector3 control = dir * (R * ROOT_SPREAD) + Vector3.Up * (spray.Y * (0.55f + 0.15f * (float)rng.NextDouble()));
                var points = new Vector3[STEM_PIECES + 1];
                var radii = new float[STEM_PIECES + 1];
                for (int k = 0; k <= STEM_PIECES; k++)
                {
                    float t = k / (float)STEM_PIECES;
                    points[k] = (1f - t) * (1f - t) * foot + 2f * t * (1f - t) * control + t * t * spray;
                    radii[k] = stemRadius * MathHelper.Lerp(1.25f, 0.35f, t);
                }
                //Closed at both ends from its own rings (#943's rule for a prop that ends in a tube): the foot is buried and the top
                //is inside its clump, but a ring left open is a hole wherever the ground or the leaf fails to hide it
                TubeGeometry.AddSweep(g.WoodVertices, g.WoodIndices, 6, points, radii, capStart: true, capEnd: true);
                sprayBacks[lead] = Vector3.Normalize(points[STEM_PIECES - 1] - points[STEM_PIECES]);
                stems.Add((points, radii));
                for (int k = 0; k < STEM_PIECES; k++) g.Skeleton.Add((points[k], points[k + 1]));
                g.Stems.Add(points);
            }

            //--- The branches: every clump no stem leads to takes one from the nearest point of a stem under it (a point over it
            //costs more, so a branch climbs or runs out rather than hanging down into its clump), bowing up a little on the way
            for (int c = 0; c < clumpCount; c++)
            {
                if (led[c]) continue;
                (Vector3 centre, float r, _) = clumps[c];

                Vector3 from = Vector3.Zero;
                float fromRadius = 0f, best = float.MaxValue;
                foreach ((Vector3[] points, float[] radii) in stems)
                    for (int k = (int)MathF.Ceiling(BRANCH_LOWEST * STEM_PIECES); k < points.Length; k++)
                    {
                        float over = MathF.Max(0f, points[k].Y - centre.Y);
                        float cost = Vector3.Distance(points[k], centre) + BRANCH_DOWNHILL * over;
                        if (cost < best) { best = cost; from = points[k]; fromRadius = radii[k]; }
                    }

                Vector3 spray = centre - Vector3.Normalize(centre - from + new Vector3(0f, 1e-3f, 0f)) * (r * 0.33f);
                sprayBases[c] = spray;
                float span = Vector3.Distance(from, spray);
                Vector3 mid = Vector3.Lerp(from, spray, 0.5f) + Vector3.Up * (span * 0.12f);
                var points2 = new Vector3[BRANCH_PIECES + 1];
                var radii2 = new float[BRANCH_PIECES + 1];
                for (int k = 0; k <= BRANCH_PIECES; k++)
                {
                    float t = k / (float)BRANCH_PIECES;
                    points2[k] = (1f - t) * (1f - t) * from + 2f * t * (1f - t) * mid + t * t * spray;
                    radii2[k] = MathHelper.Lerp(fromRadius * BRANCH_FROM, stemRadius * 0.3f, t);
                }
                TubeGeometry.AddSweep(g.WoodVertices, g.WoodIndices, 5, points2, radii2, capStart: true, capEnd: true);
                sprayBacks[c] = Vector3.Normalize(points2[BRANCH_PIECES - 1] - points2[BRANCH_PIECES]);
                for (int k = 0; k < BRANCH_PIECES; k++) g.Skeleton.Add((points2[k], points2[k + 1]));
                g.Branches.Add((from, fromRadius));
            }

            //--- The leaf: each clump's spray of twigs and broad-leaf cards, the old trees' own (#697), denser
            Random leafRng = new(seed * 41 + 7);
            for (int c = 0; c < clumpCount; c++)
            {
                (Vector3 centre, float r, _) = clumps[c];
                MeadowTreeMesh.AddSpray(g.WoodVertices, g.WoodIndices, g.LeafVertices, g.LeafIndices, sprayBases[c], sprayBacks[c], centre, domeCentre,
                    r, r * 0.8f, shaded[c], H * TWIG_RADIUS, leafRng, cover, cardSize, SHRUB_TWIGS_MIN, SHRUB_TWIGS_MAX);
            }
            g.TwigRadius = H * TWIG_RADIUS;
            g.SprayBases.AddRange(sprayBases);

            //--- Fitted: scaled as a whole about the pivot so the outermost vertex is at the reach asked for, or the highest at the
            //top, whichever binds first; normals are untouched by a uniform scale
            float widest = 0f, highest = 0f;
            foreach (var list in new[] { g.WoodVertices, g.LeafVertices })
                foreach (VertexPositionNormalTexture v in list)
                {
                    widest = MathF.Max(widest, MathF.Sqrt(v.Position.X * v.Position.X + v.Position.Z * v.Position.Z));
                    highest = MathF.Max(highest, v.Position.Y);
                }
            float fit = MathF.Min(reach / widest, top / highest);
            g.Scale(fit);
            g.Reach = widest * fit;

            Vector3 middle = new(0f, top * 0.5f, 0f);
            float sphere = 0f;
            foreach (var list in new[] { g.WoodVertices, g.LeafVertices })
                foreach (VertexPositionNormalTexture v in list) sphere = MathF.Max(sphere, Vector3.Distance(v.Position, middle));
            g.Bounds = new BoundingSphere(middle, sphere);
            return g;
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Leaves as IDisposable)?.Dispose();
        }
    }

    /// <summary>A meadow shrub as lists, before any device (<see cref="MeadowShrubMesh.Build"/>'s answer).</summary>
    internal sealed class MeadowShrubGeometry
    {
        public readonly List<VertexPositionNormalTexture> WoodVertices = new();
        public readonly List<short> WoodIndices = new();

        public readonly List<VertexPositionNormalTexture> LeafVertices = new();
        public readonly List<int> LeafIndices = new();

        /// <summary>The stems and branches as the segments they were swept along (not the twigs).</summary>
        public readonly List<(Vector3 From, Vector3 To)> Skeleton = new();

        /// <summary>Every stem's centreline from its foot, under the pivot, to the spray it leads to.</summary>
        public readonly List<Vector3[]> Stems = new();

        /// <summary>Where every branch leaves its stem, and the stem's radius there.</summary>
        public readonly List<(Vector3 From, float StemRadius)> Branches = new();

        /// <summary>Every clump's spray base, where its twigs fan out: a stem's or a branch's end.</summary>
        public readonly List<Vector3> SprayBases = new();

        public BoundingSphere Bounds;

        /// <summary>How far from its axis its farthest vertex stands.</summary>
        public float Reach;

        /// <summary>A twig's radius where it leaves its spray base: the one ring of the wood left open (inside the branch's end).</summary>
        public float TwigRadius;

        /// <summary>Scales everything about the pivot (#890's fit): positions and the recorded points; normals stay.</summary>
        internal void Scale(float s)
        {
            static void Positions(List<VertexPositionNormalTexture> list, float s)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    VertexPositionNormalTexture v = list[i];
                    v.Position *= s;
                    list[i] = v;
                }
            }
            Positions(WoodVertices, s);
            Positions(LeafVertices, s);
            for (int i = 0; i < Skeleton.Count; i++) Skeleton[i] = (Skeleton[i].From * s, Skeleton[i].To * s);
            foreach (Vector3[] stem in Stems)
                for (int k = 0; k < stem.Length; k++) stem[k] *= s;
            for (int i = 0; i < Branches.Count; i++) Branches[i] = (Branches[i].From * s, Branches[i].StemRadius * s);
            for (int i = 0; i < SprayBases.Count; i++) SprayBases[i] *= s;
            TwigRadius *= s;
        }
    }
}
