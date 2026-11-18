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
        public void The_beetle_is_walking_on_the_shots_first_frame_and_has_dug_back_in_as_the_shot_cuts()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var cue = new Cue();
                IntroShot beetle = DesertIntroShots.Beetle(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                Assert.True(float.IsNaN(cue.Onset), "cued before the shot came on screen");
                beetle.Track(0f);

                //A beetle, cued in the past: up out of the sand and walking already, its trail behind it
                Assert.False(cue.Scorpion);
                Assert.Equal(-(SceneRenderer.DESERT_FEATURE_EMERGE_SECONDS + DesertIntroShots.BEETLE_WALKED), cue.Onset, 4);

                //Its walk ends inside the shot, and its dig is done as the shot cuts: its life (and with it its hole and its
                //prints) ends no earlier than the cut, or they would go on screen, and within a frame of it
                float walkEnds = SceneRenderer.DesertFeatureWalkEnds(scorpion: false) + cue.Onset;
                float lifeEnds = SceneRenderer.DesertFeatureSeconds(scorpion: false) + cue.Onset;
                Assert.InRange(walkEnds, 1f, beetle.Seconds - 1f);
                Assert.True(lifeEnds >= beetle.Seconds - 1e-4f, $"roll {seed}: gone {beetle.Seconds - lifeEnds:F2} s before the cut");
                Assert.True(lifeEnds <= beetle.Seconds + 1f / 60f, $"roll {seed}: still digging {lifeEnds - beetle.Seconds:F2} s after the cut");
            }
        }

        /// <summary>
        /// A pause or a hitch in either close shot leaves its creature under the look (#1110): the scene draws the creature on
        /// the wall clock, which goes on through a pause and past a hitch's capped step, and the look is laid on the shot's own
        /// seconds, so the shot re-cues it on every frame (<see cref="IntroShot.Track"/>). Cued once on its first frame, a pause
        /// left the look on sand the creature had left and the beetle gone under before the cut.
        /// </summary>
        [Fact]
        public void A_pause_or_a_hitch_leaves_each_creature_under_its_look()
        {
            float worst = 0f;
            for (int seed = 1; seed <= 6; seed++)
            {
                //The wall clock the scene draws on, the creatures' onsets on it (the cue's are counted from the call)
                float wall = 100f;
                var cued = new[] { new Cue(), new Cue() };
                IntroShot[] reel = DesertIntroShots.Build(Desert, (at, yaw, onset, scorpion) => cued[scorpion ? 1 : 0].Take(at, yaw, wall + onset, scorpion),
                    GamePosition, GameTarget, FieldOfView, new Random(seed));
                Assert.Equal(DesertLanding.NAME, reel[^1].Name);

                float beetleFrom = reel[0].Seconds + reel[1].Seconds, beetleTo = beetleFrom + reel[2].Seconds;
                float homeFrom = reel.Take(reel.Length - 1).Sum(s => s.Seconds);

                var intro = new ChapterIntro();
                intro.Begin(GameTarget, GamePosition.Length(), FieldOfView, GamePosition, GameTarget, null, new Random(1), reel);

                //Each close shot: a five-second pause (no frames reach the intro, the wall clock goes on) a second in, and a 400-ms
                //hitch (the intro steps a capped frame's worth) a second later
                float[] pauses = { beetleFrom + 1f, homeFrom + 1f }, hitches = { beetleFrom + 2f, homeFrom + 2f };
                int pause = 0, hitch = 0;
                while (intro.Running)
                {
                    float step = FRAME;
                    if (pause < pauses.Length && intro.Elapsed >= pauses[pause]) { wall += 5f; pause++; }
                    if (hitch < hitches.Length && intro.Elapsed >= hitches[hitch]) { step = 0.4f; hitch++; }
                    wall += step;
                    intro.Update(step);

                    float into = intro.Elapsed;
                    bool beetle = into >= beetleFrom && into < beetleTo, scorpion = into >= homeFrom && into - homeFrom < DesertLanding.LetGo;
                    if (!beetle && !scorpion) continue;

                    Cue cue = cued[beetle ? 0 : 1];
                    float lived = wall - cue.Onset;
                    Assert.True(lived <= SceneRenderer.DesertFeatureSeconds(scorpion), $"roll {seed}: the {(beetle ? "beetle" : "scorpion")} gone {into:F2} s in, {lived - SceneRenderer.DesertFeatureSeconds(scorpion):F2} s after its life");

                    Vector3 foot = SceneRenderer.DesertFeatureAt(cue.At, cue.Yaw, lived, Desert, scorpion);
                    float off = new Vector2(intro.Target.X - foot.X, intro.Target.Z - foot.Z).Length();
                    worst = MathF.Max(worst, off);
                    Assert.True(off <= UNDER_THE_LOOK, $"roll {seed}: the look {off:F2} off the {(beetle ? "beetle" : "scorpion")} {into:F2} s in");
                }

                Assert.Equal(pauses.Length, pause);
                Assert.Equal(hitches.Length, hitch);
            }

            _output.WriteLine(FormattableString.Invariant($"the look at most {worst:F3} off its creature through the pauses and hitches"));
        }

        //A frame at 60 fps; and how far the look may stand off its creature's foot in plan through a close shot, its points laid
        //on the shot's eased clock and walked on a Catmull-Rom between them
        private const float FRAME = 1f / 60f, UNDER_THE_LOOK = 0.02f;

        [Fact]
        public void The_beetle_shot_holds_its_beetle_in_frame_from_its_first_frame_until_it_has_dug_in()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var cue = new Cue();
                IntroShot beetle = DesertIntroShots.Beetle(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                beetle.Track(0f);

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
                scorpion.Track(0f);
                Assert.True(cue.Scorpion);

                for (float seconds = 0f; seconds <= walkEnds; seconds += 0.05f)
                {
                    Vector3 foot = SceneRenderer.DesertFeatureAt(cue.At, cue.Yaw, seconds - cue.Onset, Desert);
                    InFrame(scorpion, seconds, foot, SCORPION_BACK, $"roll {seed}: the scorpion");
                }
            }
        }

        /// <summary>
        /// The close shots stand back (#1158, the owner: "in the close-ups of the beetle and the scorpion the camera should be
        /// somewhat farther away. They are small beetles and the burrowing/emerging animation does not look very realistic, so
        /// let it be seen less"): through its walk across the frame each creature's length (<see cref="DesertCritters"/>' size along its
        /// heading) spans <see cref="CREATURE_SHARE_MIN"/> to <see cref="CREATURE_SHARE_MAX"/> of a 16:9 frame's width at the
        /// shot's own lens: the beetle in its shot, the scorpion in the scorpion home until it lifts off and in the scorpion shot
        /// that stands in where no flight home is clear. From the nearer stands (2.2 off the beetle's line, 3.4 off the
        /// scorpion's walk) the beetle spanned 0.23-0.24 and the scorpion 0.24-0.30.
        /// </summary>
        [Fact]
        public void The_close_shots_stand_back_so_each_creature_spans_about_a_sixth_of_the_frame()
        {
            (float Least, float Most) beetle = (float.MaxValue, 0f), home = (float.MaxValue, 0f), scorpion = (float.MaxValue, 0f);
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var cue = new Cue();
                IntroShot shot = DesertIntroShots.Beetle(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                shot.Track(0f);
                Spans(ref beetle, shot, cue.At, cue.Yaw, cue.Onset, 0f, SceneRenderer.DesertFeatureWalkEnds(scorpion: false) + cue.Onset,
                    DesertCritters.BEETLE_SIZE, scorpion: false);

                IntroShot flight = DesertLanding.Build(Desert, null, GamePosition, GameTarget, FieldOfView, new Random(seed), out DesertLanding.Walk walk);
                Spans(ref home, flight, walk.Start, walk.Yaw, DesertLanding.ONSET, DesertLanding.ONSET + SceneRenderer.DESERT_FEATURE_EMERGE_SECONDS,
                    DesertLanding.Lift, DesertCritters.SCORPION_SIZE, scorpion: true);

                cue = new Cue();
                shot = DesertIntroShots.Scorpion(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                shot.Track(0f);
                Spans(ref scorpion, shot, cue.At, cue.Yaw, cue.Onset, DesertIntroShots.SCORPION_ONSET + SceneRenderer.DESERT_FEATURE_EMERGE_SECONDS,
                    DesertIntroShots.SCORPION_ONSET + SceneRenderer.DesertFeatureWalkEnds(), DesertCritters.SCORPION_SIZE, scorpion: true);
            }

            _output.WriteLine(FormattableString.Invariant(
                $"of a 16:9 frame's width: the beetle {beetle.Least:F2}-{beetle.Most:F2}, the scorpion home's {home.Least:F2}-{home.Most:F2}, the scorpion shot's {scorpion.Least:F2}-{scorpion.Most:F2}"));
            foreach ((string what, (float least, float most)) in new[] { ("the beetle", beetle), ("the scorpion home's scorpion", home), ("the scorpion", scorpion) })
            {
                Assert.True(most <= CREATURE_SHARE_MAX, $"{what} spans up to {most:F2} of the frame's width");
                Assert.True(least >= CREATURE_SHARE_MIN, $"{what} spans as little as {least:F2} of the frame's width");
            }
        }

        //A creature's length as a share of a 16:9 frame's width, at most and at least (small but readable, the owner's "somewhat
        //farther")
        private const float CREATURE_SHARE_MIN = 0.11f, CREATURE_SHARE_MAX = 0.2f;

        //The least and the most of the frame's width the creature cued at `at` along `yaw` (its onset `onset` seconds from the
        //shot's first frame) spans from `from` to `to` seconds into the shot, its length laid along its heading over its foot
        private static void Spans(ref (float Least, float Most) span, IntroShot shot, Vector2 at, float yaw, float onset, float from, float to,
            float length, bool scorpion)
        {
            var heading = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
            for (float seconds = from; seconds <= to; seconds += 0.05f)
            {
                float t = seconds / shot.Seconds;
                shot.Pose(t, out Vector3 lens, out Vector3 look);
                Vector3 foot = SceneRenderer.DesertFeatureAt(at, yaw, seconds - onset, Desert, scorpion);
                Vector2 nose = DesertLandingTests.Screen(lens, look, foot + heading * (length * 0.5f), shot.FieldOfViewAt(t), 16f / 9f);
                Vector2 tail = DesertLandingTests.Screen(lens, look, foot - heading * (length * 0.5f), shot.FieldOfViewAt(t), 16f / 9f);
                float share = MathF.Abs(nose.X - tail.X) * 0.5f;
                span = (MathF.Min(span.Least, share), MathF.Max(span.Most, share));
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
