using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A special kind's shape on the Potato path (#863), as a mesh of its own: what the desktop's vertex shaders carve,
    /// raise or cut out of the sphere per vertex every frame (<c>StoneVS</c>, <c>BombVS</c>, <c>FrozenVS</c>), done once
    /// on the CPU and drawn through the same instance buffer as the ball it stands for, under <c>PotatoBall</c>. The
    /// Potato effect has no vertex shader per kind and must not get one: a special kind is a handful of balls in a level,
    /// so its vertices cost a few hundred where a pixel of shader on every ball would cost the frame (the issue's rule).
    /// <para>
    /// Two of the three REPLACE the sphere for their kind's draw (the rock's carve and the ice block are the ball's own
    /// surface, inward only, so the sphere's topology carries them: <see cref="PotatoKindGeometry.Displaced"/>), and one
    /// is drawn AFTER it (the mine's eight studs stand past the casing, #860, so they are a mesh of their own over the
    /// sphere: <see cref="PotatoKindGeometry.MineStuds"/>). <c>InstancedModelRenderer.DrawPotato</c> knows which is which.
    /// </para>
    /// </summary>
    public sealed class PotatoKindMesh : IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }

        public PotatoKindMesh(GraphicsDevice graphicsDevice, (VertexPositionNormalTexture[] Vertices, short[] Indices) geometry)
        {
            PrimitiveCount = geometry.Indices.Length / 3;

            VertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.VertexDeclaration, geometry.Vertices.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(geometry.Vertices);

            IndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, geometry.Indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(geometry.Indices);
        }

        /// <summary>The rock (<see cref="PotatoKindGeometry.RockShape"/>) on the sphere of this radius and resolution.</summary>
        public static PotatoKindMesh Rock(GraphicsDevice graphicsDevice, float radius, int slices, int stacks, float shapeDepth) =>
            new(graphicsDevice, PotatoKindGeometry.Displaced(radius, slices, stacks,
                (Vector3 direction, out float r, out Vector3 n) => PotatoKindGeometry.RockShape(direction, shapeDepth, out r, out n)));

        /// <summary>The ice block (<see cref="PotatoKindGeometry.IceBlockShape"/>) on the sphere of this radius and resolution.</summary>
        public static PotatoKindMesh IceBlock(GraphicsDevice graphicsDevice, float radius, int slices, int stacks) =>
            new(graphicsDevice, PotatoKindGeometry.Displaced(radius, slices, stacks, PotatoKindGeometry.IceBlockShape));

        /// <summary>The mine's eight studs (<see cref="PotatoKindGeometry.MineStuds"/>) round a sphere of this radius.</summary>
        public static PotatoKindMesh MineStuds(GraphicsDevice graphicsDevice, float radius) =>
            new(graphicsDevice, PotatoKindGeometry.MineStuds(radius));

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }

    /// <summary>
    /// The geometry of <see cref="PotatoKindMesh"/>, with no device: the desktop's shape functions ported figure for
    /// figure (every constant here is the shader's, named beside it), so the Pi's rock, ice and mine are the desktop's
    /// shapes at a vertex's resolution. Pure, so <c>PotatoKindMeshTests</c> can hold them: inward only where the desktop
    /// is, the studs past the casing by what the desktop raises them, and every triangle wound clockwise seen from
    /// outside (CLAUDE.md's rule, the sphere's own words).
    /// </summary>
    public static class PotatoKindGeometry
    {
        /// <summary>A kind's surface in a direction from the ball's centre: the radius as a share of the sphere's, and the normal there.</summary>
        public delegate void Shape(Vector3 direction, out float radius, out Vector3 normal);

        /// <summary>
        /// <c>SphereMesh</c>'s own vertex walk and index pattern (its topology, and its winding), each vertex pushed to
        /// where <paramref name="shape"/> puts the surface in its direction and given the normal the shape has there. A
        /// mesh with the sphere's vertex count, so a level's LOD ladder applies to it unchanged.
        /// </summary>
        public static (VertexPositionNormalTexture[] Vertices, short[] Indices) Displaced(float radius, int slices, int stacks, Shape shape)
        {
            if (slices < 3) throw new ArgumentOutOfRangeException(nameof(slices));
            if (stacks < 2) throw new ArgumentOutOfRangeException(nameof(stacks));

            int vertexCount = (stacks - 1) * slices + 2;
            var vertices = new VertexPositionNormalTexture[vertexCount];

            vertices[0] = Shaped(Vector3.Up, radius, shape, new Vector2(0.5f, 0f));

            int index = 1;
            for (int stack = 1; stack < stacks; stack++)
            {
                float phi = MathF.PI * stack / stacks;
                float y = MathF.Cos(phi);
                float ringRadius = MathF.Sin(phi);

                for (int slice = 0; slice < slices; slice++)
                {
                    float theta = MathHelper.TwoPi * slice / slices;
                    Vector3 direction = new(ringRadius * MathF.Cos(theta), y, ringRadius * MathF.Sin(theta));

                    vertices[index++] = Shaped(direction, radius, shape, new Vector2((float)slice / slices, (float)stack / stacks));
                }
            }

            int bottomPole = index;
            vertices[bottomPole] = Shaped(Vector3.Down, radius, shape, new Vector2(0.5f, 1f));

            int primitiveCount = slices * 2 + (stacks - 2) * slices * 2;
            var indices = new short[primitiveCount * 3];
            int i = 0;

            for (int slice = 0; slice < slices; slice++)
            {
                indices[i++] = 0;
                indices[i++] = (short)(1 + slice);
                indices[i++] = (short)(1 + (slice + 1) % slices);
            }

            for (int stack = 0; stack < stacks - 2; stack++)
            {
                int upperRing = 1 + stack * slices;
                int lowerRing = upperRing + slices;

                for (int slice = 0; slice < slices; slice++)
                {
                    int next = (slice + 1) % slices;

                    indices[i++] = (short)(upperRing + slice);
                    indices[i++] = (short)(lowerRing + slice);
                    indices[i++] = (short)(upperRing + next);

                    indices[i++] = (short)(upperRing + next);
                    indices[i++] = (short)(lowerRing + slice);
                    indices[i++] = (short)(lowerRing + next);
                }
            }

            int lastRing = 1 + (stacks - 2) * slices;
            for (int slice = 0; slice < slices; slice++)
            {
                indices[i++] = (short)bottomPole;
                indices[i++] = (short)(lastRing + (slice + 1) % slices);
                indices[i++] = (short)(lastRing + slice);
            }

            return (vertices, indices);
        }

        private static VertexPositionNormalTexture Shaped(Vector3 direction, float radius, Shape shape, Vector2 texture)
        {
            shape(direction, out float r, out Vector3 normal);
            return new VertexPositionNormalTexture(direction * (radius * r), normal, texture);
        }

        //BallStone.fxh's StoneShapeFrequency, StoneShapeWeight, StoneShapeAxisA..D, StoneShapePower, StonePlaneA..E,
        //StonePlaneOffset and StonePlaneOffsetE (#340, #623)
        private static readonly float[] STONE_FREQUENCY = { 1.7f, 2.6f, 3.9f, 5.7f };
        private static readonly float[] STONE_WEIGHT = { 0.40f, 0.28f, 0.19f, 0.13f };
        private static readonly Vector3[] STONE_AXIS =
        {
            new(0.68f, 0.47f, -0.56f), new(-0.39f, 0.81f, 0.44f), new(0.53f, -0.35f, 0.77f), new(-0.77f, -0.33f, 0.55f)
        };
        private const float STONE_SHAPE_POWER = 1.8f;
        private static readonly Vector3[] STONE_PLANE =
        {
            new(0.803f, 0.351f, -0.482f), new(-0.551f, 0.702f, 0.451f), new(0.199f, -0.845f, 0.497f),
            new(-0.601f, -0.300f, -0.741f), new(0.100f, 0.301f, -0.949f)
        };
        private static readonly float[] STONE_PLANE_OFFSET = { 0.74f, 0.86f, 0.82f, 0.80f, 0.83f };

        /// <summary>
        /// The rock's surface (<c>StoneVS</c>, #340, #623): the sum-of-sines gouge carve through its power, inward only by
        /// at most <paramref name="shapeDepth"/> of the radius, and the five fracture planes cutting flat faces where
        /// they go deeper than the gouges, with the plane's own normal. Figure for figure the shader's.
        /// </summary>
        public static void RockShape(Vector3 direction, float shapeDepth, out float radius, out Vector3 normal)
        {
            float shape = 0f;
            Vector3 gradient = Vector3.Zero;
            for (int k = 0; k < 4; k++)
            {
                float phase = Vector3.Dot(direction, STONE_AXIS[k]) * STONE_FREQUENCY[k];
                shape += MathF.Sin(phase) * STONE_WEIGHT[k];
                gradient += STONE_AXIS[k] * (MathF.Cos(phase) * STONE_FREQUENCY[k] * STONE_WEIGHT[k]);
            }

            float bite = MathHelper.Clamp(0.5f + 0.5f * shape, 0f, 1f);
            float carve = 1f - shapeDepth * MathF.Pow(bite, STONE_SHAPE_POWER);

            float slope = shapeDepth * 0.5f * STONE_SHAPE_POWER * MathF.Pow(MathF.Max(bite, 1e-4f), STONE_SHAPE_POWER - 1f);
            Vector3 tangential = gradient - direction * Vector3.Dot(gradient, direction);
            normal = Vector3.Normalize(direction + tangential * (slope / carve));

            //The breaks: the nearest plane that cuts inside the gouged surface
            float best = 1f;
            Vector3 planeNormal = direction;
            for (int k = 0; k < STONE_PLANE.Length; k++)
            {
                float along = Vector3.Dot(direction, STONE_PLANE[k]);
                float cut = STONE_PLANE_OFFSET[k] / MathF.Max(along, 1e-3f);
                if (along > STONE_PLANE_OFFSET[k] && cut < best)
                {
                    best = cut;
                    planeNormal = STONE_PLANE[k];
                }
            }

            if (best < carve)
            {
                carve = best;
                //The shader hands the plane's constant over as it is; a unit normal here, since the constants are unit to three places
                normal = Vector3.Normalize(planeNormal);
            }

            radius = carve;
        }

        //BallFrozen.fxh's FrozenCubePower (#628)
        private const float ICE_CUBE_POWER = 4.5f;

        /// <summary>
        /// The ice block's surface (<c>FrozenVS</c>, <c>FrozenCarve</c>, #628): the rounded cube
        /// |x|^p + |y|^p + |z|^p = const at p 4.5, scaled so its corners touch the sphere (inward only: its faces sit at
        /// 3^(1/p - 1/2) of the radius, 0.74), with the solid's own gradient as the normal.
        /// </summary>
        public static void IceBlockShape(Vector3 direction, out float radius, out Vector3 normal)
        {
            Vector3 axis = Vector3.Max(new Vector3(MathF.Abs(direction.X), MathF.Abs(direction.Y), MathF.Abs(direction.Z)), new Vector3(1e-5f));
            float sum = MathF.Pow(axis.X, ICE_CUBE_POWER) + MathF.Pow(axis.Y, ICE_CUBE_POWER) + MathF.Pow(axis.Z, ICE_CUBE_POWER);

            radius = MathF.Pow(sum, -1f / ICE_CUBE_POWER) / MathF.Pow(3f, 0.5f - 1f / ICE_CUBE_POWER);

            normal = Vector3.Normalize(new Vector3(
                MathF.Pow(axis.X, ICE_CUBE_POWER - 1f) * MathF.Sign(direction.X),
                MathF.Pow(axis.Y, ICE_CUBE_POWER - 1f) * MathF.Sign(direction.Y),
                MathF.Pow(axis.Z, ICE_CUBE_POWER - 1f) * MathF.Sign(direction.Z)));
        }

        //BallBomb.fxh's MineStudRadius, MineStudRise and MineStudTop (#625, #860)
        public const float MINE_STUD_RADIUS = 0.22f;
        public const float MINE_STUD_RISE = 0.13f;
        private const float MINE_STUD_TOP = 0.55f;
        private const int STUD_SIDES = 8;

        /// <summary>
        /// The mine's eight studs (<c>BombVS</c>, <c>MineRadius</c>, #625): blunt cones on the cube's diagonals, each a
        /// frustum from the casing at the sphere's radius (an angular radius of <see cref="MINE_STUD_RADIUS"/> round its
        /// diagonal) to a flat top <see cref="MINE_STUD_RISE"/> past it, the top as wide as the desktop's flat (the
        /// profile's last <c>MineStudTop</c>). Eight sides each: a stud is a few pixels across at play distance. A mesh of
        /// its own, drawn over the casing: a stud is three to five vertices across on the desktop's finest sphere and
        /// not one on the Pi's play sphere (24 x 12), so raising the sphere's own vertices would raise nothing.
        /// </summary>
        public static (VertexPositionNormalTexture[] Vertices, short[] Indices) MineStuds(float radius)
        {
            //Per stud: a base ring, a top ring for the flank, the same top ring again for the cap (its own normal), the cap's centre
            const int perStud = STUD_SIDES * 3 + 1;
            var vertices = new VertexPositionNormalTexture[8 * perStud];
            var indices = new short[8 * (STUD_SIDES * 2 + STUD_SIDES) * 3];
            int v = 0, i = 0;

            float baseAngle = MINE_STUD_RADIUS;
            float topAngle = MINE_STUD_RADIUS * (1f - MINE_STUD_TOP);
            float topRadius = 1f + MINE_STUD_RISE;

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 axis = Vector3.Normalize(new Vector3(sx, sy, sz));
                        Vector3 u = Vector3.Normalize(Vector3.Cross(axis, Vector3.Up));
                        Vector3 w = Vector3.Cross(axis, u);

                        int first = v;
                        for (int s = 0; s < STUD_SIDES; s++)
                        {
                            float around = MathHelper.TwoPi * s / STUD_SIDES;
                            Vector3 spoke = u * MathF.Cos(around) + w * MathF.Sin(around);

                            Vector3 baseDirection = axis * MathF.Cos(baseAngle) + spoke * MathF.Sin(baseAngle);
                            Vector3 topDirection = axis * MathF.Cos(topAngle) + spoke * MathF.Sin(topAngle);
                            Vector3 basePoint = baseDirection * radius;
                            Vector3 topPoint = topDirection * (radius * topRadius);

                            //The flank's normal: across the slant and the ring's own tangent, outward
                            Vector3 tangent = -u * MathF.Sin(around) + w * MathF.Cos(around);
                            Vector3 flank = Vector3.Normalize(Vector3.Cross(tangent, topPoint - basePoint));
                            if (Vector3.Dot(flank, baseDirection) < 0f) flank = -flank;

                            vertices[first + s] = new(basePoint, flank, Vector2.Zero);
                            vertices[first + STUD_SIDES + s] = new(topPoint, flank, Vector2.Zero);
                            vertices[first + 2 * STUD_SIDES + s] = new(topPoint, axis, Vector2.Zero);
                        }

                        int centre = first + 3 * STUD_SIDES;
                        vertices[centre] = new(axis * (radius * topRadius), axis, Vector2.Zero);
                        v = centre + 1;

                        for (int s = 0; s < STUD_SIDES; s++)
                        {
                            int next = (s + 1) % STUD_SIDES;
                            int b0 = first + s, b1 = first + next, t0 = first + STUD_SIDES + s, t1 = first + STUD_SIDES + next;
                            int c0 = first + 2 * STUD_SIDES + s, c1 = first + 2 * STUD_SIDES + next;

                            //Clockwise seen from outside (CLAUDE.md): the flank's two triangles and the cap's one, checked
                            //against the vertices' own outward normals so the winding does not rest on the ring's sense
                            Wound(vertices, indices, ref i, b0, t0, b1);
                            Wound(vertices, indices, ref i, b1, t0, t1);
                            Wound(vertices, indices, ref i, centre, c0, c1);
                        }
                    }

            return (vertices, indices);
        }

        //Writes a triangle wound so that (b - a) x (c - a) points AGAINST its vertices' outward normal, which is the front
        //face under CullCounterClockwiseFace (the viewport transform flips Y: CLAUDE.md, "Triangle winding")
        private static void Wound(VertexPositionNormalTexture[] vertices, short[] indices, ref int i, int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(vertices[b].Position - vertices[a].Position, vertices[c].Position - vertices[a].Position);
            Vector3 outward = vertices[a].Normal + vertices[b].Normal + vertices[c].Normal;

            if (Vector3.Dot(cross, outward) > 0f) (b, c) = (c, b);

            indices[i++] = (short)a;
            indices[i++] = (short)b;
            indices[i++] = (short)c;
        }
    }
}
