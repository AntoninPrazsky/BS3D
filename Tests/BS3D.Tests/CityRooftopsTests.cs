using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The city's roof dressing (#436) holds what <c>CityRooftops.Dress</c> promises: a roof is split into cells and each
    /// piece takes one, so no two pieces of one roof stand in each other. #850: the neon city's extras were dressed on a
    /// cell grid of their own, and stood a second, shorter mast inside the day's mast (whose middle cell both calls take),
    /// a dish or a 5G pole inside another, wherever the two calls rolled the same cell. Held over the real layout
    /// (<c>CityRooftops.ArrangePieces</c>, no device) on both cities and a dozen rolls, the neon extras included in the
    /// day city too (built there, though only the neon city draws them).
    /// <para>
    /// #902: the masts' beacons flash in clusters, each on a clock and in a red of its own, instead of the whole city's on
    /// one. Held here: a mast's cluster is a function of its foot's coarse cell and of nothing else (so the roof's random
    /// stream is untouched and no mast moved), every cluster is used, any two flash out of step, every period keeps the
    /// rooftop intro's three flashes in five seconds, and every red still clears the glare.
    /// </para>
    /// </summary>
    public class CityRooftopsTests
    {
        private const int ROLLS = 12;

        //The city and its seeds offset by the roll the way the game adds its scene seed to both (CityIntroShotsTests)
        private static CityRooftops.RoofPiece[] Pieces(bool neon, int roll, out City city)
        {
            CitySceneConfig config = ConfigOf(roll);

            city = new City(config, neon, ArenaIsland.RADIUS);
            return CityRooftops.ArrangePieces(city, config, CityRooftops.DEFAULT_SEED + roll);
        }

        private static CitySceneConfig ConfigOf(int roll)
        {
            var config = new CitySceneConfig();
            config.Seed += roll;
            config.NeonLayout.Seed += roll;
            return config;
        }

        [Fact]
        public void NoTwoPiecesOfOneRoofShareACell()
        {
            int pieces = 0, extras = 0, shared = 0;
            string example = null;

            for (int roll = 0; roll < ROLLS; roll++)
                foreach (bool neon in new[] { false, true })
                {
                    //A piece's foot is the middle of the cell it took, so two pieces in one cell stand on one foot - and
                    //two roofs never share a cell's middle (the towers' footprints do not overlap, and a setback's tiers
                    //share a centre but not a roof height)
                    var feet = new Dictionary<Vector3, CityRooftops.RoofPiece>();
                    foreach (CityRooftops.RoofPiece piece in Pieces(neon, roll, out _))
                    {
                        pieces++;
                        if (piece.Neon) extras++;

                        if (!feet.TryGetValue(piece.Foot, out CityRooftops.RoofPiece first))
                        {
                            feet.Add(piece.Foot, piece);
                            continue;
                        }

                        shared++;
                        example ??= $"roll {roll}, the {(neon ? "neon" : "day")} city: {Name(piece)} stands in the cell of {Name(first)} at {piece.Foot}";
                    }
                }

            //Not vacuous: the layout was dressed, and the neon extras were among it
            Assert.True(pieces >= ROLLS * 2 * 5000, $"only {pieces} pieces were dressed");
            Assert.True(extras >= ROLLS * 2 * 1000, $"only {extras} of the pieces were the neon city's extras");
            Assert.True(shared == 0, $"{shared} of {pieces} pieces stand in a cell another piece of their roof took; the first: {example}");
        }

        //#902 ----------------------------------------------------------------------------------------------------------

        //What the layout's pieces hashed to on the commit before #902 (origin/main c6d12638), four rolls of each city: the
        //kind, the foot's three floats and the neon flag of every piece in order, FNV-1a. A beacon cluster drawn from the
        //roof's random stream would have shifted every later roll and with them most of these
        private const int FINGERPRINT_ROLLS = 4;
        private const int DAY_PIECES = 29056, NEON_PIECES = 38003;
        private const uint DAY_FINGERPRINT = 3123092905u, NEON_FINGERPRINT = 4000610961u;

        [Fact]
        public void SplittingTheBeaconsMovedNoPieceOfTheLayout()
        {
            foreach (bool neon in new[] { false, true })
            {
                uint h = 2166136261u;
                int pieces = 0;
                for (int roll = 0; roll < FINGERPRINT_ROLLS; roll++)
                    foreach (CityRooftops.RoofPiece piece in Pieces(neon, roll, out _))
                    {
                        pieces++;
                        unchecked
                        {
                            foreach (char c in piece.Kind) h = (h ^ c) * 16777619u;
                            h = (h ^ (uint)BitConverter.SingleToInt32Bits(piece.Foot.X)) * 16777619u;
                            h = (h ^ (uint)BitConverter.SingleToInt32Bits(piece.Foot.Y)) * 16777619u;
                            h = (h ^ (uint)BitConverter.SingleToInt32Bits(piece.Foot.Z)) * 16777619u;
                            h = (h ^ (piece.Neon ? 1u : 0u)) * 16777619u;
                        }
                    }

                Assert.Equal(neon ? NEON_PIECES : DAY_PIECES, pieces);
                Assert.Equal(neon ? NEON_FINGERPRINT : DAY_FINGERPRINT, h);
            }
        }

        [Fact]
        public void EveryMastsBeaconFlashesWithTheClusterItsFootHashesTo()
        {
            var perCluster = new int[2, CityRooftops.BEACON_CLUSTERS];
            int masts = 0;

            for (int roll = 0; roll < ROLLS; roll++)
                for (int n = 0; n < 2; n++)
                {
                    bool neon = n == 1;
                    int seed = CityRooftops.DEFAULT_SEED + roll;
                    CityRooftops.RoofPiece[] pieces = Pieces(neon, roll, out City city);
                    CityRooftops.RoofPiece[] beacons = CityRooftops.ArrangeBeacons(city, ConfigOf(roll), seed);

                    //A beacon stands over its mast's foot (the same X and Z, to the bit; two roofs of one tower's setbacks may
                    //share one) and is in the cluster that foot hashes to
                    var beaconFeet = new List<(float, float)>();
                    foreach (CityRooftops.RoofPiece beacon in beacons)
                    {
                        int cluster = int.Parse(beacon.Kind.Substring("Beacon".Length));
                        Assert.Equal(CityRooftops.BeaconCluster(beacon.Foot.X, beacon.Foot.Z, seed), cluster);
                        beaconFeet.Add((beacon.Foot.X, beacon.Foot.Z));
                        perCluster[n, cluster]++;
                    }

                    //...and every mast has exactly one beacon, none is left over
                    var mastFeet = new List<(float, float)>();
                    foreach (CityRooftops.RoofPiece mast in pieces)
                        if (mast.Kind == "MastRed") mastFeet.Add((mast.Foot.X, mast.Foot.Z));

                    masts += mastFeet.Count;
                    mastFeet.Sort();
                    beaconFeet.Sort();
                    Assert.Equal(mastFeet, beaconFeet);
                }

            Assert.True(masts >= ROLLS * 2 * 200, $"only {masts} masts were dressed");

            //Every cluster is used, and none holds a quarter of the city's: a hash that sent most masts to one cluster would
            //leave most of the city flashing in step again
            for (int n = 0; n < 2; n++)
            {
                int total = 0;
                for (int c = 0; c < CityRooftops.BEACON_CLUSTERS; c++) total += perCluster[n, c];

                for (int c = 0; c < CityRooftops.BEACON_CLUSTERS; c++)
                {
                    double share = perCluster[n, c] / (double)total;
                    Assert.True(share > 0.06 && share < 0.25,
                        $"the {(n == 1 ? "neon" : "day")} city sent {share:P0} of its {total} beacons to cluster {c}");
                }
            }
        }

        [Fact]
        public void NeighbouringTowersShareACluster()
        {
            const int seed = CityRooftops.DEFAULT_SEED;
            var random = new Random(902);
            var seen = new HashSet<int>();

            for (int cell = 0; cell < 400; cell++)
            {
                float originX = random.Next(-12, 12) * CityRooftops.BEACON_CELL, originZ = random.Next(-12, 12) * CityRooftops.BEACON_CELL;
                int expected = CityRooftops.BeaconCluster(originX + 0.01f, originZ + 0.01f, seed);
                seen.Add(expected);

                //Every point of one cell, wherever in it a foot stands, is the same cluster
                for (int point = 0; point < 20; point++)
                {
                    float x = originX + 0.01f + (float)random.NextDouble() * (CityRooftops.BEACON_CELL - 0.02f);
                    float z = originZ + 0.01f + (float)random.NextDouble() * (CityRooftops.BEACON_CELL - 0.02f);
                    Assert.Equal(expected, CityRooftops.BeaconCluster(x, z, seed));
                }
            }

            //And the cells are not all one answer
            Assert.Equal(CityRooftops.BEACON_CLUSTERS, seen.Count);
        }

        [Fact]
        public void AnyTwoClustersFlashOutOfStep()
        {
            float period = new RooftopConfig().BeaconPeriod;

            //At one clock value the clusters show different things. Before #902 the whole city was one clock: whenever a
            //beacon was lit, every beacon was. Now, of the instants at which any cluster is lit, the ones with all of them
            //lit at once are a sliver and most have some lit and some dark
            int anyLit = 0, allLit = 0, mixed = 0;
            for (float t = 0f; t < 120f; t += 0.01f)
            {
                int lit = 0;
                for (int c = 0; c < CityRooftops.BEACON_CLUSTERS; c++)
                    if (CityRooftops.BeaconFlash(c, t, period) > 0f) lit++;

                if (lit > 0) anyLit++;
                if (lit == CityRooftops.BEACON_CLUSTERS) allLit++;
                if (lit > 0 && lit < CityRooftops.BEACON_CLUSTERS) mixed++;
            }

            Assert.True(anyLit > 1000, $"only {anyLit} instants have a beacon lit");
            Assert.True(allLit < anyLit * 0.02, $"all {CityRooftops.BEACON_CLUSTERS} clusters are lit together at {allLit} of the {anyLit} instants any is");
            Assert.True(mixed > anyLit * 0.9, $"only {mixed} of the {anyLit} instants have some clusters lit and some dark");

            //And no two of them are one clock shifted: over a minute, each pair has instants where one is at its flash and
            //the other dark, and the other way round, over and over
            for (int a = 0; a < CityRooftops.BEACON_CLUSTERS; a++)
                for (int b = a + 1; b < CityRooftops.BEACON_CLUSTERS; b++)
                {
                    int aOnly = 0, bOnly = 0;
                    for (float t = 0f; t < 60f; t += 0.02f)
                    {
                        bool litA = CityRooftops.BeaconFlash(a, t, period) > 0.5f, litB = CityRooftops.BeaconFlash(b, t, period) > 0.5f;
                        if (litA && !litB) aOnly++;
                        if (litB && !litA) bOnly++;
                    }

                    Assert.True(aOnly > 100 && bOnly > 100, $"clusters {a} and {b}: {aOnly} instants only the first is lit, {bOnly} only the second");
                }
        }

        [Fact]
        public void ClusterZeroIsTheBeaconTheCityHadBefore()
        {
            var config = new RooftopConfig();

            //One red, one period of 1.6 s and a flash from the clock's zero: what every beacon was before #902
            Assert.Equal(new Vector3(1f, 0.08f, 0.03f), CityRooftops.BeaconRed(0));
            Assert.Equal(config.BeaconPeriod, CityRooftops.BeaconPeriodOf(0, config.BeaconPeriod));
            Assert.Equal(0f, CityRooftops.BeaconFlash(0, 0f, config.BeaconPeriod));
            Assert.Equal(1f, CityRooftops.BeaconFlash(0, config.BeaconPeriod * 0.11f, config.BeaconPeriod), 4);
            Assert.Equal(0f, CityRooftops.BeaconFlash(0, config.BeaconPeriod * 0.5f, config.BeaconPeriod));
        }

        [Fact]
        public void EveryClusterKeepsTheRooftopIntrosThreeFlashesInFiveSeconds()
        {
            //chapter-intro-prologues.md: the rooftop shot is five seconds and "holds at least three flashes wherever the
            //clock stands", which is true of a period up to 5/3 s. Counted, not computed: the flashes' peaks that fall in
            //each window of five seconds, over the first forty seconds of the clock
            float period = new RooftopConfig().BeaconPeriod;
            const float STEP = 0.002f;

            for (int c = 0; c < CityRooftops.BEACON_CLUSTERS; c++)
            {
                float length = CityRooftops.BeaconPeriodOf(c, period);
                Assert.True(length <= 5f / 3f, $"cluster {c}'s period is {length} s");
                Assert.True(length >= 1.1f, $"cluster {c}'s period is {length} s: too quick a flicker for a warning light");

                var peaks = new List<float>();
                float before = 0f, now = CityRooftops.BeaconFlash(c, 0f, period);
                for (int i = 1; i < 20000; i++)
                {
                    float next = CityRooftops.BeaconFlash(c, (i + 1) * STEP, period);
                    if (now >= before && now > next && now > 0.9f) peaks.Add(i * STEP);
                    before = now;
                    now = next;
                }

                Assert.True(peaks.Count >= 20, $"cluster {c} peaked {peaks.Count} times in forty seconds");

                int fewest = int.MaxValue;
                for (float from = 0f; from < 12f; from += 0.01f)
                {
                    int inWindow = 0;
                    foreach (float peak in peaks)
                        if (peak >= from && peak < from + 5f) inWindow++;

                    fewest = Math.Min(fewest, inWindow);
                }

                Assert.True(fewest >= 3, $"cluster {c} (period {length} s) peaks {fewest} times in the worst five seconds");
            }
        }

        [Fact]
        public void EveryClusterHasItsOwnPeriodPhaseAndRedAndStillBlooms()
        {
            var config = new RooftopConfig();
            var seen = new HashSet<(float, float, float)>();

            for (int c = 0; c < CityRooftops.BEACON_CLUSTERS; c++)
            {
                Vector3 red = CityRooftops.BeaconRed(c);
                float period = CityRooftops.BeaconPeriodOf(c, config.BeaconPeriod);
                Assert.True(seen.Add((red.Y, period, CityRooftops.BeaconFlash(c, 0.05f, config.BeaconPeriod))),
                    $"cluster {c} repeats another's period, red and phase");

                //The shade stays a red: the green between a deep red and an orange-red, the blue the same
                Assert.True(red.Y >= 0.03f && red.Y <= 0.16f, $"cluster {c}'s green is {red.Y}");
                Assert.Equal(1f, red.X);
                Assert.Equal(0.03f, red.Z);

                //The peak clears GLARE_THRESHOLD (0.55 on luminance, each executable's own constant) with room, as the one
                //beacon of before did (0.87), or it does not bloom
                Vector3 peak = red * config.BeaconBrightness;
                float luminance = 0.2126f * peak.X + 0.7152f * peak.Y + 0.0722f * peak.Z;
                Assert.True(luminance > 0.55f * 1.25f, $"cluster {c}'s flash peaks at luminance {luminance}, too near the glare threshold");
            }

            //The reds really differ: the greens span most of the 0.03 to 0.16 the issue named
            float lowest = 1f, highest = 0f;
            for (int c = 0; c < CityRooftops.BEACON_CLUSTERS; c++)
            {
                lowest = Math.Min(lowest, CityRooftops.BeaconRed(c).Y);
                highest = Math.Max(highest, CityRooftops.BeaconRed(c).Y);
            }

            Assert.True(highest - lowest >= 0.12f, $"the greens only span {lowest} to {highest}");
        }

        private static string Name(CityRooftops.RoofPiece piece) => (piece.Neon ? "the neon extras' " : "the day's ") + piece.Kind;
    }
}
