using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using static BS3D.Tests.PlayViewOpenings;

namespace BS3D.Tests
{
    /// <summary>
    /// The forest's lamps round the island and the wind turbine on the hills (#1166, <see cref="ForestLampLayout"/>,
    /// <see cref="ForestTurbineLayout"/>): each pole stands where the planting leaves it room at every roll of the wood, outside
    /// every lens that plays and before no ball and no gun in any play view, the opening view looking between two of them; the
    /// lights are shaded lamps with a slot left for a blast; the old lamp stutters and the rest breathe; the rotor turns and the
    /// aviation light flashes as pure functions of the clock; the turbine stands in the opening play view's sky, inside the haze,
    /// on a pad the far stand leaves open.
    /// </summary>
    public class ForestLampsTests
    {
        private readonly ITestOutputHelper _output;

        public ForestLampsTests(ITestOutputHelper output) => _output = output;

        public static readonly IEnumerable<object[]> Aspects = ASPECTS.Select(a => new object[] { a });

        private static readonly ForestSceneConfig Forest = new();

        private static ForestLampLayout Layout(int seedOffset) => ForestLampLayout.Plan(Forest,
            ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED + seedOffset, Forest, (x, z) => TerrainMirror.Forest(x, z, Forest)));

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
            //comes within CLEARANCE of, and stays on its ring. Seen to fire with the poles held at their nominal spots (Room
            //negative at most rolls: a spruce's skirt, a boulder at the foot)
            float least = float.PositiveInfinity, farthest = 0f;
            for (int seed = 0; seed < 24; seed++)
            {
                ForestLampLayout layout = Layout(seed);
                Assert.Equal(ForestLampLayout.COUNT, layout.Lamps.Count);
                foreach (ForestLampLayout.Lamp lamp in layout.Lamps)
                {
                    Assert.True(lamp.Room >= ForestLampLayout.CLEARANCE,
                        $"roll {seed}: the pole at {lamp.Foot.X:F1}, {lamp.Foot.Z:F1} has {lamp.Room:F2} of room, under {ForestLampLayout.CLEARANCE}");
                    float r = new Vector2(lamp.Foot.X, lamp.Foot.Z).Length();
                    Assert.InRange(r, ForestLampLayout.RING_RADIUS - ForestLampLayout.RING_GIVE - 1e-3f, ForestLampLayout.RING_RADIUS + ForestLampLayout.RING_GIVE + 1e-3f);
                    least = MathF.Min(least, lamp.Room);
                    farthest = MathF.Max(farthest, MathF.Abs(r - ForestLampLayout.RING_RADIUS));
                }
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "least room {0:F2}, farthest off the ring {1:F2}", least, farthest));
        }

        [Fact]
        public void TheLampsStandOnTheGroundAtDifferentHeights()
        {
            //Each foot sunk under the ground's mirror by SINK and its own sink, so no pole floats; and not all at one height
            ForestLampLayout layout = Layout(0);
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
            ForestLampLayout layout = Layout(0);
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
            //From the first level's opening pose (the lens on +Z looking at the cluster), no bulb stands near the frame's middle
            //column, where it would shine behind the cluster's middle: the two far lamps flank it. Seen to fire with the ring
            //turned by half a step (a lamp straight behind the cluster)
            Opening open = Open(Shipped.LevelFiles().First(), aspect);
            (_, Vector3 lens, Vector3 target, float fov) = open.Poses[0];
            Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);
            int flanking = 0;
            foreach (ForestLampLayout.Lamp lamp in Layout(0).Lamps)
            {
                Vector3 ndc = Project(lamp.Bulb, viewProjection, out bool ahead);
                if (!ahead || MathF.Abs(ndc.X) > 1f || MathF.Abs(ndc.Y) > 1f) continue;
                Assert.True(MathF.Abs(ndc.X) > 0.3f, $"at {aspect:F2} a bulb stands at {ndc.X:F2} of the half-frame, behind the cluster's middle");
                flanking++;
            }
            Assert.True(flanking >= 2 || aspect < 1.5f, $"at {aspect:F2} only {flanking} lamps flank the cluster in the opening view");
        }

        [Fact]
        public void TheLampsAreShadedLightsWithASlotLeftForABlast()
        {
            //Fewer lamps than SceneLights' slots, so a blast's flash always finds one; each range negative, which every scene-light
            //loop reads as a shaded lamp (SceneLamp.fxh: no light above its rim), its magnitude RANGE; the light at the bulb
            Assert.True(ForestLampLayout.COUNT < SceneLights.MaxLights);
            ForestLampLayout layout = Layout(0);
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
