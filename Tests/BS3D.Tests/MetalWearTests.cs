using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The metal's wear on High and Ultra (#1199), held with no device: which tiers draw it; how a turned profile's corners
    /// are told apart (an inside corner, where a ring meets the tube, is a join the tarnish and grime gather in; an outside
    /// one, a ring's crest, an edge the hands rub bright); that the barrel's, the wheel's and the emitters' corners are the
    /// ones their meshes are turned from and fit the shader's slots; and how they are packed for it. The look itself is the
    /// owner's eye: what these hold is that the shader is handed the right places.
    /// </summary>
    public class MetalWearTests
    {
        private readonly ITestOutputHelper _output;

        public MetalWearTests(ITestOutputHelper output) => _output = output;

        [Theory]
        [InlineData(QualityLevel.Potato, false)]
        [InlineData(QualityLevel.Low, false)]
        [InlineData(QualityLevel.Medium, false)]
        [InlineData(QualityLevel.High, true)]
        [InlineData(QualityLevel.Ultra, true)]
        public void Only_High_and_Ultra_wear_the_metal(QualityLevel quality, bool worn) =>
            Assert.Equal(worn, QualityPreset.Presets[(int)quality].MetalWear);

        /// <summary>
        /// The packed corners are handed to the shader whole (<c>EffectParameter.SetValue(Vector4[])</c>): more slots than the
        /// shader declares throw on the first worn draw, fewer leave its last slots holding whatever the previous draw wrote.
        /// So the two counts are one figure, read here off <c>WornMetal.fxh</c> with its comments taken out.
        /// </summary>
        [Fact]
        public void The_shader_reads_as_many_corner_slots_as_are_packed()
        {
            string shader = CodeOf(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "InstancedModel", "WornMetal.fxh"));

            Match slots = Regex.Match(shader, @"^#define\s+WEAR_CORNER_SLOTS\s+(\d+)\s*$", RegexOptions.Multiline);
            Assert.True(slots.Success, "WornMetal.fxh defines no WEAR_CORNER_SLOTS");
            Assert.Equal(MetalWear.CORNER_SLOTS, int.Parse(slots.Groups[1].Value));
            Assert.Matches(@"\bfloat4\s+WearCorners\s*\[\s*WEAR_CORNER_SLOTS\s*\]\s*;", shader);
        }

        /// <summary>
        /// The table is not the tier until something carries it, and the gun and the emitters are drawn plain until told
        /// otherwise. One answer (<c>BS3DGame.MetalWorn</c>: the tier's row, and never on a phone) seeds the rig where
        /// LoadContent makes it, is given to the emitters when a session hands them in, and is stated again to both on every
        /// change of tier.
        /// </summary>
        [Fact]
        public void The_tier_reaches_the_gun_and_the_emitters_and_a_phone_never_wears()
        {
            string quality = CodeOf(Path.Combine("Game", "BS3DGame.Quality.cs"));
            Assert.Matches(@"\bbool\s+MetalWorn\s*=>\s*QualityPreset\.Presets\[\(int\)_quality\]\.MetalWear\s*&&\s*GraphicsSupport\.MetalWear\s*;",
                quality);

            string apply = MethodBody(quality, "internal void ApplyQuality(");
            Assert.Matches(@"_cannonRig\.Worn\s*=\s*MetalWorn\s*;", apply);
            Assert.Matches(@"SessionEmitters\.Worn\s*=\s*MetalWorn\s*;", apply);

            string game = CodeOf(Path.Combine("Game", "BS3DGame.cs"));
            int built = game.IndexOf("_cannonRig = new CannonRig(", StringComparison.Ordinal);
            Assert.True(built >= 0, "BS3DGame.cs builds no CannonRig");
            Assert.Matches(@"_cannonRig\.Worn\s*=\s*MetalWorn\s*;",
                game.Substring(built, game.IndexOf("#endregion", built, StringComparison.Ordinal) - built));

            string scene = CodeOf(Path.Combine("Game", "BS3DGame.Scene.cs"));
            int emitters = scene.IndexOf("LaserEmitters SessionEmitters", StringComparison.Ordinal);
            Assert.True(emitters >= 0, "BS3DGame.Scene.cs has no SessionEmitters");
            Assert.Matches(@"set\s*\{\s*_sessionEmitters\s*=\s*value\s*;\s*if\s*\(\s*value\s*!=\s*null\s*\)\s*value\.Worn\s*=\s*MetalWorn\s*;",
                scene.Substring(emitters, scene.IndexOf("private BS3D.Effects.LaserEmitters _sessionEmitters", emitters, StringComparison.Ordinal) - emitters));

            Assert.Matches(@"public\s+static\s+bool\s+MetalWear\s*=>\s*!OperatingSystem\.IsAndroid\(\)\s*;",
                CodeOf(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "GraphicsSupport.cs")));
        }

        [Fact]
        public void A_step_up_a_profile_run_along_its_axis_is_a_join_at_its_foot_and_an_edge_at_its_crest()
        {
            //A ring standing proud of a tube, described muzzle to breech with the outside on the right (CannonMesh's way):
            //along at radius 1, up the ring's front face to 1.2, along its top, down its back face, along again
            Vector2[] points = { new(0f, 1f), new(1f, 1f), new(1f, 1.2f), new(2f, 1.2f), new(2f, 1f), new(3f, 1f) };
            bool[] crease = { true, true, true, true, true, true };

            var corners = new List<WearCorner>();
            MetalWear.AddProfileCorners(corners, points, crease, latheWinding: false);

            Assert.Equal(4, corners.Count);
            Assert.False(Corner(corners, 1f, 1f).Edge);
            Assert.True(Corner(corners, 1f, 1.2f).Edge);
            Assert.True(Corner(corners, 2f, 1.2f).Edge);
            Assert.False(Corner(corners, 2f, 1f).Edge);
        }

        [Fact]
        public void A_lathe_profile_down_its_axis_reads_the_same_ring_the_same_way()
        {
            //The same ring as a LatheMesh profile: (radius, height), described down the axis with the outside on the left
            LathePoint[] profile =
            {
                new(1f, 3f, crease: true), new(1f, 2f, crease: true), new(1.2f, 2f, crease: true),
                new(1.2f, 1f, crease: true), new(1f, 1f, crease: true), new(1f, 0f, crease: true),
            };

            var corners = new List<WearCorner>();
            MetalWear.AddLatheCorners(corners, profile);

            Assert.Equal(4, corners.Count);
            Assert.False(Corner(corners, 2f, 1f).Edge);
            Assert.True(Corner(corners, 2f, 1.2f).Edge);
            Assert.True(Corner(corners, 1f, 1.2f).Edge);
            Assert.False(Corner(corners, 1f, 1f).Edge);
        }

        [Fact]
        public void The_barrels_rings_are_joins_at_their_feet_and_edges_at_their_crests()
        {
            float frontZ = -Muzzle(), backZ = Breech();
            IReadOnlyList<WearCorner> corners = CannonMesh.WearCornersFor(CannonRig.BORE_RADIUS, CannonRig.WALL_THICKNESS, frontZ,
                backZ, backZ - CannonRig.CHAMBER_DEPTH);

            foreach (WearCorner c in corners) _output.WriteLine($"z {c.Axial:F3} r {c.Radial:F3} {(c.Edge ? "edge" : "join")}");

            Assert.InRange(corners.Count, 8, MetalWear.MAX_CORNERS);

            //Every step of the profile (two corners at one Z) but the muzzle face: the foot is a join, the crest an edge
            var steps = corners.Where(c => MathF.Abs(c.Axial - frontZ) > 1e-4f).GroupBy(c => MathF.Round(c.Axial, 4))
                .Where(g => g.Count() == 2).ToList();
            Assert.True(steps.Count >= 4, $"the girdle's two faces and the base ring's two, found {steps.Count} steps");

            foreach (var step in steps)
            {
                WearCorner foot = step.OrderBy(c => c.Radial).First(), crest = step.OrderBy(c => c.Radial).Last();
                Assert.False(foot.Edge, $"the step at z {step.Key} has its foot at r {foot.Radial} named an edge");
                Assert.True(crest.Edge, $"the step at z {step.Key} has its crest at r {crest.Radial} named a join");
            }

            //The muzzle face's two lips are edges, the bore's and the outer one
            Assert.Equal(2, corners.Count(c => MathF.Abs(c.Axial - frontZ) < 1e-4f && c.Edge));
        }

        [Fact]
        public void The_wheels_and_the_emitters_corners_fit_the_shaders_slots_and_name_their_feet()
        {
            IReadOnlyList<WearCorner> wheel = OmniWheelMesh.WearCorners(Rig("WHEEL_RADIUS"), Rig("ROLLER_RADIUS"),
                Rig("ROLLER_ROW_OFFSET"), Rig("PLATE_THICKNESS"));
            IReadOnlyList<WearCorner> pedestal = LaserEmitterMesh.WearCorners(LaserEmitterMesh.Part.Pedestal);
            IReadOnlyList<WearCorner> housing = LaserEmitterMesh.WearCorners(LaserEmitterMesh.Part.Housing);

            foreach (var (name, list) in new[] { ("wheel", wheel), ("pedestal", pedestal), ("housing", housing) })
            {
                foreach (WearCorner c in list) _output.WriteLine($"{name}: a {c.Axial:F3} r {c.Radial:F3} {(c.Edge ? "edge" : "join")}");
                Assert.InRange(list.Count, 2, MetalWear.MAX_CORNERS);
                Assert.Contains(list, c => c.Edge);
                Assert.Contains(list, c => !c.Edge);
            }

            //The wheel's plate rim is an edge, and the hub face's five walls (the lip's two, the flange's two and the cap's)
            //each stand on the plate's outer face in a join
            float plateRadius = Rig("WHEEL_RADIUS") - Rig("ROLLER_RADIUS");
            float plateOuterX = Rig("ROLLER_ROW_OFFSET") + Rig("ROLLER_RADIUS") + Rig("PLATE_THICKNESS");
            Assert.Contains(wheel, c => c.Edge && MathF.Abs(c.Radial - plateRadius) < 1e-4f && MathF.Abs(c.Axial - plateOuterX) < 1e-4f);
            Assert.Equal(5, wheel.Count(c => !c.Edge && MathF.Abs(c.Axial - plateOuterX) < 1e-4f));

            //The pedestal's flange where it stands on the glass (the foot's origin is on it)
            Assert.Contains(pedestal, c => !c.Edge && c.Axial == 0f && c.Radial == LaserEmitterMesh.FOOT_RADIUS);
        }

        [Fact]
        public void Corners_are_packed_two_to_a_slot_with_the_kind_in_the_radials_sign()
        {
            var corners = new[] { new WearCorner(1f, 0.5f, edge: true), new WearCorner(2f, 0.7f, edge: false), new WearCorner(3f, 0.9f, edge: true) };
            Vector4[] packed = MetalWear.Pack(corners);

            Assert.Equal(MetalWear.CORNER_SLOTS, packed.Length);
            Assert.Equal(new Vector4(1f, 0.5f, 2f, -0.7f), packed[0]);
            Assert.Equal(new Vector4(3f, 0.9f, 0f, 0f), packed[1]);
            Assert.All(packed.Skip(2), slot => Assert.Equal(Vector4.Zero, slot));

            //A corner on the axis would read as an empty place, and more than the slots hold would be dropped unseen
            Assert.Throws<ArgumentException>(() => MetalWear.Pack(new[] { new WearCorner(1f, 0f, edge: true) }));
            Assert.Throws<ArgumentException>(() => new MetalWear(Vector4.One, Vector3.Zero, axis: Vector3.UnitZ,
                corners: Enumerable.Range(0, MetalWear.MAX_CORNERS + 1).Select(i => new WearCorner(i, 1f, true)).ToArray()));
        }

        [Fact]
        public void A_part_with_no_profile_still_states_a_unit_axis_and_reads_no_corner()
        {
            //The shader builds a basis round the axis whether or not it reads a corner, and a zero axis is a NaN there
            var wear = new MetalWear(CannonRig.IRON_WEAR, Vector3.Zero);

            Assert.Equal(0f, wear.Profile.W);
            Assert.Equal(1f, new Vector3(wear.Profile.X, wear.Profile.Y, wear.Profile.Z).Length(), 5);
            Assert.All(wear.Corners, slot => Assert.Equal(Vector4.Zero, slot));
        }

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The text of a source file without its comments, so a word in a note is not taken for code
        private static string CodeOf(string relative) => Regex.Replace(File.ReadAllText(FindFile(relative)), @"//[^\n]*", string.Empty);

        //A C# method's body, from its head to the brace that closes it at the method's own indentation (eight spaces)
        private static string MethodBody(string code, string head)
        {
            int start = code.IndexOf(head, StringComparison.Ordinal);
            Assert.True(start >= 0, $"no {head}");
            int end = code.IndexOf("\n        }", start, StringComparison.Ordinal);
            return code.Substring(start, end - start);
        }

        private static WearCorner Corner(List<WearCorner> corners, float axial, float radial) =>
            corners.Single(c => MathF.Abs(c.Axial - axial) < 1e-5f && MathF.Abs(c.Radial - radial) < 1e-5f);

        private static float Rig(string name) =>
            Convert.ToSingle(typeof(CannonRig).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null));

        //The tube's ends as CannonRig cuts them for the shipped magazine: the muzzle face a ball radius ahead of the front
        //round, which sits PivotToFrontBall ahead of the trunnions, and the breech face a ball radius behind the last round
        private static float PivotToFrontBall() =>
            (Magazine.SIZE - 1) * Magazine.SPACING * 0.5f + Rig("TRUNNION_SETBACK");

        private static float Muzzle() => PivotToFrontBall() + 0.5f;

        private static float Breech() => (Magazine.SIZE - 1) * Magazine.SPACING - PivotToFrontBall() + 0.5f;
    }
}
