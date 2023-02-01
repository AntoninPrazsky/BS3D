using Microsoft.Xna.Framework;
using Prazsky.Render;

namespace Prazsky.BS3D.GameStructure
{
    public static class BasicEffectParamsProvider
    {
        public static BasicEffectParams ColorRed = new BasicEffectParams(new Vector3(0.3f, 0f, 0f), new Vector3(0.8f, 0f, 0f), 0.5f, Vector3.Zero);
        public static BasicEffectParams ColorGreen = new BasicEffectParams(new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0.8f, 0f), 0.5f, Vector3.Zero);
        public static BasicEffectParams ColorBlue = new BasicEffectParams(new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, 0.8f), 0.5f, Vector3.Zero);
        public static BasicEffectParams ColorWhite = new BasicEffectParams(new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0.3f, 0.3f, 0.3f), 0.3f, Vector3.Zero);

        public static BasicEffectParams GetEffectByType(BallType ballType)
        {
            switch (ballType)
            {
                case BallType.Type1:
                    return ColorRed;

                case BallType.Type2:
                    return ColorGreen;

                case BallType.Type3:
                    return ColorBlue;

                case BallType.Type4:
                    return ColorWhite;

                default:
                    return null;
            }
        }
    }
}