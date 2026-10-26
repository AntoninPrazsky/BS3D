using BS3D.Screens;
using Prazsky.BS3D.GameStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What Help's ODD BALLS tab holds (#895): the per-kind completeness check the table's own comment had claimed since
    /// #633 and that did not exist, so the page drifted from ten of the thirteen kinds without a test noticing — the two
    /// the cannon holds, the rainbow ball and the Cutter, had no square (the owner's note of 2026-10-16). The one
    /// kind-by-kind test beside it (<c>TutorialTests.EveryKindAPlayerCanMeetHasACard</c>) covers the cards and skips the
    /// Cutter; this covers the page.
    /// </summary>
    public class OddBallCopyTests
    {
        /// <summary>Every kind a player can meet has its entry — every <see cref="BallKind"/> but <see cref="BallKind.Normal"/>, which needs none — and none has two.</summary>
        [Fact]
        public void EveryKindButNormalHasOneEntry()
        {
            BallKind[] wanted = Enum.GetValues<BallKind>().Where(kind => kind != BallKind.Normal).ToArray();
            BallKind[] held = OddBallCopy.Entries.Select(entry => entry.Kind).ToArray();

            BallKind[] missing = wanted.Except(held).ToArray();
            Assert.True(missing.Length == 0,
                "Help's ODD BALLS has no entry for " + string.Join(", ", missing) + ": add one to OddBallCopy.Entries, with its sentence.");

            Assert.DoesNotContain(BallKind.Normal, held);
            Assert.Equal(held.Length, held.Distinct().Count());
            Assert.Equal(wanted.Length, held.Length);
        }

        /// <summary>
        /// The two the cannon holds are the kinds <see cref="BallKinds.Shootable"/> names, sit after every kind of the
        /// cluster (the page puts its line between the two groups) and are drawn in their loaded pose; the cluster's are not.
        /// </summary>
        [Fact]
        public void TheKindsTheCannonHoldsComeLastAndAreTheShootableOnes()
        {
            OddBallCopy.Entry[] entries = OddBallCopy.Entries;

            HashSet<BallKind> inGun = entries.Where(entry => entry.InGun).Select(entry => entry.Kind).ToHashSet();
            Assert.Equal(new[] { BallKind.Wildcard, BallKind.Cutter }.ToHashSet(), inGun);
            Assert.Equal(BallKinds.ShootableKinds.ToHashSet(), inGun);

            int firstInGun = Array.FindIndex(entries, entry => entry.InGun);
            Assert.True(firstInGun > 0, "the cluster's kinds come first");
            Assert.All(entries.Skip(firstInGun), entry => Assert.True(entry.InGun, entry.Kind + " is the cluster's but stands among the cannon's"));
        }

        /// <summary>
        /// Every entry has a name of its own, short enough to be a heading, and says what the kind does in more than a phrase;
        /// the page's copy keeps the house typesetting rules (#999).
        /// </summary>
        [Fact]
        public void EveryEntryIsSetAndNamedLikeTheOthers()
        {
            Assert.Null(CopyRules.Fault(OddBallCopy.GunLead));

            foreach (OddBallCopy.Entry entry in OddBallCopy.Entries)
            {
                Assert.Null(CopyRules.Fault(entry.Text));
                Assert.Null(CopyRules.Fault(entry.Name));
                Assert.False(string.IsNullOrWhiteSpace(entry.Name), entry.Kind + " needs a name for its heading");
                Assert.True(entry.Name.Length <= 16, entry.Kind + "'s name is a heading, not a sentence");
                Assert.False(entry.Text.StartsWith(entry.Name, StringComparison.Ordinal), entry.Kind + "'s sentence repeats its name, which the heading already says");
                Assert.EndsWith(".", entry.Text);
                Assert.True(entry.Text.Length > 40, entry.Kind + "'s sentence is too short to explain it");
                Assert.True(entry.Colour != 0, entry.Kind + " needs a colour to be drawn in: a ball of colour zero is dropped in silence");
            }
        }

        /// <summary>The dead weight's entry (#1023), which is not a kind and stands outside the table, is held to the same.</summary>
        [Fact]
        public void TheDeadWeightsEntryIsSetLikeTheKinds()
        {
            Assert.Null(CopyRules.Fault(OddBallCopy.DeadLead));
            Assert.Null(CopyRules.Fault(OddBallCopy.DeadName));
            Assert.Null(CopyRules.Fault(OddBallCopy.DeadText));
            Assert.True(OddBallCopy.DeadName.Length <= 16, "the dead weight's name is a heading, not a sentence");
            Assert.False(OddBallCopy.DeadText.StartsWith(OddBallCopy.DeadName, StringComparison.Ordinal));
            Assert.EndsWith(".", OddBallCopy.DeadText);
            Assert.True(OddBallCopy.DeadText.Length > 40);
            Assert.True(OddBallCopy.DeadColour != 0, "a ball of colour zero is dropped in silence");
        }
    }
}
