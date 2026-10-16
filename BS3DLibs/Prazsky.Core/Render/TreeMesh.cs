using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A forest tree: a tapering, irregular trunk (<see cref="Trunk"/>) under a crown (<see cref="Crown"/>).
    /// Two meshes, because it is two materials — bark and foliage — which a single instanced draw cannot tint
    /// differently (the diffuse colour is per-draw, not per-instance). The pattern is the island's: one
    /// object, two <see cref="IProceduralMesh"/>es, two renderers.
    /// <para>
    /// Two species share the class, chosen by <see cref="TreeSpecies"/>: a <b>conifer</b> — a spruce built as
    /// <b>tiers of branch whorls</b>, each layer a drooping skirt with a shadowed tuck beneath it, because a
    /// spruce's silhouette is its layers and a smooth cone reads as a plastic toy however much it wobbles —
    /// and a <b>broadleaf</b>, grown rather than lathed since #647 (<see cref="MeadowTreeMesh.ForForest"/>): a bole
    /// forking into limbs and branches under a dome of separate clumps. Its crown was one lathed ball of several
    /// overlapping lobes until then, and on its bare stick it read as a lollipop at any height. Both are rolled from
    /// the <c>seed</c>: tier count, taper, droop and pinch for the spruce; the clumps' layout and the wood's forks for
    /// the broadleaf; and the lathe wobble's phase for every part — so two variants differ in structure, not merely in proportions, which
    /// is what the eye checks when it decides whether a wood is real. The crown of either species spans
    /// <c>[trunkHeight, trunkHeight + crownHeight]</c> give or take its droop and lobes, so the trunk stays
    /// visible under it; the first build used the height as a semi-axis, which buried the whole trunk inside
    /// an egg resting on the ground.
    /// </para>
    /// <para>
    /// A spruce carries a second crown for High and Ultra (#733): separate boughs over a dark interior, with lumps of
    /// snow, at a near and a far detail (<see cref="FineCrown"/>, <see cref="SpruceCrownGeometry"/>), laid out by the same
    /// whorls the lathe is rolled from.
    /// </para>
    /// <para>
    /// All profiles trace <b>top → outside → underside</b>, the direction <see cref="LatheMesh"/> documents:
    /// traced the other way the solid comes out inside out — same silhouette, far side drawn, shading dark —
    /// which is exactly how the first forest looked.
    /// </para>
    /// </summary>
    public sealed class TreeMesh : IDisposable
    {
        /// <summary>The wood: for a conifer a tapering bark cylinder with a root flare, irregular enough that no two read
        /// identical; for a broadleaf the whole branching wood since #647 — the bole, its limbs and the branches into
        /// every clump of the crown (<see cref="MeadowTreeMesh.ForForest"/>).</summary>
        public IProceduralMesh Trunk { get; }

        /// <summary>The canopy, sitting on the trunk's top: tiered whorls for a conifer, a dome of clumps for a broadleaf.</summary>
        public IProceduralMesh Crown { get; }

        /// <summary>The canopy's authored radius (a conifer's widest skirt, a broadleaf's half-width) — its lobes
        /// and whorls wander a little past it. For a host keeping a camera out of the tree (#559).</summary>
        public float CrownRadius { get; }

        /// <summary>The whole tree's authored height, trunk and crown, from the ground.</summary>
        public float Height { get; }

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="species">Which of the two crown shapes the tree gets.</param>
        /// <param name="trunkBaseRadius">Trunk radius up the flank; the root flare at the ground is wider.</param>
        /// <param name="trunkTopRadius">Trunk radius at the top (a trunk tapers).</param>
        /// <param name="trunkHeight">Trunk height to the underside of the canopy.</param>
        /// <param name="crownRadius">Canopy radius (a conifer's widest skirt, a broadleaf's half-width).</param>
        /// <param name="crownHeight">Canopy height, trunk top to crown top — the crown spans this.</param>
        /// <param name="seed">Rolls everything structural: the spruce's tier layout, the broadleaf's clumps and forks,
        /// the wobble phases. The same seed always builds the same tree.</param>
        /// <param name="segments">Facets around the trunk axis. The crown uses its own facet count.</param>
        /// <param name="coniferTiers">The fewest whorls a spruce crown rolls (see
        /// <see cref="ForestTreeConfig.ConiferTiers"/>); ignored for a broadleaf.</param>
        /// <param name="coniferTierSpread">How many whorl counts a spruce rolls between, from
        /// <paramref name="coniferTiers"/> up. The defaults are the four-to-six the forest was built with, and
        /// they consume the seed exactly as it always has, so a caller that passes neither gets the same tree.</param>
        /// <param name="coniferRaggedness">How uneven a spruce's whorls are (see
        /// <see cref="ForestTreeConfig.ConiferRaggedness"/>); 1 is the forest as it was built.</param>
        public TreeMesh(GraphicsDevice graphicsDevice, TreeSpecies species,
            float trunkBaseRadius, float trunkTopRadius, float trunkHeight,
            float crownRadius, float crownHeight, int seed = 0, int segments = 8,
            int coniferTiers = 4, int coniferTierSpread = 3, float coniferRaggedness = 1f)
        {
            CrownRadius = crownRadius;
            Height = trunkHeight + crownHeight;

            //A BROADLEAF IS GROWN, NOT LATHED, since #647's second step. The owner, of the daytime forest: "the trees
            //are too low and don't look realistic" - and after the height came right the broadleaves still read as
            //lollipops, one lobed ball on a bare stick. The meadow's old trees are built the way the references draw a
            //deciduous tree (MeadowTreeMesh: the crown laid out first as a dome of separate clumps, the wood grown to
            //it - a bole forking into limbs, the limbs into branches, one into every clump), and a forest broadleaf is
            //the same tree grown up among others: a tall bare bole and a narrower crown carried high on steep limbs.
            if (species == TreeSpecies.Broadleaf)
            {
                _grown = MeadowTreeMesh.ForForest(graphicsDevice, trunkBaseRadius, trunkHeight, crownRadius, crownHeight, seed);
                Trunk = _grown.Wood;
                Crown = _grown.Crown;
                CrownRadius = _grown.CrownReach;
                Height = _grown.Height;
                return;
            }

            (float flare, float trunkWobble, List<LathePoint> crownProfile, SpruceWhorlPlan plan) = RollConifer(
                trunkBaseRadius, trunkHeight, crownRadius, crownHeight, seed, coniferTiers, coniferTierSpread, coniferRaggedness);
            float phase = plan.Phase;

            //Top rim, down the flank, out into a root flare, and in along the buried underside. No top cap:
            //the crown of either species closes over the trunk's top rim, so a cap would never be seen. The
            //flare is rolled per tree — roots grip differently — inside a band where both extremes still
            //read as a standing trunk.
            var trunkProfile = new List<LathePoint>
            {
                new(trunkTopRadius,          trunkHeight,        wobble: 0.7f),
                new(trunkBaseRadius,         trunkHeight * 0.3f, wobble: 1f),
                new(trunkBaseRadius * flare, 0f,                 crease: true, wobble: 1f), //root flare into the ground
                new(0f,                      0f)
            };

            Trunk = new LatheMesh(graphicsDevice, trunkProfile, segments, irregularityAmplitude: trunkWobble, irregularityPhase: phase);

            //Fourteen facets, for the reason RockMesh documents: the wobble runs at 3, 7 and 13 waves per revolution, and at
            //ten facets the 7-wave term aliased down to a lateral shift — fine on a smooth cone, but a skirt edge is the one
            //ring the eye traces, so it gets the sampling to break honestly.
            Crown = new LatheMesh(graphicsDevice, crownProfile, segments: 14,
                irregularityAmplitude: plan.WobbleAmplitude, irregularityPhase: phase);

            //High and Ultra's crown (#733), at both its details, off the same whorl plan the lathe was just built from and
            //random streams of their own, so the coarse crown above consumes the seed exactly as it always has
            SpruceCrownGeometry near = SpruceCrownGeometry.Build(plan, seed, SpruceDetail.Near);
            SpruceCrownGeometry far = SpruceCrownGeometry.Build(plan, seed, SpruceDetail.Far);
            FineCrown = new UploadedMesh(graphicsDevice, near.Vertices, near.Indices, near.Bounds);
            FineSnow = new UploadedMesh(graphicsDevice, near.SnowVertices, near.SnowIndices, near.Bounds);
            FineCrownFar = new UploadedMesh(graphicsDevice, far.Vertices, far.Indices, far.Bounds);
            FineSnowFar = new UploadedMesh(graphicsDevice, far.SnowVertices, far.SnowIndices, far.Bounds);
            FineShade = new LatheMesh(graphicsDevice, SpruceCrownGeometry.ShadeProfileFor(plan, SpruceDetail.Near),
                SpruceCrownGeometry.SHADE_SEGMENTS, irregularityAmplitude: plan.WobbleAmplitude * SpruceDetail.Near.Shade, irregularityPhase: phase);
            FineShadeFar = new LatheMesh(graphicsDevice, SpruceCrownGeometry.ShadeProfileFor(plan, SpruceDetail.Far),
                SpruceCrownGeometry.SHADE_SEGMENTS, irregularityAmplitude: plan.WobbleAmplitude * SpruceDetail.Far.Shade, irregularityPhase: phase);
        }

        /// <summary>
        /// High and Ultra's spruce crown (#733, <c>SceneRenderer.FineMeshes</c>) for a tree near the arena: the whorls as
        /// separate boughs, each an arm that leaves the stem, sags and turns its tip up, a plate of foliage over it and a
        /// fringe hanging under it, laid out by the same whorl plan as <see cref="Crown"/> (<see cref="SpruceCrownGeometry"/>,
        /// <see cref="SpruceDetail.Near"/>). Null for a broadleaf. Drawn over <see cref="FineShade"/>, the darker interior
        /// the boughs grow out of.
        /// </summary>
        public IProceduralMesh FineCrown { get; }

        /// <summary>The same crown for the rest of the wood (<see cref="SpruceDetail.Far"/>): fewer, wider arms of fewer
        /// rings. Null for a broadleaf.</summary>
        public IProceduralMesh FineCrownFar { get; }

        /// <summary>
        /// The snow lying in lumps on some of <see cref="FineCrown"/>'s arms and <see cref="FineCrownFar"/>'s (#733), for a
        /// scene with crown snow to draw in the snow's colour. Null for a broadleaf.
        /// </summary>
        public IProceduralMesh FineSnow { get; }
        public IProceduralMesh FineSnowFar { get; }

        /// <summary>
        /// The interior of High and Ultra's spruce crown (#733): a narrow lathe inside the boughs of <see cref="FineCrown"/>,
        /// drawn in a darker tint of its own — the shaded old needles round the stem that the gaps between the boughs open
        /// onto. Null for a broadleaf.
        /// </summary>
        public IProceduralMesh FineShade { get; }

        /// <summary>The far crown's interior, fatter under its fewer arms (<see cref="SpruceDetail.Shade"/>). Null for a broadleaf.</summary>
        public IProceduralMesh FineShadeFar { get; }

        //A broadleaf's grown wood and crown, which this owns and disposes
        private readonly MeadowTreeMesh _grown;

        /// <summary>
        /// The conifer canopy: a spruce built as <b>tiers of branch whorls</b>. Each tier is a skirt whose
        /// outer edge droops below where it leaves the stem — a spruce's branches hang — with a shadowed
        /// tuck under it where the next layer emerges narrower; the sawtooth silhouette that pattern cuts is
        /// what says "spruce" from any distance, where a smooth cone (the first build) said "toy". The tier
        /// count, the taper's curve, the droop and the tuck depth are all rolled per mesh, so two spruce
        /// variants differ in structure rather than in proportions alone. Skirt rings crease (a branch layer
        /// ends in a hard edge) and carry the full wobble; the tucks stay smooth and take half, being stem
        /// shadow rather than foliage edge. The underside disc faces down: from below (a tree on a hill) a
        /// conifer is its own shadow.
        /// </summary>
        private static List<LathePoint> PlanConiferCrown(float radius, float height, float baseY, Random rng, float phase,
            int minTiers, int tierSpread, float raggedness, out SpruceWhorlPlan plan)
        {
            int tiers = Math.Max(2, minTiers) + rng.Next(Math.Max(1, tierSpread));   //4..6 whorls by default
            float taper = 0.78f + 0.30f * (float)rng.NextDouble();   //how fast the skirts widen downwards
            float pinch = 0.52f + 0.12f * (float)rng.NextDouble();   //how far under a skirt the stem shows
            float tierHeight = height / tiers;

            //The skirts' edges as they are rolled, for High and Ultra's crown (#733) to lay its boughs out by
            var skirtRadii = new float[tiers];
            var skirtHeights = new float[tiers];

            //A ring near the tip is narrower than the wobble's peak, and a displacement past the axis turns
            //the ring inside out — so each ring's wobble is capped at a share of its own radius (against the
            //largest amplitude rolled below, displacement stays under half the ring). The raggedness scales
            //that amplitude, so it scales the cap with it.
            float SafeWobble(float ringRadius, float cap) => MathF.Min(cap, ringRadius / (radius * 0.40f * raggedness));

            var profile = new List<LathePoint> { new(0f, baseY + height) };

            float previousSkirtRadius = 0f;
            float previousSkirtY = baseY + height;

            for (int t = 1; t <= tiers; t++)
            {
                float u = t / (float)tiers;

                //The skirt widens down the crown on the rolled taper, jittered per tier — but never narrower
                //than the layer above it, or the silhouette inverts into something no spruce grows. The
                //jitter's width is the raggedness: ±10 % at 1, the forest as it was built.
                float skirtRadius = radius * MathF.Pow(u, taper)
                    * (1f - 0.1f * raggedness + 0.2f * raggedness * (float)rng.NextDouble());
                skirtRadius = MathF.Max(skirtRadius, previousSkirtRadius * 1.06f);

                float droop = tierHeight * (0.12f + 0.16f * (float)rng.NextDouble());
                float skirtY = baseY + height * (1f - u) - droop;

                //The tuck under the previous skirt: the run in-and-down is the branch layer's shadowed
                //underside, and the pinch is where the next whorl leaves the stem.
                if (t > 1)
                {
                    float pinchRadius = previousSkirtRadius * pinch;
                    profile.Add(new LathePoint(pinchRadius, previousSkirtY - tierHeight * 0.16f,
                        wobble: SafeWobble(pinchRadius, 0.55f)));
                }

                profile.Add(new LathePoint(skirtRadius, skirtY, crease: true, wobble: SafeWobble(skirtRadius, 1f)));
                skirtRadii[t - 1] = skirtRadius;
                skirtHeights[t - 1] = skirtY;

                previousSkirtRadius = skirtRadius;
                previousSkirtY = skirtY;
            }

            //The underside disc closes at the lowest skirt's own height (which droops below baseY; branch
            //tips hang past the stem's shoulder on a real spruce).
            profile.Add(new LathePoint(0f, previousSkirtY));

            float wobbleAmplitude = radius * (0.12f + 0.06f * (float)rng.NextDouble()) * raggedness;
            plan = new SpruceWhorlPlan(radius, height, baseY, phase, wobbleAmplitude, raggedness, skirtRadii, skirtHeights);
            return profile;
        }

        /// <summary>
        /// Everything a spruce rolls from its seed, in the order it has always rolled it: the root flare, the trunk's wobble,
        /// then the crown's tiers (<see cref="PlanConiferCrown"/>). One place, so the constructor and a test reading the
        /// whorl plan without a device (<see cref="PlanConifer"/>) cannot roll it differently.
        /// </summary>
        private static (float Flare, float TrunkWobble, List<LathePoint> CrownProfile, SpruceWhorlPlan Plan) RollConifer(
            float trunkBaseRadius, float trunkHeight, float crownRadius, float crownHeight, int seed,
            int tiers, int tierSpread, float raggedness)
        {
            Random rng = new(seed);

            //Spreads the meshes' wobble patterns apart; the golden angle keeps consecutive seeds from
            //landing on nearby phases of the low-frequency terms.
            float phase = seed * 2.39996f;

            float flare = 1.2f + 0.25f * (float)rng.NextDouble();

            //Bark is rough, but a trunk is still a trunk — a smaller share of the radius than a rock's wobble.
            float trunkWobble = trunkBaseRadius * (0.06f + 0.05f * (float)rng.NextDouble());

            List<LathePoint> crown = PlanConiferCrown(crownRadius, crownHeight, trunkHeight, rng, phase, tiers, tierSpread,
                raggedness, out SpruceWhorlPlan plan);
            return (flare, trunkWobble, crown, plan);
        }

        /// <summary>The whorl plan a spruce of these figures and seed is built by (#733), with no device — for a test.</summary>
        internal static SpruceWhorlPlan PlanConifer(float trunkBaseRadius, float trunkHeight, float crownRadius,
            float crownHeight, int seed, int tiers = 4, int tierSpread = 3, float raggedness = 1f) =>
            RollConifer(trunkBaseRadius, trunkHeight, crownRadius, crownHeight, seed, tiers, tierSpread, raggedness).Plan;

        public void Dispose()
        {
            if (_grown != null)
            {
                _grown.Dispose();
                return;
            }
            (Trunk as IDisposable)?.Dispose();
            (Crown as IDisposable)?.Dispose();
            (FineCrown as IDisposable)?.Dispose();
            (FineShade as IDisposable)?.Dispose();
            (FineSnow as IDisposable)?.Dispose();
            (FineCrownFar as IDisposable)?.Dispose();
            (FineSnowFar as IDisposable)?.Dispose();
            (FineShadeFar as IDisposable)?.Dispose();
        }

    }

    /// <summary>Which crown a <see cref="TreeMesh"/> carries: a tiered spruce or a broadleaf's dome of clumps.</summary>
    public enum TreeSpecies { Conifer, Broadleaf }
}
