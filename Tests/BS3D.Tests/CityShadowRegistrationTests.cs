using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The two cities' sun shadows exist only through a host's registration (#471): <see cref="SceneRenderer"/> does not own
    /// the city, so <c>GetSceneConfig</c> answers null there and the map is fitted only for a scene a host handed over with
    /// <see cref="SceneRenderer.SetHostCityShadows"/>. A host that builds a renderer and forgets it draws the editor's default
    /// scene, and ten shipped levels' scene, with no shadow from the towers, the island or the cluster, and nothing in the
    /// frame says why: that is what the map editor did until #1210's review. These hold the figures to a map that is asked
    /// for and every executable that builds a renderer to the one registration.
    /// </summary>
    public class CityShadowRegistrationTests
    {
        //The executables that build a SceneRenderer. GamePi and GameAndroid compile the Game's own sources.
        private static readonly string[] Hosts = { "Game", "Testbed", "MapEditor" };

        [Fact]
        public void The_city_config_asks_for_a_map_with_the_citys_own_fit()
        {
            CitySceneConfig city = new();

            Assert.True(city.Shadows.Enabled, "The city's config asks for no shadow map, so no host gets one.");
            Assert.Equal(CitySceneConfig.SHADOW_STRENGTH, city.Shadows.Strength);
            Assert.Equal(CitySceneConfig.SHADOW_EXTENT, city.Shadows.Extent);

            //The box has to clear the tallest tower over the street level, and reach under it
            Assert.True(CitySceneConfig.SHADOW_ABOVE > 156f);
            Assert.True(CitySceneConfig.SHADOW_BELOW > 0f);
        }

        [Fact]
        public void Every_host_that_builds_a_scene_renderer_registers_the_citys_shadows()
        {
            string root = RepositoryRoot();
            List<string> missing = new();
            int builders = 0;

            foreach (string host in Hosts)
            {
                string folder = Path.Combine(root, host);
                bool builds = false, registers = false;

                foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                        file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;

                    string code = Regex.Replace(File.ReadAllText(file), @"//[^\r\n]*", string.Empty);
                    builds |= code.Contains("new SceneRenderer(", StringComparison.Ordinal);
                    registers |= code.Contains("SetHostCityShadows(", StringComparison.Ordinal);
                }

                if (!builds) continue;
                builders++;
                if (!registers) missing.Add(host);
            }

            //The scan found the renderers, or an empty list would pass by finding nothing
            Assert.Equal(Hosts.Length, builders);
            Assert.True(missing.Count == 0,
                "These executables build a SceneRenderer but never register the city's shadows: " + string.Join(", ", missing));
        }

        private static string RepositoryRoot()
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BS3DLibs", "Prazsky.Core", "Render", "SceneRenderer.cs")))
                    return directory.FullName;
            }

            throw new DirectoryNotFoundException("No repository root (BS3DLibs/Prazsky.Core/Render/SceneRenderer.cs) was found above the test assembly.");
        }
    }
}
