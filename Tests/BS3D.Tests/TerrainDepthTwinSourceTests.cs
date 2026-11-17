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
    /// ground's entry point and the twin both call (but for a twin laid only where its ground's one cut never fires, the
    /// outback's monoliths, which makes none: <c>TwinsOfUncutDraws</c>); this reads the effects' sources and holds that, as
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

        //The effects whose ground or far ring lays its depth first, and the outback, whose monoliths do (OutbackRockDepth,
        //#1137). Held whole, so a reading that finds no twin in one of them fails here instead of passing on nothing, and a
        //twin added to another effect is seen to be checked
        private static readonly string[] Grounds =
            { "Desert", "Forest", "Mars", "Meadow", "Moon", "Mountain", "Outback", "Polar", "Savanna", "Tropical", "Volcano" };

        //Not a ground's twin: the meadow's patch lays its own depth (#924) with MeadowCarvedDepth, and its program reaches
        //MeadowCuts only under MeadowField's grid flag, which MeadowCarvedPS passes as a literal false. Neither of the two
        //discards (their compiled listings carry none); a reading of calls cannot see a literal
        private static readonly string[] NotGroundTwins = { "MeadowCarvedDepth" };

        //Twins laid only for the draws their ground's one cut never reaches, so they make no cut at all: the one cut that
        //ground may make, what that clip()'s argument must read (spaces made single), and the flag the ground's program must
        //hand the cut's first parameter. The outback's monoliths (#1137): OutbackRockCuts fires only on a boulder (Outcrop 1),
        //and the boulders are one pass. The twin must reach no cut, its ground exactly that one clip(), and the clip must
        //fire only with the flag up: a second clip, in the cut's function or anywhere else in the program, or a cut that can
        //fire on a monolith, fails here, because the twin would not make it and its depth would stand where no colour is
        //written (OutbackRockCuts says what to check). That the twin is drawn for the monoliths alone is the other fact
        private sealed record UncutDraws(string Cut, string Clip, string Flag);

        private static readonly Dictionary<string, UncutDraws> TwinsOfUncutDraws = new()
        {
            ["OutbackRockDepth"] = new("OutbackRockCuts#0.0", @"^outcrop > 0\.5 && [^|?]+ \? -1\.0 : 1\.0$", "input.Outcrop"),
        };

        [Fact]
        public void EveryDepthTwinCutsWhereItsGroundCutsAndRunsItsVertexProgram()
        {
            var failures = new List<string>();
            var effectsWithTwins = new SortedSet<string>(StringComparer.Ordinal);
            var uncutSeen = new HashSet<string>(StringComparer.Ordinal);
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
                    if (TwinsOfUncutDraws.TryGetValue(name, out UncutDraws uncut))
                    {
                        uncutSeen.Add(name);
                        if (laid.Count > 0)
                            failures.Add($"{effect}: {name} is laid for draws no cut reaches, and it cuts: {string.Join(", ", laid)}");
                        if (cut.Count != 1 || cut.Min != uncut.Cut)
                            failures.Add($"{effect}: {name}'s ground ({ground.Pixel}) may cut only at {uncut.Cut}, which its twin "
                                + $"never meets; it cuts at {string.Join(", ", cut)}");
                        else
                            failures.AddRange(SparingFailures(effect, name, source, ground.Pixel, uncut));
                        continue;
                    }

                    if (laid.Count == 0)
                        failures.Add($"{effect}: {name} ({twin.Pixel}) reaches no cut at all");
                    if (!cut.SetEquals(laid))
                        failures.Add($"{effect}: {name} and its ground cut in different places. Only the ground ({ground.Pixel}): "
                            + $"{string.Join(", ", cut.Except(laid))}; only the twin ({twin.Pixel}): {string.Join(", ", laid.Except(cut))}");
                }
            }

            Assert.Empty(failures);
            Assert.Equal(Grounds.OrderBy(g => g, StringComparer.Ordinal), effectsWithTwins);
            Assert.Equal(TwinsOfUncutDraws.Keys.OrderBy(t => t, StringComparer.Ordinal), uncutSeen.OrderBy(t => t, StringComparer.Ordinal));
            _output.WriteLine($"{twins} depth twins in {effectsWithTwins.Count} effects");
        }

        //The outback's half of TwinsOfUncutDraws (#1137): the monoliths' uncut depth is laid for them alone and only they are
        //shaded on it, and the flag the cut reads is each instance's kind. A depth pass that drew the boulders would lay their
        //depth where their program later clips it, and a monolith flagged as a boulder is clipped wherever its depth stands
        //(its whole footprint is inside a monolith's): holes on High and Ultra, nothing below. Reads OutbackRockDiscs.cs
        //without its comments, and Outback.fx's rock vertex program
        [Fact]
        public void TheOutbacksUncutDepthIsLaidForTheMonolithsAlone()
        {
            string discs = File.ReadAllText(FindUnderRepository(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "OutbackRockDiscs.cs")));
            discs = Regex.Replace(Regex.Replace(discs, @"//[^\n]*", string.Empty), @"\s+", " ");
            EffectSource outback = Read(Path.Combine(Shaders, "Outback.fx"));
            var failures = new List<string>();

            if (Regex.Matches(discs, @"\bDrawGroup\s*\(\s*\w+\s*,\s*monolithDepth\s*\)").Count != 1
                || !Regex.IsMatch(discs, @"foreach \(Group group in _groups\) if \(!group\.Outcrop\) DrawGroup\(group, monolithDepth\);"))
                failures.Add("OutbackRockDiscs.Draw: the depth pass is not one loop over the groups that draws the monoliths alone "
                    + "(if (!group.Outcrop))");
            if (Regex.Matches(discs, @"\bDepthStencilState\.DepthRead\b").Count != 1
                || !Regex.IsMatch(discs, @"monolithDepth != null && !group\.Outcrop \? DepthStencilState\.DepthRead : "))
                failures.Add("OutbackRockDiscs.Draw: something but the monoliths, whose depth alone was laid, is shaded on DepthRead");
            if (!Regex.IsMatch(discs, @"new Vector4\([^;]*, Outcrop \? 1f : 0f, 0f\)\);"))
                failures.Add("OutbackRockDiscs.Group.Cull: an instance's flag (Size.z) is not its group's kind, Outcrop ? 1f : 0f");
            if (!outback.Bodies.TryGetValue("OutbackRockVS", out List<string> vertex)
                || vertex.Count != 1 || !Regex.IsMatch(vertex[0], @"\boutput\.Outcrop\s*=\s*size\.z\s*;"))
                failures.Add("Outback.fx: OutbackRockVS does not hand the cut the instance's flag (output.Outcrop = size.z)");

            Assert.Empty(failures);
        }

        //A ground's one cut that its twin does not make (TwinsOfUncutDraws): the clip()'s argument must read as uncut.Clip,
        //and every call of the cut's function must hand it uncut.Flag first, so the cut fires only where the flag is up
        private static IEnumerable<string> SparingFailures(string effect, string twin, EffectSource source, string groundPixel, UncutDraws uncut)
        {
            Match site = Regex.Match(uncut.Cut, @"^(\w+)#(\d+)\.(\d+)$");
            string function = site.Groups[1].Value;
            string body = source.Bodies[function][int.Parse(site.Groups[2].Value)];
            Match cut = Regex.Matches(body, @"\bclip\s*\(|\bdiscard\b")[int.Parse(site.Groups[3].Value)];

            string argument = cut.Value == "discard" ? "discard" : Argument(body, cut.Index + cut.Length);
            if (!Regex.IsMatch(argument, uncut.Clip))
                yield return $"{effect}: {uncut.Cut}, the one cut {twin} does not make, reads clip({argument}), not {uncut.Clip}: "
                    + "it may fire where the twin's depth is laid";

            int calls = 0, flagged = 0;
            foreach (string caller in source.Bodies.Values.SelectMany(b => b))
            {
                calls += Regex.Matches(caller, $@"\b{function}\s*\(").Count;
                flagged += Regex.Matches(caller, $@"\b{function}\s*\(\s*{Regex.Escape(uncut.Flag)}\s*,").Count;
            }

            if (calls == 0 || flagged != calls)
                yield return $"{effect}: {function} is called {calls} times, {flagged} of them with {uncut.Flag} first; "
                    + $"{groundPixel} must hand it the flag that spares the draws {twin} is laid for";
        }

        //What stands between a call's opening parenthesis (just before `open`) and its closing one, spaces made single
        private static string Argument(string body, int open)
        {
            int depth = 1, i = open;
            for (; i < body.Length && depth > 0; i++)
            {
                if (body[i] == '(') depth++;
                else if (body[i] == ')') depth--;
            }

            return Regex.Replace(body[open..(i - 1)], @"\s+", " ").Trim();
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
