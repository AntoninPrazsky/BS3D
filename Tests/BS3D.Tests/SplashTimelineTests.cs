using BS3D.Screens;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The intro's dissolve (#1105), held as numbers: the picture starts to fade a moment after the black has begun to lift
    /// and before the lift is over, so the two read as one dissolve with the logo trailing rather than two steps; the
    /// wordmark is never at full strength while the picture is (#621: the two titles at once read as one garbled word); the
    /// page leaves when the picture has gone, which is when <c>BS3DGame.IsSplashUp</c> goes false; and a skip runs the same
    /// overlap, faster.
    /// </summary>
    public class SplashTimelineTests
    {
        private const float STEP = 1e-3f;
        private const float EPSILON = 1e-4f;

        /// <summary>A frame at the owner's 75 Hz, for the skip's walk.</summary>
        private const float FRAME = 1f / 75f;

        /// <summary>Every age from the first frame to a little past the end, a millisecond apart.</summary>
        private static IEnumerable<float> Ages()
        {
            for (float age = 0f; age <= SplashTimeline.SECONDS + 0.5f; age += STEP) yield return age;
        }

        /// <summary>The first sampled age past <paramref name="from"/> at which <paramref name="test"/> holds, or NaN.</summary>
        private static float First(float from, Func<float, bool> test)
        {
            foreach (float age in Ages())
                if (age > from && test(age)) return age;
            return float.NaN;
        }

        //Where the legs begin and end, read off the drawn strengths rather than the constants, so a leg moved by a change to
        //the functions is caught as surely as one moved by a figure
        private static float BlackStarts => First(0f, a => SplashTimeline.Black(a) < 1f - EPSILON);
        private static float BlackGone => First(0f, a => SplashTimeline.Black(a) <= EPSILON);
        private static float LogoStartsToGo => First(SplashTimeline.FADE_UP_SECONDS, a => SplashTimeline.Logo(a) < 1f - EPSILON);
        private static float LogoGone => First(SplashTimeline.FADE_UP_SECONDS, a => SplashTimeline.Logo(a) <= EPSILON);

        [Fact]
        public void ThePictureStartsToGoInsideTheBlacksLift()
        {
            float blackStarts = BlackStarts, blackGone = BlackGone, logoGoes = LogoStartsToGo;

            Assert.True(logoGoes > blackStarts,
                $"the picture starts to fade at {logoGoes:F3} s, not after the black starts to lift at {blackStarts:F3} s");
            Assert.True(logoGoes < blackGone,
                $"the picture starts to fade at {logoGoes:F3} s, after the black has gone at {blackGone:F3} s: two steps, not one dissolve");
        }

        [Fact]
        public void TheLogoTrailsTheBlackAndOutlastsIt()
        {
            //Through the dissolve the picture is at least as present as the black, so the scene never stands clear of the
            //black while the logo has already gone further than it
            foreach (float age in Ages())
                if (age >= SplashTimeline.EXIT_START)
                    Assert.True(SplashTimeline.Logo(age) >= SplashTimeline.Black(age) - EPSILON,
                        $"at {age:F3} s the black ({SplashTimeline.Black(age):F3}) is ahead of the picture ({SplashTimeline.Logo(age):F3})");

            Assert.True(LogoGone > BlackGone, $"the picture is gone at {LogoGone:F3} s, no later than the black at {BlackGone:F3} s");
        }

        [Fact]
        public void TheWordmarkIsNeverAtFullStrengthWhileThePictureIs()
        {
            foreach (float age in Ages())
            {
                float logo = SplashTimeline.Logo(age), wordmark = SplashTimeline.WordmarkPresence(age);

                Assert.True(logo + wordmark <= 1f + EPSILON,
                    $"at {age:F3} s the picture ({logo:F3}) and the wordmark ({wordmark:F3}) overlap");
                if (logo >= 1f - EPSILON)
                    Assert.True(wordmark <= EPSILON, $"at {age:F3} s the wordmark is at {wordmark:F3} under a picture still fully up");
            }

            Assert.Equal(1f, SplashTimeline.WordmarkPresence(SplashTimeline.SECONDS), 4);
        }

        [Fact]
        public void ThePageLeavesWhenThePictureHasGone()
        {
            //The page replaces itself, and IsSplashUp goes false, at SECONDS: that is when the last thing it draws has gone,
            //not a beat later and never before
            float lastGone = MathF.Max(LogoGone, BlackGone);

            Assert.Equal(0f, SplashTimeline.Logo(SplashTimeline.SECONDS), 4);
            Assert.Equal(0f, SplashTimeline.Black(SplashTimeline.SECONDS), 4);
            //"Gone" is read at EPSILON, which a smoothstep's flat end reaches a few milliseconds early: within a frame is the end
            Assert.True(lastGone <= SplashTimeline.SECONDS && SplashTimeline.SECONDS - lastGone <= FRAME,
                $"the page leaves at {SplashTimeline.SECONDS:F3} s and its picture is gone at {lastGone:F3} s");
        }

        /// <summary>
        /// A player's skip, pressed in the fade up, in the hold or in the dissolve itself: the page's own steps, frame by frame
        /// at 75 Hz (<see cref="SplashTimeline.Advance"/>, then the press, then <see cref="SplashTimeline.SkipHold"/>, in
        /// <c>SplashPage.Update</c>'s order), and the overlap read off the wall clock.
        /// </summary>
        [Theory]
        [InlineData(0.4f)]
        [InlineData(1.2f)]
        [InlineData(2.3f)]
        public void ASkipKeepsTheSameOverlap(float pressedAt)
        {
            float age = 0f, wall = 0f;
            bool skipping = false;
            float blackStarts = float.NaN, blackGone = float.NaN, logoGoes = float.NaN, logoGone = float.NaN;

            while (age < SplashTimeline.SECONDS && wall < 10f)
            {
                age = SplashTimeline.Advance(age, FRAME, skipping);
                wall += FRAME;
                if (!skipping && wall >= pressedAt) skipping = true;
                if (skipping) age = SplashTimeline.SkipHold(age);

                float black = SplashTimeline.Black(age), logo = SplashTimeline.Logo(age);
                if (float.IsNaN(blackStarts) && black < 1f - EPSILON) blackStarts = wall;
                if (float.IsNaN(blackGone) && black <= EPSILON) blackGone = wall;
                if (float.IsNaN(logoGoes) && age > SplashTimeline.FADE_UP_SECONDS && logo < 1f - EPSILON) logoGoes = wall;
                if (float.IsNaN(logoGone) && age > SplashTimeline.FADE_UP_SECONDS && logo <= EPSILON) logoGone = wall;
            }

            Assert.True(logoGoes > blackStarts && logoGoes < blackGone,
                $"skipped at {pressedAt} s: the black lifts from {blackStarts:F3} to {blackGone:F3} s and the picture starts to go at {logoGoes:F3} s");
            Assert.True(logoGone > blackGone, $"skipped at {pressedAt} s: the picture is gone at {logoGone:F3} s, the black at {blackGone:F3} s");

            //A press before the dissolve runs all of it at the skip's rate, so the lag is the unskipped one a third as long,
            //give or take the frames the two starts are read on; a press inside it runs only the rest of the lag faster
            float lag = logoGoes - blackStarts, skippedLag = SplashTimeline.LOGO_LAG_SECONDS / SplashTimeline.SKIP_RATE;
            if (pressedAt < SplashTimeline.EXIT_START)
                Assert.InRange(lag, skippedLag - 2f * FRAME, skippedLag + 2f * FRAME);
            else
                Assert.InRange(lag, skippedLag - 2f * FRAME, SplashTimeline.LOGO_LAG_SECONDS + 2f * FRAME);
        }
    }
}
