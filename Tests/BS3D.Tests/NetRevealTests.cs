using BS3D.Effects;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The floor net shown to a player who is not in danger from it (#933): the reveal at a level's start (the emitters turn
    /// from rest to their aim, the net is lit for a moment and goes out) and the blink while the line's card is up.
    /// <c>NetReveal</c> is compiled in from the Game - clocks over floats, no device - and stepped here the way the session
    /// steps it: a frame's seconds, whether the lens is held, whether the card is up.
    /// </summary>
    public class NetRevealTests
    {
        private const float FRAME = 1f / 60f;

        private static NetReveal Level(bool reveals)
        {
            NetReveal reveal = new();
            reveal.Reset(reveals);

            return reveal;
        }

        //Steps for a number of seconds, at a frame rate, and says what it saw each frame
        private static void Run(NetReveal reveal, float seconds, Action<NetReveal, float> seen = null, float frame = FRAME,
            bool held = false, bool blinking = false)
        {
            int frames = (int)MathF.Round(seconds / frame);

            for (int i = 1; i <= frames; i++)
            {
                reveal.Advance(frame, held, blinking);
                seen?.Invoke(reveal, i * frame);
            }
        }

        [Fact]
        public void ARevealWaitsAtRestForAsLongAsTheLensIsHeldAndThenBegins()
        {
            NetReveal reveal = Level(true);

            //A chapter's tour, a camera takeover: the housings stand where they were built and nothing is lit
            Run(reveal, 5f, (r, t) =>
            {
                Assert.Equal(0f, r.Swing);
                Assert.Equal(0f, r.Net);
                Assert.Equal(0f, r.Elapsed);
                Assert.True(r.Running);
            }, held: true);

            //Handed back, it starts from the beginning, not from where the five seconds would have taken it
            Run(reveal, 0.1f);
            Assert.InRange(reveal.Elapsed, 0.09f, 0.11f);
            Assert.InRange(reveal.Swing, 0.01f, 0.2f);
        }

        [Fact]
        public void TheHousingsTurnFromRestToTheAimEvenlyAndAreThereWhenTheLightComes()
        {
            NetReveal reveal = Level(true);
            float last = 0f;
            float lastLit = 0f;

            Run(reveal, NetReveal.SWING_SECONDS + 0.5f, (r, t) =>
            {
                Assert.InRange(r.Swing, last - 1e-6f, 1f);
                last = r.Swing;

                //Dark through the turn: the light comes after it, never before the housings have got to where they point
                if (t < NetReveal.SWING_SECONDS - 0.01f) Assert.True(r.Net == 0f, $"the net is lit at {t} s, before the housings have arrived");
                lastLit = r.Net;
            });

            Assert.Equal(1f, reveal.Swing);
            Assert.True(lastLit > 1f, "the net is not lit once the housings have arrived");

            //The ease is symmetric: half way through the turn it is half way round
            NetReveal half = Level(true);
            Run(half, NetReveal.SWING_SECONDS / 2f);
            Assert.InRange(half.Swing, 0.49f, 0.51f);
        }

        [Fact]
        public void TheNetIsLitAtTheBoldLevelHeldAndOutByTheEndOfTwoSeconds()
        {
            Assert.True(NetReveal.TOTAL_SECONDS <= 2f, "the reveal is longer than a fast player would wait");

            NetReveal reveal = Level(true);
            float peak = 0f;
            float litSeconds = 0f;

            Run(reveal, NetReveal.TOTAL_SECONDS + 0.2f, (r, t) =>
            {
                peak = MathF.Max(peak, r.Net);
                if (r.Net > 0.5f) litSeconds += FRAME;
            });

            //Bolder than the alarm's own peak of 1, flashing over the level it settles at
            Assert.True(peak > NetReveal.LEVEL, $"the net's peak is only {peak}");
            Assert.InRange(litSeconds, 1f, 1.5f);

            //Over: dark, aimed, and no longer running
            Assert.False(reveal.Running);
            Assert.Equal(0f, reveal.Net);
            Assert.Equal(0f, reveal.Lamp);
            Assert.Equal(1f, reveal.Swing);

            //In the middle of the hold the net burns at the level and not at the flash
            NetReveal held = Level(true);
            Run(held, 1.2f);
            Assert.InRange(held.Net, NetReveal.LEVEL * 0.99f, NetReveal.LEVEL * 1.01f);
        }

        [Fact]
        public void TheLensesAreChargedWhileTheHousingsTurnAndOutWithTheLight()
        {
            NetReveal reveal = Level(true);

            Run(reveal, NetReveal.SWING_SECONDS);
            Assert.InRange(reveal.Lamp, NetReveal.CHARGE * 0.99f, NetReveal.CHARGE * 1.01f);
            Assert.InRange(reveal.Net, 0f, 1e-3f);

            Run(reveal, NetReveal.TOTAL_SECONDS);
            Assert.Equal(0f, reveal.Lamp);
        }

        [Fact]
        public void TheRevealDoesNotDependOnTheFrameRate()
        {
            //A pure function of the seconds it is given: a slow machine and a fast one see the same reveal at the same moment
            foreach (float at in new[] { 0.3f, 0.7f, 1.2f, 1.8f })
            {
                NetReveal slow = Level(true), fast = Level(true);
                Run(slow, at, frame: 1f / 30f);
                Run(fast, at, frame: 1f / 240f);

                Assert.InRange(MathF.Abs(slow.Swing - fast.Swing), 0f, 0.01f);
                Assert.InRange(MathF.Abs(slow.Net - fast.Net), 0f, 0.05f);
            }
        }

        [Fact]
        public void ACameraTakeoverHoldsTheRevealWhereItStandsAndItGoesOnAfterwards()
        {
            NetReveal reveal = Level(true);
            Run(reveal, 0.9f);
            float elapsed = reveal.Elapsed, swing = reveal.Swing, net = reveal.Net;
            Assert.True(net > 0f);

            Run(reveal, 3f, (r, t) =>
            {
                Assert.Equal(elapsed, r.Elapsed);
                Assert.Equal(swing, r.Swing);
                Assert.Equal(net, r.Net);
            }, held: true);

            Run(reveal, 0.5f);
            Assert.True(reveal.Elapsed > elapsed + 0.4f);
        }

        [Fact]
        public void AShotFinishesTheTurnAtOnceAndPutsTheLightOutQuickly()
        {
            //In the middle of the light
            NetReveal reveal = Level(true);
            Run(reveal, 1.2f);
            Assert.True(reveal.Net > 1f);

            reveal.Skip();
            Run(reveal, NetReveal.SKIP_FADE_SECONDS + 0.05f);
            Assert.Equal(0f, reveal.Net);

            //In the middle of the turn: it lands, and the net is never lit
            NetReveal turning = Level(true);
            Run(turning, 0.2f);
            Assert.True(turning.Swing < 1f);

            float peak = 0f;
            turning.Skip();
            Run(turning, 1f, (r, t) => peak = MathF.Max(peak, r.Net));
            Assert.Equal(1f, turning.Swing);
            Assert.False(turning.Running);
            Assert.True(peak < 0.05f, $"a skipped reveal lit the net to {peak}");

            //Skipped before it has begun (the lens is still held): it is over the moment it is let go of, never lit
            NetReveal early = Level(true);
            early.Skip();
            Run(early, 2f, held: true);
            peak = 0f;
            Run(early, 1f, (r, t) => peak = MathF.Max(peak, r.Net));
            Assert.Equal(1f, early.Swing);
            Assert.True(peak < 0.05f, $"a reveal skipped before it began lit the net to {peak}");
        }

        [Fact]
        public void ALevelThatDoesNotRevealStandsAimedAndDark()
        {
            NetReveal reveal = Level(false);

            Run(reveal, 5f, (r, t) =>
            {
                Assert.Equal(1f, r.Swing);
                Assert.Equal(0f, r.Net);
                Assert.Equal(0f, r.Lamp);
                Assert.False(r.Running);
            });
        }

        [Fact]
        public void AnotherLevelStartsTheRevealOverFromRest()
        {
            NetReveal reveal = Level(true);
            Run(reveal, 3f);
            reveal.Skip();
            Assert.False(reveal.Running);

            reveal.Reset(true);
            Assert.True(reveal.Running);
            Assert.Equal(0f, reveal.Swing);
            Assert.Equal(0f, reveal.Net);

            Run(reveal, 1.2f);
            Assert.InRange(reveal.Net, NetReveal.LEVEL * 0.99f, NetReveal.LEVEL * 1.01f);
        }

        [Fact]
        public void TheCardsBlinkIsLitThenDarkAtItsOwnSlowRateAndStopsWithTheCard()
        {
            //Slower than the alarm's own pulse (1.6 Hz, LaserGrid.PULSE_HZ) and dark between, which is what makes it a blink
            Assert.True(NetReveal.BLINK_PERIOD >= 1f);

            NetReveal reveal = Level(false);
            float peak = 0f, trough = float.MaxValue;
            int lit = 0, frames = 0;

            //Past the blink's ease-in, over whole periods
            Run(reveal, 1f, blinking: true);
            Run(reveal, NetReveal.BLINK_PERIOD * 5f, (r, t) =>
            {
                peak = MathF.Max(peak, r.Net);
                trough = MathF.Min(trough, r.Net);
                if (r.Net > NetReveal.LEVEL * 0.5f) lit++;
                frames++;
            }, blinking: true);

            Assert.InRange(peak, NetReveal.LEVEL * 0.99f, NetReveal.LEVEL * 1.01f);
            Assert.Equal(0f, trough);
            Assert.InRange(lit / (float)frames, 0.45f, 0.65f);

            //The card goes: the blink is out within an ease and stays out, however long the card's clock would have run
            Run(reveal, 0.4f);
            Assert.Equal(0f, reveal.Net);
            Run(reveal, 3f, (r, t) => Assert.Equal(0f, r.Net));

            //The next card's blink begins lit, from the start, not from wherever the last one stopped
            float first = 0f;
            Run(reveal, 0.3f, (r, t) => first = MathF.Max(first, r.Net), blinking: true);
            Assert.True(first > NetReveal.LEVEL * 0.9f, $"a new blink is only {first} in its first third of a second");
        }

        [Fact]
        public void TheBlinkNeverLightsTheNetUnlessTheCardIsUp()
        {
            NetReveal reveal = Level(true);

            //A level that begins its reveal with the card never up: only the reveal's own light is ever asked for, and the
            //blink's contribution is exactly nothing once the reveal is over
            Run(reveal, NetReveal.TOTAL_SECONDS + 0.1f);
            Run(reveal, 5f, (r, t) => Assert.Equal(0f, r.Net));
        }

        [Fact]
        public void TheRevealAndTheBlinkShareTheNetByWhicheverIsMore()
        {
            //Saturn, where both stand: the card arrives while the housings are still turning, and the blink lights the net
            //at once, before the reveal's own light is due
            NetReveal reveal = Level(true);
            Run(reveal, 0.35f, blinking: true);
            float blink = NetReveal.BlinkAt(0.35f) * NetReveal.LEVEL;
            Assert.True(blink > 1f);
            Assert.True(reveal.Swing < 1f);
            Assert.InRange(reveal.Net, blink * 0.9f, blink * 1.1f);

            //And never more than the larger of the two
            Run(reveal, 1.5f, (r, t) => Assert.True(r.Net <= NetReveal.LEVEL + NetReveal.FLASH + 1e-3f), blinking: true);
        }
    }
}
