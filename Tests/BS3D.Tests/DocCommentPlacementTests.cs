using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1141: a member's doc comment that slid onto the member before it. A merge put <c>TerrainMirror.DesertPyramids</c>
    /// between <c>TerrainMirror.Desert</c> and its summary, so the pyramids carried two summaries, the first about the sand,
    /// and the sand none. The compiler says nothing about it: two <c>&lt;summary&gt;</c> elements are well-formed XML and
    /// a member without a doc comment is a warning this repository silences (CS1591, <c>Directory.Build.props</c>). So
    /// the sources are read here, and one comment block holding two summaries is the sign. The rule is seen to fire on
    /// the block the issue found, as it stood.
    /// </summary>
    public class DocCommentPlacementTests
    {
        //The folders that hold the repository's C#, the build output under each left out
        private static readonly string[] SOURCE_FOLDERS =
            { "BS3DLibs", "Game", "GamePi", "GameAndroid", "Testbed", "MapEditor", "Tools", "Tests" };

        [Fact]
        public void No_doc_comment_holds_two_summaries()
        {
            string root = RepositoryRoot();
            List<string> found = new();
            int files = 0;

            foreach (string folder in SOURCE_FOLDERS)
            {
                string directory = Path.Combine(root, folder);
                if (!Directory.Exists(directory)) continue;

                foreach (string path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
                {
                    if (InBuildOutput(path)) continue;
                    files++;
                    foreach (int line in DoubleSummaries(File.ReadAllText(path)))
                        found.Add($"{Path.GetRelativePath(root, path)}:{line}");
                }
            }

            //A scan that read nothing would pass by finding nothing
            Assert.True(files > 500, $"only {files} source files were read under {root}");
            Assert.True(found.Count == 0,
                "A doc comment block holds two summaries, so one belongs to another member (move it there): " + string.Join(", ", found));
        }

        [Fact]
        public void The_rule_fires_on_the_block_the_issue_found()
        {
            //TerrainMirror.cs as #1068's merge left it
            string misplaced = string.Join("\n",
                "        private const float SET_BLEND = 0.35f;",
                "",
                "        /// <summary>World Y of the sand at a world XZ, as <c>Desert.fx</c>'s <c>DesertHeight</c> displaces it.</summary>",
                "        /// <summary>",
                "        /// The desert's pyramids as <c>Pyramids</c> stands them (#1068): each one's centre at its foot, its side and its",
                "        /// height, Khufu first, then Khafre, Menkaure and the queens. For the desert's intro shot of them.",
                "        /// </summary>",
                "        public static List<(Vector3 Centre, float Side, float Height)> DesertPyramids(DesertSceneConfig config)");
            Assert.Equal(new[] { 3 }, DoubleSummaries(misplaced));

            //Two members each with its own summary are two blocks, whatever stands between them
            string placed = string.Join("\n",
                "        /// <summary>The pyramids.</summary>",
                "        public static int DesertPyramids() => 0;",
                "        /// <summary>The sand.</summary>",
                "        public static float Desert() => 0f;");
            Assert.Empty(DoubleSummaries(placed));
        }

        /// <summary>The 1-based first line of every run of <c>///</c> lines holding more than one summary.</summary>
        private static List<int> DoubleSummaries(string source)
        {
            List<int> starts = new();
            string[] lines = source.Split('\n');
            int blockStart = -1, summaries = 0;

            for (int i = 0; i <= lines.Length; i++)
            {
                bool doc = i < lines.Length && lines[i].TrimStart().StartsWith("///", StringComparison.Ordinal);
                if (doc)
                {
                    if (blockStart < 0)
                    {
                        blockStart = i;
                        summaries = 0;
                    }
                    summaries += Count(lines[i], "<summary>");
                    continue;
                }

                if (blockStart >= 0 && summaries > 1) starts.Add(blockStart + 1);
                blockStart = -1;
            }

            return starts;
        }

        private static int Count(string line, string what)
        {
            int count = 0;
            for (int at = line.IndexOf(what, StringComparison.Ordinal); at >= 0; at = line.IndexOf(what, at + what.Length, StringComparison.Ordinal))
                count++;
            return count;
        }

        private static bool InBuildOutput(string path)
        {
            string separator = Path.DirectorySeparatorChar.ToString();
            return path.Contains(separator + "bin" + separator, StringComparison.Ordinal)
                || path.Contains(separator + "obj" + separator, StringComparison.Ordinal);
        }

        private static string RepositoryRoot()
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "Game.sln"))) return directory.FullName;

            throw new DirectoryNotFoundException("No Game.sln was found above the test assembly.");
        }
    }
}
