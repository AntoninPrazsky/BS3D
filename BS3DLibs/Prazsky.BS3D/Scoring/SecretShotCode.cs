namespace Prazsky.BS3D.Scoring
{
    /// <summary>
    /// <b>The secret shot code</b> — another of #230's easter eggs, found only by playing it: three shots that miss,
    /// two that land, three more that miss. The session reports every shot as it resolves (a landing, or a shot
    /// spent on the stone or past everything) and the last eight are compared with that pattern; the Game
    /// answers a completed one with a short fall of the campaign's confetti and the party popper's crack. Nothing else — no ball, no
    /// score — so, like the sleeping gun, it touches nothing a gate measures; the six misses it costs are the price
    /// of finding it.
    /// </summary>
    public sealed class SecretShotCode
    {
        //Oldest first: M M M, L L, M M M
        private static readonly bool[] CODE = { false, false, false, true, true, false, false, false };

        private readonly bool[] _last = new bool[8];
        private int _count;

        /// <summary>Forget the shots so far: a level starting.</summary>
        public void Reset() => _count = 0;

        /// <summary>One shot resolved; true when it completes the code.</summary>
        public bool Record(bool landed)
        {
            //Shift the window along by one: eight bools, once a shot
            for (int i = 1; i < _last.Length; i++) _last[i - 1] = _last[i];
            _last[^1] = landed;
            if (_count < _last.Length) _count++;
            if (_count < _last.Length) return false;

            for (int i = 0; i < CODE.Length; i++)
                if (_last[i] != CODE[i]) return false;

            //Once found, a fresh eight to find it again, so the ninth shot cannot complete it on the old ones
            _count = 0;
            return true;
        }
    }
}
