using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The desert creatures' gait (#1217): <see cref="CritterGait"/> is the copy of <c>Critter.fx</c>'s <c>Foot</c> the
    /// beetle's prints are laid by, so the shader must still say what it says; and the gait it says plants a foot: it lands
    /// at rest, stands still on the sand while the body walks over it, and leaves at rest, without a side's feet crossing.
    /// </summary>
    public class CritterGaitTests
    {
        private readonly ITestOutputHelper _output;

        public CritterGaitTests(ITestOutputHelper output) => _output = output;

        //The lines of Critter.fx that CritterGait mirrors, whitespace aside
        private static readonly string[] MirroredLines =
        {
            "float c = frac(phase / TWO_PI + 0.25);",
            "float u = saturate(c * 2.0);",
            "float v = saturate(c * 2.0 - 1.0);",
            "float forward = c < 0.5 ? 4.0 * u * u * (3.0 - 2.0 * u) - 2.0 * u - 1.0 : 1.0 - 2.0 * v;",
            "float up = c < 0.5 ? sin(PI * u) : 0.0;",
            "float2 foot = Foot(gait + input.Part.x);",
            "p.z += foot.x * input.Custom.w * input.Part.y;",
            "p.y += foot.y * LIFT * input.Part.y;",
        };

        private static string Shader() => File.ReadAllText(Path.Combine(RepoRoot(), "BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Critter.fx"));

        private static string RepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Game.sln"))) dir = Path.GetDirectoryName(dir);
            return dir ?? throw new InvalidOperationException("no Game.sln above " + AppContext.BaseDirectory);
        }

        private static string Squash(string s) => Regex.Replace(s, @"\s+", " ");

        //Every mirrored line in the shader's code (its comments left out), and TWO_PI and PI what CritterGait takes them as
        private static bool MirrorsTheGait(string shader)
        {
            string code = Squash(string.Join("\n", shader.Split('\n').Select(line => line.Split("//")[0])));
            return MirroredLines.All(line => code.Contains(Squash(line), StringComparison.Ordinal))
                && Regex.IsMatch(code, @"static const float PI = 3\.14159265;")
                && Regex.IsMatch(code, @"static const float TWO_PI = 6\.28318531;");
        }

        [Fact]
        public void The_shader_swings_a_leg_by_the_gait_the_prints_are_laid_by()
        {
            Assert.True(MirrorsTheGait(Shader()), "Critter.fx no longer has the gait lines CritterGait mirrors: change CritterGait (and the prints) with them");
        }

        [Fact]
        public void The_source_check_fires_on_a_changed_gait()
        {
            //Seen to fire (BestPractices.md, section 10), on the likeliest drifts
            string shader = Shader();
            Assert.False(MirrorsTheGait(shader.Replace("1.0 - 2.0 * v", "1.0 - 1.6 * v", StringComparison.Ordinal)));
            Assert.False(MirrorsTheGait(shader.Replace("phase / TWO_PI + 0.25", "phase / TWO_PI", StringComparison.Ordinal)));
            Assert.False(MirrorsTheGait(shader.Replace("foot.x * input.Custom.w", "foot.x * 0.07", StringComparison.Ordinal)));
            Assert.False(MirrorsTheGait("//" + string.Join("\n//", MirroredLines)));
        }

        public static TheoryData<bool> Kinds => new() { false, true };

        /// <summary>
        /// A foot stands still on the sand from its touchdown to its lift-off: there its place along the body, plus the ground
        /// the body has walked, does not change (the stance sweeps back exactly the stance's ground). In the air it only ever
        /// goes forwards over the ground, from rest to rest, so it neither jumps nor drags back at either end; and it is off
        /// the sand only in the air. Both kinds, every phase.
        /// </summary>
        [Theory]
        [MemberData(nameof(Kinds))]
        public void A_foot_stands_still_on_the_sand_and_goes_only_forwards_in_the_air(bool scorpion)
        {
            float steps = CritterGait.StepsPerLength(scorpion), stride = CritterGait.Stride(scorpion);
            const int SAMPLES = 4000;
            float touchdownAt = float.NaN, worstStill = 0f, worstBack = 0f, worstJump = 0f;
            float last = float.NaN;
            for (int s = 0; s <= SAMPLES * 3; s++)
            {
                //The ground walked, in body lengths, over three cycles, and the foot's place on it
                float walked = s / (float)SAMPLES / steps;
                float phase = CritterGait.Phase(walked, 1f, scorpion);
                float ground = walked + stride * CritterGait.Forward(phase);
                bool planted = CritterGait.Planted(phase);

                if (planted)
                {
                    Assert.Equal(0f, CritterGait.Lift(phase));
                    if (float.IsNaN(touchdownAt)) touchdownAt = ground;
                    worstStill = MathF.Max(worstStill, MathF.Abs(ground - touchdownAt));
                }
                else
                {
                    touchdownAt = float.NaN;
                    Assert.True(CritterGait.Lift(phase) >= 0f);
                }
                if (!float.IsNaN(last))
                {
                    worstBack = MathF.Max(worstBack, last - ground);
                    worstJump = MathF.Max(worstJump, MathF.Abs(ground - last));
                }
                last = ground;
            }
            _output.WriteLine(FormattableString.Invariant(
                $"{(scorpion ? "scorpion" : "beetle")}: a planted foot moves {worstStill:E1} body lengths, goes back {worstBack:E1}, the largest step {worstJump:E2} (a sample is {1f / SAMPLES / steps:E2} of walking)"));
            Assert.True(worstStill < 1e-4f, $"a planted foot moves {worstStill} body lengths over the sand");
            Assert.True(worstBack < 1e-5f, $"a foot goes back {worstBack} body lengths over the sand");
            //In the air it goes at most three times the body's pace, its fastest at mid-swing
            Assert.True(worstJump < 3.1f / SAMPLES / steps, $"a foot jumps {worstJump} body lengths in one sample");

            //It lands as far forwards as its stride, and leaves as far back
            Assert.Equal(1f, CritterGait.Forward(CritterGait.TOUCHDOWN), 5);
            Assert.Equal(-1f, CritterGait.Forward(CritterGait.TOUCHDOWN + MathF.PI), 5);
            Assert.True(CritterGait.Planted(CritterGait.TOUCHDOWN + 1e-4f) && !CritterGait.Planted(CritterGait.TOUCHDOWN - 1e-4f));
        }

        /// <summary>
        /// The feet of a side never cross: each kind's stride is short enough for the feet beside each other along a side,
        /// a step out of time, to keep at least <see cref="FOOT_GAP"/> body lengths between them through a whole cycle, on
        /// either mesh. The scorpion's legs stand closer along its body than the beetle's, which is why it takes shorter,
        /// quicker steps (<see cref="CritterGait.SCORPION_STEPS_PER_LENGTH"/>): on the beetle's stride its feet cross.
        /// </summary>
        [Fact]
        public void A_sides_feet_never_cross()
        {
            foreach ((string name, var mesh, bool scorpion) in new (string, (VertexPositionNormalTexture[] Vertices, short[] Indices), bool)[]
            {
                ("beetle", CritterMesh.Beetle(), false), ("fine beetle", CritterMesh.FineBeetle(), false),
                ("scorpion", CritterMesh.Scorpion(), true), ("fine scorpion", CritterMesh.FineScorpion(), true),
            })
            {
                var feet = Feet(mesh.Vertices);
                float own = Gap(feet, CritterGait.Stride(scorpion)), beetles = Gap(feet, CritterGait.Stride(scorpion: false));
                _output.WriteLine(FormattableString.Invariant($"{name}: {feet.Count} feet, the closest two of a side {own:F3} body lengths apart (on the beetle's stride {beetles:F3})"));
                Assert.Equal(scorpion ? 8 : 6, feet.Count);
                Assert.True(own >= FOOT_GAP, $"the {name}'s feet come {own:F3} body lengths apart");
                //Seen to fire: the scorpion's feet on the beetle's stride
                if (scorpion) Assert.True(beetles < FOOT_GAP, $"the {name}'s feet keep {beetles:F3} apart on the beetle's stride");
            }
        }

        private const float FOOT_GAP = 0.03f;

        //A mesh's feet: where the end of each leg stands at rest (its vertices of full weight, round a centre) and its phase
        private static List<(Vector3 At, float Phase)> Feet(VertexPositionNormalTexture[] vertices)
        {
            var feet = new List<(Vector3 Sum, int Count, float Phase)>();
            foreach (var v in vertices.Where(v => v.TextureCoordinate.X >= 0f && v.TextureCoordinate.Y > 0.999f))
            {
                int near = feet.FindIndex(f => f.Phase == v.TextureCoordinate.X && Vector3.Distance(f.Sum / f.Count, v.Position) < 0.06f);
                if (near < 0) feet.Add((v.Position, 1, v.TextureCoordinate.X));
                else feet[near] = (feet[near].Sum + v.Position, feet[near].Count + 1, feet[near].Phase);
            }
            return feet.Select(f => (f.Sum / f.Count, f.Phase)).ToList();
        }

        //The closest two feet of a side come along it over a cycle, each swung by the gait at `stride`
        private static float Gap(List<(Vector3 At, float Phase)> feet, float stride)
        {
            float least = float.MaxValue;
            foreach (int side in new[] { -1, 1 })
            {
                var row = feet.Where(f => MathF.Sign(f.At.X) == side).OrderByDescending(f => f.At.Z).ToArray();
                for (int k = 0; k + 1 < row.Length; k++)
                    for (int s = 0; s < 2000; s++)
                    {
                        float gait = MathHelper.TwoPi * s / 2000f;
                        float front = row[k].At.Z + stride * CritterGait.Forward(gait + row[k].Phase);
                        float back = row[k + 1].At.Z + stride * CritterGait.Forward(gait + row[k + 1].Phase);
                        least = MathF.Min(least, front - back);
                    }
            }
            return least;
        }
    }
}
