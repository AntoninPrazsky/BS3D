using Newtonsoft.Json;

namespace Prazsky.BS3D.GameStructure.DataBags
{
	/// <summary>
	/// One serialized ball: its raw grid-frame position and type. Carries both Newtonsoft attributes (the
	/// legacy map files) and System.Text.Json ones (the level format, issue #32) so the JSON shape is
	/// identical whichever serializer reads or writes it.
	/// </summary>
	public class BallPositionType
    {
        [JsonProperty("x")]
        [System.Text.Json.Serialization.JsonPropertyName("x")]
        public float PositionX { get; set; }

		[JsonProperty("y")]
		[System.Text.Json.Serialization.JsonPropertyName("y")]
		public float PositionY { get; set; }

		[JsonProperty("z")]
		[System.Text.Json.Serialization.JsonPropertyName("z")]
		public float PositionZ { get; set; }

		[JsonProperty("t")]
		[System.Text.Json.Serialization.JsonPropertyName("t")]
		public BallType Type { get; set; }
    }

	public class BallPositionTypes
    {
		/// <summary>
		/// Play field width (X). May be larger than the initial ball layout in <see cref="Balls"/>.
		/// Zero in legacy files, which carried only the layout array.
		/// </summary>
		[JsonProperty("sx")]
		[System.Text.Json.Serialization.JsonPropertyName("sx")]
		public byte StageSizeX { get; set; }

		/// <summary>
		/// Play field depth (Z). May be larger than the initial ball layout in <see cref="Balls"/>.
		/// Zero in legacy files, which carried only the layout array.
		/// </summary>
		[JsonProperty("sz")]
		[System.Text.Json.Serialization.JsonPropertyName("sz")]
		public byte StageSizeZ { get; set; }

		/// <summary>
		/// Play field height in levels. May be larger than the initial ball layout in <see cref="Balls"/>,
		/// leaving empty levels at the bottom for the structure to grow into.
		/// Zero in legacy files, which carried only the layout array.
		/// </summary>
		[JsonProperty("l")]
		[System.Text.Json.Serialization.JsonPropertyName("l")]
		public byte Levels { get; set; }

		/// <summary>
		/// The layout as nested arrays [x][z][level] with null for an empty cell — the shape Newtonsoft
		/// gives a 3D array natively; the System.Text.Json converter reproduces it exactly.
		/// </summary>
		[JsonProperty("b")]
		[System.Text.Json.Serialization.JsonPropertyName("b")]
		[System.Text.Json.Serialization.JsonConverter(typeof(Balls3DArrayJsonConverter))]
		public BallPositionType[,,] Balls { get; set; }
    }
}
