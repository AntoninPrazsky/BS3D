using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The desert's chapter intro as a reel (#1110, the owner: "give them more time in the intro", the beetles and the
    /// scorpions): the erg, the pyramids, the beetle, the crest and the scorpion home that lands on the pose, no longer than a
    /// cap; and the two close shots, each holding its creature in frame for as long as it walks (the beetle from the shot's
    /// first frame until it digs in, the scorpion until its walk ends, where it once cut away mid-step), cued to the life the
    /// look is laid on. The landing itself is <see cref="DesertLandingTests"/>; the rides, <see cref="IntroShotSmoothnessTests"/>.
    /// </summary>
    public class DesertIntroShotsTests
    {
        private const float FieldOfView = DesertLandingTests.FieldOfView;
        private static readonly Vector3 GamePosition = DesertLandingTests.GamePosition, GameTarget = DesertLandingTests.GameTarget;
        private static readonly DesertSceneConfig Desert = new();

        //The whole opening, landing or not, at most this long: the reel it replaced and its tour leg ran 23.2 s
        private const float MAX_INTRO_SECONDS = 24.5f;

        //A creature in frame: its foot and its back (this high over it) inside a 16:9 frame, this share of the half width and
        //half height short of the edges, at the shot's own lens
        private const float BEETLE_BACK = 0.25f, SCORPION_BACK = DesertLanding.CRITTER_HEIGHT;
        private const float FRAME_MARGIN = 0.1f;

        private const int ROLLS = 24;

        private readonly ITestOutputHelper _output;

        public DesertIntroShotsTests(ITestOutputHelper output) => _output = output;

        private sealed class Cue
        {
            public Vector2 At;
            public float Yaw, Onset = float.NaN;
            public bool Scorpion;
            public void Take(Vector2 at, float yaw, float onset, bool scorpion) => (At, Yaw, Onset, Scorpion) = (at, yaw, onset, scorpion);
        }

        [Fact]
        public void The_reel_is_the_erg_the_pyramids_the_beetle_the_crest_and_the_scorpion_home()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                string[] names = DesertIntroShots.Build(Desert, null, GamePosition, GameTarget, FieldOfView, new Random(seed)).Select(s => s.Name).ToArray();
                Assert.Equal(new[] { "the erg", "the pyramids", "the beetle", "the crest", DesertLanding.NAME }, names);
            }

            //Where no flight home is clear, the scorpion is the last shot and does not land
            IntroShot[] reel = DesertIntroShots.Build(Desert, null, new Vector3(0f, -2f, 15f), GameTarget, FieldOfView, new Random(1));
            Assert.Equal(new[] { "the erg", "the pyramids", "the beetle", "the crest", "the scorpion" }, reel.Select(s => s.Name).ToArray());
            Assert.False(reel[^1].Lands);
        }

        [Fact]
        public void The_desert_intro_is_no_longer_than_its_cap()
        {
            //The whole opening at Rope's pose, every roll (the owner prefers faster over slower): the landing replaces the
            //tour's 4.5 s leg. And where no flight home is clear, the reel and the tour's leg after it
            float shortest = float.MaxValue, longest = 0f;
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                float seconds = DesertIntroShots.Build(Desert, null, GamePosition, GameTarget, FieldOfView, new Random(seed)).Sum(s => s.Seconds);
                Assert.True(seconds <= MAX_INTRO_SECONDS, $"roll {seed}: {seconds:F1} s");
                shortest = MathF.Min(shortest, seconds);
                longest = MathF.Max(longest, seconds);
            }

            float fallback = ChapterIntro.ARRIVAL_SECONDS
                + DesertIntroShots.Build(Desert, null, new Vector3(0f, -2f, 15f), GameTarget, FieldOfView, new Random(1)).Sum(s => s.Seconds);
            Assert.True(fallback <= MAX_INTRO_SECONDS, $"the reel that does not land: {fallback:F1} s");
            _output.WriteLine(FormattableString.Invariant($"{shortest:F1}-{longest:F1} s at Rope's pose; {fallback:F1} s where no flight home is clear"));
        }

        [Fact]
        public void The_beetle_is_walking_on_the_shots_first_frame_and_digs_back_in_before_the_cut()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var cue = new Cue();
                IntroShot beetle = DesertIntroShots.Beetle(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                Assert.True(float.IsNaN(cue.Onset), "cued before the shot came on screen");
                beetle.Start();

                //A beetle, cued in the past: up out of the sand and walking already, its trail behind it
                Assert.False(cue.Scorpion);
                Assert.Equal(-(SceneRenderer.DESERT_FEATURE_EMERGE_SECONDS + DesertIntroShots.BEETLE_WALKED), cue.Onset, 4);

                //Its walk ends inside the shot, and most of its dig is done by the cut
                float walkEnds = SceneRenderer.DesertFeatureWalkEnds(scorpion: false) + cue.Onset;
                float dug = (beetle.Seconds - walkEnds) / (SceneRenderer.DesertFeatureSeconds(scorpion: false) - SceneRenderer.DesertFeatureWalkEnds(scorpion: false));
                Assert.InRange(walkEnds, 1f, beetle.Seconds - 1f);
                Assert.True(dug >= 0.75f, $"roll {seed}: {dug:P0} dug in at the cut");
            }
        }

        [Fact]
        public void The_beetle_shot_holds_its_beetle_in_frame_from_its_first_frame_until_it_has_dug_in()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var cue = new Cue();
                IntroShot beetle = DesertIntroShots.Beetle(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                beetle.Start();

                float under = SceneRenderer.DesertFeatureWalkEnds(scorpion: false) + cue.Onset + 0.8f;
                for (float seconds = 0f; seconds <= MathF.Min(under, beetle.Seconds); seconds += 0.05f)
                {
                    Vector3 foot = SceneRenderer.DesertFeatureAt(cue.At, cue.Yaw, seconds - cue.Onset, Desert, scorpion: false);
                    InFrame(beetle, seconds, foot, BEETLE_BACK, $"roll {seed}: the beetle");
                }

                //And the lens keeps beside it, never over it
                for (float seconds = 0f; seconds <= beetle.Seconds; seconds += 0.05f)
                {
                    beetle.Pose(seconds / beetle.Seconds, out Vector3 lens, out _);
                    Vector3 foot = SceneRenderer.DesertFeatureAt(cue.At, cue.Yaw, seconds - cue.Onset, Desert, scorpion: false);
                    float off = new Vector2(lens.X - foot.X, lens.Z - foot.Z).Length();
                    Assert.InRange(off, DesertIntroShots.BEETLE_SIDE - 0.5f, DesertIntroShots.BEETLE_SIDE + 1.5f);
                }
            }
        }

        [Fact]
        public void The_scorpion_shot_holds_its_scorpion_in_frame_through_its_walk()
        {
            //Where no flight home is clear (#1110: it cut away mid-step), and the scorpion home until it takes off
            float walkEnds = DesertIntroShots.SCORPION_ONSET + SceneRenderer.DesertFeatureWalkEnds();
            Assert.True(DesertIntroShots.ScorpionSeconds >= walkEnds, "the scorpion shot ends before its walk does");
            Assert.True(DesertLanding.Lift >= DesertLanding.ONSET + SceneRenderer.DesertFeatureWalkEnds(), "the scorpion home lifts off before its walk ends");

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var cue = new Cue();
                IntroShot scorpion = DesertIntroShots.Scorpion(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                scorpion.Start();
                Assert.True(cue.Scorpion);

                for (float seconds = 0f; seconds <= walkEnds; seconds += 0.05f)
                {
                    Vector3 foot = SceneRenderer.DesertFeatureAt(cue.At, cue.Yaw, seconds - cue.Onset, Desert);
                    InFrame(scorpion, seconds, foot, SCORPION_BACK, $"roll {seed}: the scorpion");
                }
            }
        }

        /// <summary>
        /// The looks that follow a creature turn like a drone's gimbal, never by a step (#1110): the view's turn, yaw and pitch
        /// together, changes its rate at most <see cref="GIMBAL_TURN"/> through the beetle and the scorpion shots and in the
        /// scorpion home until its look lets go of the scorpion (a lens all but standing, every turn of the view is the
        /// gimbal's), and at most <see cref="FLIGHT_TURN"/> through the take-off after. A creature setting off at its walk's
        /// pace in one frame turned the look at 145 to 217 degrees a second squared, one stopping dead at 194 to 240, and a
        /// look handed from the scorpion to its direction in one frame at 740.
        /// </summary>
        [Fact]
        public void The_looks_on_the_creatures_turn_like_a_gimbal()
        {
            float beetle = 0f, scorpion = 0f, held = 0f, flight = 0f;
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                beetle = MathF.Max(beetle, TurnAcceleration(DesertIntroShots.Beetle(null, Desert, Ground, FieldOfView, new Random(seed)), 0f, float.MaxValue));
                scorpion = MathF.Max(scorpion, TurnAcceleration(DesertIntroShots.Scorpion(null, Desert, Ground, FieldOfView, new Random(seed)), 0f, float.MaxValue));
                IntroShot home = DesertLanding.Build(Desert, null, GamePosition, GameTarget, FieldOfView, new Random(seed), out _);
                held = MathF.Max(held, TurnAcceleration(home, 0f, DesertLanding.LetGo));
                flight = MathF.Max(flight, TurnAcceleration(home, DesertLanding.LetGo, float.MaxValue));
            }

            _output.WriteLine(FormattableString.Invariant(
                $"the view's turn changes at most {beetle:F0} deg/s2 (the beetle), {scorpion:F0} (the scorpion), {held:F0} (the scorpion home until it lets go), {flight:F0} (its take-off)"));
            Assert.True(beetle <= GIMBAL_TURN, $"the beetle's look: {beetle:F0} deg/s2");
            Assert.True(scorpion <= GIMBAL_TURN, $"the scorpion's look: {scorpion:F0} deg/s2");
            Assert.True(held <= GIMBAL_TURN, $"the scorpion home's look on the scorpion: {held:F0} deg/s2");
            Assert.True(flight <= FLIGHT_TURN, $"the scorpion home's take-off: {flight:F0} deg/s2");
        }

        //The view's turn changes its rate at most this much, degrees a second squared: a gimbal's tilt reached in no less than
        //half a second (IntroShotSmoothnessTests' pitch limits, 20 deg/s in 0.5 s), and a drone's yaw reached in no less than a
        //third (its 50 deg/s)
        private const float GIMBAL_TURN = 40f, FLIGHT_TURN = 150f;

        //The most the view's turn (its angular velocity, yaw and pitch together) changes in a second, between two seconds of the
        //shot on the wall clock, sampled at 60 frames a second, three frames clear of either end
        private static float TurnAcceleration(IntroShot shot, float from, float to)
        {
            int frames = (int)(shot.Seconds * 60f);
            float dt = shot.Seconds / (frames - 1), worst = 0f;
            var look = new Vector3[frames];
            for (int i = 0; i < frames; i++)
            {
                shot.Pose(i / (float)(frames - 1), out Vector3 lens, out Vector3 at);
                look[i] = Vector3.Normalize(at - lens);
            }

            for (int i = 4; i < frames - 4; i++)
            {
                if (i * dt < from || i * dt >= to) continue;
                Vector3 before = Vector3.Cross(look[i - 1], look[i]) / dt, after = Vector3.Cross(look[i], look[i + 1]) / dt;
                worst = MathF.Max(worst, MathHelper.ToDegrees((after - before).Length()) / dt);
            }
            return worst;
        }

        private static float Ground(float x, float z) => TerrainMirror.Desert(x, z, Desert);

        private static void InFrame(IntroShot shot, float seconds, Vector3 foot, float back, string what)
        {
            float t = seconds / shot.Seconds;
            shot.Pose(t, out Vector3 lens, out Vector3 at);
            foreach (Vector3 point in new[] { foot, foot + Vector3.Up * back })
            {
                Vector2 screen = DesertLandingTests.Screen(lens, at, point, shot.FieldOfViewAt(t), 16f / 9f);
                Assert.True(MathF.Abs(screen.X) <= 1f - FRAME_MARGIN && MathF.Abs(screen.Y) <= 1f - FRAME_MARGIN,
                    $"{what} at ({screen.X:F2}, {screen.Y:F2}) of the frame at {seconds:F2} s");
            }
        }
    }
}
