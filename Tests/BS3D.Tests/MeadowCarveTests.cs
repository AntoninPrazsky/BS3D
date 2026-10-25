using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The brook's swale and the pond's crater (#892, <see cref="MeadowCarve"/>), and the patch that draws them
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
        public void TheSwaleEndsInsideTheZoneTheShaderCutsIn()
        {
            //Past BROOK_CARVE_HALF the shader does not evaluate the swale at all, so the cut must be nothing just inside it,
            //on the steepest cross slopes the brook crosses (where its uphill bank is widest), or the field steps where the
            //branch ends
            float worst = 0f;
            for (float d = BrookStart + 25f; d < MeadowCarve.BROOK_CARVE_END; d += 0.5f)
                for (float side = MeadowCarve.BROOK_CARVE_HALF - 0.3f; side < MeadowCarve.BROOK_CARVE_HALF; side += 0.05f)
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        (float x, float z) = MeadowPath.BrookPoint(d, sign * side, Config);
                        if (MeadowPath.PondShore(x, z, Config) < 8f) continue;
                        worst = Math.Max(worst, Math.Abs(Cut(x, z)));
                    }

            Assert.True(worst < 1e-3f, $"the swale still cuts {worst:F4} deep at the edge of the zone it is cut in");
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

            //About 10,700 over the pond and 12,500 down the brook from the pond to its spring (#979)
            Assert.True(tried > 20000, $"only {tried} points tried");
            Assert.True(open == 0, $"{open} of {tried} points of the grid's hole have no patch over them at rows {rowLevel}, sides {sideLevel}, the first {first}");
        }

        [Theory]
        [MemberData(nameof(Levels))]
        public void EveryChunkHoldsItsVerticesInItsBoxAtEveryLevel(int rowLevel, int sideLevel)
        {
            //What the frustum test (#924) throws away must be wholly out of frame: a chunk's box holds every vertex of its
            //triangles where the shader puts it at this level of coarsening, sideways and in height (the cut's own, which is
            //the vertex shader's before it eases onto the grid near the hole's edge, where the margins are generous),
            //and the bed's box holds the bed's
            Assert.Equal(Patch.Chunks.Length, Patch.ChunkBounds.Length);
            int checkedVertices = 0;
            void Hold(BoundingBox box, int from, int count, string what)
            {
                for (int k = from; k < from + count; k++)
                {
                    int v = Patch.Indices[k];
                    Vector2 p = Patch.At(v, rowLevel, sideLevel, Config);
                    float y = MeadowCarve.Height(p.X, p.Y, Config);
                    Assert.True(p.X >= box.Min.X && p.X <= box.Max.X && p.Y >= box.Min.Z && p.Y <= box.Max.Z && y >= box.Min.Y && y <= box.Max.Y,
                        $"{what}: vertex {v} at ({p.X:F2}, {y:F2}, {p.Y:F2}) is outside its box {box.Min} .. {box.Max}");
                    checkedVertices++;
                }
            }
            for (int c = 0; c < Patch.Chunks.Length; c++)
                Hold(Patch.ChunkBounds[c], Patch.Chunks[c].Start, Patch.Chunks[c].Count, $"chunk {c}");
            Hold(Patch.BedBounds, Patch.BedStart, Patch.Indices.Length - Patch.BedStart, "the bed");
            Assert.True(checkedVertices > 100000, $"only {checkedVertices} vertices checked");
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
        public void TheBrooksWaterStandsUnderItsBanksAndItsEdgeUnderTheMargin()
        {
            //The water lies under the lower of the field and its two sides, so no ground past its edge may stand under it -
            //the margin, the step, the bank, the downhill field the bank eases into; and the water mesh's edge (WATER_HALF
            //either side) must be under the margin, where the depth test cuts it, wherever the water is drawn whole (before
            //the swale starts to shallow) and outside the pond
            float lowestBank = float.MaxValue, lowestEdge = float.MaxValue;
            string where = null;
            for (float d = BrookStart + 2f; d < MeadowCarve.BROOK_CARVE_FADE; d += 0.25f)
            {
                float water = MeadowCarve.BrookWaterLevel(d, Config);
                float half = MeadowCarve.BrookWaterHalf(d, Config);
                foreach (float sign in new[] { -1f, 1f })
                {
                    for (float s = 0.05f; half + s < MeadowCarve.BROOK_CARVE_HALF; s += 0.1f)
                    {
                        (float lx, float lz) = MeadowPath.BrookPoint(d, sign * (half + s), Config);
                        if (MeadowPath.PondShore(lx, lz, Config) < 1f) continue;
                        float over = TerrainMirror.Meadow(lx, lz, Config) - water;
                        if (over < lowestBank) { lowestBank = over; where = $"{d:F1} out, {s:F2} past the water"; }
                    }

                    (float ex, float ez) = MeadowPath.BrookPoint(d, sign * MeadowPatchGeometry.WATER_HALF, Config);
                    if (MeadowPath.PondShore(ex, ez, Config) > 0.5f)
                        lowestEdge = Math.Min(lowestEdge, TerrainMirror.Meadow(ex, ez, Config) - water);
                }
            }

            Assert.True(lowestBank > 0.03f, $"the brook's bank stands only {lowestBank:F3} over its water at {where}");
            Assert.True(lowestEdge > 0.09f, $"the water's edge is only {lowestEdge:F3} under the margin");
        }

        [Fact]
        public void TheBrooksBankIsASwaleNotAWall()
        {
            //The owner on the first groove: "as if an excavator dug a hole there". The bank from the earth step's top to the
            //field is grass at a slope a meadow has - a quarter of the field's (the slope where Meadow.fx turns grass to earth,
            //a normal 0.78 up, is 0.8) - and the swale it makes is two to three times the water's width, as the references'
            float start = BrookStart + 25f;
            var slopes = new List<float>();
            var spreads = new List<float>();
            for (float d = start; d < MeadowCarve.BROOK_CARVE_FADE; d += 0.5f)
            {
                (float cx, float cz) = MeadowPath.BrookPoint(d, 0f, Config);
                if (MeadowPath.PondShore(cx, cz, Config) < MeadowCarve.POND_CARVE_RADIUS) continue;
                float half = MeadowCarve.BrookWaterHalf(d, Config);
                float spread = 0f;
                foreach (float sign in new[] { -1f, 1f })
                {
                    float previous = float.NaN, seen = 0f;
                    for (float lateral = half + MeadowCarve.BrookShore(d, sign) + 0.1f; lateral < MeadowCarve.BROOK_CARVE_HALF; lateral += 0.05f)
                    {
                        (float x, float z) = MeadowPath.BrookPoint(d, sign * lateral, Config);
                        float h = TerrainMirror.Meadow(x, z, Config);
                        if (!float.IsNaN(previous) && Cut(x, z) > 0.02f) slopes.Add(MathF.Abs(h - previous) / 0.05f);
                        if (Cut(x, z) > 0.05f) seen = lateral;
                        previous = h;
                    }
                    spread += seen;
                }
                spreads.Add(spread / (2f * half));
            }

            slopes.Sort();
            spreads.Sort();
            float median = slopes[slopes.Count / 2], p90 = slopes[slopes.Count * 9 / 10], steepest = slopes[^1];
            Assert.True(median < 0.55f && p90 < 0.8f && steepest < 1.2f,
                $"the brook's grassy bank climbs {median:F2} at the median, {p90:F2} at the 90th percentile and {steepest:F2} at most");
            float spreadMedian = spreads[spreads.Count / 2];
            Assert.InRange(spreadMedian, 2.4f, 3.2f);
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
        public void TheBrookFallsAllTheWayToThePond()
        {
            //#979, the owner: "the water does not flow downhill". The brook's level follows the ground under its course, so it
            //falls towards the pond only if that ground does: from the mouth to the spring, never a step back up. Before the
            //fix the pond lay 1.5 over the valley's foot, so the last thirty units ran uphill into it, and past the crest the
            //painted brook climbed 16 units to the next valley.
            float mouth = MeadowCarve.BrookMouth(Config);
            float previous = MeadowCarve.BrookWaterLevel(mouth, Config);
            Assert.True(MathF.Abs(previous - MeadowCarve.PondWaterLevel(Config)) < 1e-3f, $"the mouth stands at {previous:F3}, the pond at {MeadowCarve.PondWaterLevel(Config):F3}");
            for (float d = mouth + 0.25f; d <= MeadowCarve.BROOK_CARVE_END; d += 0.25f)
            {
                float level = MeadowCarve.BrookWaterLevel(d, Config);
                Assert.True(level >= previous - 1e-4f, $"the water rises {previous - level:F4} going downstream at {d:F2} out");
                previous = level;
            }
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
        public void TheWaterIsOneSurfaceThroughTheMouth()
        {
            //#970, the owner: "where the brook joins the pond there is a sharp straight line in the water separating the brook
            //from the pond". The two water meshes meet at the shore, each clipping the other's side, so every input the water
            //is shaded by has to be continuous across it, and across the current's fan in the pond: over every pair of
            //neighbouring points within `reach` of the mouth that some water mesh draws, none may step by more than its slope
            //allows. (Not as far as the pond's centre, 12.9 in: the crater's bearing is undefined there, and its depth, over a
            //unit, comes to a point - water nothing shows through.)
            float mouth = MeadowCarve.BrookMouth(Config);
            (float mx, float mz) = MeadowPath.BrookPoint(mouth, 0f, Config);
            const float step = 0.05f, reach = 10f;
            int n = (int)(2f * reach / step) + 1;
            var water = new (float Still, float Depth, float GrooveShade)?[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = mx - reach + i * step, z = mz - reach + j * step;
                    if ((x - mx) * (x - mx) + (z - mz) * (z - mz) > reach * reach) continue;
                    bool pond = MeadowPath.PondShore(x, z, Config) < 0f;
                    bool brook = !pond && MathF.Abs(MeadowPath.BrookLateral(x, z, Config)) <= MeadowPatchGeometry.WATER_HALF;
                    if (pond || brook) water[i, j] = MeadowCarve.WaterShading(x, z, mouth, Config);
                }

            float still = 0f, depth = 0f, shade = 0f;
            string stillAt = "", depthAt = "", shadeAt = "";
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (water[i, j] is not { } a) continue;
                    foreach ((int di, int dj) in new[] { (1, 0), (0, 1) })
                    {
                        if (i + di >= n || j + dj >= n || water[i + di, j + dj] is not { } b) continue;
                        string at = $"{mx - reach + i * step:F2}, {mz - reach + j * step:F2}";
                        if (MathF.Abs(a.Still - b.Still) > still) { still = MathF.Abs(a.Still - b.Still); stillAt = at; }
                        if (MathF.Abs(a.Depth - b.Depth) > depth) { depth = MathF.Abs(a.Depth - b.Depth); depthAt = at; }
                        if (MathF.Abs(a.GrooveShade - b.GrooveShade) > shade) { shade = MathF.Abs(a.GrooveShade - b.GrooveShade); shadeAt = at; }
                    }
                }

            //The slopes: the stillness and the shade ease over units, the depth follows the bed's own shelving (the brook's
            //shallows, 0.55 over 0.8, are the steepest)
            Assert.True(still <= 1f * step, $"the water's stillness steps by {still:F3} between neighbours {step} apart at {stillAt}");
            Assert.True(depth <= 1.5f * step, $"the water's depth over the bed steps by {depth:F3} between neighbours {step} apart at {depthAt}");
            Assert.True(shade <= 1f * step, $"the swale's shade on the water steps by {shade:F3} between neighbours {step} apart at {shadeAt}");
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

        [Fact]
        public void TheShadersCutAndHoleConstantsAreTheMirrorsOwn()
        {
            //The grid's hole and the cut are made by the shader's own copies of MeadowCarve's numbers, which mirrorcheck (heights
            //only) and the coverage test (the C# numbers) cannot see: a copy that drifted would leave the sky showing through
            //the meadow in the gap, with everything else green. Every constant the shader names as the mirror's is compared
            string source = null;
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null && source == null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Meadow.fx");
                if (File.Exists(candidate)) source = File.ReadAllText(candidate);
            }
            Assert.NotNull(source);

            var mirror = new Dictionary<string, float>();
            foreach (FieldInfo field in typeof(MeadowCarve).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.IsLiteral && field.FieldType == typeof(float)) mirror[field.Name] = (float)field.GetRawConstantValue();

            //The shader's names that differ from the mirror's
            var renamed = new Dictionary<string, string>
            {
                ["BROOK_CARVE_BED"] = "BROOK_BED", ["POND_BANK_WIDTH"] = "POND_BANK", ["BROOK_BANK_WIDTH"] = "BROOK_BANK",
            };
            string[] required =
            {
                "BROOK_CARVE_HALF", "BROOK_HOLE_HALF", "BROOK_HOLE_END", "BROOK_CARVE_END", "BROOK_CARVE_FADE", "POND_CARVE_RADIUS",
                "POND_HOLE_RADIUS", "POND_DROP", "BROOK_DROP", "CARVE_ROUND", "POND_LIP", "BROOK_CARVE_BED", "BROOK_SHALLOWS",
                "BROOK_MARGIN_RISE", "BROOK_MARGIN_MIN", "BROOK_MARGIN_MAX", "BROOK_STEP", "BROOK_STEP_RUN", "BROOK_BANK_WIDTH",
                "BROOK_BANK_PER_RISE", "BROOK_BANK_MAX", "BROOK_LEVEE", "MOUTH_UP", "MOUTH_INTO", "MOUTH_SPREAD", "MOUTH_SOFT",
                "MOUTH_CURRENT_HALF",
            };
            var seen = new HashSet<string>();
            foreach (Match m in Regex.Matches(source, @"^static const float (\w+)\s*=\s*([-0-9.eE+]+);", RegexOptions.Multiline))
            {
                string name = m.Groups[1].Value;
                if (!mirror.TryGetValue(renamed.GetValueOrDefault(name, name), out float expected)) continue;
                float actual = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                Assert.True(MathF.Abs(actual - expected) < 1e-4f, $"Meadow.fx's {name} is {actual} and MeadowCarve's is {expected}");
                seen.Add(name);
            }

            foreach (string name in required) Assert.True(seen.Contains(name), $"Meadow.fx has no `static const float {name} = <number>;` to compare with the mirror's");
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
