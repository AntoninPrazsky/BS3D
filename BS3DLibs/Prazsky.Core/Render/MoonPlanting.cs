using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The rocks on the Moon's plain</b> — the one thing every Apollo surface photograph is full of and the scene never
    /// had. #508 tried them painted into <c>Moon.fx</c> and they read as shadows without rocks; a rock stands up out of a
    /// plain seen at a grazing angle, so it wants geometry, and geometry wants the ground's height, which
    /// <see cref="TerrainMirror.Moon"/> now gives. Planted as <see cref="ScatterBucket"/>s for a <see cref="PlantPass"/>,
    /// casting into the sun's map, so each lies on the regolith with the long black shadow of a 16° sun off it:
    /// <list type="bullet">
    /// <item><b>The strewn field</b> — angular blocks and rounder weathered boulders of every size, most of them small,
    /// in patches rather than evenly: a real field is thick round the craters that threw it and thin between them.</item>
    /// <item><b>Rim blocks</b> — the young craters of the top octave (<see cref="OffworldGround.MoonCraters"/>, the same
    /// cells the shader cuts) wear a ring of big angular blocks on their lip and apron, as many as the crater is young:
    /// a fresh crater dug through the regolith into bedrock and threw it out in blocks, an old one has buried them.
    /// The chapter intro's crater shot looks straight at one.</item>
    /// <item><b>Landmarks</b> — a hundred-odd blocks the size of a house, out to the highland belt's crest: the only rocks
    /// the play camera sees, since the island's deck hides the plain in front of the belt from it.</item>
    /// </list>
    /// Nothing stands on the island's pad. Deterministic off one seed.
    /// </summary>
    public sealed class MoonPlanting : IDisposable
    {
        private readonly List<IDisposable> _owned = new();

        /// <summary>The blocks (strewn and on the rims, one bucket a shape) and the weathered boulders, in draw order.</summary>
        public ScatterBucket[] Buckets { get; }

        //How many strewn, how far from the arena (the pad's edge and a little), how many blocks a rim at most per unit of
        //its radius, and the young craters it takes (a crater's youth is the roll that makes it deep and its rim bright)
        private const int FIELD_BLOCKS = 2000, FIELD_BOULDERS = 800;
        private const float NEAREST = ArenaIsland.RADIUS + 3f, FARTHEST = 280f;
        private const float RIM_BLOCKS_PER_RADIUS = 6f, RIM_MIN_YOUTH = 0.35f, RIM_MIN_DEPTH = 1f;

        //THE FEW BIG ONES, and they are what the play camera sees. It looks up at the cluster from just over the island's
        //deck, and the deck hides the whole plain to about three hundred units out: what shows over it is the highland
        //belt's slope, where a rock the field's size is a pixel. A boulder the size of a house is two dozen, and every
        //Apollo traverse had its landmark ones (House Rock, the Station 6 boulder on the North Massif's slope). Out to the
        //belt's crest and few, so they stay landmarks: at a 70-degree frustum a dozen or two stand in any one view.
        private const int LANDMARKS = 110;
        private const float LANDMARK_NEAREST = 120f, LANDMARK_FARTHEST = 380f;

        /// <param name="height">The Moon's ground (<see cref="TerrainMirror.Moon"/>).</param>
        /// <param name="terrain">The plain's config: its crater cells, for the rim blocks, and its regolith greys.</param>
        public MoonPlanting(GraphicsDevice device, Func<float, float, float> height, MoonTerrainConfig terrain, int seed)
        {
            var buckets = new List<ScatterBucket>();
            Random rng = new(seed);

            //Rock the regolith has dusted: the plain's own greys, a shade lighter, since a boulder is fresher than the
            //soil it lies in and every Apollo frame shows the rocks a touch paler than the ground round them
            Vector3 rock = terrain.RegolithColor.ToVector3() * 1.15f;
            Vector3 rockDusted = terrain.RegolithColorPale.ToVector3();

            //--- Angular blocks (the volcano's broken icosahedra) and rounder weathered boulders (the lathed rock)
            var blockMeshes = new UploadedMesh[6];
            var blockInstances = new List<ModelInstance>[6];
            for (int m = 0; m < blockMeshes.Length; m++)
            {
                blockMeshes[m] = Own(VolcanoPlanting.BlockMesh(device, rng));
                blockInstances[m] = new List<ModelInstance>();
            }

            var boulderMeshes = new RockMesh[3];
            var boulderInstances = new List<ModelInstance>[3];
            for (int m = 0; m < boulderMeshes.Length; m++)
            {
                boulderMeshes[m] = Own(new RockMesh(device, 1f, 0.55f + 0.15f * m, 16, 3.1f + m * 2.3f));
                boulderInstances[m] = new List<ModelInstance>();
            }

            void Lay(List<ModelInstance>[] into, float x, float z, float size, float sink)
            {
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float tilt = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                float dust = (float)rng.NextDouble();
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.3f;
                int variant = rng.Next(into.Length);

                //Seated on the MEAN of the ground round its footprint where that is lower than under its centre, so no side
                //hangs over a crater's lip (#658). Not on the lowest of it, the volcano's rule: on the highland belt's slope
                //the lowest point under a house-sized block is several units down the hill, and the block went into the
                //slope with it - the landmark boulders were planted and not one of them showed. On a plane the mean IS the
                //centre, and the block's own underside, half its size below its origin, takes up the downhill side.
                float around = (height(x + size, z) + height(x - size, z) + height(x, z + size) + height(x, z - size)) * 0.25f;
                float ground = MathF.Min(height(x, z), around);
                into[variant].Add(new ModelInstance(
                    Matrix.CreateScale(size) * Matrix.CreateRotationX(tilt) * Matrix.CreateRotationY(yaw)
                        * Matrix.CreateTranslation(x, ground - size * sink, z),
                    new Vector4(dust, jitter, 0f, 0f)));
            }

            //--- The strewn field, in patches: a broad noise decides how thick the field lies, so the rocks gather and
            //thin the way ejecta does rather than lying at one density everywhere (a field at one density is a texture)
            void Strew(List<ModelInstance>[] into, int count, float sizeMin, float sizeMax, float sizePower, float sink)
            {
                for (int i = 0, tries = 0; i < count && tries < count * 30; tries++)
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float d = MathF.Sqrt(MathHelper.Lerp(NEAREST * NEAREST, FARTHEST * FARTHEST, (float)rng.NextDouble()));
                    float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
                    float patch = ShaderMath.Noise(new Vector2(x, z) * 0.018f + new Vector2(5.1f, 2.7f));
                    float keep = 0.15f + 0.85f * ShaderMath.SmoothStep(-0.15f, 0.35f, patch);
                    if ((float)rng.NextDouble() > keep) continue;

                    //Most small, a few big: the roll raised to a power
                    float size = MathHelper.Lerp(sizeMin, sizeMax, MathF.Pow((float)rng.NextDouble(), sizePower));
                    Lay(into, x, z, size, sink);
                    i++;
                }
            }

            Strew(blockInstances, FIELD_BLOCKS, 0.3f, 3.2f, 3f, 0.22f);
            Strew(boulderInstances, FIELD_BOULDERS, 0.2f, 1.4f, 2.5f, 0.3f);

            for (int i = 0; i < LANDMARKS; i++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float d = MathF.Sqrt(MathHelper.Lerp(LANDMARK_NEAREST * LANDMARK_NEAREST, LANDMARK_FARTHEST * LANDMARK_FARTHEST, (float)rng.NextDouble()));
                Lay(blockInstances, MathF.Cos(a) * d, MathF.Sin(a) * d, MathHelper.Lerp(3f, 8f, MathF.Pow((float)rng.NextDouble(), 2f)), 0.25f);
            }

            //--- The young craters' rim blocks, on the lip and down the apron, thinning outwards
            OffworldGround.MoonCraters(terrain, NEAREST, FARTHEST, crater =>
            {
                if (crater.Youth < RIM_MIN_YOUTH || crater.Depth < RIM_MIN_DEPTH) return;

                float fresh = (crater.Youth - RIM_MIN_YOUTH) / (1f - RIM_MIN_YOUTH);
                int count = (int)(crater.Radius * RIM_BLOCKS_PER_RADIUS * fresh * fresh);
                for (int i = 0; i < count; i++)
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    //Out from just inside the lip, most of them near it: the square leans the roll to the rim
                    float u = (float)rng.NextDouble();
                    float r = crater.Radius * (0.88f + 0.75f * u * u);
                    float x = crater.Centre.X + MathF.Cos(a) * r, z = crater.Centre.Z + MathF.Sin(a) * r;
                    if (x * x + z * z < NEAREST * NEAREST) continue;

                    //Bigger than the field's, and bigger on a bigger crater: a crater throws blocks in proportion to its size
                    float size = MathHelper.Lerp(0.5f, 1.5f + crater.Radius * 0.12f, MathF.Pow((float)rng.NextDouble(), 2.2f));
                    Lay(blockInstances, x, z, size, 0.2f);
                }
            });

            for (int m = 0; m < blockMeshes.Length; m++)
                if (blockInstances[m].Count > 0)
                    buckets.Add(Own(new ScatterBucket(device, blockMeshes[m], blockInstances[m], rock, rockDusted,
                        dapple: 0.3f, bark: 0.4f, detailOnly: false)));
            for (int m = 0; m < boulderMeshes.Length; m++)
                if (boulderInstances[m].Count > 0)
                    buckets.Add(Own(new ScatterBucket(device, boulderMeshes[m], boulderInstances[m], rock, rockDusted,
                        dapple: 0.25f, bark: 0.2f, detailOnly: false)));

            Buckets = buckets.ToArray();
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
