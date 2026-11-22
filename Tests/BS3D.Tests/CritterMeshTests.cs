using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The desert's creatures' bodies (#966, #1110): High and Ultra's finer beetle and scorpion (<see cref="CritterMesh.FineBeetle"/>,
    /// <see cref="CritterMesh.FineScorpion"/>) stand in for Low and Medium's off the same instances, so they must be the same
    /// creature at the same size: the same extents, feet on the sand, the same legs on the same gait for <c>Critter.fx</c> to
    /// swing, wound as every procedural mesh is, and within a 16-bit index buffer. And they must be finer, or there is no
    /// reason to draw them.
    /// </summary>
    public class CritterMeshTests
    {
        private readonly ITestOutputHelper _output;

        public CritterMeshTests(ITestOutputHelper output) => _output = output;

        public static TheoryData<string> Kinds => new() { "beetle", "scorpion" };

        private static (VertexPositionNormalTexture[] Vertices, short[] Indices) Coarse(string kind) =>
            kind == "beetle" ? CritterMesh.Beetle() : CritterMesh.Scorpion();

        private static (VertexPositionNormalTexture[] Vertices, short[] Indices) Fine(string kind) =>
            kind == "beetle" ? CritterMesh.FineBeetle() : CritterMesh.FineScorpion();

        [Theory]
        [MemberData(nameof(Kinds))]
        public void The_fine_creature_is_the_coarse_ones_size_and_stands_on_the_sand(string kind)
        {
            var coarse = Coarse(kind).Vertices.Select(v => v.Position).ToArray();
            var fine = Fine(kind).Vertices.Select(v => v.Position).ToArray();
            Vector3 cMin = Min(coarse), cMax = Max(coarse), fMin = Min(fine), fMax = Max(fine);
            _output.WriteLine($"{kind}: coarse {cMin} to {cMax}, fine {fMin} to {fMax}");

            //Its length, its width and its height within a tenth of a body length of the coarse one's, and its highest point
            //(the scorpion's sting, which the intro's close shots frame) within three hundredths
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.InRange(Get(fMin, axis), Get(cMin, axis) - 0.1f, Get(cMin, axis) + 0.1f);
                Assert.InRange(Get(fMax, axis), Get(cMax, axis) - 0.1f, Get(cMax, axis) + 0.1f);
            }
            Assert.InRange(fMax.Y, cMax.Y - 0.03f, cMax.Y + 0.03f);

            //Its feet on the sand, nothing far under it
            Assert.InRange(fMin.Y, -0.01f, 0.01f);
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void The_fine_creature_walks_on_the_coarse_ones_legs(string kind)
        {
            var coarse = Coarse(kind).Vertices;
            var fine = Fine(kind).Vertices;

            //The same gait phases, so the tripod steps as it did, and as many legs on each
            float[] phases = coarse.Where(v => v.TextureCoordinate.X >= 0f).Select(v => v.TextureCoordinate.X).Distinct().OrderBy(p => p).ToArray();
            Assert.Equal(phases, fine.Where(v => v.TextureCoordinate.X >= 0f).Select(v => v.TextureCoordinate.X).Distinct().OrderBy(p => p).ToArray());

            //Every leg's weight runs from its hip (0) to its foot (1), and its foot is on the sand
            foreach (float phase in phases)
            {
                var leg = fine.Where(v => v.TextureCoordinate.X == phase).ToArray();
                Assert.Equal(0f, leg.Min(v => v.TextureCoordinate.Y), 3);
                Assert.Equal(1f, leg.Max(v => v.TextureCoordinate.Y), 3);
                Assert.InRange(leg.Where(v => v.TextureCoordinate.Y > 0.99f).Min(v => v.Position.Y), -0.01f, 0.01f);
            }

            //Nothing but a leg, the body and the tail (the pincers sway with it), each weight between nought and one
            Assert.All(fine, v => Assert.True(v.TextureCoordinate.X >= 0f || v.TextureCoordinate.X == CritterMesh.BODY || v.TextureCoordinate.X == CritterMesh.TAIL,
                $"a vertex marked {v.TextureCoordinate.X}"));
            Assert.All(fine, v => Assert.InRange(v.TextureCoordinate.Y, 0f, 1f));
            if (kind == "scorpion") Assert.Contains(fine, v => v.TextureCoordinate.X == CritterMesh.TAIL);
        }

        /// <summary>
        /// Both beetles end their legs on the one foot table the prints are laid by (#1217, <see cref="CritterMesh.BeetleFoot"/>):
        /// every foot, where its leg's vertices of full weight ring round, within two thousandths of a body length of its
        /// table entry in plan, on the sand, with its leg's phase (<see cref="CritterMesh.LegPhase"/>), and no foot that is
        /// not in the table. The coarse beetle's feet stood 0.04 nearer its body before #1217, and fail it.
        /// </summary>
        [Fact]
        public void Both_beetles_stand_on_the_feet_the_prints_are_laid_by()
        {
            foreach ((string name, VertexPositionNormalTexture[] vertices) in new[] { ("coarse", CritterMesh.Beetle().Vertices), ("fine", CritterMesh.FineBeetle().Vertices) })
            {
                var tips = vertices.Where(v => v.TextureCoordinate.X >= 0f && v.TextureCoordinate.Y > 0.999f).ToList();
                int claimed = 0;
                for (int foot = 0; foot < CritterMesh.BEETLE_FEET; foot++)
                {
                    int leg = foot >> 1, side = (foot & 1) == 0 ? -1 : 1;
                    Vector3 table = CritterMesh.BeetleFoot(leg, side);
                    var ring = tips.Where(v => new Vector2(v.Position.X - table.X, v.Position.Z - table.Z).Length() < 0.08f).ToArray();
                    Assert.True(ring.Length > 0, $"the {name} beetle has no foot near foot {foot}'s place in the table");
                    //Its ring's centre, the seam's repeated vertex counted once
                    Vector3[] round = ring.Select(v => v.Position).Distinct().ToArray();
                    Vector3 centre = round.Aggregate(Vector3.Zero, (sum, p) => sum + p) / round.Length;
                    float off = new Vector2(centre.X - table.X, centre.Z - table.Z).Length();
                    _output.WriteLine(FormattableString.Invariant($"{name} foot {foot}: {ring.Length} vertices round ({centre.X:F4}, {centre.Y:F4}, {centre.Z:F4}), {off:F5} off the table"));
                    Assert.True(off < 0.002f, $"the {name} beetle's foot {foot} stands {off:F4} off the table");
                    Assert.InRange(centre.Y, -0.01f, 0.01f);
                    Assert.All(ring, v => Assert.Equal(CritterMesh.LegPhase(leg, side), v.TextureCoordinate.X));
                    claimed += ring.Length;
                }
                Assert.Equal(tips.Count, claimed);
            }
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void The_fine_creature_is_wound_as_every_procedural_mesh_is_and_fits_its_buffer(string kind)
        {
            var (vertices, indices) = Fine(kind);
            var (_, coarseIndices) = Coarse(kind);
            _output.WriteLine($"{kind}: {vertices.Length} vertices, {indices.Length / 3} triangles (coarse {coarseIndices.Length / 3})");

            //A 16-bit index buffer, every index in it
            Assert.True(vertices.Length <= short.MaxValue, $"{vertices.Length} vertices");
            Assert.All(indices, i => Assert.InRange((int)i, 0, vertices.Length - 1));
            Assert.Equal(0, indices.Length % 3);

            //Clockwise seen from outside: each face's (b - a) x (c - a) points into the surface, against its vertices' normals,
            //which are of unit length
            int wrong = 0, faces = 0;
            for (int t = 0; t < indices.Length; t += 3)
            {
                VertexPositionNormalTexture a = vertices[indices[t]], b = vertices[indices[t + 1]], c = vertices[indices[t + 2]];
                Vector3 face = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
                if (face.LengthSquared() < 1e-14f) continue;
                faces++;
                if (Vector3.Dot(face, a.Normal + b.Normal + c.Normal) > 0f) wrong++;
            }
            Assert.Equal(0, wrong);
            Assert.True(faces > indices.Length / 3 * 0.9f, $"{faces} of {indices.Length / 3} triangles have an area");
            Assert.All(vertices, v => Assert.InRange(v.Normal.Length(), 0.99f, 1.01f));

            //Finer: at least three times the coarse one's triangles
            Assert.True(indices.Length >= 3 * coarseIndices.Length, $"{indices.Length / 3} triangles against the coarse {coarseIndices.Length / 3}");
        }

        private static Vector3 Min(Vector3[] p) => new(p.Min(v => v.X), p.Min(v => v.Y), p.Min(v => v.Z));
        private static Vector3 Max(Vector3[] p) => new(p.Max(v => v.X), p.Max(v => v.Y), p.Max(v => v.Z));
        private static float Get(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
    }
}
