namespace BS3D.Screens
{
    /// <summary>
    /// The HUD's two eases: the pop-in that overshoots and the wind-up before a throw. Their own file, with no device in
    /// them, because the award's zoom (<see cref="AwardZoom"/>) is built on both and is tested without a window.
    /// </summary>
    internal static class HudEase
    {
        //How far OutBack carries past 1 before it settles: 1 + C3·u³ + C1·u² peaks at u = -2·C1/(3·C3) (OUT_BACK_PEAK)
        private const float OUT_C1 = 2.8f;
        private const float OUT_C3 = OUT_C1 + 1f;

        //How far InBack dips under 0 before it goes: C3·t³ - C1·t² bottoms out about 6.6 % under at t ≈ 0.38
        private const float IN_C1 = 1.35f;
        private const float IN_C3 = IN_C1 + 1f;

        /// <summary>The top of <see cref="OutBack"/>'s overshoot: 1 + 4·C1³ / (27·C3²), about 1.225.</summary>
        public const float OUT_BACK_PEAK = 1f + 4f * OUT_C1 * OUT_C1 * OUT_C1 / (27f * OUT_C3 * OUT_C3);

        /// <summary>
        /// Where <see cref="InBack"/> comes back through 0 after its dip: the moment a thrown thing has finished
        /// gathering itself and passes the point it was thrown from. <c>C1 / C3</c>, about 0.57.
        /// </summary>
        public const float IN_BACK_DEPARTURE = IN_C1 / IN_C3;

        /// <summary>Overshoots past 1 and settles back — the pop-in.</summary>
        public static float OutBack(float t)
        {
            float u = t - 1f;
            return 1f + OUT_C3 * u * u * u + OUT_C1 * u * u;
        }

        /// <summary>Dips below 0 first and then accelerates — the wind-up before the throw.</summary>
        public static float InBack(float t) => IN_C3 * t * t * t - IN_C1 * t * t;
    }
}
