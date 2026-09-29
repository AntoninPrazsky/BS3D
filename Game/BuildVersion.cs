using System.Reflection;

namespace BS3D
{
    /// <summary>
    /// Which build this is, as a player and a score server read it: <c>v0.2.1</c> out of a release, and
    /// <c>dev-&lt;short sha&gt;</c> out of anything else. One place for it since the front end started to show it
    /// — it lived in the online client alone, which is not where a menu should have to ask.
    /// </summary>
    /// <remarks>
    /// <b>A release is a build that says so, and it says so through a property rather than through its version
    /// number</b> (#546): <c>release.yml</c> passes the tag as <c>-p:BS3DReleaseVersion=</c> on a tag build and only
    /// then, <c>Game.csproj</c> turns it into an assembly attribute, and this class reads it back. A local build's
    /// default <c>1.0.0</c> and a suffixed tag's cannot be told apart by looking at the version, which is the whole
    /// reason for the property.
    /// </remarks>
    internal static class BuildVersion
    {
        /// <summary>
        /// The assembly metadata key <c>release.yml</c> stamps the tag under (<c>-p:BS3DReleaseVersion=</c>,
        /// turned into an attribute by <c>Game.csproj</c>) — on a tag build and only then.
        /// </summary>
        private const string ReleaseMetadataKey = "BS3DReleaseVersion";

        /// <summary>
        /// The release this build came out of (the tag, <c>v0.2.1</c>), or null for any other build. What decides
        /// whether the built-in score server may be used (<see cref="Online.OnlineScores"/>).
        /// </summary>
        internal static string Release { get; } = ReadRelease();

        /// <summary>
        /// What the build calls itself: <see cref="Release"/>, or <c>dev-&lt;short sha&gt;</c> — the name
        /// <c>release.yml</c> gives a rehearsal run — so a screenshot, a log and a score server's request can all say
        /// which build produced them.
        /// </summary>
        internal static string Name { get; } = Release ?? DevName();

        private static string ReadRelease()
        {
            foreach (AssemblyMetadataAttribute metadata in typeof(BuildVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                if (metadata.Key == ReleaseMetadataKey && !string.IsNullOrWhiteSpace(metadata.Value))
                    return metadata.Value;

            return null;
        }

        /// <summary>
        /// <c>dev-&lt;short sha&gt;</c> off the informational version the SDK stamps (<c>1.0.0+&lt;sha&gt;</c>),
        /// or plain <c>dev</c> when the build carried no commit.
        /// </summary>
        private static string DevName()
        {
            string informational = typeof(BuildVersion).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            int plus = informational?.IndexOf('+') ?? -1;
            if (plus < 0 || plus + 1 >= informational.Length) return "dev";

            string sha = informational[(plus + 1)..];
            return "dev-" + (sha.Length > 7 ? sha[..7] : sha);
        }
    }
}
