using BS3D.Screens;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The benchmark's consent (#1041): a run starts only with online scores on, off and never answered alike refuse it,
    /// and the play alone (<c>benchmarkplay</c>), which sends nothing, is not held to it; the page's copy is set by the
    /// typesetting rules (#806).
    /// </summary>
    public class BenchmarkConsentTests
    {
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(null, false)]
        public void A_run_starts_only_with_online_scores_on(bool? online, bool runs)
        {
            Assert.Equal(runs, BenchmarkConsent.MayRun(online, playOnly: false));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public void The_play_alone_sends_nothing_and_is_not_held_to_it(bool? online)
        {
            Assert.True(BenchmarkConsent.MayRun(online, playOnly: true));
        }

        [Theory]
        [InlineData(BenchmarkConsent.SENT_NOTE)]
        [InlineData(BenchmarkConsent.OFFLINE_NOTE)]
        public void The_copy_is_set_by_the_rules(string copy)
        {
            Assert.Null(CopyRules.Fault(copy));
        }
    }
}
