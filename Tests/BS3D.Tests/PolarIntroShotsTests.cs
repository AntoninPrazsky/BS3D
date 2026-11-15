using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The icesheet's prologue and its research camp (#1128). The owner: "the intro animation does not deal with the
    /// buildings/camp". Held over the real builder on the shipped config and the ground's mirror: the camp is a shot of its
    /// own after the front, an arc that holds the station's modules, the mast and its flag and the vehicle in the frame over
    /// the skip hint from its first frame to its last, at every window shape; every cut, the one out of the reel into the
    /// tour's last leg too, lands on another picture; no lens of the reel comes near the camp but that arc, which keeps off
    /// it too; and the reel is no longer than it was by more than the camp's own shot.
    /// </summary>
    public class PolarIntroShotsTests
    {
        private readonly ITestOutputHelper _output;

        public PolarIntroShotsTests(ITestOutputHelper output) => _output = output;

        private const int ROLLS = 32;
        private const float FRAME = 1f / 60f;

        //The vertical field of view the game hands the intro (GameplayScreen.GAME_FOV), which each shot widens from, and the
        //window shapes it is played at: the projection is Hor+, so a narrower one crops the sides
        private const float INTRO_FIELD_OF_VIEW = MathF.PI / 4.2f;
        private static readonly float[] ASPECTS = { 16f / 9f, 2.4f, 1.6f, 4f / 3f };

        //The prologue and the tour's last leg together: 14.1 s before the camp's shot came in; the other three gave up 0.8 s to
        //it, the owner preferring a faster reel to a longer one
        private const float MAX_OPENING_SECONDS = 16.2f;

        //Across a cut the lens must move or the view turn (the cities' figures, CityIntroShotsTests)
        private const float MIN_CUT_ANGLE_DEGREES = 22f, MIN_CUT_DISTANCE = 40f;

        //The cut out of the reel into the tour's last leg, read this far either side of it, with the gun on GUN_STANDS sides of
        //the arena, the play camera GUN_DISTANCE off the aim it looks at and GUN_UNDER_AIM under it (VolcanoIntroShotsTests)
        private const int GUN_STANDS = 4;
        private const float GUN_DISTANCE = 30f, GUN_UNDER_AIM = -5.8f, CUT_STEP = 1e-3f;
        private static readonly Vector3 PLAY_AIM = new(0f, -1.6f, 0f);

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame; and the skip
        //hint's band along the bottom edge (up to 12.2 % of the frame's height, "Chapter intro prologues" in the docs)
        private const float FRAME_MARGIN = 0.05f;
        private const float SKIP_HINT_SHARE = 0.13f;

        //What the camp's shot frames (PolarPlanting.Landmarks): the station, its mast and flag, and the vehicle
        private static readonly string[] FRAMED = { "module 1", "module 2", "module 3", "station's end", "station's other end", "mast", "flag", "vehicle" };

        //The camp's landmarks at least this tall in the frame at the camp shot's end, a share of its height: the mast and its
        //flag over the station, a close look and not a far one (at 105 out it was 0.3)
        private const float MIN_MAST_SHARE = 0.35f;

        //Every lens of the reel this far over the ice under it at the least
        private const float MIN_CLEARANCE = 4f;

        private static readonly PolarSceneConfig Polar = new();

        private static float Ground(float x, float z) => TerrainMirror.Polar(x, z, Polar);

        private static PolarPlanting.Landmark[] Camp => PolarPlanting.Landmarks(Polar.CampCentre, Polar.CampTurn, Ground);

        private static IntroShot[] Reel(int seed) => PolarIntroShots.Build(Polar, INTRO_FIELD_OF_VIEW, new Random(seed));

        private static IEnumerable<(Vector3 Lens, Vector3 Target, float Fov)> Frames(IntroShot shot)
        {
            int frames = (int)(shot.Seconds / FRAME);
            for (int i = 0; i < frames; i++)
            {
                float t = i / (float)(frames - 1);
                shot.Pose(t, out Vector3 lens, out Vector3 target);
                yield return (lens, target, shot.FieldOfViewAt(t));
            }
        }

        private static Vector3 Project(Vector3 point, Matrix viewProjection, out bool ahead)
        {
            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
            ahead = clip.W > 0.1f;
            return new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
        }

        [Fact]
        public void TheIcesheetOpensOnTheSheetTheCrevassesTheFrontThenTheCampAndNoLonger()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot[] shots = Reel(seed);
                Assert.Equal(new[] { "the icesheet", "the crevasses", "the pressure ridge", "the camp" }, shots.Select(s => s.Name).ToArray());

                float seconds = shots.Sum(s => s.Seconds) + ChapterIntro.ARRIVAL_SECONDS;
                Assert.True(seconds <= MAX_OPENING_SECONDS, $"seed {seed}: the opening runs {seconds:F1} s");
            }
        }

        [Fact]
        public void EveryCutLandsOnAnotherPicture()
        {
            float least = float.MaxValue;
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot[] shots = Reel(seed);
                for (int i = 0; i + 1 < shots.Length; i++)
                {
                    shots[i].Pose(1f, out Vector3 fromLens, out Vector3 fromTarget);
                    shots[i + 1].Pose(0f, out Vector3 toLens, out Vector3 toTarget);

                    float angle = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(
                        Vector3.Dot(Vector3.Normalize(fromTarget - fromLens), Vector3.Normalize(toTarget - toLens)), -1f, 1f)));
                    float distance = Vector3.Distance(fromLens, toLens);
                    Assert.True(angle >= MIN_CUT_ANGLE_DEGREES || distance >= MIN_CUT_DISTANCE,
                        $"seed {seed}: the cut from '{shots[i].Name}' to '{shots[i + 1].Name}' turns {angle:F0} degrees and moves {distance:F0} units");
                    least = MathF.Min(least, distance);
                }
            }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "the lens moves {0:F0} units at the least across a cut", least));
        }

        /// <summary>
        /// The cut out of the camp into the tour's last leg is a cut too: built as <c>GameplayScreen.TryBeginChapterIntro</c>
        /// builds it, the reel and then the tour on the same generator with the icesheet's own viewpoint (the pressure ridge,
        /// <c>PolarBackdrop.TryGetViewpoint</c>), with the gun on every side of the arena.
        /// </summary>
        [Fact]
        public void TheCutIntoTheToursLegLandsOnAnotherPicture()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
                for (int stand = 0; stand < GUN_STANDS; stand++)
                {
                    var random = new Random(seed);
                    IntroShot[] shots = PolarIntroShots.Build(Polar, INTRO_FIELD_OF_VIEW, random);

                    float bearing = MathHelper.PiOver2 + MathHelper.TwoPi * stand / GUN_STANDS;
                    Vector3 gun = PLAY_AIM + new Vector3(MathF.Cos(bearing) * GUN_DISTANCE, GUN_UNDER_AIM, MathF.Sin(bearing) * GUN_DISTANCE);
                    var intro = new ChapterIntro();
                    intro.Begin(PLAY_AIM, GUN_DISTANCE, INTRO_FIELD_OF_VIEW, gun, PLAY_AIM,
                        b => new SceneViewpoint(SceneRenderer.AtBearing(b, Polar.RidgeRadius, Polar.LevelY + Polar.RidgeHeight * 0.6f),
                            2.3f, 7f, 0f, "the pressure ridge"), random, shots);

                    float reel = shots.Sum(s => s.Seconds);
                    intro.Pin(reel - CUT_STEP);
                    intro.Update(0f);
                    Vector3 fromLens = intro.Position, before = Vector3.Normalize(intro.Target - intro.Position);
                    intro.Pin(reel + CUT_STEP);
                    intro.Update(0f);
                    Vector3 toLens = intro.Position, after = Vector3.Normalize(intro.Target - intro.Position);

                    float angle = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector3.Dot(before, after), -1f, 1f)));
                    float distance = Vector3.Distance(fromLens, toLens);
                    Assert.True(angle >= MIN_CUT_ANGLE_DEGREES || distance >= MIN_CUT_DISTANCE,
                        $"seed {seed}, the gun {MathHelper.ToDegrees(bearing):F0} degrees round: 'the camp' -> the tour's leg turns {angle:F0} degrees and moves {distance:F0} units");
                }
        }

        /// <summary>
        /// The camp's shot frames the camp from its first frame to its last, at every window shape: each module, the station's
        /// two ends, the mast, the flag and the vehicle, their tops and their middles, inside the frame and over the skip hint;
        /// and it ends close enough that the mast stands <see cref="MIN_MAST_SHARE"/> of the frame tall.
        /// </summary>
        [Fact]
        public void TheCampShotFramesTheModulesTheMastAndTheVehicle()
        {
            PolarPlanting.Landmark[] camp = Camp.Where(mark => FRAMED.Contains(mark.Name)).ToArray();
            Assert.Equal(FRAMED.Length, camp.Length);
            float leastMast = float.MaxValue, widest = 0f;

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot shot = Reel(seed).Single(s => s.Name == "the camp");
                List<(Vector3 Lens, Vector3 Target, float Fov)> frames = Frames(shot).ToList();

                foreach (float aspect in ASPECTS)
                    for (int i = 0; i < frames.Count; i += 4)
                    {
                        (Vector3 lens, Vector3 target, float fov) = frames[i];
                        Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);

                        foreach (PolarPlanting.Landmark mark in camp)
                            foreach (Vector3 point in new[] { mark.Top, Vector3.Lerp(mark.Foot, mark.Top, 0.5f) })
                            {
                                Vector3 ndc = Project(point, viewProjection, out bool ahead);
                                Assert.True(ahead && MathF.Abs(ndc.X) <= 1f - FRAME_MARGIN && ndc.Y <= 1f - FRAME_MARGIN && ndc.Y >= -1f + 2f * SKIP_HINT_SHARE,
                                    $"seed {seed}, frame {i} at {aspect:F2}: the camp's {mark.Name} is out of the frame or by the skip hint at {ndc.X:F2}, {ndc.Y:F2}");
                                widest = MathF.Max(widest, MathF.Abs(ndc.X));
                            }
                    }

                (Vector3 endLens, Vector3 endTarget, float endFov) = frames[^1];
                Matrix end = Matrix.CreateLookAt(endLens, endTarget, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(endFov, 16f / 9f, 0.1f, 5000f);
                PolarPlanting.Landmark mast = camp.Single(mark => mark.Name == "mast");
                float share = (Project(mast.Top, end, out _).Y - Project(mast.Foot, end, out _).Y) * 0.5f;
                Assert.True(share >= MIN_MAST_SHARE, $"seed {seed}: the shot ends with the mast {share:P0} of the frame tall, not a close look");
                leastMast = MathF.Min(leastMast, share);
            }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "the mast ends {0:P0} of the frame tall at the least; the camp reaches {1:F2} of the half-frame", leastMast, widest));
        }

        /// <summary>
        /// No lens of the reel comes near the camp: every one keeps <see cref="PolarIntroShots.CAMP_AIR"/> off the camp's reach
        /// on the ground (the crevasse pass and the front's truck fly five to seven units over the ice, the mast stands 27.5),
        /// and every one keeps <see cref="MIN_CLEARANCE"/> over the ice under it.
        /// </summary>
        [Fact]
        public void EveryLensKeepsOffTheCampAndOverTheIce()
        {
            Vector2 centre = Polar.CampCentre;
            float nearest = float.MaxValue;
            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (IntroShot shot in Reel(seed))
                {
                    int frame = 0;
                    foreach ((Vector3 lens, _, _) in Frames(shot))
                    {
                        float off = Vector2.Distance(new Vector2(lens.X, lens.Z), centre);
                        Assert.True(off >= PolarPlanting.Reach + PolarIntroShots.CAMP_AIR - 0.01f,
                            $"seed {seed}, '{shot.Name}' frame {frame}: the lens is {off:F1} from the camp's middle, inside its reach {PolarPlanting.Reach:F1}");
                        nearest = MathF.Min(nearest, off - PolarPlanting.Reach);

                        float over = lens.Y - Ground(lens.X, lens.Z);
                        Assert.True(over >= MIN_CLEARANCE, $"seed {seed}, '{shot.Name}' frame {frame}: the lens is {over:F2} over the ice");
                        frame++;
                    }
                }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "the nearest lens stands {0:F1} past the camp's reach", nearest));
        }
    }
}
