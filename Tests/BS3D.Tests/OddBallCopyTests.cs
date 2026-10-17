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

        /// <summary>Every entry names its kind in its first word and says something; the page's copy keeps the house typesetting rules.</summary>
        [Fact]
        public void EveryEntryIsSetAndNamedLikeTheOthers()
        {
            Assert.Null(CopyRules.Fault(OddBallCopy.GunLead));

            foreach (OddBallCopy.Entry entry in OddBallCopy.Entries)
            {
                Assert.Null(CopyRules.Fault(entry.Text));
                Assert.Contains(" — ", entry.Text);
                Assert.EndsWith(".", entry.Text);
                Assert.True(entry.Text.Length > 60, entry.Kind + "'s sentence is too short to explain it");
                Assert.True(entry.Colour != 0, entry.Kind + " needs a colour to be drawn in: a ball of colour zero is dropped in silence");
            }
        }
    }
}
