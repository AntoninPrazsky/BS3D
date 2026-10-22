using System;

namespace BS3D.Effects
{
    /// <summary>
    /// How the floor net is shown to a player who is not in danger from it (#933): the red line is the one rule a level can be
    /// lost on and, until the cluster is near it, nothing on screen is red, so two things light it for the telling and not for
    /// the warning. Pure rules over floats, no device: the session steps it once a frame and reads what to draw.
    /// <para>
    /// <b>The reveal.</b> At the start of every level from the one the line's card is first taught on, the four emitters stand
    /// at rest (<c>LaserEmitterRing.RestAxis</c>), turn to the aim this level's net asks for over <see cref="SWING_SECONDS"/>,
    /// and the net is lit for a moment and goes out, so the player knows where the line is before the first shot. The whole of
    /// it is <see cref="TOTAL_SECONDS"/>, under the two seconds a fast player would grudge, and it is not waited for: the gun is
    /// the player's from the first frame, and the first shot <see cref="Skip"/>s the rest (the housings finish their turn at
    /// once, the light goes out in a tenth of a second and a net that was not lit yet never is).
    /// </para>
    /// <para>
    /// <b>The blink.</b> While the line's card is up (<c>Tutorial.ShowsTheLine</c>) the net blinks at its real place, on
    /// <see cref="BLINK_ON_SECONDS"/> and off the rest of <see cref="BLINK_PERIOD"/>: slower than the alarm's own pulse and
    /// dark between, so it reads as a blink and not as a warning, and a word that names the red line has a red line to be
    /// pointed at. It stops when the card does.
    /// </para>
    /// <para>
    /// <b>Game time, not wall time.</b> Both run on the seconds the session is updated with, so a pause holds them where they
    /// are and a camera takeover does too: the reveal does not begin while the chapter's tour or a drop cinematic has the lens
    /// (<c>held</c> in <see cref="Advance"/>), so it is never shown to someone watching something else. Nothing in here
    /// allocates.
    /// </para>
    /// </summary>
    internal sealed class NetReveal
    {
        /// <summary>The housings' turn from rest to aim.</summary>
        internal const float SWING_SECONDS = 0.6f;

        //The net comes on as the housings arrive: it rises over this, from the end of the swing, to a flash (the beat that says
        //"here"), settles to a steady burn at LEVEL until the hold ends, and goes out slowly
        internal const float LIGHT_RISE_SECONDS = 0.14f;
        internal const float LIGHT_HOLD_UNTIL = 1.55f;
        internal const float LIGHT_FADE_SECONDS = 0.45f;

        //How brightly the net burns, in the units the alarm's own light is drawn in (its pulse peaks at 1): above it, because a net
        //shown to someone it is not warning has to be seen against a bright sky from a camera that sees it nearly edge-on, and
        //the glare keys on the light's strength, so this is what makes the line bloom across the frame. The reveal's first beat
        //flashes over it by FLASH and settles over FLASH_SECONDS
        internal const float LEVEL = 1.4f;
        internal const float FLASH = 0.8f;
        internal const float FLASH_SECONDS = 0.4f;

        /// <summary>The whole reveal, from the first turn to the last of the light going out.</summary>
        internal const float TOTAL_SECONDS = LIGHT_HOLD_UNTIL + LIGHT_FADE_SECONDS;

        //The lens glows before the net does, charging while the housings turn: the same ember-to-glow the lens takes from the
        //net, held at this share of it until the net's own light takes over
        internal const float CHARGE = 0.7f;

        /// <summary>How much faster a skipped reveal runs: its turn is over in a tenth of a second.</summary>
        internal const float SKIP_SPEED = 6f;

        /// <summary>How long a skipped reveal's light takes to go out, from wherever it stands: a net that was not lit yet never is.</summary>
        internal const float SKIP_FADE_SECONDS = 0.12f;

        /// <summary>One blink: lit for <see cref="BLINK_ON_SECONDS"/> and dark for the rest.</summary>
        internal const float BLINK_PERIOD = 1.2f;
        internal const float BLINK_ON_SECONDS = 0.75f;

        //A blink's edges, and how fast the whole blink eases in when the card arrives and out when it goes
        private const float BLINK_EDGE = 0.1f;
        private const float BLINK_EASE_SECONDS = 0.25f;

        private bool _reveals;
        private bool _skipped;
        private float _skipFade = 1f;
        private bool _cutUnlit;
        private float _t;

        private float _blinkClock;
        private float _blinkOn;

        /// <summary>
        /// A level begins: whether it reveals the net at its start. A level that does not (the first levels, which do not
        /// mention the line) stands with its emitters aimed and the net dark; one that does stands at rest until
        /// <see cref="Advance"/> is first called with the lens free.
        /// </summary>
        internal void Reset(bool reveals)
        {
            _reveals = reveals;
            _skipped = false;
            _skipFade = 1f;
            _cutUnlit = false;
            _t = 0f;
            _blinkClock = 0f;
            _blinkOn = 0f;
        }

        /// <summary>
        /// Testing only (<c>revealshot=</c>): the clock is held at <paramref name="seconds"/> instead of running on, so a frame
        /// can be saved at a stated moment of the reveal however long a capture takes.
        /// </summary>
        internal void Pin(float seconds)
        {
            if (_reveals) _t = Math.Clamp(seconds, 0f, TOTAL_SECONDS - 1e-4f);
        }

        /// <summary>The reveal is waited out or not wanted any more: it finishes quickly, whatever stage it is at. Idempotent.</summary>
        internal void Skip()
        {
            _skipped = true;

            //A net that was not lit yet never is: the light is due when the turn ends, and the turn is only a tenth of a second away
            if (_t <= SWING_SECONDS) _cutUnlit = true;
        }

        /// <summary>Seconds of the reveal's own clock run so far: 0 until the lens is free, <see cref="TOTAL_SECONDS"/> when it is over.</summary>
        internal float Elapsed => _t;

        /// <summary>Whether a reveal is under way, or still waiting for the lens to be free: the emitters are not at their aim yet or the net is lit for it.</summary>
        internal bool Running => _reveals && _t < TOTAL_SECONDS;

        /// <summary>
        /// Steps the clocks. <paramref name="held"/> is a level not yet in the player's hands (the cluster still held for a
        /// chapter's tour, a camera takeover): the reveal waits and does not tick. <paramref name="blinking"/> is the
        /// line's card on screen and nothing covering it.
        /// </summary>
        internal void Advance(float elapsed, bool held, bool blinking)
        {
            if (_reveals && _t < TOTAL_SECONDS && !held)
                _t = MathF.Min(TOTAL_SECONDS, _t + elapsed * (_skipped ? SKIP_SPEED : 1f));

            //A skipped reveal's light goes out on its own short clock, held or not: the player is not waiting for it
            if (_skipped) _skipFade = MoveTowards(_skipFade, 0f, elapsed / SKIP_FADE_SECONDS);

            _blinkOn = MoveTowards(_blinkOn, blinking ? 1f : 0f, elapsed / BLINK_EASE_SECONDS);

            if (blinking)
            {
                _blinkClock += elapsed;
                if (_blinkClock >= BLINK_PERIOD) _blinkClock -= BLINK_PERIOD * MathF.Floor(_blinkClock / BLINK_PERIOD);
            }
            else if (_blinkOn <= 0f) _blinkClock = 0f;
        }

        /// <summary>
        /// Where the housings are between rest (0) and their aim (1), eased. A level that does not reveal is always at its
        /// aim: the emitters have stood there since the level was built.
        /// </summary>
        internal float Swing => _reveals ? SwingAt(_t) : 1f;

        /// <summary>How brightly the net is asked to burn by the reveal and by the blink, whichever is more: 0 when dark.</summary>
        internal float Net
        {
            get
            {
                //Idle - no reveal under way, no card's blink easing in or out - costs the two comparisons and nothing else
                bool revealing = _reveals && !_cutUnlit && _t < TOTAL_SECONDS;
                if (!revealing && _blinkOn <= 0f) return 0f;

                return MathF.Max(revealing ? NetAt(_t) * _skipFade : 0f, LEVEL * BlinkAt(_blinkClock) * Smooth(_blinkOn));
            }
        }

        /// <summary>The glow the lenses are asked for apart from the net's own: the charge while the housings turn.</summary>
        internal float Lamp => _reveals ? LampAt(_t) * _skipFade : 0f;

        /// <summary>The housings' turn as a function of the reveal's clock <paramref name="t"/>: 0 until it starts, 1 from the end of the swing.</summary>
        internal static float SwingAt(float t) => Smooth(t / SWING_SECONDS);

        /// <summary>
        /// The net's light as a function of the reveal's clock: dark through the turn, rising over <see cref="LIGHT_RISE_SECONDS"/> as the
        /// housings arrive, a flash on the first beat settling to <see cref="LEVEL"/>, held, and out over <see cref="LIGHT_FADE_SECONDS"/>.
        /// </summary>
        internal static float NetAt(float t)
        {
            float since = t - SWING_SECONDS;
            if (since <= 0f || t >= TOTAL_SECONDS) return 0f;

            float envelope = Smooth(since / LIGHT_RISE_SECONDS) * (1f - Smooth((t - LIGHT_HOLD_UNTIL) / LIGHT_FADE_SECONDS));
            float flash = LEVEL + FLASH * (1f - Smooth(since / FLASH_SECONDS));

            return envelope * flash;
        }

        /// <summary>The lens's charge as a function of the reveal's clock: coming up with the turn, holding, gone with the light.</summary>
        internal static float LampAt(float t)
        {
            if (t <= 0f || t >= TOTAL_SECONDS) return 0f;

            return CHARGE * Smooth(t / SWING_SECONDS) * (1f - Smooth((t - LIGHT_HOLD_UNTIL) / LIGHT_FADE_SECONDS));
        }

        /// <summary>One blink as a function of its clock in [0, <see cref="BLINK_PERIOD"/>), 0 to 1: in over the edge, lit, out over the edge, dark.</summary>
        internal static float BlinkAt(float phase) =>
            Smooth(phase / BLINK_EDGE) * (1f - Smooth((phase - (BLINK_ON_SECONDS - BLINK_EDGE)) / BLINK_EDGE));

        private static float Smooth(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static float MoveTowards(float value, float target, float step) =>
            value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);
    }
}
