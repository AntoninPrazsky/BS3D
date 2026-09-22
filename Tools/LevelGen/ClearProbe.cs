using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using System;
using System.Collections.Generic;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The gate that asks how FEW shots empty the field</b> — #458, and the one question about a pattern
    /// level that every other check here turns its back on.
    /// <para>
    /// <see cref="Program.DropTest"/> asks what one shot is worth and refuses a level where one shot is worth
    /// everything. <c>WorstAnchorLoad</c> asks what the glass carries afterwards. Both read a single cut. The
    /// fault they cannot see is a level that is not taken by one shot and is taken by <i>two</i>:
    /// <b>Saturn</b> shipped as a planet in two meridian halves, so its anchor course carried exactly two
    /// colours, and one blue match and one green match took all 377 balls off a 36-shot budget. Neither number
    /// the tool printed was wrong — the best single shot took 33 %, the anchor load was the block's boldest and inside the pack's spread — and the
    /// level was over before the design's own lesson could be reached.
    /// </para>
    /// <para>
    /// <b>So this plays the level instead of reading it</b>, on the lattice rather than in the simulation
    /// (which is <see cref="SagProbe"/>'s job and costs minutes). A move is a landing: an empty cell a shot can
    /// get to, a colour, the glass that landing would colour, and the group it completes — then whatever that
    /// group was holding up falls with it. The field is <b>clear</b> when no removable ball is left standing,
    /// which is <c>GameplayScreen.CheckLevelCleared</c>'s own condition (a field of nothing but rock is
    /// cleared). The answer is the fewest moves that get there.
    /// </para>
    /// <para>
    /// <b>The search is exhaustive as deep as it reports</b> (<see cref="PROVEN_SHOTS"/>), so what the gate
    /// refuses, it has proved — and then plays the line it found back through <see cref="BallsMap"/> to be sure
    /// the game agrees. Anything deeper is reported from a beam and is honestly labelled an upper bound. Most
    /// levels never reach the search at all, because of the floor below.
    /// </para>
    /// <para>
    /// <b>⚠ The refusal is NOT the shot count alone, and that is the first thing the pack taught this file.</b>
    /// Seventeen shipped levels empty in two shots; most of them are cuts through a thin support, which is the
    /// game's own lesson rather than a fault. What Saturn does and they do not is <i>match</i> most of the field
    /// away in those two shots. Both halves are stated, with what was measured, at
    /// <see cref="MINIMUM_CLEAR_SHOTS"/> and <see cref="CHEAP_CLEAR_MATCHED_PERCENT"/>.
    /// </para>
    /// <para>
    /// <b>⚠ THE FLOOR IS THE CHEAP HALF AND IT IS SOUND: a ball on the top course can only ever leave in a
    /// matched group.</b> The disconnection walk seeds from every cell of the field's top level, so a ball up
    /// there is never an orphan — nothing the player does to the rest of the level can drop it. One shot
    /// matches one group, and a group is one colour, so <b>the level cannot be emptied in fewer shots than
    /// there are colours standing on its anchor course</b>. The campaign's own design rule ("every colour
    /// stands on the top level") therefore puts most levels above the refusal before a single move is played,
    /// and Saturn's two-colour cap is the fault stated as a number. The floor is dropped for a level with a
    /// removable ball that is <i>not</i> matchable on that course (glass, a bomb): both leave by paths this
    /// model does not play, and the gate only ever takes the conservative half of a question.
    /// </para>
    /// <para>
    /// <b>What is NOT played, and why the gate is still sound.</b> Only the ordinary landing is modelled: the
    /// match, the glass a landing colours (<see cref="BallsMap.ColourTransparentGroup"/>, whole bodies since
    /// #344) and the orphans. A blast, a zap, an acid shaft, a thaw and an infection are not — they live in
    /// <c>BallsConstraintsBuilder</c> over a <c>PhysicsBall</c> array, and reproducing them here would be a
    /// second copy of the rule, which is the one thing this repository refuses. Nor is a <b>setup shot</b>
    /// modelled: a landing that completes no group parks a ball on the cluster, and every one of those is a
    /// shot spent, so no sequence this probe skips can be <i>shorter</i> than one it plays. Both omissions
    /// point the same way — <b>the probe can only ever over-state how many shots a level needs</b>, so a level
    /// it refuses is really that cheap, and the five levels holding a bomb or a zap are the ones it can
    /// miss. That is stated rather than fixed, on this tool's standing rule that a gate which is wrong in the
    /// refusing direction is worse than no gate at all.
    /// </para>
    /// <para>
    /// <b>⚠ A landing has to be somewhere a shot could arrive</b>, and that is the second conservative half. A
    /// sealed pocket inside the cluster is not offered as a landing: the empty cells are flooded from the
    /// field's own walls and floor (never from the top, which is the glass), and only a pocket that flood
    /// reaches can be shot into. It is a weaker statement than <c>ShotPlacement</c>'s sweep, which is what the
    /// sag probe fires through, and it is deliberately the cheap end of it — an unreachable landing would
    /// make a clear look cheaper than it is, which is the one direction this gate must not err in.
    /// </para>
    /// </summary>
    internal sealed class ClearProbe
    {
        /// <summary>
        /// <b>A clear in fewer shots than this is a CHEAP clear</b> — half the refusal, and the half that was
        /// obvious. The other half is <see cref="CHEAP_CLEAR_MATCHED_PERCENT"/>, and it is there because the
        /// shot count alone turned out to refuse a sixth of the shipped campaign.
        /// <para>
        /// <b>⚠ WHAT THE PACK MEASURED, and it was not what #458 assumed.</b> Sixteen shipped levels besides
        /// Saturn empty in <i>two</i> matched shots and nine more in three — and they are not faults: a field
        /// hangs off its top course alone, so the cheapest clear is always "cut what holds it", and half the
        /// Coil's block is <i>designed</i> to be cut that way (Pendant is a weight on four ropes; taking two
        /// rope tops of eight balls drops all 147). A gate refusing every clear under three shots would have
        /// sent seventeen levels back, which is a campaign rewrite rather than a gate.
        /// </para>
        /// <para>
        /// <b>⚠ THREE UNTIL #474, AND FOUR IS THE OWNER'S RULING RATHER THAN A MEASUREMENT.</b> #458 drew the
        /// line where the only reported fault was; the table it printed then showed nine more levels emptying
        /// in three, four of them matching most of themselves away — Horn <b>90 %</b>, Trophy 68, Onion 64,
        /// Lean 50.1 — and whether that is the same fault one shot further out is a question about what those
        /// chapters are for, which is not the tool's to answer. Asked, the owner took the widest of the three
        /// options offered: the rule keeps its percentage and reaches one shot further. The four levels it
        /// then refused were redrawn the way Saturn was, by cutting their plates into sectors — a colouring,
        /// never a shape.
        /// </para>
        /// </summary>
        internal const int MINIMUM_CLEAR_SHOTS = 4;

        /// <summary>
        /// <b>And the other half: how much of the field the cheap clear takes by MATCHING rather than by
        /// orphaning.</b> This is the sentence in #458 turned into a number — <i>"the two easiest shots on the
        /// field end the level"</i>. A group you cannot miss is a group that is a large part of what is hanging
        /// there; a rope top of eight balls under the glass is a shot that has to be threaded, and the level it
        /// drops was drawn to be dropped that way.
        /// <para>
        /// <b>Measured over the pack, and the line is drawn where the gap is.</b> Of the seventeen levels that
        /// empty in two shots, Saturn matches <b>61 %</b> of the field away (its two hemispheres, 114 and 119
        /// balls) and every other one is at 39 % or below — Crane 39, Minaret 29, Ghost 21, and the median of the
        /// sixteen 12.5.
        /// Fifty is the middle of that gap and it refuses exactly the design the owner sent back.
        /// </para>
        /// <para>
        /// <b>⚠ The comparison is exact and not on the printed percentage</b>, and #474 is the reason:
        /// <c>Lean</c> matches 258 of 515 away, 50.09 %, which the integer percentage in the log truncates to
        /// 50 — so a level genuinely over the line would have passed on a rounding. The percentage is for
        /// reading; the refusal multiplies out.
        /// </para>
        /// </summary>
        internal const int CHEAP_CLEAR_MATCHED_PERCENT = 50;

        /// <summary>
        /// How deep the exhaustive search runs, and therefore how deep a number the log may call a minimum.
        /// <b>One shot past the shots the refusal looks at, and that shot is the pack's own floor rather than
        /// slack</b>: nine shipped levels empty in exactly three (see the table in
        /// <c>docs/formats-and-tools.md</c>), so a level sitting ON the floor is worth naming rather than
        /// reporting as "more than two". Past this depth the number comes from <see cref="Beam"/> and is an
        /// upper bound, which is said where it is printed.
        /// </summary>
        private const int PROVEN_SHOTS = 3;

        /// <summary>How many states the beam carries forward per shot, and how many shots it plays before it
        /// gives up. The beam reports; it never refuses, so its width is a matter of how good a sequence the
        /// log is worth rather than of correctness.</summary>
        private const int BEAM_WIDTH = 6;

        private const int BEAM_SHOTS = 120;

        /// <summary>What the probe found. <paramref name="Shortest"/> is <see cref="int.MaxValue"/> when no
        /// clear was found at all; <paramref name="Exact"/> says whether it is a proved minimum (the
        /// exhaustive search) or the best the beam managed (an upper bound); <paramref name="Sequence"/> is the
        /// line of play itself, which is what an author has to see to act on a refusal.</summary>
        internal readonly record struct Reading(
            int Shortest, bool Exact, int Floor, int Removable, int Matched, bool Beamed, bool Replayed,
            string Sequence)
        {
            /// <summary>The gate. A proved minimum under <see cref="MINIMUM_CLEAR_SHOTS"/> is the refusal, and
            /// nothing else is: a beam number is an upper bound found by one line of play, and the beam is
            /// never asked anything the exhaustive search has not already answered.
            /// <para>
            /// <b>⚠ And the line has to have been played back through <see cref="BallsMap"/> first</b> — see
            /// <see cref="Replay"/>. A refusal is a design sent back, so it is not made on this file's own
            /// model of the game: it is made on the game's own map code agreeing that those shots empty that
            /// level. A disagreement is a fault in this probe and says so in the log.
            /// </para>
            /// </summary>
            internal bool TooCheap => Exact && Shortest < MINIMUM_CLEAR_SHOTS
                                      && Matched * 100 > Removable * CHEAP_CLEAR_MATCHED_PERCENT && Replayed;

            /// <summary>How much of the field the cheap clear takes by MATCHING rather than by orphaning — the
            /// figure that separates a level whose mass is its own two groups from one whose thin support was
            /// cut. See <see cref="MINIMUM_CLEAR_SHOTS"/> for what the pack measured.</summary>
            internal int MatchedPercent => Removable == 0 ? 0 : Matched * 100 / Removable;

            /// <summary>The line the validator prints. It says which of the two things it knows: a minimum it
            /// proved, or a floor under a level it stopped searching — never a number without its standing.
            /// </summary>
            internal string Describe()
            {
                if (Exact)
                {
                    string how = Replayed
                        ? string.Empty
                        : " ⚠ BUT THE REPLAY THROUGH BallsMap DID NOT AGREE - the probe is wrong, not the level";

                    return $"{Shortest} shot{(Shortest == 1 ? string.Empty : "s")} empty the field, matching"
                           + $" {Matched} of {Removable} ({MatchedPercent} %) and orphaning the rest"
                           + $" [{Sequence}]{how}";
                }

                string floor = Floor > PROVEN_SHOTS
                    ? $"no fewer than {Floor} ({Floor} colours stand on the anchor course)"
                    : $"no fewer than {PROVEN_SHOTS + 1} (no shorter sequence exists)";

                if (!Beamed) return floor;

                return floor + (Shortest == int.MaxValue
                    ? ", and no line the beam played cleared it"
                    : $"; a beam line cleared it in {Shortest}");
            }
        }

        /// <summary>One shot: where it lands, what colour it is, and everything it takes off the cluster
        /// (the group; the orphans follow from the map and are not listed).</summary>
        private readonly record struct Move(int Landing, byte Colour, List<int> Cells);

        private readonly int _n;
        private readonly int _levels;
        private readonly int _sizeX;
        private readonly int _sizeZ;

        //The lattice, flattened once: which cells touch which. It is BallsMap.FillNeighboringCells that
        //answers, never a rule written here — the parity shift is the map's business and a second copy of it
        //is a second thing to get wrong.
        private readonly int[] _neighbourStart;
        private readonly int[] _neighbourCell;

        //What each cell holds. Colour and kind never change during a search, and that is a property of the
        //model rather than a shortcut: the only thing that recolours a ball is a glass landing, and a glass
        //landing that fails to match is a setup shot, which is not played (see the class doc).
        private readonly byte[] _type;
        private readonly bool[] _matchable;
        private readonly bool[] _glass;
        private readonly bool[] _removable;
        private readonly bool[] _openSeed;
        private readonly int[] _topCells;
        private readonly byte[] _colours;
        private readonly ulong[] _zobrist;

        private readonly bool[] _present;
        private readonly int _removableCount;

        //Scratch, one set for the whole run: a node generates its whole move list before it recurses, so a
        //child can have the marks back.
        private readonly int[] _mark;
        private readonly int[] _stack;
        private readonly bool[] _open;
        private readonly Dictionary<ulong, int> _exhausted = new();
        private int _generation;

        private ClearProbe(BallPositionTypes data)
        {
            BallsMap map = new(data);
            StaticBall[,,] array = map.GetStaticBallsArray();

            _sizeX = map.StageSizeX;
            _sizeZ = map.StageSizeZ;
            _levels = map.Levels;
            _n = _sizeX * _sizeZ * _levels;

            _type = new byte[_n];
            _matchable = new bool[_n];
            _glass = new bool[_n];
            _removable = new bool[_n];
            _present = new bool[_n];
            _openSeed = new bool[_n];
            _mark = new int[_n];
            _open = new bool[_n];
            _stack = new int[_n];
            _zobrist = new ulong[_n];
            _neighbourStart = new int[_n + 1];

            //Deterministic keys: this tool is run in front of a commit and two runs of it have to print the
            //same thing. The seed is arbitrary and the value only has to be well mixed.
            Random keys = new(458);
            for (int i = 0; i < _n; i++) _zobrist[i] = (ulong)keys.NextInt64();

            XZLevel size = new(_sizeX, _sizeZ, _levels);
            List<int> neighbours = new(_n * 8);
            Span<XZLevel> found = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];
            HashSet<byte> colours = new();
            List<int> top = new();

            for (int level = 0; level < _levels; level++)
                for (int x = 0; x < _sizeX; x++)
                    for (int z = 0; z < _sizeZ; z++)
                    {
                        int index = Index(x, z, level);
                        _neighbourStart[index] = neighbours.Count;

                        int count = BallsMap.FillNeighboringCells(new XZLevel(x, z, level), size, found);
                        for (int i = 0; i < count; i++)
                            neighbours.Add(Index(found[i].X, found[i].Z, found[i].Level));

                        //A shot arrives from OUTSIDE, so the flood that decides which pockets can be landed in
                        //starts at the field's walls and its floor — and never at its lid, which is the glass
                        //plate the cluster hangs from.
                        _openSeed[index] = x == 0 || z == 0 || x == _sizeX - 1 || z == _sizeZ - 1 || level == 0;

                        if (level == _levels - 1) top.Add(index);

                        StaticBall ball = array[x, z, level];
                        if (ball == null) continue;

                        _present[index] = true;
                        _type[index] = (byte)ball.Type;
                        _matchable[index] = BallKinds.Matchable(ball.Kind);
                        _glass[index] = ball.Kind == BallKind.Transparent;
                        _removable[index] = BallKinds.Removable(ball.Kind);

                        if (_removable[index]) _removableCount++;
                        if (_matchable[index]) colours.Add((byte)ball.Type);
                    }

            _neighbourStart[_n] = neighbours.Count;
            _neighbourCell = neighbours.ToArray();
            _topCells = top.ToArray();
            _colours = new byte[colours.Count];
            colours.CopyTo(_colours);
        }

        private int Index(int x, int z, int level) => (level * _sizeX + x) * _sizeZ + z;

        /// <summary>
        /// Plays the level and answers with the fewest shots that emptied it. <paramref name="deep"/> asks for
        /// the beam as well, which is the only way to get a number out of a level the exhaustive search
        /// (rightly) gives up on — it costs about a tenth of a second a level and says nothing the gate needs,
        /// so it is opt-in.
        /// </summary>
        /// <param name="afterMove">
        /// Called with the field as <see cref="BallsMap"/> has it after each cut of the line that cleared the
        /// level, for a caller that wants to ask something of the level <b>as it is played</b> rather than as
        /// it was authored — #514's arrival report is the first, since a landing blocked on the intact cluster
        /// is very often reachable once a cut has opened the line to it. Null for the gate, which is what every
        /// other caller passes, and it is <b>only</b> called when a line was found and is being replayed
        /// through the library, so a level the search gives up on calls it not at all.
        /// </param>
        internal static Reading Measure(BallPositionTypes data, bool deep, Action<BallsMap> afterMove = null)
        {
            ClearProbe probe = new(data);
            return probe.Run(data, deep, afterMove);
        }

        private Reading Run(BallPositionTypes data, bool deep, Action<BallsMap> afterMove)
        {
            int floor = AnchorColourFloor(_present);
            List<Move> line = new();

            //The floor answers most of the pack outright and costs one pass over the top course. It is a
            //LOWER bound, so a level standing above the search's depth on it alone needs no search: what the
            //search would do is confirm what a sound argument has already settled.
            //
            //ITERATIVE DEEPENING rather than one search to the full depth, because the answer wanted is the
            //SHORTEST clear: the first budget that finds one has found the minimum, and the shallow passes it
            //repeats are the cheap ones.
            for (int budget = Math.Max(floor, 1); budget <= PROVEN_SHOTS; budget++)
            {
                line.Clear();
                if (!Search(_present, Hash(_present), budget, line)) continue;

                bool replayed = Replay(data, line, afterMove);
                int matched = 0;
                foreach (Move shot in line) matched += shot.Cells.Count;

                return new Reading(budget, true, floor, _removableCount, matched, false, replayed, Narrate(line));
            }

            if (!deep) return new Reading(int.MaxValue, false, floor, _removableCount, 0, false, false, null);

            return new Reading(Beam(), false, floor, _removableCount, 0, true, false, null);
        }

        /// <summary>The line of play in the log's own words: the colour, and the cell the shot lands in.</summary>
        private string Narrate(List<Move> line)
        {
            string[] shots = new string[line.Count];

            for (int i = 0; i < line.Count; i++)
            {
                int cell = line[i].Landing;
                int level = cell / (_sizeX * _sizeZ);
                int x = cell / _sizeZ % _sizeX;
                int z = cell % _sizeZ;

                shots[i] = $"{(BallType)line[i].Colour} at {x},{z},{level} takes {line[i].Cells.Count}";
            }

            return string.Join(" then ", shots);
        }

        /// <summary>
        /// <b>Plays the found line back through <see cref="BallsMap"/> itself and asks whether the field really
        /// is empty</b> — the game's own map code, on the game's own landing order: put the ball, colour the
        /// glass it touches, count the group, take it, then take everything hanging on nothing
        /// (<c>BallContactEventHandler</c>, and <c>ResolveDisconnected</c> behind it).
        /// <para>
        /// It exists because a refusal is a design sent back to be redrawn, and this file models the game in
        /// its own flattened lattice for speed. A model is a second copy of a rule, and the one thing worth
        /// doing with a second copy is checking it against the first — <b>every refusal this gate makes has
        /// been confirmed by the library before it is printed</b>, and a disagreement is reported as a fault in
        /// the tool rather than quietly refusing a level that plays perfectly well.
        /// </para>
        /// </summary>
        private bool Replay(BallPositionTypes data, List<Move> line, Action<BallsMap> afterMove)
        {
            BallsMap map = new(data);
            List<XZLevel> coloured = new();

            foreach (Move move in line)
            {
                int cell = move.Landing;
                byte level = (byte)(cell / (_sizeX * _sizeZ));
                byte x = (byte)(cell / _sizeZ % _sizeX);
                byte z = (byte)(cell % _sizeZ);
                XZLevel landing = new(x, z, level);

                map.PutBallAt(x, z, level, (BallType)move.Colour);
                map.ColourTransparentGroup(landing, (BallType)move.Colour, coloured);

                List<XZLevel> group = map.GetConnectedSameTypeCells(landing);
                if (group.Count < BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE) return false;

                foreach (XZLevel member in group) map.RemoveBallAt((byte)member.X, (byte)member.Z, (byte)member.Level);
                foreach (XZLevel orphan in map.GetCellsDisconnectedFromCeiling())
                    map.RemoveBallAt((byte)orphan.X, (byte)orphan.Z, (byte)orphan.Level);

                //The field as the game's own map code has it after this cut, for a caller that wants to ask
                //something of the level as it is PLAYED rather than as it was authored (#514's arrival report
                //is the first). Null for the gate itself, which is what every existing caller passes.
                afterMove?.Invoke(map);
            }

            return map.GetRemovableBallsCount() == 0;
        }

        /// <summary>
        /// <b>The sound lower bound: how many colours stand on the anchor course.</b> Every cell of the field's
        /// top level seeds <see cref="BallsMap.GetCellsDisconnectedFromCeiling"/>, so a ball up there cannot be
        /// orphaned by anything and has to be matched; a match takes one group, and a group is one colour.
        /// <para>
        /// Zero means the bound was not taken, and there is one case: a removable ball on that course that is
        /// not matchable — glass (which a landing turns into whichever colour it likes) or a bomb (which leaves
        /// by a path this model does not play). Both are already refused up there by
        /// <c>FindStrandedSpecials</c>; the check is here because a bound has to be right for a reason of its
        /// own rather than because another gate is standing in front of it.
        /// </para>
        /// </summary>
        private int AnchorColourFloor(bool[] present)
        {
            int mask = 0, colours = 0;

            foreach (int cell in _topCells)
            {
                if (!present[cell]) continue;
                if (!_removable[cell]) continue;
                if (!_matchable[cell]) return 0;

                int bit = 1 << _type[cell];
                if ((mask & bit) != 0) continue;

                mask |= bit;
                colours++;
            }

            return colours;
        }

        /// <summary>
        /// Every move of a state, deduplicated by what it takes away. A move is a landing — an open pocket, a
        /// colour, the glass body it would colour and the group that completes — and two landings that take the
        /// same cells are one move however differently they were aimed.
        /// </summary>
        private List<Move> Moves(bool[] present)
        {
            FloodOpenSpace(present);

            List<Move> moves = new();
            HashSet<ulong> seen = new();

            for (int cell = 0; cell < _n; cell++)
            {
                if (present[cell] || !_open[cell]) continue;

                //A pocket with nothing beside it is open air the shot would sail through, which is the rule
                //GlassLandingDrop applies to its own walk.
                bool touches = false, touchesGlass = false;
                int colourMask = 0;

                for (int i = _neighbourStart[cell]; i < _neighbourStart[cell + 1]; i++)
                {
                    int neighbour = _neighbourCell[i];
                    if (!present[neighbour]) continue;

                    touches = true;
                    if (_glass[neighbour]) touchesGlass = true;
                    else if (_matchable[neighbour]) colourMask |= 1 << _type[neighbour];
                }

                if (!touches) continue;

                foreach (byte colour in _colours)
                {
                    //Against glass every colour the level plays is worth trying, since the landing makes the
                    //body its own; against ordinary balls only a colour already standing there can complete
                    //anything.
                    if (!touchesGlass && (colourMask & (1 << colour)) == 0) continue;

                    List<int> group = Group(present, cell, colour);
                    if (group == null) continue;

                    ulong key = 0;
                    foreach (int member in group) key ^= _zobrist[member];

                    if (seen.Add(key)) moves.Add(new Move(cell, colour, group));
                }
            }

            return moves;
        }

        /// <summary>
        /// What one landing takes: the glass body it colours first, then the connected group of that colour
        /// around it — <c>BallContactEventHandler</c>'s own order, and for its stated reason (colour before the
        /// count, or a shot that completes a group <i>through</i> the glass is not credited with it). Null when
        /// the group falls short of <see cref="BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE"/>, which is a shot
        /// spent rather than a move.
        /// </summary>
        private List<int> Group(bool[] present, int landing, byte colour)
        {
            _generation++;
            int generation = _generation;
            List<int> group = new();

            //The glass first, from the landing through glass ONLY — the walk ColourTransparentGroup makes,
            //which is why a pane reached through a coloured ball is not part of it. Every pane it finds is
            //this colour from now on, so the group starts holding them.
            for (int i = _neighbourStart[landing]; i < _neighbourStart[landing + 1]; i++)
            {
                int neighbour = _neighbourCell[i];
                if (!present[neighbour] || !_glass[neighbour] || _mark[neighbour] == generation) continue;

                _mark[neighbour] = generation;
                group.Add(neighbour);
            }

            for (int read = 0; read < group.Count; read++)
            {
                int cell = group[read];
                for (int i = _neighbourStart[cell]; i < _neighbourStart[cell + 1]; i++)
                {
                    int neighbour = _neighbourCell[i];
                    if (!present[neighbour] || !_glass[neighbour] || _mark[neighbour] == generation) continue;

                    _mark[neighbour] = generation;
                    group.Add(neighbour);
                }
            }

            //Then the group itself, walked out from the landing and from every pane the colour just reached:
            //the balls that were already this colour, joined through each other.
            List<int> frontier = new(group) { landing };
            _mark[landing] = generation;

            for (int read = 0; read < frontier.Count; read++)
            {
                int cell = frontier[read];
                for (int i = _neighbourStart[cell]; i < _neighbourStart[cell + 1]; i++)
                {
                    int neighbour = _neighbourCell[i];
                    if (!present[neighbour] || _mark[neighbour] == generation) continue;
                    if (!_matchable[neighbour] || _type[neighbour] != colour) continue;

                    _mark[neighbour] = generation;
                    frontier.Add(neighbour);
                    group.Add(neighbour);
                }
            }

            //The landing's own ball counts towards the match and is not part of what comes down: it was never
            //hanging there.
            return group.Count + 1 < BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE ? null : group;
        }

        /// <summary>
        /// Which empty cells a shot could reach, flooded from the field's walls and floor. Recomputed for every
        /// state, because a cut opens the inside of a cluster up — a pocket sealed on the first shot is a
        /// landing on the fifth.
        /// </summary>
        private void FloodOpenSpace(bool[] present)
        {
            Array.Clear(_open);
            int depth = 0;

            for (int cell = 0; cell < _n; cell++)
            {
                if (present[cell] || !_openSeed[cell] || _open[cell]) continue;

                _open[cell] = true;
                _stack[depth++] = cell;
            }

            while (depth > 0)
            {
                int cell = _stack[--depth];
                for (int i = _neighbourStart[cell]; i < _neighbourStart[cell + 1]; i++)
                {
                    int neighbour = _neighbourCell[i];
                    if (present[neighbour] || _open[neighbour]) continue;

                    _open[neighbour] = true;
                    _stack[depth++] = neighbour;
                }
            }
        }

        /// <summary>
        /// Plays one move onto a copy of the state: the group leaves, then everything it was the last support
        /// for goes with it — the same walk <c>ResolveDisconnected</c> makes after every removal in the game.
        /// </summary>
        /// <returns>Removable balls still standing.</returns>
        private int Apply(bool[] present, List<int> group, bool[] into, ref ulong hash)
        {
            Array.Copy(present, into, _n);
            foreach (int cell in group)
            {
                into[cell] = false;
                hash ^= _zobrist[cell];
            }

            _generation++;
            int generation = _generation;
            int depth = 0;

            foreach (int cell in _topCells)
            {
                if (!into[cell] || _mark[cell] == generation) continue;

                _mark[cell] = generation;
                _stack[depth++] = cell;
            }

            while (depth > 0)
            {
                int cell = _stack[--depth];
                for (int i = _neighbourStart[cell]; i < _neighbourStart[cell + 1]; i++)
                {
                    int neighbour = _neighbourCell[i];
                    if (!into[neighbour] || _mark[neighbour] == generation) continue;

                    _mark[neighbour] = generation;
                    _stack[depth++] = neighbour;
                }
            }

            int standing = 0;

            for (int cell = 0; cell < _n; cell++)
            {
                if (!into[cell]) continue;

                if (_mark[cell] != generation)
                {
                    into[cell] = false;
                    hash ^= _zobrist[cell];
                    continue;
                }

                if (_removable[cell]) standing++;
            }

            return standing;
        }

        private ulong Hash(bool[] present)
        {
            ulong hash = 0;
            for (int cell = 0; cell < _n; cell++)
                if (present[cell]) hash ^= _zobrist[cell];

            return hash;
        }

        /// <summary>
        /// Every sequence of at most <paramref name="budget"/> shots, until one of them empties the field.
        /// <b>Deduplicated by the state it arrives at</b>, since the order two independent matches are played
        /// in changes nothing — which is what makes the depth affordable at all.
        /// </summary>
        /// <returns>Whether the field can be emptied within the budget; <paramref name="line"/> then holds the
        /// shots that did it, in the order they are played.</returns>
        private bool Search(bool[] present, ulong hash, int budget, List<Move> line)
        {
            if (budget <= 0) return false;

            List<Move> moves = Moves(present);
            bool[] child = new bool[_n];

            foreach (Move move in moves)
            {
                ulong childHash = hash;
                int standing = Apply(present, move.Cells, child, ref childHash);

                if (standing == 0)
                {
                    line.Add(move);
                    return true;
                }

                if (budget == 1) continue;

                //TWO THINGS STOP THE WALK, and the level's own shape decides which. The floor is asked again
                //of the state the shot left — four colours on the anchor course cannot be emptied in two shots
                //however they are aimed — and the table remembers a state already searched no shallower, which
                //is what keeps two independent matches played in either order from being two subtrees.
                if (AnchorColourFloor(child) > budget - 1) continue;
                if (_exhausted.TryGetValue(childHash, out int deep) && deep >= budget - 1) continue;

                if (Search(child, childHash, budget - 1, line))
                {
                    line.Insert(0, move);
                    return true;
                }

                _exhausted[childHash] = budget - 1;
            }

            return false;
        }

        /// <summary>
        /// <b>One line of play, greedily, and it proves nothing</b> — it is how the log gets a number for a
        /// level the exhaustive search has (rightly) stopped short of. The states carried forward are the ones
        /// with the least left standing, which is the obvious way to empty a field fast and is not the only
        /// one; what comes out is an upper bound and is reported as one.
        /// </summary>
        /// <returns>The fewest shots this line found, or <see cref="int.MaxValue"/> for no clear at all.</returns>
        private int Beam()
        {
            List<(bool[] Present, int Standing, ulong Hash)> live = new() { (_present, _removableCount, Hash(_present)) };

            for (int shot = 1; shot <= BEAM_SHOTS; shot++)
            {
                List<(bool[] Present, int Standing, ulong Hash)> next = new();
                HashSet<ulong> seen = new();

                foreach ((bool[] present, int _, ulong hash) in live)
                {
                    foreach (Move move in Moves(present))
                    {
                        bool[] child = new bool[_n];
                        ulong childHash = hash;
                        int standing = Apply(present, move.Cells, child, ref childHash);

                        if (standing == 0) return shot;
                        if (seen.Add(childHash)) next.Add((child, standing, childHash));
                    }
                }

                if (next.Count == 0) return int.MaxValue;

                next.Sort((a, b) => a.Standing.CompareTo(b.Standing));
                if (next.Count > BEAM_WIDTH) next.RemoveRange(BEAM_WIDTH, next.Count - BEAM_WIDTH);

                live = next;
            }

            return int.MaxValue;
        }
    }
}
