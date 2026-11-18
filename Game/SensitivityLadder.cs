using System;

namespace BS3D
{
    /// <summary>
    /// The rungs the three sensitivity rows walk (#384, the mouse's, the lean's #497 and the touch screen's #1051), as
    /// multipliers over <c>MouseAim.SENSITIVITY</c>. Its own file so the tests state the − and + ends' promises
    /// (<c>RowStepTests</c>, #1151) against the ladder the game walks rather than a copy of it.
    /// <para>
    /// Written out rather than stepped like the exposure, because what a sensitivity needs is a RATIO between rungs and
    /// not a difference: 25 percentage points is a third of the way up from 75 % and an eighth of the way up from 200 %,
    /// so an even ladder is fine at one end and useless at the other. These are near-geometric (about √2 apart) and
    /// rounded to numbers a player can hold in their head.
    /// </para>
    /// <para>
    /// The span is the point and it is not decoration: the constant is an angle per PIXEL, so the two ends are what lets
    /// one machine's feel be carried to another - 0.057°/px at the bottom to 0.34°/px at the top, a sixfold range, against
    /// a 2.4× spread in width between a 1600×900 laptop and a 4K desktop and whatever the mouse's own DPI adds on top of it.
    /// </para>
    /// <para>
    /// 1 is on the ladder ON PURPOSE and is the default: the shipped feel has to be reachable, and it has to be what a
    /// player who never opens this row keeps.
    /// </para>
    /// <para>
    /// ⚠ 1.25 EXISTS BECAUSE THE AIM ROW COULD NOT REACH ITS OWN ANSWER (#477). Precise aim multiplies the cursor rate by
    /// <c>PreciseAim.CursorRateScale</c>, which at this game's pair of fields of view (42.86 deg overview, 36 deg leaned)
    /// is tan(18)/tan(21.43) = 0.828 - a 17.2 % slowdown, and geometrically the right one, since it is what makes the same
    /// hand movement cover the same screen distance in both modes. The owner's playtest reads that as too slow, and the
    /// row that is supposed to let a player answer that went 1 -> 1.5: either leave 17 % of the slowdown in or overshoot
    /// to 24 % faster than the overview. The compensating multiplier is 1/0.828 = 1.208, so 1.25 is the rung that was
    /// missing - 0.828 x 1.25 = 1.035, parity with the overview to within 3.5 %.
    /// </para>
    /// </summary>
    internal static class SensitivityLadder
    {
        private static readonly float[] RUNGS = [0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f];

        /// <summary>The rungs, lowest first.</summary>
        internal static ReadOnlySpan<float> Rungs => RUNGS;
    }
}
