namespace BS3D.Screens
{
    /// <summary>
    /// What a finished benchmark recommends (#988): read from the numbers alone, never from the device's name, so a machine
    /// nobody has seen (the next Pi, an unknown GPU) needs no code. One step at a time, as the adaptive probe steps, because
    /// the run measured one tier and a tier's cost is not a scale factor of another's: a tier that drops frames in any scene
    /// recommends the one under it, a tier that holds the refresh everywhere with room to spare recommends the one over it,
    /// and anything between keeps the tier. Offered on the result page, never applied unasked. Pure: <c>BenchmarkVerdictTests</c>.
    /// </summary>
    internal static class BenchmarkVerdict
    {
        //More than this share of a scene's frames over the refresh's budget is dropping frames
        internal const float DROPS = 0.05f;

        //Every scene's 99th percentile under this share of the budget is room to spare for the tier above
        internal const float ROOM = 0.6f;

        /// <summary>
        /// The tier to offer after a run at <paramref name="tier"/>, and the index of the scene that decided it (-1 when every
        /// scene did, or none). Potato and an empty run keep the tier.
        /// </summary>
        internal static QualityLevel Recommend(QualityLevel tier, FrameStats.Summary[] results, int count, out int decidedBy)
        {
            decidedBy = -1;
            if (count <= 0 || tier == QualityLevel.Potato) return tier;

            int worst = 0;
            bool roomEverywhere = true;
            for (int i = 0; i < count; i++)
            {
                if (results[i].OverBudgetShare > results[worst].OverBudgetShare) worst = i;
                if (results[i].P99 > results[i].BudgetMilliseconds * ROOM) roomEverywhere = false;
            }

            if (results[worst].OverBudgetShare > DROPS)
            {
                decidedBy = worst;
                return tier > QualityLevel.Low ? tier - 1 : tier;
            }

            return roomEverywhere && tier < QualityLevel.Ultra ? tier + 1 : tier;
        }
    }
}
