using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The aurora's snow and the wood standing in it (#963), without a device: every trunk stands in a drift of its own
    /// (the snow rises against the bark, so no foot reads as a cylinder stuck into a plane), no log is buried whole, and the
    /// CPU mirror reads the mound map with the shader's own arithmetic. Each check was seen to fail: the drift check with
    /// <see cref="AuroraSnowfieldConfig.TrunkDrift"/> at 0, the log check with <see cref="AuroraSnowfieldConfig.LogDrift"/>
    /// at the first cut's 0.95 (which buried every log in the first captures).
    /// </summary>
    public class AuroraSnowfieldTests
    {
        private static readonly AuroraSceneConfig Config = new();
        private static readonly ForestScatter Planting = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED, Config.Terrain,
            (x, z) => TerrainMirror.AuroraBase(x, z, Config));
        private static readonly AuroraMoundMap Mounds = AuroraMoundMap.Bake(Planting, Config);

        private static float Scale(Matrix world) => new Vector3(world.M11, world.M12, world.M13).Length();

        [Fact]
        public void EveryTrunkStandsInADrift()
        {
            int trees = 0;
            foreach (ModelInstance[] bucket in Planting.Conifers)
            {
                foreach (ModelInstance tree in bucket)
                {
                    float x = tree.World.M41, z = tree.World.M43;
                    if (MathF.Max(MathF.Abs(x), MathF.Abs(z)) > -AuroraMoundMap.ORIGIN.X - 16f) continue;

                    float drawn = TerrainMirror.Aurora(x, z, Config, Mounds);
                    float bare = TerrainMirror.AuroraBase(x, z, Config);

                    //The foot is under the snow, and the snow at the bark stands proud of the field round it
                    Assert.True(drawn > tree.World.M42, $"A trunk at ({x:F1}, {z:F1}) stands on the snow at {drawn:F2}, its foot at {tree.World.M42:F2}");
                    Assert.True(drawn - bare > 0.3f * Config.Snowfield.TrunkDrift * Scale(tree.World),
                        $"No drift against the trunk at ({x:F1}, {z:F1}): {drawn - bare:F3} over the bare snow");
                    trees++;
                }
            }

            Assert.True(trees > 700, $"Only {trees} spruces were checked");
        }

        [Fact]
        public void NoLogIsBuriedWhole()
        {
            int logs = 0;
            for (int v = 0; v < Planting.Logs.Length; v++)
            {
                float radius = Config.Terrain.Logs.Radius * ForestScatterRenderer.LOG_SHAPES[v].Y;
                foreach (ModelInstance log in Planting.Logs[v])
                {
                    //The log's middle, where DeadwoodMesh lays its axis 0.7 of the root's radius over the planted foot
                    float r = radius * Scale(log.World);
                    Vector3 middle = Vector3.Transform(new Vector3(0f, radius * 0.7f, 0f), log.World);
                    float snow = TerrainMirror.Aurora(middle.X, middle.Z, Config, Mounds);

                    Assert.True(snow < middle.Y + 0.6f * r, $"The log at ({middle.X:F1}, {middle.Z:F1}) is buried: snow {snow:F2}, axis {middle.Y:F2}, radius {r:F2}");
                    Assert.True(snow > middle.Y - r, $"The log at ({middle.X:F1}, {middle.Z:F1}) lies on the snow: snow {snow:F2}, axis {middle.Y:F2}, radius {r:F2}");
                    logs++;
                }
            }

            Assert.Equal(Config.Terrain.Logs.Count, logs);
        }

        [Fact]
        public void TheMirrorReadsTheMapAtItsTexels()
        {
            //At a texel's centre the bilinear weights are 0 and the height is that texel's own, to the bit
            var random = new Random(963);
            for (int i = 0; i < 2000; i++)
            {
                int tx = random.Next(AuroraMoundMap.SIZE - 1), tz = random.Next(AuroraMoundMap.SIZE - 1);
                float x = AuroraMoundMap.ORIGIN.X + (tx + 0.5f) * AuroraMoundMap.TEXEL;
                float z = AuroraMoundMap.ORIGIN.Y + (tz + 0.5f) * AuroraMoundMap.TEXEL;
                Assert.Equal(Mounds.Texel(tx, tz).X, Mounds.Height(x, z));
            }

            //And off the map there is none
            Assert.Equal(0f, Mounds.Height(-AuroraMoundMap.ORIGIN.X + 5f, 0f));
        }
    }
}
