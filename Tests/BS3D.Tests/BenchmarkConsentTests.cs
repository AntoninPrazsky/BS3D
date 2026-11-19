using BS3D.Screens;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The benchmark's consent (#1041): a run starts only with online scores on, off and never answered alike refuse it,
    /// and the play alone (<c>benchmarkplay</c>), which sends nothing, is not held to it; the page's copy is set by the
    /// typesetting rules (#806), and on a phone calls the machine a phone and a tap what stops a run (#1183).
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
        [InlineData(false)]
        [InlineData(true)]
        public void The_copy_is_set_by_the_rules(bool phone)
        {
            Assert.Null(CopyRules.Fault(BenchmarkConsent.Intro(4, "High", phone)));
            Assert.Null(CopyRules.Fault(BenchmarkConsent.SentNote(phone)));
            Assert.Null(CopyRules.Fault(BenchmarkConsent.Sent(phone)));
            Assert.Null(CopyRules.Fault(BenchmarkConsent.OFFLINE_NOTE));
        }

        [Fact]
        public void On_a_phone_the_page_names_a_phone_and_a_tap()
        {
            foreach (string copy in new[] { BenchmarkConsent.Intro(4, "High", phone: true), BenchmarkConsent.SentNote(phone: true),
                BenchmarkConsent.Sent(phone: true) })
            {
                Assert.DoesNotContain("computer", copy, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("key", copy, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("graphics card", copy, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("phone", copy, StringComparison.Ordinal);
            }

            Assert.EndsWith("A tap stops it.", BenchmarkConsent.Intro(4, "High", phone: true), StringComparison.Ordinal);
        }

        [Fact]
        public void On_a_computer_it_names_the_computer_and_the_skip_key()
        {
            string intro = BenchmarkConsent.Intro(4, "High", phone: false);
            Assert.Contains("your computer can no longer draw it", intro, StringComparison.Ordinal);
            Assert.EndsWith("The skip key stops it.", intro, StringComparison.Ordinal);
            Assert.Contains("4 of the most demanding scenes at your quality (High)", intro, StringComparison.Ordinal);
            Assert.Contains("computer’s specification", BenchmarkConsent.SentNote(phone: false), StringComparison.Ordinal);
            Assert.DoesNotContain("phone", BenchmarkConsent.SentNote(phone: false), StringComparison.Ordinal);
        }
    }
}
