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
        //Olive's ambient moved with its tint too (#294, the same rule ColorNavy states above): the shaded
        //half is where the old dark-band confusion would otherwise have survived. Its blue is now near-nothing
        //rather than 0.02, which is the chroma half of the change - a desaturated shadow is what made the ball
        //read as a dark neutral from the side away from the light.
        public static BasicEffectParams ColorOlive = new BasicEffectParams(new Vector3(0.12f, 0.15f, 0.006f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);

        /// <summary>
        /// The rock's material (#324) — <b>not a fourteenth colour</b>. It is not returned by
        /// <see cref="GetEffectByType"/> and nothing indexes it by <see cref="BallType"/>: a rock is a
        /// <see cref="BallKind"/>, and <c>BallRenderSet.DrawRocks</c> is its one reader.
        /// <para>
        /// It is here rather than as constants beside the stone shading because this is where a ball's
        /// <i>material</i> lives, and because leaving it out is what the first build of the rock did — the draw
        /// passed <c>null</c> and the ball fell back on <c>DefaultLighting</c>'s ambient, which is a dim BLUE
        /// (0.053, 0.099, 0.182) meant for the scene at large. Seen from the island the player looks UP at the
        /// cluster, which is its unlit side, where the ambient is the only light there is — so the rocks came
        /// out near-black and faintly blue, next to the 8-ball rather than next to the stone they are.
        /// </para>
        /// <para>
        /// The ambient is warm and <b>the strongest in this file</b>, at roughly twice the silver's. That is
        /// not a thumb on the scale: it is the floor of the one ball here that radiates almost none of its own
        /// light, so its unlit side has to come from somewhere, and skylight on rock is where a real one gets
        /// it. Warm for the same reason the body tint is — Type11 is a cool slate and is the one type a grey
        /// ball can be taken for.
        /// </para>
        /// </summary>
        public static BasicEffectParams Stone = new BasicEffectParams(new Vector3(0.26f, 0.245f, 0.215f), GLOSS_COLOR, GLOSS_POWER, Vector3.Zero);

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
                    //Lifted out of the dark band and given back its chroma (#294). It was (0.42, 0.45, 0.08),
                    //which read as a dark drab rather than a green: measured in CIEDE2000 off a
                    //Thirteen_Colors capture, its three nearest neighbours were BLACK (24.8), brown (26.0) and
                    //silver (26.0) under dome 1 - a dark-and-neutral band, none of them a green. Two things
                    //were wrong at once and both are in the figures above: too little light, and 0.08 of blue
                    //desaturating a colour whose whole identity is that it has none.
                    //
                    //After: black 29.6, brown 32.6, silver 27.6 under dome 1; black 35.2, brown 36.3, silver
                    //29.0 under dome 13. What closes instead are the pairs separated by LIGHTNESS rather than
                    //hue - beige/olive 24.0 -> 22.4 and green/olive 32.9 -> 26.0, both under dome 13 - and
                    //both stay well clear of the palette's own tightest pairs, red/orange (14.2) and
                    //white/yellow (14.5), which nobody has ever complained about.
                    //
                    //THIS IS THE STOPPING POINT, and it was measured rather than judged: at (0.44, 0.56,
                    //0.025) the dark band opens further still (black 37.9, brown 39.2 under dome 13) but
                    //green/olive falls to 22.5 - two greens a player has to tell apart, which is #246's "one
                    //confusion traded for another" arriving from the other side. Measure any further move
                    //under BOTH a bright dome and a dark one: olive rides the dome's own light harder than
                    //its neighbours do, so dome 1 and dome 13 disagree about which pair is the tightest.
                    //
                    //It stays a GREEN because the level designs use it as one - Globe's land is green and
                    //olive together, and MOSS is white, green, olive and black.
                    return new Vector3(0.42f, 0.52f, 0.02f); //olive green

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