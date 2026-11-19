using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The mountains' summit shot shows the rock, not the sky (#1159, the owner: "in the close-up of the mountain's peak the
    /// camera should look up somewhat less - as it is, half of the picture is sky, which is not necessary; the rock should be
    /// what is seen"). Held over the real builder and the shipped range (<see cref="TerrainMirror.Mountain"/>, the CPU mirror
    /// of the drawn field) at every roll: the summit's tip as drawn (not the highest point of the grid the shot finds it by,
    /// which can stand several units under it and beside it) stands high in the frame through the whole turn, and rays cast through the frame
    /// meet the rock for more than half of it. The ride itself is <see cref="IntroShotSmoothnessTests"/>'.
    /// </summary>
    public class MountainIntroShotsTests
    {
        private const float FieldOfView = DesertLandingTests.FieldOfView;
        private static readonly MountainSceneConfig Mountain = new();

        //The shot's one roll is which way round it turns, so a few seeds cover both
        private const int ROLLS = 6;

        //The drawn tip's height in the frame, -1 at the bottom edge to 1 at the top, through the whole shot: high, so the flanks
        //under it fill the frame, and short of the top so the summit stands whole against the sky over it; and how far across
        //the middle it may stand (the shot turns round the highest 6-unit sample, and the tip stands a little aside of it)
        private const float APEX_LOWEST = 0.5f, APEX_HIGHEST = 0.75f, APEX_ACROSS = 0.1f;

        //The tip is found round the summit the shot turns round (MountainIntroShots.Peak, the highest of a grid SUMMIT_STEP
        //apart) this far each way, in steps this fine: the drawn tip stands up to a step's diagonal off the highest sample
        private const float TIP_SEARCH = 10f, TIP_STEP = 0.1f;

        //More than this share of a 16:9 frame is rock (rays through a grid over it meeting the ground within ROCK_REACH), at the
        //shot's start, middle and end
        private const float ROCK_SHARE = 0.5f;
        private const float ROCK_REACH = 1500f;
        private const int RAYS_ACROSS = 48, RAYS_DOWN = 27;

        private readonly ITestOutputHelper _output;

        public MountainIntroShotsTests(ITestOutputHelper output) => _output = output;

        private static float Rock(float x, float z) => TerrainMirror.Mountain(x, z, Mountain);

        [Fact]
        public void The_summit_stands_high_in_the_frame_through_the_whole_turn()
        {
            Vector3 sampled = MountainIntroShots.Peak(Rock), tip = Tip(sampled);
            float lowest = float.MaxValue, highest = float.MinValue, aside = 0f;
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot shot = MountainIntroShots.Summit(Rock, FieldOfView, new Random(seed));
                for (int k = 0; k <= 120; k++)
                {
                    float t = k / 120f;
                    shot.Pose(t, out Vector3 lens, out Vector3 look);
                    Vector2 screen = DesertLandingTests.Screen(lens, look, tip, shot.FieldOfViewAt(t), 16f / 9f);
                    Assert.True(MathF.Abs(screen.X) < APEX_ACROSS, $"roll {seed}: the tip {screen.X:F2} across the frame at {t:F2}");
                    Assert.True(screen.Y >= APEX_LOWEST && screen.Y <= APEX_HIGHEST, $"roll {seed}: the tip {screen.Y:F2} up the frame at {t:F2}");
                    lowest = MathF.Min(lowest, screen.Y);
                    highest = MathF.Max(highest, screen.Y);
                    aside = MathF.Max(aside, MathF.Abs(screen.X));
                }
            }

            _output.WriteLine(FormattableString.Invariant(
                $"the tip {tip.Y - sampled.Y:F2} over the highest sample; {lowest:F2} to {highest:F2} up the frame (-1 to 1), at most {aside:F3} across"));
        }

        //The summit as drawn: the highest ground within TIP_SEARCH of the highest sample, TIP_STEP apart
        private static Vector3 Tip(Vector3 sampled)
        {
            Vector3 tip = sampled;
            for (float x = sampled.X - TIP_SEARCH; x <= sampled.X + TIP_SEARCH; x += TIP_STEP)
            {
                for (float z = sampled.Z - TIP_SEARCH; z <= sampled.Z + TIP_SEARCH; z += TIP_STEP)
                {
                    float h = Rock(x, z);
                    if (h > tip.Y) tip = new Vector3(x, h, z);
                }
            }

            return tip;
        }

        [Fact]
        public void Rock_fills_most_of_the_summit_shot()
        {
            float least = float.MaxValue, most = 0f;
            for (int seed = 1; seed <= 2; seed++)
            {
                IntroShot shot = MountainIntroShots.Summit(Rock, FieldOfView, new Random(seed));
                foreach (float t in new[] { 0f, 0.5f, 1f })
                {
                    shot.Pose(t, out Vector3 lens, out Vector3 look);
                    float share = RockShare(lens, look, shot.FieldOfViewAt(t), 16f / 9f);
                    Assert.True(share > ROCK_SHARE, $"roll {seed}: {share:P0} of the frame is rock at {t:F2}");
                    least = MathF.Min(least, share);
                    most = MathF.Max(most, share);
                }
            }

            _output.WriteLine(FormattableString.Invariant($"{least:P0} to {most:P0} of the frame is rock"));
        }

        //The share of a frame whose rays meet the ground: each marched from the lens in steps of half a unit, growing as a
        //hundredth of the distance past fifty, until it is under the ground, past ROCK_REACH, or climbing over the highest the
        //field can stand (TerrainMirror.Mountain's basin and its tallest massif at full height)
        private static float RockShare(Vector3 lens, Vector3 look, float fieldOfView, float aspect)
        {
            Vector3 forward = Vector3.Normalize(look - lens);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
            Vector3 up = Vector3.Cross(right, forward);
            float tan = MathF.Tan(fieldOfView * 0.5f);
            float ceiling = Mountain.LevelY + Mountain.ClearingRelief * 1.6f + Mountain.Height * 1.6f;

            int rock = 0;
            for (int j = 0; j < RAYS_DOWN; j++)
            {
                for (int i = 0; i < RAYS_ACROSS; i++)
                {
                    float x = (2f * (i + 0.5f) / RAYS_ACROSS - 1f) * tan * aspect;
                    float y = (1f - 2f * (j + 0.5f) / RAYS_DOWN) * tan;
                    Vector3 ray = Vector3.Normalize(forward + right * x + up * y);
                    for (float d = 1f; d < ROCK_REACH; d += MathF.Max(0.5f, d * 0.01f))
                    {
                        Vector3 p = lens + ray * d;
                        if (p.Y < Rock(p.X, p.Z)) { rock++; break; }
                        if (ray.Y >= 0f && p.Y > ceiling) break;
                    }
                }
            }

            return rock / (float)(RAYS_ACROSS * RAYS_DOWN);
        }
    }
}
