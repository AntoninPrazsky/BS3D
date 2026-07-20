using Microsoft.Xna.Framework;
using Prazsky.Render;

namespace Prazsky.BS3D.GameStructure
{
    public static class BasicEffectParamsProvider
    {
        //Neutral (white-ish) specular with a tight power reads as glossy plastic: each ball gets a distinct
        //highlight whose position depends on where the ball sits relative to the light and the camera.
        //(The old strongly coloured specular with power 0.5 was just a broad tinted sheen, no visible highlight.)
        //The highlight is kept tight and bright on purpose: the tighter it is, the more of the moulded
        //micro-relief of the skin (PatternReliefStrength) it picks out instead of reading as smooth vinyl.
        private static readonly Vector3 GLOSS_COLOR = new(0.6f, 0.6f, 0.6f);
        private const float GLOSS_POWER = 40f;

        public static BasicEffectParams ColorRed = new BasicEffectParams(new Vector3(0.3f, 0f, 0f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorGreen = new BasicEffectParams(new Vector3(0f, 0.3f, 0f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorBlue = new BasicEffectParams(new Vector3(0f, 0f, 0.3f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorWhite = new BasicEffectParams(new Vector3(0.1f, 0.1f, 0.1f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);

        /// <summary>
        /// Multiplier applied to the ball model's material diffuse colours to give the ball its type colour.
        /// (Historically the type colour came from a broad coloured specular sheen; with a proper glossy
        /// highlight the type has to tint the diffuse patches instead.)
        /// </summary>
        public static Vector3 GetDiffuseTintByType(BallType ballType)
        {
            switch (ballType)
            {
                case BallType.Type1:
                    return new Vector3(1f, 0.2f, 0.2f);

                case BallType.Type2:
                    return new Vector3(0.25f, 1f, 0.25f);

                case BallType.Type3:
                    return new Vector3(0.35f, 0.45f, 1f);

                case BallType.Type4:
                    //Light beige rather than pure white: the beach-ball gores alternate with white,
                    //so a white primary would leave the ball patternless (white on white)
                    return new Vector3(0.85f, 0.78f, 0.62f);

                default:
                    return Vector3.One;
            }
        }

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