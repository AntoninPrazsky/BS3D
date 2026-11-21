using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;
using static BS3D.Tests.PlayViewOpenings;

namespace BS3D.Tests
{
    /// <summary>
    /// The Game's near plane follows the lens's distance (#1193, <see cref="RecoilCamera.NearPlaneAt"/>). The owner saw a line-like
    /// flicker round the drain's rim and down its inner sides in the meadow's intro, from the valley shot 167 to 197 units out, and
    /// none from the play view. Measured in frames: the dark pit sheath behind the glass won the depth test over the top of the
    /// glass, where the two part from nothing at the mouth, in streaks that moved with every frame of the flight, and the gold
    /// collar's edge in teeth; with the near plane held at 0.15 over that shot the streaks were gone and at 0.3 the teeth, the
    /// shadows untouched, so the depth buffer was the cause.
    /// What a number can hold is here: every play view keeps the near plane it had, the step is held where the rule says, and from
    /// the valley the band of the glass the depth buffer cannot part from the sheath spans less of the frame than from the play
    /// view the owner sees nothing wrong in; and the near plane never reaches the ground under any lens of the meadow's intro.
    /// </summary>
    public class NearPlaneTests
    {
        private readonly ITestOutputHelper _output;

        public NearPlaneTests(ITestOutputHelper output) => _output = output;

        //The near and far plane every lens that keeps the rule's lower end draws with
        private static readonly float NEAR = new RecoilCamera().NearPlane;
        private static readonly float FAR = new RecoilCamera().FarPlane;

        //The middle of the drain's mouth, where the glass meets the stone (the drain's own matrix, ArenaIsland._drainWorld)
        private static readonly Vector3 Mouth = new(0f, ArenaIsland.TOP_Y - ArenaIsland.DISH_DEPTH, 0f);

        //The widest window the game is played at (PlayViewOpenings.ASPECTS), which puts the near plane's corner farthest off
        private const float WIDEST_ASPECT = 2.4f;

        private const int ROLLS = 24;
        private const int POSES = 120;

        //A 24-bit depth step at view depth z under a perspective projection with these planes (standard Z, as RecoilCamera builds)
        private static float DepthStep(float z, float near) => z * z * (FAR - near) / (FAR * near * 16777216f);

        //The near plane a RecoilCamera posed so builds its projection with, read back off the projection (M43 / M33)
        private static float ProjectedNear(Vector3 lens, Vector3 target)
        {
            var camera = new RecoilCamera { BasePosition = lens, BaseTarget = target };
            camera.Recalculate();
            Assert.Equal(camera.EffectiveNearPlane, camera.Projection.M43 / camera.Projection.M33, 4);
            return camera.EffectiveNearPlane;
        }

        //How much of the frame the band of the glass nearer the sheath than a depth step spans, up to a factor every lens shares:
        //the band is as deep down the glass as the step at the mouth's far side (the glass and the sheath part linearly from the
        //mouth), and a length at depth z spans 1 / z of the frame
        private static float Band(Vector3 lens, float near)
        {
            float z = Vector3.Distance(lens, Mouth) + ArenaIsland.FUNNEL_TOP_RADIUS;
            return DepthStep(z, near) / z;
        }

        [Fact]
        public void A_lens_within_the_hold_of_its_target_or_the_arena_keeps_the_near_plane_and_none_passes_the_ceiling()
        {
            float hold = RecoilCamera.NEAR_HOLD_DISTANCE;
            foreach (float a in new[] { 0f, 1f, 30f, hold })
                foreach (float b in new[] { 0f, 45f, hold, hold * 2f, 900f, float.PositiveInfinity })
                {
                    Assert.Equal(NEAR, RecoilCamera.NearPlaneAt(NEAR, a, b));
                    Assert.Equal(NEAR, RecoilCamera.NearPlaneAt(NEAR, b, a));
                }

            //Past the hold the step at the nearer distance stays where it is at the hold, until the ceiling, and then the plane
            //stays at the ceiling however far
            float held = DepthStep(hold, NEAR);
            float previous = NEAR;
            for (float d = hold; d < 2500f; d += 0.5f)
            {
                float near = RecoilCamera.NearPlaneAt(NEAR, d, d + 10f);
                Assert.InRange(near, previous, RecoilCamera.NEAR_PLANE_CEILING);
                if (near < RecoilCamera.NEAR_PLANE_CEILING) Assert.True(DepthStep(d, near) <= held * 1.0001f, $"at {d} the step is {DepthStep(d, near)}");
                previous = near;
            }

            Assert.Equal(RecoilCamera.NEAR_PLANE_CEILING, RecoilCamera.NearPlaneAt(NEAR, 2000f, 2000f));
            Assert.Equal(1f, RecoilCamera.NearPlaneAt(1f, 2000f, 2000f));
        }

        /// <summary>
        /// "It costs nothing in the play view": every pose a shipped level's play view holds, at every window shape, is built with
        /// the near plane the game always had. Seen to fail with the hold at 30.
        /// </summary>
        [Fact]
        public void Every_play_view_keeps_the_near_plane()
        {
            float farthest = 0f;
            foreach (float aspect in ASPECTS)
                foreach (string file in Shipped.LevelFiles())
                    foreach ((string pose, Vector3 lens, Vector3 target, _) in Open(file, aspect).Poses)
                    {
                        float nearer = MathF.Min(Vector3.Distance(lens, target), Vector3.Distance(lens, new RecoilCamera().DepthSubject));
                        farthest = MathF.Max(farthest, nearer);
                        Assert.True(ProjectedNear(lens, target) == NEAR, $"{file} at {aspect:F2}, {pose}: the play view's near plane moved");
                    }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "the farthest a play view stands from the nearer of its target and the arena: {0:F1} (the hold {1})", farthest,
                RecoilCamera.NEAR_HOLD_DISTANCE));
        }

        /// <summary>
        /// The owner's report itself, in numbers: from every pose of the meadow's valley shot over the rolls, the band of the glass
        /// the depth buffer cannot part from the dark sheath behind it spans less of the frame than it does from the play view whose
        /// band is widest, where nobody has seen it: half as much. With the near plane at 0.05 it spanned 4.7 times as much, which
        /// is the streaks the frames showed (seen to fail with <see cref="RecoilCamera.NearPlaneAt"/> handing back its first argument).
        /// </summary>
        [Fact]
        public void From_the_meadows_valley_the_glass_the_depth_buffer_cannot_part_from_the_sheath_spans_less_than_from_the_play_view()
        {
            float playBand = 0f;
            foreach (float aspect in ASPECTS)
                foreach (string file in Shipped.LevelFiles())
                    foreach ((_, Vector3 lens, Vector3 target, _) in Open(file, aspect).Poses)
                        playBand = MathF.Max(playBand, Band(lens, ProjectedNear(lens, target)));

            float valleyBand = 0f, valleyBandAtNear = 0f;
            float nearestStand = float.MaxValue, farthestStand = 0f;
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(seed);
                IntroShot valley = Valley(planting, seed);
                for (int i = 0; i < POSES; i++)
                {
                    valley.Pose(i / (POSES - 1f), out Vector3 lens, out Vector3 target);
                    float stand = Vector3.Distance(lens, Mouth);
                    nearestStand = MathF.Min(nearestStand, stand);
                    farthestStand = MathF.Max(farthestStand, stand);
                    valleyBand = MathF.Max(valleyBand, Band(lens, ProjectedNear(lens, target)));
                    valleyBandAtNear = MathF.Max(valleyBandAtNear, Band(lens, NEAR));
                }
            }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "the valley stands {0:F0} to {1:F0} from the drain; the band from it is {2:F2} of the widest play view's ({3:F2} at the near plane 0.05)",
                nearestStand, farthestStand, valleyBand / playBand, valleyBandAtNear / playBand));
            Assert.True(valleyBandAtNear > playBand, "the valley no longer stands where the depth buffer fails the drain at 0.05: this test proves nothing");
            Assert.True(valleyBand < playBand, $"from the valley the band is {valleyBand / playBand:F2} of the play view's");
        }

        /// <summary>
        /// The other side of a farther near plane: it clips what stands nearer the lens than its corner. Over every shot of the
        /// meadow's intro and every roll, the corner of the near plane at the shot's lens and the widest window stays over the cut
        /// ground under the lens by a margin. Seen to fail with the hold at 15 and the ceiling at 12.
        /// </summary>
        [Fact]
        public void The_near_plane_never_reaches_the_ground_under_any_lens_of_the_meadows_intro()
        {
            float leastAir = float.MaxValue, mostNear = 0f;
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(seed);
                IntroShot[] shots = MeadowIntroShots.Build(planting.Config, planting.Figures, MeadowIntroShotsTests.GamePosition,
                    MeadowIntroShotsTests.GameTarget, MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW, new Random(seed * 7 + 1));
                foreach (IntroShot shot in shots)
                    for (int i = 0; i < POSES; i++)
                    {
                        float t = i / (POSES - 1f);
                        shot.Pose(t, out Vector3 lens, out Vector3 target);
                        float near = ProjectedNear(lens, target);
                        float tanHalf = MathF.Tan(shot.FieldOfViewAt(t) * 0.5f);
                        float corner = near * MathF.Sqrt(1f + tanHalf * tanHalf * (1f + WIDEST_ASPECT * WIDEST_ASPECT));
                        float air = lens.Y - TerrainMirror.Meadow(lens.X, lens.Z, planting.Config);
                        mostNear = MathF.Max(mostNear, near);
                        if (near > NEAR) leastAir = MathF.Min(leastAir, air - corner);
                        Assert.True(air > corner * 2f, $"roll {seed}, {shot.Name} at {t:F2}: the near plane's corner reaches {corner:F2} and the ground is {air:F2} under the lens");
                    }
            }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "the farthest near plane over the meadow's intro {0:F3}; wherever it is past 0.05, at least {1:F1} of air under its corner", mostNear, leastAir));
        }

        private static IntroShot Valley(MeadowIntroShotsTests.Planting planting, int seed)
        {
            IntroShot[] shots = MeadowIntroShots.Build(planting.Config, planting.Figures, MeadowIntroShotsTests.GamePosition,
                MeadowIntroShotsTests.GameTarget, MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW, new Random(seed * 7 + 1));
            Assert.Equal("the valley", shots[0].Name);
            return shots[0];
        }
    }
}
