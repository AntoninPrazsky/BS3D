using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The island's bank (#608) as a scene with no planting of its own draws it: the earthen band, the cover over the
    /// rest and a few stones set into it, as <see cref="ScatterBucket"/>s for a <see cref="PlantPass"/>. The forest and the
    /// mountains carry it; the meadow and the savanna build the same two meshes into their own scatter, where the
    /// stones and the tufts join the scene's own. In the forest the cover is moss over litter, in the mountains a drift
    /// of snow over scree — what the issue asked of each (<i>"turf and exposed soil in the forest, scree in the
    /// mountains"</i>).
    /// </summary>
    internal sealed class IslandBermPlanting : IDisposable
    {
        private readonly List<IDisposable> _owned = new();

        /// <summary>The earthen band, the cover and the stones, in draw order.</summary>
        public ScatterBucket[] Buckets { get; }

        /// <param name="height">The scene's ground at a world point (its <see cref="TerrainMirror"/>).</param>
        /// <param name="seed">Rolls the bank's wander and where the stones lie.</param>
        /// <param name="earth">The band against the drum (linear), and <paramref name="earthDry"/> the shade it mottles to.</param>
        /// <param name="cover">The cover over the rest of the bank (linear), mottled towards <paramref name="coverDry"/>.</param>
        /// <param name="stone">The stones set into it (linear).</param>
        /// <param name="stones">How many.</param>
        /// <param name="stoneRadius">How far from the arena's centre they lie, least and most.</param>
        /// <param name="stoneSize">How big, least and most (the rock mesh is a unit across).</param>
        public IslandBermPlanting(GraphicsDevice device, Func<float, float, float> height, int seed,
            Vector3 earth, Vector3 earthDry, Vector3 cover, Vector3 coverDry, float coverDapple, Vector3 stone, int stones,
            (float Min, float Max) stoneRadius, (float Min, float Max) stoneSize)
        {
            var buckets = new List<ScatterBucket>();
            var at = new List<ModelInstance> { new(Matrix.Identity, new Vector4(0.3f, 0f, 0f, 0f)) };

            IProceduralMesh earthMesh = Own(IslandBerm.Build(device, height, seed, 0, IslandBerm.EARTH_TO));
            IProceduralMesh coverMesh = Own(IslandBerm.Build(device, height, seed, IslandBerm.EARTH_TO, IslandBerm.RINGS - 1));
            buckets.Add(Own(new ScatterBucket(device, earthMesh, at, earth, earthDry, dapple: 0.6f, bark: 0f, detailOnly: false)));
            buckets.Add(Own(new ScatterBucket(device, coverMesh, at, cover, coverDry, dapple: coverDapple, bark: 0f, detailOnly: false)));

            //The stones: three rolled shapes, set into the bank or strewn past its foot
            Random rng = new(seed);
            var rockMeshes = new RockMesh[3];
            var rockInstances = new List<ModelInstance>[3];
            for (int m = 0; m < rockMeshes.Length; m++)
            {
                rockMeshes[m] = Own(new RockMesh(device, 1f, 0.6f + 0.25f * (float)rng.NextDouble(), 14, m * 1.7f));
                rockInstances[m] = new List<ModelInstance>();
            }
            for (int i = 0; i < stones; i++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float d = MathHelper.Lerp(stoneRadius.Min, stoneRadius.Max, (float)rng.NextDouble());
                float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
                float size = MathHelper.Lerp(stoneSize.Min, stoneSize.Max, (float)rng.NextDouble());
                float ground = MathF.Max(height(x, z), IslandBerm.SurfaceY(height, seed, x, z));
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float jitter = (float)(rng.NextDouble() - 0.5) * 0.2f;
                rockInstances[rng.Next(3)].Add(new ModelInstance(
                    Matrix.CreateScale(size) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, ground - size * 0.15f, z),
                    new Vector4(0f, jitter, 0f, 0f)));
            }
            for (int m = 0; m < rockMeshes.Length; m++)
                if (rockInstances[m].Count > 0)
                    buckets.Add(Own(new ScatterBucket(device, rockMeshes[m], rockInstances[m], stone, stone * new Vector3(0.8f, 1.0f, 0.7f),
                        dapple: 0.45f, bark: 0f, detailOnly: false)));

            Buckets = buckets.ToArray();
        }

        private T Own<T>(T item) where T : IDisposable
        {
            _owned.Add(item);
            return item;
        }

        public void Dispose()
        {
            foreach (IDisposable item in _owned) item.Dispose();
            _owned.Clear();
        }
    }
}
