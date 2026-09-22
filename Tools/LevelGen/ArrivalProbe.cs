using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>Whether a landing can actually be ARRIVED at</b> — #514, and the question the two instruments before
    /// it could not fail.
    /// <para>
    /// <see cref="Prazsky.BS3D.AimReachability"/> asks whether the barrel can be <i>pointed</i> at a cell, and a
    /// field subtending about 20° against a 45° traverse cone means nothing can ever fall outside it; the
    /// open-space flood asks whether a cell is connected to the outside through empty neighbours, and a flood
    /// has no direction in it, so a ring of empty cells round the cluster joins every wall to every other. Both
    /// pass everything. What neither models is the only thing that actually stops a shot: <b>it travels in a
    /// straight line and comes to rest against the first ball it touches</b>, so a pocket on the far side of the
    /// cluster can be aimed at, is connected to open space, and still cannot be reached from where the gun
    /// stands.
    /// </para>
    /// <para>
    /// <b>The gun walks the whole orbit, so the honest question is not "reachable" but "from how far round".</b>
    /// <c>Cannon.EnsureOrbitAngleInBounds</c> wraps the orbit angle into 0..2π and clamps nothing, so A/D can
    /// carry the carriage all the way round the field: a cell blocked from the resting stance is very often
    /// reachable from the other side. That makes two separable answers out of one probe, and both are worth
    /// having — a cell reachable from <b>no</b> station at all is a level-refusing fault, and the <b>walk</b>
    /// the level demands (how far from rest the gun has to go) is the measurable form of #457's original
    /// question about whether the Meadow's levels need a control their tutorial has not taught.
    /// </para>
    /// <para>
    /// <b>It is recomputed against the field as it stands, never once on the intact cluster.</b> A cut opens
    /// lines that were closed, so a single up-front answer understates reachability — the wrong direction to err
    /// in for a check whose job is to refuse a design rather than to pass one that later proves unplayable.
    /// The occluder list of one (cell, station) pair is pure geometry and never changes, so it is computed once
    /// and cached; what changes after a cut is only which of those cells are still standing, which is a scan
    /// over a handful of indices.
    /// </para>
    /// </summary>
    internal sealed class ArrivalProbe
    {
        /// <summary>
        /// How many stances round the orbit are tried. The barrel traverses ±45° (<c>Cannon.MaxTraverse</c>)
        /// about wherever the carriage stands, so the stations only have to be closer together than that cone
        /// is wide for every bearing to be covered; 16 of them are 22.5° apart, comfortably inside it.
        /// </summary>
        private const int STATIONS = 16;

        /// <summary>
        /// How near a standing ball's centre the shot's own centre may come, in lattice units. Neighbouring
        /// cells sit exactly 1 apart, so 1 is touching — which is what a landing <i>is</i>, and why the value is
        /// a hair under it rather than at it: the shot ends its flight exactly this far from the ball it lands
        /// against, and a threshold of exactly 1 would read that contact as an obstruction.
        /// <para>
        /// ⚠ It follows that a gap one cell wide between two standing balls is <b>closed</b>, and that is
        /// correct rather than conservative: the corridor is one ball wide and the ball flying down it is one
        /// ball wide. The old flood counted such a gap as open, which is half of why it passed everything.
        /// </para>
        /// </summary>
        private const float BLOCK_CLEARANCE = 0.98f;

        /// <summary>
        /// How far apart the ray is sampled when it is looking for what stands near it, in lattice units. Under
        /// a ball's own diameter, and every sample tests the 3×3×3 block of cells around it, so no standing ball
        /// beside the line can be stepped over.
        /// </summary>
        private const float WALK_STEP = 0.7f;

        private readonly int _n;
        private readonly int _sizeX;
        private readonly int _sizeZ;
        private readonly int _levels;

        //The cells' own world positions, and the stations' — computed once from the map and the gun's geometry.
        private readonly Vector3[] _cellWorld;
        private readonly Vector3[] _stationWorld;

        //The occluder run of one (cell, station), computed the first time it is asked for and kept: it is pure
        //geometry. -1 in _runStart means "not computed yet"; the lists share one flat array, the neighbour
        //table's own idiom in ClearProbe.
        private readonly int[] _runStart;
        private readonly int[] _runCount;
        private readonly List<int> _occluders = new();

        //Scratch for one ray's dedupe, generation-stamped so it is never cleared.
        private readonly int[] _mark;
        private int _generation;

        internal ArrivalProbe(BallsMap map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            _sizeX = map.StageSizeX;
            _sizeZ = map.StageSizeZ;
            _levels = map.Levels;
            _n = _sizeX * _sizeZ * _levels;

            _cellWorld = new Vector3[_n];
            _mark = new int[_n];
            _runStart = new int[_n * STATIONS];
            _runCount = new int[_n * STATIONS];
            Array.Fill(_runStart, -1);

            //Where the cluster actually hangs. The probe measures in world units because the gun does; the
            //lattice's own frame is centred on nothing the carriage knows about.
            Vector3 hang = ClusterHang.FitWorldOffset(map, out _);

            for (int level = 0; level < _levels; level++)
                for (int x = 0; x < _sizeX; x++)
                    for (int z = 0; z < _sizeZ; z++)
                        _cellWorld[Index(x, z, level)] =
                            map.GetRealCenteredPosition(new XZLevel(x, z, level)) + hang;

            //THE GUN, and it is the same stance SagProbe fires from — the two bounds of GameCameraFit's solve
            //that are pure geometry (clear the field's footprint at every orbit angle, stay outside the drain's
            //mouth), which is the CLOSEST the solve can ever return and therefore the steepest, most obstructed
            //line the player can take. A gun standing further out only shoots flatter and clears more.
            float footprint = MathF.Max(CeilingPlate.FootprintFor(map.StageSizeX),
                CeilingPlate.FootprintFor(map.StageSizeZ)) * Constants.HALF;
            float orbitRadius = MathF.Max(
                footprint * Constants.SQRT_TWO + GameCameraFit.CANNON_FIELD_CLEARANCE,
                ArenaIsland.FUNNEL_TOP_RADIUS + GameCameraFit.CANNON_DRAIN_CLEARANCE);

            //The shot leaves at the trunnions' height, which is what AimReachability calls the shot's origin in
            //Y and what CannonRig seats off the island's own dish. The barrel's length is deliberately not
            //modelled: it only ever moves the origin a little further along the line the ray already runs on.
            float trunnionsY = CannonRig.TrunnionHeightAt(orbitRadius);

            _stationWorld = new Vector3[STATIONS];
            for (int s = 0; s < STATIONS; s++)
            {
                //Station 0 is where a level opens — Cannon.CalculateInitialPositionAndAimTarget puts the
                //carriage at +Z — and the rest run round from it, so the walk a level demands is measured from
                //the stance the player is actually given.
                float angle = MathHelper.PiOver2 + MathHelper.TwoPi * s / STATIONS;
                //The orbit's centre is (0, 5, 0) in both executables and in SagProbe; only its X and Z matter
                //here, and both are zero — the Y is the aim point's throw and has nothing to do with where the
                //carriage stands.
                _stationWorld[s] = new Vector3(
                    orbitRadius * MathF.Cos(angle),
                    trunnionsY,
                    orbitRadius * MathF.Sin(angle));
            }
        }

        private int Index(int x, int z, int level) => (level * _sizeX + x) * _sizeZ + z;

        /// <summary>How far round the orbit station <paramref name="station"/> stands from where a level opens,
        /// in degrees, taking the shorter way round.</summary>
        internal static float WalkDegrees(int station)
        {
            int steps = Math.Min(station, STATIONS - station);
            return steps * 360f / STATIONS;
        }

        /// <summary>
        /// The station nearest the opening stance from which a straight shot reaches <paramref name="cell"/>
        /// through the field as <paramref name="present"/> has it, or <b>-1</b> when no station on the whole
        /// orbit can. Stations are tried outwards from rest, so the first hit is the shortest walk.
        /// </summary>
        internal int ArrivalStation(bool[] present, int cell)
        {
            for (int step = 0; step <= STATIONS / 2; step++)
            {
                if (IsClear(present, cell, WrapStation(step))) return WrapStation(step);
                if (step != 0 && step != STATIONS / 2 && IsClear(present, cell, WrapStation(-step)))
                    return WrapStation(-step);
            }

            return -1;
        }

        private static int WrapStation(int step) => (step % STATIONS + STATIONS) % STATIONS;

        private bool IsClear(bool[] present, int cell, int station)
        {
            int slot = cell * STATIONS + station;
            if (_runStart[slot] < 0) BuildRun(cell, station, slot);

            int start = _runStart[slot];
            for (int i = 0; i < _runCount[slot]; i++)
                if (present[_occluders[start + i]]) return false;

            return true;
        }

        /// <summary>
        /// Every cell whose ball, standing, would stop a shot fired from <paramref name="station"/> before it
        /// reached <paramref name="cell"/>. Pure geometry, so it is computed once and kept.
        /// <para>
        /// The line is walked rather than tested against the whole field: at 4 500 cells and 16 stations a
        /// test-everything build is 300 million distance tests a level, where a walk touches the couple of dozen
        /// cells that actually lie near the line. Each step tests the 3×3×3 block around it, and the step is
        /// under a ball's diameter, so nothing beside the line can be stepped over.
        /// </para>
        /// </summary>
        private void BuildRun(int cell, int station, int slot)
        {
            Vector3 from = _stationWorld[station];
            Vector3 to = _cellWorld[cell];
            Vector3 along = to - from;
            float length = along.Length();

            _runStart[slot] = _occluders.Count;

            if (length < 1e-3f) { _runCount[slot] = 0; return; }

            Vector3 direction = along / length;
            _generation++;
            int generation = _generation;
            int count = 0;

            for (float travelled = 0f; travelled <= length; travelled += WALK_STEP)
            {
                Vector3 at = from + direction * travelled;

                //Back out of the world frame into the lattice's own, so the cells near this point can be
                //looked up rather than searched for. The parity shift makes the X/Z rounding a per-level
                //business, which is why the level is recovered first.
                int nearLevel = (int)MathF.Round((at.Y - _cellWorld[0].Y) * Constants.SQRT_TWO);

                for (int dl = -1; dl <= 1; dl++)
                {
                    int level = nearLevel + dl;
                    if (level < 0 || level >= _levels) continue;

                    Vector3 origin = _cellWorld[Index(0, 0, level)];
                    int nearX = (int)MathF.Round(at.X - origin.X);
                    int nearZ = (int)MathF.Round(at.Z - origin.Z);

                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int x = nearX + dx, z = nearZ + dz;
                            if (x < 0 || x >= _sizeX || z < 0 || z >= _sizeZ) continue;

                            int candidate = Index(x, z, level);
                            if (candidate == cell || _mark[candidate] == generation) continue;
                            _mark[candidate] = generation;

                            if (DistanceToSegment(_cellWorld[candidate], from, direction, length) >= BLOCK_CLEARANCE)
                                continue;

                            _occluders.Add(candidate);
                            count++;
                        }
                }
            }

            _runCount[slot] = count;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 direction, float length)
        {
            float along = MathHelper.Clamp(Vector3.Dot(point - from, direction), 0f, length);
            return Vector3.Distance(point, from + direction * along);
        }
    }
}
