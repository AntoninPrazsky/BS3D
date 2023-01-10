using System;

namespace Prazsky.BS3D.GameStructure.DataBags
{
    [Serializable]
    public class BallPositionType
    {
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public eBallType Type;
    }

    [Serializable]
    public class BallPositionTypes
    {
        public BallPositionType[,,] Balls;
    }
}