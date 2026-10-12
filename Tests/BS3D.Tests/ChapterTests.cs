using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The campaign's chapters as the game loads them (#194): a block is one scene, one dome, one music family and one
    /// ball material, and each is written into every level file of it, so the music, the backdrop and the balls change
    /// when the chapter does and not when the level does. <c>Tools/LevelGen</c> states them once per block and prints a
    /// disagreement it reads back off the files (<c>CampaignSet.DescribeBlock</c>), refusing nothing; this refuses it.
    /// <para>
    /// <b>Every level can be held to it since #849</b>, when Colossus, the one hand-drawn level, became a design: its
    /// scene and its music were authored in its own file, and a file that names no family falls back to the set's
    /// positional rotation and plays whatever its place in the set gives.
    /// </para>
    /// </summary>
    public class ChapterTests
    {
        private static LevelSet ShippedSet() =>
            LevelSet.Load(Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));

        [Fact]
        public void EveryChapterIsOneSceneOneDomeOneFamilyAndOneMaterial()
        {
            LevelSet set = ShippedSet();
            Assert.True(set.HasBlocks, "the shipped set names no blocks");

            List<string> faults = new();

            for (int first = 0; first < set.Count;)
            {
                set.BlockRange(first, out _, out int last);

                Level opener = Level.Load(set.ResolvePath(first));
                for (int i = first + 1; i <= last; i++)
                {
                    Level level = Level.Load(set.ResolvePath(i));
                    string where = $"'{set.BlockName(i)}': {set.Levels[i].File}";

                    if (level.Scene != opener.Scene) faults.Add($"{where} plays {level.Scene}, the chapter {opener.Scene}");
                    if (level.SkyDome != opener.SkyDome) faults.Add($"{where} names dome {level.SkyDome}, the chapter {opener.SkyDome}");
                    if (!string.Equals(level.Music, opener.Music, StringComparison.OrdinalIgnoreCase))
                        faults.Add($"{where} plays '{level.Music ?? "(the rotation)"}', the chapter '{opener.Music ?? "(the rotation)"}'");
                    if ((level.Balls ?? BallStyle.Beach) != (opener.Balls ?? BallStyle.Beach))
                        faults.Add($"{where} hangs {level.Balls ?? BallStyle.Beach} balls, the chapter {opener.Balls ?? BallStyle.Beach}");
                }

                first = last + 1;
            }

            Assert.True(faults.Count == 0, string.Join(Environment.NewLine, faults));
        }

        /// <summary>
        /// And the family each level names is one the game has recordings of: a name with no files behind it falls back
        /// to the positional rotation as quietly as no name at all (<c>GameMusic.SetTheme</c>), grouping the recordings in
        /// <c>Game/Music</c> by the name before the first dash, as the game does.
        /// </summary>
        [Fact]
        public void EveryLevelNamesAFamilyThatHasRecordings()
        {
            LevelSet set = ShippedSet();
            string music = Path.Combine(Path.GetDirectoryName(Shipped.LevelsDirectory), "Music");
            Assert.True(Directory.Exists(music), $"no music folder at {music}");

            HashSet<string> families = new(
                Directory.GetFiles(music, "*.ogg").Select(file =>
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    int dash = stem.IndexOf('-');
                    return dash < 0 ? stem : stem.Substring(0, dash);
                }),
                StringComparer.OrdinalIgnoreCase);

            List<string> faults = new();
            for (int i = 0; i < set.Count; i++)
            {
                string named = Level.Load(set.ResolvePath(i)).Music;
                if (string.IsNullOrWhiteSpace(named)) faults.Add($"{set.Levels[i].File} names no music");
                else if (!families.Contains(named.Split('-')[0])) faults.Add($"{set.Levels[i].File} names '{named}', which has no recordings");
            }

            Assert.True(faults.Count == 0, string.Join(Environment.NewLine, faults));
        }
    }
}
