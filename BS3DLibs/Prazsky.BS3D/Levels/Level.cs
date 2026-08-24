using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Render;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// A complete level (issue #32): the ball map plus everything that reproduces its look — the scene it
    /// plays in, the sky dome and metadata. Serialized
    /// with System.Text.Json, like the plain map files since the Newtonsoft migration (issue #38). A level
    /// file is a JSON object marked by <c>"format": "bs3d-level"</c>, which is how <see cref="IsLevelFile"/>
    /// tells it from a plain map file — both use the .json extension.
    ///
    /// The ceiling, the physics statics and the cannon/camera placement are deliberately not stored:
    /// they are all derived from the map size at load (FitCeilingToMap / FitCannonAndGameCameraToMap).
    /// Neither are the scene's parameters, since format version 2: a level names its scene and the scene's
    /// look is fixed in code (the <see cref="SceneConfig"/> class defaults) — version 1 serialized the whole
    /// config into every file, and every shipped file turned out to carry pure defaults, tens of kilobytes
    /// of them restating what the code already said. A level only carries what cannot be re-derived.
    /// The format is versioned so more can be added later (lighting overrides, audio, par/scoring…).
    /// </summary>
    public sealed class Level
    {
        public const string FormatMarker = "bs3d-level";

        /// <summary>
        /// 2 since the scene became a name: version 1 stored <c>"scene"</c> as a full polymorphic
        /// <see cref="SceneConfig"/> object. The reader takes both shapes
        /// (<see cref="SceneNameJsonConverter"/>), so old files load unchanged; the bump exists for the other
        /// direction, so a build older than the change refuses a new file with a plain versioned message
        /// instead of a polymorphism exception out of the serializer.
        /// </summary>
        public const int CurrentVersion = 2;

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
        /// The scene backdrop, by name — the same parse keys the <c>scene=</c> command line takes
        /// (<see cref="SceneRenderer.TryParseScene"/>; <c>"neon"</c> for the neon city). The scene's
        /// parameters are fixed in code, so the name is all a level says about its backdrop. Null — absent,
        /// or an unknown spelling, the music field's leniency — leaves the consumer's current backdrop
        /// untouched (the Testbed keeps whatever scene is up).
        /// </summary>
        [JsonPropertyName("scene")]
        [JsonConverter(typeof(SceneNameJsonConverter))]
        public SceneKind? Scene { get; set; }

        /// <summary>
        /// Which composition this level plays (#120) — <c>"pulse"</c>, <c>"bohemia"</c>, <c>"nocturne"</c>,
        /// <c>"mural"</c> or <c>"ember"</c>. Null, which is the normal case, leaves it to the set's own
        /// rotation: a level's position picks the piece, so a set has variety without a single file having
        /// to say anything. Name one only to pin it.
        /// <para>
        /// A string rather than an enum on purpose: the music lives in the game executable and this format is
        /// the shared library's, so the name is a parse key exactly as a scene's is
        /// (<c>ProceduralMusic.ThemeFor</c>), and an unknown spelling falls back rather than throwing.
        /// </para>
        /// </summary>
        [JsonPropertyName("music")]
        public string Music { get; set; }

        /// <summary>
        /// What this level's balls are made of (#258) — <c>"beach"</c>, the moulded vinyl the game has always
        /// drawn, or <c>"bubble"</c>, a hollow glass bubble with the type colour dyed into its film. Null,
        /// which is what every level authored before the field existed says, is the beach ball: absent means
        /// unchanged, so no shipped file had to be rewritten and none was.
        /// <para>
        /// It carries no format bump, and that is the deliberate half of the decision. An older build ignores
        /// an unknown property and draws the level in vinyl — the level still opens, still plays and still
        /// scores identically, because nothing but the shading reads this. That is a degraded LOOK, which is
        /// what the version gate is for refusing when it would be a broken FILE; the same argument admitted
        /// <see cref="Music"/> at version 2 without a bump.
        /// </para>
        /// </summary>
        [JsonPropertyName("balls")]
        [JsonConverter(typeof(BallStyleJsonConverter))]
        public BallStyle? Balls { get; set; }

        /// <summary>The ball map, in the same shape as a legacy map file (sx/sz/l/b).</summary>
        [JsonPropertyName("map")]
        public BallPositionTypes Map { get; set; }

        //AllowOutOfOrderMetadataProperties stood here while the scene was polymorphic, so a hand-edited file
        //that did not keep the "kind" discriminator first still loaded. Nothing in a level is polymorphic
        //since format 2 — a legacy scene object is read by SceneNameJsonConverter's own JsonDocument, never
        //by the serializer's metadata machinery — so the option would be a switch guarding nothing.
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
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
