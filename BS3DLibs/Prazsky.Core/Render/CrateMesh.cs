using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A wooden crate (#257), the obstacle a shot banks off: one part of it per material, at its own size and centred
    /// at the origin — boards (<see cref="Part.Boards"/>), the frame along every edge with a brace across each face
    /// (<see cref="Part.Frame"/>), the dark core showing between the boards (<see cref="Part.Core"/>) and the steel
    /// brackets on the corners (<see cref="Part.Brackets"/>). Drawn from design references rendered for it (both models
    /// drew the same crate: planks, a darker frame, a diagonal brace, metal corners).
    /// <para>
    /// <b>Its outer bounds are the bounds a ball bounces off</b> (<c>Crates</c> in the physics library): the frame stands
    /// flush with them and everything else is set back inside, so the faces the eye reads are the planes the bounce is
    /// solved against. Built at the crate's real size rather than scaled from a unit one, so a long crate's beams are as
    /// thick as a short one's.
    /// </para>
    /// <para>
    /// Wound through <see cref="MeshBuilder"/>, which corrects every face against the normal it is meant to show.
    /// </para>
    /// </summary>
    public sealed class CrateMesh : IProceduralMesh, IDisposable
    {
        public enum Part { Boards, Frame, Core, Brackets }

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>The frame's beams, as a fraction of the crate's smallest size, held between these two thicknesses.</summary>
        private const float BEAM_FRACTION = 0.12f;
        private const float MIN_BEAM = 0.14f;
        private const float MAX_BEAM = 0.32f;

        /// <summary>How wide a board is, about; each face takes the whole number of boards nearest to this.</summary>
        private const float BOARD_WIDTH = 0.42f;

        /// <summary>The gap between two boards, where the dark core shows, as a fraction of a board.</summary>
        private const float BOARD_GAP = 0.08f;

        /// <param name="size">The crate's full size along each axis, world units.</param>
        public CrateMesh(GraphicsDevice device, Vector3 size, Part part)
        {
            Vector3 half = size * Constants.HALF;
            float beam = Math.Clamp(Math.Min(size.X, Math.Min(size.Y, size.Z)) * BEAM_FRACTION, MIN_BEAM, MAX_BEAM);

            MeshBuilder builder = new();

            switch (part)
            {
                case Part.Core:
                    //Set back under the boards, so it only shows in the gaps between them
                    builder.AddBox(Vector3.Zero, new Vector3(half.X - beam * 0.6f, 0f, 0f),
                        new Vector3(0f, half.Y - beam * 0.6f, 0f), new Vector3(0f, 0f, half.Z - beam * 0.6f));
                    break;

                case Part.Frame:
                    AddFrame(builder, half, beam);
                    break;

                case Part.Boards:
                    AddBoards(builder, half, beam);
                    break;

                case Part.Brackets:
                    AddBrackets(builder, half, beam);
                    break;
            }

            (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;
            BoundingSphere = new BoundingSphere(Vector3.Zero, half.Length());
        }

        //The twelve beams along the edges, flush with the bounds, and a brace across each face from corner to corner
        private static void AddFrame(MeshBuilder builder, Vector3 half, float beam)
        {
            float b = beam * Constants.HALF;

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    //Along Z at each (x, y) edge, along Y at each (x, z), along X at each (y, z)
                    builder.AddBox(new Vector3(sx * (half.X - b), sy * (half.Y - b), 0f),
                        new Vector3(b, 0f, 0f), new Vector3(0f, b, 0f), new Vector3(0f, 0f, half.Z));
                    builder.AddBox(new Vector3(sx * (half.X - b), 0f, sy * (half.Z - b)),
                        new Vector3(b, 0f, 0f), new Vector3(0f, half.Y - beam, 0f), new Vector3(0f, 0f, b));
                    builder.AddBox(new Vector3(0f, sx * (half.Y - b), sy * (half.Z - b)),
                        new Vector3(half.X - beam, 0f, 0f), new Vector3(0f, b, 0f), new Vector3(0f, 0f, b));
                }

            //A brace on each face, lying on the boards and a little under the frame's face
            for (int axis = 0; axis < 3; axis++)
                for (int side = -1; side <= 1; side += 2)
                {
                    Face(half, axis, side, out Vector3 outward, out Vector3 u, out Vector3 v, out float hu, out float hv, out float depth);

                    //The inner rectangle the frame leaves, and its diagonal
                    float iu = hu - beam, iv = hv - beam;
                    if (iu <= 0f || iv <= 0f) continue;

                    Vector3 along = Vector3.Normalize(u * iu + v * iv * (axis == 1 ? -1f : 1f));
                    Vector3 across = Vector3.Normalize(Vector3.Cross(outward, along));
                    float length = MathF.Sqrt(iu * iu + iv * iv);
                    float thick = beam * 0.32f;

                    builder.AddBox(outward * (depth - beam * 0.12f - thick), along * length,
                        across * (beam * 0.42f), outward * thick);
                }
        }

        //The boards of every face, inside the frame and set back from it, with a gap between each two
        private static void AddBoards(MeshBuilder builder, Vector3 half, float beam)
        {
            for (int axis = 0; axis < 3; axis++)
                for (int side = -1; side <= 1; side += 2)
                {
                    Face(half, axis, side, out Vector3 outward, out Vector3 u, out Vector3 v, out float hu, out float hv, out float depth);

                    float iu = hu - beam, iv = hv - beam;
                    if (iu <= 0f || iv <= 0f) continue;

                    //Boards run along v (upright on the sides), laid side by side along u
                    int count = Math.Max(1, (int)MathF.Round(2f * iu / BOARD_WIDTH));
                    float pitch = 2f * iu / count;
                    float halfBoard = pitch * (1f - BOARD_GAP) * Constants.HALF;
                    float thick = beam * 0.3f;

                    for (int i = 0; i < count; i++)
                    {
                        float at = -iu + pitch * (i + Constants.HALF);
                        builder.AddBox(outward * (depth - beam * 0.25f - thick) + u * at,
                            u * halfBoard, v * iv, outward * thick);
                    }
                }
        }

        //A steel bracket over each corner, a little proud of the frame
        private static void AddBrackets(MeshBuilder builder, Vector3 half, float beam)
        {
            float b = beam * 0.62f;

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        builder.AddBox(new Vector3(sx * (half.X - b * 0.9f), sy * (half.Y - b * 0.9f), sz * (half.Z - b * 0.9f)),
                            new Vector3(b, 0f, 0f), new Vector3(0f, b, 0f), new Vector3(0f, 0f, b));
        }

        //One face of the crate: its outward axis, two in-plane axes (v upright on the four sides), their half sizes and
        //how far out the face stands
        private static void Face(Vector3 half, int axis, int side, out Vector3 outward, out Vector3 u, out Vector3 v,
            out float hu, out float hv, out float depth)
        {
            switch (axis)
            {
                case 0:
                    outward = Vector3.UnitX * side; u = Vector3.UnitZ; v = Vector3.UnitY;
                    hu = half.Z; hv = half.Y; depth = half.X;
                    break;
                case 1:
                    outward = Vector3.UnitY * side; u = Vector3.UnitX; v = Vector3.UnitZ;
                    hu = half.X; hv = half.Z; depth = half.Y;
                    break;
                default:
                    outward = Vector3.UnitZ * side; u = Vector3.UnitX; v = Vector3.UnitY;
                    hu = half.X; hv = half.Y; depth = half.Z;
                    break;
            }
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer = null;
        }
    }
}
