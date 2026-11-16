using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The meshes of the sea's lighthouse, container ship and yacht (#1125), each in its own frame: +Y up from the waterline,
    /// and for a vessel +X its bow and +Z its starboard. Triangle lists of <see cref="RigVertex"/> for <c>Rig.fx</c>, built on
    /// the CPU without a device so the tests read the very triangles that are drawn.
    /// <para>
    /// Sizes at about two units a metre, the scenes' own: a feeder container ship 130 m long with seven bays of boxes in
    /// muted, uneven colours and its bridge at the stern; a 13 m yacht running before the wind; a lighthouse some 28 m tall
    /// on a low rock with a keeper's house. Seen from round the arena they stand 900 to 1200 out, a few tens of pixels
    /// to a hundred tall at 1080 lines, so what carries them is the silhouette and the colours, not the detail.
    /// </para>
    /// <para>
    /// <b>Wound clockwise seen from outside</b> (the repository's rule, CLAUDE.md "Triangle winding"): every triangle goes
    /// through <c>Builder.Triangle</c>, which turns it so its geometric normal points away from the normal its
    /// vertices are lit by. They are drawn <c>CullNone</c> all the same, for the sails, which are single sheets.
    /// </para>
    /// </summary>
    internal static class SeaTrafficMeshes
    {
        //The rock: dark, wet stone, a pale crust of lichen and gulls on some of its upper faces
        private static readonly Vector3 ROCK = new(0.10f, 0.095f, 0.085f);
        private static readonly Vector3 ROCK_CRUST = new(0.30f, 0.29f, 0.25f);
        private static readonly Vector3 STONE = new(0.30f, 0.29f, 0.27f);
        private static readonly Vector3 TOWER_WHITE = new(0.66f, 0.65f, 0.62f);
        private static readonly Vector3 TOWER_RED = new(0.42f, 0.05f, 0.04f);
        private static readonly Vector3 IRON = new(0.06f, 0.06f, 0.06f);
        private static readonly Vector3 LANTERN = new(1.00f, 0.86f, 0.60f);
        private static readonly Vector3 HOUSE_WHITE = new(0.60f, 0.59f, 0.56f);
        private static readonly Vector3 ROOF_RED = new(0.35f, 0.06f, 0.04f);

        //The ship: a near-black hull over a red boot-top, a rust deck, a white superstructure, a dark funnel with a blue band
        private static readonly Vector3 HULL_DARK = new(0.035f, 0.045f, 0.07f);
        private static readonly Vector3 BOOT_RED = new(0.30f, 0.06f, 0.04f);
        private static readonly Vector3 DECK_RUST = new(0.20f, 0.12f, 0.09f);
        private static readonly Vector3 SUPER_WHITE = new(0.60f, 0.60f, 0.57f);
        private static readonly Vector3 GLASS = new(0.04f, 0.05f, 0.06f);
        private static readonly Vector3 WINDOW_LIT = new(0.30f, 0.22f, 0.12f);
        private static readonly Vector3 FUNNEL = new(0.12f, 0.12f, 0.13f);
        private static readonly Vector3 FUNNEL_BAND = new(0.04f, 0.10f, 0.32f);
        private static readonly Vector3 MAST_GREY = new(0.35f, 0.35f, 0.36f);
        private static readonly Vector3 LIFEBOAT = new(0.65f, 0.22f, 0.03f);
        private static readonly Vector3 MASTHEAD = new(1.00f, 0.95f, 0.85f);

        //A container's paint, faded and uneven: rust, navy, a lighter blue, grey, white, green, orange, maroon, teal, mustard
        private static readonly Vector3[] CONTAINERS =
        {
            new(0.32f, 0.07f, 0.04f), new(0.05f, 0.10f, 0.26f), new(0.16f, 0.30f, 0.42f), new(0.28f, 0.29f, 0.29f),
            new(0.55f, 0.55f, 0.53f), new(0.07f, 0.20f, 0.10f), new(0.55f, 0.20f, 0.03f), new(0.22f, 0.04f, 0.05f),
            new(0.04f, 0.18f, 0.20f), new(0.50f, 0.36f, 0.04f),
        };

        //The yacht: a white hull over a navy boot-top, a teak deck, a silver mast, white sails
        private static readonly Vector3 YACHT_WHITE = new(0.62f, 0.62f, 0.60f);
        private static readonly Vector3 YACHT_NAVY = new(0.04f, 0.06f, 0.12f);
        private static readonly Vector3 TEAK = new(0.42f, 0.36f, 0.28f);
        private static readonly Vector3 SPAR = new(0.45f, 0.46f, 0.48f);
        private static readonly Vector3 SAILCLOTH = new(0.70f, 0.70f, 0.68f);

        /// <summary>How far under the waterline a hull or the rock goes on, so the cut at the water (<c>Rig.fx</c>) is a clean line.</summary>
        private const float DRAUGHT = 5f;

        //How far over the water the rock's waterline ring stands: under the foam's band, so the surf runs round its foot
        private const float WATERLINE_RISE = 0.2f;

        /// <summary>
        /// The lighthouse on its rock: a low, uneven rock some 24 out from its middle at the waterline, higher on one side
        /// (faceted, dark and wet, with pale crust on some of its upper faces) with a few boulders off its foot, so the surf
        /// round it is broken; a tower tapering from a stone plinth to a red band under the gallery, with a door and a few
        /// small windows up it, the lantern lit and a red cap; and a keeper's house on the rock beside it.
        /// </summary>
        internal static List<RigVertex> Lighthouse()
        {
            var b = new Builder();
            var random = new Random(1125);
            Rock(b, Vector3.Zero, 24f, 6.5f, 2.2f, 28, random);
            for (int i = 0; i < 6; i++)
            {
                float a = i * MathHelper.TwoPi / 6f + (float)random.NextDouble() * 0.7f, out0 = 25f + (float)random.NextDouble() * 6f;
                float size = 2.5f + (float)random.NextDouble() * 3f;
                Rock(b, new Vector3(MathF.Cos(a) * out0, 0f, MathF.Sin(a) * out0), size, size * 0.7f, size * 0.15f, 9, random);
            }

            b.Frustum(new Vector3(0f, 4.5f, 0f), 5.8f, 9f, 5.4f, 16, STONE, RigVertex.PAINTED);
            b.Frustum(new Vector3(0f, 9f, 0f), 4.6f, 41f, 3.6f, 16, TOWER_WHITE, RigVertex.PAINTED);
            b.Frustum(new Vector3(0f, 41f, 0f), 3.6f, 50f, 3.3f, 16, TOWER_RED, RigVertex.PAINTED);
            b.Frustum(new Vector3(0f, 50f, 0f), 4.6f, 51.2f, 4.6f, 16, IRON, RigVertex.PAINTED);
            b.Frustum(new Vector3(0f, 51.2f, 0f), 2.7f, 55.6f, 2.7f, 12, LANTERN, RigVertex.LIGHT);
            b.Frustum(new Vector3(0f, 55.6f, 0f), 3.1f, 59f, 0.3f, 12, TOWER_RED, RigVertex.PAINTED);
            b.Box(new Vector3(0f, 60f, 0f), new Vector3(0.15f, 1f, 0.15f), IRON, RigVertex.PAINTED);

            //The door at its foot and the stair's small windows up it, each a dark slot standing just out of the wall
            foreach ((float y, float turn, float halfHeight) in new[] { (11f, 0.2f, 1.6f), (19f, 1.5f, 0.9f), (27f, 3.1f, 0.9f), (35f, 4.6f, 0.9f) })
            {
                float wall = MathHelper.Lerp(4.6f, 3.6f, (y - 9f) / 32f);
                b.Box(new Vector3(MathF.Cos(turn) * wall, y, MathF.Sin(turn) * wall), new Vector3(0.2f, halfHeight, 0.55f), GLASS,
                    RigVertex.PAINTED, -turn);
            }

            //The keeper's house beside it, its floor sunk into the rock, under a red pitched roof
            b.Box(new Vector3(11f, 6.5f, 0f), new Vector3(4.5f, 2.5f, 3.5f), HOUSE_WHITE, RigVertex.PAINTED);
            b.Roof(new Vector3(11f, 9f, 0f), 4.9f, 3.9f, 2.4f, ROOF_RED);
            return b.Vertices;
        }

        /// <summary>
        /// A rock about <paramref name="centre"/>, some <paramref name="radius"/> out at the waterline and
        /// <paramref name="height"/> high: rings of uneven radius and height round a low crown, its top tipped
        /// <paramref name="tilt"/> higher on one side than the other, faceted, going on under the water to a flat foot. Lit flat,
        /// a face at a time.
        /// </summary>
        private static void Rock(Builder b, Vector3 centre, float radius, float height, float tilt, int segments, Random random)
        {
            float[] rings = { 0f, 0.3f, 0.58f, 0.82f, 1f, 1.14f };
            float[] lift = new float[segments * rings.Length];
            float[] reach = new float[segments];
            float p1 = (float)random.NextDouble() * MathHelper.TwoPi, p2 = (float)random.NextDouble() * MathHelper.TwoPi;
            float high = (float)random.NextDouble() * MathHelper.TwoPi;
            for (int s = 0; s < segments; s++)
            {
                float a = s * MathHelper.TwoPi / segments;
                reach[s] = 1f + 0.14f * MathF.Sin(2f * a + p1) + 0.08f * MathF.Sin(5f * a + p2) + 0.08f * ((float)random.NextDouble() - 0.5f);
                for (int r = 1; r < rings.Length - 1; r++) lift[s * rings.Length + r] = ((float)random.NextDouble() - 0.5f) * height * 0.45f;
            }

            //Inside the waterline's ring the crown falls away unevenly, higher towards one side; on it the rock stands just out of
            //the water, and past it the rock goes on down under the water
            Vector3 Point(int s, int r)
            {
                s %= segments;
                float f = rings[r], a = s * MathHelper.TwoPi / segments;
                float y = f < 1f
                    ? height * MathF.Pow(1f - f * f, 0.6f) + lift[s * rings.Length + r] * (1f - f) + tilt * f * (1f - f) * 4f * MathF.Cos(a - high)
                    : f == 1f ? WATERLINE_RISE : -DRAUGHT;
                float out0 = radius * f * reach[s];
                return centre + new Vector3(MathF.Cos(a) * out0, y, MathF.Sin(a) * out0);
            }

            Vector3 crown = centre + new Vector3(0f, height + height * 0.1f, 0f);
            for (int s = 0; s < segments; s++)
            {
                RockFace(b, centre, crown, Point(s + 1, 1), Point(s, 1), random);
                for (int r = 1; r < rings.Length - 1; r++)
                {
                    RockFace(b, centre, Point(s, r), Point(s + 1, r), Point(s + 1, r + 1), random);
                    RockFace(b, centre, Point(s, r), Point(s + 1, r + 1), Point(s, r + 1), random);
                }
                //The foot, under the water
                b.Triangle(centre + new Vector3(0f, -DRAUGHT, 0f), Point(s, rings.Length - 1), Point(s + 1, rings.Length - 1), -Vector3.UnitY, ROCK, RigVertex.PAINTED);
            }
        }

        //One face of a rock about its centre, lit flat: its normal the one that points out and up, its colour the stone's with a
        //little of its own, or the pale crust on some faces that look up
        private static void RockFace(Builder b, Vector3 centre, Vector3 p0, Vector3 p1, Vector3 p2, Random random)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.LengthSquared() < 1e-10f) return;
            n = Vector3.Normalize(n);
            Vector3 middle = (p0 + p1 + p2) / 3f - centre;
            //Out from the rock's axis, and up
            if (Vector3.Dot(n, new Vector3(middle.X, 0f, middle.Z)) + n.Y * 4f < 0f) n = -n;

            Vector3 color = ROCK * (0.8f + 0.4f * (float)random.NextDouble());
            if (n.Y > 0.8f && middle.Y > 2f && random.NextDouble() < 0.3) color = ROCK_CRUST * (0.85f + 0.3f * (float)random.NextDouble());
            b.Triangle(p0, p1, p2, n, color, RigVertex.PAINTED);
        }

        /// <summary>
        /// A feeder container ship some 130 m long: a hull with a raked bow and a flat transom, near black over a red boot-top;
        /// seven bays of containers three to five high in muted, uneven colours, a raised forecastle, and aft the white
        /// accommodation block with its bridge, a funnel, two orange lifeboats and two masts with their lights.
        /// </summary>
        internal static List<RigVertex> ContainerShip()
        {
            var b = new Builder();

            //The hull: from its keel to the boot-top's line, red, then near black to the deck
            Vector2[] keel = HullOutline(new[] { (-124f, 17f), (80f, 17.5f), (100f, 14f), (112f, 8f), (118f, 3f), (120f, 0f) });
            Vector2[] deck = HullOutline(new[] { (-128f, 19f), (80f, 19f), (102f, 16.5f), (118f, 10f), (128f, 4f), (132f, 0f) });
            const float KEEL_Y = -DRAUGHT, BOOT_Y = 3f, DECK_Y = 13f;
            Vector2[] boot = Between(keel, deck, (BOOT_Y - KEEL_Y) / (DECK_Y - KEEL_Y));
            b.Loft(keel, KEEL_Y, boot, BOOT_Y, BOOT_RED, capBottom: true, capTop: false);
            b.Loft(boot, BOOT_Y, deck, DECK_Y, HULL_DARK, capBottom: false, capTop: true, topColor: DECK_RUST);

            //The forecastle, raised over the bow: the deck's own outline forward of its aft end, stood straight up, so its sides
            //run on from the hull's and no corner of it stands out past the bow
            const float FORECASTLE_AFT = 98f, FORECASTLE_HEIGHT = 5f;
            Vector2[] forecastle = ForwardOf(deck, FORECASTLE_AFT);
            b.Loft(forecastle, DECK_Y, forecastle, DECK_Y + FORECASTLE_HEIGHT, HULL_DARK, capBottom: false, capTop: true);

            //Seven bays of containers, each tier three boxes across, its colours rolled once
            var random = new Random(1125);
            int[] tiers = { 4, 5, 5, 4, 5, 4, 3 };
            const float TIER = 5.2f, BAY = 11.5f, BAY_PITCH = 24f, FIRST_BAY = -64f, STACK_FLOOR = DECK_Y + 1f;
            float[] across = { -11.8f, 0f, 11.8f };
            for (int bay = 0; bay < tiers.Length; bay++)
            {
                float x = FIRST_BAY + bay * BAY_PITCH;
                for (int tier = 0; tier < tiers[bay]; tier++)
                {
                    for (int row = 0; row < across.Length; row++)
                    {
                        //An outer box off the top tier now and then, so the stacks' tops are not one straight line
                        if (tier == tiers[bay] - 1 && row != 1 && random.NextDouble() < 0.3) continue;
                        Vector3 paint = CONTAINERS[random.Next(CONTAINERS.Length)] * (0.85f + 0.25f * (float)random.NextDouble());
                        b.Box(new Vector3(x, STACK_FLOOR + TIER * (tier + 0.5f), across[row]), new Vector3(BAY, TIER * 0.5f - 0.05f, 5.8f),
                            paint, RigVertex.PAINTED);
                    }
                }
            }

            //The accommodation block aft, its decks' windows dark but one row lit, and the bridge over it with its wings
            const float BLOCK_X = -107f, BLOCK_FRONT = -97f;
            b.Box(new Vector3(BLOCK_X, 28f, 0f), new Vector3(10f, 14.5f, 13f), SUPER_WHITE, RigVertex.PAINTED);
            foreach (float y in new[] { 19f, 25f, 31f, 37f })
            {
                bool lit = y == 25f;
                b.Box(new Vector3(BLOCK_FRONT + 0.2f, y, 0f), new Vector3(0.3f, 0.8f, 11f), lit ? WINDOW_LIT : GLASS, lit ? RigVertex.LIGHT : RigVertex.PAINTED);
                foreach (float side in new[] { -1f, 1f })
                    b.Box(new Vector3(BLOCK_X, y, side * 13.2f), new Vector3(8.5f, 0.8f, 0.3f), GLASS, RigVertex.PAINTED);
            }
            b.Box(new Vector3(-106f, 45f, 0f), new Vector3(8f, 2.5f, 20f), SUPER_WHITE, RigVertex.PAINTED);
            b.Box(new Vector3(-97.8f, 45.5f, 0f), new Vector3(0.3f, 1f, 18f), GLASS, RigVertex.PAINTED);

            //The funnel behind it, with its company's band
            b.Box(new Vector3(-122f, 47f, 0f), new Vector3(4f, 7f, 4f), FUNNEL, RigVertex.PAINTED);
            b.Box(new Vector3(-122f, 50f, 0f), new Vector3(4.1f, 1.6f, 4.1f), FUNNEL_BAND, RigVertex.PAINTED);

            //The lifeboats, one either side, and the two masts with their lights
            foreach (float side in new[] { -1f, 1f })
                b.Box(new Vector3(-112f, 30f, side * 14.6f), new Vector3(4f, 1.4f, 1.4f), LIFEBOAT, RigVertex.PAINTED);
            b.Box(new Vector3(-106f, 54f, 0f), new Vector3(0.4f, 6.5f, 0.4f), MAST_GREY, RigVertex.PAINTED);
            b.Box(new Vector3(-106f, 61f, 0f), new Vector3(0.6f, 0.6f, 0.6f), MASTHEAD, RigVertex.LIGHT);
            b.Box(new Vector3(116f, DECK_Y + FORECASTLE_HEIGHT + 8f, 0f), new Vector3(0.4f, 8f, 0.4f), MAST_GREY, RigVertex.PAINTED);
            b.Box(new Vector3(116f, DECK_Y + FORECASTLE_HEIGHT + 16.5f, 0f), new Vector3(0.6f, 0.6f, 0.6f), MASTHEAD, RigVertex.LIGHT);
            return b.Vertices;
        }

        /// <summary>
        /// A 13 m yacht running before the wind: a white hull over a navy boot-top with a raked bow and a teak deck, a low
        /// cabin, a mast some 16 m tall, the mainsail on its boom eased out to <paramref name="leeward"/> (+1 starboard, -1 port)
        /// and the jib winged out on the other side, both bellied by the wind behind them.
        /// </summary>
        internal static List<RigVertex> Yacht(float leeward)
        {
            var b = new Builder();

            Vector2[] keel = HullOutline(new[] { (-11.5f, 2.4f), (-6f, 3f), (2f, 3f), (7f, 2f), (10f, 0.9f), (11.2f, 0f) });
            Vector2[] deck = HullOutline(new[] { (-13f, 3f), (-6f, 3.9f), (2f, 4f), (8f, 2.9f), (11.5f, 1.4f), (13f, 0f) });
            const float KEEL_Y = -2f, BOOT_Y = 0.6f, DECK_Y = 2.2f;
            Vector2[] boot = Between(keel, deck, (BOOT_Y - KEEL_Y) / (DECK_Y - KEEL_Y));
            b.Loft(keel, KEEL_Y, boot, BOOT_Y, YACHT_NAVY, capBottom: true, capTop: false);
            b.Loft(boot, BOOT_Y, deck, DECK_Y, YACHT_WHITE, capBottom: false, capTop: true, topColor: TEAK);

            //The cabin, its windows a dark band either side
            b.Box(new Vector3(-1f, DECK_Y + 0.7f, 0f), new Vector3(4f, 0.7f, 2.2f), YACHT_WHITE, RigVertex.PAINTED);
            foreach (float side in new[] { -1f, 1f })
                b.Box(new Vector3(-1f, DECK_Y + 0.8f, side * 2.25f), new Vector3(3f, 0.25f, 0.08f), GLASS, RigVertex.PAINTED);

            //The mast, and the boom eased out to leeward
            const float MAST_X = 2f, MAST_TOP = 34.2f, GOOSENECK = 5.4f, BOOM = 10.3f, MAIN_EASE = 1.05f, JIB_EASE = 0.95f;
            b.Box(new Vector3(MAST_X, (DECK_Y + MAST_TOP) * 0.5f, 0f), new Vector3(0.18f, (MAST_TOP - DECK_Y) * 0.5f, 0.18f), SPAR, RigVertex.PAINTED);
            Vector3 boomWay = new(-MathF.Cos(MAIN_EASE), 0f, leeward * MathF.Sin(MAIN_EASE));
            Vector3 gooseneck = new(MAST_X, GOOSENECK, 0f);
            b.Box(gooseneck + boomWay * (BOOM * 0.5f), new Vector3(BOOM * 0.5f, 0.12f, 0.12f), SPAR, RigVertex.PAINTED,
                MathF.Atan2(-boomWay.Z, boomWay.X));

            //The sails, bellied downwind: the wind blows from astern, towards the bow and to leeward
            Vector3 downwind = Vector3.Normalize(new Vector3(1f, 0f, leeward * 0.25f));
            Vector3 tack = gooseneck + Vector3.Up * 0.2f;
            b.Sail(tack, new Vector3(MAST_X, MAST_TOP - 0.6f, 0f), tack + boomWay * BOOM, downwind, 0.07f, SAILCLOTH);
            Vector3 jibWay = new(MathF.Cos(JIB_EASE) * 0.2f, 0f, -leeward * MathF.Sin(JIB_EASE));
            b.Sail(new Vector3(12.2f, DECK_Y + 0.4f, 0f), new Vector3(MAST_X + 0.4f, MAST_TOP - 4.5f, 0f),
                new Vector3(MAST_X + 1.5f, DECK_Y + 2.2f, 0f) + jibWay * 7.5f, downwind, 0.06f, SAILCLOTH);
            return b.Vertices;
        }

        //A hull's outline round from the stern on the starboard side to the stem and back on the port side, from its starboard
        //half's points (x along it, z out to starboard, the last on the centreline)
        private static Vector2[] HullOutline((float X, float Z)[] starboard)
        {
            var points = new List<Vector2>();
            foreach ((float x, float z) in starboard) points.Add(new Vector2(x, z));
            for (int i = starboard.Length - 2; i >= 0; i--) points.Add(new Vector2(starboard[i].X, -starboard[i].Z));
            return points.ToArray();
        }

        //The part of a convex outline forward of x = `aft` (x >= aft), cut straight across there, in the outline's own order
        private static Vector2[] ForwardOf(Vector2[] outline, float aft)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                bool aIn = a.X >= aft, bIn = b.X >= aft;
                if (aIn) points.Add(a);
                if (aIn != bIn) points.Add(Vector2.Lerp(a, b, (aft - a.X) / (b.X - a.X)));
            }
            return points.ToArray();
        }

        private static Vector2[] Between(Vector2[] a, Vector2[] b, float t)
        {
            var points = new Vector2[a.Length];
            for (int i = 0; i < a.Length; i++) points[i] = Vector2.Lerp(a[i], b[i], t);
            return points;
        }

        /// <summary>Collects the triangles, each wound so its geometric normal points away from the normal it is lit by.</summary>
        internal sealed class Builder
        {
            public readonly List<RigVertex> Vertices = new();

            /// <summary>A triangle lit by one normal, <paramref name="outward"/>.</summary>
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector3 color, float kind) =>
                Triangle(a, b, c, outward, outward, outward, color, kind);

            /// <summary>
            /// A triangle lit by a normal at each corner; wound clockwise seen from the side they point to: when its geometric
            /// normal, <c>(b - a) × (c - a)</c>, points the way they do, two corners are swapped.
            /// </summary>
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 color, float kind)
            {
                Vector3 geometric = Vector3.Cross(b - a, c - a);
                if (geometric.LengthSquared() < 1e-12f) return;
                var paint = new Color(new Vector4(color, kind));
                if (Vector3.Dot(geometric, na + nb + nc) > 0f)
                {
                    (b, c) = (c, b);
                    (nb, nc) = (nc, nb);
                }
                Vertices.Add(new RigVertex(a, Vector3.Normalize(na), paint));
                Vertices.Add(new RigVertex(b, Vector3.Normalize(nb), paint));
                Vertices.Add(new RigVertex(c, Vector3.Normalize(nc), paint));
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector3 color, float kind)
            {
                Triangle(a, b, c, outward, color, kind);
                Triangle(a, c, d, outward, color, kind);
            }

            /// <summary>A box <paramref name="half"/> either way of <paramref name="centre"/>, turned <paramref name="yaw"/> about the vertical.</summary>
            public void Box(Vector3 centre, Vector3 half, Vector3 color, float kind, float yaw = 0f)
            {
                Matrix turn = Matrix.CreateRotationY(yaw);
                Vector3 P(float x, float y, float z) => centre + Vector3.Transform(new Vector3(x * half.X, y * half.Y, z * half.Z), turn);
                Vector3 N(Vector3 n) => Vector3.Transform(n, turn);
                for (int axis = 0; axis < 3; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        //The face's four corners: the other two axes at -1/+1 round it
                        Vector3 F(float u, float v) => axis switch
                        {
                            0 => P(sign, u, v),
                            1 => P(u, sign, v),
                            _ => P(u, v, sign),
                        };
                        Vector3 n = axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ } * sign;
                        Quad(F(-1f, -1f), F(1f, -1f), F(1f, 1f), F(-1f, 1f), N(n), color, kind);
                    }
            }

            /// <summary>
            /// A solid between two outlines in plan (x, z) of as many points, the one at <paramref name="y0"/> and the other at
            /// <paramref name="y1"/>, its sides lit flat, and either end shut by a flat cap.
            /// </summary>
            public void Loft(Vector2[] bottom, float y0, Vector2[] top, float y1, Vector3 color, bool capBottom, bool capTop,
                Vector3? topColor = null)
            {
                int count = bottom.Length;
                Vector2 middle = Vector2.Zero;
                foreach (Vector2 p in top) middle += p;
                middle /= count;

                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count;
                    Vector3 b0 = new(bottom[i].X, y0, bottom[i].Y), b1 = new(bottom[j].X, y0, bottom[j].Y);
                    Vector3 t0 = new(top[i].X, y1, top[i].Y), t1 = new(top[j].X, y1, top[j].Y);
                    Vector3 n = Vector3.Cross(b1 - b0, t0 - b0);
                    if (n.LengthSquared() < 1e-10f) n = Vector3.Cross(t1 - t0, t0 - b0);
                    n = Vector3.Normalize(n);
                    Vector3 side = (b0 + b1 + t0 + t1) * 0.25f;
                    if (Vector3.Dot(n, new Vector3(side.X - middle.X, 0f, side.Z - middle.Y)) < 0f) n = -n;
                    Quad(b0, b1, t1, t0, n, color, RigVertex.PAINTED);
                }

                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count;
                    if (capTop)
                        Triangle(new Vector3(middle.X, y1, middle.Y), new Vector3(top[i].X, y1, top[i].Y), new Vector3(top[j].X, y1, top[j].Y),
                            Vector3.UnitY, topColor ?? color, RigVertex.PAINTED);
                    if (capBottom)
                        Triangle(new Vector3(middle.X, y0, middle.Y), new Vector3(bottom[i].X, y0, bottom[i].Y), new Vector3(bottom[j].X, y0, bottom[j].Y),
                            -Vector3.UnitY, color, RigVertex.PAINTED);
                }
            }

            /// <summary>
            /// A frustum standing on <paramref name="foot"/>: <paramref name="r0"/> round there, <paramref name="r1"/> round at
            /// <paramref name="y1"/>, its side lit smooth round it, shut at both ends.
            /// </summary>
            public void Frustum(Vector3 foot, float r0, float y1, float r1, int segments, Vector3 color, float kind)
            {
                float height = y1 - foot.Y, slope = (r0 - r1) / height;
                Vector3 top = new(foot.X, y1, foot.Z);
                for (int s = 0; s < segments; s++)
                {
                    float a0 = s * MathHelper.TwoPi / segments, a1 = (s + 1) * MathHelper.TwoPi / segments;
                    Vector3 d0 = new(MathF.Cos(a0), 0f, MathF.Sin(a0)), d1 = new(MathF.Cos(a1), 0f, MathF.Sin(a1));
                    Vector3 n0 = Vector3.Normalize(d0 + Vector3.UnitY * slope), n1 = Vector3.Normalize(d1 + Vector3.UnitY * slope);
                    Vector3 b0 = foot + d0 * r0, b1 = foot + d1 * r0, t0 = top + d0 * r1, t1 = top + d1 * r1;
                    Triangle(b0, b1, t1, n0, n1, n1, color, kind);
                    Triangle(b0, t1, t0, n0, n1, n0, color, kind);
                    if (r1 > 1e-4f) Triangle(top, t0, t1, Vector3.UnitY, color, kind);
                    Triangle(foot, b0, b1, -Vector3.UnitY, color, kind);
                }
            }

            /// <summary>A pitched roof over a box: its ridge along X <paramref name="rise"/> over <paramref name="eaves"/>, half
            /// <paramref name="halfLength"/> long and <paramref name="halfWidth"/> wide, with its two gables.</summary>
            public void Roof(Vector3 eaves, float halfLength, float halfWidth, float rise, Vector3 color)
            {
                Vector3 ridge0 = eaves + new Vector3(-halfLength, rise, 0f), ridge1 = eaves + new Vector3(halfLength, rise, 0f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 e0 = eaves + new Vector3(-halfLength, 0f, side * halfWidth), e1 = eaves + new Vector3(halfLength, 0f, side * halfWidth);
                    Vector3 n = Vector3.Normalize(new Vector3(0f, halfWidth, side * rise));
                    Quad(e0, e1, ridge1, ridge0, n, color, RigVertex.PAINTED);

                    Vector3 g0 = eaves + new Vector3(side * halfLength, 0f, -halfWidth), g1 = eaves + new Vector3(side * halfLength, 0f, halfWidth);
                    Triangle(g0, g1, side < 0f ? ridge0 : ridge1, Vector3.UnitX * side, color, RigVertex.PAINTED);
                }
                Quad(eaves + new Vector3(-halfLength, 0f, -halfWidth), eaves + new Vector3(halfLength, 0f, -halfWidth),
                    eaves + new Vector3(halfLength, 0f, halfWidth), eaves + new Vector3(-halfLength, 0f, halfWidth), -Vector3.UnitY, color, RigVertex.PAINTED);
            }

            /// <summary>
            /// A sail: one sheet over the triangle from its <paramref name="tack"/> up its luff to its <paramref name="head"/>
            /// and out along its foot to its <paramref name="clew"/>, bellied towards <paramref name="downwind"/> by
            /// <paramref name="camber"/> of its chord, most at a third of the way up and none at the luff, the leech or the head.
            /// Lit smooth from the side the wind fills; <c>Rig.fx</c> turns it to the lens.
            /// </summary>
            public void Sail(Vector3 tack, Vector3 head, Vector3 clew, Vector3 downwind, float camber, Vector3 color)
            {
                const int ROWS = 5, COLUMNS = 4;
                Vector3 flat = Vector3.Normalize(Vector3.Cross(head - tack, clew - tack));
                if (Vector3.Dot(flat, downwind) < 0f) flat = -flat;
                float chord = Vector3.Distance(tack, clew);

                Vector3 At(float u, float v)
                {
                    Vector3 luff = Vector3.Lerp(tack, head, v);
                    Vector3 p = luff + (clew - tack) * (u * (1f - v));
                    float belly = camber * chord * (1f - v) * 4f * u * (1f - u) * MathF.Min(1f, v * 3f + 0.35f);
                    return p + flat * belly;
                }

                Vector3 Normal(float u, float v)
                {
                    const float E = 1e-3f;
                    Vector3 du = At(MathF.Min(u + E, 1f), v) - At(MathF.Max(u - E, 0f), v);
                    Vector3 dv = At(u, MathF.Min(v + E, 0.999f)) - At(u, MathF.Max(v - E, 0f));
                    Vector3 n = Vector3.Cross(dv, du);
                    if (n.LengthSquared() < 1e-14f) return flat;
                    n = Vector3.Normalize(n);
                    return Vector3.Dot(n, flat) < 0f ? -n : n;
                }

                for (int row = 0; row < ROWS; row++)
                    for (int column = 0; column < COLUMNS; column++)
                    {
                        float u0 = column / (float)COLUMNS, u1 = (column + 1) / (float)COLUMNS;
                        float v0 = row / (float)ROWS, v1 = (row + 1) / (float)ROWS;
                        Vector3 p00 = At(u0, v0), p10 = At(u1, v0), p11 = At(u1, v1), p01 = At(u0, v1);
                        Vector3 n00 = Normal(u0, v0), n10 = Normal(u1, v0), n11 = Normal(u1, v1), n01 = Normal(u0, v1);
                        Triangle(p00, p10, p11, n00, n10, n11, color, RigVertex.CLOTH);
                        Triangle(p00, p11, p01, n00, n11, n01, color, RigVertex.CLOTH);
                    }
            }
        }
    }
}
