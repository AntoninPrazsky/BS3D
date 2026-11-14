using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// A terrain's ground with its depth laid first (#1097, High and Ultra) draws the one pass's picture only while two
    /// promises hold, and neither is visible where the code is changed. The depth twin (<c>&lt;technique&gt;Depth</c>)
    /// must discard on exactly the pixels its ground's program discards: a <c>clip()</c> the ground has and the twin has
    /// not leaves its depth laid where no colour is written (a hole through which whatever was drawn before the ground
    /// shows, and which nothing drawn later behind the ground fills), and one the twin has and the ground has not lets
    /// the ground's farther triangles paint over its nearer ones. And both must run the one vertex program, or the two
    /// depths are not the same bits. Below High, and on OpenGL ES, the ground is one pass and the same edit is harmless,
    /// so nothing on those tiers would show it. Every cut of a ground lives in its <c>&lt;Scene&gt;Cuts</c>, which the
    /// ground's entry point and the twin both call; this reads the effects' sources and holds that, as
    /// <see cref="PotatoShaderSourceTests"/> reads GamePi's (no test can compile an effect without the Windows SDK).
    /// <para>
    /// What it reads: the effect with its includes, without comments, both arms of every <c>#if</c>. Each technique's
    /// vertex and pixel entry point; and for each pixel entry point every <c>clip()</c> and <c>discard</c> it can reach
    /// through the functions it calls. A call made through a macro is not seen, and a call under a literal flag is
    /// taken as made (the meadow's patch, below).
    /// </para>
    /// </summary>
    public class TerrainDepthTwinSourceTests
    {
        private readonly ITestOutputHelper _output;

        public TerrainDepthTwinSourceTests(ITestOutputHelper output) => _output = output;

        private static readonly string Shaders = FindUnderRepository(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders"));

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate) || File.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The effects whose ground or far ring lays its depth first. Held whole, so a reading that finds no twin in one of
        //them fails here instead of passing on nothing, and a twin added to another effect is seen to be checked
        private static readonly string[] Grounds =
            { "Desert", "Forest", "Mars", "Meadow", "Moon", "Mountain", "Polar", "Savanna", "Tropical", "Volcano" };

        //Not a ground's twin: the meadow's patch lays its own depth (#924) with MeadowCarvedDepth, and its program reaches
        //MeadowCuts only under MeadowField's grid flag, which MeadowCarvedPS passes as a literal false. Neither of the two
        //discards (their compiled listings carry none); a reading of calls cannot see a literal
        private static readonly string[] NotGroundTwins = { "MeadowCarvedDepth" };

        [Fact]
        public void EveryDepthTwinCutsWhereItsGroundCutsAndRunsItsVertexProgram()
        {
            var failures = new List<string>();
            var effectsWithTwins = new SortedSet<string>(StringComparer.Ordinal);
            int twins = 0;

            foreach (string path in Directory.GetFiles(Shaders, "*.fx").OrderBy(p => p, StringComparer.Ordinal))
            {
                string effect = Path.GetFileNameWithoutExtension(path);
                EffectSource source = Read(path);

                foreach ((string name, Technique twin) in source.Techniques)
                {
                    if (!name.EndsWith("Depth", StringComparison.Ordinal) || NotGroundTwins.Contains(name)) continue;
                    if (!source.Techniques.TryGetValue(name[..^"Depth".Length], out Technique ground)) continue;

                    twins++;
                    effectsWithTwins.Add(effect);

                    if (twin.Vertex != ground.Vertex)
                        failures.Add($"{effect}: {name} runs {twin.Vertex}, its ground {ground.Vertex}: the depths would not be the same bits");

                    SortedSet<string> cut = source.CutsReachedFrom(ground.Pixel), laid = source.CutsReachedFrom(twin.Pixel);
                    _output.WriteLine($"{effect}: {name} ({twin.Vertex}, {twin.Pixel}) beside {ground.Pixel}: {string.Join(", ", laid)}");
                    if (laid.Count == 0)
                        failures.Add($"{effect}: {name} ({twin.Pixel}) reaches no cut at all");
                    if (!cut.SetEquals(laid))
                        failures.Add($"{effect}: {name} and its ground cut in different places. Only the ground ({ground.Pixel}): "
                            + $"{string.Join(", ", cut.Except(laid))}; only the twin ({twin.Pixel}): {string.Join(", ", laid.Except(cut))}");
                }
            }

            Assert.Empty(failures);
            Assert.Equal(Grounds.OrderBy(g => g, StringComparer.Ordinal), effectsWithTwins);
            _output.WriteLine($"{twins} depth twins in {effectsWithTwins.Count} effects");
        }

        //The reading of one effect: its functions' bodies (more than one under a name where #if arms or overloads define
        //it twice: all are read) and its techniques' entry points
        private sealed class EffectSource
        {
            public readonly Dictionary<string, List<string>> Bodies = new(StringComparer.Ordinal);
            public readonly Dictionary<string, Technique> Techniques = new(StringComparer.Ordinal);

            //Every clip() and discard the entry point can reach, each named by its function, the body and its place in it
            public SortedSet<string> CutsReachedFrom(string entry)
            {
                var cuts = new SortedSet<string>(StringComparer.Ordinal);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var pending = new Stack<string>();
                pending.Push(entry);

                while (pending.Count > 0)
                {
                    string function = pending.Pop();
                    if (!seen.Add(function) || !Bodies.TryGetValue(function, out List<string> bodies)) continue;

                    for (int b = 0; b < bodies.Count; b++)
                    {
                        MatchCollection sites = Regex.Matches(bodies[b], @"\bclip\s*\(|\bdiscard\b");
                        for (int s = 0; s < sites.Count; s++)
                            cuts.Add($"{function}#{b}.{s}");

                        foreach (Match call in Regex.Matches(bodies[b], @"\b([A-Za-z_]\w*)\s*\("))
                            if (Bodies.ContainsKey(call.Groups[1].Value)) pending.Push(call.Groups[1].Value);
                    }
                }

                return cuts;
            }
        }

        private readonly record struct Technique(string Vertex, string Pixel);

        private static EffectSource Read(string path)
        {
            string text = Expand(path, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            text = Regex.Replace(text, @"//[^\n]*", string.Empty);
            text = Regex.Replace(text, @"\\\r?\n", " ");
            text = Regex.Replace(text, @"^[ \t]*#[^\n]*", string.Empty, RegexOptions.Multiline);

            var effect = new EffectSource();
            int depth = 0, headerStart = 0, open = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    if (depth++ == 0) open = i;
                }
                else if (c == '}')
                {
                    if (--depth == 0)
                    {
                        Record(effect, text[headerStart..open].Trim(), text[(open + 1)..i]);
                        headerStart = i + 1;
                    }
                }
                else if (c == ';' && depth == 0) headerStart = i + 1;
            }

            Assert.True(depth == 0, $"{Path.GetFileName(path)}: the braces do not balance, so it was not read");
            return effect;
        }

        private static void Record(EffectSource effect, string header, string body)
        {
            Match technique = Regex.Match(header, @"^technique\s+(\w+)$");
            if (technique.Success)
            {
                string vertex = Regex.Match(body, @"VertexShader\s*=\s*compile\s+\w+\s+(\w+)\s*\(").Groups[1].Value;
                string pixel = Regex.Match(body, @"PixelShader\s*=\s*compile\s+\w+\s+(\w+)\s*\(").Groups[1].Value;
                effect.Techniques[technique.Groups[1].Value] = new Technique(vertex, pixel);
                return;
            }

            //A function: a name, its parameters and perhaps a semantic. Not a struct, a sampler's state or an initialiser
            //(an '=' outside the parameters, where a default value may stand)
            if (Regex.Replace(header, @"\([^()]*\)", "()").Contains('=') || header.StartsWith("struct", StringComparison.Ordinal)) return;
            Match function = Regex.Match(header, @"(\w+)\s*\([^()]*\)\s*(?::\s*\w+\s*)?$");
            if (!function.Success) return;

            string name = function.Groups[1].Value;
            if (!effect.Bodies.TryGetValue(name, out List<string> bodies)) effect.Bodies[name] = bodies = new List<string>();
            bodies.Add(body);
        }

        //The effect with its includes written in, each once, as the preprocessor's include guards would
        private static string Expand(string path, HashSet<string> included) =>
            Regex.Replace(File.ReadAllText(path), @"^[ \t]*#include\s+""([^""]+)""[^\n]*", include =>
            {
                string file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, include.Groups[1].Value));
                return included.Add(file) ? Expand(file, included) : string.Empty;
            }, RegexOptions.Multiline);
    }
}
