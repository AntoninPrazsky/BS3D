using Prazsky.BS3D.GameStructure.DataBags;
using System;
using System.Collections.Generic;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// <b>Whether a shot of one colour has anywhere to score</b> (#845), asked of the logical map: the Swap chip flashes
    /// when the round in the bore has nowhere, a streak is running and the round behind it has somewhere.
    /// <para>
    /// <b>The question, in the terms that bound it</b> (BestPractices.md §10): is there an empty cell a shot can come to
    /// rest in (a landing, below), touching a ball, where a ball of this colour would either complete a group of
    /// <c>minimumGroup</c> by the match rule's own walk (<see cref="BallsMap.CountGroupIfPlaced"/>) or land beside a ball
    /// a landing sets off or turns? It does <b>not</b> ask whether the gun can be laid on that cell or a shot
    /// can thread its way there: the elevation clamps, the crates a shot banks off, the wells that bend it and the gaps
    /// between courses too narrow for a ball are all left out, and every one of them could only take landings away. So
    /// <b>"none" is certain and "some" is only possible</b>, which is the direction a hint has to err in: a chip that
    /// flashed while a match was there to be had would teach the player to spend the Swap on nothing.
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>A landing is an open cell</b>, what <c>Tools/LevelGen</c>'s <c>ClearProbe</c> floods: the
    /// empty cells joined to the field's walls or floor (never its top, the glass) through empty neighbours. A flying
    /// ball's centre never enters the region of an occupied cell, so it travels only through empty cells that touch, and
    /// the placement (<c>ShotPlacement</c>: the free cell beside the ball struck nearest the contact) puts a shot struck
    /// from an open cell's region into that cell, or into a free cell it shares with the ball struck, which is open too.
    /// The path slips between courses where no ball could pass (<c>docs/formats-and-tools/levelgen-gates.md</c>), which
    /// errs the safe way.</description></item>
    /// <item><description><b>Or any free cell the placement can give a ball on a wall or the floor</b>
    /// (<see cref="IsStruckIntoFromOutside"/>): such a ball can be struck from outside the field, where there are no cells,
    /// so the placement puts the shot in whichever free cell beside it is nearest, sealed or not, and when there is none,
    /// in the nearest free cell beside its neighbours (<see cref="BallsMap.TryFindEmptyCellNextTo"/>, then
    /// <see cref="BallsMap.TryFindEmptyCellInSecondRing"/>). Every one of them counts, wherever the contact was.
    /// On the shipped levels at their start the rule shuts out the hold of a reveal level and little else: Chest's white
    /// lines a sealed hold, so a white round there has nowhere to score (<c>MatchReachTests</c>).</description></item>
    /// <item><description><b>A special a landing sets off or turns counts as somewhere to score</b> without asking what it
    /// would do: glass takes the shot's colour and may complete a group through itself, and a bomb, a zap and an acid go off
    /// and take balls, which the keeper pays as destroyed. Only the kinds a landing does nothing to are walked past
    /// (<see cref="IsInert"/>: the rock, the ice, the buckshot), so a kind added later counts as scoring until somebody
    /// says otherwise, the safe default for a hint that must not cry wolf.</description></item>
    /// </list>
    /// <para>
    /// <b>Cheap enough for the frame a landing lands on</b>, because that is when the hint asks again, for both rounds it
    /// reads. A flood of the whole open space before each answer cost 1.4 ms a field on average and 3.1 ms at most over the
    /// shipped levels (Release), twice a landing. So the field is <b>surveyed</b> once per state (one pass listing every
    /// matchable ball by colour and every special, kept until <see cref="BallsMap.Revision"/> or the map changes), the
    /// match rule's walk is asked first of the empty cells beside the colour's own balls, and only a cell that would score
    /// is asked whether a shot can land in it: by a search that heads for the floor, ends on reaching a wall, the floor or a cell
    /// whose answer is known, and gives every cell it visited its answer, since they are all one piece of space. Measured
    /// the same way (<c>MatchReachTests</c> prints it): 0.06 ms for the first ask on a field, survey included, 0.9 ms at
    /// most (the first call of the run), and about 30 microseconds for an ask on a surveyed field. The buffers are kept
    /// and regrown only for a larger field, so a warm call allocates nothing.
    /// </para>
    /// </summary>
    public sealed class MatchReach
    {
        //What is known of each empty cell's reach in the surveyed field, flat (index (level * X + x) * Z + z): not asked yet,
        //open, sealed, or being searched now
        private const byte UNKNOWN = 0, OPEN = 1, SEALED = 2, SEARCHING = 3;
        private byte[] _reach = Array.Empty<byte>();
        private XZLevel _size;

        //The match rule's walk marks the cells it has visited in this buffer, a fresh mark per walk
        private byte[,,] _stamp;
        private byte _mark;

        //The survey: every matchable ball's cell by colour (index = the BallType's value), and every special a landing
        //would answer; kept until the field changes
        private readonly List<XZLevel>[] _byType = new List<XZLevel>[byte.MaxValue + 1];
        private readonly List<XZLevel> _specials = new(16);

        private BallsMap _surveyed;
        private int _surveyedRevision;

        private readonly Stack<XZLevel> _search = new(64);
        private readonly List<int> _visited = new(64);
        private readonly Queue<XZLevel> _walk = new(64);

        /// <summary>
        /// Whether a shot of <paramref name="type"/> has an open landing that would score — see the class doc for exactly
        /// what that asks and what it leaves out.
        /// </summary>
        /// <param name="map">The field as it stands. Only its own writes move <see cref="BallsMap.Revision"/>, which is what
        /// tells this the survey it keeps is stale, so the map must not be changed through the array it hands out.</param>
        /// <param name="type">The colour of the round being asked about. Only an ordinary round asks: a wildcard takes the
        /// colour of whatever it lands beside and a cutter takes a storey, so neither needs a match.</param>
        /// <param name="minimumGroup">The match rule's group size, the shot itself included
        /// (<c>BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE</c>, which lives in the physics library this one cannot see).</param>
        public bool HasScoringLanding(BallsMap map, BallType type, int minimumGroup)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            Survey(map);

            StaticBall[,,] cells = map.GetStaticBallsArray();
            Span<XZLevel> beside = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            //A special a landing would answer, with a landing beside it: every colour has somewhere
            for (int s = 0; s < _specials.Count; s++)
            {
                int count = BallsMap.FillNeighboringCells(_specials[s], _size, beside);
                for (int i = 0; i < count; i++)
                    if (cells[beside[i].X, beside[i].Z, beside[i].Level] == null && CanLandIn(cells, beside[i])) return true;
            }

            List<XZLevel> balls = _byType[(byte)type];
            if (balls == null) return false;

            //Every empty cell beside a ball of this colour: the match rule's walk first, which for a lone ball is a cell or
            //two, and the reach only of a cell that would score. A cell already known to be sealed is passed over unless
            //the placement can still put a shot struck from outside the field into it
            for (int b = 0; b < balls.Count; b++)
            {
                int count = BallsMap.FillNeighboringCells(balls[b], _size, beside);
                for (int i = 0; i < count; i++)
                {
                    XZLevel cell = beside[i];
                    if (cells[cell.X, cell.Z, cell.Level] != null) continue;
                    if (_reach[Index(cell)] == SEALED && !IsStruckIntoFromOutside(cells, cell)) continue;

                    if (map.CountGroupIfPlaced(cell, type, _size, _stamp, NextMark(), _walk) >= minimumGroup && CanLandIn(cells, cell))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a shot can come to rest in the empty cell <paramref name="cell"/>: it is open
        /// (<see cref="IsOpen"/>), or the placement can put a shot struck from outside the field into it
        /// (<see cref="IsStruckIntoFromOutside"/>).
        /// </summary>
        private bool CanLandIn(StaticBall[,,] cells, XZLevel cell) => IsOpen(cells, cell) || IsStruckIntoFromOutside(cells, cell);

        /// <summary>
        /// Whether the placement can put a shot that struck a ball on a wall or the floor into the empty cell
        /// <paramref name="cell"/>, open or sealed. Such a ball can be struck from outside the field, where there are no
        /// cells, and the placement (<see cref="BallsMap.TryFindEmptyCellNextTo"/>) takes the free cell beside it nearest the
        /// contact, so every free cell beside it is a landing; one whose every neighbour is a ball sends the shot one ring
        /// further out (<see cref="BallsMap.TryFindEmptyCellInSecondRing"/>), to a free cell beside one of its neighbours.
        /// Asked only of a cell the flood found sealed and a ball would score in, so its few dozen reads are rare.
        /// </summary>
        private bool IsStruckIntoFromOutside(StaticBall[,,] cells, XZLevel cell)
        {
            Span<XZLevel> around = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];
            Span<XZLevel> further = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];
            Span<XZLevel> ring = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            int count = BallsMap.FillNeighboringCells(cell, _size, around);
            for (int i = 0; i < count; i++)
            {
                XZLevel ball = around[i];
                if (cells[ball.X, ball.Z, ball.Level] == null) continue;

                //Beside a ball on a wall or the floor: the first ring
                if (OnBoundary(ball)) return true;

                //Beside a ball that touches one on a wall or the floor whose own ring is full: the second ring
                int touching = BallsMap.FillNeighboringCells(ball, _size, further);
                for (int j = 0; j < touching; j++)
                {
                    XZLevel wall = further[j];
                    if (cells[wall.X, wall.Z, wall.Level] == null || !OnBoundary(wall)) continue;
                    if (RingIsFull(cells, wall, ring)) return true;
                }
            }

            return false;
        }

        /// <summary>Whether every cell beside <paramref name="ball"/> holds a ball, read into <paramref name="ring"/>.</summary>
        private bool RingIsFull(StaticBall[,,] cells, XZLevel ball, Span<XZLevel> ring)
        {
            int count = BallsMap.FillNeighboringCells(ball, _size, ring);
            for (int i = 0; i < count; i++)
                if (cells[ring[i].X, ring[i].Z, ring[i].Level] == null) return false;
            return true;
        }

        /// <summary>
        /// The kinds a landing beside does nothing to: a rock never leaves but as collateral, ice thaws only when a GROUP is
        /// cleared beside it (#329), and buckshot only pours when what holds it is cut (#257). Every other kind that is not
        /// matchable answers a landing beside it (glass takes the colour, a bomb, a zap and an acid go off).
        /// </summary>
        public static bool IsInert(BallKind kind) =>
            kind == BallKind.Rock || kind == BallKind.Frozen || kind == BallKind.Buckshot;

        /// <summary>
        /// Lists every matchable ball by colour and every special a landing would answer, and forgets every cell's reach,
        /// once per state of the field: nothing is done while <paramref name="map"/> and its <see cref="BallsMap.Revision"/>
        /// are the ones last surveyed.
        /// </summary>
        private void Survey(BallsMap map)
        {
            if (ReferenceEquals(map, _surveyed) && map.Revision == _surveyedRevision) return;

            _surveyed = map;
            _surveyedRevision = map.Revision;

            StaticBall[,,] cells = map.GetStaticBallsArray();
            _size = map.GetStaticBallsArraySize();
            Prepare(_size);

            for (int i = 0; i < _byType.Length; i++) _byType[i]?.Clear();
            _specials.Clear();

            for (int level = 0; level < _size.Level; level++)
                for (int x = 0; x < _size.X; x++)
                    for (int z = 0; z < _size.Z; z++)
                    {
                        StaticBall ball = cells[x, z, level];
                        if (ball == null) continue;

                        if (BallKinds.Matchable(ball.Kind)) (_byType[(byte)ball.Type] ??= new List<XZLevel>(64)).Add(new XZLevel(x, z, level));
                        else if (!IsInert(ball.Kind)) _specials.Add(new XZLevel(x, z, level));
                    }
        }

        /// <summary>
        /// Whether the empty cell <paramref name="start"/> is joined to a wall or the floor through empty cells. A search that
        /// tries the cells below first (under a hanging cluster the floor is the nearest way out) and ends on reaching a
        /// wall, the floor or a cell already known to be open; every cell it visited is one piece of space with the start,
        /// so all of them take its answer, and no cell is searched twice in one state of the field.
        /// </summary>
        private bool IsOpen(StaticBall[,,] cells, XZLevel start)
        {
            int first = Index(start);
            if (_reach[first] == OPEN || _reach[first] == SEALED) return _reach[first] == OPEN;

            Span<XZLevel> next = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];
            _search.Clear();
            _visited.Clear();

            _reach[first] = SEARCHING;
            _visited.Add(first);
            _search.Push(start);

            //Sealed until a way out is found: a search that runs out of cells, or meets a cell already known to be sealed
            //(the same piece of space, searched to its end before), has none
            byte answer = SEALED;
            bool done = false;
            while (_search.Count > 0 && !done)
            {
                XZLevel cell = _search.Pop();
                if (OnBoundary(cell))
                {
                    answer = OPEN;
                    break;
                }

                int count = BallsMap.FillNeighboringCells(cell, _size, next);

                //Pushed level and higher first, then lower, so the lower cells are taken next
                for (int pass = 0; pass < 2 && !done; pass++)
                    for (int i = 0; i < count && !done; i++)
                    {
                        XZLevel neighbour = next[i];
                        if ((neighbour.Level < cell.Level) != (pass == 1)) continue;
                        if (cells[neighbour.X, neighbour.Z, neighbour.Level] != null) continue;

                        int index = Index(neighbour);
                        byte known = _reach[index];
                        if (known == OPEN || known == SEALED)
                        {
                            answer = known;
                            done = true;
                        }
                        else if (known == UNKNOWN)
                        {
                            _reach[index] = SEARCHING;
                            _visited.Add(index);
                            _search.Push(neighbour);
                        }
                    }
            }

            for (int i = 0; i < _visited.Count; i++) _reach[_visited[i]] = answer;

            return answer == OPEN;
        }

        /// <summary>Whether <paramref name="cell"/> is on a wall or the floor of the field, which a shot can reach from outside.</summary>
        private bool OnBoundary(XZLevel cell) =>
            cell.X == 0 || cell.Z == 0 || cell.X == _size.X - 1 || cell.Z == _size.Z - 1 || cell.Level == 0;

        /// <summary>The reach buffer's index of <paramref name="cell"/> in the surveyed field.</summary>
        private int Index(XZLevel cell) => (cell.Level * _size.X + cell.X) * _size.Z + cell.Z;

        /// <summary>The buffers, sized to the field: regrown only when a larger field arrives, the reach forgotten every survey.</summary>
        private void Prepare(XZLevel size)
        {
            int cells = size.X * size.Z * size.Level;
            if (_reach.Length < cells) _reach = new byte[cells];
            else Array.Clear(_reach, 0, cells);

            if (_stamp == null || _stamp.GetLength(0) < size.X || _stamp.GetLength(1) < size.Z || _stamp.GetLength(2) < size.Level)
            {
                _stamp = new byte[Math.Max(size.X, _stamp?.GetLength(0) ?? 0), Math.Max(size.Z, _stamp?.GetLength(1) ?? 0),
                    Math.Max(size.Level, _stamp?.GetLength(2) ?? 0)];
                _mark = 0;
            }
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
    }
}
