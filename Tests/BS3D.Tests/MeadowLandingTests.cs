using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The meadow's brook home (#1107, <see cref="MeadowLanding"/>): the owner's "the last animation a fly-through over the
    /// brook and the pond, adjusted so that the camera flies all the way to the game position at the cannon", held as the
    /// cities' swing home is (<see cref="CitySwingTests"/>) over the meadow planting <see cref="MeadowIntroShotsTests"/> builds
    /// and a sweep of gameplay poses round the arena: it is found for nearly every pose, it flies down the brook and over the
    /// pond, it ends exactly on the pose with the game's own lens and no bank, it never comes near the ground, the arena, a
    /// tree, a low solid or the reeds round the pond, and its ride stays inside the limits the city's was held to. And the
    /// shared landing it is flown by (<see cref="IntroLanding"/>) on a route of its own.
    /// </summary>
    public class MeadowLandingTests
    {
        private const float FieldOfView = MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW;
        private static readonly Vector3 Target = MeadowIntroShotsTests.GameTarget;

        //The game's lens stands 28-45 out, a little under the gun, and a level opens with it on the gun's bearing, 90 degrees
        //(GameCameraFit; the first meadow level logs 30.6 out): the sweep takes the axes exactly, as the city's does
        private static readonly float[] Radii = { 29f, 37f, 45f };
        private const float POSE_Y = -8.2f;
        private const int BEARINGS = 12;
        private const int LAYOUTS = 6;

        //How much of the sweep must get a flight home: the rest fall back to the old reel and its tour leg. The level's own
        //pose, on the gun's bearing, gets one in every layout.
        private const float MIN_BUILT_SHARE = 0.95f;

        //The ride: the city's limits (CitySwingTests) for the view's turn, pitch and the landing, and the meadow's own for the
        //speed, the bank and the length. The bank is the meadow's lighter one (MeadowLanding.Landing).
        private const float MAX_SPEED = 50f;
        private const float MAX_YAW_RATE = 170f;
        private const float MAX_PITCH_RATE = 80f;
        private const float MAX_ROLL_DEGREES = 8f;
        private const float MAX_LANDING_SPEED = 8f;
        private const float MIN_SECONDS = 4.5f, MAX_SECONDS = 14.5f;

        //The fly-by down the brook and past the willows (#1188): at least this long from the shot's first frame to the pond's
        //middle (1.8 to 2.3 s at the level's own pose before #1188, 3.5 to 3.9 since), and past each willow at no more than
        //this, on the shot's eased clock: the trees' 14 u/s goes past them at 14.8 to 15.3 over 24 layouts, the brook's 20 went
        //past at 20.4 to 21.2 over five. A figure of its own, not MeadowLanding.TREES_SPEED's: a limit read off the constant
        //moves with it. And at the level's own pose, the glide's pace read over PACE_WINDOW either side of a pose changes by
        //no more than LOW_ACCELERATION until the pond's middle: 4.3 u/s² at the most over 24 layouts.
        private const float LOW_SECONDS = 3f;
        private const float TREES_PACE = 17f;
        private const float LOW_ACCELERATION = 6f;
        private const float PACE_WINDOW = 0.15f;

        //How finely the shot is walked, and how far from everything every pose must stay (the builder keeps 1.5 at its points;
        //the lens runs a spline through them)
        private const int POSES = 1500;
        private const float CLEARANCE = 0.5f;

        //The circle round the arena (MeadowLanding: 48 to 60 out) with a unit's slack for the spline, a lens on it travelling
        //within this many degrees of square to the radial line, and how many degrees inside the frame's edge the axis must
        //stand there (over the sweep it stands at least 7.5 inside; with no lean it is 90 degrees off the look)
        private const float CIRCLE_FROM = 47f, CIRCLE_TO = 61f;
        private const float ACROSS_THE_RADIAL = 15f;
        private const float ARENA_IN_FRAME = 5f;

        private static Vector3 Pose(int bearing, float radius)
        {
            float angle = MathHelper.TwoPi * bearing / BEARINGS;
            return new Vector3(MathF.Cos(angle) * radius, POSE_Y, MathF.Sin(angle) * radius);
        }

        private static IntroShot Home(MeadowIntroShotsTests.Planting planting, Vector3 pose, int seed) =>
            MeadowLanding.Build(planting.Config, planting.Figures, pose, Target, FieldOfView, new Random(seed));

        //Every pose of the sweep with the layout it is flown over, built once for the tests that walk them
        private static readonly Lazy<List<(int Layout, int Bearing, float Radius, MeadowIntroShotsTests.Planting Planting, IntroShot Shot)>> Sweep =
            new(() =>
            {
                var sweep = new List<(int, int, float, MeadowIntroShotsTests.Planting, IntroShot)>();
                for (int layout = 1; layout <= LAYOUTS; layout++)
                {
                    MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(layout);
                    for (int b = 0; b < BEARINGS; b++)
                        foreach (float r in Radii)
                            sweep.Add((layout, b, r, planting, Home(planting, Pose(b, r), layout * 100 + b)));
                }
                return sweep;
            });

        [Fact]
        public void A_flight_home_is_found_for_nearly_every_pose_and_for_the_levels_own_in_every_layout()
        {
            int built = 0;
            foreach (var entry in Sweep.Value)
                if (entry.Shot != null) built++;

            Assert.True(built >= MIN_BUILT_SHARE * Sweep.Value.Count, $"a flight home for {built} of {Sweep.Value.Count} poses");

            for (int layout = 1; layout <= 24; layout++)
                Assert.NotNull(Home(MeadowIntroShotsTests.Plant(layout), MeadowIntroShotsTests.GamePosition, layout));
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
                Assert.Equal(MeadowLanding.NAME, shot.Name);
                Assert.True(Vector3.Distance(position, pose) < 1e-3f, $"{where}: lands {Vector3.Distance(position, pose):F4} off the pose");
                Assert.True(Vector3.Distance(target, Target) < 1e-3f, $"{where}: looks {Vector3.Distance(target, Target):F4} off the pose's look");
                Assert.Equal(FieldOfView, shot.FieldOfViewAt(1f), 4);
                Assert.Equal(0f, shot.RollAt(1f), 4);
            }
        }

        [Fact]
        public void It_flies_down_the_brook_and_over_the_pond()
        {
            foreach (var (layout, bearing, radius, planting, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                MeadowSceneConfig config = planting.Config;
                string where = $"layout {layout}, bearing {bearing}, radius {radius}";

                //It opens on the brook's line, low over it, upstream of the pond
                shot.Pose(0f, out Vector3 first, out _);
                Assert.True(MathF.Abs(MeadowPath.BrookLateral(first.X, first.Z, config)) < 0.5f, $"{where}: opens off the brook's line");
                Assert.InRange(first.Y - TerrainMirror.MeadowGround(first.X, first.Z, config), MeadowLanding.BROOK_ABOVE - 0.1f, MeadowLanding.BROOK_ABOVE + 0.1f);
                //(a run of at least 46 up the brook from the pond's middle, which is about 11 inside its shore, since #1188: the
                //28 it had before opened a few units short of the willows; and no farther up than the reeds and the stones line it)
                Assert.True(MeadowPath.PondShore(first.X, first.Z, config) > 30f, $"{where}: opens too near the pond");
                Assert.True(new Vector2(first.X, first.Z).Length() <= MeadowCarve.BROOK_CARVE_FADE,
                    $"{where}: opens {new Vector2(first.X, first.Z).Length():F1} out, past the reeds' stretch of the brook");

                //And passes over the pond's water, well inside its shore
                float deepest = float.MaxValue;
                for (int i = 0; i <= POSES; i++)
                {
                    shot.Pose(i / (float)POSES, out Vector3 p, out _);
                    deepest = MathF.Min(deepest, MeadowPath.PondShore(p.X, p.Z, config));
                }
                Assert.True(deepest < -config.PondRadius * 0.5f, $"{where}: the lens comes no further over the pond than {deepest:F1} inside its shore");
            }
        }

        /// <summary>
        /// The fly-by down the brook and between the willows at its mouth is the long part of the shot (#1188, the owner:
        /// "shorten the shots on the hay rolls and on the bench, and lengthen the fly-by round the trees and over the brook"):
        /// the lens is low on the brook and among the willows for at least <see cref="LOW_SECONDS"/> from the shot's first
        /// frame to the pond's middle, and goes past each willow at the trees' pace (<see cref="MeadowLanding.TREES_SPEED"/>),
        /// under <see cref="TREES_PACE"/>, which the brook's own pace is not.
        /// </summary>
        [Fact]
        public void The_fly_by_down_the_brook_and_between_the_willows_is_long_and_slow_past_the_trees()
        {
            var flights = new List<(string Where, MeadowIntroShotsTests.Planting Planting, IntroShot Shot)>();
            foreach (var (layout, bearing, radius, planting, shot) in Sweep.Value)
                if (shot != null) flights.Add(($"layout {layout}, bearing {bearing}, radius {radius}", planting, shot));
            for (int layout = 1; layout <= 24; layout++)
            {
                MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(layout);
                flights.Add(($"layout {layout} at the level's pose", planting, Home(planting, MeadowIntroShotsTests.GamePosition, layout)));
            }

            float shortest = float.MaxValue;
            foreach (var (where, planting, shot) in flights)
            {
                MeadowSceneConfig config = planting.Config;

                //The willows: the planting stands them WILLOW_FROM_SHORE (3.5) out of the pond, and every other tree farther
                var willows = new List<PlantFigure>(planting.Figures.Trees).FindAll(tree => MeadowPath.PondShore(tree.Root.X, tree.Root.Z, config) < 6f);
                Assert.Equal(2, willows.Count);

                var poses = new Vector3[POSES + 1];
                for (int i = 0; i <= POSES; i++) shot.Pose(i / (float)POSES, out poses[i], out _);

                int middle = 0;
                for (int i = 1; i <= POSES; i++)
                    if (MeadowPath.PondShore(poses[i].X, poses[i].Z, config) < MeadowPath.PondShore(poses[middle].X, poses[middle].Z, config)) middle = i;

                float low = middle / (float)POSES * shot.Seconds;
                shortest = MathF.Min(shortest, low);
                Assert.True(low >= LOW_SECONDS, $"{where}: over the pond's middle {low:F2} s into the shot");

                foreach (PlantFigure willow in willows)
                {
                    Vector2 trunk = new(willow.Root.X, willow.Root.Z);
                    int nearest = 1;
                    for (int i = 1; i < middle; i++)
                        if (Vector2.DistanceSquared(new Vector2(poses[i].X, poses[i].Z), trunk) < Vector2.DistanceSquared(new Vector2(poses[nearest].X, poses[nearest].Z), trunk))
                            nearest = i;

                    float speed = Vector3.Distance(poses[nearest + 1], poses[nearest - 1]) / (2f * shot.Seconds / POSES);
                    Assert.True(speed <= TREES_PACE, $"{where}: past the willow at {trunk} at {speed:F1} u/s, the trees' pace at most {TREES_PACE}");
                }
            }

            Assert.True(shortest < float.MaxValue, "no flight home was walked");
        }

        /// <summary>
        /// The fly-by is one even glide (#1188): at the level's own pose the lens goes no faster than <see cref="TREES_PACE"/>
        /// from the shot's first frame until it is past the second willow, and its pace changes by no more than
        /// <see cref="LOW_ACCELERATION"/> from the first frame to the pond's middle. Held at the brook's pace until 30 short of
        /// the middle, the lens sped up to 19 u/s as the shot's clock eased in and then braked for the trees; given back to
        /// the brook's pace a few units past the middle, it surged off the willows at 18 u/s². Held at the level's own pose,
        /// the gun's bearing every level opens on: the shot's clock is eased over its whole length (<c>IntroShot.EASE</c>), so
        /// the sweep's shortest flights play the same route faster, and a release straight off the pond onto a pose on its own
        /// side picks the pace up steeply at its middle.
        /// </summary>
        [Fact]
        public void At_the_levels_pose_the_lens_glides_evenly_down_the_brook_past_the_willows_and_onto_the_pond()
        {
            for (int layout = 1; layout <= 24; layout++)
            {
                MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(layout);
                MeadowSceneConfig config = planting.Config;
                IntroShot shot = Home(planting, MeadowIntroShotsTests.GamePosition, layout);
                Assert.NotNull(shot);

                var poses = new Vector3[POSES + 1];
                for (int i = 0; i <= POSES; i++) shot.Pose(i / (float)POSES, out poses[i], out _);
                float dt = shot.Seconds / POSES;

                int middle = 0;
                for (int i = 1; i <= POSES; i++)
                    if (MeadowPath.PondShore(poses[i].X, poses[i].Z, config) < MeadowPath.PondShore(poses[middle].X, poses[middle].Z, config)) middle = i;

                //Past the willows: the later of the two poses nearest their trunks
                int pastTheTrees = 0;
                foreach (PlantFigure willow in planting.Figures.Trees)
                {
                    if (MeadowPath.PondShore(willow.Root.X, willow.Root.Z, config) >= 6f) continue;
                    Vector2 trunk = new(willow.Root.X, willow.Root.Z);
                    int nearest = 0;
                    for (int i = 1; i < middle; i++)
                        if (Vector2.DistanceSquared(new Vector2(poses[i].X, poses[i].Z), trunk) < Vector2.DistanceSquared(new Vector2(poses[nearest].X, poses[nearest].Z), trunk))
                            nearest = i;
                    pastTheTrees = Math.Max(pastTheTrees, nearest);
                }
                Assert.True(pastTheTrees > 0, $"layout {layout}: no willow passed before the pond's middle");

                //The pace over PACE_WINDOW either side of each pose, and its change over the same
                int k = Math.Max(1, (int)MathF.Round(PACE_WINDOW / dt));
                var pace = new float[POSES + 1];
                for (int i = 0; i <= POSES; i++)
                {
                    int a = Math.Max(i - k, 0), b = Math.Min(i + k, POSES);
                    pace[i] = Vector3.Distance(poses[a], poses[b]) / ((b - a) * dt);
                }

                for (int i = 0; i <= pastTheTrees; i++)
                    Assert.True(pace[i] <= TREES_PACE, $"layout {layout}: {pace[i]:F1} u/s {i * dt:F2} s into the shot, before it is past the willows");
                for (int i = 2 * k; i <= Math.Min(middle, POSES - k); i++)
                {
                    float change = MathF.Abs(pace[i + k] - pace[i - k]) / (2 * k * dt);
                    Assert.True(change <= LOW_ACCELERATION, $"layout {layout}: the pace changes at {change:F1} u/s² {i * dt:F2} s into the shot, before the pond's middle");
                }
            }
        }

        [Fact]
        public void It_never_comes_near_the_ground_the_arena_a_tree_a_solid_or_the_reeds()
        {
            int walked = 0;
            foreach (var (layout, bearing, radius, planting, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                walked++;
                MeadowSceneConfig config = planting.Config;
                for (int i = 0; i <= POSES; i++)
                {
                    shot.Pose(i / (float)POSES, out Vector3 p, out _);
                    string where = $"layout {layout}, bearing {bearing}, radius {radius}, at {i}/{POSES} ({p.X:F1}, {p.Y:F1}, {p.Z:F1})";

                    //Over the ground, and never nearer the axis than the pose itself (the island, the gun, the glass and the
                    //cluster all stand nearer), nor ever inside the arena's keep-out but for that arrival
                    float overGround = p.Y - TerrainMirror.Meadow(p.X, p.Z, config);
                    Assert.True(overGround >= CLEARANCE, $"{where}: {overGround:F2} over the ground");
                    float fromAxis = new Vector2(p.X, p.Z).Length();
                    Assert.True(fromAxis >= MathF.Min(IntroGround.ARENA_KEEP_OUT, radius) - 1f, $"{where}: {fromAxis:F1} from the axis");
                    Assert.True(fromAxis >= ArenaIsland.RADIUS + 2f, $"{where}: over the island");

                    //Every tree by its stem and the roots round its foot, which run out to about twice its flare's radius (the
                    //builder keeps 2.5 past them at its points); by its crown too wherever the lens is not low over the field
                    float overField = p.Y - TerrainMirror.MeadowGround(p.X, p.Z, config);
                    foreach (PlantFigure tree in planting.Figures.Trees)
                    {
                        Assert.True(IntroPaths.DistanceToSegment(p, tree.Root, tree.Crown) - tree.Stem * 2.1f >= 2f, $"{where}: against a trunk at {tree.Root}");
                        if (overField > 3.5f)
                            Assert.True(Vector3.Distance(p, tree.Crown) - tree.Reach >= CLEARANCE, $"{where}: in a crown at {tree.Crown}");
                    }

                    foreach (PlantFigure solid in planting.Figures.Solids)
                        Assert.True(IntroPaths.DistanceToSegment(p, solid.Root, solid.Crown) - solid.Stem >= CLEARANCE
                            && Vector3.Distance(p, solid.Crown) - solid.Reach >= CLEARANCE, $"{where}: against a solid at {solid.Crown}");

                    //Over the reeds round the pond (up to 4 tall, 0.9 inside its shore to 1.5 outside, in clumps, each reed up to
                    //0.04 radians off its clump's bearing and 0.54 across), or between two clumps, save on the brook's bank
                    float shore = MeadowPath.PondShore(p.X, p.Z, config);
                    if (shore > -0.9f && shore < 1.5f && !MeadowCarve.OnBrookBank(p.X, p.Z, config) && overField < 4f + CLEARANCE)
                    {
                        (float pondX, float pondZ) = MeadowPath.PondCentre(config);
                        float a = MathF.Atan2(p.Z - pondZ, p.X - pondX);
                        for (int k = -2; k <= 2; k++)
                            Assert.True(MeadowPath.PondReedClump(a + k * 0.05f) <= MeadowPath.POND_REEDS_FROM,
                                $"{where}: {overField:F2} over the field in a clump of the pond's reeds");
                    }
                }
            }

            Assert.True(walked > 0);
        }

        [Fact]
        public void The_ride_stays_inside_its_limits()
        {
            float longest = 0f, shortest = float.MaxValue;
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
                    if (t > 1f - 0.5f / shot.Seconds)
                        Assert.True(speed <= MAX_LANDING_SPEED, $"{where}: still {speed:F1} u/s at {t:F3}, landing");

                    float yawRate = MathF.Abs(MathHelper.ToDegrees(MathHelper.WrapAngle(yaw - lastYaw))) / dt;
                    float pitchRate = MathF.Abs(MathHelper.ToDegrees(pitch - lastPitch)) / dt;
                    Assert.True(yawRate <= MAX_YAW_RATE, $"{where}: the view turns {yawRate:F0} deg/s at {t:F2}");
                    Assert.True(pitchRate <= MAX_PITCH_RATE, $"{where}: the view pitches {pitchRate:F0} deg/s at {t:F2}");
                    Assert.True(MathF.Abs(MathHelper.ToDegrees(shot.RollAt(t))) <= MAX_ROLL_DEGREES, $"{where}: banked past the limit at {t:F2}");

                    last = position;
                    lastYaw = yaw;
                    lastPitch = pitch;
                }
            }

            Assert.True(longest > 0f, "no flight home was walked");
        }

        [Fact]
        public void Round_the_island_the_look_keeps_the_arena_in_frame()
        {
            //The look turns in off the travel towards the arena (MeadowLanding.Landing's LookInward, the route's Lean): along
            //the circle's tangent the axis stands 90 degrees off the look, out of any frame, and the island, the gun and the
            //cluster go by unseen. Wherever the lens runs round the island on its circle, the axis stands inside a 16:9 frame at
            //the shot's own lens, ARENA_IN_FRAME short of its edge.
            int held = 0;
            foreach (var (layout, bearing, radius, _, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                shot.Pose(0f, out Vector3 last, out _);
                for (int i = 1; i <= POSES; i++)
                {
                    float t = i / (float)POSES;
                    shot.Pose(t, out Vector3 position, out Vector3 look);
                    Vector2 plan = new(position.X, position.Z), step = plan - new Vector2(last.X, last.Z);
                    last = position;

                    //On the circle: as far out as it is laid, travelling across the radial line
                    float fromAxis = plan.Length();
                    if (fromAxis < CIRCLE_FROM || fromAxis > CIRCLE_TO || step.LengthSquared() < 1e-8f) continue;
                    if (MathF.Abs(Vector2.Dot(Vector2.Normalize(step), plan / fromAxis)) > MathF.Sin(MathHelper.ToRadians(ACROSS_THE_RADIAL))) continue;

                    float halfWidth = MathF.Atan(MathF.Tan(shot.FieldOfViewAt(t) * 0.5f) * 16f / 9f);
                    float off = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(Target.Z - position.Z, Target.X - position.X)
                        - MathF.Atan2(look.Z - position.Z, look.X - position.X)));
                    Assert.True(off <= halfWidth - MathHelper.ToRadians(ARENA_IN_FRAME),
                        $"layout {layout}, bearing {bearing}, radius {radius}, at {t:F3}: the axis {MathHelper.ToDegrees(off):F0} degrees off the look, the frame's edge {MathHelper.ToDegrees(halfWidth):F0}");
                    held++;
                }
            }

            Assert.True(held >= 1000, $"the lens was on the circle at only {held} poses");
        }

        [Fact]
        public void A_landing_meadow_prologue_plays_no_tour_leg_and_ends_on_the_pose()
        {
            MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(2);
            Vector3 pose = MeadowIntroShotsTests.GamePosition;
            IntroShot[] shots = MeadowIntroShots.Build(planting.Config, planting.Figures, pose, Target, FieldOfView, new Random(3));
            Assert.True(shots[^1].Lands);

            float seconds = 0f;
            foreach (IntroShot shot in shots) seconds += shot.Seconds;

            var intro = new ChapterIntro();
            intro.Begin(Target, pose.Length(), FieldOfView, pose, Target, null, new Random(1), shots);
            Assert.Equal(seconds, intro.Remaining, 3);

            while (intro.Running) intro.Update(1f / 60f);
            intro.Update(1f / 60f);

            Assert.True(Vector3.Distance(intro.Position, pose) < 1e-3f, $"held {Vector3.Distance(intro.Position, pose):F3} off the pose");
            Assert.True(Vector3.Distance(intro.Target, Target) < 1e-3f);
            Assert.Equal(FieldOfView, intro.FieldOfView, 4);
            Assert.Equal(0f, intro.Roll, 4);
        }

        [Fact]
        public void A_low_lens_crosses_the_ponds_shore_only_between_its_reed_clumps()
        {
            //The flights the sweep lays all leave the pond on its arena side, 27 to 98 degrees round it, where no clump stands, so
            //the sweep never meets the rule (removed, no swept flight changed enough to fail): it is held here on the shore itself.
            //Low in a clump is refused, low in the gap and high over a clump are not, and low on the brook's own bank, where none
            //is planted, is not either.
            var config = new MeadowSceneConfig();
            var obstacles = new MeadowLanding.Obstacles(config, null, 0f);
            (float pondX, float pondZ) = MeadowPath.PondCentre(config);

            Vector3 OnTheShore(float degrees, float overField)
            {
                float a = MathHelper.ToRadians(degrees), shore = config.PondRadius * MeadowPath.PondOutline(a);
                float x = pondX + MathF.Cos(a) * shore, z = pondZ + MathF.Sin(a) * shore;
                return new Vector3(x, TerrainMirror.MeadowGround(x, z, config) + overField, z);
            }

            Assert.True(MeadowPath.PondReedClump(MathHelper.ToRadians(-120f)) > MeadowPath.POND_REEDS_FROM, "the premise: reeds at -120 degrees");
            Assert.False(obstacles.Clear(OnTheShore(-120f, 2f)), "low in a clump of reeds");
            Assert.True(obstacles.Clear(OnTheShore(60f, 2f)), "low in the gap on the arena's side");
            Assert.True(obstacles.Clear(OnTheShore(-120f, 6f)), "over the clump");

            (float mouthX, float mouthZ) = MeadowPath.BrookPoint(config.ClearingRadius * MeadowPath.BROOK_START + config.PondRadius, 0f, config);
            Assert.True(obstacles.Clear(new Vector3(mouthX, TerrainMirror.MeadowGround(mouthX, mouthZ, config) + MeadowLanding.BROOK_ABOVE, mouthZ)),
                "on the brook's own bank at its mouth");
        }

        [Fact]
        public void No_flight_is_laid_into_a_pose_inside_the_islands_radius()
        {
            //The menu's crane stands as near as 15 out, over the island: no flight from the meadow reaches it without crossing
            //the arena, so the meadow plays its reel and the tour's last leg there
            MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(1);
            Assert.Null(Home(planting, new Vector3(0f, -2f, 15f), 1));
            Assert.NotNull(Home(planting, MeadowIntroShotsTests.GamePosition, 1));
        }

        /// <summary>
        /// The shared landing on a route of its own (the next scenes' starting point): a straight run at a steady speed that
        /// turns onto a pose off to one side ends exactly on it, slows to rest, and a route the clearance refuses is never flown.
        /// </summary>
        [Fact]
        public void The_shared_landing_lands_any_scenes_route_and_flies_none_its_clearance_refuses()
        {
            var pose = new Vector3(0f, -6f, 40f);
            var look = new Vector3(0f, 0f, 0f);

            IntroLanding.Route Straight()
            {
                var plan = new List<Vector2>();
                IntroLanding.Straight(plan, new Vector2(-200f, 120f), new Vector2(-60f, 120f), 0.5f);
                int release = plan.Count - 1;
                IntroLanding.Release(plan, plan[^1], Vector2.UnitX, new Vector2(pose.X, pose.Z), IntroLanding.Arrival(0.1, -Vector2.UnitY, Vector2.UnitX), 0.5f);

                var path = new List<Vector3>();
                foreach (Vector2 p in plan) path.Add(new Vector3(p.X, 10f, p.Y));
                path[^1] = pose;

                var speed = new float[path.Count];
                Array.Fill(speed, 30f);
                return new IntroLanding.Route { Path = path, Speed = speed, DecelerateFrom = release, Score = 0f };
            }

            IntroLanding.Route route = IntroLanding.Best(4, Straight);
            Assert.NotNull(route);
            IntroShot shot = IntroLanding.Fly("a test", route, pose, look, FieldOfView, MeadowLanding.Landing);

            shot.Pose(1f, out Vector3 position, out Vector3 target);
            Assert.True(shot.Lands);
            Assert.True(Vector3.Distance(position, pose) < 1e-3f);
            Assert.True(Vector3.Distance(target, look) < 1e-3f);
            Assert.Equal(FieldOfView, shot.FieldOfViewAt(1f), 4);
            Assert.Equal(FieldOfView * MeadowLanding.Landing.WideLens, shot.FieldOfViewAt(0f), 4);

            shot.Pose(1f - 0.25f / shot.Seconds, out Vector3 late, out _);
            Assert.True(Vector3.Distance(late, pose) / 0.25f < MAX_LANDING_SPEED, "it does not slow onto the pose");

            //A clearance that refuses the route's height refuses every candidate, and none is flown
            Assert.Null(IntroLanding.Best(4, Straight, p => p.Y < 5f));
        }

        private static float Yaw(Vector3 position, Vector3 target) => MathF.Atan2(target.Z - position.Z, target.X - position.X);

        private static float Pitch(Vector3 position, Vector3 target) =>
            MathF.Atan2(target.Y - position.Y, new Vector2(target.X - position.X, target.Z - position.Z).Length());
    }
}
