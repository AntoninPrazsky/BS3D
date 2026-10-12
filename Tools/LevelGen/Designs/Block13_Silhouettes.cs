using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;
using static BS3D.Tools.LevelGen.CampaignSet;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Silhouettes</b> (#491, rebuilt with depth in #736): ten black figures hung under the aurora. Its designs,
    /// and the helpers no other block's designs use; the play order is <see cref="Main"/>'s and the block's name, music
    /// and ball style are in <see cref="CampaignSet"/>'s tables, as every other block file states.
    /// </summary>
    internal static partial class Program
    {
        #region The silhouette levels (#491, #736)

        //DRAFT

        private const byte SIL_GRID = 17;

        //PICTURE_FIELD_LEVELS' 18, for its reason: the deepest field the camera frames whole, which hangs the layout
        //highest. Every design here is drawn in an even number of courses, so the offset is even.
        private const byte SIL_FIELD = PICTURE_FIELD_LEVELS;

        /// <summary>What every silhouette is cut from: black, the one ink the chapter never changes.</summary>
        private const BallType SILHOUETTE_INK = BallType.Type8;

        /// <summary>
        /// Inert: the aurora replaces the sky and states its own light rig (<c>SceneRenderer.ReplacesSky</c>), so
        /// the dome number changes nothing. Every level names the same one anyway, so DescribeBlock has something
        /// to agree with — the Grid's and the Mirage's own practice.
        /// </summary>
        private const byte SKY_SILHOUETTES = 13;

        /// <summary>
        /// One of the chapter's solids: a part function over the raw lattice (0 for air), the colour of every cell
        /// it fills, and optionally its kinds and a reveal's payoff.
        /// </summary>
        private static Design SilhouetteSolid(string name, byte depth, int shots, int ceilingStep,
            Func<int, int, int, int> part, Func<int, int, int, BallType> colour,
            Func<int, int, int, BallKind> kind = null, Func<int, int, int, bool> payoff = null, bool payoffInSight = false) => new()
        {
            File = name + ".json",
            Name = name,
            Grid = SIL_GRID,
            Depth = depth,
            FieldLevels = SIL_FIELD,
            Scene = SceneKind.Aurora,
            Sky = SKY_SILHOUETTES,
            Music = MUSIC_SILHOUETTES,
            Balls = BALLS_SILHOUETTES,
            Shots = shots,
            CeilingStep = ceilingStep,
            OccupiedBlock = (x, z, i, d) => part(x, z, i) != 0,
            BlockColour = colour,
            BlockKind = kind == null ? null : (x, z, i, d) => kind(x, z, i),
            Payoff = payoff == null ? null : (x, z, r, ang, i, d) => payoff(x, z, i),
            PayoffInSight = payoffInSight,
        };

        /// <summary>How many courses below the glass a layout level is: 0 is the anchor course.</summary>
        private static int SilCourse(int i, byte depth) => depth - 1 - i;

        /// <summary>The cell's centred plan offsets and its round radius from the axis the gun orbits.</summary>
        private static float SilPlan(int x, int z, int i, out float dx, out float dz)
        {
            Centred(x, z, i, SIL_GRID, out dx, out dz);
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Whether a cell is THE one of its course nearest to the plan point (<paramref name="px"/>,
        /// <paramref name="pz"/>). Taken on every course, these are a vertical chain one ball thick: the nearest cells
        /// of two neighbouring courses are always half a cell apart in both x and z, which is a cross-level
        /// neighbour, so the chain is connected whatever the point. Only glass may be drawn so thin (the Mirage's law).
        /// </summary>
        private static bool NearestTo(float dx, float dz, float px, float pz) =>
            dx - px > -HALF && dx - px <= HALF && dz - pz > -HALF && dz - pz <= HALF;

        /// <summary>Which of <paramref name="wedges"/> equal wedges round the axis a plan offset is in, turned by <paramref name="turn"/>.</summary>
        private static int SilWedge(float dx, float dz, int wedges, float turn = 0f) =>
            SectorIndex(MathF.Atan2(dz, dx), turn, wedges);

        /// <summary>The plan point at the middle of wedge <paramref name="wedge"/> (see <see cref="SilWedge"/>), at radius <paramref name="r"/>.</summary>
        private static void WedgeMiddle(int wedge, int wedges, float turn, float r, out float px, out float pz)
        {
            float angle = ((wedge + HALF) / wedges - HALF - turn) * MathF.Tau;
            px = r * MathF.Cos(angle);
            pz = r * MathF.Sin(angle);
        }

        /// <summary>
        /// How far inside its ink a bitmap cell is: 1 on the ink's edge, 2 one cell in, and so on — the Chebyshev
        /// distance to the nearest pixel that is not ink, the outside of the bitmap counting as not ink.
        /// </summary>
        private static int InkDepth(string[] bitmap, int column, int row)
        {
            for (int d = 1; d < 8; d++)
                for (int dr = -d; dr <= d; dr++)
                    for (int dc = -d; dc <= d; dc++)
                        if (SYMBOL_INK.IndexOf(PixelAt(bitmap, column + dc, row + dr)) < 0) return d;

            return 8;
        }

        /// <summary>
        /// Whether a background cell of <paramref name="bitmap"/> has ink above it in its own column — the cells a
        /// silhouette's ground leaves empty, so the figure's lower edge is its own outline (#556).
        /// </summary>
        private static bool UnderInk(string[] bitmap, int column, int row)
        {
            if (SYMBOL_INK.IndexOf(PixelAt(bitmap, column, row)) >= 0) return false;

            for (int above = row - 1; above >= 0; above--)
                if (SYMBOL_INK.IndexOf(PixelAt(bitmap, column, above)) >= 0) return true;

            return false;
        }

        private static readonly BallType[] SIL_GROUND = { BallType.Type4, BallType.Type5 };   //white, cyan

        /// <summary>How far a point is from a line segment, in the plane both are given in.</summary>
        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Math.Clamp(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy), 0f, 1f);
            float ex = px - (ax + t * vx), ey = py - (ay + t * vy);
            return MathF.Sqrt(ex * ex + ey * ey);
        }

        //=== 1. FISH: a relief ===========================================================================

        private const byte FISH_DEPTH = 12;

        //How many cells the body swells out of the screen on each side at most, a cell per step in from its outline
        private const int FISH_SWELL = 2;

        private static Design Fish() => SilhouetteSolid("Fish", FISH_DEPTH, shots: 48, ceilingStep: 9,
            (x, z, i) => FishPart(x, z, i), FishColour);

        private static readonly string[] FISH =
        {
            "...............",
            "...............",
            "......###......",
            ".....####......",
            "...######...#..",
            "..########.##..",
            "..###########..",
            "..###########..",
            "..########.##..",
            "...######...#..",
            ".....###.......",
            "...............",
        };

        /// <summary>0 for air, 1 for the screen, 2 for the fish.</summary>
        private static int FishPart(int x, int z, int i)
        {
            int column = x - 1, row = FISH_DEPTH - 1 - i;
            if (column < 0 || column >= FISH[0].Length) return 0;

            if (SYMBOL_INK.IndexOf(PixelAt(FISH, column, row)) >= 0)
            {
                int swell = Math.Min(InkDepth(FISH, column, row) - 1, FISH_SWELL);
                return z >= 7 - swell && z <= 8 + swell ? 2 : 0;
            }

            return (z == 7 || z == 8) && !UnderInk(FISH, column, row) ? 1 : 0;
        }

        private static BallType FishColour(int x, int z, int i)
        {
            if (FishPart(x, z, i) == 2) return SILHOUETTE_INK;

            int column = x - 1, row = FISH_DEPTH - 1 - i;
            return Band(column / 2 + row / 2, SIL_GROUND);
        }

        //=== 2. UMBRELLA: a solid of revolution, and the campaign's first glass ===========================

        private const byte UMBRELLA_DEPTH = 14;

        private const int UMBRELLA_RIM = 5;             //the canopy's last course
        private const float UMBRELLA_CROWN = 3.4f;      //the canopy's radius against the glass
        private const float UMBRELLA_FLARE = 0.6f;      //how much wider each course down
        private const float UMBRELLA_SKIN = 2.0f;       //how deep each course of the canopy is, in plan
        private const float UMBRELLA_TIP = 1.2f;        //the ferrule at the crown, the handle's colour
        private const int UMBRELLA_GORES = 12;
        private const float UMBRELLA_TURN = 0.03f;      //keeps every cell off a gore's edge
        private const float UMBRELLA_ICICLE_R = 5.8f;
        private const int UMBRELLA_ICICLE = 2;          //balls of glass an icicle is long

        private static Design Umbrella() => SilhouetteSolid("Umbrella", UMBRELLA_DEPTH, shots: 36, ceilingStep: 7,
            (x, z, i) => UmbrellaPart(x, z, i), UmbrellaColour, UmbrellaKind);

        /// <summary>0 for air, 1 for the canopy, 2 for the ferrule, the shaft and the hook, 3 for an icicle.</summary>
        private static int UmbrellaPart(int x, int z, int i)
        {
            int k = SilCourse(i, UMBRELLA_DEPTH);
            float r = SilPlan(x, z, i, out float dx, out float dz);

            if (k <= UMBRELLA_RIM)
            {
                float outer = UMBRELLA_CROWN + UMBRELLA_FLARE * k;
                float inner = k == 0 ? 0f : outer - UMBRELLA_SKIN;
                if (k == 0 && r <= UMBRELLA_TIP) return 2;
                if (r <= outer && r >= inner) return 1;
            }

            //The shaft down the middle, and the hook at its foot curling towards +x and up again
            if (MathF.Abs(dx) <= HALF && MathF.Abs(dz) <= HALF && k >= 1 && k <= UMBRELLA_DEPTH - 2) return 2;
            if (MathF.Abs(dz) <= 0.6f)
            {
                if (k >= UMBRELLA_DEPTH - 2 && dx >= 0f && dx <= 3f) return 2;
                if (k >= UMBRELLA_DEPTH - 4 && dx >= 2f && dx <= 3f) return 2;
            }

            if (k > UMBRELLA_RIM && k <= UMBRELLA_RIM + UMBRELLA_ICICLE)
                for (int gore = 0; gore < UMBRELLA_GORES; gore++)
                {
                    WedgeMiddle(gore, UMBRELLA_GORES, UMBRELLA_TURN, UMBRELLA_ICICLE_R, out float px, out float pz);
                    if (NearestTo(dx, dz, px, pz)) return 3;
                }

            return 0;
        }

        private static BallType UmbrellaColour(int x, int z, int i)
        {
            int part = UmbrellaPart(x, z, i);
            if (part == 2) return BallType.Type1;        //red handle
            if (part == 3) return BallType.Type4;        //glass: unread

            SilPlan(x, z, i, out float dx, out float dz);
            return SilWedge(dx, dz, UMBRELLA_GORES, UMBRELLA_TURN) % 2 == 0 ? SILHOUETTE_INK : BallType.Type4;
        }

        private static BallKind UmbrellaKind(int x, int z, int i) =>
            UmbrellaPart(x, z, i) == 3 ? BallKind.Transparent : BallKind.Normal;

        //=== 3. BELL: a hollow bell with a clapper sealed behind a glass floor ===========================

        private const byte BELL_DEPTH = 10;

        //The bell's outer radius course by course from the glass: a rounded shoulder, a waist, a flared lip
        private static readonly float[] BELL_OUTER = { 2.6f, 3.3f, 3.5f, 3.5f, 3.6f, 3.8f, 4.1f, 4.6f, 5.2f, 5.7f };
        private const float BELL_WALL = 1.3f;
        private const int BELL_LID = 2;               //solid courses against the glass
        private const float BELL_CROWN = 1.6f;        //the crown ring in the lid, round the clapper's cord
        private const int BELL_STAVES = 12;
        private const float BELL_TURN = 0.03f;
        private const int BELL_LIP = 8;               //the lip band's first course
        private const int BELL_CLAPPER = 5;           //the clapper's first course
        private const float BELL_RIB = HALF;          //the floor's cross of ribs

        private static Design Bell() => SilhouetteSolid("Bell", BELL_DEPTH, shots: 40, ceilingStep: 8,
            (x, z, i) => BellPart(x, z, i), BellColour, BellKind, payoff: (x, z, i) => BellPart(x, z, i) == 5);

        /// <summary>0 air, 1 a stave, 2 the crown ring, 3 the lip band and the floor's ribs, 4 the floor's glass, 5 the clapper and its cord.</summary>
        private static int BellPart(int x, int z, int i)
        {
            int k = SilCourse(i, BELL_DEPTH);
            if (k >= BELL_OUTER.Length) return 0;

            float r = SilPlan(x, z, i, out float dx, out float dz);
            float outer = BELL_OUTER[k];
            if (r > outer) return 0;

            bool cord = MathF.Abs(dx) <= HALF && MathF.Abs(dz) <= HALF;

            if (k < BELL_LID) return cord ? 5 : r <= BELL_CROWN ? 2 : 1;

            float inner = outer - BELL_WALL;
            if (r >= inner) return k >= BELL_LIP ? 3 : 1;

            //The floor across the mouth: glass in four panes between a cross of ribs
            if (k == BELL_OUTER.Length - 1)
                return MathF.Abs(dx) <= BELL_RIB || MathF.Abs(dz) <= BELL_RIB ? 3 : 4;

            //The cavity, with the clapper hanging in it on its cord, two courses of air clear of the floor
            if (k < BELL_CLAPPER) return cord ? 5 : 0;
            if (k == BELL_CLAPPER) return r <= 1.3f ? 5 : 0;
            if (k == BELL_CLAPPER + 1) return r <= 0.8f ? 5 : 0;
            return 0;
        }

        private static readonly BallType[] BELL_STAVE_COLOURS = { SILHOUETTE_INK, BallType.Type7 };   //black, yellow

        private static BallType BellColour(int x, int z, int i)
        {
            switch (BellPart(x, z, i))
            {
                case 5: return BallType.Type1;     //red clapper
                case 2: return BallType.Type4;     //white crown
                case 3: return BallType.Type4;     //white lip band and ribs
                case 4: return BallType.Type5;     //glass: unread
            }

            SilPlan(x, z, i, out float dx, out float dz);
            return BELL_STAVE_COLOURS[SilWedge(dx, dz, BELL_STAVES, BELL_TURN) % 2];
        }

        private static BallKind BellKind(int x, int z, int i) =>
            BellPart(x, z, i) == 4 ? BallKind.Transparent : BallKind.Normal;

        //=== 4. CAT: a black cat puppet in front of the lit screen ======================================

        private const byte CAT_DEPTH = 14;

        //The screen behind the puppet: two cells thick, hung from the glass by its top edge
        private const float CAT_SCREEN = 5.6f;
        private const int CAT_SCREEN_COURSES = 9;
        private const int CAT_SCREEN_Z = 5;

        private static Design Cat() => SilhouetteSolid("Cat", CAT_DEPTH, shots: 34, ceilingStep: 7,
            (x, z, i) => CatPart(x, z, i), CatColour, CatKind);

        /// <summary>0 air, 1 the screen, 2 the cat, 3 an eye (glass).</summary>
        private static int CatPart(int x, int z, int i)
        {
            int k = SilCourse(i, CAT_DEPTH);
            float below = k * INV_SQRT_TWO;
            SilPlan(x, z, i, out float dx, out float dz);

            if (z == CAT_SCREEN_Z || z == CAT_SCREEN_Z + 1)
                return k < CAT_SCREEN_COURSES && MathF.Abs(dx) <= CAT_SCREEN ? 1 : 0;

            //Its eyes, a pair of glass balls each on the face
            if ((k == 4 || k == 5) && (NearestTo(dx, dz, -0.8f, 1.8f) || NearestTo(dx, dz, 0.8f, 1.8f))) return 3;

            //The head, its ears, the body it sits on and the tail curled round its feet
            float hy = below - 3.6f;
            if (dx * dx + dz * dz + hy * hy <= 1.9f * 1.9f) return 2;
            if ((k == 1 || k == 2) && MathF.Abs(MathF.Abs(dx) - 1.2f) <= (k == 1 ? 0.5f : 0.9f) && MathF.Abs(dz) <= 0.6f) return 2;

            float by = (below - 7.0f) / 2.3f;
            if (dx * dx / (2.6f * 2.6f) + dz * dz / (2.0f * 2.0f) + by * by <= 1f) return 2;

            if (MathF.Abs(dz - 0.6f) <= 0.6f
                && (SegmentDistance(dx, below, 2.0f, 8.6f, 4.0f, 8.9f) <= 0.65f
                    || SegmentDistance(dx, below, 4.0f, 8.9f, 4.7f, 7.0f) <= 0.65f))
                return 2;

            return 0;
        }

        private static readonly BallType[] CAT_LIGHT = { BallType.Type7, BallType.Type4, BallType.Type5 };   //yellow, white, cyan

        private static BallType CatColour(int x, int z, int i) => CatPart(x, z, i) switch
        {
            2 => SILHOUETTE_INK,
            3 => BallType.Type5,      //glass: unread
            _ => Band(x / 2 + SilCourse(i, CAT_DEPTH) / 2, CAT_LIGHT),
        };

        private static BallKind CatKind(int x, int z, int i) =>
            CatPart(x, z, i) == 3 ? BallKind.Transparent : BallKind.Normal;

        //=== 5. TEAPOT: a sealed pot with the tea in it ==================================================

        private const byte TEAPOT_DEPTH = 10;

        private const float TEAPOT_RADIUS = 4.3f;
        private const float TEAPOT_HEIGHT = 3.3f;
        private const float TEAPOT_CENTRE = 2.2f;
        private const float TEAPOT_CAVITY_RADIUS = 2.6f;
        private const float TEAPOT_CAVITY_HEIGHT = 1.8f;
        private const float TEAPOT_CAVITY_CENTRE = 2.9f;
        private const float TEAPOT_LID = 1.8f;
        private const float TEAPOT_TEA = 1.2f;
        private const float TEAPOT_FOOT = 4.9f;
        private const float TEAPOT_LID_DEPTH = 1.2f;

        private static Design Teapot() => SilhouetteSolid("Teapot", TEAPOT_DEPTH, shots: 44, ceilingStep: 8,
            (x, z, i) => TeapotPart(x, z, i), TeapotColour, payoff: (x, z, i) => TeapotPart(x, z, i) == 5);

        /// <summary>0 air, 1 the pot, 2 the lid and the foot, 3 the spout, 4 the handle, 5 the tea and its cord.</summary>
        private static int TeapotPart(int x, int z, int i)
        {
            int k = SilCourse(i, TEAPOT_DEPTH);
            float below = k * INV_SQRT_TWO;
            float r = SilPlan(x, z, i, out float dx, out float dz);

            float oy = (below - TEAPOT_CENTRE) / TEAPOT_HEIGHT;
            bool inPot = r * r / (TEAPOT_RADIUS * TEAPOT_RADIUS) + oy * oy <= 1f;

            if (inPot)
            {
                bool cord = MathF.Abs(dx) <= HALF && MathF.Abs(dz) <= HALF;
                float cy = (below - TEAPOT_CAVITY_CENTRE) / TEAPOT_CAVITY_HEIGHT;
                bool inCavity = r * r / (TEAPOT_CAVITY_RADIUS * TEAPOT_CAVITY_RADIUS) + cy * cy <= 1f;

                if (cord && below <= TEAPOT_CAVITY_CENTRE) return 5;
                if (inCavity)
                {
                    float ty = (below - TEAPOT_CAVITY_CENTRE) / 0.8f;
                    return r * r / (TEAPOT_TEA * TEAPOT_TEA) + ty * ty <= 1f ? 5 : 0;
                }

                //The lid against the glass, and a foot ring under the pot where its staves would meet
                return (below <= TEAPOT_LID_DEPTH && r <= TEAPOT_LID) || (below >= TEAPOT_FOOT && r <= 2.2f) ? 2 : 1;
            }

            //The spout: a thick stroke out of the pot's lower left, rising to its lip
            if (dx < 0f) return MathF.Abs(dz) <= 1.1f && SegmentDistance(dx, below, -3.4f, 4.2f, -6.8f, 1.2f) <= 0.9f ? 3 : 0;

            //The handle: a loop out of the pot's right side
            if (MathF.Abs(dz) > 0.6f) return 0;
            float hx = dx - 4.2f, hy = below - 2.6f;
            float d = MathF.Sqrt(hx * hx + hy * hy);
            return d >= 1.2f && d <= 2.1f && dx >= 3.6f ? 4 : 0;
        }

        private static readonly BallType[] TEAPOT_GLAZE = { SILHOUETTE_INK, BallType.Type4 };   //black, white

        private static BallType TeapotColour(int x, int z, int i)
        {
            switch (TeapotPart(x, z, i))
            {
                case 5: return BallType.Type9;     //the tea, orange
                case 2: return BallType.Type1;     //the red lid
                case 3:
                case 4: return BallType.Type1;     //red spout and handle
            }

            SilPlan(x, z, i, out float dx, out float dz);
            return TEAPOT_GLAZE[SilWedge(dx, dz, 12, 0.03f) % 2];
        }

        //=== 6. KEY: hung by its bow under a board =======================================================

        private const byte KEY_DEPTH = 14;

        private static Design Key() => SilhouetteSolid("Key", KEY_DEPTH, shots: 34, ceilingStep: 7,
            (x, z, i) => KeyPart(x, z, i), KeyColour);

        /// <summary>0 air, 1 the board, 2 the bow, 3 the shank, 4 the bit.</summary>
        private static int KeyPart(int x, int z, int i)
        {
            int k = SilCourse(i, KEY_DEPTH);
            float below = k * INV_SQRT_TWO;
            SilPlan(x, z, i, out float dx, out float dz);

            if (k < 2) return MathF.Abs(dx) <= 5.5f && z >= 6 && z <= 10 ? 1 : 0;
            if (MathF.Abs(dz) > 1.1f) return 0;

            //The bow: a ring in the picture plane, its top against the board
            float by = below - 3.2f;
            float b = MathF.Sqrt(dx * dx + by * by);
            if (b <= 2.6f) return b >= 1.2f ? 2 : 0;

            if (below <= 9.2f && MathF.Abs(dx) <= 1.1f) return 3;

            //The bit, with two wards cut out of it
            if (below >= 6.6f && below <= 9.2f && dx > 1.1f && dx <= 3.8f)
            {
                bool ward = below > 7.3f && below < 8.0f && ((dx > 1.8f && dx < 2.4f) || dx > 3.2f);
                return ward ? 0 : 4;
            }

            return 0;
        }

        private static readonly BallType[] KEY_METAL = { BallType.Type1, BallType.Type7 };   //red, yellow

        private static BallType KeyColour(int x, int z, int i) => KeyPart(x, z, i) switch
        {
            1 => Band(x / 2 + z / 2, SIL_GROUND),
            2 => SILHOUETTE_INK,
            _ => KEY_METAL[(SilCourse(i, KEY_DEPTH) / 2) % 2],
        };

        //=== 7. ROCKET: flying level, crossed fins, glass portholes and a sealed core ====================

        private const byte ROCKET_DEPTH = 12;

        private const float ROCKET_AXIS = 2.0f;          //how far below the glass its long axis lies
        private const float ROCKET_R = 2.5f;
        private const float ROCKET_TAIL = -4.0f, ROCKET_SHOULDER = 3.0f, ROCKET_TIP = 6.4f;
        private const float ROCKET_CABIN = 1.2f;
        private const float ROCKET_CABIN_END = 2.4f;
        private const float ROCKET_FIN_ROOT = -1.4f;

        private static Design Rocket() => SilhouetteSolid("Rocket", ROCKET_DEPTH, shots: 32, ceilingStep: 7,
            (x, z, i) => RocketPart(x, z, i), RocketColour, RocketKind, payoff: (x, z, i) => RocketPart(x, z, i) == 6);

        /// <summary>0 air, 1 the hull, 2 the nose, 3 a fin, 4 the flame, 5 a porthole, 6 the core and its cord.</summary>
        private static int RocketPart(int x, int z, int i)
        {
            int k = SilCourse(i, ROCKET_DEPTH);
            float below = k * INV_SQRT_TWO;
            SilPlan(x, z, i, out float dx, out float dz);
            float dy = below - ROCKET_AXIS;
            float rad = MathF.Sqrt(dz * dz + dy * dy);

            //The hull's radius along its length: a cylinder, an elliptic nose and a narrower nozzle
            float hull = dx < ROCKET_TAIL - HALF ? -1f
                : dx < ROCKET_TAIL ? 1.6f
                : dx <= ROCKET_SHOULDER ? ROCKET_R
                : dx <= ROCKET_TIP ? ROCKET_R * MathF.Sqrt(1f - (dx - ROCKET_SHOULDER) * (dx - ROCKET_SHOULDER) / ((ROCKET_TIP - ROCKET_SHOULDER) * (ROCKET_TIP - ROCKET_SHOULDER)))
                : -1f;

            bool cord = MathF.Abs(dx) <= HALF && MathF.Abs(dz) <= HALF && dy <= 0f;

            if (rad <= hull)
            {
                //The cabin: a sealed cavity with the core hung in it on a cord from the glass
                if (rad < ROCKET_CABIN && MathF.Abs(dx) <= ROCKET_CABIN_END)
                    return cord || (dx * dx + dy * dy + dz * dz <= 0.8f * 0.8f) ? 6 : 0;
                if (cord) return 6;

                //Two round portholes in the wall that faces the gun
                if (dz > 0.8f && (Near(dx, dy, -1.4f) || Near(dx, dy, 1.4f))) return 5;

                return dx > ROCKET_SHOULDER ? 2 : 1;
            }

            //The flame streaming out behind
            if (dx < ROCKET_TAIL - HALF && dx >= -7.2f)
            {
                float flame = 1.9f * (dx + 7.2f) / 2.7f;
                if (rad <= flame) return 4;
            }

            //Three fins at the tail, swept back: one down, one towards the gun, one away (the glass cuts the fourth)
            if (dx >= ROCKET_TAIL && dx <= ROCKET_FIN_ROOT)
            {
                float reach = ROCKET_R + 2.6f * (ROCKET_FIN_ROOT - dx) / (ROCKET_FIN_ROOT - ROCKET_TAIL);
                if (MathF.Abs(dz) <= 0.6f && dy > 0f && dy <= reach) return 3;
                if (MathF.Abs(dy) <= 0.75f && MathF.Abs(dz) <= reach) return 3;
            }

            return 0;

            static bool Near(float px, float py, float cx) => (px - cx) * (px - cx) + py * py <= 0.85f * 0.85f;
        }

        private static readonly BallType[] ROCKET_PAINT = { BallType.Type4, SILHOUETTE_INK };   //white, black

        private static BallType RocketColour(int x, int z, int i) => RocketPart(x, z, i) switch
        {
            6 => BallType.Type6,                                                   //magenta core
            5 => BallType.Type5,                                                   //glass: unread
            4 => BallType.Type9,                                                   //orange flame
            3 => BallType.Type1,                                                   //red fins
            2 => BallType.Type1,                                                   //red nose
            _ => ROCKET_PAINT[((x + 1) / 2) % 2],                                  //white and black rings
        };

        private static BallKind RocketKind(int x, int z, int i) =>
            RocketPart(x, z, i) == 5 ? BallKind.Transparent : BallKind.Normal;

        //=== 8. CORONET: a crown hung from its points ====================================================

        private const byte CORONET_DEPTH = 10;

        private const int CORONET_POINTS = 5;
        private const float CORONET_INNER = 4.0f;
        private const float CORONET_OUTER = 5.4f;
        private const int CORONET_BAND = 6;              //the band's first course

        private static Design Coronet() => SilhouetteSolid("Coronet", CORONET_DEPTH, shots: 44, ceilingStep: 8,
            (x, z, i) => CoronetPart(x, z, i), CoronetColour, CoronetKind);

        /// <summary>0 air, 1 the band, 2 a point, 3 a ruby, 4 a diamond (glass), 5 the cap.</summary>
        private static int CoronetPart(int x, int z, int i)
        {
            int k = SilCourse(i, CORONET_DEPTH);
            float r = SilPlan(x, z, i, out float dx, out float dz);

            //The velvet cap in the middle, hung from the glass, narrowing as it comes down
            float cap = k switch { 0 => 2.8f, 1 => 2.7f, 2 => 2.5f, 3 => 2.1f, 4 => 1.5f, 5 => 0.8f, _ => -1f };
            if (r <= cap) return 5;

            if (r < CORONET_INNER || r > CORONET_OUTER) return 0;

            //How far round from the nearest point's middle, in turns
            float u = MathF.Atan2(dz, dx) / MathF.Tau * CORONET_POINTS + 0.25f;
            float off = MathF.Abs(u - MathF.Round(u)) / CORONET_POINTS;

            if (k >= CORONET_BAND)
            {
                //Jewels on the band's outer face: a ruby under each point, a diamond between
                if ((k == CORONET_BAND + 1 || k == CORONET_BAND + 2) && r > CORONET_OUTER - 1.0f)
                {
                    if (off < 0.03f) return 3;
                    if (off > 0.07f) return 4;
                }

                return 1;
            }

            //The points narrow from the band up to the glass
            float half = 0.02f + 0.0045f * k;
            return off <= half ? 2 : 0;
        }

        private static BallType CoronetColour(int x, int z, int i)
        {
            switch (CoronetPart(x, z, i))
            {
                case 5: return BallType.Type6;           //magenta velvet
                case 3: return BallType.Type1;           //ruby
                case 4: return BallType.Type5;           //glass: unread
                case 2: return BallType.Type7;           //gold points
            }

            SilPlan(x, z, i, out float dx, out float dz);
            return SilWedge(dx, dz, 10, 0.05f) % 2 == 0 ? SILHOUETTE_INK : BallType.Type3;
        }

        private static BallKind CoronetKind(int x, int z, int i) =>
            CoronetPart(x, z, i) == 4 ? BallKind.Transparent : BallKind.Normal;

        //=== 9. GUITAR: a solid guitar in the screen, its strings in glass ==============================

        private const byte GUITAR_DEPTH = 10;

        private const float GUITAR_LINE = 3.3f;          //how far below the glass its long axis lies
        private const float GUITAR_LOWER_X = -4.3f, GUITAR_LOWER = 2.8f;
        private const float GUITAR_UPPER_X = -0.8f, GUITAR_UPPER = 2.2f;
        private const float GUITAR_NECK_END = 5.3f, GUITAR_HEAD_END = 6.6f;
        private const float GUITAR_HOLE = 1.1f;
        private const float GUITAR_SCREEN = 6.8f;        //the screen's half width
        private const int GUITAR_SCREEN_Z = 7;

        private static Design Guitar() => SilhouetteSolid("Guitar", GUITAR_DEPTH, shots: 36, ceilingStep: 7,
            (x, z, i) => GuitarPart(x, z, i), GuitarColour, GuitarKind);

        /// <summary>Whether the guitar's outline, seen from the stand, covers a point of the picture plane.</summary>
        private static int GuitarOutline(float dx, float below)
        {
            float across = below - GUITAR_LINE;
            float lx = dx - GUITAR_LOWER_X, ux = dx - GUITAR_UPPER_X;
            if (lx * lx + across * across <= GUITAR_LOWER * GUITAR_LOWER || ux * ux + across * across <= GUITAR_UPPER * GUITAR_UPPER) return 4;
            if (dx > 0f && dx <= GUITAR_NECK_END && MathF.Abs(across) <= 0.8f) return 3;
            if (dx > GUITAR_NECK_END && dx <= GUITAR_HEAD_END && MathF.Abs(across) <= 1.3f) return 2;
            return 0;
        }

        /// <summary>0 air, 1 the screen, 2 the headstock, 3 the neck, 4 the body, 5 the strings.</summary>
        private static int GuitarPart(int x, int z, int i)
        {
            int k = SilCourse(i, GUITAR_DEPTH);
            float below = k * INV_SQRT_TWO;
            SilPlan(x, z, i, out float dx, out float dz);
            float across = below - GUITAR_LINE;
            float ox = dx - GUITAR_UPPER_X;
            float rz = dz + 1.0f;                        //depth about the guitar's own middle, a cell in front of the screen's

            int outline = GuitarOutline(dx, below);

            //The strings: two courses of glass in front of the face, from the bridge to the headstock
            if (MathF.Abs(across) <= 0.75f && dx >= GUITAR_LOWER_X && dx <= GUITAR_NECK_END && MathF.Abs(rz - 2.0f) <= HALF) return 5;

            if (outline == 4)
            {
                if (MathF.Abs(rz) > 1.6f) return 0;
                //A hollow sound box, the hole in its face over the upper bout
                float lx = dx - GUITAR_LOWER_X;
                bool inside = (MathF.Sqrt(lx * lx + across * across) <= GUITAR_LOWER - 1.1f
                               || MathF.Sqrt(ox * ox + across * across) <= GUITAR_UPPER - 1.1f) && MathF.Abs(rz) <= 0.6f;
                if (inside) return 0;
                if (rz > 0.6f && MathF.Sqrt(ox * ox + across * across) <= GUITAR_HOLE) return 0;
                return 4;
            }

            if (outline != 0) return MathF.Abs(rz) <= 0.6f ? outline : 0;

            //The screen round it: the check, but never under the guitar
            if (z != GUITAR_SCREEN_Z && z != GUITAR_SCREEN_Z + 1) return 0;
            if (MathF.Abs(dx) > GUITAR_SCREEN) return 0;
            for (int above = k - 1; above >= 0; above--)
                if (GuitarOutline(dx, above * INV_SQRT_TWO) != 0) return 0;
            return 1;
        }

        private static readonly BallType[] GUITAR_WOOD = { SILHOUETTE_INK, BallType.Type1 };   //black, red

        private static BallType GuitarColour(int x, int z, int i) => GuitarPart(x, z, i) switch
        {
            1 => Band(x / 2 + SilCourse(i, GUITAR_DEPTH) / 2, SIL_GROUND),
            2 => BallType.Type9,                       //orange headstock
            3 => BallType.Type10,                      //brown neck
            5 => BallType.Type5,                       //glass: unread
            _ => GUITAR_WOOD[(x / 2) % 2],
        };

        private static BallKind GuitarKind(int x, int z, int i) =>
            GuitarPart(x, z, i) == 5 ? BallKind.Transparent : BallKind.Normal;

        //=== 10. ANCHOR: hung from a beam by its ring and its stock, crossing walls ======================

        private const byte ANCHOR_DEPTH = 14;

        private static Design Anchor() => SilhouetteSolid("Anchor", ANCHOR_DEPTH, shots: 36, ceilingStep: 7,
            (x, z, i) => AnchorPart(x, z, i), AnchorColour);

        /// <summary>0 air, 1 the ring, 2 the stock's cords, 3 the stock, 4 the shank, 5 the arms, 6 a fluke, 7 the beam.</summary>
        private static int AnchorPart(int x, int z, int i)
        {
            int k = SilCourse(i, ANCHOR_DEPTH);
            float below = k * INV_SQRT_TWO;
            SilPlan(x, z, i, out float dx, out float dz);

            //The beam against the glass, running towards the gun, crossing the anchor's own plane
            if (k < 2) return MathF.Abs(dx) <= 2.6f && MathF.Abs(dz) <= 5.6f ? 7 : 0;

            //The stock across the shank's head, along z, and the two cords that hang it from the beam
            if (k >= 5 && k <= 6 && MathF.Abs(dx) <= 0.8f && MathF.Abs(dz) <= 5.6f) return 3;
            if (k < 5 && MathF.Abs(dx) <= HALF && MathF.Abs(MathF.Abs(dz) - 5.0f) <= 0.6f) return 2;

            if (MathF.Abs(dz) > 1.1f) return 0;

            //The ring under the beam
            float ry = below - 2.2f;
            float ring = MathF.Sqrt(dx * dx + ry * ry);
            if (k < 5) return ring <= 1.7f && ring >= 0.8f ? 1 : 0;

            //The arms: the lower half of a circle round the shank, ending in flukes that point up and out
            float ay = below - 4.6f;
            float arm = MathF.Sqrt(dx * dx + ay * ay);
            if (MathF.Abs(dx) >= 3.6f && MathF.Abs(dx) <= 6.2f && ay >= -1.6f && ay <= 1.0f
                && MathF.Abs(dx) - 3.6f <= (ay + 1.6f) * 1.0f + 0.4f) return 6;
            if (ay > 0.6f && arm >= 3.6f && arm <= 4.7f) return 5;

            if (MathF.Abs(dx) <= 1.1f && below <= 8.6f) return 4;
            return 0;
        }

        private static readonly BallType[] ANCHOR_BEAM = { BallType.Type4, BallType.Type5, BallType.Type10 };   //white, cyan, brown

        private static BallType AnchorColour(int x, int z, int i)
        {
            int part = AnchorPart(x, z, i);
            int k = SilCourse(i, ANCHOR_DEPTH);
            SilPlan(x, z, i, out float dx, out _);

            return part switch
            {
                7 => Band(z / 2, ANCHOR_BEAM),
                1 => BallType.Type7,                     //yellow ring
                2 => BallType.Type7,                     //and cords
                3 => BallType.Type10,                    //brown stock
                6 => BallType.Type1,                     //red flukes
                5 => dx < 0f ? BallType.Type3 : SILHOUETTE_INK,
                _ => (k / 2) % 2 == 0 ? SILHOUETTE_INK : BallType.Type3,
            };
        }

        #endregion
    }
}
