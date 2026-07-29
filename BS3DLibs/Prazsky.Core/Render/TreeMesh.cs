using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A forest tree: a tapering, irregular trunk (<see cref="Trunk"/>) under a lumpy, irregularly bulging
    /// canopy (<see cref="Crown"/>). Two meshes, because it is two materials — bark and foliage — which a
    /// single instanced draw cannot tint differently (the diffuse colour is per-draw, not per-instance). The
    /// pattern is the island's: one object, two <see cref="IProceduralMesh"/>es, two renderers.
    /// <para>
    /// The trunk is a <see cref="LatheMesh"/> (a surface of revolution with a modest bark irregularity); the
    /// crown is a UV sphere whose vertices are pushed along their normals by a 3D noise field, so the canopy
    /// bulges unevenly the way a real one does rather than reading as a ball. Crown radius sits over the
    /// trunk's tapering top, so the two meet without a gap.
    /// </para>
    /// </summary>
    public sealed class TreeMesh : IDisposable
    {
        /// <summary>The trunk: a tapering bark cylinder, irregular enough that no two read identical.</summary>
        public LatheMesh Trunk { get; }

        /// <summary>The canopy: a noise-bulged sphere above the trunk's top.</summary>
        public IProceduralMesh Crown { get; }

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="trunkBaseRadius">Trunk radius at the ground.</param>
        /// <param name="trunkTopRadius">Trunk radius at the top (a trunk tapers).</param>
        /// <param name="trunkHeight">Trunk height to the underside of the canopy.</param>
        /// <param name="crownRadius">Canopy radius.</param>
        /// <param name="crownHeight">Canopy height above the trunk top (taller than wide reads conical,
        /// wider than tall reads broadleaf).</param>
        /// <param name="segments">Facets around the trunk axis. The crown uses its own slice/stack count.</param>
        public TreeMesh(GraphicsDevice graphicsDevice,
            float trunkBaseRadius, float trunkTopRadius, float trunkHeight,
            float crownRadius, float crownHeight, int segments = 8)
        {
            var trunkProfile = new List<LathePoint>
            {
                new(0f,             0f),
                new(trunkBaseRadius, 0f,            crease: true),    //hard arris at the root flare
                new(trunkBaseRadius, trunkHeight * 0.5f, wobble: 1f),
                new(trunkTopRadius,  trunkHeight,    wobble: 0.7f),
                new(trunkTopRadius,  trunkHeight,    crease: true)     //closed top under the canopy
            };

            //Bark is rough, but a trunk is still a trunk — a smaller share of the radius than a rock's wobble.
            Trunk = new LatheMesh(graphicsDevice, trunkProfile, segments, irregularityAmplitude: trunkBaseRadius * 0.08f);

            Crown = new CrownMesh(graphicsDevice, crownRadius, crownHeight, trunkHeight);
        }

        public void Dispose()
        {
            Trunk?.Dispose();
            (Crown as IDisposable)?.Dispose();
        }

        /// <summary>
        /// A UV sphere whose vertices are displaced along their normals by a 3D noise field, so the canopy is
        /// an uneven cluster of bulges rather than a clean ball. Separated from <see cref="SphereMesh"/>
        /// because the displacement and its re-normalisation are the whole point of the crown; a plain sphere
        /// reads as a painted ball at the scale a tree stands in the scene.
        /// </summary>
        private sealed class CrownMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            internal CrownMesh(GraphicsDevice graphicsDevice, float radius, float height, float baseY)
            {
                //A canopy taller than its radius reads as a conifer; the caller scales height, the sphere is
                //stretched to it. Stretch is applied to positions only — normals stay spherical so the lighting
                //still wraps the canopy smoothly (a true ellipsoid normal would flatten the top highlight).
                float stretch = height / radius;
                int slices = 10;
                int stacks = 7;

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

                //The crown is slightly flattened at the bottom where it meets the trunk, so the join reads as
                //foliage settling over the trunk top rather than a ball balanced on a stick. Carried by the
                //displacement, not the stretch: the bottom bulges inward.
                float BottomTuck(float y) => MathHelper.SmoothStep(0f, 0.35f, y) * 0.4f;

                vertices[0] = BuildVertex(Vector3.Up, radius, stretch, baseY, Bulge, BottomTuck);

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
                        vertices[index++] = BuildVertex(normal, radius, stretch, baseY, Bulge, BottomTuck);
                    }
                }

                int bottomPole = index;
                vertices[bottomPole] = BuildVertex(Vector3.Down, radius, stretch, baseY, Bulge, BottomTuck);

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

                //The bulge can push the surface past the nominal radius, and the stretch makes the canopy
                //taller than wide — bound by the larger of the two axes plus the bulge headroom.
                float bulgeHeadroom = radius * 0.5f;
                float bound = MathF.Max(radius + bulgeHeadroom, height + bulgeHeadroom);
                BoundingSphere = new BoundingSphere(new Vector3(0f, baseY + height, 0f), bound);
            }

            //Displaces a unit direction into a canopy vertex: stretch it to the canopy's height/width, push it
            //along its normal by the bulge field, tuck the bottom in over the trunk, and sit it on the trunk
            //top. The normal is kept spherical (the pre-displacement direction) so the lighting wraps the bumps
            //smoothly — modelling the bulges into the normal would sharpen them into dimples instead.
            private static VertexPositionNormalTexture BuildVertex(Vector3 dir, float radius, float stretch, float baseY,
                Func<Vector3, float> bulge, Func<float, float> bottomTuck)
            {
                float b = bulge(dir);
                float tuck = bottomTuck(dir.Y);
                float r = radius * (1f + 0.18f * b) * (1f - tuck);
                Vector3 position = new(dir.X * r, dir.Y * radius * stretch + baseY, dir.Z * r);

                Vector2 uv = new(MathHelper.Clamp(dir.X * 0.5f + 0.5f, 0f, 1f), MathHelper.Clamp(dir.Y * 0.5f + 0.5f, 0f, 1f));
                return new VertexPositionNormalTexture(position, dir, uv);
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
}
