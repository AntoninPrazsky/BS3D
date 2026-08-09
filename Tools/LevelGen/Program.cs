using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// Writes the game's pattern levels (Three to Seven) and the set that orders them, and <b>validates
    /// every one through the game's own loader</b> before it is written anywhere the game will see it.
    /// A design is one <see cref="Design"/>: a silhouette, a colouring, a scene and a set of rules.
    /// <para>
    /// It exists because these levels are generated, and a generated level that is only checked by
    /// playing it is checked by nobody. The three properties it enforces are all invisible in a
    /// screenshot and all of them were got wrong at least once here — see <see cref="Validate"/>,
    /// <see cref="DropTest"/> and <see cref="FindLonelyBalls"/>. What it cannot check is whether the
    /// thing looks good, which is what the screenshot skill is for.
    /// </para>
    /// <para>
    /// <c>dotnet run --project Tools\LevelGen\LevelGen.csproj [output directory]</c>. Rewrites
    /// <c>Levels.json</c> too, One and Two included, so run it whole rather than for one level.
    /// </para>
    /// </summary>
    internal static class Program
    {
        // The field is 16 levels deep for every design: that is the deepest field the game hangs at its
        // standard height (FIELD_TOP_Y, 8/sqrt2) without raising it off the death line, so every level is
        // framed by the camera and the gun exactly the way One.json is. The layout hangs at the top and
        // the empty levels under it are the room shot balls attach into.
        private const byte FIELD_LEVELS = 16;

        private const float HALF = 0.5f;

        /// <summary>
        /// The smallest standing group a ball may belong to. It is <c>MINIMUM_CLUSTER_SIZE</c> less the one
        /// ball the player is about to land: a pair plus a shot is three and falls, a lone ball plus a shot
        /// is two and does nothing. Stated here rather than read off the physics constant because it is a
        /// statement about <i>authoring</i>, and the arithmetic linking them is the point of it.
        /// </summary>
        private const int MIN_GROUP = 3 - 1;

        /// <summary>
        /// How much of the cluster one shot may take before the level stops being a level. Not 100: a design
        /// whose best shot leaves a handful of balls standing is still over on the first lucky ball, and the
        /// two banded designs that failed this took 100% exactly, so the margin costs nothing and catches
        /// the near misses. The pack runs 5–38%.
        /// </summary>
        private const int ONE_SHOT_PERCENT = 90;

        /// <summary>Where the levels are written. Set once in <see cref="Main"/>, read by everything below.</summary>
        private static string _outDir;

        private static int Main(string[] args)
        {
            try
            {
                _outDir = args.Length > 0 ? args[0] : FindLevelsDirectory();
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
                return 1;
            }

            Console.WriteLine($"Writing to {_outDir}");

            //In play order. The gentle pattern levels first, then the two that ask for real aim — see
            //WriteLevelSet for where One and Two sit around them.
            Design[] designs =
            {
                One(), Bullseye(), Mosaic(), Pinwheel(), Crown(), Gem(), Prism(), Static(), Column(), Onion()
            };

            bool ok = true;
            foreach (Design design in designs) ok &= Emit(design);

            WriteLevelSet(designs);

            //A non-zero exit so this can be put in front of a commit: a level that fails the checks is a
            //level that plays wrong, and the whole point of generating them is that nobody has to notice
            if (!ok) Console.WriteLine("At least one level FAILED its checks - see above.");

            return ok ? 0 : 1;
        }

        /// <summary>
        /// The game's <c>Game\Levels</c>, found by walking up from wherever this was built to the repository
        /// root. The tool lives at a known depth inside the repo but is <i>run</i> from its bin directory,
        /// whose depth depends on the configuration and target framework, so the walk is by landmark rather
        /// than by counting <c>..</c> — and an explicit directory can always be passed instead.
        /// </summary>
        private static string FindLevelsDirectory()
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Game", "Levels");
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException(
                $"No 'Game\\Levels' directory above '{AppContext.BaseDirectory}'. Pass the output directory as an argument.");
        }

        /// <summary>
        /// Rewrites the set that orders the levels. <b>One opens the campaign and Two closes it</b>; One is a
        /// design here now (the author asked for it regenerated, see <see cref="One"/>) and states its own
        /// rules with the rest, while Two is still hand-drawn and keeps its rules verbatim — it is authored
        /// content and this generator has no opinion about it beyond where it sits.
        /// <para>
        /// Two used to be second and is the hardest level in the game by a distance: twelve wide, eighteen
        /// deep, six colours in 3×3 blocks rolled per level so nothing is ever a big easy plate, on 45 shots
        /// against a ceiling stepping every 4. Meeting that second is meeting the wall before the game has
        /// taught anything, and it is the level worth finishing last.
        /// </para>
        /// </summary>
        private static void WriteLevelSet(Design[] designs)
        {
            LevelSet set = new() { Name = "Bubble Shooter 3D" };

            foreach (Design d in designs)
                set.Levels.Add(new LevelSetEntry
                {
                    File = d.File,
                    Name = d.Name,
                    Shots = d.Shots,
                    CeilingStep = d.CeilingStep,
                });

            set.Levels.Add(new LevelSetEntry { File = "Two.json", Name = "Two", Shots = 45, CeilingStep = 4 });

            string path = Path.Combine(_outDir, LevelSet.DefaultFileName);
            set.Save(path);

            //Read back through the game's own validating loader, which is the only thing that can say the
            //set is well formed — it is what refuses a zero budget or a level that names no file
            LevelSet loaded = LevelSet.Load(path);

            Console.WriteLine();
            Console.WriteLine($"=== {LevelSet.DefaultFileName}: {loaded.Count} levels ===");
            for (int i = 0; i < loaded.Count; i++)
                Console.WriteLine($"  {i + 1}. {loaded.DisplayName(i),-12} {loaded.DescribeRules(i)}");
        }

        #region The designs

        /// <summary>
        /// The campaign's opener: a full square slab against the glass with a round pyramid tapering to a
        /// point under it. It replaces the hand-drawn <c>One.json</c>, at the author's request and for two
        /// reasons they gave — the pyramid had a <b>tail</b>, and it had no sky of its own.
        /// <para>
        /// The tail was real and is gone. The old layout narrowed from a 100-ball slab down to a single ball
        /// nine levels below it and then <b>widened again</b>, to 4, 8, 12, 25 and 16 — sixty-five balls
        /// hanging under the point, which reads as something stuck to the pyramid rather than as part of it.
        /// The point is now the bottom of the level.
        /// </para>
        /// <para>
        /// Two things came free with the regeneration. The old field was 10 wide against a slab occupying
        /// x 0…9 — <b>no lateral margin at all</b>, the wall trap that made shots bounce off the flanks of
        /// the pattern pack; twelve gives it the clear column every other level now has. And a plain map file
        /// carries no scene, so One played in whichever backdrop the player last picked; as a level file it
        /// opens the game in the savanna under the warmest dome of the set (14, the one the Testbed itself
        /// defaults that scene to).
        /// </para>
        /// <para>
        /// Four colours in concentric rings, which is a deliberate simplification: the old One used
        /// <b>eight</b>, and the magazine draws evenly among the colours still alive, so the wanted ball
        /// arrived one time in eight — a harder draw than anything after it, on the level that teaches the
        /// game. Rings also keep several colours on the slab, which is the anchor layer, so no single ball
        /// can cut the whole level loose.
        /// </para>
        /// </summary>
        private static Design One() => new()
        {
            File = "One.json",
            Name = "One",
            Grid = 12,
            Depth = 10,
            Scene = new SavannaSceneConfig(),
            Sky = 14,
            Shots = 30,
            CeilingStep = 5,
            //The slab is SQUARE and everything under it is round, which is the shape the old One had and the
            //reason this reads as a pyramid hanging off a plate rather than as a cone. The square extent is
            //recovered from the polar pair the emitter passes: max(|cos|,|sin|) scaled by the radius is the
            //Chebyshev distance, i.e. the half-extent of the square the point sits on.
            Occupied = (r, ang, i, depth) => i == depth - 1
                ? Chebyshev(r, ang) <= 4.5f
                : r <= 0.4f + i * 0.375f,
            //Each shape gets rings of its OWN kind — square ones on the square slab, round ones on the round
            //pyramid. Round rings over the whole thing was the first try and it left the slab's four corners
            //poking out past the last round ring into a ring of their own: ten balls of a fourth colour, in
            //threes, in the corners, on the level that teaches the game. Squared, the slab's outermost ring
            //is a proper border and the palette is three honest colours.
            Colour = (r, ang, i, depth) => Ring(i == depth - 1 ? Chebyshev(r, ang) : r,
                new[] { BallType.Type1, BallType.Type7, BallType.Type4 }),
        };

        /// <summary>
        /// A stepped cone hanging point-down, coloured in concentric rings: a target seen from underneath,
        /// and a flight of coloured steps seen from the side. Three colours, each ring one solid shell, so
        /// every ring is already a group of dozens waiting for one matching ball to touch it.
        /// </summary>
        private static Design Bullseye() => new()
        {
            File = "Three.json",
            Name = "Bullseye",
            Grid = 15,
            Depth = 4,
            Scene = new MeadowSceneConfig(),
            //Dome 1 is the only clear blue one in the set; most of the rest are warm or magenta, and over
            //green hills those read as a clash rather than as weather. A red-and-gold target wants that blue.
            Sky = 1,
            Shots = 40,
            CeilingStep = 8,
            //Widest at the top (that layer anchors the whole cluster to the glass) and narrowing downwards
            Occupied = (r, ang, i, depth) => r <= 5.7f - (depth - 1 - i) * 1.15f,
            Colour = (r, ang, i, depth) => Ring(r, new[] { BallType.Type1, BallType.Type4, BallType.Type7 }),
        };

        /// <summary>
        /// A cylinder tiled in 2x2x2 blocks of colour — a chunky mosaic column. Horizontal colour bands
        /// were the first try and they were a <b>one-shot level</b>: a band is a single group, and the top
        /// band is what holds the cluster to the glass, so one matching ball dropped all 387 balls at once.
        /// Blocks keep the graphic look and put all three colours on the anchor layer, which is the rule
        /// every design here has to satisfy — see the drop test in <see cref="Validate"/>.
        /// </summary>
        private static Design Mosaic() => new()
        {
            File = "Four.json",
            Name = "Mosaic",
            Grid = 13,
            Depth = 6,
            //A dark room for the brightest cluster in the game. It played over the sea, which was the wrong
            //pairing twice over: the sea mirrors its dome, so it is a large area of whatever the sky is doing
            //and never a backdrop, and against a lit dusk the CMY blocks had nothing to be vivid against.
            //The cavern is enclosed and dark, so the mosaic is the only saturated thing in the frame.
            Scene = new CavernSceneConfig(),
            Sky = 13,
            //The one design that has to be worked rather than triggered: its best shot takes 24 of 387
            //balls, so it wants a budget nearer two shots per block than the four-shot cascades elsewhere
            Shots = 56,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => r <= 4.5f,
            //Blocked on the raw indices rather than on the centred position: the blocks are meant to be
            //lattice-aligned, and the half-cell stagger between levels is the packing showing through.
            BlockColour = (x, z, i) => Band((x / 2) + (z / 2) + (i / 2),
                new[] { BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        /// <summary>
        /// A disc cut into four spiral sectors — a pinwheel from below, four vertical wedges from the side.
        /// The twist term is what bends the sector boundaries into a spiral instead of a cross.
        /// </summary>
        private static Design Pinwheel() => new()
        {
            File = "Five.json",
            Name = "Pinwheel",
            Grid = 15,
            Depth = 4,
            Scene = new DesertSceneConfig(),
            Sky = 7,
            Shots = 44,
            CeilingStep = 9,
            Occupied = (r, ang, i, depth) => r <= 5.5f,
            Colour = (r, ang, i, depth) => Sector(ang, r * 0.16f, 4,
                new[] { BallType.Type1, BallType.Type7, BallType.Type2, BallType.Type3 }),
        };

        /// <summary>
        /// A hollow ring six levels tall, in vertical bars of colour — a crown, with the drain visible
        /// straight up through the middle of it. The hole is the point: a shot fired up the axis goes
        /// clean through, so the player has to work the ring rather than spray at the centre.
        /// </summary>
        private static Design Crown() => new()
        {
            File = "Six.json",
            Name = "Crown",
            Grid = 15,
            Depth = 6,
            Scene = new MountainSceneConfig(),
            //Dome 8, a deep violet dusk, and not the 10 this shipped with. Under 10 the peaks came out pale
            //sand against a candy-pink sky and the whole frame read as kitsch; under 8 they read as snow and
            //the sky as weather, which is the same scene doing what it was built to do. The crown's gold and
            //red carry against a dark sky, where against pink they were competing with it.
            Sky = 8,
            Shots = 44,
            CeilingStep = 9,
            Occupied = (r, ang, i, depth) => r >= 2.9f && r <= 5.5f,
            //Six bars around the ring, three colours alternating: neighbouring bars never share a colour
            Colour = (r, ang, i, depth) => Sector(ang, 0f, 6,
                new[] { BallType.Type7, BallType.Type3, BallType.Type1, BallType.Type7, BallType.Type3, BallType.Type1 }),
        };

        /// <summary>
        /// An octahedron hanging point-down, cut into concentric diamond rings — the angular answer to
        /// <see cref="Bullseye"/>'s round ones, and the one design whose silhouette reads from any angle.
        /// Banding it by height was the first try and it lost the level in one shot for the reason given
        /// on <see cref="Mosaic"/>; rings put three colours on the anchor layer.
        /// </summary>
        private static Design Gem() => new()
        {
            File = "Seven.json",
            Name = "Gem",
            //Seventeen where the round designs need fifteen, and the diamond is why: a taxicab rim of m = 7
            //reaches seven whole cells along each axis, where a round radius of 5.5 reaches five. Fifteen
            //put the four points of the diamond exactly ON the field wall — no lateral margin at all, the
            //trap LateralMargin now refuses. Widening the field keeps the shape, which is the thing worth
            //keeping here; capping the rim at m = 5 would have cost the gem two rings of its widest face.
            Grid = 17,
            Depth = 6,
            Scene = new DreamSceneConfig(),
            Sky = 13,
            Shots = 44,
            CeilingStep = 9,
            //Diamond cross-section (|dx| + |dz|), widening towards the top, and rings measured on that same
            //taxicab radius so the rings follow the facets instead of cutting across them.
            //
            //Both numbers below are forced by the lattice rather than chosen for looks. The taxicab radius
            //only ever lands on whole numbers, and cells on one level touch only ORTHOGONALLY — so a ring one
            //unit wide is a diagonal staircase of balls that do not touch each other at all. Rings are
            //therefore two units wide (floor(m/2), not the m/2.2 this started with), and every layer's rim
            //stops on an ODD m so its outermost ring is a complete two, never a bare diagonal. The first Gem
            //broke both: its top layer's rim was the single shell m=7, twenty-four balls standing alone
            //against the glass with no level above to connect through, each needing two landed balls of its
            //own colour before anything could fall. It is the hardest defect here to see and the easiest to
            //author by accident, which is why FindLonelyBalls now refuses it.
            OccupiedManhattan = (m, i, depth) => m <= 1 + 2 * ((i + 1) / 2),
            //Yellow rather than the magenta this started with: the dream scene is a violet soup and the
            //magenta ring sank into it, which a screenshot showed and a palette on paper would not have
            ColourManhattan = (m, i, depth) => Band((int)MathF.Floor(m * HALF),
                new[] { BallType.Type7, BallType.Type3, BallType.Type5 }),
        };

        /// <summary>
        /// A stepped cone tiled in 2×2×1 blocks of five colours — the first level of the pack that has to be
        /// aimed rather than triggered. Everything before it is built out of plates and wedges of dozens;
        /// here a block is four balls and there are five colours to draw from, so the useful ball arrives a
        /// fifth of the time and clears four when it does.
        /// </summary>
        private static Design Prism() => new()
        {
            File = "Eight.json",
            Name = "Prism",
            Grid = 15,
            Depth = 6,
            Scene = new SeaSceneConfig(),
            Sky = 13,
            Shots = 60,
            CeilingStep = 6,
            //Stepped, so the silhouette is not another cylinder and the lower steps are reachable early
            Occupied = (r, ang, i, depth) => r <= 5.5f - (depth - 1 - i) * 0.55f,
            //Blocks TWO levels tall (x / 2, z / 2, i / 2), where this shipped with one. One level made the
            //group four balls, and against five colours that is a shot for every four — the widest step is
            //the last thing left standing and it took two dozen shots on its own, which is the "the last
            //storey drags" this was reported as. Two levels doubles every group to eight without touching
            //the shape or the palette, which are the parts worth keeping.
            BlockColour = (x, z, i) => Scatter(x / 2, z / 2, i / 2,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type7 }),
        };

        /// <summary>
        /// The hardest of the generated set and the one that stands in front of Two: a full cylinder, blocks
        /// of four, and <b>six</b> colours scattered so no two neighbouring blocks agree by design. There is
        /// no plate to trigger anywhere on it — every shot is a shot at four balls, and the ceiling steps
        /// every five while you take them.
        /// </summary>
        private static Design Static() => new()
        {
            File = "Nine.json",
            Name = "Static",
            Grid = 15,
            //Four deep and not six. The difficulty here is the six colours and the four-ball group, not the
            //tonnage: six deep came out at 555 balls, half again as many as Two, and the pack has a weak
            //laptop to run on. Four keeps it in Two's family at ~370 while every shot still costs the same.
            Depth = 4,
            Scene = new SpaceSceneConfig(),
            Sky = 1,
            Shots = 60,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) => r <= 5.5f,
            BlockColour = (x, z, i) => Scatter(x / 2, z / 2, i,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        /// <summary>
        /// The first <b>tall</b> level: a column reaching up out of shot, played from the bottom as the glass
        /// brings it down. The camera frames only the lowest <c>FRAMED_LEVELS</c> of a field this deep (see
        /// <c>GameplayScreen.FRAMED_LEVELS</c>), so the level's length is its height rather than its
        /// footprint, and the player never sees the top of what they are working through.
        /// <para>
        /// <b>Narrow on purpose.</b> Nine wide against the pack's thirteen: the column is four times the
        /// depth, so at thirteen it would be well over a thousand constrained bodies, and a tall level is
        /// meant to last through its height and not through its mass. Nine also keeps the whole visible face
        /// within an easy traverse, which matters when the interesting cells are all at the bottom.
        /// </para>
        /// <para>
        /// The descent is the ordinary <c>ceilingStep</c> and no new mechanic — but it is doing a second job
        /// here. On every other level it is the pressure; on this one it is also how the level is <i>fed</i>,
        /// so it is set fast (every 3) against a large budget. That coupling is the thing to watch when this
        /// is tuned: a step too slow leaves the player with nothing in reach, and too fast is a level that
        /// arrives at the death line with most of its column still overhead.
        /// </para>
        /// </summary>
        private static Design Column() => new()
        {
            File = "Ten.json",
            Name = "Column",
            Grid = 11,
            //The whole point. FIELD_LEVELS is 16 everywhere else; this field is 34 deep, of which 24 carry
            //balls — a layout half again as tall as an ordinary level's entire field, and the camera frames
            //16 of it. The ten empty levels under it are the usual growth room.
            Depth = 24,
            FieldLevels = 34,
            Scene = new MountainSceneConfig(),
            Sky = 1,
            Shots = 90,
            CeilingStep = 5,
            //Five cells across, against the pack's eleven. Twenty-four levels of a thirteen-wide disc would
            //be well over a thousand constrained bodies; this is ~500, in Two's family, and a tall level is
            //meant to last through its height rather than its mass.
            Occupied = (r, ang, i, depth) => r <= 2.6f,
            //Bands two levels thick around four colours: reading the column is reading what is coming, and a
            //band is a group of ~50, so the descent keeps handing the player something worth hitting
            Colour = (r, ang, i, depth) => Band(i / 2,
                new[] { BallType.Type1, BallType.Type5, BallType.Type7, BallType.Type3 }),
        };

        /// <summary>
        /// A sphere hanging whole from the glass - the roundest shape the lattice can make - coloured like a
        /// halved onion: a yellow skin around a white bulk around a small green heart, peeled from the
        /// outside in as the player clears it.
        /// <para>
        /// <see cref="SphereDistance"/> is true 3D distance from the sphere's own centre, with the level
        /// index scaled by <c>1/sqrt(2)</c> to match <c>BallsMap.GetRealPosition</c>'s vertical spacing - a
        /// radius built from <c>i</c> and <c>r</c> untouched comes out an egg, stretched along Y, because a
        /// level is not one lattice unit tall. <see cref="Depth"/> and the field's own centre are chosen so
        /// the sphere's north and south poles land exactly on the layout's top and bottom levels.
        /// </para>
        /// <para>
        /// The skin cannot wrap the whole sphere: the top layer is the one bonded to the glass, and a top
        /// layer of one colour anchors everything under it to that colour's single group (the rule every
        /// design here answers - see <see cref="Validate"/>). A true 3D shell also narrows to a single point
        /// at each pole, so whichever colour that point falls in becomes the entire cap. <see cref="OnionShell"/>
        /// answers both at once: it rings each level by its <b>own</b> radius rather than by distance from
        /// the sphere's centre, so every level - however small its own cap is - shows the same green-centre,
        /// white-ring, yellow-rim proportions the equator does. That is also just what a real onion's rings
        /// look like at any height: narrower near the root and stem, never absent.
        /// </para>
        /// </summary>
        private static Design Onion() => new()
        {
            File = "Eleven.json",
            Name = "Onion",
            Grid = 15,
            //One short of the true round number (see SphereDistance's own remarks): a mathematically exact
            //sphere at this radius wants Depth 16, whose top and bottom layers are then single points too
            //narrow to reliably carry all three colours (the annulus a lattice this coarse needs to land a
            //ball in it). Fifteen trims a hair off each pole instead - a slightly flattened onion, which is
            //closer to a real one's shape than a mathematical sphere is anyway.
            Depth = 15,
            //At the generous end of the pack's usual growth room (12, matching Bullseye/Pinwheel/Static):
            //the sphere's own bottom pole already reaches the layout's own floor, unlike every stepped-cone
            //design here, which tapers to its point several levels above the layout ends - so this design
            //has no such margin built in above the field's own growth room and wants the full amount of it.
            FieldLevels = 27,
            Scene = new ForestSceneConfig(),
            Sky = 3,
            Shots = 48,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => SphereDistance(r, i, depth) <= ONION_RADIUS,
            Colour = (r, ang, i, depth) => OnionShell(r, ang, i, depth),
        };

        #endregion

        #region Colour helpers

        //Concentric shells one and a bit cells thick: thick enough that a ring is a solid band of colour
        //rather than a dotted circle once the lattice has rounded it off.
        private static BallType Ring(float r, BallType[] palette) =>
            palette[(int)MathF.Floor(r / 1.9f) % palette.Length];

        private static BallType Band(int band, BallType[] palette) => palette[band % palette.Length];

        /// <summary>
        /// The square (Chebyshev) half-extent of a point the emitter hands over in polar form — the distance
        /// a <b>square</b> shape measures in, as opposed to <see cref="Ring"/>'s round one. Recovered from
        /// the pair rather than passed as a third argument: every design but the slab wants the round radius,
        /// and a shape function taking both would have to ignore one of them at every call site.
        /// </summary>
        private static float Chebyshev(float r, float ang) =>
            r * MathF.Max(MathF.Abs(MathF.Cos(ang)), MathF.Abs(MathF.Sin(ang)));

        /// <summary>
        /// A colour per block that looks unpatterned but is a pure function of the block's coordinates, so a
        /// level is the same every time it is played. An integer hash rather than a <see cref="Random"/>
        /// walked in loop order: the walk's order is an implementation detail of the emitter, and a layout
        /// that changes when that loop is reordered is a layout nobody can reason about.
        /// <para>
        /// Deliberately NOT anti-clustered. Two neighbouring blocks that happen to agree merge into a group
        /// of eight, and those accidents are the level's only breathing room — a scatter forced to alternate
        /// would be uniformly four everywhere, which is a grind rather than a difficulty.
        /// </para>
        /// </summary>
        private static BallType Scatter(int blockX, int blockZ, int level, BallType[] palette)
        {
            //Odd multipliers well apart in magnitude, then a couple of xorshift rounds: enough mixing that
            //neighbouring blocks land on unrelated colours, and cheap enough not to matter at generation time
            uint h = (uint)(blockX * 73856093 ^ blockZ * 19349663 ^ level * 83492791);
            h ^= h >> 13;
            h *= 2654435761;
            h ^= h >> 16;

            return palette[h % (uint)palette.Length];
        }

        //Angular wedges. twist shears the boundary with radius, which is what turns a cross into a spiral.
        private static BallType Sector(float ang, float twist, int sectors, BallType[] palette)
        {
            float turns = (ang / MathF.Tau) + 0.5f + twist;      //0..1 around the disc, plus the shear
            int index = (int)MathF.Floor(turns * sectors);
            index = ((index % sectors) + sectors) % sectors;     //MathF.Floor of a negative turns
            return palette[index % palette.Length];
        }

        //The onion's own geometry. See Onion() for why the two distances differ.
        private const float ONION_RADIUS = 5.5f;
        private const float INV_SQRT_TWO = 0.70710678f;

        //Where the two colour boundaries sit, as a share of the level's own radius, and how far the outer one
        //swings with the angle around the axis. See OnionShell for why the swing exists and what it has to
        //cross at both ends to work.
        private const float ONION_HEART = 0.28f;
        private const float ONION_BULK = 0.60f;
        private const float ONION_STAVES = 6f;
        private const float ONION_SWING = 0.50f;

        //BOTH boundaries swing, and the inner one has to go negative at the trough or the white simply wraps
        //around the heart and is one piece again: an angular gap a couple of cells wide out at the rim is
        //no gap at all by the time it has narrowed to the axis, so a rib that stops at the heart's edge
        //never actually separates anything. Measured: swinging the outer boundary alone cut the skin from
        //604 to 158 and left the white one group of 408 (42 % of the cluster).
        private const float ONION_SWING_HEART = 0.42f;

        //A level index's vertical world offset from the layout's own centre, in the same units r is
        //already in (BallsMap.GetRealPosition puts a level at Y = level / sqrt(2)).
        private static float OnionVertical(int i, int depth) => (i - (depth - 1) * HALF) * INV_SQRT_TWO;

        private static float SphereDistance(float r, int i, int depth)
        {
            float dy = OnionVertical(i, depth);
            return MathF.Sqrt(r * r + dy * dy);
        }

        /// <summary>
        /// Green heart, white bulk, yellow skin - ringed by <b>each level's own radius</b> rather than by
        /// true 3D distance from the sphere's centre. The two agree at the equator, where a level's own
        /// radius already is the sphere's, and it is everywhere else that the difference matters: a true
        /// 3D shell narrows to nothing at the poles, so whichever ring the pole's own tiny point happens to
        /// fall in becomes the ENTIRE top layer - the one bonded to the glass - and a single-colour anchor
        /// is the trap every design in this pack has to answer (see <see cref="Validate"/>). Ringed by its
        /// own radius instead, every level, however small, shows the same green-centre/white-ring/yellow-rim
        /// proportions the equator does, which is also just what a real onion's rings look like from any
        /// height - narrower near the root and stem, never absent. It is what keeps the heart's own colour
        /// standing in one piece straight up the middle from pole to pole, directly bonded to the glass at
        /// the top without ever having to pass through the white around it - and the same for the skin at
        /// its own rim - so peeling any one layer off never stands the other two on nothing.
        /// <para>
        /// <b>The boundary between the outer two SWINGS with the angle around the axis, and that is what makes
        /// this a level rather than one shot.</b> Ringed by radius alone each layer is a single connected
        /// piece from pole to pole - the skin measured 604 balls in one group, 62 % of the cluster, so the
        /// first lucky yellow ball ended it. <b>Played, three runs each, 24 shots a run with the aim spread
        /// between shots: ringed by radius alone the level was cleared every run, in 2, 4 and 8 shots of 48;
        /// with the swing, two of the three runs were still going at 24.</b> (A first attempt at that
        /// measurement spread the aim so wide that every shot missed the field outright - 24 shots, zero
        /// contacts of any kind - which any level survives. A scripted play-through is only a datum once the
        /// shots are confirmed to be landing.)
        /// <see cref="ONION_SWING"/> carries the boundary past the surface at <see cref="ONION_STAVES"/>
        /// angles and back inside the heart's own radius between them, so the two outer layers interlock as
        /// <i>staves</i>: white reaches daylight where the boundary swings out, cutting the skin there, and
        /// yellow reaches the core where it swings in, cutting the white. Each stave still runs the full
        /// height, so each is bonded to the glass on its own and the anchor rule above is untouched - and a
        /// bulb of interlocking segments is what an onion actually looks like cut across.
        /// </para>
        /// </summary>
        private static BallType OnionShell(float r, float ang, int i, int depth)
        {
            float dy = OnionVertical(i, depth);
            float capRadius = MathF.Sqrt(MathF.Max(0f, ONION_RADIUS * ONION_RADIUS - dy * dy));
            float shell = capRadius > 0f ? r / capRadius * ONION_RADIUS : 0f;

            //One swing drives both boundaries. Past 1 the white breaks the surface and cuts the skin; below 0
            //BOTH boundaries vanish and the yellow runs from rim to axis, which is the only thing that cuts
            //the white - and the heart with it, into wedges that each still stand the full height.
            float swing = MathF.Cos(ONION_STAVES * ang);
            float heart = ONION_HEART + ONION_SWING_HEART * swing;
            float bulk = ONION_BULK + ONION_SWING * swing;

            if (shell <= ONION_RADIUS * heart) return BallType.Type2; //green heart
            if (shell <= ONION_RADIUS * bulk) return BallType.Type4;  //white bulk
            return BallType.Type7;                                    //yellow skin
        }

        #endregion

        #region Emitting one design

        /// <returns>Whether the level that came out passed every check.</returns>
        private static bool Emit(Design design)
        {
            byte n = design.Grid;
            byte depth = design.Depth;
            byte fieldLevels = design.FieldLevels;
            byte offset = (byte)(fieldLevels - depth);

            if (offset % 2 != 0)
                throw new InvalidOperationException(
                    $"{design.File}: field {fieldLevels} less layout {depth} is an odd offset; the loader would " +
                    "extend the field by one level to keep the level parity and the design would not sit where it was drawn");

            //One world axis for every layer. The shifted (odd) levels put their cells on it exactly; the
            //unshifted ones sit half a cell off it, which is the lattice's own close packing and not an error.
            float axis = (n - 1) * 0.5f + 0.5f;

            BallPositionType[,,] balls = new BallPositionType[n, n, depth];

            for (byte i = 0; i < depth; i++)
            {
                byte fieldLevel = (byte)(i + offset);
                float shift = (fieldLevel % 2) > 0 ? 0.5f : 0f;

                for (byte x = 0; x < n; x++)
                    for (byte z = 0; z < n; z++)
                    {
                        float dx = x + shift - axis;
                        float dz = z + shift - axis;
                        float r = MathF.Sqrt(dx * dx + dz * dz);
                        float manhattan = MathF.Abs(dx) + MathF.Abs(dz);
                        float ang = MathF.Atan2(dz, dx);

                        bool occupied = design.OccupiedManhattan != null
                            ? design.OccupiedManhattan(manhattan, i, depth)
                            : design.Occupied(r, ang, i, depth);

                        if (!occupied) continue;

                        BallType type =
                            design.Colour != null ? design.Colour(r, ang, i, depth)
                            : design.ColourManhattan != null ? design.ColourManhattan(manhattan, i, depth)
                            : design.BlockColour(x, z, i);

                        //The position the ball will actually occupy in the raw grid frame, so the stored
                        //one agrees with what PutBallAt recomputes at load rather than merely being ignored
                        Vector3 position = BallsMap.GetRealPosition(x, z, fieldLevel);

                        balls[x, z, i] = new BallPositionType
                        {
                            PositionX = position.X,
                            PositionY = position.Y,
                            PositionZ = position.Z,
                            Type = type,
                        };
                    }
            }

            int repaired = RepairLonelyBalls(balls, n, depth, offset, fieldLevels);

            Level level = new()
            {
                Name = design.Name,
                Author = "BS3D",
                SkyDome = design.Sky,
                Scene = design.Scene,
                Map = new BallPositionTypes { StageSizeX = n, StageSizeZ = n, Levels = fieldLevels, Balls = balls },
            };

            string path = Path.Combine(_outDir, design.File);
            level.Save(path);

            return Validate(design, path, repaired);
        }

        /// <summary>
        /// Recolours every ball whose own colour group is smaller than <see cref="MIN_GROUP"/> to whichever
        /// neighbouring colour puts it in the largest one — the safety net under
        /// <see cref="FindLonelyBalls"/>, so a design cannot ship a ball that needs two shots.
        /// <para>
        /// A shape drawn as a formula meets a lattice that rounds it off, and the rounding leaves slivers: a
        /// block clipped by the rim of a disc, a ring one cell wide where the curve happens to fall between
        /// two rows. Those are a handful of balls out of hundreds and are invisible in the pattern, which is
        /// exactly why they are worth fixing here rather than by bending the formula until they go away.
        /// A whole rim of them is a <b>design</b> fault and belongs in the design — see <see cref="Gem"/>.
        /// </para>
        /// </summary>
        /// <returns>How many balls were recoloured, which is the number that says whether a design is being
        /// rounded off at its edges or quietly rewritten.</returns>
        private static int RepairLonelyBalls(BallPositionType[,,] balls, byte n, byte depth, byte offset, byte fieldLevels)
        {
            //Repaired on a map rather than on the array: the neighbour rule and the parity that drives it are
            //BallsMap's, and a second copy of them here is a second place for them to be wrong
            BallsMap map = new(new BallPositionTypes { StageSizeX = n, StageSizeZ = n, Levels = fieldLevels, Balls = balls });
            StaticBall[,,] array = map.GetStaticBallsArray();
            XZLevel size = new(map.StageSizeX, map.StageSizeZ, map.Levels);

            int repaired = 0;

            //Recolouring one ball can rescue its neighbour, so this runs until it stops changing anything.
            //Bounded because a pathological design could otherwise cycle two cells against each other.
            for (int pass = 0; pass < 8; pass++)
            {
                int changed = 0;

                for (byte l = 0; l < map.Levels; l++)
                    for (byte x = 0; x < map.StageSizeX; x++)
                        for (byte z = 0; z < map.StageSizeZ; z++)
                        {
                            if (array[x, z, l] == null) continue;

                            XZLevel cell = new(x, z, l);
                            if (map.GetConnectedSameTypeCells(cell).Count >= MIN_GROUP) continue;

                            BallType best = array[x, z, l].Type;
                            int bestGroup = 0;

                            //Every colour standing next to it is a candidate; the one that leaves it in the
                            //biggest group wins. Measured by actually recolouring and asking, because the
                            //answer depends on what those neighbours are themselves connected to.
                            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                            {
                                StaticBall other = array[neighbour.X, neighbour.Z, neighbour.Level];
                                if (other == null || other.Type == best) continue;

                                map.PutBallAt(x, z, l, other.Type);
                                int group = map.GetConnectedSameTypeCells(cell).Count;

                                if (group > bestGroup) { bestGroup = group; best = other.Type; }
                            }

                            map.PutBallAt(x, z, l, best);
                            if (bestGroup > 0) { changed++; repaired++; }
                        }

                if (changed == 0) break;
            }

            //Back into the layout array the level file is written from
            for (byte i = 0; i < depth; i++)
                for (byte x = 0; x < n; x++)
                    for (byte z = 0; z < n; z++)
                        if (balls[x, z, i] != null)
                            balls[x, z, i].Type = array[x, z, i + offset].Type;

            return repaired;
        }

        /// <summary>
        /// Reads the file back the way the game does and reports what it actually got. A design is only
        /// worth shipping if the loader agrees with it, every ball hangs off the glass, and every colour
        /// has somewhere to be matched.
        /// </summary>
        /// <returns>
        /// Whether the level passes all three: nothing floating free of the glass, no ball standing alone,
        /// and no colour whose best single shot is the whole cluster.
        /// </returns>
        private static bool Validate(Design design, string path, int repaired)
        {
            Level loaded = Level.Load(path);
            BallsMap map = new(loaded.Map);
            map.Center();

            int disconnected = map.GetCellsDisconnectedFromCeiling().Count;

            StaticBall[,,] array = map.GetStaticBallsArray();
            Dictionary<BallType, int> counts = new();
            Dictionary<BallType, int> largestGroup = new();

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        StaticBall ball = array[x, z, l];
                        if (ball == null) continue;

                        counts.TryGetValue(ball.Type, out int c);
                        counts[ball.Type] = c + 1;

                        int group = map.GetConnectedSameTypeCells(new XZLevel(x, z, l)).Count;
                        largestGroup.TryGetValue(ball.Type, out int g);
                        if (group > g) largestGroup[ball.Type] = group;
                    }

            long fileSize = new FileInfo(path).Length;

            Console.WriteLine($"--- {design.File} '{loaded.Name}' ({loaded.Scene.Kind}, sky {loaded.SkyDome}) {fileSize / 1024} kB");
            Console.WriteLine($"    field {map.StageSizeX}x{map.StageSizeZ}x{map.Levels}, layout {design.Depth} deep, "
                              + $"{map.GetBallsCount()} balls, lowest occupied level {map.GetLowestOccupiedLevel()}");
            Console.WriteLine($"    hanging off the glass: {(disconnected == 0 ? "all" : $"NO - {disconnected} balls float free")}");

            int total = map.GetBallsCount();

            int margin = LateralMargin(map);
            Console.WriteLine($"    lateral margin: {(margin >= 1 ? $"{margin} free cell(s) all round" : "NONE - the layout is ON the field wall")}");

            LonelyReport lonely = FindLonelyBalls(map);
            Console.WriteLine($"    reachable in one ball: {(lonely.Alone == 0 ? "all" : $"NO - {lonely.Alone} STAND ALONE")}"
                              + $" (in pairs {lonely.Paired}, primed {total - lonely.Alone - lonely.Paired})"
                              + $", {repaired} recoloured by the repair pass");
            foreach (string where in lonely.Examples) Console.WriteLine($"      {where}");

            bool oneShot = false;

            foreach (var pair in counts.OrderBy(p => p.Key))
            {
                int dropped = DropTest(loaded.Map, pair.Key);
                int percent = total == 0 ? 0 : dropped * 100 / total;

                if (percent >= ONE_SHOT_PERCENT) oneShot = true;

                Console.WriteLine($"    {pair.Key,-6} {pair.Value,4} balls, largest standing group {largestGroup[pair.Key],4}"
                                  + (largestGroup[pair.Key] >= 3 ? "  primed" : "  <-- NOT PRIMED")
                                  + $", best single shot drops {dropped,4} ({percent,3}%)"
                                  + (percent >= ONE_SHOT_PERCENT ? "  <-- ONE-SHOT LEVEL" : string.Empty));
            }

            return disconnected == 0 && lonely.Alone == 0 && !oneShot && margin >= 1;
        }

        /// <summary>
        /// How many empty columns of field the layout leaves on its tightest side — <b>the room a shot has to
        /// land in when it arrives at the cluster's flank</b>, and the check that was missing when this pack
        /// was written.
        /// <para>
        /// The field is a box. A ball on the cluster's side face that is also on the field's <i>wall</i> has
        /// no lateral neighbours at all: if the cells under it are taken, a shot into that pocket finds
        /// nothing in either ring, does not stick, bounces off and costs a ball and the streak. It is a
        /// documented trap ("The landing preview, and why the field's edge needed one" in
        /// <c>docs/game-session.md</c>) and every disc here walked straight into it — a radius of 5.5 in a
        /// 13-wide field reaches the wall on the unshifted levels, and the Gem's taxicab rim reached it on
        /// all four sides. It was reported from play as "the ball bounced instead of sticking", on Pinwheel
        /// and on Static, and reproduced at one refusal in 34 varied-angle shots.
        /// </para>
        /// <para>
        /// One free column is enough: it gives every flank ball a lateral neighbour to offer. It is bought by
        /// widening the FIELD rather than by shrinking the shape — the shapes are what the level looks like
        /// and they were kept deliberately — which costs a slightly wider glass plate and a slightly longer
        /// camera stand-off, and nothing else.
        /// </para>
        /// </summary>
        private static int LateralMargin(BallsMap map)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        if (array[x, z, l] == null) continue;

                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (z < minZ) minZ = z;
                        if (z > maxZ) maxZ = z;
                    }

            if (minX == int.MaxValue) return int.MaxValue; //an empty layout is all margin

            return Math.Min(
                Math.Min(minX, map.StageSizeX - 1 - maxX),
                Math.Min(minZ, map.StageSizeZ - 1 - maxZ));
        }

        /// <summary>
        /// Balls whose own colour group is too small to be finished with one shot. <b>A ball standing alone
        /// needs two landed balls</b> to make the minimum three, and that is not a puzzle, it is a chore — the
        /// player has to hit the same isolated cell twice with the right colour before anything happens.
        /// <para>
        /// It comes from the lattice, not from carelessness: cells on one level touch only their four
        /// <b>orthogonal</b> neighbours, never their diagonal ones. So any colour band one cell thick that
        /// runs diagonally is a string of balls that do not touch each other at all. A taxicab ring of odd
        /// width is exactly that, and the first Gem had 28 of them around the rim of its top layer, which is
        /// the layer against the glass and therefore has no level above to connect through either.
        /// </para>
        /// </summary>
        private static LonelyReport FindLonelyBalls(BallsMap map)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            LonelyReport report = new();

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        if (array[x, z, l] == null) continue;

                        int group = map.GetConnectedSameTypeCells(new XZLevel(x, z, l)).Count;
                        if (group >= 3) continue;

                        if (group == 1) report.Alone++; else report.Paired++;

                        if (report.Examples.Count < 3)
                            report.Examples.Add($"group of {group}: {array[x, z, l].Type} at cell ({x},{z}) on level {l}");
                    }

            return report;
        }

        private sealed class LonelyReport
        {
            public int Alone;
            public int Paired;
            public readonly List<string> Examples = new();
        }

        /// <summary>
        /// How many balls the best single shot of one colour would bring down: its largest standing group,
        /// plus everything that group was the last anchor for. This is the number that decides whether a
        /// design is a level or a firework — a colour that drops the whole cluster means the anchor layer
        /// is one colour, and the level is over on the first lucky ball.
        /// </summary>
        /// <remarks>
        /// Run on a map rebuilt from the same data rather than on the caller's, because it destroys the one
        /// it measures. <see cref="BallsMap"/> has no clone and does not need one for this.
        /// </remarks>
        private static int DropTest(BallPositionTypes data, BallType type)
        {
            BallsMap map = new(data);
            StaticBall[,,] array = map.GetStaticBallsArray();

            List<XZLevel> largest = new();

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        if (array[x, z, l]?.Type != type) continue;

                        List<XZLevel> group = map.GetConnectedSameTypeCells(new XZLevel(x, z, l));
                        if (group.Count > largest.Count) largest = group;
                    }

            foreach (XZLevel cell in largest) map.RemoveBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

            return largest.Count + map.GetCellsDisconnectedFromCeiling().Count;
        }

        #endregion

        private sealed class Design
        {
            public string File;
            public string Name;
            public byte Grid;
            public byte Depth;

            /// <summary>
            /// How deep the play field is. <see cref="FIELD_LEVELS"/> for every ordinary level — the deepest
            /// field the game hangs at its standard height and frames whole — and larger only for a tall one,
            /// which is framed from its floor up and reaches out of shot.
            /// </summary>
            public byte FieldLevels = FIELD_LEVELS;
            public SceneConfig Scene;
            public byte Sky;
            public int Shots;
            public int CeilingStep;

            /// <summary>Round radius, angle, layout level, layout depth -> is there a ball here.</summary>
            public Func<float, float, int, int, bool> Occupied;

            /// <summary>Taxicab radius instead, for the designs whose cross-section is a diamond.</summary>
            public Func<float, int, int, bool> OccupiedManhattan;

            //Exactly one of the three is set. They differ in what the pattern is a function of — the
            //centred polar frame, the centred taxicab one, or the raw lattice indices — and a design that
            //had to take all three would have to ignore two of them at every call site.
            public Func<float, float, int, int, BallType> Colour;
            public Func<float, int, int, BallType> ColourManhattan;
            public Func<int, int, int, BallType> BlockColour;
        }
    }
}
