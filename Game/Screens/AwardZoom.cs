using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Scoring;
using System;
using System.Globalization;

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
        /// From birth to the moment it leaves: the read, zoom included (<see cref="Hold(int)"/>). A bare match stays quick,
        /// about what the whole reading leg used to be (0.5 s), so a run of ordinary shots does not leave numbers parked
        /// over the cluster; a big release holds long enough to read a four-digit figure and its multiplier.
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
        /// The longest the completion bonus waits, after the field empties, for the award that cleared it to be seen
        /// (<see cref="BonusWait"/>): a tenth of a second, so the clearing shot's number is on its way up before the bonus
        /// bursts in over the aim point, and no more, because the beat (<see cref="LevelPhases.ClearedBeatSeconds"/>) is
        /// shared out as this, <see cref="BONUS_HOLD"/>, the flight and <see cref="BONUS_COUNT_IN"/> (#881). The clearing
        /// award is still read for <see cref="MIN_READ"/> from its own birth before it leaves, as any superseded award is,
        /// so the two numbers share the middle of the frame for about the bonus's first half second, while the older
        /// one shrinks away: the hand-off of any two awards.
        /// </summary>
        public const float BONUS_WAIT_MAX = 0.1f;

        /// <summary>
        /// How long the completion bonus is read, from its birth to the moment it leaves for the corner, zoom included
        /// (#881): <b>1.5 s</b>, against <see cref="HOLD_BIG"/> at the most for an award over a cell, because the words say
        /// what the figure counts and must be read, not glanced at. The zoom takes up to <see cref="BIRTH_BIG"/> of it, so the
        /// words stand at full size for about a second and a quarter. It is what the beat has left once the wait, the flight
        /// and the count-in are taken out of it (<c>AwardZoomTests</c> holds the sum to
        /// <see cref="LevelPhases.ClearedBeatSeconds"/>), so reading it longer would delay the result page.
        /// </summary>
        public const float BONUS_HOLD = 1.5f;

        /// <summary>
        /// The least time the corner gets to count the completion bonus in between its landing and the result page,
        /// which hides the HUD. The readout closes a share of the gap a second (<c>PlayHud.SCORE_CHASE_PER_SECOND</c>),
        /// so over this it shows nearly nine tenths of the bonus; a bonus landing just before the page would be cut off
        /// a little over half counted.
        /// </summary>
        public const float BONUS_COUNT_IN = 0.35f;

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

        /// <summary>
        /// How long the award is read before it leaves, zoom included, in seconds. On the <b>square</b> of the weight, where
        /// the size is on the weight itself: the size is the brag and costs the player nothing, the read is the time a
        /// number stands over the cluster being aimed at, so it stays near <see cref="HOLD_SMALL"/> through every ordinary
        /// shot (a match of three on the capped streak, 150 points, is read 0.65 s) and lengthens only for a real release.
        /// </summary>
        public static float Hold(int points)
        {
            float weight = Weight(points);
            return Lerp(HOLD_SMALL, HOLD_BIG, weight * weight);
        }

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

        /// <summary>
        /// The tallest the read ever draws an award in scores (#881): the top of the zoom's overshoot, or the swell the read
        /// has grown to by <paramref name="hold"/>, whichever is bigger. The completion bonus is placed by it, so the block
        /// that zooms about its centre never reaches down past the line it is held clear of the crosshair by.
        /// </summary>
        public static float ReadMax(float peak, float birth, float hold) =>
            peak * MathF.Max(BIRTH_FROM + (1f - BIRTH_FROM) * HudEase.OUT_BACK_PEAK, 1f + SWELL_PER_SECOND * MathF.Max(0f, hold - birth));

        /// <summary>The share of the rise reached at <paramref name="age"/>, 0 at birth towards 1; it stops where the read
        /// does, because past that the flight owns where the number goes and a source still climbing would fight it.</summary>
        public static float Rise(float age, float hold) =>
            1f - MathF.Exp(-MathF.Max(0f, MathF.Min(age, hold)) / RISE_TAU);

        /// <summary>
        /// The new hold of an award still being read when a newer one is born (#846): it <b>leaves now</b>, unless it has
        /// not yet been read for <see cref="MIN_READ"/>, in which case it leaves then. Never later than it would have, and
        /// never earlier than its own age, so its size does not jump.
        /// <para>
        /// Leaving is not vanishing: it <b>shrinks where it stands</b> over <see cref="SHRINK_SHARE"/> of the throw while
        /// the newer one zooms in, and stays larger than the score for under a quarter of a second of that
        /// (<c>AwardZoomTests</c>). So two numbers larger than the score do stand together at a hand-off, for that
        /// quarter second however far apart the landings are, and for what was left of <see cref="MIN_READ"/> longer when
        /// they are closer than it: a hand-off, the newer one bursting in as the older one shrinks off.
        /// </para>
        /// </summary>
        public static float HandOff(float age, float hold) => MathF.Min(hold, MathF.Max(age, MIN_READ));

        /// <summary>
        /// How long a cut's award waits before it is born on the struck cell (#767), in the <b>world's</b> seconds: half the
        /// slash's life (<see cref="BS3D.Effects.CutBeats.SLASH_SECONDS"/>), when it is past its white heat (wholly white-hot for
        /// its first fifth), at half its light and two thirds of its thickness and cooling towards amber, so the number zooms in
        /// over a cut already made instead of over the cut being made. On the world's clock because the slash lives on it:
        /// the strike's slow motion stretches both alike, however deep it runs (about 1.1 s of the play clock for a cut that
        /// freed nothing, 1.6 s for a full-size one). A wait on the play clock stood the number over a slash still
        /// white-hot whenever the slow motion ran deep. A cut that clears the level with no drop cinematic over it is born
        /// at once (<see cref="CutWaitLeft"/>): it must be read and flown into the score inside the cleared beat.
        /// </summary>
        public const float CUT_WORLD_WAIT = BS3D.Effects.CutBeats.SLASH_SECONDS * 0.5f;

        /// <summary>
        /// What is left of a cut's award's wait in the world's seconds (#767) when a level-clearing cut is the shot: none, unless
        /// a drop cinematic has the lens. The wait is in the world's seconds and the strike's slow motion stretches them to 1.1
        /// to 1.6 s of the play clock, while the cleared beat is <see cref="LevelPhases.ClearedBeatSeconds"/> and the award
        /// still needs its read and its flight (<see cref="Lifetime"/>); a number born that late is cut mid-read by the result
        /// page and never flies into the score. Under a cinematic the world runs at game time and the beat is held for it.
        /// </summary>
        /// <param name="worldLeft">What the award still waits, in the world's seconds.</param>
        /// <param name="levelCleared">Whether the field has just emptied.</param>
        /// <param name="dropCinematic">Whether a drop cinematic has the lens.</param>
        public static float CutWaitLeft(float worldLeft, bool levelCleared, bool dropCinematic) =>
            levelCleared && !dropCinematic ? 0f : worldLeft;

        /// <summary>How long the completion bonus waits before it is born, given the longest any award still has to be
        /// read: until that read ends, but never more than <see cref="BONUS_WAIT_MAX"/>, after which it takes the read as
        /// a newer award does (<see cref="HandOff"/>).</summary>
        public static float BonusWait(float longestReadLeft) => Math.Clamp(longestReadLeft, 0f, BONUS_WAIT_MAX);

        /// <summary>
        /// What the completion bonus says it counts (#881), over its figure: the balls the level was cleared with to spare,
        /// in the HUD's own noun ("balls left" is what the count in the corner reads while they are in hand), capitalised
        /// as the lost streak's words are. <c>12 UNUSED BALLS</c>, <c>1 UNUSED BALL</c>. Built once, at birth.
        /// </summary>
        public static string BonusCaption(int unusedBalls) =>
            unusedBalls.ToString(CultureInfo.InvariantCulture) + (unusedBalls == 1 ? " UNUSED BALL" : " UNUSED BALLS");

        private static float ReadSize(float age, float birth, float peak)
        {
            if (age < birth) return peak * Lerp(BIRTH_FROM, 1f, HudEase.OutBack(MathF.Max(0f, age) / birth));
            return peak * (1f + SWELL_PER_SECOND * (age - birth));
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp01(float t) => Math.Clamp(t, 0f, 1f);
    }
}
