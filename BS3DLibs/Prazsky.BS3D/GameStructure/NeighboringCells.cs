using Prazsky.BS3D.GameStructure.DataBags;
using System.Runtime.CompilerServices;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// The cells touching one cell, walked by a <c>foreach</c> that allocates nothing (#381). What
    /// <see cref="BallsMap.GetNeighboringCells"/> hands back.
    /// <para>
    /// It exists because the walk left the shot path and joined the <b>frame</b> path. While only a landing asked
    /// which cells touch which, the iterator this replaced cost one enumerator per shot and nobody could measure
    /// it; #70 let the aim preview ask the identical question, and from then on the same iterator was allocating
    /// once a frame from <see cref="BallsMap.TryFindEmptyCellNextTo"/> and up to ten times a frame from
    /// <see cref="BallsMap.TryFindEmptyCellInSecondRing"/>, whose walk is nested. That is
    /// <c>BestPractices.md</c> §3's own recorded incident a second time, down to the shape of the sentence: a
    /// <c>yield return</c> method that was fine until something made it per-frame.
    /// </para>
    /// <para>
    /// <b>Every cell is found in the constructor and this only walks an index.</b> The alternative — a hand-rolled
    /// state machine resuming the parity arithmetic between calls — buys back the 152 bytes this occupies on the
    /// stack and pays for them with the one thing that must not be got wrong here: the <i>order</i> the cells come
    /// out in is load-bearing (see <see cref="BallsMap.FillNeighboringCells"/>), and a buffer filled by one
    /// straight-line pass can be read against the pass it replaced line by line.
    /// The copies are stack traffic — this is a plain struct, so a <c>foreach</c> over it touches the heap nowhere.
    /// </para>
    /// </summary>
    public struct NeighboringCells
    {
        /// <summary>
        /// The found cells. An inline array rather than a rented or pooled buffer because the count is fixed and
        /// tiny: it lives inside this struct, so it is stack storage wherever the struct is.
        /// </summary>
        [InlineArray(BallsMap.MAX_NEIGHBORS)]
        private struct Cells
        {
            private XZLevel _element0;
        }

        private Cells _cells;

        private readonly int _count;

        private int _index;

        internal NeighboringCells(XZLevel cell, XZLevel size)
        {
            _cells = default;
            _count = BallsMap.FillNeighboringCells(cell, size, _cells);
            _index = -1;
        }

        /// <summary>
        /// The <c>foreach</c> pattern's hook. Hands back a copy of this — already positioned before the first
        /// cell — so the walk cannot disturb the value it was called on and two nested walks over the same cell
        /// keep separate positions. <see cref="BallsMap.TryFindEmptyCellInSecondRing"/> is that nesting.
        /// </summary>
        public readonly NeighboringCells GetEnumerator() => this;

        /// <summary>The cell reached, meaningful only after <see cref="MoveNext"/> has returned true.</summary>
        public readonly XZLevel Current => _cells[_index];

        /// <summary>Steps to the next cell; false once every one of them has been handed out.</summary>
        public bool MoveNext() => ++_index < _count;
    }
}
