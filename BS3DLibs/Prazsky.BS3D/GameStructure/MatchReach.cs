using Prazsky.BS3D.GameStructure.DataBags;
using System;
using System.Collections.Generic;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// <b>Whether a shot of one colour has anywhere to score</b> (#845), asked of the logical map: the Swap chip flashes
    /// when the round in the bore has nowhere, a streak is running and the round behind it has somewhere.
    /// <para>
    /// <b>The question, in the terms that bound it</b> (BestPractices.md §10): is there an empty cell that is joined to the
    /// field's walls or floor through empty cells, touches at least one ball, and where a ball of this colour would either
    /// complete a group of <c>minimumGroup</c> by the match rule's own walk (<see cref="BallsMap.CountGroupIfPlaced"/>)
    /// or land beside a ball a landing sets off or turns? It does <b>not</b> ask whether the gun can be laid on that cell:
    /// the elevation clamps, the crates a shot banks off and the wells that bend it are all left out, and every one of
    /// them could only take landings away. So <b>"none" is certain and "some" is only possible</b>, which is the direction
    /// a hint has to err in: a chip that flashed while a match was there to be had would teach the player to spend the
    /// Swap on nothing.
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>The open space</b> is flooded from the empty cells on the field's walls and floor, never its top
    /// (the glass), as <c>Tools/LevelGen</c>'s <c>ClearProbe</c> floods it. A flying ball's centre never enters the region
    /// of an occupied cell, so it travels only through empty cells that touch, and a pocket the flood cannot reach is one
    /// no shot can. ⚠ On nearly every field that is every empty cell: the ring of space round a cluster joins every wall
    /// to every other (§10 records the flood passing the whole pack). It shuts out only a pocket walled in on all sides,
    /// which is why it is here and also why it is not where the answer comes from.</description></item>
    /// <item><description><b>A special a landing sets off or turns counts as somewhere to score</b> without asking what it
    /// would do: glass takes the shot's colour and may complete a group through itself, and a bomb, a zap and an acid go off
    /// and take balls, which the keeper pays as destroyed. Only the kinds a landing does nothing to are walked past
    /// (<see cref="IsInert"/>: the rock, the ice, the buckshot), so a kind added later counts as scoring until somebody
    /// says otherwise, the safe default for a hint that must not cry wolf.</description></item>
    /// </list>
    /// <para>
    /// The answer is a walk over the whole field, cheap enough for a landing and too dear for every frame: the caller keeps
    /// it until <see cref="BallsMap.Revision"/> or the round changes. The buffers are kept and only regrown for a larger
    /// field, so a call allocates nothing once warm.
    /// </para>
    /// </summary>
    public sealed class MatchReach
    {
        private bool[,,] _open;
        private byte[,,] _stamp;
        private byte _mark;
        private readonly Stack<XZLevel> _flood = new(256);
        private readonly Queue<XZLevel> _walk = new(64);

        /// <summary>
        /// Whether a shot of <paramref name="type"/> has an open landing that would score — see the class doc for exactly
        /// what that asks and what it leaves out.
        /// </summary>
        /// <param name="map">The field as it stands.</param>
        /// <param name="type">The colour of the round being asked about. Only an ordinary round asks: a wildcard takes the
        /// colour of whatever it lands beside and a cutter takes a storey, so neither needs a match.</param>
        /// <param name="minimumGroup">The match rule's group size, the shot itself included
        /// (<c>BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE</c>, which lives in the physics library this one cannot see).</param>
        public bool HasScoringLanding(BallsMap map, BallType type, int minimumGroup)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            Prepare(size);
            FloodOpenSpace(cells, size);

            Span<XZLevel> touching = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        if (!_open[x, z, level]) continue;

                        XZLevel cell = new(x, z, level);
                        int count = BallsMap.FillNeighboringCells(cell, size, touching);

                        //A pocket with nothing beside it is open air the shot would sail through; one beside the type it is
                        //asked about may complete a group; one beside a special that goes off or turns on a landing scores
                        bool beside = false, besideType = false;
                        for (int i = 0; i < count; i++)
                        {
                            StaticBall ball = cells[touching[i].X, touching[i].Z, touching[i].Level];
                            if (ball == null) continue;

                            beside = true;
                            if (BallKinds.Matchable(ball.Kind)) besideType |= ball.Type == type;
                            else if (!IsInert(ball.Kind)) return true;
                        }

                        if (!beside || !besideType) continue;

                        if (map.CountGroupIfPlaced(cell, type, size, _stamp, NextMark(), _walk) >= minimumGroup) return true;
                    }

            return false;
        }

        /// <summary>
        /// The kinds a landing beside does nothing to: a rock never leaves but as collateral, ice thaws only when a GROUP is
        /// cleared beside it (#329), and buckshot only pours when what holds it is cut (#257). Every other kind that is not
        /// matchable answers a landing beside it (glass takes the colour, a bomb, a zap and an acid go off).
        /// </summary>
        public static bool IsInert(BallKind kind) =>
            kind == BallKind.Rock || kind == BallKind.Frozen || kind == BallKind.Buckshot;

        /// <summary>The buffers, sized to the field: regrown only when a larger field arrives, cleared every call.</summary>
        private void Prepare(XZLevel size)
        {
            if (_open == null || _open.GetLength(0) < size.X || _open.GetLength(1) < size.Z || _open.GetLength(2) < size.Level)
            {
                _open = new bool[size.X, size.Z, size.Level];
                _stamp = new byte[size.X, size.Z, size.Level];
                _mark = 0;
            }
            else Array.Clear(_open);
        }

        /// <summary>
        /// A fresh mark for one group walk over the shared stamp buffer. A byte runs out after 255 walks, and then the buffer
        /// is wiped and the marks start again, so no cell ever carries a mark a later walk could take for its own.
        /// </summary>
        private byte NextMark()
        {
            if (_mark == byte.MaxValue)
            {
                Array.Clear(_stamp);
                _mark = 0;
            }

            return ++_mark;
        }

        /// <summary>
        /// Marks every empty cell joined to the field's walls or floor through empty cells. A shot arrives from outside, so
        /// the flood starts at the walls and the floor, and never at the top, which is the glass the cluster hangs from.
        /// </summary>
        private void FloodOpenSpace(StaticBall[,,] cells, XZLevel size)
        {
            _flood.Clear();

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        bool wall = x == 0 || z == 0 || x == size.X - 1 || z == size.Z - 1 || level == 0;
                        if (!wall || cells[x, z, level] != null) continue;

                        _open[x, z, level] = true;
                        _flood.Push(new XZLevel(x, z, level));
                    }

            Span<XZLevel> next = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            while (_flood.Count > 0)
            {
                XZLevel cell = _flood.Pop();
                int count = BallsMap.FillNeighboringCells(cell, size, next);

                for (int i = 0; i < count; i++)
                {
                    XZLevel neighbour = next[i];
                    if (_open[neighbour.X, neighbour.Z, neighbour.Level]) continue;
                    if (cells[neighbour.X, neighbour.Z, neighbour.Level] != null) continue;

                    _open[neighbour.X, neighbour.Z, neighbour.Level] = true;
                    _flood.Push(neighbour);
                }
            }
        }
    }
}
