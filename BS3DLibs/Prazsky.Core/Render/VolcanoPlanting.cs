using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>What stands on the volcano's lava field</b> (#679's second step). The #671 audit found the field between the
    /// island and the cone an even black plain; the references of an active field (<c>C:\Users\panrd\AI\sd\out\679-klein</c>
    /// and <c>679-zimage</c>: a fumarole field, a vent, an aa block field, spatter cones) all stand the same three things on
    /// it, and this plants them as <see cref="ScatterBucket"/>s for a <see cref="PlantPass"/>, casting into the sun's map:
    /// <list type="bullet">
    /// <item><b>Blocks</b> — jagged dark basalt of every size, most of it small, a few as big as a car, the aa flow's rubble.</item>
    /// <item><b>Bombs</b> — rounder, smaller lumps thrown out of the crater and lying where they fell.</item>
    /// <item><b>Spatter cones</b> — the strongest fumaroles stand on a small lathed cone with a crater in its top, and the
    /// steam rises out of that; a vent in bare ground is a crack, a vent that has spat for a while builds a chimney.</item>
    /// </list>
    /// None stands on the island, in a river's line or on a vent's own mouth. Deterministic off one seed.
    /// </summary>
    public sealed class VolcanoPlanting : IDisposable
    {
        private readonly List<IDisposable> _owned = new();

        /// <summary>The blocks, the bombs and the cones, in draw order.</summary>
        public ScatterBucket[] Buckets { get; }

        //Fresh basalt, nearly black, and the grey-brown it weathers and dusts to; the cones are the same rock, scoriated.
        //Dark: at 0.03-0.055 under the night dome's ambient the first cut read paler than the crust it lay on
        private static readonly Vector3 BASALT = new(0.028f, 0.026f, 0.025f);
        private static readonly Vector3 BASALT_DUSTED = new(0.060f, 0.052f, 0.045f);

        //How many of each, how far from the arena, and how big (a rock mesh is a unit across)
        private const int BLOCKS = 1400, BOMBS = 600;
        private const float NEAREST = ArenaIsland.RADIUS + 8f, FARTHEST = 300f;

        /// <param name="height">The volcano's ground (<see cref="TerrainMirror.Volcano"/>).</param>
        /// <param name="keepOut">True where nothing may stand: a river's line, the cone's crater.</param>
        /// <param name="cones">The spatter cones: where each stands (on the ground), its base radius and its height.</param>
        public VolcanoPlanting(GraphicsDevice device, Func<float, float, float> height, Func<float, float, bool> keepOut,
            IReadOnlyList<(Vector3 Foot, float Radius, float Height)> cones, int seed)
        {
            var buckets = new List<ScatterBucket>();
            Random rng = new(seed);

            //--- The blocks: six jagged, flat-faced shapes (BlockMesh). ⚠ The first cut used the boulders' RockMesh, a
            //lathe, and every block came out a smooth dome - a field of mushroom caps, where aa rubble is all edges
            var blockMeshes = new UploadedMesh[6];
            var blockInstances = new List<ModelInstance>[6];
            for (int m = 0; m < blockMeshes.Length; m++)
            {
                blockMeshes[m] = Own(BlockMesh(device, rng));
                blockInstances[m] = new List<ModelInstance>();
            }

            //--- The bombs: rounder and smoother, on more facets
            var bombMeshes = new RockMesh[2];
            var bombInstances = new List<ModelInstance>[2];
            for (int m = 0; m < bombMeshes.Length; m++)
            {
                bombMeshes[m] = Own(new RockMesh(device, 1f, 0.75f + 0.15f * m, 14, 11.3f + m * 1.9f));
                bombInstances[m] = new List<ModelInstance>();
            }

            bool Clear(float x, float z, float r)
            {
                if (keepOut(x, z)) return false;
                foreach ((Vector3 foot, float radius, float _) in cones)
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(foot.X, foot.Z)) < radius + r) return false;
                return true;
            }

            void Place(List<ModelInstance>[] into, int count, float sizeMin, float sizeMax, float sizePower, float sink)
            {
                for (int i = 0, tries = 0; i < count && tries < count * 20; tries++)
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float d = MathF.Sqrt(MathHelper.Lerp(NEAREST * NEAREST, FARTHEST * FARTHEST, (float)rng.NextDouble()));
                    float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
                    //Most small, a few big: the roll raised to a power
                    float size = MathHelper.Lerp(sizeMin, sizeMax, MathF.Pow((float)rng.NextDouble(), sizePower));
                    float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float tilt = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                    float dust = (float)rng.NextDouble();
                    float jitter = ((float)rng.NextDouble() - 0.5f) * 0.3f;
                    int variant = rng.Next(into.Length);
                    if (!Clear(x, z, size)) continue;

                    //Sunk to the lowest of the ground under its footprint, so no side hangs over a slope (#658)
                    float ground = MathF.Min(height(x, z), MathF.Min(MathF.Min(height(x + size, z), height(x - size, z)),
                        MathF.Min(height(x, z + size), height(x, z - size))));
                    into[variant].Add(new ModelInstance(
                        Matrix.CreateScale(size) * Matrix.CreateRotationX(tilt) * Matrix.CreateRotationY(yaw)
                            * Matrix.CreateTranslation(x, ground - size * sink, z),
                        new Vector4(dust, jitter, 0f, 0f)));
                    i++;
                }
            }

            Place(blockInstances, BLOCKS, 0.5f, 5f, 3f, 0.25f);
            Place(bombInstances, BOMBS, 0.35f, 1.3f, 2f, 0.3f);

            for (int m = 0; m < blockMeshes.Length; m++)
                if (blockInstances[m].Count > 0)
                    buckets.Add(Own(new ScatterBucket(device, blockMeshes[m], blockInstances[m], BASALT, BASALT_DUSTED,
                        dapple: 0.3f, bark: 0.35f, detailOnly: false)));
            for (int m = 0; m < bombMeshes.Length; m++)
                if (bombInstances[m].Count > 0)
                    buckets.Add(Own(new ScatterBucket(device, bombMeshes[m], bombInstances[m], BASALT, BASALT_DUSTED,
                        dapple: 0.2f, bark: 0.15f, detailOnly: false)));

            //--- The spatter cones: one lathe, scaled per cone to its radius and height
            if (cones.Count > 0)
            {
                var profile = new List<LathePoint>
                {
                    new(0f, 0.72f),                            //the crater's floor, down inside the top
                    new(0.22f, 0.86f, wobble: 0.6f),
                    new(0.30f, 1f, crease: true, wobble: 1f),  //the rim
                    new(0.62f, 0.45f, wobble: 1f),             //the steep flank of stacked spatter
                    new(1f, 0.02f, crease: true, wobble: 1f),  //its foot, spread onto the field
                    new(0.9f, -0.35f),
                    new(0f, -0.35f),
                };
                IProceduralMesh coneMesh = Own(new LatheMesh(device, profile, 16, irregularityAmplitude: 0.12f, irregularityPhase: 2.7f));
                var coneInstances = new List<ModelInstance>();
                foreach ((Vector3 foot, float radius, float coneHeight) in cones)
                    coneInstances.Add(new ModelInstance(
                        Matrix.CreateScale(radius, coneHeight, radius) * Matrix.CreateRotationY((float)rng.NextDouble() * MathHelper.TwoPi)
                            * Matrix.CreateTranslation(foot),
                        new Vector4(0.2f, 0f, 0f, 0f)));
                buckets.Add(Own(new ScatterBucket(device, coneMesh, coneInstances, BASALT * 1.2f, BASALT_DUSTED,
                    dapple: 0.35f, bark: 0.5f, detailOnly: false)));
            }

            Buckets = buckets.ToArray();
        }

        /// <summary>
        /// One broken block of basalt: an icosahedron with every vertex pushed in or out at random and the whole squashed
        /// a little, shaded flat so every face is its own facet - the angular rubble of an aa flow. Each face wound
        /// clockwise seen from outside (CLAUDE.md, "Triangle winding"), which is checked per face against its centroid.
        /// </summary>
        private static UploadedMesh BlockMesh(GraphicsDevice device, Random rng)
        {
            float t = (1f + MathF.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            float squash = 0.55f + 0.3f * (float)rng.NextDouble();
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 c = Vector3.Normalize(corners[i]) * (0.62f + 0.55f * (float)rng.NextDouble()) * 0.5f;
                corners[i] = new Vector3(c.X, c.Y * squash + 0.18f, c.Z);
            }

            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = corners[faces[f]], b = corners[faces[f + 1]], c = corners[faces[f + 2]];
                Vector3 outward = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                if (Vector3.Dot(outward, (a + b + c) / 3f - new Vector3(0f, 0.18f, 0f)) < 0f) outward = -outward;
                else (b, c) = (c, b);   //(b - a) x (c - a) points inward: this renderer's front face
                short n = (short)v.Count;
                v.Add(new VertexPositionNormalTexture(a, outward, Vector2.Zero));
                v.Add(new VertexPositionNormalTexture(b, outward, Vector2.UnitX));
                v.Add(new VertexPositionNormalTexture(c, outward, Vector2.UnitY));
                idx.Add(n); idx.Add((short)(n + 1)); idx.Add((short)(n + 2));
            }
            return new UploadedMesh(device, v, idx, new BoundingSphere(new Vector3(0f, 0.18f, 0f), 0.7f));
        }

        private T Own<T>(T item) where T : IDisposable
        {
            _owned.Add(item);
            return item;
        }

        public void Dispose()
        {
            foreach (IDisposable d in _owned) d.Dispose();
            _owned.Clear();
        }
    }
}
