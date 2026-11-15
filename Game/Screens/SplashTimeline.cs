using Microsoft.Xna.Framework;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The intro's clock</b> (#454, #621): when each leg of <see cref="SplashPage"/>'s sequence runs, and how strong the
    /// black, the picture and the 3D wordmark are at a given age of it. Pure functions of the age with no device in them, so
    /// <c>SplashTimelineTests</c> holds the legs to each other; the page owns the age, steps it with <see cref="Advance"/> and
    /// draws from these.
    /// </summary>
    internal static class SplashTimeline
    {
        //=== THE SEQUENCE, in seconds ===
        //
        //A starting point rather than a measurement: the issue offered figures of this shape and said the
        //owner's eye decides. Five legs since #621 — up out of black, held, the black away, a beat, the picture
        //away — and the page then replaces itself with the menu the last two legs have already been showing.

        /// <summary>Black to the logo over black.</summary>
        internal const float FADE_UP_SECONDS = 0.7f;

        /// <summary>
        /// The logo alone on black — long enough to be read, short enough that nobody reaches for the skip. It was 1.0 with a
        /// second leg after it that kept the picture fully up over the arriving scene; that leg went with the hand-over
        /// (#601), and this took half of it back so the picture is not gone sooner than it can be read.
        /// </summary>
        internal const float HOLD_SECONDS = 1.5f;

        /// <summary>
        /// The black lifts off the scene, the picture still fully up over it (#621). Slower than the fade up: this is the leg
        /// that shows the player a whole new picture — the island, the sky, the city — and a quicker lift reads as the cut it
        /// replaced.
        /// </summary>
        internal const float BLACK_OUT_SECONDS = 0.8f;

        /// <summary>
        /// The picture alone over the live scene: long enough to register the scene's arrival and the title's hand-over as two
        /// steps rather than one fade smeared into the next, short enough not to read as a second hold.
        /// </summary>
        internal const float BEAT_SECONDS = 0.4f;

        /// <summary>
        /// The picture away, and the 3D wordmark in over the same leg (<see cref="WordmarkPresence"/>). A shade quicker than
        /// the black: the scene is already there, and the flat logo only has to hand over to the corner title.
        /// </summary>
        internal const float LOGO_OUT_SECONDS = 0.7f;

        /// <summary>When the black starts to lift — the end of the part of the sequence that is on black.</summary>
        internal const float EXIT_START = FADE_UP_SECONDS + HOLD_SECONDS;

        /// <summary>When the picture starts to go.</summary>
        internal const float LOGO_OUT_START = EXIT_START + BLACK_OUT_SECONDS + BEAT_SECONDS;

        /// <summary>The whole sequence; the front end takes the page's place at the end of it.</summary>
        internal const float SECONDS = LOGO_OUT_START + LOGO_OUT_SECONDS;

        /// <summary>
        /// How much faster the sequence runs once the player has asked it to go (see <see cref="SplashPage.Update"/>): the
        /// exit's 1.9 seconds pass in about 0.6, which is quick enough to read as the press being obeyed and slow enough to
        /// still be the fade rather than the cut #621 replaced.
        /// </summary>
        internal const float SKIP_RATE = 3f;

        /// <summary>
        /// How long any input is ignored for. A splash that can be skipped on frame one is skipped by the very click that gave
        /// the window focus, and the player never sees it at all.
        /// </summary>
        internal const float SKIP_AFTER = 0.35f;

        /// <summary>
        /// The age a frame of <paramref name="elapsed"/> wall seconds later: the legs last the same at any frame rate, and a
        /// skip only scales them.
        /// </summary>
        internal static float Advance(float age, float elapsed, bool skipping) => age + elapsed * (skipping ? SKIP_RATE : 1f);

        /// <summary>
        /// Where a skipping sequence stands: an age inside the hold is moved on to <see cref="EXIT_START"/>, since a player
        /// who asked for the menu has read the logo, and any other age is left where it is — the fade up finishes (quickly)
        /// rather than popping a half-faded logo to full, and an exit already under way goes on from where it is.
        /// </summary>
        internal static float SkipHold(float age) => age >= FADE_UP_SECONDS && age < EXIT_START ? EXIT_START : age;

        //Each leg a smoothstep of its own share of the clock; SmoothStep clamps its amount, so outside its leg each term is
        //simply its end value

        /// <summary>How opaque the black over the frame is: solid, then away over <see cref="BLACK_OUT_SECONDS"/>.</summary>
        internal static float Black(float age) => 1f - MathHelper.SmoothStep(0f, 1f, (age - EXIT_START) / BLACK_OUT_SECONDS);

        /// <summary>
        /// How present the picture is: up out of the black, held, then away over <see cref="LOGO_OUT_SECONDS"/> — the black
        /// gone for good before it starts to go (the beat between them).
        /// </summary>
        internal static float Logo(float age) =>
            MathHelper.SmoothStep(0f, 1f, age / FADE_UP_SECONDS)
            * (1f - MathHelper.SmoothStep(0f, 1f, (age - LOGO_OUT_START) / LOGO_OUT_SECONDS));

        /// <summary>
        /// How present the 3D wordmark is under the page, 0 to 1 (#621): zero through the legs on black, the black's lift and
        /// the beat — the picture is the only title while it is fully up — then a smoothstep to one over exactly the leg the
        /// picture fades out on, so the two cross over. See <see cref="SplashPage.WordmarkPresence"/>.
        /// </summary>
        internal static float WordmarkPresence(float age) => MathHelper.SmoothStep(0f, 1f, (age - LOGO_OUT_START) / LOGO_OUT_SECONDS);
    }
}
