using System;

namespace Prazsky.Core.Tools
{
    /// <summary>
    /// The peak caps of a spectrum display (#730): per band, the highest the level has lately been, held for a moment and
    /// then <b>dropped under gravity</b> until it meets the level again, where it rests. Pure arithmetic over arrays made
    /// once, so the widget that draws it allocates nothing, and the tests can state the promises without a window: the
    /// fall is in seconds and never in frames (the same cap falls the same distance at 60 and at 240 Hz), a cap is never
    /// below its level, and a cap with nothing pushing it ends on the floor.
    /// <para>
    /// Levels and caps are in column heights, 0 at the floor and 1 at the top of the column; gravity is in column
    /// heights a second squared.
    /// </para>
    /// </summary>
    public sealed class PeakHold
    {
        private readonly float[] _peak, _fall, _hold;
        private readonly float _holdSeconds, _gravity;

        public PeakHold(int count, float holdSeconds, float gravity)
        {
            _peak = new float[count];
            _fall = new float[count];
            _hold = new float[count];
            _holdSeconds = holdSeconds;
            _gravity = gravity;
        }

        public int Count => _peak.Length;

        /// <summary>Band <paramref name="band"/>'s cap, in column heights.</summary>
        public float this[int band] => _peak[band];

        /// <summary>
        /// Advances every cap by <paramref name="elapsed"/> seconds against the bands' present levels. A level at or above its
        /// cap pushes it up to the level and restarts its hold; otherwise it holds, then falls from rest at the gravity,
        /// speeding up, until it reaches the level and stops.
        /// </summary>
        public void Step(ReadOnlySpan<float> levels, float elapsed)
        {
            for (int b = 0; b < _peak.Length && b < levels.Length; b++)
            {
                float level = MathF.Max(0f, levels[b]);

                if (level >= _peak[b])
                {
                    _peak[b] = level;
                    _fall[b] = 0f;
                    _hold[b] = _holdSeconds;
                    continue;
                }

                //The hold is spent first, and what is left of the step after it falls: a step that straddles the end of the
                //hold must not hand the whole step to the hold, or the hold would round up to a frame and the cap's fall
                //would depend on the frame rate
                float dt = elapsed;

                if (_hold[b] > 0f)
                {
                    if (_hold[b] >= dt)
                    {
                        _hold[b] -= dt;
                        continue;
                    }

                    dt -= _hold[b];
                    _hold[b] = 0f;
                }

                //Constant acceleration, integrated exactly over the step, so the distance fallen in a time is the same
                //however many frames it was taken in
                _peak[b] -= _fall[b] * dt + 0.5f * _gravity * dt * dt;
                _fall[b] += _gravity * dt;

                if (_peak[b] <= level)
                {
                    _peak[b] = level;
                    _fall[b] = 0f;
                }
            }
        }
    }
}
