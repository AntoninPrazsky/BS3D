using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Render;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// A complete level (issue #32): the ball map plus everything that reproduces its look — the scene
    /// backdrop with its full <see cref="SceneConfig"/> (issue #44), the sky dome and metadata. Serialized
    /// with System.Text.Json, like the plain map files since the Newtonsoft migration (issue #38). A level
    /// file is a JSON object marked by <c>"format": "bs3d-level"</c>, which is how <see cref="IsLevelFile"/>
    /// tells it from a plain map file — both use the .json extension.
    ///
    /// The ceiling, the physics statics and the cannon/camera placement are deliberately not stored:
    /// they are all derived from the map size at load (FitCeilingToMap / FitCannonAndGameCameraToMap).
    /// The format is versioned so more can be added later (lighting overrides, audio, par/scoring…).
    /// </summary>
    public sealed class Level
    {
        public const string FormatMarker = "bs3d-level";
        public const int CurrentVersion = 1;

        /// <summary>File-type marker; always <see cref="FormatMarker"/> in a valid level file.</summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = FormatMarker;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        /// <summary>Display name of the level (optional).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Author of the level (optional).</summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>Sky dome number (1–18), as cycled by NumPad1. The level's dome wins over any scene-entry default.</summary>
        [JsonPropertyName("sky")]
        public byte SkyDome { get; set; } = 1;

        /// <summary>
        /// The scene backdrop and its full configuration, polymorphic on the "kind" discriminator
        /// ("sea"/"desert"/"savanna"/"mountain"/"meadow"/"city"). Null leaves the consumer's current
        /// backdrop untouched (the Testbed keeps whatever scene is up).
        /// </summary>
        [JsonPropertyName("scene")]
        public SceneConfig Scene { get; set; }

        /// <summary>The ball map, in the same shape as a legacy map file (sx/sz/l/b).</summary>
        [JsonPropertyName("map")]
        public BallPositionTypes Map { get; set; }

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            //Hand-edited files may not keep the "kind" discriminator as the first property
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>Loads and validates a level file. Throws (rather than returning a broken level) so a caller can leave its current state untouched on a bad file.</summary>
        public static Level Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            Level level = JsonSerializer.Deserialize<Level>(stream, Options)
                ?? throw new InvalidDataException($"'{path}' does not contain a level");

            if (level.Format != FormatMarker)
                throw new InvalidDataException($"'{path}' is not a level file (format marker '{level.Format}')");
            if (level.Version > CurrentVersion)
                throw new InvalidDataException($"'{path}' is a version {level.Version} level; this build reads up to {CurrentVersion}");
            if (level.Map?.Balls == null)
                throw new InvalidDataException($"'{path}' carries no ball map");

            return level;
        }

        public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Options));

        /// <summary>
        /// Cheap probe: true when the file is a JSON object carrying the level format marker. A legacy map
        /// file (or any unreadable/non-JSON file) returns false, so a caller can route to the map loader.
        /// </summary>
        public static bool IsLevelFile(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                using JsonDocument doc = JsonDocument.Parse(stream);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("format", out JsonElement format)
                    && format.ValueKind == JsonValueKind.String
                    && format.GetString() == FormatMarker;
            }
            //A best-effort probe: anything that is not a readable JSON file — a directory or ACL-denied path
            //(UnauthorizedAccessException, not an IOException subclass, e.g. a folder dragged onto the window),
            //an invalid path (ArgumentException), or malformed JSON — is "not a level file", so the caller
            //routes it to the map loader rather than the probe throwing.
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }
}
