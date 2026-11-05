using Prazsky.Core.Tools;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The door to the shipped files (#1049): the two answers only a packaged build (Android) ever asks for, here where a
    /// test can reach them. A file name against the patterns the readers pass (the music's <c>*.ogg</c>, the shaders'
    /// <c>*.xnb</c>, the libraries' <c>Prazsky.*.dll</c>), ignoring case as <c>Directory.GetFiles</c> does on Windows; and a
    /// path's place under the base directory in the store's spelling, a sibling folder that only starts with the base's
    /// name not counted as under it.
    /// </summary>
    public class ShippedFilesTests
    {
        [Theory]
        [InlineData("menu.ogg", "*.ogg", true)]
        [InlineData("MENU.OGG", "*.ogg", true)]
        [InlineData("menu.ogg.txt", "*.ogg", false)]
        [InlineData("ogg", "*.ogg", false)]
        [InlineData(".ogg", "*.ogg", true)]
        [InlineData("Prazsky.Core.dll", "Prazsky.*.dll", true)]
        [InlineData("Prazsky.dll", "Prazsky.*.dll", false)]
        [InlineData("PotatoModel.xnb", "*.xnb", true)]
        [InlineData("a1.json", "a?.json", true)]
        [InlineData("a12.json", "a?.json", false)]
        [InlineData("anything", "*", true)]
        public void APatternMatchesAsDirectoryGetFilesDoes(string name, string pattern, bool matches) =>
            Assert.Equal(matches, ShippedFiles.Matches(name, pattern));

        [Fact]
        public void APathUnderTheBaseIsNamedRelativeToItWithSlashes()
        {
            string root = Path.Combine(Path.GetTempPath(), "bs3d-shipped", "base");

            Assert.True(ShippedFiles.TryRelative(Path.Combine(root, "Music", "Drums", "x.ogg"), root, out string relative));
            Assert.Equal("Music/Drums/x.ogg", relative);

            //The base with or without its trailing separator, and the base itself
            Assert.True(ShippedFiles.TryRelative(Path.Combine(root, "Levels", "Levels.json"), root + Path.DirectorySeparatorChar, out relative));
            Assert.Equal("Levels/Levels.json", relative);
            Assert.True(ShippedFiles.TryRelative(root, root, out relative));
            Assert.Equal(string.Empty, relative);

            //A folder named on from the base, as a reader that combines a directory and a file builds it
            Assert.True(ShippedFiles.TryRelative(Path.Combine(root, "Content", "Shaders") + Path.DirectorySeparatorChar, root, out relative));
            Assert.Equal("Content/Shaders", relative);
        }

        [Fact]
        public void APathOutsideTheBaseIsTheFileSystems()
        {
            string root = Path.Combine(Path.GetTempPath(), "bs3d-shipped", "base");

            Assert.False(ShippedFiles.TryRelative(Path.Combine(Path.GetTempPath(), "bs3d-shipped", "base2", "Levels.json"), root, out _));
            Assert.False(ShippedFiles.TryRelative(Path.Combine(Path.GetTempPath(), "bs3d-shipped", "Progress.json"), root, out _));
            Assert.False(ShippedFiles.TryRelative(string.Empty, root, out _));
        }

        [Fact]
        public void WithNoStoreEveryAnswerIsTheFileSystems()
        {
            //Nothing installs a store outside the Android build, so the desktop builds, the tools and these tests read files
            Assert.False(ShippedFiles.IsPackaged);

            string folder = Path.Combine(Path.GetTempPath(), "bs3d-shipped-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string file = Path.Combine(folder, "a.json");
                File.WriteAllText(file, "{}");

                Assert.True(ShippedFiles.Exists(file));
                Assert.True(ShippedFiles.DirectoryExists(folder));
                Assert.Equal(new[] { file }, ShippedFiles.GetFiles(folder, "*.json"));
                Assert.Equal(File.ReadAllBytes(file), ShippedFiles.ReadAllBytes(file));
                using (Stream stream = ShippedFiles.OpenRead(file)) Assert.True(stream.CanSeek);
                Assert.False(ShippedFiles.Exists(Path.Combine(folder, "missing.json")));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
