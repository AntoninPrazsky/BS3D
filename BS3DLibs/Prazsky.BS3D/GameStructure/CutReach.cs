using Prazsky.BS3D.GameStructure.DataBags;
using System.Collections.Generic;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// <b>What a Cut would take</b> (#692), asked of the logical map before the shot, and <b>where it may strike</b>.
    /// <para>
    /// <b>Where:</b> at most the lower half of the cluster as it hangs now (<see cref="IsProtected"/>, the owner's rule of
    /// 2026-10-07): the storey struck and everything under it may be no more than half the storeys from the lowest ball to
    /// the highest. It replaced a fixed four storeys under the glass, which on a tall cluster let a cut take two thirds of
    /// it and on a short one left nothing to cut at all.
    /// </para>
    /// <para>
    /// <b>What:</b> the struck ball's storey, every ball of its level joined to it through that level, then every ball that
    /// no longer reaches the top level once the storey is gone: the same two walks <c>BallsConstraintsBuilder.CutBall</c>
    /// and <c>ResolveDisconnected</c> run when the cutter lands, done here on the map alone so the gun can brighten the
    /// balls before the player fires (the owner's ask: show what will be cut off). A bomb in the storey goes off when the
    /// cut lands and its blast takes more; that blast is not foretold here.
    /// </para>
    /// <para>
    /// Reused frame after frame without allocating once its buffers are sized to the map, since the gun asks it on every
    /// frame a cutter is aimed.
    /// </para>
    /// </summary>
    public sealed class CutReach
    {
        private bool[,,] _gone;
        private bool[,,] _reached;
        private readonly Queue<XZLevel> _queue = new(256);

        /// <summary>The cells the last <see cref="Measure"/> found: the storey first, then what would fall.</summary>
        public List<XZLevel> Cells { get; } = new(256);

        /// <summary>How many of <see cref="Cells"/> are the struck storey itself.</summary>
        public int StoreyCount { get; private set; }

        /// <summary>
        /// Whether a cutter may not strike <paramref name="cell"/>: whether the storey there and everything under it would
        /// be more than half the cluster's height, from its lowest ball to its highest. A cluster of one storey can never be
        /// cut; one of eight can be cut in its lower four.
        /// </summary>
        public static bool IsProtected(XZLevel cell, BallsMap map)
        {
            int lowest = map.GetLowestOccupiedLevel();
            int highest = HighestOccupiedLevel(map);

            int span = highest - lowest + 1;      //storeys the cluster hangs through
            int taken = cell.Level - lowest + 1;  //storeys a cut there would take: its own and every one under it
            return taken * 2 > span;
        }

        /// <summary>The highest level holding a ball, or 0 for an empty map (which has no ball to strike).</summary>
        public static int HighestOccupiedLevel(BallsMap map)
        {
            StaticBall[,,] cells = map.GetStaticBallsArray();
            for (int level = map.Levels - 1; level >= 0; level--)
                for (int x = 0; x < map.StageSizeX; x++)
                    for (int z = 0; z < map.StageSizeZ; z++)
                        if (cells[x, z, level] != null)
                            return level;

            return 0;
        }

        /// <summary>
        /// Fills <see cref="Cells"/> with what a cut at <paramref name="at"/> would take: nothing for an empty cell, else the
        /// storey and what would no longer reach the top level.
        /// </summary>
        public void Measure(BallsMap map, XZLevel at)
        {
            Cells.Clear();
            StoreyCount = 0;

            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            if (at.X < 0 || at.Z < 0 || at.Level < 0 || at.X >= size.X || at.Z >= size.Z || at.Level >= size.Level) return;
            if (cells[at.X, at.Z, at.Level] == null) return;

            Prepare(size);

            //The storey: a flood over the struck level's own neighbours, CutBall's
            Cells.Add(at);
            _gone[at.X, at.Z, at.Level] = true;
            for (int i = 0; i < Cells.Count; i++)
                foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(Cells[i], size))
                {
                    if (neighbour.Level != at.Level || _gone[neighbour.X, neighbour.Z, neighbour.Level]
                        || cells[neighbour.X, neighbour.Z, neighbour.Level] == null) continue;
                    _gone[neighbour.X, neighbour.Z, neighbour.Level] = true;
                    Cells.Add(neighbour);
                }
            StoreyCount = Cells.Count;

            //Then everything still there that reaches the top level, from the top level down - BallsMap's own walk
            int top = size.Level - 1;
            _queue.Clear();
            for (int x = 0; x < size.X; x++)
                for (int z = 0; z < size.Z; z++)
                    if (cells[x, z, top] != null && !_gone[x, z, top])
                    {
                        _reached[x, z, top] = true;
                        _queue.Enqueue(new XZLevel(x, z, top));
                    }

            while (_queue.Count > 0)
                foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(_queue.Dequeue(), size))
                {
                    if (_reached[neighbour.X, neighbour.Z, neighbour.Level] || _gone[neighbour.X, neighbour.Z, neighbour.Level]
                        || cells[neighbour.X, neighbour.Z, neighbour.Level] == null) continue;
                    _reached[neighbour.X, neighbour.Z, neighbour.Level] = true;
                    _queue.Enqueue(neighbour);
                }

            //What reaches nothing falls
            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                        if (cells[x, z, level] != null && !_gone[x, z, level] && !_reached[x, z, level])
                            Cells.Add(new XZLevel(x, z, level));
        }

        //Both marks sized to the map and cleared, without allocating once they fit
        private void Prepare(XZLevel size)
        {
            if (_gone == null || _gone.GetLength(0) != size.X || _gone.GetLength(1) != size.Z || _gone.GetLength(2) != size.Level)
            {
                _gone = new bool[size.X, size.Z, size.Level];
                _reached = new bool[size.X, size.Z, size.Level];
                return;
            }

            System.Array.Clear(_gone);
            System.Array.Clear(_reached);
        }
    }
}
