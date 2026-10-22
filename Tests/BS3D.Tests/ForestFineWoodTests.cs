using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// High and Ultra's wood in the daytime forest (#962): more trees, saplings and dead wood planted after everything else, and
    /// the broadleaves grown in leaf. What Low and Medium draw must not move, and the aurora, which plants no more, must get its
    /// own arrays back. Device-free.
    /// </summary>
    public class ForestFineWoodTests
    {
        private readonly ITestOutputHelper _output;

        public ForestFineWoodTests(ITestOutputHelper output) => _output = output;

        private static ForestScatter Plant(ForestSceneConfig config) =>
            ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED, config, (x, z) => TerrainMirror.Forest(x, z, config));

        [Fact]
        public void EveryTierBelowHighDrawsTheWoodItDrew()
        {
            ForestSceneConfig fine = new();
            ForestSceneConfig plain = new();
            plain.Trees.FineCount = 0;
            plain.Trees.SaplingCount = 0;
            plain.Rocks.FineCount = plain.Stumps.FineCount = plain.Snags.FineCount = plain.Logs.FineCount = 0;

            ForestScatter withFine = Plant(fine), without = Plant(plain);
            AssertSame(without.Conifers, withFine.Conifers, "conifers");
            AssertSame(without.Broadleaves, withFine.Broadleaves, "broadleaves");
            AssertSame(without.Rocks, withFine.Rocks, "rocks");
            AssertSame(without.Stumps, withFine.Stumps, "stumps");

            //And High and Ultra's arrays begin with those, variant by variant, so a tier change moves no tree
            int added = 0;
            for (int v = 0; v < withFine.Conifers.Length; v++)
            {
                for (int i = 0; i < withFine.Conifers[v].Length; i++)
                    Assert.Equal(withFine.Conifers[v][i].World, withFine.FineConifers[v][i].World);
                added += withFine.FineConifers[v].Length - withFine.Conifers[v].Length;
            }
            for (int v = 0; v < withFine.Broadleaves.Length; v++)
            {
                for (int i = 0; i < withFine.Broadleaves[v].Length; i++)
                    Assert.Equal(withFine.Broadleaves[v][i].World, withFine.FineBroadleaves[v][i].World);
                added += withFine.FineBroadleaves[v].Length - withFine.Broadleaves[v].Length;
            }
            _output.WriteLine($"High and Ultra add {added} trees");
            Assert.Equal(fine.Trees.FineCount + fine.Trees.SaplingCount, added);
        }

        [Fact]
        public void TheAuroraPlantsNoMore()
        {
            ForestScatter aurora = Plant(new AuroraSceneConfig().Terrain);
            Assert.Same(aurora.Conifers, aurora.FineConifers);
            Assert.Same(aurora.Broadleaves, aurora.FineBroadleaves);
            Assert.Same(aurora.Snags, aurora.FineSnags);
            Assert.Same(aurora.Logs, aurora.FineLogs);
        }

        [Fact]
        public void TheBroadleafInLeafIsTheSameTree()
        {
            ForestTreeConfig trees = new ForestSceneConfig().Trees;
            foreach (ForestScatterRenderer.BroadleafVariant variant in ForestScatterRenderer.BROADLEAF_SHAPES)
            {
                (float trunkRadius, float trunkHeight, float crownRadius, float crownHeight) = ForestScatterRenderer.BroadleafFigures(trees, variant);
                MeadowTreeGeometry plain = MeadowTreeMesh.ForestGeometry(trunkRadius, trunkHeight, crownRadius, crownHeight, variant.Seed, inLeaf: false);
                MeadowTreeGeometry inLeaf = MeadowTreeMesh.ForestGeometry(trunkRadius, trunkHeight, crownRadius, crownHeight, variant.Seed, inLeaf: true);

                Assert.Equal(plain.WoodVertices, inLeaf.WoodVertices);
                Assert.Equal(plain.WoodIndices, inLeaf.WoodIndices);
                Assert.Equal(plain.CrownVertices, inLeaf.CrownVertices);
                Assert.True(inLeaf.LeafIndices.Count > 0 && inLeaf.FineWoodIndices.Count > 0);
                _output.WriteLine($"seed {variant.Seed}: {inLeaf.LeafIndices.Count / 6} leaf cards, " +
                    $"{inLeaf.FineWoodIndices.Count / 3} fine-wood and {inLeaf.TwigIndices.Count / 3} twig triangles");
            }
        }

        private static void AssertSame(ModelInstance[][] expected, ModelInstance[][] actual, string kind)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int v = 0; v < expected.Length; v++)
            {
                Assert.True(expected[v].Length == actual[v].Length, $"{kind} variant {v}: {expected[v].Length} against {actual[v].Length}");
                for (int i = 0; i < expected[v].Length; i++)
                    Assert.Equal(expected[v][i].World, actual[v][i].World);
            }
        }
    }
}
