using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The brook's groove and the pond's crater (#892, <see cref="MeadowCarve"/>), and the patch that draws them
    /// (<see cref="MeadowPatchGeometry"/>). The camera grid draws the field uncut with a hole where the cut is, so what can
    /// go wrong without a frame is arithmetic: a cut reaching past the zone the shader evaluates it in (a step in the field
    /// where its branch ends), a hole the patch does not cover (the sky through the ground), water standing over its banks
    /// or poking out from under them, the two waters at two levels where they meet, and triangles wound the wrong way. The
    /// GPU's agreement with the mirror is the Testbed's <c>mirrorcheck</c>; how the cut looks is the eye's.
    /// </summary>
    public class MeadowCarveTests
    {
        private static readonly MeadowSceneConfig Config = new();

        private static float Cut(float x, float z) =>
            TerrainMirror.MeadowGround(x, z, Config) - TerrainMirror.Meadow(x, z, Config);

        private static float BrookStart => Config.ClearingRadius * MeadowPath.BROOK_START;

        [Fact]
        public void TheGrooveEndsInsideTheZoneTheShaderCutsIn()
        {
            //Past BROOK_CARVE_HALF the shader does not evaluate the channel at all, so the cut must be nothing just inside it,
            //on the steepest cross slopes the brook crosses, or the field steps where the branch ends
            float worst = 0f;
            for (float d = BrookStart + 25f; d < MeadowCarve.BROOK_CARVE_END; d += 0.5f)
                for (float side = MeadowCarve.BROOK_CARVE_HALF - 0.3f; side < MeadowCarve.BROOK_CARVE_HALF; side += 0.05f)
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        (float x, float z) = MeadowPath.BrookPoint(d, sign * side, Config);
                        if (MeadowPath.PondShore(x, z, Config) < 8f) continue;
                        worst = Math.Max(worst, Math.Abs(Cut(x, z)));
                    }

            Assert.True(worst < 1e-3f, $"the groove still cuts {worst:F4} deep at the edge of the zone it is cut in");
        }

        [Fact]
        public void TheCraterEndsInsideTheZoneTheShaderCutsIn()
        {
            (float px, float pz) = MeadowPath.PondCentre(Config);
            float worst = 0f;
            for (float a = 0f; a < MathHelper.TwoPi; a += 0.01f)
                for (float r = MeadowCarve.POND_CARVE_RADIUS - 1f; r < MeadowCarve.POND_CARVE_RADIUS; r += 0.1f)
                {
                    float x = px + MathF.Cos(a) * r, z = pz + MathF.Sin(a) * r;
                    if (MathF.Abs(MeadowPath.BrookLateral(x, z, Config)) < MeadowCarve.BROOK_HOLE_HALF) continue;
                    worst = Math.Max(worst, Math.Abs(Cut(x, z)));
                }

            Assert.True(worst < 1e-3f, $"the crater still cuts (or its lip raises) {worst:F4} at the edge of the zone it is cut in");
        }

        /// <summary>Every pair of levels the vertex shader can coarsen the patch to: the rows always at least as coarse as
        /// the columns, since they start sooner (<see cref="MeadowPatchGeometry.LOD_ROWS_FROM"/>).</summary>
        public static IEnumerable<object[]> Levels()
        {
            for (int rows = 0; rows <= MeadowPatchGeometry.LOD_LEVELS; rows++)
                for (int sides = 0; sides <= rows; sides++)
                    yield return new object[] { rows, sides };
        }

        private static readonly MeadowPatchGeometry.GroundMesh Patch = MeadowPatchGeometry.Ground(Config);

        [Theory]
        [MemberData(nameof(Levels))]
        public void ThePatchCoversTheGridsWholeHole(int rowLevel, int sideLevel)
        {
            //Every point the camera grid clips away (Meadow.fx's MeadowField, grid) must lie under a triangle of the patch,
            //or the sky shows through the meadow there - at every level the patch coarsens to with distance, where the
            //vertices have slid onto the coarser places and most triangles closed to nothing
            var vertices = new Vector3[Patch.Vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector2 p = Patch.At(v, rowLevel, sideLevel, Config);
                vertices[v] = new Vector3(p.X, 0f, p.Y);
            }
            int[] indices = Patch.Indices;
            var cells = new Dictionary<(int, int), List<int>>();
            const float CELL = 2f;
            for (int t = 0; t < indices.Length; t += 3)
            {
                Vector2 a = Xz(vertices[indices[t]]), b = Xz(vertices[indices[t + 1]]), c = Xz(vertices[indices[t + 2]]);
                if (MathF.Abs(Cross(a, b, c)) < 1e-7f) continue;   //closed to nothing at this level
                int x0 = (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X)) / CELL), x1 = (int)MathF.Floor(MathF.Max(a.X, MathF.Max(b.X, c.X)) / CELL);
                int z0 = (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) / CELL), z1 = (int)MathF.Floor(MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) / CELL);
                for (int i = x0; i <= x1; i++)
                    for (int j = z0; j <= z1; j++)
                    {
                        if (!cells.TryGetValue((i, j), out List<int> list)) cells[(i, j)] = list = new List<int>();
                        list.Add(t);
                    }
            }

            bool Covered(float x, float z)
            {
                if (!cells.TryGetValue(((int)MathF.Floor(x / CELL), (int)MathF.Floor(z / CELL)), out List<int> list)) return false;
                var p = new Vector2(x, z);
                foreach (int t in list)
                    if (Inside(p, Xz(vertices[indices[t]]), Xz(vertices[indices[t + 1]]), Xz(vertices[indices[t + 2]]))) return true;
                return false;
            }

            (float px, float pz) = MeadowPath.PondCentre(Config);
            int open = 0, tried = 0;
            string first = null;
            for (float x = px - MeadowCarve.POND_HOLE_RADIUS; x <= px + MeadowCarve.POND_HOLE_RADIUS; x += 0.37f)
                for (float z = pz - MeadowCarve.POND_HOLE_RADIUS; z <= pz + MeadowCarve.POND_HOLE_RADIUS; z += 0.37f)
                {
                    if ((x - px) * (x - px) + (z - pz) * (z - pz) >= MeadowCarve.POND_HOLE_RADIUS * MeadowCarve.POND_HOLE_RADIUS) continue;
                    tried++;
                    if (!Covered(x, z)) { open++; first ??= $"({x:F1}, {z:F1}) by the pond"; }
                }
            for (float d = BrookStart; d < MeadowCarve.BROOK_HOLE_END; d += 0.41f)
                for (float side = -MeadowCarve.BROOK_HOLE_HALF + 0.01f; side < MeadowCarve.BROOK_HOLE_HALF; side += 0.29f)
                {
                    (float x, float z) = MeadowPath.BrookPoint(d, side, Config);
                    tried++;
                    if (!Covered(x, z)) { open++; first ??= $"({x:F1}, {z:F1}) on the brook, {d:F1} out"; }
                }

            Assert.True(tried > 30000, $"only {tried} points tried");
            Assert.True(open == 0, $"{open} of {tried} points of the grid's hole have no patch over them at rows {rowLevel}, sides {sideLevel}, the first {first}");
        }

        [Fact]
        public void APatchVertexSlidesOntoAnotherAndTheBlocksEdgesStay()
        {
            //What makes the coarsening watertight: a vertex's coarser place is another vertex's finest one, and the blocks'
            //corners (whole multiples of the coarsest spacing) stay where they are at every level
            var finest = new HashSet<(float, float)>();
            for (int v = 0; v < Patch.Vertices.Length; v++) finest.Add((Patch.Rows[v].X, Patch.Sides[v].X));
            int corners = 0;
            for (int v = 0; v < Patch.Vertices.Length; v++)
            {
                Vector4 r = Patch.Rows[v], s = Patch.Sides[v];
                foreach ((float d, float side) in new[] { (r.Y, s.Y), (r.Z, s.Z), (r.W, s.W), (r.W, s.X), (r.X, s.W) })
                    Assert.Contains((d, side), finest);
                Assert.True(r.X >= r.Y && r.Y >= r.Z && r.Z >= r.W, $"vertex {v}'s rows slide away from the arena");
                Assert.True(MathF.Abs(s.X) >= MathF.Abs(s.Y) && MathF.Abs(s.Y) >= MathF.Abs(s.Z) && MathF.Abs(s.Z) >= MathF.Abs(s.W),
                    $"vertex {v}'s sides slide away from the centreline");
                if (r.X == r.W && s.X == s.W) corners++;
            }
            //A block's corners, one vertex in the coarsest level's 8 x 8
            Assert.True(corners * 100 > Patch.Vertices.Length, $"only {corners} of {Patch.Vertices.Length} vertices stay put");
        }

        [Fact]
        public void TheBrooksWaterStandsUnderItsBanksAndItsEdgeUnderTheWalls()
        {
            //The water lies under the lower of the field and its two sides, so neither lip may stand under it; and the water
            //mesh's edge (WATER_HALF either side) must be under the walls, where the depth test cuts it, wherever the water is
            //drawn whole (before the groove starts to shallow) and outside the pond
            float lowestLip = float.MaxValue, lowestEdge = float.MaxValue;
            for (float d = BrookStart + 2f; d < MeadowCarve.BROOK_CARVE_FADE; d += 0.25f)
            {
                float water = MeadowCarve.BrookWaterLevel(d, Config);
                float half = MeadowCarve.BrookWaterHalf(d, Config);
                foreach (float sign in new[] { -1f, 1f })
                {
                    (float lx, float lz) = MeadowPath.BrookPoint(d, sign * (half + 0.5f), Config);
                    if (MeadowPath.PondShore(lx, lz, Config) > 1f)
                        lowestLip = Math.Min(lowestLip, TerrainMirror.Meadow(lx, lz, Config) - water);

                    (float ex, float ez) = MeadowPath.BrookPoint(d, sign * MeadowPatchGeometry.WATER_HALF, Config);
                    if (MeadowPath.PondShore(ex, ez, Config) > 0.5f)
                        lowestEdge = Math.Min(lowestEdge, TerrainMirror.Meadow(ex, ez, Config) - water);
                }
            }

            Assert.True(lowestLip > 0.5f, $"a bank of the brook stands only {lowestLip:F3} over its water");
            Assert.True(lowestEdge > 0.1f, $"the water's edge is only {lowestEdge:F3} under the wall");
        }

        [Fact]
        public void ThePondsWaterEdgeIsUnderItsBanks()
        {
            //The pond's disc reaches POND_WATER_PAST_SHORE past the shore, under the earth step - except where the brook's
            //groove cuts through the bank, where the brook's own water takes over
            (float px, float pz) = MeadowPath.PondCentre(Config);
            float level = MeadowCarve.PondWaterLevel(Config);
            float lowest = float.MaxValue;
            for (float a = 0f; a < MathHelper.TwoPi; a += 0.005f)
            {
                float r = Config.PondRadius * MeadowPath.PondOutline(a) + MeadowPatchGeometry.POND_WATER_PAST_SHORE;
                float x = px + MathF.Cos(a) * r, z = pz + MathF.Sin(a) * r;
                if (MathF.Abs(MeadowPath.BrookLateral(x, z, Config)) < MeadowCarve.BROOK_CARVE_HALF) continue;
                lowest = Math.Min(lowest, TerrainMirror.Meadow(x, z, Config) - level);
            }

            Assert.True(lowest > 0.1f, $"the pond's water reaches out from under its bank: the bank is {lowest:F3} over it");
        }

        [Fact]
        public void TheTwoWatersMeetAtOneLevel()
        {
            //At the mouth the brook's water and the pond's are drawn edge to edge at the shore: one level, or a step shows
            for (float d = BrookStart; d < BrookStart + 30f; d += 0.25f)
            {
                (float x, float z) = MeadowPath.BrookPoint(d, 0f, Config);
                float shore = MeadowPath.PondShore(x, z, Config);
                if (MathF.Abs(shore) > 0.3f) continue;
                Assert.Equal(MeadowCarve.PondWaterLevel(Config), MeadowCarve.BrookWaterLevel(d, Config), 3);
            }
        }

        [Fact]
        public void TheWaterLiesBelowTheFieldAndOverTheBed()
        {
            //A groove and a crater with water in them: the water under the field, the bed under the water
            (float px, float pz) = MeadowPath.PondCentre(Config);
            float pondWater = MeadowCarve.PondWaterLevel(Config);
            Assert.True(TerrainMirror.MeadowGround(px, pz, Config) - pondWater > 1f);
            Assert.True(pondWater - TerrainMirror.Meadow(px, pz, Config) > 1f);

            float d = 150f;
            (float x, float z) = MeadowPath.BrookPoint(d, 0f, Config);
            float brookWater = MeadowCarve.BrookWaterLevel(d, Config);
            Assert.True(TerrainMirror.MeadowGround(x, z, Config) - brookWater >= MeadowCarve.BROOK_DROP);
            Assert.InRange(brookWater - TerrainMirror.Meadow(x, z, Config), MeadowCarve.BROOK_BED * 0.9f, MeadowCarve.BROOK_BED * 1.1f);
        }

        [Fact]
        public void ThePatchAndTheWaterAreWoundClockwiseFromAbove()
        {
            //CLAUDE.md, "Triangle winding": (b - a) x (c - a) pointing away from a viewer above is the front face - the ground
            //at its finest and at its coarsest, where only the triangles spanning a whole block are left
            var coarsest = new Vector3[Patch.Vertices.Length];
            for (int v = 0; v < coarsest.Length; v++)
            {
                Vector2 p = Patch.At(v, MeadowPatchGeometry.LOD_LEVELS, MeadowPatchGeometry.LOD_LEVELS, Config);
                coarsest[v] = new Vector3(p.X, 0f, p.Y);
            }
            foreach ((Vector3[] vertices, int[] indices) in new[] { (Patch.Vertices, Patch.Indices), (coarsest, Patch.Indices), MeadowPatchGeometry.Water(Config) })
                for (int t = 0; t < indices.Length; t += 3)
                {
                    Vector3 a = vertices[indices[t]], b = vertices[indices[t + 1]], c = vertices[indices[t + 2]];
                    a.Y = b.Y = c.Y = 0f;
                    float up = Vector3.Cross(b - a, c - a).Y;
                    if (MathF.Abs(up) < 1e-6f) continue;   //the pond disc's centre ring is degenerate
                    Assert.True(up < 0f, $"triangle {t / 3} is wound anticlockwise from above");
                }
        }

        private static Vector2 Xz(Vector3 v) => new(v.X, v.Z);

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool negative = d1 < -1e-5f || d2 < -1e-5f || d3 < -1e-5f;
            bool positive = d1 > 1e-5f || d2 > 1e-5f || d3 > 1e-5f;
            return !(negative && positive);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (a.X - p.X) * (b.Y - p.Y) - (a.Y - p.Y) * (b.X - p.X);
    }
}
