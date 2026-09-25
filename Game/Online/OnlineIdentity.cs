using Prazsky.Core.Tools;
using System;
using System.Buffers.Text;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BS3D.Online
{
    /// <summary>
    /// Who this install is to the online score boards (#546, the decisions in #542): a random player id, a
    /// random token and the nickname the player chose — <c>%LOCALAPPDATA%\BS3D\Online.json</c>. There are no
    /// accounts. The id is what the boards group a player's clears by, the nickname is the only thing anyone
    /// else ever sees, and the token is proof of "the same install": the service keeps only its hash and
    /// accepts a submission, a rename or a deletion for that id only with it.
    /// <para>
    /// <b>Its own file, not a row of <see cref="GameSettings"/></b>, because the settings file is rewritten
    /// by every settings click and restored by hand after scripted runs, and a token lost to either is a player
    /// who can no longer reach their own scores. It goes through <see cref="AtomicFile"/> with a backup for the
    /// save's reason.
    /// </para>
    /// <para>
    /// <b>Created when the player opts in and removed when they remove their scores</b> — both are the settings
    /// page's (#548), which is why <see cref="Create"/> and <see cref="Save"/> have no caller in this issue.
    /// The client (<see cref="OnlineScores"/>) only reads it, and a missing or unusable one means the player has
    /// not opted in, whatever <see cref="GameSettings.Online"/> says.
    /// </para>
    /// </summary>
    internal sealed class OnlineIdentity
    {
        internal const string FormatMarker = "bs3d-online";
        internal const int CurrentVersion = 1;

        internal const string DefaultFileName = "Online.json";
        internal const string BackupSuffix = ".bak";

        /// <summary>Random bytes in a token: 256 bits, which nobody guesses and nobody brute-forces.</summary>
        private const int TokenBytes = 32;

        [JsonPropertyName("format")]
        public string Format { get; set; } = FormatMarker;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        [JsonPropertyName("playerId")]
        public Guid PlayerId { get; set; }

        /// <summary>Base64url, no padding — it travels in an <c>Authorization: Bearer</c> header as it is.</summary>
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Whether this is an identity anything can be sent under: an id, a token and a name.</summary>
        [JsonIgnore]
        internal bool IsUsable => PlayerId != Guid.Empty && !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(Name);

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>A fresh identity under <paramref name="name"/> — a new id and a new token, never reused.</summary>
        internal static OnlineIdentity Create(string name) => new()
        {
            PlayerId = Guid.NewGuid(),
            Token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes)),
            Name = name,
        };

        /// <summary>
        /// The identity at <paramref name="path"/>, or its backup, or null — lenient, as the settings are: a
        /// missing file is a player who has not opted in, and an unreadable one must cost the online boards,
        /// never the game.
        /// </summary>
        internal static OnlineIdentity Load(string path)
        {
            OnlineIdentity identity = TryRead(path);
            if (identity != null) return identity;

            //⚠ The one player file whose loss cannot be recovered from (#571): the token in it is the only proof
            //that the scores under this id are the player's, and the only way to remove them (#548). A file this
            //build cannot read - damaged, or a newer build's - reads as "not opted in", and opting in again mints
            //a new identity over it. Kept aside first, so the token survives by hand whatever happens next.
            string kept = AtomicFile.KeepUnreadable(path);
            identity = TryRead(path + BackupSuffix);
            kept ??= identity == null ? AtomicFile.KeepUnreadable(path + BackupSuffix) : null;

            if (kept != null)
                Console.WriteLine($"[online] '{path}' would not read (damaged, or a newer build's);"
                    + $" {(identity != null ? "using its backup" : "treated as not opted in")}, and the file is kept as '{kept}'");

            return identity;
        }

        internal void Save(string path) => AtomicFile.WriteText(path, JsonSerializer.Serialize(this, Options), BackupSuffix);

        private static OnlineIdentity TryRead(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                OnlineIdentity identity = JsonSerializer.Deserialize<OnlineIdentity>(stream, Options);
                if (identity?.Format == FormatMarker && identity.Version <= CurrentVersion) return identity;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException)
            {
            }

            return null;
        }
    }
}
