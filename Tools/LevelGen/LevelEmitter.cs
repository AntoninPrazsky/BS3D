using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// Turns one <see cref="Design"/> into a level file: the walk over the lattice that asks the design what stands
    /// in every cell and what colour and kind it is, the skin (<see cref="HollowOut"/>), the repair pass under the
    /// lonely-ball gate (<see cref="RepairLonelyBalls"/>) and the write — and then hands the file to
    /// <see cref="LevelGates.Validate"/>. Split out of <c>Program.cs</c> in #597.
    /// </summary>
    internal static class LevelEmitter
    {
        /// <summary>
        /// WHAT COLOUR A ROCK IS WRITTEN AS, forced by <see cref="Emit"/> over whatever the design's own
        /// colour rule answered. Nothing in the game reads it — <c>GranitePS</c> ignores
        /// <c>PatternPrimaryColor</c> entirely and is the one shading whose colour is a constant, and
        /// <c>RecountBallTypes</c> counts only the matchable balls, so a rock never puts a colour in the
        /// magazine either. But <see cref="BallPositionType.Type"/> is not nullable and every cell carries
        /// one, so the value is a decision about what a READER of the file sees.
        /// <para>
        /// Slate, because it is the nearest of the thirteen to what the ball is actually drawn as, so a map
        /// opened in the editor or read as JSON says roughly the truth rather than something arbitrary — and
        /// <b>no rock level plays slate</b> (nor black, its neighbour; see the five Mirage palettes). A rock
        /// wearing a colour the level is matching invites the one misreading the granite technique's header
        /// exists to prevent, which is a player aiming that colour at it.
        /// </para>
        /// <para>
        /// <b>⚠ It is forced in the emitter rather than asked of each design, and that is the fix for how it
        /// first shipped.</b> The constant was written, documented and then simply never called: every rock
        /// in the block came out wearing whatever its design's own <c>BlockColour</c> had answered for that
        /// cell, which on Obsidian was one of the five colours the level plays. Nothing failed — the census
        /// skips rocks, so no gate could see it — and the file said the opposite of what this comment
        /// claimed. A rule about what a rock IS belongs in the one place every rock passes through.
        /// </para>
        /// </summary>
        private const BallType ROCK_TINT = BallType.Type11;

        /// <summary>Where the levels are written. Set once in <see cref="Program.Main"/>, read by everything below.</summary>
        internal static string OutDir;

        /// <summary>
        /// Takes the inside out of a body, leaving <paramref name="skin"/> cells of it standing against every
        /// free cell — <see cref="Design.Hollow"/>'s implementation, and the answer to #398.
        /// </summary>
        /// <remarks>
        /// A multi-source walk out of the free space rather than a test per cell: every empty cell of the
        /// layout box, and the box's own outside, is distance zero, and a ball is kept while it is within
        /// <paramref name="skin"/> steps of one. <b>The steps are the lattice's</b>
        /// (<see cref="BallsMap.FillNeighboringCells"/>), never a rule written here — a cross-level neighbour
        /// is a diagonal in (x, z) and the offsets depend on the level's parity, which is exactly the sort of
        /// thing a second copy gets wrong.
        /// <para>
        /// <b>⚠ A cell on the box's boundary is skin whatever stands around it</b>, which is what keeps the
        /// anchor course whole: the top course has no level above it, so its cells are one step from the
        /// outside and no depth of hollowing can take them. The same is true of the floor and the four walls,
        /// so a body that fills its box keeps its whole surface and loses only what is buried.
        /// </para>
        /// <para>
        /// It runs on the LAYOUT box and not on the field, and the two agree in parity because
        /// <see cref="Emit"/> refuses an odd offset — the same fact <c>Centred</c> leans on.
        /// </para>
        /// </remarks>
        /// <returns>How many balls it took out, for the emitter's own line.</returns>
        private static int HollowOut(BallPositionType[,,] balls, byte n, byte depth, int skin)
        {
            XZLevel size = new(n, n, depth);
            int[] distance = new int[n * n * depth];
            Queue<XZLevel> frontier = new();
            Span<XZLevel> neighbours = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            int Key(int x, int z, int i) => (i * n + x) * n + z;

            for (int i = 0; i < depth; i++)
                for (int x = 0; x < n; x++)
                    for (int z = 0; z < n; z++)
                    {
                        if (balls[x, z, i] == null)
                        {
                            distance[Key(x, z, i)] = 0;
                            frontier.Enqueue(new XZLevel(x, z, i));
                            continue;
                        }

                        distance[Key(x, z, i)] = int.MaxValue;

                        //The box's outside is free, so a ball that lost a neighbour to the clip is already
                        //skin - that is what holds the anchor course, the floor and the walls.
                        if (BallsMap.FillNeighboringCells(new XZLevel(x, z, i), size, neighbours)
                            < BallsMap.MAX_NEIGHBORS)
                        {
                            distance[Key(x, z, i)] = 1;
                            frontier.Enqueue(new XZLevel(x, z, i));
                        }
                    }

            while (frontier.Count > 0)
            {
                XZLevel cell = frontier.Dequeue();
                int next = distance[Key(cell.X, cell.Z, cell.Level)] + 1;
                if (next > skin) continue;

                int count = BallsMap.FillNeighboringCells(cell, size, neighbours);
                for (int k = 0; k < count; k++)
                {
                    XZLevel neighbour = neighbours[k];
                    int key = Key(neighbour.X, neighbour.Z, neighbour.Level);
                    if (balls[neighbour.X, neighbour.Z, neighbour.Level] == null || distance[key] <= next) continue;

                    distance[key] = next;
                    frontier.Enqueue(neighbour);
                }
            }

            int removed = 0;

            for (int i = 0; i < depth; i++)
                for (int x = 0; x < n; x++)
                    for (int z = 0; z < n; z++)
                    {
                        if (balls[x, z, i] == null || distance[Key(x, z, i)] <= skin) continue;

                        balls[x, z, i] = null;
                        removed++;
                    }

            return removed;
        }

        /// <returns>Whether the level that came out passed every check.</returns>
        internal static bool Emit(Design design)
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

                        bool occupied =
                            design.OccupiedBlock != null ? design.OccupiedBlock(x, z, i, depth)
                            : design.OccupiedManhattan != null ? design.OccupiedManhattan(manhattan, i, depth)
                            : design.Occupied(r, ang, i, depth);

                        if (!occupied) continue;

                        BallType type =
                            design.Colour != null ? design.Colour(r, ang, i, depth)
                            : design.ColourManhattan != null ? design.ColourManhattan(manhattan, i, depth)
                            : design.BlockColour(x, z, i);

                        //What the ball IS, beside what colour it is (#323/#325). Resolved AFTER the colour
                        //and never instead of it: the cell carries both, and a glass ball simply ignores the
                        //colour it was given - see Design.Kind.
                        BallKind kind =
                            design.Kind != null ? design.Kind(r, ang, i, depth)
                            : design.BlockKind != null ? design.BlockKind(x, z, i, depth)
                            : BallKind.Normal;

                        //A ROCK'S COLOUR IS NOT THE DESIGN'S TO CHOOSE, and forcing it here is the only place
                        //that can be true of every rock at once - see ROCK_TINT, and the way it first shipped.
                        if (kind == BallKind.Rock) type = ROCK_TINT;

                        //The position the ball will actually occupy in the raw grid frame, so the stored
                        //one agrees with what PutBallAt recomputes at load rather than merely being ignored
                        Vector3 position = BallsMap.GetRealPosition(x, z, fieldLevel);

                        balls[x, z, i] = new BallPositionType
                        {
                            PositionX = position.X,
                            PositionY = position.Y,
                            PositionZ = position.Z,
                            Type = type,
                            Kind = kind,
                        };
                    }
            }

            //THE SKIN (#398), before the repair pass rather than after it: hollowing can leave a ball
            //with one neighbour where it had six, and the repair is what looks at that.
            int hollowed = design.Hollow > 0 ? HollowOut(balls, n, depth, design.Hollow) : 0;

            int repaired = RepairLonelyBalls(balls, n, depth, offset, fieldLevels);

            Level level = new()
            {
                Name = design.Name,
                Author = "BS3D",
                SkyDome = design.Sky,
                Scene = design.Scene,
                Music = design.Music,
                Balls = design.Balls,
                Map = new BallPositionTypes { StageSizeX = n, StageSizeZ = n, Levels = fieldLevels, Balls = balls },
            };

            string path = Path.Combine(OutDir, design.File);
            level.Save(path);

            //What the skin took out, said where the level's own figures are said (#398).
            if (hollowed > 0)
                Console.WriteLine($"--- {design.File}: hollowed to a skin of {design.Hollow}, "
                                  + $"{hollowed} buried ball(s) taken out");

            return LevelGates.Validate(design, path, repaired);
        }

        /// <summary>
        /// Recolours every ball whose own colour group is smaller than <see cref="LevelGates.MIN_GROUP"/> to whichever
        /// neighbouring colour puts it in the largest one — the safety net under
        /// <see cref="LevelGates.FindLonelyBalls"/>, so a design cannot ship a ball that needs two shots.
        /// <para>
        /// A shape drawn as a formula meets a lattice that rounds it off, and the rounding leaves slivers: a
        /// block clipped by the rim of a disc, a ring one cell wide where the curve happens to fall between
        /// two rows. Those are a handful of balls out of hundreds and are invisible in the pattern, which is
        /// exactly why they are worth fixing here rather than by bending the formula until they go away.
        /// A whole rim of them is a <b>design</b> fault and belongs in the design — see <see cref="Program.Gem"/>.
        /// </para>
        /// <para>
        /// <b>⚠ It skips every ball that is not <see cref="BallKind.Normal"/>, and both halves of that matter
        /// (#323/#325).</b> A rock and a glass ball have no colour group by definition — <see cref="BallsMap.
        /// GetConnectedSameTypeCells"/> returns an EMPTY list for either, which is a group of 0 and reads here
        /// as the worst lonely ball in the level — so without the skip this pass would have tried to repair
        /// every special in the Mirage block. And the repair is <c>PutBallAt</c>, whose <c>kind</c> parameter
        /// <b>defaults to Normal</b>: the "repair" would have turned each of them into an ordinary coloured
        /// ball, silently, with the level file written from the result and nothing anywhere saying so. The
        /// neighbour scan skips them too, for the plainer reason that a colour a rock is carrying is not a
        /// colour anything can match.
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
                            if (!BallKinds.Matchable(array[x, z, l].Kind)) continue;

                            XZLevel cell = new(x, z, l);
                            if (map.GetConnectedSameTypeCells(cell).Count >= LevelGates.MIN_GROUP) continue;

                            BallType best = array[x, z, l].Type;
                            int bestGroup = 0;

                            //⚠ THE KIND HAS TO BE CARRIED THROUGH EVERY PutBallAt BELOW (#331), and this is
                            //exactly #325's recorded data loss arriving through the opposite door. That one
                            //was "a special is not matchable, so the repair must skip it"; this is "a special
                            //IS matchable" — an infectious ball is an ordinary ball of its colour that is
                            //sick, so it passes the guard above and belongs in the repair — and PutBallAt's
                            //`kind` parameter defaults to Normal, so every recolour here would silently CURE
                            //it, with the level file written from the result. The trial recolours below do it
                            //too: the ball must still be sick while the group is measured, or a sick ball's
                            //group is measured on a field the repair has already changed.
                            BallKind kind = array[x, z, l].Kind;

                            //Every colour standing next to it is a candidate; the one that leaves it in the
                            //biggest group wins. Measured by actually recolouring and asking, because the
                            //answer depends on what those neighbours are themselves connected to.
                            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                            {
                                StaticBall other = array[neighbour.X, neighbour.Z, neighbour.Level];
                                if (other == null || !BallKinds.Matchable(other.Kind) || other.Type == best) continue;

                                map.PutBallAt(x, z, l, other.Type, kind);
                                int group = map.GetConnectedSameTypeCells(cell).Count;

                                if (group > bestGroup) { bestGroup = group; best = other.Type; }
                            }

                            map.PutBallAt(x, z, l, best, kind);
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
    }
}
