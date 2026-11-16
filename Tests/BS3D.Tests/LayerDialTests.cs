using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The scene layer dials (#540, #1027-#1035, #1099, #1102): one measurement dial a scene, <c>SceneRenderer.&lt;Scene&gt;Layers</c>,
    /// which the Testbed's <c>alt=&lt;scene&gt;=all/-&lt;layer&gt;</c> sets. What is pinned here, with no device: every layer a
    /// dial names is in its <c>All</c> (a layer left out of it is drawn by nobody, or is never put back by <c>all</c>), the
    /// grammar adds and takes away left to right, and every dial the renderer exposes is one the Testbed's <c>alt=</c> takes.
    /// Whether a layer's draw really leaves with its flag needs the GPU: the sweep's own figures and captures show it.
    /// </summary>
    public class LayerDialTests
    {
        /// <summary>Every layer dial the renderer exposes: its public <c>&lt;Scene&gt;Layers</c> properties of a flags enum.</summary>
        private static IEnumerable<PropertyInfo> Dials() => typeof(SceneRenderer)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.EndsWith("Layers", StringComparison.Ordinal) && p.PropertyType.IsEnum
                && p.PropertyType.IsDefined(typeof(FlagsAttribute), false));

        public static IEnumerable<object[]> DialEnums() => Dials().Select(p => new object[] { p.PropertyType.Name });

        private static Type EnumNamed(string name) => typeof(SceneRenderer).Assembly.GetType("Prazsky.Core.Render." + name, throwOnError: true);

        //The named layers of a dial: every flag but None and All
        private static (string Name, long Bits)[] Layers(Type type) => Enum.GetNames(type)
            .Where(n => n is not ("None" or "All"))
            .Select(n => (n, Convert.ToInt64(Enum.Parse(type, n))))
            .ToArray();

        private static long Parse(Type type, string list) =>
            Convert.ToInt64(typeof(LayerDial).GetMethod(nameof(LayerDial.Parse))!.MakeGenericMethod(type).Invoke(null, new object[] { list }));

        [Fact]
        public void TheRendererHasTheDialsTheIssuesBuilt()
        {
            string[] names = Dials().Select(p => p.Name).ToArray();
            foreach (string dial in new[] { "VolcanoLayers", "AuroraLayers", "CavernLayers", "MeadowLayers", "MountainLayers",
                         "ForestLayers", "MarsLayers", "OutbackLayers" })
                Assert.Contains(dial, names);
        }

        [Theory]
        [MemberData(nameof(DialEnums))]
        public void AllIsEveryNamedLayerAndEachIsOneBitOfItsOwn(string enumName)
        {
            Type type = EnumNamed(enumName);
            (string Name, long Bits)[] layers = Layers(type);

            Assert.Equal(0L, Convert.ToInt64(Enum.Parse(type, "None")));
            long all = Convert.ToInt64(Enum.Parse(type, "All"));
            long union = 0;
            foreach ((string name, long bits) in layers)
            {
                Assert.True(bits != 0 && (bits & (bits - 1)) == 0, $"{enumName}.{name} is {bits}, not one bit");
                Assert.True((union & bits) == 0, $"{enumName}.{name} shares its bit with another layer");
                union |= bits;
            }

            Assert.Equal(union, all);
        }

        [Theory]
        [MemberData(nameof(DialEnums))]
        public void AllLessOneLayerTakesThatLayerAndNothingElse(string enumName)
        {
            Type type = EnumNamed(enumName);
            long all = Convert.ToInt64(Enum.Parse(type, "All"));

            Assert.Equal(all, Parse(type, "all"));
            Assert.Equal(0L, Parse(type, "all/-all"));
            foreach ((string name, long bits) in Layers(type))
            {
                Assert.Equal(all & ~bits, Parse(type, "all/-" + name));
                Assert.Equal(all & ~bits, Parse(type, "ALL,-" + name.ToLowerInvariant()));
                Assert.Equal(bits, Parse(type, name));
            }
        }

        [Fact]
        public void TheGrammarReadsLeftToRightAndSkipsWhatItCannotRead()
        {
            Assert.Equal(MarsLayer.All & ~MarsLayer.Moons & ~MarsLayer.Bake, LayerDial.Parse<MarsLayer>("all/-moons/-bake"));
            Assert.Equal(MarsLayer.Ground | MarsLayer.Bank, LayerDial.Parse<MarsLayer>(" ground / bank "));
            Assert.Equal(MarsLayer.All, LayerDial.Parse<MarsLayer>("all/-moons/moons"));
            Assert.Equal(MarsLayer.Moons, LayerDial.Parse<MarsLayer>("-moons/moons"));
            Assert.Equal(OutbackLayer.All & ~OutbackLayer.Spinifex, LayerDial.Parse<OutbackLayer>("all/-spinifex/-nosuchlayer//"));
            Assert.Equal(MarsLayer.None, LayerDial.Parse<MarsLayer>(""));

            //The forest's floor is the camera grid alone since #1102; its far ring and the bank are layers of their own
            Assert.Equal(ForestLayer.All & ~ForestLayer.Floor, LayerDial.Parse<ForestLayer>("all/-floor"));
            Assert.NotEqual(ForestLayer.None, ForestLayer.All & ForestLayer.FarRing);
            Assert.NotEqual(ForestLayer.None, ForestLayer.All & ForestLayer.Bank);
        }

        [Fact]
        public void EveryDialIsOneTheTestbedAlternatesAndApplies()
        {
            string source = File.ReadAllText(Path.Combine(RepoRoot(), "Testbed", "Testbed.cs"));
            Match list = Regex.Match(source, @"ALTERNATION_DIALS\s*=\s*\{([^}]*)\}");
            Assert.True(list.Success, "ALTERNATION_DIALS not found in Testbed.cs");
            string[] alternated = Regex.Matches(list.Groups[1].Value, "\"([a-z]+)\"").Select(m => m.Groups[1].Value).ToArray();

            foreach (PropertyInfo dial in Dials())
            {
                //VolcanoLayers is volcano=, MarsLayers is mars=
                string spelling = dial.Name[..^"Layers".Length].ToLowerInvariant();
                Assert.Contains(spelling, alternated);
                Assert.Contains($"case \"{spelling}\":", source);
                Assert.Contains($"_sceneRenderer.{dial.Name} = ", source);
            }
        }

        private static string RepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Testbed.sln"))) dir = Path.GetDirectoryName(dir);
            return dir ?? throw new DirectoryNotFoundException("no Testbed.sln above " + AppContext.BaseDirectory);
        }
    }
}
