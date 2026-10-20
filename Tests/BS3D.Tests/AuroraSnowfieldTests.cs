using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The aurora's snow and the wood standing in it (#963), without a device.
    /// <list type="bullet">
    /// <item>Every trunk stands in a collar of snow that rises against the bark itself (measured at the bark's edge, where
    /// the eye sees the join, not at the trunk's centre) and melts into the ground within its reach. The first cut drew the
    /// drift into the ground's mound map at a unit a texel under a grid of 0.75: it came out at four fifths of its height
    /// smeared over two units, and every foot read as a cylinder stuck into a plane. Seen to fail with the collars at no
    /// height (<see cref="AuroraSnowfieldConfig.TrunkDrift"/> 0).</item>
    /// <item>No log is buried whole (a fifth of its thickness at least shows over the snow) or lies on top of it. Seen to fail with
    /// <see cref="AuroraSnowfieldConfig.LogDrift"/> at 1.2 (buried).</item>
    /// <item>The CPU mirror reads the mound map with the shader's bilinear arithmetic: at its texels to the bit, and
    /// anywhere against an independent bilinear in double precision.</item>
    /// </list>
    /// </summary>
    public class AuroraSnowfieldTests
    {
        private static readonly AuroraSceneConfig Config = new();
        private static readonly ForestScatter Planting = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED, Config.Terrain,
            (x, z) => TerrainMirror.AuroraBase(x, z, Config));
        private static readonly AuroraMoundMap Mounds = AuroraMoundMap.Bake(Planting, Config);
        private static float Ground(float x, float z) => TerrainMirror.Aurora(x, z, Config, Mounds);
        private static readonly AuroraCollars.Collar[] Collars = AuroraCollars.Build(Planting, Config, Ground);

        private static float Scale(Matrix world) => new Vector3(world.M11, world.M12, world.M13).Length();

        [Fact]
        public void EveryTrunkStandsInADriftAgainstItsBark()
        {
            Vector2 wind = TerrainMirror.SnowWind(Config.Snowfield);
            int index = 0, trees = 0;

            foreach (ModelInstance[] bucket in Planting.Conifers)
            {
                foreach (ModelInstance tree in bucket)
                {
                    AuroraCollars.Collar collar = Collars[index++];
                    float scale = Scale(tree.World);
                    Vector2 at = new(tree.World.M41, tree.World.M43);
                    Assert.Equal(at, new Vector2(collar.Place.X, collar.Place.Y));

                    for (int k = 0; k < 8; k++)
                    {
                        float angle = k * MathHelper.TwoPi / 8f;
                        Vector2 outward = new(MathF.Cos(angle), MathF.Sin(angle));
                        float downwind = Vector2.Dot(outward, wind);

                        //Up the bark: the snow stands well over the ground the grid draws, right where it meets the wood
                        float atBark = AuroraCollars.HeightAt(collar, collar.Place.W, downwind, Config.Snowfield.CollarLee);
                        Assert.True(atBark > 0.6f * Config.Snowfield.TrunkDrift * scale,
                            $"The snow against the trunk at ({at.X:F1}, {at.Y:F1}) stands {atBark:F3} up the bark, scale {scale:F2}");

                        //And it is over the foot: the foot is buried in the collar
                        Vector2 bark = at + outward * collar.Place.W;
                        Assert.True(Ground(bark.X, bark.Y) + atBark > tree.World.M42,
                            $"The trunk's foot at ({at.X:F1}, {at.Y:F1}) stands proud of its drift");

                        //And it melts into the ground by the end of its reach
                        float reach = collar.Place.W * AuroraCollars.INNER + collar.Shape.Y * AuroraCollars.LeeStretch(downwind, Config.Snowfield.CollarLee);
                        Assert.True(AuroraCollars.HeightAt(collar, reach, downwind, Config.Snowfield.CollarLee) <= 0f,
                            $"The drift round ({at.X:F1}, {at.Y:F1}) ends above the snow");
                    }

                    trees++;
                }
            }

            Assert.True(trees > 800, $"Only {trees} spruces were checked");
        }

        [Fact]
        public void NoLogIsBuriedWholeOrLiesOnTheSnow()
        {
            int first = Collars.Length - Planting.Logs[0].Length - Planting.Logs[1].Length, logs = 0;
            for (int v = 0; v < Planting.Logs.Length; v++)
            {
                float radius = Config.Terrain.Logs.Radius * ForestScatterRenderer.LOG_SHAPES[v].Y;
                float length = Config.Terrain.Logs.Length * ForestScatterRenderer.LOG_SHAPES[v].X;
                float bend = DeadwoodMesh.Bend(length, ForestScatterRenderer.LOG_SEEDS[v]);
                foreach (ModelInstance log in Planting.Logs[v])
                {
                    AuroraCollars.Collar collar = Collars[first + logs];
                    float r = radius * Scale(log.World);

                    //The middle of the log as DeadwoodMesh lays it, its axis 0.7 of the root's radius over the planted foot
                    Vector3 middle = Vector3.Transform(new Vector3(0f, radius * 0.7f, bend), log.World);
                    Vector2 core = new(collar.Place.X, collar.Place.Y);
                    float fromCore = Vector2.Distance(new Vector2(middle.X, middle.Z), core);
                    float snow = Ground(middle.X, middle.Z) + AuroraCollars.HeightAt(collar, fromCore, 0f, Config.Snowfield.CollarLee);

                    Assert.True(snow < middle.Y + 0.8f * r, $"The log at ({middle.X:F1}, {middle.Z:F1}) is buried: snow {snow:F2}, axis {middle.Y:F2}, radius {r:F2}");
                    Assert.True(snow > middle.Y - 0.6f * r, $"The log at ({middle.X:F1}, {middle.Z:F1}) lies on the snow: snow {snow:F2}, axis {middle.Y:F2}, radius {r:F2}");
                    logs++;
                }
            }

            Assert.Equal(Config.Terrain.Logs.Count, logs);
        }

        [Fact]
        public void TheMirrorReadsTheMapAsTheShaderDoes()
        {
            var random = new Random(963);

            //At a texel's centre the bilinear weights are 0 and the height is that texel's own, to the bit
            for (int i = 0; i < 2000; i++)
            {
                int tx = random.Next(AuroraMoundMap.SIZE - 1), tz = random.Next(AuroraMoundMap.SIZE - 1);
                float x = AuroraMoundMap.ORIGIN.X + (tx + 0.5f) * AuroraMoundMap.TEXEL;
                float z = AuroraMoundMap.ORIGIN.Y + (tz + 0.5f) * AuroraMoundMap.TEXEL;
                Assert.Equal(Mounds.Texel(tx, tz).X, Mounds.Height(x, z));
            }

            //Anywhere on the map, against an independent bilinear over the four texels round the point, in double precision;
            //half of the points beside trees, where the map is not flat
            int besideTrees = 0;
            for (int i = 0; i < 4000; i++)
            {
                double x, z;
                if (i % 2 == 0)
                {
                    ModelInstance[] bucket = Planting.Conifers[random.Next(Planting.Conifers.Length)];
                    ModelInstance tree = bucket[random.Next(bucket.Length)];
                    x = tree.World.M41 + (random.NextDouble() - 0.5) * 8.0;
                    z = tree.World.M43 + (random.NextDouble() - 0.5) * 8.0;
                    besideTrees++;
                }
                else
                {
                    x = (random.NextDouble() - 0.5) * (AuroraMoundMap.SIZE - 4) * AuroraMoundMap.TEXEL;
                    z = (random.NextDouble() - 0.5) * (AuroraMoundMap.SIZE - 4) * AuroraMoundMap.TEXEL;
                }

                double tx = (x - AuroraMoundMap.ORIGIN.X) / AuroraMoundMap.TEXEL - 0.5, tz = (z - AuroraMoundMap.ORIGIN.Y) / AuroraMoundMap.TEXEL - 0.5;
                int cx = (int)Math.Floor(tx), cz = (int)Math.Floor(tz);
                double fx = tx - cx, fz = tz - cz;
                double expected = (1 - fz) * ((1 - fx) * Mounds.Texel(cx, cz).X + fx * Mounds.Texel(cx + 1, cz).X)
                    + fz * ((1 - fx) * Mounds.Texel(cx, cz + 1).X + fx * Mounds.Texel(cx + 1, cz + 1).X);

                Assert.InRange(Mounds.Height((float)x, (float)z), expected - 1e-4, expected + 1e-4);
            }

            Assert.Equal(2000, besideTrees);

            //And off the map there is none
            Assert.Equal(0f, Mounds.Height(-AuroraMoundMap.ORIGIN.X + 5f, 0f));
        }
    }
}
