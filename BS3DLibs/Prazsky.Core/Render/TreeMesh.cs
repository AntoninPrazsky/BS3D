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
    /// and a <b>broadleaf</b> — a crown of several overlapping <b>leaf lobes</b>, because a deciduous crown is
    /// a cluster of masses, not one ball. Both are rolled from the <paramref name="seed"/>: tier count, taper,
    /// droop and pinch for the spruce; lobe count, directions and weights for the broadleaf; and the lathe
    /// wobble's phase for every part — so two variants differ in structure, not merely in proportions, which
    /// is what the eye checks when it decides whether a wood is real. The crown of either species spans
    /// <c>[trunkHeight, trunkHeight + crownHeight]</c> give or take its droop and lobes, so the trunk stays
    /// visible under it; the first build used the height as a semi-axis, which buried the whole trunk inside
    /// an egg resting on the ground.
    /// </para>
    /// <para>
    /// All profiles trace <b>top → outside → underside</b>, the direction <see cref="LatheMesh"/> documents:
    /// traced the other way the solid comes out inside out — same silhouette, far side drawn, shading dark —
    /// which is exactly how the first forest looked.
    /// </para>
    /// </summary>
    public sealed class TreeMesh : IDisposable
    {
        /// <summary>The trunk: a tapering bark cylinder with a root flare, irregular enough that no two read identical.</summary>
        public LatheMesh Trunk { get; }

        /// <summary>The canopy, sitting on the trunk's top: tiered whorls for a conifer, a lobed mass for a broadleaf.</summary>
        public IProceduralMesh Crown { get; }

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="species">Which of the two crown shapes the tree gets.</param>
        /// <param name="trunkBaseRadius">Trunk radius up the flank; the root flare at the ground is wider.</param>
        /// <param name="trunkTopRadius">Trunk radius at the top (a trunk tapers).</param>
        /// <param name="trunkHeight">Trunk height to the underside of the canopy.</param>
        /// <param name="crownRadius">Canopy radius (a conifer's widest skirt, a broadleaf's half-width).</param>
        /// <param name="crownHeight">Canopy height, trunk top to crown top — the crown spans this.</param>
        /// <param name="seed">Rolls everything structural: the spruce's tier layout, the broadleaf's lobes,
        /// the wobble phases. The same seed always builds the same tree.</param>
        /// <param name="segments">Facets around the trunk axis. The crown uses its own facet count.</param>
        public TreeMesh(GraphicsDevice graphicsDevice, TreeSpecies species,
            float trunkBaseRadius, float trunkTopRadius, float trunkHeight,
            float crownRadius, float crownHeight, int seed = 0, int segments = 8)
        {
            Random rng = new(seed);

            //Spreads the meshes' wobble patterns apart; the golden angle keeps consecutive seeds from
            //landing on nearby phases of the low-frequency terms.
            float phase = seed * 2.39996f;

            //Top rim, down the flank, out into a root flare, and in along the buried underside. No top cap:
            //the crown of either species closes over the trunk's top rim, so a cap would never be seen. The
            //flare is rolled per tree — roots grip differently — inside a band where both extremes still
            //read as a standing trunk.
            float flare = 1.2f + 0.25f * (float)rng.NextDouble();
            var trunkProfile = new List<LathePoint>
            {
                new(trunkTopRadius,          trunkHeight,        wobble: 0.7f),
                new(trunkBaseRadius,         trunkHeight * 0.3f, wobble: 1f),
                new(trunkBaseRadius * flare, 0f,                 crease: true, wobble: 1f), //root flare into the ground
                new(0f,                      0f)
            };

            //Bark is rough, but a trunk is still a trunk — a smaller share of the radius than a rock's wobble.
            Trunk = new LatheMesh(graphicsDevice, trunkProfile, segments,
                irregularityAmplitude: trunkBaseRadius * (0.06f + 0.05f * (float)rng.NextDouble()),
                irregularityPhase: phase);

            Crown = species == TreeSpecies.Conifer
                ? BuildConiferCrown(graphicsDevice, crownRadius, crownHeight, trunkHeight, rng, phase)
                : new BroadleafCrownMesh(graphicsDevice, crownRadius, crownHeight, trunkHeight, rng, phase);
        }

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
        private static LatheMesh BuildConiferCrown(GraphicsDevice graphicsDevice, float radius, float height,
            float baseY, Random rng, float phase)
        {
            int tiers = 4 + rng.Next(3);                             //4..6 whorls
            float taper = 0.78f + 0.30f * (float)rng.NextDouble();   //how fast the skirts widen downwards
            float pinch = 0.52f + 0.12f * (float)rng.NextDouble();   //how far under a skirt the stem shows
            float tierHeight = height / tiers;

            //A ring near the tip is narrower than the wobble's peak, and a displacement past the axis turns
            //the ring inside out — so each ring's wobble is capped at a share of its own radius (against the
            //largest amplitude rolled below, displacement stays under half the ring).
            float SafeWobble(float ringRadius, float cap) => MathF.Min(cap, ringRadius / (radius * 0.40f));

            var profile = new List<LathePoint> { new(0f, baseY + height) };

            float previousSkirtRadius = 0f;
            float previousSkirtY = baseY + height;

            for (int t = 1; t <= tiers; t++)
            {
                float u = t / (float)tiers;

                //The skirt widens down the crown on the rolled taper, jittered per tier — but never narrower
                //than the layer above it, or the silhouette inverts into something no spruce grows.
                float skirtRadius = radius * MathF.Pow(u, taper) * (0.9f + 0.2f * (float)rng.NextDouble());
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

                previousSkirtRadius = skirtRadius;
                previousSkirtY = skirtY;
            }

            //The underside disc closes at the lowest skirt's own height (which droops below baseY; branch
            //tips hang past the stem's shoulder on a real spruce).
            profile.Add(new LathePoint(0f, previousSkirtY));

            //Fourteen facets, for the reason RockMesh documents: the wobble runs at 3, 7 and 13 waves per
            //revolution, and at ten facets the 7-wave term aliased down to a lateral shift — fine on a smooth
            //cone, but a skirt edge is the one ring the eye traces, so it gets the sampling to break honestly.
            return new LatheMesh(graphicsDevice, profile, segments: 14,
                irregularityAmplitude: radius * (0.12f + 0.06f * (float)rng.NextDouble()),
                irregularityPhase: phase);
        }

        public void Dispose()
        {
            Trunk?.Dispose();
            (Crown as IDisposable)?.Dispose();
        }

        /// <summary>
        /// The broadleaf canopy: a UV sphere displaced into a cluster of overlapping <b>leaf lobes</b> — each
        /// a smooth swell around its own rolled direction — over a base noise field. A deciduous crown is a
        /// cluster of masses, and it is the lobes that read as one: the first build's single noise-bulged
        /// ball still read as a ball, because gradient noise has no structure at the one scale a crown does.
        /// The lobe count, directions, weights and widths are rolled per mesh, so no two variants share a
        /// silhouette. Normals stay spherical (the pre-displacement direction), so the lighting wraps the
        /// lobes softly the way lit foliage does — modelling them into the normal would sharpen every lobe
        /// into a dented ball.
        /// </summary>
        private sealed class BroadleafCrownMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            internal BroadleafCrownMesh(GraphicsDevice graphicsDevice, float radius, float height, float baseY,
                Random rng, float phase)
            {
                //The leaf masses. Directions bias upward — a crown lobes at its top and sides, while its
                //underside is the shaded hollow the trunk disappears into — and each lobe carries its own
                //weight and angular width, so one crown holds both broad shoulders and small knuckles.
                int lobes = 5 + rng.Next(4);
                var lobeDirection = new Vector3[lobes];
                var lobeWeight = new float[lobes];
                var lobeSharpness = new float[lobes];

                for (int k = 0; k < lobes; k++)
                {
                    float ly = -0.15f + 1.05f * (float)rng.NextDouble();
                    float la = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float lr = MathF.Sqrt(MathF.Max(0f, 1f - ly * ly));

                    lobeDirection[k] = new Vector3(MathF.Cos(la) * lr, ly, MathF.Sin(la) * lr);
                    lobeWeight[k] = 0.10f + 0.22f * (float)rng.NextDouble();
                    lobeSharpness[k] = 2.5f + 4f * (float)rng.NextDouble();
                }

                //A base unevenness under the lobes, phase-shifted per mesh. Sampled three times at angles
                //derived from the spherical direction, so the noise is a function of the full 3D position
                //rather than the latitude/longitude alone — a pure lat/lon field would leave the crown
                //rotationally symmetric about its pole.
                const float NOISE_SWELL = 0.16f;
                float Bulge(Vector3 dir)
                {
                    float a = MathF.Atan2(dir.Z, dir.X) + phase;
                    float noise = 0.5f * LatheMesh.Irregularity(a, dir.Y * 3f + phase)
                        + 0.3f * LatheMesh.Irregularity(a + 2.1f, dir.Y * 3f + 1.7f + phase)
                        + 0.2f * LatheMesh.Irregularity(a + 4.3f, dir.Y * 3f + 3.4f + phase);

                    float swell = NOISE_SWELL * noise;

                    for (int k = 0; k < lobes; k++)
                    {
                        float toward = MathF.Max(0f, Vector3.Dot(dir, lobeDirection[k]));
                        swell += lobeWeight[k] * MathF.Pow(toward, lobeSharpness[k]);
                    }

                    return swell;
                }

                //The crown spans [baseY, baseY + height] nominally — the lobes reach past both ends. Stretch
                //is applied to positions only; normals stay spherical (see the class summary).
                float centreY = baseY + height * 0.5f;
                float semiY = height * 0.5f;
                int slices = 18;
                int stacks = 12;

                int vertexCount = (stacks - 1) * slices + 2;
                var vertices = new VertexPositionNormalTexture[vertexCount];

                //The real reach of the displaced surface, tracked while the vertices are built, so the bound
                //is exact rather than a guess at how far overlapping lobes can stack.
                float maxReach = 0f;

                VertexPositionNormalTexture Build(Vector3 dir)
                {
                    var vertex = BuildVertex(dir, radius, semiY, centreY, Bulge);
                    maxReach = MathF.Max(maxReach, (vertex.Position - new Vector3(0f, centreY, 0f)).Length());
                    return vertex;
                }

                vertices[0] = Build(Vector3.Up);

                int index = 1;
                for (int stack = 1; stack < stacks; stack++)
                {
                    float phi = MathF.PI * stack / stacks;
                    float y = MathF.Cos(phi);
                    float ringRadius = MathF.Sin(phi);

                    for (int slice = 0; slice < slices; slice++)
                    {
                        float theta = MathHelper.TwoPi * slice / slices;
                        Vector3 normal = new(ringRadius * MathF.Cos(theta), y, ringRadius * MathF.Sin(theta));
                        vertices[index++] = Build(normal);
                    }
                }

                int bottomPole = index;
                vertices[bottomPole] = Build(Vector3.Down);

                //Indices are the standard UV-sphere winding (clockwise seen from outside, MonoGame's front
                //face) — copied from SphereMesh, which documents the Y-flip that makes it so.
                PrimitiveCount = slices * 2 + (stacks - 2) * slices * 2;
                var indices = new short[PrimitiveCount * 3];
                int i = 0;

                for (int slice = 0; slice < slices; slice++)
                {
                    indices[i++] = 0;
                    indices[i++] = (short)(1 + slice);
                    indices[i++] = (short)(1 + (slice + 1) % slices);
                }

                for (int stack = 0; stack < stacks - 2; stack++)
                {
                    int upperRing = 1 + stack * slices;
                    int lowerRing = upperRing + slices;

                    for (int slice = 0; slice < slices; slice++)
                    {
                        int next = (slice + 1) % slices;

                        indices[i++] = (short)(upperRing + slice);
                        indices[i++] = (short)(lowerRing + slice);
                        indices[i++] = (short)(upperRing + next);

                        indices[i++] = (short)(upperRing + next);
                        indices[i++] = (short)(lowerRing + slice);
                        indices[i++] = (short)(lowerRing + next);
                    }
                }

                int lastRing = 1 + (stacks - 2) * slices;
                for (int slice = 0; slice < slices; slice++)
                {
                    indices[i++] = (short)bottomPole;
                    indices[i++] = (short)(lastRing + (slice + 1) % slices);
                    indices[i++] = (short)(lastRing + slice);
                }

                VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, vertexCount, BufferUsage.WriteOnly);
                VertexBuffer.SetData(vertices);

                IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
                IndexBuffer.SetData(indices);

                BoundingSphere = new BoundingSphere(new Vector3(0f, centreY, 0f), maxReach);
            }

            //Displaces a unit direction into a canopy vertex: scale it to the canopy's half-width/half-height,
            //push it along its direction by the lobe field, tuck the underside in over the trunk, and centre
            //it half way up the crown's span. The normal is kept spherical (the pre-displacement direction) so
            //the lighting wraps the lobes smoothly — modelling them into the normal would sharpen them into
            //dimples instead.
            private static VertexPositionNormalTexture BuildVertex(Vector3 dir, float radius, float semiY, float centreY,
                Func<Vector3, float> bulge)
            {
                float swell = 1f + bulge(dir);
                float tuck = BottomTuck(dir.Y);
                float r = radius * swell * (1f - tuck);
                Vector3 position = new(dir.X * r, dir.Y * semiY * swell + centreY, dir.Z * r);

                Vector2 uv = new(MathHelper.Clamp(dir.X * 0.5f + 0.5f, 0f, 1f), MathHelper.Clamp(dir.Y * 0.5f + 0.5f, 0f, 1f));
                return new VertexPositionNormalTexture(position, dir, uv);
            }

            //The crown narrows towards its underside, so the join reads as foliage settling over the trunk
            //top rather than a ball balanced on a stick. Strongest at the bottom pole and gone by the
            //equator — the first build ran this the other way up and pinched the TOP, which is what made
            //every crown an egg.
            private static float BottomTuck(float y)
            {
                float t = MathHelper.Clamp((-y - 0.2f) / 0.8f, 0f, 1f);
                return 0.45f * t * t;
            }

            public void Dispose()
            {
                VertexBuffer?.Dispose();
                VertexBuffer = null;
                IndexBuffer?.Dispose();
                IndexBuffer = null;
            }
        }
    }

    /// <summary>Which crown a <see cref="TreeMesh"/> carries: a tiered spruce or a lobed broadleaf canopy.</summary>
    public enum TreeSpecies { Conifer, Broadleaf }
}
