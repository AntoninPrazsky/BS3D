using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The cities' swing home (#972, #973, <see cref="CitySwing"/>): the owner's "Spider-Man-style swinging fly-throughs until
    /// we finally arrive at the island and the camera gets into the game camera position", held over real cities of both
    /// kinds and a sweep of gameplay poses round the arena: it is found for nearly every pose, it ends exactly on the pose
    /// with the game's own lens and no bank, it never comes near a tower, and its ride stays inside the limits it was tuned
    /// to. The two cities fly their own styles, the day city's two corners and the neon city's three.
    /// </summary>
    public class CitySwingTests
    {
        private static readonly float FieldOfView = MathF.PI / 4.2f;
        private static readonly Vector3 Target = new(0f, 3.5f, 0f);

        //The game's lens stands 28-45 out, a little under the gun (GameCameraFit; the Spectrum's first level logs 28.7 out,
        //the Arcade's 30), on any bearing, and a level opens with it ON AN AXIS (bearing 90), so the sweep takes the axes
        //exactly: a sweep offset from them hid that the day city found a swing for only 77 of 108 axis-heavy poses before the
        //cross streets (see CitySwing.Lay). The neon city's clearing ends 41 out (its blocks are 25 apart against the day
        //city's 30), so its poses are swept inside that.
        private static readonly (bool Neon, float[] Radii)[] Cities = { (false, new[] { 29f, 37f, 45f }), (true, new[] { 29f, 34f, 39f }) };
        private const int BEARINGS = 12;
        private const int CITIES = 3;

        //How much of the sweep must get a swing: the rest fall back to the old reel and its tour leg
        private const float MIN_BUILT_SHARE = 0.95f;

        //The ride, as tuned (desktop probe over the sweep below, 2026-10-24). The day city: up to 59 u/s at the bottom of a
        //sag, the view turning up to 155 degrees a second round a corner and pitching up to 51, banked up to 10 degrees,
        //6.8-7.8 s, 5.3 u/s in the last half second. The neon city, its three corners in narrower streets: up to 51 u/s,
        //turning up to 164 and pitching up to 56, banked up to 10, 6.6-8.9 s, 4.0 u/s landing. The limits leave a margin
        //over both and catch a change that brings back the first cut's 200-330 degree-a-second spikes or its 10-second length.
        private const float MAX_SPEED = 65f;
        private const float MAX_YAW_RATE = 170f;
        private const float MAX_PITCH_RATE = 80f;
        private const float MAX_ROLL_DEGREES = 14f;
        private const float MAX_LANDING_SPEED = 8f;
        private const float MIN_SECONDS = 5.5f, MAX_SECONDS = 9f;

        //How finely the shot is walked, and how far from a tower's box every pose must stay (the builder keeps 2 at its
        //points; the lens runs a spline through them)
        private const int POSES = 1500;
        private const float CLEARANCE = 1f;

        private static City CityOf(bool neon, int seed)
        {
            var config = new CitySceneConfig();
            config.Seed += seed;
            config.NeonLayout.Seed += seed;
            return new City(config, neon, ArenaIsland.RADIUS);
        }

        private static Vector3 Pose(int bearing, float radius)
        {
            float angle = MathHelper.TwoPi * bearing / BEARINGS;
            return new Vector3(MathF.Cos(angle) * radius, -4f, MathF.Sin(angle) * radius);
        }

        private static IntroShot Swing(City city, bool neon, Vector3 pose, int seed) =>
            CitySwing.Build(city, pose, Target, FieldOfView, new Random(seed), neon ? CitySwing.Neon : CitySwing.Day);

        [Fact]
        public void A_swing_is_found_for_nearly_every_pose()
        {
            foreach ((bool neon, float[] radii) in Cities)
            {
                int built = 0, tried = 0;
                for (int c = 0; c < CITIES; c++)
                {
                    City city = CityOf(neon, c);
                    for (int b = 0; b < BEARINGS; b++)
                        foreach (float r in radii)
                        {
                            tried++;
                            if (Swing(city, neon, Pose(b, r), c * 100 + b) != null) built++;
                        }
                }

                Assert.True(built >= MIN_BUILT_SHARE * tried, $"neon {neon}: a swing for {built} of {tried} poses");
            }
        }

        [Fact]
        public void It_lands_on_the_pose_with_the_games_lens_and_level()
        {
            foreach ((bool neon, float[] radii) in Cities)
            for (int b = 0; b < BEARINGS; b++)
            {
                City city = CityOf(neon, 0);
                Vector3 pose = Pose(b, radii[1]);
                IntroShot shot = Swing(city, neon, pose, b);
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
            foreach ((bool neon, float[] radii) in Cities)
            for (int c = 0; c < CITIES; c++)
            {
                City city = CityOf(neon, c);
                var towers = new CitySwing.Towers(city);

                for (int b = 0; b < BEARINGS; b++)
                    foreach (float r in radii)
                    {
                        IntroShot shot = Swing(city, neon, Pose(b, r), c * 100 + b);
                        if (shot == null) continue;

                        for (int i = 0; i <= POSES; i++)
                        {
                            shot.Pose(i / (float)POSES, out Vector3 position, out _);
                            Assert.True(towers.Clear(position, CLEARANCE),
                                $"neon {neon}, city {c}, bearing {b}, radius {r}: the lens at {i}/{POSES} ({position.X:F1}, {position.Y:F1}, {position.Z:F1}) stands in a tower");
                        }
                    }
            }
        }

        [Fact]
        public void The_ride_stays_inside_its_limits()
        {
            foreach ((bool neon, float[] radii) in Cities)
            for (int c = 0; c < CITIES; c++)
            {
                City city = CityOf(neon, c);
                for (int b = 0; b < BEARINGS; b++)
                    foreach (float r in radii)
                    {
                        IntroShot shot = Swing(city, neon, Pose(b, r), c * 100 + b);
                        if (shot == null) continue;

                        string where = $"neon {neon}, city {c}, bearing {b}, radius {r}";
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
            City city = CityOf(false, 0);
            Vector3 pose = Pose(3, 37f);
            IntroShot swing = Swing(city, false, pose, 3);
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
