using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The fins of a mesh (#804, the Potato path's anti-aliasing of an outline): which edges get one, which way out of
    /// each face is, and which face a fin belongs to from an eye - <see cref="EdgeFinMesh.Build"/> and
    /// <see cref="EdgeFinMesh.Owner"/>, the vertex shader's decision on the CPU. Nothing is drawn here.
    /// </summary>
    public class EdgeFinTests
    {
        private static Vector3 Place(Vector4 corner) => new(corner.X, corner.Y, corner.Z);

        //A unit cube round the origin the way the procedural meshes build one: four vertices a face, each with the
        //face's own normal, so a corner is three vertices at one place
        private static (Vector3[] Positions, Vector3[] Normals, int[] Indices) Cube()
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();

            foreach (Vector3 normal in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
            {
                Vector3 u = normal.X != 0f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 v = Vector3.Cross(normal, u);
                int first = positions.Count;

                foreach ((float a, float b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    positions.Add((normal + u * a + v * b) * 0.5f);
                    normals.Add(normal);
                }

                indices.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            return (positions.ToArray(), normals.ToArray(), indices.ToArray());
        }

        [Fact]
        public void ACubeHasAFinOnEachOfItsTwelveEdgesAndNoneAcrossAFace()
        {
            (Vector3[] positions, Vector3[] normals, int[] indices) = Cube();

            (EdgeFinVertex[] vertices, int[] finIndices) = EdgeFinMesh.Build(positions, normals, indices);

            //The six diagonals each lie in one face - two coplanar triangles - and can never be an outline
            Assert.Equal(12 * 4, vertices.Length);
            Assert.Equal(12 * 6, finIndices.Length);

            //Every one is a hard convex edge: a crease, drawn when both faces show
            Assert.All(vertices, vertex => Assert.Equal(1f, vertex.Other.W));

            //Two rows a fin: the vertices on the edge and the ones the shader pushes out of it
            Assert.Equal(vertices.Length / 2, vertices.Count(vertex => vertex.Position.W == 0f));
            Assert.Equal(vertices.Length / 2, vertices.Count(vertex => vertex.Position.W == 1f));
        }

        [Fact]
        public void AFlatSheetHasFinsOnlyRoundItsRim()
        {
            Vector3[] positions = { new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1) };
            Vector3[] normals = { Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up };
            int[] indices = { 0, 2, 1, 0, 3, 2 };

            (EdgeFinVertex[] vertices, _) = EdgeFinMesh.Build(positions, normals, indices);

            Assert.Equal(4 * 4, vertices.Length);

            //A rim is an outline from above and from below alike, and never a crease
            Assert.All(vertices, vertex =>
            {
                Assert.Equal(0f, vertex.Other.W);
                Assert.NotEqual(0, EdgeFinMesh.Owner(vertex, Place(vertex.Position) + new Vector3(0.3f, 4f, 0.2f)));
                Assert.NotEqual(0, EdgeFinMesh.Owner(vertex, Place(vertex.Position) + new Vector3(0.3f, -4f, 0.2f)));

                //Out of a sheet at its rim is off the sheet, whichever side shows
                Assert.Equal(vertex.OutA, vertex.OutB);
                Vector3 mid = (Place(vertex.Position) + Place(vertex.Other)) * 0.5f;
                Assert.True(Vector3.Dot(vertex.OutA, mid - new Vector3(0.5f, 0f, 0.5f)) > 0f, "out of the rim points into the sheet");
            });
        }

        [Fact]
        public void OutOfAFaceIsAwayFromThatFace()
        {
            (Vector3[] positions, Vector3[] normals, int[] indices) = Cube();
            (EdgeFinVertex[] vertices, _) = EdgeFinMesh.Build(positions, normals, indices);

            foreach (EdgeFinVertex vertex in vertices)
            {
                Vector3 edge = Place(vertex.Other) - Place(vertex.Position);

                //In the face's own plane, across the edge, a unit long...
                Assert.Equal(0f, Vector3.Dot(vertex.OutA, vertex.FaceA), 5);
                Assert.Equal(0f, Vector3.Dot(vertex.OutA, edge), 5);
                Assert.Equal(1f, vertex.OutA.Length(), 5);
                Assert.Equal(0f, Vector3.Dot(vertex.OutB, vertex.FaceB), 5);

                //...and off the cube: a face of a cube round the origin has its middle at half its normal, and "out" of
                //it across one of its edges is away from that middle
                Vector3 midEdge = (Place(vertex.Position) + Place(vertex.Other)) * 0.5f;
                Assert.True(Vector3.Dot(vertex.OutA, midEdge - vertex.FaceA * 0.5f) > 0f, "out of face A points into face A");
                Assert.True(Vector3.Dot(vertex.OutB, midEdge - vertex.FaceB * 0.5f) > 0f, "out of face B points into face B");
            }
        }

        [Fact]
        public void AnOutlineBelongsToTheFaceThatShowsAndAHiddenEdgeToNone()
        {
            (Vector3[] positions, Vector3[] normals, int[] indices) = Cube();
            (EdgeFinVertex[] vertices, _) = EdgeFinMesh.Build(positions, normals, indices);

            //The edge between the +X and the +Y face
            EdgeFinVertex edge = vertices.First(v =>
                (v.FaceA == Vector3.UnitX && v.FaceB == Vector3.UnitY) || (v.FaceA == Vector3.UnitY && v.FaceB == Vector3.UnitX));
            int faceX = edge.FaceA == Vector3.UnitX ? 1 : 2, faceY = 3 - faceX;

            //Seen from where only +X shows it is +X's outline; from where only +Y shows, +Y's; from behind both, nothing
            Assert.Equal(faceX, EdgeFinMesh.Owner(edge, new Vector3(6f, -6f, 0f)));
            Assert.Equal(faceY, EdgeFinMesh.Owner(edge, new Vector3(-6f, 6f, 0f)));
            Assert.Equal(0, EdgeFinMesh.Owner(edge, new Vector3(-6f, -6f, 0f)));
        }

        [Fact]
        public void ACreaseWithBothFacesShowingBelongsToTheFaceWhoseNeighbourFallsAway()
        {
            //A roof: a ridge along Z, the two slopes 20 degrees off level, so their normals are 40 degrees apart - a
            //crease, and an OBTUSE one. On a right angle (a cube's edge) both faces fall away from the edge whichever
            //the eye is over, and this rule cannot be told from its opposite (seen: the opposite passed on the cube).
            float sine = MathF.Sin(MathHelper.ToRadians(20f)), cosine = MathF.Cos(MathHelper.ToRadians(20f));
            Vector3 east = new(sine, cosine, 0f), west = new(-sine, cosine, 0f);

            Vector3[] positions =
            {
                new(0, 0, -1), new(0, 0, 1), new(2 * cosine, -2 * sine, 1), new(2 * cosine, -2 * sine, -1),
                new(0, 0, -1), new(0, 0, 1), new(-2 * cosine, -2 * sine, 1), new(-2 * cosine, -2 * sine, -1),
            };
            Vector3[] normals = { east, east, east, east, west, west, west, west };
            int[] indices = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };

            (EdgeFinVertex[] vertices, _) = EdgeFinMesh.Build(positions, normals, indices);

            EdgeFinVertex ridge = vertices.First(v => v.Other.W > 0.5f && v.Position.X == 0f && v.Other.X == 0f);

            //From over either slope, high enough that both show. The fin lies at the RIDGE's depth over the other face's
            //first pixel, so that face must be farther than the ridge there: a step into it must go away from the eye.
            foreach (Vector3 eye in new[] { new Vector3(10f, 5f, 0.2f), new Vector3(-10f, 5f, -0.3f), new Vector3(6f, 9f, 0f), new Vector3(-3f, 12f, 0.5f) })
            {
                Assert.True(Vector3.Dot(ridge.FaceA, eye - Place(ridge.Position)) > 0f && Vector3.Dot(ridge.FaceB, eye - Place(ridge.Position)) > 0f,
                    "the eye was meant to see both slopes");

                int owner = EdgeFinMesh.Owner(ridge, eye);
                Assert.NotEqual(0, owner);

                //Into the face that does not own the fin: the opposite of the way out of it
                Vector3 intoOther = -(owner == 1 ? ridge.OutB : ridge.OutA);

                Vector3 onEdge = Place(ridge.Position), inOther = Place(ridge.Position) + intoOther * 0.05f;
                Assert.True(Vector3.Distance(eye, inOther) > Vector3.Distance(eye, onEdge),
                    $"from {eye} the fin's neighbour comes towards the eye, and would hide the fin");
            }
        }

        [Fact]
        public void ASmoothSurfacesOutlineIsAClosedLoopOfEdgesAndNoCrease()
        {
            //A UV sphere: smooth normals, no hard edge anywhere
            const int slices = 16, stacks = 8;
            var positions = new List<Vector3>();
            var indices = new List<int>();

            for (int stack = 0; stack <= stacks; stack++)
            {
                float phi = MathF.PI * stack / stacks;
                for (int slice = 0; slice < slices; slice++)
                {
                    float theta = MathHelper.TwoPi * slice / slices;
                    positions.Add(new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta)));
                }
            }

            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int a = stack * slices + slice, b = stack * slices + (slice + 1) % slices;
                    int c = a + slices, d = b + slices;
                    indices.AddRange(new[] { a, b, d, a, d, c });
                }
            }

            Vector3[] normals = positions.ToArray();
            (EdgeFinVertex[] vertices, _) = EdgeFinMesh.Build(positions.ToArray(), normals, indices.ToArray());

            Assert.NotEmpty(vertices);
            Assert.All(vertices, vertex => Assert.Equal(0f, vertex.Other.W));

            //From an eye off every symmetry plane, the edges that open are exactly those with one face showing and one
            //not, and each corner on that outline is shared by an even number of them: a closed loop, with no gap a
            //staircase could show through
            Vector3 eye = new(7f, 3.1f, 1.7f);
            var ends = new Dictionary<(int, int, int), int>();
            int open = 0;

            for (int i = 0; i < vertices.Length; i += 4)
            {
                EdgeFinVertex vertex = vertices[i];
                int owner = EdgeFinMesh.Owner(vertex, eye);

                float facingA = Vector3.Dot(vertex.FaceA, eye - Place(vertex.Position)), facingB = Vector3.Dot(vertex.FaceB, eye - Place(vertex.Position));
                Assert.Equal(facingA * facingB < 0f, owner != 0);

                if (owner == 0) continue;
                open++;

                foreach (Vector3 end in new[] { Place(vertex.Position), Place(vertex.Other) })
                {
                    var key = ((int)MathF.Round(end.X * 1e4f), (int)MathF.Round(end.Y * 1e4f), (int)MathF.Round(end.Z * 1e4f));
                    ends[key] = ends.GetValueOrDefault(key) + 1;
                }
            }

            Assert.True(open >= slices, $"only {open} edges of the sphere's outline opened");
            Assert.All(ends.Values, count => Assert.Equal(0, count % 2));
        }
    }
}
