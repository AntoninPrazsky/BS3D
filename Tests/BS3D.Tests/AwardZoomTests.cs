using BS3D.Screens;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The award's zoom (#846): what the owner asked for, held as numbers. The points gained zoom well past the score's
    /// size, a big release bigger and longer than a bare match, then shrink and are thrown into the corner at the size
    /// they always landed at; nothing snaps on the way, however the read was cut short by a newer award; and the
    /// completion bonus, which waits for the clearing shot's award, still lands in time to be counted into the
    /// score before the result page.
    /// </summary>
    public class AwardZoomTests
    {
        //Every award size the game can pay: a bare match up to far past a big release
        private static readonly int[] Awards = { AwardZoom.SMALLEST_POINTS, 50, 90, 150, 300, 600, 1260, 2000, 5000, 40000 };

        private const float STEP = 1e-3f;

        /// <summary>Every age from birth to landing, a millisecond apart, with the size drawn at it.</summary>
        private static IEnumerable<(float Age, float Size)> Sampled(int points, float hold)
        {
            float birth = AwardZoom.Birth(points), peak = AwardZoom.Peak(points);
            float end = AwardZoom.Lifetime(hold);
            for (float age = 0f; age <= end + STEP; age += STEP)
                yield return (age, AwardZoom.Size(age, hold, birth, peak));
        }

        /// <summary>Each award's own hold, and the same award superseded at a spread of ages (<see cref="AwardZoom.HandOff"/>).</summary>
        private static IEnumerable<(int Points, float Hold)> Reads()
        {
            foreach (int points in Awards)
            {
                float hold = AwardZoom.Hold(points);
                yield return (points, hold);
                for (float at = 0f; at < hold; at += 0.05f) yield return (points, AwardZoom.HandOff(at, hold));
            }
        }

        [Fact]
        public void EvenABareMatchZoomsWellPastTheScore()
        {
            //The score's own type is 1 in these units; the old award peaked at 0.8 of it
            Assert.True(AwardZoom.Peak(AwardZoom.SMALLEST_POINTS) >= 1.5f,
                $"a bare match tops out at {AwardZoom.Peak(AwardZoom.SMALLEST_POINTS)} of the score");
            Assert.Equal(AwardZoom.SMALLEST_POINTS, 3 * ScoreKeeper.MatchedBallPoints);

            //And it gets there: the size at the end of the zoom is the peak
            int points = AwardZoom.SMALLEST_POINTS;
            float birth = AwardZoom.Birth(points);
            Assert.Equal(AwardZoom.Peak(points), AwardZoom.Size(birth, AwardZoom.Hold(points), birth, AwardZoom.Peak(points)), 4);
        }

        [Fact]
        public void ABigReleaseZoomsBiggerAndIsReadLonger()
        {
            float lastPeak = 0f, lastHold = 0f;
            for (int points = AwardZoom.SMALLEST_POINTS; points <= 100000; points += Math.Max(1, points / 50))
            {
                float peak = AwardZoom.Peak(points), hold = AwardZoom.Hold(points);
                Assert.True(peak >= lastPeak && hold >= lastHold, $"{points} points: peak {peak} or hold {hold} went down");
                lastPeak = peak;
                lastHold = hold;
            }

            Assert.Equal(AwardZoom.PEAK_BIG, AwardZoom.Peak(AwardZoom.BIG_POINTS), 4);
            Assert.Equal(AwardZoom.PEAK_BIG, AwardZoom.Peak(int.MaxValue), 4);
            Assert.True(AwardZoom.PEAK_BIG >= 2f * AwardZoom.PEAK_SMALL - 1e-4f, "a big release is not much bigger than a bare match");
            Assert.True(AwardZoom.Hold(AwardZoom.BIG_POINTS) >= 2f * AwardZoom.Hold(AwardZoom.SMALLEST_POINTS),
                "a big release is not read much longer than a bare match");

            //A release of a few hundred stands visibly between the two, not at either end
            float middle = AwardZoom.Peak(300);
            Assert.True(middle > AwardZoom.PEAK_SMALL + 0.4f && middle < AwardZoom.PEAK_BIG - 0.4f, $"300 points tops out at {middle}");
        }

        [Fact]
        public void AnOrdinaryMatchStaysQuick()
        {
            //Three balls on a capped streak (x5) is still an ordinary shot: read in about what the old popup took
            int points = 3 * ScoreKeeper.MatchedBallPoints * ScoreKeeper.MaxMultiplier;
            Assert.True(AwardZoom.Hold(points) <= 0.75f, $"{points} points is read for {AwardZoom.Hold(points)} s");
            Assert.True(AwardZoom.Lifetime(AwardZoom.Hold(points)) <= 1.3f);
        }

        [Fact]
        public void TheSizeNeverJumps()
        {
            //The steepest honest change is the zoom's first instant: a big release goes from 0.15 of three scores up
            //through OutBack's opening slope (5.8) in a third of a second, about 46 scores a second. A step between two
            //legs, or a hold cut short without the throw starting from where the read ended, is a jump far past that.
            const float MOST_PER_STEP = 60f * STEP;

            foreach ((int points, float hold) in Reads())
            {
                float? last = null;
                foreach ((float age, float size) in Sampled(points, hold))
                {
                    if (last is float before)
                        Assert.True(MathF.Abs(size - before) <= MOST_PER_STEP,
                            $"{points} points held {hold:0.000} s: the size jumps {before} -> {size} at {age:0.000} s");
                    last = size;
                }
            }
        }

        [Fact]
        public void ItLandsAtHalfTheScoreHavingShrunkBeforeItFlew()
        {
            foreach ((int points, float hold) in Reads())
            {
                float birth = AwardZoom.Birth(points), peak = AwardZoom.Peak(points);

                Assert.Equal(AwardZoom.ARRIVE, AwardZoom.Size(AwardZoom.Lifetime(hold), hold, birth, peak), 4);

                //Shrunk by the time the throw passes the point it was read at, and still there while it shrank
                float passing = hold + AwardZoom.SHRINK_SHARE * AwardZoom.FLIGHT;
                Assert.Equal(AwardZoom.ARRIVE, AwardZoom.Size(passing, hold, birth, peak), 4);
            }

            Assert.Equal(0f, HudEase.InBack(AwardZoom.SHRINK_SHARE), 4);
            for (float f = STEP; f < AwardZoom.SHRINK_SHARE; f += STEP)
                Assert.True(HudEase.InBack(f) <= 0f, $"the throw is past its source at {f} of the flight, before the shrink is done");
        }

        [Fact]
        public void NeverDrawnBiggerThanTheTallestAwardFace()
        {
            //BS3DGame rasterises the top of the awards' ladder at LARGEST: drawing past it would stretch the glyphs
            float largest = 0f;
            foreach ((int points, float hold) in Reads())
                foreach ((_, float size) in Sampled(points, hold))
                    largest = MathF.Max(largest, size);

            Assert.True(largest <= AwardZoom.LARGEST + 1e-4f, $"an award is drawn at {largest} scores, past LARGEST {AwardZoom.LARGEST}");
            Assert.True(largest >= AwardZoom.LARGEST - 0.01f, $"LARGEST {AwardZoom.LARGEST} is never reached (top {largest}): the face is rasterised bigger than it needs");
        }

        [Fact]
        public void ASupersededAwardIsReadWholeAndLeavesWithoutAJump()
        {
            Assert.True(AwardZoom.MIN_READ >= AwardZoom.BIRTH_BIG, "a superseded award can leave before its zoom has finished");

            foreach (int points in Awards)
            {
                float hold = AwardZoom.Hold(points);
                for (float age = 0f; age <= hold; age += STEP)
                {
                    float cut = AwardZoom.HandOff(age, hold);

                    Assert.True(cut >= age - 1e-6f, $"{points} points superseded at {age} s would have left in the past ({cut} s)");
                    Assert.True(cut <= hold + 1e-6f, $"{points} points superseded at {age} s is read longer than its own hold");
                    Assert.True(cut >= MathF.Min(hold, AwardZoom.MIN_READ) - 1e-6f,
                        $"{points} points superseded at {age} s leaves after {cut} s, short of the least read");

                    //And when it leaves it has been seen at its full size
                    float size = AwardZoom.Size(cut, cut, AwardZoom.Birth(points), AwardZoom.Peak(points));
                    Assert.True(size >= AwardZoom.Peak(points) - 1e-4f, $"{points} points leaves at {size}, under its peak");
                }

                //Superseded after it would have left anyway, it is unchanged
                Assert.Equal(hold, AwardZoom.HandOff(hold, hold), 6);
            }
        }

        [Fact]
        public void TheBonusLandsAndIsCountedInBeforeThePage()
        {
            //The page covers the HUD when the cleared beat ends: an award still in the air then is never seen to land, and
            //one landing just before it is cut off while the corner is still counting it into the score
            float worst = AwardZoom.BonusWait(float.MaxValue) + AwardZoom.Lifetime(AwardZoom.Hold(int.MaxValue));
            Assert.True(worst + AwardZoom.BONUS_COUNT_IN <= LevelPhases.ClearedBeatSeconds + 1e-4f,
                $"the bonus can land {worst} s after the clear, leaving the corner {LevelPhases.ClearedBeatSeconds - worst} s to count it in");

            //And the clearing shot's award is seen whole before the bonus takes the read, as a superseded award is
            foreach (float readLeft in new[] { -1f, 0f, 0.3f, AwardZoom.MIN_READ, 1f, float.MaxValue })
            {
                float wait = AwardZoom.BonusWait(readLeft);
                Assert.True(wait >= MathF.Min(MathF.Max(readLeft, 0f), AwardZoom.MIN_READ) - 1e-6f,
                    $"the bonus is born {wait} s after the clear, before the clearing award ({readLeft} s of read left) is read");
                Assert.True(wait <= MathF.Max(readLeft, 0f) + 1e-6f, $"the bonus waits {wait} s on a read that ends in {readLeft} s");
            }
        }

        [Fact]
        public void AnAwardHandedOnShrinksUnderTheScoreWithinAQuarterSecond()
        {
            //A superseded award shrinks where it stands while the newer one zooms in, and is larger than the score for no
            //longer than this after it leaves: how long two big numbers stand together at a hand-off, which the HUD's doc
            //quotes (plus what was left of MIN_READ when the landings were closer than it)
            const float SHRUNK_WITHIN = 0.25f;

            foreach ((int points, float hold) in Reads())
            {
                float birth = AwardZoom.Birth(points), peak = AwardZoom.Peak(points);
                float size = AwardZoom.Size(hold + SHRUNK_WITHIN, hold, birth, peak);
                Assert.True(size <= 1f, $"{points} points left at {hold:0.000} s and is still {size} scores {SHRUNK_WITHIN} s later");
            }
        }

        [Fact]
        public void TheRiseStopsWhenTheReadDoes()
        {
            foreach ((int points, float hold) in Reads())
            {
                Assert.Equal(0f, AwardZoom.Rise(0f, hold));

                float last = 0f;
                for (float age = 0f; age <= AwardZoom.Lifetime(hold); age += STEP)
                {
                    float rise = AwardZoom.Rise(age, hold);
                    Assert.True(rise >= last && rise < 1f, $"{points} points: the rise goes {last} -> {rise} at {age} s");
                    if (age > hold) Assert.Equal(AwardZoom.Rise(hold, hold), rise, 6);
                    last = rise;
                }
            }
        }
    }
}
