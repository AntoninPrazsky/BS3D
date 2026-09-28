using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The island sits IN the ground</b> (#608): a bank of earth and turf round the island's foot, geometry and not a
    /// texture (the owner's standing ruling on the island). His report, from the meadow's intro: <i>"an oval is visible
    /// under the island — it does not sink into the ground, a proper seam is missing"</i>. The drum stopped on the grass
    /// with a hard line and a dark band under it, and nothing joined the two.
    /// <para>
    /// <b>Built in world space, off the scene's own ground</b>: the bank's inner edge rides up the drum a little above the
    /// foot, and its outer edge dips just under the terrain the ground mirror reports at that point, so it meets whatever
    /// relief the scene has round the island (the meadow's basin runs a unit and a half either way) without a gap or a
    /// lip. Nothing about the island moves: its radius, top, floor and the terrain hole are what they were; the bank is
    /// drawn over the seam and its inner ring is inside the drum's face where the drum hides it.
    /// </para>
    /// </summary>
    internal static class IslandBerm
    {
        //The bank's section, from the drum out: radius, and height over the ground (or, for the first ring, over the
        //island's foot). The inner ring sits inside the drum's face at the foot, the outer ones slope down onto the field.
        private static readonly (float Radius, float Above)[] SECTION =
        {
            (ArenaIsland.RADIUS - 0.35f, 1.35f),
            (ArenaIsland.RADIUS + 0.5f, 1.05f),
            (ArenaIsland.RADIUS + 1.2f, 0.75f),
            (ArenaIsland.RADIUS + 2.6f, 0.30f),
            (ArenaIsland.RADIUS + 4.4f, -0.25f),
        };

        /// <summary>
        /// Where the earth ends and the turf begins, as a ring of <see cref="SECTION"/>: the bank is two meshes, the
        /// earthen band against the drum and the turf over the rest, because the plant material takes one colour a
        /// draw — and one colour for the whole bank, darker than the field, read as a mat laid round the island.
        /// </summary>
        public const int EARTH_TO = 2;

        /// <summary>The rings of <see cref="SECTION"/>: the cover spans <see cref="EARTH_TO"/> to <c>RINGS - 1</c>.</summary>
        public static int RINGS => SECTION.Length;

        private const int SEGMENTS = 128;

        /// <summary>The island's foot, where the drum meets the ground it was built to stand on.</summary>
        private const float FOOT_Y = ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT;

        /// <param name="height">The scene's ground at a world point (its <see cref="TerrainMirror"/>).</param>
        /// <param name="seed">Rolls the bank's wander, so no two scenes wear the same one.</param>
        /// <param name="fromRing">The first ring of <see cref="SECTION"/> this mesh spans (0 for the earthen band,
        /// <see cref="EARTH_TO"/> for the turf).</param>
        /// <param name="toRing">The last ring it spans.</param>
        public static UploadedMesh Build(GraphicsDevice device, Func<float, float, float> height, int seed, int fromRing, int toRing)
        {
            float phase = seed * 0.6180339f;
            int rings = SECTION.Length;
            var positions = new Vector3[rings, SEGMENTS + 1];

            for (int s = 0; s <= SEGMENTS; s++)
            {
                float a = MathHelper.TwoPi * s / SEGMENTS;
                (float sin, float cos) = MathF.SinCos(a);

                //The bank wanders: wider and higher in places, a slump here and there, off a few sines round the ring
                float spread = 1f + 0.35f * MathF.Sin(a * 3f + phase) + 0.2f * MathF.Sin(a * 7f + phase * 2.3f);
                float lift = 1f + 0.25f * MathF.Sin(a * 5f + phase * 1.7f) + 0.15f * MathF.Sin(a * 11f + phase * 0.9f);

                for (int r = 0; r < rings; r++)
                {
                    (float radius, float above) = SECTION[r];
                    float rad = r == 0 ? radius : ArenaIsland.RADIUS + (radius - ArenaIsland.RADIUS) * spread;
                    float x = cos * rad, z = sin * rad;
                    float ground = height(x, z);
                    float y = r == 0
                        ? MathF.Max(ground, FOOT_Y) + above * lift
                        : ground + (above > 0f ? above * lift : above);
                    positions[r, s] = new Vector3(x, y, z);
                }
            }

            //Normals off the surface itself: the cross of the two grid directions, averaged into each vertex
            var normals = new Vector3[rings, SEGMENTS + 1];
            for (int r = 0; r < rings; r++)
                for (int s = 0; s <= SEGMENTS; s++)
                {
                    Vector3 around = positions[r, Math.Min(s + 1, SEGMENTS)] - positions[r, Math.Max(s - 1, 0)];
                    Vector3 outward = positions[Math.Min(r + 1, rings - 1), s] - positions[Math.Max(r - 1, 0), s];
                    Vector3 n = Vector3.Cross(around, outward);
                    if (n.Y < 0f) n = -n;
                    normals[r, s] = Vector3.Normalize(n);
                }

            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            for (int r = 0; r < rings; r++)
                for (int s = 0; s <= SEGMENTS; s++)
                    v.Add(new VertexPositionNormalTexture(positions[r, s], normals[r, s], new Vector2(0f, 0f)));

            //Wound clockwise seen from above (the project's convention: (b - a) x (c - a) points away from the viewer)
            void Tri(int a, int b, int c)
            {
                Vector3 pa = v[a].Position, pb = v[b].Position, pc = v[c].Position;
                if (Vector3.Cross(pb - pa, pc - pa).Y > 0f) (b, c) = (c, b);
                idx.Add((short)a); idx.Add((short)b); idx.Add((short)c);
            }

            for (int r = fromRing; r < toRing; r++)
                for (int s = 0; s < SEGMENTS; s++)
                {
                    int i00 = r * (SEGMENTS + 1) + s, i01 = i00 + 1;
                    int i10 = i00 + SEGMENTS + 1, i11 = i10 + 1;
                    Tri(i00, i10, i11);
                    Tri(i00, i11, i01);
                }

            float outer = ArenaIsland.RADIUS + 9f;
            return new UploadedMesh(device, v, idx, new BoundingSphere(new Vector3(0f, FOOT_Y, 0f), outer));
        }

        /// <summary>A point on the bank's middle, for a stone or a tuft set into it: its radius from the arena's centre.</summary>
        public const float STONE_RADIUS_MIN = ArenaIsland.RADIUS + 1.2f, STONE_RADIUS_MAX = ArenaIsland.RADIUS + 3.2f;
    }
}
