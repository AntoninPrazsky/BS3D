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
    /// each face is, which face a fin belongs to from an eye and which fins a frame hands the GPU -
    /// <see cref="EdgeFinMesh.Build"/>, <see cref="EdgeFinMesh.Owner"/> (the vertex shader's decision on the CPU) and
    /// <see cref="EdgeFinMesh.SelectLive(ReadOnlySpan{EdgeFinMesh.Edge}, Vector3, Span{int})"/> with
    /// <see cref="EdgeFinMesh.WriteIndices"/>. Nothing is drawn here.
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
        public void AFlatSheetHasFinsOnlyRoundItsRimWhereItsFaceIsDrawn()
        {
            Vector3[] positions = { new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1) };
            Vector3[] normals = { Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up };
            int[] indices = { 0, 2, 1, 0, 3, 2 };

            //Drawn with culling off: a rim is an outline from above and from below alike
            (EdgeFinVertex[] bothSides, _) = EdgeFinMesh.Build(positions, normals, indices, twoSided: true);

            //Drawn from the side its normal is on: from above its rim is an outline, from below nothing of it is drawn
            (EdgeFinVertex[] oneSide, _) = EdgeFinMesh.Build(positions, normals, indices);

            Assert.Equal(4 * 4, bothSides.Length);
            Assert.Equal(4 * 4, oneSide.Length);

            foreach ((EdgeFinVertex[] vertices, bool below) in new[] { (bothSides, true), (oneSide, false) })
            {
                EdgeFinMesh.Edge[] edges = EdgeFinMesh.Edges(vertices);
                int[] live = new int[edges.Length];

                Assert.All(vertices, vertex =>
                {
                    Assert.NotEqual(0, EdgeFinMesh.Owner(vertex, Place(vertex.Position) + new Vector3(0.3f, 4f, 0.2f)));
                    Assert.Equal(below, EdgeFinMesh.Owner(vertex, Place(vertex.Position) + new Vector3(0.3f, -4f, 0.2f)) != 0);

                    //Out of a sheet at its rim is off the sheet, whichever side shows
                    Assert.Equal(vertex.OutA, vertex.OutB);
                    Vector3 mid = (Place(vertex.Position) + Place(vertex.Other)) * 0.5f;
                    Assert.True(Vector3.Dot(vertex.OutA, mid - new Vector3(0.5f, 0f, 0.5f)) > 0f, "out of the rim points into the sheet");
                });

                //And the choice of a frame's fins agrees: all four from above, all four or none from below
                Assert.Equal(4, EdgeFinMesh.SelectLive(edges, new Vector3(0.4f, 4f, 0.6f), live));
                Assert.Equal(below ? 4 : 0, EdgeFinMesh.SelectLive(edges, new Vector3(0.4f, -4f, 0.6f), live));
            }
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

        //Two slopes meeting along Z, `rise` degrees off level each: a ridge when the slopes fall away from the shared edge,
        //a valley when they climb from it. Both wound and given normals so that "out of the surface" is up.
        private static (Vector3[] Positions, Vector3[] Normals, int[] Indices) Fold(float rise, bool valley)
        {
            float sine = MathF.Sin(MathHelper.ToRadians(rise)), cosine = MathF.Cos(MathHelper.ToRadians(rise));
            float lift = valley ? 2 * sine : -2 * sine;
            Vector3 east = new(valley ? -sine : sine, cosine, 0f), west = new(valley ? sine : -sine, cosine, 0f);

            Vector3[] positions =
            {
                new(0, 0, -1), new(0, 0, 1), new(2 * cosine, lift, 1), new(2 * cosine, lift, -1),
                new(0, 0, -1), new(0, 0, 1), new(-2 * cosine, lift, 1), new(-2 * cosine, lift, -1),
            };
            Vector3[] normals = { east, east, east, east, west, west, west, west };
            int[] indices = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };

            return (positions, normals, indices);
        }

        [Fact]
        public void AConcaveEdgeGetsNoFinAndTheSameEdgeFoldedTheOtherWayDoes()
        {
            //A valley's floor is never a visible outline: where one slope shows and the other does not, the one that
            //shows is behind the other. Its six rim edges are open, and still get theirs.
            (Vector3[] positions, Vector3[] normals, int[] indices) = Fold(20f, valley: true);
            (EdgeFinVertex[] valley, _) = EdgeFinMesh.Build(positions, normals, indices);

            Assert.Equal(6 * 4, valley.Length);
            Assert.DoesNotContain(valley, v => v.Position.X == 0f && v.Other.X == 0f);

            //The ridge is the valley with its slopes falling instead: seven, the shared edge among them
            (positions, normals, indices) = Fold(20f, valley: false);
            (EdgeFinVertex[] ridge, _) = EdgeFinMesh.Build(positions, normals, indices);

            Assert.Equal(7 * 4, ridge.Length);
            Assert.Contains(ridge, v => v.Position.X == 0f && v.Other.X == 0f);
        }

        //A box from `low` to `high` the way the procedural meshes build one: four vertices a face, the face's own normal
        private static void AddBox(List<Vector3> positions, List<Vector3> normals, List<int> indices, Vector3 low, Vector3 high)
        {
            Vector3 centre = (low + high) * 0.5f, half = (high - low) * 0.5f;

            foreach (Vector3 normal in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
            {
                Vector3 u = normal.X != 0f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 v = Vector3.Cross(normal, u);
                int first = positions.Count;

                foreach ((float a, float b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    positions.Add(centre + (normal + u * a + v * b) * half);
                    normals.Add(normal);
                }

                indices.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
        }

        [Fact]
        public void AnEdgeInsideAWallTwoSolidsShareGetsNoFinInAnAssembledMesh()
        {
            //A unit box and a half-height one against its +X side, flush with it in front, behind and underneath: the
            //carriage's cheek and its socket. Where they meet, the tall box's front and back walls go on as the low one's.
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            AddBox(positions, normals, indices, Vector3.Zero, Vector3.One);
            AddBox(positions, normals, indices, new Vector3(1f, 0f, 0f), new Vector3(2f, 0.5f, 1f));

            static bool InTheSeam(EdgeFinVertex v) => v.Position.X == 1f && v.Other.X == 1f;
            static bool Upright(EdgeFinVertex v) => v.Position.Y != v.Other.Y;

            //As one surface each (what a lathe is told): every edge of both but the one they have in common end to end
            //underneath, which four faces meet at and no rule here reasons about
            (EdgeFinVertex[] apart, _) = EdgeFinMesh.Build(positions.ToArray(), normals.ToArray(), indices.ToArray());
            Assert.Equal(22 * 4, apart.Length);
            Assert.Equal(4 * 4, apart.Count(v => InTheSeam(v) && Upright(v)));

            //Assembled: the four upright edges in the seam - the tall box's two and the low one's two, each inside the
            //wall the other continues - are gone, and nothing else is
            (EdgeFinVertex[] assembled, _) = EdgeFinMesh.Build(positions.ToArray(), normals.ToArray(), indices.ToArray(), assembled: true);
            Assert.Equal(18 * 4, assembled.Length);
            Assert.DoesNotContain(assembled, v => InTheSeam(v) && Upright(v));

            //The tall box's top edge over the low one stays: its top goes on nowhere, and it is the step's outline
            Assert.Contains(assembled, v => InTheSeam(v) && v.Position.Y == 1f && v.Other.Y == 1f);
        }

        [Fact]
        public void AFinIsShadedAtEachEndWithThatEndsOwnNormalOnEitherFace()
        {
            //A unit sphere's normal at a point is the point, so a fin vertex's two shading normals must both be its own
            //place - not the edge's other end, which is what face B's were until the crease rule read them
            (Vector3[] positions, int[] indices) = Sphere(12, 6);
            (EdgeFinVertex[] vertices, _) = EdgeFinMesh.Build(positions, positions, indices);

            Assert.NotEmpty(vertices);
            //(To a hair: the vertices of a pole are one corner, and sin(pi) puts each a ten-millionth off the axis its own way)
            Assert.All(vertices, vertex =>
            {
                Assert.True(Vector3.Distance(Place(vertex.Position), vertex.ShadeA) < 1e-5f, "face A's normal is the other end's");
                Assert.True(Vector3.Distance(Place(vertex.Position), vertex.ShadeB) < 1e-5f, "face B's normal is the other end's");
                Assert.Equal(0f, vertex.Other.W);
            });
        }

        [Fact]
        public void ACreaseIsWhereTheShadingBreaksAndNotWhereTheFacesMeetSharply()
        {
            //A six-sided pin along Y, its sides 60 degrees apart: once shaded as the cylinder it stands for (one normal a
            //corner, pointing out from the axis) and once as the prism it is (every side its own normal)
            const int sides = 6;
            var round = (Positions: new List<Vector3>(), Normals: new List<Vector3>(), Indices: new List<int>());
            var flat = (Positions: new List<Vector3>(), Normals: new List<Vector3>(), Indices: new List<int>());

            for (int side = 0; side < sides; side++)
            {
                float from = MathHelper.TwoPi * side / sides, to = MathHelper.TwoPi * (side + 1) / sides, mid = (from + to) * 0.5f;
                Vector3 a = new(MathF.Cos(from), 0f, MathF.Sin(from)), b = new(MathF.Cos(to), 0f, MathF.Sin(to));
                Vector3 face = new(MathF.Cos(mid), 0f, MathF.Sin(mid));

                foreach (var mesh in new[] { round, flat })
                {
                    int first = mesh.Positions.Count;
                    mesh.Positions.AddRange(new[] { a, b, b + Vector3.Up, a + Vector3.Up });
                    mesh.Normals.AddRange(mesh == round ? new[] { a, b, b, a } : new[] { face, face, face, face });
                    mesh.Indices.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
            }

            static bool Upright(EdgeFinVertex v) => v.Position.Y != v.Other.Y && v.Position.X == v.Other.X && v.Position.Z == v.Other.Z;

            //The same six upright edges either way - each can be the pin's outline - and a crease only on the prism
            EdgeFinVertex[] roundFins = EdgeFinMesh.Build(round.Positions.ToArray(), round.Normals.ToArray(), round.Indices.ToArray(), twoSided: true).Vertices;
            EdgeFinVertex[] flatFins = EdgeFinMesh.Build(flat.Positions.ToArray(), flat.Normals.ToArray(), flat.Indices.ToArray(), twoSided: true).Vertices;

            Assert.Equal(sides * 4, roundFins.Count(Upright));
            Assert.Equal(sides * 4, flatFins.Count(Upright));
            Assert.All(roundFins.Where(Upright), v => Assert.Equal(0f, v.Other.W));
            Assert.All(flatFins.Where(Upright), v => Assert.Equal(1f, v.Other.W));
        }

        //A UV sphere's triangles, smooth: its own positions are its normals
        private static (Vector3[] Positions, int[] Indices) Sphere(int slices, int stacks)
        {
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

            return (positions.ToArray(), indices.ToArray());
        }

        [Fact]
        public void TheFinsChosenForAnEyeLeaveOutNoneTheShaderWouldOpenAndMostOfTheRest()
        {
            (Vector3[] cubePositions, Vector3[] cubeNormals, int[] cubeIndices) = Cube();
            (Vector3[] ridgePositions, Vector3[] ridgeNormals, int[] ridgeIndices) = Fold(20f, valley: false);
            (Vector3[] spherePositions, int[] sphereIndices) = Sphere(48, 24);

            var meshes = new[]
            {
                EdgeFinMesh.Build(cubePositions, cubeNormals, cubeIndices).Vertices,
                EdgeFinMesh.Build(ridgePositions, ridgeNormals, ridgeIndices).Vertices,
                EdgeFinMesh.Build(spherePositions, spherePositions, sphereIndices).Vertices,
            };

            var random = new Random(804);
            int sphereEdges = 0, sphereChosen = 0;

            foreach (EdgeFinVertex[] vertices in meshes)
            {
                EdgeFinMesh.Edge[] edges = EdgeFinMesh.Edges(vertices);
                int[] chosen = new int[edges.Length], triangles = new int[edges.Length * 6];
                Assert.Equal(vertices.Length / 4, edges.Length);

                for (int trial = 0; trial < 200; trial++)
                {
                    //Eyes near and far, all round, a few of them inside the mesh
                    Vector3 eye = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f)
                        * (trial % 4 == 0 ? 1.5f : 24f);

                    int count = EdgeFinMesh.SelectLive(edges, eye, chosen);

                    //Each edge once, and its quad's two triangles in Build's own pattern
                    var live = new HashSet<int>();
                    EdgeFinMesh.WriteIndices(chosen.AsSpan(0, count), triangles);
                    for (int i = 0; i < count; i++)
                    {
                        Assert.True(live.Add(chosen[i]), "an edge was chosen twice");

                        int first = chosen[i] * 4;
                        Assert.Equal(new[] { first, first + 1, first + 3, first, first + 3, first + 2 }, triangles[(i * 6)..(i * 6 + 6)]);
                    }

                    //Everything the vertex shader would open - at either end of the edge, which it decides separately
                    for (int e = 0; e < edges.Length; e++)
                    {
                        bool opens = EdgeFinMesh.Owner(vertices[e * 4], eye) != 0 || EdgeFinMesh.Owner(vertices[e * 4 + 2], eye) != 0;
                        if (opens) Assert.True(live.Contains(e), $"edge {e} opens from {eye} and was not chosen");
                    }

                    if (vertices == meshes[2] && trial % 4 != 0)
                    {
                        sphereEdges += edges.Length;
                        sphereChosen += live.Count;
                    }
                }
            }

            //And it is a choice: a smooth ball's outline from outside is a loop, a twentieth of its edges at this fineness
            Assert.True(sphereChosen * 10 < sphereEdges, $"{sphereChosen} of {sphereEdges} of a sphere's edges were chosen");
            Assert.True(sphereChosen > 0);
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
