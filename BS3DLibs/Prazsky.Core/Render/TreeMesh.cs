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
    /// Two species share the class, chosen by <see cref="TreeSpecies"/>: a <b>conifer</b> — a tall, mildly
    /// concave cone (a <see cref="LatheMesh"/> whose radial irregularity breaks the machined silhouette into
    /// a spruce) over a short trunk — and a <b>broadleaf</b> — a noise-bulged canopy over a taller trunk. The
    /// crown of either species spans <c>[trunkHeight, trunkHeight + crownHeight]</c> — exactly for the
    /// conifer, whose lathe wobbles radially only, and within its noise swell for the broadleaf — so the
    /// trunk stays visible under it; the first build used the height as a semi-axis, which buried the whole
    /// trunk inside an egg resting on the ground.
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

        /// <summary>The canopy, sitting on the trunk's top: a cone for a conifer, a bulged mass for a broadleaf.</summary>
        public IProceduralMesh Crown { get; }

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="species">Which of the two crown shapes the tree gets.</param>
        /// <param name="trunkBaseRadius">Trunk radius up the flank; the root flare at the ground is wider.</param>
        /// <param name="trunkTopRadius">Trunk radius at the top (a trunk tapers).</param>
        /// <param name="trunkHeight">Trunk height to the underside of the canopy.</param>
        /// <param name="crownRadius">Canopy radius (a conifer's skirt, a broadleaf's half-width).</param>
        /// <param name="crownHeight">Canopy height, trunk top to crown top — the crown spans this.</param>
        /// <param name="segments">Facets around the trunk axis. The crown uses its own facet count.</param>
        public TreeMesh(GraphicsDevice graphicsDevice, TreeSpecies species,
            float trunkBaseRadius, float trunkTopRadius, float trunkHeight,
            float crownRadius, float crownHeight, int segments = 8)
        {
            //Top rim, down the flank, out into a root flare, and in along the buried underside. No top cap:
            //the crown of either species closes over the trunk's top rim, so a cap would never be seen.
            var trunkProfile = new List<LathePoint>
            {
                new(trunkTopRadius,         trunkHeight,        wobble: 0.7f),
                new(trunkBaseRadius,        trunkHeight * 0.3f, wobble: 1f),
                new(trunkBaseRadius * 1.3f, 0f,                 crease: true, wobble: 1f), //root flare into the ground
                new(0f,                     0f)
            };

            //Bark is rough, but a trunk is still a trunk — a smaller share of the radius than a rock's wobble.
            Trunk = new LatheMesh(graphicsDevice, trunkProfile, segments, irregularityAmplitude: trunkBaseRadius * 0.08f);

            Crown = species == TreeSpecies.Conifer
                ? BuildConiferCrown(graphicsDevice, crownRadius, crownHeight, trunkHeight)
                : new BroadleafCrownMesh(graphicsDevice, crownRadius, crownHeight, trunkHeight);
        }

        /// <summary>
        /// The conifer canopy: a mildly concave cone from a wide skirt at the trunk top to a tip, traced tip →
        /// skirt → underside. The lathe's radial irregularity is what turns the machined cone into a spruce —
        /// it is large enough to break the silhouette, and its height term varies it along the cone so the
        /// flank undulates rather than fluting straight down. The underside disc faces down: from below (a
        /// tree on a hill) a conifer is its own shadow.
        /// </summary>
        private static LatheMesh BuildConiferCrown(GraphicsDevice graphicsDevice, float radius, float height, float baseY)
        {
            var profile = new List<LathePoint>
            {
                new(0f,             baseY + height),
                new(radius * 0.34f, baseY + height * 0.66f, wobble: 0.8f),
                new(radius * 0.62f, baseY + height * 0.34f, wobble: 1f),  //slightly inside the straight cone:
                new(radius * 0.85f, baseY + height * 0.12f, wobble: 1f),  //a spruce's flank sags concave
                new(radius,         baseY,                  crease: true, wobble: 1f),
                new(0f,             baseY)
            };

            return new LatheMesh(graphicsDevice, profile, segments: 10, irregularityAmplitude: radius * 0.16f);
        }

        public void Dispose()
        {
            Trunk?.Dispose();
            (Crown as IDisposable)?.Dispose();
        }

        /// <summary>
        /// The broadleaf canopy: a UV sphere whose vertices are displaced along their normals by a 3D noise
        /// field, so the canopy is an uneven cluster of bulges rather than a clean ball. Separated from
        /// <see cref="SphereMesh"/> because the displacement and its re-normalisation are the whole point of
        /// the crown; a plain sphere reads as a painted ball at the scale a tree stands in the scene.
        /// </summary>
        private sealed class BroadleafCrownMesh : IProceduralMesh, IDisposable
        {
            /// <summary>
            /// How far the noise swells a vertex past the nominal surface, as a share of the semi-axis it
            /// rides. <see cref="LatheMesh.Irregularity"/>'s amplitudes sum to 1, so this is a true bound.
            /// Generous, because the crown is only ever read as a <b>silhouette</b> against the sky or the
            /// hills: its normals stay spherical (see BuildVertex), so a timid swell leaves a green ball with
            /// nothing but its outline to say otherwise, and the outline is exactly what this moves.
            /// </summary>
            private const float BULGE_SWELL = 0.3f;

            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            internal BroadleafCrownMesh(GraphicsDevice graphicsDevice, float radius, float height, float baseY)
            {
                //The crown spans [baseY, baseY + height] nominally — the noise swell reaches a little past
                //both ends: centred half way up, the vertical semi-axis is half the height. Stretch is
                //applied to positions only — normals stay spherical so the lighting still wraps the canopy
                //smoothly (a true ellipsoid normal would flatten the top highlight).
                float centreY = baseY + height * 0.5f;
                float semiY = height * 0.5f;
                int slices = 12;
                int stacks = 8;

                int vertexCount = (stacks - 1) * slices + 2;
                var vertices = new VertexPositionNormalTexture[vertexCount];

                //Sample LatheMesh.Irregularity three times at angles derived from the spherical direction, so
                //the noise is a function of the full 3D position rather than the latitude/longitude alone — a
                //pure lat/lon field would leave the crown rotationally symmetric about its pole.
                float Bulge(Vector3 dir)
                {
                    float a = MathF.Atan2(dir.Z, dir.X);
                    float noise = 0.5f * LatheMesh.Irregularity(a, dir.Y * 3f)
                        + 0.3f * LatheMesh.Irregularity(a + 2.1f, dir.Y * 3f + 1.7f)
                        + 0.2f * LatheMesh.Irregularity(a + 4.3f, dir.Y * 3f + 3.4f);
                    return noise;
                }

                vertices[0] = BuildVertex(Vector3.Up, radius, semiY, centreY, Bulge);

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
                        vertices[index++] = BuildVertex(normal, radius, semiY, centreY, Bulge);
                    }
                }

                int bottomPole = index;
                vertices[bottomPole] = BuildVertex(Vector3.Down, radius, semiY, centreY, Bulge);

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

                //The bulge pushes the surface past the nominal radius on BOTH axes (BuildVertex scales the
                //vertical semi-axis by the same swell), so the headroom is a share of the larger semi-axis
                //rather than of the radius: keyed to the radius alone the bound fails as soon as the crown is
                //taller than it is wide, which the narrow broadleaf variant already is.
                float bound = MathF.Max(radius, semiY) * (1f + BULGE_SWELL);
                BoundingSphere = new BoundingSphere(new Vector3(0f, centreY, 0f), bound);
            }

            //Displaces a unit direction into a canopy vertex: scale it to the canopy's half-width/half-height,
            //push it along its direction by the bulge field, tuck the underside in over the trunk, and centre
            //it half way up the crown's span. The normal is kept spherical (the pre-displacement direction) so
            //the lighting wraps the bumps smoothly — modelling the bulges into the normal would sharpen them
            //into dimples instead.
            private static VertexPositionNormalTexture BuildVertex(Vector3 dir, float radius, float semiY, float centreY,
                Func<Vector3, float> bulge)
            {
                float swell = 1f + BULGE_SWELL * bulge(dir);
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

    /// <summary>Which crown a <see cref="TreeMesh"/> carries: a spruce cone or a bulged broadleaf canopy.</summary>
    public enum TreeSpecies { Conifer, Broadleaf }
}
