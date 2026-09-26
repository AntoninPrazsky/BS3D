using BepuPhysics;
using BepuPhysics.Constraints;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using System;
using System.Collections.Generic;
using System.Text;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// <b>The constraint bookkeeping, checked against the solver</b> (#585) — what CLAUDE.md's "Constraint handle
    /// bookkeeping" states as true by construction, asked of a live cluster rather than trusted.
    /// <para>
    /// Correctness there rests on three separately written walks agreeing exactly — the build pass's +X/+Z and
    /// even-level rule, <see cref="BallsConstraintsBuilder.ConnectToNeighborsOnOtherLevels"/>'s own copy of the
    /// parity arithmetic, and <see cref="BallsMap.FillNeighboringCells"/> — plus the four-slot capacity. A fault
    /// in any of them shows nowhere on screen until much later: a constraint no slot remembers is a ball that
    /// never falls, and a slot holding a handle the solver has since reused makes
    /// <see cref="PhysicsBall.RemoveAllConstraints"/>'s <c>ConstraintExists</c> guard answer yes for an
    /// <i>unrelated</i> constraint and remove it silently. So this reads the solver's side directly — each body's
    /// own constraint list — and never through the guard that hides the fault.
    /// </para>
    /// <para>
    /// A test-and-debug instrument: it allocates, and nothing on a frame path calls it. The logic tests run it
    /// after every operation of long seeded sequences of attaches and releases (<c>ClusterInvariantTests</c>).
    /// </para>
    /// </summary>
    public static class ClusterInvariants
    {
        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> listing every broken invariant, or returns when there
        /// is none. See <see cref="Check"/> for what is checked.
        /// </summary>
        public static void Verify(PhysicsBall[,,] physicsBalls, BallsMap map, Simulation simulation, BodyHandle ceiling)
        {
            List<string> problems = Check(physicsBalls, map, simulation, ceiling);
            if (problems.Count == 0) return;

            StringBuilder message = new($"{problems.Count} cluster invariant(s) broken:");
            foreach (string problem in problems) message.Append(Environment.NewLine).Append("  ").Append(problem);
            throw new InvalidOperationException(message.ToString());
        }

        /// <summary>
        /// Every broken invariant, one line each; empty when the cluster is consistent. Checked:
        /// <list type="number">
        /// <item><description>The map and the physics array agree cell for cell — occupancy, and each physics
        /// ball's <see cref="PhysicsBall.ArrayPosition"/>, <see cref="PhysicsBall.Type"/> and
        /// <see cref="PhysicsBall.Kind"/> (#323's rule that the two move together).</description></item>
        /// <item><description>Every stored handle exists, is a <see cref="BallSocket"/> and is stored once on
        /// its ball; and the ball's body takes part in no <see cref="BallSocket"/> that is not stored on it — the
        /// constraint no release could remove.</description></item>
        /// <item><description>Every handle joins its ball to exactly one other: a lattice neighbour holding the
        /// same handle in the mirrored group (same level: both in <c>HandlesMiddle</c>; across levels: the lower
        /// ball's <c>HandlesTop</c> and the upper ball's <c>HandlesBottom</c>), or — on the top level only, in
        /// <c>HandlesTop</c> — the ceiling body.</description></item>
        /// <item><description>Every occupied neighbouring pair shares exactly one constraint, and every
        /// top-level ball has exactly one to the ceiling.</description></item>
        /// <item><description>The ceiling body takes part in no <see cref="BallSocket"/> that no top-level ball
        /// stores: a released ball's anchor left behind.</description></item>
        /// </list>
        /// </summary>
        public static List<string> Check(PhysicsBall[,,] physicsBalls, BallsMap map, Simulation simulation, BodyHandle ceiling)
        {
            List<string> problems = new();
            StaticBall[,,] cells = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            int top = size.Level - 1;
            int ballSocketType = BallSocket.ConstraintTypeId;

            if (physicsBalls.GetLength(0) != size.X || physicsBalls.GetLength(1) != size.Z || physicsBalls.GetLength(2) != size.Level)
            {
                problems.Add($"the physics array is {physicsBalls.GetLength(0)}x{physicsBalls.GetLength(1)}x{physicsBalls.GetLength(2)}, the map {size.X}x{size.Z}x{size.Level}");
                return problems;
            }

            //Where each handle is stored: the cell and the group, as many times as it was found
            Dictionary<int, List<(XZLevel Cell, Group Group)>> stored = new();
            List<ConstraintHandle> handles = new();
            HashSet<int> ceilingHandles = new();

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        XZLevel cell = new(x, z, level);
                        StaticBall logical = cells[x, z, level];
                        PhysicsBall ball = physicsBalls[x, z, level];

                        if ((logical == null) != (ball == null))
                        {
                            problems.Add($"{Name(cell)}: the map says {(logical == null ? "empty" : "occupied")}, the physics array {(ball == null ? "empty" : "occupied")}");
                            continue;
                        }

                        if (ball == null) continue;

                        if (!ball.ArrayPosition.Equals(cell))
                            problems.Add($"{Name(cell)}: the ball's ArrayPosition says {Name(ball.ArrayPosition)}");
                        if (ball.Type != logical.Type || ball.Kind != logical.Kind)
                            problems.Add($"{Name(cell)}: the physics ball is {ball.Type}/{ball.Kind}, the map {logical.Type}/{logical.Kind}");

                        //What the ball remembers
                        HashSet<int> own = new();
                        Collect(ball.HandlesBottom, Group.Bottom, cell, own, stored, problems);
                        Collect(ball.HandlesMiddle, Group.Middle, cell, own, stored, problems);
                        Collect(ball.HandlesTop, Group.Top, cell, own, stored, problems);

                        //What the solver says the body takes part in
                        handles.Clear();
                        BodySockets(simulation, ball.BallReference.Handle, ballSocketType, handles);
                        foreach (ConstraintHandle handle in handles)
                            if (!own.Contains(handle.Value))
                                problems.Add($"{Name(cell)}: the body takes part in socket {handle.Value}, which no slot of the ball holds (a constraint no release can remove)");
                    }

            handles.Clear();
            BodySockets(simulation, ceiling, ballSocketType, handles);
            foreach (ConstraintHandle handle in handles) ceilingHandles.Add(handle.Value);

            //Each stored handle: exists, is a socket, and pairs up the way the lattice says
            Dictionary<(XZLevel, XZLevel), int> pairCount = new();
            Dictionary<XZLevel, int> ceilingCount = new();

            foreach (KeyValuePair<int, List<(XZLevel Cell, Group Group)>> entry in stored)
            {
                ConstraintHandle handle = new(entry.Key);
                List<(XZLevel Cell, Group Group)> at = entry.Value;

                if (!simulation.Solver.ConstraintExists(handle))
                {
                    problems.Add($"socket {entry.Key}, held by {Places(at)}, does not exist in the solver (a stale handle)");
                    continue;
                }

                if (simulation.Solver.HandleToConstraint[handle.Value].TypeId != ballSocketType)
                {
                    problems.Add($"handle {entry.Key}, held by {Places(at)}, is not a BallSocket (a stale handle the solver reused)");
                    continue;
                }

                if (at.Count == 1)
                {
                    (XZLevel cell, Group group) = at[0];

                    if (cell.Level != top || group != Group.Top || !ceilingHandles.Contains(entry.Key))
                        problems.Add($"socket {entry.Key} is held only by {Places(at)} and is not that top-level ball's ceiling anchor");
                    else
                        ceilingCount[cell] = ceilingCount.GetValueOrDefault(cell) + 1;
                    continue;
                }

                if (at.Count != 2)
                {
                    problems.Add($"socket {entry.Key} is held by {at.Count} slots: {Places(at)}");
                    continue;
                }

                (XZLevel a, Group groupA) = at[0];
                (XZLevel b, Group groupB) = at[1];

                if (!AreNeighbours(a, b, size))
                {
                    problems.Add($"socket {entry.Key} joins {Name(a)} and {Name(b)}, which are not lattice neighbours");
                    continue;
                }

                //The lower ball of a cross-level pair holds it upward, the upper one downward
                if (a.Level > b.Level) { (a, b) = (b, a); (groupA, groupB) = (groupB, groupA); }
                bool groupsRight = a.Level == b.Level
                    ? groupA == Group.Middle && groupB == Group.Middle
                    : groupA == Group.Top && groupB == Group.Bottom;
                if (!groupsRight)
                    problems.Add($"socket {entry.Key} is held as {groupA} by {Name(a)} and {groupB} by {Name(b)}");

                (XZLevel, XZLevel) key = Order(a, b);
                pairCount[key] = pairCount.GetValueOrDefault(key) + 1;
            }

            //Every occupied neighbouring pair exactly once, every top-level ball anchored exactly once
            Span<XZLevel> neighbours = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        if (physicsBalls[x, z, level] == null || cells[x, z, level] == null) continue;
                        XZLevel cell = new(x, z, level);

                        int count = BallsMap.FillNeighboringCells(cell, size, neighbours);
                        for (int i = 0; i < count; i++)
                        {
                            XZLevel n = neighbours[i];
                            if (physicsBalls[n.X, n.Z, n.Level] == null) continue;

                            (XZLevel, XZLevel) key = Order(cell, n);
                            if (!key.Item1.Equals(cell)) continue; //each pair once

                            int joined = pairCount.GetValueOrDefault(key);
                            if (joined != 1) problems.Add($"{Name(cell)} and {Name(n)} are neighbours joined by {joined} sockets");
                        }

                        if (level == top && ceilingCount.GetValueOrDefault(cell) != 1)
                            problems.Add($"{Name(cell)} is on the top level with {ceilingCount.GetValueOrDefault(cell)} ceiling anchors");
                    }

            foreach (int handle in ceilingHandles)
                if (!stored.ContainsKey(handle))
                    problems.Add($"the ceiling takes part in socket {handle}, which no ball holds (an anchor left behind)");

            return problems;
        }

        private enum Group { Bottom, Middle, Top }

        private static void Collect(ConstraintHandles slots, Group group, XZLevel cell, HashSet<int> own,
            Dictionary<int, List<(XZLevel, Group)>> stored, List<string> problems)
        {
            Add(slots.Handle1);
            Add(slots.Handle2);
            Add(slots.Handle3);
            Add(slots.Handle4);

            void Add(ConstraintHandle handle)
            {
                if (handle.Value < 0) return;

                if (!own.Add(handle.Value))
                {
                    problems.Add($"{Name(cell)} holds socket {handle.Value} in two slots");
                    return;
                }

                if (!stored.TryGetValue(handle.Value, out List<(XZLevel, Group)> list))
                    stored[handle.Value] = list = new List<(XZLevel, Group)>(2);
                list.Add((cell, group));
            }
        }

        /// <summary>The <see cref="BallSocket"/>s the body takes part in, read off the body's own constraint list.</summary>
        private static void BodySockets(Simulation simulation, BodyHandle body, int ballSocketType, List<ConstraintHandle> into)
        {
            BodyReference reference = simulation.Bodies[body];
            ref var constraints = ref reference.Constraints;

            for (int i = 0; i < constraints.Count; i++)
            {
                ConstraintHandle handle = constraints[i].ConnectingConstraintHandle;
                if (simulation.Solver.HandleToConstraint[handle.Value].TypeId == ballSocketType) into.Add(handle);
            }
        }

        private static bool AreNeighbours(XZLevel a, XZLevel b, XZLevel size)
        {
            Span<XZLevel> neighbours = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];
            int count = BallsMap.FillNeighboringCells(a, size, neighbours);
            for (int i = 0; i < count; i++) if (neighbours[i].Equals(b)) return true;
            return false;
        }

        private static (XZLevel, XZLevel) Order(XZLevel a, XZLevel b) => Index(a) <= Index(b) ? (a, b) : (b, a);

        private static long Index(XZLevel c) => ((long)c.Level << 32) | ((long)c.X << 16) | (uint)c.Z;

        private static string Name(XZLevel c) => $"({c.X}, {c.Z}, {c.Level})";

        private static string Places(List<(XZLevel Cell, Group Group)> at)
        {
            StringBuilder text = new();
            for (int i = 0; i < at.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(Name(at[i].Cell)).Append(' ').Append(at[i].Group);
            }
            return text.ToString();
        }
    }
}
