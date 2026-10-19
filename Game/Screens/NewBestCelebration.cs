using Microsoft.Xna.Framework;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>How the result page celebrates a new best</b> (#926), as pure functions of the page's clock so the tests hold them without a
    /// window (<c>NewBestCelebrationTests</c>) and <see cref="NewBestBanner"/> only draws them. The owner's playtest of 2026-10-18:
    /// the "New best" line was set like every other line of the breakdown, and "should be celebrated somehow, e.g. by enlarging or
    /// animating the type; a pastel-rainbow gradient could show as the fill". His standing complaint, written under the verdict of
    /// the neighbouring feature, is that celebrations are built discreetly and he has to write "enlarge it, multiply, emphasise". So
    /// this ships at the loud end and he scales it down.
    /// <list type="bullet">
    /// <item><description><b>It grows in with a bounce</b> (<see cref="Scale"/>): an under-damped spring from <see cref="BIRTH_FROM"/> of
    /// its size through a <b>1.4 times overshoot</b> to rest, in about 0.6 s, so the word is struck onto the page and not faded in. At
    /// rest it is <see cref="NewBestBanner.FONT_DESIGN"/>, nearly twice the breakdown's type, and it stays that size.</description></item>
    /// <item><description><b>Its letters are filled with a pastel rainbow</b> (<see cref="Fill"/>) that flows slowly through the word
    /// (<see cref="FLOW_CYCLES_PER_SECOND"/>) with a pale gloss sweeping across it every <see cref="GLOSS_PERIOD"/> seconds: a gradient
    /// of the whole word rather than a colour a letter, so each letter is itself shaded across its width.</description></item>
    /// <item><description><b>It throws sparkles</b> (<see cref="Burst"/>) the moment it lands, and keeps glinting
    /// (<see cref="Glint"/>) for as long as the page is up.</description></item>
    /// </list>
    /// Sparkles are placed in units of the word's own height, so they follow it at any resolution.
    /// </summary>
    internal static class NewBestCelebration
    {
        /// <summary>
        /// The gap, in seconds, between the last earned star settling and the word striking. Short enough to read as the rating's
        /// last beat and long enough that the star's chime has rung before the word's own begins.
        /// </summary>
        public const float STAR_GAP = 0.1f;

        /// <summary>Where the word starts from, as a share of its size: near nothing, so it bursts in.</summary>
        public const float BIRTH_FROM = 0.1f;

        /// <summary>The spring's damping ratio: 0.28 overshoots a little over 40 % of the way travelled and rings once more, softly.</summary>
        public const float DAMPING = 0.28f;

        /// <summary>The spring's natural frequency, in hertz: the first peak lands about 0.2 s in.</summary>
        public const float SPRING_HERTZ = 2.6f;

        /// <summary>How long the word takes to fade in at the start of the spring, so its first frame is not a hard-edged speck.</summary>
        public const float FADE_IN = 0.07f;

        /// <summary>How fast the rainbow flows through the word, in whole rainbows a second: one every ten seconds, slow.</summary>
        public const float FLOW_CYCLES_PER_SECOND = 0.1f;

        /// <summary>How much of the rainbow the word spans, left to right: one whole turn of the hue.</summary>
        public const float SPAN = 1f;

        /// <summary>How far the rainbow tilts down the word's height, in turns: a fifth of one, so the letters are shaded top to bottom too.</summary>
        public const float TILT = 0.2f;

        /// <summary>The darkest any channel of the fill gets: the rainbow is lifted this far towards white, which is what makes it pastel.</summary>
        public const float LIFT = 0.52f;

        /// <summary>The time between two sweeps of the gloss, in seconds.</summary>
        public const float GLOSS_PERIOD = 3.2f;

        /// <summary>How long a sweep of the gloss takes to cross the word.</summary>
        public const float GLOSS_SWEEP = 1.1f;

        /// <summary>How much of the way to white the gloss takes the fill at its brightest.</summary>
        public const float GLOSS_STRENGTH = 0.6f;

        /// <summary>How many sparkles the arrival throws.</summary>
        public const int BURST_COUNT = 24;

        /// <summary>The longest any of the burst's sparkles lives, in seconds, delay included: the page may stop thinking about them after it.</summary>
        public const float BURST_END = 2.0f;

        /// <summary>How many glints twinkle on the word once the burst is over.</summary>
        public const int GLINT_COUNT = 8;

        /// <summary>How long one glint lasts, in seconds.</summary>
        public const float GLINT_LIFE = 0.6f;

        /// <summary>When the glints begin, after the arrival: the burst has the word to itself first.</summary>
        public const float GLINT_FROM = 0.9f;

        /// <summary>
        /// When the word strikes, in seconds since the page opened: right after the last <i>earned</i> star settles, so the rating
        /// is read first and the new best is its last beat. <paramref name="lastStarLandsAt"/> is when that star lands and
        /// <paramref name="punchSeconds"/> how long it takes to seat.
        /// </summary>
        public static float Arrival(float lastStarLandsAt, float punchSeconds) =>
            lastStarLandsAt + punchSeconds * 0.55f + STAR_GAP;

        /// <summary>
        /// The word's size at <paramref name="age"/> seconds after it struck, as a multiple of its resting size: zero before it strikes
        /// and one a little after the spring has rung out.
        /// </summary>
        public static float Scale(float age)
        {
            if (age < 0f) return 0f;

            //The step response of an under-damped spring: 0 at the start, up through 1 to its first peak, back under it, and still
            float omega = MathF.Tau * SPRING_HERTZ;
            float decay = DAMPING * omega;
            float damped = omega * MathF.Sqrt(1f - DAMPING * DAMPING);
            float response = 1f - MathF.Exp(-decay * age) * (MathF.Cos(damped * age) + DAMPING / MathF.Sqrt(1f - DAMPING * DAMPING) * MathF.Sin(damped * age));

            return BIRTH_FROM + (1f - BIRTH_FROM) * response;
        }

        /// <summary>The word's opacity at <paramref name="age"/> (<see cref="Scale"/>'s own clock): a quick fade-in, then solid.</summary>
        public static float Alpha(float age) => age <= 0f ? 0f : MathF.Min(1f, age / FADE_IN);

        /// <summary>
        /// The pastel rainbow at a point of the word: <paramref name="across"/> is how far along its width (0 at the left of the first letter, 1
        /// at the right of the last), <paramref name="down"/> how far down its height, <paramref name="clock"/> the page's seconds. Returns the
        /// colour as red, green and blue in 0 to 1, <b>not</b> premultiplied.
        /// <para>
        /// Three raised cosines a third of a turn apart, which sum to a constant, so the lightness never swings as the hue goes round:
        /// each channel runs from <see cref="LIFT"/> to 1, and a colour is therefore always at least that far towards white. The gloss
        /// is a bright band tilted across the word, lerping the colour towards white.
        /// </para>
        /// </summary>
        public static Vector3 Fill(float across, float down, float clock)
        {
            float turn = across * SPAN + down * TILT - clock * FLOW_CYCLES_PER_SECOND;

            Vector3 rainbow = Pastel(turn);

            //The gloss: a soft band, tilted by the height, that crosses the word and then rests until its period comes round
            float into = clock % GLOSS_PERIOD / GLOSS_SWEEP;
            float centre = -0.35f + into * 1.7f;
            float distance = (across + down * 0.35f - centre) / 0.11f;
            float gloss = into <= 1f ? MathF.Exp(-distance * distance) * GLOSS_STRENGTH : 0f;

            return Vector3.Lerp(rainbow, Vector3.One, gloss);
        }

        /// <summary>One whole rainbow in the turn from 0 to 1 and round again: red at 0, then yellow, green, cyan, blue and violet, all lifted.</summary>
        public static Vector3 Pastel(float turn) => new(
            Channel(turn, 0f),
            Channel(turn, 1f / 3f),
            Channel(turn, 2f / 3f));

        private static float Channel(float turn, float shift) =>
            LIFT + (1f - LIFT) * (0.5f + 0.5f * MathF.Cos(MathF.Tau * (turn - shift)));

        /// <summary>One sparkle: where it is, how big, how bright and how turned, all in units of the word's height.</summary>
        /// <param name="X">Across, from the word's centre, in heights.</param>
        /// <param name="Y">Down, from the word's centre, in heights.</param>
        /// <param name="Size">How wide it is, in heights.</param>
        /// <param name="Spin">How far turned, in radians.</param>
        /// <param name="Strength">How bright, 0 for not there and up to 1.</param>
        internal readonly record struct Sparkle(float X, float Y, float Size, float Spin, float Strength);

        /// <summary>
        /// The <paramref name="index"/>th sparkle of the arrival's burst, <paramref name="age"/> seconds after the word struck (a
        /// <see cref="Sparkle.Strength"/> of zero when it is not there). It starts somewhere on the word, flies <b>out</b> from its middle with
        /// the speed it was thrown at, easing as it goes and sagging a little, and brightens at once and dies away. Each is drawn from its own
        /// index, so a frame holds no state. <paramref name="aspect"/> is the word's width over its height as it is laid out, which spreads
        /// the sparkles along it.
        /// </summary>
        public static Sparkle Burst(int index, float age, float aspect)
        {
            uint seed = 0x9E3779B1u * (uint)(index + 1);
            float delay = Unit(seed) * 0.2f;
            float life = 0.7f + Unit(seed + 1) * 0.75f;
            float local = age - delay;

            if (local < 0f || local > life) return default;

            float x = (Unit(seed + 2) - 0.5f) * aspect * 0.95f;
            float y = (Unit(seed + 3) - 0.5f) * 0.7f;

            //Out from the word's middle along the way it already points from it, the height stretched so a sparkle near the middle goes up
            //or down and one near an end goes sideways
            float length = MathF.Sqrt(x * x + 9f * y * y) + 1e-3f;
            float speed = 1.2f + Unit(seed + 4) * 2.6f;
            float travelled = speed * (1f - MathF.Exp(-local * 2.4f)) / 2.4f;

            float share = local / life;
            float pop = MathF.Min(1f, local / 0.06f) * MathF.Pow(1f - share, 1.2f);
            float size = (0.6f + Unit(seed + 5) * 0.75f) * (0.55f + 0.45f * (1f - share));

            return new Sparkle(
                x + x / length * travelled,
                y + 3f * y / length * travelled + 0.4f * local * local,
                size,
                Unit(seed + 6) * MathF.Tau + local * (Unit(seed + 7) - 0.5f) * 5f,
                pop);
        }

        /// <summary>
        /// The <paramref name="index"/>th of the glints that twinkle on the word after the burst (<see cref="GLINT_FROM"/>), <paramref name="age"/>
        /// seconds after it struck: each in its own rhythm, a new place on the word every time round, up and back down in
        /// <see cref="GLINT_LIFE"/>. Nothing about one depends on the others or on a frame before it. <paramref name="aspect"/> as for <see cref="Burst"/>.
        /// </summary>
        public static Sparkle Glint(int index, float age, float aspect)
        {
            float t = age - GLINT_FROM;
            if (t < 0f) return default;

            float period = 1.05f + 0.19f * index;
            float cycle = t / period + Unit(0xA511E9B3u * (uint)(index + 1)) * 7f;
            float whole = MathF.Floor(cycle);
            float local = (cycle - whole) * period;

            if (local > GLINT_LIFE) return default;

            uint seed = 0x68E31DA4u * (uint)(index + 1) + (uint)whole * 0x1B873593u;
            float share = local / GLINT_LIFE;

            return new Sparkle(
                (Unit(seed) - 0.5f) * aspect * 0.9f,
                (Unit(seed + 1) - 0.5f) * 0.62f,
                0.5f + Unit(seed + 2) * 0.45f,
                Unit(seed + 3) * MathF.Tau * 0.25f,
                MathF.Pow(MathF.Sin(MathF.PI * share), 1.5f));
        }

        /// <summary>A hash of <paramref name="seed"/> to the unit interval: the sparkles are a function of their index and the clock, and have no random state.</summary>
        internal static float Unit(uint seed)
        {
            seed ^= seed >> 16;
            seed *= 0x7FEB352Du;
            seed ^= seed >> 15;
            seed *= 0x846CA68Bu;
            seed ^= seed >> 16;
            return (seed & 0xFFFFFFu) / (float)0x1000000;
        }
    }
}
