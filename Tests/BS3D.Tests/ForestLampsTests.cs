using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using static BS3D.Tests.PlayViewOpenings;

namespace BS3D.Tests
{
    /// <summary>
    /// The forest's lamps round the island and the wind turbine on the hills (#1166, <see cref="ForestLampLayout"/>,
    /// <see cref="ForestTurbineLayout"/>): each pole has its room at every roll of the wood (the wood giving way where no spot
    /// in its reach is clear), stands outside every lens that plays and before no ball and no gun in any play view, the opening
    /// view looking between two of them at every roll; the lights are shaded lamps with a slot left for a blast, cut where the
    /// tipped shades' rims are, and reach the whole cluster; the old lamp stutters and the rest breathe; the rotor turns and the
    /// aviation light flashes as pure functions of the clock; the turbine stands in the opening play view's sky, inside the haze,
    /// on a pad the far stand leaves open.
    /// </summary>
    public class ForestLampsTests
    {
        private readonly ITestOutputHelper _output;

        public ForestLampsTests(ITestOutputHelper output) => _output = output;

        public static readonly IEnumerable<object[]> Aspects = ASPECTS.Select(a => new object[] { a });

        private static readonly ForestSceneConfig Forest = new();

        //The rolls of the wood the layout is held at (the Game rolls a new one every launch, Random.Shared.Next()): the first
        //200 offsets, 200 from the whole int range off a fixed stream (so a failure reproduces), and the rolls a review found
        //poles short of their room at before the wood gave way (a broadleaf's crown over the ring at 804533675 and 1862119023)
        //and a bulb behind the cluster's middle at before the walk was held off it (59, 804900268, 142914922)
        private static readonly int[] ROLLS = BuildRolls();

        private static int[] BuildRolls()
        {
            var rolls = new List<int>(Enumerable.Range(0, 200));
            var stream = new Random(1166);
            for (int i = 0; i < 200; i++) rolls.Add(stream.Next());
            rolls.AddRange(new[] { 804533675, 1372121676, 1862119023, 1659728552, 59, 804900268, 142914922 });
            return rolls.Distinct().ToArray();
        }

        //Each roll's planting and its layout, planned once for every test here (the plan fells out of its planting)
        private static readonly Lazy<(int Roll, ForestScatter Planting, ForestLampLayout Layout)[]> Planned = new(() =>
            ROLLS.AsParallel().AsOrdered().Select(roll =>
            {
                ForestScatter planting = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED + roll, Forest,
                    (x, z) => TerrainMirror.Forest(x, z, Forest));
                return (roll, planting, ForestLampLayout.Plan(Forest, planting));
            }).ToArray());

        private static ForestLampLayout Layout0 => Planned.Value[0].Layout;

        //The pole, its arm and its shade in the world, as points
        private static IEnumerable<Vector3> Body(ForestLampLayout.Lamp lamp)
        {
            for (float h = 0f; h <= ForestLampLayout.POLE_LENGTH; h += 1f) yield return Vector3.Transform(new Vector3(0f, h, 0f), lamp.World);
            for (float a = 0f; a <= ForestLampLayout.ARM_REACH; a += 0.25f)
                yield return Vector3.Transform(new Vector3(0f, ForestLampLayout.POLE_LENGTH - 1.1f, -a), lamp.World);
            yield return lamp.Bulb;
        }

        [Fact]
        public void EveryPoleHasRoomAtEveryRollOfTheWood()
        {
            //sceneseed= rolls the planting; at every roll each pole walks to a spot no trunk, crown, boulder, stump or dead wood
            //comes within CLEARANCE of, or where none in its reach is clear the wood gives way there, and it stays on its ring.
            //The room is measured in the planting as it stands after the plan, which is what the hosts draw. Seen to fire with
            //the poles held at their nominal spots (negative at most rolls: a spruce's skirt, a boulder at the foot), with the
            //felling taken out of Plan and with the planting left unfelled (roll 44: 0.59, through RoomIn)
            float least = float.PositiveInfinity, farthest = 0f;
            int felledRolls = 0, felled = 0;
            foreach ((int roll, ForestScatter planting, ForestLampLayout layout) in Planned.Value)
            {
                Assert.Equal(ForestLampLayout.COUNT, layout.Lamps.Count);
                if (layout.Felled > 0) felledRolls++;
                felled += layout.Felled;
                foreach (ForestLampLayout.Lamp lamp in layout.Lamps)
                {
                    float room = ForestLampLayout.RoomIn(lamp, Forest, planting);
                    Assert.True(room >= ForestLampLayout.CLEARANCE,
                        $"roll {roll}: the pole at {lamp.Foot.X:F1}, {lamp.Foot.Z:F1} has {room:F2} of room, under {ForestLampLayout.CLEARANCE}");
                    float r = new Vector2(lamp.Foot.X, lamp.Foot.Z).Length();
                    Assert.InRange(r, ForestLampLayout.RING_RADIUS - ForestLampLayout.RING_GIVE - 1e-3f, ForestLampLayout.RING_RADIUS + ForestLampLayout.RING_GIVE + 1e-3f);
                    least = MathF.Min(least, room);
                    farthest = MathF.Max(farthest, MathF.Abs(r - ForestLampLayout.RING_RADIUS));
                }
            }

            //The wood gives way at a few rolls (the review's are among them), never at most
            Assert.InRange(felledRolls, 1, ROLLS.Length / 10);
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} rolls: least room {1:F2}, farthest off the ring {2:F2}, {3} felled at {4} rolls",
                ROLLS.Length, least, farthest, felled, felledRolls));
        }

        [Fact]
        public void TheLampsStandOnTheGroundAtDifferentHeights()
        {
            //Each foot sunk under the ground's mirror by SINK and its own sink, so no pole floats; and not all at one height
            ForestLampLayout layout = Layout0;
            var tops = new List<float>();
            foreach (ForestLampLayout.Lamp lamp in layout.Lamps)
            {
                float ground = TerrainMirror.Forest(lamp.Foot.X, lamp.Foot.Z, Forest);
                Assert.True(lamp.Foot.Y < ground - 0.4f, $"the pole at {lamp.Foot.X:F1}, {lamp.Foot.Z:F1} stands {lamp.Foot.Y - ground:F2} off the ground");
                tops.Add(Vector3.Transform(new Vector3(0f, ForestLampLayout.POLE_LENGTH, 0f), lamp.World).Y - ground);
                _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "foot {0:F1},{1:F1},{2:F1} bulb {3:F1},{4:F1},{5:F1}",
                    lamp.Foot.X, lamp.Foot.Y, lamp.Foot.Z, lamp.Bulb.X, lamp.Bulb.Y, lamp.Bulb.Z));
            }
            Assert.True(tops.Max() - tops.Min() > 0.5f, "every pole stands to the same height over its ground");
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void NoLampStandsBeforeTheFieldInAnyPlayView(float aspect)
        {
            //The play view of every shipped level, moved into the forest through the pause's Scene list, in every pose the
            //overview holds: every lens stands inside the ring's nearest reach (OpenGroundRadius, which the front end's orbit keeps
            //inside too), and no point of a pole, its arm or its bulb is in the frame nearer the lens than the arena's axis, so
            //nothing of a lamp stands before the gun or a ball. Seen to fire with the ring at 40.
            ForestLampLayout layout = Layout0;
            float farthestLens = 0f;
            foreach (string file in Shipped.LevelFiles())
            {
                Opening open = Open(file, aspect);
                foreach ((string pose, Vector3 lens, Vector3 target, float fov) in open.Poses)
                {
                    string where = $"{open.Level} at {aspect:F2}, {pose}";
                    float lensRadius = new Vector2(lens.X, lens.Z).Length();
                    farthestLens = MathF.Max(farthestLens, lensRadius);
                    Assert.True(lensRadius < ForestLampLayout.OpenGroundRadius, $"{where}: the lens stands {lensRadius:F1} out, past the lamps' {ForestLampLayout.OpenGroundRadius:F1}");

                    Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);
                    float axis = Vector2.Distance(new Vector2(lens.X, lens.Z), Vector2.Zero);
                    foreach (ForestLampLayout.Lamp lamp in layout.Lamps)
                        foreach (Vector3 point in Body(lamp))
                        {
                            Vector3 ndc = Project(point, viewProjection, out bool ahead);
                            if (!ahead || MathF.Abs(ndc.X) > 1f || MathF.Abs(ndc.Y) > 1f) continue;
                            float distance = Vector2.Distance(new Vector2(lens.X, lens.Z), new Vector2(point.X, point.Z));
                            Assert.True(distance > axis, $"{where}: a lamp at {point.X:F1}, {point.Z:F1} stands in the frame {distance:F1} from the lens, before the axis at {axis:F1}");
                            Assert.False(BehindTheGun(lens, point, open.Gun, open.Aim) && distance < axis, $"{where}: a lamp before the gun");
                        }
                }
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F2}: the farthest lens {1:F1} out, the lamps' nearest reach {2:F1}",
                aspect, farthestLens, ForestLampLayout.OpenGroundRadius));
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void TheOpeningViewLooksBetweenTwoLamps(float aspect)
        {
            //From the first level's opening pose (the lens on +Z looking at the cluster), at every roll of the wood, no bulb stands
            //near the frame's middle column, where it would shine behind the cluster's middle, however far the planting sent the
            //poles looking for room (FLANK); and at the default roll the two far lamps flank it in the frame (at others a pole that
            //walked outwards may stand just past a narrower window's edge: roll 114 at 1.6). Seen to fire with the ring turned by
            //half a step (a lamp straight behind the cluster), and with FLANK at 0 (rolls 59, 804900268 and 142914922 at 2.4: a
            //bulb at -0.27 of the half-frame)
            Opening open = Open(Shipped.LevelFiles().First(), aspect);
            (_, Vector3 lens, Vector3 target, float fov) = open.Poses[0];
            Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);
            float nearest = 1f;
            int flankedRolls = 0;
            foreach ((int roll, _, ForestLampLayout layout) in Planned.Value)
            {
                int flanking = 0;
                foreach (ForestLampLayout.Lamp lamp in layout.Lamps)
                {
                    Vector3 ndc = Project(lamp.Bulb, viewProjection, out bool ahead);
                    if (!ahead || MathF.Abs(ndc.X) > 1f || MathF.Abs(ndc.Y) > 1f) continue;
                    Assert.True(MathF.Abs(ndc.X) > 0.3f, $"roll {roll} at {aspect:F2}: a bulb stands at {ndc.X:F2} of the half-frame, behind the cluster's middle");
                    nearest = MathF.Min(nearest, MathF.Abs(ndc.X));
                    flanking++;
                }
                if (flanking >= 2) flankedRolls++;
                if (roll == 0) Assert.True(flanking >= 2 || aspect < 1.5f, $"at {aspect:F2} only {flanking} lamps flank the cluster in the opening view");
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F2}: the nearest bulb to the middle at {1:F2} of the half-frame; two lamps in the frame at {2} of {3} rolls",
                aspect, nearest, flankedRolls, ROLLS.Length));
        }

        //SceneLamp.fxh's constants, read off its source: the test's copy of the cut below is the shader's
        private static float ShaderConstant(string name)
        {
            string source = File.ReadAllText(Path.Combine(RepoRoot(), "BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "SceneLamp.fxh"));
            Match match = Regex.Match(source, @"static const float " + name + @" = ([0-9.]+);");
            Assert.True(match.Success, $"SceneLamp.fxh states no {name}");
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Game.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("Game.sln not found above the test's output");
        }

        //SceneLamp.fxh's SceneLightFalloff for a shaded lamp, line for line: the falloff to the range, times the soft cut at
        //the rim's plane, which tips LAMP_TILT towards the arena's axis
        private static float ShadedFalloff(Vector3 light, Vector3 point, float gain, float spill, float sin, float cos)
        {
            Vector3 toLight = light - point;
            float dist = toLight.Length();
            float atten = MathHelper.Clamp(1f - dist / ForestLampLayout.RANGE, 0f, 1f);
            Vector2 inward = -Vector2.Normalize(new Vector2(light.X, light.Z));
            Vector3 axis = new(inward.X * sin, -cos, inward.Y * sin);
            float below = MathHelper.Clamp(-Vector3.Dot(toLight, axis) / dist * gain + spill, 0f, 1f);
            return atten * atten * below;
        }

        [Fact]
        public void TheLightIsCutWhereTheTippedShadesRimIs()
        {
            //The shader tips every shaded lamp's cut LAMP_TILT towards the arena's axis, read off the light's position; the
            //meshes tip each shade SHADE_TILT towards its own arm. One angle (the shader's sine and cosine are SHADE_TILT's), and
            //at every lamp the two axes within its arm's skew and its pole's lean of each other, so the light goes out at the
            //rim that is drawn. Seen to fire with the meshes' tilt the other way (an axis 50 degrees off)
            Assert.Equal(MathF.Sin(ForestLampLayout.SHADE_TILT), ShaderConstant("LAMP_TILT_SIN"), 4);
            Assert.Equal(MathF.Cos(ForestLampLayout.SHADE_TILT), ShaderConstant("LAMP_TILT_COS"), 4);

            float worst = 0f;
            foreach (ForestLampLayout.Lamp lamp in Layout0.Lamps)
            {
                Vector3 meshAxis = Vector3.Normalize(Vector3.TransformNormal(Vector3.TransformNormal(-Vector3.UnitY, ForestLampLayout.SHADE_TURN), lamp.World));
                Vector2 inward = -Vector2.Normalize(new Vector2(lamp.Bulb.X, lamp.Bulb.Z));
                Vector3 lightAxis = new(inward.X * MathF.Sin(ForestLampLayout.SHADE_TILT), -MathF.Cos(ForestLampLayout.SHADE_TILT), inward.Y * MathF.Sin(ForestLampLayout.SHADE_TILT));
                float degrees = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector3.Dot(meshAxis, lightAxis), -1f, 1f)));
                Assert.True(degrees < 12f, $"the shade at {lamp.Foot.X:F1}, {lamp.Foot.Z:F1} is tipped {degrees:F1} degrees off the light's cut");
                worst = MathF.Max(worst, degrees);
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "the shades' axes within {0:F1} degrees of their lights' cuts", worst));
        }

        [Fact]
        public void TheLampsLightTheWholeCluster()
        {
            //Every ball of every shipped level, moved into the forest, takes the lamps' light (the falloff summed over the five,
            //SceneLamp.fxh's cut copied above): none is left in the dusk alone, and the top of the cluster takes about as much as
            //its bottom, so the field reads as one colour under the lamps, not warm at its foot and cold at its crown. Seen to
            //fire with the shade hanging straight down (LAMP_TILT 0: a ball of Amphora takes 0.049)
            float gain = ShaderConstant("LAMP_RIM_GAIN"), spill = ShaderConstant("LAMP_RIM_SPILL");
            float sin = ShaderConstant("LAMP_TILT_SIN"), cos = ShaderConstant("LAMP_TILT_COS");
            ForestLampLayout layout = Layout0;
            float least = float.PositiveInfinity, worstRatio = float.PositiveInfinity, most = 0f;
            string worstLevel = "";
            foreach (string file in Shipped.LevelFiles())
            {
                Opening open = Open(file, 16f / 9f);
                var lit = new List<(float Y, float Light)>();
                foreach (Vector3 ball in open.Balls)
                {
                    float light = 0f;
                    foreach (ForestLampLayout.Lamp lamp in layout.Lamps) light += ShadedFalloff(lamp.Bulb, ball, gain, spill, sin, cos);
                    Assert.True(light > 0.05f, $"{open.Level}: the ball at {ball.X:F1}, {ball.Y:F1}, {ball.Z:F1} takes {light:F3} of the lamps");
                    least = MathF.Min(least, light);
                    most = MathF.Max(most, light);
                    lit.Add((ball.Y, light));
                }

                if (lit.Count < 9) continue;
                lit.Sort((a, b) => a.Y.CompareTo(b.Y));
                int third = lit.Count / 3;
                float bottom = lit.Take(third).Average(b => b.Light), top = lit.Skip(lit.Count - third).Average(b => b.Light);
                float ratio = top / bottom;
                Assert.True(ratio > 0.6f, $"{open.Level}: the top third of the cluster takes {top:F3} of the lamps, its bottom {bottom:F3}");
                if (ratio < worstRatio)
                {
                    worstRatio = ratio;
                    worstLevel = open.Level;
                }
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "a ball takes {0:F3} to {1:F3} of the lamps; the top third against the bottom at worst {2:F2} ({3})",
                least, most, worstRatio, worstLevel));
        }

        [Fact]
        public void TheLampsAreShadedLightsWithASlotLeftForABlast()
        {
            //Fewer lamps than SceneLights' slots, so a blast's flash always finds one; each range negative, which every scene-light
            //loop reads as a shaded lamp (SceneLamp.fxh: no light on the shut side of its rim), its magnitude RANGE; the light at the bulb
            Assert.True(ForestLampLayout.COUNT < SceneLights.MaxLights);
            ForestLampLayout layout = Layout0;
            var position = new Vector3[SceneLights.MaxLights];
            var colour = new Vector3[SceneLights.MaxLights];
            var range = new float[SceneLights.MaxLights];
            int count = layout.Lights(3.2f, position, colour, range, SceneLights.MaxLights - 1);
            Assert.Equal(ForestLampLayout.COUNT, count);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(-ForestLampLayout.RANGE, range[i]);
                Assert.Equal(layout.Lamps[i].Bulb, position[i]);
                Assert.True(colour[i].X > colour[i].Y && colour[i].Y > colour[i].Z, "a lamp's light is not warm");
            }
            Assert.Equal(2, layout.Lights(0f, position, colour, range, 2));
        }

        [Fact]
        public void TheBallsTakeTheLampsLightWithoutTheirHighlights()
        {
            //The instanced effect's loop gates a slot's highlight on SceneLampHighlight for a shaded lamp alone (every other
            //light keeps it on every surface), the renderer defaults it to 1 and states it every draw (the shared effect would
            //carry a 0 over onto the island's stone, whose warm sheen is the lamps' highlight), and the balls' renderers set 0.
            //Read off the sources, there being no device here. Seen to fire with BallRenderSet's 0 taken out, with the gate on
            //the range's other sign, and with the renderer's per-draw SetValue taken out
            string lighting = Source("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "InstancedModel", "Lighting.fxh");
            Assert.Matches(@"float SceneLampHighlight = 1;", lighting);
            Assert.Matches(@"\[branch\] if \(SceneLightRange\[i\] > 0\.0 \|\| SceneLampHighlight > 0\.0\)\s*\{\s*float dotH", lighting);

            string renderer = Source("BS3DLibs", "Prazsky.Core", "Render", "InstancedModelRenderer.cs");
            Assert.Matches(@"public float SceneLampHighlight \{ get; set; \} = 1f;", renderer);
            Assert.Matches(@"_sceneLampHighlightParam\.SetValue\(SceneLampHighlight\);", renderer);

            string balls = Source("BS3DLibs", "Prazsky.BS3D", "BallRenderSet.cs");
            Assert.Matches(@"_renderers\[lod\] = new InstancedModelRenderer\([^;]*SceneLampHighlight = 0f\s*\};", balls);
        }

        private static string Source(params string[] path) => File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(path).ToArray()));

        [Fact]
        public void TheOldLampStuttersAndTheOthersOnlyBreathe()
        {
            //Pure functions of the clock: the same value a period on; the old lamp dips deep inside its stutter and nowhere
            //else, the rest never leave their breath. Seen to fire with the stutter on every lamp
            var deepest = new float[3] { 1f, 1f, 1f };
            for (float t = 0f; t < 3f * ForestLampLayout.STUTTER_PERIOD; t += 0.01f)
                for (int lamp = 0; lamp < ForestLampLayout.COUNT; lamp++)
                {
                    float b = ForestLampLayout.Brightness(lamp, t);
                    Assert.Equal(b, ForestLampLayout.Brightness(lamp, t));
                    int period = (int)(t / ForestLampLayout.STUTTER_PERIOD);
                    float into = t - period * ForestLampLayout.STUTTER_PERIOD;
                    if (lamp == ForestLampLayout.OLD_LAMP && into < ForestLampLayout.STUTTER_SECONDS) deepest[period] = MathF.Min(deepest[period], b);
                    else Assert.InRange(b, 0.939f, 1.001f);
                }

            //Every period's stutter dips deep, and none puts the lamp out
            foreach (float dip in deepest)
            {
                Assert.True(dip < 0.6f, $"the old lamp's stutter only dips to {dip:F2}");
                Assert.True(dip > 0.2f, $"the old lamp goes out ({dip:F2}): a filament cools, it does not switch");
            }
        }

        [Fact]
        public void TheRotorTurnsOnTheClock()
        {
            //A pure function of time: in [0, 2 pi), the same a turn's period on, and turning at ROTOR_SPEED between
            float period = MathHelper.TwoPi / ForestTurbineLayout.ROTOR_SPEED;
            for (float t = 0f; t < 200f; t += 0.37f)
            {
                float a = ForestTurbineLayout.RotorAngle(t);
                Assert.InRange(a, 0f, MathHelper.TwoPi);
                float again = ForestTurbineLayout.RotorAngle(t + period);
                Assert.True(MathF.Min(MathF.Abs(again - a), MathHelper.TwoPi - MathF.Abs(again - a)) < 1e-3f, $"the rotor at {t:F2} s is not where it is a turn later");
                float step = ForestTurbineLayout.RotorAngle(t + 0.01f) - a;
                if (step < 0f) step += MathHelper.TwoPi;
                Assert.InRange(step, ForestTurbineLayout.ROTOR_SPEED * 0.01f - 1e-4f, ForestTurbineLayout.ROTOR_SPEED * 0.01f + 1e-4f);
            }

            //And the rotor's frame keeps the hub where the tower's frame puts it, whatever the turn
            Matrix tower = ForestTurbineLayout.World(Forest);
            Vector3 hub = Vector3.Transform(ForestTurbineLayout.HUB, tower);
            foreach (float t in new[] { 0f, 1.3f, 77f })
                Assert.True(Vector3.Distance(Vector3.Transform(Vector3.Zero, ForestTurbineLayout.RotorWorld(tower, t)), hub) < 1e-3f);
        }

        [Fact]
        public void TheAviationLightFlashesTwiceAPeriod()
        {
            //On, a short gap, on again, then dark to the period's end: the turbines' "W-rot" flashing, smooth at its edges
            float p = ForestTurbineLayout.BEACON_PERIOD;
            foreach (float k in new[] { 0f, 1f, 5f })
            {
                Assert.Equal(1f, ForestTurbineLayout.Beacon(k * p + 0.5f), 3);
                Assert.Equal(0f, ForestTurbineLayout.Beacon(k * p + 1.25f), 3);
                Assert.Equal(1f, ForestTurbineLayout.Beacon(k * p + 2.0f), 3);
                Assert.Equal(0f, ForestTurbineLayout.Beacon(k * p + 3.2f), 3);
            }
            //No step from one frame to the next of more than a 60 Hz frame's share of the ramp
            for (float t = 0f; t < p; t += 1f / 240f)
                Assert.True(MathF.Abs(ForestTurbineLayout.Beacon(t + 1f / 240f) - ForestTurbineLayout.Beacon(t)) <= 1f / 240f / ForestTurbineLayout.BEACON_RAMP + 1e-3f);
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void TheTurbineTurnsInTheOpeningPlayViewsSky(float aspect)
        {
            //From the first level's opening pose the hub stands in the frame, beside the cluster and above the lens, inside the
            //haze distance (so the far fade has not taken it on any tier), and the rotor's top over the hub. Seen to fire with the
            //turbine on the bearing behind the lens
            Opening open = Open(Shipped.LevelFiles().First(), aspect);
            (_, Vector3 lens, Vector3 target, float fov) = open.Poses[0];
            Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);
            Matrix tower = ForestTurbineLayout.World(Forest);
            Vector3 hub = Vector3.Transform(ForestTurbineLayout.HUB, tower);
            Vector3 ndc = Project(hub, viewProjection, out bool ahead);
            Assert.True(InFrame(ndc, ahead), $"at {aspect:F2} the turbine's hub is out of the frame at {ndc.X:F2}, {ndc.Y:F2}");
            Assert.True(MathF.Abs(ndc.X) > 0.4f, $"at {aspect:F2} the turbine stands behind the cluster at {ndc.X:F2}");
            float distance = Vector3.Distance(lens, hub);
            Assert.True(distance < Forest.HorizonHazeDistance, $"the turbine stands {distance:F0} from the lens, past the haze distance");
            float elevation = MathF.Atan2(hub.Y - lens.Y, new Vector2(hub.X - lens.X, hub.Z - lens.Z).Length());
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F2}: the hub at {1:F2}, {2:F2} of the frame, {3:F0} away, {4:F1} degrees up",
                aspect, ndc.X, ndc.Y, distance, MathHelper.ToDegrees(elevation)));
        }

        [Fact]
        public void TheFarStandLeavesTheTurbinesPadOpen()
        {
            //No tree of the far stand within PAD_RADIUS of the tower's foot, and the stand still stands round it. Seen to fire
            //with the pad's test taken out of ForestFarStand.Plant
            Vector2 foot = ForestTurbineLayout.FootXZ;
            ForestScatter planting = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED, Forest, (x, z) => TerrainMirror.Forest(x, z, Forest));
            int near = 0;
            foreach (Vector4 place in ForestFarStand.PlacesOf(Forest, planting))
            {
                float d = Vector2.Distance(new Vector2(place.X, place.Z), foot);
                Assert.True(d >= ForestTurbineLayout.PAD_RADIUS, $"a far tree stands {d:F1} from the turbine's foot");
                if (d < ForestTurbineLayout.PAD_RADIUS * 3f) near++;
            }
            Assert.True(near > 0, "the turbine stands in no wood at all");
        }
    }
}
