using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Eruption chapter's prologue (#975): the owner's playtest note — "the first two shots are similar. We should
    /// start with a global view of the volcano. Then a cut to the volcano spewing. Then the continuation" — held over the
    /// real builder on the shipped config and its height mirror: the three shots in that order, every cut a different
    /// picture, every lens clear of the cone, the volcano and the island in the first frame, the vent and its jets in the
    /// second, and the cued burst going off inside the second.
    /// </summary>
    public class VolcanoIntroShotsTests
    {
        private const int ROLLS = 16;

        //The vertical field of view the game hands the intro (GameplayScreen.GAME_FOV), which each shot widens from,
        //and the narrowest window shape the owner plays at: the projection is Hor+, so a narrower one crops the sides.
        private const float INTRO_FIELD_OF_VIEW = MathF.PI / 4.2f;
        private const float ASPECT = 16f / 9f;

        //Across a cut the lens must move AND the view must turn. The cities' test takes either, at 22 degrees or 40 units;
        //here the two shots the note named were 56 units apart at best and turned only 18 degrees, a view sliding along its
        //own line, which is the same picture. The angle is film's thirty-degree rule (a smaller change of angle on the same
        //subject reads as a jump cut), and the distance is set against a subject 250 to 600 units off: the old flank put
        //back as the second shot cuts to the crater at 22-30 degrees and 62-73 units, and fails both.
        private const float MIN_CUT_ANGLE_DEGREES = 30f;
        private const float MIN_CUT_DISTANCE = 100f;

        //How far over the mirror's ground every lens must stay, sampled along every shot. The drawn ground carries scoria
        //and blocks the mirror leaves out (docs/scenes/volcano.md), up to five units.
        private const float MIN_CLEARANCE = 8f;

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame.
        private const float FRAME_MARGIN = 0.05f;

        //How high the jets reach over the vent at a burst's peak: Speed² / (2 Gravity) at the burst's boost, a little
        //under so the test holds the visible column and not the last blob.
        private const float JET_REACH = 60f;

        private const int POSES = 120;

        private static IntroShot[] Shots(int seed, out VolcanoSceneConfig volcano, out Func<float, float, float> ground, out Vector3 vent)
        {
            var config = new VolcanoSceneConfig();
            Func<float, float, float> mirror = (x, z) => TerrainMirror.Volcano(x, z, config);
            volcano = config;
            ground = mirror;
            vent = new VolcanoLava(config, 0).VentPosition[0];
            return VolcanoIntroShots.Shots(mirror, vent, config, INTRO_FIELD_OF_VIEW, new Random(seed));
        }

        [Fact]
        public void TheVolcanoOpensThenTheEruptionThenTheCrater()
        {
            IntroShot[] shots = Shots(1, out _, out _, out _);

            Assert.Equal(new[] { "the volcano", "the eruption", "the crater" }, Array.ConvertAll(shots, shot => shot.Name));
        }

        [Fact]
        public void NoCutLandsOnTheSamePicture()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot[] shots = Shots(seed, out _, out _, out _);

                for (int i = 0; i + 1 < shots.Length; i++)
                {
                    shots[i].Pose(1f, out Vector3 fromPosition, out Vector3 fromTarget);
                    shots[i + 1].Pose(0f, out Vector3 toPosition, out Vector3 toTarget);

                    Vector3 before = Vector3.Normalize(fromTarget - fromPosition);
                    Vector3 after = Vector3.Normalize(toTarget - toPosition);
                    float angle = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector3.Dot(before, after), -1f, 1f)));
                    float distance = Vector3.Distance(fromPosition, toPosition);

                    Assert.True(angle >= MIN_CUT_ANGLE_DEGREES && distance >= MIN_CUT_DISTANCE,
                        $"seed {seed}: '{shots[i].Name}' -> '{shots[i + 1].Name}' cuts to a view {angle:F0} degrees and {distance:F0} units from the last");
                }
            }
        }

        [Fact]
        public void EveryLensStaysClearOfTheCone()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (IntroShot shot in Shots(seed, out _, out Func<float, float, float> ground, out _))
                    for (int i = 0; i < POSES; i++)
                    {
                        shot.Pose(i / (float)(POSES - 1), out Vector3 position, out _);
                        float under = ground(position.X, position.Z);
                        Assert.True(position.Y - under >= MIN_CLEARANCE,
                            $"seed {seed}: '{shot.Name}' at {i}/{POSES} stands {position.Y - under:F1} over the ground");
                    }
        }

        [Fact]
        public void TheFirstShotHoldsTheVolcanoAndTheIsland()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot shot = Shots(seed, out VolcanoSceneConfig volcano, out Func<float, float, float> ground, out Vector3 vent)[0];
                Vector2 cone = volcano.ConeCenter.ToVector2();

                //The cone's foot under the axis, its vent, and the island, all through the whole shot
                Vector3 foot = new(cone.X, ground(cone.X + volcano.ConeRadius, cone.Y), cone.Y);
                Vector3 island = new(0f, volcano.LevelY, 0f);

                for (int i = 0; i < POSES; i++)
                {
                    float t = i / (float)(POSES - 1);
                    AssertInFrame(shot, t, vent, $"seed {seed}: the vent");
                    AssertInFrame(shot, t, foot, $"seed {seed}: the cone's foot");
                    AssertInFrame(shot, t, island, $"seed {seed}: the island");
                }
            }
        }

        [Fact]
        public void TheEruptionShotHoldsTheVentAndItsJets()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot shot = Shots(seed, out _, out _, out Vector3 vent)[1];

                for (int i = 0; i < POSES; i++)
                {
                    float t = i / (float)(POSES - 1);
                    AssertInFrame(shot, t, vent, $"seed {seed}: the vent");
                    AssertInFrame(shot, t, vent + new Vector3(0f, JET_REACH, 0f), $"seed {seed}: the jets' top");
                }
            }
        }

        [Fact]
        public void TheCuedBurstGoesOffInsideTheEruptionShot()
        {
            IntroShot[] shots = Shots(1, out VolcanoSceneConfig volcano, out _, out _);
            const float start = 1234.5f;

            float onset = VolcanoIntroShots.BurstOnset(start) - start;
            float peak = onset + 0.14f * volcano.Eruption.Length;

            //The light starts after the cut to the eruption and peaks before the cut away from it
            Assert.InRange(onset, shots[0].Seconds + 0.5f, shots[0].Seconds + shots[1].Seconds);
            Assert.InRange(peak, shots[0].Seconds, shots[0].Seconds + shots[1].Seconds - 1f);
        }

        [Fact]
        public void TheEruptionShotRecuesTheBurstAsItComesOnScreen()
        {
            int cues = 0;
            var config = new VolcanoSceneConfig();
            IntroShot[] shots = VolcanoIntroShots.Shots((x, z) => TerrainMirror.Volcano(x, z, config), new VolcanoLava(config, 0).VentPosition[0],
                config, INTRO_FIELD_OF_VIEW, new Random(1), () => cues++);

            shots[0].Start();
            shots[2].Start();
            Assert.Equal(0, cues);

            shots[1].Start();
            Assert.Equal(1, cues);
        }

        [Fact]
        public void TheBurstIsCuedAgainOnlyWhenTheIntroFellBehind()
        {
            const float start = 600f;
            float onTime = start + shotsBefore();

            //On time, or a hitch's worth late: the cue made at the build stands
            Assert.True(float.IsNaN(VolcanoIntroShots.Recue(start, onTime)));
            Assert.True(float.IsNaN(VolcanoIntroShots.Recue(start, onTime + 0.05f)));

            //Four seconds behind a pause page: the burst goes off the lead after the shot really starts
            Assert.Equal(onTime + 4f + VolcanoIntroShots.BURST_LEAD, VolcanoIntroShots.Recue(start, onTime + 4f), 3);

            static float shotsBefore() => VolcanoIntroShots.BurstOnset(0f) - VolcanoIntroShots.BURST_LEAD;
        }

        private static void AssertInFrame(IntroShot shot, float t, Vector3 point, string what)
        {
            shot.Pose(t, out Vector3 position, out Vector3 target);
            Matrix view = Matrix.CreateLookAt(position, target, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(shot.FieldOfView, ASPECT, 1f, 5000f);

            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), view * projection);
            float x = clip.X / clip.W, y = clip.Y / clip.W;

            Assert.True(clip.W > 0f && MathF.Abs(x) <= 1f - FRAME_MARGIN && MathF.Abs(y) <= 1f - FRAME_MARGIN,
                $"{what} at ({x:F2}, {y:F2}) of '{shot.Name}' at t {t:F2}");
        }
    }
}
