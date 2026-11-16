using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// Space's still shot and the saucer that crosses it (#1117). The owner: "one shot of the intro animation static, with a
    /// big planet in the background, the smaller island somewhere lower, and then an unexpected flying saucer flying through
    /// this static shot, relatively close to the camera". Held over the real builder, the shipped config and the craft's own
    /// flight (<see cref="SaucerFlight.WorldAt(float, in SaucerPass)"/>) at every roll and at moments all round the wall
    /// clock: the lens stands still; the planet's whole disc stands in the frame's upper half, larger than the station, which
    /// stands low to one side of it clear of the skip hint; the craft, cued on the shot's first frame, comes into the frame
    /// after a moment of stillness, crosses it close to the lens and is gone before the cut, on its run in every frame of the
    /// shot; it never comes near the lens or the island; and with no pass cued the loop flies exactly as it always did.
    /// </summary>
    public class SpaceIntroShotsTests
    {
        private const float FRAME = 1f / 60f;

        //The window shapes the game is played at: 16:9 the narrowest it targets, the owner's 3840x1600, 16:10 and 4:3. The
        //projection is Hor+, so a narrower shape only crops the sides
        private static readonly float[] ASPECTS = { 16f / 9f, 2.4f, 1.6f, 4f / 3f };
        public static readonly IEnumerable<object[]> Aspects = ASPECTS.Select(a => new object[] { a });

        //The rolls: the shot's one roll is the side, so a few seeds cover both
        private const int ROLLS = 8;

        //Wall-clock moments the shot may come on screen at: the loop's phase and its bob differ at each
        private static readonly float[] WALL_CLOCKS = { 0f, 13.7f, 41.2f, 77.9f, 250.3f, 1234.5f, 4321.9f };

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame; and the skip
        //hint's band along the bottom edge (up to 12.2 % of the frame's height, "Chapter intro prologues" in the docs)
        private const float FRAME_MARGIN = 0.05f;
        private const float SKIP_HINT_SHARE = 0.13f;

        //The craft's reach from its middle: the rim's radius, and a little for the dome's antenna and the bank
        private const float CRAFT_REACH = SaucerMesh.RIM_RADIUS + 0.5f;

        //"Relatively close to the camera": the craft's middle never nearer the lens than this anywhere in the pass, eases
        //included, and at its nearest in the shot within this much of the distance it is laid at
        private const float NEAREST_EVER = 20f;
        private const float NEAREST_SLACK = 4f;

        //"Unexpected": the frame holds still with no craft in it for at least this long at every shape, and this long at 16:9;
        //and it is gone, wholly outside the frame, at least this long before the cut at every shape. Measured: 0.75 s still and
        //gone 0.15 s before the cut at 2.4:1, where the frame is widest; 1.17 and 0.57 at 16:9
        private const float STILL_SECONDS = 0.7f, STILL_SECONDS_16_9 = 1.1f;
        private const float GONE_BEFORE_CUT = 0.12f;

        //Neither a blur nor a crawl: the craft crosses the frame (from first in to last out) in this many seconds at 16:9 (2.37,
        //measured)
        private const float CROSSING_MIN = 1.8f, CROSSING_MAX = 3.2f;

        //Its dome turned to the lens: at its moment the lens sees the craft's top at least this many degrees over its rim's plane
        //(the loop's own bank would show it the belly)
        private const float TOP_SEEN_DEGREES = 8f;

        //The space reel's whole opening, the tour's last leg included: 13.9 s before the still shot, and the owner prefers
        //faster over slower
        private const float MAX_OPENING_SECONDS = 14.3f;

        private static readonly SpaceSceneConfig Space = new();

        private readonly ITestOutputHelper _output;

        public SpaceIntroShotsTests(ITestOutputHelper output) => _output = output;

        private static Vector3 Planet => Vector3.Normalize(Space.Planet.Direction.ToVector3());

        //The shot for a roll, with the pass it cues on its first frame when that comes on screen at the wall-clock moment wall.
        //A run that keeps up (no hitch, no pause) cues the same pass on every frame after
        private static (IntroShot Shot, SaucerPass Pass) Saucer(int seed, float wall)
        {
            SaucerPass pass = default;
            IntroShot shot = SpaceIntroShots.Saucer(Space, (point, closest) => pass = SaucerPass.Through(point, closest), () => wall,
                new Random(seed));
            shot.Start();
            shot.Track(0f);
            Assert.True(pass.Cued, "the shot cued no pass as it came on screen");
            return (shot, pass);
        }

        private static (Matrix View, Matrix Projection, Vector3 Lens) Camera(IntroShot shot, float aspect)
        {
            shot.Pose(0f, out Vector3 lens, out Vector3 target);
            return (Matrix.CreateLookAt(lens, target, Vector3.Up),
                Matrix.CreatePerspectiveFieldOfView(shot.FieldOfView, aspect, 0.1f, 5000f), lens);
        }

        //A point in normalized device coordinates (x right, y up, -1..1 across the frame), and whether it is ahead of the lens
        private static Vector3 Project(Vector3 point, Matrix view, Matrix projection, out bool ahead)
        {
            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), view * projection);
            ahead = clip.W > 0.1f;
            return new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.W);
        }

        //A direction (a point at infinity) the same way
        private static Vector3 ProjectDirection(Vector3 direction, Matrix view, Matrix projection, out bool ahead)
        {
            Vector4 clip = Vector4.Transform(new Vector4(direction, 0f), view * projection);
            ahead = clip.W > 1e-4f;
            return new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.W);
        }

        private static bool Inside(Vector3 ndc, bool ahead, float margin = FRAME_MARGIN) =>
            ahead && MathF.Abs(ndc.X) <= 1f - margin && MathF.Abs(ndc.Y) <= 1f - margin;

        //Whether any of a sphere is in the frame: its middle's place grown by its radius over its depth, against the edges. A
        //sphere reaching the lens's own plane counts as in it
        private static bool AnyInFrame(Vector3 middle, float radius, Matrix view, Matrix projection, float fov, float aspect)
        {
            Vector3 ndc = Project(middle, view, projection, out _);
            if (ndc.Z < radius) return true;
            float grow = radius / ndc.Z / MathF.Tan(fov * 0.5f);
            return MathF.Abs(ndc.X) - grow / aspect < 1f && MathF.Abs(ndc.Y) - grow < 1f;
        }

        //The planet's disc: its middle and points round its limb
        private static IEnumerable<Vector3> PlanetLimb()
        {
            Vector3 middle = Planet;
            Vector3 a = Vector3.Normalize(Vector3.Cross(middle, Vector3.Up)), b = Vector3.Cross(a, middle);
            float radius = MathHelper.ToRadians(Space.Planet.AngularRadiusDegrees);
            for (int k = 0; k < 48; k++)
            {
                float t = k * MathHelper.TwoPi / 48f;
                yield return Vector3.Normalize(middle * MathF.Cos(radius) + (a * MathF.Cos(t) + b * MathF.Sin(t)) * MathF.Sin(radius));
            }
        }

        //The island's deck rim and the drain's tip
        private static IEnumerable<Vector3> Station()
        {
            for (int k = 0; k < 48; k++)
            {
                float t = k * MathHelper.TwoPi / 48f;
                yield return new Vector3(MathF.Cos(t) * ArenaIsland.RADIUS, ArenaIsland.TOP_Y, MathF.Sin(t) * ArenaIsland.RADIUS);
                yield return new Vector3(MathF.Cos(t) * ArenaIsland.RADIUS, ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT, MathF.Sin(t) * ArenaIsland.RADIUS);
            }
            yield return new Vector3(0f, ArenaIsland.FUNNEL_BOTTOM_Y, 0f);
        }

        [Fact]
        public void The_saucer_shot_stands_still()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                (IntroShot shot, _) = Saucer(seed, 0f);
                shot.Pose(0f, out Vector3 lens0, out Vector3 look0);
                for (float t = 0f; t <= 1f; t += 0.05f)
                {
                    shot.Pose(t, out Vector3 lens, out Vector3 look);
                    Assert.Equal(lens0, lens);
                    Assert.Equal(look0, look);
                    Assert.Equal(shot.FieldOfView, shot.FieldOfViewAt(t));
                    Assert.Equal(0f, shot.RollAt(t));
                }
            }
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void The_planet_stands_big_over_the_station_which_stands_small_and_low_to_one_side(float aspect)
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                (IntroShot shot, _) = Saucer(seed, 0f);
                (Matrix view, Matrix projection, _) = Camera(shot, aspect);

                //The planet's whole disc in the frame, its middle in the upper half
                Vector3 middle = ProjectDirection(Planet, view, projection, out bool ahead);
                Assert.True(Inside(middle, ahead) && middle.Y > 0.3f, $"seed {seed}: the planet's middle at ({middle.X:F2}, {middle.Y:F2})");
                float planetLeft = float.MaxValue, planetRight = float.MinValue, planetBottom = float.MaxValue;
                foreach (Vector3 limb in PlanetLimb())
                {
                    Vector3 ndc = ProjectDirection(limb, view, projection, out bool limbAhead);
                    Assert.True(Inside(ndc, limbAhead), $"seed {seed}: the planet's limb at ({ndc.X:F2}, {ndc.Y:F2}) is out of the frame");
                    planetLeft = MathF.Min(planetLeft, ndc.X);
                    planetRight = MathF.Max(planetRight, ndc.X);
                    planetBottom = MathF.Min(planetBottom, ndc.Y);
                }

                //The station in the frame, over the skip hint, under the planet's disc, its deck in the lower half
                float stationLeft = float.MaxValue, stationRight = float.MinValue, stationTop = float.MinValue;
                foreach (Vector3 point in Station())
                {
                    Vector3 ndc = Project(point, view, projection, out bool pointAhead);
                    Assert.True(Inside(ndc, pointAhead) && ndc.Y > -1f + 2f * SKIP_HINT_SHARE,
                        $"seed {seed}: the station's point {point} at ({ndc.X:F2}, {ndc.Y:F2}) is out of the frame or under the skip hint");
                    stationLeft = MathF.Min(stationLeft, ndc.X);
                    stationRight = MathF.Max(stationRight, ndc.X);
                    stationTop = MathF.Max(stationTop, ndc.Y);
                }
                Vector3 deck = Project(new Vector3(0f, ArenaIsland.TOP_Y, 0f), view, projection, out _);
                Assert.True(deck.Y < -0.4f, $"seed {seed}: the deck's middle at {deck.Y:F2} of the frame's height, not low");
                Assert.True(stationTop < planetBottom, $"seed {seed}: the island reaches {stationTop:F2}, the planet's disc down to {planetBottom:F2}");

                //Smaller than the planet, and to one side of it: the deck's middle and the planet's on either side of the frame's middle
                Assert.True((stationRight - stationLeft) < 0.75f * (planetRight - planetLeft),
                    $"seed {seed}: the island is {stationRight - stationLeft:F2} across, the planet {planetRight - planetLeft:F2}");
                Assert.True(MathF.Sign(deck.X) == -MathF.Sign(middle.X) && MathF.Abs(deck.X) > 0.1f,
                    $"seed {seed}: the deck at {deck.X:F2} across, the planet at {middle.X:F2}");

                if (seed == 1)
                    _output.WriteLine(FormattableString.Invariant(
                        $"aspect {aspect:F2}: planet ({middle.X:F2}, {middle.Y:F2}) x {planetLeft:F2}..{planetRight:F2}, down to {planetBottom:F2}; station x {stationLeft:F2}..{stationRight:F2}, up to {stationTop:F2}, deck ({deck.X:F2}, {deck.Y:F2})"));
            }
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void The_saucer_crosses_the_frame_close_to_the_lens_after_a_moment_of_stillness_and_is_gone_before_the_cut(float aspect)
        {
            float shortestStill = float.MaxValue, shortestGone = float.MaxValue, nearest = float.MaxValue, farthest = 0f;
            float crossingMin = float.MaxValue, crossingMax = 0f, topLeast = float.MaxValue;

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                foreach (float wall in WALL_CLOCKS)
                {
                    (IntroShot shot, SaucerPass pass) = Saucer(seed, wall);
                    (Matrix view, Matrix projection, Vector3 lens) = Camera(shot, aspect);

                    //The moment it was cued for, at the point it was laid at, its middle in the frame
                    Assert.Equal(wall + SpaceIntroShots.PASS_AT_SECONDS, pass.Closest, 2);
                    Matrix moment = SaucerFlight.WorldAt(pass.Closest, pass);
                    Vector3 at = moment.Translation;
                    Assert.True(Vector3.Distance(at, pass.Point) < 1e-2f, $"seed {seed} at {wall}: the craft is {Vector3.Distance(at, pass.Point):F3} off its point at its moment");

                    //Its top to the lens, so the dome and the crew are seen
                    float topSeen = MathHelper.ToDegrees(MathF.Asin(Vector3.Dot(Vector3.Normalize(moment.Up), Vector3.Normalize(lens - at))));
                    Assert.True(topSeen >= TOP_SEEN_DEGREES, $"seed {seed} at {wall}: the lens sees the craft's top at {topSeen:F1} degrees");
                    topLeast = MathF.Min(topLeast, topSeen);
                    Assert.Equal(SpaceIntroShots.PASS_DISTANCE, Vector3.Distance(at, lens), 2);
                    Vector3 middle = Project(at, view, projection, out bool ahead);
                    Assert.True(Inside(middle, ahead, 0.4f), $"seed {seed} at {wall}: the craft's middle at ({middle.X:F2}, {middle.Y:F2}) at its moment");

                    float first = float.NaN, last = float.NaN, closest = float.MaxValue;
                    for (float s = 0f; s <= shot.Seconds; s += FRAME)
                    {
                        float time = wall + s;
                        Assert.Equal(1f, pass.Weight(time));
                        Vector3 craft = SaucerFlight.WorldAt(time, pass).Translation;
                        closest = MathF.Min(closest, Vector3.Distance(craft, lens));
                        if (AnyInFrame(craft, CRAFT_REACH, view, projection, shot.FieldOfView, aspect))
                        {
                            if (float.IsNaN(first)) first = s;
                            last = s;
                        }
                    }

                    Assert.False(float.IsNaN(first), $"seed {seed} at {wall}: the craft never comes into the frame");
                    float still = aspect == 16f / 9f ? STILL_SECONDS_16_9 : STILL_SECONDS;
                    Assert.True(first >= still, $"seed {seed} at {wall}: the craft comes into the frame {first:F2} s in");
                    Assert.True(last <= shot.Seconds - GONE_BEFORE_CUT, $"seed {seed} at {wall}: the craft is in the frame {last:F2} s in, the cut at {shot.Seconds:F2}");
                    Assert.InRange(closest, SpaceIntroShots.PASS_DISTANCE - NEAREST_SLACK, SpaceIntroShots.PASS_DISTANCE + 0.01f);
                    if (aspect == 16f / 9f)
                        Assert.InRange(last - first, CROSSING_MIN, CROSSING_MAX);

                    shortestStill = MathF.Min(shortestStill, first);
                    shortestGone = MathF.Min(shortestGone, shot.Seconds - last);
                    nearest = MathF.Min(nearest, closest);
                    farthest = MathF.Max(farthest, closest);
                    crossingMin = MathF.Min(crossingMin, last - first);
                    crossingMax = MathF.Max(crossingMax, last - first);
                }
            }

            _output.WriteLine(FormattableString.Invariant(
                $"aspect {aspect:F2}: still for {shortestStill:F2} s at least, gone {shortestGone:F2} s before the cut, crossing {crossingMin:F2}-{crossingMax:F2} s, nearest {nearest:F1}-{farthest:F1}, top seen at {topLeast:F1} degrees at least"));
        }

        [Fact]
        public void The_pass_never_comes_near_the_lens_or_the_island_and_eases_on_and_off_without_a_jump()
        {
            float fastest = 0f, nearestLens = float.MaxValue, nearestAxis = float.MaxValue;

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                foreach (float wall in WALL_CLOCKS)
                {
                    (IntroShot shot, SaucerPass pass) = Saucer(seed, wall);
                    shot.Pose(0f, out Vector3 lens, out _);

                    //From the cue through the ease back onto the loop, and on a while after
                    float from = wall, to = pass.Closest + SaucerPass.HOLD_SECONDS + SaucerPass.EASE_SECONDS + 1f;
                    Vector3 previous = SaucerFlight.WorldAt(from, pass).Translation;
                    for (float time = from + FRAME; time <= to; time += FRAME)
                    {
                        Vector3 craft = SaucerFlight.WorldAt(time, pass).Translation;
                        fastest = MathF.Max(fastest, Vector3.Distance(craft, previous) / FRAME);
                        nearestLens = MathF.Min(nearestLens, Vector3.Distance(craft, lens));
                        nearestAxis = MathF.Min(nearestAxis, new Vector2(craft.X, craft.Z).Length());
                        previous = craft;
                    }
                }
            }

            _output.WriteLine(FormattableString.Invariant($"fastest {fastest:F1} u/s, nearest the lens {nearestLens:F1}, nearest the axis {nearestAxis:F1}"));
            Assert.True(nearestLens >= NEAREST_EVER, $"the craft comes {nearestLens:F1} from the lens");
            Assert.True(nearestAxis >= ArenaIsland.RADIUS + CRAFT_REACH + 100f, $"the craft comes {nearestAxis:F1} from the island's axis");
            //A jump would be a frame's leap; the eases' quickest is a swoop out of every frame
            Assert.True(fastest < 120f, $"the craft moves {fastest:F1} units a second at its quickest");
        }

        [Fact]
        public void The_saucer_keeps_to_the_intros_clock_through_hitches_and_a_pause()
        {
            //A level's first seconds as the intro sees them: frames that keep up, hitches the intro's clock is held to a frame's
            //worth through (ChapterIntro.MAX_INTRO_STEP) and a pause the wall clock runs on through while the intro stands still
            var frames = new List<(float Elapsed, bool Paused)>();
            for (int k = 0; k < 400; k++)
            {
                float elapsed = k % 37 == 5 ? 0.11f : k % 53 == 20 ? 0.23f : FRAME;
                frames.Add((elapsed, k is >= 90 and < 120));
            }

            float worst = 0f;
            foreach (float wall0 in WALL_CLOCKS)
            {
                SaucerPass pass = default;
                double wall = wall0;
                IntroShot shot = SpaceIntroShots.Saucer(Space, (point, closest) => pass = SaucerPass.Through(point, closest),
                    () => (float)wall, new Random(3));

                //Where a run that keeps up has the craft at each second of the shot: its first frame's cue, flown on the wall clock
                shot.Start();
                shot.Track(0f);
                SaucerPass kept = pass;

                double intro = 0.0;
                bool lagged = false;
                foreach ((float elapsed, bool paused) in frames)
                {
                    wall += elapsed;
                    if (paused) continue;

                    intro += MathF.Min(elapsed, ChapterIntro.MAX_INTRO_STEP);
                    if (intro >= shot.Seconds) break;
                    lagged |= wall - wall0 - intro > 0.5;

                    shot.Track((float)intro);
                    Vector3 craft = SaucerFlight.WorldAt((float)wall, pass).Translation;
                    Vector3 laid = SaucerFlight.WorldAt(wall0 + (float)intro, kept).Translation;
                    worst = MathF.Max(worst, Vector3.Distance(craft, laid));
                }

                Assert.True(lagged, $"at {wall0}: the run never fell behind the wall clock, so it tests nothing");
            }

            _output.WriteLine(FormattableString.Invariant($"the craft at most {worst:F3} off where the shot was laid for it"));
            Assert.True(worst < 0.1f, $"the craft is {worst:F2} off where the shot was laid for it after a hitch or a pause");
        }

        [Fact]
        public void After_the_pass_the_craft_flies_its_loop_turned_on_and_with_no_pass_the_loop_is_as_it_always_was()
        {
            for (float time = 0f; time < 3000f; time += 7.3f)
            {
                Matrix reference = Loop(time, 0f);
                AssertClose(reference, SaucerFlight.WorldAt(time), time);
                AssertClose(reference, SaucerFlight.WorldAt(time, default), time);
            }

            foreach (float wall in WALL_CLOCKS)
            {
                (_, SaucerPass pass) = Saucer(1, wall);
                float after = pass.Closest + SaucerPass.HOLD_SECONDS + SaucerPass.EASE_SECONDS + 0.01f;
                for (float time = after; time < after + 200f; time += 3.1f)
                {
                    Assert.Equal(0f, pass.Weight(time));
                    AssertClose(Loop(time, pass.LoopShift), SaucerFlight.WorldAt(time, pass), time);
                }

                //And the turn is the one that brings the loop to the point's bearing at the moment
                float bearing = MathF.Atan2(pass.Point.Z, pass.Point.X);
                Matrix there = Loop(pass.Closest, pass.LoopShift);
                Assert.True(MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(there.Translation.Z, there.Translation.X) - bearing)) < 1e-3f,
                    $"at {wall}: the loop is not at the pass's bearing at its moment");
            }
        }

        [Fact]
        public void The_opening_stays_short()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot[] shots = SpaceIntroShots.Build(Space, null, () => 0f, new Random(seed));
                Assert.Equal("the saucer", shots[0].Name);
                float total = shots.Sum(s => s.Seconds) + ChapterIntro.ARRIVAL_SECONDS;
                Assert.True(total <= MAX_OPENING_SECONDS, $"seed {seed}: the opening runs {total:F1} s");
            }
        }

        //The loop as #955 flew it, written out here so a change to it fails: on a circle of 240 round the station, 30 up with a
        //bob of 14, a turn in 70 s from 2.2 radians, banked 0.22 with a rock of 0.06, spinning 0.8 radians a second; and turned on
        //by shift
        private static Matrix Loop(float time, float shift)
        {
            float angle = 2.2f + shift + time * MathHelper.TwoPi / 70f;
            var centre = new Vector3(MathF.Cos(angle) * 240f, 30f + 14f * MathF.Sin(time * 0.21f), MathF.Sin(angle) * 240f);
            Matrix world = Matrix.CreateRotationY(-time * 0.8f)
                * Matrix.CreateRotationZ(0.22f + 0.06f * MathF.Sin(time * 0.7f))
                * Matrix.CreateRotationY(-angle);
            world.Translation = centre;
            return world;
        }

        private static void AssertClose(Matrix expected, Matrix actual, float time)
        {
            Assert.True(Vector3.Distance(expected.Translation, actual.Translation) < 1e-3f,
                $"at {time}: the craft is at {actual.Translation}, the loop at {expected.Translation}");
            Assert.True(Vector3.Distance(expected.Up, actual.Up) < 1e-4f && Vector3.Distance(expected.Right, actual.Right) < 1e-4f,
                $"at {time}: the craft lies otherwise than the loop has it");
        }
    }
}
