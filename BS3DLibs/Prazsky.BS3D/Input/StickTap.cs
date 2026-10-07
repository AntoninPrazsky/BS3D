using System;

namespace Prazsky.BS3D.Input
{
    /// <summary>
    /// <b>A stick read as taps</b>, for the main menu's Konami code on a pad (#818): one direction on the frame the stick
    /// leaves its rest past <see cref="Press"/>, then nothing while it is held or wanders, until it is back inside
    /// <see cref="Release"/>. A pad player walks the menu with the left stick, so that is the hand the code is tried
    /// with, and a code is spelled in taps: a stick held up must not read as up, up, up.
    /// <para>
    /// Two thresholds rather than one, so a thumb resting on the edge of the press zone cannot flicker in and out of
    /// it and tap twice. A diagonal push taps the axis pushed further. Disarmed until the stick is first seen at rest,
    /// and again by <see cref="Reset"/>, so a stick already held when the menu comes up taps nothing.
    /// </para>
    /// </summary>
    public sealed class StickTap
    {
        private bool _armed;

        /// <param name="press">How far from the centre, along either axis, a push counts as a tap.</param>
        /// <param name="release">How near the centre the stick has to come back before it can tap again; below
        /// <paramref name="press"/>.</param>
        public StickTap(float press, float release)
        {
            if (!(release > 0f && release < press)) throw new ArgumentOutOfRangeException(nameof(release));

            Press = press;
            Release = release;
        }

        /// <summary>How far from the centre a push counts as a tap.</summary>
        public float Press { get; }

        /// <summary>How near the centre the stick has to return before the next tap.</summary>
        public float Release { get; }

        /// <summary>Forget the stick's state: nothing taps until it has been seen at rest.</summary>
        public void Reset() => _armed = false;

        /// <summary>
        /// One frame of the stick, y up as the pad reports it; the direction tapped on this frame, or null.
        /// </summary>
        public KonamiKey? Feed(float x, float y)
        {
            float reach = MathF.Max(MathF.Abs(x), MathF.Abs(y));

            if (!_armed)
            {
                if (reach < Release) _armed = true;
                return null;
            }

            if (reach <= Press) return null;

            _armed = false;

            return MathF.Abs(x) > MathF.Abs(y)
                ? x > 0f ? KonamiKey.Right : KonamiKey.Left
                : y > 0f ? KonamiKey.Up : KonamiKey.Down;
        }
    }
}
