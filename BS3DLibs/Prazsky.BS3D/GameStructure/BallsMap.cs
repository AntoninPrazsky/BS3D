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
        /// <returns>Created static ball.</returns>
        public StaticBall PutBallAt(byte stageX, byte stageZ, byte level, BallType type = BallType.Type4)
        {
            if (stageX >= StageSizeX || stageZ >= StageSizeZ || level >= Levels) throw new ArgumentOutOfRangeException($"Invalid requested ball position, array size is: {StageSizeX} × {StageSizeZ} × {Levels}");

            Vector3 realPosition = GetRealPosition(stageX, stageZ, level);
            if (Centered) realPosition = ComputeCentered(realPosition);

            var ball = new StaticBall(realPosition, type);
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
        /// Enumerates all in-bounds cells that geometrically touch the given cell: four on the same level and up to four
        /// on each adjacent level. Odd levels are shifted by +0.5 in X and Z, so their neighbors on adjacent levels sit
        /// towards +X/+Z indices, while even levels neighbor towards -X/-Z.
        /// </summary>
        public static IEnumerable<XZLevel> GetNeighboringCells(XZLevel cell, XZLevel size)
        {
            //Same level
            if (cell.X - 1 >= 0) yield return new XZLevel(cell.X - 1, cell.Z, cell.Level);
            if (cell.X + 1 < size.X) yield return new XZLevel(cell.X + 1, cell.Z, cell.Level);
            if (cell.Z - 1 >= 0) yield return new XZLevel(cell.X, cell.Z - 1, cell.Level);
            if (cell.Z + 1 < size.Z) yield return new XZLevel(cell.X, cell.Z + 1, cell.Level);

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

                        if (x >= 0 && z >= 0 && x < size.X && z < size.Z) yield return new XZLevel(x, z, level);
                    }
            }
        }

        /// <summary>
        /// Counts occupied cells among the up-to-12 cells touching the given one (the same parity rules as
        /// <see cref="GetNeighboringCells"/>, but without allocating an enumerator, since this runs for every
        /// ball every frame). Used for ambient occlusion, over either the logical or the physics ball array.
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
        /// </summary>
        public List<XZLevel> GetConnectedSameTypeCells(XZLevel start)
        {
            List<XZLevel> cluster = new();

            StaticBall startBall = _balls[start.X, start.Z, start.Level];
            if (startBall == null) return cluster;

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
                    if (neighborBall == null || neighborBall.Type != startBall.Type) continue;

                    toVisit.Enqueue(neighbor);
                }
            }

            return cluster;
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
                                Type = _balls[x, z, level].Type
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
                            PutBallAt(x, z, (byte)(level + levelOffset), ballPositionTypes.Balls[x, z, level].Type);
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