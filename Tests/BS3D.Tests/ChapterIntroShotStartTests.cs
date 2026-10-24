using BS3D.Effects;
using Microsoft.Xna.Framework;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// A prologue shot is told once that it has come on screen (#975, <see cref="IntroShot.Start"/>), on the intro's own
    /// clock: a pause stops that clock and a hitch holds it to <c>ChapterIntro.MAX_INTRO_STEP</c> a frame, while the wall
    /// clock a scene's events run on does not stop, so a shot timed to one has to hear when it really starts.
    /// </summary>
    public class ChapterIntroShotStartTests
    {
        private const float FRAME = 1f / 60f;

        private static IntroShot Shot(string name, float seconds, Action onStart) =>
            new(name, new[] { new Vector3(0f, 50f, 100f), new Vector3(10f, 50f, 100f) }, seconds, 1f, lookAt: Vector3.Zero,
                onStart: onStart);

        [Fact]
        public void Each_shot_starts_once_when_the_intro_reaches_it_and_a_pause_or_hitch_delays_it()
        {
            var intro = new ChapterIntro();
            int firstStarts = 0, secondStarts = 0;
            float secondAt = float.NaN;

            intro.Begin(Vector3.Zero, 40f, 0.75f, new Vector3(0f, 10f, 40f), Vector3.Zero, null, new Random(1),
                new[] { Shot("a", 2f, () => firstStarts++), Shot("b", 3f, () => { secondStarts++; secondAt = intro.Elapsed; }) });

            //A second of play, a five-second pause (no frames reach the intro), one 400-ms hitch, then play on
            float wall = 0f;
            for (int i = 0; i < 60; i++) { intro.Update(FRAME); wall += FRAME; }
            wall += 5f;
            intro.Update(0.4f);
            wall += 0.4f;
            for (int i = 0; i < 300; i++) { intro.Update(FRAME); wall += FRAME; }

            Assert.Equal(1, firstStarts);
            Assert.Equal(1, secondStarts);
            Assert.InRange(secondAt, 2f, 2f + 2f * FRAME);
        }
    }
}
