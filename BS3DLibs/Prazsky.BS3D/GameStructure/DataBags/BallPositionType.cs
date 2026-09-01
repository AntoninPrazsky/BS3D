using System.Text.Json.Serialization;

namespace Prazsky.BS3D.GameStructure.DataBags
{
    /// <summary>
    /// One serialized ball: its raw grid-frame position and type. Serialized with System.Text.Json; the
    /// property names match the legacy map files (and the level format, issue #32), so both stay loadable.
    /// </summary>
    public class BallPositionType
    {
        [JsonPropertyName("x")]
        public float PositionX { get; set; }

        [JsonPropertyName("y")]
        public float PositionY { get; set; }

        [JsonPropertyName("z")]
        public float PositionZ { get; set; }

        [JsonPropertyName("t")]
        public BallType Type { get; set; }

        /// <summary>
        /// What the ball is, beside its colour (#323). <b>Written only when it is not
        /// <see cref="BallKind.Normal"/></b>, and absent means normal — so every map and level authored before
        /// kinds existed round-trips byte for byte, and an older build opens a new file and plays it with
        /// ordinary balls rather than refusing it. That is the same decision #258 took for the ball style, and
        /// it carries the same caveat: an older build playing a level whose rocks are ordinary balls has
        /// changed the level, not only its look. The format is not versioned for it because a level that
        /// <i>needs</i> the kind is a level whose own design says so, and the campaign's shipped files carry
        /// none.
        /// </summary>
        [JsonPropertyName("k")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public BallKind Kind { get; set; }
    }

    public class BallPositionTypes
    {
        /// <summary>
        /// Play field width (X). May be larger than the initial ball layout in <see cref="Balls"/>.
        /// Zero in legacy files, which carried only the layout array.
        /// </summary>
        [JsonPropertyName("sx")]
        public byte StageSizeX { get; set; }

        /// <summary>
        /// Play field depth (Z). May be larger than the initial ball layout in <see cref="Balls"/>.
        /// Zero in legacy files, which carried only the layout array.
        /// </summary>
        [JsonPropertyName("sz")]
        public byte StageSizeZ { get; set; }

        /// <summary>
        /// Play field height in levels. May be larger than the initial ball layout in <see cref="Balls"/>,
        /// leaving empty levels at the bottom for the structure to grow into.
        /// Zero in legacy files, which carried only the layout array.
        /// </summary>
        [JsonPropertyName("l")]
        public byte Levels { get; set; }

        /// <summary>
        /// The layout as nested arrays [x][z][level] with null for an empty cell. System.Text.Json has no
        /// native multidimensional-array support, so <see cref="Balls3DArrayJsonConverter"/> reads and writes
        /// this exact nested shape — the shape the legacy map files (written by Newtonsoft) already use.
        /// </summary>
        [JsonPropertyName("b")]
        [JsonConverter(typeof(Balls3DArrayJsonConverter))]
        public BallPositionType[,,] Balls { get; set; }
    }
}
