using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The bench under the meadow's old oak (#891): the owner could hardly find it, because it was built at one unit to a
    /// metre in a scene whose metre is two (the fence's, #698, and the bales', #864). What is checked here is what can be
    /// stated without a device: the mesh's size against the tree it stands by, its bounding sphere (the culling and the
    /// sun's shadow casting read it), that it stands clear of the bare ground and the roots at the foot of the oak, and
    /// (#1022, when the owner found it crude, "it does not look as if it would be good to sit on") that it is a bench: a
    /// seat of slats at a seat's height, a back that leans, arms and legs, in two meshes the Potato path still draws as
    /// themselves. How it reads on the screen is the eye's.
    /// </summary>
    public class MeadowBenchTests
    {
        //The knoll oak's trunk at its widest, as MeadowTreeMesh grows it (the oak's 1.3 radius, the seed's 0.92..1.08)
        //and MeadowScatter plants it
        private const float OAK_TRUNK_RADIUS_MAX = 1.3f * 1.08f * MeadowScatter.KNOLL_OAK_SCALE;

        //Its longest surface root, from the same figures: a root reaches 3.2 trunk radii out from the axis at the most
        private const float OAK_ROOT_REACH_MAX = OAK_TRUNK_RADIUS_MAX * 3.2f;

        private static (float X, float Z) Knoll() => MeadowPath.PathEnd(new MeadowSceneConfig());

        [Fact]
        public void TheBenchIsAsLongAsTheOaksTrunkIsThick()
        {
            //The fault: a bench shorter than the trunk beside it is thick (1.9 against 2.75 to 3.2)
            var (vertices, _, _) = MeadowScatter.BenchGeometry();
            float min = float.MaxValue, max = float.MinValue;
            foreach (var vertex in vertices)
            {
                min = Math.Min(min, vertex.Position.X);
                max = Math.Max(max, vertex.Position.X);
            }

            Assert.True(max - min >= 2f * OAK_TRUNK_RADIUS_MAX,
                $"the bench is {max - min:F2} long and the oak's trunk is up to {2f * OAK_TRUNK_RADIUS_MAX:F2} thick");
        }

        //The bench in the scene's metre: its own units times the mesh's scale (the fence's, MeadowScatter.BENCH_SCALE)
        private const float K = 2f;

        /// <summary>
        /// What the owner saw (#1022): a seat of two boards on four sticks, a plank for a back, no arms. A person sits on a seat
        /// of slats with gaps, 0.40 to 0.50 up (in its own metre) and over 0.4 deep, whose plane tips back, against a back that
        /// leans away and stands near a metre.
        /// </summary>
        [Fact]
        public void TheBenchIsASeatOfFiveSlatsAndABackOfThreeYouCouldSitOn()
        {
            var (wood, _, _) = MeadowScatter.BenchWoodGeometry();

            //Slats are separate solids: a gap between each, the sky showing through (welded positions, pieces counted)
            Assert.Equal(8, CountPieces(MeadowScatter.BenchWoodGeometry()));

            //The seat's top faces (their normals lean back a few degrees), the back's faces (they lean up and forward)
            List<VertexPositionNormalTexture> seatTops = wood.Where(p => p.Normal.Y > 0.99f).ToList();
            List<VertexPositionNormalTexture> backFaces = wood.Where(p => p.Normal.Z > 0.9f && p.Normal.Y > 0.15f).ToList();
            Assert.NotEmpty(seatTops);
            Assert.NotEmpty(backFaces);

            float seatHigh = seatTops.Max(p => p.Position.Y) / K;
            Assert.InRange(seatHigh, 0.40f, 0.50f);
            float depth = (seatTops.Max(p => p.Position.Z) - seatTops.Min(p => p.Position.Z)) / K;
            Assert.True(depth >= 0.40f, $"the seat is {depth:F2} deep");

            //Tipped back: the lowest of the seat's top is at its back
            Assert.True(seatTops.MinBy(p => p.Position.Y).Position.Z < 0f, "the seat's lowest edge is at its front");
            Assert.True(seatTops.Min(p => p.Normal.Z) < -0.05f, "no seat face leans back");

            //Leans away: from its foot to its top the back goes back at least 8 degrees off the vertical, and it stands near a metre
            VertexPositionNormalTexture backTop = backFaces.MaxBy(p => p.Position.Y), backFoot = backFaces.MinBy(p => p.Position.Y);
            float lean = MathHelper.ToDegrees(MathF.Atan2(backFoot.Position.Z - backTop.Position.Z, backTop.Position.Y - backFoot.Position.Y));
            Assert.True(lean >= 8f, $"the back leans {lean:F1} degrees from the vertical");
            Assert.InRange(backFaces.Max(p => p.Position.Y) / K, 0.80f, 1.0f);
        }

        /// <summary>The cast ends: each carries an arm that comes forward at hand height over the seat, and legs that reach the ground.</summary>
        [Fact]
        public void TheBenchHasArmsOverTheSeatAndLegsToTheGround()
        {
            var (iron, _, _) = MeadowScatter.BenchIronGeometry();

            foreach (float side in new[] { -1f, 1f })
            {
                List<VertexPositionNormalTexture> end = iron.Where(p => MathF.Sign(p.Position.X) == side).ToList();
                //An arm: iron at the front of the seat (z over a quarter metre) 0.18 to 0.30 over its front edge (0.47)
                Assert.Contains(end, p => p.Position.Z / K > 0.25f && p.Position.Y / K > 0.47f + 0.18f && p.Position.Y / K < 0.47f + 0.30f);
                //Legs: iron below the origin at the front and at the back (the feet are buried), and none above a metre
                Assert.Contains(end, p => p.Position.Y < 0f && p.Position.Z > 0f);
                Assert.Contains(end, p => p.Position.Y < 0f && p.Position.Z < 0f);
                Assert.True(end.Max(p => p.Position.Y) / K < 1.0f, "an end stands over a metre");
            }
        }

        /// <summary>
        /// Each mesh is drawn by the Potato path as itself while it is under 1,200 indices (<c>PotatoBackdrop</c>'s
        /// <c>PROXY_OVER_VERTICES</c>) and as an ellipsoid of its bounding sphere over it: a bench that stood there as an egg is
        /// the fault the detail must not bring.
        /// </summary>
        [Fact]
        public void EachMeshOfTheBenchIsUnderThePotatoPathsBlobLimit()
        {
            Assert.InRange(MeadowScatter.BenchWoodGeometry().Indices.Count, 1, 1200);
            Assert.InRange(MeadowScatter.BenchIronGeometry().Indices.Count, 1, 1200);
        }

        //How many separate solids a mesh is, its vertices welded by position: a slat or a rod each
        private static int CountPieces((List<VertexPositionNormalTexture> Vertices, List<short> Indices, BoundingSphere Bounds) mesh)
        {
            var weld = new Dictionary<(int, int, int), int>();
            int[] id = new int[mesh.Vertices.Count];
            for (int i = 0; i < id.Length; i++)
            {
                Vector3 p = mesh.Vertices[i].Position;
                var key = ((int)MathF.Round(p.X * 1000f), (int)MathF.Round(p.Y * 1000f), (int)MathF.Round(p.Z * 1000f));
                if (!weld.TryGetValue(key, out id[i])) weld[key] = id[i] = weld.Count;
            }

            int[] parent = Enumerable.Range(0, weld.Count).ToArray();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            for (int t = 0; t + 2 < mesh.Indices.Count; t += 3)
            {
                parent[Find(id[mesh.Indices[t]])] = Find(id[mesh.Indices[t + 1]]);
                parent[Find(id[mesh.Indices[t + 1]])] = Find(id[mesh.Indices[t + 2]]);
            }

            return mesh.Indices.Select(i => Find(id[i])).Distinct().Count();
        }

        //=== The feet (#1022): "the bench has one leg in the air and does not come out of the ground ... a person sitting on it
        //would tip over". The meadow's ground is not level under the bench, so each leg must reach the ground under it.

        //The ground a foot is held against, both ways the planting measures it: the exact field it stands on, and the surface
        //the grid draws between its vertices. The desktop's grid, and the Potato path's (64 cells of 15 over +-480, uncut)
        private static readonly MeadowSceneConfig Config = new();

        private static float Exact(float x, float z) => TerrainMirror.Meadow(x, z, Config);
        private static float DrawnOnDesktop(float x, float z) => TerrainMirror.OnGrid(
            (px, pz) => TerrainMirror.MeadowGround(px, pz, Config), MeadowBackdrop.MEADOW_GRID_N, MeadowBackdrop.MEADOW_EXTENT, x, z);
        private static float UncutField(float x, float z) => TerrainMirror.MeadowGround(x, z, Config);
        private static float DrawnOnPotato(float x, float z) => TerrainMirror.OnGrid(UncutField, 65, 960f, x, z);

        /// <summary>
        /// How far each foot's bottom stands under the ground (the lowest of the two surfaces) at the very vertices of its
        /// bottom ring, as the planting places the mesh: positive is buried, negative is a foot in the air. In
        /// <see cref="MeadowBenchGeometry.FootSites"/>'s order. <paramref name="feet"/> is what the planting asks the rods for.
        /// </summary>
        private static float[] Burial(Func<float, float, float> standOn, Func<float, float, float> drawn, float[] feet)
        {
            (float knollX, float knollZ) = MeadowPath.PathEnd(Config);
            (float bx, float bz, float yaw) = MeadowScatter.BenchStand(knollX, knollZ);
            float standY = standOn(bx, bz);
            Matrix world = MeadowScatter.BenchWorld(knollX, knollZ, standY);

            var (iron, _, _) = MeadowScatter.BenchIronGeometry(feet);
            (float X, float Z)[] sites = MeadowBenchGeometry.FootSites(K);
            float reach = 1.5f * MeadowBenchGeometry.FootRadius(K);

            var burial = new float[sites.Length];
            for (int i = 0; i < sites.Length; i++)
            {
                //The foot's vertices: near its site horizontally and down at the bottom of the rod (the rod's next ring is 0.2 up)
                List<VertexPositionNormalTexture> near = iron.Where(p =>
                    MathF.Abs(p.Position.X - sites[i].X) < reach && MathF.Abs(p.Position.Z - sites[i].Z) < reach && p.Position.Y < 0.15f).ToList();
                Assert.NotEmpty(near);
                float lowest = near.Min(p => p.Position.Y);
                float least = float.MaxValue;
                foreach (VertexPositionNormalTexture p in near.Where(p => p.Position.Y < lowest + 0.02f))
                {
                    Vector3 at = Vector3.Transform(p.Position, world);
                    least = MathF.Min(least, MathF.Min(standOn(at.X, at.Z), drawn(at.X, at.Z)) - at.Y);
                }

                burial[i] = least;
            }

            return burial;
        }

        /// <summary>
        /// The box bench as origin/main built it before #1022, its four legs as they were: boxes 0.1 across ending 0.4 under the
        /// bench's origin (0.45 under the ground there), at x of 1.52 and z of 0.28 and -0.6, the bench 6.6 from the trunk. The
        /// owner: "one leg in the air". The check seen to fire on that code (BestPractices section 10): on the desktop's ground a
        /// foot is in the air, or buried by less than half of what it must be.
        /// </summary>
        [Fact]
        public void TheLegsOfTheBoxBenchLeftAFootInTheAirOnTheKnoll()
        {
            (float knollX, float knollZ) = MeadowPath.PathEnd(Config);
            float toArena = MathF.Atan2(-knollZ, -knollX), side = toArena + 0.55f;
            float bx = knollX + MathF.Cos(side) * 6.6f, bz = knollZ + MathF.Sin(side) * 6.6f, yaw = MathF.PI * 0.5f - toArena;
            float bottom = Exact(bx, bz) - 0.05f - 0.4f;
            Matrix turn = Matrix.CreateRotationY(yaw);

            var burial = new List<float>();
            foreach (float x in new[] { -1.52f, 1.52f })
                foreach (float z in new[] { 0.28f, -0.6f })
                {
                    float least = float.MaxValue;
                    foreach ((float dx, float dz) in new[] { (-0.1f, -0.1f), (0.1f, -0.1f), (-0.1f, 0.1f), (0.1f, 0.1f), (0f, 0f) })
                    {
                        Vector3 at = Vector3.Transform(new Vector3(x + dx, 0f, z + dz), turn);
                        least = MathF.Min(least, MathF.Min(Exact(bx + at.X, bz + at.Z), DrawnOnDesktop(bx + at.X, bz + at.Z)) - bottom);
                    }

                    burial.Add(least);
                }

            Assert.True(burial.Min() < MeadowScatter.FOOT_BURY * 0.5f,
                $"every leg of the box bench was buried by at least {MeadowScatter.FOOT_BURY * 0.5f}: {string.Join(", ", burial.Select(b => b.ToString("F3")))}");
        }

        /// <summary>
        /// Each of the four feet is seated on the ground under it: its bottom under the lowest of the exact field and the surface
        /// the grid draws, by at least the planting's burial less a hundredth (a rod's disc stands on a slope). The desktop's
        /// ground, and the Potato path's uncut field on its 15-unit grid.
        /// </summary>
        [Fact]
        public void EveryFootOfTheBenchIsBuriedInTheGroundUnderIt()
        {
            (float knollX, float knollZ) = MeadowPath.PathEnd(Config);
            float[] desktop = MeadowScatter.BenchFeet(knollX, knollZ, Exact, DrawnOnDesktop);
            float[] burialDesktop = Burial(Exact, DrawnOnDesktop, desktop);
            Assert.All(burialDesktop, b => Assert.True(b >= MeadowScatter.FOOT_BURY - 0.01f, $"a foot is buried by {b:F3}: {string.Join(", ", burialDesktop.Select(x => x.ToString("F3")))}"));

            float[] potato = MeadowScatter.BenchFeet(knollX, knollZ, UncutField, DrawnOnPotato);
            float[] burialPotato = Burial(UncutField, DrawnOnPotato, potato);
            Assert.All(burialPotato, b => Assert.True(b >= MeadowScatter.FOOT_BURY - 0.01f, $"a foot on the Potato path is buried by {b:F3}: {string.Join(", ", burialPotato.Select(x => x.ToString("F3")))}"));
        }

        /// <summary>
        /// The seat stays level and the legs are made as long as the ground asks, but no longer than a leg can be made
        /// (<c>FOOT_LOWEST</c>): a foot asked for deeper would hang there. And the iron's own sphere, which the culling and the
        /// sun's map read, holds the legs as made, on either path's ground.
        /// </summary>
        [Fact]
        public void NoLegIsAskedForLongerThanCanBeMadeAndTheSphereHoldsTheLongest()
        {
            (float knollX, float knollZ) = MeadowPath.PathEnd(Config);
            foreach (float[] feet in new[] { MeadowScatter.BenchFeet(knollX, knollZ, Exact, DrawnOnDesktop),
                                             MeadowScatter.BenchFeet(knollX, knollZ, UncutField, DrawnOnPotato) })
            {
                Assert.All(feet, f => Assert.True(f >= MeadowBenchGeometry.FOOT_LOWEST * K, $"a foot is asked for {f:F3} under the origin"));

                var (iron, _, bounds) = MeadowScatter.BenchIronGeometry(feet);
                Assert.All(iron, p => Assert.True(Vector3.Distance(p.Position, bounds.Center) <= bounds.Radius, $"a vertex at {p.Position} is outside the sphere"));
            }
        }

        [Fact]
        public void TheBenchsIronIsTwoEndsOfFourRodsEach()
        {
            Assert.Equal(8, CountPieces(MeadowScatter.BenchIronGeometry()));
        }

        [Fact]
        public void TheBoundingSphereHoldsTheWholeBenchAndNotMuchMore()
        {
            //Scaling the mesh and not its sphere culls a bench that is in view (and drops its shadow) at the edge of the
            //frame; a sphere left too big keeps drawing it when it is far off. Every vertex inside, the farthest one near the rim.
            var (vertices, _, bounds) = MeadowScatter.BenchGeometry();
            float farthest = 0f;
            foreach (var vertex in vertices)
                farthest = Math.Max(farthest, Vector3.Distance(vertex.Position, bounds.Center));

            Assert.True(farthest <= bounds.Radius, $"a vertex lies {farthest:F2} from the sphere's centre, past its radius {bounds.Radius:F2}");
            Assert.True(farthest >= 0.8f * bounds.Radius, $"the farthest vertex is {farthest:F2} out in a sphere of {bounds.Radius:F2}");
        }

        [Fact]
        public void TheBenchStandsPastTheTroddenGroundAndTheRootsOfTheOak()
        {
            //The back corner is the bench's nearest part to the trunk: measured on the real mesh, placed as the planting places it
            (float knollX, float knollZ) = Knoll();
            (float x, float z, float yaw) = MeadowScatter.BenchStand(knollX, knollZ);
            Matrix world = Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, 0f, z);

            var (vertices, _, _) = MeadowScatter.BenchGeometry();
            float nearest = float.MaxValue;
            foreach (var vertex in vertices)
            {
                Vector3 p = Vector3.Transform(vertex.Position, world);
                nearest = Math.Min(nearest, MathF.Sqrt((p.X - knollX) * (p.X - knollX) + (p.Z - knollZ) * (p.Z - knollZ)));
            }

            Assert.True(nearest >= MeadowScatter.TREE_BARE + MeadowScatter.TREE_BARE_EDGE,
                $"the bench reaches {nearest:F2} from the trunk, onto the trodden patch that ends at {MeadowScatter.TREE_BARE + MeadowScatter.TREE_BARE_EDGE:F2}");
            Assert.True(nearest >= OAK_ROOT_REACH_MAX, $"the bench reaches {nearest:F2} from the trunk, into roots that reach {OAK_ROOT_REACH_MAX:F2}");
        }

        [Fact]
        public void TheBenchStandsOutFromTheTrunkAndFacesTheArena()
        {
            //What the planting did before the placement became a function of its own
            (float knollX, float knollZ) = Knoll();
            (float x, float z, float yaw) = MeadowScatter.BenchStand(knollX, knollZ);
            Assert.Equal(MeadowScatter.BENCH_OUT, MathF.Sqrt((x - knollX) * (x - knollX) + (z - knollZ) * (z - knollZ)), 3);

            //Local +Z, the way its face looks, turned by the yaw, points from the bench's side of the oak to the arena's centre
            Vector3 facing = Vector3.TransformNormal(Vector3.UnitZ, Matrix.CreateRotationY(yaw));
            Vector2 toArena = Vector2.Normalize(new Vector2(-knollX, -knollZ));
            Assert.Equal(1f, facing.X * toArena.X + facing.Z * toArena.Y, 4);
        }
    }
}
