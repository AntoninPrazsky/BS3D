using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Game's half of #1192's two answers to a moving cluster. <see cref="ShotRig"/> fires as the Game does when a test
    /// turns them on (<see cref="ShotRig.StampsPromise"/>, <see cref="ShotRig.Leads"/>), and
    /// <c>ShotPushTests.ShotsIntoAPushedClusterLandWhereTheirGhostShowed</c> holds what each is worth there; but the Game's
    /// own preview and <c>Shoot</c> need a graphics device and run in no test, and a dropped argument there (the preview
    /// solved without <c>lead</c>, a shot that leaves without its ghost's promise) would take the answer out of play with
    /// every test still green. So these read the source.
    /// </summary>
    public class ShotPromiseSourceTests
    {
        private static string Code(string file)
        {
            string relative = Path.Combine("Game", "Screens", file);
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);

                //The text without its comments, so a word in a note is not taken for code
                if (File.Exists(candidate)) return Regex.Replace(File.ReadAllText(candidate), @"//[^\n]*", string.Empty);
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        /// <summary>The aim's preview solves the ghost leading a moving cluster.</summary>
        [Fact]
        public void ThePreviewLeadsAMovingCluster()
        {
            Match call = Regex.Match(Code("GameplayScreen.Draw.cs"),
                @"ShotPlacement\.TryFindFirstHitCurved\(_physicsBalls,\s*muzzle,\s*aim\s*\*\s*ShotSpeed,[^;]*\)");
            Assert.True(call.Success, "the preview's solve (TryFindFirstHitCurved from the muzzle along the aim) was not found");
            Assert.Matches(@"lead:\s*true", call.Value);
        }

        /// <summary>A shot leaves carrying the cell its ghost showed and the ball that cell was solved against.</summary>
        [Fact]
        public void AShotLeavesWithItsGhostsPromise()
        {
            string code = Code("GameplayScreen.Input.cs");
            Assert.Matches(@"PromisedCell\s*=\s*_previewHasCell\s*\?\s*_previewCell\s*:\s*null", code);
            Assert.Matches(@"PromisedAgainst\s*=\s*_previewAgainst", code);
        }
    }
}
