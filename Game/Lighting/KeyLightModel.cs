using BS3D.Screens;
using Microsoft.Xna.Framework;
using System;

namespace BS3D.Lighting
{
    /// <summary>The keys the game lights by name (#987): the ones play is driven with, and the four the queue is shown on.</summary>
    internal enum LitKey { Space, E, Q, R, W, A, S, D, F1, F2, F3, F4 }

    /// <summary>What the keyboard is showing: the menus, the level being played, or the ending on the result page.</summary>
    internal enum KeyLightMode { Idle, Playing, Won, Lost }

    /// <summary>
    /// What one frame asks the keyboard to show (#987), read off the game by <c>BS3DGame.UpdateKeyboardLights</c>. Colours are authored
    /// sRGB, 0 to 1, as the game's own tints are (<c>GameplayScreen.LoadedTint</c>).
    /// </summary>
    internal struct KeyLightInput
    {
        public KeyLightMode Mode;

        /// <summary>The wall clock, in seconds, for what blinks and drifts.</summary>
        public float Clock;

        /// <summary>Seconds since <see cref="Mode"/> began, for what plays once (the ending's rainbow).</summary>
        public float ModeSeconds;

        /// <summary>The round in the bore, while playing. With <see cref="HasLoaded"/> false (no balls left) its keys go dark.</summary>
        public Vector3 Loaded;

        public bool HasLoaded;

        /// <summary>How many of the queue's colours (the caller's array, next first) are real: fewer near the end of the budget.</summary>
        public int QueueCount;

        /// <summary>The cup the ending earned, 1 (Bronze) to <see cref="KeyLightModel.CUPS"/> (Diamond), when <see cref="Mode"/> is Won.</summary>
        public int Stars;

        /// <summary>The keys a tutorial card is asking for, a bit per <see cref="LitKey"/>: they blink white over the rest.</summary>
        public int TutorialKeys;
    }

    /// <summary>
    /// One keyboard's lamps as the model needs them (#987): how many, where each sits across the board (0 at the left edge, 1 at the
    /// right), and which lamps each <see cref="LitKey"/> is. Built once for a device by the platform backend from what the device reports
    /// (<c>Platform/KeyboardLights</c>), and by the tests by hand.
    /// </summary>
    internal sealed class KeyLightLayout
    {
        public readonly int LampCount;

        /// <summary>Each lamp's place across the board, 0 to 1 left to right.</summary>
        public readonly float[] X;

        /// <summary>The lamps of each <see cref="LitKey"/>, indexed by it; empty for a key the device does not have.</summary>
        public readonly int[][] KeyLamps;

        public KeyLightLayout(float[] x, int[][] keyLamps)
        {
            if (keyLamps.Length != KeyLightModel.KEY_COUNT) throw new ArgumentException($"{KeyLightModel.KEY_COUNT} keys expected", nameof(keyLamps));

            LampCount = x.Length;
            X = x;
            KeyLamps = keyLamps;
        }
    }

    /// <summary>
    /// <b>What the player's backlit keyboard shows</b> (#987), as a pure function of the game's state, so the tests hold it without a
    /// device (<c>KeyLightModelTests</c>) and the backend only sends it. The owner's ask (2026-10-22): the colour of the ball about to
    /// be fired, the balls coming after it, a rainbow and the cup's colour at a clear and red at a loss, and the keys a tutorial card
    /// asks for blinking white.
    /// <list type="bullet">
    /// <item><description><b>Playing</b>: the whole board in the loaded ball's colour at <see cref="PLAY_BASE"/>, Space (which fires) at
    /// full, and F1 to F4 the next four balls of the magazine at full, F1 next, so the queue reads left to right as the strip on screen
    /// does. A wildcard's keys cycle with it and a cutter's take the Cut's accent, because the tint is the one the muzzle collar and the
    /// aim beam are given.</description></item>
    /// <item><description><b>A tutorial card</b> that asks for a key makes it blink white at <see cref="BLINK_HERTZ"/>.</description></item>
    /// <item><description><b>Won</b>: a full-brightness rainbow sweeps across the board for <see cref="RAINBOW_SECONDS"/>, then the
    /// board settles into the cup's colour (<see cref="CupColour"/>), breathing, with a Diamond's keys twinkling white. At the loud
    /// end on purpose: the owner's standing ruling on celebrations.</description></item>
    /// <item><description><b>Lost</b>: red, pulsing slowly, whether the cluster reached the line or the balls ran out.</description></item>
    /// <item><description><b>Idle</b> (the menus, a pause): a dim rainbow drifting slowly across the board, so the game still owns the
    /// keyboard it took without shouting over a menu.</description></item>
    /// </list>
    /// </summary>
    internal static class KeyLightModel
    {
        public const int KEY_COUNT = 12;

        /// <summary>The cups, Bronze to Diamond: <c>TrophyPodium.TIERS</c>.</summary>
        public const int CUPS = 4;

        /// <summary>The rest of the board while playing, as a share of the loaded colour: Space and the queue stand out above it.</summary>
        public const float PLAY_BASE = 0.4f;

        /// <summary>How often a key a card asks for blinks, on and off, in hertz.</summary>
        public const float BLINK_HERTZ = 2f;

        /// <summary>How long the clear's rainbow sweeps before the cup's colour takes over, in seconds.</summary>
        public const float RAINBOW_SECONDS = 3f;

        /// <summary>How long the rainbow takes to hand over to the cup's colour, in seconds.</summary>
        public const float CUP_FADE_SECONDS = 0.6f;

        /// <summary>How fast the rainbow runs across the board, in boards a second, and how much of the hue one board spans.</summary>
        public const float RAINBOW_SPEED = 0.9f, RAINBOW_SPAN = 1f;

        /// <summary>The cup's breathing: its period in seconds and the darkest it gets, as a share of full.</summary>
        public const float CUP_BREATH_SECONDS = 2f, CUP_BREATH_LOW = 0.7f;

        /// <summary>A Diamond's twinkles: how many times a second they move on, and the share of keys lit at once.</summary>
        public const float TWINKLE_HERTZ = 6f, TWINKLE_SHARE = 0.1f;

        /// <summary>The loss's red and its pulse: the period in seconds and the darkest it gets.</summary>
        public static readonly Vector3 LOSS_RED = new(1f, 0.04f, 0.02f);
        public const float LOSS_PULSE_SECONDS = 1.25f, LOSS_LOW = 0.35f;

        /// <summary>The menus' drifting rainbow: its brightness, its saturation, and how fast it drifts in turns of the hue a second.</summary>
        public const float IDLE_VALUE = 0.22f, IDLE_SATURATION = 0.75f, IDLE_DRIFT = 0.02f;

        /// <summary>
        /// The four cups' colours on a keyboard, Bronze to Diamond. Not the podium's metals (<c>TrophyPodium</c>'s finishes): an LED has
        /// no specular, and a silver and a diamond read as the same white, so each is pushed to a hue of its own that still names its metal.
        /// </summary>
        private static readonly Vector3[] CUP_COLOURS =
        {
            new(1f, 0.42f, 0.1f),   //Bronze: a warm orange
            new(0.7f, 0.78f, 0.9f), //Silver: a cool white
            new(1f, 0.72f, 0.08f),  //Gold
            new(0.45f, 0.85f, 1f),  //Diamond: an icy blue, and twinkles on top
        };

        /// <summary>The colour of the cup <paramref name="stars"/> earns, clamped to Bronze to Diamond.</summary>
        public static Vector3 CupColour(int stars) => CUP_COLOURS[Math.Clamp(stars, 1, CUPS) - 1];

        /// <summary>
        /// The keys a tutorial card asks for on a keyboard, as <see cref="KeyLightInput.TutorialKeys"/>' bits: what the card's own
        /// glyph and words name for the keyboard and mouse (<c>Tutorial</c>'s definitions), at its <paramref name="step"/> - the
        /// lean-and-walk card asks for the right button first and A and D second. Fire's card shows the mouse and says "Space fires
        /// too", so Space blinks. A card about the mouse, the rules or a ball's kind lights nothing.
        /// </summary>
        public static int KeysFor(Tutorial.Lesson? lesson, int step) => lesson switch
        {
            Tutorial.Lesson.Fire => Bit(LitKey.Space),
            Tutorial.Lesson.Traverse => Bit(LitKey.A) | Bit(LitKey.D),
            Tutorial.Lesson.Walk => Bit(LitKey.W) | Bit(LitKey.S),
            Tutorial.Lesson.Combine when step > 0 => Bit(LitKey.A) | Bit(LitKey.D),
            Tutorial.Lesson.Swap => Bit(LitKey.E),
            Tutorial.Lesson.Brake => Bit(LitKey.Q),
            Tutorial.Lesson.Cut => Bit(LitKey.R),
            _ => 0,
        };

        public static int Bit(LitKey key) => 1 << (int)key;

        /// <summary>
        /// Fills <paramref name="lamps"/> (one colour a lamp of <paramref name="layout"/>, authored sRGB 0 to 1) with what
        /// <paramref name="input"/> asks for. <paramref name="queue"/> holds the magazine's next balls, next first. Allocates nothing.
        /// </summary>
        public static void Compose(in KeyLightInput input, Vector3[] queue, KeyLightLayout layout, Vector3[] lamps)
        {
            int count = Math.Min(layout.LampCount, lamps.Length);

            switch (input.Mode)
            {
                case KeyLightMode.Playing:
                    Vector3 loaded = input.HasLoaded ? input.Loaded : Vector3.Zero;
                    Fill(lamps, count, loaded * PLAY_BASE);
                    Paint(layout, LitKey.Space, loaded, lamps);
                    for (int i = 0; i < 4; i++)
                        Paint(layout, LitKey.F1 + i, i < input.QueueCount && i < queue.Length ? queue[i] : Vector3.Zero, lamps);
                    break;

                case KeyLightMode.Won:
                    ComposeWon(input, layout, lamps, count);
                    break;

                case KeyLightMode.Lost:
                    float pulse = LOSS_LOW + (1f - LOSS_LOW) * Wave(input.ModeSeconds / LOSS_PULSE_SECONDS);
                    Fill(lamps, count, LOSS_RED * pulse);
                    break;

                default:
                    for (int i = 0; i < count; i++)
                        lamps[i] = Hsv(layout.X[i] * RAINBOW_SPAN * 0.5f + input.Clock * IDLE_DRIFT, IDLE_SATURATION, IDLE_VALUE);
                    break;
            }

            //A card's keys blink over whatever the mode drew, on for the first half of each beat
            if (input.TutorialKeys != 0 && Fraction(input.Clock * BLINK_HERTZ) < 0.5f)
                for (int key = 0; key < KEY_COUNT; key++)
                    if ((input.TutorialKeys & (1 << key)) != 0) Paint(layout, (LitKey)key, Vector3.One, lamps);
        }

        private static void ComposeWon(in KeyLightInput input, KeyLightLayout layout, Vector3[] lamps, int count)
        {
            float t = input.ModeSeconds;
            Vector3 cup = CupColour(input.Stars);
            float breath = CUP_BREATH_LOW + (1f - CUP_BREATH_LOW) * Wave(t / CUP_BREATH_SECONDS);

            //0 while the rainbow runs, 1 once the cup's colour has taken over
            float cupShare = Math.Clamp((t - RAINBOW_SECONDS) / CUP_FADE_SECONDS, 0f, 1f);
            int twinkleBeat = (int)MathF.Floor(input.Clock * TWINKLE_HERTZ);

            for (int i = 0; i < count; i++)
            {
                Vector3 settled = cup * breath;
                if (input.Stars >= CUPS && Hash(i, twinkleBeat) < TWINKLE_SHARE) settled = Vector3.One;

                lamps[i] = cupShare >= 1f
                    ? settled
                    : Vector3.Lerp(Hsv(layout.X[i] * RAINBOW_SPAN - t * RAINBOW_SPEED, 1f, 1f), settled, cupShare);
            }
        }

        private static void Fill(Vector3[] lamps, int count, Vector3 colour)
        {
            for (int i = 0; i < count; i++) lamps[i] = colour;
        }

        private static void Paint(KeyLightLayout layout, LitKey key, Vector3 colour, Vector3[] lamps)
        {
            int[] indices = layout.KeyLamps[(int)key];
            for (int i = 0; i < indices.Length; i++)
                if ((uint)indices[i] < (uint)lamps.Length) lamps[indices[i]] = colour;
        }

        //0 to 1 and back, a cosine, once per unit of t; 1 at t = 0
        private static float Wave(float t) => 0.5f + 0.5f * MathF.Cos(MathHelper.TwoPi * t);

        private static float Fraction(float x) => x - MathF.Floor(x);

        //A fixed scatter in 0 to 1 per lamp and beat, so the twinkles are the same on every run
        private static float Hash(int lamp, int beat)
        {
            uint h = (uint)lamp * 0x9E3779B1u ^ (uint)beat * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>A hue (in turns, any value), saturation and value to sRGB 0 to 1.</summary>
        public static Vector3 Hsv(float hue, float saturation, float value)
        {
            float h = Fraction(hue) * 6f;
            float c = value * saturation;
            float x = c * (1f - MathF.Abs(h % 2f - 1f));
            float m = value - c;

            Vector3 rgb = h < 1f ? new Vector3(c, x, 0f)
                : h < 2f ? new Vector3(x, c, 0f)
                : h < 3f ? new Vector3(0f, c, x)
                : h < 4f ? new Vector3(0f, x, c)
                : h < 5f ? new Vector3(x, 0f, c)
                : new Vector3(c, 0f, x);

            return rgb + new Vector3(m);
        }
    }
}
