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
