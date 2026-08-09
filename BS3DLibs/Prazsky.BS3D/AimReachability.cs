using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using System;

namespace Prazsky.BS3D
{
    /// <summary>
    /// Whether every cell of a field can actually be shot at — the check that says a level is finishable before
    /// anyone tries to finish it.
    /// <para>
    /// The awkward cell is never the far one. A shot fired from the near side of the orbit has the <b>smallest</b>
    /// horizontal separation to cross and therefore needs the <b>steepest</b> elevation, so the test is the facing
    /// shot: put the carriage on the same side as the cell and ask what elevation that demands. If the steepest
    /// such cell asks for more than the gun's clamp allows, that cell can never be cleared and the level cannot
    /// be finished.
    /// </para>
    /// <para>
    /// It lived in the Testbed as a routine that wrote three lines to the console (#76 hoists it). Writing
    /// nothing is the point of the move: a pure function of a map and a gun's orbit geometry — no simulation, no
    /// graphics device, no live <see cref="Cannon"/> instance — can be called from the map editor's save path,
    /// which is what makes it worth more than a diagnostic. <b>Presentation stays with the caller</b>: the
    /// Testbed's <c>[aimcheck]</c> lines are a documented CLI surface that a verification script reads, so those
    /// exact strings belong in the Testbed, formatted from the facts below.
    /// </para>
    /// </summary>
    public static class AimReachability
    {
        /// <summary>
        /// Runs the facing-shot test over every cell of <paramref name="map"/>.
        /// </summary>
        /// <param name="map">The field. Read only; its own centred world positions are what the test measures.</param>
        /// <param name="orbitRadius">How far out the gun's carriage orbits the field's centre.</param>
        /// <param name="trunnionsY">The height the barrel pivots at — the shot's origin in Y.</param>
        /// <param name="maxElevation">The gun's up-elevation clamp. A cell needing more than this is unreachable.</param>
        /// <param name="highestLevel">
        /// The topmost level to test. Defaults to every level, which is the right question for a field that
        /// is played whole. It is <b>not</b> the right question for a field taller than the camera frames:
        /// there the column reaches up out of shot and is delivered by the descending glass, so its upper
        /// levels are unreachable <i>now</i> by design and will be at the bottom of the frame by the time
        /// they matter. Asked of the whole of such a field the answer is "unfinishable", which is false —
        /// the caller passes the top of the window instead, and the question becomes the one worth asking:
        /// can the gun reach everything that is <i>in play</i>?
        /// </param>
        /// <param name="worldOffset">
        /// Where the lattice sits in the world. Zero — the default — is the Testbed's case, where the lattice
        /// frame <i>is</i> the world frame; the Game hangs level 0 at its cluster offset and must pass it.
        /// <para>
        /// <b>Only Y matters and all three are taken anyway</b>, because the horizontal term is measured from
        /// the gun's orbit centre and the field is centred on it — so X and Z cancel today and would not if a
        /// field were ever hung off-axis. Getting this wrong is quiet and expensive: without it the Game's
        /// cells are measured at their lattice heights, which on a deep field is several units above where
        /// they actually hang, and every required elevation comes out too steep. That is exactly what the
        /// Game's first <c>[aimcheck]</c> line did — it reported a tall level's top cell at Y 23.3 when it
        /// hangs at 18.3, and called a level unfinishable that finishes.
        /// </para>
        /// </param>
        public static AimReachabilityResult Check(BallsMap map, float orbitRadius, float trunnionsY,
            float maxElevation, int highestLevel = int.MaxValue, Vector3 worldOffset = default)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            int top = Math.Min(map.Levels - 1, highestLevel);

            int total = 0, unreachable = 0;
            float worstElevation = float.MinValue;
            XZLevel worstCell = default;
            float worstY = 0f;

            for (byte level = 0; level <= top; level++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        Vector3 world = map.GetRealCenteredPosition(new XZLevel(x, z, level)) + worldOffset;
                        float cellHorizontal = MathF.Sqrt(world.X * world.X + world.Z * world.Z);

                        //Facing shot: the gun on the same side as the cell, so the horizontal separation is the
                        //smallest it can be and the elevation the steepest. Floored at 1 so a cell directly under
                        //the carriage cannot divide the angle by nothing.
                        float facingSeparation = MathF.Max(orbitRadius - cellHorizontal, 1f);
                        float facingElevation = MathF.Atan2(world.Y - trunnionsY, facingSeparation);

                        total++;
                        if (facingElevation > maxElevation) unreachable++;
                        if (facingElevation > worstElevation) { worstElevation = facingElevation; worstCell = new XZLevel(x, z, level); worstY = world.Y; }
                    }

            return new AimReachabilityResult(total, unreachable, worstCell, worstY, worstElevation);
        }
    }

    /// <summary>
    /// What <see cref="AimReachability.Check"/> found: how many cells were tested, how many the gun cannot reach,
    /// and which single cell asked for the most elevation. A readonly struct carrying facts and no strings — the
    /// caller decides whether they become a console line, a dialog or a refused save.
    /// </summary>
    public readonly struct AimReachabilityResult
    {
        /// <summary>Cells tested — every cell of the field, occupied or not, since a shot may have to reach any.</summary>
        public readonly int TotalCells;

        /// <summary>Cells that need more up-elevation than the clamp allows. Any at all makes the field unfinishable.</summary>
        public readonly int UnreachableCells;

        /// <summary>The cell that asked for the most elevation — the one to look at when this fails.</summary>
        public readonly XZLevel WorstCell;

        /// <summary>That cell's world height.</summary>
        public readonly float WorstCellY;

        /// <summary>The elevation it demands, in radians, to be compared against the gun's clamp.</summary>
        public readonly float WorstElevation;

        public AimReachabilityResult(int totalCells, int unreachableCells, XZLevel worstCell, float worstCellY,
            float worstElevation)
        {
            TotalCells = totalCells;
            UnreachableCells = unreachableCells;
            WorstCell = worstCell;
            WorstCellY = worstCellY;
            WorstElevation = worstElevation;
        }

        /// <summary>Every cell can be shot while facing it.</summary>
        public bool Pass => UnreachableCells == 0;
    }
}
