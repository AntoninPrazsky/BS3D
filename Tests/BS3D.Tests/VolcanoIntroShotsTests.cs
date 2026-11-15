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
    /// real builder on the shipped config and its height mirror: the shots in that order, every cut a different picture,
    /// every lens clear of the cone, the volcano and the island in the first frame, the vent and its jets in the second,
    /// and the cued burst going off inside the second. And the land past the field (#1127, the owner: "there should also be
    /// shots of the village, the river, the mountains and the forests in the distance"): the village orbited last, over
    /// every tree, house and the church, the village in the frame throughout and the cone over it at the end, the cut out of
    /// it into the tour's last leg a different picture too, and no such shot on a tier that draws no village.
    /// </summary>
    public class VolcanoIntroShotsTests
    {
        private const int ROLLS = 16;

        //The prologue and the tour's last leg together, with the village and without it (Low): the owner prefers a faster
        //reel to a longer one, so the village came in with the first two shots shortened (17.3 s before it, 20.1 with it), and
        //a reel that grows past these fails here rather than in his hands.
        private const float MAX_SECONDS = 20.5f;
        private const float MAX_SECONDS_WITHOUT_THE_VILLAGE = 17.3f;

        //How far over everything the land stands the village's lens keeps (the shot flies VolcanoIntroShots.TREE_AIR over it,
        //reading the ground more coarsely than this test does), and how finely round the lens the ground is read for it
        private const float STANDING_MARGIN = 2f;
        private const int RING_BEARINGS = 16;
        private const int RING_STEPS = 4;

        //The skip hint stands centred along the frame's foot, up to 12.2 % of its height: the village's middle stays this far
        //up the frame (in the frame's half-heights from its middle) through the whole shot
        private const float OVER_THE_HINT = -0.6f;

        //The village held in the frame: its middle, this far over the ground there (about its roofs)
        private const float VILLAGE_ROOFS = 6f;

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

        //The cut out of the reel into the tour's last leg, over this many rolls with the gun on each of GUN_STANDS sides of the
        //arena (the first is where it opens, on +Z facing the cone): the play camera GUN_DISTANCE off the aim it looks at and
        //GUN_UNDER_AIM over it, as the Eruption's first level stands it, and the poses read CUT_STEP either side of the cut
        private const int CUT_ROLLS = 32;
        private const int GUN_STANDS = 4;
        private const float GUN_DISTANCE = 31f;
        private const float GUN_UNDER_AIM = -4.5f;
        private const float CUT_STEP = 1e-3f;
        private static readonly Vector3 PLAY_AIM = new(0f, -1.6f, 0f);

        //How far over the mirror's ground every lens must stay, sampled along every shot. The drawn ground carries scoria
        //and blocks the mirror leaves out (docs/scenes/volcano.md), up to five units.
        private const float MIN_CLEARANCE = 8f;

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame.
        private const float FRAME_MARGIN = 0.05f;

        //How high the jets reach over the vent at a burst's peak: Speed² / (2 Gravity) at the burst's boost, a little
        //under so the test holds the visible column and not the last blob.
        private const float JET_REACH = 60f;

        private const int POSES = 120;

        private static IntroShot[] Shots(int seed, out VolcanoSceneConfig volcano, out Func<float, float, float> ground, out Vector3 vent,
            bool village = true) => Shots(new Random(seed), out volcano, out ground, out vent, village);

        private static IntroShot[] Shots(Random random, out VolcanoSceneConfig volcano, out Func<float, float, float> ground, out Vector3 vent,
            bool village = true)
        {
            var config = new VolcanoSceneConfig();
            Func<float, float, float> mirror = (x, z) => TerrainMirror.Volcano(x, z, config);
            volcano = config;
            ground = mirror;
            vent = new VolcanoLava(config, 0).VentPosition[0];
            return VolcanoIntroShots.Shots(mirror, vent, config, INTRO_FIELD_OF_VIEW, random, village: village);
        }

        [Fact]
        public void TheVolcanoOpensThenTheEruptionThenTheCraterThenTheVillage()
        {
            IntroShot[] shots = Shots(1, out _, out _, out _);

            Assert.Equal(new[] { "the volcano", "the eruption", "the crater", "the village" }, Array.ConvertAll(shots, shot => shot.Name));
        }

        /// <summary>
        /// Low draws the land's ground but not its woods or houses: there is no village to shoot, and the reel is the three
        /// shots it was before #1127. Whether the village is rolled never moves what the session deals after the intro (the
        /// benchmark plays one seed on every tier), nor what the three shots roll.
        /// </summary>
        [Fact]
        public void WithoutTheVillageTheReelIsTheThreeShotsAndTheSessionRollsTheSame()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var withRandom = new Random(seed);
                var withoutRandom = new Random(seed);
                IntroShot[] with = Shots(withRandom, out _, out _, out _, village: true);
                IntroShot[] without = Shots(withoutRandom, out _, out _, out _, village: false);

                Assert.Equal(new[] { "the volcano", "the eruption", "the crater" }, Array.ConvertAll(without, shot => shot.Name));
                Assert.Equal(withRandom.Next(), withoutRandom.Next());

                for (int i = 0; i < without.Length; i++)
                    foreach (float t in new[] { 0f, 0.5f, 1f })
                    {
                        with[i].Pose(t, out Vector3 a, out Vector3 aim);
                        without[i].Pose(t, out Vector3 b, out Vector3 bim);
                        Assert.True(a == b && aim == bim, $"seed {seed}: '{with[i].Name}' rolls another path without the village");
                    }
            }
        }

        [Fact]
        public void TheReelStaysUnderItsLength()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                float with = ChapterIntro.ARRIVAL_SECONDS, without = ChapterIntro.ARRIVAL_SECONDS;
                foreach (IntroShot shot in Shots(seed, out _, out _, out _)) with += shot.Seconds;
                foreach (IntroShot shot in Shots(seed, out _, out _, out _, village: false)) without += shot.Seconds;

                Assert.True(with <= MAX_SECONDS, $"seed {seed}: the reel runs {with:F1} s with the village");
                Assert.True(without <= MAX_SECONDS_WITHOUT_THE_VILLAGE, $"seed {seed}: the reel runs {without:F1} s without it");
            }
        }

        /// <summary>
        /// The lens over the land clears every tree, house and the church wherever the planting stood them: at every pose it
        /// stands <see cref="STANDING_MARGIN"/> over the tallest thing the land stands
        /// (<see cref="VolcanoCountryPlanting.Tallest"/>) above the highest ground within that thing's reach
        /// (<see cref="VolcanoCountryPlanting.Reach"/>) of it, read on rings round the lens.
        /// </summary>
        [Fact]
        public void TheVillageClearsEveryTreeHouseAndTheChurch()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot shot = Shots(seed, out _, out Func<float, float, float> ground, out _)[^1];

                for (int i = 0; i < POSES; i++)
                {
                    shot.Pose(i / (float)(POSES - 1), out Vector3 position, out _);
                    float highest = ground(position.X, position.Z);
                    for (int b = 0; b < RING_BEARINGS; b++)
                        for (int r = 1; r <= RING_STEPS; r++)
                        {
                            float bearing = MathHelper.TwoPi * b / RING_BEARINGS, reach = VolcanoCountryPlanting.Reach * r / RING_STEPS;
                            highest = MathF.Max(highest, ground(position.X + MathF.Cos(bearing) * reach, position.Z + MathF.Sin(bearing) * reach));
                        }

                    float over = position.Y - highest - VolcanoCountryPlanting.Tallest;
                    Assert.True(over >= STANDING_MARGIN,
                        $"seed {seed}: '{shot.Name}' at {i}/{POSES} stands {over:F1} over the tallest thing the land could stand under it");
                }
            }
        }

        /// <summary>
        /// The village is the subject: its middle, about its roofs, in the frame from the first pose to the last and above the
        /// skip hint; and the shot ends with the cone over it, the vent and the column over it in the frame above the village.
        /// </summary>
        [Fact]
        public void TheVillageStaysInFrameAndEndsUnderTheCone()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot shot = Shots(seed, out VolcanoSceneConfig volcano, out Func<float, float, float> ground, out Vector3 vent)[^1];
                Vector2 middle = volcano.Country.Village.ToVector2();
                Vector3 roofs = new(middle.X, ground(middle.X, middle.Y) + VILLAGE_ROOFS, middle.Y);

                for (int i = 0; i < POSES; i++)
                {
                    float t = i / (float)(POSES - 1);
                    Vector2 at = AssertInFrame(shot, t, roofs, $"seed {seed}: the village");
                    Assert.True(at.Y >= OVER_THE_HINT, $"seed {seed}: the village at {at.Y:F2} of '{shot.Name}' at t {t:F2} is down by the skip hint");
                }

                Vector2 village = AssertInFrame(shot, 1f, roofs, $"seed {seed}: the village");
                Vector2 crater = AssertInFrame(shot, 1f, vent, $"seed {seed}: the vent");
                AssertInFrame(shot, 1f, vent + new Vector3(0f, JET_REACH, 0f), $"seed {seed}: the column");
                Assert.True(crater.Y > village.Y, $"seed {seed}: the vent ends at {crater.Y:F2}, under the village at {village.Y:F2}");
            }
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

        /// <summary>
        /// The cut out of the reel into the tour's last leg is held to the same rule (#1127). The leg opens looking down at the
        /// arena's centre from 38 to 48 degrees up, on the gun's side, and the village's last look runs across the arena from that
        /// side at the cone, so the two views can share a heading: before the village's last look was held nearly level the cut
        /// turned as little as 28 degrees. Built as <c>GameplayScreen.TryBeginChapterIntro</c> builds it, the reel and then the
        /// tour on the same generator with the volcano's own viewpoint, with the gun on every side of the arena, and at Low too,
        /// where the crater is the last shot.
        /// </summary>
        [Fact]
        public void TheCutIntoTheToursLegLandsOnAnotherPicture()
        {
            foreach (bool village in new[] { true, false })
                for (int seed = 1; seed <= CUT_ROLLS; seed++)
                    for (int stand = 0; stand < GUN_STANDS; stand++)
                    {
                        var random = new Random(seed);
                        IntroShot[] shots = Shots(random, out _, out _, out Vector3 vent, village);

                        float bearing = MathHelper.PiOver2 + MathHelper.TwoPi * stand / GUN_STANDS;
                        Vector3 gun = PLAY_AIM + new Vector3(MathF.Cos(bearing) * GUN_DISTANCE, GUN_UNDER_AIM, MathF.Sin(bearing) * GUN_DISTANCE);
                        //The tour's stands off VolcanoBackdrop.TryGetViewpoint's (it needs a device to construct)
                        var intro = new ChapterIntro();
                        intro.Begin(PLAY_AIM, GUN_DISTANCE, INTRO_FIELD_OF_VIEW, gun, PLAY_AIM,
                            _ => new SceneViewpoint(vent, 2.4f, 14f, 160f, "the crater"), random, shots);

                        float reel = 0f;
                        foreach (IntroShot shot in shots) reel += shot.Seconds;

                        intro.Pin(reel - CUT_STEP);
                        intro.Update(0f);
                        Vector3 fromPosition = intro.Position, before = Vector3.Normalize(intro.Target - intro.Position);
                        intro.Pin(reel + CUT_STEP);
                        intro.Update(0f);
                        Vector3 toPosition = intro.Position, after = Vector3.Normalize(intro.Target - intro.Position);

                        float angle = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector3.Dot(before, after), -1f, 1f)));
                        float distance = Vector3.Distance(fromPosition, toPosition);

                        Assert.True(angle >= MIN_CUT_ANGLE_DEGREES && distance >= MIN_CUT_DISTANCE,
                            $"seed {seed}, the gun {MathHelper.ToDegrees(bearing):F0} degrees round: '{shots[^1].Name}' -> the tour's leg cuts to a view "
                            + $"{angle:F0} degrees and {distance:F0} units from the last");
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
            shots[3].Start();
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

        //Where the point stands in the frame, in its half-widths and half-heights from its middle (up positive)
        private static Vector2 AssertInFrame(IntroShot shot, float t, Vector3 point, string what)
        {
            shot.Pose(t, out Vector3 position, out Vector3 target);
            Matrix view = Matrix.CreateLookAt(position, target, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(shot.FieldOfView, ASPECT, 1f, 5000f);

            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), view * projection);
            float x = clip.X / clip.W, y = clip.Y / clip.W;

            Assert.True(clip.W > 0f && MathF.Abs(x) <= 1f - FRAME_MARGIN && MathF.Abs(y) <= 1f - FRAME_MARGIN,
                $"{what} at ({x:F2}, {y:F2}) of '{shot.Name}' at t {t:F2}");
            return new Vector2(x, y);
        }
    }
}
