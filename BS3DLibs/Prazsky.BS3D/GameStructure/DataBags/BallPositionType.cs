using Newtonsoft.Json;

namespace Prazsky.BS3D.GameStructure.DataBags
{
	public class BallPositionType
    {
        [JsonProperty("x")]
        public float PositionX;

		[JsonProperty("y")]
		public float PositionY;

		[JsonProperty("z")]
		public float PositionZ;

		[JsonProperty("t")]
		public BallType Type;
    }

	public class BallPositionTypes
    {
		/// <summary>
		/// Play field width (X). May be larger than the initial ball layout in <see cref="Balls"/>.
		/// Zero in legacy files, which carried only the layout array.
		/// </summary>
		[JsonProperty("sx")]
		public byte StageSizeX;

		/// <summary>
		/// Play field depth (Z). May be larger than the initial ball layout in <see cref="Balls"/>.
		/// Zero in legacy files, which carried only the layout array.
		/// </summary>
		[JsonProperty("sz")]
		public byte StageSizeZ;

		/// <summary>
		/// Play field height in levels. May be larger than the initial ball layout in <see cref="Balls"/>,
		/// leaving empty levels at the bottom for the structure to grow into.
		/// Zero in legacy files, which carried only the layout array.
		/// </summary>
		[JsonProperty("l")]
		public byte Levels;

		[JsonProperty("b")]
		public BallPositionType[,,] Balls;
    }
}