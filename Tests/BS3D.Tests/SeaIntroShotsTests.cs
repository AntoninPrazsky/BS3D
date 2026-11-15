using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The sea's prologue (#1124, the owner: "in the intro the camera does not deal with the oil platforms"), held over the real
    /// builder on the shipped config and the rigs' own boxes (<see cref="OilRigs.Parts"/>, the ones the mesh is built from): the
    /// four shots in their order and the prologue's length, every cut a different picture, every lens over the water (the depths
    /// under it), the run at the rig clear of both rigs' legs, decks, derrick and booms at every point, the rig in frame from
    /// the waterline to the derrick's top through the whole run, and the derrick's top in sight over the deck at its end.
    /// </summary>
    public class SeaIntroShotsTests
    {
        private const int ROLLS = 24;
        private const int POSES = 160;

        //The vertical field of view the game hands the intro (GameplayScreen.GAME_FOV), which each shot widens from, and the
        //narrowest window shape the owner plays at: the projection is Hor+, so a narrower one crops the sides. The scene menu
        //replays the tour from a wider lens (BackdropScreen's 60 degrees), which frames more round the same look.
        private const float INTRO_FIELD_OF_VIEW = MathF.PI / 4.2f;
        private const float ASPECT = 16f / 9f;

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame
        private const float FRAME_MARGIN = 0.05f;

        //The suns the climb heads into: dome 13's (the sea's own, elevation 13 at azimuth 135) and a sweep round the compass
        //at a low and a high elevation, and no sun at all (a rolled heading)
        private static IEnumerable<Vector3?> Suns()
        {
            yield return Sun(13f, 135f);
            for (float azimuth = 0f; azimuth < 360f; azimuth += 30f)
            {
                yield return Sun(6f, azimuth);
                yield return Sun(50f, azimuth);
            }
            yield return null;
        }

        //SkyDome's convention: elevation over the horizon, azimuth from +Z towards +X
        private static Vector3? Sun(float elevation, float azimuth)
        {
            float e = MathHelper.ToRadians(elevation), a = MathHelper.ToRadians(azimuth);
            return new Vector3(MathF.Cos(e) * MathF.Sin(a), MathF.Sin(e), MathF.Cos(e) * MathF.Cos(a));
        }

        private static IntroShot[] Shots(int seed, Vector3? sun, out SeaSceneConfig sea)
        {
            sea = new SeaSceneConfig();
            return SeaIntroShots.Shots(sea, sun, INTRO_FIELD_OF_VIEW, new Random(seed));
        }

        private static IntroShot Rig(IntroShot[] shots) => Array.Find(shots, shot => shot.Name == "the rig");

        [Fact]
        public void TheIslandThenTheDepthsThenTheRigThenTheClimb()
        {
            IntroShot[] shots = Shots(1, Sun(13f, 135f), out _);

            Assert.Equal(new[] { "the island", "the depths", "the rig", "the climb to the sun" }, Array.ConvertAll(shots, shot => shot.Name));
        }

        [Fact]
        public void TheRigCostsTheTourLessThanItsOwnLength()
        {
            //The island, the depths and the climb gave back a second between them, so the prologue grew by 2.6 s, not 3.6
            float seconds = 0f, rig = 0f;
            foreach (IntroShot shot in Shots(1, Sun(13f, 135f), out _))
            {
                seconds += shot.Seconds;
                if (shot.Name == "the rig") rig = shot.Seconds;
            }

            Assert.True(seconds <= 13.4f + 1e-3f, $"the prologue is {seconds:F2} s");
            Assert.True(seconds - rig <= 9.8f + 1e-3f, $"the three shots before the rig came are {seconds - rig:F2} s");
        }

        //Across a cut the lens moves AND the view turns by film's thirty-degree rule, as VolcanoIntroShotsTests holds, unless
        //the cut crosses the water's surface or goes to another place altogether, where no frame of the one can read as the
        //other: the island to the depths is a few tens of units but under the water, and the rig to the climb is the open sea
        //700 units away, where with the sun behind the island the two looks can share a heading.
        private const float MIN_CUT_ANGLE_DEGREES = 30f;
        private const float MIN_CUT_DISTANCE = 100f;
        private const float ANOTHER_PLACE = 500f;

        private static bool ChangesThePicture(Vector3 fromPosition, Vector3 fromTarget, Vector3 toPosition, Vector3 toTarget, float waterY,
            out string why)
        {
            Vector3 before = Vector3.Normalize(fromTarget - fromPosition);
            Vector3 after = Vector3.Normalize(toTarget - toPosition);
            float angle = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector3.Dot(before, after), -1f, 1f)));
            float distance = Vector3.Distance(fromPosition, toPosition);
            bool crosses = (fromPosition.Y < waterY) != (toPosition.Y < waterY);

            why = $"a view {angle:F0} degrees and {distance:F0} units from the last" + (crosses ? ", across the surface" : "");
            return crosses || distance >= ANOTHER_PLACE || (angle >= MIN_CUT_ANGLE_DEGREES && distance >= MIN_CUT_DISTANCE);
        }

        [Fact]
        public void NoCutLandsOnTheSamePicture()
        {
            foreach (Vector3? sun in Suns())
                for (int seed = 1; seed <= ROLLS; seed++)
                {
                    IntroShot[] shots = Shots(seed, sun, out SeaSceneConfig sea);

                    for (int i = 0; i + 1 < shots.Length; i++)
                    {
                        shots[i].Pose(1f, out Vector3 fromPosition, out Vector3 fromTarget);
                        shots[i + 1].Pose(0f, out Vector3 toPosition, out Vector3 toTarget);

                        Assert.True(ChangesThePicture(fromPosition, fromTarget, toPosition, toTarget, sea.LevelY, out string why),
                            $"sun {sun}, seed {seed}: '{shots[i].Name}' -> '{shots[i + 1].Name}' cuts to {why}");
                    }
                }
        }

        [Fact]
        public void TheCutRuleRefusesTheSwellThatWasReadAsAGlitch()
        {
            //Seen to fire (BestPractices.md, section 10) on the pair the owner read as a jerk (#759): the swell and the climb ran
            //one heading into the sun at the lens's floor, 3 degrees down, and the cut jumped back along the line and to one side
            var sea = new SeaSceneConfig();
            Vector3 heading = Vector3.Normalize(new Vector3(0.71f, 0f, -0.71f)), across = Vector3.Cross(heading, Vector3.Up);
            Vector3 look = Vector3.Normalize(heading - Vector3.UnitY * MathF.Tan(MathHelper.ToRadians(3f)));
            Vector3 swellEnd = new Vector3(40f, sea.LevelY + 4.18f, -40f), climbStart = swellEnd - heading * 50f + across * 30f;

            Assert.False(ChangesThePicture(swellEnd, swellEnd + look * 10f, climbStart, climbStart + look * 10f, sea.LevelY, out _));
        }

        [Fact]
        public void EveryLensIsOverTheWaterOrUnderEveryTrough()
        {
            foreach (Vector3? sun in Suns())
                for (int seed = 1; seed <= ROLLS; seed++)
                    foreach (IntroShot shot in Shots(seed, sun, out SeaSceneConfig sea))
                    {
                        //The highest any crest stands over the mean level (SeaIntroShots' own bound, CREST_REACH)
                        float crest = SeaIntroShots.CrestHeight(sea);
                        for (int i = 0; i < POSES; i++)
                        {
                            shot.Pose(i / (float)(POSES - 1), out Vector3 lens, out _);
                            string at = $"sun {sun}, seed {seed}: '{shot.Name}' at {i}/{POSES} stands {lens.Y - sea.LevelY:F2} over the mean level";

                            if (shot.Name == "the depths") Assert.True(lens.Y < sea.LevelY - crest - 10f, at);
                            else Assert.True(lens.Y >= sea.LevelY + crest + 1f, at);
                        }
                    }
        }

        [Fact]
        public void TheRunAtTheRigStaysClearOfBothRigs()
        {
            var sea = new SeaSceneConfig();
            var boxes = new List<(Matrix World, Vector3 Half)>();
            void Collect(Matrix world, Vector3 half, Vector3 color, float kind) => boxes.Add((world, half));
            OilRigs.Parts(OilRigLayout.Near(sea.LevelY), Collect);
            OilRigs.Parts(OilRigLayout.Far(sea.LevelY), Collect);

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot rig = Rig(Shots(seed, Sun(13f, 135f), out _));
                for (int i = 0; i < POSES; i++)
                {
                    rig.Pose(i / (float)(POSES - 1), out Vector3 lens, out _);
                    foreach ((Matrix world, Vector3 half) in boxes)
                    {
                        float distance = DistanceToBox(lens, world, half);
                        Assert.True(distance >= MIN_RIG_CLEARANCE, $"seed {seed}: the rig's lens at {i}/{POSES} is {distance:F1} from a box of a rig");
                    }
                }
            }
        }

        //How near any box of either rig the lens may come: a leg is 8.8 wide and the run passes at sixty units a second
        private const float MIN_RIG_CLEARANCE = 20f;

        [Fact]
        public void TheRigIsInFrameFromTheWaterlineToTheDerricksTop()
        {
            var sea = new SeaSceneConfig();
            OilRig near = OilRigLayout.Near(sea.LevelY);
            Vector3 deck = near.Foot + Vector3.Up * OilRigLayout.DECK_Y;

            foreach (float fieldOfView in new[] { INTRO_FIELD_OF_VIEW, MathF.PI / 3f })
                for (int seed = 1; seed <= ROLLS; seed++)
                {
                    IntroShot rig = Rig(SeaIntroShots.Shots(sea, Sun(13f, 135f), fieldOfView, new Random(seed)));
                    for (int i = 0; i < POSES; i++)
                    {
                        float t = i / (float)(POSES - 1);
                        string what = $"field of view {MathHelper.ToDegrees(fieldOfView):F0}, seed {seed}";
                        AssertInFrame(rig, t, near.Foot, what + ": the jacket at the waterline");
                        AssertInFrame(rig, t, deck, what + ": the deck");
                        AssertInFrame(rig, t, near.DerrickTop, what + ": the derrick's top");
                    }
                }
        }

        [Fact]
        public void TheRigGrowsFromTheHorizonUntilItTowersOverTheLens()
        {
            //From the waterline at its jacket to its derrick's top, at the game's lens: under half the frame's height as the run
            //starts, about two fifths, and over two thirds at its end, about three quarters
            var sea = new SeaSceneConfig();
            OilRig near = OilRigLayout.Near(sea.LevelY);

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot rig = Rig(Shots(seed, Sun(13f, 135f), out _));
                float first = FrameHeightShare(rig, 0f, near.Foot, near.DerrickTop), last = FrameHeightShare(rig, 1f, near.Foot, near.DerrickTop);

                Assert.True(first < 0.5f, $"seed {seed}: the rig stands {first:P0} of the frame's height as the run starts");
                Assert.True(last > 2f / 3f, $"seed {seed}: the rig stands {last:P0} of the frame's height at the run's end");
            }
        }

        [Fact]
        public void TheDerricksUpperPartStandsOverTheDeckAtTheEnd()
        {
            //The deck and the modules on it hide the derrick's foot from the waterline, and all of it from under the deck's edge,
            //where the run first ended; and the flare's boom, which points out over the water, hid it from a lens on its line.
            //Over the last fifth of the run the derrick from DERRICK_SEEN of the way up to its top is in sight.
            var sea = new SeaSceneConfig();
            OilRig near = OilRigLayout.Near(sea.LevelY);
            var occluders = new List<(Matrix World, Vector3 Half)>();
            Vector3 axis = near.ToWorld(new Vector3(OilRigLayout.DERRICK_AXIS.X, 0f, OilRigLayout.DERRICK_AXIS.Y));
            OilRigs.Parts(near, (world, half, color, kind) =>
            {
                //The derrick's own boxes stand on its axis over its foot and are what is being looked at
                Vector3 centre = world.Translation;
                if (new Vector2(centre.X - axis.X, centre.Z - axis.Z).Length() > 0.5f || centre.Y < near.DerrickFoot.Y) occluders.Add((world, half));
            });

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot rig = Rig(Shots(seed, Sun(13f, 135f), out _));
                for (int i = (int)(POSES * 0.8f); i < POSES; i++)
                {
                    rig.Pose(i / (float)(POSES - 1), out Vector3 lens, out _);
                    for (int k = 0; k <= 10; k++)
                    {
                        float share = MathHelper.Lerp(DERRICK_SEEN, 1f, k / 10f);
                        Assert.True(Seen(lens, Vector3.Lerp(near.DerrickFoot, near.DerrickTop, share), occluders),
                            $"seed {seed}: at {i}/{POSES} the derrick {share:P0} of the way up is hidden from the lens at {lens}");
                    }
                }
            }
        }

        //From how far up its height the derrick must be in sight at the end: about the upper half is, from every run
        private const float DERRICK_SEEN = 0.6f;

        //The share of the frame's height between two points at the shot's lens at t, on a 16:9 frame
        private static float FrameHeightShare(IntroShot shot, float t, Vector3 low, Vector3 high)
        {
            shot.Pose(t, out Vector3 position, out Vector3 target);
            Matrix viewProjection = Matrix.CreateLookAt(position, target, Vector3.Up)
                * Matrix.CreatePerspectiveFieldOfView(shot.FieldOfViewAt(t), ASPECT, 1f, 5000f);
            Vector4 a = Vector4.Transform(new Vector4(low, 1f), viewProjection), b = Vector4.Transform(new Vector4(high, 1f), viewProjection);
            return MathF.Abs(b.Y / b.W - a.Y / a.W) * 0.5f;
        }

        private static bool Seen(Vector3 lens, Vector3 point, List<(Matrix World, Vector3 Half)> occluders)
        {
            foreach ((Matrix world, Vector3 half) in occluders)
                if (SegmentHitsBox(lens, point, world, half)) return false;

            return true;
        }

        //The nearest a point comes to a box that is `half` either way along `world`'s own axes about its origin
        private static float DistanceToBox(Vector3 point, Matrix world, Vector3 half)
        {
            Vector3 local = Vector3.Transform(point, Matrix.Invert(world));
            Vector3 closest = Vector3.Clamp(local, -half, half);
            return Vector3.Distance(point, Vector3.Transform(closest, world));
        }

        //Whether the segment from a to b passes through such a box (slabs in the box's own frame, where the segment stays straight)
        private static bool SegmentHitsBox(Vector3 a, Vector3 b, Matrix world, Vector3 half)
        {
            Matrix inverse = Matrix.Invert(world);
            Vector3 p = Vector3.Transform(a, inverse), q = Vector3.Transform(b, inverse), d = q - p;
            float enter = 0f, leave = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float start = Component(p, axis), step = Component(d, axis), h = Component(half, axis);
                if (MathF.Abs(step) < 1e-9f)
                {
                    if (start < -h || start > h) return false;
                    continue;
                }

                float t0 = (-h - start) / step, t1 = (h - start) / step;
                if (t0 > t1) (t0, t1) = (t1, t0);
                enter = MathF.Max(enter, t0);
                leave = MathF.Min(leave, t1);
                if (enter > leave) return false;
            }

            return true;
        }

        private static float Component(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        private static void AssertInFrame(IntroShot shot, float t, Vector3 point, string what)
        {
            shot.Pose(t, out Vector3 position, out Vector3 target);
            Matrix view = Matrix.CreateLookAt(position, target, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(shot.FieldOfViewAt(t), ASPECT, 1f, 5000f);

            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), view * projection);
            float x = clip.X / clip.W, y = clip.Y / clip.W;

            Assert.True(clip.W > 0f && MathF.Abs(x) <= 1f - FRAME_MARGIN && MathF.Abs(y) <= 1f - FRAME_MARGIN,
                $"{what} at ({x:F2}, {y:F2}) of '{shot.Name}' at t {t:F2}");
        }
    }
}
