using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// No code calls <c>GraphicsDevice.SetVertexBuffers</c> except <c>Prazsky.Core.Render.InstanceBinding</c>. The call
    /// takes <c>params VertexBufferBinding[]</c>, so two bindings written inline build a new array on every draw. With
    /// about seventy-five instanced draws a frame, that was nearly all of the frame thread's steady allocation:
    /// 4.2 KB a frame on the main menu, measured with an allocation-tick listener (BestPractices.md §3).
    /// <para>
    /// The check reads the source, because the allocation happens inside a draw no test can make. It scans every
    /// project that draws: the libraries, the three executables and GamePi's own sources. It was seen to fail on the
    /// tree before the change, which had fourteen such calls.
    /// </para>
    /// </summary>
    public class InstanceBindingSourceTests
    {
        private static readonly string[] Projects = { "BS3DLibs", "Game", "Testbed", "MapEditor", "GamePi" };

        [Fact]
        public void Only_InstanceBinding_sets_vertex_buffers()
        {
            string root = RepositoryRoot();
            List<string> offenders = new();
            int scanned = 0;

            foreach (string project in Projects)
            {
                string folder = Path.Combine(root, project);
                if (!Directory.Exists(folder)) continue;

                foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                        file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;

                    scanned++;
                    if (Path.GetFileName(file) == "InstanceBinding.cs") continue;

                    string code = Regex.Replace(File.ReadAllText(file), @"//[^\r\n]*", string.Empty);
                    if (code.Contains("SetVertexBuffers(", StringComparison.Ordinal))
                        offenders.Add(Path.GetRelativePath(root, file));
                }
            }

            //The scan found the code, or an empty list would pass by finding nothing
            Assert.True(scanned > 100, $"Only {scanned} source files were scanned; update this test with the tree.");
            Assert.True(offenders.Count == 0,
                "SetVertexBuffers is called outside InstanceBinding (a params array per draw): " + string.Join(", ", offenders));
        }

        private static string RepositoryRoot()
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BS3DLibs", "Prazsky.Core", "Render", "InstanceBinding.cs")))
                    return directory.FullName;
            }

            throw new DirectoryNotFoundException("No repository root (BS3DLibs/Prazsky.Core/Render/InstanceBinding.cs) was found above the test assembly.");
        }
    }
}
