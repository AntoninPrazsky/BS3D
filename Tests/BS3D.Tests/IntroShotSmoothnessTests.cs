using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The chapter-intro shots that fly close over rough ground do not judder (#645, #530). Built through the
    /// real builders on the shipped scene configs over a dozen rolls, and sampled at sixty frames a second: the
    /// owner's "jerky, bumpy and too fast, like a roller coaster" on the mountains' pass and the desert's crest,
    /// and "the camera judders as it approaches the top" on the volcano's crater run, were measured as a lens
    /// moving at 30-59 units a second whose vertical acceleration ran to 436, 450 and 1141 u/s² and whose view
    /// pitched 100-160 degrees a second. The limits below sit between what the three shots do now (rms 8-15,
    /// pitch up to 32) and what they did (rms 49-253, pitch up to 159), so a cut that brings the rough ride
    /// back — a narrower window, a shorter clock, a hug that reads the ground point by point — fails here.
    /// </summary>
    public class IntroShotSmoothnessTests
    {
        private const int ROLLS = 12;
        private const float FRAME = 1f / 60f;

        //What "smooth and unhurried" means, in world units and degrees per second.
        private const float MAX_SPEED = 28f;
        private const float MAX_VERTICAL_ACCELERATION_RMS = 30f;
        private const float MAX_PITCH_RATE = 45f;
        private const float MAX_YAW_RATE = 50f;

        private static readonly float FieldOfView = MathHelper.ToRadians(45f);

        [Fact]
        public void TheMountainPassIsSlowAndSmooth()
        {
            var mountain = new MountainSceneConfig();
            Func<float, float, float> ground = (x, z) => TerrainMirror.Mountain(x, z, mountain);

            for (int seed = 1; seed <= ROLLS; seed++)
                AssertSmooth("the pass", MountainIntroShots.Pass(ground, FieldOfView, new Random(seed)), ground);
        }

        [Fact]
        public void TheDesertCrestIsSlowAndSmooth()
        {
            var desert = new DesertSceneConfig();
            Func<float, float, float> ground = (x, z) => TerrainMirror.Desert(x, z, desert);

            for (int seed = 1; seed <= ROLLS; seed++)
                AssertSmooth("the crest", DesertIntroShots.Crest(desert, ground, FieldOfView, new Random(seed)), ground);
        }

        [Fact]
        public void TheVolcanoCraterRunIsSlowAndSmooth()
        {
            var volcano = new VolcanoSceneConfig();
            Func<float, float, float> ground = (x, z) => TerrainMirror.Volcano(x, z, volcano);
            Vector2 cone = volcano.ConeCenter.ToVector2();
            Vector3 vent = new(cone.X, ground(cone.X, cone.Y) + 2f, cone.Y);

            for (int seed = 1; seed <= ROLLS; seed++)
                AssertSmooth("the crater", VolcanoIntroShots.Crater(ground, vent, volcano, FieldOfView, new Random(seed)), ground);
        }

        private static void AssertSmooth(string name, IntroShot shot, Func<float, float, float> ground)
        {
            int frames = (int)(shot.Seconds / FRAME);
            var position = new Vector3[frames];
            var pitch = new float[frames];
            var yaw = new float[frames];

            for (int i = 0; i < frames; i++)
            {
                shot.Pose(i / (float)(frames - 1), out Vector3 p, out Vector3 target);
                Vector3 forward = Vector3.Normalize(target - p);
                position[i] = p;
                pitch[i] = MathHelper.ToDegrees(MathF.Asin(MathHelper.Clamp(forward.Y, -1f, 1f)));
                yaw[i] = MathHelper.ToDegrees(MathF.Atan2(forward.X, forward.Z));

                //Never inside the land: a smooth ride that cuts through the dune is not a fix.
                Assert.True(p.Y > ground(p.X, p.Z) + 1f, $"{name}: the lens is {p.Y - ground(p.X, p.Z):F2} over the ground at frame {i}");
            }

            float dt = shot.Seconds / (frames - 1);
            float travelled = 0f;
            double accelerationSquares = 0;
            float maxPitchRate = 0f, maxYawRate = 0f;
            float previousVertical = 0f;

            for (int i = 1; i < frames; i++)
            {
                travelled += Vector3.Distance(position[i], position[i - 1]);

                float vertical = (position[i].Y - position[i - 1].Y) / dt;
                float acceleration = i >= 2 ? (vertical - previousVertical) / dt : 0f;
                previousVertical = vertical;
                accelerationSquares += acceleration * acceleration;

                float yawStep = yaw[i] - yaw[i - 1];
                while (yawStep > 180f) yawStep -= 360f;
                while (yawStep < -180f) yawStep += 360f;

                //Skip the first frame: it has no neighbour before it.
                if (i >= 2)
                {
                    maxPitchRate = MathF.Max(maxPitchRate, MathF.Abs(pitch[i] - pitch[i - 1]) / dt);
                    maxYawRate = MathF.Max(maxYawRate, MathF.Abs(yawStep) / dt);
                }
            }

            float speed = travelled / shot.Seconds;
            float accelerationRms = MathF.Sqrt((float)(accelerationSquares / (frames - 2)));

            Assert.True(speed <= MAX_SPEED, $"{name}: {speed:F1} units a second is too fast");
            Assert.True(accelerationRms <= MAX_VERTICAL_ACCELERATION_RMS, $"{name}: vertical acceleration {accelerationRms:F1} u/s² rms is a roller coaster");
            Assert.True(maxPitchRate <= MAX_PITCH_RATE, $"{name}: the view pitches {maxPitchRate:F1} degrees a second");
            Assert.True(maxYawRate <= MAX_YAW_RATE, $"{name}: the view turns {maxYawRate:F1} degrees a second");
        }
    }
}
