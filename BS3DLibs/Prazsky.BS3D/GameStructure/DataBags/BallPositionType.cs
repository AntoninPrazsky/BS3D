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
		[JsonProperty("b")]
		public BallPositionType[,,] Balls;
    }
}