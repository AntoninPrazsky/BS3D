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
        //Blue is the one primary whose ambient is not a pure channel: it carries a little R and G (#246, with
        //its tint) because it was lifted to let Type12 out of the near-black band, and an ambient of pure blue
        //under a lighter tint left the shaded half darker and more saturated than the lit half - see ColorNavy
        //below for why the unlit side is what decides this.
        public static BasicEffectParams ColorBlue = new BasicEffectParams(new Vector3(0.02f, 0.05f, 0.32f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorWhite = new BasicEffectParams(new Vector3(0.1f, 0.1f, 0.1f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);

        //The second four types: the CMY complements of red/green/blue, plus black. The ambient here is a dark
        //version of the colour (like the primaries above); the diffuse tint that actually colours the ball is
        //in GetDiffuseTintByType. Black's ambient is near-nothing, so it stays dark under every dome - its
        //form comes from the glossy highlight and its white gores, not from any colour it reflects.
        public static BasicEffectParams ColorCyan = new BasicEffectParams(new Vector3(0f, 0.3f, 0.3f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorMagenta = new BasicEffectParams(new Vector3(0.3f, 0f, 0.3f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorYellow = new BasicEffectParams(new Vector3(0.3f, 0.3f, 0f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorBlack = new BasicEffectParams(new Vector3(0.02f, 0.02f, 0.02f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);

        //The five #152 types, ambient again a dark version of each colour. The darker the ball (navy, olive,
        //brown), the smaller the ambient - like black's above, their form leans on the highlight and the gores.
        public static BasicEffectParams ColorOrange = new BasicEffectParams(new Vector3(0.3f, 0.15f, 0f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorBrown = new BasicEffectParams(new Vector3(0.15f, 0.08f, 0.03f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorSilver = new BasicEffectParams(new Vector3(0.12f, 0.13f, 0.15f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        //Navy's ambient moves WITH its tint, and Type3's did too (#246, both here and in
        //GetDiffuseTintByType): the ambient is what the ball's UNLIT side has, so a brighter tint over the old
        //(0.02, 0.04, 0.2) would have left the shaded half still reading as the black ball's silhouette - the
        //very confusion the tint was raised to end, surviving on the half of the ball facing away from the
        //light. Same shape as the tint, a dark version of the colour, R and G equal.
        public static BasicEffectParams ColorNavy = new BasicEffectParams(new Vector3(0.06f, 0.06f, 0.24f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);
        public static BasicEffectParams ColorOlive = new BasicEffectParams(new Vector3(0.12f, 0.13f, 0.02f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);

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
                    //Lighter than the (0.35, 0.45, 1.0) it shipped with, and NOT for its own sake (#246):
                    //Type12 had to come up out of the near-black band it shared with Type8, and lifting navy
                    //on its own just walks it into this blue. So the two moved together and the whole blue
                    //family re-spaced - black, navy, blue - which is what made both pairs come out better
                    //instead of one at the other's cost. The figures are on Type12.
                    //
                    //Room to go up existed here and nowhere else: silver measures 115 and cyan 165 on that
                    //capture against this blue's 103, and silver/blue is tight (dE00 17) because the two sit
                    //at nearly the SAME lightness - so lifting blue moves it away from silver as well. Kept
                    //cyan-leaning (G over R) so navy's equal R and G reads as the deeper, more violet blue
                    //of the pair rather than merely the darker one.
                    return new Vector3(0.45f, 0.6f, 1f);

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

                case BallType.Type9:
                    //The green channel sits halfway between red's 0.2 and gold's 0.82, so the orange
                    //splits the two colours it lives between instead of drifting into either
                    return new Vector3(1f, 0.5f, 0.03f);    //orange

                case BallType.Type10:
                    return new Vector3(0.42f, 0.24f, 0.11f); //brown

                case BallType.Type11:
                    //A cool slate rather than a light silver: the gore trap again (see Type4), and the
                    //blue cast is what separates it from the warm beige the white ball is drawn as
                    return new Vector3(0.5f, 0.53f, 0.58f);  //silver

                case BallType.Type12:
                    //Darker than Type3's blue, but lifted OUT OF THE NEAR-BLACK BAND it used to share with
                    //Type8 (#246). It was (0.05, 0.10, 0.45), whose whole difference from black sat in the
                    //BLUE channel - the one the eye weights least (0.072 of luminance against green's 0.715)
                    //- and measured off a capture under a dark dome that put the two balls at luminance 31
                    //and 13 of 255, both at the very bottom of the display's range. Two things follow, and
                    //the second is the one the issue is actually about:
                    //
                    //  - at that level a lit room takes the difference away before the eye gets it;
                    //  - the cannon's pane MULTIPLIES what is behind it (CannonRig's GLASS_ALPHA 0.62 keeps
                    //    about 0.38), so navy behind the glass came out at ~12 - which is what black reads
                    //    in the OPEN. A glazed navy round and an unglazed black one were the same colour,
                    //    and the magazine is exactly where the issue says it cannot tell them apart.
                    //
                    //This is 240 degrees exactly - R and G equal - which is the blue-violet a "navy" already
                    //was, only bright enough to be one. It STAYS a blue on purpose, because the level designs
                    //use it as one: Globe's ocean is 92 navy against 108 cyan and 57 blue, and Wishbone's
                    //bulb sits next to 83 blue. The palette's one free hue is the violet gap (240-300 is the
                    //only one) and moving it there was tried and MEASURED WORSE on both pairs at once - and
                    //it would have put a violet band through a pixel Earth, whose "ocean" is written down in
                    //Tools/LevelGen.
                    //
                    //Lifting this alone is not the fix, and that is the part worth keeping: it walks navy
                    //straight into Type3. Measured in CIEDE2000 off the same capture, navy at (0.08, 0.22,
                    //0.62) took black/navy from 25.3 to 30.8 and pushed navy/blue from 24.4 down to 16.7 -
                    //the palette's tightest pair, one confusion traded for another. Type3 had to come up
                    //with it, and once it did BOTH pairs improved (30.8 and 33.4) with no other pair in the
                    //palette meaningfully tighter. See Type3.
                    return new Vector3(0.16f, 0.16f, 0.62f);  //navy blue

                case BallType.Type13:
                    //Dark and yellow-leaning against Type2's vivid green, saturated so the gores
                    //do not wash out (the same reasoning as the gold above)
                    return new Vector3(0.42f, 0.45f, 0.08f); //olive green

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

                case BallType.Type9:
                    return ColorOrange;

                case BallType.Type10:
                    return ColorBrown;

                case BallType.Type11:
                    return ColorSilver;

                case BallType.Type12:
                    return ColorNavy;

                case BallType.Type13:
                    return ColorOlive;

                default:
                    return null;
            }
        }
    }
}