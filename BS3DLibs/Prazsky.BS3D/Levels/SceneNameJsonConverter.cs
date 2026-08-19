using Prazsky.Core.Render;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// Reads and writes <see cref="Level.Scene"/> as a plain scene NAME — <c>"scene": "moon"</c> — the same
    /// parse keys every executable's <c>scene=</c> switch takes (<see cref="SceneRenderer.TryParseScene"/>;
    /// <c>"neon"</c> for the neon city). Since format version 2 a level names its scene and nothing more:
    /// the scene's parameters are fixed in code (the <see cref="SceneConfig"/> class defaults), so there is
    /// nothing else to carry.
    /// <para>
    /// Reading is deliberately LENIENT, the music field's precedent: an unknown name is a null scene (the
    /// consumer keeps its current backdrop) rather than a refused file. A version-1 level — where "scene"
    /// was a full serialized <see cref="SceneConfig"/> object — still loads: the object's "kind"
    /// discriminator is read, its "Neon" flag maps the city onto the neon city (version 1 served both from
    /// one config under kind "city"), and every other value is ignored. That loses nothing in practice:
    /// every shipped level carried pure defaults, verified file-by-file before the change — Colossus.json
    /// literally said <c>{"kind": "moon"}</c>.
    /// </para>
    /// </summary>
    internal sealed class SceneNameJsonConverter : JsonConverter<SceneKind?>
    {
        public override SceneKind? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return SceneRenderer.TryParseScene(reader.GetString(), out SceneKind named) ? named : null;

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using JsonDocument doc = JsonDocument.ParseValue(ref reader);

                if (!doc.RootElement.TryGetProperty("kind", out JsonElement kindElement)
                    || kindElement.ValueKind != JsonValueKind.String
                    || !SceneRenderer.TryParseScene(kindElement.GetString(), out SceneKind kind))
                    return null;

                //Version 1 told the city and the neon city apart by a bool inside the config object
                if (kind == SceneKind.City
                    && doc.RootElement.TryGetProperty("Neon", out JsonElement neon)
                    && neon.ValueKind == JsonValueKind.True)
                    return SceneKind.NeonCity;

                return kind;
            }

            //Null, or a shape no version ever wrote: consume it and leave the scene unset
            if (reader.TokenType is JsonTokenType.StartArray) reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, SceneKind? value, JsonSerializerOptions options)
        {
            if (value is not SceneKind kind)
            {
                writer.WriteNullValue();
                return;
            }

            //The one kind whose parse key is not its own name lowercased
            writer.WriteStringValue(kind == SceneKind.NeonCity ? "neon" : kind.ToString().ToLowerInvariant());
        }
    }
}
