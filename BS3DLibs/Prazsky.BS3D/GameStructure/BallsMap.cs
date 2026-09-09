using Microsoft.Xna.Framework;
using System.Text.Json;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;

namespace Prazsky.BS3D.GameStructure
{
    public class BallsMap
    {
        private StaticBall[,,] _balls;

        private static readonly float BALL_RADIUS = Constants.HALF;

        /// <summary>
        /// Empty levels added below the layout of legacy map files, which carried no play field size.
        /// Gives the structure room to grow downwards when shot balls attach.
        /// Must be an even number so the layout keeps its level parity (odd levels are shifted by +0.5 in X and Z);
        /// an odd offset would flip the parity of every layer and change how shaped layouts nest into each other.
        /// </summary>
        private static readonly byte DEFAULT_EXTRA_LEVELS = 6;

        public byte StageSizeX { get; internal set; }
        public byte StageSizeZ { get; internal set; }
        public byte Levels { get; internal set; }
        public bool Centered { get; internal set; } = false;
        public Vector2 BoundingBoxCenter { get; internal set; }


        public BallsMap(byte stageSizeX, byte stageSizeZ, byte levels)
        {
            if (stageSizeX < 2 || stageSizeZ < 2 || levels < 2) throw new ArgumentException($"Minimum BallsMap size is 2×2×2, given arguments: {stageSizeX}, {stageSizeZ}, {levels}");

            StageSizeX = stageSizeX;
            StageSizeZ = stageSizeZ;
            Levels = levels;

            _balls = new StaticBall[StageSizeX, StageSizeZ, Levels];
        }

        public BallsMap(string fileNameForDeserialization)
        {
            DeserializeJson(fileNameForDeserialization);
        }

        /// <summary>
        /// Builds the map from already-deserialized data — the path a level file takes (its map portion is
        /// deserialized by the level loader, not read from a separate file). Throws on invalid data so the
        /// caller can leave its current state untouched.
        /// </summary>
        public BallsMap(BallPositionTypes ballPositionTypes)
        {
            if (ballPositionTypes?.Balls == null) throw new ArgumentNullException(nameof(ballPositionTypes));

            ApplyBallPositionTypes(ballPositionTypes);
        }

        /// <summary>
        /// Creates new <see cref="StaticBall"/> of given type <see cref="BallType"/>, computes its real position and places it into internal three-dimensional array.
        /// If there is already a <see cref="StaticBall"/> at that position, it is replaced without warning.
        /// </summary>
        /// <param name="stageX">The X coordinate in the given level.</param>
        /// <param name="stageZ">The Z coordinate in the given level.</param>
        /// <param name="level">Level.</param>
        /// <param name="type">Ball type.</param>
        /// <param name="kind">What the ball is beside its colour (#323). Normal unless a caller says otherwise.</param>
        /// <returns>Created static ball.</returns>
        public StaticBall PutBallAt(byte stageX, byte stageZ, byte level, BallType type = BallType.Type4,
            BallKind kind = BallKind.Normal)
        {
            if (stageX >= StageSizeX || stageZ >= StageSizeZ || level >= Levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {StageSizeX} × {StageSizeZ} × {Levels}");

            Vector3 realPosition = GetRealPosition(stageX, stageZ, level);
            if (Centered) realPosition = ComputeCentered(realPosition);

            //A kind that cannot hang in a cluster becomes an ordinary ball here (#330), and this is the one
            //place it can be caught: every ball the map ever holds is placed through this method — the loader,
            //the editor and the landing alike. Today that is the wildcard, which collapses to a colour as it
            //lands and so should never arrive still wearing its kind; a hand-written "k": 5 in a file is the
            //case this actually stops, and it stops it silently rather than refusing the file, because the
            //result is a playable level either way. See BallKinds.InCluster for what a hanging wildcard would do
            //to the two end-of-level conditions.
            if (!BallKinds.InCluster(kind)) kind = BallKind.Normal;

            var ball = new StaticBall(realPosition, type, kind);
            _balls[stageX, stageZ, level] = ball;

            return ball;
        }

        public void RemoveBallAt(byte stageX, byte stageZ, byte level)
        {
            if (stageX >= StageSizeX || stageZ >= StageSizeZ || level >= Levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {StageSizeX} × {StageSizeZ} × {Levels}");

            _balls[stageX, stageZ, level] = null;
        }

        public static Vector3 GetRealPosition(byte stageX, byte stageZ, byte level)
        {
            bool isShifted = (level % 2) > 0;

            float realPosX = stageX;
            if (isShifted) realPosX += Constants.HALF;

            float realPosZ = stageZ;
            if (isShifted) realPosZ += Constants.HALF;

            float realPosY = level / Constants.SQRT_TWO;

            return new Vector3(realPosX, realPosY, realPosZ);
        }

        public Vector3 GetRealCenteredPosition(XZLevel arrayPosition)
        {
            var realPos = GetRealPosition((byte)arrayPosition.X, (byte)arrayPosition.Z, (byte)arrayPosition.Level);
            return ComputeCentered(realPos);
        }

        /// <summary>
        /// Which top-level cell a ball that reached the ceiling belongs in — the cell <paramref name="position"/>
        /// rounds to, when that cell is inside the field and free. Reads nothing but occupancy and writes nothing,
        /// so the same question can be asked before a shot is fired as after it lands (#70).
        /// </summary>
        /// <param name="position">Centred (lattice-frame) position, typically the contact point.</param>
        /// <param name="cell">The cell, <b>only meaningful when this returns <c>true</c></b>.</param>
        public bool TryFindEmptyCeilingCell(Vector3 position, out XZLevel cell)
        {
            byte level = (byte)(Levels - 1); //Balls hitting the ceiling always land on the top level
            bool isShifted = (level % 2) > 0;

            cell = new XZLevel(-1, -1, -1);

            Vector3 uncentered = ComputeUncentered(position);

            if (isShifted) uncentered = new Vector3(uncentered.X - Constants.HALF, uncentered.Y, uncentered.Z - Constants.HALF);

            //Convert.ToByte rounds to nearest, so [-0.5, byte.MaxValue + 0.5) is exactly the range that
            //maps to a valid byte without overflowing
            if (uncentered.X < -0.5f || uncentered.X >= 255.5f || uncentered.Z < -0.5f || uncentered.Z >= 255.5f) return false;

            byte x = Convert.ToByte(uncentered.X);
            byte z = Convert.ToByte(uncentered.Z);

            if (x >= StageSizeX || z >= StageSizeZ //Outside of map
                || _balls[x, z, level] != null) //There is already a ball there
                return false;

            cell = new XZLevel(x, z, level);
            return true;
        }

        //The "decide and write in one call" pair that used to sit here — PutBallAtClosestEmptyCeilingPosition and
        //PutBallAtClosestEmptyPositionNextTo, one over TryFindEmptyCeilingCell above and one over
        //TryFindEmptyCellNextTo below — is gone with #68. Their last caller was the Testbed's own contact
        //handler, and that is the copy #68 merged away; the shared handler asks ShotPlacement to decide and then
        //places the answer itself, in two steps. That separation is #70's whole point, so having the combined
        //form still standing was an invitation to undo it: a caller of the combined form cannot let the aim
        //preview ask the identical question, because asking would move a ball. The refusal convention was the
        //other trap the removed remarks recorded — both signalled failure with a float.MinValue position while
        //ALSO handing back a plausible-looking cell, and a caller that tested the cell rather than the position
        //indexed the structure out of bounds or overwrote a live ball that then stayed in the simulation for
        //ever, untracked and unreleasable. TryFind* answer with a bool, so that cannot be got wrong.

        /// <summary>
        /// The most cells that can touch one cell: four on its own level and up to four on each adjacent level.
        /// The length every buffer handed to <see cref="FillNeighboringCells"/> must have, and the figure
        /// <see cref="CountOccupiedNeighbors"/> states as "up to 12".
        /// </summary>
        public const int MAX_NEIGHBORS = 12;

        /// <summary>
        /// Writes all in-bounds cells that geometrically touch the given cell into <paramref name="into"/> and
        /// returns how many there were: four on the same level and up to four on each adjacent level. Odd levels
        /// are shifted by +0.5 in X and Z, so their neighbors on adjacent levels sit towards +X/+Z indices, while
        /// even levels neighbor towards -X/-Z.
        /// <para>
        /// <b>The order is load-bearing, and it is the order this has always produced</b>: the four on the cell's
        /// own level (-X, +X, -Z, +Z), then the four below, then the four above. <see cref="CollectAcidShaft"/>
        /// breaks its ties on it — on an on-axis step all four candidates are equidistant and the first one wins —
        /// so two identical clusters break the same way, and the shipped campaign was generated and gated against
        /// exactly this sequence. A change to it is a change to the levels on disk.
        /// </para>
        /// </summary>
        /// <param name="into">
        /// At least <see cref="MAX_NEIGHBORS"/> long. A shorter buffer is an index-out-of-range on the write that
        /// overruns it rather than a truncated answer, which is the loud failure this would want anyway.
        /// </param>
        public static int FillNeighboringCells(XZLevel cell, XZLevel size, Span<XZLevel> into)
        {
            int count = 0;

            //Same level
            if (cell.X - 1 >= 0) into[count++] = new XZLevel(cell.X - 1, cell.Z, cell.Level);
            if (cell.X + 1 < size.X) into[count++] = new XZLevel(cell.X + 1, cell.Z, cell.Level);
            if (cell.Z - 1 >= 0) into[count++] = new XZLevel(cell.X, cell.Z - 1, cell.Level);
            if (cell.Z + 1 < size.Z) into[count++] = new XZLevel(cell.X, cell.Z + 1, cell.Level);

            //Levels above and below
            int diagonalShift = (cell.Level % 2) > 0 ? 0 : -1;

            for (int levelOffset = -1; levelOffset <= 1; levelOffset += 2)
            {
                int level = cell.Level + levelOffset;
                if (level < 0 || level >= size.Level) continue;

                for (int dX = 0; dX <= 1; dX++)
                    for (int dZ = 0; dZ <= 1; dZ++)
                    {
                        int x = cell.X + dX + diagonalShift;
                        int z = cell.Z + dZ + diagonalShift;

                        if (x >= 0 && z >= 0 && x < size.X && z < size.Z) into[count++] = new XZLevel(x, z, level);
                    }
            }

            return count;
        }

        /// <summary>
        /// The same cells in the same order, for a <c>foreach</c> — and, unlike the <c>yield return</c> method
        /// this replaced, without allocating an enumerator to do it (#381). See <see cref="NeighboringCells"/>
        /// for why that mattered: this walk is on the frame path, not only the shot path.
        /// </summary>
        public static NeighboringCells GetNeighboringCells(XZLevel cell, XZLevel size) => new(cell, size);

        /// <summary>
        /// Counts occupied cells among the up-to-12 cells touching the given one — the same parity rules as
        /// <see cref="GetNeighboringCells"/>. Used for ambient occlusion, over either the logical or the physics
        /// ball array.
        /// <para>
        /// <b>Why it walks the neighborhood itself</b> rather than over <see cref="GetNeighboringCells"/>: it
        /// never wants the cells. It tests the array and accumulates a direction in the same pass, and the
        /// direction needs the <c>dX</c>/<c>dZ</c> of the step it is taking, which a produced cell has already
        /// thrown away. Until #381 the reason on this comment was the allocation — that one is gone, the walk is
        /// free to enumerate now, and this still does not want to.
        /// </para>
        /// </summary>
        /// <param name="occlusionDirection">Sum of the unit vectors pointing at the occupied neighbors
        /// (touching neighbors are always exactly one ball diameter away, so every contribution has length 1).
        /// The part of the ball surface facing this direction is the occluded one.</param>
        public static int CountOccupiedNeighbors<T>(T[,,] balls, XZLevel cell, XZLevel size, out Vector3 occlusionDirection) where T : class
        {
            int occupied = 0;
            occlusionDirection = Vector3.Zero;

            if (cell.X - 1 >= 0 && balls[cell.X - 1, cell.Z, cell.Level] != null) { occupied++; occlusionDirection.X -= 1f; }
            if (cell.X + 1 < size.X && balls[cell.X + 1, cell.Z, cell.Level] != null) { occupied++; occlusionDirection.X += 1f; }
            if (cell.Z - 1 >= 0 && balls[cell.X, cell.Z - 1, cell.Level] != null) { occupied++; occlusionDirection.Z -= 1f; }
            if (cell.Z + 1 < size.Z && balls[cell.X, cell.Z + 1, cell.Level] != null) { occupied++; occlusionDirection.Z += 1f; }

            int diagonalShift = (cell.Level % 2) > 0 ? 0 : -1;

            for (int levelOffset = -1; levelOffset <= 1; levelOffset += 2)
            {
                int level = cell.Level + levelOffset;
                if (level < 0 || level >= size.Level) continue;

                float offsetY = levelOffset * Constants.SQRT_TWO * Constants.HALF;

                for (int dX = 0; dX <= 1; dX++)
                    for (int dZ = 0; dZ <= 1; dZ++)
                    {
                        int x = cell.X + dX + diagonalShift;
                        int z = cell.Z + dZ + diagonalShift;

                        if (x >= 0 && z >= 0 && x < size.X && z < size.Z && balls[x, z, level] != null)
                        {
                            occupied++;
                            //Independently of level parity the horizontal offset to a touching cross-level neighbor is ±0.5
                            occlusionDirection += new Vector3(dX - Constants.HALF, offsetY, dZ - Constants.HALF);
                        }
                    }
            }

            return occupied;
        }

        /// <summary>
        /// The free cell touching <paramref name="nextTo"/> that is closest to <paramref name="position"/> — the
        /// <b>first ring</b>. Candidates come from <see cref="GetNeighboringCells"/>, so they are in-bounds by
        /// construction. Writes nothing, which is what lets the aim preview ask the same question the attach
        /// asks (#70).
        /// </summary>
        /// <param name="cell">The cell, <b>only meaningful when this returns <c>true</c></b>.</param>
        public bool TryFindEmptyCellNextTo(Vector3 position, XZLevel nextTo, out XZLevel cell)
        {
            cell = new XZLevel(-1, -1, -1);

            float closestDistanceSquared = float.MaxValue;

            foreach (XZLevel candidate in GetNeighboringCells(nextTo, new XZLevel(StageSizeX, StageSizeZ, Levels)))
            {
                if (_balls[candidate.X, candidate.Z, candidate.Level] != null) continue;

                float distanceSquared = Vector3.DistanceSquared(GetRealCenteredPosition(candidate), position);

                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    cell = candidate;
                }
            }

            return cell.X >= 0;
        }

        /// <summary>
        /// The free cell nearest <paramref name="position"/> among those touching a ball that itself touches
        /// <paramref name="nextTo"/> — one ring further out than <see cref="TryFindEmptyCellNextTo"/> looks.
        /// <para>
        /// It exists because nothing free touching the ball a shot hit is <b>not</b> an exotic case: the ball a
        /// shot reaches first is on the cluster's outer face, and where that face is the field's own wall there is
        /// no cell beyond it, so the pocket around an edge ball fills after a handful of shots. Without this every
        /// shot after that would be silently eaten.
        /// </para>
        /// <para>
        /// <b>Two rings and no more, deliberately.</b> The search is local so that a ball never lands somewhere it
        /// could not have rolled to — widening it further is how a shot ends up attaching visibly behind what the
        /// player aimed at, which is the defect #70 was opened for. When both rings are full the honest answer is
        /// that this shot does not stick, and the aim preview is what tells the player so <i>before</i> they spend
        /// it rather than after.
        /// </para>
        /// </summary>
        /// <param name="cell">The cell, <b>only meaningful when this returns <c>true</c></b>.</param>
        public bool TryFindEmptyCellInSecondRing(Vector3 position, XZLevel nextTo, out XZLevel cell)
        {
            cell = new XZLevel(-1, -1, -1);

            XZLevel size = new(StageSizeX, StageSizeZ, Levels);
            float closest = float.MaxValue;

            foreach (XZLevel neighbour in GetNeighboringCells(nextTo, size))
            {
                if (_balls[neighbour.X, neighbour.Z, neighbour.Level] == null) continue; //free cells were the first ring's business

                foreach (XZLevel candidate in GetNeighboringCells(neighbour, size))
                {
                    if (_balls[candidate.X, candidate.Z, candidate.Level] != null) continue;

                    float distance = Vector3.DistanceSquared(GetRealCenteredPosition(candidate), position);
                    if (distance >= closest) continue;

                    closest = distance;
                    cell = candidate;
                }
            }

            return cell.X >= 0;
        }

        /// <summary>
        /// Finds the connected cluster of balls of the same <see cref="BallType"/> as the ball at <paramref name="start"/>,
        /// walking over touching cells (see <see cref="GetNeighboringCells"/>). The start cell itself is included.
        /// Returns an empty list when the start cell is empty.
        /// <para>
        /// <b>A ball only joins the group if its kind lets it</b> (#323's first seam,
        /// <see cref="BallKinds.Matchable"/>). A rock is not a colour the wave can pass through: it stops the
        /// walk exactly as an empty cell does, so a group is never counted <i>through</i> one and a rock is
        /// never released by a match. That is the whole of the Rock's removal rule — everything else about it
        /// falls out of code that was already here, because
        /// <see cref="GetCellsDisconnectedFromCeiling"/> has never cared what kind a ball is.
        /// </para>
        /// <para>
        /// The <b>start</b> is tested too, and not only for symmetry: this is asked of the cell a shot landed
        /// in, and a shot is always an ordinary ball today — but an unmatchable start would otherwise report a
        /// group of one and quietly make every future kind that lands in the lattice a special case here.
        /// </para>
        /// </summary>
        public List<XZLevel> GetConnectedSameTypeCells(XZLevel start)
        {
            List<XZLevel> cluster = new();

            StaticBall startBall = _balls[start.X, start.Z, start.Level];
            if (startBall == null || !BallKinds.Matchable(startBall.Kind)) return cluster;

            XZLevel size = new(StageSizeX, StageSizeZ, Levels);

            var visited = new bool[StageSizeX, StageSizeZ, Levels];
            var toVisit = new Queue<XZLevel>();

            visited[start.X, start.Z, start.Level] = true;
            toVisit.Enqueue(start);

            while (toVisit.Count > 0)
            {
                XZLevel cell = toVisit.Dequeue();
                cluster.Add(cell);

                foreach (XZLevel neighbor in GetNeighboringCells(cell, size))
                {
                    if (visited[neighbor.X, neighbor.Z, neighbor.Level]) continue;
                    visited[neighbor.X, neighbor.Z, neighbor.Level] = true;

                    StaticBall neighborBall = _balls[neighbor.X, neighbor.Z, neighbor.Level];
                    if (neighborBall == null || neighborBall.Type != startBall.Type
                        || !BallKinds.Matchable(neighborBall.Kind)) continue;

                    toVisit.Enqueue(neighbor);
                }
            }

            return cluster;
        }

        /// <summary>
        /// How many cells can touch one cell: four on its own level, four on the level above and four below
        /// (<see cref="GetNeighboringCells"/>, and <see cref="CountOccupiedNeighbors"/> states the same figure).
        /// Named because the obvious guess is eight, and a span sized by that guess would throw on the landing
        /// the day a cell touched nine different colours.
        /// </summary>
        private const int MAX_TOUCHING_CELLS = 12;

        /// <summary>
        /// What colour a <see cref="BallKind.Wildcard"/> landing in <paramref name="cell"/> should become
        /// (#330): the one that completes the <b>largest</b> group, counting the wildcard itself. False when the
        /// cell touches nothing matchable, and then the caller keeps the colour the ball was showing — see
        /// <see cref="BallKind.Wildcard"/> for why that fallback is the honest one.
        /// <para>
        /// Asked <b>before</b> the ball is written into the map, which is why this cannot simply be
        /// <see cref="GetConnectedSameTypeCells"/> on the landing cell: the cell is still empty, so each
        /// candidate colour is walked as if the wildcard were already there and already that colour. That
        /// ordering is deliberate — everything downstream of the landing (the glass colouring, the zap's colour,
        /// the group release, the score, the tint the award flies in) reads the shot ball's
        /// <see cref="BallType"/>, so the collapse has to be finished before any of it runs.
        /// </para>
        /// <para>
        /// <b>Largest group, and the tie-breaks are stated rather than left to whatever the walk happens to
        /// meet first.</b> Equal groups go to the colour the landing actually touches most balls of, and a still
        /// perfect tie to the lower <see cref="BallType"/> — arbitrary, but fixed, so two identical-looking
        /// landings can never do different things. The largest-group rule itself is what a player expects a
        /// wildcard to do; anything else reads as the game refusing a good shot.
        /// </para>
        /// <para>
        /// Costs one walk per distinct touching colour — at most eight, and in practice one or two — on a
        /// landing that happens a handful of times in a level. The stamp buffer and the queue are allocated once
        /// here and reused across those walks rather than per candidate.
        /// </para>
        /// </summary>
        /// <param name="group">How big the group it chose would be, the wildcard included — 3 or more is a
        /// match. Zero when there was nothing to choose between. It exists for the handler's landing line: a
        /// level whose wildcards keep landing on groups of two says so in the log instead of being diagnosed
        /// from a screenshot, which is the same job <c>BallLanding.Coloured</c> does for the glass.</param>
        public bool TryChooseWildcardColour(XZLevel cell, out BallType type, out int group)
        {
            type = default;
            group = 0;

            XZLevel size = new(StageSizeX, StageSizeZ, Levels);

            //At most one candidate colour per touching cell - see MAX_TOUCHING_CELLS for why that is not eight
            Span<BallType> candidates = stackalloc BallType[MAX_TOUCHING_CELLS];
            Span<int> touching = stackalloc int[MAX_TOUCHING_CELLS];
            int candidateCount = 0;

            foreach (XZLevel neighbour in GetNeighboringCells(cell, size))
            {
                StaticBall ball = _balls[neighbour.X, neighbour.Z, neighbour.Level];
                if (ball == null || !BallKinds.Matchable(ball.Kind)) continue;

                int existing = -1;
                for (int i = 0; i < candidateCount; i++) if (candidates[i] == ball.Type) { existing = i; break; }

                if (existing >= 0) touching[existing]++;
                else
                {
                    candidates[candidateCount] = ball.Type;
                    touching[candidateCount] = 1;
                    candidateCount++;
                }
            }

            if (candidateCount == 0) return false;

            var stamp = new byte[StageSizeX, StageSizeZ, Levels];
            var toVisit = new Queue<XZLevel>();

            int bestGroup = -1;
            int bestTouching = -1;

            for (int i = 0; i < candidateCount; i++)
            {
                //The stamp value identifies the walk, so one buffer serves every candidate: a cell visited by
                //an earlier colour's walk carries that colour's mark and is not mistaken for visited by this one
                int candidateGroup = CountGroupIfPlaced(cell, candidates[i], size, stamp, (byte)(i + 1), toVisit);

                if (candidateGroup < bestGroup) continue;
                if (candidateGroup == bestGroup)
                {
                    if (touching[i] < bestTouching) continue;
                    if (touching[i] == bestTouching && candidates[i] >= type) continue;
                }

                bestGroup = candidateGroup;
                bestTouching = touching[i];
                type = candidates[i];
            }

            group = bestGroup;

            return true;
        }

        /// <summary>
        /// How many balls would be in the connected same-colour group if a ball of <paramref name="type"/> were
        /// placed in the (empty) cell <paramref name="start"/>, itself included. The walk is
        /// <see cref="GetConnectedSameTypeCells"/>' exactly — same neighbour rule, same
        /// <see cref="BallKinds.Matchable"/> gate — differing only in that the start cell is hypothetical, so it
        /// is counted and its neighbours seeded without being read out of the map.
        /// </summary>
        private int CountGroupIfPlaced(XZLevel start, BallType type, XZLevel size, byte[,,] stamp, byte mark,
            Queue<XZLevel> toVisit)
        {
            toVisit.Clear();

            stamp[start.X, start.Z, start.Level] = mark;
            int count = 1;                                  //the placed ball itself

            foreach (XZLevel neighbour in GetNeighboringCells(start, size))
            {
                if (stamp[neighbour.X, neighbour.Z, neighbour.Level] == mark) continue;

                StaticBall ball = _balls[neighbour.X, neighbour.Z, neighbour.Level];
                if (ball == null || ball.Type != type || !BallKinds.Matchable(ball.Kind)) continue;

                stamp[neighbour.X, neighbour.Z, neighbour.Level] = mark;
                toVisit.Enqueue(neighbour);
            }

            while (toVisit.Count > 0)
            {
                XZLevel cell = toVisit.Dequeue();
                count++;

                foreach (XZLevel neighbour in GetNeighboringCells(cell, size))
                {
                    if (stamp[neighbour.X, neighbour.Z, neighbour.Level] == mark) continue;

                    StaticBall ball = _balls[neighbour.X, neighbour.Z, neighbour.Level];
                    if (ball == null || ball.Type != type || !BallKinds.Matchable(ball.Kind)) continue;

                    stamp[neighbour.X, neighbour.Z, neighbour.Level] = mark;
                    toVisit.Enqueue(neighbour);
                }
            }

            return count;
        }

        /// <summary>
        /// How far off an acid's own vertical axis a shaft cell may sit, in world units (#328). Just over the
        /// <b>0.707</b> that every cell on the level below a ball sits at in this packing, so the shaft may take
        /// the half-cell step the lattice forces on it and nothing wider.
        /// <para>
        /// <b>It is what makes "vertical" structural rather than hopeful.</b> Without it a level whose on-axis
        /// cell is missing would let the walk take a neighbour a whole cell away, and from there another, and
        /// the hole would wander off across the cluster — the leaning shaft that would be blamed on the physics.
        /// With it, a missing cell is simply the end of the shaft, which is also the stop rule the player reads
        /// off the cluster before firing.
        /// </para>
        /// </summary>
        public const float ACID_SHAFT_DRIFT = 0.75f;

        /// <summary>
        /// The cells a <see cref="BallKind.Acid"/> at <paramref name="acid"/> eats (#328): itself, then one cell
        /// per level straight down until the shaft reaches a gap. Returns how many were written into
        /// <paramref name="shaft"/>, which is cleared first — one is the acid alone, which is what an acid with
        /// nothing beneath it costs.
        /// <para>
        /// <b>⚠ THE SHAFT IS WALKED CELL BY CELL AND NOT CAST AS A VERTICAL CYLINDER, and the geometry that
        /// decides it was measured rather than assumed.</b> #328 recommended casting the vertical line from the
        /// acid's world position and taking every cell within half a ball of it. In this packing that does not
        /// work: odd levels are shifted by +0.5 in X and Z, so <b>all four</b> cells on the level below a ball
        /// sit at a horizontal distance of <b>0.707</b> from its axis — the same figure for either parity —
        /// while the cell two levels down sits at <b>0.000</b>. A vertical line through a ball's centre passes
        /// <i>between</i> the four balls under it. A cylinder of half a ball's radius therefore takes every
        /// SECOND level and leaves a dotted hole with balls hanging inside it; one wide enough to catch 0.707
        /// takes all four at once and is a fat tube, which is the bomb's shape and not this one's.
        /// </para>
        /// <para>
        /// <b>⚠ And #328's other warning — that walking <c>level--</c> at a fixed <c>(x, z)</c> "drills a
        /// diagonal shaft leaning off in one direction", named there as the single most likely way to get this
        /// wrong — is itself wrong, measured over thirteen levels.</b> The parity shift <i>alternates</i>, so a
        /// fixed index column sits at 0.707, 0.000, 0.707, 0.000 … off the axis: it wobbles half a cell and the
        /// deviation is <b>bounded</b>, where a lean would grow without limit. This walk reduces to exactly that
        /// column in a dense cluster; what it adds is its behaviour at holes, which is where the hazard really is.
        /// </para>
        /// <para>
        /// Each step takes the occupied cell below whose horizontal distance to <b>the acid's own axis</b> is
        /// smallest — the acid's, not the previous cell's, which is what makes the walk self-correcting: from an
        /// on-axis cell every candidate is 0.707 away and one is picked, and from that cell the candidate back
        /// <i>on</i> the axis is 0.000 away and always wins. Ties (the on-axis step, where all four are equal) go
        /// to the order <see cref="GetNeighboringCells"/> yields, so two identical clusters always break the same
        /// way. <see cref="ACID_SHAFT_DRIFT"/> is what stops a hole turning the walk into that leaning shaft.
        /// </para>
        /// <para>
        /// <b>The stop rule is the first gap</b>, and it is a design decision rather than an implementation
        /// detail: depth becomes a property of the level's own construction, so an author can build a shallow
        /// shaft and a deep one and the player can read which is which off the cluster before firing. An acid
        /// that fell through gaps would be a zap with a different shape and no counterplay — and on a tall level,
        /// where the field is deliberately larger than the layout, it would reach the floor nearly every time.
        /// </para>
        /// <para>
        /// <b>What "a gap" means falls out of the packing rather than being chosen, and the two steps differ</b>
        /// (both measured): stepping <i>down from an even level</i> there is exactly one cell inside the bound —
        /// the one on the axis, at 0.000, its three siblings being a whole cell away — so a hole there ends the
        /// shaft even when the column continues below it. Stepping <i>down from an odd level</i> all four
        /// candidates sit at 0.707, so a single missing ball is <b>jogged around</b> half a cell and the shaft
        /// carries on, still inside the bound. That is the honest reading of a hole in this packing: a drill
        /// stops at a floor, not at one absent ball beside its edge.
        /// </para>
        /// <para>
        /// It asks nothing of the physics, which is why it lives here beside the other walks over the grid and
        /// not with the removal that consumes it: what is under a cell is a question about the lattice.
        /// </para>
        /// </summary>
        public int CollectAcidShaft(XZLevel acid, List<XZLevel> shaft)
        {
            shaft.Clear();

            StaticBall start = _balls[acid.X, acid.Z, acid.Level];
            if (start == null || start.Kind != BallKind.Acid) return 0;

            XZLevel size = new(StageSizeX, StageSizeZ, Levels);
            Vector3 top = GetRealPosition((byte)acid.X, (byte)acid.Z, (byte)acid.Level);

            shaft.Add(acid);

            XZLevel current = acid;

            while (true)
            {
                XZLevel next = default;
                float nearest = float.MaxValue;
                bool found = false;

                foreach (XZLevel candidate in GetNeighboringCells(current, size))
                {
                    if (candidate.Level != current.Level - 1) continue;
                    if (_balls[candidate.X, candidate.Z, candidate.Level] == null) continue;

                    Vector3 at = GetRealPosition((byte)candidate.X, (byte)candidate.Z, (byte)candidate.Level);

                    //Horizontal only: every candidate is one level down, so the drop is the same for all of them
                    //and including it would add the same constant to every distance
                    float dx = at.X - top.X;
                    float dz = at.Z - top.Z;
                    float distance = dx * dx + dz * dz;

                    if (distance > ACID_SHAFT_DRIFT * ACID_SHAFT_DRIFT) continue;
                    if (distance >= nearest) continue;

                    nearest = distance;
                    next = candidate;
                    found = true;
                }

                if (!found) break;      //the first gap, which is where a shaft ends

                shaft.Add(next);
                current = next;
            }

            return shaft.Count;
        }

        /// <summary>
        /// Returns cells of balls that are no longer connected to the ceiling: walks the touching-neighbor graph
        /// (see <see cref="GetNeighboringCells"/>) from all balls on the top level (those hang from the ceiling)
        /// and collects every ball the walk did not reach.
        /// </summary>
        public List<XZLevel> GetCellsDisconnectedFromCeiling()
        {
            XZLevel size = new(StageSizeX, StageSizeZ, Levels);

            var visited = new bool[StageSizeX, StageSizeZ, Levels];
            var toVisit = new Queue<XZLevel>();

            byte topLevel = (byte)(Levels - 1);
            for (byte x = 0; x < StageSizeX; x++)
                for (byte z = 0; z < StageSizeZ; z++)
                    if (_balls[x, z, topLevel] != null)
                    {
                        visited[x, z, topLevel] = true;
                        toVisit.Enqueue(new XZLevel(x, z, topLevel));
                    }

            while (toVisit.Count > 0)
            {
                XZLevel cell = toVisit.Dequeue();

                foreach (XZLevel neighbor in GetNeighboringCells(cell, size))
                {
                    if (visited[neighbor.X, neighbor.Z, neighbor.Level]) continue;
                    if (_balls[neighbor.X, neighbor.Z, neighbor.Level] == null) continue;

                    visited[neighbor.X, neighbor.Z, neighbor.Level] = true;
                    toVisit.Enqueue(neighbor);
                }
            }

            List<XZLevel> disconnected = new();

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null && !visited[x, z, level])
                            disconnected.Add(new XZLevel(x, z, level));

            return disconnected;
        }

        public StaticBall[,,] GetStaticBallsArray() => _balls;

        public XZLevel GetStaticBallsArraySize() => XZLevel.FromArray(_balls);

        /// <summary>
        /// The lowest level index holding at least one ball — how deep the layout actually reaches, as
        /// opposed to how deep the field is. An empty map answers with the top level: the caller is asking
        /// how far down the layout hangs, and an empty one hangs nowhere.
        /// </summary>
        public byte GetLowestOccupiedLevel()
        {
            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                            return level;

            return (byte)(Levels - 1);
        }

        public void Clear()
        {
            if (_balls == null) return;

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        _balls[x, z, level] = null;
        }

        public int GetBallsCount()
        {
            int count = 0;

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                            count++;
            return count;
        }

        /// <summary>
        /// How many balls are still there that a shot can do anything about (#323) — everything
        /// <see cref="BallKinds.Removable"/> says yes to. <b>This, and not
        /// <see cref="GetBallsCount"/>, is what the end of a level is decided on.</b>
        /// <para>
        /// The moment a ball can exist that no colour removes, both of the Game's end conditions read the wrong
        /// number: <c>CheckLevelCleared</c> returns early while any ball hangs, so a level with one rock in it
        /// is <i>never</i> cleared, and the out-of-shots test beside it reads the same count, so the same level
        /// is <i>always</i> lost. Two conditions, one count, opposite failures — which is exactly why this is
        /// one method both of them call rather than a predicate each of them grew.
        /// </para>
        /// <para>
        /// A rock still counts in <see cref="GetBallsCount"/>, deliberately: that is "what is hanging here",
        /// which is what the drawing, the physics and the cluster profile all want.
        /// </para>
        /// <para>
        /// ⚠ It asked <see cref="BallKinds.Matchable"/> until #325 and was named for it. A transparent ball is
        /// not matchable and <i>is</i> removable — one shot beside it makes it ordinary — so the two questions
        /// parted, and this one has always been the removable one: the doc sentence above was already written
        /// in <see cref="BallKinds.Removable"/>'s words while the code asked the other predicate.
        /// </para>
        /// </summary>
        public int GetRemovableBallsCount()
        {
            int count = 0;

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null && BallKinds.Removable(_balls[x, z, level].Kind))
                            count++;
            return count;
        }

        /// <summary>
        /// Colours the whole connected body of <see cref="BallKind.Transparent"/> balls that touches
        /// <paramref name="cell"/> in <paramref name="type"/>, turning each into an ordinary ball of that
        /// colour, and reports the cells it changed (#325, #344). Nothing else in the map moves.
        /// <para>
        /// <b>The landing still decides WHERE, and it is still the shot ball's colour</b> — the colour of the
        /// group that was removed, or of the nearest coloured ball, or of whatever the contact was technically
        /// made against are all rules that fire somewhere the player was not looking. A landing is a place, and
        /// this starts from what stands next to that place.
        /// </para>
        /// <para>
        /// <b>⚠ IT DOES NOT STOP THERE, and that RETRACTS this method's own first rule (#344).</b> #325 scoped
        /// the colouring to the balls actually touching the landing cell, deliberately, to keep it unambiguous.
        /// The owner's verdict overrides it: one ball recoloured among still-colourless neighbours is a ball
        /// the player can do nothing with, and a mechanic nobody can use does not read as one. So the colour
        /// runs through the glass — every transparent ball reachable from the landing through other transparent
        /// balls takes it, in one landing, and what appears is a whole same-coloured group where a pane of
        /// glass was. That is what makes the shot worth aiming: it does not pay a ball, it pays a group, and by
        /// the usual match rule it very often pays what the group was holding up as well.
        /// </para>
        /// <para>
        /// <b>Every transparent neighbour takes it, not one of them</b>, for the reason that outlived the
        /// scope change: choosing among several would be invisible dice, two identical-looking landings doing
        /// different things.
        /// </para>
        /// <para>
        /// The walk needs no visited set and allocates nothing: colouring a cell <i>is</i> the mark, since the
        /// ball that replaces it answers <see cref="BallKind.Normal"/> and can never be seeded again. So
        /// <paramref name="coloured"/> serves as its own worklist, read forward while it is still growing.
        /// </para>
        /// <para>
        /// The ball is <b>replaced</b> rather than mutated, which is why this lives on the map: a
        /// <see cref="StaticBall"/>'s colour and kind are read-only by construction, and
        /// <see cref="PutBallAt"/> already builds one at the right world position for a cell. The caller
        /// mirrors the change onto the physics side, which this library cannot see.
        /// </para>
        /// </summary>
        /// <param name="cell">Where the shot came to rest.</param>
        /// <param name="type">The shot ball's own colour.</param>
        /// <param name="coloured">Filled with the cells that changed — empty when no glass was touching, which
        /// is nearly every landing. Cleared first, so one list can serve every shot of a level.</param>
        /// <returns>How many balls were coloured.</returns>
        public int ColourTransparentGroup(XZLevel cell, BallType type, List<XZLevel> coloured)
        {
            coloured.Clear();

            XZLevel size = new(StageSizeX, StageSizeZ, Levels);

            ColourTransparentNeighbours(cell, type, size, coloured);

            //The list is the worklist. Count is re-read every pass on purpose: the loop is meant to see what
            //the pass before it appended.
            for (int i = 0; i < coloured.Count; i++)
                ColourTransparentNeighbours(coloured[i], type, size, coloured);

            return coloured.Count;
        }

        /// <summary>
        /// One ring of <see cref="ColourTransparentGroup"/>'s walk: colours the transparent balls touching
        /// <paramref name="cell"/> and appends them to <paramref name="coloured"/>, which it never clears.
        /// </summary>
        private void ColourTransparentNeighbours(XZLevel cell, BallType type, XZLevel size, List<XZLevel> coloured)
        {
            foreach (XZLevel neighbor in GetNeighboringCells(cell, size))
            {
                StaticBall ball = _balls[neighbor.X, neighbor.Z, neighbor.Level];
                if (ball == null || ball.Kind != BallKind.Transparent) continue;

                PutBallAt((byte)neighbor.X, (byte)neighbor.Z, (byte)neighbor.Level, type);
                coloured.Add(neighbor);
            }
        }

        /// <summary>
        /// Breaks the ice on every <see cref="BallKind.Frozen"/> ball standing next to a group that has just
        /// been cleared, turning each into an ordinary ball of <b>its own</b> colour, and reports the cells it
        /// changed (#329). Nothing else in the map moves.
        /// <para>
        /// <b>The group, not the landing</b>, and that is #329's ruling: a big group breaks more ice than a
        /// small one, so reading the cluster is what pays — which is the currency this game already deals in,
        /// an orphan being worth double a matched ball. The cells handed in are the group's, so they are
        /// <i>already empty</i> by the time this runs; only their neighbour indices are wanted, and
        /// <see cref="GetNeighboringCells"/> is index arithmetic that neither knows nor cares whether the cell
        /// it starts from still holds a ball.
        /// </para>
        /// <para>
        /// <b>Its OWN colour, where the glass takes the shot's</b> (#325's <see cref="ColourTransparentGroup"/>
        /// is otherwise this method's twin down to the walk). A frozen ball has been wearing a colour the
        /// player could see through the ice all along, and the whole of what it says is "this red one will be
        /// available later" — handing it the shot's colour instead would make the level's plan a lie told to
        /// the player two shots before it is found out.
        /// </para>
        /// <para>
        /// <b>It does not spread.</b> The glass runs a flood fill through the connected body of glass; ice
        /// breaks one ball deep, exactly the ring the group touched. A frozen ball behind another frozen ball
        /// takes the shot after, which is what makes a wall of ice a sequence of moves rather than one.
        /// </para>
        /// <para>
        /// No visited set and nothing allocated: thawing a cell <i>is</i> the mark, since the ball that
        /// replaces it answers <see cref="BallKind.Normal"/> and can never be seen again by this walk. So a
        /// frozen ball touching two cells of the same group thaws once, which is what the <see cref="StaticBall"/>
        /// replacement buys for free.
        /// </para>
        /// <para>
        /// The ball is <b>replaced</b> rather than mutated, for <see cref="ColourTransparentGroup"/>'s reason: a
        /// <see cref="StaticBall"/>'s colour and kind are read-only by construction. The caller mirrors the
        /// change onto the physics side, which this library cannot see.
        /// </para>
        /// </summary>
        /// <param name="group">The cells the released group occupied. Read, never written.</param>
        /// <param name="thawed">Filled with the cells that changed — empty on nearly every release, which is
        /// every level with no ice in it. Cleared first, so one list can serve every shot of a level.</param>
        /// <returns>How many balls thawed.</returns>
        public int ThawFrozenBesideGroup(List<XZLevel> group, List<XZLevel> thawed)
        {
            thawed.Clear();

            if (group == null || group.Count == 0) return 0;

            XZLevel size = new(StageSizeX, StageSizeZ, Levels);

            for (int i = 0; i < group.Count; i++)
                foreach (XZLevel neighbour in GetNeighboringCells(group[i], size))
                {
                    StaticBall ball = _balls[neighbour.X, neighbour.Z, neighbour.Level];
                    if (ball == null || ball.Kind != BallKind.Frozen) continue;

                    PutBallAt((byte)neighbour.X, (byte)neighbour.Z, (byte)neighbour.Level, ball.Type);
                    thawed.Add(neighbour);
                }

            return thawed.Count;
        }

        /// <summary>
        /// One tick of the infection (#331): every <see cref="BallKind.Infectious"/> ball on the field infects
        /// one healthy neighbour and <b>hardens into a <see cref="BallKind.Rock"/></b>. Reports both sets of
        /// cells. Nothing else in the map moves.
        /// <para>
        /// <b>⚠ WHEN this is called is the whole design of the kind and it is NOT this method's business.</b>
        /// It is the only rule in the game that fires on something other than a landing, and #331 is emphatic
        /// about where it goes: <b>after the release, before the census</b>. Put it after the census and every
        /// derived thing — the magazine's live colours, the clear test, the loss test — is one shot stale, and
        /// the bug that produces is intermittent and gets blamed on the physics. The Game states that order in
        /// <c>GameplayScreen.Rules</c>; this method only says what a tick <i>is</i>.
        /// </para>
        /// <para>
        /// <b>It moves rather than multiplies.</b> The spreader hardens in the same tick it infects, so the
        /// number of sick balls can only stay level or fall — it falls when one has no healthy neighbour left
        /// and hardens with nothing to pass on, which is how an infection in a pocket burns itself out. That is
        /// the stopping rule #331 asks to be <i>built in rather than tuned later</i>, and it needs no per-ball
        /// counter and therefore no new key in the map format. What grows is the stone behind it: exactly one
        /// permanent ball per sick ball per shot of delay.
        /// </para>
        /// <para>
        /// <b>The population is read BEFORE any of it acts</b>, which is the one thing in here that would be
        /// wrong if it were written the obvious way. Walking the array and spreading as it goes would let a
        /// ball infected by this same tick infect again inside it — a chain across the field in one shot, at a
        /// speed nothing else in the game moves at, and dependent on the array's scan order rather than on the
        /// rule. So the sick cells are collected first and then acted on.
        /// </para>
        /// <para>
        /// <b>It takes a healthy ball and nothing else</b> — <see cref="BallKind.Normal"/> only. Every other
        /// kind is already not an ordinary ball, so none of them is a special case here: stone, glass, bombs,
        /// zaps, acids, ice and other sick balls are all passed over. A <see cref="BallKind.Frozen"/> ball is
        /// worth knowing about, because the interaction reads as itself: ice is shelter, until the player
        /// breaks it.
        /// </para>
        /// <para>
        /// <b>It CLIMBS, and the tie-break is total.</b> Of the healthy neighbours it takes the one nearest the
        /// ceiling (highest <c>Level</c>), then — among equals, which the packing makes common, since all four
        /// cells on an adjacent level sit the same distance from a ball's axis — the lowest <c>X</c> and then
        /// the lowest <c>Z</c>. Climbing is what makes the threat legible: a player can see where it is going
        /// and how many shots they have. A random neighbour would be invisible dice, and two identical-looking
        /// fields would do different things.
        /// </para>
        /// </summary>
        /// <param name="infected">Filled with the cells that just became sick. Cleared first.</param>
        /// <param name="hardened">Filled with the cells that just became stone. Cleared first. Never shorter
        /// than <paramref name="infected"/>: every spreader hardens, whether or not it found anyone.</param>
        /// <returns>How many balls hardened, which is what one tick costs the player.</returns>
        public int SpreadInfection(List<XZLevel> infected, List<XZLevel> hardened)
        {
            infected.Clear();
            hardened.Clear();

            XZLevel size = new(StageSizeX, StageSizeZ, Levels);

            //The population, read before any of it acts — see the remarks. `hardened` is the list because every
            //one of these is going to be stone by the end of the tick, so it needs no second buffer.
            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null && _balls[x, z, level].Kind == BallKind.Infectious)
                            hardened.Add(new XZLevel(x, z, level));

            for (int i = 0; i < hardened.Count; i++)
            {
                XZLevel sick = hardened[i];

                if (TryFindInfectionTarget(sick, size, out XZLevel target))
                {
                    StaticBall victim = _balls[target.X, target.Z, target.Level];
                    PutBallAt((byte)target.X, (byte)target.Z, (byte)target.Level, victim.Type, BallKind.Infectious);
                    infected.Add(target);
                }

                //Hardened whether or not it found anyone, which is the burn-out: an infection walled in by
                //stone, glass and its own trail turns to stone itself and the level is rid of it.
                StaticBall ball = _balls[sick.X, sick.Z, sick.Level];
                PutBallAt((byte)sick.X, (byte)sick.Z, (byte)sick.Level, ball.Type, BallKind.Rock);
            }

            return hardened.Count;
        }

        /// <summary>
        /// <see cref="SpreadInfection"/>'s choice of victim: the healthy neighbour nearest the ceiling, with a
        /// total tie-break so the rule is a rule rather than a scan order. See that method for why it climbs.
        /// </summary>
        private bool TryFindInfectionTarget(XZLevel from, XZLevel size, out XZLevel target)
        {
            target = default;
            bool found = false;

            foreach (XZLevel neighbour in GetNeighboringCells(from, size))
            {
                StaticBall ball = _balls[neighbour.X, neighbour.Z, neighbour.Level];

                //Healthy means ORDINARY. A ball already sick is not a victim, and every other kind is already
                //not an ordinary ball — see SpreadInfection's remarks on why that is one rule and not seven.
                if (ball == null || ball.Kind != BallKind.Normal) continue;

                if (!found || Prefers(neighbour, target)) { target = neighbour; found = true; }
            }

            return found;
        }

        /// <summary>Higher first, then lowest X, then lowest Z — <see cref="SpreadInfection"/>'s total order.</summary>
        private static bool Prefers(XZLevel candidate, XZLevel best) =>
            candidate.Level != best.Level ? candidate.Level > best.Level
            : candidate.X != best.X ? candidate.X < best.X
            : candidate.Z < best.Z;

        public void SerializeAsJson(string fileName)
        {
            var ballPositionTypes = BuildBallPositionTypes();
            var json = JsonSerializer.Serialize(ballPositionTypes);
            File.WriteAllText(fileName, json);
        }

        /// <summary>
        /// The current layout as a serializable data bag (field size plus the ball array) — what a level file
        /// embeds as its map, and the same shape <see cref="SerializeAsJson"/> writes for a plain map file.
        /// </summary>
        public BallPositionTypes ToBallPositionTypes() => BuildBallPositionTypes();

        public void DeserializeJson(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

            using FileStream stream = File.OpenRead(fileName);
            BallPositionTypes ballPositionTypes = JsonSerializer.Deserialize<BallPositionTypes>(stream);

            if (ballPositionTypes == null || ballPositionTypes.Balls == null) return;

            ApplyBallPositionTypes(ballPositionTypes);
        }

        /// <summary>
        /// Validates deserialized map data, sizes the play field (legacy files carry no field size, so they
        /// get extra bottom levels to grow into) and builds the ball layout. Shared by the map file path and
        /// the level path, which hands the data over already deserialized.
        /// </summary>
        private void ApplyBallPositionTypes(BallPositionTypes ballPositionTypes)
        {
            #region Basic validation

            if (ballPositionTypes.Balls.Rank != 3)
                throw new InvalidDataException("Deserialized data invalid");

            XZLevel size = XZLevel.FromArray(ballPositionTypes.Balls);

            if (size.Level > byte.MaxValue || size.X > byte.MaxValue || size.Z > byte.MaxValue)
                throw new InvalidDataException("Deserialized data invalid");

            #endregion

            if (ballPositionTypes.Levels == 0)
            {
                //Legacy file without play field size: the field is the layout itself,
                //plus a few empty levels below so the structure has room to grow
                StageSizeX = (byte)size.X;
                StageSizeZ = (byte)size.Z;
                Levels = (byte)Math.Min(byte.MaxValue, size.Level + DEFAULT_EXTRA_LEVELS);
            }
            else
            {
                //The play field can never be smaller than the stored layout
                StageSizeX = Math.Max(ballPositionTypes.StageSizeX, (byte)size.X);
                StageSizeZ = Math.Max(ballPositionTypes.StageSizeZ, (byte)size.Z);
                Levels = Math.Max(ballPositionTypes.Levels, (byte)size.Level);
            }

            //The layout is placed at the top of the field. An odd offset would flip the level parity of every layer
            //(odd levels are shifted by +0.5 in X and Z) and change how shaped layouts nest into each other,
            //so the field is extended by one level to keep the offset even.
            if (((Levels - size.Level) % 2) != 0) Levels = (byte)Math.Min(byte.MaxValue, Levels + 1);

            BuildMapFromBallPositionTypes(ballPositionTypes);
        }

        private BallPositionTypes BuildBallPositionTypes()
        {
            BallPositionTypes ballPositionTypes = new();

            ballPositionTypes.StageSizeX = StageSizeX;
            ballPositionTypes.StageSizeZ = StageSizeZ;
            ballPositionTypes.Levels = Levels;

            ballPositionTypes.Balls = new BallPositionType[StageSizeX, StageSizeZ, Levels];

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                            ballPositionTypes.Balls[x, z, level] = new BallPositionType()
                            {
                                PositionX = _balls[x, z, level].Position.X,
                                PositionY = _balls[x, z, level].Position.Y,
                                PositionZ = _balls[x, z, level].Position.Z,
                                Type = _balls[x, z, level].Type,
                                Kind = _balls[x, z, level].Kind
                            };

            return ballPositionTypes;
        }

        private void BuildMapFromBallPositionTypes(BallPositionTypes ballPositionTypes)
        {
            _balls = new StaticBall[StageSizeX, StageSizeZ, Levels];

            XZLevel layoutSize = XZLevel.FromArray(ballPositionTypes.Balls);

            //The layout hangs from the ceiling: its top level goes to the field's top level,
            //extra field levels stay empty at the bottom as room to grow
            byte levelOffset = (byte)(Levels - layoutSize.Level);

            for (byte level = 0; level < layoutSize.Level; level++)
                for (byte x = 0; x < layoutSize.X; x++)
                    for (byte z = 0; z < layoutSize.Z; z++)
                        if (ballPositionTypes.Balls[x, z, level] != null)
                            PutBallAt(x, z, (byte)(level + levelOffset), ballPositionTypes.Balls[x, z, level].Type,
                                ballPositionTypes.Balls[x, z, level].Kind);
        }

        private Vector3 ComputeUncentered(Vector3 position)
        {
            if (!Centered) return position;

            return new(
                position.X + BoundingBoxCenter.X + BALL_RADIUS,
                position.Y,
                position.Z + BoundingBoxCenter.Y + BALL_RADIUS);
        }

        private Vector3 ComputeCentered(Vector3 position) => Centered ? ApplyCenterOffset(position) : position;

        /// <summary>Translates a raw grid-frame position into the centered world frame.</summary>
        private Vector3 ApplyCenterOffset(Vector3 position) => new(
            position.X - BoundingBoxCenter.X - BALL_RADIUS,
            position.Y,
            position.Z - BoundingBoxCenter.Y - BALL_RADIUS);

        public void Center()
        {
            float minPosX = float.MaxValue, minPosZ = float.MaxValue;
            float maxPosX = float.MinValue, maxPosZ = float.MinValue;

            byte topLevel = (byte)(Levels - 1);

            for (byte x = 0; x < StageSizeX; x++)
                for (byte z = 0; z < StageSizeZ; z++)
                    if (_balls[x, z, topLevel] != null)
                    {
                        StaticBall currentBall = _balls[x, z, topLevel];

                        if (currentBall.Position.X < minPosX) minPosX = currentBall.Position.X;
                        if (currentBall.Position.Z < minPosZ) minPosZ = currentBall.Position.Z;

                        if (currentBall.Position.X > maxPosX) maxPosX = currentBall.Position.X;
                        if (currentBall.Position.Z > maxPosZ) maxPosZ = currentBall.Position.Z;
                    }

            //An empty top level leaves min/max at their sentinels (float.Max/float.Min), and their difference
            //overflows to ±Infinity — which would poison every centered position derived from BoundingBoxCenter
            //(ComputeCentered/ComputeUncentered), so a ball shot at the ceiling of a fully empty map — the default
            //field installed at startup with no map loaded — could never attach. Fall back to the full field
            //extent, exactly what a filled top level would give, so the map centres on the origin over the whole
            //field the way the ceiling does and a ball shot up attaches centred, building a cluster from nothing.
            if (maxPosX < minPosX) //Sentinels still inverted: no ball on the top level
            {
                minPosX = 0f;
                minPosZ = 0f;
                maxPosX = StageSizeX - 1;
                maxPosZ = StageSizeZ - 1;
            }

            Vector2 minPos = new(minPosX, minPosZ);
            Vector2 maxPos = new(maxPosX, maxPosZ);

            BoundingBoxCenter = (maxPos - minPos) / 2f;

#if DEBUG
            Console.WriteLine("Map minPos: " + minPos);
            Console.WriteLine("Map maxPos: " + maxPos);
            Console.WriteLine("Map AABB center: " + BoundingBoxCenter);
#endif

            for (byte level = 0; level < Levels; level++)
                for (byte x = 0; x < StageSizeX; x++)
                    for (byte z = 0; z < StageSizeZ; z++)
                        if (_balls[x, z, level] != null)
                            _balls[x, z, level].Position = ApplyCenterOffset(_balls[x, z, level].Position);

            Centered = true;
        }
    }
}