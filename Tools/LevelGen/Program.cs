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

            Design[] designs =
            {
                Bullseye(), Mosaic(), Pinwheel(), Crown(), Gem()
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
        /// Rewrites the set that orders the levels. The two hand-drawn levels that came before this pack
        /// keep their own rules verbatim — they are authored content and this generator has no opinion
        /// about them; it only appends the pattern levels after them.
        /// </summary>
        private static void WriteLevelSet(Design[] designs)
        {
            LevelSet set = new()
            {
                Name = "Bubble Shooter 3D",
                Levels = new List<LevelSetEntry>
                {
                    new() { File = "One.json", Name = "One", Shots = 30, CeilingStep = 5 },
                    new() { File = "Two.json", Name = "Two", Shots = 45, CeilingStep = 4 },
                },
            };

            foreach (Design d in designs)
                set.Levels.Add(new LevelSetEntry
                {
                    File = d.File,
                    Name = d.Name,
                    Shots = d.Shots,
                    CeilingStep = d.CeilingStep,
                });

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
        /// A stepped cone hanging point-down, coloured in concentric rings: a target seen from underneath,
        /// and a flight of coloured steps seen from the side. Three colours, each ring one solid shell, so
        /// every ring is already a group of dozens waiting for one matching ball to touch it.
        /// </summary>
        private static Design Bullseye() => new()
        {
            File = "Three.json",
            Name = "Bullseye",
            Grid = 13,
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
            Grid = 11,
            Depth = 6,
            Scene = new SeaSceneConfig(),
            //The sea mirrors the dome, so a bright one gives it a flat sandy horizon rather than water —
            //measured, under dome 1 the whole sea read as tan. 13 is the violet/teal dusk the Testbed
            //already defaults the sea to (SEA_DEFAULT_SKY_DOME), and the mosaic's CMY pops against it.
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
            Grid = 13,
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
            Grid = 13,
            Depth = 6,
            Scene = new MountainSceneConfig(),
            Sky = 10,
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
            Grid = 13,
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

        #endregion

        #region Colour helpers

        //Concentric shells one and a bit cells thick: thick enough that a ring is a solid band of colour
        //rather than a dotted circle once the lattice has rounded it off.
        private static BallType Ring(float r, BallType[] palette) =>
            palette[(int)MathF.Floor(r / 1.9f) % palette.Length];

        private static BallType Band(int band, BallType[] palette) => palette[band % palette.Length];

        //Angular wedges. twist shears the boundary with radius, which is what turns a cross into a spiral.
        private static BallType Sector(float ang, float twist, int sectors, BallType[] palette)
        {
            float turns = (ang / MathF.Tau) + 0.5f + twist;      //0..1 around the disc, plus the shear
            int index = (int)MathF.Floor(turns * sectors);
            index = ((index % sectors) + sectors) % sectors;     //MathF.Floor of a negative turns
            return palette[index % palette.Length];
        }

        #endregion

        #region Emitting one design

        /// <returns>Whether the level that came out passed every check.</returns>
        private static bool Emit(Design design)
        {
            byte n = design.Grid;
            byte depth = design.Depth;
            byte offset = (byte)(FIELD_LEVELS - depth);

            if (offset % 2 != 0)
                throw new InvalidOperationException(
                    $"{design.File}: field {FIELD_LEVELS} less layout {depth} is an odd offset; the loader would " +
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

            int repaired = RepairLonelyBalls(balls, n, depth, offset);

            Level level = new()
            {
                Name = design.Name,
                Author = "BS3D",
                SkyDome = design.Sky,
                Scene = design.Scene,
                Map = new BallPositionTypes { StageSizeX = n, StageSizeZ = n, Levels = FIELD_LEVELS, Balls = balls },
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
        private static int RepairLonelyBalls(BallPositionType[,,] balls, byte n, byte depth, byte offset)
        {
            //Repaired on a map rather than on the array: the neighbour rule and the parity that drives it are
            //BallsMap's, and a second copy of them here is a second place for them to be wrong
            BallsMap map = new(new BallPositionTypes { StageSizeX = n, StageSizeZ = n, Levels = FIELD_LEVELS, Balls = balls });
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

            return disconnected == 0 && lonely.Alone == 0 && !oneShot;
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
