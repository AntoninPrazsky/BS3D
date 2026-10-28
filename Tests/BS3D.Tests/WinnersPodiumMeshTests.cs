using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The winners' podium (#827, <see cref="WinnersPodiumMesh"/>): what the Top Players page places things by, held with no
    /// device - the three steps stand in order and apart by the same rise, a name panel's face stays clear of everything
    /// proud of it, the constants the page scales by are the geometry's own extents, and both materials' solids are shut and
    /// wound clockwise from outside. The openness and winding of the whole mesh are also run by <c>Tools/WindingCheck</c>.
    /// </summary>
    public class WinnersPodiumMeshTests
    {
        private static (List<VertexPositionNormalTexture> Vertices, List<int> Indices) Build(WinnersPodiumMesh.Part part) =>
            WinnersPodiumMesh.Compose(part).Geometry;

        [Fact]
        public void The_steps_stand_in_order_a_flight_of_stairs_with_the_first_in_the_middle()
        {
            Vector3 first = WinnersPodiumMesh.StepTop(1), second = WinnersPodiumMesh.StepTop(2), third = WinnersPodiumMesh.StepTop(3);

            Assert.Equal(0f, first.X);
            Assert.True(second.X < first.X && first.X < third.X, "the second is on the left and the third on the right");
            Assert.Equal(-second.X, third.X, 5);

            Assert.True(first.Y > second.Y && second.Y > third.Y, "the first is the highest");
            Assert.Equal(first.Y - second.Y, second.Y - third.Y, 4);

            //One floor and one depth under all three, so a cup on each stands square to the others
            Assert.Equal(first.Z, second.Z);
            Assert.Equal(first.Z, third.Z);
        }

        [Fact]
        public void Every_name_is_drawn_on_one_line_on_the_front_face_under_its_own_cup()
        {
            for (int place = 1; place <= 3; place++)
            {
                Vector3 top = WinnersPodiumMesh.StepTop(place), front = WinnersPodiumMesh.StepFront(place);

                Assert.Equal(top.X, front.X);
                Assert.True(front.Z > 0f, "the front faces the camera, +Z");
                Assert.True(front.Y < top.Y, "the name is under the top it belongs to");
                Assert.Equal(WinnersPodiumMesh.StepFront(1).Y, front.Y);
                Assert.Equal(WinnersPodiumMesh.StepFront(1).Z, front.Z);
            }
        }

        [Fact]
        public void The_tops_lie_inside_the_footprint_and_under_the_height_the_page_scales_by()
        {
            for (int place = 1; place <= 3; place++)
            {
                Vector3 top = WinnersPodiumMesh.StepTop(place);
                float half = WinnersPodiumMesh.StepTopWidth(place) * 0.5f;

                Assert.True(top.X - half > -WinnersPodiumMesh.WIDTH * 0.5f && top.X + half < WinnersPodiumMesh.WIDTH * 0.5f);
                Assert.True(top.Y < WinnersPodiumMesh.HEIGHT);
                Assert.True(half > 0f);
            }

            //Neighbouring flats do not overlap: a cup is scaled to its own step and cannot reach the next one
            Assert.True(WinnersPodiumMesh.StepTopWidth(1) < WinnersPodiumMesh.StepTop(3).X - WinnersPodiumMesh.StepTop(1).X);
        }

        [Fact]
        public void A_place_that_is_not_on_the_podium_is_refused_by_every_question()
        {
            foreach (int place in new[] { 0, 4, -1 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => WinnersPodiumMesh.StepTop(place));
                Assert.Throws<ArgumentOutOfRangeException>(() => WinnersPodiumMesh.StepFront(place));
                Assert.Throws<ArgumentOutOfRangeException>(() => WinnersPodiumMesh.StepTopWidth(place));
            }
        }

        [Fact]
        public void The_extent_constants_are_the_geometrys_own()
        {
            List<VertexPositionNormalTexture> all = Build(WinnersPodiumMesh.Part.Body).Vertices
                .Concat(Build(WinnersPodiumMesh.Part.Trim).Vertices).ToList();

            float minX = all.Min(v => v.Position.X), maxX = all.Max(v => v.Position.X);
            float minY = all.Min(v => v.Position.Y), maxY = all.Max(v => v.Position.Y);
            float minZ = all.Min(v => v.Position.Z), maxZ = all.Max(v => v.Position.Z);

            Assert.Equal(-WinnersPodiumMesh.WIDTH * 0.5f, minX, 4);
            Assert.Equal(WinnersPodiumMesh.WIDTH * 0.5f, maxX, 4);
            Assert.Equal(0f, minY, 4);
            Assert.Equal(WinnersPodiumMesh.HEIGHT, maxY, 4);
            Assert.Equal(-WinnersPodiumMesh.DEPTH * 0.5f, minZ, 4);
            Assert.Equal(WinnersPodiumMesh.DEPTH * 0.5f, maxZ, 4);
        }

        /// <summary>
        /// The name panel is bare lacquer inside a frame, and the page draws on it in 2D: nothing may stand proud of the face
        /// in front of that rectangle, or a numeral or a post would cut across a name. Held against the Trim, which is all
        /// that stands proud of a block.
        /// </summary>
        [Fact]
        public void Nothing_stands_in_front_of_a_name_panel()
        {
            List<VertexPositionNormalTexture> trim = Build(WinnersPodiumMesh.Part.Trim).Vertices;

            for (int place = 1; place <= 3; place++)
            {
                Vector3 front = WinnersPodiumMesh.StepFront(place);
                const float MARGIN = 0.002f;

                IEnumerable<VertexPositionNormalTexture> inside = trim.Where(v =>
                    v.Position.Z > front.Z + 0.001f
                    && MathF.Abs(v.Position.X - front.X) < WinnersPodiumMesh.PLAQUE_WIDTH * 0.5f - MARGIN
                    && MathF.Abs(v.Position.Y - front.Y) < WinnersPodiumMesh.PLAQUE_HEIGHT * 0.5f - MARGIN);

                Assert.Empty(inside);
            }
        }

        [Theory]
        [InlineData(WinnersPodiumMesh.Part.Body)]
        [InlineData(WinnersPodiumMesh.Part.Trim)]
        public void Both_materials_solids_are_shut(WinnersPodiumMesh.Part part)
        {
            (List<VertexPositionNormalTexture> vertices, List<int> indices) = Build(part);

            Assert.Empty(MeshOpenness.OpenEdges(vertices, indices));
        }

        /// <summary>
        /// CLAUDE.md's winding rule against the mesh's own normals: a triangle wound clockwise from outside has a geometric
        /// normal <c>(b - a) x (c - a)</c> pointing against the outward normals the lighting uses. The builder corrects every
        /// face against the normal it is given, so this holds the builder's use of it here - the chipped corners' triangles,
        /// whose normals come from the geometry, included.
        /// </summary>
        [Theory]
        [InlineData(WinnersPodiumMesh.Part.Body)]
        [InlineData(WinnersPodiumMesh.Part.Trim)]
        public void Every_triangle_is_wound_clockwise_from_outside_against_its_own_normals(WinnersPodiumMesh.Part part)
        {
            (List<VertexPositionNormalTexture> vertices, List<int> indices) = Build(part);
            int wrong = 0;

            for (int i = 0; i < indices.Count; i += 3)
            {
                VertexPositionNormalTexture a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                Vector3 geometric = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
                if (geometric.LengthSquared() < 1e-14f) continue;

                if (Vector3.Dot(Vector3.Normalize(geometric), Vector3.Normalize(a.Normal + b.Normal + c.Normal)) > 0f) wrong++;
            }

            Assert.Equal(0, wrong);
        }

        [Fact]
        public void The_whole_podium_is_a_few_thousand_triangles()
        {
            int triangles = Build(WinnersPodiumMesh.Part.Body).Indices.Count / 3 + Build(WinnersPodiumMesh.Part.Trim).Indices.Count / 3;

            Assert.InRange(triangles, 1000, 5000);
        }
    }
}
