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
        /// let it be seen less"): through its walk across the frame each creature's body as drawn (every vertex of
        /// <see cref="CritterMesh"/>'s High and Ultra body, whose extents Low and Medium's share, placed by
        /// <see cref="DesertCritters.World"/>: the scorpion's pincers and the curl of its tail, the beetle's antennae and its
        /// legs) spans at most <see cref="BEETLE_SHARE_MAX"/> or <see cref="SCORPION_SHARE_MAX"/> of a 16:9 frame's width at the
        /// shot's own lens, and at least <see cref="CREATURE_SHARE_MIN"/>: the beetle in its shot, the scorpion in the scorpion
        /// home until it lifts off and in the scorpion shot that stands in where no flight home is clear. Both maxima fail with
        /// the lens 2.2 off the beetle's line and 3.4 off the scorpion's walk.
        /// </summary>
        [Fact]
        public void The_close_shots_stand_back_so_each_creature_is_small_in_the_frame()
        {
            (float Least, float Most) beetle = (float.MaxValue, 0f), home = (float.MaxValue, 0f), scorpion = (float.MaxValue, 0f);
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var cue = new Cue();
                IntroShot shot = DesertIntroShots.Beetle(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                shot.Track(0f);
                Spans(ref beetle, shot, cue.At, cue.Yaw, cue.Onset, 0f, SceneRenderer.DesertFeatureWalkEnds(scorpion: false) + cue.Onset, scorpion: false);

                IntroShot flight = DesertLanding.Build(Desert, null, GamePosition, GameTarget, FieldOfView, new Random(seed), out DesertLanding.Walk walk);
                Spans(ref home, flight, walk.Start, walk.Yaw, DesertLanding.ONSET, DesertLanding.ONSET + SceneRenderer.DESERT_FEATURE_EMERGE_SECONDS,
                    DesertLanding.Lift, scorpion: true);

                cue = new Cue();
                shot = DesertIntroShots.Scorpion(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                shot.Track(0f);
                Spans(ref scorpion, shot, cue.At, cue.Yaw, cue.Onset, DesertIntroShots.SCORPION_ONSET + SceneRenderer.DESERT_FEATURE_EMERGE_SECONDS,
                    DesertIntroShots.SCORPION_ONSET + SceneRenderer.DesertFeatureWalkEnds(), scorpion: true);
            }

            _output.WriteLine(FormattableString.Invariant(
                $"of a 16:9 frame's width, as drawn: the beetle {beetle.Least:F2}-{beetle.Most:F2}, the scorpion home's {home.Least:F2}-{home.Most:F2}, the scorpion shot's {scorpion.Least:F2}-{scorpion.Most:F2}"));
            foreach ((string what, (float least, float most), float max) in new[]
                { ("the beetle", beetle, BEETLE_SHARE_MAX), ("the scorpion home's scorpion", home, SCORPION_SHARE_MAX), ("the scorpion", scorpion, SCORPION_SHARE_MAX) })
            {
                Assert.True(most <= max, $"{what} spans up to {most:F2} of the frame's width");
                Assert.True(least >= CREATURE_SHARE_MIN, $"{what} spans as little as {least:F2} of the frame's width");
            }
        }

        /// <summary>
        /// The beetle shot shows each print under the foot that made it (#1217, the issue's measure, in pixels): on every
        /// frame of every roll while the beetle walks, each foot that stands on the sand and the print it laid as it touched
        /// down (its newest; the first frame of a stance is the frame that print appears in) fall within
        /// <see cref="PRINT_PIXELS"/> of each other in a 1920 x 1080 frame at the shot's own lens: the foot's spot on the
        /// sand where <c>Critter.fx</c> puts the foot (<c>DesertCritterTests.FootOnSand</c>, the sand's height under it)
        /// against the print's middle as the scene draws it (<see cref="DesertCritters.PrintCentre"/>, drawn towards the
        /// lens). Seen to fire (BestPractices.md, section 10): the print lifted straight up as the other discs are, and laid
        /// where the foot stands at rest rather than where it reaches as it lands, both fall many pixels off.
        /// </summary>
        [Fact]
        public void The_beetle_shot_shows_each_print_under_the_foot_that_made_it()
        {
            float worst = 0f, upright = float.MaxValue, unreached = float.MaxValue, radius = float.MaxValue;
            int planted = 0, appeared = 0;
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                var cue = new Cue();
                IntroShot shot = DesertIntroShots.Beetle(cue.Take, Desert, Ground, FieldOfView, new Random(seed));
                shot.Track(0f);
                var heights = new DesertCritters.SandHeights(Ground);
                var wasDown = new bool[CritterMesh.BEETLE_FEET];
                float walkEnds = MathF.Min(SceneRenderer.DesertFeatureWalkEnds(scorpion: false) + cue.Onset, shot.Seconds);
                for (float seconds = 0f; seconds <= walkEnds; seconds += 1f / 60f)
                {
                    Assert.True(DesertCritters.TryFeaturePose(cue.At, cue.Yaw, cue.Onset, seconds, Ground, out DesertCritters.Pose pose, scorpion: false));
                    float t = seconds / shot.Seconds;
                    shot.Pose(t, out Vector3 lens, out Vector3 look);
                    float fov = shot.FieldOfViewAt(t);
                    Vector2 Pixel(Vector3 point)
                    {
                        Vector2 screen = DesertLandingTests.Screen(lens, look, point, fov, 16f / 9f);
                        return new Vector2((screen.X + 1f) * 960f, (1f - screen.Y) * 540f);
                    }

                    for (int foot = 0; foot < CritterMesh.BEETLE_FEET; foot++)
                    {
                        bool down = CritterGait.Planted(DesertCritterTests.FootPhase(pose, foot));
                        bool justDown = down && !wasDown[foot] && seconds > 0f;
                        wasDown[foot] = down;
                        if (!down || !DesertCritters.PrintCentre(pose, DesertCritters.SandHeights.TRAILS - 1, foot, 0, lens, heights, out Vector3 centre, out _))
                            continue;

                        Vector2 plan = DesertCritterTests.FootOnSand(pose, foot);
                        Vector2 spot = Pixel(new Vector3(plan.X, Ground(plan.X, plan.Y), plan.Y));
                        worst = MathF.Max(worst, Vector2.Distance(spot, Pixel(centre)));
                        planted++;
                        if (justDown) appeared++;

                        //The same print lifted straight up, and laid where the foot stands at rest rather than where it
                        //reaches as it lands (the beetle walks straight, along its yaw)
                        Assert.True(DesertCritters.TrailPrint(pose, foot, 0, out Vector2 at, out _));
                        var sand = new Vector3(at.X, Ground(at.X, at.Y), at.Y);
                        upright = MathF.Min(upright, Vector2.Distance(spot, Pixel(sand + Vector3.Up * 0.03f)));
                        Vector2 back = at - new Vector2(MathF.Sin(pose.Yaw), MathF.Cos(pose.Yaw)) * (CritterGait.Stride(scorpion: false) * DesertCritters.BEETLE_SIZE);
                        unreached = MathF.Min(unreached, Vector2.Distance(spot, Pixel(new Vector3(back.X, Ground(back.X, back.Y), back.Y))));
                        //Its radius across the frame (along the lens's level right, where the sand does not foreshorten it)
                        Vector3 across = Vector3.Normalize(Vector3.Cross(look - lens, Vector3.Up));
                        radius = MathF.Min(radius, Vector2.Distance(Pixel(sand), Pixel(sand + across * (DesertCritters.BEETLE_SIZE * DesertCritters.PRINT_RADIUS))));
                    }
                }
            }

            _output.WriteLine(FormattableString.Invariant(
                $"{planted} planted feet over {ROLLS} rolls ({appeared} on the frame their print appeared): each at most {worst:F3} px from its print in 1920 x 1080; lifted straight up at least {upright:F1} px off, laid at the foot's rest at least {unreached:F1} px; a print's radius across the frame at least {radius:F1} px"));
            Assert.True(appeared > 20 * ROLLS && planted > 100 * ROLLS, $"too little walking to judge: {planted} planted feet, {appeared} touchdowns");
            Assert.True(worst < PRINT_PIXELS, $"a planted foot stands {worst:F3} px from its print in the beetle shot");
            Assert.True(upright > 5f * PRINT_PIXELS && unreached > 5f * PRINT_PIXELS, $"seen to fire: lifted straight up {upright:F1} px, a foot at rest {unreached:F1} px");
        }

        //How far a planted foot's spot may stand from the middle of its print in the beetle shot's 1920 x 1080 frame
        private const float PRINT_PIXELS = 0.5f;

        //A creature's body as drawn, as a share of a 16:9 frame's width: at most these (the owner's "somewhat farther": the
        //beetle under a fifth, the scorpion about a quarter, its pincers and its tail's curl reaching past its body), and at least
        //this (small but readable)
        private const float BEETLE_SHARE_MAX = 0.2f, SCORPION_SHARE_MAX = 0.3f, CREATURE_SHARE_MIN = 0.12f;

        private static readonly Vector3[] BeetleBody = CritterMesh.FineBeetle().Vertices.Select(v => v.Position).ToArray();
        private static readonly Vector3[] ScorpionBody = CritterMesh.FineScorpion().Vertices.Select(v => v.Position).ToArray();

        //The least and the most of the frame's width the creature cued at `at` along `yaw` (its onset `onset` seconds from the
        //shot's first frame) spans from `from` to `to` seconds into the shot: its body's every vertex where the scene draws it,
        //at rest (the gait's swing of the legs and the tail is left out)
        private static void Spans(ref (float Least, float Most) span, IntroShot shot, Vector2 at, float yaw, float onset, float from, float to,
            bool scorpion)
        {
            Vector3[] body = scorpion ? ScorpionBody : BeetleBody;
            for (float seconds = from; seconds <= to; seconds += 0.05f)
            {
                float t = seconds / shot.Seconds;
                shot.Pose(t, out Vector3 lens, out Vector3 look);
                Assert.True(DesertCritters.TryFeaturePose(at, yaw, onset, seconds, Ground, out DesertCritters.Pose pose, scorpion),
                    $"no {(scorpion ? "scorpion" : "beetle")} {seconds:F2} s into {shot.Name}");
                Matrix world = DesertCritters.World(pose, scorpion);

                Vector3 forward = Vector3.Normalize(look - lens);
                Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
                float across = MathF.Tan(shot.FieldOfViewAt(t) * 0.5f) * 16f / 9f;
                float left = float.MaxValue, rightmost = float.MinValue;
                foreach (Vector3 vertex in body)
                {
                    Vector3 d = Vector3.Transform(vertex, world) - lens;
                    float x = Vector3.Dot(d, right) / Vector3.Dot(d, forward) / across;
                    left = MathF.Min(left, x);
                    rightmost = MathF.Max(rightmost, x);
                }

                float share = (rightmost - left) * 0.5f;
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
