using BS3D.Screens;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The pause page's debug row (#876): which kinds it has a button for, that a release has no row and loads nothing, and what a
    /// press does to the magazine. The page's widgets and the session's log line need a window and were checked in the running game;
    /// the rules the row is made of are <see cref="DebugKindRow"/>'s and are held here.
    /// </summary>
    public class DebugKindRowTests
    {
        //A tag build's word and a suffixed one's: what BuildVersion.Release holds out of a release (#546)
        private static readonly string[] RELEASES = ["v0.3.5", "v0.4.0-rc1"];

        private static Magazine Build()
        {
            int dealt = 0;
            return new Magazine(() => (BallType)(dealt++ % BallTypes.Count + 1));
        }

        /// <summary>
        /// The kinds a shot can carry, found the way the game finds them out rather than by asking the predicate the row is
        /// built from: the map's one placement door turns a kind that cannot hang in a cluster into an ordinary ball, so a kind it
        /// changes is one that exists only on the gun's side. Another derivation of the same set, which is what makes the
        /// comparison below mean something.
        /// </summary>
        private static HashSet<BallKind> KindsTheMapRefusesToHang()
        {
            HashSet<BallKind> refused = [];
            BallsMap map = new(4, 4, 4);

            foreach (BallKind kind in Enum.GetValues<BallKind>())
                if (map.PutBallAt(1, 1, 1, BallType.Type4, kind).Kind != kind) refused.Add(kind);

            return refused;
        }

        [Fact]
        public void TheRowHasAButtonForEveryShootableKindAndNoOtherInADevBuild()
        {
            HashSet<BallKind> shootable = KindsTheMapRefusesToHang();
            IReadOnlyList<BallKind> row = DebugKindRow.Kinds(null);

            Assert.Equal(shootable.OrderBy(k => k), row.OrderBy(k => k));
            Assert.Equal(row.Count, row.Distinct().Count());

            //The two the game fires today, so that an empty answer from both derivations at once cannot pass
            Assert.Contains(BallKind.Wildcard, row);
            Assert.Contains(BallKind.Cutter, row);
        }

        [Fact]
        public void NoKindALevelPlacesHasAButton()
        {
            foreach (BallKind kind in Enum.GetValues<BallKind>().Where(BallKinds.InCluster))
                Assert.DoesNotContain(kind, DebugKindRow.Kinds(null));
        }

        [Fact]
        public void AReleaseDrawsNoRow()
        {
            Assert.True(DebugKindRow.Available(null));

            foreach (string release in RELEASES)
            {
                Assert.False(DebugKindRow.Available(release));
                Assert.Empty(DebugKindRow.Kinds(release));
            }
        }

        [Fact]
        public void AReleaseLoadsNothingEvenWhenAskedDirectly()
        {
            foreach (string release in RELEASES)
                foreach (BallKind kind in BallKinds.ShootableKinds)
                {
                    Magazine magazine = Build();

                    Assert.False(DebugKindRow.Load(release, kind, magazine));

                    for (int slot = 0; slot < Magazine.SIZE; slot++) Assert.Equal(BallKind.Normal, magazine.Slot(slot).Kind);
                }
        }

        [Fact]
        public void ADevBuildLoadsTheKindIntoTheBoreAndChangesNothingElse()
        {
            foreach (BallKind kind in DebugKindRow.Kinds(null))
            {
                Magazine magazine = Build();
                MagazineSlot[] before = Enumerable.Range(0, Magazine.SIZE).Select(magazine.Slot).ToArray();

                Assert.True(DebugKindRow.Load(null, kind, magazine));

                Assert.Equal(kind, magazine.Slot(0).Kind);

                //The colour, the dissolve and every slot behind it are as they were: the next round is the one that changed
                Assert.Equal(before[0].Type, magazine.Slot(0).Type);
                Assert.Equal(before[0].Transmute, magazine.Slot(0).Transmute);
                for (int slot = 1; slot < Magazine.SIZE; slot++) Assert.Equal(before[slot], magazine.Slot(slot));
            }
        }

        [Fact]
        public void AKindALevelPlacesIsRefusedBecauseAShotWouldLoseIt()
        {
            foreach (BallKind kind in Enum.GetValues<BallKind>().Where(BallKinds.InCluster))
            {
                Magazine magazine = Build();

                Assert.False(DebugKindRow.Load(null, kind, magazine));
                Assert.Equal(BallKind.Normal, magazine.Slot(0).Kind);
            }
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 2)]
        [InlineData(4, 2)]
        [InlineData(9, 2)]
        public void TheRowIsNeverMoreThanTwoButtonsToALine(int kinds, int perLine)
        {
            Assert.Equal(perLine, DebugKindRow.PerLine(kinds));
        }

        [Fact]
        public void EveryButtonSaysPlainlyWhatItLoads()
        {
            Assert.Equal("Cutter", DebugKindRow.Name(BallKind.Cutter));
            Assert.Equal("Wildcard", DebugKindRow.Name(BallKind.Wildcard));
            Assert.Equal("ordinary", DebugKindRow.Describe(BallKind.Normal));
            Assert.Equal("Cutter", DebugKindRow.Describe(BallKind.Cutter));
            Assert.Contains("DEBUG", DebugKindRow.CAPTION);
        }
    }
}
