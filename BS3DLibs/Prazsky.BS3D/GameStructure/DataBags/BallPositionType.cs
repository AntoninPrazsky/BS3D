using System;
using System.Runtime.Serialization;

namespace Prazsky.BS3D.GameStructure.DataBags
{
	[Serializable]
	public class BallPositionType
	{
		[DataMember]
		public float PositionX;
		[DataMember]
		public float PositionY;
		[DataMember]
		public float PositionZ;
		[DataMember]
		public eBallType Type;
	}

	[Serializable]
	public class BallPositionTypes
	{
		[DataMember]
		public BallPositionType[,,] Balls;
	}
}