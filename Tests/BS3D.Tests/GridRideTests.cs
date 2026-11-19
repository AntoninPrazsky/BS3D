using BS3D.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The Grid's ride beside a light cycle (#1116, <see cref="GridRide"/>): the lens a few units beside the cycle the frame draws,
    /// at the real wall clock, however late the shot comes on and through a pause; never in a wall, a cycle or a solid; the cycle
    /// whole in the frame; a turn ridden, not snapped; nothing allocated choosing again; and the reel it opens the Grid's prologue
    /// with. The cycle it is held against is made the way
    /// <see cref="LightCycles"/> makes the one it draws (its <c>Rider</c> on its <c>RiderSeed</c> over a <c>Lattice</c> of the
    /// same solids, its wall by <c>Trail</c>), not through <see cref="LightCycleRuns"/>, which the ride itself reads.
    /// </summary>
    public class GridRideTests
    {
        private readonly ITestOutputHelper _output;

        public GridRideTests(ITestOutputHelper output) => _output = output;

        private static readonly GridSceneConfig Grid = new();
        private static float FloorY => Grid.Terrain.LevelY;

        //Builds at wall-clock seconds spread from the game's first second to an hour in, and the lateness the shot comes on with:
        //on time, the frame that built the level (a measured run came on 0.94 s late, a capture's stall included), and the most
        //the plan covers
        private static readonly float[] LATENESS = { 0f, 0.6f, GridIntroShots.RIDE_LATE };
        private const int BUILDS = 120;

        private static float BuildTime(int i) => i == 0 ? 0f : 3.7f + i * 29.3f;

        private const float FRAME = 1f / 60f;
        private static readonly float[] ASPECTS = { 4f / 3f, 16f / 10f, 16f / 9f, 21f / 9f };

        /// <summary>The landmark ring of <paramref name="grid"/>, where <c>GridLayout.Landmark</c> stands it.</summary>
        private static GridRing RingOf(GridSceneConfig grid)
        {
            GridLandmarkConfig landmark = grid.Landmark;
            float bearing = MathHelper.ToRadians(landmark.Bearing);
            var stand = new Vector3(MathF.Cos(bearing) * landmark.Distance, grid.Terrain.LevelY, MathF.Sin(bearing) * landmark.Distance);
            Vector3 normal = Vector3.Normalize(new Vector3(-stand.X, 0f, -stand.Z));
            return new GridRing(stand + Vector3.Up * (landmark.Radius + landmark.TubeRadius), normal,
                landmark.Radius - landmark.TubeRadius, landmark.Radius + landmark.TubeRadius, landmark.Width * 0.5f);
        }

        /// <summary>
        /// Solids rolled the way the config draws them (<c>GridTowerConfig</c>'s count, radii and sizes, cubes first): a cube near
        /// the ring's inner edge reaches in among the cycles (a 75 cube centred at 140 reaches to 102.5 square on an axis and to 87
        /// on a diagonal, and the cycles ride from 56 to 124).
        /// </summary>
        private static List<GridSolid> Solids(int seed)
        {
            GridTowerConfig towers = Grid.Towers;
            var random = new Random(seed);
            var solids = new List<GridSolid>();
            for (int i = 0; i < towers.Count; i++)
            {
                bool cube = i < (int)MathF.Round(towers.Count * towers.CubeFraction);
                float angle = (float)random.NextDouble() * MathHelper.TwoPi;
                float radius = MathHelper.Lerp(towers.RadiusMin, towers.RadiusMax, (float)random.NextDouble());
                Vector3 size = cube
                    ? new Vector3(MathHelper.Lerp(towers.CubeSizeMin, towers.CubeSizeMax, (float)random.NextDouble()))
                    : new Vector3(MathHelper.Lerp(towers.TowerFootprintMin, towers.TowerFootprintMax, (float)random.NextDouble()),
                        MathHelper.Lerp(towers.TowerHeightMin, towers.TowerHeightMax, (float)random.NextDouble()),
                        MathHelper.Lerp(towers.TowerFootprintMin, towers.TowerFootprintMax, (float)random.NextDouble()));
                solids.Add(new GridSolid(new Vector3(MathF.Cos(angle) * radius, FloorY, MathF.Sin(angle) * radius), size, cube));
            }

            return solids;
        }

        /// <summary>The cycles as <see cref="LightCycles.Draw"/> makes them, clear of <c>solids</c> (#1136): where each stands and
        /// its wall's foot, at a second.</summary>
        private sealed class DrawnCycles
        {
            private readonly LightCycles.Rider[] _riders = new LightCycles.Rider[LightCycles.RIDERS];
            private readonly VertexPositionTexture[] _trail = new VertexPositionTexture[LightCycles.TRAIL_POINTS * 2];

            public DrawnCycles(IReadOnlyList<GridSolid> solids)
            {
                var lattice = new LightCycles.Lattice(solids);
                for (int i = 0; i < _riders.Length; i++) _riders[i] = new LightCycles.Rider(LightCycles.RiderSeed(Grid, i), lattice);
            }

            public void At(int i, float time, out Vector2 position, out Vector2 heading) =>
                _riders[i].At(time * LightCycles.SPEED, out position, out heading);

            //How far a point on the floor's plane stands from cycle i's wall at a second, along the strip Draw uploads
            public float WallDistance(int i, float time, Vector2 point)
            {
                float distance = time * LightCycles.SPEED;
                _riders[i].At(distance, out _, out _);
                int count = _riders[i].Trail(distance, _trail, 0, FloorY);
                float nearest = float.PositiveInfinity;
                for (int k = 2; k < count; k += 2)
                {
                    Vector2 a = new(_trail[k - 2].Position.X, _trail[k - 2].Position.Z), b = new(_trail[k].Position.X, _trail[k].Position.Z);
                    Vector2 ab = b - a;
                    float s = ab.LengthSquared() > 1e-8f ? MathHelper.Clamp(Vector2.Dot(point - a, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
                    nearest = MathF.Min(nearest, Vector2.Distance(point, a + ab * s));
                }

                return nearest;
            }
        }

        /// <summary>One frame of a ride: the second it is drawn at, the lens, its look and the field of view.</summary>
        private readonly record struct Frame(float Time, Vector3 Lens, Vector3 Look, float FieldOfView);

        /// <summary>
        /// The ride of a reel built at <paramref name="built"/> on the wall clock, coming on <paramref name="late"/> seconds after
        /// the second the reel puts it at, played a frame every <paramref name="frame"/> seconds of the wall clock (60 fps unless
        /// said), each advancing the intro's clock by at most <see cref="ChapterIntro.MAX_INTRO_STEP"/>, and stopped for
        /// <paramref name="pause"/> seconds of the wall clock once <paramref name="pauseAt"/> seconds of the shot have played (the
        /// pause page stops the intro and not the wall clock); null when the reel has none.
        /// </summary>
        private static List<Frame> Ride(float built, float late, IReadOnlyList<GridSolid> solids, out IntroShot[] reel,
            float frame = FRAME, float pause = 0f, float pauseAt = 0f)
        {
            float now = built;
            reel = GridIntroShots.Shots(Grid, solids, RingOf(Grid), built, () => now, new Random(7));
            if (reel == null) return null;

            float on = built;
            IntroShot ride = null;
            foreach (IntroShot shot in reel)
            {
                if (shot.Name == "the cycle")
                {
                    ride = shot;
                    break;
                }

                on += shot.Seconds;
            }

            if (ride == null) return null;

            now = on + late;
            ride.Start();

            var frames = new List<Frame>();
            float step = MathF.Min(frame, ChapterIntro.MAX_INTRO_STEP);
            int count = (int)MathF.Round(ride.Seconds / step);
            for (int f = 0; f <= count; f++)
            {
                now = on + late + f * frame + (pause > 0f && f * step > pauseAt ? pause : 0f);
                ride.Pose(f / (float)count, out Vector3 lens, out Vector3 look);
                frames.Add(new Frame(now, lens, look, ride.FieldOfViewAt(f / (float)count)));
            }

            return frames;
        }

        private static readonly float Beside = MathF.Sqrt(GridRide.SIDE * GridRide.SIDE + GridRide.AHEAD * GridRide.AHEAD);

        //The cycle a frame films: the drawn one standing nearest where the ride stands beside its cycle (another may pass nearer)
        private static int Filmed(DrawnCycles cycles, Frame frame)
        {
            int nearest = 0;
            float best = float.PositiveInfinity;
            for (int i = 0; i < LightCycles.RIDERS; i++)
            {
                cycles.At(i, frame.Time, out Vector2 p, out _);
                float d = MathF.Abs(Vector2.Distance(p, new Vector2(frame.Lens.X, frame.Lens.Z)) - Beside);
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }

            return nearest;
        }

        [Fact]
        public void The_lens_rides_beside_the_drawn_cycle_at_the_wall_clock()
        {
            float beside = Beside;
            int rides = 0, frames = 0;
            float worst = 0f;

            for (int b = 0; b < BUILDS; b++)
            {
                foreach (float late in LATENESS)
                {
                    List<GridSolid> solids = Solids(b);
                    List<Frame> ride = Ride(BuildTime(b), late, solids, out _);
                    if (ride == null) continue;
                    rides++;

                    var cycles = new DrawnCycles(solids);
                    int filmed = Filmed(cycles, ride[0]);
                    foreach (Frame frame in ride)
                    {
                        cycles.At(filmed, frame.Time, out Vector2 rider, out _);
                        float d = Vector2.Distance(rider, new Vector2(frame.Lens.X, frame.Lens.Z));
                        worst = MathF.Max(worst, MathF.Abs(d - beside));
                        Assert.True(MathF.Abs(d - beside) < 0.05f,
                            $"built at {BuildTime(b)}, {late} s late: the lens is {d:F2} off cycle {filmed} at {frame.Time:F3}, not {beside:F2}");
                        Assert.Equal(FloorY + GridRide.HEIGHT, frame.Lens.Y, 3);
                        frames++;
                    }
                }
            }

            _output.WriteLine($"{rides} rides of {BUILDS * LATENESS.Length}, {frames} frames, the lens at most {worst:F4} off its place beside the cycle");
            Assert.True(rides >= BUILDS * LATENESS.Length * 95 / 100, $"only {rides} of {BUILDS * LATENESS.Length} reels had a ride");
        }

        [Fact]
        public void The_lens_is_never_in_a_wall_a_cycle_or_a_solid()
        {
            Assert.True(GridRide.HEIGHT > LightCycleRuns.WALL_HEIGHT && GridRide.HEIGHT > LightCycleRuns.BODY_HEIGHT,
                "the lens rides lower than a wall or a cycle");

            float nearestWall = float.PositiveInfinity, nearestCycle = float.PositiveInfinity, nearestSolid = float.PositiveInfinity;
            for (int b = 0; b < BUILDS; b++)
            {
                List<GridSolid> solids = Solids(b);
                foreach (float late in LATENESS)
                {
                    List<Frame> ride = Ride(BuildTime(b), late, solids, out _);
                    if (ride == null) continue;

                    var cycles = new DrawnCycles(solids);
                    int filmed = Filmed(cycles, ride[0]);
                    foreach (Frame frame in ride)
                    {
                        var lens = new Vector2(frame.Lens.X, frame.Lens.Z);
                        for (int i = 0; i < LightCycles.RIDERS; i++)
                        {
                            nearestWall = MathF.Min(nearestWall, cycles.WallDistance(i, frame.Time, lens));
                            if (i == filmed) continue;
                            cycles.At(i, frame.Time, out Vector2 other, out _);
                            nearestCycle = MathF.Min(nearestCycle, Vector2.Distance(lens, other));
                        }

                        foreach (GridSolid solid in solids) nearestSolid = MathF.Min(nearestSolid, solid.Distance(frame.Lens));
                        nearestSolid = MathF.Min(nearestSolid, RingOf(Grid).Distance(frame.Lens));
                    }
                }
            }

            _output.WriteLine($"nearest a wall {nearestWall:F2}, another cycle {nearestCycle:F2}, a solid {nearestSolid:F2}");
            Assert.True(nearestWall >= GridRide.WALL_CLEARANCE - 0.05f, $"the lens came {nearestWall:F2} from a wall");
            Assert.True(nearestCycle >= GridRide.CYCLE_CLEARANCE - 0.05f, $"the lens came {nearestCycle:F2} from another cycle");
            Assert.True(nearestSolid >= GridRide.SOLID_CLEARANCE - 0.05f, $"the lens came {nearestSolid:F2} from a solid");
        }

        [Fact]
        public void The_cycle_is_whole_in_the_frame_throughout()
        {
            float widest = 0f, tallest = 0f, spanMost = 0f, spanSum = 0f;
            int spans = 0;
            for (int b = 0; b < BUILDS; b++)
            {
                foreach (float late in LATENESS)
                {
                    List<GridSolid> solids = Solids(b);
                    List<Frame> ride = Ride(BuildTime(b), late, solids, out _);
                    if (ride == null) continue;

                    var cycles = new DrawnCycles(solids);
                    int filmed = Filmed(cycles, ride[0]);
                    foreach (Frame frame in ride)
                    {
                        cycles.At(filmed, frame.Time, out Vector2 middle, out Vector2 heading);
                        Matrix view = Matrix.CreateLookAt(frame.Lens, frame.Look, Vector3.Up);
                        foreach (float aspect in ASPECTS)
                        {
                            Matrix viewProjection = view * Matrix.CreatePerspectiveFieldOfView(frame.FieldOfView, aspect, 0.1f, 2000f);
                            float left = float.MaxValue, right = float.MinValue;
                            foreach (float along in new[] { -LightCycleRuns.BODY_REACH, LightCycleRuns.BODY_REACH })
                            {
                                foreach (float up in new[] { 0f, LightCycleRuns.BODY_HEIGHT })
                                {
                                    Vector2 xz = middle + heading * along;
                                    Vector4 clip = Vector4.Transform(new Vector4(xz.X, FloorY + up, xz.Y, 1f), viewProjection);
                                    Assert.True(clip.W > 0f, "the cycle is behind the lens");
                                    float x = MathF.Abs(clip.X / clip.W), y = MathF.Abs(clip.Y / clip.W);
                                    if (aspect == 4f / 3f) widest = MathF.Max(widest, x);
                                    tallest = MathF.Max(tallest, y);
                                    left = MathF.Min(left, clip.X / clip.W);
                                    right = MathF.Max(right, clip.X / clip.W);
                                    Assert.True(x < 0.9f && y < 0.9f,
                                        $"built at {BuildTime(b)}: the cycle's corner at ({x:F2}, {y:F2}) of the frame at {aspect:F2}");
                                }
                            }

                            //How much of a 16:9 frame's width the cycle spans, nose to tail
                            if (aspect != 16f / 9f) continue;
                            float span = (right - left) * 0.5f;
                            spanMost = MathF.Max(spanMost, span);
                            spanSum += span;
                            spans++;
                        }
                    }
                }
            }

            _output.WriteLine($"the cycle's corners at most {widest:F2} of a 4:3 frame's half-width, {tallest:F2} of its half-height; "
                + $"it spans {spanSum / spans:F2} of a 16:9 frame's width on average, {spanMost:F2} at most");
            Assert.True(spanMost <= CYCLE_SPAN_MOST, $"the cycle spans up to {spanMost:F2} of a 16:9 frame's width");
            Assert.True(spanSum / spans <= CYCLE_SPAN_MEAN, $"the cycle spans {spanSum / spans:F2} of a 16:9 frame's width on average");
        }

        //The most of a 16:9 frame's width the filmed cycle spans nose to tail at any frame of a ride, and on average (#1161, the
        //owner: "the motorcycles do not have much detail, so we should not show them needlessly close")
        private const float CYCLE_SPAN_MOST = 0.36f, CYCLE_SPAN_MEAN = 0.3f;

        [Fact]
        public void A_turn_is_ridden_not_snapped()
        {
            float fastest = 0f, fastestTurn = 0f, slowest = float.PositiveInfinity;
            int turns = 0, rides = 0, swift = 0;
            for (int b = 0; b < BUILDS; b++)
            {
                List<GridSolid> solids = Solids(b);
                List<Frame> ride = Ride(BuildTime(b), 0f, solids, out _);
                if (ride == null) continue;
                rides++;
                float rideTurn = 0f;

                var cycles = new DrawnCycles(solids);
                int filmed = Filmed(cycles, ride[0]);
                cycles.At(filmed, ride[0].Time, out _, out Vector2 was);
                for (int f = 1; f < ride.Count; f++)
                {
                    cycles.At(filmed, ride[f].Time, out _, out Vector2 heading);
                    if (Vector2.Dot(was, heading) < 0.5f) turns++;
                    was = heading;

                    float speed = Vector3.Distance(ride[f].Lens, ride[f - 1].Lens) / FRAME;
                    fastest = MathF.Max(fastest, speed);
                    slowest = MathF.Min(slowest, speed);

                    float yaw = MathF.Atan2(ride[f].Look.Z - ride[f].Lens.Z, ride[f].Look.X - ride[f].Lens.X)
                        - MathF.Atan2(ride[f - 1].Look.Z - ride[f - 1].Lens.Z, ride[f - 1].Look.X - ride[f - 1].Lens.X);
                    yaw = MathHelper.WrapAngle(yaw);
                    rideTurn = MathF.Max(rideTurn, MathF.Abs(MathHelper.ToDegrees(yaw)) / FRAME);
                }

                fastestTurn = MathF.Max(fastestTurn, rideTurn);
                if (rideTurn > GridRide.MAX_SWING_DEGREES + 1f) swift++;
            }

            _output.WriteLine($"{turns} turns ridden in {rides} rides; the lens at {slowest:F1} to {fastest:F1} u/s, the view turning at most "
                + $"{fastestTurn:F0} deg/s, faster than a calm ride's {GridRide.MAX_SWING_DEGREES:F0} in {swift}");
            Assert.True(turns > 20, $"only {turns} turns were ridden: the test is not exercising one");

            //A calm ride wherever one is clear: one swinging faster only where none is, and never a whip. The cycle at 24 u/s and
            //the lens swung round it at its distance: at most the fastest swing's own speed on top
            Assert.True(swift <= rides * 3 / 100, $"{swift} of {rides} rides swung faster than {GridRide.MAX_SWING_DEGREES} deg/s");
            Assert.True(fastestTurn < 110f, $"the view turned at {fastestTurn:F0} deg/s");
            Assert.True(fastest < LightCycleRuns.SPEED + MathHelper.ToRadians(fastestTurn) * Beside + 0.5f, $"the lens reached {fastest:F1} u/s");
            Assert.True(slowest > LightCycleRuns.SPEED - MathHelper.ToRadians(fastestTurn) * Beside - 0.5f, $"the lens slowed to {slowest:F1} u/s");
        }

        [Fact]
        public void A_solid_where_the_ride_would_go_is_kept_off()
        {
            int moved = 0, dropped = 0;
            for (int b = 0; b < 40; b++)
            {
                List<Frame> ride = Ride(BuildTime(b), 0f, Array.Empty<GridSolid>(), out _);
                if (ride == null) continue;

                //A tower standing where the lens is half-way through
                Vector3 at = ride[ride.Count / 2].Lens;
                var tower = new GridSolid(new Vector3(at.X, FloorY, at.Z), new Vector3(4f, 40f, 4f), false);
                List<Frame> again = Ride(BuildTime(b), 0f, new[] { tower }, out _);
                if (again == null)
                {
                    dropped++;
                    continue;
                }

                moved++;
                foreach (Frame frame in again)
                    Assert.True(tower.Distance(frame.Lens) >= GridRide.SOLID_CLEARANCE - 0.05f,
                        $"built at {BuildTime(b)}: the lens came {tower.Distance(frame.Lens):F2} from the tower planted in its way");
            }

            _output.WriteLine($"a tower in the ride's way: ridden round {moved} times, the ride left out {dropped} times");
            Assert.True(moved >= 25, $"only {moved} of 40 rides found another way past a tower");
        }

        [Fact]
        public void Coming_on_later_than_planned_chooses_again()
        {
            int clear = 0, rides = 0;
            float nearestSolid = float.PositiveInfinity;
            for (int b = 0; b < 60; b++)
            {
                foreach (float late in new[] { 3f, 9f, 31f })
                {
                    List<GridSolid> solids = Solids(b);
                    List<Frame> ride = Ride(BuildTime(b), late, solids, out _);
                    if (ride == null) continue;
                    rides++;

                    var cycles = new DrawnCycles(solids);
                    bool ok = true;
                    foreach (Frame frame in ride)
                    {
                        var lens = new Vector2(frame.Lens.X, frame.Lens.Z);
                        for (int i = 0; i < LightCycles.RIDERS; i++)
                            if (cycles.WallDistance(i, frame.Time, lens) < GridRide.WALL_CLEARANCE - 0.05f) ok = false;
                        foreach (GridSolid solid in solids) nearestSolid = MathF.Min(nearestSolid, solid.Distance(frame.Lens));
                    }

                    if (ok) clear++;
                }
            }

            //Off every solid always (chosen again off the solids alone where nothing is clear of the walls too), and off every
            //wall as a ride on time is in all but a few
            _output.WriteLine($"{clear} of {rides} rides that came on 3 to 31 s late kept off every wall; the nearest a solid {nearestSolid:F2}");
            Assert.True(nearestSolid >= GridRide.SOLID_CLEARANCE - 0.05f, $"a late ride came {nearestSolid:F2} from a solid");
            Assert.True(clear >= rides * 85 / 100, $"only {clear} of {rides} late rides kept off every wall");
        }

        //A pause a second into the ride (Escape, or the focus lost), as long as these on the wall clock; and a run at 10 fps,
        //whose shot of 2.6 seconds on the intro's clock takes 5.2 on the wall clock
        private static readonly float[] PAUSES = { 2f, 5f, 15f };
        private const float PAUSE_AT = 1f;
        private const float SLOW_FRAME = 0.1f;

        [Fact]
        public void A_pause_mid_ride_or_a_slow_frame_rate_chooses_again()
        {
            int rides = 0, clear = 0, cuts = 0;
            float nearestSolid = float.PositiveInfinity, nearestRider = float.PositiveInfinity, worstBeside = 0f;
            for (int b = 0; b < BUILDS; b++)
            {
                List<GridSolid> solids = Solids(b);
                for (int run = 0; run <= PAUSES.Length; run++)
                {
                    List<Frame> ride = run < PAUSES.Length
                        ? Ride(BuildTime(b), 0f, solids, out _, pause: PAUSES[run], pauseAt: PAUSE_AT)
                        : Ride(BuildTime(b), 0f, solids, out _, frame: SLOW_FRAME);
                    if (ride == null) continue;
                    rides++;

                    var cycles = new DrawnCycles(solids);
                    bool ok = true;
                    int was = Filmed(cycles, ride[0]);
                    foreach (Frame frame in ride)
                    {
                        //Beside a drawn cycle at every frame, whichever it is after a choice made again
                        int filmed = Filmed(cycles, frame);
                        if (filmed != was) cuts++;
                        was = filmed;
                        cycles.At(filmed, frame.Time, out Vector2 rider, out _);
                        var lens = new Vector2(frame.Lens.X, frame.Lens.Z);
                        worstBeside = MathF.Max(worstBeside, MathF.Abs(Vector2.Distance(rider, lens) - Beside));

                        var body = new Vector3(rider.X, FloorY + LightCycleRuns.BODY_HEIGHT * 0.5f, rider.Y);
                        foreach (GridSolid solid in solids)
                        {
                            nearestSolid = MathF.Min(nearestSolid, solid.Distance(frame.Lens));
                            nearestRider = MathF.Min(nearestRider, solid.Distance(body));
                        }
                        nearestSolid = MathF.Min(nearestSolid, RingOf(Grid).Distance(frame.Lens));

                        for (int i = 0; i < LightCycles.RIDERS; i++)
                            if (cycles.WallDistance(i, frame.Time, lens) < GridRide.WALL_CLEARANCE - 0.05f) ok = false;
                    }

                    if (ok) clear++;
                }
            }

            _output.WriteLine($"{rides} rides paused {string.Join(", ", PAUSES)} s a second in or played at {1f / SLOW_FRAME:F0} fps: {clear} kept "
                + $"off every wall, {cuts} cuts to another cycle; the lens {worstBeside:F4} off its place beside one at worst, the nearest a "
                + $"solid {nearestSolid:F2}, the filmed cycle's middle {nearestRider:F2} from one");
            Assert.True(rides >= BUILDS * (PAUSES.Length + 1) * 95 / 100, $"only {rides} reels had a ride");
            Assert.True(worstBeside < 0.05f, $"the lens came {worstBeside:F2} off its place beside the cycle");
            Assert.True(nearestSolid >= GridRide.SOLID_CLEARANCE - 0.05f, $"a ride came {nearestSolid:F2} from a solid after a pause or slow frames");
            Assert.True(nearestRider >= GridRide.RIDER_SOLID_CLEARANCE - 0.05f, $"the filmed cycle came {nearestRider:F2} from a solid");
            Assert.True(clear >= rides * 95 / 100, $"only {clear} of {rides} paused or slow rides kept off every wall");
        }

        /// <summary>
        /// What a ride asks of the runs it reads once it is built: planned over the longest stretch the intro plans one for, posed
        /// from its start, chosen again at one second with every keep a shot on screen tries (<c>GridIntroShots.Again</c>) and
        /// posed on, and the same again past a pause. A rider asked back further than it keeps makes its run again from its start
        /// (<see cref="LightCycleRuns.KEPT_SECONDS"/>): a <see cref="Random"/>, and every node since, 5,400 of them an hour in.
        /// </summary>
        [Fact]
        public void Choosing_again_and_posing_allocate_nothing()
        {
            float stretch = GridIntroShots.RIDE_SECONDS + GridIntroShots.RIDE_LATE;
            Assert.True(stretch + 2f * GridRide.REACH_SECONDS < LightCycleRuns.KEPT_SECONDS,
                $"a choice over {stretch} s reads {stretch + 2f * GridRide.REACH_SECONDS} s of a run, more than the {LightCycleRuns.KEPT_SECONDS} kept");

            //Two passes over the same rides, each on runs built afresh: the first untimed, so every path any ride takes has run
            //once before anything is counted (the runtime's first call of a method or of a lazily built helper allocates once,
            //which is not what a ride costs a frame); the second counted, phase by phase, so a failure names where it allocated
            int rides = 0;
            long allocated = 0;
            var where = new List<string>();
            for (int pass = 0; pass < 2; pass++)
            {
                rides = 0;
                for (int b = 0; b < BUILDS; b += 4)
                {
                    float start = BuildTime(b) + 3.2f;
                    List<GridSolid> solids = Solids(b);
                    GridRide ride = GridRide.Plan(new LightCycleRuns(Grid, solids), FloorY, solids, RingOf(Grid), start, start + stretch,
                        GridIntroShots.RIDE_SECONDS);
                    if (ride == null) continue;
                    rides++;

                    //The ride's last allocation, then this thread's allocation buffer emptied by a gen0 collection, so the phases
                    //count from a buffer with nothing left in it. ⚠ A background GC, set off by the tests running beside this one,
                    //suspends the runtime and retires a thread's partly used buffer, and GetAllocatedBytesForCurrentThread counts
                    //its unused remainder as allocated: 128 to 8,104 bytes in phases that allocate nothing (#1136, the
                    //linux-arm64 runner; never with DOTNET_gcConcurrent=0, never from an empty buffer). An allocation a phase
                    //really makes still counts in full.
                    var phases = new long[PHASES.Length * 2];
                    GC.Collect(0, GCCollectionMode.Forced, blocking: true);
                    PlayChosenAgain(ride, start, stretch, phases);
                    if (pass == 0) continue;
                    for (int p = 0; p < phases.Length; p++)
                    {
                        allocated += phases[p];
                        if (phases[p] != 0) where.Add($"build {b}, {PHASES[p % PHASES.Length]} (pass {p / PHASES.Length + 1}): {phases[p]} bytes");
                    }
                }
            }

            _output.WriteLine($"{rides} rides chosen again twice each: {allocated} bytes allocated");
            foreach (string line in where) _output.WriteLine(line);
            Assert.True(rides >= 10, $"only {rides} reels had a ride");
            Assert.True(allocated == 0L, $"{allocated} bytes allocated: {string.Join("; ", where)}");
        }

        private static readonly string[] PHASES =
            { "posing a second", "choosing calm, staying", "choosing clear, staying", "choosing calm", "choosing clear", "choosing safe", "posing on" };

        //A second of frames from `start`, every choice GridIntroShots.Again can make at the second it ends on, a second more, a
        //pause; twice. Writes the bytes each phase allocated on this thread into `phases`, PHASES' order, twice: allocating
        //nothing itself, so the buffer the caller emptied stays empty unless a phase allocates
        private static void PlayChosenAgain(GridRide ride, float start, float stretch, long[] phases)
        {
            float now = start;
            for (int pass = 0; pass < 2; pass++)
            {
                int p = pass * PHASES.Length;
                long mark = GC.GetAllocatedBytesForCurrentThread();
                for (int f = 0; f < 60; f++, now += FRAME) ride.Pose(now, out _, out _);
                phases[p++] = Took(ref mark);

                float until = now + stretch;
                ride.Choose(now, until, GridIntroShots.RIDE_SECONDS, GridRide.Keep.Calm, stay: true);
                phases[p++] = Took(ref mark);
                ride.Choose(now, until, GridIntroShots.RIDE_SECONDS, GridRide.Keep.Clear, stay: true);
                phases[p++] = Took(ref mark);
                ride.Choose(now, until, GridIntroShots.RIDE_SECONDS, GridRide.Keep.Calm);
                phases[p++] = Took(ref mark);
                ride.Choose(now, until, GridIntroShots.RIDE_SECONDS, GridRide.Keep.Clear);
                phases[p++] = Took(ref mark);
                ride.Choose(now, until, GridIntroShots.RIDE_SECONDS, GridRide.Keep.Safe);
                phases[p++] = Took(ref mark);

                for (int f = 0; f < 60; f++, now += FRAME) ride.Pose(now, out _, out _);
                phases[p] = Took(ref mark);
                now += PAUSES[^1];
            }
        }

        //The bytes allocated on this thread since `mark`, and the mark moved to now
        private static long Took(ref long mark)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            long took = now - mark;
            mark = now;
            return took;
        }

        [Fact]
        public void The_reel_is_the_grid_the_cycle_the_cube_the_ring()
        {
            int full = 0, second = 0;
            for (int b = 0; b < 40; b++)
            {
                float now = BuildTime(b);
                IntroShot[] reel = GridIntroShots.Shots(Grid, Solids(b), RingOf(Grid), now, () => now, new Random(b));
                IntroShot[] without = GridIntroShots.Shots(Grid, Solids(b), RingOf(Grid), now, null, new Random(b));
                Assert.NotNull(reel);

                var names = new List<string>();
                float seconds = 0f;
                foreach (IntroShot shot in reel)
                {
                    names.Add(shot.Name);
                    seconds += shot.Seconds;
                }

                if (names.Count != 4) _output.WriteLine($"built at {now}: {string.Join(", ", names)}");
                if (names.Count == 4)
                {
                    full++;
                    Assert.Equal(16.5f, seconds + ChapterIntro.ARRIVAL_SECONDS, 2);

                    //The cycle second, after the grid's crane; later only where no cycle is clear for the second place's seconds
                    Assert.Equal("the grid", names[0]);
                    if (names[1] == "the cycle") second++;
                    else Assert.Null(GridRide.Plan(new LightCycleRuns(Grid, Solids(b)), FloorY, Solids(b), RingOf(Grid), now + reel[0].Seconds,
                        now + reel[0].Seconds + GridIntroShots.RIDE_SECONDS + GridIntroShots.RIDE_LATE, GridIntroShots.RIDE_SECONDS,
                        GridRide.Keep.Calm));
                    names.Remove("the cycle");
                    Assert.Equal(new[] { "the grid", "the cube", "the ring" }, names);
                }

                //The other three shots are the ones they were without the ride: it draws none of their rolls
                int k = 0;
                foreach (IntroShot shot in reel)
                {
                    if (shot.Name == "the cycle") continue;
                    Assert.Equal(without[k].Name, shot.Name);
                    foreach (float t in new[] { 0f, 0.5f, 1f })
                    {
                        shot.Pose(t, out Vector3 lens, out Vector3 look);
                        without[k].Pose(t, out Vector3 lens0, out Vector3 look0);
                        Assert.Equal(lens0, lens);
                        Assert.Equal(look0, look);
                    }

                    k++;
                }
            }

            _output.WriteLine($"{full} of 40 reels had all four shots, {second} with the cycle second");
            Assert.True(full >= 36, $"only {full} of 40 reels had all four shots");
        }
    }
}
