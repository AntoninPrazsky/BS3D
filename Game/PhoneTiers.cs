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
    }
}
