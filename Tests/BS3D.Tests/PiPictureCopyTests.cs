using Prazsky.BS3D.Levels;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// What the Raspberry Pi build's picture lacks and keeps is a sentence two shipped places print (#947): the website's
    /// download steps and the release notes' Pi section, which is pasted into every release page. It said "no scenery
    /// behind the island" for a month after the Potato path began drawing the backdrops (#829), on the page of a release
    /// whose own change list said the opposite. So the sentence is stated <b>once</b>, as <c>piPicture</c> in
    /// <c>Web/src/_data/site.json</c>; <c>index.njk</c> prints it and <c>release.yml</c> reads it into a placeholder
    /// (<c>@@PI_PICTURE@@</c>), and these tests refuse a hand-typed copy, a placeholder that is gone and the one claim
    /// that ages without anyone touching the sentence: that EVERY chapter keeps its scenery, which stops being true the
    /// day a chapter is set in a scene <c>PotatoBackdrop.For</c> has no arm for.
    /// </summary>
    public class PiPictureCopyTests
    {
        private const string Placeholder = "@@PI_PICTURE@@";

        //Phrases that were once the sentence and are false now (or say it by hand): none may be typed into the site or the notes
        private static readonly string[] Stale = { "no scenery", "no shadows" };

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        private static string Text(params string[] relative) => File.ReadAllText(FindUnderRepository(Path.Combine(relative)));

        private static string PiPicture()
        {
            using JsonDocument site = JsonDocument.Parse(Text("Web", "src", "_data", "site.json"));
            Assert.True(site.RootElement.TryGetProperty("piPicture", out JsonElement picture), "site.json has no piPicture");
            return picture.GetString();
        }

        /// <summary>
        /// The kinds a shipped chapter is set in that <c>PotatoBackdrop.For</c> (the Potato path's backdrop, which the
        /// Raspberry Pi build draws) has no arm for: the arms are read from the method's text, up to its fall-through.
        /// </summary>
        internal static List<SceneKind> WithoutPotatoBackdrop(IEnumerable<SceneKind> scenes, string potatoBackdropSource)
        {
            int start = potatoBackdropSource.IndexOf("public static PotatoBackdrop For(", StringComparison.Ordinal);
            Assert.True(start >= 0, "PotatoBackdrop.For was not found; this check reads its arms and must be updated with it");
            int end = potatoBackdropSource.IndexOf("_ => null", start, StringComparison.Ordinal);
            Assert.True(end > start, "PotatoBackdrop.For no longer ends in a fall-through to null; this check reads its arms");

            HashSet<string> armed = Regex.Matches(potatoBackdropSource.Substring(start, end - start), @"SceneKind\.(\w+)(?:\s+when\b[^\r\n]*?)?\s*=>")
                .Select(m => m.Groups[1].Value).ToHashSet();
            Assert.NotEmpty(armed);
            return scenes.Distinct().Where(scene => !armed.Contains(scene.ToString())).OrderBy(scene => scene).ToList();
        }

        [Fact]
        public void TheSentenceIsOneLineOfPlainWords()
        {
            string picture = PiPicture();
            Assert.False(string.IsNullOrWhiteSpace(picture));
            Assert.DoesNotContain('\n', picture);
            //It sits inside a sentence on the page and inside a Markdown paragraph in the notes: no markup, no placeholder syntax
            Assert.DoesNotContain(picture, c => "<>*`@{}|".Contains(c));
            //and it states what it keeps as well as what it lacks, so that it is not just a list of what the Pi cannot do
            Assert.Contains("scenery", picture);
        }

        [Fact]
        public void ThePageAndTheNotesQuoteItAndNeitherTypesItByHand()
        {
            string picture = PiPicture();
            string page = Text("Web", "src", "index.njk");
            string workflow = Text(".github", "workflows", "release.yml");

            Assert.Contains("{{ site.piPicture }}", page);
            Assert.Contains(Placeholder, workflow);
            Assert.Contains("piPicture", workflow);
            Assert.Contains("Web/src/_data/site.json", workflow);

            //A copy typed by hand is the thing that drifted, however it is worded or wrapped
            string flatWorkflow = Regex.Replace(workflow, @"\s+", " ");
            Assert.DoesNotContain(picture, page);
            Assert.DoesNotContain(picture, flatWorkflow);
            foreach (string stale in Stale)
            {
                Assert.DoesNotContain(stale, page, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(stale, flatWorkflow, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void NoPageOfTheSiteSaysThePiHasNoScenery()
        {
            string site = FindUnderRepository(Path.Combine("Web", "src"));
            string[] files = Directory.GetFiles(site, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".njk") || path.EndsWith(".md") || path.EndsWith(".json") || path.EndsWith(".html")).ToArray();
            Assert.NotEmpty(files);

            //"no shadows" stays allowed in a post (the Pi blog's "no HDR, no bloom and no shadows" is true); the false half is the scenery
            Assert.All(files, path => Assert.DoesNotContain("no scenery", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EveryChapterKeepsItsSceneryOnThePotatoPath()
        {
            string source = Text("BS3DLibs", "Prazsky.Core", "Render", "PotatoBackdrop.cs");
            List<SceneKind> shipped = Shipped.LevelFiles().Select(path => Level.Load(path).Scene).Where(scene => scene.HasValue).Select(scene => scene.Value).Distinct().ToList();
            Assert.True(shipped.Count >= 14, $"only {shipped.Count} scenes found in the shipped levels");

            List<SceneKind> without = WithoutPotatoBackdrop(shipped, source);
            Assert.True(without.Count == 0,
                $"Chapters are set in {string.Join(", ", without)}, which PotatoBackdrop.For draws no backdrop for, so the Raspberry Pi's " +
                "\"every chapter still has its scenery\" (site.json, piPicture) is false for them: give the scene a backdrop or reword the sentence.");
        }

        //Seen to fire: the check above reads text and a green answer from it is evidence only once its failing branch has been seen to
        //fire on real data, so this runs it on the real source with one arm taken out
        [Fact]
        public void TheScenerySentenceCheckNamesASceneWhoseArmIsRemoved()
        {
            string source = Text("BS3DLibs", "Prazsky.Core", "Render", "PotatoBackdrop.cs");
            Assert.Contains("SceneKind.Savanna =>", source);
            string without = source.Replace("SceneKind.Savanna =>", "SceneKind.Sea =>");

            Assert.Empty(WithoutPotatoBackdrop(new[] { SceneKind.Savanna }, source));
            Assert.Equal(new[] { SceneKind.Savanna }, WithoutPotatoBackdrop(new[] { SceneKind.Savanna }, without));
            //an arm with a guard (City when the host has a config) counts as an arm
            Assert.Empty(WithoutPotatoBackdrop(new[] { SceneKind.City, SceneKind.NeonCity }, source));
        }
    }
}
