using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A static instance buffer's instances laid out by cell round the arena's axis, so a draw sends only the runs of cells a
    /// frustum can see (#1099): a cell is a ring of distance from the axis and a sector of bearing in it (<see cref="SECTORS"/>
    /// round the axis), the buffer is sorted ring by ring outwards and, inside a ring, sector by sector round the axis, and each
    /// cell keeps a box round everything in it. The aurora's far stand culls by sector the same way (#1027); a wood planted
    /// all round the clearing is mostly behind or beside the lens, and drawn whole every triangle of it went through the
    /// vertex shader for nothing.
    /// <para>
    /// Rings come first so that a reach (<see cref="Runs"/>) drops whole rings: a bucket drawn only near the lens is a run or
    /// two of sectors in each of its first rings. A bucket with no reach is laid out in one ring and is one run of the sectors
    /// in view, two where the run wraps past the last sector.
    /// </para>
    /// <para>
    /// The cut is exact: a cell is skipped only when its box is outside the frustum (or, with a reach, wholly farther from the
    /// lens than that), and the box holds every instance's bounding sphere, so nothing that would have put a pixel on the screen
    /// or a texel in the sun's map is left out. Consecutive cells that are drawn go out as one run, one draw.
    /// </para>
    /// <para>
    /// Built once, device-free (so a test can read it); <see cref="Runs"/> writes into arrays it owns and allocates nothing.
    /// </para>
    /// </summary>
    public sealed class InstanceCells
    {
        /// <summary>Sectors of bearing round the axis in every ring.</summary>
        public const int SECTORS = 32;

        /// <summary>How many rings there are, and how wide one is (world units from the axis; the last runs on to the farthest
        /// instance).</summary>
        public int Rings { get; }
        public float RingWidth { get; }

        /// <summary>Per cell (ring × <see cref="SECTORS"/> + sector): where its run starts in the sorted order, how long it is,
        /// and the box round its instances' bounding spheres (an empty cell keeps a degenerate box and is never drawn).</summary>
        public int[] Start { get; }
        public int[] Count { get; }
        public BoundingBox[] Bounds { get; }

        /// <summary>How many instances there are in all.</summary>
        public int Total { get; }

        /// <summary>The runs <see cref="Runs"/> found last: their starts and lengths in the sorted order.</summary>
        public int[] RunStart { get; }
        public int[] RunCount { get; }

        private InstanceCells(int rings, float ringWidth, int total)
        {
            Rings = rings;
            RingWidth = ringWidth;
            Total = total;
            int cells = SECTORS * rings;
            Start = new int[cells];
            Count = new int[cells];
            Bounds = new BoundingBox[cells];
            RunStart = new int[cells];
            RunCount = new int[cells];
        }

        /// <summary>The cell a point stands in: its ring of distance from the axis and its sector of bearing.</summary>
        public int CellOf(Vector3 at)
        {
            int sector = Math.Clamp((int)((MathF.Atan2(at.Z, at.X) + MathF.PI) / MathHelper.TwoPi * SECTORS), 0, SECTORS - 1);
            int ring = Rings == 1 ? 0 : Math.Clamp((int)(MathF.Sqrt(at.X * at.X + at.Z * at.Z) / RingWidth), 0, Rings - 1);
            return ring * SECTORS + sector;
        }

        /// <summary>
        /// Lays out instances whose bounding spheres are <paramref name="centres"/> and <paramref name="radii"/>: returns the cells
        /// and the order to write the instances in (<c>order[k]</c> is the index of the k-th instance in the buffer). Inside a
        /// cell they keep the order they were given.
        /// </summary>
        /// <param name="ringWidth">How wide a ring is; the rings run out to the farthest instance. Zero or less lays everything
        /// out in one ring, for a bucket culled by the frustum alone.</param>
        public static InstanceCells Build(IReadOnlyList<Vector3> centres, IReadOnlyList<float> radii, float ringWidth, out int[] order)
        {
            int rings = 1;
            if (ringWidth > 0f)
            {
                float farthest = 0f;
                for (int i = 0; i < centres.Count; i++)
                    farthest = MathF.Max(farthest, MathF.Sqrt(centres[i].X * centres[i].X + centres[i].Z * centres[i].Z));
                rings = Math.Max(1, (int)(farthest / ringWidth) + 1);
            }

            var cells = new InstanceCells(rings, ringWidth, centres.Count);
            int count = SECTORS * rings;
            var cellOf = new int[centres.Count];
            for (int i = 0; i < centres.Count; i++)
            {
                cellOf[i] = cells.CellOf(centres[i]);
                cells.Count[cellOf[i]]++;
            }

            for (int c = 1; c < count; c++) cells.Start[c] = cells.Start[c - 1] + cells.Count[c - 1];

            order = new int[centres.Count];
            var filled = new int[count];
            for (int i = 0; i < centres.Count; i++)
            {
                int c = cellOf[i];
                order[cells.Start[c] + filled[c]++] = i;

                Vector3 extent = new(radii[i]);
                BoundingBox box = new(centres[i] - extent, centres[i] + extent);
                cells.Bounds[c] = filled[c] == 1 ? box : BoundingBox.CreateMerged(cells.Bounds[c], box);
            }
            return cells;
        }

        /// <summary>
        /// The runs of consecutive cells to draw: those with anything in them whose box meets <paramref name="frustum"/> and, with
        /// <paramref name="reach"/> above zero, comes within it of <paramref name="lens"/>. Written into <see cref="RunStart"/>
        /// and <see cref="RunCount"/>; returns how many.
        /// </summary>
        public int Runs(BoundingFrustum frustum, Vector3 lens, float reach = 0f)
        {
            int runs = 0, cells = SECTORS * Rings;
            float reach2 = reach * reach;
            int c = 0;
            while (c < cells)
            {
                if (Count[c] == 0 || !Seen(c, frustum, lens, reach2)) { c++; continue; }
                int first = c;
                while (c < cells && (Count[c] == 0 || Seen(c, frustum, lens, reach2))) c++;
                int start = Start[first], end = Start[c - 1] + Count[c - 1];
                RunStart[runs] = start;
                RunCount[runs] = end - start;
                runs++;
            }
            return runs;
        }

        //An empty cell never starts a run and never breaks one (Runs): it has nothing to draw, and two runs either side of it are one
        private bool Seen(int c, BoundingFrustum frustum, Vector3 lens, float reach2)
        {
            if (reach2 > 0f && DistanceSquared(Bounds[c], lens) > reach2) return false;
            return frustum.Contains(Bounds[c]) != ContainmentType.Disjoint;
        }

        /// <summary>How far a point is from a box, squared: 0 inside it.</summary>
        public static float DistanceSquared(BoundingBox box, Vector3 point)
        {
            Vector3 nearest = Vector3.Clamp(point, box.Min, box.Max);
            return Vector3.DistanceSquared(nearest, point);
        }
    }
}
