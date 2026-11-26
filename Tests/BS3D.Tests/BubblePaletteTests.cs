using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The colours the bubble draws as one in a cluster (#1194): <b>no bubble level hangs brown beside black or orange</b>.
    /// <para>
    /// The bubble draws the spots figure set (<see cref="ColourCues.SetOf"/>), where brown's twelve dots are a paler brown, an
    /// orange-brown, on a ground darkened to <see cref="ColourCues.GROUND_MIN"/> to pay for them, so nearly black, and the
    /// orange's four spots are the orange darkened, a brown. Each carries the other's colour. Measured on Amphora in the Game
    /// (close-up of the foot and the belly, 1920x1080, High, the median pixel of each ball as its ground, CIEDE2000):
    /// brown's ground 16.2 from black's with the figure and 31.6 without it, and brown's dots 15.8 from orange's ground. The
    /// colour rig (<c>Thirteen_Colors</c>, a lone row against the sky) reads the same three 28.1, 35.7 and 43.2 apart, whole
    /// disc: a film shows what is behind it, and in a cluster that is the other balls. The owner could not tell the three
    /// apart on Amphora, which hung all of them; its ends are blue since.
    /// </para>
    /// <para>
    /// Brown takes the darker ink since #1247 (<see cref="ColourCueInversionTests"/>), so its ground is no longer darkened and
    /// its dots are no longer an orange: both causes are gone. The rule stays until a bubble level hanging those pairs has
    /// been measured in a cluster again.
    /// </para>
    /// </summary>
    public class BubblePaletteTests
    {
        /// <summary>The pairs the bubble merges, each measured as above.</summary>
        internal static readonly (BallType A, BallType B)[] MergedOnTheBubble =
        {
            (BallType.Type10, BallType.Type8), //brown, black: the brown's ground is darkened nearly to black
            (BallType.Type10, BallType.Type9), //brown, orange: the brown's dots are an orange-brown, the orange's spots a brown
        };

        public static TheoryData<string> BubbleLevels()
        {
            TheoryData<string> data = new();
            foreach (string path in Shipped.LevelFiles())
                if (Level.Load(path).Balls == BallStyle.Bubble) data.Add(Path.GetFileName(path));

            return data;
        }

        [Fact]
        public void TheMeadowHangsBubbles()
        {
            //The theory below asks nothing of a level that is not a bubble one, so it must be given some to ask
            Assert.True(BubbleLevels().Count >= 10, "fewer than a chapter of bubble levels was found");
        }

        [Theory]
        [MemberData(nameof(BubbleLevels))]
        public void ABubbleLevelHangsNoPairTheBubbleMerges(string file)
        {
            Assert.Empty(MergedPairs(Colours(Level.Load(Shipped.Level(file)))));
        }

        /// <summary>The check fires: Amphora's palette as it was before #1194, brown at the foot and the neck.</summary>
        [Fact]
        public void TheCheckFindsAmphorasOldEnds()
        {
            HashSet<BallType> old = new() { BallType.Type1, BallType.Type4, BallType.Type8, BallType.Type9, BallType.Type10 };

            Assert.Equal(new[] { "Type10/Type8", "Type10/Type9" }, MergedPairs(old));
        }

        private static HashSet<BallType> Colours(Level level)
        {
            HashSet<BallType> colours = new();
            foreach (StaticBall ball in new BallsMap(level.Map).GetStaticBallsArray())
                if (ball != null) colours.Add(ball.Type);

            return colours;
        }

        private static List<string> MergedPairs(HashSet<BallType> colours) =>
            MergedOnTheBubble.Where(p => colours.Contains(p.A) && colours.Contains(p.B))
                .Select(p => $"{p.A}/{p.B}")
                .ToList();
    }
}
