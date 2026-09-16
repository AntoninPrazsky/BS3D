using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The city's rooftop equipment (#436), one mesh per kind: a triangular lattice mast in red and white
    /// sections, a satellite dish at two elevations with the neon ring its rim wears in the neon city, a pole
    /// of three 5G sector panels, and an air-conditioning unit. Every one stands on <b>y = 0</b> — the roof —
    /// with its own up along +Y, so <see cref="CityRooftops"/> places one with a yaw, a uniform scale and a
    /// translation and nothing else (a uniform scale keeps the normals right under the shared shader, which
    /// transforms them by the world matrix with no inverse transpose).
    /// <para>
    /// <b>Proportions follow the references #441 rendered for this issue</b> — the lattice tower, the dish on
    /// its post with a feed arm at the focus, the three rectangular panels round a pole — simplified to what
    /// reads from the play camera, which sees these as silhouettes against the sky thirty to four hundred units
    /// away. Nothing thinner than about a fiftieth of a mast's height: a strut under a pixel wide does not read
    /// as a strut, it shimmers.
    /// </para>
    /// <para>
    /// <b>The mast is two meshes</b> because its bands are two materials and one renderer is one material: the
    /// red sections and the white ones are built by the same walk, each keeping the members of its own colour,
    /// so the two cannot drift apart.
    /// </para>
    /// </summary>
    public sealed class RooftopMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private RooftopMesh(GraphicsDevice device, MeshBuilder builder, BoundingSphere bounds)
        {
            (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;
            BoundingSphere = bounds;
        }

        //THE MAST, in units of its own height. Six sections, a triangle of legs tapering from the base to the
        //platform, a single zig-zag brace on each face of each section and a ring every other level — the
        //X-braced lattice of the reference is twice the struts, and at a mast's distance the second diagonal
        //is not seen, only paid for. Above the platform a short pole carries the beacon.
        private const int MAST_SECTIONS = 6;
        private const float MAST_PLATFORM_Y = 0.86f;
        private const float MAST_BASE_RADIUS = 0.085f, MAST_TOP_RADIUS = 0.026f;
        private const float MAST_LEG_HALF = 0.0095f, MAST_BRACE_HALF = 0.0060f;

        /// <summary>
        /// The height of a mast's beacon above the roof, in units of the mast's own height — where
        /// <see cref="CityRooftops"/> puts the light.
        /// </summary>
        public const float MAST_TOP = 1f;

        /// <summary>
        /// One colour of the lattice mast: the red sections (the bottom one, every other one above it and the
        /// pole) or the white ones (the rest, and the platform).
        /// </summary>
        public static RooftopMesh CreateMast(GraphicsDevice device, bool red)
        {
            MeshBuilder builder = new();

            Vector3 Leg(int leg, int level)
            {
                float y = MAST_PLATFORM_Y * level / MAST_SECTIONS;
                float radius = MathHelper.Lerp(MAST_BASE_RADIUS, MAST_TOP_RADIUS, level / (float)MAST_SECTIONS);
                float angle = MathHelper.PiOver2 + leg * MathHelper.TwoPi / 3f;
                return new Vector3(radius * MathF.Cos(angle), y, radius * MathF.Sin(angle));
            }

            for (int section = 0; section < MAST_SECTIONS; section++)
            {
                if ((section % 2 == 0) != red) continue;

                for (int leg = 0; leg < 3; leg++)
                {
                    int next = (leg + 1) % 3;

                    Strut(builder, Leg(leg, section), Leg(leg, section + 1), MAST_LEG_HALF);

                    //The zig-zag: alternate faces lean the other way, so the braces read as a lattice
                    if ((section + leg) % 2 == 0) Strut(builder, Leg(leg, section), Leg(next, section + 1), MAST_BRACE_HALF);
                    else Strut(builder, Leg(next, section), Leg(leg, section + 1), MAST_BRACE_HALF);

                    if (section % 2 == 0) Strut(builder, Leg(leg, section), Leg(next, section), MAST_BRACE_HALF);
                }
            }

            if (red)
            {
                //The pole above the platform, up to the beacon
                builder.AddTube(new Vector3(0f, (MAST_PLATFORM_Y + MAST_TOP) * 0.5f, 0f), Vector3.UnitY, Vector3.UnitX,
                    (MAST_TOP - MAST_PLATFORM_Y) * 0.5f, MAST_LEG_HALF * 1.3f, 8);
            }
            else
            {
                //The platform: a flat plate a little wider than the legs at the top
                builder.AddBox(new Vector3(0f, MAST_PLATFORM_Y, 0f),
                    new Vector3(MAST_TOP_RADIUS * 1.9f, 0f, 0f), new Vector3(0f, 0.006f, 0f), new Vector3(0f, 0f, MAST_TOP_RADIUS * 1.9f));
            }

            return new RooftopMesh(device, builder, new BoundingSphere(new Vector3(0f, 0.5f, 0f), 0.52f));
        }

        //THE DISH, in metres at scale 1: a bowl a metre across on a post a little over half a metre tall, the
        //feed on its arm at the bowl's focus. Tilted about the post's top, facing +Z; the instance's yaw turns
        //it. Real dishes over a city all face the same stretch of sky, which CityRooftops honours.
        private const float DISH_RADIUS = 0.5f, DISH_DEPTH = 0.12f, DISH_THICKNESS = 0.02f;
        private const float DISH_PIVOT_Y = 0.62f;
        private const int DISH_SEGMENTS = 18, DISH_RINGS = 4;

        private static Vector3 DishAxis(float elevation) => new(0f, MathF.Sin(elevation), MathF.Cos(elevation));

        /// <summary>A satellite dish on its post, the bowl raised <paramref name="elevation"/> radians from horizontal.</summary>
        public static RooftopMesh CreateDish(GraphicsDevice device, float elevation)
        {
            MeshBuilder builder = new();

            //The post, its foot plate and the yoke at the top
            builder.AddTube(new Vector3(0f, DISH_PIVOT_Y * 0.5f, 0f), Vector3.UnitY, Vector3.UnitX, DISH_PIVOT_Y * 0.5f, 0.045f, 8);
            builder.AddBox(new Vector3(0f, 0.015f, 0f), new Vector3(0.16f, 0f, 0f), new Vector3(0f, 0.015f, 0f), new Vector3(0f, 0f, 0.16f));
            builder.AddBox(new Vector3(0f, DISH_PIVOT_Y, 0f), new Vector3(0.07f, 0f, 0f), new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0f, 0.05f));

            Vector3 axis = DishAxis(elevation);
            Vector3 right = Vector3.UnitX;
            Vector3 upInBowl = Vector3.Cross(axis, right);
            Vector3 pivot = new(0f, DISH_PIVOT_Y, 0f);
            float k = DISH_DEPTH / (DISH_RADIUS * DISH_RADIUS);

            Vector3 Front(float r, float angle) => pivot + axis * (k * r * r) + (right * MathF.Cos(angle) + upInBowl * MathF.Sin(angle)) * r;

            Vector3 FrontNormal(float r, float angle)
            {
                Vector3 radial = right * MathF.Cos(angle) + upInBowl * MathF.Sin(angle);
                return Vector3.Normalize(axis - radial * (2f * k * r));
            }

            for (int ring = 0; ring < DISH_RINGS; ring++)
            {
                float r0 = DISH_RADIUS * ring / DISH_RINGS, r1 = DISH_RADIUS * (ring + 1) / DISH_RINGS;

                for (int s = 0; s < DISH_SEGMENTS; s++)
                {
                    float a0 = s * MathHelper.TwoPi / DISH_SEGMENTS, a1 = (s + 1) * MathHelper.TwoPi / DISH_SEGMENTS;

                    //The concave face, towards the sky…
                    Vector3 f00 = Front(r0, a0), f01 = Front(r0, a1), f10 = Front(r1, a0), f11 = Front(r1, a1);
                    Vector3 n00 = FrontNormal(r0, a0), n01 = FrontNormal(r0, a1), n10 = FrontNormal(r1, a0), n11 = FrontNormal(r1, a1);
                    Vector3 face = Vector3.Normalize(n00 + n01 + n10 + n11);

                    //…and the back, the same surface a shell's thickness behind it, facing away
                    Vector3 back = -axis * DISH_THICKNESS;

                    if (ring == 0)
                    {
                        builder.AddTriangle(f00, f10, f11, n00, n10, n11, face);
                        builder.AddTriangle(f00 + back, f10 + back, f11 + back, -n00, -n10, -n11, -face);
                    }
                    else
                    {
                        builder.AddQuad(f00, f10, f11, f01, n00, n10, n11, n01, face);
                        builder.AddQuad(f00 + back, f10 + back, f11 + back, f01 + back, -n00, -n10, -n11, -n01, -face);
                    }

                    if (ring == DISH_RINGS - 1)
                    {
                        //The rim's own edge, between the two faces
                        Vector3 radial0 = right * MathF.Cos(a0) + upInBowl * MathF.Sin(a0);
                        Vector3 radial1 = right * MathF.Cos(a1) + upInBowl * MathF.Sin(a1);
                        builder.AddQuad(f10, f11, f11 + back, f10 + back, radial0, radial1, radial1, radial0,
                            Vector3.Normalize(radial0 + radial1));
                    }
                }
            }

            //The feed arm out to the focus, and the feed itself
            float focus = DISH_RADIUS * DISH_RADIUS / (4f * DISH_DEPTH);
            builder.AddTube(pivot + axis * (focus * 0.5f), axis, right, focus * 0.5f, 0.012f, 6);
            builder.AddBox(pivot + axis * focus, right * 0.035f, upInBowl * 0.035f, axis * 0.045f);

            return new RooftopMesh(device, builder, new BoundingSphere(pivot * 0.8f, 0.9f));
        }

        /// <summary>
        /// The neon ring on a dish's rim, for the neon city: a thin tube round the bowl's edge at the same
        /// <paramref name="elevation"/> as <see cref="CreateDish"/>, so it shares the dish's placement exactly.
        /// </summary>
        public static RooftopMesh CreateDishRing(GraphicsDevice device, float elevation)
        {
            MeshBuilder builder = new();

            Vector3 axis = DishAxis(elevation);
            Vector3 right = Vector3.UnitX;
            Vector3 upInBowl = Vector3.Cross(axis, right);
            Vector3 rimCentre = new Vector3(0f, DISH_PIVOT_Y, 0f) + axis * (DISH_DEPTH - DISH_THICKNESS * 0.5f);

            const int STEPS = 32;
            Vector3[] path = new Vector3[STEPS + 1];
            for (int i = 0; i <= STEPS; i++)
            {
                float a = i * MathHelper.TwoPi / STEPS;
                path[i] = rimCentre + (right * MathF.Cos(a) + upInBowl * MathF.Sin(a)) * (DISH_RADIUS + 0.01f);
            }

            builder.AddSweptTube(path, axis, 0.022f, 6);

            return new RooftopMesh(device, builder, new BoundingSphere(rimCentre, DISH_RADIUS + 0.05f));
        }

        //THE 5G POLE, in units of its height: a slim pole with three sector panels round its head at 120°,
        //each on two brackets — the reference's panels at their own proportion, a panel about a tenth of the
        //pole's height across and a third of it tall.
        private const float SECTOR_PANEL_Y = 0.80f, SECTOR_PANEL_RADIUS = 0.08f;

        /// <summary>A pole carrying three 5G sector panels.</summary>
        public static RooftopMesh CreateSectorPole(GraphicsDevice device)
        {
            MeshBuilder builder = new();

            builder.AddTube(new Vector3(0f, 0.5f, 0f), Vector3.UnitY, Vector3.UnitX, 0.5f, 0.018f, 8);
            builder.AddBox(new Vector3(0f, 0.008f, 0f), new Vector3(0.06f, 0f, 0f), new Vector3(0f, 0.008f, 0f), new Vector3(0f, 0f, 0.06f));

            for (int i = 0; i < 3; i++)
            {
                float angle = i * MathHelper.TwoPi / 3f;
                Vector3 outward = new(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 along = new(-MathF.Sin(angle), 0f, MathF.Cos(angle));

                builder.AddBox(new Vector3(0f, SECTOR_PANEL_Y, 0f) + outward * SECTOR_PANEL_RADIUS,
                    along * 0.045f, Vector3.UnitY * 0.16f, outward * 0.013f);

                foreach (float y in new[] { SECTOR_PANEL_Y - 0.1f, SECTOR_PANEL_Y + 0.1f })
                    builder.AddBox(new Vector3(0f, y, 0f) + outward * (SECTOR_PANEL_RADIUS * 0.5f),
                        outward * (SECTOR_PANEL_RADIUS * 0.5f), Vector3.UnitY * 0.006f, along * 0.006f);
            }

            return new RooftopMesh(device, builder, new BoundingSphere(new Vector3(0f, 0.5f, 0f), 0.52f));
        }

        /// <summary>
        /// An air-conditioning unit in metres at scale 1: a cabinet on two rails with two fans on its top — the
        /// clutter that makes a flat roof read as a roof from above, where the tall kinds are seen end-on.
        /// </summary>
        public static RooftopMesh CreateHvac(GraphicsDevice device)
        {
            MeshBuilder builder = new();

            builder.AddBox(new Vector3(0f, 0.53f, 0f), new Vector3(0.8f, 0f, 0f), new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0f, 0.5f));

            foreach (float x in new[] { -0.55f, 0.55f })
                builder.AddBox(new Vector3(x, 0.04f, 0f), new Vector3(0.06f, 0f, 0f), new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0f, 0.55f));

            foreach (float x in new[] { -0.38f, 0.38f })
                builder.AddTube(new Vector3(x, 1.03f, 0f), Vector3.UnitY, Vector3.UnitX, 0.05f, 0.3f, 12);

            return new RooftopMesh(device, builder, new BoundingSphere(new Vector3(0f, 0.55f, 0f), 1.05f));
        }

        /// <summary>Strut between two points, a square bar <paramref name="half"/> thick either side of its line.</summary>
        private static void Strut(MeshBuilder builder, Vector3 from, Vector3 to, float half)
        {
            Vector3 along = (to - from) * 0.5f;
            Vector3 direction = Vector3.Normalize(along);
            Vector3 reference = MathF.Abs(direction.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 side = Vector3.Normalize(Vector3.Cross(direction, reference));
            Vector3 third = Vector3.Cross(direction, side);

            builder.AddBox((from + to) * 0.5f, along, side * half, third * half);
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
