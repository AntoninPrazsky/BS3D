using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The scene registry (#580): one row per <see cref="SceneKind"/>, the spellings a level file and every
    /// <c>scene=</c> switch take, and the three classifications — pinned to what the five tables it replaced
    /// answered, and its load-time check seen to refuse a broken table (BestPractices.md §10).
    /// </summary>
    public class SceneCatalogTests
    {
        //What the five pre-#580 tables answered, written out by hand: SCENE_NAMES, the TryParseScene switch
        //(and the converter's "neon"), ReplacesSky, IsSolidTerrainScene and AnimatesLightRig. A level file
        //stores the parse key, so a spelling that drifted would be a level that quietly loads in the city.
        private static readonly (SceneKind Kind, string Name, string Key, string[] Aliases, bool Sky, bool Solid, bool Rig)[] BEFORE =
        {
            (SceneKind.City, "City", "city", new string[0], false, false, false),
            (SceneKind.Sea, "Sea", "sea", new string[0], false, false, false),
            (SceneKind.Savanna, "Savanna", "savanna", new string[0], false, true, false),
            (SceneKind.Desert, "Desert", "desert", new string[0], false, true, false),
            (SceneKind.Mountain, "Mountains", "mountain", new string[0], false, true, false),
            (SceneKind.Meadow, "Meadow", "meadow", new string[0], false, true, false),
            (SceneKind.NeonCity, "Neon City", "neon", new string[0], false, false, false),
            (SceneKind.Forest, "Forest", "forest", new string[0], false, true, false),
            (SceneKind.Space, "Space", "space", new string[0], true, false, false),
            (SceneKind.Dream, "Dream", "dream", new string[0], true, false, false),
            (SceneKind.Cavern, "Cavern", "cavern", new string[0], true, false, false),
            (SceneKind.Moon, "Moon", "moon", new string[0], true, true, false),
            (SceneKind.Outback, "Outback", "outback", new string[0], false, true, false),
            (SceneKind.Tropical, "Tropical", "tropical", new string[0], false, true, false),
            (SceneKind.Volcano, "Volcano", "volcano", new string[0], false, true, false),
            (SceneKind.Mars, "Mars", "mars", new string[0], false, true, false),
            (SceneKind.Storm, "Storm", "storm", new string[0], false, false, false),
            (SceneKind.Polar, "Polar", "polar", new[] { "ice" }, false, true, false),
            (SceneKind.Aurora, "Aurora", "aurora", new string[0], true, true, true),
            (SceneKind.Grid, "Grid", "grid", new[] { "tron" }, true, true, false),
        };

        [Fact]
        public void EveryKindHasItsRowAndAnswersAsBefore()
        {
            Assert.Equal(Enum.GetValues<SceneKind>().Length, SceneCatalog.Count);
            Assert.Equal(BEFORE.Length, SceneCatalog.Count);

            foreach (var row in BEFORE)
            {
                Assert.Equal(row.Kind, SceneCatalog.Get(row.Kind).Kind);
                Assert.Equal(row.Name, SceneCatalog.DisplayName(row.Kind));
                Assert.Equal(row.Name, SceneRenderer.SceneName(row.Kind));
                Assert.Equal(row.Key, SceneCatalog.ParseKey(row.Kind));
                Assert.Equal(row.Aliases, SceneCatalog.Get(row.Kind).Aliases);
                Assert.Equal(row.Sky, SceneRenderer.ReplacesSky(row.Kind));
                Assert.Equal(row.Solid, SceneRenderer.IsSolidTerrainScene(row.Kind));
                Assert.Equal(!row.Solid, SceneRenderer.OpenBelow(row.Kind));
                Assert.Equal(row.Rig, SceneRenderer.AnimatesLightRig(row.Kind));
            }
        }

        [Fact]
        public void EverySpellingParsesBackToItsKind()
        {
            foreach (SceneInfo row in SceneCatalog.All)
            {
                foreach (string spelling in row.Aliases.Prepend(row.ParseKey))
                {
                    Assert.True(SceneRenderer.TryParseScene(spelling, out SceneKind kind));
                    Assert.Equal(row.Kind, kind);

                    //The switch it replaced lowercased its input
                    Assert.True(SceneCatalog.TryParse(spelling.ToUpperInvariant(), out kind));
                    Assert.Equal(row.Kind, kind);
                }
            }

            Assert.False(SceneCatalog.TryParse(null, out _));
            Assert.False(SceneCatalog.TryParse("", out _));
            Assert.False(SceneCatalog.TryParse("mountains", out _));
            Assert.False(SceneCatalog.TryParse("neoncity", out _));
        }

        [Fact]
        public void NextSceneWalksTheWholeEnum()
        {
            HashSet<SceneKind> seen = new();
            SceneKind kind = SceneKind.City;

            for (int i = 0; i < SceneCatalog.Count; i++)
            {
                Assert.True(seen.Add(kind));
                kind = SceneCatalog.NextScene(kind);
            }

            Assert.Equal(SceneKind.City, kind);
        }

        //The failing branches, each seen to fire on a table broken the way a careless edit would break it
        [Fact]
        public void ARowMissingIsRefused() =>
            Assert.Throws<InvalidOperationException>(() => SceneCatalog.Check(SceneCatalog.All.Take(SceneCatalog.Count - 1).ToArray()));

        [Fact]
        public void RowsOutOfOrderAreRefused()
        {
            SceneInfo[] rows = SceneCatalog.All.ToArray();
            (rows[1], rows[2]) = (rows[2], rows[1]);

            Assert.Throws<InvalidOperationException>(() => SceneCatalog.Check(rows));
        }

        [Fact]
        public void ASpellingNamingTwoKindsIsRefused()
        {
            SceneInfo[] rows = SceneCatalog.All.ToArray();
            rows[(int)SceneKind.Storm] = new SceneInfo(SceneKind.Storm, "Storm", "storm", aliases: new[] { "ice" });

            Assert.Throws<InvalidOperationException>(() => SceneCatalog.Check(rows));
        }

        [Fact]
        public void AnUppercaseSpellingIsRefused()
        {
            SceneInfo[] rows = SceneCatalog.All.ToArray();
            rows[(int)SceneKind.Sea] = new SceneInfo(SceneKind.Sea, "Sea", "Sea");

            Assert.Throws<InvalidOperationException>(() => SceneCatalog.Check(rows));
        }
    }
}
