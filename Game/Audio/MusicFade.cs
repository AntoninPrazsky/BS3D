using System;

namespace BS3D.Audio
{
    /// <summary>
    /// One instance's departure or return (#211): a level walking towards a target at a stated full-scale time,
    /// applied to the voice <b>squared</b> — the procedural arrangements' own outro curve, whose tail reads
    /// smoother than a straight line. It is a multiplier over the authored volume and the player's gain, so a
    /// fade and a settings change cannot fight over one <c>Volume</c> property.
    /// <para>
    /// Its own file since #443, because three players share it: <see cref="GameMusic"/> (the theme's retiring
    /// chain, the menu loop, and yielding to the About page's player), <see cref="ProceduralMusic"/> (the
    /// fanfare), and <c>Tools/MusicBake</c>, which compiles the latter as source and therefore this with it.
    /// </para>
    /// </summary>
    internal sealed class MusicFade
    {
        private float _seconds = 1f;

        public float Level { get; private set; } = 1f;
        public float Target { get; private set; } = 1f;

        /// <summary>What the instance's authored volume is multiplied by.</summary>
        public float Applied => Level * Level;

        /// <summary>True once a fade-out has arrived — the frame to actually stop the instance on.</summary>
        public bool Silent => Target == 0f && Level == 0f;

        public void To(float target, float seconds)
        {
            Target = target;
            _seconds = seconds;
        }

        /// <summary>Rest at full, instantly — what a chain being RETIRED starts its fade-out from.</summary>
        public void Reset()
        {
            Level = 1f;
            Target = 1f;
        }

        /// <summary>
        /// Rest at silence, then walk to full over <paramref name="seconds"/> — what a fresh chain's own
        /// ARRIVAL does (#456): unlike <see cref="Reset"/>, which puts a piece on at once, this is for the
        /// piece a caller is bringing UP rather than one already sounding at its authored level.
        /// </summary>
        public void Arrive(float seconds)
        {
            Level = 0f;
            Target = 1f;
            _seconds = seconds;
        }

        /// <summary>Walks the level one frame towards the target. True when it moved.</summary>
        public bool Advance(float elapsed)
        {
            if (Level == Target) return false;

            float step = elapsed / _seconds;

            Level = Level > Target
                ? MathF.Max(Target, Level - step)
                : MathF.Min(Target, Level + step);

            return true;
        }
    }
}
