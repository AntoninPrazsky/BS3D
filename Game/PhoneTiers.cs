using System;

namespace BS3D
{
    /// <summary>
    /// What the quality ladder is on a phone (#1060), from what #1061 measured on the vivo X300 Ultra through the desktop
    /// path ("The phone" in <c>docs/game-shell/quality-measurements.md</c>). Everywhere else each answer is the desktop's.
    /// <list type="bullet">
    /// <item>Only <see cref="QualityLevel.Low"/> at 1080 rows held 60 FPS in the city and the meadow; at native even Low
    /// was 47-48. So the 3D is drawn at 1080 rows until the player picks another step on the Resolution row.</item>
    /// <item>High's supersampling (four times the pixels) read 10-17 FPS, so a phone never supersamples, and Ultra,
    /// which only adds a doubled shadow map to that, is not on the row.</item>
    /// <item>A first run starts at Medium and Auto quality measures it, as the desktop's starts at High: the X300 Ultra's
    /// 47-52 FPS there is under its floor, so it steps to Low; a stronger phone keeps Medium, and a weaker one goes on
    /// from Low to Potato, from the next start. A weak phone is never left on High, which only the row reaches.</item>
    /// <item>Auto quality holds <see cref="TARGET_FPS"/> whatever the display refreshes at (#1070): a 120 Hz display
    /// raises the frame rate where a frame has the time, and never costs a tier or rows.</item>
    /// </list>
    /// </summary>
    internal static class PhoneTiers
    {
        /// <summary>Whether this is a phone: the Android build.</summary>
        internal static bool Phone => OperatingSystem.IsAndroid();

        /// <summary>The tier a run with nothing chosen starts at, and Auto quality's ceiling.</summary>
        internal static QualityLevel StartingTier => Phone ? QualityLevel.Medium : QualityLevel.High;

        /// <summary>The 3D's height when nothing chose one, 0 for the display's own.</summary>
        internal const int PHONE_RENDER_HEIGHT = 1080;

        internal static int DefaultRenderHeight => Phone ? PHONE_RENDER_HEIGHT : 0;

        /// <summary>The largest supersample factor a tier or <c>ssaa=</c> may set.</summary>
        internal static int MaxSupersample => Phone ? 1 : 4;

        /// <summary>Whether the Quality row offers Ultra.</summary>
        internal static bool OffersUltra => !Phone;

        /// <summary>
        /// The frame rate a phone's Auto quality holds (the owner's target for a phone's look, #1056). Off the display's
        /// 120 Hz the floors would be 108 and 114 FPS, which no tier of the desktop's renderer holds on the X300 Ultra:
        /// the probe would step a phone that draws Medium at 60 down to Low and ask for Potato.
        /// </summary>
        internal const int TARGET_FPS = 60;

        /// <summary>The refresh Auto quality's floors come off: the display's, and on a phone at most <see cref="TARGET_FPS"/>.</summary>
        internal static float FloorRefresh(float refreshHz) => FloorRefresh(refreshHz, Phone);

        /// <inheritdoc cref="FloorRefresh(float)"/>
        internal static float FloorRefresh(float refreshHz, bool phone) => phone ? Math.Min(refreshHz, TARGET_FPS) : refreshHz;

        /// <summary>The slowest refresh that counts as a high one: a display offering it and running at 60 is being held.</summary>
        internal const int HIGH_REFRESH_HZ = 90;

        /// <summary>
        /// What the FPS limit row reads (#124, #1070). A monitor's cap is "Monitor", never a number, which would go stale
        /// the moment the window lands on another one. A phone has one display and names its rate, because the rate is
        /// what its player can change: a phone holds an app at 60 Hz until the player allows it a high refresh rate in the
        /// phone's own settings (measured on the vivo X300 Ultra: nothing the app declares or asks for lifts it), and the
        /// row is the one place in the game that says where the 60 comes from.
        /// </summary>
        /// <param name="uncapped">The player's own choice of no limit.</param>
        /// <param name="phone">Whether this is a phone (<see cref="Phone"/>).</param>
        /// <param name="refreshHz">The display's refresh now, 0 when it does not say.</param>
        /// <param name="fastestHz">The fastest refresh among the display's modes, 0 when it does not say.</param>
        internal static string FpsLimitLabel(bool uncapped, bool phone, int refreshHz, int fastestHz)
        {
            if (uncapped) return "Unlimited";
            if (!phone || refreshHz <= 0) return "Monitor";

            return HeldByPhone(phone, refreshHz, fastestHz) ? $"{refreshHz} Hz (phone setting)" : $"{refreshHz} Hz";
        }

        /// <summary>
        /// Whether the phone itself holds the game under what its display offers (#1070): a refresh at or under
        /// <see cref="TARGET_FPS"/> on a display with a mode of <see cref="HIGH_REFRESH_HZ"/> or more.
        /// </summary>
        internal static bool HeldByPhone(bool phone, int refreshHz, int fastestHz) =>
            phone && refreshHz > 0 && refreshHz <= TARGET_FPS && fastestHz >= HIGH_REFRESH_HZ;
    }
}
