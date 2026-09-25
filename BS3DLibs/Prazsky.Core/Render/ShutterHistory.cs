using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A short record of one pose — a camera's view-projection, a barrel's world matrix — kept so the motion blur
    /// can ask <b>where it was a shutter ago</b> (#402).
    /// <para>
    /// <b>This is what makes the blur independent of the frame rate.</b> The textbook motion blur differences this
    /// frame against the last one, so its length is one frame's motion — at 144 Hz a fast flick of the barrel moves
    /// the muzzle under a tenth of the tube's own width, which is why #402's second comment measured "correct"
    /// motion blur as nearly invisible there. Differencing against the pose a fixed
    /// <see cref="MotionBlur.SHUTTER_SECONDS"/> ago instead gives the same smear at 60 Hz, at 144 Hz and at 500:
    /// what the exposure shows is a length of time, not a frame. The samples are interpolated, so a frame rate
    /// SLOWER than the shutter (one frame takes longer than it) still gets the pose from inside that frame.
    /// </para>
    /// <para>
    /// The interpolation is componentwise on the matrices, which is a straight line between the two samples for
    /// every vertex and exactly what the velocity pass then does with the result — it only ever needs a point
    /// on each vertex's path, never a matrix that is still a rotation. Fixed capacity, no allocation after
    /// construction: pushed once a frame per pose (BestPractices.md).
    /// </para>
    /// </summary>
    public sealed class ShutterHistory
    {
        //Enough for SHUTTER_SECONDS at ~1900 FPS; past that the oldest sample is simply a little younger than the
        //shutter and the smear a little shorter, which nobody at that frame rate will ever see.
        private const int CAPACITY = 64;

        private readonly float[] _times = new float[CAPACITY];
        private readonly Matrix[] _poses = new Matrix[CAPACITY];

        //_head is the NEWEST sample's slot; the older ones follow it backwards round the ring
        private int _head = -1;
        private int _count;

        /// <summary>Forgets everything — a camera cut, a new level: the next pose starts a fresh record, and until a
        /// second one arrives <see cref="At"/> answers with it, i.e. no motion.</summary>
        public void Clear()
        {
            _head = -1;
            _count = 0;
        }

        /// <summary>
        /// Records the pose at <paramref name="time"/> (seconds on any clock that only goes forward). A time that has
        /// not advanced overwrites the newest sample rather than adding a second one at the same instant.
        /// </summary>
        public void Push(float time, in Matrix pose)
        {
            if (_count > 0 && time <= _times[_head])
            {
                _poses[_head] = pose;
                return;
            }

            _head = (_head + 1) % CAPACITY;
            _times[_head] = time;
            _poses[_head] = pose;
            if (_count < CAPACITY) _count++;
        }

        /// <summary>The newest pose, or <paramref name="fallback"/> before anything has been pushed.</summary>
        public Matrix Latest(in Matrix fallback) => _count > 0 ? _poses[_head] : fallback;

        /// <summary>
        /// The pose at <paramref name="time"/>, interpolated between the two samples either side of it; the oldest
        /// sample when the record does not reach back that far, and <paramref name="fallback"/> when it is empty.
        /// </summary>
        public Matrix At(float time, in Matrix fallback)
        {
            if (_count == 0) return fallback;

            int newer = _head;
            for (int i = 1; i < _count; i++)
            {
                int older = (_head - i + CAPACITY) % CAPACITY;

                if (_times[older] <= time)
                {
                    float span = _times[newer] - _times[older];
                    float t = span > 0f ? Math.Clamp((time - _times[older]) / span, 0f, 1f) : 0f;
                    return Matrix.Lerp(_poses[older], _poses[newer], t);
                }

                newer = older;
            }

            //Older than anything kept: the oldest sample, which is the nearest thing the record has
            return _poses[newer];
        }
    }
}
