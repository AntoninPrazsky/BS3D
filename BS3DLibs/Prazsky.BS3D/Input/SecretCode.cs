using System.Collections.Generic;

namespace Prazsky.BS3D.Input
{
    /// <summary>
    /// A hidden code, entered one input at a time: the last few inputs are compared with a fixed sequence and the
    /// input that completes it says so. The one mechanism behind #230's codes (<c>SecretShotCode</c>, a pattern of
    /// shot outcomes, and <see cref="KonamiCode"/>, a pattern of keys), each of which is a subclass that names its
    /// own sequence.
    /// <para>
    /// A sliding window, not a state machine that restarts on a wrong input: whatever came before, the code
    /// completes on the input that makes the last <i>n</i> equal it. Once found it forgets everything, so the very
    /// next input cannot complete it a second time on the old ones.
    /// </para>
    /// </summary>
    public abstract class SecretCode<T>
    {
        private readonly T[] _code;
        private readonly T[] _last;
        private int _count;

        /// <param name="code">The sequence, oldest input first.</param>
        protected SecretCode(params T[] code)
        {
            _code = code;
            _last = new T[code.Length];
        }

        /// <summary>Forget the inputs so far.</summary>
        public void Reset() => _count = 0;

        /// <summary>One input; true when it completes the code.</summary>
        public bool Record(T input)
        {
            //Shift the window along by one: a handful of values, once an input
            for (int i = 1; i < _last.Length; i++) _last[i - 1] = _last[i];
            _last[^1] = input;
            if (_count < _last.Length) _count++;
            if (_count < _last.Length) return false;

            for (int i = 0; i < _code.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(_last[i], _code[i])) return false;

            _count = 0;
            return true;
        }
    }
}
