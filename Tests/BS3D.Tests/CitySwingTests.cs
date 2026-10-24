using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The day city's swing home (#972, <see cref="CitySwing"/>): the owner's "Spider-Man-style swinging fly-throughs until
    /// we finally arrive at the island and the camera gets into the game camera position", held over real cities and a
    /// sweep of gameplay poses round the arena: it is found for nearly every pose, it ends exactly on the pose with the
    /// game's own lens and no bank, it never comes near a tower, and its ride stays inside the limits it was tuned to.
    /// </summary>
    public class CitySwingTests
    {
        private static readonly float FieldOfView = MathF.PI / 4.2f;
        private static readonly Vector3 Target = new(0f, 3.5f, 0f);

        //The game's lens stands 28-45 out on any bearing, a little under the gun (GameCameraFit; the Spectrum's first level
        //logs 28.7 out)
        private static readonly float[] Radii = { 29f, 37f, 45f };
        private const int BEARINGS = 12;
        private const int CITIES = 3;

        //How much of the sweep must get a swing: the rest fall back to the old reel and its tour leg
        private const float MIN_BUILT_SHARE = 0.95f;

        //The ride, as tuned (desktop probe, 2026-10-24): up to 59 u/s at the bottom of a sag, the view turning up to 151
        //degrees a second round a corner and pitching up to 64, banked up to 10 degrees, 6.6-7.6 s, under 7 u/s in the last
        //half second. The limits leave a margin over that and catch a change that brings back the first cut's 200-330
        //degree-a-second spikes or its 10-second length.
        private const float MAX_SPEED = 65f;
        private const float MAX_YAW_RATE = 170f;
        private const float MAX_PITCH_RATE = 80f;
        private const float MAX_ROLL_DEGREES = 14f;
        private const float MAX_LANDING_SPEED = 8f;
        private const float MIN_SECONDS = 5.5f, MAX_SECONDS = 8.5f;

        //How finely the shot is walked, and how far from a tower's box every pose must stay (the builder keeps 2 at its
        //points; the lens runs a spline through them)
        private const int POSES = 1500;
        private const float CLEARANCE = 1f;

        private static City DayCity(int seed)
        {
            var config = new CitySceneConfig();
            config.Seed += seed;
            return new City(config, false, ArenaIsland.RADIUS);
        }

        private static Vector3 Pose(int bearing, float radius)
        {
            float angle = MathHelper.TwoPi * bearing / BEARINGS + 0.1f;
            return new Vector3(MathF.Cos(angle) * radius, -4f, MathF.Sin(angle) * radius);
        }

        private static IntroShot Swing(City city, Vector3 pose, int seed) =>
            CitySwing.Build(city, pose, Target, FieldOfView, new Random(seed), CitySwing.Day);

        [Fact]
        public void A_swing_is_found_for_nearly_every_pose()
        {
            int built = 0, tried = 0;
            for (int c = 0; c < CITIES; c++)
            {
                City city = DayCity(c);
                for (int b = 0; b < BEARINGS; b++)
                    foreach (float r in Radii)
                    {
                        tried++;
                        if (Swing(city, Pose(b, r), c * 100 + b) != null) built++;
                    }
            }

            Assert.True(built >= MIN_BUILT_SHARE * tried, $"a swing for {built} of {tried} poses");
        }

        [Fact]
        public void It_lands_on_the_pose_with_the_games_lens_and_level()
        {
            City city = DayCity(0);
            for (int b = 0; b < BEARINGS; b++)
            {
                Vector3 pose = Pose(b, 37f);
                IntroShot shot = Swing(city, pose, b);
                if (shot == null) continue;

                shot.Pose(1f, out Vector3 position, out Vector3 target);
                Assert.True(shot.Lands);
                Assert.True(Vector3.Distance(position, pose) < 1e-3f, $"bearing {b}: lands {Vector3.Distance(position, pose):F3} off the pose");
                Assert.True(Vector3.Distance(target, Target) < 1e-3f, $"bearing {b}: looks {Vector3.Distance(target, Target):F3} off the pose's look");
                Assert.Equal(FieldOfView, shot.FieldOfViewAt(1f), 4);
                Assert.Equal(0f, shot.RollAt(1f), 4);
            }
        }

        [Fact]
        public void It_never_comes_near_a_tower()
        {
            for (int c = 0; c < CITIES; c++)
            {
                City city = DayCity(c);
                var towers = new CitySwing.Towers(city);

                for (int b = 0; b < BEARINGS; b++)
                    foreach (float r in Radii)
                    {
                        IntroShot shot = Swing(city, Pose(b, r), c * 100 + b);
                        if (shot == null) continue;

                        for (int i = 0; i <= POSES; i++)
                        {
                            shot.Pose(i / (float)POSES, out Vector3 position, out _);
                            Assert.True(towers.Clear(position, CLEARANCE),
                                $"city {c}, bearing {b}, radius {r}: the lens at {i}/{POSES} ({position.X:F1}, {position.Y:F1}, {position.Z:F1}) stands in a tower");
                        }
                    }
            }
        }

        [Fact]
        public void The_ride_stays_inside_its_limits()
        {
            for (int c = 0; c < CITIES; c++)
            {
                City city = DayCity(c);
                for (int b = 0; b < BEARINGS; b++)
                    foreach (float r in Radii)
                    {
                        IntroShot shot = Swing(city, Pose(b, r), c * 100 + b);
                        if (shot == null) continue;

                        string where = $"city {c}, bearing {b}, radius {r}";
                        Assert.InRange(shot.Seconds, MIN_SECONDS, MAX_SECONDS);

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
            }
        }

        [Fact]
        public void A_landing_prologue_plays_no_tour_leg_and_ends_on_the_pose()
        {
            City city = DayCity(0);
            Vector3 pose = Pose(3, 37f);
            IntroShot swing = Swing(city, pose, 3);
            Assert.NotNull(swing);

            var opener = new IntroShot("opener", new[] { new Vector3(0f, 50f, 100f), new Vector3(10f, 50f, 100f) }, 2f, 1f, lookAt: Vector3.Zero);
            var intro = new ChapterIntro();
            intro.Begin(Target, 37f, FieldOfView, pose, Target, null, new Random(1), new[] { opener, swing });

            Assert.Equal(opener.Seconds + swing.Seconds, intro.Remaining, 3);

            while (intro.Running) intro.Update(1f / 60f);
            intro.Update(1f / 60f);

            Assert.True(Vector3.Distance(intro.Position, pose) < 1e-3f, $"held {Vector3.Distance(intro.Position, pose):F3} off the pose");
            Assert.True(Vector3.Distance(intro.Target, Target) < 1e-3f);
            Assert.Equal(FieldOfView, intro.FieldOfView, 4);
            Assert.Equal(0f, intro.Roll, 4);
        }

        private static float Yaw(Vector3 position, Vector3 target) => MathF.Atan2(target.Z - position.Z, target.X - position.X);

        private static float Pitch(Vector3 position, Vector3 target) =>
            MathF.Atan2(target.Y - position.Y, new Vector2(target.X - position.X, target.Z - position.Z).Length());
    }
}
