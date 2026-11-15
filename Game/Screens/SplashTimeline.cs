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
        //A starting point rather than a measurement: the issues offered figures of this shape and said the owner's
        //eye decides. Up out of black, held, then ONE dissolve (#1105): the black lifts, and a moment after it has
        //begun the picture starts to go too, trailing it, so the logo is gone a moment after the black rather than
        //standing alone over a scene that has already fully arrived. The page then replaces itself with the menu
        //the dissolve has already been showing.

        /// <summary>Black to the logo over black.</summary>
        internal const float FADE_UP_SECONDS = 0.7f;

        /// <summary>
        /// The logo alone on black — long enough to be read, short enough that nobody reaches for the skip. It was 1.0 with a
        /// second leg after it that kept the picture fully up over the arriving scene; that leg went with the hand-over
        /// (#601), and this took half of it back so the picture is not gone sooner than it can be read.
        /// </summary>
        internal const float HOLD_SECONDS = 1.5f;

        /// <summary>
        /// The black lifts off the scene (#621). Slower than the fade up: this is the leg that shows the player a whole new
        /// picture — the island, the sky, the city — and a quicker lift reads as the cut it replaced.
        /// </summary>
        internal const float BLACK_OUT_SECONDS = 0.8f;

        /// <summary>
        /// How far the picture's fade trails the black's (#1105): it starts this long after the black has begun to lift, well
        /// inside the lift, so the two read as one dissolve with the logo a little behind — the PNG's transparency lets the
        /// arriving scene show round its letters while it goes. Until #1105 the picture waited for the black to be gone and
        /// then a beat more, and the scene stood fully revealed under a logo that had not yet begun to leave, which the owner
        /// saw as the black melting into the menu faster than the logo. How long the lag should be is the owner's eye.
        /// </summary>
        internal const float LOGO_LAG_SECONDS = 0.25f;

        /// <summary>
        /// The picture away, and the 3D wordmark in over the same leg (<see cref="WordmarkPresence"/>). A shade quicker than
        /// the black, so with <see cref="LOGO_LAG_SECONDS"/> the logo is gone shortly after the black rather than long after it.
        /// </summary>
        internal const float LOGO_OUT_SECONDS = 0.7f;

        /// <summary>When the black starts to lift — the end of the part of the sequence that is on black.</summary>
        internal const float EXIT_START = FADE_UP_SECONDS + HOLD_SECONDS;

        /// <summary>When the picture starts to go: <see cref="LOGO_LAG_SECONDS"/> into the black's lift.</summary>
        internal const float LOGO_OUT_START = EXIT_START + LOGO_LAG_SECONDS;

        /// <summary>
        /// The whole sequence; the front end takes the page's place at the end of it. The picture's fade ends last (the black's
        /// lift ends <c>LOGO_LAG_SECONDS + LOGO_OUT_SECONDS - BLACK_OUT_SECONDS</c> before it), so this is where it ends.
        /// </summary>
        internal const float SECONDS = LOGO_OUT_START + LOGO_OUT_SECONDS;

        /// <summary>
        /// How much faster the sequence runs once the player has asked it to go (see <see cref="SplashPage.Update"/>): the
        /// exit's <see cref="LOGO_LAG_SECONDS"/> + <see cref="LOGO_OUT_SECONDS"/> pass in a third of the time, quick enough to
        /// read as the press being obeyed and still the fade rather than the cut #621 replaced. The rate scales the age, so a
        /// skipped exit keeps the same overlap, the logo a third of the lag behind the black.
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
        /// How present the picture is: up out of the black, held, then away over <see cref="LOGO_OUT_SECONDS"/>, starting
        /// <see cref="LOGO_LAG_SECONDS"/> into the black's lift — so through the dissolve it is always at least as present as
        /// the black, and outlasts it.
        /// </summary>
        internal static float Logo(float age) =>
            MathHelper.SmoothStep(0f, 1f, age / FADE_UP_SECONDS)
            * (1f - MathHelper.SmoothStep(0f, 1f, (age - LOGO_OUT_START) / LOGO_OUT_SECONDS));

        /// <summary>
        /// How present the 3D wordmark is under the page, 0 to 1 (#621): zero through the legs on black and the start of the
        /// black's lift — the picture is the only title while it is fully up — then a smoothstep to one over exactly the leg
        /// the picture fades out on, so the two cross over: the wordmark is the picture's complement, never at full strength
        /// while the picture is. See <see cref="SplashPage.WordmarkPresence"/>.
        /// </summary>
        internal static float WordmarkPresence(float age) => MathHelper.SmoothStep(0f, 1f, (age - LOGO_OUT_START) / LOGO_OUT_SECONDS);
    }
}
