using Prazsky.BS3D.GameStructure;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// Reads and writes <see cref="Level.Balls"/> as a plain style NAME — <c>"balls": "bubble"</c> — through
    /// <see cref="BallStyles"/>, which is the one place those spellings live.
    /// <para>
    /// Lenient in exactly the way <see cref="SceneNameJsonConverter"/> is, and for the same reason: an unknown
    /// spelling reads as null, which every consumer takes as "the vinyl beach ball", rather than as a level
    /// that will not open. A look is not worth throwing a whole file away over.
    /// </para>
    /// </summary>
    internal sealed class BallStyleJsonConverter : JsonConverter<BallStyle?>
    {
        public override BallStyle? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && BallStyles.TryParse(reader.GetString(), out BallStyle named))
                return named;

            //Null, or any shape no version ever wrote: consume it and leave the style unset
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, BallStyle? value, JsonSerializerOptions options)
        {
            if (value is not BallStyle style) writer.WriteNullValue();
            else writer.WriteStringValue(BallStyles.ToName(style));
        }
    }
}
