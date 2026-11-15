using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The desert's scorpion home (#1110, <see cref="DesertLanding"/>): the owner's "the final animation of the intro could
    /// also take off from this detailed view of the beetles and scorpions into the game position behind the cannon", held as
    /// the meadow's brook home and the big top's gate home are (<see cref="MeadowLandingTests"/>, <see cref="CircusLandingTests"/>)
    /// over a sweep of gameplay poses round the arena: it is found for nearly every pose and for the level's own off every
    /// roll, it opens as the close view of the scorpion with the scorpion in frame, it ends exactly on the pose with the game's
    /// own lens and no bank, every point of it stays over the desert's own sand (<see cref="TerrainMirror.Desert"/>), off the
    /// scorpion and no nearer the axis than the pose, and its ride stays inside the limits the other landings are held to.
    /// </summary>
    public class DesertLandingTests
    {
        internal const float FieldOfView = MathF.PI / 4.2f;

        //The Coil's first level (Rope) logs its pose: 29.4 out on the gun's bearing (90), the lens at -7.33, looking at
        //(0, -1.92, 0)
        internal static readonly Vector3 GamePosition = new(0f, -7.33f, 29.4f);
        internal static readonly Vector3 GameTarget = new(0f, -1.92f, 0f);

        //The game's lens stands 28-45 out (GameCameraFit); the sweep takes the axes exactly, as the meadow's and the big top's
        //do, and each pose is flown off a roll of its own. The dunes are not seeded (sceneseed= does not reach them), so the
        //rolls are what changes the land a flight is laid over
        private static readonly float[] Radii = { 29f, 37f, 45f };
        private const int BEARINGS = 12;
        private const int ROLLS = 4;

        //How much of the sweep must get a flight home: the rest fall back to the old reel and its tour leg. The level's own
        //pose gets one off every roll.
        private const float MIN_BUILT_SHARE = 0.95f;

        //The ride: the other landings' limits for the view's turn and pitch and the landing (MeadowLandingTests), the desert's
        //own for the speed (its cruise is 8-11 u/s), the bank (DesertLanding.Landing) and the length
        private const float MAX_SPEED = 20f;
        private const float MAX_YAW_RATE = 170f;
        private const float MAX_PITCH_RATE = 80f;
        private const float MAX_ROLL_DEGREES = 6f;
        private const float MAX_LANDING_SPEED = 8f;
        private const float MIN_SECONDS = 6f, MAX_SECONDS = 13f;

        //How finely the shot is walked, and how far over the sand every pose must stay (the builder keeps LOWEST at its points;
        //the lens runs a spline through them)
        private const int POSES = 1500;
        private const float SAND_CLEARANCE = DesertLanding.LOWEST - 0.1f;

        //The scorpion in frame while the look holds it: its foot and its back inside a 16:9 frame this share of the half
        //width and half height short of the edges, at the shot's own lens
        private const float FRAME_MARGIN = 0.1f;

        private static readonly DesertSceneConfig Desert = new();

        private readonly ITestOutputHelper _output;

        public DesertLandingTests(ITestOutputHelper output) => _output = output;

        private static Vector3 Pose(int bearing, float radius)
        {
            float angle = MathHelper.TwoPi * bearing / BEARINGS;
            return new Vector3(MathF.Cos(angle) * radius, GamePosition.Y, MathF.Sin(angle) * radius);
        }

        private static IntroShot Home(Vector3 pose, int seed, out DesertLanding.Walk walk) =>
            DesertLanding.Build(Desert, null, pose, GameTarget, FieldOfView, new Random(seed), out walk);

        //Every pose of the sweep, built once for the tests that walk them
        private static readonly Lazy<List<(int Roll, int Bearing, float Radius, IntroShot Shot, DesertLanding.Walk Walk)>> Sweep = new(() =>
        {
            var sweep = new List<(int, int, float, IntroShot, DesertLanding.Walk)>();
            for (int roll = 1; roll <= ROLLS; roll++)
                for (int b = 0; b < BEARINGS; b++)
                    foreach (float r in Radii)
                    {
                        IntroShot shot = Home(Pose(b, r), roll * 100 + b, out DesertLanding.Walk walk);
                        sweep.Add((roll, b, r, shot, walk));
                    }
            return sweep;
        });

        [Fact]
        public void A_flight_home_is_found_for_nearly_every_pose_and_for_the_levels_own_off_every_roll()
        {
            int built = 0;
            foreach (var entry in Sweep.Value)
                if (entry.Shot != null) built++;

            Assert.True(built >= MIN_BUILT_SHARE * Sweep.Value.Count, $"a flight home for {built} of {Sweep.Value.Count} poses");
            _output.WriteLine($"a flight home for {built} of {Sweep.Value.Count} poses");

            for (int seed = 1; seed <= 24; seed++)
                Assert.NotNull(Home(GamePosition, seed, out _));
        }

        [Fact]
        public void The_sand_and_the_arena_refuse_a_lens_under_the_clearance_or_nearer_the_axis_than_the_pose()
        {
            //The predicate on its own, where the sweep never meets it (the routes come in from outside the pose and over flat
            //sand by construction): a point is refused under LOWEST over the sand or nearer the axis than the keep-out
            float sand = TerrainMirror.Desert(0f, 50f, Desert);
            Assert.True(DesertLanding.Clear(Desert, 30f, new Vector3(0f, sand + DesertLanding.LOWEST + 0.05f, 50f)), "over the sand");
            Assert.False(DesertLanding.Clear(Desert, 30f, new Vector3(0f, sand + DesertLanding.LOWEST - 0.05f, 50f)), "under the clearance");
            Assert.False(DesertLanding.Clear(Desert, 30f, new Vector3(0f, -5f, 29.5f)), "nearer the axis than the pose");
            Assert.True(DesertLanding.Clear(Desert, 30f, new Vector3(0f, -5f, 30.5f)), "as far out as the pose");

            //And the scorpion's walk, which no swept route comes near: a lens low beside its middle is refused, one higher over
            //it or further off is taken
            Vector2 start = new(0f, 50f), finish = new(3f, 50f);
            Assert.False(DesertLanding.ClearOfWalk(new Vector3(1.5f, sand + 0.85f, 51f), start, finish, Desert), "a unit off the scorpion");
            Assert.True(DesertLanding.ClearOfWalk(new Vector3(1.5f, sand + 2.5f, 50f), start, finish, Desert), "well over it");
            Assert.True(DesertLanding.ClearOfWalk(new Vector3(1.5f, sand + 0.85f, 52f), start, finish, Desert), "two units off it");
        }

        [Fact]
        public void It_lands_on_the_pose_with_the_games_lens_and_level()
        {
            foreach (var (roll, bearing, radius, shot, _) in Sweep.Value)
            {
                if (shot == null) continue;

                Vector3 pose = Pose(bearing, radius);
                shot.Pose(1f, out Vector3 position, out Vector3 target);
                string where = $"roll {roll}, bearing {bearing}, radius {radius}";
                Assert.True(shot.Lands);
                Assert.Equal(DesertLanding.NAME, shot.Name);
                Assert.True(Vector3.Distance(position, pose) < 1e-3f, $"{where}: lands {Vector3.Distance(position, pose):F4} off the pose");
                Assert.True(Vector3.Distance(target, GameTarget) < 1e-3f, $"{where}: looks {Vector3.Distance(target, GameTarget):F4} off the pose's look");
                Assert.Equal(FieldOfView, shot.FieldOfViewAt(1f), 4);
                Assert.Equal(0f, shot.RollAt(1f), 4);
            }
        }

        [Fact]
        public void It_opens_low_beside_the_scorpions_burrow_and_holds_the_scorpion_in_frame()
        {
            foreach (var (roll, bearing, radius, shot, walk) in Sweep.Value)
            {
                if (shot == null) continue;

                string where = $"roll {roll}, bearing {bearing}, radius {radius}";
                shot.Pose(0f, out Vector3 first, out Vector3 look);
                float sand = TerrainMirror.Desert(first.X, first.Z, Desert);
                Assert.InRange(first.Y - sand, DesertLanding.LENS_ABOVE - 0.05f, DesertLanding.LENS_ABOVE + 0.05f);
                Assert.InRange(Vector2.Distance(new Vector2(first.X, first.Z), walk.Start), 2f, 5f);

                //Its foot and its back (a scorpion's height over it) in frame from the shot's first frame until the look lets go
                for (float seconds = 0f; seconds <= DesertLanding.Lift; seconds += 0.05f)
                {
                    float t = seconds / shot.Seconds;
                    shot.Pose(t, out Vector3 lens, out Vector3 at);
                    Vector3 foot = SceneRenderer.DesertFeatureAt(walk.Start, walk.Yaw, seconds - DesertLanding.ONSET, Desert);
                    foreach (Vector3 point in new[] { foot, foot + Vector3.Up * DesertLanding.CRITTER_HEIGHT })
                    {
                        Vector2 screen = Screen(lens, at, point, shot.FieldOfViewAt(t), 16f / 9f);
                        Assert.True(MathF.Abs(screen.X) <= 1f - FRAME_MARGIN && MathF.Abs(screen.Y) <= 1f - FRAME_MARGIN,
                            $"{where}: the scorpion at ({screen.X:F2}, {screen.Y:F2}) of the frame at {seconds:F2} s");
                    }
                }
            }
        }

        [Fact]
        public void It_keeps_over_the_sand_off_the_scorpion_and_no_nearer_the_axis_than_the_pose()
        {
            int walked = 0;
            foreach (var (roll, bearing, radius, shot, walk) in Sweep.Value)
            {
                if (shot == null) continue;

                walked++;
                for (int i = 0; i <= POSES; i++)
                {
                    float t = i / (float)POSES;
                    shot.Pose(t, out Vector3 p, out _);
                    float r = new Vector2(p.X, p.Z).Length();
                    string where = $"roll {roll}, bearing {bearing}, radius {radius}, at {i}/{POSES} ({p.X:F1}, {p.Y:F1}, {p.Z:F1}), {r:F1} out";

                    Assert.True(p.Y - TerrainMirror.Desert(p.X, p.Z, Desert) >= SAND_CLEARANCE, $"{where}: {p.Y - TerrainMirror.Desert(p.X, p.Z, Desert):F2} over the sand");
                    Assert.True(r >= radius - 1f, $"{where}: nearer the axis than the pose");
                    Assert.True(r >= ArenaIsland.RADIUS + 2f, $"{where}: over the island");

                    //Off the scorpion wherever it is at that moment of the wall clock (the shot's points are on its eased one)
                    float seconds = t * shot.Seconds;
                    Vector3 foot = SceneRenderer.DesertFeatureAt(walk.Start, walk.Yaw, seconds - DesertLanding.ONSET, Desert);
                    float off = IntroPaths.DistanceToSegment(p, foot, foot + Vector3.Up * DesertLanding.CRITTER_HEIGHT);
                    Assert.True(off >= DesertLanding.CRITTER_AIR, $"{where}: {off:F2} off the scorpion");
                }
            }

            Assert.True(walked > 0);
        }

        [Fact]
        public void The_ride_stays_inside_its_limits()
        {
            var stats = new Stats();
            foreach (var (roll, bearing, radius, shot, _) in Sweep.Value)
            {
                if (shot == null) continue;

                string where = $"roll {roll}, bearing {bearing}, radius {radius}";
                Assert.InRange(shot.Seconds, MIN_SECONDS, MAX_SECONDS);
                stats.Seconds(shot.Seconds);

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
                    {
                        Assert.True(speed <= MAX_LANDING_SPEED, $"{where}: still {speed:F1} u/s at {t:F3}, landing");
                        stats.Landing = MathF.Max(stats.Landing, speed);
                    }

                    float yawRate = MathF.Abs(MathHelper.ToDegrees(MathHelper.WrapAngle(yaw - lastYaw))) / dt;
                    float pitchRate = MathF.Abs(MathHelper.ToDegrees(pitch - lastPitch)) / dt;
                    float rollDegrees = MathF.Abs(MathHelper.ToDegrees(shot.RollAt(t)));
                    Assert.True(yawRate <= MAX_YAW_RATE, $"{where}: the view turns {yawRate:F0} deg/s at {t:F2}");
                    Assert.True(pitchRate <= MAX_PITCH_RATE, $"{where}: the view pitches {pitchRate:F0} deg/s at {t:F2}");
                    Assert.True(rollDegrees <= MAX_ROLL_DEGREES, $"{where}: banked past the limit at {t:F2}");

                    stats.Speed = MathF.Max(stats.Speed, speed);
                    stats.YawRate = MathF.Max(stats.YawRate, yawRate);
                    stats.PitchRate = MathF.Max(stats.PitchRate, pitchRate);
                    stats.Roll = MathF.Max(stats.Roll, rollDegrees);
                    last = position;
                    lastYaw = yaw;
                    lastPitch = pitch;
                }
            }

            _output.WriteLine(stats.ToString());
            Assert.True(stats.Longest > 0f, "no flight home was walked");
        }

        [Fact]
        public void A_landing_desert_prologue_plays_no_tour_leg_and_ends_on_the_pose()
        {
            IntroShot[] shots = DesertIntroShots.Build(Desert, null, GamePosition, GameTarget, FieldOfView, new Random(3));
            Assert.True(shots[^1].Lands);
            Assert.Equal(DesertLanding.NAME, shots[^1].Name);
            Assert.DoesNotContain(shots, s => s.Name == "the scorpion");

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
        public void No_flight_is_laid_into_a_pose_inside_the_islands_radius_and_the_reel_keeps_its_scorpion()
        {
            //The menu's crane stands as near as 15 out, over the island: no flight lands there, and the desert plays the reel
            //it had, the scorpion a shot of its own last and the tour's leg after it
            var crane = new Vector3(0f, -2f, 15f);
            Assert.Null(Home(crane, 1, out _));
            Assert.NotNull(Home(GamePosition, 1, out _));

            IntroShot[] reel = DesertIntroShots.Build(Desert, null, crane, GameTarget, FieldOfView, new Random(1));
            Assert.Contains(reel, s => s.Name == "the scorpion");
            Assert.False(reel[^1].Lands);
        }

        [Fact]
        public void The_shot_cues_the_scorpion_where_the_look_expects_it_on_its_first_frame()
        {
            //The cue is the shot's own start (ChapterIntro calls IntroShot.Start on its first frame), with the walk the look was
            //laid on and the onset the look counts from
            Vector2 cuedAt = default;
            float cuedYaw = float.NaN, cuedOnset = float.NaN;
            IntroShot[] shots = DesertIntroShots.Build(Desert, (at, yaw, onset, scorpion) =>
                {
                    Assert.True(scorpion, "the scorpion home cued a beetle");
                    (cuedAt, cuedYaw, cuedOnset) = (at, yaw, onset);
                }, GamePosition, GameTarget, FieldOfView, new Random(5));
            Assert.True(float.IsNaN(cuedYaw), "cued before the shot came on screen");

            shots[^1].Start();
            Assert.Equal(DesertLanding.ONSET, cuedOnset);

            //The look's first point is over the burrow it was cued at
            shots[^1].Pose(0f, out _, out Vector3 look);
            Assert.True(Vector2.Distance(new Vector2(look.X, look.Z), cuedAt) < 0.05f, $"the look opens {Vector2.Distance(new Vector2(look.X, look.Z), cuedAt):F2} off the burrow");
            Assert.False(float.IsNaN(cuedYaw));
        }

        //Where a point stands in a level lens's frame: -1 to 1 across its width and height
        internal static Vector2 Screen(Vector3 position, Vector3 look, Vector3 point, float fieldOfView, float aspect)
        {
            Vector3 forward = Vector3.Normalize(look - position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
            Vector3 up = Vector3.Cross(right, forward);
            Vector3 d = point - position;
            float depth = Vector3.Dot(d, forward);
            float tan = MathF.Tan(fieldOfView * 0.5f);
            return new Vector2(Vector3.Dot(d, right) / depth / (tan * aspect), Vector3.Dot(d, up) / depth / tan);
        }

        private static float Yaw(Vector3 position, Vector3 target) => MathF.Atan2(target.Z - position.Z, target.X - position.X);

        private static float Pitch(Vector3 position, Vector3 target) =>
            MathF.Atan2(target.Y - position.Y, new Vector2(target.X - position.X, target.Z - position.Z).Length());

        private sealed class Stats
        {
            public float Speed, YawRate, PitchRate, Roll, Landing, Shortest = float.MaxValue, Longest;

            public void Seconds(float s)
            {
                Shortest = MathF.Min(Shortest, s);
                Longest = MathF.Max(Longest, s);
            }

            public override string ToString() => string.Format(CultureInfo.InvariantCulture,
                "{0:F1}-{1:F1} s, {2:F1} u/s, yaw {3:F0} deg/s, pitch {4:F0} deg/s, bank {5:F1} deg, landing {6:F1} u/s",
                Shortest, Longest, Speed, YawRate, PitchRate, Roll, Landing);
        }
    }
}
