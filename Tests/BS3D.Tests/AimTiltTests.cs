using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The overview lens's tilt with the aim (#720): zero at the aim the level opens on, rising with the barrel inside its
    /// two limits and reaching most of them at the ends of the gun's range, a gun keep and a top-row keep that put the
    /// muzzle and the top row exactly on their lines through the real projection, a bound that never pushes the lens the
    /// other way, and a chase that is the same at every frame rate, accelerates into a sudden change rather than jumping
    /// with it, and from rest never passes what it chases.
    /// </summary>
    public class AimTiltTests
    {
        //Where the level opens the gun on the shipped set: the rest stance's elevation, 33.8° (the [camera] lines of #720)
        private static readonly float SHIPPED_REST = MathHelper.ToRadians(33.8f);

        [Fact]
        public void AnUntouchedAimLeavesTheFrameAsItIs()
        {
            Assert.Equal(0f, AimTilt.TargetFor(0f));
            //The clamped tall-level aim is rest + (limit - rest), a float's noise off its own zero
            Assert.Equal(0f, AimTilt.TargetFor(3e-8f));
            Assert.Equal(0f, AimTilt.TargetFor(-3e-8f));

            Vector3 lens = new(0f, -9f, 30f), lookAt = new(0f, -1.3f, 0f);
            Assert.Equal(lookAt, AimTilt.Tilted(lens, lookAt, 0f));
        }

        [Fact]
        public void TheTargetRisesWithTheBarrelInsideItsLimits()
        {
            float previous = float.NegativeInfinity;

            for (float offset = -2f; offset <= 2f; offset += 0.001f)
            {
                float target = AimTilt.TargetFor(offset);

                Assert.True(target >= previous, $"the tilt fell from {previous} to {target} as the aim rose to {offset}");
                Assert.True(target <= AimTilt.UP_MAX && target >= -AimTilt.DOWN_MAX, $"{target} at {offset} is past its limit");
                Assert.True(MathF.Sign(target) == MathF.Sign(offset) || target == 0f, $"{target} leans against the aim {offset}");
                previous = target;
            }

            //GAIN is the slope round the zero, both ways: the bend is a tanh, so there is no corner at the zero either
            const float NUDGE = 1e-3f;
            Assert.Equal(AimTilt.GAIN, AimTilt.TargetFor(NUDGE) / NUDGE, 3);
            Assert.Equal(AimTilt.GAIN, AimTilt.TargetFor(-NUDGE) / -NUDGE, 3);
        }

        [Fact]
        public void TheEndsOfTheGunsRangeLeanMostOfTheWay()
        {
            //The lens has to visibly follow the barrel at both ends, not creep: the gun's own range from where it rests
            float top = AimTilt.TargetFor(Cannon.MaxElevation - SHIPPED_REST);
            float bottom = AimTilt.TargetFor(Cannon.MinElevation - SHIPPED_REST);

            Assert.True(top > 0.85f * AimTilt.UP_MAX, $"the top of the range leans {MathHelper.ToDegrees(top):F2} deg");
            Assert.True(bottom < -0.85f * AimTilt.DOWN_MAX, $"the bottom of the range leans {MathHelper.ToDegrees(bottom):F2} deg");
        }

        [Fact]
        public void TheTiltTurnsAboutTheLensOnTheSameBearing()
        {
            Vector3 lens = new(3f, -9f, 28f), lookAt = new(-1f, -1.3f, -2f);
            float angle = MathHelper.ToRadians(7f);

            Vector3 tilted = AimTilt.Tilted(lens, lookAt, angle);

            Assert.Equal(Vector3.Distance(lens, lookAt), Vector3.Distance(lens, tilted), 4);
            Assert.Equal(MathF.Atan2(lookAt.X - lens.X, lookAt.Z - lens.Z), MathF.Atan2(tilted.X - lens.X, tilted.Z - lens.Z), 5);
            Assert.Equal(Pitch(lens, lookAt) + angle, Pitch(lens, tilted), 5);
        }

        [Theory]
        [InlineData(0f, 16f / 9f)]
        [InlineData(1.8f, 16f / 9f)]
        [InlineData(-1.2f, 2.4f)]
        public void TheGunKeepLimitPutsTheMuzzleOnItsLine(float sideways, float aspect)
        {
            //An overview of the shipped proportions: the lens 30 out and below, the muzzle 13 in front of it and a little
            //above, off to the side as a traversed or trailed gun stands
            Vector3 lens = new(0f, -9.1f, 30.5f), lookAt = new(0f, -1.3f, 0f);
            Vector3 muzzle = new(sideways, -6.5f, 16.5f);
            float fov = MathF.PI / 4.2f;

            float limit = AimTilt.GunKeepLimit(lens, lookAt, muzzle, fov);
            Assert.True(limit > 0f, $"a gun the fit framed whole allows no tilt up ({limit})");

            //Through the projection the camera draws with: tilted by the limit, the muzzle sits exactly on its line
            Assert.Equal(1f - AimTilt.GUN_KEEP_FRACTION, ScreenY(lens, AimTilt.Tilted(lens, lookAt, limit), muzzle, fov, aspect), 3);

            //...and a hair further up sinks it under the line, a hair less keeps it above
            Assert.True(ScreenY(lens, AimTilt.Tilted(lens, lookAt, limit + 0.01f), muzzle, fov, aspect) > 1f - AimTilt.GUN_KEEP_FRACTION);
            Assert.True(ScreenY(lens, AimTilt.Tilted(lens, lookAt, limit - 0.01f), muzzle, fov, aspect) < 1f - AimTilt.GUN_KEEP_FRACTION);
        }

        [Theory]
        [InlineData(0f, 16f / 9f)]
        [InlineData(1.8f, 16f / 9f)]
        [InlineData(-1.2f, 2.4f)]
        public void TheTopKeepLimitPutsTheTopRowOnItsLine(float sideways, float aspect)
        {
            //The same overview, and a top corner of the cluster's top row: the near edge of a 13-cell plate's footprint,
            //a unit under the glass of a field whose top hangs at the shipped height
            Vector3 lens = new(0f, -9.1f, 30.5f), lookAt = new(0f, -1.3f, 0f);
            Vector3 corner = new(sideways - 7f, 6.2f, 7f);
            float fov = MathF.PI / 4.2f;

            float limit = AimTilt.TopKeepLimit(lens, lookAt, corner, fov);
            Assert.True(limit > 0f, $"a top row the fit framed allows no tilt down ({limit})");

            //Tilted down by the limit, the corner sits exactly on its line through the projection the camera draws with
            Assert.Equal(AimTilt.TOP_KEEP_FRACTION, ScreenY(lens, AimTilt.Tilted(lens, lookAt, -limit), corner, fov, aspect), 3);

            //...a hair further down lifts it over the line, a hair less keeps it under
            Assert.True(ScreenY(lens, AimTilt.Tilted(lens, lookAt, -limit - 0.01f), corner, fov, aspect) < AimTilt.TOP_KEEP_FRACTION);
            Assert.True(ScreenY(lens, AimTilt.Tilted(lens, lookAt, -limit + 0.01f), corner, fov, aspect) > AimTilt.TOP_KEEP_FRACTION);
        }

        [Fact]
        public void TheRoomBoundsTheTiltAndNeverPushesIt()
        {
            float up = MathHelper.ToRadians(4f), down = MathHelper.ToRadians(2f);

            //Inside the room the wanted tilt passes untouched, past it each side stops at its own room
            Assert.Equal(0.03f, AimTilt.Bounded(0.03f, up, down));
            Assert.Equal(-0.02f, AimTilt.Bounded(-0.02f, up, down));
            Assert.Equal(up, AimTilt.Bounded(AimTilt.UP_MAX, up, down));
            Assert.Equal(-down, AimTilt.Bounded(-AimTilt.DOWN_MAX, up, down));

            //A room already gone stops its side at zero and never tilts the lens the other way to make up for it
            Assert.Equal(0f, AimTilt.Bounded(AimTilt.UP_MAX, -0.05f, down));
            Assert.Equal(0f, AimTilt.Bounded(-AimTilt.DOWN_MAX, up, -0.05f));
            Assert.Equal(0f, AimTilt.Bounded(0f, -0.05f, -0.05f));
            Assert.Equal(0.01f, AimTilt.Bounded(0.01f, up, -0.05f));
        }

        [Fact]
        public void TheChaseIsTheSameAtEveryFrameRate()
        {
            //From a lens already moving (a sweep under way), towards a held target the other way: 24, 60 and 240 frames a
            //second have to be at the same place at every instant they share, because the closed form composes exactly
            float[] slow = Chase(24, 2f);
            float[] mid = Chase(60, 2f);
            float[] fast = Chase(240, 2f);

            for (int i = 0; i < slow.Length; i++)
            {
                Assert.Equal(slow[i], Sample(mid, 60, i / 24f), 5);
                Assert.Equal(slow[i], Sample(fast, 240, i / 24f), 5);
            }
        }

        [Fact]
        public void FromRestItAcceleratesAndNeverPasses()
        {
            const float TARGET = 0.1f, DT = 1f / 60f;
            AimTilt tilt = new();
            tilt.Step(0f, DT);

            float first = tilt.Step(TARGET, DT);

            //Second order: the first frame after a step moves a sliver, where a first-order chase of the same time
            //constant would take an eighth of the way at once — the jolt a mouse flick would otherwise put in the lens
            Assert.True(first < 0.02f * TARGET, $"the first frame took {first / TARGET:P1} of the step");

            float previous = first;
            float at = DT;
            bool tookMost = false;

            while (at < 3f)
            {
                float angle = tilt.Step(TARGET, DT);
                at += DT;

                Assert.True(angle >= previous, $"the chase turned back at {at:F2} s");
                Assert.True(angle <= TARGET, $"the chase passed its target at {at:F2} s: {angle}");
                if (at <= 0.51f && angle >= 0.9f * TARGET) tookMost = true;
                previous = angle;
            }

            Assert.True(tookMost, "the chase had not taken 90 % of the step by half a second");
            Assert.Equal(TARGET, tilt.Angle);
            Assert.Equal(0f, tilt.Rate);
        }

        [Fact]
        public void ALongFrameLandsAndDoesNotRing()
        {
            AimTilt tilt = new();
            tilt.Step(0f, 0.016f);
            for (int i = 0; i < 10; i++) tilt.Step(0.12f, 0.016f);

            //A resumed pause hands the camera five seconds in one frame
            float angle = tilt.Step(-0.05f, 5f);

            Assert.Equal(-0.05f, angle, 4);
            Assert.True(MathF.Abs(tilt.Rate) < 1e-3f);
        }

        [Fact]
        public void AReseedLandsOnTheTarget()
        {
            AimTilt tilt = new();
            tilt.Step(0f, 0.016f);
            for (int i = 0; i < 5; i++) tilt.Step(0.12f, 0.016f);

            tilt.Reseed();

            Assert.Equal(-0.03f, tilt.Step(-0.03f, 0.016f));
            Assert.Equal(0f, tilt.Rate);
        }

        //The angle at each frame boundary over the given seconds: the chase seeded at 0 and set moving up by the same
        //prelude of millisecond steps whatever the frame rate, then chasing a target below it at that rate
        private static float[] Chase(int framesPerSecond, float seconds)
        {
            AimTilt tilt = new();
            tilt.Step(0f, 0.001f);
            for (int i = 0; i < 100; i++) tilt.Step(0.3f, 0.001f);
            Assert.True(tilt.Rate > 0.5f, $"the prelude left the chase at {tilt.Rate} rad/s");

            float dt = 1f / framesPerSecond;
            int frames = (int)MathF.Round(seconds * framesPerSecond);
            float[] angles = new float[frames + 1];
            angles[0] = tilt.Angle;

            for (int i = 1; i <= frames; i++) angles[i] = tilt.Step(0.08f, dt);

            return angles;
        }

        private static float Sample(float[] angles, int framesPerSecond, float seconds) =>
            angles[(int)MathF.Round(seconds * framesPerSecond)];

        private static float Pitch(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            return MathF.Atan2(d.Y, MathF.Sqrt(d.X * d.X + d.Z * d.Z));
        }

        //A point's height in the frame, 0 the top edge and 1 the bottom, through the view and projection the Game's camera
        //builds (world up, the vertical field of view)
        private static float ScreenY(Vector3 lens, Vector3 lookAt, Vector3 point, float fov, float aspect)
        {
            Matrix viewProjection = Matrix.CreateLookAt(lens, lookAt, Vector3.Up)
                * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 1000f);
            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
            return (1f - clip.Y / clip.W) * 0.5f;
        }
    }
}
