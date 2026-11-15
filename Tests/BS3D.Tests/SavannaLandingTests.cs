using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna's grass home (#1108, <see cref="SavannaLanding"/>): the owner's "a fly-through that starts low over the ground
    /// and ends at the position where the game camera stands behind the cannon", held as the meadow's brook home is
    /// (<see cref="MeadowLandingTests"/>) over the very plain the game plants (<see cref="SavannaScatter"/> planted with no
    /// device, which uploads nothing) and a sweep of gameplay poses round the arena: it is found for nearly every pose, it
    /// opens low over the grass among the trees, it ends exactly on the pose with the game's own lens and no bank, it never
    /// comes near the ground, the bank round the island, the arena, a campfire or anything planted, and its ride stays inside
    /// the limits the meadow's and the cities' were held to.
    /// </summary>
    public class SavannaLandingTests
    {
        private readonly ITestOutputHelper _output;

        public SavannaLandingTests(ITestOutputHelper output) => _output = output;

        private const float FieldOfView = MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW;

        //The gameplay pose a savanna level opens on (the Gallery's first level, Heart: the lens 30.0 out on the gun's bearing,
        //90 degrees, 7.3 under the axis's zero, looking at the field's aim height, -1.6), which the grass home lands on
        internal static readonly Vector3 GamePosition = new(0f, -7.3f, 30f);
        internal static readonly Vector3 GameTarget = new(0f, -1.6f, 0f);

        //The levels' own poses: a level opens with the gun on its bearing, 90 degrees, and the savanna's fields (15 and 17
        //wide) put the lens 30 to 34 out; every one gets a flight home in every layout of LEVEL_LAYOUTS
        private static readonly float[] LevelRadii = { 30f, 32f, 34f };
        private const int LEVEL_LAYOUTS = 24;

        //And a sweep round the arena, for what the builder makes of a pose anywhere: the axes exactly, as the meadow's sweep
        //takes them, at radii round the savanna's own lens
        private static readonly float[] Radii = { 29f, 33f, 37f };
        private const int BEARINGS = 12;
        private const int LAYOUTS = 6;

        //How much of the sweep must get a flight home: the rest fall back to the old reel and its tour leg. 135 of the 144 did
        //(measured): the nine are on bearings no savanna level opens on, where the groves' spheres reach the island all round
        //the pose and close every way in at a lens this low. A pose that stands in a campfire's clearance itself is never
        //landed on and is not counted (no game lens stands there: on the gun's bearing the nearest fire is 7.1 off the line the
        //lens stands on).
        private const float MIN_BUILT_SHARE = 0.9f;

        //The ride: the meadow's limits (MeadowLandingTests) for the view's turn and pitch, the bank and the landing, and the
        //savanna's own for the speed (its cruise of up to 26 round the island at the eased clock's peak, 1.15 of it, about the
        //28 the arid shots that look at the ground under the lens are held to) and the length
        private const float MAX_SPEED = 30f;
        private const float MAX_YAW_RATE = 170f;
        private const float MAX_PITCH_RATE = 80f;
        private const float MAX_ROLL_DEGREES = 8f;
        private const float MAX_LANDING_SPEED = 8f;
        private const float MIN_SECONDS = 3.5f, MAX_SECONDS = 6.5f;
        private const float MAX_VERTICAL_ACCELERATION = 20f;

        //What is planted on the bank round the island stands within this of the axis, and nothing else does (the plain's tufts
        //start 40 out, SavannaSceneConfig's TuftMinRadius): the bank's two meshes, its 90 tufts against the wall and its 18
        //stones at its foot (SavannaScatter's BERM_TUFTS and BERM_STONES)
        private const float BANK_DRESSING = 34f;
        private const int BANK_SOLIDS = 2 + 90 + 18;

        //The opening's share of the shot that stays low over the grass, and the share of the flights that pass a tree before
        //the last quarter of the shot, where they settle onto the pose
        private const float OPENING = 0.25f;
        private const float AMONG_TREES_UNTIL = 0.75f, AMONG_TREES_SHARE = 0.9f;

        //How finely the shot is walked, and how far from everything every pose must stay (the builder keeps 1.2 at its points,
        //1.5 off a fire; the lens runs a spline through them)
        private const int POSES = 1500;
        private const float CLEARANCE = 0.5f;

        /// <summary>A savanna as the game plants it, with no device.</summary>
        internal sealed class Plain
        {
            public SavannaSceneConfig Config;
            public SavannaLand Land;
            public int Seed;
        }

        /// <summary>
        /// The plain the game plants at scene seed <paramref name="layout"/> (<c>sceneseed=</c>, the scatter's seed being its
        /// default plus the scene's offset), over the shipped config: the campfires' ring and their hearths reserved as
        /// SavannaBackdrop reserves them, every plant where the scatter puts it.
        /// </summary>
        internal static Plain Plant(int layout)
        {
            var config = new SavannaSceneConfig();
            float Height(float x, float z) => TerrainMirror.Savanna(x, z, config);

            //SavannaBackdrop.BuildSavannaScatter's own reservation: each fire's hearth, the stones' ring and a stone, and a unit
            CampfireConfig cf = config.Campfire;
            float hearth = cf.FlameSize * (cf.StoneRingScale + cf.StoneSizeScale) + 1f;
            var fires = new Vector3[SavannaBackdrop.CampfireRingCount(config)];
            var reserved = new List<ScatterSpacing.Footprint>();
            for (int i = 0; i < fires.Length; i++)
            {
                fires[i] = SavannaBackdrop.CampfireRingPoint(config, i, cf.HeightAboveTerrain);
                reserved.Add(new ScatterSpacing.Footprint(fires[i].X, fires[i].Z, hearth));
            }

            int seed = SavannaScatter.DEFAULT_SEED + layout;
            var planting = new SavannaScatter(null, config, Height, reserved, seed);
            return new Plain { Config = config, Land = SavannaLand.Of(config, planting, Height, fires), Seed = seed };
        }

        private static readonly Dictionary<int, Plain> Plains = new();

        internal static Plain Planted(int layout)
        {
            lock (Plains)
            {
                if (!Plains.TryGetValue(layout, out Plain plain)) Plains[layout] = plain = Plant(layout);
                return plain;
            }
        }

        private static Vector3 Pose(int bearing, float radius)
        {
            float angle = MathHelper.TwoPi * bearing / BEARINGS;
            return new Vector3(MathF.Cos(angle) * radius, GamePosition.Y, MathF.Sin(angle) * radius);
        }

        private static IntroShot Home(Plain plain, Vector3 pose, int seed) =>
            SavannaLanding.Build(plain.Land, pose, GameTarget, FieldOfView, new Random(seed));

        //How far a lens at a point stands off a campfire's hearth and flame: off the capsule from its foot up the flame, less
        //its reach
        private static float OffTheFires(SavannaLand land, Vector3 p)
        {
            float nearest = float.MaxValue;
            foreach (Vector3 fire in land.Fires)
                nearest = MathF.Min(nearest, IntroPaths.DistanceToSegment(p, fire, fire + Vector3.Up * land.FireHeight) - land.FireReach);
            return nearest;
        }

        //Whether a pose stands in a fire's clearance itself, where no flight can land
        private static bool BesideAFire(Plain plain, Vector3 pose) => OffTheFires(plain.Land, pose) < SavannaLanding.FIRE_MARGIN;

        //Every pose of the sweep with the layout it is flown over, built once for the tests that walk them
        private static readonly Lazy<List<(int Layout, int Bearing, float Radius, Plain Plain, IntroShot Shot)>> Sweep =
            new(() =>
            {
                var sweep = new List<(int, int, float, Plain, IntroShot)>();
                for (int layout = 0; layout < LAYOUTS; layout++)
                {
                    Plain plain = Planted(layout);
                    for (int b = 0; b < BEARINGS; b++)
                        foreach (float r in Radii)
                            sweep.Add((layout, b, r, plain, Home(plain, Pose(b, r), layout * 100 + b)));
                }
                return sweep;
            });

        [Fact]
        public void A_flight_home_is_found_for_every_levels_own_pose_in_every_layout()
        {
            int built = 0, counted = 0;
            for (int layout = 0; layout < LEVEL_LAYOUTS; layout++)
                foreach (float radius in LevelRadii)
                {
                    counted++;
                    if (Home(Planted(layout), new Vector3(0f, GamePosition.Y, radius), layout * 7 + (int)radius) != null) built++;
                    else _output.WriteLine($"none: layout {layout}, {radius} out");
                }

            _output.WriteLine($"a flight home for {built} of {counted} level poses");
            Assert.Equal(counted, built);
        }

        [Fact]
        public void A_flight_home_is_found_for_most_poses_round_the_arena()
        {
            int built = 0, counted = 0, beside = 0;
            foreach (var (_, bearing, radius, plain, shot) in Sweep.Value)
            {
                if (BesideAFire(plain, Pose(bearing, radius)))
                {
                    Assert.Null(shot);
                    beside++;
                    continue;
                }

                counted++;
                if (shot != null) built++;
                else _output.WriteLine($"none: bearing {bearing * 30}, radius {radius}");
            }

            _output.WriteLine($"a flight home for {built} of {counted} poses ({beside} beside a fire)");
            Assert.True(built >= MIN_BUILT_SHARE * counted, $"a flight home for {built} of {counted} poses");
        }

        [Fact]
        public void It_lands_on_the_pose_with_the_games_lens_and_level()
        {
            foreach (var (layout, bearing, radius, _, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                Vector3 pose = Pose(bearing, radius);
                shot.Pose(1f, out Vector3 position, out Vector3 target);
                string where = $"layout {layout}, bearing {bearing}, radius {radius}";
                Assert.True(shot.Lands);
                Assert.Equal(SavannaLanding.NAME, shot.Name);
                Assert.True(Vector3.Distance(position, pose) < 1e-3f, $"{where}: lands {Vector3.Distance(position, pose):F4} off the pose");
                Assert.True(Vector3.Distance(target, GameTarget) < 1e-3f, $"{where}: looks {Vector3.Distance(target, GameTarget):F4} off the pose's look");
                Assert.Equal(FieldOfView, shot.FieldOfViewAt(1f), 4);
                Assert.Equal(0f, shot.RollAt(1f), 4);
            }
        }

        [Fact]
        public void It_opens_low_over_the_grass_among_the_trees()
        {
            float lowest = float.MaxValue, highest = 0f;
            int fewest = int.MaxValue, flights = 0, amongTrees = 0;
            foreach (var (layout, bearing, radius, plain, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                string where = $"layout {layout}, bearing {bearing}, radius {radius}";

                //It opens among the groves, low over the grass
                shot.Pose(0f, out Vector3 first, out _);
                Assert.True(Vector2.Distance(new Vector2(first.X, first.Z), new Vector2(Pose(bearing, radius).X, Pose(bearing, radius).Z)) >= SavannaLanding.START_RUN_MIN - 0.5f,
                    $"{where}: opens too near the pose");
                Assert.True(new Vector2(first.X, first.Z).Length() >= 40f, $"{where}: opens inside the plain's grass");
                float over = first.Y - plain.Land.Height(first.X, first.Z);
                lowest = MathF.Min(lowest, over);
                highest = MathF.Max(highest, over);

                //And flies its first OPENING of the way low, and past trees before it settles onto the pose: the plan up to
                //AMONG_TREES_UNTIL of the shot, and the trees beside it
                var plan = new List<Vector2>();
                for (int i = 0; i <= POSES * AMONG_TREES_UNTIL; i++)
                {
                    shot.Pose(i / (float)POSES, out Vector3 p, out _);
                    plan.Add(new Vector2(p.X, p.Z));
                    float over0 = p.Y - plain.Land.Height(p.X, p.Z);
                    if (i <= POSES * OPENING) Assert.True(over0 <= SavannaLanding.ABOVE_MAX + 2f, $"{where}: {over0:F1} over the grass at {i / (float)POSES:F2}");
                }
                (int left, int right) = SavannaLanding.Passed(plan, plan.Count - 1, plain.Land.Trees);
                fewest = Math.Min(fewest, left + right);
                flights++;
                if (left + right > 0) amongTrees++;
            }

            _output.WriteLine($"opens {lowest:F2} to {highest:F2} over the grass; the fewest trees passed {fewest}; {amongTrees} of {flights} pass a tree");
            Assert.True(amongTrees >= AMONG_TREES_SHARE * flights, $"{amongTrees} of {flights} flights pass a tree");
            Assert.True(lowest >= SavannaLanding.ABOVE_MIN - 0.1f, $"opens {lowest:F2} over the grass");
            Assert.True(highest <= SavannaLanding.ABOVE_MAX + 2f, $"opens {highest:F2} over the grass");
        }

        [Fact]
        public void It_never_comes_near_the_ground_the_bank_the_arena_a_fire_or_anything_planted()
        {
            int walked = 0;
            float nearestFire = float.MaxValue;
            foreach (var (layout, bearing, radius, plain, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                walked++;
                SavannaLand land = plain.Land;
                Vector3 pose = Pose(bearing, radius);

                //The solids within reach of the shot's plan, the bank's left to the ground above
                Vector2 low = new(float.MaxValue), high = new(float.MinValue);
                for (int i = 0; i <= 100; i++)
                {
                    shot.Pose(i / 100f, out Vector3 q, out _);
                    low = Vector2.Min(low, new Vector2(q.X, q.Z));
                    high = Vector2.Max(high, new Vector2(q.X, q.Z));
                }
                var near = new List<PlantFigure>();
                foreach (PlantFigure solid in land.Solids)
                {
                    if (SavannaLanding.Obstacles.IsBank(solid)) continue;
                    float reach = MathF.Max(solid.Reach, solid.Stem) + 2f;
                    if (MathF.Max(solid.Root.X, solid.Crown.X) + reach < low.X || MathF.Min(solid.Root.X, solid.Crown.X) - reach > high.X
                        || MathF.Max(solid.Root.Z, solid.Crown.Z) + reach < low.Y || MathF.Min(solid.Root.Z, solid.Crown.Z) - reach > high.Y) continue;
                    near.Add(solid);
                }

                for (int i = 0; i <= POSES; i++)
                {
                    shot.Pose(i / (float)POSES, out Vector3 p, out _);
                    string where = $"layout {layout}, bearing {bearing}, radius {radius}, at {i}/{POSES} ({p.X:F1}, {p.Y:F1}, {p.Z:F1})";

                    //Over the ground and over the bank round the island as it is built (IslandBerm, off the planting's seed)
                    float ground = MathF.Max(land.Height(p.X, p.Z), IslandBerm.SurfaceY(land.Height, plain.Seed, p.X, p.Z));
                    Assert.True(p.Y - ground >= CLEARANCE, $"{where}: {p.Y - ground:F2} over the ground");

                    //Never nearer the axis than the pose itself (the island, the gun, the glass and the cluster all stand nearer),
                    //nor ever inside the arena's keep-out but for that arrival
                    float fromAxis = new Vector2(p.X, p.Z).Length();
                    Assert.True(fromAxis >= MathF.Min(IntroGround.ARENA_KEEP_OUT, radius) - 1f, $"{where}: {fromAxis:F1} from the axis");
                    Assert.True(fromAxis >= ArenaIsland.RADIUS + 2f, $"{where}: over the island");

                    //Over the bank's grass and stones, which stand under the game's own lens, never lower than the pose (less the
                    //builder's half unit and the spline's)
                    if (fromAxis < BANK_DRESSING) Assert.True(p.Y >= pose.Y - 0.6f, $"{where}: {pose.Y - p.Y:F2} under the pose over the bank");

                    //Off every campfire's hearth and flame
                    float offFires = OffTheFires(land, p);
                    nearestFire = MathF.Min(nearestFire, offFires);
                    Assert.True(offFires >= 1f, $"{where}: {offFires:F2} off a campfire");

                    //And off everything planted, at its whole figure, but the bank and what stands on it (above): by CLEARANCE, or
                    //where the pose itself stands nearer a solid (a tree's loose sphere reaching the game's lens), by no less than
                    //the pose does, less the builder's half unit and the spline's
                    foreach (PlantFigure solid in near)
                    {
                        float off = MathF.Min(IntroPaths.DistanceToSegment(p, solid.Root, solid.Crown) - solid.Stem, Vector3.Distance(p, solid.Crown) - solid.Reach);
                        float poseOff = MathF.Min(IntroPaths.DistanceToSegment(pose, solid.Root, solid.Crown) - solid.Stem, Vector3.Distance(pose, solid.Crown) - solid.Reach);
                        Assert.True(off >= MathF.Min(CLEARANCE, poseOff - 0.6f), $"{where}: {off:F2} off a solid at {solid.Crown} (the pose {poseOff:F2})");
                    }
                }
            }

            _output.WriteLine($"{walked} flights walked; the nearest a fire's reach {nearestFire:F2}");
            Assert.True(walked > 0);
        }

        [Fact]
        public void A_pose_a_trees_loose_sphere_reaches_is_still_landed_on()
        {
            //A solid's sphere is its mesh's bounding sphere, up to 31 round a big acacia whose crown reaches 17, so the trees 60
            //out reach a lens 40 out and more: such a pose is not clear itself by MARGIN, and the grass home holds that solid at
            //the pose's own clearance of it instead (SavannaLanding's POSE_SLACK), never nearer it than the pose less a little
            int inside = 0, landed = 0;
            for (int layout = 0; layout < LAYOUTS; layout++)
            {
                Plain plain = Planted(layout);
                for (int b = 0; b < BEARINGS; b++)
                    foreach (float radius in new[] { 41f, 45f })
                    {
                        Vector3 pose = Pose(b, radius);
                        if (BesideAFire(plain, pose)) continue;

                        var reaching = new List<(PlantFigure Solid, float PoseOff)>();
                        foreach (PlantFigure solid in plain.Land.Solids)
                        {
                            if (SavannaLanding.Obstacles.IsBank(solid)) continue;
                            float poseOff = SavannaLanding.Obstacles.Off(solid, pose);
                            if (poseOff < 1.2f) reaching.Add((solid, poseOff));
                        }
                        if (reaching.Count == 0) continue;

                        inside++;
                        IntroShot shot = Home(plain, pose, layout * 100 + b);
                        if (shot == null) continue;

                        landed++;
                        for (int i = 0; i <= POSES; i++)
                        {
                            shot.Pose(i / (float)POSES, out Vector3 p, out _);
                            foreach ((PlantFigure solid, float poseOff) in reaching)
                                Assert.True(SavannaLanding.Obstacles.Off(solid, p) >= MathF.Min(CLEARANCE, poseOff - 0.6f),
                                    $"layout {layout}, bearing {b}, radius {radius}: nearer a solid the pose's sphere reaches than the pose");
                        }
                    }
            }

            _output.WriteLine($"{landed} of {inside} poses a sphere reaches landed on");
            Assert.True(inside >= 10, $"only {inside} poses a sphere reaches");
            Assert.True(landed >= inside / 2, $"{landed} of {inside} poses a sphere reaches landed on");
        }

        [Fact]
        public void Only_the_bank_and_what_stands_on_it_are_held_as_the_arena_is()
        {
            //The grass home holds the bank and its dressing as the arena is, by the pose's distance and height, not by their
            //spheres (SavannaLanding's BANK_REACH): which is right only while they are all that stands near the island
            for (int layout = 0; layout < LAYOUTS; layout++)
            {
                int bank = 0;
                foreach (PlantFigure solid in Planted(layout).Land.Solids)
                {
                    float out0 = new Vector2(solid.Root.X, solid.Root.Z).Length();
                    if (SavannaLanding.Obstacles.IsBank(solid))
                    {
                        bank++;
                        Assert.True(out0 < BANK_DRESSING, $"layout {layout}: a solid {out0:F1} out held as the bank");
                    }
                    else Assert.True(out0 >= 39f, $"layout {layout}: a solid {out0:F1} out, beside the bank");
                }
                Assert.Equal(BANK_SOLIDS, bank);
            }
        }

        [Fact]
        public void The_ride_stays_inside_its_limits()
        {
            float longest = 0f, shortest = float.MaxValue, fastest = 0f, yawPeak = 0f, pitchPeak = 0f, landing = 0f;
            foreach (var (layout, bearing, radius, _, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                string where = $"layout {layout}, bearing {bearing}, radius {radius}";
                Assert.InRange(shot.Seconds, MIN_SECONDS, MAX_SECONDS);
                longest = MathF.Max(longest, shot.Seconds);
                shortest = MathF.Min(shortest, shot.Seconds);

                float dt = shot.Seconds / POSES;
                shot.Pose(0f, out Vector3 last, out Vector3 lastTarget);
                float lastYaw = Yaw(last, lastTarget), lastPitch = Pitch(last, lastTarget);

                for (int i = 1; i <= POSES; i++)
                {
                    float t = i / (float)POSES;
                    shot.Pose(t, out Vector3 position, out Vector3 target);
                    float speed = Vector3.Distance(position, last) / dt;
                    float yaw = Yaw(position, target), pitch = Pitch(position, target);

                    Assert.True(speed <= MAX_SPEED, $"{where}: {speed:F0} u/s at {t:F2}");
                    fastest = MathF.Max(fastest, speed);
                    if (t > 1f - 0.5f / shot.Seconds)
                    {
                        Assert.True(speed <= MAX_LANDING_SPEED, $"{where}: still {speed:F1} u/s at {t:F3}, landing");
                        landing = MathF.Max(landing, speed);
                    }

                    float yawRate = MathF.Abs(MathHelper.ToDegrees(MathHelper.WrapAngle(yaw - lastYaw))) / dt;
                    float pitchRate = MathF.Abs(MathHelper.ToDegrees(pitch - lastPitch)) / dt;
                    Assert.True(yawRate <= MAX_YAW_RATE, $"{where}: the view turns {yawRate:F0} deg/s at {t:F2}");
                    Assert.True(pitchRate <= MAX_PITCH_RATE, $"{where}: the view pitches {pitchRate:F0} deg/s at {t:F2}");
                    Assert.True(MathF.Abs(MathHelper.ToDegrees(shot.RollAt(t))) <= MAX_ROLL_DEGREES, $"{where}: banked past the limit at {t:F2}");
                    yawPeak = MathF.Max(yawPeak, yawRate);
                    pitchPeak = MathF.Max(pitchPeak, pitchRate);

                    last = position;
                    lastYaw = yaw;
                    lastPitch = pitch;
                }
            }

            _output.WriteLine($"{shortest:F2}-{longest:F2} s, {fastest:F1} u/s, {yawPeak:F0} and {pitchPeak:F0} deg/s, landing {landing:F1} u/s");
            Assert.True(longest > 0f, "no flight home was walked");
        }

        [Fact]
        public void The_lens_rides_the_grass_smoothly()
        {
            //The lens's climb rate changes over a second, not a frame, as the arid shots' does (IntroShotSmoothnessTests'
            //vertical limit): sampled at 60 frames a second on the shot's own clock, three frames in from each end
            float worst = 0f;
            string where = "";
            foreach (var (layout, bearing, radius, plain, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                int frames = (int)(shot.Seconds * 60f);
                var y = new float[frames + 1];
                for (int k = 0; k <= frames; k++)
                {
                    shot.Pose(k / (float)frames, out Vector3 p, out _);
                    y[k] = p.Y;
                }
                float dt = shot.Seconds / frames;
                for (int k = 4; k < frames - 3; k++)
                {
                    float accel = MathF.Abs(y[k + 1] - 2f * y[k] + y[k - 1]) / (dt * dt);
                    if (accel > worst)
                    {
                        shot.Pose(k / (float)frames, out Vector3 p, out _);
                        worst = accel;
                        where = $"layout {layout}, bearing {bearing}, radius {radius}, at {k / (float)frames:F2} ({p.Y - plain.Land.Height(p.X, p.Z):F1} over the grass, {new Vector2(p.X, p.Z).Length():F0} out)";
                    }
                }
            }

            _output.WriteLine($"vertical {worst:F1} u/s2 at {where}");
            Assert.True(worst <= MAX_VERTICAL_ACCELERATION, $"vertical {worst:F1} u/s2 at {where}");
        }

        [Fact]
        public void A_landing_savanna_prologue_plays_no_tour_leg_and_ends_on_the_pose()
        {
            Plain plain = Planted(2);
            IntroShot[] shots = SavannaIntroShots.Build(plain.Land, GamePosition, GameTarget, FieldOfView, new Random(3));
            Assert.True(shots[^1].Lands);

            float seconds = 0f;
            foreach (IntroShot shot in shots) seconds += shot.Seconds;

            var intro = new ChapterIntro();
            intro.Begin(GameTarget, GamePosition.Length(), FieldOfView, GamePosition, GameTarget, null, new Random(1), shots);
            Assert.Equal(seconds, intro.Remaining, 3);

            while (intro.Running) intro.Update(1f / 60f);
            intro.Update(1f / 60f);

            Assert.True(Vector3.Distance(intro.Position, GamePosition) < 1e-3f, $"held {Vector3.Distance(intro.Position, GamePosition):F3} off the pose");
            Assert.True(Vector3.Distance(intro.Target, GameTarget) < 1e-3f);
            Assert.Equal(FieldOfView, intro.FieldOfView, 4);
            Assert.Equal(0f, intro.Roll, 4);
        }

        [Fact]
        public void The_prologue_keeps_its_shots_its_length_and_its_cut()
        {
            float longest = 0f;
            for (int layout = 0; layout < 12; layout++)
            {
                Plain plain = Planted(layout);
                IntroShot[] shots = SavannaIntroShots.Build(plain.Land, GamePosition, GameTarget, FieldOfView, new Random(layout * 7 + 1));
                IntroShot[] reel = SavannaIntroShots.Build(plain.Land, new Vector3(0f, -2f, 15f), GameTarget, FieldOfView, new Random(layout * 7 + 1));

                //The grass home last, after the reel's own shots, which are the very shots the reel plays without it: it is
                //built off the roll after them
                Assert.Equal(SavannaLanding.NAME, shots[^1].Name);
                Assert.False(reel[^1].Lands);
                Assert.Equal(reel.Length + 1, shots.Length);
                for (int s = 0; s < reel.Length; s++)
                {
                    Assert.Equal(reel[s].Name, shots[s].Name);
                    foreach (float t in new[] { 0f, 0.5f, 1f })
                    {
                        reel[s].Pose(t, out Vector3 a, out _);
                        shots[s].Pose(t, out Vector3 b, out _);
                        Assert.Equal(a, b);
                    }
                }

                //The cut into it moves the lens or turns the view enough (film's thirty-degree rule)
                shots[^2].Pose(1f, out Vector3 lens, out Vector3 look);
                shots[^1].Pose(0f, out Vector3 next, out Vector3 nextLook);
                float turned = MathHelper.ToDegrees(MathF.Abs(MathHelper.WrapAngle(
                    MathF.Atan2(nextLook.Z - next.Z, nextLook.X - next.X) - MathF.Atan2(look.Z - lens.Z, look.X - lens.X))));
                Assert.True(Vector3.Distance(lens, next) >= SavannaLanding.CUT_TRAVEL - 1f || turned >= SavannaLanding.CUT_TURN - 5f,
                    $"layout {layout}: the cut moves the lens {Vector3.Distance(lens, next):F1} and turns the view {turned:F0} degrees");

                //And the whole intro is about as long as the reel and its tour leg were (14.3 s)
                float seconds = 0f;
                foreach (IntroShot shot in shots) seconds += shot.Seconds;
                longest = MathF.Max(longest, seconds);
            }

            _output.WriteLine($"the prologue at most {longest:F1} s");
            Assert.True(longest <= 15.5f, $"the prologue runs {longest:F1} s");
        }

        [Fact]
        public void No_flight_is_laid_into_a_pose_inside_the_islands_radius_or_beside_a_fire()
        {
            //The menu's crane stands as near as 15 out, over the island: no flight from the plain reaches it without crossing the
            //arena, so the savanna plays its reel and the tour's last leg there; nor onto a pose in a campfire's flame
            Plain plain = Planted(1);
            Assert.Null(Home(plain, new Vector3(0f, -2f, 15f), 1));
            Vector3 fire = plain.Land.Fires[0];
            Assert.Null(Home(plain, new Vector3(fire.X, GamePosition.Y, fire.Z) * new Vector3(1.05f, 1f, 1.05f), 1));
            Assert.NotNull(Home(plain, GamePosition, 1));
        }

        [Fact]
        public void A_fires_reach_covers_its_stones_and_its_flame()
        {
            //The hearth as SavannaBackdrop.BuildHearthStones lays it: three rock variants, at a scale of up to 1.27, a ring
            //jittered out to 1.12 of its radius; and the flame's volume, whose box a lens must stand outside
            var config = new SavannaSceneConfig();
            CampfireConfig cf = config.Campfire;
            float size = cf.FlameSize * cf.StoneSizeScale, ring = cf.FlameSize * cf.StoneRingScale;
            float stones = 0f;
            for (int v = 0; v < 3; v++)
            {
                var rock = new RockMesh(null, size * (0.82f + 0.18f * v), size * (0.78f - 0.14f * v), irregularityPhase: 1.7f * v);
                BoundingSphere sphere = rock.BoundingSphere;
                stones = MathF.Max(stones, ring * 1.12f + (new Vector2(sphere.Center.X, sphere.Center.Z).Length() + sphere.Radius) * 1.27f);
            }

            SavannaLand land = Planted(0).Land;
            Assert.True(land.FireReach >= stones, $"the reach {land.FireReach:F2}, the stones {stones:F2}");
            Assert.True(land.FireReach >= SavannaBackdrop.FLAME_VOLUME_RADIUS * cf.FlameSize, "the flame's volume");
            Assert.Equal(cf.FlameSize * cf.FlameHeightScale, land.FireHeight, 4);
        }

        private static float Yaw(Vector3 position, Vector3 target) => MathF.Atan2(target.Z - position.Z, target.X - position.X);

        private static float Pitch(Vector3 position, Vector3 target) =>
            MathF.Atan2(target.Y - position.Y, new Vector2(target.X - position.X, target.Z - position.Z).Length());
    }
}
