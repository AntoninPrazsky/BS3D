using System;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The colour cue's figures on the Potato path (#828) are <c>BallColourCue.fxh</c>'s, verbatim: the slice of
    /// <c>PotatoModel.fx</c> between its two marker lines is the same text as the desktop file from the solids to the
    /// end of <c>ColourCueMotif</c>. A figure changed on one side and not the other fails here, which is the rule
    /// <c>docs/rendering/ball-styles.md</c> states for <c>ColourCueField.cs</c> as well.
    /// </summary>
    public class PotatoColourCueSourceTests
    {
        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        private static string Normalised(string text) => text.Replace("\r\n", "\n").Trim();

        [Fact]
        public void ThePotatoCopyOfTheFiguresIsTheDesktopsVerbatim()
        {
            string desktop = File.ReadAllText(FindUnderRepository(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "InstancedModel", "BallColourCue.fxh")));
            string potato = File.ReadAllText(FindUnderRepository(Path.Combine("GamePi", "Shaders", "PotatoModel.fx")));

            const string from = "//The regular solids' vertices";
            const string to = "//The ink at this pixel";
            int start = desktop.IndexOf(from, StringComparison.Ordinal), end = desktop.IndexOf(to, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, "BallColourCue.fxh no longer has the solids' comment and the ink's in that order");
            string expected = Normalised(desktop[start..end]);

            const string open = "//---- BallColourCue.fxh, verbatim from here ----";
            const string close = "//---- to here ----";
            int opened = potato.IndexOf(open, StringComparison.Ordinal), closed = potato.IndexOf(close, StringComparison.Ordinal);
            Assert.True(opened >= 0 && closed > opened, "PotatoModel.fx no longer marks its copy of the figures");
            string actual = Normalised(potato[(opened + open.Length)..closed]);

            Assert.True(expected == actual, "PotatoModel.fx's copy of the colour cue's figures differs from BallColourCue.fxh: a figure changed on one side is changed on the other in the same commit (docs/rendering/ball-styles.md)");
            Assert.Contains("float ColourCueMotif(", actual);
        }
    }
}
