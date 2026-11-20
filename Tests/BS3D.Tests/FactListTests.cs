using BS3D.Online;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// A list of labelled facts (<c>MenuPage.FactList</c>, the online privacy list on Settings and About) sets its labels
    /// in the text's own face, its bold cut, at the text's size and as written (#1154, the owner: not the display face's
    /// big capitals, "the same font as the text beside them, just bold"), each label on its text's first baseline.
    /// <para>
    /// The baseline is held with nothing moved, which rests on a fact of the two font files, held here: Inter's regular and
    /// bold cuts share their vertical tables, so at one size a line of each is as tall with its baseline as deep. A bold
    /// cut swapped for one of other metrics would stand its labels off their text's line with no error anywhere, and the
    /// test names the tables that differ. The page itself is built in a Myra tree no test can construct, so what the method
    /// sets its labels in is read from its source, as <see cref="ProseLabelsTests"/> reads the pages'.
    /// </para>
    /// <para>
    /// Seen to fail: the metrics with Anton put in for Inter Bold (every table differs), the source check on the method as
    /// #1034 wrote it (the display face, upper-cased), and the labels' check on a list with "SENT" in it.
    /// </para>
    /// </summary>
    public class FactListTests
    {
        [Fact]
        public void The_labels_bold_cut_stands_on_the_texts_baseline()
        {
            VerticalMetrics regular = VerticalMetrics.Read(FontPath("Inter-Regular.ttf"));
            VerticalMetrics bold = VerticalMetrics.Read(FontPath("Inter-Bold.ttf"));

            //FontStashSharp sizes a face by its ascent to descent; the baseline stands the ascent's share of that down
            Assert.True(regular.Equals(bold),
                $"Inter Bold's vertical metrics ({bold}) are not Inter Regular's ({regular}): MenuPage.FactList stands its "
                + "labels at the top of their row with no drop, so they would no longer sit on their text's first baseline");
        }

        [Fact]
        public void The_list_sets_its_labels_in_the_bold_cut_as_written()
        {
            string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "Game", "Screens", "MenuPage.cs"));

            //The method's body: from its signature to the first line that closes a member at its indentation
            Match method = Regex.Match(source, @"protected Grid FactList\(.*?\n        \}", RegexOptions.Singleline);
            Assert.True(method.Success, "MenuPage.FactList was not found; update this test with the method");
            string body = method.Value;

            Assert.Contains("Font = FontStrong", body);
            Assert.Contains("FontStrong.MeasureString(label)", body);
            Assert.DoesNotContain("FontBody", body);
            Assert.DoesNotContain("ToUpper", body);
        }

        [Fact]
        public void The_privacy_labels_are_written_as_they_are_drawn()
        {
            //Drawn as written since #1154: a capital, then lower case, as the text beside them is set
            List<string> faults = new();
            foreach ((string label, _) in OnlineScores.PrivacyList(new GameSettings(), phone: false))
            {
                bool sentence = label.Length > 1 && char.IsUpper(label[0]) && label.Substring(1) == label.Substring(1).ToLowerInvariant();
                if (!sentence) faults.Add(label);
            }

            Assert.True(faults.Count == 0, "Privacy labels not in sentence case: " + string.Join(", ", faults));
        }

        /// <summary>A face's vertical tables: what FontStashSharp places a line and its baseline by.</summary>
        private readonly record struct VerticalMetrics(int UnitsPerEm, int Ascender, int Descender, int LineGap,
            int TypoAscender, int TypoDescender, int TypoLineGap, int WinAscent, int WinDescent)
        {
            public static VerticalMetrics Read(string path)
            {
                byte[] font = File.ReadAllBytes(path);
                int head = Table(font, "head"), hhea = Table(font, "hhea"), os2 = Table(font, "OS/2");

                return new VerticalMetrics(
                    UInt16(font, head + 18),
                    Int16(font, hhea + 4), Int16(font, hhea + 6), Int16(font, hhea + 8),
                    Int16(font, os2 + 68), Int16(font, os2 + 70), Int16(font, os2 + 72),
                    UInt16(font, os2 + 74), UInt16(font, os2 + 76));
            }

            //The table directory: a count at 4, then 16 bytes a table (tag, checksum, offset, length) from 12
            private static int Table(byte[] font, string tag)
            {
                int count = UInt16(font, 4);
                for (int i = 0; i < count; i++)
                {
                    int entry = 12 + 16 * i;
                    if (System.Text.Encoding.ASCII.GetString(font, entry, 4) == tag)
                        return (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(entry + 8, 4));
                }

                throw new InvalidDataException($"No '{tag}' table in the font");
            }

            private static int UInt16(byte[] font, int at) => BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at, 2));

            private static int Int16(byte[] font, int at) => BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(at, 2));
        }

        private static string FontPath(string file) => Path.Combine(RepositoryRoot(), "Game", "Content", "Fonts", file);

        private static string RepositoryRoot()
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Game", "Content", "Fonts", "Inter-Bold.ttf")))
                    return directory.FullName;
            }

            throw new DirectoryNotFoundException("No repository root (Game/Content/Fonts/Inter-Bold.ttf) was found above the test assembly.");
        }
    }
}
