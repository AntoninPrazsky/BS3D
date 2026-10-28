using BS3D.Lighting;
using BS3D.Screens;
using Microsoft.Xna.Framework;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #987: what the backlit keyboard shows, held as colours a lamp without a device. Whether it looks good is the owner's eye on his
    /// own keyboard; what a test can refuse is a fire key that is not the loaded ball, a queue in the wrong keys or order, a card's key
    /// that does not blink, a clear that is not a rainbow and then its cup, and a loss that is not red.
    /// </summary>
    public class KeyLightModelTests
    {
        //A board of 21 lamps: one a named key (0 to 11, in LitKey's order), and nine more across the rest of the board
        private static KeyLightLayout Board()
        {
            const int lamps = 21;
            var x = new float[lamps];
            for (int i = 0; i < lamps; i++) x[i] = i / (float)(lamps - 1);

            var keys = new int[KeyLightModel.KEY_COUNT][];
            for (int key = 0; key < keys.Length; key++) keys[key] = new[] { key };

            return new KeyLightLayout(x, keys);
        }

        private static readonly Vector3 RED = new(1f, 0.1f, 0.1f), GREEN = new(0.1f, 1f, 0.1f), BLUE = new(0.1f, 0.2f, 1f);

        private static Vector3[] Compose(KeyLightInput input, Vector3[] queue = null)
        {
            KeyLightLayout board = Board();
            var lamps = new Vector3[board.LampCount];
            KeyLightModel.Compose(input, queue ?? new Vector3[4], board, lamps);
            return lamps;
        }

        private static void Near(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"{expected} != {actual}");

        [Fact]
        public void Playing_lights_the_board_in_the_loaded_ball_and_space_and_the_queue_at_full()
        {
            var queue = new[] { GREEN, BLUE, RED, GREEN };
            Vector3[] lamps = Compose(new KeyLightInput { Mode = KeyLightMode.Playing, HasLoaded = true, Loaded = RED, QueueCount = 4 }, queue);

            Near(RED, lamps[(int)LitKey.Space]);
            Near(RED * KeyLightModel.PLAY_BASE, lamps[(int)LitKey.E]);
            Near(RED * KeyLightModel.PLAY_BASE, lamps[20]);
            for (int i = 0; i < 4; i++) Near(queue[i], lamps[(int)LitKey.F1 + i]);
        }

        [Fact]
        public void The_queue_stops_where_the_budget_does_and_no_loaded_ball_is_dark()
        {
            Vector3[] lamps = Compose(new KeyLightInput { Mode = KeyLightMode.Playing, HasLoaded = true, Loaded = RED, QueueCount = 1 },
                new[] { GREEN, BLUE, BLUE, BLUE });
            Near(GREEN, lamps[(int)LitKey.F1]);
            Near(Vector3.Zero, lamps[(int)LitKey.F2]);
            Near(Vector3.Zero, lamps[(int)LitKey.F4]);

            lamps = Compose(new KeyLightInput { Mode = KeyLightMode.Playing, HasLoaded = false, Loaded = RED });
            Near(Vector3.Zero, lamps[(int)LitKey.Space]);
            Near(Vector3.Zero, lamps[20]);
        }

        [Fact]
        public void A_cards_keys_blink_white_and_nothing_else_does()
        {
            int keys = KeyLightModel.KeysFor(Tutorial.Lesson.Swap, 0);
            var on = new KeyLightInput { Mode = KeyLightMode.Playing, HasLoaded = true, Loaded = BLUE, TutorialKeys = keys, Clock = 0.1f };
            var off = on;
            off.Clock = 0.1f + 0.5f / KeyLightModel.BLINK_HERTZ;

            Near(Vector3.One, Compose(on)[(int)LitKey.E]);
            Near(BLUE * KeyLightModel.PLAY_BASE, Compose(off)[(int)LitKey.E]);
            Near(BLUE * KeyLightModel.PLAY_BASE, Compose(on)[(int)LitKey.Q]);
            Near(BLUE, Compose(on)[(int)LitKey.Space]);
        }

        [Fact]
        public void The_cards_name_the_keys_they_ask_for()
        {
            Assert.Equal(KeyLightModel.Bit(LitKey.Space), KeyLightModel.KeysFor(Tutorial.Lesson.Fire, 0));
            Assert.Equal(KeyLightModel.Bit(LitKey.A) | KeyLightModel.Bit(LitKey.D), KeyLightModel.KeysFor(Tutorial.Lesson.Traverse, 0));
            Assert.Equal(KeyLightModel.Bit(LitKey.W) | KeyLightModel.Bit(LitKey.S), KeyLightModel.KeysFor(Tutorial.Lesson.Walk, 0));
            Assert.Equal(KeyLightModel.Bit(LitKey.Q), KeyLightModel.KeysFor(Tutorial.Lesson.Brake, 0));
            Assert.Equal(KeyLightModel.Bit(LitKey.R), KeyLightModel.KeysFor(Tutorial.Lesson.Cut, 0));

            //The lean-and-walk card asks for the mouse first and the walk second
            Assert.Equal(0, KeyLightModel.KeysFor(Tutorial.Lesson.Combine, 0));
            Assert.Equal(KeyLightModel.Bit(LitKey.A) | KeyLightModel.Bit(LitKey.D), KeyLightModel.KeysFor(Tutorial.Lesson.Combine, 1));

            Assert.Equal(0, KeyLightModel.KeysFor(Tutorial.Lesson.Aim, 0));
            Assert.Equal(0, KeyLightModel.KeysFor(Tutorial.Lesson.KindBomb, 0));
            Assert.Equal(0, KeyLightModel.KeysFor(null, 0));
        }

        [Fact]
        public void A_clear_sweeps_a_rainbow_and_then_settles_into_its_cup()
        {
            //The rainbow: full brightness, and different colours across the board
            Vector3[] rainbow = Compose(new KeyLightInput { Mode = KeyLightMode.Won, Stars = 3, ModeSeconds = 0.5f });
            Assert.True(Vector3.Distance(rainbow[0], rainbow[10]) > 0.5f, $"{rainbow[0]} and {rainbow[10]} are one colour");
            Assert.All(rainbow, c => Assert.Equal(1f, MathF.Max(c.X, MathF.Max(c.Y, c.Z)), 3));

            //Then the cup: every lamp its colour, at most dimmed by the breath
            for (int stars = 1; stars <= 3; stars++)
            {
                Vector3 cup = KeyLightModel.CupColour(stars);
                Vector3[] settled = Compose(new KeyLightInput
                {
                    Mode = KeyLightMode.Won, Stars = stars, ModeSeconds = KeyLightModel.RAINBOW_SECONDS + KeyLightModel.CUP_FADE_SECONDS + 0.3f,
                });
                foreach (Vector3 c in settled)
                {
                    float share = c.Length() / cup.Length();
                    Assert.InRange(share, KeyLightModel.CUP_BREATH_LOW - 1e-3f, 1f + 1e-3f);
                    Near(cup * share, c);
                }
            }
        }

        [Fact]
        public void The_four_cups_are_four_colours_and_a_diamond_twinkles()
        {
            for (int a = 1; a <= KeyLightModel.CUPS; a++)
                for (int b = a + 1; b <= KeyLightModel.CUPS; b++)
                    Assert.True(Vector3.Distance(KeyLightModel.CupColour(a), KeyLightModel.CupColour(b)) > 0.2f, $"cups {a} and {b} read alike");

            int white = 0, lamps = 0;
            for (float clock = 0f; clock < 3f; clock += 0.17f)
            {
                Vector3[] board = Compose(new KeyLightInput { Mode = KeyLightMode.Won, Stars = 4, ModeSeconds = 10f, Clock = clock });
                foreach (Vector3 c in board)
                {
                    lamps++;
                    if (c == Vector3.One) white++;
                }
            }
            Assert.InRange(white / (float)lamps, KeyLightModel.TWINKLE_SHARE * 0.4f, KeyLightModel.TWINKLE_SHARE * 2f);
        }

        [Fact]
        public void A_loss_is_red_and_pulses()
        {
            Vector3[] bright = Compose(new KeyLightInput { Mode = KeyLightMode.Lost, ModeSeconds = 0f });
            Vector3[] dim = Compose(new KeyLightInput { Mode = KeyLightMode.Lost, ModeSeconds = KeyLightModel.LOSS_PULSE_SECONDS / 2f });

            Near(KeyLightModel.LOSS_RED, bright[5]);
            Near(KeyLightModel.LOSS_RED * KeyLightModel.LOSS_LOW, dim[5]);
            Assert.All(bright, c => Assert.True(c.X > 5f * MathF.Max(c.Y, c.Z), $"{c} is not red"));
        }

        [Fact]
        public void The_menus_are_dim_and_a_key_the_board_lacks_is_skipped()
        {
            Vector3[] idle = Compose(new KeyLightInput { Mode = KeyLightMode.Idle, Clock = 12f });
            Assert.All(idle, c => Assert.InRange(MathF.Max(c.X, MathF.Max(c.Y, c.Z)), 0f, KeyLightModel.IDLE_VALUE + 1e-4f));

            //A board without F1 to F4 (keys reported with no lamps) composes without throwing, every key asked for, in the blink's dark half
            var keys = new int[KeyLightModel.KEY_COUNT][];
            for (int key = 0; key < keys.Length; key++) keys[key] = key >= (int)LitKey.F1 ? Array.Empty<int>() : new[] { key };
            var board = new KeyLightLayout(new float[8], keys);
            var lamps = new Vector3[8];
            KeyLightModel.Compose(new KeyLightInput { Mode = KeyLightMode.Playing, HasLoaded = true, Loaded = RED, QueueCount = 4, TutorialKeys = -1, Clock = 0.3f },
                new[] { GREEN, GREEN, GREEN, GREEN }, board, lamps);
            Near(RED, lamps[(int)LitKey.Space]);
        }
    }
}
