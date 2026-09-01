using Prazsky.BS3D.GameStructure.DataBags;
using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// How many shells of packed balls stand between a cell and open air — the burial depth the neighbour
    /// count cannot see (#303, the half of #40 that never landed). <see cref="BallsMap.CountOccupiedNeighbors"/>
    /// examines exactly the twelve touching cells, so a ball one layer under the surface and a ball in the
    /// dead centre of a nine-hundred-ball cluster read identically: both have their ring full. This measures
    /// the difference — the lattice distance to the nearest cell that is empty or out of bounds — with a
    /// multi-source BFS over the grid, seeded on every occupied cell whose ring is short of full (a missing
    /// or empty neighbour IS the air) and walked over the same parity-dependent twelve-cell adjacency the
    /// rest of the grid speaks.
    /// <para>
    /// <b>Depth is clamped at <see cref="MAX_DEPTH"/></b>: past three shells nothing about the shading
    /// changes any more (see <c>BallRenderSet.OCCLUSION_DEPTH_STRENGTH</c>, whose ramp this feeds), so the
    /// wave stops expanding there and everything deeper simply keeps the clamp — which also bounds the walk:
    /// only the outer three shells of a cluster are ever traversed.
    /// </para>
    /// <para>
    /// <b>Per-frame hygiene:</b> the two work arrays are grown only when a bigger grid arrives and are reused
    /// forever after, so a steady frame allocates nothing here (BestPractices.md). Both per-frame walks own
    /// one instance each — <c>ClusterCollector</c> over the physics array, <c>BallDrawFrame.AddMap</c> over a
    /// static map — the same split as the occlusion count itself.
    /// </para>
    /// </summary>
    public sealed class AirDepthField
    {
        /// <summary>
        /// Where the depth ramp tops out, in shells. Three is where the visible effect saturates by
        /// construction — the shader's burial ramp reaches its floor there — and every cell deeper carries
        /// this value without being visited.
        /// </summary>
        public const int MAX_DEPTH = 3;

        //What an empty cell carries in the depth array: never read back (DepthAt is asked about balls, and a
        //ball's cell is occupied by definition), but it has to be a value the BFS relaxation can never treat
        //as "already closer to air" - see Relax, which tests occupancy before it ever looks at this.
        private const byte AIR = byte.MaxValue;

        private byte[] _depth = Array.Empty<byte>();
        private int[] _queue = Array.Empty<int>();

        private int _sizeX, _sizeZ, _sizeLevel;

        /// <summary>
        /// Recomputes the whole field from the grid — called once per frame by each walk, before its per-ball
        /// loop, the same cadence the occlusion count itself is re-derived on (and for the same reason: a ball
        /// that attaches or a group that releases changes its whole neighbourhood's burial, and asking the
        /// grid is cheaper than tracking whom to touch). Measured at 117 µs a call on a 1,789-cell blob,
        /// denser than the 959-ball stress level — see the frame-rate figure beside the cluster walk's call.
        /// </summary>
        public void Compute<T>(T[,,] balls, XZLevel size) where T : class
        {
            int cells = size.X * size.Z * size.Level;

            if (cells > _depth.Length)
            {
                _depth = new byte[cells];
                _queue = new int[cells];
            }

            _sizeX = size.X;
            _sizeZ = size.Z;
            _sizeLevel = size.Level;

            int tail = 0;

            //Seed: every occupied cell short of a full ring touches air (an out-of-bounds position and an
            //empty cell are both air, and both leave the count under twelve), so it is depth 0 and a BFS
            //source. A full ring is provisionally the clamp, to be relaxed by the wave.
            for (int level = 0; level < size.Level; level++)
                for (int z = 0; z < size.Z; z++)
                    for (int x = 0; x < size.X; x++)
                    {
                        int index = (level * _sizeZ + z) * _sizeX + x;

                        if (balls[x, z, level] == null)
                        {
                            _depth[index] = AIR;
                            continue;
                        }

                        if (BallsMap.CountOccupiedNeighbors(balls, new XZLevel(x, z, level), size, out _)
                            < BallRenderSet.MAX_OCCLUDERS)
                        {
                            _depth[index] = 0;
                            _queue[tail++] = index;
                        }
                        else _depth[index] = MAX_DEPTH;
                    }

            //The wave: plain BFS, so every cell is finished the first time it is reached. Expansion stops a
            //shell short of the clamp - cells the wave never reaches are exactly the ones at least MAX_DEPTH
            //shells in, and they already carry it.
            for (int head = 0; head < tail; head++)
            {
                int index = _queue[head];
                byte next = (byte)(_depth[index] + 1);

                if (next >= MAX_DEPTH) continue;

                int level = index / (_sizeX * _sizeZ);
                int rest = index - level * _sizeX * _sizeZ;
                int z = rest / _sizeX;
                int x = rest - z * _sizeX;

                //The same twelve candidates CountOccupiedNeighbors tests, in the same parity arithmetic
                Relax(balls, x - 1, z, level, next, ref tail);
                Relax(balls, x + 1, z, level, next, ref tail);
                Relax(balls, x, z - 1, level, next, ref tail);
                Relax(balls, x, z + 1, level, next, ref tail);

                int diagonalShift = (level % 2) > 0 ? 0 : -1;

                for (int levelOffset = -1; levelOffset <= 1; levelOffset += 2)
                    for (int dX = 0; dX <= 1; dX++)
                        for (int dZ = 0; dZ <= 1; dZ++)
                            Relax(balls, x + dX + diagonalShift, z + dZ + diagonalShift, level + levelOffset,
                                next, ref tail);
            }
        }

        /// <summary>
        /// The computed burial of one occupied cell, 0 (touching air) to <see cref="MAX_DEPTH"/>. Only
        /// meaningful after <see cref="Compute{T}"/> this frame, and only for a cell that holds a ball —
        /// which is the only kind either caller asks about.
        /// </summary>
        public int DepthAt(int x, int z, int level) => _depth[(level * _sizeZ + z) * _sizeX + x];

        private void Relax<T>(T[,,] balls, int x, int z, int level, byte next, ref int tail) where T : class
        {
            if (x < 0 || z < 0 || level < 0 || x >= _sizeX || z >= _sizeZ || level >= _sizeLevel) return;
            if (balls[x, z, level] == null) return;

            int index = (level * _sizeZ + z) * _sizeX + x;

            if (_depth[index] <= next) return;

            _depth[index] = next;
            _queue[tail++] = index;
        }
    }
}
