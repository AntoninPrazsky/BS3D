using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The grid's light cycles turn only right angles (#1069, the owner: "they sometimes turn by 180 degrees - only by 90").
    /// A rider makes its route node by node; where the move it rolled would leave the ring it tries another, and the first
    /// picker reversed (the way it came) after trying only two of the three, so a cycle at the ring's edge turned round.
    /// And they never ride into a solid (#1136): a cube near the solids' inner radius reaches in among them, and a run that
    /// knew only the ring rode a cycle and its wall through one (sceneseed=18, the blue cycle 25.5 s into the clock). And they
    /// ride long straight runs between turns (#1162, the owner: they turned too often): half the runs were a single segment.
    /// </summary>
    public class LightCycleRouteTests
    {
        private readonly ITestOutputHelper _output;

        public LightCycleRouteTests(ITestOutputHelper output) => _output = output;

        //Many riders and a long run each: a reversal at the ring's edge is rare per node and common per minute
        private const int SEEDS = 200;
        private const float RUN = 10000f;
        private const float STEP = 4f;

        private static readonly GridSceneConfig Grid = new();

        [Fact]
        public void No_rider_ever_reverses()
        {
            int reversals = 0, turns = 0;
            for (int seed = 0; seed < SEEDS; seed++)
            {
                var rider = new LightCycles.Rider(seed * 7919 + 13, LightCycles.Lattice.Bare);
                rider.At(0f, out _, out Vector2 previous);
                for (float d = STEP; d < RUN; d += STEP)
                {
                    rider.At(d, out _, out Vector2 heading);
                    float dot = Vector2.Dot(previous, heading);
                    if (dot < -0.5f) reversals++;
                    else if (dot < 0.5f) turns++;
                    previous = heading;
                }
            }

            Assert.True(turns > 1000, $"the riders turned only {turns} times: the test is not exercising the picker");
            Assert.Equal(0, reversals);
        }

        //The launch's rolls the solids are placed on (sceneseed=), the Game's from any int: the first two hundred and a few
        //from the far end, where the config's seed plus the roll wraps round
        private static IEnumerable<int> SceneSeeds()
        {
            for (int seed = 0; seed < 200; seed++) yield return seed;
            foreach (int seed in new[] { int.MaxValue, int.MaxValue - 392, int.MinValue, -1, 1_234_567_891 }) yield return seed;
        }

        //Each drawn rider's run over the first 25 minutes on the wall clock
        private const float CLOCK = 1500f;

        //Whether a solid's footprint, the box seen from above, comes within `reach` of the axis
        private static bool Within(GridSolid solid, float reach) => Footprint(solid, Vector2.Zero) < reach;

        /// <summary>The solids whose footprint comes near the ring: the only ones a cycle could reach. A run stays inside
        /// `RING_MAX`, so a footprint further out than that and `SOLID_CLEARANCE` keeps the clearance from every point of a
        /// wall, and from a body's corner all but the few hundredths its corner reaches past `BODY_REACH`, which the body's
        /// assertion allows.</summary>
        private static List<GridSolid> Near(List<GridSolid> solids) =>
            solids.FindAll(solid => Within(solid, LightCycles.RING_MAX + LightCycles.SOLID_CLEARANCE));

        /// <summary>How far a point on the floor (world X, Z) stands from a solid's footprint seen from above; negative inside it,
        /// by how far in.</summary>
        private static float Footprint(GridSolid solid, Vector2 p)
        {
            float dx = MathF.Abs(p.X - solid.BaseCentre.X) - solid.Size.X * 0.5f;
            float dz = MathF.Abs(p.Y - solid.BaseCentre.Z) - solid.Size.Z * 0.5f;
            if (dx <= 0f && dz <= 0f) return MathF.Max(dx, dz);
            return new Vector2(MathF.Max(dx, 0f), MathF.Max(dz, 0f)).Length();
        }

        //How far a segment on the floor stands from a solid's footprint, sampled every half unit and at both ends
        private static float Footprint(GridSolid solid, Vector2 a, Vector2 b)
        {
            int samples = Math.Max(1, (int)(Vector2.Distance(a, b) * 2f));
            float nearest = float.PositiveInfinity;
            for (int k = 0; k <= samples; k++) nearest = MathF.Min(nearest, Footprint(solid, Vector2.Lerp(a, b, k / (float)samples)));
            return nearest;
        }

        [Fact]
        public void No_cycle_or_wall_ever_enters_a_solid()
        {
            float nearestBody = float.PositiveInfinity, nearestWall = float.PositiveInfinity;
            int layouts = 0, reaching = 0, walls = 0;
            var trail = new VertexPositionTexture[LightCycles.TRAIL_POINTS * 2];
            float floorY = Grid.Terrain.LevelY;
            string worstBody = "", worstWall = "";

            foreach (int sceneSeed in SceneSeeds())
            {
                layouts++;
                List<GridSolid> placed = GridLayout.PlaceSolids(Grid, sceneSeed, null, out _);
                if (placed.Exists(solid => Within(solid, LightCycles.RING_MAX))) reaching++;

                //The lattice of every solid, as the backdrop builds it; only the near ones measured against
                var lattice = new LightCycles.Lattice(placed);
                List<GridSolid> solids = Near(placed);
                if (solids.Count == 0) continue;

                for (int i = 0; i < LightCycles.RIDERS; i++)
                {
                    //Made as LightCycles makes the ones it draws
                    var rider = new LightCycles.Rider(LightCycles.RiderSeed(Grid, i), lattice);
                    float run = CLOCK * LightCycles.SPEED;
                    for (float d = 0f; d < run; d += 2f)
                    {
                        //The body: its plan's four corners, the run's point BODY_REACH either way along its heading and
                        //BODY_HALF_WIDTH either side, every two units of the run (every node among them)
                        rider.At(d, out Vector2 middle, out Vector2 heading);
                        var side = new Vector2(-heading.Y, heading.X);
                        foreach (GridSolid solid in solids)
                        {
                            for (int corner = 0; corner < 4; corner++)
                            {
                                Vector2 p = middle + heading * ((corner & 1) == 0 ? LightCycles.BODY_REACH : -LightCycles.BODY_REACH)
                                    + side * ((corner & 2) == 0 ? LightCycles.BODY_HALF_WIDTH : -LightCycles.BODY_HALF_WIDTH);
                                float gap = Footprint(solid, p);
                                if (gap < nearestBody)
                                {
                                    nearestBody = gap;
                                    worstBody = $"sceneseed={sceneSeed}, cycle {i} at {d / LightCycles.SPEED:F2} s";
                                }
                            }
                        }

                        //The wall as Trail writes it for the frame, every four nodes (it is seven long): each segment of its foot
                        if (d % (4f * LightCycles.LATTICE) != 0f) continue;
                        int count = rider.Trail(d, trail, 0, floorY);
                        walls++;
                        for (int k = 2; k < count; k += 2)
                        {
                            var a = new Vector2(trail[k - 2].Position.X, trail[k - 2].Position.Z);
                            var b = new Vector2(trail[k].Position.X, trail[k].Position.Z);
                            foreach (GridSolid solid in solids)
                            {
                                float gap = Footprint(solid, a, b);
                                if (gap < nearestWall)
                                {
                                    nearestWall = gap;
                                    worstWall = $"sceneseed={sceneSeed}, cycle {i} at {d / LightCycles.SPEED:F2} s";
                                }
                            }
                        }
                    }
                }
            }

            _output.WriteLine($"{layouts} layouts, {reaching} with a solid's footprint inside the ring, {CLOCK:F0} s of each cycle's run, "
                + $"{walls} walls: the nearest a body came to a footprint {nearestBody:F2} ({worstBody}), a wall {nearestWall:F2} ({worstWall})");
            Assert.True(reaching >= 40, $"only {reaching} of {layouts} layouts put a solid into the ring: the test is not exercising one");
            Assert.True(nearestBody >= LightCycles.SOLID_CLEARANCE - LightCycles.BODY_REACH - 0.05f,
                $"a cycle's body came {nearestBody:F2} from a solid's footprint ({worstBody})");
            Assert.True(nearestWall >= LightCycles.SOLID_CLEARANCE - 0.05f, $"a wall came {nearestWall:F2} from a solid's footprint ({worstWall})");
        }

        [Fact]
        public void Among_the_solids_no_rider_turns_back_or_leaves_the_ring()
        {
            int reversals = 0, turns = 0, outside = 0;
            float nearest = float.PositiveInfinity, furthest = 0f;
            foreach (int sceneSeed in SceneSeeds())
            {
                var lattice = new LightCycles.Lattice(GridLayout.PlaceSolids(Grid, sceneSeed, null, out _));
                for (int i = 0; i < LightCycles.RIDERS; i++)
                {
                    var rider = new LightCycles.Rider(LightCycles.RiderSeed(Grid, i), lattice);
                    rider.At(0f, out _, out Vector2 previous);
                    for (float d = STEP; d < CLOCK * LightCycles.SPEED; d += STEP)
                    {
                        rider.At(d, out Vector2 position, out Vector2 heading);
                        float dot = Vector2.Dot(previous, heading);
                        if (dot < -0.5f) reversals++;
                        else if (dot < 0.5f) turns++;
                        previous = heading;

                        float r = position.Length();
                        nearest = MathF.Min(nearest, r);
                        furthest = MathF.Max(furthest, r);
                        if (r < LightCycles.RING_MIN - 0.01f || r > LightCycles.RING_MAX + 0.01f) outside++;
                    }
                }
            }

            _output.WriteLine($"{turns} turns, {reversals} reversals; the cycles {nearest:F1} to {furthest:F1} from the axis");
            Assert.True(turns > 10000, $"the riders turned only {turns} times: the test is not exercising the picker");
            Assert.Equal(0, reversals);
            Assert.Equal(0, outside);
        }

        /// <summary>Adds to <paramref name="runs"/> every straight run between two turns of <paramref name="rider"/>'s first
        /// <paramref name="segments"/> segments, in segments: the run it starts on (begun before any turn) and the one it ends on
        /// (not yet ended) left out. Each segment's heading read at its middle.</summary>
        private static void StraightRuns(LightCycles.Rider rider, int segments, List<int> runs)
        {
            rider.At(LightCycles.LATTICE * 0.5f, out _, out Vector2 was);
            int length = 1;
            bool begun = false;
            for (int k = 1; k < segments; k++)
            {
                rider.At((k + 0.5f) * LightCycles.LATTICE, out _, out Vector2 heading);
                if (Vector2.Dot(was, heading) < 0.5f)
                {
                    if (begun) runs.Add(length);
                    begun = true;
                    length = 1;
                }
                else length++;

                was = heading;
            }
        }

        //At 0.6 straight on and no shortest run the riders ran 2.1 segments between turns (1.4 s) and 46 % of the runs were one
        //segment, a turn a node after a turn (#1162)
        private const float MEAN_RUN_SEGMENTS = 3.5f;
        private const int SHORTEST_RUN_SEGMENTS = 2;

        [Fact]
        public void Riders_run_long_straight_runs_between_turns()
        {
            float seconds = LightCycles.LATTICE / LightCycles.SPEED;
            var runs = new List<int>[] { new(), new() };
            for (int seed = 0; seed < SEEDS; seed++)
                StraightRuns(new LightCycles.Rider(seed * 7919 + 13, LightCycles.Lattice.Bare), (int)(RUN / LightCycles.LATTICE), runs[0]);
            foreach (int sceneSeed in SceneSeeds())
            {
                var lattice = new LightCycles.Lattice(GridLayout.PlaceSolids(Grid, sceneSeed, null, out _));
                for (int i = 0; i < LightCycles.RIDERS; i++)
                    StraightRuns(new LightCycles.Rider(LightCycles.RiderSeed(Grid, i), lattice), (int)(CLOCK * LightCycles.SPEED / LightCycles.LATTICE), runs[1]);
            }

            string[] what = { $"{SEEDS} riders on the bare ring, {RUN:F0} units each", $"the drawn riders of {SceneSeeds().Count()} layouts, {CLOCK:F0} s each" };
            for (int k = 0; k < runs.Length; k++)
            {
                List<int> all = runs[k];
                double mean = all.Average();
                int shortest = all.Min(), single = all.Count(r => r == 1), twoSegments = all.Count(r => r == 2);
                _output.WriteLine($"{what[k]}: {all.Count} runs between turns, {mean:F2} segments on average ({mean * seconds:F2} s), the shortest "
                    + $"{shortest} ({shortest * seconds:F2} s), the longest {all.Max()}; {single * 100.0 / all.Count:F1} % one segment, "
                    + $"{twoSegments * 100.0 / all.Count:F1} % two");
                Assert.True(all.Count > 10000, $"{what[k]}: only {all.Count} runs: the test is not exercising the picker");
                Assert.True(shortest >= SHORTEST_RUN_SEGMENTS, $"{what[k]}: a run of {shortest} segments between two turns");
                Assert.True(mean >= MEAN_RUN_SEGMENTS, $"{what[k]}: {mean:F2} segments between turns on average");
            }
        }

        [Fact]
        public void A_run_is_the_same_however_and_whenever_it_is_asked()
        {
            //A rider asked every frame at 60 fps; one on a lattice built again from the same solids asked in jumps forward and
            //back, as a host filming one asks ahead of the frame and a clock that goes back makes the run again; and the runs a
            //host films from (#1116), on the placement made a third time
            var random = new Random(5);
            int asked = 0;
            for (int sceneSeed = 0; sceneSeed < 40; sceneSeed++)
            {
                List<GridSolid> solids = GridLayout.PlaceSolids(Grid, sceneSeed, null, out _);
                var runs = new LightCycleRuns(Grid, GridLayout.PlaceSolids(Grid, sceneSeed, null, out _));
                for (int i = 0; i < LightCycles.RIDERS; i++)
                {
                    var framed = new LightCycles.Rider(LightCycles.RiderSeed(Grid, i), new LightCycles.Lattice(solids));
                    var jumped = new LightCycles.Rider(LightCycles.RiderSeed(Grid, i),
                        new LightCycles.Lattice(GridLayout.PlaceSolids(Grid, sceneSeed, null, out _)));
                    for (int frame = 0; frame < 60 * 120; frame++)
                    {
                        float time = frame / 60f;
                        framed.At(time * LightCycles.SPEED, out Vector2 p, out Vector2 h);
                        if (frame % 7 != 0) continue;

                        jumped.At(MathF.Max(time + (float)(random.NextDouble() * 120.0 - 60.0), 0f) * LightCycles.SPEED, out _, out _);
                        jumped.At(time * LightCycles.SPEED, out Vector2 q, out Vector2 g);
                        Assert.Equal(p, q);
                        Assert.Equal(h, g);

                        runs.At(i, time, out Vector2 r, out Vector2 k);
                        Assert.Equal(p, r);
                        Assert.Equal(h, k);
                        asked++;
                    }
                }
            }

            _output.WriteLine($"{asked} seconds asked three ways, every answer the same");
        }
    }
}
