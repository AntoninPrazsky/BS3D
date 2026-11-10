using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The third-party notices every package carries (#1081), <c>Game/THIRD-PARTY-NOTICES.txt</c>. A package added to one of
    /// the three game projects without a line here would ship in the next release with no licence beside it, and only the
    /// release itself would show it, so the notices are held to the project files: every package a game project references
    /// is named, and every game project carries the file. release.yml and build.yml check that the packages they build
    /// really hold it.
    /// </summary>
    public class ThirdPartyNoticesTests
    {
        //The three projects that ship: the Windows zip, the Pi tarball, the APK
        private static readonly string[] Projects =
        {
            Path.Combine("Game", "Game.csproj"), Path.Combine("GamePi", "GamePi.csproj"), Path.Combine("GameAndroid", "GameAndroid.csproj"),
        };

        //Referenced only to build: it compiles the content and ships nothing of its own
        private static readonly string[] BuildOnly = { "MonoGame.Content.Builder.Task" };

        private static readonly string Notices = File.ReadAllText(FindUnderRepository(Path.Combine("Game", "THIRD-PARTY-NOTICES.txt")));

        public static TheoryData<string> ShippedPackages()
        {
            var data = new TheoryData<string>();

            foreach (string package in Projects
                .SelectMany(project => Regex.Matches(File.ReadAllText(FindUnderRepository(project)), "<PackageReference Include=\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value))
                .Except(BuildOnly).Distinct().OrderBy(name => name, StringComparer.Ordinal))
                data.Add(package);

            return data;
        }

        [Theory]
        [MemberData(nameof(ShippedPackages))]
        public void Every_package_a_game_project_references_is_named(string package)
        {
            Assert.True(Notices.Contains(package, StringComparison.Ordinal),
                $"{package} is referenced by a game project but THIRD-PARTY-NOTICES.txt does not name it: add it, its version and its licence");
        }

        [Theory]
        [InlineData("SDL")]
        [InlineData("OpenAL Soft")]
        [InlineData("GNU Library General Public License")]
        public void The_native_libraries_and_their_licence_are_named(string text)
        {
            //The two native libraries the DesktopGL and Android packages bring with them, which no PackageReference names
            Assert.Contains(text, Notices);
        }

        [Theory]
        [MemberData(nameof(ProjectFiles))]
        public void Every_game_project_carries_the_notices(string project)
        {
            Assert.Contains("THIRD-PARTY-NOTICES.txt", File.ReadAllText(FindUnderRepository(project)));
        }

        public static TheoryData<string> ProjectFiles() => new(Projects);

        [Fact]
        public void The_notices_are_plain_LF_text()
        {
            //.gitattributes holds every file to LF; a CR or a BOM here would come from pasting a licence in by hand
            //(read as bytes: File.ReadAllText takes a BOM off before anyone sees it)
            byte[] bytes = File.ReadAllBytes(FindUnderRepository(Path.Combine("Game", "THIRD-PARTY-NOTICES.txt")));

            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "THIRD-PARTY-NOTICES.txt begins with a byte-order mark");
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
