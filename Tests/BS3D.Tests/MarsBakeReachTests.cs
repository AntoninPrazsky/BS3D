using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Mars's baked ground (#1100): High and Ultra draw it wherever the lens stands within
    /// <see cref="MarsBackdrop.BAKE_CAMERA_REACH"/> of the arena, and the exact ground past it. Held over the real intro
    /// builder and the shipped config: no shot of the chapter intro stands inside the reach at any frame, so no shot turns
    /// from one ground to the other in flight; and from any lens inside the reach, the bake holds every point of the plain the
    /// haze leaves anything of.
    /// </summary>
    public class MarsBakeReachTests
    {
        private const float FRAME = 1f / 60f;

        private static readonly MarsSceneConfig Mars = new();

        [Fact]
        public void NoShotOfTheChapterIntroStandsInsideTheReach()
        {
            float nearest = float.MaxValue;
            string where = "";
            for (int seed = 0; seed < 64; seed++)
            {
                foreach (bool tracksDrawn in new[] { true, false })
                {
                    foreach (IntroShot shot in MarsIntroShots.Build(Mars, tracksDrawn, new Random(seed)))
                    {
                        int frames = (int)(shot.Seconds / FRAME);
                        for (int i = 0; i < frames; i++)
                        {
                            shot.Pose(i / (float)(frames - 1), out Vector3 lens, out _);
                            float radius = new Vector2(lens.X, lens.Z).Length();
                            if (radius < nearest)
                            {
                                nearest = radius;
                                where = $"seed {seed}, '{shot.Name}', frame {i}";
                            }
                        }
                    }
                }
            }

            Assert.True(nearest >= MarsBackdrop.BAKE_CAMERA_REACH,
                $"{where}: the lens stands {nearest:F1} from the arena, inside the bake's reach of {MarsBackdrop.BAKE_CAMERA_REACH}");
        }

        [Fact]
        public void TheBakeHoldsWhatTheHazeLeavesOfThePlainFromAnyLensInsideTheReach()
        {
            //Mars.fx's MarsLight: the ground is the horizon's colour outright from HorizonHazeDistance off the lens
            float seen = MarsBackdrop.BAKE_CAMERA_REACH + Mars.Air.HorizonHazeDistance;
            Assert.True(MarsBackdrop.BAKE_MAX_RADIUS >= seen,
                $"the bake reaches {MarsBackdrop.BAKE_MAX_RADIUS} from the arena; a lens at the reach sees the plain {seen} out");
        }
    }
}
