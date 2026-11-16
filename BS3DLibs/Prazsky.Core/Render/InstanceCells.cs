using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A static instance buffer's instances laid out by cell round the arena's axis, so a draw sends only the runs of cells a
    /// frustum can see (#1099): a cell is a ring of distance from the axis (<see cref="RING_WIDTH"/> wide) and a sector of bearing
    /// in it (<see cref="SECTORS"/> round the axis), the buffer is sorted ring by ring outwards and, inside a ring, sector by
    /// sector, and each cell keeps a box round everything in it, turned to its sector's bearing. The aurora's far stand culls by
    /// sector the same way (#1027); a wood planted all round the clearing is mostly behind or beside a lens in the clearing,
    /// and drawn whole every triangle of it went through the vertex shader for nothing.
    /// <para>
    /// ⚠ <b>Rings, not sectors alone.</b> Every sector reaches in towards the axis, and a lens in the clearing looks across the
    /// axis: by sector alone the play camera met 77 % of a wood planted from 34 to 300 out, by cell under half. And the boxes
    /// are turned to the sector: one square to the world round a wedge on the diagonal is as wide as the wedge is long.
    /// </para>
    /// <para>
    /// The cut is exact: a cell is skipped only when its box lies wholly outside one of the frustum's planes, and the box holds
    /// every instance's bounding sphere, so nothing that would have put a pixel on the screen or a texel in the sun's map is
    /// left out. Consecutive cells that are drawn go out as one run, one draw: a ring the lens looks across is one run or two.
    /// </para>
    /// <para>
    /// Built once, device-free (so a test can read it); <see cref="Runs"/> writes into arrays it owns and allocates nothing.
    /// </para>
    /// </summary>
    public sealed class InstanceCells
    {
        /// <summary>Sectors of bearing round the axis in every ring.</summary>
        public const int SECTORS = 32;

        /// <summary>How wide a ring is, in world units from the axis; the last runs on to the farthest instance.</summary>
        public const float RING_WIDTH = 70f;

        /// <summary>How many rings there are.</summary>
        public int Rings { get; }

        /// <summary>Per cell (ring × <see cref="SECTORS"/> + sector): where its run starts in the sorted order and how long it is.</summary>
        public int[] Start { get; }
        public int[] Count { get; }

        /// <summary>How many instances there are in all.</summary>
        public int Total { get; }

        /// <summary>The runs <see cref="Runs"/> found last: their starts and lengths in the sorted order.</summary>
        public int[] RunStart { get; }
        public int[] RunCount { get; }

        //Per cell, the eight corners of a box round its instances' bounding spheres, turned to its sector's bearing
        private readonly Vector3[] _corners;

        //The frustum Runs was handed, as its six planes (which face out)
        private readonly Plane[] _planes = new Plane[6];

        private InstanceCells(int rings, int total)
        {
            Rings = rings;
            Total = total;
            int cells = rings * SECTORS;
            Start = new int[cells];
            Count = new int[cells];
            RunStart = new int[cells];
            RunCount = new int[cells];
            _corners = new Vector3[cells * 8];
        }

        /// <summary>The sector a point stands in, by its bearing round the axis.</summary>
        public static int SectorOf(Vector3 at) =>
            Math.Clamp((int)((MathF.Atan2(at.Z, at.X) + MathF.PI) / MathHelper.TwoPi * SECTORS), 0, SECTORS - 1);

        /// <summary>The cell a point stands in: its ring of distance from the axis and its sector of bearing.</summary>
        public int CellOf(Vector3 at) =>
            Math.Min((int)(MathF.Sqrt(at.X * at.X + at.Z * at.Z) / RING_WIDTH), Rings - 1) * SECTORS + SectorOf(at);

        /// <summary>
        /// Lays out instances whose bounding spheres are <paramref name="centres"/> and <paramref name="radii"/>: returns the
        /// cells and the order to write the instances in (<c>order[k]</c> is the index of the k-th instance in the buffer).
        /// Inside a cell they keep the order they were given, so a planting sorted nearest the axis first stays so.
        /// </summary>
        public static InstanceCells Build(IReadOnlyList<Vector3> centres, IReadOnlyList<float> radii, out int[] order)
        {
            float farthest = 0f;
            for (int i = 0; i < centres.Count; i++)
                farthest = MathF.Max(farthest, MathF.Sqrt(centres[i].X * centres[i].X + centres[i].Z * centres[i].Z));

            var cells = new InstanceCells((int)(farthest / RING_WIDTH) + 1, centres.Count);
            int count = cells.Rings * SECTORS;
            var cellOf = new int[centres.Count];
            for (int i = 0; i < centres.Count; i++)
            {
                cellOf[i] = cells.CellOf(centres[i]);
                cells.Count[cellOf[i]]++;
            }

            for (int c = 1; c < count; c++) cells.Start[c] = cells.Start[c - 1] + cells.Count[c - 1];

            order = new int[centres.Count];
            var filled = new int[count];
            var min = new Vector3[count];
            var max = new Vector3[count];
            for (int i = 0; i < centres.Count; i++)
            {
                int c = cellOf[i];
                order[cells.Start[c] + filled[c]++] = i;

                //In the sector's frame: along its bisector, up, and across it
                (Vector3 along, Vector3 across) = Frame(c % SECTORS);
                Vector3 local = new(Vector3.Dot(centres[i], along), centres[i].Y, Vector3.Dot(centres[i], across));
                Vector3 extent = new(radii[i]);
                min[c] = filled[c] == 1 ? local - extent : Vector3.Min(min[c], local - extent);
                max[c] = filled[c] == 1 ? local + extent : Vector3.Max(max[c], local + extent);
            }

            for (int c = 0; c < count; c++)
            {
                (Vector3 along, Vector3 across) = Frame(c % SECTORS);
                for (int k = 0; k < 8; k++)
                {
                    float a = (k & 1) == 0 ? min[c].X : max[c].X;
                    float y = (k & 2) == 0 ? min[c].Y : max[c].Y;
                    float b = (k & 4) == 0 ? min[c].Z : max[c].Z;
                    cells._corners[c * 8 + k] = along * a + Vector3.Up * y + across * b;
                }
            }
            return cells;
        }

        //A sector's bisector and the direction across it, both level
        private static (Vector3 Along, Vector3 Across) Frame(int sector)
        {
            float bearing = -MathF.PI + (sector + 0.5f) * MathHelper.TwoPi / SECTORS;
            return (new Vector3(MathF.Cos(bearing), 0f, MathF.Sin(bearing)), new Vector3(-MathF.Sin(bearing), 0f, MathF.Cos(bearing)));
        }

        /// <summary>
        /// The runs of consecutive cells to draw: those with anything in them whose box can meet <paramref name="frustum"/>.
        /// Written into <see cref="RunStart"/> and <see cref="RunCount"/>; returns how many.
        /// </summary>
        public int Runs(BoundingFrustum frustum)
        {
            _planes[0] = frustum.Near; _planes[1] = frustum.Far; _planes[2] = frustum.Left;
            _planes[3] = frustum.Right; _planes[4] = frustum.Top; _planes[5] = frustum.Bottom;

            int runs = 0, cells = Rings * SECTORS, c = 0;
            while (c < cells)
            {
                if (!Seen(c)) { c++; continue; }
                int first = c;
                while (c < cells && (Count[c] == 0 || Seen(c))) c++;
                RunStart[runs] = Start[first];
                RunCount[runs] = Start[c - 1] + Count[c - 1] - Start[first];
                runs++;
            }
            return runs;
        }

        //Whether a cell's box can meet the frustum Runs took: not when all eight corners lie outside one plane, which a convex
        //box then lies wholly beyond. Conservative, never wrong the other way. An empty cell never starts a run and never breaks
        //one (Runs): it has nothing to draw, and two runs either side of it are one
        private bool Seen(int c)
        {
            if (Count[c] == 0) return false;
            for (int p = 0; p < 6; p++)
            {
                Plane plane = _planes[p];
                bool allOutside = true;
                for (int k = 0; k < 8 && allOutside; k++) allOutside = plane.DotCoordinate(_corners[c * 8 + k]) > 0f;
                if (allOutside) return false;
            }
            return true;
        }
    }
}
