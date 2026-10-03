using Prazsky.BS3D.GameStructure.DataBags;
using System;
using System.Collections.Generic;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>A modelled player</b> on the lattice (#719): the half of <see cref="ClearProbe"/> that plays a level to a clear
    /// the way a person might instead of searching for the fewest shots, so <see cref="DropProbe"/> can count how often the
    /// drop cinematic's trigger would fire. It is a partial of the probe because everything it needs — the lattice, the
    /// landings, the group a landing takes and the walk that lets the orphans go — is the probe's own, and a second copy of
    /// that rule is the one thing this repository refuses.
    /// <para>
    /// A shot is a colour dealt from those still standing in the cluster; the player lands it somewhere that completes a group
    /// (<see cref="Moves"/>, restricted to that colour), or the shot is spent: the setup shots a real player also takes are
    /// not modelled, as in the search, so a run is a lower bound on the shots a level takes and says nothing about its budget.
    /// Physics, blasts, zaps and the ceiling's descent are not played either, which is the lattice's standing limit.
    /// </para>
    /// </summary>
    internal sealed partial class ClearProbe
    {
        /// <summary>How the player chooses among the landings of the colour in the cannon.</summary>
        internal enum DropPlayer
        {
            /// <summary>The landing that lets the most balls go (matched and orphaned together) — the careful player.</summary>
            Greedy,

            /// <summary>Any landing that completes a group, chosen at random — the casual one.</summary>
            Casual,
        }

        /// <summary>
        /// One played level: the size of every release in order (the landing ball counted, as <c>BallsReleased.Matched</c>
        /// counts it), whether the field ended up empty, and the balls the level started with.
        /// </summary>
        internal readonly record struct DropRun(int[] Releases, bool Cleared, int Shots, int InitialBalls);

        /// <summary>Plays <paramref name="data"/>'s level to a clear, or until <paramref name="shotCap"/> shots are spent.</summary>
        internal static DropRun PlayDrops(BallPositionTypes data, DropPlayer player, int seed, int shotCap)
        {
            ClearProbe probe = new(data);
            return probe.PlayOut(player, seed, shotCap);
        }

        /// <summary>
        /// Consecutive shots with nowhere to land before the player is taken to be stuck: the removable balls that are left
        /// can only leave by a rule this model does not play (a bomb's blast, a zap), and dealing colours for ever would
        /// never say so.
        /// </summary>
        private const int STUCK_AFTER_SPENT_SHOTS = 60;

        private DropRun PlayOut(DropPlayer player, int seed, int shotCap)
        {
            Random random = new(seed);
            bool[] present = (bool[])_present.Clone();
            bool[] trial = new bool[_n];
            List<int> releases = new();
            List<byte> dealt = new();

            int initial = Count(present);
            int standing = _removableCount;
            int shots = 0, spent = 0;
            ulong hash = 0;

            while (standing > 0 && shots < shotCap && spent < STUCK_AFTER_SPENT_SHOTS)
            {
                //The deal: a colour that still has a ball standing to complete a group with
                dealt.Clear();
                foreach (byte colour in _colours)
                    for (int cell = 0; cell < _n; cell++)
                        if (present[cell] && _matchable[cell] && _type[cell] == colour)
                        {
                            dealt.Add(colour);
                            break;
                        }

                if (dealt.Count == 0) break;

                byte shot = dealt[random.Next(dealt.Count)];
                shots++;

                List<Move> moves = Moves(present, shot);
                if (moves.Count == 0)
                {
                    spent++;
                    continue;
                }

                spent = 0;

                int before = Count(present);
                Move chosen = moves[random.Next(moves.Count)];

                if (player == DropPlayer.Greedy)
                {
                    int most = -1;
                    foreach (Move move in moves)
                    {
                        ulong scratch = 0;
                        Apply(present, move.Cells, trial, ref scratch);
                        int gone = before - Count(trial);

                        if (gone <= most) continue;

                        most = gone;
                        chosen = move;
                    }
                }

                standing = Apply(present, chosen.Cells, trial, ref hash);

                //+1: the landing ball is part of the match and was never in the cluster to be counted going
                releases.Add(before - Count(trial) + 1);

                (present, trial) = (trial, present);
            }

            return new DropRun(releases.ToArray(), standing == 0, shots, initial);
        }

        private int Count(bool[] cells)
        {
            int count = 0;
            for (int i = 0; i < _n; i++)
                if (cells[i]) count++;

            return count;
        }
    }
}
