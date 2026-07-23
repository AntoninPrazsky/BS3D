using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prazsky.BS3D.GameStructure.DataBags
{
    /// <summary>
    /// System.Text.Json converter for the map's 3D ball array. System.Text.Json cannot serialize
    /// multi-dimensional arrays on its own, so this writes and reads the exact nested shape the legacy map
    /// files use for <c>BallPositionType[,,]</c> (they were written by Newtonsoft, which does it natively):
    /// nested arrays with dimension 0 outermost, i.e. <c>[x][z][level]</c>, with <c>null</c> for an empty
    /// cell. Both a plain map file and a level file's embedded map therefore stay byte-shape-compatible.
    /// </summary>
    public sealed class Balls3DArrayJsonConverter : JsonConverter<BallPositionType[,,]>
    {
        public override BallPositionType[,,] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            //Read as jagged (which System.Text.Json handles natively), then validate it is rectangular
            BallPositionType[][][] jagged = JsonSerializer.Deserialize<BallPositionType[][][]>(ref reader, options);
            if (jagged == null) return null;

            int sizeX = jagged.Length;
            if (sizeX == 0) return new BallPositionType[0, 0, 0];

            if (jagged[0] == null) throw new JsonException("Map array row is null");
            int sizeZ = jagged[0].Length;
            int sizeLevel = sizeZ > 0 ? (jagged[0][0]?.Length ?? throw new JsonException("Map array column is null")) : 0;

            var result = new BallPositionType[sizeX, sizeZ, sizeLevel];

            for (int x = 0; x < sizeX; x++)
            {
                if (jagged[x] == null || jagged[x].Length != sizeZ)
                    throw new JsonException("Map array is not rectangular");

                for (int z = 0; z < sizeZ; z++)
                {
                    if (jagged[x][z] == null || jagged[x][z].Length != sizeLevel)
                        throw new JsonException("Map array is not rectangular");

                    for (int level = 0; level < sizeLevel; level++)
                        result[x, z, level] = jagged[x][z][level];
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, BallPositionType[,,] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            for (int x = 0; x < value.GetLength(0); x++)
            {
                writer.WriteStartArray();
                for (int z = 0; z < value.GetLength(1); z++)
                {
                    writer.WriteStartArray();
                    for (int level = 0; level < value.GetLength(2); level++)
                        JsonSerializer.Serialize(writer, value[x, z, level], options);
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
    }
}
