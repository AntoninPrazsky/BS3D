using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One vertex of an edge's fin (#804): the edge's two ends, the two faces that meet at it and how this end is shaded
    /// on each, which is everything <c>PotatoModel.fx</c>'s <c>PotatoFinVS</c> needs to decide, from the eye, whether
    /// the edge is an outline and which way out of the surface the fin stands.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EdgeFinVertex : IVertexType
    {
        /// <summary>This end of the edge, and in W the row: 0 for the vertex on the edge, 1 for the one pushed out of it.</summary>
        public Vector4 Position;

        /// <summary>The other end, and in W what the edge is: 0 an outline only, 1 a crease (convex, drawn when both
        /// of its faces show), 2 a concave crease (drawn when both show, lying on the one that faces the eye more).</summary>
        public Vector4 Other;

        /// <summary>The geometric (flat) normals of the two faces sharing the edge, pointing out of the surface.</summary>
        public Vector3 FaceA, FaceB;

        /// <summary>This end's shading normal on each of the two faces: equal on a smooth surface, different across a
        /// hard edge.</summary>
        public Vector3 ShadeA, ShadeB;

        /// <summary>
        /// The way out of face A and out of face B: in the face's own plane, across the edge, away from the face's own
        /// triangle. A direction of the mesh and not a sign on a cross product, so an instance that mirrors the mesh
        /// (a negative scale) carries it as it carries every other direction.
        /// </summary>
        public Vector3 OutA, OutB;

        //Fifteen vertex attributes with the instance stream's seven, of the sixteen a Shader Model 3 vertex shader takes:
        //which is why the row and the crease ride in the two positions' fourth components
        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.Position, 0),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(32, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(44, VertexElementFormat.Vector3, VertexElementUsage.Normal, 1),
            new VertexElement(56, VertexElementFormat.Vector3, VertexElementUsage.Tangent, 0),
            new VertexElement(68, VertexElementFormat.Vector3, VertexElementUsage.Binormal, 0),
            new VertexElement(80, VertexElementFormat.Vector3, VertexElementUsage.Tangent, 1),
            new VertexElement(92, VertexElementFormat.Vector3, VertexElementUsage.Binormal, 1));

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }

    /// <summary>
    /// The <b>fins</b> of a mesh (#804): the anti-aliasing of its outline on the Raspberry Pi's Potato path, which has
    /// no multisampled target. For every edge of the mesh that can ever be an outline there is a quad lying collapsed
    /// on the edge; the vertex shader opens it by one pixel, outwards on the screen, exactly where the edge IS an
    /// outline from this eye, and its alpha falls from 1 on the edge to 0 at the far side - the coverage of a pixel by
    /// the surface, blended against whatever is really beyond it. It is the balls' rim (<see cref="BallRimMesh"/>)
    /// for a surface whose outline is not a formula: there the outline is found, per edge, by the two faces' facing.
    /// <para>
    /// <b>Which edges.</b> Every <b>convex</b> edge between two faces that are not coplanar (two coplanar faces turn
    /// their backs together, so their shared edge is never an outline), and every edge that has only one face (the rim
    /// of an open surface: an outline whenever its face is drawn - from the side its normal is on, or from both where
    /// the mesh is drawn with culling off). A <b>concave</b> edge is never an outline: where one of its faces shows
    /// and the other does not, the one that shows is behind the other's plane as the eye sees it, so the outline it
    /// would be is always hidden by the surface itself (Sander, Hoppe, Snyder and Gortler 2001 leave them out for the
    /// same reason) - and they were more than half of the edges a frame's meshes had (18 507 with them, 8 042 without,
    /// on the meadow's island with the gun and the plate). A <b>crease</b> - an edge across which the SHADING breaks,
    /// the two faces' normals at it more than <see cref="CREASE_COSINE"/> apart - is drawn when both of its faces
    /// show, convex or concave: there the staircase is between two surfaces lit differently, and the fin lays the one
    /// over the first pixel of the other. A convex crease's fin stands at the edge's depth, over the face that falls
    /// away; a <b>concave</b> crease's lies IN the plane of the face that faces the eye more, since at a concave edge
    /// both faces come towards the eye and a fin at the edge's depth would be behind either - which is why it is drawn
    /// with a depth bias, as a decal is (<c>InstancedModelRenderer.DrawPotatoFins</c>). The gun's dark bands are
    /// such edges: a step face between two faces of one mesh, concave on one side.
    /// </para>
    /// <para>
    /// <b>And in a mesh assembled of several solids, no edge a wall runs on past.</b> The gun's carriage is plates,
    /// sockets and pads built into one mesh, and where two of them stand in one flat wall the edge each has there is
    /// inside that wall, not round it: its fin drew a dark dashed line down the middle of the carriage's cheek (seen).
    /// An edge is left out when either of its faces goes on, in its own plane and facing the same way, through another
    /// triangle on the far side of the edge.
    /// </para>
    /// <para>
    /// <b>Which of them a frame draws is decided by the CPU</b> (<see cref="SelectLive(Vector3)"/>): the vertex shader
    /// opens a fin only where its edge is an outline or a crease from this eye, but the Raspberry Pi's V3D runs that
    /// shader's position half for every vertex handed to it, in the binning pass, and that measured +1.05 to +1.43 ms
    /// a frame for collapsed quads nobody sees. Two dot products an edge here hand it a few hundred instead.
    /// </para>
    /// <para>
    /// <see cref="Build"/> is pure arithmetic over arrays, so the choice of edges is tested without a device
    /// (<c>EdgeFinTests</c>); <see cref="EdgeFins"/> is what hangs a built one on a mesh.
    /// </para>
    /// </summary>
    public sealed class EdgeFinMesh : IDisposable
    {
        /// <summary>
        /// The cosine of the angle between two faces' SHADING normals at their shared edge below which the edge is a
        /// crease worth drawing when both faces show: 30 degrees. The shading normals and not the faces' own: what a fin
        /// hides is a break in the colour, and a round pin of eight flat sides shaded as a cylinder has none along its
        /// sides however sharply they meet, while a lathe's ring marked as a crease has one at any angle past this.
        /// </summary>
        public const float CREASE_COSINE = 0.866f;

        //Two faces within a quarter of a degree of the same plane are left without a fin. Exactly coplanar ones can never
        //make an outline of their edge; nearly coplanar ones can only from an eye almost in their plane, and they are what
        //a finely tessellated flat is made of - the island's dished top is tens of thousands of them, none of which a
        //camera above the island can see edge-on. NOT wider: a lathe's round wall is facets a fraction of a degree apart
        //(the island's, 512 round, turn 0.7 degrees each), and those are exactly the edges its side outline runs along -
        //at a degree and a half the collar's and the island's flanks lost their fins (seen).
        private const float COPLANAR_COSINE = 0.99999f;

        public VertexBuffer VertexBuffer { get; }

        /// <summary>Every fin, for a draw that lets the vertex shader alone decide which open (several instances of
        /// the mesh in one draw, each seen from its own side).</summary>
        public IndexBuffer IndexBuffer { get; }
        public int PrimitiveCount { get; }

        /// <summary>The fins <see cref="SelectLive(Vector3)"/> last chose. Null until it has chosen once.</summary>
        public IndexBuffer LiveIndexBuffer => _liveBuffer;

        /// <summary>How many edges have a fin.</summary>
        public int EdgeCount { get; }

        private readonly GraphicsDevice _graphicsDevice;

        //What SelectLive reads for every edge, apart from the vertices the GPU holds: one cache line an edge
        private readonly Edge[] _edges;

        //The edges chosen last and the eye they were chosen for, the ones being chosen now, and the triangles of the last
        //choice as they were uploaded. -1 until there has been a choice.
        private int[] _liveEdges, _chosenEdges;
        private readonly int[] _liveIndices;
        private int _liveCount = -1;
        private Vector3 _liveEye;
        private DynamicIndexBuffer _liveBuffer;

        public EdgeFinMesh(GraphicsDevice graphicsDevice, EdgeFinVertex[] vertices, int[] indices)
        {
            _graphicsDevice = graphicsDevice;
            EdgeCount = vertices.Length / 4;
            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, EdgeFinVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            //Four vertices an edge: a mesh of a few thousand triangles is past sixteen bits
            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            _edges = Edges(vertices);
            _liveEdges = new int[_edges.Length];
            _chosenEdges = new int[_edges.Length];
            _liveIndices = new int[indices.Length];
        }

        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
            _liveBuffer?.Dispose();
        }

        /// <summary>
        /// What deciding whether an edge's fin can open takes: a point of the edge, its two faces' normals and what
        /// the edge is (<see cref="EdgeFinVertex.Other"/>'s W). <see cref="EdgeFinVertex"/> without what only the
        /// shader needs.
        /// </summary>
        public readonly struct Edge
        {
            public readonly Vector3 At, NormalA, NormalB;
            public readonly float Kind;

            public Edge(Vector3 at, Vector3 normalA, Vector3 normalB, float kind)
            {
                At = at;
                NormalA = normalA;
                NormalB = normalB;
                Kind = kind;
            }

            /// <summary>Drawn when both faces show.</summary>
            public bool Crease => Kind > 0.5f;

            /// <summary>Never an outline: only ever drawn with both faces showing.</summary>
            public bool Concave => Kind > 1.5f;
        }

        /// <summary>The edges of a built fin mesh, in the order of their quads: one for every four vertices.</summary>
        public static Edge[] Edges(ReadOnlySpan<EdgeFinVertex> vertices)
        {
            var edges = new Edge[vertices.Length / 4];
            for (int e = 0; e < edges.Length; e++)
            {
                ref readonly EdgeFinVertex vertex = ref vertices[e * 4];
                edges[e] = new Edge(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), vertex.FaceA, vertex.FaceB,
                    vertex.Other.W);
            }

            return edges;
        }

        //How near to edge-on a face may be - the sine of its plane's angle to the eye's ray, squared - and still count as
        //facing either way: 0.6 of a degree. SelectLive must never leave out a fin the shader would open, and the two do
        //not round alike (the shader tests in world space, this in the mesh's own), so an edge with a face this close to
        //turning is handed over whichever way it fell here.
        private const float EDGE_ON_SQUARED = 1e-4f;

        /// <summary>
        /// The edges whose fins can be open from <paramref name="eye"/>, by their number, into <paramref name="live"/>;
        /// returns how many. A superset of the edges <see cref="Owner"/> gives a face - the shader still decides each
        /// one - and a small one: the outline, the creases with both faces showing, and what is within a hair of turning.
        /// </summary>
        /// <param name="edges">The mesh's edges (<see cref="Edges"/>).</param>
        /// <param name="eye">The eye in the mesh's own space.</param>
        /// <param name="live">Room for every edge.</param>
        public static int SelectLive(ReadOnlySpan<Edge> edges, Vector3 eye, Span<int> live)
        {
            int count = 0;

            for (int e = 0; e < edges.Length; e++)
            {
                ref readonly Edge edge = ref edges[e];

                Vector3 toEye = eye - edge.At;
                float facingA = Vector3.Dot(edge.NormalA, toEye), facingB = Vector3.Dot(edge.NormalB, toEye);
                float edgeOn = EDGE_ON_SQUARED * toEye.LengthSquared();

                //Collapsed for certain: both faces plainly away, or both plainly towards and nothing to draw between
                //them; a concave crease, never an outline, only with both towards
                bool open = edge.Concave
                    ? facingA > 0f && facingB > 0f
                    : facingA * facingB < 0f
                        || facingA * facingA < edgeOn || facingB * facingB < edgeOn
                        || (edge.Crease && facingA > 0f && facingB > 0f);
                if (open) live[count++] = e;
            }

            return count;
        }

        /// <summary>The two triangles of each of <paramref name="live"/>'s edges' quads, six indices an edge, in the
        /// pattern of <see cref="Build"/>.</summary>
        public static void WriteIndices(ReadOnlySpan<int> live, Span<int> indices)
        {
            for (int i = 0; i < live.Length; i++)
            {
                //P on the edge, P out, Q on the edge, Q out
                int first = live[i] * 4, at = i * 6;
                indices[at] = first; indices[at + 1] = first + 1; indices[at + 2] = first + 3;
                indices[at + 3] = first; indices[at + 4] = first + 3; indices[at + 5] = first + 2;
            }
        }

        /// <summary>
        /// Chooses this frame's fins for one instance of the mesh seen from <paramref name="eye"/> (in the mesh's own
        /// space) into <see cref="LiveIndexBuffer"/>, and returns how many triangles that is.
        /// <para>
        /// Asked from the eye it was last asked from - a mesh standing still under a camera standing still, which is most
        /// frames of a level - it answers without looking; and a choice that comes out as the last one did is not
        /// uploaded again. No allocation either way.
        /// </para>
        /// </summary>
        public int SelectLive(Vector3 eye)
        {
            bool lost = _liveBuffer != null && _liveBuffer.IsContentLost;

            if (_liveCount >= 0 && eye == _liveEye && !lost) return _liveCount * 2;

            int count = SelectLive(_edges, eye, _chosenEdges);
            _liveEye = eye;

            if (count == _liveCount && !lost && _chosenEdges.AsSpan(0, count).SequenceEqual(_liveEdges.AsSpan(0, count))) return count * 2;

            (_liveEdges, _chosenEdges) = (_chosenEdges, _liveEdges);
            _liveCount = count;
            if (count == 0) return 0;

            WriteIndices(_liveEdges.AsSpan(0, count), _liveIndices);

            _liveBuffer ??= new DynamicIndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, _liveIndices.Length, BufferUsage.WriteOnly);
            _liveBuffer.SetData(_liveIndices, 0, count * 6, SetDataOptions.Discard);

            return count * 2;
        }

        /// <summary>
        /// The fins of a triangle list: four vertices and two triangles for every edge that can be an outline. Vertices
        /// at the same place are one corner whatever their normals (the procedural meshes repeat a corner for every face
        /// that has its own normal there), which is what finds the two faces of a hard edge.
        /// </summary>
        /// <param name="positions">The mesh's vertex positions.</param>
        /// <param name="normals">Its shading normals, one a vertex. They also say which side of a triangle is out:
        /// the meshes here wind clockwise seen from outside, but an open surface drawn two-sided is whatever it is.</param>
        /// <param name="indices">Its triangles, three indices each.</param>
        /// <param name="assembled">Whether the mesh is several solids in one - a <see cref="MeshBuilder"/>'s - so that
        /// an edge may lie inside a wall two of them share (the class remarks). A surface of revolution is not, and
        /// is spared the search.</param>
        /// <param name="twoSided">Whether the mesh is drawn with culling off, so that the back of an open surface shows
        /// and the surface's rim is an outline from there too. Otherwise a rim's fin opens only where its face is
        /// towards the eye: the back of a culled face is not drawn, and neither is the last pixel of it.</param>
        /// <param name="rims">Whether the edges with one face get fins at all. False for a surface whose rims are
        /// buried in other meshes (the drain's gold band, its edges sunk into the stone and the glass): a rim is an
        /// outline from everywhere, so every one of them would be handed to the GPU every frame to be hidden by the
        /// depth test - a thousand of the band's.</param>
        /// <param name="concave">Whether a concave crease gets a fin (<see cref="EdgeFins.ConcaveCreases"/>).</param>
        public static (EdgeFinVertex[] Vertices, int[] Indices) Build(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Vector3> normals,
            ReadOnlySpan<int> indices, bool assembled = false, bool twoSided = false, bool rims = true, bool concave = true)
        {
            //Corners: vertices welded by place
            var corners = new Dictionary<(int, int, int), int>();
            int[] corner = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var key = ((int)MathF.Round(positions[i].X * WELD), (int)MathF.Round(positions[i].Y * WELD), (int)MathF.Round(positions[i].Z * WELD));
                if (!corners.TryGetValue(key, out int id)) corners.Add(key, id = corners.Count);
                corner[i] = id;
            }

            //Every edge's faces: the triangle, and its own two vertices on the edge (theirs are the shading normals)
            var edges = new Dictionary<(int, int), List<EdgeFace>>();
            int triangles = indices.Length / 3;

            //An assembled mesh's triangles as planes, to ask whether a face goes on past one of its edges
            Wall[] walls = assembled ? new Wall[triangles] : null;
            int wallCount = 0;

            for (int t = 0; t < triangles; t++)
            {
                int a = indices[t * 3], b = indices[t * 3 + 1], c = indices[t * 3 + 2];
                Vector3 faceNormal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                if (faceNormal.LengthSquared() < 1e-14f) continue;
                faceNormal.Normalize();

                //Out of the surface is the side the shading normals are on
                if (Vector3.Dot(faceNormal, normals[a] + normals[b] + normals[c]) < 0f) faceNormal = -faceNormal;

                if (assembled) walls[wallCount++] = new Wall(faceNormal, positions[a], positions[b], positions[c]);

                AddFace(edges, corner, a, b, c, faceNormal);
                AddFace(edges, corner, b, c, a, faceNormal);
                AddFace(edges, corner, c, a, b, faceNormal);
            }

            var vertices = new List<EdgeFinVertex>();
            var finIndices = new List<int>();

            foreach (KeyValuePair<(int, int), List<EdgeFace>> entry in edges)
            {
                List<EdgeFace> faces = entry.Value;

                //More than two faces at an edge is not a surface this can reason about; one is an open rim
                if (faces.Count > 2) continue;

                EdgeFace faceA = faces[0];
                bool open = faces.Count == 1;
                if (open && !rims) continue;
                EdgeFace faceB = open ? faceA : faces[1];

                Vector3 p = positions[faceA.From], q = positions[faceA.To];
                Vector3 normalA = faceA.Normal;
                //A rim has one face. Seen from both sides it is carried as an edge between that face and its own back,
                //which is an outline from everywhere; seen from one, as a crease of the face with itself, which opens
                //exactly where the face is towards the eye (the shader and SelectLive read both as they read any edge)
                Vector3 normalB = open ? (twoSided ? -faceA.Normal : faceA.Normal) : faceB.Normal;

                float cosine = Vector3.Dot(normalA, normalB);
                if (!open && cosine > COPLANAR_COSINE) continue;

                //Which way is out of each face, across the edge: in the face's plane, away from its third corner
                Vector3 edge = q - p;
                Vector3 outA = OutOf(normalA, edge, positions[faceA.Third] - p);
                Vector3 outB = open ? outA : OutOf(normalB, edge, positions[faceB.Third] - p);

                //Each face's own vertex at each end. AddFace stored both faces from the edge's lower corner to its higher,
                //so "From" is at P for B as it is for A. (This read B's the other way round until the crease rule looked
                //at them: a fin that belonged to B was shaded with the far end's normal, and every edge along a lathe's
                //profile - its two ends on rings that face differently - came out a crease.)
                Vector3 shadeAP = normals[faceA.From], shadeAQ = normals[faceA.To];
                Vector3 shadeBP = open ? shadeAP : normals[faceB.From], shadeBQ = open ? shadeAQ : normals[faceB.To];

                //Hard: the shading breaks across the edge, at either end
                bool hard = !open && (Vector3.Dot(shadeAP, shadeBP) < CREASE_COSINE || Vector3.Dot(shadeAQ, shadeBQ) < CREASE_COSINE);

                //Convex: the other face's third corner is behind this face's plane. A concave edge is never a visible
                //outline (the class remarks): with the shading smooth across it, nothing of it is ever seen
                bool convex = open || Vector3.Dot(normalA, positions[faceB.Third] - p) < 0f;
                if (!convex && (!hard || !concave)) continue;

                //Inside a wall two solids of the mesh share: not an edge of anything the eye sees
                if (assembled)
                {
                    ReadOnlySpan<Wall> all = walls.AsSpan(0, wallCount);
                    if (GoesOnPast(all, p, q, normalA, outA) || (!open && GoesOnPast(all, p, q, normalB, outB))) continue;
                }

                //What the edge is (EdgeFinVertex.Other's W): a rim of a one-sided surface is carried as a crease of its
                //face with itself (above), a hard edge as a crease, convex or concave, and the rest is an outline only
                float kind = open ? (twoSided ? 0f : 1f) : hard ? (convex ? 1f : 2f) : 0f;

                int first = vertices.Count;
                for (int end = 0; end < 2; end++)
                {
                    for (int row = 0; row < 2; row++)
                    {
                        vertices.Add(new EdgeFinVertex
                        {
                            Position = new Vector4(end == 0 ? p : q, row),
                            Other = new Vector4(end == 0 ? q : p, kind),
                            FaceA = normalA,
                            FaceB = normalB,
                            ShadeA = end == 0 ? shadeAP : shadeAQ,
                            ShadeB = end == 0 ? shadeBP : shadeBQ,
                            OutA = outA,
                            OutB = outB
                        });
                    }
                }

                //P on the edge, P out, Q on the edge, Q out
                finIndices.Add(first); finIndices.Add(first + 1); finIndices.Add(first + 3);
                finIndices.Add(first); finIndices.Add(first + 3); finIndices.Add(first + 2);
            }

            return (vertices.ToArray(), finIndices.ToArray());
        }

        //Places closer than a hundredth of a millimetre of a unit are one corner
        private const float WELD = 1e5f;

        //The unit direction in a face's plane, across its edge, away from the face's third corner (given from the edge)
        private static Vector3 OutOf(Vector3 faceNormal, Vector3 edge, Vector3 toThird)
        {
            Vector3 across = Vector3.Cross(faceNormal, edge);
            if (across.LengthSquared() < 1e-20f) return Vector3.Zero;
            across.Normalize();

            return Vector3.Dot(across, toThird) < 0f ? across : -across;
        }

        private readonly record struct EdgeFace(int From, int To, int Third, Vector3 Normal);

        //A triangle as the piece of a plane it is
        private readonly struct Wall
        {
            public readonly Vector3 Normal, A, B, C;

            public Wall(Vector3 normal, Vector3 a, Vector3 b, Vector3 c)
            {
                Normal = normal;
                A = a;
                B = b;
                C = c;
            }
        }

        //How far past an edge the wall is looked for, how nearly two triangles must face the same way to be one wall
        //(a quarter of a degree, as COPLANAR_COSINE), and how near its plane a point must be to lie in it
        private const float PAST_EDGE = 1e-3f;
        private const float SAME_PLANE = 2e-4f;

        //Whether the face with this normal goes on past its edge P-Q, in its own plane, through another triangle facing
        //the same way: asked a step out of the face (outOf) at three places along the edge, and yes if at any - an edge
        //that is partly inside a wall draws its line there, which is worse than a stretch of outline left stepped
        private static bool GoesOnPast(ReadOnlySpan<Wall> walls, Vector3 p, Vector3 q, Vector3 normal, Vector3 outOf)
        {
            Vector3 quarter = p + (q - p) * 0.25f + outOf * PAST_EDGE;
            Vector3 half = p + (q - p) * 0.5f + outOf * PAST_EDGE;
            Vector3 threeQuarters = p + (q - p) * 0.75f + outOf * PAST_EDGE;

            for (int w = 0; w < walls.Length; w++)
            {
                ref readonly Wall wall = ref walls[w];

                if (Vector3.Dot(wall.Normal, normal) < COPLANAR_COSINE) continue;
                if (MathF.Abs(Vector3.Dot(wall.Normal, half - wall.A)) > SAME_PLANE) continue;

                if (Holds(wall, quarter) || Holds(wall, half) || Holds(wall, threeQuarters)) return true;
            }

            return false;
        }

        //Whether a point of a triangle's plane is inside the triangle: on one side of all three of its edges, whichever
        //way it is wound (its normal was turned to the shading normals' side, its corners were not)
        private static bool Holds(in Wall wall, Vector3 point)
        {
            float ab = Vector3.Dot(Vector3.Cross(wall.B - wall.A, point - wall.A), wall.Normal);
            float bc = Vector3.Dot(Vector3.Cross(wall.C - wall.B, point - wall.B), wall.Normal);
            float ca = Vector3.Dot(Vector3.Cross(wall.A - wall.C, point - wall.C), wall.Normal);

            return (ab > 0f && bc > 0f && ca > 0f) || (ab < 0f && bc < 0f && ca < 0f);
        }

        private static void AddFace(Dictionary<(int, int), List<EdgeFace>> edges, int[] corner, int from, int to, int third, Vector3 normal)
        {
            int a = corner[from], b = corner[to];
            if (a == b) return;

            //Stored from the lower corner to the higher, so the two faces of an edge agree which end is P
            var key = a < b ? (a, b) : (b, a);
            if (!edges.TryGetValue(key, out List<EdgeFace> faces)) edges.Add(key, faces = new List<EdgeFace>(2));

            faces.Add(a < b ? new EdgeFace(from, to, third, normal) : new EdgeFace(to, from, third, normal));
        }

        /// <summary>
        /// Which face a fin belongs to from this eye - whose colour it carries - as <c>PotatoFinEdge</c> decides it: 0
        /// for none (the fin stays collapsed), 1 for face A, 2 for face B. The shader's arithmetic on the CPU, so the
        /// rule is tested where a test can run.
        /// <para>
        /// An <b>outline</b> - one face towards the eye, one away - belongs to the face that shows. A <b>crease</b> with
        /// both faces showing belongs to the face whose neighbour falls away from the edge as the eye sees it, because
        /// the fin lies at the edge's own depth and is drawn over that neighbour's first pixel: for a convex edge at
        /// least one of the two always does. A <b>concave crease</b> with both showing belongs to the face that faces
        /// the eye less: its colour is laid over the first pixel of the one that faces it more, in that face's own
        /// plane, where a depth bias lets it stand.
        /// </para>
        /// </summary>
        public static int Owner(in EdgeFinVertex vertex, Vector3 eye)
        {
            Vector3 toEye = eye - new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
            float facingA = Vector3.Dot(vertex.FaceA, toEye), facingB = Vector3.Dot(vertex.FaceB, toEye);

            if (vertex.Other.W > 1.5f) return facingA > 0f && facingB > 0f ? (facingA < facingB ? 1 : 2) : 0;

            if (facingA * facingB < 0f) return facingA > 0f ? 1 : 2;

            if (vertex.Other.W > 0.5f && facingA > 0f && facingB > 0f)
                return Vector3.Dot(toEye, vertex.OutB) > 0f ? 1 : 2;

            return 0;
        }
    }

    /// <summary>
    /// Which meshes carry <see cref="EdgeFinMesh"/> fins (#804), without any of them having to say so: a procedural
    /// mesh hands its arrays here as it uploads them, and an <see cref="InstancedModelRenderer"/> asks by the vertex
    /// buffer it was given. Off unless a host turns it on before building its meshes - the Raspberry Pi's build - so
    /// every other build constructs exactly what it did.
    /// </summary>
    public static class EdgeFins
    {
        /// <summary>Whether this build draws fins at all. Set once, before the host builds its scene.</summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Whether concave creases get fins (<see cref="EdgeFinMesh"/>): true, unless a run says "finsless=concave" to
        /// measure them apart. Read as a mesh is built, like <see cref="Enabled"/>.
        /// </summary>
        public static bool ConcaveCreases { get; set; } = true;

        /// <summary>
        /// Whether the gun's wheels get fins: true, unless a run says "finsless=wheels" to measure them apart (two
        /// draws a frame, chosen instance by instance). Read by <c>CannonRig</c> as the wheels are built.
        /// </summary>
        public static bool Wheels { get; set; } = true;

        /// <summary>
        /// Whether a draw of one instance hands the GPU only the fins <see cref="EdgeFinMesh.SelectLive(Vector3)"/>
        /// chose for this eye (the default), or every fin of the mesh for the vertex shader to collapse - the first cut,
        /// kept behind a launch argument so the one can be measured against the other.
        /// </summary>
        public static bool SelectOnCpu { get; set; } = true;

        //How many Wanted() scopes are open: a mesh gets fins only when it is built inside one. And whether the innermost
        //said its meshes are drawn with culling off.
        private static int _wanted;
        private static bool _twoSided, _rims = true;

        /// <summary>
        /// Says the meshes built until the returned scope is disposed are ones whose outline shows in play - the island,
        /// the ceiling's plate, the gun - and so get fins when <see cref="Enabled"/>. Everything built outside one gets
        /// none: building fins for every procedural mesh measured 177 ms of load on the laptop and 142 000 edges, most
        /// of them the trophy cups' and the wordmark's, whose outlines nobody sees in a level.
        /// </summary>
        /// <param name="twoSided">Whether the meshes built inside are drawn with culling off
        /// (<see cref="EdgeFinMesh.Build"/>'s parameter of the name).</param>
        /// <param name="rims">Whether their edges with one face get fins (<see cref="EdgeFinMesh.Build"/>'s
        /// parameter of the name).</param>
        public static Scope Wanted(bool twoSided = false, bool rims = true)
        {
            var scope = new Scope(_twoSided, _rims);

            _wanted++;
            _twoSided = twoSided;
            _rims = rims;

            return scope;
        }

        /// <summary>See <see cref="Wanted"/>.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly bool _outerTwoSided, _outerRims;

            internal Scope(bool outerTwoSided, bool outerRims)
            {
                _outerTwoSided = outerTwoSided;
                _outerRims = outerRims;
            }

            public void Dispose()
            {
                _wanted--;
                _twoSided = _outerTwoSided;
                _rims = _outerRims;
            }
        }

        //Weak on the vertex buffer: a mesh that is dropped takes its fins' entry with it, and the fins' own buffers go
        //as every graphics resource nobody disposed goes, by the device's finalizer queue
        private static readonly ConditionalWeakTable<VertexBuffer, EdgeFinMesh> _fins = new();

        /// <summary>The fins of the mesh these arrays are, built and kept against its vertex buffer. Nothing unless
        /// <see cref="Enabled"/>.</summary>
        /// <param name="assembled">Whether the mesh is several solids in one (<see cref="EdgeFinMesh.Build"/>).</param>
        public static void Register(GraphicsDevice device, VertexBuffer meshVertices, ReadOnlySpan<VertexPositionNormalTexture> vertices,
            ReadOnlySpan<int> indices, bool assembled = false)
        {
            if (!Enabled || _wanted <= 0 || vertices.Length == 0) return;

            Vector3[] positions = new Vector3[vertices.Length], normals = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                positions[i] = vertices[i].Position;
                normals[i] = vertices[i].Normal;
            }

            (EdgeFinVertex[] finVertices, int[] finIndices) = EdgeFinMesh.Build(positions, normals, indices, assembled, _twoSided, _rims, ConcaveCreases);
            if (finVertices.Length == 0) return;

            _fins.AddOrUpdate(meshVertices, new EdgeFinMesh(device, finVertices, finIndices));

            EdgeCount += finVertices.Length / 4;
        }

        /// <summary>How many edges have been given a fin since the process began, for the host's log.</summary>
        public static int EdgeCount { get; private set; }

        /// <summary><see cref="Register(GraphicsDevice, VertexBuffer, ReadOnlySpan{VertexPositionNormalTexture}, ReadOnlySpan{int}, bool)"/>
        /// for a mesh that keeps sixteen-bit indices, which the GPU reads unsigned - the surfaces of revolution, none
        /// of them assembled.</summary>
        public static void Register(GraphicsDevice device, VertexBuffer meshVertices, ReadOnlySpan<VertexPositionNormalTexture> vertices,
            ReadOnlySpan<short> indices)
        {
            if (!Enabled || _wanted <= 0) return;

            int[] wide = new int[indices.Length];
            for (int i = 0; i < wide.Length; i++) wide[i] = (ushort)indices[i];

            Register(device, meshVertices, vertices, wide);
        }

        /// <summary>The fins registered for a mesh's vertex buffer, or null.</summary>
        public static EdgeFinMesh Find(VertexBuffer meshVertices) =>
            meshVertices != null && _fins.TryGetValue(meshVertices, out EdgeFinMesh fins) ? fins : null;
    }
}
