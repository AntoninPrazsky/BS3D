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
    /// ragged rim — the dome was the shape of a bush held up on a stick. Since #884 the tiers are stretched up a little,
    /// as a game's stylisation (the <c>*_TIER</c> profiles and <see cref="MAX_TIER_ASPECT"/>): a savanna roll made a tier up to
    /// seven times wider than its half-thickness.
    /// </para>
    /// </summary>
    public sealed class AcaciaMesh : IDisposable
    {
        public AcaciaKind Kind { get; }

        /// <summary>
        /// The trunk, boughs, limbs and twigs as Low and Medium draw them: straight tapered tubes of seven sides, every
        /// bough starting at the trunk's top centre. <see cref="FineWood"/> is the same tree at High and Ultra's detail.
        /// </summary>
        public IProceduralMesh Wood { get; }

        /// <summary>
        /// The same wood grown rather than assembled (#758), for High and Ultra (<see cref="SceneRenderer.FineMeshes"/>):
        /// the trunk swells into a crotch that takes the shape of the boughs it divides into, closed inside them by a low
        /// dome whose saddles dip between neighbours, and every bough roots just inside it and leaves along the trunk's own
        /// surface on a curve that turns outward, so the fork is one surface rather than tubes poking out of a cylinder's cut
        /// end. Limbs and a dead tree's twigs leave their bough the same way. Twelve to twenty sides where <see cref="Wood"/> has seven. It ends at the same points
        /// <see cref="Wood"/> does and carries the same crown twigs, so <see cref="Leaves"/> hangs from either.
        /// <para>
        /// <b>The bark stays the world's</b> (<c>Acacia.fx</c> stretches its fissures up Y), and that is a decision, not an
        /// omission: laying them along each bough instead (the distance along the wood and its radius carried in the
        /// texture coordinate) was built and photographed, and it put a seam exactly at the fork — the trunk and every
        /// bough sampled unrelated stretches of the field where they meet. A field of world position is the same field on
        /// both sides of any intersection, which is what lets the trunk and the boughs read as one surface.
        /// </para>
        /// </summary>
        public IProceduralMesh FineWood { get; }

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
        internal const float CROWN_SOLID = 0.92f;

        //How thick a crown tier is (#884). The owner: "the crowns of some of those trees are far too flat ... stretch the crowns
        //up a little". A tier's radius and the tree's height are rolled apart (SavannaScatter), and its thickness was a fixed
        //share of the height whatever the roll, so a mature tier ran from 3 to 7 times wider than its half-thickness. Per kind
        //(and per tier), as shares of the tree's height: where the tier was centred and its half-thickness at #663 (the nominal
        //underside, the difference, stays where it was - the drawn plate's own underside does not quite: see Tier), and the
        //half-thickness it has now - the one number that sets how flat a crown reads. The top rises by what the tier grew.
        private static readonly TierProfile MATURE_TIER = new(0.78f, 0.14f, 0.21f);
        private static readonly TierProfile SECOND_TIER = new(0.94f, 0.10f, 0.12f);
        private static readonly TierProfile YOUNG_TIER = new(0.84f, 0.11f, 0.155f);
        private static readonly TierProfile BROKEN_TIER = new(0.76f, 0.13f, 0.18f);

        //The widest a tier may run against its half-thickness: its radius over its half-thickness is never more than this, so
        //the wide-short end of the roll is thickened until it is (the narrow-tall end, already thicker, is not touched)
        internal const float MAX_TIER_ASPECT = 3.8f;

        //The crown's twigs (#782): how thick one leaves its bough against the trunk at the fork, and how many sprays it
        //carries - few, so a tier's twigs are many and its leaf spreads over the plate rather than in spokes
        private const float TWIG_RADIUS = 0.07f;
        private const int SPRAYS_PER_TWIG = 8;

        //The fine wood's sides (#758): the trunk's, where the boughs' shape and the grooves between them have to read; a
        //bough's and a spar's; a limb's, a dead tree's twig's and a root's. The coarse wood has 7 (SEG) and 5
        private const int FINE_TRUNK_SIDES = 20;
        private const int FINE_BOUGH_SIDES = 12;
        private const int FINE_LIMB_SIDES = 8;

        //The crotch (#758): how far below the fork the trunk starts to swell and to take the boughs' shape, in fork radii;
        //how much wider it is at the fork than the plain taper would be; the dome closing the top, in fork radii; and how
        //deep a saddle between two boughs dips, as a share of it
        private const float CROTCH_HEIGHT = 1.6f;
        private const float CROTCH_SWELL = 1.15f;
        private const float CROTCH_DOME = 0.35f;
        private const float SADDLE_DIP = 0.6f;

        //A bough's root ring: this many fork radii under the fork, inside the trunk's top ring by this share of its radius,
        //so that leaning out with the trunk's flare it crosses the trunk's side just under the fork and is outside the top
        //ring at the fork — rooted ON the top ring, it left a band of the dome's rim showing between trunk and bough, and
        //flush, two coincident surfaces fight for the depth. How far its first control point stands along the trunk's
        //surface, as a share of the way to the bough's middle: the length over which it turns from the trunk's line to its
        //own. And how many of its rings take the trunk's normals, fading (see BuildFineWood)
        private const float BOUGH_ROOT_DEPTH = 0.25f;
        private const float BOUGH_FLUSH = 0.98f;
        private const float BOUGH_LEAVE = 0.15f;
        private const int BOUGH_BLENDED_RINGS = 3;

        //A limb (or a dead tree's twig) leaves its bough this far along the bough's first run, from inside it, along the
        //bough's own axis for this share of its length
        private const float LIMB_ROOT_AT = 0.8f;
        private const float LIMB_LEAVE = 0.3f;

        /// <summary>Every tier of foliage in one mesh; <c>null</c> for a <see cref="AcaciaKind.Dead"/> tree.</summary>
        public IProceduralMesh Canopy { get; }

        /// <summary>
        /// The same tiers as <see cref="Canopy"/>, as leaf sprays rather than a plate (#610) — drawn instead of it
        /// at scene detail, where <see cref="Canopy"/> is the Low tier's. <c>null</c> for a dead tree. See
        /// <see cref="LeafSprays"/>.
        /// </summary>
        public IProceduralMesh Leaves { get; }

        public AcaciaMesh(GraphicsDevice device, AcaciaKind kind, float trunkRadius, float treeHeight, float canopyRadius, int seed)
            : this(device, Build(kind, trunkRadius, treeHeight, canopyRadius, seed))
        {
        }

        private AcaciaMesh(GraphicsDevice device, AcaciaGeometry geometry)
        {
            Kind = geometry.Kind;
            BaseRadius = geometry.BaseRadius;
            Volume = geometry.Volume;
            Wood = new UploadedMesh(device, geometry.WoodVertices, geometry.WoodIndices, geometry.WoodBounds);
            FineWood = new UploadedMesh(device, geometry.FineWoodVertices, geometry.FineWoodIndices, geometry.WoodBounds);

            if (geometry.CanopyVertices != null)
            {
                Canopy = new UploadedMesh(device, geometry.CanopyVertices, geometry.CanopyIndices, geometry.CanopyBounds);
                Leaves = new UploadedMesh(device, geometry.LeafVertices, geometry.LeafIndices, geometry.CanopyBounds);
            }
        }

        /// <summary>
        /// The whole tree as vertex and index lists, with no device (#782): what the constructor uploads, and what a test
        /// reads to hold the tree to its promises — every leaf spray's stem on the wood (<see cref="AcaciaGeometry.Skeleton"/>).
        /// </summary>
        internal static AcaciaGeometry Build(AcaciaKind kind, float trunkRadius, float treeHeight, float canopyRadius, int seed)
        {
            AcaciaGeometry geometry = new() { Kind = kind, BaseRadius = trunkRadius * ROOT_FLARE };
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
                    tiers.Add(Tier(Vector2.Zero, YOUNG_TIER, canopyRadius * (0.5f + 0.12f * (float)rng.NextDouble()), treeHeight));
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
                    tiers.Add(Tier(offset, BROKEN_TIER, canopyRadius * 0.8f, treeHeight));
                    //The spar stands where the crown is not: opposite the surviving tier, and above it.
                    spars.Add(new Vector2(side + MathF.PI + (float)(rng.NextDouble() - 0.5) * 0.6f, treeHeight * (1.02f + 0.08f * (float)rng.NextDouble())));
                    break;
                }

                default: //Mature
                {
                    forkY = treeHeight * (0.30f + 0.10f * (float)rng.NextDouble());
                    boughs = 3 + rng.Next(3);
                    tiers.Add(Tier(Vector2.Zero, MATURE_TIER, canopyRadius, treeHeight));
                    if (rng.NextDouble() < 0.55)
                    {
                        float side = (float)rng.NextDouble() * MathHelper.TwoPi;
                        Vector2 offset = new Vector2(MathF.Cos(side), MathF.Sin(side)) * (canopyRadius * 0.3f);
                        //Stands on the first tier, so it rides up with the first tier's top and keeps its place on it
                        tiers.Add(Tier(offset, SECOND_TIER, canopyRadius * (0.4f + 0.15f * (float)rng.NextDouble()), treeHeight, tiers[0].TopRise));
                    }
                    break;
                }
            }

            //The wood, and the points under each tier its boughs end at - what the tier's twigs grow out of (#782)
            var supports = new List<Vector3>[tiers.Count];
            for (int t = 0; t < tiers.Count; t++) supports[t] = new List<Vector3>();

            float reach = BuildWood(geometry, supports, trunkRadius, treeHeight, canopyRadius, forkY, boughs, tiers, spars, twigs, rng);
            geometry.WoodBounds = new BoundingSphere(new Vector3(0f, treeHeight * 0.55f, 0f), reach * 1.2f + treeHeight * 0.6f);

            var volume = new List<Slab> { new(0f, forkY, trunkRadius * TRUNK_MEAN) };
            foreach (TierSpec tier in tiers)
                volume.Add(new Slab(tier.CentreY - tier.HalfHeight, tier.CentreY + tier.HalfHeight, tier.Radius * CROWN_SOLID, tier.Offset.X, tier.Offset.Y));
            if (tiers.Count == 0) volume.Add(new Slab(forkY, treeHeight, canopyRadius * 0.55f));
            geometry.Volume = volume.ToArray();

            if (tiers.Count > 0)
            {
                geometry.CanopyVertices = new List<VertexPositionNormalTexture>();
                geometry.CanopyIndices = new List<short>();
                for (int t = 0; t < tiers.Count; t++)
                {
                    TierSpec tier = tiers[t];
                    FoliageMesh.Generate(geometry.CanopyVertices, geometry.CanopyIndices, tier.Radius, tier.HalfHeight,
                        new Vector3(tier.Offset.X, tier.CentreY, tier.Offset.Y), seed * 31 + 7 + t * 13, FoliageStyle.Tier);
                }
                geometry.CanopyBounds = new BoundingSphere(new Vector3(0f, treeHeight * 0.85f, 0f), canopyRadius * 1.6f + treeHeight * 0.2f);

                //The sprays on twigs grown from the boughs' ends under each tier (#782), the twigs into both woods — the fine
                //wood's boughs end where the coarse wood's do (#758), so one set of twigs and leaves serves both
                Random leafRng = new(seed * 37 + 11);
                for (int t = 0; t < tiers.Count; t++)
                {
                    TierSpec tier = tiers[t];
                    geometry.Supports.AddRange(supports[t]);
                    LeafSprays.HangOnTwigs(geometry.WoodVertices, geometry.WoodIndices, geometry.Skeleton, geometry.LeafVertices,
                        geometry.LeafIndices, supports[t], new Vector3(tier.Offset.X, tier.CentreY, tier.Offset.Y), tier.Radius,
                        tier.HalfHeight, trunkRadius * TWIG_RADIUS, SPRAYS_PER_TWIG, leafRng,
                        geometry.FineWoodVertices, geometry.FineWoodIndices);
                }
            }

            return geometry;
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (FineWood as IDisposable)?.Dispose();
            (Canopy as IDisposable)?.Dispose();
            (Leaves as IDisposable)?.Dispose();
        }

        /// <summary>One flat plate of foliage: where it sits off the trunk's axis, its height, radius and thickness, and how far
        /// its top stands above where #663 left it (<see cref="TopRise"/>, what a tier standing on it rides up by).</summary>
        private readonly struct TierSpec
        {
            public readonly Vector2 Offset;
            public readonly float CentreY, Radius, HalfHeight, TopRise;
            public TierSpec(Vector2 offset, float centreY, float radius, float halfHeight, float topRise)
            { Offset = offset; CentreY = centreY; Radius = radius; HalfHeight = halfHeight; TopRise = topRise; }
        }

        /// <summary>A kind's tier as shares of the tree's height (#884): <paramref name="centre"/> and <paramref name="was"/> are
        /// where it was centred and its half-thickness at #663 (so its nominal underside, the difference), <paramref name="half"/>
        /// its half-thickness now.</summary>
        private readonly struct TierProfile(float centre, float was, float half)
        {
            public readonly float Centre = centre, Was = was, Half = half;
        }

        /// <summary>
        /// A crown tier stretched upward (#884): it keeps its <i>nominal</i> underside (the centre less the half-thickness, as #663
        /// left it - the <see cref="Slab"/> the planting keeps apart by) and its radius, takes the profile's half-thickness, and is
        /// never thinner than <see cref="MAX_TIER_ASPECT"/> allows for its radius, so the centre rises by what the tier grew and
        /// the top by a little more (the top is flattened, so it stands <c>1 - TopFlatten</c> of the half-thickness over the
        /// centre). <b>The drawn plate's underside is not the nominal one:</b> <see cref="FoliageStyle.Tier"/> flattens it
        /// (<c>BottomFlatten</c>) so its lowest point is only about 0.4 of the half-thickness under the centre, and it therefore rises,
        /// by about 0.6 of what the tier grew (0.3 to 0.9 units over the savanna's rolls); the nominal slab stays under the leaves.
        /// Every share the leaves and the boughs hang from is a share of the half-thickness, so they follow.
        /// </summary>
        /// <param name="lift">How far the whole tier is carried up, because it stands on another that rose.</param>
        private static TierSpec Tier(Vector2 offset, TierProfile profile, float radius, float treeHeight, float lift = 0f)
        {
            float was = profile.Was * treeHeight;
            float half = MathF.Max(MathF.Max(profile.Half * treeHeight, radius / MAX_TIER_ASPECT), was);
            float grown = half - was;
            return new TierSpec(offset, profile.Centre * treeHeight + grown + lift, radius, half,
                lift + grown * (2f - FoliageStyle.Tier.TopFlatten));
        }

        /// <summary>Trunk + boughs: a flared trunk to the fork, then several tapered boughs fanning up and out
        /// to the tiers' undersides, each with a slight upward bend so the crown sits on spread arms. A dead
        /// tree's boughs branch on into twigs instead, and a broken tree's spar rises bare past the crown.
        /// <para>Into <paramref name="geometry"/>'s wood lists, every tube also a segment of its <see cref="AcaciaGeometry.Skeleton"/>,
        /// and each bough's and limb's end under a tier added to that tier's <paramref name="supports"/> (#782). Returns how far
        /// the wood reaches off the axis.</para>
        /// <para>The same tree into the fine wood's lists as well (#758, <see cref="BuildFineWood"/>), off the same rolls: the
        /// coarse wood is emitted as the rolls are taken, exactly as it always was, and every roll the fine wood needs is
        /// recorded beside it, because the trunk's crotch takes the shape of every bough before the first one can leave it.</para></summary>
        private static float BuildWood(AcaciaGeometry geometry, List<Vector3>[] supports, float trunkRadius, float treeHeight,
            float canopyRadius, float forkY, int boughs, List<TierSpec> tiers, List<Vector2> spars, bool twigs, Random rng)
        {
                List<VertexPositionNormalTexture> v = geometry.WoodVertices;
                List<short> idx = geometry.WoodIndices;
                const int SEG = 7;

                //What the fine wood is grown from: the roots (where each leaves, bends and ends) and every departure from the fork
                var fineRoots = new List<(Vector3 Leave, Vector3 Knee, Vector3 End, float Foot)>();
                var departures = new List<FineDeparture>();

                void Tube(int seg, Vector3 from, float fromRadius, Vector3 to, float toRadius)
                {
                    TubeGeometry.AddTube(v, idx, seg, from, fromRadius, to, toRadius);
                    geometry.Skeleton.Add((from, to));
                }

                //The trunk, flared at the root and holding most of its girth to the fork.
                Tube(SEG, new Vector3(0f, 0f, 0f), trunkRadius * ROOT_FLARE,
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
                    Tube(5, leave, foot * 0.42f, knee, foot * 0.24f);
                    Tube(5, knee, foot * 0.24f, end, foot * 0.08f);
                    fineRoots.Add((leave, knee, end, foot));
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
                    int carried = -1;
                    if (tiers.Count > 0)
                    {
                        TierSpec tier = tiers[0];
                        carried = 0;
                        float best = float.NegativeInfinity;
                        for (int t = 0; t < tiers.Count; t++)
                        {
                            float toward = Vector2.Dot(tiers[t].Offset, new Vector2(dir.X, dir.Z)) + (t == 0 ? 0.01f : 0f);
                            if (toward > best) { best = toward; tier = tiers[t]; carried = t; }
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
                    Tube(SEG, fork, trunkRadius * 0.6f, mid, trunkRadius * 0.42f);
                    Tube(SEG, mid, trunkRadius * 0.42f, tip, trunkRadius * 0.14f);
                    if (carried >= 0) supports[carried].Add(tip);
                    var departure = new FineDeparture(a, mid, tip, trunkRadius * 0.6f, trunkRadius * 0.42f, trunkRadius * 0.14f);
                    departures.Add(departure);

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
                            Tube(5, mid, trunkRadius * 0.3f, sTip, trunkRadius * 0.08f);
                            if (carried >= 0) supports[carried].Add(sTip);
                            departure.Limbs.Add((sTip, trunkRadius * 0.3f, trunkRadius * 0.08f));
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
                            float along = 0.35f + 0.5f * (float)rng.NextDouble();
                            Vector3 from = Vector3.Lerp(mid, tip, along);
                            float ta = a + (float)(rng.NextDouble() - 0.5) * 1.6f;
                            float len = canopyRadius * (0.25f + 0.25f * (float)rng.NextDouble());
                            Vector3 to = from + new Vector3(MathF.Cos(ta), 0f, MathF.Sin(ta)) * (len * 0.7f) + Vector3.Up * (len * 0.7f);
                            Tube(5, from, trunkRadius * 0.22f, to, trunkRadius * 0.05f);
                            departure.Twigs.Add((along, to - from, trunkRadius * 0.22f, trunkRadius * 0.05f));
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
                    Tube(SEG, fork, trunkRadius * 0.55f, mid, trunkRadius * 0.38f);
                    Tube(SEG, mid, trunkRadius * 0.38f, top, trunkRadius * 0.06f);
                    departures.Add(new FineDeparture(a, mid, top, trunkRadius * 0.55f, trunkRadius * 0.38f, trunkRadius * 0.06f));
                }

                BuildFineWood(geometry, trunkRadius, forkY, fineRoots, departures);
                return reach;
        }

        /// <summary>
        /// One bough or spar leaving the fork, as the coarse wood rolled it (#758): its bearing, its middle and its end with the
        /// radius at its root, middle and end; the limbs it carries (each an end and the radius at its root and end) or, on a
        /// dead tree, its twigs (where along its outer run each leaves, the twig's own reach, and its radii).
        /// </summary>
        private sealed class FineDeparture(float bearing, Vector3 mid, Vector3 end, float rootRadius, float midRadius, float endRadius)
        {
            public readonly float Bearing = bearing;
            public readonly Vector3 Mid = mid, End = end;
            public readonly float RootRadius = rootRadius, MidRadius = midRadius, EndRadius = endRadius;
            public readonly List<(Vector3 End, float RootRadius, float EndRadius)> Limbs = new();
            public readonly List<(float Along, Vector3 Reach, float RootRadius, float EndRadius)> Twigs = new();
        }

        /// <summary>
        /// The wood at High and Ultra's detail (#758): <see cref="AddGrownTrunk"/>, the roots as one bent run each, and every
        /// bough and spar on a curve out of the crotch: it roots just inside the trunk under the fork, on the circle the
        /// crotch was shaped to for it, leaves along the trunk's own surface and turns outward to the coarse bough's middle and
        /// on to its end — so its outer side carries on the trunk's outline and it parts from its neighbours in a saddle. Each
        /// limb leaves its bough the same way, from inside it along the bough's axis, to the coarse limb's end; so do a dead
        /// tree's twigs. The ends are the coarse wood's, which is what keeps the crown's twigs on wood.
        /// </summary>
        private static void BuildFineWood(AcaciaGeometry geometry, float trunkRadius, float forkY,
            List<(Vector3 Leave, Vector3 Knee, Vector3 End, float Foot)> roots, List<FineDeparture> departures)
        {
            List<VertexPositionNormalTexture> v = geometry.FineWoodVertices;
            List<short> idx = geometry.FineWoodIndices;
            List<(Vector3 From, Vector3 To)> skeleton = geometry.FineSkeleton;

            var roots0 = new (float Bearing, float Radius)[departures.Count];
            for (int d = 0; d < roots0.Length; d++) roots0[d] = (departures[d].Bearing, departures[d].RootRadius);
            GrownTrunk trunk = new(trunkRadius, forkY, roots0);
            AddGrownTrunk(v, idx, trunk);
            skeleton.Add((Vector3.Zero, new Vector3(0f, forkY, 0f)));
            geometry.FineTrunkRadius = trunk.Radius;

            //The roots as one bent run each, where the coarse wood lays two tubes end to end and leaves both their ends open
            //at the knee
            foreach ((Vector3 leave, Vector3 knee, Vector3 end, float foot) in roots)
            {
                TubeGeometry.AddSweep(v, idx, FINE_LIMB_SIDES, new[] { leave, knee, end }, new[] { foot * 0.42f, foot * 0.24f, foot * 0.08f });
                skeleton.Add((leave, knee));
                skeleton.Add((knee, end));
            }

            Vector3 fork = new(0f, forkY, 0f);
            var points = new List<Vector3>();
            var radii = new List<float>();
            foreach (FineDeparture departure in departures)
            {
                Vector3 dir = new(MathF.Cos(departure.Bearing), 0f, MathF.Sin(departure.Bearing));

                //The root just under the trunk's top ring, centred on the circle the trunk's crotch was shaped to (GrownTrunk);
                //the run to the middle leaving along the trunk's surface (so the two shade alike where they part), the run to
                //the end leaving the middle along the line the coarse wood's fork-to-end chord gives it
                float rootY = forkY - BOUGH_ROOT_DEPTH * trunkRadius * 0.9f;
                float offset = MathF.Min(trunk.RootOffset(departure.RootRadius), trunk.Radius(rootY, departure.Bearing) - departure.RootRadius);
                Vector3 root = new Vector3(0f, rootY, 0f) + dir * MathF.Max(offset, 0f);
                Vector3 alongTrunk = trunk.Tangent(rootY, departure.Bearing);
                Vector3 throughMid = Vector3.Normalize(departure.End - fork);
                float first = Vector3.Distance(root, departure.Mid), second = Vector3.Distance(departure.Mid, departure.End);
                Vector3 a0 = root, a1 = root + alongTrunk * (first * BOUGH_LEAVE), a2 = departure.Mid - throughMid * (first * 0.3f), a3 = departure.Mid;
                Vector3 b0 = departure.Mid, b1 = departure.Mid + throughMid * (second * 0.33f),
                    b2 = departure.End - Vector3.Normalize(departure.End - departure.Mid) * (second * 0.33f), b3 = departure.End;

                points.Clear();
                radii.Clear();
                foreach (float t in BOUGH_FIRST_RUN)
                {
                    points.Add(Bezier(a0, a1, a2, a3, t));
                    radii.Add(MathHelper.Lerp(departure.RootRadius * BOUGH_FLUSH, departure.MidRadius, t));
                }
                foreach (float t in BOUGH_SECOND_RUN)
                {
                    points.Add(Bezier(b0, b1, b2, b3, t));
                    radii.Add(MathHelper.Lerp(departure.MidRadius, departure.EndRadius, t));
                }
                int firstRing = v.Count;
                TubeGeometry.AddSweep(v, idx, FINE_BOUGH_SIDES, points, radii);
                for (int p = 0; p + 1 < points.Count; p++) skeleton.Add((points[p], points[p + 1]));

                //Shaded as the trunk where it leaves it: the first rings take the trunk's own normals at their height and
                //bearing, fading into the bough's own. A sweep's ring stands square to its first chord, which already leans out
                //with the bough's turn, and so did its normals — 18 to 23 degrees down at the fork against the trunk's 7 up, a
                //step a tenth lighter across the junction (measured on seed 4103, the Testbed's close camera)
                for (int ring = 0; ring < BOUGH_BLENDED_RINGS; ring++)
                {
                    float own = ring / (float)BOUGH_BLENDED_RINGS;
                    for (int side = 0; side <= FINE_BOUGH_SIDES; side++)
                    {
                        int at = firstRing + ring * (FINE_BOUGH_SIDES + 1) + side;
                        VertexPositionNormalTexture vertex = v[at];
                        Vector3 trunkNormal = trunk.Normal(MathF.Min(trunk.AtHeight(vertex.Position.Y), 1f),
                            MathF.Atan2(vertex.Position.Z, vertex.Position.X));
                        vertex.Normal = Vector3.Normalize(Vector3.Lerp(trunkNormal, vertex.Normal, own));
                        v[at] = vertex;
                    }
                }

                //The limbs, out of the bough's first run near its middle
                Vector3 limbRoot = Bezier(a0, a1, a2, a3, LIMB_ROOT_AT);
                Vector3 limbLeave = Vector3.Normalize(BezierTangent(a0, a1, a2, a3, LIMB_ROOT_AT));
                foreach ((Vector3 end, float rootRadius, float endRadius) in departure.Limbs)
                    AddOffshoot(v, idx, skeleton, limbRoot, limbLeave, end, rootRadius, endRadius);

                //A dead tree's twigs, out of the bough's outer run where the coarse twig left its chord
                foreach ((float along, Vector3 twigReach, float rootRadius, float endRadius) in departure.Twigs)
                {
                    Vector3 from = Bezier(b0, b1, b2, b3, along);
                    Vector3 leave = Vector3.Normalize(BezierTangent(b0, b1, b2, b3, along));
                    AddOffshoot(v, idx, skeleton, from, leave, from + twigReach, rootRadius, endRadius);
                }
            }
        }

        //Where a fine bough's rings stand on its two runs (#758): close together where it turns out of the crotch, wider
        //apart along the straighter outer run
        private static readonly float[] BOUGH_FIRST_RUN = { 0f, 0.08f, 0.16f, 0.25f, 0.35f, 0.47f, 0.6f, 0.75f, 0.88f, 1f };
        private static readonly float[] BOUGH_SECOND_RUN = { 0.34f, 0.67f, 1f };
        private static readonly float[] OFFSHOOT_RUN = { 0f, 0.12f, 0.25f, 0.4f, 0.6f, 0.8f, 1f };

        /// <summary>
        /// A limb or a twig out of the wood it grows from (#758): from <paramref name="root"/> inside the parent, leaving along
        /// the parent's own axis there (<paramref name="leave"/>) and turning towards <paramref name="end"/> — one Bezier, so
        /// it emerges at a slant rather than starting square out of the parent's surface.
        /// </summary>
        private static void AddOffshoot(List<VertexPositionNormalTexture> v, List<short> idx, List<(Vector3 From, Vector3 To)> skeleton,
            Vector3 root, Vector3 leave, Vector3 end, float rootRadius, float endRadius)
        {
            float length = Vector3.Distance(root, end);
            Vector3 c1 = root + leave * (length * LIMB_LEAVE);
            Vector3 c2 = end - Vector3.Normalize(end - root) * (length * 0.3f);
            var points = new Vector3[OFFSHOOT_RUN.Length];
            var radii = new float[OFFSHOOT_RUN.Length];
            for (int p = 0; p < points.Length; p++)
            {
                points[p] = Bezier(root, c1, c2, end, OFFSHOOT_RUN[p]);
                radii[p] = MathHelper.Lerp(rootRadius, endRadius, OFFSHOOT_RUN[p]);
            }
            TubeGeometry.AddSweep(v, idx, FINE_LIMB_SIDES, points, radii);
            for (int p = 0; p + 1 < points.Length; p++) skeleton.Add((points[p], points[p + 1]));
        }

        private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        private static Vector3 BezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        /// <summary>
        /// The fine wood's trunk as a surface (#758): the coarse trunk's cone (<see cref="ROOT_FLARE"/> at the foot to 0.9 of
        /// the trunk radius at the fork) up to <see cref="CrotchFrom"/>; from there to the fork it swells to
        /// <see cref="CROTCH_SWELL"/> times the cone and takes the shape of the boughs it divides into, so that its top ring
        /// is the outline of their root circles together (<see cref="Shape"/>) — each circle as wide as the bough it roots,
        /// its outer side on the swollen trunk's circle — with a groove running down from every saddle between two, the
        /// references' trunk of fused limbs. Over the fork a low dome closes it, its saddles dipping between neighbours. One
        /// parameter runs up the profile: 0 at the foot, 1 at the fork, 2 at the dome's top.
        /// <para>
        /// A trunk merely swollen and lobed, with the boughs leaving it through its side, was the first build: the boughs
        /// left under the lobes' rim, which stood round them as a lip, and their round sections crossed the gentle lobes at
        /// an angle, so they shaded a step lighter than the trunk below them.
        /// </para>
        /// </summary>
        private sealed class GrownTrunk
        {
            private readonly float _trunkRadius, _forkY, _domeHeight, _topRadius;
            private readonly float[] _bearings;   //sorted, in [0, 2pi)
            private readonly (float Bearing, float Radius)[] _roots;
            public readonly float CrotchFrom;

            public GrownTrunk(float trunkRadius, float forkY, (float Bearing, float Radius)[] roots)
            {
                _trunkRadius = trunkRadius;
                _forkY = forkY;
                float forkRadius = trunkRadius * 0.9f;
                _topRadius = forkRadius * CROTCH_SWELL;
                CrotchFrom = MathF.Max(forkY - CROTCH_HEIGHT * forkRadius, forkY * 0.5f);
                _domeHeight = CROTCH_DOME * forkRadius;
                _roots = roots;
                _bearings = new float[roots.Length];
                for (int i = 0; i < roots.Length; i++) _bearings[i] = Wrap(roots[i].Bearing);
                Array.Sort(_bearings);
            }

            private static float Wrap(float angle)
            {
                float a = angle % MathHelper.TwoPi;
                return a < 0f ? a + MathHelper.TwoPi : a;
            }

            /// <summary>The profile parameter at a height on the trunk's side (0 at the foot, 1 at the fork).</summary>
            public float AtHeight(float y) => y / _forkY;

            /// <summary>How far off the axis the root circle of a bough this thick is centred: its outer side on the trunk's
            /// swollen top circle, and never past the axis.</summary>
            public float RootOffset(float rootRadius) => MathF.Max(_topRadius - rootRadius, 0f);

            /// <summary>
            /// +1 towards a departure's bearing and −1 midway between two neighbours, a cosine across each gap: where the
            /// dome's saddles dip. The world angle is the bearing convention the boughs are rolled in (cos on X, sin on Z).
            /// </summary>
            public float Lobe(float angle)
            {
                int n = _bearings.Length;
                if (n < 2) return 0f;
                float a = Wrap(angle);
                int k = n - 1;
                for (int i = 0; i < n; i++) if (_bearings[i] <= a) k = i;
                float from = _bearings[k];
                float to = k + 1 < n ? _bearings[k + 1] : _bearings[0] + MathHelper.TwoPi;
                if (a < from) a += MathHelper.TwoPi;
                float t = (a - from) / MathF.Max(to - from, 1e-4f);
                return MathF.Cos(MathHelper.TwoPi * t);
            }

            /// <summary>
            /// The top ring's reach at a bearing, as a share of the swollen circle: how far out along it the outline of the
            /// boughs' root circles together stands — 1 on a bough's own bearing, less in the cusp between two.
            /// </summary>
            public float Shape(float bearing)
            {
                float reach = 0f;
                foreach ((float rootBearing, float radius) in _roots)
                {
                    float centre = RootOffset(radius);
                    float off = bearing - rootBearing;
                    float across = centre * MathF.Sin(off);
                    if (MathF.Abs(across) > radius) continue;
                    reach = MathF.Max(reach, centre * MathF.Cos(off) + MathF.Sqrt(radius * radius - across * across));
                }
                return reach > 0f ? MathF.Min(reach / _topRadius, 1f) : 1f;
            }

            /// <summary>
            /// The trunk's radius at a height on its side and a bearing. The crotch eases in from the cone (no crease where it
            /// starts) and is still widening at the fork, so the trunk flares into the boughs and a bough leaving along its
            /// surface (<see cref="Tangent"/>) leans out with it; the boughs' shape comes in over the same height.
            /// </summary>
            public float Radius(float y, float bearing)
            {
                float cone = _trunkRadius * (ROOT_FLARE + (0.9f - ROOT_FLARE) * MathHelper.Clamp(y / _forkY, 0f, 1f));
                float t = MathHelper.Clamp((y - CrotchFrom) / MathF.Max(_forkY - CrotchFrom, 1e-4f), 0f, 1f);
                float crotch = t * t;
                return cone * (1f + (CROTCH_SWELL - 1f) * crotch) * MathHelper.Lerp(1f, Shape(bearing), crotch);
            }

            /// <summary>The direction up the trunk's side at a height and a bearing: up, leaning with the surface (taken from
            /// below, so at the fork it is the side's and not the dome's).</summary>
            public Vector3 Tangent(float y, float bearing)
            {
                const float STEP = 1e-3f;
                float h = MathF.Min(AtHeight(y), 1f);
                return Vector3.Normalize(Point(h, bearing) - Point(h - STEP, bearing));
            }

            /// <summary>
            /// The surface's outward normal at profile parameter <paramref name="h"/> and world bearing
            /// <paramref name="bearing"/>, from its two tangents — the shape and the dome make it anything but radial. Up at
            /// the dome's top, where the ring has closed to a point.
            /// </summary>
            public Vector3 Normal(float h, float bearing)
            {
                const float STEP = 1e-3f;
                Vector3 up = Point(MathF.Min(h + STEP, 2f), bearing) - Point(MathF.Max(h - STEP, 0f), bearing);
                Vector3 around = Point(h, bearing - STEP) - Point(h, bearing + STEP);
                Vector3 n = Vector3.Cross(up, around);
                if (n.LengthSquared() < 1e-12f) return Vector3.Up;
                n.Normalize();
                Vector3 radial = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));
                return Vector3.Dot(n, radial) < 0f && h < 2f ? -n : n;
            }

            /// <summary>The point on the surface at profile parameter <paramref name="h"/> and world bearing <paramref name="bearing"/>.</summary>
            public Vector3 Point(float h, float bearing)
            {
                Vector3 radial = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));
                if (h <= 1f)
                {
                    float y = h * _forkY;
                    return radial * Radius(y, bearing) + Vector3.Up * y;
                }
                //The saddles dip on the dome's flanks and fade out towards its top, which is one point: a dip carried to the
                //top would leave the last ring a line of points up the axis, a seam of open slivers
                float phi = MathHelper.Clamp(h - 1f, 0f, 1f) * MathHelper.PiOver2;
                float rise = MathF.Sin(phi);
                float height = _domeHeight * (1f - SADDLE_DIP * 0.5f * (1f - Lobe(bearing)) * (1f - rise));
                return radial * (Radius(_forkY, bearing) * MathF.Cos(phi)) + Vector3.Up * (_forkY + height * rise);
            }
        }

        //The grown trunk's rings: the cone needs only its two ends, the crotch and the dome are where the shape is
        private static readonly float[] CROTCH_RUN = { 0.25f, 0.5f, 0.75f, 1f };
        private static readonly float[] DOME_RUN = { 0.2f, 0.4f, 0.6f, 0.8f, 1f };

        /// <summary>
        /// <see cref="GrownTrunk"/> as rings of <see cref="FINE_TRUNK_SIDES"/>, foot to dome. Each normal is the surface's own,
        /// from its two tangents (the boughs' shape and the dome make it anything but radial). The texture coordinate is
        /// <see cref="TubeGeometry.AddTube"/>'s (round the ring, up the profile), which nothing reads on wood. Wound as
        /// <see cref="TubeGeometry.AddTube"/> is, the lower ring as its start.
        /// </summary>
        private static void AddGrownTrunk(List<VertexPositionNormalTexture> v, List<short> idx, GrownTrunk trunk)
        {
            var stations = new List<float> { 0f, trunk.AtHeight(trunk.CrotchFrom) };
            float crotchStart = trunk.AtHeight(trunk.CrotchFrom);
            foreach (float t in CROTCH_RUN) stations.Add(crotchStart + (1f - crotchStart) * t);
            foreach (float t in DOME_RUN) stations.Add(1f + t);

            const int SIDES = FINE_TRUNK_SIDES;
            short baseIdx = (short)v.Count;
            for (int i = 0; i < stations.Count; i++)
            {
                float h = stations[i];
                for (int s = 0; s <= SIDES; s++)
                {
                    //AddTube's basis for an axis pointing up: the ring starts on +Z and turns towards +X
                    float ang = MathHelper.TwoPi * s / SIDES;
                    float bearing = MathHelper.PiOver2 - ang;
                    v.Add(new VertexPositionNormalTexture(trunk.Point(h, bearing), trunk.Normal(h, bearing),
                        new Vector2(s / (float)SIDES, i / (stations.Count - 1f))));
                }
            }
            for (int i = 0; i + 1 < stations.Count; i++)
            {
                int lower = baseIdx + i * (SIDES + 1), upper = lower + SIDES + 1;
                for (int s = 0; s < SIDES; s++)
                {
                    idx.Add((short)(lower + s)); idx.Add((short)(upper + s)); idx.Add((short)(lower + s + 1));
                    idx.Add((short)(lower + s + 1)); idx.Add((short)(upper + s)); idx.Add((short)(upper + s + 1));
                }
            }
        }
    }

    /// <summary>
    /// An acacia as lists, before any device (#782): <see cref="AcaciaMesh.Build"/>'s answer, which the mesh uploads and a
    /// test reads. <see cref="Skeleton"/> is every piece of wood as the segment it was swept along — trunk, roots, boughs,
    /// limbs, spars and the crown's twigs — so a leaf can be measured against the wood it should grow from.
    /// </summary>
    internal sealed class AcaciaGeometry
    {
        public AcaciaKind Kind;
        public float BaseRadius;
        public Slab[] Volume;

        public readonly List<VertexPositionNormalTexture> WoodVertices = new();
        public readonly List<short> WoodIndices = new();
        public BoundingSphere WoodBounds;

        /// <summary>High and Ultra's wood (#758), crown twigs included, and its skeleton without them: the grown trunk's
        /// axis, the roots, and every bough, spar, limb and dead twig as the curve it was swept along.</summary>
        public readonly List<VertexPositionNormalTexture> FineWoodVertices = new();
        public readonly List<short> FineWoodIndices = new();
        public readonly List<(Vector3 From, Vector3 To)> FineSkeleton = new();

        /// <summary>Every point the crown's twigs grow from (#782): the ends of the boughs and limbs under a tier.</summary>
        public readonly List<Vector3> Supports = new();

        /// <summary>The fine wood's trunk as a radius at a height on its side (up to the fork) and a world bearing.</summary>
        public Func<float, float, float> FineTrunkRadius;

        public List<VertexPositionNormalTexture> CanopyVertices;
        public List<short> CanopyIndices;
        public BoundingSphere CanopyBounds;

        public readonly List<VertexPositionNormalTexture> LeafVertices = new();
        public readonly List<int> LeafIndices = new();

        public readonly List<(Vector3 From, Vector3 To)> Skeleton = new();
    }

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

        //A spray's length as a share of the tier's radius, and its width against its length; how far the layers droop at the
        //rim, as a share of the half-thickness at the rim's own square (a tier's, and a bush's in LeafShape)
        private const float SPRAY_LENGTH = 0.20f, SPRAY_ASPECT = 0.45f, DROOP = 0.5f;

        /// <summary>
        /// How a cloud of sprays is laid over the wood (#883): the layers (each a height in the cloud's half-thickness, a reach
        /// as a share of its radius and a cover, as <c>LAYERS</c> above), how long a spray is as a share of the radius and how
        /// far the layers droop towards the rim. <see cref="Tier"/> is an acacia's and a baobab's tuft's: a thin flat plate.
        /// A thorn bush's (<see cref="ThornBushMesh"/>) is a mound, and takes its own.
        /// </summary>
        public sealed class LeafShape((float Y, float Reach, float Cover)[] layers, float sprayLength, float droop)
        {
            public readonly (float Y, float Reach, float Cover)[] Layers = layers;
            public readonly float SprayLength = sprayLength, Droop = droop;

            public static readonly LeafShape Tier = new(LAYERS, SPRAY_LENGTH, DROOP);
        }

        //A twig's leafy part (#782): the inner share of it is bare wood, as a twig's base is, and the sprays hang along
        //the rest; its taper, from its base to its tip, against the radius it leaves its support at
        private const float LEAVES_FROM = 0.3f;
        private static readonly float[] TWIG_TAPER = { 1f, 0.7f, 0.45f, 0.2f };

        //How finely a twig is written into the skeleton: its curve in this many straight pieces, so a stem on the curve is
        //on the skeleton too
        private const int SKELETON_PIECES = 8;

        //How a spray leaves its twig (#882): the lean off the twig's heading, as a sideways share of the forward one (0.8 to 1.1
        //is forty-odd degrees, as the meadow's sprigs, #697), the last spray's lean when it carries straight on, how much of the
        //twig's own rise (its climb over its run) a spray follows and the most it may climb or fall, and the run a twig's tangent
        //must have to give a heading (a twig on end has none)
        private const float LEAN = 0.8f, LEAN_SPREAD = 0.3f, LAST_LEAN = 0.25f;
        private const float TWIG_CLIMB = 0.5f, MAX_CLIMB = 0.5f, MIN_HEADING = 0.2f;

        /// <summary>
        /// A tier's (or a tuft's) sprays on twigs (#782): the owner's "leaves always grow out of branches, never out of
        /// the air". The sprays used to be laid uniformly over the disc with nothing under them but the boughs at its rim,
        /// and on the savanna's trees up to nine in ten stood clear of any wood. Now twigs grow from the wood under the
        /// disc (<paramref name="supports"/>: the boughs' and limbs' ends) to points spread over its three layers exactly
        /// as the sprays were spread, each bowing up on the way, and the sprays hang along the outer part of the twig with
        /// every stem — the card's <c>u</c> = 0 end, where <c>Acacia.fx</c>'s mask starts the spray — on it. The layers keep
        /// their spray counts, so the leaf mass and its cost are what they were; the twigs go into the wood lists
        /// (<paramref name="woodVertices"/>, the bark's material) and their curves into <paramref name="skeleton"/>.
        /// <para>
        /// <b>A spray points along its twig</b> (#882): its direction is the twig's heading at the stem (the tangent of the
        /// curve, seen from above) leaned forwards by forty-odd degrees to alternate sides, as the meadow's sprigs are
        /// (#697), the last one on a twig carrying straight on; it follows half of the twig's rise, droops towards the rim
        /// and tilts a little at random, so the tier stays a thin plate of foliage. Pointing every card away from the tier's
        /// centre instead, whatever its twig does, hangs the cards of a twig that ends nearer the centre than the bough it
        /// grows from (about half of them) backwards, against their wood: 40 to 65 % of an acacia's cards
        /// (<c>LeafAttachmentTests</c>). The rolls are taken in the same order whatever the heading, so it moves no twig and
        /// no stem.
        /// </para>
        /// </summary>
        /// <param name="spraysPerTwig">How many sprays a twig carries: few on a crown the eye walks across, so the twigs
        /// spread the leaf over the disc rather than in spokes; more on a small tuft.</param>
        /// <param name="fineWoodVertices">A second wood the same twigs go into: an acacia's fine wood (#758), whose boughs
        /// end where the coarse wood's do. <c>null</c> for none.</param>
        /// <param name="fineWoodIndices">Its indices.</param>
        /// <param name="shape">How the sprays are laid over the disc (#883): <see cref="LeafShape.Tier"/> (the default, every
        /// acacia's and baobab's tuft's, unchanged) or a thorn bush's mound. The rolls are taken in the same order whichever.</param>
        public static void HangOnTwigs(List<VertexPositionNormalTexture> woodVertices, List<short> woodIndices,
            List<(Vector3 From, Vector3 To)> skeleton, List<VertexPositionNormalTexture> v, List<int> idx,
            IReadOnlyList<Vector3> supports, Vector3 centre, float radius, float halfHeight, float twigRadius, int spraysPerTwig,
            Random rng, List<VertexPositionNormalTexture> fineWoodVertices = null, List<short> fineWoodIndices = null,
            LeafShape shape = null)
        {
            shape ??= LeafShape.Tier;
            float length = radius * shape.SprayLength;
            float width = length * SPRAY_ASPECT;

            for (int layer = 0; layer < shape.Layers.Length; layer++)
            {
                (float layerY, float reach, float cover) = shape.Layers[layer];
                float disc = MathF.PI * radius * reach * radius * reach;
                int sprays = (int)(cover * disc / (length * width));
                int twigs = Math.Max(1, (sprays + spraysPerTwig - 1) / spraysPerTwig);

                for (int t = 0; t < twigs; t++)
                {
                    //Where the twig reaches: over the layer's disc as a spray was laid, area-even with a ragged edge, drooping
                    //at the rim
                    float r = radius * reach * MathF.Sqrt((float)rng.NextDouble()) * (0.88f + 0.24f * (float)rng.NextDouble());
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float rim = r / radius;
                    Vector3 tip = centre + new Vector3(MathF.Cos(a) * r,
                        halfHeight * (layerY + 0.35f * ((float)rng.NextDouble() - 0.5f)) - halfHeight * shape.Droop * rim * rim,
                        MathF.Sin(a) * r);

                    //From the wood nearest it, as seen from above: the support it would have grown out of
                    Vector3 basePoint = centre;
                    float best = float.MaxValue;
                    for (int s = 0; s < supports.Count; s++)
                    {
                        float dx = supports[s].X - tip.X, dz = supports[s].Z - tip.Z;
                        float d = dx * dx + dz * dz;
                        if (d < best) { best = d; basePoint = supports[s]; }
                    }

                    //Bowing up on the way, as wood grown towards the light does, through a middle point shaken off the chord
                    float span = Vector3.Distance(basePoint, tip);
                    Vector3 jitter = new((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                    Vector3 mid = Vector3.Lerp(basePoint, tip, 0.5f) + new Vector3(0f, span * 0.1f, 0f) + jitter * (span * 0.1f);
                    Vector3 At(float f) => (1f - f) * (1f - f) * basePoint + 2f * f * (1f - f) * mid + f * f * tip;
                    Vector3 TangentAt(float f) => 2f * (1f - f) * (mid - basePoint) + 2f * f * (tip - mid);

                    Vector3[] twigPoints = { basePoint, At(0.33f), At(0.67f), tip };
                    float[] twigRadii = { twigRadius * TWIG_TAPER[0], twigRadius * TWIG_TAPER[1], twigRadius * TWIG_TAPER[2], twigRadius * TWIG_TAPER[3] };
                    TubeGeometry.AddSweep(woodVertices, woodIndices, 3, twigPoints, twigRadii);
                    if (fineWoodVertices != null) TubeGeometry.AddSweep(fineWoodVertices, fineWoodIndices, 3, twigPoints, twigRadii);
                    for (int piece = 0; piece < SKELETON_PIECES; piece++)
                        skeleton.Add((At(piece / (float)SKELETON_PIECES), At((piece + 1) / (float)SKELETON_PIECES)));

                    //This twig's share of the layer's sprays, along its outer part, each stem on the twig
                    int cards = sprays / (twigs - t);
                    sprays -= cards;
                    for (int k = 0; k < cards; k++)
                    {
                        float f = LEAVES_FROM + (1f - LEAVES_FROM) * (k + 0.3f + 0.4f * (float)rng.NextDouble()) / cards;
                        Vector3 stem = At(f);
                        Vector2 out_ = new(stem.X - centre.X, stem.Z - centre.Z);
                        float srim = MathF.Min(out_.Length() / radius, 1.2f);

                        //Along the twig it hangs on (#882): the heading is the twig's own at the stem, so a twig that runs inwards
                        //carries sprays that point inwards with it; only a twig standing on end (it has no heading) falls back to
                        //pointing out of the tier's centre, rising (or falling) as steeply as a spray may, as the twig does
                        Vector3 tangent = TangentAt(f);
                        float tangentLength = tangent.Length();
                        if (tangentLength > 1e-6f) tangent /= tangentLength;
                        float heading = MathF.Sqrt(tangent.X * tangent.X + tangent.Z * tangent.Z);
                        Vector3 forward;
                        float climb;
                        if (heading > MIN_HEADING)
                        {
                            forward = new Vector3(tangent.X / heading, 0f, tangent.Z / heading);
                            climb = Math.Clamp(tangent.Y / heading * TWIG_CLIMB, -MAX_CLIMB, MAX_CLIMB);
                        }
                        else
                        {
                            float sa = out_.LengthSquared() > 1e-8f ? MathF.Atan2(out_.Y, out_.X) : a;
                            forward = new Vector3(MathF.Cos(sa), 0f, MathF.Sin(sa));
                            climb = tangent.Y >= 0f ? MAX_CLIMB : -MAX_CLIMB;
                        }

                        //Laid near flat and leaving the twig forwards, to alternate sides as a leaf's pairs do, the last one carrying
                        //straight on (the meadow's rule, #697); it follows the twig's rise by half, droops at the rim and tilts a
                        //little at random
                        float leanRoll = (float)rng.NextDouble();
                        float lean = (k == cards - 1 ? LAST_LEAN : LEAN + LEAN_SPREAD * leanRoll) * (((k + t) & 1) == 0 ? 1f : -1f);
                        Vector3 flat = Vector3.Normalize(forward + Vector3.Cross(Vector3.Up, forward) * lean);
                        Vector3 along = new(flat.X, climb - 0.25f * srim + 0.3f * ((float)rng.NextDouble() - 0.5f), flat.Z);
                        along.Normalize();
                        Vector3 across = Vector3.Normalize(Vector3.Cross(Vector3.Up, along));
                        across = Vector3.Normalize(across + Vector3.Up * (0.8f * ((float)rng.NextDouble() - 0.5f)));
                        Vector3 normal = Vector3.Normalize(Vector3.Cross(along, across));
                        if (normal.Y < 0f) normal = -normal;

                        float scale = 0.75f + 0.5f * (float)rng.NextDouble();
                        Vector3 end = stem + along * (length * scale);
                        Vector3 half = across * (width * scale * 0.5f);

                        float layerCode = 2f * layer;
                        int b = v.Count;
                        v.Add(new VertexPositionNormalTexture(stem - half, normal, new Vector2(0f, layerCode)));
                        v.Add(new VertexPositionNormalTexture(stem + half, normal, new Vector2(0f, layerCode + 1f)));
                        v.Add(new VertexPositionNormalTexture(end + half, normal, new Vector2(1f, layerCode + 1f)));
                        v.Add(new VertexPositionNormalTexture(end - half, normal, new Vector2(1f, layerCode)));
                        idx.Add(b); idx.Add(b + 1); idx.Add(b + 2);
                        idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
                    }
                }
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

        /// <summary>
        /// One continuous tube along a polyline, a ring at every point (#609's third round): each ring square to the
        /// mean of the two segments it joins and turned by parallel transport from the one before, so a bent limb is one
        /// skin. Straight tubes laid end to end at an angle meet with their end rings in two different planes — a step
        /// on one side and an overlap on the other — and on the meadow's thick old trunks that read as a sleeve pulled
        /// over the wood. Side faces only unless asked, wound as <see cref="AddTube"/> is.
        /// <para>
        /// The ends can be closed from the rings' own vertices (#889), so no sliver opens between a disc and the ring it
        /// closes, as one does when a separate <see cref="AddCap"/> lays its polygon turned against the ring's: an open
        /// end is a hole under back-face culling, the sky seen through the wood.
        /// </para>
        /// </summary>
        /// <param name="capStart">Close the first ring with a flat disc facing back along the tube.</param>
        /// <param name="capEnd">Close the last ring: a flat disc, or with <paramref name="endDome"/> a rounded end.</param>
        /// <param name="endDome">How far the closed end bulges past the last ring, as a share of its radius (0 flat):
        /// a half-ellipsoid of <see cref="DOME_RINGS"/> rings, its normals the ellipsoid's.</param>
        public static void AddSweep(List<VertexPositionNormalTexture> v, List<short> idx, int seg,
            IReadOnlyList<Vector3> points, IReadOnlyList<float> radii, bool capStart = false, bool capEnd = false, float endDome = 0f)
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
            Vector3 firstAxis = axis, lastU = u, lastW = Vector3.Zero;
            for (int k = 0; k < count; k++)
            {
                axis = Tangent(k);
                //Parallel transport: the last ring's reference direction, with what now lies along the axis taken out
                u = Vector3.Normalize(u - axis * Vector3.Dot(u, axis));
                Vector3 w = Vector3.Cross(axis, u);
                if (k == 0) firstAxis = axis;
                lastU = u;
                lastW = w;
                for (int s = 0; s <= seg; s++)
                {
                    float ang = MathHelper.TwoPi * s / seg;
                    Vector3 dir = u * MathF.Cos(ang) + w * MathF.Sin(ang);
                    v.Add(new VertexPositionNormalTexture(points[k] + dir * radii[k], dir, new Vector2(s / (float)seg, k / (count - 1f))));
                }
            }

            //A rounded end: more rings past the last, closing to its tip on the same frame, so they join it as the sweep's own
            int rings = count;
            if (capEnd && endDome > 0f)
            {
                Vector3 end = points[count - 1];
                float radius = radii[count - 1];
                for (int j = 1; j <= DOME_RINGS; j++)
                {
                    float psi = MathHelper.PiOver2 * j / DOME_RINGS;
                    float c = MathF.Cos(psi), sn = MathF.Sin(psi);
                    for (int s = 0; s <= seg; s++)
                    {
                        float ang = MathHelper.TwoPi * s / seg;
                        Vector3 dir = lastU * MathF.Cos(ang) + lastW * MathF.Sin(ang);
                        //The tip is one point, exactly: cos(pi/2) in floats left its vertices a hair apart round it
                        bool tip = j == DOME_RINGS;
                        Vector3 normal = tip ? axis : Vector3.Normalize(dir * (endDome * c) + axis * sn);
                        Vector3 at = tip ? end + axis * (radius * endDome) : end + dir * (radius * c) + axis * (radius * endDome * sn);
                        v.Add(new VertexPositionNormalTexture(at, normal, new Vector2(s / (float)seg, 1f)));
                    }
                }
                rings += DOME_RINGS;
            }

            for (int k = 0; k < rings - 1; k++)
            {
                int ring = baseIdx + k * (seg + 1), next = ring + seg + 1;
                for (int s = 0; s < seg; s++)
                {
                    idx.Add((short)(ring + s)); idx.Add((short)(next + s)); idx.Add((short)(ring + s + 1));
                    idx.Add((short)(ring + s + 1)); idx.Add((short)(next + s)); idx.Add((short)(next + s + 1));
                }
            }

            //Flat discs from the rings' own vertices, wound as AddCap's for the way each faces: the last ring's basis turns
            //with its axis as AddCap's does with `outward`, so the end winds as AddCap; the start faces back, so it is reversed
            if (capStart) AddFan(v, idx, baseIdx, seg, points[0], -firstAxis, reversed: true);
            if (capEnd && endDome <= 0f) AddFan(v, idx, (short)(baseIdx + (count - 1) * (seg + 1)), seg, points[count - 1], axis, reversed: false);
        }

        /// <summary>How many rings close a rounded end (<see cref="AddSweep"/>'s <c>endDome</c>), the tip's included.</summary>
        public const int DOME_RINGS = 4;

        /// <summary>A disc fanned from a centre to a ring already in the list (<paramref name="ring"/>, <paramref name="seg"/> + 1
        /// vertices, the last on the first), facing <paramref name="outward"/>: <paramref name="reversed"/> false for a ring
        /// laid as <see cref="AddSweep"/> lays one (its second direction the axis crossed with its first) seen along that axis,
        /// true for the same ring closed facing back.</summary>
        public static void AddFan(List<VertexPositionNormalTexture> v, List<short> idx, short ring, int seg, Vector3 centre,
            Vector3 outward, bool reversed)
        {
            short centreIdx = (short)v.Count;
            v.Add(new VertexPositionNormalTexture(centre, outward, new Vector2(0.5f, 0.5f)));
            //The rim takes the disc's normal too, so the disc shades flat rather than as the side it closes
            for (int s = 0; s < seg; s++)
                v.Add(new VertexPositionNormalTexture(v[ring + s].Position, outward, v[ring + s].TextureCoordinate));
            for (int s = 0; s < seg; s++)
            {
                short a = (short)(centreIdx + 1 + s), b = (short)(centreIdx + 1 + (s + 1) % seg);
                idx.Add(centreIdx);
                idx.Add(reversed ? a : b);
                idx.Add(reversed ? b : a);
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
        /// The savanna's bushes are still this on Low and Medium and on the Potato path; High and Ultra draw
        /// <see cref="ThornBushMesh"/> in its place (#883). The meadow's old trees' clumps are this too.</summary>
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
