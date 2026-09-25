using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The map and level formats: every shipped level survives a save and a reload unchanged, a ball of a
    /// colour or kind this build does not have is refused (#571), and a legacy map with no field size keeps its
    /// level parity when it is padded (CLAUDE.md, "The ball grid").
    /// </summary>
    public class LevelFormatTests
    {
        public static IEnumerable<object[]> Levels() => Shipped.LevelFileNames();

        [Theory]
        [MemberData(nameof(Levels))]
        public void ShippedLevelSurvivesSaveAndLoad(string file)
        {
            using TempDirectory temp = new();

            Level original = Level.Load(Shipped.Level(file));
            string copy = temp.File(file);
            original.Save(copy);
            Level reloaded = Level.Load(copy);

            Assert.Equal(original.Format, reloaded.Format);
            Assert.Equal(original.Version, reloaded.Version);
            Assert.Equal(original.Name, reloaded.Name);
            Assert.Equal(original.Author, reloaded.Author);
            Assert.Equal(original.SkyDome, reloaded.SkyDome);
            Assert.Equal(original.Scene, reloaded.Scene);
            Assert.Equal(original.Music, reloaded.Music);
            Assert.Equal(original.Balls, reloaded.Balls);
            Assert.Equal(original.Weather, reloaded.Weather);

            AssertSameMap(original.Map, reloaded.Map);

            //And the reloaded map is one the game can build: the same field, the same balls in the same cells
            BallsMap a = new(original.Map), b = new(reloaded.Map);
            Assert.Equal((a.StageSizeX, a.StageSizeZ, a.Levels), (b.StageSizeX, b.StageSizeZ, b.Levels));
            Assert.Equal(a.GetBallsCount(), b.GetBallsCount());
            Assert.True(a.GetBallsCount() > 0, $"{file} carries no balls");
        }

        private static void AssertSameMap(BallPositionTypes expected, BallPositionTypes actual)
        {
            Assert.Equal(expected.StageSizeX, actual.StageSizeX);
            Assert.Equal(expected.StageSizeZ, actual.StageSizeZ);
            Assert.Equal(expected.Levels, actual.Levels);

            XZLevel size = XZLevel.FromArray(expected.Balls);
            Assert.Equal(size, XZLevel.FromArray(actual.Balls));

            for (int x = 0; x < size.X; x++)
                for (int z = 0; z < size.Z; z++)
                    for (int l = 0; l < size.Level; l++)
                    {
                        BallPositionType e = expected.Balls[x, z, l], r = actual.Balls[x, z, l];

                        Assert.Equal(e == null, r == null);
                        if (e == null) continue;

                        Assert.Equal((e.Type, e.Kind, e.PositionX, e.PositionY, e.PositionZ),
                            (r.Type, r.Kind, r.PositionX, r.PositionY, r.PositionZ));
                    }
        }

        /// <summary>
        /// A 2×2×3 layout with one ball per cell, as a level file. <paramref name="type"/> and
        /// <paramref name="kind"/> go on the first ball; <paramref name="kind"/> is left out when null, as a
        /// normal ball is written.
        /// </summary>
        private static string LevelJson(int type, int? kind = null)
        {
            string first = kind == null
                ? $"{{\"x\":0,\"y\":0,\"z\":0,\"t\":{type}}}"
                : $"{{\"x\":0,\"y\":0,\"z\":0,\"t\":{type},\"k\":{kind}}}";
            const string ball = "{\"x\":0,\"y\":0,\"z\":0,\"t\":1}";

            return "{\"format\":\"bs3d-level\",\"version\":2,\"map\":{\"sx\":2,\"sz\":2,\"l\":9,\"b\":["
                + $"[[{first},{ball},{ball}],[{ball},{ball},{ball}]],"
                + $"[[{ball},{ball},{ball}],[{ball},{ball},{ball}]]"
                + "]}}";
        }

        private static BallsMap BuildFromLevelText(string json)
        {
            using TempDirectory temp = new();
            string path = temp.File("level.json");
            File.WriteAllText(path, json);

            return new BallsMap(Level.Load(path).Map);
        }

        [Fact]
        public void LevelWithKnownColoursBuilds() =>
            Assert.Equal(12, BuildFromLevelText(LevelJson(type: 13)).GetBallsCount());

        /// <summary>Colour 14 does not exist: there are thirteen (<see cref="BallType"/>), 1 to 13.</summary>
        [Theory]
        [InlineData(14)]
        [InlineData(0)]
        public void BallOfAnUndefinedColourIsRefused(int type) =>
            Assert.Throws<InvalidDataException>(() => BuildFromLevelText(LevelJson(type)));

        [Fact]
        public void BallOfAnUndefinedKindIsRefused() =>
            Assert.Throws<InvalidDataException>(() => BuildFromLevelText(LevelJson(type: 1, kind: 250)));

        /// <summary>
        /// A legacy map carries no <c>sx</c>/<c>sz</c>/<c>l</c>: it gets extra empty levels below, an even
        /// number of them, so every layout level keeps its parity (odd levels are the shifted ones). Layouts of
        /// both an odd and an even number of levels.
        /// </summary>
        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public void LegacyMapPaddingKeepsLevelParity(int layoutLevels)
        {
            using TempDirectory temp = new();
            string path = temp.File("legacy.json");
            File.WriteAllText(path, LegacyMapJson(layoutLevels));

            BallsMap map = new(path);

            int added = map.Levels - layoutLevels;
            Assert.True(added > 0, "a legacy map gets room to grow below its layout");
            Assert.Equal(0, added % 2);

            //The layout's own level k is at field level k + added, with k's parity: a ball on every layout cell
            StaticBall[,,] balls = map.GetStaticBallsArray();
            for (int k = 0; k < layoutLevels; k++)
            {
                Assert.NotNull(balls[0, 0, k + added]);
                Assert.Equal(k % 2, (k + added) % 2);
            }

            for (int l = 0; l < added; l++) Assert.Null(balls[0, 0, l]);
        }

        /// <summary>
        /// The same guarantee on the sized path: a field whose empty levels below the layout are odd in number
        /// is grown by one, since the layout is hung at the top.
        /// </summary>
        [Fact]
        public void SizedMapWithAnOddGapIsExtendedByOne()
        {
            BallPositionTypes data = new()
            {
                StageSizeX = 2,
                StageSizeZ = 2,
                Levels = 6,
                Balls = new BallPositionType[2, 2, 3],
            };
            for (int x = 0; x < 2; x++)
                for (int z = 0; z < 2; z++)
                    for (int l = 0; l < 3; l++)
                        data.Balls[x, z, l] = new BallPositionType { Type = BallType.Type1 };

            BallsMap map = new(data);

            Assert.Equal(7, map.Levels);
            Assert.Equal(0, (map.Levels - 3) % 2);
        }

        private static string LegacyMapJson(int levels)
        {
            System.Text.StringBuilder column = new("[");
            for (int l = 0; l < levels; l++) column.Append(l == 0 ? "" : ",").Append("{\"x\":0,\"y\":0,\"z\":0,\"t\":2}");
            column.Append(']');

            //2×2 columns; no "sx"/"sz"/"l" - that is what makes it legacy
            return $"{{\"b\":[[{column},{column}],[{column},{column}]]}}";
        }
    }
}
