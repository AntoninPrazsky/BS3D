using BS3D.Audio;
using BS3D.Screens;
using Prazsky.BS3D.Levels;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #726: a pour of blue ceiling steps climbs. <see cref="CeilingFeedPour"/> places each blue step (a feed, or the Brake's
    /// lift) in its run: a semitone above the last while they keep coming, to a fifth, and from the bottom again after a gap.
    /// It is driven here the way the session drives it, beside <see cref="CeilingDescent.Update"/> on the same frame seconds,
    /// so what is held is the rule against the descent's real timing: the queued steps of one feed come inside the window at
    /// any frame rate, a red step plays at its own pitch and does not break a pour, and a pause (the screen not updated at
    /// all) holds a pour where it was, however long it lasts on the wall clock.
    /// </summary>
    public class CeilingFeedPourTests
    {
        //A gap a step apart inside the pour, as the descent lets queued steps go, and one well past the window
        private const float INSIDE = CeilingFeedPour.WINDOW_SECONDS * 0.45f;
        private const float OUTSIDE = CeilingFeedPour.WINDOW_SECONDS * 1.5f;

        [Theory]
        [InlineData(1f / 240f)]
        [InlineData(1f / 60f)]
        [InlineData(1f / 30f)]
        [InlineData(1f / 15f)]
        public void A_feeds_queued_steps_climb_a_semitone_each_to_a_fifth_at_any_frame_rate(float frame)
        {
            //Eleven feed steps queued by one landing, the run a scripted Totem run produced, on glass with room for all
            CeilingDescent glass = new();
            glass.Reset(ClusterHang.DEATH_Y + 30f, feedFloorLevel: 0);
            Assert.Equal(11, glass.Feed(lowest: 20, intoReach: 11, out _, out _));

            CeilingFeedPour pour = new();
            List<int> climbs = new();
            for (int i = 0; i < 100_000 && climbs.Count < 11; i++)
            {
                pour.Advance(frame);
                if (glass.Update(frame, levelDecided: false, takeoverEngaged: false, out bool feeding, out _))
                {
                    Assert.True(feeding);
                    climbs.Add(pour.Step(feeding));
                }
            }

            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7 }, climbs);
        }

        [Fact]
        public void The_first_blue_step_starts_at_the_bottom()
        {
            CeilingFeedPour pour = new();
            Assert.Equal(0, pour.Step(feed: true));
        }

        [Fact]
        public void A_gap_longer_than_the_window_starts_a_new_pour()
        {
            CeilingFeedPour pour = new();
            Assert.Equal(0, pour.Step(feed: true));
            pour.Advance(INSIDE);
            Assert.Equal(1, pour.Step(feed: true));

            pour.Advance(OUTSIDE);
            Assert.Equal(0, pour.Step(feed: true));
        }

        [Fact]
        public void A_red_step_plays_at_its_own_pitch_and_leaves_the_pour_alone()
        {
            CeilingFeedPour pour = new();
            Assert.Equal(0, pour.Step(feed: true));
            pour.Advance(INSIDE);
            Assert.Equal(1, pour.Step(feed: true));

            //A pressure step between two feed steps: its own sound at its own pitch, and the pour goes on over it
            pour.Advance(INSIDE * 0.5f);
            Assert.Equal(0, pour.Step(feed: false));
            pour.Advance(INSIDE * 0.5f);
            Assert.Equal(2, pour.Step(feed: true));
        }

        [Fact]
        public void A_pause_holds_the_pour_however_long_it_lasts()
        {
            CeilingFeedPour pour = new();
            Assert.Equal(0, pour.Step(feed: true));
            pour.Advance(INSIDE);
            Assert.Equal(1, pour.Step(feed: true));

            //The pause page stops the screen dead: no frame reaches Advance while the wall clock runs on past the window.
            //A pour timed on the wall clock started over here (#726's review); this one goes on from where it was.
            Thread.Sleep((int)(CeilingFeedPour.WINDOW_SECONDS * 1000f) + 100);

            pour.Advance(INSIDE);
            Assert.Equal(2, pour.Step(feed: true));
        }

        [Fact]
        public void A_new_level_starts_with_no_pour_behind_it()
        {
            CeilingFeedPour pour = new();
            Assert.Equal(0, pour.Step(feed: true));
            pour.Advance(INSIDE);
            Assert.Equal(1, pour.Step(feed: true));

            pour.Reset();
            Assert.Equal(0, pour.Step(feed: true));
        }
    }
}
