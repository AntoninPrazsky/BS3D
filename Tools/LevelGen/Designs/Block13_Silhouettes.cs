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

        //THE CHAPTER WITH DEPTH (#736). The owner's playtest of 2026-10-03: "The Aurora levels will need refactoring -
        //they are quite boring, similar to one another, flat. They lack pyramid shapes and any spatiality. There could
        //also already be some special balls in an Aurora level." What stood here since #491 were ten picture walls two
        //cells thick, a black image-model silhouette over a pale check: from the gun a poster, and walked round (two
        //seconds of A or D turn the gun about ninety degrees) a wall seen edge-on. His answer of 2026-10-11 was to
        //rebuild all ten at once, not a prototype first, and to make this the chapter the campaign meets glass in.
        //
        //EVERY LEVEL IS A SOLID, AND EACH HANGS FROM THE GLASS A DIFFERENT WAY. The subjects and the names stay:
        //  Fish      a relief: the fish swells out of its lit screen a cell a step in from its outline, front and back
        //  Umbrella  a solid of revolution hung by its own crown, twelve gores, a hooked handle, glass icicles on the rim
        //  Bell      a hollow bell hung by its shoulder, its red clapper sealed behind a floor of four glass panes
        //  Cat       a black cat puppet in the round in front of the lit screen, hung by its ears too, glass eyes
        //  Teapot    a pot hung by its lid, striped in twelve staves, with the tea sealed inside on its own cord
        //  Key       a key hung by its bow under a board, and the bow is the only black
        //  Rocket    flying level with its back against the glass: crossed fins, glass portholes, a sealed core
        //  Coronet   a crown hung from its five gold points, a velvet cap inside, diamonds of glass in the band
        //  Guitar    a guitar four cells deep set into its screen, a hollow sound box, its two strings of glass
        //  Anchor    hung by its stock against the glass, its arms crossing it, the flukes lashed up to the stock
        //
        //THE CHAPTER'S LOOK IS STILL ONE IDEA, a shadow play under the northern lights: black figures, and where a
        //figure needs a ground to read against, the lit screen behind or round it in pale colours (Fish, Cat, Guitar).
        //A solid in black alone shows no depth from the stand - the eye reads black as a hole whatever its shape - so
        //a body that has to read as round wears its form in two colours (the umbrella's gores, the bell's and the
        //teapot's staves, the rocket's bands), and the black is what the figure is cut from.
        //
        //WHAT READS FROM THE WALK. Ninety degrees round, a flat thing goes edge-on whatever is done to it, so the
        //bodies of revolution (Umbrella, Bell, Teapot, Coronet) are the ones that read the same from everywhere; the
        //others are drawn to read from the stand and at forty-five degrees, and to be a sensible solid from the side
        //(the cat's profile, the rocket nose-on with its fins in a cross, the anchor's stock across its arms).
        //
        //THE CAMPAIGN'S FIRST GLASS IS THE UMBRELLA'S, the second level: the Fish is where the Cut's card comes up
        //(LevelSet.CUT_FROM_BLOCK), so one new thing arrives a level. Its icicles are the lowest, outermost thing on
        //the canopy - where the first shots arrive - and each is two balls long, so the first landing beside one
        //colours both and makes three with itself: the rule states itself on the first shot, the Mirage's Facet
        //lesson brought forward. After it, glass is what a thing in the picture is made of: the bell's floor, the
        //cat's eyes, the rocket's portholes, the crown's diamonds, the guitar's strings. Every pane is on the skin and
        //none on the anchor course (the Mirage's law, FindStrandedSpecials), and the kind card (#735) fires on the
        //Umbrella, the first level whose map carries it.
        //
        //WHAT IS SEALED. Three levels take up the Reveal's payoff (#743, Design.Payoff): the bell's clapper behind its
        //glass floor - seen through the glass, and opened by colouring a pane - the teapot's tea and the rocket's core,
        //each hung from the glass on a cord of its own, in a colour nothing else in the level wears.
        //
        //WHAT THE LATTICE TAUGHT ON THE WAY, each a draft that passed or failed a gate for a reason worth keeping:
        //  - Wedges of two colours round an axis converge where the radius gets small, and two of a colour touch there:
        //    the teapot's black staves met just under its lid as one 126-ball group. Every body of revolution keeps a
        //    cap or a ring of another colour where its staves would meet (the umbrella's ferrule, the bell's crown,
        //    the teapot's lid and foot).
        //  - A two-colour checker of wedge and course welds along a diagonal through the cross-level contacts, as the
        //    Quarry's tiles do: the rocket's first hull and the basket a cat once sat in came out one spiral of a colour.
        //  - Heavy parts on thin links fold: the first anchor, hung by a ring and two cords one cell thick, swung over
        //    in the start swing and crossed the line with no shot fired, 5 runs of 5. Hung by its stock against the
        //    glass, on a shank three cells square, it reads 0 of 5. The cat leaning on its screen alone came 0.03
        //    from the line once the screen behind it was cut; with its ears to the glass as well, 1.95.
        //  - A body hung from a few groups clears in two or three cuts and the shortest-clear gate passes it (the
        //    matched share is small): a guitar on a bracket emptied in 3 shots, the cat on a moon disc in 2. Read the
        //    line on every level, not only on the refused ones; both now hang from many groups.
        //
        //WHAT THEY MEASURE (LevelGen, 2026-10-12): balls / standing groups / shots, the one shot that drops most,
        //and the glass.
        //  Fish      368 / 18 / 46   51 %, the black body          Umbrella  368 / 13 / 36   13 %, 24 glass
        //  Bell      352 / 15 / 40   39 %, lip, ribs and panes     Cat       331 / 11 / 30   37 %, the cat; 4 glass
        //  Teapot    344 / 17 / 40    9 %                          Key       223 / 13 / 32   48 %, the black bow
        //  Rocket    244 / 12 / 28   29 %, 11 glass                Coronet   367 / 21 / 42   24 %, the cap; 19 glass
        //  Guitar    269 / 16 / 34   37 %, 28 glass                Anchor    252 / 14 / 28   13 %
        //⚠ THE WEIGHT IS KEPT, THE LENGTH IS NOT. The ten carry 3,118 balls against the 3,254 of the walls they replace
        //(#742's lesson: a rebuild that adds balls changes the difficulty unasked), but a solid comes apart in fewer,
        //larger pieces than a check does: 150 standing groups against 211, so the budgets follow them down, 356 shots
        //against 524, each level keeping roughly its old place on the ramp of shots a group (2.6 to 2.8 on the first
        //four, 2.0 to 2.3 on the last four). Every level hung 0 times in 5 in the sag probe at its own budget and step.
        //
        //THE FILES ARE THE OLD ONES: a save is keyed by the file, and a level that changes under its file keeps the
        //road open. The online boards start again (LevelIdentity hashes the bytes), and the ceilings table changes.

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

        /// <summary>
        /// The fish, and the chapter's opener: the picture the chapter used to be, swelling into a solid. The black fish
        /// stands a cell proud of its screen for every step in from its outline, front and back, up to <see cref="FISH_SWELL"/>,
        /// so from the stand it is the picture the player knows and walked round it is a body with a back. One ink: the
        /// whole fish is one group, half the level on the first black ball - the gentle opener's own payoff - and the
        /// screen's check (white and cyan, nothing under the fish, #556) is the work.
        /// </summary>
        private static Design Fish() => SilhouetteSolid("Fish", FISH_DEPTH, shots: 46, ceilingStep: 9,
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

        /// <summary>
        /// An umbrella, a solid of revolution hung by its own crown: a stepped canopy of twelve gores in black and white,
        /// each reaching the glass alone, a red ferrule, shaft and hook in one group, and the campaign's FIRST GLASS - an
        /// icicle two balls long under the middle of every gore. The icicles are the lowest, outermost thing on the
        /// canopy, so the first landing beside one colours both and the three fall; landed in the colour of the gore
        /// above, the gore comes down with them. It reads as an umbrella from every side of the walk.
        /// </summary>
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

        /// <summary>
        /// A hollow bell hung by its shoulder, staves of black and yellow round a white crown, its mouth closed by a floor
        /// of glass in four panes on a white cross of ribs, and the red clapper sealed inside on its own cord (a reveal,
        /// <see cref="Design.Payoff"/>): the payoff is seen through the glass from the first frame. A landing under a pane
        /// colours it; in white it joins the lip band and the ribs and the whole floor drops at once, which is the way in.
        /// </summary>
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
        private const float CAT_SCREEN = 4.6f;
        private const int CAT_SCREEN_COURSES = 11;
        private const int CAT_SCREEN_Z = 5;

        /// <summary>
        /// A black cat puppet in the round, sitting in front of the lit screen - the chapter's shadow play made solid: a
        /// head with two glass eyes and ears that reach the glass (it hangs by them as well as against the screen), the
        /// body it sits on, a tail curled round its feet, against a screen checked in three pale colours. The cat is one
        /// group, the attractive shot; the screen is the work.
        /// </summary>
        private static Design Cat() => SilhouetteSolid("Cat", CAT_DEPTH, shots: 30, ceilingStep: 7,
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
            //The ears reach the glass: the puppet hangs by them as well as against its screen, a second load path
            if (k <= 2 && MathF.Abs(MathF.Abs(dx) - 1.2f) <= (k < 2 ? 0.5f : 0.9f) && MathF.Abs(dz) <= 0.6f) return 2;

            float by = (below - 6.5f) / 2.0f;
            if (dx * dx / (2.6f * 2.6f) + dz * dz / (2.0f * 2.0f) + by * by <= 1f) return 2;

            if (MathF.Abs(dz - 0.6f) <= 0.6f
                && (SegmentDistance(dx, below, 2.0f, 8.0f, 4.0f, 8.3f) <= 0.65f
                    || SegmentDistance(dx, below, 4.0f, 8.3f, 4.7f, 6.4f) <= 0.65f))
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

        /// <summary>
        /// A teapot hung by its lid: a squat round pot in twelve staves of black and white, the lid, spout, handle and a
        /// foot ring red, and the tea sealed inside - orange, hung from the glass on a cord through the lid, in a cavity
        /// no shot reaches until a stave is opened (a reveal). Seventeen groups and no big single shot: the chapter's
        /// middle is worked, not triggered.
        /// </summary>
        private static Design Teapot() => SilhouetteSolid("Teapot", TEAPOT_DEPTH, shots: 40, ceilingStep: 8,
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

        /// <summary>
        /// A key hung by its bow under a pale board: the bow a ring the board shows through, the shank and the warded bit
        /// in bands of red and yellow. The bow is the only black in the level, so one black ball lets the shank and the
        /// bit go - the key colour the Quarry's Hoist plays later - and the board's check is the work.
        /// </summary>
        private static Design Key() => SilhouetteSolid("Key", KEY_DEPTH, shots: 32, ceilingStep: 7,
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
        private const float ROCKET_R = 2.3f;
        private const float ROCKET_TAIL = -4.6f, ROCKET_SHOULDER = 1.6f, ROCKET_TIP = 6.8f;
        private const float ROCKET_CABIN = 1.1f;
        private const float ROCKET_CABIN_END = 2.0f;
        private const float ROCKET_FIN_ROOT = -2.2f;

        /// <summary>
        /// A rocket flying level, its back against the glass: a white hull with two black bands, a long red nose, three red fins in a
        /// cross (down, towards the gun and away from it; the glass takes the fourth), an orange flame streaming behind,
        /// and two glass portholes in the side facing the gun with the magenta core sealed behind them on its own cord (a
        /// reveal), hung in the cabin's far half so that no pane touches it. Seen nose-on from the walk, the fins make
        /// the cross.
        /// </summary>
        private static Design Rocket() => SilhouetteSolid("Rocket", ROCKET_DEPTH, shots: 28, ceilingStep: 6,
            (x, z, i) => RocketPart(x, z, i), RocketColour, RocketKind, payoff: (x, z, i) => RocketPart(x, z, i) == 6);

        /// <summary>0 air, 1 the hull, 2 the nose, 3 a fin, 4 the flame, 5 a porthole, 6 the core and its cord.</summary>
        private static int RocketPart(int x, int z, int i)
        {
            int k = SilCourse(i, ROCKET_DEPTH);
            float below = k * INV_SQRT_TWO;
            SilPlan(x, z, i, out float dx, out float dz);
            float dy = below - ROCKET_AXIS;
            float rad = MathF.Sqrt(dz * dz + dy * dy);

            //The hull's radius along its length: a nozzle, a cylinder and a long nose drawn to a point
            float hull = dx < ROCKET_TAIL - HALF ? -1f
                : dx < ROCKET_TAIL ? 1.5f
                : dx <= ROCKET_SHOULDER ? ROCKET_R
                : dx <= ROCKET_TIP ? ROCKET_R * MathF.Pow(1f - (dx - ROCKET_SHOULDER) / (ROCKET_TIP - ROCKET_SHOULDER), 0.6f)
                : -1f;

            //The core and its cord keep to the cabin's far half, away from the portholes: a pane beside the core
            //takes the colour of the shot landed beside it, so one magenta landing there would drop the payoff
            //through the window with the hull whole (LevelGates.RevealFaults counts glass beside a payoff)
            bool back = dz <= 0f;
            bool cord = back && MathF.Abs(dx) <= HALF && MathF.Abs(dz) <= HALF && dy <= 0f;

            if (rad <= hull)
            {
                //The cabin: a sealed cavity with the core hung in it on a cord from the glass
                if (rad < ROCKET_CABIN && MathF.Abs(dx) <= ROCKET_CABIN_END)
                    return cord || (back && dx * dx + dy * dy + dz * dz <= 0.8f * 0.8f) ? 6 : 0;
                if (cord) return 6;

                //Two round portholes in the wall that faces the gun
                if (dz > 0.8f && (Near(dx, dy, -1.5f) || Near(dx, dy, 1.1f))) return 5;

                return dx > ROCKET_SHOULDER ? 2 : 1;
            }

            //The flame streaming out behind
            if (dx < ROCKET_TAIL - HALF && dx >= -7.0f)
            {
                float flame = 1.6f * (dx + 7.2f) / 2.1f;
                if (rad <= flame) return 4;
            }

            //Three fins at the tail, swept back: one down, one towards the gun, one away (the glass cuts the fourth)
            if (dx >= ROCKET_TAIL && dx <= ROCKET_FIN_ROOT)
            {
                float reach = ROCKET_R + 2.4f * (ROCKET_FIN_ROOT - dx) / (ROCKET_FIN_ROOT - ROCKET_TAIL);
                if (MathF.Abs(dz) <= 0.6f && dy > 0f && dy <= reach) return 3;
                if (MathF.Abs(dy) <= 0.75f && MathF.Abs(dz) <= reach) return 3;
            }

            return 0;

            static bool Near(float px, float py, float cx) => (px - cx) * (px - cx) + py * py <= 0.85f * 0.85f;
        }

        /// <summary>Whether a hull cell is in one of its two black bands: round the tail, and between the portholes.</summary>
        private static bool RocketBand(int x, int i)
        {
            SilPlan(x, 0, i, out float dx, out _);
            return (dx >= ROCKET_TAIL && dx < ROCKET_TAIL + 0.9f) || (dx >= -0.6f && dx < 0.4f);
        }

        private static BallType RocketColour(int x, int z, int i) => RocketPart(x, z, i) switch
        {
            6 => BallType.Type6,                                                   //magenta core
            5 => BallType.Type5,                                                   //glass: unread
            4 => BallType.Type9,                                                   //orange flame
            3 => BallType.Type1,                                                   //red fins
            2 => BallType.Type1,                                                   //red nose
            _ => RocketBand(x, i) ? SILHOUETTE_INK : BallType.Type4,               //a white hull with two black bands
        };

        private static BallKind RocketKind(int x, int z, int i) =>
            RocketPart(x, z, i) == 5 ? BallKind.Transparent : BallKind.Normal;

        //=== 8. CORONET: a crown hung from its points ====================================================

        private const byte CORONET_DEPTH = 10;

        private const int CORONET_POINTS = 5;
        private const float CORONET_INNER = 4.0f;
        private const float CORONET_OUTER = 5.4f;
        private const int CORONET_BAND = 6;              //the band's first course

        /// <summary>
        /// A crown hung from its five gold points: a band of black and blue below them, set with rubies under the points
        /// and diamonds of glass between, and a magenta velvet cap inside, hung from the glass on its own. The band hangs
        /// from the points alone, so the points are the cuts that matter; the most groups in the chapter.
        /// </summary>
        private static Design Coronet() => SilhouetteSolid("Coronet", CORONET_DEPTH, shots: 42, ceilingStep: 8,
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
            float half = 0.032f + 0.003f * k;
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

        /// <summary>
        /// A guitar lying on its back, four cells deep and set into a pale screen: a hollow sound box with the hole in its
        /// face, a brown neck, an orange headstock, a body striped black and red, and two strings of glass from the bridge
        /// to the headstock that touch each other, so one landing colours both, and in the colour of what they lie on it
        /// takes that too. The screen round it holds it at many places, so no handful of cuts drops the guitar.
        /// </summary>
        private static Design Guitar() => SilhouetteSolid("Guitar", GUITAR_DEPTH, shots: 34, ceilingStep: 7,
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

        //=== 10. ANCHOR: hung by its stock against the glass, its arms crossing it =======================

        private const byte ANCHOR_DEPTH = 14;

        private const float ANCHOR_STOCK = 5.6f;          //the stock's half length, along x against the glass
        private const int ANCHOR_SHANK_LO = 7, ANCHOR_SHANK_HI = 9;   //the shank's raw x and z: three cells square on every course
        private const float ANCHOR_CROWN = 4.6f;          //how far below the glass the arms' circle is centred
        private const float ANCHOR_ARM_IN = 3.4f, ANCHOR_ARM_OUT = 4.6f;
        private const float ANCHOR_FLUKE_X = 4.7f, ANCHOR_FLUKE_Y = -0.4f;   //each fluke's middle, from the arms' centre

        /// <summary>
        /// An anchor, the chapter's closer, hung by its stock against the glass with its arms crossing it: a shank three
        /// cells square in bands of black and blue, arms of blue and black, red flukes, and each fluke lashed up to the
        /// stock's end with a yellow rope. Two load paths, so no single cut drops the arms, and the tightest budget in the
        /// chapter, two shots a group.
        /// </summary>
        private static Design Anchor() => SilhouetteSolid("Anchor", ANCHOR_DEPTH, shots: 28, ceilingStep: 6,
            (x, z, i) => AnchorPart(x, z, i), AnchorColour);

        /// <summary>0 air, 2 a lashing, 3 the stock, 4 the shank, 5 the arms, 6 a fluke.</summary>
        private static int AnchorPart(int x, int z, int i)
        {
            int k = SilCourse(i, ANCHOR_DEPTH);
            float below = k * INV_SQRT_TWO;
            SilPlan(x, z, i, out float dx, out float dz);

            //The stock: a bar across the top, against the glass, rounded at its ends
            if (k < 2) return MathF.Abs(dz) <= 1.6f && MathF.Abs(dx) <= ANCHOR_STOCK - (k == 1 ? 0.6f : 0f) ? 3 : 0;

            //The arms and flukes, in the picture plane, and the lashings that tie each fluke up to the stock's end
            float ay = below - ANCHOR_CROWN;
            if (MathF.Abs(dz) <= 1.1f)
            {
                float arm = MathF.Sqrt(dx * dx + ay * ay);
                float spade = MathF.Abs(MathF.Abs(dx) - ANCHOR_FLUKE_X) + MathF.Abs(ay - ANCHOR_FLUKE_Y) * 0.8f;
                if (spade <= 1.5f) return 6;
                if (ay > 0.4f && arm >= ANCHOR_ARM_IN && arm <= ANCHOR_ARM_OUT) return 5;
            }
            if (MathF.Abs(MathF.Abs(dx) - ANCHOR_FLUKE_X) <= 0.6f && MathF.Abs(dz) <= 0.6f && ay < ANCHOR_FLUKE_Y) return 2;

            //The shank, three cells square on every course
            if (x >= ANCHOR_SHANK_LO && x <= ANCHOR_SHANK_HI && z >= ANCHOR_SHANK_LO && z <= ANCHOR_SHANK_HI
                && below <= ANCHOR_CROWN + ANCHOR_ARM_IN + 0.2f) return 4;
            return 0;
        }

        private static readonly BallType[] ANCHOR_STOCK_WOOD = { BallType.Type10, BallType.Type4 };              //brown, white
        private static readonly BallType[] ANCHOR_IRON = { SILHOUETTE_INK, BallType.Type3 };   //black, blue

        private static BallType AnchorColour(int x, int z, int i)
        {
            int part = AnchorPart(x, z, i);
            int k = SilCourse(i, ANCHOR_DEPTH);
            SilPlan(x, z, i, out float dx, out _);

            return part switch
            {
                3 => ANCHOR_STOCK_WOOD[((x + 1) / 3) % 2],
                2 => BallType.Type7,                                     //yellow lashings
                6 => BallType.Type1,                                     //red flukes
                5 => dx < 0f ? BallType.Type3 : SILHOUETTE_INK,
                _ => ANCHOR_IRON[(k / 2) % 2],
            };
        }

        #endregion
    }
}
