using Microsoft.Xna.Framework;
using Prazsky.Core.Render;

namespace Prazsky.BS3D.GameStructure
{
    public static class BasicEffectParamsProvider
    {
        //Neutral (white-ish) specular with a tight power reads as glossy plastic: each ball gets a distinct
        //highlight whose position depends on where the ball sits relative to the light and the camera.
        //(The old strongly colored specular with power 0.5 was just a broad tinted sheen, no visible highlight.)
        //The highlight is kept tight and bright on purpose: the tighter it is, the more of the molded
        //micro-relief of the skin (PatternReliefStrength) it picks out instead of reading as smooth vinyl.
        private static readonly Vector3 GLOSS_COLOR = new(0.6f, 0.6f, 0.6f);
        private const float GLOSS_POWER = 40f;

        public static BasicEffectParams ColorRed = new BasicEffectParams(new Vector3(0.3f, 0f, 0f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorGreen = new BasicEffectParams(new Vector3(0f, 0.3f, 0f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorBlue = new BasicEffectParams(new Vector3(0f, 0f, 0.3f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorWhite = new BasicEffectParams(new Vector3(0.1f, 0.1f, 0.1f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);

        //The second four types: the CMY complements of red/green/blue, plus black. The ambient here is a dark
        //version of the colour (like the primaries above); the diffuse tint that actually colours the ball is
        //in GetDiffuseTintByType. Black's ambient is near-nothing, so it stays dark under every dome - its
        //form comes from the glossy highlight and its white gores, not from any colour it reflects.
        public static BasicEffectParams ColorCyan = new BasicEffectParams(new Vector3(0f, 0.3f, 0.3f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorMagenta = new BasicEffectParams(new Vector3(0.3f, 0f, 0.3f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorYellow = new BasicEffectParams(new Vector3(0.3f, 0.3f, 0f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorBlack = new BasicEffectParams(new Vector3(0.02f, 0.02f, 0.02f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);

        /// <summary>
        /// Multiplier applied to the ball model's material diffuse colors to give the ball its type color.
        /// (Historically the type color came from a broad colored specular sheen; with a proper glossy
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

                case BallType.Type5:
                    return new Vector3(0.15f, 1f, 1f);      //cyan

                case BallType.Type6:
                    return new Vector3(1f, 0.15f, 0.9f);    //magenta

                case BallType.Type7:
                    //A rich gold-yellow, not a pale one: the gores alternate with white, so a light
                    //yellow would wash out against them the way a white primary does
                    return new Vector3(1f, 0.82f, 0.1f);    //yellow

                case BallType.Type8:
                    //Near-black: the coloured gores go almost to black against the white ones (an
                    //8-ball look). The glossy highlight and sky sheen still give it shape, and its
                    //emission is near-nothing, so it is the one ball that stays dark while the rest pulse.
                    return new Vector3(0.045f, 0.045f, 0.05f); //black

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

                case BallType.Type5:
                    return ColorCyan;

                case BallType.Type6:
                    return ColorMagenta;

                case BallType.Type7:
                    return ColorYellow;

                case BallType.Type8:
                    return ColorBlack;

                default:
                    return null;
            }
        }
    }
}