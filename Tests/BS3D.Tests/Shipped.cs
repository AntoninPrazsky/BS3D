using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

//One test at a time, deliberately. BallsConstraintsBuilder caches the sphere's shape index in two statics keyed
//on the last Simulation it was asked about (GetSphereShapeIndex) - correct for a game with one simulation,
//a race for a runner that builds several at once. The whole gate runs in a few seconds serially, so the
//parallelism is not worth teaching the library a thread-safety it has no other use for.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BS3D.Tests
{
    /// <summary>
    /// The shipped data the tests read: <c>Game\Levels</c>, found by walking up from the test's own bin
    /// directory by landmark rather than by counting <c>..</c> — the way Tools/ScoreSim and Tools/LevelGen find it.
    /// </summary>
    internal static class Shipped
    {
        private static readonly Lazy<string> _levelsDirectory = new(FindLevelsDirectory);

        public static string LevelsDirectory => _levelsDirectory.Value;

        /// <summary>Every level file in the directory (the level set's own file is not one), sorted by name.</summary>
        public static IReadOnlyList<string> LevelFiles() =>
            Directory.GetFiles(LevelsDirectory, "*.json")
                .Where(Prazsky.BS3D.Levels.Level.IsLevelFile)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

        public static string Level(string fileName)
        {
            string path = Path.Combine(LevelsDirectory, fileName);
            Assert.True(File.Exists(path), $"Shipped level '{fileName}' is missing from {LevelsDirectory}");
            return path;
        }

        /// <summary>xUnit's shape for one row per shipped level.</summary>
        public static IEnumerable<object[]> LevelFileNames() =>
            LevelFiles().Select(p => new object[] { Path.GetFileName(p) });

        private static string FindLevelsDirectory()
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Game", "Levels");
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException("No Game\\Levels directory was found above the test assembly.");
        }
    }

    /// <summary>A scratch directory of the test's own, removed when the test ends.</summary>
    internal sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bs3d-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
