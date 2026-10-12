using Prazsky.BS3D.Scoring;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>How big an award is, and for how long</b> (#846): the popup's size and its legs as pure functions of its age and
    /// its points, so the tests hold them without a window (<c>AwardZoomTests</c>) and <c>PlayHud</c> only draws them.
    /// <para>
    /// The owner's playtest of 2026-10-11: the points gained should be shown bigger, "a zoom animation, then shrinking
    /// again and flying off into the corner, so the player perceives more how many points they are getting". Until then
    /// the award peaked at 0.8 of the score it lands on and was gone in about a second. So it now <b>zooms well past the
    /// score's size</b> (<see cref="PEAK_SMALL"/> for a bare match, up to <see cref="PEAK_BIG"/> for a big release),
    /// <b>holds to be read</b> (longer the bigger it is), then <b>shrinks where it stands</b> and is <b>thrown into the
    /// corner</b> as before, arriving at <see cref="ARRIVE"/>.
    /// </para>
    /// <para>
    /// <b>Sizes are in scores</b>: multiples of the corner score's type (<c>BS3DGame.HUD_FONT_SCORE</c>), because "past the
    /// score's size" is the brief. <b>Seconds are the play clock's</b>, like every other HUD motion.
    /// </para>
    /// </summary>
    internal static class AwardZoom
    {
        /// <summary>The least a scoring landing pays: a bare match of three at ×1. It zooms to <see cref="PEAK_SMALL"/>.</summary>
        public const int SMALLEST_POINTS = 3 * ScoreKeeper.MatchedBallPoints;

        /// <summary>
        /// An award this big or bigger is a big release and zooms all the way to <see cref="PEAK_BIG"/>: about a hundred
        /// balls let go at ×1, or a few dozen on a running streak. In between, the award's share of the way is
        /// <b>logarithmic</b> in its points (<see cref="Weight"/>): ×3 a bare match's points reads as a step, not as a
        /// sliver of a linear scale a release of a thousand would own.
        /// </summary>
        public const int BIG_POINTS = 2000;

        /// <summary>The smallest award's top size, half as tall again as the score it will land on.</summary>
        public const float PEAK_SMALL = 1.5f;

        /// <summary>A big release's top size: three times the score.</summary>
        public const float PEAK_BIG = 3f;

        /// <summary>Where the zoom starts from, as a share of the top size: near nothing, so it bursts in.</summary>
        public const float BIRTH_FROM = 0.15f;

        /// <summary>The zoom in, from <see cref="BIRTH_FROM"/> to the top size through <see cref="HudEase.OutBack"/>'s
        /// overshoot: quick for a bare match, a beat longer for a release so the growth is seen as growth.</summary>
        public const float BIRTH_SMALL = 0.2f, BIRTH_BIG = 0.32f;

        /// <summary>
        /// From birth to the moment it leaves: the read, zoom included. A bare match stays quick, about what the whole
        /// reading leg used to be (0.5 s), so a run of ordinary shots does not leave numbers parked over the cluster; a
        /// big release holds long enough to read a four-digit figure and its multiplier.
        /// </summary>
        public const float HOLD_SMALL = 0.55f, HOLD_BIG = 1.2f;

        /// <summary>The slow growth over the read once the zoom has landed, as a share of the top size a second: a number
        /// standing dead still reads as a label, one still swelling a little reads as still arriving.</summary>
        public const float SWELL_PER_SECOND = 0.05f;

        /// <summary>The throw into the corner, in seconds. Past about half a second an object crossing the screen stops
        /// reading as thrown and starts reading as drifting.</summary>
        public const float FLIGHT = 0.55f;

        /// <summary>The size it lands at, half the score's: what the flight used to arrive at (0.62 of the old 112-unit
        /// popup type, 69 design units), kept, because the corner's own kick is what finishes the gesture.</summary>
        public const float ARRIVE = 0.5f;

        /// <summary>
        /// The share of the flight over which it shrinks from its read size to <see cref="ARRIVE"/>: exactly the part in
        /// which the throw's wind-up (<see cref="HudEase.InBack"/>) is still gathering behind the point it was read at.
        /// So it <b>shrinks where it stands, then flies</b>, the owner's order, and a big number is never thrown across
        /// the frame at full size.
        /// </summary>
        public const float SHRINK_SHARE = HudEase.IN_BACK_DEPARTURE;

        /// <summary>The time constant of the rise over the cluster: most of it in the first half second, then easing to a
        /// stop, so a long read does not climb on into the glass. The rise ends when the read does.</summary>
        public const float RISE_TAU = 0.25f;

        /// <summary>
        /// The shortest read a superseded award is cut to (<see cref="HandOff"/>). Longer than the longest zoom
        /// (<see cref="BIRTH_BIG"/>), so every award is seen at its full size before it goes.
        /// </summary>
        public const float MIN_READ = 0.4f;

        /// <summary>
        /// The longest the completion bonus waits for the award that cleared the field to finish being read
        /// (<see cref="BonusWait"/>). Bounded so the bonus lands inside the cleared beat (2.5 s, <c>LEVEL_CLEARED_BEAT</c>):
        /// this, the longest read and the flight together come to 2.35 s.
        /// </summary>
        public const float BONUS_WAIT_MAX = 0.6f;

        /// <summary>The largest an award is ever drawn, in scores: <see cref="PEAK_BIG"/> at the top of the zoom's
        /// overshoot. The tallest of the HUD's award fonts is rasterised at this, so a glyph is only ever shrunk.</summary>
        public const float LARGEST = PEAK_BIG * (BIRTH_FROM + (1f - BIRTH_FROM) * HudEase.OUT_BACK_PEAK);

        /// <summary>How far <paramref name="points"/> is towards a big release: 0 for a bare match, 1 from
        /// <see cref="BIG_POINTS"/> up, logarithmic between.</summary>
        public static float Weight(int points)
        {
            if (points <= SMALLEST_POINTS) return 0f;
            return MathF.Min(1f, MathF.Log(points / (float)SMALLEST_POINTS) / MathF.Log(BIG_POINTS / (float)SMALLEST_POINTS));
        }

        /// <summary>The top of the zoom for an award of <paramref name="points"/>, in scores.</summary>
        public static float Peak(int points) => Lerp(PEAK_SMALL, PEAK_BIG, Weight(points));

        /// <summary>How long the zoom in takes, in seconds.</summary>
        public static float Birth(int points) => Lerp(BIRTH_SMALL, BIRTH_BIG, Weight(points));

        /// <summary>How long the award is read before it leaves, zoom included, in seconds.</summary>
        public static float Hold(int points) => Lerp(HOLD_SMALL, HOLD_BIG, Weight(points));

        /// <summary>From birth to landing in the corner.</summary>
        public static float Lifetime(float hold) => hold + FLIGHT;

        /// <summary>How far through the throw it is: 0 while it is being read, 1 on landing.</summary>
        public static float Flight(float age, float hold) => Clamp01((age - hold) / FLIGHT);

        /// <summary>
        /// The award's size in scores at <paramref name="age"/>. <b>Continuous</b> everywhere, the handover from read to
        /// throw included and whatever the hold was cut to (<see cref="HandOff"/>): the throw starts from the size the
        /// read ended at, so nothing snaps.
        /// </summary>
        public static float Size(float age, float hold, float birth, float peak)
        {
            if (age < hold) return ReadSize(age, birth, peak);

            float shrink = Clamp01(Flight(age, hold) / SHRINK_SHARE);
            return Lerp(ReadSize(hold, birth, peak), ARRIVE, shrink * shrink * (3f - 2f * shrink));
        }

        /// <summary>The share of the rise reached at <paramref name="age"/>, 0 at birth towards 1; it stops where the read
        /// does, because past that the flight owns where the number goes and a source still climbing would fight it.</summary>
        public static float Rise(float age, float hold) =>
            1f - MathF.Exp(-MathF.Max(0f, MathF.Min(age, hold)) / RISE_TAU);

        /// <summary>
        /// The new hold of an award still being read when a newer one is born (#846): it <b>leaves now</b>, so two numbers
        /// are never read at once, unless it has not yet been read for <see cref="MIN_READ"/>, in which case it leaves
        /// then. Never later than it would have, and never earlier than its own age, so its size does not jump.
        /// </summary>
        public static float HandOff(float age, float hold) => MathF.Min(hold, MathF.Max(age, MIN_READ));

        /// <summary>How long the completion bonus waits before it is born, given the longest any award still has to be
        /// read: until that read ends, but never more than <see cref="BONUS_WAIT_MAX"/>.</summary>
        public static float BonusWait(float longestReadLeft) => Math.Clamp(longestReadLeft, 0f, BONUS_WAIT_MAX);

        private static float ReadSize(float age, float birth, float peak)
        {
            if (age < birth) return peak * Lerp(BIRTH_FROM, 1f, HudEase.OutBack(MathF.Max(0f, age) / birth));
            return peak * (1f + SWELL_PER_SECOND * (age - birth));
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp01(float t) => Math.Clamp(t, 0f, 1f);
    }
}
