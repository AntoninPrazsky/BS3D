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

        /// <summary>The other end, and in W whether the edge is a crease: 1 for one that is drawn when both of its faces
        /// show, 0 for an edge that is drawn only as an outline.</summary>
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
    /// <b>Which edges.</b> Every edge between two faces that are not coplanar (two coplanar faces turn their backs
    /// together, so their shared edge is never an outline), and every edge that has only one face (the rim of an open
    /// surface, always an outline). A <b>crease</b> - a convex edge sharper than <see cref="CREASE_COSINE"/> - is
    /// drawn as well when both of its faces show: there the staircase is between two surfaces lit differently, and the
    /// fin lays the one over the first pixel of the other.
    /// </para>
    /// <para>
    /// <see cref="Build"/> is pure arithmetic over arrays, so the choice of edges is tested without a device
    /// (<c>EdgeFinTests</c>); <see cref="EdgeFins"/> is what hangs a built one on a mesh.
    /// </para>
    /// </summary>
    public sealed class EdgeFinMesh : IDisposable
    {
        /// <summary>
        /// The cosine of the angle between two faces' normals below which their edge is a crease worth drawing when both
        /// faces show: 30 degrees. A lathe's neighbouring facets are a few degrees apart and are shaded as one smooth
        /// surface, so a fin over their shared edge would have nothing to hide and would cost a strip of blended pixels.
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
        public IndexBuffer IndexBuffer { get; }
        public int PrimitiveCount { get; }

        /// <summary>How many edges have a fin.</summary>
        public int EdgeCount { get; }

        public EdgeFinMesh(GraphicsDevice graphicsDevice, EdgeFinVertex[] vertices, int[] indices)
        {
            EdgeCount = vertices.Length / 4;
            PrimitiveCount = indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, EdgeFinVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertices);

            //Four vertices an edge: a mesh of a few thousand triangles is past sixteen bits
            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);
        }

        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
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
        public static (EdgeFinVertex[] Vertices, int[] Indices) Build(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Vector3> normals,
            ReadOnlySpan<int> indices)
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
            for (int t = 0; t < triangles; t++)
            {
                int a = indices[t * 3], b = indices[t * 3 + 1], c = indices[t * 3 + 2];
                Vector3 faceNormal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                if (faceNormal.LengthSquared() < 1e-14f) continue;
                faceNormal.Normalize();

                //Out of the surface is the side the shading normals are on
                if (Vector3.Dot(faceNormal, normals[a] + normals[b] + normals[c]) < 0f) faceNormal = -faceNormal;

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
                EdgeFace faceB = open ? faceA : faces[1];

                Vector3 p = positions[faceA.From], q = positions[faceA.To];
                Vector3 normalA = faceA.Normal;
                Vector3 normalB = open ? -faceA.Normal : faceB.Normal;

                float cosine = Vector3.Dot(normalA, normalB);
                if (!open && cosine > COPLANAR_COSINE) continue;

                //Which way is out of each face, across the edge: in the face's plane, away from its third corner
                Vector3 edge = q - p;
                Vector3 outA = OutOf(normalA, edge, positions[faceA.Third] - p);
                Vector3 outB = open ? outA : OutOf(normalB, edge, positions[faceB.Third] - p);

                //A crease: sharp, and convex - the other face's third corner is behind this face's plane
                bool crease = !open && cosine < CREASE_COSINE && Vector3.Dot(normalA, positions[faceB.Third] - p) < 0f;

                //Face B runs the edge the other way, so its vertex at P is its "To"
                Vector3 shadeAP = normals[faceA.From], shadeAQ = normals[faceA.To];
                Vector3 shadeBP = open ? shadeAP : normals[faceB.To], shadeBQ = open ? shadeAQ : normals[faceB.From];

                int first = vertices.Count;
                for (int end = 0; end < 2; end++)
                {
                    for (int row = 0; row < 2; row++)
                    {
                        vertices.Add(new EdgeFinVertex
                        {
                            Position = new Vector4(end == 0 ? p : q, row),
                            Other = new Vector4(end == 0 ? q : p, crease ? 1f : 0f),
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
        /// Which face a fin belongs to from this eye, as <c>PotatoFinVS</c> decides it: 0 for none (the fin stays
        /// collapsed), 1 for face A, 2 for face B. The shader's arithmetic on the CPU, so the rule is tested where a
        /// test can run.
        /// <para>
        /// An <b>outline</b> - one face towards the eye, one away - belongs to the face that shows. A <b>crease</b> with
        /// both faces showing belongs to the face whose neighbour falls away from the edge as the eye sees it, because
        /// the fin lies at the edge's own depth and is drawn over that neighbour's first pixel: for a convex edge at
        /// least one of the two always does.
        /// </para>
        /// </summary>
        public static int Owner(in EdgeFinVertex vertex, Vector3 eye)
        {
            Vector3 toEye = eye - new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
            float facingA = Vector3.Dot(vertex.FaceA, toEye), facingB = Vector3.Dot(vertex.FaceB, toEye);

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

        //How many Wanted() scopes are open: a mesh gets fins only when it is built inside one
        private static int _wanted;

        /// <summary>
        /// Says the meshes built until the returned scope is disposed are ones whose outline shows in play - the island,
        /// the ceiling's plate, the gun - and so get fins when <see cref="Enabled"/>. Everything built outside one gets
        /// none: building fins for every procedural mesh measured 177 ms of load on the laptop and 142 000 edges, most
        /// of them the trophy cups' and the wordmark's, whose outlines nobody sees in a level.
        /// </summary>
        public static Scope Wanted()
        {
            _wanted++;

            return default;
        }

        /// <summary>See <see cref="Wanted"/>.</summary>
        public readonly struct Scope : IDisposable
        {
            public void Dispose() => _wanted--;
        }

        //Weak on the vertex buffer: a mesh that is dropped takes its fins' entry with it, and the fins' own buffers go
        //as every graphics resource nobody disposed goes, by the device's finalizer queue
        private static readonly ConditionalWeakTable<VertexBuffer, EdgeFinMesh> _fins = new();

        /// <summary>The fins of the mesh these arrays are, built and kept against its vertex buffer. Nothing unless
        /// <see cref="Enabled"/>.</summary>
        public static void Register(GraphicsDevice device, VertexBuffer meshVertices, ReadOnlySpan<VertexPositionNormalTexture> vertices,
            ReadOnlySpan<int> indices)
        {
            if (!Enabled || _wanted <= 0 || vertices.Length == 0) return;

            Vector3[] positions = new Vector3[vertices.Length], normals = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                positions[i] = vertices[i].Position;
                normals[i] = vertices[i].Normal;
            }

            (EdgeFinVertex[] finVertices, int[] finIndices) = EdgeFinMesh.Build(positions, normals, indices);
            if (finVertices.Length == 0) return;

            _fins.AddOrUpdate(meshVertices, new EdgeFinMesh(device, finVertices, finIndices));

            EdgeCount += finVertices.Length / 4;
        }

        /// <summary>How many edges have been given a fin since the process began, for the host's log.</summary>
        public static int EdgeCount { get; private set; }

        /// <summary><see cref="Register(GraphicsDevice, VertexBuffer, ReadOnlySpan{VertexPositionNormalTexture}, ReadOnlySpan{int})"/>
        /// for a mesh that keeps sixteen-bit indices, which the GPU reads unsigned.</summary>
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
